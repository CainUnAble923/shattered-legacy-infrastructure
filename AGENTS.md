# Shattered Legacy: agent working agreements

**This shard is being migrated from ModernUO to ServUO pub57.** Before making any
substantive change here, read the migration repo:

- `D:\UO\shard-migration\AGENTS.md` - the full working agreements
- `D:\UO\shard-migration\docs\` - decisions, the phased plan, the measured inventory
- GitHub: https://github.com/CainUnAble923/shard-migration

## What this repo is

Infrastructure-as-code for the Shattered Legacy shard. It is **not** a ModernUO fork.
It builds ModernUO from a pinned upstream commit in Docker and layers our code on top.

```
server/customizations/   CANONICAL custom scripts. 111 .cs, 37,581 lines.
server/patches/          real upstream overrides. 11 .patch + 6 full-file replacements.
server/migrations/       data migrations
server/uo/modernuo/      BUILD TREE - see warning below
docker/                  compose stack, Dockerfiles
design/                  system design notes
client-data/ downloads/ backups/   binary, not source
```

## Do not read `server/uo/`

It is a build tree containing a full ModernUO checkout with our customizations already
copied into `Projects/UOContent/Misc/`. **Every custom file exists twice on disk.**
An agent reading both copies will not know which is canonical.

`server/customizations/` is canonical. Edit there. `server/uo/` is generated.

## Two facts that are easy to get wrong

- **ServUO targets `net48`** on every branch, including `p58-wip`. The .NET 10 in its
  setup instructions is the build toolchain, not the target framework.
- **The six-facet client ceiling is an official-client limit only.** ClassicUO reads map
  indices well past 5, and ServUO's core allocates 256 map slots.

## Standing rules

1. Verify against the source trees, not documentation. Neither ModernUO nor ServUO
   documents its content coverage accurately.
2. State explicitly what you could not verify. "Unverified" beats a confident guess.
3. Decisions go in `shard-migration/docs/` as part of the same change that makes them.
4. Do not relitigate the ServUO direction. It was costed with numbers in
   `shard-migration/docs/direction-decision.md`. New evidence welcome, opinions are not.
5. Never commit `Saves/`, `Accounts/`, `client-data/`, `backups/` or logs.
6. Claude is working this project in parallel. Commit and push before handing off.
