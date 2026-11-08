"""cc-P223: bot checks for the first half of the play-test items that said "Not built yet" (items 1 to 60 of that set in
testing/playtest-steps.json order). Same shape as checks.py: one function per play-test id, setup as admin, the action
as a player unless the test is about staff, an assertion on what the server actually sent back, (passed, evidence).

New primitives this prompt gave the client (client.py, packets.py): use_skill (0x12 0x24), context menus (0xBF 0x13 /
0x14 / 0x15), gump_rows (every text-bearing layout command with its x, y, clilocs resolved) and the craft-gump walker
below (pinned Engines/Craft/Core/CraftGump.cs: GetButtonID(type, index) = 1 + type + index * 7; type 0 show group,
1 create, 2 details, 5 resource, 6 misc; CraftGumpItem.cs: BACK is button 0, MAKE NOW button 1, skills at y 132 + 20i,
materials at y 219 + 20i).
"""
import re
import time

from . import packets as P
from .checks import (check, SHARED, entries, plain, all_text, buttons, near, give, staff_target, set_skill, try_equip,
                     place_forge, MINING)

# pinned Server/Skills/SkillName.cs, in order (0 Alchemy ... 57 Throwing).
SK = {"Alchemy": 0, "Anatomy": 1, "AnimalLore": 2, "ItemID": 3, "ArmsLore": 4, "Parry": 5, "Begging": 6, "Blacksmith": 7,
      "Fletching": 8, "Peacemaking": 9, "Camping": 10, "Carpentry": 11, "Cartography": 12, "Cooking": 13, "DetectHidden": 14,
      "Discordance": 15, "EvalInt": 16, "Healing": 17, "Fishing": 18, "Forensics": 19, "Herding": 20, "Hiding": 21,
      "Provocation": 22, "Inscribe": 23, "Lockpicking": 24, "Magery": 25, "MagicResist": 26, "Tactics": 27, "Snooping": 28,
      "Musicianship": 29, "Poisoning": 30, "Archery": 31, "SpiritSpeak": 32, "Stealing": 33, "Tailoring": 34,
      "AnimalTaming": 35, "TasteID": 36, "Tinkering": 37, "Tracking": 38, "Veterinary": 39, "Swords": 40, "Macing": 41,
      "Fencing": 42, "Wrestling": 43, "Lumberjacking": 44, "Mining": 45, "Meditation": 46, "Stealth": 47, "RemoveTrap": 48,
      "Necromancy": 49, "Focus": 50, "Chivalry": 51, "Bushido": 52, "Ninjitsu": 53, "Spellweaving": 54, "Mysticism": 55,
      "Imbuing": 56, "Throwing": 57}

# Our own clilocs (server/customizations/ShardClilocs.cs): the player package adds the words to the client; the bot
# sees the numbers in the 0xBF 0x14 menu and the OPL.
CL_BREED, CL_SMELT_ORE, CL_METAL_FAMILIARITY = 1900000, 1900001, 1900002
CL_POST_VALORITE_FIRST, CL_POST_VALORITE_LAST = 1900003, 1900010   # "All items must be made with <metal> ingots."


# -- helpers -----------------------------------------------------------------------------------------------------------

def base(c, skill):
    """A skill's base value as the last 0x3A said (checks.py keeps skills by index: value, base, lock, cap)."""
    return (c.skills.get(SK[skill]) or {}).get("base")


def set_skills(a, who, **kv):
    for name, value in kv.items():
        staff_target(a, "[SetSkill %s %s" % (name, value), who)
    who.send(P.status_request(who.serial, 5))
    who.sleep(0.8)


def add_beside(a, who, type_name, dx=1, dy=0, timeout=8):
    """Admin [add <type> on the tile beside the character; returns the new world item's serial as the character
    sees it (0xF3), or None."""
    near(a, who)
    before = set(who.items)
    a.command("[add " + type_name, 0.2)
    if not a.wait_target(8):
        raise AssertionError("[add %s gave no target cursor" % type_name)
    x, y, z = who.pos[:3]
    a.target_xyz(x + dx, y + dy, z)
    return who.wait(lambda: next((s for s, it in who.items.items() if s not in before and it.get("world")), None), timeout)


def add_mobile_beside(a, who, type_name, dx=1, dy=0, timeout=8, name_part=None):
    """Admin [add <creature> beside the character; returns the new mobile's serial as the character sees it. With
    name_part the new mobile's OPL name must contain it: a pet left over from an earlier run (it follows the player)
    can wander into view at the same moment and be taken for the new one (seen with a pack mule and a mimic)."""
    near(a, who)
    before = set(who.mobiles)
    a.command("[add " + type_name, 0.2)
    if not a.wait_target(8):
        raise AssertionError("[add %s gave no target cursor" % type_name)
    x, y, z = who.pos[:3]
    a.target_xyz(x + dx, y + dy, z)
    def found():
        for s in list(who.mobiles):
            if s in before or s == who.serial:
                continue
            if name_part is None or name_part.lower() in name_of(who, s).lower():
                return s
        return None
    return who.wait(found, timeout)


def delete_mobile(a, serial):
    """Admin [Delete on a creature the check made, so it does not follow the player into the next check."""
    if not serial:
        return
    a.command("[Delete", 0.2)
    if a.wait_target(6):
        a.target_obj(serial)
    a.sleep(0.6)


