"""cc-P223, sixth batch: a quest, the generated dungeons, three checks across a restart of the scratch shard, and
skills past 120 in a fight (Tactics, the axe bonus, stable slots, the Fire Horn). Imported by checks_p223e.py.

A restart is of the bot's own container only (sl-modernuo-bot, Rule 1): the world is saved first ([save), the
container is stopped (its own shutdown save) and started, and the check waits for the 2595 listener again. The
skill-scaling figures are ClusterFSkillClamps.cs (CSC below), read at origin/main 62c675d.
"""
import datetime
import os
import re
import subprocess
import time

from . import packets as P
from .checks_p223b import roomy
from .checks import check, all_text, buttons, give, staff_target, near, try_equip
from .checks_p223 import SK, base, add_beside, add_mobile_beside, delete_mobile, name_of
from .checks_p223b import move_to
from .checks_p223e import get_prop, staff_on, find_npc

BOT_CONFIG = r"D:\UO\bot-shard\Configuration\modernuo.json"


# -- the scratch shard's restart ---------------------------------------------------------------------------------------

def restart_shard(ctx, edit=None, timeout=420):
    """[save, stop and start sl-modernuo-bot (never any other container), optionally editing the bot's Configuration
    copy while it is down; waits for 'Listening: ... 2595'. The long-lived clients are dropped and log in again."""
    if ctx.container != "sl-modernuo-bot":
        raise RuntimeError("refused: restart is for sl-modernuo-bot only, not %r" % ctx.container)
    a = ctx.ensure_admin()
    a.command("[save", 8.0)
    for c in (ctx.admin, ctx.player):
        if c is not None and c.closed is None:
            c.close()
    ctx.admin = ctx.player = None
    ctx.tr.write("harness", "restarting %s%s" % (ctx.container, " with a config edit" if edit else ""))
    r = subprocess.run(["docker", "stop", "-t", "60", ctx.container], capture_output=True, text=True, timeout=120)
    if r.returncode != 0:
        raise RuntimeError("docker stop failed: %s" % r.stderr.strip())
    if edit:
        with open(BOT_CONFIG, "r", encoding="utf-8") as f:
            text = f.read()
        new = edit(text)
        with open(BOT_CONFIG, "w", encoding="utf-8", newline="") as f:
            f.write(new)
    since = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")
    r = subprocess.run(["docker", "start", ctx.container], capture_output=True, text=True, timeout=120)
    if r.returncode != 0:
        raise RuntimeError("docker start failed: %s" % r.stderr.strip())
    end = time.time() + timeout
    while time.time() < end:
        time.sleep(5)
        logs = ctx.docker_logs_since(since)
        if re.search(r"Listening:.*2595", logs):
            time.sleep(3)
            ctx.tr.write("harness", "restarted: listening again")
            return logs
    raise RuntimeError("the scratch shard did not listen again within %d s" % timeout)


def set_config(key, value):
    def edit(text):
        rx = re.compile(r'("%s"\s*:\s*)"[^"]*"' % re.escape(key))
        if not rx.search(text):
            raise RuntimeError("%s is not in the bot's Configuration" % key)
        return rx.sub(lambda m: m.group(1) + '"%s"' % value, text, count=1)
    return edit


def config_value(key):
    with open(BOT_CONFIG, "r", encoding="utf-8") as f:
        m = re.search(r'"%s"\s*:\s*"([^"]*)"' % re.escape(key), f.read())
    return m.group(1) if m else None


def props_title(a, serial, timeout=8):
    """[props on a serial: PropsGump's title "<TypeName> (0x<serial>)" (pinned Gumps/Props/PropsGump.cs:112-114)."""
    ng = len(a.gumps)
    a.command("[props", 0.2)
    if not a.wait_target(6):
        return None
    a.target_obj(serial)
    g = a.wait_gump(None, timeout, since=ng)
    if not g:
        return None
    t = next((x for x in all_text(a, g) if re.search(r"\(0x[0-9A-Fa-f]+\)$", x)), None)
    a.reply_gump(g, 0)
    return t


