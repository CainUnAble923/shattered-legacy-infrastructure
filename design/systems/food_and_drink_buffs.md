# Food and Drink Buffs

## Purpose

Food and drink should be useful in Shattered Legacy without becoming a survival tax.

The shard should use the existing UO eating/drinking/fullness system and messages where possible. Do not build a complex hunger/thirst survival system unless there is a later explicit design reason.

## Design Philosophy

```text
Not eating or drinking = normal baseline.
Eating and drinking = slightly better prepared baseline.
Special provisions = activity-specific bonuses.
```

Avoid:

- starvation penalties
- thirst penalties
- death from hunger or thirst
- frequent nag messages
- complex survival UI

## Vanilla Food and Drink

Vanilla food and drink should grant modest generic bonuses based on the existing food/drink state.

Examples:

| State | Concept |
| --- | --- |
| Fed | small HP/stamina support |
| Hydrated | small mana/stamina support |
| Fed + Hydrated | better general baseline |

The exact bonuses should remain modest so food is useful but not mandatory.

Potential generic bonuses:

- small HP regeneration bonus
- small stamina regeneration bonus
- small mana regeneration bonus
- small carry weight bonus
- small skill gain support

## Specialized Provisions

Custom foods and drinks can provide activity-specific bonuses.

### Rations

Rations are food/provision items, often created by Cooks.

For mining:

```text
Miner's Rations increase ore rarity/tier for the vein being mined.
```

Rations affect quality/rarity rather than quantity.

### Tonics

Tonics are drink/potion items, often created by Alchemists.

For mining:

```text
Prospector's Tonics increase ore yield.
```

Tonics affect quantity rather than rarity.

## Mining Examples

| Provision | Source | Effect |
| --- | --- | --- |
| Miner's Ration | Cooking / Miners' Compact | ore-tier/rarity boost |
| Deepdelver Stew | Cooking | stronger ore-tier/rarity boost |
| Prospector's Tonic | Alchemy / Miners' Compact | ore yield boost |
| Rich Vein Tonic | Alchemy | stronger ore yield boost |
| Stoneblood Elixir | Alchemy | high-tier yield boost and possible relic durability support |

## Stacking Philosophy

Suggested active slots:

- one generic/specialized food effect
- one generic/specialized drink or tonic effect

Do not allow stacking many foods or many tonics simultaneously.

## Felucca and High-Tier Tonics

Felucca should remain the high-risk/high-reward mining facet.

Basic yield bonuses can stack additively by default, but high-tier guild-crafted tonics may intentionally create very high yields in Felucca.

Design principle:

```text
Felucca is allowed to be the exceptional yield environment when combined with rare or expensive tonics.
```

This high output should require meaningful risk and resource investment.

## Cross-Guild Economy

Food and drink buffs create natural cross-guild demand.

Examples:

- Cooks create Miner's Rations for Miners' Compact contracts.
- Alchemists create Prospector's Tonics for Miners' Compact contracts.
- Rangers may later use trail rations.
- Warriors may later use battle feasts.
- Arcane guilds may later use scholar teas or mana infusions.

This system should support future Guild Work Orders.
