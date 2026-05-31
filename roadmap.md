# ClusterF UO Roadmap — Shattered Legacy

This document captures future gameplay ideas and design direction for the Shattered Legacy shard (ClusterF ModernUO). It is intentionally separate from `containers/uo/README.md`, which should remain focused on deployed/current operational state.

## Current Implemented Feature Notes

Operational/current implementation details belong in `containers/uo/README.md`. As of the latest repository state, the README documents that Shattered Legacy is operational, has 300 skill caps, expanded stat caps, New Haven seeding/repair, healer/self-resurrection customizations, TerMur spawns, south mine decor, and an implemented Pet Mimic system. Roadmap entries below should guide future design and expansion without duplicating every operational command.

## Development Architecture Summary

Shattered Legacy is evolving from a list of custom shard features into an expedition-focused MMO systems architecture inside Ultima Online.

Future development should follow this dependency order:

```text
Foundation frameworks -> institutions -> logistics/exploration infrastructure -> reward ecosystem -> dungeon ecology -> companions/endgame
```

This avoids building isolated one-off features that need to be rewritten later.

Core pillars:

1. Institutional world design
2. Expedition gameplay ecosystem
3. Unified logistics architecture
4. Achievement/Renown and guild progression
5. Exploration, travel, and cartography
6. Dungeon ecology and living treasure
7. Companion and mimic systems
8. Mastery progression beyond 300

## Design Principles

- Support one-character-friendly progression.
- Avoid permanent missables and irreversible early mistakes.
- Keep early-game content relevant through restoration, upgrades, and legacy systems.
- Treat NPC guilds as profession progression systems, not exclusive player-guild replacements.
- Allow players to join and progress through all NPC guilds over time.
- Build long-term character identity through professions, relics, reputation, discoveries, companions, and custom content.
- Balance future systems around the ClusterF high-progression model: 300 skill caps and expanded stat caps.
- Prefer systems that emerge from normal gameplay over repetitive daily quest loops.
- Systems should exist in the world through guilds, halls, lodges, archives, and physical locations instead of floating vendor stones or abstract convenience hubs.
- Travel, geography, and player-built infrastructure should matter.
- Reduce friction without removing immersion.
- Build reusable frameworks before large content rollouts.

## World Identity

Shard name:

```text
Shattered Legacy
```

The name should guide future lore and system naming. It fits UO shard lore, fractured Britannias, Mondain's shattered gem, forgotten histories, and long-term legacy progression.

Tone:

- immersive
- mystical
- grounded
- Ultima-inspired
- exploratory
- institution-driven
- history-driven

Avoid:

- neon vendor-stone shard vibes
- excessive convenience hubs
- anime parody tone
- disposable progression loops
- generic custom-server clutter

## Institutional Worldbuilding

Every major custom system should have an in-world home.

Examples:

| System | Preferred Home |
| --- | --- |
| Achievements and renown | League of Extraordinary Citizens |
| Mercenaries and squires | Mercenary Guild, Order of Knights, or Free Company hall |
| Cartography and map discovery | Cartographers Guild or League archive |
| Ore surveys and mining contracts | Mining Cooperative |
| Legacy item restoration | Relevant profession guild archive or relic keeper |
| Companion breeding/bonding | Animal Husbandry Society, stables, ranger lodges, or druid groves |
| Portable logistics/crafting | League expedition quartermaster and profession guilds |
| Battlefield recovery | Tinkers/Engineers Guild, League, and profession upgrade paths |

Avoid centralizing all custom systems in one bank-adjacent reward room. A player should remember where a system lives in the world.

## NPC Guild Reputation

ClusterF should expand NPC guilds into long-term profession organizations.

Players should be able to join and progress with every NPC guild because ClusterF is designed around one primary character per player. NPC guilds should not be mutually exclusive factions. Instead, they should represent careers and institutions within the world.

### Existing Guildmaster First — Design Constraint

**All guild reputation and currency systems must be anchored to existing in-world guildmaster NPCs before any new NPCs or guilds are created.**

The standard UO guildmasters already exist in New Haven and across Britannia. They are the entry points. New institutions (Mining Cooperative, League of Extraordinary Citizens, Cartographers Guild, etc.) are expansions that come *after* the existing guildmasters are functional, not replacements for them.

Existing guildmaster NPCs and their guild keys:

| NPC Class | Guild Key | Display Name |
| --- | --- | --- |
| `BlacksmithGuildmaster` | `smithing` | Society of Smiths |
| `MageGuildmaster` | `arcane` | Guild of Arcane Arts |
| `TailorGuildmaster` | `tailoring` | Tailors Guild |
| `TinkerGuildmaster` | `tinkers` | Tinkers Guild |
| `HealerGuildmaster` | `healers` | Healers Guild |
| `BardGuildmaster` | `bards` | Bards Guild |
| `ThiefGuildmaster` | `thieves` | Thieves Guild |
| `WarriorGuildmaster` | `warriors` | Warriors Guild |
| `MinerGuildmaster` | `mining` | Mining Cooperative (uses existing NPC) |
| `BowyrGuildmaster` or `Bowyer` | `archers` | Archers Guild |

New NPC guilds added later (after existing guildmasters are functional):

- Rangers Lodge / Rangers Guild
- Maritime Guild
- Animal Husbandry Society
- Mercenary Guild
- Order of Knights
- Cartographers Guild
- Engineers Guild (Tinkers expansion)
- League of Extraordinary Citizens (achievement/expedition hub)

