"""The two emails the nightly run adds to the morning summary (cc-P102 Part B): "starting" and "it crashed".

    python -m uobot.notify before --work <work.json> --task <task name> --out <file>
    python -m uobot.notify crash --short <sha> --reason <text> --log <run.log> --run-dir <dir> [--archive-note <text>] --out <file>

Each writes a mail text in email.txt's shape (subject, blank line, body), which Send-BotReport.ps1 -TextFile sends.
The after email on a finished run is still report.py's email.txt. ASCII only, like the rest of the mail path.
"""
import argparse
import json
import sys

TAIL = 40


def _duration(minutes):
    h, m = divmod(int(minutes), 60)
    return ("%d h %d min" % (h, m)) if h else ("%d min" % m)


def before_text(short, items, checks, minutes, task, forced=False):
    subject = "Overnight play tests starting: %d checks on %s%s" % (len(items), short, " (forced)" if forced else "")
    body = ["The overnight play-test bot is starting on %s." % short, ""]
    if items:
        body.append("Play-test items with work tonight (open, with a bot check that has not run on this code):")
        body += ["  %s: %s" % (i["id"], i["reason"]) for i in items]
    else:
        body.append("No item has work: this run was started with -Force.")
    body += ["",
             "It runs all %d checks (some hand results to others), so the report covers more than the list above." % checks,
             "Expected length: about %s (build, boot, checks, soak)." % _duration(minutes),
             "",
             "To stop it:",
             '  schtasks /End /TN "%s"' % task,
             "  docker compose -p sl-uo-bot down -t 60",
             "The first stops the script; the second stops the scratch shard (sl-modernuo-bot only, never live or test).",
             "A summary email follows when it finishes."]
    return subject, "\n".join(body)


def crash_text(short, reason, log_lines, run_dir, archive_note=""):
    subject = "Overnight play tests FAILED on %s: %s" % (short, reason)
    tail = list(log_lines)[-TAIL:]
    body = ["The overnight play-test run stopped before it finished.", "",
            "Reason: %s" % reason, "Run folder: %s" % run_dir, ""]
    if archive_note:
        body += [archive_note, ""]
    body += ["The last %d lines of run.log:" % TAIL, ""] + tail
    return subject, "\n".join(body)


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
