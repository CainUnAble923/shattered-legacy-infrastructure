# =============================================================================
# -*- coding: utf-8 -*-
# ClusterF Lumberjacking Radar for Razor Enhanced  v1.0
#
# USAGE
#   Razor Enhanced -> Scripts tab -> Add -> select this file -> Play.
#   Forest-green cells = tree present, wood type unknown. Click to chop.
#   AUTO loops a clicked tile until depleted. NEXT chops nearest in-range tree.
#   WALK pathfinds toward the nearest visible tree without chopping.
#   Auto walk checkbox: walks to out-of-range tiles before chopping.
#
# CACHE
#   C:\UO\Scripts\clusterf_lj_cache.json
# =============================================================================

import clr
import json
import os
import re
import struct
import threading
import time

clr.AddReference('System.Windows.Forms')
clr.AddReference('System.Drawing')

from System import Action
from System.Windows.Forms import (
    Application, Form, Button, Label, CheckBox, TableLayoutPanel,
    SizeType, RowStyle, ColumnStyle, TableLayoutPanelCellBorderStyle,
    FlowLayoutPanel, FlowDirection, FormBorderStyle, FlatStyle,
    Padding, ToolTip, Timer, FormStartPosition, Cursors,
)
from System.Drawing import Color, Font, FontStyle, Point, Size, SolidBrush, Pen

# =============================================================================
# Configuration
# =============================================================================

SKILL_NAME          = 'Lumberjacking'
GRID_RADIUS         = 6
GRID_SIZE           = GRID_RADIUS * 2 + 1   # 13
CELL_PX             = 36
REFRESH_MS          = 500
CHOP_WAIT           = 2800                   # ms between swings
MAX_AUTO_SWINGS     = 35
MAX_TARGET_RANGE    = 2
WALK_TIMEOUT        = 8.0
STOP_WEIGHT_MARGIN  = 20
CACHE_SAVE_SECONDS  = 30
CACHE_VERSION       = 1
RESOURCE_CHUNK_SIZE = 8
SCAN_CACHE_MAX      = 2000

CACHE_PATH = os.path.join(os.path.dirname(__file__), 'clusterf_lj_cache.json')

# =============================================================================
# Wood type definitions  (7 standard UO types; extend WOOD_LABELS/WOOD_DISPLAY
# and CELL_BG if ClusterF adds custom wood types in future)
# =============================================================================

WOOD_LABELS = {
    'Ordinary':  'Lo',
    'Oak':       'Ok',
    'Ash':       'Ah',
    'Yew':       'Yw',
    'Heartwood': 'Hw',
    'Frostwood': 'Fw',
    'Bloodwood': 'Bw',
}

WOOD_DISPLAY = {
    'Ordinary':  '',           # "you chop some logs" has no type word
    'Oak':       'oak',
    'Ash':       'ash',
    'Yew':       'yew',
    'Heartwood': 'heartwood',
    'Frostwood': 'frostwood',
    'Bloodwood': 'bloodwood',
}

CELL_BG = {
    'surface':   Color.FromArgb(25,  40,  18),
    'tree':      Color.FromArgb(50,  72,  35),
    'Ordinary':  Color.FromArgb(110, 78,  48),
    'Oak':       Color.FromArgb(88,  60,  32),
    'Ash':       Color.FromArgb(108, 118, 130),
    'Yew':       Color.FromArgb(42,  95,  42),
    'Heartwood': Color.FromArgb(155, 45,  45),
    'Frostwood': Color.FromArgb(150, 205, 235),
    'Bloodwood': Color.FromArgb(168, 22,  22),
    'Depleted':  Color.FromArgb(18,  18,  18),
    'player':    Color.FromArgb(255, 255, 255),
}

_NEEDS_WHITE = frozenset([
    'surface', 'tree', 'Oak', 'Yew', 'Heartwood', 'Bloodwood', 'Depleted',
])

# Fonts used in the radar grid
_FONT_CELL  = Font('Arial Narrow',   7,  FontStyle.Bold)    # labels, @, X
_FONT_EMOJI = Font('Segoe UI Emoji', 16, FontStyle.Regular) # tree emoji

# Tree emoji shown on uncharted tree cells
_TREE_EMOJI = u'\U0001F332'   # 🌲  Evergreen Tree

# =============================================================================
# Chopable static tile IDs  (CyberPope's %DefaultChopable list)
# =============================================================================

CHOPABLE_TILES = set([
    3220, 3221, 3222, 3230,
    3274, 3275, 3276, 3277, 3278, 3279, 3280, 3281, 3282, 3283, 3284, 3285,
    3286, 3287, 3288, 3289, 3290, 3291, 3292, 3293, 3294, 3295, 3296, 3297,
    3298, 3299, 3300, 3301, 3302, 3304,
    3320, 3321, 3322, 3323, 3324, 3325, 3326, 3327, 3328, 3329, 3330, 3331,
    3393, 3394, 3395, 3396, 3397, 3398, 3399, 3400, 3401, 3402, 3403, 3404,
    3405, 3406, 3407, 3408, 3409, 3410, 3411,
    3415, 3416, 3417, 3418, 3419, 3420, 3421, 3422, 3423, 3424, 3425, 3426,
    3427, 3428, 3429, 3430, 3431, 3432, 3433,
    3438, 3439, 3440, 3441, 3442, 3443, 3444, 3445, 3446, 3447, 3448, 3449,
    3450, 3451, 3452, 3453, 3454, 3455,
    3460, 3461, 3462, 3463, 3464, 3465, 3466, 3467, 3468, 3469, 3470, 3471,
    3472,
    3477, 3478, 3479, 3480, 3481, 3482, 3483, 3484, 3485, 3486, 3487, 3488,
    3489, 3490, 3491, 3492, 3493, 3494, 3495, 3496, 3497, 3498, 3499,
    4789, 4790, 4791, 4792, 4793, 4794, 4795, 4796, 4797, 4798, 4799,
    4801, 4802, 4803, 4804, 4805, 4806, 4807,
    8778, 8779, 8780, 8781,
])

_scan_cache = {}

# =============================================================================
# Journal patterns
# =============================================================================

