from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(r"D:\ShatteredLegacy\design\kitsune-ears-tail")
REF = ROOT / "reference" / "8x"
SIL_DIR = REF / "silhouettes"
OUT_ROOT = ROOT / "stage4"
GUIDES_JSON = REF / "frame-guides.json"

GAME_SIZE = (130, 128)
CANVAS_SIZE = (1040, 1024)
DIRECTIONS = (0, 2, 3, 4)

# Exact approved stage3/dir1 palette.
OUTLINE = (67, 43, 38, 255)
TIP_DARK = (83, 52, 45, 255)
EAR_DARK = (101, 57, 43, 255)
TAIL_UNDER = (174, 68, 30, 255)
TAIL_BASE = (238, 96, 31, 255)
TAIL_TOP = (255, 151, 64, 255)
TIP_CREAM = (250, 221, 177, 255)
EAR_ORANGE = (241, 105, 35, 255)
EAR_LIGHT = (255, 151, 62, 255)
EAR_CREAM = (250, 220, 177, 255)

# One-beat-late follow-through used consistently in all directions.
TAIL_SWAY = (0, 1, 2, 1, 0, -1, -2, -1, 0, 1)
TAIL_BOB = (0, 1, 1, 0, -1, -1, 0, 1, 1, 0)
# The profile tail has enough exposed length for a two-pixel vertical follow-
# through to read in the client.  Its phase remains one beat behind the hips.
DIR2_BOB = (0, 1, 2, 1, 0, -1, -2, -1, 0, 1)
EAR_LEAN = (0, 0, 1, 1, 0, -1, -1, 0, 1, 0)
# Low front-view emergence points selected from each silhouette's leg gap or
# nearest outer calf edge. Positive is viewer-right, negative viewer-left.
DIR0_TIP_X = (0, 6, 5, 5, 4, -7, -7, -6, 6, 6)


def load_guides() -> dict[str, dict]:
    data = json.loads(GUIDES_JSON.read_text(encoding="utf-8"))
    return data["frames"] if "frames" in data else data


def game_silhouette(direction: int, frame: int) -> Image.Image:
    name = f"walk_dir{direction}_f{frame:02d}_8x.png"
    return Image.open(SIL_DIR / name).convert("RGBA").resize(GAME_SIZE, Image.Resampling.NEAREST)


def raster_line(a: tuple[int, int], b: tuple[int, int]) -> list[tuple[int, int]]:
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


def tail_points(direction: int, frame: int, guide: dict) -> list[tuple[int, int]]:
    left, right = map(int, guide["body_x_at_lower_back"])
    center = (left + right) // 2
    y = int(guide["lower_back_row"])
    sway = TAIL_SWAY[frame]
    bob = TAIL_BOB[frame]

    if direction == 0:
        # Front view: the full tail exists behind the body, but body masking
        # leaves only a low glimpse beside or between the moving legs.
        tip_x = DIR0_TIP_X[frame]
        return [
            (center, y),
            (center, y + 3),
            (center + round(tip_x * 0.15), y + 7),
            (center + round(tip_x * 0.35), y + 11),
            (center + round(tip_x * 0.70), y + 15 + bob),
            (center + tip_x, y + 18 + bob),
        ]

    if direction == 2:
        # Left profile: sweep down and back in a soft S. The upper-middle is
        # fullest; the cream tip finishes near knee height. Later points carry
        # the delayed swing so the root stays planted at the lower back.
        profile_bob = DIR2_BOB[frame]
        return [
            (center + 2, y),
            (right - 3, y + 2),
            (right + 1, y + 5),
            (right + 4 + round(sway * 0.25), y + 9),
            (right + 8 + round(sway * 0.50), y + 12 + profile_bob),
            (right + 11 + sway, y + 15 + profile_bob),
            (right + 10 + sway, y + 18 + profile_bob),
        ]

    if direction == 3:
        # Rear three-quarter view: foreground tail lies over lower back/legs.
        return [
            (center, y),
            (center + 1, y + 3),
            (center + 3, y + 7),
            (center + 5 + round(sway * 0.35), y + 11),
            (center + 5 + sway, y + 15 + bob),
            (center + 4 + sway, y + 19 + bob),
        ]

    # dir4 straight rear: foreground tail remains centered but sways across
    # the legs with delayed movement at the lower half.
    return [
        (center, y),
        (center, y + 3),
        (center + round(sway * 0.25), y + 7),
        (center + round(sway * 0.50), y + 11),
        (center + sway, y + 15 + bob),
        (center + sway, y + 19 + bob),
    ]


