"""cc-P224, group Rootdeep: the Vault doorway (P141-2) and the Strongroom crawlway (P141-3).

Code under test: server/customizations/Regions/RootdeepVault.cs (the region, registered from code in
RootdeepVaultRegion.Initialize/Register, :50-61, over x 5142-5178, y 1132-1159, :42; the doorway at 5179,1137, :97),
Tutorial/ClusterFTutorial.cs (the states and the [Tutorial staff command, :238-280), Mobiles/Burrows/VaultCreatures.cs
(TheOverdraft, :241-294). The shard facts V2, V3 and V4 (server/tests/Regions/RootdeepVaultVerification.cs) pin the same
behaviour in-process; these checks walk it on the shard through the real movement packets.

What the two items need that the seeder would place: only the crawlway item (P141-3). The region needs nothing seeded.
P141-3 finds a RootdeepCrawlway on the mouth tile 5160,1159 and, when there is none, the admin [add]s one there at the
floor's z (what the seeder does, ClusterFRootdeepSeeder.cs:199-200; a later [ClusterFSeedRootdeep counts it as present,
:191-194 and :277-289). The seeder itself is never run.

Fresh characters are rationed, and both items want a brand-new character in the tutorial, so one fresh character does
both: P141-2's function runs P141-3's steps after its own and hands the result over through SHARED (the P64-1/P64-2
shape in checks.py).

The tutorial record is per account (ClusterFAccountData.cs:1204-1259), and IsParticipant also wants the record's
CharacterSerial to be this character (ClusterFTutorial.cs:48-52), so a pool account's new character is never a
participant by itself: the admin's [Tutorial reset then [Tutorial enter makes it one (:272-274, :257-258). The check
leaves the account InCore with this character's serial, the same shape every pool account has after its first fresh
character logged in (:58-65).
"""
import re

from .checks import check, near, staff_target, SHARED
from .checks_p224 import spawn, set_on, make_pet, fair_kill, wait_text

# The Echo's lines, as written (RootdeepVault.cs:110-116); SystemLine prefixes "System: " (ClusterFTutorial.cs:54).
NOT_PARTICIPANT = "The Vault is for the League's trainees. You are not one. The door has excellent judgement."  # :110
GRADUATE = "You graduated. The Vault does not do reunions."                                                      # :111
SEALED = "The Vault sealed behind you. Nothing in there owes you anything now."                                 # :112
CRAWLWAY_OPENS = "Something gave way in the south wall. A crawlway. One way, I am told. Do not get sentimental about the Vault."  # :113
CRAWL = "You squeeze through the crawlway. Behind you the earth settles. The Vault is sealed."                   # :116

DOORWAY = (5179, 1137)          # RootdeepVault.cs:97; the landing side of the wall (the Vault starts at x 5178, :42)
ADMIN_SPOT = (5181, 1137)       # on the landing, off the player's and the pet's path through the doorway
ADMIN_ASIDE = (5185, 1137)
CRAWLWAY_MOUTH = (5160, 1159)   # RootdeepVault.cs:103
CRAWLWAY_EXIT = (5160, 1163)    # RootdeepVault.cs:104
STAND = (5160, 1156)            # Strongroom floor south of the pillars (notes: pillars x 5151-5153 and 5167-5169, y 1149-1154)
CRAWLWAY_CLOSED, CRAWLWAY_OPEN = 0x1367, 0x1B71   # RootdeepVault.cs:257-258
OVERDRAFT_BODY = 2057           # VaultCreatures.cs:250
DOG_BODY = 0xD9                 # pinned Mobiles/Animals/Town Critters/Dog.cs:11

# The nine core stations (TutorialStations.cs:59-76). [Tutorial station <key> runs each: core.loop through its demo,
# which completes it (TutorialStations.cs:179, ClusterFLoops.cs:64); the others through the stub, which completes it
# (ClusterFTutorial.cs:123-125). Graduate then passes "Finish the core first." (:200-203).
CORE_KEYS = ("core.movement", "core.paperdoll", "core.backpack", "core.chat", "core.death", "core.banking",
             "core.travel", "core.league", "core.loop")


# -- helpers ---------------------------------------------------------------------------------------------------------

