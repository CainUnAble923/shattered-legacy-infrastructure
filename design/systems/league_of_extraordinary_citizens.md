# League of Extraordinary Citizens

## Purpose

The League of Extraordinary Citizens is the shard-wide civic, adventuring, and expedition institution for Shattered Legacy.

The League is not a profession guild. Instead, it acts as:

- onboarding/tutorial authority
- achievement and Renown authority
- adventurer registry
- monster hunting board
- expedition sponsor
- public bulletin office
- citizen registry
- civic contract office
- public logistics network
- connective layer between profession guilds

## Design Philosophy

The League exists to unify shard-wide progression while still allowing profession guilds to maintain strong identities.

The League should:

- explain the world
- introduce players to systems
- recognize accomplishments
- route players toward guilds and progression paths
- provide adventuring contracts
- provide public commission boards
- act as the umbrella institution for civic progression

The League should not replace profession guilds.

## Adventurer Guild Identity

The League should serve the role of a Britannian adventurer guild while remaining grounded in Shattered Legacy lore.

It should support:

- monster hunting contracts
- boss hunting contracts
- expedition requests
- exploration reports
- gathering requests for lower ranks
- profession guild referrals
- player-submitted Citizen Commissions

The tone can borrow the satisfying progression structure of fantasy adventurer guilds while remaining serious in-world.

## Metal Rank Ladder

League ranks should use the shard's ore/metal progression language.

| Rank | Concept |
| --- | --- |
| Iron Citizen | newly registered adventurer |
| Dull Copper Citizen | trusted local helper |
| Shadow Iron Citizen | proven field operative |
| Copper Citizen | expedition-capable adventurer |
| Bronze Citizen | respected contractor |
| Gold Citizen | regional adventurer |
| Agapite Citizen | elite expeditionary |
| Verite Citizen | veteran League operative |
| Valorite Citizen | renowned hero |
| Platinum Citizen | legendary adventurer |
| Toxic Citizen | dangerous field specialist |
| Blaze Citizen | champion-level hunter |
| Frost Citizen | master explorer |
| Obsidian Citizen | deep-realm operative |
| Mythril Citizen | realm-renowned figure |
| Adamantium Citizen | world-class League champion |
| Celestial Citizen | near-mythic adventurer |

Profession guild ranks remain separate from League ranks.

Example:

```text
League Rank: Verite Citizen
Miners' Compact Rank: Surveyor
```

## MVP Responsibilities

The first implementation pass should remain intentionally lightweight.

### Initial Features

- League membership
- initial metal citizen rank
- Renown placeholder
- introductory quest flow
- guild directory
- New Haven League representative
- League Dispatch integration
- future-facing hooks for achievements and expedition systems

## Shared Data Concepts

Suggested fields:

```text
league.joined
league.rank
league.renown
league.dispatches_seen
league.intro_completed
league.first_profession_referral
league.first_contract_completed
```

## Renown

Renown is a shard-wide civic progression currency.

Renown is not intended to function as gold.

Future Renown uses:

- achievement rewards
- expedition upgrades
- cosmetics
- special unlocks
- registry support
- League services
- Citizen Commission rewards
- contract board permissions

## New Haven League Office

The League should have a visible representative in New Haven.

Suggested NPC:

```text
League Registrar
```

Suggested office concept:

```text
League of Extraordinary Citizens — New Haven Field Office
```

Potential office functions:

- League registration
- tutorial quests
- League Dispatch access
- profession guild referrals
- citizen rank explanation
- public commission board
- early contract board

## Introductory Flow

Example MVP tutorial:

1. Speak to League Registrar.
2. Join the League.
3. Receive Iron Citizen rank.
4. Learn about Renown, guilds, relics, commissions, and expedition systems.
5. Visit a profession guild representative.
6. Return to the League.
7. Receive introductory Renown.

The first active profession referral should be the Miners' Compact.

## Profession Guild Integration

The League should direct players toward active profession guilds.

The first active pilot guild is the Miners' Compact.

Example dialogue:

```text
The Miners' Compact records ore claims, restores mining relics, and sponsors dangerous excavation contracts.
```

The League should eventually refer players to:

- Miners' Compact
- Smiths' Fellowship
- Tinkers' Union
- Rangers' League
- Arcane Society
- Healers' Covenant
- other profession institutions

## League Contracts

League Contracts are NPC/system-generated adventuring and civic requests.

Examples:

- beginner gathering requests
- local monster hunts
- dungeon clearing contracts
- boss hunting contracts
- exploration reports
- expedition supply requests
- world event notices

These should share backend concepts with Guild Work Orders and Citizen Commissions where possible.

## Citizen Commissions

Citizen Commissions are player-created public requests managed through the League.

They allow citizens to request resources, crafted goods, consumables, trophies, expedition supplies, or other materials from other players.

Players can offer rewards such as:

- gold
- Renown
- guild currency
- items or resources later

### Purpose

Citizen Commissions make the League a public logistics network and player economy hub.

They should complement vendors, Guild Work Orders, and League Contracts rather than replace them.

| System | Source | Purpose |
| --- | --- | --- |
| Vendors | players/NPCs | passive economy |
| Guild Work Orders | institutions | profession logistics |
| League Contracts | system/NPC | adventuring and civic progression |
| Citizen Commissions | players | player-driven requests and logistics |

### Example Commissions

```text
Deliver 2,000 Iron Ingots.
Reward: gold + Renown.
```

```text
Deliver 50 Miner's Rations.
Reward: gold + Mining Vouchers.
```

```text
Deliver 20 Prospector's Tonics.
Reward: gold + Renown.
```

```text
Deliver mimic cores, dragon scales, or other trophies.
Reward: gold + Renown.
```

### Rank Permissions

Suggested permission model:

| League Rank | Permission |
| --- | --- |
| Iron Citizen | accept commissions |
| Bronze Citizen | create small commissions |
| Steel Citizen | create larger commissions |
| Silver+ equivalent | create public/high-value commissions |

Possible safeguards:

- posting fee
- expiration timer
- rank requirement
- reward escrow
- commission limit per account

### Shared Architecture

Citizen Commissions should reuse the same underlying order/contract framework as Guild Work Orders and League Contracts.

Shared needs:

- item/resource requirement tracking
- quantity progress
- delivery validation
- expiration handling
- reward escrow/distribution
- permission checks
- rank checks

Avoid creating three unrelated contract systems.

## League Dispatch

The League Dispatch serves as the shard bulletin/MOTD framework.

Potential future uses:

- update notices
- world events
- expedition reports
- invasions
- guild announcements
- rare discoveries
- public commission highlights

## Future Expansion

The League is expected to expand later into:

- achievement presentation
- expedition systems
- Citizen's Expedition Pack progression
- advanced Renown systems
- exploration records
- civic reputation
- seasonal records
- monster hunting boards
- public commission boards
- player-funded expedition requests
