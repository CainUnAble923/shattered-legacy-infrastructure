"""cc-P207: the glow behind each Golem's portrait on the web console.

GlowState maps every Golem state to its glow class (panels.glow_state) with Iron's, Gold's and a Bronze status file in
the real schema (tests/fixtures/golem-status: the real files of 2026-10-20 cut to the fields the glow reads; Bronze's
real file is on the game PC, so it is the same schema resting). GlowCards: the server reads each Golem's own status
file (a local one's status-<name>.json, a remote one's posted status) and settings.json stallMinutes. PhoneGlow: on a
390 px phone, five Golems in the five states, their classes and animations, prefers-reduced-motion (no animation, the
ring kept), the "?" legend, and no sideways scroll; GLOW_SCREENSHOT=<path> saves the Golem tab there.
"""
import calendar
import json
import os
import shutil
import sys
import time
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, HERE)

import panels  # noqa: E402
import test_remote_golems as trg  # noqa: E402
from test_remote_golems import EDGE, CHASE  # noqa: E402

FIX = os.path.join(HERE, "fixtures", "golem-status")


def status(name, **changes):
    with open(os.path.join(FIX, "status-%s.json" % name), encoding="utf-8") as f:
        d = json.load(f)
    d.update(changes)
    return d


def utc(epoch):
    return time.strftime("%Y-%m-%dT%H:%M:%S", time.gmtime(epoch))


def card(state, **more):
    c = {"name": "Iron", "state": state, "enabled": True, "remote": False, "online": True, "needsYou": ""}
    c.update(more)
    return c


class GlowState(unittest.TestCase):

    def test_the_real_status_files(self):
        gold = status("Gold")   # RUNNING, P207, the Activity hooks' last tool at lastActivityUtc
        last = calendar.timegm(time.strptime(gold["lastActivityUtc"], "%Y-%m-%dT%H:%M:%S"))
        g = card("RUNNING", name="Gold")
        self.assertEqual("working", panels.glow_state(g, gold, last + 120))
        self.assertEqual("quiet", panels.glow_state(g, gold, last + 15 * 60))
        self.assertEqual("needs", panels.glow_state(g, gold, last + 46 * 60), "past the stall email")
        iron = status("Iron")   # OFF and disabled
        self.assertEqual("off", panels.glow_state(card(iron["state"], enabled=iron["enabled"]), iron, last))
        bronze = status("Bronze")   # RESTING: back-off
        self.assertEqual("waiting", panels.glow_state(card(bronze["state"], name="Bronze"), bronze, last))

    def test_every_state_maps_to_its_glow(self):
        now = time.time()
        busy = {"lastActivityUtc": utc(now - 60)}
        want = {"RUNNING": "working", "IDLE": "waiting", "RESTING": "waiting", "WAITING": "waiting", "PAUSED": "waiting",
                "NEEDS YOU": "needs", "STOPPED": "needs", "OFF": "off", "DISABLED": "off", "UNKNOWN": "off", "": "off"}
        for state, glow in want.items():
            with self.subTest(state=state):
                self.assertEqual(glow, panels.glow_state(card(state), busy, now))

    def test_what_overrides_the_state(self):
        now = time.time()
        busy = {"lastActivityUtc": utc(now - 60)}
        self.assertEqual("needs", panels.glow_state(card("RUNNING", needsYou="a decision"), busy, now))
        self.assertEqual("off", panels.glow_state(card("RUNNING", enabled=False), busy, now))
        self.assertEqual("off", panels.glow_state(card("RUNNING", remote=True, online=False), busy, now), "an agent that stopped posting")
        self.assertEqual("working", panels.glow_state(card("RUNNING", remote=True, online=True), busy, now))
        self.assertEqual("off", panels.glow_state(None))

    def test_quiet_and_stalled_come_from_the_last_activity(self):
        now = time.time()
        self.assertEqual("working", panels.glow_state(card("RUNNING"), {"lastActivityUtc": utc(now - 9 * 60)}, now))
        self.assertEqual("quiet", panels.glow_state(card("RUNNING"), {"lastActivityUtc": utc(now - 11 * 60)}, now))
        self.assertEqual("quiet", panels.glow_state(card("RUNNING"), {"lastActivityUtc": utc(now - 44 * 60)}, now))
        self.assertEqual("needs", panels.glow_state(card("RUNNING"), {"lastActivityUtc": utc(now - 45 * 60)}, now))
        self.assertEqual("quiet", panels.glow_state(card("RUNNING"), {"lastActivityUtc": utc(now - 45 * 60)}, now, stall_minutes=60))
        # No activity yet: startedAt (this PC's local time), as golem.ps1's stall check falls back; nothing at all: working.
        started = time.strftime("%Y-%m-%dT%H:%M:%S", time.localtime(now - 20 * 60))
        self.assertEqual("quiet", panels.glow_state(card("RUNNING"), {"lastActivityUtc": "", "startedAt": started}, now))
        self.assertEqual("working", panels.glow_state(card("RUNNING"), {}, now))
        self.assertEqual("working", panels.glow_state(card("RUNNING"), {"lastActivityUtc": "not a time"}, now))


