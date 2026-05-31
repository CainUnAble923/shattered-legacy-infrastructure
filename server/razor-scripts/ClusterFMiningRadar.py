# =============================================================================
# -*- coding: utf-8 -*-
# ClusterF Mining Radar for RazorEnhanced
# Adapted from CyberPope's Mining Radar 4.1d_F002.euo
#
# USAGE
#   In RazorEnhanced → Scripts tab → Add → select this file → Play
#   A 13x13 tile grid appears. Dark-gray = mineable unknown. Click to mine.
#
# CELL COLORS
#   Light gray        = mineable mountain/rock wall, ore type not known
#   Dark gray-brown   = mineable cave/unknown, ore type not yet known
#   Black/dark        = non-mineable paths/roads
#   Green             = grass/foliage
#   Colored           = known ore type (see legend at bottom of window)
#   Red !             = blocked from current position (line of sight)
#   Black X           = depleted
#   White @           = you
#
# CACHE
#   C:\UO\Scripts\clusterf_mining_cache.json
#   ClusterF uses permanent veins, so cache entries never go stale.
# =============================================================================

import clr
import json
import os
import re
import struct
import threading
import time

clr.AddReference('System')
clr.AddReference('System.Windows.Forms')
clr.AddReference('System.Drawing')

from System import Action
from System.Windows.Forms import (
    Application, Form, Button, Label, Panel, CheckBox,
    FlowLayoutPanel, FlowDirection, FormBorderStyle, FlatStyle,
    Padding, ToolTip, Timer, FormStartPosition, Cursors, AnchorStyles
)
from System.Drawing import Color, Font, FontStyle, Point, Size, SolidBrush, ContentAlignment, Pen

# =============================================================================
# Configuration
# =============================================================================

GRID_RADIUS = 6
GRID_SIZE   = GRID_RADIUS * 2 + 1
CELL_PX     = 36
REFRESH_MS  = 500
MINE_WAIT          = 3200
MUST_WAIT_RETRY_MS = 2500
MAX_WAIT_RETRIES   = 6
MAX_TARGET_RANGE   = 2      # Chebyshev distance — mine without walking
CHAIN_DEFAULT      = 50     # CHAIN tile limit per run  (0 = unlimited)
WALK_TIMEOUT_S     = 8.0    # seconds to wait for pathfinding to arrive
STOP_WEIGHT_MARGIN = 20     # weight headroom before auto-stop
LEGEND_H           = 116
BANK_SIZE   = 8
ASSUME_BANK_DEPLETED = True
# How long (seconds) before a depleted tile auto-clears. Set this to match the
# server's ore-respawn timer (ModernUO default is 600 s = 10 min).
ORE_RESPAWN_SECONDS = 600

# Isometric (UO-radar-aligned) grid layout.
#   GRID_DIAG  = 4·R + 1 cells per side of the screen grid.
#   CELL_ISO   = pixel size of each screen cell (including dead-zone cells).
# Only cells where (col+row) % 2 == 0 are active; they form a diamond whose
# edges point N (upper-right), E (lower-right), S (lower-left), W (upper-left),
# matching the UO minimap / classic-client radar orientation.
GRID_DIAG  = 4 * GRID_RADIUS + 1   # 25 for default GRID_RADIUS=6
CELL_STEP  = 18    # screen pixels between adjacent col/row positions
CELL_DRAW  = 24    # drawn cell size in pixels.  CELL_DRAW > CELL_STEP so that
                   # UO-adjacent tiles (diagonal neighbours in screen space) overlap
                   # by (CELL_DRAW - CELL_STEP) px at their corners, eliminating the
                   # large dead-zone gap produced by a TableLayoutPanel.

CACHE_PATH = os.path.join(os.path.dirname(__file__), 'clusterf_mining_cache.json')

# ClassicUO world-map marker export.
# ClassicUO CSV marker rows use:
#   x,y,mapindex,name of marker,iconname,color,zoom level
# If auto-detection misses your client, set MARKER_CLIENT_DIR to its Data\Client
# folder, e.g. r'C:\Games\ClassicUO\Data\Client'.
MARKER_EXPORT_ENABLED = True
MARKER_CLIENT_DIR     = ''
MARKER_CSV_NAME       = 'ClusterF_Ores.csv'
MARKER_ICON_NAME      = 'clusterf_ore'
MARKER_ZOOM_LEVEL     = 0
MARKER_SCRIPT_PATH    = os.path.join(os.path.dirname(__file__), MARKER_CSV_NAME)

# =============================================================================
# Ore definitions  (17 types: vanilla UO + 8 ClusterF extended)
# =============================================================================

ORE_LABELS = {
    'Iron': 'Fe',  'DullCopper': 'DC', 'ShadowIron': 'SI',
    'Copper': 'Cu', 'Bronze': 'Bz',   'Gold': 'Au',
    'Agapite': 'Ag','Verite': 'Vr',   'Valorite': 'Va',
    'Platinum': 'Pt','Toxic': 'Tx',   'Blaze': 'Bl',
    'Frost': 'Fr',  'Obsidian': 'Ob', 'Mythril': 'My',
    'Adamantium': 'Ad', 'Celestial': 'Ce',
}

ORE_DISPLAY = {
    'Iron': 'iron', 'DullCopper': 'dull copper', 'ShadowIron': 'shadow iron',
    'Copper': 'copper', 'Bronze': 'bronze', 'Gold': 'gold',
    'Agapite': 'agapite', 'Verite': 'verite', 'Valorite': 'valorite',
    'Platinum': 'platinum', 'Toxic': 'toxic', 'Blaze': 'blaze',
    'Frost': 'frost', 'Obsidian': 'obsidian', 'Mythril': 'mythril',
    'Adamantium': 'adamantium', 'Celestial': 'celestial',
}

CELL_BG = {
    'surface':    Color.FromArgb(38,  50,  25),
    'cave':       Color.FromArgb(65,  55,  50),
    'mountain':   Color.FromArgb(170, 170, 165),
    'rock':       Color.FromArgb(135, 135, 130),
    'path':       Color.FromArgb(10,  10,  10),
    'grass':      Color.FromArgb(42,  95,  38),
    'forest':     Color.FromArgb(25,  70,  30),
    'dirt':       Color.FromArgb(92,  66,  42),
    'sand':       Color.FromArgb(150, 128, 76),
    'water':      Color.FromArgb(32,  70,  120),
    'stone':      Color.FromArgb(78,  78,  76),
    'blocked':    Color.FromArgb(115, 38, 38),
    'Iron':       Color.FromArgb(140, 140, 140),
    'DullCopper': Color.FromArgb(115, 82,  45),
    'ShadowIron': Color.FromArgb(72,  68,  100),
    'Copper':     Color.FromArgb(190, 118, 52),
    'Bronze':     Color.FromArgb(158, 106, 58),
    'Gold':       Color.FromArgb(210, 178, 50),
    'Agapite':    Color.FromArgb(188, 108, 140),
    'Verite':     Color.FromArgb(60,  165, 82),
    'Valorite':   Color.FromArgb(95,  130, 225),
    'Platinum':   Color.FromArgb(195, 215, 255),
    'Toxic':      Color.FromArgb(98,  215, 58),
    'Blaze':      Color.FromArgb(235, 82,  28),
    'Frost':      Color.FromArgb(145, 205, 255),
    'Obsidian':   Color.FromArgb(42,  28,  52),
    'Mythril':    Color.FromArgb(162, 108, 235),
    'Adamantium': Color.FromArgb(75,  88,  185),
    'Celestial':  Color.FromArgb(255, 218, 95),
    'Depleted':   Color.FromArgb(18,  18,  18),
    'player':     Color.FromArgb(255, 255, 255),
}

_NEEDS_WHITE = frozenset([
    'surface', 'cave', 'path', 'grass', 'forest', 'dirt', 'water', 'stone',
    'blocked', 'DullCopper', 'ShadowIron', 'Copper', 'Bronze', 'Agapite',
    'Obsidian', 'Mythril', 'Adamantium', 'Depleted',
])

_MINE_TARGET_STATES = frozenset(['cave', 'mountain'])

# States where we allow a click even though classification looks non-mineable.
# Includes 'water' because cliff faces sit on ocean land tiles; 'rock'/'stone'/
# 'surface'/'dirt'/'sand' for unclassified or partially-classified terrain; and
# 'blocked' because LOS issues don't mean the tile can't yield ore.
# 'grass', 'forest', 'path', 'player' are excluded — genuinely non-mineable.
_CLICKABLE_STATES = _MINE_TARGET_STATES | frozenset(
    ['Depleted', 'blocked', 'water', 'rock', 'stone', 'surface', 'dirt', 'sand']
) | frozenset(ORE_LABELS.keys())

# =============================================================================
# Mineable land tile IDs  (CyberPope''s %digable list, verbatim)
# =============================================================================

_DIGABLE_RANGES = [
    (220, 231), (236, 247), (252, 263), (268, 279),
    (286, 294), (296, 297), (321, 324),
    (467, 474), (476, 487), (492, 495),
    (543, 601), (610, 613),
    (1010, 1010),
    (1339, 1359),
    (1741, 1757), (1771, 1790),
    (1801, 1809), (1811, 1824),
    (1831, 1854), (1861, 1884),
    (1981, 2004), (2028, 2033), (2100, 2105),
]

MINEABLE_TILES = {2, 430, 475}
for _lo, _hi in _DIGABLE_RANGES:
    for _t in range(_lo, _hi + 1):
        MINEABLE_TILES.add(_t)

_scan_cache     = {}
_SCAN_CACHE_MAX = 20000   # ~20k tiles ≈ a 141×141 area; well beyond any session's range

# =============================================================================
# Journal patterns
# ClusterF''s TryLogDiscovery bakes exact (x, y) coords into every message.
# =============================================================================

_PAT_DISC  = re.compile(r'Discovery recorded:\s+(.+?)\s+ore\b.*?\((\d+),\s*(\d+)\)', re.IGNORECASE)
_PAT_VEIN  = re.compile(r'New\s+(.+?)\s+vein logged.*?\((\d+),\s*(\d+)\)', re.IGNORECASE)
_PAT_DIG   = re.compile(r'[Yy]ou dig some (.+?) ore', re.IGNORECASE)

