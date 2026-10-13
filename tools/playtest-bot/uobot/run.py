"""Runs the checks (and the soak) against the scratch shard; Run-OvernightPlaytests.ps1 calls it (cc-P91).

    python -m uobot.run --secrets secrets.local.json --out <run folder> [--runs 3] [--only T-041,P64-1] [--soak-minutes 120]

Each check runs --runs times. 3/3 is PASS, 0/3 FAIL, anything between FLAKY; a check that cannot be set up is ERROR.
Every run writes its own transcript (transcripts/<id>-run<n>.txt). results.json is what report.py reads.
"""
import argparse
import json
import os
import subprocess
import sys
import threading
import time
import traceback

from .client import Client, Cliloc, LoginFailed, Transcript
from . import checks as C

CLIENT_DATA = r"D:\ShatteredLegacy\client-data\classic-client"
CACHE = r"D:\UO\bot-shard\cache"
# An account's characters stay in the world for 5 minutes after it disconnects outside an inn
# (Server/Regions/Region.cs:200, DefaultLogoutDelay), and "account in use" refuses another character on it until then
# (ACC:160-170). So fresh characters rotate over these accounts, each rested before reuse.
POOL = ["slbot-player2", "slbot-player3", "slbot-player4"]
REST_SECONDS = 330


