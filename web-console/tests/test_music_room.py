"""cc-P367: the Music Room (web-console/music.py and its routes in server.py). Run from the repo root:

    python -m unittest discover -s web-console/tests

The page lists all four packs and the stock client's music (from small made-up mp3s in the same folder shape, and,
where this PC has them, from the real folders in D:/UO/third-party with their real counts); a Range request returns
partial content (206) and the bytes asked for; a note round-trips to docs/music/music-notes.json and into the summary;
the page, its tracks and its POSTs refuse anyone but Chase; Save to repo commits that one file alone, and never while
the docs repo is mid-merge or a Golem holds the commit lock; and, in Edge, a note typed on the page lands in the file."""
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.dirname(HERE)
REPO = os.path.dirname(WEB)
sys.path.insert(0, WEB)
sys.path.insert(0, HERE)
import music  # noqa: E402
import panels  # noqa: E402
import server  # noqa: E402
from test_actions import CHASE, EDGE, PROOF, free_port  # noqa: E402

ACT = dict(CHASE, **{"X-UOConsole-Action": "1"})
FRAME = b"\xff\xfb\x90\x64"   # MPEG-1 layer III, 128 kbps, 44.1 kHz, no padding: 417 bytes a frame, 1152 samples


def id3(title, album):
    def frame(fid, text):
        body = b"\x00" + text.encode("latin-1")
        return fid + struct.pack(">I", len(body)) + b"\x00\x00" + body
    body = frame(b"TIT2", title) + frame(b"TALB", album)
    n = len(body)
    return b"ID3\x03\x00\x00" + bytes([(n >> 21) & 127, (n >> 14) & 127, (n >> 7) & 127, n & 127]) + body


