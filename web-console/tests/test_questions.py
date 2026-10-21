"""cc-P216: the Golems ask their questions on the web console, and Chase answers there. Run from the repo root:

    python -m unittest discover -s web-console/tests

A question is a file in D:/UO/golem/questions/ (Questions.ps1 writes it: golem.ps1 on a STOPPED or a NEEDS YOU, or the
session through Ask-Chase.ps1); a remote Golem's agent posts its own. These tests use the real handlers on loopback ports
with RemoteGolems' fixture (the sidecar's proof set by hand) and a fake Answer-Question.ps1 that records its argv: the
card shows the open question in full with its choices and history, an answer runs the Golem's own script with argv only
(the words as base64), a closed question or a stale page is refused, only the signed-in page may answer, an answer that
cannot be undone wants a confirm, and a remote Golem's question and answer go through its agent."""
import base64
import json
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import test_remote_golems as base  # noqa: E402
from test_remote_golems import CHASE, EDGE, FIX, RemoteGolems  # noqa: E402

QID = "Bronze-P100-20261021-091500-a1b2"


def question(qid, **kw):
    q = {"id": qid, "golem": qid.split("-")[0], "prompt": qid.split("-")[1], "kind": "stopped",
         "question": "P100 stopped: the saves folder holds two worlds and I cannot tell which one the test shard should keep. " + "More words. " * 30 + "END",
         "choices": [{"label": "Retry with my answer", "does": "P100 goes back first in the queue and a new session starts it again"}],
         "freeText": True, "context": "CC's last message:\nI looked at both.", "askedAt": "2026-10-21T09:15:00", "irreversible": False,
         "irreversibleWhy": "", "source": "stopped", "state": "open", "closedAt": "", "closedWhy": "", "answeredBy": ""}
    q.update(kw)
    return q


