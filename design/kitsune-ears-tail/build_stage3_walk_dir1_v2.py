from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(r"D:\ShatteredLegacy\design\kitsune-ears-tail")
REF = ROOT / "reference" / "8x"
SIL_DIR = REF / "silhouettes"
OUT_DIR = ROOT / "stage3" / "walk_dir1"
GUIDES_JSON = REF / "frame-guides.json"

SCALE = 8
GAME_SIZE = (130, 128)
CANVAS_SIZE = (1040, 1024)

# Natural fox palette. No entry is pure black and every painted pixel is opaque.
OUTLINE = (67, 43, 38, 255)
TIP_DARK = (83, 52, 45, 255)
EAR_DARK = (101, 57, 43, 255)
DEEP_RED = (128, 46, 31, 255)
RED_SHADOW = (159, 48, 27, 255)
EDGE_RED = (190, 54, 25, 255)
FOX_DARK = (211, 61, 24, 255)
FOX = (232, 72, 24, 255)
FOX_MID = (244, 87, 29, 255)
FOX_LIGHT = (252, 110, 42, 255)
FOX_GOLD = (255, 132, 55, 255)
CREAM_DARK = (169, 124, 94, 255)
CREAM_SHADOW = (194, 151, 113, 255)
CREAM_MID = (220, 180, 137, 255)
CREAM = (239, 204, 164, 255)
CREAM_LIGHT = (255, 226, 188, 255)
CREAM_HI = (255, 239, 210, 255)

# High-readability game-client colors used by the final walk-cycle polish.
TAIL_UNDER = (174, 68, 30, 255)
TAIL_BASE = (238, 96, 31, 255)
TAIL_TOP = (255, 151, 64, 255)
TIP_CREAM = (250, 221, 177, 255)
EAR_ORANGE = (241, 105, 35, 255)
EAR_LIGHT = (255, 151, 62, 255)
EAR_CREAM = (250, 220, 177, 255)


# Each visible centerline is authored for its frame at game resolution. A
# separate hidden root is added inside the lower back. The list then runs down
# and behind the viewer-right hip toward the low cream tip. These are different
# shapes, not offsets of one source tail. dx is relative to the guide's right
# body edge, dy to the green lower-back row.
TAILS = {
    0: [(-4, 2), (-1, 5), (2, 8), (3, 11), (4, 15), (5, 18)],
    1: [(-4, 2), (-1, 5), (3, 8), (4, 11), (5, 15), (6, 18)],
    2: [(-3, 2), (0, 5), (4, 8), (5, 11), (6, 15), (7, 19)],
    3: [(-4, 2), (-1, 5), (3, 8), (4, 11), (5, 15), (6, 18)],
    4: [(-4, 2), (-1, 5), (2, 8), (3, 11), (6, 15), (7, 18)],
    5: [(-4, 2), (-1, 5), (4, 8), (7, 11), (9, 15), (10, 18)],
    6: [(-4, 2), (-1, 5), (2, 8), (3, 11), (4, 15), (5, 18)],
    7: [(-3, 2), (0, 5), (4, 8), (5, 11), (6, 15), (7, 19)],
    8: [(-3, 2), (0, 5), (5, 8), (6, 11), (7, 15), (8, 19)],
    9: [(-4, 2), (-1, 5), (3, 8), (4, 11), (5, 15), (6, 18)],
}

# Small per-frame changes in ear lean emphasize head motion and the 3/4 view.
EAR_LEAN = [0, 0, 1, 1, 0, -1, -1, 0, 1, 0]


def load_walk_guides() -> dict[str, dict]:
    data = json.loads(GUIDES_JSON.read_text(encoding="utf-8"))
    # The file is a dict keyed directly by frame name in the supplied pack.
    if "frames" in data:
        data = data["frames"]
    return data


def game_silhouette(frame: int) -> Image.Image:
    src = Image.open(SIL_DIR / f"walk_dir1_f{frame:02d}_8x.png").convert("RGBA")
    return src.resize(GAME_SIZE, Image.Resampling.NEAREST)


