"""cc-P224, group: the Thieves' Den and monster stealing. cc-P127 ported ServUO's monster stealing (the hook at the end of
pinned Stealing.cs's OnTarget, Stealing-monster-stealing.patch) and cc-P142 put the shard's rules on top of it
(server/customizations/Services/Monster Stealing/Core/ThievesDenStealRules.cs, StealingHandler.cs, StealLootTables.cs).
The play test's steps are the For Chase list of notes/cc-P142-monster-stealing-shard-rules.md; the strings asserted here
are read from the code under test and cited per line.

How the bot steals (every check): the admin clears the thief's skill delay ([set NextSkillTime 0: pinned Mobile.cs:700-701,
an Administrator command property; pinned Stealing.cs:69 asks 30 s between uses, which no 2-minute check could carry),
the thief uses Stealing (0x12), gets the cursor (Stealing.cs:62-66) and targets the creature standing beside it (the
cursor's range is 1, Stealing.cs:76). Creatures are [add]ed beside the thief with FightMode None and CantWalk so they
neither wander off nor pick fights on their own (a miss still turns them on the thief, StealingHandler.cs:215-226).
The thief is the long-lived player; its membership of the Den is one-way (nothing in the game leaves a guild short of
a reset, which the bot never runs), its standing is set per check and put back to 0 at the end. Non-members are fresh
characters. The thief is [set Blessed true during the loops that do not read the creature's combatant.
"""
import re
import time

from .checks import check, all_text, near, staff_target
from .checks_p224 import skill_value, set_skill_to, use_skill, spawn, set_on, gump_with, wait_text, SKILL

# -- the lines, from the code under test ---------------------------------------------------------------------------------

# ThievesDenStealRules.cs:77-78
NOT_MEMBER = "Only a hand sworn to the Thieves' Den knows where a monster keeps its treasures."
# TooLowLine (:80-81) with PlaceName(FacetDungeon) "the dungeons beyond Felucca" (:91); Article() gives "an Initiate" (:83-84).
INITIATE_BEYOND_FELUCCA = "The Den does not trust an Initiate to work the dungeons beyond Felucca. Come back a Journeyman."
# TooLowLine with "a creature of that renown" (:217): a boss (:74 ShardBossTypes holds TheBurrowMother) in a place the
# Initiate may otherwise work, needing RankForBosses = Master (:55).
INITIATE_RENOWN = "The Den does not trust an Initiate to work a creature of that renown. Come back a Master."
WARY = "The creature is wary of your hands. Give it a moment."            # StealingHandler.cs:69, sent at :170
CAUGHT = "The creature catches your hand in its hoard and turns on you!"  # :70, sent at :218 (Miss)
MINOR = "You slip something small from the creature."                    # :71, sent at :181 (Stealing below 100)
SPECIAL = 1094947     # :187 "You successfully steal a special item from the creature!"
ALREADY = 1094948     # :133 "That creature has already been stolen from. There is nothing left to steal."
NEVER = 502708        # :140 "You can't steal from this."
MUST_WAIT = 500118    # pinned Mobile.cs:3734 "You must wait a few moments to use another skill."
# StealLootTables.cs:117 (scrip), :148-151 (gold, lockpicks, potion, bandages).
MINOR_KINDS = (
    ("scrip", re.compile(r"^You pocket (\d+) Thieves' Den scrip from the creature's hoard\.$")),
    ("gold", re.compile(r"^You lift (\d+) gold from the creature\.$")),
    ("lockpicks", re.compile(r"^You lift (\d+) lockpicks from the creature\.$")),
    ("potion", re.compile(r"^You lift a small potion from the creature\.$")),
    ("bandages", re.compile(r"^You lift (\d+) bandages from the creature\.$")),
)
RETRY_COOLDOWN = 5.0  # StealingHandler.cs:67 RetryCooldown after a miss

# -- the places --------------------------------------------------------------------------------------------------------

