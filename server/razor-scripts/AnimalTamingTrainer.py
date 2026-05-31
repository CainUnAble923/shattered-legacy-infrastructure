# =============================================================================
# -*- coding: utf-8 -*-
# ClusterF Animal Taming Trainer for Razor Enhanced
#
# USAGE
#   In Razor Enhanced -> Scripts tab -> Add -> select this file -> Play.
#   Stand near animals, press SCAN, select one and press TAME, or turn AUTO on.
#
# SAFETY
#   This helper does not attack. WALK and AUTO WALK use Razor pathfinding only
#   toward scanned animals. Release automation is optional and off by default.
# =============================================================================

import clr
import re
import threading
import time

clr.AddReference('System.Windows.Forms')
clr.AddReference('System.Drawing')

from System import Action
from System.Windows.Forms import (
    Application, Form, Button, Label, ListBox, CheckBox, ToolTip, TextBox,
    FormBorderStyle, FormStartPosition, FlatStyle, Timer
)
from System.Drawing import Color, Font, FontStyle, Point, Size


# =============================================================================
# Configuration
# =============================================================================

SKILL_NAME = 'Animal Taming'

SCAN_RANGE = 12
TAME_RANGE = 2
PATH_STOP_RANGE = 1
PATH_TIMEOUT_SECONDS = 9.0
FOLLOW_PATH_TIMEOUT_SECONDS = 4.0
FOLLOW_CHECK_SECONDS = 0.5
PATH_MAX_CANDIDATES = 12
REFRESH_MS = 500
SCAN_EVERY_SECONDS = 2.0
ATTEMPT_TIMEOUT_SECONDS = 17.0
RETRY_DELAY_SECONDS = 1.0

# The bands are deliberately broad. Edit these names if ClusterF has custom
# training animals you want prioritized.
TRAINING_BANDS = [
    (0.0, 35.0, [
        'rabbit', 'bird', 'cat', 'dog', 'chicken', 'goat', 'sheep',
        'cow', 'pig', 'rat',
    ]),
    (25.0, 50.0, [
        'hind', 'hart', 'horse', 'pack horse', 'llama', 'timber wolf',
        'forest ostard',
    ]),
    (45.0, 70.0, [
        'black bear', 'brown bear', 'grizzly', 'panther', 'cougar',
        'snow leopard', 'alligator',
    ]),
    (60.0, 85.0, [
        'great hart', 'bull', 'frenzied ostard', 'ridgeback',
        'giant beetle',
    ]),
    (80.0, 105.0, [
        'hell hound', 'dire wolf', 'white wolf', 'unicorn', 'ki-rin',
    ]),
    (95.0, 120.0, [
        'nightmare', 'drake', 'dragon', 'white wyrm',
    ]),
]

DANGEROUS_WORDS = [
    'nightmare', 'dragon', 'drake', 'white wyrm', 'hell hound',
    'cu sidhe', 'reptalon', 'hiryu',
]

SUCCESS_PATTERNS = [
    re.compile(r'accept(?:s|ed)? you as (?:its|their|the) master', re.I),
    re.compile(r'you have tamed', re.I),
    re.compile(r'it seems to accept you as master', re.I),
]

RETRY_PATTERNS = [
    re.compile(r'you fail to tame', re.I),
    re.compile(r'you were unable to tame', re.I),
    re.compile(r'the animal looks angry', re.I),
    re.compile(r'you must wait', re.I),
    re.compile(r'too far away', re.I),
    re.compile(r'clear path', re.I),
]

REPATH_PATTERNS = [
    re.compile(r'clear path', re.I),
]

BAD_TARGET_PATTERNS = [
    re.compile(r'no chance of taming', re.I),
    re.compile(r'cannot be tamed', re.I),
    re.compile(r'that creature is already tame', re.I),
    re.compile(r'already being tamed', re.I),
    re.compile(r'someone else is already taming', re.I),
    re.compile(r'too many followers', re.I),
    re.compile(r'already control', re.I),
]


# =============================================================================
# Small RE helpers
# =============================================================================


def send(msg, hue=0x44):
    try:
        Misc.SendMessage('[Taming] ' + str(msg), hue)
    except:
        pass


def skill_value(real=True):
    try:
        if real:
            return float(Player.GetRealSkillValue(SKILL_NAME))
        return float(Player.GetSkillValue(SKILL_NAME))
    except:
        return 0.0


