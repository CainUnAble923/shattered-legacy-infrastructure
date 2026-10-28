"""cc-P224: P100-1 to P100-5, the Visual Builder (staff). Code under test: server/customizations/ClusterFVisualBuilder.cs
(the gump at :752-852, the tools :115-330, the command :414-485). P100-6 (a restart) is in checks_p224_restart.py.

Everything happens beside New Haven's square, in a small box the admin [go]es to. Each check starts and ends with
[VBuild undo until "Nothing of yours to undo.", so it finds the corner clean and leaves it so (a check that throws
still cleans up), and deletes any Static it [add]ed for the world-mode case.

The palette is a singleton DynamicGump that the server re-sends after every button, target and cancel, so a reply must
go to the NEWEST instance (latest_builder), never to one taken before an action.
"""
import re

from .checks import check, all_text, buttons
from .checks_p224 import wait_text

TITLE = "Visual Builder (after Kamras's Architect Edition)"   # ClusterFVisualBuilder.cs:782
B_SEARCH, B_APPLY = 1, 4                                      # :760
B_PAINT, B_FILL, B_ERASE, B_WIPE, B_HUE = 10, 11, 12, 13, 14   # :761
B_NUDGE_XPLUS = 21                                             # :762 (20 + 1: k=1, d=+1 on x)
B_UNDO = 30                                                    # :763
ENTRY_SEARCH, ENTRY_ID, ENTRY_HUE, ENTRY_Z = 0, 1, 2, 3        # :786, :812-814
BENCH = 0x0B2D                                                 # a wooden bench tile
FLOOR = 0x0519                                                 # "stone pavers Dark"
HUE = 1157

# The square: new characters start a tile from the board (T-039; cc-P678: HavenSpots.Start in the author's Haven, the
# board at 3633,2585). The work box sits east of it, well inside the admin's view.
SQUARE = (3634, 2586, 0)
BOX_DX, BOX_DY = 6, 4


def is_builder(a, g):
    return any(t == TITLE for t in all_text(a, g))


def latest_builder(a, timeout=4):
    """The newest Visual Builder gump the server has sent (it re-opens after every action)."""
    def f():
        for g in reversed(a.gumps):
            if g.get("closed"):
                return None          # the newest palette was closed by the bot: open it again
            if is_builder(a, g):
                return g
        return None
    a.sleep(0.4)
    return a.wait(f, timeout)


def open_builder(a):
    n = len(a.gumps)
    a.command("[VBuild", 0.3)
    g = a.wait_gump(lambda g: is_builder(a, g), 8, since=n)
    if not g:
        raise AssertionError("[VBuild opened no Visual Builder gump")
    return g


def builder(a):
    return latest_builder(a) or open_builder(a)


def go_square(a):
    a.command("[go %d %d %d" % SQUARE, 1.0)
    x, y, z = a.pos[:3]
    return x + BOX_DX, y + BOX_DY, z


def box_items(a, x0, y0, side, pad=0):
    """World items the admin sees inside the box [x0, x0+side) x [y0, y0+side), padded by pad tiles."""
    out = {}
    for s, it in a.items.items():
        if it.get("world") and x0 - pad <= it.get("x", -1) < x0 + side + pad and y0 - pad <= it.get("y", -1) < y0 + side + pad:
            out[s] = it
    return out


def apply_settings(a, item_id, hue, z=0):
    g = builder(a)
    n = len(a.gumps)
    a.reply_gump(g, B_APPLY, texts=[(ENTRY_ID, "0x%04X" % item_id), (ENTRY_HUE, str(hue)), (ENTRY_Z, str(z))])
    g2 = a.wait_gump(lambda g: is_builder(a, g), 8, since=n)
    if not g2:
        raise AssertionError("Apply brought no gump back")
    shown = next((t for t in all_text(a, g2) if t.startswith("0x") and len(t) == 6 and t.upper() == "0x%04X" % item_id), None)
    return g2, shown


def two_corners(a, button, x1, y1, x2, y2, z):
    """Fill box or Wipe box: the button on the newest palette, then the two corners (BoundingBoxPicker). Returns the
    messages since."""
    g = builder(a)
    n = len(a.messages)
    a.reply_gump(g, button)
    if not a.wait_target(8):
        raise AssertionError("button %d gave no first corner cursor" % button)
    a.target_xyz(x1, y1, z)
    if not a.wait_target(8):
        raise AssertionError("button %d gave no second corner cursor" % button)
    a.target_xyz(x2, y2, z)
    a.sleep(1.2)
    return a.texts_since(n)


