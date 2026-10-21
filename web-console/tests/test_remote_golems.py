"""cc-P186: every Golem on the one web console, and the remote Golems' agents. Run from the repo root:

    python -m unittest discover -s web-console/tests

A remote Golem's PC opens no port: its agent (D:/UO/golem/Remote-Agent.ps1) pulls its commands from here through the
uoconsole sidecar, with a per-Golem secret stored hashed on the game PC. These tests use the real handlers on loopback
ports (the sidecar's proof set by hand, as in test_actions.py) and a fake remote that speaks the agent's endpoints:
the queue, the secret (wrong or missing refused), the expiry, "queued until <name> is online", the results on the
Timeline with the Golem's name, the cards with their readiness, a local second Golem through the console's runner."""
import json
import os
import shutil
import socket
import sys
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.dirname(HERE)
FIX = os.path.join(HERE, "fixtures")
sys.path.insert(0, WEB)
import panels  # noqa: E402
import server  # noqa: E402

PROOF = "p186-test-proof-0123456789abcdef0123456789abcdef"
CHASE = {"Tailscale-User-Login": "chase@example.com", "X-UOConsole-Proof": PROOF}
REGISTRY = {"schema": 1, "golems": [
    {"name": "Iron", "machine": "GAMEPC", "engine": "claude", "lane": "Opus", "account": "", "areas": ["*"],
     "work": {"code": "D:\\ShatteredLegacy", "docs": "D:\\UO\\shard-migration"}, "enabled": True, "integrator": True, "hours": ""},
    {"name": "Bronze", "machine": "GAMEPC", "engine": "claude", "lane": "Fable", "account": "", "areas": ["*"],
     "work": {"code": "D:\\UO\\golems\\Bronze\\ShatteredLegacy", "docs": "D:\\UO\\golems\\Bronze\\shard-migration"}, "enabled": True, "integrator": False, "hours": ""},
    {"name": "Gold", "machine": "STEPHS-DESKTOP", "engine": "claude", "lane": "any", "account": "", "areas": ["*"],
     "work": {"code": "D:\\ShatteredLegacy", "docs": "D:\\UO\\shard-migration"}, "enabled": True, "integrator": False, "hours": "", "portrait": ""},
    {"name": "Silver", "machine": "SILVER-LAPTOP", "engine": "claude", "lane": "any", "account": "", "areas": ["*"],
     "work": {"code": "D:\\ShatteredLegacy", "docs": "D:\\UO\\shard-migration"}, "enabled": False, "integrator": False, "hours": ""},
]}
READY = {"golem": "Gold", "machine": "STEPHS-DESKTOP", "at": "2026-10-19T15:00:00", "verdict": "NOT READY",
         "summary": "2 of 9 checks failed: docker, push", "checks": [
             {"name": "claude", "ok": True, "detail": "signed in, Max plan", "fix": ""},
             {"name": "docker", "ok": False, "detail": "Docker Desktop is not running", "fix": "start Docker Desktop"},
             {"name": "push", "ok": False, "detail": "push to golem/Gold/test refused", "fix": "add the deploy key"}],
         "laptop": None}


def free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    p = s.getsockname()[1]
    s.close()
    return p


def cards():
    return [{"name": g["name"], "machine": g["machine"], "engine": g["engine"], "lane": g["lane"], "state": "UNKNOWN" if g["machine"] != "GAMEPC" else "IDLE",
             "integrator": g["integrator"], "enabled": g["enabled"], "current": "", "branch": "", "locks": []} for g in REGISTRY["golems"]]


