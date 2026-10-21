"""cc-P223, fifth batch: the evo pets (cc-P65) and the Compact's pack mules (cc-P69). Imported by checks_p223d.py.

Each check makes a fresh character, so follower slots and League or guild state start empty. Property values are read
with the admin's [get <property> (pinned Commands.cs:769-795, "<name> = <value>"). Our property-list lines are plain
strings on the rotating ~1_NOTHING~ clilocs, so they are matched on their text.
"""
import re

from .checks import check, all_text, buttons, give, staff_target, near
from .checks_p223 import add_beside, add_mobile_beside, delete_mobile, opl, name_of
from .checks_p223b import move_to


def get_prop(a, serial, prop, timeout=5):
    """[get <prop> on a serial: returns the value text, or None."""
    n = len(a.messages)
    a.command("[get " + prop, 0.2)
    if not a.wait_target(6):
        return None
    a.target_obj(serial)
    m = a.find_text(r"^%s\s*=" % re.escape(prop), since=n, timeout=timeout)
    return m["text"].split("=", 1)[1].strip() if m else None


def staff_on(a, command, serial, settle=0.8):
    """A targeted staff command on any serial; returns the admin's messages since."""
    n = len(a.messages)
    a.command(command, 0.2)
    if not a.wait_target(6):
        raise AssertionError("%s gave no target cursor" % command)
    a.target_obj(serial)
    a.sleep(settle)
    return [m["text"] for m in a.messages[n:]]


def new_mobile(c, before, pred=None, timeout=8):
    return c.wait(lambda: next((s for s, m in c.mobiles.items() if s not in before and s != c.serial and (pred is None or pred(m))), None), timeout)


def echo_lines(c, n):
    return [t for t in c.texts_since(n) if t.startswith("The Echo:")]


# -- P65-0: an evo dog through its five stages -----------------------------------------------------------------------

@check("P65-0", "[EvoPet: a dog goes 217 > 739 > the pick > 277 > 1410 with the Echo lines; a reset keeps it; stable and claim; death and res",
       kind="BOT-PARTIAL", left="P65-0: each body's swing, flinch, fall and idle; the door; the 10 decisions")
