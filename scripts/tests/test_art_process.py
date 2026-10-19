"""cc-P152: art_process.py's tests, one per processing step. Run from the repo root:

    python -P -m unittest discover -s scripts/tests -p "test_art_process.py"

ART_PROCESS (a path) swaps in another copy of the script, which is how the tests were seen failing first. Every sample
image is drawn here from numbers chosen for the test: no art file is read."""
import importlib.util
import json
import math
import os
import shutil
import tempfile
import unittest

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.environ.get('ART_PROCESS') or os.path.join(os.path.dirname(HERE), 'art', 'art_process.py')
_spec = importlib.util.spec_from_file_location('art_process', SCRIPT)
ap = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ap)


def disc(side=400, r=120, offset=(0, 0), colour=(200, 160, 90)):
    """A solid disc on transparency, off centre by offset, with lots of empty space around it."""
    a = np.zeros((side, side, 4), np.uint8)
    yy, xx = np.mgrid[0:side, 0:side]
    m = (xx - side / 2 - offset[0]) ** 2 + (yy - side / 2 - offset[1]) ** 2 <= r * r
    a[m] = colour + (255,)
    return Image.fromarray(a, 'RGBA')


def noise_tile(side=128, seamless=True, seed=1):
    """A texture made of whole sine periods (it wraps exactly) or of random noise with a ramp (it does not)."""
    if seamless:
        yy, xx = np.mgrid[0:side, 0:side]
        v = 128 + 50 * np.sin(2 * math.pi * xx * 3 / side) + 40 * np.cos(2 * math.pi * yy * 2 / side)
    else:
        rng = np.random.default_rng(seed)
        v = rng.normal(128, 4, (side, side)) + np.linspace(0, 120, side)[None, :]
    v = np.clip(v, 0, 255).astype(np.uint8)
    return Image.fromarray(np.stack([v, v, v], axis=2), 'RGB')


