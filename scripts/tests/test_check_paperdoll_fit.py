"""cc-P138: check-paperdoll-fit.py's tests. Run from the repo root:

    python -m unittest discover -s scripts/tests -p "test_check_paperdoll_fit.py"

The fixtures start from the vendored files (the committed gumps, the SFW bodies, paperdoll-fit.json and
registry.csv), copied into a temporary folder and bent one way at a time, so each test is one failure the check
must name. The Topless and Nude sets are never read: the add-on tests use a copy of the SFW body with a changed
outline, in the temporary folder."""
import contextlib
import importlib.util
import io
import json
import os
import shutil
import tempfile
import unittest

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
SCRIPT = os.path.join(os.path.dirname(HERE), 'check-paperdoll-fit.py')
_spec = importlib.util.spec_from_file_location('check_paperdoll_fit', SCRIPT)
cpf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cpf)

ART = os.path.join(REPO, 'player-package', 'vendor', 'shattered-legacy-art')
BODIES = os.path.join(REPO, 'player-package', 'vendor', 'paperdoll-sos', 'bodies-sfw')


def shifted(gump_path, dx_ears, dy_ears, dx_tail, dy_tail, speck=None):
    """The gump with its ears and tail components moved, and an optional lone pixel added."""
    im = Image.open(gump_path).convert('RGBA')
    comps = cpf.components(im.split()[3])
    tail, ears = comps[0], comps[1:]
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    for pts, (dx, dy) in ((tail, (dx_tail, dy_tail)), ([p for c in ears for p in c], (dx_ears, dy_ears))):
        for x, y in pts:
            out.putpixel((x + dx, y + dy), im.getpixel((x, y)))
    if speck:
        out.putpixel(speck, (255, 0, 0, 255))
    return out


class Fixture:
    def __init__(self):
        self.root = tempfile.mkdtemp(prefix='sl-paperdoll-fit-')
        self.art = os.path.join(self.root, 'art')
        shutil.copytree(ART, self.art, ignore=shutil.ignore_patterns('anim-src', '*.bin', 'art'))
        self.bodies = os.path.join(self.root, 'bodies')
        shutil.copytree(BODIES, self.bodies)
        self.addons = os.path.join(self.root, 'addons')
        os.makedirs(self.addons)

    def run(self, extra=()):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = cpf.main(['--art', self.art, '--bodies', self.bodies, '--addons', self.addons, '--kit', os.path.join(self.root, 'no-kit')] + list(extra))
        return code, out.getvalue()

    def close(self):
        shutil.rmtree(self.root, ignore_errors=True)


