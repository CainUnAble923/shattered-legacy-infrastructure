"""cc-P224: the shared helpers for the checks written on 2026-10-20 for the second half of the "Not built yet" items, and
the registration of the group modules (checks_p224_*.py). checks.py imports this module at its end, so every check here
lands in checks.REGISTRY the same way the cc-P91 ones do.

Conventions, as in checks.py: a check is `@check(id, title, kind, left)` on a function taking ctx and returning
(passed, evidence); it cites the code under test for every string it asserts; it only adds items and characters.
"""
import re
import time

from . import packets as P
from .checks import (check, entries, plain, all_text, buttons, near, give, staff_target, set_skill,  # noqa: F401
                     try_equip, REGISTRY, SHARED)

# Pinned Server/Skills.cs (SkillName order), for 0x12 use-skill and the 0x3A updates.
SKILL = {
    "Alchemy": 0, "Anatomy": 1, "AnimalLore": 2, "ItemID": 3, "ArmsLore": 4, "Parry": 5, "Begging": 6, "Blacksmith": 7,
    "Fletching": 8, "Peacemaking": 9, "Camping": 10, "Carpentry": 11, "Cartography": 12, "Cooking": 13,
    "DetectHidden": 14, "Discordance": 15, "EvalInt": 16, "Healing": 17, "Fishing": 18, "Forensics": 19, "Herding": 20,
    "Hiding": 21, "Provocation": 22, "Inscribe": 23, "Lockpicking": 24, "Magery": 25, "MagicResist": 26, "Tactics": 27,
    "Snooping": 28, "Musicianship": 29, "Poisoning": 30, "Archery": 31, "SpiritSpeak": 32, "Stealing": 33,
    "Tailoring": 34, "AnimalTaming": 35, "TasteID": 36, "Tinkering": 37, "Tracking": 38, "Veterinary": 39, "Swords": 40,
    "Macing": 41, "Fencing": 42, "Wrestling": 43, "Lumberjacking": 44, "Mining": 45, "Meditation": 46, "Stealth": 47,
    "RemoveTrap": 48, "Necromancy": 49, "Focus": 50, "Chivalry": 51, "Bushido": 52, "Ninjitsu": 53, "Spellweaving": 54,
    "Mysticism": 55, "Imbuing": 56,
}

GHOST_BODIES = (0x192, 0x193, 0x25F, 0x260, 0x2B6, 0x2B7)


def set_skill_to(admin, who, name, value):
    """[SetSkill <name> <value> on `who`, waiting for the 0x3A update of that skill (any skill, unlike checks.set_skill)."""
    sid = SKILL[name]
    staff_target(admin, "[SetSkill %s %s" % (name, value), who)
    return who.wait(lambda: who.skills.get(sid, {}).get("value") == float(value) or who.skills.get(sid, {}).get("base") == float(value), 6)


def skill_value(who, name):
    return (who.skills.get(SKILL[name]) or {}).get("value")


def use_skill(who, name, settle=0.3):
    """Uses a skill (0x12 0x24, Skills.UseSkill); returns the target cursor if one came within 6 s, else None."""
    who.target = None
    who.send(P.use_skill(SKILL[name]))
    return who.wait_target(6) if settle is not None else None


def spawn(admin, who, type_name, bodies=None, settle=8, where=None):
    """Admin [add <type> beside `who` (or at `where`), returning the new mobile's serial as `who` sees it (its 0x78), or
    None. `bodies`: the body or bodies to expect, else any new mobile that is not `who`."""
    if isinstance(bodies, int):
        bodies = (bodies,)
    near(admin, who)
    before = set(who.mobiles)
    admin.command("[add " + type_name, 0.2)
    if not admin.wait_target(8):
        raise AssertionError("[add %s gave no target cursor" % type_name)
    x, y, z = (where or who.pos)[:3]
    admin.target_xyz(x + 1, y, z)
    return who.wait(lambda: next((s for s, m in who.mobiles.items() if s not in before and s != who.serial
                                  and (bodies is None or m.get("body") in bodies)), None), settle)


def add_item_here(admin, who, type_name, settle=8, dx=1, dy=0):
    """Admin [add <type> on the ground beside `who`; returns the world item's serial (0xF3) as `who` sees it, or None."""
    near(admin, who)
    before = set(who.items)
    admin.command("[add " + type_name, 0.2)
    if not admin.wait_target(8):
        raise AssertionError("[add %s gave no target cursor" % type_name)
    x, y, z = who.pos[:3]
    admin.target_xyz(x + dx, y + dy, z)
    return who.wait(lambda: next((s for s, it in who.items.items() if s not in before and it.get("world")), None), settle)