Each guild can track:

- reputation or standing
- rank
- profession currency or scrip
- guild services
- contracts
- legacy item unlocks
- upgrade paths
- recipes or crafting permissions
- access to profession-specific content
- travel or logistics privileges

Possible rank model:

| Standing | Rank |
| --- | --- |
| 0-999 | Initiate |
| 1,000-4,999 | Apprentice |
| 5,000-14,999 | Journeyman |
| 15,000-49,999 | Master |
| 50,000-99,999 | Grandmaster |
| 100,000+ | Legendary |

Reputation should come from doing the profession, not from artificial daily chores.

Examples:

- Mining reputation from mining, ore turn-ins, survey contracts, rare vein discovery, and refinery work.
- Smithing reputation from crafting, bulk work, repairs, guild contracts, and special alloy commissions.
- Ranger reputation from exploration, monster hunting, tracking, skinning, and wilderness contracts.
- Healer reputation from healing, cures, resurrections, field medicine, and recovery contracts.
- Cartography reputation from exploration, map creation, survey records, treasure tracking, and rune archive support.
- Mercenary reputation from contracts, escort work, companion advancement, and battlefield support.
- Tinkering/engineering reputation from scavenger construction, field devices, and expedition logistics.

## Currency Architecture

Shattered Legacy should not rely on gold alone. Separate currencies prevent gold inflation and keep progression systems distinct.

| Currency | Purpose |
| --- | --- |
| Gold | base economy |
| Renown | achievement/prestige currency through the League |
| Legacy Fragments | long-term progression, mastery, relic, and event currency |
| Guild Scrip | profession-specific spending and upgrades |

Possible guild currencies:

- Mining Vouchers
- Smithing Seals
- Arcane Research Notes
- Ranger Commendations
- Healer's Marks
- Maritime Scrip
- Cartographer's Seals
- Mercenary Warrants
- Husbandry Tokens
- Engineering Gears or Tinker Warrants

These currencies can be spent on:

- restoring legacy items
- upgrading legacy items
- buying profession tools
- buying survey maps
- unlocking recipes
- training conveniences
- accessing guild services
- purchasing cosmetic variants
- acquiring bonding/breeding aids
- unlocking expedition pack modules
- upgrading Clockwork Scavenger modules

Costs should often combine:

- guild reputation threshold
- guild currency
- profession materials
- gold sink
- Renown or Legacy Fragments for cross-system upgrades
- cooldown or one-active-copy rule for legacy restorations

## League of Extraordinary Citizens

The League of Extraordinary Citizens should be the main institution for achievements, renown, exploration records, and citizen advancement.

The League should feel like a Britannian civic/adventuring society, not a superhero parody. It can maintain:

- achievement records
- citizen ranks
- Renown rewards
- expedition charters
- discovery logs
- public contracts
- the Citizen's Codex / wiki
- expedition pack upgrades
- exploration-related unlocks
- bulletin/dispatch archives

Achievements should be framed as deeds recognized by the League instead of abstract popup spam.

## Achievements and Renown

Shattered Legacy should eventually have an achievement framework, preferably data-driven and event-hook based.

Separate these systems:

| System | Purpose |
| --- | --- |
| Achievements | milestone/history tracking |
| Achievement points | permanent non-spendable prestige score |
| Renown | spendable achievement currency |
| Guild reputation | profession progression |
| Guild currencies | profession-specific spending |
| Legacy Fragments | long-term mastery/relic/event currency |

Renown should be used for:

- Citizen's Expedition Pack upgrades
- cosmetic rewards
- titles
- quality-of-life unlocks
- relic restoration support
- access to League services
- special modules or expedition tools

Avoid direct best-in-slot gear from achievement vendors. Favor recognition, identity, logistics, cosmetics, and world access.

Candidate achievement categories:

- Exploration
- Combat
- Crafting
- Mining
- Fishing
- Guilds
- Legacy Items
- Companions
- Events
- Discovery
- Collection
- Economy
- Treasure Hunting
- Dungeon Ecology

## Bulletin / MOTD / League Dispatch System

Shattered Legacy should implement an in-game bulletin or MOTD system early.

Prefer lore framing:

```text
League Dispatches
Citizen's Bulletin
Expedition Ledger
Britannian Notices
```

Design goals:

- notify players of updates and new features
- provide concise in-game patch summaries
- support world/event notices
- avoid repeatedly showing the same message forever
- maintain an archive players can reread
- eventually connect to physical bulletin boards and the Citizen's Codex

Message categories:

| Category | Use |
| --- | --- |
| Critical Notice | maintenance, downtime, important changes |
| League Dispatch | world updates and expedition news |
| Guild Notice | profession/guild updates |
| Event Notice | temporary events and world changes |
| Personal Notice | future player-specific unlocks, promotions, companion awakenings |

In-game bulletins should be concise and immersive; full technical patch notes can live in the wiki/Citizen's Codex.

## Tutorial and Onboarding Systems

As Shattered Legacy becomes more system-heavy, tutorial and onboarding quests become mandatory.

Tutorials should be tied to institutions rather than floating helper stones.

Priority onboarding areas:

