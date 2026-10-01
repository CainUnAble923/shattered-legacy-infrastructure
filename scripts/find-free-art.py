#!/usr/bin/env python3
r"""
find-free-art.py: list art IDs that are free for our own art (cc-P26, F-8; reused by F-21 to F-23).

    python scripts/find-free-art.py                      runs of 11 free IDs, best first
    python scripts/find-free-art.py --run 44 --top 5     runs of 44
    python scripts/find-free-art.py --explain 0x9E40     why one ID is or is not free
    python scripts/find-free-art.py --check-registry     every registered ID still free, apart from being registered

An ID (a static's item ID, without the 0x4000 art offset) is free only when ALL of these hold:

  art       the art slot in artLegacyMUL.uop is empty, or decodes to OSI's "UNUSED" placeholder (the one
            44x44 image 2,494 slots share; identified by the SHA-1 of its decoded pixels, PLACEHOLDER_PIXELS)
  art.def   art.def neither names the slot nor points it at another (TazUO fills an empty slot from art.def)
  tiledata  the static entry has no flags and its name is empty or "UNUSED"
  animdata  its own animdata entry lies inside the file (TazUO animates nothing past the end) and has no
            frames, and no other entry's frames land on it (that static would draw our art as a frame)
  maps      no static on any map, map diff or multi uses it (statics*.mul, stadif*.mul, MultiCollection.uop
            and multi.mul)
  code      the number does not appear in pinned ModernUO (Projects\, Distribution\) or in our server\
            (customizations\, patches\), in hex (0x1A80, any case, any leading zeros) or as a decimal word.
            A decimal hit is often a coordinate or a cliloc, not an item: a hit is a candidate, but it
            still blocks the ID, because checking it by hand is cheaper than a collision found in game
  overrides no ExternalImages\art PNG or BMP we ship names it (any vendor\ folder of the player package),
            except our own art folder, whose IDs are the registry's
  registry  it is not already in player-package\vendor\shattered-legacy-art\registry.csv

Reads EA's files, writes nothing. Python 3 with numpy and Pillow (Pillow only for --explain's picture).
ASCII only.
"""
import argparse, csv, hashlib, os, re, struct, sys, zlib
from collections import defaultdict

import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_DATA = os.path.join(REPO, 'client-data', 'classic-client')
DEFAULT_PINNED = r'D:\UO\ModernUO-pinned'
REGISTRY = os.path.join(REPO, 'player-package', 'vendor', 'shattered-legacy-art', 'registry.csv')
OUR_ART = os.path.join(REPO, 'player-package', 'vendor', 'shattered-legacy-art', 'art')

# SHA-1 of the decoded RGBA pixels of OSI's 44x44 "UNUSED" art (counted 2026-10-01: 2,494 static slots share
# it, among them the farm-cart slots 6784, 6785, 6807, 6808 that F-21 found). Other UNUSED-looking variants exist
# (7182 "Valid_Art", 9224 "Missing_Name"); they are not accepted, on purpose.
PLACEHOLDER_PIXELS = '1fed9f31d00823c100506cf8173c92ade95ec2aa'

TILE_LAND_BYTES = 512 * (4 + 32 * 30)   # new format (client 7.0.9.0 and later): 493,568
TILE_GROUP = 4 + 32 * 41
ANIM_ENTRY = 68
TAG_NAMES = ('art', 'art.def', 'tiledata', 'animdata', 'maps', 'code', 'overrides', 'registry')
M32 = 0xFFFFFFFF


