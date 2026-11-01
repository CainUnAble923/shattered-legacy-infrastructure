"""cc-P735, cc-P762: the bot's New Haven start (creation's city 1) for T-039, P64-1 and P64-2.

cc-P762 locks every new player character to the Wakening Isle and to Human, shard-wide; only staff accounts (GameMaster
and up) still start in New Haven. So fresh(city=HAVEN_CITY) takes a staff pool account (STAFF_POOL), which the bot's
Admin makes GameMaster on first use ([BotStaffAccount), and a player pool account's character always wakes on the isle.
CREATE expects an Elf or a Gargoyle asked for on a player account to come out Human, told why. A fake client stands in
for the shard.

    python -P tools/playtest-bot/tests/test_haven_start.py
"""
import os
import re
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from uobot import checks as C, run as R  # noqa: E402
from uobot.client import Transcript  # noqa: E402

ISLE, HAVEN, LOCKED = "Tutorial Island", "New Haven", "New Haven (locked)"
BODIES = {"human": (400, 401), "elf": (605, 606), "gargoyle": (666, 667)}
MADE = "is a GameMaster account now"   # what the shard says (NewCharacterRules.BotStaffDone)


class FakeShard(object):
    """Which accounts are staff; what was created, and where it landed; what the Admin typed."""

    def __init__(self, raise_works=True):
        self.staff = set()
        self.raise_works = raise_works
        self.created = []       # (account, name, city, landed)
        self.logins = []
        self.admin_said = []
        self.serial = 0x1000


class FakeClient(object):
    shard = None

    def __init__(self, user, password, tr, cliloc, label=None):
        self.user, self.account, self.label = user, user, label
        self.chars, self.cities, self.messages, self.gumps = [], [], [], []
        self.closed, self.pos, self.serial, self.body, self.status = None, None, 0, 0, {}

    def login(self):
        s = self.shard
        s.logins.append(self.user)
        self.chars = ["", "", "", "", "", "", ""]
        self.cities = [(0, ISLE, "The Wakening Isle"), (1, HAVEN if self.user in s.staff else LOCKED, "The Bountiful Harvest Inn")]
        return self.chars

    def create(self, name, race="human", female=False, city=0):
        s = self.shard
        s.serial += 1
        self.serial = s.serial
        staff = self.user in s.staff
        haven = city == 1 and staff
        self.pos = (3634, 2586, 0) if haven else (6638, 3993, 0)
        made = race if staff or race == "human" else "human"
        if made != race:
            self.messages.append({"text": "Only Humans can be made for now; Elves and Gargoyles come later. This character was made Human."})
        self.body = BODIES[made][1 if female else 0]
        self.status = {"name": name}
        s.created.append((self.user, name, city, "haven" if haven else "isle"))
        return True

    def command(self, text, settle=0.8):
        s = self.shard
        s.admin_said.append(text)
        if text.startswith("[BotStaffAccount ") and s.raise_works:
            user = text.split()[1]
            s.staff.add(user)
            return [{"text": "%s %s." % (user, MADE)}]
        return [{"text": "That account cannot be made staff here."}]

    def find_text(self, pattern, since=0, timeout=0):
        return next((m for m in self.messages[since:] if re.search(pattern, m["text"], re.I)), None)

    def sleep(self, s):
        pass

    def close(self):
        self.closed = "closed by the bot"


