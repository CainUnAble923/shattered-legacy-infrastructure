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
  cc-P90, the feature-ideas inbox (shard-migration/tools/ideas.py; data testing/feature-ideas.json):
  GET  /api/ideas        {ideas, nextId, summary, areas, statuses, features (docs/feature-queue.md, read only)}
  GET  /api/ideas/copy   every 'new' idea as plain text for the features chat ({"text", "ids"})
  POST /api/ideas/add    {"title", "description", "area", "playtest"}
  POST /api/ideas/edit   {"id", and any of "title", "description", "area", "playtest"}
  POST /api/ideas/status {"id", "status": "new|taken|became|declined", "feature": "F-NN", "reason"}
  POST /api/ideas/taken  {"ids": [...]}  marks copied ideas 'taken to features chat'
  POST /api/ideas/delete {"id", "reason"}  (cc-P97: a reason is required; the id is never reused)
  cc-P97, the bug-reports inbox (shard-migration/tools/bugs.py; data testing/bug-reports.json, screenshots in
  testing/bug-reports/files/):
  GET  /api/bugs         {bugs, deleted, nextId, summary, severities, areas, statuses, version}
  GET  /api/bugs/copy    every 'new' report as plain text for the overseer chat ({"text", "ids"})
  GET  /api/bugs/file/B-001-1.png   a screenshot
  POST /api/bugs/add     {"title", "happened", "expected", "steps", "severity", "area", "playtest", "character",
                          "location"}  (the shard version is filled in here, never sent)
  POST /api/bugs/edit    {"id", and any of the fields above}
  POST /api/bugs/status  {"id", "status": "new|taken|duplicate|notbug|fixed", "dnum", "duplicateOf", "reason"}
  POST /api/bugs/taken   {"ids": [...]}  marks copied reports taken, pending their D-number
  POST /api/bugs/delete  {"id", "reason"}  (a reason is required; the id is never reused)
  POST /api/bugs/shot    {"id", "data": "<data URL or base64>"}  one PNG or JPG, 2 MB at most, 3 per report

cc-P105 (Haven step H1): the same service can run on Haven. All of it is off by default, so on the game PC it serves
and writes exactly what it did before.
  PLAYTESTS_DATA         where testing/ (play tests, SUMMARY.md, ideas, bug reports and their screenshots) is read and
                         written; default the notes root, as before. On Haven, a data volume apart from the notes clone.
  PLAYTESTS_LAN_PORT     a second listener, every request needing "Authorization: Bearer <PLAYTESTS_TOKEN>"
                         (or the token in PLAYTESTS_TOKEN_FILE); off (0) by default, and not started without a token:
    GET  /api/export          every file under testing/ as one bundle (shard-migration/tools/opsdata.py)
    POST /api/ingest/gamepc   the game PC's snapshot, at most 64 KB, kept with the time it arrived
    GET  /api/gamepc          that snapshot, its age, and "offline since" once it is over 3 minutes old
  ACCESS_TEAM_DOMAIN and ACCESS_AUD   both set: every request but /nav.js must carry a valid Cloudflare Access token
                         (shard-migration/tools/accessjwt.py), and its email is the writer identity, "by", on every
                         result, manual item, idea and bug report. Unset: no check, and "by" stays empty.
  REPO_STAMP             the notes clone's pull stamp ("<commit> <time>"), for "repo as of"; else git, else empty.
  /api/items "service" gains gamepc (receivedAt, offline, offlineSince), repo (commit, at) and identity (by, access).

All parsing and merging is in shard-migration/tools/playtests.py, the same file Import-PlayTests.ps1 runs, so
there is one set of rules. This file only serves. The importer runs on start and every IMPORT_SECONDS, so a
prompt whose notes end "Pnn DONE" is on the page within a minute with nobody doing anything.

