# Achievement System Expansion — Mythik-Inspired Typed Progress

## Purpose

This document captures how Shattered Legacy should take useful ideas from DarkLotus/MythikAchievmentSystem and integrate them into the existing ClusterF achievement system without replacing it.

The current authoritative system remains:

```text
ClusterFAchievements
ClusterFAccountData
League of Extraordinary Citizens
Renown
Achievement Points
```

Mythik should be used as inspiration for typed achievement categories and progress patterns, not as a drop-in replacement.

## External Reference

Repository reviewed:

```text
https://github.com/DarkLotus/MythikAchievmentSystem
```

Useful concepts identified:

- Hunter achievements
- Resource gathering achievements
- Region discovery achievements
- Item crafting achievements
- stable achievement IDs
- progress-based achievements

## Integration Rule

```text
Do not replace ClusterFAchievements.
Do not import Mythik persistence/gumps wholesale.
Integrate useful category patterns into ClusterF's existing account-wide framework.
```

Reasons:

- ClusterF already has account-wide persistence through `ClusterFAccountData`.
- Achievements already feed Renown and Achievement Points.
- The League of Extraordinary Citizens is already the in-world institution for achievement records.
- ModernUO compatibility should be preserved.
- Avoid duplicate gumps, duplicate commands, and duplicate save formats.

## Stable Achievement Key Rule

Adopt this as a hard shard rule:

```text
Achievement keys must be stable after deployment.
Display text may change, but keys must never be renamed, reused, or repurposed once player progress exists.
```

Example:

```text
Good: achievement.mining.iron_ore_1000 keeps its key forever.
Bad: renaming it to achievement.mining.iron_ore_tier1 after players earned progress.
```

## Typed Achievement Archetypes

Add reusable archetypes to `ClusterFAchievements`.

Suggested archetypes:

| Archetype | Purpose |
| --- | --- |
| KillCount | monster/family/boss kill milestones |
| ResourceGathered | mining, lumberjacking, fishing, skinning, harvesting |
| RegionDiscovered | exploration, map discovery, city/region visits |
| ItemCrafted | crafting milestones and exceptional item achievements |
| WorkOrderCompleted | guild work order completions |
| GuildRankReached | guild standing/rank achievements |
| SkillMilestone | skill progression achievements |
| LegacyItemUpgraded | relic/restoration/upgrade achievements |
| ItemTurnedIn | contract/BOD/resource contribution achievements |
| FacetExplored | facet-level exploration milestones |

## Event Hook API

Add helper methods so gameplay systems can report progress without each system knowing achievement internals.

Suggested methods:

```csharp
ClusterFAchievements.OnCreatureKilled(PlayerMobile pm, BaseCreature creature);
ClusterFAchievements.OnResourceGathered(PlayerMobile pm, CraftResource resource, int amount, string sourceSystem = null);
ClusterFAchievements.OnRegionDiscovered(PlayerMobile pm, string regionKey, Map map);
ClusterFAchievements.OnItemCrafted(PlayerMobile pm, Item item, CraftResource resource, bool exceptional);
ClusterFAchievements.OnWorkOrderCompleted(PlayerMobile pm, string guildKey, string workOrderId);
ClusterFAchievements.OnGuildRankReached(PlayerMobile pm, string guildKey, string rankKey);
ClusterFAchievements.OnSkillMilestone(PlayerMobile pm, SkillName skill, double value);
ClusterFAchievements.OnLegacyItemUpgraded(PlayerMobile pm, string legacyKey, int tier);
ClusterFAchievements.OnItemTurnedIn(PlayerMobile pm, string contextKey, Type itemType, int amount);
```

These should update account-backed counters and grant achievements when thresholds are met.

## Account Progress Storage

Progress should be account-wide unless a specific achievement explicitly requires character-level tracking.

Suggested account data shape:

```text
AchievementProgressCounters: Dictionary<string, long>
```

Counter key examples:

```text
kill.orc
kill.daemon
resource.iron_ore
resource.celestial_ore
region.new_haven
region.ilshenar_blood_dungeon
crafted.pickaxe
crafted.exceptional_plate_chest
workorder.mining.completed
workorder.smithing.completed
skill.blacksmithy.100
legacy.jacobs_pickaxe.tier2
```

The exact internal structure can be optimized by coder, but the design goal is stable typed counters.

## Resource Gathering Achievements

Resource achievements are the highest priority because they support the Miners' Compact and the Phase 3 discovery/logistics systems.

Examples:

| Key | Title | Requirement |
| --- | --- | --- |
| `achievement.mining.iron_ore_100` | Iron Starter | Gather 100 Iron ore/ore-equivalent |
| `achievement.mining.iron_ore_1000` | Iron Hauler | Gather 1,000 Iron ore/ore-equivalent |
| `achievement.mining.first_colored_ore` | First Color in the Stone | Mine any colored ore |
| `achievement.mining.first_extended_ore` | Beyond Valorite | Mine any extended ore |
| `achievement.mining.celestial_first` | Celestial Spark | Mine Celestial ore |
| `achievement.mining.all_classic_ores` | Classic Veins Surveyed | Mine all classic ore types |
| `achievement.mining.all_extended_ores` | Shattered Veins Surveyed | Mine all extended ore types |

## Item Crafting Achievements

Crafting achievements should support Phase 4 and future crafting guilds.

Examples:

| Key | Title | Requirement |
| --- | --- | --- |
| `achievement.smithing.first_item` | First Strike | Craft first Blacksmithy item |
| `achievement.smithing.pickaxes_100` | Toolmaker | Craft 100 pickaxes |
| `achievement.smithing.exceptional_first` | A Fine Edge | Craft first exceptional smithing item |
| `achievement.smithing.extended_metal_first` | Strange Alloy | Craft with first extended metal |
| `achievement.smithing.all_extended_metals` | Master of Shattered Alloys | Craft at least one item with every extended metal |
| `achievement.smithing.bod_first` | First Commission | Complete first Smith BOD/commission |
| `achievement.smithing.workorders_25` | Guild Supplier | Complete 25 Smith work orders |

## Region Discovery Achievements

Region discovery should support the League, Cartographers Guild, and future exploration systems.

Examples:

| Key | Title | Requirement |
| --- | --- | --- |
| `achievement.exploration.new_haven` | New Haven Visitor | Visit New Haven |
| `achievement.exploration.ilshenar_first` | Through the Old Gates | Visit Ilshenar |
| `achievement.exploration.malas_first` | Under Dark Skies | Visit Malas |
| `achievement.exploration.tokuno_first` | Eastern Shores | Visit Tokuno |
| `achievement.exploration.termur_first` | Gargoyle Lands | Visit Ter Mur |
| `achievement.exploration.all_facets` | World Walker | Visit all major facets |

Region achievements should be conservative at first. Avoid over-instrumenting every region before the Cartographers Guild design is ready.

## Hunter Achievements

Hunter achievements should eventually support combat progression, dungeon ecology, Rangers/Mercenaries, and champion/dungeon content.

Examples:

| Key | Title | Requirement |
| --- | --- | --- |
| `achievement.hunter.undead_100` | Grave Disturber | Kill 100 undead |
| `achievement.hunter.daemons_100` | Daemon Bane | Kill 100 daemons |
| `achievement.hunter.elementals_100` | Elemental Breaker | Kill 100 elementals |
| `achievement.hunter.dragons_25` | Scale Hunter | Kill 25 dragons/drakes |
| `achievement.hunter.first_boss` | First Trophy | Defeat first boss-class creature |

Hunter achievements should classify mobs by family/tag rather than only by exact class names where possible.

## Skill Milestone Achievements

Support the shard's long-term skill progression model.

Suggested baseline:

| Skill Value | Title Tier |
| --- | --- |
| 10 | Neophyte |
| 20 | Novice |
| 30 | Apprentice |
| 40 | Journeyman |
| 50 | Adept |
| 60 | Expert |
| 70 | Master |
| 80 | Renowned |
| 90 | Elder |
| 100 | Grandmaster |
| 120 | Legendary |
| 200 | Sage |
| 300 | Paragon |
| 400 | Archeon |
| 500 | Avatar |

Skill achievements should be generated consistently for each skill where practical.

## Guild and Work Order Achievements

These should connect achievements to guild progression without duplicating guild ranks.

Examples:

| Key | Title | Requirement |
| --- | --- | --- |
| `achievement.guild.first_join` | Guildbound | Join first NPC guild |
| `achievement.guild.three_guilds` | Working Citizen | Join three NPC guilds |
| `achievement.workorders.first` | First Commission | Complete first guild work order |
| `achievement.workorders.mining_25` | Compact Supplier | Complete 25 Mining work orders |
| `achievement.workorders.smithing_25` | Society Supplier | Complete 25 Smith work orders |
| `achievement.workorders.crossguild_first` | Interguild Trade | Complete first cross-guild work order |

## Legacy Item Achievements

Examples:

| Key | Title | Requirement |
| --- | --- | --- |
| `achievement.legacy.jacobs_pickaxe_restored` | Restored Relic | Restore Jacob's Pickaxe |
| `achievement.legacy.jacobs_pickaxe_t2` | Reinforced Legacy | Upgrade Jacob's Pickaxe to Tier 2 |
| `achievement.legacy.hammer_hephaestus_restored` | The Forge Remembers | Restore Hammer of Hephaestus |
| `achievement.legacy.hammer_hephaestus_t2` | Reinforced Memory | Upgrade Hammer of Hephaestus to Tier 2 |

## UI/Gump Improvements

Existing `[achievements` gump remains authoritative.

Enhancements to consider:

- category filters for typed achievements
- progress bars for incomplete progress achievements
- hidden/secret achievements where appropriate
- recently-earned section
- League Records page for account-wide totals
- display text changed to `New Achievement!` for achievement unlock popup/header

Do not add a second Mythik-style achievement gump unless there is a strong reason.

## Implementation Priority

### Priority 1 — Foundation

- Add typed progress counter support to `ClusterFAccountData`.
- Add archetype metadata to achievement definitions.
- Add helper/event hook API.
- Preserve existing earned achievements and display behavior.

### Priority 2 — Mining and Smithing Hooks

- ResourceGathered counters for mining.
- ItemCrafted counters for blacksmithing.
- WorkOrderCompleted counters for guild work orders.
- LegacyItemUpgraded counters for Jacob's Pickaxe and Hammer of Hephaestus.

### Priority 3 — Skill Milestones

- Add generic skill milestone hooks.
- Generate or define milestone achievements per skill.

### Priority 4 — Exploration and Hunter Hooks

- RegionDiscovered support.
- KillCount support by creature family.
- Fold into Rangers/Mercenaries/Cartographers later.

## Migration and Compatibility Notes

- Keep existing achievement keys and earned states unchanged.
- Add new progress counters in a new account-data version.
- Missing progress counters should default to 0.
- Existing achievements remain binary earned records unless migrated intentionally.
- Do not reuse old keys for new threshold-style achievements.

## Testing Requirements

- Existing earned achievements persist after save/restart.
- Existing `[achievements` command still opens the gump.
- New progress achievements show progress correctly.
- Resource gathering increments only when resource is actually gathered.
- Crafting increments only when craft succeeds.
- Work orders increment only on completion.
- Skill milestones trigger once.
- Achievement unlock popup says `New Achievement!`.
- Admin grant/revoke/info/list commands still work.

## Summary

Use Mythik's useful categories and progress ideas, but keep Shattered Legacy's system as the source of truth.

```text
Mythik gives us category inspiration.
ClusterFAchievements remains the achievement engine.
The League remains the in-world institution.
```