class HavenStart(unittest.TestCase):
    def ctx(self, raise_works=True):
        shard = FakeShard(raise_works)
        FakeClient.shard = shard
        self._client, self._rest = R.Client, R.REST_SECONDS
        R.Client, R.REST_SECONDS = FakeClient, 0
        self.addCleanup(self.restore)
        ctx = R.Ctx.__new__(R.Ctx)
        ctx.secrets = {"playerPassword": "x", "admin": "a", "adminPassword": "x"}
        ctx.tr = Transcript(os.devnull)
        ctx.cliloc = None
        staff_pool = getattr(R, "STAFF_POOL", [])
        ctx.pool_rest = {a: 0.0 for a in R.POOL + staff_pool}
        import threading
        ctx.pool_lock, ctx.admin_lock = threading.Lock(), threading.RLock()
        ctx.first_logins, ctx.staff_ready, ctx.fresh_count, ctx.serials = {}, set(), 0, {}
        ctx.haven_open, ctx.haven_shut = set(), set()   # the cc-P735 fields, for a red run on the old code
        ctx.admin = FakeClient("a", "x", None, None, "admin")
        ctx.ensure_admin = lambda: ctx.admin
        return ctx, shard

    def restore(self):
        R.Client, R.REST_SECONDS = self._client, self._rest

    def test_a_player_pool_character_wakes_on_the_isle(self):
        ctx, shard = self.ctx()
        c = ctx.fresh()
        self.assertIn(c.account, R.POOL)
        self.assertEqual(shard.created, [(R.POOL[0], c.name, 0, "isle")])
        self.assertEqual(shard.admin_said, [])

    def test_new_haven_comes_from_a_staff_account_made_gamemaster_first(self):
        ctx, shard = self.ctx()
        c = ctx.fresh(city=R.HAVEN_CITY)
        self.assertIn(c.account, R.STAFF_POOL)
        self.assertNotIn(c.account, R.POOL)
        self.assertEqual(shard.admin_said, ["[BotStaffAccount %s" % c.account])
        self.assertEqual(shard.logins, [c.account, c.account], "a first login makes the account, then the real one")
        self.assertEqual(shard.created, [(c.account, c.name, 1, "haven")])
        self.assertEqual(c.pos[:2], (3634, 2586))
        self.assertIn(c.account, ctx.first_logins, "the real character's login is the one P64-1 reads")

    def test_a_staff_account_is_raised_once_per_run(self):
        ctx, shard = self.ctx()
        a = ctx.fresh(city=R.HAVEN_CITY)
        ctx.done(a)
        for x in R.STAFF_POOL:
            ctx.pool_rest[x] = 0.0 if x == a.account else float("inf")
        b = ctx.fresh(city=R.HAVEN_CITY)
        self.assertEqual(b.account, a.account)
        self.assertEqual(len(shard.admin_said), 1)

    def test_a_raise_that_does_not_happen_is_an_error_not_a_player_start(self):
        ctx, shard = self.ctx(raise_works=False)
        with self.assertRaises(R.LoginFailed):
            ctx.fresh(city=R.HAVEN_CITY)
        self.assertEqual(shard.created, [])

    def test_p64_1_prefers_an_account_without_a_first_login(self):
        ctx, shard = self.ctx()
        ctx.first_logins = {R.STAFF_POOL[0]: {}, R.STAFF_POOL[1]: {}}
        c = ctx.fresh(city=R.HAVEN_CITY, first_login=True)
        self.assertEqual(c.account, R.STAFF_POOL[2])

    def test_the_shard_line_and_the_bot_agree(self):
        self.assertEqual(R.STAFF_MADE, MADE)

    def test_t039_and_p64_1_ask_for_new_haven(self):
        src = open(C.__file__, encoding="utf-8").read()
        for fn in ("def t_039", "def p64_1"):
            body = src[src.index(fn):src.index("\n@check", src.index(fn))]
            self.assertRegex(body, r"ctx\.fresh\([^)]*city=HAVEN_CITY", fn)
        self.assertEqual(C.HAVEN_CITY, R.HAVEN_CITY)

    def test_create_expects_human_for_every_race_a_player_asks_for(self):
        ctx, shard = self.ctx()
        C.SHARED["create"] = 0
        results = []
        for _ in range(len(C.RACES)):
            results.append(C.create_all(ctx))
            for a in R.POOL:
                ctx.pool_rest[a] = 0.0
        self.assertTrue(all(ok for ok, _ in results), results)
        self.assertEqual({row[3] for row in shard.created}, {"isle"})
        self.assertTrue(any(ev.startswith("elf") for _, ev in results))


if __name__ == "__main__":
    unittest.main(verbosity=2)
