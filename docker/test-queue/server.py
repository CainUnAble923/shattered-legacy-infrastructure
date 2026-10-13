"""Play-test queue service (cc-P83). Replaces the read-only nginx page on port 8093.

Serves html/index.html and a small JSON API over D:\\UO\\shard-migration\\testing\\play-tests.json:
  GET  /                 the page
  GET  /status           the shard status dashboard (cc-P85): shard-migration/dashboard.html, read from disk on every
                         request and sent with Cache-Control: no-store, so it is always the file as it is now
  GET  /nav.js           the shared menu (cc-P88): html/nav.js, used by both pages here and by Homepage
  GET  /api/items        the whole document (summary, prompts, items)
  GET  /api/summary      counts only, for the Homepage dashboard's customapi widget
  POST /api/result       {"id": "P71-3", "status": "pass|fail|later|open", "note": "..."}  (a fail needs a note)
  POST /api/bug          {"id": "P71-3", "bug": "D86"}  marks a Fail as logged in the bug list
  POST /api/manual       {"what": "...", "expect": "..."}  adds an M-NNN item
  POST /api/import       runs the importer now

All parsing and merging is in shard-migration/tools/playtests.py, the same file Import-PlayTests.ps1 runs, so
there is one set of rules. This file only serves. The importer runs on start and every IMPORT_SECONDS, so a
prompt whose notes end "Pnn DONE" is on the page within a minute with nobody doing anything.

LAN only: docker-compose.yml publishes it on 192.168.1.58 and 127.0.0.1, never through NPM or DNS. There is no
login: anyone on the home network can mark results. Standard library only (python:3-alpine). ASCII only.
"""
import argparse
import json
import os
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ap = argparse.ArgumentParser()
ap.add_argument("--root", default=os.environ.get("PLAYTESTS_ROOT", "/data"),
                help="the shard-migration folder: notes/ and docs/ are read, testing/ is written")
ap.add_argument("--lib", default=os.environ.get("PLAYTESTS_LIB", "/app/tools"), help="the folder holding playtests.py")
ap.add_argument("--html", default=os.environ.get("PLAYTESTS_HTML", os.path.join(os.path.dirname(os.path.abspath(__file__)), "html")))
ap.add_argument("--bind", default=os.environ.get("PLAYTESTS_BIND", "0.0.0.0"))
ap.add_argument("--port", type=int, default=int(os.environ.get("PLAYTESTS_PORT", "8000")))
ap.add_argument("--import-seconds", type=int, default=int(os.environ.get("IMPORT_SECONDS", "60")))
ap.add_argument("--status-page", default=os.environ.get("STATUS_PAGE", "/data/site/dashboard.html"),
                help="cc-P85: the dashboard file served at /status (read only)")
args = ap.parse_args()

sys.path.insert(0, args.lib)
import playtests as pt  # noqa: E402

STATE = {"lastImport": "", "lastImportError": ""}
MAX_BODY = 16 * 1024


def log(msg):
    print(time.strftime("%Y-%m-%d %H:%M:%S"), msg, flush=True)


def run_import():
    try:
        changed, _ = pt.import_all(args.root, write=True, log=log)
        STATE["lastImport"] = pt.now_iso()
        STATE["lastImportError"] = ""
        if changed:
            log("import: play-tests.json updated")
    except Exception as e:  # never let a bad notes file stop the page
        STATE["lastImportError"] = str(e)
        log("import FAILED: %s" % e)


def importer_loop():
    while True:
        time.sleep(max(10, args.import_seconds))
        run_import()


def read_doc():
    path = os.path.join(args.root, pt.DATA_REL)
    return pt.ordered_doc(pt.load(path))


class Handler(BaseHTTPRequestHandler):
    server_version = "sl-play-tests/1"

    def log_message(self, fmt, *a):
        if not self.path.startswith("/api/summary"):
            log("%s %s" % (self.address_string(), fmt % a))

    def send(self, code, body, ctype="application/json; charset=utf-8"):
        data = body if isinstance(body, bytes) else body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def send_json(self, code, obj):
        self.send(code, json.dumps(obj, indent=1))

    def do_GET(self):
        p = self.path.split("?", 1)[0]
        if p in ("/", "/index.html"):
            try:
                with open(os.path.join(args.html, "index.html"), "rb") as f:
                    return self.send(200, f.read(), "text/html; charset=utf-8")
            except OSError as e:
                return self.send(500, "page missing: %s" % e, "text/plain; charset=utf-8")
        if p == "/nav.js":
            # cc-P88: the one menu file, read fresh each time like the page itself.
            try:
                with open(os.path.join(args.html, "nav.js"), "rb") as f:
                    return self.send(200, f.read(), "text/javascript; charset=utf-8")
            except OSError as e:
                return self.send(404, "// nav.js missing: %s" % e, "text/javascript; charset=utf-8")
        if p in ("/status", "/status/"):
            # cc-P85: the dashboard, read fresh each time (progress.py rewrites it on every commit).
            try:
                with open(args.status_page, "rb") as f:
                    return self.send(200, f.read(), "text/html; charset=utf-8")
            except OSError as e:
                return self.send(500, "dashboard missing: %s" % e, "text/plain; charset=utf-8")
        if p == "/api/items":
            try:
                doc = read_doc()
            except Exception as e:
                return self.send_json(500, {"error": str(e)})
            doc["service"] = dict(STATE)
            return self.send_json(200, doc)
        if p == "/api/summary":
            try:
                s = read_doc()["summary"]
            except Exception as e:
                return self.send_json(500, {"error": str(e)})
            return self.send_json(200, {k: s[k] for k in ("open", "failed", "passed", "later", "blocked", "total", "unloggedFails", "generatedAt")})
        return self.send_json(404, {"error": "not found"})

    def do_POST(self):
        p = self.path.split("?", 1)[0]
        try:
            n = int(self.headers.get("Content-Length") or 0)
            if n > MAX_BODY:
                return self.send_json(413, {"error": "too large"})
            body = json.loads(self.rfile.read(n).decode("utf-8") or "{}") if n else {}
            if not isinstance(body, dict):
                raise ValueError("send a JSON object")
            if p == "/api/result":
                item = pt.set_result(args.root, str(body.get("id", "")), str(body.get("status", "")), str(body.get("note", "")))
                log("result %s = %s %s" % (item["id"], item["status"], item["note"]))
                return self.send_json(200, {"item": item})
            if p == "/api/bug":
                item = pt.set_bug(args.root, str(body.get("id", "")), str(body.get("bug", "")))
                return self.send_json(200, {"item": item})
            if p == "/api/manual":
                item = pt.add_manual(args.root, str(body.get("what", "")), str(body.get("expect", "")))
                log("manual %s: %s" % (item["id"], item["what"]))
                return self.send_json(200, {"item": item})
            if p == "/api/import":
                run_import()
                return self.send_json(200, {"service": STATE})
        except KeyError as e:
            return self.send_json(404, {"error": "no such item: %s" % e})
        except (ValueError, RuntimeError) as e:
            return self.send_json(400, {"error": str(e)})
        return self.send_json(404, {"error": "not found"})


def main():
    run_import()
    threading.Thread(target=importer_loop, daemon=True).start()
    srv = ThreadingHTTPServer((args.bind, args.port), Handler)
    log("serving %s:%d, data %s, import every %d s" % (args.bind, args.port, os.path.join(args.root, pt.DATA_REL), args.import_seconds))
    srv.serve_forever()


if __name__ == "__main__":
    main()
