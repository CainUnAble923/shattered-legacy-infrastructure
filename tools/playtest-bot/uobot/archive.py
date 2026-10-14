"""14 days in the repo, then iCloud Drive (cc-P102 Part D). Run-OvernightPlaytests.ps1 calls it after each run.

    python -m uobot.archive --runs-root <bot-runs> --drive "D:\\Cloud Drives\\iCloudDrive"
                            --path "Shattered Legacy\\bot-runs" --days 14 [--json <out file>]

A run folder's transcripts\\ older than --days (by the folder's yyyy-MM-dd-HHmm name) is COPIED to
<drive>\\<path>\\<stamp>\\transcripts\\, VERIFIED (same file count, same total bytes, and the SHA-256 of every file), and
only then removed from the repo, leaving transcripts-archived.json to say where it went. report.md, results.json and
everything else in the run folder stay. If the drive (the iCloud sync root) is missing, or a copy does not verify,
nothing is removed and the note says so. The drive is never created here: a missing one means iCloud is not there.
"""
import argparse
import hashlib
import json
import os
import shutil
import sys
from datetime import datetime


def _files(root):
    out = {}
    for d, _, names in os.walk(root):
        for n in names:
            p = os.path.join(d, n)
            out[os.path.relpath(p, root)] = p
    return out


def _sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def verify(src, dst):
    """Problems with dst as a copy of src; empty means it verified."""
    a, b = _files(src), _files(dst)
    problems = []
    if len(a) != len(b):
        problems.append("file count %d, copy has %d" % (len(a), len(b)))
    size_a = sum(os.path.getsize(p) for p in a.values())
    size_b = sum(os.path.getsize(p) for p in b.values())
    if size_a != size_b:
        problems.append("total bytes %d, copy has %d" % (size_a, size_b))
    for rel, p in sorted(a.items()):
        if rel not in b:
            problems.append("missing in copy: " + rel)
        elif _sha(p) != _sha(b[rel]):
            problems.append("hash differs: " + rel)
    return problems


def archive_old(runs_root, drive, rel_path, days, now=None, copy=shutil.copy2):
    now = now or datetime.now()
    old = []
    for stamp in sorted(os.listdir(runs_root)) if os.path.isdir(runs_root) else []:
        try:
            when = datetime.strptime(stamp, "%Y-%m-%d-%H%M")
        except ValueError:
            continue
        if (now - when).total_seconds() > days * 86400 and os.path.isdir(os.path.join(runs_root, stamp, "transcripts")):
            old.append(stamp)
    result = {"archived": [], "problems": [], "candidates": old}
    if not old:
        result["note"] = "Archive: nothing older than %d days." % days
        return result
    if not os.path.isdir(drive):
        result["problems"].append("archive root missing: %s" % drive)
    else:
        for stamp in old:
            src = os.path.join(runs_root, stamp, "transcripts")
            dst = os.path.join(drive, rel_path, stamp, "transcripts")
            try:
                shutil.copytree(src, dst, copy_function=copy, dirs_exist_ok=True)
                bad = verify(src, dst)
            except OSError as e:
                bad = ["copy failed: %s" % e]
            if bad:
                result["problems"] += ["%s: %s" % (stamp, p) for p in bad]
                continue
            files = _files(src)
            with open(os.path.join(runs_root, stamp, "transcripts-archived.json"), "w", encoding="utf-8") as f:
                json.dump({"to": dst, "files": len(files), "bytes": sum(os.path.getsize(p) for p in files.values()),
                           "at": now.strftime("%Y-%m-%dT%H:%M:%S")}, f, indent=1)
            shutil.rmtree(src)
            result["archived"].append(stamp)
    n = len(result["archived"])
    note = []
    if n:
        note.append("Archive: transcripts of %d run%s moved to %s, verified (%s)." % (
            n, "" if n == 1 else "s", os.path.join(drive, rel_path), ", ".join(result["archived"])))
    if result["problems"]:
        left = [s for s in old if s not in result["archived"]]
        note.append("Archive problem, transcripts NOT removed for %s: %s." % (", ".join(left), "; ".join(result["problems"][:5])))
    result["note"] = " ".join(note)
    return result


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs-root", required=True)
    ap.add_argument("--drive", required=True)
    ap.add_argument("--path", required=True)
    ap.add_argument("--days", type=int, default=14)
    ap.add_argument("--json", default="")
    a = ap.parse_args(argv)
    r = archive_old(a.runs_root, a.drive, a.path, a.days)
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            json.dump(r, f, indent=1)
    print(r["note"])
    return 1 if r["problems"] else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
