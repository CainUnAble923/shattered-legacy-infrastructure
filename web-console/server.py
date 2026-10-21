"""The web console (cc-P155, phase 1; cc-P182, phase 2): the Shard Console's status and the Golem's buttons, for the phone.

Two listeners, both on 127.0.0.1 only (nothing on the LAN, nothing public):
  127.0.0.1:8095  local: this PC's browser. Allowed as the PowerShell console is: anything on this PC is Chase.
  127.0.0.1:8096  tailnet: reached only through the uoconsole sidecar (web-console/tailscale), whose Tailscale Serve
                  gives https://uoconsole.tail81098d.ts.net/ Tailscale's certificate and sets Tailscale-User-Login, and
                  whose proof forwarder (tailscale/proof_proxy.py) sets X-UOConsole-Proof to the secret in
                  D:/UO/web-console/proof.key. A request is answered only with that proof and an allowed login
                  (panels.decide); anything else gets "not allowed". Only this listener may act (cc-P182).
                  cc-P186: the same listener answers the remote Golems' agents (/api/agent/...), each on another PC on
                  the tailnet (tag:golem), which opens no port of its own: it calls out to
                  https://uoconsole.tail81098d.ts.net/api/agent/... (Tailscale Serve, tailnet only, never Funnel), the
                  sidecar adds the proof as for every request, and the agent names its Golem (X-Golem-Name) and sends
                  that Golem's secret (X-Golem-Key). A tagged PC has no Tailscale login, so an agent is let in by the
                  proof and its secret instead (panels.decide_agent, Console.agent_auth), and only to its own endpoints.

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
                         cc-P186: {key, typed, inputs, golem} acts on that Golem: the integrator as before; another Golem
                         on this PC through the same runner with -Golem <name>; a Golem on another PC is queued for its
                         agent (D:/UO/golem/remote/<name>/commands/<id>.json) until it pulls it, or until it expires
                         (settings.json agentCommandMinutes, default 30). Only the buttons marked anyGolem.
  GET /portrait/<name>   cc-P186: a Golem's portrait (golems.json portrait), only from scripts/console-art
  cc-P187, the web console's pages (web-console/console-actions.json: every other Shard Console tab):
  GET /api/page?id=<page>    that page's lists (scripts/Get-WebConsoleList.ps1: snapshots, zips, images, commits, the
                             shard question, world commands, sites, art requests), at most every 20 s per page
  POST /api/preview          {key, inputs}: the console's plan for a change or dangerous page button, a dry run of the
                             same runner (-DryRun), and a token for the run: the phrase the plan asks, its confirm text,
                             and for a dangerous one the second confirm's plain words. A token is good once, for 10
                             minutes, for that button with those inputs
  POST /api/action           a page button too: {key, typed, inputs, token, second}. read: runs at once. change: the
                             token and the plan's phrase. dangerous (and anything on the LIVE shard or with a yes/no
                             question): also second (the second confirm screen); its script's own 'Type yes' or word is
                             written to its stdin only then. Refused while another action runs.
  GET /api/prompt?name=<file>   a queued or running prompt's text (only one the status names); ?notes=<n> its notes
  GET /art-thumb/<id>        an art request's newest picture (only under D:/UO/art)
  cc-P367, the Music Room (music.py): every candidate music track on disk, played in the browser, and Chase's notes.
  GET /music                 the page (static/music.html; music.js and music.css beside it)
  GET /api/music             the packs, every track (its stock original or new, length, ID3 origin), the notes, the
                             tags and the target suggestions (the shard's regions and ART-LIBRARY.md's boss shortlist)
  GET /api/music/summary     the tracks marked Use, grouped by tag and then by target: what P368 reads
  GET /music-file/<pack>/<file>  one listed track, read only, streamed with Range requests (206) so seeking works
  POST /api/music/note       {key, rating, tags, note, target}: one track's record in <notes>/docs/music/music-notes.json
  POST /api/music/commit     commit music-notes.json alone in the docs repo (never pushed); also hourly if it changed
                             Both POSTs: either listener (each is Chase: this PC, or his tailnet login), only from the
                             page (the same header, origin and JSON checks as POST /api/action). They write only that file.
  GET /api/agent/ping        cc-P186, a remote agent: {golem, ack}: the game PC knows this Golem and its secret
  GET /api/agent/commands    cc-P186: this Golem's queued commands, each handed out once (the agent's poll)
  POST /api/agent/result     cc-P186: {id, code, result, lines}: a command's result, onto the Timeline with the name
  POST /api/agent/status     cc-P186: {status}: the Golem's status-<name>.json as its PC wrote it
  POST /api/agent/ready      cc-P186: the readiness file Test-GolemReady.ps1 wrote on that PC
  GET /api/agent/kit         cc-P222: the Golem kit's manifest (kit/current/kit-manifest.json under the Golem root):
                             each PC's agent compares it and fetches what differs
  POST /api/agent/questions  cc-P216: {golem, questions: {open, history}}: that remote Golem's questions for Chase
  POST /api/answer           cc-P216: {id, choice, text, confirm}: Chase's answer to a Golem's question, from the page
                             (the same sign-in, proof, header and origin checks as /api/action). A Golem on this PC:
                             the Golem folder's Answer-Question.ps1 (argv only; the words as base64), which writes the answer
                             file through Submit-GolemAnswer and resumes the Golem. A remote one: queued for its agent.
  GET /api/agent/kit/file    cc-P222: ?path=<a manifest path>: that file from the snapshot, only a path the manifest lists
  POST /api/golem-password   cc-P215: {golem, role}: one Golem test account's password (Get-GolemTestAccount.ps1, this
                             service's DPAPI), as text/plain for the card's copy button. The tailnet listener, an allowed
                             login and the page's own header and origin only, as POST /api/action; logged without it
Every other method is refused (and every other POST: only the ones above change anything). Every string sent is passed through panels.redact_tree, except that one password.

The collector runs as a child: Write-ConsoleStatus.ps1 -Loop -ParentPid <this>, restarted if it ends.
Standard library only (like docker/test-queue/server.py): nothing to install on the game PC. ASCII only.
"""
import argparse
import base64
import hashlib
import hmac
import json
import mimetypes
import os
import re
import secrets
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, unquote, urlparse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import music  # noqa: E402
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
KIT_PATH = re.compile(r"^(golem/[A-Za-z0-9][A-Za-z0-9._-]*|unattended-permission\.ps1)$")   # cc-P222, as Kit.ps1
PANE_LINE = re.compile(r"^\[\[shard-console:([a-z]+)\]\](.*)$")
# cc-P182: what a page may pass for each input, checked before the runner is started (argv, never a shell).
INPUT_RULES = {
    "model": (re.compile(r"^(?i:auto|fable|opus|sonnet)$"), "-Model", lambda v: v[:1].upper() + v[1:].lower()),
    "cap": (re.compile(r"^(?i:all|\d{1,3})$"), "-Cap", lambda v: v.lower()),
    "until": (re.compile(r"^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}$"), "-Until", lambda v: v.replace("T", " ")),
    "start": (re.compile(r"^([01]?\d|2[0-3]):[0-5]\d$"), "-NightStart", lambda v: v),
    "end": (re.compile(r"^([01]?\d|2[0-3]):[0-5]\d$"), "-NightEnd", lambda v: v),
    "prompt": (re.compile(r"^[Pp]?\d{1,4}$"), "-Prompt", lambda v: "P" + v.lstrip("Pp")),
}
INPUT_RULES["assignTo"] = (re.compile(r"^[A-Za-z][A-Za-z0-9]{1,23}$"), "-AssignTo", lambda v: v)   # cc-P187
INPUT_RULES["next"] = (re.compile(r"^(yes|no)$"), "-Next", lambda v: v)
# cc-P187: the page buttons' inputs (console-actions.json fields). Argv only, never a shell; free text never starts with
# '-' (PowerShell would read it as a parameter name) and never holds a double quote.
TEXT = r"^(?!-)[\x20\x21\x23-\x7e]{1,%d}$"
PAGE_INPUT_RULES = {
    "shard": (re.compile(r"^(test|live)$"), "-Shard"),
    "snapshotName": (re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$"), "-SnapshotName"),
    "snapshot": (re.compile(r"^\d{8}-\d{6}_[A-Za-z0-9._-]{1,80}$"), "-Snapshot"),
    "commitMessage": (re.compile(TEXT % 200), "-CommitMessage"),
    "force": (re.compile(r"^(yes|no)$"), "-CommitForce"),
    "packageNotes": (re.compile(TEXT % 200), "-PackageNotes"),
    "packageZip": (re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,79}\.zip$"), "-PackageZip"),
    "imageTag": (re.compile(r"^sl-modernuo:[A-Za-z0-9][A-Za-z0-9._-]{0,59}$"), "-ImageTag"),
    "commit": (re.compile(r"^([0-9a-f]{7,40}|HEAD)$"), "-Commit"),
    "site": (re.compile(r"^[a-z0-9][a-z0-9-]{0,39}$"), "-Site"),
    "vdFile": (re.compile(r"^[A-Za-z]:\\[^\"<>|?*\x00-\x1f]{1,240}\.vd$", re.I), "-VdFile"),
    "body": (re.compile(r"^\d{1,5}$"), "-Body"),
    "huesFile": (re.compile(r"^[A-Za-z]:\\[^\"<>|?*\x00-\x1f]{1,240}\.mul$", re.I), "-HuesFile"),
    "artId": (re.compile(r"^[a-z0-9][a-z0-9-]{0,63}$"), "-ArtId"),
    "note": (re.compile(TEXT % 300), "-Note"),
}
PAGES_FILE = os.path.join(HERE, "console-actions.json")
PAGE_IDS = ("test", "fix", "question", "snapshots", "world", "package", "commit", "websites", "art", "live")
LIST_SECONDS = 20       # a page's lists are read again at most this often
PREVIEW_TIMEOUT = 120   # seconds for the look first (a dry run of the plan)
TOKEN_SECONDS = 600     # a preview's token is good this long, once
ART_STORE = r"D:\UO\art"
PROMPTS_DIR = r"D:\UO\cc-prompts"
ONLINE_UNKNOWN = "Who is online: the console cannot tell (it reads no player list), so look in game first."
TYPED = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 ]{0,39}$")
ACTION_TIMEOUT = 600   # seconds; the Golem's scripts take a few
# cc-P186: every Golem in D:/UO/golem/golems.json, and the remote ones' agents.
DEFAULT_GOLEM_ROOT = r"D:\UO\golem"
GOLEM_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9]{0,23}$")     # golems.json: letters and digits
COMMAND_ID = re.compile(r"^\d{13}-[0-9a-f]{6}$")
# cc-P216: a question file's name (Questions.ps1): <Golem>-P<nn>-<yyyyMMdd-HHmmss>-<4 hex>; 'Golem' when one works alone.
QUESTION_ID = re.compile(r"^([A-Za-z][A-Za-z0-9]{0,23})-P\d{1,4}-\d{8}-\d{6}-[0-9a-f]{4}$")
ANSWER_TEXT_MAX = 2000   # as Questions.ps1's QTextMax
ANSWER_TIMEOUT = 120     # seconds for Answer-Question.ps1 (the answer, the Golem's state, a wake)
WHO_UNSAFE = re.compile(r"[^A-Za-z0-9@._+ -]")
COMMAND_MINUTES = 30   # how long a command waits for its Golem's agent (settings.json agentCommandMinutes)
TAKEN_MINUTES = 20     # a command handed out with no result by then is closed as unanswered
AGENT_ONLINE = 180     # seconds: the agent polls every POLL_AWAKE s while its Golem is awake, POLL_ASLEEP s asleep
POLL_AWAKE, POLL_ASLEEP = 5, 60
AGENT_BODY = 65536     # a readiness file with its checks is bigger than a button press
PORTRAIT_NAME = re.compile(r"(?i)\.(png|gif|jpg|jpeg|webp)$")
MUSIC_COMMIT_SECONDS = 3600   # cc-P367: music-notes.json is committed this often when it changed
MUSIC_BODY = 16384            # a note is 4000 characters at most
MUSIC_FILE = re.compile(r"^/music-file/([a-z0-9-]{1,24})/([^/]{1,124})$")
AGENTS_COMMENT = ("cc-P186: one salted SHA-256 per remote Golem's agent secret (server.py --mint-agent <name>). Outside git. "
                  "The secret itself is shown once when minted and kept only on that Golem's PC.")


