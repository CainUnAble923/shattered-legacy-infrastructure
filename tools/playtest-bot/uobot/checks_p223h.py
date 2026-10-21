"""cc-P223, eighth batch: Nerve Strike past 120 (cc-P71) and Parry with Evasion past 120 (cc-P66), measured in a fight.
Imported by checks_p223g.py. The client keeps each mobile's status flags and the graphical effects for these
(client.py, _p223_flags_effects); the weapon ability and the spell go out as packets.weapon_ability and cast_spell."""
import time

from . import packets as P
from .checks_p223b import roomy
from .checks import check, give, staff_target, try_equip
from .checks_p223 import delete_mobile
from .checks_p223e import staff_on
from .checks_p223f import _dummy

NERVE_STRIKE = 18          # pinned Items/Weapons/Abilities/WeaponAbility.cs:36, :70
EVASION = 403              # registered 402 (Spells/Initializer.cs:145), sent 1-based
BLOCK_EFFECT = 0x37B9      # pinned BaseWeapon.cs:1586, defender.FixedEffect(0x37B9, 10, 16) on a parry


# -- P71-1: Nerve Strike at Bushido 200 and 120 --------------------------------------------------------------------

def _strikes(a, p, mob, count):
    """Arm Nerve Strike and swing until it lands `count` times (1063356 'You cripple your target with a nerve
    strike!' on every landed one, paralysed or not, NerveStrike.cs). Paralysed = the target's flags bit 0x01 within
    1.2 s of the strike. After a paralyse the target is immune 8 s (ParalyzingBlow.cs:14, :94-99), so the fight stops
    for 9 s. Returns (landed, paralysed, other messages)."""
    landed = para = 0
    other = []
    tries = 0
    while landed < count and tries < count * 6:
        tries += 1
        staff_target(a, "[set Mana 200 Stam 200", p)
        p.send(P.weapon_ability(p.serial, NERVE_STRIKE))
        n = len(p.messages)
        f0 = len(p.flag_log)
        p.send(P.war_mode(True))
        p.sleep(0.2)
        p.send(P.attack(mob))
        m = p.wait(lambda: next((x for x in p.messages[n:] if x.get("cliloc") in (1063356, 1070804, 1061923, 1063347)), None), 8)
        if not m:
            continue
        if m.get("cliloc") != 1063356:
            other.append(m.get("cliloc"))
            if m.get("cliloc") == 1063347:      # not enough Bushido: no point going on
                break
            p.send(P.war_mode(False))
            p.sleep(9.0)
            continue
        landed += 1
        hit = p.wait(lambda: any(s == mob and f & 0x01 for _, s, f in p.flag_log[f0:]), 1.2)
        p.send(P.war_mode(False))
        if hit:
            para += 1
            p.sleep(9.0)
        else:
            p.sleep(1.0)
    p.send(P.war_mode(False))
    return landed, para, other


@check("P71-1", "Nerve Strike: at Bushido 200 about 1 in 20 does not paralyse, at 120 about 2 in 20")
def p71_1(ctx):
    # NerveStrike-paralyze-scaled-200.patch -> ClusterFSkillClamps.NerveStrikeChance (:207-228): 0.90 at 120, 0.95 at
    # 200 (pinned: certain from 137.5). The Bokuto carries it (SE Weapons/Bokuto.cs:17); Swords and Tactics 90 and
    # Bushido 50 are needed (WeaponAbility.cs:122-268).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    for sk, v in (("Swords", 120), ("Tactics", 120), ("Anatomy", 100)):
        staff_target(a, "[SetSkill %s %s" % (sk, v), p)
    staff_target(a, "[set RawStr 150 RawDex 150 RawInt 200 Hits 150", p)
    bokuto = give(a, p, "Bokuto")
    worn, _, _ = try_equip(p, bokuto)
    mob = _dummy(a, p)
    out = {}
    for b in (200, 120):
        staff_target(a, "[SetSkill Bushido %s" % b, p)
        out[b] = _strikes(a, p, mob, 20)
    delete_mobile(a, mob)
    if worn:
        p.lift_drop(bokuto, 1, p.backpack())
    l200, p200, o200 = out[200]
    l120, p120, o120 = out[120]
    # Binomial bounds: at 0.95 of 20, more than 4 misses is under 0.3%; at 0.90, more than 6 under 0.3%.
    ok = worn and l200 >= 18 and l120 >= 18 and (l200 - p200) <= 4 and (l120 - p120) <= 6 and p200 > 0 and p120 > 0
    return ok, "Bokuto worn %s; Bushido 200: %d of %d landed strikes paralysed (%d did not; about 1 expected)%s; Bushido 120: %d of %d (%d did not; about 2 expected)%s" % (
        worn, p200, l200, l200 - p200, (" other %s" % o200) if o200 else "", p120, l120, l120 - p120, (" other %s" % o120) if o120 else "")