def p65_0(ctx):
    # server/customizations/Mobiles/Evo: EvoPetCommands.cs (the replies), EvoPet.cs:96-121, :319-371 (status, gates,
    # Echo lines), EvoPetData.cs:93-215 (bodies 217, 739, pick, 277, 1410; XP 500, 3,000, 13,000, 53,000; stage 4 needs
    # loop 1, stage 5 loop 5 and the Bond Trial), EvoPetGumps.cs (hue gump buttons 1-7, pick gump buttons 1-6:
    # 1 Alaskan malamute 1547).
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=True)
    steps, bad = [], []
    try:
        near(a, c)
        before = set(c.mobiles)
        ng = len(c.gumps)
        said = staff_on(a, "[EvoPet give dog", c.serial)
        pet = new_mobile(c, before, lambda m: m.get("body") == 217)
        hue_g = c.wait_gump(lambda g: any("Choose a pup's color" in t for t in all_text(c, g)), 6, since=ng)
        if hue_g:
            c.reply_gump(hue_g, 1)
        if not pet:
            return False, "no pup (body 217) after [EvoPet give dog: %s" % said[-2:]
        steps.append("give: %s, body 217, hue gump %s" % (said[-1:], bool(hue_g)))

        def evo(cmd, want_body=None, want_echo=None, want_status=None):
            n = len(c.messages)
            out = staff_on(a, "[EvoPet " + cmd, pet, settle=1.5)
            c.sleep(1.0)
            body = (c.mobiles.get(pet) or {}).get("body")
            echo = echo_lines(c, n)
            reply = next((t for t in out if "stage" in t), out[-1] if out else None)
            steps.append("%s: body %s; %r; echo %s" % (cmd, body, reply, echo))
            if want_body is not None and body != want_body:
                bad.append("%s: body %s, want %s" % (cmd, body, want_body))
            if want_echo and not any(want_echo in e for e in echo):
                bad.append("%s: no Echo line with %r" % (cmd, want_echo))
            if want_status and not (reply and want_status in reply):
                bad.append("%s: reply %r lacks %r" % (cmd, reply, want_status))
            return reply

        evo("xp 500", 739, "It grew.")
        ng = len(c.gumps)
        evo("xp 2500", None, "Choose what it becomes", "waiting: the owner's pick")
        pick = c.wait_gump(lambda g: any("What will a young hound become?" in t for t in all_text(c, g)), 6, since=ng)
        if pick:
            n = len(c.messages)
            c.reply_gump(pick, 1)
            c.sleep(1.5)
            body = (c.mobiles.get(pet) or {}).get("body")
            steps.append("pick 1 (Alaskan malamute): body %s; echo %s" % (body, echo_lines(c, n)))
            if body != 1547:
                bad.append("pick: body %s, want 1547" % body)
        else:
            bad.append("no pick gump at 3,000 XP")
        evo("xp 10000", None, "Loop 1 time first", "waiting: owner at loop 1")
        evo("loops 1", 277, "done this before")
        evo("xp 40000", None, "Loop 5 times first")
        evo("loops 5", None, None, "waiting: the Bond Trial")
        evo("bond on", 1410, "remembers every loop")
        info = evo("info", 1410, None, "stage 5, at its top for now")

        # The Reset Stone (ClusterFResetStone.cs, ClusterFDevTools.cs:288-536): Select All (1), Reset (10), Confirm (1).
        stone = add_beside(a, c, "ResetStone", dx=1, dy=1)
        if stone:
            c.sleep(1.1)
            ng = len(c.gumps)
            c.dclick(stone)
            g = c.wait_gump(lambda g: 10 in buttons(g), 6, since=ng)
            if g:
                ng = len(c.gumps)
                c.reply_gump(g, 1)
                g2 = c.wait_gump(lambda g: 10 in buttons(g), 6, since=ng)
                if g2:
                    ng = len(c.gumps)
                    c.reply_gump(g2, 10, switches=list(range(10)))
                    conf = c.wait_gump(None, 6, since=ng)
                    if conf:
                        c.reply_gump(conf, 1)
                        c.find_text(r"\[ClusterFReset\] Done", timeout=8)
            delete_mobile(a, stone)
        after = staff_on(a, "[EvoPet info", pet, settle=1.5)
        after_info = next((t for t in after if t.startswith("Dog line")), None)
        same = bool(info and after_info) and info == after_info
        steps.append("after the Reset Stone: %r (same %s)" % (after_info, same))
        if not same:
            bad.append("the reset changed the pet: %r -> %r" % (info, after_info))

        # Stable and claim (pinned AnimalTrainer.cs:186-341): 3006126, target the pet, NPC 1049677; 3006127, 1042559.
        give(a, c, "Gold 200")
        trainer = add_mobile_beside(a, c, "AnimalTrainer", dx=-1, name_part="animal trainer")
        if trainer:
            staff_on(a, "[set CantWalk true", trainer, 0.4)
            n = len(c.messages)
            c.menu_pick(trainer, 3006126)
            if c.wait_target(6):
                c.target_obj(pet)
            stabled = c.wait(lambda: any(m.get("cliloc") == 1049677 for m in c.messages[n:]), 6)
            c.sleep(1.0)
            n = len(c.messages)
            c.menu_pick(trainer, 3006127)
            claimed = c.wait(lambda: any(m.get("cliloc") == 1042559 for m in c.messages[n:]), 6)
            steps.append("stable: %s; claim: %s" % (bool(stabled), bool(claimed)))
            if not (stabled and claimed):
                bad.append("stable %s, claim %s" % (bool(stabled), bool(claimed)))
            delete_mobile(a, trainer)
        else:
            bad.append("no animal trainer")
        c.sleep(1.5)

        # Death and resurrection: [kill, then [res (pinned Commands.cs:988-1043); a bonded pet keeps its stage.
        killed = staff_on(a, "[kill", pet, 2.0)
        ressed = staff_on(a, "[res", pet, 2.0)
        final = staff_on(a, "[EvoPet info", pet, 1.5)
        final_info = next((t for t in final if t.startswith("Dog line")), None)
        steps.append("kill: %s; res: %s; then %r" % (killed[-1:], ressed[-1:], final_info))
        if not (final_info and "stage 5" in final_info and "body 1410" in final_info):
            bad.append("after death and res: %r" % final_info)
        delete_mobile(a, pet)
        return not bad, ("problems: %s || " % bad if bad else "") + " | ".join(steps)
    finally:
        ctx.done(c)


