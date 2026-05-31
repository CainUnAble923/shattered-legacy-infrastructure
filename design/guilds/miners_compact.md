# Miners' Compact

## Purpose

The Miners' Compact is the first deep profession guild implementation for Shattered Legacy.

The Compact is not just a mining vendor guild. It acts as Britannia's:

- geological authority
- ore-survey institution
- excavation sponsor
- relic restoration authority for mining relics
- cave-expedition organization
- ore logistics network

The Miners' Compact serves as the pilot implementation for the broader institutional guild framework.

## Design Goals

- Make mining part of exploration gameplay.
- Give mining systems physical homes and NPC infrastructure.
- Support long-term profession identity.
- Establish reusable guild progression patterns.
- Pilot restoration and legacy systems.

## Important Dependency

Extended ores, harvesting systems, resource spawning, ingot integration, and crafting resource menus must exist before the Miners' Compact can be fully implemented.

The guild relies heavily on the extended resource framework.

## Rank Structure

| Rank | Standing |
| --- | --- |
| Initiate | 0 |
| Apprentice | 1,000 |
| Journeyman | 5,000 |
| Surveyor | 15,000 |
| Master Delver | 40,000 |
| Deepwarden | 80,000 |
| Legendary Prospector | 150,000 |

Ranks should unlock systems, contracts, and restoration privileges rather than functioning only as titles.

## Standing Sources

Standing should primarily come from mining-related gameplay.

Examples:

- mining ore
- discovering rare veins
- ore shipment contracts
- geological surveys
- cave expeditions
- restoring mining relics
- mapping dangerous caverns
- reporting legendary deposits
- completing Felucca contracts
- supplying other guilds

## Guild Currency

Primary guild currency:

```text
Mining Vouchers
```

Mining Vouchers should support:

- relic restoration
- relic upgrades
- ore survey purchases
- mining logistics tools
- scavenger modules
- cosmetics
- expedition supplies
- Prospector's Logbook replacements

## Guild NPC Roles

| NPC | Role |
| --- | --- |
| Compact Guildmaster | Membership and standing overview |
| Quartermaster | Contracts and voucher exchange |
| Master Forgemaster | Restoration and upgrades |
| Survey Archivist | Ore surveys and geological records |
| Deep Delver Marshal | Dangerous expedition contracts |
| Relic Keeper | Registry-linked relic restoration |

## Jacob's Pickaxe

Jacob's Pickaxe is intended to be the first full pilot relic for:

- Restoration Registry
- Legacy Durability
- relic upgrades
- guild restoration
- profession identity
- linear tier progression
- extended ore progression

### Registry Key

```text
legacy.jacobs_pickaxe
```

## Upgrade Path

The upgrade path is a single linear chain. Each upgrade consumes the previous tier pickaxe.

```text
Tier 1 → Tier 2 → Tier 3 → Tier 4 → Tier 5
```

### Tier 1 — Jacob's Pickaxe

Source:

```text
New Haven mining quest
or Miners' Compact legacy onboarding
```

Stats: 150 uses, +5 Mining skill bonus

Purpose:

- starter mining relic
- restoration registry pilot
- profession identity relic

### Tier 2 — Jacob's Reinforced Pickaxe

Theme:

```text
reliable guild-restored mining tool
```

Stats: 400 uses, +10 Mining skill bonus

Requirements:

- Apprentice rank (1,000 standing)
- Mining 75.0+
- 50 Vouchers + 1,000 Iron + 250 Dull Copper + 25,000 gp
- Existing T1 pickaxe (consumed)

### Tier 3 — Jacob's Prospector Pickaxe

Theme:

```text
surveys, discovery, mapping, rare ore tracking
```

Stats: 600 uses, +18 Mining skill bonus

Special: Prospector's Insight — on each new ore discovery, immediately credits +3 Mining Vouchers.

Prospector tier interacts strongly with:

- ore discovery
- survey reporting
- Prospector's Logbook
- Survey Archivist
- Cartography integration (future)
- Miner's Rations (future)

Requirements:

- Surveyor rank (15,000 standing)
- Mining 80.0+
- 200 Vouchers + 1,500 Iron + 500 Agapite + 50,000 gp
- Existing T2 pickaxe (consumed)

Registry key: `legacy.jacobs_prospector_pickaxe`

### Tier 4 — Jacob's Deepdelver Pickaxe

Theme:

```text
dangerous mining, Felucca mining, unstable deposits
```

Stats: 800 uses, +22 Mining skill bonus

Special: Deepdelver's Advantage — +1 ore per yield when mining in Felucca.

Deepdelver tier interacts strongly with:

- Felucca mining
- dangerous contracts
- Gargoyle Pickaxe events (future)
- mining tonics (future)
- deep-earth systems (future)

Requirements:

- Master Delver rank (40,000 standing)
- Mining 90.0+
- 350 Vouchers + 2,000 Iron + 500 Valorite + 150,000 gp
- Existing T3 pickaxe (consumed)

Registry key: `legacy.jacobs_deepdelver_pickaxe`

### Tier 5 — Jacob's Worldbreaker Pickaxe

Theme:

```text
mastery of discovery and deep excavation
```

Stats: 1,200 uses, +25 Mining skill bonus

Special: Combines T3 and T4 bonuses. Additionally, Worldbreaker's Edge — +1 ore per yield everywhere (stacks with Felucca bonus for +2 total in Felucca).

