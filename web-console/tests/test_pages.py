"""cc-P187: the web console's pages (every other Shard Console tab) and the Golem picker. Run from the repo root:

    python -m unittest discover -s web-console/tests

Each page button reaches the console's runner with its inputs (a fake runner records each call and its stdin); read
buttons run at a tap and are never given a word or the second confirm; a change button needs the look first (the
preview's token, once, for those inputs) and the console's phrase; a dangerous one (and anything on the LIVE shard)
also the second confirm, which alone lets its script's own 'Type yes' be answered; refused while another action runs;
PC only buttons are refused with their reason; the inputs are checked; the page lists come from the list script (a
fake), cached; the picker lists every Golem with why it cannot be picked; the prompt reader reads only what the status
names; and every console action has a web twin (console-actions.json), read from Shard-Console.ps1 itself."""
import json
import os
import re
import shutil
import sys
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.dirname(HERE)
REPO = os.path.dirname(WEB)
FIX = os.path.join(HERE, "fixtures")
sys.path.insert(0, WEB)
sys.path.insert(0, HERE)
import panels  # noqa: E402
import server  # noqa: E402
from test_actions import PROOF, CHASE, EDGE, free_port  # noqa: E402

ACT = dict(CHASE, **{"X-UOConsole-Action": "1"})
EXPECT = {"expect": {"package.publish": "publish player package", "fix.restart": "restart test", "commit.run": "commit shard work",
                     "snapshot.create": "", "live.restart": "restart live", "question.y": "delete on test"},
          "danger": ["package.publish", "commit.run", "question.y", "snapshot.delete", "live.restart"],
          "asks": {"snapshot.delete": "Delete the snapshot? It cannot be undone."},
          "refuse": ["web.logs"]}
# A valid value for every page input (console-actions.json fields).
VALUES = {"shard": "test", "snapshotName": "before-p187", "snapshot": "20261019-101500_before-p185", "commitMessage": "P187 by hand",
          "force": "yes", "packageNotes": "web console pages", "packageZip": "ShatteredLegacy-2026.10.18.1709.zip",
          "imageTag": "sl-modernuo:cc-p187", "commit": "7083632", "site": "website", "vdFile": "D:\\UO\\art-inbox\\golem.vd",
          "body": "400", "huesFile": "D:\\UO\\other\\hues.mul", "artId": "console-crystal-amber", "note": "make it squatter"}


def pages():
    with open(os.path.join(WEB, "console-actions.json"), encoding="utf-8") as f:
        return json.load(f)


def buttons():
    return [(b, p) for p in pages()["pages"] for b in p["buttons"]]


