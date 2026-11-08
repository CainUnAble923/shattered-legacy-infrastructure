"""cc-P223, seventh batch: an explosion trap made at Tinkering 200 and 120 (cc-P67) and a Platinum vein (cc-P57).
Imported by checks_p223f.py."""
import re

from .checks_p223b import roomy
from .checks import check, give, staff_target
from .checks_p223 import SK, add_beside, delete_mobile, open_craft, craft_find, craft_make, learn_all
from .checks_p223e import get_prop, staff_on


# -- P67-6: the explosion trap -------------------------------------------------------------------------------------

def _int(v):
    m = re.match(r"(-?\d+)", v or "")
    return int(m.group(1)) if m else None


def _craft_trap(a, p, box, tinkering):
    """Tinkering's 'explosion trap' (pinned Engines/Craft/DefTinkering.cs:438-449, 614-689; 1 iron ingot and an
    explosion potion), then the box at the 502921 cursor. Returns (TrapPower, TrapLevel, messages)."""
    staff_target(a, "[SetSkill Tinkering %s" % tinkering, p)
    tools = give(a, p, "TinkerTools")
    give(a, p, "IronIngot 5")
    give(a, p, "ExplosionPotion")
    g = open_craft(p, tools)
    g2, idx = craft_find(p, g, "explosion trap", exact=True)
    if g2 is None:
        return None, None, ["no 'explosion trap' in the Tinkering menu"]
    n = len(p.messages)
    texts, g3 = craft_make(p, g2, idx)
    if p.wait_target(6):
        p.target_obj(box)
        p.sleep(1.5)
    if g3:
        p.reply_gump(g3, 0)
    said = p.texts_since(n)
    return _int(get_prop(a, box, "TrapPower")), _int(get_prop(a, box, "TrapLevel")), said


def _set_off(a, p, box, power, level):
    """Arm the box as the craft did, but to go off on opening (TrapOnOpen is !TrapOnLockpick, LockableContainer.cs:13),
    then the player (not staff: GMs open traps 'with godly powers') opens it. Returns the hits lost."""
    staff_on(a, "[set TrapType ExplosionTrap TrapPower %d TrapLevel %d TrapOnLockpick false Locked false" % (power, level), box, 0.5)
    staff_target(a, "[set Hits 600", p)
    p.sleep(1.2)
    h0 = p.status.get("hits")
    n = len(p.messages)
    p.dclick(box)
    p.sleep(2.0)
    h1 = p.status.get("hits")
    fired = any("set off a trap" in t for t in p.texts_since(n))
    return (h0 - h1) if (fired and h0 is not None and h1 is not None) else None


@check("P67-6", "An explosion trap made at Tinkering 200 does no more harm than one made at 120; Remove Trap 200 still fails some of the time on it")
def p67_6(ctx):
    # DefTinkering.cs:676-689 with Tinkering-trap-damage-osi.patch: TrapPower (Tinkering/10) x 9 (180 at 200, 108 at
    # 120), TrapLevel min(Tinkering, 120)/10 = 12 at both. Damage RandomMinMax(10, 30) x TrapLevel = 120 to 360
    # (TrappableContainer.ExecuteTrap.cs:56-80), the same at both. Remove Trap (RemoveTrap.cs:61-71): the chance is
    # (skill - TrapPower)/30, so 200 against 180 succeeds two times in three (502377), fails without setting it off
    # (502372).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    staff_target(a, "[set RawStr 600", p)
    learn_all(a, p)        # the pattern gate (cc-P78) hides the trap entries too (10-21)
    box = add_beside(a, p, "WoodenChest")
    if not box:
        raise AssertionError("no chest appeared")
    staff_on(a, "[set KeyValue 1234 Locked false Movable true", box, 0.5)
    made = {}
    for t in (200, 120):
        power, level, said = _craft_trap(a, p, box, t)
        made[t] = (power, level, [x for x in said if "trap" in x.lower()][-2:])
        if power is not None and level is not None:
            made[t] = made[t] + ([_set_off(a, p, box, power, level) for _ in range(3)],)
    staff_target(a, "[SetSkill RemoveTrap 200", p)
    ok_n = fail_n = other = 0
    p200 = made[200][0] or 180
    for _ in range(6):
        staff_on(a, "[set TrapType ExplosionTrap TrapPower %d TrapLevel 12 TrapOnLockpick true" % p200, box, 0.4)
        n = len(p.messages)
        p.use_skill(SK["RemoveTrap"])
        if p.wait_target(5):
            p.target_obj(box)
        p.sleep(2.0)
        cl = [m.get("cliloc") for m in p.messages[n:]]
        tx = p.texts_since(n)
        if 502377 in cl or any("successfully render the trap harmless" in x for x in tx):
            ok_n += 1
        elif 502372 in cl or any("fail to disarm" in x for x in tx):
            fail_n += 1
        else:
            other += 1
        p.sleep(8.5)
    delete_mobile(a, box)
    d200 = [d for d in (made[200][3] if len(made[200]) > 3 else []) if d is not None]
    d120 = [d for d in (made[120][3] if len(made[120]) > 3 else []) if d is not None]
    ok = (made[200][0] == 180 and made[120][0] == 108 and made[200][1] == made[120][1] == 12
          and len(d200) == 3 and len(d120) == 3 and all(d <= 360 for d in d200 + d120) and fail_n >= 1)
    return ok, "made at 200: power %s level %s %s, damage %s; at 120: power %s level %s %s, damage %s (120-360 at both); Remove Trap 200 on the 200 trap, 6 tries: %d disarmed, %d failed, %d other" % (
        made[200][0], made[200][1], made[200][2], d200, made[120][0], made[120][1], made[120][2], d120, ok_n, fail_n, other)