# -- P69: the League's errand and the mules --------------------------------------------------------------------------

REGISTRAR_AT = (3459, 2601, 18)      # ClusterFNewHavenSeeder.cs:71, "Elara Voss, the League Registrar"
LIAISON_AT = (3498, 2744, 4)         # ClusterFGuildStarterPath.cs:216, MinersCompactLiaison "Garrett Ashveil"


def find_npc(a, c, x, y, z, type_name, word):
    """The NPC near a spot by its OPL name; if a fresh scratch world has none, the admin adds the type there."""
    move_to(a, c, x + 1, y + 1, z)
    for attempt in range(2):
        c.sleep(1.5)
        for s, m in list(c.mobiles.items()):
            if s != c.serial and abs(m.get("x", 0) - x) <= 6 and abs(m.get("y", 0) - y) <= 6 and word.lower() in name_of(c, s).lower():
                return s
        if attempt == 0:
            a.command("[add " + type_name, 0.2)
            if a.wait_target(6):
                a.target_xyz(x, y, z)
            a.sleep(1.5)
    return None


def registrar(c, reg, page=None, timeout=8):
    """Double-click the Registrar (LeagueRegistrar.cs:71-103) for its gump; with page, press that menu button."""
    c.sleep(1.1)
    ng = len(c.gumps)
    c.dclick(reg)
    g = c.wait_gump(None, timeout, since=ng)
    if g and page is not None and page in buttons(g):
        ng = len(c.gumps)
        c.reply_gump(g, page)
        g = c.wait_gump(None, timeout, since=ng)
    return g


def page_press(c, g, button, timeout=8):
    n = len(c.messages)
    ng = len(c.gumps)
    c.reply_gump(g, button)
    c.sleep(2.0)
    return c.texts_since(n), c.wait_gump(None, 3, since=ng)


ERRAND = 21     # LeagueRegistrarGump.cs:29-44: 21 the Compact's Mule page; 22 take, 23 deliver, 24 collect, 25 replace