class Pages(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="p187-web-")
        with open(os.path.join(cls.tmp, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"]}, f)
        with open(os.path.join(cls.tmp, "proof.key"), "w", encoding="ascii") as f:
            f.write(PROOF + "\n")
        cls.status = os.path.join(cls.tmp, "console-status.json")
        shutil.copy(os.path.join(FIX, "console-status.json"), cls.status)
        cls.record = os.path.join(cls.tmp, "calls.jsonl")
        with open(cls.record + ".expect.json", "w", encoding="utf-8") as f:
            json.dump(EXPECT, f)
        cls.lists = os.path.join(cls.tmp, "lists.jsonl")
        # The Golem registry: the integrator, a local Fable lane, a disabled one, a remote one.
        cls.groot = os.path.join(cls.tmp, "golem")
        os.makedirs(cls.groot)
        reg = {"schema": 1, "golems": [
            {"name": "Iron", "machine": "GAMEPC", "engine": "claude", "lane": "Opus", "enabled": True, "integrator": True},
            {"name": "Bronze", "machine": "GAMEPC", "engine": "claude", "lane": "Fable", "enabled": True, "integrator": False},
            {"name": "Silver", "machine": "LAPTOP", "engine": "claude", "lane": "any", "enabled": False, "integrator": False},
            {"name": "Gold", "machine": "STEPHS-DESKTOP", "engine": "claude", "lane": "any", "enabled": True, "integrator": False}]}
        with open(os.path.join(cls.groot, "golems.json"), "w", encoding="utf-8") as f:
            json.dump(reg, f)
        with open(os.path.join(cls.groot, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"multiGolem": True}, f)
        # An art store with one picture in it (server.ART_STORE points here for these tests).
        cls.art = os.path.join(cls.tmp, "art")
        os.makedirs(os.path.join(cls.art, "review"))
        cls.png = os.path.join(cls.art, "review", "iron.preview.png")
        with open(cls.png, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\nfake")
        os.environ["P187_ART_PNG"] = cls.png
        cls.out_png = os.path.join(cls.tmp, "outside.png")   # an image, but not in the art store
        with open(cls.out_png, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\nout")
        os.environ["P187_OUT_PNG"] = cls.out_png
        cls.old_art = server.ART_STORE
        server.ART_STORE = cls.art
        cls.prompts = os.path.join(cls.tmp, "cc-prompts")
        os.makedirs(os.path.join(cls.prompts, "queue"))
        for n, t in (("cc-P151-guild-halls-survey.md", "# CC task P151: guild halls\n\nSurvey them."), ("cc-P998-not-named.md", "# CC task P998: not in the status")):
            with open(os.path.join(cls.prompts, "queue", n), "w", encoding="utf-8") as f:
                f.write(t)
        cls.old_prompts = server.PROMPTS_DIR
        server.PROMPTS_DIR = cls.prompts
        runner = [sys.executable, os.path.join(FIX, "fake_page_runner.py"), cls.record]
        cls.console = server.Console(REPO, os.path.join(cls.tmp, "notes"), cls.tmp, cls.status, collector=False, runner=runner,
                                     golem_root=cls.groot, machine="GAMEPC")
        cls.console.list_runner = [sys.executable, os.path.join(FIX, "fake_list.py"), cls.lists]
        cls.local_port, cls.tail_port = free_port(), free_port()
        cls.servers = []
        for listener, port in ((panels.LOCAL, cls.local_port), (panels.TAILNET, cls.tail_port)):
            s = server.ThreadingHTTPServer(("127.0.0.1", port), server.make_handler(cls.console, listener, os.path.join(cls.tmp, "access.log")))
            threading.Thread(target=s.serve_forever, daemon=True).start()
            cls.servers.append(s)

    @classmethod
    def tearDownClass(cls):
        for s in cls.servers:
            s.shutdown()
            s.server_close()
        server.ART_STORE = cls.old_art
        server.PROMPTS_DIR = cls.old_prompts
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def setUp(self):
        self.wait_idle()
        for p in (self.record, self.lists):
            if os.path.exists(p):
                os.remove(p)

    # --- helpers --------------------------------------------------------------------------------------------------
    def req(self, path, body=None, headers=None, port=None):
        data = None if body is None else json.dumps(body).encode("utf-8")
        h = dict(headers if headers is not None else CHASE)
        if data is not None:
            h.setdefault("Content-Type", "application/json")
        r = urllib.request.Request("http://127.0.0.1:%d%s" % (port or self.tail_port, path), data=data, headers=h, method="POST" if data is not None else "GET")
        try:
            with urllib.request.urlopen(r, timeout=30) as x:
                raw = x.read()
                return x.status, (json.loads(raw.decode("utf-8")) if x.headers.get("Content-Type", "").startswith("application/json") else raw)
        except urllib.error.HTTPError as e:
            raw = e.read()
            try:
                return e.code, json.loads(raw.decode("utf-8"))
            except ValueError:
                return e.code, raw

    def calls(self):
        if not os.path.exists(self.record):
            return []
        with open(self.record, encoding="utf-8") as f:
            return [json.loads(l) for l in f if l.strip()]

    def wait_idle(self, timeout=15):
        end = time.time() + timeout
        while time.time() < end:
            j = self.console.job_view(0)
            if not j or not j.get("running"):
                return j
            time.sleep(0.05)
        self.fail("the action did not end")

    def look(self, key, inputs):
        return self.req("/api/preview", {"key": key, "inputs": inputs}, ACT)

    def press(self, key, inputs, typed="", token=None, second=False):
        body = {"key": key, "inputs": inputs, "typed": typed, "second": second}
        if token:
            body["token"] = token
        return self.req("/api/action", body, ACT)

    @staticmethod
    def inputs_for(b):
        return {n: VALUES[n] for n in b.get("inputs") or []}

    # --- each button reaches the console's runner -----------------------------------------------------------------
    def test_every_page_button_calls_the_consoles_runner_with_its_inputs(self):
        for b, p in buttons():
            if b.get("pcOnly") or b["key"] in EXPECT["refuse"]:
                continue
            inputs = self.inputs_for(b)
            if b["kind"] == "read":
                code, j = self.press(b["key"], inputs)
            else:
                code, pv = self.look(b["key"], inputs)
                self.assertEqual(200, code, b["key"] + ": " + str(pv))
                self.assertNotIn("refused", pv, b["key"])
                code, j = self.press(b["key"], inputs, pv["expect"], pv["token"], second=pv["danger"])
            self.assertEqual(202, code, b["key"] + ": " + str(j))
            self.wait_idle()
        runs = [c["args"] for c in self.calls() if "-DryRun" not in c["args"]]
        got = {a[a.index("-Key") + 1]: a for a in runs}
        for b, p in buttons():
            if b.get("pcOnly") or b["key"] in EXPECT["refuse"]:
                self.assertNotIn(b["key"], got, "a PC only button runs nothing: " + b["key"])
                continue
            a = got[b["key"]]
            for n in b.get("inputs") or []:
                flag = server.PAGE_INPUT_RULES[n][1]
                self.assertEqual(VALUES[n], a[a.index(flag) + 1], "%s passes %s" % (b["key"], n))
            if b["kind"] == "read":
                self.assertNotIn("-Typed", a, "a read button is given no word: " + b["key"])
                self.assertNotIn("-Second", a, b["key"])
        # The job's label names the page; its result is the console's line.
        self.assertTrue(self.console.job_view(0)["label"].count(" > ") == 1)

    def test_read_buttons_run_at_a_tap_and_have_no_look_first(self):
        code, j = self.press("fix.diagnose", {})
        self.assertEqual(202, code, j)
        self.wait_idle()
        a = self.calls()[-1]["args"]
        self.assertEqual(["-Key", "fix.diagnose"], a, "nothing but the key: no word, no second confirm")
        self.assertEqual("fix.diagnose: done.", self.console.job_view(0)["result"])
        code, j = self.look("fix.diagnose", {})
        self.assertEqual(400, code, "a read button has nothing to look at first")
        for b, p in buttons():
            if b["kind"] == "read":
                self.assertFalse(b.get("answers") or b.get("answersBySite") or b.get("locks"), "a read button takes no lock and answers nothing: " + b["key"])

    # --- change: the look first and the console's phrase ---------------------------------------------------------
    def test_a_change_button_needs_the_look_first_and_the_consoles_phrase(self):
        code, j = self.press("fix.restart", {}, "restart test")
        self.assertEqual(400, code, "no token: the plan was not seen")
        self.assertIn("look first", j["error"])
        code, pv = self.look("fix.restart", {})
        self.assertEqual(200, code, pv)
        self.assertEqual("restart test", pv["expect"], "the phrase the console's plan asks")
        self.assertTrue(any("DRY RUN" in l["text"] for l in pv["lines"]), "the plan is shown")
        self.assertFalse(any("abc123secretvalue" in l["text"] for l in pv["lines"]), "redacted")
        self.assertNotIn("dangerText", pv, "a change is not dangerous")
        code, j = self.press("fix.restart", {}, "restart", pv["token"])
        self.assertEqual(400, code, "the wrong word")
        code, j = self.press("fix.restart", {}, " Restart Test ", pv["token"])
        self.assertEqual(202, code, "a typo does not use the token up; case ignored: %s" % j)
        self.wait_idle()
        self.assertEqual(["-Key", "fix.restart", "-Typed", "Restart Test"], self.calls()[-1]["args"])
        self.assertEqual("", self.calls()[-1]["stdin"], "nothing on stdin for a change")
        code, j = self.press("fix.restart", {}, "restart test", pv["token"])
        self.assertEqual(400, code, "a token is good once")
        # A token is for those inputs only, and expires.
        code, pv = self.look("snapshot.create", {"shard": "test", "snapshotName": "a"})
        code, j = self.press("snapshot.create", {"shard": "test", "snapshotName": "b"}, "", pv["token"])
        self.assertEqual(400, code, "other inputs than the plan shown")
        self.assertIn("inputs changed", j["error"])
        self.console.tokens[pv["token"]]["at"] -= server.TOKEN_SECONDS + 1
        code, j = self.press("snapshot.create", {"shard": "test", "snapshotName": "a"}, "", pv["token"])
        self.assertEqual(400, code, "an old look is refused")

    # --- dangerous: both confirms ---------------------------------------------------------------------------------
    def test_a_dangerous_button_refuses_without_both_confirms(self):
        zip_ = {"packageZip": VALUES["packageZip"]}
        code, pv = self.look("package.publish", zip_)
        self.assertEqual(200, code, pv)
        self.assertTrue(pv["danger"])
        self.assertIn(VALUES["packageZip"], pv["dangerText"], "the second screen says what happens, and to what")
        code, j = self.press("package.publish", zip_, "publish player package", pv["token"])
        self.assertEqual(400, code, "the phrase alone is not enough")
        self.assertIn("second confirm", j["error"])
        code, j = self.press("package.publish", zip_, "publish player package", None, second=True)
        self.assertEqual(400, code, "the second confirm alone is not enough either")
        self.assertEqual([], [c for c in self.calls() if "-DryRun" not in c["args"]], "nothing ran")
        code, j = self.press("package.publish", zip_, "publish player package", pv["token"], second=True)
        self.assertEqual(202, code, j)
        self.wait_idle()
        c = self.calls()[-1]
        self.assertIn("-Second", c["args"])
        self.assertEqual(["yes"], c["stdin"].splitlines(), "its own 'Type yes' is answered only after the second confirm")
        # Anything on the LIVE shard is dangerous, whatever its kind; a yes/no question is too.
        code, pv = self.look("snapshot.create", {"shard": "live", "snapshotName": "x"})
        self.assertTrue(pv["danger"], pv)
        self.assertEqual(server.ONLINE_UNKNOWN, pv["online"], "who is online: said, as far as the console can tell")
        code, j = self.press("snapshot.create", {"shard": "live", "snapshotName": "x"}, "", pv["token"])
        self.assertEqual(400, code, "live needs the second confirm")
        code, pv = self.look("snapshot.delete", {"shard": "test", "snapshot": VALUES["snapshot"]})
        self.assertTrue(pv["danger"] and pv["asks"], pv)

    def test_a_dangerous_button_waits_for_another_action_and_keeps_its_token(self):
        hold = self.record + ".sleep"
        open(hold, "w").close()
        try:
            self.assertEqual(202, self.press("fix.diagnose", {})[0])
            code, pv = self.look("commit.run", {"commitMessage": "x"})
            self.assertEqual(200, code, "a look first while one runs is fine: it changes nothing")
            code, j = self.press("commit.run", {"commitMessage": "x"}, "commit shard work", pv["token"], second=True)
            self.assertEqual(409, code, j)
            self.assertIn("busy", j["error"])
        finally:
            os.remove(hold)
        self.wait_idle()
        code, j = self.press("commit.run", {"commitMessage": "x"}, "commit shard work", pv["token"], second=True)
        self.assertEqual(202, code, "the token was kept: %s" % j)
        self.wait_idle()

    # --- PC only, the inputs, the local page ----------------------------------------------------------------------
    def test_pc_only_buttons_are_refused_with_the_reason(self):
        pc = [(b, p) for b, p in buttons() if b.get("pcOnly")]
        self.assertTrue(len(pc) >= 10, "the wipe, the keys, the clients, ...")
        for b, p in pc:
            code, j = self.press(b["key"], {})
            self.assertEqual(409, code, b["key"])
            self.assertEqual("PC only: " + b["pcOnly"], j["error"])
            self.assertTrue(len(b["pcOnly"]) > 20, "a reason, in words: " + b["key"])
        self.assertEqual([], self.calls())

    def test_page_inputs_are_checked(self):
        for key, inputs in (("commit.preview", {"commitMessage": "-Second"}), ("commit.preview", {"commitMessage": 'a "quote"'}),
                            ("snapshot.restore", {"shard": "test", "snapshot": "..\\..\\Saves"}), ("web.restart", {"site": "Website"}),
                            ("snapshot.list", {"shard": "both"}), ("art.checkvd", {"vdFile": "\\\\server\\share\\x.vd"}),
                            ("package.check", {"packageZip": "../x.zip"}), ("test.deploy", {"imageTag": "evil:latest"})):
            code, j = self.press(key, inputs)
            self.assertEqual(400, code, "%s %r" % (key, inputs))
            self.assertIn("not allowed", j["error"])
        self.assertEqual([], self.calls())

    def test_this_pcs_own_page_cannot_act_and_other_sites_cannot_post(self):
        code, j = self.req("/api/preview", {"key": "fix.restart", "inputs": {}}, dict(ACT), port=self.local_port)
        self.assertEqual(403, code)
        code, j = self.req("/api/preview", {"key": "fix.restart", "inputs": {}}, dict(CHASE))
        self.assertEqual(403, code, "no X-UOConsole-Action: not from this page")
        code, j = self.req("/api/action", {"key": "fix.diagnose"}, dict(CHASE, **{"X-UOConsole-Action": "1", "Origin": "https://evil.example"}))
        self.assertEqual(403, code)
        self.assertEqual([], self.calls())

    def test_the_action_log_and_timeline_hold_page_presses_and_never_the_word(self):
        code, pv = self.look("fix.restart", {})
        self.press("fix.restart", {}, "Restart TEST", pv["token"])
        self.wait_idle()
        with open(os.path.join(self.tmp, "actions.log"), encoding="utf-8") as f:
            text = f.read()
        self.assertIn('"key": "fix.restart"', text)
        self.assertNotIn("Restart TEST", text, "never the typed word")
        self.assertNotIn(pv["token"], text, "never the token")
        code, j = self.req("/api/status")
        self.assertTrue(any(e["kind"] == "web" and "Fix test shard" in e["text"] for e in j["panels"]["timeline"]), j["panels"]["timeline"][:3])

    # --- lists, the art store, the prompt reader ------------------------------------------------------------------
    def test_page_lists_come_from_the_list_script_and_are_cached(self):
        code, j = self.req("/api/page?id=package")
        self.assertEqual(200, code, j)
        self.assertEqual("ShatteredLegacy-2026.10.18.1709.zip", j["zips"][0]["value"])
        self.assertNotIn("abc123secretvalue", json.dumps(j), "redacted")
        self.req("/api/page?id=package")
        with open(self.lists, encoding="utf-8") as f:
            self.assertEqual(1, len(f.readlines()), "read once in 20 s")
        self.assertEqual(404, self.req("/api/page?id=..")[0])
        self.assertEqual(404, self.req("/api/page?id=golem")[0], "only the pages with lists")
        code, j = self.req("/api/page?id=art")
        self.assertFalse(any("thumb" in r for r in j["requests"]), "no path on the page")
        self.assertNotIn("outside.png", json.dumps(j))
        self.assertNotIn("iron.preview.png", json.dumps(j))
        bad, good = j["requests"]
        self.assertEqual("", bad["picture"], "a picture outside the art store is never offered")
        self.assertEqual("/art-thumb/golem-iron", good["picture"])
        self.assertEqual(200, self.req(good["picture"])[0])
        self.assertEqual(404, self.req("/art-thumb/console-crystal-amber")[0], "and never served")
        self.assertEqual("", self.console.art_picture(self.out_png), "an image outside the art store is not a picture")
        self.assertEqual(404, self.req("/art-thumb/..%5C..%5Cwin.ini")[0])

    def test_the_prompt_reader_reads_only_what_the_status_names(self):
        code, j = self.req("/api/prompt?name=cc-P151-guild-halls-survey.md")
        self.assertEqual(200, code, j)
        self.assertIn("Survey them.", j["text"])
        self.assertEqual(404, self.req("/api/prompt?name=cc-P998-not-named.md")[0], "on disk, but not in the status")
        self.assertEqual(404, self.req("/api/prompt?name=cc-P999-nothing.md")[0], "not in the status")
        self.assertEqual(404, self.req("/api/prompt?name=..%5C..%5Cgolem%5Csettings.json")[0])
        self.assertEqual(404, self.req("/api/prompt?notes=999")[0])

    # --- the Golem picker -----------------------------------------------------------------------------------------
    def test_the_status_carries_the_pages_and_the_golem_picker(self):
        code, j = self.req("/api/status")
        self.assertEqual(13, len(j["pages"]["pages"]), "one page per Shard Console tab")
        self.assertEqual(["Test shard", "Fix test shard", "Shard question", "Snapshots", "World setup", "Map and Art", "Diagnostics", "Commit",
                          "Player package", "Golem", "Websites", "Art", "LIVE shard"], [p["tab"] for p in j["pages"]["pages"]])
        names = [g["name"] for g in j["golemPicker"]]
        self.assertEqual("Iron", names[0], "the integrator first")
        by = {g["name"]: g for g in j["golemPicker"]}
        self.assertTrue(by["Iron"]["usable"] and by["Bronze"]["usable"])
        self.assertEqual("Fable", by["Bronze"]["lane"])
        self.assertFalse(by["Silver"]["usable"])
        self.assertIn("disabled", by["Silver"]["why"])
        self.assertTrue(by["Gold"]["usable"] and by["Gold"]["remote"], "a remote Golem may be picked")
        self.assertIn("offline", by["Gold"]["grey"], "but shows it is offline")
        with open(os.path.join(self.groot, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"multiGolem": False}, f)
        try:
            by = {g["name"]: g for g in self.req("/api/status")[1]["golemPicker"]}
            self.assertTrue(by["Iron"]["usable"])
            self.assertFalse(by["Bronze"]["usable"])
            self.assertIn("multiGolem is off", by["Bronze"]["why"])
        finally:
            with open(os.path.join(self.groot, "settings.json"), "w", encoding="utf-8") as f:
                json.dump({"multiGolem": True}, f)

    def test_shadow_iron_is_not_in_the_picker_and_iron_stays_the_consoles_own(self):
        """cc-P208: with Shadow Iron (engine script) the integrator, the picker leaves it out, Iron is still the console's
        own (home), a Golem button named for Shadow Iron is refused, and its card buttons are listed with card 'merge'."""
        path = os.path.join(self.groot, "golems.json")
        with open(path, encoding="utf-8") as f:
            before = f.read()
        doc = json.loads(before)
        for g in doc["golems"]:
            g["integrator"] = False
        doc["golems"].append({"name": "ShadowIron", "machine": self.console.machine, "engine": "script", "lane": "", "areas": [],
                              "work": {"code": "D:\\ShatteredLegacy", "docs": "D:\\UO\\shard-migration"}, "enabled": True, "integrator": True})
        try:
            with open(path, "w", encoding="utf-8") as f:
                json.dump(doc, f)
            st = os.stat(path)
            os.utime(path, (st.st_atime, st.st_mtime + 5))
            self.assertEqual("Iron", self.console.home_name())
            picker = self.req("/api/status")[1]["golemPicker"]
            names = [g["name"] for g in picker]
            self.assertNotIn("ShadowIron", names)
            self.assertEqual("Iron", names[0], "Iron, the console's own, first")
            self.assertTrue(picker[0]["integrator"], "own")
            code, j = self.req("/api/action", {"key": "poke", "typed": "poke shadowiron", "golem": "ShadowIron"}, ACT)
            self.assertIn(code, (403, 409), j)
            cards = {b["key"]: b for b in self.console.public_buttons()}
            self.assertEqual("merge", cards["merge-pause"]["card"])
            self.assertEqual("mergePaused:off", cards["merge-pause"]["when"])
            self.assertEqual("Merge now", cards["integrate"]["label"])
        finally:
            with open(path, "w", encoding="utf-8") as f:
                f.write(before)
            st = os.stat(path)
            os.utime(path, (st.st_atime, st.st_mtime + 10))

    def test_the_word_names_the_picked_golem_and_assign_names_both(self):
        g = {b["key"]: b for b in self.console.buttons()}
        err, ok = self.console.check_request(g["wake"], "wake the golem", {"model": "Fable"}, "Bronze")
        self.assertEqual(400, err[0], "the integrator's word is not enough for Bronze")
        self.assertIn("wake bronze", err[1])
        err, ok = self.console.check_request(g["wake"], "Wake Bronze", {"model": "Fable"}, "Bronze")
        self.assertIsNone(err, err)
        err, ok = self.console.check_request(g["runnext"], "P151 bronze", {"prompt": "P151"}, "Bronze")
        self.assertIsNone(err, err)
        err, ok = self.console.check_request(g["assign"], "P151 on bronze", {"prompt": "P151", "assignTo": "Bronze", "next": "yes"})
        self.assertIsNone(err, err)
        self.assertEqual(["-Key", "assign", "-Typed", "P151 on bronze", "-Prompt", "P151", "-AssignTo", "Bronze", "-Next", "yes"], ok[0])
        err, ok = self.console.check_request(g["assign"], "P151 on gold", {"prompt": "P151", "assignTo": "Bronze"})
        self.assertEqual(400, err[0], "the word names the Golem it goes to")


class Parity(unittest.TestCase):
    """Every Shard Console action has a web twin: a page button, a Golem button, or a twins entry (PC only with its
    reason). The window's every button is checked by the console fact P187_EveryConsoleButtonHasAWebTwin."""

    def test_every_console_action_has_a_web_twin(self):
        with open(os.path.join(REPO, "scripts", "Shard-Console.ps1"), encoding="utf-8") as f:
            text = f.read()
        body = text[text.index("function Get-ConsoleActions {"):]
        body = body[:body.index("\n}\n")]
        keys = re.findall(r"Key = '([A-Za-z.-]+)'", body)
        self.assertGreater(len(keys), 80, "the console's action list was read")
        doc = pages()
        page_keys = {b["action"] for b, p in buttons() if b.get("action")}
        with open(os.path.join(WEB, "golem-actions.json"), encoding="utf-8") as f:
            golem_keys = {b["action"] for b in json.load(f)["actions"] if b.get("action")}
        twins = doc["twins"]
        web_keys = {b["key"] for b, p in buttons()}
        with open(os.path.join(WEB, "golem-actions.json"), encoding="utf-8") as f:
            web_keys |= {b["key"] for b in json.load(f)["actions"]}
        missing = [k for k in keys if k not in page_keys and k not in golem_keys and k not in twins]
        self.assertEqual([], missing, "console actions with no web twin")
        for k, v in twins.items():
            target = v.split(" ", 1)[0]
            if target.startswith(("page:", "input:", "logs:")):
                continue
            if target.startswith("pc:"):
                self.assertGreater(len(v), 20, "a PC only twin says why: " + k)
                continue
            self.assertIn(target, web_keys, "the twin of %s is a web button: %s" % (k, v))
        for b, p in buttons():
            self.assertIn(b["kind"], ("read", "change", "dangerous"), b["key"])
            if b.get("action"):
                self.assertIn(b["action"], keys, "a page button runs a console action: " + b["key"])
            for n in b.get("inputs") or []:
                self.assertIn(n, server.PAGE_INPUT_RULES, b["key"] + " " + n)
                self.assertIn(n, doc["fields"], b["key"] + " " + n)
        dangerous = {b["key"] for b, p in buttons() if b["kind"] == "dangerous"}
        for k in ("commit.run", "package.publish", "snapshot.delete", "live.start", "live.stop", "live.restart", "web.deploy", "package.scripts.dev"):
            self.assertIn(k, dangerous, "dangerous: " + k)


class PhonePages(Pages):
    """The pages on a phone (390 px, headless Edge): the Console tab, a list to tap, the look first, the phrase, the
    second confirm, the result; the Golem picker with Wake's model from the lane and the word naming the Golem."""

    @unittest.skipUnless(EDGE, "Microsoft Edge is not installed here")
    def test_the_notes_box_fills_itself_and_refill_puts_it_back(self):
        """cc-P288: the Player package page's Notes box starts with the suggested What is new line, an edit stays, Refill
        puts the line back, and the list it was made from is under the box."""
        import cdp
        line = "Rootdeep map, satchel and guild bag icons"
        with cdp.Browser(EDGE) as b:
            b.phone(390, 844)
            b.call("Network.enable")
            b.call("Network.setExtraHTTPHeaders", headers=CHASE)
            b.open("http://127.0.0.1:%d/#console" % self.tail_port)
            self.assertTrue(b.wait_for("document.querySelectorAll('#page-grid a').length === 13"), "a tile per page")
            b.eval("document.querySelector('#page-grid a[data-page=package]').click()")
            self.assertTrue(b.wait_for("!!document.getElementById('pg-packageNotes-refill')", 30), "Refill under the Notes box")
            self.assertEqual(line, b.eval("document.getElementById('pg-packageNotes').value"), "the box fills itself")
            text = b.eval("document.getElementById('page-body').innerText")
            self.assertIn("What changed for players since 2026.10.18.1122 (commit ac9eee3, found by its build time)", text)
            self.assertIn("- Rootdeep map (P188, map)", text, "one line per item")
            self.assertIn("staff only in CHANGELOG.md: P277", text, "and what was left out")
            b.eval("var i = document.getElementById('pg-packageNotes'); i.value = 'My own line'; i.dispatchEvent(new Event('input'));")
            b.eval("document.querySelector('#page-body button.row[data-value=\"ShatteredLegacy-2026.10.18.1709.zip\"]').click()")
            self.assertTrue(b.wait_for("!!document.querySelector('#page-body button.row.on')"), "the page draws again")
            self.assertEqual("My own line", b.eval("document.getElementById('pg-packageNotes').value"), "an edit is kept")
            b.eval("document.getElementById('pg-packageNotes-refill').click()")
            self.assertEqual(line, b.eval("document.getElementById('pg-packageNotes').value"), "Refill puts the line back")
            self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391, "nothing wider than the phone")

    @unittest.skipUnless(EDGE, "Microsoft Edge is not installed here")
    def test_publish_from_the_phone_through_both_confirms_and_pick_a_golem(self):
        import cdp
        with cdp.Browser(EDGE) as b:
            b.phone(390, 844)
            b.call("Network.enable")
            b.call("Network.setExtraHTTPHeaders", headers=CHASE)
            b.open("http://127.0.0.1:%d/#console" % self.tail_port)
            self.assertTrue(b.wait_for("document.querySelectorAll('#page-grid a').length === 13"), "a tile per page")
            b.eval("document.querySelector('#page-grid a[data-page=package]').click()")
            self.assertTrue(b.wait_for("document.querySelectorAll('#page-body .picklist button.row').length === 2", 30), "the zips, tappable")
            self.assertTrue(b.eval("document.getElementById('page-body').innerText.indexOf('PC only: ssh-keygen') >= 0"), "PC only buttons say why")
            b.eval("document.querySelector('#page-body button.row[data-value=\"ShatteredLegacy-2026.10.18.1709.zip\"]').click()")
            self.assertTrue(b.wait_for("!!document.querySelector('#page-body button.row.on')"), "picked")
            b.eval("document.querySelector('#page-body button[data-key=\"package.publish\"]').click()")
            self.assertTrue(b.wait_for("document.getElementById('sheet-want').textContent === 'publish player package'", 30), "the console's phrase, from its plan")
            self.assertIn("DRY RUN", b.eval("document.getElementById('sheet-plan').textContent"), "the plan first")
            self.assertTrue(b.eval("document.getElementById('sheet-run').disabled"), "dark until the phrase")
            b.eval("var i = document.getElementById('sheet-word'); i.value = 'Publish player package'; i.dispatchEvent(new Event('input'));")
            self.assertEqual("Next", b.eval("document.getElementById('sheet-run').textContent"), "a dangerous one goes on to the second confirm")
            b.eval("document.getElementById('sheet-run').click()")
            self.assertTrue(b.wait_for("!document.getElementById('sheet-second').hidden"), "the second confirm")
            self.assertIn("ShatteredLegacy-2026.10.18.1709.zip", b.eval("document.getElementById('second-text').textContent"))
            self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391, "nothing wider than the phone")
            b.eval("document.getElementById('second-yes').click()")
            self.assertTrue(b.wait_for("document.getElementById('job-result').textContent === 'package.publish: done.'", 30), "the result line")
            self.assertEqual(["yes"], self.calls()[-1]["stdin"].splitlines())
            # The Golem picker: Bronze, Wake's model from its lane, the word naming it.
            b.open("http://127.0.0.1:%d/#golem" % self.tail_port)
            self.assertTrue(b.wait_for("document.querySelectorAll('#picker button.pick').length === 4"), "every Golem")
            self.assertTrue(b.eval("document.querySelector('#picker button[data-golem=Silver]').disabled"), "a disabled one cannot be picked")
            b.eval("document.querySelector('#picker button[data-golem=Bronze]').click()")
            self.assertTrue(b.wait_for("document.getElementById('ctl-note').textContent.indexOf('Bronze') >= 0"))
            keys = b.eval("Array.prototype.map.call(document.querySelectorAll('#ctl-buttons button'), function (x) { return x.getAttribute('data-key'); }).join(',')")
            self.assertNotIn("longrun", keys.split(","), "only the buttons any Golem has")
            self.assertIn("assign", keys.split(","))
            b.eval("document.querySelector('#ctl-buttons button[data-key=wake]').click()")
            self.assertTrue(b.wait_for("!document.getElementById('sheet').hidden"))
            # cc-P153: Auto is the default: Bronze's own Golem picks its next prompt (its lane) and reads the Model line.
            self.assertEqual("Auto", b.eval("document.getElementById('in-model').value"), "Wake's model starts on Auto")
            self.assertEqual("Fable,Opus,Sonnet", b.eval("Array.prototype.slice.call(document.getElementById('in-model').options, 1).map(function (o) { return o.value; }).join(',')"), "the overrides")
            self.assertEqual("wake bronze", b.eval("document.getElementById('sheet-want').textContent"))
            self.assertEqual("no", b.eval("document.documentElement.getAttribute('data-overflow')"))


for _name in [n for n in dir(Pages) if n.startswith("test_")]:
    setattr(PhonePages, _name, None)   # its tests run once, in Pages


if __name__ == "__main__":
    unittest.main()