def draw_tail(layer: Image.Image, frame: int, body_mask: Image.Image, guide: dict) -> None:
    draw = ImageDraw.Draw(layer)
    left, right = map(int, guide["body_x_at_lower_back"])
    back_y = int(guide["lower_back_row"])
    hidden_root = ((left + right) // 2 + 2, back_y)
    pts = [hidden_root] + [(right + dx, back_y + dy) for dx, dy in TAILS[frame]]

    def raster_line(a: tuple[int, int], b: tuple[int, int]) -> list[tuple[int, int]]:
        """Integer points from a to b, including both ends."""
        x0, y0 = a
        x1, y1 = b
        dx = abs(x1 - x0)
        sx = 1 if x0 < x1 else -1
        dy = -abs(y1 - y0)
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        result = []
        while True:
            result.append((x0, y0))
            if x0 == x1 and y0 == y1:
                return result
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def stamp(x: int, y: int, radius: int, color: tuple[int, int, int, int]) -> None:
        if radius <= 0:
            draw.point((x, y), fill=color)
        else:
            draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=color)

    # Tapered bushy silhouette: narrow hidden root and fullest upper-middle.
    # The final segment is drawn separately as a stepped rounded taper.
    draw.line(pts[:-1], fill=OUTLINE, width=5, joint="curve")
    draw.line(pts[2:-1], fill=OUTLINE, width=8, joint="curve")
    # Three high-contrast lengthwise fur bands: dark underside, bright body,
    # and a light upper ridge. These remain readable after the client palette.
    draw.line(pts[:-1], fill=TAIL_UNDER, width=3, joint="curve")
    draw.line(pts[2:-1], fill=TAIL_UNDER, width=6, joint="curve")
    draw.line(pts[1:-1], fill=TAIL_BASE, width=4, joint="curve")
    draw.line(pts[2:-2], fill=TAIL_BASE, width=5, joint="curve")
    draw.line([(x, y + 2) for x, y in pts[2:-2]], fill=TAIL_UNDER, width=2, joint="curve")
    draw.line([(x, y - 2) for x, y in pts[2:-2]], fill=TAIL_TOP, width=2, joint="curve")

    # A few deliberate one-pixel wedges break the smooth sleeve-like edges.
    tuft_index = 3 + (frame % 3)
    tx, ty = pts[min(tuft_index, len(pts) - 2)]
    draw.point((tx - 4, ty - 1), fill=OUTLINE)
    draw.point((tx - 3, ty - 1), fill=TAIL_TOP)
    draw.point((tx + 4, ty + 1), fill=OUTLINE)
    draw.point((tx + 3, ty + 1), fill=TAIL_UNDER)
    sx, sy = pts[-3]
    draw.point((sx - 4, sy), fill=OUTLINE)
    draw.point((sx - 3, sy), fill=TAIL_UNDER)

    # Cream section narrows 7 -> 5 -> 3 -> 1 pixels and ends at the existing
    # endpoint, producing a rounded point without changing length or motion.
    tip_path = raster_line(pts[-2], pts[-1])
    last = max(1, len(tip_path) - 1)
    for i, (x, y) in enumerate(tip_path):
        t = i / last
        outer_radius = max(0, round(3 * (1 - t)))
        inner_radius = max(0, outer_radius - 1)
        stamp(x, y, outer_radius, OUTLINE)
        stamp(x, y, inner_radius, TIP_CREAM)
    draw.point(pts[-1], fill=TIP_CREAM)

    # Bridge across the internal join so the cream is one solid patch, not a
    # stack of outlined rings. The dark color remains only on the outer edge.
    join_path = raster_line(pts[-3], pts[-2])
    draw.line(join_path[-2:], fill=TIP_CREAM, width=3, joint="curve")

    # The tail is behind the character. Omit every pixel covered by the body.
    px = layer.load()
    mask = body_mask.load()
    for y in range(GAME_SIZE[1]):
        for x in range(GAME_SIZE[0]):
            if mask[x, y][3] != 0 and px[x, y][3] != 0:
                px[x, y] = (0, 0, 0, 0)

    # Masking can expose a one-pixel root remnant on a few poses. Remove only
    # disconnected micro-components; attached jagged fur pixels remain intact.
    remaining = {
        (x, y)
        for y in range(GAME_SIZE[1])
        for x in range(GAME_SIZE[0])
        if px[x, y][3] != 0
    }
    components = []
    while remaining:
        seed = remaining.pop()
        stack = [seed]
        component = [seed]
        while stack:
            cx, cy = stack.pop()
            for nx in range(cx - 1, cx + 2):
                for ny in range(cy - 1, cy + 2):
                    neighbor = (nx, ny)
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        stack.append(neighbor)
                        component.append(neighbor)
        components.append(component)
    for component in components:
        if len(component) <= 2:
            for x, y in component:
                px[x, y] = (0, 0, 0, 0)


