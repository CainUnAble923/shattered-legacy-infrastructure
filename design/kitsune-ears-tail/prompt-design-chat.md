# Design prompt: kitsune fox ears and tail (Ultima Online art)

Paste everything below the line into the design chat. Attach the files listed under "Attach".

**Status (2026-10-05):** stage 1 done (concept C, "Moonrunner", picked). Stage 2 item icon done (converted by the
overseer: `icon_44.png`, `icon_44_partialhue.png`, `icon_44_gray.png`). Next: the stage 2 paperdoll image.

**Attach (full paths):**

Paperdoll and body, at 8x (draw over these; they are the exact canvases to deliver on):
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\8x\paperdoll_Gump_60701_(Long_Hair)_8x.png` (her hair and head position)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\8x\paperdoll_Gump_60559_(Female_Kimono)_8x.png` (body outline, 2080 x 1896 canvas)
- For stages 3 and 4: the frames in `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\8x\body-frames\` for the direction being drawn (each 1040 x 1024)

Style references (Chase's picks):
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-1-fox-ears-sprite.png` (ear shape and palette: orange outer, gray inner, dark tips)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-2-fox-ears-portrait.png` (ears rising out of the hair on a woman's head)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-3-fox-tail-sprite.png` (tail shape, with a pale tip)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\chase-ref-4-fox-tail-shading-steps.png` (how to shade the tail: base, edge color, big fur, small fur, tiny fur)

---

I'm building a private Ultima Online shard (the classic 2D client). I need new art for **one wearable item: fox ears and a single fox tail**, worn by a female human character. She is a kitsune (fox spirit) in her human form. The design is chosen: **concept C, "Moonrunner"**. Work in the stages below and stop for my approval after each one.

## Look (Moonrunner)

- Tall, pointed fox ears with dark tips and tufted cream inner fur, rising out of her hair on top of her head.
- **One** long, low fox tail sweeping out behind her, orange-red with a cream tip.
- Natural fox colors only. I make the grayscale and hueable versions myself.

## How to deliver (every stage)

I convert your images to the game's true size myself, so focus on the look and on exact placement:

- **Draw at exactly 8x the game size, on the attached 8x canvas, at the same size and alignment.** Do not crop, resize or shift the canvas. Placement must match the reference exactly, because I shrink by exactly 8.
- **Transparent background** (real PNG transparency), not black.
- Draw only the ears and tail. Never include the body, hair or dress in a delivered file.
- Keep it crisp: flat fur colors in clear bands, like pixel art seen up close. No glow, blur, soft shadows or anti-aliased haze around the edges; those turn into mush when shrunk.
- Keep the shapes chunky enough to survive shrinking: detail smaller than about an 8 x 8 block disappears in game.

## Stage 2: paperdoll image (stop for approval)

One image, **2080 x 1896**, on the attached 8x paperdoll canvas:

- The ears sit on top of her head, rising out of the long hair in the Long Hair gump.
- The tail shows behind her hip, sweeping low and out to one side so it is visible past the body outline. The part hidden behind her body is simply not drawn.
- Also send a preview with the ears and tail laid over the kimono and long hair gumps, so I can judge the fit.

## Stage 3: animation test (stop for approval)

The game draws the item on top of her body in every animation frame, so each frame shows only the ears and tail, placed to match her body in that frame. Start with **one direction of the walk cycle**: the 10 frames `walk_dir1_f00_8x.png` to `walk_dir1_f09_8x.png`.

- Each output frame is **1040 x 1024**, the same canvas as its reference frame, with the ears and tail placed exactly where they belong on her body in that frame. Her feet anchor at pixel (536, 784) in every frame.
- Where her body would hide part of the tail, leave those pixels transparent.
- Motion: the tail sways and follows through with her gait (it lags a little behind her hips); the ears bob slightly with her head.
- Name each output like its reference frame, without the `_8x` (`walk_dir1_f00.png` to `walk_dir1_f09.png`).
- Also send one preview strip with the ears and tail laid over the body frames.

## Stage 4: the full set (after the test is approved)

**105 frames in total**, each 1040 x 1024 with the same rules as stage 3, named to match the reference frames:

| Action | Frames per direction | Directions | Frames |
|---|---|---|---|
| Stand | 1 | 5 | 5 |
| Walk | 10 | 5 | 50 |
| Run | 10 | 5 | 50 |
| **Total** | | | **105** |

UO draws 5 directions (`dir0` to `dir4`); the game mirrors them for the other 3, so do not draw mirrored directions. One preview strip per action and direction, over the body frames, is enough.

## Stage 3 retry (2026-10-05, after the first walk test was rejected)

The first walk test pasted the front-view paperdoll art onto the walking sprite. It was rejected: wrong scale, the tail root at the thigh, the tail drawn over her front leg, front-facing ears on a body turned three-quarters away, and no real motion (the art only slid around).

New body references are plain blue silhouettes, so there is no gray body to trip the image tools:
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\8x\silhouettes\` (1040 x 1024 each, same alignment as before)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\8x\silhouette-guides\` (red line = top of her head, green line = lower back / hip height, black circle = the sprite's anchor point)
- `D:\ShatteredLegacy\design\kitsune-ears-tail\reference\8x\frame-guides.json` (per frame, in game pixels: crown_y, head_x left and right, lower_back_row, body x range at that row)