Requirements:

- Deepwarden rank (80,000 standing)
- Mining 100.0 (GM)
- 500 Vouchers + 3,000 Iron + 1,000 Valorite + 200 Adamantium + 300,000 gp
- Existing T4 pickaxe (consumed)

Registry key: `legacy.jacobs_worldbreaker_pickaxe`

## Legacy Durability

Mining relics should not permanently break.

```text
Healthy → Worn → Damaged → Exhausted
```

At Exhausted:

- bonuses may disable
- upgrades may lock
- restoration becomes required

## Repair Philosophy

The Compact should support:

- player crafter repair culture
- field repair kits
- repair contracts
- guild restoration for exhausted relics

Guildmasters should augment player crafting rather than replace it.

## Guild Work Orders

The Miners' Compact should heavily use the shared Guild Work Order framework.

### Small Work Orders

Examples:

```text
Deliver 250 Iron Ingots.
Deliver 100 Dull Copper Ingots.
Craft 10 Pickaxes.
```

### Large Work Orders

Examples:

```text
Deliver:
- 500 Iron Ingots
- 250 Dull Copper Ingots
- 100 Bronze Ingots
```

Large Work Orders should accept direct item/resource turn-ins rather than requiring smaller deeds.

## Contract Eligibility

Contracts should not be based on skill alone.

Eligibility should consider:

- Compact rank
- Compact standing
- Mining skill
- facet progression
- prior discoveries
- reported discoveries

Example:

```text
A player with 300 Mining but Initiate standing should still receive beginner contracts.
```

## Facet Mining Philosophy

### Trammel

- safe mining environment
- all ores technically possible
- extended ores rare after Valorite

### Felucca

- +100% mining yield
- dangerous/high-output mining environment
- extended ores rare after Valorite

### Other Facets

Other facets should specialize in extended ore identities.

## Facet Contract Rules

### Trammel and Felucca Contracts

Trammel and Felucca mining contracts should stop at Valorite.

Valorite should represent the highest-value classic-tier contract.

Extended ores may still exist there but should not normally appear in those contract pools.

### Other Facet Contracts

Ilshenar, Malas, Tokuno, and Ter Mur contracts should focus on their extended ore identities.

These contracts should generally avoid requesting Valorite and lower ores.

## Ore Discovery and Survey Reporting

Rare ore contracts should not unlock from skill alone.

Players should:

```text
Discover ore
→ log discovery
→ report discovery to Survey Archivist
→ unlock future contract eligibility
```

## Prospector's Logbook

The Prospector's Logbook is issued when joining the Miners' Compact.

### Rules

- replacement copies available from guild store
- replacement requires Mining Vouchers and guild standing
- account-backed discovery data
- configurable logging filters
- Iron excluded by default

### Logged Events

Examples:

- first ore discovery
- first discovery in a facet
- rare vein discovery
- extended ore discovery
- Gargoyle Pickaxe event
- legendary deposit discovery

## Survey Copies

Logbook entries can be copied using Inscription.

Copied entries:

- are informational
- can be traded/shared
- cannot immediately grant guild credit
- cannot immediately unlock contracts

If the receiving player mines the matching ore near the copied location, the copied entry should convert into that player's own verified discovery.

## Cartography Integration

Future Cartography systems should support:

- pinned ore maps
- expedition charts
- route maps
- survey atlases
- ClassicUO world map integration

## Mining Consumables

Mining consumables should build on the shared Food and Drink Buff system.

### Tonics

Tonics primarily increase ore yield.

Created primarily by:

```text
Alchemy
```

High-tier tonics may intentionally create extremely high yields in Felucca.

### Rations

Rations primarily increase ore rarity/tier.

Created primarily by:

```text
Cooking
```

Rations should function similarly to time-based ore-tier escalation.

## Cross-Guild Economy

The Miners' Compact should heavily support cross-guild supply relationships.

Examples:

| Supplier | Goods |
| --- | --- |
| Cooks | Miner's Rations |
| Alchemists | Prospector's Tonics |
| Tinkers | Survey Tools |
| Smiths | Tool Repairs |
| Miners | Ingots and ore |

## Gargoyle Pickaxe Integration

Gargoyle Pickaxes should become part of advanced Compact progression.

Potential uses:

- dangerous excavation tools
- ore escalation mechanics
- unstable vein events
- advanced contracts
- Tier 3/Tier 4 Jacob's Pickaxe requirements

## Ore Survey System

Long-term mining gameplay should support:

- ore surveys
- geological records
- map markers
- shared atlas data
- rare vein tracking
- expedition routes
- ClassicUO map integration

## Clockwork Scavenger Integration

Future Miners' Compact upgrades may include:

- ore recovery modules
- gem extraction modules
- golem salvage systems
- cave debris processors

These upgrades should later integrate with the Tinkers' Union.

## MVP Implementation Goal

Initial implementation target:

```text
Extended ore framework
basic ore spawning
resource menu integration
League referral
join Miners' Compact
complete mining contract
earn Mining Vouchers
restore Jacob's Pickaxe
upgrade Jacob's Pickaxe to Tier 2
```

Do not attempt full Tier 4 or full expedition systems immediately.

This guild should be implemented as a vertical slice prototype for the broader guild framework.
