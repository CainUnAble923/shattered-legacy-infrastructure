# Shattered Legacy Design Documentation

This folder contains detailed design specifications for planned and in-progress Shattered Legacy gameplay systems.

## Documentation Roles

| File or Folder | Purpose |
| --- | --- |
| `../README.md` | Operational/deployed state, deployment notes, commands, and live implementation details |
| `../roadmap.md` | High-level vision, system pillars, dependency order, and implementation phases |
| `design/` | Detailed implementation design docs for systems, guilds, items, and gameplay loops |

Keep the roadmap readable. When a system needs enough detail for coding, place that detail here and link or summarize it from the roadmap.

## Recommended Structure

```text
design/
├── README.md
├── guilds/
│   └── miners_compact.md
├── systems/
│   ├── guild_framework.md
│   ├── league_of_extraordinary_citizens.md
│   ├── legacy_durability.md
│   └── restoration_registry.md
├── items/
└── quests/
```

## Design Principles

- Systems should have in-world homes: guild halls, lodges, archives, quartermasters, and NPCs.
- Prefer reusable frameworks over one-off implementations.
- Support one-character-friendly progression.
- Avoid permanent missables.
- Reduce friction without removing immersion.
- Keep operational facts in `../README.md`; keep future architecture in `../roadmap.md`; keep implementation-level design here.

## Current Priority

The first detailed guild implementation should be the Miners' Compact, using the existing account data, guild, achievement, bulletin, Renown, and restoration-registry foundations documented in the operational README.