# (map, x, y, z). Dungeon spots are the regions' own GoLocation in pinned Data/regions.json (the Despise and Shame
# entries, both facets: Despise area x 5377-5631, y 516-1022; Shame x 5377-5634, y 2-262). The play test names the
# overworld entrances; those are outside the DungeonRegion, so the checks stand inside.
TRAMMEL_DESPISE = ("Trammel", 5501, 570, 59)
FELUCCA_SHAME = ("Felucca", 5395, 126, 0)
# OldHavenBurrowsRegion.cs:38 (Trammel x 5136-5247, y 1128-1263), the note's spot; outside the Vault
# (RootdeepVault.cs:42, x 5142-5178, y 1132-1159), which IsNeverStolen refuses (ThievesDenStealRules.cs:173-175).
BURROWS = ("Trammel", 5200, 1200, 0)
# Where [GenChamps puts champion altars (pinned GenChamps.cs:33-35 Lost Lands, :50 Despise, all Felucca); the spawn's
# region covers its SpawnArea whenever the spawn exists (ChampionSpawn.cs:198-207, 1365-1370).
CHAMPION_SPOTS = (("Felucca", 5511, 2360, 42), ("Felucca", 6038, 2401, 47), ("Felucca", 5549, 2640, 16),
                  ("Felucca", 5557, 827, 65))

# -- the creatures -----------------------------------------------------------------------------------------------------

SKELETON = ("Skeleton", (50, 56))              # pinned Skeleton.cs:12, Fame 450 (:35): fame level 0, no bonus
# Fame 28000 (pinned RedDeath.cs:43) is fame level 37 (StealingHandler.cs:283-287), bonus +3 (:247-255); a SkeletalMount
# (body 793, SkeletalMount.cs:9); not a boss to the Den (ClusterFRareSelfRepair.cs:57-60 lists none of its kind).
FAMOUS = ("RedDeath", (793,))
MOTHER = ("TheBurrowMother", (2061,))          # WarrenCreatures.cs:170; ShardBossTypes, ThievesDenStealRules.cs:74
GUILDMASTER = ("ThiefGuildmaster", (0x190, 0x191))  # the Den's guildmaster type, ClusterFGuildStarterPath.cs:219

# spawn() targets (x + 1, y) of `where`: these put a creature E, W, S and N of the thief.
SLOT_OFFSETS = ((0, 0), (-2, 0), (-1, 1), (-1, -1))


# -- helpers -----------------------------------------------------------------------------------------------------------

def move_to(admin, who, place):
    """Staff move of another character across facets: [set Map <m> X <x> Y <y> Z <z> on it (pinned Properties through
    Types.TryParse's Parse-method path, Map.Parse at Map.cs:1759; X, Y, Z, Map are Mobile command properties). Waits for
    the thief's own 0x20, then brings the admin alongside. Returns True when the character arrived."""
    m, x, y, z = place
    set_on(admin, who.serial, "Map %s X %d Y %d Z %d" % (m, x, y, z), who)
    ok = who.wait(lambda: who.pos and abs(who.pos[0] - x) <= 2 and abs(who.pos[1] - y) <= 2, 8)
    near(admin, who)
    who.sleep(0.5)
    return bool(ok)


def get_prop(admin, serial, name):
    """[get <name> on a serial the admin can see; the value text after "<name> = " (pinned Properties.cs:329), or None."""
    n = len(admin.messages)
    admin.command("[get " + name, 0.2)
    if not admin.wait_target(8):
        raise AssertionError("[get %s gave no target cursor" % name)
    admin.target_obj(serial)
    m = admin.find_text(r"^%s = " % re.escape(name), since=n, timeout=5)
    return m["text"].split(" = ", 1)[1] if m else None


def set_standing(admin, who, amount):
    """[GuildStanding thieves <amount> on the character (ClusterFGuildStandingCommand.cs:27-68); the staff line
    "Set <name>'s Thieves' Den standing to N (Rank)." (:112), or None. 0 Initiate, 5000 Journeyman, 15000 Master
    (ClusterFGuildSystem.cs:749-756 GenericRank)."""
    n = len(admin.messages)
    staff_target(admin, "[GuildStanding thieves %d" % amount, who)
    m = admin.find_text(r"Thieves' Den standing to", since=n, timeout=5)
    return m["text"] if m else None


def empty_hands(who):
    """Stealing needs both hands free (pinned Stealing.cs:53-55): anything on layers 1 and 2 goes into the pack."""
    held = [s for s, it in who.items.items() if it.get("parent") == who.serial and it.get("layer") in (1, 2)]
    for s in held:
        who.lift_drop(s, 1, who.backpack())
        who.sleep(0.6)
    return held


