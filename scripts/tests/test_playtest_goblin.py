"""cc-P773: the Play Test Goblin (scripts/playtest_goblin.py) against a fixture. Run from the repo root:

    python -m unittest discover -s scripts/tests -p "test_playtest_goblin.py"

Its rules: at most 3 open tasks per tester; never the same card twice to a tester (answered or pulled back included);
paused testers skipped; cards no human has answered first, then conflicting ones, then the rest; a settled play-test item
is not handed out; the Goblin's voice only in the one-line intro, the card's plain words and its test id kept; a
summary each run, a draft for each problem report, an email only when something changed; and nothing it writes says
Pass or Fail, and the play-test page's file is never written. The real task bank (the isle path) is checked too."""
import hashlib
import json
import os
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)
sys.path.insert(0, SCRIPTS)
import playtest_goblin as goblin  # noqa: E402


def card(key, test="T-100"):
    return {"key": key, "testId": test, "title": "Title " + key, "why": "Why " + key, "where": "Where " + key,
            "coords": "1, 2", "steps": ["Step one for " + key], "lookFor": "Look for " + key}


class Clock:
    def __init__(self):
        self.n = 0

    def __call__(self):
        self.n += 1
        return "2026-11-03T05:00:%02dZ" % self.n


class Goblin(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p773-goblin-")
        self.pt = os.path.join(self.tmp, "playtest")
        self.notes = os.path.join(self.tmp, "shard-migration")
        os.makedirs(self.pt)
        os.makedirs(os.path.join(self.notes, "testing"))
        self.bank = os.path.join(self.tmp, "bank.json")
        self.cards = [card("c1", "T-101"), card("c2", "T-102"), card("c3", "T-103"), card("c4", "T-104"),
                      card("c5", "T-105"), card("c6", "T-106"), card("settled", "T-900"), dict(card("old"), retired=True)]
        self.write(self.bank, {"version": 1, "cards": self.cards})
        self.playtests = os.path.join(self.notes, "testing", "play-tests.json")
        self.write(self.playtests, {"items": [{"id": "T-900", "status": "pass"}, {"id": "T-101", "status": "open"}]})
        self.clock = Clock()

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    @staticmethod
    def raw(path):
        with open(path, "rb") as f:
            return f.read()

    @staticmethod
    def write(path, obj):
        with open(path, "w", encoding="utf-8") as f:
            json.dump(obj, f)

    def testers(self, *rows):
        self.write(os.path.join(self.pt, "testers.json"),
                   {"version": 1, "testers": [{"account": a, "name": a.title(), "paused": p} for a, p in rows]})

    def tasks(self):
        with open(os.path.join(self.pt, "tasks.json"), encoding="utf-8") as f:
            return json.load(f)["tasks"]

    def answer(self, account, tid, button, text="words"):
        with open(os.path.join(self.pt, "playtest-reports.jsonl"), "a", encoding="utf-8") as f:
            f.write(json.dumps({"at": "2026-11-03T18:00:00Z", "account": account, "character": account.title(), "task": tid,
                                "test": "", "button": button, "text": text, "facet": "Trammel", "x": 1, "y": 2, "z": 0,
                                "version": "v"}) + "\n")

    def run_goblin(self, day="2026-11-03"):
        return goblin.run(self.pt, self.notes, self.bank, day, clock=self.clock)

    def test_cap_three_no_repeats_paused_skipped(self):
        self.testers(("chase", False), ("paige", False), ("dylan", True))
        out = self.run_goblin()
        mine = lambda acc: [t for t in self.tasks() if t["account"] == acc]   # noqa: E731
        self.assertEqual(3, len(mine("chase")))
        self.assertEqual(3, len(mine("paige")))
        self.assertEqual([], mine("dylan"))
        self.assertIn("Dylan: paused", out["skipped"])
        # never the settled or retired card
        self.assertFalse({"settled", "old"} & {t["card"] for t in self.tasks()})
        # spread: the second tester gets the cards the first did not, before any repeats across testers
        self.assertEqual(["c1", "c2", "c3"], [t["card"] for t in mine("chase")])
        self.assertEqual(["c4", "c5", "c6"], [t["card"] for t in mine("paige")])
        self.assertEqual(["G-0001", "G-0002", "G-0003", "G-0004", "G-0005", "G-0006"], [t["id"] for t in self.tasks()])

        # a second night with nothing answered: nobody is over the cap, nothing new
        out = self.run_goblin("2026-11-04")
        self.assertEqual([], out["newTasks"])
        self.assertIn("Chase: already has 3 open tasks", out["skipped"])
        self.assertEqual("", out["mail"])

        # chase answers one, one is pulled back: two new, never c1, c2 or c3 again
        self.answer("chase", "G-0001", "worked", "")
        ts = self.tasks()
        ts[1]["status"] = "pulled"
        self.write(os.path.join(self.pt, "tasks.json"), {"version": 1, "tasks": ts})
        out = self.run_goblin("2026-11-05")
        got = [t["card"] for t in out["newTasks"]]
        self.assertEqual(2, len(got))
        self.assertFalse({"c1", "c2", "c3"} & set(got))
        open_chase = [t for t in self.tasks() if t["account"] == "chase" and t["status"] == "open" and t["id"] != "G-0001"]
        self.assertEqual(3, len(open_chase))

        # chase has had every card: said so, nothing repeated
        for t in [t for t in self.tasks() if t["account"] == "chase" and t["status"] == "open"]:
            self.answer("chase", t["id"], "worked", "")
        out = self.run_goblin("2026-11-06")
        self.assertEqual(1, len([t for t in out["newTasks"] if t["account"] == "chase"]))
        cards = [t["card"] for t in self.tasks() if t["account"] == "chase"]
        self.assertEqual(len(cards), len(set(cards)))

    def test_unanswered_first_then_conflicting(self):
        self.cards = [card("a"), card("b"), card("c")]
        self.write(self.bank, {"version": 1, "cards": self.cards})
        self.testers(("chase", False), ("paige", False))
        self.run_goblin()   # chase a, b, c; paige a, b, c (spread, but only three cards)
        ids = {(t["account"], t["card"]): t["id"] for t in self.tasks()}
        self.answer("chase", ids[("chase", "a")], "worked", "")
        self.answer("paige", ids[("paige", "a")], "wrong", "broke")      # a: conflicting
        self.answer("chase", ids[("chase", "b")], "worked", "")          # b: agreed
        tasks = goblin.playtesters.PlayTestData(self.pt).tasks()[0]
        reports = goblin.playtesters.PlayTestData(self.pt).reports()
        answers = goblin.card_answers(tasks, reports)
        self.assertEqual(1, goblin.rank(answers["a"]))
        self.assertEqual(2, goblin.rank(answers["b"]))
        self.assertEqual(0, goblin.rank(answers.get("c", set())))
        # a newcomer gets c (nobody answered), then a (conflicting), then b (agreed)
        with open(os.path.join(self.pt, "testers.json"), encoding="utf-8") as f:
            doc = json.load(f)
        doc["testers"].append({"account": "dylan", "name": "Dylan", "paused": False})
        self.write(os.path.join(self.pt, "testers.json"), doc)
        out = self.run_goblin("2026-11-04")
        self.assertEqual(["c", "a", "b"], [t["card"] for t in out["newTasks"] if t["account"] == "dylan"])

    def test_plain_words_goblin_intro_and_test_id(self):
        self.testers(("chase", False))
        out = self.run_goblin()
        t = out["newTasks"][0]
        self.assertEqual("T-101", t["testId"])
        self.assertEqual(("Title c1", "Why c1", "Where c1", "1, 2", ["Step one for c1"], "Look for c1"),
                         (t["title"], t["why"], t["where"], t["coords"], t["steps"], t["lookFor"]))
        self.assertIn(t["intro"], goblin.INTROS)
        self.assertTrue(all(i.startswith("The Goblin") for i in goblin.INTROS))
        for k in ("title", "why", "where", "lookFor"):
            self.assertNotIn("Goblin", t[k])

    def test_summary_drafts_mail_and_never_pass_or_fail(self):
        before = self.raw(self.playtests)
        self.testers(("chase", False))
        out = self.run_goblin()
        self.assertTrue(os.path.exists(os.path.join(self.notes, "notes", "goblin", "2026-11-03.md")))
        self.assertTrue(out["mail"])
        with open(out["mail"], encoding="utf-8") as f:
            subject = f.readline().strip()
        self.assertEqual("Play testers: 3 new tasks handed out", subject)
        os.remove(out["mail"])

        self.answer("chase", "G-0001", "worked", "")
        self.answer("chase", "G-0002", "couldnt", "The gate would not open.")
        self.answer("chase", "", "report", "A rabbit hums.")
        out = self.run_goblin("2026-11-04")
        self.assertEqual(3, len(out["newReports"]))
        with open(os.path.join(self.notes, "notes", "goblin", "pending-pt.md"), encoding="utf-8") as f:
            pend = f.read()
        self.assertEqual(2, pend.count("### Draft PT-?"))   # the problem and the free report; not the Worked
        self.assertIn("The gate would not open.", pend)
        with open(out["mail"], encoding="utf-8") as f:
            self.assertTrue(f.readline().startswith("Play testers: 3 new reports, 2 new tasks handed out"))
        os.remove(out["mail"])

        # nothing new: a summary, no mail, no new drafts
        out = self.run_goblin("2026-11-05")
        self.assertEqual(([], [], ""), (out["newReports"], out["newTasks"], out["mail"]))

        # never Pass or Fail, and the play-test page's file untouched
        self.assertEqual(before, self.raw(self.playtests))
        for root, _, files in os.walk(self.tmp):
            for name in files:
                if os.path.join(root, name) in (self.playtests, self.bank):
                    continue
                with open(os.path.join(root, name), encoding="utf-8") as f:
                    text = f.read().lower()
                for word in ('"pass"', '"fail"', "status: pass", "status: fail", "marked pass", "marked fail"):
                    self.assertNotIn(word, text, name)

    def test_a_broken_file_stops_without_changes(self):
        with open(os.path.join(self.pt, "testers.json"), "w", encoding="utf-8") as f:
            f.write("{ broken")
        self.assertEqual(2, goblin.main(["--playtest-dir", self.pt, "--notes", self.notes, "--bank", self.bank]))
        self.assertFalse(os.path.exists(os.path.join(self.pt, "tasks.json")))
        self.testers(("chase", False))
        with open(os.path.join(self.pt, "tasks.json"), "w", encoding="utf-8") as f:
            f.write("[")
        self.assertEqual(2, goblin.main(["--playtest-dir", self.pt, "--notes", self.notes, "--bank", self.bank]))
        with open(os.path.join(self.pt, "tasks.json"), encoding="utf-8") as f:
            self.assertEqual("[", f.read())

    def test_dry_run_hands_out_nothing(self):
        self.testers(("chase", False))
        out = goblin.run(self.pt, self.notes, self.bank, "2026-11-03", clock=self.clock, dry_run=True)
        self.assertEqual(3, len(out["newTasks"]))   # what it would hand out
        self.assertFalse(os.path.exists(os.path.join(self.pt, "tasks.json")))
        self.assertEqual("", out["mail"])


class TheRealBank(unittest.TestCase):
    def test_the_isle_path_comes_first_in_plain_words(self):
        cards = goblin.load_bank(goblin.DEFAULT_BANK)
        live = [c for c in cards if not c.get("retired")]
        self.assertGreaterEqual(len(live), 12)
        self.assertTrue(all(c["key"].startswith("isle-") for c in live[:15]), [c["key"] for c in live[:15]])
        for c in cards:
            for k in ("key", "testId", "title", "why", "where", "coords", "steps", "lookFor"):
                self.assertTrue(c.get(k), (c.get("key"), k))
            blob = json.dumps(c)
            self.assertNotIn("Goblin", blob)
            self.assertTrue(all(ord(ch) < 128 for ch in blob), c["key"])
        self.assertEqual(sorted(c["key"] for c in cards), sorted(set(c["key"] for c in cards)))


if __name__ == "__main__":
    unittest.main()
