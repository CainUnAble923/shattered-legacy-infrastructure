"""cc-P182 tests: stands in for scripts/Invoke-WebConsoleAction.ps1. Records its arguments (one JSON line per call) in
the file named first, prints what the console's runner prints (pane-marked lines, a planted token, the result line)
and exits 0. While <record>.sleep exists it waits (at most 15 s), so a test can hold an action running. ASCII only."""
import json
import os
import sys
import time

record, args = sys.argv[1], sys.argv[2:]
with open(record, "a", encoding="utf-8") as f:
    f.write(json.dumps(args) + "\n")
key = args[args.index("-Key") + 1] if "-Key" in args else "?"
print("[[shard-console:head]]Web console: " + key, flush=True)
print("plain line from a Golem script, token=abc123secretvalue", flush=True)
until = time.time() + 15
while os.path.exists(record + ".sleep") and time.time() < until:
    time.sleep(0.05)
print("[[shard-console:green]]golem.%s: done." % key, flush=True)
sys.exit(0)