def go_xy(admin, x, y):
    """The admin [go x y (two arguments: the z is the map's GetAverageZ, pinned Handlers.cs:537). Returns the z the
    server put the admin at: the floor's z, read from the overlay the test shard mounts."""
    admin.command("[go %d %d" % (x, y), 0.8)
    if not admin.wait(lambda: admin.pos and admin.pos[:2] == (x, y), 6):
        raise RuntimeError("the admin's [go %d %d did not land there (at %s)" % (x, y, admin.pos))
    return admin.pos[2]


def put(admin, who, x, y, z):
    """The admin [set X Y Z on `who` (Mobile.X/Y/Z are GameMaster-settable, pinned Mobile.cs:2647-2662). A property set
    never asks OnMoveInto, so this puts a character on the doorway tile without walking it. The player is already on
    Trammel: a new character starts in Haven, Trammel (CharacterCreation.cs:83), and the Burrows are Trammel
    (OldHavenBurrowsRegion.cs:60)."""
    set_on(admin, who.serial, "X %d Y %d Z %d" % (x, y, z), who)
    if not who.wait(lambda: who.pos and who.pos[:2] == (x, y), 6):
        raise RuntimeError("[set X %d Y %d Z %d left %s at %s" % (x, y, z, who.label, who.pos))


def tutorial(admin, who, verb, reply):
    """[Tutorial <verb>, targeting `who` (ClusterFTutorial.cs:246-247); returns the admin's reply line matching
    `reply`, or None (then the evidence shows the last lines the admin got)."""
    n = len(admin.messages)
    staff_target(admin, "[Tutorial " + verb, who)
    return wait_text(admin, reply, n, 5)


def make_trainee(admin, who):
    """reset ("Tutorial record cleared.", :272-274) then enter ("Entered.", :257-258): `who` is the account's
    participant (InCore, CharacterSerial = who; TryEnter, :68-83)."""
    cleared = tutorial(admin, who, "reset", r"Tutorial record cleared")
    entered = tutorial(admin, who, "enter", r"^Entered\.")
    return cleared, entered


def walk_west(c, steps=3):
    """Walks west from the doorway tile: the first step may only turn (client.py:633-641), the next asks the Vault's
    OnMoveInto for 5178 (RootdeepVault.cs:63-76). Returns (accepted steps, position, the messages that arrived)."""
    n = len(c.messages)
    accepted = c.walk("w", steps)
    c.sleep(1.0)
    return accepted, c.pos, c.texts_since(n)


def crawlway_seen(c):
    """The RootdeepCrawlway on the mouth tile as `c` sees it: (serial, graphic) or None."""
    for s, it in c.world_items_near(CRAWLWAY_MOUTH[0], CRAWLWAY_MOUTH[1], 0).items():
        if it.get("graphic") in (CRAWLWAY_CLOSED, CRAWLWAY_OPEN):
            return s, it.get("graphic")
    return None


# -- P141-2: the doorway ----------------------------------------------------------------------------------------------

