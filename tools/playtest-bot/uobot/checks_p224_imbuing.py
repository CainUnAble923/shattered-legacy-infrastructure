"""cc-P224, group: the Artificers' Order imbuing rework (cc-P82, cc-P173, cc-P176).

Three checks. The player joins the Artificers' Order through its guildmaster's context menu (the hall page's Join button:
ClusterFGuildSystem.cs:1092-1093, :1204-1206) and opens the Imbuing Table by double-clicking the Artificers' Toolkit the
join hands over (ClusterFGuildSystem.cs:500-504, ArtificersToolkit.cs:59). Essence Shards are the Artificers' guild
currency (ArtificersImbueGump.cs:906, :910); no staff command sets it, so the checks earn shards the way a player does: a
staff-made magic sword, fully disenchanted at the table (UnravelOne, ArtificersImbueGump.cs:1976-1982).

Every string asserted is read from server/customizations (cited per check); the play tests' own wording is the notes'
"For Chase" lists (notes/cc-P82-artificers-imbuing.md, cc-P173-imbue-all-slayers.md,
cc-P176-cont-artificers-unravel-container.md).
"""
import re

from . import packets as P
from .checks import check, all_text, buttons, entries, plain, give, staff_target, SHARED
from .checks_p224 import set_skill_to, spawn, add_item_here, set_on, gump_with, pack_items, wait_text, SKILL

TABLE_TITLE = "Artificers' Order - Imbuing Table"     # ArtificersImbueGump.cs:899
TOOLKIT_GRAPHIC, ARTIFICER_HUE = 0x1EB8, 0x497         # ArtificersToolkit.cs:22, :24
SATCHEL_GRAPHIC = 0xA272                               # ArtificersSatchel.cs:32 (hue 0x497, :33)
FORGE_GRAPHIC = 0x4277                                 # ImbueSites.cs:111 (ArtificersSoulForge), hue 0x497 (:112)
ESSENCE_GRAPHIC = 0x26B4                               # PropertyEssence.cs:28
BOOK_GRAPHIC = 0x0FF4                                  # ImbueFormulaBooks.cs:33

# ArtificersImbueGump.cs:1587-1606 (BtnId)
BTN_IMBUE_TARGET, BTN_APPLY_IMBUE, BTN_DISENCHANT_TARGET, BTN_FULL_DISENCHANT, BTN_APPLY_DISENCHANT = 1, 5, 6, 7, 8
BTN_APPLY_EXTRACT, BTN_UNRAVEL_TARGET, BTN_APPLY_UNRAVEL, BTN_NEXT_PAGE, BTN_CONFIRM_BASE = 9, 12, 13, 4, 1000
TIER = {"None": 0, "Basic": 1, "Refined": 2, "Full": 3, "Overcharged": 4}   # ClusterFImbueKnowledge.cs:26-33

# ImbuingTable.cs:193, :200: the four books below the Master Formulary (rank 5 needs the trial, ImbueFormulaBooks.cs:145-149).
BOOK_IDS = ("apprentice-formulary", "journeyman-formulary", "artificers-formulary", "arcane-formulary")

# A shard mule: six catalogue rows on one Longsword, each worth ShardBase / 2 at a full disenchant (ArtificersImbueGump.cs:1979):
# Self Repair 40 (:371), Hit Life/Stamina/Mana Leech 7 (:308-316), Luck 5 (:259), Damage Increase 8 (:211) = 35 shards.
# Nested AOS attributes go through [set's dotted property chain (pinned UOContent/Commands/Properties.cs:119).
FARM_PROPS = ("WeaponAttributes.SelfRepair 5 WeaponAttributes.HitLeechHits 50 WeaponAttributes.HitLeechStam 50 "
              "WeaponAttributes.HitLeechMana 50 Attributes.Luck 100 Attributes.WeaponDamage 50")


# -- gump rows -----------------------------------------------------------------------------------------------------------

def row(g, pred, x=None):
    """The row of the first label matching pred (and drawn at x, when given): its y, and every button and label laid
    out within 4 px of that y (a table row is AddButton(18, y) then AddLabel(44, y or y + 2); the knowledge gump's is
    two buttons then three labels, ImbueFormulaBooks.cs:263-267). None when no label matches. The view page names an
    imbued property twice, at x=18 (Current Enchantments, :1033) and x=44 (Known Properties, :1101): x tells them apart."""
    es = entries(g)
    hit = next(((lx, y, v) for k, lx, y, v in es if k == "text" and pred(v) and (x is None or lx == x)), None)
    if not hit:
        return None
    y0 = hit[1]
    return {"y": y0, "label": hit[2],
            "buttons": sorted((bx, v) for k, bx, y, v in es if k == "button" and abs(y - y0) <= 4),
            "texts": sorted((tx, plain(v)) for k, tx, y, v in es if k == "text" and abs(y - y0) <= 4)}


