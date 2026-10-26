"""cc-P668: the DDNS gate (docker/uo/ddns-gate/gate.py) and Haven's home-IP service (docker/homeip/homeip.py).

    python -P -m unittest discover -s scripts/tests -p "test_ddns_home_ip.py"

No network: each lookup is replaced by a table. The end-to-end facts (the real ddns-updater against the gate, with a
dummy token) are scripts/tests/ddns/Test-DdnsHomeIp.ps1.
"""
import importlib.util
import os
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.join(HERE, '..', '..')


def load(name, rel, env):
    os.environ.update(env)
    spec = importlib.util.spec_from_file_location(name, os.path.join(REPO, rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class GateLearnsVpnExits(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.gate = load('gate_under_test', 'docker/uo/ddns-gate/gate.py', {
            'STATE_FILE': os.path.join(self.tmp.name, 'state.json'), 'EGRESS_CACHE': '0',
            'LEARN_AFTER': '900', 'GAP': '900', 'HAVEN_URLS': 'http://haven/ip',
            'EGRESS_URLS': 'http://e1,http://e2', 'BLOCK': '159.26.100.174/32'})
        self.answers = {}
        self.gate.fetch = lambda url: self.answers[url]
        self.gate.log = lambda msg: None

    def tearDown(self):
        self.tmp.cleanup()

    def world(self, haven, egress):
        self.answers = {'http://haven/ip': haven, 'http://e1': egress, 'http://e2': egress}

    def test_vpn_on_serves_havens_address(self):
        self.world('8.8.4.4', '1.0.0.1')
        d = self.gate.decide(now=0)
        self.assertTrue(d['ok'])
        self.assertEqual('8.8.4.4', d['ip'])

    def test_an_egress_that_differs_for_long_is_learned_and_then_refused(self):
        self.world('8.8.4.4', '1.0.0.1')
        for t in (0, 600, 1000):
            self.gate.decide(now=t)
        self.world('1.0.0.1', '1.0.0.1')   # Haven now reports the game PC's VPN exit
        d = self.gate.decide(now=1100)
        self.assertFalse(d['ok'])
        self.assertIn('VPN egress', d['why'])

    def test_a_gap_starts_the_run_again(self):
        self.world('8.8.4.4', '1.0.0.1')
        self.gate.decide(now=0)
        self.gate.decide(now=2000)         # more than GAP since the last sighting: not one unbroken run
        self.world('1.0.0.1', '1.0.0.1')
        self.assertTrue(self.gate.decide(now=2100)['ok'])

    def test_a_real_change_of_home_address_is_not_learned(self):
        self.world('8.8.4.4', '1.0.0.2')   # the house moved to 1.0.0.2; Haven has not caught up yet
        self.gate.decide(now=0)
        self.gate.decide(now=180)
        self.world('1.0.0.2', '1.0.0.2')   # Haven catches up within minutes
        d = self.gate.decide(now=300)
        self.assertTrue(d['ok'])
        self.assertEqual('1.0.0.2', d['ip'])

    def test_a_blocked_address_is_refused(self):
        self.world('159.26.100.174', '159.26.100.174')
        d = self.gate.decide(now=0)
        self.assertFalse(d['ok'])
        self.assertIn('known VPN exit', d['why'])

    def test_a_private_answer_from_haven_is_refused(self):
        self.world('192.168.1.61', '8.8.4.4')
        self.assertFalse(self.gate.decide(now=0)['ok'])

    def test_a_refusal_never_carries_an_address(self):
        # ddns-updater reads an address out of the body of any answer but 403 and 429 (fetch.go:36-40).
        self.assertNotRegex(self.gate.IPV4_TEXT.sub('<address>', 'Haven reports 159.26.100.174 is in BLOCK'),
                            r'\d+\.\d+\.\d+\.\d+')


class HomeIpNeedsAgreement(unittest.TestCase):
    def setUp(self):
        self.h = load('homeip_under_test', 'docker/homeip/homeip.py', {})

    def test_two_that_agree(self):
        self.assertEqual(('8.8.4.4', None), self.h.decide({'a': '8.8.4.4', 'b': '8.8.4.4'}))

    def test_two_that_disagree(self):
        ip, why = self.h.decide({'a': '8.8.4.4', 'b': '8.8.8.8'})
        self.assertIsNone(ip)
        self.assertIn('disagree', why)

    def test_one_that_failed(self):
        self.assertIsNone(self.h.decide({'a': '8.8.4.4', 'b': None})[0])

    def test_one_lookup_is_not_enough(self):
        self.assertIsNone(self.h.decide({'a': '8.8.4.4'})[0])

    def test_private_and_ipv6_are_not_answers(self):
        self.assertIsNone(self.h.public_ipv4('192.168.1.61'))
        self.assertIsNone(self.h.public_ipv4('2606:4700::1'))
        self.assertEqual('8.8.4.4', self.h.public_ipv4(' 8.8.4.4\n'))


if __name__ == '__main__':
    unittest.main()
