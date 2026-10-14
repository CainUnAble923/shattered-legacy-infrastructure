"""cc-P102: the nightly run's own facts. Standard library only; no shard, no docker, no mail.

    cd D:\\ShatteredLegacy\\tools\\playtest-bot
    python -m unittest discover -s tests

Covers: the work check (none, some, -Force, re-opened, code changed), each email's text (before, after, crash), the
14-day archive (copied, verified, then removed; a failed verify or a missing root removes nothing; report and results
stay), the runner's no-work exit and crash email, and the installer's -DryRun.
"""
import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
BOT = os.path.dirname(HERE)
sys.path.insert(0, BOT)
from uobot import work, notify, archive, report  # noqa: E402

PS = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File"]


def git(repo, *args):
    return subprocess.run(["git", "-C", repo] + list(args), capture_output=True, text=True, check=True).stdout.strip()


def write_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f)


def rmtree(path):
    """git's object files are read-only on Windows; clear the flag and retry, so no fixture is left in %TEMP%."""
    def again(fn, p, _):
        os.chmod(p, stat.S_IWRITE)
        fn(p)
    shutil.rmtree(path, onerror=again)


def item(iid, status="open", reopened=""):
    return {"id": iid, "status": status, "resultAt": "", "reopenedAt": reopened}


