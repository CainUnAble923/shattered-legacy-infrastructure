"""Golem login checks (cc-P215): a Golem's own test accounts log in on a test shard, and two Golems' players meet.

    python -m uobot.golemcheck login --host 127.0.0.1 --port 2594 [--golem Bronze] [--roles admin,player] [--out DIR]
    python -m uobot.golemcheck pair  --host 192.168.1.58 --port 2594 --golem Bronze --with Gold [--out DIR]

login: each account logs in (seed, 0x80, relay, 0x91), plays its starter character ('Bronze', 'Bronze the Tester'),
says where it stands, and logs out. pair: both Golems' players log in, each must see the other, and the first opens a
secure trade by dropping a gold coin on the second (pinned's drop-on-a-player trade); both must get the trade window
(0x6F display), then both disconnect, which cancels it and gives the coin back.

The accounts are the running Golem's (GOLEM_NAME) unless --golem names another; their passwords come from
D:\\UO\\golem\\Get-GolemTestAccount.ps1 and never reach a transcript, the output or a file. Test shards only:
safety.check_test_target refuses the live port from any host. One JSON line per account (login) or one for the pair is
printed; the exit code is 0 when every check passed.
"""
import argparse
import json
import os
import subprocess
import sys
import time

from .client import Client, Cliloc, LoginFailed, Transcript
from . import packets as P
from .safety import UnsafeTarget, check_test_target

GOLEM_ROOT = os.environ.get("GOLEM_ROOT_DIR", r"D:\UO\golem")
CLILOC = r"D:\UO\tool-trials\client-data-copy\Cliloc.enu"


def account(golem, role, root=GOLEM_ROOT):
    """(username, password) from Get-GolemTestAccount.ps1; the password stays in memory."""
    script = os.path.join(root, "Get-GolemTestAccount.ps1")
    r = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                        "-Golem", golem, "-Role", role, "-Json"], capture_output=True, text=True, timeout=60)
    if r.returncode != 0:
        raise LoginFailed("no %s account for %s on this PC (Get-GolemTestAccount.ps1 exit %d: %s)"
                          % (role, golem, r.returncode, (r.stderr or "").strip()[:200]))
    o = json.loads(r.stdout.strip().splitlines()[-1])
    return o["Username"], o["Password"]


def character_name(golem, role):
    return golem if role == "admin" else golem + " the Tester"


def enter(golem, role, host, port, tr, cl):
    user, pw = account(golem, role)
    c = Client(user, pw, tr, cl, label="%s-%s" % (golem.lower(), role), test_target=(host, port))
    chars = c.login()
    want = character_name(golem, role)
    if want not in chars:
        c.close()
        raise LoginFailed("%s logged in, but its characters are %s, not %r" % (user, chars, want))
    c.play(chars.index(want))
    return c, user, want


def do_login(a, tr, cl):
    ok = True
    for role in a.roles.split(","):
        res = {"check": "login", "golem": a.golem, "role": role, "host": a.host, "port": a.port}
        try:
            c, user, name = enter(a.golem, role, a.host, a.port, tr, cl)
            res.update(user=user, character=name, ok=bool(c.logged_in and c.pos), pos=c.pos,
                       access="staff" if role == "admin" else "player")
            c.close()
        except (LoginFailed, UnsafeTarget, OSError, ValueError) as e:
            res.update(ok=False, why=str(e))
        ok = ok and res["ok"]
        print(json.dumps(res))
        sys.stdout.flush()
    return ok


def find_gold(c, timeout=8):
    """Opens the backpack (layer 0x15 of our own mobile) and returns a gold pile's serial in it, or None."""
    pack = None
    for s, it in list(c.items.items()):
        if it.get("parent") == c.serial and it.get("layer") == 0x15:
            pack = s
    if not pack:
        return None
    c.dclick(pack)
    return c.wait(lambda: next((s for s, it in c.items.items() if it.get("container") == pack and it.get("graphic") in (0x0EED, 0x0EEE, 0x0EEF)), None), timeout)


def do_pair(a, tr, cl):
    res = {"check": "pair", "golems": [a.golem, a.with_], "host": a.host, "port": a.port}
    one = two = None
    try:
        one, u1, n1 = enter(a.golem, "player", a.host, a.port, tr, cl)
        two, u2, n2 = enter(a.with_, "player", a.host, a.port, tr, cl)
        res.update(players=[n1, n2])
        sees = one.wait(lambda: two.serial in one.mobiles, 15) and two.wait(lambda: one.serial in two.mobiles, 15)
        res["see_each_other"] = bool(sees)
        gold = find_gold(one)
        res["gold_found"] = bool(gold)
        if sees and gold:
            # Opening the backpack used the action delay ("You must wait to perform another action", cliloc 500119):
            # let it pass before the lift.
            one.sleep(1.5)
            one.log("-> 0x07 lift 0x%08X x1, 0x08 drop on 0x%08X (trade)" % (gold, two.serial))
            one.send(P.lift(gold, 1))
            one.sleep(0.5)
            one.send(P.drop(gold, -1, -1, 0, two.serial))
            t1 = one.wait(lambda: any(t["action"] == 0 for t in one.trades), 10)
            t2 = two.wait(lambda: any(t["action"] == 0 for t in two.trades), 10)
            res["trade_window"] = {"first": bool(t1), "second": bool(t2)}
            res["ok"] = bool(t1 and t2)
        else:
            res["ok"] = False
    except (LoginFailed, UnsafeTarget, OSError, ValueError) as e:
        res.update(ok=False, why=str(e))
    finally:
        for c in (one, two):
            if c:
                c.close()
    print(json.dumps(res))
    return res["ok"]


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("check", choices=["login", "pair"])
    ap.add_argument("--host", required=True)
    ap.add_argument("--port", type=int, required=True)
    ap.add_argument("--golem", default=os.environ.get("GOLEM_NAME", ""))
    ap.add_argument("--with", dest="with_", default="")
    ap.add_argument("--roles", default="admin,player")
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
    out = a.out or os.path.join(os.environ.get("TEMP", "."), "golemcheck")
    os.makedirs(out, exist_ok=True)
    tr = Transcript(os.path.join(out, "golemcheck-%s-%s.txt" % (a.check, time.strftime("%Y%m%d-%H%M%S"))))
    cl = Cliloc(CLILOC if os.path.exists(CLILOC) else None)
    try:
        if a.check == "login":
            ok = do_login(a, tr, cl)
        else:
            if not a.with_:
                print(json.dumps({"ok": False, "why": "pair needs --with <another Golem>"}))
                return 2
            ok = do_pair(a, tr, cl)
    finally:
        tr.close()
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
