"""AGENTS.md keeps the standing rules every agent works under (cc-P158). Codex reads AGENTS.md, Claude Code reads it
through CLAUDE.md, so a rule dropped here is dropped for both.

    python -m unittest scripts/tests/test_agents_md.py

The docs repo has the same check (D:\\UO\\shard-migration\\tools\\tests\\test_agents_md.py).
"""
import os, re, unittest

AGENTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'AGENTS.md')

# Each rule: what it is, and a pattern its line must match inside the "Standing rules" section.
RULES = [
    ('never connect to Haven', r'(?i)never connect to Haven'),
    ('never edit docs\\', r'(?i)never edit `?docs\\`?'),
    ('the 5 MB rule', r'(?i)no file over 5 MB'),
    ('do not commit, the Golem commits', r'(?i)do not commit.*the Golem commits'),
    ('the DONE line', r'P<nn> DONE'),
    ('the STOPPED line', r'P<nn> STOPPED: <reason>'),
    ('tests seen failing first', r'(?i)seen failing first'),
    ('Windows PowerShell 5.1', r'Windows PowerShell 5\.1'),
    ('the pointer to the rest', r'CLAUDE\.md'),
]


def standing_section(text):
    m = re.search(r'(?ms)^## Standing rules for every agent run by the Golem$.*?(?=^## |\Z)', text)
    return m.group(0) if m else ''


class AgentsMdKeepsTheStandingRules(unittest.TestCase):
    def test_every_rule_is_in_the_standing_rules_section(self):
        with open(AGENTS, encoding='utf-8') as f:
            sec = standing_section(f.read())
        self.assertTrue(sec, 'AGENTS.md has no "## Standing rules for every agent run by the Golem" section')
        missing = [name for name, pat in RULES if not re.search(pat, sec)]
        self.assertEqual([], missing, 'AGENTS.md dropped: ' + ', '.join(missing))

    def test_the_check_notices_a_dropped_rule(self):
        with open(AGENTS, encoding='utf-8') as f:
            sec = standing_section(f.read())
        cut = re.sub(r'(?im)^.*seen failing first.*$', '', sec)
        self.assertTrue(any(not re.search(p, cut) for _, p in RULES), 'removing a rule must fail the check')


if __name__ == '__main__':
    unittest.main()
