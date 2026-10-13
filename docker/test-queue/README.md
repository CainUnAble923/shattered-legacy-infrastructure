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
  The rules are in `D:\UO\shard-migration\tools\ideas.py`.

Every write is under a lock file (`play-tests.json.lock`) and goes through a temp file and a rename. So two
browser tabs, the service's own importer and `Import-PlayTests.ps1` on the host never lose each other's
results.

## API

| Call | What |
|---|---|
| `GET /nav.js` | the shared menu (cc-P88): Play tests, Shard status, Dashboard. The one source for all three pages; its `PAGES` table holds each page's address |
| `GET /api/items` | the whole document |
| `GET /api/summary` | `open`, `failed`, `passed`, `later`, `blocked`, `total`, `unloggedFails`, `generatedAt`, and `ideas_new` (cc-P90). The dashboard's Play tests widget reads this |
| `POST /api/result` | `{"id", "status": "pass|fail|later|open", "note"}`; a fail needs a note |
| `POST /api/bug` | `{"id", "bug": "D86"}`: the Fail is logged in `bug-list.md` |
| `POST /api/manual` | `{"what", "expect"}`: adds an `M-NNN` item |
| `POST /api/import` | runs the importer now |
| `GET /api/ideas` | cc-P90: the ideas, their counts, the areas and statuses, and the F-NN entries of `docs/feature-queue.md` (read only) |
| `GET /api/ideas/copy` | every `new` idea as plain text for the features chat, and their ids |
| `POST /api/ideas/add`, `/edit`, `/status`, `/taken`, `/delete` | change an idea |

## Run it by hand (testing)

```
python server.py --root <a copy of shard-migration> --lib D:\UO\shard-migration\tools --bind 127.0.0.1 --port 18093
```
