"""cc-P223, third batch (written on the second day, against main 62c675d): the satchel's Smelt Ore, stealing (a stack
cap, the stealable artifacts, the caught rate past 120) and a Platinum vein. Imported by checks_p223b.py; shares the
helpers of checks.py and checks_p223.py.

What each check expects is read from the code under test, cited per check (server/customizations, server/patches,
pinned D:\\UO\\ModernUO-pinned\\Projects at d4531cd94).
"""
import re

from . import packets as P
from .checks_p223b import roomy
from .checks import check, SHARED, give, staff_target, near, place_forge, try_equip
from .checks_p223 import SK, base, add_beside, add_mobile_beside, delete_mobile, name_of
from .checks_p223b import move_to

INGOT_GRAPHICS = (0x1BEF, 0x1BF0, 0x1BF1, 0x1BF2, 0x1BF3, 0x1BF4, 0x1BF5, 0x1BF6, 0x1BF7, 0x1BF8, 0x1BF9, 0x1BFA, 0x1BFB)
ORE_GRAPHICS = (0x19B7, 0x19B8, 0x19B9, 0x19BA)

# pinned UOContent/Skills/Stealing.cs (cliloc numbers as the server sends them).
STEAL_OK, STEAL_FAIL, STEAL_NOT_SKILLED, STEAL_CANT = 502724, 502723, 1060025, 502710


def open_container(c, serial, timeout=6):
    """Double-click a container and wait for its 0x24 (opened) and contents (0x3C), so its items are known."""
    c.sleep(1.1)                 # the action delay after the last double-click
    n = len(c.containers)
    c.dclick(serial)
    c.wait(lambda: serial in c.containers[n:], timeout)
    c.sleep(0.8)
    return c.contents(serial)


def clilocs_since(c, n):
    return [m.get("cliloc") for m in c.messages[n:] if m.get("cliloc")]


def steal(c, target, wait=2.5):
    """Use Stealing (skill 33) and target the item or creature. Returns (clilocs, texts) the thief received."""
    c.sleep(0.3)
    n = len(c.messages)
    c.use_skill(SK["Stealing"])
    if not c.wait_target(6):
        return clilocs_since(c, n), c.texts_since(n) or ["(no target cursor)"]
    c.target_obj(target)
    c.sleep(wait)
    return clilocs_since(c, n), c.texts_since(n)


def home(a, p):
    """Back to New Haven's square on Trammel (the long-lived player's home between checks)."""
    staff_target(a, "[set Map Trammel X 3505 Y 2580 Z 14", p)
    p.sleep(2.0)


def thief(a, p, skill):
    """The player as a thief: Stealing at `skill`, not Young (Stealing.cs:51-70 refuses a Young thief, 502700), hands
    free (1005584 otherwise), and visible."""
    staff_target(a, "[SetSkill Stealing %s" % skill, p)
    staff_target(a, "[set Young false Hidden false", p)


# -- P56-5: Smelt Ore from the Reinforced Ore Satchel -------------------------------------------------------------

SMELT_LINE = re.compile(r"Smelt Ore: (\d+) piles?: (\d+) smelted into (\d+) ingots?, (\d+) failed, (\d+) left unsmelted\.")


def satchel_smelt(p, satchel, timeout=6):
    """The context entry 1900001 ("Smelt Ore", ShardClilocs.cs:28) on the satchel; CompactOreSatchel.cs:215-276 runs
    SmeltAllOre and ends with the summary line (:273-275). Returns (match or None, texts)."""
    n = len(p.messages)
    if not p.menu_pick(satchel, 1900001):
        return None, ["(no Smelt Ore entry in the satchel's menu)"]
    m = p.wait(lambda: next((SMELT_LINE.search(t) for t in p.texts_since(n) if SMELT_LINE.search(t)), None), timeout)
    return m, p.texts_since(n)