@check("P141-2", "The Vault doorway: a trainee and its pet walk in; a graduate and a skipper are turned back with the Echo's lines")
def p141_2(ctx):
    # One fresh character in three states, each put on the doorway tile 5179,1137 and walked west:
    #   skipper  ([Tutorial reset, enter, skip: State Skipped, :149-167): refused, NOT_PARTICIPANT (MayEnter, :146-149);
    #   trainee  (reset, enter: InCore): walks in to x <= 5178, and its following dog comes through too (:132, :146);
    #   graduate (nine core stations, then graduate: State Graduated, :193-219): refused, GRADUATE (:148).
    # The play test makes three characters; the states are per account, so one character through all three is the same
    # three refusal/entry decisions, and the evidence says which state each walk was made in.
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=True, label="p141")
    try:
        z = go_xy(a, *ADMIN_SPOT)
        parts = []

        # 1. The skipper.
        cleared, entered = make_trainee(a, c)
        skipped = tutorial(a, c, "skip", r"^Skipped\.")
        put(a, c, DOORWAY[0], DOORWAY[1], z)
        _, pos_skip, said_skip = walk_west(c)
        line_skip = next((t for t in said_skip if NOT_PARTICIPANT in t), None)
        ok_skip = pos_skip[:2] == DOORWAY and line_skip is not None
        parts.append("skipper (reset %s, enter %s, skip %s): stayed at %s, said %r" % (
            bool(cleared), bool(entered), bool(skipped), pos_skip[:2], line_skip or said_skip[-2:]))

        # 2. The trainee, with a dog that follows it.
        cleared, entered = make_trainee(a, c)
        if c.pos[:2] != DOORWAY:
            put(a, c, DOORWAY[0], DOORWAY[1], z)
        dog = make_pet(a, c, "Dog", DOG_BODY)
        if dog:
            c.pet_command("all follow me")      # BaseAI's "all follow me" (keyword 0x16C, packets.py:196): no target
        go_xy(a, *ADMIN_ASIDE)                  # off the dog's path through the two-tile doorway
        _, pos_train, said_train = walk_west(c)
        dog_x = c.wait(lambda: (c.mobiles.get(dog, {}).get("x") or 9999) <= 5178 and c.mobiles[dog]["x"], 10) if dog else None
        refused_train = [t for t in said_train if t.startswith("System:")]
        ok_train = pos_train[0] <= 5178 and bool(dog_x) and not refused_train
        parts.append("trainee (reset %s, enter %s): walked to %s, dog 0x%X at x %s, Echo lines %s" % (
            bool(cleared), bool(entered), pos_train[:2], dog or 0, dog_x if dog_x else (c.mobiles.get(dog, {}).get("x") if dog else "no dog"),
            refused_train or "none"))

        # 3. The graduate: back on the doorway tile, the core done by staff, graduated.
        put(a, c, DOORWAY[0], DOORWAY[1], z)
        done = 0
        for key in CORE_KEYS:
            n = len(c.messages)
            staff_target(a, "[Tutorial station " + key, c)
            if wait_text(c, r"^System: Station done:", n, 5):     # ClusterFTutorial.cs:137
                done += 1
        graduated = tutorial(a, c, "graduate", r"^Graduated\.|Finish the core first|not in the tutorial")
        _, pos_grad, said_grad = walk_west(c)
        line_grad = next((t for t in said_grad if GRADUATE in t), None)
        ok_grad = graduated is not None and graduated.startswith("Graduated") and pos_grad[:2] == DOORWAY and line_grad is not None
        parts.append("graduate (%d/9 stations done, graduate said %r): stayed at %s, said %r" % (
            done, graduated, pos_grad[:2], line_grad or said_grad[-2:]))

        ok = ok_skip and ok_train and ok_grad
        ev = "doorway z %d; " % z + "; ".join(parts)

        # -- P141-3 on the same character, handed over through SHARED.
        try:
            SHARED.setdefault("p141_3", []).append(_p141_3(a, c))
        except Exception as e:  # its setup failed: P141-3 reports the ERROR, P141-2 keeps its own result
            SHARED.setdefault("p141_3", []).append(("error", "%s: %s" % (type(e).__name__, e)))
        return ok, ev
    finally:
        ctx.done(c)


# -- P141-3: the crawlway ---------------------------------------------------------------------------------------------