def page_find(c, g, pred, next_id=BTN_NEXT_PAGE, pages=12, x=None):
    """The row matching pred on this gump or a later page (Next = next_id). Returns (gump, row or None)."""
    for _ in range(pages):
        r = row(g, pred, x)
        if r or next_id not in buttons(g):
            return g, r
        n = len(c.gumps)
        c.reply_gump(g, next_id)
        g = c.wait_gump(None, 8, since=n)
        if not g:
            return None, None
    return g, None


def has(c, g, text):
    return any(text in t for t in all_text(c, g))


# -- the Order, the toolkit, the table --------------------------------------------------------------------------------------

def ensure_member(a, p):
    """Joins p to the Artificers' Order at a freshly [add]ed ArtificersGuildmaster: its "Talk" context entry (cliloc 6146,
    ClusterFGuildSystem.cs:786, sent +3000000 as pinned does for small numbers; ClusterFGuildmasterExtension.cs:37) opens
    the hall page (GuildTaskDetailGump, def.Name label :1065-1070), whose Join button is 1 (:1040, :1092-1093). Join says
    "You have joined the Artificers' Order as <rank>. Joining is free." (:436) and hands over the Essence Satchel and the
    Toolkit (:500-504 through ClusterFGuildStarterPath.GiveTools). Returns (state, texts)."""
    # One guildmaster per session, pinned so it cannot wander out of the entry's range (4) or the Join's (8, :91).
    gm = SHARED.get("artificers_gm")
    at = p.mobiles.get(gm) if gm else None
    if not at or max(abs(at.get("x", -9999) - p.pos[0]), abs(at.get("y", -9999) - p.pos[1])) > 3:
        gm = spawn(a, p, "ArtificersGuildmaster")
        if not gm:
            raise AssertionError("[add ArtificersGuildmaster brought no mobile into the player's view")
        set_on(a, gm, "CantWalk true", p)
        SHARED["artificers_gm"] = gm
    m = p.context_menu(gm)
    if not m:
        raise AssertionError("the guildmaster sent no context menu")
    cl = next((e["cliloc"] for e in m["entries"] if e["cliloc"] in (3006146, 6146)), None)
    if cl is None:
        raise AssertionError("no Talk entry (6146) on the guildmaster: %s" % [(e["cliloc"], e.get("text")) for e in m["entries"]])
    n = len(p.gumps)
    p.pick_context(m, cl)
    g = gump_with(p, "Artificers' Order", 10, since=n)
    if not g:
        raise AssertionError("Talk opened no Artificers' Order hall page")
    texts = all_text(p, g)
    if 1 in buttons(g) and any(t == "Join the guild" for t in texts):
        k = len(p.messages)
        n = len(p.gumps)
        p.reply_gump(g, 1)
        said = wait_text(p, r"You have joined the Artificers' Order", k, 8)
        again = p.wait_gump(None, 6, since=n)
        if again:
            p.reply_gump(again, 0)
        return "joined now", [said] + [t for t in p.texts_since(k) if "Toolkit" in t][:1]
    p.reply_gump(g, 0)
    member = next((t for t in texts if t.startswith("Member. Rank:")), None)
    return ("already a member" if member else "NOT a member and no Join button"), [member or texts[:4]]


def toolkit_of(a, p):
    """The Artificers' Toolkit in the pack (0x1EB8, hue 0x497), else one from the admin ([AddToPack ArtificersToolkit)."""
    kit = next((s for s, it in pack_items(p, TOOLKIT_GRAPHIC).items() if it.get("hue") == ARTIFICER_HUE), None)
    return kit or give(a, p, "ArtificersToolkit")


def open_table(c, kit):
    """Double-clicks the toolkit (ArtificersToolkit.cs:59): the Imbuing Table's first page."""
    g = None
    for attempt in range(4):
        n, m = len(c.gumps), len(c.messages)
        c.dclick(kit)
        g = c.wait_gump(lambda x: TABLE_TITLE in all_text(c, x), 10, since=n)
        if g:
            break
        # "You must wait to perform another action." (the item-use delay after lifting and dropping, cc-P224 trial).
        c.sleep(2.0)
    if not g:
        raise AssertionError("the toolkit opened no Imbuing Table: %s" % c.texts_since(m)[-3:])
    return g


def shards_of(c, g):
    """The header's "Essence Shards: N" (ArtificersImbueGump.cs:910)."""
    for t in all_text(c, g):
        m = re.match(r"^Essence Shards: (\d+)$", t)
        if m:
            return int(m.group(1))
    return None