_DEPL_PATS = [
    re.compile(r'[Tt]here is no (?:metal|ore) here', re.IGNORECASE),
    re.compile(r'[Nn]o metal here',                  re.IGNORECASE),
    re.compile(r'[Yy]ou find no metal',              re.IGNORECASE),
]

_BLOCK_PATS = [
    re.compile(r"(?:cannot|can't) be seen", re.IGNORECASE),
    re.compile(r"[Yy]ou (?:cannot|can't) see that", re.IGNORECASE),
    re.compile(r"[Yy]ou (?:cannot|can't) see the target", re.IGNORECASE),
]

# Substrings matched against recent journal entries to detect a server-side
# action-timer rejection.  Journal.Search() is unreliable on this shard;
# _journal_recent_has() uses GetJournalEntry instead.
_WAIT_MSGS = [
    'you must wait',
    'must wait a few',
    'wait before performing',
]

_DISPLAY_TO_KEY = {v: k for k, v in ORE_DISPLAY.items()}
for _k in list(ORE_LABELS.keys()):
    _DISPLAY_TO_KEY[_k.lower()] = _k


def _resolve_ore(phrase):
    return _DISPLAY_TO_KEY.get(phrase.strip().lower())

# Session counters — reset each time the script is loaded.
_session_stats = {'ore_found': 0, 'swings': 0}

# =============================================================================
# Persistent tile cache
# =============================================================================

_cache             = {}
_cache_lock        = threading.Lock()
_dirty             = False
# Maps cache-key → Unix timestamp when the 'Depleted' entry should auto-clear.
# Protected by _cache_lock.  Persisted in the JSON cache alongside tile data.
_depletion_expiry  = {}
_blocked           = {}
_blocked_lock      = threading.Lock()


def _ckey(x, y, m):
    return '{},{},{}'.format(x, y, m)