LAN only: docker-compose.yml publishes it on 192.168.1.58 and 127.0.0.1, never through NPM or DNS. There is no
login: anyone on the home network can mark results. Standard library only (python:3-alpine). ASCII only.
"""
import argparse
import hmac
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
ap.add_argument("--host-root", default=os.environ.get("PLAYTESTS_HOST_ROOT", "D:\\UO\\shard-migration"),
                help="cc-P97: where --root is on the Windows box, for the screenshot paths in Copy new bugs")
ap.add_argument("--deployed", default=os.environ.get("SHARD_DEPLOYED", "/data/testworld/shard-commit.json"),
                help="cc-P97: the record Start-TestShard.ps1 writes after a healthy start; its version goes on a report")
# cc-P105, Haven step H1. Every one of these is off, or the old behaviour, by default.
ap.add_argument("--data", default=os.environ.get("PLAYTESTS_DATA", ""),
                help="cc-P105: where testing/ is read and written (default: --root, as before)")
ap.add_argument("--lan-bind", default=os.environ.get("PLAYTESTS_LAN_BIND", "0.0.0.0"))
ap.add_argument("--lan-port", type=int, default=int(os.environ.get("PLAYTESTS_LAN_PORT", "0") or 0),
                help="cc-P105: the token-guarded listener for export, ingest and the game PC snapshot (0: off)")
ap.add_argument("--token-file", default=os.environ.get("PLAYTESTS_TOKEN_FILE", ""))
ap.add_argument("--access-team", default=os.environ.get("ACCESS_TEAM_DOMAIN", ""),
                help="cc-P105: the Cloudflare Access team domain, e.g. sl.cloudflareaccess.com (with --access-aud: on)")
ap.add_argument("--access-aud", default=os.environ.get("ACCESS_AUD", ""))
ap.add_argument("--access-certs", default=os.environ.get("ACCESS_CERTS_URL", ""),
                help="cc-P105: tests only; default https://<team>/cdn-cgi/access/certs")
ap.add_argument("--repo-stamp", default=os.environ.get("REPO_STAMP", ""))
args = ap.parse_args()

sys.path.insert(0, args.lib)
import playtests as pt  # noqa: E402
import ideas  # noqa: E402  (cc-P90)
import bugs  # noqa: E402  (cc-P97)
import opsdata  # noqa: E402  (cc-P105)
import accessjwt  # noqa: E402  (cc-P105)

# cc-P105: testing/ lives here; the notes and docs stay under args.root.
DATA = args.data or args.root
TOKEN = os.environ.get("PLAYTESTS_TOKEN", "").strip()
if not TOKEN and args.token_file:
    try:
        with open(args.token_file, "r", encoding="ascii") as _f:
            TOKEN = _f.read().strip()
    except OSError:
        TOKEN = ""
VERIFIER = accessjwt.AccessVerifier(args.access_team, args.access_aud, certs_url=args.access_certs) if (args.access_team and args.access_aud) else None
_REPO = {"at": 0.0, "value": {"commit": "", "at": "", "source": ""}}

STATE = {"lastImport": "", "lastImportError": ""}
MAX_BODY = 16 * 1024
# cc-P97: one screenshot, base64 (4/3 of 2 MB) in a small JSON object.
MAX_SHOT_BODY = 3 * 1024 * 1024


def log(msg):
    print(time.strftime("%Y-%m-%d %H:%M:%S"), msg, flush=True)


def run_import():
    try:
        changed, _ = pt.import_all(args.root, write=True, log=log, data=DATA)
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
    path = os.path.join(DATA, pt.DATA_REL)
    return pt.ordered_doc(pt.load(path))


def repo_as_of():
    """cc-P105: how fresh the notes are, looked up at most once a minute (git is a subprocess)."""
    if time.time() - _REPO["at"] > 60:
        _REPO["value"] = opsdata.repo_as_of(args.root, args.repo_stamp)
        _REPO["at"] = time.time()
    return _REPO["value"]


def service_state(identity):
    st = dict(STATE)
    g = opsdata.gamepc_status(DATA)
    st["gamepc"] = {k: g[k] for k in ("receivedAt", "ageSeconds", "offline", "offlineSince")}
    st["repo"] = repo_as_of()
    st["identity"] = {"by": identity["by"], "access": VERIFIER is not None}
    return st


def deployed_version():
    """cc-P97: the test shard's version as deployed, from the record Start-TestShard.ps1 writes after a healthy start
    (cc-P96). Empty when there is none, so a report says (unknown) rather than a guess."""
    try:
        with open(args.deployed, "r", encoding="utf-8-sig") as f:
            rec = json.load(f)
    except (OSError, ValueError):
        return ""
    v = str(rec.get("version") or "").strip()
    c = str(rec.get("commit") or "").strip()[:7]
    if v and c:
        return "%s (test shard, %s)" % (v, c)
    return v


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

    def identity(self):
        """cc-P105: who is asking. Cloudflare Access (when configured) is the one source today; the shard login (H6)
        becomes a second source here, tried in turn. Returns {"by", "source"}; raises AccessDenied when Access is on
        and the request does not carry a valid token."""
        if VERIFIER is None:
            return {"by": "", "source": ""}
        claims = VERIFIER.verify(accessjwt.token_from(self.headers))
        return {"by": str(claims["email"]).strip().lower()[:60], "source": "access"}

    def who(self, p):
        """identity(), or None after answering 403 (every route but /nav.js, which holds no data)."""
        try:
            return self.identity()
        except accessjwt.AccessDenied as e:
            if p == "/nav.js":
                return {"by": "", "source": ""}
            log("access refused %s: %s" % (p, e))
            self.send_json(403, {"error": "Cloudflare Access is required: %s" % e})
            return None

    def do_GET(self):
        p = self.path.split("?", 1)[0]
        me = self.who(p)
        if me is None:
            return
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
            doc["service"] = service_state(me)
            return self.send_json(200, doc)
        if p == "/api/summary":
            try:
                s = read_doc()["summary"]
            except Exception as e:
                return self.send_json(500, {"error": str(e)})
            out = {k: s[k] for k in ("open", "failed", "passed", "later", "blocked", "total", "unloggedFails", "generatedAt")}
            # cc-P90: new ideas, for the Homepage widget, the shared menu's badge and the Shard Console.
            try:
                out["ideas_new"] = ideas.summary(ideas.load(DATA))["new"]
            except Exception:
                out["ideas_new"] = None
            # cc-P97: new bug reports, beside the ideas.
            try:
                out["bugs_new"] = bugs.summary(bugs.load(DATA))["new"]
            except Exception:
                out["bugs_new"] = None
            return self.send_json(200, out)
        if p == "/api/bugs":
            try:
                doc = bugs.ordered_doc(bugs.load(DATA))
            except Exception as e:
                return self.send_json(500, {"error": str(e)})
            doc.update(summary=bugs.summary(doc), severities=list(bugs.SEVERITIES), areas=list(bugs.AREAS),
                       statuses=bugs.STATUSES, version=deployed_version())
            return self.send_json(200, doc)
        if p == "/api/bugs/copy":
            try:
                doc = bugs.load(DATA)
                text = bugs.copy_text(doc, read_doc(), args.host_root)
            except Exception as e:
                return self.send_json(500, {"error": str(e)})
            return self.send_json(200, {"text": text, "ids": [b["id"] for b in doc["bugs"] if b["status"] == "new"]})
        if p.startswith("/api/bugs/file/"):
            name = p[len("/api/bugs/file/"):]
            try:
                with open(bugs.shot_path(DATA, name), "rb") as f:
                    data = f.read()
            except (KeyError, OSError):
                return self.send_json(404, {"error": "no such screenshot"})
            return self.send(200, data, "image/png" if name.endswith(".png") else "image/jpeg")
        if p == "/api/ideas":
            try:
                doc = ideas.load(DATA)
            except Exception as e:
                return self.send_json(500, {"error": str(e)})
            doc = ideas.ordered_doc(doc)
            doc.update(summary=ideas.summary(doc), areas=list(ideas.AREAS), statuses=ideas.STATUSES,
                       features=ideas.read_feature_queue(args.root))
            return self.send_json(200, doc)
        if p == "/api/ideas/copy":
            try:
                doc = ideas.load(DATA)
                text = ideas.copy_text(doc, read_doc())
            except Exception as e:
                return self.send_json(500, {"error": str(e)})
            return self.send_json(200, {"text": text, "ids": [i["id"] for i in doc["ideas"] if i["status"] == "new"]})
        return self.send_json(404, {"error": "not found"})

    def do_POST(self):
        p = self.path.split("?", 1)[0]
        me = self.who(p)
        if me is None:
            return
        by = me["by"]
        try:
            n = int(self.headers.get("Content-Length") or 0)
            if n > (MAX_SHOT_BODY if p == "/api/bugs/shot" else MAX_BODY):
                return self.send_json(413, {"error": "too large: a screenshot can be at most 2 MB" if p == "/api/bugs/shot" else "too large"})
            body = json.loads(self.rfile.read(n).decode("utf-8") or "{}") if n else {}
            if not isinstance(body, dict):
                raise ValueError("send a JSON object")
            if p == "/api/result":
                item = pt.set_result(DATA, str(body.get("id", "")), str(body.get("status", "")), str(body.get("note", "")), by=by)
                log("result %s = %s %s" % (item["id"], item["status"], item["note"]))
                return self.send_json(200, {"item": item})
            if p == "/api/bug":
                item = pt.set_bug(DATA, str(body.get("id", "")), str(body.get("bug", "")))
                return self.send_json(200, {"item": item})
            if p == "/api/manual":
                item = pt.add_manual(DATA, str(body.get("what", "")), str(body.get("expect", "")), by=by)
                log("manual %s: %s" % (item["id"], item["what"]))
                return self.send_json(200, {"item": item})
            if p == "/api/ideas/add":
                it = ideas.add(DATA, str(body.get("title", "")), str(body.get("description", "")),
                               str(body.get("area", "")), str(body.get("playtest", "")), by=by)
                log("idea %s: %s" % (it["id"], it["title"]))
                return self.send_json(200, {"idea": it})
            if p == "/api/ideas/edit":
                kw = {k: str(body[k]) for k in ("title", "description", "area", "playtest") if k in body}
                return self.send_json(200, {"idea": ideas.edit(DATA, str(body.get("id", "")), **kw)})
            if p == "/api/ideas/status":
                it = ideas.set_status(DATA, str(body.get("id", "")), str(body.get("status", "")),
                                      str(body.get("feature", "")), str(body.get("reason", "")))
                log("idea %s = %s %s%s" % (it["id"], it["status"], it["feature"], it["reason"]))
                return self.send_json(200, {"idea": it})
            if p == "/api/ideas/taken":
                ids = body.get("ids", [])
                if not isinstance(ids, list):
                    raise ValueError("ids must be a list")
                return self.send_json(200, {"taken": ideas.mark_taken(DATA, [str(i) for i in ids])})
            if p == "/api/ideas/delete":
                gone = ideas.delete(DATA, str(body.get("id", "")), str(body.get("reason", "")), by=by)
                log("idea %s deleted" % gone)
                return self.send_json(200, {"deleted": gone})
            # cc-P97: bug reports.
            if p == "/api/bugs/add":
                kw = {k: str(body.get(k, "")) for k in ("happened", "expected", "steps", "severity", "area", "playtest",
                                                        "character", "location")}
                b = bugs.add(DATA, str(body.get("title", "")), version=deployed_version(), by=by, **kw)
                log("bug %s: %s" % (b["id"], b["title"]))
                return self.send_json(200, {"bug": b})
            if p == "/api/bugs/edit":
                kw = {k: str(body[k]) for k in ("title", "happened", "expected", "steps", "severity", "area", "playtest",
                                                "character", "location") if k in body}
                return self.send_json(200, {"bug": bugs.edit(DATA, str(body.get("id", "")), **kw)})
            if p == "/api/bugs/status":
                b = bugs.set_status(DATA, str(body.get("id", "")), str(body.get("status", "")),
                                    str(body.get("dnum", "")), str(body.get("duplicateOf", "")), str(body.get("reason", "")))
                log("bug %s = %s %s%s%s" % (b["id"], b["status"], b["dnum"], b["duplicateOf"], b["reason"]))
                return self.send_json(200, {"bug": b})
            if p == "/api/bugs/taken":
                ids = body.get("ids", [])
                if not isinstance(ids, list):
                    raise ValueError("ids must be a list")
                return self.send_json(200, {"taken": bugs.mark_taken(DATA, [str(i) for i in ids])})
            if p == "/api/bugs/delete":
                gone = bugs.delete(DATA, str(body.get("id", "")), str(body.get("reason", "")), by=by)
                log("bug %s deleted" % gone)
                return self.send_json(200, {"deleted": gone})
            if p == "/api/bugs/shot":
                name = bugs.add_shot(DATA, str(body.get("id", "")), bugs.decode_shot(body.get("data", "")))
                log("bug screenshot %s" % name)
                return self.send_json(200, {"shot": name})
            if p == "/api/import":
                run_import()
                return self.send_json(200, {"service": STATE})
        except KeyError as e:
            return self.send_json(404, {"error": "no such item: %s" % e})
        except (ValueError, RuntimeError) as e:
            return self.send_json(400, {"error": str(e)})
        return self.send_json(404, {"error": "not found"})


class LanHandler(BaseHTTPRequestHandler):
    """cc-P105: the second listener (export, ingest, the game PC snapshot). Every request needs the bearer token."""
    server_version = "sl-play-tests-lan/1"
    send = Handler.send
    send_json = Handler.send_json

    def log_message(self, fmt, *a):
        log("lan %s %s" % (self.address_string(), fmt % a))

    def authorised(self):
        got = (self.headers.get("Authorization") or "").strip()
        ok = bool(TOKEN) and got.startswith("Bearer ") and hmac.compare_digest(
            got[len("Bearer "):].strip().encode("utf-8"), TOKEN.encode("utf-8"))
        if not ok:
            self.send_json(401, {"error": "a bearer token is required"})
        return ok

    def do_GET(self):
        if not self.authorised():
            return
        p = self.path.split("?", 1)[0]
        if p == "/api/export":
            return self.send_json(200, opsdata.export_bundle(DATA))
        if p == "/api/gamepc":
            return self.send_json(200, opsdata.gamepc_status(DATA))
        return self.send_json(404, {"error": "not found"})

    def do_POST(self):
        if not self.authorised():
            return
        p = self.path.split("?", 1)[0]
        if p != "/api/ingest/gamepc":
            return self.send_json(404, {"error": "not found"})
        try:
            n = int(self.headers.get("Content-Length") or 0)
            if n > opsdata.MAX_SNAPSHOT:
                return self.send_json(413, {"error": "too large"})
            rec = opsdata.save_gamepc(DATA, json.loads(self.rfile.read(n).decode("utf-8") or "null"))
        except ValueError as e:
            return self.send_json(400, {"error": str(e)})
        return self.send_json(200, {"receivedAt": rec["receivedAt"]})


def main():
    run_import()
    threading.Thread(target=importer_loop, daemon=True).start()
    if args.lan_port:
        if TOKEN:
            lan = ThreadingHTTPServer((args.lan_bind, args.lan_port), LanHandler)
            threading.Thread(target=lan.serve_forever, daemon=True).start()
            log("token listener on %s:%d (export, ingest, game PC)" % (args.lan_bind, args.lan_port))
        else:
            log("token listener NOT started: PLAYTESTS_LAN_PORT is set but there is no PLAYTESTS_TOKEN")
    srv = ThreadingHTTPServer((args.bind, args.port), Handler)
    log("serving %s:%d, data %s, import every %d s%s" % (args.bind, args.port, os.path.join(DATA, pt.DATA_REL),
                                                          args.import_seconds,
                                                          ", Cloudflare Access required" if VERIFIER else ""))
    srv.serve_forever()


if __name__ == "__main__":
    main()