# ClusterF discovery messages (coord-embedded), mirroring mining system
_PAT_DISC = re.compile(r'Discovery recorded:\s+(.+?)\s+(?:wood|logs?)\b.*?\((\d+),\s*(\d+)\)', re.I)
_PAT_VEIN = re.compile(r'New\s+(.+?)\s+(?:tree|grove|stand) logged.*?\((\d+),\s*(\d+)\)', re.I)

# Standard chop message: "You chop some oak logs." or "You chop some logs."
_PAT_CHOP = re.compile(r'[Yy]ou chop some (.+?) logs?\.?', re.I)
_PAT_CHOP_PLAIN = re.compile(r'[Yy]ou chop some logs?\.?', re.I)

_DEPL_PATS = [
    re.compile(r"[Tt]here'?s? not enough wood here",     re.I),
    re.compile(r'[Nn]o wood here',                        re.I),
    re.compile(r'[Yy]ou find no wood',                    re.I),
    re.compile(r'[Tt]here is no wood here',               re.I),
]

# Map display name → wood key
_DISPLAY_TO_KEY = {v: k for k, v in WOOD_DISPLAY.items() if v}
for _k in list(WOOD_LABELS.keys()):
    _DISPLAY_TO_KEY[_k.lower()] = _k


def _resolve_wood(phrase):
    p = phrase.strip().lower()
    if not p or p == 'logs' or p == 'some':
        return 'Ordinary'
    return _DISPLAY_TO_KEY.get(p)

# =============================================================================
# Session stats
# =============================================================================

_session_stats = {'wood_found': 0}

# =============================================================================
# Persistent tile cache
# =============================================================================

_cache           = {}
_depleted_chunks = set()
_cache_lock      = threading.Lock()
_dirty           = False


def _ckey(x, y, m):
    return '{},{},{}'.format(x, y, m)


