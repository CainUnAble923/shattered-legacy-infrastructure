"""What tonight's bot run will attempt, for the play-test page's robot icons (cc-P112).

    python -m uobot.tonight --repo D:\\ShatteredLegacy --plays <play-tests.json> --runs-root <bot-runs>
                            --triage <bot-triage.md> --out <bot-tonight.json> [--commit HEAD] [--max-age 300]

The rule is uobot.work.find_work, the same call Run-OvernightPlaytests.ps1 makes at step 0 (same repo, play-tests file,
runs root and commit), so the page's "Bot tonight" count is the runner's work count. Nothing here builds, starts a
container or writes to git: find_work reads git (rev-parse, diff) and the run folders.

Writes one small JSON file (schema 1):
  available  true, or false with "error" when the work check cannot run (no repo, a bad commit)
  commit, short, generatedAt, minutes (the whole night, as the runner estimates it)
  tonight    {id: {reason, minutes}}: open items with a bot check and work on this code (the robot)
  checkable  [ids]: BOT or BOT-PARTIAL in bot-triage.md, or with a check, and not in tonight (the dim robot)
  last       {id: {result, run, at}}: each item's newest bot result (PASS, FAIL, FLAKY or ERROR), from any run

Publish-GamePcSnapshot.ps1 runs this every minute on the game PC; --max-age skips the work while the file is younger than
that, so the work check runs at most every 5 minutes. The play-test service reads the file on the game PC, and on Haven
the same content arrives inside the game PC snapshot (botTonight).
"""
import argparse
import json
import math
import os
import re
import sys
import time
from datetime import datetime, timezone

from . import work as W

TRIAGE_ROW = re.compile(r"^\|\s*([A-Za-z0-9-]+)\s*\|\s*(BOT|BOT-PARTIAL|EYES|DEPLOY|DECISION)\s*\|")


def triage_bot_ids(path):
    """Ids marked BOT or BOT-PARTIAL in bot-triage.md (its '| id | BUCKET | reason |' rows)."""
    ids = []
    try:
        with open(path, "r", encoding="utf-8") as f:
            for line in f:
                m = TRIAGE_ROW.match(line)
                if m and m.group(2) in ("BOT", "BOT-PARTIAL") and m.group(1) not in ids:
                    ids.append(m.group(1))
    except OSError:
        pass
    return ids


def last_results(runs_root):
    """Each id's newest result in any run folder, PASS, FAIL, FLAKY or ERROR, with the run's stamp and time."""
    last = {}
    for stamp, when, _sha, ids in W.bot_runs(runs_root):
        for iid, result in ids.items():
            if result:
                last[iid] = {"result": result, "run": stamp, "at": when.strftime("%Y-%m-%dT%H:%M:%SZ") if when else ""}
    return last


def now_utc():
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def plan(repo, plays, runs_root, triage, commit="HEAD", check_ids=None, runs=3, soak_minutes=120):
    if check_ids is None:
        from . import checks as C
        check_ids = [c.id for c in C.REGISTRY]
    out = {"schema": 1, "generatedAt": now_utc()}
    try:
        w = W.find_work(repo, plays, runs_root, commit, check_ids)
    except Exception as e:  # a bad repo or commit: the page shows no robots and one quiet line
        out.update(available=False, error=str(e))
        return out
    seconds = W.check_seconds(runs_root)
    tonight = {}
    for it in w["items"]:
        per = seconds.get(it["id"], W.UNKNOWN_CHECK_SECONDS)
        tonight[it["id"]] = {"reason": it["reason"], "minutes": int(math.ceil(runs * per / 60.0))}
    bot_ids = list(dict.fromkeys(triage_bot_ids(triage) + list(check_ids)))
    out.update(available=True, commit=w["sha"], short=w["short"], run=w["run"],
               minutes=W.estimate_minutes(runs_root, check_ids, runs, soak_minutes) if w["run"] else 0,
               tonight=tonight, checkable=[i for i in bot_ids if i not in tonight], last=last_results(runs_root))
    return out


def fresh(path, max_age):
    try:
        return max_age > 0 and (time.time() - os.path.getmtime(path)) < max_age
    except OSError:
        return False


def write(path, doc):
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    os.replace(tmp, path)


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--plays", required=True)
    ap.add_argument("--runs-root", required=True)
    ap.add_argument("--triage", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--commit", default="HEAD")
    ap.add_argument("--runs", type=int, default=3)
    ap.add_argument("--soak-minutes", type=int, default=120)
    ap.add_argument("--max-age", type=int, default=300)
    a = ap.parse_args(argv)
    if fresh(a.out, a.max_age):
        print("fresh: %s" % a.out)
        return 0
    doc = plan(a.repo, a.plays, a.runs_root, a.triage, a.commit, runs=a.runs, soak_minutes=a.soak_minutes)
    write(a.out, doc)
    print("%s: %s" % (a.out, ("%d tonight on %s" % (len(doc["tonight"]), doc["short"])) if doc["available"] else "unavailable: " + doc["error"]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
