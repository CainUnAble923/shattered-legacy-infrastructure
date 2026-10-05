from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
REF = ROOT / "reference"
OUT = ROOT / "stage1"

CANVAS = (260, 237)

# All accessory colors are multiples of eight, keeping them inside a 5-bit-per-channel palette.
OUTLINE = (48, 32, 24, 255)
DEEP = (96, 40, 24, 255)
SHADOW = (160, 48, 16, 255)
FOX = (224, 72, 24, 255)
FOX_LIGHT = (248, 120, 48, 255)
CREAM_SHADOW = (208, 176, 136, 255)
CREAM = (248, 224, 184, 255)
CREAM_LIGHT = (248, 240, 216, 255)


def load_uo_gump(path: Path) -> Image.Image:
    """Convert UO's black color key to real PNG transparency."""
    src = Image.open(path).convert("RGB")
    dst = Image.new("RGBA", src.size, (0, 0, 0, 0))
    src_px = src.load()
    dst_px = dst.load()
    for y in range(src.height):
        for x in range(src.width):
            color = src_px[x, y]
            if color != (0, 0, 0):
                dst_px[x, y] = (*color, 255)
    return dst


def draw_ears(layer: Image.Image, variant: str) -> None:
    d = ImageDraw.Draw(layer)
    if variant == "a":
        ears = [
            ([(76, 35), (80, 13), (93, 31)], [(80, 30), (82, 19), (89, 30)]),
            ([(96, 31), (107, 12), (111, 36)], [(100, 30), (105, 19), (107, 31)]),
        ]
    elif variant == "b":
        ears = [
            ([(74, 36), (79, 18), (93, 32)], [(79, 31), (81, 23), (89, 31)]),
            ([(96, 32), (108, 17), (113, 37)], [(101, 31), (107, 22), (109, 32)]),
        ]
    else:
        ears = [
            ([(76, 34), (81, 8), (93, 30)], [(81, 29), (83, 15), (89, 29)]),
            ([(96, 30), (108, 7), (112, 35)], [(101, 29), (107, 14), (109, 30)]),
        ]

    for outer, inner in ears:
        d.polygon(outer, fill=OUTLINE)
        # A one-pixel dark tip remains visible above the orange body.
        tip = outer[1]
        d.polygon([tip, (tip[0] - 2, tip[1] + 6), (tip[0] + 3, tip[1] + 6)], fill=DEEP)
        orange = [(outer[0][0] + 2, outer[0][1] - 1), (tip[0], tip[1] + 4), (outer[2][0] - 2, outer[2][1] - 1)]
        d.polygon(orange, fill=FOX)
        d.polygon(inner, fill=CREAM_SHADOW)
        d.line(inner[1:], fill=CREAM, width=1)
        d.point((inner[1][0], inner[1][1] + 2), fill=CREAM_LIGHT)
        # Small hard-edged tufts where the ear meets the hair.
        d.line([outer[0], (outer[0][0] - 2, outer[0][1] + 3)], fill=FOX_LIGHT, width=1)
        d.line([outer[2], (outer[2][0] + 2, outer[2][1] + 3)], fill=SHADOW, width=1)


