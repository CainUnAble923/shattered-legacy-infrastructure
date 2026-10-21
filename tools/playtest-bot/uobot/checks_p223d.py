"""cc-P223, fourth batch: the Society of Smiths' bulk orders and Smithing Seals (cc-P56, cc-P57, cc-P61, cc-P67).
Imported by checks_p223c.py.

The player joins the Society at the New Haven guildmaster the way T-041 does ("I wish to join", SmithGuildmasterGump
BtnJoin 10). Every figure is read from the code under test, cited per check; the Seal sums are ClusterFSmithBODSystem.cs
:339-432 (an iron small regular 10 is always 52, its own comment at :330).
"""
import re

from . import packets as P
from .checks_p223b import roomy
from .checks import check, entries, all_text, buttons, give, staff_target, near
from .checks_p223 import opl
from .checks_p223b import SMITHS_AT, SMITHS_NAMES, move_to, guildmaster_near, ask_guild

# SmithGuildmasterGump.cs:27-35.
BTN_BULK, BTN_CATALOG, BTN_JOIN = 1, 6, 10
# SmithBulkOrderChoiceGump.cs:20-24.
CH_SMALL, CH_LARGE, CH_TEACH, CH_MODE = 1, 2, 3, 5
# The SmallSmithBOD, filled ([add takes trailing "set <prop> <value>", pinned Commands/Object Creation/Add.cs:121-146);
# Number is 1020000 + the graphic (SmallBulkEntry.cs:80-87), PlateChest 0x1415.
IRON_10 = ("None", False, 10)
CELESTIAL_EXC_20 = ("Celestial", True, 20)


def filled_deed(a, p, spec):
    """A filled small smith deed in the player's pack: [AddToPack SmallSmithBOD, then its properties in two [set lines
    (one command with them all is 140 characters, and speech over 128 is dropped unanswered, 10-21)."""
    material, exc, amount = spec
    deed = give(a, p, "SmallSmithBOD")
    for cmd in ("[set AmountMax %d AmountCur %d Material %s" % (amount, amount, material),
                "[set RequireExceptional %s Type PlateChest Number 1025141 Graphic 5141" % ("true" if exc else "false")):
        a.command(cmd, 0.2)
        if a.wait_target(6):
            a.target_obj(deed)
        a.sleep(0.6)
    return deed
BANKED = re.compile(r"Society of Smiths: \+([\d,]+) standing, \+([\d,]+) Smithing Seals banked\.")
BOD_GRAPHIC = 0x2258


def num(s):
    return int(s.replace(",", ""))


def society(ctx):
    """The player, a Society member, standing at the guildmaster. Returns (admin, player, guildmaster serial)."""
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    roomy(a, p)
    move_to(a, p, *SMITHS_AT)
    gm, nm = guildmaster_near(a, p, SMITHS_AT[0], SMITHS_AT[1], SMITHS_NAMES, "BlacksmithGuildmaster")
    if not gm:
        raise AssertionError("no Society guildmaster near %s" % (SMITHS_AT,))
    g = ask_guild(p, gm)
    if g and BTN_JOIN in buttons(g):
        p.reply_gump(g, BTN_JOIN)
        p.sleep(3.0)
        g = ask_guild(p, gm)
    if not g or seal_balance_of(g) is None:
        raise AssertionError("the Society gump shows no Smithing Seals line (not a member?): %s" % (all_text(p, g)[:6] if g else None))
    p.reply_gump(g, 0)
    return a, p, gm


def seal_balance_of(g):
    """SmithGuildmasterGump member view: 'Smithing Seals:' at (18,86), the value at (120,86) (SmithGuildmasterGump.cs:98-99)."""
    es = entries(g)
    if not any(e[0] == "text" and e[3] == "Smithing Seals:" for e in es):
        return None
    v = next((e[3] for e in es if e[0] == "text" and e[1] == 120 and e[2] == 86), None)
    return num(v) if v and re.match(r"^[\d,]+$", v) else None


def seals(p, gm):
    g = ask_guild(p, gm)
    b = seal_balance_of(g) if g else None
    if g:
        p.reply_gump(g, 0)
    p.sleep(0.6)
    return b


