# Phase 4 — Society of Smiths and Extended Ore Crafting

## Objective

Phase 4 proves that the shard-wide guild/work-order framework works beyond the Miners' Compact.

The Society of Smiths should become the second deep profession guild implementation. Miners produce resources; Smiths turn those resources into crafted value, repairs, field infrastructure, and upgrade components.

Phase 4 should be a second-profession vertical slice, not a full completion pass for every Smithing system.

## Phase Identity

```text
Phase 1 = League onboarding foundation
Phase 2 = first profession guild vertical slice with Miners' Compact
Phase 3 = shared work orders, discovery records, and expedition logistics
Phase 4 = second profession guild + crafted value from mining output
```

## Core Player Loop

```text
Join the Society of Smiths
→ view Smith standing/rank/currency
→ complete Smith work orders
→ earn Smithing Seals
→ craft with extended ores
→ restore/upgrade the Hammer of Hephaestus
→ use crafting quality-of-life tools
→ support Miners' Compact through cross-guild commissions
```

## 4A — Society of Smiths Onboarding

Use existing NPC infrastructure first.

Primary anchor:

```text
NPC: BlacksmithGuildmaster
Guild key: smithing
Display name: Society of Smiths
```

Do not add a new Smith liaison unless the existing guildmaster flow cannot support the needed interactions.

### Guildmaster Options

The Blacksmith Guildmaster should eventually support:

- Join the Society of Smiths
- View Smith Status
- What are Smithing Seals?
- Work Orders
- Repair Services
- Extended Ores
- Cross-Guild Commissions
- Hammer of Hephaestus
- Smiths' Commission Ledger
- Portable Field Forge

### Rank Model

Use the shared guild-rank model unless implementation already provides a better reusable model.

| Standing | Rank |
| --- | --- |
| 0 | Initiate |
| 1,000 | Apprentice |
| 5,000 | Journeyman |
| 15,000 | Master |
| 50,000 | Grandmaster |
| 100,000 | Legendary |

## 4B — Smithing Seals

Introduce Smithing Seals as the Society of Smiths guild currency.

Storage key:

```text
GuildCurrency["smithing"]
```

Smithing Seals may be spent on:

- Hammer of Hephaestus restoration/upgrades
- repair services
- extended ore recipe unlocks
- field forge components
- Portable Field Forge rewards
- Smiths' Commission Ledger upgrades
- mule/satchel reinforcement components
- future Smith reward shop purchases

## 4C — Smith Work Orders

Use the Phase 3 shared Guild Work Order framework.

### Work Order Categories

| Type | Examples |
| --- | --- |
| Tool Orders | pickaxes, shovels, smith hammers |
| Weapon Orders | broadswords, axes, maces |
| Armor Orders | plate gorgets, shields, gloves |
| Repair Orders | damaged tools/equipment |
| Material Orders | ingot deliveries |
| Cross-Guild Orders | mining tools, smelter plates, mule barding parts |

### Small Smith Orders

Examples:

```text
Craft 10 Pickaxes.
Craft 10 Shovels.
Craft 5 Smith Hammers.
Deliver 100 Iron Ingots.
Craft 3 Broadswords.
Craft 3 Plate Gorgets.
```

### Large Smith Orders

Examples:

```text
Forge Resupply Order:
- 10 Pickaxes
- 10 Shovels
- 5 Smith Hammers
- 250 Iron Ingots
```

```text
Militia Armor Order:
- 5 Plate Gorgets
- 5 Plate Gloves
- 5 Shields
```

Large orders must follow the Phase 3 rule:

```text
Large work orders accept direct item/resource turn-ins.
They do not require smaller deeds.
```

## 4D — Extended Ore Crafting

Phase 2 added extended ore/ingot types. Phase 4 should make them practically useful in Blacksmithy.

### Extended Ore Chain

| Tier | Ore |
| --- | --- |
| 10 | Platinum |
| 11 | Toxic |
| 12 | Blaze |
| 13 | Frost |
| 14 | Obsidian |
| 15 | Mythril |
| 16 | Adamantium |
| 17 | Celestial |

### Required Behavior

Extended ingots should:

- appear in Blacksmithy crafting resource selection
- be usable to craft allowed weapons, armor, shields, and tools where appropriate
- apply correct hue
- apply material properties
- persist through save/restart
- be recognized by work orders and BOD modernization logic

### Material Identity Direction

| Ore | Theme |
| --- | --- |
| Platinum | refined durability, physical/cold |
| Toxic | poison damage/resistance |
| Blaze | fire damage/resistance |
| Frost | cold damage/resistance |
| Obsidian | physical durability / shadow |
| Mythril | lightness, energy, luck |
| Adamantium | extreme durability/defense |
| Celestial | rare endgame, all-resist/luck/magic |

Do not make every ore purely linear. Higher-tier ores can be stronger, but each should retain identity.

## 4E — Crafting Quality-of-Life

This is a shard-wide crafting improvement, piloted through Blacksmithy in Phase 4.

### Make X Amount

Add a button to the crafting item detail/info page:

```text
Make X Amount
```

Flow:

```text
Click Make X Amount
→ prompt player for desired quantity
→ repeat normal crafting until requested quantity is reached or interrupted
```

Stop conditions:

- materials run out
- tool breaks
- player moves/cancels
- backpack cannot hold more
- crafting rules fail
- server safety limit is reached

### Make Max

Add a button:

```text
Make Max
```

Flow:

```text
Calculate max craftable from selected resource/materials
→ craft repeatedly through normal crafting queue
```

Implementation must not create hundreds of items in one server tick. Repeat normal crafting safely with existing delays/rules.

## 4F — Bulk Order Deed Modernization

Classic BOD behavior assumes a large trading economy. Shattered Legacy has a small player count, so BODs should preserve identity while removing friction.

### Fill From Backpack

Add a BOD action:

```text
Fill From Backpack
```

Behavior:

- scan player backpack
- find matching crafted items
- check item type
- check material
- check exceptional/quality requirement
- check quantity
- add eligible items automatically
- report progress clearly

Example message:

```text
Added 7 matching exceptional iron plate gloves to the order.
```

### Large BOD Direct Fill

This is a hard design rule for Shattered Legacy:

```text
Large BODs should accept direct requested crafted item turn-ins.
They should not require completed smaller deeds.
```

Example:

```text
Large Plate Armor Order:
- 20 exceptional iron plate gorgets
- 20 exceptional iron plate gloves
- 20 exceptional iron plate arms
- 20 exceptional iron plate legs
- 20 exceptional iron plate tunics
```

The player should be able to craft those items and add them directly to the large deed.

### BOD Claim Timer

Lower the BOD claim timer for small-server pacing.

Suggested simple model:

| Access | Timer |
| --- | --- |
| Base shard timer | 30 minutes |
| Commission Ledger upgraded tiers | 15 / 10 / 5 minutes |

Avoid infinite instant BOD generation.

## 4G — Smiths' Commission Ledger

Add a Society of Smiths reward item that helps manage BODs and commissions.

Preferred name:

```text
Smiths' Commission Ledger
```

### Purpose

The ledger helps players manage and fill Bulk Order Deeds.

Core functions:

- stores active BODs
- shows what items each BOD needs
- scans backpack for matching items
- fills eligible BODs automatically
- helps track large BOD progress
- eventually pulls from satchels, nearby containers, mules, and guild depositories

### Ledger Tier Direction

| Tier | Name | Benefit |
| --- | --- | --- |
| 1 | Smiths' Commission Ledger | stores/view BODs, Fill From Backpack |
| 2 | Reinforced Commission Ledger | shorter BOD timer, sorting/filtering |
| 3 | Masterwork Commission Ledger | fill from nearby Smith Cache / advanced tools |
| 4 | Guildmaster's Commission Ledger | cross-guild orders and advanced large-project tracking |

### Phase 4 MVP

Implement if feasible:

- ledger item
- open/view stored BODs
- Fill From Backpack on selected BOD
- lower BOD timer support

Defer:

- batch fill all
- fill from depository
- commission rerolls
- cross-guild ledger integration
- advanced sorting

## 4H — Repair and Reinforcement Services

Smiths should own repair culture.

Possible services:

- repair targeted metal item
- repair equipped metal gear
- repair legacy tools if eligible
- repair field modules later
- explain player-crafter repair culture

Design rule:

```text
NPC Smith services should augment player smiths, not replace them.
```

Repair work order examples:

```text
Repair 5 damaged pickaxes.
Repair field tools for the Miners' Compact.
Reinforce mining gear for a Deepdelver expedition.
```

## 4I — Hammer of Hephaestus Legacy Line

The Hammer of Hephaestus is the Smiths' legacy relic, parallel to Jacob's Pickaxe.

Core identity:

```text
Jacob's Pickaxe records the world.
Hammer of Hephaestus remembers the metal.
```

### Tier Structure

| Tier | Name | Identity |
| --- | --- | --- |
| T1 | Hammer of Hephaestus | regenerating starter relic |
| T2 | Reinforced Hammer of Hephaestus | longer-lasting guild-restored hammer |
| T3A | Masterwork Hammer | exceptional crafting / quality |
| T3B | Field Forgemaster Hammer | repair / expedition forging |
| T4 | Hammer of the First Forge | combined legendary smith relic |

### Uses and Regeneration

Each tier should have more max uses and better regeneration.

Example direction:

| Tier | Max Uses | Regen |
| --- | --- | --- |
| T1 | 100–150 | slow |
| T2 | 300–400 | moderate |
| T3A/T3B | ~600 | improved |
| T4 | ~1000 | strong |

### Phase 4 Hammer Scope

Implement or normalize:

- T1 restoration/normalization
- T2 Reinforced Hammer
- use regeneration improved by tier
- max uses increased by tier
- basic Metal Familiarity foundation
- T3/T4 documented but deferred

## 4J — Metal Familiarity

The Hammer should learn metals through use.

Core idea:

```text
The more you forge with a metal, the more the hammer understands that metal.
```

Track familiarity for:

- Iron
- Dull Copper
- Shadow Iron
- Copper
- Bronze
- Gold
- Agapite
- Verite
- Valorite
- Platinum
- Toxic
- Blaze
- Frost
- Obsidian
- Mythril
- Adamantium
- Celestial

### Data Ownership

Metal Familiarity should be hammer-lineage-bound and preserved through upgrades/restoration.

### Familiarity Caps

| Hammer Tier | Familiarity Cap |
| --- | --- |
| T1 | 100 per metal |
| T2 | 250 per metal |
| T3A/T3B | 500 per metal |
| T4 | 1000 per metal |

### Future Bonuses

Familiarity may later provide:

- reduced material loss
- increased exceptional chance
- reduced repair damage chance
- reduced repair cost
- improved durability on crafted items
- unlocks metal-specific commissions

For Phase 4 MVP, bonuses may be light or display-only.

## 4K — Hephaestian Tempering

This is a later-tier Hammer feature, not required for Phase 4 MVP unless trivial.

Decision: passive, no extra cost.

When repairing an eligible metal item with a high-tier Hammer:

```text
Successful repair
→ passive chance to increase max durability by +1
→ no extra ingots
→ no powder consumed
→ no toggle
```

### Suggested Unlock

| Tier | Tempering |
| --- | --- |
| T1 | none |
| T2 | none or tiny teaser chance |
| T3A | improves new crafted items |
| T3B | passive repair tempering |
| T4 | stronger crafting + repair tempering |

Suggested chance:

- T3B: 5% base
- T4: 10% base
- Metal Familiarity can add a small bonus

Hard rule:

```text
Cannot exceed Powder of Temperament durability cap.
```

## 4L — Portable Field Forge

Add a Smiths reward/system that supports field smelting and field crafting.

Preferred name:

```text
Portable Field Forge
```

### Purpose

Allows players to:

- smelt ore in the field
- craft basic Smithing items in the field
- repair tools/equipment in the field if repair services are implemented
- support mining expeditions

### Design Rule

```text
Portable Field Forge should be convenient, not superior to permanent forges, houses, or guild halls.
```

### Phase 4 MVP

A feasible MVP:

- deployable item
- temporary forge object at player location
- acts as nearby forge for smelting
- optionally acts as forge/anvil pair for basic Blacksmithy crafting
- limited charges/uses or duration
- Smith rank / Smithing Seals / Blacksmithy skill gated

