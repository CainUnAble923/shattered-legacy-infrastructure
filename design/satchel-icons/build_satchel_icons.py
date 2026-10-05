from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
GENERATED = Path(r"C:\Users\ckfle\.codex\generated_images\01a10c4d-d71f-7ed1-92a7-9776cd8a28b6")

SOURCES = {
    "ore_satchel": GENERATED / "exec-4d2d35bb-7b12-44d4-bdaf-68892159fa3c.png",
    "lumber_satchel": GENERATED / "exec-3cfbfa6b-c08e-479c-9de0-61c9bb1c81bd.png",
    "hunters_satchel": GENERATED / "exec-cdaa1fe5-3f80-48bd-9346-13d5a2714cc2.png",
}

CANVAS = (44, 40)
ART_SIZE = (24, 30)


def alpha_bbox(image: Image.Image, threshold: int = 24) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A").point(lambda a: 255 if a >= threshold else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError("Source image has no visible pixels")
    return bbox


def is_low_chroma(r: int, g: int, b: int, tolerance: int = 24) -> bool:
    return max(r, g, b) - min(r, g, b) <= tolerance


def quantize_channel(value: int) -> int:
    return max(0, min(255, round(value / 12) * 12))


def make_sprite(name: str, source_path: Path) -> Image.Image:
    source = Image.open(source_path).convert("RGBA")
    source = source.crop(alpha_bbox(source))
    source = source.resize(ART_SIZE, Image.Resampling.LANCZOS)

    sprite = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    offset = ((CANVAS[0] - ART_SIZE[0]) // 2, (CANVAS[1] - ART_SIZE[1]) // 2)

    pixels = []
    for y in range(ART_SIZE[1]):
        for x in range(ART_SIZE[0]):
            r, g, b, a = source.getpixel((x, y))
            if a < 72:
                pixels.append((0, 0, 0, 0))
                continue

            r, g, b = map(quantize_channel, (r, g, b))
            low_chroma = is_low_chroma(r, g, b)

            if name == "ore_satchel":
                # The ore occupies the open upper bowl. Only it is tint-neutral.
                tint_region = 4 <= x <= 20 and y <= 18 and low_chroma
            else:
                # Generated leather is neutral; natural contents and brass are chromatic.
                tint_region = low_chroma

            if tint_region:
                gray = quantize_channel(round((r + g + b) / 3))
                r = g = b = gray
            elif r == g == b:
                # Preserve a dark-looking outline without accidentally entering the hue mask.
                if name == "ore_satchel":
                    r = min(255, r + 8)
                    b = max(0, b - 4)
                else:
                    r = min(255, r + 4)
                    b = max(0, b - 4)

            pixels.append((r, g, b, 255))

    art = Image.new("RGBA", ART_SIZE, (0, 0, 0, 0))
    art.putdata(pixels)
    sprite.alpha_composite(art, offset)
    return sprite


def write_preview(sprite: Image.Image, destination: Path) -> None:
    sprite.resize((CANVAS[0] * 4, CANVAS[1] * 4), Image.Resampling.NEAREST).save(destination)


def main() -> None:
    sprites = {}
    for name, source in SOURCES.items():
        sprite = make_sprite(name, source)
        sprites[name] = sprite
        sprite.save(ROOT / f"{name}.png")
        write_preview(sprite, ROOT / f"{name}_4x.png")

    family = Image.new("RGBA", (CANVAS[0] * 3, CANVAS[1]), (0, 0, 0, 0))
    for index, name in enumerate(SOURCES):
        family.alpha_composite(sprites[name], (CANVAS[0] * index, 0))
    family.resize((CANVAS[0] * 3 * 4, CANVAS[1] * 4), Image.Resampling.NEAREST).save(
        ROOT / "satchel_family_4x.png"
    )

    for name, sprite in sprites.items():
        opaque = [px for px in sprite.getdata() if px[3]]
        neutral = sum(1 for r, g, b, _ in opaque if r == g == b)
        print(f"{name}: {sprite.size}, opaque={len(opaque)}, pure_gray={neutral}")


if __name__ == "__main__":
    main()
