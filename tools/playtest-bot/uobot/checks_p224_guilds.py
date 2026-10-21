"""cc-P224, group "guilds": the Society of Smiths' teaching orders (P170-1) and the Custodians' clean-up points (P175-1,
P175-2, P175-3). Written 2026-10-20 (Silver Golem's brief, HARNESS.md); nothing here was run against a shard.

Every asserted string is read from the code under test and cited at the check. The two guildmasters stand in New Haven
(GuildLocations, ClusterFGuildStarterPath.cs:213 and :221); the bot moves the player to the post, looks for the NPC,
and only when none stands within reach does the admin [add one beside the player (the evidence says which).

Both long-lived characters are reused; joining a guild is one-way, so the first run joins and later runs find a member.
"""
import re

from .checks import check, all_text, buttons, entries, near, give, staff_target, try_equip, _wait_kill
from .checks_p224 import set_skill_to, spawn, add_item_here, set_on, kill, corpse_near, wait_text
from . import packets as P


# -- gump layout helpers -------------------------------------------------------------------------------------------------

def cmds(g):
    """The layout's commands in order: (kind, args)."""
    return [(m.group(1), m.group(2).split()) for m in re.finditer(r"\{\s*([a-z]+)\s+([^}]*)\}", g["layout"])]


def clilocs(g):
    """The cliloc numbers a layout names (xmfhtmlgump x y w h NUM ..., xmfhtmlgumpcolor, xmfhtmltok, tooltip NUM), in order."""
    out = []
    for kind, a in cmds(g):
        try:
            if kind in ("xmfhtmlgump", "xmfhtmlgumpcolor") and len(a) >= 5:
                out.append(int(a[4]))
            elif kind == "xmfhtmltok":
                out.append(next(int(x) for x in a if x.lstrip("-").isdigit() and int(x) >= 500000))
            elif kind == "tooltip" and a:
                out.append(int(a[0]))
        except (ValueError, StopIteration):
            pass
    return out


def tilepics(g):
    """(x, y, graphic, hue) of each tilepic / tilepichue in the layout."""
    out = []
    for kind, a in cmds(g):
        if kind in ("tilepic", "tilepichue") and len(a) >= 3:
            try:
                out.append((int(a[0]), int(a[1]), int(a[2]), int(a[3]) if kind == "tilepichue" and len(a) >= 4 else 0))
            except ValueError:
                pass
    return out


def button_art(g, bid):
    """The up-art of button `bid` ('button x y up down type page id'), or None."""
    for kind, a in cmds(g):
        if kind == "button" and len(a) >= 7 and a[6].lstrip("-").isdigit() and int(a[6]) == bid:
            return int(a[2])
    return None


def label_at(g, x, y):
    return next((e[3] for e in entries(g) if e[0] == "text" and e[1] == x and e[2] == y), None)


def msgs_since(c, n):
    return list(c.messages[n:])


# -- the guildmasters and the guild hall page ----------------------------------------------------------------------------

# GuildLocations.All (ClusterFGuildStarterPath.cs:213, :221): the post, the type, Trammel. Neither row is Seeded: the smith
# guildmaster is pinned's spawned stock vendor (it wanders from its tile), the Warden is placed by ClusterFInstitutionSeeder
# (:69-76). The bot never runs a seeder: it looks, then [add]s.
POSTS = {
    "smithing": ("BlacksmithGuildmaster", (3469, 2536, 41), "Society of Smiths"),
    "custodians": ("SanitationWarden", (3506, 2560, 21), "The Custodians"),
}

TALK = 3006146                 # GuildMembershipEntry (ClusterFGuildSystem.cs:785-786), added to every guildmaster with a def
                            # (ClusterFGuildmasterExtension.cs:37); OnClick -> OpenGuildHall -> GuildTaskDetailGump (:532-538)
HALL_JOIN = 1               # GuildTaskDetailGump.BtnJoin (:1040), "Join the guild" (:1092-1093), drawn at the guildmaster
HALL_SERVICES = 5           # BtnServices (:1044), "Guild services" (:1177-1178) -> OpenMemberServices (:1226-1228, members)


def move_player(a, p, x, y, z):
    """The admin [set X Y Z on the player (Mobile.X/Y/Z are GameMaster command properties, pinned Server/Mobiles/
    Mobile.cs:2647-2648); a player cannot [go. Returns True when the player's position followed."""
    set_on(a, p.serial, "X %d Y %d Z %d" % (x, y, z), p)
    return bool(p.wait(lambda: p.pos and max(abs(p.pos[0] - x), abs(p.pos[1] - y)) <= 1, 6))


def hall_page(c, npc, timeout=8, a=None):
    """Talk on a guildmaster: the guild hall page (GuildTaskDetailGump), or None when the menu has no Talk or no page came.
    A Talk entry flagged Disabled (0x01) is out of range: with the admin given, the player is moved beside the NPC first."""
    n = len(c.gumps)
    m = c.context_menu(npc, 4)
    if not m or not any(e["cliloc"] == TALK for e in m["entries"]):
        return None
    e = next(e for e in m["entries"] if e["cliloc"] == TALK)
    if e["flags"] & 1 and a is not None and npc in c.mobiles:
        mob = c.mobiles[npc]
        move_player(a, c, mob["x"] + 1, mob["y"], mob.get("z", c.pos[2]))
        c.sleep(0.8)
        n = len(c.gumps)
        m = c.context_menu(npc, 4)
        if not m:
            return None
    c.pick_context(m, TALK)
    return c.wait_gump(None, timeout, since=n)