def _p141_3(a, c):
    """A trainee kills the Overdraft in the Strongroom: the rocks on 5160,1159 become the hole (0x1367 -> 0x1B71,
    RootdeepVault.cs:273-285, from OnOverdraftDeath :191-197 via TheOverdraft.OnDeath, VaultCreatures.cs:286), the Echo
    says the crawlway opened (:185), and a step onto the mouth puts the player at 5160,1163 (OnMoveOver :296 ->
    TryCrawl :204-229) with the crawl line (:227). Returns (ok, evidence)."""
    cleared, entered = make_trainee(a, c)      # a participant again, StationsDone empty (no credit yet)
    # A fighter that lands the one hit: Wrestling and Tactics 100, 175 hits (checks._p119_fight's numbers). The Overdraft
    # ignores damage from a non-participant (TutorialCreature.cs:40-48), so the enter above is what makes the kill count.
    for sk in ("Wrestling", "Tactics"):
        staff_target(a, "[SetSkill %s 100" % sk, c)
    staff_target(a, "[set RawStr 250 RawDex 100 Hits 175", c)
    z = go_xy(a, *STAND)
    put(a, c, STAND[0], STAND[1], z)

    # The crawlway on the mouth tile: found, or added where the seeder would put it (ClusterFRootdeepSeeder.cs:199-200).
    found = c.wait(lambda: crawlway_seen(c), 4)
    added = False
    if not found:
        near(a, c)
        a.command("[add RootdeepCrawlway", 0.2)
        if not a.wait_target(8):
            raise RuntimeError("[add RootdeepCrawlway gave no target cursor")
        a.target_xyz(CRAWLWAY_MOUTH[0], CRAWLWAY_MOUTH[1], z)
        found = c.wait(lambda: crawlway_seen(c), 8)
        added = True
    if not found:
        return False, "no RootdeepCrawlway on %s as the player sees it, and [add RootdeepCrawlway placed none there" % (CRAWLWAY_MOUTH,)
    crawl, before = found
    waited = 0
    if before == CRAWLWAY_OPEN:
        # Still open from the previous run (2 minutes after the last Overdraft death, RootdeepVault.cs:107): wait it out.
        closed = c.wait(lambda: c.items.get(crawl, {}).get("graphic") == CRAWLWAY_CLOSED, 125)
        waited = 1
        before = c.items.get(crawl, {}).get("graphic")
        if not closed:
            return False, "the crawlway was already open (0x%X) and did not close within 125 s" % before

    mob = spawn(a, c, "TheOverdraft", OVERDRAFT_BODY)
    if not mob:
        raise RuntimeError("[add TheOverdraft showed no body %d beside the player" % OVERDRAFT_BODY)
    n = len(c.messages)
    outcome = fair_kill(a, c, mob, 60)
    opened = c.wait(lambda: c.items.get(crawl, {}).get("graphic") == CRAWLWAY_OPEN, 8)
    after = c.items.get(crawl, {}).get("graphic")
    line_open = wait_text(c, re.escape(CRAWLWAY_OPENS), n, 4)

    # Through it: south from 5160,1156 onto the mouth; the step is cancelled and the player is moved to the exit.
    n = len(c.messages)
    c.walk("s", 5)
    arrived = c.wait(lambda: c.pos and c.pos[:2] == CRAWLWAY_EXIT, 6)
    pos = c.pos
    line_crawl = wait_text(c, re.escape(CRAWL), n, 4)

    # Evidence only (fact V4): the doorway now refuses this credited player with the sealed line (:152-155).
    sealed = None
    try:
        zd = go_xy(a, *ADMIN_SPOT)
        put(a, c, DOORWAY[0], DOORWAY[1], zd)
        _, pos_sealed, said = walk_west(c)
        sealed = (pos_sealed[:2], next((t for t in said if SEALED in t), None))
    except RuntimeError as e:
        sealed = ("not tried", str(e))

    ok = outcome == "killed" and before == CRAWLWAY_CLOSED and bool(opened) and after == CRAWLWAY_OPEN and bool(arrived)
    ev = ("trainee (reset %s, enter %s); crawlway 0x%X %s; rocks before 0x%X%s, after the kill 0x%s; Overdraft: %s; "
          "Echo on the kill %r; after the step south at %s (want %s); crawl line %r; doorway afterwards: %s") % (
        bool(cleared), bool(entered), crawl, "added by the admin (none stood there)" if added else "already there",
        before or 0, " (waited for the previous run's hole to close)" if waited else "", ("%X" % after) if after else "none",
        outcome, line_open, pos[:2] if pos else None, CRAWLWAY_EXIT, line_crawl, sealed)
    return ok, ev


@check("P141-3", "The Overdraft's death opens the crawlway (0x1367 to 0x1B71); a step onto it lands the killer at 5160,1163",
       kind="BOT-PARTIAL", left="P141-3: the cave-in and the dust read right")
def p141_3(ctx):
    runs = SHARED.get("p141_3", [])
    if not runs:
        raise RuntimeError("P141-2 did not run first (its character does this item too)")
    ok, ev = runs.pop(0)
    if ok == "error":
        raise RuntimeError("setup failed during the matching P141-2 run: " + ev)
    return ok, ev + " (observed during the matching P141-2 run)"
