"""cc-P155: the web console's panels, as pure functions over what the service read (testable from fixtures).

    decide(listener, headers, allowed, proof)
                                             who may look: the local listener (this PC) always; the tailnet listener only
                                             with the sidecar's proof (cc-P182) and Tailscale Serve's identity header
                                             naming an allowed login
    decide_agent(listener, headers, proof)   cc-P186: a remote Golem's agent: the tailnet listener and the sidecar's
                                             proof only (its own secret is checked by server.py's Console.agent_auth)
    ready_summary(doc)                       cc-P186: a Golem's readiness file (Test-GolemReady.ps1) for its card
    redact(text) / redact_tree(value)        tokens, keys and passwords blanked in anything sent to a page
    needs_you(status, playtests, decisions, chores, services)
                                             one list of what waits on Chase
    timeline(commits, transcripts, deploys, finished, bot_runs, web_actions)
                                             what happened, newest first
    links(status, extra, checks, repos)      every link with its up dot
    tail(lines, n, query)                    the last n lines (of those matching query), redacted
    parse_chores(text) / parse_transcript_name(name) / parse_git_log(text, repo)

Nothing here reads a file, runs a process or touches the network; server.py does, and hands the results in.
"""
import calendar
import hmac
import re
import time

PLAYTESTS_SHOWN = 10   # the first ten play-test items marked yours, then a count (85 were on 2026-10-17)
LOCAL = "local"
TAILNET = "tailnet"
IDENTITY_HEADER = "Tailscale-User-Login"
PROOF_HEADER = "X-UOConsole-Proof"   # cc-P182: set by the sidecar's proof forwarder (tailscale/proof_proxy.py) only
AGENT_NAME_HEADER = "X-Golem-Name"   # cc-P186: a remote Golem's agent names its Golem ...
AGENT_KEY_HEADER = "X-Golem-Key"     # ... and sends that Golem's secret (never logged, never on a page)


# --- who may look ---------------------------------------------------------------------------------------------------

def decide(listener, headers, allowed, proof=None):
    """(allowed, why). headers: a mapping (case-insensitive lookups are the caller's: http.server's message is).

    The local listener (127.0.0.1:8095) is this PC: anything on it is Chase at the game PC, as the PowerShell console
    is; it may look but never act (server.py). The tailnet listener (127.0.0.1:8096) is reached through the uoconsole
    sidecar: Tailscale Serve sets Tailscale-User-Login to the caller's login (and drops any the caller sent), and its
    proof forwarder sets X-UOConsole-Proof to the secret only the sidecar and this service hold (cc-P182, P155's
    Deviation 2: a container on this PC's Docker can reach 127.0.0.1:8096 too and forge the login header, but not the
    proof). No proof configured, or a missing or wrong one, refuses everything; then a tagged device or a request with
    no login gets no header and is refused, as is every login not in allowed (compared without case)."""
    if listener == LOCAL:
        return True, "local"
    if listener != TAILNET:
        return False, "unknown listener"
    if not proof:
        return False, "no sidecar proof configured"
    got = (headers.get(PROOF_HEADER) or "").strip()
    if not got or not hmac.compare_digest(got.encode("utf-8"), proof.encode("utf-8")):
        return False, "no sidecar proof"
    login = (headers.get(IDENTITY_HEADER) or "").strip()
    if not login:
        return False, "no Tailscale identity"
    ok = {a.strip().lower() for a in (allowed or []) if a and a.strip()}
    if login.lower() in ok:
        return True, "tailnet " + login
    return False, "login not allowed"


def decide_agent(listener, headers, proof):
    """(allowed, why) for a remote Golem's agent (cc-P186), before its secret is checked. Only the tailnet listener,
    only with the sidecar's proof: so only a caller that came through Tailscale Serve on the uoconsole node, which
    only tailnet devices reach (Funnel is off). No login is asked: a PC tagged tag:golem has none. The local listener
    never answers an agent (anything on this PC could reach it)."""
    if listener != TAILNET:
        return False, "agents come through the tailnet listener only"
    if not proof:
        return False, "no sidecar proof configured"
    got = (headers.get(PROOF_HEADER) or "").strip()
    if not got or not hmac.compare_digest(got.encode("utf-8"), proof.encode("utf-8")):
        return False, "no sidecar proof"
    return True, "tailnet agent"


def ready_summary(doc):
    """A readiness file ({golem, machine, at, verdict, summary, checks: [{name, ok, detail, fix}], laptop}, written by
    Test-GolemReady.ps1) as its card shows it: the verdict and time, the one line, the checks that failed by name, the
    laptop facts. None when there is no file or it is not one."""
    if not isinstance(doc, dict) or doc.get("verdict") not in ("READY", "NOT READY"):
        return None
    checks = doc.get("checks") if isinstance(doc.get("checks"), list) else []
    return {"verdict": doc["verdict"], "at": str(doc.get("at") or "")[:19], "summary": str(doc.get("summary") or "")[:200],
            "machine": str(doc.get("machine") or ""),
            "failed": [str(c.get("name") or "?")[:40] for c in checks if isinstance(c, dict) and not c.get("ok")],
            "laptop": doc.get("laptop") if isinstance(doc.get("laptop"), dict) else None}