def find_guildmaster(a, p, key):
    """Moves the player to the guild's post and finds its guildmaster within 10 tiles: the Warden by name ("Sanitation
    Warden", SanitationWarden.cs:45), the smith guildmaster by its Talk page's title (def.Name "Society of Smiths",
    ClusterFGuildSystem.cs:106, label :1071). None there: the admin [add]s the type beside the player. Returns (serial, how)."""
    type_name, (x, y, z), title = POSTS[key]
    moved = move_player(a, p, x + 1, y, z)
    near(a, p)
    p.sleep(1.0)
    near_post = [(s, m) for s, m in list(p.mobiles.items())
                 if s not in (p.serial, a.serial) and m.get("x") is not None
                 and max(abs(m["x"] - x), abs(m["y"] - y)) <= 10]
    for s, m in near_post:
        if key == "custodians":
            if p.mobile_name(s, 3) == "Sanitation Warden":
                return s, "found at the post (player moved there: %s)" % moved
            continue
        if max(abs(m["x"] - p.pos[0]), abs(m["y"] - p.pos[1])) > 3:
            move_player(a, p, m["x"] + 1, m["y"], m.get("z", z))
        g = hall_page(p, s, 4)
        if g:
            hit = title in all_text(p, g)
            p.reply_gump(g, 0)
            if hit:
                return s, "found %d tiles from the post" % max(abs(m["x"] - x), abs(m["y"] - y))
    s = spawn(a, p, type_name)
    if not s:
        raise AssertionError("[add %s showed no new mobile" % type_name)
    return s, "none within 10 tiles of the post (%d, %d, %d): the admin [add]ed a %s beside the player" % (x, y, z, type_name)


def ensure_member(p, npc, key, a=None):
    """Joins the player to the guild at its guildmaster through the hall page's Join (ClusterFGuildSystem.cs:1204-1206 ->
    Join :427-445: "You have joined the <Name> as <rank>. Joining is free."). A member's page says "Member. Rank: ..."
    (:1103). Returns a short description of what happened."""
    g = hall_page(p, npc, 8, a)
    if not g:
        raise AssertionError("Talk on the %s guildmaster opened no hall page" % key)
    texts = all_text(p, g)
    if any(t.startswith("Member.") for t in texts):
        p.reply_gump(g, 0)
        return "already a member (%s)" % next(t for t in texts if t.startswith("Member."))
    if HALL_JOIN not in buttons(g) or "Join the guild" not in texts:
        p.reply_gump(g, 0)
        raise AssertionError("the hall page offers no Join: %s" % [t for t in texts if "join" in t.lower()][:3])
    n = len(p.messages)
    p.reply_gump(g, HALL_JOIN)
    said = wait_text(p, r"You have joined the ", n, 8)
    if not said:
        raise AssertionError("Join brought no 'You have joined' message: %s" % p.texts_since(n)[-3:])
    p.sleep(1.0)
    return "joined now: %r" % said


def member_services(p, npc, title, timeout=8):
    """Talk -> hall page -> Guild services: the guild's member gump (OpenMemberServices, ClusterFGuildSystem.cs:549-558:
    smithing -> SmithGuildmasterGump, custodians -> SanitationWardenGump). Returns the gump whose text holds `title`."""
    g = hall_page(p, npc)
    if not g:
        return None
    if HALL_SERVICES not in buttons(g):
        p.reply_gump(g, 0)
        return None
    n = len(p.gumps)
    p.reply_gump(g, HALL_SERVICES)
    return p.wait_gump(lambda x: any(title in t for t in all_text(p, x)), timeout, since=n)


# -- P170-1: Society orders at 144.0 Blacksmithy ------------------------------------------------------------------------

CHOICE_TITLE = "Society of Smiths - Bulk Order"     # SmithBulkOrderChoiceGump.cs:58
BTN_SMALL, BTN_LARGE, BTN_TEACHING = 1, 2, 3        # :20-22; Large is drawn only at 70.1 Blacksmithy (:65-69)
CHECK_OFF, CHECK_ON = 210, 211                      # :27-28: the check box art, 211 when the setting is on (:79)
BULK_ORDER_INFO = 3006152                           # GuildmasterBulkOrderEntry.Cliloc (ClusterFSmithBODSystem.cs:602)
SMITH_GUMP_BULK = 1                                 # SmithGuildmasterGump.BtnBulkOrders (:27), "Bulk Orders ..." (:170-172, :303)