def press_and_target(c, g, button, serial, pred, timeout=10):
    """A table button that asks for a target, answered with an item; returns (the next gump, the messages)."""
    n, m = len(c.gumps), len(c.messages)
    c.target = None
    c.reply_gump(g, button)
    if not c.wait_target(8):
        raise AssertionError("table button %d gave no target cursor: %s" % (button, c.texts_since(m)[-3:]))
    c.target_obj(serial)
    return c.wait_gump(pred, timeout, since=n), c.texts_since(m)


def view_item(c, kit, item):
    """Target an item to imbue (button 1, :951): the ViewItem page ("Item: <name>", :1018)."""
    g = open_table(c, kit)
    g, texts = press_and_target(c, g, BTN_IMBUE_TARGET, item, lambda x: any(t.startswith("Item: ") for t in all_text(c, x)))
    if not g:
        raise AssertionError("no imbue view for the item: %s" % texts[-3:])
    return g


def imbue_attempt(c, g_view, row_name, tier, btn=None):
    """From a ViewItem page: the row's button (PropBase + index, :1090), the tier's button (ConfirmBase + tier, :1189,
    :1242), then Apply Imbue (5, :1306). `btn` skips the row lookup (a row at [MAX] draws no button, :1073, :1088).
    Returns (message, select-page texts, confirm-page texts, the page that followed)."""
    if btn is None:
        g, r = page_find(c, g_view, lambda t: t == row_name, x=44)
        if not r:
            return "no row %r on the imbue view" % row_name, [], [], g
        if not r["buttons"]:
            return "row %r has no button: %s" % (row_name, r["texts"]), [], [], g
        btn = r["buttons"][0][1]
    else:
        g = g_view
    n = len(c.gumps)
    c.reply_gump(g, btn)
    sel = c.wait_gump(lambda x: any(t == "Imbue: " + row_name for t in all_text(c, x)), 8, since=n)
    if not sel:
        return "button %d opened no 'Imbue: %s' page" % (btn, row_name), [], [], None
    n = len(c.gumps)
    c.reply_gump(sel, BTN_CONFIRM_BASE + tier)
    conf = c.wait_gump(lambda x: has(c, x, "Confirm Imbue"), 8, since=n)
    if not conf:
        return "tier button %d opened no Confirm Imbue page" % (BTN_CONFIRM_BASE + tier), all_text(c, sel), [], None
    m, n = len(c.messages), len(c.gumps)
    c.reply_gump(conf, BTN_APPLY_IMBUE)
    msg = wait_text(c, r"You successfully imbue|Your hands falter|Overcharged imbuing needs|Here you can imbue only|"
                       r"^You need |^Your account|^You cannot|reached your maximum|intensity budget|The item is gone", m, 8)
    after = c.wait_gump(None, 6, since=n)
    return msg, all_text(c, sel), all_text(c, conf), after


def imbue_until(c, g_view, row_name, tier, btn=None, tries=6):
    """Apply until the server says success (a falter consumes nothing, :1858). Returns (msg, sel, conf, view, falters)."""
    falters = 0
    for _ in range(tries):
        msg, sel, conf, after = imbue_attempt(c, g_view, row_name, tier, btn)
        if msg and "Your hands falter" in msg and after:
            falters += 1
            g_view = after
            continue
        return msg, sel, conf, after, falters
    return "faltered %d times" % falters, [], [], g_view, falters


def full_disenchant(c, kit, item):
    """Disenchant target (6), Proceed to Full Disenchant Confirmation (7), Destroy Item and Collect Yield (8):
    "You disenchant the <name>, recovering N Essence Shard(s)..." (:1927-1929). Returns that line or the refusal."""
    g = open_table(c, kit)
    g, texts = press_and_target(c, g, BTN_DISENCHANT_TARGET, item, lambda x: any(t.startswith("Disenchant: ") for t in all_text(c, x)))
    if not g:
        return "no Disenchant view: %s" % texts[-2:]
    n = len(c.gumps)
    c.reply_gump(g, BTN_FULL_DISENCHANT)
    g = c.wait_gump(lambda x: has(c, x, "Full Disenchant - FINAL CONFIRMATION"), 8, since=n)
    if not g:
        return "no Full Disenchant confirmation page"
    m = len(c.messages)
    c.reply_gump(g, BTN_APPLY_DISENCHANT)
    return wait_text(c, r"You disenchant the |no magic in this|cannot .*unravel|must be in your backpack", m, 8)


def farm_shards(a, c, kit, want, notes):
    """Earns Essence Shards up to `want` by disenchanting staff-made swords. Returns the balance the table shows."""
    for _ in range(5):
        g = open_table(c, kit)
        have = shards_of(c, g)
        c.reply_gump(g, 0)
        if have is None or have >= want:
            return have
        sword = give(a, c, "Longsword")
        set_on(a, sword, FARM_PROPS, c)
        props = c.opl(sword) or []
        if not any("life leech" in t.lower() for t in props):
            # The dotted [set did not land: a slayer is a direct property (BaseWeapon Slayer), 15 / 2 = 7 shards a sword.
            notes.append("dotted [set left no leech on the sword (%s); slayer swords instead" % props[:3])
            sword = give(a, c, "Longsword set Slayer Silver")
        notes.append(full_disenchant(c, kit, sword))
    g = open_table(c, kit)
    have = shards_of(c, g)
    c.reply_gump(g, 0)
    return have


