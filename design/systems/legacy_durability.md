# Legacy Durability

## Purpose

Legacy Durability defines how important relics and progression items behave when damaged.

The goal is to preserve long-term attachment and upgrade lineage while still supporting repair and maintenance gameplay.

## Design Philosophy

Legacy relics should not permanently disappear because of durability loss.

Instead of:

```text
item breaks forever
```

Shattered Legacy relics should transition through condition states.

## Suggested Condition States

| State | Meaning |
| --- | --- |
| Healthy | Fully functional |
| Worn | Minor wear |
| Damaged | Reduced effectiveness |
| Exhausted | Major restoration required |

## Exhausted State

At Exhausted:

- the relic still exists
- major bonuses may disable
- the item may not upgrade further
- restoration or retempering is required

This preserves item lineage and emotional attachment.

## Repair Layers

### Basic Repairs

Handled through:

- player crafters
- repair contracts
- field kits
- normal repair services

### Advanced Restoration

Handled through:

- guildmasters
- specialist NPCs
- relic restoration systems

Examples:

| Item | Institution |
| --- | --- |
| Jacob's Pickaxe | Miners' Compact |
| Expedition Pack | League Quartermaster |
| Clockwork Scavenger | Tinkers' Union |

## Relationship To Restoration Registry

Legacy Durability and the Restoration Registry are separate systems.

### Legacy Durability

Handles:

- wear
- repairs
- exhaustion
- maintenance

### Restoration Registry

Handles:

- lost items
- deleted items
- reissue eligibility
- missed unlocks

## Player Crafter Importance

Player crafters should remain valuable.

Guild NPCs should not replace:

- repair culture
- field maintenance
- profession interaction

The preferred model is:

| Activity | Source |
| --- | --- |
| normal repairs | players |
| repair contracts | players |
| field restoration | kits/contracts |
| exhausted restoration | guild specialists |

## First Planned Pilot

The first planned pilot for Legacy Durability is:

```text
Jacob's Pickaxe
```

through the:

```text
Miners' Compact restoration framework
```