# Pinned SmallBODAcceptGump / LargeBODAcceptGump (UOContent/Engines/Bulk Orders/): 1045133 "A bulk order" / 1045134 "A large
# bulk order"; 1045136 "Item requested:" then the deed's Number; 1045137 "Items requested:" then each entry's Number;
# 1045140 "Special requirements"; 1045141 exceptional; the material line GetMaterialNumberFor: 1045142 + (metal - DullCopper)
# for the stock metals, ShardClilocs.PostValoriteMaterial for ours (Platinum 1900003 .. Celestial 1900010, ShardClilocs.cs:
# 40-61; Blaze = 1900005, :46-47). 1045139 "Do you want to accept this order?"; Ok = button 1, CANCEL = button 0 (the deed
# is deleted). The small gump also draws the item (AddItem(385, 96, _deed.Graphic)).
ACCEPT_Q = 1045139
METALS = {1045142: "DullCopper", 1045143: "ShadowIron", 1045144: "Copper", 1045145: "Bronze", 1045146: "Gold",
          1045147: "Agapite", 1045148: "Verite", 1045149: "Valorite", 1900003: "Platinum", 1900004: "Toxic",
          1900005: "Blaze", 1900006: "Frost", 1900007: "Obsidian", 1900008: "Mythril", 1900009: "Adamantium",
          1900010: "Celestial"}
BLAZE = 1900005

# Pinned's smith deed tables, Distribution/Data/Bulk Orders/Blacksmith/armor.cfg and weapons.cfg (graphic per type); a
# cfg entry's cliloc is 1020000 + graphic (SmallBulkEntry.cs:85-87). The large sets: largering.cfg, largechain.cfg,
# largeplate.cfg; the weapon sets are largeswords/fencing/maces/polearms/axes.cfg.
ARMOR = {0x13BF: "ChainChest", 0x13BE: "ChainLegs", 0x140C: "Bascinet", 0x13BB: "ChainCoif", 0x1408: "CloseHelm",
         0x140A: "Helmet", 0x140E: "NorseHelm", 0x1412: "PlateHelm", 0x1C04: "FemalePlateChest", 0x1410: "PlateArms",
         0x1415: "PlateChest", 0x1414: "PlateGloves", 0x1413: "PlateGorget", 0x1411: "PlateLegs", 0x13EE: "RingmailArms",
         0x13EC: "RingmailChest", 0x13EB: "RingmailGloves", 0x13F0: "RingmailLegs", 0x1B72: "BronzeShield",
         0x1B73: "Buckler", 0x1B76: "HeaterShield", 0x1B74: "MetalKiteShield", 0x1B7B: "MetalShield", 0x1B78: "WoodenKiteShield"}
WEAPONS = {0x0F49: "Axe", 0x0F47: "BattleAxe", 0x0F52: "Dagger", 0x0F4B: "DoubleAxe", 0x0F45: "ExecutionersAxe",
           0x13FB: "LargeBattleAxe", 0x1443: "TwoHandedAxe", 0x13B0: "WarAxe", 0x143D: "HammerPick", 0x0F5C: "Mace",
           0x143B: "Maul", 0x1439: "WarHammer", 0x1407: "WarMace", 0x0F4D: "Bardiche", 0x143E: "Halberd",
           0x1403: "ShortSpear", 0x0F62: "Spear", 0x1405: "WarFork", 0x0F5E: "Broadsword", 0x1441: "Cutlass",
           0x13FF: "Katana", 0x1401: "Kryss", 0x0F61: "Longsword", 0x13B6: "Scimitar", 0x13B9: "VikingSword"}
LARGE_SETS = {"ring": {0x13EB, 0x13EC, 0x13EE, 0x13F0}, "chain": {0x13BB, 0x13BE, 0x13BF},
              "plate": {0x1410, 0x1411, 0x1412, 0x1413, 0x1414, 0x1415}}

_choice_route = {}


def choice_gump(p, gm):
    """The Society's Small/Large choice gump. First the guildmaster's "Bulk Order Info" context entry (ClusterFSmithBODSystem.cs
    :80-90, :608-618; pinned adds the stock entry only at an active vendor, BaseVendor.cs:1335-1339, so it may be missing);
    else Talk -> Guild services -> SmithGuildmasterGump -> Bulk Orders (SmithGuildmasterGump.cs:303)."""
    n = len(p.gumps)
    if _choice_route.get(p.serial) != "services":
        m = p.context_menu(gm, 4)
        if m and any(e["cliloc"] == BULK_ORDER_INFO for e in m["entries"]):
            p.pick_context(m, BULK_ORDER_INFO)
            g = p.wait_gump(lambda x: CHOICE_TITLE in all_text(p, x), 6, since=n)
            if g:
                _choice_route[p.serial] = "context"
                return g
        _choice_route[p.serial] = "services"
    sg = member_services(p, gm, "Society of Smiths")
    if not sg or SMITH_GUMP_BULK not in buttons(sg):
        return None
    n = len(p.gumps)
    p.reply_gump(sg, SMITH_GUMP_BULK)
    return p.wait_gump(lambda x: CHOICE_TITLE in all_text(p, x), 6, since=n)


def teaching_on(g):
    return button_art(g, BTN_TEACHING) == CHECK_ON


