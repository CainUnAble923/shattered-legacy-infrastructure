from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
REF = ROOT / "reference" / "8x"
OUT = ROOT / "stage2-8x"

GAME_CANVAS = (260, 237)
SCALE = 8
CANVAS_8X = (2080, 1896)

# Natural palette sampled and simplified from the finalized icon_44.png.
OUTLINE = (74, 41, 32, 255)
DEEP = (98, 57, 41, 255)
SHADOW = (156, 32, 16, 255)
EDGE_ORANGE = (205, 41, 16, 255)
FOX = (246, 65, 8, 255)
FOX_LIGHT = (255, 98, 24, 255)
CREAM_SHADOW = (230, 164, 123, 255)
CREAM = (246, 205, 172, 255)
CREAM_LIGHT = (255, 222, 189, 255)


def load_black_key(path: Path) -> Image.Image:
    """Convert the reference gump's black color key to real transparency."""
    src = Image.open(path).convert("RGB")
    dst = Image.new("RGBA", src.size, (0, 0, 0, 0))
    src_px = src.load()
    dst_px = dst.load()
    for y in range(src.height):
        for x in range(src.width):
            r, g, b = src_px[x, y]
            if (r, g, b) != (0, 0, 0):
                dst_px[x, y] = (r, g, b, 255)
    return dst


def draw_ears_1x() -> Image.Image:
    layer = Image.new("RGBA", GAME_CANVAS, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    ears = [
        ([(75, 35), (81, 7), (94, 31)], [(80, 30), (83, 14), (90, 29)]),
        ([(96, 31), (108, 6), (113, 36)], [(101, 29), (107, 13), (110, 31)]),
    ]

    for outer, inner in ears:
        d.polygon(outer, fill=OUTLINE)
        tip = outer[1]
        d.polygon([tip, (tip[0] - 2, tip[1] + 7), (tip[0] + 3, tip[1] + 7)], fill=DEEP)
        d.polygon(
            [(outer[0][0] + 2, outer[0][1] - 1), (tip[0], tip[1] + 4),
             (outer[2][0] - 2, outer[2][1] - 1)],
            fill=FOX,
        )
        d.line([(outer[0][0] + 2, outer[0][1] - 2), (tip[0] - 1, tip[1] + 6)],
               fill=FOX_LIGHT, width=1)
        d.polygon(inner, fill=CREAM_SHADOW)
        d.polygon(
            [(inner[0][0] + 1, inner[0][1] - 1), (inner[1][0], inner[1][1] + 3),
             (inner[2][0] - 1, inner[2][1] - 1)],
            fill=CREAM,
        )
        d.line([(inner[1][0], inner[1][1] + 3), (inner[2][0] - 1, inner[2][1] - 1)],
               fill=CREAM_LIGHT, width=1)
        # Chunky one-pixel-at-game-size tufts at the hairline.
        d.point((outer[0][0] - 1, outer[0][1] + 2), fill=FOX_LIGHT)
        d.point((outer[2][0] + 1, outer[2][1] + 2), fill=EDGE_ORANGE)
    return layer


def draw_tail_1x() -> Image.Image:
    layer = Image.new("RGBA", GAME_CANVAS, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    # A fuller version of Moonrunner's long, low crescent. The right-hand root is later masked.
    outer = [
        (110, 110), (100, 107), (90, 109), (80, 114), (70, 122), (61, 132),
        (52, 140), (43, 145), (33, 148), (24, 147), (17, 143), (12, 138),
        (9, 142), (11, 150), (18, 156), (28, 160), (40, 160), (52, 156),
        (64, 149), (75, 140), (85, 130), (94, 124), (103, 122), (110, 125),
    ]
    inner = [
        (105, 114), (96, 112), (87, 115), (78, 121), (68, 130), (59, 139),
        (49, 147), (39, 152), (30, 153), (22, 150), (16, 145), (15, 149),
        (21, 154), (30, 157), (40, 156), (51, 152), (62, 145), (73, 136),
        (84, 126), (94, 120), (104, 118),
    ]
    cream_tip = [
        (9, 142), (12, 138), (17, 143), (24, 147), (33, 148), (39, 152),
        (34, 157), (28, 160), (18, 156), (11, 150),
    ]
    d.polygon(outer, fill=OUTLINE)
    d.polygon(inner, fill=EDGE_ORANGE)

    # Broad fur bands, then medium and small clusters, all at least one game pixel wide.
    d.polygon(
        [(103, 115), (91, 116), (80, 123), (68, 134), (56, 144), (45, 151),
         (38, 153), (47, 151), (59, 146), (71, 138), (82, 128), (94, 121), (105, 119)],
        fill=FOX,
    )
    d.polygon(
        [(96, 113), (85, 118), (74, 126), (63, 136), (53, 144), (45, 148),
         (54, 144), (65, 137), (76, 128), (87, 120), (98, 116)],
        fill=FOX_LIGHT,
    )
    d.line([(101, 122), (89, 126), (77, 136), (65, 146), (53, 152)],
           fill=SHADOW, width=2)
    d.line([(92, 117), (81, 124), (70, 134), (60, 141)], fill=CREAM_LIGHT, width=1)

    d.polygon(cream_tip, fill=CREAM_SHADOW)
    d.polygon(
        [(12, 142), (18, 146), (25, 150), (34, 152), (30, 157), (23, 156),
         (16, 152), (12, 148)],
        fill=CREAM,
    )
    d.line([(14, 145), (21, 151), (29, 153)], fill=CREAM_LIGHT, width=1)

    # Edge breaks and tiny fur tips that still survive the exact 8:1 shrink.
    for point, color in (
        ((18, 143), CREAM_LIGHT), ((29, 148), CREAM), ((42, 146), FOX_LIGHT),
        ((53, 140), FOX_LIGHT), ((64, 131), FOX_LIGHT), ((75, 121), FOX_LIGHT),
        ((86, 113), DEEP), ((40, 159), DEEP), ((52, 155), SHADOW),
    ):
        d.point(point, fill=color)
    return layer


def mask_hidden_tail(tail_8x: Image.Image, kimono_8x: Image.Image) -> None:
    """Remove every tail pixel covered by the exact supplied body silhouette."""
    tail_px = tail_8x.load()
    body_alpha = kimono_8x.getchannel("A").load()
    for y in range(CANVAS_8X[1]):
        for x in range(CANVAS_8X[0]):
            if body_alpha[x, y] != 0:
                tail_px[x, y] = (0, 0, 0, 0)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    kimono = load_black_key(REF / "paperdoll_Gump_60559_(Female_Kimono)_8x.png")
    hair = load_black_key(REF / "paperdoll_Gump_60701_(Long_Hair)_8x.png")

    ears = draw_ears_1x().resize(CANVAS_8X, Image.Resampling.NEAREST)
    tail = draw_tail_1x().resize(CANVAS_8X, Image.Resampling.NEAREST)
    mask_hidden_tail(tail, kimono)

    wearable = Image.new("RGBA", CANVAS_8X, (0, 0, 0, 0))
    wearable.alpha_composite(tail)
    wearable.alpha_composite(ears)
    wearable.save(OUT / "moonrunner_paperdoll_8x.png", optimize=False)

    # Fit preview: tail behind the body; hair/body above it; ears above the hair.
    preview = Image.new("RGBA", CANVAS_8X, (0, 0, 0, 0))
    preview.alpha_composite(tail)
    preview.alpha_composite(kimono)
    preview.alpha_composite(hair)
    preview.alpha_composite(ears)
    preview.save(OUT / "moonrunner_paperdoll_8x_preview_overlay.png", optimize=False)


if __name__ == "__main__":
    main()
