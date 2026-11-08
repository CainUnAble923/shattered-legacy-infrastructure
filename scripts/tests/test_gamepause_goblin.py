"""cc-P819: the Play Test Goblin waits while Chase plays another game on the game PC, run for real with a fake game.

    cd D:\\ShatteredLegacy\\scripts
    python -m unittest tests.test_gamepause_goblin

The fake game is a copy of ping.exe renamed FakeGame.exe in a temp ...\\steamapps\\common\\FakeGame folder; the check is
a temp copy of the Golem kit's Test-GameRunning.ps1 (and what it reads) with its own settings.json naming this PC as
the game PC. Without the kit on this PC the facts are skipped. -DryRun throughout: no task file changes, no email.
"""
import json
import os
import shutil
import subprocess
import tempfile
import threading
import time
import unittest
from datetime import datetime, timedelta

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)
KIT = r"D:\UO\golem"
KIT_FILES = ["Test-GameRunning.ps1", "GamePause.ps1", "GolemPower.ps1", "Golems.ps1"]
PS = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File"]


@unittest.skipUnless(all(os.path.isfile(os.path.join(KIT, f)) for f in KIT_FILES), "the Golem kit's game check is not on this PC")
class GoblinGamePause(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p819-g-")
        kit = os.path.join(self.tmp, "kit")
        os.makedirs(kit)
        for f in KIT_FILES:
            shutil.copy(os.path.join(KIT, f), kit)
        with open(os.path.join(kit, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"gamePause": True, "gamePausePc": os.environ.get("COMPUTERNAME", "")}, f)
        self.check = os.path.join(kit, "Test-GameRunning.ps1")
        d = os.path.join(self.tmp, "Steam", "steamapps", "common", "FakeGame")
        os.makedirs(d)
        self.exe = os.path.join(d, "FakeGame.exe")
        shutil.copy(os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "PING.EXE"), self.exe)
        self.game = subprocess.Popen([self.exe, "-n", "900", "127.0.0.1"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                     creationflags=0x08000000)
        time.sleep(0.8)
        self.log = os.path.join(self.tmp, "goblin.log")
        os.makedirs(os.path.join(self.tmp, "playtest"))

    def tearDown(self):
        if self.game.poll() is None:
            self.game.kill()
            self.game.wait(10)
        shutil.rmtree(self.tmp, ignore_errors=True)

    def goblin(self, *extra):
        args = ["-DryRun", "-PlaytestDir", os.path.join(self.tmp, "playtest"), "-Notes", os.path.join(self.tmp, "notes"),
                "-Log", self.log, "-GameCheck", self.check, "-GameIntervalSeconds", "2"] + list(extra)
        return subprocess.run(PS + [os.path.join(SCRIPTS, "Run-PlayTestGoblin.ps1")] + args, capture_output=True, text=True, timeout=240)

    def read_log(self):
        with open(self.log, encoding="ascii") as f:
            return f.read()

    def test_it_waits_and_goes_on_when_the_game_closes(self):
        threading.Timer(6, self.game.kill).start()
        p = self.goblin()
        log = self.read_log()
        self.assertIn("game pause: waiting: Chase is playing FakeGame; the Play Test Goblin waits", log, p.stdout + p.stderr)
        self.assertIn("game pause: the game closed: the Play Test Goblin goes on", log)
        self.assertIn("goblin: ", log, "the Goblin did not run after the game closed")
        self.assertLess(log.index("the game closed"), log.index("goblin: "))

    def test_it_skips_the_run_when_the_game_outlasts_the_night(self):
        end = (datetime.now() + timedelta(minutes=1)).strftime("%H:%M")
        p = self.goblin("-NightEnd", end)
        self.assertEqual(0, p.returncode, p.stdout + p.stderr)
        log = self.read_log()
        self.assertIn("Skipped this run: a game was running until %s." % end, log)
        self.assertNotIn("goblin: ", log, "the Goblin ran while the game was running")

    def test_no_game_wait_runs_at_once(self):
        p = self.goblin("-NoGameWait")
        log = self.read_log()
        self.assertNotIn("game pause", log)
        self.assertIn("goblin: ", log, p.stdout + p.stderr)


if __name__ == "__main__":
    unittest.main()