def draw_ears(layer: Image.Image, frame: int, guide: dict) -> None:
    px = layer.load()
    head_l, head_r = map(int, guide["head_x"])
    crown = int(guide["crown_y"])
    lean = EAR_LEAN[frame]
    center = (head_l + head_r) // 2

    def put(x: int, y: int, color: tuple[int, int, int, int]) -> None:
        if 0 <= x < GAME_SIZE[0] and 0 <= y < GAME_SIZE[1]:
            px[x, y] = color

    # Far ear first: pointed 4 x 7 wedge, partially hidden by the near ear.
    fx = center + 1 + lean
    far = [
        (0, -6, TIP_DARK),
        (0, -5, EAR_DARK),
        (0, -4, EAR_ORANGE), (1, -4, EAR_LIGHT),
        (-1, -3, OUTLINE), (0, -3, EAR_ORANGE), (1, -3, EAR_LIGHT),
        (-1, -2, EAR_ORANGE), (0, -2, EAR_CREAM), (1, -2, EAR_LIGHT), (2, -2, OUTLINE),
        (-1, -1, EAR_ORANGE), (0, -1, EAR_CREAM), (1, -1, EAR_ORANGE), (2, -1, OUTLINE),
        (-1, 0, OUTLINE), (0, 0, EAR_ORANGE), (1, 0, EAR_ORANGE), (2, 0, OUTLINE),
    ]
    for dx, dy, color in far:
        put(fx + dx, crown + dy, color)

    # Near ear: pointed 4 x 7 triangle with a two-pixel brown tip and clear
    # cream inner fur. Its base stays on the exact approved crown anchor.
    nx = center - 2 + lean
    near = [
        (0, -6, TIP_DARK),
        (0, -5, EAR_DARK),
        (0, -4, EAR_ORANGE), (1, -4, EAR_LIGHT),
        (-1, -3, OUTLINE), (0, -3, EAR_ORANGE), (1, -3, EAR_LIGHT),
        (-1, -2, EAR_ORANGE), (0, -2, EAR_CREAM), (1, -2, EAR_CREAM), (2, -2, OUTLINE),
        (-1, -1, EAR_ORANGE), (0, -1, EAR_CREAM), (1, -1, EAR_LIGHT), (2, -1, OUTLINE),
        (-1, 0, OUTLINE), (0, 0, EAR_ORANGE), (1, 0, EAR_ORANGE), (2, 0, OUTLINE),
    ]
    for dx, dy, color in near:
        put(nx + dx, crown + dy, color)