def paint_two(a, bx, by, z):
    """Paint at (bx, by) and (bx + 1, by), then Escape. Returns the serials of the pieces that appeared there."""
    g = builder(a)
    before = set(a.items)
    a.reply_gump(g, B_PAINT)
    if not a.wait_target(8):
        raise AssertionError("Paint gave no target cursor")
    a.target_xyz(bx, by, z)
    if not a.wait_target(8):
        raise AssertionError("Paint did not repeat its cursor after the first piece")
    a.target_xyz(bx + 1, by, z)
    a.wait_target(4)
    a.cancel_target()          # OnTargetCancel re-opens the palette (:966)
    return a.wait(lambda: [s for s, it in a.items.items() if s not in before and it.get("world")
                           and it.get("x") in (bx, bx + 1) and it.get("y") == by] or None, 6) or []


def close_builder(a):
    g = latest_builder(a, 1)
    if g:
        a.reply_gump(g, 0)
        g["closed"] = True


def undo_all(a, limit=40):
    """[VBuild undo until nothing is left. Returns the messages of each undo."""
    said = []
    for _ in range(limit):
        n = len(a.messages)
        a.command("[VBuild undo", 0.6)
        t = [x for x in a.texts_since(n) if x.startswith("Undone") or x.startswith("Nothing of yours")]
        said.append(t[0] if t else "(no reply)")
        if not t or t[0].startswith("Nothing"):
            break
    return said


def place_set(a, bx, by, z):
    """Two painted benches and a 3 x 3 floor of pavers, all in HUE; returns (serials placed, the fill's message)."""
    open_builder(a)
    apply_settings(a, BENCH, HUE)
    benches = paint_two(a, bx, by, z)
    apply_settings(a, FLOOR, HUE)
    before = set(a.items)
    said = two_corners(a, B_FILL, bx, by + 2, bx + 2, by + 4, z)
    floors = a.wait(lambda: [s for s, it in a.items.items() if s not in before and it.get("world") and it.get("graphic") == FLOOR] or None, 6) or []
    return benches + floors, next((t for t in said if t.startswith("Filled ")), None)


@check("P100-1", "[VBuild opens the Visual Builder: title, Paint, Fill box (20), Wipe box (50) and Undo")
def p100_1(ctx):
    # ClusterFVisualBuilder.cs:782 (title), :819-822 (Paint, Fill box (20), Erase, Wipe box (50), Hue an item),
    # :833-834 (Undo (n)); MaxFillSide 20 and MaxWipeSide 50 at :35-36.
    a = ctx.ensure_admin()
    go_square(a)
    g = open_builder(a)
    texts = all_text(a, g)
    bs = buttons(g)
    want = [TITLE, "Paint", "Fill box (20)", "Wipe box (50)"]
    missing = [w for w in want if w not in texts]
    undo = [t for t in texts if re.match(r"^Undo \(\d+\)$", t)]
    have_buttons = all(b in bs for b in (B_PAINT, B_FILL, B_WIPE, B_UNDO))
    a.reply_gump(g, 0)
    ok = not missing and bool(undo) and have_buttons
    return ok, "title and tools present: %s (missing %s); %s; buttons 10/11/13/30 present %s; %d lines" % (
        not missing, missing or "none", undo[0] if undo else "no Undo label", have_buttons, len(texts))


@check("P100-2", "Search bench, Paint two with a hue, Fill box 3 x 3 (Filled 9), 21 x 21 refused, Nudge moves them",
       kind="BOT-PARTIAL", left="P100-2: the painted pieces and the hue look right on screen")
