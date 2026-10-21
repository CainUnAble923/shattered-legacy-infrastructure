"""Packet layouts the bot sends and reads, each checked against pinned ModernUO (D:\\UO\\ModernUO-pinned\\Projects).
README.md carries the full citation table; the citations here are the same, abbreviated:
  S = Server/Network/Packets/   U = UOContent/   ACC = U/Network/Packets/IncomingAccountPackets.cs
Big-endian unless said otherwise (Server/Buffers/SpanWriter.cs:182-260).
"""
import struct
import zlib

CLIENT_VERSION = (7, 0, 117, 0)
CLIENT_VERSION_TEXT = "7.0.117.0"

# Server-to-client lengths: a number is a fixed length (id included), None means "u16 length after the id".
# Only used to cross-check: on the game connection every compressed chunk is exactly one packet
# (Server/Network/NetState/NetState.cs:658-660), and the client logs any packet whose real length disagrees.
FIXED = {
    0x82: 2,    # login reject          S/OutgoingAccountPackets.cs:402
    0x8C: 11,   # relay                 S/OutgoingAccountPackets.cs:447
    0xB9: 5,    # features, extended    S/OutgoingAccountPackets.cs:140
    0x85: 2,    # delete result         S/OutgoingAccountPackets.cs:121
    0x53: 2,    # popup message         S/OutgoingAccountPackets.cs:131
    0x1B: 37,   # login confirm         S/OutgoingAccountPackets.cs:190
    0x55: 1,    # login complete        S/OutgoingAccountPackets.cs:231
    0x6C: 19,   # target                S/OutgoingTargetPackets.cs:57
    0x25: 21,   # item into container   S/OutgoingContainerPackets.cs:127
    0x24: 9,    # open container (HS)   S/OutgoingContainerPackets.cs:108
    0xF3: 26,   # world entity (HS)     S/OutgoingEntityPackets.cs:88
    0x20: 19,   # mobile update         S/OutgoingMobilePackets.cs:578
    0x77: 17,   # mobile moving         S/OutgoingMobilePackets.cs:104
    0x22: 3,    # move ack              S/OutgoingMovementPackets.cs:42
    0x21: 8,    # move reject           S/OutgoingMovementPackets.cs:45
    0x73: 2,    # ping ack              S/OutgoingPlayerPackets.cs:332
    0x1D: 5,    # remove entity         S/OutgoingEntityPackets.cs:63
    0x2E: 15,   # equip update          S/OutgoingEquipmentPackets.cs:86
    0x6D: 3,    # music                 S/OutgoingPlayerPackets.cs:271
    0x5B: 4,    # time                  S/OutgoingPlayerPackets.cs:312
    0xBC: 3,    # season                S/OutgoingPlayerPackets.cs:240
    0x4F: 2,    # global light          S/OutgoingLightPackets.cs:39
    0x4E: 6,    # personal light        S/OutgoingLightPackets.cs:23
    0x72: 5,    # war mode              S/OutgoingCombatPackets.cs:40
    0xE2: 10,   # new animation         S/OutgoingMobilePackets.cs:354
    0x2C: 2,    # death status          S/OutgoingPlayerPackets.cs:62
    0x88: 66,   # paperdoll             S/OutgoingPlayerPackets.cs:243
    0xA1: 9, 0xA2: 9, 0xA3: 9,  # hits/mana/stam  S/OutgoingMobilePackets.cs:210,235,260
    0x2D: 17,   # mobile attributes     S/OutgoingMobilePackets.cs:285
    0x65: 4,    # weather               S/OutgoingPlayerPackets.cs:95
    0x76: 16,   # server change         S/OutgoingPlayerPackets.cs:98
    0xDC: 9,    # OPL info              S/OutgoingEntityPackets.cs:33
    0x54: 12,   # sound                 S/OutgoingEffectPackets.cs:23 (SoundPacketLength)
    0xC0: 36,   # hued effect           S/OutgoingEffectPackets.cs:25
    0xC7: 49,   # particle effect       S/OutgoingEffectPackets.cs:24
    0x6E: 14,   # animation             S/OutgoingMobilePackets.cs:35
}
VARIABLE = {0xA8, 0xA9, 0x86, 0xBD, 0x1C, 0xAE, 0xC1, 0xCC, 0xDD, 0x11, 0x3A, 0x3C, 0x78, 0xD6, 0xBF, 0x17}


def u16(b, o): return struct.unpack_from(">H", b, o)[0]
def i16(b, o): return struct.unpack_from(">h", b, o)[0]
def u32(b, o): return struct.unpack_from(">I", b, o)[0]
def i32(b, o): return struct.unpack_from(">i", b, o)[0]