def ore_into(a, p, satchel, ore_type, amount):
    """Platinum ore into the satchel: [AddToPack to the player, then the player drags it into the satchel (the
    satchel takes ore, CompactOreSatchel.cs)."""
    s = give(a, p, "%s %d" % (ore_type, amount))
    p.lift_drop(s, amount, satchel)
    p.sleep(1.0)
    return s


@check("P56-5", "Smelt Ore on a Reinforced Ore Satchel: Platinum is left unsmelted at Mining 112.4, smelts into the satchel at 112.5")
def p56_5(ctx):
    # Platinum's smelt difficulty is ClusterFMetalTiers.RequiredSkill 112.5 (ClusterFMetalTiers.cs:20), compared with
    # Mining's Value (Ore.cs, server/patches/Ore-smelt-at-forge.patch): below it 501986 and the pile is "left
    # unsmelted"; at it a roll on 87.5-137.5, so about half the piles smelt and the rest burn (501990, "failed").
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    forge = place_forge(a, p)
    satchel = give(a, p, "ReinforcedOreSatchel")
    open_container(p, satchel)
    staff_target(a, "[SetSkill Mining 112.4", p)
    ore_into(a, p, satchel, "PlatinumOre", 10)
    m1, t1 = satchel_smelt(p, satchel)
    below = "no summary line %s" % t1[-3:] if not m1 else m1.group(0)
    refused_ok = bool(m1) and int(m1.group(2)) == 0 and int(m1.group(5)) >= 1
    staff_target(a, "[SetSkill Mining 112.5", p)
    tries, smelted, at = 0, 0, []
    while tries < 6 and not smelted:
        tries += 1
        if not any(it.get("graphic") in ORE_GRAPHICS for it in p.contents(satchel).values()):
            ore_into(a, p, satchel, "PlatinumOre", 10)
        m2, t2 = satchel_smelt(p, satchel)
        at.append(m2.group(0) if m2 else "no summary line %s" % t2[-2:])
        if m2 and int(m2.group(5)) == 0 and int(m2.group(2)) > 0:
            smelted = int(m2.group(3))
        p.sleep(1.0)
    ingots = sum((it.get("amount") or 0) for it in p.contents(satchel).values() if it.get("graphic") in INGOT_GRAPHICS)
    ok = refused_ok and smelted > 0 and ingots > 0
    return ok, "at 112.4: %s; at 112.5 over %d smelt(s): %s; ingots in the satchel %d" % (below, tries, at, ingots)


# -- P57-7: a stolen stack is capped -------------------------------------------------------------------------------

@check("P57-7", "Stealing 150 from 150 iron ingots in a pack on the ground takes at most 100")
def p57_7(ctx):
    # server/patches/Stealing-stack-weight.patch (pinned Stealing.cs:270-276): maxAmount = min(Stealing / 10,
    # MaxWeightToSteal 10) / weight 0.1, so 100 at 150, and the amount taken is random from 1 to it.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    thief(a, p, 150)
    bag = add_beside(a, p, "Backpack")
    if not bag:
        raise AssertionError("no backpack appeared beside the player")
    near(a, p)
    a.command("[AddToPack IronIngot 150", 0.2)
    if not a.wait_target(8):
        raise AssertionError("[AddToPack gave no target cursor")
    a.target_obj(bag)
    a.sleep(1.0)
    inside = open_container(p, bag)
    pile = next((s for s, it in inside.items() if it.get("graphic") in INGOT_GRAPHICS), None)
    if not pile:
        return False, "no ingot pile seen in the bag (%d items)" % len(inside)
    start = inside[pile].get("amount")
    pack = p.backpack()
    before = sum((it.get("amount") or 0) for it in p.contents(pack).values() if it.get("graphic") in INGOT_GRAPHICS)
    cl, said = steal(p, pile)
    after = sum((it.get("amount") or 0) for it in p.contents(pack).values() if it.get("graphic") in INGOT_GRAPHICS)
    left = (p.items.get(pile) or {}).get("amount")
    got = after - before
    delete_mobile(a, bag)        # [Delete on the bag, so the next run starts clean
    ok = STEAL_OK in cl and 1 <= got <= 100 and start == 150
    return ok, "pile of %s; the thief got %d (%s left in the bag); messages %s %s" % (start, got, left, cl, said[-2:])


