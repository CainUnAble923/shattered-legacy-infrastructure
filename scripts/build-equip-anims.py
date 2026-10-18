#!/usr/bin/env python3
r"""
build-equip-anims.py: our animations, encoded for TazUO as an extra anim file: worn items (cc-P62, F-35) and
creature bodies (cc-P139, F-46).

    python scripts/build-equip-anims.py            write vendor\shattered-legacy-art\sl-anim7.bin and sl-anim7-idx.bin
    python scripts/build-equip-anims.py --check    rebuild in memory; fail unless the committed files are identical,
                                                   and decode them back and compare with the sources (the PNGs, the .vd)

Why a seventh anim file. A worn item's tiledata names an Animation number N; TazUO draws body N's frames over the
wearer (MobileView.Draw). A creature's body N is drawn the same way from body N's own frames. EA's own files hold
anim.mul and anim2..anim6.mul, and TazUO opens anim.mul up to anim10.mul, each through the -uofilesoverride list
(AnimationsLoader.Load). A Bodyconv.def line "N -1 -1 -1 -1 -1 S" sends body N to slot S of anim7.mul
(AnimationsLoader.ProcessBodyConvDef: column i is anim<i+1>; no expansion flag is checked past column 2). So the
package ships anim7 as two files that are entirely ours, and the launcher adds the Bodyconv.def line (and, for a
creature, the mobtypes.txt line) to copies of the player's own files (app\Build-UoOverrides.ps1). Nothing of EA's ships.

Slots. TazUO types a body in anim7 by its slot (CalculateTypeByGraphic, file index 6: below 200 Monster, below 400
Animal, else Human), unless mobtypes.txt names the body. The slot's layout fixes where its index entries are and how
many (AnimationsLoader.CalculateOffset):
    high (monster)  slot 1-199     entries slot x 110,                 22 actions x 5 directions
    low (animal)    slot 200-399   entries 22000 + (slot - 200) x 65,  13 actions x 5
    people (human)  slot 400 up    entries 35000 + (slot - 400) x 175, 35 actions x 5
A worn item's slot must be 400 or more: TazUO only draws a worn item whose type is Human or Equipment
(Animations.GetAnimationFrames, isEquip). A creature body takes the slot of its .vd's layout. Every other entry is
empty (position -1).

Inputs, all ours (records.json):
  "equipAnims": [{"anim": N, "slot": S, ...}]
      anim-src\<N>\a<action>_d<direction>_f<frame>.png    130 x 128, fully transparent or fully opaque pixels.
      Canvas pixel (x, y) is (x - 67, y - 98) in the frame's own coordinates, the ones a run header holds; that is
      also the wearer's (the art was drawn over body 401's frames in that frame). Frames of one action and
      direction are numbered from 0 with no gaps.
  "bodyAnims": [{"body": N, "slot": S, "type": "MONSTER", "flags": 0, "vd": "anim-src/bodies/N.vd", ...}]
      a UOFiddler .vd (cc-P59 notes): int16 6, int16 layout (0 high, 1 low, 2 people), then actions x 5 index
      entries (int32 offset in the file, length, extra), each a ready anim.mul block. The block is copied unchanged
      (the .vd's blocks are already in the client's format), after this script has decoded it, so the frames in
      the package are the author's bytes. The .vd's layout must be the slot's.

The format (anim.mul's; AnimationsLoader.ReadMULAnimationFrames and ReadSpriteData), one block per action and
direction: 256 palette colours (uint16, 1555), int32 frame count, int32 frame offsets from the count, then per
frame int16 centerX, centerY, width, height and run headers (length in bits 0-11, y in 12-21, x in 22-31, both
signed 10-bit), each followed by its palette indices, ending 0x7FFF7FFF. The client puts a run pixel at image
column x + centerX, row y + centerY + height, and draws the image at the wearer's anchor minus (centerX,
height + centerY), so a pixel lands at the anchor plus its own (x, y) whatever box is chosen.

Colour: the format holds 5 bits a channel. Each PNG colour becomes the 15-bit colour whose expansion through
TazUO's table (HuesHelper.Color16To32, _table) is nearest, channel by channel. --check prints the largest
difference that leaves. A .vd is already 15-bit, so its colours are exact. ASCII only. Python 3 with Pillow.
"""
import argparse, json, os, re, struct, sys

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(REPO, 'player-package', 'vendor', 'shattered-legacy-art')
MUL_OUT = os.path.join(ART, 'sl-anim7.bin')
IDX_OUT = os.path.join(ART, 'sl-anim7-idx.bin')
CANVAS = (130, 128)
ORIGIN = (67, 98)
ACTIONS, DIRECTIONS = 35, 5
# TazUO 26.0909.63, ClassicUO.Utility/HuesHelper.cs:10-14: a 5-bit channel's 8-bit value.
TABLE = [0x00, 0x08, 0x10, 0x18, 0x20, 0x29, 0x31, 0x39, 0x41, 0x4A, 0x52, 0x5A, 0x62, 0x6A, 0x73, 0x7B, 0x83, 0x8B,
         0x94, 0x9C, 0xA4, 0xAC, 0xB4, 0xBD, 0xC5, 0xCD, 0xD5, 0xDE, 0xE6, 0xEE, 0xF6, 0xFF]