def skill_cap():
    try:
        return float(Player.GetSkillCap(SKILL_NAME))
    except:
        return 100.0


def followers_text():
    try:
        return '{}/{}'.format(Player.Followers, Player.FollowersMax)
    except:
        return '?/?'


def _xyz(obj):
    try:
        p = obj.Position
        return int(p.X), int(p.Y), int(p.Z)
    except:
        pass
    try:
        return int(obj.X), int(obj.Y), int(obj.Z)
    except:
        return None


def player_position():
    return _xyz(Player)


def mobile_position(mob):
    return _xyz(mob)


def tile_distance(a, b):
    if a is None or b is None:
        return 999
    return max(abs(int(a[0]) - int(b[0])), abs(int(a[1]) - int(b[1])))


def mobile_name(mob):
    try:
        name = mob.Name
        if name:
            return str(name)
    except:
        pass
    return 'unknown'


def mobile_serial(mob):
    try:
        return int(mob.Serial)
    except:
        return 0


def mobile_body(mob):
    for attr in ('MobileID', 'Body', 'ItemID'):
        try:
            return int(getattr(mob, attr))
        except:
            pass
    return 0


def mobile_notoriety(mob):
    try:
        return int(mob.Notoriety)
    except:
        return 0


def mobile_distance(mob):
    try:
        return int(mob.Distance)
    except:
        pass
    try:
        return int(Player.DistanceTo(mob))
    except:
        pass
    try:
        p = Player.Position
        mp = mob.Position
        return max(abs(int(p.X) - int(mp.X)), abs(int(p.Y) - int(mp.Y)))
    except:
        return 999


def _approach_candidates(mob, stop_range):
    mpos = mobile_position(mob)
    ppos = player_position()
    if mpos is None or ppos is None:
        return []

    mx, my, mz = mpos
    pz = ppos[2]
    candidates = []
    for ox in range(-stop_range, stop_range + 1):
        for oy in range(-stop_range, stop_range + 1):
            if ox == 0 and oy == 0:
                continue
            if max(abs(ox), abs(oy)) != stop_range:
                continue
            point = (mx + ox, my + oy, pz if pz is not None else mz)
            candidates.append((tile_distance(ppos, point), point))
    candidates.sort(key=lambda item: item[0])
    return [point for _score, point in candidates[:PATH_MAX_CANDIDATES]]


def _pathfind_point(x, y, z, run_path, timeout_seconds=PATH_TIMEOUT_SECONDS):
    try:
        route = PathFinding.Route()
        route.X = int(x)
        route.Y = int(y)
        route.Run = bool(run_path)
        route.StopIfStuck = True
        route.IgnoreMobile = True
        route.UseResync = False
        route.MaxRetry = 1
        route.Timeout = int(timeout_seconds)
        result = PathFinding.Go(route)
        return result is not False
    except:
        pass

    try:
        result = Player.PathFindTo(int(x), int(y), 1, bool(run_path), True, True, False, False)
        return result is not False
    except:
        pass

    try:
        result = Player.PathFindTo(int(x), int(y), int(z))
        return result is not False
    except:
        return False


def path_to_mobile(serial, run_path=True, timeout_seconds=PATH_TIMEOUT_SECONDS):
    mob = Mobiles.FindBySerial(int(serial))
    if mob is None:
        return False, 'target disappeared'
    if mobile_distance(mob) <= TAME_RANGE:
        return True, 'already in range'

    candidates = _approach_candidates(mob, PATH_STOP_RANGE)
    if not candidates:
        return False, 'target position unavailable'

    for x, y, z in candidates:
        if not _pathfind_point(x, y, z, run_path, timeout_seconds):
            continue

        deadline = time.time() + timeout_seconds
        while time.time() < deadline:
            mob = Mobiles.FindBySerial(int(serial))
            if mob is None:
                return False, 'target disappeared'
            if mobile_distance(mob) <= TAME_RANGE:
                return True, 'in range'
            time.sleep(0.25)

        mob = Mobiles.FindBySerial(int(serial))
        if mob is not None and mobile_distance(mob) <= TAME_RANGE:
            return True, 'in range'

    return False, 'could not path within taming range'


def bool_prop(obj, attr, default=False):
    try:
        return bool(getattr(obj, attr))
    except:
        return default


def safe_ignore(serial):
    try:
        Misc.IgnoreObject(int(serial))
        return True
    except:
        return False