class Questions(RemoteGolems):

    def setUp(self):
        super().setUp()
        self.arecord = os.path.join(self.tmp, "answers.jsonl")
        self.console.answer_runner = [sys.executable, os.path.join(FIX, "fake_answer.py"), self.arecord]
        os.makedirs(os.path.join(self.groot, "questions"))
        os.makedirs(os.path.join(self.groot, "answers"))

    def put(self, q, answer=None):
        with open(os.path.join(self.groot, "questions", q["id"] + ".json"), "w", encoding="utf-8") as f:
            json.dump(q, f)
        if answer:
            with open(os.path.join(self.groot, "answers", q["id"] + ".json"), "w", encoding="utf-8") as f:
                json.dump(answer, f)

    def answer(self, body, headers=None, port=None):
        return self.req("/api/answer", body, dict(CHASE, **{"X-UOConsole-Action": "1"}) if headers is None else headers, port=port)

    def answers(self):
        if not os.path.exists(self.arecord):
            return []
        with open(self.arecord, encoding="utf-8") as f:
            return [json.loads(l) for l in f if l.strip()]

    def test_the_open_question_is_on_its_card_in_full_with_its_history_and_the_tab_count(self):
        self.put(question(QID))
        old = question("Bronze-P99-20261020-101000-c3d4", state="answered", closedAt="2026-10-20T10:20:00", closedWhy="answered from console by chase@example.com")
        self.put(old, {"id": old["id"], "golem": "Bronze", "prompt": "P99", "at": "2026-10-20T10:20:00", "who": "chase@example.com", "source": "console",
                       "choice": 0, "choiceLabel": "Retry with my answer", "does": "", "text": "Keep Tokuno", "delivered": True, "deliveredAt": "2026-10-20T10:21:00"})
        self.put(question("Iron-P98-20261021-080000-0000", golem="Iron", prompt="P98"))
        c, j = self.card("Bronze")
        self.assertEqual([QID], [q["id"] for q in c["questions"]["open"]])
        self.assertTrue(c["questions"]["open"][0]["question"].endswith("END"), "the whole question, not cut off")
        self.assertEqual("Retry with my answer", c["questions"]["open"][0]["choices"][0]["label"])
        self.assertEqual(1, len(c["questions"]["history"]))
        self.assertEqual("Keep Tokuno", c["questions"]["history"][0]["answer"]["text"])
        self.assertEqual(2, j["questions"]["open"], "the tab's count: every Golem's open questions")
        self.assertEqual([], self.card("Gold")[0]["questions"]["open"])

    def test_an_answer_runs_the_golems_own_script_with_argv_only_and_the_words_as_base64(self):
        self.put(question(QID))
        words = 'Keep Tokuno. Not "rm -rf saves"; $(calc) & del x | y -Root C:\\'
        code, j = self.answer({"id": QID, "choice": 0, "text": words})
        self.assertEqual(200, code, j)
        self.assertTrue(j["ok"])
        self.assertIn("back in the queue", j["result"])
        a = self.answers()
        self.assertEqual(1, len(a))
        args = a[0]
        self.assertEqual(QID, args[args.index("-Id") + 1])
        self.assertEqual("0", args[args.index("-Choice") + 1])
        self.assertEqual(words, base64.b64decode(args[args.index("-TextB64") + 1]).decode("utf-8"), "the words, intact, only as base64")
        self.assertNotIn(words, args)
        self.assertEqual(["-Who", "chase@example.com"], args[args.index("-Who"):args.index("-Who") + 2])
        self.assertEqual("console", args[args.index("-Source") + 1])
        self.assertEqual("Bronze", args[args.index("-Golem") + 1])
        with open(os.path.join(self.data, "actions.log"), encoding="utf-8") as f:
            log = f.read()
        self.assertIn(QID, log)
        self.assertNotIn("Tokuno", log, "the action log never holds Chase's words")

    def test_a_closed_question_a_stale_page_and_a_bad_answer_are_refused(self):
        self.put(question(QID, state="expired", closedAt="2026-10-21T09:20:00", closedWhy="P100 is no longer STOPPED or running"))
        code, j = self.answer({"id": QID, "choice": 0})
        self.assertEqual(409, code, j)
        self.assertIn("closed: the question was expired", j["error"])
        code, j = self.answer({"id": "Bronze-P100-20261021-091500-ffff", "choice": 0})
        self.assertEqual(409, code, j)
        for bad in ({"id": "../../x", "choice": 0}, {"id": QID.replace("Bronze", "Nobody"), "choice": 0}):
            code, j = self.answer(bad)
            self.assertIn(code, (400, 404), j)
        q2 = "Bronze-P101-20261021-093000-0001"
        self.put(question(q2, freeText=False))
        for body, why in (({"id": q2, "choice": 4}, "not one of its choices"), ({"id": q2, "choice": True}, "not one of its choices"),
                          ({"id": q2, "choice": -1, "text": "words"}, "not text"), ({"id": q2, "choice": -1}, "pick a choice"),
                          ({"id": q2, "choice": 0, "text": 7}, "is text")):
            code, j = self.answer(body)
            self.assertEqual(400, code, (body, j))
            self.assertIn(why, j["error"])
        self.put(question(QID.replace("P100", "P102")))
        code, j = self.answer({"id": QID.replace("P100", "P102"), "choice": 0, "text": "x" * 2001})
        self.assertEqual(400, code, j)
        self.assertIn("longer than 2000", j["error"])
        self.assertEqual([], self.answers(), "nothing ran")
        # The Golem's own check is the last word: a refusal there comes back as it is.
        self.put(question("Bronze-P103-20261021-094000-0002"))
        open(self.arecord + ".refuse", "w").close()
        code, j = self.answer({"id": "Bronze-P103-20261021-094000-0002", "choice": 0})
        self.assertEqual(409, code, j)
        self.assertIn("answered already", j["result"])

    def test_an_answer_that_cannot_be_undone_wants_a_confirm(self):
        self.put(question(QID, irreversible=True, irreversibleWhy="it deletes or overwrites saves or data, which cannot be undone"))
        code, j = self.answer({"id": QID, "choice": 0})
        self.assertEqual(409, code, j)
        self.assertTrue(j.get("needsConfirm"))
        self.assertIn("cannot be undone", j["error"])
        self.assertEqual([], self.answers())
        code, j = self.answer({"id": QID, "choice": 0, "confirm": True})
        self.assertEqual(200, code, j)

    def test_only_the_signed_in_page_may_answer(self):
        self.put(question(QID))
        body = {"id": QID, "choice": 0}
        self.assertEqual(403, self.answer(body, port=self.local_port)[0], "this PC's own page is read only")
        self.assertEqual(403, self.answer(body, headers=dict(CHASE))[0], "no X-UOConsole-Action header")
        self.assertEqual(403, self.answer(body, headers={"X-UOConsole-Action": "1", "X-UOConsole-Proof": base.PROOF})[0], "no login")
        self.assertEqual(403, self.answer(body, headers={"X-UOConsole-Action": "1", "Tailscale-User-Login": "chase@example.com"})[0], "no proof")
        self.assertEqual(403, self.answer(body, headers=dict(CHASE, **{"X-UOConsole-Action": "1", "Tailscale-User-Login": "someone@example.com"}))[0], "another login")
        self.assertEqual(403, self.answer(body, headers=dict(CHASE, **{"X-UOConsole-Action": "1", "Origin": "https://evil.example"}))[0], "another origin")
        self.assertEqual(403, self.answer(body, headers=dict(CHASE, **{"X-UOConsole-Action": "1", "Sec-Fetch-Site": "cross-site"}))[0], "a cross-site fetch")
        self.assertIn(self.agent("/api/answer", body)[0], (403,), "an agent cannot answer")
        self.assertEqual([], self.answers())

    def test_a_remote_golems_question_and_answer_go_through_its_agent(self):
        gid = "Gold-P140-20261021-100000-beef"
        posted = {"golem": "Gold", "questions": {"open": [question(gid, golem="Gold", prompt="P140")], "history": []}}
        code, j = self.agent("/api/agent/questions", posted)
        self.assertEqual(200, code, j)
        code, j = self.agent("/api/agent/questions", {"golem": "Gold", "questions": {"open": [question(QID)], "history": []}})
        self.assertEqual(400, code, "an agent posts only its own Golem's questions")
        c, j = self.card("Gold")
        self.assertEqual([gid], [q["id"] for q in c["questions"]["open"]])
        code, j = self.answer({"id": gid, "choice": 0, "text": "Keep the newer save"})
        self.assertEqual(202, code, j)
        self.assertTrue(j["queued"])
        self.assertEqual([], self.answers(), "nothing runs on the game PC for a remote Golem")
        c, _ = self.card("Gold")
        self.assertIn("answer sent", c["questions"]["open"][0]["answerPending"])
        self.assertEqual(409, self.answer({"id": gid, "choice": 0})[0], "a second answer while the first is on its way")
        code, j = self.agent("/api/agent/commands")
        self.assertEqual(["answer"], [x["key"] for x in j["commands"]])
        inp = j["commands"][0]["inputs"]
        self.assertEqual({"question": gid, "choice": "0", "who": "chase@example.com"}, {k: inp[k] for k in ("question", "choice", "who")})
        self.assertEqual("Keep the newer save", base64.b64decode(inp["textb64"]).decode("utf-8"))
        # The agent answers there, and its next post shows the question closed.
        done = dict(question(gid, golem="Gold", prompt="P140"), state="answered", closedAt="2026-10-21T10:05:00")
        code, j = self.agent("/api/agent/questions", {"golem": "Gold", "questions": {"open": [], "history": [done]}})
        self.assertEqual(200, code, j)
        code, j = self.answer({"id": gid, "choice": 0})
        self.assertEqual(409, code, j)
        self.assertIn("closed: the question was answered", j["error"])


