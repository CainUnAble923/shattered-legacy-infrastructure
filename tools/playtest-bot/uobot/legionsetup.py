"""Legion script tests (cc-P219): a Golem's own Admin sets up its Player character on a TEST shard before a script runs.

    python -m uobot.legionsetup --host 127.0.0.1 --port 2594 --golem Iron --commands <file.json> [--out DIR]

commands: a JSON list of staff commands typed by the Golem's Admin character ('<golem>-admin', starter character
'<golem>'), each with {player} standing for the Golem's Player character ('<golem> the Tester'), for example
'[global set Skills.Mining.Base 100 where playermobile Name == "{player}"'. Each is typed as Chase would type it and
the server's answer is kept; then the Admin logs out. Test shards only (safety.check_test_target refuses the live port
from any host); the passwords come from Get-GolemTestAccount.ps1 and never reach the output or a file.

One JSON line is printed: {ok, golem, host, port, commands: [{command, replies, ok}]}; the exit code is 0 when every
command was answered without an error.
"""
import argparse
import json
import os
import re
import sys
import time

from .client import Cliloc, LoginFailed, Transcript
from .golemcheck import CLILOC, character_name, enter
from .safety import UnsafeTarget, check_test_target

# What a ModernUO staff command answers when it did nothing (Commands/Generic, Server/Commands).
ERROR = re.compile(r"(?i)(is not a recognized command|bad command|not recognized|invalid|no matching objects|"
                   r"no property|cannot be set|failed|usage:|you do not have access)")


def expand(commands, golem):
    """The commands with {player} and {golem} filled in."""
    player = character_name(golem, "player")
    return [c.replace("{player}", player).replace("{golem}", golem) for c in commands]


def run(golem, host, port, commands, tr, cl, settle=1.5):
    res = {"golem": golem, "host": host, "port": port, "commands": []}
    c = None
    try:
        c, user, name = enter(golem, "admin", host, port, tr, cl)
        res["admin"] = name
        ok = True
        for cmd in expand(commands, golem):
            replies = [str(m.get("text", "")) for m in c.command(cmd, settle=settle)]
            good = not any(ERROR.search(r) for r in replies)
            res["commands"].append({"command": cmd, "replies": replies, "ok": good})
            ok = ok and good
        res["ok"] = ok
    except (LoginFailed, UnsafeTarget, OSError, ValueError) as e:
        res.update(ok=False, why=str(e))
    finally:
        if c:
            c.close()
    return res


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", required=True)
    ap.add_argument("--port", type=int, required=True)
    ap.add_argument("--golem", default=os.environ.get("GOLEM_NAME", ""))
    ap.add_argument("--commands", required=True)
    ap.add_argument("--out", default="")
    a = ap.parse_args(argv)
    if not a.golem:
        print(json.dumps({"ok": False, "why": "say --golem (GOLEM_NAME is not set)"}))
        return 2
    try:
        check_test_target(a.host, a.port)
    except UnsafeTarget as e:
        print(json.dumps({"ok": False, "why": str(e)}))
        return 2
    with open(a.commands, "r", encoding="utf-8-sig") as f:
        commands = json.load(f)
    if not isinstance(commands, list) or not all(isinstance(x, str) and x.startswith("[") for x in commands):
        print(json.dumps({"ok": False, "why": "commands must be a list of staff commands, each starting with ["}))
        return 2
    out = a.out or os.path.join(os.environ.get("TEMP", "."), "legionsetup")
    os.makedirs(out, exist_ok=True)
    tr = Transcript(os.path.join(out, "legionsetup-%s-%s.txt" % (a.golem.lower(), time.strftime("%Y%m%d-%H%M%S"))))
    cl = Cliloc(CLILOC if os.path.exists(CLILOC) else None)
    try:
        res = run(a.golem, a.host, a.port, commands, tr, cl)
    finally:
        tr.close()
    print(json.dumps(res))
    return 0 if res.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main())