def _chunk_origin(x, y):
    return (int(x) // RESOURCE_CHUNK_SIZE) * RESOURCE_CHUNK_SIZE, \
           (int(y) // RESOURCE_CHUNK_SIZE) * RESOURCE_CHUNK_SIZE


def _chunk_key(x, y, m):
    cx, cy = _chunk_origin(x, y)
    return _ckey(cx, cy, m)


def _chunk_label(x, y):
    cx, cy = _chunk_origin(x, y)
    return '{}-{}, {}-{}'.format(cx, cx + RESOURCE_CHUNK_SIZE - 1,
                                  cy, cy + RESOURCE_CHUNK_SIZE - 1)


def _chunk_key_from_tile_key(key):
    try:
        parts = str(key).split(',', 2)
        if len(parts) != 3:
            return None
        return _chunk_key(int(parts[0]), int(parts[1]), parts[2])
    except:
        return None


def cache_load():
    global _cache, _depleted_chunks
    if not os.path.isfile(CACHE_PATH):
        return
    try:
        with open(CACHE_PATH, 'r') as f:
            data = json.load(f)
        if isinstance(data, dict) and 'tiles' in data:
            tiles  = data.get('tiles', {})
            chunks = data.get('depleted_chunks', [])
        else:
            tiles, chunks = data, []
        if not isinstance(tiles, dict):
            tiles = {}
        chunk_set = set(str(c) for c in chunks) if isinstance(chunks, list) else set()
        for key, value in tiles.items():
            if value == 'Depleted':
                ck = _chunk_key_from_tile_key(key)
                if ck:
                    chunk_set.add(ck)
        with _cache_lock:
            _cache           = tiles
            _depleted_chunks = chunk_set
        Misc.SendMessage('[LJRadar] {} tiles, {} depleted chunks loaded.'.format(
            len(_cache), len(_depleted_chunks)), 0x44)
    except Exception as ex:
        Misc.SendMessage('[LJRadar] Cache read error: {}'.format(ex), 0x22)


def cache_save():
    global _dirty
    if not _dirty:
        return
    try:
        with _cache_lock:
            snap   = dict(_cache)
            chunks = sorted(_depleted_chunks)
        tmp = CACHE_PATH + '.tmp'
        with open(tmp, 'w') as f:
            json.dump({'version': CACHE_VERSION, 'chunk_size': RESOURCE_CHUNK_SIZE,
                       'depleted_chunks': chunks, 'tiles': snap},
                      f, indent=2, sort_keys=True)
        if os.path.isfile(CACHE_PATH):
            os.remove(CACHE_PATH)
        os.rename(tmp, CACHE_PATH)
        _dirty = False
    except Exception as ex:
        Misc.SendMessage('[LJRadar] Cache write error: {}'.format(ex), 0x22)


def cache_mark_depleted_chunk(x, y, map_name):
    global _dirty
    changed   = False
    tile_key  = _ckey(x, y, map_name)
    chunk_key = _chunk_key(x, y, map_name)
    with _cache_lock:
        if chunk_key not in _depleted_chunks:
            _depleted_chunks.add(chunk_key)
            changed = True
        if _cache.get(tile_key) != 'Depleted':
            _cache[tile_key] = 'Depleted'
            changed = True
        if changed:
            _dirty = True
    if changed:
        Misc.SendMessage('[LJRadar] Cleared chunk {}.'.format(_chunk_label(x, y)), 0x44)
    return changed


def cache_set(x, y, map_name, wood_key):
    # Trees are independent — each tile has its own supply.  Never mark a whole
    # chunk as depleted; just record this specific tile.
    global _dirty
    k = _ckey(x, y, map_name)
    with _cache_lock:
        existing = _cache.get(k)
        if existing == wood_key:
            return False
        # Don't overwrite a confirmed colored wood with Ordinary
        if (existing and existing != 'Depleted'
                and wood_key == 'Ordinary' and existing != 'Ordinary'):
            return False
        _cache[k] = wood_key
        _dirty = True
    if wood_key == 'Depleted':
        Misc.SendMessage('[LJRadar] Tree at ({}, {}) depleted.'.format(x, y), 0x44)
    return True


def cache_get(x, y, map_name):
    # No chunk-level depletion for LJ — check per-tile only.
    with _cache_lock:
        return _cache.get(_ckey(x, y, map_name))


def cache_counts(map_name=None):
    suffix = None if map_name is None else ',' + str(map_name)
    with _cache_lock:
        values = [v for k, v in _cache.items()
                  if suffix is None or k.endswith(suffix)]
        chunk_count = len(_depleted_chunks) if suffix is None else sum(
            1 for k in _depleted_chunks if k.endswith(suffix))
    known    = sum(1 for v in values if v in WOOD_LABELS)
    depleted = sum(1 for v in values if v == 'Depleted')
    return known, depleted, len(values), chunk_count

# =============================================================================
# Map / player helpers
# =============================================================================

_MAP_INDEX = {
    'felucca': 0, 'trammel': 1, 'ilshenar': 2,
    'malas': 3,   'tokuno': 4,  'termur': 5,
}


def _map_idx(map_name):
    # Player.Map often returns an int directly (0=Fel, 1=Tram, etc.)
    try:
        v = int(map_name)
        if 0 <= v <= 5:
            return v
    except (ValueError, TypeError):
        pass
    return _MAP_INDEX.get(str(map_name).lower(), 1)


def _player_pos():
    try:
        return Player.X, Player.Y, Player.Z, Player.Map
    except AttributeError:
        pass
    try:
        p = Player.Position
        return p.X, p.Y, p.Z, Player.Map
    except Exception:
        return None

# =============================================================================
# Skill helpers
# =============================================================================


def _skill_real():
    try:
        return float(Player.GetRealSkillValue(SKILL_NAME))
    except:
        return 0.0


def _skill_shown():
    try:
        return float(Player.GetSkillValue(SKILL_NAME))
    except:
        return 0.0


def _skill_cap():
    try:
        return float(Player.GetSkillCap(SKILL_NAME))
    except:
        return 100.0


def _followers_text():
    try:
        return '{}/{}'.format(Player.Followers, Player.FollowersMax)
    except:
        return '?/?'

# =============================================================================
# Pathfinding
# =============================================================================


def _path_to(tx, ty, tz, run=True, timeout_ms=8000):
    try:
        route              = PathFinding.Route()
        route.X            = int(tx)
        route.Y            = int(ty)
        route.Run          = bool(run)
        route.StopIfStuck  = True
        route.IgnoreMobile = True
        route.MaxRetry     = 1
        route.Timeout      = int(timeout_ms)
        result = PathFinding.Go(route)
        return result is not False
    except:
        pass
    try:
        result = Player.PathFindTo(int(tx), int(ty), 1, bool(run), True, True, False, False)
        return result is not False
    except:
        pass
    try:
        result = Player.PathFindTo(int(tx), int(ty), int(tz))
        return result is not False
    except:
        return False


def _wait_in_range(tx, ty, required_range, timeout=WALK_TIMEOUT):
    deadline = time.time() + timeout
    while time.time() < deadline:
        p = _player_pos()
        if p and max(abs(p[0] - tx), abs(p[1] - ty)) <= required_range:
            return True
        time.sleep(0.2)
    p = _player_pos()
    return bool(p and max(abs(p[0] - tx), abs(p[1] - ty)) <= required_range)

# =============================================================================
# Tile classification  (trees are statics, not land tiles)
# =============================================================================


def _as_int(v):
    try:
        return int(v)
    except:
        return None


_API_GETTERS = [lambda: Statics, lambda: Tiles]  # noqa — resolved at call time, not definition


def _call_map_api(method_name, x, y, map_name):
    midx = _map_idx(map_name)
    for get_api in _API_GETTERS:
        try:
            api = get_api()
        except NameError:
            continue
        method = getattr(api, method_name, None)
        if method is None:
            continue
        for map_arg in (midx, map_name):
            try:
                result = method(int(x), int(y), map_arg)
                if result is not None:
                    return result
            except:
                continue
    return None


def _tile_id(info):
    for attr in ('StaticID', 'ItemID', 'ID', 'TileID'):
        try:
            v = _as_int(getattr(info, attr))
            if v is not None:
                return v
        except:
            pass
    return None


def _tile_z(info):
    for attr in ('StaticZ', 'Z'):
        try:
            v = _as_int(getattr(info, attr))
            if v is not None:
                return v
        except:
            pass
    return None


def _get_land_z(x, y, map_name):
    v = _as_int(_call_map_api('GetLandZ', x, y, map_name))
    if v is not None:
        return v
    info = _call_map_api('GetStaticsLandInfo', x, y, map_name)
    return _tile_z(info) if info is not None else None


def _get_static_infos(x, y, map_name):
    v = _call_map_api('GetStaticsTileInfo', x, y, map_name)
    if v is None:
        return None
    try:
        return list(v)
    except:
        return []


def _scan_tile(x, y, map_name):
    """Returns ('tree'|'surface', source, tile_id, z)."""
    k      = _ckey(x, y, map_name)
    cached = _scan_cache.get(k)
    if cached is not None:
        return cached

    land_z = _get_land_z(x, y, map_name)

    # Trees are static objects — check statics first
    infos = _get_static_infos(x, y, map_name)
    if infos is not None:
        for info in infos:
            sid = _tile_id(info)
            if sid in CHOPABLE_TILES:
                z = _tile_z(info)
                result = ('tree', 'static', sid, z if z is not None else land_z)
                _scan_cache[k] = result
                return result

    result = ('surface', 'land', None, land_z)
    _scan_cache[k] = result
    return result


def cell_state(x, y, map_name):
    cached = cache_get(x, y, map_name)
    if cached:
        return cached
    return _scan_tile(x, y, map_name)[0]


def _target_z(x, y, map_name, fallback_z):
    z = _scan_tile(x, y, map_name)[3]
    if z is not None:
        return z
    z = _get_land_z(x, y, map_name)
    return z if z is not None else fallback_z

# =============================================================================
# Tool finding / chopping
# =============================================================================

_TOOL_IDS = {
    0x0F43, 0x0F44,   # Hatchet
    0x0F45, 0x0F46,   # Double Axe
    0x0F47, 0x0F48,   # Battle Axe
    0x0F49, 0x0F4A,   # Axe
    0x0F4B, 0x0F4C,   # Two-Handed Axe
    0x1443, 0x1444,   # War Axe
    0x0E81, 0x0E82,   # War Axe alt
    0x1BB0,           # Executioner's Axe
}


def _iter_container_items(container, depth=0, seen=None):
    if container is None or depth > 4:
        return
    if seen is None:
        seen = set()
    serial = getattr(container, 'Serial', None)
    if serial in seen:
        return
    if serial is not None:
        seen.add(serial)
        try:
            Items.WaitForContents(serial, 250)
        except:
            pass
    try:
        contents = list(container.Contains)
    except:
        contents = []
    for item in contents:
        yield item
        try:
            if getattr(item, 'Contains', None):
                for child in _iter_container_items(item, depth + 1, seen):
                    yield child
        except:
            pass


def find_tool():
    for layer in ('LeftHand', 'RightHand'):
        try:
            tool = Player.GetItemOnLayer(layer)
            if tool and tool.ItemID in _TOOL_IDS:
                return tool.Serial
        except:
            pass
    pack = Player.Backpack
    if not pack:
        return None
    try:
        for item in _iter_container_items(pack):
            try:
                name = (item.Name or '').lower()
                if 'axe' in name or 'hatchet' in name:
                    return item.Serial
            except:
                pass
    except:
        pass
    for tid in _TOOL_IDS:
        try:
            try:
                item = Items.FindByID(tid, -1, pack.Serial, True)
            except:
                item = Items.FindByID(tid, -1, pack.Serial)
            if item:
                return item.Serial
        except:
            pass
    return None


def _activate(serial):
    try:
        Items.UseItem(serial)
        return True
    except:
        pass
    try:
        Misc.UseObject(serial)
        return True
    except:
        pass
    try:
        Player.DoubleClick(serial)
        return True
    except:
        pass
    try:
        from System import Array, Byte
        raw = struct.pack('>BI', 0x06, serial & 0x7FFFFFFF)
        Misc.SendPacket(Array[Byte]([Byte(b) for b in bytearray(raw)]), True)
        return True
    except:
        pass
    return False


def do_chop(tx, ty, tz, tile_id=None):
    serial = find_tool()
    if not serial:
        Misc.SendMessage('[LJRadar] No axe or hatchet found.', 0x22)
        return False
    if not _activate(serial):
        Misc.SendMessage('[LJRadar] Cannot activate tool.', 0x22)
        return False
    if not Target.WaitForTarget(3000, False):
        Misc.SendMessage('[LJRadar] No target cursor.', 0x22)
        return False
    # Trees are statics — server needs the graphic ID in the target packet.
    # Try 4-param form first; fall back to 3-param if the overload doesn't exist.
    if tile_id is not None:
        try:
            Target.TargetExecute(int(tx), int(ty), int(tz), int(tile_id))
            return True
        except:
            pass
    Target.TargetExecute(int(tx), int(ty), int(tz))
    return True

# =============================================================================
# Journal processing
# =============================================================================

_grid_dirty   = threading.Event()
_pending      = None
_pending_lock = threading.Lock()


def set_pending(x, y, map_name):
    global _pending
    with _pending_lock:
        _pending = (x, y, map_name)


def get_pending():
    with _pending_lock:
        return _pending


def clear_pending(expected=None):
    global _pending
    with _pending_lock:
        if expected is None or _pending == expected:
            _pending = None


def process_journal():
    try:
        entries = Journal.GetJournalEntry(30)
    except:
        return
    for entry in entries:
        text = getattr(entry, 'Text', None) or str(entry)

        # ClusterF coord-embedded discovery messages
        m = _PAT_DISC.search(text) or _PAT_VEIN.search(text)
        if m:
            wood_key = _resolve_wood(m.group(1))
            if wood_key:
                if cache_set(int(m.group(2)), int(m.group(3)), Player.Map, wood_key):
                    _session_stats['wood_found'] += 1
                    _grid_dirty.set()
            continue

        pending = get_pending()

        # Standard chop: "You chop some oak logs."
        m = _PAT_CHOP.search(text)
        if m and pending:
            wood_key = _resolve_wood(m.group(1))
            if wood_key:
                if cache_set(pending[0], pending[1], pending[2], wood_key):
                    _session_stats['wood_found'] += 1
                    _grid_dirty.set()
                clear_pending(pending)
            continue

        # Plain chop with no type word: "You chop some logs." → Ordinary
        if _PAT_CHOP_PLAIN.search(text) and pending:
            if cache_set(pending[0], pending[1], pending[2], 'Ordinary'):
                _session_stats['wood_found'] += 1
                _grid_dirty.set()
            clear_pending(pending)
            continue

        if any(p.search(text) for p in _DEPL_PATS) and pending:
            if cache_set(pending[0], pending[1], pending[2], 'Depleted'):
                _grid_dirty.set()
            clear_pending(pending)

# =============================================================================
# RadarCell — button with haze overlay and chunk border drawing
# =============================================================================

_HAZE_DEPLETED = Color.FromArgb(110, 210,  20,  20)
_HAZE_CHOPPING = Color.FromArgb( 80, 255, 200,   0)
_CHUNK_BORDER  = Color.FromArgb(210,  95, 205, 255)


class RadarCell(Button):
    def __init__(self):
        super(RadarCell, self).__init__()
        self._haze        = None
        self._chunk_edges = (False, False, False, False)

    def SetChunkEdges(self, left, top, right, bottom):
        edges = (left, top, right, bottom)
        if self._chunk_edges != edges:
            self._chunk_edges = edges
            self.Invalidate()

    def SetHaze(self, color):
        self._haze = color
        self.Invalidate()

    def ClearHaze(self):
        if self._haze is not None:
            self._haze = None
            self.Invalidate()

    def OnPaint(self, e):
        Button.OnPaint(self, e)
        if self._haze is not None:
            b = SolidBrush(self._haze)
            e.Graphics.FillRectangle(b, 0, 0, self.Width, self.Height)
            b.Dispose()
        if any(self._chunk_edges):
            left, top, right, bottom = self._chunk_edges
            p = Pen(_CHUNK_BORDER, 2)
            if left:
                e.Graphics.DrawLine(p, 0, 0, 0, self.Height - 1)
            if top:
                e.Graphics.DrawLine(p, 0, 0, self.Width - 1, 0)
            if right:
                e.Graphics.DrawLine(p, self.Width - 1, 0, self.Width - 1, self.Height - 1)
            if bottom:
                e.Graphics.DrawLine(p, 0, self.Height - 1, self.Width - 1, self.Height - 1)
            p.Dispose()

# =============================================================================
# Radar form
# =============================================================================

_GRID_TOP = 40


class RadarForm(Form):

    def __init__(self):
        super(RadarForm, self).__init__()
        self._cells       = {}
        self._tips        = ToolTip()
        self._last_pos    = (None, None, None)
        self._last_map    = None
        self._last_save   = time.time()
        self._auto        = False
        self._chopping    = False
        self._last_status = ''
        self._start_skill = _skill_real()
        self._last_skill  = self._start_skill
        self._attempts    = 0
        self._gains       = 0
        self._build_ui()
        self._tmr          = Timer()
        self._tmr.Interval = REFRESH_MS
        self._tmr.Tick    += self._on_tick
        self._tmr.Start()

    # ------------------------------------------------------------------
    # UI construction
    # ------------------------------------------------------------------

    def _build_ui(self):
        grid_px  = GRID_SIZE * CELL_PX
        btn_y    = _GRID_TOP + grid_px + 4
        legend_y = btn_y + 24
        status_y = legend_y + 24

        self.Text            = 'ClusterF LJ Radar v1.0'
        self.FormBorderStyle = FormBorderStyle.FixedToolWindow
        self.TopMost         = True
        self.BackColor       = Color.FromArgb(14, 20, 10)
        self.Width           = grid_px + 18
        self.Height          = status_y + 30
        self.StartPosition   = FormStartPosition.Manual
        self.Location        = Point(100, 100)

        mono7 = Font('Courier New', 7, FontStyle.Regular)

        self._lbl_skill = Label()
        self._lbl_skill.ForeColor = Color.Silver
        self._lbl_skill.BackColor = Color.Transparent
        self._lbl_skill.Font      = mono7
        self._lbl_skill.Location  = Point(4, 4)
        self._lbl_skill.Size      = Size(grid_px + 8, 14)
        self.Controls.Add(self._lbl_skill)

        self._lbl_stats = Label()
        self._lbl_stats.ForeColor = Color.FromArgb(130, 195, 120)
        self._lbl_stats.BackColor = Color.Transparent
        self._lbl_stats.Font      = mono7
        self._lbl_stats.Location  = Point(4, 20)
        self._lbl_stats.Size      = Size(grid_px + 8, 14)
        self.Controls.Add(self._lbl_stats)

        # Tile grid
        tbl             = TableLayoutPanel()
        tbl.RowCount    = GRID_SIZE
        tbl.ColumnCount = GRID_SIZE
        tbl.Location    = Point(4, _GRID_TOP)
        tbl.Size        = Size(grid_px, grid_px)
        tbl.BackColor   = Color.FromArgb(14, 20, 10)   # matches form background
        tbl.Padding     = Padding(0)
        tbl.Margin      = Padding(0)
        tbl.CellBorderStyle = getattr(TableLayoutPanelCellBorderStyle, 'None')

        for _ in range(GRID_SIZE):
            tbl.RowStyles.Add(RowStyle(SizeType.Absolute, CELL_PX))
            tbl.ColumnStyles.Add(ColumnStyle(SizeType.Absolute, CELL_PX))

        for dy in range(-GRID_RADIUS, GRID_RADIUS + 1):
            for dx in range(-GRID_RADIUS, GRID_RADIUS + 1):
                btn           = RadarCell()
                btn.Width     = CELL_PX - 2
                btn.Height    = CELL_PX - 2
                btn.Margin    = Padding(1)
                btn.FlatStyle = FlatStyle.Flat
                btn.FlatAppearance.BorderSize = 0
                btn.Text      = ''
                btn.Font      = _FONT_CELL

                def _make_click(adx, ady):
                    def _h(s, e):
                        self._on_cell_click(adx, ady)
                    return _h

                btn.Click += _make_click(dx, dy)
                self._cells[(dx, dy)] = btn
                tbl.Controls.Add(btn, dx + GRID_RADIUS, dy + GRID_RADIUS)

        self.Controls.Add(tbl)

        # Button + checkbox row
        def _mkbtn(text, x, w, handler, fg=None, bg=None, border=None):
            b           = Button()
            b.Text      = text
            b.Location  = Point(x, btn_y)
            b.Size      = Size(w, 20)
            b.FlatStyle = FlatStyle.Flat
            b.Font      = Font('Arial Narrow', 7, FontStyle.Bold)
            b.ForeColor = fg     or Color.FromArgb(210, 210, 210)
            b.BackColor = bg     or Color.FromArgb(32, 45, 28)
            b.FlatAppearance.BorderColor = border or Color.FromArgb(70, 110, 60)
            if handler:
                b.Click += handler
            self.Controls.Add(b)
            return b

        self._btn_auto = _mkbtn('AUTO: OFF', 4, 70, self._on_auto_toggle,
                                fg=Color.FromArgb(140, 160, 130),
                                bg=Color.FromArgb(28, 38, 22),
                                border=Color.FromArgb(60, 85, 50))

        btn_next = _mkbtn('NEXT', 78, 48, self._on_next)
        self._tips.SetToolTip(btn_next, 'Chop nearest in-range uncharted tree.')

        btn_walk = _mkbtn('WALK', 130, 48, self._on_walk)
        self._tips.SetToolTip(btn_walk, 'Pathfind toward nearest visible tree without chopping.')

        btn_save = _mkbtn('SAVE', 182, 48, self._on_save,
                          bg=Color.FromArgb(28, 45, 28),
                          border=Color.FromArgb(60, 120, 60))
        self._tips.SetToolTip(btn_save, 'Flush cache to disk now.')

        def _mkchk(text, x, w, checked):
            c           = CheckBox()
            c.Text      = text
            c.Location  = Point(x, btn_y)
            c.Size      = Size(w, 20)
            c.Checked   = checked
            c.ForeColor = Color.FromArgb(170, 195, 155)
            c.BackColor = Color.Transparent
            c.Font      = Font('Arial Narrow', 8, FontStyle.Regular)
            self.Controls.Add(c)
            return c

        self._chk_walk = _mkchk('Auto walk', 236, 92, True)
        self._tips.SetToolTip(self._chk_walk, 'Walk to out-of-range trees before chopping.')
        self._chk_run  = _mkchk('Run', 332, 52, True)
        self._tips.SetToolTip(self._chk_run, 'Run while pathfinding.')

        btn_dbg = _mkbtn('DBG', grid_px - 34, 36, self._on_debug,
                         fg=Color.Yellow, bg=Color.FromArgb(30, 30, 20),
                         border=Color.Yellow)
        self._tips.SetToolTip(btn_dbg, 'Dump tile + tool info to chat.')

        # Legend
        legend_items = [
            ('Tree', 'tree'),  ('Lo', 'Ordinary'), ('Ok', 'Oak'),
            ('Yw', 'Yew'),     ('Hw', 'Heartwood'), ('Fw', 'Frostwood'),
            ('Bw', 'Bloodwood'), ('X', 'Depleted'),
        ]
        fp             = FlowLayoutPanel()
        fp.Location    = Point(4, legend_y)
        fp.Size        = Size(grid_px + 8, 20)
        fp.BackColor   = Color.Transparent
        fp.FlowDirection = FlowDirection.LeftToRight
        for txt, state in legend_items:
            l           = Label()
            l.Text      = txt
            l.BackColor = CELL_BG.get(state, Color.Gray)
            l.ForeColor = Color.White if state in _NEEDS_WHITE else Color.Black
            l.Font      = Font('Arial Narrow', 6, FontStyle.Regular)
            l.Width     = 28
            l.Height    = 14
            l.Margin    = Padding(1, 0, 1, 0)
            fp.Controls.Add(l)
        self.Controls.Add(fp)

        self._lbl_status = Label()
        self._lbl_status.ForeColor = Color.FromArgb(150, 175, 140)
        self._lbl_status.BackColor = Color.Transparent
        self._lbl_status.Font      = mono7
        self._lbl_status.Location  = Point(4, status_y)
        self._lbl_status.Size      = Size(grid_px + 8, 16)
        self.Controls.Add(self._lbl_status)

        self._update_labels()
        self._update_status()

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    def _ui_call(self, callback):
        try:
            if self.IsDisposed:
                return
            if self.InvokeRequired:
                self.BeginInvoke(Action(callback))
            else:
                callback()
        except:
            pass

    def _set_status(self, text, color=None):
        if text == self._last_status:
            return
        self._last_status          = text
        self._lbl_status.Text      = text
        self._lbl_status.ForeColor = color or Color.FromArgb(150, 175, 140)

    def _update_labels(self):
        real  = _skill_real()
        shown = _skill_shown()
        cap   = _skill_cap()
        ppos  = _player_pos()
        coord = '  ({}, {})'.format(ppos[0], ppos[1]) if ppos else ''
        self._lbl_skill.Text = '{} {:.1f}/{:.1f}  shown {:.1f}  followers {}{}'.format(
            SKILL_NAME, real, cap, shown, _followers_text(), coord)
        self._lbl_stats.Text = 'tries {}  wood {}  gains {}  start {:.1f}'.format(
            self._attempts, _session_stats['wood_found'], self._gains, self._start_skill)

    def _update_status(self, extra=None, color=None):
        ppos = _player_pos()
        if ppos is None:
            self._set_status('Player unavailable', Color.FromArgb(220, 120, 120))
            return
        known, depleted, total, _chunks = cache_counts(ppos[3])
        mode = 'AUTO' if self._auto else 'MAN'
        work = 'chopping' if self._chopping else 'idle'
        text = '{}  {}  | known {}  depleted {}  cache {}'.format(
            mode, work, known, depleted, total)
        if extra:
            text = extra + '  |  ' + text
        self._set_status(text, color or Color.FromArgb(150, 175, 140))

    # ------------------------------------------------------------------
    # Button handlers
    # ------------------------------------------------------------------

    def _on_auto_toggle(self, sender, e):
        self._auto = not self._auto
        if self._auto:
            self._btn_auto.Text      = 'AUTO: ON'
            self._btn_auto.ForeColor = Color.FromArgb(100, 230, 80)
            self._btn_auto.BackColor = Color.FromArgb(18, 55, 18)
            self._btn_auto.FlatAppearance.BorderColor = Color.FromArgb(60, 185, 55)
            Misc.SendMessage('[LJRadar] Auto-chop ON.', 0x44)
        else:
            self._btn_auto.Text      = 'AUTO: OFF'
            self._btn_auto.ForeColor = Color.FromArgb(140, 160, 130)
            self._btn_auto.BackColor = Color.FromArgb(28, 38, 22)
            self._btn_auto.FlatAppearance.BorderColor = Color.FromArgb(60, 85, 50)
            Misc.SendMessage('[LJRadar] Auto-chop OFF.', 0x44)
        self._update_status()

    def _on_save(self, sender, e):
        global _dirty
        _dirty = True
        cache_save()
        self._update_status('saved', Color.FromArgb(120, 220, 120))

    def _on_debug(self, sender, e):
        try:
            ppos = _player_pos()
            if ppos is None:
                Misc.SendMessage('[DBG] player pos unavailable', 0x22)
                return
            px, py, pz, map_name = ppos
            midx = _map_idx(map_name)
            Misc.SendMessage('[DBG] pos=({},{},{}) map={!r} midx={}'.format(
                px, py, pz, map_name, midx), 0x44)
            Misc.SendMessage('[DBG] tool={} skill={:.1f}'.format(find_tool(), _skill_real()), 0x44)

            # Dump raw statics at player tile and ring of adjacent tiles
            check_tiles = [(px, py)] + [(px+dx, py+dy)
                          for dx in range(-2, 3) for dy in range(-2, 3)
                          if not (dx == 0 and dy == 0)]
            found_any = False
            for tx, ty in check_tiles[:25]:
                infos = _get_static_infos(tx, ty, map_name)
                if not infos:
                    continue
                for info in infos:
                    sid = _tile_id(info)
                    sz  = _tile_z(info)
                    hit = sid in CHOPABLE_TILES if sid is not None else False
                    if hit or (tx == px and ty == py):
                        Misc.SendMessage('[DBG] ({},{}) static id={:#06x}({}) z={} chopable={}'.format(
                            tx, ty, sid or 0, sid, sz, hit), 0x44)
                        found_any = True
            if not found_any:
                Misc.SendMessage('[DBG] no statics found in 5x5 area (API may be failing)', 0x35)
                # Try listing available methods on Statics object
                try:
                    api = Statics
                    meths = [m for m in dir(api) if not m.startswith('_')][:10]
                    Misc.SendMessage('[DBG] Statics methods: {}'.format(meths), 0x44)
                except NameError:
                    Misc.SendMessage('[DBG] Statics not accessible at all!', 0x22)
        except Exception as ex:
            Misc.SendMessage('[DBG] error: {}'.format(ex), 0x22)

    # ------------------------------------------------------------------
    # Target selection
    # ------------------------------------------------------------------

    def _candidate_offsets(self):
        offsets = []
        for dy in range(-GRID_RADIUS, GRID_RADIUS + 1):
            for dx in range(-GRID_RADIUS, GRID_RADIUS + 1):
                if dx == 0 and dy == 0:
                    continue
                if max(abs(dx), abs(dy)) > MAX_TARGET_RANGE:
                    continue
                offsets.append((max(abs(dx), abs(dy)), abs(dx) + abs(dy), dx, dy))
        offsets.sort()
        return [(dx, dy) for _, _, dx, dy in offsets]

    def _find_next_target(self):
        ppos = _player_pos()
        if ppos is None:
            return None
        px, py, _pz, map_name = ppos
        best_known = best_unknown = None
        for dx, dy in self._candidate_offsets():
            state = cell_state(px + dx, py + dy, map_name)
            if state == 'tree' and best_unknown is None:
                best_unknown = (dx, dy)
            elif state in WOOD_LABELS and best_known is None:
                best_known = (dx, dy)
        return best_known or best_unknown

    def _on_next(self, sender, e):
        target = self._find_next_target()
        if target is None:
            Misc.SendMessage('[LJRadar] No tree within range {}.'.format(MAX_TARGET_RANGE), 0x22)
            self._update_status('no target in range', Color.FromArgb(220, 170, 90))
            return
        self._start_chop_offset(target[0], target[1], allow_depleted=False)

    def _on_walk(self, sender, e):
        if self._chopping:
            return
        target = self._find_next_target()
        if target is None:
            ppos = _player_pos()
            if ppos:
                px, py, _pz, map_name = ppos
                for dist in range(1, GRID_RADIUS + 1):
                    for dy in range(-dist, dist + 1):
                        for dx in range(-dist, dist + 1):
                            if max(abs(dx), abs(dy)) != dist:
                                continue
                            s = cell_state(px + dx, py + dy, map_name)
                            if s == 'tree' or s in WOOD_LABELS:
                                target = (dx, dy)
                                break
                        if target:
                            break
                    if target:
                        break
        if target is None:
            Misc.SendMessage('[LJRadar] No tree visible.', 0x22)
            return

        ppos = _player_pos()
        if ppos is None:
            return
        px, py, pz, map_name = ppos
        dx, dy = target
        tx, ty = px + dx, py + dy
        tz     = _target_z(tx, ty, map_name, pz)
        run    = bool(self._chk_run.Checked)

        self._chopping = True
        self._update_status()

        def _walk_worker():
            try:
                self._ui_call(lambda: self._set_status(
                    'Walking to ({}, {})...'.format(tx, ty),
                    Color.FromArgb(180, 220, 155)))
                _path_to(tx, ty, tz, run=run)
                arrived = _wait_in_range(tx, ty, MAX_TARGET_RANGE)
                if arrived:
                    self._ui_call(lambda: self._set_status(
                        'In range of ({}, {}).'.format(tx, ty),
                        Color.FromArgb(120, 220, 100)))
                else:
                    self._ui_call(lambda: self._set_status(
                        'Could not reach ({}, {}).'.format(tx, ty),
                        Color.FromArgb(220, 120, 100)))
            finally:
                self._chopping = False
                self._ui_call(lambda: self._update_status())

        t = threading.Thread(target=_walk_worker)
        t.daemon = True
        t.start()

    # ------------------------------------------------------------------
    # Cell click → chop
    # ------------------------------------------------------------------

    def _on_cell_click(self, dx, dy):
        self._start_chop_offset(dx, dy, allow_depleted=True)

    def _start_chop_offset(self, dx, dy, allow_depleted=True):
        if self._chopping:
            return
        ppos = _player_pos()
        if ppos is None:
            Misc.SendMessage('[LJRadar] Player position unavailable.', 0x22)
            return
        px, py, pz, map_name = ppos
        tx, ty = px + dx, py + dy

        state = cell_state(tx, ty, map_name)
        if state == 'surface':
            return
        if state == 'Depleted' and not allow_depleted:
            return

        dist       = max(abs(dx), abs(dy))
        tz         = _target_z(tx, ty, map_name, pz)
        cell       = self._cells.get((dx, dy))
        needs_walk = dist > MAX_TARGET_RANGE

        if needs_walk and not self._chk_walk.Checked:
            Misc.SendMessage(
                '[LJRadar] Too far (dist {}). Enable Auto Walk or move closer.'.format(dist),
                0x35)
            self._update_status('too far – enable Auto Walk',
                                Color.FromArgb(235, 180, 80))
            return

        self._chopping = True
        self._update_status()
        run = bool(self._chk_run.Checked)

        def _worker():
            tz_use = tz
            try:
                if needs_walk:
                    self._ui_call(lambda: self._set_status(
                        'Walking to ({}, {})...'.format(tx, ty),
                        Color.FromArgb(180, 220, 155)))
                    _path_to(tx, ty, tz_use, run=run)
                    if not _wait_in_range(tx, ty, MAX_TARGET_RANGE):
                        self._ui_call(lambda: self._set_status(
                            'Could not reach ({}, {}).'.format(tx, ty),
                            Color.FromArgb(220, 120, 100)))
                        return
                    p2 = _player_pos()
                    if p2:
                        tz_use = _target_z(tx, ty, p2[3], p2[2])

                if self._auto:
                    self._auto_chop_loop(tx, ty, tz_use, map_name, cell)
                else:
                    self._single_chop(tx, ty, tz_use, map_name)
            finally:
                self._chopping = False
                if cell:
                    self._ui_call(lambda: cell.ClearHaze())
                self._ui_call(lambda: self._update_labels())
                self._ui_call(lambda: self._update_status())
                _grid_dirty.set()

        t = threading.Thread(target=_worker)
        t.daemon = True
        t.start()

    # ------------------------------------------------------------------
    # Chopping loops
    # ------------------------------------------------------------------

    _EMPTY_MSGS = [
        "There's not enough wood here to harvest",
        'There is not enough wood here',
        'You find no wood',
        'No wood here',
    ]
    _STOP_MSGS = [
        'You have worn out your tool',
        'Your backpack is full',
        'Your pack is full',
        'You are overloaded',
        "You can't carry any more",
        'The axe must be equipped',
    ]

    def _single_chop(self, tx, ty, tz, map_name):
        self._attempts += 1
        try:
            Journal.Clear()
        except:
            pass
        tile_id = _scan_tile(tx, ty, map_name)[2]   # static graphic ID, already cached
        set_pending(tx, ty, map_name)
        if not do_chop(tx, ty, tz, tile_id):
            clear_pending((tx, ty, map_name))
        time.sleep(CHOP_WAIT / 1000.0)

    def _auto_chop_loop(self, tx, ty, tz, map_name, cell):
        if cell:
            self._ui_call(lambda: cell.SetHaze(_HAZE_CHOPPING))
        for _ in range(MAX_AUTO_SWINGS):
            if not self._auto:
                break
            try:
                if Player.Weight >= Player.MaxWeight - STOP_WEIGHT_MARGIN:
                    Misc.SendMessage('[LJRadar] Overloaded — stopping.', 0x22)
                    break
            except:
                pass

            self._attempts += 1
            try:
                Journal.Clear()
            except:
                pass
            tile_id = _scan_tile(tx, ty, map_name)[2]
            set_pending(tx, ty, map_name)
            if not do_chop(tx, ty, tz, tile_id):
                clear_pending((tx, ty, map_name))
                break

            time.sleep(CHOP_WAIT / 1000.0)

            for msg in self._STOP_MSGS:
                try:
                    if Journal.Search(msg):
                        Misc.SendMessage('[LJRadar] Stopping: ' + msg, 0x22)
                        return
                except:
                    pass

            depleted = False
            for msg in self._EMPTY_MSGS:
                try:
                    if Journal.Search(msg):
                        depleted = True
                        break
                except:
                    pass
            if not depleted and cache_get(tx, ty, map_name) == 'Depleted':
                depleted = True
            if depleted:
                cache_set(tx, ty, map_name, 'Depleted')
                break

    # ------------------------------------------------------------------
    # Timer tick
    # ------------------------------------------------------------------

    def _on_tick(self, sender, e):
        try:
            process_journal()
        except:
            pass

        current = _skill_real()
        if current > self._last_skill + 0.0001:
            self._gains     += 1
            self._last_skill = current

        now = time.time()
        if now - self._last_save > CACHE_SAVE_SECONDS:
            cache_save()
            self._last_save = now

        ppos = _player_pos()
        if ppos is not None:
            px, py, _pz, map_name = ppos

            if map_name != self._last_map:
                _scan_cache.clear()
                self._last_map = map_name
            elif len(_scan_cache) > SCAN_CACHE_MAX:
                _scan_cache.clear()

            pos = (px, py, map_name)
            if pos != self._last_pos or _grid_dirty.is_set():
                self._last_pos = pos
                _grid_dirty.clear()
                self._refresh(px, py, map_name)

        self._update_labels()
        self._update_status()

    # ------------------------------------------------------------------
    # Grid refresh
    # ------------------------------------------------------------------

    def _refresh(self, cx, cy, map_name):
        _bg = Color.FromArgb(14, 20, 10)   # form background — used for invisible cells
        for (dx, dy), btn in self._cells.items():
            tx, ty   = cx + dx, cy + dy
            is_ctr   = (dx == 0 and dy == 0)
            in_range = max(abs(dx), abs(dy)) <= MAX_TARGET_RANGE

            if is_ctr:
                state, label, font = 'player', '@', _FONT_CELL
            else:
                state = cell_state(tx, ty, map_name)
                if state == 'tree':
                    label, font = _TREE_EMOJI, _FONT_EMOJI
                else:
                    label = WOOD_LABELS.get(state, 'X' if state == 'Depleted' else '')
                    font  = _FONT_CELL

            # Surface tiles: invisible — blend into form background
            if not is_ctr and state == 'surface':
                btn.BackColor = _bg
                btn.ForeColor = _bg
                btn.FlatAppearance.BorderColor       = _bg
                btn.FlatAppearance.MouseOverBackColor = _bg
                btn.Text   = ''
                btn.Cursor = Cursors.Default
                btn.SetChunkEdges(False, False, False, False)
                btn.ClearHaze()
                self._tips.SetToolTip(btn, '')
                continue

            # Tree / wood / depleted / player cell
            btn.SetChunkEdges(
                (tx % RESOURCE_CHUNK_SIZE) == 0,
                (ty % RESOURCE_CHUNK_SIZE) == 0,
                (tx % RESOURCE_CHUNK_SIZE) == RESOURCE_CHUNK_SIZE - 1,
                (ty % RESOURCE_CHUNK_SIZE) == RESOURCE_CHUNK_SIZE - 1,
            )

            bg = CELL_BG.get(state, CELL_BG['surface'])
            if not is_ctr and not in_range:
                bg = Color.FromArgb(max(bg.R-30, 0), max(bg.G-30, 0), max(bg.B-30, 0))

            fg = Color.White if (state in _NEEDS_WHITE or is_ctr) else Color.Black
            btn.BackColor = bg
            btn.ForeColor = fg
            btn.FlatAppearance.BorderColor       = bg   # borderless look; chunk lines show via OnPaint
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                min(bg.R+35, 255), min(bg.G+35, 255), min(bg.B+35, 255))
            btn.Font   = font
            btn.Cursor = Cursors.Hand
            btn.SetHaze(_HAZE_DEPLETED) if state == 'Depleted' else btn.ClearHaze()
            btn.Text = label

            tip = '({}, {})  Chunk {}'.format(tx, ty, _chunk_label(tx, ty))
            if not in_range and not is_ctr:
                tip += '  [out of range]'
            if state in WOOD_LABELS:
                tip += '  –  ' + state
            elif state == 'tree':
                scan = _scan_tile(tx, ty, map_name)
                tip += '  –  Tree'
                if scan[2]:
                    tip += ' (0x{:04X})'.format(scan[2])
            elif state == 'Depleted':
                tip += '  –  Depleted'
            self._tips.SetToolTip(btn, tip)


# =============================================================================
# Entry point
# =============================================================================


def main():
    Misc.SendMessage('[LJRadar] Starting...', 0x44)
    cache_load()
    Application.EnableVisualStyles()
    form = RadarForm()
    Misc.SendMessage('[LJRadar] Ready. Green cells = uncharted trees.', 0x44)
    Application.Run(form)
    cache_save()
    Misc.SendMessage('[LJRadar] Closed.', 0x44)


main()
