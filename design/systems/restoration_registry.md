# Restoration Registry

## Purpose

The Restoration Registry exists to prevent permanent progression loss for major legacy systems in Shattered Legacy.

The registry records whether a player has legitimately unlocked a legacy item, relic, or progression path before.

The goal is:

```text
No permanent missables.
No infinite duplication.
```

## Design Philosophy

The registry supports the shard's one-character-friendly progression model.

Players should not be permanently punished because:

- they deleted an important item
- a relic broke or was lost
- they missed a New Haven quest
- they out-skilled a training quest
- they joined the shard later

## Suggested Registry Data

Potential fields:

```text
RegistryKey
Unlocked
OriginalSource
UnlockDate
RestorationCount
LastRestorationDate
ActiveCopy
Tier
Branch
Metadata
```

## Example Keys

```text
legacy.jacobs_pickaxe
legacy.pet_mimic
legacy.clockwork_scavenger
legacy.citizens_expedition_pack
```

## Restoration Rules

Recommended baseline rules:

- one active copy check
- restoration cost
- guild/institution involvement
- material requirements
- optional cooldowns

## Institutions

Restoration should happen through appropriate world institutions.

Examples:

| Relic | Institution |
| --- | --- |
| Jacob's Pickaxe | Miners' Compact |
| Expedition Pack | League of Extraordinary Citizens |
| Clockwork Scavenger | Tinkers' Union |
| Pet Mimic | future taming/research institution |

## Upgrade Preservation

The long-term goal is for restored relics to preserve progression lineage.

Potential approaches:

- restore exact upgraded state
- restore base item and reapply upgrades separately

Initial implementation can start simpler and evolve later.

## Relationship To Durability

The Restoration Registry is separate from normal durability and repair systems.

### Durability

Handles:

- wear
- damage
- exhaustion
- maintenance

### Restoration Registry

Handles:

- missing items
- deleted items
- catastrophic loss
- missed unlocks

## First Planned Pilot

The first full pilot implementation should be:

```text
Jacob's Pickaxe
```

through:

```text
Miners' Compact restoration services
```
