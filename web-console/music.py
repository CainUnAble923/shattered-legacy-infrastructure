"""cc-P367: the Music Room. Every candidate music track on disk, Chase's notes on each, and the summary P368 reads.

    MusicRoom(packs, stock_dirs, notes_file, ...)
        tracks()                  every track: pack, file, the stock track it remixes (or new), length, and its
                                  ID3 title, artist and album (notUltima: an album tag that does not say Ultima)
        path_of(pack, file)       the file on disk, only for a track the scan listed (the page never names a path)
        notes() / save_note(key, fields)
                                  Chase's rating, tags, note and target per track, in music-notes.json (one record per
                                  track, keyed "<pack>/<file>"), written whole and atomically on every save
        summary()                 the tracks marked Use, grouped by tag and then by target
        suggestions()             target names: the shard's regions and the boss shortlist in ART-LIBRARY.md
        commit()                  commits music-notes.json alone in the docs repo (see commit_notes)

The music folders are read only: nothing is copied, run or written there. Standard library only, like server.py.
"""
import json
import os
import re
import struct
import subprocess
import threading
import time

THIRD_PARTY = r"D:\UO\third-party"
# (id, label, folder under THIRD_PARTY, credit). The folder's own SOURCE.txt says where each came from.
PACKS = (
    ("sos-vol2", "SOS Mix Vol.2", r"sos-music-vol2\UO Classic Music (SOS Mix) Vol.2\Digital", "Zigholtul"),
    ("sos-dlc", "SOS Mix + DLC", r"sos-music-dlc\UO Classic Music (SOS Mix) + DLC\Digital", "Zigholtul"),
    ("paranoia", "Paranoia remixes", "paranoia-remixes", "ParanoiaPhD"),
    ("turfin", "Paranoia Turfin", "paranoia-turfin", "ParanoiaPhD"),
)
STOCK_ID, STOCK_LABEL = "stock", "Stock client"
# The stock client's music, first one found: the shard's own client data, then the plain client this PC keeps.
STOCK_DIRS = (r"{repo}\client-data\classic-client\Music\Digital", r"D:\UO\UltimaOnlineVanilla\Music\Digital")
REGIONS_JSON = r"D:\UO\ModernUO-pinned\Distribution\Data\regions.json"
ART_LIBRARY = os.path.join(THIRD_PARTY, "ART-LIBRARY.md")
NOTES_REL = ("docs", "music", "music-notes.json")

RATINGS = ("use", "maybe", "pass")
TAGS = (("town", "town"), ("region", "region or area"), ("dungeon", "dungeon"), ("monster", "monster"), ("boss", "boss"),
        ("event", "event or holiday"), ("travel", "travel or sea"), ("combat", "combat"), ("menu", "menu or login"))
TAG_IDS = tuple(t for t, _ in TAGS)
NOTE_MAX, TARGET_MAX = 4000, 120
TRACK_FILE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 _.,()'&+-]{0,118}\.mp3$", re.I)
NOTES_ABOUT = ("cc-P367: Chase's notes from the web console's Music Room (/music), one record per track keyed "
               "<pack>/<file>. rating: use, maybe or pass; tags: " + ", ".join(TAG_IDS) + "; target: an area, monster "
               "or boss. P368 reads the tracks marked use. Written by web-console/music.py; edit on the page.")
# ID3v2 text frames worth showing: title, artist, album (v2.3/2.4 names, then v2.2's three-letter ones).
ID3_FRAMES = {b"TIT2": "title", b"TPE1": "artist", b"TALB": "album", b"TT2": "title", b"TP1": "artist", b"TAL": "album"}
BITRATES = {  # kbps by (MPEG-1?, layer) and the header's 4-bit index
    (True, 1): (0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448),
    (True, 2): (0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384),
    (True, 3): (0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320),
    (False, 1): (0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256),
    (False, 2): (0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160),
}
BITRATES[(False, 3)] = BITRATES[(False, 2)]
RATES = {3: (44100, 48000, 32000), 2: (22050, 24000, 16000), 0: (11025, 12000, 8000)}   # by version bits