# -- T-026 and T-023: the stealable artifacts ----------------------------------------------------------------------

def _steal_artifact(ctx, type_hint, x, y, z, skill):
    """[RemoveStealArties then [GenStealArties (pinned Items/Special/StealableArtifacts.cs:20-63, AccessLevel
    Developer; the bot's admin is Owner): every entry is placed at once, the 15-minute timer is only the respawn
    check, so no wait. The thief is moved beside the spot on Ter Mur and steals what lies there."""
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    rm = a.command("[RemoveStealArties", 2.0)
    crashed = a.closed is not None
    if crashed:
        # Pinned StealableArtifacts.RemoveStealableArtifacts reads si.Item.Deleted with no null check, and an entry
        # whose artifact was stolen has no item: the command throws and the server drops the admin (bug list, found
        # by T-026 on 10-21). The respawn timer is already stopped by then, so [GenStealArties still places them.
        SHARED.setdefault("p223_remove_steal_arties_crash", 0)
        SHARED["p223_remove_steal_arties_crash"] += 1
        a = ctx.ensure_admin()
    gen = a.command("[GenStealArties", 3.0)
    gen_said = [m["text"] for m in gen if "artifact" in (m["text"] or "").lower()]
    thief(a, p, skill)
    staff_target(a, "[set Map TerMur X %d Y %d Z %d" % (x - 1, y, z), p)
    p.sleep(3.0)
    def find():
        # By name: another item can share the tile (a forge was taken for the artifact on 10-21).
        for s, it in list(p.items.items()):
            if it.get("world") and it.get("x") == x and it.get("y") == y and type_hint in name_of(p, s).lower():
                return s
        return None
    item = p.wait(find, 10)
    if not item:
        here = [name_of(p, s) for s, it in list(p.items.items()) if it.get("world") and it.get("x") == x and it.get("y") == y]
        return False, "[GenStealArties said %s; no %r at %d,%d on Ter Mur (there: %s)" % (gen_said, type_hint, x, y, here)
    nm = name_of(p, item)
    pack = p.backpack()
    tries, results = 0, []
    while tries < 4:
        tries += 1
        cl, said = steal(p, item)
        results.append(cl[-1] if cl else (said[-1] if said else None))
        if p.wait(lambda: (p.items.get(item) or {}).get("container") == pack, 3):
            break
        if STEAL_NOT_SKILLED in cl or STEAL_CANT in cl:
            break
        p.sleep(10.5)            # Stealing's own delay (Stealing.cs:424-430)
    in_pack = (p.items.get(item) or {}).get("container") == pack
    if in_pack:
        delete_mobile(a, item)   # [Delete it: a stolen one respawns only after 1152-1728 minutes
    home(a, p)
    return in_pack, \
        "%s[GenStealArties: %s; %r at %d,%d; %d tries at Stealing %s: %s; in the pack %s" % (
            "[RemoveStealArties disconnected the admin (NullReferenceException, see the bug list); " if crashed else "",
            gen_said, nm, x, y, tries, skill, results, in_pack)


@check("T-026", "[GenStealArties places the Half Eaten Supper at Ter Mur 1134 1204 7, and a thief at 120 steals it into the pack")
def t_026(ctx):
    # The entry: server/patches/StealableArtifacts-servuo-entries.patch, HalfEatenSupperArtifact (cliloc 1113658,
    # weight 10, SADecorationArtifacts.cs:456-465): at 120 about 85% a try, so up to four tries.
    return _steal_artifact(ctx, "half eaten supper", 1134, 1204, 7, 120)


