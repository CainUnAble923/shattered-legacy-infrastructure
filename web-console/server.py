"""The web console, phase 1 (cc-P155): the Shard Console's status, read only, for the phone and the work PC.

Two listeners, both on 127.0.0.1 only (nothing on the LAN, nothing public):
  127.0.0.1:8095  local: this PC's browser. Allowed as the PowerShell console is: anything on this PC is Chase.
  127.0.0.1:8096  tailnet: reached only through the uoconsole sidecar (web-console/tailscale), whose Tailscale Serve
                  gives https://uoconsole.tail81098d.ts.net/ Tailscale's certificate and sets Tailscale-User-Login. A
                  request is answered only when that login is allowed (panels.decide); anything else gets "not allowed".

  GET /                  the page (static/index.html; app.js and app.css beside it)
  GET /art/<slot>.<ext>  the console art (scripts/console-art), by slot name only
  GET /api/status        the status file both consoles read (scripts/shard-console-cache/console-status.json, written by
                         scripts/Write-ConsoleStatus.ps1 or the Shard Console), with the panels built here: needs you,
                         timeline, links. ?refresh=1 asks the collector for a fresh read (at most every 10 s)
  GET /api/logs?source=<key>&q=<text>&n=<lines>   the tail of one log, read only, searched, redacted
  GET /api/health        {ok}
Every other method is refused: phase 1 changes nothing. Every string sent is passed through panels.redact_tree.

The collector runs as a child: Write-ConsoleStatus.ps1 -Loop -ParentPid <this>, restarted if it ends.
Standard library only (like docker/test-queue/server.py): nothing to install on the game PC. ASCII only.
"""
import argparse
import json
import mimetypes
import os
import re
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import panels  # noqa: E402

DEFAULT_REPO = os.path.dirname(HERE)
DEFAULT_NOTES = r"D:\UO\shard-migration"
DEFAULT_DATA = r"D:\UO\web-console"
TAILSCALE_EXE = r"C:\Program Files\Tailscale\tailscale.exe"
NO_WINDOW = 0x08000000 if os.name == "nt" else 0
ART_NAME = re.compile(r"^[a-z0-9][a-z0-9-]{0,40}\.(png|gif|jpg|jpeg|webp)$")
LOOPBACK = ("127.0.0.1", "::1", "localhost")


