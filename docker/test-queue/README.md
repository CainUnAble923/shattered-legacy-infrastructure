# Play-test queue

> **This runs on the Windows box (`192.168.1.58`), not on Haven,** because the files it reads and writes
> live here. LAN only: http://192.168.1.58:8093/ . **Never route it through NPM**; it is an internal
> checklist with no login. Start or recreate it with `Start-TestQueue.ps1` in this folder.

cc-P83 (2026-10-12). One page listing every play-test that is waiting, grouped by prompt, newest first.
Chase marks each **Pass**, **Fail** (with a one-line note) or **Later** from any browser on the home
network, phone included. It replaced `sl-test-queue`, the read-only nginx view of
`docs/client-test-queue.md`.

## It fills itself

`server.py` runs the importer on start and every 60 seconds. A prompt whose notes file
(`D:\UO\shard-migration\notes\cc-Pnn-*.md`) ends with the line `Pnn DONE` has its in-game checklist on the
page within a minute, with no overseer or Chase step. The rules for what counts as a play-test step are in
`D:\UO\shard-migration\tools\playtests.py` (top of the file). That is the only copy: `server.py` imports it,
and `D:\UO\shard-migration\tools\Import-PlayTests.ps1` runs it from the host.

## Where the data is

- `D:\UO\shard-migration\testing\play-tests.json`: every item and its result. Pretty-printed, stable key
  order, so git diffs read. The `summary` block at the top has the counts and every Fail.
- `D:\UO\shard-migration\testing\SUMMARY.md`: the same, rewritten on every write, for the overseer.
- `D:\UO\shard-migration\testing\feature-ideas.json` (cc-P90): the Ideas tab's inbox. Ids `I-NNN` come from a
  stored counter and are never reused. The same atomic write, under its own lock (`feature-ideas.json.lock`).
  The rules are in `D:\UO\shard-migration\tools\ideas.py`. cc-P97: declining or deleting needs a reason; a deleted
  idea keeps its id, title and reason under `deleted`.
