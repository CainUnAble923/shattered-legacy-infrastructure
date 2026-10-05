from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
GENERATED = Path(r"C:\Users\ckfle\.codex\generated_images\01a10c4d-d71f-7ed1-92a7-9776cd8a28b6")

SOURCES = {
    "arcane_society_reagent_pouch": GENERATED / "exec-c8c696c8-a8ec-4d62-91db-e884fcd03223.png",
    "tailors_cloth_satchel": GENERATED / "exec-6311a251-18af-4f65-9090-611476cc2cd6.png",
    "maritime_fishers_creel": GENERATED / "exec-4dc323eb-65f8-4fd0-96b9-acdd5af73bc4.png",
    "healers_covenant_satchel": GENERATED / "exec-55fe2170-20e7-4476-abb4-72a8881485ce.png",
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


def tint_region(name: str, x: int, y: int, r: int, g: int, b: int) -> bool:
    if not low_chroma(r, g, b):
        return False
    if name == "maritime_fishers_creel":
        # The generated creel is neutral wicker; fish and strap remain chromatic.
        return True
    if name == "tailors_cloth_satchel":
        # Exclude the blue-steel shears on the lower-right edge.
        return (y >= 14 and x <= 17) or x <= 4 or (x >= 20 and y <= 13)
    # Gray container; keep upper-center ingredients/bandages/potion chromatic.
    return y >= 14 or x <= 5 or x >= 20


def build_sprite(name: str, source_path: Path) -> Image.Image:
    source = Image.open(source_path).convert("RGBA")
    source = source.crop(alpha_bbox(source)).resize(ART_SIZE, Image.Resampling.LANCZOS)
    pixels = []

    for y in range(ART_SIZE[1]):
        for x in range(ART_SIZE[0]):
            r, g, b, a = source.getpixel((x, y))
            if a < 72:
                pixels.append((0, 0, 0, 0))
                continue

            r, g, b = map(quantize, (r, g, b))
            if tint_region(name, x, y, r, g, b):
                gray = quantize(round((r + g + b) / 3))
                r = g = b = gray
            elif r == g == b:
                # Keep non-container pixels out of the game's neutral hue mask.
                r = min(255, r + 8)
                b = max(0, b - 4)
            pixels.append((r, g, b, 255))

    art = Image.new("RGBA", ART_SIZE, (0, 0, 0, 0))
    art.putdata(pixels)
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

    family = Image.new("RGBA", (44 * len(SOURCES), 40), (0, 0, 0, 0))
    for index, name in enumerate(SOURCES):
        family.alpha_composite(sprites[name], (44 * index, 0))
    family.resize((176 * len(SOURCES), 160), Image.Resampling.NEAREST).save(
        ROOT / "satchel_family_set3_4x.png"
    )

    for name, sprite in sprites.items():
        opaque = [pixel for pixel in sprite.getdata() if pixel[3]]
        neutral = sum(1 for r, g, b, _ in opaque if r == g == b)
        print(f"{name}: canvas={sprite.size}, bbox={sprite.getchannel('A').getbbox()}, pure_gray={neutral}")


if __name__ == "__main__":
    main()