- basic shard connection/setup information through the wiki
- New Haven introduction updates
- League of Extraordinary Citizens introduction
- guild initiation quests
- Expedition Pack tutorial
- Clockwork Scavenger tutorial
- travel/rune library explanation
- resource satchel and fieldcrafting tutorial
- treasure map/cartography tutorial
- Pet Mimic care and purge potion explanation

Early tutorial systems should support testing convenience without permanently damaging the long-term travel/geography philosophy.

## Legacy Awakening

Legacy Awakening is the shard's long-term progression concept. It replaces a generic prestige framing.

It is inspired by mastery/refinement/cultivation concepts, but should be adapted into Britannian/Ultima tone.

Do not make it a mandatory treadmill or infinite stat reset loop. It should represent refinement of identity through experience.

Possible stages:

- Soul Awakening
- Foundation Tempering
- Inner Core
- Spirit Ascension
- Legacy Embodiment

Legacy Awakening can unlock:

- specialization trees
- guild advancement layers
- companion awakenings
- expedition pack modules
- legacy item branches
- titles/cosmetics
- limited permanent bonuses
- new activities and trials

Avoid mandatory resets that make players feel obsolete if they do not prestige. If resets exist, they should be optional and preserve history such as registry unlocks, guild standing, achievements, housing, discoveries, and cosmetics.

## Restoration Registry

ClusterF should implement a restoration registry for important legacy profession items.

The registry should permanently track whether a player or account has unlocked a legacy item. This prevents one-character players from being punished for losing an item, deleting an item, missing a quest, or outleveling a New Haven quest before realizing the reward mattered.

Design goals:

- No permanent missables.
- No need to reroll a character.
- Once earned, always recoverable through the appropriate guild.
- Recovery should not be free spam.
- Prevent duplicate abuse.

Possible rules:

- Each legacy item has a unique registry key.
- Unlocks are stored **account-wide** (decided). Single character per account will be enforced in a later phase, making this a natural fit.
- Guild NPCs can restore an item if the unlock exists.
- Players who missed the original quest can unlock the item through a guild legacy contract.
- Restoration requires guild rank, guild currency, materials, gold, cooldown, or a one-active-copy check.

Example registry keys:

```text
legacy.jacobs_pickaxe
legacy.hammer_of_hephaestus
legacy.healers_touch
legacy.ring_of_the_savant
legacy.bulwark_leggings
```

## Guild Legacy Contracts

Guild Legacy Contracts are alternate acquisition paths for important legacy items.

They solve two problems:

1. Players may lose early quest rewards.
2. Players may miss New Haven quests because their skill was too high or the shard changed after they already started.

Example: Mining Cooperative legacy contract

```text
Legacy Contract: Jacob Waltz
```

Requirements might include:

- Mining skill threshold
- Mining Cooperative standing
- ore turn-ins
- exploration objective
- small quest chain
- guild currency

Reward:

```text
Jacob's Pickaxe
```

Once completed, the restoration registry records that the player has unlocked Jacob's Pickaxe permanently.

## New Haven Legacy Item Upgrades

The New Haven skill trainer quest rewards should become early-game identity items that can evolve into useful early/midgame and later utility relics.

Design philosophy:

- Do not make them disposable newbie items.
- Do not make them guaranteed best-in-slot forever.
- Make them useful, sentimental, and profession-defining.
- Tie upgrades to NPC guild reputation and profession activity.
- Preserve item identity and name lineage where possible.

Example: Jacob's Pickaxe

| Stage | Concept |
| --- | --- |
| Tier 1 | Jacob's Pickaxe, starter mining reward |
| Tier 2 | Jacob's Reinforced Pickaxe, better durability and slight mining utility |
| Tier 3 | Jacob's Prospector Pickaxe, detects rare veins or creates survey notes |
| Tier 4 | Jacob's Worldbreaker Pickaxe, prestige utility relic for legendary mining content |

Possible upgrade branches:

- Industrial: yield, durability, bulk extraction
- Prospector: rare ore detection, survey generation, ore map support
- Deep Delver: dangerous caverns, legendary veins, special mining events

Other New Haven rewards should receive similar guild-linked treatment.

## Extended Resources and Mining

ClusterF plans to expand mining and resources beyond the traditional OSI ore chain.

Current design direction:

- Individual skill cap: 300
- Total skill cap: enough for all skills to reach 300
- Mining progression should use the full 0-300 range
- Ores should have slight linear tier progression plus distinctive identity

Standard ore chain:

- Iron
- Dull Copper
- Shadow Iron
- Copper
- Bronze
- Gold
- Agapite
- Verite
- Valorite

Possible extended ore chain:

- Platinum
- Toxic
- Blaze
- Frost
- Obsidian
- Mythril
- Adamantium
- Celestial

Progression idea:

| Skill Range | Resources |
| --- | --- |
| 0-100 | Standard OSI ores |
| 100-150 | Platinum, Toxic, Blaze |
| 150-200 | Frost, Obsidian, Mythril |
| 200-250 | Adamantium |
| 250-300 | Celestial and legendary deposits |

Design principle:

Higher ores should be generally stronger and rarer, but not purely linear replacements. Each should have a theme.

Examples:

- Toxic: poison, corruption, lifesteal, venom themes
- Blaze: fire damage, heat, volcanic zones
- Frost: cold, slows, frozen caverns
- Obsidian: void, reflection, dark volcanic glass
- Mythril: lightweight, stamina, speed
- Adamantium: durability, defense, tankiness
- Celestial: endgame magic synergy and rare events