def ensure_gold(a, c, need):
    """Pack gold up to `need` (CompactGoldHelper.GetTotalGold: pack, then bank). The status packet's gold is the pack's."""
    c.send(P.status_request(c.serial, 4))
    c.sleep(0.6)
    gold = c.status.get("gold") or 0
    if gold < need:
        give(a, c, "Gold %d" % (need - gold + 500))
    return gold


def knowledge(a, p, key, want, exact=False):
    """[ImbueKnowledge (ClusterFImbueKnowledge.cs:106, :357-374) on p: the admin's gump (ImbueFormulaBooks.cs:241-283)
    lists every key with + (1000 + 2i) and - (1001 + 2i) and the known tier at x=330 (:263-267); Next is 3 (:278).
    Presses + until the key knows at least `want` (with exact: - while above it, so "None" revokes everything).
    Returns the tiers seen, first to last."""
    n, m = len(a.gumps), len(a.messages)
    staff_target(a, "[ImbueKnowledge", p)
    g = gump_with(a, "Imbue knowledge: account", 10, since=n)
    if not g:
        raise AssertionError("[ImbueKnowledge opened no gump: %s" % a.texts_since(m)[-4:])
    seen = []
    for _ in range(14):
        g, r = page_find(a, g, lambda t: t == key, next_id=3, x=85)
        if not r:
            raise AssertionError("no row %r in [ImbueKnowledge" % key)
        tier = next((t for x, t in r["texts"] if x == 330), "?").replace(" (free)", "")
        seen.append(tier)
        have, goal = TIER.get(tier, -1), TIER[want]
        if have == goal or (not exact and have > goal) or have < 0:
            break
        plus = [v for x, v in r["buttons"] if x == 20]
        minus = [v for x, v in r["buttons"] if x == 50]
        btn = plus[0] if have < goal else minus[0]
        n = len(a.gumps)
        a.reply_gump(g, btn)
        g = a.wait_gump(lambda x: any("Imbue knowledge: account" in t for t in all_text(a, x)), 8, since=n)
        if not g:
            raise AssertionError("[ImbueKnowledge sent no gump after button %d" % btn)
    a.reply_gump(g, 0)
    return seen


def soul_forge_art(graphic):
    # ImbueSites.cs:26-27 (ServUO's soul forge IDs).
    return 0x4263 <= graphic <= 0x4272 or 0x4277 <= graphic <= 0x4286 or 17607 <= graphic <= 17610


def forges_near(c, r=2):
    """World items with soul forge art within r tiles (ImbuingTable.cs:132 ForgeRange = 2)."""
    x, y = c.pos[:2]
    return [s for s, it in c.items.items() if it.get("world") and soul_forge_art(it.get("graphic", 0))
            and abs(it.get("x", -9999) - x) <= r and abs(it.get("y", -9999) - y) <= r]


def step_away_from_forges(c):
    """Earlier runs leave their forge where the player stood: walk until none is within 2 tiles."""
    for d in ("e", "w", "n", "s", "ne", "sw", "nw", "se"):
        if not forges_near(c):
            return True
        c.walk(d, 6, run=True)
    return not forges_near(c)


def new_items(c, before, graphic):
    return [s for s, it in c.items.items() if s not in before and it.get("graphic") == graphic]


# -- P82-0 --------------------------------------------------------------------------------------------------------------

@check("P82-0", "Artificers' imbuing rework: a formula book read, staff-granted Bountiful imbued at Full anywhere and at "
                "Overcharged only by a hall soul forge, the hint line, the refusal and the tooltip")