def ensure_den_member(admin, who):
    """Joins `who` to the Thieves' Den unless it is one already. The admin [add]s the Den's guildmaster beside `who`
    (GUILDMASTER), `who` picks Talk (cliloc 6146: GuildMembershipEntry, ClusterFGuildSystem.cs:785-797, added to every
    guildmaster with a guild definition by ClusterFGuildmasterExtension.cs:37) and the hall page opens (GuildTaskDetailGump,
    titled with the guild's name, :1075): a member reads "Member. Rank: ..." (:1103); anyone else presses Join (BtnJoin = 1,
    :1040, :1092-1093, within GuildmasterRange 8, :91, :1200-1206) and Join says "You have joined the Thieves' Den as
    Initiate. Joining is free." (:436). Returns (is_member, how)."""
    gm = spawn(admin, who, GUILDMASTER[0], GUILDMASTER[1])
    if not gm or gm == admin.serial:
        raise AssertionError("[add ThiefGuildmaster brought no guildmaster beside %s" % who.label)
    who.sleep(0.5)
    n = len(who.gumps)
    menu = who.context_menu(gm)
    if not menu:
        raise AssertionError("the guildmaster offered no context menu")
    who.pick_context(menu, 6146)
    g = gump_with(who, "Thieves' Den", 8, since=n)
    if not g:
        raise AssertionError("Talk opened no Thieves' Den hall page")
    texts = all_text(who, g)
    member_line = next((t for t in texts if t.startswith("Member. Rank:")), None)
    if member_line:
        who.reply_gump(g, 0)
        return True, "already a member (%s)" % member_line
    if not any("Join the guild" in t for t in texts):
        who.reply_gump(g, 0)
        return False, "the hall page offered no Join: %s" % texts[:6]
    nm = len(who.messages)
    who.reply_gump(g, 1)
    said = wait_text(who, r"You have joined the Thieves' Den as Initiate\. Joining is free\.|Welcome back to the Thieves' Den\.", nm)
    return bool(said), "joined now: %r" % said


def outcome_of(msgs):
    """One steal's outcome by the handler's lines (StealingHandler.cs:130-206), ignoring OSI's own item-steal lines."""
    for m in msgs:
        cl, t = m.get("cliloc"), m.get("text") or ""
        if cl == SPECIAL:
            return "special"
        if cl == ALREADY:
            return "already"
        if cl == NEVER:
            return "never"
        if cl == MUST_WAIT:
            return "must wait"
        if t == CAUGHT:
            return "caught"
        if t == WARY:
            return "wary"
        if t == MINOR:
            return "minor"
        if t == NOT_MEMBER:
            return "not a member"
        if t.startswith("The Den does not trust"):
            return "refused"
    return "nothing"


def refusal_text(msgs):
    return next((m["text"] for m in msgs if m.get("text", "").startswith(("The Den does not trust", "Only a hand sworn"))), None)


def minor_kind(msgs):
    for m in msgs:
        for kind, rx in MINOR_KINDS:
            if rx.match(m.get("text") or ""):
                return kind, m["text"]
    return None, None


def steal(admin, thief, mob, settle=2.5):
    """One Stealing attempt on `mob` (see the module docstring). Returns (outcome, messages since the attempt)."""
    set_on(admin, thief.serial, "NextSkillTime 0")
    n = len(thief.messages)
    cursor = use_skill(thief, "Stealing")
    if not cursor:
        msgs = thief.messages[n:]
        if any("Both hands must be free" in (m.get("text") or "") for m in msgs):
            empty_hands(thief)
            set_on(admin, thief.serial, "NextSkillTime 0")
            n = len(thief.messages)
            cursor = use_skill(thief, "Stealing")
        if not cursor:
            return "no cursor", thief.messages[n:]
    thief.target_obj(mob)
    thief.wait(lambda: outcome_of(thief.messages[n:]) != "nothing", settle)
    msgs = thief.messages[n:]
    return outcome_of(msgs), msgs


def place(admin, thief, creature, slot=0):
    """[add the creature E/W/S/N of the thief (SLOT_OFFSETS), then FightMode None and CantWalk on it. Its serial."""
    type_name, bodies = creature
    x, y, z = thief.pos[:3]
    dx, dy = SLOT_OFFSETS[slot]
    mob = spawn(admin, thief, type_name, bodies, where=(x + dx, y + dy, z))
    if not mob or mob == admin.serial:
        raise AssertionError("[add %s beside %s brought no creature" % (type_name, thief.label))
    set_on(admin, mob, "FightMode None CantWalk true")
    return mob


