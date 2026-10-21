"""A headless UO client for the scratch shard (cc-P91). Standard library only.

One Client is one account on one connection. It logs in exactly as a 7.0.117.0 client with no encryption does
(seed, 0x80, server select, relay, 0x91; README.md has the sequence with pinned citations), then keeps a small model
of what the server has said: messages (with cliloc text resolved from the client's own Cliloc.enu), gumps, the
pending target, items and mobiles it has seen, its own status and skills. Every packet in and out goes to the
transcript, the readable parts in full, so a failure can be read without a rerun.
"""
import os
import random
import re
import socket
import struct
import threading
import time

from . import huffman
from . import packets as P
from .safety import HOST, PORT, check_relay, check_target

DIRS = {"n": 0, "ne": 1, "e": 2, "se": 3, "s": 4, "sw": 5, "w": 6, "nw": 7}
DELTA = {0: (0, -1), 1: (1, -1), 2: (1, 0), 3: (1, 1), 4: (0, 1), 5: (-1, 1), 6: (-1, 0), 7: (-1, -1)}


class LoginFailed(Exception):
    pass


class Cliloc(object):
    """The client's Cliloc.enu (uobot/cliloc.py reads it as pinned does). Arguments '~1_NAME~' are filled from the
    tab-separated args, and an argument '#NNN' is itself a cliloc number."""

    def __init__(self, path, cache_dir=None):
        self.table = {}
        if path and os.path.exists(path):
            from . import cliloc as _c
            self.table = _c.load(path, cache_dir or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "cache"))

    def text(self, number, args=""):
        t = self.table.get(number)
        if t is None:
            return "#%d%s" % (number, (" [" + args + "]") if args else "")
        parts = args.split("\t") if args else []
        def sub(m):
            i = int(m.group(1)) - 1
            if i < len(parts):
                a = parts[i]
                if a.startswith("#") and a[1:].isdigit():
                    return self.text(int(a[1:]))
                return a
            return ""
        return re.sub(r"~(\d+)_[^~]*~", sub, t)


class Transcript(object):
    def __init__(self, path):
        self.f = open(path, "a", encoding="utf-8")
        self.t0 = time.time()
        self.lock = threading.Lock()

    def write(self, who, text):
        with self.lock:
            self.f.write("%8.2f %-8s %s\n" % (time.time() - self.t0, who, text))
            self.f.flush()

    def close(self):
        self.f.close()


