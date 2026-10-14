"""Does tonight have work? (cc-P102 Part A.) Run-OvernightPlaytests.ps1 asks before it builds anything.

    python -m uobot.work --repo D:\\ShatteredLegacy --plays <play-tests.json> --runs-root <bot-runs> --commit HEAD
                         [--force] [--json <out file>] [--runs 3] [--soak-minutes 120]

An item counts when it is OPEN in play-tests.json, has a bot check (uobot.checks.REGISTRY), and has not run against the
code it would test. "Run against" means a bot run folder whose results.json holds the id with a real result (PASS, FAIL
or FLAKY; ERROR is not a run) and whose code is the same as the target's for everything the bot tests: no change in
CODE_PATHS between the two commits. So a new item counts, an item whose last run is older than its code counts, and an
item re-opened (play-tests.json reopenedAt, written by tools/playtests.py) after its last run counts. Nothing else does.
"""
import argparse
import json
import math
import os
import re
import subprocess
import sys
from datetime import datetime, timezone

# What a run tests: the server and its image (what build.sh builds) and the bot's own checks.
CODE_PATHS = ["server", "docker/uo", "tools/playtest-bot/uobot"]
REAL_RESULTS = ("PASS", "FAIL", "FLAKY")
BUILD_AND_BOOT_MINUTES = 25        # build.sh with its gates, then the boot and the account wipe (cc-P91 run.log times)
UNKNOWN_CHECK_SECONDS = 60.0


def _git(repo, *args):
    p = subprocess.run(["git", "-C", repo] + list(args), capture_output=True, text=True)
    return p.returncode, p.stdout.strip()


def resolve(repo, commit):
    code, out = _git(repo, "rev-parse", "--verify", "--quiet", commit + "^{commit}")
    if code != 0 or not out:
        raise ValueError("not a commit in %s: %s" % (repo, commit))
    return out


def run_sha(world):
    """The commit a run tested: world.json commitSha (cc-P102 on), else read from its code line (cc-P91 runs)."""
    if world.get("commitSha"):
        return world["commitSha"]
    m = re.search(r"(?:^commit |working tree at )([0-9a-f]{7,40})\b", world.get("code") or "")
    return m.group(1) if m else ""


def run_time_utc(stamp):
    """A run folder's name is its local start time, yyyy-MM-dd-HHmm."""
    try:
        return datetime.strptime(stamp, "%Y-%m-%d-%H%M").astimezone(timezone.utc)
    except ValueError:
        return None


def parse_utc(text):
    if not text:
        return None
    for fmt in ("%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%dT%H:%M:%S.%fZ", "%Y-%m-%d"):
        try:
            return datetime.strptime(text, fmt).replace(tzinfo=timezone.utc)
        except ValueError:
            pass
    return None


def _load(path):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def bot_runs(runs_root):
    """Every finished run: (stamp, time, sha, {id: result}), oldest first."""
    out = []
    if not os.path.isdir(runs_root):
        return out
    for stamp in sorted(os.listdir(runs_root)):
        d = os.path.join(runs_root, stamp)
        res, world = _load(os.path.join(d, "results.json")), _load(os.path.join(d, "world.json"))
        if not res or not world:
            continue
        ids = {c["id"]: c.get("result") for c in res.get("checks", [])}
        out.append((stamp, run_time_utc(stamp), run_sha(world), ids))
    return out


def find_work(repo, plays_path, runs_root, commit, check_ids, force=False):
    sha = resolve(repo, commit)
    plays = _load(plays_path) or {}
    checked = set(check_ids)
    open_items = [i for i in plays.get("items", []) if i.get("status") == "open" and i.get("id") in checked]
    runs = bot_runs(runs_root)
    same = {}

    def same_code(other):
        if other not in same:
            code, full = _git(repo, "rev-parse", "--verify", "--quiet", other + "^{commit}")
            same[other] = code == 0 and _git(repo, "diff", "--quiet", full, sha, "--", *CODE_PATHS)[0] == 0
        return same[other]

    items = []
    for it in open_items:
        iid = it["id"]
        mine = [r for r in runs if r[3].get(iid) in REAL_RESULTS]
        if not mine:
            items.append({"id": iid, "reason": "no bot run yet"})
            continue
        current = [r for r in mine if r[2] and same_code(r[2])]
        if not current:
            last = mine[-1]
            items.append({"id": iid, "reason": "code changed since %s (run %s)" % (last[2][:7] or "?", last[0])})
            continue
        last = current[-1]
        reopened = parse_utc(it.get("reopenedAt", ""))
        if reopened and last[1] and reopened > last[1]:
            items.append({"id": iid, "reason": "re-opened %s, after its last bot run %s" % (it["reopenedAt"], last[0])})
    return {"sha": sha, "short": sha[:7], "work": bool(items), "forced": bool(force) and not items,
            "run": bool(items) or bool(force), "items": items, "checks": len(check_ids)}


def check_seconds(runs_root):
    """Each check's mean seconds per run, from its newest run that has timings. (cc-P112: shared with uobot.tonight.)"""
    seconds = {}
    for stamp in sorted(os.listdir(runs_root)) if os.path.isdir(runs_root) else []:
        res = _load(os.path.join(runs_root, stamp, "results.json")) or {}
        for c in res.get("checks", []):
            per = [r.get("seconds", 0.0) for r in c.get("runs", [])]
            if per:
                seconds[c["id"]] = sum(per) / len(per)
    return seconds


def estimate_minutes(runs_root, check_ids, runs=3, soak_minutes=120):
    """Build and boot, the soak, and each check's seconds from its newest run (60 s if never run), times runs."""
    seconds = check_seconds(runs_root)
    total = BUILD_AND_BOOT_MINUTES * 60.0 + soak_minutes * 60.0
    for cid in check_ids:
        total += runs * seconds.get(cid, UNKNOWN_CHECK_SECONDS)
    return int(math.ceil(total / 60.0))


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--plays", required=True)
    ap.add_argument("--runs-root", required=True)
    ap.add_argument("--commit", default="HEAD")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--runs", type=int, default=3)
    ap.add_argument("--soak-minutes", type=int, default=0)
    ap.add_argument("--json", default="")
    a = ap.parse_args(argv)
    from . import checks as C
    ids = [c.id for c in C.REGISTRY]
    w = find_work(a.repo, a.plays, a.runs_root, a.commit, ids, force=a.force)
    w["minutes"] = estimate_minutes(a.runs_root, ids, a.runs, a.soak_minutes)
    text = json.dumps(w, indent=1)
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            f.write(text)
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
