"""The checks (cc-P91). One function per play-test id: setup as admin, the action as a player (unless the test is
about staff), and an assertion on what the server actually sent back. Each returns (passed, evidence).

The expected strings are read from the code under test (cited per check) and from the play test's own wording in
testing/play-tests.json; where the two differ the check says which it used.
"""
import re
import time
from collections import namedtuple

from . import packets as P

Check = namedtuple("Check", "id title kind left_for_chase fn")
REGISTRY = []
SHARED = {}            # results one check hands to another (P64-1's runs feed P64-2)

VERSION_UNDER_TEST = None   # set from the bulletin or [version


def check(cid, title, kind="BOT", left=""):
    def deco(fn):
        REGISTRY.append(Check(cid, title, kind, left, fn))
        return fn
    return deco


# -- helpers --------------------------------------------------------------------------------------------------------

def entries(g):
    """The layout's text-bearing commands, in order: (kind, x, y, text). 'text x y hue idx' and 'croppedtext ...' name a
    line; 'htmlgump x y w h idx ...' names a line; 'xmfhtmlgump x y w h cliloc ...' names a cliloc."""
    out = []
    for m in re.finditer(r"\{\s*([a-z]+)\s+([^}]*)\}", g["layout"]):
        kind, args = m.group(1), m.group(2).split()
        try:
            if kind == "text" and len(args) >= 4:
                out.append(("text", int(args[0]), int(args[1]), g["lines"][int(args[3])]))
            elif kind == "croppedtext" and len(args) >= 6:
                out.append(("text", int(args[0]), int(args[1]), g["lines"][int(args[5])]))
            elif kind == "htmlgump" and len(args) >= 5:
                out.append(("html", int(args[0]), int(args[1]), g["lines"][int(args[4])]))
            elif kind == "button" and len(args) >= 7:
                out.append(("button", int(args[0]), int(args[1]), int(args[6])))
        except (ValueError, IndexError):
            pass
    return out


def plain(s):
    return re.sub(r"<[^>]+>", "", s or "").strip()


def all_text(c, g):
    return [plain(t) for t in c.gump_text(g)]


def buttons(g):
    return [e[3] for e in entries(g) if e[0] == "button"]


def history_pages(c, staff=False):
    """[version: the PatchHistoryGump, paged with Next (button 2, PatchHistoryGump.cs:34) to the last page.
    Returns the list of pages' texts, in order."""
    n = len(c.gumps)
    c.command("[version", 0.3)
    g = c.wait_gump(lambda g: any("patch notes" in t for t in all_text(c, g)), 10, since=n)
    if not g:
        raise AssertionError("[version opened no patch notes gump")
    pages = [all_text(c, g)]
    last = g
    # Walk to the last page, however many there are: the history was 25 pages when this was written and 62 on
    # 2026-10-20, when a cap of 40 made five checks fail (cc-P224). The counter's "of N" bounds the loop, 400 at most.
    counter = next((t for t in pages[0] if re.match(r"^Page \d+ of \d+$", t)), "Page 1 of 1")
    total = min(int(counter.split()[-1]), 400)
    for _ in range(total + 2):
        if 2 not in buttons(last):
            break
        n = len(c.gumps)
        c.reply_gump(last, 2)
        g = c.wait_gump(None, 10, since=n)
        if not g:
            raise AssertionError("Next brought no page")
        pages.append(all_text(c, g))
        last = g
    SHARED.setdefault("last_history_gump", {})[c.label] = last
    return pages


def version_blocks(pages):
    """Each version's heading and the '- ' lines under it, across pages (continued headings merged)."""
    blocks, cur = {}, None
    order = []
    for page in pages:
        for t in page:
            m = re.match(r"^Shattered Legacy (\d{4}\.\d{2}\.\d{2}(?:\.\d+)?)( \(continued\))?$", t)
            if m:
                cur = m.group(1)
                if cur not in blocks:
                    blocks[cur] = {"lines": [], "sections": []}
                    order.append(cur)
                continue
            if cur is None:
                continue
            if t.startswith("- "):
                blocks[cur]["lines"].append((blocks[cur]["sections"][-1] if blocks[cur]["sections"] else "", t))
            elif t in ("Added", "Changed", "Fixed", "Staff (players do not see this list)") or t.endswith("(continued)"):
                blocks[cur]["sections"].append(t.replace(" (continued)", ""))
    return order, blocks


def near(admin, who):
    """Staff targeting has a range ("That is too far away", cliloc 500446): the admin goes to the character first
    ([go <serial>, UOContent/Commands/Handlers.cs:374-390)."""
    admin.command("[go 0x%X" % who.serial, 1.0)