def latin1(s, n):
    """WriteLatin1(s, n): cut to n, zero padded (Server/Buffers/SpanWriter.cs:322-351)."""
    b = s.encode("latin-1", "replace")[:n]
    return b + b"\x00" * (n - len(b))


def read_latin1(b, o, n):
    return b[o:o + n].split(b"\x00", 1)[0].decode("latin-1")


def read_cstr(b, o):
    end = b.index(b"\x00", o) if b"\x00" in b[o:] else len(b)
    return b[o:end].decode("latin-1"), end + 1


def read_bigu_null(b, o):
    i = o
    while i + 1 < len(b) and (b[i] or b[i + 1]):
        i += 2
    return b[o:i].decode("utf-16-be", "replace"), i + 2


def read_littleu_null(b, o):
    i = o
    while i + 1 < len(b) and (b[i] or b[i + 1]):
        i += 2
    return b[o:i].decode("utf-16-le", "replace"), i + 2


def split_chunk(chunk):
    """One decompressed chunk into packets. Normally it is exactly one; returns (packets, problems)."""
    out, problems, o = [], [], 0
    while o < len(chunk):
        pid = chunk[o]
        if pid in FIXED:
            n = FIXED[pid]
        elif pid in VARIABLE and o + 3 <= len(chunk):
            n = u16(chunk, o + 1)
        else:
            if o == 0:
                out.append(chunk)        # unknown id: the chunk is the packet
            else:
                problems.append("unknown 0x%02X inside a chunk at %d" % (pid, o))
                out.append(chunk[o:])
            return out, problems
        if n <= 0 or o + n > len(chunk):
            problems.append("0x%02X: table says %d, chunk has %d left; trusting the chunk" % (pid, n, len(chunk) - o))
            out.append(chunk[o:])
            return out, problems
        if o == 0 and n != len(chunk) and pid in FIXED:
            problems.append("0x%02X: table says %d, the server sent %d in one chunk" % (pid, n, len(chunk)))
        out.append(chunk[o:o + n])
        o += n
    return out, problems


# --- client to server ------------------------------------------------------------------------------------------

def seed(seed_value):
    """0xEF, 21: int32 seed; int32 major, minor, revision, patch. ACC:522 (registered :86)."""
    return struct.pack(">BI4i", 0xEF, seed_value, *CLIENT_VERSION)


def account_login(user, password):
    """0x80, 62: Latin1 user[30]; Latin1 password[30]; 1 byte. ACC:550 (registered :80)."""
    return b"\x80" + latin1(user, 30) + latin1(password, 30) + b"\x00"


def play_server(index):
    """0xA0, 3: int16 index. ACC:493."""
    return struct.pack(">Bh", 0xA0, index)


def game_login(auth_id, user, password):
    """0x91, 65: int32 authId; Latin1 user[30]; Latin1 password[30]. ACC:433."""
    return struct.pack(">Bi", 0x91, auth_id) + latin1(user, 30) + latin1(password, 30)


def create_character(name, gender_race, stats, skills, skin_hue, hair, hair_hue, beard, beard_hue, city, shirt_hue,
                     pants_hue, profession=0, flags=0):
    """0xF8, 106. ACC:90-139: skip 9; name[30]; skip 2; int32 flags; skip 8; byte profession; skip 15;
    byte genderRace (SA: 2/3 human, 4/5 elf, 6/7 gargoyle, ACC:142-150); str, dex, int; 4 (skill, value) pairs
    (NewCharacterCreation, 7.0.16+); u16 hue; i16 hair; i16 hairHue; i16 beard; i16 beardHue; byte; byte cityIndex;
    skip 8; i16 shirtHue; i16 pantsHue."""
    b = bytearray(b"\xF8")
    b += struct.pack(">IIB", 0xEDEDEDED, 0xFFFFFFFF, 0)
    b += latin1(name, 30)
    b += b"\x00\x00"
    b += struct.pack(">i", flags)
    b += b"\x00" * 8
    b += struct.pack(">B", profession)
    b += b"\x00" * 15
    b += struct.pack(">B", gender_race)
    b += struct.pack(">BBB", *stats)
    for sk, val in skills:
        b += struct.pack(">BB", sk, val)
    b += struct.pack(">Hhhhh", skin_hue, hair, hair_hue, beard, beard_hue)
    b += b"\x00"
    b += struct.pack(">B", city)
    b += b"\x00" * 8
    b += struct.pack(">hh", shirt_hue, pants_hue)
    assert len(b) == 106, len(b)
    return bytes(b)