def build_frame(frame: int, guide: dict) -> tuple[Image.Image, Image.Image]:
    silhouette = game_silhouette(frame)
    item = Image.new("RGBA", GAME_SIZE, (0, 0, 0, 0))
    draw_tail(item, frame, silhouette, guide)
    draw_ears(item, frame, guide)

    # Preview ordering: tail behind silhouette; ears on top of the head.
    tail_only = item.copy()
    crown = int(guide["crown_y"])
    ear_cut = max(0, crown - 7)
    for y in range(ear_cut, min(GAME_SIZE[1], crown + 2)):
        for x in range(GAME_SIZE[0]):
            tail_only.putpixel((x, y), (0, 0, 0, 0))
    preview = Image.alpha_composite(tail_only, silhouette)
    ears_only = Image.new("RGBA", GAME_SIZE, (0, 0, 0, 0))
    ears_only.alpha_composite(item.crop((0, ear_cut, GAME_SIZE[0], crown + 2)), (0, ear_cut))
    preview = Image.alpha_composite(preview, ears_only)
    return item, preview


def validate(item: Image.Image, silhouette: Image.Image, frame: int, guide: dict) -> dict:
    colors = item.getdata()
    alphas = sorted({p[3] for p in colors})
    opaque_black = sum(1 for p in colors if p[3] and p[:3] == (0, 0, 0))

    crown = int(guide["crown_y"])
    item_px = item.load()
    sil_px = silhouette.load()
    tail_overlap = 0
    tail_points = []
    ear_points = []
    for y in range(GAME_SIZE[1]):
        for x in range(GAME_SIZE[0]):
            if item_px[x, y][3]:
                if y >= crown + 2:
                    tail_points.append((x, y))
                    if sil_px[x, y][3]:
                        tail_overlap += 1
                elif crown - 7 <= y <= crown + 1:
                    ear_points.append((x, y))

    def bbox(points):
        xs = [p[0] for p in points]
        ys = [p[1] for p in points]
        return [min(xs), min(ys), max(xs), max(ys)] if points else None

    return {
        "frame": frame,
        "alpha_values": alphas,
        "opaque_black_pixels": opaque_black,
        "tail_body_overlap_pixels": tail_overlap,
        "tail_bbox_game_px": bbox(tail_points),
        "ear_bbox_game_px": bbox(ear_points),
        "palette_colors": len({p for p in colors if p[3]}),
    }


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    guides = load_walk_guides()
    previews = []
    report = []

    for frame in range(10):
        key = f"walk_dir1_f{frame:02d}"
        guide = guides[key]
        item, preview = build_frame(frame, guide)
        silhouette = game_silhouette(frame)
        report.append(validate(item, silhouette, frame, guide))

        out = item.resize(CANVAS_SIZE, Image.Resampling.NEAREST)
        out.save(OUT_DIR / f"{key}.png", optimize=False)
        previews.append(preview.resize(CANVAS_SIZE, Image.Resampling.NEAREST))

    strip = Image.new("RGBA", (CANVAS_SIZE[0] * 10, CANVAS_SIZE[1]), (0, 0, 0, 0))
    for i, preview in enumerate(previews):
        strip.alpha_composite(preview, (i * CANVAS_SIZE[0], 0))
    strip.save(OUT_DIR / "walk_dir1_preview_strip.png", optimize=False)

    # A compact close-up strip makes the actual sprite-scale drawing readable
    # without changing any delivered frame coordinates.
    crop_box = (24, 30, 94, 112)
    closeups = [
        p.resize(GAME_SIZE, Image.Resampling.NEAREST)
        .crop(crop_box)
        .resize(((crop_box[2] - crop_box[0]) * 4, (crop_box[3] - crop_box[1]) * 4), Image.Resampling.NEAREST)
        for p in previews
    ]
    close_strip = Image.new("RGBA", (closeups[0].width * 10, closeups[0].height), (0, 0, 0, 0))
    for i, closeup in enumerate(closeups):
        close_strip.alpha_composite(closeup, (i * closeups[0].width, 0))
    close_strip.save(OUT_DIR / "walk_dir1_preview_strip_closeup_4x.png", optimize=False)
    (OUT_DIR / "walk_dir1_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