def steal_rounds(admin, thief, creature, seconds, tries, slots=4, hold=None):
    """Steals from `slots` fresh creatures in turn (so each gets more than RETRY_COOLDOWN between its tries) until
    `seconds` or `tries` run out; a creature stolen from (one success each, StealingHandler.cs:200) is replaced by a
    fresh one in its slot. Returns a dict: counts per outcome, the minor kinds seen, the refusal line if any, tries."""
    mobs = [place(admin, thief, creature, i) for i in range(slots)]
    missed_at = [0.0] * slots
    counts, kinds, refusal = {}, [], None
    end = time.monotonic() + seconds
    done = 0
    while done < tries and time.monotonic() < end:
        i = done % slots
        thief.sleep(max(0.0, missed_at[i] + RETRY_COOLDOWN + 0.3 - time.monotonic()))
        out, msgs = steal(admin, thief, mobs[i])
        done += 1
        counts[out] = counts.get(out, 0) + 1
        # The test shard's 5x gain took Stealing from 80 to 155 inside 50 tries (second trial), which turned the
        # minor tier into specials: keep the skill where the item put it.
        if hold is not None and (skill_value(thief, "Stealing") or 0) >= hold + 5:
            set_skill_to(admin, thief, "Stealing", hold)
            counts["re-set"] = counts.get("re-set", 0) + 1
        # The test shard's 5x gain took Stealing from 80 to 155 inside 50 tries (second trial), which turned the
        # minor tier into specials: keep the skill where the item put it.
        if hold is not None and (skill_value(thief, "Stealing") or 0) >= hold + 5:
            set_skill_to(admin, thief, "Stealing", hold)
            counts["re-set"] = counts.get("re-set", 0) + 1
        if out in ("caught", "wary"):
            missed_at[i] = time.monotonic()
        if out in ("refused", "not a member"):
            refusal = refusal or refusal_text(msgs)
        if out == "minor":
            kinds.append(minor_kind(msgs))
        if out in ("special", "minor"):
            mobs[i] = place(admin, thief, creature, i)
        if out == "no cursor":
            thief.sleep(1.0)
    return {"counts": counts, "kinds": kinds, "refusal": refusal, "tries": done, "mobs": mobs}


def prepare_thief(admin, thief, stealing, bless=True):
    """The common setup: Den membership, Stealing at `stealing`, empty hands, Blessed (optional)."""
    member, how = ensure_den_member(admin, thief)
    if not member:
        raise AssertionError("the thief could not join the Thieves' Den: %s" % how)
    set_skill_to(admin, thief, "Stealing", stealing)
    empty_hands(thief)
    if bless:
        set_on(admin, thief.serial, "Blessed true", thief)
    return how


def stealing_of(thief):
    return (thief.skills.get(SKILL["Stealing"]) or {}).get("value")


def summary(r):
    return "%d tries: %s%s" % (r["tries"], dict(sorted(r["counts"].items())), ("; hauls %s" % [k for k, _ in r["kinds"]]) if r["kinds"] else "")


# -- P142-1: a Trammel dungeon at Initiate, Journeyman and as a non-member ------------------------------------------------