def clear_ignores():
    try:
        Misc.ClearIgnore()
        return True
    except:
        return False


def use_taming(serial):
    serial = int(serial)
    try:
        Player.UseSkill(SKILL_NAME, serial, True)
        return True
    except:
        pass

    try:
        Player.UseSkill(SKILL_NAME)
        if Target.WaitForTarget(3500, False):
            Target.TargetExecute(serial)
            return True
    except:
        pass

    return False


def journal_texts(limit=40):
    try:
        entries = Journal.GetJournalEntry(limit)
    except:
        return []
    texts = []
    for entry in entries:
        try:
            texts.append(str(entry.Text))
        except:
            texts.append(str(entry))
    return texts


def match_any(patterns, texts):
    for text in texts:
        for pat in patterns:
            if pat.search(text):
                return text
    return None


def wait_taming_result(timeout_seconds, serial=None, follow=False, run_path=True, status_callback=None):
    deadline = time.time() + timeout_seconds
    next_follow = time.time() + FOLLOW_CHECK_SECONDS
    while time.time() < deadline:
        texts = journal_texts()

        hit = match_any(SUCCESS_PATTERNS, texts)
        if hit:
            return 'success', hit

        hit = match_any(BAD_TARGET_PATTERNS, texts)
        if hit:
            return 'bad', hit

        hit = match_any(RETRY_PATTERNS, texts)
        if hit:
            return 'retry', hit

        if follow and serial is not None and time.time() >= next_follow:
            next_follow = time.time() + FOLLOW_CHECK_SECONDS
            mob = Mobiles.FindBySerial(int(serial))
            if mob is None:
                return 'bad', 'Target disappeared while taming.'
            dist = mobile_distance(mob)
            if dist > PATH_STOP_RANGE:
                if status_callback is not None:
                    status_callback('Following target, distance {}...'.format(dist))
                ok, reason = path_to_mobile(serial, run_path, FOLLOW_PATH_TIMEOUT_SECONDS)
                if not ok and status_callback is not None:
                    status_callback('Still following: {}.'.format(reason))

        time.sleep(0.25)
    return 'timeout', 'No taming result before timeout.'


def release_pet(serial):
    serial = int(serial)
    try:
        ok = Misc.UseContextMenu(serial, 'Release', 2000)
    except:
        ok = False
    if not ok:
        return False

    try:
        if Gumps.WaitForGump(0, 1500):
            gid = Gumps.CurrentGump()
            # Most RunUO/ServUO release confirmations use button 2.
            Gumps.SendAction(gid, 2)
    except:
        pass
    return True


# =============================================================================
# Target selection
# =============================================================================


def _word_match(name, words):
    lname = name.lower()
    for word in words:
        if word in lname:
            return word
    return None


def animal_band(name, skill):
    best = None
    for lo, hi, words in TRAINING_BANDS:
        word = _word_match(name, words)
        if not word:
            continue
        if lo <= skill <= hi:
            return lo, hi, word, 0
        if skill < lo:
            penalty = lo - skill
        else:
            penalty = skill - hi
        if best is None or penalty < best[3]:
            best = (lo, hi, word, penalty)
    return best


def is_dangerous_name(name):
    return _word_match(name, DANGEROUS_WORDS) is not None


class TargetInfo(object):
    def __init__(self, mob, skill):
        self.serial = mobile_serial(mob)
        self.name = mobile_name(mob)
        self.body = mobile_body(mob)
        self.notoriety = mobile_notoriety(mob)
        self.distance = mobile_distance(mob)
        self.band = animal_band(self.name, skill)
        self.dangerous = is_dangerous_name(self.name)

    def band_text(self):
        if self.band is None:
            return 'unlisted'
        return '{:.0f}-{:.0f}'.format(self.band[0], self.band[1])

    def score(self):
        listed = 0 if self.band is not None else 1
        band_penalty = 0 if self.band is None else self.band[3]
        danger = 10 if self.dangerous else 0
        return (listed, danger, band_penalty, self.distance, self.name.lower())

    def __str__(self):
        danger = ' !' if self.dangerous else ''
        return '{}{} | {} | d{} | body 0x{:04X} | {}'.format(
            self.name, danger, self.band_text(), self.distance, self.body, self.serial)

    def ToString(self):
        return self.__str__()