def set_teaching(p, gm, on):
    """Makes the "Orders that still teach me" box (ClusterFSmithTeaching.cs:68) read `on`; the press redraws the gump
    (SmithBulkOrderChoiceGump.cs:112-115) and says which way it went (:131-133). Returns (open choice gump, what was said)."""
    g = choice_gump(p, gm)
    if not g:
        raise AssertionError("no Society choice gump (route %s)" % _choice_route.get(p.serial))
    if teaching_on(g) == on:
        return g, "already %s" % ("on" if on else "off")
    n, nm = len(p.gumps), len(p.messages)
    p.reply_gump(g, BTN_TEACHING)
    g2 = p.wait_gump(lambda x: CHOICE_TITLE in all_text(p, x), 6, since=n)
    said = wait_text(p, r"Society orders will ask for", nm, 4)
    if not g2 or teaching_on(g2) != on:
        raise AssertionError("the teaching box did not flip to %s (said %r)" % (on, said))
    return g2, said


def ask_order(p, gm, large, g=None):
    """Asks for one order from an open choice gump (or opens one), reads the accept gump and cancels it. Returns a dict:
    kind ("armor"/"weapon"/"?"), metal (name), items (names), large (bool); or {"err": ...}."""
    g = g or choice_gump(p, gm)
    if not g:
        return {"err": "no choice gump"}
    btn = BTN_LARGE if large else BTN_SMALL
    if btn not in buttons(g):
        p.reply_gump(g, 0)
        return {"err": "no %s button: %s" % ("Large" if large else "Small", [t for t in all_text(p, g) if "bulk order" in t.lower()])}
    n, nm = len(p.gumps), len(p.messages)
    p.reply_gump(g, btn)
    acc = p.wait_gump(lambda x: ACCEPT_Q in clilocs(x), 8, since=n)
    if not acc:
        return {"err": "no accept gump; said %s" % p.texts_since(nm)[-2:]}
    cl = clilocs(acc)
    metal = next((c for c in cl if c in METALS), None)
    if 1045137 in cl:
        i = cl.index(1045137) + 1
        nums = []
        while i < len(cl) and cl[i] not in (1045140, ACCEPT_Q):
            nums.append(cl[i])
            i += 1
        graphics = [c - 1020000 for c in nums if 1020000 <= c < 1024000]
    else:
        graphics = [t[2] for t in tilepics(acc) if t[0] == 385]
        if not graphics and 1045136 in cl and cl.index(1045136) + 1 < len(cl):
            c = cl[cl.index(1045136) + 1]
            graphics = [c - 1020000] if 1020000 <= c < 1024000 else []
    p.reply_gump(acc, 0)                      # CANCEL: the deed is deleted, nothing reaches the pack
    kinds = {"armor" if gr in ARMOR else "weapon" if gr in WEAPONS else "?" for gr in graphics}
    return {"large": 1045134 in cl, "metal": METALS.get(metal, "Iron" if metal is None else "#%d" % metal),
            "metal_num": metal, "graphics": graphics,
            "items": [ARMOR.get(gr) or WEAPONS.get(gr) or "0x%X" % gr for gr in graphics],
            "kind": "armor" if kinds == {"armor"} else "weapon" if "weapon" in kinds else "?"}


def fmt(offers):
    return [o.get("err") or "%s/%s" % ("+".join(o["items"]) if o["items"] else "?", o["metal"]) for o in offers]


def all_blaze_armor(offers):
    return bool(offers) and all(not o.get("err") and o["kind"] == "armor" and o["metal_num"] == BLAZE for o in offers)


def large_set(o):
    s = set(o.get("graphics") or ())
    return next((name for name, want in LARGE_SETS.items() if s == want), None)


