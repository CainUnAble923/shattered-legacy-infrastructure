"""cc-P134: validate-art.py's tests. Run from the repo root:

    python -m unittest discover -s scripts/tests -p "test_validate_art.py"

Every fixture is made here, in a temporary folder, from numbers chosen for the test: no EA file is read or
committed. Each test is one fact about how TazUO finds a body's frames, or one check the script makes."""
import contextlib
import importlib.util
import io
import json
import os
import shutil
import struct
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(os.path.dirname(HERE), 'validate-art.py')
_spec = importlib.util.spec_from_file_location('validate_art', SCRIPT)
va = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(va)

COLOUR = 0x7C00                     # pure red in 1555
RED = (255, 0, 0, 255)


def put(path, data, mode='wb'):
    with open(path, mode, **({} if 'b' in mode else {'newline': '\n'})) as f:
        f.write(data)


def record(px=1, py=2, w=4, h=5, cx=2, cy=0):
    """One anim*.mul record: a palette whose entry 1 is red, one frame of w x h with one red pixel at (px, py)."""
    pal = [0] * 256
    pal[1] = COLOUR
    head = struct.pack('<256H', *pal) + struct.pack('<ii', 1, 8)
    x, y = px - cx, py - cy - h
    run = ((x & 0x3FF) << 22) | ((y & 0x3FF) << 12) | 1
    frame = struct.pack('<hhHHI', cx, cy, w, h, run) + bytes([1]) + struct.pack('<I', 0x7FFF7FFF)
    return head + frame


def write_anim(folder, name, n_records, filled):
    """name.idx/.mul with n_records records; filled: record numbers given one record() each."""
    idx, mul = bytearray(), bytearray()
    for i in range(n_records):
        if i in filled:
            r = record()
            idx += struct.pack('<iii', len(mul), len(r), 0)
            mul += r
        else:
            idx += struct.pack('<iii', -1, -1, -1)
    put(os.path.join(folder, name + '.idx'), idx)
    put(os.path.join(folder, name + '.mul'), mul)


def write_vd(path, layout, filled, magic=6):
    n = {0: 110, 1: 65, 2: 175}[layout]
    head = struct.pack('<hh', magic, layout)
    data_start = 4 + 12 * n
    table, data = bytearray(), bytearray()
    for i in range(n):
        if i in filled:
            r = record()
            table += struct.pack('<iii', data_start + len(data), len(r), 0)
            data += r
        else:
            table += struct.pack('<iii', -1, -1, -1)
    put(path, head + table + data)


def write_hues(path, changes):
    """3 groups of 8 hues; changes: {hue id from 1: (colour, name)} replacing colour 0 and the name."""
    out = bytearray()
    hid = 1
    for _g in range(3):
        out += struct.pack('<I', 0)
        for _k in range(8):
            cols = [0x1234] * 32
            name = b'hue%d' % hid
            if hid in changes:
                c, nm = changes[hid]
                if c is not None:
                    cols[0] = c
                if nm is not None:
                    name = nm
            out += struct.pack('<32H', *cols) + struct.pack('<HH', 0, 0) + name.ljust(20, b'\0')
            hid += 1
    put(path, out)


class Fixture(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix='validate-art-')
        self.data = os.path.join(self.tmp, 'client')
        self.pinned = os.path.join(self.tmp, 'pinned')
        self.pkg = os.path.join(self.tmp, 'pkg')
        for d in (self.data, os.path.join(self.pinned, 'Distribution', 'Data'), self.pkg):
            os.makedirs(d)
        # anim.mul: body 5 (high, 5 x 110) has action 0; body 210 (low) has action 2; body 400 (people) none.
        write_anim(self.data, 'anim', 35000 + 2 * 175, {550, 22000 + 10 * 65 + 10})
        # anim2.mul: the kirin case, slot 68 read with the low layout at record 13420 (= 22000 + (68 - 200) x 65).
        write_anim(self.data, 'anim2', 22000, {13420, 13425})
        # anim3.mul exists, so a Bodyconv anim3 column applies.
        write_anim(self.data, 'anim3', 1000, {3 * 110})
        self.write('mobtypes.txt', '# ID TYPE FLAGS\n132\tANIMAL\t0\n5\tMONSTER\t0\n700\tMONSTER\t10000\n')
        self.write('Bodyconv.def', '# comment\n132\t68\t-1\t-1\t-1\n150\t1\t3\t-1\t-1\n')
        self.write('Body.def', '# Format\n9 {5} 0\n')
        self.write('Corpse.def', '')
        self.write_pinned('2\tMonster\n5\tMonster\n')
        # an empty UOP archive for check-registry's art test (header only, no blocks)
        put(os.path.join(self.data, 'artLegacyMUL.uop'), struct.pack('<IIIqII', 0x50594D, 5, 0, 0, 1000, 0))
        self.records = os.path.join(self.pkg, 'records.json')
        put(self.records, json.dumps({'equipAnims': []}), 'w')

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def write(self, name, text):
        put(os.path.join(self.data, name), text, 'w')

    def write_pinned(self, text):
        put(os.path.join(self.pinned, 'Distribution', 'Data', 'bodyTable.cfg'), text, 'w')

    def client(self):
        return va.Client(self.data, pinned=self.pinned, records=self.records)

    def run_main(self, *argv):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            rc = va.main(['--data', self.data, '--pinned', self.pinned, '--records', self.records] + list(argv))
        return rc, buf.getvalue()


