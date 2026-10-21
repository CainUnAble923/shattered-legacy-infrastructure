"""cc-P215: the Golem login checks' own facts. Standard library only; no shard needed.

    cd D:\\ShatteredLegacy\\tools\\playtest-bot
    python -m unittest discover -s tests

The Golem login checks (uobot/golemcheck.py) may talk to test shards (2594, and the scratch ports 2595-2599) on any host,
and never to the live port 2593, from any host or through any relay. The overnight run keeps its scratch-only rule.
"""
import json
import os
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from uobot import safety, golemcheck  # noqa: E402
from uobot.client import Client, Transcript, Cliloc  # noqa: E402


class TestShardTargets(unittest.TestCase):
    def test_the_live_port_is_refused_from_any_host(self):
        for host in ("127.0.0.1", "192.168.1.58", "chases-tower", "shatteredlegacyuo.com", "100.64.0.1"):
            with self.assertRaises(safety.UnsafeTarget, msg=host):
                safety.check_test_target(host, 2593)

    def test_test_ports_are_allowed_and_nothing_else(self):
        for host, port in (("127.0.0.1", 2594), ("192.168.1.58", 2594), ("stephs-desktop", 2594), ("127.0.0.1", 2597)):
            safety.check_test_target(host, port)
        for port in (22, 80, 443, 2592, 2600, 8080):
            with self.assertRaises(safety.UnsafeTarget, msg=port):
                safety.check_test_target("127.0.0.1", port)
        with self.assertRaises(safety.UnsafeTarget):
            safety.check_test_target("", 2594)

    def test_the_relay_must_name_the_port_we_dialled_and_we_go_back_to_our_host(self):
        # The real Test Shard advertises its LAN address; another PC dialled it by its tailnet name.
        self.assertEqual("chases-tower", safety.check_test_relay(bytes([192, 168, 1, 58]), 2594, "chases-tower", 2594))
        for ip, port in ((bytes([192, 168, 1, 58]), 2593), (bytes([127, 0, 0, 1]), 2595), (bytes([192, 168, 1]), 2594)):
            with self.assertRaises(safety.UnsafeTarget, msg=port):
                safety.check_test_relay(ip, port, "chases-tower", 2594)

    def test_the_overnight_scratch_rule_is_unchanged(self):
        with self.assertRaises(safety.UnsafeTarget):
            safety.check_target("127.0.0.1", 2594)
        c = Client("x", "y", Transcript(os.devnull), Cliloc(None), "t")
        with self.assertRaises(safety.UnsafeTarget):
            c._connect("127.0.0.1", 2594)

    def test_a_test_client_refuses_the_live_port_before_opening_a_socket(self):
        c = Client("x", "y", Transcript(os.devnull), Cliloc(None), "t", test_target=("127.0.0.1", 2593))
        with self.assertRaises(safety.UnsafeTarget):
            c.login()
        self.assertIsNone(c.sock)

    def test_golemcheck_refuses_the_live_port_and_says_so(self):
        out = tempfile.mkdtemp()
        self.assertEqual(2, golemcheck.main(["login", "--host", "127.0.0.1", "--port", "2593", "--golem", "Bronze", "--out", out]))


class Accounts(unittest.TestCase):
    def test_character_names_follow_the_golem(self):
        self.assertEqual("Copper", golemcheck.character_name("Copper", "admin"))
        self.assertEqual("Copper the Tester", golemcheck.character_name("Copper", "player"))

    def test_the_password_comes_from_the_helper_and_is_not_printed(self):
        root = tempfile.mkdtemp()
        with open(os.path.join(root, "Get-GolemTestAccount.ps1"), "w") as f:
            f.write("param([string]$Golem, [string]$Role, [switch]$Json)\n"
                    "[Console]::Out.WriteLine((@{ Username = ($Golem.ToLower() + '-' + $Role); Password = 'Pw12345Example' } | ConvertTo-Json -Compress))\n")
        user, pw = golemcheck.account("Pfaketest", "player", root=root)
        self.assertEqual(("pfaketest-player", "Pw12345Example"), (user, pw))
        with open(os.path.join(root, "Get-GolemTestAccount.ps1"), "w") as f:
            f.write("exit 3\n")
        with self.assertRaises(Exception) as e:
            golemcheck.account("Pfaketest", "player", root=root)
        self.assertNotIn("Pw12345Example", str(e.exception))


class OvernightRunAsAGolem(unittest.TestCase):
    def test_a_golem_running_the_bot_seeds_and_uses_its_own_accounts(self):
        from uobot import run as R
        sent = []

        class StubClient(object):
            def __init__(self, user, password, tr, cl, label=None, test_target=None):
                self.user = user
                sent.append(("login", user))

            def login(self):
                return ["Bot Admin"]

            def play(self, slot):
                return True

            def command(self, text, settle=0.8):
                sent.append(("command", text))
                return [{"text": "[GolemAccounts] 18 Golem accounts in the manifest: 0 made"}]

            def close(self):
                pass

        old_client, old_account = R.Client, golemcheck.account
        R.Client = StubClient
        golemcheck.account = lambda golem, role, root=None: ("%s-%s" % (golem.lower(), role), "pw-" + role)
        try:
            class Ctx(object):
                secrets = {"admin": "slbot-admin", "adminPassword": "a", "player": "slbot-player", "playerPassword": "p"}
                tr = Transcript(os.devnull)
                cliloc = None
            ctx = Ctx()
            R.use_golem_accounts(ctx, "Bronze")
        finally:
            R.Client, golemcheck.account = old_client, old_account
        self.assertEqual([("login", "slbot-admin"), ("command", "[SeedGolemAccounts")], sent)
        self.assertEqual(("bronze-admin", "pw-admin", "bronze-player", "pw-player", "p"),
                         (ctx.secrets["admin"], ctx.secrets["adminPassword"], ctx.secrets["player"], ctx.secrets["playerPassword"], ctx.secrets["poolPassword"]))


if __name__ == "__main__":
    unittest.main()