@check("P170-1", "Society orders at 144.0 Blacksmithy: teaching on gives Blaze armor only (large: a Blaze set; hammer too); off is mixed")
def p170_1(ctx):
    # The setting on: every Society offer teaches in its own metal (ClusterFSmithTeaching.cs OrderMetal :249-269 and the
    # patched SmallSmithBOD / LargeSmithBOD, cc-P170 notes); at Base 144 the metal that teaches is Blaze (137.5 to 150,
    # ClusterFMetalTiers.cs:22, :74) and weapon orders carry no metal, so small offers are armor and large ones the ring,
    # chain or plate set. Off: pinned's stock pick (ClusterFSmithBODSystem.cs:158-159). No cooldown (:92-93), and the
    # 3-small / 1-large cap counts only deeds in the pack (:579-585), so cancelled offers never block the next ask.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    gm, how = find_guildmaster(a, p, "smithing")
    joined = ensure_member(p, gm, "smithing", a)
    if not set_skill_to(a, p, "Blacksmith", 144.0):
        raise AssertionError("Blacksmithy did not reach 144.0: %s" % p.skills.get(7))
    g, said_on = set_teaching(p, gm, True)
    on_small = [ask_order(p, gm, False, g)] + [ask_order(p, gm, False) for _ in range(9)]
    on_large = [ask_order(p, gm, True) for _ in range(3)]
    # The Hammer of Hephaestus: +5 Blacksmithy while worn (HammerOfHephaestus.cs:37, :285, :431), Value 149, Base still 144.
    hammer = give(a, p, "HammerOfHephaestus")
    worn, htexts, _ = try_equip(p, hammer)
    on_hammer = [ask_order(p, gm, False) for _ in range(3)]
    if worn:
        p.lift_drop(hammer, 1, p.backpack())
        p.sleep(0.6)
    g, said_off = set_teaching(p, gm, False)
    off_small = [ask_order(p, gm, False, g)] + [ask_order(p, gm, False) for _ in range(9)]
    set_teaching(p, gm, True)                 # back on, the default (ClusterFSmithTeaching.cs:54), for the next run
    sets = [large_set(o) for o in on_large]
    ok_small = all_blaze_armor(on_small)
    ok_large = bool(on_large) and all(not o.get("err") and o["large"] and o["metal_num"] == BLAZE for o in on_large)
    # The hammer's equip did not happen in the second trial (no message either way): when it is not worn, the three asks
    # are still made and judged as plain Blaze-armor offers; the evidence says the hammer was not worn.
    ok_hammer = all_blaze_armor(on_hammer)
    off_ok = [o for o in off_small if not o.get("err")]
    ok_off = bool(off_ok) and any(o["kind"] != "armor" or o["metal_num"] != BLAZE for o in off_ok)
    ok = ok_small and ok_large and ok_hammer and ok_off
    return ok, ("guildmaster %s; %s; route %s; on (%s): 10 small %s; 3 large %s (sets %s); hammer worn %s%s: 3 small %s; "
                "off (%s): 10 small %s") % (
        how, joined, _choice_route.get(p.serial), said_on, fmt(on_small), fmt(on_large), sets, worn,
        "" if worn else " (%s)" % htexts[-2:], fmt(on_hammer), said_off, fmt(off_small))


# -- P175-1: Appraise for Cleanup, a plain and a magic longsword ---------------------------------------------------------

APPRAISE_ENTRY = 1151298    # "Appraise for Cleanup", pinned CleanUpTrash.cs:148 (AppraiseforCleanup), on a TrashBarrel
                            # (pinned Items/Misc/TrashBarrel.cs:31); the Custodian TrashBag has only its Dump entry
                            # (TrashBag.cs:95-101), so the barrel is what the bot right-clicks.
# AppraiseforCleanupTarget.Appraise (CleanUpBritanniaData.cs:969-988): 1151271 no value, 1151272 less than one, 1151273
# "approximately one point", 1151274 "approximately ~1_VALUE~ points"; the cursor re-arms after each item (:1008).
APPRAISE_CLILOCS = {1151271: 0.0, 1151272: 0.5, 1151273: 1.0}


def appraise_value(m):
    if not m:
        return None
    if m.get("cliloc") in APPRAISE_CLILOCS:
        return APPRAISE_CLILOCS[m["cliloc"]]
    hit = re.search(r"approximately ([\d.,]+) points", m["text"])
    return float(hit.group(1).replace(",", "")) if hit else None


@check("P175-1", "Appraise for Cleanup: a plain longsword about 1 point, one with Hit Lightning 50 and Swing Speed 30 about 6")
def p175_1(ctx):
    # cc-P175 Part A: a loot item pays its material value plus its imbue weight / 30 (CleanUpBritanniaData.cs:293-299);
    # the notes measured the plain longsword at 1 and the magic one at 6 (weight 150). [set takes a dotted path
    # (pinned Properties.cs:113-150, GetPropertyInfoChain splits on '.').
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    barrel = add_item_here(a, p, "TrashBarrel")
    if not barrel:
        raise AssertionError("no trash barrel appeared beside the player")
    plain = give(a, p, "Longsword")
    magic = give(a, p, "Longsword")
    set_on(a, magic, "WeaponAttributes.HitLightning 50 Attributes.WeaponSpeed 30", p)
    props = p.opl(magic) or []
    m = p.context_menu(barrel)
    if not m:
        raise AssertionError("the trash barrel gave no context menu")
    p.pick_context(m, APPRAISE_ENTRY)
    if not p.wait_target(6):
        raise AssertionError("Appraise for Cleanup gave no target cursor")
    n = len(p.messages)
    p.target_obj(plain)
    m1 = p.wait(lambda: next((x for x in msgs_since(p, n) if x.get("cliloc") in (1151271, 1151272, 1151273, 1151274)), None), 6)
    if not p.wait_target(5):
        raise AssertionError("the appraisal cursor did not re-arm")
    n2 = len(p.messages)
    p.target_obj(magic)
    m2 = p.wait(lambda: next((x for x in msgs_since(p, n2) if x.get("cliloc") in (1151271, 1151272, 1151273, 1151274)), None), 6)
    if p.wait_target(3):
        p.cancel_target()
    v1, v2 = appraise_value(m1), appraise_value(m2)
    ok = v1 == 1.0 and v2 is not None and 5.0 <= v2 <= 7.0
    return ok, "plain: %r (%s); magic: %r (%s); the magic sword's tooltip: %s" % (
        m1["text"] if m1 else None, v1, m2["text"] if m2 else None, v2,
        [t for t in props if "lightning" in t.lower() or "swing" in t.lower()])


# -- P175-2: [cleanup on an orc's and a dragon's corpse ---------------------------------------------------------------

