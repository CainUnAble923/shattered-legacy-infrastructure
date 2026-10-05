from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
GENERATED = Path(r"C:\Users\ckfle\.codex\generated_images\01a10c4d-d71f-7ed1-92a7-9776cd8a28b6")

SOURCES = {
    "artificers_satchel": GENERATED / "exec-cb13f176-b8ab-4128-ac8c-6b66343e8c7d.png",
    "gemologists_satchel": GENERATED / "exec-10b99f27-5bf6-4154-baaf-96cace549d6d.png",
    "smith_guild_salvage_bag": GENERATED / "exec-c7a52233-7848-49bc-b54a-9ccb2abd04ba.png",
}

CANVAS = (44, 40)
ART_SIZE = (24, 30)


def alpha_bbox(image: Image.Image, threshold: int = 24) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A").point(lambda value: 255 if value >= threshold else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError("Source image has no visible pixels")
    return bbox


def quantize(value: int) -> int:
    return max(0, min(255, round(value / 12) * 12))


def low_chroma(r: int, g: int, b: int) -> bool:
    return max(r, g, b) - min(r, g, b) <= 24


def is_tint_region(name: str, x: int, y: int, r: int, g: int, b: int) -> bool:
    if not low_chroma(r, g, b):
        return False
    if name == "artificers_satchel":
        # Gray outer leather; keep pale and luminous ingredients chromatic.
        return y >= 12 or x <= 5 or x >= 19
    if name == "gemologists_satchel":
        # Gray outer satchel; exclude the colored inset pouch and gems.
        return y >= 15 or x <= 5 or x >= 20
    # Salvage metal and blade fragments occupy the open upper half.
    return y <= 18 and 4 <= x <= 20


def build_sprite(name: str, source_path: Path) -> Image.Image:
    source = Image.open(source_path).convert("RGBA")
    source = source.crop(alpha_bbox(source)).resize(ART_SIZE, Image.Resampling.LANCZOS)
    art = Image.new("RGBA", ART_SIZE, (0, 0, 0, 0))

    output = []
    for y in range(ART_SIZE[1]):
        for x in range(ART_SIZE[0]):
            r, g, b, a = source.getpixel((x, y))
            if a < 72:
                output.append((0, 0, 0, 0))
                continue

            r, g, b = map(quantize, (r, g, b))
            if is_tint_region(name, x, y, r, g, b):
                gray = quantize(round((r + g + b) / 3))
                r = g = b = gray
            elif r == g == b:
                # Nudge accidental neutrals outside the intended game hue mask.
                r = min(255, r + 8)
                b = max(0, b - 4)
            output.append((r, g, b, 255))

    art.putdata(output)
    sprite = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    sprite.alpha_composite(art, ((CANVAS[0] - ART_SIZE[0]) // 2, (CANVAS[1] - ART_SIZE[1]) // 2))
    return sprite


def main() -> None:
    sprites = {}
    for name, source in SOURCES.items():
        sprite = build_sprite(name, source)
        sprites[name] = sprite
        sprite.save(ROOT / f"{name}.png")
        sprite.resize((176, 160), Image.Resampling.NEAREST).save(ROOT / f"{name}_4x.png")

    family = Image.new("RGBA", (132, 40), (0, 0, 0, 0))
    for index, name in enumerate(SOURCES):
        family.alpha_composite(sprites[name], (44 * index, 0))
    family.resize((528, 160), Image.Resampling.NEAREST).save(ROOT / "satchel_family_set2_4x.png")

    for name, sprite in sprites.items():
        opaque = [pixel for pixel in sprite.getdata() if pixel[3]]
        neutral = sum(1 for r, g, b, _ in opaque if r == g == b)
        print(f"{name}: canvas={sprite.size}, bbox={sprite.getchannel('A').getbbox()}, pure_gray={neutral}")


if __name__ == "__main__":
    main()