# -- P66-5: Parry 200 with Evasion -----------------------------------------------------------------------------------

def _take_hits(a, p, seconds, evasion=False):
    """Stand in war mode against the attacker for `seconds`, topping up hits. A parry shows as the 0x37B9 effect on
    the player (no message); a hit that lands drops the hits. With evasion, Evasion is cast whenever its 20 s
    cooldown allows and only the 7 s windows after 1063120 are counted (Evasion.cs:123-173). Returns (blocks, hits)."""
    blocks = hits = 0
    end = time.monotonic() + seconds
    window_end = 0.0
    next_cast = 0.0
    last = p.status.get("hits")
    e0 = len(p.effects)
    while time.monotonic() < end:
        now = time.monotonic()
        if evasion and now >= next_cast:
            staff_target(a, "[set Mana 200", p)
            n = len(p.messages)
            p.send(P.cast_spell(EVASION))
            if p.wait(lambda: any(m.get("cliloc") == 1063120 for m in p.messages[n:]), 3):
                window_end = time.monotonic() + 6.5
                e0 = len(p.effects)
                last = p.status.get("hits")
            next_cast = time.monotonic() + 20.5
        counting = (not evasion) or time.monotonic() < window_end
        p.sleep(0.25)
        h = p.status.get("hits")
        if counting:
            if h is not None and last is not None and h < last:
                hits += 1
            blocks += sum(1 for e in p.effects[e0:] if e["item"] == BLOCK_EFFECT and p.serial in (e["source"], e["target"]))
        e0 = len(p.effects)
        last = h
        if h is not None and h < 120:
            staff_target(a, "[set Hits 400", p)
            last = p.status.get("hits")
    return blocks, hits


@check("P66-5", "Parry 200 and Bushido 120 with Evasion and a two-hander: some hits still get through (about 60% blocked, not 95%)",
       kind="BOT-PARTIAL", left="P66-5: how the fight feels")
def p66_5(ctx):
    # pinned BaseWeapon.cs:1491-1574 (no shield: parry x bushido / 41140 for a two-hander, +0.05 at 100, x Evasion's
    # scalar 1.5 at Bushido 120 with GM Anatomy and Tactics) with BaseWeapon-block-chance-osi.patch: the chance is
    # capped at OSI's top, 0.600 (ClusterFSkillClamps.cs:57-58, :113-114). So 0.60 at Parry 200 with or without Evasion
    # and 0.40 at 120 without it; pinned would block 95% with Evasion at 200.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    for sk, v in (("Parry", 200), ("Bushido", 120), ("Anatomy", 100), ("Tactics", 100), ("Swords", 100)):
        staff_target(a, "[SetSkill %s %s" % (sk, v), p)
    staff_target(a, "[set RawStr 400 RawDex 120 RawInt 100 Hits 400", p)
    give(a, p, "BookOfBushido")
    sword = give(a, p, "Nodachi")
    worn, _, _ = try_equip(p, sword)
    ettin = _dummy(a, p)
    staff_on(a, "[set FightMode Closest HitsMaxSeed 60000 Hits 60000 DamageMin 8 DamageMax 14 RawDex 200", ettin, 0.5)
    # The player is Young (monsters leave a young player alone) and Combatant is no [set property: on 10-21 the ettin
    # never swung. So she is made not Young and strikes it first; it answers, and keeps swinging for the whole test.
    staff_target(a, "[set Young false", p)
    p.send(P.war_mode(True))
    p.sleep(0.3)
    p.send(P.attack(ettin))
    p.sleep(3.0)
    eb, eh = _take_hits(a, p, 240, evasion=True)
    nb, nh = _take_hits(a, p, 60)
    staff_target(a, "[SetSkill Parry 120", p)
    lb, lh = _take_hits(a, p, 60)
    delete_mobile(a, ettin)
    if worn:
        p.lift_drop(sword, 1, p.backpack())

    def rate(b, h):
        return (b / float(b + h)) if (b + h) else None
    re_, rn, rl = rate(eb, eh), rate(nb, nh), rate(lb, lh)
    ok = worn and eh > 0 and re_ is not None and re_ < 0.85 and (eb + eh) >= 8
    return ok, "two-hander worn %s; Parry 200 with Evasion: %d blocked, %d hit (%.0f%%); Parry 200 without: %d / %d (%s); Parry 120 without: %d / %d (%s) (expected 60%%, 60%%, 40%%)" % (
        worn, eb, eh, 100 * (re_ or 0), nb, nh, "%.0f%%" % (100 * rn) if rn is not None else "n/a", lb, lh, "%.0f%%" % (100 * rl) if rl is not None else "n/a")
