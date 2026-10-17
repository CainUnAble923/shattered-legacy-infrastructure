"""cc-P155: the web console's panels, as pure functions over what the service read (testable from fixtures).

    decide(listener, headers, allowed)       who may look: the local listener (this PC) always; the tailnet listener only
                                             with Tailscale Serve's identity header naming an allowed login
    redact(text) / redact_tree(value)        tokens, keys and passwords blanked in anything sent to a page
    needs_you(status, playtests, decisions, chores, services)
                                             one list of what waits on Chase
    timeline(commits, transcripts, deploys, finished, bot_runs)
                                             what happened, newest first
    links(status, extra, checks, repos)      every link with its up dot
    tail(lines, n, query)                    the last n lines (of those matching query), redacted
    parse_chores(text) / parse_transcript_name(name) / parse_git_log(text, repo)

Nothing here reads a file, runs a process or touches the network; server.py does, and hands the results in.
"""
import re

PLAYTESTS_SHOWN = 10   # the first ten play-test items marked yours, then a count (85 were on 2026-10-17)
LOCAL = "local"
TAILNET = "tailnet"
IDENTITY_HEADER = "Tailscale-User-Login"


# --- who may look ---------------------------------------------------------------------------------------------------

def decide(listener, headers, allowed):
    """(allowed, why). headers: a mapping (case-insensitive lookups are the caller's: http.server's message is).

    The local listener (127.0.0.1:8095) is this PC: anything on it is Chase at the game PC, as the PowerShell console
    is. The tailnet listener (127.0.0.1:8096) is only ever reached through the uoconsole sidecar's Tailscale Serve, which
    sets Tailscale-User-Login to the caller's login (and drops any the caller sent); a tagged device or a request with
    no login gets no header, and is refused, as is every login not in allowed (compared without case)."""
    if listener == LOCAL:
        return True, "local"
    if listener != TAILNET:
        return False, "unknown listener"
    login = (headers.get(IDENTITY_HEADER) or "").strip()
    if not login:
        return False, "no Tailscale identity"
    ok = {a.strip().lower() for a in (allowed or []) if a and a.strip()}
    if login.lower() in ok:
        return True, "tailnet " + login
    return False, "login not allowed"


# --- never a secret on a page ---------------------------------------------------------------------------------------