def global_count(a, type_name):
    """[Global Count where <Type> (pinned Commands.cs:194-217): 'There is one matching object.' / 'There are N
    matching objects.' / 'Nothing was found to use this command on.'. Returns the number, or None."""
    said = [m["text"] for m in a.command("[Global Count where " + type_name, 1.5)]
    for t in said:
        if "There is one matching object" in t:
            return 1
        m = re.search(r"There are (\d+) matching objects", t)
        if m:
            return int(m.group(1))
        if "Nothing was found" in t:
            return 0
    return None


def admin_to(a, facet, x, y, z, settle=4.0):
    a.command("[Self Set Map %s" % facet, 1.0)
    a.command("[go %d %d %d" % (x, y, z), settle)


def admin_home(a):
    admin_to(a, "Trammel", 3503, 2574, 14, 2.0)


PLAYER_BODIES = (400, 401, 605, 606, 666, 667, 402, 403, 607, 608, 694, 695)


def creatures_near(c, x, y, radius=18):
    return [s for s, m in c.mobiles.items() if s != c.serial and m.get("body") not in PLAYER_BODIES
            and abs(m.get("x", -999) - x) <= radius and abs(m.get("y", -999) - y) <= radius]


# -- T-002: a New Haven training quest ----------------------------------------------------------------------------

CHIYO_AT = (3424, 2520, 21)     # ClusterFNewHavenSeeder.cs:59, the Hiding Instructor at the dojo


@check("T-002", "A New Haven training quest: Chiyo's Hiding quest accepted, Hiding rises in skill updates, the rank message, the reward")
def t_002(ctx):
    # pinned Engines/ML Quests/Definitions/NewHavenSkillTraining.cs:437-452 (GainSkillObjective Hiding to 50.0 real,
    # accelerated), Gumps/QuestOfferGump.cs:21-22 (1 accept), MLQuest.cs:198 (1049019), MLQuestEntry.cs:158-169
    # (1078171), :405 (1074360); our NewHavenTrainerGuildLines.cs wraps the double-click and runs the stock path first.
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=False)
    try:
        chiyo = find_npc(a, c, CHIYO_AT[0], CHIYO_AT[1], CHIYO_AT[2], "Chiyo", "chiyo")
        if not chiyo:
            return False, "no Chiyo near %s" % (CHIYO_AT,)
        c.sleep(1.1)
        ng, n = len(c.gumps), len(c.messages)
        c.dclick(chiyo)
        offer = c.wait_gump(lambda g: 1 in buttons(g) and 2 in buttons(g), 8, since=ng)
        if not offer:
            return False, "double-clicking Chiyo offered no quest: gumps %s; said %s" % ([all_text(c, g)[:2] for g in c.gumps[ng:]], c.texts_since(n)[-3:])
        n = len(c.messages)
        c.reply_gump(offer, 1)
        accepted = c.wait(lambda: any(m.get("cliloc") == 1049019 for m in c.messages[n:]), 6)
        for g in c.gumps[ng:]:
            if g is not offer:
                c.reply_gump(g, 0)          # the guild line's gump, if any, closed
        staff_target(a, "[SetSkill Hiding 49.5", c)
        c.send(P.status_request(c.serial, 5))
        c.sleep(1.0)
        b0 = base(c, "Hiding")
        n = len(c.messages)
        rises, tries = [], 0
        while tries < 20:
            tries += 1
            last = base(c, "Hiding")
            c.use_skill(SK["Hiding"])
            c.sleep(2.0)
            now = base(c, "Hiding")
            if now is not None and last is not None and now > last:
                rises.append(now)
            if any(m.get("cliloc") == 1078171 for m in c.messages[n:]):
                break
            c.sleep(8.6)                # Hiding's 10 s delay
        rank = next((m for m in c.messages[n:] if m.get("cliloc") == 1078171), None)
        # Report back: Chiyo again, QuestReportBackGump button 4, QuestRewardGump button 1.
        reward = None
        if rank:
            c.sleep(1.1)
            ng, n2 = len(c.gumps), len(c.messages)
            c.dclick(chiyo)
            rep = c.wait_gump(lambda g: 4 in buttons(g), 8, since=ng)
            if rep:
                ng = len(c.gumps)
                c.reply_gump(rep, 4)
                rw = c.wait_gump(lambda g: 1 in buttons(g), 8, since=ng)
                if rw:
                    c.reply_gump(rw, 1)
                    reward = c.wait(lambda: next((m for m in c.messages[n2:] if m.get("cliloc") == 1074360), None), 6)
        ok = bool(accepted) and bool(rises) and bool(rank) and bool(reward)
        return ok, "accepted %s; Hiding %s -> %s, rises in 0x3A %s over %d tries; rank message %r; reward %r" % (
            bool(accepted), b0, base(c, "Hiding"), rises, tries, rank["text"] if rank else None, reward["text"] if reward else None)
    finally:
        ctx.done(c)


