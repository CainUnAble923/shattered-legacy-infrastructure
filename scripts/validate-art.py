#!/usr/bin/env python3
r"""
validate-art.py: check creature animation art before it is imported or registered (cc-P134).

Adapted from vseUOAware by ghostbyte420, GPL-3.0 (vseUtilities commit e4b1d243, D:\UO\third-party\code\vseutilities:
Services\AnimationCatalog.cs, MobTypesReader.cs, BodyConverterReader.cs, AnimationReader.cs,
ClassicAnimationReader.cs, HuesReader.cs, Tools\AnimationTools.cs). This file is GPL-3.0 too.

    python scripts/validate-art.py bodies [--all]                   every body the client data names or draws
    python scripts/validate-art.py actions 132                      which of the 80 actions body 132 has frames for
    python scripts/validate-art.py check-vd Kirin.vd 132            is body 132 a free, matching slot for this .vd
    python scripts/validate-art.py check-vd X.vd 2100 --file 7 --slot 1 [--type monster] [--flags 1004]
                                                                    the same, for a planned Bodyconv/mobtypes line
    python scripts/validate-art.py propose A.vd B.vd ...            a free server body and anim7 slot for each
    python scripts/validate-art.py check-registry                   registry.csv rows that collide
    python scripts/validate-art.py hues-diff a\hues.mul b\hues.mul  per-hue differences, report only
    python scripts/validate-art.py render 132 0 2 --out DIR         one body/action/direction to PNGs
    python scripts/validate-art.py render --vd X.vd 0 2 --out DIR   the same, from a .vd before import

How the client finds a body's frames. TazUO is the client we ship, so its loader is the authority here, not
vseUOAware's (which follows an older client and differs in three places, listed at the end):
  1. mobtypes.txt  "<body> <TYPE> <hex flags>". Flag 0x10000 sends the body to AnimationFrame*.uop
                   (build/animationlegacyframe/<body:06>/<action:02>.bin), and the rest is skipped.
                   AnimationsLoader.Load (mobtypes), GetIndices.
  2. Bodyconv.def  "<body> <anim2> <anim3> <anim4> <anim5> [<anim6> ...]": column i sends the body to slot S of
                   anim(i+1).mul. Every column that is not -1 and whose file exists is applied in turn, so the LAST
                   one wins. AnimationsLoader.ProcessBodyConvDef. Our package adds "N -1 -1 -1 -1 -1 S" lines (anim7,
                   our sl-anim7.bin) for records.json's equipAnims, and (cc-P139) a Bodyconv line and a mobtypes
                   entry for each of its bodyAnims (player-package\payload\app\Build-UoOverrides.ps1).
  3. The layout:   the mobtypes TYPE, or with no entry the slot number's range in that file (CalculateTypeByGraphic:
                   anim2 <200 monster else animal; anim3 <300 animal, <400 monster, else human; every other file
                   <200 monster, <400 animal, else human), then the flags (CalculateOffset):
                     monster   high, unless 0x400 (people) or 0x40 (low)
                     sea       high offset, 13 actions read
                     animal    low, unless 0x20 is set, then 0x400 people, 0x40 low, else high
                     human, equipment  people
                   high = slot x 110 records (22 actions), low = 22000 + (slot - 200) x 65 (13), people = 35000 +
                   (slot - 400) x 175 (35); 5 records an action (directions 0-4), 12 bytes a record (pos, size, extra).
  Other flags: 0x4 idles on action 8 when the body has it (a hover idle; MobileAnimation.GetGroupForAnimation),
  0x8 flies on action 19 when running. The server's own type for a body is pinned Distribution\Data\bodyTable.cfg
  (Server\Mobiles\Body.cs:42-72, bodies below 0x1000).

A .vd file (UOFiddler's export): int16 6, int16 layout (0 high: 110 records, 1 low: 65, 2 people: 175), then that many
12-byte records (offset into the .vd, length, extra; -1 empty), then each record's data as in anim*.mul: 256 palette
colours, an int32 frame count, int32 frame offsets from the count, then the frames.

Reads EA's files and our package's, writes nothing except render's PNGs (to --out). Python 3 with numpy; Pillow for
render. ASCII only.

Where this differs from vseUOAware, on purpose (each checked against the client data, cc-P134 notes):
  - Bodyconv.def: vseUOAware takes the FIRST column that is not -1; TazUO applies each in turn, so the last wins.
  - Layout: vseUOAware picks high/low/people from the in-file slot number only and ignores mobtypes. The kirin
    (body 132, ANIMAL 0, Bodyconv anim2 slot 68) is where it matters: its frames are at anim2.idx record 13420 (the
    low layout), and vseUOAware probes 7480 (high) and finds none.
  - anim4 to anim6: TazUO uses the default ranges; vseUOAware's anim5 "body 34" exception is not in TazUO.
"""
import argparse, csv, importlib.util, json, os, re, struct, sys

import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_DATA = os.path.join(REPO, 'client-data', 'classic-client-7.0.117.0')
ART = os.path.join(REPO, 'player-package', 'vendor', 'shattered-legacy-art')
REGISTRY = os.path.join(ART, 'registry.csv')
RECORDS = os.path.join(ART, 'records.json')
DEFAULT_PINNED = r'D:\UO\ModernUO-pinned'

