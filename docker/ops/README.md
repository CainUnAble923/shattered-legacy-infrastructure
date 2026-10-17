# The ops pages on Haven (cc-P107, Haven steps H2 and H3)

Play tests, Ideas, Bug reports (with screenshots) and Shard status, served on Haven by the same `sl-play-tests` code as
on the game PC (cc-P105's Haven mode). The plan is `shard-migration/notes/cc-P89-haven-pages-plan.md`; this is Part C
items 2 and 3. Notes: `shard-migration/notes/cc-P107-haven-ops-move.md`.

| On Haven | What |
|---|---|
| `~/sl-ops/stack/` | `docker-compose.yml` and `git-pull.sh` from this folder (compose project `sl-ops`) |
| `~/sl-ops/repo/` | read-only clones of both repos, pulled every 2 minutes by `sl-ops-git-pull`, with `stamp-<repo>.txt` ("repo as of") |
| `~/sl-ops/data/testing/` | the one copy of play-test results, ideas and bug reports after the cutover |
| `~/sl-ops/keys/` | the read-only deploy key for the private `shard-migration` repo (made on Haven; never leaves it) |
| `~/sl-ops/ops.env` | `PLAYTESTS_TOKEN`, chmod 600, written by the deploy from a prompt |
| `192.168.1.61:8093` | the page (as the game PC served it), LAN only |
| `192.168.1.61:8094` | the token listener: `GET /api/export`, `POST /api/ingest/gamepc`, `GET /api/gamepc` |
| `http://haven.tail81098d.ts.net:8093/` | the page on the tailnet (cc-P133): Tailscale Serve forwards it to the page's loopback port `127.0.0.1:8093`. Tailscale only: nothing here is ever public, and the Cloudflare tunnel and Access plan (P89's H4) is dropped |

Every service on Haven and the game PC is listed in `docker/haven/services.json` (with the runbook,
`shard-migration/notes/haven-runbook.md`). Nothing else on Haven is touched: Vaultwarden, NPM and its hosts, the website, the wiki, Kevin's Parts, and
`~/shattered-legacy` (how those are deployed) all stay as they are.

## The three buttons (Shard Console, Websites tab, after the phrase `deploy to haven`)

All three are `Deploy-HavenOps.ps1` (this folder) behind one-line launchers in `D:\UO\haven-migration`. Each starts
with a read-only discovery of Haven, prints its plan, and changes nothing until its word is typed.

1. **ops-pages** (`-Mode Deploy`, word `deploy`): the folders, the two files, the deploy key (first time: its public
   half is printed; add it on GitHub as a read-only deploy key of `shard-migration`), the token (first time: asked, or
   made; written to Haven's `ops.env` and `D:\UO\haven-ops\token.txt`), then git-pull, waiting for both clones. After
   the cutover it also recreates the page, so a new `server.py` takes effect.
2. **ops-cutover** (`-Mode Cutover`, word `cut over`): stops the game PC's `sl-play-tests`, seeds Haven's data from
   `D:\UO\shard-migration\testing`, starts Haven's page, checks Haven's export against the seed by SHA-256, then
   writes `mode: haven` to `D:\UO\haven-ops\state.json`. A failure before that puts the game PC's page back.
3. **ops-rollback** (`-Mode Rollback`, word `roll back`): takes Haven's export into `testing\` (nothing marked on
   Haven is lost), stops Haven's page, starts the game PC's, and writes `mode: local`.

## The game PC side

- `scripts\Publish-GamePcSnapshot.ps1`, every minute once `scripts\Install-GamePcSnapshotTask.ps1` has registered it:
  the Golem, the queue and the containers, pushed to `:8094/api/ingest/gamepc`. Sends nothing before the cutover.
- `D:\UO\Commit-ShardWork.ps1` runs `shard-migration\tools\opsmirror.py` first: after the cutover, Haven's export
  becomes `testing\` before `progress.py` counts it. It never blocks a commit.
- The console's links and PLAY TESTS tile follow `state.json` (Haven's page, falling back to the mirror with
  "Haven unreachable"); the menu (`nav.js`, Homepage's `custom.js`) follows wherever it was loaded from.