def give(admin, who, item_type):
    """Admin: [AddToPack <type>, then target the character (Commands.cs:450-475, AddToPackCommand). Returns the new
    item's serial, from the 0x25 the owner receives."""
    pack = who.backpack()
    before = {s: it.get("amount") for s, it in who.items.items() if it.get("container") == pack}
    near(admin, who)
    admin.command("[AddToPack " + item_type, 0.2)
    if not admin.wait_target(8):
        raise AssertionError("[AddToPack gave no target cursor")
    admin.target_obj(who.serial)
    def new_item():
        # A new item, or a stackable pile in the pack that grew (stackables merge on the way in).
        now = [s for s, it in who.items.items() if it.get("container") == pack
               and (s not in before or (it.get("amount") or 0) > (before[s] or 0))]
        return now[0] if now else None
    s = who.wait(new_item, 8)
    if not s:
        raise AssertionError("%s never reached %s's pack" % (item_type, who.label))
    return s


def staff_target(admin, command, who):
    near(admin, who)
    admin.command(command, 0.2)
    if not admin.wait_target(8):
        raise AssertionError("%s gave no target cursor" % command)
    admin.target_obj(who.serial)
    admin.sleep(0.6)


# -- P64-1 / P64-2: the login bulletin -----------------------------------------------------------------------------

@check("P64-1", "Fresh account login: bulletin with the current version, All patch notes, no wiki button")
def p64_1(ctx):
    # ClusterFBulletin.cs:376-512: title "Shattered Legacy <version>" (:436), All patch notes button id 2 (:378, :468),
    # Dismiss button 1 (:511). Shown once per account per version, so each run needs an account's first login.
    unused = [a for a in ctx.pool_rest if a not in ctx.first_logins]
    if not unused:
        raise RuntimeError("no pool account left for a first login")
    c = ctx.fresh(race="human", female=False, label="fresh")
    try:
        seen = ctx.first_logins.get(c.account)
        if seen is None:
            raise RuntimeError("this was not the account's first login")
        bulletins = [g for g in seen["gumps"] if any(re.match(r"^Shattered Legacy \d{4}\.", t) for t in all_text(c, g))]
        if not bulletins:
            SHARED.setdefault("p64_2", []).append((False, "no bulletin, so no All patch notes to press"))
            return False, "no bulletin gump on the account's first login (%d gumps: %s)" % (
                len(seen["gumps"]), [all_text(c, g)[:2] for g in seen["gumps"]])
        g = bulletins[0]
        texts = all_text(c, g)
        title = next(t for t in texts if re.match(r"^Shattered Legacy \d{4}\.", t))
        global VERSION_UNDER_TEST
        VERSION_UNDER_TEST = title.split()[-1]
        has_btn = 2 in buttons(g) and any("All patch notes" in t for t in texts)
        wiki = [t for t in texts if "wiki" in t.lower()]
        ok = has_btn and not wiki
        ev = "title %r; All patch notes button %s; wiki text %s" % (title, "yes" if has_btn else "NO", wiki or "none")
        # P64-2 on the same character: press All patch notes, expect the history; then relog, expect no bulletin.
        n = len(c.gumps)
        c.reply_gump(g, 2)
        h = c.wait_gump(lambda x: any("patch notes" in t and "Shattered Legacy" in t for t in all_text(c, x)), 10, since=n)
        acct, slot = c.account, c.chars.index("") if "" in c.chars else 0
        c.close()
        from .client import Client
        c2 = Client(acct, ctx.secrets["playerPassword"], ctx.tr, ctx.cliloc, "relog")
        chars = c2.login()
        c2.play(chars.index(c.name) if c.name in chars else 0)
        c2.sleep(3.0)
        again = [x for x in c2.gumps if any(re.match(r"^Shattered Legacy \d{4}\.", t) for t in all_text(c2, x))]
        c2.account = acct
        ctx.done(c2)
        SHARED.setdefault("p64_2", []).append((bool(h) and not again,
            "history gump after All patch notes: %s; bulletin on relog: %s" % ("yes" if h else "NO", "YES" if again else "none")))
        return ok, ev
    finally:
        if c.closed is None:
            ctx.done(c)


@check("P64-2", "All patch notes opens the history; a relog shows no bulletin")
def p64_2(ctx):
    runs = SHARED.get("p64_2", [])
    if not runs:
        raise RuntimeError("P64-1 did not run first")
    ok, ev = runs.pop(0)
    return ok, ev + " (observed during the matching P64-1 run)"


# -- [version history (PatchHistoryGump.cs) ----------------------------------------------------------------------

@check("P64-3", "[version: the current version first, with (you are here)", kind="BOT-PARTIAL", left="P64-3: the entry shows gold")
def p64_3(ctx):
    p = ctx.ensure_player()
    pages = history_pages(p)
    first = pages[0]
    heads = [t for t in first if t.startswith("Shattered Legacy 20")]
    here = "(you are here)" in first
    ok = bool(heads) and here
    return ok, "first heading %r; (you are here) on page 1: %s" % (heads[0] if heads else None, here)