def mp3(path, frames, title="", album=""):
    """A constant-bit-rate mp3 of silence-shaped frames: frames * 1152 / 44100 seconds long."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        if title or album:
            f.write(id3(title, album))
        for i in range(frames):
            f.write(FRAME + bytes([i % 251]) * 413)


def make_library(root):
    """The four packs in their real folder shapes, and a stock client folder, with a few tracks each."""
    tp = os.path.join(root, "third-party")
    d = {p[0]: os.path.join(tp, p[2]) for p in music.PACKS}
    mp3(os.path.join(d["sos-vol2"], "Turfin.mp3"), 400)
    mp3(os.path.join(d["sos-vol2"], "Britain1.mp3"), 200, "Britain", "Ultima Online")
    with open(os.path.join(d["sos-vol2"], "Config.txt"), "w", encoding="ascii") as f:
        f.write("0 turfin,loop\n10 britain1,loop\n41 turfin,loop\n")
    mp3(os.path.join(d["sos-dlc"], "Britain_Sewers.mp3"), 300, "Sewers", "Diablo 2 OST")
    mp3(os.path.join(d["sos-dlc"], "Turfin.mp3"), 100)
    mp3(os.path.join(d["paranoia"], "OLDULT01.mp3"), 150)
    mp3(os.path.join(d["paranoia"], "BRITAIN1.mp3"), 150)
    with open(os.path.join(d["paranoia"], "SOURCE.txt"), "w", encoding="ascii") as f:
        f.write("not a track\n")
    mp3(os.path.join(d["turfin"], "Turfin.mp3"), 120)
    stock = os.path.join(root, "client", "Music", "Digital")
    mp3(os.path.join(stock, "Turfin.mp3"), 90)
    mp3(os.path.join(stock, "Britain1.mp3"), 90)
    with open(os.path.join(stock, "Config.txt"), "w", encoding="ascii") as f:
        f.write("0 turfin,loop\n10 britain1\n")
    regions = os.path.join(root, "regions.json")
    with open(regions, "w", encoding="utf-8") as f:
        json.dump([{"Name": "Britain"}, {"Name": "A Wheatfield in Britain 1"}, {"Name": "Despise"}, {"Map": "Felucca"}], f)
    lib = os.path.join(tp, "ART-LIBRARY.md")
    with open(lib, "w", encoding="utf-8") as f:
        f.write("# x\n\n### Boss shortlist\nGloom Queen and Burrow Mother are New Haven's; candidates for others: `beholder`,\n"
                "`otyugh` (sewer, comic), `orc-lord`. Systems queued: dungeon boss system Q#442.\n\n## next\n")
    return tp, stock, regions, lib


def git(repo, *a):
    r = subprocess.run(["git", "-C", repo] + list(a), capture_output=True, text=True)
    if r.returncode:
        raise AssertionError("git %s: %s" % (" ".join(a), r.stderr))
    return r.stdout.strip()


class MusicRoom(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="p367-music-")
        with open(os.path.join(cls.tmp, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"]}, f)
        with open(os.path.join(cls.tmp, "proof.key"), "w", encoding="ascii") as f:
            f.write(PROOF + "\n")
        cls.tp, cls.stock, regions, lib = make_library(cls.tmp)
        # The docs repo: a git repo on main with one commit, as D:/UO/shard-migration is.
        cls.docs = os.path.join(cls.tmp, "docs-repo")
        os.makedirs(cls.docs)
        git(cls.docs, "init", "-q", "-b", "main")
        git(cls.docs, "config", "user.email", "test@example.com")
        git(cls.docs, "config", "user.name", "P367 test")
        with open(os.path.join(cls.docs, "README.md"), "w", encoding="utf-8") as f:
            f.write("docs\n")
        git(cls.docs, "add", "README.md")
        git(cls.docs, "commit", "-q", "-m", "start")
        cls.locks = os.path.join(cls.tmp, "golem", "locks")
        os.makedirs(cls.locks)
        cls.console = server.Console(REPO, cls.docs, cls.tmp, os.path.join(cls.tmp, "status.json"), collector=False,
                                     golem_root=os.path.join(cls.tmp, "golem"))
        cls.console.music = music.MusicRoom(cls.docs, third_party=cls.tp, stock_dirs=[cls.stock], regions_json=regions,
                                            art_library=lib, code_repo=REPO, lock_dir=cls.locks,
                                            cache_file=os.path.join(cls.tmp, "music-index.json"))
        cls.local_port, cls.tail_port = free_port(), free_port()
        cls.servers = []
        for listener, port in ((panels.LOCAL, cls.local_port), (panels.TAILNET, cls.tail_port)):
            s = server.ThreadingHTTPServer(("127.0.0.1", port), server.make_handler(cls.console, listener))
            threading.Thread(target=s.serve_forever, daemon=True).start()
            cls.servers.append(s)

    @classmethod
    def tearDownClass(cls):
        for s in cls.servers:
            s.shutdown()
            s.server_close()
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def req(self, path, body=None, headers=None, port=None, method=None):
        data = None if body is None else (body if isinstance(body, bytes) else json.dumps(body).encode("utf-8"))
        h = dict(CHASE if headers is None else headers)
        if data is not None:
            h.setdefault("Content-Type", "application/json")
        r = urllib.request.Request("http://127.0.0.1:%d%s" % (port or self.tail_port, path), data=data, headers=h,
                                   method=method or ("POST" if data is not None else "GET"))
        try:
            with urllib.request.urlopen(r, timeout=30) as x:
                raw, hd = x.read(), dict(x.headers)
                code = x.status
        except urllib.error.HTTPError as e:
            raw, hd, code = e.read(), dict(e.headers), e.code
        if (hd.get("Content-Type") or "").startswith("application/json"):
            return code, json.loads(raw.decode("utf-8")), hd
        return code, raw, hd

    def note(self, key, **fields):
        """POST one track's record. Without a rev, the one the page would hold: what GET /api/music reports now."""
        if "rev" not in fields:
            fields["rev"] = (self.req("/api/music")[1]["notes"].get(key) or {}).get("rev", 0)
        return self.req("/api/music/note", dict({"key": key}, **fields), ACT)

    def notes_path(self):
        return os.path.join(self.docs, "docs", "music", "music-notes.json")

    # --- the list ---------------------------------------------------------------------------------------------
    def test_the_page_lists_all_four_packs_and_the_stock_music(self):
        code, d, _ = self.req("/api/music")
        self.assertEqual(200, code, d)
        packs = {p["id"]: p for p in d["packs"]}
        self.assertEqual(["sos-vol2", "sos-dlc", "paranoia", "turfin", "stock"], [p["id"] for p in d["packs"]])
        self.assertEqual({"sos-vol2": 2, "sos-dlc": 2, "paranoia": 2, "turfin": 1, "stock": 2}, {k: v["count"] for k, v in packs.items()})
        t = {x["key"]: x for x in d["tracks"]}
        self.assertNotIn("paranoia/SOURCE.txt", t, "only .mp3 files are tracks")
        self.assertEqual(("Turfin", "replaces"), (t["sos-vol2/Turfin.mp3"]["remixOf"], t["sos-vol2/Turfin.mp3"]["remixKind"]))
        self.assertEqual("new", t["sos-dlc/Britain_Sewers.mp3"]["remixKind"], "no stock track of that name")
        self.assertEqual(("OLDULT01", "midi"), (t["paranoia/OLDULT01.mp3"]["remixOf"], t["paranoia/OLDULT01.mp3"]["remixKind"]))
        self.assertEqual("Britain1", t["paranoia/BRITAIN1.mp3"]["remixOf"], "matched without case")
        self.assertEqual("stock", t["stock/Turfin.mp3"]["remixKind"])
        self.assertAlmostEqual(400 * 1152 / 44100, t["sos-vol2/Turfin.mp3"]["seconds"], delta=0.1)
        self.assertAlmostEqual(300 * 1152 / 44100, t["sos-dlc/Britain_Sewers.mp3"]["seconds"], delta=0.1, msg="after an ID3 tag")
        self.assertEqual(("Sewers", "Diablo 2 OST", True), (t["sos-dlc/Britain_Sewers.mp3"]["title"], t["sos-dlc/Britain_Sewers.mp3"]["album"],
                                                             t["sos-dlc/Britain_Sewers.mp3"]["notUltima"]))
        self.assertFalse(t["sos-vol2/Britain1.mp3"]["notUltima"], "an Ultima album is not flagged")
        self.assertEqual([0, 41], t["sos-vol2/Turfin.mp3"]["slots"], "the client slots its Config.txt plays it in")
        self.assertEqual(["town", "region", "dungeon", "monster", "boss", "event", "travel", "combat", "menu"], [x["id"] for x in d["tags"]])
        self.assertEqual(["Britain", "Despise"], [r for r in d["suggestions"]["regions"] if r in ("Britain", "Despise", "A Wheatfield in Britain 1")],
                         "regions without the farm fields")
        self.assertIn("the Rootdeep Burrows", d["suggestions"]["regions"], "and our own regions from server/customizations/Regions")
        self.assertEqual(["Gloom Queen", "Burrow Mother", "beholder", "otyugh", "orc-lord"], d["suggestions"]["bosses"])
        code, page, hd = self.req("/music")
        self.assertEqual(200, code)
        self.assertIn(b'src="/music.js"', page)
        self.assertEqual(200, self.req("/music.js")[0])
        self.assertIn(b'href="/music"', self.req("/")[1], "the console's nav links to it")

    @unittest.skipUnless(all(os.path.isdir(os.path.join(music.THIRD_PARTY, p[2])) for p in music.PACKS), "the real music folders are not on this PC")
    def test_the_real_folders_give_every_track(self):
        m = music.MusicRoom(os.path.join(self.tmp, "real-notes"), cache_file=os.path.join(self.tmp, "real-index.json"))
        counts = {p["id"]: p["count"] for p in m.packs_view()}
        self.assertEqual({"sos-vol2": 93, "sos-dlc": 117, "paranoia": 49, "turfin": 1}, {k: counts[k] for k in ("sos-vol2", "sos-dlc", "paranoia", "turfin")})
        self.assertEqual([], [t["key"] for t in m.tracks() if not t["seconds"]], "every track has a length (Deathtune is 7.7 s)")

    # --- streaming --------------------------------------------------------------------------------------------
    def test_a_range_request_returns_partial_content(self):
        path = os.path.join(self.tp, music.PACKS[0][2], "Turfin.mp3")
        with open(path, "rb") as f:
            whole = f.read()
        url = "/music-file/sos-vol2/Turfin.mp3"
        code, body, hd = self.req(url, headers=dict(CHASE, Range="bytes=1000-1999"))
        self.assertEqual(206, code)
        self.assertEqual(whole[1000:2000], body)
        self.assertEqual("bytes 1000-1999/%d" % len(whole), hd.get("Content-Range"))
        self.assertEqual("bytes", hd.get("Accept-Ranges"))
        self.assertEqual("audio/mpeg", hd.get("Content-Type"))
        code, body, hd = self.req(url, headers=dict(CHASE, Range="bytes=%d-" % (len(whole) - 10)))
        self.assertEqual((206, whole[-10:]), (code, body), "open-ended")
        code, body, hd = self.req(url, headers=dict(CHASE, Range="bytes=-25"))
        self.assertEqual((206, whole[-25:]), (code, body), "the last n bytes")
        code, body, hd = self.req(url, headers=dict(CHASE, Range="bytes=%d-" % len(whole)))
        self.assertEqual(416, code, "past the end")
        self.assertEqual("bytes */%d" % len(whole), hd.get("Content-Range"))
        code, body, hd = self.req(url)
        self.assertEqual((200, whole), (code, body), "no Range: the whole file")
        code, body, hd = self.req(url, method="HEAD", headers=dict(CHASE, Range="bytes=0-9"))
        self.assertEqual((206, "10"), (code, hd.get("Content-Length")))
        self.assertEqual(200, self.req("/music-file/sos-dlc/Britain_Sewers.mp3")[0])
        self.assertEqual(200, self.req("/music-file/stock/Turfin.mp3")[0])
        for bad in ("/music-file/paranoia/SOURCE.txt", "/music-file/sos-vol2/..%2F..%2Fregions.json", "/music-file/nope/Turfin.mp3",
                    "/music-file/sos-vol2/Nope.mp3"):
            self.assertEqual(404, self.req(bad)[0], bad)

    # --- notes ------------------------------------------------------------------------------------------------
    def test_a_note_round_trips_to_music_notes_json(self):
        key = "sos-dlc/Britain_Sewers.mp3"
        text = "Under Britain: damp, echoing.\nGood for the sewer otyugh."
        code, out, _ = self.note(key, rating="use", tags=["dungeon", "boss", "dungeon"], note=text, target="otyugh")
        self.assertEqual(200, code, out)
        path = os.path.join(self.docs, "docs", "music", "music-notes.json")
        with open(path, encoding="utf-8") as f:
            saved = json.load(f)
        self.assertEqual(1, saved["schema"])
        rec = saved["tracks"][key]
        self.assertEqual(("sos-dlc", "Britain_Sewers.mp3", "use", ["dungeon", "boss"], text, "otyugh"),
                         (rec["pack"], rec["file"], rec["rating"], rec["tags"], rec["note"], rec["target"]))
        self.assertEqual(rec, self.req("/api/music")[1]["notes"][key], "the page reads back what was saved")
        code, s, _ = self.req("/api/music/summary")
        self.assertEqual(1, s["count"])
        self.assertEqual(["dungeon", "boss"], [g["tag"] for g in s["groups"]], "grouped by tag in the tags' order")
        self.assertEqual(("otyugh", key), (s["groups"][1]["targets"][0]["target"], s["groups"][1]["targets"][0]["tracks"][0]["key"]))
        self.assertEqual(200, self.note("paranoia/OLDULT01.mp3", rating="maybe", tags=[], note="", target="")[0])
        self.assertEqual(1, self.req("/api/music/summary")[1]["count"], "only Use is summarised")
        # Clearing every field removes the record; the other stays.
        self.assertEqual(200, self.note("paranoia/OLDULT01.mp3", rating="", tags=[], note="", target="")[0])
        with open(path, encoding="utf-8") as f:
            self.assertEqual([key], list(json.load(f)["tracks"]))
        for bad, why in (({"rating": "great"}, "rating"), ({"tags": ["castle"]}, "tags"), ({"note": "x" * 4001}, "note"),
                         ({"target": "a\nb"}, "target")):
            code, out, _ = self.note(key, **bad)
            self.assertEqual(400, code, why)
        self.assertEqual(400, self.note("sos-vol2/Nope.mp3", rating="use")[0], "only a listed track")
        with open(path, encoding="utf-8") as f:
            self.assertEqual(rec, json.load(f)["tracks"][key], "a refused note changes nothing")
        self.assertFalse(os.path.exists(path + ".tmp"))

    # cc-P456: a save that cannot be made says so, and never loses what is on disk.
    def test_a_malformed_notes_file_is_untouched_after_a_save(self):
        path = self.notes_path()
        os.makedirs(os.path.dirname(path), exist_ok=True)
        keep = None
        if os.path.exists(path):
            with open(path, "rb") as f:
                keep = f.read()
        try:
            for bad, why in ((b'{"about": "x", "tracks": {"sos-vol2/Turfin.mp3": {"rating": "use"', "cut short"),
                             (b'{"schema": 1, "tracks": []}', "tracks is not an object"),
                             (b'["sos-vol2/Turfin.mp3"]', "not an object at all"),
                             (b'\xff\xfe{"tracks": {}}', "not UTF-8"),
                             (b'{"schema": 1, "tracks": {"sos-vol2/Turfin.mp3": "use"}}', "a record is not an object")):
                with open(path, "wb") as f:
                    f.write(bad)
                code, out, _ = self.req("/api/music/note", {"key": "sos-vol2/Britain1.mp3", "rev": 0, "rating": "use",
                                                            "note": "Britain at dawn"}, ACT)
                self.assertNotEqual(200, code, why)
                self.assertIn("music-notes.json", out.get("error", ""), why)
                with open(path, "rb") as f:
                    self.assertEqual(bad, f.read(), "the file is untouched: " + why)
                self.assertFalse(os.path.exists(path + ".tmp"), why)
                code, d, _ = self.req("/api/music")
                self.assertEqual(200, code, why)
                self.assertIn("music-notes.json", d.get("notesError") or "", "the page is told: " + why)
            os.remove(path)
            code, d, _ = self.req("/api/music")
            self.assertEqual(("", {}), (d.get("notesError"), d["notes"]), "only a missing file is empty")
            self.assertEqual(200, self.note("sos-vol2/Britain1.mp3", rating="use")[0], "a missing file is a first save")
        finally:
            if keep is None:
                if os.path.exists(path):
                    os.remove(path)
            else:
                with open(path, "wb") as f:
                    f.write(keep)

    def test_a_stale_revision_is_refused(self):
        key = "sos-dlc/Turfin.mp3"
        code, first, _ = self.note(key, rating="maybe", note="the older note")
        self.assertEqual(200, code, first)
        base = first["record"]["rev"]
        code, newer, _ = self.note(key, rev=base, rating="maybe", note="the newer note")
        self.assertEqual(200, code, newer)
        self.assertGreater(newer["record"]["rev"], base)
        # The older save, sent from the same revision, arrives last: it is refused and the newer note stays.
        code, out, _ = self.note(key, rev=base, rating="pass", note="the older note")
        self.assertEqual(409, code, out)
        with open(self.notes_path(), encoding="utf-8") as f:
            rec = json.load(f)["tracks"][key]
        self.assertEqual(("maybe", "the newer note"), (rec["rating"], rec["note"]))
        self.assertEqual(400, self.req("/api/music/note", {"key": key, "rating": "use"}, ACT)[0], "a save names its revision")
        self.assertEqual(400, self.note(key, rev="2", rating="use")[0], "a revision is a number")
        self.assertEqual(200, self.note(key, rating="", tags=[], note="", target="")[0])

    # --- only Chase -------------------------------------------------------------------------------------------
    def test_the_page_refuses_anyone_but_chase(self):
        nobody = {}
        stranger = {"Tailscale-User-Login": "someone@example.com", "X-UOConsole-Proof": PROOF}
        forged = {"Tailscale-User-Login": "chase@example.com", "X-UOConsole-Proof": "wrong"}
        for who in (nobody, stranger, forged):
            for path in ("/music", "/music.js", "/api/music", "/api/music/summary", "/music-file/sos-vol2/Turfin.mp3"):
                code, body, _ = self.req(path, headers=who)
                self.assertEqual(403, code, (path, who))
                self.assertNotIn(b"ID3", body if isinstance(body, bytes) else b"", "no audio leaks")
            code, _, _ = self.req("/api/music/note", {"key": "sos-vol2/Turfin.mp3", "rating": "pass"}, dict(who, **{"X-UOConsole-Action": "1"}))
            self.assertEqual(403, code, who)
            self.assertEqual(403, self.req("/api/music/commit", {}, dict(who, **{"X-UOConsole-Action": "1"}))[0])
        # Chase, but not from the page: no header, another origin, a cross-site fetch, not JSON.
        for h in (dict(CHASE), dict(ACT, Origin="https://evil.example"), dict(ACT, **{"Sec-Fetch-Site": "cross-site"})):
            self.assertEqual(403, self.req("/api/music/note", {"key": "sos-vol2/Turfin.mp3", "rating": "pass"}, h)[0], h)
        self.assertEqual(415, self.req("/api/music/note", b'{"key": "x"}', dict(ACT, **{"Content-Type": "text/plain"}))[0])
        path = os.path.join(self.docs, "docs", "music", "music-notes.json")
        if os.path.exists(path):
            with open(path, encoding="utf-8") as f:
                self.assertNotIn("sos-vol2/Turfin.mp3", json.load(f)["tracks"], "nothing refused was written")
        # This PC's own listener is Chase at the game PC (as for the rest of the console): it may look and take notes.
        self.assertEqual(200, self.req("/api/music", headers={}, port=self.local_port)[0])
        self.assertEqual(200, self.req("/api/music/note", {"key": "turfin/Turfin.mp3", "rev": 0, "rating": "maybe"},
                                       {"X-UOConsole-Action": "1"}, port=self.local_port)[0])
        self.assertEqual(405, self.req("/api/music/note", method="PUT")[0])

    # --- Save to repo -----------------------------------------------------------------------------------------
    def test_save_to_repo_commits_that_file_alone(self):
        self.assertEqual(200, self.note("sos-vol2/Britain1.mp3", rating="use", tags=["town"], target="Britain")[0])
        with open(os.path.join(self.docs, "README.md"), "a", encoding="utf-8") as f:
            f.write("someone else's change\n")
        before = git(self.docs, "rev-parse", "HEAD")
        code, out, _ = self.req("/api/music/commit", {}, ACT)
        self.assertEqual(200, code)
        self.assertTrue(out["ok"], out)
        self.assertEqual(["docs/music/music-notes.json"], git(self.docs, "show", "--name-only", "--format=", "HEAD").split())
        self.assertEqual(before, git(self.docs, "rev-parse", "HEAD~1"), "one commit on main")
        self.assertIn("Music Room notes", git(self.docs, "log", "-1", "--format=%s"))
        self.assertIn("README.md", git(self.docs, "status", "--porcelain"), "another file's change is left alone")
        self.assertFalse(self.console.music.changed())
        code, out, _ = self.req("/api/music/commit", {}, ACT)
        self.assertEqual((False, "nothing changed since the last commit"), (out["ok"], out["result"]))
        self.assertEqual(200, self.note("sos-vol2/Britain1.mp3", rating="use", tags=["town"], target="Britain Graveyard")[0])
        self.assertTrue(self.console.music.changed(), "the hourly commit sees the change")
        gitdir = os.path.join(self.docs, ".git")
        for busy, word in ((os.path.join(gitdir, "MERGE_HEAD"), "busy"), (os.path.join(self.locks, "commit.json"), "commit lock")):
            with open(busy, "w", encoding="ascii") as f:
                f.write("x\n")
            try:
                ok, msg = self.console.music.commit("hourly")
                self.assertFalse(ok, msg)
                self.assertIn(word, msg)
            finally:
                os.remove(busy)
        head = git(self.docs, "rev-parse", "HEAD")
        ok, msg = self.console.music.commit("hourly")
        self.assertTrue(ok, msg)
        self.assertNotEqual(head, git(self.docs, "rev-parse", "HEAD"))
        self.assertIn("hourly", git(self.docs, "log", "-1", "--format=%s"))
        git(self.docs, "checkout", "-q", "README.md")


