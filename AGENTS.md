# Shattered Legacy: agent working agreements

**This shard runs on ModernUO and stays on ModernUO.** ServUO pub57 is a **read-only
content source** we port from, piece by piece, as an open-ended backlog. There is no
migration, no cutover and no ServUO base. Before making any substantive change here,
read the migration repo:

- `D:\UO\shard-migration\AGENTS.md` - the full working agreements
- `D:\UO\shard-migration\docs\direction-decision.md` - why, with the numbers
- `D:\UO\shard-migration\docs\tasks.md` - the single task list
- GitHub: https://github.com/CainUnAble923/shard-migration

## What this repo is

Infrastructure-as-code for the Shattered Legacy shard. It is **not** a ModernUO fork.
It builds ModernUO from a pinned upstream commit in Docker and layers our code on top.

```
server/customizations/   CANONICAL custom scripts. 191 .cs, 42,617 lines.
server/patches/          real upstream overrides. 13 .patch + 5 full-file replacements.
server/migrations/       data migrations
server/uo/modernuo/      BUILD TREE - see warning below
docker/                  compose stack, Dockerfiles
design/                  system design notes
client-data/ downloads/ backups/   binary, not source
```

Counts are as of 2026-09-06 and move with every port. `shard-migration/tools/progress.py`
recomputes them; treat that output as the live number and this block as a snapshot.

## Do not read `server/uo/`

It is a stale partial build tree, and it is **not** a copy of ModernUO. Counted 2026-09-06:
`Configuration/` and `Projects/UOContent/Misc/` only, 161 `.cs` in total. 109 are ours, 52 are
stock ModernUO `Misc/` files, and five of our current top-level customizations are missing from
it, so it predates the F1 recovery. Reading it gets you a second, older copy of our own files
and no way to tell which is canonical.

`server/customizations/` is canonical. Edit there. `server/uo/` is generated and gitignored.

**It is not where you check whether ModernUO has a type.** 161 files against ModernUO's 7,058
types. Use `D:\UO\ModernUO-pinned` if it exists, or the Docker builder stage
(`/build/modernuo`) if it does not. See the migration repo's `AGENTS.md`.

`server/uo/modernuo/Configuration/` is the exception worth knowing: it is bind-mounted into the
live container, so `modernuo.json` there **is** the running server's configuration.

## Two facts that are easy to get wrong

- **ServUO targets `net48`** on every branch, including `p58-wip`. The .NET 10 in its
  setup instructions is the build toolchain, not the target framework. This is one of
  the reasons the shard stays on ModernUO.
- **The six-facet client ceiling is an official-client limit only.** ClassicUO reads map
  indices well past 5, and ModernUO's core is not the constraint either.

## Standing rules

1. Verify against the source trees, not documentation. Neither ModernUO nor ServUO
   documents its content coverage accurately.
2. State explicitly what you could not verify. "Unverified" beats a confident guess.
   A keyword hit is a candidate, not a result: publish the file list beside any count.
3. Decisions go in `shard-migration/docs/` as part of the same change that makes them.
   Agents do not edit `shard-migration/docs/`; findings go in `shard-migration/notes/`.
4. **Do not relitigate the direction.** It was settled with numbers in
   `shard-migration/docs/direction-decision.md`. New evidence welcome, opinions are not.
5. Never commit `Saves/`, `Accounts/`, `client-data/`, `backups/`, `server/uo/` or logs.
6. **Never deploy.** `docker compose up`, restarts, image builds against the live stack
   and anything account-affecting are Chase's decision alone. Test in a throwaway
   loopback-only container with a scratch save directory.
7. Claude is overseeing this project in parallel. Commit before handing off; Chase pushes.

## The layering pattern is not negotiable

The architecture is: **pristine ModernUO pinned to commit `7c9215d97`**, plus additive
`.cs` in `server/customizations/`, plus targeted `.patch` files and full-file `.cs`
replacements in `server/patches/`, assembled at build time by
`docker/uo/apply-patches.sh`.

**This pattern is what makes porting cheap.** It is why there is no Core bucket and why
the custom surface stays a clean, enumerable set instead of a diffuse fork. Ported
ServUO content lands in `server/customizations/` under the same layering rules as
anything else we wrote.

- `D:\UO\ServUO-pub57` is the **pristine reference**. Never edit, never build in it,
  never commit into it. It exists so "is this mine or is this stock?" stays a
  one-minute question.
- `D:\UO\ServUO-work` (if present) is a **scratch tree** for figuring out ports. Not the
  deliverable. Create it with `git clone ServUO-pub57 ServUO-work`, not `git worktree`,
  so the reference repo's `.git` is never written to.
- **Final output lands in `server/customizations/` and `server/patches/`** in this repo.
- **Never commit our code into a ServUO fork, and never vendor ServUO into this repo.**
  A year from now, pulling ModernUO fixes must still be a pinned-commit bump.

## Build with `docker/uo/build.sh`, not `docker build`

Since S4 this is the build command: builder stage, then the shard's own tests under
`server/tests/`, then the image, each gating the next. **`docker build` and `docker compose build`
are internal steps of it.** Run them directly and you get an image with no test gate and no
warning that there wasn't one.

A failing assertion cannot fail `docker build` here: every ModernUO test fixture calls
`NetState.Configure()`, which needs io_uring, which Docker Desktop's default seccomp blocks, and
`docker build` has no `--security-opt`. The BuildKit alternative would break `docker compose
build`, so `build.sh` carries the gate instead. It also refuses to treat "the filter matched no
tests" as a pass.

## `server/tests/` is mirrored into ModernUO's test project

`server/tests/<path>` lands at `Projects/UOContent.Tests/Tests/<path>`, by the same
`mirror_cs_tree()` the customizations use, with the same destination-must-not-already-exist guard.

**A ported subsystem needs a test; ported content does not.** Content is checked by the compiler
and by reading the ServUO source. A subsystem is not: nothing else would catch a pinned-commit
bump that leaves every patch applying while the behaviour quietly stops reaching the player.

## How `apply-patches.sh` reaches the build tree

Since F2 it does three things, and any change must preserve all three:

- Top-level `server/customizations/*.cs` are copied flat into `Projects/UOContent/Misc/`
  via `-maxdepth 1`, with several files deliberately excluded by name because they are
  handled as patches instead.
- Subdirectories under `server/customizations/` are **mirrored by path** into
  `Projects/UOContent/` (`-mindepth 2`). Ported content goes in a subdirectory for this
  reason - it keeps the ServUO tree's structure and avoids basename collisions.
- **A failed patch fails the build.** The script reports every problem it finds, then
  exits non-zero. It must never let a patch fail silently again.

## The fidelity principle

Port unchanged. Ported content should behave the way OSI's does, so players can use
UOGuide and Stratics instead of a custom wiki. A deliberate deviation is a documentation
debt and gets logged in `shard-migration/docs/fidelity-principle.md`.
