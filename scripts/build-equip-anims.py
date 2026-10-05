#!/usr/bin/env python3
r"""
build-equip-anims.py: our worn-item animations, encoded for TazUO as an extra anim file (cc-P62, F-35).

    python scripts/build-equip-anims.py            write vendor\shattered-legacy-art\sl-anim7.bin and sl-anim7-idx.bin
    python scripts/build-equip-anims.py --check    rebuild in memory; fail unless the committed files are identical,
                                                   and decode them back and compare with the PNGs

Why a seventh anim file. A worn item's tiledata names an Animation number N; TazUO draws body N's frames over the
wearer (MobileView.Draw). EA's own files hold anim.mul and anim2..anim6.mul, and TazUO opens anim.mul up to
anim10.mul, each through the -uofilesoverride list (AnimationsLoader.Load). A Bodyconv.def line "N -1 -1 -1 -1 -1 S"
sends body N to slot S of anim7.mul (AnimationsLoader.ProcessBodyConvDef: column i is anim<i+1>; no expansion flag
is checked past column 2). So the package ships anim7 as two files that are entirely ours, and the launcher adds
the one Bodyconv.def line to a copy of the player's own file (app\Build-UoOverrides.ps1). Nothing of EA's ships.

Slot S must be 400 or more: TazUO types a body in anim7 by its slot (CalculateTypeByGraphic: 400 and up is
Human), and it only draws a worn item whose type is Human or Equipment (Animations.GetAnimationFrames, isEquip).
The people layout then puts slot S at index entries 35000 + (S - 400) x 175, 35 actions x 5 directions
(CalculatePeopleGroupOffset). Every other entry is empty (position -1).

Inputs, all ours:
  records.json "equipAnims": [{"anim": N, "slot": S, ...}]
  anim-src\<N>\a<action>_d<direction>_f<frame>.png    130 x 128, fully transparent or fully opaque pixels.
      Canvas pixel (x, y) is (x - 67, y - 98) in the frame's own coordinates, the ones a run header holds; that is
      also the wearer's (the art was drawn over body 401's frames in that frame). Frames of one action and
      direction are numbered from 0 with no gaps.

The format (anim.mul's; AnimationsLoader.ReadMULAnimationFrames and ReadSpriteData), one block per action and
direction: 256 palette colours (uint16, 1555), int32 frame count, int32 frame offsets from the count, then per
frame int16 centerX, centerY, width, height and run headers (length in bits 0-11, y in 12-21, x in 22-31, both
signed 10-bit), each followed by its palette indices, ending 0x7FFF7FFF. The client puts a run pixel at image
column x + centerX, row y + centerY + height, and draws the image at the wearer's anchor minus (centerX,
height + centerY), so a pixel lands at the anchor plus its own (x, y) whatever box is chosen.

Colour: the format holds 5 bits a channel. Each PNG colour becomes the 15-bit colour whose expansion through
TazUO's table (HuesHelper.Color16To32, _table) is nearest, channel by channel. --check prints the largest
difference that leaves. ASCII only. Python 3 with Pillow.
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
    with open(os.path.join(ART, 'records.json'), encoding='ascii') as f:
        return json.load(f).get('equipAnims', [])


def build():
    entries = read_records()
    if not entries:
        raise SystemExit('records.json has no equipAnims')
    slots = [e['slot'] for e in entries]
    if len(set(slots)) != len(slots) or min(slots) < 400:
        raise SystemExit('equipAnims slots must be distinct and 400 or more: %s' % slots)
    count = 35000 + (max(slots) - 400 + 1) * 175
    idx = [(-1, 0, 0)] * count
    mul = bytearray()
    sources = {}
    for e in sorted(entries, key=lambda e: e['slot']):
        blocks = load_frames(e['anim'])
        sources[e['anim']] = blocks
        for (a, d) in sorted(blocks):
            data = encode_block(blocks[(a, d)])
            idx[35000 + (e['slot'] - 400) * 175 + a * 5 + d] = (len(mul), len(data), 0)
            mul += data
    return bytes(mul), b''.join(struct.pack('<iii', *t) for t in idx), sources


def read_slot(mul, idx, slot, a, d):
    pos, size, _ = struct.unpack_from('<iii', idx, (35000 + (slot - 400) * 175 + a * 5 + d) * 12)
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
            fail.append('%s is not what the PNGs build (run without --check, then commit)' % os.path.basename(path))
    have_mul = open(MUL_OUT, 'rb').read() if os.path.exists(MUL_OUT) else b''
    have_idx = open(IDX_OUT, 'rb').read() if os.path.exists(IDX_OUT) else b''
    worst = 0
    for e in read_records():
        for (act, d), frames in sorted(sources[e['anim']].items()):
            blk = read_slot(have_mul, have_idx, e['slot'], act, d) if len(have_idx) >= 12 * (35000 + (e['slot'] - 399) * 175) else None
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
    for x in fail:
        print('FAIL: ' + x)
    print('equip anims %s' % ('do NOT match' if fail else 'match'))
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