@check("P64-4", "[version pages: Next to Page N of N, last version 2026.10.01, Previous goes back")
def p64_4(ctx):
    p = ctx.ensure_player()
    pages = history_pages(p)
    counter = [t for t in pages[-1] if re.match(r"^Page \d+ of \d+$", t)]
    order, _ = version_blocks(pages)
    last = SHARED["last_history_gump"][p.label]
    n = len(p.gumps)
    if 1 not in buttons(last) and len(pages) > 1:
        return False, "the last page has no Previous button"
    p.reply_gump(last, 1)
    back = p.wait_gump(None, 10, since=n)
    back_counter = [t for t in all_text(p, back) if re.match(r"^Page \d+ of \d+$", t)] if back else []
    m = re.match(r"^Page (\d+) of (\d+)$", counter[0]) if counter else None
    ok = bool(m) and m.group(1) == m.group(2) and order and order[-1] == "2026.10.01" and back_counter == ["Page %d of %s" % (int(m.group(1)) - 1, m.group(2))]
    return ok, "last page %s; last version %s; after Previous %s" % (counter, order[-1] if order else None, back_counter)


@check("P58-0", "[version as a player: 2026.10.02 to 2026.10.05 sections, no Staff lines", kind="BOT-PARTIAL", left="P58-0: the patch-note wording reads right")
def p58_0(ctx):
    p = ctx.ensure_player()
    pages = history_pages(p)
    order, blocks = version_blocks(pages)
    want = [v for v in order if re.match(r"^2026\.10\.0[2-5]", v)]
    staff = any("Staff (players do not see this list)" in t for page in pages for t in page)
    need = ["2026.10.02", "2026.10.03", "2026.10.05"]
    missing = [v for v in need if v not in order]
    ok = not missing and not staff
    return ok, "versions 10.02-10.05 shown: %s; missing %s; Staff list visible to a player: %s" % (want, missing or "none", staff)


@check("P61-5", "[version history holds 2026.10.05.2 with its four lines")
def p61_5(ctx):
    p = ctx.ensure_player()
    order, blocks = version_blocks(history_pages(p))
    b = blocks.get("2026.10.05.2")
    n = len(b["lines"]) if b else 0
    return n == 4, "2026.10.05.2 present: %s, %d player lines (CHANGELOG at the code under test: 4)" % (bool(b), n)


@check("P64-5", "The 2026.10.05.2 line ends 'The chance to make the item is unchanged.'", kind="BOT-PARTIAL", left="P64-5: it wraps onto four rows")
def p64_5(ctx):
    p = ctx.ensure_player()
    order, blocks = version_blocks(history_pages(p))
    b = blocks.get("2026.10.05.2", {"lines": []})
    hit = [t for _, t in b["lines"] if t.endswith("The chance to make the item is unchanged.")]
    return bool(hit), "matching line: %r" % (hit[0][-90:] if hit else None)


@check("P71-7", "[version history holds 2026.10.10 with five lines")
def p71_7(ctx):
    p = ctx.ensure_player()
    order, blocks = version_blocks(history_pages(p))
    b = blocks.get("2026.10.10")
    n = len(b["lines"]) if b else 0
    return n == 5, "2026.10.10 present: %s, %d player lines (CHANGELOG at the code under test: 5)" % (bool(b), n)


@check("P64-6", "Staff list: shown to staff, hidden from players", kind="BOT-PARTIAL", left="P64-6: the staff list shows lavender")
def p64_6(ctx):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    label = "Staff (players do not see this list)"
    staff = any(label in t for page in history_pages(a) for t in page)
    player = any(label in t for page in history_pages(p) for t in page)
    return staff and not player, "admin sees %r: %s; player sees it: %s" % (label, staff, player)


# -- skills and diagnostics ------------------------------------------------------------------------------------------

@check("P66-7", "[ClusterFSkillGain reports the rate and 'past stock ceilings on'")
def p66_7(ctx):
    # ClusterFSkillGain.cs:113-115, Describe(): "rate enabled, N chance attempt(s), Nx gain amount; past stock ceilings on".
    a = ctx.ensure_admin()
    n = len(a.messages)
    a.command("[ClusterFSkillGain", 1.0)
    m = a.find_text(r"past stock ceilings", since=n, timeout=5)
    if not m:
        return False, "no reply naming past stock ceilings: %s" % a.texts_since(n)[-3:]
    t = m["text"]
    ok = "past stock ceilings on" in t and re.search(r"\d+(\.\d+)?x gain amount", t) is not None
    return ok, "reply: %r" % t


# -- equipment -------------------------------------------------------------------------------------------------------

def try_equip(c, serial, wait=2.0):
    """Lift the item, then 0x13 equip on oneself (IncomingItemPackets.cs:35, :44)."""
    n = len(c.messages)
    c.send(P.lift(serial, 1))
    c.sleep(0.4)
    c.send(P.equip(serial, 0, c.serial))
    c.sleep(wait)
    it = c.items.get(serial, {})
    worn = it.get("parent") == c.serial and it.get("layer")
    return bool(worn), c.texts_since(n), it


