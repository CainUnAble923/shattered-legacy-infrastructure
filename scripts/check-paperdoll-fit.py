#!/usr/bin/env python3
r"""
check-paperdoll-fit.py: our paperdoll gumps sit where they should on the bodies the client draws them over (cc-P138).

    python scripts/check-paperdoll-fit.py                 check every registered gump on every body set this machine has
    python scripts/check-paperdoll-fit.py --render DIR    also write the composites (body + hair + gump, body + hooded
                                                          robe + gump), 3x, for the SFW bodies only
    python scripts/check-paperdoll-fit.py --render-addons DIR
                                                          the same composites for the Topless and Nude add-on sets, into
                                                          a folder that must never be in a repo

Why. The fox ears and tail gump (64783, cc-P62) was drawn over EA's female body, whose head sits 25 rows lower than
the Siege of Sosaria body the shard ships (cc-P81, P103, P111). Drawn over the new body, the ears landed on her face
and the tail on her thighs (Chase's screenshot, 2026-10-16). TazUO draws every paperdoll layer at the same origin
(PaperDollInteractable.UpdateUI: GumpPic(0, 0, ...) for the body and each GumpPicEquipment), so a gump fits a body
only if its pixels are in the right place of the 260 x 237 canvas. This check says so before a package is built.

What it checks, from vendor\shattered-legacy-art\paperdoll-fit.json:
  - every registry.csv gump has a fit entry and every entry has a PNG, 260 x 237, alpha 0 or 255 only, no stray
    specks (components under 3 pixels);
  - the ears (the components that end above crown + 12 on every body the gump serves) stay out of the face box, end
    within [crown - 1, crown + 6] (they rise out of the head, not float or sink), and are centred on the head;
  - the tail (the largest other component) starts in the lower-back box: its root column, the one nearest the head's
    centre, lies inside the box, and no tail pixel is above the box's top;
  - each body's first opaque row is the fit's crown_y, so a changed body file is noticed;
  - for each add-on set present (client-addons\paperdoll-nsfw\<set>\), each body's silhouette edges inside the
    face box and the lower-back box are within a pixel of the SFW body's, row by row, and its crown row is the
    same, so the same numbers hold for it (the sets were exported separately and differ by a pixel along the
    outline here and there, never in shape).

Exit 0 when everything fits; 1 with one line per failure. ASCII only. Python 3 with Pillow.
"""
import argparse, csv, json, os, sys

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(REPO, 'player-package', 'vendor', 'shattered-legacy-art')
BODIES = os.path.join(REPO, 'player-package', 'vendor', 'paperdoll-sos', 'bodies-sfw')
KIT = os.path.join(REPO, 'player-package', 'vendor', 'paperdoll-sos', 'ExternalImages', 'gumps')
ADDONS = os.path.join(REPO, 'client-addons', 'paperdoll-nsfw')
CANVAS = (260, 237)
STRAY = 3          # a component smaller than this is a speck, not art
EARS_BELOW = 12    # a component ending within this many rows under the crown is an ear
EARS_SIT = (-1, 6)  # the ears' bottom row relative to the crown
EARS_CENTRE = 4    # the ears' centre column may be this far from the head's centre


def components(alpha):
    """Connected components (8-way) of the opaque pixels, largest first, as lists of (x, y)."""
    w, h = alpha.size
    data = alpha.load()
    px = {(x, y) for y in range(h) for x in range(w) if data[x, y]}
    seen, out = set(), []
    for p in sorted(px):
        if p in seen:
            continue
        stack, comp = [p], []
        seen.add(p)
        while stack:
            x, y = stack.pop()
            comp.append((x, y))
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1), (x + 1, y + 1), (x - 1, y - 1), (x + 1, y - 1), (x - 1, y + 1)):
                if n in px and n not in seen:
                    seen.add(n)
                    stack.append(n)
        out.append(comp)
    return sorted(out, key=len, reverse=True)


