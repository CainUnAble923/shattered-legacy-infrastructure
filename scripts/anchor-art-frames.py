#!/usr/bin/env python3
r"""
anchor-art-frames.py: turn one animation's PNG frames into TazUO art overrides named by art ID (cc-P26, F-8).

    python scripts/anchor-art-frames.py --src D:\UO\third-party\animated-runestone\frames\stone1 \
        --first-id 15376 --out player-package\vendor\shattered-legacy-art\art --bottom-pad 22

Frames are taken in file-name order and written as <first-id + n>.png (decimal: TazUO reads a name as
decimal unless it starts with 0x, ExternalImageLoader.TryParseId).

How TazUO places a static (View.cs:25-26 and DrawStaticAnimated): the image's column Width/2 goes on the
tile's centre and its bottom row on the tile's bottom tip, 44 pixels below the tile's top. So, the same for
every frame of the animation, so the frames stay registered:
  - fully transparent columns and rows around the union of all frames are cut away (no pixel that has any
    alpha is touched),
  - which centres the union horizontally (to half a pixel),
  - --bottom-pad transparent rows go under it. 22 puts the lowest pixel on the tile's centre, which suits
    art that floats; 0 suits art that stands on the ground with its lowest pixel at the tile's front tip.
Never scaled. The tool re-reads every file it wrote and checks the art inside is the source's, pixel for
pixel, or it fails.

ASCII only.
"""
import argparse, glob, os, sys

import numpy as np
from PIL import Image


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', required=True, help='folder of PNG frames, in name order')
    ap.add_argument('--first-id', required=True, type=lambda s: int(s, 0))
    ap.add_argument('--out', required=True)
    ap.add_argument('--bottom-pad', type=int, default=0)
    ap.add_argument('--overwrite', action='store_true', help='replace files already in --out')
    a = ap.parse_args()

    files = sorted(glob.glob(os.path.join(a.src, '*.png')))
    if not files:
        sys.exit('no PNG frames in %s' % a.src)
    frames = [np.array(Image.open(f).convert('RGBA')) for f in files]
    if len({f.shape for f in frames}) != 1:
        sys.exit('frames differ in size: %s' % sorted({f.shape for f in frames}))
    union = np.zeros(frames[0].shape[:2], bool)
    for f in frames:
        union |= f[:, :, 3] > 0
    ys, xs = np.nonzero(union)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    cw, ch = x1 - x0 + 1, y1 - y0 + 1
    # Cut to the union, the union's centre is (cw - 1) / 2 and TazUO centres on cw >> 1: the same column
    # for an odd width, half a pixel off for an even one. No padding column is needed either way.
    left = 0
    width = cw
    height = ch + a.bottom_pad

    os.makedirs(a.out, exist_ok=True)
    written = []
    for n, (f, src) in enumerate(zip(frames, files)):
        out = np.zeros((height, width, 4), np.uint8)
        out[0:ch, left:left + cw] = f[y0:y1 + 1, x0:x1 + 1]
        p = os.path.join(a.out, '%d.png' % (a.first_id + n))
        if os.path.exists(p) and not a.overwrite:
            sys.exit('%s exists; not overwriting it (use --overwrite)' % p)
        Image.fromarray(out, 'RGBA').save(p, optimize=True)
        back = np.array(Image.open(p).convert('RGBA'))
        inner = back[0:ch, left:left + cw]
        if not np.array_equal(inner, f[y0:y1 + 1, x0:x1 + 1]) or back[:, :, 3].sum() != f[:, :, 3].sum():
            sys.exit('%s does not hold %s pixel for pixel' % (p, src))
        written.append((os.path.basename(src), os.path.basename(p)))
    print('%s: %d frames %dx%d -> %dx%d (cut to x %d-%d, y %d-%d of the source; bottom pad %d)'
          % (a.src, len(files), frames[0].shape[1], frames[0].shape[0], width, height, x0, x1, y0, y1,
             a.bottom_pad))
    for s, d in written:
        print('  %s -> %s' % (s, d))
    return 0


if __name__ == '__main__':
    sys.exit(main())