class ClientFacts(Fixture):
    def test_bodyconv_last_applicable_column_wins(self):
        # 150 has anim2 slot 1 and anim3 slot 3: TazUO applies both in turn, so anim3 wins (vseUOAware took anim2).
        r = self.client().resolve(150)
        self.assertEqual((r['file'], r['slot']), (2, 3))

    def test_bodyconv_column_for_a_missing_file_is_skipped(self):
        os.remove(os.path.join(self.data, 'anim3.idx'))
        r = self.client().resolve(150)
        self.assertEqual((r['file'], r['slot']), (1, 1))

    def test_mobtypes_type_decides_the_layout_the_kirin_case(self):
        c = self.client()
        r = c.resolve(132)
        self.assertEqual((r['file'], r['slot'], r['group'], r['start']), (1, 68, 'low', 13420))
        self.assertEqual(c.classic_actions(r), [0, 1])

    def test_no_mobtypes_entry_uses_the_slot_range(self):
        c = self.client()
        self.assertEqual(c.resolve(210)['group'], 'low')
        self.assertEqual(c.resolve(5)['group'], 'high')
        self.assertEqual(c.resolve(450)['group'], 'people')
        self.assertEqual(c.actions(210)[1], [2])

    def test_flag_10000_sends_the_body_to_uop(self):
        self.assertEqual(self.client().resolve(700)['format'], 'uop')

    def test_animal_flags(self):
        animal = va.TYPES.index('animal')
        self.assertEqual(va.layout(animal, 0), ('low', 13))
        self.assertEqual(va.layout(animal, 0x20), ('high', 22))
        self.assertEqual(va.layout(animal, 0x40), ('low', 13))      # 0x40 alone does nothing for an animal
        self.assertEqual(va.layout(animal, 0x60), ('low', 13))
        self.assertEqual(va.layout(va.TYPES.index('monster'), 0x40), ('low', 13))
        self.assertEqual(va.layout(va.TYPES.index('sea_monster'), 0), ('high', 13))

    def test_our_equip_anim_override_reaches_anim7(self):
        write_anim(self.pkg, 'x', 0, set())
        os.replace(os.path.join(self.pkg, 'x.idx'), os.path.join(self.pkg, 'sl-anim7-idx.bin'))
        os.replace(os.path.join(self.pkg, 'x.mul'), os.path.join(self.pkg, 'sl-anim7.bin'))
        put(self.records, json.dumps({'equipAnims': [{'anim': 4783, 'slot': 400}]}), 'w')
        r = self.client().resolve(4783)
        self.assertEqual((r['file'], r['slot'], r['group']), (6, 400, 'people'))

    def test_our_body_anim_override_reaches_anim7_and_types_the_body(self):
        # cc-P139: a creature body from records.json bodyAnims: a Bodyconv line into anim7 (slot 1, the high layout
        # at records 110-219) and a mobtypes entry (MONSTER 0), both as the launcher writes them.
        write_anim(self.pkg, 'x', 300, {110, 160})
        os.replace(os.path.join(self.pkg, 'x.idx'), os.path.join(self.pkg, 'sl-anim7-idx.bin'))
        os.replace(os.path.join(self.pkg, 'x.mul'), os.path.join(self.pkg, 'sl-anim7.bin'))
        put(self.records, json.dumps({'bodyAnims': [{'body': 2048, 'slot': 1, 'type': 'MONSTER', 'flags': 0,
                                                     'vd': 'anim-src/bodies/2048.vd'}]}), 'w')
        c = self.client()
        r = c.resolve(2048)
        self.assertEqual((r['file'], r['slot'], r['group'], r['start']), (6, 1, 'high', 110))
        self.assertEqual(c.mobtypes[2048], (0, 0, 'records.json bodyAnims'))
        self.assertEqual(c.classic_actions(r), [0, 10])
        # The player's own mobtypes.txt line wins over ours (the launcher skips a body the file names).
        self.write('mobtypes.txt', '# ID TYPE FLAGS\n132\tANIMAL\t0\n5\tMONSTER\t0\n700\tMONSTER\t10000\n2048\tANIMAL\t0\n')
        self.assertEqual(self.client().mobtypes[2048], (2, 0, 'mobtypes.txt:5'))