@unittest.skipUnless(os.path.exists(EDGE), "Microsoft Edge is not installed")
class MusicRoomInEdge(unittest.TestCase):
    """The page in headless Edge through the tailnet listener (the proof and login as the sidecar sets them): every
    track drawn, Next unrated plays one (the player loads it through the Range route), a rating and a typed note
    land in music-notes.json, and the Summary shows it."""

    def test_rate_and_note_a_track_in_the_browser(self):
        import cdp
        tmp = tempfile.mkdtemp(prefix="p367-edge-")
        try:
            with open(os.path.join(tmp, "settings.json"), "w", encoding="utf-8") as f:
                json.dump({"allowedLogins": ["chase@example.com"]}, f)
            with open(os.path.join(tmp, "proof.key"), "w", encoding="ascii") as f:
                f.write(PROOF)
            tp, stock, regions, lib = make_library(tmp)
            docs = os.path.join(tmp, "docs")
            console = server.Console(REPO, docs, tmp, os.path.join(tmp, "status.json"), collector=False, golem_root=os.path.join(tmp, "golem"))
            console.music = music.MusicRoom(docs, third_party=tp, stock_dirs=[stock], regions_json=regions, art_library=lib, code_repo=REPO)
            port = free_port()
            s = server.ThreadingHTTPServer(("127.0.0.1", port), server.make_handler(console, panels.TAILNET))
            threading.Thread(target=s.serve_forever, daemon=True).start()
            try:
                with cdp.Browser(EDGE) as b:
                    b.phone(390, 844)
                    b.call("Network.enable")
                    b.call("Network.setExtraHTTPHeaders", headers=CHASE)
                    b.open("http://127.0.0.1:%d/music" % port)
                    self.assertTrue(b.wait_for("document.documentElement.getAttribute('data-drawn') === '1'"), "drawn")
                    self.assertEqual(9, b.eval("document.querySelectorAll('#list .track').length"))
                    self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391, "no sideways scroll on a phone")
                    self.assertEqual(6, b.eval("document.querySelectorAll('#f-pack option').length"), "every pack, the stock music and 'every'")
                    self.assertFalse(b.eval("document.getElementById('autoplay').checked"), "autoplay is off at first")
                    self.assertIn("world-building", b.eval("document.querySelector('#list .track textarea').placeholder"))
                    b.eval("document.getElementById('next-unrated').click()")
                    self.assertTrue(b.wait_for("document.getElementById('audio').src.indexOf('/music-file/') > 0"), "Next unrated plays a track")
                    first = b.eval("document.querySelector('.track.playing').getAttribute('data-key')")
                    self.assertEqual("sos-vol2/Britain1.mp3", first, "the first unrated, in list order")
                    b.eval("var r = document.querySelector('.track.playing'); r.querySelector('.rate button.use').click();"
                           "var t = r.querySelector('textarea'); t.value = 'Britain at dusk'; t.dispatchEvent(new Event('input'));"
                           "var g = r.querySelector('input.target'); g.value = 'Britain'; g.dispatchEvent(new Event('input'));"
                           "r.querySelector('.tags input[value=town]').click();")
                    self.assertTrue(b.wait_for("document.querySelector('.track.playing .saved').textContent.indexOf('saved ') === 0"), "saved")
                    path = os.path.join(docs, "docs", "music", "music-notes.json")
                    for _ in range(50):
                        with open(path, encoding="utf-8") as f:
                            rec = json.load(f)["tracks"].get(first, {})
                        if rec.get("note") == "Britain at dusk":
                            break
                        threading.Event().wait(0.1)
                    self.assertEqual(("use", ["town"], "Britain at dusk", "Britain"), (rec.get("rating"), rec.get("tags"), rec.get("note"), rec.get("target")))
                    b.eval("document.getElementById('next-unrated').click()")
                    self.assertTrue(b.wait_for("document.querySelector('.track.playing').getAttribute('data-key') !== '%s'" % first), "on to the next")
                    b.eval("document.querySelector('#views button[data-view=summary]').click()")
                    self.assertTrue(b.wait_for("document.querySelectorAll('#summary .item').length === 1"), "the summary lists it")
                    self.assertIn("Britain at dusk", b.eval("document.getElementById('summary').textContent"))
            finally:
                s.shutdown()
                s.server_close()
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


