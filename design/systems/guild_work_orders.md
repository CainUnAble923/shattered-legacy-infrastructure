# Guild Work Orders

## Purpose

Guild Work Orders are Shattered Legacy's shared contract and logistics framework.

The system is inspired by Ultima Online Bulk Order Deeds, but redesigned to reduce friction and support modern cross-guild gameplay.

## Design Philosophy

Guild Work Orders should:

- support direct item and resource turn-ins
- avoid nested-deed clutter
- support cross-guild economies
- integrate with guild standing and currencies
- support profession identity and progression

## Small and Large Work Orders

### Small Work Orders

Simple single-resource or single-item requests.

Examples:

```text
Deliver 250 Iron Ingots.
Craft 10 Pickaxes.
Prepare 20 Miner's Rations.
```

### Large Work Orders

Multi-line contracts with larger rewards.

Examples:

```text
Deliver:
- 500 Iron Ingots
- 250 Dull Copper Ingots
- 100 Bronze Ingots
```

```text
Supply Expedition Tools:
- 10 Pickaxes
- 10 Shovels
- 5 Survey Tools
```

## Important Difference From Classic BODs

Large Guild Work Orders should be fulfillable directly with the requested items/resources.

Do not require players to gather and combine many smaller deeds.

The player should be able to insert matching items or resources directly into the Large Work Order.

## Vanilla BOD Modernization

The existing Ultima Online Bulk Order Deed system should later be modernized with the same philosophy.

Goal:

```text
Large BODs should accept direct crafted item turn-ins rather than only completed smaller deeds.
```

The intent is to preserve the recognizable BOD system while removing unnecessary friction.

## Shared Contract Data

Potential shared fields:

```text
OrderId
IssuerGuildId
TargetGuildId optional
DifficultyTier
RequiredItems
RequiredResources
RequiredQuantities
RequiredFacet optional
MinGuildRank
MinGuildStanding
MinSkill
Rewards
StandingReward
CurrencyReward
GoldReward
```

## Contract Eligibility

Contracts should not be generated from skill alone.

Eligibility should consider:

- guild rank
- guild standing
- relevant skill level
- facet access
- prior discoveries or reports
- contract progression

Example:

```text
A player with 300 Mining but Initiate Miners' Compact standing should still receive beginner contracts until they build guild trust.
```

## Cross-Guild Contracts

Guilds should be able to request goods and services from other guild ecosystems.

Examples:

| Requesting Guild | Supplying Guild | Example |
| --- | --- | --- |
| Miners' Compact | Cooks | Miner's Rations |
| Miners' Compact | Alchemists | Prospector's Tonics |
| Miners' Compact | Tinkers | Survey Tools |
| Tinkers' Union | Miners' Compact | Ingots |
| Rangers' League | Cooks | Trail Rations |
| League | Many guilds | Expedition Supplies |

## Facet-Specific Contracts

Some contracts should require activity within a specific facet.

For MVP implementation, these contracts should track progress after acceptance rather than requiring special resource item variants.

Example:

```text
Accept Felucca Ore Shipment Order
→ mine ore in Felucca
→ contract progress increases
```

This follows the same philosophy as modern kill quests which only count progress after the quest is accepted.

## Future Expansion

Potential future features:

- timed work orders
- public guild boards
- player-created contracts
- faction contracts
- expedition supply chains
- region-based contracts
- convoy logistics
- shared guild projects