class CheckVd(Fixture):
    def vd(self, layout, filled):
        p = os.path.join(self.tmp, 'test.vd')
        write_vd(p, layout, filled)
        return va.Vd(p)

    def test_free_matching_slot_passes(self):
        out, problems = va.check_vd(self.client(), self.vd(0, {0, 5}), 3000, conv=(1, 7))
        self.assertEqual(problems, [], out)

    def test_slot_with_frames_is_not_free(self):
        out, problems = va.check_vd(self.client(), self.vd(0, {0}), 3000, conv=(1, 122))
        self.assertTrue(any(p.startswith('frames:') for p in problems), problems)

    def test_layout_mismatch_is_a_problem(self):
        out, problems = va.check_vd(self.client(), self.vd(1, {0}), 3000, conv=(1, 7))
        self.assertTrue(any('is a low block but the client reads a high block' in p for p in problems), problems)

    def test_body_with_a_bodyconv_line_is_not_free_for_a_new_line(self):
        out, problems = va.check_vd(self.client(), self.vd(0, {0}), 150, conv=(1, 7))
        self.assertTrue(any(p.startswith('bodyconv:') for p in problems), problems)

    def test_body_def_redirect_counts_only_in_anim_mul(self):
        c = self.client()
        _o, problems = va.check_vd(c, self.vd(0, {0}), 9)
        self.assertTrue(any('Body.def:2 draws body 9 as body 5' in p for p in problems), problems)
        _o, problems = va.check_vd(c, self.vd(0, {0}), 9, conv=(1, 7))
        self.assertFalse(any('Body.def' in p for p in problems), problems)

    def test_server_body_table_is_reported(self):
        out, _p = va.check_vd(self.client(), self.vd(0, {0}), 2, conv=(1, 7))
        self.assertIn('server: bodyTable.cfg says Monster', out)

    def test_hover_flag_hint(self):
        out, _p = va.check_vd(self.client(), self.vd(0, {40}), 3000, conv=(1, 7))
        self.assertTrue(any('IdleAt8Frame' in x for x in out), out)

    def test_bad_vd_is_refused(self):
        p = os.path.join(self.tmp, 'bad.vd')
        write_vd(p, 0, {0}, magic=5)
        with self.assertRaises(ValueError):
            va.Vd(p)

    def test_propose_gives_distinct_free_slots(self):
        write_anim(self.pkg, 'x', 35000 + 175, set())
        os.replace(os.path.join(self.pkg, 'x.idx'), os.path.join(self.pkg, 'sl-anim7-idx.bin'))
        os.replace(os.path.join(self.pkg, 'x.mul'), os.path.join(self.pkg, 'sl-anim7.bin'))
        put(self.records, json.dumps({'equipAnims': [{'anim': 4783, 'slot': 400}]}), 'w')
        a, b = os.path.join(self.tmp, 'a.vd'), os.path.join(self.tmp, 'b.vd')
        write_vd(a, 0, {0})
        write_vd(b, 0, {0})
        rc, out = self.run_main('--registry', os.path.join(self.tmp, 'none.csv'), 'propose', a, b, '--body-from', '2')
        rows = [l.split('\t') for l in out.splitlines()[1:]]
        self.assertEqual(rc, 0, out)
        self.assertEqual([r[3] for r in rows], ['3 (0x3)', '4 (0x4)'])     # 2 is in bodyTable.cfg
        self.assertEqual([r[4] for r in rows], ['1', '2'])