@check("P69-1", "The Compact's Mule: refused at Iron; at Dull Copper 49 ore is refused, 50 ore and 25 ingots give a bonded mule; release, then 'Replace my mule'")
def p69_1(ctx):
    # server/customizations/Mobiles/Mule/MuleErrand.cs:25-204, LeagueRegistrarGump.cs:142-403, GuildResources.cs:268-378.
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=False)
    steps, bad = [], []
    try:
        reg = find_npc(a, c, REGISTRAR_AT[0], REGISTRAR_AT[1], REGISTRAR_AT[2], "LeagueRegistrar", "registrar")
        if not reg:
            return False, "no League Registrar near %s" % (REGISTRAR_AT,)
        g = registrar(c, reg)
        if g and 10 in buttons(g):
            said, _ = page_press(c, g, 10)
            steps.append("join: %s" % [t for t in said if "League" in t][-1:])
        g = registrar(c, reg, ERRAND)
        iron = [t for t in all_text(c, g)] if g else []
        refused = any("Dull Copper Citizen rank or higher" in t for t in iron) and 22 not in buttons(g or {"layout": ""})
        steps.append("at Iron: refused %s" % refused)
        if not refused:
            bad.append("at Iron the page reads %s" % iron[:4])
        staff_target(a, "[set LeagueRank 2", c)
        g = registrar(c, reg, ERRAND)
        if not g or 22 not in buttons(g):
            return False, "at Dull Copper no Take the errand button: %s" % (all_text(c, g)[:4] if g else None)
        said, _ = page_press(c, g, 22)
        steps.append("take: %s" % [t for t in said if "Compact" in t][-1:])
        give(a, c, "IronOre 49")
        give(a, c, "IronIngot 25")
        pack = c.backpack()
        ore_before = sum((it.get("amount") or 0) for it in c.contents(pack).values() if it.get("graphic") in (0x19B7, 0x19B8, 0x19B9, 0x19BA))
        g = registrar(c, reg, ERRAND)
        said, _ = page_press(c, g, 23) if g and 23 in buttons(g) else (["(no deliver button)"], None)
        short = next((t for t in said if t.startswith("You need 50 iron ores")), None)
        ore_after = sum((it.get("amount") or 0) for it in c.contents(pack).values() if it.get("graphic") in (0x19B7, 0x19B8, 0x19B9, 0x19BA))
        steps.append("49 ore: %r; ore %d -> %d" % (short, ore_before, ore_after))
        if not short or ore_after != ore_before:
            bad.append("49 ore: %r, ore %d -> %d" % (short, ore_before, ore_after))
        give(a, c, "IronOre 1")
        g = registrar(c, reg, ERRAND)
        said, _ = page_press(c, g, 23) if g and 23 in buttons(g) else (["(no deliver button)"], None)
        stamped = any("stamps your requisition" in t for t in said)
        steps.append("50 ore: stamped %s" % stamped)
        before = set(c.mobiles)
        g = registrar(c, reg, ERRAND)
        said, _ = page_press(c, g, 24) if g and 24 in buttons(g) else (["(no collect button)"], None)
        mule = new_mobile(c, before, timeout=6)
        led = any("leads your new mule over" in t for t in said)
        bonded = get_prop(a, mule, "IsBonded") if mule else None
        slots = get_prop(a, mule, "ControlSlots") if mule else None
        mname = name_of(c, mule) if mule else None
        steps.append("collect: %s; mule %r bonded %s slots %s" % (led, mname, bonded, slots))
        if not (stamped and led and mule and bonded == "True" and (slots or "").split()[0:1] == ["1"]):
            bad.append("collect: stamped %s led %s mule %s bonded %s slots %s" % (stamped, led, bool(mule), bonded, slots))
        g = registrar(c, reg, ERRAND)
        have = any(t.startswith("You have your mule.") for t in (all_text(c, g) if g else []))
        if g:
            c.reply_gump(g, 0)
        steps.append("page: 'You have your mule.' %s" % have)
        if not have:
            bad.append("no 'You have your mule.' line")
        if mule:
            c.sleep(1.0)
            ng = len(c.gumps)
            c.menu_pick(mule, 3006118)
            conf = c.wait_gump(None, 6, since=ng)
            if conf:
                c.reply_gump(conf, 2)
            c.sleep(2.0)
        g = registrar(c, reg, ERRAND)
        lines = all_text(c, g) if g else []
        replace = any("find you another for 5,000 gold and 10 Mining Vouchers" in t for t in lines) and 25 in buttons(g or {"layout": ""})
        if g:
            c.reply_gump(g, 0)
        steps.append("after release: Replace my mule with the fee %s" % replace)
        if not replace:
            bad.append("after release the page reads %s" % lines[:4])
        if mule:
            delete_mobile(a, mule)
        return not bad, ("problems: %s || " % bad if bad else "") + " | ".join(steps)
    finally:
        ctx.done(c)


def deed_mule(a, c):
    """[AddToPack PackMuleDeed and use it: a bonded tier 1 mule (PackMuleDeed.cs:41-45). Returns its serial."""
    deed = give(a, c, "PackMuleDeed")
    before = set(c.mobiles)
    c.sleep(1.1)
    c.dclick(deed)
    return new_mobile(c, before, timeout=8)


