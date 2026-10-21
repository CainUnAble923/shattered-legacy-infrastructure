"""cc-P224, group: the Compact Mule and its modules (cc-P69 PackMule.cs, cc-P70 Mobiles/Mule/*.cs). One check per play-test
id; every asserted string is read from the code under test and cited at the check. Written 2026-10-20 against the files as
they stood then, not run against a shard: the Golem runs them.

Shared state within one run (checks.SHARED): one mule per tier ("p224_mule_<tier>"), re-used by the checks that need that
tier, pinned beside the player and stripped of its modules before each use. Everything else is given fresh each run.

What the bot cannot grant: a guild's scrip. [CompactVouchers sets Mining Vouchers and [GuildStanding sets standing, but no
staff command writes GuildCurrency for any other guild (grep of server/customizations, 2026-10-20). So the schematic
purchase (P70-1) is exercised at the Miners' Compact, whose module is priced in vouchers alone, and the Tinkers' recipes the
crafting checks need come from [Recipes grant (ClusterFRecipeStore.cs:276, :308-309).
"""
import re
import time

from .checks import check, all_text, buttons, entries, near, give, staff_target, SHARED
from .checks_p224 import set_skill_to, spawn, set_on, make_pet, corpse_near, wait_text
from . import packets as P

MULE_BODY = 0x123            # PackMule.cs:21, :129 (the pack horse body)
OPTIONS_CLILOC = 3000127     # MuleOptionsEntry.Cliloc, MuleModuleGumps.cs:22 ("Options")
TALK_CLILOC = 3006146           # GuildMembershipEntry, ClusterFGuildSystem.cs:790 ("Talk" on a guildmaster)
REMOVE_BASE, TRACK_BASE = 100, 200   # MuleOptionsGump.RemoveBase / TrackBase, MuleModuleGumps.cs:39-40
BTN_DIG, BTN_BANK = 10, 14           # MuleModuleGumps.cs:42, :46
BTN_HALL_JOIN, BTN_HALL_SCHEMATICS = 1, 7   # GuildTaskDetailGump, ClusterFGuildSystem.cs:1041, :1046
MODULE_ART = 0x1053          # the Clockwork Load Harness, MuleModuleData.cs:69 (gears art, so a gear pile looks the same)
INGOT, GEARS, BONE, DAEMON_BONE = 0x1BF2, 0x1053, 0xF7E, 0xF80
DESPISE = (5501, 570, 59)    # the DungeonRegion's go spot on Felucca and Trammel: pinned Distribution/Data/regions.json:733-739, :2383-2389
CONTENTS = re.compile(r"Contents: (\d+)/(\d+) items, (\d+)/(\d+) stones")   # cliloc 1072241, pinned Server/Items/Container.cs:649


# -- helpers -----------------------------------------------------------------------------------------------------------

def dist(m, pos):
    return max(abs((m.get("x") or -9999) - pos[0]), abs((m.get("y") or -9999) - pos[1]))


def followers(c):
    c.send(P.status_request(c.serial, 4))
    c.sleep(0.8)
    return c.status.get("followers")


def mule_pack(c, mule):
    """The mule's pack: the layer-0x15 item the mule's 0x78 lists (PackMule.cs:146 AddItem(new PackMuleBackpack()))."""
    return next((s for s, it in c.items.items() if it.get("parent") == mule and it.get("layer") == 0x15), None)


def hits_of(c, mob):
    """The mobile's hits from the short status (0x11) a status request brings; None when it did not answer."""
    if mob not in c.mobiles:
        return None
    c.mobile_name(mob)
    return c.mobiles.get(mob, {}).get("hits")


def pack_count(c, graphic, skip_modules=False):
    """Pieces of one graphic in the pack (piles summed). skip_modules leaves out items whose tooltip is a mule module's
    (MuleModule.GetProperties, MuleModule.cs:95), since the Clockwork Load Harness wears the gears' art."""
    total = 0
    for s, it in c.items.items():
        if it.get("container") != c.backpack() or it.get("graphic") != graphic:
            continue
        if skip_modules and any("Mule module:" in t for t in (c.opl(s) or [])):
            continue
        total += it.get("amount") or 1
    return total


def modules_in_pack(c, tier_text):
    """Serials of mule modules in the pack whose tooltip says `tier_text` (e.g. "Tinkers' Union, tier I")."""
    out = []
    for s, it in list(c.items.items()):
        if it.get("container") == c.backpack() and it.get("graphic") == MODULE_ART:
            if any(("Mule module: " + tier_text) == t for t in (c.opl(s) or [])):
                out.append(s)
    return out


def button_beside(g, label, dx=35, dy=2, exact=True):
    """The button drawn dx left of and dy above a label: the mule gumps (AddButton(20, y), AddLabel(55, y + 2)) and the craft
    gump (AddButton(x, y), AddLabel(x + 35, y + 3)) both lay rows out that way."""
    es = entries(g)
    for e in es:
        if e[0] == "text" and (e[3] == label if exact else label in e[3]):
            for b in es:
                if b[0] == "button" and b[1] == e[1] - dx and b[2] == e[2] - dy:
                    return b[3]
    return None


def fit(p, mule, serial, name, timeout=6):
    """Drops a module on the mule (PackMule.OnDragDrop :630-633 -> MuleModules.TryAttach :153-172). Returns (fitted, texts):
    fitted when "You fit the {name} to the mule." (:169) came and the item left the pack (Internalize, :164)."""
    n = len(p.messages)
    p.lift_drop(serial, 1, mule)
    said = wait_text(p, r"You fit the %s to the mule" % re.escape(name), n, timeout)
    gone = p.wait(lambda: serial not in p.items or p.items[serial].get("container") != p.backpack(), 4)
    return bool(said and gone), p.texts_since(n)


def options(p, mule, timeout=8):
    """The mule's Options window (MuleOptionsEntry, MuleModuleGumps.cs:20-35; the title "{name}: modules n / m", :63)."""
    m = p.context_menu(mule)
    if not m:
        raise AssertionError("the mule 0x%X gave no context menu" % mule)
    n = len(p.gumps)
    p.pick_context(m, OPTIONS_CLILOC)
    g = p.wait_gump(lambda g: any(": modules " in t for t in all_text(p, g)), timeout, since=n)
    if not g:
        raise AssertionError("Options opened no mule window; the menu was %s" % [(e["cliloc"], e.get("text")) for e in m["entries"]])
    return g