@check("P142-1", "Trammel Despise: an Initiate is refused the dungeons beyond Felucca, a Journeyman steals specials, a non-member is told of the Den")
def p142_1(ctx):
    # ThievesDenStealRules.cs:120-128 (a DungeonRegion off Felucca is FacetDungeon), :155-159 (Journeyman), :205-219 (the
    # lines); StealingHandler.cs:145-158 says them; :187 the special-item cliloc. The play test says Stealing 120; against
    # RedDeath (+3) that is 11 + 3 = 14 % a try (StealingHandler.cs:239-243), so the Journeyman gets ~30 tries in 60 s
    # with the skill delay cleared (one of them succeeds 99 times in 100).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    f = None
    try:
        how = prepare_thief(a, p, 120)
        if not move_to(a, p, TRAMMEL_DESPISE):
            raise AssertionError("the thief did not arrive in Trammel Despise (at %s)" % (p.pos,))
        set_standing(a, p, 0)
        mob = place(a, p, FAMOUS, 0)
        out0, msgs0 = steal(a, p, mob)
        line0 = refusal_text(msgs0)
        if out0 == "nothing":
            out0, msgs0 = steal(a, p, mob)
            line0 = refusal_text(msgs0)
        standing = set_standing(a, p, 5000)
        r = steal_rounds(a, p, FAMOUS, 60, 40)
        # The non-member: a fresh character beside the thief, same skill, same creature (never stolen from: a refusal
        # marks nothing, StealingHandler.cs:145-158).
        f = ctx.fresh(label="p142-nonmember")
        set_skill_to(a, f, "Stealing", 120)
        empty_hands(f)
        move_to(a, f, (TRAMMEL_DESPISE[0],) + tuple(p.pos[:3]))
        fmob = place(a, f, FAMOUS, 0)
        outf, msgsf = steal(a, f, fmob)
        linef = refusal_text(msgsf)
        initiate_ok = line0 == INITIATE_BEYOND_FELUCCA and not any(m.get("cliloc") == SPECIAL for m in msgs0)
        journeyman_ok = r["counts"].get("special", 0) >= 1 and r["counts"].get("refused", 0) == 0
        nonmember_ok = linef == NOT_MEMBER
        ok = initiate_ok and journeyman_ok and nonmember_ok
        ev = ("Den: %s; Initiate (standing 0) at Despise %s: %r; %s; Journeyman at Stealing %s over %s; "
              "non-member: %r. Stealing 120 as the play test says; RedDeath for the fame bonus; the thief Blessed; "
              "the Den join is one-way on the long-lived player" % (
                  how, p.pos[:3] if p.pos else None, line0 or out0, standing, stealing_of(p), summary(r), linef or outf))
        return ok, ev
    finally:
        if f is not None:
            ctx.done(f)
        set_standing(a, p, 0)
        set_on(a, p.serial, "Blessed false", p)


# -- P142-2: a champion spawn at Initiate ----------------------------------------------------------------------------------

@check("P142-2", "A champion spawn: an Initiate works it, the special-item message comes, no refusal")
def p142_2(ctx):
    # ThievesDenStealRules.cs:120-123 (IsPartOf<ChampionSpawnRegion> wins over the dungeon), :155 (RankForOsiPlaces =
    # Initiate). The region exists only where [GenChamps (a Developer generator the bot never runs) placed a spawn: the
    # admin's [where (pinned Handlers.cs:76-105; a nameless region prints its type, Region.cs:730) must name a
    # ChampionSpawnRegion at one of CHAMPION_SPOTS, else the check is an ERROR (setup that cannot happen here).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    try:
        how = prepare_thief(a, p, 120)
        found, tried = None, []
        for spot in CHAMPION_SPOTS:
            move_to(a, p, spot)
            n = len(a.messages)
            a.command("[where", 0.6)
            m = a.find_text(r"^Your region is ", since=n, timeout=3)
            tried.append((spot[1:], m["text"] if m else "no region"))
            if m and "ChampionSpawnRegion" in m["text"]:
                found = spot
                break
        if not found:
            raise RuntimeError("no ChampionSpawnRegion at any of [GenChamps' spots (%s): the world has no champion spawn, "
                               "the bot cannot work one; [GenChamps (Developer) would place them" % tried)
        set_standing(a, p, 0)
        r = steal_rounds(a, p, FAMOUS, 60, 40)
        ok = r["counts"].get("special", 0) >= 1 and r["counts"].get("refused", 0) == 0 and r["counts"].get("not a member", 0) == 0
        return ok, "Den: %s; at %s (%s); Initiate at Stealing %s over %s; refusal line %r" % (
            how, found[1:], tried[-1][1], stealing_of(p), summary(r), r["refusal"])
    finally:
        set_standing(a, p, 0)
        set_on(a, p.serial, "Blessed false", p)


# -- P142-3: the Rootdeep Burrows ------------------------------------------------------------------------------------------

