# Design prompt: kitsune fox ears and tail (Ultima Online art)

Paste everything below the line into the design chat. Attach the files listed under "Attach".

**Attach (full paths):**

Paperdoll and body (from the game client):
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\paperdoll_Gump_60701_(Long_Hair).png` (her hair and head position on the female paperdoll)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\paperdoll_Gump_60559_(Female_Kimono).png` (body outline and the 260 x 237 canvas)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\sheet_walk_dir1.png` (the walk cycle, for the test in stage 3)
- For stage 4: the frames in `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\body-frames\` for the direction being drawn

Style references (Chase's picks):
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-1-fox-ears-sprite.png` (ear shape and palette: orange outer, gray inner, dark tips)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-2-fox-ears-portrait.png` (ears rising out of the hair on a woman's head)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-3-fox-tail-sprite.png` (tail shape, with a pale tip)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-4-fox-tail-shading-steps.png` (how to shade the tail: base, edge color, big fur, small fur, tiny fur)
---

I'm building a private Ultima Online shard (the classic 2D client). I need new art for **one wearable item: fox ears and a single fox tail**, worn by a female human character. She is a kitsune (fox spirit) in her human form. Please design it in the classic UO 2D art style, in the stages below, and stop for my approval after each stage.

## Look

- Fox ears that sit on top of her head and rise out of her hair. Pointed, fairly large, with a lighter inner ear.
- **One** full, bushy fox tail coming from her lower back, with a lighter or white tip.
- Natural fox colors for previews (orange-red fur, cream or white inner ear and tail tip). The final art must also come in a **grayscale version** so the game can recolor it to the player's chosen hue, the way UO recolors hair.

## Hard technical rules (every stage)

- Classic UO 2D sprite style: painterly pixel art at true size, no smoothing into the background, no soft shadows, no semi-transparent pixels. Every pixel is either fully opaque or fully transparent.
- **Pure black (0,0,0) means transparent in UO.** Never use pure black inside the art; use very dark brown or gray instead.
- Keep colors within 15-bit color (5 bits per channel). Avoid smooth gradients that only work in 24-bit.
- Deliver PNGs with transparent backgrounds, at exact size (no upscaling), plus a 4x nearest-neighbor preview of each.

## Stage 1: concept (stop for approval)

Show 3 different designs for the ears and tail, each drawn on the female paperdoll (use the attached kimono and long hair gumps as the body and hair). Follow my four style references for the ear shape, the tail shape and the fur shading. Vary ear size and tail size and curl. Natural fox colors.

## Stage 2: paperdoll image and item icon (stop for approval)

For the design I pick:

1. **Paperdoll gump**, exactly **260 x 237 pixels**, same canvas and alignment as the attached gumps. Draw only the ears and tail; everything else is transparent (black). Place the ears on top of her head, rising out of the long hair in `Gump_60701`. The tail shows behind her hip, curving out to one side so it is visible past the body outline. Any part of the tail hidden behind her body is simply not drawn.
2. **Item icon** (how it looks in a backpack), at most **44 x 44 pixels**: the ears and tail as a small bundle.
3. Both in natural color **and** grayscale.

## Stage 3: animation test (stop for approval)

The game draws the item on top of her body in every animation frame, so each frame shows only the ears and tail, positioned to match her body in that frame. Start with **one direction of the walk cycle**: the 10 frames in `sheet_walk_dir1.png`.

- Each output frame must be **130 x 128 pixels**, the same canvas as the reference frames, with the ears and tail placed exactly where they belong on her body in that frame. Her feet anchor at pixel (67, 98) in every frame.
- Draw **only** the ears and tail, not the body. Where her body would hide part of the tail, leave those pixels transparent.
- Motion: the tail sways and follows through with her gait (it lags a little behind her hips); the ears bob slightly with her head.
- Name each output exactly like its reference frame (`walk_dir1_f00.png` to `walk_dir1_f09.png`).

## Stage 4: the full set (after the test is approved)

**105 frames in total**, each 130 x 128 with the same rules as stage 3, named to match the reference frames:

| Action | Frames per direction | Directions | Frames |
|---|---|---|---|
| Stand | 1 | 5 | 5 |
| Walk | 10 | 5 | 50 |
| Run | 10 | 5 | 50 |
| **Total** | | | **105** |

UO draws 5 directions (`dir0` to `dir4`); the game mirrors them for the other 3, so do not draw mirrored directions. Deliver grayscale frames; one natural-color strip per action and direction as a preview is enough.