def remove_rows(g):
    """[(button id, label)] of the "Remove {module}" rows (MuleModuleGumps.cs:67-72): button RemoveBase + i at (20, y), label at (55, y + 2)."""
    es = entries(g)
    out = []
    for e in es:
        if e[0] == "button" and REMOVE_BASE <= e[3] < TRACK_BASE:
            label = next((t[3] for t in es if t[0] == "text" and t[1] == 55 and t[2] == e[2] + 2), "")
            out.append((e[3], label))
    return out


def remove_module(p, mule, name_part):
    """Options > Remove for the module whose label contains name_part (Detach, MuleModules.cs:175-198). Returns the texts."""
    g = options(p, mule)
    row = next((r for r in remove_rows(g) if name_part in r[1]), None)
    if row is None:
        p.reply_gump(g, 0)
        return None
    n = len(p.messages)
    p.reply_gump(g, row[0])
    p.sleep(1.2)
    return p.texts_since(n)


def strip_modules(p, mule):
    removed = []
    for _ in range(6):
        g = options(p, mule)
        rows = remove_rows(g)
        if not rows:
            p.reply_gump(g, 0)
            break
        p.reply_gump(g, rows[0][0])
        p.sleep(1.2)
        removed.append(rows[0][1])
    return removed


def mule_for(ctx, a, p, tier):
    """This run's mule of that tier: re-placed beside the player, pinned (CantWalk) so drops and menus stay in range, with no
    modules on it. A new one ([add PackMule <tier>, owned through make_pet) when there is none, it is dead, or out of view.
    Returns (serial, new)."""
    key = "p224_mule_%d" % tier
    mule = SHARED.get(key)
    near(a, p)
    x, y, z = p.pos[:3]
    if mule and mule in p.mobiles and (hits_of(p, mule) or 0) > 0:
        set_on(a, mule, "X %d Y %d Z %d CantWalk true" % (x + 1, y, z), p)
        p.sleep(0.8)
        strip_modules(p, mule)
        return mule, False
    mule = make_pet(a, p, "PackMule %d" % tier, MULE_BODY)
    if not mule:
        raise AssertionError("[add PackMule %d brought no mule (body 0x123) into the player's view" % tier)
    set_on(a, mule, "CantWalk true", p)
    p.pet_command("all follow me")      # order Follow: what a player's mule has (Homing reads it, MuleModules.cs:553)
    SHARED[key] = mule
    return mule, True


def staff_get(a, serial, prop, who=None):
    """[get <prop> on a serial, by the admin; returns the value text of "<prop> = <value>" (pinned Properties.cs:329)."""
    if who is not None:
        near(a, who)
    n = len(a.messages)
    a.command("[get " + prop, 0.2)
    if not a.wait_target(8):
        raise AssertionError("[get %s gave no target cursor" % prop)
    a.target_obj(serial)
    m = a.find_text(re.escape(prop) + r" = ", since=n, timeout=5)
    return m["text"].split(" = ", 1)[1].strip() if m else None


def teleport(a, serial, x, y, z=None):
    """Staff: [set X Y [Z] on a mobile (Mobile.X/Y/Z are GameMaster command properties, pinned Server/Mobiles/Mobile.cs:2647-2662)."""
    set_on(a, serial, "X %d Y %d" % (x, y) + ("" if z is None else " Z %d" % z))


def stash_on_ground(c, serials):
    """Drops items at the character's own feet (0x08 to the world, container 0xFFFFFFFF, the character's z) and returns the
    ones that left the pack; unstash() picks them up again. Used to clear a craft's previous-tier check of extra modules."""
    out = []
    x, y, z = c.pos[:3]
    for s in serials:
        c.send(P.lift(s, 1))
        c.sleep(0.4)
        c.send(P.drop(s, x, y, z, 0xFFFFFFFF))
        if c.wait(lambda: c.items.get(s, {}).get("container") != c.backpack(), 4):
            out.append(s)
    return out


def unstash(c, serials):
    for s in serials:
        c.lift_drop(s, 1, c.backpack())
        c.sleep(0.6)


def clear_player_modules(a, p, keep=()):
    """Admin-deletes every mule module in the player's pack whose serial is not in `keep`. Instant (staff [Delete on the
    serial, no lift/drop throttle), so the pack does not fill with the modules earlier mule checks left: P70-2 makes a
    tier I each run and the ground-stash dropped only some, so by P70-5/P70-6 several tier-I modules remained, the
    "no tier I left" phase never ran and a full pack refused the detached tack (cc-P224 second session, 2026-10-21).
    Returns how many were deleted."""
    keep = set(keep)
    targets = [s for s, it in list(p.items.items())
               if it.get("container") == p.backpack() and it.get("graphic") == MODULE_ART
               and s not in keep and any(t.startswith("Mule module:") for t in (p.opl(s) or []))]
    gone = 0
    for s in targets:
        a.command("[Delete", 0.2)
        if a.wait_target(6):
            a.target_obj(s)
            if p.wait(lambda s=s: s not in p.items, 4):
                gone += 1
    return gone


def craft_gump(c, since, timeout=12):
    """The craft menu (pinned CraftGump.cs:71, cliloc 1044010 "<CENTER>CATEGORIES</CENTER>") sent since `since`."""
    return c.wait_gump(lambda g: any("CATEGORIES" in t for t in all_text(c, g)), timeout, since=since)


def open_craft(c, tool):
    n = len(c.gumps)
    c.dclick(tool)
    g = craft_gump(c, n, 8)
    if not g:
        raise AssertionError("the tool opened no craft menu")
    return g


def craft(c, g, group, item):
    """Presses the group, then the item's make button (pinned CraftGump.cs:472-476 groups at (15, y)/(50, y + 3); :402-410 items at
    (220, y)/(255, y + 3); GetButtonID :492). Returns (texts of the gump that came back, what went wrong or None)."""
    gb = button_beside(g, group, 35, 3)
    if gb is None:
        return all_text(c, g), "no %r group in the menu" % group
    n = len(c.gumps)
    c.reply_gump(g, gb)
    g2 = craft_gump(c, n, 8)
    if not g2:
        return [], "the group brought no menu back"
    ib = button_beside(g2, item, 35, 3)
    if ib is None:
        rows = [e[3] for e in entries(g2) if e[0] == "text" and e[1] == 255]
        return all_text(c, g2), "no %r row in the group (rows: %s)" % (item, rows)
    n = len(c.gumps)
    c.reply_gump(g2, ib)
    g3 = craft_gump(c, n, 12)
    if not g3:
        return [], "no craft menu came back after the make button"
    return all_text(c, g3), None


