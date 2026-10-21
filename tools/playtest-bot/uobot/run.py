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
from . import packets as P
from . import checks as C
from . import safety

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
        self.revive(self.player)
        return self.player

    GHOST_BODIES = (0x192, 0x193, 0x25F, 0x260, 0x2B6, 0x2B7)

    def revive(self, c):
        """cc-P224: a check that gets the long-lived player killed (a dragon did, 2026-10-20) must not fail every check
        after it with "I am dead and cannot do that.": the admin [Resurrect]s a ghost before the character is handed out."""
        if c is None or c.closed is not None or c.body not in self.GHOST_BODIES:
            return False
        a = self.ensure_admin()
        a.command("[go 0x%X" % c.serial, 1.0)
        a.command("[Resurrect", 0.2)
        if a.wait_target(6):
            a.target_obj(c.serial)
        back = bool(c.wait(lambda: c.body not in self.GHOST_BODIES, 8))
        self.tr.write("harness", "%s was a ghost (body 0x%X): [Resurrect by the admin, back %s" % (c.label, c.body, back))
        c.send(P.status_request(c.serial, 4))
        c.sleep(0.5)
        return back

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
        c = Client(acct, self.secrets.get("poolPassword", self.secrets["playerPassword"]), self.tr, self.cliloc, label)
        c.account = acct
        first = acct not in self.first_logins
        try:
            chars = c.login()
        except Exception:
            self.done(c)
            raise
        slot = next((i for i, n in enumerate(chars) if not n), None)
        if slot is None:
            # cc-P224: the pool's 3 accounts give 21 character slots, and done() never deleted the throwaway it made,
            # so a run with more than 21 fresh() calls (the nightly set plus the cc-P224 checks, over 3 runs) ran the
            # accounts dry and every later fresh() ERRORed with "no free slot". The character in slot 0 has rested at
            # least REST_SECONDS, which is past the 5-minute logout delay, so it is fully out of the world and
            # deletable. Delete it and reuse its slot. Probed on this PC's test shard 2026-10-21: 0x83 on a full
            # account returns a 0x86 list with that slot now empty.
            self.tr.write("harness", "account %s full (7 characters); deleting slot 0 (%r) to reuse it" % (acct, chars[0]))
            c.delete_character(0)
            if not c.wait(lambda: len(c.chars) > 0 and not c.chars[0], 10):
                raise LoginFailed("account %s is full and slot 0 would not delete (chars %s)" % (acct, c.chars))
            chars = c.chars
            slot = 0
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


def use_golem_accounts(ctx, golem):
    """cc-P215: a Golem running the bot uses its own accounts ('<golem>-admin', '<golem>-player', their passwords from
    Get-GolemTestAccount.ps1). The scratch world's account wipe leaves only slbot-admin, so that admin first seeds every
    Golem's accounts from golem-accounts.json in the world folder ([SeedGolemAccounts, test shard only), then the checks
    log in as the Golem. The fresh-character pool keeps slbot-player2 to 4 and their own password."""
    from .golemcheck import account
    admin_user, admin_pw = account(golem, "admin")
    player_user, player_pw = account(golem, "player")
    seeder = Client(ctx.secrets["admin"], ctx.secrets["adminPassword"], ctx.tr, ctx.cliloc, "seed")
    chars = seeder.login()
    if chars and chars[0]:
        seeder.play(0)
    else:
        seeder.create("Bot Admin", race="human", female=False)
    lines = seeder.command("[SeedGolemAccounts", settle=3.0)
    seeder.close()
    said = " | ".join(m["text"] for m in lines if "[GolemAccounts]" in (m["text"] or ""))
    ctx.tr.write("harness", "golem accounts seeded for %s: %s" % (golem, said or "(no reply)"))
    ctx.secrets = dict(ctx.secrets, poolPassword=ctx.secrets["playerPassword"], admin=admin_user, adminPassword=admin_pw,
                       player=player_user, playerPassword=player_pw)
    print("accounts: %s's own (%s, %s)" % (golem, admin_user, player_user), flush=True)


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--secrets", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--runs", type=int, default=3)
    ap.add_argument("--only", default="")
    ap.add_argument("--container", default="sl-modernuo-bot")
    ap.add_argument("--soak-minutes", type=int, default=0)
    ap.add_argument("--no-golem", action="store_true", help="keep slbot-admin and slbot-player even when GOLEM_NAME is set")
    # cc-P224: "scratch" (127.0.0.1:2595, the overnight runner's shard) or "test" (127.0.0.1:2594, this PC's own test
    # shard, started by Start-TestShard.ps1). Nothing else; safety.py refuses the live port in both.
    ap.add_argument("--target", default="scratch", choices=sorted(safety.TARGETS))
    a = ap.parse_args(argv)
    safety.configure(a.target)
    print("target: %s" % safety.describe(), flush=True)
    os.makedirs(os.path.join(a.out, "transcripts"), exist_ok=True)
    with open(a.secrets, "r", encoding="utf-8") as f:
        secrets = json.load(f)
    ctx = Ctx(secrets, a.out, a.container)
    golem = os.environ.get("GOLEM_NAME", "") if not a.no_golem else ""
    if golem:
        use_golem_accounts(ctx, golem)
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