class CheckPaperdollFit(unittest.TestCase):
    def setUp(self):
        self.fx = Fixture()
        self.addCleanup(self.fx.close)

    def test_the_committed_art_fits_every_body_it_serves(self):
        code, text = self.fx.run()
        self.assertEqual(0, code, text)
        self.assertIn('0 failures', text)
        for gump, bodies in (('64783', ('13', '15')), ('54783', ('12', '14'))):
            for b in bodies:
                self.assertIn('gump %s on body %s' % (gump, b), text)

    def test_the_old_placement_fails_on_the_face_and_the_thigh(self):
        # cc-P62's placement was 25 rows lower for the ears and 14 for the tail (Chase's screenshot, 2026-10-16).
        g = os.path.join(self.fx.art, 'gumps', '64783.png')
        shifted(g, -2, 25, 0, 14).save(g)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('no ears', text)
        self.assertIn('outside the lower-back box', text)
        self.assertNotIn('gump 54783 on body 12 (male human (0x190, 0x192)): the', text)   # the untouched gump still fits

    def test_ears_a_little_too_low_cross_the_face(self):
        # cc-P166: 5 rows (was 8): the ears already sit 2 rows lower, and 8 more takes them past EARS_BELOW
        g = os.path.join(self.fx.art, 'gumps', '54783.png')
        shifted(g, 0, 5, 0, 0).save(g)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('cross the face box', text)

    def test_ears_floating_above_the_head_fail(self):
        g = os.path.join(self.fx.art, 'gumps', '54783.png')
        shifted(g, 0, -8, 0, 0).save(g)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('must sit on the crown', text)

    def test_ears_with_air_under_them_on_a_bald_head_fail(self):
        # cc-P166: Chase, 2026-10-18, a bald Eve: the ears sat a little high. cc-P138's placement (3 rows up from the
        # committed one) left 3 rows of air between the ears' band and the bald scalp; the kit's hair filled them.
        g = os.path.join(self.fx.art, 'gumps', '64783.png')
        shifted(g, 0, -3, 0, 0).save(g)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('rows of air between the ears and the bald scalp', text)
        self.assertIn('gump 64783 on body 13', text)

    def test_ears_off_centre_fail(self):
        g = os.path.join(self.fx.art, 'gumps', '64783.png')
        shifted(g, 7, 0, 0, 0).save(g)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('centred on column', text)

    def test_a_tail_above_the_waist_fails(self):
        g = os.path.join(self.fx.art, 'gumps', '64783.png')
        shifted(g, 0, 0, 0, -9).save(g)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('above the lower-back box', text)

    def test_a_stray_speck_fails(self):
        g = os.path.join(self.fx.art, 'gumps', '64783.png')
        shifted(g, 0, 0, 0, 0, speck=(200, 200)).save(g)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('stray 1-pixel speck at (200, 200)', text)

    def test_a_registered_gump_with_no_fit_entry_fails(self):
        with open(os.path.join(self.fx.art, 'registry.csv'), 'a', newline='\n') as f:
            f.write('55555,0xD903,a test gump,test,test,gump\n')
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('gump 55555 is in registry.csv but paperdoll-fit.json says nothing', text)

    def test_a_registered_button_gump_needs_no_fit_entry(self):
        # cc-P147: a gump below 50000 is not a worn item's paperdoll gump (those are 50000 + and 60000 + an Animation
        # number); the Achievements button's states are drawn by the paperdoll, never over a body.
        with open(os.path.join(self.fx.art, 'registry.csv'), 'a', newline='\n') as f:
            f.write('22456,0x57B8,a button gump,test,test,gump\n')
        code, text = self.fx.run()
        self.assertEqual(0, code, text)
        self.assertNotIn('22456', text)

    def test_a_changed_body_file_is_noticed(self):
        p = os.path.join(self.fx.bodies, '13.png')
        im = Image.open(p).convert('RGBA')
        moved = Image.new('RGBA', im.size, (0, 0, 0, 0))
        moved.paste(im, (0, 3))
        moved.save(p)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('body 13: first opaque row is 36, paperdoll-fit.json says crown_y 33', text)

    def test_an_addon_set_with_the_sfw_outline_passes_and_one_with_another_fails(self):
        for setname in ('topless', 'nude'):
            d = os.path.join(self.fx.addons, setname)
            shutil.copytree(self.fx.bodies, d)
        code, text = self.fx.run()
        self.assertEqual(0, code, text)
        self.assertIn('add-on set nude body 13: crown row and the face and lower-back edges as the SFW body', text)
        # Widen the hips of the nude 13 by two pixels inside the lower-back box.
        p = os.path.join(self.fx.addons, 'nude', '13.png')
        im = Image.open(p).convert('RGBA')
        fit = json.load(open(os.path.join(self.fx.art, 'paperdoll-fit.json')))['bodies']['13']
        x0, y0, x1, y1 = fit['lower_back']
        for y in range(y0, y1 + 1):
            xs = [x for x in range(x0, x1 + 1) if im.getpixel((x, y))[3]]
            if xs:
                for x in (xs[0] - 1, xs[0] - 2):
                    im.putpixel((x, y), (200, 200, 200, 255))
        im.save(p)
        code, text = self.fx.run()
        self.assertEqual(1, code, text)
        self.assertIn('add-on set nude body 13 is not the SFW silhouette', text)
        self.assertIn('lower-back box', text)

    def test_render_writes_a_composite_per_gump_body_and_cover(self):
        out = os.path.join(self.fx.root, 'render')
        code, text = self.fx.run(['--render', out])
        self.assertEqual(0, code, text)
        names = sorted(os.listdir(out))
        self.assertEqual(8, len(names), names)
        self.assertIn('fit-64783-on-13-hood-sfw.png', names)
        im = Image.open(os.path.join(out, names[0]))
        self.assertEqual((780, 711), im.size)


if __name__ == '__main__':
    unittest.main()