@check("P142-3", "The Burrows: an Initiate steals, a non-member is refused, The Burrow Mother needs a Master")
def p142_3(ctx):
    # ThievesDenStealRules.cs:61 (OldHavenBurrowsRegion is the tutorial dungeon), :110-113, :156 (Initiate), :74 (The
    # Burrow Mother is a boss), :162 and :213-217 ("a creature of that renown", Master). An Initiate against skeletons:
    # a roll (caught, StealingHandler.cs:218, or the special cliloc :187) and never a refusal; the Master against the
    # Mother: a roll. Stealing 120 (carried from the play test's step 1): 11 % a try against a skeleton, so the special
    # message is counted but the pass asks for a roll with no refusal.
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    f = None
    try:
        how = prepare_thief(a, p, 120)
        if not move_to(a, p, BURROWS):
            raise AssertionError("the thief did not arrive in the Burrows (at %s)" % (p.pos,))
        set_standing(a, p, 0)
        r = steal_rounds(a, p, SKELETON, 40, 20)
        rolled = r["counts"].get("caught", 0) + r["counts"].get("special", 0) + r["counts"].get("minor", 0)
        # The non-member beside the thief.
        f = ctx.fresh(label="p142-nonmember")
        set_skill_to(a, f, "Stealing", 120)
        empty_hands(f)
        move_to(a, f, (BURROWS[0],) + tuple(p.pos[:3]))
        fmob = place(a, f, SKELETON, 0)
        outf, msgsf = steal(a, f, fmob)
        linef = refusal_text(msgsf)
        # The Burrow Mother, west of the thief: refused at standing 0, rolled at 15000.
        mother = place(a, p, MOTHER, 1)
        out_m0, msgs_m0 = steal(a, p, mother)
        line_m0 = refusal_text(msgs_m0)
        standing = set_standing(a, p, 15000)
        out_m1 = None
        end = time.monotonic() + 40
        while time.monotonic() < end:
            out_m1, _ = steal(a, p, mother)
            if out_m1 in ("caught", "special", "minor", "already"):
                break
            p.sleep(RETRY_COOLDOWN + 0.5)
        ok = (rolled >= 1 and r["counts"].get("refused", 0) == 0 and linef == NOT_MEMBER
              and line_m0 == INITIATE_RENOWN and out_m1 in ("caught", "special", "minor"))
        ev = ("Den: %s; Initiate at %s over %s (rolled %d); non-member: %r; Mother at standing 0: %r; %s; Mother at 15000: %s"
              % (how, p.pos[:3] if p.pos else None, summary(r), rolled, linef or outf, line_m0 or out_m0, standing, out_m1))
        return ok, ev
    finally:
        if f is not None:
            ctx.done(f)
        set_standing(a, p, 0)
        set_on(a, p.serial, "Blessed false", p)


# -- P142-4: the minor tier in Felucca Shame --------------------------------------------------------------------------------

@check("P142-4", "Felucca Shame at Stealing 80: minor hauls only (gold, lockpicks, potion, bandages, scrip), never a special, Stealing rises")
def p142_4(ctx):
    # StealingHandler.cs:160-206: below SpecialFrom (100) a success is the minor line (:181) and GiveMinor (StealLootTables.cs
    # :107-155, the five kind lines), never cliloc 1094947 (:187); the gain roll after every attempt (:208-211, :292-298).
    # At 80 the chance is 2 + 20 x 0.075 = 3.5 % plus the fame bonus (:239-243): +3 against RedDeath, 6.5 % a try. The
    # play test says thirty fresh monsters; here four fresh RedDeaths are cycled, a stolen-from one replaced, for up to 50
    # tries in 95 s (a haul comes at least once in ~96 of 100 such runs).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    try:
        how = prepare_thief(a, p, 80)
        if not move_to(a, p, FELUCCA_SHAME):
            raise AssertionError("the thief did not arrive in Felucca Shame (at %s)" % (p.pos,))
        set_standing(a, p, 0)
        before = stealing_of(p)
        r = steal_rounds(a, p, FAMOUS, 95, 50, hold=80)
        p.sleep(1.0)
        after = stealing_of(p)
        kinds = [k for k, _ in r["kinds"]]
        bad = r["counts"].get("special", 0) + r["counts"].get("refused", 0) + r["counts"].get("not a member", 0)
        rose = before is not None and after is not None and after > before
        ok = bad == 0 and len(kinds) >= 1 and rose
        return ok, "Den: %s; Felucca Shame at %s, standing 0; %s; haul lines %s; specials %d; Stealing %s -> %s" % (
            how, p.pos[:3] if p.pos else None, summary(r), [t for _, t in r["kinds"]], r["counts"].get("special", 0), before, after)
    finally:
        set_standing(a, p, 0)
        set_on(a, p.serial, "Blessed false", p)


# -- P142-5: a miss, the wary retry, the retry after the cooldown, nothing left ----------------------------------------------