def opl(c, serial, timeout=4):
    """The object property list (0xD6) of a serial: the name first, then each line."""
    c.send(P.opl_request([serial]))
    c.wait(lambda: ((c.items.get(serial) or c.mobiles.get(serial) or {}).get("props")), timeout)
    return (c.items.get(serial) or c.mobiles.get(serial) or {}).get("props") or []


def name_of(c, serial):
    p = opl(c, serial)
    return plain(p[0]) if p else ""


def texts(c, n):
    return c.texts_since(n)


# -- the craft gump ---------------------------------------------------------------------------------------------------

def is_craft_gump(g):
    return "1044014" in g["layout"]          # LAST TEN: CraftGump.CreateGroupList


def is_detail_gump(g):
    return "1044053" in g["layout"] and "1044054" in g["layout"]   # ITEM and <CENTER>SKILLS</CENTER>: CraftGumpItem


def open_craft(c, tool, timeout=10):
    c.sleep(1.2)                 # the action delay: a double-click within a second of the last gump reply is refused (500119)
    n = len(c.gumps)
    c.dclick(tool)
    g = c.wait_gump(is_craft_gump, timeout, since=n)
    if not g:
        raise AssertionError("double-clicking the tool opened no craft gump (%d gumps since: %s)" % (
            len(c.gumps) - n, [all_text(c, x)[:3] for x in c.gumps[n:]]))
    return g


GROUP_PREV, GROUP_NEXT = 70, 77     # server/patches/CraftGump-category-pages.patch: GetButtonID(6, 9), GetButtonID(6, 10)