class Fixture(unittest.TestCase):
    """A throwaway git repo with server/ code, a play-tests.json and a bot-runs folder."""

    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p102-")
        self.repo = os.path.join(self.tmp, "repo")
        os.makedirs(os.path.join(self.repo, "server"))
        git(self.tmp, "init", "-q", self.repo)
        git(self.repo, "config", "user.email", "t@example.invalid")
        git(self.repo, "config", "user.name", "t")
        self.commit("server/a.cs", "one")
        self.sha1 = git(self.repo, "rev-parse", "HEAD")
        self.runs = os.path.join(self.tmp, "bot-runs")
        os.makedirs(self.runs)
        self.plays = os.path.join(self.tmp, "play-tests.json")

    def tearDown(self):
        rmtree(self.tmp)

    def commit(self, rel, text):
        p = os.path.join(self.repo, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w") as f:
            f.write(text)
        git(self.repo, "add", "-A")
        git(self.repo, "commit", "-q", "-m", rel)
        return git(self.repo, "rev-parse", "HEAD")

    def plays_with(self, *items):
        write_json(self.plays, {"schema": 1, "items": list(items)})

    def bot_run(self, stamp, sha, ids, result="PASS", code=None):
        d = os.path.join(self.runs, stamp)
        world = {"code": code or ("working tree at " + sha[:7])}
        if code is None:
            world["commitSha"] = sha
        write_json(os.path.join(d, "world.json"), world)
        write_json(os.path.join(d, "results.json"), {"checks": [
            {"id": i, "result": result, "passes": 3, "runs": [{"run": 1, "seconds": 10.0}]} for i in ids]})
        return d

    def check(self, target=None, force=False, ids=("A-1", "A-2", "B-1")):
        return work.find_work(self.repo, self.plays, self.runs, target or "HEAD", list(ids), force=force)


class WorkCheck(Fixture):
    def test_none_when_every_open_item_ran_on_this_code(self):
        self.plays_with(item("A-1"), item("A-2"), item("Z-9"), item("B-1", status="pass"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1", "A-2"])
        w = self.check()
        self.assertFalse(w["work"], w)
        self.assertEqual([], w["items"])

    def test_items_without_a_check_and_closed_items_never_count(self):
        self.plays_with(item("Z-9"), item("A-1", status="fail"), item("A-2", status="later"))
        self.assertFalse(self.check()["work"])

    def test_a_new_item_counts(self):
        self.plays_with(item("A-1"), item("B-1"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"])
        w = self.check()
        self.assertTrue(w["work"])
        self.assertEqual(["B-1"], [i["id"] for i in w["items"]])
        self.assertIn("no bot run yet", w["items"][0]["reason"])

    def test_a_run_on_older_code_counts_but_a_docs_only_commit_does_not(self):
        self.plays_with(item("A-1"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"])
        self.commit("docs/readme.md", "words")
        self.assertFalse(self.check()["work"], "a commit outside the code under test is the same code")
        self.commit("server/a.cs", "two")
        w = self.check()
        self.assertEqual(["A-1"], [i["id"] for i in w["items"]])
        self.assertIn("code changed since " + self.sha1[:7], w["items"][0]["reason"])

    def test_a_change_to_the_checks_themselves_counts(self):
        self.plays_with(item("A-1"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"])
        self.commit("tools/playtest-bot/uobot/checks.py", "x")
        self.assertTrue(self.check()["work"])

    def test_a_reopened_item_counts_when_reopened_after_its_last_run(self):
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"])
        ran = datetime(2026, 10, 13, 1, 30).astimezone(timezone.utc)
        before = (ran - timedelta(hours=2)).strftime("%Y-%m-%dT%H:%M:%SZ")
        after = (ran + timedelta(hours=2)).strftime("%Y-%m-%dT%H:%M:%SZ")
        self.plays_with(item("A-1", reopened=before))
        self.assertFalse(self.check()["work"])
        self.plays_with(item("A-1", reopened=after))
        w = self.check()
        self.assertEqual(["A-1"], [i["id"] for i in w["items"]])
        self.assertIn("re-opened", w["items"][0]["reason"])

    def test_an_error_result_is_not_a_run(self):
        self.plays_with(item("A-1"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"], result="ERROR")
        self.assertTrue(self.check()["work"])

    def test_old_runs_without_commitSha_are_read_from_their_code_line(self):
        self.plays_with(item("A-1"), item("A-2"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"], code="commit %s (-Commit, a worktree in x)" % self.sha1[:7])
        self.bot_run("2026-10-14-0130", self.sha1, ["A-2"], code="working tree at %s" % self.sha1[:7])
        self.assertFalse(self.check()["work"])

    def test_force_runs_with_no_work_and_says_so(self):
        self.plays_with(item("A-1"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"])
        w = self.check(force=True)
        self.assertTrue(w["run"])
        self.assertFalse(w["work"])
        self.assertTrue(w["forced"])

    def test_a_target_commit_is_resolved_and_reported(self):
        self.plays_with(item("A-1"))
        sha2 = self.commit("server/a.cs", "two")
        w = self.check(target=self.sha1)
        self.assertEqual(self.sha1, w["sha"])
        self.assertEqual(self.sha1[:7], w["short"])
        self.assertNotEqual(sha2, w["sha"])

    def test_the_estimate_uses_the_last_seconds_and_the_soak(self):
        self.plays_with(item("A-1"), item("B-1"))
        self.bot_run("2026-10-13-0130", self.sha1, ["A-1"])
        mins = work.estimate_minutes(self.runs, ["A-1", "B-1"], runs=3, soak_minutes=120)
        # build and boot 25, soak 120, A-1 3 x 10 s, B-1 unknown 3 x 60 s: 145 + 3.5 = 148.5, rounded up
        self.assertEqual(149, mins)


class Emails(unittest.TestCase):
    def test_before(self):
        subject, body = notify.before_text("abc1234", [{"id": "A-1", "reason": "no bot run yet"},
                                                        {"id": "B-1", "reason": "code changed since 1111111"}],
                                           checks=20, minutes=150, task="SL overnight play tests")
        self.assertEqual("Overnight play tests starting: 2 checks on abc1234", subject)
        self.assertIn("A-1: no bot run yet", body)
        self.assertIn("B-1: code changed since 1111111", body)
        self.assertIn("about 2 h 30 min", body)
        self.assertIn('schtasks /End /TN "SL overnight play tests"', body)
        self.assertIn("docker compose -p sl-uo-bot down -t 60", body)
        self.assertIn("all 20 checks", body)

    def test_before_when_forced(self):
        subject, body = notify.before_text("abc1234", [], checks=20, minutes=30, task="t", forced=True)
        self.assertEqual("Overnight play tests starting: 0 checks on abc1234 (forced)", subject)
        self.assertIn("-Force", body)

    def test_crash(self):
        lines = ["line %d" % i for i in range(100)]
        subject, body = notify.crash_text("abc1234", "the scratch shard did not start listening", lines,
                                          r"D:\runs\x", archive_note="Archive: nothing older than 14 days.")
        self.assertEqual("Overnight play tests FAILED on abc1234: the scratch shard did not start listening", subject)
        self.assertIn("line 60", body)
        self.assertIn("line 99", body)
        self.assertNotIn("line 59\n", body + "\n")
        self.assertIn("last 40 lines of run.log", body)
        self.assertIn(r"D:\runs\x", body)
        self.assertIn("Archive: nothing older than 14 days.", body)

    def test_after_keeps_the_summary_and_adds_the_report_path_and_the_archive_line(self):
        d = tempfile.mkdtemp(prefix="p102-r-")
        try:
            write_json(os.path.join(d, "results.json"), {"checks": [
                {"id": "A-1", "title": "a", "kind": "BOT", "result": "PASS", "passes": 3, "runs": [
                    {"run": 1, "verdict": "pass", "evidence": "ok", "transcript": "transcripts/A-1-run1.txt"}]}]})
            report.main(["--out", d, "--archive-note", "Archive: 2 runs moved to iCloud Drive, verified."])
            with open(os.path.join(d, "email.txt"), encoding="ascii") as f:
                text = f.read()
            self.assertTrue(text.startswith("Overnight play tests: 1 pass, 0 fail, 0 flaky\n"))
            self.assertIn("Report: " + os.path.join(d, "report.md"), text)
            self.assertIn("Archive: 2 runs moved to iCloud Drive, verified.", text)
        finally:
            rmtree(d)


class Archive(unittest.TestCase):
    NOW = datetime(2026, 10, 30, 2, 0)

    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p102-a-")
        self.runs = os.path.join(self.tmp, "bot-runs")
        self.drive = os.path.join(self.tmp, "iCloudDrive")
        os.makedirs(self.drive)
        self.old = self.run_folder("2026-10-13-1428")       # 16 days old
        self.new = self.run_folder("2026-10-20-0130")       # 10 days old

    def tearDown(self):
        rmtree(self.tmp)

    def run_folder(self, stamp):
        d = os.path.join(self.runs, stamp)
        os.makedirs(os.path.join(d, "transcripts", "sub"))
        for name, text in (("report.md", "r"), ("results.json", "{}")):
            with open(os.path.join(d, name), "w") as f:
                f.write(text)
        for name in ("A-1-run1.txt", "A-1-run2.txt", os.path.join("sub", "deep.txt")):
            with open(os.path.join(d, "transcripts", name), "w") as f:
                f.write(stamp + name * 50)
        return d

    def go(self, **kw):
        return archive.archive_old(self.runs, self.drive, r"Shattered Legacy\bot-runs", 14, now=self.NOW, **kw)

    def dest(self, stamp):
        return os.path.join(self.drive, "Shattered Legacy", "bot-runs", stamp, "transcripts")

    def test_old_transcripts_are_copied_verified_then_removed(self):
        r = self.go()
        self.assertEqual(["2026-10-13-1428"], r["archived"])
        self.assertEqual([], r["problems"])
        self.assertFalse(os.path.exists(os.path.join(self.old, "transcripts")))
        self.assertTrue(os.path.isfile(os.path.join(self.dest("2026-10-13-1428"), "sub", "deep.txt")))
        self.assertEqual(3, sum(len(f) for _, _, f in os.walk(self.dest("2026-10-13-1428"))))
        for name in ("report.md", "results.json"):
            self.assertTrue(os.path.isfile(os.path.join(self.old, name)), name)
        with open(os.path.join(self.old, "transcripts-archived.json"), encoding="utf-8") as f:
            self.assertEqual(self.dest("2026-10-13-1428"), json.load(f)["to"])
        self.assertTrue(os.path.isdir(os.path.join(self.new, "transcripts")), "10 days old stays")
        self.assertIn("1 run", r["note"])

    def test_a_failed_verify_removes_nothing(self):
        def corrupt(src, dst):
            shutil.copy2(src, dst)
            if dst.endswith("deep.txt"):
                with open(dst, "a") as f:
                    f.write("!")
        r = self.go(copy=corrupt)
        self.assertEqual([], r["archived"])
        self.assertTrue(r["problems"])
        self.assertTrue(os.path.isfile(os.path.join(self.old, "transcripts", "sub", "deep.txt")))
        self.assertIn("NOT removed", r["note"])

    def test_a_same_size_change_is_caught_by_the_hash(self):
        def flip(src, dst):
            shutil.copy2(src, dst)
            if dst.endswith("A-1-run1.txt"):
                with open(dst, "r+b") as f:
                    f.write(b"X")
        r = self.go(copy=flip)
        self.assertEqual([], r["archived"])
        self.assertTrue(any("hash" in p for p in r["problems"]), r["problems"])
        self.assertTrue(os.path.isdir(os.path.join(self.old, "transcripts")))

    def test_a_missing_root_removes_nothing(self):
        shutil.rmtree(self.drive)
        r = self.go()
        self.assertEqual([], r["archived"])
        self.assertTrue(any("missing" in p for p in r["problems"]), r["problems"])
        self.assertTrue(os.path.isdir(os.path.join(self.old, "transcripts")))
        self.assertFalse(os.path.exists(self.drive), "the root is never created")
        self.assertIn("NOT removed", r["note"])

    def test_nothing_old_says_so(self):
        shutil.rmtree(self.old)
        r = self.go()
        self.assertEqual([], r["archived"])
        self.assertEqual([], r["problems"])
        self.assertIn("nothing older than 14 days", r["note"])


class Scripts(unittest.TestCase):
    """The PowerShell side, run for real against fixture folders. Nothing here reaches docker's bot project or mail."""

    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="p102-s-")

    def tearDown(self):
        rmtree(self.tmp)

    def run_ps(self, script, *args):
        return subprocess.run(PS + [os.path.join(BOT, script)] + list(args), capture_output=True, text=True,
                              timeout=300)

    def test_installer_dry_run_prints_the_call_and_registers_nothing(self):
        p = self.run_ps("Install-OvernightTask.ps1", "-DryRun")
        self.assertEqual(0, p.returncode, p.stdout + p.stderr)
        out = p.stdout
        self.assertIn("DRY RUN", out)
        self.assertIn("Register-ScheduledTask", out)
        self.assertIn("SL overnight play tests", out)
        self.assertIn("01:30", out)
        self.assertIn("-WakeToRun", out)
        self.assertIn("Run-OvernightPlaytests.ps1", out)
        self.assertIn("\n  $trigger = New-ScheduledTaskTrigger -Daily -At '01:30'\n", out, "one call per line")
        q = subprocess.run(["schtasks", "/Query", "/TN", "SL overnight play tests"], capture_output=True, text=True)
        self.assertNotEqual(0, q.returncode, "the dry run registered the task")

    def test_installer_uninstall_dry_run(self):
        p = self.run_ps("Install-OvernightTask.ps1", "-Uninstall", "-DryRun")
        self.assertEqual(0, p.returncode, p.stdout + p.stderr)
        self.assertIn("Unregister-ScheduledTask", p.stdout)

    def fixture_plays(self, status):
        plays = os.path.join(self.tmp, "play-tests.json")
        write_json(plays, {"schema": 1, "items": [item("T-047", status=status)]})
        return plays

    def test_runner_with_no_work_writes_one_line_and_exits_0(self):
        runs = os.path.join(self.tmp, "runs")
        # A bot root Assert-Rule1 refuses, so that if the work check ever wrongly finds work the run stops before any
        # build or container. (The first fail-first run of this test, against the pre-P102 runner, had no such guard:
        # the old script ignored the unknown parameters and started a real run. cc-P102 notes.)
        botroot = os.path.join(self.tmp, "shard-migration-fixture", "bot")
        outbox = os.path.join(self.tmp, "outbox")
        p = self.run_ps("Run-OvernightPlaytests.ps1", "-PlayTests", self.fixture_plays("pass"), "-RunsRoot", runs,
                        "-BotRoot", botroot, "-MailOutbox", outbox)
        self.assertEqual(0, p.returncode, p.stdout + p.stderr)
        with open(os.path.join(botroot, "overnight.log"), encoding="ascii") as f:
            lines = f.read().splitlines()
        self.assertEqual(1, len(lines), lines)
        self.assertIn("no work", lines[0])
        self.assertFalse(os.path.exists(runs) and os.listdir(runs), "no run folder on a night with no work")
        self.assertFalse(os.path.exists(outbox) and os.listdir(outbox), "no email on a night with no work")

    def test_runner_crash_sends_the_after_email_with_the_log_tail(self):
        runs = os.path.join(self.tmp, "runs")
        # A bot root inside a repo's name: Assert-Rule1 refuses it, after the before email and before any build,
        # worktree or container, so the run fails the way a crash does without touching docker or git.
        botroot = os.path.join(self.tmp, "shard-migration-fixture", "bot")
        outbox = os.path.join(self.tmp, "outbox")
        drive = os.path.join(self.tmp, "nocloud")
        old = (datetime.now() - timedelta(days=30)).strftime("%Y-%m-%d-%H%M")
        old_tr = os.path.join(runs, old, "transcripts")
        os.makedirs(old_tr)
        with open(os.path.join(old_tr, "A-1-run1.txt"), "w") as f:
            f.write("old")
        p = self.run_ps("Run-OvernightPlaytests.ps1", "-PlayTests", self.fixture_plays("open"), "-RunsRoot", runs,
                        "-BotRoot", botroot, "-MailOutbox", outbox, "-ArchiveDrive", drive)
        self.assertNotEqual(0, p.returncode, p.stdout + p.stderr)
        mails = sorted(os.listdir(outbox))
        self.assertEqual(2, len(mails), mails)
        with open(os.path.join(outbox, mails[0]), encoding="ascii") as f:
            self.assertTrue(f.readline().startswith("Overnight play tests starting:"))
        with open(os.path.join(outbox, mails[1]), encoding="ascii") as f:
            crash = f.read()
        self.assertTrue(crash.startswith("Overnight play tests FAILED"), crash[:200])
        self.assertIn("Rule 1 refused", crash)
        self.assertIn("last 40 lines of run.log", crash)
        self.assertIn("missing", crash, "the archive root is missing and the email says so")
        self.assertTrue(os.path.isfile(os.path.join(old_tr, "A-1-run1.txt")), "a missing root removes nothing")


if __name__ == "__main__":
    unittest.main()