class Console:
    """What the handlers read, refreshed in the background so a request never waits on git, docker or the network."""

    def __init__(self, repo, notes, data, status_file=None, collector=True):
        self.repo, self.notes, self.data = repo, notes, data
        self.status_file = status_file or os.path.join(repo, "scripts", "shard-console-cache", "console-status.json")
        self.settings_file = os.path.join(data, "settings.json")
        self.lock = threading.Lock()
        self.allowed = []
        self.allowed_from = ""
        self.allowed_at = 0.0
        self.panels_cache = (0.0, None)
        self.checks = {}
        self.last_refresh_ask = 0.0
        self.collector = None
        self.extra_links = self._load_json(os.path.join(HERE, "links.json"), {}).get("links", [])
        if collector:
            threading.Thread(target=self._collector_loop, daemon=True).start()
            threading.Thread(target=self._link_loop, daemon=True).start()

    # --- who may look ---------------------------------------------------------------------------------------------
    def allowed_logins(self):
        """settings.json's allowedLogins (outside git, D:\\UO\\web-console); without it, the login this PC's own
        Tailscale is signed in as (Chase's), looked up again every 5 minutes. Never written to a repo or a log."""
        now = time.time()
        if now - self.allowed_at < 300 and self.allowed_from:
            return self.allowed
        s = self._load_json(self.settings_file, None)
        if isinstance(s, dict) and isinstance(s.get("allowedLogins"), list):
            self.allowed, self.allowed_from = [str(x) for x in s["allowedLogins"]], "settings.json"
        else:
            self.allowed, self.allowed_from = self._own_login(), "this PC's Tailscale login"
        self.allowed_at = now
        return self.allowed

    @staticmethod
    def _own_login():
        try:
            out = subprocess.run([TAILSCALE_EXE, "status", "--json"], capture_output=True, timeout=10, creationflags=NO_WINDOW)
            d = json.loads(out.stdout.decode("utf-8", "replace"))
            u = (d.get("User") or {}).get(str((d.get("Self") or {}).get("UserID")), {})
            return [u["LoginName"]] if u.get("LoginName") else []
        except Exception:
            return []

    # --- the status file and the collector ------------------------------------------------------------------------
    def status(self):
        doc = self._load_json(self.status_file, None)
        age = None
        if isinstance(doc, dict) and doc.get("generatedAt"):
            try:
                age = int(time.time() - time.mktime(time.strptime(doc["generatedAt"][:19], "%Y-%m-%dT%H:%M:%S")))
            except ValueError:
                age = None
        return doc if isinstance(doc, dict) else None, age

    def ask_refresh(self):
        now = time.time()
        if now - self.last_refresh_ask < 10:
            return False
        self.last_refresh_ask = now
        try:
            os.makedirs(os.path.dirname(self.status_file), exist_ok=True)
            with open(self.status_file + ".request", "w", encoding="ascii") as f:
                f.write("web console\n")
            return True
        except OSError:
            return False

    def _collector_loop(self):
        script = os.path.join(self.repo, "scripts", "Write-ConsoleStatus.ps1")
        os.makedirs(self.data, exist_ok=True)
        log = os.path.join(self.data, "collector.log")
        while True:
            try:
                if os.path.exists(log) and os.path.getsize(log) > 2_000_000:
                    os.replace(log, log + ".1")
                with open(log, "a", encoding="utf-8") as out:
                    self.collector = subprocess.Popen(
                        ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Loop",
                         "-ParentPid", str(os.getpid())], stdout=out, stderr=subprocess.STDOUT, creationflags=NO_WINDOW)
                    self.collector.wait()
            except Exception as e:
                with open(log, "a", encoding="utf-8") as out:
                    out.write("collector failed to start: %s\n" % e)
            time.sleep(10)

    # --- links ----------------------------------------------------------------------------------------------------
    def _link_loop(self):
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE   # the check only asks whether something answers (self-signed Portainer)

        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self, *a, **k):
                return None
        opener = urllib.request.build_opener(NoRedirect, urllib.request.HTTPSHandler(context=ctx))
        while True:
            for e in self.extra_links:
                url = e.get("check") or e.get("url")
                try:
                    r = opener.open(urllib.request.Request(url, method="GET", headers={"User-Agent": "uoconsole-links"}), timeout=4)
                    self.checks[e["key"]] = {"up": True, "why": "HTTP %s" % r.status}
                    r.close()
                except urllib.error.HTTPError as x:
                    self.checks[e["key"]] = {"up": x.code < 500, "why": "HTTP %s" % x.code}
                except Exception as x:
                    self.checks[e["key"]] = {"up": False, "why": str(getattr(x, "reason", x))[:120]}
            time.sleep(60)

    # --- the panels -----------------------------------------------------------------------------------------------
    def panels(self, doc):
        at, cached = self.panels_cache
        if cached is not None and time.time() - at < 15:
            return cached
        playtests = self._playtests()
        decisions = self._load_json(os.path.join(self.notes, "testing", "decisions.json"), None)
        chores = panels.parse_chores(self._read_text(os.path.join(self.notes, "notes", "chores.md")))
        services = self._load_json(os.path.join(self.repo, "docker", "haven", "services.json"), None)
        out = {
            "needsYou": panels.needs_you(doc, playtests, decisions, chores, services),
            "decisionsBuilt": decisions is not None,
            "timeline": panels.timeline(self._commits(), self._names(os.path.join(self.repo, "scripts", "shard-console-output")),
                                        self._names(os.path.join(self.repo, "scripts", "shard-console-output", "deploys")),
                                        ((doc or {}).get("golem") or {}).get("finished"),
                                        self._dirs(os.path.join(self.notes, "testing", "bot-runs"))),
            "links": panels.links(doc, self.extra_links, self.checks, self._repos()),
            "playTestsMine": (playtests or {}).get("who", {}).get("mine"),
        }
        self.panels_cache = (time.time(), out)
        return out

    def _playtests(self):
        tools = os.path.join(self.notes, "tools")
        if tools not in sys.path:
            sys.path.insert(0, tools)
        try:
            import playtests as pt
            import playwho
            doc = pt.ordered_doc(pt.load(os.path.join(self.notes, pt.DATA_REL)))
            return playwho.annotate(doc, *playwho.load_sources([self.notes]))
        except Exception:
            return None

    def _commits(self):
        out = []
        for name, path in (("shard", self.repo), ("notes", self.notes)):
            try:
                r = subprocess.run(["git", "-C", path, "--no-optional-locks", "log", "-25", "--format=%H%x09%cI%x09%an%x09%s"],
                                   capture_output=True, timeout=10, creationflags=NO_WINDOW)
                out += panels.parse_git_log(r.stdout.decode("utf-8", "replace"), name)
            except Exception:
                pass
        return out

    def _repos(self):
        out = []
        for name, path in (("shattered-legacy-infrastructure", self.repo), ("shard-migration", self.notes)):
            try:
                r = subprocess.run(["git", "-C", path, "remote", "get-url", "origin"], capture_output=True, timeout=5, creationflags=NO_WINDOW)
                url = r.stdout.decode("utf-8", "replace").strip()
                if url.startswith("https://"):
                    out.append({"name": name, "url": url[:-4] if url.endswith(".git") else url})
            except Exception:
                pass
        return out

    # --- logs -----------------------------------------------------------------------------------------------------
    def log_sources(self):
        """key -> (label, how). Fixed here: a page can only pick a key, never a path."""
        out_dir = os.path.join(self.repo, "scripts", "shard-console-output")
        return {
            "golem": ("The Golem's log", ("file", r"D:\UO\golem\golem.log")),
            "console": ("The Shard Console's action log", ("file", os.path.join(self.repo, "scripts", "shard-console.log"))),
            "shard": ("The test shard (docker logs sl-modernuo-test)", ("docker", "sl-modernuo-test")),
            "actions": ("The newest console action transcript", ("newest", out_dir)),
            "deploys": ("The newest Haven deploy transcript", ("newest", os.path.join(out_dir, "deploys"))),
            "bot": ("The newest overnight bot run", ("bot", os.path.join(self.notes, "testing", "bot-runs"))),
            "collector": ("The status collector", ("file", os.path.join(self.data, "collector.log"))),
        }

    def log_lines(self, key, name=""):
        src = self.log_sources().get(key)
        if not src:
            return None, ""
        how, where = src[1]
        if how == "file":
            return self._tail_file(where), where
        if how == "docker":
            try:
                r = subprocess.run(["docker", "logs", "--tail", "2000", "-t", where], capture_output=True, timeout=15, creationflags=NO_WINDOW)
                return (r.stdout + r.stderr).decode("utf-8", "replace").splitlines(), "docker logs " + where
            except Exception as e:
                return ["docker logs failed: %s" % e], "docker logs " + where
        if how == "newest":
            names = self._names(where)
            if name and name in names:
                pick = name
            else:
                pick = names[-1] if names else ""
            return (self._tail_file(os.path.join(where, pick)) if pick else []), pick
        if how == "bot":
            runs = self._dirs(where)
            run = name if name in runs else (runs[-1] if runs else "")
            return (self._tail_file(os.path.join(where, run, "run.log")) if run else []), run
        return None, ""

    def art(self):
        """The console art slots that have a file (scripts/console-art), so the page asks only for those."""
        try:
            return sorted(n for n in os.listdir(os.path.join(self.repo, "scripts", "console-art")) if ART_NAME.match(n))
        except OSError:
            return []

    # --- small readers --------------------------------------------------------------------------------------------
    @staticmethod
    def _tail_file(path, max_bytes=400_000):
        try:
            with open(path, "rb") as f:
                f.seek(0, os.SEEK_END)
                size = f.tell()
                f.seek(max(0, size - max_bytes))
                return f.read().decode("utf-8", "replace").splitlines()[-5000:]
        except OSError:
            return []

    @staticmethod
    def _names(folder):
        try:
            return sorted(n for n in os.listdir(folder) if n.lower().endswith(".log"))
        except OSError:
            return []

    @staticmethod
    def _dirs(folder):
        try:
            return sorted(n for n in os.listdir(folder) if os.path.isdir(os.path.join(folder, n)))
        except OSError:
            return []

    @staticmethod
    def _read_text(path):
        try:
            with open(path, "r", encoding="utf-8-sig") as f:
                return f.read()
        except OSError:
            return ""

    @staticmethod
    def _load_json(path, default):
        try:
            with open(path, "r", encoding="utf-8-sig") as f:
                return json.load(f)
        except (OSError, ValueError):
            return default