- `D:\UO\shard-migration\testing\bug-reports.json` (cc-P97): the Bugs tab's inbox. Ids `B-NNN` from a stored
  counter, never reused; a deleted report keeps its id, title and reason under `deleted`. Screenshots (PNG or JPG,
  2 MB at most, 3 per report) are in `testing\bug-reports\files\`. Same lock and atomic write. The rules are in
  `D:\UO\shard-migration\tools\bugs.py`. A report's shard version comes from the test world's `shard-commit.json`
  (written by `Start-TestShard.ps1` after a healthy start), mounted read only.
- `D:\UO\shard-migration\testing\decisions.json` (cc-P146): the answers Chase gives to decisions on the page, each
  with who and when, and the overseer's "taken into P<nn>". The decisions themselves are read from each notes file's
  decisions section by `D:\UO\shard-migration\tools\decisions.py`, with the hand-written
  `testing\decisions-overlay.json` (wording, older prompts only noted, answers given elsewhere, moot ones, and the
  play-test items they replace). Same lock and atomic write; exported with the other marks.

- `D:\UO\shard-migration\testing\sessions.json` (cc-P164): **the overseer's file** (nothing writes it): the page's
  sessions, each a title, a one-line why, a "before you start" list and item ids in order. `/api/items` carries
  `sessions` (`tools/playsessions.py`): those, automatic ones for every other open item of Chase's (by Where, then
  prompt, newest first, 12 at most), Probably stale, and `upNext`. The page opens on **Up next**; **All items** is the
  full list. A "needs: rebuild test shard" tag compares a prompt's import time with the test shard's deploy record
  (`--deployed`, or on Haven the game PC snapshot's `testShard`).

Every write is under a lock file (`play-tests.json.lock`) and goes through a temp file and a rename. So two
browser tabs, the service's own importer and `Import-PlayTests.ps1` on the host never lose each other's
results.

## API

| Call | What |
|---|---|
| `GET /nav.js` | the shared menu (cc-P88): Play tests, Shard status, Dashboard. The one source for all three pages; its `PAGES` table holds each page's address |
| `GET /api/items` | the whole document |
| `GET /api/summary` | `open`, `failed`, `passed`, `later`, `blocked`, `total`, `unloggedFails`, `generatedAt`, `ideas_new` (cc-P90), `bugs_new` (cc-P97) and `answers_new` (cc-P146). The dashboard's Play tests widget reads this |
| `POST /api/result` | `{"id", "status": "pass|fail|later|open", "note"}`; a fail needs a note |
| `POST /api/bug` | `{"id", "bug": "D86"}`: the Fail is logged in `bug-list.md` |
| `POST /api/manual` | `{"what", "expect"}`: adds an `M-NNN` item |
| `POST /api/import` | runs the importer now |
| `POST /api/sessions/later` | cc-P164: `{"items": [{"id", "note"}, ...]}`: each item Later with its own note, in one write (Probably stale's "Mark all Later"); every id must exist or nothing changes |
| `GET /api/ideas` | cc-P90: the ideas, their counts, the areas and statuses, and the F-NN entries of `docs/feature-queue.md` (read only) |
| `GET /api/ideas/copy` | every `new` idea as plain text for the features chat, and their ids |
| `POST /api/ideas/add`, `/edit`, `/status`, `/taken`, `/delete` | change an idea (cc-P97: `/delete` needs a `reason`) |
| `GET /api/bugs` | cc-P97: the reports, their counts, severities, areas and statuses, and the deployed test-shard version |
| `GET /api/bugs/copy` | every `new` report as plain text for the overseer chat, and their ids |
| `GET /api/bugs/file/B-001-1.png` | a screenshot |
| `POST /api/bugs/add`, `/edit`, `/status`, `/taken`, `/delete`, `/shot` | change a report; `/shot` takes one picture as base64 |
| `GET /api/decisions/copy` | cc-P146: the answers not yet taken, as plain text for the overseer, and their ids. `/api/items` carries the decisions themselves (`decisions`), each item's `where`, and the counts (`answers`) |
| `POST /api/decisions/answer` | `{"id": "P77-D3", "choice": "<an option's label>" or "built", "text"}`: one or both; refused once taken |
| `POST /api/decisions/later` | `{"id"}`: "Ask me later" |
| `POST /api/decisions/taken` | `{"ids", "prompt": "P150", "expect": {id: {"rev"}}}`: the overseer took them into that prompt; `"undo": true` puts them back (`expect` then also names `takenInto`). cc-P610: an answer changed since the page or the copy saw it, or a late undo, is refused (400) and nothing changes; `/api/decisions/copy` returns the `expect` to send |

## Run it by hand (testing)

```
python server.py --root <a copy of shard-migration> --lib D:\UO\shard-migration\tools --bind 127.0.0.1 --port 18093
```

## Running on Haven (cc-P105, step H1 of cc-P89's plan)

All of this is **off by default**: with none of it set, the service serves the same routes and writes the same files
as before (checked side by side against the previous `server.py`; notes `cc-P105-haven-h1-host-agnostic.md`).

| Setting | What |
|---|---|
| `PLAYTESTS_DATA` (`--data`) | Where `testing/` is read and written: play tests, `SUMMARY.md`, ideas, bug reports and their screenshots. Default: the notes root, as before. On Haven, a data volume apart from the read-only notes clone. |
| `PLAYTESTS_LAN_PORT` (`--lan-port`), `PLAYTESTS_LAN_BIND` | A second listener, for Haven's LAN address only. Every request needs `Authorization: Bearer <token>`. Not started without a token. |
| `PLAYTESTS_TOKEN` or `PLAYTESTS_TOKEN_FILE` | That token. Never in git. |
| `ACCESS_TEAM_DOMAIN` and `ACCESS_AUD` | Both set: every request but `/nav.js` must carry a valid Cloudflare Access token (`Cf-Access-Jwt-Assertion` or the `CF_Authorization` cookie), checked by `shard-migration/tools/accessjwt.py`, and its email is the writer, `by`. |
| `REPO_STAMP` | The pull container's stamp file (`<commit> <ISO time>`), for "repo as of". Without it, git when installed, else empty. |

The token listener:

- `GET /api/export`: every file under `testing/` as one bundle (`shard-migration/tools/opsdata.py`; `write_bundle`
  puts it back as files, checked against each file's SHA-256).
- `POST /api/ingest/gamepc`: the game PC's snapshot (JSON, 64 KB at most), stored in `<data>/ops/gamepc.json` with the
  time it arrived.
- `GET /api/gamepc`: that snapshot, its age, and `offlineSince` once it is older than 3 minutes.

`/api/items` `service` also has `gamepc`, `repo` and `identity`. The page shows "Repo as of", "Game PC offline since"
and "Signed in as" in its footer, and "by" on results, ideas and bug reports, when there is something to show.

A scratch Haven-mode run, as cc-P105 did it:

```
set PLAYTESTS_DATA=<scratch>\data
set PLAYTESTS_TOKEN_FILE=<scratch>\token.txt
python server.py --root D:\UO\shard-migration --lib D:\UO\shard-migration\tools --bind 127.0.0.1 --port 18093 --lan-port 18094 --lan-bind 127.0.0.1
```
