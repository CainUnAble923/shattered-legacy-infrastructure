"""cc-P215: each Golem card shows its own test-shard accounts, and Chase can copy a password. Run from the repo root:

    python -m unittest discover -s web-console/tests

The names are on the card (never a password); the password comes only from POST /api/golem-password, on the tailnet
listener, for an allowed login, from this page (the action header), through the Golem kit's helper (a stand-in here).
"""
import json
import os
import sys
import unittest
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import test_remote_golems as _r  # noqa: E402

CHASE = _r.CHASE

SECRET = "StandInPw0123456789abcd"


class GolemTestAccounts(unittest.TestCase):
    # The remote-Golem fixture's world (a registry, the status, both listeners), without its own tests.
    tearDown = _r.RemoteGolems.tearDown
    req = _r.RemoteGolems.req

    def setUp(self):
        _r.RemoteGolems.setUp(self)
        self.reads = []
        self.console.password_reader = lambda script, golem, role, root: (self.reads.append((golem, role)) or SECRET + golem + role)
        for n in ("iron", "gold"):
            with open(os.path.join(self.groot, "test-accounts-%s.key" % n), "w", encoding="ascii") as f:
                f.write("{}")

    def text(self, body, headers, port=None):
        r = urllib.request.Request("http://127.0.0.1:%d/api/golem-password" % (port or self.tail_port),
                                   data=json.dumps(body).encode("utf-8"), method="POST",
                                   headers=dict(headers, **{"Content-Type": "application/json"}))
        try:
            with urllib.request.urlopen(r, timeout=10) as x:
                return x.status, x.read().decode("utf-8"), dict(x.headers)
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8"), dict(e.headers)

    def test_each_card_names_its_two_accounts_and_never_a_password(self):
        code, j = self.req("/api/status", headers=CHASE)
        self.assertEqual(200, code)
        cards = {c["name"]: c for c in j["status"]["golem"]["golems"]}
        self.assertEqual({"admin": "gold-admin", "player": "gold-player", "copy": True}, cards["Gold"]["testAccounts"])
        self.assertEqual({"admin": "bronze-admin", "player": "bronze-player", "copy": False}, cards["Bronze"]["testAccounts"])
        self.assertNotIn(SECRET, json.dumps(j))
        self.assertEqual([], self.reads, "the status never reads a password")

    def test_chase_copies_one_password_as_plain_text(self):
        code, body, h = self.text({"golem": "Gold", "role": "player"}, dict(CHASE, **{"X-UOConsole-Action": "1"}))
        self.assertEqual(200, code)
        self.assertEqual(SECRET + "Gold" + "player", body)
        self.assertEqual("no-store", h.get("Cache-Control"))
        self.assertTrue(h.get("Content-Type", "").startswith("text/plain"))
        with open(os.path.join(self.data, "access.log"), encoding="utf-8") as f:
            log = f.read()
        self.assertIn("test password Gold player", log)
        self.assertNotIn(SECRET, log)

    def test_only_the_page_on_the_tailnet_for_an_allowed_login_gets_it(self):
        ok = dict(CHASE, **{"X-UOConsole-Action": "1"})
        for why, headers, port, want in (
                ("the local listener", ok, self.local_port, 403),
                ("no action header", CHASE, None, 403),
                ("another login", dict(ok, **{"Tailscale-User-Login": "someone@example.com"}), None, 403),
                ("no proof", {"Tailscale-User-Login": "chase@example.com", "X-UOConsole-Action": "1"}, None, 403),
                ("another origin", dict(ok, Origin="https://evil.example"), None, 403)):
            code, body, _ = self.text({"golem": "Gold", "role": "admin"}, headers, port)
            self.assertEqual(want, code, why)
            self.assertNotIn(SECRET, body, why)
        self.assertEqual([], self.reads, "no refused request ever ran the helper")

    def test_a_golem_without_a_file_here_a_bad_role_or_no_such_golem_is_refused(self):
        ok = dict(CHASE, **{"X-UOConsole-Action": "1"})
        self.assertEqual(404, self.text({"golem": "Bronze", "role": "admin"}, ok)[0])
        self.assertEqual(400, self.text({"golem": "Gold", "role": "owner"}, ok)[0])
        self.assertEqual(404, self.text({"golem": "Nobody", "role": "admin"}, ok)[0])
        self.assertEqual([], self.reads)


if __name__ == "__main__":
    unittest.main()