class Client(object):
    def __init__(self, user, password, transcript, cliloc, label=None):
        self.user, self.password = user, password
        self.tr, self.cliloc = transcript, cliloc
        self.label = label or user
        self.sock = None
        self.compressed = False
        self.decoder = None
        self.buf = b""
        self.lock = threading.RLock()
        self.cv = threading.Condition(self.lock)
        self.closed = None
        self.reset_world()
        self.reader = None
        self.problems = []
        self.seen_ids = {}

    def reset_world(self):
        self.messages = []          # dicts: t, kind, serial, name, text, cliloc
        self.gumps = []             # parsed 0xDD, newest last
        self.target = None          # (targetId, type)
        self.items = {}             # serial -> dict
        self.mobiles = {}           # serial -> dict
        self.serial = None
        self.pos = None             # (x, y, z, dir)
        self.body = None
        self.status = {}
        self.skills = {}
        self.chars = []
        self.cities = []
        self.logged_in = False
        self.seq = 0
        self.move_result = None
        self.last_dir = None
        self.containers = []        # opened container serials (0x24)
        self.menus = []             # cc-P223: parsed 0xBF 0x14 context menus, newest last
        self.mobile_flags = {}      # cc-P223: serial -> the last status flags byte (0x77 / 0x78)
        self.flag_log = []          # cc-P223: (time, serial, flags) at each change
        self.effects = []           # cc-P223: graphical effects (0x70, 0xC0)
        self.popups = []
        self.delete_results = []

    # -- transport --------------------------------------------------------------------------------------------
    def log(self, text):
        self.tr.write(self.label, text)

    def _connect(self, host, port):
        check_target(host, port)
        s = socket.create_connection((host, port), timeout=10)
        s.settimeout(None)
        return s

    def send(self, data):
        self.seen_ids.setdefault("sent:0x%02X" % data[0], 0)
        self.seen_ids["sent:0x%02X" % data[0]] += 1
        self.sock.sendall(data)

    def _recv_raw(self, n, timeout):
        self.sock.settimeout(timeout)
        try:
            data = self.sock.recv(n)
        finally:
            self.sock.settimeout(None)
        if not data:
            raise ConnectionError("the server closed the connection")
        return data

    def _read_plain_packet(self, timeout=10):
        """The login connection (uncompressed): one packet, split by the table."""
        deadline = time.time() + timeout
        while True:
            if self.buf:
                pid = self.buf[0]
                n = P.FIXED.get(pid)
                if n is None and len(self.buf) >= 3:
                    n = P.u16(self.buf, 1)
                if n and len(self.buf) >= n:
                    p, self.buf = self.buf[:n], self.buf[n:]
                    return p
            left = deadline - time.time()
            if left <= 0:
                raise TimeoutError("no packet from the login server")
            self.buf += self._recv_raw(65536, left)

    def _reader(self):
        try:
            while True:
                data = self.sock.recv(65536)
                if not data:
                    raise ConnectionError("the server closed the connection")
                for chunk in self.decoder.feed(data):
                    pkts, probs = P.split_chunk(chunk)
                    for pr in probs:
                        self.problems.append(pr)
                        self.log("FRAMING " + pr)
                    for p in pkts:
                        with self.cv:
                            try:
                                self._handle(p)
                            except Exception as e:  # a parse bug must not kill the connection
                                self.problems.append("0x%02X parse: %s" % (p[0], e))
                                self.log("PARSE-ERROR 0x%02X %s: %s" % (p[0], e, p[:64].hex()))
                            self.cv.notify_all()
        except Exception as e:
            with self.cv:
                if self.closed is None:
                    self.closed = str(e)
                    self.log("DISCONNECTED: %s" % e)
                self.cv.notify_all()

    def close(self):
        with self.cv:
            if self.closed is None:
                self.closed = "closed by the bot"
        try:
            self.sock.close()
        except Exception:
            pass

    def wait(self, pred, timeout=10.0):
        """Waits until pred() is true; returns its value, or None on timeout or disconnect."""
        end = time.time() + timeout
        with self.cv:
            while True:
                v = pred()
                if v:
                    return v
                if self.closed is not None:
                    return None
                left = end - time.time()
                if left <= 0:
                    return None
                self.cv.wait(min(left, 0.5))

    def sleep(self, seconds):
        self.wait(lambda: False, seconds)

    # -- login ------------------------------------------------------------------------------------------------
    def login(self):
        """Seed, 0x80, server list, 0xA0, relay, then the game connection with 0x91. Returns the character list."""
        self.sock = self._connect(HOST, PORT)
        seed_value = random.randint(1, 0x7FFFFFFF)
        self.send(P.seed(seed_value))
        self.send(P.account_login(self.user, self.password))
        self.log("-> 0xEF seed, client %s; 0x80 login as %s" % (P.CLIENT_VERSION_TEXT, self.user))
        p = self._read_plain_packet()
        if p[0] == 0x82:
            raise LoginFailed("login refused, reason %d" % p[1])
        if p[0] != 0xA8:
            raise LoginFailed("expected the server list (0xA8), got 0x%02X" % p[0])
        count = P.u16(p, 4)
        servers = []
        o = 6
        for _ in range(count):
            servers.append((P.u16(p, o), P.read_latin1(p, o + 2, 32)))
            o += 40
        self.log("<- 0xA8 servers %s" % servers)
        if not servers:
            raise LoginFailed("empty server list")
        self.send(P.play_server(servers[0][0]))
        p = self._read_plain_packet()
        if p[0] != 0x8C:
            raise LoginFailed("expected the relay (0x8C), got 0x%02X" % p[0])
        ip, port, auth = p[1:5], P.u16(p, 5), P.i32(p, 7)
        try:
            host = check_relay(ip, port)
        except Exception as e:
            self.log("REFUSED RELAY: %s" % e)
            self.sock.close()
            raise
        self.log("<- 0x8C relay %s:%d (checked: the scratch shard)" % (host, port))
        self.sock.close()
        self.buf = b""
        self.sock = self._connect(host, port)
        self.send(struct.pack(">i", auth))
        self.send(P.game_login(auth, self.user, self.password))
        self.compressed = True
        self.decoder = huffman.Decoder()
        self.closed = None
        self.reader = threading.Thread(target=self._reader, daemon=True)
        self.reader.start()
        if not self.wait(lambda: self.cities or self.chars, 15):
            raise LoginFailed("no character list after the game login (%s)" % (self.closed or "timeout"))
        return self.chars

    def create(self, name, race="human", female=False, stats=(60, 10, 10), skills=((0, 50), (1, 50), (2, 0), (3, 0)),
               city=0, skin=0x83EA, hair=0x203B, hair_hue=0x044E, beard=0, beard_hue=0, shirt=0x0101, pants=0x0101):
        """0xF8 with the real creation fields. race: human, elf or gargoyle (SA codes, ACC:142-150)."""
        base = {"human": 2, "elf": 4, "gargoyle": 6}[race]
        gr = base + (1 if female else 0)
        self.log("-> 0xF8 create %r %s %s city %d" % (name, race, "female" if female else "male", city))
        self.send(P.create_character(name, gr, stats, skills, skin, hair, hair_hue, beard, beard_hue, city, shirt, pants))
        return self._after_enter()

    def play(self, slot):
        name = self.chars[slot] if slot < len(self.chars) else ""
        self.log("-> 0x5D play slot %d (%s)" % (slot, name))
        self.send(P.play_character(name, slot))
        return self._after_enter()

    def _after_enter(self):
        ok = self.wait(lambda: self.logged_in, 20)
        if not ok:
            raise LoginFailed("not in the world (%s)" % (self.closed or "no 0x55 login complete"))
        self.send(P.status_request(self.serial, 4))
        self.send(P.status_request(self.serial, 5))
        self.sleep(1.0)
        return True

    def delete_character(self, slot):
        self.send(P.delete_character(slot))

    # -- actions ----------------------------------------------------------------------------------------------
    def say(self, text):
        self.log("-> say %r" % text)
        self.send(P.unicode_speech(text))

    def command(self, text, settle=0.8):
        """A staff command, typed as Chase would: speech starting with the command prefix '['."""
        n = len(self.messages)
        self.say(text)
        self.sleep(settle)
        return self.messages[n:]

    def dclick(self, serial):
        self.log("-> 0x06 double click 0x%08X" % serial)
        self.send(P.double_click(serial))

    def click(self, serial):
        self.log("-> 0x09 single click 0x%08X" % serial)
        self.send(P.single_click(serial))

    def wait_target(self, timeout=8):
        return self.wait(lambda: self.target, timeout)

    def target_obj(self, serial):
        t = self.target
        if not t:
            raise RuntimeError("no target cursor")
        self.target = None
        self.log("-> 0x6C target object 0x%08X" % serial)
        self.send(P.target_object(t[0], serial))

    def target_xyz(self, x, y, z, graphic=0):
        t = self.target
        if not t:
            raise RuntimeError("no target cursor")
        self.target = None
        self.log("-> 0x6C target ground %d,%d,%d" % (x, y, z))
        self.send(P.target_ground(t[0], x, y, z, graphic))

    def target_self(self):
        self.target_obj(self.serial)

    def cancel_target(self):
        t = self.target
        if t:
            self.target = None
            self.send(P.target_cancel(t[0]))

    def wait_gump(self, pred=None, timeout=8, since=None):
        start = len(self.gumps) if since is None else since
        def f():
            for g in self.gumps[start:]:
                if pred is None or pred(g):
                    return g
            return None
        return self.wait(f, timeout)

    def reply_gump(self, gump, button, switches=(), texts=()):
        self.log("-> 0xB1 gump 0x%08X type %d button %d switches %s texts %s" % (gump["serial"], gump["typeId"], button, list(switches), list(texts)))
        self.send(P.gump_response(gump["serial"], gump["typeId"], button, switches, texts))

    def walk(self, direction, steps=1, run=False, pause=0.25):
        """Walks; returns how many steps the server accepted."""
        d = DIRS[direction] if isinstance(direction, str) else direction
        ok = 0
        for _ in range(steps):
            with self.cv:
                self.move_result = None
                self.last_dir = d
                seq = self.seq
            self.send(P.move(d, seq, run))
            r = self.wait(lambda: self.move_result, 3)
            if r != "ack":
                break
            ok += 1
            self.sleep(pause)
        return ok

    def keepalive(self):
        try:
            self.send(P.ping(0))
        except Exception:
            pass

    def lift_drop(self, serial, amount, container, x=50, y=50):
        self.send(P.lift(serial, amount))
        self.sleep(0.4)
        self.send(P.drop(serial, x, y, 0, container))

    # -- queries ----------------------------------------------------------------------------------------------
    def texts_since(self, n):
        return [m["text"] for m in self.messages[n:]]

    def find_text(self, pattern, since=0, timeout=0):
        rx = re.compile(pattern, re.I)
        def f():
            for m in self.messages[since:]:
                if rx.search(m["text"]):
                    return m
            return None
        return self.wait(f, timeout) if timeout else f()

    def backpack(self):
        for s, it in self.items.items():
            if it.get("layer") == 0x15 and it.get("parent") == self.serial:
                return s
        return None

    def contents(self, container):
        return {s: it for s, it in self.items.items() if it.get("container") == container}

    def gump_text(self, g):
        """Every string a gump shows: its lines, and the cliloc numbers its layout names, resolved."""
        out = list(g["lines"])
        for m in re.finditer(r"\{\s*(?:xmfhtmlgump|xmfhtmlgumpcolor|xmfhtmltok|tooltip)\s+([^}]*)\}", g["layout"]):
            nums = [int(x) for x in re.findall(r"-?\d+", m.group(1))]
            cl = [n for n in nums if n >= 500000]
            args = ""
            am = re.search(r"@([^@]*)@", m.group(1))
            if am:
                args = am.group(1)
            for n in cl[:1]:
                out.append(self.cliloc.text(n, args))
        return out

    def gump_buttons(self, g):
        """(x, y, id) of each 'button x y up down type page id' in the layout."""
        return [tuple(int(v) for v in (m.group(1), m.group(2), m.group(3)))
                for m in re.finditer(r"\{\s*button\s+(-?\d+)\s+(-?\d+)\s+\d+\s+\d+\s+\d+\s+\d+\s+(-?\d+)\s*\}", g["layout"])]

    # -- the server's packets ---------------------------------------------------------------------------------
    # -- cc-P223: skills by text command, context menus, gump rows -----------------------------------------------
    def use_skill(self, skill_index):
        self.log("-> 0x12 use skill %d" % skill_index)
        self.send(P.use_skill(skill_index))

    def context_menu(self, serial, timeout=6):
        """Ask for an entity's context menu (0xBF 0x13) and wait for the 0xBF 0x14 answer. Returns the menu dict
        ({serial, entries: [{index, number, flags, text}]}) or None."""
        n = len(self.menus)
        self.log("-> 0xBF 0x13 context menu 0x%08X" % serial)
        self.send(P.context_menu_request(serial))
        return self.wait(lambda: next((m for m in self.menus[n:] if m["serial"] == serial), None), timeout)

    def menu_click(self, serial, index):
        self.log("-> 0xBF 0x15 context menu 0x%08X entry %d" % (serial, index))
        self.send(P.context_menu_select(serial, index))

    def menu_pick(self, serial, text_or_number, timeout=6):
        """Open the entity's menu and click the entry whose cliloc number, or resolved text (a substring, case
        folded), matches. Returns the entry, or None when the menu or the entry is missing."""
        m = self.context_menu(serial, timeout)
        if not m:
            return None
        for e in m["entries"]:
            if (isinstance(text_or_number, int) and e["number"] == text_or_number) or                (isinstance(text_or_number, str) and text_or_number.lower() in (e.get("text") or "").lower()):
                self.menu_click(serial, e["index"])
                return e
        return None

    def gump_rows(self, g):
        """The gump's layout as rows: {y: {"texts": [str], "buttons": [id], "x": [...]}} with every text-bearing
        command resolved (text, croppedtext, htmlgump by line index; xmfhtmlgump, xmfhtmlgumpcolor, xmfhtmltok by
        cliloc) and every reply button. Rows are keyed by the command's y; texts sit 3 px below their button in
        pinned's craft gumps, so callers match with a tolerance (see row_at)."""
        rows = {}
        page = [0]
        def add(y, kind, x, val):
            r = rows.setdefault(y, {"texts": [], "buttons": [], "x": [], "pages": [], "all": []})
            if kind == "text":
                r["texts"].append(val)
            else:
                r["buttons"].append(val)
                r["x"].append(x)          # x and page per button, in order (the craft walker pairs them)
                r["pages"].append(page[0])
            r["all"].append((kind, val, x, page[0]))
        for m in re.finditer(r"\{\s*([a-z]+)\s+([^}]*)\}", g["layout"]):
            kind, args = m.group(1), m.group(2).split()
            try:
                if kind == "page" and args:
                    page[0] = int(args[0])
                    continue
                if kind in ("text", "croppedtext"):
                    idx = int(args[3]) if kind == "text" else int(args[5])
                    add(int(args[1]), "text", int(args[0]), g["lines"][idx])
                elif kind == "htmlgump":
                    add(int(args[1]), "text", int(args[0]), g["lines"][int(args[4])])
                elif kind in ("xmfhtmlgump", "xmfhtmlgumpcolor", "xmfhtmltok"):
                    nums = [int(v) for v in args if re.match(r"^-?\d+$", v)]
                    cl = next((v for v in nums[4:] if v >= 500000), None)
                    am = re.search(r"@([^@]*)@", m.group(2))
                    if cl is not None:
                        add(int(args[1]), "text", int(args[0]), self.cliloc.text(cl, am.group(1) if am else ""))
                elif kind == "button" and len(args) >= 7 and int(args[4]) == 1:
                    add(int(args[1]), "button", int(args[0]), int(args[6]))
            except (ValueError, IndexError):
                pass
        return rows

    def row_at(self, rows, y, tol=4, xmin=None, xmax=None, page=None):
        """The texts and buttons within tol px of y, merged; with xmin/xmax only the commands in that x range (the
        craft gump's group list at x 15-200 and its item list at x 220-500 share y values), and with page only the
        commands on that gump page (page 0 is what every page shows)."""
        out = {"texts": [], "buttons": []}
        for ry, r in rows.items():
            if abs(ry - y) > tol:
                continue
            for kind, val, x, pg in r.get("all", []):
                if (xmin is not None and x < xmin) or (xmax is not None and x > xmax):
                    continue
                if page is not None and pg not in (0, page):
                    continue
                out[kind + "s"].append(val)
        return out

    def _msg(self, kind, serial, name, text, cliloc=None):
        self.messages.append({"t": time.time(), "kind": kind, "serial": serial, "name": name, "text": text, "cliloc": cliloc})
        self.log("<- %s %s%s: %s" % (kind, name or "", (" #%d" % cliloc) if cliloc else "", text))

    def _handle(self, p):
        pid = p[0]
        self.seen_ids["recv:0x%02X" % pid] = self.seen_ids.get("recv:0x%02X" % pid, 0) + 1
        if pid in (0x77, 0x78, 0x70, 0xC0, 0xA1):
            self._p223_flags_effects(pid, p)
        if pid == 0xA9:
            # S/OutgoingAccountPackets.cs:293: byte slots; per slot name[30]+30; byte cities; per city (new format)
            # idx, city[32], building[32], int32 x, y, z, map, cliloc, 0; int32 flags; int16 -1.
            n = p[3]
            o = 4
            self.chars = []
            for _ in range(n):
                self.chars.append(P.read_latin1(p, o, 30))
                o += 60
            nc = p[o]
            o += 1
            self.cities = []
            for _ in range(nc):
                self.cities.append((p[o], P.read_latin1(p, o + 1, 32), P.read_latin1(p, o + 33, 32)))
                o += 89
            self.log("<- 0xA9 characters %s; %d cities %s" % ([c for c in self.chars], nc, [c[1] for c in self.cities]))
        elif pid == 0x86:
            n = p[3]
            self.chars = [P.read_latin1(p, 4 + i * 60, 30) for i in range(n)]
            self.log("<- 0x86 characters now %s" % self.chars)
        elif pid == 0x85:
            self.delete_results.append(p[1])
            self.log("<- 0x85 delete result %d" % p[1])
        elif pid == 0x53:
            self.popups.append(p[1])
            self.log("<- 0x53 popup message %d" % p[1])
        elif pid == 0x82:
            self.log("<- 0x82 login rejected %d" % p[1])
        elif pid == 0x1B:
            # S/OutgoingAccountPackets.cs:190: serial; int32 0; short body; short x, y, z; byte dir; ...
            self.serial = P.u32(p, 1)
            self.body = P.u16(p, 9)
            self.pos = (P.i16(p, 11), P.i16(p, 13), P.i16(p, 15), p[17])
            self.log("<- 0x1B login confirm serial 0x%08X body 0x%X at %s" % (self.serial, self.body, self.pos[:3]))
        elif pid == 0x55:
            self.logged_in = True
            self.log("<- 0x55 login complete")
        elif pid == 0xBD:
            self.send(P.client_version())
        elif pid in (0x1C, 0xAE, 0xC1, 0xCC):
            # S/OutgoingMessagePackets.cs:189 (1C, AE), :60 (C1), :120 (CC). Header: u16 len; serial; short graphic;
            # byte type; short hue; short font.
            serial = P.u32(p, 3)
            mtype = p[9]
            if pid == 0x1C:
                name = P.read_latin1(p, 14, 30)
                text, _ = P.read_cstr(p, 44)
                self._msg("ascii(%d)" % mtype, serial, name, text)
            elif pid == 0xAE:
                name = P.read_latin1(p, 18, 30)
                text, _ = P.read_bigu_null(p, 48)
                self._msg("unicode(%d)" % mtype, serial, name, text)
            elif pid == 0xC1:
                num = P.i32(p, 14)
                name = P.read_latin1(p, 18, 30)
                args, _ = P.read_littleu_null(p, 48)
                self._msg("cliloc(%d)" % mtype, serial, name, self.cliloc.text(num, args), num)
            else:
                num = P.i32(p, 14)
                affix_type = p[18]
                name = P.read_latin1(p, 19, 30)
                affix, o = P.read_cstr(p, 49)
                args, _ = P.read_bigu_null(p, o)
                base = self.cliloc.text(num, args)
                text = (affix + base) if affix_type & 1 else (base + affix)
                self._msg("affix(%d)" % mtype, serial, name, text, num)
        elif pid == 0xDD:
            g = P.parse_gump_dd(p)
            self.gumps.append(g)
            self.log("<- 0xDD gump 0x%08X type %d: %s" % (g["serial"], g["typeId"], " | ".join(self.gump_text(g))[:4000]))
            self.log("   layout: %s" % g["layout"][:4000])
        elif pid == 0xBF and len(p) >= 12 and P.u16(p, 3) == 0x14:
            m = P.parse_context_menu(p)
            for e in m["entries"]:
                e["text"] = self.cliloc.text(e["number"]) if e["number"] else ""
            self.menus.append(m)
            self.log("<- 0xBF 0x14 context menu 0x%08X: %s" % (m["serial"], ["%d:%s(%d)%s" % (e["index"], e["text"], e["number"], " disabled" if e["flags"] & 1 else "") for e in m["entries"]]))
        elif pid == 0x6C:
            self.target = (P.i32(p, 2), p[1])
            self.log("<- 0x6C target cursor %d (type %d)" % self.target)
        elif pid == 0x11:
            self._status(p)
        elif pid == 0x3A:
            self._skills(p)
        elif pid == 0x3C:
            n = P.u16(p, 3)
            o = 5
            for _ in range(n):
                s = P.u32(p, o)
                self.items[s] = {"graphic": P.u16(p, o + 4), "amount": P.u16(p, o + 7), "x": P.i16(p, o + 9),
                                 "y": P.i16(p, o + 11), "container": P.u32(p, o + 14), "hue": P.u16(p, o + 18)}
                o += 20
            self.log("<- 0x3C %d items into containers" % n)
        elif pid == 0x25:
            s = P.u32(p, 1)
            self.items[s] = {"graphic": P.u16(p, 5), "amount": P.u16(p, 8), "x": P.i16(p, 10), "y": P.i16(p, 12),
                             "container": P.u32(p, 15), "hue": P.u16(p, 19)}
            self.log("<- 0x25 item 0x%08X graphic 0x%X x%d into 0x%08X" % (s, self.items[s]["graphic"], self.items[s]["amount"], self.items[s]["container"]))
        elif pid == 0x24:
            self.containers.append(P.u32(p, 1))
            self.log("<- 0x24 open container 0x%08X gump 0x%X" % (P.u32(p, 1), P.u16(p, 5)))
        elif pid == 0xF3:
            # S/OutgoingEntityPackets.cs:88: short 1; byte type; serial; u16 graphic; byte 0; short amount x2;
            # short x; short y; sbyte z; byte light; short hue; byte flags; short 0.
            typ, s = p[3], P.u32(p, 4)
            d = {"graphic": P.u16(p, 8), "amount": P.u16(p, 11), "x": P.u16(p, 15) & 0x7FFF, "y": P.u16(p, 17) & 0x3FFF,
                 "z": struct.unpack_from(">b", p, 19)[0], "hue": P.u16(p, 21), "container": 0, "world": True, "kind": typ}
            self.items[s] = d
        elif pid == 0x2E:
            s = P.u32(p, 1)
            self.items[s] = {"graphic": P.u16(p, 5), "layer": p[8], "parent": P.u32(p, 9), "hue": P.u16(p, 13),
                             "container": 0}
        elif pid == 0x1D:
            s = P.u32(p, 1)
            self.items.pop(s, None)
            self.mobiles.pop(s, None)
        elif pid == 0x78:
            # S/OutgoingMobilePackets.cs:601: serial; body; x; y; sbyte z; dir; hue; flags; notoriety; then
            # equipment (serial, u16 itemId, byte layer, short hue) until int32 0.
            s = P.u32(p, 3)
            m = {"body": P.u16(p, 7), "x": P.i16(p, 9), "y": P.i16(p, 11), "z": struct.unpack_from(">b", p, 13)[0],
                 "hue": P.u16(p, 15), "notoriety": p[18]}
            o = 19
            while o + 4 <= len(p):
                es = P.u32(p, o)
                if es == 0:
                    break
                self.items[es] = {"graphic": P.u16(p, o + 4), "layer": p[o + 6], "hue": P.u16(p, o + 7), "parent": s, "container": 0}
                o += 9
            self.mobiles[s] = m
            if s == self.serial:
                self.body = m["body"]
        elif pid == 0x20:
            s = P.u32(p, 1)
            self.body = P.u16(p, 5)
            self.pos = (P.i16(p, 11), P.i16(p, 13), struct.unpack_from(">b", p, 18)[0], p[17])
        elif pid == 0x77:
            s = P.u32(p, 1)
            self.mobiles.setdefault(s, {}).update({"body": P.u16(p, 5), "x": P.i16(p, 7), "y": P.i16(p, 9),
                                                   "z": struct.unpack_from(">b", p, 11)[0], "notoriety": p[16]})
        elif pid == 0x22:
            # MovementThrottle.cs:264-269: after an accepted move the sequence is seq+1, 255 wrapping to 1. A step in a
            # new direction only turns the mobile; a step in the facing direction moves it one tile.
            self.seq = 1 if self.seq == 255 else self.seq + 1
            if self.pos and self.last_dir is not None:
                x, y, z, d = self.pos
                if (d & 7) == self.last_dir:
                    dx, dy = DELTA[self.last_dir]
                    x, y = x + dx, y + dy
                self.pos = (x, y, z, self.last_dir)
            self.move_result = "ack"
        elif pid == 0x21:
            self.pos = (P.i16(p, 2), P.i16(p, 4), struct.unpack_from(">b", p, 7)[0], p[6])
            self.seq = 0
            self.move_result = "reject"
            self.log("<- 0x21 move rejected; at %s" % (self.pos[:3],))
        elif pid == 0xD6:
            s = P.u32(p, 5)
            o = 15
            props = []
            while o + 6 <= len(p):
                num = P.i32(p, o)
                if num == 0:
                    break
                ln = P.u16(p, o + 4)
                args = p[o + 6:o + 6 + ln].decode("utf-16-le", "replace")
                props.append(self.cliloc.text(num, args))
                o += 6 + ln
            tgt = self.items.get(s) or self.mobiles.get(s)
            if tgt is not None:
                tgt["props"] = props
            else:
                self.items[s] = {"props": props, "container": None}
            self.log("<- 0xD6 properties 0x%08X: %s" % (s, " | ".join(props)))

    def _p223_flags_effects(self, pid, p):
        """cc-P223: a mobile's status flags (0x77 at 15, 0x78 at 17; bit 0x01 frozen/paralysed, pinned
        Server/Mobiles/Mobile.cs:7067-7074) with each change logged, and graphical effects (0x70, 0xC0: byte type,
        u32 source, u32 target, u16 item id; S/OutgoingEffectPackets.cs)."""
        if pid == 0xA1:
            # S/OutgoingMobilePackets.cs (MobileHits): u32 serial; u16 hitsMax; u16 hits. One's own are exact; a fight
            # sends these, not 0x11, so without them the bot's own hits stood still (P67-6, P66-5, 10-21).
            if len(p) >= 9:
                s = P.u32(p, 1)
                if s == self.serial and self.status:
                    self.status["hitsMax"], self.status["hits"] = P.u16(p, 5), P.u16(p, 7)
                else:
                    self.mobiles.setdefault(s, {}).update(hitsMax=P.u16(p, 5), hits=P.u16(p, 7))
            return
        if pid in (0x77, 0x78):
            o = 15 if pid == 0x77 else 17
            if len(p) > o:
                s = P.u32(p, 1 if pid == 0x77 else 3)
                f = p[o]
                if pid == 0x77 and s == self.serial:
                    # A resurrection arrives as 0x77 for oneself; the ghost body would otherwise stay (T-024, 10-21).
                    self.body = P.u16(p, 5)
                old = self.mobile_flags.get(s)
                self.mobile_flags[s] = f
                if old != f:
                    self.flag_log.append((time.time(), s, f))
        elif len(p) >= 12:
            self.effects.append({"t": time.time(), "type": p[1], "source": P.u32(p, 2), "target": P.u32(p, 6), "item": P.u16(p, 10)})

    def _status(self, p):
        # S/OutgoingMobilePackets.cs:497: u16 len; serial; name[30]; short hits; short hitsMax; bool rename; byte
        # version; >=3: bool female, short str, dex, int, stam, stamMax, mana, manaMax, int32 gold, short AR,
        # short weight; >=5: short maxWeight, byte race; short statCap; byte followers, followersMax; >=4: ...
        s = P.u32(p, 3)
        st = {"serial": s, "name": P.read_latin1(p, 7, 30), "hits": P.i16(p, 37), "hitsMax": P.i16(p, 39), "version": p[42]}
        if st["version"] >= 3 and len(p) >= 66:
            st.update(female=p[43], str=P.i16(p, 44), dex=P.i16(p, 46), int=P.i16(p, 48), stam=P.i16(p, 50),
                      stamMax=P.i16(p, 52), mana=P.i16(p, 54), manaMax=P.i16(p, 56), gold=P.i32(p, 58),
                      ar=P.i16(p, 62), weight=P.i16(p, 64))
        if st["version"] >= 5 and len(p) >= 69:
            st.update(maxWeight=P.i16(p, 66), race=p[68])
            o = 69
            st.update(statCap=P.i16(p, o), followers=p[o + 2], followersMax=p[o + 3])
        if s == self.serial:
            self.status = st
            self.log("<- 0x11 status %s" % {k: v for k, v in st.items() if k != "serial"})
        else:
            self.mobiles.setdefault(s, {}).update(name=st["name"], hits=st["hits"], hitsMax=st["hitsMax"])

    def _skills(self, p):
        # S/OutgoingPlayerPackets.cs:119 full list (type 0x02): per skill u16 id+1, u16 value*10, u16 base, byte lock,
        # u16 cap; ends u16 0. :154 single (0xDF): u16 id (0-based), value, base, lock, cap.
        typ = p[3]
        if typ == 0xDF:
            sid = P.u16(p, 4)
            self.skills[sid] = {"value": P.u16(p, 6) / 10.0, "base": P.u16(p, 8) / 10.0, "lock": p[10], "cap": P.u16(p, 11) / 10.0}
            self.log("<- 0x3A skill %d now %.1f (cap %.1f)" % (sid, self.skills[sid]["value"], self.skills[sid]["cap"]))
            return
        o = 4
        while o + 2 <= len(p):
            sid = P.u16(p, o)
            if sid == 0:
                break
            self.skills[sid - 1] = {"value": P.u16(p, o + 2) / 10.0, "base": P.u16(p, o + 4) / 10.0, "lock": p[o + 6],
                                    "cap": P.u16(p, o + 7) / 10.0}
            o += 9
        self.log("<- 0x3A %d skills" % len(self.skills))