def p100_2(ctx):
    # Search: BSearch with entry 0 (:786-788), the label "{n} found, page" (:789). Paint: PaintTarget repeats until
    # Escape (:941-968). Fill: "Filled {n} item(s). Undo removes them." (:979-980); over 20: CheckBox (:97-102) ->
    # "Refused: the box is 21 x 21 tiles; the most a fill takes is 20 x 20." (:979). Nudge X+ is button 21 ->
    # "Moved {n} item(s)." (:896-901).
    a = ctx.ensure_admin()
    bx, by, z = go_square(a)
    undo_all(a)
    try:
        g = open_builder(a)
        n = len(a.gumps)
        a.reply_gump(g, B_SEARCH, texts=[(ENTRY_SEARCH, "bench")])
        g = a.wait_gump(lambda g: is_builder(a, g), 8, since=n)
        found = next((t for t in all_text(a, g) if re.match(r"^\d+ found, page", t)), None) if g else None
        found_n = int(found.split()[0]) if found else 0
        apply_settings(a, BENCH, HUE)
        painted = paint_two(a, bx, by, z)
        pgraphics = sorted(set(a.items[s].get("graphic") for s in painted))
        hues = sorted(set(a.items[s].get("hue") for s in painted))
        apply_settings(a, FLOOR, HUE)
        before = set(a.items)
        said = two_corners(a, B_FILL, bx, by + 2, bx + 2, by + 4, z)
        filled_msg = next((t for t in said if t.startswith("Filled ")), None)
        filled = a.wait(lambda: [s for s, it in a.items.items() if s not in before and it.get("world") and it.get("graphic") == FLOOR] or None, 6) or []
        fhues = sorted(set(a.items[s].get("hue") for s in filled))
        before = set(a.items)
        said21 = two_corners(a, B_FILL, bx, by + 2, bx + 20, by + 22, z)
        refused = next((t for t in said21 if t.startswith("Refused: ")), None)
        placed21 = [s for s, it in a.items.items() if s not in before and it.get("world")]
        xs_before = sorted(a.items[s].get("x") for s in filled if s in a.items)
        g = builder(a)
        n = len(a.messages)
        a.reply_gump(g, B_NUDGE_XPLUS)
        moved_msg = wait_text(a, r"^Moved \d+ item", n, 6)
        nudged = a.wait(lambda: sorted(a.items[s].get("x") for s in filled if s in a.items) == [x + 1 for x in xs_before] or None, 6)
        close_builder(a)
    finally:
        undone = undo_all(a)
    ok = found_n > 0 and len(painted) == 2 and pgraphics == [BENCH] and hues == [HUE] and filled_msg == "Filled 9 item(s). Undo removes them." \
        and len(filled) == 9 and fhues == [HUE] and refused == "Refused: the box is 21 x 21 tiles; the most a fill takes is 20 x 20." \
        and not placed21 and moved_msg == "Moved 9 item(s)." and bool(nudged)
    return ok, "search 'bench': %s; painted %d bench(es) (graphics %s) hue %s; fill: %r (%d pavers, hue %s); 21x21: %r (%d placed); nudge: %r, the nine moved x+1: %s; cleanup %s" % (
        found, len(painted), [hex(g_) for g_ in pgraphics], hues, filled_msg, len(filled), fhues, refused, len(placed21), moved_msg, bool(nudged), undone[:5])


@check("P100-3", "Wipe box over the corner removes what the session placed and leaves the town's own items")
def p100_3(ctx):
    # Wipe (:179-205): only CanModify items (:81-95: a Static this session placed, in protected mode) are recorded and
    # deleted; the message is "Wiped {n} item(s). Undo puts back them." (:979-980).
    a = ctx.ensure_admin()
    bx, by, z = go_square(a)
    undo_all(a)
    a.sleep(1.0)
    try:
        town_before = set(box_items(a, bx - 2, by - 2, 9))
        placed, fill_msg = place_set(a, bx, by, z)
        said = two_corners(a, B_WIPE, bx - 2, by - 2, bx + 6, by + 6, z)
        wiped_msg = next((t for t in said if t.startswith("Wiped ")), None)
        gone = a.wait(lambda: all(s not in a.items for s in placed) or None, 6)
        town_after = set(box_items(a, bx - 2, by - 2, 9))
        close_builder(a)
    finally:
        undone = undo_all(a)
    ok = len(placed) == 11 and wiped_msg == "Wiped %d item(s). Undo puts back them." % len(placed) and bool(gone) and town_after == town_before
    return ok, "placed %d (%s); wipe said %r; all placed gone %s; town items in the box before %d, after %d, same set %s; cleanup %s" % (
        len(placed), fill_msg, wiped_msg, bool(gone), len(town_before), len(town_after), town_after == town_before, undone[:5])


@check("P100-4", "Undo puts the wiped items back, then undoes the fill and the paints until the corner is as it was")
def p100_4(ctx):
    # Undo (:254-305): the newest step first; a wipe's records come back as new Statics (:275-291); the command says
    # "Undone: {n} item(s). {m} step(s) left." (:437-438) and "Nothing of yours to undo." when empty. Each paint is its
    # own step (:115-130), so 2 paints + the fill + the wipe = 4 steps, and 3 are left once the wipe is undone.
    a = ctx.ensure_admin()
    bx, by, z = go_square(a)
    undo_all(a)
    a.sleep(1.0)

    def picture():
        return sorted((it.get("graphic"), it.get("hue"), it.get("x"), it.get("y")) for it in box_items(a, bx - 2, by - 2, 9).values())
    start = picture()
    try:
        placed, _ = place_set(a, bx, by, z)
        after_place = picture()
        two_corners(a, B_WIPE, bx - 2, by - 2, bx + 6, by + 6, z)
        a.sleep(0.8)
        close_builder(a)
        n = len(a.messages)
        a.command("[VBuild undo", 0.8)
        first = wait_text(a, r"^(Undone|Nothing of yours)", n, 6)
        a.sleep(1.0)
        restored = picture()
    finally:
        rest = undo_all(a)
    a.sleep(1.0)
    end = picture()
    ok = len(placed) == 11 and first == "Undone: 11 item(s). 3 step(s) left." and restored == after_place and end == start \
        and rest[-1].startswith("Nothing of yours")
    return ok, "placed %d; first undo: %r; corner after it equals the placed picture: %s; then %s; corner back to its start: %s (%d items)" % (
        len(placed), first, restored == after_place, rest, end == start, len(start))