@check("P69-2", "The Civic Mule Exchange: a tier 1 mule becomes a Bronzeback, with its hue, more hits and a bigger pack, keeping its name, contents and bond",
       kind="BOT-PARTIAL", left="P69-2: each tier's hue looks right on the mule")
def p69_2(ctx):
    # CrossGuildExchangeGump.cs:42-142 (button 100 + index), MuleUpgrades.cs:123-161, PackMule.cs:393-403 (TierUp keeps
    # name, genes, pack and bond), MuleData.cs:58-62 (tier 2: hue 0x972, hits 200-250, pack 2,160-2,400 stones). Three
    # of the four currencies have no staff command, so the Dev Testing Crystal waives the cost (DevTestingCrystal.cs).
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=True)
    try:
        mule = deed_mule(a, c)
        if not mule:
            return False, "no mule from the PackMuleDeed"
        staff_on(a, "[set Name Bot Mule", mule, 0.5)
        packs = [s for s, it in c.items.items() if it.get("parent") == mule and it.get("layer") == 0x15]
        mpack = packs[0] if packs else None
        if mpack:
            a.command("[AddToPack IronIngot 7", 0.2)
            if a.wait_target(6):
                a.target_obj(mule)
            a.sleep(0.8)
        b = {"hue": (c.mobiles.get(mule) or {}).get("hue"), "hits": get_prop(a, mule, "HitsMax"), "bonded": get_prop(a, mule, "IsBonded"),
             "items": get_prop(a, mpack, "TotalItems") if mpack else None, "weight": get_prop(a, mpack, "MaxWeight") if mpack else None}
        staff_target(a, "[GiveDevCrystal", c)
        give(a, c, "Gold 25000")
        liaison = find_npc(a, c, LIAISON_AT[0], LIAISON_AT[1], LIAISON_AT[2], "MinersCompactLiaison", "garrett")
        if not liaison:
            return False, "no Miners' Compact Liaison near %s" % (LIAISON_AT,)
        staff_on(a, "[set X %d Y %d Z %d" % (c.pos[0] + 1, c.pos[1], c.pos[2]), mule, 1.0)

        def main_gump():
            c.sleep(1.1)
            ng = len(c.gumps)
            c.dclick(liaison)
            return c.wait_gump(None, 8, since=ng)
        g = main_gump()
        if g and 30 not in buttons(g):
            # A non-member gets the join page straight away (Join = button 1, ClusterFGuildSystem.cs:1040, :1092);
            # the main menu's 20 leads to the same page.
            if 1 not in buttons(g) and 20 in buttons(g):
                ng = len(c.gumps)
                c.reply_gump(g, 20)
                g = c.wait_gump(lambda x: 1 in buttons(x), 6, since=ng)
            if g and 1 in buttons(g):
                c.reply_gump(g, 1)
                c.sleep(2.0)
            g = main_gump()
        if g and 30 not in buttons(g) and 5 in buttons(g):
            # A member's double-click opens the guild hall page; its "Guild services" (5) opens the Liaison's own
            # menu (ClusterFGuildSystem.cs:1177-1178, :1224 -> OpenMemberServices :548).
            ng = len(c.gumps)
            c.reply_gump(g, 5)
            g = c.wait_gump(lambda x: 30 in buttons(x), 6, since=ng) or g
        if not g or 30 not in buttons(g):
            return False, "no Member Dashboard button: %s" % (all_text(c, g)[:4] if g else None)
        ng = len(c.gumps)
        c.reply_gump(g, 30)
        dash = c.wait_gump(lambda x: 100 in buttons(x), 6, since=ng)
        if not dash:
            return False, "the dashboard has no Civic Mule Exchange button"
        ng = len(c.gumps)
        c.reply_gump(dash, 100)
        ex = c.wait_gump(lambda x: any("The Civic Mule Exchange" in t for t in all_text(c, x)), 6, since=ng)
        if not ex:
            return False, "no Exchange gump"
        lines = all_text(c, ex)
        up = next((bt for bt in buttons(ex) if 100 <= bt < 110), None)
        if up is None:
            return False, "the Exchange shows no upgrade button: %s" % lines[:8]
        n = len(c.messages)
        c.reply_gump(ex, up)
        c.sleep(2.5)
        said = [t for t in c.texts_since(n) if "mule is now" in t]
        aft = {"hue": (c.mobiles.get(mule) or {}).get("hue"), "hits": get_prop(a, mule, "HitsMax"), "bonded": get_prop(a, mule, "IsBonded"),
               "items": get_prop(a, mpack, "TotalItems") if mpack else None, "weight": get_prop(a, mpack, "MaxWeight") if mpack else None}
        props = [x for x in opl(c, mule)]
        tier = next((x for x in props if x.startswith("Tier ")), None)
        nm = name_of(c, mule)

        def i(v):
            try:
                return int(v)
            except (TypeError, ValueError):
                return None
        ok = (bool(said) and aft["hue"] == 0x972 and tier and tier.startswith("Tier 2") and "Bot Mule" in nm
              and i(aft["hits"]) and i(b["hits"]) and i(aft["hits"]) > i(b["hits"]) and 200 <= i(aft["hits"]) <= 250
              and i(aft["weight"]) and i(b["weight"]) and i(aft["weight"]) > i(b["weight"])
              and aft["items"] == b["items"] and aft["bonded"] == "True")
        delete_mobile(a, mule)
        return ok, "said %s; %r %r; before %s; after %s (hue 0x%X)" % (said, nm, tier, b, aft, aft["hue"] or 0)
    finally:
        ctx.done(c)