@check("P62-1", "A female character can wear the Kitsune Ears and Tail")
def p62_1(ctx):
    # KitsuneEarsTail.cs:29-60 (the female check at :53).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()        # Bot Human F 1: female
    s = give(a, p, "KitsuneEarsTail")
    worn, texts, it = try_equip(p, s)
    # Put it back in the pack, so the next run starts from the same place.
    if worn:
        p.lift_drop(s, 1, p.backpack())
        p.sleep(0.8)
    return worn and not any("Only females" in t for t in texts), "worn: %s (layer %s); messages %s" % (worn, it.get("layer"), texts[-3:])


@check("P62-7", "A male character cannot: 'Only females can wear this.', the item stays in the pack")
def p62_7(ctx):
    # KitsuneEarsTail.cs:53: SendLocalizedMessage(1010388), "Only females can wear this."
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=False)
    try:
        s = give(a, c, "KitsuneEarsTail")
        worn, texts, it = try_equip(c, s, 2.5)
        refused = any("Only females can wear this" in t for t in texts)
        back = c.wait(lambda: c.items.get(s, {}).get("container") == c.backpack(), 4)
        return refused and not worn and bool(back), "refusal %s; worn %s; back in pack %s; messages %s" % (refused, worn, bool(back), texts[-3:])
    finally:
        ctx.done(c)


# -- new characters ---------------------------------------------------------------------------------------------------

RACES = [("human", True), ("elf", False), ("gargoyle", True), ("human", False), ("elf", True), ("gargoyle", False)]


@check("T-039", "A new character is pointed at the board", kind="BOT-PARTIAL", left="T-039: a new player can find the board by eye")
def t_039(ctx):
    # ClusterFGuildStarterPath.cs:768-780: "Follow the arrow to the Guild Board in New Haven's square." (same map), or
    # "The Guild Board stands in ...". The board's spot: NewHavenQuestBoard.HomeLocation (3502, 2573, 14), :69.
    race, female = RACES[SHARED.setdefault("t039", 0) % len(RACES)]
    SHARED["t039"] += 1
    c = ctx.fresh(race=race, female=female)
    try:
        m = c.find_text(r"Guild Board", timeout=12)
        line = c.wait(lambda: next((t for g in c.gumps for t in all_text(c, g) if "Guild Board" in t), None), 3)
        # Quote only the sentence that names the board.
        board = next((s.strip() for s in re.split(r"(?<=[.!?])", line or "") if "Guild Board" in s), None)
        x, y = (c.pos or (0, 0))[:2]
        dist = max(abs(x - 3502), abs(y - 2573))
        ok = bool(m or board) and dist <= 40
        return ok, "%s %s at %s: message %r; welcome gump says %r; the board is %d tiles away" % (
            race, "female" if female else "male", (c.pos or ())[:3], m["text"] if m else None, board, dist)
    finally:
        ctx.done(c)


@check("CREATE", "Character creation for every race and sex, through the real 0xF8 packet", kind="HARNESS")
def create_all(ctx):
    race, female = RACES[(SHARED.setdefault("create", 0) + 3) % len(RACES)]
    SHARED["create"] += 1
    c = ctx.fresh(race=race, female=female)
    try:
        st = c.status
        # The body the server gave it: human 400/401, elf 605/606, gargoyle 666/667 (Server/Race.cs bodies).
        want = {"human": (400, 401), "elf": (605, 606), "gargoyle": (666, 667)}[race][1 if female else 0]
        ok = c.body == want and st.get("name") == c.name
        return ok, "%s %s: body %s (want %d), status name %r (asked for %r), at %s" % (
            race, "female" if female else "male", c.body, want, st.get("name"), c.name, (c.pos or ())[:3])
    finally:
        ctx.done(c)


@check("T-047", "Young by time played: the countdown, and 'I want to grow up!' brings the confirm gump")
def t_047(ctx):
    # Countdown: pinned Account.cs:818-827 ("... for {hours} more hours"); 336 hours here (ClusterFYoungPlayer.cs:28).
    # The phrase: ClusterFYoungPlayer.cs:32-48 opens RenounceYoungGump (cliloc 1013004). The gump is closed, never accepted.
    c = ctx.fresh(race="human", female=True)
    try:
        m = c.find_text(r"young player", timeout=8)
        n = len(c.gumps)
        c.say("I want to grow up!")
        g = c.wait_gump(lambda g: any("Young" in t for t in all_text(c, g)), 8, since=n)
        if g:
            c.reply_gump(g, 0)          # cancel: the account stays Young
        text = m["text"] if m else ""
        days_hours = bool(re.search(r"\bdays?\b", text) and re.search(r"\bhours?\b", text))
        hours = re.findall(r"(\d+)", text)
        ok = bool(m) and bool(g) and days_hours
        return ok, "countdown %r (days and hours: %s); confirm gump: %s" % (text, days_hours, all_text(c, g)[:2] if g else None)
    finally:
        ctx.done(c)