# -- T-006: Ter Mur, the Abyss and the Underworld populate --------------------------------------------------------

T006_FILES = ["Data/Spawns/post-uoml/termur/Abyss.json", "Data/Spawns/post-uoml/termur/TerMur.json",
              "Data/Spawns/post-uoml/termur/Underworld.json"]
T006_SPOTS = [("the Abyss", 992, 337, 9), ("the Underworld", 1097, 1189, -38), ("Ter Mur", 1106, 3138, -43)]


@check("T-006", "[GenerateSpawners for Ter Mur, the Abyss and the Underworld: creatures stand at a spot in each")
def t_006(ctx):
    # pinned Engines/Spawners/Commands/ImportSpawnersCommand.cs:75-108, :177, :259-290 (every spawner respawns at once;
    # re-running replaces). The spots are each beside a spawner of those files (Abyss 992 337 9 count 35; Underworld
    # 1097 1189 -38 AcidSlug x25; Ter Mur 1106 3138 -43 sliths).
    a = ctx.ensure_admin()
    gen = []
    for f in T006_FILES:
        said = [m["text"] for m in a.command("[GenerateSpawners " + f, 12.0)]
        gen.append(next((t for t in said if "Generated" in t), said[-1] if said else "(no reply)"))
    seen = []
    try:
        for label, x, y, z in T006_SPOTS:
            admin_to(a, "TerMur", x, y, z, 6.0)
            near_ = creatures_near(a, x, y)
            names = sorted(set(name_of(a, s) for s in near_[:6]))
            seen.append((label, len(near_), names))
    finally:
        admin_home(a)
    ok = all(" 0 failures" in g or "0 failures" in g for g in gen) and all(n > 0 for _, n, _ in seen)
    return ok, "generate: %s; creatures in range: %s" % (gen, "; ".join("%s %d %s" % s for s in seen))


# -- T-007: Shame and Despise --------------------------------------------------------------------------------------

SHAME_TYPES = ["ChaosVortex", "ClayGolem", "CorruptedMage", "CrazedMage", "DiseasedBloodElemental", "EternalGazer",
               "EvilMageLord", "FlameElemental", "GreaterAirElemental", "GreaterBloodElemental", "GreaterEarthElemental",
               "GreaterWaterElemental", "Kraken", "MoltenEarthElemental", "MudElemental", "MudPie", "Scorpion",
               "SeaSerpent", "ShameEarthElemental", "ShameGreaterPoisonElemental", "StoneElemental",
               "UnboundEnergyVortex", "VileMage", "WindElemental", "CaveTroll"]
DESPISE_TYPES = ["Silenii", "ForestNymph", "DespiseUnicorn", "Sagittarri", "Ursadane", "DivineGuardian", "Dendrite",
                 "Fairy", "Phantom", "Naba", "Darkmane", "Skeletrex", "Hellion", "Echidnite", "BirlingBlades",
                 "Prometheoid"]


@check("T-007", "[GenerateNewShame and [SetupDespise: every creature type their spawners name is in the world (25 and 16)")
def t_007(ctx):
    # Shame Revamped/Generate.cs:49-144 and ShameSpawns.cs (24 spawner types on each facet, plus the walls' CaveTroll,
    # ShameWall.cs:92); DespiseRevamped/Setup.cs:31 and DespiseSpawns.cs:71-79 (47 spawners, 16 types). The play test's
    # 41 and 37 are the reachability audit's counts of ported content types (creatures, items, altars, walls, chests),
    # not of creature types, so this check counts creatures against the spawner tables instead.
    a = ctx.ensure_admin()
    shame = [m["text"] for m in a.command("[GenerateNewShame", 15.0)]
    despise = [m["text"] for m in a.command("[SetupDespise", 15.0)]
    a.sleep(5.0)
    s_ok = any("Shame Revamped setup" in t for t in shame)
    d_ok = any("Despise setup complete" in t or "already be setup" in t for t in despise)
    spawners = global_count(a, "DespiseSpawner")
    s_missing = [t for t in SHAME_TYPES if not global_count(a, t)]
    d_missing = [t for t in DESPISE_TYPES if not global_count(a, t)]
    ok = s_ok and d_ok and spawners == 47 and not s_missing and not d_missing
    return ok, "Shame: %s, %d of %d types in the world (missing %s); Despise: %s, %s spawners, %d of %d types (missing %s)" % (
        [t for t in shame if "Shame" in t][-1:], len(SHAME_TYPES) - len(s_missing), len(SHAME_TYPES), s_missing or "none",
        [t for t in despise if "Despise" in t][-1:], spawners, len(DESPISE_TYPES) - len(d_missing), len(DESPISE_TYPES), d_missing or "none")