class Console:
    """What the handlers read, refreshed in the background so a request never waits on git, docker or the network."""

    def __init__(self, repo, notes, data, status_file=None, collector=True, runner=None, golem_root=None, machine=None, answer_runner=None):
        self.repo, self.notes, self.data = repo, notes, data
        # cc-P186: the Golem registry and the remote Golems' folders (D:/UO/golem), this PC's name, the agents' hashes.
        self.golem_root = golem_root or DEFAULT_GOLEM_ROOT
        self.password_reader = None   # cc-P215: tests replace Get-GolemTestAccount.ps1 with a stand-in
        self.machine = (machine or os.environ.get("COMPUTERNAME") or "").upper()
        self.agents_file = os.path.join(data, "golem-agents.json")
        self._registry = (None, [])
        self.queue_lock = threading.Lock()
        self.clock = time.time   # tests move it to see a command expire
        # cc-P182: the Golem buttons. The runner is the Shard Console's (tests pass a fake that records each call).
        self.runner = runner or ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                                 os.path.join(repo, "scripts", "Invoke-WebConsoleAction.ps1")]
        # cc-P216: Chase's answers to the Golems' questions (tests pass a fake that records each call).
        self.answer_runner = answer_runner or ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                                               os.path.join(self.golem_root, "Answer-Question.ps1")]
        self.answer_lock = threading.Lock()
        self.proof_file = os.path.join(data, "proof.key")
        self.action_log = os.path.join(data, "actions.log")
        self._proof = (None, "")
        self._buttons = (None, [])
        self._pages = (None, {})        # cc-P187: console-actions.json
        self.tokens = {}                # cc-P187: preview token -> {key, inputs, expect, danger, at}
        self.preview_lock = threading.Lock()
        self.lists = {}                 # cc-P187: page id -> (time, data)
        self.list_lock = threading.Lock()
        self.list_runner = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", os.path.join(repo, "scripts", "Get-WebConsoleList.ps1")]
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
        # cc-P367: the Music Room. The notes live in the docs repo (notes), the scan's length cache beside settings.json.
        self.music = music.MusicRoom(notes, code_repo=repo, lock_dir=os.path.join(self.golem_root, "locks"),
                                     cache_file=os.path.join(data, "music-index.json"))
        if collector:
            threading.Thread(target=self._music_loop, daemon=True).start()

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
        keep = ("key", "label", "word", "inputs", "when", "disabled", "help", "art", "anyGolem", "golemWord", "card", "engine")   # cc-P208 card, cc-P159 engine
        return [{k: b[k] for k in keep if k in b} for b in self.buttons()]

    def check_request(self, entry, typed, inputs, golem=None):
        """(None, args) when the runner may start, else (http code, why). The word is checked again by the runner
        against the console's own plan; here it is the button's word, the picked prompt, or the shape of a prompt.
        cc-P187: golem, another Golem than the integrator: a button with a golemWord wants that word and the Golem's
        name ('wake bronze'; the prompt and the name for Run next and Retry). Assign wants 'P<nn> on <golem>'."""
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
        gw = entry.get("golemWord") if golem else None
        if word == "#prompt":
            expected = clean.get("prompt")
            if not expected:
                return (400, "pick a prompt first"), None
        elif word == "#assign":
            if not clean.get("prompt") or not clean.get("assignTo"):
                return (400, "pick a prompt and the Golem to give it to first"), None
            expected = "%s on %s" % (clean["prompt"], clean["assignTo"].lower())
        elif word == "#stopped":
            expected = None
            if not re.match(r"^[Pp]\d{1,4}" + (r" " + re.escape(golem.lower()) if gw else "") + r"$", typed, re.I):
                return (400, "type the stopped prompt's number (P<nn>)%s to confirm" % (" and " + golem.lower() if gw else "")), None
        else:
            expected = word
        if gw and expected is not None:
            expected = (expected if gw.startswith("#") else gw) + " " + golem.lower()
        if expected is not None and typed.lower() != expected.lower():
            return (400, "type %s to confirm" % expected), None
        if not TYPED.match(typed):
            return (400, "the typed word may hold letters, digits and spaces only"), None
        return None, (["-Key", entry["key"], "-Typed", typed] + args, clean)

    def start_action(self, entry, typed, inputs, who, golem=None):
        """(http code, body). One at a time: a second request while one runs is refused with what is running.
        cc-P186: golem names another Golem on this PC; the runner gets -Golem <name> (the console's plan for that Golem)."""
        err, ok = self.check_request(entry, typed, inputs, golem)
        if err:
            self.log_action(entry, who, "refused: " + err[1], None, {}, golem=golem)
            return err[0], {"error": err[1]}
        args, clean = ok
        if golem:
            args = args + ["-Golem", golem]
        return self._start_job(entry, args, clean, who, golem)

    def _start_job(self, entry, args, clean, who, golem=None, answers=None, timeout=ACTION_TIMEOUT, kind=""):
        """cc-P187: the one-at-a-time job, for a Golem button or a page button. answers: lines for the script's stdin."""
        with self.job_lock:
            if self.job and self.job["running"]:
                j = self.job
                return 409, {"error": "busy: %s is running (since %s)" % (j["label"], j["started"]),
                             "running": {"key": j["key"], "label": j["label"], "started": j["started"]}}
            job = {"id": int(time.time() * 1000), "key": entry["key"], "label": (entry.get("label") or entry["key"]) + (" (%s)" % golem if golem else ""),
                   "action": entry.get("action") or entry.get("art") or "", "who": who, "started": time.strftime("%Y-%m-%dT%H:%M:%S"), "ended": None,
                   "running": True, "code": None, "result": "", "lines": [], "inputs": clean, "golem": golem, "kind": kind, "timeout": timeout}
            self.job = job
        threading.Thread(target=self._run_job, args=(job, args, answers), daemon=True).start()
        return 202, {"id": job["id"], "label": job["label"]}

    def _run_job(self, job, args, answers=None):
        code = None
        limit = job.get("timeout") or ACTION_TIMEOUT
        try:
            proc = subprocess.Popen(self.runner + args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                    stdin=subprocess.PIPE if answers else subprocess.DEVNULL, creationflags=NO_WINDOW, cwd=self.repo)
            if answers:
                # cc-P187: the script's own 'Type yes' (or word), answered only after the page's second confirm.
                try:
                    proc.stdin.write(("\r\n".join(answers) + "\r\n").encode("ascii"))
                    proc.stdin.close()
                except OSError:
                    pass
            killer = threading.Timer(limit, proc.kill)
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
            job["result"] = "stopped after %d s: %s" % (limit, job["result"])
        job["code"], job["ended"] = code, time.strftime("%Y-%m-%dT%H:%M:%S")
        self.log_action({"key": job["key"], "label": job["label"], "action": job["action"]}, job["who"], job["result"], code, job["inputs"],
                        golem=job.get("golem"))
        self.panels_cache = (0.0, None)
        job["running"] = False

    def job_view(self, since=0):
        j = self.job
        if not j:
            return None
        out = {k: j.get(k) for k in ("id", "key", "label", "action", "started", "ended", "running", "code", "result", "kind")}
        out["lines"] = j["lines"][max(0, int(since or 0)):]
        out["total"] = len(j["lines"])
        return out

    def log_action(self, entry, who, result, code, inputs, golem=None):
        """One JSON line: time, who, the button, its console action, its inputs (already checked), the result line, the
        exit code, and (cc-P186) the Golem it was for when not the integrator. Never a header, the typed word, the
        proof, a token or an agent's secret: only these fields, each redacted."""
        rec = {"time": time.strftime("%Y-%m-%dT%H:%M:%S"), "who": who, "key": entry.get("key"), "label": entry.get("label"),
               "action": entry.get("action"), "inputs": inputs or {}, "result": (result or "")[:300], "code": code}
        if golem:
            rec["golem"] = golem
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

    # --- the pages (cc-P187) ---------------------------------------------------------------------------------------
    def pages_doc(self):
        try:
            stamp = os.path.getmtime(PAGES_FILE)
        except OSError:
            return {}
        if self._pages[0] != stamp:
            self._pages = (stamp, self._load_json(PAGES_FILE, {}) or {})
        return self._pages[1]

    def page_button(self, key):
        """(entry, page) for a page button's key, else (None, None)."""
        for p in self.pages_doc().get("pages") or []:
            for b in p.get("buttons") or []:
                if b.get("key") == key:
                    return b, p
        return None, None

    def public_pages(self):
        keep = ("key", "label", "kind", "inputs", "pcOnly", "disabled", "help", "minutes", "danger")
        d = self.pages_doc()
        return {"fields": d.get("fields") or {},
                "pages": [{"id": p.get("id"), "tab": p.get("tab"), "title": p.get("title"), "caption": p.get("caption"), "pcNotes": p.get("pcNotes") or [],
                           "buttons": [{k: b[k] for k in keep if k in b} for b in p.get("buttons") or []]} for p in d.get("pages") or []]}

    @staticmethod
    def needs_second(entry, clean):
        return entry.get("kind") == "dangerous" or clean.get("shard") == "live" or str(entry.get("action") or "").startswith("live.")

    def page_args(self, entry, inputs):
        """(None, (args, clean)) or ((http code, why), None): the button's inputs, each checked."""
        if entry.get("pcOnly"):
            return (409, "PC only: " + entry["pcOnly"]), None
        if entry.get("disabled"):
            return (409, entry["disabled"]), None
        inputs = inputs if isinstance(inputs, dict) else {}
        args, clean = [], {}
        for name in entry.get("inputs") or []:
            v = inputs.get(name)
            if v is None or str(v).strip() == "":
                continue
            v = str(v).strip()
            rule = PAGE_INPUT_RULES.get(name)
            if not rule or not rule[0].match(v):
                return (400, "%s is not allowed: %r" % (name, v[:40])), None
            clean[name] = v
            args += [rule[1], v]
        return None, (args, clean)

    def danger_text(self, entry, clean):
        t = entry.get("danger") or ""
        return re.sub(r"\{([A-Za-z]+)\}", lambda m: clean.get(m.group(1)) or ("the " + m.group(1)), t)

    def preview(self, entry, inputs):
        """(http code, body): the console's plan for this page button with these inputs (the runner's -DryRun), and a
        token for running it once within TOKEN_SECONDS."""
        if entry.get("kind") == "read":
            return 400, {"error": "a read button runs at once; there is nothing to look at first"}
        err, ok = self.page_args(entry, inputs)
        if err:
            return err[0], {"error": err[1]}
        args, clean = ok
        if not self.preview_lock.acquire(blocking=False):
            return 409, {"error": "busy: another look first is being read"}
        try:
            try:
                r = subprocess.run(self.runner + ["-Key", entry["key"], "-DryRun"] + args, capture_output=True, timeout=PREVIEW_TIMEOUT,
                                   stdin=subprocess.DEVNULL, creationflags=NO_WINDOW, cwd=self.repo)
                text = r.stdout.decode("utf-8", "replace")
            except Exception as e:
                return 500, {"error": panels.redact("the look first did not run: %s" % e)}
        finally:
            self.preview_lock.release()
        lines, meta = [], {}
        for line in text.splitlines():
            if line.startswith("[[web-preview]]"):
                try:
                    meta = json.loads(line[len("[[web-preview]]"):])
                except ValueError:
                    meta = {"refused": "the look first could not be read"}
                continue
            m = PANE_LINE.match(line)
            color, t = (m.group(1), m.group(2)) if m else ("normal", line)
            lines.append({"color": color, "text": panels.redact(t)})
        out = {"lines": lines[-400:], "expect": str(meta.get("expect") or ""), "confirmText": panels.redact(str(meta.get("confirmText") or "")),
               "preface": panels.redact(str(meta.get("preface") or "")), "asks": panels.redact(str(meta.get("asks") or ""))}
        if meta.get("refused") or not meta:
            out["refused"] = panels.redact(str(meta.get("refused") or "the plan was not made (see its lines)"))
            return 200, out
        danger = bool(meta.get("danger")) or self.needs_second(entry, clean)
        out["danger"] = danger
        if danger:
            out["dangerText"] = self.danger_text(entry, clean) or out["confirmText"] or "This changes something that cannot simply be undone."
            if clean.get("shard") == "live" or str(entry.get("action") or "").startswith("live."):
                out["online"] = ONLINE_UNKNOWN
        tok = secrets.token_urlsafe(18)
        now = time.time()
        with self.job_lock:
            for k in [k for k, v in self.tokens.items() if now - v["at"] > TOKEN_SECONDS]:
                self.tokens.pop(k, None)
            self.tokens[tok] = {"key": entry["key"], "inputs": clean, "expect": out["expect"], "danger": danger, "at": now}
        out["token"] = tok
        return 200, out

    def start_page_action(self, entry, page, body, who):
        """(http code, body): a page button. read: at once. change and dangerous: the preview's token (this button,
        these inputs, unexpired, once) and the plan's phrase; dangerous also the second confirm."""
        err, ok = self.page_args(entry, body.get("inputs"))
        if err:
            self.log_action(entry, who, "refused: " + err[1], None, {})
            return err[0], {"error": err[1]}
        args, clean = ok
        typed = (body.get("typed") if isinstance(body.get("typed"), str) else "").strip()
        second = body.get("second") is True
        if entry.get("kind") != "read":
            tok = body.get("token") if isinstance(body.get("token"), str) else ""
            with self.job_lock:
                t = self.tokens.get(tok)
            why = ""
            if not t:
                why = "look first: the plan was not seen (or that look is used or older than 10 minutes)"
            elif t["key"] != entry["key"] or t["inputs"] != clean:
                why = "the inputs changed since the plan was shown: look again"
            elif time.time() - t["at"] > TOKEN_SECONDS:
                why = "the plan is older than 10 minutes: look again"
            elif t["expect"] and typed.lower() != t["expect"].lower():
                why = "type %s to confirm" % t["expect"]
            elif t["expect"] and not TYPED.match(typed):
                why = "the typed word may hold letters, digits and spaces only"
            elif (t["danger"] or self.needs_second(entry, clean)) and not second:
                why = "this needs the second confirm (what it does and to what, then Yes)"
            if why:
                self.log_action(entry, who, "refused: " + why, None, clean)
                return 400, {"error": why}
            args = ["-Typed", typed] + args
            if t["danger"] or self.needs_second(entry, clean):
                args.append("-Second")
            with self.job_lock:
                busy = self.job and self.job["running"]
                if not busy:
                    self.tokens.pop(tok, None)
            if busy:
                j = self.job
                return 409, {"error": "busy: %s is running (since %s); a dangerous button waits for it" % (j["label"], j["started"])}
        answers = None
        if second:
            answers = entry.get("answers") or (entry.get("answersBySite") or {}).get(clean.get("site") or "")
        minutes = entry.get("minutes") if isinstance(entry.get("minutes"), int) else 10
        code, out = self._start_job(dict(entry, label="%s > %s" % (page.get("title"), entry.get("label"))), ["-Key", entry["key"]] + args, clean, who,
                                    answers=answers, timeout=max(60, minutes * 60), kind=entry.get("kind") or "")
        if code != 202:
            self.log_action(entry, who, "refused: " + out.get("error", ""), None, clean)
        return code, out

    def page_data(self, pid, fresh=False):
        """A page's lists from Get-WebConsoleList.ps1, read again after LIST_SECONDS (fresh: after 5 s)."""
        if pid not in PAGE_IDS:
            return None
        now = time.time()
        at, data = self.lists.get(pid, (0.0, None))
        if data is not None and now - at < (5 if fresh else LIST_SECONDS):
            return data
        with self.list_lock:
            at, data = self.lists.get(pid, (0.0, None))
            if data is not None and time.time() - at < (5 if fresh else LIST_SECONDS):
                return data
            try:
                r = subprocess.run(self.list_runner + ["-Page", pid], capture_output=True, timeout=90, stdin=subprocess.DEVNULL,
                                   creationflags=NO_WINDOW, cwd=self.repo)
                text = r.stdout.decode("utf-8", "replace").strip()
                data = json.loads(text[text.find("{"):]) if "{" in text else {"error": "no answer"}
            except Exception as e:
                data = {"error": "the lists could not be read: %s" % e}
            if pid == "art" and isinstance(data, dict):
                # A picture's path stays here: the page gets /art-thumb/<id>.
                thumbs = {}
                for r0 in data.get("requests") or []:
                    path = self.art_picture(r0.pop("thumb", ""))
                    if path:
                        thumbs[str(r0.get("value"))] = path
                    r0["picture"] = ("/art-thumb/" + str(r0.get("value"))) if path else ""
                self._thumbs = thumbs
            data = panels.redact_tree(data)
            self.lists[pid] = (time.time(), data)
            return data

    def art_picture(self, path):
        """A request's picture path when it is an image under the art store, else ''."""
        if not isinstance(path, str) or not PORTRAIT_NAME.search(path):
            return ""
        base = os.path.realpath(ART_STORE)
        full = os.path.realpath(path)
        return full if full.startswith(base + os.sep) and os.path.isfile(full) else ""

    def art_thumb_path(self, rid):
        if not re.match(r"^[a-z0-9][a-z0-9-]{0,63}$", str(rid or "")):
            return ""
        return self.art_picture(getattr(self, "_thumbs", {}).get(str(rid), ""))

    def prompt_text(self, name=None, notes=None):
        """(text, what) for a prompt the status names (queued, running or finished), or its notes; (None, why)."""
        doc, _ = self.status()
        g = (doc or {}).get("golem") or {}
        if name:
            names = {str(q.get("name")) for q in (g.get("queue") or []) if isinstance(q, dict)}
            cur = g.get("current") if isinstance(g.get("current"), dict) else {}
            if cur.get("name"):
                names.add(str(cur["name"]))
            if name not in names or not re.match(r"^cc-P\d+-[A-Za-z0-9._-]+\.md$", name):
                return None, "not a prompt the status names"
            for d in [os.path.join(PROMPTS_DIR, "queue"), PROMPTS_DIR] + [os.path.join(PROMPTS_DIR, "claimed", x) for x in self._dirs(os.path.join(PROMPTS_DIR, "claimed"))]:
                p = os.path.join(d, name)
                if os.path.isfile(p):
                    return self._read_text(p)[:200_000], name
            return None, "the file is gone (handed on?)"
        if notes:
            nums = {str(f.get("number")) for f in (g.get("finished") or []) if isinstance(f, dict)}
            if str(notes) not in nums:
                return None, "not a finished prompt the status names"
            d = os.path.join(self.notes, "notes")
            try:
                hit = sorted(n for n in os.listdir(d) if re.match(r"^cc-P%s-.*\.md$" % re.escape(str(notes)), n))
            except OSError:
                hit = []
            if not hit:
                return None, "no notes file for P%s" % notes
            return self._read_text(os.path.join(d, hit[0]))[:200_000], hit[0]
        return None, "name or notes"

    def golem_picker(self, doc):
        """cc-P187: every Golem in golems.json for the Golem page's picker: name, lane, machine, its state, and whether
        a button can act for it now and why not (disabled; multiGolem off; a remote Golem's agent offline is shown
        greyed but may be picked: its commands wait for it, cc-P186)."""
        reg = self.registry()
        if not reg:
            return []
        g = ((doc or {}).get("golem") or {}) if isinstance(doc, dict) else {}
        cards = {str(c.get("name")).lower(): c for c in (g.get("golems") or []) if isinstance(c, dict)}
        s = self._load_json(os.path.join(self.golem_root, "settings.json"), {}) or {}
        multi = bool(s.get("multiGolem")) if isinstance(s, dict) else False
        out = []
        own = self.home_name()
        for e in reg:
            if str(e.get("engine") or "") == "script":
                continue   # cc-P208: the merge Golem (Shadow Iron) has its own card and buttons, not the picker
            c = cards.get(str(e["name"]).lower(), {})
            # cc-P208: "integrator" here is the console's own Golem, the home one (the integrator, or Iron while Shadow Iron merges).
            integ = str(e["name"]).lower() == own.lower()
            remote = self.is_remote(e)
            why, grey = "", ""
            if not integ and not e.get("enabled"):
                why = "disabled in golems.json"
            elif not integ and not multi:
                why = "multiGolem is off (settings.json): only the integrator works the queue"
            elif remote and not self.agent_online(e):
                grey = "offline (its agent has not called in lately): a button waits for it up to %d minutes" % self.command_minutes()
            out.append({"name": e["name"], "lane": e.get("lane") or "", "machine": e.get("machine") or "", "engine": e.get("engine") or "",
                        "integrator": integ, "remote": remote, "usable": not why, "why": why, "grey": grey,
                        "state": (g.get("state") if integ else c.get("state")) or ("OFF" if not e.get("enabled") else "UNKNOWN"),
                        "current": (((g.get("current") or {}).get("number") and "P%s" % g["current"]["number"]) if integ else c.get("current")) or "",
                        "portraitUrl": ("/portrait/" + e["name"]) if self.portrait_path(e) else ""})
        out.sort(key=lambda x: (not x["integrator"], x["name"].lower()))
        return out

    # --- every Golem, and the remote Golems' agents (cc-P186) -----------------------------------------------------
    def registry(self):
        """golems.json's entries (re-read when the file changes); [] when there is none (one Golem)."""
        path = os.path.join(self.golem_root, "golems.json")
        try:
            stamp = os.path.getmtime(path)
        except OSError:
            return []
        if self._registry[0] != stamp:
            doc = self._load_json(path, {})
            gs = doc.get("golems") if isinstance(doc, dict) else None
            self._registry = (stamp, [g for g in (gs if isinstance(gs, list) else []) if isinstance(g, dict) and GOLEM_NAME.match(str(g.get("name") or ""))])
        return self._registry[1]

    def home_name(self):
        """cc-P208: the console's own Golem (golem.ps1's home Golem): the integrator, or with a script integrator (Shadow
        Iron, the merge Golem) the first Golem that claims on its PC. '' with no registry."""
        reg = self.registry()
        integ = next((g for g in reg if g.get("integrator")), None)
        if not integ:
            return ""
        if str(integ.get("engine") or "") != "script":
            return str(integ["name"])
        mach = str(integ.get("machine") or "").upper()
        h = next((g for g in reg if str(g.get("engine") or "") != "script" and str(g.get("machine") or "").upper() == mach), None)
        return str((h or integ)["name"])

    def is_home(self, g):
        return bool(g) and str(g.get("name") or "").lower() == self.home_name().lower()

    def golem(self, name):
        n = str(name or "").strip().lower()
        return next((g for g in self.registry() if str(g["name"]).lower() == n), None) if n else None

    def is_remote(self, g):
        return str(g.get("machine") or "").upper() != self.machine

    # cc-P215: every Golem that claims prompts has its own test-shard accounts, '<golem>-admin' and '<golem>-player'
    # (D:/UO/golem/GolemTestAccounts.ps1). The cards show the names; the password is read only on a button press.
    @staticmethod
    def claims(g):
        return str(g.get("engine") or "") in ("claude", "codex")

    def test_accounts(self, g):
        n = str(g["name"]).lower()
        return {"admin": n + "-admin", "player": n + "-player",
                "copy": os.path.isfile(os.path.join(self.golem_root, "test-accounts-%s.key" % n))}

    def test_password(self, g, role):
        """One account's password, through the Golem kit's own helper (DPAPI for the Windows account this service runs
        as, which on the game PC is Chase's). (code, text): 200 and the password, or an error; never logged."""
        if role not in ("admin", "player"):
            return 400, "role is admin or player"
        if not self.claims(g):
            return 404, "%s claims no prompts and has no test accounts" % g["name"]
        if not self.test_accounts(g)["copy"]:
            return 404, "no test account file for %s on this PC" % g["name"]
        script = os.path.join(self.golem_root, "Get-GolemTestAccount.ps1")
        try:
            r = (self.password_reader or _read_test_password)(script, g["name"], role, self.golem_root)
        except Exception as e:  # noqa: BLE001 - the reason, never the value
            return 500, "the helper failed: %s" % type(e).__name__
        return (200, r) if r else (500, "the helper gave no password")

    def remote_dir(self, name):
        return os.path.join(self.golem_root, "remote", name)

    def command_minutes(self):
        s = self._load_json(self.settings_file, None)
        v = s.get("agentCommandMinutes") if isinstance(s, dict) else None
        return v if isinstance(v, int) and not isinstance(v, bool) and 1 <= v <= 1440 else COMMAND_MINUTES

    def _iso(self, t):
        return time.strftime("%Y-%m-%dT%H:%M:%S", time.localtime(t))

    @staticmethod
    def _when(iso):
        try:
            return time.mktime(time.strptime(str(iso)[:19], "%Y-%m-%dT%H:%M:%S"))
        except (TypeError, ValueError):
            return 0.0

    @staticmethod
    def _hash(salt, secret):
        return hashlib.sha256((salt + ":" + secret).encode("utf-8")).hexdigest()

    def _agents(self):
        d = self._load_json(self.agents_file, None)
        return d if isinstance(d, dict) and isinstance(d.get("golems"), dict) else {"_comment": AGENTS_COMMENT, "golems": {}}

    def mint_agent(self, name):
        """A new secret for that Golem's agent (any old one stops working). Returns it: shown once, never stored here;
        golem-agents.json keeps a salted SHA-256 of it. The secret is 32 random bytes, so one hash is enough."""
        g = self.golem(name)
        if not g:
            raise ValueError("no Golem named %r in %s" % (name, os.path.join(self.golem_root, "golems.json")))
        secret = secrets.token_urlsafe(32)
        salt = secrets.token_hex(16)
        with self.queue_lock:
            d = self._agents()
            d["golems"][g["name"]] = {"salt": salt, "sha256": self._hash(salt, secret), "minted": self._iso(time.time())}
            self._write_json(self.agents_file, d)
        return secret

    def revoke_agent(self, name):
        with self.queue_lock:
            d = self._agents()
            gone = d["golems"].pop(str(name), None) is not None
            self._write_json(self.agents_file, d)
        return gone

    def agent_auth(self, headers):
        """(golem, why) when the request is that remote Golem's agent with its own secret, else (None, why)."""
        name = (headers.get(panels.AGENT_NAME_HEADER) or "").strip()
        key = (headers.get(panels.AGENT_KEY_HEADER) or "").strip()
        if not GOLEM_NAME.match(name):
            return None, "no Golem name"
        g = self.golem(name)
        if not g:
            return None, "no such Golem"
        if not self.is_remote(g):
            return None, "a Golem on this PC has no agent"
        if not key:
            return None, "no agent secret"
        rec = self._agents()["golems"].get(g["name"])
        if not isinstance(rec, dict) or not rec.get("salt") or not rec.get("sha256"):
            return None, "no agent secret minted"
        if not hmac.compare_digest(self._hash(str(rec["salt"]), key).encode("ascii"), str(rec["sha256"]).encode("ascii", "replace")):
            return None, "wrong agent secret"
        return g, "agent " + g["name"]

    def seen(self, g, what):
        d = self.remote_dir(g["name"])
        self._write_json(os.path.join(d, "agent.json"), {"golem": g["name"], "lastSeen": self._iso(self.clock()), "last": what})

    def kit_manifest(self):
        """cc-P222: the Golem kit's manifest as the game PC last snapshotted it (Kit.ps1 New-KitSnapshot), or None."""
        m = self._load_json(os.path.join(self.golem_root, "kit", "current", "kit-manifest.json"), None)
        return m if isinstance(m, dict) and isinstance(m.get("files"), list) else None

    def kit_file(self, path):
        """cc-P222: one file of the kit snapshot, as bytes, or None. Only a path the manifest lists (golem/<name> or
        unattended-permission.ps1, no folders, no ..): the snapshot holds no secret, state or settings file."""
        m = self.kit_manifest()
        if not m or not isinstance(path, str) or not KIT_PATH.match(path):
            return None
        if path not in {f.get("path") for f in m["files"] if isinstance(f, dict)}:
            return None
        full = os.path.join(self.golem_root, "kit", "current", *path.split("/"))
        try:
            with open(full, "rb") as fh:
                return fh.read()
        except OSError:
            return None

    def agent_online(self, g):
        a = self._load_json(os.path.join(self.remote_dir(g["name"]), "agent.json"), None)
        t = self._when((a or {}).get("lastSeen")) if isinstance(a, dict) else 0.0
        return bool(t) and self.clock() - t <= AGENT_ONLINE

    def _cmd_dir(self, name):
        return os.path.join(self.remote_dir(name), "commands")

    def _commands(self, name):
        try:
            names = sorted(n for n in os.listdir(self._cmd_dir(name)) if n.endswith(".json"))
        except OSError:
            return []
        out = []
        for n in names:
            r = self._load_json(os.path.join(self._cmd_dir(name), n), None)
            if isinstance(r, dict) and COMMAND_ID.match(str(r.get("id") or "")) and n == r["id"] + ".json":
                out.append(r)
        return out

    def _save_command(self, name, rec):
        self._write_json(os.path.join(self._cmd_dir(name), rec["id"] + ".json"), rec)

    def _sweep(self, g):
        """Closes what waited too long, each onto the Timeline: a queued command past its expiry, a handed-out one with
        no result after TAKEN_MINUTES. Keeps the newest 50 closed ones. Call with queue_lock held."""
        now, name = self.clock(), g["name"]
        closed = []
        for r in self._commands(name):
            if r.get("state") == "queued" and self._when(r.get("expiresAt")) <= now:
                r.update(state="expired", endedAt=self._iso(now), code=None,
                         result="expired: %s did not come online within %s minutes" % (name, r.get("minutes") or self.command_minutes()))
            elif r.get("state") == "taken" and self._when(r.get("takenAt")) + TAKEN_MINUTES * 60 <= now:
                r.update(state="unanswered", endedAt=self._iso(now), code=None,
                         result="no answer: %s took it but sent no result within %d minutes" % (name, TAKEN_MINUTES))
            else:
                if r.get("state") not in ("queued", "taken"):
                    closed.append(r)
                continue
            self._save_command(name, r)
            self.log_action(r, "", r["result"], None, self.loggable(r.get("inputs")), golem=name)
            closed.append(r)
        closed.sort(key=lambda r: r.get("endedAt") or "")
        for r in closed[:-50]:
            try:
                os.remove(os.path.join(self._cmd_dir(name), r["id"] + ".json"))
            except OSError:
                pass

    def queue_remote(self, entry, typed, inputs, who, g):
        """(http code, body): a button for a Golem on another PC, checked as on this PC (the word, the inputs, the
        state its last status shows), then queued for its agent. Nothing runs on the game PC."""
        name = g["name"]
        err, ok = self.check_request(entry, typed, inputs, name)
        if not err:
            st = self._load_json(os.path.join(self.remote_dir(name), "status.json"), None)
            st = st.get("status") if isinstance(st, dict) and isinstance(st.get("status"), dict) else st
            clean = ok[1]
            if entry.get("word") == "#stopped":
                sp = str((st or {}).get("stoppedPrompt") or "") if isinstance(st, dict) else ""
                want = (sp + " " + name).lower() if entry.get("golemWord") else sp.lower()
                if not sp:
                    err = (409, "nothing is STOPPED on %s (its last status)" % name)
                elif str(typed or "").strip().lower() != want:
                    err = (400, "type %s to confirm" % want)
                else:
                    clean = dict(clean, prompt=sp.upper())
            w = str(entry.get("when") or "")
            if not err and w.startswith(("winding:", "notWinding:")) and isinstance(st, dict):
                kind, _, val = w.partition(":")
                wind = str(st.get("winding") or "")
                if (kind == "winding") != (wind == val):
                    err = (409, "not now: %s's last status says %s" % (name, ("it is not set" if kind == "winding" else "it is already set")))
        if err:
            self.log_action(entry, who, "refused: " + err[1], None, {}, golem=name)
            return err[0], {"error": err[1]}
        with self.queue_lock:
            self._sweep(g)
            waiting = [r for r in self._commands(name) if r.get("state") in ("queued", "taken")]
            if len(waiting) >= 10:
                return 429, {"error": "%s already has %d commands waiting" % (name, len(waiting))}
            now, minutes = self.clock(), self.command_minutes()
            rec = {"id": "%013d-%s" % (int(now * 1000), secrets.token_hex(3)), "golem": name, "key": entry["key"],
                   "label": entry.get("label") or entry["key"], "action": entry.get("action") or "", "inputs": clean, "who": who,
                   "queuedAt": self._iso(now), "expiresAt": self._iso(now + minutes * 60), "minutes": minutes, "state": "queued",
                   "takenAt": None, "endedAt": None, "code": None, "result": "", "lines": []}
            self._save_command(name, rec)
        online = self.agent_online(g)
        msg = ("sent to %s: its agent takes it at its next poll (expires %s if not taken)" if online else
               "queued until %s is online (expires %s)") % (name, rec["expiresAt"][11:16])
        self.log_action(entry, who, msg, None, clean, golem=name)
        self.panels_cache = (0.0, None)
        return 202, {"queued": True, "id": rec["id"], "golem": name, "online": online, "message": msg, "expiresAt": rec["expiresAt"]}

    @staticmethod
    def loggable(inputs):
        """cc-P216: a command's inputs for the action log: an answer's words (textb64) are never logged, only their size."""
        if not isinstance(inputs, dict) or "textb64" not in inputs:
            return inputs
        return dict(inputs, textb64="(%d characters of base64, not logged)" % len(str(inputs.get("textb64") or "")))

    def take_commands(self, g):
        """The agent's poll: its queued commands (not expired), each handed out once. The typed word stays here."""
        out = []
        with self.queue_lock:
            self.seen(g, "commands")
            self._sweep(g)
            for r in self._commands(g["name"]):
                if r.get("state") == "queued":
                    r.update(state="taken", takenAt=self._iso(self.clock()))
                    self._save_command(g["name"], r)
                    out.append({"id": r["id"], "key": r["key"], "label": r.get("label"), "inputs": r.get("inputs") or {},
                                "queuedAt": r.get("queuedAt"), "who": r.get("who") or ""})
        return out

    def finish_command(self, g, body):
        cid = str(body.get("id") or "")
        if not COMMAND_ID.match(cid):
            return 400, {"error": "no such command id"}
        with self.queue_lock:
            self.seen(g, "result")
            rec = self._load_json(os.path.join(self._cmd_dir(g["name"]), cid + ".json"), None)
            if not isinstance(rec, dict) or rec.get("id") != cid:
                return 404, {"error": "no such command for %s" % g["name"]}
            if rec.get("state") != "taken":
                return 409, {"error": "that command is %s" % rec.get("state")}
            code = body.get("code") if isinstance(body.get("code"), int) and not isinstance(body.get("code"), bool) else None
            lines = body.get("lines") if isinstance(body.get("lines"), list) else []
            result = panels.redact(str(body.get("result") or "").strip())[:300] or ("exited with %s" % code)
            rec.update(state="done", code=code, result=result, endedAt=self._iso(self.clock()),
                       lines=[panels.redact(str(l))[:400] for l in lines[-60:]])
            self._save_command(g["name"], rec)
        self.log_action(rec, "", result, code, self.loggable(rec.get("inputs")), golem=g["name"])
        self.panels_cache = (0.0, None)
        return 200, {"ok": True}

    def save_report(self, g, kind, body):
        """A remote Golem's readiness file (Test-GolemReady.ps1's) or its status-<name>.json, kept in remote/<name>/."""
        name = g["name"]
        if kind == "ready":
            if str(body.get("golem") or "").lower() != name.lower():
                return 400, {"error": "the readiness file names another Golem: %r" % str(body.get("golem"))[:24]}
            if body.get("verdict") not in ("READY", "NOT READY") or not isinstance(body.get("at"), str):
                return 400, {"error": "a readiness file has verdict READY or NOT READY and at"}
            doc = dict(panels.redact_tree(body), receivedAt=self._iso(self.clock()))
        else:
            st = body.get("status")
            if not isinstance(st, dict):
                return 400, {"error": "status is not an object"}
            doc = dict(panels.redact_tree(st), receivedAt=self._iso(self.clock()))
        with self.queue_lock:
            self.seen(g, kind)
            self._write_json(os.path.join(self.remote_dir(name), kind + ".json"), doc)
        self.panels_cache = (0.0, None)
        return 200, {"ok": True, "golem": name}

    def command_view(self, g):
        """The newest eight commands for one remote Golem, for its card: what waits ("queued until <name> is online"),
        what it is running, what came back, what expired and when."""
        name = g["name"]
        with self.queue_lock:
            self._sweep(g)
            recs = self._commands(name)
        online = self.agent_online(g)
        out = []
        for r in sorted(recs, key=lambda r: r["id"], reverse=True)[:8]:
            s = r.get("state")
            note = {"queued": ("sent, waiting for %s to take it" if online else "queued until %s is online") % name + " (expires %s)" % str(r.get("expiresAt"))[11:16],
                    "taken": "%s is running it" % name}.get(s, r.get("result") or "")
            out.append({k: r.get(k) for k in ("id", "key", "label", "state", "queuedAt", "expiresAt", "takenAt", "endedAt", "code", "result", "who")})
            out[-1]["note"] = note
        return out

    # --- cc-P216: the Golems' questions for Chase, and his answers ----------------------------------------------------
    def _question_files(self, name):
        d = os.path.join(self.golem_root, "questions")
        try:
            names = os.listdir(d)
        except OSError:
            return []
        out = []
        for n in names:
            m = QUESTION_ID.match(n[:-5]) if n.endswith(".json") else None
            if not m or m.group(1).lower() != name.lower():
                continue
            q = self._load_json(os.path.join(d, n), None)
            if isinstance(q, dict) and q.get("id") == n[:-5]:
                out.append(q)
        return out

    @staticmethod
    def _history_row(q, a):
        row = {k: q.get(k) for k in ("id", "prompt", "kind", "question", "state", "askedAt", "closedAt", "closedWhy")}
        row["answer"] = {k: a.get(k) for k in ("choiceLabel", "text", "who", "source", "at", "delivered")} if isinstance(a, dict) else None
        return row

    def local_questions(self, name):
        """A Golem on this PC: its open questions (newest first) and the last five closed, with their answers."""
        qs = self._question_files(name)
        opened = sorted((q for q in qs if q.get("state") == "open"), key=lambda q: str(q.get("askedAt")), reverse=True)
        closed = sorted((q for q in qs if q.get("state") != "open"), key=lambda q: str(q.get("closedAt")), reverse=True)[:5]
        return {"open": opened, "history": [self._history_row(q, self._load_json(os.path.join(self.golem_root, "answers", q["id"] + ".json"), None)) for q in closed]}

    def remote_questions(self, g):
        """A remote Golem: what its agent last posted, and which open ones have an answer on its way (queued for the agent)."""
        d = self._load_json(os.path.join(self.remote_dir(g["name"]), "questions.json"), None)
        d = d if isinstance(d, dict) else {}
        out = {"open": [q for q in d.get("open") or [] if isinstance(q, dict)], "history": [h for h in d.get("history") or [] if isinstance(h, dict)],
               "receivedAt": d.get("receivedAt")}
        with self.queue_lock:
            self._sweep(g)
            sent = {str((r.get("inputs") or {}).get("question")): r for r in self._commands(g["name"]) if r.get("key") == "answer"}
        for q in out["open"]:
            r = sent.get(str(q.get("id")))
            if r and r.get("state") in ("queued", "taken"):
                q["answerPending"] = "answer sent: waiting for %s's agent to take it" % g["name"] if r.get("state") == "queued" else "answer sent: %s is resuming" % g["name"]
            elif r and r.get("state") == "done" and r.get("code") not in (0, None):
                q["answerFailed"] = r.get("result") or "the answer was refused there"
        return out

    def questions_for(self, name):
        g = self.golem(name)
        return self.remote_questions(g) if g and self.is_remote(g) else self.local_questions(g["name"] if g else name)

    def questions_summary(self):
        """For the tab header and a Golem working alone: the open count across every Golem, and (no registry) the one
        Golem's questions."""
        reg = [g for g in self.registry() if str(g.get("engine") or "") != "script"]
        if not reg:
            solo = self.local_questions("Golem")
            return {"open": len(solo["open"]), "golems": [{"name": "Golem", "open": len(solo["open"])}], "solo": solo}
        rows = [{"name": g["name"], "open": len(self.questions_for(g["name"])["open"])} for g in reg]
        return {"open": sum(r["open"] for r in rows), "golems": rows, "solo": None}

    def save_questions(self, g, body):
        """cc-P216: a remote Golem's questions as its agent posts them: only that Golem's, kept in remote/<name>/questions.json."""
        qs = body.get("questions")
        if not isinstance(qs, dict) or not isinstance(qs.get("open"), list) or not isinstance(qs.get("history"), list):
            return 400, {"error": "questions is {open: [...], history: [...]}"}
        name = g["name"]

        def mine(q):
            m = QUESTION_ID.match(str(q.get("id") or "")) if isinstance(q, dict) else None
            return bool(m) and m.group(1).lower() == name.lower()
        if not all(mine(q) for q in qs["open"] + qs["history"]):
            return 400, {"error": "a question that is not %s's" % name}
        doc = {"golem": name, "open": panels.redact_tree(qs["open"][:20]), "history": panels.redact_tree(qs["history"][:5]), "receivedAt": self._iso(self.clock())}
        with self.queue_lock:
            self.seen(g, "questions")
            self._write_json(os.path.join(self.remote_dir(name), "questions.json"), doc)
        self.panels_cache = (0.0, None)
        return 200, {"ok": True, "golem": name, "open": len(doc["open"])}

    def answer(self, body, who):
        """(http code, body): Chase's answer from the page. Checked against the open question as this service sees it
        (a stale page's answer to a closed one is refused), then written by the Golem's own Answer-Question.ps1 (this
        PC) or queued for the remote Golem's agent. The words are data: base64 on the command line, never a shell's."""
        qid = body.get("id")
        m = QUESTION_ID.match(qid) if isinstance(qid, str) else None
        if not m:
            return 400, {"error": "no such question"}
        name = m.group(1)
        g = self.golem(name)
        if not g and not (name == "Golem" and not self.registry()):
            return 404, {"error": "no such Golem in golems.json"}
        remote = bool(g) and self.is_remote(g)
        card = self.remote_questions(g) if remote else self.local_questions(g["name"] if g else name)
        q = next((x for x in card["open"] if x.get("id") == qid), None)
        if not q:
            h = next((x for x in card["history"] if x.get("id") == qid), None)
            why = ("closed: the question was %s%s" % (h.get("state"), (" (%s)" % h["closedWhy"]) if h.get("closedWhy") else "")) if h else "closed: it is no longer open"
            return 409, {"error": why, "closed": True}
        if q.get("answerPending"):
            return 409, {"error": "an answer is already on its way to %s" % name, "closed": True}
        choice = body.get("choice", -1)
        choices = q.get("choices") if isinstance(q.get("choices"), list) else []
        if isinstance(choice, bool) or not isinstance(choice, int) or not -1 <= choice < len(choices):
            return 400, {"error": "not one of its choices"}
        text = body.get("text") or ""
        if not isinstance(text, str):
            return 400, {"error": "the answer is text"}
        text = text.strip()
        if len(text) > ANSWER_TEXT_MAX:
            return 400, {"error": "the answer is longer than %d characters" % ANSWER_TEXT_MAX}
        if text and not q.get("freeText"):
            return 400, {"error": "this question takes one of its choices, not text"}
        if choice < 0 and not text:
            return 400, {"error": "pick a choice or write an answer"}
        if q.get("irreversible") and body.get("confirm") is not True:
            return 409, {"error": "This cannot be undone: %s. Confirm to send the answer." % (q.get("irreversibleWhy") or "the Golem says so"), "needsConfirm": True}
        who_c = WHO_UNSAFE.sub("", who or "")[:80].lstrip("-. ") or "console"
        b64 = base64.b64encode(text.encode("utf-8")).decode("ascii") if text else ""
        entry = {"key": "answer", "label": "Answer %s's question" % name, "action": "golem.answer"}
        logged = {"question": qid, "choice": choice, "words": len(text)}   # never the words themselves
        if remote:
            with self.queue_lock:
                self._sweep(g)
                now, minutes = self.clock(), self.command_minutes()
                rec = {"id": "%013d-%s" % (int(now * 1000), secrets.token_hex(3)), "golem": name, "key": "answer",
                       "label": "Answer %s" % q.get("prompt", ""), "action": "golem.answer",
                       "inputs": {"question": qid, "choice": str(choice), "textb64": b64, "who": who_c}, "who": who,
                       "queuedAt": self._iso(now), "expiresAt": self._iso(now + minutes * 60), "minutes": minutes, "state": "queued",
                       "takenAt": None, "endedAt": None, "code": None, "result": "", "lines": []}
                self._save_command(name, rec)
            online = self.agent_online(g)
            msg = ("answer sent to %s: its agent takes it at its next poll" % name) if online else ("answer queued until %s is online (expires %s)" % (name, rec["expiresAt"][11:16]))
            self.log_action(entry, who, msg, None, logged, golem=name)
            self.panels_cache = (0.0, None)
            return 202, {"queued": True, "golem": name, "online": online, "message": msg}
        args = list(self.answer_runner) + ["-Id", qid, "-Choice", str(choice), "-Who", who_c, "-Source", "console", "-Root", self.golem_root]
        if b64:
            args += ["-TextB64", b64]
        if name != "Golem":
            args += ["-Golem", name]
        if not self.answer_lock.acquire(timeout=5):
            return 409, {"error": "busy: another answer is being written; try again in a moment"}
        try:
            try:
                p = subprocess.run(args, capture_output=True, timeout=ANSWER_TIMEOUT, creationflags=NO_WINDOW)
                code, raw = p.returncode, (p.stdout or b"") + (p.stderr or b"")
            except subprocess.TimeoutExpired:
                code, raw = None, b"no answer from Answer-Question.ps1 within %d s" % ANSWER_TIMEOUT
            except OSError as e:
                code, raw = None, ("Answer-Question.ps1 did not start: %s" % e).encode("utf-8")
        finally:
            self.answer_lock.release()
        lines = [panels.redact(l)[:400] for l in raw.decode("utf-8", "replace").splitlines() if l.strip()]
        result = lines[-1] if lines else "exited with %s" % code
        self.log_action(entry, who, result, code, logged, golem=name)
        self.panels_cache = (0.0, None)
        return (200 if code == 0 else 409), {"ok": code == 0, "code": code, "result": result, "lines": lines[-20:]}

    def portrait_path(self, g):
        """The registry's portrait for that Golem when it is an image under scripts/console-art; else ''."""
        p = g.get("portrait")
        if not isinstance(p, str) or not p or not PORTRAIT_NAME.search(p):
            return ""
        base = os.path.realpath(os.path.join(self.repo, "scripts", "console-art"))
        full = os.path.realpath(p)
        return full if full.startswith(base + os.sep) and os.path.isfile(full) else ""

    def golem_cards(self, doc):
        """cc-P186: the status file's Golem cards (Get-GolemCards, as of the collector's last read) with what this
        service knows fresher: a remote Golem's agent (last seen, online), the status and readiness it posted, a local
        Golem's readiness file, the commands waiting, the portrait's address and (cc-P207) its glow. Changes doc in
        place."""
        cards = (((doc or {}).get("golem") or {}).get("golems")) if isinstance(doc, dict) else None
        if not isinstance(cards, list):
            return
        gs = self._load_json(os.path.join(self.golem_root, "settings.json"), {}) or {}
        try:
            stall = int(gs.get("stallMinutes") or panels.STALL_MINUTES) if isinstance(gs, dict) else panels.STALL_MINUTES
        except (TypeError, ValueError):
            stall = panels.STALL_MINUTES
        now = self.clock()
        for c in cards:
            g = self.golem(c.get("name")) if isinstance(c, dict) else None
            if not g:
                continue
            remote = self.is_remote(g)
            c["remote"] = remote
            if remote:
                d = self.remote_dir(g["name"])
                a = self._load_json(os.path.join(d, "agent.json"), None)
                c["lastSeen"] = (a.get("lastSeen") if isinstance(a, dict) else None) or None
                c["online"] = self.agent_online(g)
                st = self._load_json(os.path.join(d, "status.json"), None)
                if isinstance(st, dict):
                    for k in ("state", "current", "winding", "stoppedPrompt", "needsYou", "restReason", "branch",
                              "stepsDone", "stepsTotal", "lanePaused", "note"):   # cc-P159: a Codex lane's step, pause and note
                        if k in st:
                            c[k] = st[k]
                    c["statusAt"] = st.get("generatedAt") or st.get("receivedAt")
                if str(g.get("engine") or "") == "codex":
                    c["laneLine"] = panels.lane_line(c)   # cc-P159: from what its agent posted, fresher than the status file
                ready = self._load_json(os.path.join(d, "ready.json"), None)
                c["commands"] = self.command_view(g)
            else:
                c["online"] = True
                c.setdefault("lastSeen", None)
                ready = self._load_json(os.path.join(self.golem_root, "ready-%s.json" % g["name"]), None)
                c["commands"] = []
                st = self._load_json(os.path.join(self.golem_root, "status-%s.json" % g["name"]), None)
            # cc-P207: the glow behind its portrait, from the state above and its status file's last activity.
            c["glow"] = panels.glow_state(c, st, now, stall)
            r = panels.ready_summary(ready)
            c["ready"] = r if r else (c.get("ready") if isinstance(c.get("ready"), dict) else None)
            c["portraitUrl"] = ("/portrait/" + g["name"]) if self.portrait_path(g) else ""
            c["testAccounts"] = self.test_accounts(g) if self.claims(g) else None
            c.pop("portrait", None)   # a path on this PC: the page gets the address above
            c["questions"] = self.remote_questions(g) if remote else self.local_questions(g["name"])   # cc-P216

    def _write_json(self, path, value):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        tmp = path + ".tmp-%d" % threading.get_ident()
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(value, f, ensure_ascii=True, indent=1)
        os.replace(tmp, path)

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

    def _music_loop(self):
        """cc-P367: once an hour, commit music-notes.json when it changed (music.commit_notes: that file alone, on main,
        never while a merge or a Golem's commit is under way, never pushed)."""
        while True:
            time.sleep(MUSIC_COMMIT_SECONDS)
            try:
                if self.music.changed():
                    self.music.commit("hourly")
            except Exception:
                pass

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
            "live": ("The LIVE shard (docker logs sl-modernuo)", ("docker", "sl-modernuo")),
            "diag": ("The newest diagnostic (Check-Docker, Check-Shard)", ("newestmatch", self.repo)),
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
        if how == "newestmatch":
            try:
                hit = sorted((os.path.getmtime(os.path.join(where, n)), n) for n in os.listdir(where)
                             if re.match(r"^(check-[A-Za-z0-9_-]+\.txt|test-shard-diagnostic\.log)$", n))
            except OSError:
                hit = []
            return (self._tail_file(os.path.join(where, hit[-1][1])) if hit else []), (hit[-1][1] if hit else "")
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


