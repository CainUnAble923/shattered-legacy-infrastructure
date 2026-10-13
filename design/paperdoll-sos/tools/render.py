# cc-P81: offline paperdoll renders with the Siege of Sosaria art, for the notes and Chase's in-game check.
# Reads only: the vendored SFW PNGs (player-package/vendor/paperdoll-sos), and this machine's own classic client files
# (gumpartLegacyMUL.uop, tiledata.mul) for every gump the kit does not redraw and for item animation numbers. Writes
# small PNGs to design/paperdoll-sos/test/. Not a client: no hues (body gumps show their stored colors, the client would
# tint them by skin hue), and items are layered by ClassicUO's paperdoll order.
import os, struct, sys, zlib
from PIL import Image, ImageDraw
src = open(r'D:/ShatteredLegacy/design/mine-camp-tents/uorender.py').read()
src = src.replace("os.path.expanduser('~/mnt/ShatteredLegacy/client-data/classic-client')", "r'D:/ShatteredLegacy/client-data/classic-client'")
src = src.split('def persp_coeffs')[0].replace('from PIL import Image, ImageDraw, ImageFont', '')
NS = {}
exec(compile(src, 'uorender', 'exec'), NS)
CD = NS['CD']
VENDOR = r'D:/ShatteredLegacy/player-package/vendor/paperdoll-sos/ExternalImages/gumps'
OUT = r'D:/ShatteredLegacy/design/paperdoll-sos/test'
os.makedirs(OUT, exist_ok=True)

gumps = NS['UOP'](f'{CD}/gumpartLegacyMUL.uop')

def stock_gump(gid):
    v = gumps.e.get(NS['hashname']('build/gumpartlegacymul/%08d.tga' % gid))
    if not v:
        return None
    gumps.f.seek(v[0]); data = gumps.f.read(v[1])
    if v[3] in (1, 3):
        data = zlib.decompress(data)
    if v[3] == 3:
        data = bwt_decompress(data)
    w, h = struct.unpack_from('<II', data, 0)
    if w == 0 or h == 0 or w > 2000 or h > 2000:
        return None
    block = data[8:]
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); px = im.load()
    for y in range(h):
        off = struct.unpack_from('<I', block, y * 4)[0] * 4
        x = 0
        while x < w and off + 4 <= len(block):
            c, run = struct.unpack_from('<HH', block, off); off += 4
            if c:
                r = ((c >> 10) & 31) * 255 // 31; g = ((c >> 5) & 31) * 255 // 31; b = (c & 31) * 255 // 31
                for k in range(run):
                    if x + k < w: px[x + k, y] = (r, g, b, 255)
            x += run
    return im

# TazUO's BwtDecompress (ClassicUO.Utility/BwtDecompress.cs at 3212623f, BSD 2-Clause), ported for compression flag 3.
def bwt_decompress(buf):
    # BuildTable's 65,536 values (every byte pair, counted from the first byte), sorted: simply 0 to 65535.
    table = list(range(65536))
    lst = bytearray(len(buf) - 4)
    i = 0
    first = buf[4]
    pos = 5
    while pos < len(buf):
        c = first
        val = table[c]
        if c > 0:
            table[1:c + 1] = table[0:c]
        table[0] = val
        lst[i] = val & 255
        i += 1
        first = buf[pos]
        pos += 1
    return bwt_internal(bytes(lst))

def bwt_internal(inp):
    part = list(struct.unpack_from('<256i', inp, 0)) + [0] * 512
    total = sum(part[:256])
    out = bytearray(total)
    sym = list(range(256))
    tmp = part[:256]
    freq = []
    for _ in range(256):
        value = 0; index = 0
        for j in range(256):
            if tmp[j] > value:
                index = j; value = tmp[j]
        if value == 0:
            break
        freq.append(index); tmp[index] = 0
    nz = sum(1 for k in range(256) if part[k] != 0)
    m = 0
    for k in range(nz):
        f = freq[k]
        sym[inp[m + 1024]] = f
        part[f + 256] = m + 1
        m += part[f]
        part[f + 512] = m
    val = sym[0]
    count = 0
    while count < total:
        out[count] = val
        if part[val + 256] >= part[val + 512]:
            if nz > 0:
                nz -= 1
                sym[0:nz] = sym[1:nz + 1]
                val = sym[0]
        else:
            idx = inp[part[val + 256] + 1024]
            part[val + 256] += 1
            if idx != 0:
                sym[0:idx] = sym[1:idx + 1]
                sym[idx] = val
                val = sym[0]
        count += 1
    return bytes(out)

FMT = [r'D:/ShatteredLegacy/player-package/vendor/fiddle-me-this/[Original]/ExternalImages/gumps',
       r'D:/ShatteredLegacy/player-package/vendor/fiddle-me-this/[Custom]/ExternalImages/gumps']

# As the shipped client sees it: the kit's PNG, else Fiddle-Me-This's (they never share an ID), else EA's own.
def gump(gid):
    p = os.path.join(VENDOR, f'{gid}.png')
    if os.path.exists(p):
        return Image.open(p).convert('RGBA'), 'kit'
    for d in FMT:
        q = os.path.join(d, f'{gid}.png')
        if os.path.exists(q):
            return Image.open(q).convert('RGBA'), 'fiddle-me-this'
    s = stock_gump(gid)
    return (s, 'stock') if s else (None, 'none')