def hall_page(p, guildmaster):
    """The guild hall page at a guildmaster: its "Talk" context entry (GuildMembershipEntry, ClusterFGuildSystem.cs:786-797) ->
    GuildTaskDetailGump (:1052-1193, the "Apprentice task:" label at :1145)."""
    m = p.context_menu(guildmaster)
    n = len(p.gumps)
    if m and any(e["cliloc"] == TALK_CLILOC for e in m["entries"]):
        p.pick_context(m, TALK_CLILOC)
    else:
        p.say("guild menu")     # the speech trigger, ClusterFGuildmasterExtension.cs:40-41
    return p.wait_gump(lambda g: any(t.startswith("Apprentice task") for t in all_text(p, g)), 8, since=n)


# -- P69-6: an existing mule's tier line, slot cost and follower count --------------------------------------------------

@check("P69-6", "A tier-3 mule shows 'Tier 3: Shadowhide Mule', lists its module slots, and costs its owner 1 follower slot")
def p69_6(ctx):
    # PackMule.GetProperties :218 "Tier {n}: {TierInfo.Name} Mule ({sex})" with MuleData.Tiers[2].Name "Shadowhide" (MuleData.cs:60);
    # :227 "Module slots: {n} / {m}"; the slot cost is MuleNumbers.SlotCost = 1 (:35) written by SetSlotCost (:369-390), which
    # AddFollowers the owner. This world has no pre-cc-P69 mule to look at, so a new tier-3 mule is made for the player
    # (make_pet: [add PackMule 3, then [set Controlled true ControlMaster <player>); the evidence says so.
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    f0 = followers(p)
    mule = make_pet(a, p, "PackMule 3", MULE_BODY)
    if not mule:
        raise AssertionError("[add PackMule 3 brought no mule (body 0x123) into view")
    set_on(a, mule, "CantWalk true", p)
    p.pet_command("all follow me")
    SHARED["p224_mule_3"] = mule        # the tier-3 mule the later checks re-use
    props = p.opl(mule) or []
    tier_line = next((t for t in props if t.startswith("Tier ")), None)
    slots = next((t for t in props if t.startswith("Module slots:")), None)
    f1 = followers(p)
    ok = tier_line is not None and tier_line.startswith("Tier 3: Shadowhide Mule") and slots == "Module slots: 0 / 3" \
        and f0 is not None and f1 == f0 + 1
    return ok, "a NEW tier-3 mule (no old mules exist here; the play test's 'existing mules' are the same code path): %r; %r; " \
               "owner's followers %s -> %s (1 slot); other lines %s" % (tier_line, slots, f0, f1, [t for t in props if t not in (tier_line, slots)][:4])


# -- P70-1: the schematic shop ------------------------------------------------------------------------------------------

@check("P70-1", "A member at the guildmaster buys a mule module schematic from 'Mule module schematics' and the account learns it "
                "(Miners' Compact, Ore Hopper I: priced in Mining Vouchers, which staff can set)",
       kind="BOT-PARTIAL",
       left="P70-1: the Tinkers' Union half as written (20 Tinkers' scrip, Clockwork Load Harness I): no staff command grants a "
            "guild's scrip, only [CompactVouchers (mining) and [GuildStanding (standing); the Apprentice task pays 10")
def p70_1(ctx):
    # Hall page: GuildTaskDetailGump (ClusterFGuildSystem.cs:1052-1253): "Join the guild" = button 1 at the master (:1041, :1176,
    # :1212-1214; Join says "You have joined the {name} as {rank}. Joining is free." :435); "Mule module schematics" = button 7 for
    # members at the master (:1046, :1126-1131, :1233-1235) -> GuildSchematicsGump (MuleModuleGumps.cs:568-629): title :586, rows
    # "{Name} {grade}: {cost} + {n} Mining Vouchers" with button 1.. (:600-602). Buy (:539-565): "You learn the {name} {grade}
    # schematic ({system})." (:563), or RefuseBuy's "Your account already knows that schematic." (:495) on a rerun. Gates: rank
    # index >= RankFor[0] = 1 (:477, :503), which mining's ladder gives at standing 1000 (ClusterFGuildSystem.cs:618, :632-656),
    # set by [CompactStanding (ClusterFCompactAdminTools.cs:48-78); the Miners' own module costs 20 + 5 Mining Vouchers (:479-480,
    # :530-533), set by [CompactVouchers (:80-109). The recipe id is 9011 (MuleModule.cs:235: 9000 + OreHopper(1) x 10 + 1), which
    # [Recipes list prints as "recipe:9011" (ClusterFRecipeStore.cs:36, :340-348).
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    set_skill_to(a, p, "Mining", 100)     # ClusterFGuildPromotion caps a tracked guild's rank by its craft skill (Update at the hall, :534)
    gm = spawn(a, p, "MinerGuildmaster")
    if not gm:
        raise AssertionError("[add MinerGuildmaster brought no guildmaster into view")
    staff_target(a, "[CompactStanding 1000", p)
    staff_target(a, "[CompactVouchers 30", p)
    g = hall_page(p, gm)
    if not g:
        return False, "the guildmaster's Talk opened no hall page"
    texts = all_text(p, g)
    joined = None
    if BTN_HALL_JOIN in buttons(g) and any("Join the guild" in t for t in texts):
        n, nm = len(p.gumps), len(p.messages)
        p.reply_gump(g, BTN_HALL_JOIN)
        joined = wait_text(p, r"You have joined the Miners' Compact", nm, 5)
        g = p.wait_gump(lambda x: any(t.startswith("Apprentice task") for t in all_text(p, x)), 8, since=n) or hall_page(p, gm)
        texts = all_text(p, g) if g else []
    member = next((t for t in texts if t.startswith("Member. Rank:")), None)
    if BTN_HALL_SCHEMATICS not in buttons(g):
        return False, "joined now: %r; %r; no 'Mule module schematics' button (7) on the hall page: %s" % (joined, member, texts)
    n = len(p.gumps)
    p.reply_gump(g, BTN_HALL_SCHEMATICS)
    sg = p.wait_gump(lambda x: any(t == "Mule module schematics" for t in all_text(p, x)), 8, since=n)
    if not sg:
        return False, "button 7 opened no 'Mule module schematics' page"
    row = button_beside(sg, "Ore Hopper I:", 35, 2, exact=False)
    if row is None:
        return False, "no 'Ore Hopper I' row on the schematics page: %s" % all_text(p, sg)
    n, nm = len(p.gumps), len(p.messages)
    p.reply_gump(sg, row)
    learned = wait_text(p, r"You learn the Ore Hopper I schematic", nm, 5)
    known = None if learned else wait_text(p, r"already knows that schematic", nm, 2)
    sg2 = p.wait_gump(lambda x: any(t == "Mule module schematics" for t in all_text(p, x)), 5, since=n)
    row_note = next((t for t in all_text(p, sg2) if "schematic" in t and "Ore Hopper" not in t), None) if sg2 else None
    if sg2:
        p.reply_gump(sg2, 0)
    near(a, p)
    na = len(a.messages)
    a.command("[Recipes list", 0.2)
    if a.wait_target(8):
        a.target_obj(p.serial)
    a.sleep(1.5)
    listed = any("recipe:9011" in t for t in a.texts_since(na))
    ok = bool(learned or known) and listed
    return ok, "Miners' Compact in place of the Tinkers' Union (no staff scrip grant); joined now: %r; %r; buy said %r; " \
               "page then says %r; [Recipes list shows recipe:9011: %s" % (joined, member, learned or known, row_note, listed)