def p82_0(ctx):
    # The notes' For Chase list (cc-P82 section 7): imbue rows at the table, read a formula book, place an
    # ArtificersSoulForge and try Overcharged, look at the custom property's tooltip line. The property is Bountiful
    # (ImbuingTable.cs:247: Full 15, Overcharged 22, armour/jewel/clothing, 5 SpiderCarapace, 15 shards, 8000 gold;
    # ladder rank 3, :177, so staff grants it: [ImbueKnowledge). The place rule: siteCaps Anywhere = Full, Forge =
    # Overcharged (ImbuingTable.cs:121-127), a forge within 2 tiles gives +2% (:130, :132; ImbueSites.cs:92-97).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    ev = []
    state, said = ensure_member(a, p)
    ev.append("order: %s %s" % (state, said))
    kit = toolkit_of(a, p)
    set_skill_to(a, p, "Imbuing", 140)
    ev.append("Imbuing %s" % (p.skills.get(SKILL["Imbuing"]) or {}).get("value"))

    # 1. A formula book: [add]ed, read from the pack (ImbueFormulaBooks.cs:52-66, :135-164). A different one each run.
    book_id = BOOK_IDS[SHARED.setdefault("p82_book", 0) % len(BOOK_IDS)]
    SHARED["p82_book"] += 1
    book = give(a, p, "ImbueFormulaBook " + book_id)
    book_props = p.opl(book) or []
    k = len(p.messages)
    p.dclick(book)
    book_said = wait_text(p, r"You study the |has already read the |mean nothing to you", k, 8)
    book_gone = bool(p.wait(lambda: book not in p.items, 4))
    # :49 "Artificers' rank <name>: N imbue formulae"; :159-161 "You study the <book>. Your account now knows: ..." (and
    # the book is consumed, :163); :154 "Your account has already read the <book>. You keep this."
    # (the 1049644 line arrives bracketed: "[Artificers' rank <name>: N imbue formulae]")
    book_ok = any(re.search(r"Artificers' rank .+: \d+ imbue formulae", t) for t in book_props) and bool(book_said) and (
        ("You study the" in book_said) == book_gone)
    ev.append("book %s: tooltip %r; %r; consumed %s" % (book_id, next((t for t in book_props if "formulae" in t), None), book_said, book_gone))

    # 2. Staff grants: Bountiful revoked to None, then granted tier by tier to Overcharged (ImbueFormulaBooks.cs:315-327).
    down = knowledge(a, p, "Bountiful", "None", exact=True)
    up = knowledge(a, p, "Bountiful", "Overcharged")
    know_ok = up[-1] == "Overcharged" and up == ["None", "Basic", "Refined", "Full", "Overcharged"][-len(up):]
    ev.append("[ImbueKnowledge Bountiful: %s then %s" % (down, up))

    # 3. Resources: 10 SpiderCarapace (two imbues), gold, 40 shards (15 + 22, ShardsFor :90-101).
    try:
        give(a, p, "SpiderCarapace 10")
    except AssertionError:
        for _ in range(10):      # no amount constructor: ten singles
            give(a, p, "SpiderCarapace")
    ensure_gold(a, p, 21000)
    notes = []
    shards = farm_shards(a, p, kit, 40, notes)
    ev.append("shards %s (farm: %s)" % (shards, notes))

    # 4. Away from any soul forge: the Full tier, the hint line, the Overcharged tier greyed, the refusal.
    away = step_away_from_forges(p)
    chest = give(a, p, "LeatherChest")
    g = view_item(p, kit, chest)
    texts = all_text(p, g)
    here_away = next((t for t in texts if "Here: up to" in t), None)                # :1020-1021
    hints = [t for t in texts if re.search(r": .+ locked\. Learn more: .+\.$", t)]    # ClusterFImbueKnowledge.cs:333
    g, r = page_find(p, g, lambda t: t == "Bountiful", x=44)
    row_away = [t for x, t in (r or {}).get("texts", []) if x == 300]                  # :1101-1102 "0 / 15"
    btn = r["buttons"][0][1] if r and r["buttons"] else None
    msg_full, sel_away, conf_full, g, falters1 = imbue_until(p, g, "Bountiful", TIER["Full"], btn)
    greyed = [t for t in sel_away if "needs a soul forge" in t]                        # :1214 "Overcharged (22): needs a soul forge"
    ingredient = [t for t in sel_away if t.startswith("Ingredient: ")]                 # :1158
    full_line = [t for t in sel_away if t.startswith("Full: set to 15")]              # :1236
    full_ok = bool(msg_full) and "with Bountiful = 15!" in msg_full                    # :1902
    cur_full = [t for x, t in (row(g, lambda t: t == "Bountiful", x=18) or {}).get("texts", []) if x == 340] if g else []  # :1032-1034
    ev.append("away from forges %s: %r; hint %r; row %s; select %s %s %s; Full -> %r (falters %d); now %s" % (
        away, here_away, hints[:1], row_away, ingredient, full_line, greyed, msg_full, falters1, cur_full))
    # The refusal: the tier's button is not drawn here, so the bot sends ConfirmBase + 4 itself (:1718-1726 lets it
    # through to the Confirm page) and Apply answers with the place rule (:1781-1784).
    refusal = None
    if g and btn is not None:
        refusal, _, _, g = imbue_attempt(p, g, "Bountiful", TIER["Overcharged"], btn)
    # Evidence only (cc-P224 trial): the hint line shows only while a tier is still locked, and every tier was just granted;
    # the Overcharged button is not drawn away from a forge, so its refusal cannot be pressed: the greyed row is the refusal.
    ev.append("Overcharged away from a forge (not pressable, the row is greyed) -> %r" % refusal)

    # 5. The hall soul forge beside the player (ImbueSites.cs:108-123): up to Overcharged, +2%, and the imbue at 22.
    forge = add_item_here(a, p, "ArtificersSoulForge")
    forge_props = (p.opl(forge) or []) if forge else []
    g = view_item(p, kit, chest)
    here_forge = next((t for t in all_text(p, g) if "Here: up to" in t), None)
    g, r = page_find(p, g, lambda t: t == "Bountiful", x=44)
    row_forge = [t for x, t in (r or {}).get("texts", []) if x == 300]                 # "15 / 22" with a button again
    if r and r["buttons"]:
        btn = r["buttons"][0][1]
    msg_over, sel_forge, conf_over, g, falters2 = imbue_until(p, g, "Bountiful", TIER["Overcharged"], btn) if g else ("no view", [], [], None, 0)
    over_line = [t for t in sel_forge if t.startswith("Overcharged: set to 22")]
    over_ok = bool(msg_over) and "with Bountiful = 22!" in msg_over
    cur_over = [t for x, t in (row(g, lambda t: t == "Bountiful", x=18) or {}).get("texts", []) if x == 340] if g else []
    if g:
        p.reply_gump(g, 0)
    ev.append("forge %s %s: %r; row %s; select %s; Overcharged -> %r (falters %d); now %s" % (
        "placed" if forge else "NOT placed", [t for t in forge_props if "Overcharged" in t or "soul forge" in t], here_forge,
        row_forge, over_line, msg_over, falters2, cur_over))

    # 6. The tooltip line: ImbueCustomStore.AddTooltip, ClusterFImbueEffects.cs:83, cliloc 1049644 "[~1_stuff~]".
    tip = [t for t in (p.opl(chest) or []) if "Bountiful" in t]
    tip_ok = any(re.search(r"Bountiful 22%", t) for t in tip)
    ev.append("tooltip %s" % tip)

    ok = (state in ("joined now", "already a member") and book_ok and know_ok and away
          and here_away is not None and "Here: up to Full" in here_away and "success" not in here_away
          and bool(greyed) and full_ok
          and here_forge is not None and "Here: up to Overcharged, +2% success" in here_forge and over_ok and tip_ok)
    return ok, "; ".join(ev)