@unittest.skipUnless(os.path.exists(EDGE), "Microsoft Edge is not installed here")
class PhoneQuestions(Questions):
    """The question on Bronze's card on a 390 px phone: the whole question, a button per choice, the text box; a choice
    sends the answer; the Golem tab's header counts the open questions; an irreversible one says so."""

    def test_the_question_and_its_answer_on_a_phone(self):
        import cdp
        self.put(question(QID, choices=[{"label": "Retry with my answer", "does": "P100 goes back first"}, {"label": "Keep both", "does": "I rebuild beside them"}]))
        self.put(question("Bronze-P101-20261021-093000-0001", prompt="P101", question="Delete the old test saves?", irreversible=True,
                          irreversibleWhy="it deletes or overwrites saves or data, which cannot be undone", askedAt="2026-10-21T09:30:00"))
        with cdp.Browser(EDGE) as b:
            b.phone(390, 844)
            b.call("Network.enable")
            b.call("Network.setExtraHTTPHeaders", headers=CHASE)
            b.open("http://127.0.0.1:%d/#golem" % self.tail_port)
            self.assertTrue(b.wait_for("document.querySelectorAll('#golem-team [data-question]').length === 2"), "both questions on Bronze's card")
            self.assertEqual("2", b.eval("document.getElementById('golem-qcount').textContent"))
            box = "document.querySelector('[data-question=\"%s\"]')" % QID
            self.assertTrue(b.eval(box + ".innerText.indexOf('END') >= 0"), "the whole question")
            self.assertEqual(3, b.eval(box + ".querySelectorAll('button.answer').length"), "two choices and Send my words")
            self.assertEqual(1, b.eval(box + ".querySelectorAll('textarea.qtext').length"))
            self.assertIn("Cannot be undone", b.eval("document.querySelector('[data-question=\"Bronze-P101-20261021-093000-0001\"]').innerText"))
            self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391)
            b.eval("var t = %s.querySelector('textarea.qtext'); t.value = 'Keep the newer one'; t.dispatchEvent(new Event('input'));" % box)
            b.eval(box + ".querySelector('button[data-choice=\"1\"]').click()")
            self.assertTrue(b.wait_for("document.getElementById('golem-team').innerText.indexOf('back in the queue') >= 0"),
                            "the Golem's reply: " + str(b.eval("document.getElementById('golem-team').innerText")))
        a = self.answers()
        self.assertEqual(1, len(a))
        self.assertEqual("1", a[0][a[0].index("-Choice") + 1])
        self.assertEqual("Keep the newer one", base64.b64decode(a[0][a[0].index("-TextB64") + 1]).decode("utf-8"))


for _name in [n for n in dir(RemoteGolems) if n.startswith("test_")]:
    setattr(Questions, _name, None)   # RemoteGolems' own tests run once, in test_remote_golems
for _name in [n for n in dir(Questions) if n.startswith("test_") and getattr(Questions, n) is not None]:
    setattr(PhoneQuestions, _name, None)


if __name__ == "__main__":
    unittest.main()