# -- P70-2: crafting the Clockwork Load Harness I -----------------------------------------------------------------------

@check("P70-2", "Tinker tools, 'Mule Modules': the Clockwork Load Harness I is refused without 15 ingots, 2 gears and 10 bone, and made with them")
def p70_2(ctx):
    # MuleModuleCrafts.Rows (MuleModule.cs:189-191): Tinkering, iron ingots 15, gears 2, and bone 10 (PartAmounts :176), in the group
    # "Mule Modules" (:166), from skill 50 (:169), never exceptional (:280). The recipe is 9041 (:235), a quest recipe (:281) the
    # account learns from the Tinkers' schematic; here staff teach it ([Recipes grant recipe:9041, ClusterFRecipeStore.cs:276,
    # :308-309) since no staff command grants the scrip the schematic costs. Refused without materials: pinned CraftItem.cs:748 ->
    # 1044253 "You don't have the components needed to make that."; made: DefTinkering.cs:132 -> 1044154 "You create the item."
    # Both arrive as the notice of the craft menu sent back. The made item's tooltip: "Mule module: Tinkers' Union, tier I" (:95).
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    set_skill_to(a, p, "Tinkering", 100)
    staff_target(a, "[Recipes grant recipe:9041", p)
    tool = give(a, p, "TinkerTools")
    have = (pack_count(p, INGOT), pack_count(p, GEARS, skip_modules=True), pack_count(p, BONE))
    short = have[0] < 15 or have[1] < 2 or have[2] < 10
    refused = None
    if short:
        texts, why = craft(p, open_craft(p, tool), "Mule Modules", "Clockwork Load Harness I")
        if why:
            return False, "before the materials: %s" % why
        # The craft is refused when any required resource is short: the generic "components needed to make that"
        # (1044253), or a resource-specific line. Here the pack's ingots are coloured (the P56 smelting checks leave
        # valorite etc., same graphic as iron), so the recipe's plain iron reads 0 and the refusal is the metal line
        # "You do not have sufficient metal to make that." (cc-P224 second session, seen 2026-10-21). Either counts.
        refused = any(m in t for t in texts for m in (
            "components needed to make that", "sufficient metal to make that", "do not have the resources"))
    for what in ("IronIngot 15", "Gears 2", "Bone 10"):
        give(a, p, what)
    before = set(modules_in_pack(p, "Tinkers' Union, tier I"))
    texts, why = craft(p, open_craft(p, tool), "Mule Modules", "Clockwork Load Harness I")
    if why:
        return False, "with the materials: %s" % why
    made = any("You create the item" in t for t in texts)
    # The made module's 0x25 follows the craft gump by a moment (the first trial read the pack too soon): poll for it.
    new = p.wait(lambda: [s for s in modules_in_pack(p, "Tinkers' Union, tier I") if s not in before] or None, 8) or []
    notice = [t for t in texts if "item" in t or "recipe" in t or "skill" in t][:2]
    ok = made and bool(new) and (refused is True or not short)
    return ok, "refusal without materials: %s (pack had ingots %d, gears %d, bone %d%s); with them: made %s, new tier-I harness %s; notice %s" % (
        refused, have[0], have[1], have[2], "" if short else "; all present from leftovers, the refusal half was skipped",
        made, ["0x%X" % s for s in new], notice)


# -- P70-3 / P70-4: attaching and removing --------------------------------------------------------------------------------

@check("P70-3", "A module dropped on the mule is fitted and listed; Options > Remove puts it back in the pack, still tier I")
def p70_3(ctx):
    # TryAttach (MuleModules.cs:153-172): "You fit the {name} to the mule." (:169), the item internalized (:164); the mule's tooltip
    # then holds "Module slots: 1 / 1" and "Module: Clockwork Load Harness I" (PackMule.cs:227, :233). Options (cliloc 3000127,
    # MuleModuleGumps.cs:22) -> "Remove {name}" button 100 + i (:39, :69-70, :166-168) -> Detach (MuleModules.cs:175-198): "You take
    # the {name} off the mule." (:191), the same item back in the pack; its tooltip "Mule module: Tinkers' Union, tier I" (MuleModule.cs:95).
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    mule, _ = mule_for(ctx, a, p, 1)
    s = give(a, p, "MuleModule ClockworkLoadHarness 1")
    fitted, texts = fit(p, mule, s, "Clockwork Load Harness I")
    props = p.opl(mule) or []
    listed = "Module: Clockwork Load Harness I" in props
    slots = next((t for t in props if t.startswith("Module slots:")), None)
    n = len(p.messages)
    removed = remove_module(p, mule, "Clockwork Load Harness I")
    took = wait_text(p, r"You take the Clockwork Load Harness I off the mule", n, 4)
    back = p.wait(lambda: p.items.get(s, {}).get("container") == p.backpack(), 6)
    tier = next((t for t in (p.opl(s) or []) if t.startswith("Mule module:")), None) if back else None
    ok = fitted and listed and slots == "Module slots: 1 / 1" and bool(took) and bool(back) and tier == "Mule module: Tinkers' Union, tier I"
    return ok, "fitted %s (%s); mule lists it %s, %r; Remove row %s -> %r; back in the pack %s as %r" % (
        fitted, texts[-1:] if texts else None, listed, slots, removed is not None, took, bool(back), tier)


