# Guild Framework

## Purpose

The guild framework provides the shared architecture used by all major institutions in Shattered Legacy.

Guilds are not simple vendors or title systems. They are gameplay infrastructure layers tied to progression, restoration, logistics, contracts, achievements, and exploration.

## Design Goals

- Support multi-guild participation on a single primary character.
- Avoid mutually-exclusive profession choices.
- Provide long-term progression through ranks, standing, and guild currencies.
- Give gameplay systems physical homes in the world.
- Support future guild expansion without major rewrites.

## Shared Concepts

### Guild Membership

Membership should be account-aware and persist through `ClusterFAccountData`.

Suggested shared fields:

```text
OrganizationId
Joined
Rank
Standing
Currencies
Flags
```

Example IDs:

```text
league.citizens
guild.miners_compact
guild.smiths_fellowship
guild.tinkers_union
```

## Shared Guild Features

All guilds should eventually support:

- joining/leaving
- standing gain
- rank progression
- guild currency
- contracts
- restoration services
- achievement hooks
- bulletin/dispatch integration
- reputation-aware dialogue

## Standing Philosophy

Standing should primarily come from participating in the guild's gameplay identity.

Avoid repetitive daily-quest style progression.

Examples:

| Guild | Standing Sources |
| --- | --- |
| Miners' Compact | mining, surveys, cave contracts |
| Smiths' Fellowship | crafting, repairs, reforging |
| Rangers' League | exploration, tracking, hunting |
| Tinkers' Union | gadgets, salvage, scavenger upgrades |

## Guild Currencies

Guild currencies are institution-specific progression resources.

Examples:

| Guild | Currency |
| --- | --- |
| League of Extraordinary Citizens | Renown |
| Miners' Compact | Mining Vouchers |
| Smiths' Fellowship | Smithing Seals |
| Tinkers' Union | Engineering Scrip |

Currencies should support:

- restoration costs
- upgrades
- cosmetics
- contracts
- logistics tools
- progression unlocks

## Guildmasters

Guildmasters should become meaningful NPCs rather than decorative vendors.

Potential NPC roles:

- Quartermaster
- Archivist
- Forgemaster
- Expedition Marshal
- Relic Keeper
- Surveyor

## Guild Integration With Other Systems

Guilds are expected to integrate heavily with:

- Restoration Registry
- Legacy Durability
- Achievement framework
- Expedition systems
- Clockwork Scavenger
- League Dispatch bulletins
- Cartography/map systems

## Current Pilot Guild

The Miners' Compact is the first deep implementation target for the guild framework.