def hand_in(p, deed, npc, wait=3.0):
    """Drag the deed onto the NPC (0x07 then 0x08 onto the mobile; the BlacksmithGuildmaster's OnDragDrop,
    ClusterFSmithBODSystem.cs:225-292). Returns the messages since."""
    n = len(p.messages)
    p.send(P.lift(deed, 1))
    p.sleep(0.4)
    p.send(P.drop(deed, -1, -1, 0, npc))
    p.sleep(wait)
    return p.messages[n:]


def choice_gump(p, gm, timeout=8):
    """The guildmaster's 'Bulk Order Info' context entry (3006152) for a member opens SmithBulkOrderChoiceGump
    (ClusterFSmithBODSystem.cs:599-619)."""
    p.sleep(1.0)
    n = len(p.gumps)
    if not p.menu_pick(gm, 3006152):
        return None
    return p.wait_gump(lambda g: any("Society of Smiths - Bulk Order" in t for t in all_text(p, g)), timeout, since=n)


def press(p, g, button, pred, timeout=8):
    n = len(p.gumps)
    p.reply_gump(g, button)
    return p.wait_gump(pred, timeout, since=n)


def is_choice(p):
    return lambda g: any("Society of Smiths - Bulk Order" in t for t in all_text(p, g))


def mode_line(p, g):
    return next((t for t in all_text(p, g) if t.startswith("Completed orders:")), None)


def set_mode(p, gm, want):
    """Press 5 (cycle Bank, CashOut, Ask; ClusterFSmithBODPayout.cs:89-100) until the label reads `want`."""
    g = choice_gump(p, gm)
    if not g:
        raise AssertionError("Bulk Order Info opened no choice gump")
    for _ in range(4):
        if mode_line(p, g) == "Completed orders: " + want:
            p.reply_gump(g, 0)
            return True
        g = press(p, g, CH_MODE, is_choice(p))
        if not g:
            raise AssertionError("the choice gump did not come back after the mode button")
    p.reply_gump(g, 0)
    return False


def teaching_on(p, g):
    """Button 3 is drawn with art 211 when on, 210 when off (SmithBulkOrderChoiceGump.cs:76-81)."""
    m = re.search(r"\{\s*button\s+-?\d+\s+-?\d+\s+(\d+)\s+\d+\s+\d+\s+\d+\s+3\s*\}", g["layout"])
    return m and m.group(1) == "211"


def set_teaching(p, gm, on):
    g = choice_gump(p, gm)
    if not g:
        raise AssertionError("Bulk Order Info opened no choice gump")
    if bool(teaching_on(p, g)) != on:
        g = press(p, g, CH_TEACH, is_choice(p))
    state = bool(teaching_on(p, g)) if g else None
    if g:
        p.reply_gump(g, 0)
    return state


def deed_facts(p, deed):
    """(material line, item line) from the deed's properties: our post-Valorite materials read '#1900003'..'#1900010'
    (ShardClilocs.cs:39-83; the bot has EA's table), stock ones in words; the item is cliloc 1060658 '<item>: <n>'."""
    (p.items.get(deed) or {}).pop("props", None)
    props = [x.strip() for x in opl(p, deed)]
    mat = next((x for x in props if re.match(r"^#19000(0[3-9]|10)$", x)), None)
    if not mat:
        m = next((re.search(r"must be made with (.+) ingots", x) for x in props if re.search(r"must be made with (.+) ingots", x)), None)
        mat = m.group(1).title() if m else "Iron"
    item = next((x for x in props if re.match(r"^.+: \d+$", x) and not x.lower().startswith(("amount to make", "weight"))), None)
    return mat, item, props


FROST = "#1900006"
POST_VALORITE = {"#1900003": "Platinum", "#1900004": "Toxic", "#1900005": "Blaze", "#1900006": "Frost",
                 "#1900007": "Obsidian", "#1900008": "Mythril", "#1900009": "Adamantium", "#1900010": "Celestial"}