@check("P70-4", "A tier III module is refused on a tier 1 mule and stays in the pack")
def p70_4(ctx):
    # RefuseAttach (MuleModules.cs:134-137): "A tier III module needs a tier 3 mule or better; yours is tier 1." The module is never
    # deleted (:629, TryAttach :155-161): OnDragDrop returns false and the item bounces back to the player.
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    mule, _ = mule_for(ctx, a, p, 1)
    s = give(a, p, "MuleModule TimberRack 3")
    n = len(p.messages)
    p.lift_drop(s, 1, mule)
    said = wait_text(p, r"A tier III module needs a tier 3 mule or better; yours is tier 1\.", n, 6)
    back = p.wait(lambda: p.items.get(s, {}).get("container") == p.backpack(), 6)
    props = p.opl(mule) or []
    ok = bool(said) and bool(back) and "Module: Timber Rack III" not in props
    return ok, "refusal %r; module back in the pack %s; mule's module lines %s" % (said, bool(back), [t for t in props if t.startswith("Module")])


# -- P70-5: tier II built from a tier I --------------------------------------------------------------------------------------

@check("P70-5", "Tier II is crafted from a tier I in the pack; the tier I is gone after the success, and a second try is refused")
def p70_5(ctx):
    # MuleModuleCrafts.CheckPreviousTier (MuleModule.cs:293-326), called from the patched CraftItem.ConsumeAttributes (pinned
    # CraftItem.cs:336-341, after the material check at :1038 so the materials must be there to reach it): a tier-II craft needs a
    # tier-I of the same kind in the pack, taken only with consume (a successful craft, :320-323); without one the notice is "You
    # need a Clockwork Load Harness I in your backpack to build on." (:316). Tier II: 30 ingots, 4 gears, 5 daemon bone (:190, :176),
    # skill 70 (:169), recipe 9042 (:235) taught by staff here (see P70-2). At Tinkering 100 the craft cannot fail (max 70 + 40).
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    set_skill_to(a, p, "Tinkering", 100)
    staff_target(a, "[Recipes grant recipe:9042", p)
    tool = give(a, p, "TinkerTools")
    # Clear every mule module from the pack first (instant staff delete), then add exactly one tier I: the craft must
    # have one and only one tier-I candidate to consume, and the "no tier I left" phase below needs the pack to reach
    # zero tier-I modules. The earlier ground-stash dropped only some and left tier-I modules behind (cc-P224 second
    # session, 2026-10-21), so clear_player_modules replaces it; nothing to unstash afterwards.
    clear_player_modules(a, p)
    s1 = give(a, p, "MuleModule ClockworkLoadHarness 1")
    ones = [s1]
    stashed = []
    if True:
        for what in ("IronIngot 60", "Gears 8", "DaemonBone 10"):    # two crafts' worth: the second try must get past the material check
            give(a, p, what)
        twos_before = set(modules_in_pack(p, "Tinkers' Union, tier II"))
        # A craft can fail its skill roll ("You failed to create the item, and some of your materials are lost.", pinned
        # CraftItem.cs 1044043): up to six tries, and after each failure the tier I must still be there, which is the
        # item's own "used up only if the craft succeeds".
        made, failed, kept_on_failure = False, 0, True
        for _ in range(6):
            texts, why = craft(p, open_craft(p, tool), "Mule Modules", "Clockwork Load Harness II")
            if why:
                return False, why
            if any("You create the item" in t for t in texts):
                made = True
                break
            if any("failed to create the item" in t for t in texts):
                failed += 1
                p.sleep(0.5)
                if s1 not in p.items or p.items[s1].get("container") != p.backpack():
                    kept_on_failure = False
                for what in ("IronIngot 30", "Gears 4", "DaemonBone 5"):
                    give(a, p, what)
                continue
            break
        one_gone = made and bool(p.wait(lambda: s1 not in p.items or p.items[s1].get("container") != p.backpack(), 4))
        twos = [s for s in modules_in_pack(p, "Tinkers' Union, tier II") if s not in twos_before]
        ones_left = modules_in_pack(p, "Tinkers' Union, tier I")
        refused = None
        if not ones_left:
            texts2, why2 = craft(p, open_craft(p, tool), "Mule Modules", "Clockwork Load Harness II")
            refused = any("You need a Clockwork Load Harness I in your backpack to build on" in t for t in texts2) if not why2 else why2
    ok = kept_on_failure and made and one_gone and bool(twos) and refused is True
    return ok, "tier-I 0x%X used (pack cleared of modules first, then one tier I added); craft: made %s after %d failed roll(s) (tier I kept through each failure: %s), the tier I gone %s, new tier II %s; " \
               "second try with no tier I: refused %s%s" % (
                   s1, made, failed, kept_on_failure, one_gone, ["0x%X" % s for s in twos], refused,
                   "" if not ones_left else " (skipped: tier I(s) still in the pack %s)" % ["0x%X" % s for s in ones_left])


# -- P70-6: Trailwise Tack III and the follower slot ------------------------------------------------------------------------

@check("P70-6", "Trailwise Tack III on a tier-3 mule: the owner's followers drop by one, and come back when it is removed")
def p70_6(ctx):
    # MuleModules.Apply (MuleModules.cs:259-267): tier >= 3 sets SlotCost 0; PackMule.SetSlotCost (:369-390) removes and re-adds the
    # followers at the new cost; Detach (:194) applies the numbers again.
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    clear_player_modules(a, p)   # earlier mule checks leave modules in the pack; Detach needs room to return the tack
    # By this point the pack also carries a lot of accumulated loot, so Detach's put-back was refused with "cannot hold
    # more weight" and the module stayed off the pack (cc-P224 second session, 2026-10-21). Raise RawStr for headroom.
    set_on(a, p.serial, "RawStr 400", p)
    mule, _ = mule_for(ctx, a, p, 3)
    f0 = followers(p)
    s = give(a, p, "MuleModule TrailwiseTack 3")
    fitted, texts = fit(p, mule, s, "Trailwise Tack III")
    f1 = followers(p)
    remove_module(p, mule, "Trailwise Tack III")
    back = p.wait(lambda: p.items.get(s, {}).get("container") == p.backpack(), 6)
    f2 = followers(p)
    ok = fitted and f0 is not None and f1 == f0 - 1 and f2 == f0 and bool(back)
    return ok, "fitted %s; followers %s -> %s with the tack -> %s without; module back in the pack %s; %s" % (fitted, f0, f1, f2, bool(back), texts[-1:])