# ClusterFCustodianSystem.CleanupTarget (:290-335): "Target an item to clean up." (:78); a corpse pays
# ClusterFCorpseValue.BaseFor (Fame x 0.001 for the Custodian who held looting rights, ClusterFCorpseValue.cs:88-115)
# plus each item inside at its Clean Up value (:163-173); the message "Corpse cleared (N items inside). +X Clean Up
# points." or "Empty corpse cleared. +X Clean Up points." (:319-322), X in FormatPoints "#,0.##" (:286). Fame: pinned
# Orc.cs:37 1500 (body 17, :14), Dragon.cs:36 15000 (body 12 or 59, :11). A kill by the admin's [kill leaves no damage
# entry, so no one earned it and the base is 0 (ClusterFCorpseValue.cs:58-66).
POINTS_RX = re.compile(r"\+([\d,]+(?:\.\d+)?) Clean Up points")


def empty_corpse(p, corpse, limit=40):
    """Opens the corpse and lifts everything into the player's pack, so the corpse pays its base alone. Returns (lifted,
    left)."""
    p.dclick(corpse)
    p.wait(lambda: p.contents(corpse), 4)
    lifted = 0
    for s, it in list(p.contents(corpse).items())[:limit]:
        for _ in range(3):   # the item-use delay refuses back-to-back moves: pause and retry
            p.lift_drop(s, it.get("amount") or 1, p.backpack())
            if p.wait(lambda: p.items.get(s, {}).get("container") != corpse, 3):
                lifted += 1
                break
            p.sleep(1.2)
        p.sleep(0.9)
    p.sleep(0.6)
    return lifted, len(p.contents(corpse))


def cleanup(p, corpse):
    """`[cleanup`, target the corpse; returns (points or None, the message)."""
    n = len(p.messages)
    p.command("[cleanup", 0.3)
    if not p.wait_target(6):
        return None, "no target cursor: %s" % p.texts_since(n)[-2:]
    p.target_obj(corpse)
    m = p.find_text(r"corpse cleared|cannot be cleaned up|not an item|must be a member", since=n, timeout=6)
    text = m["text"] if m else None
    hit = POINTS_RX.search(text or "")
    return (float(hit.group(1).replace(",", "")) if hit else None), text


def hunt(a, p, type_name, bodies, fair):
    """Spawns the creature beside the player and kills it: a fair kill (pinned at 1 hit and CantWalk, the player's bare
    hands, checks._wait_kill topping the player up) or the admin's [kill. Returns (corpse serial or None, note)."""
    # Open ground beside the square (the Warden's post is on a building at z 21: the second trial's orc could not be
    # reached there), and the creature's damage pinned to 1 as well as its hits, so a dragon cannot kill the Custodian.
    move_player(a, p, 3512, 2581, 14)
    staff_target(a, "[set Hits 175 Stam 100", p)
    before = set(p.items)
    mob = spawn(a, p, type_name, bodies)
    if not mob:
        return None, "%s never appeared" % type_name
    body = p.mobiles[mob].get("body")
    if fair:
        set_on(a, mob, "CantWalk true Hits 1 DamageMin 1 DamageMax 1", p)
        p.sleep(0.4)
        p.send(P.war_mode(True))
        p.sleep(0.3)
        p.send(P.attack(mob))
        outcome = _wait_kill(a, p, mob)
        p.send(P.war_mode(False))
    else:
        outcome = "killed" if kill(a, p, mob) else "timed out"
    if outcome != "killed":
        return None, "%s: %s" % (type_name, outcome)
    corpse = corpse_near(p, before, body)
    return corpse, "%s (body %s) %s" % (type_name, body, "by the player" if fair else "by the admin's [kill")


@check("P175-2", "[cleanup: a Custodian's own orc corpse pays 1.5 + contents, a dragon 15 + contents; one the admin killed pays contents only")
def p175_2(ctx):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    warden, how = find_guildmaster(a, p, "custodians")
    joined = ensure_member(p, warden, "custodians", a)
    for sk in ("Wrestling", "Tactics"):
        set_skill_to(a, p, sk, 100)
    staff_target(a, "[set RawStr 250 RawDex 100 Hits 175", p)     # as checks._p119_fight (:680-683)
    legs = []
    for type_name, bodies, fair, base in (("Orc", 17, True, 1.5), ("Dragon", (12, 59), True, 15.0), ("Orc", 17, False, 0.0)):
        corpse, note = hunt(a, p, type_name, bodies, fair)
        if not corpse:
            legs.append((False, "%s: no corpse (%s)" % (type_name, note)))
            continue
        lifted, left = empty_corpse(p, corpse)
        pts, text = cleanup(p, corpse)
        if pts is None:
            legs.append((False, "%s: %s; [cleanup said %r" % (note, "emptied" if not left else "%d left inside" % left, text)))
            continue
        if left == 0:
            good = abs(pts - base) < 0.005
        elif fair:
            good = pts >= base
        else:
            good = False      # its contents' value is unknown, so a 0 base cannot be told apart from them
        legs.append((good, "%s: lifted %d out, %d left; %r -> %s (expected %s%s)" % (
            note, lifted, left, text, pts, base, "" if left == 0 else " plus what is inside" if fair else ", contents unknown: not judged")))
        p.sleep(0.5)
    ok = all(g for g, _ in legs)
    return ok, "warden %s; %s; " % (how, joined) + "; ".join(e for _, e in legs)