# -- guilds ------------------------------------------------------------------------------------------------------------

@check("D41", "Regression: the Guild Directory lists each guild once")
def d41(ctx):
    # ClusterFGuildSystem.cs:808-980 (GuildProgressGump): every guild row is a label at x=20 with a "Show me the way"
    # button BtnWayBase + i (100 + i); the header says "Member of n of N guilds".
    p = ctx.ensure_player()
    n = len(p.gumps)
    p.command("[guild", 0.3)
    g = p.wait_gump(lambda g: any(t == "Guild Directory" for t in all_text(p, g)), 10, since=n)
    if not g:
        return False, "[guild opened no Guild Directory"
    es = entries(g)
    way = sorted(e[3] for e in es if e[0] == "button" and 100 <= e[3] < 200)
    rows = []
    for e in es:
        if e[0] == "text" and e[1] == 20 and e[3] not in ("Guild Directory", "League of Extraordinary Citizens") and not e[3].startswith(("Every skill", "Member of")):
            rows.append(e[3])
    total = next((int(m.group(1)) for t in all_text(p, g) for m in [re.search(r"of (\d+) guilds", t)] if m), None)
    dupes = sorted(set(r for r in rows if rows.count(r) > 1))
    p.reply_gump(g, 0)
    ok = not dupes and total is not None and len(rows) == total == len(way) and rows.count("Artificers' Order") == 1
    return ok, "%d rows, header says %s guilds, %d way buttons; duplicates %s; Artificers' Order %d time(s)" % (
        len(rows), total, len(way), dupes or "none", rows.count("Artificers' Order"))


# -- smelting (Ore.cs, patched by server/patches/Ore-smelt-at-forge.patch) ---------------------------------------------

MINING = 45


def set_skill(admin, who, skill, value):
    staff_target(admin, "[SetSkill %s %s" % (skill, value), who)
    return who.wait(lambda: who.skills.get(MINING if skill == "Mining" else -1, {}).get("value") == float(value), 5) if skill == "Mining" else True


def place_forge(admin, who):
    """[add Forge on the tile beside the character; returns the forge's serial as the character sees it (0xF3)."""
    near(admin, who)
    before = set(who.items)
    admin.command("[add Forge", 0.2)
    if not admin.wait_target(8):
        raise AssertionError("[add Forge gave no target cursor")
    x, y, z = who.pos[:3]
    admin.target_xyz(x + 1, y, z)
    def forge():
        for s, it in who.items.items():
            if s not in before and it.get("world") and it.get("graphic") in (0xFB1,) or (s not in before and it.get("world") and 6522 <= it.get("graphic", 0) <= 6569):
                return s
        return None
    s = who.wait(forge, 8)
    if not s:
        raise AssertionError("no forge appeared beside %s" % who.label)
    return s


def smelt(who, ore, forge):
    """Double-click the ore, target the forge. Returns the messages the server sent."""
    n = len(who.messages)
    who.dclick(ore)
    if not who.wait_target(6):
        raise AssertionError("double-clicking the ore gave no target cursor")
    who.target_obj(forge)
    who.sleep(1.5)
    return who.texts_since(n)


REFUSED = "You have no idea how to smelt this strange ore!"     # cliloc 501986, Ore.cs:186


def smelt_case(ctx, ore_type, below, at, tries_at=8):
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    forge = place_forge(a, p)
    set_skill(a, p, "Mining", below)
    ore = give(a, p, "%s 10" % ore_type)
    t_below = smelt(p, ore, forge)
    refused_below = any(REFUSED in t for t in t_below)
    still = p.items.get(ore, {}).get("amount")
    set_skill(a, p, "Mining", at)
    results = []
    for _ in range(tries_at):
        # A pile needs 2 ore to smelt ("There is not enough metal-bearing ore in this pile", cliloc 501987, Ore.cs:190):
        # top it up, or later tries say nothing about the skill. (The first overnight run did not, see the cc-P91 notes.)
        if ore not in p.items or (p.items[ore].get("amount") or 0) < 2:
            ore = give(a, p, "%s 10" % ore_type)
        results.append(smelt(p, ore, forge))
    # One outcome per try, by the message the server sent: 501986 refused, 501988 smelted, 501990 burned (Ore.cs).
    def outcome(r):
        if any(REFUSED in t for t in r):
            return "refused"
        if any("put the metal in your backpack" in t for t in r):
            return "smelted"
        if any("burn away the impurities" in t for t in r):
            return "burned"
        if any("not enough metal-bearing ore" in t for t in r):
            return "too little ore"
        return "other"
    kinds = [outcome(r) for r in results]
    refused_at, smelted, burned = kinds.count("refused"), kinds.count("smelted"), kinds.count("burned")
    extra = ""
    if kinds.count("too little ore") or kinds.count("other"):
        extra = " (%d tries with too little ore, %d unanswered)" % (kinds.count("too little ore"), kinds.count("other"))
    return refused_below, still, refused_at, smelted, burned, (t_below[-1:] if t_below else []), extra