Likely ModernUO implementation areas:

```text
CraftResource
OreInfo
BaseOre
Mining.cs
HarvestDefinition
HarvestVein
HarvestResource
DefBlacksmithy.cs
```

## Ore Tracking and Map Sharing

ClusterF should use ClassicUO's world map marker ecosystem as the first ore tracking layer.

Near-term idea:

- Razor/ClassicUO scripts log ore discoveries locally.
- Logs record ore type, coordinates, map/facet, and timestamp.
- Data can be converted into ClassicUO marker CSV files.
- Marker packs can be shared with other users later.

Possible marker pack names:

```text
ClusterF_Ores_Common.csv
ClusterF_Ores_Rare.csv
ClusterF_Ores_Legendary.csv
ClusterF_GuildSurvey.csv
```

Long-term feature ideas:

- Prospector's Journal item
- ore survey maps
- guild-shared mining networks
- player-tradable survey data
- rare ore discovery achievements
- dynamic resource events
- admin-only map layers for regions/spawners/veins

## Exploration and Fog of War

ClassicUO world-map exploration should eventually support fog-of-war style discovery.

Goal:

- unexplored areas begin hidden or blacked/fogged
- terrain, roads, dungeons, and landmarks reveal through physical exploration
- discoveries persist per player, account, guild, or family depending on configuration
- Cartography and the League can support shared maps and archives

Design principles:

- knowledge is progression
- geography should matter
- exploration achievements should be meaningful
- do not reveal every secret through a static wiki or map pack
- use forgiving region/radius-based reveal, not pixel-perfect tedium

Possible features:

- region completion percentage
- hidden landmark discovery
- dungeon mapping
- Cartographer's Guild map services
- player-tradable maps
- shared family/guild atlas

## Cartographers Guild and Treasure Tracking

The Cartographers Guild should become Britannia's exploration and navigation authority.

Future Cartographer systems can include:

- treasure map triangulation
- approximate directional pulses
- improved dig radius
- treasure level support upgrades
- facet support upgrades
- hidden vault discovery
- expedition route recording
- shared atlas services
- fog-of-war reveal assistance
- survey map creation
- corrupted/unstable map stabilization

Clockwork Scavenger integration:

- attach a treasure tracking module
- feed it a treasure map or survey map
- receive approximate guidance instead of an exact teleport marker
- upgrade support for higher TMap levels
- upgrade support for other facets

Possible upgrade tiers:

| Tier | Capability |
| --- | --- |
| Survey Scanner | low-level map assistance, improved dig radius |
| Expedition Triangulator | mid-level TMap tracking and directional pulses |
| Facet Resonance Array | cross-facet map support |
| Deep Cartography Matrix | ancient, corrupted, mimic nest, or hidden vault maps |

Do not instantly reveal every treasure location. The system should preserve exploration and discovery.

## Travel, Moongates, and Rune Libraries

Avoid placing moongates outside every important place. Too many moongates collapse the world into disconnected convenience lobbies and kill player-created rune libraries.

Travel should support:

- city moongates where loreful
- ships, ferries, caravans, and roads
- player-made rune libraries
- guild travel networks
- public map archives
- restored abandoned buildings as travel hubs

Principle:

```text
Players should solve convenience through worldbuilding.
```

This keeps geography, housing location, travel routes, and public infrastructure meaningful.

## Resource-Based Expedition Travel

Testing and early-game travel need to be less painful than vanilla moongates, but a massive free gate network would damage exploration.

Future travel systems should provide convenience through cost, infrastructure, or progression.

Possible systems:

- charged expedition gates
- leyline relays that consume reagents/gems/Legacy Fragments
- cartographer waypoints
- consumable travel charges
- guild transport contracts
- caravan routes
- ferry routes
- temporary testing gates that are removed or converted later
- rechargeable relay stones

Goal:

```text
travel convenience without destroying geography
```

Resource costs should be light enough for family/private testing but meaningful enough that rune libraries and player travel infrastructure still matter.

## Citizen's Expedition Pack

The Citizen's Expedition Pack is a planned major progression artifact issued by the League of Extraordinary Citizens.

It should be more than a bigger backpack. It should be a lore-friendly logistics relic that grows with the player.

Design goals:

- hold specialized satchels/modules
- reduce bank friction without destroying world logistics
- integrate with crafting and fieldcrafting
- evolve through Renown, achievements, guild standing, exploration, and Legacy Awakening
- reflect player history
- serve as the player's main expedition logistics hub

Possible tiers:

| Tier | Concept |
| --- | --- |
| 1 | Citizen's Expedition Pack |
| 2 | Journeyman Expedition Pack |
| 3 | Runed Expedition Pack |
| 4 | Legacy Reliquary |

Possible modules:

- Reagent Satchel
- Smith's Cache
- Ore/Granite Satchel
- Gem Pouch
- Survey Kit
- Cartographer's Roll
- Field Forge Kit
- Portable Anvil/Folding Anvil
- Soul Vessel
- Beastmaster Kit
- Mercenary Supply Kit
- Clockwork Scavenger Dock
- Recovery/Skinning Module

Progression should unlock more module slots, new functions, and bonuses rather than only adding raw storage capacity.

## Resource Satchels and Bags of Holding