# tiledata static entries: flags 8, weight 1, layer 1, count 4, animId 2 ... (client 7.0.9+ layout, 41 bytes)
td = open(f'{CD}/tiledata.mul', 'rb').read()
def anim_of(item):
    o = 512 * (4 + 32 * 30) + (item // 32) * (4 + 32 * 41) + 4 + (item % 32) * 41
    return struct.unpack_from('<H', td, o + 14)[0], td[o + 9]

LAYER_ORDER = [20, 5, 4, 3, 24, 19, 13, 17, 8, 14, 15, 7, 23, 22, 12, 10, 11, 16, 18, 6, 1, 2]  # cloak..twohanded
def item_gump(item, female):
    anim, layer = anim_of(item)
    for base in ((60000, 50000) if female else (50000,)):
        im, where = gump(base + anim)
        if im is not None:
            return im, where, layer, base + anim
    return None, 'none', layer, None

def doll(body, items, female, label):
    base, bw = gump(body)
    canvas = Image.new('RGBA', (300, 300), (24, 24, 24, 255))
    used = [f'body {body} ({bw})']
    canvas.alpha_composite(base, (20, 20))
    parts = []
    for it in items:
        im, where, layer, gid = item_gump(it, female)
        used.append(f'0x{it:04X} gump {gid} ({where})')
        if im is not None:
            parts.append((LAYER_ORDER.index(layer) if layer in LAYER_ORDER else 99, im))
    for _, im in sorted(parts, key=lambda t: t[0]):
        canvas.alpha_composite(im, (20, 20))
    d = ImageDraw.Draw(canvas); d.text((6, 284), label, fill=(230, 230, 230, 255))
    return canvas, used

ROBE, HAT, CLOAK = 0x1F03, 0x171A, 0x1515
PLATE = [0x1415, 0x1411, 0x1410, 0x1414, 0x1413, 0x1412]
SHIELD = 0x1B76
GARG_ROBE = 0x4000
FACE = 0x3B44  # a stock face item (anim 1000)

def sheet(name, cells):
    w = 300 * len(cells); im = Image.new('RGBA', (w, 300), (0, 0, 0, 255)); lines = []
    for i, (c, used) in enumerate(cells):
        im.alpha_composite(c, (i * 300, 0)); lines.append(' | '.join(used))
    im.convert('RGB').save(os.path.join(OUT, name), optimize=True)
    return lines

report = []
for body, female, race in [(12, False, 'human male'), (13, True, 'human female'), (14, False, 'elf male'), (15, True, 'elf female')]:
    cells = [doll(body, [], female, f'{race}: naked'),
             doll(body, [ROBE, HAT, CLOAK], female, f'{race}: robe, hat, cloak'),
             doll(body, PLATE + [SHIELD], female, f'{race}: plate, shield'),
             doll(body, [FACE], female, f'{race}: stock face item')]
    report += [f'{race}: ' + l for l in sheet(f'{race.replace(" ", "-")}.png', cells)]
for body, female, race in [(665, False, 'gargoyle male'), (666, True, 'gargoyle female')]:
    cells = [doll(body, [], female, f'{race}: naked'),
             doll(body, [GARG_ROBE], female, f'{race}: gargish robe'),
             doll(body, [FACE], female, f'{race}: stock face item')]
    report += [f'{race}: ' + l for l in sheet(f'{race.replace(" ", "-")}.png', cells)]

# The background with TazUO's stock button positions (x 185) beside the kit's layout (x 197).
bg, bgw = gump(2000)
for name, bx in (('background-tazuo-positions.png', 185), ('background-kit-positions.png', 197)):
    im = Image.new('RGBA', (bg.width + 40, bg.height + 40), (24, 24, 24, 255)); im.alpha_composite(bg, (20, 20))
    for k, g in enumerate((2006, 2009, 2012, 2015, 22450, 2024, 2027)):
        b, _ = gump(g)
        if b is not None: im.alpha_composite(b, (20 + bx, 20 + 44 + 27 * (k + 1 if k else 1) - (27 if k == 0 else 0)))
    im.convert('RGB').save(os.path.join(OUT, name), optimize=True)
report.append(f'background 2000 ({bgw})')

# Spell icons: the first 16 the kit upgrades, kit beside stock.
sp = sorted(int(f[:-4]) for f in os.listdir(r"D:/ShatteredLegacy/design/paperdoll-sos/kit/UO Paperdoll Project (Siege of Sosaria) Builder's Kit/PNGs/#03 Spell Icon Upgrades") if f.endswith('.png'))[:16]
im = Image.new('RGBA', (16 * 50, 110), (24, 24, 24, 255))
for i, g in enumerate(sp):
    k, _ = gump(g); s = stock_gump(g)
    if k: k.thumbnail((44, 44)); im.alpha_composite(k, (i * 50 + 3, 3))
    if s: s.thumbnail((44, 44)); im.alpha_composite(s, (i * 50 + 3, 58))
im.convert('RGB').save(os.path.join(OUT, 'spell-icons-kit-top-stock-bottom.png'), optimize=True)
report.append('spell icons ' + ', '.join(map(str, sp)))
open(os.path.join(OUT, 'render-report.txt'), 'w', newline='\n').write('\n'.join(report) + '\n')
print('\n'.join(report))