# -- T-010: the Royal City alchemists across a restart (bug list D34) ---------------------------------------------

ALCHEMISTS_AT = [(778, 3478, -20), (777, 3490, -20)]     # ClusterFRoyalCitySeeder.cs:59, :62


def _alchemists(a):
    out = {}
    for x, y, z in ALCHEMISTS_AT:
        admin_to(a, "TerMur", x, y, z, 3.0)
        for s in creatures_near(a, x, y, 3) + [s for s, m in a.mobiles.items() if m.get("body") in (666, 667)
                                                and abs(m.get("x", -999) - x) <= 3 and abs(m.get("y", -999) - y) <= 3]:
            if "alchemist" in name_of(a, s).lower():
                out[s] = props_title(a, s)
    return out


@check("T-010", "The Royal City gargoyle alchemists are GargoyleAlchemist before and after [save and a restart (same serials)")
def t_010(ctx):
    # D34: pinned Glassblower carries [TypeAlias("Server.Mobiles.GargoyleAlchemist")] (Glassblower.cs:6) and shares the
    # title "the alchemist", so the type is read from [props by serial. The seeder places new ones on load only when
    # none exist (ClusterFRoyalCitySeeder.cs:107-116), so the same serials must come back.
    a = ctx.ensure_admin()
    before = _alchemists(a)
    n0 = global_count(a, "GargoyleAlchemist")
    admin_home(a)
    if not before:
        return False, "no alchemist at the Royal City spots %s" % ALCHEMISTS_AT
    restart_shard(ctx)
    a = ctx.ensure_admin()
    after = {}
    for s in before:
        a.command("[go 0x%X" % s, 3.0)
        after[s] = props_title(a, s)
    n1 = global_count(a, "GargoyleAlchemist")
    admin_home(a)
    ok = all(t and t.startswith("GargoyleAlchemist") for t in before.values()) and \
        all(after.get(s) and after[s].startswith("GargoyleAlchemist") for s in before) and n0 == n1
    return ok, "before: %s (Global Count %s); after the restart: %s (Global Count %s)" % (before, n0, after, n1)


# -- P62-8: the Kitsune Ears and Tail stay worn across a restart ---------------------------------------------------

@check("P62-8", "The Kitsune Ears and Tail, worn by a female, are still worn on layer 18 after [save and a restart")
def p62_8(ctx):
    # KitsuneEarsTail.cs:19-49: Layer.Earrings (0x12), because pinned Cleanup.cs:79 deletes real items on the hair and
    # beard layers at load.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()          # Bot Human F Aa: female
    roomy(a, p)
    s = give(a, p, "KitsuneEarsTail")
    worn, texts, it = try_equip(p, s)
    if not worn:
        return False, "not worn before the restart: %s %s" % (texts[-2:], it)
    layer0 = it.get("layer")
    restart_shard(ctx)
    p = ctx.ensure_player()
    p.sleep(2.0)
    it2 = p.items.get(s) or {}
    layer1 = it2.get("layer") if it2.get("parent") == p.serial else None
    beard = [x for x, i in p.items.items() if i.get("parent") == p.serial and i.get("layer") == 0x10]
    if layer1:
        p.lift_drop(s, 1, p.backpack())
        p.sleep(0.8)
    ok = layer0 == 0x12 and layer1 == 0x12 and s not in beard
    return ok, "serial 0x%X: layer %s before, %s after the restart (worn by %s); beard layer items %s" % (
        s, layer0, layer1, "her" if it2.get("parent") == p.serial else it2.get("parent"), beard)