def craft_groups_page(c, g):
    """[(index, name)] of the groups on this gump's page (buttons at x 15, GetButtonID(0, i) = 1 + 7i; the name 3 px
    lower). The group list is paged server-side (CraftGump-category-pages.patch, "Page N of M" with its own buttons)."""
    rows = c.gump_rows(g)
    out = []
    for y, r in sorted(rows.items()):
        for b, x, pg in zip(r["buttons"], r["x"], r["pages"]):
            if x == 15 and (b - 1) % 7 == 0 and b not in (GROUP_PREV, GROUP_NEXT):
                name = " ".join(c.row_at(rows, y + 3, xmin=40, xmax=210, page=pg)["texts"])
                out.append(((b - 1) // 7, plain(name)))
    return out


def craft_groups(c, g):
    """Every group, over every server-side page of the group list: [(index, name)]. Leaves the gump on its last page
    (any group index can be shown from any page)."""
    seen = {}
    for _ in range(8):           # back to the first page first: the gump may be left on a later one
        if GROUP_PREV not in buttons(g):
            break
        n = len(c.gumps)
        c.reply_gump(g, GROUP_PREV)
        g2 = c.wait_gump(is_craft_gump, 10, since=n)
        if not g2:
            break
        g = g2
    for _ in range(8):
        for i, nm in craft_groups_page(c, g):
            seen.setdefault(i, nm)
        if GROUP_NEXT not in buttons(g):
            break
        n = len(c.gumps)
        c.reply_gump(g, GROUP_NEXT)
        g2 = c.wait_gump(is_craft_gump, 10, since=n)
        if not g2:
            break
        g = g2
    SHARED["p223_last_craft_gump"] = g
    return sorted(seen.items())


def craft_items(c, g):
    """[(index, name)] of the selected group's items (create button at x 220, GetButtonID(1, i) = 2 + 7i)."""
    rows = c.gump_rows(g)
    out = []
    for y, r in sorted(rows.items()):
        for b, x, pg in zip(r["buttons"], r["x"], r["pages"]):
            if x == 220 and (b - 2) % 7 == 0:
                name = " ".join(c.row_at(rows, y + 3, xmin=240, xmax=479, page=pg)["texts"])
                out.append(((b - 2) // 7, plain(name)))
    return out


def newest_craft_gump(c):
    for g in reversed(c.gumps):
        if is_craft_gump(g):
            return g
    return None


def craft_show_group(c, g, index, timeout=10):
    """Show a group. The server sometimes answers a group press with two gumps and then ignores a reply to the older
    one, so a press that brings nothing is tried once more on the newest craft gump it sent."""
    for attempt in range(2):
        n = len(c.gumps)
        c.reply_gump(g, 1 + 7 * index)
        g2 = c.wait_gump(is_craft_gump, timeout if attempt else 6, since=n)
        if g2:
            c.sleep(0.15)
            return newest_craft_gump(c) or g2
        g = newest_craft_gump(c) or g
    raise AssertionError("group %d brought no craft gump" % index)


def craft_details(c, g, index, timeout=10):
    """Open an item's detail page (CraftGumpItem). Returns the gump and a dict: name, make_x (the MAKE X html is
    there), skills [(skill, min)], resources [(name, amount)]."""
    n = len(c.gumps)
    c.reply_gump(g, 3 + 7 * index)
    d = c.wait_gump(is_detail_gump, timeout, since=n)
    if not d:
        raise AssertionError("details of item %d brought no detail gump" % index)
    rows = c.gump_rows(d)
    info = {"name": " ".join(c.row_at(rows, 40, xmin=320)["texts"][-1:]), "make_x": any("MAKE X" in t for t in d["lines"]),
            "skills": [], "resources": []}
    for i in range(6):
        r = c.row_at(rows, 132 + i * 20, xmin=160)
        nums = [t for t in r["texts"] if re.match(r"^\d+(\.\d+)?$", t.strip())]
        names = [t for t in r["texts"] if not re.match(r"^\d+(\.\d+)?$", t.strip())]
        if names and nums:
            info["skills"].append((plain(names[0]), float(nums[0])))
    for i in range(6):
        r = c.row_at(rows, 219 + i * 20, xmin=160)
        nums = [t for t in r["texts"] if re.match(r"^\d+$", t.strip())]
        names = [t for t in r["texts"] if not re.match(r"^\d+$", t.strip()) and t.strip() != "*"]
        if names and nums:
            info["resources"].append((plain(names[0]), int(nums[0])))
    return d, info


def craft_back(c, d, timeout=10):
    n = len(c.gumps)
    c.reply_gump(d, 0)
    return c.wait_gump(is_craft_gump, timeout, since=n)


def craft_make(c, g, index, wait=6.0):
    """Press an item's create button and wait for the result message (1044154 made, 1044043 failed, 1044153 no
    skill ...) and the gump the server sends back. Returns (texts, new gump or None)."""
    n = len(c.messages)
    ng = len(c.gumps)
    c.reply_gump(g, 2 + 7 * index)
    m = c.find_text(r"You create|You failed to create|required skills|don't have the resources|You have worn out|cannot work this|not enough", since=n, timeout=wait)
    g2 = c.wait_gump(is_craft_gump, 4, since=ng)
    return c.texts_since(n), g2


def craft_find(c, g, name_part, exact=False):
    """Walk the groups until an item whose name contains name_part (or equals it, with exact: 'bow' is also in
    'crossbow'); returns (gump showing it, index) or (None, None)."""
    groups = craft_groups(c, g)
    g = SHARED.get("p223_last_craft_gump", g)
    for gi, gname in groups:
        g = craft_show_group(c, g, gi)
        for ii, iname in craft_items(c, g):
            if (iname.lower() == name_part.lower()) if exact else (name_part.lower() in iname.lower()):
                return g, ii
    return None, None


def craft_walk(c, g, details=False):
    """Every group and item: [(group, item name, info or None)]. With details, each item's detail page is read (slow:
    one gump round trip per item)."""
    out = []
    groups = craft_groups(c, g)
    g = SHARED.get("p223_last_craft_gump", g)
    for gi, gname in groups:
        g = craft_show_group(c, g, gi)
        items = craft_items(c, g)
        for ii, iname in items:
            info = None
            if details:
                d, info = craft_details(c, g, ii)
                g = craft_back(c, d) or g
            out.append((gname, iname, info))
    return out, g


def craft_resources(c, g):
    """The resource selection list (misc button 7 = GetButtonID(6, 0)): [(index, name)] from the type-5 buttons."""
    n = len(c.gumps)
    c.reply_gump(g, 7)
    g2 = c.wait_gump(is_craft_gump, 10, since=n)
    if not g2:
        raise AssertionError("resource selection brought no craft gump")
    rows = c.gump_rows(g2)
    out = []
    for y, r in sorted(rows.items()):
        for b, x, pg in zip(r["buttons"], r["x"], r["pages"]):
            if x == 220 and (b - 6) % 7 == 0:
                name = " ".join(c.row_at(rows, y + 3, xmin=240, xmax=479, page=pg)["texts"])
                out.append(((b - 6) // 7, plain(name)))
    return out, g2


def craft_pick_resource(c, g, name_part):
    """Open the resource list and pick the one whose name contains name_part. Returns the craft gump after, or None."""
    lst, g2 = craft_resources(c, g)
    for i, nm in lst:
        if name_part.lower() in nm.lower():
            n = len(c.gumps)
            c.reply_gump(g2, 6 + 7 * i)
            return c.wait_gump(is_craft_gump, 10, since=n), lst
    return None, lst


# Our registrations and the min skill each declares (server/customizations/Engines/Craft/*Registrations.cs). T-004
# asks that the new entries show at sane skill ranges, so the detail page's SKILLS line is compared with these.
OURS_SMITH = {"gargish platemail arms": 66.3, "gargish platemail chest": 75.0, "gargish platemail leggings": 68.8,
              "gargish platemail kilt": 58.9, "gargish amulet": 60.0, "small plate shield": 0.0, "gargish kite shield": 4.6,
              "large plate shield": 24.3, "medium plate shield": 0.0, "gargish chaos shield": 85.0, "gargish order shield": 85.0,
              "gargish katana": 44.1, "gargish kryss": 36.7, "gargish bone harvester": 33.0, "gargish tekagi": 55.0,
              "gargish daisho": 60.0, "gargish dagger": 0.0, "shortblade": 28.0, "gargish battle axe": 30.5, "gargish axe": 34.2,
              "gargish bardiche": 31.7, "gargish war fork": 42.9}
OURS_TAILOR = {"leather talons": 40.4, "gargish cloth arms": 87.1, "gargish cloth chest": 94.0, "gargish cloth leggings": 91.2,
               "gargish cloth kilt": 82.9}
OURS_TINKER = {"gargish necklace": 60.0, "gargish bracelet": 55.0, "gargish ring": 65.0, "gargish earrings": 55.0,
               "gargish cleaver": 20.0, "gargish butcher knife": 25.0, "void orb": 90.0, "brilliant amber bracelet": 75.0,
               "fire ruby bracelet": 75.0, "dark sapphire bracelet": 75.0, "white pearl bracelet": 75.0, "ecru citrine ring": 75.0,
               "blue diamond ring": 75.0, "perfect emerald ring": 75.0, "turquoise ring": 75.0}
OURS_CARPENTER = {"gargish gnarled staff": 78.9, "aud-char": 78.9}

SYSTEMS = (("Blacksmith", "SmithHammer", OURS_SMITH), ("Tailoring", "SewingKit", OURS_TAILOR),
           ("Tinkering", "TinkerTools", OURS_TINKER), ("Carpentry", "Saw", OURS_CARPENTER))


def _craft_walk_all(ctx, details):
    """The admin at 120 in the four skills walks the four systems once per run; the walk is shared by T-004 and T-005."""
    key = "p223_walk_%s" % ("details" if details else "names")
    if key in SHARED:
        return SHARED[key]
    a = ctx.ensure_admin()
    learn_all(a, a)       # the pattern gate (cc-P78) hides entries until their band is learned
    staff_target(a, "[SetSkill Blacksmith 120", a)
    for sk in ("Tailoring", "Tinkering", "Carpentry"):
        staff_target(a, "[SetSkill %s 120" % sk, a)
    walks = {}
    for skill, tool, ours in SYSTEMS:
        s = give(a, a, tool)
        g = open_craft(a, s)
        rows, g = craft_walk(a, g, details=details)
        walks[skill] = rows
        a.reply_gump(g, 0)
        a.sleep(0.5)
    SHARED[key] = walks
    return walks


# ---------------------------------------------------------------------------------------------------------------------
# T-001: MAKE X on every craft gump's item page (server/patches/CraftGumpItem-MakeX.patch: AddHtml(175, 390, ...,
# "<BASEFONT COLOR=#787878>MAKE X") on CraftGumpItem). The bot reads the html line; whether it draws is Chase's.

@check("T-001", "MAKE X is on the tinker and smith craft gumps' item pages", kind="BOT-PARTIAL",
       left="T-001: the MAKE X label draws in the client (the original bug was a drawing one)")
def t_001(ctx):
    a = ctx.ensure_admin()
    found = []
    for tool in ("TinkerTools", "SmithHammer"):
        s = give(a, a, tool)
        g = open_craft(a, s)
        groups = craft_groups(a, g)
        g = SHARED.get("p223_last_craft_gump", g)
        if not groups:
            return False, "%s: the craft gump lists no groups (%d rows of text)" % (tool, len(all_text(a, g)))
        g = craft_show_group(a, g, groups[0][0])
        items = craft_items(a, g)
        if not items:
            return False, "%s: group %r lists no items" % (tool, groups[0][1])
        d, info = craft_details(a, g, items[0][0])
        found.append((tool, items[0][1], info["make_x"]))
        a.reply_gump(d, 0)
        a.sleep(0.3)
    ok = all(f[2] for f in found)
    return ok, "; ".join("%s: item page of %r has MAKE X: %s" % f for f in found)


# T-004 / T-005: the four craft systems walked at 120 (our registrations' skill lines; no unported resource named).

@check("T-004", "Our smith, tailor, tinker and carpentry entries show at their registered skill, not 0 or 120")
def t_004(ctx):
    walks = _craft_walk_all(ctx, details=True)
    bad, seen = [], 0
    for skill, tool, ours in SYSTEMS:
        names = {iname.lower(): info for (_g, iname, info) in walks[skill]}
        for want, min_skill in ours.items():
            info = next((v for k, v in names.items() if want == k), None)
            if info is None:
                bad.append("%s: %r not listed" % (skill, want))
                continue
            seen += 1
            line = next((m for s, m in info["skills"] if skill.lower()[:5] in s.lower()), None)
            if line is None:
                bad.append("%s: %r has no %s skill line (%s)" % (skill, want, skill, info["skills"]))
            elif line in (0.0, 120.0) and min_skill not in (0.0,):
                bad.append("%s: %r shows %s" % (skill, want, line))
            elif abs(line - max(min_skill, 0.0)) > 0.05:
                bad.append("%s: %r shows %s, registered %s" % (skill, want, line, min_skill))
    total = sum(len(v) for v in walks.values())
    return not bad, "%d of our entries read on %d listed items across 4 systems; %s" % (seen, total, "all at their registered skill" if not bad else "; ".join(bad[:8]))


@check("T-005", "No craft entry or material names WoodPulp or WorkableGlass")
def t_005(ctx):
    walks = _craft_walk_all(ctx, details=True)
    hits = []
    for skill, rows in walks.items():
        for gname, iname, info in rows:
            texts_ = [iname] + [r[0] for r in (info or {}).get("resources", [])]
            for t in texts_:
                if re.search(r"wood ?pulp|workable ?glass", t, re.I):
                    hits.append("%s/%s/%s: %r" % (skill, gname, iname, t))
    total = sum(len(v) for v in walks.values())
    return not hits, "%d items and their materials scanned across 4 systems; %s" % (total, "none" if not hits else "; ".join(hits[:6]))


# T-013: TreasureLevel1h (server/customizations/Items/Containers/TreasureChestMod.cs:54-66): locked (RequiredSkill 56),
# MagicTrap, gold and bolts inside. The bot: locked message to a player (501747), then unlocked by the admin, opened:
# the trap fires (hits drop or a trap message) and the contents show. How the three crate graphics draw is Chase's.

@check("T-013", "TreasureLevel1h: locked to a player, trapped when opened, holds its loot", kind="BOT-PARTIAL",
       left="T-013: each of its three crate graphics draws as a proper crate or chest")
def t_013(ctx):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    staff_target(a, "[set Hits 100 HitsMaxSeed 100", p)   # a known pool to see a trap bite
    p.sleep(0.5)
    chest = add_beside(a, p, "TreasureLevel1h")
    if not chest:
        return False, "no chest appeared"
    graphic = p.items[chest].get("graphic")
    n = len(p.messages)
    p.dclick(chest)
    locked = p.find_text(r"appears to be locked", since=n, timeout=5)
    a.command("[set Locked false", 0.2)
    if a.wait_target(8):
        a.target_obj(chest)
    a.sleep(0.6)
    p.sleep(1.5)                         # the action delay: a second double-click within a second is refused (500119)
    p.send(P.status_request(p.serial, 4))
    p.sleep(0.8)
    hits_before = p.status.get("hits")
    n = len(p.messages)
    nc = len(p.containers)
    p.dclick(chest)                      # TrappableContainer.OnDoubleClick: the trap fires and the lid stays shut
    p.sleep(2.0)
    p.send(P.status_request(p.serial, 4))
    p.sleep(0.8)
    hits_after = p.status.get("hits")
    trap_msgs = [t for t in p.texts_since(n) if re.search(r"trap|magical|explod|poison|dart|spike|jolt|energ|blister|shock", t, re.I)]
    bitten = (hits_before is not None and hits_after is not None and hits_after < hits_before) or bool(trap_msgs)
    if len(p.containers) == nc:
        p.dclick(chest)                  # the trap is spent: this one opens it
    opened = p.wait(lambda: len(p.containers) > nc, 6)
    p.sleep(1.0)
    contents = p.contents(chest)
    gold = any(it.get("graphic") == 0xEED for it in contents.values())
    bolts = any(it.get("graphic") == 0x1BFB for it in contents.values())
    ok = bool(locked) and bitten and gold and bolts
    return ok, "graphic 0x%X; locked to the player: %s; opened after unlock: %s; trap bit: hits %s -> %s, messages %s; contents %d (gold %s, bolts %s)" % (
        graphic or 0, bool(locked), bool(opened), hits_before, hits_after, trap_msgs[:2], len(contents), gold, bolts)


# T-027: TreasureMapChest 5 x5 as admin: masks (OrcMask 0x141B, TribalMask 0x154B; server/patches/
# Loot-hat-types-orc-tribal-mask.patch adds them to the hat table) among the magic items, with properties in the OPL.

@check("T-027", "Five level-5 treasure chests hold orc or tribal masks with random properties")
def t_027(ctx):
    a = ctx.ensure_admin()
    masks, per_chest = [], []
    for k in range(5):
        chest = add_beside(a, a, "TreasureMapChest 5", dx=1)
        if not chest:
            per_chest.append("no chest")
            continue
        # A level-5 chest's trap fires on every double-click (503000 "Your skin blisters from the heat!") and keeps the
        # lid shut, even for a GM, so the admin disarms and unlocks it first: the contents are what this test is about.
        a.command("[set TrapType None Locked false", 0.2)
        if a.wait_target(6):
            a.target_obj(chest)
        a.sleep(1.2)
        nc = len(a.containers)
        a.dclick(chest)
        a.wait(lambda: len(a.containers) > nc, 6)
        a.sleep(1.0)
        items = a.contents(chest)
        found = [s for s, it in items.items() if it.get("graphic") in (0x141B, 0x154B)]
        for s in found:
            props = opl(a, s)
            masks.append((hex(items[s]["graphic"]), len(props), props[:3]))
        per_chest.append("%d items, %d masks" % (len(items), len(found)))
        a.command("[Delete", 0.2)
        if a.wait_target(6):
            a.target_obj(chest)
        a.sleep(1.5)
    with_props = [m for m in masks if m[1] >= 2]
    ok = len(masks) >= 2 and len(with_props) == len(masks)
    return ok, "chests: %s; masks %d, with properties beyond the name %d; e.g. %s" % ("; ".join(per_chest), len(masks), len(with_props), masks[:2])


# T-048: the mine camp. The seeder's status, then the NPCs' spots and the tents' pieces as the admin sees them at
# 3498 2744 4 (server/customizations/ClusterFMineCampSeeder.cs, ClusterFMineCampLayout.cs: Liaison tent 3494-3500 x
# 2742-2745, Archivist tent 3492-3495 x 2753-2755).

@check("T-048", "Mine camp: the Liaison and the Survey Archivist stand at the tent openings, the tents' pieces are there", kind="BOT-PARTIAL",
       left="T-048: the two tents look right on the ground")
def t_048(ctx):
    a = ctx.ensure_admin()
    n = len(a.messages)
    a.command("[ClusterFSeedMineCamp status", 1.0)
    status = [t for t in a.texts_since(n)]
    if any("would place" in t for t in status):
        # The item's own steps: "if not placed, [ClusterFSeedMineCamp then [ClusterFMoveMineCampNpcs". A fresh test
        # world (Start-TestShard -Fresh) has neither tent; seeding and moving are world setup on the scratch copy.
        a.command("[ClusterFSeedMineCamp", 2.0)
        a.command("[ClusterFMoveMineCampNpcs", 2.0)
        n = len(a.messages)
        a.command("[ClusterFSeedMineCamp status", 1.0)
        status = ["(seeded and moved first)"] + [t for t in a.texts_since(n)]
    a.command("[go 3498 2744 4", 1.5)
    a.sleep(1.5)
    names = {}
    for s in list(a.mobiles):
        if s == a.serial:
            continue
        m = a.mobiles[s]
        if abs(m.get("x", 0) - 3498) > 12 or abs(m.get("y", 0) - 2748) > 12:
            continue
        names[s] = (name_of(a, s), m.get("x"), m.get("y"))
    liaison = next((v for v in names.values() if "Liaison" in v[0]), None)
    archivist = next((v for v in names.values() if "Archivist" in v[0]), None)
    tent_walls = [it for it in a.items.values() if it.get("world") and it.get("graphic") in (0x036D, 0x036E, 0x036F, 0x0368, 0x0369)
                  and 3490 <= it.get("x", 0) <= 3500 and 2742 <= it.get("y", 0) <= 2756]
    ok_l = liaison is not None and abs(liaison[1] - 3498) <= 1 and abs(liaison[2] - 2744) <= 1
    ok_a = archivist is not None and abs(archivist[1] - 3496) <= 1 and abs(archivist[2] - 2754) <= 1
    ok = ok_l and ok_a and len(tent_walls) >= 12
    return ok, "status: %s; Liaison %s; Archivist %s; tent wall pieces in the camp box: %d (14 expected)" % (
        status[:2], liaison, archivist, len(tent_walls))


# P56-7 / P57-5: the context-menu entries our items add (cliloc numbers, and that each opens what it should).

def _tamed_mule(a, p):
    mule = add_mobile_beside(a, p, "PackMule", name_part="mule")
    if not mule:
        raise AssertionError("no pack mule appeared")
    a.command("[set Controlled true ControlMaster 0x%X" % p.serial, 0.2)
    if a.wait_target(8):
        a.target_obj(mule)
    a.sleep(0.8)
    return mule


@check("P56-7", "Trash bag, pet mimic and a tamed pack mule: their context entries, and what each opens", kind="BOT-PARTIAL",
       left="P56-7: the entries read as words in the client (ours are custom clilocs the package adds)")
def p56_7(ctx):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    out = []
    bag = give(a, p, "TrashBag")
    m = p.context_menu(bag)
    nums = [e["number"] for e in (m or {}).get("entries", [])]
    ng = len(p.gumps)
    e = p.menu_pick(bag, 1151316)      # "Clean Up Britannia" (TrashBag.DumpEntry.Cliloc)
    g = p.wait_gump(None, 6, since=ng) if e else None
    out.append(("trash bag", 1151316 in nums, bool(g)))
    if g:
        p.reply_gump(g, 0)
    # PetMimic is an item (PetMimic.cs:456, "a pet mimic", a dormant chest in the pack), not a creature.
    mimic = give(a, p, "PetMimic")
    m = p.context_menu(mimic)
    nums = [e["number"] for e in (m or {}).get("entries", [])]
    ng = len(p.gumps)
    e = p.menu_pick(mimic, 3000132)    # "Status" (PetMimic.CheckStatusEntry.Cliloc)
    g = p.wait_gump(None, 6, since=ng) if e else None
    out.append(("pet mimic", (6135 in nums or 3006135 in nums) and 3000132 in nums, bool(g)))
    if g:
        p.reply_gump(g, 0)
    mule = _tamed_mule(a, p)
    m = p.context_menu(mule)
    nums = [e["number"] for e in (m or {}).get("entries", [])]
    n = len(p.messages)
    e = p.menu_pick(mule, CL_BREED)
    tgt = p.wait_target(6) if e else None
    if tgt:
        p.cancel_target()
    out.append(("pack mule", CL_BREED in nums, bool(tgt)))
    delete_mobile(a, mule)
    ok = all(o[1] and o[2] for o in out)
    return ok, "; ".join("%s: entry %s, opens %s" % o for o in out)


@check("P57-5", "Context entries: Breed on a tamed mule, Smelt Ore on a Reinforced Ore Satchel, Metal Familiarity on a Hammer of Hephaestus (the panel opens)", kind="BOT-PARTIAL",
       left="P57-5: the words draw in our client, and a client started without the launcher shows a blank row")
def p57_5(ctx):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    mule = _tamed_mule(a, p)
    m1 = p.context_menu(mule)
    satchel = give(a, p, "ReinforcedOreSatchel")
    m2 = p.context_menu(satchel)
    hammer = give(a, p, "HammerOfHephaestus")
    m3 = p.context_menu(hammer)
    n1 = [e["number"] for e in (m1 or {}).get("entries", [])]
    n2 = [e["number"] for e in (m2 or {}).get("entries", [])]
    n3 = [e["number"] for e in (m3 or {}).get("entries", [])]
    ng = len(p.gumps)
    e = p.menu_pick(hammer, CL_METAL_FAMILIARITY)
    g = p.wait_gump(None, 6, since=ng) if e else None
    panel = bool(g) and any("Familiarity" in t or "familiarity" in t.lower() for t in all_text(p, g))
    if g:
        p.reply_gump(g, 0)
    delete_mobile(a, mule)
    ok = CL_BREED in n1 and CL_SMELT_ORE in n2 and CL_METAL_FAMILIARITY in n3 and panel
    return ok, "mule %s has %d: %s; satchel has %d: %s; hammer has %d: %s; panel opened: %s" % (
        "menu" if m1 else "NO MENU", CL_BREED, CL_BREED in n1, CL_SMELT_ORE, CL_SMELT_ORE in n2, CL_METAL_FAMILIARITY, CL_METAL_FAMILIARITY in n3, panel)


# P61-4: a post-Valorite smith deed's metal line (ShardClilocs 1900003-1900010; the patches BOD-gumps-post-valorite-
# material and SmallBOD-post-valorite-material) in the OPL and in the deed's gump.

@check("P61-4", "A Platinum..Celestial small smith deed carries 'All items must be made with <metal> ingots.' in its tooltip and gump", kind="BOT-PARTIAL",
       left="P61-4: the line is blank in a client started without the launcher")
def p61_4(ctx):
    # The random roll happens at the guildmaster (SmallSmithBOD-PostValorite.patch: 30 percent for a smith over 100,
    # one deed per cooldown), so the deed's metal is set by the admin ([set Material Frost, the patched
    # BulkMaterialType) and the test is the display: pinned's lines stop at Valorite (1045142-1045149), ours are
    # ShardClilocs 1900003-1900010, which the bot's EA cliloc table shows as "#1900006" and so on.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    out = []
    for metal in ("Platinum", "Frost", "Celestial"):
        deed = give(a, p, "SmallSmithBOD")
        a.command("[set Material %s" % metal, 0.2)
        if a.wait_target(8):
            a.target_obj(deed)
        a.sleep(0.8)
        p.items[deed].pop("props", None)
        props = opl(p, deed)
        line = next((x.strip() for x in props if re.match(r"^#19000(0[3-9]|10)$", x.strip())), None)
        ea_line = next((x for x in props if "All items must be made with" in x), None)
        ng = len(p.gumps)
        p.dclick(deed)
        g = p.wait_gump(None, 8, since=ng)
        gl = [plain(t) for t in all_text(p, g)] if g else []
        in_gump = bool(line) and any(line == t.strip() for t in gl)
        if g:
            p.reply_gump(g, 0)
        out.append((metal, line, ea_line, in_gump))
        p.sleep(1.2)
    ok = all(o[1] and o[3] and not o[2] for o in out)
    return ok, "; ".join("%s: tooltip %s%s, gump shows it %s" % (m, l or "no post-Valorite line", (" (EA line %r!)" % e) if e else "", g_) for m, l, e, g_ in out) + " (our clilocs read as #19000xx here: the bot has EA's cliloc table, the package adds the words)"


# P67-3: Tinkering's material list (Platinum to Celestial, CraftMaterialNames.cs) and scissors with Frost ingots:
# gains at 150, none at 162.5 (the per-metal ceilings of cc-P61 / cc-P67).

def gain_loop(c, g, index, tries, skill, stop_at=None):
    """Press create `tries` times; returns (base before, base after, made, failed, other)."""
    b0 = base(c, skill)
    made = failed = other = 0
    for _ in range(tries):
        t, g2 = craft_make(c, g, index)
        if any("You create" in x for x in t):
            made += 1
        elif any("You failed" in x for x in t):
            failed += 1
        else:
            other += 1
        g = g2 or g
        if stop_at is not None and (base(c, skill) or 0) >= stop_at:
            break
    c.send(P.status_request(c.serial, 5))
    c.sleep(0.8)
    return b0, base(c, skill), made, failed, other, g


@check("P67-3", "Tinkering: the material list runs Platinum to Celestial; scissors in Frost gain at 150 and stop at 162.5")
def p67_3(ctx):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    set_skills(a, p, Tinkering=150)
    tools = give(a, p, "TinkerTools")
    give(a, p, "FrostIngot 200")
    g = open_craft(p, tools)
    lst, g = craft_resources(p, g)
    names = [n for _, n in lst]
    post = [m for m in ("Platinum", "Frost", "Obsidian", "Mythril", "Celestial") if any(m.lower() in n.lower() for n in names)]
    g2, _ = craft_pick_resource(p, g, "Frost")
    g = g2 or g
    g, idx = craft_find(p, g, "scissors", exact=True)
    if g is None:
        return False, "no scissors entry; materials %s" % names
    b0, b1, made, failed, other, g = gain_loop(p, g, idx, 25, "Tinkering", stop_at=150.6)
    gained_150 = (b1 or 0) > (b0 or 0)
    set_skills(a, p, Tinkering=162.5)
    give(a, p, "FrostIngot 200")
    c0, c1, made2, failed2, other2, g = gain_loop(p, g, idx, 25, "Tinkering")
    none_1625 = c1 == c0
    ok = len(post) >= 4 and gained_150 and none_1625
    return ok, "materials %s (post-Valorite seen %s); at 150: %s -> %s over %d made / %d failed / %d other; at 162.5: %s -> %s over %d/%d/%d" % (
        names, post, b0, b1, made, failed, other, c0, c1, made2, failed2, other2)


# T-044 / T-003: a gargoyle at the tailor's and the smith's.

GARGISH_CLOTH = ("gargish cloth arms", "gargish cloth chest", "gargish cloth kilt", "gargish cloth leggings")


def learn_all(a, who):
    """GM [Recipes learnall on the character (ClusterFRecipeStore.cs:268): every gated pattern, account-wide. The recipe
    gate (cc-P78) hides a craft entry until its band's book is learned, and the hint line says who sells it."""
    staff_target(a, "[Recipes learnall", who)
    who.sleep(1.0)


def gate_lines(c, g):
    return [t for t in all_text(c, g) if re.search(r"more pattern", t)]


@check("T-044", "A gargoyle's Tailoring lists the gargish cloth armor (two of each of four pieces) once the patterns are known, one crafts; robe, sash and half apron are absent")
def t_044(ctx):
    a = ctx.ensure_admin()
    c = ctx.fresh(race="gargoyle", female=True, label="garg-tailor")
    try:
        set_skills(a, c, Tailoring=100)
        kit = give(a, c, "SewingKit")
        give(a, c, "Cloth 100")
        g = open_craft(c, kit)
        gates0 = gate_lines(c, g)
        rows, g = craft_walk(c, g, details=False)
        names0 = [iname.lower() for _, iname, _ in rows]
        counts0 = {n: names0.count(n) for n in GARGISH_CLOTH}
        c.reply_gump(g, 0)
        learned = False
        if any(v < 2 for v in counts0.values()):
            learn_all(a, c)
            learned = True
        g = open_craft(c, kit)
        rows, g = craft_walk(c, g, details=False)
        names = [iname.lower() for _, iname, _ in rows]
        counts = {n: names.count(n) for n in GARGISH_CLOTH}
        garments = [n for n in names if re.search(r"gargish (robe|sash|half apron)", n)]
        g, idx = craft_find(c, g, "gargish cloth arms")
        made = None
        tries = 0
        while g is not None and tries < 5 and not (made and made[0]):
            # Gargish cloth arms need 87.1 (OURS_TAILOR): at 100 a try can fail ("You failed to create the item").
            tries += 1
            before = set(c.items)
            t, g2 = craft_make(c, g, idx, wait=8)
            g = g2 or g
            new = [s for s, it in c.items.items() if s not in before and it.get("container") == c.backpack()]
            names = [name_of(c, s).lower() for s in new]
            made = (any("gargish cloth arms" in n for n in names), [hex(c.items[s].get("graphic", 0)) for s in new][:2], [x[:60] for x in t if "create" in x or "fail" in x][:1], tries)
            c.sleep(1.0)
        if g:
            c.reply_gump(g, 0)
        ok = all(v == 2 for v in counts.values()) and not garments and bool(made and made[0])
        return ok, "as a plain gargoyle: %s (gate lines %s); %safter [Recipes learnall: %s; garments listed %s; crafted arms: %s" % (
            counts0, gates0[:3], "" if learned else "no grant needed; ", counts, garments or "none", made)
    finally:
        ctx.done(c)


@check("T-003", "A gargoyle crafts gargish plate (smith) and gargish cloth (tailor) and wears both")
def t_003(ctx):
    a = ctx.ensure_admin()
    c = ctx.fresh(race="gargoyle", female=False, label="garg-smith")
    try:
        set_skills(a, c, Blacksmith=100, Tailoring=100)
        staff_target(a, "[set RawStr 100 RawDex 50", c)      # a fresh character's Str 10 cannot wear plate or carry the ingots
        forge = place_forge(a, c)
        add_beside(a, c, "Anvil", dx=0, dy=1)
        hammer = give(a, c, "SmithHammer")
        give(a, c, "IronIngot 100")
        kit = give(a, c, "SewingKit")
        give(a, c, "Cloth 100")
        results = []
        granted = False
        for tool, want in ((hammer, "gargish platemail arms"), (kit, "gargish cloth arms")):
            g = open_craft(c, tool)
            gates = gate_lines(c, g)
            g, idx = craft_find(c, g, want)
            if g is None and not granted:
                learn_all(a, c)
                granted = True
                g = open_craft(c, tool)
                g, idx = craft_find(c, g, want)
            if g is None:
                results.append((want, "not listed even after [Recipes learnall (gate lines %s)" % gates[:2], None))
                continue
            before = set(c.items)
            made = None
            for _ in range(6):
                t, g = craft_make(c, g, idx, wait=8)
                new = [s for s, it in c.items.items() if s not in before and it.get("container") == c.backpack() and (it.get("amount") or 1) == 1]
                named = [s for s in new if want.split()[-1] in name_of(c, s).lower() and "gargish" in name_of(c, s).lower()]
                if named:
                    made = named[0]
                    break
                if g is None:
                    break
            if g:
                c.reply_gump(g, 0)
            if made is None:
                results.append((want, "never made in 6 tries", None))
                continue
            c.sleep(1.2)
            worn, msgs, it = try_equip(c, made, 2.5)
            if not worn:
                c.sleep(1.5)
                worn, msgs2, it = try_equip(c, made, 3.0)
                msgs = msgs + msgs2
            results.append((want, "made", "worn on layer %s" % it.get("layer") if worn else "not worn: %s" % msgs[-3:]))
            if worn:
                # Both pieces are arms (layer 19): the plate goes back to the pack before the cloth is tried.
                c.lift_drop(made, 1, c.backpack())
                c.sleep(1.2)
        ok = all(r[1] == "made" and r[2] and r[2].startswith("worn") for r in results)
        return ok, "; ".join("%s: %s, %s" % r for r in results) + ("; the patterns were granted with [Recipes learnall first (the recipe gate hides them from a plain gargoyle)" if granted else "")
    finally:
        ctx.done(c)


# The second batch (guilds, skill caps, crafting ceilings, dungeon spawns) shares these helpers.
from . import checks_p223b  # noqa: E402,F401