@check("P69-3", "Breed on two owned mules, one of each sex: the mating message, both on cooldown", kind="BOT-PARTIAL",
       left="P69-3: the foal deed in the bank 24 hours later (DeliverAt is read-only and gestation fixed; the bot cannot wait it out)")
def p69_3(ctx):
    # PackMule.cs:641-651, :725-736 (entry 1900000, range 2), PackMuleBreedingSystem.cs:19-76 (the target, the
    # refusals, "The mules seem quite taken with each other. A foal deed will arrive in your bank box in 24 hours.",
    # a 7-day cooldown on both), BredPackMuleDeed.cs:60-66 (DeliverAt read-only).
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=False)
    try:
        m1 = deed_mule(a, c)
        m2 = deed_mule(a, c)
        if not (m1 and m2):
            return False, "two deed mules: %s %s" % (m1, m2)
        staff_on(a, "[set IsMale true", m1, 0.4)
        staff_on(a, "[set IsMale false", m2, 0.4)
        # Same sex first: refused.
        staff_on(a, "[set IsMale true", m2, 0.4)
        c.sleep(1.0)
        n = len(c.messages)
        c.menu_pick(m1, 1900000)
        if c.wait_target(6):
            c.target_obj(m2)
        c.sleep(1.5)
        same = [t for t in c.texts_since(n) if "Both mules are male" in t]
        staff_on(a, "[set IsMale false", m2, 0.4)
        c.sleep(1.0)
        n = len(c.messages)
        c.menu_pick(m1, 1900000)
        if c.wait_target(6):
            c.target_obj(m2)
        c.sleep(2.0)
        ok_line = [t for t in c.texts_since(n) if "taken with each other" in t]
        nb1, nb2 = get_prop(a, m1, "NextBreedTime"), get_prop(a, m2, "NextBreedTime")
        delete_mobile(a, m1)
        delete_mobile(a, m2)
        ok = bool(same) and bool(ok_line) and bool(nb1) and bool(nb2)
        return ok, "same sex: %s; one of each: %s; NextBreedTime %s / %s" % (same[-1:], ok_line[-1:], nb1, nb2)
    finally:
        ctx.done(c)


from . import checks_p223f  # noqa: E402,F401