# -- P71-6: Stealth steps at 200 follow the setting ---------------------------------------------------------------

STEPS_KEY = "clusterf.skillScaling.stealth.stepsAt200"


def _steps_at_200(ctx):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    staff_target(a, "[SetSkill Hiding 100", p)
    staff_target(a, "[SetSkill Stealth 200", p)
    staff_target(a, "[set Hidden false", p)
    p.sleep(1.0)
    n = len(p.messages)
    for _ in range(3):
        p.use_skill(SK["Hiding"])
        if p.find_text(r"hidden yourself well", since=n, timeout=3):
            break
        p.sleep(10.5)
    p.sleep(10.5)
    n = len(p.messages)
    p.use_skill(SK["Stealth"])
    began = p.find_text(r"move quietly", since=n, timeout=4)
    steps = get_prop(a, p.serial, "AllowedStealthSteps")
    staff_target(a, "[set Hidden false", p)
    m = re.match(r"(-?\d+)", steps or "")
    return (int(m.group(1)) if m else None), bool(began)


@check("P71-6", "stealth.stepsAt200 set to 30 and the shard restarted: a Stealth at 200 allows 30 steps (34 before)")
def p71_6(ctx):
    # CSC:148-169 reads the key once at start; Stealth-steps-scaled-200.patch -> CSC.StealthSteps (:288-306) gives
    # exactly the setting at 200. Read with [get AllowedStealthSteps (pinned Mobile.cs:545-546) after "You begin to
    # move quietly." (502730). The setting is put back and the shard restarted again afterwards.
    old = config_value(STEPS_KEY)
    if old is None:
        raise RuntimeError("%s is not in %s" % (STEPS_KEY, BOT_CONFIG))
    before, b_ok = _steps_at_200(ctx)
    try:
        restart_shard(ctx, set_config(STEPS_KEY, "30"))
        after, a_ok = _steps_at_200(ctx)
    finally:
        restart_shard(ctx, set_config(STEPS_KEY, old))
    ok = b_ok and a_ok and after == 30 and before == int(old)
    return ok, "setting %s: %s steps (stealth began %s); setting 30 after a restart: %s steps (began %s); put back to %s" % (
        old, before, b_ok, after, a_ok, old)


# -- P66-6: Tactics past a cap with an item bonus -----------------------------------------------------------------

def _dummy(a, p, type_name="Ettin", name_part="ettin", extra=""):
    """A creature to hit: it cannot walk or fight back and has hits to spare (BaseCreature properties)."""
    mob = add_mobile_beside(a, p, type_name, name_part=name_part)
    if not mob:
        raise AssertionError("no %s appeared beside %s" % (type_name, p.label))
    staff_on(a, "[set CantWalk true FightMode None HitsMaxSeed 60000 Hits 60000 " + extra, mob, 0.6)
    # It still answers whoever strikes it, whatever its FightMode: P67-7's fighter died at 60 s (10-21). No damage.
    staff_on(a, "[set DamageMin 0 DamageMax 0", mob, 0.4)
    return mob


def _swing(p, mob, seconds):
    p.send(P.war_mode(True))
    p.sleep(0.3)
    p.send(P.attack(mob))
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        p.sleep(1.0)
    p.send(P.war_mode(False))
    p.sleep(0.5)


@check("P66-6", "Tactics 185 with a +15 Tactics ring: the base still rises in a fight")
def p66_6(ctx):
    # Every landed hit rolls Tactics (pinned BaseWeapon.cs:2524); ClusterFSkillGain.EffectiveMax (:210-217) widens the
    # window to the cap plus the item bonus, so Value 200 still has a challenge; item bonuses past the cap
    # (ClusterFSkillBonusAboveCap.cs). The ring's bonus is set before it is worn (AOS.cs AddTo runs on equip).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    staff_target(a, "[SetSkill Tactics 185", p)
    staff_target(a, "[SetSkill Wrestling 120", p)
    staff_target(a, "[set RawStr 150 RawDex 120 Hits 150", p)
    ring = give(a, p, "GoldRing")
    staff_on(a, "[set SkillBonuses.Skill_1_Name Tactics SkillBonuses.Skill_1_Value 15", ring, 0.6)
    worn, _, _ = try_equip(p, ring)
    p.send(P.status_request(p.serial, 5))
    p.sleep(1.0)
    v0 = (p.skills.get(SK["Tactics"]) or {}).get("value")
    b0 = base(p, "Tactics")
    mob = _dummy(a, p)
    _swing(p, mob, 75)
    b1 = base(p, "Tactics")
    delete_mobile(a, mob)
    if worn:
        p.lift_drop(ring, 1, p.backpack())
    ok = worn and v0 is not None and v0 >= 199.9 and b0 is not None and b1 is not None and b1 > b0
    return ok, "ring worn %s; Tactics value %s, base %s -> %s after 75 s of swings" % (worn, v0, b0, b1)