# -- P70-7: Homing and "all come" ----------------------------------------------------------------------------------------------

@check("P70-7", "Trailwise Tack II: left behind on the same map, the mule is beside its owner within seconds; 'all come' reaches it from far away")
def p70_7(ctx):
    # Homing (MuleModules.cs:548-574): order Follow or Come, more than 12 tiles off on the same map for 3 s -> MoveToWorld to the
    # owner. "all come" (keyword 0x164) from any distance: OnSpeech (:576-594) moves a Trailwise-II mule farther than 14 tiles to
    # the speaker. The owner is moved by staff ([set X Y, same map) in place of a gate; the mule is pinned (CantWalk) so only the
    # module can bring it. Told to stay first (:553 refuses Homing on Stay), the second move shows "all come" doing it.
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    mule, _ = mule_for(ctx, a, p, 3)
    s = give(a, p, "MuleModule TrailwiseTack 2")
    fitted, _ = fit(p, mule, s, "Trailwise Tack II")
    p.pet_command("all follow me")
    ox, oy, oz = p.pos[:3]
    try:
        teleport(a, p.serial, ox - 25, oy)
        moved = p.wait(lambda: abs(p.pos[0] - (ox - 25)) <= 1, 5)
        t0 = time.monotonic()
        came = p.wait(lambda: mule in p.mobiles and dist(p.mobiles[mule], p.pos) <= 2, 12)
        homing_s = round(time.monotonic() - t0, 1)
        p.pet_command("all stay")
        teleport(a, p.serial, ox, oy - 25)
        moved2 = p.wait(lambda: abs(p.pos[1] - (oy - 25)) <= 1, 5)
        stayed = not p.wait(lambda: mule in p.mobiles and dist(p.mobiles[mule], p.pos) <= 2, 6)
        p.pet_command("all come")
        t1 = time.monotonic()
        came2 = p.wait(lambda: mule in p.mobiles and dist(p.mobiles[mule], p.pos) <= 2, 8)
        come_s = round(time.monotonic() - t1, 1)
    finally:
        teleport(a, p.serial, ox, oy, oz)
        p.sleep(0.5)
        p.pet_command("all come")
        p.sleep(1.5)
        p.pet_command("all follow me")
    ok = fitted and bool(moved) and bool(came) and bool(moved2) and stayed and bool(came2)
    return ok, "fitted %s; owner moved 25 tiles %s: mule beside within %ss (%s); told to stay and owner moved again %s: stayed away %s; " \
               "'all come' brought it within %ss (%s)" % (fitted, bool(moved), homing_s, bool(came), bool(moved2), stayed, come_s, bool(came2))


# -- P70-8: Soul Tether II ---------------------------------------------------------------------------------------------------------

@check("P70-8", "Soul Tether II: the killed mule's corpse is empty, its pack stays on the ghost, and it is resurrected")
def p70_8(ctx):
    # Apply (MuleModules.cs:279-285): tier >= 2 sets KeepPackOnDeath; PackMule.OnBeforeDeath (:655-670) and GetInventoryMoveResultFor
    # (:672-673) then keep the pack (MoveToBackpack). A bonded pet dies to a ghost (pinned BaseCreature.OnDeath :3507-3520, IsDeadPet),
    # so the mule is bonded first ([set IsBonded true, a GameMaster property, :693-696). The owner cannot open a dead pet's pack
    # (PackHorse.cs:137-140, :174-179), staff can, so the admin opens it; the pack's tooltip counts its contents either way (cliloc
    # 1072241, pinned Server/Items/Container.cs:649). Resurrection: the owner's own bandage at Veterinary/Animal Lore 80+ calls
    # ResurrectPet (pinned Bandage.cs:274-284). Soul Tether I's own rise comes only after 2 minutes (MuleModules.cs:694-718).
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    mule, _ = mule_for(ctx, a, p, 2)
    set_on(a, mule, "IsBonded true", p)
    s = give(a, p, "MuleModule SoulTether 2")
    fitted, _ = fit(p, mule, s, "Soul Tether II")
    ingots = give(a, p, "IronIngot 100")
    p.lift_drop(ingots, 100, mule)
    p.sleep(1.0)
    pack = mule_pack(p, mule)
    if not pack:
        raise AssertionError("the mule's pack (layer 0x15) is not in the player's view")
    m0 = CONTENTS.search(" ".join(p.opl(pack) or []))
    before_items = set(p.items)
    near(a, p)
    a.command("[kill", 0.2)
    if a.wait_target(8):
        a.target_obj(mule)
    dead = None
    for _ in range(8):
        p.sleep(1.0)
        if hits_of(p, mule) == 0:
            dead = True
            break
    ghost = mule in p.mobiles
    corpse = corpse_near(p, before_items, MULE_BODY, 6)
    corpse_items = None
    if corpse:
        p.dclick(corpse)
        p.sleep(1.5)
        corpse_items = list(p.contents(corpse))
    m1 = CONTENTS.search(" ".join(p.opl(pack) or []))
    a.dclick(pack)
    a.sleep(1.5)
    admin_sees = [it.get("graphic") for it in a.contents(pack).values()]
    set_skill_to(a, p, "Veterinary", 100)
    set_skill_to(a, p, "AnimalLore", 100)
    b = give(a, p, "Bandage 5")
    n = len(p.messages)
    p.dclick(b)
    if p.wait_target(6):
        p.target_obj(mule)
    res = False
    how = "the owner's bandage"
    for _ in range(10):
        p.sleep(1.5)
        if (hits_of(p, mule) or 0) > 0:
            res = True
            break
    if not res and any("cannot be resurrected at that location" in t for t in p.texts_since(n)):
        # Pinned Bandage.cs:488-490: CanFit(location, 16) fails where the ghost stands (seen 2026-10-20 beside its own
        # corpse in the square). Staff's [Resurrect calls ResurrectPet (pinned Commands.cs:1043); the evidence says so.
        how = "staff [Resurrect after the bandage's 501042"
        near(a, p)
        a.command("[Resurrect", 0.2)
        if a.wait_target(6):
            a.target_obj(mule)
        for _ in range(6):
            p.sleep(1.0)
            if (hits_of(p, mule) or 0) > 0:
                res = True
                break
    ok = fitted and dead is True and ghost and (corpse is None or corpse_items == []) and m1 is not None and int(m1.group(1)) >= 1 \
        and INGOT in admin_sees and res
    return ok, "fitted %s; pack before %r; killed (hits 0) %s, ghost still in view %s; corpse %s with %s items; pack tooltip after %r; " \
               "admin opening the pack sees graphics %s; resurrected by %s: %s (%s)" % (
                   fitted, m0.group(0) if m0 else None, dead, ghost, "none" if corpse is None else "0x%X" % corpse,
                   None if corpse_items is None else len(corpse_items), m1.group(0) if m1 else None, admin_sees, how, res, p.texts_since(n)[-2:])