@check("P56-6", "Valorite refused at Mining 98.9, smelts at 99")
def p56_6(ctx):
    rb, still, ra, ok_n, burn_n, said, extra = smelt_case(ctx, "ValoriteOre", 98.9, 99.0, 6)
    ok = rb and still == 10 and ra == 0 and (ok_n + burn_n) > 0
    return ok, "at 98.9: refused %s %s, ore left %s; at 99.0 over 6 tries: refused %d, smelted %d, burned %d%s" % (rb, said, still, ra, ok_n, burn_n, extra)


@check("P56-4", "Platinum refused at Mining 112.4; at 112.5 it smelts (about half the time)")
def p56_4(ctx):
    rb, still, ra, ok_n, burn_n, said, extra = smelt_case(ctx, "PlatinumOre", 112.4, 112.5, 8)
    ok = rb and still == 10 and ra == 0 and ok_n > 0
    return ok, "at 112.4: refused %s %s, ore left %s; at 112.5 over 8 tries: refused %d, smelted %d, burned %d%s" % (rb, said, still, ra, ok_n, burn_n, extra)


# -- achievements (ClusterFAchievements.cs) ---------------------------------------------------------------------------

@check("P61-3", "Achievements: 'Oh Look, It's Dead' shows its Earned line and no 'Progress: 9/1'; unearned ones show progress")
def p61_3(ctx):
    # combat.first_blood "Oh Look, It's Dead" (ClusterFAchievements.cs:310-317, Kills >= 1); the Combat tab is button
    # TabBase + 1 = 101 (:1764, :1789-1793); progress lines only while not earned (:2076-2082).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    near(a, p)
    before = set(p.mobiles)
    a.command("[add Rat", 0.2)
    if a.wait_target(8):
        x, y, z = p.pos[:3]
        a.target_xyz(x + 1, y, z)
    rat = p.wait(lambda: next((s for s, m in p.mobiles.items() if s not in before and s != p.serial and m.get("body") == 238), None), 8)
    killed = False
    if rat:
        # A fair kill by the player: the admin pins the rat at 1 hit point, the player goes to war mode and attacks.
        a.command("[set CantWalk true Hits 1", 0.2)
        if a.wait_target(8):
            a.target_obj(rat)
        p.sleep(0.5)
        p.send(P.war_mode(True))
        p.sleep(0.3)
        p.send(P.attack(rat))
        killed = bool(p.wait(lambda: rat not in p.mobiles, 60))
        p.send(P.war_mode(False))
        p.sleep(1.0)
    n = len(p.gumps)
    p.command("[achievements", 0.3)
    g = p.wait_gump(None, 10, since=n)
    if not g:
        return False, "[achievements opened no gump"
    n = len(p.gumps)
    p.reply_gump(g, 101)
    g = p.wait_gump(None, 10, since=n)
    if not g:
        return False, "the Combat tab brought no gump"
    blocks = [plain(t) for t in all_text(p, g)]
    # The row whose title IS the achievement (other rows name it in a "Requires:" line).
    row = next((b for b in blocks if b.startswith("Oh Look, It's Dead")), None)
    progress_elsewhere = [b for b in blocks if "Progress:" in b and "Oh Look" not in b]
    p.reply_gump(g, 0)
    earned = row is not None and "AP" in row
    ok = earned and "Progress:" not in (row or "") and bool(progress_elsewhere)
    return ok, "rat killed by the player: %s; row %r; other rows with progress: %d" % (killed, (row or "")[:220], len(progress_elsewhere))


# -- P119-1: a sample of cc-P119's ported creatures, fought by a player ------------------------------------------------

# (type, body) from the ports (server/customizations/Mobiles/Normal/*.cs, cc-P119): one per kind of AI and body family.
# Not Ortanord: its 80-90 physical resistance is ServUO's, and it turns a wrestling hit into 0 damage even at 1 hit point,
# so it never dies to a bare-handed player (the second cc-P119 run, 60 s of swings). Lifestealer (45-55) instead.
P119_SAMPLE = (("IceHound", 98), ("OrcChopper", 7), ("Lifestealer", 303), ("EffeteUndeadGargoyle", 722), ("Wight", 252))