# --- the glow behind a Golem's portrait (cc-P207) ---------------------------------------------------------------------

QUIET_MINUTES = 10   # RUNNING with nothing done for this long is "running but quiet"
STALL_MINUTES = 45   # golem.ps1's stall email (settings.json stallMinutes): past it the card needs you


def _epoch(text, utc):
    """A status time ('2026-10-20T20:52:00', no zone) as seconds since 1970: UTC when utc, else this PC's local time."""
    try:
        t = time.strptime(str(text)[:19], "%Y-%m-%dT%H:%M:%S")
    except (TypeError, ValueError):
        return None
    return calendar.timegm(t) if utc else time.mktime(t)


def glow_state(card, status=None, now=None, stall_minutes=STALL_MINUTES):
    """Which of the five glows a Golem's card shows: 'working', 'quiet', 'waiting', 'needs' or 'off'. card is its
    Get-GolemCards card (state, enabled, remote, online, needsYou); status its status file, for lastActivityUtc (the
    Activity hooks' last tool, as golem.ps1's stall check reads it) or else startedAt; now is time.time()."""
    if not isinstance(card, dict):
        return "off"
    state = str(card.get("state") or "").upper()
    if card.get("enabled") is False or state in ("OFF", "DISABLED", "UNKNOWN", "") or (card.get("remote") and not card.get("online")):
        return "off"
    if state in ("NEEDS YOU", "STOPPED") or card.get("needsYou"):
        return "needs"
    if state != "RUNNING":
        return "waiting"   # IDLE, RESTING, WAITING, PAUSED: resting, back-off, outside hours, a model, pace
    st = status if isinstance(status, dict) else {}
    last = _epoch(st.get("lastActivityUtc"), True) if st.get("lastActivityUtc") else None
    if last is None and st.get("startedAt"):
        last = _epoch(st.get("startedAt"), False)
    if last is None:
        return "working"   # nothing to judge by yet (golem.ps1 does not call that stalled either)
    quiet = ((time.time() if now is None else now) - last) / 60.0
    if quiet >= stall_minutes:
        return "needs"
    return "quiet" if quiet >= QUIET_MINUTES else "working"


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


def lane_line(card):
    """cc-P159: a Codex lane's line on its card, as Get-LaneLine in D:/UO/golem/Golems.ps1 says it: the step, paused,
    in use, asleep. '' for a Golem that is not a Codex lane."""
    c = card if isinstance(card, dict) else {}
    if str(c.get("engine") or "") != "codex":
        return ""
    remote = bool(c.get("remote"))
    where = "Second PC" if remote else "This PC"
    if c.get("enabled") is False:
        return where + ": off (enabled false in golems.json)"
    if remote and not c.get("online"):
        try:
            t = time.strptime(str(c.get("lastSeen") or "")[:19], "%Y-%m-%dT%H:%M:%S")
        except ValueError:
            t = None
        return where + ": asleep" + (" (last seen %s)" % time.strftime("%a %H:%M", t) if t else " (its agent has not called in yet)")
    cur, sp, note = str(c.get("current") or ""), str(c.get("stoppedPrompt") or ""), str(c.get("note") or "")
    in_use = bool(re.search(r"(?i)owner active|a game is running", note))
    if sp:
        return "%s: %s needs you" % (where, sp)
    if cur:
        try:
            tot, done = int(c.get("stepsTotal") or 0), int(c.get("stepsDone") or 0)
        except (TypeError, ValueError):
            tot, done = 0, 0
        step = "step %d of %d" % (min(done + 1, tot), tot) if tot > 0 else ""
        if c.get("lanePaused"):
            return where + ": " + cur + " paused" + (" at " + step if step else "") + (", in use" if in_use else "")
        return where + ": " + cur + " " + (step or "running")
    if in_use:
        return where + ": idle, in use"
    return where + ": idle" + (" (" + note + ")" if note else "")


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


def timeline(commits=None, transcripts=None, deploys=None, finished=None, bot_runs=None, web_actions=None, limit=60):
    """Newest first. transcripts and deploys: file names; finished: the status file's golem.finished; bot_runs: run
    folder names (yyyy-mm-dd-hhmm); web_actions: the web console's action log entries (cc-P182)."""
    ev = list(commits or [])
    for a in web_actions or []:
        if a.get("time"):
            # cc-P186: a press for another Golem names it (a remote one's queued, expired and returned results too).
            g = a.get("golem") or ""
            ev.append({"time": a["time"][:19], "kind": "web", "text": "%s%s (web console): %s" % (g + " Golem: " if g else "", a.get("label") or a.get("key"), a.get("result") or ""),
                       "who": a.get("who") or "", "golem": g})
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
            # cc-P184: the Golem cards (several Golems), optional; each names its Golem and its state.
            cards = g.get("golems")
            if cards is not None:
                if not isinstance(cards, list):
                    p.append("golem.golems is not a list")
                else:
                    for c in cards:
                        if not isinstance(c, dict) or not isinstance(c.get("name"), str) or not isinstance(c.get("state"), str):
                            p.append("a Golem card lacks name or state")
                            break
    if not isinstance(doc.get("websites"), dict):
        p.append("websites is not an object")
    return p
