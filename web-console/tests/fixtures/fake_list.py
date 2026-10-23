"""cc-P187 tests: stands in for scripts/Get-WebConsoleList.ps1. Records each call (one JSON line) in the file named first
and prints a page's lists as the real script does (the shapes of Get-WebConsoleList.ps1's JSON). ASCII only."""
import json
import os
import sys

record, args = sys.argv[1], sys.argv[2:]
with open(record, "a", encoding="utf-8") as f:
    f.write(json.dumps(args) + "\n")
page = args[args.index("-Page") + 1]
out = {"page": page, "at": "2026-10-20T12:00:00"}
if page == "package":
    out.update(zips=[{"value": "ShatteredLegacy-2026.10.18.1709.zip", "cells": ["2026.10.18.1709", "98.4 MB", "2026-10-18 17:09:51", ""]},
                     {"value": "ShatteredLegacy-2026.10.18.1122.zip", "cells": ["2026.10.18.1122", "98.4 MB", "2026-10-18 11:23:09", "token=abc123secretvalue"]}],
               client="TazUO 4.1 (sl-build.txt: ok)",
               whatsNew={"line": "Rootdeep map, satchel and guild bag icons", "version": "2026.10.18.1122", "base": "ac9eee3", "baseHow": "time",
                         "list": ["Rootdeep map (P188, map): The Rootdeep Burrows are in the world", "satchel and guild bag icons (P236, art)"],
                         "leftOut": ["Left out, staff only in CHANGELOG.md: P277 rideable red dragon"], "note": ""})
elif page == "snapshots":
    out["shards"] = {"test": {"rows": [{"value": "20261019-101500_before-p185", "cells": ["2026-10-19 10:15:00", "1.2 GB", "812", "yes", "named"]}],
                              "save": "D:\\x\\modernuo-test\\Saves", "folder": "D:\\x\\Snapshots\\test", "retired": False},
                     "live": {"rows": [], "save": "D:\\x\\modernuo\\Saves", "folder": "D:\\x\\Snapshots\\live", "retired": True}}
elif page == "art":
    out["requests"] = [{"value": "console-crystal-amber", "cells": ["generated", "P132", "v1 codex", "Console art: crystal-amber"], "thumb": os.environ.get("P187_OUT_PNG", "C:\\Windows\\win.ini")},
                       {"value": "golem-iron", "cells": ["new", "P186", "", "Iron portrait"], "thumb": os.environ.get("P187_ART_PNG", "")}]
elif page == "live":
    out.update(liveRetired=True, line="retired 2026-10-14 (returns as a fresh 1.0 world)")
elif page == "question":
    out["shards"] = {k: {"lines": ["The %s shard is not waiting on a console question." % k.upper()], "enabled": ["question.show", "question.stop"], "asking": False, "retired": k == "live"} for k in ("test", "live")}
print(json.dumps(out))