def request_smalls(a, p, gm, count):
    """Ask for `count` small orders (choice button 1, then the accept gump's OK, pinned SmallBODAcceptGump.cs:62-87);
    each deed is read and then deleted, so the 3-in-pack cap (ClusterFSmithBODSystem.cs:56-57) never bites."""
    out = []
    pack = p.backpack()
    for _ in range(count):
        g = choice_gump(p, gm)
        if not g:
            out.append(("no choice gump", None))
            continue
        before = {s for s, it in p.contents(pack).items() if it.get("graphic") == BOD_GRAPHIC}
        acc = press(p, g, CH_SMALL, lambda x: not is_choice(p)(x))
        if not acc:
            out.append(("no accept gump", None))
            continue
        p.reply_gump(acc, 1)
        deed = p.wait(lambda: next((s for s, it in p.contents(pack).items() if it.get("graphic") == BOD_GRAPHIC and s not in before), None), 6)
        if not deed:
            out.append(("no deed", None))
            continue
        mat, item, _ = deed_facts(p, deed)
        out.append((POST_VALORITE.get(mat, mat), item))
        near(a, p)
        a.command("[Delete", 0.2)
        if a.wait_target(6):
            a.target_obj(deed)
        a.sleep(0.6)
    return out


# -- the checks ------------------------------------------------------------------------------------------------------

@check("P56-1", "A Society member hands in a filled iron small 10 deed: '+52 Smithing Seals banked.' and the balance rises by 52")
def p56_1(ctx):
    a, p, gm = society(ctx)
    set_mode(p, gm, "Always bank")
    b0 = seals(p, gm)
    deed = filled_deed(a, p, IRON_10)
    said = hand_in(p, deed, gm)
    m = next((BANKED.search(x["text"]) for x in said if BANKED.search(x["text"] or "")), None)
    b1 = seals(p, gm)
    ok = bool(m) and num(m.group(2)) == 52 and b0 is not None and b1 == b0 + 52
    return ok, "message %r; Seals %s -> %s; NPC said %s" % (m.group(0) if m else None, b0, b1,
        [x["text"] for x in said if x.get("serial") == gm][-1:])


@check("P56-2", "A non-member hands a filled iron deed to a town Blacksmith: paid in gold, the bank-points gump (1157076) opens", kind="BOT-PARTIAL",
       left="P56-2: where the gold shows (account gold, or a pile in the bank box)")
def p56_2(ctx):
    # Blacksmith.OnDragDrop -> TryMemberTurnIn (false for a non-member) -> BulkOrderVendors.TryTurnIn
    # (ClusterFSmithBODPayout.cs:433-438, :592-595; BulkOrderVendors.cs:263-357): the NPC says 1157204, the gold goes
    # through Banker.Deposit, and BODConfirmBankPointsGump opens (title 1157076, buttons 1 and 2). A Blacksmithy of 0
    # is refused (1045130, pinned Blacksmith.cs:108), so the fresh character gets 50.
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=False)
    try:
        staff_target(a, "[SetSkill Blacksmith 50", c)
        smith = None
        from .checks_p223 import add_mobile_beside
        smith = add_mobile_beside(a, c, "Blacksmith", name_part="blacksmith")
        if not smith:
            raise AssertionError("no Blacksmith appeared beside the character")
        a.command("[set CantWalk true", 0.2)
        if a.wait_target(6):
            a.target_obj(smith)
        a.sleep(0.5)
        deed = filled_deed(a, c, IRON_10)
        ng = len(c.gumps)
        said = hand_in(c, deed, smith, wait=4.0)
        g = c.wait_gump(lambda x: "1157076" in x["layout"], 8, since=ng)
        cl = [x.get("cliloc") for x in said if x.get("cliloc")]
        gone = deed not in c.items or (c.items.get(deed) or {}).get("container") != c.backpack()
        if g:
            c.reply_gump(g, 1)
        from .checks_p223 import delete_mobile
        delete_mobile(a, smith)
        ok = bool(g) and 1157204 in cl and gone
        return ok, "points gump 1157076 %s (buttons %s); NPC/cliloc lines %s; deed taken %s; texts %s" % (
            bool(g), buttons(g) if g else None, cl, gone, [x["text"] for x in said][-3:])
    finally:
        ctx.done(c)