def remove_small_components(layer: Image.Image) -> None:
    px = layer.load()
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


def draw_tail(
    direction: int,
    frame: int,
    guide: dict,
    silhouette: Image.Image,
) -> Image.Image:
    layer = Image.new("RGBA", GAME_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    pts = tail_points(direction, frame, guide)

    def stamp(x: int, y: int, radius: int, color: tuple[int, int, int, int]) -> None:
        if radius <= 0:
            draw.point((x, y), fill=color)
        else:
            draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=color)

    # Same approved tapered silhouette and three-band fur treatment as dir1.
    draw.line(pts[:-1], fill=OUTLINE, width=5, joint="curve")
    draw.line(pts[2:-1], fill=OUTLINE, width=8, joint="curve")
    draw.line(pts[:-1], fill=TAIL_UNDER, width=3, joint="curve")
    draw.line(pts[2:-1], fill=TAIL_UNDER, width=6, joint="curve")
    draw.line(pts[1:-1], fill=TAIL_BASE, width=4, joint="curve")
    draw.line(pts[2:-2], fill=TAIL_BASE, width=5, joint="curve")
    draw.line([(x, y + 2) for x, y in pts[2:-2]], fill=TAIL_UNDER, width=2, joint="curve")
    draw.line([(x, y - 2) for x, y in pts[2:-2]], fill=TAIL_TOP, width=2, joint="curve")

    tuft_index = 3 + (frame % 2)
    tx, ty = pts[min(tuft_index, len(pts) - 2)]
    draw.point((tx - 4, ty - 1), fill=OUTLINE)
    draw.point((tx - 3, ty - 1), fill=TAIL_TOP)
    draw.point((tx + 4, ty + 1), fill=OUTLINE)
    draw.point((tx + 3, ty + 1), fill=TAIL_UNDER)

    # One uninterrupted cream tip; no internal rings or stripes.
    tip_path = raster_line(pts[-2], pts[-1])
    last = max(1, len(tip_path) - 1)
    for i, (x, y) in enumerate(tip_path):
        t = i / last
        outer_radius = max(0, round(3 * (1 - t)))
        inner_radius = max(0, outer_radius - 1)
        stamp(x, y, outer_radius, OUTLINE)
        stamp(x, y, inner_radius, TIP_CREAM)
    draw.point(pts[-1], fill=TIP_CREAM)
    join_path = raster_line(pts[-3], pts[-2])
    draw.line(join_path[-2:], fill=TIP_CREAM, width=3, joint="curve")

    if direction in (0, 2):
        # Behind-body directions omit all covered pixels from the deliverable.
        px = layer.load()
        mask = silhouette.load()
        for y in range(GAME_SIZE[1]):
            for x in range(GAME_SIZE[0]):
                if mask[x, y][3] and px[x, y][3]:
                    px[x, y] = (0, 0, 0, 0)
        remove_small_components(layer)

    return layer


def put(px, x: int, y: int, color: tuple[int, int, int, int]) -> None:
    if 0 <= x < GAME_SIZE[0] and 0 <= y < GAME_SIZE[1]:
        px[x, y] = color