def bbox(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return min(xs), min(ys), max(xs), max(ys)


def boxes_cross(a, b):
    return not (a[2] < b[0] or b[2] < a[0] or a[3] < b[1] or b[3] < a[1])


def registry_gumps(path):
    with open(path, newline='') as f:
        rows = [r for r in csv.DictReader(l for l in f if not l.startswith('#'))]
    return sorted(int(r['id']) for r in rows if r.get('kind') == 'gump')


def silhouette(path):
    im = Image.open(path).convert('RGBA')
    return im, im.split()[3].point(lambda v: 255 if v else 0)


def extents(alpha, box):
    """Per row of the box, the leftmost and rightmost opaque column (None for an empty row)."""
    data = alpha.load()
    out = []
    for y in range(box[1], box[3] + 1):
        xs = [x for x in range(box[0], box[2] + 1) if data[x, y]]
        out.append((xs[0], xs[-1]) if xs else None)
    return out


def extents_differ(a, b, box, tol=1):
    """How many rows of the box have an edge more than tol columns apart between the two silhouettes."""
    bad = 0
    for ea, eb in zip(extents(a, box), extents(b, box)):
        if (ea is None) != (eb is None) or (ea and (abs(ea[0] - eb[0]) > tol or abs(ea[1] - eb[1]) > tol)):
            bad += 1
    return bad


class Fit:
    def __init__(self, art, bodies, kit, addons, fit_path):
        self.art, self.bodies_dir, self.kit, self.addons = art, bodies, kit, addons
        with open(fit_path) as f:
            self.fit = json.load(f)
        self.fail = []
        self.info = []

    def check(self):
        fit = self.fit
        registered = registry_gumps(os.path.join(self.art, 'registry.csv'))
        listed = sorted(int(g) for g in fit['gumps'])
        for g in registered:
            if g not in listed:
                self.fail.append('gump %d is in registry.csv but paperdoll-fit.json says nothing about where it sits' % g)
        for g in listed:
            if g not in registered:
                self.fail.append('gump %d is in paperdoll-fit.json but not in registry.csv' % g)

        bodies = {}
        for bid, spec in fit['bodies'].items():
            path = os.path.join(self.bodies_dir, '%s.png' % bid)
            if not os.path.isfile(path):
                self.fail.append('body %s: no %s' % (bid, path))
                continue
            im, alpha = silhouette(path)
            if im.size != CANVAS:
                self.fail.append('body %s is %dx%d, not %dx%d' % (bid, im.size[0], im.size[1], CANVAS[0], CANVAS[1]))
                continue
            crown = alpha.getbbox()[1]
            if crown != spec['crown_y']:
                self.fail.append('body %s: first opaque row is %d, paperdoll-fit.json says crown_y %d (the body file changed, or the fit is wrong)' % (bid, crown, spec['crown_y']))
            bodies[bid] = (im, alpha)

        for gid, gspec in fit['gumps'].items():
            path = os.path.join(self.art, 'gumps', '%s.png' % gid)
            if not os.path.isfile(path):
                self.fail.append('gump %s: no %s' % (gid, path))
                continue
            im = Image.open(path).convert('RGBA')
            if im.size != CANVAS:
                self.fail.append('gump %s is %dx%d, not %dx%d' % (gid, im.size[0], im.size[1], CANVAS[0], CANVAS[1]))
                continue
            alphas = sorted(set(im.split()[3].getdata()))
            if any(a not in (0, 255) for a in alphas):
                self.fail.append('gump %s has part-transparent pixels (alpha values %s); the art is drawn with hard edges' % (gid, [a for a in alphas if a not in (0, 255)][:5]))
            comps = components(im.split()[3])
            specks = [c for c in comps if len(c) < STRAY]
            for c in specks:
                self.fail.append('gump %s: stray %d-pixel speck at %s' % (gid, len(c), sorted(c)[0]))
            comps = [c for c in comps if len(c) >= STRAY]
            if not comps:
                self.fail.append('gump %s is empty' % gid)
                continue
            for bid in gspec['bodies']:
                if bid not in bodies:
                    self.fail.append('gump %s serves body %s, which has no fit entry or file' % (gid, bid))
                    continue
                self.check_on_body(gid, comps, bid, fit['bodies'][bid])

        for setname in fit.get('addon_sets', []):
            folder = os.path.join(self.addons, setname)
            if not os.path.isdir(folder):
                self.info.append('add-on set %s: not on this machine, skipped' % setname)
                continue
            for bid, spec in fit['bodies'].items():
                if bid not in bodies:
                    continue
                path = os.path.join(folder, '%s.png' % bid)
                if not os.path.isfile(path):
                    self.fail.append('add-on set %s has no body %s' % (setname, bid))
                    continue
                aim, aalpha = silhouette(path)
                if aim.size != CANVAS:
                    self.fail.append('add-on set %s body %s is %dx%d' % (setname, bid, aim.size[0], aim.size[1]))
                    continue
                sfw = bodies[bid][1]
                bad = []
                if aalpha.getbbox()[1] != spec['crown_y']:
                    bad.append('its first opaque row is %d, not %d' % (aalpha.getbbox()[1], spec['crown_y']))
                for name, box in (('face', spec['face']), ('lower-back', spec['lower_back'])):
                    n = extents_differ(sfw, aalpha, box)
                    if n:
                        bad.append('%d rows of the %s box have an edge more than a pixel from the SFW body' % (n, name))
                if bad:
                    self.fail.append('add-on set %s body %s is not the SFW silhouette: %s; measure it and give it its own fit' % (setname, bid, '; '.join(bad)))
                else:
                    self.info.append('add-on set %s body %s: crown row and the face and lower-back edges as the SFW body' % (setname, bid))
        return not self.fail

    def check_on_body(self, gid, comps, bid, spec):
        crown = spec['crown_y']
        face = spec['face']
        back = spec['lower_back']
        head_centre = (spec['head_x'][0] + spec['head_x'][1]) / 2.0
        ears = [c for c in comps if bbox(c)[3] <= crown + EARS_BELOW]
        rest = [c for c in comps if c not in ears]
        where = 'gump %s on body %s (%s)' % (gid, bid, spec['who'])
        if not ears:
            self.fail.append('%s: no ears: nothing ends within %d rows of the crown (row %d); the lowest component ends at row %d' % (where, EARS_BELOW, crown, min(bbox(c)[3] for c in comps)))
        else:
            eb = bbox([p for c in ears for p in c])
            if boxes_cross(eb, face):
                self.fail.append('%s: the ears (box %s) cross the face box %s' % (where, eb, face))
            if not (crown + EARS_SIT[0] <= eb[3] <= crown + EARS_SIT[1]):
                self.fail.append('%s: the ears end at row %d; they must sit on the crown, rows %d to %d' % (where, eb[3], crown + EARS_SIT[0], crown + EARS_SIT[1]))
            centre = (eb[0] + eb[2]) / 2.0
            if abs(centre - head_centre) > EARS_CENTRE:
                self.fail.append('%s: the ears are centred on column %.1f, the head on %.1f' % (where, centre, head_centre))
            self.info.append('%s: ears box %s, crown %d, head centre %.1f' % (where, eb, crown, head_centre))
        if not rest:
            self.fail.append('%s: no tail: every component ends near the crown' % where)
            return
        tail = rest[0]
        tb = bbox(tail)
        if tb[1] < back[1]:
            self.fail.append('%s: the tail reaches up to row %d, above the lower-back box %s' % (where, tb[1], back))
        # The root: the tail's column nearest the head's centre (the tail leaves the body there).
        root_x = min((p[0] for p in tail), key=lambda x: abs(x - head_centre))
        root_rows = sorted(p[1] for p in tail if p[0] == root_x)
        if not (back[0] <= root_x <= back[2]):
            self.fail.append('%s: the tail root is at column %d, outside the lower-back box %s' % (where, root_x, back))
        if root_rows[0] < back[1] or root_rows[-1] > back[3]:
            self.fail.append('%s: the tail root (column %d, rows %d to %d) is outside the lower-back box %s' % (where, root_x, root_rows[0], root_rows[-1], back))
        self.info.append('%s: tail box %s, root column %d rows %d-%d, lower-back box %s' % (where, tb, root_x, root_rows[0], root_rows[-1], back))

    def render(self, out_dir, bodies_dir, tag):
        """Composites the way the client draws them: body, then hair or a hooded robe (the kit's gumps), then ours."""
        os.makedirs(out_dir, exist_ok=True)
        written = []
        for gid, gspec in self.fit['gumps'].items():
            gpath = os.path.join(self.art, 'gumps', '%s.png' % gid)
            if not os.path.isfile(gpath):
                continue
            gump = Image.open(gpath).convert('RGBA')
            for bid in gspec['bodies']:
                bpath = os.path.join(bodies_dir, '%s.png' % bid)
                if not os.path.isfile(bpath):
                    continue
                body = Image.open(bpath).convert('RGBA')
                for kind, over in (('hair', gspec.get('hair')), ('hood', gspec.get('hood'))):
                    im = Image.new('RGBA', CANVAS, (0, 0, 0, 255))
                    im.alpha_composite(body)
                    opath = os.path.join(self.kit, '%d.png' % over) if over else None
                    if opath and os.path.isfile(opath):
                        im.alpha_composite(Image.open(opath).convert('RGBA'))
                    im.alpha_composite(gump)
                    im = im.resize((CANVAS[0] * 3, CANVAS[1] * 3), Image.NEAREST)
                    name = 'fit-%s-on-%s-%s-%s.png' % (gid, bid, kind, tag)
                    im.save(os.path.join(out_dir, name))
                    written.append(name)
        return written


def main(argv=None):
    ap = argparse.ArgumentParser(description='check that our paperdoll gumps fit the shipped bodies (cc-P138)')
    ap.add_argument('--art', default=ART)
    ap.add_argument('--bodies', default=BODIES, help='the SFW body gumps')
    ap.add_argument('--kit', default=KIT, help='the paperdoll art folder, for the hair and robe in renders')
    ap.add_argument('--addons', default=ADDONS, help='the Topless and Nude add-on root (checked when present)')
    ap.add_argument('--fit', default=None, help='paperdoll-fit.json (default: in --art)')
    ap.add_argument('--render', metavar='DIR', help='write the SFW composites here')
    ap.add_argument('--render-addons', metavar='DIR', help='write the add-on set composites here (never a repo folder)')
    ap.add_argument('-q', '--quiet', action='store_true')
    a = ap.parse_args(argv)
    f = Fit(a.art, a.bodies, a.kit, a.addons, a.fit or os.path.join(a.art, 'paperdoll-fit.json'))
    ok = f.check()
    if not a.quiet:
        for line in f.info:
            print('  ' + line)
    if a.render:
        for n in f.render(a.render, a.bodies, 'sfw'):
            print('  wrote ' + os.path.join(a.render, n))
    if a.render_addons:
        for setname in f.fit.get('addon_sets', []):
            folder = os.path.join(a.addons, setname)
            if os.path.isdir(folder):
                for n in f.render(a.render_addons, folder, setname):
                    print('  wrote ' + os.path.join(a.render_addons, n))
    for line in f.fail:
        print('FAIL: ' + line)
    print('paperdoll fit: %d gumps, %d failures' % (len(f.fit['gumps']), len(f.fail)))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
