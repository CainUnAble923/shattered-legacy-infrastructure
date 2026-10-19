"""The web console (cc-P155, phase 1; cc-P182, phase 2): the Shard Console's status and the Golem's buttons, for the phone.

Two listeners, both on 127.0.0.1 only (nothing on the LAN, nothing public):
  127.0.0.1:8095  local: this PC's browser. Allowed as the PowerShell console is: anything on this PC is Chase.
  127.0.0.1:8096  tailnet: reached only through the uoconsole sidecar (web-console/tailscale), whose Tailscale Serve
                  gives https://uoconsole.tail81098d.ts.net/ Tailscale's certificate and sets Tailscale-User-Login, and
                  whose proof forwarder (tailscale/proof_proxy.py) sets X-UOConsole-Proof to the secret in
                  D:/UO/web-console/proof.key. A request is answered only with that proof and an allowed login
                  (panels.decide); anything else gets "not allowed". Only this listener may act (cc-P182).

  GET /                  the page (static/index.html; app.js and app.css beside it)
  GET /art/<slot>.<ext>  the console art (scripts/console-art), by slot name only
  GET /api/status        the status file both consoles read (scripts/shard-console-cache/console-status.json, written by
                         scripts/Write-ConsoleStatus.ps1 or the Shard Console), with the panels built here: needs you,
                         timeline, links. ?refresh=1 asks the collector for a fresh read (at most every 10 s)
  GET /api/logs?source=<key>&q=<text>&n=<lines>   the tail of one log, read only, searched, redacted
  GET /api/health        {ok}
  POST /api/action       cc-P182: {key, typed, inputs} runs one Golem button (golem-actions.json) through the Shard
                         Console's own code (scripts/Invoke-WebConsoleAction.ps1: Get-ActionPlan, Invoke-Plan). Only on
                         the tailnet listener, only from this page (X-UOConsole-Action, same origin, JSON), only with
                         the typed word, one at a time. Appended to D:/UO/web-console/actions.log (time, who, button,
                         result; never a header, the word or a token).
  GET /api/action?since=<n>  the running or last action: its lines from n on, its result line
Every other method is refused. Every string sent is passed through panels.redact_tree.

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
LOCAL_READ_ONLY = ("This PC's own page (127.0.0.1:8095) is read only: the Golem's buttons work through "
                   "https://uoconsole.tail81098d.ts.net/ (Tailscale on), or use the Shard Console here.")
DEFAULT_NOTES = r"D:\UO\shard-migration"
DEFAULT_DATA = r"D:\UO\web-console"
TAILSCALE_EXE = r"C:\Program Files\Tailscale\tailscale.exe"
NO_WINDOW = 0x08000000 if os.name == "nt" else 0
ART_NAME = re.compile(r"^[a-z0-9][a-z0-9-]{0,40}\.(png|gif|jpg|jpeg|webp)$")
LOOPBACK = ("127.0.0.1", "::1", "localhost")
ACTIONS_FILE = os.path.join(HERE, "golem-actions.json")
PANE_LINE = re.compile(r"^\[\[shard-console:([a-z]+)\]\](.*)$")
# cc-P182: what a page may pass for each input, checked before the runner is started (argv, never a shell).
INPUT_RULES = {
    "model": (re.compile(r"^(?i:fable|opus|sonnet)$"), "-Model", lambda v: v[:1].upper() + v[1:].lower()),
    "cap": (re.compile(r"^(?i:all|\d{1,3})$"), "-Cap", lambda v: v.lower()),
    "until": (re.compile(r"^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}$"), "-Until", lambda v: v.replace("T", " ")),
    "start": (re.compile(r"^([01]?\d|2[0-3]):[0-5]\d$"), "-NightStart", lambda v: v),
    "end": (re.compile(r"^([01]?\d|2[0-3]):[0-5]\d$"), "-NightEnd", lambda v: v),
    "prompt": (re.compile(r"^[Pp]?\d{1,4}$"), "-Prompt", lambda v: "P" + v.lstrip("Pp")),
}
TYPED = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 ]{0,39}$")
ACTION_TIMEOUT = 600   # seconds; the Golem's scripts take a few


class Console:
    """What the handlers read, refreshed in the background so a request never waits on git, docker or the network."""

    def __init__(self, repo, notes, data, status_file=None, collector=True, runner=None):
        self.repo, self.notes, self.data = repo, notes, data
        # cc-P182: the Golem buttons. The runner is the Shard Console's (tests pass a fake that records each call).
        self.runner = runner or ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                                 os.path.join(repo, "scripts", "Invoke-WebConsoleAction.ps1")]
        self.proof_file = os.path.join(data, "proof.key")
        self.action_log = os.path.join(data, "actions.log")
        self._proof = (None, "")
        self._buttons = (None, [])
        self.job = None
        self.job_lock = threading.Lock()
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

    def proof(self):
        """cc-P182: the secret only the sidecar's proof forwarder and this service hold (proof.key, outside git, made by
        Install-WebConsole.ps1). Read again when the file changes; '' when there is none (the tailnet is then refused)."""
        try:
            st = os.stat(self.proof_file)
            stamp = (st.st_mtime, st.st_size)
        except OSError:
            return ""
        if self._proof[0] != stamp:
            try:
                with open(self.proof_file, "r", encoding="ascii") as f:
                    self._proof = (stamp, f.read().strip())
            except (OSError, ValueError):
                self._proof = (stamp, "")
        return self._proof[1]

    # --- the Golem buttons (cc-P182) ------------------------------------------------------------------------------
    def buttons(self):
        try:
            stamp = os.path.getmtime(ACTIONS_FILE)
        except OSError:
            return []
        if self._buttons[0] != stamp:
            self._buttons = (stamp, (self._load_json(ACTIONS_FILE, {}) or {}).get("actions") or [])
        return self._buttons[1]

    def public_buttons(self):
        keep = ("key", "label", "word", "inputs", "when", "disabled", "help", "art")
        return [{k: b[k] for k in keep if k in b} for b in self.buttons()]

    def check_request(self, entry, typed, inputs):
        """(None, args) when the runner may start, else (http code, why). The word is checked again by the runner
        against the console's own plan; here it is the button's word, the picked prompt, or the shape of a prompt."""
        if entry.get("disabled"):
            return (409, entry["disabled"]), None
        typed = (typed if isinstance(typed, str) else "").strip()
        inputs = inputs if isinstance(inputs, dict) else {}
        args = []
        clean = {}
        for name in entry.get("inputs") or []:
            v = inputs.get(name)
            if v is None or str(v).strip() == "":
                continue
            v = str(v).strip()
            rx, flag, norm = INPUT_RULES[name]
            if not rx.match(v):
                return (400, "%s is not allowed: %r" % (name, v[:40])), None
            clean[name] = norm(v)
            args += [flag, clean[name]]
        word = entry.get("word") or ""
        if word == "#prompt":
            expected = clean.get("prompt")
            if not expected:
                return (400, "pick a prompt first"), None
        elif word == "#stopped":
            expected = None
            if not re.match(r"^[Pp]\d{1,4}$", typed):
                return (400, "type the stopped prompt's number (P<nn>) to confirm"), None
        else:
            expected = word
        if expected is not None and typed.lower() != expected.lower():
            return (400, "type %s to confirm" % expected), None
        if not TYPED.match(typed):
            return (400, "the typed word may hold letters, digits and spaces only"), None
        return None, (["-Key", entry["key"], "-Typed", typed] + args, clean)

    def start_action(self, entry, typed, inputs, who):
        """(http code, body). One at a time: a second request while one runs is refused with what is running."""
        err, ok = self.check_request(entry, typed, inputs)
        if err:
            self.log_action(entry, who, "refused: " + err[1], None, {})
            return err[0], {"error": err[1]}
        args, clean = ok
        with self.job_lock:
            if self.job and self.job["running"]:
                j = self.job
                return 409, {"error": "busy: %s is running (since %s)" % (j["label"], j["started"]),
                             "running": {"key": j["key"], "label": j["label"], "started": j["started"]}}
            job = {"id": int(time.time() * 1000), "key": entry["key"], "label": entry.get("label") or entry["key"],
                   "action": entry.get("action") or "", "who": who, "started": time.strftime("%Y-%m-%dT%H:%M:%S"), "ended": None,
                   "running": True, "code": None, "result": "", "lines": [], "inputs": clean}
            self.job = job
        threading.Thread(target=self._run_job, args=(job, args), daemon=True).start()
        return 202, {"id": job["id"], "label": job["label"]}

    def _run_job(self, job, args):
        code = None
        try:
            proc = subprocess.Popen(self.runner + args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL,
                                    creationflags=NO_WINDOW, cwd=self.repo)
            killer = threading.Timer(ACTION_TIMEOUT, proc.kill)
            killer.start()
            try:
                for raw in proc.stdout:
                    line = raw.decode("utf-8", "replace").rstrip("\r\n")
                    m = PANE_LINE.match(line)
                    color, text = (m.group(1), m.group(2)) if m else ("normal", line)
                    if len(job["lines"]) < 3000:
                        job["lines"].append({"color": color, "text": panels.redact(text)})
                code = proc.wait()
            finally:
                killer.cancel()
        except Exception as e:  # the runner could not start
            job["lines"].append({"color": "red", "text": panels.redact("the runner did not start: %s" % e)})
            code = -1
        texts = [l["text"] for l in job["lines"] if l["text"].strip()]
        job["result"] = texts[-1].strip() if texts else ("exited with %s" % code)
        if code is not None and code < 0 and job["result"] and "did not start" not in job["result"]:
            job["result"] = "stopped after %d s: %s" % (ACTION_TIMEOUT, job["result"])
        job["code"], job["ended"] = code, time.strftime("%Y-%m-%dT%H:%M:%S")
        self.log_action({"key": job["key"], "label": job["label"], "action": job["action"]}, job["who"], job["result"], code, job["inputs"])
        self.panels_cache = (0.0, None)
        job["running"] = False

    def job_view(self, since=0):
        j = self.job
        if not j:
            return None
        out = {k: j[k] for k in ("id", "key", "label", "action", "started", "ended", "running", "code", "result")}
        out["lines"] = j["lines"][max(0, int(since or 0)):]
        out["total"] = len(j["lines"])
        return out

    def log_action(self, entry, who, result, code, inputs):
        """One JSON line: time, who, the button, its console action, its inputs (already checked), the result line, the
        exit code. Never a header, the typed word, the proof or a token: only these fields, each redacted."""
        rec = {"time": time.strftime("%Y-%m-%dT%H:%M:%S"), "who": who, "key": entry.get("key"), "label": entry.get("label"),
               "action": entry.get("action"), "inputs": inputs or {}, "result": (result or "")[:300], "code": code}
        try:
            os.makedirs(self.data, exist_ok=True)
            if os.path.exists(self.action_log) and os.path.getsize(self.action_log) > 2_000_000:
                os.replace(self.action_log, self.action_log + ".1")
            with open(self.action_log, "a", encoding="utf-8") as f:
                f.write(json.dumps(panels.redact_tree(rec), ensure_ascii=True) + "\n")
        except OSError:
            pass

    def web_actions(self, n=40):
        out = []
        for line in self._tail_file(self.action_log, 200_000)[-n:]:
            try:
                out.append(json.loads(line))
            except ValueError:
                pass
        return out

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
                                        self._dirs(os.path.join(self.notes, "testing", "bot-runs")), self.web_actions()),
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
            "web": ("The web console's action log", ("file", os.path.join(self.data, "actions.log"))),
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

    def art_frames(self):
        """art.json's animated strips (frames, fps) by slot, for the page to step through (cc-P132's format)."""
        s = (self._load_json(os.path.join(self.repo, "scripts", "console-art", "art.json"), {}) or {}).get("slots") or {}
        return {k: v for k, v in s.items() if isinstance(v, dict) and isinstance(v.get("frames"), int) and v["frames"] > 1}

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
            ok, why = panels.decide(listener, self.headers, console.allowed_logins(), console.proof())
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
                job = console.job_view(10**9)
                return self._json(200, {"status": doc, "ageSeconds": age, "refreshAsked": asked,
                                        "panels": console.panels(doc), "logSources": {k: v[0] for k, v in console.log_sources().items()},
                                        "art": console.art(), "artFrames": console.art_frames(),
                                        "actions": {"buttons": console.public_buttons(), "canAct": listener == panels.TAILNET,
                                                    "why": "" if listener == panels.TAILNET else LOCAL_READ_ONLY, "job": job},
                                        "server": {"now": time.strftime("%Y-%m-%dT%H:%M:%S"), "listener": listener}})
            if p == "/api/action":
                j = console.job_view((q.get("since") or ["0"])[0] if (q.get("since") or ["0"])[0].isdigit() else 0)
                return self._json(200, j or {"running": False, "lines": [], "total": 0})
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
            self._send(405, "not allowed: only POST /api/action changes anything\n", "text/plain; charset=utf-8")

        do_PUT = do_DELETE = do_PATCH = do_OPTIONS = _refuse

        def do_POST(self):
            # cc-P182: the Golem's buttons, and nothing else.
            if urlparse(self.path).path != "/api/action":
                return self._refuse()
            if not self._allowed():
                return
            if listener != panels.TAILNET:
                self._note(403, "action on the local listener")
                return self._json(403, {"error": LOCAL_READ_ONLY})
            # Only this page: a header no form or plain link can send (a cross-site script needs a CORS preflight,
            # which is never answered), the same origin when the browser says, and JSON.
            why = ""
            if self.headers.get("X-UOConsole-Action") != "1":
                why = "no X-UOConsole-Action header"
            origin = self.headers.get("Origin")
            if not why and origin and urlparse(origin).netloc.lower() != (self.headers.get("Host") or "").lower():
                why = "another origin"
            if not why and (self.headers.get("Sec-Fetch-Site") or "same-origin") not in ("same-origin", "none"):
                why = "a cross-site fetch"
            if why:
                self._note(403, why)
                return self._json(403, {"error": "not allowed: " + why})
            if not (self.headers.get("Content-Type") or "").lower().startswith("application/json"):
                self._note(415, "not JSON")
                return self._json(415, {"error": "JSON only"})
            try:
                n = int(self.headers.get("Content-Length") or 0)
            except ValueError:
                n = -1
            if n < 0 or n > 4096:
                return self._json(413, {"error": "too large"})
            try:
                body = json.loads(self.rfile.read(n).decode("utf-8") or "{}")
                if not isinstance(body, dict):
                    raise ValueError("not an object")
            except ValueError:
                return self._json(400, {"error": "not JSON"})
            entry = next((b for b in console.buttons() if b.get("key") == body.get("key")), None)
            if not entry:
                self._note(404, "no such button")
                return self._json(404, {"error": "no such button"})
            who = (self.headers.get(panels.IDENTITY_HEADER) or "").strip()
            code, out = console.start_action(entry, body.get("typed"), body.get("inputs"), who)
            self._note(code, "action " + entry["key"])
            return self._json(code, out)

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