# -- P57-3: a Platinum vein ---------------------------------------------------------------------------------------

# A Trammel bank whose vein rolls Platinum: ClusterFMiningExtension.cs:98-150 (one vein per 8x8 bank, seed
# bankX*17 + bankY*11 + mapID*3, Platinum 8 of 1036). Computed from that roll and checked against the map files:
# the bank 2584-2591 x 416-423 near Minoc is mountain (land 0x022D/0x022E, mineable). The bank holds 10 to 34 ore and
# refills in 10 to 20 minutes (Mining.cs:84-110), so three runs in a row can empty it: that is reported, not failed.
VEIN_STAND = (2588, 421, 63)
VEIN_TILE = (2589, 421, 61)
ORE = {0x19B7, 0x19B8, 0x19B9, 0x19BA}


def _mine(a, c, shovel, swings):
    """Dig `swings` times (Shovel: 503033 'Where do you wish to dig?', the land tile). Returns ({hue: amount} of new
    ore in the pack, empty-bank flag)."""
    pack = c.backpack()
    before = {s: (it.get("amount") or 0) for s, it in c.contents(pack).items()}
    empty = False
    for _ in range(swings):
        c.sleep(0.3)
        n = len(c.messages)
        c.dclick(shovel)
        if not c.wait_target(5):
            continue
        c.target_xyz(*VEIN_TILE)
        c.sleep(1.6)
        if any(m.get("cliloc") == 503040 for m in c.messages[n:]) or any("no metal here to mine" in t for t in c.texts_since(n)):
            empty = True
            break
    got = {}
    for s, it in c.contents(pack).items():
        if it.get("graphic") in ORE:
            d = (it.get("amount") or 0) - before.get(s, 0)
            if d > 0:
                got[it.get("hue", 0)] = got.get(it.get("hue", 0), 0) + d
    return got, empty


@check("P57-3", "A Platinum vein: at Mining 112.4 only iron ore comes up; at 112.5 Platinum ore does too (about a third)")
def p57_3(ctx):
    # pinned HarvestSystem.cs:139-147, 332-352: the vein's ore needs Base 112.5, else the fallback (iron); at 112.5 a
    # swing gives the fallback half the time and Platinum a quarter. Until Platinum is reported to the Survey Archivist
    # it crumbles into iron (CompactOreSatchelRoutingHook.cs:69-115), so the admin seeds the report first
    # ([CompactSeedDiscoveries Reported, ClusterFCompactAdminTools.cs:374-425). Iron ore has hue 0.
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=False)
    try:
        staff_target(a, "[CompactSeedDiscoveries Reported", c)
        # Str 400: a fresh character (Str 60) was overloaded after a few piles, and the Platinum it dug was lost
        # ("Your backpack is full, so the ore you mined is lost.", 1010481) while the shard logged the vein (10-21).
        staff_target(a, "[set RawStr 400", c)
        shovel = give(a, c, "Shovel")
        staff_target(a, "[set Map Trammel X %d Y %d Z %d" % VEIN_STAND, c)
        c.sleep(3.0)
        staff_target(a, "[SetSkill Mining 112.4", c)
        low, e1 = _mine(a, c, shovel, 5)
        staff_target(a, "[SetSkill Mining 112.5", c)
        high, e2 = _mine(a, c, shovel, 14)
        low_other = sum(v for h, v in low.items() if h != 0)
        plat = sum(v for h, v in high.items() if h != 0)
        total_high = sum(high.values())
        lost = sum(1 for m in c.messages if m.get("cliloc") == 1010481)
        if (e1 or e2) and not plat:
            raise RuntimeError("the vein's bank ran out of ore (it refills in 10 to 20 minutes): at 112.4 %s, at 112.5 %s" % (low, high))
        ok = sum(low.values()) > 0 and low_other == 0 and plat > 0
        logged = next((t for t in c.texts_since(0) if t.startswith("New Platinum vein logged")), None)
        return ok, "at 112.4 (5 swings): ore by hue %s; at 112.5 (14 swings): %s, Platinum-hued %d of %d%s%s; %r" % (
            low, high, plat, total_high, " (the bank ran out)" if e2 else "", (" (%d lost to a full pack)" % lost) if lost else "", logged)
    finally:
        ctx.done(c)


from . import checks_p223h  # noqa: E402,F401