END = 0x7FFF7FFF
NAME = re.compile(r'^a(\d\d)_d(\d)_f(\d\d)\.png$')
# layout -> (first slot, last slot, actions, entries per slot); VD_LAYOUTS: the .vd header's layout number.
LAYOUTS = {'high': (1, 199, 22, 110), 'low': (200, 399, 13, 65), 'people': (400, 2047, 35, 175)}
VD_LAYOUTS = {0: 'high', 1: 'low', 2: 'people'}


def slot_layout(slot):
    """The layout TazUO gives a slot of anim7 (CalculateTypeByGraphic, file index 6), or None outside 1..2047."""
    for name, (lo, hi, _a, _n) in LAYOUTS.items():
        if lo <= slot <= hi:
            return name
    return None


def slot_start(slot):
    """The first index entry of a slot (CalculateHigh/Low/PeopleGroupOffset)."""
    return {'high': slot * 110, 'low': 22000 + (slot - 200) * 65, 'people': 35000 + (slot - 400) * 175}[slot_layout(slot)]


def slot_end(slot):
    return slot_start(slot) + LAYOUTS[slot_layout(slot)][3]


def to15(r, g, b):
    q = lambda v: min(range(32), key=lambda k: (abs(TABLE[k] - v), k))
    return (q(r) << 10) | (q(g) << 5) | q(b)


def to24(c):
    return TABLE[(c >> 10) & 31], TABLE[(c >> 5) & 31], TABLE[c & 31]


def load_frames(anim):
    """{(action, direction): [pixel dicts {(x, y): (r, g, b)} in frame order]} for one Animation number."""
    src = os.path.join(ART, 'anim-src', str(anim))
    found = {}
    for f in sorted(os.listdir(src)):
        m = NAME.match(f)
        if not m:
            raise SystemExit('%s: %s is not a<action>_d<direction>_f<frame>.png' % (src, f))
        a, d, i = (int(x) for x in m.groups())
        if a >= ACTIONS or d >= DIRECTIONS:
            raise SystemExit('%s: action %d, direction %d is outside 35 x 5' % (f, a, d))
        im = Image.open(os.path.join(src, f)).convert('RGBA')
        if im.size != CANVAS:
            raise SystemExit('%s is %dx%d, not %dx%d' % (f, im.size[0], im.size[1], CANVAS[0], CANVAS[1]))
        px = {}
        for y in range(CANVAS[1]):
            for x in range(CANVAS[0]):
                r, g, b, al = im.getpixel((x, y))
                if al not in (0, 255):
                    raise SystemExit('%s: pixel %d,%d has alpha %d; the format has no partial transparency' % (f, x, y, al))
                if al:
                    px[(x - ORIGIN[0], y - ORIGIN[1])] = (r, g, b)
        if not px:
            raise SystemExit('%s is empty' % f)
        found.setdefault((a, d), {})[i] = px
    out = {}
    for key, fr in found.items():
        if sorted(fr) != list(range(len(fr))):
            raise SystemExit('action %d direction %d: frames %s are not 0..%d' % (key[0], key[1], sorted(fr), len(fr) - 1))
        out[key] = [fr[i] for i in range(len(fr))]
    return out


