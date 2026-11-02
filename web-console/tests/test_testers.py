"""cc-P773: the Testers page (web-console/playtesters.py and its routes in server.py). Run from the repo root:

    python -m unittest discover -s web-console/tests

A tester is added by account name (and refused twice or with a bad name), paused, resumed and removed; the shard's
reports show under their tester as finished tasks; Pull back takes an open task off (and refuses an answered one);
a broken file is reported on the page and never changed over; the page and its POSTs refuse anyone but Chase and
anything not from the page; and the page's view never says Pass or Fail."""
import json
import os
import shutil
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.dirname(HERE)
REPO = os.path.dirname(WEB)
sys.path.insert(0, WEB)
sys.path.insert(0, HERE)
import panels  # noqa: E402
import playtesters  # noqa: E402
import server  # noqa: E402
from test_actions import CHASE, PROOF, free_port  # noqa: E402

ACT = dict(CHASE, **{"X-UOConsole-Action": "1"})


def task(tid, account, status="open"):
    return {"id": tid, "account": account, "testId": "T-9" + tid[-2:], "title": "Title " + tid, "why": "w", "where": "x",
            "coords": "1, 2", "steps": ["a"], "lookFor": "b", "intro": "i", "status": status, "assignedAt": "2026-11-02T05:00:00Z"}


def report(account, tid, button, text=""):
    return json.dumps({"at": "2026-11-02T18:00:00Z", "account": account, "character": "Paige", "task": tid,
                       "test": "T-9" + tid[-2:] if tid else "", "button": button, "text": text, "facet": "Trammel",
                       "x": 1, "y": 2, "z": 0, "version": "2026.11.02.1"})


