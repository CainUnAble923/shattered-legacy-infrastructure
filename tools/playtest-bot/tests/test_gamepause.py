"""cc-P819: the game pause, run for real with a fake game (Chase 11-07: "pause bot play testing when im playing other
games"). Standard library only; no shard, no docker, no mail.

    cd D:\\ShatteredLegacy\\tools\\playtest-bot
    python -m unittest tests.test_gamepause      (or: python -m unittest discover -s tests)

The fake game is a copy of ping.exe renamed FakeGame.exe in a temp ...\\steamapps\\common\\FakeGame folder (harmless: it
pings loopback), started and stopped by the facts. The check is a temp copy of the Golem kit's Test-GameRunning.ps1 and
the files it reads (GamePause.ps1, GolemPower.ps1, Golems.ps1) with its own settings.json naming this PC as the game PC,
so the real kit's settings are never read or changed. Without the kit on this PC the facts are skipped.

Covers: the overnight runner waits while a game runs and goes on when it closes (overnight.log shows the wait and the
resume), skips the night when the game outlasts -NightEnd, and does not wait with -NoGameWait; uobot.run's checks wait
for a game started mid-run (the clients closed first) and resume; the report shows the waits and the skipped checks.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from datetime import datetime, timedelta

HERE = os.path.dirname(os.path.abspath(__file__))
BOT = os.path.dirname(HERE)
sys.path.insert(0, BOT)
sys.path.insert(0, HERE)
from uobot import gamepause, report  # noqa: E402
from uobot import run as R  # noqa: E402
from test_overnight import PS, item, rmtree, write_json  # noqa: E402

KIT = r"D:\UO\golem"
KIT_FILES = ["Test-GameRunning.ps1", "GamePause.ps1", "GolemPower.ps1", "Golems.ps1"]


def kit_here():
    return all(os.path.isfile(os.path.join(KIT, f)) for f in KIT_FILES)


class FakeWorld(object):
    """A temp kit (the check) and a fake game to start and stop."""

    def __init__(self, tmp):
        self.kit = os.path.join(tmp, "kit")
        os.makedirs(self.kit)
        for f in KIT_FILES:
            shutil.copy(os.path.join(KIT, f), self.kit)
        write_json(os.path.join(self.kit, "settings.json"), {"gamePause": True, "gamePausePc": os.environ.get("COMPUTERNAME", "")})
        self.check = os.path.join(self.kit, "Test-GameRunning.ps1")
        d = os.path.join(tmp, "Steam", "steamapps", "common", "FakeGame")
        os.makedirs(d)
        self.exe = os.path.join(d, "FakeGame.exe")
        shutil.copy(os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "PING.EXE"), self.exe)
        self.proc = None

    def start(self):
        self.proc = subprocess.Popen([self.exe, "-n", "900", "127.0.0.1"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                     creationflags=0x08000000)  # CREATE_NO_WINDOW
        time.sleep(0.8)

    def stop(self):
        if self.proc and self.proc.poll() is None:
            self.proc.kill()
            self.proc.wait(10)


@unittest.skipUnless(kit_here(), "the Golem kit's game check is not on this PC")
class GamePause(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p819-")
        self.w = FakeWorld(self.tmp)

    def tearDown(self):
        self.w.stop()
        rmtree(self.tmp)

    # -- the shared check, from the bot's side
    def test_the_check_sees_the_fake_game_and_not_after_it_closes(self):
        self.assertEqual((False, "no game running"), gamepause.now(self.w.check))
        self.w.start()
        self.assertEqual((True, "waiting: Chase is playing FakeGame"), gamepause.now(self.w.check))
        self.w.stop()
        self.assertFalse(gamepause.now(self.w.check)[0])
        self.assertFalse(gamepause.now("none")[0], "'none' turns the look off")

    # -- the overnight runner
    def plays(self):
        p = os.path.join(self.tmp, "play-tests.json")
        write_json(p, {"schema": 1, "items": [item("T-047", status="open")]})
        return p

    def runner(self, *extra):
        # A bot root inside a repo's name: Assert-Rule1 refuses it right after the wait, so a run that goes on stops
        # before any build, worktree or container, and never touches docker or git (test_overnight's crash fact's way).
        self.botroot = os.path.join(self.tmp, "shard-migration-fixture", "bot")
        self.runs = os.path.join(self.tmp, "runs")
        args = ["-PlayTests", self.plays(), "-RunsRoot", self.runs, "-BotRoot", self.botroot, "-MailOutbox",
                os.path.join(self.tmp, "outbox"), "-ArchiveDrive", os.path.join(self.tmp, "nocloud"),
                "-GameCheck", self.w.check, "-GameIntervalSeconds", "2"] + list(extra)
        return subprocess.Popen(PS + [os.path.join(BOT, "Run-OvernightPlaytests.ps1")] + args, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True)

    def night_log(self):
        p = os.path.join(self.botroot, "overnight.log")
        if not os.path.isfile(p):
            return ""
        with open(p, encoding="ascii") as f:
            return f.read()

    def ran(self):
        return os.path.isdir(self.runs) and bool(os.listdir(self.runs))

    def test_the_runner_waits_while_a_game_runs_and_goes_on_when_it_closes(self):
        self.w.start()
        p = self.runner()
        deadline = time.time() + 40
        while "waiting: Chase is playing FakeGame" not in self.night_log() and time.time() < deadline:
            time.sleep(0.5)
        self.assertIn("game pause: waiting: Chase is playing FakeGame; the overnight play tests waits", self.night_log())
        time.sleep(5)
        self.assertIsNone(p.poll(), "the runner went on while the game ran")
        self.assertFalse(self.ran(), "a run folder was made while the game ran")
        self.w.stop()
        out = p.communicate(timeout=120)[0]
        self.assertIn("game pause: the game closed: the overnight play tests goes on", self.night_log(), out)
        self.assertTrue(self.ran(), "no run after the game closed: " + out)

    def test_the_runner_skips_the_night_when_the_game_outlasts_it(self):
        self.w.start()
        end = (datetime.now() + timedelta(minutes=1)).strftime("%H:%M")
        p = self.runner("-NightEnd", end)
        out = p.communicate(timeout=200)[0]
        self.assertEqual(0, p.returncode, out)
        log = self.night_log()
        self.assertIn("game pause: still playing FakeGame at %s: the overnight play tests is skipped" % end, log)
        self.assertIn("skipped on ", log)
        self.assertIn("a game was running until %s; nothing built, no email" % end, log)
        self.assertFalse(self.ran(), "a run folder on a skipped night")
        self.assertFalse(os.path.isdir(os.path.join(self.tmp, "outbox")), "an email on a skipped night")

    def test_no_game_wait_runs_whatever_is_played(self):
        self.w.start()
        p = self.runner("-NoGameWait")
        out = p.communicate(timeout=120)[0]
        self.assertNotIn("game pause", self.night_log())
        self.assertTrue(self.ran(), out)

    # -- uobot.run: a game started mid-run
    def test_a_check_waits_for_a_game_started_mid_run_and_resumes(self):
        class Chk(object):
            def __init__(self, cid, fn):
                self.id, self.title, self.kind, self.left_for_chase, self.fn = cid, cid, "bot", "", fn

        class Ctx(object):
            def __init__(self, out):
                self.out = out
                self.closed = 0
                self.tr = None

            def use_transcript(self, tr):
                self.tr = tr

            def close_clients(self):
                self.closed += 1

        order = []

        def first(ctx):
            order.append("first")
            self.w.start()                     # Chase starts a game while the first check runs
            threading.Timer(6, self.w.stop).start()
            return True, "ok"

        def second(ctx):
            order.append(("second", self.w.proc.poll() is not None))
            return True, "ok"

        out = os.path.join(self.tmp, "run")
        os.makedirs(os.path.join(out, "transcripts"))
        ctx = Ctx(out)
        pause = R.GamePause(self.w.check, (datetime.now() + timedelta(hours=2)).strftime("%H:%M"), interval=2)
        results = R.run_checks(ctx, [Chk("A-1", first), Chk("A-2", second)], 1, pause)
        self.assertEqual(["first", ("second", True)], order, "the second check ran while the game was still running")
        self.assertEqual(["PASS", "PASS"], [r["result"] for r in results])
        self.assertEqual(1, ctx.closed, "the clients were not closed before the wait")
        self.assertEqual(1, len(pause.waits))
        self.assertEqual("resumed", pause.waits[0]["outcome"])
        self.assertEqual("waiting: Chase is playing FakeGame", pause.waits[0]["line"])

    def test_checks_left_at_the_nights_end_are_skipped_and_the_report_says_so(self):
        pause = R.GamePause(self.w.check, "06:30")
        pause.over = True
        pause.waits.append({"at": "2026-11-08T06:30:00", "where": "A-1", "line": "waiting: Chase is playing FakeGame", "outcome": "skipped"})

        class Chk(object):
            id, title, kind, left_for_chase = "A-1", "a check", "bot", ""

            @staticmethod
            def fn(ctx):
                raise AssertionError("a check ran after the night's end")

        out = os.path.join(self.tmp, "run")
        os.makedirs(os.path.join(out, "transcripts"))
        results = R.run_checks(type("C", (), {"out": out})(), [Chk], 3, pause)
        self.assertEqual("SKIPPED", results[0]["result"])
        soak = R.merge_soak(120, [])
        with open(os.path.join(out, "results.json"), "w", encoding="utf-8") as f:
            json.dump({"checks": results, "soak": soak, "gameWaits": pause.waits}, f)
        report.main(["--out", out])
        md = open(os.path.join(out, "report.md"), encoding="utf-8").read()
        self.assertIn("1 skipped (a game was running)", md)
        self.assertIn("waiting: Chase is playing FakeGame; skipped", md)
        self.assertIn("Skipped: a game was running until the night's end", md)
        mail = open(os.path.join(out, "email.txt"), encoding="ascii").read()
        self.assertIn("Game pause: waiting: Chase is playing FakeGame before A-1 (skipped).", mail)

    def test_a_split_soak_reads_as_one(self):
        a = {"minutes": 120, "stoppedFor": "waiting: Chase is playing FakeGame", "events": {"walker_steps": 5}, "samples": [1],
             "errors": [], "loud": [], "loudCount": 1, "memStartMB": 100, "memEndMB": 110, "memMaxMB": 120}
        b = {"minutes": 80, "stoppedFor": "", "events": {"walker_steps": 7, "miner_digs": 2}, "samples": [2], "errors": ["x"],
             "loud": ["l"], "loudCount": 2, "memStartMB": 90, "memEndMB": 95, "memMaxMB": 130}
        m = R.merge_soak(120, [a, b])
        self.assertEqual({"walker_steps": 12, "miner_digs": 2}, m["events"])
        self.assertEqual((100, 95, 130, 3, 2), (m["memStartMB"], m["memEndMB"], m["memMaxMB"], m["loudCount"], m["parts"]))
        self.assertEqual(["waiting: Chase is playing FakeGame"], m["stoppedFor"])


if __name__ == "__main__":
    unittest.main()
