# Wiki Content Seed

This directory contains reproducible starter content for the Shattered Legacy DokuWiki.

Directory mapping:

| Repository path | DokuWiki path |
| --- | --- |
| `pages/` | `/var/lib/dokuwiki/data/pages/` |
| `media/` | `/var/lib/dokuwiki/data/media/` |

Use the sync script to deploy this content:

```bash
sudo SYNC_MODE=seed ./scripts/deploy/wiki/sync-dokuwiki-content.sh
```

`SYNC_MODE=seed` preserves existing wiki pages and media.

`SYNC_MODE=overwrite` replaces matching pages/media from this repository. Use it only when intentionally refreshing seed content.

## Image Standards

- Prefer real in-game screenshots for player-facing pages.
- Store server brand assets in both the page namespace and the DokuWiki template namespace when needed.
- Store UO images under the `uo:media` namespace.
- Use lowercase names with dashes or underscores.
- Add useful alt text in the DokuWiki image syntax.
- Keep screenshots under 2 MB where possible.
- Use diagrams only when they explain a path, system, or relationship better than prose.

Current logo assets:

| Asset | DokuWiki namespace | Purpose |
| --- | --- | --- |
| `media/uo/media/shattered_legacy_logo.png` | `uo:media:shattered_legacy_logo.png` | Full-size page logo |
| `media/wiki/logo.png` | `wiki:logo.png` | DokuWiki header logo |
| `media/wiki/favicon.ico` | `wiki:favicon.ico` | Browser tab icon |
| `media/wiki/favicon.png` | `wiki:favicon.png` | PNG favicon fallback |