def _read_test_password(script, golem, role, root):
    """cc-P215: Get-GolemTestAccount.ps1 -Json, as this service's Windows account (its DPAPI)."""
    r = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Golem", golem,
                        "-Role", role, "-Json", "-Root", root], capture_output=True, text=True, timeout=60)
    if r.returncode != 0:
        return ""
    return json.loads(r.stdout.strip().splitlines()[-1]).get("Password") or ""


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

        def _agent(self):
            """cc-P186: the remote Golem whose agent this is, or None (answered 403). The tailnet listener and the
            sidecar's proof (panels.decide_agent; no Tailscale login: a tagged PC has none), then that Golem's own
            secret (Console.agent_auth). An agent reaches only /api/agent/..., never the page or a button."""
            ok, why = (False, "not loopback") if self.client_address[0] not in LOOPBACK else panels.decide_agent(listener, self.headers, console.proof())
            g = None
            if ok:
                g, why = console.agent_auth(self.headers)
            if not g:
                self._note(403, why)
                self._json(403, {"error": "not allowed"})
            return g

        def _read_json(self, limit):
            """The request's JSON object, or None (already answered: 415, 413 or 400)."""
            if not (self.headers.get("Content-Type") or "").lower().startswith("application/json"):
                self._note(415, "not JSON")
                self._json(415, {"error": "JSON only"})
                return None
            try:
                n = int(self.headers.get("Content-Length") or 0)
            except ValueError:
                n = -1
            if n < 0 or n > limit:
                self._json(413, {"error": "too large"})
                return None
            try:
                body = json.loads(self.rfile.read(n).decode("utf-8") or "{}")
                if not isinstance(body, dict):
                    raise ValueError("not an object")
            except ValueError:
                self._json(400, {"error": "not JSON"})
                return None
            return body

        def do_HEAD(self):
            self.do_GET()

        def do_GET(self):
            u = urlparse(self.path)
            if u.path.startswith("/api/agent/"):
                # cc-P186: a remote Golem's agent, polling.
                g = self._agent()
                if not g:
                    return
                if u.path == "/api/agent/ping":
                    console.seen(g, "ping")
                    return self._json(200, {"ok": True, "ack": True, "golem": g["name"], "now": time.strftime("%Y-%m-%dT%H:%M:%S"),
                                            "pollAwake": POLL_AWAKE, "pollAsleep": POLL_ASLEEP})
                if u.path == "/api/agent/commands":
                    return self._json(200, {"golem": g["name"], "commands": console.take_commands(g), "pollAwake": POLL_AWAKE, "pollAsleep": POLL_ASLEEP})
                if u.path == "/api/agent/kit":
                    m = console.kit_manifest()
                    return self._json(200, m) if m else self._json(404, {"error": "no kit snapshot"})
                if u.path == "/api/agent/kit/file":
                    data = console.kit_file((parse_qs(u.query).get("path") or [""])[0])
                    if data is None:
                        return self._json(404, {"error": "not a kit file"})
                    return self._send(200, data, "application/octet-stream")
                return self._json(404, {"error": "not found"})
            if not self._allowed():
                return
            p, q = u.path, parse_qs(u.query)
            if p in ("/", "/index.html"):
                return self._static("index.html")
            if p in ("/app.js", "/app.css"):
                return self._static(p[1:])
            if p in ("/music", "/music.html", "/music.js", "/music.css"):
                return self._static("music.html" if p in ("/music", "/music.html") else p[1:])
            if p == "/api/music":
                m = console.music
                return self._json(200, {"packs": m.packs_view(), "tracks": m.tracks(), "notes": m.notes(),
                                        "tags": [{"id": i, "label": l} for i, l in music.TAGS], "ratings": list(music.RATINGS),
                                        "suggestions": m.suggestions(), "lastCommit": m.last_commit,
                                        "notesFile": "/".join(music.NOTES_REL), "noteMax": music.NOTE_MAX})
            if p == "/api/music/summary":
                return self._json(200, console.music.summary())
            mf = MUSIC_FILE.match(p)
            if mf:
                path = console.music.path_of(mf.group(1), unquote(mf.group(2)))
                if not path:
                    return self._send(404, "no such track\n", "text/plain; charset=utf-8")
                return self._stream(path, "audio/mpeg")
            if p.startswith("/art/"):
                name = p[5:]
                path = os.path.join(console.repo, "scripts", "console-art", name)
                if not ART_NAME.match(name) or not os.path.isfile(path):
                    return self._send(404, "no such art\n", "text/plain; charset=utf-8")
                with open(path, "rb") as f:
                    return self._send(200, f.read(), mimetypes.guess_type(name)[0] or "application/octet-stream")
            if p.startswith("/portrait/"):
                g = console.golem(p[10:]) if GOLEM_NAME.match(p[10:]) else None
                path = console.portrait_path(g) if g else ""
                if not path:
                    return self._send(404, "no portrait\n", "text/plain; charset=utf-8")
                with open(path, "rb") as f:
                    return self._send(200, f.read(), mimetypes.guess_type(path)[0] or "application/octet-stream")
            if p == "/api/health":
                return self._json(200, {"ok": True})
            if p == "/api/status":
                asked = console.ask_refresh() if q.get("refresh") == ["1"] else False
                doc, age = console.status()
                console.golem_cards(doc)
                job = console.job_view(10**9)
                return self._json(200, {"status": doc, "ageSeconds": age, "refreshAsked": asked,
                                        "panels": console.panels(doc), "logSources": {k: v[0] for k, v in console.log_sources().items()},
                                        "art": console.art(), "artFrames": console.art_frames(),
                                        "actions": {"buttons": console.public_buttons(), "canAct": listener == panels.TAILNET,
                                                    "why": "" if listener == panels.TAILNET else LOCAL_READ_ONLY, "job": job},
                                        "pages": console.public_pages(), "golemPicker": console.golem_picker(doc),
                                        "questions": console.questions_summary(),   # cc-P216
                                        "server": {"now": time.strftime("%Y-%m-%dT%H:%M:%S"), "listener": listener}})
            if p == "/api/page":
                d = console.page_data((q.get("id") or [""])[0], fresh=q.get("fresh") == ["1"])
                return self._json(200, d) if d is not None else self._json(404, {"error": "no such page"})
            if p == "/api/prompt":
                text, what = console.prompt_text(name=(q.get("name") or [""])[0] or None, notes=(q.get("notes") or [""])[0] or None)
                if text is None:
                    return self._json(404, {"error": what})
                return self._json(200, {"what": what, "text": text})
            if p.startswith("/art-thumb/"):
                path = console.art_thumb_path(p[11:])
                if not path:
                    return self._send(404, "no picture\n", "text/plain; charset=utf-8")
                with open(path, "rb") as f:
                    return self._send(200, f.read(), mimetypes.guess_type(path)[0] or "application/octet-stream")
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
            # cc-P186: a remote Golem's agent posting back: a result, its status, its readiness.
            path = urlparse(self.path).path
            if path in ("/api/agent/result", "/api/agent/status", "/api/agent/ready", "/api/agent/questions"):
                g = self._agent()
                if not g:
                    return
                body = self._read_json(AGENT_BODY)
                if body is None:
                    return
                if path == "/api/agent/questions":
                    code, out = console.save_questions(g, body)   # cc-P216
                else:
                    code, out = console.finish_command(g, body) if path == "/api/agent/result" else console.save_report(g, path.rsplit("/", 1)[1], body)
                self._note(code, "agent %s %s" % (g["name"], path.rsplit("/", 1)[1]))
                return self._json(code, out)
            # cc-P182: the Golem's buttons; cc-P187: the page buttons and their look first.
            if path in ("/api/music/note", "/api/music/commit"):
                return self._music_post(path)
            if path not in ("/api/action", "/api/preview", "/api/golem-password", "/api/answer"):
                return self._refuse()
            if not self._allowed():
                return
            if listener != panels.TAILNET:
                self._note(403, "action on the local listener")
                return self._json(403, {"error": LOCAL_READ_ONLY})
            why = self._not_this_page()
            if why:
                self._note(403, why)
                return self._json(403, {"error": "not allowed: " + why})
            body = self._read_json(16384 if path == "/api/answer" else 4096)
            if body is None:
                return
            who = (self.headers.get(panels.IDENTITY_HEADER) or "").strip()
            if path == "/api/golem-password":
                # cc-P215: Chase's "copy password" on a Golem card. The tailnet listener, his login, and the same checks
                # as a button above; plain text (not JSON, which the redaction would blank), no-store, logged without it.
                gname = body.get("golem")
                g = console.golem(gname) if isinstance(gname, str) and GOLEM_NAME.match(gname) else None
                if not g:
                    self._note(404, "test password: no such Golem")
                    return self._json(404, {"error": "no such Golem in golems.json"})
                code, text = console.test_password(g, str(body.get("role") or ""))
                self._note(code, "test password %s %s by %s" % (g["name"], body.get("role"), who or "?"))
                if code != 200:
                    return self._json(code, {"error": text})
                return self._send(200, text, "text/plain; charset=utf-8")
            if path == "/api/answer":
                # cc-P216: Chase's answer to a Golem's question (the words: up to 2000 characters, as data).
                code, out = console.answer(body, who)
                self._note(code, "answer %s" % str(body.get("id"))[:60])
                return self._json(code, out)
            pentry, page = console.page_button(body.get("key"))
            if path == "/api/preview":
                if not pentry:
                    return self._json(404, {"error": "no such page button"})
                code, out = console.preview(pentry, body.get("inputs"))
                self._note(code, "preview " + pentry["key"])
                return self._json(code, out)
            if pentry:
                code, out = console.start_page_action(pentry, page, body, who)
                self._note(code, "action " + pentry["key"])
                return self._json(code, out)
            entry = next((b for b in console.buttons() if b.get("key") == body.get("key")), None)
            if not entry:
                self._note(404, "no such button")
                return self._json(404, {"error": "no such button"})
            who = (self.headers.get(panels.IDENTITY_HEADER) or "").strip()
            # cc-P186: which Golem. None, or the integrator, is the console's own Golem as before.
            gname = body.get("golem")
            if gname not in (None, ""):
                g = console.golem(gname) if isinstance(gname, str) and GOLEM_NAME.match(gname) else None
                if not g:
                    self._note(404, "no such Golem")
                    return self._json(404, {"error": "no such Golem in golems.json"})
                if str(g.get("engine") or "") == "script":   # cc-P208: its card's buttons go without a Golem
                    self._note(409, "action %s for %s" % (entry["key"], g["name"]))
                    return self._json(409, {"error": "the %s Golem only merges: its buttons are on its card" % g["name"]})
                if not console.is_home(g):
                    refuse = ("%s is the integrator's only" % (entry.get("label") or entry["key"])) if not entry.get("anyGolem") else (
                        "the %s Golem is disabled in golems.json" % g["name"] if not g.get("enabled") else "")
                    if not refuse and entry.get("engine") and str(g.get("engine") or "") != str(entry.get("engine")):   # cc-P159
                        refuse = "%s is a Codex lane's: the %s Golem is not one" % (entry.get("label") or entry["key"], g["name"])
                    if refuse:
                        self._note(409, "action %s for %s" % (entry["key"], g["name"]))
                        return self._json(409, {"error": refuse})
                    if console.is_remote(g):
                        code, out = console.queue_remote(entry, body.get("typed"), body.get("inputs"), who, g)
                    else:
                        code, out = console.start_action(entry, body.get("typed"), body.get("inputs"), who, golem=g["name"])
                    self._note(code, "action %s for %s" % (entry["key"], g["name"]))
                    return self._json(code, out)
            code, out = console.start_action(entry, body.get("typed"), body.get("inputs"), who)
            self._note(code, "action " + entry["key"])
            return self._json(code, out)

        def _not_this_page(self):
            """Why a POST is not from this console's own page, or '': a header no form or plain link can send (a
            cross-site script needs a CORS preflight, which is never answered), the same origin when the browser
            says, and JSON."""
            if self.headers.get("X-UOConsole-Action") != "1":
                return "no X-UOConsole-Action header"
            origin = self.headers.get("Origin")
            if origin and urlparse(origin).netloc.lower() != (self.headers.get("Host") or "").lower():
                return "another origin"
            if (self.headers.get("Sec-Fetch-Site") or "same-origin") not in ("same-origin", "none"):
                return "a cross-site fetch"
            return ""

        def _music_post(self, path):
            """cc-P367: a Music Room note or its Save to repo. Either listener (both are Chase), only from the page."""
            if not self._allowed():
                return
            why = self._not_this_page()
            if why:
                self._note(403, why)
                return self._json(403, {"error": "not allowed: " + why})
            body = self._read_json(MUSIC_BODY)
            if body is None:
                return
            if path == "/api/music/commit":
                ok, msg = console.music.commit("button")
                self._note(200, "music commit: " + ("done" if ok else "not done"))
                return self._json(200, {"ok": ok, "result": msg, "lastCommit": console.music.last_commit})
            rec, err = console.music.save_note(body.get("key"), body)
            if err:
                self._note(400, "music note: " + err)
                return self._json(400, {"error": err})
            self._note(200, "music note")
            return self._json(200, {"record": rec})

        def _stream(self, path, ctype):
            """cc-P367: a file in 256 KB pieces, with a single Range (bytes=a-b, a- or -n) answered 206 so the
            player can seek; a range past the end is 416. Anything else gets the whole file (200)."""
            size = os.path.getsize(path)
            start, end, code = 0, size - 1, 200
            m = re.match(r"^bytes=(\d*)-(\d*)$", (self.headers.get("Range") or "").strip())
            if m and (m.group(1) or m.group(2)):
                if m.group(1):
                    start = int(m.group(1))
                    end = min(int(m.group(2)), size - 1) if m.group(2) else size - 1
                else:
                    start = max(0, size - int(m.group(2)))
                if start >= size or start > end:
                    self.send_response(416)
                    self.send_header("Content-Range", "bytes */%d" % size)
                    self.send_header("Content-Length", "0")
                    self.end_headers()
                    return
                code = 206
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(end - start + 1))
            self.send_header("Accept-Ranges", "bytes")
            if code == 206:
                self.send_header("Content-Range", "bytes %d-%d/%d" % (start, end, size))
            self.send_header("Cache-Control", "private, max-age=3600")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.end_headers()
            if self.command == "HEAD":
                return
            try:
                with open(path, "rb") as f:
                    f.seek(start)
                    left = end - start + 1
                    while left > 0:
                        chunk = f.read(min(262144, left))
                        if not chunk:
                            break
                        self.wfile.write(chunk)
                        left -= len(chunk)
            except (ConnectionError, OSError):
                pass   # the player moved on (a seek or the next track) and closed this one

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
    ap.add_argument("--golem-root", default=DEFAULT_GOLEM_ROOT, help="cc-P186: golems.json and remote\\<name>\\")
    ap.add_argument("--mint-agent", metavar="NAME", help="cc-P186: make a new secret for that remote Golem's agent, print it once, and exit")
    ap.add_argument("--revoke-agent", metavar="NAME", help="cc-P186: forget that Golem's agent secret (its agent is refused from now on), and exit")
    a = ap.parse_args(argv)
    if a.mint_agent or a.revoke_agent:
        c = Console(a.repo, a.notes, a.data, a.status_file, collector=False, golem_root=a.golem_root)
        if a.revoke_agent:
            print("revoked" if c.revoke_agent(a.revoke_agent) else "there was no secret for %s" % a.revoke_agent)
            return
        try:
            secret = c.mint_agent(a.mint_agent)
        except ValueError as e:
            sys.exit(str(e))
        name = c.golem(a.mint_agent)["name"]
        print("The %s Golem's agent secret (shown this once; only its hash is kept in %s):\n\n    %s\n" % (name, c.agents_file, secret))
        print("On %s, signed in as the golem account, save it with:\n    powershell -NoProfile -ExecutionPolicy Bypass -File D:\\UO\\golem\\Remote-Agent.ps1 -Golem %s -SaveKey\n"
              "(it asks for the secret and keeps it with Windows DPAPI for that account only), then check it with -Ping."
              % (c.golem(a.mint_agent).get("machine") or "its PC", name))
        print("Minting again replaces it; --revoke-agent %s refuses the agent at once. The running web console reads the new hash at the next poll." % name)
        return
    if a.bind not in ("127.0.0.1", "::1"):
        sys.exit("refused: the web console listens on the loopback only (127.0.0.1); the tailnet reaches it through the sidecar")
    os.makedirs(a.data, exist_ok=True)
    console = Console(a.repo, a.notes, a.data, a.status_file, collector=not a.no_collector, golem_root=a.golem_root)
    access = os.path.join(a.data, "access.log")
    tail_srv = ThreadingHTTPServer((a.bind, a.tailnet_port), make_handler(console, panels.TAILNET, access))
    threading.Thread(target=tail_srv.serve_forever, daemon=True).start()
    local = ThreadingHTTPServer((a.bind, a.local_port), make_handler(console, panels.LOCAL, access))
    print("web console on http://%s:%d/ (local) and %s:%d (tailnet, through the uoconsole sidecar)" % (a.bind, a.local_port, a.bind, a.tailnet_port), flush=True)
    local.serve_forever()


if __name__ == "__main__":
    main()
