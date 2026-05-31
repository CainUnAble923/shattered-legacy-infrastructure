# Extended Resources and Ores

## Purpose

Shattered Legacy extends the classic Ultima Online ore ladder with new resources, facet identities, exploration systems, and guild integration.

Extended resources should support:

- exploration gameplay
- facet identity
- Miners' Compact progression
- crafting diversity
- achievement systems
- survey/cartography systems
- future expedition systems

## Important Implementation Order

Extended ores, ore spawning, resource tables, harvesting systems, ingots, crafting integration, and resource menus must be implemented before the Miners' Compact is developed too deeply.

The mining guild and contract systems depend on a stable extended resource framework.

## Classic Ore Ladder

Classic ore ladder:

| Tier | Ore |
| --- | --- |
| 0 | Iron |
| 1 | Dull Copper |
| 2 | Shadow Iron |
| 3 | Copper |
| 4 | Bronze |
| 5 | Gold |
| 6 | Agapite |
| 7 | Verite |
| 8 | Valorite |

## Extended Ore Ladder

| Tier | Ore | Identity |
| --- | --- | --- |
| 9 | Platinum | refined and high-quality metal |
| 10 | Toxic | poison/corruption-infused ore |
| 11 | Blaze | volcanic and fire-infused ore |
| 12 | Frost | cold/deep-earth ore |
| 13 | Obsidian | void/shadow/deep-earth ore |
| 14 | Mythril | lightweight and refined ore |
| 15 | Adamantium | legendary hardness and endurance |
| 16 | Celestial | endgame legendary ore |

## Facet Identity

### Trammel

- safe baseline mining
- all ores technically possible
- extended ores become very rare after Valorite

### Felucca

- +100% mining yield
- all ores technically possible
- extended ores become very rare after Valorite
- dangerous/high-output mining environment

### Ilshenar

- magical and elemental ore identity
- better Toxic, Blaze, and Frost opportunities

### Malas

- deep-earth and void ore identity
- better Obsidian and Mythril opportunities

### Tokuno

- refined and exotic ore identity
- better Platinum and Mythril opportunities

### Ter Mur

- gargoyle and legendary ore identity
- better Adamantium and Celestial opportunities
- future Gargoyle Pickaxe integration focus

## Facet Contract Rules

Mining spawn tables and mining contracts should not be identical.

### Trammel and Felucca Contracts

Mining gather contracts for Trammel and Felucca should stop at Valorite.

These contracts should not request extended ores.

Valorite should represent the highest-value classic-tier contract.

### Other Facet Contracts

Ilshenar, Malas, Tokuno, and Ter Mur contracts should focus on their extended ore identities.

These contracts should generally avoid requesting Valorite and lower ores.

## Ore Discovery and Reporting

Rare and extended ore contracts should not unlock from skill alone.

Players should:

```text
Discover ore
→ log discovery in Prospector's Logbook
→ report discovery to Survey Archivist
→ unlock future contract eligibility
```

## Prospector's Logbook

The Prospector's Logbook is issued by the Miners' Compact.

### Rules

- given on joining the Miners' Compact
- replacement copies available through guild store using Mining Vouchers and guild standing
- account-backed discovery data
- configurable logging filters
- Iron should not be logged by default

### Suggested Loggable Events

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
- cannot be used for guild turn-ins directly
- cannot immediately unlock contract eligibility

If the receiving player personally mines the matching ore near the copied location, the copied entry should convert into that player's own verified discovery.

## Cartography Integration

Cartography should later support converting copied or verified survey entries into map-based survey tools.

Examples:

- pinned ore maps
- expedition charts
- route maps
- survey atlases

Higher Cartography skill should improve:

- pin accuracy
- search radius
- number of entries supported
- route complexity

## ClassicUO Map Integration

Long-term goal:

```text
Prospector's Logbook
→ Survey Copies
→ Cartography Survey Maps
→ ClassicUO world map integration
```

Potential future features:

- ore pins
- survey overlays
- shared atlas exports
- guild expedition maps
- route planning
- discovery archives

## Extended Ore Hues

Initial proposed hues:

| Ore | Proposed Hue | Identity |
| --- | --- | --- |
| Platinum | 0x0482 | pale silver-blue |
| Toxic | 0x0044 | poison green |
| Blaze | 0x0489 | volcanic orange |
| Frost | 0x0480 | icy blue |
| Obsidian | 0x0455 | dark void-black |
| Mythril | 0x04F2 | bright silver-cyan |
| Adamantium | 0x0450 | deep steel-blue |
| Celestial | 0x0481 | luminous blue-white |

These hues should be validated in-client before finalization.

## Resource System Notes

Ore/resource type should remain authoritative.

Hues are presentation.

Server logic should rely on resource enums/types rather than hue values.

## Future Expansion

Potential future systems:

- legendary deposits
- unstable veins
- Gargoyle Pickaxe escalation events
- regional geology systems
- shared guild atlas data
- expedition mining routes
- deep-earth events
- celestial mining events
