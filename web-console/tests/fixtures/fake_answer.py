"""cc-P216 tests: stands in for D:/UO/golem/Answer-Question.ps1. Records its arguments (one JSON line per call) in the
file named first, prints what the script prints when it answers and resumes, and exits 0 (or 1 while <record>.refuse
exists, printing a refusal). ASCII only."""
import json
import os
import sys

record, args = sys.argv[1], sys.argv[2:]
with open(record, "a", encoding="utf-8") as f:
    f.write(json.dumps(args) + "\n")
qid = args[args.index("-Id") + 1] if "-Id" in args else "?"
if os.path.exists(record + ".refuse"):
    print("Not answered: closed: it was answered already", flush=True)
    sys.exit(1)
print("Answered %s: \"Retry with my answer\"." % qid, flush=True)
print("P100 is back in the queue and goes next (cc-P100-a.md); stoppedPrompt is cleared. The new session is given the answer first.", flush=True)
sys.exit(0)
