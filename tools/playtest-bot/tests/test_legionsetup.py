"""cc-P219: uobot.legionsetup, the Golem's own Admin setting up its Player character before a Legion script test.
Standard library only; no shard is touched (the refusals come before any connection).

    cd D:\\ShatteredLegacy\\tools\\playtest-bot
    python -m unittest discover -s tests
"""
import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stdout

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from uobot import legionsetup  # noqa: E402


def run(argv):
    out = io.StringIO()
    with redirect_stdout(out):
        code = legionsetup.main(argv)
    return code, json.loads(out.getvalue().strip().splitlines()[-1])


class LegionSetup(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p219-ls-")
        self.cmds = os.path.join(self.tmp, "c.json")
        with open(self.cmds, "w") as f:
            json.dump(['[global set Skills.Mining.Base 100 where playermobile Name == "{player}"'], f)

    def test_player_and_golem_are_filled_in(self):
        self.assertEqual(['[global addtopack Pickaxe where playermobile Name == "Iron the Tester"', "[where Iron"],
                         legionsetup.expand(['[global addtopack Pickaxe where playermobile Name == "{player}"', "[where {golem}"], "Iron"))

    def test_the_live_port_is_refused_from_any_host(self):
        for host in ("127.0.0.1", "192.168.1.58", "shatteredlegacyuo.com"):
            code, j = run(["--host", host, "--port", "2593", "--golem", "Iron", "--commands", self.cmds])
            self.assertEqual(2, code, j)
            self.assertFalse(j["ok"])
            self.assertIn("live shard", j["why"])

    def test_a_port_that_is_no_test_shard_is_refused(self):
        code, j = run(["--host", "127.0.0.1", "--port", "8080", "--golem", "Iron", "--commands", self.cmds])
        self.assertEqual(2, code, j)
        self.assertIn("not a test shard port", j["why"])

    def test_only_staff_commands_are_typed(self):
        with open(self.cmds, "w") as f:
            json.dump(["hello there", "[where"], f)
        code, j = run(["--host", "127.0.0.1", "--port", "2594", "--golem", "Iron", "--commands", self.cmds])
        self.assertEqual(2, code, j)
        self.assertIn("staff commands", j["why"])

    def test_an_error_answer_marks_the_command_failed(self):
        for bad in ("'Foo' is not a recognized command.", "No matching objects found.", "Usage: Set <propertyName> <value>"):
            self.assertTrue(legionsetup.ERROR.search(bad), bad)
        for good in ("Property has been set.", "1 object generated in 0.00 seconds.", "Skills.Mining.Base = 100"):
            self.assertFalse(legionsetup.ERROR.search(good), good)


if __name__ == "__main__":
    unittest.main()