def load_vd(path):
    """(layout name, {(action, direction): block bytes}) of a UOFiddler .vd; every block is decoded first."""
    d = open(path, 'rb').read()
    if len(d) < 4:
        raise SystemExit('%s: too short for a .vd header' % path)
    magic, lay = struct.unpack_from('<hh', d, 0)
    if magic != 6 or lay not in VD_LAYOUTS:
        raise SystemExit('%s: not a UOFiddler .vd (header %d, layout %d)' % (path, magic, lay))
    layout = VD_LAYOUTS[lay]
    actions = LAYOUTS[layout][2]
    blocks = {}
    for i in range(actions * DIRECTIONS):
        off, length, _extra = struct.unpack_from('<iii', d, 4 + i * 12)
        if off < 0 or length <= 0:
            continue
        if off + length > len(d):
            raise SystemExit('%s: block %d runs past the end of the file' % (path, i))
        blk = d[off:off + length]
        try:
            frames = decode_block(blk)
        except (SystemExit, struct.error) as e:
            raise SystemExit('%s: block %d (action %d direction %d) does not decode: %s' % (path, i, i // 5, i % 5, e))
        if not frames:
            raise SystemExit('%s: block %d has no frames' % (path, i))
        blocks[(i // DIRECTIONS, i % DIRECTIONS)] = blk
    if not blocks:
        raise SystemExit('%s: no block has frames' % path)
    return layout, blocks


def encode_block(frames):
    colours = sorted({to15(*c) for px in frames for c in px.values()})
    if len(colours) > 256:
        raise SystemExit('a block has %d colours; the palette holds 256' % len(colours))
    index = {c: k for k, c in enumerate(colours)}
    pal = colours + [0] * (256 - len(colours))
    bodies = []
    for px in frames:
        xs = [p[0] for p in px]; ys = [p[1] for p in px]
        if min(xs) < -512 or max(xs) > 511 or min(ys) < -512 or max(ys) > 511:
            raise SystemExit('a frame reaches past the 10-bit run coordinates')
        minx, miny = min(xs), min(ys)
        w, h = max(xs) - minx + 1, max(ys) - miny + 1
        cx, cy = -minx, -miny - h            # image (x - minx, y - miny); see the docstring
        b = bytearray(struct.pack('<hhhh', cx, cy, w, h))
        for y in sorted(set(ys)):
            row = sorted(x for (x, yy) in px if yy == y)
            k = 0
            while k < len(row):
                start = k
                while k + 1 < len(row) and row[k + 1] == row[k] + 1 and k + 1 - start < 0xFFF:
                    k += 1
                run = row[start:k + 1]
                b += struct.pack('<I', ((run[0] & 0x3FF) << 22) | ((y & 0x3FF) << 12) | len(run))
                b += bytes(index[to15(*px[(x, y)])] for x in run)
                k += 1
        b += struct.pack('<I', END)
        bodies.append(bytes(b))
    head = struct.pack('<256H', *pal) + struct.pack('<i', len(frames))
    offs, at = [], 4 + 4 * len(frames)
    for body in bodies:
        offs.append(at)
        at += len(body)
    return head + struct.pack('<%di' % len(frames), *offs) + b''.join(bodies)


def decode_block(data):
    """The client's reading of one block (ReadMULAnimationFrames, ReadSpriteData): per frame a dict
    {(x, y): (r, g, b)} in run coordinates, colours expanded through TABLE."""
    pal = struct.unpack_from('<256H', data, 0)
    n = struct.unpack_from('<i', data, 512)[0]
    if n <= 0 or n > 1000:
        raise SystemExit('a block says it has %d frames' % n)
    offs = struct.unpack_from('<%di' % n, data, 516)
    out = []
    for o in offs:
        q = 512 + o
        cx, cy, w, h = struct.unpack_from('<hhhh', data, q)
        q += 8
        px = {}
        while True:
            hd = struct.unpack_from('<I', data, q)[0]
            q += 4
            if hd == END:
                break
            ln = hd & 0xFFF
            x = (hd >> 22) & 0x3FF
            y = (hd >> 12) & 0x3FF
            x = x - 0x400 if x & 0x200 else x
            y = y - 0x400 if y & 0x200 else y
            for j in range(ln):
                ix, iy = x + j + cx, y + cy + h
                if not (0 <= ix < w and 0 <= iy < h):
                    raise SystemExit('a run pixel falls outside its frame (%d,%d in %dx%d)' % (ix, iy, w, h))
                px[(x + j, y)] = to24(pal[data[q + j]] & 0x7FFF)
            q += ln
        out.append(px)
    return out


def read_records():
    """(equipAnims, bodyAnims) from records.json."""
    with open(os.path.join(ART, 'records.json'), encoding='ascii') as f:
        j = json.load(f)
    return j.get('equipAnims', []), j.get('bodyAnims', [])


def check_slots(equip, bodies):
    slots = [int(e['slot']) for e in equip] + [int(b['slot']) for b in bodies]
    if len(set(slots)) != len(slots):
        raise SystemExit('anim7 slots must be distinct: %s' % slots)
    for e in equip:
        if int(e['slot']) < 400:
            raise SystemExit('equipAnims slot %d: a worn item needs a people slot, 400 or more' % int(e['slot']))
    for b in bodies:
        if slot_layout(int(b['slot'])) is None:
            raise SystemExit('bodyAnims body %d: slot %d is outside 1..2047' % (int(b['body']), int(b['slot'])))
        if not 0 <= int(b['body']) < 0x1000:
            raise SystemExit('bodyAnims body %d is outside the server\'s bodies (0..0xFFF, Body.cs:49)' % int(b['body']))


def build():
    equip, bodies = read_records()
    if not equip and not bodies:
        raise SystemExit('records.json has no equipAnims and no bodyAnims')
    check_slots(equip, bodies)
    count = max(slot_end(int(x['slot'])) for x in equip + bodies)
    idx = [(-1, 0, 0)] * count
    mul = bytearray()
    sources = {}
    # Every block in slot order, so the file is the same whatever the records' order.
    plan = [('equip', e) for e in equip] + [('body', b) for b in bodies]
    for kind, e in sorted(plan, key=lambda ke: int(ke[1]['slot'])):
        slot = int(e['slot'])
        if kind == 'equip':
            frames = load_frames(e['anim'])
            blocks = {k: encode_block(v) for k, v in frames.items()}
            sources[('anim', int(e['anim']))] = frames
        else:
            layout, blocks = load_vd(os.path.join(ART, e['vd'].replace('/', os.sep)))
            if layout != slot_layout(slot):
                raise SystemExit('bodyAnims body %d: %s is a %s .vd but anim7 slot %d is %s'
                                 % (int(e['body']), e['vd'], layout, slot, slot_layout(slot)))
            sources[('body', int(e['body']))] = blocks
        actions = LAYOUTS[slot_layout(slot)][2]
        for (a, d) in sorted(blocks):
            if a >= actions or d >= DIRECTIONS:
                raise SystemExit('slot %d: action %d direction %d is outside the %s layout' % (slot, a, d, slot_layout(slot)))
            data = blocks[(a, d)]
            idx[slot_start(slot) + a * 5 + d] = (len(mul), len(data), 0)
            mul += data
    return bytes(mul), b''.join(struct.pack('<iii', *t) for t in idx), sources


def read_slot(mul, idx, slot, a, d):
    k = slot_start(slot) + a * 5 + d
    if len(idx) < 12 * (k + 1):
        return None
    pos, size, _ = struct.unpack_from('<iii', idx, k * 12)
    if pos == -1 or size <= 0:
        return None
    return mul[pos:pos + size]


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args()
    mul, idx, sources = build()
    if not a.check:
        with open(MUL_OUT, 'wb') as f:
            f.write(mul)
        with open(IDX_OUT, 'wb') as f:
            f.write(idx)
        print('wrote %s (%d bytes) and %s (%d bytes, %d entries)' % (MUL_OUT, len(mul), IDX_OUT, len(idx), len(idx) // 12))
        return 0
    fail = []
    for path, want in ((MUL_OUT, mul), (IDX_OUT, idx)):
        have = open(path, 'rb').read() if os.path.exists(path) else None
        if have != want:
            fail.append('%s is not what the sources build (run without --check, then commit)' % os.path.basename(path))
    have_mul = open(MUL_OUT, 'rb').read() if os.path.exists(MUL_OUT) else b''
    have_idx = open(IDX_OUT, 'rb').read() if os.path.exists(IDX_OUT) else b''
    equip, bodies = read_records()
    worst = 0
    for e in equip:
        for (act, d), frames in sorted(sources[('anim', int(e['anim']))].items()):
            blk = read_slot(have_mul, have_idx, int(e['slot']), act, d)
            if blk is None:
                fail.append('anim %d action %d direction %d: no block in the committed files' % (e['anim'], act, d))
                continue
            got = decode_block(blk)
            if len(got) != len(frames):
                fail.append('anim %d action %d direction %d: %d frames, the PNGs have %d' % (e['anim'], act, d, len(got), len(frames)))
                continue
            for i, (g, s) in enumerate(zip(got, frames)):
                if set(g) != set(s):
                    fail.append('anim %d a%02d d%d f%02d: pixel positions differ from the PNG' % (e['anim'], act, d, i))
                    continue
                for p, c in s.items():
                    if g[p] != to24(to15(*c)):
                        fail.append('anim %d a%02d d%d f%02d: pixel %s decodes to %s' % (e['anim'], act, d, i, p, g[p]))
                        break
                    worst = max(worst, max(abs(x - y) for x, y in zip(g[p], c)))
            print('anim %d slot %d action %d direction %d: %d frames, positions identical, colours within %d of the PNGs'
                  % (e['anim'], e['slot'], act, d, len(got), worst))
    for b in bodies:
        body, slot = int(b['body']), int(b['slot'])
        n_frames = n_blocks = 0
        for (act, d), src_blk in sorted(sources[('body', body)].items()):
            blk = read_slot(have_mul, have_idx, slot, act, d)
            if blk is None:
                fail.append('body %d action %d direction %d: no block in the committed files' % (body, act, d))
                continue
            got, want = decode_block(blk), decode_block(src_blk)
            if blk != src_blk:
                fail.append('body %d action %d direction %d: the committed block is not the .vd\'s bytes' % (body, act, d))
            if len(got) != len(want):
                fail.append('body %d action %d direction %d: %d frames, the .vd has %d' % (body, act, d, len(got), len(want)))
                continue
            for i, (g, s) in enumerate(zip(got, want)):
                if g != s:
                    fail.append('body %d a%02d d%d f%02d: pixels differ from the .vd' % (body, act, d, i))
            n_frames += len(got)
            n_blocks += 1
        # Nothing else of ours in the slot: every entry of the slot either names one of the .vd's blocks or is empty.
        for k in range(slot_start(slot), slot_end(slot)):
            if len(have_idx) >= 12 * (k + 1):
                pos, size, _ = struct.unpack_from('<iii', have_idx, k * 12)
                a, d = (k - slot_start(slot)) // 5, (k - slot_start(slot)) % 5
                if (pos != -1 and size > 0) != ((a, d) in sources[('body', body)]):
                    fail.append('body %d: idx entry %d (action %d direction %d) does not match the .vd' % (body, k, a, d))
        print('body %d slot %d (%s): %d blocks, %d frames, every block the .vd\'s bytes and every pixel identical'
              % (body, slot, slot_layout(slot), n_blocks, n_frames))
    for x in fail:
        print('FAIL: ' + x)
    print('anim7 %s' % ('does NOT match' if fail else 'matches'))
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