# --- UOP -------------------------------------------------------------------------------------------
def uop_hash(name):
    """Mythic's UOP name hash (hashlittle2), as ClassicUO's UOFileUop.CreateHash."""
    s = name.lower().encode('ascii')
    L = len(s)
    eax = ecx = edx = 0
    ebx = edi = esi = (L + 0xDEADBEEF) & M32
    i = 0
    while i + 12 < L:
        edi = (struct.unpack_from('<I', s, i + 4)[0] + edi) & M32
        esi = (struct.unpack_from('<I', s, i + 8)[0] + esi) & M32
        edx = (struct.unpack_from('<I', s, i)[0] - esi) & M32
        edx = ((edx + ebx) & M32) ^ (esi >> 28) ^ ((esi << 4) & M32)
        esi = (esi + edi) & M32
        edi = ((edi - edx) & M32) ^ (edx >> 26) ^ ((edx << 6) & M32)
        edx = (edx + esi) & M32
        esi = ((esi - edi) & M32) ^ (edi >> 24) ^ ((edi << 8) & M32)
        edi = (edi + edx) & M32
        ebx = ((edx - esi) & M32) ^ (esi >> 16) ^ ((esi << 16) & M32)
        esi = (esi + edi) & M32
        edi = ((edi - ebx) & M32) ^ (ebx >> 13) ^ ((ebx << 19) & M32)
        ebx = (ebx + esi) & M32
        esi = ((esi - edi) & M32) ^ (edi >> 28) ^ ((edi << 4) & M32)
        edi = (edi + ebx) & M32
        i += 12
    r = L - i
    if r > 0:
        b = s[i:]
        g = lambda k: b[k] if k < len(b) else 0
        if r >= 9:
            esi = (esi + sum(g(k) << (8 * (k - 8)) for k in range(8, min(r, 12)))) & M32
        if r >= 5:
            edi = (edi + sum(g(k) << (8 * (k - 4)) for k in range(4, min(r, 8)))) & M32
        ebx = (ebx + sum(g(k) << (8 * k) for k in range(0, min(r, 4)))) & M32
        esi = ((esi ^ edi) - ((edi >> 18) ^ ((edi << 14) & M32))) & M32
        ecx = ((esi ^ ebx) - ((esi >> 21) ^ ((esi << 11) & M32))) & M32
        edi = ((edi ^ ecx) - ((ecx >> 7) ^ ((ecx << 25) & M32))) & M32
        esi = ((esi ^ edi) - ((edi >> 16) ^ ((edi << 16) & M32))) & M32
        edx = ((esi ^ ecx) - ((esi >> 28) ^ ((esi << 4) & M32))) & M32
        edi = ((edi ^ edx) - ((edx >> 18) ^ ((edx << 14) & M32))) & M32
        eax = ((esi ^ edi) - ((edi >> 8) ^ ((edi << 24) & M32))) & M32
        return (edi << 32) | eax
    return (esi << 32) | eax


class Uop:
    def __init__(self, path):
        self.f = open(path, 'rb')
        _magic, _ver, _sig, nxt, _cap, _cnt = struct.unpack('<IIIqII', self.f.read(28))
        self.e = {}
        while nxt:
            self.f.seek(nxt)
            n, nxt = struct.unpack('<Iq', self.f.read(12))
            for _ in range(n):
                off, hl, cl, dl, h, _ad, fl = struct.unpack('<qIIIQIH', self.f.read(34))
                if off:
                    self.e[h] = (off + hl, cl, dl, fl)

    def get(self, name):
        v = self.e.get(uop_hash(name))
        if not v:
            return None
        self.f.seek(v[0])
        d = self.f.read(v[1])
        if v[3] == 1:
            d = zlib.decompress(d)
        return d


# --- art -------------------------------------------------------------------------------------------
def decode_static(d):
    """RGBA array of a static art entry (run-length rows of 1555 colour), or None."""
    if not d or len(d) < 8:
        return None
    w, h = struct.unpack_from('<HH', d, 4)
    if not (0 < w < 1024 and 0 < h < 1024):
        return None
    look = struct.unpack_from('<%dH' % h, d, 8)
    base = 8 + h * 2
    a = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        o = base + look[y] * 2
        x = 0
        while True:
            xo, rl = struct.unpack_from('<HH', d, o)
            o += 4
            if xo + rl == 0:
                break
            x += xo
            v = np.frombuffer(d, '<u2', rl, o).astype(np.uint32)
            o += rl * 2
            a[y, x:x + rl, 0] = ((v >> 10) & 31) * 255 // 31
            a[y, x:x + rl, 1] = ((v >> 5) & 31) * 255 // 31
            a[y, x:x + rl, 2] = (v & 31) * 255 // 31
            a[y, x:x + rl, 3] = 255
            x += rl
    return a