class EdgeRoom:
    """cc-P456: the Music Room in headless Edge on its own docs repo (a git repo on main), with a hook on save_note so a
    test can slow or count the saves. with EdgeRoom() as r: r.b is the browser, r.saves the notes saved, in order."""

    def __init__(self, delay=None):
        self.delay = delay or (lambda n: 0)   # seconds the n-th save (1, 2, ...) waits before it writes
        self.saves, self.live, self.most = [], 0, 0
        self.lock = threading.Lock()

    def __enter__(self):
        import cdp
        self.tmp = tempfile.mkdtemp(prefix="p456-edge-")
        with open(os.path.join(self.tmp, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"]}, f)
        with open(os.path.join(self.tmp, "proof.key"), "w", encoding="ascii") as f:
            f.write(PROOF)
        tp, stock, regions, lib = make_library(self.tmp)
        self.docs = os.path.join(self.tmp, "docs")
        os.makedirs(self.docs)
        git(self.docs, "init", "-q", "-b", "main")
        git(self.docs, "config", "user.email", "test@example.com")
        git(self.docs, "config", "user.name", "P456 test")
        with open(os.path.join(self.docs, "README.md"), "w", encoding="utf-8") as f:
            f.write("docs\n")
        git(self.docs, "add", "README.md")
        git(self.docs, "commit", "-q", "-m", "start")
        self.path = os.path.join(self.docs, "docs", "music", "music-notes.json")
        console = server.Console(REPO, self.docs, self.tmp, os.path.join(self.tmp, "status.json"), collector=False,
                                 golem_root=os.path.join(self.tmp, "golem"))
        console.music = music.MusicRoom(self.docs, third_party=tp, stock_dirs=[stock], regions_json=regions, art_library=lib,
                                        code_repo=REPO)
        real = console.music.save_note

        def save_note(key, fields):
            with self.lock:
                self.saves.append(fields.get("note"))
                n = len(self.saves)
                self.live += 1
                self.most = max(self.most, self.live)
            try:
                threading.Event().wait(self.delay(n))
                return real(key, fields)
            finally:
                with self.lock:
                    self.live -= 1
        console.music.save_note = save_note
        self.port = free_port()
        self.server = server.ThreadingHTTPServer(("127.0.0.1", self.port), server.make_handler(console, panels.TAILNET))
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.browser = cdp.Browser(EDGE)
        self.b = self.browser.__enter__()
        self.b.call("Network.enable")
        self.b.call("Network.setExtraHTTPHeaders", headers=CHASE)
        self.b.open("http://127.0.0.1:%d/music" % self.port)
        if not self.b.wait_for("document.documentElement.getAttribute('data-drawn') === '1'"):
            raise AssertionError("the page never drew")
        return self

    def __exit__(self, *a):
        try:
            self.browser.__exit__(*a)
        finally:
            self.server.shutdown()
            self.server.server_close()
            shutil.rmtree(self.tmp, ignore_errors=True)

    def row(self, key):
        return "document.querySelector('.track[data-key=\"%s\"]')" % key

    def type_note(self, key, text):
        self.b.eval("var t = %s.querySelector('textarea'); t.value = %s; t.dispatchEvent(new Event('input'));"
                    % (self.row(key), json.dumps(text)))

    def saved_text(self, key):
        return self.b.eval("%s.querySelector('.saved').textContent" % self.row(key))

    def state(self):
        return self.b.eval("document.getElementById('state').textContent")

    def leaving_warns(self):
        """What beforeunload does now: true when the page would ask before it is closed or reloaded."""
        return self.b.eval("(function () { var e = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(e);"
                           " return e.defaultPrevented; })()")

    def file_note(self, key):
        if not os.path.exists(self.path):
            return None
        with open(self.path, encoding="utf-8") as f:
            return (json.load(f)["tracks"].get(key) or {}).get("note")

    def wait(self, test, seconds=15):
        for _ in range(int(seconds * 10)):
            if test():
                return True
            threading.Event().wait(0.1)
        return False


@unittest.skipUnless(os.path.exists(EDGE), "Microsoft Edge is not installed")
class MusicRoomSavesInEdge(unittest.TestCase):
    """cc-P456: no path on the page reports saved when it is not."""
    KEY = "sos-vol2/Britain1.mp3"

    def commit_line(self, r):
        self.assertTrue(r.b.wait_for("/^(Saved to the repo|Not committed)/.test(document.getElementById('commit-line').textContent)", 20),
                        r.b.eval("document.getElementById('commit-line').textContent"))
        return r.b.eval("document.getElementById('commit-line').textContent")

    def test_a_failed_save_then_reload_warns(self):
        with EdgeRoom() as r:
            os.makedirs(os.path.dirname(r.path))
            with open(r.path, "w", encoding="utf-8") as f:
                f.write('{"tracks": {"cut short"')
            r.type_note(self.KEY, "Britain at dusk")
            self.assertTrue(r.wait(lambda: r.saved_text(self.KEY).startswith("not saved")), r.saved_text(self.KEY))
            self.assertIn("not saved", r.state())
            self.assertTrue(r.leaving_warns(), "a reload or close asks first while a track is not saved")
            self.assertTrue(r.leaving_warns(), "and asks again: the failed record is still kept")
            os.remove(r.path)
            r.b.eval("%s.querySelector('button.retry').click()" % r.row(self.KEY))
            self.assertTrue(r.wait(lambda: r.saved_text(self.KEY).startswith("saved ")), r.saved_text(self.KEY))
            self.assertEqual("Britain at dusk", r.file_note(self.KEY))
            self.assertFalse(r.leaving_warns(), "nothing to lose now")
            self.assertIn("all saved", r.state())

    def test_reversed_arrival_keeps_the_newer_note(self):
        with EdgeRoom(delay=lambda n: 1.5 if n == 1 else 0) as r:
            r.type_note(self.KEY, "the older note")
            self.assertTrue(r.wait(lambda: len(r.saves) == 1), "the first save is under way")
            r.type_note(self.KEY, "the newer note")
            threading.Event().wait(1.0)   # the newer one's quiet time ends while the first is still in flight
            self.assertTrue(r.wait(lambda: "all saved" in r.state() and r.live == 0, 20), r.state())
            self.assertEqual("the newer note", r.file_note(self.KEY))
            self.assertEqual(1, r.most, "one save per track in flight at a time")
            self.assertEqual("the newer note", r.saves[-1])

    def test_save_to_repo_during_a_slow_save_commits_the_latest(self):
        with EdgeRoom(delay=lambda n: 1.5) as r:
            r.type_note(self.KEY, "the latest note")
            self.assertTrue(r.wait(lambda: len(r.saves) == 1), "the save is under way")
            r.b.eval("document.getElementById('save-repo').click()")
            line = self.commit_line(r)
            self.assertTrue(line.startswith("Saved to the repo"), line)
            committed = json.loads(git(r.docs, "show", "HEAD:docs/music/music-notes.json"))
            self.assertEqual("the latest note", committed["tracks"][self.KEY]["note"])

    def test_save_to_repo_stops_when_a_track_is_not_saved(self):
        with EdgeRoom() as r:
            os.makedirs(os.path.dirname(r.path))
            with open(r.path, "w", encoding="utf-8") as f:
                f.write('{"tracks": []}\n')
            git(r.docs, "add", "docs/music/music-notes.json")
            git(r.docs, "commit", "-q", "-m", "a bad notes file")
            with open(r.path, "a", encoding="utf-8") as f:
                f.write(" \n")   # changed on disk, so a commit would go ahead if nothing stopped it
            head = git(r.docs, "rev-parse", "HEAD")
            r.type_note(self.KEY, "Britain at dusk")
            r.b.eval("document.getElementById('save-repo').click()")
            line = self.commit_line(r)
            self.assertTrue(line.startswith("Not committed"), line)
            self.assertIn("not saved", line)
            self.assertEqual(head, git(r.docs, "rev-parse", "HEAD"))


if __name__ == "__main__":
    unittest.main()
