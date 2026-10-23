"""cc-P277: a creature body whose .vd is over the 5 MB repo rule stays outside git (P139-D6). records.json names where
it comes from and its SHA-256 (bodyAnims "outsideGit"); build-equip-anims.py copies it into anim-src\\bodies\\ when
it is missing, and refuses a copy or a source whose bytes are not the recorded ones.

    python -P -m unittest discover -s scripts/tests -p "test_build_equip_anims.py"
"""
import hashlib, importlib.util, os, shutil, tempfile, unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(os.path.dirname(HERE), 'build-equip-anims.py')


def load():
    spec = importlib.util.spec_from_file_location('build_equip_anims', SCRIPT)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


class OutsideGit(unittest.TestCase):
    def setUp(self):
        self.m = load()
        self.tmp = tempfile.mkdtemp(prefix='p277-')
        self.art = os.path.join(self.tmp, 'art')
        os.makedirs(os.path.join(self.art, 'anim-src', 'bodies'))
        self.m.ART = self.art
        self.src = os.path.join(self.tmp, 'third-party', 'L-Ridable Red Dragon.vd')
        os.makedirs(os.path.dirname(self.src))
        self.data = b'\x06\x00\x01\x00' + bytes(range(256)) * 8
        with open(self.src, 'wb') as f:
            f.write(self.data)
        self.sha = hashlib.sha256(self.data).hexdigest()
        self.dest = os.path.join(self.art, 'anim-src', 'bodies', '2086.vd')

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def body(self, sha=None, src=None):
        return {'body': 2086, 'slot': 207, 'type': 'ANIMAL', 'flags': 0, 'vd': 'anim-src/bodies/2086.vd',
                'outsideGit': {'from': src or self.src, 'sha256': sha or self.sha}}

    def test_a_missing_copy_is_fetched_from_its_source_and_checked(self):
        said = self.m.fetch_outside_git([self.body(), {'body': 2048, 'slot': 1, 'vd': 'anim-src/bodies/2048.vd'}])
        with open(self.dest, 'rb') as f:
            self.assertEqual(self.data, f.read())
        self.assertEqual(1, len(said))
        self.assertIn('copied', said[0])
        # A second run with the copy in place: nothing to do, and the copy is checked, not taken on trust.
        self.assertEqual([], self.m.fetch_outside_git([self.body()]))

    def test_a_copy_or_a_source_with_other_bytes_is_refused(self):
        with self.assertRaises(SystemExit) as e:
            self.m.fetch_outside_git([self.body(sha='0' * 64)])
        self.assertIn('not the recorded', str(e.exception))
        self.assertFalse(os.path.exists(self.dest))
        with open(self.dest, 'wb') as f:
            f.write(b'changed')
        with self.assertRaises(SystemExit) as e:
            self.m.fetch_outside_git([self.body()])
        self.assertIn('2086.vd', str(e.exception))

    def test_no_copy_and_no_source_says_where_to_get_it(self):
        with self.assertRaises(SystemExit) as e:
            self.m.fetch_outside_git([self.body(src=os.path.join(self.tmp, 'gone.vd'))])
        self.assertIn('outside git', str(e.exception))
        self.assertIn('gone.vd', str(e.exception))


if __name__ == '__main__':
    unittest.main()