# -- P70-9: the Ore Hopper's weight -------------------------------------------------------------------------------------------

@check("P70-9", "Ore Hopper: 100 iron ingots in the mule's pack weigh less once the hopper is fitted")
def p70_9(ctx):
    # PackMuleBackpack.GetTotal(Weight) takes WeightDiscount off (:35-61): ore, ingots and gems at the hopper's factor (MuleModules.cs
    # :328-359, tier I saves 25 points, plus any Burdened trait). The server-sent number: the pack's tooltip, cliloc 1072241
    # "Contents: n/m items, w/W stones" (pinned Server/Items/Container.cs:649), rebuilt when the pack's totals change (UpdateTotal ->
    # InvalidateProperties, :74-78), so a second 100 ingots are added after fitting to force a fresh line; the line read straight
    # after fitting is reported too (fitting alone invalidates the mule, :170, not its pack).
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    mule, _ = mule_for(ctx, a, p, 1)
    pack = mule_pack(p, mule)
    if not pack:
        raise AssertionError("the mule's pack (layer 0x15) is not in the player's view")
    m0 = CONTENTS.search(" ".join(p.opl(pack) or []))
    w_start = int(m0.group(3)) if m0 else None
    ingots = give(a, p, "IronIngot 100")
    p.lift_drop(ingots, 100, mule)
    p.sleep(1.2)
    m1 = CONTENTS.search(" ".join(p.opl(pack) or []))
    s = give(a, p, "MuleModule OreHopper 1")
    fitted, _ = fit(p, mule, s, "Ore Hopper I")
    m2 = CONTENTS.search(" ".join(p.opl(pack) or []))
    more = give(a, p, "IronIngot 100")
    p.lift_drop(more, 100, mule)
    p.sleep(1.2)
    m3 = CONTENTS.search(" ".join(p.opl(pack) or []))
    if not (m1 and m3) or w_start is None:
        return False, "no 'Contents: ... stones' line on the pack: %s / %s / %s" % (m0 and m0.group(0), m1 and m1.group(0), m3 and m3.group(0))
    w100, w_after_fit, w200 = int(m1.group(3)), int(m2.group(3)) if m2 else None, int(m3.group(3))
    per100 = w100 - w_start
    ok = fitted and per100 > 0 and (w200 - w_start) < 2 * per100
    return ok, "fitted %s; stones: pack alone %d, with 100 ingots %d (+%d), right after fitting %s, with 200 ingots %d (undiscounted would be +%d, " \
               "a 25%% hopper gives about +%d)" % (fitted, w_start, w100, per100, w_after_fit, w200, 2 * per100, int(2 * per100 * 0.75))


# -- P70-10: Survey Saddle Roll II and III -----------------------------------------------------------------------------------------

@check("P70-10", "Survey Saddle Roll: Options lists a decoded map in the pack and tracks it with a quest arrow; at the spot with III, 'Dig here' raises the chest and its guardians")
def p70_10(ctx):
    # TrackableMaps (MuleModules.cs:852-868): a TreasureMap, not Completed, with a Decoder; the Options window lists them under "Track
    # map (nearest by default):" with buttons TrackBase 200 + i (MuleModuleGumps.cs:85-95) -> Track (:879-902): the owner's QuestArrow
    # (0xBA) at ChestLocation. III: "Dig here for the tracked map" button 10 (:97-101) -> Dig (:907-926) within 6 tiles of the spot on
    # ChestMap: a TreasureMapChest at the spot and OnDug's guardians; otherwise "Track a decoded treasure map first, and stand within
    # 6 tiles of its spot." (:188). The map: [AddToPack TreasureMap 1 <the player's map> (pinned TreasureMap.cs:285-286), decoded by
    # [set Decoder <player> (its GameMaster property, :217-218); the spot read with [get ChestLocation. Staff move the owner and the
    # mule to the spot ([go x y for the z, then [set X Y Z).
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    mule, _ = mule_for(ctx, a, p, 3)
    ox, oy, oz = p.pos[:3]
    pmap = staff_get(a, p.serial, "Map", p) or "Trammel"
    tm = give(a, p, "TreasureMap 1 %s" % pmap)
    set_on(a, tm, "Decoder 0x%X" % p.serial, p)
    loc = staff_get(a, tm, "ChestLocation", p) or ""
    nums = re.findall(r"-?\d+", loc)
    if len(nums) < 2:
        return False, "[get ChestLocation on the map said %r" % loc
    cx, cy = int(nums[0]), int(nums[1])
    p.lift_drop(tm, 1, mule)
    in_pack = p.wait(lambda: tm not in p.items or p.items[tm].get("container") != p.backpack(), 4)
    s2 = give(a, p, "MuleModule SurveySaddleRoll 2")
    fitted2, _ = fit(p, mule, s2, "Survey Saddle Roll II")
    g = options(p, mule)
    texts = all_text(p, g)
    track = [b for b in buttons(g) if TRACK_BASE <= b < 300]
    heading = any(t.startswith("Track map") for t in texts)
    na = len(p.arrows)
    arrow = None
    if track:
        p.reply_gump(g, track[0])
        arrow = p.wait(lambda: next((x for x in p.arrows[na:] if x["active"]), None), 5)
    else:
        p.reply_gump(g, 0)
    points = bool(arrow) and (arrow["x"], arrow["y"]) == (cx, cy)
    # III at the spot.
    remove_module(p, mule, "Survey Saddle Roll II")
    s3 = give(a, p, "MuleModule SurveySaddleRoll 3")
    fitted3, _ = fit(p, mule, s3, "Survey Saddle Roll III")
    a.command("[go %d %d" % (cx, cy), 1.5)
    z = a.pos[2] if a.pos else oz
    guardians, chest, dig_row, refusal = [], None, False, None
    set_on(a, p.serial, "Blessed true")
    try:
        teleport(a, p.serial, cx, cy, z)
        teleport(a, mule, cx + 1, cy, z)
        there = p.wait(lambda: dist({"x": p.pos[0], "y": p.pos[1]}, (cx, cy)) <= 1, 5)
        p.sleep(1.0)
        g = options(p, mule)
        track = [b for b in buttons(g) if TRACK_BASE <= b < 300]
        if track:
            p.reply_gump(g, track[0])
            g = options(p, mule)
        dig_row = BTN_DIG in buttons(g) and any(t.startswith("Dig here") for t in all_text(p, g))
        before_m, before_i = set(p.mobiles), set(p.items)
        n = len(p.messages)
        p.reply_gump(g, BTN_DIG)
        chest = p.wait(lambda: next((s for s, it in p.items.items() if s not in before_i and it.get("world") and dist(it, (cx, cy)) <= 3), None), 8)
        guardians = p.wait(lambda: [s for s, m in p.mobiles.items() if s not in before_m and s not in (p.serial, mule) and dist(m, (cx, cy)) <= 10] or None, 6) or []
        refusal = wait_text(p, r"Track a decoded treasure map first", n, 1)
    finally:
        for gs in guardians:
            a.command("[kill", 0.2)
            if a.wait_target(5):
                a.target_obj(gs)
        set_on(a, p.serial, "Blessed false")
        teleport(a, p.serial, ox, oy, oz)
        teleport(a, mule, ox + 1, oy, oz)
    ok = bool(in_pack) and fitted2 and heading and points and fitted3 and bool(there) and dig_row and bool(chest) and bool(guardians) and not refusal
    return ok, "map on %s at (%d, %d), in the mule's pack %s; II fitted %s: 'Track map' heading %s, %d map rows, arrow %s -> points at the spot %s; " \
               "III fitted %s, owner at the spot %s, 'Dig here' row %s: chest %s, %d guardians %s, refusal %r" % (
                   pmap, cx, cy, bool(in_pack), fitted2, heading, len(track), arrow, points, fitted3, bool(there), dig_row,
                   "0x%X (graphic 0x%X)" % (chest, p.items.get(chest, {}).get("graphic", 0)) if chest else None, len(guardians),
                   [p.mobiles.get(gs, {}).get("body") for gs in guardians], refusal)


