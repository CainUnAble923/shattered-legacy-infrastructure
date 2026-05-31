# Rangers' Guild (Working Title: The Outriders)

**Status: Stub — not yet implemented**

> Working name is "The Outriders." Rename before implementation if a better fit emerges.
> Guild key in code will be `"rangers"` to match the convention (`"mining"`, `"smithing"`).

## Purpose

The Rangers' Guild is the wilderness and animal-handling profession guild for Shattered Legacy.
Where the Miners' Compact goes deep and the Society of Smiths works the forge, the Outriders
range the surface world — tracking, hunting, taming, and moving things across Britannia.

The guild covers:

- animal taming, training, and care
- pack animal logistics (mules, pack horses)
- hunting and tracking contracts
- wilderness survival and field camp operations
- courier and expedition escort work
- wilderness resource gathering (hides, feathers, reagents, lumber)

The Rangers' Guild acts as the expedition support layer for the cross-guild economy. Miners need
their ore moved. Smiths need their goods delivered. Rangers move things and keep the pack animals
alive to do it.

## Important Dependencies

The following must exist before the Rangers' Guild can be implemented:

- Animal Taming skill integration (already in UO baseline)
- Animal Lore and Veterinary skill hooks
- Mule / pack animal item system (inventory-bearing animal companion)
- Taming trainer NPC placement (New Haven or equivalent location)
- Guild system hooks for `"rangers"` key (shared ClusterFGuildSystem framework — already supports arbitrary guild keys)

## Design Goals

- Make animal taming part of profession identity, not just a pet combat skill.
- Establish the mule/pack animal as the core logistics tool for expeditions.
- Support long-term cross-guild economy (Miners, Smiths, Rangers all interdependent).
- Give rangers a distinct field-camp and wilderness-survival identity.

## Rank Structure

> Placeholder — adjust standing thresholds during implementation to match shard pacing.

| Rank | Standing |
| --- | --- |
| Wanderer | 0 |
| Scout | 1,000 |
| Outrider | 5,000 |
| Trailblazer | 15,000 |
| Beastmaster | 40,000 |
| Warden of the Wild | 80,000 |
| Legendary Outrider | 150,000 |

## Standing Sources

Standing should come from wilderness and animal-handling gameplay.

Examples:

- taming new animal species (first tame of a type)
- completing hunting contracts
- delivering courier contracts (especially cross-guild)
- expedition escort completions
- caring for pack animals (Animal Lore / Veterinary skill use)
- wilderness survival contracts
- rare creature tracking

## Guild Currency

Primary guild currency:

```text
Trail Marks
```

Trail Marks should support:

- mule and pack animal purchases
- animal care supplies (feed, medicine)
- taming and tracking tools
- field camp equipment
- expedition rigging upgrades
- relic restoration (ranger relics — TBD)

## Guild NPC Roles

> Placement TBD — likely New Haven stables or a wilderness outpost.

| NPC | Role |
| --- | --- |
| Outriders Guildmaster | Membership and standing overview |
| Stable Hand | Mule and pack animal sales, care |
| Hunt Master | Hunting and tracking contracts |
| Trail Agent | Courier and escort contracts |
| Taming Trainer | Animal Taming skill training |
| Expedition Quartermaster | Field camp equipment and upgrades |

## Taming Trainers

Taming trainers are a hard prerequisite for the guild. Players must be able to raise Animal Taming
before joining at meaningful rank.

Placement options:

- New Haven stables (starter trainer, trains to ~50 Animal Taming)
- Outriders outpost in the field (advanced trainer, trains to ~80+)
- Existing NPC stable hands (minor training bonus, no guild affiliation)

> The exact location of the Outriders' base camp is TBD. A wilderness location (outside New Haven
> walls) fits the guild identity better than a town hall. Consider a camp near the Britain forest
> or a Trammel wilderness area.

## Mule / Pack Animal System

The mule is the central item/NPC this guild delivers. It is the prerequisite for the
Expedition Forge Rig (cross-guild item with Miners' Compact + Society of Smiths).

### Design Intent

A mule is not a combat pet. It is a logistics tool:

- carries overflow ore, ingots, and supplies
- can be loaded and unloaded from an inventory gump
- follows the player or is hitched at a location
- requires feed and care (Animal Lore / Veterinary)
- can be upgraded with rigging (saddlebags, forge rig, etc.)

### Mule Add-Ons

Rigging slots allow the mule to carry specialized equipment.

| Add-On | Source Guild(s) | Function |
| --- | --- | --- |
| Ore Saddlebags | Rangers | Expanded ore carry capacity |
| Expedition Forge Rig | Miners' Compact + Smiths + Rangers | Deployable field forge (see below) |
| Reinforced Barding | Society of Smiths | Mule survivability in dangerous areas |
| Compact Ore Hopper | Miners' Compact + Rangers | Auto-siphon ore from satchel to mule |

### Expedition Forge Rig

The Expedition Forge Rig is the three-guild collaboration item that motivated the Rangers' Guild
stub. It is the mule-mounted version of the `PortableFieldForge`.

**Requirements:**

- Miners' Compact: Outrider rank (5,000 standing) + Mining Vouchers
- Society of Smiths: membership + Smithing Seals + ingots
- Rangers' Guild: Outrider rank (5,000 standing) + Trail Marks + mule ownership

**Capabilities (vs. PortableFieldForge):**

| | Portable Field Forge | Expedition Forge Rig |
| --- | --- | --- |
| Carried by | Player backpack | Mule |
| Charges | 10 | Unlimited while mule is present |
| Duration | 30 minutes | Persistent (until mule leaves or dismantled) |
| Smelting | Yes | Yes |
| Crafting (anvil) | No | Optional add-on (future) |
| Solo use | Yes | Requires mule nearby |

**Implementation dependency:** `PortableFieldForge.cs` and `DeployedFieldForge.cs` already exist.
The Expedition Forge Rig will reuse `DeployedFieldForge` for the world-placed forge object and
add a mule-presence check in deploy. See also Phase 4L notes in
`containers/uo/design/phases/phase_4_smiths_guild_extended_crafting.md`.

## Ranger Relic — TBD

The Rangers' Guild should have a legacy relic parallel to Jacob's Pickaxe and Hammer of Hephaestus.

Candidate identity:

```text
A taming/tracking tool with tier progression.
Something that records the beasts you've tamed or the distances you've tracked.
```

> Design deferred. Establish the mule system and guild infrastructure first.

## Cross-Guild Economy

The Rangers' Guild should be a hub for cross-guild logistics.

| Guild | Rangers provide | Rangers receive |
| --- | --- | --- |
| Miners' Compact | Pack animal transport, expedition support | Ore and ingots for mule rigging |
| Society of Smiths | Hides, materials, delivery contracts | Reinforced barding, tools |
| Cooks' Guild (future) | Game meat, rare ingredients | Field rations |
| Alchemists (future) | Reagents from wilderness harvesting | Potions, tonics |

## MVP Implementation Goal

Minimum viable Rangers' Guild:

```text
Taming trainer NPC in New Haven
Join Rangers' Guild
Complete one hunting contract
Earn Trail Marks
Purchase a mule from the Stable Hand
Mule follows player and holds inventory
```

Do not attempt full rigging system, Expedition Forge Rig, or relic on first pass.
The mule with basic inventory is the v1 deliverable everything else builds on.