Shattered Legacy should support lore-friendly high-capacity resource storage inspired by bags of holding, resource keys, and profession satchels.

Avoid generic neon key systems where possible. Prefer immersive item identities:

| Resource Type | Possible Item |
| --- | --- |
| Reagents | Arcane Reagent Satchel |
| Ingots | Smith's Cache |
| Ore/Granite | Prospector's Pack |
| Gems | Gem Pouch |
| Wood | Forester's Bundle |
| Cloth/Leather | Weaver's Roll / Tanner's Kit |
| Imbuing materials | Soul Vessel |

Crafting should eventually be able to consume directly from these storage artifacts when they are in the player's pack, nested in the Expedition Pack, or otherwise valid.

## Portable Crafting and Fieldcrafting

Portable crafting should exist, but remain immersive and earned.

Examples:

- Folding Anvil
- Field Forge
- Expedition Camp Kit
- Portable Soulforge
- Ranger Workbench
- Traveler's Loom

Portable crafting should support expeditions and reduce tedious banking, but permanent workshops, guild halls, and houses should remain superior for bonuses, bulk crafting, and advanced recipes.

## Unified Loot and Reward Framework

Before overhauling dungeon chests, champion spawns, treasure hunting, and mimic encounters, Shattered Legacy should define a reusable loot architecture.

Goals:

- avoid rewriting every reward source separately
- support dungeon identity
- support package-based loot
- support profession rewards
- support relics and fragments
- support Renown and achievement hooks
- support guild currency hooks
- support mimic/dungeon ecology hooks
- support expedition and scavenger recovery systems

Every major reward source should eventually be able to emit:

| Reward Type | Examples |
| --- | --- |
| Currency | gold, Renown, Legacy Fragments, guild scrip |
| Materials | ores, gems, reagents, rare crafting components |
| Relics | legacy fragments, artifact parts, upgrade components |
| Logistics | satchel upgrades, Expedition Pack modules, scavenger modules |
| Exploration | maps, survey notes, vault clues |
| Companions | bonding aids, bloodline items, mimic materials |
| Cosmetics | dyes, trophies, display items |

## Corpse Claim, Loot Routing, and Battlefield Recovery

Large-scale combat such as champion spawns can create too many corpses for traditional manual looting. Shattered Legacy should implement scalable corpse recovery and loot routing infrastructure.

Design philosophy:

```text
reduce corpse friction without turning loot into invisible vacuum automation
```

Potential systems:

- `[claim` or equivalent corpse claim command
- category-based loot filters
- player-configured recovery bags
- loot routing into satchels/Expedition Pack modules
- gold/resource/relic salvage
- corpse cleanup after recovery
- incremental recovery during combat, not only after combat ends
- backend loot queues and batched recovery for performance

Recovery should work during long fights, not only after combat, because champion spawns and mass PvE can generate large corpse piles.

Possible loot categories:

- gold
- gems
- reagents
- resources
- maps
- relics
- trophies
- crafting materials
- skins/hides/meat
- magic items
- trash/salvage

## Clockwork Scavenger

The Clockwork Scavenger is the preferred lore-friendly presentation layer for corpse claim, loot routing, salvage, and skinning systems.

It should be a visible expedition/recovery construct that appears to process corpses and gather loot, while the real backend uses efficient queues and batched recovery.

Core principle:

```text
the construct should look active, but the server should not rely on expensive pathfinding for every corpse
```

Possible tier structure:

| Tier | Concept | Capabilities |
| --- | --- | --- |
| Mk I | basic recovery construct | gold/basic loot, small radius, slow recovery |
| Field Recovery Construct | improved battlefield recovery | categories, routing, faster recovery |
| Expedition Quartermaster Construct | advanced logistics | skinning, salvage, trophies, rare mats |
| Legacy Scavenger Platform | late-game relic construct | advanced extraction, mimic interaction, large-scale events |

Guild-specific upgrades:

| Guild | Upgrade Direction |
| --- | --- |
| Mining Cooperative | ore/gem/golem salvage, metal compression |
| Rangers Lodge | skinning, hides, meat, trophies, wilderness harvesting |
| Arcane Conservatory | reagents, magical residue, soul fragments, corrupted relics |
| Mercenary Guild | battlefield salvage, armor/weapon dismantling, emergency retrieval radius |
| Cartographers Guild | map fragments, expedition relics, treasure triangulation |
| Tinkers/Engineers Guild | core chassis, speed, radius, module slots, maintenance |

The Scavenger can integrate with the Citizen's Expedition Pack as a docked module or deployed companion device.

Avoid making it an AFK looting bot. It should support active combat and reduce tedium, not replace gameplay.

## Dungeon Ecology and Living Treasure

Dungeon treasure should become gameplay, not just static containers.

Philosophy:

```text
Opening a chest should feel like an event.
```

Future systems:

- suspicious chests
- living chests
- ancient mimic nests
- corrupted vaults
- dungeon infestations
- dungeon-specific loot packages
- expedition caches
- mimic ambushes
- hidden vault chains

Dungeon chests should reward:

- curiosity
- preparation
- lockpicking
- remove trap
- detect hidden
- cartography
- exploration
- risk-taking

Dungeon-specific identity examples:

| Dungeon Theme | Possible Rewards |
| --- | --- |
| Ice/cold areas | Frost materials, frozen mimic variants, cold relics |
| Fire/volcanic areas | Blaze materials, ash mimics, heat relics |
| Undead areas | hollow mimics, bone relics, spirit fragments |
| Arcane areas | runed mimics, magical residue, spell relics |
| Mining caves | deepdelver mimics, ore surveys, rare metals |

## Treasure Hunting Expansion

Treasure hunting should evolve into expedition gameplay.

Avoid reducing it to:

```text
dig chest -> loot chest
```

Future ideas:

- chest quality tiers
- themed treasure packages
- profession-oriented rewards
- Cartographer Guild progression
- scavenger-assisted triangulation
- mimic nest maps
- corrupted maps
- hidden vault chains
- survey map crossover
- League expedition contracts
- dungeon-specific treasure ecosystems

Treasure maps should become part of the exploration/logistics loop rather than an isolated profession.

## Champion Spawn and Mastery Rewrite

Traditional powerscrolls are much less useful with 300 skill caps. Champion spawns should be updated so they remain meaningful.

Future direction:

- replace or supplement old 105/110/115/120 cap scrolls
- implement additive mastery scrolls or Legacy Scrolls
- scrolls grant +5, +10, +15, or +20 skill cap beyond current cap
- allow 300 skill to become 305/310/320+ through endgame progression
- eventually tie advanced mastery to Legacy Awakening and guild systems

Possible names:

- Legacy Scroll
- Mastery Scroll
- Ascendant Scroll
- Scroll of Refinement
- Mastery Sigil

Champion spawns should become evergreen mastery events, not obsolete powerscroll farms.

Possible future champion design:

- profession-themed champion events
- dungeon-specific champion identities
- guild-sponsored champion contracts
- Legacy Fragment rewards
- mastery scrolls targeted by theme
- exploration/dungeon ecology hooks

## Pet Mimic

The Pet Mimic is an implemented custom item. See `containers/uo/README.md` for operational details, file paths, settings, and phase status.

Current design summary:

- A blessed companion item worn in the talisman slot. Double-click from pack to equip.
- Starts dormant (blue pouch, hue 1154). Eats equippable items to absorb their stats and take their form.
- Category-locked after first eat (weapon type, armor, jewelry, clothing, tool).
- Accumulates stats from every eaten item regardless of active form — not per-form.
- Stat grants to wearer: Str/Dex/Int, LRC/FC/FCR/DI, resistances, skill bonuses.
- HP system: gems permanently grow MaxHP; potions and bandages restore current HP.
- Combat decay: loses HP per hit to wearer (same trigger as armor durability via `IWearableDurability`).
- Regen scales with meals eaten and gem HP — early game needs active care, late game self-sustains.
- Can be bandaged directly (double-click bandage, target mimic).
- Form journal: known forms searchable by name; click to switch appearance while keeping all stats.
- All caps, heal values, decay rate, and regen scales configurable in `modernuo.json`.

Phase status: see README phase table. Phases 1–5 are complete.

## Pet Mimic Ecosystem Expansion

Long-term, the Pet Mimic should evolve from a custom item into a living adaptive companion species.

Concept:

```text
A mimic is not truly an item; it is a living creature disguised as an item.
```

Future design goals:

- acquisition through dungeon mimic encounters
- skill interaction with Lockpicking, Remove Trap, Detect Hidden, Animal Taming, Animal Lore, Item ID, and possibly Spirit Speak
- follower slot usage because mimics are living creatures
- taming skill influences how many mimics can be equipped/controlled
- corpse eating for healing, temporary forms, and gradual stat adaptation
- metal/ore feeding to increase hardness and reduce health decay
- armor feeding for durability/hardness growth
- gem feeding for max HP growth
- tool forms that may consume gathered resources
- mimic temperaments and personalities
- mimic archetypes/evolution paths

Potential mimic follower slot model:

| Mimic Type | Follower Slots |
| --- | --- |
| Lesser Mimic | 1 |
| Greater Mimic | 2-3 |
| Awakened Mimic | 4-5 |

Potential Animal Taming interaction:

| Taming Skill | Mimic Handling Concept |
| --- | --- |
| low/no taming | 1 lesser mimic or limited handling |
| 100 | improved control, basic mimic use |
| 200 | multiple mimics or advanced forms |
| 300 | mimic master handling |
| 300+ mastery | awakened mimic control |

Potential feeding effects:

| Food | Effect |
| --- | --- |
| Corpses | heal, temporary form, predator/beast adaptation |
| Metals/Ores | hardness, reduced decay, ore-themed traits |
| Armor | durability/body reinforcement |
| Weapons/Magic Items | offensive traits and stats |
| Gems | max HP |
| Resources | profession-specific growth |

Tool form behavior:

- mimic pickaxe may consume ore
- mimic hatchet may consume wood
- mimic skinning tool may consume hides
- consumption may sometimes mutate, refine, duplicate, or improve resources

Potential temperaments:

- Hungry
- Loyal
- Clever
- Curious
- Greedy
- Feral
- Protective

Potential archetypes:

- Tool Mimic
- Armor Mimic
- Arcane Mimic
- Beast Mimic
- Expedition Mimic
- Relic Mimic

Design warning: mimics should be powerful, strange, evolving, and high-maintenance. They should not become passive mandatory best-in-slot stat sticks.

## Companion Philosophy

Companions should be living beings with history, not disposable stat blobs.

Power should come from:

- shared travel
- shared battle
- bonding
- affinity
- quests
- memories
- lineage
- awakenings

A first horse or early pet should be able to become meaningful over time.

Possible tracked memories:

- miles traveled
- battles survived
- bosses defeated
- regions discovered
- owner history
- companion age/history
- awakenings completed

Examples:

```text
Ashhoof the Faithful
Ashfang the Deepdelver
```

## Bonding, Breeding, and Bloodlines

The shard is private/family/community-focused and does not need week-long time gates.

Design goals:

- faster bonding
- faster breeding and incubation
- accessible experimentation
- powerful bloodlines allowed
- future difficulty can scale upward around strong companions

Avoid:

- week-long waiting
- excessive RNG frustration
- pure spreadsheet breeding
- infinite raw stat inflation without identity

Prefer:

- traits
- affinities
- lineage
- specializations
- companion identity
- Companion Awakening

Possible traits:

- Flameblood
- Stonehide
- Deepdelver
- Moonmarked
- Wildborn
- Guardian
- Loyal
- Swiftpaw

Guilds and achievements can unlock bonding aids, breeding accelerators, lineage stabilizers, and companion awakening tools.

## Mercenaries and Squires

Mercenaries/squires should help solo players tackle challenges without becoming AFK bot armies.

Design philosophy:

- persistent retainers, squires, apprentices, or trusted allies
- support roles rather than automated dungeon clearers
- one active major human companion by default
- progression through contracts, affinity, guilds, and history

Possible roles:

- Squire / Shieldbearer: tank and protection
- Scout: tracking and exploration
- Healer: bandages, cures, emergency support
- Porter: loot and resource carrying
- Arcanist: buffs/debuffs/utility magic

Mercenaries should belong to the world:

- Mercenary Guild
- Order of Knights
- Rangers Lodge
- League expedition contracts

Future systems:

- companion affinity
- contracts
- morale/wages
- equipment
- lodging
- Companion Legacy Awakening
- public reputation of famous retainers

## Wiki / Citizen's Codex

Internal wiki target:

```text
wiki.clusterf.lab
```

The wiki should eventually become the in-universe/public documentation source for Shattered Legacy. Possible public names later:

- wiki.shatteredlegacy.com
- codex.shatteredlegacy.com

Lore framing:

```text
The Citizen's Codex
```

Maintained by:

```text
League of Extraordinary Citizens
```

The Codex should document:

- onboarding
- install/connect instructions
- guilds
- Legacy Awakening
- achievements/Renown
- exploration
- companions
- crafting/logistics
- known public systems
- lore

It should not fully spoil hidden content, secret discoveries, or mystery systems.

## One-Character Progression Philosophy

ClusterF assumes players may focus on one primary character. Systems should support that.

Implications:

- Avoid permanent faction lockouts unless there is a recovery or alternate path.
- Let players join and progress all NPC guilds.
- Avoid permanent missables.
- Let players recover legacy items.
- Let players experiment and evolve over time.
- Favor time investment and specialization depth over hard exclusivity.

Soft friction is acceptable. Hard exclusion should be rare.

Examples of soft friction:

- high cost to master every guild
- rank-specific materials
- slower progression in advanced branches
- mutually different upgrade branches for an item, with respec/restoration cost
- guild politics as narrative, not permanent exclusion

## Deferred from Phase 2 Testing (2026-05-14)

Items identified during the first full Miners' Compact testing session. Not blocking Phase 3 but should be scheduled.

### UI polish backlog

- **Achievement Unlocked popup** — current popup is too small and uses green-on-black coloring that is hard to read in motion. Needs a larger, better-contrasted design.
- **Achievement Record gump overflow** — gump layout breaks if a player has many achievements; needs scroll region or pagination.
- **Guild join gump resize** — join confirmation gump is too small; needs to be larger for readability.
- **"Explore Old Haven Ruins" achievement description** — the trigger condition is not obvious to players. Add a location hint or clarify the description text.

### Content backlog

- **Mule mount as Miners' Compact reward** — a pack mule or beast of burden as a guild reward at a mid-tier rank. Fits the mining / hauling theme. Defer to Phase 3+ after voucher shop design.
- **South mine atmosphere** — add a canvas tent over or near the Miners' Compact Liaison location and a mining wagon prop nearby to sell the encampment aesthetic. Small decor pass, no gameplay change.
- **Guild join referral flavor** — when a player joins the Miners' Compact via the guild context menu (from `ClusterFGuildSystem`), there is no acknowledgment that the League Registrar referred them. Low priority but adds flavor. Consider a one-line message or a flag check.

### Skill tier achievement milestones

Players suggested achievement milestones tied to skill progression using terms that extend the standard OSI vocabulary for skills above 120. Proposed naming convention:

| Skill Range | Title |
| --- | --- |
| 0–30 | Novice |
| 30–60 | Apprentice |
| 60–90 | Journeyman |
| 90–120 | Expert |
| 120–150 | Adept |
| 150–200 | Sage |
| 200–250 | Paragon |
| 250–300 | Avatar |

These would map to per-skill achievements (e.g. "Mining: Sage", "Mining: Paragon", "Mining: Avatar") that grant Renown and Achievement Points. Design individually per skill or use a generic per-tier framework.

---

## Open Questions