# -- P70-11: the Trader's Ledger's bank -------------------------------------------------------------------------------------------

@check("P70-11", "Trader's Ledger III: 'Open your bank box' opens it in town, and is refused while criminal, in a fight, and in a dungeon")
def p70_11(ctx):
    # Options "Open your bank box" = button 14 at ledger tier 3 (MuleModuleGumps.cs:150-154, :203-213) -> RefuseBank (MuleModules.cs
    # :932-963): "The ledger will not open the bank for a criminal." (:951), "The ledger will not open the bank in the middle of a
    # fight." (:959: a combatant, or any aggression in the last 30 s), "The ledger will not open the bank in a dungeon." (:946,
    # DungeonRegion), else BankBox.Open (pinned Server/Items/Containers.cs:29-51: DisplayTo the owner, a 0x24 for the bank box).
    # Criminal is a GameMaster property (pinned Mobile.cs:1679-1680); the dungeon is Despise's go spot, on both facets.
    a, p = ctx.ensure_admin(), ctx.ensure_player()
    mule, _ = mule_for(ctx, a, p, 3)
    s = give(a, p, "MuleModule TradersLedger 3")
    fitted, _ = fit(p, mule, s, "Trader's Ledger III")
    bp, pack = p.backpack(), mule_pack(p, mule)

    def press_bank():
        g = options(p, mule)
        if BTN_BANK not in buttons(g):
            p.reply_gump(g, 0)
            return None, ["no 'Open your bank box' button: %s" % all_text(p, g)]
        n, nc = len(p.messages), len(p.containers)
        p.reply_gump(g, BTN_BANK)
        p.sleep(1.5)
        opened = [c for c in p.containers[nc:] if c not in (bp, pack)]
        return opened, p.texts_since(n)

    opened, t1 = press_bank()
    if opened is not None and not opened and any("middle of a fight" in t for t in t1):
        p.sleep(31)                     # an earlier check's fight is still inside RefuseBank's 30 s
        opened, t1 = press_bank()
    bank = next((x for x, it in p.items.items() if it.get("parent") == p.serial and it.get("layer") == 0x1D), None)
    town = bool(opened) and (bank is None or bank in opened)
    set_on(a, p.serial, "Criminal true", p)
    _, t2 = press_bank()
    set_on(a, p.serial, "Criminal false", p)
    criminal = any("The ledger will not open the bank for a criminal." in t for t in t2)
    rat = spawn(a, p, "Rat", 238)
    if rat:
        set_on(a, rat, "CantWalk true Hits 1", p)
        p.send(P.war_mode(True))
        p.sleep(0.3)
        p.send(P.attack(rat))
        p.sleep(1.5)
    _, t3 = press_bank()
    p.send(P.war_mode(False))
    fight = any("The ledger will not open the bank in the middle of a fight." in t for t in t3)
    if rat and rat in p.mobiles:
        a.command("[kill", 0.2)
        if a.wait_target(5):
            a.target_obj(rat)
    ox, oy, oz = p.pos[:3]
    try:
        teleport(a, p.serial, *DESPISE)
        teleport(a, mule, DESPISE[0] + 1, DESPISE[1], DESPISE[2])
        there = p.wait(lambda: dist({"x": p.pos[0], "y": p.pos[1]}, DESPISE[:2]) <= 1, 5)
        p.sleep(1.0)
        _, t4 = press_bank()
    finally:
        teleport(a, p.serial, ox, oy, oz)
        teleport(a, mule, ox + 1, oy, oz)
    dungeon = any("The ledger will not open the bank in a dungeon." in t for t in t4)
    ok = fitted and town and criminal and fight and bool(there) and dungeon
    return ok, "fitted %s; town: bank box 0x%X opened %s (%s); criminal: %s %s; fight (rat %s): %s %s; Despise %s: %s %s" % (
        fitted, bank or 0, town, t1[-1:], criminal, t2[-1:], "attacked" if rat else "NOT spawned", fight, t3[-1:], bool(there), dungeon, t4[-1:])
