"""Reading the client's Cliloc.enu, so the bot can show localized messages and gump text as words (cc-P91).

The format and the decompression are pinned's own: Server/Localization/Localization.cs:97-140 (a 6-byte header
int32 2, int16 1, or else a BWT-compressed file whose first 4 bytes are skipped; then per entry int32 number, byte
flag, int16 length, UTF-8 text, little-endian) and Server/Client/BwtDecompress.cs (ported below, step for step).
Decoding 5 MB in Python takes a while, so the result is cached as JSON beside the bot's world (outside the repos).
"""
import json
import os
import struct


def _bwt_decompress(data):
    """Server/Client/BwtDecompress.cs. data: the file from offset 4."""
    # Decompress (:9-35). BuildTable (:37-55) fills every 16-bit value once and sorts them, so the table is simply
    # 0..65535 and only its first 256 entries are ever read: a move-to-front over the byte values.
    first = data[0]
    table = list(range(256))
    out = bytearray(len(data))
    i = 0
    # The C# loop reads the first byte, then runs while the stream has more: so it processes len - 1 bytes, the last
    # byte read being only ever looked at, never processed (:18-33).
    for pos in range(1, len(data)):
        c = first
        value = table[c]
        if c > 0:
            table[1:c + 1] = table[0:c]
        table[0] = value
        out[i] = value & 0xFF
        i += 1
        first = data[pos]
    out = bytes(out[:i])
    return _internal(out)


def _internal(inp):
    """InternalDecompress (:57-116)."""
    symbol = bytearray(range(256))
    partial = list(struct.unpack_from("<256i", inp, 0)) + [0] * 512
    total = sum(partial[:256])
    nonzero = 256 - partial[:256].count(0)
    # Frequency (:118-144): indices ordered by count, largest first.
    tmp = partial[:256]
    freq = []
    for _ in range(256):
        best, idx = 0, 0
        for j in range(256):
            if tmp[j] > best:
                idx, best = j, tmp[j]
        if best == 0:
            break
        freq.append(idx)
        tmp[idx] = 0
    m = 0
    for i in range(nonzero):
        f = freq[i]
        symbol[inp[m + 1024]] = f
        partial[f + 256] = m + 1
        m += partial[f]
        partial[f + 512] = m
    val = symbol[0]
    out = bytearray(total)
    count = 0
    while count < total:
        first_ref = partial[val + 256]
        out[count] = val
        if first_ref >= partial[val + 512]:
            if nonzero > 0:
                nonzero -= 1
                symbol[0:nonzero] = symbol[1:nonzero + 1]
                val = symbol[0]
            else:
                nonzero -= 1
        else:
            idx = inp[first_ref + 1024]
            partial[val + 256] = first_ref + 1
            if idx != 0:
                symbol[0:idx] = symbol[1:idx + 1]
                symbol[idx] = val
                val = symbol[0]
        count += 1
    return bytes(out)


def parse(raw):
    """The cliloc file's bytes -> {number: text}."""
    if struct.unpack_from("<ih", raw, 0) == (2, 1):
        data = raw[6:]
    else:
        data = _bwt_decompress(raw[4:])
        if struct.unpack_from("<ih", data, 0) != (2, 1):
            raise ValueError("invalid cliloc header after decompression")
        data = data[6:]
    table, o = {}, 0
    while o + 7 <= len(data):
        num, _flag, ln = struct.unpack_from("<iBH", data, o)
        o += 7
        table[num] = data[o:o + ln].decode("utf-8", "replace")
        o += ln
    return table


def load(path, cache_dir):
    """The table, from the cache when it matches the file's size and time, else decoded and cached."""
    st = os.stat(path)
    key = "%d-%d" % (st.st_size, int(st.st_mtime))
    cache = os.path.join(cache_dir, "cliloc-enu-%s.json" % key)
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            return {int(k): v for k, v in json.load(f).items()}
    with open(path, "rb") as f:
        table = parse(f.read())
    os.makedirs(cache_dir, exist_ok=True)
    with open(cache, "w", encoding="utf-8") as f:
        json.dump(table, f)
    return table