def _bank_origin(x, y):
    return (x // BANK_SIZE) * BANK_SIZE, (y // BANK_SIZE) * BANK_SIZE


def _bank_label(x, y):
    bx, by = _bank_origin(x, y)
    return '{}-{}, {}-{}'.format(bx, bx + BANK_SIZE - 1, by, by + BANK_SIZE - 1)


def _bank_edges(x, y):
    edges = 0
    if x % BANK_SIZE == 0:
        edges |= _EDGE_LEFT
    if x % BANK_SIZE == BANK_SIZE - 1:
        edges |= _EDGE_RIGHT
    if y % BANK_SIZE == 0:
        edges |= _EDGE_TOP
    if y % BANK_SIZE == BANK_SIZE - 1:
        edges |= _EDGE_BOTTOM
    return edges


def cache_load():
    global _cache, _depletion_expiry
    if not os.path.isfile(CACHE_PATH):
        return
    try:
        with open(CACHE_PATH, 'r') as f:
            data = json.load(f)

        tiles  = data.get('tiles', {})
        expiry = {k: float(v) for k, v in data.get('depletion_expiry', {}).items()}

        # Immediately clear any depletions whose timer elapsed while the script was offline.
        now = time.time()
        auto_cleared = 0
        for k, exp in list(expiry.items()):
            if now >= exp:
                tiles.pop(k, None)   # remove 'Depleted'; tile returns to unknown/mineable
                del expiry[k]
                auto_cleared += 1

        # Legacy tiles depleted before the expiry-tracking system was added have no entry
        # in the expiry dict, so expire_depletions() never fires for them.  Give each one
        # a fresh full-length respawn timer so they eventually auto-clear like normal tiles.
        legacy_count = 0
        for k, v in list(tiles.items()):
            if v == 'Depleted' and k not in expiry:
                expiry[k] = now + ORE_RESPAWN_SECONDS
                legacy_count += 1

        with _cache_lock:
            _cache            = tiles
            _depletion_expiry = expiry

        Misc.SendMessage(
            '[MineRadar] {} cached tiles loaded ({} auto-cleared on load, {} legacy depleted given fresh timer).'.format(
                len(_cache), auto_cleared, legacy_count), 0x44)
    except Exception as ex:
        Misc.SendMessage('[MineRadar] Cache read error: {}'.format(ex), 0x22)


def cache_save():
    global _dirty
    # Snapshot and clear the dirty flag together under the lock so any concurrent
    # cache_set that fires between snapshot and write is recorded for the next save.
    with _cache_lock:
        if not _dirty:
            return
        snap        = dict(_cache)
        snap_expiry = dict(_depletion_expiry)
        _dirty = False
    try:
        with open(CACHE_PATH, 'w') as f:
            json.dump({'version': 2, 'tiles': snap, 'depletion_expiry': snap_expiry}, f)
    except Exception as ex:
        # Restore dirty so the next tick retries the write.
        with _cache_lock:
            _dirty = True
        Misc.SendMessage('[MineRadar] Cache write error: {}'.format(ex), 0x22)


def cache_set(x, y, map_name, ore_key):
    global _dirty
    k = _ckey(x, y, map_name)
    with _cache_lock:
        existing = _cache.get(k)
        if existing == ore_key:
            return False
        if existing and existing != 'Depleted' and ore_key == 'Iron' and existing != 'Iron':
            return False
        _cache[k] = ore_key
        _dirty = True
        return True


def cache_deplete_bank(x, y, map_name):
    expiry = time.time() + ORE_RESPAWN_SECONDS

    if not ASSUME_BANK_DEPLETED:
        k = _ckey(x, y, map_name)
        if cache_set(x, y, map_name, 'Depleted'):
            with _cache_lock:
                _depletion_expiry[k] = expiry
            return 1
        return 0

    changed = 0
    bx, by = _bank_origin(x, y)
    for tx in range(bx, bx + BANK_SIZE):
        for ty in range(by, by + BANK_SIZE):
            k = _ckey(tx, ty, map_name)
            state = cell_state(tx, ty, map_name)
            if _is_resource_state(state) and cache_set(tx, ty, map_name, 'Depleted'):
                with _cache_lock:
                    _depletion_expiry[k] = expiry
                changed += 1
    return changed


def _current_origin(map_name):
    ppos = _player_pos()
    if ppos is None:
        return None
    return ppos[0], ppos[1], map_name


def block_tile_from_here(x, y, map_name):
    origin = _current_origin(map_name)
    if origin is None:
        return False
    with _blocked_lock:
        _blocked[_ckey(x, y, map_name)] = origin
    return True


def is_blocked_from_here(x, y, map_name):
    k = _ckey(x, y, map_name)
    with _blocked_lock:
        origin = _blocked.get(k)
    if origin is None:
        return False

    if origin == _current_origin(map_name):
        return True

    with _blocked_lock:
        if _blocked.get(k) == origin:
            del _blocked[k]
    return False


def cache_get(x, y, map_name):
    with _cache_lock:
        return _cache.get(_ckey(x, y, map_name))


def expire_depletions():
    """Remove cache entries for depleted tiles whose respawn timer has elapsed.
    Called from the UI-thread timer tick so no additional locking is needed beyond
    the standard _cache_lock used everywhere else."""
    global _dirty
    now = time.time()
    with _cache_lock:
        expired = [k for k, exp in _depletion_expiry.items() if now >= exp]
        if not expired:
            return
        for k in expired:
            if _cache.get(k) == 'Depleted':
                del _cache[k]         # tile returns to unknown/mineable
            del _depletion_expiry[k]
        _dirty = True
    _grid_dirty.set()
    Misc.SendMessage(
        '[MineRadar] {} depleted tile(s) refreshed — ore respawn timer elapsed.'.format(len(expired)),
        0x44)

# =============================================================================
# Map name → integer index  (Tiles API requires int, Player.Map returns string)
# =============================================================================

_MAP_INDEX = {
    'felucca': 0, 'trammel': 1, 'ilshenar': 2,
    'malas': 3,   'tokuno': 4,  'termur': 5,
}

def _map_idx(map_name):
    return _MAP_INDEX.get(str(map_name).lower(), 1)  # default Trammel

# =============================================================================
# ClassicUO world-map marker export
# =============================================================================

_MARKER_COLORS = {
    'Iron': 'white',
    'DullCopper': 'yellow',
    'ShadowIron': 'purple',
    'Copper': 'yellow',
    'Bronze': 'yellow',
    'Gold': 'yellow',
    'Agapite': 'purple',
    'Verite': 'green',
    'Valorite': 'blue',
    'Platinum': 'white',
    'Toxic': 'green',
    'Blaze': 'red',
    'Frost': 'blue',
    'Obsidian': 'black',
    'Mythril': 'purple',
    'Adamantium': 'blue',
    'Celestial': 'yellow',
}

_ORE_MARKER_RANK = dict((ore, idx) for idx, ore in enumerate([
    'Iron', 'DullCopper', 'ShadowIron', 'Copper', 'Bronze', 'Gold',
    'Agapite', 'Verite', 'Valorite', 'Platinum', 'Toxic', 'Blaze',
    'Frost', 'Obsidian', 'Mythril', 'Adamantium', 'Celestial',
]))

_marker_lock             = threading.Lock()
_marker_warned_no_client = False


def _marker_clean(value):
    return str(value).replace(',', ' ').replace('\r', ' ').replace('\n', ' ').strip()


def _marker_bank_center(x, y):
    bx, by = _bank_origin(int(x), int(y))
    return bx + (BANK_SIZE // 2), by + (BANK_SIZE // 2)


def _marker_client_dirs():
    dirs = []
    seen = set()

    def add_dir(path, require_exists=False):
        if not path:
            return
        try:
            path = os.path.abspath(os.path.expandvars(os.path.expanduser(path)))
        except:
            return
        if require_exists and not os.path.isdir(path):
            return
        key = os.path.normcase(path)
        if key not in seen:
            dirs.append(path)
            seen.add(key)

    add_dir(MARKER_CLIENT_DIR)
    add_dir(os.environ.get('CLASSICUO_DATA_CLIENT_DIR'))

    # Best case: RazorEnhanced is attached while ClassicUO is running, so locate
    # the client process and write directly beside its executable.
    try:
        from System.Diagnostics import Process
        for proc_name in ('ClassicUO', 'ClassicUO-x64', 'TazUO', 'TazUO-x64'):
            for proc in Process.GetProcessesByName(proc_name):
                try:
                    exe = str(proc.MainModule.FileName)
                    add_dir(os.path.join(os.path.dirname(exe), 'Data', 'Client'))
                except:
                    pass
                try:
                    proc.Dispose()
                except:
                    pass
    except:
        pass

    # Fallbacks for common installs. These only count if the folder already exists.
    roots = [
        os.environ.get('LOCALAPPDATA'),
        os.environ.get('APPDATA'),
        os.environ.get('ProgramFiles'),
        os.environ.get('ProgramFiles(x86)'),
        r'D:\UO',
        r'C:\UO',
    ]
    rels = [
        os.path.join('ClassicUO', 'Data', 'Client'),
        os.path.join('ClassicUO', 'Client', 'Data', 'Client'),
        os.path.join('com.classicuo.launcher', 'Data', 'Client'),
        os.path.join('com.classicuo.launcher', 'Client', 'Data', 'Client'),
        os.path.join('ClassicUOLauncher-win-x64-release', 'ClassicUO', 'Data', 'Client'),
    ]
    for root in roots:
        for rel in rels:
            add_dir(os.path.join(root, rel) if root else '', True)

    return dirs


def _marker_paths():
    paths = [MARKER_SCRIPT_PATH]
    seen = set([os.path.normcase(os.path.abspath(MARKER_SCRIPT_PATH))])
    for data_dir in _marker_client_dirs():
        path = os.path.join(data_dir, MARKER_CSV_NAME)
        key = os.path.normcase(os.path.abspath(path))
        if key not in seen:
            paths.append(path)
            seen.add(key)
    return paths


def _is_client_marker_path(path):
    return os.path.normcase(os.path.abspath(path)) != os.path.normcase(os.path.abspath(MARKER_SCRIPT_PATH))


def _ensure_marker_icon(path):
    data_dir = os.path.dirname(path)
    icon_dir = os.path.join(data_dir, 'MapIcons')
    icon_path = os.path.join(icon_dir, MARKER_ICON_NAME + '.png')
    if os.path.isfile(icon_path):
        return

    try:
        if not os.path.isdir(icon_dir):
            os.makedirs(icon_dir)

        from System.Drawing import Bitmap, Graphics
        from System.Drawing.Imaging import ImageFormat

        bmp = Bitmap(13, 13)
        g = Graphics.FromImage(bmp)
        try:
            g.Clear(Color.FromArgb(0, 0, 0, 0))
            g.FillEllipse(SolidBrush(Color.FromArgb(240, 185, 40)), 2, 2, 9, 9)
            g.DrawEllipse(Pen(Color.FromArgb(20, 20, 20), 1), 2, 2, 9, 9)
            g.DrawLine(Pen(Color.FromArgb(20, 20, 20), 2), 4, 9, 9, 4)
            g.DrawLine(Pen(Color.FromArgb(255, 255, 230), 1), 3, 4, 7, 2)
            bmp.Save(icon_path, ImageFormat.Png)
        finally:
            try:
                g.Dispose()
                bmp.Dispose()
            except:
                pass
    except:
        pass


def _marker_csv_line(x, y, map_name, ore_key):
    cx, cy = _marker_bank_center(x, y)
    midx = _map_idx(map_name)
    ore_name = ORE_DISPLAY.get(ore_key, ore_key).title()
    label = 'ClusterF {} bank {}'.format(ore_name, _bank_label(x, y))
    line = '{},{},{},{},{},{},{}'.format(
        cx, cy, midx, _marker_clean(label), MARKER_ICON_NAME,
        _MARKER_COLORS.get(ore_key, 'yellow'), MARKER_ZOOM_LEVEL)
    return line, (cx, cy, midx)


def _bank_best_marker_ore(x, y, map_name, default_ore):
    best = default_ore
    best_rank = _ORE_MARKER_RANK.get(best, 0)
    bx, by = _bank_origin(x, y)
    with _cache_lock:
        for tx in range(bx, bx + BANK_SIZE):
            for ty in range(by, by + BANK_SIZE):
                ore_key = _cache.get(_ckey(tx, ty, map_name))
                rank = _ORE_MARKER_RANK.get(ore_key, -1)
                if rank > best_rank:
                    best = ore_key
                    best_rank = rank
    return best


def _marker_line_matches(raw, key):
    parts = [p.strip() for p in raw.split(',')]
    if len(parts) < 4:
        return False
    try:
        x, y, midx = int(parts[0]), int(parts[1]), int(parts[2])
    except:
        return False
    return (x, y, midx) == key and parts[3].startswith('ClusterF ')


def _upsert_marker_file(path, line, key):
    folder = os.path.dirname(path)
    if folder and not os.path.isdir(folder):
        os.makedirs(folder)

    lines = []
    try:
        if os.path.isfile(path):
            with open(path, 'r') as f:
                lines = [raw.rstrip('\r\n') for raw in f.readlines()]
    except:
        lines = []

    out = []
    changed = False
    inserted = False

    for raw in lines:
        if _marker_line_matches(raw, key):
            if not inserted:
                out.append(line)
                inserted = True
            changed = True
        else:
            out.append(raw)

    if not inserted:
        if line not in out:
            out.append(line)
            changed = True

    if not changed:
        return False

    with open(path, 'w') as f:
        for raw in out:
            if raw is not None and str(raw).strip():
                f.write(str(raw) + '\n')
    return True


def marker_record_ore(x, y, map_name, ore_key, quiet=False):
    global _marker_warned_no_client
    if not MARKER_EXPORT_ENABLED or ore_key not in ORE_LABELS:
        return False

    ore_key = _bank_best_marker_ore(x, y, map_name, ore_key)
    line, key = _marker_csv_line(x, y, map_name, ore_key)
    paths = _marker_paths()
    has_client_path = any(_is_client_marker_path(p) for p in paths)
    wrote = 0
    first_error = None

    with _marker_lock:
        for path in paths:
            try:
                if _upsert_marker_file(path, line, key):
                    wrote += 1
                _ensure_marker_icon(path)
            except Exception as ex:
                if first_error is None:
                    first_error = ex

    if wrote and not quiet:
        cx, cy = key[0], key[1]
        Misc.SendMessage(
            '[MineRadar] World-map marker: {} at {},{} (bank {}). Reload map markers if the map is already open.'.format(
                ORE_DISPLAY.get(ore_key, ore_key), cx, cy, _bank_label(x, y)),
            0x44)

    if wrote and not has_client_path and not _marker_warned_no_client:
        _marker_warned_no_client = True
        Misc.SendMessage(
            '[MineRadar] Marker CSV saved beside the script. Set MARKER_CLIENT_DIR if ClassicUO does not auto-load it.',
            0x35)
    elif first_error is not None and not quiet:
        Misc.SendMessage('[MineRadar] Marker write error: {}'.format(first_error), 0x22)

    return wrote > 0


def marker_export_cache():
    if not MARKER_EXPORT_ENABLED:
        return

    banks = {}
    with _cache_lock:
        snap = dict(_cache)

    for k, ore_key in snap.items():
        if ore_key not in ORE_LABELS:
            continue
        try:
            xs, ys, map_name = k.split(',', 2)
            x, y = int(xs), int(ys)
        except:
            continue
        cx, cy = _marker_bank_center(x, y)
        key = (cx, cy, _map_idx(map_name))
        old = banks.get(key)
        if old is None or _ORE_MARKER_RANK.get(ore_key, 0) > _ORE_MARKER_RANK.get(old[3], 0):
            banks[key] = (x, y, map_name, ore_key)

    changed = 0
    for key in sorted(banks.keys()):
        x, y, map_name, ore_key = banks[key]
        if marker_record_ore(x, y, map_name, ore_key, True):
            changed += 1

    if changed:
        Misc.SendMessage(
            '[MineRadar] Exported {} cached ore-bank marker(s) to {}.'.format(changed, MARKER_CSV_NAME),
            0x44)

# =============================================================================
# Safe player-position accessor
# RE exposes coords as Player.X/Y/Z on most builds; some use Player.Position.X/Y/Z.
# Returns (x, y, z, map_name) or None if player state is unavailable.
# =============================================================================


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


def _compass_offset(dx, dy):
    if dx == 0 and dy == 0:
        return 'Here'

    parts = []
    if dy < 0:
        parts.append('N{}'.format(-dy))
    elif dy > 0:
        parts.append('S{}'.format(dy))

    if dx > 0:
        parts.append('E{}'.format(dx))
    elif dx < 0:
        parts.append('W{}'.format(-dx))

    return ' '.join(parts)


# =============================================================================
# Tile classification
# =============================================================================


def _as_int(value):
    try:
        return int(value)
    except:
        return None


def _api_method(api_name, method_name):
    try:
        api = globals().get(api_name)
        if api is None:
            api = eval(api_name)
        if api is None:
            return None
        return getattr(api, method_name, None)
    except:
        return None


def _call_map_api(method_name, x, y, map_name):
    midx = _map_idx(map_name)
    for api_name in ('Statics', 'Tiles'):
        method = _api_method(api_name, method_name)
        if method is None:
            continue
        try:
            return method(x, y, midx)
        except:
            try:
                return method(x, y, map_name)
            except:
                pass
    return None


def _call_id_api(method_name, tile_id):
    if tile_id is None:
        return None
    for api_name in ('Statics', 'Tiles'):
        method = _api_method(api_name, method_name)
        if method is None:
            continue
        try:
            return method(tile_id)
        except:
            pass
    return None


def _tile_id(info):
    for attr in ('StaticID', 'ItemID', 'ID', 'TileID'):
        try:
            value = _as_int(getattr(info, attr))
            if value is not None:
                return value
        except:
            pass
    return None


def _tile_z(info):
    for attr in ('StaticZ', 'Z'):
        try:
            value = _as_int(getattr(info, attr))
            if value is not None:
                return value
        except:
            pass
    return None


def _get_land_name(tile_id):
    value = _call_id_api('GetLandName', tile_id)
    return str(value or '')


def _get_tile_name(tile_id):
    value = _call_id_api('GetTileName', tile_id)
    return str(value or '')


def _has_any(text, words):
    for word in words:
        if word in text:
            return True
    return False


def _terrain_state(name):
    n = str(name or '').lower()
    if not n:
        return 'surface'
    # 'swamp' intentionally excluded: swamp terrain is mineable in UO and borders
    # mountain areas.  Classifying it as water caused mountain tiles to show blue.
    if _has_any(n, ('water', 'sea', 'ocean', 'river', 'lava')):
        return 'water'
    if _has_any(n, ('grass', 'meadow', 'lawn')):
        return 'grass'
    if _has_any(n, ('forest', 'foliage', 'leaf', 'leaves', 'tree', 'jungle')):
        return 'forest'
    if _has_any(n, ('swamp', 'marsh', 'bog', 'sand', 'desert', 'beach')):
        return 'sand'
    if _has_any(n, ('dirt', 'mud', 'soil', 'earth')):
        return 'dirt'
    if _has_any(n, ('rock', 'mountain', 'cliff', 'wall', 'cave')):
        return 'rock'
    if _has_any(n, ('road', 'path', 'pavement', 'paved', 'flagstone', 'cobble', 'brick',
                   'floor', 'tile', 'plank', 'board')):
        return 'path'
    if _has_any(n, ('stone', 'slate', 'granite', 'marble')):
        return 'stone'
    return 'surface'


def _mineable_state(name):
    n = str(name or '').lower()
    if not n:
        return 'cave'
    if _has_any(n, ('mountain', 'rock', 'cliff', 'wall')):
        return 'mountain'
    if 'cave' in n:
        return 'cave'
    if _terrain_state(n) != 'surface':
        return None
    return 'cave'


def _state_label(state):
    labels = {
        'cave': 'Mineable',
        'mountain': 'Mountain wall',
        'rock': 'Rock wall',
        'path': 'Path / road',
        'grass': 'Grass',
        'forest': 'Forest / foliage',
        'dirt': 'Dirt',
        'sand': 'Sand',
        'water': 'Water',
        'stone': 'Stone',
        'blocked': 'Blocked LOS',
        'surface': 'Surface',
    }
    return labels.get(state, state)


def _is_mine_target_state(state):
    return state in _MINE_TARGET_STATES or state in ORE_LABELS


def _is_resource_state(state):
    return _is_mine_target_state(state) or state == 'Depleted'


def _get_land_id(x, y, map_name):
    value = _as_int(_call_map_api('GetLandID', x, y, map_name))
    if value is not None:
        return value

    info = _call_map_api('GetStaticsLandInfo', x, y, map_name)
    if info is not None:
        return _tile_id(info)
    return None


def _get_land_z(x, y, map_name):
    value = _as_int(_call_map_api('GetLandZ', x, y, map_name))
    if value is not None:
        return value

    info = _call_map_api('GetStaticsLandInfo', x, y, map_name)
    if info is not None:
        return _tile_z(info)
    return None


def _get_static_infos(x, y, map_name):
    value = _call_map_api('GetStaticsTileInfo', x, y, map_name)
    if value is None:
        return None
    try:
        return list(value)
    except:
        return []


def _scan_tile(x, y, map_name):
    k = _ckey(x, y, map_name)
    cached = _scan_cache.get(k)
    if cached is not None:
        return cached

    saw_scan = False
    land_id = _get_land_id(x, y, map_name)
    land_z = _get_land_z(x, y, map_name)
    land_name = _get_land_name(land_id)
    if land_id is not None:
        saw_scan = True
        if land_id in MINEABLE_TILES:
            # Always return mineable — default 'cave' when the tile name implies
            # water/swamp/lava.  Those names are misleading: if the ID is in
            # MINEABLE_TILES it IS mineable regardless of terrain name.
            mine_state = _mineable_state(land_name) or 'cave'
            result = (mine_state, 'land', land_id, land_z, land_name)
            _scan_cache[k] = result
            return result

    infos = _get_static_infos(x, y, map_name)
    terrain = _terrain_state(land_name)
    terrain_source = 'land'
    terrain_id = land_id
    terrain_z = land_z
    terrain_name = land_name
    if infos is not None:
        saw_scan = True
        for info in infos:
            sid = _tile_id(info)
            tile_name = _get_tile_name(sid)
            if sid in MINEABLE_TILES:
                z = _tile_z(info)
                if z is None:
                    z = land_z
                # Same as land path — default 'cave' so swamp/lava-named
                # mineable statics don't fall through to a 'water' classification.
                mine_state = _mineable_state(tile_name or land_name) or 'cave'
                result = (mine_state, 'static', sid, z, tile_name)
                _scan_cache[k] = result
                return result
            static_state = _terrain_state(tile_name)
            # Allow a solid static to override BOTH 'surface' (unclassified land)
            # AND 'water' (the land tile under a cliff face is often ocean).
            # In UO, mountain walls are statics placed on top of water land tiles
            # where the cliff extends over the coastline — without this, the whole
            # mountain face shows as blue.
            # We never allow statics to set terrain TO 'water' (water is a land type).
            if terrain in ('surface', 'water') and static_state not in ('surface', 'water'):
                terrain = static_state
                terrain_source = 'static'
                terrain_id = sid
                terrain_z = _tile_z(info)
                terrain_name = tile_name

    if saw_scan:
        result = (terrain, terrain_source, terrain_id, terrain_z, terrain_name)
        # Evict oldest 20 % of entries before the cache grows unbounded.
        if len(_scan_cache) >= _SCAN_CACHE_MAX:
            evict = list(_scan_cache.keys())[:_SCAN_CACHE_MAX // 5]
            for _ek in evict:
                del _scan_cache[_ek]
        _scan_cache[k] = result
        return result
    # API returned nothing — do NOT cache so the next refresh retries.
    # This prevents all tiles from being permanently stuck as 'cave/unscanned'
    # when the Statics API is unavailable or slow to start up.
    return ('cave', 'unscanned', None, None, '')


def cell_state(x, y, map_name):
    if is_blocked_from_here(x, y, map_name):
        return 'blocked'

    cached = cache_get(x, y, map_name)
    if cached:
        return cached
    return _scan_tile(x, y, map_name)[0]


def _target_z(x, y, map_name, fallback_z):
    z = _scan_tile(x, y, map_name)[3]
    if z is not None:
        return z
    z = _get_land_z(x, y, map_name)
    if z is not None:
        return z
    return fallback_z


def _find_mine_target(tx, ty, map_name, fallback_z):
    """Fuzzy mine-target search.

    When tile classification is wrong (e.g. a cliff face whose LAND tile is
    ocean), use raw MINEABLE_TILES ID lookups — which never depend on names —
    to find the nearest actual mineable surface.

    Checks (tx, ty) first (statics before land, so mountain faces win over
    water land tiles), then searches the 8 surrounding tiles.  Returns
    (actual_x, actual_y, actual_z) — callers should use the returned coords
    for both the mine target and set_pending so journal attribution is correct.
    """
    def _mineable_z_at(cx, cy):
        # Check statics first — mountain face statics sit on top of ocean land.
        for info in (_get_static_infos(cx, cy, map_name) or []):
            sid = _tile_id(info)
            if sid is not None and sid in MINEABLE_TILES:
                z = _tile_z(info)
                if z is None:
                    z = _get_land_z(cx, cy, map_name)
                return z if z is not None else fallback_z
        # Then check the land tile itself.
        lid = _get_land_id(cx, cy, map_name)
        if lid is not None and lid in MINEABLE_TILES:
            z = _get_land_z(cx, cy, map_name)
            return z if z is not None else fallback_z
        return None

    # Exact clicked tile first.
    z = _mineable_z_at(tx, ty)
    if z is not None:
        return tx, ty, z

    # Cardinal directions first (most likely location for adjacent mountain face),
    # then diagonals.
    for ddx, ddy in [(0,-1),(0,1),(1,0),(-1,0),(1,-1),(-1,-1),(1,1),(-1,1)]:
        z = _mineable_z_at(tx + ddx, ty + ddy)
        if z is not None:
            return tx + ddx, ty + ddy, z

    # Nothing found nearby — return original position; let the server decide.
    return tx, ty, _target_z(tx, ty, map_name, fallback_z)


# =============================================================================
# Mining action
# =============================================================================

_TOOL_IDS = {0x0E85, 0x0E86, 0x0F39, 0x0F3A, 0x0F3E, 0x0F3F}


def _iter_container_items(container, depth=0, seen=None):
    """Recursively yield every item in a container (handles bags-in-bags)."""
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
    # Check equipped hand slots — same layer names used by mining.py
    for layer in ('LeftHand', 'RightHand'):
        try:
            tool = Player.GetItemOnLayer(layer)
            if tool and tool.ItemID in _TOOL_IDS:
                return tool.Serial
        except:
            pass

    # 2. Search backpack by item name — catches ClusterF custom pickaxes (T1-T5)
    pack = Player.Backpack
    if not pack:
        return None
    try:
        for item in _iter_container_items(pack):
            try:
                name = (item.Name or '').lower()
                if 'pick' in name or 'shovel' in name:
                    # Skip exhausted Jacob's Compact pickaxes — hue 0x0415 = charcoal.
                    try:
                        if item.Hue == 0x0415:
                            continue
                    except:
                        pass
                    return item.Serial
            except:
                pass
    except:
        pass

    # 3. Fallback: vanilla graphic IDs
    for tid in _TOOL_IDS:
        try:
            item = Items.FindByID(tid, -1, pack.Serial)
            if item:
                return item.Serial
        except:
            pass

    return None


def _activate(serial):
    """Double-click an object. Tries every known RE API path, then raw packet."""
    # Path 1: Items.UseItem
    try:
        Items.UseItem(serial)
        return True
    except:
        pass
    # Path 2: Misc.UseObject (some RE builds)
    try:
        Misc.UseObject(serial)
        return True
    except:
        pass
    # Path 3: Player.DoubleClick (some RE builds)
    try:
        Player.DoubleClick(serial)
        return True
    except:
        pass
    # Path 4: raw UO double-click packet 0x06
    try:
        from System import Array, Byte
        raw = struct.pack('>BI', 0x06, serial & 0x7FFFFFFF)
        Misc.SendPacket(Array[Byte]([Byte(b) for b in bytearray(raw)]), True)
        return True
    except:
        pass
    return False


def do_mine(tx, ty, tz, map_name, wait_baseline=None):
    serial = find_tool()
    if not serial:
        Misc.SendMessage('[MineRadar] No pickaxe or shovel found equipped or in backpack.', 0x22)
        return 'failed'
    if not _activate(serial):
        if (_journal_recent_has(_WAIT_MSGS, 10, wait_baseline) or
                _journal_recent_has(_WAIT_MSGS, 10)):
            return 'must_wait'
        Misc.SendMessage('[MineRadar] Cannot activate pickaxe - all API paths failed.', 0x22)
        return 'failed'
    if not Target.WaitForTarget(3000, False):
        if (_journal_recent_has(_WAIT_MSGS, 10, wait_baseline) or
                _journal_recent_has(_WAIT_MSGS, 10)):
            return 'must_wait'
        Misc.SendMessage('[MineRadar] No target cursor - did the tool activate?', 0x22)
        return 'failed'
    Target.TargetExecute(tx, ty, tz)
    return 'ok'


def _path_to(tx, ty, tz, run=True):
    """Start pathfinding toward (tx, ty). Returns True if the call succeeded."""
    try:
        route              = PathFinding.Route()
        route.X            = int(tx)
        route.Y            = int(ty)
        route.Run          = bool(run)
        route.StopIfStuck  = True
        route.IgnoreMobile = True
        route.MaxRetry     = 1
        route.Timeout      = int(WALK_TIMEOUT_S * 1000)
        PathFinding.Go(route)
        return True
    except:
        pass
    try:
        Player.PathFindTo(int(tx), int(ty), int(tz))
        return True
    except:
        return False


def _wait_in_range(tx, ty, required_range):
    """Block until player reaches Chebyshev distance ≤ required_range, or timeout.
    Returns True on arrival."""
    deadline = time.time() + WALK_TIMEOUT_S
    while time.time() < deadline:
        p = _player_pos()
        if p and max(abs(p[0] - tx), abs(p[1] - ty)) <= required_range:
            return True
        time.sleep(0.25)
    p = _player_pos()
    return bool(p and max(abs(p[0] - tx), abs(p[1] - ty)) <= required_range)


# =============================================================================
# Journal processing
# =============================================================================

_grid_dirty   = threading.Event()
_pending      = None
_pending_lock = threading.Lock()

# Hashes of journal lines already processed — prevents double-counting on every tick.
# Bounded to 300 entries; when full we clear (dedup still works because old entries
# won't reappear in the 30-entry window).
_journal_seen      = set()
_JOURNAL_SEEN_MAX  = 300


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


def _journal_entry_text(entry):
    return getattr(entry, 'Text', None) or str(entry)


def _journal_entry_key(entry):
    parts = [_journal_entry_text(entry)]
    for attr in ('Timestamp', 'TimeStamp', 'Time', 'DateTime', 'Serial', 'Name', 'Type', 'Color', 'Hue'):
        try:
            value = getattr(entry, attr)
            if value is not None:
                parts.append(str(value))
        except:
            pass
    return '|'.join(parts)


def _journal_snapshot(count=30):
    try:
        return set(_journal_entry_key(entry) for entry in (Journal.GetJournalEntry(count) or []))
    except:
        return set()


def _journal_recent_has(substrings, count=30, after=None):
    """Return True if any recent journal entry contains any of the given substrings.
    Uses GetJournalEntry — Journal.Search() is unreliable on this shard."""
    try:
        entries = Journal.GetJournalEntry(count)
    except:
        return False
    for entry in entries:
        if after is not None and _journal_entry_key(entry) in after:
            continue
        text = _journal_entry_text(entry).lower()
        for s in substrings:
            if s.lower() in text:
                return True
    return False


def process_journal():
    global _journal_seen
    try:
        entries = Journal.GetJournalEntry(30)
    except:
        return

    for entry in entries:
        text = getattr(entry, 'Text', None) or str(entry)

        # Skip entries we've already handled this session.
        h = hash(text)
        if h in _journal_seen:
            continue
        _journal_seen.add(h)
        if len(_journal_seen) > _JOURNAL_SEEN_MAX:
            _journal_seen = set()

        m = _PAT_DISC.search(text) or _PAT_VEIN.search(text)
        if m:
            ore_key = _resolve_ore(m.group(1))
            if ore_key:
                x, y, map_name = int(m.group(2)), int(m.group(3)), Player.Map
                changed = cache_set(x, y, map_name, ore_key)
                if changed or cache_get(x, y, map_name) == ore_key:
                    marker_record_ore(x, y, map_name, ore_key)
                if changed:
                    _grid_dirty.set()
            continue

        pending = get_pending()

        m = _PAT_DIG.search(text)
        if m and pending:
            ore_key = _resolve_ore(m.group(1))
            if ore_key:
                _session_stats['ore_found'] += 1
                changed = cache_set(pending[0], pending[1], pending[2], ore_key)
                if changed or cache_get(pending[0], pending[1], pending[2]) == ore_key:
                    marker_record_ore(pending[0], pending[1], pending[2], ore_key)
                if changed:
                    _grid_dirty.set()
                clear_pending(pending)
            continue

        if any(p.search(text) for p in _DEPL_PATS) and pending:
            if cache_deplete_bank(pending[0], pending[1], pending[2]):
                _grid_dirty.set()
            clear_pending(pending)
            continue

        if any(p.search(text) for p in _BLOCK_PATS) and pending:
            if block_tile_from_here(pending[0], pending[1], pending[2]):
                _grid_dirty.set()
            clear_pending(pending)

# =============================================================================
# RadarCell — Button with an optional semi-transparent colour overlay
# =============================================================================

_HAZE_DEPLETED = Color.FromArgb(110, 210, 20, 20)   # red haze for depleted tiles
_HAZE_MINING   = Color.FromArgb(80,  255, 200, 0)   # gold pulse while auto-mining
_BANK_EDGE     = Color.FromArgb(235, 235, 235)

_EDGE_TOP    = 1
_EDGE_RIGHT  = 2
_EDGE_BOTTOM = 4
_EDGE_LEFT   = 8


class RadarCell(Button):
    def __init__(self):
        super(RadarCell, self).__init__()
        self._haze = None
        self._bank_edges = 0

    def SetHaze(self, color):
        self._haze = color
        self.Invalidate()

    def SetBankEdges(self, edges):
        if self._bank_edges != edges:
            self._bank_edges = edges
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
        if self._bank_edges:
            p = Pen(_BANK_EDGE, 2)
            if self._bank_edges & _EDGE_TOP:
                e.Graphics.DrawLine(p, 0, 1, self.Width, 1)
            if self._bank_edges & _EDGE_RIGHT:
                e.Graphics.DrawLine(p, self.Width - 2, 0, self.Width - 2, self.Height)
            if self._bank_edges & _EDGE_BOTTOM:
                e.Graphics.DrawLine(p, 0, self.Height - 2, self.Width, self.Height - 2)
            if self._bank_edges & _EDGE_LEFT:
                e.Graphics.DrawLine(p, 1, 0, 1, self.Height)
            p.Dispose()


# =============================================================================
# Radar form
# =============================================================================


class RadarForm(Form):

    def __init__(self):
        super(RadarForm, self).__init__()
        self._cells       = {}
        self._tips        = ToolTip()
        self._last_pos    = (None, None, None)
        self._last_save   = time.time()
        self._dbg_done    = True   # set False to re-enable one-shot debug
        self._auto        = False  # auto-mine tile toggle
        self._chain       = False  # chain-mine session toggle
        self._mining      = False  # prevents overlapping mine threads
        self._chain_count = 0
        # Skill / session tracking (populated after UI thread is live)
        self._start_skill = 0.0
        self._last_skill  = 0.0
        self._gains       = 0
        self._attempts    = 0
        self._build_ui()
        try:
            self._start_skill = float(Player.GetRealSkillValue('Mining'))
            self._last_skill  = self._start_skill
        except:
            pass
        self._tmr = Timer()
        self._tmr.Interval = REFRESH_MS
        self._tmr.Tick    += self._on_tick
        self._tmr.Start()

    def _build_ui(self):
        _GRID_TOP = 36   # leaves room for two 14-px label rows above the grid
        grid_px   = (GRID_DIAG - 1) * CELL_STEP + CELL_DRAW
        btn_y     = _GRID_TOP + grid_px + 4
        legend_y  = btn_y + 24
        form_h    = legend_y + LEGEND_H + 12

        self.Text            = 'ClusterF Mining Radar'
        self.FormBorderStyle = FormBorderStyle.SizableToolWindow
        self.TopMost         = True
        self.BackColor       = Color.FromArgb(18, 18, 18)
        self.Width           = grid_px + 18
        self.Height          = form_h
        self.MinimumSize     = Size(grid_px + 18, form_h)
        self.StartPosition   = FormStartPosition.Manual
        self.Location        = Point(100, 100)

        mono7 = Font('Courier New', 7, FontStyle.Regular)

        # Skill bar
        self._lbl_skill = Label()
        self._lbl_skill.ForeColor = Color.Silver
        self._lbl_skill.BackColor = Color.Transparent
        self._lbl_skill.Font      = mono7
        self._lbl_skill.Location  = Point(4, 4)
        self._lbl_skill.Size      = Size(grid_px + 8, 14)
        self.Controls.Add(self._lbl_skill)

        # Session stats bar
        self._lbl_stats = Label()
        self._lbl_stats.ForeColor = Color.FromArgb(120, 190, 120)
        self._lbl_stats.BackColor = Color.Transparent
        self._lbl_stats.Font      = mono7
        self._lbl_stats.Location  = Point(4, 20)
        self._lbl_stats.Size      = Size(grid_px + 8, 14)
        self.Controls.Add(self._lbl_stats)

        # ── Isometric (UO-radar-aligned) grid ─────────────────────────────────
        # Coordinate transform:  col = dx − dy + 2R,  row = dx + dy + 2R
        # Only cells where (col+row) % 2 == 0 are valid UO-offset positions.
        # The valid cells form a diamond whose edges point:
        #   N → upper-right,  E → lower-right,  S → lower-left,  W → upper-left
        # matching the UO classic-client minimap/radar orientation.
        #
        # Cells are placed at absolute pixel positions col*CELL_STEP, row*CELL_STEP
        # and drawn at size CELL_DRAW × CELL_DRAW.  Because CELL_DRAW > CELL_STEP,
        # UO-adjacent tiles (diagonal screen neighbours) overlap by
        # (CELL_DRAW − CELL_STEP) px at their corners.  This eliminates the large
        # dead-zone gap that a TableLayoutPanel produces between every pair of cells.
        # The only remaining background seam is between UO-diagonal tiles
        # (horizontal/vertical in screen space), which are not mining neighbours.
        # ─────────────────────────────────────────────────────────────────────

        pnl = Panel()
        pnl.Location  = Point(4, _GRID_TOP)
        pnl.Size      = Size(grid_px, grid_px)
        pnl.BackColor = Color.FromArgb(18, 18, 18)

        # Compass labels at dead-zone positions inside the panel
        # (outside the active diamond — |dx|>R or |dy|>R at these grid coords).
        _cmp_font = Font('Arial Narrow', 8, FontStyle.Bold)
        _cmp_fg   = Color.FromArgb(210, 210, 210)
        for (cc, rr), txt in [((20, 2), 'N'), ((22, 20), 'E'),
                               ((4, 22), 'S'), ((2,  4), 'W')]:
            _cl = Label()
            _cl.Text      = txt
            _cl.ForeColor = _cmp_fg
            _cl.BackColor = Color.FromArgb(18, 18, 18)
            _cl.Font      = _cmp_font
            _cl.TextAlign = ContentAlignment.MiddleCenter
            _cl.Size      = Size(CELL_DRAW, CELL_DRAW)
            _cl.Location  = Point(cc * CELL_STEP, rr * CELL_STEP)
            pnl.Controls.Add(_cl)

        # Build the list of active cells then add them in reverse col+row order
        # so cells with a smaller col+row sum (visually "upper-left") end up added
        # last and therefore sit highest in z-order where diagonal cells overlap.
        _cells_to_add = []
        for r in range(GRID_DIAG):
            for c in range(GRID_DIAG):
                if (c + r) % 2 != 0:
                    continue  # dead-zone position — no button placed here
                dx = (c + r - 4 * GRID_RADIUS) // 2
                dy = (r - c) // 2
                if abs(dx) > GRID_RADIUS or abs(dy) > GRID_RADIUS:
                    continue  # outside the active diamond — skip
                _cells_to_add.append((c + r, c, r, dx, dy))

        _cells_to_add.sort(reverse=True)  # largest sum first → added first → lowest z-order

        for _, c, r, dx, dy in _cells_to_add:
            btn = RadarCell()
            btn.Size      = Size(CELL_DRAW, CELL_DRAW)
            btn.Location  = Point(c * CELL_STEP, r * CELL_STEP)
            btn.Margin    = Padding(0)
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 0
            btn.Text      = ''
            btn.Font      = Font('Arial Narrow', 6, FontStyle.Bold)

            def _make_click(adx, ady):
                def _handler(s, e):
                    self._on_cell_click(adx, ady)
                return _handler

            btn.Click += _make_click(dx, dy)
            self._cells[(dx, dy)] = btn
            pnl.Controls.Add(btn)

        self.Controls.Add(pnl)

        legend = [
            ('Mine wall', 'mountain'), ('Rock wall', 'rock'), ('Mineable', 'cave'),
            ('Path', 'path'), ('Grass', 'grass'), ('Forest', 'forest'),
            ('Dirt', 'dirt'), ('Sand', 'sand'), ('Water', 'water'),
            ('Stone', 'stone'), ('! Blocked', 'blocked'),
        ]
        for ore_key in ORE_LABELS:
            legend.append(('{} {}'.format(ORE_LABELS[ore_key], ORE_DISPLAY[ore_key]), ore_key))
        legend.append(('X Depleted', 'Depleted'))

        fp = FlowLayoutPanel()
        fp.Location      = Point(4, legend_y)
        fp.Size          = Size(grid_px + 8, LEGEND_H)
        fp.Anchor        = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
        fp.BackColor     = Color.Transparent
        fp.FlowDirection = FlowDirection.LeftToRight
        fp.WrapContents  = True
        fp.AutoScroll    = True

        for txt, state in legend:
            l = Label()
            l.Text      = txt
            l.BackColor = CELL_BG.get(state, Color.Gray)
            l.ForeColor = Color.White if state in _NEEDS_WHITE else Color.Black
            l.Font      = Font('Arial Narrow', 8, FontStyle.Bold)
            l.TextAlign = ContentAlignment.MiddleCenter
            l.Width     = 102
            l.Height    = 20
            l.Margin    = Padding(2, 1, 2, 1)
            fp.Controls.Add(l)

        self.Controls.Add(fp)

        # ── Button row ────────────────────────────────────────────────────
        def _mkbtn(text, x, w, handler, fg=None, bg=None, border=None):
            b           = Button()
            b.Text      = text
            b.Location  = Point(x, btn_y)
            b.Size      = Size(w, 20)
            b.FlatStyle = FlatStyle.Flat
            b.Font      = Font('Arial Narrow', 7, FontStyle.Bold)
            b.ForeColor = fg     or Color.FromArgb(160, 160, 160)
            b.BackColor = bg     or Color.FromArgb(40, 40, 40)
            b.FlatAppearance.BorderColor = border or Color.FromArgb(80, 80, 80)
            if handler:
                b.Click += handler
            self.Controls.Add(b)
            return b

        # AUTO
        self._btn_auto = _mkbtn('AUTO: OFF', 4, 70, self._on_auto_toggle)
        self._tips.SetToolTip(self._btn_auto,
            'Toggle auto-mine — loops the clicked tile until depleted.')

        # NEXT — mine nearest in-range tile
        _mkbtn('NEXT', 78, 46, self._on_next,
               fg=Color.FromArgb(190, 220, 255),
               border=Color.FromArgb(80, 130, 200))
        self._tips.SetToolTip(self.Controls[self.Controls.Count - 1],
            'Mine the nearest in-range mineable tile once (range {}).'.format(MAX_TARGET_RANGE))

        # CHAIN — mine tile after tile, walking to reach them
        self._btn_chain = _mkbtn('CHAIN', 128, 82, self._on_chain_toggle,
                                  fg=Color.FromArgb(160, 255, 180),
                                  border=Color.FromArgb(60, 180, 90))
        self._tips.SetToolTip(self._btn_chain,
            'Chain-mine: auto-find → walk → mine to depletion, repeat up to {} tiles. '
            'Click again to stop.'.format(CHAIN_DEFAULT))

        # CLR DEP
        _mkbtn('CLR DEP', 214, 62, self._on_clear_depleted,
               fg=Color.FromArgb(220, 130, 130),
               border=Color.FromArgb(160, 60, 60))
        self._tips.SetToolTip(self.Controls[self.Controls.Count - 1],
            'Clear all depleted tiles from cache — forces re-scan of every depleted cell.')

        # CLR SCN
        _mkbtn('CLR SCN', 280, 62, self._on_clear_scan,
               fg=Color.FromArgb(130, 180, 220),
               border=Color.FromArgb(60, 100, 160))
        self._tips.SetToolTip(self.Controls[self.Controls.Count - 1],
            'Clear terrain scan cache — forces re-read of every tile from the Statics API.')

        # SAVE
        _mkbtn('SAVE', 346, 46, self._on_save,
               fg=Color.FromArgb(180, 220, 180),
               border=Color.FromArgb(80, 150, 80))
        self._tips.SetToolTip(self.Controls[self.Controls.Count - 1],
            'Flush cache to disk now.')

        # DBG
        _mkbtn('DBG', grid_px - 44, 48, self._on_debug,
               fg=Color.Yellow, border=Color.Yellow)
        self._tips.SetToolTip(self.Controls[self.Controls.Count - 1],
            'Dump tile + tool debug info to RE chat.')

    def _on_auto_toggle(self, sender, e):
        self._auto = not self._auto
        if self._auto:
            self._btn_auto.Text      = 'AUTO: ON'
            self._btn_auto.ForeColor = Color.FromArgb(80, 255, 80)
            self._btn_auto.BackColor = Color.FromArgb(20, 60, 20)
            self._btn_auto.FlatAppearance.BorderColor = Color.FromArgb(60, 200, 60)
            Misc.SendMessage('[MineRadar] Auto-mine ON — click a tile to mine until depleted.', 0x44)
        else:
            self._btn_auto.Text      = 'AUTO: OFF'
            self._btn_auto.ForeColor = Color.FromArgb(160, 160, 160)
            self._btn_auto.BackColor = Color.FromArgb(40, 40, 40)
            self._btn_auto.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80)
            Misc.SendMessage('[MineRadar] Auto-mine OFF.', 0x44)

    # ------------------------------------------------------------------
    # Thread-safe UI helper + skill/session label update
    # ------------------------------------------------------------------

    def _ui_call(self, callback):
        """Safely invoke callback on the UI thread from any thread."""
        try:
            if self.IsDisposed:
                return
            if self.InvokeRequired:
                self.BeginInvoke(Action(callback))
            else:
                callback()
        except:
            pass

    def _update_labels(self):
        """Refresh skill bar and session stats. Must be called on the UI thread."""
        try:
            real  = float(Player.GetRealSkillValue('Mining'))
            cap   = float(Player.GetSkillCap('Mining'))
            shown = float(Player.GetSkillValue('Mining'))
            if real > self._last_skill + 0.0001:
                self._gains      += 1
                self._last_skill  = real
            ppos = _player_pos()
            coord = '  ({}, {})'.format(ppos[0], ppos[1]) if ppos else ''
            map_s = '  {}'.format(ppos[3]) if ppos else ''
            self._lbl_skill.Text = 'Mining {:.1f}/{:.1f}  shown {:.1f}{}{}'.format(
                real, cap, shown, coord, map_s)
            self._lbl_stats.Text = 'tries {}  ore {}  gains {}  start {:.1f}  chain {}'.format(
                self._attempts, _session_stats['ore_found'],
                self._gains, self._start_skill, self._chain_count)
        except:
            pass

    def _auto_debug(self):
        try:
            # --- Layer probe ---
            for idx in (1, 2):
                try:
                    t = Player.GetItemOnLayer(idx)
                    Misc.SendMessage('[DBG] Layer{}: serial={} id={} name={!r}'.format(
                        idx, t.Serial if t else None,
                        t.ItemID if t else None,
                        t.Name   if t else None), 0x44)
                except Exception as ex:
                    Misc.SendMessage('[DBG] Layer{} ERR: {}'.format(idx, ex), 0x22)

            # --- Backpack probe ---
            pack = Player.Backpack
            if pack is None:
                Misc.SendMessage('[DBG] Backpack is None', 0x22)
                return
            Misc.SendMessage('[DBG] Backpack serial={}'.format(pack.Serial), 0x44)

            # Try pack.Contains
            try:
                count = 0
                for item in pack.Contains:
                    count += 1
                    Misc.SendMessage('[DBG]  item id={} name={!r}'.format(
                        item.ItemID, item.Name), 0x44)
                    if count >= 15:
                        Misc.SendMessage('[DBG]  (stopped at 15)', 0x44)
                        break
                if count == 0:
                    Misc.SendMessage('[DBG] pack.Contains empty', 0x44)
            except Exception as ex:
                Misc.SendMessage('[DBG] pack.Contains ERR: {}'.format(ex), 0x22)

            # Try Items.FindByID with vanilla IDs
            for tid in list(_TOOL_IDS)[:3]:
                try:
                    item = Items.FindByID(tid, -1, pack.Serial)
                    Misc.SendMessage('[DBG] FindByID(0x{:04X}): {}'.format(
                        tid, item.Serial if item else None), 0x44)
                except Exception as ex:
                    Misc.SendMessage('[DBG] FindByID ERR: {}'.format(ex), 0x22)

        except Exception as ex:
            Misc.SendMessage('[DBG] auto_debug ERR: {}'.format(ex), 0x22)

    def _on_debug(self, sender, e):
        try:
            ppos = _player_pos()
            if ppos is None:
                Misc.SendMessage('[DBG] _player_pos() returned None', 0x22)
                return
            px, py, pz, map_name = ppos
            Misc.SendMessage('[DBG] pos=({},{},{}) map={!r}'.format(px, py, pz, map_name), 0x44)
            midx = _map_idx(map_name)
            Misc.SendMessage('[DBG] map_idx={}'.format(midx), 0x44)
            scan = _scan_tile(px, py, map_name)
            Misc.SendMessage('[DBG] scan state={} source={} id={} z={} name={!r}'.format(
                scan[0], scan[1], scan[2], scan[3], scan[4] if len(scan) > 4 else ''), 0x44)
            # Sample neighbours
            for ddx, ddy in [(1,0),(-1,0),(0,1),(0,-1)]:
                tx, ty = px+ddx, py+ddy
                nscan = _scan_tile(tx, ty, map_name)
                Misc.SendMessage('[DBG] ({},{}) state={} source={} id={} z={} name={!r}'.format(
                    tx, ty, nscan[0], nscan[1], nscan[2], nscan[3],
                    nscan[4] if len(nscan) > 4 else ''), 0x44)
        except Exception as ex:
            Misc.SendMessage('[DBG] outer error: {}'.format(ex), 0x22)

    def _on_tick(self, sender, e):
        # One-shot debug dump — fires once, prints tile info to RE chat
        if not self._dbg_done:
            self._dbg_done = True
            self._auto_debug()

        # Journal processing (runs on UI thread — safe for RE API calls)
        try:
            process_journal()
        except:
            pass

        # Auto-clear depleted tiles whose respawn timer has elapsed
        try:
            expire_depletions()
        except:
            pass

        # Periodic cache flush
        now = time.time()
        if now - self._last_save > 30:
            cache_save()
            self._last_save = now

        # Grid refresh when player moves or a new vein is cached
        ppos = _player_pos()
        if ppos is not None:
            px, py, _pz, map_name = ppos
            pos = (px, py, map_name)
            if pos != self._last_pos or _grid_dirty.is_set():
                self._last_pos = pos
                _grid_dirty.clear()
                self._refresh(px, py, map_name)

        self._update_labels()

    def _refresh(self, cx, cy, map_name):
        for (dx, dy), btn in self._cells.items():
            tx, ty  = cx + dx, cy + dy
            is_ctr  = (dx == 0 and dy == 0)

            if is_ctr:
                state, label = 'player', '@'
            else:
                state = cell_state(tx, ty, map_name)
                label = ORE_LABELS.get(state, 'X' if state == 'Depleted' else '!' if state == 'blocked' else '')

            bg = CELL_BG.get(state, CELL_BG['surface'])
            fg = Color.White if (state in _NEEDS_WHITE or is_ctr) else Color.Black

            btn.BackColor = bg
            btn.ForeColor = fg

            if is_ctr or _is_mine_target_state(state):
                btn.Cursor = Cursors.Hand
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                    min(bg.R + 35, 255), min(bg.G + 35, 255), min(bg.B + 35, 255))
            elif state == 'Depleted':
                btn.Cursor = Cursors.Default
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 20, 20)
            elif state in _CLICKABLE_STATES:
                # Ambiguous terrain (water/rock/surface/etc.) — may be a cliff face
                # misclassified over ocean land.  Show Default cursor so the player
                # knows it's clickable; fuzzy search will locate the mine target.
                btn.Cursor = Cursors.Default
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                    min(bg.R + 20, 255), min(bg.G + 20, 255), min(bg.B + 20, 255))
            else:
                btn.Cursor = Cursors.No
                btn.FlatAppearance.MouseOverBackColor = bg

            # Red haze overlay on depleted tiles
            if state == 'Depleted':
                btn.SetHaze(_HAZE_DEPLETED)
            else:
                btn.ClearHaze()

            btn.SetBankEdges(_bank_edges(tx, ty))
            btn.Text = label

            tip = '{}  ({}, {})  bank {}'.format(_compass_offset(dx, dy), tx, ty, _bank_label(tx, ty))
            if state in ORE_LABELS:
                tip += ' - ' + state
            elif state in _MINE_TARGET_STATES:
                scan = _scan_tile(tx, ty, map_name)
                if scan[2] is not None:
                    tip += ' - {} ({} 0x{:04X})'.format(_state_label(state), scan[1], scan[2])
                else:
                    tip += ' - Mineable (scan unavailable)'
                if len(scan) > 4 and scan[4]:
                    tip += ' ' + str(scan[4])
            elif state == 'Depleted':
                tip += ' - Depleted (click to re-test for respawn)'
            elif state == 'blocked':
                tip += ' - Cannot be seen from here; move to retry'
            elif state in _CLICKABLE_STATES:
                tip += ' - {} (click to attempt — may be misclassified cliff/cave)'.format(
                    _state_label(state))
            else:
                scan = _scan_tile(tx, ty, map_name)
                if scan[2] is not None:
                    tip += ' - {} ({} 0x{:04X})'.format(_state_label(state), scan[1], scan[2])
                else:
                    tip += ' - ' + _state_label(state)
                if len(scan) > 4 and scan[4]:
                    tip += ' ' + str(scan[4])
            self._tips.SetToolTip(btn, tip)

    # ------------------------------------------------------------------
    # Core mine loop — shared by AUTO click, CHAIN, and future callers
    # ------------------------------------------------------------------

    _EMPTY_MSGS = [
        'There is no metal here to mine',
        'There is no ore here to mine',
        'You find no metal',
        'No metal here',
    ]
    _STOP_MSGS = [
        'You have worn out your tool',
        'Your backpack is full',
        'Your pack is full',
        'You are overloaded',
        "You can't carry any more",
    ]
    _BLOCK_MSGS = [
        'That cannot be seen',
        'Target cannot be seen',
        'You cannot see that',
        "You can't see that",
        'You cannot see the target',
        "You can't see the target",
    ]

    def _mark_blocked_if_seen(self, tx, ty, map_name):
        if _journal_recent_has(self._BLOCK_MSGS):
            if block_tile_from_here(tx, ty, map_name):
                _grid_dirty.set()
            clear_pending((tx, ty, map_name))
            Misc.SendMessage('[MineRadar] Cannot see that tile from here — skipping until you move.', 0x22)
            return True

        if cell_state(tx, ty, map_name) == 'blocked':
            clear_pending((tx, ty, map_name))
            return True

        return False

    def _mine_until_depleted(self, tx, ty, tz, map_name, should_continue, cell=None):
        """Mine one tile until depleted, stopped, or capped at 60 swings.

        should_continue — zero-arg callable; loop exits when it returns False.
        Returns a string reason: 'depleted' | 'stop' | 'blocked' | 'failed' |
                                  'must_wait' | 'overloaded' | 'cancelled' | 'timeout'
        """
        if cell:
            self._ui_call(lambda: cell.SetHaze(_HAZE_MINING))

        for _ in range(60):
            if not should_continue():
                return 'cancelled'
            try:
                if Player.Weight >= Player.MaxWeight - STOP_WEIGHT_MARGIN:
                    Misc.SendMessage('[MineRadar] Overloaded — stopping.', 0x22)
                    return 'overloaded'
            except:
                pass

            do_mine_failed = False
            swing_ok       = False
            for attempt in range(MAX_WAIT_RETRIES + 1):
                try:
                    Journal.Clear()
                except:
                    pass
                wait_baseline = _journal_snapshot(30)
                set_pending(tx, ty, map_name)
                mine_status = do_mine(tx, ty, tz, map_name, wait_baseline)
                if mine_status != 'ok':
                    clear_pending((tx, ty, map_name))
                    if mine_status == 'must_wait' and attempt < MAX_WAIT_RETRIES:
                        Misc.SendMessage(
                            '[MineRadar] Action timer - waiting before retry ({}/{}).'.format(
                                attempt + 1, MAX_WAIT_RETRIES), 0x44)
                        time.sleep(MUST_WAIT_RETRY_MS / 1000.0)
                        continue
                    if mine_status == 'must_wait':
                        return 'must_wait'
                    do_mine_failed = True
                    break
                time.sleep(MINE_WAIT / 1000.0)
                if _journal_recent_has(_WAIT_MSGS, 30, wait_baseline):
                    clear_pending((tx, ty, map_name))
                    if attempt < MAX_WAIT_RETRIES:
                        Misc.SendMessage(
                            '[MineRadar] Action timer - waiting before retry ({}/{}).'.format(
                                attempt + 1, MAX_WAIT_RETRIES), 0x44)
                        time.sleep(MUST_WAIT_RETRY_MS / 1000.0)
                        continue
                    return 'must_wait'
                _session_stats['swings'] += 1
                self._attempts += 1
                swing_ok = True
                break

            if do_mine_failed:
                return 'failed'
            if not swing_ok:
                return 'must_wait'

            if self._mark_blocked_if_seen(tx, ty, map_name):
                return 'blocked'
            if _journal_recent_has(self._STOP_MSGS):
                return 'stop'

            depleted = (_journal_recent_has(self._EMPTY_MSGS) or
                        cache_get(tx, ty, map_name) == 'Depleted')
            if depleted:
                cache_deplete_bank(tx, ty, map_name)
                return 'depleted'

        return 'timeout'

    def _auto_mine_task(self, tx, ty, tz, map_name, cell):
        try:
            result = self._mine_until_depleted(
                tx, ty, tz, map_name, lambda: self._auto, cell)
            _STOP_REASONS = {
                'stop':       'pack full or tool worn out',
                'must_wait':  'max "must wait" retries exceeded',
                'overloaded': 'overloaded',
                'failed':     'tool activation failed',
            }
            if result in _STOP_REASONS:
                Misc.SendMessage('[MineRadar] Auto stopped: {}.'.format(
                    _STOP_REASONS[result]), 0x22)
        finally:
            self._mining = False
            _grid_dirty.set()

    # ------------------------------------------------------------------

    def _on_clear_depleted(self, sender, e):
        """Flush every 'Depleted' entry from _cache and _depletion_expiry, then redraw."""
        global _dirty
        with _cache_lock:
            keys = [k for k, v in _cache.items() if v == 'Depleted']
            for k in keys:
                del _cache[k]
                _depletion_expiry.pop(k, None)
            if keys:
                _dirty = True
        if keys:
            _grid_dirty.set()
            Misc.SendMessage(
                '[MineRadar] {} depleted tile(s) cleared from cache.'.format(len(keys)), 0x44)
        else:
            Misc.SendMessage('[MineRadar] No depleted tiles to clear.', 0x44)

    def _on_clear_scan(self, sender, e):
        """Wipe the in-memory terrain scan cache so every tile gets re-classified."""
        count = len(_scan_cache)
        _scan_cache.clear()
        _grid_dirty.set()
        Misc.SendMessage(
            '[MineRadar] Terrain scan cache cleared ({} entries). Tiles will re-scan on next refresh.'.format(count),
            0x44)

    # ------------------------------------------------------------------

    # ------------------------------------------------------------------
    # NEXT / CHAIN / SAVE
    # ------------------------------------------------------------------

    def _find_next_target_global(self, px, py, map_name, max_range):
        """Return (dx, dy) of the nearest mineable tile within max_range,
        preferring known-ore tiles over unknown cave tiles. Returns None if empty."""
        best_ore  = None;  best_ore_d  = 9999
        best_cave = None;  best_cave_d = 9999
        for dy in range(-max_range, max_range + 1):
            for dx in range(-max_range, max_range + 1):
                if dx == 0 and dy == 0:
                    continue
                state = cell_state(px + dx, py + dy, map_name)
                dist  = max(abs(dx), abs(dy))
                if state in ORE_LABELS and dist < best_ore_d:
                    best_ore = (dx, dy);  best_ore_d = dist
                elif state in _MINE_TARGET_STATES and dist < best_cave_d:
                    best_cave = (dx, dy);  best_cave_d = dist
        return best_ore or best_cave

    def _on_next(self, sender, e):
        """Mine nearest in-range tile once."""
        if self._mining:
            return
        ppos = _player_pos()
        if ppos is None:
            return
        px, py, pz, map_name = ppos
        target = self._find_next_target_global(px, py, map_name, MAX_TARGET_RANGE)
        if target is None:
            Misc.SendMessage('[MineRadar] NEXT: no mineable tile within range {}.'.format(
                MAX_TARGET_RANGE), 0x22)
            return
        self._on_cell_click(target[0], target[1])

    def _on_save(self, sender, e):
        global _dirty
        _dirty = True
        cache_save()
        Misc.SendMessage('[MineRadar] Cache saved.', 0x44)

    def _on_chain_toggle(self, sender, e):
        if self._chain:
            self._chain = False
            self._btn_chain.Text      = 'CHAIN'
            self._btn_chain.ForeColor = Color.FromArgb(160, 160, 160)
            self._btn_chain.BackColor = Color.FromArgb(40, 40, 40)
            self._btn_chain.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80)
            return
        if self._mining:
            return
        self._chain       = True
        self._chain_count = 0
        self._mining      = True
        self._btn_chain.Text      = 'CHAIN: ON'
        self._btn_chain.ForeColor = Color.FromArgb(80, 255, 80)
        self._btn_chain.BackColor = Color.FromArgb(20, 60, 20)
        self._btn_chain.FlatAppearance.BorderColor = Color.FromArgb(60, 200, 60)
        t = threading.Thread(target=self._chain_worker, args=(CHAIN_DEFAULT,))
        t.daemon = True
        t.start()

    def _chain_worker(self, limit):
        """Find and mine tiles one by one, walking to out-of-range tiles as needed.
        Stops when limit tiles have been depleted or no more targets are visible."""
        lim_str = str(limit) if limit else 'inf'
        found = 0
        try:
            while self._chain and (limit == 0 or found < limit):
                ppos = _player_pos()
                if ppos is None:
                    break
                px, py, pz, map_name = ppos

                # Look out to full radar radius; walk covers the far tiles
                target = self._find_next_target_global(px, py, map_name, GRID_RADIUS)
                if target is None:
                    Misc.SendMessage('[MineRadar] Chain: no more targets visible.', 0x44)
                    break

                dx, dy = target
                tx, ty = px + dx, py + dy
                tz     = _target_z(tx, ty, map_name, pz)
                dist   = max(abs(dx), abs(dy))

                # Walk to tile when out of mining range
                if dist > MAX_TARGET_RANGE:
                    Misc.SendMessage(
                        '[MineRadar] Chain: walking to ({},{})…'.format(tx, ty), 0x44)
                    _path_to(tx, ty, tz, run=True)
                    if not _wait_in_range(tx, ty, MAX_TARGET_RANGE):
                        Misc.SendMessage(
                            '[MineRadar] Chain: could not reach ({},{}), skipping.'.format(
                                tx, ty), 0x22)
                        block_tile_from_here(tx, ty, map_name)
                        _grid_dirty.set()
                        continue
                    ppos2 = _player_pos()
                    if ppos2:
                        pz, map_name = ppos2[2], ppos2[3]
                        tz = _target_z(tx, ty, map_name, pz)
                    # Recalculate dx/dy after walk so haze lands on the right cell
                    dx = tx - ppos2[0] if ppos2 else dx
                    dy = ty - ppos2[1] if ppos2 else dy

                cell   = self._cells.get((dx, dy))
                result = self._mine_until_depleted(
                    tx, ty, tz, map_name, lambda: self._chain, cell)

                if result in ('stop', 'overloaded', 'failed'):
                    Misc.SendMessage('[MineRadar] Chain stopped: {}.'.format(result), 0x22)
                    break

                found += 1
                n, s = found, lim_str
                def _upd(nn=n, ss=s):
                    try:
                        self._btn_chain.Text = 'CHAIN {}/{}'.format(nn, ss)
                    except:
                        pass
                self._ui_call(_upd)
                Misc.SendMessage(
                    '[MineRadar] Chain: tile {} done ({}).'.format(found, result), 0x44)
        finally:
            self._chain  = False
            self._mining = False
            self._chain_count = found
            def _done():
                try:
                    self._btn_chain.Text      = 'CHAIN'
                    self._btn_chain.ForeColor = Color.FromArgb(160, 160, 160)
                    self._btn_chain.BackColor = Color.FromArgb(40, 40, 40)
                    self._btn_chain.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80)
                except:
                    pass
            self._ui_call(_done)
            _grid_dirty.set()
            Misc.SendMessage(
                '[MineRadar] Chain complete — {} tile(s) mined.'.format(found), 0x44)

    def _on_cell_click(self, dx, dy):
        if self._mining:
            return   # ignore clicks while a mine thread is active

        ppos = _player_pos()
        if ppos is None:
            Misc.SendMessage('[MineRadar] Player position unavailable.', 0x22)
            return
        px, py, pz, map_name = ppos
        tx, ty = px + dx, py + dy

        state = cell_state(tx, ty, map_name)
        # Refuse clearly non-mineable terrain (open grass, forest, stone paths).
        # Water, rock, surface, dirt etc. are allowed through: tile classification
        # is unreliable for cliff-face areas where the land tile is ocean.
        # _find_mine_target() will locate the actual mineable static nearby.
        if state not in _CLICKABLE_STATES:
            return

        # Fuzzy target search: check (tx,ty) statics first (mountain face over
        # ocean land), then immediate neighbours.  Returns the best actual
        # mineable coordinate — may differ from (tx, ty) — along with its Z.
        tx, ty, tz = _find_mine_target(tx, ty, map_name, pz)

        cell = self._cells.get((dx, dy))
        self._mining = True

        if self._auto:
            t = threading.Thread(target=self._auto_mine_task,
                                 args=(tx, ty, tz, map_name, cell))
        else:
            def _single_mine():
                try:
                    for attempt in range(MAX_WAIT_RETRIES + 1):
                        try:
                            Journal.Clear()
                        except:
                            pass
                        wait_baseline = _journal_snapshot(30)
                        set_pending(tx, ty, map_name)
                        mine_status = do_mine(tx, ty, tz, map_name, wait_baseline)
                        if mine_status != 'ok':
                            clear_pending((tx, ty, map_name))
                            if mine_status == 'must_wait' and attempt < MAX_WAIT_RETRIES:
                                Misc.SendMessage(
                                    '[MineRadar] Action timer - waiting before retry ({}/{}).'.format(
                                        attempt + 1, MAX_WAIT_RETRIES), 0x44)
                                time.sleep(MUST_WAIT_RETRY_MS / 1000.0)
                                continue
                            break
                        time.sleep(MINE_WAIT / 1000.0)
                        if not _journal_recent_has(_WAIT_MSGS, 30, wait_baseline):
                            break
                        clear_pending((tx, ty, map_name))
                        if attempt < MAX_WAIT_RETRIES:
                            Misc.SendMessage(
                                '[MineRadar] Action timer - waiting before retry ({}/{}).'.format(
                                    attempt + 1, MAX_WAIT_RETRIES), 0x44)
                            time.sleep(MUST_WAIT_RETRY_MS / 1000.0)
                    self._mark_blocked_if_seen(tx, ty, map_name)
                finally:
                    self._mining = False
                    _grid_dirty.set()
            t = threading.Thread(target=_single_mine)

        t.daemon = True
        t.start()

# =============================================================================
# Entry point
# =============================================================================


def main():
    Misc.SendMessage('[MineRadar] Starting...', 0x44)
    cache_load()
    marker_export_cache()

    Application.EnableVisualStyles()
    form = RadarForm()
    Misc.SendMessage('[MineRadar] Window open. Dark-gray cells = uncharted mineable tiles.', 0x44)
    Application.Run(form)   # blocks here; message pump runs until form is closed
    cache_save()
    Misc.SendMessage('[MineRadar] Closed.', 0x44)


main()