# -- P175-3: a trash bag full of runic hammers, the store, the pigment --------------------------------------------------

WARDEN_STORE, WARDEN_BAG = 3, 5                 # SanitationWardenGump.cs:32-33; "Clean Up Britannia store" (:113-114),
                                                # "Trash bag (free)" (:116-117), member view
BAG_GRAPHIC, BAG_HUE = 0xE75, 0x48D             # TrashBag.cs:35-36
DUMP_ENTRY = 1151316                            # TrashBag.DumpEntry.Cliloc (:121) -> TrashBagGump; Dump Now = button 1 (:143)
STORE_TITLE = 1151316                           # CleanUpBritanniaRewardGump (CleanUpBritanniaRewards.cs:331)
CONFIRM_BTN, CONFIRM_RADIO = 2, 1               # pinned BaseConfirmGump.cs:38-44, :55-63, enum :76-81 (Close, Break, Confirm)
# Store rows (CleanUpBritanniaRewards.cs): (type, graphic, tooltip cliloc, hue, price). :142 Bamboo, :191 the first pigment
# (IntenseTealPigment; every pigment is 0xEFF at 250,000, :191-214), :215 LuckyCharm. A row's buy button is 200 + its index
# (:355), drawn only when affordable; the item at x 60 (:359), the price label at x 250 as "N0" (:371).
WANT = {"Bamboo": (0x246D, 1029324, 15000), "pigment": (0xEFF, None, 250000), "Lucky Charm": (0x2F5B, 1154739, 300000)}


def store_rows(g):
    """The store's rows across its pages: dicts with page, button (None when unaffordable), graphic, hue, tooltip, price."""
    rows, page, pending = [], 0, None
    for kind, a in cmds(g):
        try:
            if kind == "page" and a:
                page = int(a[0])
            elif kind == "button" and len(a) >= 7 and int(a[6]) >= 200:
                pending = int(a[6])
            elif kind in ("tilepic", "tilepichue") and len(a) >= 3 and int(a[0]) == 60:
                rows.append({"page": page, "button": pending, "graphic": int(a[2]),
                             "hue": int(a[3]) if kind == "tilepichue" and len(a) >= 4 else 0, "tooltip": None, "price": None})
                pending = None
            elif kind == "tooltip" and a and rows and rows[-1]["tooltip"] is None:
                rows[-1]["tooltip"] = int(a[0])
            elif kind == "text" and len(a) >= 4 and int(a[0]) == 250 and rows and rows[-1]["price"] is None:
                rows[-1]["price"] = g["lines"][int(a[3])]
        except (ValueError, IndexError):
            pass
    return rows


def open_store(p, warden):
    """Talk -> hall page -> Guild services -> SanitationWardenGump -> the Clean Up Britannia store (needs the Warden within
    5 tiles, SanitationWardenGump.cs:223-244)."""
    wg = member_services(p, warden, "The Custodians")
    if not wg or WARDEN_STORE not in buttons(wg):
        return None
    n = len(p.gumps)
    p.reply_gump(wg, WARDEN_STORE)
    return p.wait_gump(lambda x: STORE_TITLE in clilocs(x), 8, since=n)


def buy(p, warden, row):
    """Presses the row's button, confirms (ConfirmCleanUpRewardGump, :420-441), and returns the new pack item's serial, or
    None with the messages."""
    pack = p.backpack()
    before = set(p.items)
    g = open_store(p, warden)
    if not g:
        return None, "the store did not open"
    live = next((r for r in store_rows(g) if r["graphic"] == row["graphic"] and r["tooltip"] == row["tooltip"] and r["price"] == row["price"]), None)
    if not live or live["button"] is None:
        return None, "no buy button for that row now (points %s)" % label_at(g, 230, 65)
    n, nm = len(p.gumps), len(p.messages)
    p.reply_gump(g, live["button"])
    c = p.wait_gump(lambda x: 1074975 in clilocs(x), 8, since=n)
    if not c:
        return None, "no confirm gump: %s" % p.texts_since(nm)[-2:]
    p.reply_gump(c, CONFIRM_BTN, switches=(CONFIRM_RADIO,))
    said = wait_text(p, r"placed in your backpack|enough points|could not be given", nm, 8)
    item = p.wait(lambda: next((s for s, it in p.items.items() if s not in before and it.get("container") == pack
                                and it.get("graphic") == row["graphic"]), None), 6)
    return item, said


@check("P175-3", "Clean Up store: two Valorite runic hammers dumped (640,000), Bamboo, a pigment and the Lucky Charm bought, the pigment dyes an artifact and counts down",
       kind="BOT-PARTIAL", left="P175-3: the Bamboo's, pigment's and Lucky Charm's art in the pack, and the Helm of Insight's new hue on screen")