@check("P119-1", "Five of cc-P119's ported creatures spawn, fight back and die to a player, leaving a corpse")
def p119_1(ctx):
    # P61-3's fair kill, five times: the admin [add]s each type beside the player and pins it at 1 hit point and
    # CantWalk; the player, at Wrestling and Tactics 100, attacks. A pass is the creature showing with the port's body,
    # leaving the player's view when it dies, and a corpse (0x2006, amount = the body, Corpse.cs) landing where it died.
    # The first run used the shared 55-hit player, and the orc chopper's crushing blow killed it, so every later kill in
    # that run and the next was a ghost's (cc-P119 notes). So: a fresh character each run, at Str 250 and 175 hits
    # (AOS HitsMax 50 + Str/2), and its hits are part of the evidence.
    a = ctx.ensure_admin()
    p = ctx.fresh(label="p119")
    try:
        return _p119_fight(a, p)
    finally:
        ctx.done(p)


# -- P129-1: a sample of cc-P129's ported creatures (Pet Training callers and the leftovers), the same fight ------------

# (type, body or bodies) from the ports (cc-P129): two tameables with profile abilities (Blood Fox: Grasping Claw and
# bleed; Ossein Ram: Life Leech and Mortal Strike), the leather wolf's pack fellow, the gargish outcast (a gargoyle of
# either sex), Chief Paroxysmus' barded swamp dragon and ServUO's Drelgor. A fair kill (a Wrestling 100 player at 175
# hits, bare hands) is for creatures that cannot kill the player first: not the frost dragon or the giant turkey (85-92
# physical, 25,000 hits), not the dragon wolf or the cold drake (their DragonBreath, fired through the profile before the
# pin to 1 hit, deals 16% of the breather's hits: 90 of 175 in the wolf's first second, cc-P129's first two runs), not
# the macaw (Dex 400-500, Wrestling 120-140: dead in 13 s) or the stygian drake (its spells: dead in 8 s), the third run.
P129_SAMPLE = (("BloodFox", 0x58f), ("OsseinRam", 0x591), ("LeatherWolfFellow", 739), ("GargishOutcast", (666, 667)),
               ("ParoxysmusSwampDragon", 0x31F), ("Drelgor", 147))


@check("P129-1", "Six of cc-P129's ported creatures spawn, fight back and die to a player, leaving a corpse")
def p129_1(ctx):
    # P119-1's fight over this prompt's sample. The outcast's body is one of two (666 or 667), so it is matched by
    # either; everything else as P119-1.
    a = ctx.ensure_admin()
    p = ctx.fresh(label="p129")
    try:
        return _p119_fight(a, p, P129_SAMPLE)
    finally:
        ctx.done(p)


# cc-P139: the creature body route's proof creature, on body 2048 (0x800), the first third-party body the package
# ships (anim7 slot 1). The server half of the proof: it spawns with that body in the mobile packet, fights back with
# its bow and dies to a player, and its corpse (0x2006) carries 2048 as its amount. How it draws is Chase's eyes.
P139_SAMPLE = (("BurrowScampSlingerBodyTest", 2048),)


@check("P139-1", "The body-route test creature spawns on body 2048, fights back with its bow and dies to a player, leaving a corpse")
def p139_1(ctx):
    a = ctx.ensure_admin()
    p = ctx.fresh(label="p139")
    try:
        return _p119_fight(a, p, P139_SAMPLE)
    finally:
        ctx.done(p)


# cc-P140: six of the Rootdeep Burrows' normal spawns, one per zone and the hog (the tutorial ones engage only a
# tutorial participant, which the bot's fighter is not). Each shows with its registered body, fights back, dies and
# leaves a corpse carrying the body; the Sporecap wears one of two bodies. How they draw is Chase's eyes (P140-3).
P140_SAMPLE = (("BurrowHog", 2054), ("BurrowGrub", 2058), ("BurrowScamp", 2059), ("Rootling", 779),
               ("Sporecap", (2062, 2063)), ("Gloomling", 2067))


@check("P140-1", "Six Rootdeep Burrows spawns appear on their bodies, fight back and die to a player, leaving a corpse")
def p140_1(ctx):
    a = ctx.ensure_admin()
    p = ctx.fresh(label="p140")
    try:
        return _p119_fight(a, p, P140_SAMPLE)
    finally:
        ctx.done(p)


# Ghost bodies (human, elf, gargoyle; male and female): the fighter is dead.
GHOST_BODIES = (0x192, 0x193, 0x25F, 0x260, 0x2B6, 0x2B7)


def _wait_kill(a, p, mob, seconds=60, top_up_below=0.5):
    """cc-P137: wait for `mob` to leave the fighter's view. Below half its hits the fighter is topped up ([set Hits
    175, at most every 3 s), so one long fight cannot wear it down; P119-1's second run on 10-16 lost its fighter to
    the third creature (0/176) and then blamed the last three for never dying. Returns "killed", "fighter died" (a
    ghost body or 0 hits: the test's fault, not the creature's) or "timed out"."""
    end = time.monotonic() + seconds
    last_top_up = -999.0
    while time.monotonic() < end:
        if mob not in p.mobiles:
            return "killed"
        if p.body in GHOST_BODIES or p.status.get("hits") == 0:
            return "fighter died"
        hits, most = p.status.get("hits"), p.status.get("hitsMax")
        if hits is not None and most and hits < most * top_up_below and time.monotonic() - last_top_up >= 3:
            staff_target(a, "[set Hits 175", p)
            last_top_up = time.monotonic()
        p.sleep(1.0)
    return "timed out"


