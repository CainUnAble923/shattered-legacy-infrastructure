"""The two emails the nightly run adds to the morning summary (cc-P102 Part B): "starting" and "it crashed".

    python -m uobot.notify before --work <work.json> --task <task name> --out <file>
    python -m uobot.notify crash --short <sha> --reason <text> --log <run.log> --run-dir <dir> [--archive-note <text>] --out <file>

Each writes a mail text in email.txt's shape (subject, blank line, body), which Send-BotReport.ps1 -TextFile sends.
The after email on a finished run is still report.py's email.txt. ASCII only, like the rest of the mail path.
"""
import argparse
import json
import sys

from uobot import mailshape

TAIL = 40


def _duration(minutes):
    h, m = divmod(int(minutes), 60)
    return ("%d h %d min" % (h, m)) if h else ("%d min" % m)


def before_text(short, items, checks, minutes, task, forced=False):
    # cc-P157: the Golem emails' shape: what happens tonight in words, What to do, then the commit and the commands.
    n = len(items)
    subject = "Overnight play tests starting: " + ("a forced run" if forced else "%d item%s to check" % (n, "" if n == 1 else "s"))
    if items:
        lead = ("The overnight test bot is starting its play tests on the scratch shard. It checks %d play-test item%s "
                "with new work tonight and takes about %s." % (n, "" if n == 1 else "s", _duration(minutes)))
    else:
        lead = ("The overnight test bot is starting its play tests on the scratch shard because it was told to run, though "
                "no item has new work. It takes about %s." % _duration(minutes))
    details = ["Code under test: %s." % short, ""]
    if items:
        details.append("Play-test items with work tonight (open, with a bot check that has not run on this code):")
        details += ["  %s: %s" % (i["id"], i["reason"]) for i in items]
    else:
        details.append("No item has work: this run was started with -Force.")
    details += ["",
                "It runs all %d checks (some hand results to others), so the report covers more than the list above." % checks,
                "Expected length: about %s (build, boot, checks, soak)." % _duration(minutes),
                "",
                "To stop it:",
                '  schtasks /End /TN "%s"' % task,
                "  docker compose -p sl-uo-bot down -t 60",
                "The first stops the script; the second stops the scratch shard (sl-modernuo-bot only, never live or test)."]
    return subject, mailshape.shape(lead, "Nothing to do. The results come by email when it finishes.", details="\n".join(details))


def crash_text(short, reason, log_lines, run_dir, archive_note=""):
    subject = "Overnight play tests stopped early: %s" % reason
    tail = list(log_lines)[-TAIL:]
    lead = "The overnight test bot stopped before it finished: %s. Nothing was marked on the play-test page." % reason
    details = ["Code under test: %s." % short, "Run folder: %s" % run_dir, ""]
    if archive_note:
        details += [archive_note, ""]
    details += ["The last %d lines of run.log:" % TAIL, ""] + tail
    return subject, mailshape.shape(lead, "if it happens again tomorrow night, ask CC on the game PC to look at the run log "
                                          "(Details say where it is).", details="\n".join(details))


def write(path, subject, body):
    with open(path, "w", encoding="ascii", errors="replace", newline="\n") as f:
        f.write(subject + "\n\n" + body + "\n")


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["before", "crash"])
    ap.add_argument("--out", required=True)
    ap.add_argument("--work", default="")
    ap.add_argument("--task", default="SL overnight play tests")
    ap.add_argument("--short", default="?")
    ap.add_argument("--reason", default="unknown")
    ap.add_argument("--log", default="")
    ap.add_argument("--run-dir", default="")
    ap.add_argument("--archive-note", default="")
    a = ap.parse_args(argv)
    if a.kind == "before":
        with open(a.work, "r", encoding="utf-8") as f:
            w = json.load(f)
        s, b = before_text(w["short"], w["items"], w["checks"], w.get("minutes", 0), a.task, forced=w.get("forced"))
    else:
        lines = []
        try:
            with open(a.log, "r", encoding="ascii", errors="replace") as f:
                lines = f.read().splitlines()
        except OSError:
            lines = ["(no run.log)"]
        s, b = crash_text(a.short, a.reason, lines, a.run_dir, a.archive_note)
    write(a.out, s, b)
    print(s)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