# -- P173-1 -------------------------------------------------------------------------------------------------------------

@check("P173-1", "A Snow Elemental slayer war axe: listed as [ON], extracted to its essence, imbued onto a second weapon")
def p173_1(ctx):
    # The row "Slayer: Summer Wind (Snow Elemental)" (ArtificersImbueGump.cs:450; slayer rows are on/off, 15 shards,
    # 6000 gold, :641-685). The disenchant list says "<row>  [ON]" (:1340-1341); Extract (button 200 + index, :1388-1389)
    # then Confirm Extraction (9, :1491) clears the slayer (:2098) and delivers "Essence of <row>" (PropertyEssence.cs:42,
    # :2115-2116 into the satchel first) with "You extract <row> from WarAxe, receiving an Essence of <row> and 3
    # Essence Shard(s)." (:2125-2127). Imbuing it needs the account to know the Slayer family (ladder rank 3,
    # ImbuingTable.cs:175; staff grant) and the essence in the pack (:1826-1834); the weapon then carries pinned's
    # slayer title, cliloc 1060476 "snow elemental slayer" (the notes, SlayerEntry m_AosTitles).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    ev = []
    state, said = ensure_member(a, p)
    ev.append("order: %s" % state)
    kit = toolkit_of(a, p)
    set_skill_to(a, p, "Imbuing", 140)
    ev.append("Slayer family known: %s" % knowledge(a, p, "Slayer", "Basic"))
    # The axe through [AddToPack's own property list (pinned Add.cs:125-146: "set <prop> <value>" on the new item).
    axe = give(a, p, "WarAxe set Slayer SummerWind")
    axe_props = p.opl(axe) or []
    sword = give(a, p, "Longsword")
    ensure_gold(a, p, 7000)
    notes = []
    shards0 = farm_shards(a, p, kit, 15, notes)
    ev.append("axe tooltip has the slayer: %s; shards %s %s" % (any("snow elemental slayer" in t for t in axe_props), shards0, notes or ""))

    # Disenchant view of the axe.
    before = set(p.items)
    g = open_table(p, kit)
    g, texts = press_and_target(p, g, BTN_DISENCHANT_TARGET, axe, lambda x: any(t.startswith("Disenchant: ") for t in all_text(p, x)))
    if not g:
        return False, "; ".join(ev) + "; the axe opened no Disenchant view: %s" % texts[-3:]
    listed = [t for t in all_text(p, g) if t.startswith("Slayer: Summer Wind (Snow Elemental)")]
    r = row(g, lambda t: t.startswith("Extract: Slayer: Summer Wind (Snow Elemental)"))
    ev.append("disenchant list %s; extract row %s" % (listed, (r or {}).get("label")))
    if not r or not r["buttons"]:
        p.reply_gump(g, 0)
        return False, "; ".join(ev) + "; no Extract button for the slayer"
    n = len(p.gumps)
    p.reply_gump(g, r["buttons"][0][1])
    conf = p.wait_gump(lambda x: has(p, x, "Extract Property - Confirmation"), 8, since=n)
    if not conf:
        return False, "; ".join(ev) + "; no Extract Property confirmation page"
    k, n = len(p.messages), len(p.gumps)
    p.reply_gump(conf, BTN_APPLY_EXTRACT)
    extracted = wait_text(p, r"You extract |is not present on this item|must have the item", k, 8)
    after = p.wait_gump(None, 6, since=n)
    if after:
        p.reply_gump(after, 0)
    ess = p.wait(lambda: new_items(p, before, ESSENCE_GRAPHIC) or None, 6) or []
    ess_props = (p.opl(ess[0]) or []) if ess else []
    pack, satchel = p.backpack(), next(iter(pack_items(p, SATCHEL_GRAPHIC)), None)
    where = {pack: "backpack", satchel: "satchel"}.get(p.items.get(ess[0], {}).get("container")) if ess else None
    axe_after = p.opl(axe) or []
    axe_cleared = not any("snow elemental slayer" in t for t in axe_after)
    ev.append("extract -> %r; essence %s (%s) %s; axe still says slayer: %s" % (
        extracted, "seen" if ess else "NOT seen", where, [t for t in ess_props if "Essence" in t or "Slayer" in t][:2], not axe_cleared))

    # Imbue it onto the Longsword: the row's button, Apply (1001, a bool row's one tier, :1189-1190), Apply Imbue (5).
    g = view_item(p, kit, sword)
    msg, sel, conf_t, g, falters = imbue_until(p, g, "Slayer: Summer Wind (Snow Elemental)", TIER["Basic"])
    if g:
        p.reply_gump(g, 0)
    requires = [t for t in conf_t if "Requires: 1 Essence of Slayer: Summer Wind" in t]     # :1284-1285 (DescribeEssences)
    sword_props = p.opl(sword) or []
    imbued = any("snow elemental slayer" in t for t in sword_props)
    ev.append("imbue -> %r (falters %d); confirm said %s; enable line %s; Longsword tooltip says snow elemental slayer: %s" % (
        msg, falters, [t[:120] for t in requires][:1], [t for t in sel if t.startswith("Enable ")], imbued))

    ok = (bool(listed) and listed[0].endswith("[ON]") and bool(extracted) and extracted.startswith("You extract Slayer: Summer Wind (Snow Elemental)")
          and "Essence of Slayer: Summer Wind (Snow Elemental)" in extracted and axe_cleared
          and bool(ess) and any("Essence of Slayer: Summer Wind (Snow Elemental)" in t for t in ess_props)
          and bool(msg) and "with Slayer: Summer Wind (Snow Elemental) = 1!" in msg and imbued)
    return ok, "; ".join(ev)


