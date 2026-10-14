"""cc-P112: uobot.tonight, the plan behind the play-test page's robot icons.

    python -m unittest discover -s tools\\playtest-bot\\tests -p "test_*.py"

Uses P102's fixture (a throwaway git repo, play-tests.json and bot-runs folder). The plan must be the runner's own work
check: the same items, the same estimate, nothing written to git.
"""
import json
import os
import sys
import time
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
from test_overnight import Fixture, git, item  # noqa: E402
from uobot import tonight, work  # noqa: E402

IDS = ["A-1", "A-2", "B-1"]
TRIAGE = """# triage
| Id | Bucket | Reason |
|---|---|---|
| A-1 | BOT | a message |
| A-2 | BOT-PARTIAL | half |
| C-7 | BOT | no check built yet |
| E-1 | EYES | looks |
| D-1 | DEPLOY | live |
| X-1 | DECISION | pick |
"""


class Tonight(Fixture):
    def setUp(self):
        super().setUp()
        self.triage = os.path.join(self.tmp, "bot-triage.md")
        with open(self.triage, "w", encoding="utf-8") as f:
            f.write(TRIAGE)

    def plan(self, commit="HEAD"):
        return tonight.plan(self.repo, self.plays, self.runs, self.triage, commit, check_ids=IDS)

    def test_tonight_is_exactly_the_runners_work_and_estimate(self):
        self.plays_with(item("A-1"), item("A-2"), item("B-1"), item("C-7"), item("E-1"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"])
        p = self.plan()
        w = work.find_work(self.repo, self.plays, self.runs, "HEAD", IDS)
        self.assertTrue(p["available"], p)
        self.assertEqual(sorted(i["id"] for i in w["items"]), sorted(p["tonight"]))
        self.assertEqual(["A-2", "B-1"], sorted(p["tonight"]))
        self.assertEqual(work.estimate_minutes(self.runs, IDS, 3, 120), p["minutes"])
        self.assertEqual(w["sha"], p["commit"])
        self.assertIn("no bot run yet", p["tonight"]["B-1"]["reason"])
        self.assertEqual(3, p["tonight"]["B-1"]["minutes"], "never run: 60 s a run, three runs")

    def test_checkable_is_triage_BOT_and_BOT_PARTIAL_plus_the_checks_minus_tonight(self):
        self.plays_with(item("A-1"), item("A-2"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1", "A-2"])
        p = self.plan()
        self.assertEqual({}, p["tonight"])
        self.assertEqual(0, p["minutes"], "no work: the runner builds nothing tonight")
        self.assertEqual(["A-1", "A-2", "C-7", "B-1"], p["checkable"])
        for none in ("E-1", "D-1", "X-1"):
            self.assertNotIn(none, p["checkable"], none + " is EYES, DEPLOY or DECISION: no icon")

    def test_last_is_each_items_newest_result_including_could_not_run(self):
        self.plays_with(item("A-1"), item("A-2"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1", "A-2"], result="PASS")
        self.bot_run("2026-10-14-0130", self.sha1, ["A-2"], result="ERROR")
        last = self.plan()["last"]
        self.assertEqual({"result": "PASS", "run": "2026-10-13-0130"}, {k: last["A-1"][k] for k in ("result", "run")})
        self.assertEqual("ERROR", last["A-2"]["result"])
        self.assertEqual("2026-10-14-0130", last["A-2"]["run"])

    def test_a_bad_commit_is_unavailable_not_an_exception(self):
        self.plays_with(item("A-1"))
        p = self.plan(commit="0000000000000000000000000000000000000000")
        self.assertFalse(p["available"])
        self.assertIn("not a commit", p["error"])
        p = tonight.plan(os.path.join(self.tmp, "no-repo"), self.plays, self.runs, self.triage, check_ids=IDS)
        self.assertFalse(p["available"])

    def test_the_file_is_refreshed_at_most_every_max_age_and_git_is_untouched(self):
        self.plays_with(item("A-1"))
        out = os.path.join(self.tmp, "bot-tonight.json")
        head, status = git(self.repo, "rev-parse", "HEAD"), git(self.repo, "status", "--porcelain")
        args = ["--repo", self.repo, "--plays", self.plays, "--runs-root", self.runs, "--triage", self.triage, "--out", out]
        self.assertEqual(0, tonight.main(args + ["--max-age", "300"]))
        first = json.load(open(out, encoding="utf-8"))
        self.assertEqual(1, first["schema"])
        os.utime(out, (time.time(), time.time()))
        with open(out, "w", encoding="utf-8") as f:
            f.write('{"schema": 1, "marker": "kept"}')
        tonight.main(args + ["--max-age", "300"])
        self.assertEqual("kept", json.load(open(out, encoding="utf-8")).get("marker"), "fresh: not rewritten")
        old = time.time() - 301
        os.utime(out, (old, old))
        tonight.main(args + ["--max-age", "300"])
        self.assertNotIn("marker", json.load(open(out, encoding="utf-8")), "stale: rewritten")
        self.assertEqual(head, git(self.repo, "rev-parse", "HEAD"))
        self.assertEqual(status, git(self.repo, "status", "--porcelain"), "git is only read")


if __name__ == "__main__":
    unittest.main()