TYPES = ('monster', 'sea_monster', 'animal', 'human', 'equipment')
GROUP_RECORDS = {'high': 110, 'low': 65, 'people': 175}
VD_LAYOUTS = {0: 'high', 1: 'low', 2: 'people'}
UOP_FLAG, PEOPLE_FLAG, LOW_FLAG, LOW_EXT_FLAG, IDLE8_FLAG, FLY_FLAG = 0x10000, 0x400, 0x40, 0x20, 0x4, 0x8
MAX_ACTIONS = 80
MAX_BODY = 0x1000           # Server\Mobiles\Body.cs:49 (m_Types = new BodyType[0x1000])
OUR_FILE = 6                # anim7.mul: our sl-anim7.bin, handed to TazUO by the override list
DEFAULT_BODY_FROM = 0x800   # proposals start past EA's bodies (the highest named today is below 0x800)

# find-free-art.py's UOP reader and .def tokeniser, so the two scripts read the files the same way.
_spec = importlib.util.spec_from_file_location('find_free_art', os.path.join(os.path.dirname(__file__), 'find-free-art.py'))
ffa = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ffa)


def file_name(i):
    """File index (TazUO's _files index) to its name: 0 anim, 1 anim2, ..."""
    return 'anim' if i == 0 else 'anim%d' % (i + 1)


# --- the definition files ----------------------------------------------------------------------------
def read_mobtypes(path):
    """body -> (type index, flags, where: "mobtypes.txt:<line>"), as AnimationsLoader.Load reads mobtypes.txt."""
    out = {}
    if not os.path.exists(path):
        return out
    for n, raw in enumerate(open(path, 'r', encoding='latin1'), 1):
        line = raw.strip()
        if not line or not line[0].isdigit():
            continue
        parts = line.split()
        if len(parts) < 3:
            continue
        flag = parts[2]
        c = flag.find('#')
        if c == 0:
            continue
        if c > 0:
            flag = flag[:c]
        try:
            body, flags = int(parts[0]), int(flag.strip(), 16)
        except ValueError:
            continue
        t = parts[1].lower()
        if t in TYPES:
            out[body] = (TYPES.index(t), flags, 'mobtypes.txt:%d' % n)
    return out


def read_bodyconv(path, files_present, extra=()):
    """body -> (file index, slot, where): the last applicable column of the last line naming the body, as
    ProcessBodyConvDef leaves _bodyConvInfos. extra: (body, file index, slot, where) lines appended after the file."""
    out = {}
    lines = [(toks, '%s:%d' % (os.path.basename(path), n)) for n, toks in ffa.def_lines(path)]
    for body, fi, slot, where in extra:
        toks = [body] + [-1] * (fi - 1) + [slot]
        lines.append((toks, where))
    for toks, where in lines:
        body = toks[0]
        for i in range(1, len(toks)):
            if toks[i] >= 0 and i in files_present:
                out[body] = (i, toks[i], where)
    return out


def type_by_graphic(slot, fi):
    if fi == 1:
        return 0 if slot < 200 else 2
    if fi == 2:
        return 2 if slot < 300 else (0 if slot < 400 else 3)
    return 0 if slot < 200 else (2 if slot < 400 else 3)


def layout(type_idx, flags):
    """(group whose offset is used, actions read): CalculateOffset."""
    t = TYPES[type_idx]
    if t == 'monster':
        g = 'people' if flags & PEOPLE_FLAG else 'low' if flags & LOW_FLAG else 'high'
    elif t == 'sea_monster':
        return 'high', 13
    elif t == 'animal':
        if flags & LOW_EXT_FLAG:
            g = 'people' if flags & PEOPLE_FLAG else 'low' if flags & LOW_FLAG else 'high'
        else:
            g = 'low'
    else:
        g = 'people'
    return g, GROUP_RECORDS[g] // 5


def group_start(group, slot):
    return {'high': slot * 110, 'low': 22000 + (slot - 200) * 65, 'people': 35000 + (slot - 400) * 175}[group]


def region_of(record):
    return 'high' if record < 22000 else 'low' if record < 35000 else 'people'


def read_registry(path):
    rows = []
    if os.path.exists(path):
        with open(path, newline='', encoding='ascii') as f:
            for n, r in enumerate(csv.DictReader(line for line in f if not line.startswith('#')), 1):
                r['kind'] = (r.get('kind') or 'art').strip()
                r['id'] = int(r['id'])
                rows.append(r)
    return rows


def read_body_table(pinned):
    """Server body -> BodyType name, from pinned Distribution\\Data\\bodyTable.cfg (Body.cs:42-72)."""
    out = {}
    p = os.path.join(pinned, 'Distribution', 'Data', 'bodyTable.cfg')
    if os.path.exists(p):
        for line in open(p, 'r', encoding='latin1'):
            if not line.strip() or line.startswith('#'):
                continue
            s = line.rstrip('\r\n').split('\t')
            if len(s) >= 2 and s[0].strip().isdigit():
                out[int(s[0])] = s[1].strip()
    return out