def full_ear(px, tip_x: int, crown: int, mirror: bool, cream: bool) -> None:
    # Approved 4x7 pointed ear. Mirroring changes only its taper direction.
    pattern = [
        (0, -6, TIP_DARK),
        (0, -5, EAR_DARK),
        (0, -4, EAR_ORANGE), (1, -4, EAR_LIGHT),
        (-1, -3, OUTLINE), (0, -3, EAR_ORANGE), (1, -3, EAR_LIGHT),
        (-1, -2, EAR_ORANGE), (0, -2, EAR_CREAM if cream else EAR_LIGHT),
        (1, -2, EAR_CREAM if cream else EAR_ORANGE), (2, -2, OUTLINE),
        (-1, -1, EAR_ORANGE), (0, -1, EAR_CREAM if cream else EAR_LIGHT),
        (1, -1, EAR_LIGHT), (2, -1, OUTLINE),
        (-1, 0, OUTLINE), (0, 0, EAR_ORANGE), (1, 0, EAR_ORANGE), (2, 0, OUTLINE),
    ]
    for dx, dy, color in pattern:
        put(px, tip_x + (-dx if mirror else dx), crown + dy, color)


def small_far_ear(px, tip_x: int, crown: int, cream: bool) -> None:
    # Profile/rear-three-quarter far ear: mostly hidden behind the near ear.
    pattern = [
        (0, -5, TIP_DARK),
        (0, -4, EAR_DARK),
        (-1, -3, OUTLINE), (0, -3, EAR_ORANGE),
        (-1, -2, EAR_ORANGE), (0, -2, EAR_CREAM if cream else EAR_LIGHT), (1, -2, OUTLINE),
        (-1, -1, OUTLINE), (0, -1, EAR_ORANGE), (1, -1, OUTLINE),
        (-1, 0, OUTLINE), (0, 0, EAR_ORANGE), (1, 0, OUTLINE),
    ]
    for dx, dy, color in pattern:
        put(px, tip_x + dx, crown + dy, color)


def draw_ears(direction: int, frame: int, guide: dict) -> Image.Image:
    layer = Image.new("RGBA", GAME_SIZE, (0, 0, 0, 0))
    px = layer.load()
    head_l, head_r = map(int, guide["head_x"])
    crown = int(guide["crown_y"])
    center = (head_l + head_r) // 2
    lean = EAR_LEAN[frame]

    if direction == 0:
        full_ear(px, center - 3 + lean, crown, mirror=False, cream=True)
        full_ear(px, center + 3 + lean, crown, mirror=True, cream=True)
    elif direction == 2:
        small_far_ear(px, center + 2 + lean, crown, cream=True)
        full_ear(px, center - 1 + lean, crown, mirror=False, cream=True)
    elif direction == 3:
        small_far_ear(px, center + 2 + lean, crown, cream=False)
        full_ear(px, center - 1 + lean, crown, mirror=False, cream=False)
    else:
        full_ear(px, center - 3 + lean, crown, mirror=False, cream=False)
        full_ear(px, center + 3 + lean, crown, mirror=True, cream=False)
    return layer


def build_frame(direction: int, frame: int, guide: dict):
    silhouette = game_silhouette(direction, frame)
    tail = draw_tail(direction, frame, guide, silhouette)
    ears = draw_ears(direction, frame, guide)
    item = Image.alpha_composite(tail, ears)

    if direction in (0, 2):
        preview = Image.alpha_composite(tail, silhouette)
    else:
        preview = Image.alpha_composite(silhouette, tail)
    preview = Image.alpha_composite(preview, ears)
    return item, preview, tail, ears, silhouette