@check("P56-3", "'Completed orders: Ask each time': a hand-in asks, and the Bank button shows the Seal figure")
def p56_3(ctx):
    # SmithTurnInChoiceGump (ClusterFSmithBODPayout.cs:249-289): button 1 "Bank: about 52 Smithing Seals" at (54,76);
    # closing it keeps the deed in the pack (ClusterFSmithBODSystem.cs:286-288).
    a, p, gm = society(ctx)
    asked = set_mode(p, gm, "Ask each time")
    deed = filled_deed(a, p, IRON_10)
    ng = len(p.gumps)
    hand_in(p, deed, gm)
    g = p.wait_gump(lambda x: any("Completed Order" in t for t in all_text(p, x)), 8, since=ng)
    lines = all_text(p, g) if g else []
    bank = next((t for t in lines if t.startswith("Bank:")), None)
    if g:
        p.reply_gump(g, 0)
    p.sleep(1.0)
    kept = (p.items.get(deed) or {}).get("container") == p.backpack()
    set_mode(p, gm, "Always bank")
    if kept:
        near(a, p)
        a.command("[Delete", 0.2)
        if a.wait_target(6):
            a.target_obj(deed)
    ok = asked and bank == "Bank: about 52 Smithing Seals" and 1 in buttons(g or {"layout": ""}) and kept
    return ok, "mode set: %s; gump %s; bank line %r; deed kept on close %s" % (asked, lines[:1], bank, kept)


CATALOG = {"Sturdy Shovel": "150", "Mining Gloves +5": "1,800", "Smith Guild Salvage Bag": "750"}


def catalog_rows(p, g):
    """SmithSealCatalogGump rows: the name at x 191, the cost at x 400, on the same row (SmithSealCatalogGump.cs:173-193)."""
    es = [e for e in entries(g) if e[0] == "text"]
    rows = {}
    for e in es:
        if e[1] == 191:
            cost = next((f[3] for f in es if f[1] == 400 and abs(f[2] - e[2]) <= 4), None)
            rows[e[3]] = cost
    return rows


@check("P57-1", "The Seal catalog: 150, 1,800, 750 and 45,000 on their rows, and an iron 10 deed still banks +52")
def p57_1(ctx):
    # SmithSealCatalogGump.cs:70-115 (base x PriceFactor 3): Sturdy Shovel 150, Mining Gloves +5 1,800, Smith Guild
    # Salvage Bag 750 on Tools (tab 1, the default); Valorite Runic Hammer 45,000 on Runic Hammers (tab button 3).
    a, p, gm = society(ctx)
    g = ask_guild(p, gm)
    cat = press(p, g, BTN_CATALOG, lambda x: any(t == "Balance:" for t in all_text(p, x)))
    if not cat:
        return False, "Seal Catalog (button 6) opened no catalog"
    tools = catalog_rows(p, cat)
    runic_g = press(p, cat, 3, lambda x: any(t == "Balance:" for t in all_text(p, x)))
    runic = catalog_rows(p, runic_g) if runic_g else {}
    if runic_g:
        p.reply_gump(runic_g, 0)
    seen = dict((k, tools.get(k)) for k in CATALOG)
    seen["Valorite Runic Hammer"] = runic.get("Valorite Runic Hammer")
    prices_ok = all(seen[k] == v for k, v in CATALOG.items()) and seen["Valorite Runic Hammer"] == "45,000"
    set_mode(p, gm, "Always bank")
    deed = filled_deed(a, p, IRON_10)
    said = hand_in(p, deed, gm)
    m = next((BANKED.search(x["text"]) for x in said if BANKED.search(x["text"] or "")), None)
    ok = prices_ok and bool(m) and num(m.group(2)) == 52
    return ok, "catalog %s; iron 10 deed: %r" % (seen, m.group(0) if m else None)