@check("T-023", "Light in the Void (Ter Mur 345 621 26): a thief at Stealing 120 steals it into the pack")
def t_023(ctx):
    # LightInTheVoid : GargishTalwar (server/customizations/Items/Artifacts/Equipment/Weapons/LightInTheVoid.cs),
    # placed by the same patch's entry; Stealing needs 100 (1060025 below).
    return _steal_artifact(ctx, "light in the void", 345, 621, 26, 120)


# -- P66-3: the caught rate past 120 ------------------------------------------------------------------------------

P66_3_TRIES = 50


@check("P66-3", "A thief at 127.5 in Shadow Dancer Leggings, stealing 50 times from a monster, is caught about 1 in 5")
def p66_3(ctx):
    # server/patches/Stealing-caught-osi-skill.patch (Stealing.cs:337) with ClusterFSkillClamps.cs:67: caught =
    # min(Stealing, 120) < Random(150), so 29/150 (19%) at any skill from 120, the leggings' +20 included. The thief is
    # told nothing when caught; everyone within 8 tiles sees "You notice <thief> trying to steal from <monster>."
    # (Stealing.cs:388-410), so the admin stands by and counts. Outside a dungeon, so the Thieves' Den layer
    # (StealingHandler.cs) stays out of it.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    thief(a, p, 127.5)
    legs = give(a, p, "ShadowDancerLeggings")
    # The starting pants fill the leggings' layer (Pants, 0x04): the server bounced the leggings back (10-21).
    for s, it in list(p.items.items()):
        if it.get("parent") == p.serial and it.get("layer") == 0x04:
            p.lift_drop(s, 1, p.backpack())
            p.sleep(0.8)
    worn, _, _ = try_equip(p, legs)
    home(a, p)
    # By body, not name: an orc is named at random ("Vetorkag", 10-21). Orc body 17 (0x11).
    near(a, p)
    before = set(p.mobiles)
    a.command("[add Orc", 0.2)
    if a.wait_target(8):
        x, y, z = p.pos[:3]
        a.target_xyz(x + 1, y, z)
    orc = p.wait(lambda: next((s for s, m in p.mobiles.items() if s not in before and m.get("body") == 17), None), 8)
    if not orc:
        raise AssertionError("no orc appeared beside the player")
    # FightMode None: it never fights back, so the count is only the steal.
    a.command("[set FightMode None CantWalk true Blessed false", 0.2)
    if a.wait_target(6):
        a.target_obj(orc)
    a.sleep(0.5)
    near(a, p)
    n_admin = len(a.messages)
    ok_n = fail_n = other = 0
    for _ in range(P66_3_TRIES):
        a.command("[AddToPack Gold 50", 0.2)     # something light to take every time
        if a.wait_target(6):
            a.target_obj(orc)
        a.sleep(0.4)
        cl, said = steal(p, orc, wait=1.5)
        if STEAL_OK in cl:
            ok_n += 1
        elif STEAL_FAIL in cl:
            fail_n += 1
        else:
            other += 1
        p.sleep(11.0)            # Stealing's delay; 9 s brought 'You must wait a few moments' (500118)
    seen = [m["text"] for m in a.messages[n_admin:] if re.search(r"You notice .* trying to steal", m["text"] or "")]
    delete_mobile(a, orc)
    if worn:
        p.lift_drop(legs, 1, p.backpack())
    caught = len(seen)
    # 29/150 of 50 is 9.7, sd 2.8: 3 to 17 is about +-2.6 sd.
    ok = worn and 3 <= caught <= 17
    return ok, "leggings worn %s; %d tries: %d stolen, %d failed, %d other; caught (seen by the admin) %d" % (
        worn, P66_3_TRIES, ok_n, fail_n, other, caught)


from . import checks_p223d  # noqa: E402,F401
