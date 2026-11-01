"""cc-P735: the bot's New Haven start (creation's city 1) for T-039, P64-1 and P64-2.

On an isle-ready world a new character made with city 0 wakes on the isle (cc-P688). These three now ask fresh() for
city 1. cc-P717 locks New Haven for an account until one of its characters graduates or skips, so on a locked pool
account fresh() first has a throwaway wake on the isle and the bot's Admin skip it ([Tutorial skip), then takes an
account whose list shows New Haven. A fake client stands in for the shard.

    python -P tools/playtest-bot/tests/test_haven_start.py
"""
import os
import re
import sys
import time
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from uobot import checks as C, run as R  # noqa: E402
from uobot.client import Transcript  # noqa: E402

ISLE, HAVEN, LOCKED = "Tutorial Island", "New Haven", "New Haven (locked)"


class FakeShard(object):
    """Per account: whether New Haven is open; what was created, with which city; what the Admin typed."""

    def __init__(self, open_accounts=()):
        self.open = set(open_accounts)
        self.created = []       # (account, name, city)
        self.admin_said = []
        self.serial = 0x1000


class FakeClient(object):
    shard = None

    def __init__(self, user, password, tr, cliloc, label=None):
        self.user, self.account, self.label = user, user, label
        self.chars, self.cities, self.messages, self.gumps = [], [], [], []
        self.closed, self.pos, self.serial, self.target = None, None, 0, None

    def login(self):
        s = self.shard
        self.chars = ["", "", "", "", "", "", ""]
        self.cities = [(0, ISLE, "The Wakening Isle"), (1, HAVEN if self.user in s.open else LOCKED, "The Bountiful Harvest Inn")]
        return self.chars

    def create(self, name, race="human", female=False, city=0):
        s = self.shard
        s.serial += 1
        self.serial = s.serial
        s.created.append((self.user, name, city))
        haven = city == 1 and self.user in s.open
        self.pos = (3634, 2586, 0) if haven else (6638, 3993, 0)
        FakeClient.last_by_serial[self.serial] = self
        return True

    def command(self, text, settle=0.8):
        self.shard.admin_said.append(text)
        if text == "[Tutorial skip":
            self.target = (1,)
        return []

    def wait_target(self, timeout=8):
        return self.target

    def target_obj(self, serial):
        self.target = None
        victim = FakeClient.last_by_serial[serial]
        if self.shard.skip_works:
            self.shard.open.add(victim.user)   # ClusterFTutorial.Skip -> TutorialStart.Unlock
            victim.messages.append({"text": "You skipped the tutorial. Double-click Rook if you want ..."})

    def find_text(self, pattern, since=0, timeout=0):
        return next((m for m in self.messages[since:] if re.search(pattern, m["text"], re.I)), None)

    def sleep(self, s):
        pass

    def close(self):
        self.closed = "closed by the bot"