# --- reading an mp3 -----------------------------------------------------------------------------------------------
def _syncsafe(b):
    return (b[0] << 21) | (b[1] << 14) | (b[2] << 7) | b[3]


def _text(data):
    """An ID3 text frame's value (its first byte names the encoding)."""
    if not data:
        return ""
    enc, raw = data[0], data[1:]
    try:
        if enc == 0:
            s = raw.decode("latin-1")
        elif enc == 1:
            s = raw.decode("utf-16")
        elif enc == 2:
            s = raw.decode("utf-16-be")
        else:
            s = raw.decode("utf-8")
    except (UnicodeDecodeError, ValueError):
        s = raw.decode("latin-1", "replace")
    return s.replace("\x00", " ").strip()


def read_id3(tag, major):
    """{title, artist, album} from an ID3v2 tag body (major version 2, 3 or 4)."""
    out, i = {}, 0
    small = major == 2
    head = 6 if small else 10
    while i + head <= len(tag):
        fid = tag[i:i + (3 if small else 4)]
        if not fid.strip(b"\x00") or not re.match(rb"^[A-Z0-9]{3,4}$", fid):
            break
        if small:
            size = (tag[i + 3] << 16) | (tag[i + 4] << 8) | tag[i + 5]
        elif major == 4:
            size = _syncsafe(tag[i + 4:i + 8])
        else:
            size = struct.unpack(">I", tag[i + 4:i + 8])[0]
        body = tag[i + head:i + head + size]
        if fid in ID3_FRAMES and ID3_FRAMES[fid] not in out:
            v = _text(body)
            if v:
                out[ID3_FRAMES[fid]] = v[:200]
        i += head + size
    return out