def p175_3(ctx):
    # A Valorite runic hammer pays 8,000 a use (CleanUpBritanniaData.cs:138-150): the notes' one hammer of 40 uses is
    # 320,000, short of the three rewards (565,000), so the bot dumps two. [add RunicHammer Valorite 40 is the
    # (CraftResource, int uses) constructor (pinned RunicHammer.cs:18; Add.cs:251-258 parses an enum by name, Types.cs:17-21).
    # The dye target: HelmOfInsight (pinned DemonKnight.ArtifactRarity11, DemonKnight.cs:67-81; a PlateHelm, hue 0x554),
    # accepted by BasePigmentsOfTokuno.IsValidItem (:188-226), which CompassionPigment's target uses (CleanUpPigments.cs:174-181).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    warden, how = find_guildmaster(a, p, "custodians")
    joined = ensure_member(p, warden, "custodians", a)
    pack = p.backpack()
    # A fresh bag each run: "Trash bag (free)" replaces the old one (SanitationWardenGump.cs:246-265, TrashBag.IssueTo :76-91).
    wg = member_services(p, warden, "The Custodians")
    if not wg or WARDEN_BAG not in buttons(wg):
        raise AssertionError("no Sanitation Warden member gump with the trash bag button")
    before = set(p.items)
    nm = len(p.messages)
    p.reply_gump(wg, WARDEN_BAG)
    bag_said = wait_text(p, r"You receive a (new )?trash bag", nm, 6)
    bag = p.wait(lambda: next((s for s, it in p.items.items() if s not in before and it.get("container") == pack
                               and it.get("graphic") == BAG_GRAPHIC), None), 6)
    if not bag:
        raise AssertionError("no trash bag arrived: %r" % bag_said)
    hammers = [give(a, p, "RunicHammer Valorite 40") for _ in range(2)]
    for h in hammers:
        p.lift_drop(h, 1, bag)
        p.wait(lambda: p.items.get(h, {}).get("container") == bag, 4)
    m = p.context_menu(bag)
    if not m:
        raise AssertionError("the trash bag gave no context menu")
    n = len(p.gumps)
    p.pick_context(m, DUMP_ENTRY)
    bg = p.wait_gump(lambda x: any(t == "Trash Bag" for t in all_text(p, x)), 8, since=n)
    if not bg:
        raise AssertionError("no Trash Bag gump")
    dump_label = next((t for t in all_text(p, bg) if t.startswith("Dump Now")), None)
    nm = len(p.messages)
    p.reply_gump(bg, 1)
    dumped = wait_text(p, r"^Dumped \d+ items?: ", nm, 8)
    # The store: all pages come in one layout; the bot reads every row.
    g = open_store(p, warden)
    if not g:
        return False, "dumped %r (%r); the store did not open (%s)" % (dumped, dump_label, p.texts_since(nm)[-2:])
    rows = store_rows(g)
    points_label = label_at(g, 230, 65)
    picks = {}
    for name, (graphic, tooltip, price) in WANT.items():
        picks[name] = next((r for r in rows if r["graphic"] == graphic and (tooltip is None or r["tooltip"] == tooltip)
                            and r["price"] == "{:,}".format(price)), None)
    p.reply_gump(g, 0)
    bought, notes = {}, []
    for name in ("Bamboo", "pigment", "Lucky Charm"):
        row = picks[name]
        if not row:
            notes.append("%s: no such row" % name)
            continue
        item, said = buy(p, warden, row)
        bought[name] = item
        notes.append("%s (row page %s, tooltip %s, %s): %s, %r" % (name, row["page"], row["tooltip"], row["price"],
                                                                  "in pack 0x%X hue %s" % (item, p.items[item].get("hue")) if item else "NOT in pack", said))
    pig = bought.get("pigment")
    uses_before = uses_after = None
    hue_after = None
    if pig:
        uses_before = next((t for t in (p.opl(pig) or []) if "uses remaining" in t.lower()), None)      # 1060584, CleanUpPigments.cs:139
        helm = give(a, p, "HelmOfInsight")
        nm = len(p.messages)
        p.dclick(pig)
        if p.wait_target(6):
            p.target_obj(helm)
            hue_after = p.wait(lambda: p.items.get(helm, {}).get("hue") == picks["pigment"]["hue"] and p.items[helm]["hue"], 6)
            p.sleep(0.5)
            uses_after = next((t for t in (p.opl(pig) or []) if "uses remaining" in t.lower()), None)
        dye_said = p.texts_since(nm)[-2:]
    else:
        dye_said = []
    ok = (bool(dumped) and all(bought.get(k) for k in WANT) and uses_before is not None and uses_before.endswith("5")
          and bool(hue_after) and uses_after is not None and uses_after.endswith("4"))
    return ok, ("warden %s; %s; bag %r; %r -> %r; store: %d rows on %d pages, points label %r; %s; pigment %s before, "
                "helm hue after dye %s (pigment hue %s), %s after; dye messages %s") % (
        how, joined, bag_said, dump_label, dumped, len(rows), max((r["page"] for r in rows), default=0), points_label,
        "; ".join(notes), uses_before, hue_after, picks["pigment"]["hue"] if picks.get("pigment") else None, uses_after, dye_said)