class Steps(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix='art-test-')

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def save(self, im, name):
        p = os.path.join(self.tmp, name)
        im.save(p)
        return p

    # resize
    def test_resize_keeps_the_size_asked_and_no_dark_fringe(self):
        # A pale disc on transparency, shrunk: the edge pixels that are part-transparent keep the disc's colour
        # (premultiplied resize), where a naive resize blends in the transparent black and darkens them.
        small = ap.resize_rgba(disc(colour=(240, 220, 180)), (32, 32))
        self.assertEqual(small.size, (32, 32))
        a = np.asarray(small)
        edge = a[(a[:, :, 3] > 20) & (a[:, :, 3] < 200)]
        self.assertTrue(len(edge) > 0)
        self.assertGreater(edge[:, 0].min(), 200, 'part-transparent edge pixels darkened: %s' % edge[:, 0].min())

    # trim
    def test_trim_centres_the_object_and_drops_the_empty_space(self):
        t = ap.trim_to_object(disc(offset=(90, -60)))
        self.assertEqual(t.width, t.height)
        self.assertLessEqual(abs(t.width - 241), 2)
        box = ap.object_bbox(t)
        self.assertLessEqual(abs(box[0] - (t.width - box[2])), 1)

    def test_fit_with_margin_leaves_the_margin_clear(self):
        out = ap.fit_with_margin(ap.trim_to_object(disc()), (32, 32), 2)
        self.assertTrue(ap.check_clear_edges(out, 2)['ok'])
        self.assertGreater(np.asarray(out)[16, 16, 3], 250)

    # alpha
    def test_alpha_check_fails_an_object_touching_the_edge_and_passes_a_clear_one(self):
        touching = disc(side=64, r=40)
        self.assertFalse(ap.check_clear_edges(touching, 2)['ok'])
        self.assertTrue(ap.check_clear_edges(disc(side=64, r=20), 2)['ok'])

    def test_opaque_check_fails_any_transparency(self):
        self.assertTrue(ap.check_opaque(noise_tile())['ok'])
        holed = noise_tile().convert('RGBA')
        holed.putpixel((5, 5), (0, 0, 0, 0))
        self.assertFalse(ap.check_opaque(holed)['ok'])

    # seamless
    def test_seamless_check_tells_a_wrapping_tile_from_a_ramp(self):
        self.assertTrue(all(c['ok'] for c in ap.check_seamless(noise_tile(seamless=True), ('x', 'y'))))
        self.assertFalse(ap.check_seamless(noise_tile(seamless=False), ('x',))[0]['ok'])

    # tone
    def test_tone_remap_puts_every_pixel_inside_the_range(self):
        out = ap.remap_tone(noise_tile(), (26, 28, 32), (60, 60, 66))
        self.assertTrue(ap.check_tone(out, (26, 28, 32), (60, 60, 66))['ok'])
        self.assertFalse(ap.check_tone(noise_tile(), (26, 28, 32), (60, 60, 66))['ok'])

    # budget
    def test_budget_reduces_colours_when_over_and_says_so(self):
        rng = np.random.default_rng(3)
        busy = Image.fromarray(rng.integers(0, 256, (128, 128, 4), dtype=np.uint8), 'RGBA')
        data, how = ap.fit_budget(busy, 30 * 1024)
        self.assertLessEqual(len(data), 30 * 1024)
        self.assertIn('colours', how)
        data2, how2 = ap.fit_budget(disc(side=32, r=10), 30 * 1024)
        self.assertEqual(how2, 'fits as it is')
        _, how3 = ap.fit_budget(busy, 200)
        self.assertIn('OVER BUDGET', how3)

    # strips
    def slot(self, **kw):
        s = {'file': 'x.png', 'sizes': [[32, 32], [64, 64]], 'kind': 'animated', 'frames': 12, 'fps': 8,
             'animation': 'pulse-turn', 'transparent': True, 'budgetKB': 120, 'margin': 2, 'glow': [120, 255, 150]}
        s.update(kw)
        return s

    def test_a_crystal_strip_has_its_frames_side_by_side_and_they_differ(self):
        img = ap.render_slot(disc(colour=(80, 200, 120)), self.slot(), (32, 32))
        self.assertEqual(img.size, (12 * 32, 32))
        frames = [np.asarray(img.crop((i * 32, 0, i * 32 + 32, 32))) for i in range(12)]
        self.assertTrue(any(np.abs(frames[0].astype(int) - f.astype(int)).sum() > 0 for f in frames[1:]))
        self.assertTrue(all(c['ok'] for c in ap.run_checks(img, self.slot())), ap.run_checks(img, self.slot()))

    def test_the_golem_strip_breathes_and_its_sparks_move(self):
        s = self.slot(frames=8, fps=6, animation='halo-sparks', glow=[255, 196, 96], sizes=[[64, 64]], budgetKB=300)
        img = ap.render_slot(disc(colour=(150, 110, 60)), s, (64, 64))
        self.assertEqual(img.size, (8 * 64, 64))
        halo = [np.asarray(img.crop((i * 64, 0, i * 64 + 64, 64)))[:, :, 3].astype(int).sum() for i in range(8)]
        self.assertGreater(max(halo) - min(halo), 0)
        self.assertTrue(ap.check_clear_edges(img.crop((0, 0, 64, 64)), 2)['ok'])

    def test_the_spinner_lights_one_mark_per_frame_and_steps_round(self):
        s = self.slot(frames=8, fps=10, animation='spinner', glow=[220, 178, 98], sizes=[[48, 48]], budgetKB=100)
        img = ap.render_slot(disc(colour=(200, 160, 90)), s, (48, 48))
        self.assertEqual(img.size, (8 * 48, 48))
        # The brightest spot moves round the ring: its angle from the centre changes from frame to frame.
        angles = []
        for i in range(8):
            f = np.asarray(img.crop((i * 48, 0, i * 48 + 48, 48))).astype(float)
            lum = f[:, :, :3].sum(axis=2) * (f[:, :, 3] / 255)
            y, x = np.unravel_index(np.argmax(lum), lum.shape)
            angles.append(round(math.degrees(math.atan2(y - 24, x - 24))))
        self.assertEqual(len(set(angles)), 8, angles)

    def test_strip_shape_check_fails_a_wrong_width(self):
        self.assertFalse(ap.check_strip(Image.new('RGBA', (100, 32)), 12)['ok'])
        self.assertTrue(ap.check_strip(Image.new('RGBA', (384, 32)), 12)['ok'])

    # place, end to end
    def test_place_writes_each_size_and_reports_every_check(self):
        m = self.save(disc(colour=(80, 200, 120)), 'master.png')
        r = ap.place(m, self.slot(), os.path.join(self.tmp, 'out'))
        self.assertTrue(r['ok'], r['why'])
        self.assertTrue(os.path.isfile(os.path.join(self.tmp, 'out', 'x.png')))
        self.assertTrue(os.path.isfile(os.path.join(self.tmp, 'out', 'x@64x64.png')))
        with Image.open(os.path.join(self.tmp, 'out', 'x.png')) as im:
            self.assertEqual(im.size, (384, 32))
        names = [c['name'] for c in r['checks']]
        self.assertIn('strip shape', names)
        self.assertIn('budget', names)

    def test_place_a_tile_remaps_its_tone_and_checks_the_seam(self):
        m = self.save(noise_tile(256), 'tile.png')
        s = {'file': 'stone.png', 'sizes': [[128, 128]], 'kind': 'tile', 'frames': 1, 'transparent': False,
             'budgetKB': 200, 'margin': 0, 'toneLow': [26, 28, 32], 'toneHigh': [60, 60, 66]}
        r = ap.place(m, s, os.path.join(self.tmp, 'out'))
        self.assertTrue(r['ok'], r['why'])
        self.assertIn('tone range', [c['name'] for c in r['checks']])
        self.assertIn('seamless left-right', [c['name'] for c in r['checks']])

    def test_check_file_refuses_a_file_over_budget_or_the_wrong_size(self):
        p = self.save(disc(side=40, r=10), 'f.png')
        r = ap.check_file(p, {'file': 'f.png', 'sizes': [[32, 32]], 'kind': 'static', 'transparent': True, 'budgetKB': 1, 'margin': 2})
        self.assertFalse(r['ok'])
        self.assertIn('size', r['why'])

    def test_preview_writes_one_sheet(self):
        m = self.save(disc(), 'master.png')
        out = os.path.join(self.tmp, 'p.png')
        r = ap.preview(m, [self.slot()], out)
        self.assertTrue(r['ok'])
        self.assertTrue(os.path.getsize(out) > 0)

    def test_the_command_line_answers_with_one_json_object(self):
        import contextlib
        import io
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            code = ap.main(['check', '--file', os.path.join(self.tmp, 'missing.png'), '--slot', os.path.join(self.tmp, 'missing.json')])
        self.assertEqual(code, 1)
        self.assertFalse(json.loads(buf.getvalue())['ok'])


if __name__ == '__main__':
    unittest.main()
