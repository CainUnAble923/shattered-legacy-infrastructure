#!/usr/bin/env python3
r"""
art_process.py: the image work of the art pipeline (cc-P152), called by ArtLib.ps1. Pillow and numpy.

    python -P art_process.py place   --master M.png --slot slot.json --out-dir DIR
    python -P art_process.py preview --master M.png --slots slots.json --out P.png
    python -P art_process.py check   --file F.png --slot slot.json

Every command prints one JSON object (ok, why, ...). A slot is ArtLib's: file, sizes [[w, h], ...], kind (static,
animated, tile, band), frames, fps, animation (pulse-turn, halo-sparks, spinner), transparent, budgetKB, margin,
glow [r, g, b], toneLow / toneHigh [r, g, b].

place writes <file> (the first size) and <stem>@<w>x<h>.png for the others into DIR, after:
  trim     transparent slots are cut to the object (alpha > 8) and centred with the slot's margin of clear pixels
  resize   Lanczos on premultiplied alpha, so no dark fringe shows on the stone
  tone     tile and band slots with toneLow/toneHigh are remapped linearly into that range
  strip    animated slots are built from the one still (below), frame 1 on the left, width = frames x height
  checks   transparent: the outer margin ring is fully clear; opaque: no alpha below 255; tile: left/right and
           top/bottom edges continue each other; band: left/right do
  budget   the PNG is optimised; over budget it is quantised (256, 128, 64 colours) and the report says so
Animations from one still: pulse-turn (a crystal: a faked turn by scaling X with a highlight sweeping across, and a
glow pulse), halo-sparks (the Golem: a breathing halo and sparks drifting up), spinner (eight copies of a rune in a
ring, one lit, stepping round).
"""
import argparse
import io
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

ALPHA_SOLID = 8


def load_rgba(path):
    im = Image.open(path)
    im.load()
    return im.convert('RGBA')


# ---- geometry ---------------------------------------------------------------------------------------------------