def scan_targets(require_known=True, avoid_danger=True):
    skill = skill_value(True)
    try:
        flt = Mobiles.Filter()
        flt.Enabled = True
        flt.RangeMin = 0
        flt.RangeMax = SCAN_RANGE
        flt.IsHuman = 0
        flt.IsGhost = 0
        flt.CheckIgnoreObject = True
        try:
            flt.CheckLineOfSight = True
        except:
            pass
        try:
            flt.Notorieties.Add(1)
            flt.Notorieties.Add(3)
        except:
            pass
        mobs = list(Mobiles.ApplyFilter(flt))
    except Exception as ex:
        send('Mobile scan failed: {}'.format(ex), 0x22)
        return []

    targets = []
    for mob in mobs:
        serial = mobile_serial(mob)
        if not serial:
            continue
        try:
            if serial == int(Player.Serial):
                continue
        except:
            pass
        if bool_prop(mob, 'IsHuman') or bool_prop(mob, 'IsGhost'):
            continue
        if bool_prop(mob, 'CanRename'):
            continue
        if bool_prop(mob, 'InParty'):
            continue
        if bool_prop(mob, 'WarMode'):
            continue
        if mobile_notoriety(mob) not in (1, 3):
            continue

        info = TargetInfo(mob, skill)
        if require_known and info.band is None:
            continue
        if avoid_danger and info.dangerous:
            continue
        targets.append(info)

    targets.sort(key=lambda t: t.score())
    return targets


# =============================================================================
# UI
# =============================================================================