def play_character(name, slot):
    """0x5D, 73: 0xEDEDEDED; name[30]; 2 unknown; int32 flags; 24 skipped; int32 slot; 4 bytes IP. ACC:248."""
    b = struct.pack(">I", 0xEDEDEDED) + latin1(name, 30) + b"\x00\x00" + struct.pack(">i", 0x1F) + b"\x00" * 24
    b += struct.pack(">i", slot) + b"\x7F\x00\x00\x01"
    return b"\x5D" + b


def delete_character(index):
    """0x83, 39: password[30] (not read); int32 index; 4 bytes IP. ACC:217."""
    return b"\x83" + b"\x00" * 30 + struct.pack(">i", index) + b"\x7F\x00\x00\x01"


def client_version():
    """0xBD, var: ASCII version, null-terminated. ACC:225 (registered :84)."""
    body = CLIENT_VERSION_TEXT.encode("ascii") + b"\x00"
    return struct.pack(">BH", 0xBD, 3 + len(body)) + body


def move(direction, seq, run=False):
    """0x02, 7: byte dir (0x80 run); byte seq; u32 fastwalk key, read and ignored. U/Network/Packets/IncomingMovementPackets.cs:83-94."""
    return struct.pack(">BBBI", 0x02, (direction & 7) | (0x80 if run else 0), seq & 0xFF, 0)


def unicode_speech(text, typ=0, hue=0x34, font=3, lang="ENU"):
    """0xAD, var: byte type; i16 hue; i16 font; ASCII lang[4]; then, with the 0xC0 bit unset, BigUnicode text
    null-terminated. U/Network/Packets/IncomingMessagePackets.cs:58. Without keywords, keyword-driven NPCs
    (vendors' buy, bankers' balance) do not react; commands and plain speech do."""
    body = struct.pack(">Bhh", typ, hue, font) + latin1(lang, 4) + text.encode("utf-16-be") + b"\x00\x00"
    return struct.pack(">BH", 0xAD, 3 + len(body)) + body


def double_click(serial):
    """0x06, 5: u32 serial. U/Network/Packets/IncomingEntityPackets.cs:61."""
    return struct.pack(">BI", 0x06, serial)


def single_click(serial):
    """0x09, 5: u32 serial. U/Network/Packets/IncomingEntityPackets.cs:105."""
    return struct.pack(">BI", 0x09, serial)


def target_object(target_id, serial, flags=0):
    """0x6C, 19: byte type 0; int32 targetId; byte flags; u32 serial; i16 x; i16 y; byte; sbyte z; u16 graphic.
    U/Network/Packets/IncomingTargetingPackets.cs:28."""
    return struct.pack(">BBiBIhhBbH", 0x6C, 0, target_id, flags, serial, 0, 0, 0, 0, 0)


def target_ground(target_id, x, y, z, graphic=0, flags=0):
    return struct.pack(">BBiBIhhBbH", 0x6C, 1, target_id, flags, 0, x, y, 0, z, graphic)


def target_cancel(target_id):
    return struct.pack(">BBiBIhhBbH", 0x6C, 0, target_id, 3, 0, -1, -1, 0, 0, 0)


