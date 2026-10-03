import API
import time
import math
import heapq
import re
import os

# =============================================================================
# SL_Mining.py  -  Shattered Legacy: mine every tile in a radius around you
# Stand in a cave or next to a mountain wall and start it. It works each tile
# until it is dry, then moves to the next. Stops when every tile is dry, you
# hit the weight limit, or you run out of tools.
#
# With WANDER on, when your spot is dry it pathfinds to a nearby spot that
# still has ore (ore lives in 8x8 tile blocks, so one "no metal here" means
# that whole block is empty and gets skipped).
#
# Control window (GUI = True): Start / Pause / Stop, Set Forge, Set Satchel,
# Smelt Now, and live status. When you get heavy it walks to your forge,
# smelts, puts the bars in your satchel, and walks back to keep mining.
#
# Survey (v15): Shattered Legacy ore veins never move, so the script works out
# which ore every 8x8 block holds, reads your Prospector's Logbook, and steers
# you toward colored veins your skill can mine and your logbook doesn't have
# yet (new veins pay out at the Survey Archivist). "Survey" lists and marks the
# best veins nearby; "Go to vein" walks you to the top one.
#
# Caves and dungeons (v16): knows every tile the shard lets you mine, cave
# floors included, and walks through teleporters (ModernUO's standard list plus
# any you walk through with the window open) on forge trips, e.g. in and out of
# the New Haven mine.
#
# Set DIAGNOSE = True for one run to list every tool it can see (graphic, hue,
# name) if it still says "No pickaxe or shovel".
# =============================================================================

# ---- CONFIG -----------------------------------------------------------------
RADIUS          = 2         # 1 = the 8 tiles touching you, 2 = 5x5 box, etc.
SWING_DELAY     = 4.0       # longest to wait for a swing's result (ModernUO takes ~1.6 s + lag;
                            # it moves on as soon as the result shows up)
MAX_SWINGS_TILE = 30        # safety cap per tile
WEIGHT_STOP     = 90        # % of max weight -> stop
LOOP_FOREVER    = False     # True = start over after a pause once all tiles are dry
RESPAWN_WAIT    = 600       # seconds to wait before re-mining when LOOP_FOREVER
DIAGNOSE        = False

# pathfinding to the next vein
WANDER          = True      # walk to a fresh spot when this one is dry
WANDER_RANGE    = 16        # never go further than this from where you started
MAX_MOVES       = 25        # stop after this many moves (0 = no limit)
MIN_FRESH       = 3         # a spot needs this many untried tiles around it
RETURN_TO_START = True      # with LOOP_FOREVER, walk back to start before waiting
HUE_DEST        = 88        # blue: where it is walking to
MOVE_DEBUG      = True      # print what the mover is doing in the journal

# how it picks the next spot (higher weight = matters more)
LOOKAHEAD       = 4         # also count unmined rock this far past a spot
W_FRESH         = 1.0       # untried rock tiles in reach of the spot
W_AHEAD         = 0.15      # unmined rock in the area beyond the spot
W_DIST          = 1.0       # penalty per tile walked
W_HEADING       = 2.0       # bonus for keeping the same direction as last move
BANK_MEMORY_MIN = 15        # remember empty ore blocks this long, even across runs

# tile highlighting (TazUO MarkTile). Hues are standard UO hue numbers.
SHOW_MARKS      = True
HUE_ACTIVE      = 53        # yellow: tile being mined right now
HUE_ORE         = 68        # green (iron); colored ore shows in its own ore color: tile gave ore
HUE_DRY         = 33        # red: tile is empty
SHOW_PROGRESS   = True      # "Tile 7/25" over your head after each tile
MARK_SECONDS    = 20        # green/red marks fade after this many seconds (0 = keep)
END_LINGER      = 5         # seconds marks stay up after the script finishes

# forge trips and satchel
GUI             = True      # show the control window (False = old start-and-go mode)
AUTO_SMELT      = True      # walk to the forge when heavy (needs Set Forge)
SMELT_AT        = 75        # % of max weight -> go smelt / stash
FORGE_STAND     = 2         # how close to stand to the forge
FORGE_MAX_DIST  = 150       # refuse a forge farther than this
HOP             = 12        # long walks are split into hops this long
CRUMB_STEP      = 5         # remember a walked tile every this many tiles
MAX_CRUMBS      = 600       # walked tiles remembered per character
SMELT_DELAY     = 1.3       # seconds per smelt
STASH_ORE       = False     # also put raw ore in the satchel while mining
ORE_GRAPHICS    = [0x19B7, 0x19B8, 0x19B9, 0x19BA]
INGOT_GRAPHICS  = [0x1BEF, 0x1BF0, 0x1BF1, 0x1BF2, 0x1BF3, 0x1BF4]
STASH_EXTRA     = []        # other graphics to put in the satchel (gems, granite...)

# making new tools (Tinkering) and Jacob's Pickaxe
MAKE_TOOLS      = True      # craft more when you run low (needs Tinkering + iron ingots)
MAKE_WHEN_LEFT  = 1         # craft when this many spare tools (not counting Jacob's) are left
MAKE_COUNT      = 2         # how many to make each time
MAKE_WHAT       = "pickaxe" # "pickaxe" or "shovel"
KEEP_TINKER_SPARE = True    # also make a spare set of tinker's tools before the last one breaks
PREFER_JACOB    = True      # swing Jacob's Pickaxe first while it has uses
JACOB_RESERVE   = 0         # stop using Jacob's at this many uses (Shattered Legacy never
                            # destroys it: at 0 it turns charcoal and goes back to the Liaison)
JACOB_DEAD_HUE  = 0x0415    # charcoal = exhausted Jacob's, restore at the Miners' Compact Liaison
EQUIP_JACOB     = False     # hold Jacob's in your hands while mining (Prospector's Insight
                            # vouchers and Deepdelver/Worldbreaker bonus ore only count when held)
JACOB_RECHECK   = 60        # seconds between checks for Jacob's recharging
TOOL_REST       = 120       # seconds to set aside any tool that stops working (doubles if it fails again)

# log file: everything the script says and does, for troubleshooting
LOG_TO_FILE     = True      # writes SL_Mining.log next to the script
LOG_MAX_KB      = 2048      # then starts over (the old one is kept as SL_Mining.log.old)

# survey: vein map + Prospector's Logbook
SEEK_VEINS      = True      # steer toward colored veins (window checkbox)
W_VEIN          = 1.5       # bonus per untried tile in a colored vein you can mine
W_NEWVEIN       = 2.5       # extra bonus per tile in a vein your logbook doesn't have yet
NEW_VEIN_DIST   = 12        # the logbook counts a vein as new this far from a logged one
SURVEY_RANGE    = 48        # how far Survey looks
SURVEY_SHOW     = 6         # veins Survey lists
SURVEY_MARKS    = 60        # seconds Survey's colored marks stay up
SHOW_ORE_MAP    = False     # tint mineable tiles in their block's ore color (window checkbox)
ORE_MAP_RANGE   = 2         # blocks around you to tint (2 = a 5x5 block area)
ORE_MAP_BATCH   = 25        # new tiles checked per step (each one costs a game frame)
AUTO_RELOCATE   = 4         # when the area is dry, walk to this many surveyed veins (0 = off)
LOGBOOK_GRAPHIC = 0x1C11
SATCHEL_GRAPHIC = 0xA272    # Compact Ore Satchel: found and opened automatically

# pickaxe (both facings), shovel (both facings), gargoyle pickaxe
TOOL_GRAPHICS   = [0x0E85, 0x0E86, 0x0F39, 0x0F3A, 0x48B2, 0x48B3]
TOOL_NAMES      = ["pickaxe", "pick axe", "shovel", "mattock"]
HAND_LAYERS     = ["OneHanded", "TwoHanded"]
# -----------------------------------------------------------------------------

EMPTY_MSGS = ["There is no metal here to mine", "no metal here",
              "Someone has gotten to the metal before you"]
BAD_MSGS   = ["You can't mine there", "You can't mine that",
              "Try mining in rock", "Try mining elsewhere",
              "That is too far away", "Target cannot be seen",
              "You have no line of sight", "You cannot mine",
              "You have moved too far away"]
FULL_MSGS = ["You are overloaded", "cannot hold more weight",
             "Your backpack is full", "You can't carry"]
GOT_MSGS  = ["You dig some", "and put it in your backpack",
             "You loosen some rocks",                  # a miss, but the vein still has ore
             "You carefully extract some workable stone"]
BUSY_MSGS = ["You must wait to perform another action", "You are already"]
BROKE_MSG = ["You have worn out your tool", "worn out"]


MARKED = {}                 # (x, y) -> time the mark expires (None = while active)
MARK_VAR = "SL_Mining_Marks"


def _save_marks():
    # remember what is on the ground so the next run can clean it up
    try:
        API.SetSharedVar(MARK_VAR, ";".join("{},{}".format(x, y) for x, y in
                                            list(MARKED) + list(ORE_MAP["tiles"])))
    except Exception:
        pass


def _unmark(x, y):
    MARKED.pop((x, y), None)
    hue = ORE_MAP["tiles"].get((x, y)) if ORE_MAP["on"] else None
    try:
        if hue:
            API.MarkTile(x, y, hue)     # back to the ore-map tint
        else:
            API.RemoveMarkedTile(x, y)
    except Exception:
        pass


def mark(dx, dy, hue, lasting=True):
    if not opt("marks"):
        return
    x = API.Player.X + dx
    y = API.Player.Y + dy
    try:
        API.MarkTile(x, y, hue)
    except Exception:
        return
    MARKED[(x, y)] = (time.time() + MARK_SECONDS) if (lasting and MARK_SECONDS > 0) else None
    _save_marks()


def mark_at(x, y, hue, secs):
    if not opt("marks"):
        return
    try:
        API.MarkTile(x, y, hue)
    except Exception:
        return
    MARKED[(x, y)] = time.time() + secs
    _save_marks()


def expire_marks():
    now = time.time()
    if CTRL.get("arrow_until") and now > CTRL["arrow_until"]:
        CTRL["arrow_until"] = 0
        try:
            API.TrackingArrow(-1, -1)
        except Exception:
            pass
    gone = [k for k, t in MARKED.items() if t is not None and t <= now]
    for x, y in gone:
        _unmark(x, y)
    if gone:
        _save_marks()


def clear_marks():
    for x, y in list(MARKED):
        _unmark(x, y)
    clear_ore_map()
    _save_marks()


def clear_leftover_marks():
    # marks from a previous run that stopped before it could clean up
    try:
        saved = API.GetSharedVar(MARK_VAR) or ""
    except Exception:
        saved = ""
    for pair in str(saved).split(";"):
        if "," in pair:
            try:
                x, y = pair.split(",")
                API.RemoveMarkedTile(int(x), int(y))
            except Exception:
                pass
    try:
        API.SetSharedVar(MARK_VAR, "")
    except Exception:
        pass


# ---- log file -------------------------------------------------------------------
LOG = {"path": None, "failed": False}


def _log_path():
    cands = []
    try:
        cands.append(os.path.dirname(os.path.abspath(__file__)))
    except Exception:
        pass
    try:
        cwd = os.getcwd()
        cands += [os.path.join(cwd, "LegionScripts"), cwd]
    except Exception:
        pass
    for d in cands:
        if d and os.path.isdir(d) and os.path.exists(os.path.join(d, "SL_Mining.py")):
            return os.path.join(d, "SL_Mining.log")
    for d in cands:
        if d and os.path.isdir(d):
            return os.path.join(d, "SL_Mining.log")
    return "SL_Mining.log"


def log(msg):
    if not LOG_TO_FILE or LOG["failed"]:
        return
    try:
        if LOG["path"] is None:
            LOG["path"] = _log_path()
            try:
                if os.path.getsize(LOG["path"]) > LOG_MAX_KB * 1024:
                    old = LOG["path"] + ".old"
                    if os.path.exists(old):
                        os.remove(old)
                    os.rename(LOG["path"], old)
            except Exception:
                pass
        try:
            where = "{} {},{}".format(API.GetMap(), API.Player.X, API.Player.Y)
        except Exception:
            where = "?"
        with open(LOG["path"], "a") as f:
            f.write("{} [{}] {}\n".format(time.strftime("%Y-%m-%d %H:%M:%S"), where, msg))
    except Exception as e:
        LOG["failed"] = True
        try:
            API.SysMsg("[Mining] Can't write the log file: {}".format(e), 33)
        except Exception:
            pass


def sysmsg(msg, hue=946):
    log(msg)
    API.SysMsg(msg, hue)


# ---- run control (driven by the window) --------------------------------------
class Halt(Exception):
    pass


CTRL = {"running": False, "paused": False, "quit": False, "want": [],
        "busy": False, "state": "Idle", "last_ui": 0.0, "session": False,
        "stash_refused": False}


def set_state(text):
    if CTRL.get("state") != text:
        log("state: " + text)
    CTRL["state"] = text
    gui_tick(force=True)


def checkpoint():
    """Called between every action: keeps the window alive, handles buttons,
    holds while paused, and bails out when Stop is pressed."""
    API.ProcessCallbacks()
    track_position()
    expire_marks()
    ore_map_tick()
    handle_wants()
    gui_tick()
    while CTRL["paused"] and CTRL["running"] and not CTRL["quit"] and not API.StopRequested:
        API.ProcessCallbacks()
        handle_wants()
        gui_tick()
        API.Pause(0.2)
    if API.StopRequested or CTRL["quit"]:
        raise Halt()
    if CTRL["session"] and not CTRL["running"]:
        raise Halt()


def on_stop():
    clear_marks()
    try:
        API.TrackingArrow(-1, -1)
    except Exception:
        pass
    sysmsg("SL_Mining stopped.", 33)

API.OnStop(on_stop)


def item_name(item):
    name = item.Name or ""
    if not name:
        try:
            data = item.GetItemData()
            if data and data.Name:
                name = data.Name
        except Exception:
            pass
    return name.lower()


def is_tool(item):
    if item is None:
        return False
    if item.Graphic in TOOL_GRAPHICS:
        return True
    n = item_name(item)
    return any(t in n for t in TOOL_NAMES)


TINKER_GRAPHICS = [0x1EB8, 0x1EB9, 0x1EBC]
JACOB = {"serial": 0, "uses": -1, "exhausted": False, "checked": 0.0,
         "warned_low": False, "seen": False, "gone_noted": False, "dead": False}
DEAD_TOOLS = set()          # exhausted Jacob's copies: never swing them again
NOTES = []                  # tool events shown in the window


def note(msg, hue=43):
    sysmsg("[Mining] " + msg, hue)
    API.HeadMsg(msg, API.Player, hue)
    NOTES.append(msg)
    del NOTES[:-3]


def pack_all():
    items = API.ItemsInContainer(API.Backpack, True) or []
    if not items:
        # client only knows contents of opened containers
        API.UseObject(API.Backpack)
        API.Pause(1.0)
        items = API.ItemsInContainer(API.Backpack, True) or []
    return items


TOOL_TEXT = {}              # serial -> tooltip text (cached)
REST = {}                   # serial -> [until_time, seconds]
STRIKES = {}                # serial -> swings with no recognizable result
CUR_TOOL = [0]


def tooltip(item, refresh=False):
    s_ = item.Serial
    if refresh or not TOOL_TEXT.get(s_):
        try:
            if refresh:
                API.RequestOPLData([s_])
                API.Pause(0.4)
            first = s_ not in TOOL_TEXT
            TOOL_TEXT[s_] = (API.ItemNameAndProps(s_, first and not refresh, 1) or "").lower()
        except Exception:
            TOOL_TEXT[s_] = TOOL_TEXT.get(s_, "")
    return TOOL_TEXT[s_]


def is_jacob(item):
    # the client often shows plain "pickaxe" until the tooltip arrives, so check both
    return item is not None and ("jacob" in item_name(item) or "jacob" in tooltip(item))


def rest_tool(serial, why):
    """Set a tool aside; each repeat doubles how long."""
    now = time.time()
    prev = REST.get(serial)
    secs = min(prev[1] * 2, 1200) if prev else TOOL_REST
    if serial == JACOB["serial"]:
        secs = max(secs, JACOB_RECHECK)
    REST[serial] = [now + secs, secs]
    STRIKES.pop(serial, None)
    return secs


def resting(serial):
    r = REST.get(serial)
    return bool(r) and time.time() < r[0]


def uses_left(item, wait=False, refresh=False):
    """Uses remaining from the item's tooltip, or -1 if unknown."""
    props = tooltip(item, refresh=refresh or not wait)
    m = re.search(r"uses remaining:?\s*(\d+)", props, re.I)
    return int(m.group(1)) if m else -1


def jacob_dead(item):
    """Shattered Legacy swaps a used-up Jacob's for a charcoal copy that can't mine."""
    if item.Serial in DEAD_TOOLS:
        return True
    if (item.Hue or 0) == JACOB_DEAD_HUE or "exhausted" in tooltip(item):
        mark_jacob_dead(item.Serial)
        return True
    return False


def mark_jacob_dead(serial):
    DEAD_TOOLS.add(serial)
    REST.pop(serial, None)
    if not JACOB["dead"]:
        note("Jacob's Pickaxe is exhausted. Restore it at the Miners' Compact Liaison.", 33)
    JACOB.update({"dead": True, "seen": True, "gone_noted": True, "exhausted": True})


def mining_tools():
    """All harvest tools in hand and pack: (jacobs_or_None, [other tools])."""
    found = []
    for layer in HAND_LAYERS:
        try:
            held = API.FindLayer(layer)
        except Exception:
            held = None
        if is_tool(held):
            found.append(held)
    for item in pack_all():
        if is_tool(item) and all(item.Serial != f.Serial for f in found):
            found.append(item)
    jac = None
    others = []
    for t in found:
        if is_jacob(t):
            if jacob_dead(t):
                continue
            jac = t
            JACOB["dead"] = False
        elif not resting(t.Serial):
            others.append(t)
    return jac, others


def jacob_usable(jac, force=False):
    """Track Jacob's Pickaxe: note when it runs low, runs out, recharges or is gone."""
    if jac is None:
        if JACOB["seen"] and not JACOB["gone_noted"] and not JACOB["dead"]:
            note("Jacob's Pickaxe is gone (it breaks at 0 uses). Get it reissued at the guild.", 33)
            JACOB["gone_noted"] = True
        return False
    JACOB["seen"] = True
    JACOB["gone_noted"] = False
    now = time.time()
    if JACOB["serial"] != jac.Serial:
        JACOB.update({"serial": jac.Serial, "uses": -1, "exhausted": False, "checked": 0.0})
    if JACOB["exhausted"] and resting(jac.Serial) and not force:
        return False            # still inside its rest window: don't even look
    if force or now - JACOB["checked"] > (JACOB_RECHECK if JACOB["exhausted"] else 10):
        JACOB["checked"] = now
        u = uses_left(jac, wait=JACOB["uses"] < 0, refresh=JACOB["exhausted"])
        if u < 0 and JACOB["exhausted"]:
            JACOB["exhausted"] = False      # can't read uses: just try it again
        if u >= 0:
            JACOB["uses"] = u
            if JACOB["exhausted"] and u > JACOB_RESERVE:
                JACOB["exhausted"] = False
                note("Jacob's Pickaxe recharged ({} uses). Back to using it.".format(u), 68)
            elif JACOB["exhausted"]:
                rest_tool(jac.Serial, "still empty")
            elif not JACOB["exhausted"] and u <= JACOB_RESERVE:
                JACOB["exhausted"] = True
                rest_tool(jac.Serial, "reserve")
                note("Jacob's Pickaxe is exhausted ({} uses left). Saving it.".format(u), 33)
            elif not JACOB["warned_low"] and u <= JACOB_RESERVE + 3 and not JACOB["exhausted"]:
                JACOB["warned_low"] = True
                note("Jacob's Pickaxe is low: {} uses left.".format(u), 43)
            if u > JACOB_RESERVE + 3:
                JACOB["warned_low"] = False
    return not JACOB["exhausted"]


def jacob_ran_dry(serial=0):
    """The server said the tool needs to recharge (that is Jacob's Pickaxe)."""
    if serial:
        JACOB["serial"] = serial
        JACOB["seen"] = True
        TOOL_TEXT[serial] = TOOL_TEXT.get(serial, "") + " jacob"
    secs = rest_tool(JACOB["serial"] or serial, "recharge")
    if not JACOB["exhausted"]:
        JACOB["exhausted"] = True
        JACOB["checked"] = time.time()
        note("Jacob's Pickaxe is exhausted (needs to recharge). Switching tools.", 33)
    else:
        dbg("Jacob's still recharging, resting it {}s".format(int(secs)))


OPENED_BAGS = [0.0]


def open_bags():
    """The client only knows what is in bags that were opened: open the ones in the pack."""
    if time.time() - OPENED_BAGS[0] < 60:
        return False
    OPENED_BAGS[0] = time.time()
    opened = 0
    for it in pack_all():
        try:
            is_bag = bool(it.IsContainer)
        except Exception:
            is_bag = False
        if is_bag and it.Serial != API.Backpack:
            API.UseObject(it.Serial)
            API.Pause(0.6)
            opened += 1
    if opened:
        dbg("Opened {} bag(s) in the pack to look for tools".format(opened))
    return opened > 0