- ~~Should the restoration registry be account-wide or character-wide?~~ **Decided: account-wide.** Single character per account will be enforced at a later phase; the account-wide model is the correct foundation regardless.
- Should legacy items be blessed, insured, account-bound, or recoverable only?
- Should restored items require a cooldown?
- Should only one active copy of a legacy item exist per account/character?
- What are the guild currency names?
- What are the rank thresholds?
- How much linear power should extended ores provide?
- Which New Haven rewards should be upgraded first?
- Should guild progression be stored in XML, JSON, account tags, player properties, or custom persistence?
- Should ClassicUO ore marker packs be generated client-side, server-side, or both?
- How should fog-of-war exploration data be stored and synchronized?
- How many Expedition Pack module slots should each tier have?
- Should resource satchels be craftable, guild rewards, achievement rewards, or all three?
- How fast should pet bonding, breeding, and incubation be?
- How powerful should late-generation bloodlines become relative to future PvE difficulty?
- Should Companion Awakening be shared across pets, mercenaries, and squires?
- Should Renown be account-wide or character-wide?
- How should Legacy Fragments be earned and spent?
- Should Clockwork Scavenger upgrades be permanent, modular, swappable, or all three?
- How aggressive should `[claim`/recovery automation be during champion spawns?
- Should Pet Mimics count against normal follower slots, a mimic-specific slot pool, or both?
- How should mimic feeding be limited to prevent mandatory best-in-slot scaling?
- How should testing travel be supported without permanently damaging the travel philosophy?

## Recommended Implementation Phases

### Phase 0 — Stability and Current Systems

Already underway:

- service reliability
- backups
- deployment flow
- 300 skill caps
- expanded stat caps
- New Haven seeding/repair
- healer/self-resurrection QoL
- Pet Mimic implementation

### Phase 1 — Core Frameworks

Build these before major content rollouts:

1. achievement framework
2. Renown currency
3. guild reputation framework
4. guild currency framework
5. restoration registry
6. onboarding/tutorial architecture
7. unified loot category framework
8. basic reward package architecture

### Phase 2 — Existing Guildmaster Activation

Hook the guild reputation and currency framework into existing in-world guildmasters before creating any new NPC guilds or institutions.

1. guild join / leave interaction on existing guildmasters
2. reputation gain from profession activity (smithing, taming, healing, etc.) hooked via existing skill/craft events
3. guild rank display and NPC greeting changes based on rank
4. basic guild scrip earn/spend loop through existing guildmasters
5. Citizen's Bulletin / League Dispatch system (already implemented — wire into guildmaster greetings)
6. restoration registry: legacy item recovery through existing guildmasters

No new guild NPCs are created in this phase. All systems use NPCs already in the world.

### Phase 3 — Institutional Expansion

Only after existing guildmasters are functional, add new institutions for systems that have no existing NPC home:

1. League of Extraordinary Citizens (achievement/renown hub, expedition charters)
2. Mining Cooperative expansion NPC (supplements existing MinerGuildmaster)
3. Cartographers Guild NPC and location
4. Rangers Lodge NPC and location
5. guild contract framework (works with both existing and new NPCs)
6. expedition quartermaster concept

### Phase 4 — Logistics and Exploration Infrastructure

Implement reusable expedition infrastructure:

1. Citizen's Expedition Pack base architecture
2. resource satchels and crafting-consumption hooks
3. loot routing / recovery bag framework
4. basic `[claim` or corpse claim command
5. Clockwork Scavenger prototype
6. resource-based expedition travel prototype
7. ore discovery logging / marker export tooling
8. initial map discovery/fog-of-war design spike

### Phase 5 — Pilot Content Loops

Use focused pilots before generalizing:

1. Mining Cooperative reputation pilot (MinerGuildmaster + Mining Cooperative NPC)
2. Jacob's Pickaxe restoration path through MinerGuildmaster
3. Jacob's Pickaxe upgrade path
4. basic League/Renown reward path
5. basic Cartographer treasure tracking prototype
6. basic Scavenger skinning or salvage upgrade
7. basic tutorial questline through New Haven/League

### Phase 6 — Reward Ecosystem Rewrite

Only after core frameworks exist:

1. unified dungeon chest loot packages
2. treasure hunting package update
3. profession-themed loot rewards
4. Legacy Fragment sources/sinks
5. champion reward modernization plan
6. dungeon-specific reward identity

### Phase 7 — Dungeon Ecology and Mimic Expansion

Add deeper content after loot/reward framework exists:

1. suspicious/living chests
2. dungeon mimic encounters
3. mimic mini-bosses
4. Pet Mimic acquisition through dungeon ecology
5. mimic feeding/evolution expansion
6. corrupted vaults and mimic nests
7. dungeon-specific mimic variants

### Phase 8 — Mastery and Endgame Progression

1. champion spawn modernization
2. additive mastery scrolls / Legacy Scrolls
3. mastery beyond 300
4. Legacy Awakening skeleton
5. profession/event-based mastery rewards

### Phase 9 — Companion and Retainer Expansion

1. companion affinity
2. fast bonding framework
3. breeding/bloodline system
4. companion awakening
5. mercenary/squire framework
6. retainer guild contracts

### Phase 10 — Public/Long-Term Polish

1. `wiki.clusterf.lab` / Citizen's Codex content
2. public-facing connection docs if needed
3. hidden discovery policies
4. permanent travel infrastructure tuning
5. shard identity/lore polish
6. public onboarding and help systems