Preferred behavior:

```text
Double-click Portable Field Forge
→ deploy temporary forge
→ forge lasts X minutes or X uses
→ player smelts/crafts nearby
→ forge can be packed up if not exhausted
```

### Future Integrations

- smelt ore directly from Compact Ore Satchel
- Smelt All support
- mule-mounted field forge rig
- shared satchel/mule logistics gump
- fragment recovery

### Hammer of Hephaestus Interaction — Mobile Forge Toggle

**Design note (2026-05-19):**

The Hammer of Hephaestus currently uses forge proximity to decide what double-click does:
- Near a forge → opens craft menu
- Away from a forge → opens Metal Familiarity panel

When the Portable Field Forge is introduced, players will always be "near a forge" in the field,
breaking the away-from-forge path to the familiarity panel.

**Planned fix:** add a `bool _familiarityMode` toggle bit to T1 and T2 hammer serialization.

- **Craft mode** (default) — current proximity-based behavior unchanged
- **Familiarity mode** — always opens the familiarity gump; gump gains a "Smith at Forge" button
  that explicitly opens the craft menu

The toggle button lives in the `HammerFamiliarityGump` footer so no context menu cliloc is needed.
Serialization bump: add one bool to the existing Serialize/Deserialize block, bump version.

**When to implement:** alongside the Portable Field Forge feature, not before.

## 4M — Cross-Guild Miners ↔ Smiths Contracts

This is the most important proof that guilds are interconnected.

### Miners Request From Smiths

Examples:

```text
Craft pickaxes for Compact recruits.
Forge Field Smelter Plates.
Produce reinforced ore satchel buckles.
Repair Jacob's Pickaxe frames.
Prepare Compact Mule barding plates.
```

### Smiths Request From Miners

Examples:

```text
Deliver Iron Ingots.
Deliver Bronze Ingots.
Deliver Platinum Ingots.
Deliver Obsidian samples.
Deliver Adamantium for reinforcement research.
```

Supplying guild members should earn their own standing/currency.

## 4N — Smith-Made Upgrade Components

Phase 4 should create the first Smith-made component hooks for later systems.

Candidate components:

| Component | Future Use |
| --- | --- |
| Field Smelter Plate | mule smelting rig |
| Reinforced Satchel Buckle | Ore Satchel upgrade |
| Compact Barding Plate | Compact Mule survivability upgrade |
| Tempered Tool Frame | Jacob's Pickaxe future tiers |
| Portable Anvil Fitting | field forge systems |
| Legacy Tool Binding | relic upgrade/restoration support |
| Bellows Assembly | Portable Field Forge upgrade |
| Heatstone Core | Portable Field Forge charge/fuel system |
| Reinforced Forge Frame | Portable Field Forge durability |

These may be crafted items, work order objectives, or future shop/reward items.

## 4O — NPC / World Placement

Use existing Smith-related NPCs first:

- `BlacksmithGuildmaster`
- George Hephaestus
- Gervis

Avoid creating too many new NPCs unless needed.

Possible later NPCs:

- Society Quartermaster
- Master Forgemaster
- Smith Contract Clerk
- Forge Archivist

## Explicit Non-Goals

Do not include in Phase 4 unless explicitly moved into scope later:

- full Miners' Compact Tier 3/Tier 4
- full Compact Mule implementation
- full Ore Satchel upgrade tree
- full Guild Depository system
- full Tinkers Guild
- full Alchemy/Cooking rations and tonics
- full Cartographers Guild
- full Clockwork Scavenger
- full corpse claim
- full dungeon/champion reward rewrite
- full Smith legendary item line

## Completion Checklist

Phase 4 is complete when:

- [x] Society of Smiths can be joined/interacted with through existing Blacksmith Guildmaster flow. *(ClusterFGuildmasterExtension — speech + context menu)*
- [x] Smith status/rank/currency can be viewed. *(SmithGuildmasterGump)*
- [x] Smithing Seals exist and persist. *(GuildCurrency["smithing"] in ClusterFAccountData)*
- [x] Smith work orders exist in the shared ledger. *(ClusterFWorkOrderSystem — 14 smith orders across 5 tiers)*
- [x] Small Smith work orders accept direct crafted item/resource turn-ins. *(SmithCommissionSystem)*
- [x] Large Smith work orders accept direct multi-line turn-ins. *(SmithLargeCommissionEntry + SmithGuildBook commission tab)*
- [x] Extended ingots can be selected and used in Blacksmithy crafting. *(ClusterFMiningExtension.ExtendBlacksmithySubResources)*
- [x] Extended ore crafted items get correct hue/material identity. *(ResourceInfo.cs — hues + CraftAttributeInfo for Platinum→Celestial)*
- [ ] At least several extended ores are tested in real crafted items. *(needs in-game validation)*
- [x] Crafting Make X and Make Max work safely for Blacksmithy. *(CraftGumpItem-MakeX + CraftGump-MakeXClear patches applied)*
- [ ] Blacksmith BODs can Fill From Backpack. *(auto-fill on craft via HammerBODAutoFill is done; manual Fill button in BOD gump is pending)*
- [x] Large BODs accept direct requested crafted item turn-ins rather than completed small deeds. *(SmithCommissionSystem.TurnInLargePiece)*
- [x] BOD claim timer is reduced for small-server pacing. *(ClusterFSmithBODSystem — GetNextBulkOrder returns TimeSpan.Zero)*
- [x] Smiths' Commission Ledger exists or is documented if deferred. *(SmithCommissionGump + SmithGuildBook commission tab)*
- [ ] Portable Field Forge exists or is documented if deferred. *(deferred — not in Phase 4 scope)*
- [x] Hammer of Hephaestus T1 is normalized/restorable. *(HammerOfHephaestus — GuildmasterRestore, HammerRestoreGump)*
- [x] Reinforced Hammer of Hephaestus T2 exists. *(ReinforcedHammerOfHephaestus)*
- [x] Hammer uses/regen scale by tier. *(T1: 150 uses / 30min; T2: 400 uses / 15min)*
- [x] Metal Familiarity foundation exists. *(HammerOfHephaestus.RecordFamiliarity — T1 D-bonus, T2 C-bonus SkillMod)*
- [x] At least one Miners ↔ Smiths cross-guild contract exists. *(smithing.compact_tools_cross in ClusterFWorkOrderSystem)*
- [ ] First Smith-made upgrade components exist or are documented as work order outputs. *(pending — deferred to Phase 4E)*
- [ ] README and command docs are updated.

### Recent completions (2026-05-20)

- **Hammer T1 and T2 base SkillMods** — Both tiers now apply real `DefaultSkillMod` entries:
  T1 grants +5 Blacksmithy; T2 grants +10 base + up to +2.5 familiarity bonus.
  SkillMods applied on `OnAdded`, removed on `OnRemoved`/`OnDelete`/exhaustion, restored via `GuildmasterRestore`.
- **Crafting achievement hooks** — `NotifySmithedItem`/`NotifyTailoredItem`/`NotifyCraftedItem` wired in `CraftItem.cs`;
  `NotifyIngotsSmelted` wired in `SmithGuildSalvageBag.SalvageIngots`.

## Suggested Implementation Order

### Phase 4A — Society of Smiths Foundation

- Smith Guildmaster gump/status/currency
- Smithing Seals
- basic Smith work orders

### Phase 4B — Extended Ore Crafting

- Blacksmithy resource support
- extended ore recipes/properties
- work order recognition

### Phase 4C — Crafting and BOD Quality-of-Life

- Make X / Make Max
- BOD Fill From Backpack
- Large BOD direct item turn-ins
- reduced BOD timer

### Phase 4D — Hammer of Hephaestus

- T1 restore/normalize
- T2 Reinforced Hammer
- use regen/uses scaling
- Metal Familiarity foundation

### Phase 4E — Rewards and Cross-Guild Infrastructure

- Portable Field Forge
- Smiths' Commission Ledger
- Miners ↔ Smiths contracts
- Smith-made upgrade components

## Phase 4F — Powerscroll Skill Cap Expansion (300 → 500)