class Testers(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p773-testers-")
        self.folder = os.path.join(self.tmp, "playtest")
        with open(os.path.join(self.tmp, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"]}, f)
        with open(os.path.join(self.tmp, "proof.key"), "w", encoding="ascii") as f:
            f.write(PROOF + "\n")
        self.console = server.Console(REPO, self.tmp, self.tmp, os.path.join(self.tmp, "status.json"), collector=False,
                                      golem_root=os.path.join(self.tmp, "golem"))
        self.console.playtest = playtesters.PlayTestData(self.folder)
        self.port = free_port()
        self.srv = server.ThreadingHTTPServer(("127.0.0.1", self.port), server.make_handler(self.console, panels.TAILNET))
        threading.Thread(target=self.srv.serve_forever, daemon=True).start()

    def tearDown(self):
        self.srv.shutdown()
        self.srv.server_close()
        shutil.rmtree(self.tmp, ignore_errors=True)

    def req(self, path, body=None, headers=None):
        data = None if body is None else json.dumps(body).encode("utf-8")
        h = dict((ACT if data is not None else CHASE) if headers is None else headers)
        if data is not None:
            h.setdefault("Content-Type", "application/json")
        r = urllib.request.Request("http://127.0.0.1:%d%s" % (self.port, path), data=data, headers=h,
                                   method="POST" if data is not None else "GET")
        try:
            with urllib.request.urlopen(r, timeout=30) as x:
                raw, code, ctype = x.read(), x.status, x.headers.get("Content-Type") or ""
        except urllib.error.HTTPError as e:
            raw, code, ctype = e.read(), e.code, e.headers.get("Content-Type") or ""
        return code, (json.loads(raw.decode("utf-8")) if ctype.startswith("application/json") else raw)

    def write(self, name, text):
        os.makedirs(self.folder, exist_ok=True)
        with open(os.path.join(self.folder, name), "w", encoding="utf-8") as f:
            f.write(text)

    def read(self, name):
        with open(os.path.join(self.folder, name), "r", encoding="utf-8") as f:
            return json.load(f)

    def test_add_pause_remove(self):
        code, page = self.req("/testers")
        self.assertEqual(200, code)
        self.assertIn(b"Add tester", page)
        code, d = self.req("/api/testers")
        self.assertEqual((200, []), (code, d["testers"]))

        code, out = self.req("/api/testers/add", {"account": "chase", "name": "Chase"})
        self.assertEqual(200, code, out)
        self.assertEqual({"account": "chase", "name": "Chase", "paused": False},
                         {k: out["tester"][k] for k in ("account", "name", "paused")})
        self.assertEqual(400, self.req("/api/testers/add", {"account": "CHASE", "name": "again"})[0])   # any case
        self.assertEqual(400, self.req("/api/testers/add", {"account": "bad<name>", "name": ""})[0])
        self.assertEqual(400, self.req("/api/testers/add", {"account": "", "name": "x"})[0])
        code, out = self.req("/api/testers/add", {"account": "PaigeTest1", "name": ""})
        self.assertEqual("PaigeTest1", out["tester"]["name"])   # no display name: the account's

        self.assertEqual(200, self.req("/api/testers/pause", {"account": "paigetest1", "paused": True})[0])
        on_disk = self.read("testers.json")
        self.assertEqual(1, on_disk["version"])
        self.assertEqual([False, True], [t["paused"] for t in on_disk["testers"]])
        self.assertEqual(200, self.req("/api/testers/pause", {"account": "PaigeTest1", "paused": False})[0])
        self.assertFalse(self.read("testers.json")["testers"][1]["paused"])

        self.assertEqual(200, self.req("/api/testers/remove", {"account": "PaigeTest1"})[0])
        self.assertEqual(["chase"], [t["account"] for t in self.read("testers.json")["testers"]])
        self.assertEqual(400, self.req("/api/testers/remove", {"account": "nobody"})[0])
        self.assertFalse(os.path.exists(os.path.join(self.folder, ".lock")))

    def test_tasks_reports_and_pull_back(self):
        self.req("/api/testers/add", {"account": "chase", "name": "Chase"})
        self.write("tasks.json", json.dumps({"version": 1, "tasks": [task("G-0001", "chase"), task("G-0002", "chase"),
                                                                     task("G-0003", "chase"), task("G-0004", "dylan")]}))
        self.write("playtest-reports.jsonl", "\n".join([
            report("chase", "G-0001", "wrong", "The arrow pointed at the sea."),
            "this line is broken",
            report("Chase", "", "report", "A rabbit hums."),
            report("dylan", "G-0004", "worked"),
        ]) + "\n")
        code, d = self.req("/api/testers")
        t = d["testers"][0]
        self.assertEqual(["G-0002", "G-0003"], [k["id"] for k in t["open"]])
        self.assertEqual(["G-0001"], [k["id"] for k in t["finished"]])
        self.assertEqual("Something was wrong", t["finished"][0]["answer"]["words"])
        self.assertEqual("The arrow pointed at the sea.", t["finished"][0]["answer"]["text"])
        self.assertEqual(["report", "wrong"], [r["button"] for r in t["reports"]])   # newest first, his only
        self.assertEqual(1, d["otherReports"])

        code, out = self.req("/api/testers/pullback", {"id": "G-0002"})
        self.assertEqual(200, code, out)
        tasks = {k["id"]: k for k in self.read("tasks.json")["tasks"]}
        self.assertEqual("pulled", tasks["G-0002"]["status"])
        self.assertTrue(tasks["G-0002"]["pulledAt"].endswith("Z"))
        self.assertEqual(4, len(tasks))   # never deleted from
        self.assertEqual(400, self.req("/api/testers/pullback", {"id": "G-0002"})[0])   # pulled already
        self.assertEqual(400, self.req("/api/testers/pullback", {"id": "G-0001"})[0])   # answered
        self.assertEqual(400, self.req("/api/testers/pullback", {"id": "G-9999"})[0])
        d = self.req("/api/testers")[1]["testers"][0]
        self.assertEqual(["G-0003"], [k["id"] for k in d["open"]])
        self.assertEqual(["G-0002"], [k["id"] for k in d["pulled"]])

        # nothing on the page or in the files says pass or fail
        blob = json.dumps(self.req("/api/testers")[1]).lower() + json.dumps(self.read("tasks.json")).lower()
        self.assertNotIn('"pass', blob)
        self.assertNotIn('"fail', blob)

    def test_a_broken_file_is_reported_and_never_written_over(self):
        self.write("testers.json", "{ broken")
        code, d = self.req("/api/testers")
        self.assertEqual(200, code)
        self.assertIn("testers.json cannot be read", d["testersError"])
        code, out = self.req("/api/testers/add", {"account": "chase", "name": "Chase"})
        self.assertEqual(400, code)
        with open(os.path.join(self.folder, "testers.json"), encoding="utf-8") as f:
            self.assertEqual("{ broken", f.read())
        self.write("testers.json", json.dumps({"version": 1, "testers": [{"account": "chase", "name": "Chase"}]}))
        self.write("tasks.json", "[]")
        d = self.req("/api/testers")[1]
        self.assertIn("tasks.json has no tasks list", d["tasksError"])
        self.assertEqual(400, self.req("/api/testers/pullback", {"id": "G-0001"})[0])
        with open(os.path.join(self.folder, "tasks.json"), encoding="utf-8") as f:
            self.assertEqual("[]", f.read())

    def test_only_chase_and_only_from_the_page(self):
        self.assertEqual(403, self.req("/api/testers", headers={})[0])
        self.assertEqual(403, self.req("/api/testers", headers={"Tailscale-User-Login": "eve@example.com",
                                                               "X-UOConsole-Proof": PROOF})[0])
        for h in (dict(CHASE), dict(ACT, Origin="https://evil.example"), dict(ACT, **{"Sec-Fetch-Site": "cross-site"})):
            self.assertEqual(403, self.req("/api/testers/add", {"account": "eve", "name": "Eve"}, headers=h)[0])
        self.assertFalse(os.path.exists(os.path.join(self.folder, "testers.json")))

    def test_the_default_folder_is_the_test_worlds(self):
        old = os.environ.pop("SL_PLAYTEST_DIR", None)
        try:
            self.assertEqual(os.path.join("R", "server", "lib", "uo", "modernuo-test", "playtest"), playtesters.default_dir("R"))
            os.environ["SL_PLAYTEST_DIR"] = "elsewhere"
            self.assertEqual("elsewhere", playtesters.default_dir("R"))
        finally:
            os.environ.pop("SL_PLAYTEST_DIR", None)
            if old is not None:
                os.environ["SL_PLAYTEST_DIR"] = old


if __name__ == "__main__":
    unittest.main()
