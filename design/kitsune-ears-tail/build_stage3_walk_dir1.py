from pathlib import Path
from statistics import mean

from PIL import Image


ROOT = Path(__file__).resolve().parent
APPROVED = ROOT / "stage2-8x" / "moonrunner_paperdoll_8x_earfix.png"
BODY_DIR = ROOT / "reference" / "8x" / "body-frames"
OUT = ROOT / "stage3" / "walk_dir1"

GAME_SIZE = (130, 128)
OUTPUT_SIZE = (1040, 1024)
SCALE = 8

# Approved paperdoll component bounds at game size.
EARS_BBOX = (77, 16, 111, 39)
TAIL_BBOX = (6, 105, 78, 164)

# Tip motion lags the hips over one ten-frame gait cycle. Root displacement remains zero.
TIP_DX = (0, -1, -2, -2, -1, 0, 1, 2, 2, 1)
TIP_DY = (0, 1, 2, 3, 2, 0, -1, -2, -3, -2)


def black_key_1x(path: Path) -> Image.Image:
    src = Image.open(path).convert("RGB").resize(GAME_SIZE, Image.Resampling.NEAREST)
    dst = Image.new("RGBA", GAME_SIZE, (0, 0, 0, 0))
    src_px = src.load()
    dst_px = dst.load()
    for y in range(GAME_SIZE[1]):
        for x in range(GAME_SIZE[0]):
            color = src_px[x, y]
            if color != (0, 0, 0):
                dst_px[x, y] = (*color, 255)
    return dst


def body_anchors(body: Image.Image) -> tuple[float, int, float, int]:
    alpha = body.getchannel("A")
    pts = [(x, y) for y in range(GAME_SIZE[1]) for x in range(GAME_SIZE[0]) if alpha.getpixel((x, y))]
    top = min(y for _, y in pts)

    head_xs = [x for x, y in pts if top <= y <= top + 8]
    head_center = mean(head_xs)

    hip_y = top + 30
    hip_xs = [
        x
        for y in range(max(0, hip_y - 2), min(GAME_SIZE[1], hip_y + 3))
        for x in range(GAME_SIZE[0])
        if alpha.getpixel((x, y))
    ]
    hip_center = mean(hip_xs)
    return head_center, top, hip_center, hip_y


def binary_resize(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    resized = image.resize(size, Image.Resampling.NEAREST).convert("RGBA")
    alpha = resized.getchannel("A").point(lambda a: 255 if a else 0)
    resized.putalpha(alpha)
    return resized


def warp_tail_tip(tail: Image.Image, dx_tip: int, dy_tip: int) -> tuple[Image.Image, tuple[int, int]]:
    """Bend the plume progressively toward its tip while leaving the root fixed."""
    width, height = tail.size
    pad = 4
    result = Image.new("RGBA", (width + pad * 2, height + pad * 2), (0, 0, 0, 0))
    src = tail.load()
    dst = result.load()

    for out_y in range(result.height):
        for out_x in range(result.width):
            base_x = out_x - pad
            clamped_x = min(width - 1, max(0, base_x))
            influence = (width - 1 - clamped_x) / max(1, width - 1)
            src_x = round(base_x - dx_tip * influence)
            src_y = round(out_y - pad - dy_tip * influence)
            if 0 <= src_x < width and 0 <= src_y < height:
                dst[out_x, out_y] = src[src_x, src_y]

    # In the approved tail the attachment is its upper-right pixel.
    root = (pad + width - 1, pad)
    return result, root


def remove_body_overlap(layer: Image.Image, body: Image.Image) -> None:
    layer_px = layer.load()
    body_alpha = body.getchannel("A").load()
    for y in range(GAME_SIZE[1]):
        for x in range(GAME_SIZE[0]):
            if body_alpha[x, y]:
                layer_px[x, y] = (0, 0, 0, 0)


def paste_at_anchor(canvas: Image.Image, sprite: Image.Image, anchor: tuple[int, int], target: tuple[int, int]) -> None:
    x = target[0] - anchor[0]
    y = target[1] - anchor[1]
    canvas.alpha_composite(sprite, (x, y))


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)

    approved_1x = Image.open(APPROVED).convert("RGBA").resize((260, 237), Image.Resampling.NEAREST)
    approved_ears = approved_1x.crop(EARS_BBOX)
    approved_tail = approved_1x.crop(TAIL_BBOX)

    # Body sprites are smaller than paperdolls; preserve proportions from the approved asset.
    ears = binary_resize(approved_ears, (16, 11))
    tail = binary_resize(approved_tail, (34, 28))

    previews: list[Image.Image] = []
    body_paths = sorted(BODY_DIR.glob("walk_dir1_f*_8x.png"))
    if len(body_paths) != 10:
        raise RuntimeError(f"Expected 10 dir1 walk frames, found {len(body_paths)}")

    for index, body_path in enumerate(body_paths):
        body = black_key_1x(body_path)
        head_x, head_top, hip_x, hip_y = body_anchors(body)

        accessory = Image.new("RGBA", GAME_SIZE, (0, 0, 0, 0))

        warped_tail, tail_root = warp_tail_tip(tail, TIP_DX[index], TIP_DY[index])
        tail_layer = Image.new("RGBA", GAME_SIZE, (0, 0, 0, 0))
        paste_at_anchor(
            tail_layer,
            warped_tail,
            tail_root,
            (round(hip_x), hip_y),
        )
        remove_body_overlap(tail_layer, body)
        accessory.alpha_composite(tail_layer)

        # Seat the bases into the crown by one game pixel; this mirrors the approved earfix.
        ear_x = round(head_x - ears.width / 2)
        ear_y = head_top - ears.height + 3
        accessory.alpha_composite(ears, (ear_x, ear_y))

        output = accessory.resize(OUTPUT_SIZE, Image.Resampling.NEAREST)
        output.save(OUT / f"walk_dir1_f{index:02}.png", optimize=False)

        preview = body.copy()
        preview.alpha_composite(accessory)
        previews.append(preview.resize(OUTPUT_SIZE, Image.Resampling.NEAREST))

    strip = Image.new("RGBA", (OUTPUT_SIZE[0] * 10, OUTPUT_SIZE[1]), (0, 0, 0, 0))
    for index, preview in enumerate(previews):
        strip.alpha_composite(preview, (index * OUTPUT_SIZE[0], 0))
    strip.save(OUT / "walk_dir1_preview_strip.png", optimize=False)


if __name__ == "__main__":
    main()