Extend the individual skill cap from 300 to 500 using a new additive powerscroll system.
Deliver alongside Phase 4E rewards so scrolls have a natural source from the start.

### Design Decisions (locked 2026-05-20)

- **Mechanic**: additive — each scroll raises the target skill's individual cap by the scroll's amount
- **Tiers**: +5 / +10 / +15 / +20 (four tiers, same as vanilla powerscroll count)
- **Ceiling**: 500 hard cap; scrolls cannot push a skill above 500
- **Base cap**: 300 (unchanged — scrolls are required to go higher)
- **Sources**: Champion Spawn loot tables + Society of Smiths / Guild Reward vendors
- **Skill gain**: no tuning needed — natural gain slowdown past 200 is sufficient

### Scroll Progression

A player needs 200 points of scrolls per skill to reach the cap:

| Scroll | Cap delta | Example path to 500 |
|--------|-----------|---------------------|
| +5     | 300 → 305 | 40× +5 scrolls (impractical, filler/starter) |
| +10    | 300 → 310 | 20× +10 scrolls |
| +15    | 300 → 315 | ≈13× +15 scrolls |
| +20    | 300 → 320 | 10× +20 scrolls (optimal) |

Mixed-tier paths are expected. Scrolls from champion spawns trend +5/+10; Guild Reward
vendors sell +15/+20 for Smithing Seals.

### Achievement Milestones to Add

Four generic milestones (any skill) + skill-specific tiers for Mining, Blacksmithy, Magery:

| Tier | Generic key | Title sketch |
|------|-------------|--------------|
| 350  | `skills.transcendent` | "Above and Beyond" |
| 400  | `skills.ascendant`    | "This Requires Special Equipment Now" |
| 450  | `skills.apex`         | "The Cap Has Its Own Cap" |
| 500  | `skills.mythic`       | "Physical Law: Suspended" |

Skill-specific 350/400/450/500 achievements to follow the same chain pattern as existing
Mining/Blacksmithy/Magery tiers.

### Implementation Checklist

- [ ] Raise `DefaultIndividualSkillCap` from 300.0 → 500.0 in `ClusterFSkillCaps.cs`
- [ ] Create `SkillCapExtenderScroll` item (4 subclasses or parameterized) — applies +N to target skill cap, clamps at 500
- [ ] Wire into Champion Spawn loot tables (+5/+10 weight)
- [ ] Wire into Guild Reward vendor (`NotifySmithedItem` chain) for +15/+20
- [ ] Add `NotifySkillValue` thresholds at 350/400/450/500
- [ ] Add `OnLogin` breadth checks for new tiers
- [ ] Add ~16 achievement definitions (generic 350–500 + skill-specific)

## Deferred — Chromatic Dragon Armor System

Dragon scale armor uses `CraftResourceType.Scales` (not `CraftResourceType.Metal`).  
The Hammer of Hephaestus BOD auto-fill **correctly ignores** scale-resource armor — a guard in
`HammerBODAutoFill.TryAutoFill` short-circuits before any BOD matching occurs.

The full chromatic system is deferred to a future phase. Rough design intent:

- **Chromatic Dragon Scales** — crafted via a new skill (TBD; likely `Tailoring` or a new
  `Dragoncraft` skill) using raw dragon scales + reagents.  Each color family (Red, Blue, Yellow,
  White, Black, Green) produces a distinct chromatic variant.
- **Chromatic Dragon Armor** — armor set crafted from chromatic scales.  Quality/tier mirrors the
  extended ore tiers but uses scale material type rather than metal.
- **BOD support** — a new `LargeDragonBOD` (or scale-material variant of `LargeSmithBOD`) would be
  issued through a separate guild or as a specialty order from the Society of Smiths.
- **Auto-fill** — a separate auto-fill path (not `HammerBODAutoFill`) keyed on the new crafting tool.
- **Scale salvage** — `SmithGuildSalvageBag` already correctly excludes scale armor from the ingot
  resmelt path; scale armor should go through a cut/salvage path (scissors) if salvage is desired.

No implementation work needed now.  Update this section when the design is locked.

## Phase Summary

```text
Phase 4 turns mined resources into crafted value and proves the guild framework works for a second profession.
```
