"""cc-P155: the web console's tests. Run from the repo root:

    python -m unittest discover -s web-console/tests

The identity check (pure, and through a real server on loopback ports), the status file's shape and both consoles
reading it, each panel from fixtures, secrets never on a page, read only, and the page on a phone-sized screen
(Microsoft Edge, headless; skipped with a reason when Edge is not installed)."""
import json
import os
import shutil
import socket
import subprocess
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

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
# cc-P182: the tailnet listener also needs the sidecar's proof (panels.decide); these P155 tests carry it, and
# test_actions.py pins what happens without it.
PROOF = "p155-test-proof-0123456789abcdef0123456789abcdef"
P = {"X-UOConsole-Proof": PROOF}


def load_fixture():
    with open(os.path.join(FIX, "console-status.json"), encoding="utf-8") as f:
        return json.load(f)


class Identity(unittest.TestCase):
    ALLOWED = ["Chase@Example.com"]

    def test_local_is_allowed_without_identity(self):
        self.assertEqual((True, "local"), panels.decide(panels.LOCAL, {}, []))

    def test_the_allowed_login_on_the_tailnet_listener(self):
        ok, why = panels.decide(panels.TAILNET, dict(P, **{"Tailscale-User-Login": "chase@example.com"}), self.ALLOWED, PROOF)
        self.assertTrue(ok, why)

    def test_another_login_is_refused(self):
        self.assertEqual((False, "login not allowed"), panels.decide(panels.TAILNET, dict(P, **{"Tailscale-User-Login": "someone@example.com"}), self.ALLOWED, PROOF))

    def test_no_identity_is_refused(self):
        self.assertEqual((False, "no Tailscale identity"), panels.decide(panels.TAILNET, dict(P), self.ALLOWED, PROOF))
        self.assertEqual((False, "no Tailscale identity"), panels.decide(panels.TAILNET, dict(P, **{"Tailscale-User-Login": "  "}), self.ALLOWED, PROOF))

    def test_nobody_is_allowed_when_no_login_is_known(self):
        self.assertFalse(panels.decide(panels.TAILNET, dict(P, **{"Tailscale-User-Login": "chase@example.com"}), [], PROOF)[0])

    def test_an_unknown_listener_is_refused(self):
        self.assertFalse(panels.decide("lan", {"Tailscale-User-Login": "chase@example.com"}, self.ALLOWED)[0])


def free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    p = s.getsockname()[1]
    s.close()
    return p