def tail_layer(variant: str) -> Image.Image:
    layer = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    if variant == "a":
        outer = [(106, 111), (94, 111), (84, 117), (73, 126), (61, 134), (49, 137),
                 (38, 134), (30, 128), (26, 120), (22, 124), (22, 136), (28, 146),
                 (39, 153), (52, 155), (66, 151), (79, 143), (91, 135), (103, 130)]
        inner = [(103, 116), (92, 117), (81, 124), (69, 134), (55, 142), (43, 143),
                 (34, 139), (29, 132), (31, 142), (41, 148), (53, 149), (66, 145),
                 (80, 137), (93, 128), (104, 125)]
        tip = [(22, 124), (26, 120), (30, 128), (38, 134), (49, 137), (55, 142),
               (43, 143), (34, 139), (29, 132), (31, 142), (28, 146), (22, 136)]
        highlights = [[(58, 143), (72, 136), (87, 125)], [(73, 130), (88, 119), (100, 117)]]
    elif variant == "b":
        outer = [(106, 124), (96, 119), (87, 110), (79, 99), (68, 89), (56, 84),
                 (43, 86), (32, 94), (27, 103), (22, 98), (18, 105), (22, 114),
                 (32, 120), (42, 118), (50, 110), (52, 101), (48, 94), (39, 91),
                 (29, 95), (24, 105), (25, 119), (33, 133), (45, 143), (59, 148),
                 (73, 145), (86, 137), (98, 132), (106, 131)]
        inner = [(101, 124), (92, 119), (84, 109), (76, 99), (67, 94), (58, 91),
                 (50, 93), (45, 98), (46, 105), (41, 111), (34, 114), (29, 111),
                 (31, 122), (39, 134), (50, 141), (61, 143), (73, 140), (86, 132), (99, 128)]
        tip = [(56, 84), (43, 86), (32, 94), (27, 103), (22, 98), (18, 105),
               (22, 114), (32, 120), (42, 118), (50, 110), (52, 101), (48, 94)]
        highlights = [[(42, 128), (52, 138), (65, 140)], [(67, 135), (83, 126), (98, 125)]]
    else:
        outer = [(106, 110), (94, 108), (81, 113), (68, 122), (56, 133), (44, 142),
                 (31, 147), (20, 146), (13, 140), (10, 145), (14, 153), (24, 159),
                 (38, 159), (52, 154), (65, 146), (77, 136), (88, 127), (99, 123), (107, 124)]
        inner = [(102, 114), (93, 113), (82, 117), (70, 126), (58, 138), (45, 148),
                 (33, 152), (24, 151), (17, 147), (18, 153), (27, 156), (38, 155),
                 (51, 150), (64, 142), (77, 131), (88, 122), (101, 118)]
        tip = [(10, 145), (13, 140), (20, 146), (31, 147), (38, 152), (33, 157),
               (24, 159), (14, 153)]
        highlights = [[(43, 151), (57, 144), (71, 133)], [(65, 137), (80, 123), (96, 115)]]

    d.polygon(outer, fill=OUTLINE)
    d.polygon(inner, fill=FOX)
    d.polygon(tip, fill=CREAM_SHADOW)
    # Layered, directional fur marks: broad shadow, medium orange, small light tufts.
    d.line([(101, 122), (88, 128), (76, 139), (61, 147)], fill=SHADOW, width=2)
    for line in highlights:
        d.line(line, fill=FOX_LIGHT, width=1)
    if variant == "b":
        d.line([(65, 92), (73, 99), (79, 108)], fill=FOX_LIGHT, width=2)
        d.line([(29, 106), (35, 110), (42, 108)], fill=CREAM, width=1)
    elif variant == "a":
        d.line([(27, 132), (34, 139), (43, 141)], fill=CREAM, width=1)
    else:
        d.line([(18, 148), (26, 153), (35, 153)], fill=CREAM, width=1)
    # Small one- and two-pixel fur breaks along the silhouette.
    for x, y in outer[2:-2:3]:
        d.point((x, y), fill=FOX_LIGHT if y % 2 else DEEP)
    return layer


def build(variant: str, name: str, kimono: Image.Image, hair: Image.Image) -> None:
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(tail_layer(variant))
    canvas.alpha_composite(kimono)
    canvas.alpha_composite(hair)
    ears = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    draw_ears(ears, variant)
    canvas.alpha_composite(ears)

    path = OUT / f"{name}_260x237.png"
    canvas.save(path, optimize=False)
    preview = canvas.resize((CANVAS[0] * 4, CANVAS[1] * 4), Image.Resampling.NEAREST)
    preview.save(OUT / f"{name}_preview_4x.png", optimize=False)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    kimono = load_uo_gump(REF / "paperdoll_Gump_60559_(Female_Kimono).png")
    hair = load_uo_gump(REF / "paperdoll_Gump_60701_(Long_Hair).png")
    build("a", "concept_a_balanced_vixen", kimono, hair)
    build("b", "concept_b_hearth_plume", kimono, hair)
    build("c", "concept_c_moonrunner", kimono, hair)


if __name__ == "__main__":
    main()