def hold_jacob(jac):
    """EQUIP_JACOB: keep Jacob's in hand so its held-only bonuses apply."""
    if not EQUIP_JACOB or CTRL.get("equip_failed", 0) >= 3:
        return
    for layer in HAND_LAYERS:
        try:
            held = API.FindLayer(layer)
        except Exception:
            held = None
        if held is not None and held.Serial == jac.Serial:
            return
    try:
        API.EquipItem(jac.Serial)
        API.Pause(0.7)
    except Exception:
        pass
    held = None
    try:
        held = API.FindLayer("TwoHanded")
    except Exception:
        pass
    if held is None or held.Serial != jac.Serial:
        CTRL["equip_failed"] = CTRL.get("equip_failed", 0) + 1
        if CTRL["equip_failed"] >= 3:
            note("Could not equip Jacob's (hands full?). Mining from the pack.", 43)


def find_tool():
    jac, others = mining_tools()
    if jac is None and not others and open_bags():
        jac, others = mining_tools()
    use_jac = PREFER_JACOB and jacob_usable(jac)
    if use_jac:
        hold_jacob(jac)
        return jac
    if others:
        return others[0]
    if jac is not None and jacob_usable(jac):
        return jac
    return None


# ---- making tools with Tinkering ------------------------------------------------
# ModernUO craft gump: button id = 1 + type + index * 7 (CraftGump.GetButtonID).
# Tinkering groups: Wooden Items 0, Tools 1 ...  Tools items: tinker's tools 3,
# shovel 10, pickaxe 16. The script reads the real buttons from the gump layout
# first and only falls back to these numbers.
CL_TINKER_MENU = 1044007
CL_TOOLS_GROUP = 1044046
CRAFT_ITEMS = {"pickaxe": (1023718, 16), "shovel": (1023898, 10), "tinker": (1044164, 3)}


def tinker_tools():
    return [it for it in pack_all() if it.Graphic in TINKER_GRAPHICS]


def iron_loose():
    return sum((it.Amount or 1) for it in pack_items(INGOT_GRAPHICS)
               if (it.Hue or 0) == 0 and not (SATCHEL[0] and it.Container == SATCHEL[0]))


def pull_iron(need):
    """Take iron ingots out of the satchel if the pack is short."""
    have = iron_loose()
    if have >= need or not SATCHEL[0]:
        return have >= need
    for it in pack_items(INGOT_GRAPHICS):
        if (it.Hue or 0) == 0 and it.Container == SATCHEL[0]:
            take = min(it.Amount or 1, need - have + 4)
            API.MoveItem(it.Serial, API.Backpack, take)
            API.Pause(0.8)
            have = iron_loose()
            if have >= need:
                break
    return have >= need


def _layout_button(layout, cliloc, button_x):
    """Find the button on the same row (and page) as a localized label in raw gump layout."""
    buttons = {}
    labels = []
    page = 0
    for line in layout.split("\n"):
        p = line.split()
        if not p:
            continue
        try:
            if p[0] == "page" and len(p) > 1:
                page = int(p[1])
            elif p[0] == "button" and len(p) >= 8 and int(p[1]) == button_x:
                buttons[(page, int(p[2]))] = int(p[7])
            elif p[0].startswith("xmfhtml") and len(p) >= 6 and p[5] == str(cliloc):
                labels.append((page, int(p[2])))
        except ValueError:
            continue
    for lp, ly in labels:
        for (bp, by), bid in buttons.items():
            if bp == lp and abs((ly - 3) - by) <= 2:
                return bid
    return None


def _craft_gump(timeout=5.0):
    """Wait for the Tinkering menu and return (gump_id, layout)."""
    t = 0.0
    while t < timeout:
        gid = API.HasGump()
        if gid:
            g = API.GetGump(gid)
            layout = ""
            try:
                layout = g.PacketGumpText or ""
            except Exception:
                pass
            if str(CL_TINKER_MENU) in layout or API.GumpContains("TINKERING", gid):
                return gid, layout
        API.Pause(0.25)
        t += 0.25
    return 0, ""


CRAFT_GRAPHICS = {"pickaxe": [0x0E85, 0x0E86], "shovel": [0x0F39, 0x0F3A],
                  "tinker": TINKER_GRAPHICS}
# notices ModernUO shows inside the craft gump
CL_NO_METAL = ["1044037", "1044253"]     # not enough metal / material
CL_NO_SKILL = ["1044153", "1044263"]     # missing skill / tool not on you


def _how_many(what):
    return sum(1 for it in pack_all() if it.Graphic in CRAFT_GRAPHICS[what] and not is_jacob(it))


def craft(what, count):
    """Make `count` of pickaxe / shovel / tinker. Returns how many were made."""
    cl, idx = CRAFT_ITEMS[what]
    made = 0
    for _ in range(count * 3):          # allow for failures
        if made >= count or API.StopRequested:
            break
        tools = tinker_tools()
        if not tools:
            note("No tinker's tools left, can't make more tools.", 33)
            break
        need = 2 if what == "tinker" else 4
        if not pull_iron(need):
            note("Not enough iron ingots to make a {}.".format(what), 33)
            break
        before = _how_many(what)
        API.ClearJournal()
        API.UseObject(tools[0].Serial)
        gid, layout = _craft_gump()
        if not gid:
            note("The Tinkering menu did not open.", 33)
            break
        grp = _layout_button(layout, CL_TOOLS_GROUP, 15) or (1 + 0 + 1 * 7)
        API.ReplyGump(grp, gid)
        gid, layout = _craft_gump()
        if not gid:
            break
        btn = _layout_button(layout, cl, 220) or (1 + 1 + idx * 7)
        API.ReplyGump(btn, gid)
        # the result comes back as a fresh craft gump with a notice (or a
        # journal line if the tinker's tools broke)
        gid, layout = _craft_gump(8.0)
        API.Pause(0.4)
        if _how_many(what) > before:
            made += 1
        elif any(c in layout for c in CL_NO_METAL):
            note("Not enough metal to make a {}.".format(what), 33)
            break
        elif any(c in layout for c in CL_NO_SKILL) or API.InJournal("required skill"):
            note("Missing the skill to make a {}.".format(what), 33)
            break
    try:
        gid = API.HasGump()
        if gid and API.GumpContains("TINKERING", gid):
            API.CloseGump(gid)
    except Exception:
        pass
    return made


def tinkering_skill():
    try:
        return float(API.GetSkill("Tinkering").Value)
    except Exception:
        return 0.0


def ensure_tools(force=False):
    """Make more tools when spares run low. Returns False if there is nothing to swing."""
    jac, others = mining_tools()
    jacob_ok = jac is not None and jacob_usable(jac)
    if MAKE_TOOLS and (force or len(others) <= MAKE_WHEN_LEFT) and not CTRL["busy"]:
        skill = tinkering_skill()
        if skill < 40.0:
            if not CTRL.get("skill_noted"):
                note("Tinkering {:.1f}: need 40 to make {}s.".format(skill, MAKE_WHAT), 43)
                CTRL["skill_noted"] = True
        else:
            CTRL["busy"] = True
            prev = CTRL["state"]
            try:
                if KEEP_TINKER_SPARE and len(tinker_tools()) == 1:
                    set_state("Making tinker's tools")
                    craft("tinker", 1)
                want_n = MAKE_COUNT if (force or len(others) <= MAKE_WHEN_LEFT) else 0
                if want_n:
                    set_state("Making {}s".format(MAKE_WHAT))
                    n = craft(MAKE_WHAT, want_n)
                    if n:
                        note("Made {} {}{}.".format(n, MAKE_WHAT, "s" if n > 1 else ""), 68)
            finally:
                CTRL["busy"] = False
                set_state(prev)
            jac, others = mining_tools()
    return bool(others) or jacob_ok


def wait_for_jacob():
    """Only Jacob's is left and it is recharging: wait for it instead of quitting."""
    jac, others = mining_tools()
    if jac is None or others:
        return False
    prev = CTRL["state"]
    set_state("Waiting for Jacob's to recharge")
    API.HeadMsg("Waiting for Jacob's Pickaxe to recharge", API.Player, 43)
    while True:
        checkpoint()
        r = REST.get(jac.Serial)
        if not r or time.time() >= r[0]:
            if jacob_usable(jac, force=True):
                set_state(prev)
                return True
        API.Pause(2)


def tools_summary():
    jac, others = mining_tools()
    parts = ["Tools: {} spare".format(len(others))]
    if jac is not None:
        u = JACOB["uses"]
        tag = "resting" if JACOB["exhausted"] else "ok"
        parts.append("Jacob's {} ({})".format(u if u >= 0 else "?", tag))
    elif JACOB["dead"]:
        parts.append("Jacob's EXHAUSTED")
    elif JACOB["seen"]:
        parts.append("Jacob's GONE")
    parts.append("Tinker {}".format(len(tinker_tools())))
    return "   ".join(parts)


def diagnose():
    sysmsg("=== SL_Mining tool check ===", 68)
    for layer in HAND_LAYERS:
        held = API.FindLayer(layer)
        if held:
            sysmsg("{}: 0x{:04X} hue {} {}".format(
                layer, held.Graphic, held.Hue, item_name(held)), 90)
    for item in API.ItemsInContainer(API.Backpack, True) or []:
        n = item_name(item)
        if is_tool(item) or "axe" in n or "pick" in n or "shovel" in n:
            sysmsg("pack: 0x{:04X} hue {} {}".format(
                item.Graphic, item.Hue, n), 90)
    t = find_tool()
    sysmsg("Tool picked: " + (item_name(t) if t else "NONE"), 68 if t else 33)


def weight_pct():
    if API.Player.WeightMax <= 0:
        return 0
    return (API.Player.Weight * 100) / API.Player.WeightMax


# ---- what we have learned this run ------------------------------------------
BANK = 8
DEPLETED = set()        # (bx, by) ore blocks that said "no metal here"
TRIED = {}              # (x, y) -> "ore" / "empty" / "bad"
VISITED = set()         # standing spots already mined from
GOOD_LAND = set()       # land graphics that turned out to be mineable
GOOD_STATIC = set()     # static graphics that turned out to be mineable
BAD_LAND = set()        # land graphics that said "can't mine there"
TILE_CACHE = {}
HEADING = [0.0, 0.0]    # direction of the last successful move
START = [0, 0, 0, 0]    # wander anchor x, y, z, map (moves when "Go to vein" takes you somewhere new)

# The exact tiles the shard's Mining.cs accepts (patches/Mining.cs, OreAndStone
# LandTiles / StaticTiles). Includes cave floors like the New Haven mine's
# 0x0245-0x0249, which you stand on and mine under your feet.
_MINE_LAND_RANGES = [(220, 231), (236, 247), (268, 279), (286, 297), (321, 324),
                     (467, 474), (476, 487), (492, 495), (543, 579), (581, 621),
                     (1741, 1757), (1771, 1790), (1801, 1824), (1831, 1854),
                     (1861, 1884), (1981, 2004), (2028, 2033), (2100, 2105),
                     (0x3F39, 0x3F74), (0x3F82, 0x3F8F), (0x3F91, 0x3FCF)]
_MINE_STATIC_RANGES = [(0x053B, 0x054F)]
MINE_LAND = set()
for _a, _b in _MINE_LAND_RANGES:
    MINE_LAND.update(range(_a, _b + 1))
MINE_STATIC = set()
for _a, _b in _MINE_STATIC_RANGES:
    MINE_STATIC.update(range(_a, _b + 1))
SEED_LAND = MINE_LAND

MEM_VAR = "SL_Mining_Depleted"