# -- P176-1 -------------------------------------------------------------------------------------------------------------

@check("P176-1", "Unravel Container: a bag of 3 magic, 2 plain and 1 blessed item; the confirmation counts, the three "
                 "summary lines, the bag keeps the plain and blessed ones, the shards rise by the Ingredients line")
def p176_1(ctx):
    # ArtificersImbueGump.cs: button 12 "Unravel Container - every magic item in a bag..." (:968-969) says "Target a
    # container in your backpack to unravel the magic items in it." (:1635); the confirmation page (:1434-1463): "N
    # item(s) will be DESTROYED for their ingredients.", "M item(s) will be skipped and stay in the container: ...",
    # button 13 "Unravel N Item(s)"; then "You magically unravel the items!" (:2059), "Unraveled: N/M items" (:2062),
    # "Unraveled N item(s), skipped K." (:2063), "Ingredients: X Essence Shard(s)[, ...]." (:2066-2068). A blessed item is
    # skipped (CanUnravel :1961), a plain one has no magic (:1965). Magic here is Hit Life Leech 30 (row :308, 7 shards,
    # so 3 a sword at :1979) set through [set's dotted chain; the shards are the table header (:910).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    ev = []
    # By this point in the run the player's backpack holds a lot of accumulated loot, so a drop into a bag inside it
    # was refused with "That container cannot hold more weight." (1080016) and the item bounced back (cc-P224 second
    # session, 2026-10-21). Raise RawStr so the backpack's carry capacity has room for the six test items.
    set_on(a, p.serial, "RawStr 400", p)
    state, said = ensure_member(a, p)
    ev.append("order: %s" % state)
    kit = toolkit_of(a, p)
    bag = give(a, p, "Bag")

    def magic_sword(blessed):
        s = give(a, p, "Longsword set LootType Blessed" if blessed else "Longsword")
        set_on(a, s, "WeaponAttributes.HitLeechHits 30", p)
        props = p.opl(s) or []
        if not any("life leech" in t.lower() for t in props):
            # The dotted [set did not land: a slayer is a direct property, and a row of the table all the same (:641-685).
            s = give(a, p, "Longsword set Slayer Silver" + (" LootType Blessed" if blessed else ""))
            props = p.opl(s) or []
            ev.append("leech did not land, slayer sword instead: %s" % props[:3])
        return s, props

    magic = [magic_sword(False)[0] for _ in range(3)]
    blessed, bl_props = magic_sword(True)
    plain_items = [give(a, p, "Dagger"), give(a, p, "GoldRing")]
    stays = plain_items + [blessed]
    moved = []
    for s in magic + stays:
        # Every second drop was refused by the action delay in the first trial: pause between moves and retry once.
        ok_move = False
        for _ in range(3):
            p.lift_drop(s, 1, bag)
            ok_move = bool(p.wait(lambda: p.items.get(s, {}).get("container") == bag, 4))
            if ok_move:
                break
            p.sleep(1.5)
        p.sleep(1.0)
        moved.append(ok_move)
    p.dclick(bag)
    p.sleep(0.8)
    in_bag_before = sorted(p.contents(bag))
    ev.append("bag holds %d of 6 (moved %s); blessed tooltip %s" % (len(in_bag_before), moved, [t for t in bl_props if "Blessed" in t]))

    g = open_table(p, kit)
    shards_before = shards_of(p, g)
    k = len(p.messages)
    g, texts = press_and_target(p, g, BTN_UNRAVEL_TARGET, bag, lambda x: has(p, x, "Unravel Container - FINAL CONFIRMATION"))
    prompt = next((t for t in texts if t.startswith("Target a container in your backpack")), None)
    if not g:
        return False, "; ".join(ev) + "; no confirmation page after targeting the bag: %s" % p.texts_since(k)[-4:]
    ctexts = all_text(p, g)
    page = " | ".join(ctexts)
    go = re.search(r"(\d+) item\(s\) will be DESTROYED", page)
    stay = re.search(r"(\d+) item\(s\) will be skipped and stay in the container", page)
    unravel_btn = next((t for t in ctexts if re.match(r"^Unravel \d+ Item\(s\)$", t)), None)
    ev.append("prompt %r; confirmation: go %s, stay %s, button %r" % (prompt, go and go.group(1), stay and stay.group(1), unravel_btn))
    k, n = len(p.messages), len(p.gumps)
    p.reply_gump(g, BTN_APPLY_UNRAVEL)
    wait_text(p, r"^Ingredients: |^Unraveled \d+ item\(s\), skipped", k, 10)
    p.sleep(0.8)
    lines = p.texts_since(k)
    l_magic = next((t for t in lines if t == "You magically unravel the items!"), None)
    l_count = next((t for t in lines if re.match(r"^Unraveled: \d+/\d+ items$", t)), None)
    l_skip = next((t for t in lines if re.match(r"^Unraveled \d+ item\(s\), skipped \d+\.$", t)), None)
    l_ingr = next((t for t in lines if t.startswith("Ingredients: ")), None)
    after = p.wait_gump(lambda x: TABLE_TITLE in all_text(p, x), 8, since=n)
    shards_after = shards_of(p, after) if after else None
    if after:
        p.reply_gump(after, 0)
    x = re.match(r"^Ingredients: (\d+) Essence Shard\(s\)", l_ingr or "")
    x = int(x.group(1)) if x else None
    p.dclick(bag)
    p.sleep(0.8)
    left = p.contents(bag)
    magic_gone = [s not in left for s in magic]
    stayed = [s in left for s in stays]
    ev.append("summary %r / %r / %r / %r; shards %s -> %s; magic gone %s; plain+blessed still in the bag %s" % (
        l_magic, l_count, l_skip, l_ingr, shards_before, shards_after, magic_gone, stayed))

    ok = (len(in_bag_before) == 6 and go is not None and go.group(1) == "3" and stay is not None and stay.group(1) == "3"
          and unravel_btn == "Unravel 3 Item(s)" and l_magic is not None and l_count == "Unraveled: 3/6 items"
          and l_skip == "Unraveled 3 item(s), skipped 3." and x is not None and x > 0
          and shards_before is not None and shards_after is not None and shards_after - shards_before == x
          and all(magic_gone) and all(stayed))
    return ok, "; ".join(ev)