def art_state(art, i):
    """'empty', 'placeholder', or 'art' for static item ID i."""
    d = art.get('build/artlegacymul/%08d.tga' % (0x4000 + i))
    if d is None:
        return 'empty'
    if len(d) < 8:
        return 'art'
    w, h = struct.unpack_from('<HH', d, 4)
    if (w, h) != (44, 44):
        return 'art'
    a = decode_static(d)
    if a is not None and hashlib.sha1(a.tobytes()).hexdigest() == PLACEHOLDER_PIXELS:
        return 'placeholder'
    return 'art'


# --- tiledata and animdata -------------------------------------------------------------------------
def tile_offset(i):
    return TILE_LAND_BYTES + (i // 32) * TILE_GROUP + 4 + (i % 32) * 41


def read_tiles(path):
    d = open(path, 'rb').read()
    n = (len(d) - TILE_LAND_BYTES) // TILE_GROUP * 32
    flags = np.zeros(n, np.uint64)
    names = [''] * n
    for i in range(n):
        o = tile_offset(i)
        flags[i] = struct.unpack_from('<Q', d, o)[0]
        names[i] = d[o + 21:o + 41].split(b'\0')[0].decode('latin1')
    return flags, names, len(d)


def anim_offset(i):
    return i * ANIM_ENTRY + 4 * (i // 8 + 1)


def read_anim(path, n):
    d = open(path, 'rb').read()
    inside = np.zeros(n, bool)
    count = np.zeros(n, np.int32)
    targeted = defaultdict(set)    # id -> the bases whose frames land on it
    for i in range(n):
        o = anim_offset(i)
        if o + ANIM_ENTRY > len(d):
            continue
        inside[i] = True
        c = d[o + 65]
        count[i] = c
        if c:
            for k in range(min(c, 64)):
                t = i + struct.unpack_from('<b', d, o + k)[0]
                if t != i and 0 <= t < n:
                    targeted[t].add(i)
    return inside, count, targeted, len(d)


# --- maps and multis -------------------------------------------------------------------------------
def map_static_ids(data):
    used = set()
    for name in sorted(os.listdir(data)):
        l = name.lower()
        if re.fullmatch(r'statics\d+x?\.mul|stadif\d+\.mul', l):
            raw = np.fromfile(os.path.join(data, name), np.uint8)
            raw = raw[:len(raw) - len(raw) % 7].reshape(-1, 7)
            ids = raw[:, 0].astype(np.uint32) | (raw[:, 1].astype(np.uint32) << 8)
            used.update(np.unique(ids).tolist())
    return used


def multi_ids(data):
    used = set()
    p = os.path.join(data, 'MultiCollection.uop')
    if os.path.exists(p):
        u = Uop(p)
        for idx in range(0x2200):
            d = u.get('build/multicollection/%06d.bin' % idx)
            if not d:
                continue
            o = 4
            cnt = struct.unpack_from('<i', d, o)[0]
            o += 4
            for _ in range(cnt):
                gid, _x, _y, _z, _fl, extra = struct.unpack_from('<HhhhHI', d, o)
                o += 14 + extra * 4
                used.add(gid)
    mi, mm = os.path.join(data, 'multi.idx'), os.path.join(data, 'multi.mul')
    if os.path.exists(mi) and os.path.exists(mm):
        idx = open(mi, 'rb').read()
        mul = open(mm, 'rb').read()
        for k in range(len(idx) // 12):
            look, length, _ = struct.unpack_from('<iii', idx, k * 12)
            if look < 0 or length <= 0:
                continue
            for o in range(look, look + length - 15, 16):
                used.add(struct.unpack_from('<H', mul, o)[0])
    return used


def art_def_ids(data):
    used = set()
    p = os.path.join(data, 'art.def')
    if os.path.exists(p):
        for line in open(p, 'r', encoding='latin1'):
            line = line.split('#')[0]
            for n in re.findall(r'\d+', line):
                v = int(n)
                if v >= 0x4000:
                    used.add(v - 0x4000)
    return used


# --- code ------------------------------------------------------------------------------------------
TEXT_EXT = {'.cs', '.cfg', '.txt', '.xml', '.json', '.csv', '.patch', '.def', '.map', '.md'}


def code_numbers(roots):
    """(hex, dec): value -> one file it appears in, over every text file under roots."""
    hexes, decs = {}, {}
    rx_hex = re.compile(r'0x([0-9a-fA-F]+)')
    rx_dec = re.compile(r'(?<![0-9A-Za-z_.])([0-9]{3,5})(?![0-9A-Za-z_])')
    for root in roots:
        if not os.path.isdir(root):
            print('warning: no such folder, not searched: %s' % root, file=sys.stderr)
            continue
        for dp, dn, fn in os.walk(root):
            dn[:] = [x for x in dn if x not in ('bin', 'obj', '.git')]
            for f in fn:
                if os.path.splitext(f)[1].lower() not in TEXT_EXT:
                    continue
                p = os.path.join(dp, f)
                try:
                    t = open(p, 'r', encoding='utf-8', errors='replace').read()
                except OSError:
                    continue
                for m in rx_hex.findall(t):
                    if len(m) <= 8:
                        hexes.setdefault(int(m, 16), p)
                for m in rx_dec.findall(t):
                    decs.setdefault(int(m), p)
    return hexes, decs


def override_ids(vendor_root):
    used = {}
    for dp, _dn, fn in os.walk(vendor_root):
        parts = [x.lower() for x in dp.replace('\\', '/').split('/')]
        if os.path.abspath(dp).lower().startswith(os.path.abspath(OUR_ART).lower()):
            continue
        if not (len(parts) >= 2 and parts[-1] == 'art' and parts[-2] == 'externalimages'):
            continue
        for f in fn:
            b, e = os.path.splitext(f)
            if e.lower() not in ('.png', '.bmp'):
                continue
            v = parse_id(b)
            if v is not None:
                used[v] = os.path.join(dp, f)
    return used


def parse_id(s):
    """An art file name as TazUO reads it (ExternalImageLoader.TryParseId): decimal, or hex after 0x."""
    try:
        return int(s[2:], 16) if s.lower().startswith('0x') else int(s, 10)
    except ValueError:
        return None


def read_registry(path):
    rows = {}
    if os.path.exists(path):
        with open(path, newline='', encoding='ascii') as f:
            for r in csv.DictReader(line for line in f if not line.startswith('#')):
                rows[int(r['id'])] = r
    return rows


# --- the test --------------------------------------------------------------------------------------
class World:
    def __init__(self, a):
        self.data = a.data
        print('reading %s ...' % a.data, file=sys.stderr)
        self.art = Uop(os.path.join(a.data, 'artLegacyMUL.uop'))
        self.flags, self.names, self.tile_bytes = read_tiles(os.path.join(a.data, 'tiledata.mul'))
        self.n = len(self.names)
        self.anim_inside, self.anim_count, self.anim_targeted, self.anim_bytes = read_anim(
            os.path.join(a.data, 'animdata.mul'), self.n)
        self.maps = map_static_ids(a.data)
        self.multis = multi_ids(a.data)
        self.artdef = art_def_ids(a.data)
        roots = [os.path.join(a.pinned, 'Projects'), os.path.join(a.pinned, 'Distribution'),
                 os.path.join(REPO, 'server', 'customizations'), os.path.join(REPO, 'server', 'patches')]
        print('reading code under %s ...' % ', '.join(roots), file=sys.stderr)
        self.hexes, self.decs = code_numbers(roots)
        self.overrides = override_ids(os.path.join(REPO, 'player-package', 'vendor'))
        self.registry = read_registry(a.registry)
        self._art = {}

    def art_state(self, i):
        if i not in self._art:
            self._art[i] = art_state(self.art, i)
        return self._art[i]

    def reasons(self, i, ignore_registry=False, placeholder_only=False):
        """Why ID i is not free; an empty list means free. Cheap tests first."""
        r = []
        if i >= self.n:
            return ['tiledata: past the end (%d statics)' % self.n]
        if int(self.flags[i]) != 0:
            r.append('tiledata: flags 0x%X' % int(self.flags[i]))
        if self.names[i].strip().lower() not in ('', 'unused'):
            r.append('tiledata: name "%s"' % self.names[i])
        if not self.anim_inside[i]:
            r.append('animdata: entry past the end of animdata.mul (%d bytes)' % self.anim_bytes)
        elif self.anim_count[i]:
            r.append('animdata: has %d frames' % self.anim_count[i])
        if i in self.anim_targeted:
            r.append('animdata: a frame of %s' % ', '.join('0x%X' % b for b in sorted(self.anim_targeted[i])))
        if i in self.maps:
            r.append('maps: a map static')
        if i in self.multis:
            r.append('maps: in a multi')
        if i in self.artdef:
            r.append('art.def: named there')
        if i in self.hexes:
            r.append('code: hex 0x%X in %s' % (i, self.hexes[i]))
        if i in self.decs:
            r.append('code: decimal %d in %s' % (i, self.decs[i]))
        if i in self.overrides:
            r.append('overrides: %s' % self.overrides[i])
        if not ignore_registry and i in self.registry:
            r.append('registry: %s' % self.registry[i].get('what', ''))
        if not r:
            s = self.art_state(i)
            if s == 'art':
                r.append('art: real art in the slot')
            elif placeholder_only and s != 'placeholder':
                r.append('art: slot is empty, and only placeholder slots were asked for')
        return r


def runs(w, length, lo, hi, placeholder_only):
    out = []
    start = None
    for i in range(lo, hi + 1):
        free = not w.reasons(i, placeholder_only=placeholder_only)
        if free and start is None:
            start = i
        if (not free or i == hi) and start is not None:
            end = i if free else i - 1
            if end - start + 1 >= length:
                out.append((start, end))
            start = None
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--data', default=DEFAULT_DATA, help='EA Classic Client folder (default: the shard\'s client-data)')
    ap.add_argument('--pinned', default=DEFAULT_PINNED, help='pinned ModernUO checkout')
    ap.add_argument('--registry', default=REGISTRY)
    ap.add_argument('--run', type=int, default=11, help='consecutive free IDs wanted (default 11)')
    ap.add_argument('--top', type=int, default=20, help='how many runs to print')
    ap.add_argument('--from', dest='lo', type=lambda s: int(s, 0), default=0x0400)
    ap.add_argument('--to', dest='hi', type=lambda s: int(s, 0), default=0xFFFF)
    ap.add_argument('--explain', type=lambda s: int(s, 0), nargs='*', help='IDs to explain (decimal or 0x hex)')
    ap.add_argument('--placeholder', action='store_true',
                    help='only slots holding the UNUSED placeholder art (an empty slot also counts as free otherwise)')
    ap.add_argument('--check-registry', action='store_true',
                    help='re-test every registered ID (a hit in our own server code is its expected use)')
    a = ap.parse_args()

    w = World(a)
    print('tiledata %d bytes, %d statics; animdata %d bytes; %d map/diff static IDs; %d multi IDs; '
          '%d art.def IDs; %d hex and %d decimal numbers in code; %d shipped art overrides; %d registered'
          % (w.tile_bytes, w.n, w.anim_bytes, len(w.maps), len(w.multis), len(w.artdef), len(w.hexes),
             len(w.decs), len(w.overrides), len(w.registry)))

    if a.explain:
        for i in a.explain:
            r = w.reasons(i)
            print('%d (0x%X): %s; art %s' % (i, i, 'FREE' if not r else 'not free', w.art_state(i) if i < w.n else '-'))
            for x in r:
                print('    ' + x)
        return 0

    if a.check_registry:
        bad = 0
        ours = os.path.join(REPO, 'server').lower()
        for i in sorted(w.registry):
            # Our own server code naming a registered ID is the ID in use, not a collision. Pinned is not ours.
            r = [x for x in w.reasons(i, ignore_registry=True)
                 if not (x.startswith('code: ') and x.lower().split(' in ', 1)[-1].startswith(ours))]
            print('%d (0x%X) %s: %s' % (i, i, w.registry[i].get('what', ''), 'still free' if not r else '; '.join(r)))
            bad += bool(r)
        print('%d of %d registered IDs are no longer free' % (bad, len(w.registry)))
        return 1 if bad else 0

    found = runs(w, a.run, a.lo, min(a.hi, w.n - 1), a.placeholder)
    found.sort(key=lambda se: (-(se[1] - se[0]), se[0]))
    print('%d runs of at least %d free IDs%s between 0x%X and 0x%X; longest first:'
          % (len(found), a.run, ' (placeholder art only)' if a.placeholder else '', a.lo, a.hi))
    for s, e in found[:a.top]:
        print('  0x%04X-0x%04X  (%d-%d)  %d IDs' % (s, e, s, e, e - s + 1))
    return 0


if __name__ == '__main__':
    sys.exit(main())