def bank(x, y):
    return (x // BANK, y // BANK)


def tile_info(x, y):
    """(land graphic, land impassable, [static graphics], any static impassable, cave static)"""
    key = (x, y)
    if key in TILE_CACHE:
        return TILE_CACHE[key]
    lg, limp, sg, simp, cave = -1, False, [], False, False
    try:
        t = API.GetTile(x, y)
        if t:
            lg = t.Graphic
            limp = bool(t.Impassible)
    except Exception:
        pass
    for g, imp, cv in _statics_at(x, y):
        sg.append(g)
        simp = simp or imp
        cave = cave or cv
    info = (lg, limp, sg, simp, cave)
    if lg >= 0:
        TILE_CACHE[key] = info      # far tiles the client hasn't loaded yet: ask again later
    else:
        STATIC_CHUNKS.pop((x >> 3, y >> 3), None)
    return info


# Every API call that reads the world waits for the game's next frame (~20 ms),
# so statics are read a whole 8x8 block at a time instead of tile by tile.
STATIC_CHUNKS = {}


def _statics_at(x, y):
    b = (x >> 3, y >> 3)
    ch = STATIC_CHUNKS.get(b)
    if ch is None:
        ch = {}
        try:
            for st in API.GetStaticsInArea(b[0] * 8, b[1] * 8, b[0] * 8 + 7, b[1] * 8 + 7) or []:
                ch.setdefault((st.X, st.Y), []).append(
                    (st.Graphic, bool(st.IsImpassible), bool(st.IsCave)))
        except Exception:
            return []
        STATIC_CHUNKS[b] = ch
    return ch.get((x, y), [])


def learn_mineable(x, y, use_land=True):
    lg, _, sg, _, _ = tile_info(x, y)
    if use_land and lg >= 0:
        GOOD_LAND.add(lg)
        BAD_LAND.discard(lg)
    if not use_land and sg:
        GOOD_STATIC.add(sg[-1])


def learn_bad(x, y):
    lg, _, sg, _, _ = tile_info(x, y)
    if lg >= 0 and lg not in GOOD_LAND and not sg:
        BAD_LAND.add(lg)


def land_mineable(lg):
    return lg in MINE_LAND or lg in GOOD_LAND


def static_mineable(sg):
    return any(g in MINE_STATIC or g in GOOD_STATIC for g in sg)


def is_rock(x, y):
    """Is this a tile the server lets you mine (wall, mountain or cave floor)?"""
    lg, _, sg, _, _ = tile_info(x, y)
    return land_mineable(lg) or static_mineable(sg)


def usable(x, y):
    return (x, y) not in TRIED and bank(x, y) not in DEPLETED and is_rock(x, y)


def looks_mineable(x, y):
    return is_rock(x, y)


def is_fresh(x, y):
    return usable(x, y)


# ---- empty-block memory that survives restarting the script ----------------
def _now():
    return int(time.time())


def load_bank_memory():
    try:
        raw = API.GetPersistentVar(MEM_VAR, "", API.PersistentVar.Char) or ""
    except Exception:
        return
    m = API.GetMap()
    cutoff = _now() - BANK_MEMORY_MIN * 60
    n = 0
    for e in str(raw).split(";"):
        parts = e.split(",")
        if len(parts) != 4:
            continue
        try:
            mp, bx, by, t = [int(v) for v in parts]
        except ValueError:
            continue
        if mp == m and t >= cutoff:
            DEPLETED.add((bx, by))
            n += 1
    if n:
        dbg("Remembered {} empty ore blocks from earlier".format(n))


def save_bank(b):
    try:
        raw = API.GetPersistentVar(MEM_VAR, "", API.PersistentVar.Char) or ""
        cutoff = _now() - BANK_MEMORY_MIN * 60
        keep = []
        for e in str(raw).split(";"):
            parts = e.split(",")
            if len(parts) == 4 and parts[3].isdigit() and int(parts[3]) >= cutoff:
                keep.append(e)
        keep.append("{},{},{},{}".format(API.GetMap(), b[0], b[1], _now()))
        API.SavePersistentVar(MEM_VAR, ";".join(keep[-400:]), API.PersistentVar.Char)
    except Exception:
        pass


def deplete(x, y):
    b = bank(x, y)
    if b not in DEPLETED:
        DEPLETED.add(b)
        save_bank(b)
        if ORE_MAP["on"]:
            ore_map_tick(force=True)    # an emptied block turns red


# ---- survey: the vein map and your Prospector's Logbook ------------------------
# Veins on Shattered Legacy never re-roll (RandomizeVeins = false), so the ore in
# each 8x8 block comes from a fixed formula (ModernUO HarvestDefinition.GetVeinAt
# with StableRandom). The script works it out here, checks itself against the
# veins in your logbook and the ore you dig, and turns itself off if they disagree.
_M64 = (1 << 64) - 1


def _rotl(x, k):
    return ((x << k) | (x >> (64 - k))) & _M64


def _stable_first(seed, maxv):
    """StableRandom.First(seed, max): splitmix64 seeds xoshiro256**, Lemire bounded draw."""
    s = [0, 0, 0, 0]
    x = seed & _M64
    for i in range(4):
        x = (x + 0x9E3779B97F4A7C15) & _M64
        z = x
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & _M64
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & _M64
        s[i] = z ^ (z >> 31)

    def nxt():
        r = (_rotl((s[1] * 5) & _M64, 7) * 9) & _M64
        t = (s[1] << 17) & _M64
        s[2] ^= s[0]
        s[3] ^= s[1]
        s[1] ^= s[2]
        s[0] ^= s[3]
        s[2] ^= t
        s[3] = _rotl(s[3], 45)
        return r

    prod = maxv * nxt()
    if (prod & _M64) < maxv:
        rem = ((1 << 64) - maxv) % maxv
        while (prod & _M64) < rem:
            prod = maxv * nxt()
    return prod >> 64


# (name as the logbook writes it, vein weight, Mining needed to pull it)
VEINS = [("Iron", 496, 0), ("Dull Copper", 112, 65), ("Shadow Iron", 98, 70),
         ("Copper", 84, 75), ("Bronze", 70, 80), ("Gold", 56, 85), ("Agapite", 42, 90),
         ("Verite", 28, 95), ("Valorite", 14, 99), ("Platinum", 8, 100), ("Toxic", 7, 100),
         ("Blaze", 6, 100), ("Frost", 5, 100), ("Obsidian", 4, 100), ("Mythril", 3, 100),
         ("Adamantium", 2, 100), ("Celestial", 1, 100)]
VEIN_TOTAL = sum(w for _, w, _ in VEINS)
ORE_REQ = dict((n, r) for n, _, r in VEINS)
ORE_RANK = dict((n, i) for i, (n, _, _) in enumerate(VEINS))
EXTENDED = set(n for n, _, _ in VEINS[9:])     # yield iron until reported to the Archivist
ORE_NAMES = sorted([n for n, _, _ in VEINS[1:]], key=len, reverse=True)
ORE_HUE = {"Dull Copper": 0x973, "Shadow Iron": 0x966, "Copper": 0x96D, "Bronze": 0x972,
           "Gold": 0x8A5, "Agapite": 0x979, "Verite": 0x89F, "Valorite": 0x8AB,
           "Platinum": 0x481, "Toxic": 0x44, "Blaze": 0x2B, "Frost": 0x58,
           "Obsidian": 0x455, "Mythril": 0x59, "Adamantium": 0x13, "Celestial": 0x35}
FACETS = ["Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "TerMur"]
VEIN_VAR = "SL_Mining_Veins"

VEIN_CACHE = {}             # (map, bx, by) -> ore name
LOGGED = {}                 # ore name -> [(x, y)] veins in the logbook on this map
WORTH = {}                  # bank -> (vein bonus, new-vein bonus)
SURVEY = {"map_ok": True, "hits": 0, "misses": 0, "book_hits": 0, "book_total": 0,
          "book": None, "read_t": 0.0, "reported": set(), "skill": -1.0, "skill_t": 0.0,
          "map": -1, "list": [], "list_t": 0.0, "list_at": (0, 0), "went": set(),
          "seen_lines": []}


def cur_map():
    try:
        return int(API.GetMap())
    except Exception:
        return 0


def vein_at_bank(b, mp=None):
    if mp is None:
        mp = cur_map()
    k = (mp, b[0], b[1])
    v = VEIN_CACHE.get(k)
    if v is None:
        r = _stable_first((b[0] * 17 + b[1] * 11 + mp * 3) & _M64, VEIN_TOTAL)
        v = VEINS[-1][0]
        for name, w, _ in VEINS:
            if r < w:           # the shard's patch uses "<" (stock ModernUO "<=")
                v = name
                break
            r -= w
        VEIN_CACHE[k] = v
    return v


def vein_at(x, y):
    return vein_at_bank(bank(x, y))


def mining_skill():
    now = time.time()
    if now - SURVEY["skill_t"] > 30:
        SURVEY["skill_t"] = now
        try:
            v = float(API.GetSkill("Mining").Value)
        except Exception:
            v = 0.0
        if v != SURVEY["skill"]:
            SURVEY["skill"] = v
            WORTH.clear()
    return SURVEY["skill"]


def ore_ok(name):
    """Is your Mining high enough to get this ore from its vein? (else it gives iron)"""
    return name != "Iron" and mining_skill() >= ORE_REQ.get(name, 999)


def logged_near(name, x, y, r=None):
    r = NEW_VEIN_DIST if r is None else r
    for lx, ly in LOGGED.get(name, ()):
        if (lx - x) * (lx - x) + (ly - y) * (ly - y) <= r * r:
            return True
    return False


def survey_on():
    return opt("seek") and SURVEY["map_ok"]


def bank_worth(b):
    """(bonus for ore you'll actually get, bonus for a vein the logbook doesn't have)"""
    if SURVEY["map"] != cur_map():
        SURVEY["map"] = cur_map()
        WORTH.clear()
        load_vein_log()
    mining_skill()
    w = WORTH.get(b)
    if w is None:
        name = vein_at_bank(b)
        v = n = 0.0
        if ore_ok(name):
            if name not in EXTENDED or name in SURVEY["reported"]:
                v = W_VEIN
            if not logged_near(name, b[0] * BANK + BANK // 2, b[1] * BANK + BANK // 2):
                n = W_NEWVEIN
        w = WORTH[b] = (v, n)
    return w


def tile_value(x, y):
    if not survey_on() or not usable(x, y):
        return 0.0
    v, n = bank_worth(bank(x, y))
    return v + n


# Blocks whose ore you actually know: colored ore you dug there, or a vein in your
# logbook. Only these get colored, so the script never shows a block before you
# (or your logbook) found it.
KNOWN = {}                  # bank -> ore name
KNOWN_VAR = "SL_Mining_Known"


def logged_bank(name, x, y):
    """The block a logbook vein is in (the log holds where you stood, up to 2 tiles off)."""
    for dx in (0, -2, 2):
        for dy in (0, -2, 2):
            b = bank(x + dx, y + dy)
            if vein_at_bank(b) == name:
                return b
    return bank(x, y)


def know(b, name):
    if KNOWN.get(b) == name:
        return
    KNOWN[b] = name
    try:
        raw = API.GetPersistentVar(KNOWN_VAR, "", API.PersistentVar.Char) or ""
        keep = [e for e in str(raw).split(";") if e]
        keep.append("{},{},{},{}".format(cur_map(), b[0], b[1], name))
        API.SavePersistentVar(KNOWN_VAR, ";".join(keep[-1500:]), API.PersistentVar.Char)
    except Exception:
        pass
    if ORE_MAP["on"]:
        ore_map_tick(force=True)


def load_known():
    mp = cur_map()
    try:
        raw = API.GetPersistentVar(KNOWN_VAR, "", API.PersistentVar.Char) or ""
    except Exception:
        return
    for e in str(raw).split(";"):
        p = e.split(",")
        if len(p) == 4 and p[3] in ORE_REQ:
            try:
                if int(p[0]) == mp:
                    KNOWN[(int(p[1]), int(p[2]))] = p[3]
            except ValueError:
                pass


def _add_logged(name, x, y, save=True):
    pts = LOGGED.setdefault(name, [])
    KNOWN.setdefault(logged_bank(name, x, y), name)
    if (x, y) in pts:
        return False
    pts.append((x, y))
    WORTH.clear()
    if save:
        save_vein_log()
    return True


def load_vein_log():
    """Veins this character saw logged, kept in case the logbook isn't in the pack."""
    LOGGED.clear()
    KNOWN.clear()
    load_known()
    try:
        raw = API.GetPersistentVar(VEIN_VAR, "", API.PersistentVar.Char) or ""
    except Exception:
        return
    mp = cur_map()
    for e in str(raw).split(";"):
        p = e.split(",")
        if len(p) == 4 and p[1] in ORE_REQ:
            try:
                if int(p[0]) == mp:
                    _add_logged(p[1], int(p[2]), int(p[3]), save=False)
            except ValueError:
                pass


def save_vein_log():
    try:
        raw = API.GetPersistentVar(VEIN_VAR, "", API.PersistentVar.Char) or ""
        mp = cur_map()
        keep = [e for e in str(raw).split(";") if e and not e.startswith(str(mp) + ",")]
        for name, pts in LOGGED.items():
            for x, y in pts:
                keep.append("{},{},{},{}".format(mp, name, x, y))
        API.SavePersistentVar(VEIN_VAR, ";".join(keep[-600:]), API.PersistentVar.Char)
    except Exception:
        pass


# ---- reading the Prospector's Logbook -------------------------------------------
_HEAD_RE = re.compile(r"(" + "|".join(ORE_NAMES) + r")[^0-9|]{0,24}?(\d+)\s+veins?", re.I)
_ROW_RE = re.compile(r"\|\s*([^|]*),\s*([^|,]+?)\s*\|\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)")


def find_logbook():
    for it in pack_all():
        n = item_name(it)
        if "logbook" in n or (it.Graphic == LOGBOOK_GRAPHIC and "prospector" in tooltip(it)):
            return it
    return None


def _strip_html(text):
    text = re.sub(r"(?i)<br\s*/?>", "\n", text)
    text = re.sub(r"<[^>]*>", " ", text)
    return text.replace("&nbsp;", " ")


def parse_logbook(text):
    """{ore: {"reported": bool, "veins": [(facet, x, y)]}} from the logbook gump text."""
    text = _strip_html(text)
    heads = list(_HEAD_RE.finditer(text))
    out = {}
    for i, h in enumerate(heads):
        name = next((n for n in ORE_NAMES if n.lower() == h.group(1).lower()), h.group(1))
        seg = text[h.start():heads[i + 1].start() if i + 1 < len(heads) else len(text)]
        bar = seg.find("|")
        entry = out.setdefault(name, {"reported": False, "veins": []})
        if "[reported]" in (seg[:bar] if bar >= 0 else seg).lower():
            entry["reported"] = True
        for r in _ROW_RE.finditer(seg):
            entry["veins"].append((r.group(2).strip(), int(r.group(3)), int(r.group(4))))
    return out


def _logbook_gump(timeout=4.0):
    t = 0.0
    while t < timeout:
        gid = API.HasGump()
        if gid and (API.GumpContains("Logbook", gid) or API.GumpContains("discoveries", gid)):
            API.Pause(0.3)
            return gid
        API.Pause(0.25)
        t += 0.25
    return 0


def _gump_text(gid):
    parts = []
    try:
        parts.append(API.GetGumpContents(gid) or "")
    except Exception:
        pass
    try:
        g = API.GetGump(gid)
        if g is not None:
            parts.append(g.PacketGumpText or "")
    except Exception:
        pass
    return "\n".join(parts)


def read_logbook(quiet=False):
    """Open the logbook, note every logged vein on this facet, close it."""
    SURVEY["read_t"] = time.time()
    if SURVEY["map"] != cur_map():
        SURVEY["map"] = cur_map()
        load_vein_log()
    book = find_logbook()
    if book is None:
        SURVEY["book"] = None
        if not quiet:
            note("No Prospector's Logbook in your pack; tracking veins myself.", 43)
        return False
    API.UseObject(book.Serial)
    gid = _logbook_gump()
    if not gid:
        if not quiet:
            note("The logbook didn't open.", 43)
        return False
    text = _gump_text(gid)
    try:
        API.CloseGump(gid)
    except Exception:
        pass
    data = parse_logbook(text)
    mp = cur_map()
    facet = FACETS[mp].lower() if 0 <= mp < len(FACETS) else ""
    n = 0
    for name, e in data.items():
        if e["reported"]:
            SURVEY["reported"].add(name)
        for fac, x, y in e["veins"]:
            if fac.lower().replace(" ", "") == facet:
                _add_logged(name, x, y, save=False)
                n += 1
    save_vein_log()
    WORTH.clear()
    SURVEY["book"] = sum(len(e["veins"]) for e in data.values())
    check_vein_map()
    dbg("Logbook: {} ore types, {} veins on this facet".format(len(data), n))
    return True


def check_vein_map():
    """How many logged veins sit where the vein map says that ore is."""
    hits = total = 0
    for name, pts in LOGGED.items():
        for x, y in pts:
            total += 1
            # the logbook stores where you stood; you mine up to 2 tiles out
            if any(vein_at(x + dx, y + dy) == name for dx in (-2, 2) for dy in (-2, 2)):
                hits += 1
    SURVEY["book_hits"], SURVEY["book_total"] = hits, total
    judge_vein_map()


def judge_vein_map():
    hits = SURVEY["book_hits"] + SURVEY["hits"]
    total = SURVEY["book_total"] + SURVEY["hits"] + SURVEY["misses"]
    ok = total < 5 or hits >= 0.6 * total
    if SURVEY["map_ok"] and not ok:
        note("Vein map doesn't match this shard ({}/{}); not steering by it.".format(hits, total), 33)
    SURVEY["map_ok"] = ok


_DIG_RE = re.compile(r"You dig some (.+?) ore", re.I)
_LOG_RE = re.compile(r"(?:Discovery recorded:|New)\s+(" + "|".join(ORE_NAMES) +
                     r")\b.*?\((\d+),\s*(\d+)\)", re.I)


def survey_after_ore(x, y):
    """After a good swing: check the ore against the map, catch new logbook entries."""
    try:
        entries = API.GetJournalEntries(3) or []
    except Exception:
        entries = []
    for e in entries:
        text = getattr(e, "Text", "") or ""
        key = (text, str(getattr(e, "Time", "")))
        if key in SURVEY["seen_lines"]:
            continue
        SURVEY["seen_lines"].append(key)
        del SURVEY["seen_lines"][:-30]
        m = _LOG_RE.search(text)
        if m:
            name = next((n for n in ORE_NAMES if n.lower() == m.group(1).lower()), m.group(1))
            if _add_logged(name, int(m.group(2)), int(m.group(3))):
                note("New {} vein logged! Report it to the Survey Archivist.".format(name), 68)
            continue
        m = _DIG_RE.search(text)
        if m:
            dug = m.group(1).lower()
            name = next((n for n in ORE_NAMES if n.lower() in dug), None)
            if name is None:
                continue            # iron: every colored vein falls back to it sometimes
            know(bank(x, y), name)
            if vein_at(x, y) == name:
                SURVEY["hits"] += 1
            else:
                SURVEY["misses"] += 1
                dbg("Dug {} where the map says {}".format(name, vein_at(x, y)))
            judge_vein_map()


# ---- Survey button: list and mark the best veins nearby --------------------------
def _bank_tiles(b):
    return [(b[0] * BANK + i, b[1] * BANK + j) for i in range(BANK) for j in range(BANK)]


def _bank_rock(b):
    """Rough count of untried rock in a block from 16 sample tiles (reading every
    tile is slow), or None if the client hasn't loaded that far."""
    n = known = 0
    for i in (1, 3, 5, 7):
        for j in (0, 2, 4, 6):
            x, y = b[0] * BANK + i, b[1] * BANK + j
            lg = tile_info(x, y)[0]
            if lg >= 0:
                known += 1
            if (x, y) not in TRIED and is_rock(x, y):
                n += 1
    return n * 4 if known >= 4 else None


def survey(show=True):
    """Rank colored veins within SURVEY_RANGE. Returns the list, best first."""
    if time.time() - SURVEY["read_t"] > 300:
        read_logbook(quiet=True)
    px, py = API.Player.X, API.Player.Y
    pb = bank(px, py)
    r = SURVEY_RANGE // BANK + 1
    cands, low = [], {}
    for bx in range(pb[0] - r, pb[0] + r + 1):
        for by in range(pb[1] - r, pb[1] + r + 1):
            b = (bx, by)
            name = vein_at_bank(b)
            if name == "Iron" or b in DEPLETED:
                continue
            cx, cy = bx * BANK + BANK // 2, by * BANK + BANK // 2
            d = max(abs(cx - px), abs(cy - py))
            if d > SURVEY_RANGE:
                continue
            if not ore_ok(name):
                low[name] = low.get(name, 0) + 1
                continue
            new = not logged_near(name, cx, cy)
            pre = ORE_RANK[name] + (4 if new else 0) - d / 10.0
            cands.append([pre, name, b, d, new])
    cands.sort(reverse=True)
    out = []
    for pre, name, b, d, new in cands[:12]:
        checkpoint()
        rock = _bank_rock(b)
        if rock is not None and rock < 3:
            continue
        score = pre + min(rock if rock is not None else 8, 24) / 6.0
        out.append({"score": score, "name": name, "bank": b, "dist": d, "new": new,
                    "rock": rock, "gated": name in EXTENDED and name not in SURVEY["reported"]})
    out.sort(key=lambda v: -v["score"])
    SURVEY["list"], SURVEY["list_t"], SURVEY["list_at"] = out, time.time(), (px, py)
    if show:
        show_survey(out, low)
    return out


def _a(name):
    return ("an " if name[0] in "AEIOU" else "a ") + name


def show_survey(out, low):
    here = vein_at(API.Player.X, API.Player.Y)
    sysmsg("[Survey] Mining {:.1f}. You're on {}{}.".format(
        mining_skill(), "an iron block" if here == "Iron" else _a(here) + " vein",
        "" if here == "Iron" or logged_near(here, API.Player.X, API.Player.Y) else " (not in your logbook)"), 68)
    if not SURVEY["map_ok"]:
        sysmsg("[Survey] Vein map is off for this shard; the list may be wrong.", 33)
    if not out:
        sysmsg("[Survey] No colored veins you can mine within {} tiles.".format(SURVEY_RANGE), 43)
    for v in out[:SURVEY_SHOW]:
        cx, cy = v["bank"][0] * BANK + BANK // 2, v["bank"][1] * BANK + BANK // 2
        sysmsg("[Survey] {}{}{}  {} tiles {}  ({} rock)".format(
            v["name"], "  NEW" if v["new"] else "", "  (gives iron until reported)" if v["gated"] else "",
            v["dist"], compass(cx - API.Player.X, cy - API.Player.Y),
            v["rock"] if v["rock"] is not None else "?"), 88 if v["new"] else 90)
    if low:
        sysmsg("[Survey] Also nearby, need more skill: " + ", ".join(
            "{} {} ({})".format(n, k, ORE_REQ[k]) for k, n in sorted(low.items(), key=lambda kv: ORE_RANK[kv[0]])), 90)
    for v in out[:3]:
        hue = ORE_HUE.get(v["name"], 0x481)
        for x, y in _bank_tiles(v["bank"]):
            if (x, y) not in TRIED:
                mark_at(x, y, hue, SURVEY_MARKS)
    if out:
        b = out[0]["bank"]
        try:
            API.TrackingArrow(b[0] * BANK + BANK // 2, b[1] * BANK + BANK // 2)
            CTRL["arrow_until"] = time.time() + SURVEY_MARKS
        except Exception:
            pass


def vein_stand_spot(b):
    """A walkable tile next to the most untried rock of a block (None if not loaded yet)."""
    best = None
    for x in range(b[0] * BANK - RADIUS, b[0] * BANK + BANK + RADIUS):
        for y in range(b[1] * BANK - RADIUS, b[1] * BANK + BANK + RADIUS):
            if tile_info(x, y)[0] < 0 or not standable(x, y):
                continue
            n = sum(1 for dx in range(-RADIUS, RADIUS + 1) for dy in range(-RADIUS, RADIUS + 1)
                    if bank(x + dx, y + dy) == b and usable(x + dx, y + dy))
            d = max(abs(x - API.Player.X), abs(y - API.Player.Y))
            if n >= 2 and (best is None or (n, -d) > best[0]):
                best = ((n, -d), x, y)
    return (best[1], best[2]) if best else None


def go_to_vein(auto=False):
    """Walk to the best surveyed vein. True if we got there."""
    lst = SURVEY["list"]
    if not lst or time.time() - SURVEY["list_t"] > 300 or \
            dist2((API.Player.X, API.Player.Y), SURVEY["list_at"]) > 16:
        lst = survey(show=not auto)
    lst = [v for v in lst if v["bank"] not in SURVEY["went"] and v["bank"] not in DEPLETED
           and v["bank"] != bank(API.Player.X, API.Player.Y)]
    if not lst:
        if not auto:
            note("No surveyed vein to go to.", 43)
        return False
    v = lst[0]
    b = v["bank"]
    SURVEY["went"].add(b)
    spot = vein_stand_spot(b)
    tx, ty = spot if spot else (b[0] * BANK + BANK // 2, b[1] * BANK + BANK // 2)
    note("Going to {}{} vein, {} tiles.".format(_a(v["name"]), " (new)" if v["new"] else "", v["dist"]), 88)
    prev = CTRL["state"]
    set_state("Walking to {} vein".format(v["name"]))
    ok = go_to(tx, ty, 1 if spot else 4, v["name"] + " vein")
    if ok and not spot:
        spot = vein_stand_spot(b)       # close enough now to see the rock
        if spot:
            ok = go_to(spot[0], spot[1], 1, v["name"] + " vein")
    set_state(prev)
    if ok:
        START[:] = [API.Player.X, API.Player.Y, API.Player.Z, cur_map()]
        HEADING[0] = HEADING[1] = 0.0
        CTRL["moved"] = True
    else:
        note("Couldn't reach the {} vein.".format(v["name"]), 43)
    return ok


# ---- ore map: tint every block around you in its ore's color ---------------------
ORE_MAP = {"tiles": {}, "center": None, "on": False, "queue": []}     # (x, y) -> hue


def ore_hue(x, y):
    """Highlight color for a tile: the ore you found in its block, else plain green."""
    name = KNOWN.get(bank(x, y))
    return ORE_HUE.get(name, 0x481) if name else HUE_ORE


def ore_map_tick(force=False):
    """Tint the mineable tiles of every colored block around you. Reading a tile
    waits a game frame, so new tiles are checked a few at a time."""
    on = opt("oremap")
    if not on:
        if ORE_MAP["on"]:
            clear_ore_map()
        return
    c = (cur_map(), API.Player.X // BANK, API.Player.Y // BANK)
    if force or not ORE_MAP["on"] or ORE_MAP["center"] != c:
        ORE_MAP["on"] = True
        ORE_MAP["center"] = c
        want = {}
        r = ORE_MAP_RANGE
        for bx in range(c[1] - r, c[1] + r + 1):
            for by in range(c[2] - r, c[2] + r + 1):
                b = (bx, by)
                name = KNOWN.get(b)
                if name is None:
                    continue            # not found yet: stays plain
                hue = HUE_DRY if b in DEPLETED else ORE_HUE.get(name, 0x481)
                for t in _bank_tiles(b):
                    want[t] = hue
        for t in list(ORE_MAP["tiles"]):
            if want.get(t) != ORE_MAP["tiles"][t]:
                if t not in MARKED:
                    try:
                        API.RemoveMarkedTile(t[0], t[1])
                    except Exception:
                        pass
                del ORE_MAP["tiles"][t]
        px, py = API.Player.X, API.Player.Y
        ORE_MAP["queue"] = sorted([(t, h) for t, h in want.items() if t not in ORE_MAP["tiles"]],
                                  key=lambda th: -_cheb(th[0][0], th[0][1], px, py))
    q = ORE_MAP.get("queue") or []
    slow = 0
    while q and slow < ORE_MAP_BATCH:
        t, hue = q.pop()            # closest first
        if t not in TILE_CACHE:
            slow += 1
        if not is_rock(t[0], t[1]):
            continue
        ORE_MAP["tiles"][t] = hue
        if t not in MARKED:
            try:
                API.MarkTile(t[0], t[1], hue)
            except Exception:
                pass


def clear_ore_map():
    for t in ORE_MAP["tiles"]:
        if t not in MARKED:
            try:
                API.RemoveMarkedTile(t[0], t[1])
            except Exception:
                pass
    ORE_MAP.update({"tiles": {}, "center": None, "on": False, "queue": []})


def survey_line():
    if not opt("seek"):
        return "Survey: off"
    here = KNOWN.get(bank(API.Player.X, API.Player.Y), "Iron")
    tot = SURVEY["book_total"] + SURVEY["hits"] + SURVEY["misses"]
    hits = SURVEY["book_hits"] + SURVEY["hits"]
    check = "map {}/{}".format(hits, tot) if tot else "map unchecked"
    if not SURVEY["map_ok"]:
        check = "map OFF ({}/{})".format(hits, tot)
    tag = ""
    if here == "Iron":
        here = "not found yet"
    else:
        tag = " new" if not logged_near(here, API.Player.X, API.Player.Y) else ""
        if not ore_ok(here):
            tag = " (skill {})".format(ORE_REQ[here])
    book = "no logbook" if SURVEY["book"] is None else "log {}".format(sum(len(p) for p in LOGGED.values()))
    return "Here: {}{}   {}   {}".format(here, tag, book, check)


def offsets():
    out = []
    for dx in range(-RADIUS, RADIUS + 1):
        for dy in range(-RADIUS, RADIUS + 1):
            out.append((dx, dy))
    # closest tiles first
    out.sort(key=lambda o: max(abs(o[0]), abs(o[1])))
    return out


def swing(dx, dy, use_land):
    """One swing at a relative tile. Returns ore/empty/bad/full/broke/notool/none."""
    tool = find_tool()
    if tool is None:
        return "notool"

    CUR_TOOL[0] = tool.Serial
    # a cursor left open by an earlier swing would eat this one; clear it first
    if API.HasTarget():
        API.CancelTarget()
        API.Pause(0.3)
    API.ClearJournal()
    API.UseObject(tool.Serial)
    if not API.WaitForTarget("any", 3):
        # no target cursor at all: the tool refused (recharging, worn, busy...)
        API.Pause(0.3)
        if API.InJournal("is exhausted") or API.InJournal("Miners' Compact"):
            mark_jacob_dead(tool.Serial)
            return "retry"
        if API.InJournal("recharge") or API.InJournal("wait a moment"):
            jacob_ran_dry(tool.Serial)
            return "retry"
        if API.InJournalAny(BUSY_MSGS):
            API.Pause(1.0)
            return "retry"
        return strike(tool)

    if use_land:
        API.TargetLandRel(dx, dy)
    else:
        API.TargetTileRel(dx, dy)
    # TazUO's TargetTileRel silently does nothing when the tile has no targetable
    # static, leaving the cursor open. That is the tile's fault, not the tool's.
    API.Pause(0.15)
    if API.HasTarget():
        API.CancelTarget()
        STRIKES.pop(tool.Serial, None)
        return "bad"

    # wait for the result instead of a fixed pause (mining takes ~1.6 s plus lag)
    known = FULL_MSGS + BROKE_MSG + GOT_MSGS + EMPTY_MSGS + BAD_MSGS + BUSY_MSGS + \
        ["recharge", "wait a moment"]
    waited = 0.0
    while waited < SWING_DELAY and not API.InJournalAny(known):
        API.Pause(0.2)
        waited += 0.2
    API.Pause(0.2)                  # let the rest of the lines arrive

    if API.InJournalAny(BUSY_MSGS):
        API.Pause(1.0)
        return "retry"
    if API.InJournalAny(FULL_MSGS):
        return "full"
    if API.InJournal("recharge") or API.InJournal("wait a moment"):
        jacob_ran_dry(tool.Serial)
        return "retry"
    if API.InJournalAny(BROKE_MSG):
        return "broke"
    if API.InJournalAny(GOT_MSGS):
        STRIKES.pop(tool.Serial, None)
        survey_after_ore(API.Player.X + dx, API.Player.Y + dy)
        return "ore"
    if API.InJournalAny(EMPTY_MSGS):
        STRIKES.pop(tool.Serial, None)
        return "empty"
    if API.InJournalAny(BAD_MSGS):
        STRIKES.pop(tool.Serial, None)
        return "bad"
    # the target went through but nothing we recognize came back: count it against
    # the tile (lag or unknown text), never against the tool
    heard = last_journal_line()
    if heard:
        dbg("No result recognized; last message: " + heard)
    return "none"


def last_journal_line():
    try:
        entries = API.GetJournalEntries(SWING_DELAY + 2) or []
        if entries:
            return entries[-1].Text
    except Exception:
        pass
    return ""


def strike(tool):
    """A swing with no recognizable result. Two in a row and the tool is set aside."""
    n = STRIKES.get(tool.Serial, 0) + 1
    STRIKES[tool.Serial] = n
    if n < 3:
        return "none"
    heard = last_journal_line()
    if is_jacob(tool):
        jacob_ran_dry(tool.Serial)
    else:
        secs = rest_tool(tool.Serial, "no response")
        note("A {} stopped working; setting it aside {}s.".format(item_name(tool) or "tool", int(secs)), 43)
    if heard:
        dbg("Last message was: " + heard)
    return "retry"


def mine_tile(dx, dy):
    """Mine one tile until dry. Returns 'stop' to end the script, else ore/empty/bad/skip."""
    x = API.Player.X + dx
    y = API.Player.Y + dy
    if (x, y) in TRIED or bank(x, y) in DEPLETED:
        return "skip"           # already known empty, don't waste swings

    mark(dx, dy, HUE_ACTIVE, lasting=False)
    result = _mine_tile(dx, dy)
    if (API.Player.X, API.Player.Y) != (x - dx, y - dy):
        _unmark(x, y)
        return "moved"          # "Go to vein" walked us away mid-tile
    if result == "notile":
        _unmark(x, y)
        return "skip"
    if result in ("stop", "trip"):
        mark(dx, dy, HUE_ACTIVE)    # let it fade; we will be back
        return result

    if result == "ore_done":        # got ore, then the block ran out
        deplete(x, y)
        result = "ore"
    TRIED[(x, y)] = result
    if result in ("ore", "empty"):
        learn_mineable(x, y, CTRL.get("mode", True))
    if result == "empty":
        deplete(x, y)
    if result == "bad":
        learn_bad(x, y)
    mark(dx, dy, ore_hue(x, y) if result == "ore" else HUE_DRY)
    return result


def _mine_tile(dx, dy):
    # mountain walls are statics, cave floors and dirt are land tiles
    lg, _, sg, _, _ = tile_info(API.Player.X + dx, API.Player.Y + dy)
    # target the part the server accepts: a mineable static, else the land under it
    # (cave floors often have ore piles, debris or mushrooms on top)
    if static_mineable(sg):
        modes = [False] + ([True] if land_mineable(lg) else [])
    elif land_mineable(lg):
        modes = [True]
    else:
        modes = [False, True] if sg else [True]
    got_ore = False
    for use_land in modes:
        swings = 0
        blanks = 0
        retries = 0
        here = (API.Player.X, API.Player.Y)
        while swings < MAX_SWINGS_TILE:
            checkpoint()
            if (API.Player.X, API.Player.Y) != here:
                return "notile"
            u = unload_check()
            if u != "ok":
                return u            # "trip" or "stop"
            if not ensure_tools():
                if not wait_for_jacob():
                    sysmsg("[Mining] Out of mining tools and can't make more.", 33)
                    return "stop"
            r = swing(dx, dy, use_land)
            log("swing {},{} ({}) -> {}{}".format(
                API.Player.X + dx, API.Player.Y + dy, "land" if use_land else "static", r,
                ("  heard: " + last_journal_line()) if r in ("none", "bad") else ""))
            swings += 1
            if r == "notool":
                if ensure_tools(force=True) or wait_for_jacob():
                    continue
                sysmsg("[Mining] No pickaxe or shovel, and can't make one.", 33)
                return "stop"
            if r == "full":
                u = unload_check(force=True)
                if u == "ok":
                    continue
                if u == "stop":
                    sysmsg("[Mining] Overloaded. Go unload.", 33)
                return u
            if r == "broke":
                continue            # grab the next tool on the next swing
            if r == "retry":
                swings -= 1         # the tool's fault, not the tile's
                blanks = 0
                retries += 1
                if retries > 6:
                    return "notile"     # tools keep failing: leave this tile for later
                continue
            if r == "ore":
                got_ore = True
                CTRL["mode"] = use_land
                blanks = 0
                continue
            if r == "empty":
                # the block is out of ore (true even if we just got some)
                return "empty" if not got_ore else "ore_done"
            if r == "bad":
                break               # try the other targeting mode, if any
            blanks += 1             # no message; lag or unknown text
            if blanks >= 3:
                break
        if got_ore:
            return "ore"
    return "bad"


def mine_here():
    """Mine every tile around the current spot. Returns done / trip / stop."""
    px, py = API.Player.X, API.Player.Y
    VISITED.add((px, py))
    CTRL["moved"] = False
    set_state("Mining")
    tiles = offsets()
    # tiles in a colored vein first (stable sort keeps closest-first otherwise)
    tiles.sort(key=lambda o: -tile_value(px + o[0], py + o[1]))
    v = KNOWN.get(bank(px, py), "Iron")
    if survey_on() and v != "Iron" and ore_ok(v):
        API.HeadMsg("{} vein{}".format(v, "" if logged_near(v, px, py) else " (new!)"), API.Player, 68)
    for i, (dx, dy) in enumerate(tiles):
        checkpoint()
        if CTRL.get("moved"):
            return "moved"
        r = mine_tile(dx, dy)
        if r == "moved":
            return r
        if r in ("stop", "trip"):
            return r
        if SHOW_PROGRESS and r != "skip":
            API.HeadMsg("Tile {}/{}".format(i + 1, len(tiles)), API.Player, 53)
    return "done"


def standable(x, y):
    if LINK_SRC and (cur_map(), x, y) in LINK_SRC:
        return False            # a teleporter: stepping there sends you away
    lg, limp, sg, simp, _ = tile_info(x, y)
    if lg < 0:
        return True             # unknown: let the pathfinder decide
    return not limp and not simp


def dbg(msg):
    if MOVE_DEBUG:
        sysmsg("[Mining] " + msg, 90)
    else:
        log("[debug] " + msg)


class Grid:
    """Counts usable rock in any box fast (summed-area table)."""
    def __init__(self, x0, y0, x1, y1, fn):
        self.x0, self.y0 = x0, y0
        w, h = x1 - x0 + 1, y1 - y0 + 1
        self.w, self.h = w, h
        self.sat = [[0] * (w + 1) for _ in range(h + 1)]
        self.total = 0
        for j in range(h):
            row = 0
            for i in range(w):
                v = fn(x0 + i, y0 + j)
                v = (1 if v is True else v) or 0
                self.total += v
                row += v
                self.sat[j + 1][i + 1] = self.sat[j][i + 1] + row

    def count(self, xa, ya, xb, yb):
        i0 = max(0, xa - self.x0)
        j0 = max(0, ya - self.y0)
        i1 = min(self.w - 1, xb - self.x0)
        j1 = min(self.h - 1, yb - self.y0)
        if i1 < i0 or j1 < j0:
            return 0
        S = self.sat
        return S[j1 + 1][i1 + 1] - S[j0][i1 + 1] - S[j1 + 1][i0] + S[j0][i0]


def compass(dx, dy):
    names = ["E", "SE", "S", "SW", "W", "NW", "N", "NE"]   # UO: +y is south
    ang = math.atan2(dy, dx)
    return names[int(round(ang / (math.pi / 4))) % 8]


def find_spots(start):
    """Score every standable spot in range; best first as [(x, y, fresh, ahead)]."""
    px, py = API.Player.X, API.Player.Y
    pad = RADIUS + LOOKAHEAD
    x0, y0 = start[0] - WANDER_RANGE - pad, start[1] - WANDER_RANGE - pad
    x1, y1 = start[0] + WANDER_RANGE + pad, start[1] + WANDER_RANGE + pad
    grid = Grid(x0, y0, x1, y1, usable)
    if grid.total == 0 and not GOOD_LAND and not GOOD_STATIC:
        # nothing recognized as rock yet: treat any untried tile as a maybe
        grid = Grid(x0, y0, x1, y1, lambda x, y: (x, y) not in TRIED and bank(x, y) not in DEPLETED)
    vgrid = Grid(x0, y0, x1, y1, tile_value) if survey_on() else None
    dbg("{} unmined rock tiles in range".format(grid.total))

    hx, hy = HEADING
    hlen = math.hypot(hx, hy)
    cands = []
    for x in range(start[0] - WANDER_RANGE, start[0] + WANDER_RANGE + 1):
        for y in range(start[1] - WANDER_RANGE, start[1] + WANDER_RANGE + 1):
            dx, dy = x - px, y - py
            d = max(abs(dx), abs(dy))
            if d <= 1 or (x, y) in VISITED or not standable(x, y):
                continue
            fresh = grid.count(x - RADIUS, y - RADIUS, x + RADIUS, y + RADIUS)
            if fresh < MIN_FRESH:
                continue
            ahead = grid.count(x - pad, y - pad, x + pad, y + pad) - fresh
            score = W_FRESH * fresh + W_AHEAD * ahead - W_DIST * d
            if vgrid is not None:
                vin = vgrid.count(x - RADIUS, y - RADIUS, x + RADIUS, y + RADIUS)
                score += vin + W_AHEAD * (vgrid.count(x - pad, y - pad, x + pad, y + pad) - vin)
            if hlen > 0:
                cosang = (dx * hx + dy * hy) / (math.hypot(dx, dy) * hlen)
                score += W_HEADING * cosang
            cands.append((score, x, y, fresh, ahead))
    cands.sort(reverse=True)
    return [(x, y, f, a) for _, x, y, f, a in cands], grid


# TazUO's Pathfind z default is int.MinValue ("use the ground height there").
# The API.py stub shows 1337, but passing 1337 makes the pathfinder aim at
# height 1337, which it can never reach. Always pass this instead.
ANY_Z = -2147483648


def settle_after_jump(secs=2.5):
    """Right after a teleport the client is still loading the new area and its
    pathfinder fails; wait for it."""
    while time.time() - POS.get("jump_t", 0) < secs:
        API.ProcessCallbacks()
        API.Pause(0.25)


def walk_to(x, y, near=1, arrow=True, timeout=15):
    """Pathfind to (x, y). True if we ended up within `near` tiles of it."""
    settle_after_jump()
    ox, oy = API.Player.X, API.Player.Y
    if opt("marks"):
        try:
            API.MarkTile(x, y, HUE_DEST)
        except Exception:
            pass
    if arrow:
        try:
            API.TrackingArrow(x, y)
        except Exception:
            pass
    try:
        API.Pathfind(x, y, ANY_Z, near, False, timeout)
    except Exception as e:
        dbg("Pathfind error: " + str(e))
    waited = 0.0
    try:
        while API.Pathfinding() and waited < timeout:
            checkpoint()
            API.Pause(0.25)
            waited += 0.25
    except Halt:
        API.CancelPathfinding()
        raise
    if opt("marks"):
        try:
            API.RemoveMarkedTile(x, y)
        except Exception:
            pass
    try:
        API.TrackingArrow(-1, -1)
    except Exception:
        pass
    track_position()            # notice a teleport right away
    moved = (API.Player.X, API.Player.Y) != (ox, oy)
    if moved:
        HEADING[0] = float(API.Player.X - ox)
        HEADING[1] = float(API.Player.Y - oy)
    close = max(abs(API.Player.X - x), abs(API.Player.Y - y)) <= near
    return close and (moved or near > 0)


# ---- long-distance walking ------------------------------------------------------
# Every tile you stand on (while the window is open) is remembered as a proven
# stepping stone. Long walks hop between stepping stones when it can, and
# between straight-line points with detours when it can't.
CRUMBS = []                 # [(x, y)] walked tiles on this map
CRUMB_VAR = "SL_Mining_Crumbs"


def dist2(a, b):
    return max(abs(a[0] - b[0]), abs(a[1] - b[1]))


def track_position():
    now = time.time()
    q = (cur_map(), API.Player.X, API.Player.Y)
    last = POS["last"]
    dt = now - POS.get("t", now)
    POS["t"] = now
    if last is not None and q != last:
        # running mounted covers ~10 tiles a second, so allow for time between looks
        if q[0] != last[0] or _cheb(q[1], q[2], last[1], last[2]) > max(6, 12 * dt + 2):
            POS["jumps"] += 1
            POS["jump_t"] = now
            log("teleported: map {} {},{} -> map {} {},{}".format(last[0], last[1], last[2], q[0], q[1], q[2]))
            # walked into it (not recall / gate travel, which start standing still)
            if LEARN_TELEPORTS and now - POS["moved_t"] < 2.5:
                learn_link(last, q, POS["prev"])
            if q[0] != last[0]:
                on_map_change()
            POS["prev"] = None
        else:
            POS["prev"] = last
            POS["moved_t"] = now
    POS["last"] = q
    p = (API.Player.X, API.Player.Y)
    if not CRUMBS or min(dist2(p, c) for c in CRUMBS[-40:]) >= CRUMB_STEP:
        CRUMBS.append(p)
        if len(CRUMBS) > MAX_CRUMBS:
            del CRUMBS[:len(CRUMBS) - MAX_CRUMBS]


def load_crumbs():
    try:
        raw = API.GetPersistentVar(CRUMB_VAR, "", API.PersistentVar.Char) or ""
        mp, _, pts = str(raw).partition("|")
        if pts and int(mp) == API.GetMap():
            for e in pts.split(";"):
                x, y = e.split(",")
                CRUMBS.append((int(x), int(y)))
    except Exception:
        pass


def save_crumbs():
    try:
        API.SavePersistentVar(CRUMB_VAR, "{}|{}".format(
            API.GetMap(), ";".join("{},{}".format(x, y) for x, y in CRUMBS[-MAX_CRUMBS:])),
            API.PersistentVar.Char)
    except Exception:
        pass


BLOCKED = set()             # tiles a hop could not reach (per trip)


def walkable(x, y):
    return (x, y) not in BLOCKED and standable(x, y)


def plan_path(start, goal, near):
    """A* over the client's tile data; searches wider if the first try finds nothing."""
    for pad in (20, 45):
        path = _plan(start, goal, near, pad)
        if path:
            return path
    return None


def _plan(start, goal, near, pad):
    x0 = min(start[0], goal[0]) - pad
    x1 = max(start[0], goal[0]) + pad
    y0 = min(start[1], goal[1]) - pad
    y1 = max(start[1], goal[1]) + pad
    known = set(CRUMBS)

    def h(p):
        return max(0, dist2(p, goal) - near)

    openq = [(h(start), 0.0, start)]
    came = {start: None}
    cost = {start: 0.0}
    expanded = 0
    while openq and expanded < 25000:
        _, g, cur = heapq.heappop(openq)
        if g > cost.get(cur, 1e9):
            continue
        if dist2(cur, goal) <= near:
            path = []
            while cur is not None:
                path.append(cur)
                cur = came[cur]
            path.reverse()
            return path
        expanded += 1
        cx, cy = cur
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if not dx and not dy:
                    continue
                nx, ny = cx + dx, cy + dy
                if nx < x0 or nx > x1 or ny < y0 or ny > y1:
                    continue
                # right around the start, trust the game over our tile reading
                near_start = dist2((nx, ny), start) <= 2
                if not near_start and not walkable(nx, ny):
                    continue
                if not near_start and dx and dy and not (walkable(cx + dx, cy) and walkable(cx, cy + dy)):
                    continue            # no cutting corners
                step = 0.7 if (nx, ny) in known else 1.0
                ng = g + step
                if ng < cost.get((nx, ny), 1e9):
                    cost[(nx, ny)] = ng
                    came[(nx, ny)] = cur
                    heapq.heappush(openq, (ng + h((nx, ny)), ng, (nx, ny)))
    return None


def _go_to(tx, ty, near=1, label=""):
    """Walk anywhere on this map: plan a route, walk it in hops, re-plan around
    anything the game refuses. True if we got within `near`. Stops if a
    teleporter moves us (go_to re-plans)."""
    target = (tx, ty)
    j0 = POS["jumps"]
    try:
        API.TrackingArrow(tx, ty)
    except Exception:
        pass
    try:
        # the game's own pathfinder handles elevation, houses and creatures:
        # give it the whole trip first, then fall back to planned hops
        if walk_to(tx, ty, near, arrow=False, timeout=30):
            return True
        if POS["jumps"] != j0:
            return False
        if time.time() - POS.get("jump_t", 0) < 10:
            dbg("Just teleported; trying the walk again")
            API.Pause(1.5)
            if walk_to(tx, ty, near, arrow=False, timeout=30):
                return True
        dbg("Direct path failed, planning hops")
        replans = 0
        path = None
        while replans <= 8:
            checkpoint()
            if POS["jumps"] != j0:
                return False
            me = (API.Player.X, API.Player.Y)
            if dist2(me, target) > FORGE_MAX_DIST:
                dbg("Too far to plan a walk ({} tiles)".format(dist2(me, target)))
                return False
            if dist2(me, target) <= near:
                return True
            if path is None:
                path = plan_path(me, target, near)
                if path is None:
                    dbg("No route found to {},{}".format(tx, ty))
                    # last resort: let the game's own pathfinder try
                    return dist2(me, target) <= HOP * 2 and walk_to(tx, ty, near, arrow=False)
                dbg("Route planned: {} steps".format(len(path)))
            # the farthest point on the route within a hop
            idx = 0
            for k, p in enumerate(path):
                if dist2(me, p) <= 1:
                    idx = k
            wp_i = min(len(path) - 1, idx + HOP)
            wp = path[wp_i]
            if wp == me:
                return dist2(me, target) <= near
            last = wp_i >= len(path) - 1
            ok = walk_to(tx, ty, near, arrow=False) if last else walk_to(wp[0], wp[1], 1, arrow=False)
            if POS["jumps"] != j0:
                return False
            if ok and (API.Player.X, API.Player.Y) == me:
                ok = False              # "arrived" without moving: treat as stuck
            if ok:
                if label:
                    API.HeadMsg("{} ({} tiles)".format(label, dist2((API.Player.X, API.Player.Y), target)),
                                API.Player, 88)
                continue
            # the game would not take us there: block it and re-plan
            dbg("Hop to {},{} failed, re-planning".format(wp[0], wp[1]))
            BLOCKED.add(wp)
            if wp_i > idx + 1:
                BLOCKED.add(path[(idx + wp_i) // 2])
            path = None
            replans += 1
        return False
    finally:
        try:
            API.TrackingArrow(-1, -1)
        except Exception:
            pass
        BLOCKED.clear()
        save_crumbs()


# ---- teleporters (dungeon and mine entrances) ---------------------------------------
# Most cave and dungeon entrances are invisible server teleporters the client can't
# see. The script starts from ModernUO's standard list (Distribution/Data/
# teleporters.json: map,x,y -> map,x,y, back) and learns any other teleporter you
# walk through with the window open. Long walks (forge trips, going back to the
# mine) use them when that is much shorter, e.g. the New Haven mine:
# 3508,2777 outside <-> 5912,355 inside.
TELEPORTER_DATA = "0,311,786,0,314,784,0;0,311,786,0,5750,350,0;0,313,786,0,5748,361,0;0,314,786,0,5749,361,0;0,424,3283,0,5267,2757,0;0,512,1559,0,5394,127,1;0,513,1559,0,5395,127,1;0,514,1559,0,5396,127,1;0,588,1637,0,6472,868,1;0,766,1645,0,6174,21,0;0,766,1646,0,6174,22,0;0,766,1647,0,6174,23,0;0,1013,1433,0,5138,2016,0;0,1013,1434,0,5138,2016,0;0,1142,3621,0,1414,3828,0;0,1163,2204,0,1171,2202,0;0,1163,2208,0,1165,2214,0;0,1165,2204,0,1163,2211,0;0,1166,2237,0,1163,2205,0;0,1406,3996,0,1414,3828,0;0,1409,3824,0,1124,3623,0;0,1419,3832,0,1466,4015,0;0,1491,1640,0,6032,1499,0;0,1491,1642,0,6032,1501,0;0,1593,2488,0,1600,2489,0;0,1594,2488,0,1600,2489,0;0,1595,2488,0,1600,2489,0;0,1629,3320,0,5899,1411,0;0,1653,2963,0,1677,2987,0;0,1677,2987,0,1675,2987,0;0,1714,2996,0,6308,892,0;0,1714,2996,0,6309,891,0;0,1714,2997,0,6308,892,0;0,1714,2997,0,6309,892,0;0,1987,2062,0,5826,464,0;0,1988,2062,0,5827,464,0;0,1989,2062,0,5828,464,0;0,1997,81,0,5881,242,0;0,2384,836,0,5615,1996,1;0,2384,837,0,5615,1997,1;0,2384,838,0,5615,1998,1;0,2399,198,0,5753,436,0;0,2400,198,0,5754,436,0;0,2420,883,0,5392,1959,1;0,2421,883,0,5393,1959,1;0,2422,883,0,5394,1959,1;0,2455,858,0,5388,2027,1;0,2456,858,0,5389,2027,1;0,2457,858,0,5390,2027,1;0,2498,916,0,5455,1864,1;0,2499,916,0,5456,1864,1;0,2500,916,0,5457,1864,1;0,2544,850,0,5578,1927,1;0,2545,850,0,5579,1927,1;0,2546,850,0,5580,1927,1;0,2603,2120,0,2605,2130,0;0,2603,2121,0,2605,2130,0;0,2618,977,0,2727,2133,1;0,2669,2071,0,2666,2099,0;0,2669,2072,0,2666,2099,0;0,2669,2073,0,2666,2099,0;0,2676,2241,0,2691,2234,0;0,2676,2242,0,2691,2234,0;0,2685,2063,0,2685,2063,0;0,2758,2092,0,2756,2097,0;0,2759,2092,0,2756,2097,0;0,2776,895,0,5685,387,0;0,2923,3405,0,5687,1423,0;0,2985,2890,0,5974,2697,0;0,4110,430,0,5187,639,0;0,4111,430,0,5188,639,0;0,4112,430,0,5189,639,0;0,4300,968,0,4442,1122,0;0,4436,1107,0,4300,992,0;0,4443,1137,0,4487,1475,0;0,4449,1107,0,4539,890,0;0,4449,1115,0,4671,1135,0;0,4496,1475,0,4442,1122,0;0,4540,898,0,4442,1122,0;0,4619,1203,0,4622,1204,0;0,4627,1207,0,4633,1202,0;0,4663,1134,0,4442,1122,0;0,4721,3813,0,5904,16,1;0,4722,3813,0,5905,16,1;0,4723,3813,0,5906,16,1;0,5126,3143,0,5849,432,0;0,5129,909,0,5142,798,0;0,5130,909,0,5143,798,0;0,5131,909,0,5144,798,0;0,5132,909,0,5145,798,0;0,5132,1946,0,5332,1379,0;0,5133,984,0,5152,808,0;0,5133,985,0,5152,809,0;0,5133,986,0,5152,810,0;0,5133,987,0,5152,811,0;0,5133,1946,0,5332,1379,0;0,5134,1946,0,5332,1379,0;0,5135,1946,0,5332,1379,0;0,5137,3664,0,6025,1344,0;0,5137,3665,0,6025,1345,0;0,5139,2015,0,1014,1434,0;0,5139,2016,0,1014,1434,0;0,5139,2017,0,1014,1434,0;0,5140,973,0,5331,707,0;0,5140,1773,0,5171,1586,0;0,5142,797,0,5129,908,0;0,5143,797,0,5130,908,0;0,5144,797,0,5131,908,0;0,5145,797,0,5132,908,0;0,5145,973,0,5201,1564,0;0,5150,4062,0,6005,1378,0;0,5150,4063,0,6005,1380,0;0,5153,808,0,5134,984,0;0,5153,809,0,5134,985,0;0,5153,810,0,5134,986,0;0,5153,811,0,5134,987,0;0,5172,1589,0,5143,1774,0;0,5186,639,0,4110,430,0;0,5187,639,0,4110,430,0;0,5188,639,0,4111,430,0;0,5189,639,0,4112,430,0;0,5191,152,0,1367,891,0;0,5200,71,0,5211,22,0;0,5203,2327,0,5876,147,0;0,5207,2322,0,5877,147,0;0,5207,2323,0,5876,147,0;0,5216,586,0,5304,533,0;0,5217,18,0,5204,74,0;0,5217,586,0,5305,533,0;0,5218,586,0,5306,533,0;0,5218,761,0,5305,651,0;0,5219,761,0,5306,651,0;0,5242,1007,0,1175,2635,1;0,5243,1007,0,1176,2635,1;0,5244,1007,0,1177,2635,1;0,5265,669,0,5876,1378,0;0,5265,683,0,5606,802,0;0,5267,2757,0,424,3283,0;0,5272,2043,0,5363,1290,0;0,5273,2043,0,5363,1290,0;0,5304,532,0,5216,585,0;0,5304,676,0,7109,1097,0;0,5305,532,0,5217,585,0;0,5305,650,0,5218,760,0;0,5306,532,0,5218,585,0;0,5306,650,0,5219,760,0;0,5329,1381,0,5134,1947,0;0,5330,1381,0,5134,1947,0;0,5331,1381,0,5134,1947,0;0,5332,1381,0,5134,1947,0;0,5333,1381,0,5134,1947,0;0,5334,1381,0,5134,1947,0;0,5340,1599,0,5426,3122,0;0,5341,1599,0,5426,3122,0;0,5346,578,0,5137,649,1;0,5356,1540,0,5331,707,0;0,5360,1540,0,5876,1378,0;0,5363,1289,0,5272,2041,0;0,5364,1289,0,5272,2041,0;0,5386,755,0,5408,858,0;0,5386,756,0,5408,859,0;0,5386,757,0,5408,860,0;0,5400,1288,0,5879,3817,0;0,5400,1289,0,5879,3817,0;0,5400,1290,0,5879,3818,0;0,5409,858,0,5387,755,0;0,5409,859,0,5387,756,0;0,5409,860,0,5387,757,0;0,5426,3123,0,5341,1602,0;0,5443,2325,0,5814,3548,0;0,5451,1360,0,5435,1341,0;0,5453,1360,0,5436,1341,0;0,5466,1804,0,5593,1840,0;0,5466,1805,0,5593,1841,0;0,5466,1806,0,5593,1842,0;0,5490,19,0,5514,10,1;0,5503,3971,0,5972,2418,0;0,5504,569,0,5574,628,0;0,5504,570,0,5574,629,0;0,5504,571,0,5574,630,0;0,5506,814,0,5164,1009,0;0,5506,817,0,5201,1564,0;0,5513,176,0,5540,187,0;0,5522,672,0,5573,632,0;0,5522,673,0,5573,633,0;0,5522,674,0,5573,634,0;0,5527,1340,0,5412,1396,0;0,5538,170,0,5517,176,0;0,5543,1355,0,5572,1307,0;0,5551,1805,0,5556,1825,0;0,5552,1805,0,5557,1825,0;0,5553,1805,0,5557,1825,0;0,5556,1826,0,5551,1806,0;0,5557,1317,0,5396,1297,0;0,5557,1826,0,5552,1806,0;0,5566,1337,0,5620,1341,0;0,5571,1299,0,6012,3786,0;0,5572,632,0,5521,672,0;0,5572,633,0,5521,673,0;0,5572,634,0,5521,674,0;0,5572,1299,0,6013,3786,0;0,5573,628,0,5503,569,0;0,5573,629,0,5503,570,0;0,5573,630,0,5503,571,0;0,5588,630,0,1296,1080,1;0,5588,631,0,1296,1081,1;0,5588,632,0,1296,1082,1;0,5594,1840,0,5467,1804,0;0,5594,1841,0,5467,1805,0;0,5604,102,0,5514,147,1;0,5658,423,0,5697,3659,0;0,5670,2391,0,5753,325,0;0,5682,1437,0,5164,1009,0;0,5682,1440,0,5606,802,0;0,5686,385,0,2777,894,0;0,5686,386,0,2777,894,0;0,5686,387,0,2777,895,0;0,5687,1424,0,2923,3406,0;0,5690,569,0,5829,595,0;0,5691,1348,0,5690,1352,1;0,5697,3660,0,5658,424,0;0,5698,662,0,5793,527,0;0,5698,662,0,5793,527,0;0,5701,1320,0,5786,1336,0;0,5702,1320,0,5787,1336,0;0,5703,1320,0,5788,1336,0;0,5705,146,0,5705,305,0;0,5705,147,0,5705,306,0;0,5705,625,0,5703,639,0;0,5706,305,0,5705,146,0;0,5706,306,0,5705,147,0;0,5731,445,0,6087,3676,0;0,5731,3420,0,5490,2348,0;0,5733,554,0,5829,595,0;0,5748,362,0,313,786,0;0,5749,362,0,313,786,0;0,5750,362,0,314,786,0;0,5753,324,0,5670,2391,0;0,5753,437,0,2400,199,0;0,5757,2907,0,5791,1415,0;0,5757,2908,0,5791,1416,0;0,5757,2909,0,5791,1417,0;0,5786,1335,0,5701,1319,0;0,5787,1335,0,5702,1319,0;0,5788,1335,0,5703,1319,0;0,5792,1415,0,5758,2907,0;0,5792,1416,0,5758,2908,0;0,5792,1417,0,5758,2909,0;0,5824,631,0,2041,215,1;0,5825,631,0,2042,215,1;0,5825,631,0,2043,215,1;0,5826,465,0,1987,2063,0;0,5827,465,0,1988,2063,0;0,5827,593,0,5690,569,0;0,5828,465,0,1989,2063,0;0,5829,593,0,5690,569,0;0,5831,323,0,5849,238,0;0,5832,323,0,5850,238,0;0,5833,323,0,5851,238,0;0,5834,323,0,5852,238,0;0,5835,323,0,5853,238,0;0,5849,239,0,5831,324,0;0,5850,239,0,5832,324,0;0,5850,431,0,5127,3143,0;0,5850,432,0,5127,3143,0;0,5850,433,0,5127,3143,0;0,5850,434,0,5127,3143,0;0,5851,239,0,5833,324,0;0,5852,239,0,5834,324,0;0,5853,239,0,5835,324,0;0,5867,528,0,5703,639,0;0,5867,529,0,5703,639,0;0,5868,528,0,5703,639,0;0,5868,529,0,5703,639,0;0,5870,530,0,5690,569,0;0,5870,531,0,5690,569,0;0,5871,530,0,5690,569,0;0,5871,531,0,5690,569,0;0,5874,146,0,5208,2323,0;0,5875,19,0,5507,162,1;0,5875,146,0,5208,2322,0;0,5876,146,0,5208,2322,0;0,5877,146,0,5208,2322,0;0,5878,3817,0,5399,1289,0;0,5878,3818,0,5399,1290,0;0,5882,241,0,1998,81,0;0,5882,242,0,1998,81,0;0,5882,243,0,1998,81,0;0,5899,1411,0,1630,3320,0;0,5900,1411,0,1630,3320,0;0,5919,168,0,6083,144,0;0,5919,169,0,6083,145,0;0,5919,170,0,6083,146,0;0,5918,1410,0,5961,1408,0;0,5918,1411,0,5961,1408,0;0,5918,1412,0,5961,1409,0;0,5920,168,0,6083,144,0;0,5920,169,0,6083,145,0;0,5920,170,0,6083,146,0;0,5926,145,0,5970,147,0;0,5926,146,0,5970,147,0;0,5926,147,0,5970,147,0;0,5926,148,0,5970,147,0;0,5926,149,0,5970,147,0;0,5933,145,0,5970,147,0;0,5933,146,0,5970,147,0;0,5933,147,0,5970,147,0;0,5933,149,0,5970,147,0;0,5944,144,0,5970,147,0;0,5944,145,0,5970,147,0;0,5944,146,0,5970,147,0;0,5944,147,0,5970,147,0;0,5944,148,0,5970,147,0;0,5955,144,0,5970,147,0;0,5955,145,0,5970,147,0;0,5955,146,0,5970,147,0;0,5955,148,0,5970,147,0;0,5955,149,0,5970,147,0;0,5955,150,0,5970,147,0;0,5961,1408,0,5918,1411,0;0,5961,1409,0,5918,1412,0;0,5963,144,0,5970,147,0;0,5963,145,0,5970,147,0;0,5963,146,0,5970,147,0;0,5963,147,0,5970,147,0;0,5963,148,0,5970,147,0;0,5972,168,0,5905,100,0;0,5974,2697,0,2985,2890,0;0,5996,2367,0,5522,3921,0;0,6005,1378,0,5151,4062,0;0,6005,1379,0,5151,4062,0;0,6005,1380,0,5151,4063,0;0,6011,3787,0,5570,1300,0;0,6012,3787,0,5571,1300,0;0,6013,3787,0,5572,1300,0;0,6014,3787,0,5573,1300,0;0,6025,1344,0,5137,3664,0;0,6025,1345,0,5137,3664,0;0,6025,1346,0,5137,3665,0;0,6026,196,0,6987,805,0;0,6026,197,0,6987,805,0;0,6031,1499,0,1491,1640,0;0,6031,1501,0,1491,1642,0;0,6083,144,0,5918,168,0;0,6083,145,0,5918,169,0;0,6083,146,0,5918,170,0;0,6040,194,0,6059,90,1;0,6044,226,0,6041,204,0;0,6053,2407,0,5958,2345,0;0,6075,3332,0,6126,1410,0;0,6080,2340,0,6089,2400,0;0,6083,144,0,5920,168,0;0,6083,145,0,5920,169,0;0,6083,146,0,5920,170,0;0,6086,3676,0,5731,446,0;0,6107,179,0,6124,155,0;0,6115,176,0,6107,176,0;0,6125,1411,0,6075,3332,0;0,6126,1411,0,6075,3332,0;0,6127,1411,0,6075,3332,0;0,6137,1409,0,6140,1432,0;0,6138,1409,0,6140,1432,0;0,6140,1431,0,6137,1408,0;0,6141,1431,0,6138,1408,0;0,6159,130,0,6317,63,0;0,6161,163,0,6321,106,0;0,6164,73,0,6320,22,0;0,6172,21,0,765,1645,0;0,6172,22,0,765,1646,0;0,6172,23,0,765,1647,0;0,6173,176,0,6233,15,0;0,6174,176,0,6234,15,0;0,6175,176,0,6235,15,0;0,6176,176,0,6236,15,0;0,6177,176,0,6237,15,0;0,6178,176,0,6238,15,0;0,6179,176,0,6239,15,0;0,6211,106,0,6355,34,0;0,6233,14,0,6172,174,0;0,6234,14,0,6173,174,0;0,6235,14,0,6174,174,0;0,6236,14,0,6175,174,0;0,6237,14,0,6176,174,0;0,6238,14,0,6177,174,0;0,6239,14,0,6177,174,0;0,6240,14,0,6178,174,0;0,6256,97,0,6257,95,0;0,6260,97,0,6260,95,0;0,6262,95,0,6262,99,0;0,6263,97,0,6262,95,0;0,6269,97,0,6269,95,0;0,6273,97,0,6272,95,0;0,6276,40,0,6374,124,0;0,6310,890,0,1715,2996,0;0,6310,890,0,1716,2997,0;0,6310,891,0,1715,2996,0;0,6310,891,0,1716,2997,0;0,6310,892,0,1715,2997,0;0,6310,892,0,1716,2997,0;0,6310,893,0,1715,2997,0;0,6310,893,0,1716,2997,0;0,6316,62,0,6160,131,0;0,6319,19,0,6165,74,0;0,6356,32,0,6210,105,0;0,6373,49,0,801,1682,0;0,6374,49,0,801,1682,0;0,6374,125,0,6277,41,0;0,6375,49,0,801,1682,0;0,6376,49,0,801,1682,0;0,6377,49,0,801,1682,0;0,6469,97,0,6503,88,0;0,6540,116,0,6537,138,0;0,6540,117,0,6537,139,0;0,6554,104,0,6555,116,0;0,6568,77,0,6576,73,0;0,6477,860,0,6574,889,0;0,6577,159,0,6577,177,0;0,6578,159,0,6577,177,0;0,6579,159,0,6577,177,0;0,6582,109,0,6553,142,0;0,6588,867,0,6488,849,0;0,6589,178,0,6519,116,0;0,6941,987,0,5524,3353,0;0,6972,802,0,5905,21,0;0,6992,1367,5,511,585,0;0,6993,1367,5,512,585,0;0,6994,1367,5,513,585,0;0,7045,1090,0,5405,2640,0;0,7049,709,0,2923,3406,0;0,7149,756,5,974,419,1;0,7150,756,5,975,419,1;0,7151,756,5,976,419,1;0,7152,756,5,977,419,1;1,311,786,1,314,784,0;1,311,786,1,5750,350,0;1,313,786,1,5748,361,0;1,314,786,1,5749,361,0;1,424,3283,1,5267,2757,0;1,512,1559,1,5394,127,1;1,513,1559,1,5395,127,1;1,514,1559,1,5396,127,1;1,588,1637,1,6472,868,1;1,6477,860,1,6574,889,0;1,766,1645,1,6174,21,0;1,766,1646,1,6174,22,0;1,766,1647,1,6174,23,0;1,1013,1433,1,5138,2016,0;1,1013,1434,1,5138,2016,0;1,1142,3621,1,1414,3828,0;1,1163,2204,1,1171,2202,0;1,1163,2208,1,1165,2214,0;1,1165,2204,1,1163,2211,0;1,1166,2237,1,1163,2205,0;1,1406,3996,1,1414,3828,0;1,1409,3824,1,1124,3623,0;1,1419,3832,1,1466,4015,0;1,1477,1471,1,6432,2677,0;1,1477,1472,1,6432,2678,0;1,1477,1473,1,6432,2679,0;1,1477,1474,1,6432,2680,0;1,1477,1475,1,6432,2681,0;1,1491,1640,1,6032,1499,0;1,1491,1642,1,6032,1501,0;1,1593,2488,1,1600,2489,0;1,1594,2488,1,1600,2489,0;1,1595,2488,1,1600,2489,0;1,1629,3320,1,5899,1411,0;1,1653,2963,1,1677,2987,0;1,1677,2987,1,1675,2987,0;1,1714,2996,1,6308,892,0;1,1714,2996,1,6309,891,0;1,1714,2997,1,6308,892,0;1,1714,2997,1,6309,892,0;1,1987,2062,1,5826,464,0;1,1988,2062,1,5827,464,0;1,1989,2062,1,5828,464,0;1,1997,81,1,5881,242,0;1,2384,836,1,5615,1996,1;1,2384,837,1,5615,1997,1;1,2384,838,1,5615,1998,1;1,2399,198,1,5753,436,0;1,2400,198,1,5754,436,0;1,2420,883,1,5392,1959,1;1,2421,883,1,5393,1959,1;1,2422,883,1,5394,1959,1;1,2455,858,1,5388,2027,1;1,2456,858,1,5389,2027,1;1,2457,858,1,5390,2027,1;1,2498,916,1,5455,1864,1;1,2499,916,1,5456,1864,1;1,2500,916,1,5457,1864,1;1,2544,850,1,5578,1927,1;1,2545,850,1,5579,1927,1;1,2546,850,1,5580,1927,1;1,2603,2120,1,2605,2130,0;1,2603,2121,1,2605,2130,0;1,2618,977,1,2727,2133,1;1,2669,2071,1,2666,2099,0;1,2669,2072,1,2666,2099,0;1,2669,2073,1,2666,2099,0;1,2676,2241,1,2691,2234,0;1,2676,2242,1,2691,2234,0;1,2685,2063,1,2685,2063,0;1,2758,2092,1,2756,2097,0;1,2759,2092,1,2756,2097,0;1,2776,895,1,5685,387,0;1,2923,3405,1,5687,1423,0;1,2985,2890,1,5974,2697,0;1,3508,2777,1,5912,355,1;1,4110,430,1,5187,639,0;1,4111,430,1,5188,639,0;1,4112,430,1,5189,639,0;1,4194,3260,5,1127,1215,0;1,4195,3260,5,1129,1215,0;1,4300,968,1,4442,1122,0;1,4436,1107,1,4300,992,0;1,4443,1137,1,4487,1475,0;1,4449,1107,1,4539,890,0;1,4449,1115,1,4671,1135,0;1,4496,1475,1,4442,1122,0;1,4540,898,1,4442,1122,0;1,4619,1203,1,4622,1204,0;1,4627,1207,1,4633,1202,0;1,4663,1134,1,4442,1122,0;1,4721,3813,1,5904,16,1;1,4722,3813,1,5905,16,1;1,4723,3813,1,5906,16,1;1,5126,3143,1,5849,432,0;1,5129,909,1,5142,798,0;1,5130,909,1,5143,798,0;1,5131,909,1,5144,798,0;1,5132,909,1,5145,798,0;1,5132,1946,1,5332,1379,0;1,5133,984,1,5152,808,0;1,5133,985,1,5152,809,0;1,5133,986,1,5152,810,0;1,5133,987,1,5152,811,0;1,5133,1946,1,5332,1379,0;1,5134,1946,1,5332,1379,0;1,5135,1946,1,5332,1379,0;1,5137,3664,1,6025,1344,0;1,5137,3665,1,6025,1345,0;1,5139,2015,1,1014,1434,0;1,5139,2016,1,1014,1434,0;1,5139,2017,1,1014,1434,0;1,5142,797,1,5129,908,0;1,5143,797,1,5130,908,0;1,5144,797,1,5131,908,0;1,5145,797,1,5132,908,0;1,5150,4062,1,6005,1378,0;1,5150,4063,1,6005,1380,0;1,5153,808,1,5134,984,0;1,5153,809,1,5134,985,0;1,5153,810,1,5134,986,0;1,5153,811,1,5134,987,0;1,5186,639,1,4110,430,0;1,5187,639,1,4110,430,0;1,5188,639,1,4111,430,0;1,5189,639,1,4112,430,0;1,5191,152,1,1367,891,0;1,5200,71,1,5211,22,0;1,5203,2327,1,5876,147,0;1,5207,2322,1,5877,147,0;1,5207,2323,1,5876,147,0;1,5216,586,1,5304,533,0;1,5217,18,1,5204,74,0;1,5217,586,1,5305,533,0;1,5218,586,1,5306,533,0;1,5218,761,1,5305,651,0;1,5219,761,1,5306,651,0;1,5242,1007,1,1175,2635,1;1,5243,1007,1,1176,2635,1;1,5244,1007,1,1177,2635,1;1,5260,1841,1,3613,2592,0;1,5267,2757,1,424,3283,0;1,5272,2043,1,5363,1290,0;1,5273,2043,1,5363,1290,0;1,5304,532,1,5216,585,0;1,5305,532,1,5217,585,0;1,5305,650,1,5218,760,0;1,5306,532,1,5218,585,0;1,5306,650,1,5219,760,0;1,5329,1381,1,5134,1947,0;1,5330,1381,1,5134,1947,0;1,5331,1381,1,5134,1947,0;1,5332,1381,1,5134,1947,0;1,5333,1381,1,5134,1947,0;1,5334,1381,1,5134,1947,0;1,5340,1599,1,5426,3122,0;1,5341,1599,1,5426,3122,0;1,5346,578,1,5137,649,1;1,5363,1289,1,5272,2041,0;1,5364,1289,1,5272,2041,0;1,5386,755,1,5408,858,0;1,5386,756,1,5408,859,0;1,5386,757,1,5408,860,0;1,5400,1288,1,5879,3817,0;1,5400,1289,1,5879,3817,0;1,5400,1290,1,5879,3818,0;1,5426,3123,1,5341,1602,0;1,5451,1360,1,5435,1341,0;1,5453,1360,1,5436,1341,0;1,5466,1804,1,5593,1840,0;1,5466,1805,1,5593,1841,0;1,5466,1806,1,5593,1842,0;1,5490,19,1,5514,10,1;1,5504,569,1,5574,628,0;1,5504,570,1,5574,629,0;1,5504,571,1,5574,630,0;1,5513,176,1,5540,187,0;1,5522,672,1,5573,632,0;1,5522,673,1,5573,633,0;1,5522,674,1,5573,634,0;1,5527,1340,1,5412,1396,0;1,5538,170,1,5517,176,0;1,5543,1355,1,5572,1307,0;1,5551,1805,1,5556,1825,0;1,5552,1805,1,5557,1825,0;1,5553,1805,1,5557,1825,0;1,5556,1826,1,5551,1806,0;1,5557,1317,1,5396,1297,0;1,5557,1826,1,5552,1806,0;1,5566,1337,1,5620,1341,0;1,5571,1299,1,6012,3786,0;1,5572,632,1,5521,672,0;1,5572,633,1,5521,673,0;1,5572,634,1,5521,674,0;1,5572,1299,1,6013,3786,0;1,5573,628,1,5503,569,0;1,5573,629,1,5503,570,0;1,5573,630,1,5503,571,0;1,5588,630,1,1296,1080,1;1,5588,631,1,1296,1081,1;1,5588,632,1,1296,1082,1;1,5594,1840,1,5467,1804,0;1,5594,1841,1,5467,1805,0;1,5604,102,1,5514,147,1;1,5658,423,1,5697,3659,0;1,5670,2391,1,5753,325,0;1,5686,385,1,2777,894,0;1,5686,386,1,2777,894,0;1,5686,387,1,2777,895,0;1,5687,1424,1,2923,3406,0;1,5690,569,1,5829,595,0;1,5691,1348,1,5690,1352,1;1,5697,3660,1,5658,424,0;1,5698,662,1,5793,527,0;1,5698,662,1,5793,527,0;1,5701,1320,1,5786,1336,0;1,5702,1320,1,5787,1336,0;1,5703,1320,1,5788,1336,0;1,5705,146,1,5705,305,0;1,5705,147,1,5705,306,0;1,5705,625,1,5703,639,0;1,5706,305,1,5705,146,0;1,5706,306,1,5705,147,0;1,5731,445,1,6087,3676,0;1,5733,554,1,5829,595,0;1,5748,362,1,313,786,0;1,5749,362,1,313,786,0;1,5750,362,1,314,786,0;1,5753,324,1,5670,2391,0;1,5753,437,1,2400,199,0;1,5757,2907,1,5791,1415,0;1,5757,2908,1,5791,1416,0;1,5757,2909,1,5791,1417,0;1,5786,1335,1,5701,1319,0;1,5787,1335,1,5702,1319,0;1,5788,1335,1,5703,1319,0;1,5792,1415,1,5758,2907,0;1,5792,1416,1,5758,2908,0;1,5792,1417,1,5758,2909,0;1,5824,631,1,2041,215,1;1,5825,631,1,2042,215,1;1,5825,631,1,2043,215,1;1,5826,465,1,1987,2063,0;1,5827,465,1,1988,2063,0;1,5827,593,1,5690,569,0;1,5828,465,1,1989,2063,0;1,5829,593,1,5690,569,0;1,5831,323,1,5849,238,0;1,5832,323,1,5850,238,0;1,5833,323,1,5851,238,0;1,5834,323,1,5852,238,0;1,5835,323,1,5853,238,0;1,5849,239,1,5831,324,0;1,5850,239,1,5832,324,0;1,5850,431,1,5127,3143,0;1,5850,432,1,5127,3143,0;1,5850,433,1,5127,3143,0;1,5850,434,1,5127,3143,0;1,5851,239,1,5833,324,0;1,5852,239,1,5834,324,0;1,5853,239,1,5835,324,0;1,5867,528,1,5703,639,0;1,5867,529,1,5703,639,0;1,5868,528,1,5703,639,0;1,5868,529,1,5703,639,0;1,5870,530,1,5690,569,0;1,5870,531,1,5690,569,0;1,5871,530,1,5690,569,0;1,5871,531,1,5690,569,0;1,5874,146,1,5208,2323,0;1,5875,19,1,5507,162,1;1,5875,146,1,5208,2322,0;1,5876,146,1,5208,2322,0;1,5877,146,1,5208,2322,0;1,5878,3817,1,5399,1289,0;1,5878,3818,1,5399,1290,0;1,5882,241,1,1998,81,0;1,5882,242,1,1998,81,0;1,5919,168,1,6083,144,0;1,5919,169,1,6083,145,0;1,5919,170,1,6083,146,0;1,5906,96,1,5977,169,0;1,5906,4069,1,2494,3576,1;1,5918,1410,1,5961,1408,0;1,5918,1411,1,5961,1408,0;1,5918,1412,1,5961,1409,0;1,5920,168,1,6083,144,0;1,5920,169,1,6083,145,0;1,5920,170,1,6083,146,0;1,5926,145,1,5970,147,0;1,5926,146,1,5970,147,0;1,5926,147,1,5970,147,0;1,5926,148,1,5970,147,0;1,5926,149,1,5970,147,0;1,5933,145,1,5970,147,0;1,5933,146,1,5970,147,0;1,5933,147,1,5970,147,0;1,5933,149,1,5970,147,0;1,5944,144,1,5970,147,0;1,5944,145,1,5970,147,0;1,5944,146,1,5970,147,0;1,5944,147,1,5970,147,0;1,5944,148,1,5970,147,0;1,5955,144,1,5970,147,0;1,5955,145,1,5970,147,0;1,5955,146,1,5970,147,0;1,5955,148,1,5970,147,0;1,5955,149,1,5970,147,0;1,5955,150,1,5970,147,0;1,5961,1408,1,5918,1411,0;1,5961,1409,1,5918,1412,0;1,5963,144,1,5970,147,0;1,5963,145,1,5970,147,0;1,5963,146,1,5970,147,0;1,5963,147,1,5970,147,0;1,5963,148,1,5970,147,0;1,5972,168,1,5905,100,0;1,5974,2697,1,2985,2890,0;1,6005,1378,1,5151,4062,0;1,6005,1379,1,5151,4062,0;1,6005,1380,1,5151,4063,0;1,6011,3787,1,5570,1300,0;1,6012,3787,1,5571,1300,0;1,6013,3787,1,5572,1300,0;1,6014,3787,1,5573,1300,0;1,6025,1344,1,5137,3664,0;1,6025,1345,1,5137,3664,0;1,6025,1346,1,5137,3665,0;1,6083,144,1,5918,168,0;1,6083,145,1,5918,169,0;1,6083,146,1,5918,170,0;1,6040,192,1,6059,88,1;1,6040,193,1,6059,89,1;1,6040,194,1,6059,90,1;1,6044,226,1,6041,204,0;1,6075,3332,1,6126,1410,0;1,6083,144,1,5920,168,0;1,6083,145,1,5920,169,0;1,6083,146,1,5920,170,0;1,6086,3676,1,5731,446,0;1,6107,179,1,6124,155,0;1,6115,176,1,6107,176,0;1,6125,1411,1,6075,3332,0;1,6126,1411,1,6075,3332,0;1,6127,1411,1,6075,3332,0;1,6137,1409,1,6140,1432,0;1,6138,1409,1,6140,1432,0;1,6140,1431,1,6137,1408,0;1,6141,1431,1,6138,1408,0;1,6159,130,1,6317,63,0;1,6161,163,1,6321,106,0;1,6164,73,1,6320,22,0;1,6172,21,1,765,1645,0;1,6172,22,1,765,1646,0;1,6172,23,1,765,1647,0;1,6173,176,1,6233,15,0;1,6174,176,1,6234,15,0;1,6175,176,1,6235,15,0;1,6176,176,1,6236,15,0;1,6177,176,1,6237,15,0;1,6178,176,1,6238,15,0;1,6179,176,1,6239,15,0;1,6211,106,1,6355,34,0;1,6233,14,1,6172,174,0;1,6234,14,1,6173,174,0;1,6235,14,1,6174,174,0;1,6236,14,1,6175,174,0;1,6237,14,1,6176,174,0;1,6238,14,1,6177,174,0;1,6239,14,1,6177,174,0;1,6240,14,1,6178,174,0;1,6256,97,1,6257,95,0;1,6260,97,1,6260,95,0;1,6262,95,1,6262,99,0;1,6263,97,1,6262,95,0;1,6269,97,1,6269,95,0;1,6273,97,1,6272,95,0;1,6276,40,1,6374,124,0;1,6310,890,1,1715,2996,0;1,6310,890,1,1716,2997,0;1,6310,891,1,1715,2996,0;1,6310,891,1,1716,2997,0;1,6310,892,1,1715,2997,0;1,6310,892,1,1716,2997,0;1,6310,893,1,1715,2997,0;1,6310,893,1,1716,2997,0;1,6316,62,1,6160,131,0;1,6319,19,1,6165,74,0;1,6356,32,1,6210,105,0;1,6373,49,1,801,1682,0;1,6374,49,1,801,1682,0;1,6374,125,1,6277,41,0;1,6375,49,1,801,1682,0;1,6376,49,1,801,1682,0;1,6377,49,1,801,1682,0;1,6440,2677,1,1477,1471,0;1,6440,2678,1,1477,1472,0;1,6440,2679,1,1477,1473,0;1,6440,2680,1,1477,1474,0;1,6440,2681,1,1477,1475,0;1,6469,97,1,6503,88,0;1,6540,116,1,6537,138,0;1,6540,117,1,6537,139,0;1,6554,104,1,6555,116,0;1,6568,77,1,6576,73,0;1,6577,159,1,6577,177,0;1,6578,159,1,6577,177,0;1,6579,159,1,6577,177,0;1,6582,109,1,6553,142,0;1,6588,867,1,6488,849,0;1,6589,178,1,6519,116,0;2,10,872,2,10,1518,0;2,10,1519,2,10,873,0;2,11,872,2,11,1518,0;2,11,1519,2,11,873,0;2,12,872,2,12,1518,0;2,12,1519,2,12,873,0;2,78,1365,2,393,1586,0;2,79,1365,2,394,1586,0;2,80,1365,2,395,1586,0;2,81,1365,2,396,1586,0;2,131,129,2,284,68,0;2,132,129,2,285,68,0;2,133,129,2,286,68,0;2,134,129,2,287,68,0;2,154,1472,2,575,1155,0;2,155,88,2,357,40,0;2,155,89,2,357,41,0;2,155,90,2,357,42,0;2,155,1472,2,576,1155,0;2,156,1472,2,577,1155,0;2,164,746,2,636,815,0;2,242,25,2,372,29,0;2,242,26,2,372,30,0;2,242,27,2,372,31,0;2,265,130,2,284,72,0;2,265,1587,2,225,1334,1;2,266,130,2,285,72,0;2,266,1587,2,226,1334,1;2,267,130,2,286,72,0;2,267,1587,2,227,1334,1;2,268,130,2,287,72,0;2,272,141,2,555,427,0;2,273,141,2,556,427,0;2,274,141,2,557,427,0;2,284,67,2,131,128,0;2,284,73,2,265,131,0;2,285,67,2,132,128,0;2,285,73,2,266,131,0;2,286,67,2,133,128,0;2,286,73,2,267,131,0;2,287,67,2,134,128,0;2,287,73,2,268,131,0;2,313,1329,2,327,1593,1;2,314,1329,2,328,1593,1;2,315,1329,2,329,1593,1;2,330,1593,2,315,1329,0;2,348,1427,2,18,1198,1;2,349,1427,2,19,1198,1;2,350,1427,2,20,1198,1;2,351,1427,2,21,1198,1;2,358,40,2,156,88,0;2,358,41,2,156,89,0;2,358,42,2,156,90,0;2,371,29,2,241,25,0;2,371,30,2,241,26,0;2,371,31,2,241,27,0;2,393,1587,2,78,1366,0;2,394,1587,2,79,1366,0;2,395,1587,2,80,1366,0;2,396,1587,2,81,1366,0;2,429,113,2,548,455,0;2,531,1533,2,810,875,0;2,532,1533,2,811,875,0;2,533,1533,2,812,875,0;2,534,1533,2,813,875,0;2,546,455,2,426,113,1;2,547,455,2,427,113,1;2,548,455,2,428,113,1;2,555,426,2,272,140,0;2,556,426,2,273,140,0;2,557,426,2,274,140,0;2,575,1156,2,154,1473,0;2,576,1156,2,155,1473,0;2,577,1156,2,156,1473,0;2,626,1527,2,650,1298,0;2,627,1527,2,651,1298,0;2,628,1527,2,652,1298,0;2,629,1527,2,653,1298,0;2,636,813,2,164,743,0;2,650,1297,2,626,1526,0;2,651,1297,2,627,1526,0;2,652,1297,2,628,1526,0;2,653,1297,2,629,1526,0;2,668,928,2,3,1267,1;2,668,929,2,3,1268,1;2,668,930,2,3,1269,1;2,694,1490,2,719,1490,0;2,694,1491,2,719,1491,0;2,694,1492,2,719,1492,0;2,694,1493,2,719,1493,0;2,709,667,2,912,451,0;2,710,667,2,912,452,0;2,711,667,2,912,453,0;2,712,1490,2,686,1490,0;2,712,1491,2,686,1491,0;2,712,1492,2,686,1492,0;2,712,1493,2,686,1493,0;2,722,1505,2,833,1550,0;2,722,1506,2,833,1551,0;2,722,1507,2,833,1552,0;2,722,1508,2,833,1553,0;2,747,1539,2,785,1514,0;2,747,1555,2,785,1570,0;2,751,1473,2,763,1479,0;2,751,1479,2,763,1555,0;2,752,1549,2,751,1484,0;2,775,1467,2,658,1498,0;2,775,1492,2,827,1515,0;2,776,1546,2,798,1547,0;2,776,1549,2,798,1547,0;2,777,1547,2,798,1547,0;2,777,1548,2,798,1547,0;2,777,1553,2,798,1547,0;2,777,1555,2,798,1547,0;2,778,1541,2,798,1547,0;2,778,1554,2,787,1538,0;2,779,1540,2,798,1547,0;2,779,1542,2,798,1547,0;2,780,1541,2,785,1554,0;2,780,1546,2,798,1547,0;2,780,1550,2,798,1547,0;2,780,1554,2,798,1547,0;2,781,1545,2,789,1552,0;2,781,1547,2,798,1547,0;2,781,1549,2,798,1547,0;2,781,1551,2,798,1547,0;2,781,1553,2,798,1547,0;2,781,1555,2,789,1556,0;2,782,1538,2,798,1547,0;2,782,1542,2,785,1546,0;2,782,1546,2,798,1547,0;2,782,1550,2,783,1538,0;2,782,1554,2,798,1547,0;2,783,1537,2,798,1547,0;2,783,1539,2,787,1542,0;2,783,1541,2,798,1547,0;2,783,1543,2,798,1547,0;2,784,1538,2,798,1547,0;2,784,1542,2,798,1547,0;2,784,1546,2,776,1548,0;2,784,1550,2,777,1554,0;2,784,1554,2,798,1547,0;2,784,1580,2,798,1547,0;2,785,1524,2,798,1547,0;2,785,1545,2,798,1547,0;2,785,1547,2,798,1547,0;2,785,1549,2,798,1547,0;2,785,1551,2,798,1547,0;2,785,1553,2,798,1547,0;2,785,1555,2,783,1542,0;2,786,1538,2,798,1547,0;2,786,1542,2,798,1547,0;2,786,1546,2,798,1547,0;2,786,1550,2,798,1547,0;2,786,1554,2,798,1547,0;2,787,1537,2,781,1546,0;2,787,1539,2,798,1547,0;2,787,1541,2,798,1547,0;2,787,1543,2,781,1554,0;2,788,1538,2,798,1547,0;2,788,1542,2,798,1547,0;2,788,1546,2,798,1547,0;2,788,1552,2,798,1547,0;2,788,1556,2,798,1547,0;2,789,1545,2,779,1541,0;2,789,1547,2,798,1547,0;2,789,1551,2,798,1547,0;2,789,1553,2,789,1546,0;2,789,1555,2,798,1547,0;2,789,1557,2,785,1550,0;2,790,1546,2,798,1547,0;2,790,1552,2,798,1547,0;2,790,1556,2,798,1547,0;2,791,1545,2,798,1547,0;2,791,1546,2,798,1547,0;2,791,1547,2,798,1547,0;2,791,1548,2,781,1547,0;2,791,1549,2,798,1547,0;2,791,1550,2,798,1547,0;2,810,874,2,532,1532,0;2,811,874,2,533,1532,0;2,812,874,2,534,1532,0;2,812,1546,2,848,1434,0;2,812,1547,2,848,1435,0;2,812,1548,2,848,1436,0;2,812,1549,2,848,1437,0;2,827,777,2,1975,114,0;2,827,778,2,1975,114,0;2,827,779,2,1975,114,0;2,828,777,2,1975,114,0;2,828,778,2,1975,114,0;2,828,779,2,1975,114,0;2,829,777,2,1975,114,0;2,829,778,2,1975,114,0;2,829,779,2,1975,114,0;2,838,1550,2,728,1505,0;2,838,1551,2,728,1506,0;2,838,1552,2,728,1507,0;2,838,1553,2,728,1508,0;2,843,1434,2,807,1546,0;2,843,1435,2,807,1547,0;2,843,1436,2,807,1548,0;2,843,1437,2,807,1549,0;2,871,1433,2,897,1449,0;2,871,1434,2,897,1450,0;2,871,1435,2,897,1451,0;2,871,1436,2,897,1452,0;2,871,1437,2,897,1453,0;2,879,1490,2,960,1425,0;2,879,1491,2,960,1426,0;2,879,1492,2,960,1427,0;2,879,1493,2,960,1428,0;2,892,1449,2,866,1433,0;2,892,1450,2,866,1434,0;2,892,1451,2,866,1435,0;2,892,1452,2,866,1436,0;2,892,1453,2,866,1437,0;2,904,1360,2,1014,1506,1;2,904,1361,2,1014,1507,1;2,904,1362,2,1014,1508,1;2,904,1363,2,1014,1509,1;2,911,451,2,709,668,0;2,911,452,2,710,668,0;2,911,453,2,711,668,0;2,938,494,2,83,749,1;2,939,494,2,84,749,1;2,940,494,2,85,749,1;2,941,494,2,86,749,1;2,948,1464,2,951,1442,1;2,948,1465,2,951,1443,1;2,948,1466,2,951,1444,1;2,948,1467,2,951,1445,1;2,954,1425,2,874,1490,0;2,954,1426,2,874,1491,0;2,954,1427,2,874,1492,0;2,954,1428,2,874,1493,0;2,954,1429,2,874,1493,0;2,1029,1153,2,1349,1510,1;2,1029,1154,2,1349,1511,1;2,1029,1155,2,1349,1512,1;2,1037,579,2,1790,66,0;2,1037,580,2,1790,67,0;2,1037,581,2,1790,68,0;2,1038,578,2,1786,70,0;2,1038,579,2,1786,70,0;2,1038,581,2,1786,65,0;2,1038,582,2,1786,65,0;2,1039,578,2,1787,70,0;2,1039,582,2,1787,65,0;2,1040,578,2,1788,70,0;2,1040,579,2,1788,70,0;2,1040,581,2,1788,65,0;2,1040,582,2,1788,65,0;2,1041,579,2,1785,66,0;2,1041,580,2,1785,67,0;2,1041,581,2,1785,68,0;2,1139,592,2,1238,583,0;2,1140,592,2,1238,584,0;2,1141,592,2,1238,585,0;2,1142,592,2,1238,585,0;2,1237,583,2,1139,593,0;2,1237,584,2,1140,593,0;2,1237,585,2,1141,593,0;2,1250,1511,2,1268,1510,0;2,1250,1512,2,1268,1510,0;2,1268,1508,2,1250,1508,1;2,1268,1509,2,1250,1509,1;2,1268,1510,2,1250,1510,1;2,1349,1509,2,1030,1153,0;2,1362,1031,2,1981,1107,1;2,1363,1031,2,1982,1107,1;2,1364,1031,2,1983,1107,1;2,1419,909,2,1784,994,1;2,1420,909,2,1785,994,1;2,1421,909,2,1786,994,1;2,1456,1328,2,1479,1494,1;2,1456,1329,2,1479,1495,1;2,1456,1330,2,1479,1496,1;2,1479,1493,2,1456,1328,0;2,1479,1497,2,1456,1330,0;2,1516,879,2,1363,1105,1;2,1745,1236,2,2112,829,1;2,1746,1236,2,2113,829,1;2,1747,1236,2,2114,829,1;2,1748,1236,2,2115,829,1;2,1783,994,2,1419,909,0;2,1786,66,2,1038,583,0;2,1786,67,2,1042,579,0;2,1786,68,2,1042,580,0;2,1786,69,2,1042,581,0;2,1787,66,2,1039,583,0;2,1787,69,2,1038,577,0;2,1787,994,2,1421,909,0;2,1788,66,2,1040,583,0;2,1788,69,2,1039,577,0;2,1788,994,2,1421,909,0;2,1789,66,2,1041,583,0;2,1789,67,2,1036,579,0;2,1789,68,2,1036,578,0;2,1789,69,2,1036,581,0;2,1861,980,2,1490,877,1;2,1861,981,2,1490,878,1;2,1861,982,2,1490,879,1;2,1861,983,2,1490,880,1;2,1861,984,2,1490,880,0;2,1978,114,2,835,778,0;2,1978,115,2,835,778,0;2,1978,116,2,835,778,0;2,1978,117,2,835,778,0;2,1979,114,2,835,778,0;2,1979,115,2,835,778,0;2,1979,116,2,835,778,0;2,1979,117,2,835,778,0;2,1980,114,2,835,778,0;2,1980,115,2,835,778,0;2,1980,116,2,835,778,0;2,1980,117,2,835,778,0;2,1980,1107,2,1362,1031,0;2,1981,114,2,835,778,0;2,1981,115,2,835,778,0;2,1981,116,2,835,778,0;2,1981,117,2,835,778,0;2,1984,1107,2,1364,1031,0;2,2116,829,2,1748,1236,0;2,2186,294,2,2186,33,1;2,2187,294,2,2187,33,1;2,2187,320,2,1787,569,0;2,2188,294,2,2188,33,1;2,2188,320,2,1787,569,1;2,2189,294,2,2189,33,1;2,2189,320,2,1788,569,1;3,3,128,4,259,785,0;3,4,128,4,259,785,0;3,5,128,4,259,785,0;3,6,128,4,259,785,0;3,7,128,4,259,785,0;3,8,128,4,259,785,0;3,61,523,3,63,352,0;3,61,524,3,63,352,0;3,61,525,3,63,352,0;3,61,526,3,63,352,0;3,64,336,4,983,195,0;3,64,337,4,983,195,0;3,64,338,4,983,195,0;3,64,339,4,983,195,0;3,66,351,3,63,524,0;3,66,352,3,63,524,0;3,66,353,3,63,524,0;3,66,354,3,63,524,0;3,73,688,3,100,556,0;3,73,689,3,100,556,0;3,73,690,3,100,556,0;3,73,691,3,100,556,0;3,73,692,3,100,556,0;3,73,693,3,100,556,0;3,73,694,3,100,556,0;3,84,1673,3,156,1613,0;3,103,555,3,76,691,0;3,103,556,3,76,691,0;3,103,557,3,76,691,0;3,103,558,3,76,691,0;3,156,1609,3,87,1673,0;3,157,1609,3,87,1673,0;3,328,1972,3,1731,978,0;3,328,1973,3,1731,978,0;3,328,1974,3,1731,978,0;3,328,1975,3,1731,978,0;3,330,706,3,384,733,0;3,331,769,3,384,733,0;3,355,745,3,384,733,0;3,375,802,4,770,1209,0;3,384,810,3,403,1167,0;3,389,704,3,384,733,0;3,391,777,4,770,1209,0;3,403,1169,3,385,811,0;3,404,1169,3,385,808,0;3,405,1169,3,385,808,0;3,407,254,3,428,318,0;3,408,254,3,428,318,0;3,409,254,3,428,318,0;3,411,1117,3,422,806,0;3,412,1086,3,391,803,0;3,412,1123,3,417,806,0;3,421,1092,3,391,803,0;3,426,780,3,384,733,0;3,427,321,3,422,327,0;3,428,321,3,422,327,0;3,429,321,3,422,327,0;3,496,49,3,2350,1270,0;3,2315,1267,3,381,132,0;3,2315,1268,3,381,132,0;3,2315,1269,3,381,132,0;3,2316,1267,3,381,132,0;3,2316,1269,3,381,132,0;3,2317,1266,3,381,132,0;3,2317,1267,3,381,132,0;3,2317,1268,3,381,132,0;3,2317,1269,3,381,132,0;4,257,783,3,5,128,0;4,258,783,3,5,128,0;4,259,783,3,5,128,0;4,260,783,3,5,128,0;4,987,196,3,67,337,0;4,988,194,3,67,337,0;4,988,195,3,67,337,0;4,988,197,3,67,337,0;5,33,241,5,996,3842,0;5,34,241,5,997,3842,0;5,35,241,5,998,3842,0;5,36,241,5,999,3842,0;5,37,241,5,1000,3842,0;5,40,366,5,79,353,0;5,40,564,5,79,551,0;5,40,772,5,79,759,0;5,45,366,5,81,399,0;5,45,564,5,81,597,0;5,45,772,5,81,805,0;5,52,343,5,80,338,0;5,52,389,5,79,379,0;5,52,541,5,80,536,0;5,52,587,5,79,577,0;5,52,749,5,80,744,0;5,52,795,5,79,785,0;5,79,353,5,40,366,0;5,79,379,5,52,389,0;5,79,551,5,40,564,0;5,79,577,5,52,587,0;5,79,759,5,40,772,0;5,79,785,5,52,795,0;5,80,338,5,52,343,0;5,80,536,5,52,541,0;5,80,744,5,52,749,0;5,81,399,5,45,366,0;5,81,597,5,45,564,0;5,81,805,5,45,772,0;5,317,136,5,310,159,0;5,317,182,5,346,192,0;5,344,146,5,349,138,0;5,344,172,5,349,180,0;5,345,131,5,345,128,0;5,442,152,5,850,272,0;5,442,153,5,850,273,0;5,442,166,5,850,286,0;5,442,167,5,850,287,0;5,511,584,0,6992,1367,0;5,512,584,0,6992,1367,0;5,513,584,0,6993,1367,0;5,514,584,0,6993,1367,0;5,519,919,5,1125,1075,0;5,519,919,5,1125,1076,0;5,520,919,5,1125,1075,0;5,520,919,5,1126,1076,0;5,521,919,5,1126,1075,0;5,522,919,5,1126,1075,0;5,594,3846,5,644,3857,0;5,644,3857,5,594,3846,0;5,841,272,5,434,152,0;5,841,273,5,434,153,0;5,841,287,5,433,167,0;5,843,286,5,433,166,0;5,996,3841,5,33,240,0;5,997,3841,5,34,240,0;5,998,3841,5,35,240,0;5,999,3841,5,36,240,0;5,1000,3841,5,37,240,0;5,1006,1110,5,1012,1071,0;5,1006,1127,5,1010,1144,0;5,1010,1144,5,1006,1127,0;5,1012,1071,5,1006,1110,0;5,1042,1047,5,1238,1151,0;5,1079,882,5,1080,977,0;5,1079,883,5,1080,977,0;5,1079,884,5,1080,977,0;5,1079,885,5,1080,977,0;5,1080,974,5,1081,883,0;5,1080,975,5,1081,883,0;5,1081,974,5,1081,883,0;5,1081,975,5,1081,883,0;5,1125,1076,5,520,920,0;5,1125,1215,1,4194,3261,0;5,1125,1215,1,4194,3261,0;5,1126,1076,5,521,920,0;5,1126,1215,1,4194,3261,0;5,1126,1215,1,4194,3261,0;5,1127,1215,1,4194,3261,0;5,1127,1215,1,4194,3261,0;5,1128,1215,1,4194,3261,0;5,1128,1215,1,4194,3261,0;5,1129,1215,1,4194,3261,0;5,1129,1215,1,4195,3261,0;5,1130,1215,1,4194,3261,0;5,1130,1215,1,4195,3261,0;5,1131,1215,1,4194,3261,0;5,1131,1215,1,4195,3261,0;5,1162,1120,5,1147,1089,0;5,1180,885,5,1187,1127,0;5,1183,1180,5,1183,1182,0;5,1183,1182,5,1183,1180,0;5,1183,1187,5,1183,1189,0;5,1183,1189,5,1183,1187,0;5,1184,1177,5,1186,1177,0;5,1186,1177,5,1184,1177,0;5,1186,1183,5,1184,1183,0;5,1187,1126,5,1180,884,0;5,1187,1173,5,1187,1175,0;5,1187,1175,5,1187,1173,0;5,1190,1185,5,1190,1183,0;5,1192,1186,5,1190,1186,0;5,1197,1189,5,1195,1189,0;5,1198,1187,5,1196,1187,0;5,1200,1187,5,1200,1190,0;5,1201,1189,5,1201,1186,0;5,1202,1183,5,1202,1185,0;5,1203,1190,5,1201,1190,0;5,1206,1181,5,1204,1181,0;5,1238,1151,5,1140,951,0;5,4194,3260,5,1128,1213,0;5,4195,3260,5,1129,1213,0;5,7149,756,0,511,585,0;5,7149,756,5,511,585,0;5,7150,756,5,511,585,0;5,7151,756,5,511,585,0;5,7152,756,5,511,585,0;5,7153,756,5,511,585,0"
LEARN_TELEPORTS = True
LINK_VAR = "SL_Mining_Links"
LINKS = []                  # dicts: sm sx sy dm dx dy (dir vx vy) kind fails
LINK_SRC = set()            # (map, x, y) tiles that teleport you: never stand there
POS = {"last": None, "prev": None, "moved_t": 0.0, "jumps": 0}


def _add_link(sm, sx, sy, dm, dx, dy, kind, vx=0, vy=0):
    for L in LINKS:
        if L["sm"] == sm and L["dm"] == dm and abs(L["sx"] - sx) <= 2 and abs(L["sy"] - sy) <= 2 \
                and abs(L["dx"] - dx) <= 4 and abs(L["dy"] - dy) <= 4:
            if kind == "learned":
                L["fails"] = 0
                L["seen"] = True
            return None
    L = {"sm": sm, "sx": sx, "sy": sy, "dm": dm, "dx": dx, "dy": dy, "kind": kind,
         "vx": vx, "vy": vy, "fails": 0, "seen": kind == "learned"}
    LINKS.append(L)
    LINK_SRC.add((sm, sx, sy))
    return L


def load_links():
    LINKS[:] = []
    LINK_SRC.clear()
    data = TELEPORTER_DATA if not TELEPORTER_DATA.startswith("@@") else ""
    for e in data.split(";"):
        p = e.split(",")
        if len(p) != 7:
            continue
        try:
            sm, sx, sy, dm, dx, dy, back = [int(v) for v in p]
        except ValueError:
            continue
        _add_link(sm, sx, sy, dm, dx, dy, "data")
        if back:
            _add_link(dm, dx, dy, sm, sx, sy, "data")
    try:
        raw = API.GetPersistentVar(LINK_VAR, "", API.PersistentVar.Char) or ""
    except Exception:
        raw = ""
    for e in str(raw).split(";"):
        p = e.split(",")
        if len(p) == 8:
            try:
                v = [int(t) for t in p]
                _add_link(v[0], v[1], v[2], v[3], v[4], v[5], "learned", v[6], v[7])
            except ValueError:
                pass


def save_links():
    try:
        API.SavePersistentVar(LINK_VAR, ";".join(
            "{},{},{},{},{},{},{},{}".format(L["sm"], L["sx"], L["sy"], L["dm"], L["dx"], L["dy"],
                                             L["vx"], L["vy"])
            for L in LINKS if L["kind"] == "learned")[-4000:], API.PersistentVar.Char)
    except Exception:
        pass


def learn_link(src, dst, prev):
    vx = vy = 0
    if prev is not None and prev[0] == src[0]:
        vx = (src[1] > prev[1]) - (src[1] < prev[1])
        vy = (src[2] > prev[2]) - (src[2] < prev[2])
    L = _add_link(src[0], src[1], src[2], dst[0], dst[1], dst[2], "learned", vx, vy)
    if L is not None:
        save_links()
        note("Learned a teleporter: {},{} -> {},{}.".format(src[1], src[2], dst[1], dst[2]), 68)


def on_map_change():
    TILE_CACHE.clear()
    STATIC_CHUNKS.clear()
    TRIED.clear()
    VISITED.clear()
    DEPLETED.clear()
    CRUMBS[:] = []
    SURVEY["map"] = cur_map()
    WORTH.clear()
    load_vein_log()
    load_bank_memory()
    load_crumbs()


def _cheb(ax, ay, bx, by):
    return max(abs(ax - bx), abs(ay - by))


ROUTE_WALK = 400            # longest single walking leg a teleporter route may use


def plan_links(tm, tx, ty):
    """Teleporters to take to reach (tm, tx, ty): (cost, [links]). Direct walk = []."""
    cm, px, py = cur_map(), API.Player.X, API.Player.Y
    best = (_cheb(px, py, tx, ty) if cm == tm else 10 ** 9, [])
    ok = [L for L in LINKS if L["fails"] < 2]
    first = [L for L in ok if L["sm"] == cm and _cheb(px, py, L["sx"], L["sy"]) <= ROUTE_WALK]
    last = [L for L in ok if L["dm"] == tm and _cheb(L["dx"], L["dy"], tx, ty) <= ROUTE_WALK]
    for L in first:
        if L["dm"] == tm:
            c = _cheb(px, py, L["sx"], L["sy"]) + 3 + _cheb(L["dx"], L["dy"], tx, ty)
            if c < best[0]:
                best = (c, [L])
    first.sort(key=lambda L: _cheb(px, py, L["sx"], L["sy"]))
    last.sort(key=lambda L: _cheb(L["dx"], L["dy"], tx, ty))
    for A in first[:30]:
        a = _cheb(px, py, A["sx"], A["sy"]) + 3
        for B in last[:30]:
            if A is B or B["sm"] != A["dm"]:
                continue
            leg = _cheb(A["dx"], A["dy"], B["sx"], B["sy"])
            if leg > ROUTE_WALK:
                continue
            c = a + leg + 3 + _cheb(B["dx"], B["dy"], tx, ty)
            if c < best[0]:
                best = (c, [A, B])
    if best[1] and cm == tm and best[0] > _cheb(px, py, tx, ty) * 0.7 - 5:
        return (_cheb(px, py, tx, ty), [])      # walking is about as good
    return best


def _jumped_to(L, j0):
    return POS["jumps"] != j0 and cur_map() == L["dm"] and \
        _cheb(API.Player.X, API.Player.Y, L["dx"], L["dy"]) <= 4


def _step(vx, vy):
    for name, v in DIRS8.items():
        if v == (vx, vy):
            API.Walk(name)
            API.Pause(0.5)
            track_position()
            return


def take_link(L):
    """Walk onto a teleporter and make sure it took us across."""
    j0 = POS["jumps"]
    dbg("Taking teleporter {},{} -> {},{}".format(L["sx"], L["sy"], L["dx"], L["dy"]))
    if (cur_map(), API.Player.X, API.Player.Y) != (L["sm"], L["sx"], L["sy"]):
        _go_to(L["sx"], L["sy"], 0, "To teleporter")
    if _jumped_to(L, j0):
        return True
    if POS["jumps"] == j0 and _cheb(API.Player.X, API.Player.Y, L["sx"], L["sy"]) <= 1:
        if L["vx"] or L["vy"]:
            # learned while walking: the real trigger may be a step or two further on
            for _ in range(3):
                _step(L["vx"], L["vy"])
                if POS["jumps"] != j0:
                    break
        else:
            # standing on it doesn't fire it (you may have just arrived on it): step off and on
            for name, (vx, vy) in DIRS8.items():
                nx, ny = API.Player.X + vx, API.Player.Y + vy
                if standable(nx, ny):
                    API.Walk(name)
                    API.Pause(0.5)
                    _step(-vx, -vy)
                    break
        t = 0.0
        while POS["jumps"] == j0 and t < 2.0:
            API.Pause(0.25)
            track_position()
            t += 0.25
    if _jumped_to(L, j0):
        L["fails"] = 0
        return True
    L["fails"] += 1
    dbg("Teleporter at {},{} didn't work ({} fails)".format(L["sx"], L["sy"], L["fails"]))
    return False


def route_dist(tm, tx, ty):
    return plan_links(tm, tx, ty)[0]


def go_to(tx, ty, near=1, label="", tmap=None):
    """Walk to (tx, ty) on map tmap, using teleporters when that is much shorter."""
    tm = cur_map() if tmap is None else tmap
    for _ in range(4):
        checkpoint()
        cost, route = plan_links(tm, tx, ty)
        if route:
            note("Route via {} teleporter{} ({} tiles).".format(
                len(route), "s" if len(route) > 1 else "", cost), 88)
        took = True
        for L in route:
            if not take_link(L):
                took = False
                break
        if not took:
            continue                # re-plan from wherever we ended up
        if cur_map() != tm:
            continue
        j0 = POS["jumps"]
        if _go_to(tx, ty, near, label):
            return True
        if POS["jumps"] == j0:
            return False
        dbg("Teleported somewhere on the way; re-planning")
    return False


DIRS8 = {"north": (0, -1), "northeast": (1, -1), "east": (1, 0), "southeast": (1, 1),
         "south": (0, 1), "southwest": (-1, 1), "west": (-1, 0), "northwest": (-1, -1)}


def explore_step(start, grid):
    """Nothing worth walking to: head toward the most unmined rock, else away from dug-out blocks."""
    dist = RADIUS * 2 + 1
    px, py = API.Player.X, API.Player.Y
    ranked = []
    for name, (vx, vy) in DIRS8.items():
        tx, ty = px + vx * dist, py + vy * dist
        if max(abs(tx - start[0]), abs(ty - start[1])) > WANDER_RANGE:
            continue
        # rock in a box centered a few tiles out that way
        cx, cy = px + vx * (dist + 2), py + vy * (dist + 2)
        rock = grid.count(cx - 4, cy - 4, cx + 4, cy + 4)
        dug = sum(1 for b in DEPLETED if b == bank(tx, ty))
        ranked.append((rock - dug * 20, name, tx, ty))
    ranked.sort(reverse=True)
    for rock, name, tx, ty in ranked:
        ox, oy = API.Player.X, API.Player.Y
        dbg("Exploring {} ({} rock that way)".format(name, max(rock, 0)))
        API.HeadMsg("Exploring " + name, API.Player, 88)
        if not walk_to(tx, ty):
            for _ in range(dist):
                API.Walk(name)
                API.Pause(0.45)
        if (API.Player.X, API.Player.Y) != (ox, oy):
            HEADING[0] = float(API.Player.X - ox)
            HEADING[1] = float(API.Player.Y - oy)
            return True
        dbg("Blocked going " + name)
    return False


def move_on(start):
    """Get to a new spot. Returns False if there is nowhere left to go."""
    spots, grid = find_spots(start)
    dbg("{} candidate spots in range".format(len(spots)))
    for x, y, fresh, ahead in spots[:8]:
        if API.StopRequested:
            return False
        d = compass(x - API.Player.X, y - API.Player.Y)
        API.HeadMsg("Heading {} ({} rock here, {} beyond)".format(d, fresh, ahead), API.Player, 88)
        if walk_to(x, y):
            dbg("Arrived near {},{}".format(x, y))
            return True
        dbg("Could not reach {},{}".format(x, y))
        VISITED.add((x, y))
    # nothing good nearby: walk to the best surveyed vein before wandering blind
    if survey_on() and CTRL.get("relocs", 0) < AUTO_RELOCATE:
        CTRL["relocs"] = CTRL.get("relocs", 0) + 1
        set_state("Surveying")
        if go_to_vein(auto=True):
            return True
    return explore_step(start, grid)


def reset_memory():
    DEPLETED.clear()
    TRIED.clear()
    VISITED.clear()
    HEADING[0] = HEADING[1] = 0.0
    SURVEY["went"].clear()
    try:
        API.SavePersistentVar(MEM_VAR, "", API.PersistentVar.Char)
    except Exception:
        pass


# ---- forge, smelting and satchel ---------------------------------------------
FORGE_VAR = "SL_Mining_Forge"
SATCHEL_VAR = "SL_Mining_Satchel"
FORGE = {}                  # map, x, y, z, graphic, serial
SATCHEL = [0]


def load_targets():
    try:
        raw = API.GetPersistentVar(FORGE_VAR, "", API.PersistentVar.Char) or ""
        p = [int(v) for v in str(raw).split(",")] if raw else []
        if len(p) == 6:
            FORGE.update(zip(["map", "x", "y", "z", "graphic", "serial"], p))
    except Exception:
        pass
    try:
        SATCHEL[0] = int(API.GetPersistentVar(SATCHEL_VAR, "0", API.PersistentVar.Char) or 0)
    except Exception:
        SATCHEL[0] = 0


def set_forge():
    sysmsg("[Mining] Target your forge.", 68)
    obj = API.RequestAnyTarget(20)
    if obj is None:
        sysmsg("[Mining] No forge set.", 33)
        return
    serial = 0
    try:
        serial = int(obj.Serial or 0)
    except Exception:
        serial = 0
    FORGE.clear()
    FORGE.update({"map": API.GetMap(), "x": obj.X, "y": obj.Y, "z": obj.Z,
                  "graphic": obj.Graphic, "serial": serial})
    API.SavePersistentVar(FORGE_VAR, ",".join(str(FORGE[k]) for k in
                          ["map", "x", "y", "z", "graphic", "serial"]), API.PersistentVar.Char)
    sysmsg("[Mining] Forge set at {},{}.".format(obj.X, obj.Y), 68)
    try:
        API.MarkTile(obj.X, obj.Y, HUE_DEST)
        API.Pause(1.5)
        API.RemoveMarkedTile(obj.X, obj.Y)
    except Exception:
        pass


def set_satchel():
    CTRL["stash_refused"] = False
    sysmsg("[Mining] Target your ore satchel.", 68)
    serial = API.RequestTarget(20)
    if not serial:
        sysmsg("[Mining] No satchel set.", 33)
        return
    SATCHEL[0] = int(serial)
    API.SavePersistentVar(SATCHEL_VAR, str(SATCHEL[0]), API.PersistentVar.Char)
    sysmsg("[Mining] Satchel set.", 68)


def auto_satchel(open_it=True):
    """Find the Compact Ore Satchel and open it: mined ore goes straight into it on
    this shard, and the client only sees what is inside an opened bag."""
    if not (SATCHEL[0] and API.FindItem(SATCHEL[0]) is not None):
        for it in pack_all():
            if it.Graphic == SATCHEL_GRAPHIC or "ore satchel" in item_name(it):
                SATCHEL[0] = int(it.Serial)
                API.SavePersistentVar(SATCHEL_VAR, str(SATCHEL[0]), API.PersistentVar.Char)
                note("Found your ore satchel.", 68)
                break
    if open_it and SATCHEL[0] and API.FindItem(SATCHEL[0]) is not None:
        API.UseObject(SATCHEL[0])
        API.Pause(0.6)


def forge_ok():
    if not FORGE:
        return False
    return FORGE.get("map") == cur_map() or forge_dist() < 10 ** 8


FORGE_ROUTE = {"key": None, "d": 9999, "via": 0}


def forge_dist():
    """Walking distance to the forge, through teleporters if that is shorter (cached)."""
    if not FORGE:
        return 9999
    key = (cur_map(), API.Player.X // 4, API.Player.Y // 4, FORGE["map"], FORGE["x"], FORGE["y"])
    if FORGE_ROUTE["key"] != key:
        cost, route = plan_links(FORGE["map"], FORGE["x"], FORGE["y"])
        FORGE_ROUTE.update({"key": key, "d": cost, "via": len(route)})
    return FORGE_ROUTE["d"]


def pack_items(graphics):
    out = []
    for it in API.ItemsInContainer(API.Backpack, True) or []:
        if it.Graphic in graphics:
            out.append(it)
    return out


def count(graphics, outside_satchel=False):
    n = 0
    for it in pack_items(graphics):
        if outside_satchel and SATCHEL[0] and it.Container == SATCHEL[0]:
            continue
        n += it.Amount or 1
    return n


def stash(graphics):
    """Move matching items from the pack into the satchel. Returns how many moved."""
    if not SATCHEL[0]:
        return 0
    if API.FindItem(SATCHEL[0]) is None:
        sysmsg("[Mining] Can't see the satchel. Is it in your pack?", 33)
        return 0
    moved = 0
    for it in pack_items(graphics):
        if it.Container == SATCHEL[0] or it.Serial == SATCHEL[0]:
            continue
        API.MoveItem(it.Serial, SATCHEL[0])
        API.Pause(0.8)
        after = API.FindItem(it.Serial)
        if after is not None and after.Container != SATCHEL[0]:
            sysmsg("[Mining] The satchel would not take that item.", 33)
            CTRL["stash_refused"] = True
            break
        moved += 1
    return moved


SMALL_ORE = 0x19B7          # a single one of these can't be smelted (ModernUO Ore.cs)
LEFTOVER_ORE = set()        # serials we could not do anything with this trip


def _ore_piles():
    return [o for o in pack_items(ORE_GRAPHICS) if API.FindItem(o.Serial) is not None]


def _is_single_small(o):
    return o.Graphic == SMALL_ORE and (o.Amount or 1) < 2


def combine_small_ore():
    """Fold single tiny ore bits into another pile of the same metal (same hue).
    ModernUO lets you target another ore pile with ore; the result smelts normally
    and no metal is lost. Returns how many singles were combined."""
    merged = 0
    for _ in range(20):
        piles = _ore_piles()
        singles = [o for o in piles if _is_single_small(o) and o.Serial not in LEFTOVER_ORE]
        if not singles:
            break
        s_ = singles[0]
        partners = [o for o in piles if o.Serial != s_.Serial and (o.Hue or 0) == (s_.Hue or 0)]
        # prefer a real pile over another single bit
        partners.sort(key=lambda o: (_is_single_small(o), -(o.Amount or 1)))
        if not partners:
            LEFTOVER_ORE.add(s_.Serial)
            continue
        API.ClearJournal()
        API.UseObject(s_.Serial)
        if not API.WaitForTarget("any", 3):
            LEFTOVER_ORE.add(s_.Serial)
            continue
        API.Target(partners[0].Serial)
        API.Pause(0.8)
        if API.FindItem(s_.Serial) is None:
            merged += 1
        else:
            API.CancelTarget()
            LEFTOVER_ORE.add(s_.Serial)
            if API.InJournal("too great") or API.InJournal("too much ore"):
                dbg("Could not combine ore: too heavy / too much")
    return merged


def smelt_all():
    """Smelt every ore pile in the pack at the forge, combining tiny single bits first."""
    LEFTOVER_ORE.clear()
    tries = {}
    strange = set()                 # hues this character can't smelt yet
    for _ in range(80):
        checkpoint()
        combine_small_ore()
        piles = [o for o in _ore_piles()
                 if not _is_single_small(o) and tries.get(o.Serial, 0) < 4
                 and (o.Hue or 0) not in strange and o.Serial not in LEFTOVER_ORE]
        if not piles:
            break
        ore = piles[0]
        tries[ore.Serial] = tries.get(ore.Serial, 0) + 1
        API.ClearJournal()
        API.UseObject(ore.Serial)
        if not API.WaitForTarget("any", 3):
            continue
        if FORGE.get("serial"):
            API.Target(FORGE["serial"])
        else:
            API.Target(FORGE["x"], FORGE["y"], FORGE["z"], FORGE["graphic"])
        API.Pause(SMELT_DELAY)
        if API.InJournal("no idea how to smelt"):
            strange.add(ore.Hue or 0)
            note("Mining skill too low to smelt that colored ore; keeping it.", 43)
        # "not enough metal-bearing ore" / "burn away the impurities" leave a smaller
        # pile behind; the next pass combines or smelts it
    left = [o for o in _ore_piles() if _is_single_small(o)]
    if left:
        dbg("{} tiny ore bit(s) with nothing to combine with; kept for next time".format(len(left)))
    if strange and STASH_ORE is False and SATCHEL[0]:
        # colored ore we can't smelt yet: at least get it into the satchel
        stash(ORE_GRAPHICS)


def unload_check(force=False):
    """ok = keep mining, trip = go to the forge, stop = can't unload."""
    if not force and weight_pct() < SMELT_AT:
        return "ok"
    loose = INGOT_GRAPHICS + STASH_EXTRA + (ORE_GRAPHICS if STASH_ORE else [])
    if SATCHEL[0] and not CTRL["stash_refused"] and count(loose, outside_satchel=True):
        stash(loose)
        if weight_pct() < SMELT_AT:
            return "ok"
    if opt("smelt") and forge_ok() and forge_dist() <= FORGE_MAX_DIST:
        return "trip"
    if weight_pct() >= WEIGHT_STOP or force:
        if opt("smelt") and not forge_ok():
            sysmsg("[Mining] Heavy, and no forge set on this map. Press Set Forge.", 33)
        elif opt("smelt"):
            sysmsg("[Mining] Forge is too far away ({} tiles).".format(forge_dist()), 33)
        else:
            sysmsg("[Mining] Weight limit reached. Go unload.", 33)
        return "stop"
    return "ok"


def forge_trip():
    """Walk to the forge, smelt, stash bars, walk back. True if we made it back."""
    if CTRL["busy"]:
        return False
    CTRL["busy"] = True
    try:
        if not forge_ok():
            sysmsg("[Mining] No forge set on this map.", 33)
            return False
        home = (API.Player.X, API.Player.Y)
        home_map = cur_map()
        heading = list(HEADING)
        set_state("Walking to forge")
        API.HeadMsg("Off to the forge ({} tiles)".format(forge_dist()), API.Player, 88)
        if (cur_map() != FORGE["map"] or forge_dist() > FORGE_STAND) and \
                not go_to(FORGE["x"], FORGE["y"], FORGE_STAND, "To forge", FORGE["map"]):
            sysmsg("[Mining] Could not reach the forge ({} tiles away). Walk there once "
                       "with the window open so it learns the way.".format(forge_dist()), 33)
            HEADING[:] = heading
            return False
        set_state("Smelting")
        auto_satchel()              # see the ore that went into the satchel
        smelt_all()
        set_state("Stashing bars")
        stash(INGOT_GRAPHICS + STASH_EXTRA)
        if cur_map() != home_map or max(abs(API.Player.X - home[0]), abs(API.Player.Y - home[1])) > 0:
            set_state("Walking back")
            if not go_to(home[0], home[1], 1, "Back to mine", home_map):
                sysmsg("[Mining] Could not get back to the mining spot.", 33)
                HEADING[:] = heading
                return False
        HEADING[:] = heading
        sysmsg("[Mining] Unloaded. Weight {}%.".format(int(weight_pct())), 68)
        return True
    finally:
        CTRL["busy"] = False
        CTRL["legit_jumps"] = POS["jumps"]      # teleports on a forge trip are expected


# ---- control window ------------------------------------------------------------
UI = {}


def opt(name):
    cb = UI.get("cb_" + name)
    if cb is None:
        return {"wander": WANDER, "smelt": AUTO_SMELT, "marks": SHOW_MARKS, "seek": SEEK_VEINS,
                "oremap": SHOW_ORE_MAP}[name]
    try:
        return bool(cb.IsChecked)
    except Exception:
        return True


def want(action):
    CTRL["want"].append(action)


def handle_wants():
    """Button actions that need targeting or walking run here, at a safe point."""
    while CTRL["want"] and not CTRL["busy"]:
        a = CTRL["want"].pop(0)
        if a == "forge":
            set_forge()
        elif a == "satchel":
            set_satchel()
        elif a == "smelt":
            forge_trip() if forge_ok() else sysmsg("[Mining] Set a forge first.", 33)
        elif a == "tools":
            ensure_tools(force=True)
        elif a in ("survey", "vein"):
            CTRL["busy"] = True
            try:
                if a == "survey":
                    read_logbook()
                    survey()
                else:
                    go_to_vein()
            finally:
                CTRL["busy"] = False
        elif a == "stash":
            CTRL["stash_refused"] = False
            n = stash(INGOT_GRAPHICS + STASH_EXTRA + ORE_GRAPHICS)
            sysmsg("[Mining] Stashed {} stacks.".format(n), 68)
        gui_tick(force=True)


def on_start():
    if not CTRL["running"]:
        CTRL["running"] = True
        CTRL["paused"] = False
    else:
        CTRL["paused"] = not CTRL["paused"]
    gui_tick(force=True)


def on_stop_btn():
    CTRL["running"] = False
    CTRL["paused"] = False


def on_close():
    CTRL["quit"] = True
    CTRL["running"] = False


def build_gui():
    W, H = 250, 402
    g = API.CreateGump(True, True, False)
    g.SetRect(120, 120, W, H)
    g.Add(API.CreateGumpColorBox(0.85, "#161616").SetRect(0, 0, W, H))
    g.Add(API.CreateGumpTTFLabel("SL Mining", 16, "#E8C15A").SetPos(10, 6))

    UI["state"] = API.CreateGumpTTFLabel("Idle", 13, "#FFFFFF")
    UI["load"] = API.CreateGumpTTFLabel("", 12, "#BBBBBB")
    UI["forge"] = API.CreateGumpTTFLabel("", 12, "#BBBBBB")
    UI["sat"] = API.CreateGumpTTFLabel("", 12, "#BBBBBB")
    g.Add(UI["state"].SetPos(10, 30))
    g.Add(UI["load"].SetPos(10, 50))
    g.Add(UI["forge"].SetPos(10, 68))
    g.Add(UI["sat"].SetPos(10, 86))
    UI["tools"] = API.CreateGumpTTFLabel("", 12, "#BBBBBB")
    UI["note"] = API.CreateGumpTTFLabel("", 12, "#E8C15A")
    UI["survey"] = API.CreateGumpTTFLabel("", 12, "#9FD3FF")
    g.Add(UI["tools"].SetPos(10, 104))
    g.Add(UI["survey"].SetPos(10, 122))
    g.Add(UI["note"].SetPos(10, 140))

    def button(text, x, y, fn, w=72):
        b = API.CreateSimpleButton(text, w, 22)
        g.Add(b.SetPos(x, y))
        API.AddControlOnClick(b, fn)
        return b

    UI["start"] = button("Start", 10, 166, on_start)
    button("Stop", 88, 166, on_stop_btn)
    button("Smelt now", 166, 166, lambda: want("smelt"))
    button("Set Forge", 10, 194, lambda: want("forge"), 110)
    button("Set Satchel", 128, 194, lambda: want("satchel"), 110)
    button("Stash now", 10, 222, lambda: want("stash"), 110)
    button("Make tools", 128, 222, lambda: want("tools"), 110)
    button("Survey", 10, 250, lambda: want("survey"), 110)
    button("Go to vein", 128, 250, lambda: want("vein"), 110)

    y = 282
    for key, label, val in [("wander", "Wander to new veins", WANDER),
                            ("seek", "Seek colored veins", SEEK_VEINS),
                            ("smelt", "Go smelt when heavy", AUTO_SMELT),
                            ("marks", "Highlight tiles", SHOW_MARKS),
                            ("oremap", "Show ore map (tint blocks)", SHOW_ORE_MAP)]:
        cb = API.CreateGumpCheckbox(label, 1153, val)
        g.Add(cb.SetPos(10, y))
        UI["cb_" + key] = cb
        y += 20

    API.AddControlOnDisposed(g, on_close)
    API.AddGump(g)
    UI["gump"] = g
    gui_tick(force=True)


def _set(key, text):
    try:
        UI[key].SetText(text)
    except Exception:
        pass


def gui_tick(force=False):
    if not UI:
        return
    now = time.time()
    if not force and now - CTRL["last_ui"] < 1.0:
        return
    CTRL["last_ui"] = now
    st = CTRL["state"]
    if CTRL["running"] and CTRL["paused"]:
        st = "Paused (" + st + ")"
    elif not CTRL["running"]:
        st = "Idle"
    _set("state", st)
    _set("load", "Weight {}%   Ore {}   Bars {}".format(
        int(weight_pct()), count(ORE_GRAPHICS), count(INGOT_GRAPHICS)))
    if forge_ok():
        d = forge_dist()
        _set("forge", "Forge: {},{}  ({} tiles{})".format(
            FORGE["x"], FORGE["y"], d if d < 10 ** 8 else "?",
            ", via teleporter" if FORGE_ROUTE["via"] else ""))
    elif FORGE:
        _set("forge", "Forge: set on another map")
    else:
        _set("forge", "Forge: not set")
    if SATCHEL[0] and API.FindItem(SATCHEL[0]) is not None:
        _set("sat", "Satchel: set   ({} bars inside)".format(
            count(INGOT_GRAPHICS) - count(INGOT_GRAPHICS, outside_satchel=True)))
    else:
        _set("sat", "Satchel: not set" if not SATCHEL[0] else "Satchel: not found")
    _set("tools", tools_summary())
    _set("survey", survey_line())
    _set("note", NOTES[-1] if NOTES else "")
    try:
        UI["start"].SetText("Pause" if CTRL["running"] and not CTRL["paused"]
                            else ("Resume" if CTRL["running"] else "Start"))
    except Exception:
        pass


# ---- the mining session ----------------------------------------------------------
def run_session():
    tool = find_tool()
    if tool is None and ensure_tools(force=True):
        tool = find_tool()
    if tool is None:
        sysmsg("[Mining] No pickaxe or shovel found. Run with DIAGNOSE = True.", 33)
        return
    START[:] = [API.Player.X, API.Player.Y, API.Player.Z, cur_map()]
    start = START
    load_bank_memory()
    auto_satchel()
    if opt("seek"):
        set_state("Reading logbook")
        read_logbook(quiet=True)
        tot = SURVEY["book_total"]
        if SURVEY["book"] is not None:
            sysmsg("[Survey] Logbook: {} veins here; vein map matches {}/{}.".format(
                tot, SURVEY["book_hits"], tot), 68 if SURVEY["map_ok"] else 33)
    sysmsg("[Mining] Using " + (item_name(tool) or "tool") +
               ". Radius " + str(RADIUS) + ", smelt at " + str(SMELT_AT) + "%" +
               (", wandering up to " + str(WANDER_RANGE) + " tiles." if opt("wander") else "."), 68)

    moves = 0
    CTRL["relocs"] = 0
    while True:
        checkpoint()
        j0 = POS["jumps"]
        r = mine_here()
        stray = POS["jumps"] not in (j0, CTRL.get("legit_jumps")) or cur_map() != START[3]
        if r == "moved" or stray:
            if stray and not CTRL.get("moved"):
                # wandered onto a teleporter: go back the way we came
                note("Teleported away by accident; heading back.", 43)
                if not go_to(START[0], START[1], 2, "Back to the mine", START[3]):
                    note("Couldn't get back. Mining here instead.", 43)
                    START[:] = [API.Player.X, API.Player.Y, API.Player.Z, cur_map()]
            moves = 0
            continue
        if r == "trip":
            if not forge_trip():
                return
            if weight_pct() >= SMELT_AT:
                note("Still {}% heavy after unloading. Bank or drop something.".format(int(weight_pct())), 33)
                return
            continue                # back at the spot: finish its tiles
        if r == "stop":
            return

        if opt("wander") and (MAX_MOVES == 0 or moves < MAX_MOVES):
            set_state("Looking for ore")
            if move_on(start):
                moves += 1
                continue
            dbg("Nowhere left to go within " + str(WANDER_RANGE) + " tiles")

        if not LOOP_FOREVER:
            sysmsg("[Mining] Nothing left in range. Move to a new area.", 43)
            API.HeadMsg("ALL DRY - MOVE", API.Player, 33)
            return

        if opt("wander") and RETURN_TO_START:
            go_to(start[0], start[1], 1, "Back to start", start[3])
        set_state("Waiting for respawn")
        waited = 0
        while waited < RESPAWN_WAIT:
            checkpoint()
            API.Pause(1)
            waited += 1
        reset_memory()
        moves = 0


def run_logged():
    """run_session, but a script error is written to the log instead of vanishing."""
    try:
        run_session()
    except Halt:
        log("stopped")
        raise
    except Exception as e:
        try:
            import traceback
            log("SCRIPT ERROR:\n" + traceback.format_exc())
        except Exception:
            log("SCRIPT ERROR: {!r}".format(e))
        sysmsg("[Mining] Script error: {} (details in SL_Mining.log)".format(e), 33)


def main():
    log("===== SL_Mining started =====")
    if DIAGNOSE:
        diagnose()
        return

    clear_leftover_marks()
    load_targets()
    load_crumbs()
    load_links()
    SURVEY["map"] = cur_map()
    load_vein_log()             # veins logged before + blocks you've dug
    track_position()

    if not GUI:
        CTRL["running"] = True
        CTRL["session"] = True
        try:
            run_logged()
        except Halt:
            pass
        API.Pause(END_LINGER)
        return

    build_gui()
    sysmsg("[Mining] Window open. Set your forge and satchel, then press Start.", 68)
    while not CTRL["quit"] and not API.StopRequested:
        API.ProcessCallbacks()
        track_position()        # learn the way while you walk around
        ore_map_tick()
        try:
            handle_wants()      # Set Forge / Smelt now while idle
        except Halt:
            pass
        gui_tick()
        if CTRL["running"]:
            CTRL["session"] = True
            try:
                run_logged()
            except Halt:
                pass
            CTRL["session"] = False
            CTRL["running"] = False
            CTRL["paused"] = False
            set_state("Idle")
            try:
                API.TrackingArrow(-1, -1)
            except Exception:
                pass
        API.Pause(0.2)
    save_crumbs()
    try:
        if not UI["gump"].IsDisposed:
            UI["gump"].Dispose()
    except Exception:
        pass


main()
clear_marks()               # every normal exit