@check("P100-5", "World mode (Administrator): a wipe takes a loose Static the builder did not place, undo puts it back")
def p100_5(ctx):
    # SetWorldMode (:65-74) is WorldModeAccess (Administrator) only; "[VBuild world on" says "World mode ON: ..." and
    # off says "World mode off: erase and wipe only touch what this session placed." (:466-470). In protected mode a
    # loose Static is not CanModify (:81-95), so a wipe says "Wiped 0 item(s)"; in world mode it is recorded and
    # removed; undo re-creates it (:275-291).
    a = ctx.ensure_admin()
    bx, by, z = go_square(a)
    undo_all(a)
    a.command("[VBuild world off", 0.6)
    before = set(a.items)
    a.command("[add Static 2713", 0.2)
    if not a.wait_target(8):
        raise AssertionError("[add Static gave no target cursor")
    a.target_xyz(bx + 1, by + 1, z)
    loose = a.wait(lambda: next((s for s, it in a.items.items() if s not in before and it.get("world") and it.get("graphic") == 2713), None), 8)
    if not loose:
        return False, "the loose Static never appeared"
    try:
        open_builder(a)
        said_protected = two_corners(a, B_WIPE, bx - 1, by - 1, bx + 3, by + 3, z)
        w0 = next((t for t in said_protected if t.startswith("Wiped ")), None)
        still = loose in a.items
        n = len(a.messages)
        a.command("[VBuild world on", 0.8)
        on_msg = next((t for t in a.texts_since(n) if t.startswith("World mode ON")), None)
        said_world = two_corners(a, B_WIPE, bx - 1, by - 1, bx + 3, by + 3, z)
        w1 = next((t for t in said_world if t.startswith("Wiped ")), None)
        gone = a.wait(lambda: loose not in a.items or None, 6)
        close_builder(a)
        n = len(a.messages)
        a.command("[VBuild undo", 0.8)
        undo_msg = wait_text(a, r"^(Undone|Nothing of yours)", n, 6)
        back = a.wait(lambda: next((s for s, it in a.items.items() if it.get("world") and it.get("graphic") == 2713
                                    and it.get("x") == bx + 1 and it.get("y") == by + 1), None), 6)
        n = len(a.messages)
        a.command("[VBuild world off", 0.8)
        off_msg = next((t for t in a.texts_since(n) if t.startswith("World mode")), None)
    finally:
        a.command("[VBuild world off", 0.3)
        undo_all(a)
        # The Static is not the session's (or was put back as a non-session item): delete it by hand.
        for s_ in [s for s, it in a.items.items() if it.get("world") and it.get("graphic") == 2713 and it.get("x") == bx + 1 and it.get("y") == by + 1]:
            a.command("[Delete", 0.2)
            if a.wait_target(6):
                a.target_obj(s_)
            a.sleep(0.5)
    # The box sits on the New Haven square, which has its own town Statics, so a world-mode wipe removes the loose
    # Static the check placed AND any town Statics in the box: the count is >= 1, not exactly 1 (cc-P224 second
    # session, seen 2026-10-21: "Wiped 3 item(s)" where the square's pavers fall in range). The test is the behaviour,
    # not the global count: protected wipe leaves the loose Static ("Wiped 0", still there); world wipe removes it
    # (gone) and reports some N >= 1; undo puts that same N back, the loose Static among them (back).
    mw = re.match(r"^Wiped (\d+) item\(s\)\. Undo puts back them\.$", w1 or "")
    n_wiped = int(mw.group(1)) if mw else 0
    ok = w0 == "Wiped 0 item(s). Undo puts back them." and still and bool(on_msg) and n_wiped >= 1 \
        and bool(gone) and undo_msg == ("Undone: %d item(s). 0 step(s) left." % n_wiped) and bool(back) \
        and off_msg == "World mode off: erase and wipe only touch what this session placed."
    return ok, "protected wipe: %r (loose still there %s); %r; world wipe: %r (gone %s); undo: %r (back %s); %r" % (
        w0, still, (on_msg or "")[:40], w1, bool(gone), undo_msg, bool(back), off_msg)