class Ctx(object):
    def __init__(self, secrets, out, container):
        self.secrets = secrets
        self.out = out
        self.container = container
        self.cliloc = Cliloc(os.path.join(CLIENT_DATA, "Cliloc.enu"), CACHE)
        self.tr = Transcript(os.path.join(out, "transcripts", "session.txt"))
        self.admin = None
        self.player = None
        self.pool_rest = {a: 0.0 for a in POOL}
        self.pool_lock = threading.Lock()
        # One admin client, shared: the soak's threads take this before any staff command, so two target cursors
        # never cross.
        self.admin_lock = threading.RLock()
        self.first_logins = {}      # account -> what the first login saw (P64-1)
        self.fresh_count = 0
        self.serials = {}

    # -- the long-lived characters ------------------------------------------------------------------------------
    def use_transcript(self, tr):
        self.tr = tr
        for c in (self.admin, self.player):
            if c is not None:
                c.tr = tr

    def ensure_admin(self):
        if self.admin is None or self.admin.closed is not None:
            c = Client(self.secrets["admin"], self.secrets["adminPassword"], self.tr, self.cliloc, "admin")
            chars = c.login()
            if chars and chars[0]:
                c.play(0)
            else:
                c.create("Bot Admin", race="human", female=False)
            self.admin = c
        return self.admin

    def ensure_player(self):
        if self.player is None or self.player.closed is not None:
            c = Client(self.secrets["player"], self.secrets["playerPassword"], self.tr, self.cliloc, "player")
            chars = c.login()
            if chars and chars[0]:
                c.play(0)
            else:
                c.create("Bot Human F Aa", race="human", female=True)
            self.player = c
        return self.player

    def fresh(self, race="human", female=False, label="fresh"):
        """A brand-new character on a rested pool account, through the real creation packet. Disconnect it with
        done() so the account can rest."""
        # Thread-safe: the soak asks for three at once. An account is marked busy (rest = forever) the moment it is
        # picked, and done() starts its rest.
        while True:
            with self.pool_lock:
                now = time.time()
                ready = [a for a in POOL if self.pool_rest[a] <= now]
                if ready:
                    acct = ready[0]
                    self.pool_rest[acct] = float("inf")
                    break
                wait = min(self.pool_rest.values()) - now
            self.tr.write("harness", "all pool accounts busy or resting; waiting %.0f s" % min(wait, 30))
            time.sleep(max(1.0, min(wait, 30)))
        self.fresh_count += 1
        c = Client(acct, self.secrets["playerPassword"], self.tr, self.cliloc, label)
        c.account = acct
        first = acct not in self.first_logins
        try:
            chars = c.login()
        except Exception:
            self.done(c)
            raise
        slot = next((i for i, n in enumerate(chars) if not n), None)
        if slot is None:
            raise LoginFailed("account %s has no free slot" % acct)
        # Letters only: a name with a digit ("Bot Human F 1", as first planned) is refused by the shard's name check and
        # the character is created as "Generic Player" (seen 2026-10-13), so the count is spelled in letters.
        n = self.fresh_count
        tag = chr(65 + (n // 26) % 26) + chr(97 + n % 26)
        name = "Bot %s %s %s" % (race.capitalize(), "F" if female else "M", tag)
        try:
            c.create(name[:16], race=race, female=female)
        except Exception:
            self.done(c)
            raise
        c.name = name[:16]
        if first:
            # The first login of an account: what it was shown, for P64-1 (bulletin once per account per version).
            c.sleep(2.0)
            self.first_logins[acct] = {"gumps": list(c.gumps), "messages": list(c.messages)}
        return c

    def done(self, c):
        acct = getattr(c, "account", None)
        c.close()
        if acct:
            self.pool_rest[acct] = time.time() + REST_SECONDS

    def docker_logs_since(self, since):
        try:
            out = subprocess.run(["docker", "logs", "--since", since, self.container], capture_output=True, text=True,
                                 encoding="utf-8", errors="replace", timeout=60)
            return out.stdout + out.stderr
        except Exception as e:
            return "(docker logs failed: %s)" % e


def run_checks(ctx, selected, runs):
    results = []
    tdir = os.path.join(ctx.out, "transcripts")
    for chk in selected:
        outcomes = []
        for k in range(1, runs + 1):
            path = os.path.join(tdir, "%s-run%d.txt" % (chk.id, k))
            tr = Transcript(path)
            ctx.use_transcript(tr)
            tr.write("harness", "== %s run %d: %s" % (chk.id, k, chk.title))
            t0 = time.time()
            try:
                ok, evidence = chk.fn(ctx)
                verdict = "pass" if ok else "fail"
            except Exception as e:
                verdict, evidence = "error", "%s: %s" % (type(e).__name__, e)
                tr.write("harness", traceback.format_exc())
            tr.write("harness", "== %s run %d: %s (%s)" % (chk.id, k, verdict.upper(), evidence))
            tr.close()
            outcomes.append({"run": k, "verdict": verdict, "evidence": evidence, "seconds": round(time.time() - t0, 1),
                             "transcript": os.path.relpath(path, ctx.out).replace("\\", "/")})
            print("%-10s run %d: %-5s %s" % (chk.id, k, verdict.upper(), evidence[:150]), flush=True)
        passes = sum(1 for o in outcomes if o["verdict"] == "pass")
        errors = sum(1 for o in outcomes if o["verdict"] == "error")
        if passes == runs:
            result = "PASS"
        elif errors == runs:
            result = "ERROR"
        elif passes == 0:
            result = "FAIL"
        else:
            result = "FLAKY"
        results.append({"id": chk.id, "title": chk.title, "kind": chk.kind, "left_for_chase": chk.left_for_chase,
                        "result": result, "passes": passes, "runs": outcomes})
        with open(os.path.join(ctx.out, "results.json"), "w", encoding="utf-8") as f:
            json.dump({"checks": results}, f, indent=1)
    return results


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--secrets", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--runs", type=int, default=3)
    ap.add_argument("--only", default="")
    ap.add_argument("--container", default="sl-modernuo-bot")
    ap.add_argument("--soak-minutes", type=int, default=0)
    a = ap.parse_args(argv)
    os.makedirs(os.path.join(a.out, "transcripts"), exist_ok=True)
    with open(a.secrets, "r", encoding="utf-8") as f:
        secrets = json.load(f)
    ctx = Ctx(secrets, a.out, a.container)
    only = [x.strip() for x in a.only.split(",") if x.strip()]
    selected = [c for c in C.REGISTRY if not only or c.id in only]
    print("checks: %d (%s)" % (len(selected), ", ".join(c.id for c in selected)), flush=True)
    results = run_checks(ctx, selected, a.runs)
    soak = None
    if a.soak_minutes > 0:
        from . import soak as S
        ctx.use_transcript(Transcript(os.path.join(a.out, "transcripts", "soak.txt")))
        soak = S.run(ctx, a.soak_minutes)
    with open(os.path.join(a.out, "results.json"), "w", encoding="utf-8") as f:
        json.dump({"checks": results, "soak": soak, "firstLogins": sorted(ctx.first_logins)}, f, indent=1)
    for c in (ctx.admin, ctx.player):
        if c is not None:
            c.close()
    bad = sum(1 for r in results if r["result"] in ("FAIL", "ERROR"))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