def gump_response(serial, type_id, button, switches=(), texts=()):
    """0xB1, var: u32 serial; int32 typeId; int32 button; int32 switchCount; int32 switches; int32 textCount;
    per entry u16 id, u16 charLen, UTF-16BE (no null). U/Gumps/Base/GumpSystem.IncomingPackets.cs:31."""
    body = struct.pack(">Iii", serial, type_id, button)
    body += struct.pack(">i", len(switches)) + b"".join(struct.pack(">i", s) for s in switches)
    body += struct.pack(">i", len(texts))
    for tid, txt in texts:
        t = txt.encode("utf-16-be")
        body += struct.pack(">HH", tid, len(t) // 2) + t
    return struct.pack(">BH", 0xB1, 3 + len(body)) + body


def status_request(serial, kind=4):
    """0x34, 10: int32 0xEDEDEDED; byte type (4 stats, 5 skills); u32 serial. U/Network/Packets/IncomingPlayerPackets.cs:389."""
    return struct.pack(">BIBI", 0x34, 0xEDEDEDED, kind, serial)


def ping(n=0):
    """0x73, 2: echoed. U/Network/Packets/IncomingPlayerPackets.cs:379."""
    return struct.pack(">BB", 0x73, n & 0xFF)


def lift(serial, amount=1):
    """0x07, 7: u32 serial; u16 amount. U/Network/Packets/IncomingItemPackets.cs:35."""
    return struct.pack(">BIH", 0x07, serial, amount)


def drop(serial, x, y, z, container):
    """0x08, 15 (ContainerGridLines): u32 serial; i16 x; i16 y; sbyte z; byte grid; u32 destination.
    U/Network/Packets/IncomingItemPackets.cs:69, length U/Network/ContainerGridPacketHandler.cs."""
    return struct.pack(">BIhhbBI", 0x08, serial, x, y, z, 0, container)


def equip(serial, layer, mobile):
    """0x13, 10: u32 item (skipped); byte layer (skipped); u32 mobile. The server equips the item being held.
    U/Network/Packets/IncomingItemPackets.cs:44."""
    return struct.pack(">BIBI", 0x13, serial, layer, mobile)


def war_mode(on):
    """0x72, 5: bool warmode; 3 bytes ignored. U/Network/Packets/IncomingPlayerPackets.cs:356."""
    return struct.pack(">BBBBB", 0x72, 1 if on else 0, 0, 0x32, 0)


def attack(serial):
    """0x05, 5: u32 serial. U/Network/Packets/IncomingPlayerPackets.cs:69."""
    return struct.pack(">BI", 0x05, serial)


def opl_request(serials):
    """0xD6, var: u32 serials. U/Network/Packets/IncomingEntityPackets.cs:154."""
    body = b"".join(struct.pack(">I", s) for s in serials)
    return struct.pack(">BH", 0xD6, 3 + len(body)) + body


def text_command(kind, text):
    """0x12, var: byte type; ASCII text, null-terminated. U/Network/Packets/IncomingPlayerPackets.cs:135 (the 0x24
    branch :145 is 'use skill': the text is the skill index then a space and 0)."""
    body = struct.pack(">B", kind) + text.encode("ascii") + b"\x00"
    return struct.pack(">BH", 0x12, 3 + len(body)) + body


def use_skill(skill_index):
    """cc-P223: 0x12 type 0x24 '<skillIndex> 0' (IncomingPlayerPackets.cs:145-153, Skills.UseSkill)."""
    return text_command(0x24, "%d 0" % skill_index)


def context_menu_request(serial):
    """cc-P223: 0xBF sub 0x13, 9: u16 len; u16 0x13; u32 serial. U/Context Menus/ContextMenuSystem.cs:101
    (registered :15)."""
    return struct.pack(">BHHI", 0xBF, 9, 0x13, serial)


def context_menu_select(serial, index):
    """cc-P223: 0xBF sub 0x15, 11: u16 len; u16 0x15; u32 serial; u16 index. ContextMenuSystem.cs:48-98 (the entry
    must be enabled and in range, else nothing happens)."""
    return struct.pack(">BHHIH", 0xBF, 11, 0x15, serial, index)


# --- server to client -------------------------------------------------------------------------------------------

def parse_context_menu(p):
    """cc-P223: 0xBF sub 0x14 (ContextMenuSystem.cs:140-215): u16 sub; u16 command (1 old, 2 new); u32 serial; byte
    count; per entry, new: i32 number, u16 index, u16 flags; old: u16 index, u16 number - 3000000, u16 flags, and
    u16 color when flags has Colored (0x20). Returns {serial, entries: [{index, number, flags}]}."""
    command = u16(p, 5)
    serial = u32(p, 7)
    count = p[11]
    o = 12
    entries = []
    for _ in range(count):
        if command == 2:
            number, index, flags = i32(p, o), u16(p, o + 4), u16(p, o + 6)
            o += 8
        else:
            index, number, flags = u16(p, o), u16(p, o + 2) + 3000000, u16(p, o + 4)
            o += 6
            if flags & 0x20:
                o += 2
        entries.append({"index": index, "number": number, "flags": flags})
    return {"serial": serial, "entries": entries, "command": command}


def parse_gump_dd(p):
    """0xDD: u16 len; u32 serial; int32 typeId; int32 x; int32 y; layout block (int32 cLen = 4 + zlib bytes,
    int32 dLen, zlib); int32 lineCount; text block (same). An empty block is a lone int32 0.
    Text: per string u16 charCount + UTF-16BE. U/Gumps/Base/OutgoingGumpPackets.cs:31-38."""
    serial, type_id, x, y = u32(p, 3), i32(p, 7), i32(p, 11), i32(p, 15)
    o = 19

    def block(o):
        clen = i32(p, o)
        if clen == 0:
            return b"", o + 4
        data = zlib.decompress(p[o + 8:o + 4 + clen])
        return data, o + 4 + clen

    layout, o = block(o)
    nlines = i32(p, o)
    o += 4
    text, o = block(o)
    lines, t = [], 0
    for _ in range(nlines):
        if t + 2 > len(text):
            break
        n = u16(text, t)
        lines.append(text[t + 2:t + 2 + n * 2].decode("utf-16-be", "replace"))
        t += 2 + n * 2
    return {"serial": serial, "typeId": type_id, "x": x, "y": y,
            "layout": layout.rstrip(b"\x00").decode("latin-1", "replace"), "lines": lines}