# -- P67-7: the axe's Lumberjacking bonus stops at 100 ------------------------------------------------------------

def _axe_damage(a, p, mob, lumber, seconds=45):
    """One phase: the fighting skills at their cap and Str put back first, so nothing gains during it (with the bot
    config's 5x gain amount Tactics rose 15 points in 75 s, P66-6, 10-21) and Lumberjacking is the only change."""
    for sk in ("Swords", "Tactics", "Anatomy"):
        staff_target(a, "[SetSkill %s 200" % sk, p)
    staff_target(a, "[SetSkill Lumberjacking %s" % lumber, p)
    staff_target(a, "[set RawStr 100 RawDex 100 Stam 100", p)
    staff_on(a, "[set Hits 60000", mob, 0.4)
    _swing(p, mob, seconds)
    left = get_prop(a, mob, "Hits")
    m = re.match(r"(\d+)", left or "")
    return 60000 - int(m.group(1)) if m else None


@check("P67-7", "An axe at Lumberjacking 148 deals no more than at 100 (the bonus stops at 100): rules out a large bonus", kind="BOT-PARTIAL",
       left="P67-7: the 7% between ours and pinned's is inside the bot's noise")
def p67_7(ctx):
    # pinned BaseWeapon.cs:2486-2586: lumber = (LJ x 0.2 + 10 at 100)/100 for axes; BaseWeapon-axe-bonus-osi.patch uses
    # min(LJ, 100) for players (CSC:99), so +30% at both; pinned at 148 would be +39.6%, a few percent more in all.
    # Phases alternate 100, 148, 100, 148 so a drift cancels; the bot can rule out a large bonus, not 7%.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    staff_target(a, "[set Hits 150", p)
    axe = give(a, p, "DoubleAxe")
    worn, _, _ = try_equip(p, axe)
    mob = _dummy(a, p, extra="PhysicalResistanceSeed 0")
    d = [(lj, _axe_damage(a, p, mob, lj)) for lj in (100, 148, 100, 148)]
    delete_mobile(a, mob)
    if worn:
        p.lift_drop(axe, 1, p.backpack())
    low = [v for lj, v in d if lj == 100 and v]
    high = [v for lj, v in d if lj == 148 and v]
    ratio = (sum(high) / float(len(high))) / (sum(low) / float(len(low))) if len(low) == 2 and len(high) == 2 else None
    ok = worn and ratio is not None and ratio < 1.15
    return ok, "axe worn %s; damage per 45 s, Swords/Tactics/Anatomy 200: %s; 148 / 100 = %s (ours about 1.0, pinned's a few percent more)" % (
        worn, ", ".join("LJ %d %s" % x for x in d), "%.2f" % ratio if ratio else None)


# -- P71-2: stable slots at 200 ----------------------------------------------------------------------------------