def set_on(admin, target_serial, props, who=None):
    """Admin [set <props> targeting a serial the admin can see (`who` is walked to first when given). Speech over 128
    characters is dropped by the server without a word (pinned IncomingMessagePackets.cs:126-129; seen 2026-10-20 with
    a six-pair [set), so a long property list goes as several [set commands of at most 120 characters."""
    if who is not None:
        near(admin, who)
    words = props.split()
    chunks, cur = [], []
    for i in range(0, len(words) - 1, 2):
        pair = words[i:i + 2]
        if cur and len("[set " + " ".join(cur + pair)) > 120:
            chunks.append(" ".join(cur))
            cur = []
        cur += pair
    if len(words) % 2:
        cur += words[-1:]
    if cur:
        chunks.append(" ".join(cur))
    for chunk in chunks:
        admin.command("[set " + chunk, 0.2)
        if not admin.wait_target(8):
            raise AssertionError("[set %s gave no target cursor" % chunk)
        admin.target_obj(target_serial)
        admin.sleep(0.5)


def make_pet(admin, who, type_name, bodies=None):
    """[add <type> beside `who`, then [set Controlled true ControlMaster <who> on it by the admin (Properties.cs: an
    entity property takes a serial), so `who` owns it. Returns the pet's serial as `who` sees it, or None."""
    pet = spawn(admin, who, type_name, bodies)
    if pet:
        set_on(admin, pet, "Controlled true ControlMaster 0x%X" % who.serial, who)
        who.send(P.status_request(who.serial, 4))
        who.sleep(0.8)
    return pet


def pet_of(ctx, owner, type_name):
    """The owner says [PetTrainTest <type>: a tamed, bonded pet at their feet (PetTrainingHelper.cs:1753-1800; staff
    only, so the owner must be the admin). Returns the pet's serial."""
    before = set(owner.mobiles)
    owner.command("[PetTrainTest " + type_name, 1.5)
    return owner.wait(lambda: next((s for s, m in owner.mobiles.items() if s not in before and s != owner.serial), None), 8)


def kill(admin, who, mob):
    """Admin [kill on a mobile `who` sees; waits for it to leave `who`'s view. Returns True when it did."""
    near(admin, who)
    admin.command("[kill", 0.2)
    if admin.wait_target(8):
        admin.target_obj(mob)
    return bool(who.wait(lambda: mob not in who.mobiles or who.mobiles.get(mob, {}).get("hits") == 0, 10))


def fair_kill(admin, who, mob, seconds=60):
    """A player kill: the admin pins the creature at 1 hit and CantWalk, `who` attacks bare-handed. Returns "killed",
    "fighter died" or "timed out" (the shape of checks._wait_kill)."""
    set_on(admin, mob, "CantWalk true Hits 1", who)
    who.sleep(0.4)
    who.send(P.war_mode(True))
    who.sleep(0.3)
    who.send(P.attack(mob))
    end = time.monotonic() + seconds
    out = "timed out"
    while time.monotonic() < end:
        if mob not in who.mobiles:
            out = "killed"
            break
        if who.body in GHOST_BODIES or who.status.get("hits") == 0:
            out = "fighter died"
            break
        who.sleep(1.0)
    who.send(P.war_mode(False))
    return out


def corpse_near(who, before_items, body=None, timeout=8):
    """A corpse (0x2006, amount = the body) that appeared since `before_items`."""
    return who.wait(lambda: next((s for s, it in who.items.items() if s not in before_items and it.get("world")
                                  and it.get("graphic") == 0x2006 and (body is None or it.get("amount") == body)), None), timeout)


def gump_with(c, text, timeout=8, since=None):
    """The first gump (since `since`) whose text or clilocs contain `text` (case-insensitive)."""
    t = text.lower()
    return c.wait_gump(lambda g: any(t in x.lower() for x in all_text(c, g)), timeout, since=since)


def pack_items(who, graphic=None):
    pack = who.backpack()
    return {s: it for s, it in who.items.items() if it.get("container") == pack and (graphic is None or it.get("graphic") == graphic)}


def wait_text(c, pattern, since, timeout=6):
    m = c.find_text(pattern, since=since, timeout=timeout)
    return m["text"] if m else None


def join_guild(ctx, who, key):
    """Joins `who` to a guild by its key through the guild directory ([guild), pressing the row's Join. Returns the
    text that came back, or raises. Not every guild joins from the directory: see ClusterFGuildSystem.OpenGuildHall."""
    raise NotImplementedError("join_guild: see the group module that needs it")


# -- the group modules ---------------------------------------------------------------------------------------------------
# Each module registers its checks on import. A module that fails to import is reported as one ERROR check, so one bad
# group never hides the others' results in the nightly report.
import importlib  # noqa: E402

for _name in ("checks_p224_vbuild", "checks_p224_kitsune", "checks_p224_mule", "checks_p224_imbuing", "checks_p224_pets",
              "checks_p224_thieves", "checks_p224_guilds", "checks_p224_rootdeep", "checks_p224_restart"):
    try:
        importlib.import_module("uobot." + _name)
    except ImportError as e:
        if e.name == "uobot." + _name:
            continue            # not written (yet)
        raise