class TamingForm(Form):
    def __init__(self):
        super(TamingForm, self).__init__()
        self._tips = ToolTip()
        self._targets = []
        self._running = False
        self._active = False
        self._last_scan = 0
        self._last_serial = None
        self._attempts = 0
        self._successes = 0
        self._fails = 0
        self._gains = 0
        self._start_skill = skill_value(True)
        self._last_skill = self._start_skill
        self._last_attempt_time = 0
        self._build_ui()
        self._timer = Timer()
        self._timer.Interval = REFRESH_MS
        self._timer.Tick += self._on_tick
        self._timer.Start()

    def _build_ui(self):
        self.Text = 'ClusterF Taming Trainer v1.3'
        self.FormBorderStyle = FormBorderStyle.FixedToolWindow
        self.TopMost = True
        self.BackColor = Color.FromArgb(22, 24, 24)
        self.ForeColor = Color.Gainsboro
        self.Width = 468
        self.Height = 406
        self.StartPosition = FormStartPosition.Manual
        self.Location = Point(130, 130)

        self._lbl_skill = Label()
        self._lbl_skill.Location = Point(8, 8)
        self._lbl_skill.Size = Size(442, 18)
        self._lbl_skill.Font = Font('Consolas', 8, FontStyle.Regular)
        self.Controls.Add(self._lbl_skill)

        self._lbl_stats = Label()
        self._lbl_stats.Location = Point(8, 28)
        self._lbl_stats.Size = Size(442, 18)
        self._lbl_stats.Font = Font('Consolas', 8, FontStyle.Regular)
        self.Controls.Add(self._lbl_stats)

        self._list = ListBox()
        self._list.Location = Point(8, 52)
        self._list.Size = Size(442, 150)
        self._list.BackColor = Color.FromArgb(12, 12, 12)
        self._list.ForeColor = Color.Gainsboro
        self._list.Font = Font('Consolas', 8, FontStyle.Regular)
        self.Controls.Add(self._list)

        self._btn_scan = self._button('SCAN', 8, 210, 54, self._on_scan)
        self._btn_walk = self._button('WALK', 66, 210, 58, self._on_walk)
        self._btn_auto = self._button('AUTO OFF', 128, 210, 76, self._on_auto)
        self._btn_tame = self._button('TAME', 208, 210, 56, self._on_tame)
        self._btn_ignore = self._button('IGNORE', 268, 210, 64, self._on_ignore)
        self._btn_release = self._button('RELEASE', 336, 210, 72, self._on_release)
        self._btn_clear = self._button('CLR', 412, 210, 38, self._on_clear)

        self._chk_known = self._check('Known only', 10, 240, True)
        self._chk_danger = self._check('Avoid danger', 112, 240, True)
        self._chk_ignore = self._check('Ignore success', 224, 240, True)
        self._chk_walk = self._check('Auto walk', 10, 264, True)
        self._chk_follow = self._check('Follow target', 112, 264, True)
        self._chk_run = self._check('Run path', 224, 264, True)
        self._chk_release = self._check('Auto release', 334, 264, False)

        self._lbl_filter = Label()
        self._lbl_filter.Text = 'Filter:'
        self._lbl_filter.Location = Point(8, 290)
        self._lbl_filter.Size = Size(38, 18)
        self._lbl_filter.Font = Font('Arial Narrow', 8, FontStyle.Regular)
        self._lbl_filter.ForeColor = Color.Gainsboro
        self.Controls.Add(self._lbl_filter)

        self._txt_filter = TextBox()
        self._txt_filter.Location = Point(48, 288)
        self._txt_filter.Size = Size(402, 20)
        self._txt_filter.BackColor = Color.FromArgb(12, 12, 12)
        self._txt_filter.ForeColor = Color.Gainsboro
        self._txt_filter.Font = Font('Consolas', 8, FontStyle.Regular)
        self._txt_filter.TextChanged += self._on_filter_changed
        self.Controls.Add(self._txt_filter)
        self._tips.SetToolTip(self._txt_filter, 'Only tame animals whose name contains this text (case-insensitive). Leave blank for all.')

        self._lbl_status = Label()
        self._lbl_status.Location = Point(8, 318)
        self._lbl_status.Size = Size(442, 38)
        self._lbl_status.Font = Font('Consolas', 8, FontStyle.Regular)
        self._lbl_status.ForeColor = Color.FromArgb(180, 210, 180)
        self.Controls.Add(self._lbl_status)

        self._tips.SetToolTip(self._btn_walk, 'Pathfind near the selected animal without taming.')
        self._tips.SetToolTip(self._btn_release, 'Release the last successful tame or selected tame.')
        self._tips.SetToolTip(self._chk_walk, 'When enabled, TAME and AUTO pathfind toward out-of-range targets.')
        self._tips.SetToolTip(self._chk_follow, 'During taming, keep pathing after the animal if it wanders away.')
        self._tips.SetToolTip(self._chk_run, 'Use running instead of walking during pathfinding.')
        self._tips.SetToolTip(self._chk_release, 'Optional. Uses the pet context menu and confirms release gumps.')
        self._set_status('Ready. Stand near animals and scan.')
        self._update_labels()

    def _button(self, text, x, y, w, handler):
        btn = Button()
        btn.Text = text
        btn.Location = Point(x, y)
        btn.Size = Size(w, 22)
        btn.FlatStyle = FlatStyle.Flat
        btn.BackColor = Color.FromArgb(38, 42, 44)
        btn.ForeColor = Color.Gainsboro
        btn.FlatAppearance.BorderColor = Color.FromArgb(85, 95, 100)
        btn.Font = Font('Arial Narrow', 7, FontStyle.Bold)
        btn.Click += handler
        self.Controls.Add(btn)
        return btn

    def _check(self, text, x, y, checked):
        chk = CheckBox()
        chk.Text = text
        chk.Location = Point(x, y)
        chk.Size = Size(104, 20)
        chk.Checked = checked
        chk.ForeColor = Color.Gainsboro
        chk.BackColor = Color.Transparent
        chk.Font = Font('Arial Narrow', 8, FontStyle.Regular)
        self.Controls.Add(chk)
        return chk

    def _ui(self, callback):
        try:
            if self.IsDisposed:
                return
            if self.InvokeRequired:
                self.BeginInvoke(Action(callback))
            else:
                callback()
        except:
            pass

    def _set_status(self, text, hue=None):
        self._lbl_status.Text = str(text)
        if hue == 'bad':
            self._lbl_status.ForeColor = Color.FromArgb(230, 135, 120)
        elif hue == 'good':
            self._lbl_status.ForeColor = Color.FromArgb(130, 230, 130)
        elif hue == 'warn':
            self._lbl_status.ForeColor = Color.FromArgb(235, 205, 120)
        else:
            self._lbl_status.ForeColor = Color.FromArgb(180, 210, 180)

    def _update_labels(self):
        real = skill_value(True)
        shown = skill_value(False)
        cap = skill_cap()
        self._lbl_skill.Text = '{} {:.1f}/{:.1f} shown {:.1f} | followers {}'.format(
            SKILL_NAME, real, cap, shown, followers_text())
        self._lbl_stats.Text = 'tries {} | success {} | fail {} | gains {} | start {:.1f}'.format(
            self._attempts, self._successes, self._fails, self._gains, self._start_skill)

    def _scan(self, quiet=False):
        selected = None
        try:
            item = self._list.SelectedItem
            if item is not None:
                selected = item.serial
        except:
            selected = None

        self._targets = scan_targets(self._chk_known.Checked, self._chk_danger.Checked)
        try:
            filter_text = self._txt_filter.Text.strip().lower()
        except:
            filter_text = ''
        if filter_text:
            self._targets = [t for t in self._targets if filter_text in t.name.lower()]
        self._list.Items.Clear()
        for target in self._targets:
            self._list.Items.Add(target)
            if selected and target.serial == selected:
                self._list.SelectedItem = target
        if self._list.SelectedIndex < 0 and self._list.Items.Count > 0:
            self._list.SelectedIndex = 0
        self._last_scan = time.time()
        if not quiet:
            self._set_status('Found {} target(s).'.format(len(self._targets)))

    def _selected_target(self):
        try:
            item = self._list.SelectedItem
            if item is not None:
                return item
        except:
            pass
        return None

    def _best_auto_target(self):
        self._scan(True)
        for target in self._targets:
            if self._chk_walk.Checked or target.distance <= TAME_RANGE:
                return target
        return None

    def _on_filter_changed(self, sender, event):
        if not self._active:
            self._scan(True)

    def _on_scan(self, sender, event):
        self._scan()

    def _on_auto(self, sender, event):
        self._running = not self._running
        if self._running:
            self._btn_auto.Text = 'AUTO ON'
            self._btn_auto.BackColor = Color.FromArgb(25, 78, 35)
            self._set_status('AUTO on. Attempts nearby eligible animals.', 'good')
            send('Auto taming ON. Stay within {} tiles of targets.'.format(TAME_RANGE), 0x44)
        else:
            self._btn_auto.Text = 'AUTO OFF'
            self._btn_auto.BackColor = Color.FromArgb(38, 42, 44)
            self._set_status('AUTO off.')
            send('Auto taming OFF.', 0x44)

    def _on_tame(self, sender, event):
        target = self._selected_target()
        if target is None:
            self._set_status('No target selected.', 'warn')
            return
        self._start_attempt(target, manual=True)

    def _on_walk(self, sender, event):
        target = self._selected_target()
        if target is None:
            self._set_status('No target selected.', 'warn')
            return
        if self._active:
            return
        self._active = True
        run_path = bool(self._chk_run.Checked)
        t = threading.Thread(target=self._walk_worker, args=(target, run_path))
        t.daemon = True
        t.start()

    def _on_ignore(self, sender, event):
        target = self._selected_target()
        if target is None:
            return
        if safe_ignore(target.serial):
            self._set_status('Ignored {}.'.format(target.name))
            self._scan()

    def _on_release(self, sender, event):
        target = self._selected_target()
        serial = self._last_serial
        if target is not None and bool_prop(Mobiles.FindBySerial(target.serial), 'CanRename'):
            serial = target.serial
        if not serial:
            self._set_status('No recent tame to release.', 'warn')
            return
        if release_pet(serial):
            self._set_status('Release requested for {}.'.format(serial), 'good')
        else:
            self._set_status('Release failed or unavailable.', 'bad')

    def _on_clear(self, sender, event):
        if clear_ignores():
            self._set_status('Ignore list cleared.')
            self._scan()

    def _start_attempt(self, target, manual=False):
        if self._active:
            return
        if target.distance > TAME_RANGE:
            if not self._chk_walk.Checked:
                self._set_status('Move closer to {}. Distance {} > {}.'.format(
                    target.name, target.distance, TAME_RANGE), 'warn')
                if manual:
                    send('Move closer to {} before taming, or enable Auto walk.'.format(target.name), 0x35)
                return
        mob = Mobiles.FindBySerial(target.serial)
        if mob is None:
            self._set_status('Target disappeared.', 'warn')
            self._scan()
            return

        self._active = True
        auto_ignore = bool(self._chk_ignore.Checked)
        auto_release = bool(self._chk_release.Checked)
        auto_walk = bool(self._chk_walk.Checked)
        follow_target = bool(self._chk_follow.Checked)
        run_path = bool(self._chk_run.Checked)
        t = threading.Thread(target=self._attempt_worker,
                             args=(target, auto_ignore, auto_release, auto_walk, follow_target, run_path))
        t.daemon = True
        t.start()

    def _walk_worker(self, target, run_path):
        try:
            self._ui(lambda: self._set_status('Walking to {}...'.format(target.name)))
            ok, reason = path_to_mobile(target.serial, run_path)
            if ok:
                self._ui(lambda: self._set_status('In range of {}.'.format(target.name), 'good'))
            else:
                self._ui(lambda: self._set_status('Walk failed: {}.'.format(reason), 'bad'))
        finally:
            self._active = False
            self._ui(lambda: self._update_labels())
            self._ui(lambda: self._scan(True))

    def _attempt_worker(self, target, auto_ignore, auto_release, auto_walk, follow_target, run_path):
        before = skill_value(True)
        self._attempts += 1
        self._last_attempt_time = time.time()
        self._ui(lambda: self._set_status('Preparing {}...'.format(target.name)))
        self._ui(lambda: self._update_labels())

        try:
            mob = Mobiles.FindBySerial(target.serial)
            if mob is None:
                self._fails += 1
                self._ui(lambda: self._set_status('Target disappeared.', 'warn'))
                return

            if mobile_distance(mob) > TAME_RANGE:
                if not auto_walk:
                    self._fails += 1
                    self._ui(lambda: self._set_status('Move closer to {}.'.format(target.name), 'warn'))
                    return
                self._ui(lambda: self._set_status('Pathing to {}...'.format(target.name)))
                ok, reason = path_to_mobile(target.serial, run_path)
                if not ok:
                    self._fails += 1
                    self._ui(lambda: self._set_status('Path failed: {}.'.format(reason), 'bad'))
                    return

            self._ui(lambda: self._set_status('Taming {}...'.format(target.name)))
            try:
                Journal.Clear()
            except:
                pass

            if not use_taming(target.serial):
                self._fails += 1
                self._ui(lambda: self._set_status('Could not start Animal Taming.', 'bad'))
                return

            def _follow_status(text):
                self._ui(lambda: self._set_status(text))

            outcome, text = wait_taming_result(
                ATTEMPT_TIMEOUT_SECONDS,
                target.serial,
                follow_target,
                run_path,
                _follow_status)
            after = skill_value(True)
            gained = after > before + 0.0001
            if gained:
                self._gains += 1

            if outcome == 'success':
                self._successes += 1
                self._last_serial = target.serial
                if auto_ignore:
                    safe_ignore(target.serial)
                if auto_release:
                    release_pet(target.serial)
                msg = 'Tamed {}{}.'.format(target.name, ' +gain' if gained else '')
                send(msg, 0x44)
                self._ui(lambda: self._set_status(msg, 'good'))
            elif outcome == 'bad':
                self._fails += 1
                safe_ignore(target.serial)
                msg = '{}: {}'.format(target.name, text)
                send(msg, 0x22)
                self._ui(lambda: self._set_status(msg, 'bad'))
            elif outcome == 'retry':
                self._fails += 1
                if match_any(REPATH_PATTERNS, [text]):
                    self._ui(lambda: self._set_status('No clear path to {}, re-pathing...'.format(target.name), 'warn'))
                    path_to_mobile(target.serial, run_path)
                else:
                    msg = '{}: retry{}'.format(target.name, ' +gain' if gained else '')
                    self._ui(lambda: self._set_status(msg, 'warn'))
            else:
                self._fails += 1
                self._ui(lambda: self._set_status('Timed out on {}.'.format(target.name), 'warn'))
        finally:
            self._active = False
            self._ui(lambda: self._update_labels())
            self._ui(lambda: self._scan(True))

    def _on_tick(self, sender, event):
        self._update_labels()
        now = time.time()
        if now - self._last_scan > SCAN_EVERY_SECONDS and not self._active:
            self._scan(True)
        if not self._running or self._active:
            return
        if now - self._last_attempt_time < RETRY_DELAY_SECONDS:
            return
        target = self._best_auto_target()
        if target is None:
            wait_range = SCAN_RANGE if self._chk_walk.Checked else TAME_RANGE
            self._set_status('AUTO waiting for a target within {} tiles.'.format(wait_range), 'warn')
            return
        self._start_attempt(target)


# =============================================================================
# Entry point
# =============================================================================


def main():
    send('Starting Animal Taming Trainer.', 0x44)
    Application.EnableVisualStyles()
    form = TamingForm()
    Application.Run(form)
    send('Animal Taming Trainer closed.', 0x44)


main()
