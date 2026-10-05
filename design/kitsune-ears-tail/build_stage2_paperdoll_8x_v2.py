from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
REF = ROOT / "reference" / "8x"
OUT = ROOT / "stage2-8x"
STUDY = Path(
    r"C:\Users\ckfle\.codex\generated_images\01a10c6b-a3f6-7521-b745-759215e756b2"
    r"\exec-2a02b480-9de9-46f3-a570-d2cfaf64358f.png"
)

GAME_CANVAS = (260, 237)
SCALE = 8
CANVAS_8X = (2080, 1896)

# Component bounds in the corrected study, established from its opaque connected components.
LEFT_EAR_BBOX = (357, 25, 506, 219)
RIGHT_EAR_BBOX = (521, 25, 669, 219)
TAIL_BBOX = (41, 714, 653, 1125)

DARK_REPLACEMENT = (58, 34, 27, 255)


def load_black_key(path: Path) -> Image.Image:
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


def study_component(study: Image.Image, bbox: tuple[int, int, int, int], size: tuple[int, int]) -> Image.Image:
    """Reduce a study component to game pixels, quantize it, and force binary alpha."""
    crop = study.crop(bbox).resize(size, Image.Resampling.NEAREST)
    alpha = crop.getchannel("A").point(lambda a: 255 if a >= 128 else 0)

    # A chroma-key background reserves one palette entry; the remaining 24 hold fur shades.
    keyed = Image.new("RGB", size, (0, 255, 0))
    keyed.paste(crop.convert("RGB"), (0, 0), alpha)
    quantized = keyed.quantize(
        colors=25,
        method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE,
    ).convert("RGB")

    result = Image.new("RGBA", size, (0, 0, 0, 0))
    src_px = quantized.load()
    alpha_px = alpha.load()
    dst_px = result.load()
    for y in range(size[1]):
        for x in range(size[0]):
            if alpha_px[x, y] == 0:
                continue
            r, g, b = src_px[x, y]
            # The study's deepest outline sometimes quantizes to black; keep it visible in UO.
            if r < 18 and g < 18 and b < 18:
                dst_px[x, y] = DARK_REPLACEMENT
            else:
                dst_px[x, y] = (r, g, b, 255)
    return result


def mask_hidden_tail(tail_8x: Image.Image, kimono_8x: Image.Image) -> None:
    tail_px = tail_8x.load()
    body_alpha = kimono_8x.getchannel("A").load()
    for y in range(CANVAS_8X[1]):
        for x in range(CANVAS_8X[0]):
            if body_alpha[x, y] != 0:
                tail_px[x, y] = (0, 0, 0, 0)


def upscale_component(component: Image.Image, position: tuple[int, int]) -> Image.Image:
    layer_1x = Image.new("RGBA", GAME_CANVAS, (0, 0, 0, 0))
    layer_1x.alpha_composite(component, position)
    return layer_1x.resize(CANVAS_8X, Image.Resampling.NEAREST)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    study = Image.open(STUDY).convert("RGBA")
    kimono = load_black_key(REF / "paperdoll_Gump_60559_(Female_Kimono)_8x.png")
    hair = load_black_key(REF / "paperdoll_Gump_60701_(Long_Hair)_8x.png")

    # Ears are 23 game pixels tall versus the hair/head's roughly 44-pixel height.
    left_ear = study_component(study, LEFT_EAR_BBOX, (17, 23))
    right_ear = study_component(study, RIGHT_EAR_BBOX, (17, 23))
    ears_1x = Image.new("RGBA", GAME_CANVAS, (0, 0, 0, 0))
    ears_1x.alpha_composite(left_ear, (75, 7))
    ears_1x.alpha_composite(right_ear, (96, 7))
    ears_8x = ears_1x.resize(CANVAS_8X, Image.Resampling.NEAREST)

    # Preserve the study's broad 3:2 plume and large cream tip at a 96x64 game-pixel footprint.
    tail_1x = study_component(study, TAIL_BBOX, (96, 64))
    tail_8x = upscale_component(tail_1x, (6, 100))
    mask_hidden_tail(tail_8x, kimono)

    wearable = Image.new("RGBA", CANVAS_8X, (0, 0, 0, 0))
    wearable.alpha_composite(tail_8x)
    wearable.alpha_composite(ears_8x)
    wearable.save(OUT / "moonrunner_paperdoll_8x.png", optimize=False)

    preview = Image.new("RGBA", CANVAS_8X, (0, 0, 0, 0))
    preview.alpha_composite(tail_8x)
    preview.alpha_composite(kimono)
    preview.alpha_composite(hair)
    preview.alpha_composite(ears_8x)
    preview.save(OUT / "moonrunner_paperdoll_8x_preview_overlay.png", optimize=False)


if __name__ == "__main__":
    main()