class GlowCards(trg.RemoteGolems):
    """golem_cards on the console: RemoteGolems' registry (Iron and Bronze here, Gold remote, Silver disabled)."""

    def write(self, path, value):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(value, f)

    def setUp(self):
        super().setUp()
        now = time.time()
        self.console.clock = lambda: now
        self.write(os.path.join(self.groot, "status-Iron.json"), status("Gold", golemName="Iron", lastActivityUtc=utc(now - 60)))
        self.write(os.path.join(self.groot, "status-Bronze.json"), status("Bronze"))
        self.write(os.path.join(self.groot, "remote", "Gold", "status.json"), status("Gold", lastActivityUtc=utc(now - 15 * 60)))
        self.write(os.path.join(self.groot, "remote", "Gold", "agent.json"), {"lastSeen": time.strftime("%Y-%m-%dT%H:%M:%S", time.localtime(now))})

    def glows(self, states):
        doc = {"golem": {"golems": [dict(name=n, state=s, enabled=n != "Silver") for n, s in states.items()]}}
        self.console.golem_cards(doc)
        return {c["name"]: c["glow"] for c in doc["golem"]["golems"]}

    def test_each_golem_glows_from_its_own_status_file(self):
        got = self.glows({"Iron": "RUNNING", "Bronze": "RESTING", "Gold": "UNKNOWN", "Silver": "DISABLED"})
        # Gold's state comes from the status its agent posted (RUNNING), quiet for 15 minutes.
        self.assertEqual({"Iron": "working", "Bronze": "waiting", "Gold": "quiet", "Silver": "off"}, got)

    def test_the_stall_minutes_setting(self):
        self.write(os.path.join(self.groot, "settings.json"), {"stallMinutes": 12})
        self.assertEqual("needs", self.glows({"Gold": "UNKNOWN"})["Gold"])

    def test_a_remote_golem_offline_has_no_glow(self):
        os.remove(os.path.join(self.groot, "remote", "Gold", "agent.json"))
        self.assertEqual("off", self.glows({"Gold": "UNKNOWN"})["Gold"])