class LiveServer(unittest.TestCase):
    """The real handlers on two loopback ports, on the fixture status file and a settings file allowing one login."""

    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="p155-web-")
        with open(os.path.join(cls.tmp, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"]}, f)
        with open(os.path.join(cls.tmp, "proof.key"), "w", encoding="ascii") as f:
            f.write(PROOF)
        cls.status = os.path.join(cls.tmp, "console-status.json")
        shutil.copy(os.path.join(FIX, "console-status.json"), cls.status)
        cls.console = server.Console(REPO, os.path.join(cls.tmp, "notes"), cls.tmp, cls.status, collector=False)
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
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def get(self, port, path, headers=None, method="GET"):
        req = urllib.request.Request("http://127.0.0.1:%d%s" % (port, path), headers=headers or {}, method=method)
        try:
            with urllib.request.urlopen(req, timeout=10) as r:
                return r.status, r.read().decode("utf-8"), dict(r.headers)
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8"), dict(e.headers)

    def test_local_listener_answers_without_identity(self):
        code, body, _ = self.get(self.local_port, "/api/health")
        self.assertEqual(200, code, body)

    def test_tailnet_listener_answers_only_the_allowed_login(self):
        self.assertEqual(200, self.get(self.tail_port, "/api/health", dict(P, **{"Tailscale-User-Login": "chase@example.com"}))[0])
        code, body, _ = self.get(self.tail_port, "/", dict(P, **{"Tailscale-User-Login": "daughter@example.com"}))
        self.assertEqual((403, "not allowed\n"), (code, body))
        code, body, _ = self.get(self.tail_port, "/api/status", dict(P))
        self.assertEqual((403, "not allowed\n"), (code, body), "no identity")
        with open(os.path.join(self.tmp, "access.log"), encoding="utf-8") as f:
            log = f.read()
        self.assertIn("login not allowed", log)
        self.assertIn("no Tailscale identity", log)
        self.assertNotIn("daughter@example.com", log, "the access log names the refusal, not the caller's headers")

    def test_read_only_every_other_method_is_refused(self):
        for m in ("POST", "PUT", "DELETE", "PATCH"):
            code, body, _ = self.get(self.local_port, "/api/status", method=m)
            self.assertEqual(405, code, m)

    def test_the_status_is_the_file_both_consoles_read(self):
        code, body, h = self.get(self.local_port, "/api/status")
        self.assertEqual(200, code)
        j = json.loads(body)
        self.assertEqual([], panels.status_problems(j["status"]))
        with open(self.status, encoding="utf-8") as f:
            on_disk = json.load(f)
        self.assertEqual([t["key"] for t in on_disk["tiles"]], [t["key"] for t in j["status"]["tiles"]])
        self.assertEqual(on_disk["golem"]["queue"][0]["summary"], j["status"]["golem"]["queue"][0]["summary"])
        self.assertIn("default-src 'self'", h.get("Content-Security-Policy", ""))
        self.assertEqual("DENY", h.get("X-Frame-Options"))

    def test_no_secret_reaches_a_page(self):
        body = self.get(self.local_port, "/api/status")[1]
        self.assertNotIn("abc123secretvalue", body)
        self.assertIn("token=[removed]", body)

    def test_art_is_served_by_slot_name_only(self):
        for bad in ("/art/../server.py", "/art/..%5Cserver.py", "/art/README.md", "/art/art.json"):
            self.assertEqual(404, self.get(self.local_port, bad)[0], bad)

    def test_logs_only_by_key(self):
        self.assertEqual(404, self.get(self.local_port, "/api/logs?source=C:%5CWindows%5Cwin.ini")[0])
        code, body, _ = self.get(self.local_port, "/api/logs?source=collector&n=5")
        self.assertEqual(200, code, body)


class StatusShape(unittest.TestCase):
    def test_the_fixture_is_schema_1(self):
        self.assertEqual([], panels.status_problems(load_fixture()))

    def test_problems_are_named(self):
        d = load_fixture()
        d["schema"] = 2
        del d["tiles"][0]["word"]
        d["golem"]["queue"][0].pop("size")
        p = panels.status_problems(d)
        self.assertIn("schema is not 1", p)
        self.assertIn("a tile lacks one of key, title, word, detail, color", p)
        self.assertIn("a queued prompt lacks number, title, summary, model or size", p)

    def test_the_file_on_this_pc_if_the_collector_wrote_one(self):
        path = os.path.join(REPO, "scripts", "shard-console-cache", "console-status.json")
        if not os.path.exists(path):
            self.skipTest("no status file on this PC yet")
        with open(path, encoding="utf-8-sig") as f:
            self.assertEqual([], panels.status_problems(json.load(f)))


class Panels(unittest.TestCase):
    def test_needs_you_from_fixtures(self):
        status = load_fixture()
        playtests = {"items": [
            {"id": "T-1", "what": "check the crate", "status": "open", "who": {"key": "you", "label": "You"}},
            {"id": "T-2", "what": "bot does it", "status": "open", "who": {"key": "bot", "label": "Bot"}},
            {"id": "T-3", "what": "already passed", "status": "pass", "who": {"key": "you", "label": "You"}},
        ] + [{"id": "M-%d" % i, "what": "x", "status": "open", "who": {"key": "decide", "label": "You decide"}} for i in range(12)]}
        decisions = {"decisions": [{"id": "D-1", "title": "Pick the lead NPC", "status": "open", "link": "/decisions#D-1"},
                                   {"id": "D-2", "title": "answered", "status": "answered"}]}
        chores = panels.parse_chores("# chores\n    - [ ] an indented example\n- [ ] router DNS: point the domain at Haven\n- [x] done already\nnot a chore\n")
        services = {"services": [{"key": "homepage", "name": "This dashboard", "state": "planned", "verified": "Built; not deployed yet."},
                                 {"key": "grafana", "name": "Grafana", "state": "planned", "verified": "Chase's notes: planned."}]}
        n = panels.needs_you(status, playtests, decisions, chores, services)
        kinds = [x["kind"] for x in n]
        self.assertEqual("stopped", kinds[0], "the Golem first")
        self.assertIn("The Golem stopped: P150 (P150 needs a decision)", n[0]["text"])
        self.assertIn("P150 stopped: Needs a decision on the lead NPC.", [x["text"] for x in n])
        self.assertEqual(["Decide: Pick the lead NPC"], [x["text"] for x in n if x["kind"] == "decision"])
        pt = [x["text"] for x in n if x["kind"] == "playtest"]
        self.assertEqual("T-1: check the crate", pt[0])
        self.assertEqual(panels.PLAYTESTS_SHOWN + 1, len(pt), "ten, then a count")
        self.assertIn("and 3 more", pt[-1])
        self.assertEqual(["Deploy waiting: This dashboard"], [x["text"] for x in n if x["kind"] == "deploy"])
        self.assertEqual(1, kinds.count("package"))
        self.assertEqual(["router DNS: point the domain at Haven"], [x["text"] for x in n if x["kind"] == "chore"])

    def test_nothing_waits_on_a_quiet_day(self):
        s = load_fixture()
        s["golem"].update(state="RESTING", stoppedPrompt="", finished=[])
        s["lights"]["package"]["word"] = "LATEST"
        only_planned = {"services": [{"key": "grafana", "name": "Grafana", "state": "planned", "verified": "Chase's notes: planned."}]}
        self.assertEqual([], panels.needs_you(s, {"items": []}, None, [], only_planned), "a service only planned is no deploy waiting")

    def test_timeline_newest_first_with_kinds(self):
        commits = panels.parse_git_log("abc123456789\t2026-10-17T14:59:59-05:00\tChase\tCC task P154: responsive\n"
                                       "def\tbad line\n", "shard")
        ev = panels.timeline(commits, ["20261002-060323_test.rebuild_test.log", "20261016-080000_package.publish_package.log",
                                       "20261016-090000_commit.run_repos.log", "not-a-transcript.txt"],
                             ["20261016-080534_ops-cutover.log"], load_fixture()["golem"]["finished"], ["2026-10-17-0130", "junk"])
        self.assertEqual(["commit", "prompt", "prompt", "bot", "commit", "deploy", "publish", "build"], [e["kind"] for e in ev])
        self.assertEqual("CC task P154: responsive", ev[0]["text"])
        self.assertEqual("ops-cutover (Haven)", [e for e in ev if e["kind"] == "deploy"][0]["text"])
        self.assertEqual("2026-10-02T06:03:23", ev[-1]["time"])
        self.assertEqual(2, len(panels.timeline(commits * 5, limit=2)))

    def test_links_with_their_dots(self):
        ls = panels.links(load_fixture(), [{"key": "downloads", "name": "Download site", "url": "https://get.example/"},
                                           {"key": "playtests", "name": "dup", "url": "x"}],
                          {"downloads": {"up": True, "why": "HTTP 200"}}, [{"name": "shard-migration", "url": "https://github.com/x/shard-migration"}])
        self.assertEqual(["Play tests", "Print queue", "Download site", "shard-migration (GitHub)"], [l["name"] for l in ls])
        self.assertEqual([True, False, True, None], [l["up"] for l in ls])

    def test_log_tail_searches_and_redacts(self):
        lines = ["one", "ERROR two", "three", "error four Authorization: Bearer abcdefghijklmnop", "five"]
        self.assertEqual(["three", "error four Authorization: [removed]", "five"], panels.tail(lines, 3))
        self.assertEqual(["ERROR two", "error four Authorization: [removed]"], panels.tail(lines, 50, "error"))

    def test_redaction(self):
        cases = {
            "TS_AUTHKEY=tskey-auth-kAbCdEf123456-XYZ": "[removed]",
            "the key is tskey-auth-kAbCdEf123456-XYZ, pasted bare": "tskey-auth",
            "password: hunter2": "hunter2",
            "Bearer eyJhbGciOiJIUzI1NiJ9.abc": "eyJ",
            "-----BEGIN OPENSSH PRIVATE KEY-----\nabc\n-----END OPENSSH PRIVATE KEY-----": "abc",
            "ghp_0123456789abcdefghijABCDEFGHIJ": "ghp_",
            "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIExample0123456789 pq": "AAAAC3",
        }
        for text, gone in cases.items():
            out = panels.redact(text)
            if gone == "[removed]":
                self.assertIn("[removed]", out, text)
            else:
                self.assertNotIn(gone, out, text)
        self.assertEqual("an ordinary line", panels.redact("an ordinary line"))
        self.assertEqual({"a": ["token=[removed]"]}, panels.redact_tree({"a": ["token=xyz"]}))


@unittest.skipUnless(os.path.exists(EDGE), "Microsoft Edge is not installed here")
class PhoneScreen(unittest.TestCase):
    """The page as a phone shows it: 390 x 844 CSS pixels (DevTools device emulation, tests/cdp.py; headless Edge will
    not make a window narrower than about 500 px), drawn by its own script from the fixture."""

    def test_the_page_draws_and_nothing_is_wider_than_a_phone(self):
        import cdp
        tmp = tempfile.mkdtemp(prefix="p155-phone-")
        try:
            status = os.path.join(tmp, "console-status.json")
            shutil.copy(os.path.join(FIX, "console-status.json"), status)
            console = server.Console(REPO, os.path.join(tmp, "notes"), tmp, status, collector=False)
            port = free_port()
            s = server.ThreadingHTTPServer(("127.0.0.1", port), server.make_handler(console, panels.LOCAL))
            threading.Thread(target=s.serve_forever, daemon=True).start()
            try:
                with cdp.Browser(EDGE) as b:
                    b.phone(390, 844)
                    for tab in ("today", "golem", "needs", "timeline", "links"):
                        b.open("http://127.0.0.1:%d/#%s" % (port, tab))
                        self.assertTrue(b.wait_for("document.documentElement.getAttribute('data-drawn')"), tab + ": drawn by its script")
                        self.assertEqual(390, b.eval("window.innerWidth"), "a phone's width, not the window's")
                        wide = b.eval("document.documentElement.scrollWidth")
                        self.assertLessEqual(wide, 391, "%s: the page is %s px wide on a 390 px phone" % (tab, wide))
                        if tab == "today":
                            text = b.eval("document.body.innerText")
                            self.assertIn("TEST: RUNNING", text)
                            self.assertIn("The Golem stopped: P150", text)
                            self.assertNotIn("abc123secretvalue", text)
                            self.assertNotIn("[object Object]", text)
                            if os.environ.get("P155_SHOT"):
                                b.screenshot(os.environ["P155_SHOT"])
            finally:
                s.shutdown()
                s.server_close()
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