# --- .vd files ---------------------------------------------------------------------------------------
class Vd:
    def __init__(self, path):
        self.path = path
        self.d = open(path, 'rb').read()
        if len(self.d) < 4:
            raise ValueError('%s: too short for a .vd header' % path)
        magic, lay = struct.unpack_from('<hh', self.d, 0)
        if magic != 6 or lay not in VD_LAYOUTS:
            raise ValueError('%s: not a UOFiddler .vd (header %d, layout %d)' % (path, magic, lay))
        self.group = VD_LAYOUTS[lay]
        self.count = GROUP_RECORDS[self.group]
        if 4 + 12 * self.count > len(self.d):
            raise ValueError('%s: the %s record table runs past the end of the file' % (path, self.group))
        self.records = [struct.unpack_from('<iii', self.d, 4 + 12 * i) for i in range(self.count)]
        self.warnings = []
        for i, (o, l, _e) in enumerate(self.records):
            if o >= 0 and l > 0:
                if o + l > len(self.d):
                    self.warnings.append('record %d (action %d dir %d) runs past the end of the file' % (i, i // 5, i % 5))
                elif l < 516:
                    self.warnings.append('record %d is %d bytes, too short for a palette and frame table' % (i, l))
                else:
                    fc = struct.unpack_from('<i', self.d, o + 512)[0]
                    if not 0 < fc <= 512:
                        self.warnings.append('record %d has an implausible frame count %d' % (i, fc))

    def used(self):
        return [i for i, (o, l, _e) in enumerate(self.records) if o >= 0 and l > 0]

    def actions(self):
        return sorted({i // 5 for i in self.used()})

    def record_data(self, i):
        o, l, _e = self.records[i]
        return self.d[o:o + l] if o >= 0 and l > 0 else None


# --- the client --------------------------------------------------------------------------------------
class Client:
    def __init__(self, data, overrides=True, pinned=DEFAULT_PINNED, records=RECORDS):
        self.data = data
        self.files = {}     # file index -> (idx records as an (n, 3) array, idx path, mul path)
        for i in range(10):
            idx, mul = self._find(file_name(i) + '.idx'), self._find(file_name(i) + '.mul')
            if idx and mul:
                self.files[i] = (np.fromfile(idx, '<i4').reshape(-1, 3), idx, mul)
        extra = []
        self.equip, self.bodies = [], []
        if overrides and os.path.exists(records):
            j = json.load(open(records, encoding='utf-8'))
            self.equip = j.get('equipAnims', [])
            self.bodies = j.get('bodyAnims', [])         # cc-P139: creature bodies, a .vd each, into anim7 too
            here = os.path.dirname(records)
            # cc-P140: the generated files live in build\ (git-ignored); beside records.json is the older place.
            where = os.path.join(here, 'build') if os.path.exists(os.path.join(here, 'build', 'sl-anim7-idx.bin')) else here
            sl_idx, sl_mul = os.path.join(where, 'sl-anim7-idx.bin'), os.path.join(where, 'sl-anim7.bin')
            if (self.equip or self.bodies) and os.path.exists(sl_idx) and os.path.exists(sl_mul) and OUR_FILE not in self.files:
                self.files[OUR_FILE] = (np.fromfile(sl_idx, '<i4').reshape(-1, 3), sl_idx, sl_mul)
            extra = [(int(e['anim']), OUR_FILE, int(e['slot']), 'records.json equipAnims') for e in self.equip]
            extra += [(int(b['body']), OUR_FILE, int(b['slot']), 'records.json bodyAnims') for b in self.bodies]
        self.mobtypes = read_mobtypes(self._find('mobtypes.txt') or os.path.join(data, 'mobtypes.txt'))
        # The launcher adds "body TYPE flags" for each creature body to its copy of mobtypes.txt (Build-UoOverrides.ps1),
        # unless the player's file already names the body (then it skips the body, and the file's line stands).
        for b in self.bodies:
            tn = str(b.get('type', '')).lower()
            if tn in TYPES and int(b['body']) not in self.mobtypes:
                self.mobtypes[int(b['body'])] = (TYPES.index(tn), int(b.get('flags', 0)), 'records.json bodyAnims')
        self.bodyconv_path = self._find('Bodyconv.def') or os.path.join(data, 'Bodyconv.def')
        self.bodyconv = read_bodyconv(self.bodyconv_path, set(self.files), extra)
        self.named = ffa.anim_defs(data)        # mobtypes, Body.def, Corpse.def, Bodyconv.def, Equipconv.def
        self.uops = [(f, ffa.Uop(os.path.join(data, f))) for f in sorted(os.listdir(data))
                     if re.fullmatch(r'AnimationFrame\d+\.uop', f, re.I)]
        self.body_table = read_body_table(pinned)

    def redirects(self, body):
        """{file: ([(line, new body)] where body is redirected, {bodies drawn as body})} for Body.def and Corpse.def
        ("<orig> {<new>} <hue>"; ProcessBodyDef takes the third of a group of three, else the first)."""
        if not hasattr(self, '_redir'):
            self._redir = {}
            for f in ('Body.def', 'Corpse.def'):
                for n, toks in ffa.def_lines(os.path.join(self.data, f)):
                    if len(toks) < 2:
                        continue
                    grp = toks[1:-1] if len(toks) > 2 else toks[1:]
                    to = grp[2] if len(grp) >= 3 else grp[0]
                    self._redir.setdefault((f, toks[0]), [[], set()])[0].append((n, to))
                    self._redir.setdefault((f, to), [[], set()])[1].add(toks[0])
        out = {}
        for f in ('Body.def', 'Corpse.def'):
            v = self._redir.get((f, body))
            if v:
                out[f] = (v[0], v[1])
        return out

    def _find(self, name):
        p = os.path.join(self.data, name)
        if os.path.exists(p):
            return p
        for f in os.listdir(self.data):
            if f.lower() == name.lower():
                return os.path.join(self.data, f)
        return None

    def resolve(self, body, mob=None, conv=None):
        """How the client reads body: a dict. mob (type index, flags) and conv (file index, slot) replace the
        files' own lines, for a planned import."""
        m = mob if mob is not None else self.mobtypes.get(body, (None, 0, None))[:2]
        type_idx, flags = m
        r = {'body': body, 'type': type_idx, 'flags': flags}
        if flags & UOP_FLAG:
            r.update(format='uop', file=None, slot=body, group=None, actions_read=MAX_ACTIONS)
            return r
        fi, slot = conv if conv is not None else self.bodyconv.get(body, (0, body, None))[:2]
        t = type_idx if type_idx is not None else type_by_graphic(slot, fi)
        group, n = layout(t, flags)
        start = group_start(group, slot)
        r.update(format='classic', file=fi, slot=slot, group=group, layout_type=t, actions_read=n,
                 start=start, end=start + n * 5)
        return r

    def records(self, fi, start, end):
        if fi not in self.files or start < 0:
            return None
        a = self.files[fi][0]
        if end > len(a):
            return None         # the client returns no frames when the block runs past the idx (GetIndices)
        return a[start:end]

    def classic_actions(self, r):
        blk = self.records(r['file'], r['start'], r['end'])
        if blk is None:
            return []
        used = (blk[:, 0] != -1) & (blk[:, 1] > 0)
        return sorted({i // 5 for i in np.nonzero(used)[0].tolist()})

    def uop_actions(self, body):
        found = {}
        for a in range(MAX_ACTIONS):
            name = 'build/animationlegacyframe/%06d/%02d.bin' % (body, a)
            for f, u in self.uops:
                if u.has(name):
                    found[a] = f
                    break
        return found

    def actions(self, body):
        r = self.resolve(body)
        if r['format'] == 'uop':
            return r, sorted(self.uop_actions(body))
        return r, self.classic_actions(r)

    def source(self, r):
        if r['format'] == 'uop':
            return 'AnimationFrame*.uop'
        if r['file'] in self.files:
            return os.path.basename(self.files[r['file']][2])
        return file_name(r['file']) + '.mul (missing)'

    def readers(self, fi, start, end, skip=()):
        """Bodies the client would send into records [start, end) of file fi: (body, start, end, why named)."""
        out = []
        for b in sorted(set(range(MAX_BODY)) | set(self.bodyconv) | set(self.mobtypes)):
            if b in skip:
                continue
            conv = self.bodyconv.get(b)
            if fi == 0 and conv is None:
                pass
            elif conv is None or conv[0] != fi:
                continue
            r = self.resolve(b)
            if r['format'] != 'classic' or r['file'] != fi:
                continue
            if r['start'] < end and start < r['end']:
                why = []
                if b in self.mobtypes:
                    why.append(self.mobtypes[b][2])
                if conv:
                    why.append(conv[2])
                if b in self.body_table:
                    why.append('bodyTable.cfg %s' % self.body_table[b])
                out.append((b, r['start'], r['end'], why))
        return out


# --- checks ------------------------------------------------------------------------------------------
def needed_mobtypes(vd_group, actions, slot, fi, flags=0):
    """(type name, flags, needed?, note): what mobtypes.txt must say so the client reads a block of vd_group at
    this slot, with this file's default type as the fallback."""
    want = {'high': 'monster', 'low': 'animal', 'people': 'human'}[vd_group]
    note = ''
    if vd_group == 'high' and actions and max(actions) < 13:
        note = ('only actions 0-12: a monster, or an animal (a mount) cut into a high block, as the kirin. An animal '
                'needs ANIMAL 0, and the client then reads a low block (13 actions) at the slot low-layout offset, '
                'so the frames must be written there, not at slot x 110. Render it to tell.')
    default = TYPES[type_by_graphic(slot, fi)]
    need = default != want or flags != 0
    return want.upper(), flags, need, note


def check_vd(c, vd, body, conv=None, mob=None, label=None):
    """Report lines and a verdict for importing vd as server body. conv/mob: planned Bodyconv/mobtypes values."""
    out, problems = [], []
    acts = vd.actions()
    out.append('%s: %s layout, %d records, %d with frames, actions %s'
               % (label or vd.path, vd.group, vd.count, len(vd.used()), compact(acts)))
    for w in vd.warnings:
        problems.append('vd: ' + w)
    if not 0 <= body < MAX_BODY:
        problems.append('body: %d is outside 0..%d (Body.cs:49)' % (body, MAX_BODY - 1))
        return out, problems
    planned = conv is not None
    cur = c.resolve(body)
    # Free: nothing of EA's or ours names it or draws it today.
    if body in c.bodyconv and not planned:
        b = c.bodyconv[body]
        out.append('bodyconv: %s sends it to %s slot %d' % (b[2], file_name(b[0]), b[1]))
    elif body in c.bodyconv:
        b = c.bodyconv[body]
        problems.append('bodyconv: already has a line (%s, %s slot %d)' % (b[2], file_name(b[0]), b[1]))
    if body in c.mobtypes:
        t, f, n = c.mobtypes[body]
        if planned:
            problems.append('mobtypes: already named; %s says %s %X' % (n, TYPES[t].upper(), f))
    for f, (first, drawn) in c.redirects(body).items():
        # Body.def/Corpse.def apply only to a classic body left in anim.mul (Animation.cs:250-254 in TazUO).
        tgt = c.resolve(body, mob=mob, conv=conv)
        for n, to in first:
            if tgt['format'] == 'classic' and tgt['file'] == 0:
                problems.append('defs: %s:%d draws body %d as body %d' % (f, n, body, to))
            else:
                out.append('defs: %s:%d (body %d as %d) does not apply: the body is not in anim.mul' % (f, n, body, to))
        if drawn:
            out.append('defs: %s draws %s with the frames of this body (a replacement changes them too)'
                       % (f, compact(sorted(drawn))))
    for w in c.named.get(body, []):
        if w.startswith('Equipconv'):
            problems.append('defs: named in %s' % w)
    if cur['format'] == 'uop' or planned:
        u = c.uop_actions(body)
        if u:
            problems.append('uop: %s has frames for actions %s' % (sorted(set(u.values()))[0], compact(sorted(u))))
    if cur['format'] == 'classic':
        a = c.classic_actions(cur)
        if a:
            problems.append('frames: %s slot %d (%s) already has actions %s'
                            % (file_name(cur['file']), cur['slot'], cur['group'], compact(a)))
    if body in c.body_table:
        out.append('server: bodyTable.cfg says %s' % c.body_table[body])
    # Where it lands, and whether the block fits there.
    r = c.resolve(body, mob=mob, conv=conv) if planned or mob else cur
    if r['format'] == 'uop':
        problems.append('layout: mobtypes sends body %d to the UOP files; a .vd goes in a classic anim file' % body)
        return out, problems
    out.append('lands: %s slot %d, read as %s (%s layout, %d actions) at records %d-%d'
               % (file_name(r['file']), r['slot'], TYPES[r['layout_type']].upper(), r['group'], r['actions_read'],
                  r['start'], r['end'] - 1))
    if r['file'] not in c.files:
        problems.append('file: %s.idx/.mul not found' % file_name(r['file']))
    if planned:
        blk = c.records(r['file'], r['start'], r['end'])
        if blk is not None:
            used = sorted({i // 5 for i in np.nonzero((blk[:, 0] != -1) & (blk[:, 1] > 0))[0].tolist()})
            if used:
                problems.append('frames: %s records %d-%d already hold actions %s'
                                % (file_name(r['file']), r['start'], r['end'] - 1, compact(used)))
    if r['group'] != vd.group:
        if vd.group == 'high' and r['group'] == 'low' and acts and max(acts) < 13:
            out.append('layout: high .vd with only animal actions, read as a low block: fits (the kirin case)')
        else:
            problems.append('layout: the .vd is a %s block but the client reads a %s block here' % (vd.group, r['group']))
    if acts and max(acts) >= r['actions_read']:
        problems.append('layout: the .vd has action %d, the client reads only %d actions here' % (max(acts), r['actions_read']))
    reg = region_of(r['start'])
    if reg != r['group'] or region_of(r['end'] - 1) != reg:
        out.append('range: records %d-%d lie in the %s range of %s (the %s layout read across ranges, as the kirin)'
                   % (r['start'], r['end'] - 1, reg, file_name(r['file']), r['group']))
    for b, s, e, why in c.readers(r['file'], r['start'], r['end'], skip={body}):
        if why or c.classic_actions(c.resolve(b)):
            problems.append('overlap: body %d also reads %s records %d-%d (%s)'
                            % (b, file_name(r['file']), s, e - 1, ', '.join(why) or 'has frames'))
    t, f, need, note = needed_mobtypes(vd.group, acts, r['slot'], r['file'], (mob or (0, 0))[1])
    if mob is not None:
        out.append('mobtypes: planned %d %s %X' % (body, TYPES[mob[0]].upper(), mob[1]))
    elif body in c.mobtypes and not planned:
        out.append('mobtypes: keeps %s (%s %X)' % (c.mobtypes[body][2], TYPES[c.mobtypes[body][0]].upper(),
                                                              c.mobtypes[body][1]))
    elif need:
        out.append('mobtypes: needs "%d %s %X"' % (body, t, f))
    else:
        out.append('mobtypes: none needed (%s is the default for slot %d of %s); "%d %s 0" says it explicitly'
                   % (t, r['slot'], file_name(r['file']), body, t))
    if note:
        out.append('note: ' + note)
    if 8 in acts and vd.group == 'high':
        out.append('note: has action 8; if that is a hover idle the body needs flag 0x4 (IdleAt8Frame)')
    return out, problems


def compact(nums):
    if not nums:
        return 'none'
    out, s = [], nums[0]
    for a, b in zip(nums, nums[1:] + [None]):
        if b != a + 1:
            out.append(str(s) if s == a else '%d-%d' % (s, a))
            s = b
    return ','.join(out)


def free_body(c, start, taken, registry_ids):
    for b in range(start, MAX_BODY):
        if b in taken or b in registry_ids or b in c.named or b in c.mobtypes or b in c.bodyconv or b in c.body_table:
            continue
        r = c.resolve(b)
        if c.classic_actions(r) or c.uop_actions(b):
            continue
        return b
    return None


def free_slot(c, group, taken):
    lo, hi = {'high': (1, 199), 'low': (200, 399), 'people': (400, 2047)}[group]
    used = {s for (fi, s, _w) in c.bodyconv.values() if fi == OUR_FILE}
    for s in range(lo, hi + 1):
        if s in used or (group, s) in taken:
            continue
        start = group_start(group, s)
        blk = c.records(OUR_FILE, start, start + GROUP_RECORDS[group])
        if blk is not None and ((blk[:, 0] != -1) & (blk[:, 1] > 0)).any():
            continue
        return s
    return None


# --- hues --------------------------------------------------------------------------------------------
HUE_ENTRY = 32 * 2 + 2 + 2 + 20
HUE_GROUP = 4 + 8 * HUE_ENTRY


def read_hues(path):
    """[(hue id from 1, colours (32 raw), start, end, name)], as HuesReader.ReadAll."""
    d = open(path, 'rb').read()
    out = []
    for g in range(len(d) // HUE_GROUP):
        for k in range(8):
            o = g * HUE_GROUP + 4 + k * HUE_ENTRY
            cols = struct.unpack_from('<32H', d, o)
            s, e = struct.unpack_from('<HH', d, o + 64)
            name = ''.join(chr(ch) if 32 <= ch < 127 else '?' for ch in d[o + 68:o + 88].split(bytes(1))[0])
            out.append((len(out) + 1, cols, s, e, name))
    return out, len(d)


def hues_diff(a, b, limit):
    ha, la = read_hues(a)
    hb, lb = read_hues(b)
    lines = ['%s: %d bytes, %d hues' % (a, la, len(ha)), '%s: %d bytes, %d hues' % (b, lb, len(hb))]
    for p, n in ((a, la), (b, lb)):
        if n % HUE_GROUP:
            lines.append('warning: %s is not whole %d-byte groups (%d left over)' % (p, HUE_GROUP, n % HUE_GROUP))
    diff = []
    for x, y in zip(ha, hb):
        what = []
        nc = sum(1 for i in range(32) if (x[1][i] & 0x7FFF) != (y[1][i] & 0x7FFF))
        if nc:
            what.append('%d of 32 colours' % nc)
        if (x[2], x[3]) != (y[2], y[3]):
            what.append('table %d-%d -> %d-%d' % (x[2], x[3], y[2], y[3]))
        if x[4] != y[4]:
            what.append('name "%s" -> "%s"' % (x[4], y[4]))
        if what:
            diff.append((x[0], what))
    if len(ha) != len(hb):
        lines.append('hue counts differ: %d against %d; only the first %d compared' % (len(ha), len(hb), min(len(ha), len(hb))))
    lines.append('%d hues differ' % len(diff))
    col = [h for h, w in diff if any('colours' in x for x in w)]
    lines.append('  %d in their colours (what an item or creature shows): %s' % (len(col), compact(col)))
    other = [h for h, w in diff if not any('colours' in x for x in w)]
    lines.append('  %d only in name or table range (unused by drawing; EA leaves junk there): %s' % (len(other), compact(other)))
    diff = [d for d in diff if d[0] in set(col)] + [d for d in diff if d[0] not in set(col)]
    for h, what in diff[:limit]:
        lines.append('  hue %d (0x%X): %s' % (h, h, '; '.join(what)))
    if len(diff) > limit:
        lines.append('  ... %d more (raise --limit)' % (len(diff) - limit))
    return lines, diff


# --- render ------------------------------------------------------------------------------------------
def decode_record(data):
    """[(rgba array, centre x, centre y)] of one anim*.mul record: palette, frame table, frames
    (ClassicAnimationReader.ParseFrames, AnimationReader.DecodeFrame; run headers as TazUO's ReadSpriteData reads
    them: x and y are signed 10-bit fields, which is what vseUOAware's XOR with 0x200 and base offset amount to)."""
    pal = struct.unpack_from('<256H', data, 0)
    start = 512
    fc = struct.unpack_from('<i', data, start)[0]
    if not 0 < fc <= 512:
        raise ValueError('implausible frame count %d' % fc)
    frames = []
    for k in range(fc):
        o = start + struct.unpack_from('<i', data, start + 4 + 4 * k)[0]
        if not 0 <= o < len(data) - 8:
            frames.append(None)
            continue
        cx, cy, w, h = struct.unpack_from('<hhHH', data, o)
        o += 8
        if not (0 < w <= 4096 and 0 < h <= 4096):
            frames.append(None)
            continue
        img = np.zeros((h, w, 4), np.uint8)
        while o + 4 <= len(data):
            hd = struct.unpack_from('<I', data, o)[0]
            o += 4
            if hd == 0x7FFF7FFF:
                break
            x = (hd >> 22) & 0x3FF
            y = (hd >> 12) & 0x3FF
            x = x - 0x400 if x & 0x200 else x
            y = y - 0x400 if y & 0x200 else y
            run = hd & 0xFFF
            px, py = x + cx, y + cy + h
            for i in range(run):
                v = pal[data[o + i]]        # every run pixel is drawn, colour 0 too (ReadSpriteData, no alpha check)
                if 0 <= py < h and 0 <= px + i < w:
                    img[py, px + i] = (((v >> 10) & 31) * 255 // 31, ((v >> 5) & 31) * 255 // 31, (v & 31) * 255 // 31, 255)
            o += run
        frames.append((img, cx, cy))
    return frames


def record_index(direction):
    """Stored direction for 0-7 (5-7 mirror 3-1) and whether to flip."""
    return (direction, False) if direction <= 4 else (8 - direction, True)


def render(c, body, vd, action, direction, out):
    from PIL import Image
    d, flip = record_index(direction)
    if vd is not None:
        data = vd.record_data(action * 5 + d)
        tag = os.path.splitext(os.path.basename(vd.path))[0].replace(' ', '_')
    else:
        r = c.resolve(body)
        if r['format'] == 'uop':
            raise SystemExit('body %d is a UOP body; render it with the vseUOAware MCP server (RenderAnimationFrames)' % body)
        if action >= r['actions_read']:
            raise SystemExit('body %d reads only %d actions' % (body, r['actions_read']))
        blk = c.records(r['file'], r['start'], r['end'])
        rec = blk[action * 5 + d] if blk is not None else None
        data = None
        if rec is not None and rec[0] != -1 and rec[1] > 0:
            with open(c.files[r['file']][2], 'rb') as f:
                f.seek(int(rec[0]))
                data = f.read(int(rec[1]))
        tag = 'body%d' % body
    if not data:
        raise SystemExit('no frames for action %d direction %d' % (action, direction))
    os.makedirs(out, exist_ok=True)
    paths = []
    for k, fr in enumerate(decode_record(data)):
        if fr is None:
            continue
        img = fr[0][:, ::-1] if flip else fr[0]
        p = os.path.join(out, '%s_a%02d_d%d_f%02d.png' % (tag, action, direction, k))
        Image.fromarray(np.ascontiguousarray(img), 'RGBA').save(p)
        paths.append(p)
    return paths


# --- main --------------------------------------------------------------------------------------------
def parse_type(s):
    s = s.lower()
    if s == 'sea':
        s = 'sea_monster'
    if s not in TYPES:
        raise argparse.ArgumentTypeError('type is one of %s' % ', '.join(TYPES))
    return TYPES.index(s)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[1].strip())
    ap.add_argument('--data', default=DEFAULT_DATA, help='EA Classic Client folder (default: client-data\\classic-client-7.0.117.0)')
    ap.add_argument('--no-overrides', action='store_true', help='leave out our package (records.json equipAnims and bodyAnims, sl-anim7)')
    ap.add_argument('--pinned', default=DEFAULT_PINNED)
    ap.add_argument('--registry', default=REGISTRY)
    ap.add_argument('--records', default=RECORDS, help='records.json (its equipAnims and bodyAnims, and sl-anim7*.bin beside it)')
    sub = ap.add_subparsers(dest='cmd', required=True)
    p = sub.add_parser('bodies', help='every body the client data names or draws')
    p.add_argument('--all', action='store_true', help='also bodies with frames only by default slot (no def line)')
    p = sub.add_parser('actions', help='which of the 80 actions a body has frames for')
    p.add_argument('body', type=lambda s: int(s, 0))
    p = sub.add_parser('check-vd', help='is this body a free, matching slot for this .vd')
    p.add_argument('vd')
    p.add_argument('body', type=lambda s: int(s, 0))
    p.add_argument('--file', type=int, help='planned Bodyconv target: anim file number (2 = anim2, 7 = our anim7)')
    p.add_argument('--slot', type=int, help='planned Bodyconv target: slot in that file')
    p.add_argument('--type', type=parse_type, help='planned mobtypes TYPE')
    p.add_argument('--flags', type=lambda s: int(s, 16), default=None, help='planned mobtypes flags, hex')
    p = sub.add_parser('propose', help='a free server body and anim7 slot for each .vd (registers nothing)')
    p.add_argument('vds', nargs='+')
    p.add_argument('--body-from', type=lambda s: int(s, 0), default=DEFAULT_BODY_FROM)
    sub.add_parser('check-registry', help='registry.csv rows that collide')
    p = sub.add_parser('hues-diff', help='per-hue differences between two hues.mul files')
    p.add_argument('a')
    p.add_argument('b')
    p.add_argument('--limit', type=int, default=40)
    p = sub.add_parser('render', help='one body/action/direction to PNGs')
    p.add_argument('body_or_action', type=int, nargs='+', help='body action direction, or with --vd: action direction')
    p.add_argument('--vd')
    p.add_argument('--out', required=True)
    a = ap.parse_args(argv)

    if a.cmd == 'hues-diff':
        lines, _ = hues_diff(a.a, a.b, a.limit)
        print('\n'.join(lines))
        return 0

    if a.cmd == 'render' and a.vd:
        if len(a.body_or_action) != 2:
            ap.error('render --vd takes: action direction')
        for x in render(None, None, Vd(a.vd), a.body_or_action[0], a.body_or_action[1], a.out):
            print(x)
        return 0

    c = Client(a.data, overrides=not a.no_overrides, pinned=a.pinned, records=a.records)

    if a.cmd == 'render':
        if len(a.body_or_action) != 3:
            ap.error('render takes: body action direction')
        for x in render(c, a.body_or_action[0], None, a.body_or_action[1], a.body_or_action[2], a.out):
            print(x)
        return 0

    if a.cmd == 'bodies':
        print('body\ttype\tflags\tformat\tsource\tslot\tlayout\tactions\tdefined by')
        n = 0
        for b in sorted(set(range(MAX_BODY)) | set(c.bodyconv) | set(c.mobtypes)):
            named = b in c.mobtypes or b in c.bodyconv
            if not named and not a.all:
                continue
            r, acts = c.actions(b)
            if not named and not acts:
                continue
            t = TYPES[c.mobtypes[b][0]].upper() if b in c.mobtypes else '-'
            fl = '%X' % c.mobtypes[b][1] if b in c.mobtypes else '-'
            why = []
            if b in c.mobtypes:
                why.append(c.mobtypes[b][2])
            if b in c.bodyconv:
                why.append(c.bodyconv[b][2])
            print('%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
                b, t, fl, 'UOP' if r['format'] == 'uop' else 'classic', c.source(r),
                '-' if r['format'] == 'uop' else r['slot'], r['group'] or '-', compact(acts), ', '.join(why) or 'default slot'))
            n += 1
        print('%d bodies' % n)
        return 0

    if a.cmd == 'actions':
        r, acts = c.actions(a.body)
        where = c.source(r) if r['format'] == 'uop' else '%s slot %d, %s layout, records %d-%d' % (
            c.source(r), r['slot'], r['group'], r['start'], r['end'] - 1)
        print('body %d: %s, %s' % (a.body, 'UOP' if r['format'] == 'uop' else 'classic', where))
        print('%d of %d actions have frames: %s' % (len(acts), MAX_ACTIONS, compact(acts)))
        return 0

    if a.cmd == 'check-vd':
        conv = None
        if a.file is not None or a.slot is not None:
            if a.file is None or a.slot is None:
                ap.error('--file and --slot go together')
            if a.file < 2:
                ap.error('--file is a Bodyconv target, anim2 (2) or later; anim.mul (1) is where a body is without one')
            conv = (a.file - 1, a.slot)
        mob = None
        if a.type is not None or a.flags is not None:
            cur = c.mobtypes.get(a.body, (None, 0, None))
            mob = (a.type if a.type is not None else (cur[0] if cur[0] is not None else
                   type_by_graphic(conv[1] if conv else a.body, conv[0] if conv else 0)),
                   a.flags if a.flags is not None else 0)
        out, problems = check_vd(c, Vd(a.vd), a.body, conv=conv, mob=mob)
        print('\n'.join(out))
        for x in problems:
            print('PROBLEM ' + x)
        print('verdict: %s' % ('free and matching' if not problems else '%d problem(s)' % len(problems)))
        return 1 if problems else 0

    if a.cmd == 'propose':
        reg = {r['id'] for r in read_registry(a.registry)}
        taken_b, taken_s = set(), set()
        nb = a.body_from
        print('vd\tlayout\tactions\tbody\tanim7 slot\tmobtypes\tproblems and notes')
        bad = 0
        for path in a.vds:
            vd = Vd(path)
            group = vd.group
            acts = vd.actions()
            if group == 'high' and acts and max(acts) < 13:
                note_group = 'low?'
            else:
                note_group = ''
            b = free_body(c, nb, taken_b, reg)
            s = free_slot(c, group, taken_s)
            if b is None or s is None:
                print('%s\t%s\t%s\t-\t-\t-\tno free %s' % (path, group, compact(acts), 'body' if b is None else 'slot'))
                bad += 1
                continue
            taken_b.add(b)
            taken_s.add((group, s))
            nb = b + 1
            out, problems = check_vd(c, vd, b, conv=(OUR_FILE, s))
            mt = [x for x in out if x.startswith('mobtypes:')]
            notes = [x for x in out if x.startswith('note:')] + ['PROBLEM ' + p for p in problems]
            bad += bool(problems)
            print('%s\t%s%s\t%s\t%d (0x%X)\t%d\t%s\t%s' % (
                os.path.basename(path), group, (' ' + note_group) if note_group else '', compact(acts), b, b, s,
                mt[0][len('mobtypes: '):] if mt else '-', ' | '.join(notes) or '-'))
        return 1 if bad else 0

    if a.cmd == 'check-registry':
        rows = read_registry(a.registry)
        art = ffa.Uop(os.path.join(a.data, 'artLegacyMUL.uop'))
        seen, bad = {}, 0
        for r in rows:
            k, i = r['kind'], r['id']
            prob = []
            if (k, i) in seen:
                prob.append('collides: registered twice (%s)' % seen[(k, i)])
            seen.setdefault((k, i), r.get('what', ''))
            if k == 'art':
                s = ffa.art_state(art, i)
                if s == 'art':
                    prob.append('collides: EA art is in slot %d' % i)
            elif k in ('anim', 'body'):
                ea = [w for w in c.named.get(i, []) if 'records.json' not in w]
                if i in c.bodyconv and not c.bodyconv[i][2].startswith('records.json'):
                    prob.append('collides: %s sends it elsewhere' % c.bodyconv[i][2])
                if k == 'anim':
                    if i in c.mobtypes:
                        prob.append('collides: %s names it (TazUO would read UOP or another layout)' % c.mobtypes[i][2])
                    if not any(int(e['anim']) == i for e in c.equip):
                        prob.append('missing: records.json equipAnims has no line for it')
                else:
                    ours = any(int(b['body']) == i for b in c.bodies)
                    if i not in c.mobtypes:
                        prob.append('missing: no mobtypes.txt entry')
                    elif ours and c.mobtypes[i][2] != 'records.json bodyAnims':
                        prob.append('collides: %s names it; the launcher would skip our body' % c.mobtypes[i][2])
                    if not ours and i in c.bodyconv and c.bodyconv[i][2].startswith('records.json'):
                        prob.append('collides: %s sends it elsewhere' % c.bodyconv[i][2])
                    if not ours and i not in c.named and i not in c.bodyconv:
                        prob.append('missing: records.json bodyAnims has no line for it')
                for w in ea:
                    if not w.startswith(('mobtypes', 'Bodyconv')):
                        prob.append('collides: named in %s' % w)
                r0 = c.resolve(i)
                if r0['format'] == 'classic':
                    for b, s, e, why in c.readers(r0['file'], r0['start'], r0['end'], skip={i}):
                        if why or c.classic_actions(c.resolve(b)):
                            prob.append('collides: body %d also reads %s records %d-%d' % (b, file_name(r0['file']), s, e - 1))
                    if not c.classic_actions(r0):
                        prob.append('missing: no frames at %s slot %d' % (file_name(r0['file']), r0['slot']))
                    note = '%s slot %d %s' % (file_name(r0['file']), r0['slot'], r0['group'])
                else:
                    note = 'UOP'
                if k == 'anim':
                    note += '; no mobtypes entry, as an equipment animation needs'
                elif i in c.mobtypes and c.mobtypes[i][2] == 'records.json bodyAnims':
                    note += '; mobtypes %s %X from records.json' % (TYPES[c.mobtypes[i][0]].upper(), c.mobtypes[i][1])
                r['note'] = note
            print('%s %d (0x%X) %s: %s%s' % (k, i, i, r.get('what', '')[:70], 'ok' if not prob else '; '.join(prob),
                                            ' [%s]' % r['note'] if r.get('note') else ''))
            bad += bool(prob)
        print('%d of %d rows have problems' % (bad, len(rows)))
        return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