@check("P142-5", "Felucca Shame at Stealing 60, hidden: a miss reveals and is caught, the creature turns, wary at once, a roll after 6 s, then nothing left")
def p142_5(ctx):
    # StealingHandler.cs:214-226 (Miss: cooldown, the caught line, RevealingAction, AggressiveAction and Combatant),
    # :168-173 (wary within RetryCooldown, :67 five seconds), :130-136 (already stolen, cliloc 1094948 after one success,
    # :200). Pinned Stealing.cs:62-64 already reveals the thief when the cursor comes, so the evidence says where the
    # thief stopped being hidden ([get Hidden on it, Mobile.cs:1249). The creature's combatant is read with [get Combatant
    # (Mobile.cs:720; printed as pinned Mobile.ToString, :3723). The thief is NOT Blessed for the miss: a Blessed target
    # cannot become a combatant; its hits are raised instead and the skeleton's damage pinned at 1. The success at the
    # end needs Stealing raised to 200 (2 % a try at 60; 35 % at 200, StealingHandler.cs:239-243).
    a = ctx.ensure_admin()
    p = ctx.ensure_player()
    try:
        how = prepare_thief(a, p, 60, bless=False)
        set_skill_to(a, p, "Hiding", 100)
        staff_target(a, "[set RawStr 250 Hits 175", p)
        if not move_to(a, p, FELUCCA_SHAME):
            raise AssertionError("the thief did not arrive in Felucca Shame (at %s)" % (p.pos,))
        set_standing(a, p, 0)
        # Hide (Hiding.cs:89-91, "You have hidden yourself well." 501240), before any creature is near.
        hidden_before = None
        for _ in range(3):
            set_on(a, p.serial, "NextSkillTime 0")
            n = len(p.messages)
            use_skill(p, "Hiding", settle=None)
            wait_text(p, r"hidden yourself well|can't seem to hide", n)
            hidden_before = get_prop(a, p.serial, "Hidden")
            if hidden_before == "True":
                break
        mob = place(a, p, SKELETON, 0)
        # It must be able to fight back (place() gives FightMode None): Closest, 1 damage a hit.
        set_on(a, mob, "DamageMin 1 DamageMax 1 FightMode Closest")
        # The first steal: a miss at Stealing 60 (98 of 100). On the rare success, a fresh skeleton and again.
        out1, msgs1 = steal(a, p, mob)
        t_miss = time.monotonic()
        attempts = 1
        while out1 != "caught" and attempts < 3:
            if out1 in ("minor", "special"):
                mob = place(a, p, SKELETON, 0)
                set_on(a, mob, "DamageMin 1 DamageMax 1 FightMode Closest")
            out1, msgs1 = steal(a, p, mob)
            t_miss = time.monotonic()
            attempts += 1
        # At once: wary (RetryCooldown is 5 s, StealingHandler.cs:67; the second trial read two [get]s first and the
        # window had passed, so the retry rolled).
        out2, _ = steal(a, p, mob)
        hidden_after = get_prop(a, p.serial, "Hidden")
        combatant = get_prop(a, mob, "Combatant")
        turned = bool(combatant) and re.search(r"0x0*%X\b" % p.serial, combatant, re.I) is not None
        set_on(a, p.serial, "Blessed true")
        # After six seconds from the miss: a roll again.
        p.sleep(max(0.0, t_miss + 6.0 - time.monotonic()))
        out3, _ = steal(a, p, mob)
        # Until a success, then once more: nothing left.
        set_skill_to(a, p, "Stealing", 200)
        last, out4, out5 = None, None, None
        end = time.monotonic() + 70
        while time.monotonic() < end:
            if last == "caught":
                p.sleep(RETRY_COOLDOWN + 0.5)
            last, _ = steal(a, p, mob)
            if last in ("special", "minor", "already"):
                out4 = last
                break
        if out4 in ("special", "minor"):
            out5, _ = steal(a, p, mob)
        ok = (hidden_before == "True" and out1 == "caught" and hidden_after == "False" and turned and out2 == "wary"
              and out3 in ("caught", "minor") and out4 in ("special", "minor") and out5 == "already")
        ev = ("Den: %s; hidden before %s; first steal %s (attempt %d); hidden after the miss %s (pinned Stealing.cs:64 reveals at the "
              "cursor already); creature's Combatant %r (the thief: %s); at once %s; after 6 s %s; at Stealing %s: %s then %s; "
              "thief hits %s/%s" % (how, hidden_before, out1, attempts, hidden_after, combatant, turned, out2, out3,
                                     stealing_of(p), out4 or last, out5, p.status.get("hits"), p.status.get("hitsMax")))
        return ok, ev
    finally:
        set_standing(a, p, 0)
        set_on(a, p.serial, "Blessed false", p)