def _p119_fight(a, p, sample=P119_SAMPLE):
    for sk in ("Wrestling", "Tactics"):
        staff_target(a, "[SetSkill %s 100" % sk, p)
    # cc-P129: Dex 100 as well, so the swings come fast enough to land the kill before a hard hitter lands several.
    staff_target(a, "[set RawStr 250 RawDex 100 Hits 175", p)
    results = []
    for type_name, body in sample:
        bodies = body if isinstance(body, tuple) else (body,)
        near(a, p)
        # cc-P129: a full refill before each fight. The first P129-1 run showed the fights add up: the blood fox's bleed
        # and the cold drake's aura (15 cold every 5 s) left the player dead at the third creature, every run.
        staff_target(a, "[set Hits 175 Stam 100", p)
        before_m = set(p.mobiles)
        before_i = set(p.items)
        a.command("[add " + type_name, 0.2)
        if a.wait_target(8):
            x, y, z = p.pos[:3]
            a.target_xyz(x + 1, y, z)
        mob = p.wait(lambda: next((s for s, m in p.mobiles.items()
                                   if s not in before_m and s != p.serial and m.get("body") in bodies), None), 8)
        if not mob:
            results.append((type_name, False, "never appeared with body %s" % (bodies,)))
            continue
        body = p.mobiles[mob].get("body")
        a.command("[set CantWalk true Hits 1", 0.2)
        if a.wait_target(8):
            a.target_obj(mob)
        p.sleep(0.5)
        p.send(P.war_mode(True))
        p.sleep(0.3)
        p.send(P.attack(mob))
        outcome = _wait_kill(a, p, mob)
        killed = outcome == "killed"
        p.send(P.war_mode(False))
        if outcome == "fighter died":
            # The rest are not fought by a ghost: the run says the test failed, not the creatures (cc-P137).
            results.append((type_name, False, "the fighter died (a test problem, not this creature's)"))
            for rest, _ in sample[len(results):]:
                results.append((rest, False, "not fought: the fighter was dead"))
            break
        corpse = p.wait(lambda: next((s for s, it in p.items.items() if s not in before_i and it.get("world")
                                      and it.get("graphic") == 0x2006 and it.get("amount") == body), None), 8) if killed else None
        results.append((type_name, killed and bool(corpse), "killed %s, corpse %s" % (killed, bool(corpse))))
        p.sleep(1.0)
    ok = all(r[1] for r in results)
    return ok, "; ".join("%s: %s" % (t, e) for t, _, e in results) + "; player hits at the end %s/%s" % (
        p.status.get("hits"), p.status.get("hitsMax"))


# -- reset (ClusterFResetStone.cs, ClusterFDevTools.cs) ---------------------------------------------------------------

@check("T-046", "Reset stone and [ResetMyAccount open the same gump", kind="BOT-PARTIAL",
       left="T-046: each scope (this character / all / account) clearing what it says, joined guilds included, is not pressed by the bot; it would wipe its own characters")
def t_046(ctx):
    # ClusterFDevTools.cs:39, :72 (OpenSelfReset) and ClusterFResetStone.cs:113 (ResetStone). The bot compares the two
    # gumps' text and buttons and presses nothing.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    n = len(p.gumps)
    p.command("[ResetMyAccount", 0.3)
    g1 = p.wait_gump(None, 10, since=n)
    if g1:
        p.reply_gump(g1, 0)
    near(a, p)
    before = set(p.items)
    a.command("[add ResetStone", 0.2)
    if a.wait_target(8):
        x, y, z = p.pos[:3]
        a.target_xyz(x + 1, y, z)
    stone = p.wait(lambda: next((s for s, it in p.items.items() if s not in before and it.get("world")), None), 8)
    if not stone:
        return False, "no reset stone appeared"
    n = len(p.gumps)
    p.dclick(stone)
    g2 = p.wait_gump(None, 10, since=n)
    if g2:
        p.reply_gump(g2, 0)
    if not g1 or not g2:
        return False, "[ResetMyAccount gump %s; stone gump %s" % (bool(g1), bool(g2))
    t1, t2 = all_text(p, g1), all_text(p, g2)
    b1, b2 = sorted(buttons(g1)), sorted(buttons(g2))
    same = t1 == t2 and b1 == b2 and g1["typeId"] == g2["typeId"]
    return same, "same gump type %s; same text %s (%d lines); same buttons %s %s" % (
        g1["typeId"] == g2["typeId"], t1 == t2, len(t1), b1 == b2, b1)


# cc-P224: the second-half checks and their shared helpers; imported last so they register after the cc-P91 ones.
from . import checks_p224  # noqa: E402,F401