@unittest.skipUnless(os.path.exists(EDGE), "Microsoft Edge is not installed here")
class PhoneGlow(GlowCards):
    """The Golem tab on a 390 px phone with five Golems, one in each state, and real portraits."""

    def setUp(self):
        super().setUp()
        reg_path = os.path.join(self.groot, "golems.json")
        with open(reg_path, encoding="utf-8") as f:
            reg = json.load(f)
        copper = dict(reg["golems"][1], name="Copper", lane="Sonnet")
        reg["golems"].append(copper)
        art = os.path.join(os.path.dirname(os.path.dirname(HERE)), "scripts", "console-art", "golems")
        for g in reg["golems"]:
            dst = os.path.join(self.repo, "scripts", "console-art", "golems", g["name"].lower() + ".png")
            shutil.copyfile(os.path.join(art, g["name"].lower() + ".png"), dst)
            g["portrait"] = dst
        with open(reg_path, "w", encoding="utf-8") as f:
            json.dump(reg, f)
        st = os.stat(reg_path)
        os.utime(reg_path, (st.st_atime, st.st_mtime + 5))   # the registry is re-read when its time changes
        with open(self.status, encoding="utf-8") as f:
            doc = json.load(f)
        want = {"Iron": "RUNNING", "Bronze": "RESTING", "Gold": "UNKNOWN", "Silver": "DISABLED", "Copper": "NEEDS YOU"}
        for c in doc["golem"]["golems"]:
            c["state"] = want[c["name"]]
        doc["golem"]["golems"].append(dict(doc["golem"]["golems"][1], name="Copper", lane="Sonnet", state="NEEDS YOU",
                                           needsYou="P999 stopped: a decision"))
        with open(self.status, "w", encoding="utf-8") as f:
            json.dump(doc, f)
        self.console.clock = time.time   # the page's own polls: the times above are relative to now

    def test_five_states_on_a_phone(self):
        import cdp
        with cdp.Browser(EDGE) as b:
            b.phone(390, 844)
            b.call("Network.enable")
            b.call("Network.setExtraHTTPHeaders", headers=CHASE)
            b.open("http://127.0.0.1:%d/#golem" % self.tail_port)
            self.assertTrue(b.wait_for("document.querySelectorAll('#golem-team .portrait-glow').length === 5"), "a glowing portrait per Golem")
            got = json.loads(b.eval("JSON.stringify(Array.prototype.map.call(document.querySelectorAll('#golem-team .portrait-glow'), "
                                    "function (x) { return [x.querySelector('img').alt, x.className, getComputedStyle(x).animationName, "
                                    "getComputedStyle(x).boxShadow, getComputedStyle(x.querySelector('img')).opacity, x.title]; }))"))
            rows = {r[0].split(" ")[0]: r for r in got}
            want = {"Iron": ("glow-working", "glow-pulse"), "Gold": ("glow-quiet", "glow-soft"), "Bronze": ("glow-waiting", "none"),
                    "Copper": ("glow-needs", "glow-alarm"), "Silver": ("glow-off", "none")}
            for name, (cls, anim) in want.items():
                with self.subTest(golem=name):
                    self.assertIn(cls, rows[name][1].split())
                    self.assertIn("metal-" + name.lower(), rows[name][1].split())
                    self.assertEqual(anim, rows[name][2])
            self.assertNotEqual("none", rows["Bronze"][3], "waiting: a faint static ring")
            self.assertEqual("none", rows["Silver"][3], "off: no glow")
            self.assertLess(float(rows["Silver"][4]), 1.0, "off: the portrait faded")
            self.assertEqual("Copper: Needs you", rows["Copper"][5])
            self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391, "no sideways scroll on a phone")
            # The legend: hidden until "?", then one line with the five states.
            self.assertTrue(b.eval("document.getElementById('glow-legend').hidden"))
            b.eval("document.getElementById('glow-help').click()")
            legend = b.eval("document.getElementById('glow-legend').innerText")
            for word in ("Working", "Quiet 10+ min", "Waiting", "Needs you", "Off or offline"):
                self.assertIn(word, legend)
            self.assertEqual("true", b.eval("document.getElementById('glow-help').getAttribute('aria-expanded')"))
            self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391)
            shot = os.environ.get("GLOW_SCREENSHOT")
            if shot:
                b.eval("document.getElementById('golem-team-card').scrollIntoView(); window.scrollBy(0, -90)")   # clear the sticky tabs
                time.sleep(1.0)   # mid-pulse rather than the first frame
                b.screenshot(shot)
            # prefers-reduced-motion: no animation, and the state still reads from the ring.
            b.call("Emulation.setEmulatedMedia", features=[{"name": "prefers-reduced-motion", "value": "reduce"}])
            calm = json.loads(b.eval("JSON.stringify(Array.prototype.map.call(document.querySelectorAll('#golem-team .portrait-glow'), "
                                     "function (x) { return [x.querySelector('img').alt.split(' ')[0], getComputedStyle(x).animationName, getComputedStyle(x).boxShadow]; }))"))
            for name, anim, shadow in calm:
                with self.subTest(golem=name, reduced=True):
                    self.assertEqual("none", anim)
                    self.assertEqual(name == "Silver", shadow == "none", shadow)


for _name in [n for n in dir(trg.RemoteGolems) if n.startswith("test_")]:
    setattr(GlowCards, _name, None)   # RemoteGolems' own tests run once, in test_remote_golems
for _name in [n for n in dir(GlowCards) if n.startswith("test_") and n not in PhoneGlow.__dict__]:
    setattr(PhoneGlow, _name, None)


if __name__ == "__main__":
    unittest.main()
