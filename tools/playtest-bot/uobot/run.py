"""Runs the checks (and the soak) against the scratch shard; Run-OvernightPlaytests.ps1 calls it (cc-P91).

    python -m uobot.run --secrets secrets.local.json --out <run folder> [--runs 3] [--only T-041,P64-1] [--soak-minutes 120]

Each check runs --runs times. 3/3 is PASS, 0/3 FAIL, anything between FLAKY; a check that cannot be set up is ERROR.
Every run writes its own transcript (transcripts/<id>-run<n>.txt). results.json is what report.py reads.

cc-P819: the game pause (uobot.gamepause, the Golem kit's Test-GameRunning.ps1). With --game-until (the overnight
runner: the night window's end, 06:30) it looks before each check and every soak minute; while Chase plays another game
on the game PC it finishes the step, closes its clients and waits (every --game-interval seconds), then goes on; still
playing at --game-until, the checks not run are SKIPPED and the soak's rest is skipped. Without --game-until (a Golem's
own run) it looks once at the start and, while a game runs, exits 4 with "waiting: Chase is playing <game>": the Golem
waits (Test-GameRunning.ps1 -Wait, in the background) and runs it again. --game-check '' turns the look off.
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
from . import gamepause

CLIENT_DATA = r"D:\ShatteredLegacy\client-data\classic-client"
CACHE = r"D:\UO\bot-shard\cache"
# An account's characters stay in the world for 5 minutes after it disconnects outside an inn
# (Server/Regions/Region.cs:200, DefaultLogoutDelay), and "account in use" refuses another character on it until then
# (ACC:160-170). So fresh characters rotate over these accounts, each rested before reuse.
POOL = ["slbot-player2", "slbot-player3", "slbot-player4"]
# cc-P762: New Haven starts (T-039, P64-1, P64-2) come from staff accounts, the only ones the new-character lock leaves
# New Haven to; the Admin makes each GameMaster on first use (make_staff).
STAFF_POOL = ["slbot-staff2", "slbot-staff3", "slbot-staff4"]
STAFF_MADE = "is a GameMaster account now"   # NewCharacterRules.BotStaffDone
REST_SECONDS = 330
# cc-P735: creation's start list (TutorialStart.Cities): 0 the isle, 1 New Haven ("The Bountiful Harvest Inn"). cc-P762: a
# player account sees index 1 as "New Haven (locked)", and any pick wakes on the isle.
ISLE_CITY, HAVEN_CITY = 0, 1
LOCKED_HAVEN = "New Haven (locked)"


class Ctx(object):
    def __init__(self, secrets, out, container):
        self.secrets = secrets
        self.out = out
        self.container = container
        self.cliloc = Cliloc(os.path.join(CLIENT_DATA, "Cliloc.enu"), CACHE)
        self.tr = Transcript(os.path.join(out, "transcripts", "session.txt"))
        self.admin = None
        self.player = None
        self.pool_rest = {a: 0.0 for a in POOL + STAFF_POOL}
        self.pool_lock = threading.Lock()
        # One admin client, shared: the soak's threads take this before any staff command, so two target cursors
        # never cross.
        self.admin_lock = threading.RLock()
        self.first_logins = {}      # account -> what the first login saw (P64-1)
        self.staff_ready = set()    # cc-P762: staff pool accounts made GameMaster this run
        self.fresh_count = 0
        self.serials = {}

    # -- the long-lived characters ------------------------------------------------------------------------------
    def use_transcript(self, tr):
        self.tr = tr
        for c in (self.admin, self.player):
            if c is not None:
                c.tr = tr

    def close_clients(self):
        """cc-P819: the long-lived clients closed cleanly (the game pause); ensure_admin/ensure_player open them again."""
        for c in (self.admin, self.player):
            if c is not None and c.closed is None:
                try:
                    c.close()
                except Exception:
                    pass
        self.admin = None
        self.player = None

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

    def fresh(self, race="human", female=False, label="fresh", city=ISLE_CITY, first_login=False):
        """A brand-new character on a rested pool account, through the real creation packet. Disconnect it with
        done() so the account can rest.

        cc-P762: every new player character starts on the Wakening Isle (newCharacters.newHavenStart is off) and is Human
        (newCharacters.races). city=HAVEN_CITY asks for creation's New Haven start, which only staff accounts keep: it
        uses the staff pool (STAFF_POOL, made GameMaster by the Admin's [BotStaffAccount on first use), so its characters
        are staff (blessed, in a staff robe). first_login=True prefers an account this run has not made a character on
        yet (P64-1)."""
        # Thread-safe: the soak asks for three at once. An account is marked busy (rest = forever) the moment it is
        # picked, and done() starts its rest.
        pool = STAFF_POOL if city == HAVEN_CITY else POOL
        while True:
            with self.pool_lock:
                now = time.time()
                ready = [a for a in pool if self.pool_rest[a] <= now]
                if first_login:
                    ready.sort(key=lambda a: a in self.first_logins)
                if ready:
                    acct = ready[0]
                    self.pool_rest[acct] = float("inf")
                else:
                    acct = None
                    pending = [self.pool_rest[a] for a in pool if self.pool_rest[a] > now]
                    wait = (min(pending) - now) if pending else 1.0
            if acct is None:
                self.tr.write("harness", "all pool accounts busy or resting; waiting %.0f s" % min(wait, 30))
                time.sleep(max(1.0, min(wait, 30)))
                continue
            if city == HAVEN_CITY and acct not in self.staff_ready:
                self.make_staff(acct)
            c = Client(acct, self.secrets.get("poolPassword", self.secrets["playerPassword"]), self.tr, self.cliloc, label)
            c.account = acct
            try:
                chars = c.login()
            except Exception:
                self.done(c)
                raise
            break
        self.fresh_count += 1
        first = acct not in self.first_logins
        slot = next((i for i, n in enumerate(chars) if not n), None)
        if slot is None:
            # cc-P224 (and cc-P223, which found the same): the pool's 3 accounts give 21 character slots, and done() never deleted the throwaway it made,
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
            c.create(name[:16], race=race, female=female, city=city)
        except Exception:
            self.done(c)
            raise
        c.name = name[:16]
        if first:
            # The first login of an account: what it was shown, for P64-1 (bulletin once per account per version).
            c.sleep(2.0)
            self.first_logins[acct] = {"gumps": list(c.gumps), "messages": list(c.messages)}
        return c

    def make_staff(self, acct):
        """cc-P762: a staff pool account, made GameMaster so its characters may start in New Haven (staff are exempt
        from the new-character lock). Its first login makes the account (the shard makes a missing one as a player); then
        the Admin raises it ([BotStaffAccount, test worlds only, slbot-staff accounts only). Nothing is left in the world,
        so the account is free again at once."""
        c = Client(acct, self.secrets.get("poolPassword", self.secrets["playerPassword"]), self.tr, self.cliloc, "staff")
        c.account = acct
        try:
            c.login()
        finally:
            c.close()
        with self.admin_lock:
            a = self.ensure_admin()
            said = a.command("[BotStaffAccount %s" % acct, 1.0)
        ok = any(STAFF_MADE in (m.get("text") or "") for m in said or [])
        self.tr.write("harness", "%s made GameMaster by the Admin: %s" % (acct, "yes" if ok else "NO (%r)" % (said,)))
        if not ok:
            with self.pool_lock:
                self.pool_rest[acct] = 0.0
            raise LoginFailed("could not make %s a staff account for a New Haven start" % acct)
        self.staff_ready.add(acct)

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


class GamePause(object):
    """cc-P819: the overnight runner's waits. script: the kit's check ('' none); until: HH:mm, the night's end."""

    def __init__(self, script, until, interval=600):
        self.script, self.until, self.interval = script, until, interval
        self.over = False      # still playing at `until`: what is left is skipped
        self.waits = []        # {"at", "game", "outcome"} for results.json and the report

    def active(self):
        return bool(self.until) and gamepause.available(self.script)

    def playing(self):
        if not self.active() or self.over:
            return ""
        playing, line = gamepause.now(self.script)
        return line if playing else ""

    def hold(self, ctx, where):
        """A game is running: close the clients, wait. True to go on, False when the night is over."""
        line = self.playing()
        if not line:
            return True
        print("game pause before %s: %s" % (where, line), flush=True)
        ctx.close_clients()
        ok = gamepause.wait(self.script, self.until, self.interval, who="overnight",
                            what="the overnight play tests", say=lambda l: print("  " + l, flush=True))
        self.waits.append({"at": time.strftime("%Y-%m-%dT%H:%M:%S"), "where": where, "line": line, "outcome": "resumed" if ok else "skipped"})
        if not ok:
            self.over = True
        return ok


def merge_soak(minutes, parts):
    """cc-P819: a soak split by game waits as one record (report.py reads one): events summed, samples, errors and loud
    lines in order, memory from the first and last parts; stoppedFor lists each stop; skipped when no part ran."""
    if not parts:
        return {"minutes": minutes, "events": {}, "samples": [], "errors": [], "loud": [], "loudCount": 0,
                "memStartMB": None, "memEndMB": None, "memMaxMB": None, "stoppedFor": [], "skipped": "a game was running until the night's end"}
    events = {}
    for p in parts:
        for k, v in p.get("events", {}).items():
            events[k] = events.get(k, 0) + v
    maxes = [p.get("memMaxMB") for p in parts if p.get("memMaxMB") is not None]
    return {"minutes": minutes, "events": events, "parts": len(parts),
            "samples": [s for p in parts for s in p.get("samples", [])], "errors": [e for p in parts for e in p.get("errors", [])],
            "loud": [l for p in parts for l in p.get("loud", [])][:200], "loudCount": sum(p.get("loudCount", 0) for p in parts),
            "memStartMB": parts[0].get("memStartMB"), "memEndMB": parts[-1].get("memEndMB"), "memMaxMB": max(maxes) if maxes else None,
            "stoppedFor": [p["stoppedFor"] for p in parts if p.get("stoppedFor")]}


def run_checks(ctx, selected, runs, pause=None):
    results = []
    tdir = os.path.join(ctx.out, "transcripts")
    for chk in selected:
        if pause is not None and (pause.over or not pause.hold(ctx, chk.id)):
            # cc-P819: the night ended while Chase played: this check and the rest are not run tonight
            results.append({"id": chk.id, "title": chk.title, "kind": chk.kind, "left_for_chase": chk.left_for_chase,
                            "result": "SKIPPED", "passes": 0, "runs": [], "why": "a game was running until the night's end"})
            print("%-10s SKIPPED: a game was running until the night's end" % chk.id, flush=True)
            continue
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
    # cc-P819: the game pause (see the top of this file)
    ap.add_argument("--game-check", default=gamepause.DEFAULT_SCRIPT, help="the kit's Test-GameRunning.ps1; '' for none")
    ap.add_argument("--game-until", default="", help="HH:mm: wait for a game to close up to then (the overnight runner)")
    ap.add_argument("--game-interval", type=int, default=600)
    a = ap.parse_args(argv)
    safety.configure(a.target)
    print("target: %s" % safety.describe(), flush=True)
    pause = GamePause(a.game_check, a.game_until, a.game_interval)
    if not a.game_until and a.game_check:
        playing, line = gamepause.now(a.game_check)
        if playing:
            print(line + ": no bot client connects on the game PC while he plays. Wait with D:\\UO\\golem\\Test-GameRunning.ps1 -Wait "
                  "(in the background; it ends when the game closes), then run this again.", flush=True)
            return 4
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
    results = run_checks(ctx, selected, a.runs, pause if pause.active() else None)
    soak = None
    if a.soak_minutes > 0:
        from . import soak as S
        ctx.use_transcript(Transcript(os.path.join(a.out, "transcripts", "soak.txt")))
        if pause.active():
            # cc-P819: the soak stops at a game (its clients finish their step and close), waits, and runs the minutes
            # left after; parts are kept in order. Still playing at the night's end: the rest is skipped.
            left, parts = a.soak_minutes, []
            while left > 0 and pause.hold(ctx, "the soak"):
                t0 = time.time()
                part = S.run(ctx, left, game=pause.playing)
                parts.append(part)
                left -= max(1, int(round((time.time() - t0) / 60)))
                if not part.get("stoppedFor"):
                    break
            soak = merge_soak(a.soak_minutes, parts)
        else:
            soak = S.run(ctx, a.soak_minutes)
    with open(os.path.join(a.out, "results.json"), "w", encoding="utf-8") as f:
        json.dump({"checks": results, "soak": soak, "firstLogins": sorted(ctx.first_logins), "gameWaits": pause.waits}, f, indent=1)
    for c in (ctx.admin, ctx.player):
        if c is not None:
            c.close()
    bad = sum(1 for r in results if r["result"] in ("FAIL", "ERROR"))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