def make_handler(console, listener, access_log=None):
    class Handler(BaseHTTPRequestHandler):
        server_version = "uoconsole"
        sys_version = ""

        def log_message(self, fmt, *a):  # the access log below, never headers
            pass

        def _note(self, code, why):
            if access_log:
                try:
                    with open(access_log, "a", encoding="utf-8") as f:
                        f.write("%s %s %s %s %s\n" % (time.strftime("%Y-%m-%d %H:%M:%S"), listener, code, self.path.split("?")[0][:80], why))
                except OSError:
                    pass

        def _send(self, code, body, ctype):
            data = body if isinstance(body, bytes) else body.encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("X-Frame-Options", "DENY")
            self.send_header("Referrer-Policy", "no-referrer")
            self.send_header("Content-Security-Policy", "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'none'")
            self.end_headers()
            if self.command != "HEAD":
                self.wfile.write(data)

        def _json(self, code, value):
            self._send(code, json.dumps(panels.redact_tree(value), ensure_ascii=True), "application/json; charset=utf-8")

        def _allowed(self):
            if self.client_address[0] not in LOOPBACK:
                self._note(403, "not loopback")
                self._send(403, "not allowed\n", "text/plain; charset=utf-8")
                return False
            ok, why = panels.decide(listener, self.headers, console.allowed_logins())
            if not ok:
                self._note(403, why)
                self._send(403, "not allowed\n", "text/plain; charset=utf-8")
                return False
            return True

        def do_HEAD(self):
            self.do_GET()

        def do_GET(self):
            if not self._allowed():
                return
            u = urlparse(self.path)
            p, q = u.path, parse_qs(u.query)
            if p in ("/", "/index.html"):
                return self._static("index.html")
            if p in ("/app.js", "/app.css"):
                return self._static(p[1:])
            if p.startswith("/art/"):
                name = p[5:]
                path = os.path.join(console.repo, "scripts", "console-art", name)
                if not ART_NAME.match(name) or not os.path.isfile(path):
                    return self._send(404, "no such art\n", "text/plain; charset=utf-8")
                with open(path, "rb") as f:
                    return self._send(200, f.read(), mimetypes.guess_type(name)[0] or "application/octet-stream")
            if p == "/api/health":
                return self._json(200, {"ok": True})
            if p == "/api/status":
                asked = console.ask_refresh() if q.get("refresh") == ["1"] else False
                doc, age = console.status()
                return self._json(200, {"status": doc, "ageSeconds": age, "refreshAsked": asked,
                                        "panels": console.panels(doc), "logSources": {k: v[0] for k, v in console.log_sources().items()},
                                        "art": console.art(),
                                        "server": {"now": time.strftime("%Y-%m-%dT%H:%M:%S"), "listener": listener}})
            if p == "/api/logs":
                key = (q.get("source") or ["golem"])[0]
                lines, what = console.log_lines(key, (q.get("name") or [""])[0])
                if lines is None:
                    return self._json(404, {"error": "no such log"})
                return self._json(200, {"source": key, "what": os.path.basename(what) if what else "",
                                        "lines": panels.tail(lines, (q.get("n") or ["200"])[0], (q.get("q") or [""])[0])})
            self._send(404, "not found\n", "text/plain; charset=utf-8")

        def _refuse(self):
            self._note(405, "method " + self.command)
            self._send(405, "read only: phase 1 changes nothing\n", "text/plain; charset=utf-8")

        do_POST = do_PUT = do_DELETE = do_PATCH = do_OPTIONS = _refuse

        def _static(self, name):
            path = os.path.join(HERE, "static", name)
            ctype = {"html": "text/html", "js": "application/javascript", "css": "text/css"}[name.rsplit(".", 1)[1]]
            with open(path, "rb") as f:
                self._send(200, f.read(), ctype + "; charset=utf-8")
    return Handler


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--bind", default="127.0.0.1")
    ap.add_argument("--local-port", type=int, default=8095)
    ap.add_argument("--tailnet-port", type=int, default=8096)
    ap.add_argument("--repo", default=DEFAULT_REPO)
    ap.add_argument("--notes", default=DEFAULT_NOTES)
    ap.add_argument("--data", default=DEFAULT_DATA)
    ap.add_argument("--status-file", default=None)
    ap.add_argument("--no-collector", action="store_true", help="tests: read the status file only")
    a = ap.parse_args(argv)
    if a.bind not in ("127.0.0.1", "::1"):
        sys.exit("refused: the web console listens on the loopback only (127.0.0.1); the tailnet reaches it through the sidecar")
    os.makedirs(a.data, exist_ok=True)
    console = Console(a.repo, a.notes, a.data, a.status_file, collector=not a.no_collector)
    access = os.path.join(a.data, "access.log")
    tail_srv = ThreadingHTTPServer((a.bind, a.tailnet_port), make_handler(console, panels.TAILNET, access))
    threading.Thread(target=tail_srv.serve_forever, daemon=True).start()
    local = ThreadingHTTPServer((a.bind, a.local_port), make_handler(console, panels.LOCAL, access))
    print("web console on http://%s:%d/ (local) and %s:%d (tailnet, through the uoconsole sidecar)" % (a.bind, a.local_port, a.bind, a.tailnet_port), flush=True)
    local.serve_forever()


if __name__ == "__main__":
    main()