@check("P57-2", "Blacksmith 200: a filled Celestial exceptional 20 deed banks about 1,466 Seals")
def p57_2(ctx):
    # Gold part ceil(gold x 1.25 x 3 / 400) with gold = Valorite's 12000 roll (90% to 111%) x 5, plus the item part 300 x
    # 3.0 = 900: 1,407 to 1,525, mean about 1,466; standing (60 + 15 x 16) x 2 = 600 (ClusterFSmithBODSystem.cs:339-474).
    a, p, gm = society(ctx)
    staff_target(a, "[SetSkill Blacksmith 200", p)
    set_mode(p, gm, "Always bank")
    deed = filled_deed(a, p, CELESTIAL_EXC_20)
    said = hand_in(p, deed, gm)
    m = next((BANKED.search(x["text"]) for x in said if BANKED.search(x["text"] or "")), None)
    n = num(m.group(2)) if m else None
    ok = bool(m) and 1407 <= n <= 1525 and num(m.group(1)) == 600
    return ok, "message %r (expected 1,407 to 1,525 Seals and +600 standing)" % (m.group(0) if m else [x["text"] for x in said][-3:])


@check("P57-4", "A member's Bulk Order Info opens the Small/Large choice with its Completed orders line, and hands out no deed", kind="BOT-PARTIAL",
       left="P57-4: the gump is solid and the line ends inside the frame")
def p57_4(ctx):
    # SmithBulkOrderChoiceGump.cs:58-96; ClusterFSmithBODSystem.cs:608-618.
    a, p, gm = society(ctx)
    pack = p.backpack()
    before = sum(1 for it in p.contents(pack).values() if it.get("graphic") == BOD_GRAPHIC)
    g = choice_gump(p, gm)
    if not g:
        return False, "Bulk Order Info opened no choice gump"
    lines = all_text(p, g)
    p.sleep(1.5)
    after = sum(1 for it in p.contents(pack).values() if it.get("graphic") == BOD_GRAPHIC)
    has = {k: any(t.startswith(k) for t in lines) for k in ("Small bulk order", "Large bulk order", "Completed orders:", "Active orders:")}
    p.reply_gump(g, 0)
    ok = all(has.values()) and CH_SMALL in buttons(g) and after == before
    return ok, "lines %s; buttons %s; deeds in pack %d -> %d" % (has, buttons(g), before, after)


@check("P61-2", "Blacksmith 150 with 'Orders that still teach me' on: the small orders are Frost, and their items vary beyond the plate tunic")
def p61_2(ctx):
    # ClusterFSmithTeaching.cs:103-146, :256-298 with SmallSmithBOD-teaching-orders.patch: at base 150 only Frost
    # still teaches (Blaze's ceiling is exactly 150), and PickItems keeps every makeable armour piece.
    a, p, gm = society(ctx)
    staff_target(a, "[SetSkill Blacksmith 150", p)
    on = set_teaching(p, gm, True)
    got = request_smalls(a, p, gm, 8)
    mats = [m for m, _ in got]
    items = sorted(set(i for _, i in got if i))
    ok = on and len(got) == 8 and all(m == "Frost" for m in mats) and len(items) >= 3
    return ok, "teaching on %s; 8 orders: metals %s; %d different items %s" % (on, mats, len(items), items[:8])


@check("P67-5", "Blacksmith 150: with teaching on the post-Valorite orders are all Frost; off, Platinum, Toxic or Blaze appear")
def p67_5(ctx):
    # Off is the stock roll (CreateRandomFor(m, false)) with SmallSmithBOD-PostValorite.patch's 30% post-Valorite
    # chance on armour, kept only at the skill: at 150 Platinum, Toxic, Blaze or Frost, about 15% of orders in all.
    # 40 orders off: the chance that none is Platinum, Toxic or Blaze is about 0.87^40, under 1%.
    a, p, gm = society(ctx)
    staff_target(a, "[SetSkill Blacksmith 150", p)
    on = set_teaching(p, gm, True)
    with_on = [m for m, _ in request_smalls(a, p, gm, 6)]
    off = set_teaching(p, gm, False)
    with_off = [m for m, _ in request_smalls(a, p, gm, 40)]
    set_teaching(p, gm, True)
    lower = [m for m in with_off if m in ("Platinum", "Toxic", "Blaze")]
    counts = dict((m, with_off.count(m)) for m in sorted(set(with_off)))
    ok = on and off is False and with_on and all(m == "Frost" for m in with_on) and bool(lower)
    return ok, "on (%s): %s; off (%s), 40 orders: %s" % (on, with_on, off, counts)


from . import checks_p223e  # noqa: E402,F401