def object_bbox(im, threshold=ALPHA_SOLID):
    a = np.asarray(im)[:, :, 3]
    ys, xs = np.nonzero(a > threshold)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def trim_to_object(im, pad_fraction=0.0):
    """Crop to the object and centre it on a square transparent canvas, with pad_fraction of the side around it."""
    box = object_bbox(im)
    if box is None:
        return im.copy()
    obj = im.crop(box)
    side = max(obj.width, obj.height)
    side = int(math.ceil(side * (1 + 2 * pad_fraction)))
    canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    canvas.paste(obj, ((side - obj.width) // 2, (side - obj.height) // 2))
    return canvas


def premultiply(im):
    a = np.asarray(im).astype(np.float32)
    a[:, :, :3] *= a[:, :, 3:4] / 255.0
    return a


def unpremultiply(a):
    out = a.copy()
    al = out[:, :, 3:4]
    with np.errstate(divide='ignore', invalid='ignore'):
        out[:, :, :3] = np.where(al > 0, out[:, :, :3] * 255.0 / np.maximum(al, 1e-6), 0)
    return Image.fromarray(np.clip(np.rint(out), 0, 255).astype(np.uint8), 'RGBA')


def resize_rgba(im, size):
    """Lanczos on premultiplied alpha (each channel resized as a float image), so edges carry no dark fringe."""
    w, h = size
    a = premultiply(im)
    chans = [Image.fromarray(a[:, :, i], 'F').resize((w, h), Image.LANCZOS) for i in range(4)]
    return unpremultiply(np.stack([np.asarray(c) for c in chans], axis=2))


def fit_with_margin(im, size, margin):
    """The object scaled to fit inside size minus margin on every side, centred, transparent around it."""
    w, h = size
    iw, ih = w - 2 * margin, h - 2 * margin
    scale = min(iw / im.width, ih / im.height)
    nw, nh = max(1, round(im.width * scale)), max(1, round(im.height * scale))
    small = resize_rgba(im, (nw, nh))
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    canvas.paste(small, ((w - nw) // 2, (h - nh) // 2))
    return clear_margin(canvas, margin)


def clear_margin(im, margin):
    if margin <= 0:
        return im
    a = np.asarray(im).copy()
    a[:margin, :, 3] = 0
    a[-margin:, :, 3] = 0
    a[:, :margin, 3] = 0
    a[:, -margin:, 3] = 0
    a[a[:, :, 3] == 0, :3] = 0
    return Image.fromarray(a, 'RGBA')


def cover(im, size):
    """Opaque slots: scale to cover the size and crop the centre (textures and bands have no object to trim)."""
    w, h = size
    scale = max(w / im.width, h / im.height)
    nw, nh = max(w, round(im.width * scale)), max(h, round(im.height * scale))
    big = im.convert('RGB').resize((nw, nh), Image.LANCZOS)
    x, y = (nw - w) // 2, (nh - h) // 2
    return big.crop((x, y, x + w, y + h))


# ---- tone -------------------------------------------------------------------------------------------------------

def remap_tone(im, low, high):
    """Each channel mapped linearly from the image's own range (0.2 to 99.8 percentile) into [low, high]."""
    a = np.asarray(im.convert('RGB')).astype(np.float32)
    out = np.empty_like(a)
    for c in range(3):
        lo_s, hi_s = np.percentile(a[:, :, c], 0.2), np.percentile(a[:, :, c], 99.8)
        span = max(hi_s - lo_s, 1.0)
        out[:, :, c] = low[c] + (np.clip(a[:, :, c], lo_s, hi_s) - lo_s) * (high[c] - low[c]) / span
    return Image.fromarray(np.clip(np.rint(out), 0, 255).astype(np.uint8), 'RGB')


def tone_bounds(im):
    a = np.asarray(im.convert('RGB'))
    return [int(v) for v in a.reshape(-1, 3).min(axis=0)], [int(v) for v in a.reshape(-1, 3).max(axis=0)]


# ---- checks -----------------------------------------------------------------------------------------------------

def check_clear_edges(im, margin):
    """Transparent slots: every pixel in the outer margin ring (at least 1 px) has alpha 0."""
    m = max(1, margin)
    a = np.asarray(im.convert('RGBA'))[:, :, 3]
    ring = np.concatenate([a[:m, :].ravel(), a[-m:, :].ravel(), a[:, :m].ravel(), a[:, -m:].ravel()])
    worst = int(ring.max()) if ring.size else 0
    return {'name': 'clear edges', 'ok': worst == 0, 'detail': 'max alpha %d in the outer %d px' % (worst, m)}


def check_opaque(im):
    if im.mode not in ('RGBA', 'LA', 'PA') and 'transparency' not in im.info:
        return {'name': 'opaque', 'ok': True, 'detail': 'no alpha channel'}
    a = np.asarray(im.convert('RGBA'))[:, :, 3]
    lo = int(a.min())
    return {'name': 'opaque', 'ok': lo == 255, 'detail': 'min alpha %d' % lo}


def seam_ratio(im, axis):
    """How far the wrap-around seam differs from an ordinary neighbouring pair: 1.0 is as smooth as the inside."""
    a = np.asarray(im.convert('RGB')).astype(np.float32)
    if axis == 'x':
        seam = np.abs(a[:, 0, :] - a[:, -1, :]).mean()
        inside = np.abs(np.diff(a, axis=1)).mean()
    else:
        seam = np.abs(a[0, :, :] - a[-1, :, :]).mean()
        inside = np.abs(np.diff(a, axis=0)).mean()
    return float(seam / max(inside, 0.5))


def check_seamless(im, axes, limit=2.0):
    out = []
    for ax in axes:
        r = seam_ratio(im, ax)
        out.append({'name': 'seamless ' + ('left-right' if ax == 'x' else 'top-bottom'), 'ok': r <= limit,
                    'detail': 'seam %.2f x an inside step (limit %.1f)' % (r, limit)})
    return out


def check_strip(im, frames):
    ok = im.width == frames * im.height
    return {'name': 'strip shape', 'ok': ok, 'detail': '%dx%d for %d frames' % (im.width, im.height, frames)}


def check_tone(im, low, high):
    lo, hi = tone_bounds(im)
    ok = all(lo[c] >= low[c] and hi[c] <= high[c] for c in range(3))
    return {'name': 'tone range', 'ok': ok, 'detail': 'pixels %s to %s, allowed %s to %s' % (lo, hi, list(low), list(high))}


# ---- budget -----------------------------------------------------------------------------------------------------

def png_bytes(im):
    buf = io.BytesIO()
    im.save(buf, 'PNG', optimize=True)
    return buf.getvalue()


def fit_budget(im, budget_bytes):
    """(bytes, how): the optimised PNG, or quantised to fewer colours until it fits; how says what was done."""
    data = png_bytes(im)
    if len(data) <= budget_bytes:
        return data, 'fits as it is'
    for colours in (256, 128, 64):
        if im.mode == 'RGBA':
            q = im.quantize(colors=colours, method=Image.FASTOCTREE)
        else:
            q = im.convert('RGB').quantize(colors=colours, method=Image.MEDIANCUT)
        data = png_bytes(q)
        if len(data) <= budget_bytes:
            return data, 'reduced to %d colours to fit' % colours
    return data, 'OVER BUDGET even at 64 colours'


# ---- animations from one still ----------------------------------------------------------------------------------

def soft_glow(alpha, radius, colour, strength):
    """A coloured halo from an alpha mask: the mask blurred, tinted, at strength (0 to 1)."""
    g = Image.fromarray(alpha, 'L').filter(ImageFilter.GaussianBlur(radius))
    ga = (np.asarray(g).astype(np.float32) * strength).clip(0, 255).astype(np.uint8)
    out = np.zeros(alpha.shape + (4,), np.uint8)
    out[:, :, 0], out[:, :, 1], out[:, :, 2] = colour
    out[:, :, 3] = ga
    return Image.fromarray(out, 'RGBA')


def brighten(im, factor):
    a = np.asarray(im).astype(np.float32)
    a[:, :, :3] = np.clip(a[:, :, :3] * factor, 0, 255)
    return Image.fromarray(a.astype(np.uint8), 'RGBA')


def frames_pulse_turn(still, n, glow):
    """A crystal turning: X scaled by |cos| (it never goes flat: 0.35 at the edge-on point), mirrored on the back half,
    a highlight band sweeping across, and the glow breathing once per turn."""
    side = still.width
    out = []
    for i in range(n):
        t = 2 * math.pi * i / n
        sx = 0.35 + 0.65 * abs(math.cos(t))
        body = still if math.cos(t) >= 0 else still.transpose(Image.FLIP_LEFT_RIGHT)
        w = max(2, round(side * sx))
        body = resize_rgba(body, (w, side))
        a = np.asarray(body).astype(np.float32)
        xs = np.linspace(-1, 1, w)[None, :]
        centre = math.sin(t) * 0.8
        band = np.exp(-((xs - centre) ** 2) / 0.04) * 0.45
        a[:, :, :3] = np.clip(a[:, :, :3] + band[:, :, None] * 255 * (a[:, :, 3:4] / 255.0), 0, 255)
        body = Image.fromarray(a.astype(np.uint8), 'RGBA')
        frame = Image.new('RGBA', (side, side), (0, 0, 0, 0))
        pulse = 0.55 + 0.35 * (0.5 + 0.5 * math.cos(t))
        alpha = np.zeros((side, side), np.uint8)
        alpha[:, (side - w) // 2:(side - w) // 2 + w] = np.asarray(body)[:, :, 3]
        if glow:
            frame.alpha_composite(soft_glow(alpha, side * 0.06, glow, pulse))
        frame.alpha_composite(body, ((side - w) // 2, 0))
        out.append(frame)
    return out


def frames_halo_sparks(still, n, glow, seed=152):
    """The Golem: the halo breathes brighter and dimmer, and a few sparks drift up and fade, looping cleanly."""
    side = still.width
    rng = np.random.default_rng(seed)
    alpha = np.asarray(still)[:, :, 3]
    box = object_bbox(still) or (0, 0, side, side)
    sparks = [(rng.uniform(box[0], box[2]), rng.uniform(box[1] + (box[3] - box[1]) * 0.2, box[3]), rng.uniform(0, 1)) for _ in range(7)]
    rise = (box[3] - box[1]) * 0.5
    out = []
    for i in range(n):
        t = i / n
        frame = Image.new('RGBA', (side, side), (0, 0, 0, 0))
        breath = 0.45 + 0.4 * (0.5 + 0.5 * math.sin(2 * math.pi * t))
        frame.alpha_composite(soft_glow(alpha, side * 0.05, glow or (255, 196, 96), breath))
        frame.alpha_composite(still)
        sp = np.asarray(frame).astype(np.float32).copy()
        yy, xx = np.mgrid[0:side, 0:side]
        for (x0, y0, ph) in sparks:
            p = (t + ph) % 1.0
            x, y = x0 + math.sin(p * 6.28) * side * 0.01, y0 - p * rise
            fade = math.sin(p * math.pi)
            d = np.exp(-((xx - x) ** 2 + (yy - y) ** 2) / max(1.0, (side * 0.008) ** 2)) * fade
            col = np.array(glow or (255, 196, 96), np.float32)
            sp[:, :, :3] = sp[:, :, :3] * (1 - d[:, :, None]) + col * d[:, :, None]
            sp[:, :, 3] = np.maximum(sp[:, :, 3], d * 255)
        out.append(Image.fromarray(sp.clip(0, 255).astype(np.uint8), 'RGBA'))
    return out


def frames_spinner(rune, n, side, glow):
    """Eight copies of the rune round a ring, dim, one lit bright with a glow, stepping one place per frame."""
    mark = side * 0.24
    rune = trim_to_object(rune)
    small = resize_rgba(rune, (max(2, round(mark)), max(2, round(mark))))
    dim = brighten(small, 0.45)
    da = np.asarray(dim).copy()
    da[:, :, 3] = (da[:, :, 3] * 0.75).astype(np.uint8)
    dim = Image.fromarray(da, 'RGBA')
    lit = brighten(small, 1.35)
    r = side * 0.31
    out = []
    for i in range(n):
        frame = Image.new('RGBA', (side, side), (0, 0, 0, 0))
        for k in range(8):
            ang = -math.pi / 2 + 2 * math.pi * k / 8
            cx, cy = side / 2 + r * math.cos(ang), side / 2 + r * math.sin(ang)
            pos = (round(cx - small.width / 2), round(cy - small.height / 2))
            if k == i % 8:
                al = np.zeros((side, side), np.uint8)
                sa = np.asarray(lit)[:, :, 3]
                x0, y0 = pos
                al[max(0, y0):y0 + sa.shape[0], max(0, x0):x0 + sa.shape[1]] = sa[max(0, -y0):side - y0, max(0, -x0):side - x0]
                frame.alpha_composite(soft_glow(al, side * 0.04, glow or (255, 200, 90), 0.9))
                frame.alpha_composite(lit, pos)
            else:
                frame.alpha_composite(dim, pos)
        out.append(frame)
    return out


def build_frames(still, slot, side):
    kind = slot.get('animation') or ''
    n = int(slot.get('frames') or 1)
    glow = tuple(slot['glow']) if slot.get('glow') else None
    if kind == 'pulse-turn':
        return frames_pulse_turn(still, n, glow)
    if kind == 'halo-sparks':
        return frames_halo_sparks(still, n, glow)
    if kind == 'spinner':
        return frames_spinner(still, n, side, glow)
    raise ValueError('no animation named %r (pulse-turn, halo-sparks, spinner)' % kind)


def strip(frames):
    h = frames[0].height
    out = Image.new('RGBA', (h * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        out.alpha_composite(f, (i * h, 0))
    return out


# ---- one slot ---------------------------------------------------------------------------------------------------

def render_slot(master, slot, size):
    """The slot at one size, as an image, before the budget."""
    w, h = size
    kind = slot.get('kind', 'static')
    margin = int(slot.get('margin', 2)) if slot.get('transparent', True) else 0
    if kind in ('tile', 'band'):
        img = cover(master, size)
        if slot.get('toneLow') and slot.get('toneHigh'):
            img = remap_tone(img, slot['toneLow'], slot['toneHigh'])
        return img
    if not slot.get('transparent', True):
        return cover(master, size)
    if kind == 'animated':
        # Work at four times the size so the effects stay smooth, then each frame comes down with the same resampler.
        work = max(w, h) * 4
        pad = 0.0 if slot.get('animation') == 'spinner' else 0.14
        still = fit_with_margin(trim_to_object(master, pad), (work, work), 0)
        frames = build_frames(still, slot, work)
        return strip([clear_margin(resize_rgba(f, (w, h)), margin) for f in frames])
    return fit_with_margin(trim_to_object(master), size, margin)


def run_checks(img, slot):
    kind = slot.get('kind', 'static')
    checks = []
    if kind == 'animated':
        n = int(slot.get('frames') or 1)
        checks.append(check_strip(img, n))
        h = img.height
        for i in range(n):
            c = check_clear_edges(img.crop((i * h, 0, (i + 1) * h, h)), int(slot.get('margin', 2)))
            if not c['ok']:
                c['name'] = 'clear edges, frame %d' % (i + 1)
                checks.append(c)
                break
        else:
            checks.append({'name': 'clear edges', 'ok': True, 'detail': 'every frame'})
    elif slot.get('transparent', True):
        checks.append(check_clear_edges(img, int(slot.get('margin', 2))))
    else:
        checks.append(check_opaque(img))
    if kind == 'tile':
        checks += check_seamless(img, ('x', 'y'))
    if kind == 'band':
        checks += check_seamless(img, ('x',))
    if kind in ('tile', 'band') and slot.get('toneLow') and slot.get('toneHigh'):
        checks.append(check_tone(img, slot['toneLow'], slot['toneHigh']))
    return checks


def place(master_path, slot, out_dir):
    master = load_rgba(master_path)
    os.makedirs(out_dir, exist_ok=True)
    stem = os.path.splitext(slot['file'])[0]
    budget = int(slot.get('budgetKB', 100)) * 1024
    outputs = []
    primary = None
    for i, (w, h) in enumerate(slot['sizes']):
        img = render_slot(master, slot, (w, h))
        checks = run_checks(img, slot)
        data, how = fit_budget(img, budget)
        name = slot['file'] if i == 0 else '%s@%dx%d.png' % (stem, w, h)
        path = os.path.join(out_dir, name)
        with open(path, 'wb') as f:
            f.write(data)
        within = len(data) <= budget
        checks.append({'name': 'budget', 'ok': within, 'detail': '%d bytes of %d: %s' % (len(data), budget, how)})
        outputs.append({'file': name, 'path': path, 'size': [w, h], 'bytes': len(data), 'checks': checks})
        if i == 0:
            primary = outputs[-1]
    bad = [c['name'] + ' (' + c['detail'] + ')' for o in outputs for c in o['checks'] if not c['ok']]
    return {'ok': not bad, 'why': ('failed: ' + '; '.join(bad)) if bad else 'all checks passed', 'file': slot['file'],
            'primary': primary['path'], 'bytes': primary['bytes'], 'checks': primary['checks'], 'outputs': outputs}


def preview(master_path, slots, out_path):
    """The variant on slate: the master, then every slot at every size (strips frame by frame), at 2x for small ones."""
    master = load_rgba(master_path)
    tiles = []
    thumb = fit_with_margin(trim_to_object(master), (160, 160), 4)
    tiles.append(thumb)
    for slot in slots:
        for (w, h) in slot['sizes']:
            img = render_slot(master, slot, (w, h)).convert('RGBA')
            z = 2 if max(w, h) <= 64 else 1
            if img.width * z > 1024:
                z = max(1, 1024 // img.width)
            tiles.append(img.resize((img.width * z, img.height * z), Image.NEAREST))
    pad = 8
    width = min(1100, max(t.width for t in tiles) + 2 * pad)
    rows, x, y, rh = [], pad, pad, 0
    for t in tiles:
        if x + t.width + pad > width and x > pad:
            x, y = pad, y + rh + pad
            rh = 0
        rows.append((t, x, y))
        x += t.width + pad
        rh = max(rh, t.height)
    sheet = Image.new('RGBA', (width, y + rh + pad), (40, 42, 47, 255))
    for t, x, y in rows:
        sheet.alpha_composite(t, (x, y))
    sheet.convert('RGB').save(out_path, 'PNG', optimize=True)
    return {'ok': True, 'why': 'preview written', 'preview': out_path}


def check_file(path, slot):
    img = Image.open(path)
    img.load()
    checks = run_checks(img if slot.get('kind') in ('tile', 'band') or not slot.get('transparent', True) else img.convert('RGBA'), slot)
    w, h = slot['sizes'][0]
    if slot.get('kind') == 'animated':
        size_ok = img.height == h and img.width == h * int(slot.get('frames') or 1)
    else:
        size_ok = img.size == (w, h)
    checks.append({'name': 'size', 'ok': size_ok, 'detail': '%dx%d, the slot is %dx%d%s' % (img.width, img.height, w, h, ' per frame' if slot.get('kind') == 'animated' else '')})
    n = os.path.getsize(path)
    budget = int(slot.get('budgetKB', 100)) * 1024
    checks.append({'name': 'budget', 'ok': n <= budget, 'detail': '%d bytes of %d' % (n, budget)})
    bad = [c['name'] + ' (' + c['detail'] + ')' for c in checks if not c['ok']]
    return {'ok': not bad, 'why': ('failed: ' + '; '.join(bad)) if bad else 'all checks passed', 'file': path, 'checks': checks}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    p = sub.add_parser('place')
    p.add_argument('--master', required=True)
    p.add_argument('--slot', required=True)
    p.add_argument('--out-dir', required=True)
    v = sub.add_parser('preview')
    v.add_argument('--master', required=True)
    v.add_argument('--slots', required=True)
    v.add_argument('--out', required=True)
    c = sub.add_parser('check')
    c.add_argument('--file', required=True)
    c.add_argument('--slot', required=True)
    a = ap.parse_args(argv)
    try:
        if a.cmd == 'place':
            r = place(a.master, json.load(open(a.slot, encoding='utf-8-sig')), a.out_dir)
        elif a.cmd == 'preview':
            s = json.load(open(a.slots, encoding='utf-8-sig'))
            r = preview(a.master, s if isinstance(s, list) else [s], a.out)
        else:
            r = check_file(a.file, json.load(open(a.slot, encoding='utf-8-sig')))
    except Exception as e:  # one JSON answer, whatever happened
        r = {'ok': False, 'why': '%s: %s' % (type(e).__name__, e)}
    print(json.dumps(r))
    return 0 if r.get('ok') else 1


if __name__ == '__main__':
    sys.exit(main())