def _frame_header(h):
    """(bytes per frame, samples per frame, sample rate, MPEG-1?, mono?) for a 4-byte MPEG audio header, or None."""
    if len(h) < 4 or h[0] != 0xFF or (h[1] & 0xE0) != 0xE0:
        return None
    ver, layer_bits = (h[1] >> 3) & 3, (h[1] >> 1) & 3
    br_i, sr_i, pad = (h[2] >> 4) & 15, (h[2] >> 2) & 3, (h[2] >> 1) & 1
    if ver == 1 or layer_bits == 0 or br_i in (0, 15) or sr_i == 3:
        return None
    layer = 4 - layer_bits
    mpeg1 = ver == 3
    rate = RATES[ver][sr_i]
    kbps = BITRATES[(mpeg1, layer)][br_i]
    if layer == 1:
        spf, size = 384, (12 * kbps * 1000 // rate + pad) * 4
    else:
        spf = 1152 if (layer == 2 or mpeg1) else 576
        size = (spf // 8) * kbps * 1000 // rate + pad
    return size, spf, rate, mpeg1, ((h[3] >> 6) & 3) == 3, kbps


def mp3_info(path):
    """{seconds, title, artist, album} for an mp3: the ID3v2 tag, then the length from the Xing/Info or VBRI header's
    frame count, or (constant bit rate) from the audio's size and bit rate. seconds is None when no frame is found."""
    out = {"seconds": None}
    size = os.path.getsize(path)
    with open(path, "rb") as f:
        head = f.read(10)
        start = 0
        if head[:3] == b"ID3" and len(head) == 10:
            tag_size = _syncsafe(head[6:10])
            out.update(read_id3(f.read(min(tag_size, 4 * 1024 * 1024)), head[3]))
            start = 10 + tag_size + (10 if head[5] & 0x10 else 0)
        f.seek(start)
        buf = f.read(256 * 1024)
        f.seek(max(0, size - 128))
        end = size - 128 if f.read(3) == b"TAG" else size
    for i in range(0, max(0, len(buf) - 4)):
        fh = _frame_header(buf[i:i + 4])
        if not fh:
            continue
        flen, spf, rate, mpeg1, mono, kbps = fh
        nxt = buf[i + flen:i + flen + 4]
        if len(nxt) == 4 and not _frame_header(nxt):
            continue   # a false sync inside other data: a real frame is followed by another
        side = (17 if mono else 32) if mpeg1 else (9 if mono else 17)
        frames = None
        x = buf[i + 4 + side:i + 4 + side + 16]
        if x[:4] in (b"Xing", b"Info") and struct.unpack(">I", x[4:8])[0] & 1:
            frames = struct.unpack(">I", x[8:12])[0]
        v = buf[i + 36:i + 36 + 18]
        if frames is None and v[:4] == b"VBRI":
            frames = struct.unpack(">I", v[14:18])[0]
        if frames:
            out["seconds"] = round(frames * spf / rate, 1)
        else:
            out["seconds"] = round((end - start - i) * 8 / (kbps * 1000), 1)
        break
    return out


def read_config(folder):
    """A client Music\\Digital Config.txt: {track name (lower case, no .mp3): [slot numbers]}."""
    slots = {}
    p = os.path.join(folder, "Config.txt")
    try:
        with open(p, encoding="latin-1") as f:
            for line in f:
                m = re.match(r"^\s*(\d+)\s+([^,\r\n]+)", line)
                if m:
                    name = m.group(2).strip()
                    if name.lower().endswith(".mp3"):
                        name = name[:-4]
                    slots.setdefault(name.lower(), []).append(int(m.group(1)))
    except OSError:
        pass
    return slots


# --- target suggestions -------------------------------------------------------------------------------------------
FIELD = re.compile(r"\b(Field|Wheatfield|Farm|Field of Sheep|Cabbage|Carrot|Turnip|Onion)\b")


def region_names(regions_json, repo):
    """The shard's region names: ModernUO's regions.json (farm fields left out), then our own regions' names
    (server/customizations/Regions/*.cs: RegionName and OldHavenTraining constants)."""
    names = set()
    try:
        with open(regions_json, encoding="utf-8") as f:
            for r in json.load(f):
                n = isinstance(r, dict) and r.get("Name")
                if n and not FIELD.search(n):
                    names.add(n.strip())
    except (OSError, ValueError):
        pass
    d = os.path.join(repo, "server", "customizations", "Regions")
    try:
        for fn in sorted(os.listdir(d)):
            if fn.endswith(".cs"):
                with open(os.path.join(d, fn), encoding="utf-8", errors="replace") as f:
                    for m in re.finditer(r'const string (?:RegionName|OldHavenTraining) = "([^"]+)"', f.read()):
                        names.add(m.group(1))
    except OSError:
        pass
    return sorted(names, key=lambda s: re.sub(r"^the ", "", s.lower()))


def boss_names(art_library):
    """The boss shortlist in ART-LIBRARY.md (its '### Boss shortlist' paragraph, before 'Systems queued')."""
    try:
        with open(art_library, encoding="utf-8") as f:
            text = f.read()
    except OSError:
        return []
    m = re.search(r"^### Boss shortlist\s*\n(.*?)(?:\n\s*\n|\n#)", text, re.S | re.M)
    if not m:
        return []
    body = " ".join(m.group(1).split()).split("Systems queued")[0]
    body = re.sub(r"\([^)]*\)", "", body).replace(" are New Haven's;", ",").replace("candidates for others:", "")
    out = []
    for piece in re.split(r",| and (?=[A-Z])", body):
        p = " ".join(piece.replace("`", "").split()).strip(" .;:")
        if p.startswith("and "):
            p = p[4:]
        if p and len(p) <= 60 and p not in out:
            out.append(p)
    return out


# --- committing the notes -----------------------------------------------------------------------------------------
def _git(repo, *args):
    r = subprocess.run(["git", "-C", repo] + list(args), capture_output=True, timeout=60,
                       creationflags=0x08000000 if os.name == "nt" else 0)
    return r.returncode, (r.stdout + r.stderr).decode("utf-8", "replace").strip()


def commit_notes(repo, rel, lock_dir=None, message="Music Room notes (web console)"):
    """(committed, why). Commits the one file rel in repo, alone (git commit --only), on main, and only when nothing
    else is in flight there: no merge, rebase or cherry-pick under way, no git index lock, and no Golem holding the
    commit lock (lock_dir/commit.json). Never pushes: the next Golem or Commit-ShardWork push carries it."""
    if not os.path.isfile(os.path.join(repo, rel)):
        return False, "no notes yet"
    code, gitdir = _git(repo, "rev-parse", "--git-dir")
    if code:
        return False, "not a git repo: " + gitdir[:120]
    gitdir = gitdir if os.path.isabs(gitdir) else os.path.join(repo, gitdir)
    for busy in ("MERGE_HEAD", "REBASE_HEAD", "CHERRY_PICK_HEAD", "rebase-merge", "rebase-apply", "index.lock"):
        if os.path.exists(os.path.join(gitdir, busy)):
            return False, "the docs repo is busy (%s); tried again later" % busy
    if lock_dir and os.path.exists(os.path.join(lock_dir, "commit.json")):
        return False, "a Golem holds the commit lock; tried again later"
    code, branch = _git(repo, "rev-parse", "--abbrev-ref", "HEAD")
    if code or branch != "main":
        return False, "the docs repo is on %s, not main" % (branch or "?")
    code, st = _git(repo, "status", "--porcelain", "--", rel)
    if code:
        return False, st[:200]
    if not st:
        return False, "nothing changed since the last commit"
    code, out = _git(repo, "add", "--", rel)
    if not code:
        code, out = _git(repo, "commit", "--only", "-m", message, "--", rel)
    if code:
        return False, "git refused: " + out[-300:]
    code, sha = _git(repo, "rev-parse", "--short", "HEAD")
    return True, "committed %s (not pushed: the next push carries it)" % sha


class MusicRoom:
    def __init__(self, notes_repo, packs=None, stock_dirs=None, third_party=THIRD_PARTY, regions_json=REGIONS_JSON,
                 art_library=None, code_repo="", lock_dir=None, cache_file=None):
        self.notes_repo = notes_repo
        self.notes_file = os.path.join(notes_repo, *NOTES_REL)
        self.packs = [{"id": i, "label": l, "dir": os.path.join(third_party, d), "credit": c} for i, l, d, c in (packs or PACKS)]
        self.stock_dirs = [d.replace("{repo}", code_repo or "") for d in (stock_dirs or STOCK_DIRS)]
        self.regions_json, self.code_repo = regions_json, code_repo
        self.art_library = art_library or os.path.join(third_party, "ART-LIBRARY.md")
        self.lock_dir = lock_dir
        self.cache_file = cache_file
        self.lock = threading.Lock()
        self._scan = (None, [])
        self._info = self._load_cache()
        self._info_dirty = False
        self.last_commit = {"at": "", "why": ""}

    # --- the tracks -------------------------------------------------------------------------------------------
    def stock_dir(self):
        for d in self.stock_dirs:
            if d and os.path.isfile(os.path.join(d, "Config.txt")):
                return d
        return ""

    def _folders(self):
        out = [dict(p) for p in self.packs]
        s = self.stock_dir()
        out.append({"id": STOCK_ID, "label": STOCK_LABEL, "dir": s, "credit": "EA (the client's own music)"})
        return out

    def _load_cache(self):
        try:
            with open(self.cache_file, encoding="utf-8") as f:
                d = json.load(f)
            return d if isinstance(d, dict) else {}
        except (OSError, ValueError, TypeError):
            return {}

    def _info_for(self, path):
        st = os.stat(path)
        key = "%s|%d|%d" % (path, st.st_size, int(st.st_mtime))
        if key not in self._info:
            try:
                self._info[key] = mp3_info(path)
            except (OSError, struct.error, IndexError, KeyError):
                self._info[key] = {"seconds": None}
            self._info_dirty = True
        return self._info[key]

    def tracks(self):
        """Every track, packs in PACKS order then the stock client's, each pack's files by name. Read again when a
        folder changes (its listing's names and times)."""
        folders = self._folders()
        stamp = []
        for p in folders:
            try:
                stamp.append((p["dir"], os.path.getmtime(p["dir"]) if p["dir"] else 0))
            except OSError:
                stamp.append((p["dir"], -1))
        with self.lock:
            if self._scan[0] == stamp:
                return self._scan[1]
            self._info_dirty = False
            stock_dir = self.stock_dir()
            stock = {}
            if stock_dir:
                for fn in os.listdir(stock_dir):
                    if fn.lower().endswith(".mp3"):
                        stock[os.path.splitext(fn)[0].lower()] = os.path.splitext(fn)[0]
            out = []
            for p in folders:
                if not p["dir"] or not os.path.isdir(p["dir"]):
                    continue
                cfg = read_config(p["dir"])
                for fn in sorted(os.listdir(p["dir"]), key=str.lower):
                    full = os.path.join(p["dir"], fn)
                    if not TRACK_FILE.match(fn) or not os.path.isfile(full):
                        continue
                    stem = os.path.splitext(fn)[0]
                    info = self._info_for(full)
                    if p["id"] == STOCK_ID:
                        remix, kind = "", "stock"
                    elif stem.lower() in stock:
                        remix, kind = stock[stem.lower()], "replaces"
                    elif p["id"] in ("paranoia", "turfin"):
                        remix, kind = stem.upper(), "midi"
                    else:
                        remix, kind = "", "new"
                    album = info.get("album") or ""
                    out.append({"key": p["id"] + "/" + fn, "pack": p["id"], "file": fn, "name": stem,
                                "remixOf": remix, "remixKind": kind, "seconds": info.get("seconds"),
                                "title": info.get("title") or "", "artist": info.get("artist") or "", "album": album,
                                "notUltima": bool(album) and "ultima" not in album.lower(),
                                "slots": cfg.get(stem.lower(), []), "bytes": os.path.getsize(full)})
            self._scan = (stamp, out)
            if self._info_dirty and self.cache_file:
                try:
                    tmp = self.cache_file + ".tmp"
                    with open(tmp, "w", encoding="utf-8") as f:
                        json.dump(self._info, f)
                    os.replace(tmp, self.cache_file)
                except OSError:
                    pass
            return out

    def packs_view(self):
        t = self.tracks()
        return [{"id": p["id"], "label": p["label"], "credit": p["credit"], "found": bool(p["dir"]) and os.path.isdir(p["dir"]),
                 "count": sum(1 for x in t if x["pack"] == p["id"])} for p in self._folders()]

    def path_of(self, pack, fn):
        """The track's file, only when the scan listed it (so a request can never name another path)."""
        for t in self.tracks():
            if t["pack"] == pack and t["file"] == fn:
                d = next(p["dir"] for p in self._folders() if p["id"] == pack)
                return os.path.join(d, fn)
        return None

    # --- the notes --------------------------------------------------------------------------------------------
    def notes(self):
        try:
            with open(self.notes_file, encoding="utf-8") as f:
                d = json.load(f)
            t = d.get("tracks") if isinstance(d, dict) else None
            return t if isinstance(t, dict) else {}
        except (OSError, ValueError):
            return {}

    def save_note(self, key, fields):
        """(record or None, error). Sets one track's record from fields (rating, tags, note, target); a record with
        nothing in it is removed. The whole file is written to a temporary file and moved into place."""
        if not isinstance(key, str) or "/" not in key:
            return None, "no such track"
        pack, fn = key.split("/", 1)
        if not self.path_of(pack, fn):
            return None, "no such track"
        rating = fields.get("rating") or ""
        if rating not in RATINGS + ("",):
            return None, "rating is use, maybe or pass"
        tags = fields.get("tags") or []
        if not isinstance(tags, list) or any(t not in TAG_IDS for t in tags):
            return None, "tags are " + ", ".join(TAG_IDS)
        note, target = fields.get("note") or "", fields.get("target") or ""
        if not isinstance(note, str) or len(note) > NOTE_MAX:
            return None, "the note is text, %d characters at most" % NOTE_MAX
        if not isinstance(target, str) or len(target) > TARGET_MAX or "\n" in target:
            return None, "the target is one line, %d characters at most" % TARGET_MAX
        rec = {"pack": pack, "file": fn, "rating": rating, "tags": [t for t in TAG_IDS if t in tags],
               "note": note.replace("\r\n", "\n"), "target": target.strip(), "updated": time.strftime("%Y-%m-%dT%H:%M:%S")}
        with self.lock:
            notes = self.notes()
            if rating or rec["tags"] or rec["note"].strip() or rec["target"]:
                notes[key] = rec
            else:
                notes.pop(key, None)
                rec = dict(rec, removed=True)
            os.makedirs(os.path.dirname(self.notes_file), exist_ok=True)
            tmp = self.notes_file + ".tmp"
            with open(tmp, "w", encoding="utf-8", newline="\n") as f:
                json.dump({"about": NOTES_ABOUT, "schema": 1, "tracks": dict(sorted(notes.items(), key=lambda kv: kv[0].lower()))},
                          f, indent=1, ensure_ascii=False)
                f.write("\n")
            os.replace(tmp, self.notes_file)
        return rec, ""

    def summary(self):
        """The tracks marked Use: {tag: {target: [track]}} with 'untagged' and '(no target)' for the rest, plus a count."""
        notes, by = self.notes(), {}
        tracks = {t["key"]: t for t in self.tracks()}
        n = 0
        for key, r in sorted(notes.items(), key=lambda kv: kv[0].lower()):
            if r.get("rating") != "use":
                continue
            n += 1
            t = tracks.get(key, {})
            item = {"key": key, "pack": r.get("pack"), "file": r.get("file"), "name": t.get("name") or os.path.splitext(r.get("file") or "")[0],
                    "remixOf": t.get("remixOf", ""), "remixKind": t.get("remixKind", ""), "seconds": t.get("seconds"),
                    "album": t.get("album", ""), "notUltima": t.get("notUltima", False), "note": r.get("note", ""),
                    "onDisk": key in tracks}
            for tag in (r.get("tags") or ["untagged"]):
                by.setdefault(tag, {}).setdefault(r.get("target") or "(no target)", []).append(item)
        order = list(TAG_IDS) + ["untagged"]
        return {"count": n, "groups": [{"tag": tag, "label": dict(TAGS).get(tag, tag),
                                        "targets": [{"target": k, "tracks": v} for k, v in sorted(by[tag].items(), key=lambda kv: kv[0].lower())]}
                                       for tag in order if tag in by]}

    def suggestions(self):
        return {"regions": region_names(self.regions_json, self.code_repo), "bosses": boss_names(self.art_library)}

    def commit(self, why="button"):
        ok, msg = commit_notes(self.notes_repo, "/".join(NOTES_REL), self.lock_dir,
                               "Music Room notes (web console, %s)" % ("hourly" if why == "hourly" else "Save to repo"))
        self.last_commit = {"at": time.strftime("%Y-%m-%dT%H:%M:%S"), "why": why, "ok": ok, "result": msg}
        return ok, msg

    def changed(self):
        """True when music-notes.json differs from what the docs repo has committed (or is not in it yet)."""
        if not os.path.isfile(self.notes_file):
            return False
        code, st = _git(self.notes_repo, "status", "--porcelain", "--", "/".join(NOTES_REL))
        return code == 0 and bool(st)