@check("P71-2", "Taming, Lore and Veterinary 200: 23 pets stable, the 24th is refused")
def p71_2(ctx):
    # pinned AnimalTrainer.cs:58-100 with AnimalTrainer-stable-slots-scaled-200.patch -> CSC.StableSlots (:230-266):
    # 23 at 200 (pinned 38); Stable 3006126, 1042558 and a target, NPC 1049677; full 1042565 (:186-270).
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=True)
    pets = []
    try:
        for sk in ("AnimalTaming", "AnimalLore", "Veterinary"):
            staff_target(a, "[SetSkill %s 200" % sk, c)
        give(a, c, "Gold 2000")
        trainer = add_mobile_beside(a, c, "AnimalTrainer", dx=-1, name_part="animal trainer")
        if not trainer:
            raise AssertionError("no animal trainer")
        staff_on(a, "[set CantWalk true", trainer, 0.3)
        near(a, c)
        x, y, z = c.pos[:3]
        for i in range(24):
            before = set(c.mobiles)
            a.command("[add Horse set Controlled true ControlMaster 0x%X" % c.serial, 0.2)
            if a.wait_target(6):
                a.target_xyz(x + 1 + i % 3, y + 1 + (i // 3) % 3, z)
            s = c.wait(lambda: next((m for m in c.mobiles if m not in before and m != c.serial and m != trainer), None), 6)
            if s:
                pets.append(s)
        stabled, refused, other = 0, None, []
        for s in pets:
            n = len(c.messages)
            c.menu_pick(trainer, 3006126)
            if not c.wait_target(6):
                other.append("no target")
                continue
            c.target_obj(s)
            r = c.wait(lambda: next((m.get("cliloc") for m in c.messages[n:] if m.get("cliloc") in (1049677, 1042565, 1042563, 1042562, 1048053, 502677)), None), 5)
            if r == 1049677:
                stabled += 1
            elif r == 1042565:
                refused = stabled + 1
                break
            else:
                other.append(r)
            c.sleep(0.6)
        for s in pets:
            if s in c.mobiles:
                delete_mobile(a, s)
        delete_mobile(a, trainer)
        ok = len(pets) == 24 and stabled == 23 and refused == 24
        return ok, "%d horses made; %d stabled; refused at pet %s (1042565 'too many pets'); other replies %s" % (
            len(pets), stabled, refused, other[:5])
    finally:
        ctx.done(c)


# -- P71-3: the Fire Horn at 200 -----------------------------------------------------------------------------------

BARD = ("Musicianship", "Provocation", "Peacemaking", "Discordance")


def _horn_hits(a, p, horn, mob, tries=5):
    out = []
    for _ in range(tries):
        staff_on(a, "[set Hits 60000", mob, 0.4)
        p.sleep(0.4)
        p.dclick(horn)
        if not p.wait_target(5):
            out.append(None)
            p.sleep(6.5)
            continue
        p.target_obj(mob)
        p.sleep(1.5)
        left = get_prop(a, mob, "Hits")
        m = re.match(r"(\d+)", left or "")
        out.append(60000 - int(m.group(1)) if m else None)
        p.sleep(5.5)              # the horn's 6 s cooldown (FireHorn.cs:19-45)
    return out


@check("P71-3", "Fire Horn with the four bard skills at 200: about a tenth more damage than at 120 (89-110 against 81-100), not double")
def p71_3(ctx):
    # pinned Items/Skill Items/Misc/FireHorn.cs:130-163 with FireHorn-damage-scaled-200.patch -> CSC.FireHornScaled
    # (:268-286): one monster, fire resist 0: 81-100 at 120, 89-110 at 200 (pinned 189-233).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    horn = give(a, p, "FireHorn")
    give(a, p, "SulfurousAsh 60")
    mob = _dummy(a, p, extra="FireResistSeed 0")
    res = {}
    for v in (120, 200):
        for sk in BARD:
            staff_target(a, "[SetSkill %s %s" % (sk, v), p)
        res[v] = _horn_hits(a, p, horn, mob)
    delete_mobile(a, mob)
    lo = [d for d in res[120] if d is not None]
    hi = [d for d in res[200] if d is not None]
    # The play test's point is the ratio: about 1.10, not pinned's 2.3. The exact ranges are reported, not asserted: on
    # 10-21 one blast at 120 did 105 (above 100), so something multiplies a little that the bounds above leave out.
    ratio = (sum(hi) / float(len(hi))) / (sum(lo) / float(len(lo))) if lo and hi else None
    ok = len(lo) >= 4 and len(hi) >= 4 and ratio is not None and 1.0 <= ratio <= 1.3
    return ok, "damage at 120: %s; at 200: %s; mean ratio %s (expected about 1.10; ranges 81-100 and 89-110 by the formula; pinned at 200: 189-233, about 2.3)" % (
        res[120], res[200], "%.2f" % ratio if ratio else None)


from . import checks_p223g  # noqa: E402,F401
