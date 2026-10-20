"""cc-P187 tests: stands in for scripts/Invoke-WebConsoleAction.ps1 for the page buttons. Records its arguments and what
came on stdin (one JSON line per call) in the file named first. With -DryRun it prints a plan and the [[web-preview]]
line, the phrase taken from <record>.expect.json (key -> phrase) and dangerous when the key is in its "danger" list or
-Shard live; otherwise it prints the console's result line. While <record>.sleep exists it waits (at most 15 s), so a
test can hold an action running. ASCII only."""
import json
import os
import sys
import time

record, args = sys.argv[1], sys.argv[2:]
key = args[args.index("-Key") + 1] if "-Key" in args else "?"
stdin = ""
if "-DryRun" not in args:
    try:
        stdin = sys.stdin.read() if not sys.stdin.isatty() else ""
    except (OSError, ValueError):
        stdin = ""
with open(record, "a", encoding="utf-8") as f:
    f.write(json.dumps({"args": args, "stdin": stdin}) + "\n")
cfg = {}
try:
    with open(record + ".expect.json", encoding="utf-8") as f:
        cfg = json.load(f)
except (OSError, ValueError):
    pass
print("[[shard-console:head]]Web console: " + key, flush=True)
if "-DryRun" in args:
    print("[[shard-console:normal]]== DRY RUN: %s ==" % key, flush=True)
    print("[[shard-console:normal]]  1. SAY  a planted token=abc123secretvalue", flush=True)
    live = "-Shard" in args and args[args.index("-Shard") + 1] == "live"
    meta = {"expect": cfg.get("expect", {}).get(key, ""), "confirmText": "The console's own words for " + key + ".", "preface": "",
            "asks": cfg.get("asks", {}).get(key, ""), "danger": key in cfg.get("danger", []) or live, "shard": "live" if live else "test"}
    if key in cfg.get("refuse", []):
        meta = {"refused": "Refused: the plan refuses " + key + "."}
    print("[[web-preview]]" + json.dumps(meta), flush=True)
    sys.exit(0)
until = time.time() + 15
while os.path.exists(record + ".sleep") and time.time() < until:
    time.sleep(0.05)
print("[[shard-console:green]]%s: done." % key, flush=True)
sys.exit(0)