class RemoteGolems(unittest.TestCase):

    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p186-web-")
        self.data = os.path.join(self.tmp, "data")
        self.groot = os.path.join(self.tmp, "golem")
        self.repo = os.path.join(self.tmp, "repo")
        for d in (self.data, self.groot, os.path.join(self.repo, "scripts", "console-art", "golems")):
            os.makedirs(d)
        with open(os.path.join(self.data, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"], "agentCommandMinutes": 30}, f)
        with open(os.path.join(self.data, "proof.key"), "w", encoding="ascii") as f:
            f.write(PROOF + "\n")
        reg = json.loads(json.dumps(REGISTRY))
        self.portrait = os.path.join(self.repo, "scripts", "console-art", "golems", "gold.png")
        with open(self.portrait, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\nfake")
        reg["golems"][2]["portrait"] = self.portrait
        with open(os.path.join(self.groot, "golems.json"), "w", encoding="utf-8") as f:
            json.dump(reg, f)
        with open(os.path.join(FIX, "console-status.json"), encoding="utf-8") as f:
            doc = json.load(f)
        doc["golem"]["multiGolem"] = True
        doc["golem"]["golems"] = cards()
        self.status = os.path.join(self.data, "console-status.json")
        with open(self.status, "w", encoding="utf-8") as f:
            json.dump(doc, f)
        self.record = os.path.join(self.tmp, "calls.jsonl")
        runner = [sys.executable, os.path.join(FIX, "fake_runner.py"), self.record]
        self.console = server.Console(self.repo, os.path.join(self.tmp, "notes"), self.data, self.status, collector=False, runner=runner,
                                      golem_root=self.groot, machine="GAMEPC")
        self.secret = self.console.mint_agent("Gold")
        self.local_port, self.tail_port = free_port(), free_port()
        self.servers = []
        for listener, port in ((panels.LOCAL, self.local_port), (panels.TAILNET, self.tail_port)):
            s = server.ThreadingHTTPServer(("127.0.0.1", port), server.make_handler(self.console, listener, os.path.join(self.data, "access.log")))
            threading.Thread(target=s.serve_forever, daemon=True).start()
            self.servers.append(s)

    def tearDown(self):
        for s in self.servers:
            s.shutdown()
            s.server_close()
        shutil.rmtree(self.tmp, ignore_errors=True)

    # --- small helpers ------------------------------------------------------------------------------------------------
    def req(self, path, body=None, headers=None, port=None, method=None):
        data = None if body is None else json.dumps(body).encode("utf-8")
        h = dict(headers or {})
        if data is not None:
            h.setdefault("Content-Type", "application/json")
        r = urllib.request.Request("http://127.0.0.1:%d%s" % (port or self.tail_port, path), data=data, headers=h,
                                   method=method or ("POST" if data is not None else "GET"))
        try:
            with urllib.request.urlopen(r, timeout=10) as x:
                return x.status, json.loads(x.read().decode("utf-8") or "null")
        except urllib.error.HTTPError as e:
            text = e.read().decode("utf-8")
            try:
                return e.code, json.loads(text)
            except ValueError:
                return e.code, text

    def agent(self, path, body=None, name="Gold", key=None, proof=True):
        """The fake remote: what Remote-Agent.ps1 sends, through the sidecar (which adds the proof, never a login)."""
        h = {"X-Golem-Name": name, "X-Golem-Key": self.secret if key is None else key}
        if proof:
            h["X-UOConsole-Proof"] = PROOF
        if key == "":
            del h["X-Golem-Key"]
        return self.req(path, body, h)

    def act(self, golem, key, typed, inputs=None):
        h = dict(CHASE, **{"X-UOConsole-Action": "1"})
        body = {"key": key, "typed": typed, "inputs": inputs or {}}
        if golem:
            body["golem"] = golem
        return self.req("/api/action", body, h)

    def card(self, name):
        code, j = self.req("/api/status", headers=CHASE)
        self.assertEqual(200, code, j)
        return next(c for c in j["status"]["golem"]["golems"] if c["name"] == name), j

    def files(self, name):
        d = os.path.join(self.groot, "remote", name, "commands")
        out = []
        for n in sorted(os.listdir(d)) if os.path.isdir(d) else []:
            with open(os.path.join(d, n), encoding="utf-8") as f:
                out.append(json.load(f))
        return out

    def wait_idle(self):
        end = time.time() + 15
        while time.time() < end:
            j = self.console.job_view(0)
            if not j or not j.get("running"):
                return j
            time.sleep(0.05)
        self.fail("the action did not end")

    # --- cc-P159: the Codex lanes on another PC ------------------------------------------------------------------------
    def add_lane(self):
        """Verite: a Codex lane on the second PC, with its own agent secret and a card in the status file."""
        reg = json.loads(json.dumps(REGISTRY))
        reg["golems"][2]["portrait"] = self.portrait
        reg["golems"].append({"name": "Verite", "machine": "STEPHS-DESKTOP", "engine": "codex", "lane": "any", "account": "lane-steph",
                              "areas": ["docs-only"], "work": {"code": "D:\\UO\\golems\\Verite\\ShatteredLegacy", "docs": "D:\\UO\\golems\\Verite\\shard-migration"},
                              "enabled": True, "integrator": False, "hours": "23:00-06:30"})
        with open(os.path.join(self.groot, "golems.json"), "w", encoding="utf-8") as f:
            json.dump(reg, f)
        os.utime(os.path.join(self.groot, "golems.json"), (time.time() + 5, time.time() + 5))
        with open(self.status, encoding="utf-8") as f:
            doc = json.load(f)
        doc["golem"]["golems"].append({"name": "Verite", "machine": "STEPHS-DESKTOP", "engine": "codex", "lane": "any", "state": "UNKNOWN",
                                       "integrator": False, "enabled": True, "current": "", "branch": "", "locks": []})
        with open(self.status, "w", encoding="utf-8") as f:
            json.dump(doc, f)
        os.utime(self.status, (time.time() + 5, time.time() + 5))
        return self.console.mint_agent("Verite")

    def test_a_codex_lanes_card_says_its_line_and_send_to_the_second_pc_reaches_its_agent(self):
        sec = self.add_lane()
        v, _ = self.card("Verite")
        self.assertTrue(v["laneLine"].startswith("Second PC: asleep"), v.get("laneLine"))
        code, j = self.agent("/api/agent/status", {"status": {"state": "RUNNING", "current": "P151", "stepsDone": 2, "stepsTotal": 7, "lanePaused": False,
                                                              "note": "Codex works on P151", "engine": "codex", "generatedAt": "2026-10-21T23:30:00"}}, name="Verite", key=sec)
        self.assertEqual(200, code, j)
        v, _ = self.card("Verite")
        self.assertEqual("Second PC: P151 step 3 of 7", v["laneLine"])
        self.agent("/api/agent/status", {"status": {"state": "IDLE", "current": "P151", "stepsDone": 2, "stepsTotal": 7, "lanePaused": True,
                                                    "note": "backs off: owner active: steph signed in and using the PC", "engine": "codex"}}, name="Verite", key=sec)
        v, _ = self.card("Verite")
        self.assertEqual("Second PC: P151 paused at step 3 of 7, in use", v["laneLine"])
        g, _ = self.card("Gold")
        self.assertEqual("", g.get("laneLine", ""), "a Claude Golem has no lane line")
        # The button: on a Codex lane's card only, queued for its agent, and pulled once.
        b = next(x for x in self.console.public_buttons() if x["key"] == "lanenow")
        self.assertEqual("codex", b.get("engine"), "the page shows it on a Codex lane's card only")
        code, j = self.act("Gold", "lanenow", "send gold")
        self.assertEqual(409, code, j)
        self.assertIn("Codex lane", j["error"])
        self.assertEqual([], self.files("Gold"))
        code, j = self.act("Verite", "lanenow", "send verite")
        self.assertEqual(202, code, j)
        self.assertEqual(["lanenow"], [x["key"] for x in self.files("Verite")])
        code, j = self.agent("/api/agent/commands", name="Verite", key=sec)
        self.assertEqual(200, code, j)
        self.assertEqual(["lanenow"], [x["key"] for x in j["commands"]])

    def test_the_lane_line_follows_the_golems_own_rules(self):
        base = {"name": "Agapite", "engine": "codex", "enabled": True, "remote": True, "online": True, "lastSeen": "2026-10-21T23:10:00",
                "state": "RUNNING", "current": "P151", "stepsDone": 2, "stepsTotal": 7, "lanePaused": False, "note": "", "stoppedPrompt": ""}
        c = lambda **k: dict(base, **k)
        self.assertEqual("Second PC: P151 step 3 of 7", panels.lane_line(c()))
        self.assertEqual("Second PC: P151 running", panels.lane_line(c(stepsTotal=0, stepsDone=0)))
        self.assertEqual("Second PC: idle, in use", panels.lane_line(c(current="", state="IDLE", note="backs off: a game is running: RocketLeague")))
        self.assertEqual("Second PC: idle (the queue is empty)", panels.lane_line(c(current="", state="IDLE", note="the queue is empty")))
        self.assertEqual("Second PC: asleep (last seen Wed 23:10)", panels.lane_line(c(online=False)))
        self.assertEqual("Second PC: P151 needs you", panels.lane_line(c(stoppedPrompt="P151", state="STOPPED")))
        self.assertEqual("This PC: P151 step 3 of 7", panels.lane_line(c(remote=False)))
        self.assertEqual("", panels.lane_line(c(engine="claude")))
        self.assertEqual("Second PC: off (enabled false in golems.json)", panels.lane_line(c(enabled=False)))

    # --- cc-P222: the kit ----------------------------------------------------------------------------------------------
    def test_the_kit_manifest_and_only_its_files_are_served_to_an_agent(self):
        code, j = self.agent("/api/agent/kit")
        self.assertEqual(404, code, "no snapshot yet")
        snap = os.path.join(self.groot, "kit", "current")
        os.makedirs(os.path.join(snap, "golem"))
        with open(os.path.join(snap, "golem", "golem.ps1"), "wb") as f:
            f.write(b"# the hook")
        with open(os.path.join(snap, "golem", "settings.json"), "wb") as f:
            f.write(b"{}")
        man = {"schema": 1, "id": "abc123", "files": [{"path": "golem/golem.ps1", "sha256": "x", "size": 12}]}
        with open(os.path.join(snap, "kit-manifest.json"), "w", encoding="utf-8") as f:
            json.dump(man, f)
        code, j = self.agent("/api/agent/kit")
        self.assertEqual(200, code, j)
        self.assertEqual("abc123", j["id"])
        def raw(path, **kw):
            h = {"X-Golem-Name": "Gold", "X-Golem-Key": self.secret, "X-UOConsole-Proof": PROOF}
            h.update(kw)
            r = urllib.request.Request("http://127.0.0.1:%d%s" % (self.tail_port, path), headers=h)
            try:
                with urllib.request.urlopen(r, timeout=10) as x:
                    return x.status, x.read()
            except urllib.error.HTTPError as e:
                return e.code, e.read()
        self.assertEqual((200, b"# the hook"), raw("/api/agent/kit/file?path=golem/golem.ps1"))
        for bad in ("golem/settings.json", "golem/../golems.json", "golem%2F..%2Fsettings.json", "C:/Windows/win.ini", "golem/sub/x.ps1", ""):
            self.assertEqual(404, raw("/api/agent/kit/file?path=" + bad)[0], "not served: " + bad)
        self.assertEqual(403, raw("/api/agent/kit/file?path=golem/golem.ps1", **{"X-Golem-Key": "wrong"})[0], "an agent's secret is needed")

    # --- the secret ---------------------------------------------------------------------------------------------------
    def test_the_agents_secret_is_stored_hashed_and_a_wrong_or_missing_one_is_refused(self):
        code, j = self.agent("/api/agent/ping")
        self.assertEqual(200, code, j)
        self.assertEqual("Gold", j["golem"])
        self.assertTrue(j["ack"])
        for why, kw in (("a wrong secret", {"key": "not-the-secret"}), ("no secret", {"key": ""}),
                        ("another Golem's name with Gold's secret", {"name": "Silver"}),
                        ("a Golem that is not in the registry", {"name": "Agapite"}),
                        ("no sidecar proof", {"proof": False})):
            code, j = self.agent("/api/agent/ping", **kw)
            self.assertEqual(403, code, why)
            code, j = self.agent("/api/agent/commands", **kw)
            self.assertEqual(403, code, why)
        code, j = self.agent("/api/agent/ping", name="Bronze", key=self.console.mint_agent("Bronze"))
        self.assertEqual(403, code, "a Golem on this PC needs no agent: it runs through the console's runner")
        code, j = self.req("/api/agent/ping", headers={"X-Golem-Name": "Gold", "X-Golem-Key": self.secret}, port=self.local_port)
        self.assertEqual(403, code, "agents come through the sidecar only, never this PC's own listener")
        with open(os.path.join(self.data, "golem-agents.json"), encoding="utf-8") as f:
            stored = f.read()
        self.assertNotIn(self.secret, stored, "only a salted hash is kept")
        self.assertIn("sha256", json.loads(stored)["golems"]["Gold"])
        again = self.console.mint_agent("Gold")
        self.assertEqual(403, self.agent("/api/agent/ping")[0], "minting again replaces the old secret")
        self.assertEqual(200, self.agent("/api/agent/ping", key=again)[0])
        self.secret = again
        for log in ("access.log", "actions.log"):
            p = os.path.join(self.data, log)
            if os.path.exists(p):
                with open(p, encoding="utf-8") as f:
                    self.assertNotIn(again, f.read(), log)

    # --- the queue ----------------------------------------------------------------------------------------------------
    def test_a_command_waits_until_the_golem_is_online_then_its_agent_pulls_it_once(self):
        code, j = self.act("Gold", "wake", "Wake gold", {"model": "opus"})
        self.assertEqual(202, code, j)
        self.assertTrue(j["queued"])
        self.assertIn("queued until Gold is online", j["message"])
        c, _ = self.card("Gold")
        self.assertTrue(c["remote"])
        self.assertFalse(c["online"])
        self.assertIsNone(c["lastSeen"])
        self.assertEqual(1, len(c["commands"]))
        self.assertEqual("queued", c["commands"][0]["state"])
        self.assertIn("queued until Gold is online", c["commands"][0]["note"])
        self.assertTrue(c["commands"][0]["expiresAt"])
        code, j = self.agent("/api/agent/commands")
        self.assertEqual(200, code, j)
        self.assertEqual(["wake"], [x["key"] for x in j["commands"]])
        self.assertEqual({"model": "Opus"}, j["commands"][0]["inputs"])
        self.assertNotIn("typed", j["commands"][0], "the word stays on the game PC")
        self.assertEqual([], self.agent("/api/agent/commands")[1]["commands"], "a command is handed out once")
        c, _ = self.card("Gold")
        self.assertTrue(c["online"])
        self.assertTrue(c["lastSeen"])
        self.assertEqual("taken", c["commands"][0]["state"])
        # Online now: the next one is sent, not queued.
        code, j = self.act("Gold", "rest", "rest gold")
        self.assertEqual(202, code, j)
        self.assertIn("sent to Gold", j["message"])
        self.assertEqual([], self.calls(), "nothing runs on the game PC for a remote Golem")

    def test_the_word_and_the_buttons_are_checked_for_a_remote_golem_too(self):
        code, j = self.act("Gold", "wake", "wake", {"model": "Opus"})
        self.assertEqual(400, code, j)
        code, j = self.act("Gold", "wake", "wake the golem", {"model": "Opus; rm -rf"})
        self.assertEqual(400, code, j)
        code, j = self.act("Gold", "longrun", "start long run", {"cap": "3", "until": "2026-10-20 06:00"})
        self.assertEqual(409, code, j)
        self.assertIn("integrator", j["error"])
        code, j = self.act("Agapite", "rest", "rest")
        self.assertEqual(404, code, j)
        code, j = self.act("Silver", "rest", "rest")
        self.assertEqual(409, code, "a disabled Golem takes no commands")
        code, j = self.act("Gold", "retry", "P140 gold")
        self.assertEqual(409, code, "nothing is STOPPED on Gold (as far as its last status says)")
        self.assertEqual([], self.files("Gold"))
        h = dict(CHASE)   # no X-UOConsole-Action: not from this page
        code, j = self.req("/api/action", {"key": "rest", "typed": "rest", "golem": "Gold"}, h)
        self.assertEqual(403, code, j)
        code, j = self.req("/api/action", {"key": "rest", "typed": "rest", "golem": "Gold"}, {"X-UOConsole-Action": "1"}, port=self.local_port)
        self.assertEqual(403, code, "this PC's own page stays read only")
        self.assertEqual([], self.files("Gold"))

    def test_a_command_expires_after_the_set_time_and_says_so(self):
        t0 = time.time()
        self.console.clock = lambda: t0
        code, j = self.act("Gold", "poke", "poke gold")
        self.assertEqual(202, code, j)
        self.assertIn("expires", j["message"])
        self.console.clock = lambda: t0 + 31 * 60
        self.assertEqual([], self.agent("/api/agent/commands")[1]["commands"], "an expired command is never handed out")
        rec = self.files("Gold")[0]
        self.assertEqual("expired", rec["state"])
        self.assertIn("Gold did not come online within 30 minutes", rec["result"])
        c, j = self.card("Gold")
        self.assertEqual("expired", c["commands"][0]["state"])
        self.assertTrue(c["commands"][0]["endedAt"])
        tl = j["panels"]["timeline"]
        self.assertTrue(any(e["kind"] == "web" and "Gold" in e["text"] and "expired" in e["text"] for e in tl), tl[:3])
        # The setting moves it.
        with open(os.path.join(self.data, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"], "agentCommandMinutes": 5}, f)
        self.console.clock = lambda: t0
        self.assertEqual(5, self.console.command_minutes())
        self.act("Gold", "rest", "rest gold")
        self.console.clock = lambda: t0 + 6 * 60
        self.assertEqual([], self.agent("/api/agent/commands")[1]["commands"])

    def test_the_result_comes_back_to_the_timeline_with_the_golems_name(self):
        self.act("Gold", "rest", "rest gold")
        cmd = self.agent("/api/agent/commands")[1]["commands"][0]
        code, j = self.agent("/api/agent/result", {"id": cmd["id"], "code": 0, "result": "The Gold Golem is resting.",
                                                   "lines": ["The Gold Golem is resting.", "token=abc123secretvalue"]})
        self.assertEqual(200, code, j)
        rec = self.files("Gold")[0]
        self.assertEqual("done", rec["state"])
        self.assertEqual(0, rec["code"])
        self.assertNotIn("abc123secretvalue", json.dumps(rec), "lines are redacted when kept")
        c, j = self.card("Gold")
        self.assertEqual("The Gold Golem is resting.", c["commands"][0]["result"])
        tl = j["panels"]["timeline"]
        hit = [e for e in tl if e["kind"] == "web" and "Gold" in e["text"] and "resting" in e["text"]]
        self.assertTrue(hit, tl[:4])
        self.assertEqual("Gold", hit[0]["golem"])
        with open(os.path.join(self.data, "actions.log"), encoding="utf-8") as f:
            last = json.loads(f.read().strip().splitlines()[-1])
        self.assertEqual("Gold", last["golem"])
        self.assertEqual("rest", last["key"])
        # Another Golem cannot answer for it, and an unknown id is refused.
        silver = self.console.mint_agent("Silver")
        code, j = self.agent("/api/agent/result", {"id": cmd["id"], "code": 0, "result": "x"}, name="Silver", key=silver)
        self.assertEqual(404, code, "Silver cannot answer for Gold's command")
        code, j = self.agent("/api/agent/result", {"id": "../../etc", "code": 0, "result": "x"})
        self.assertEqual(400, code, j)
        code, j = self.agent("/api/agent/result", {"id": cmd["id"], "code": 0, "result": "twice"})
        self.assertEqual(409, code, "a result is taken once")

    def test_cards_carry_readiness_status_and_portrait(self):
        code, j = self.agent("/api/agent/ready", READY)
        self.assertEqual(200, code, j)
        code, j = self.agent("/api/agent/status", {"status": {"state": "RUNNING", "current": "P190", "winding": "", "stoppedPrompt": "P140",
                                                              "generatedAt": "2026-10-19T15:01:00"}})
        self.assertEqual(200, code, j)
        with open(os.path.join(self.groot, "ready-Bronze.json"), "w", encoding="utf-8") as f:
            json.dump(dict(READY, golem="Bronze", machine="GAMEPC", verdict="READY", summary="9 of 9 checks passed", checks=[]), f)
        gold, j = self.card("Gold")
        self.assertEqual("NOT READY", gold["ready"]["verdict"])
        self.assertEqual("2026-10-19T15:00:00", gold["ready"]["at"])
        self.assertEqual(["docker", "push"], gold["ready"]["failed"])
        self.assertEqual("RUNNING", gold["state"], "the remote's own status")
        self.assertEqual("P140", gold["stoppedPrompt"])
        self.assertEqual("/portrait/Gold", gold["portraitUrl"])
        bronze, _ = self.card("Bronze")
        self.assertEqual("READY", bronze["ready"]["verdict"])
        self.assertFalse(bronze["remote"])
        self.assertTrue(bronze["online"])
        self.assertEqual("", bronze.get("portraitUrl", ""), "no portrait: nothing shown")
        silver, _ = self.card("Silver")
        self.assertIsNone(silver.get("ready"))
        self.assertFalse(silver["online"])
        code, body = self.raw("/portrait/Gold")
        self.assertEqual(200, code)
        self.assertTrue(body.startswith(b"\x89PNG"))
        self.assertEqual(404, self.raw("/portrait/Bronze")[0])
        # A portrait outside the console art is never served.
        reg = json.loads(json.dumps(REGISTRY))
        outside = os.path.join(self.tmp, "elsewhere.png")
        with open(outside, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\nx")
        reg["golems"][2]["portrait"] = outside
        with open(os.path.join(self.groot, "golems.json"), "w", encoding="utf-8") as f:
            json.dump(reg, f)
        os.utime(os.path.join(self.groot, "golems.json"), (time.time() + 5, time.time() + 5))
        self.assertEqual(404, self.raw("/portrait/Gold")[0])
        gold, _ = self.card("Gold")
        self.assertEqual("", gold["portraitUrl"])
        # Now retry is allowed with the stopped prompt's number.
        code, j = self.act("Gold", "retry", "p140 Gold")
        self.assertEqual(202, code, j)
        self.assertEqual({"prompt": "P140"}, self.agent("/api/agent/commands")[1]["commands"][0]["inputs"])
        code, j = self.agent("/api/agent/ready", dict(READY, golem="Silver"))
        self.assertEqual(400, code, "a readiness file for another Golem is refused")
        code, j = self.agent("/api/agent/ready", dict(READY, verdict="MAYBE"))
        self.assertEqual(400, code, j)

    def test_a_second_golem_on_this_pc_runs_through_the_consoles_runner_with_its_name(self):
        code, j = self.act("Bronze", "rest", "rest bronze")
        self.assertEqual(202, code, j)
        self.wait_idle()
        c = self.calls()
        self.assertEqual(1, len(c))
        self.assertEqual(["-Key", "rest", "-Typed", "rest bronze", "-Golem", "Bronze"], c[0])
        self.assertIn("Bronze", self.console.job_view(0)["label"])
        code, j = self.act("Iron", "poke", "poke")
        self.assertEqual(202, code, j)
        self.wait_idle()
        self.assertNotIn("-Golem", self.calls()[-1], "the integrator is the console's own Golem: no name passed")
        code, j = self.act("Bronze", "integrate", "merge branches")
        self.assertEqual(409, code, "Merge branches is the integrator's only")
        _, j = self.req("/api/status", headers=CHASE)
        self.assertTrue(any(e["kind"] == "web" and "Bronze" in e["text"] for e in j["panels"]["timeline"]))
        every = [b["key"] for b in j["actions"]["buttons"] if b.get("anyGolem")]
        for k in ("wake", "on", "poke", "rest", "wrapup", "wrapup-cancel", "stoppoint", "stoppoint-cancel", "runnext", "runnext-cancel", "retry"):
            self.assertIn(k, every)
        for k in ("longrun", "nightshift-on", "integrate", "away-on"):
            self.assertNotIn(k, every)

    def test_the_agent_posts_status_and_no_secret_reaches_a_page(self):
        self.agent("/api/agent/commands")
        code, body = self.raw("/api/status", CHASE)
        self.assertEqual(200, code)
        self.assertNotIn(self.secret.encode("ascii"), body)
        self.assertNotIn(b"sha256", body)
        code, j = self.agent("/api/agent/status", {"status": "not an object"})
        self.assertEqual(400, code)
        code, j = self.req("/api/agent/status", {"status": {}}, {"X-Golem-Name": "Gold", "X-Golem-Key": self.secret, "X-UOConsole-Proof": PROOF,
                                                                 "Content-Type": "text/plain"})
        self.assertEqual(415, code)

    # --- pure parts ---------------------------------------------------------------------------------------------------
    def test_the_readiness_summary_is_compact(self):
        s = panels.ready_summary(READY)
        self.assertEqual({"verdict": "NOT READY", "at": "2026-10-19T15:00:00", "summary": "2 of 9 checks failed: docker, push",
                          "machine": "STEPHS-DESKTOP", "failed": ["docker", "push"], "laptop": None}, s)
        self.assertIsNone(panels.ready_summary(None))
        self.assertIsNone(panels.ready_summary({"verdict": "?"}))

    # --- helpers that read the raw answer -----------------------------------------------------------------------------
    def raw(self, path, headers=None):
        r = urllib.request.Request("http://127.0.0.1:%d%s" % (self.tail_port, path), headers=headers or CHASE)
        try:
            with urllib.request.urlopen(r, timeout=10) as x:
                return x.status, x.read()
        except urllib.error.HTTPError as e:
            return e.code, e.read()

    def calls(self):
        if not os.path.exists(self.record):
            return []
        with open(self.record, encoding="utf-8") as f:
            return [json.loads(l) for l in f if l.strip()]


EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


@unittest.skipUnless(os.path.exists(EDGE), "Microsoft Edge is not installed here")
class PhoneTeam(RemoteGolems):
    """The Golems card on a 390 px phone, through the tailnet listener as the sidecar would pass it: every Golem with
    its machine and readiness, a portrait only where there is one, and a remote Golem's own button queued for its
    agent with "queued until Gold is online" on its card. Runs RemoteGolems' setUp, not its tests."""

    def test_every_golems_card_and_its_buttons_on_a_phone(self):
        import cdp
        os.makedirs(os.path.join(self.groot, "remote", "Gold"))
        with open(os.path.join(self.groot, "remote", "Gold", "ready.json"), "w", encoding="utf-8") as f:
            json.dump(READY, f)   # as the server keeps it, without the agent calling in (that would make Gold online)
        with open(os.path.join(self.groot, "ready-Bronze.json"), "w", encoding="utf-8") as f:
            json.dump(dict(READY, golem="Bronze", machine="GAMEPC", verdict="READY", summary="9 of 9 checks passed", checks=[]), f)
        with cdp.Browser(EDGE) as b:
            b.phone(390, 844)
            b.call("Network.enable")
            b.call("Network.setExtraHTTPHeaders", headers=CHASE)
            b.open("http://127.0.0.1:%d/#golem" % self.tail_port)
            self.assertTrue(b.wait_for("document.querySelectorAll('#golem-team li.golemcard').length === 4"), "a card per Golem")
            text = b.eval("document.getElementById('golem-team').innerText")
            for want in ("Gold (any, claude, STEPHS-DESKTOP)", "Ready check: NOT READY", "failed: docker, push", "its agent has not called in yet",
                         "Bronze (Fable, claude, GAMEPC)", "Ready check: READY"):
                self.assertIn(want, text)
            self.assertEqual(1, b.eval("document.querySelectorAll('#golem-team img.portrait').length"), "only Gold has a portrait")
            keys = b.eval("Array.prototype.map.call(document.querySelectorAll('#golem-team button[data-golem=Gold]'), function (x) { return x.getAttribute('data-key'); }).join(',')")
            for k in ("wake", "rest", "poke", "stoppoint", "runnext"):
                self.assertIn(k, keys.split(","), keys)
            self.assertNotIn("longrun", keys)
            self.assertEqual(0, b.eval("document.querySelectorAll('#golem-team button[data-golem=Iron]').length"), "the integrator's are the Controls card")
            self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391)
            b.eval("document.querySelector('#golem-team button[data-golem=Gold][data-key=rest]').click()")
            self.assertTrue(b.wait_for("!document.getElementById('sheet').hidden"), "the sheet opens")
            self.assertIn("Gold Golem", b.eval("document.getElementById('sheet-title').textContent"))
            b.eval("var i = document.getElementById('sheet-word'); i.value = 'Rest gold'; i.dispatchEvent(new Event('input'));")
            b.eval("document.getElementById('sheet-run').click()")
            self.assertTrue(b.wait_for("document.getElementById('job-result').textContent.indexOf('queued until Gold is online') >= 0"),
                            "the wait is said: " + str(b.eval("document.getElementById('sheet-msg').textContent + ' | ' + document.getElementById('job-result').textContent")))
            self.assertTrue(b.wait_for("document.getElementById('golem-team').innerText.indexOf('Rest: queued until Gold is online') >= 0"), "and on Gold's card")
        self.assertEqual(["rest"], [r["key"] for r in self.files("Gold")])
        self.assertEqual([], self.calls())


for _name in [n for n in dir(RemoteGolems) if n.startswith("test_")]:
    setattr(PhoneTeam, _name, None)   # its tests run once, in RemoteGolems


if __name__ == "__main__":
    unittest.main()
