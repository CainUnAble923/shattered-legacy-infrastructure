# Console art slots (cc-P132)

The Shard Console reads this folder once at start. Each slot is one fixed file name; drop a file in, restart the
console, and that piece replaces its drawn stand-in. A missing or bad file never stops the console: the drawn
fallback is used and one line goes to the startup log (`console-art: ...`).

The full spec for making the art (sizes, style, one image prompt per piece) is in the docs repo:
`D:\UO\shard-migration\notes\console-art-spec.md`.

| Slot (file name) | Shown at | Kind | Budget | What it is |
|---|---|---|---|---|
| `crystal-green.png` | 32 px (22 to 28 px on tiles and lights) | animated | 120 KB | the green crystal: running, healthy, latest |
| `crystal-amber.png` | 32 px | animated | 120 KB | the amber crystal: warning, waiting, needs a rebuild |
| `crystal-red.png` | 32 px | animated | 120 KB | the red crystal: down, failed, blocked |
| `crystal-grey.png` | 32 px | static | 60 KB | the dull grey crystal: off, retired, unknown |
| `golem-running.png` | 64 px (56 on the tile) | animated | 300 KB | the Golem, glowing: running a prompt |
| `golem-idle.png` | 64 px | static | 120 KB | the Golem, a soft glow: awake, nothing running |
| `golem-dark.png` | 64 px | static | 120 KB | the Golem, dark: resting or off |
| `rune-tab.png` | 16 px | static | 30 KB | the rune before each tab header |
| `rune-corner.png` | 16 px (40 in the header) | static | 30 KB | the sigil in a corner and at the header's end |
| `spinner.png` | 24 px | animated | 100 KB | the working sigil |
| `header-band.png` | 64 px tall, tiled across | static | 250 KB | the band behind the title |
| `stone.png` | 256 px, tiled | tile | 200 KB | the window's stone |
| `page.png` | 256 px, tiled | tile | 200 KB | the tab pages' slate |
| `icon-live.png`, `icon-test.png`, `icon-publisher.png`, `icon-dns.png`, `icon-golem.png`, `icon-playtests.png`, `icon-websites.png` | 32 px | static | 40 KB each | one icon per tile |

Formats: PNG with transparency for everything static. An animated slot takes either a **horizontal sprite strip PNG**
(every frame the same width, side by side) with its frame count and speed in `art.json`, or an animated GIF. The
console prefers the strip: a PNG keeps 8-bit transparency (a GIF's is on or off, which leaves a hard halo on the dark
stone), the frame timing is exact, and the console draws the frames itself. A GIF is taken as it is.

`art.json` (only animated strips need an entry):

```json
{ "slots": { "crystal-green": { "frames": 12, "fps": 8 }, "golem-running": { "frames": 8, "fps": 6 } } }
```

The whole folder stays well under the repo's 5 MB file rule: the budgets above add up to about 1.9 MB, and no single
file may be over its budget (an oversized file is refused and the fallback drawn, with the reason in the log).

## The web console (cc-P155, cc-P182)

The web console (`web-console\server.py`) serves this folder by slot name too and picks up every slot above with no
code change: the crystals on tiles and lights, the three Golem figures by its state, both runes, the spinner while a
button runs, the header band, the stone and page textures, and the seven tile icons. A strip in `art.json` steps through
its frames there as well. cc-P182 adds fifteen button slots, `btn-<name>.png` (28 px, static, 20 KB each), one per
Golem button (`web-console\golem-actions.json`, `art`), listed in the spec (`notes\console-art-spec.md`, "The web
console's Golem buttons"); until they land each button draws its stand-in.
