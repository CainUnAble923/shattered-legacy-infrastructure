"""The soak (cc-P91 section 5): a few bot characters doing ordinary repeated things for up to two hours while the
harness watches the scratch shard for exceptions, crashes, save errors and memory growth.

  walker  - walks a loop around New Haven's square and says something now and then
  miner   - [SetSkill Mining 100, a pickaxe; digs the same mountain spot over and over, and smelts at a forge
  fighter - [SetSkill Swords/Tactics/Anatomy 100, a broadsword; the admin spawns a mongbat beside it whenever it has
            nothing to fight, and it attacks
Every minute: docker stats (memory), docker logs since the last look (grepped for Exception, Error, crash, save).
AFK farming is allowed on this shard, so the bots running unattended is not itself a finding.
"""
import json
import re
import subprocess
import threading
import time

from . import packets as P

LOUD = re.compile(r"Exception|\bError\b|FATAL|crash|Crash|save fail|Save fail|Unhandled|stack trace", re.I)
NEW_HAVEN_LOOP = ["e"] * 6 + ["s"] * 6 + ["w"] * 6 + ["n"] * 6


def _mem(container):
    try:
        out = subprocess.run(["docker", "stats", "--no-stream", "--format", "{{.MemUsage}}|{{.CPUPerc}}", container],
                             capture_output=True, text=True, timeout=60).stdout.strip()
        return out
    except Exception as e:
        return "stats failed: %s" % e


def _mb(text):
    m = re.match(r"([\d.]+)\s*(KiB|MiB|GiB)", text or "")
    if not m:
        return None
    v = float(m.group(1))
    return v / 1024 if m.group(2) == "KiB" else v * 1024 if m.group(2) == "GiB" else v


def run(ctx, minutes):
    a = ctx.ensure_admin()
    stop = time.time() + minutes * 60
    events = {"walker_steps": 0, "miner_digs": 0, "miner_smelts": 0, "fighter_kills": 0, "fighter_swings": 0}
    errors = []
    actors = []

    def walker():
        c = ctx.fresh(race="human", female=False, label="walker")
        actors.append(c)
        i = 0
        while time.time() < stop and c.closed is None:
            events["walker_steps"] += c.walk(NEW_HAVEN_LOOP[i % len(NEW_HAVEN_LOOP)], 1, pause=0.3)
            i += 1
            if i % 50 == 0:
                c.say("still walking, %d steps" % events["walker_steps"])
                c.keepalive()

    def miner():
        c = ctx.fresh(race="gargoyle", female=False, label="miner")
        actors.append(c)
        from .checks import give, place_forge, staff_target
        with ctx.admin_lock:
            staff_target(a, "[SetSkill Mining 100", c)
            pick = give(a, c, "Pickaxe")
            forge = place_forge(a, c)
        x, y, z = c.pos[:3]
        while time.time() < stop and c.closed is None:
            c.dclick(pick)
            if c.wait_target(5):
                # Dig at the character's own feet: on a spot with no ore the server says so, which is fine for a soak.
                c.target_xyz(x, y, z)
                events["miner_digs"] += 1
            c.sleep(2.0)
            ores = [s for s, it in c.items.items() if it.get("container") == c.backpack() and 0x19B7 <= (it.get("graphic") or 0) <= 0x19BA]
            if ores:
                c.dclick(ores[0])
                if c.wait_target(5):
                    c.target_obj(forge)
                    events["miner_smelts"] += 1
                c.sleep(1.5)
            if events["miner_digs"] % 30 == 0:
                c.keepalive()

    def fighter():
        c = ctx.fresh(race="elf", female=True, label="fighter")
        actors.append(c)
        from .checks import give, near, staff_target
        with ctx.admin_lock:
            for sk in ("Swords", "Tactics", "Anatomy"):
                staff_target(a, "[SetSkill %s 100" % sk, c)
            blade = give(a, c, "Broadsword")
        c.send(P.lift(blade, 1))
        c.sleep(0.4)
        c.send(P.equip(blade, 0, c.serial))
        while time.time() < stop and c.closed is None:
            with ctx.admin_lock:
                near(a, c)
                a.command("[add Mongbat", 0.2)
                if a.wait_target(5):
                    x, y, z = c.pos[:3]
                    a.target_xyz(x + 1, y, z)
            c.sleep(1.5)
            mongbats = [s for s, m in c.mobiles.items() if m.get("body") == 39 and s != c.serial]
            if mongbats:
                c.send(P.war_mode(True))
            for s in mongbats:
                c.send(P.attack(s))
                events["fighter_swings"] += 1
                c.wait(lambda: s not in c.mobiles, 20)
                if s not in c.mobiles:
                    events["fighter_kills"] += 1
            c.keepalive()

    threads = []
    for fn in (walker, miner, fighter):
        def wrap(fn=fn):
            try:
                fn()
            except Exception as e:
                errors.append("%s: %s: %s" % (fn.__name__, type(e).__name__, e))
        t = threading.Thread(target=wrap, daemon=True)
        t.start()
        threads.append(t)

    samples = []
    loud = []
    last = time.strftime("%Y-%m-%dT%H:%M:%S")
    t0 = time.time()
    while time.time() < stop:
        time.sleep(60)
        mem = _mem(ctx.container)
        now = time.strftime("%Y-%m-%dT%H:%M:%S")
        logs = ctx.docker_logs_since(last)
        last = now
        for line in logs.splitlines():
            clean = re.sub(r"\x1b\[[0-9;]*m", "", line)
            if LOUD.search(clean):
                loud.append(clean[:400])
        running = subprocess.run(["docker", "inspect", "--format", "{{.State.Status}}", ctx.container],
                                 capture_output=True, text=True).stdout.strip()
        samples.append({"minute": round((time.time() - t0) / 60), "mem": mem, "state": running, **events})
        ctx.tr.write("soak", "minute %d: %s, %s, %s" % (samples[-1]["minute"], running, mem, json.dumps(events)))
        if running != "running":
            errors.append("the scratch shard is %s at minute %d" % (running, samples[-1]["minute"]))
            break
        with ctx.admin_lock:
            a.keepalive()
    for t in threads:
        t.join(30)
    for c in actors:
        ctx.done(c)
    mems = [_mb(s["mem"]) for s in samples if _mb(s["mem"])]
    return {"minutes": minutes, "samples": samples, "events": events, "errors": errors, "loud": loud[:200],
            "loudCount": len(loud), "memStartMB": mems[0] if mems else None, "memEndMB": mems[-1] if mems else None,
            "memMaxMB": max(mems) if mems else None}