def validate(item, tail, ears, silhouette, direction: int, frame: int, guide: dict) -> dict:
    colors = list(item.getdata())
    tail_px = tail.load()
    sil_px = silhouette.load()
    overlap = 0
    for y in range(GAME_SIZE[1]):
        for x in range(GAME_SIZE[0]):
            if tail_px[x, y][3] and sil_px[x, y][3]:
                overlap += 1

    crown = int(guide["crown_y"])
    ear_cream = sum(
        1
        for y in range(max(0, crown - 7), min(GAME_SIZE[1], crown + 1))
        for x in range(GAME_SIZE[0])
        if ears.getpixel((x, y)) == EAR_CREAM
    )
    tip_points = [
        (x, y)
        for y in range(GAME_SIZE[1])
        for x in range(GAME_SIZE[0])
        if tail.getpixel((x, y)) == TIP_CREAM
    ]
    tip_bbox = None
    if tip_points:
        xs = [p[0] for p in tip_points]
        ys = [p[1] for p in tip_points]
        tip_bbox = [min(xs), min(ys), max(xs), max(ys)]

    return {
        "direction": direction,
        "frame": frame,
        "alpha_values": sorted({p[3] for p in colors}),
        "opaque_black_pixels": sum(1 for p in colors if p[3] and p[:3] == (0, 0, 0)),
        "tail_body_overlap_pixels": overlap,
        "tail_layer": "behind" if direction in (0, 2) else "front",
        "ear_cream_pixels": ear_cream,
        "cream_tip_bbox_game_px": tip_bbox,
        "palette_colors": len({p for p in colors if p[3]}),
    }


def main() -> None:
    guides = load_guides()
    aggregate = []
    directions = tuple(int(value) for value in sys.argv[1:]) or DIRECTIONS
    invalid = sorted(set(directions) - set(DIRECTIONS))
    if invalid:
        raise ValueError(f"Unsupported directions: {invalid}")

    for direction in directions:
        out_dir = OUT_ROOT / f"walk_dir{direction}"
        out_dir.mkdir(parents=True, exist_ok=True)
        previews = []
        report = []

        for frame in range(10):
            key = f"walk_dir{direction}_f{frame:02d}"
            item, preview, tail, ears, silhouette = build_frame(direction, frame, guides[key])
            report.append(validate(item, tail, ears, silhouette, direction, frame, guides[key]))
            item.resize(CANVAS_SIZE, Image.Resampling.NEAREST).save(out_dir / f"{key}.png", optimize=False)
            previews.append(preview)

        full_strip = Image.new("RGBA", (CANVAS_SIZE[0] * 10, CANVAS_SIZE[1]), (0, 0, 0, 0))
        for i, preview in enumerate(previews):
            full_strip.alpha_composite(preview.resize(CANVAS_SIZE, Image.Resampling.NEAREST), (i * CANVAS_SIZE[0], 0))
        full_strip.save(out_dir / f"walk_dir{direction}_preview_strip.png", optimize=False)

        crop_box = (24, 28, 104, 116)
        closeups = [
            p.crop(crop_box).resize(
                ((crop_box[2] - crop_box[0]) * 4, (crop_box[3] - crop_box[1]) * 4),
                Image.Resampling.NEAREST,
            )
            for p in previews
        ]
        close_strip = Image.new("RGBA", (closeups[0].width * 10, closeups[0].height), (0, 0, 0, 0))
        for i, closeup in enumerate(closeups):
            close_strip.alpha_composite(closeup, (i * closeups[0].width, 0))
        close_strip.save(out_dir / f"walk_dir{direction}_preview_strip_closeup_4x.png", optimize=False)

        (out_dir / f"walk_dir{direction}_validation.json").write_text(
            json.dumps(report, indent=2), encoding="utf-8"
        )
        aggregate.extend(report)

    aggregate_path = OUT_ROOT / "walk_remaining_validation.json"
    if directions != DIRECTIONS and aggregate_path.exists():
        prior = json.loads(aggregate_path.read_text(encoding="utf-8"))
        aggregate.extend(row for row in prior if int(row["direction"]) not in directions)
        aggregate.sort(key=lambda row: (int(row["direction"]), int(row["frame"])))
    aggregate_path.write_text(
        json.dumps(aggregate, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
