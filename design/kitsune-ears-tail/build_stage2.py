from pathlib import Path

from PIL import Image, ImageDraw

from build_stage1 import (
    CANVAS,
    CREAM,
    CREAM_LIGHT,
    CREAM_SHADOW,
    DEEP,
    FOX,
    FOX_LIGHT,
    OUTLINE,
    SHADOW,
    draw_ears,
    load_uo_gump,
    tail_layer,
)


ROOT = Path(__file__).resolve().parent
REF = ROOT / "reference"
OUT = ROOT / "stage2"

GRAY_MAP = {
    OUTLINE: (48, 48, 48, 255),
    DEEP: (64, 64, 64, 255),
    SHADOW: (88, 88, 88, 255),
    FOX: (136, 136, 136, 255),
    FOX_LIGHT: (184, 184, 184, 255),
    CREAM_SHADOW: (200, 200, 200, 255),
    CREAM: (232, 232, 232, 255),
    CREAM_LIGHT: (248, 248, 248, 255),
}


def grayscale_accessory(image: Image.Image) -> Image.Image:
    """Map the hand-authored natural palette to a hueable UO grayscale ramp."""
    result = Image.new("RGBA", image.size, (0, 0, 0, 0))
    src = image.load()
    dst = result.load()
    for y in range(image.height):
        for x in range(image.width):
            color = src[x, y]
            if color[3] == 0:
                continue
            dst[x, y] = GRAY_MAP.get(color, (128, 128, 128, 255))
    return result


def visible_paperdoll_accessory(kimono: Image.Image) -> Image.Image:
    """Build ears plus only the tail pixels visible past the real body silhouette."""
    tail = tail_layer("c")
    tail_px = tail.load()
    body_alpha = kimono.getchannel("A").load()
    for y in range(CANVAS[1]):
        for x in range(CANVAS[0]):
            if body_alpha[x, y] != 0:
                tail_px[x, y] = (0, 0, 0, 0)

    ears = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    draw_ears(ears, "c")
    tail.alpha_composite(ears)
    return tail


def item_icon() -> Image.Image:
    """44x44 inventory bundle: the approved tall ears above one curled tail."""
    icon = Image.new("RGBA", (44, 44), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon)

    # Tall Moonrunner ears.
    left_outer = [(8, 19), (12, 2), (20, 18)]
    right_outer = [(23, 18), (32, 2), (35, 20)]
    for outer, inner in (
        (left_outer, [(11, 16), (13, 7), (17, 16)]),
        (right_outer, [(26, 16), (31, 7), (32, 17)]),
    ):
        d.polygon(outer, fill=OUTLINE)
        tip = outer[1]
        d.polygon([tip, (tip[0] - 1, tip[1] + 4), (tip[0] + 2, tip[1] + 4)], fill=DEEP)
        d.polygon([(outer[0][0] + 2, outer[0][1] - 1), (tip[0], tip[1] + 3),
                   (outer[2][0] - 2, outer[2][1] - 1)], fill=FOX)
        d.polygon(inner, fill=CREAM_SHADOW)
        d.line(inner[1:], fill=CREAM, width=1)

    # One compact crescent tail. Its narrow left end is the root; the pale raised end is the tip.
    tail_outer = [(7, 23), (5, 27), (6, 33), (11, 38), (18, 41), (26, 41),
                  (34, 38), (39, 32), (41, 26), (39, 21), (35, 18), (30, 18),
                  (28, 21), (32, 22), (35, 25), (35, 29), (32, 32), (27, 34),
                  (21, 34), (16, 32), (13, 29), (12, 25)]
    tail_inner = [(8, 26), (8, 31), (12, 35), (18, 38), (25, 38), (32, 35),
                  (36, 31), (38, 26), (36, 23), (33, 21), (30, 21), (33, 24),
                  (33, 28), (30, 31), (25, 32), (20, 32), (16, 30), (13, 26)]
    tail_tip = [(30, 18), (35, 18), (39, 21), (41, 26), (39, 31), (36, 33),
                (32, 31), (35, 29), (35, 25), (32, 22), (28, 21)]
    d.polygon(tail_outer, fill=OUTLINE)
    d.polygon(tail_inner, fill=FOX)
    d.polygon(tail_tip, fill=CREAM_SHADOW)
    d.line([(9, 31), (15, 36), (23, 38), (30, 35)], fill=SHADOW, width=1)
    d.line([(14, 27), (19, 30), (25, 30)], fill=FOX_LIGHT, width=1)
    d.line([(34, 21), (38, 24), (38, 28)], fill=CREAM, width=1)
    d.point((37, 22), fill=CREAM_LIGHT)
    return icon


def save_asset(image: Image.Image, stem: str) -> None:
    image.save(OUT / f"{stem}.png", optimize=False)
    preview = image.resize((image.width * 4, image.height * 4), Image.Resampling.NEAREST)
    preview.save(OUT / f"{stem}_preview_4x.png", optimize=False)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    kimono = load_uo_gump(REF / "paperdoll_Gump_60559_(Female_Kimono).png")

    natural_gump = visible_paperdoll_accessory(kimono)
    gray_gump = grayscale_accessory(natural_gump)
    natural_icon = item_icon()
    gray_icon = grayscale_accessory(natural_icon)

    save_asset(natural_gump, "moonrunner_paperdoll_natural_260x237")
    save_asset(gray_gump, "moonrunner_paperdoll_grayscale_260x237")
    save_asset(natural_icon, "moonrunner_item_icon_natural_44x44")
    save_asset(gray_icon, "moonrunner_item_icon_grayscale_44x44")


if __name__ == "__main__":
    main()