class HavenStart(unittest.TestCase):
    def ctx(self, open_accounts=(), skip_works=True):
        shard = FakeShard(open_accounts)
        shard.skip_works = skip_works
        FakeClient.shard = shard
        FakeClient.last_by_serial = {}
        self._client, self._rest = R.Client, R.REST_SECONDS
        R.Client, R.REST_SECONDS = FakeClient, 0
        self._look, R.LOOK_PAUSE = R.LOOK_PAUSE, 0
        self.addCleanup(self.restore)
        ctx = R.Ctx.__new__(R.Ctx)
        ctx.secrets = {"playerPassword": "x", "admin": "a", "adminPassword": "x"}
        ctx.tr = Transcript(os.devnull)
        ctx.cliloc = None
        ctx.pool_rest = {a: 0.0 for a in R.POOL}
        import threading
        ctx.pool_lock, ctx.admin_lock = threading.Lock(), threading.RLock()
        ctx.first_logins, ctx.haven_open, ctx.haven_shut, ctx.fresh_count, ctx.serials = {}, set(), set(), 0, {}
        ctx.admin = FakeClient("a", "x", None, None, "admin")
        ctx.ensure_admin = lambda: ctx.admin
        return ctx, shard

    def restore(self):
        R.Client, R.REST_SECONDS = self._client, self._rest
        R.LOOK_PAUSE = self._look

    def test_the_isle_is_still_the_default(self):
        ctx, shard = self.ctx()
        c = ctx.fresh()
        self.assertEqual(shard.created, [(R.POOL[0], c.name, 0)])

    def test_an_open_account_creates_in_new_haven_at_once(self):
        ctx, shard = self.ctx(open_accounts=[R.POOL[1]])
        c = ctx.fresh(city=R.HAVEN_CITY)
        self.assertEqual(c.account, R.POOL[1], "the account already open is picked first")
        self.assertEqual(shard.created, [(R.POOL[1], c.name, 1)])
        self.assertEqual(c.pos[:2], (3634, 2586))
        self.assertEqual(shard.admin_said, [])

    def test_a_locked_account_is_opened_by_a_skipped_throwaway_first(self):
        ctx, shard = self.ctx()
        c = ctx.fresh(city=R.HAVEN_CITY)
        throwaway = shard.created[0]
        self.assertEqual(throwaway[2], 0, "the throwaway takes the isle")
        self.assertEqual(throwaway[0], c.account, "on the account the real character then uses")
        self.assertEqual(len(shard.created), 2, "one throwaway for all three locked accounts")
        self.assertTrue(throwaway[1].startswith("Bot Unlock"))
        self.assertEqual(shard.admin_said[1:], ["[Tutorial skip"])
        self.assertTrue(shard.admin_said[0].startswith("[go 0x"))
        self.assertEqual(shard.created[-1], (c.account, c.name, 1), "then the real character asks for New Haven")
        self.assertIn(c.account, shard.open)
        self.assertEqual(c.pos[:2], (3634, 2586))
        self.assertIn(c.account, ctx.first_logins, "the real character's login is the one P64-1 reads")

    def test_a_locked_account_is_not_unlocked_while_another_may_be_open(self):
        ctx, shard = self.ctx(open_accounts=[R.POOL[2]])
        c = ctx.fresh(city=R.HAVEN_CITY)
        self.assertEqual(c.account, R.POOL[2])
        self.assertEqual(shard.created, [(R.POOL[2], c.name, 1)], "no throwaway: player4 was open")
        self.assertEqual(shard.admin_said, [])
        self.assertEqual(ctx.pool_rest[R.POOL[0]], 0.0, "a look at the list holds nothing: no rest")

    def test_with_an_open_account_resting_no_locked_account_is_logged_into(self):
        """run1: T-039 unlocked a second account while the one it had just opened was resting."""
        ctx, shard = self.ctx(open_accounts=[R.POOL[1]])
        ctx.haven_open = {R.POOL[1]}
        ctx.haven_shut = {R.POOL[0], R.POOL[2]}
        ctx.pool_rest[R.POOL[1]] = time.time() + 1.5
        logins = []
        real_login = FakeClient.login
        FakeClient.login = lambda c: (logins.append(c.user), real_login(c))[1]
        self.addCleanup(lambda: setattr(FakeClient, "login", real_login))
        c = ctx.fresh(city=R.HAVEN_CITY)
        self.assertEqual(c.account, R.POOL[1])
        self.assertEqual(logins, [R.POOL[1]], "it waited for the open account")
        self.assertEqual([n for n in shard.created if n[1].startswith("Bot Unlock")], [])

    def test_a_skip_that_does_not_happen_is_an_error_not_an_isle_start(self):
        ctx, shard = self.ctx(skip_works=False)
        with self.assertRaises(R.LoginFailed):
            ctx.fresh(city=R.HAVEN_CITY)
        self.assertNotIn(1, [city for _, _, city in shard.created])

    def test_p64_1_prefers_an_account_without_a_first_login(self):
        ctx, shard = self.ctx(open_accounts=R.POOL)
        ctx.first_logins = {R.POOL[0]: {}, R.POOL[1]: {}}
        c = ctx.fresh(city=R.HAVEN_CITY, first_login=True)
        self.assertEqual(c.account, R.POOL[2])

    def test_t039_and_p64_1_ask_for_new_haven(self):
        src = open(C.__file__, encoding="utf-8").read()
        for fn in ("def t_039", "def p64_1"):
            body = src[src.index(fn):src.index("\n@check", src.index(fn))]
            self.assertRegex(body, r"ctx\.fresh\([^)]*city=HAVEN_CITY", fn)
        self.assertEqual(C.HAVEN_CITY, R.HAVEN_CITY)


if __name__ == "__main__":
    unittest.main(verbosity=2)