_SECRET_PATTERNS = [
    (re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----.*?(-----END [A-Z ]*PRIVATE KEY-----|$)", re.S), "[private key removed]"),
    (re.compile(r"(?i)\b(bearer)\s+[A-Za-z0-9._~+/=-]{8,}"), r"\1 [removed]"),
    (re.compile(r"(?i)\b(authorization)\s*[:=]\s*\S+(\s+\S+)?"), r"\1: [removed]"),
    (re.compile(r"(?i)\b([A-Za-z_]*(?:token|password|passwd|secret|api[_-]?key|authkey|auth_key|pwd))\b(\s*[:=]\s*)(\"[^\"]*\"|'[^']*'|\S+)"),
     r"\1\2[removed]"),
    (re.compile(r"\b(tskey-[A-Za-z0-9-]{6,}|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9_-]{20,}|glsa_[A-Za-z0-9_]{10,}|ptr_[A-Za-z0-9]{10,}|xox[abp]-[A-Za-z0-9-]{10,})"),
     "[removed]"),
    (re.compile(r"\bssh-(ed25519|rsa) AAAA[A-Za-z0-9+/=]{20,}"), r"ssh-\1 [key removed]"),
]


def redact(text):
    s = "" if text is None else str(text)
    for rx, rep in _SECRET_PATTERNS:
        s = rx.sub(rep, s)
    return s


def redact_tree(value):
    """Every string in a JSON-ready value, redacted (dict keys are left alone)."""
    if isinstance(value, str):
        return redact(value)
    if isinstance(value, list):
        return [redact_tree(v) for v in value]
    if isinstance(value, dict):
        return {k: redact_tree(v) for k, v in value.items()}
    return value


# --- the overseer's chores ------------------------------------------------------------------------------------------

_CHORE = re.compile(r"^[-*]\s+\[( |x|X)\]\s+(.+?)\s*$")   # at the start of a line: an indented example is not a chore


def parse_chores(text):
    """[{text, done}] from '- [ ] text' and '- [x] text' lines at the start of a line; anything else is ignored."""
    out = []
    for line in (text or "").splitlines():
        m = _CHORE.match(line)
        if m:
            out.append({"text": m.group(2), "done": m.group(1).lower() == "x"})
    return out


# --- what waits on Chase --------------------------------------------------------------------------------------------

def needs_you(status, playtests=None, decisions=None, chores=None, services=None):
    """[{kind, text, link, where}] in the order Chase should look: the Golem first, then decisions, play tests, deploys,
    the package, chores. status: the status file's document; playtests: play-tests.json annotated by playwho;
    decisions: P146's testing/decisions.json (None until P146 is built); services: docker/haven/services.json."""
    out = []
    g = (status or {}).get("golem") or {}
    state = (g.get("state") or "").upper()
    if state == "STOPPED" or g.get("stoppedPrompt"):
        out.append({"kind": "stopped", "text": "The Golem stopped" + (": " + g["stoppedPrompt"] if g.get("stoppedPrompt") else "")
                    + (" (" + g["reason"] + ")" if g.get("reason") else ""), "link": "#golem", "where": "Golem"})
    if g.get("needsYou"):
        out.append({"kind": "golem", "text": "The Golem needs you: " + g["needsYou"], "link": "#golem", "where": "Golem"})
    for f in g.get("finished") or []:
        if (f.get("result") or "").upper() == "STOPPED":
            out.append({"kind": "stopped", "text": "P%s stopped: %s" % (f.get("number"), f.get("headline") or f.get("title") or ""),
                        "link": "#golem", "where": "Golem"})
    for d in ((decisions or {}).get("decisions") or (decisions or {}).get("items") or []):
        if (d.get("status") or "open") in ("open", "new", ""):
            out.append({"kind": "decision", "text": "Decide: " + (d.get("title") or d.get("question") or d.get("id") or "a decision"),
                        "link": d.get("link") or d.get("card") or "", "where": "Play tests"})
    who_keys = ("you", "botyou", "decide")
    mine = [it for it in (playtests or {}).get("items") or []
            if it.get("status") in ("open", "later", "blocked") and (it.get("who") or {}).get("key") in who_keys]
    for it in mine[:PLAYTESTS_SHOWN]:
        w = it.get("who") or {}
        out.append({"kind": "playtest", "text": "%s: %s" % (it.get("id"), it.get("what") or ""), "link": "",
                    "where": "Play tests (" + (w.get("label") or "You") + ")"})
    if len(mine) > PLAYTESTS_SHOWN:
        out.append({"kind": "playtest", "text": "and %d more play-test items marked yours, on the play-test page" % (len(mine) - PLAYTESTS_SHOWN),
                    "link": "", "where": "Play tests"})
    for s in (services or {}).get("services") or []:
        # Built and waiting for its deploy button (P133's inventory says "not deployed yet"); a service only planned
        # (Prometheus, Grafana before they exist) is not a deploy waiting.
        if (s.get("state") or "") == "planned" and "not deployed" in (s.get("verified") or ""):
            out.append({"kind": "deploy", "text": "Deploy waiting: " + (s.get("name") or s.get("key")), "link": "",
                        "where": "Websites tab"})
    pk = ((status or {}).get("lights") or {}).get("package") or {}
    if (pk.get("word") or "").upper() == "PUBLISH":
        out.append({"kind": "package", "text": "A player package is ready to publish: " + (pk.get("detail") or ""), "link": "",
                    "where": "Player package tab"})
    for c in chores or []:
        if not c.get("done"):
            out.append({"kind": "chore", "text": c["text"], "link": "", "where": "chore"})
    return out


# --- what happened --------------------------------------------------------------------------------------------------

_TRANSCRIPT = re.compile(r"^(\d{8})-(\d{6})_(.+?)(?:_([a-z]+))?\.log$")
KIND_OF_ACTION = [
    (re.compile(r"^(test|fix)\.rebuild$"), "build"),
    (re.compile(r"^(test\.deploy|web\.deploy|ops-|mega-|print-queue)"), "deploy"),
    (re.compile(r"^package\.publish$"), "publish"),
    (re.compile(r"^package\.build$"), "build"),
    (re.compile(r"^commit\.run$"), "commit"),
]


def parse_transcript_name(name):
    """(iso time, action, shard) from '20261016-080534_ops-cutover.log' or '20260929-163354_test.rebuild_test.log'."""
    m = _TRANSCRIPT.match(name or "")
    if not m:
        return None
    d, t, action, shard = m.groups()
    iso = "%s-%s-%sT%s:%s:%s" % (d[0:4], d[4:6], d[6:8], t[0:2], t[2:4], t[4:6])
    return iso, action, shard or ""


def kind_of_action(action):
    for rx, kind in KIND_OF_ACTION:
        if rx.search(action or ""):
            return kind
    return "action"


def parse_git_log(text, repo):
    """[{time, kind 'commit', text, repo, who}] from 'hash<TAB>iso<TAB>author<TAB>subject' lines."""
    out = []
    for line in (text or "").splitlines():
        p = line.split("\t", 3)
        if len(p) == 4:
            out.append({"time": p[1][:19], "kind": "commit", "text": p[3], "repo": repo, "who": p[2], "ref": p[0][:9]})
    return out


def timeline(commits=None, transcripts=None, deploys=None, finished=None, bot_runs=None, limit=60):
    """Newest first. transcripts and deploys: file names; finished: the status file's golem.finished; bot_runs: run
    folder names (yyyy-mm-dd-hhmm)."""
    ev = list(commits or [])
    for name in transcripts or []:
        p = parse_transcript_name(name)
        if p:
            ev.append({"time": p[0], "kind": kind_of_action(p[1]), "text": p[1] + (" (" + p[2] + ")" if p[2] else ""),
                       "who": "Shard Console", "log": "actions:" + name})
    for name in deploys or []:
        p = parse_transcript_name(name)
        if p:
            ev.append({"time": p[0], "kind": "deploy", "text": p[1] + " (Haven)", "who": "Shard Console", "log": "deploys:" + name})
    for f in finished or []:
        if f.get("time"):
            ev.append({"time": f["time"][:19], "kind": "prompt", "text": "P%s %s: %s" % (f.get("number"), f.get("result"), f.get("title")),
                       "who": "Golem", "headline": f.get("headline") or ""})
    for run in bot_runs or []:
        m = re.match(r"^(\d{4})-(\d{2})-(\d{2})-(\d{2})(\d{2})$", run or "")
        if m:
            ev.append({"time": "%s-%s-%sT%s:%s:00" % m.groups(), "kind": "bot", "text": "overnight bot run " + run, "who": "bot",
                       "log": "bot:" + run})
    ev.sort(key=lambda e: e.get("time") or "", reverse=True)
    return ev[:limit]


# --- links ----------------------------------------------------------------------------------------------------------

def links(status, extra=None, checks=None, repos=None):
    """[{key, name, url, up (True/False/None), why}]: the Shard Console's websites (their check from the status file),
    then the extra links (their check from the service's own checker), then the repos."""
    out, seen = [], set()
    for key, w in ((status or {}).get("websites") or {}).items():
        code = w.get("code")
        up = None if code is None and not w.get("error") and not w.get("timedOut") else (bool(code) and code < 500)
        out.append({"key": key, "name": w.get("name") or key, "url": w.get("url") or "", "up": up,
                    "why": ("HTTP %s" % code) if code else (w.get("error") or ("timed out" if w.get("timedOut") else ""))})
        seen.add(key)
    for e in extra or []:
        if e.get("key") in seen:
            continue
        c = (checks or {}).get(e.get("key")) or {}
        out.append({"key": e.get("key"), "name": e.get("name"), "url": e.get("url"), "up": c.get("up"), "why": c.get("why", "not checked yet")})
    for r in repos or []:
        out.append({"key": "repo-" + r["name"], "name": r["name"] + " (GitHub)", "url": r["url"], "up": None, "why": "not checked"})
    return out


# --- logs -----------------------------------------------------------------------------------------------------------

def tail(lines, n=200, query=""):
    """The last n lines (of those containing query, without case), each redacted."""
    n = max(1, min(int(n or 200), 2000))
    q = (query or "").strip().lower()
    pick = [l for l in lines if q in l.lower()] if q else list(lines)
    return [redact(l) for l in pick[-n:]]

# --- the status file's shape (schema 1, written by Write-ConsoleStatusFile in scripts/Shard-Console.ps1) -------------

TILE_KEYS = ("key", "title", "word", "detail", "color")


def status_problems(doc):
    """[] when doc is the status file's schema 1 as both consoles read it; otherwise what is wrong."""
    p = []
    if not isinstance(doc, dict):
        return ["not an object"]
    if doc.get("schema") != 1:
        p.append("schema is not 1")
    if not isinstance(doc.get("generatedAt"), str) or len(doc.get("generatedAt", "")) < 19:
        p.append("generatedAt is not a time")
    if doc.get("generatedBy") not in ("console", "collector"):
        p.append("generatedBy is neither console nor collector")
    tiles = doc.get("tiles")
    if not isinstance(tiles, list) or not tiles:
        p.append("no tiles")
    else:
        for t in tiles:
            if not isinstance(t, dict) or any(k not in t for k in TILE_KEYS):
                p.append("a tile lacks one of " + ", ".join(TILE_KEYS))
                break
    lights = doc.get("lights")
    if not isinstance(lights, dict) or "rebuild" not in lights or "package" not in lights:
        p.append("lights lacks rebuild or package")
    g = doc.get("golem")
    if g is not None:
        if not isinstance(g, dict) or not isinstance(g.get("state"), str):
            p.append("golem has no state")
        else:
            for k in ("queue", "finished", "usage"):
                if not isinstance(g.get(k), list):
                    p.append("golem." + k + " is not a list")
            for q in g.get("queue") or []:
                if not all(k in q for k in ("number", "title", "summary", "model", "size")):
                    p.append("a queued prompt lacks number, title, summary, model or size")
                    break
    if not isinstance(doc.get("websites"), dict):
        p.append("websites is not an object")
    return p
