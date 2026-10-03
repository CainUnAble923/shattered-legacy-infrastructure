# Wiki macros

Source for the wiki's Macros pages (`uo:macros:*`): downloadable TazUO Legion scripts.

- `macros.csv` lists each macro: script file, page name, title, one-line summary.
- `<Name>.head.txt` is the hand-written top of that macro's page. The script itself is appended as a downloadable code block.
- `hub.head.txt` / `hub.foot.txt` wrap the generated table on `uo:macros:start`.
- `scripts/` holds the last published copy of every script (so they are in Git).

Publish or update: run `D:\UO\haven-migration\Publish-WikiMacros.ps1`. It reads the scripts from the newest
player package's `app\tazuo\LegionScripts` (or `-Source <folder>`), regenerates `content\pages\uo\macros\`,
and replaces only those pages on Haven. Edit the macro pages here, not in the wiki; the next publish overwrites them.

To add a macro: add a row to `macros.csv`, write `<Name>.head.txt`, publish.