class Registry(Fixture):
    def test_check_registry_flags_collisions_and_missing_mobtypes(self):
        reg = os.path.join(self.tmp, 'registry.csv')
        put(reg, (
            '# test\nid,id_hex,what,source,license,kind\n'
            '100,0x64,a tile,us,ours,art\n100,0x64,the same tile again,us,ours,art\n'
            '3000,0xBB8,a creature,us,ours,body\n'
            '5,0x5,a creature on a stock body,us,ours,body\n'), 'w')
        rc, out = self.run_main('--registry', reg, 'check-registry')
        self.assertEqual(rc, 1)
        self.assertIn('registered twice', out)
        self.assertIn('body 3000 (0xBB8) a creature: missing: no mobtypes.txt entry', out)
        self.assertNotIn('body 5 (0x5) a creature on a stock body: missing: no mobtypes', out)
        self.assertIn('3 of 4 rows have problems', out)

    def test_check_registry_passes_a_body_whose_lines_come_from_records(self):
        # cc-P139: a body row is fine when records.json bodyAnims gives it its anim7 slot and its mobtypes line and
        # the slot holds frames; it is a problem when the player's mobtypes.txt names the body (the launcher skips it).
        write_anim(self.pkg, 'x', 300, {110})
        os.replace(os.path.join(self.pkg, 'x.idx'), os.path.join(self.pkg, 'sl-anim7-idx.bin'))
        os.replace(os.path.join(self.pkg, 'x.mul'), os.path.join(self.pkg, 'sl-anim7.bin'))
        put(self.records, json.dumps({'bodyAnims': [{'body': 2048, 'slot': 1, 'type': 'MONSTER', 'flags': 0,
                                                     'vd': 'anim-src/bodies/2048.vd'}]}), 'w')
        reg = os.path.join(self.tmp, 'registry.csv')
        put(reg, '# test\nid,id_hex,what,source,license,kind\n2048,0x800,our body,us,ours,body\n', 'w')
        rc, out = self.run_main('--registry', reg, 'check-registry')
        self.assertEqual(rc, 0, out)
        self.assertIn('body 2048 (0x800) our body: ok [anim7 slot 1 high; mobtypes MONSTER 0 from records.json]', out)
        self.write('mobtypes.txt', '# ID TYPE FLAGS\n2048\tANIMAL\t0\n')
        rc, out = self.run_main('--registry', reg, 'check-registry')
        self.assertEqual(rc, 1, out)
        self.assertIn('collides: mobtypes.txt:2 names it; the launcher would skip our body', out)


class Hues(Fixture):
    def test_colour_and_name_differences_are_counted_apart(self):
        a, b = os.path.join(self.tmp, 'a.mul'), os.path.join(self.tmp, 'b.mul')
        write_hues(a, {})
        write_hues(b, {3: (0x7FFF, None), 10: (None, b'renamed'), 24: (0x0001, b'x')})
        lines, diff = va.hues_diff(a, b, 10)
        self.assertEqual([h for h, _w in diff], [3, 24, 10])
        self.assertIn('  2 in their colours (what an item or creature shows): 3,24', lines)
        self.assertIn('  1 only in name or table range (unused by drawing; EA leaves junk there): 10', lines)

    def test_alpha_bit_is_not_a_difference(self):
        a, b = os.path.join(self.tmp, 'a.mul'), os.path.join(self.tmp, 'b.mul')
        write_hues(a, {1: (0x1234, None)})
        write_hues(b, {1: (0x9234, None)})
        self.assertEqual(va.hues_diff(a, b, 10)[1], [])


class Render(Fixture):
    def test_frame_decodes_to_the_right_pixel(self):
        frames = va.decode_record(record(px=1, py=2, w=4, h=5, cx=2, cy=0))
        img = frames[0][0]
        self.assertEqual(img.shape, (5, 4, 4))
        self.assertEqual(tuple(img[2, 1]), RED)
        self.assertEqual(int((img[:, :, 3] > 0).sum()), 1)

    def test_render_mirrors_directions_5_to_7(self):
        self.assertEqual(va.record_index(5), (3, True))
        self.assertEqual(va.record_index(7), (1, True))
        self.assertEqual(va.record_index(4), (4, False))


if __name__ == '__main__':
    unittest.main()
