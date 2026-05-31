# Phase 3 — Guild Work Orders, Discovery Records, and Expedition Logistics

## Objective

Phase 3 is a shard-wide systems phase, piloted through the Miners' Compact.

The goal is not to build every future guild system at once. The goal is to establish reusable frameworks for:

- guild work orders
- active contract tracking
- discovery records
- profession logistics containers
- future guild resource infrastructure

The Miners' Compact is the first guild to fully exercise these systems, but the systems should be designed so Smiths, Tinkers, Mages, Rangers, Alchemists, Cartographers, Healers, and future guilds can reuse them.

## Phase Identity

```text
Phase 1 = League onboarding foundation
Phase 2 = first profession vertical slice
Phase 3 = reusable guild contract, discovery, and logistics infrastructure
```

Phase 3 should make mining feel like an expedition profession while keeping the architecture shard-wide.

## Primary Pilot Loop

```text
Mine ore
→ ore routes into a Compact Ore Satchel
→ rare/extended discoveries are recorded in a Prospector's Logbook
→ discoveries are reported to a Survey Archivist
→ work order eligibility improves
→ small/large work orders are fulfilled through direct item/resource turn-ins
→ rewards improve guild standing, currency, and future logistics unlocks
```

## Shard-Wide Work Order Framework

Phase 3 should expand the Phase 2 simple iron order into a reusable Guild Work Order system.

### Work Order Types

| Type | Purpose |
| --- | --- |
| Resource Contract | ore, ingot, granite, saltpeter, reagent, or material deliveries |
| Crafted Supply Contract | crafted tools, consumables, equipment, or components |
| Expedition Contract | discover, gather, or retrieve something from a dangerous/remote area |
| Survey Contract | record/report region, ore, treasure, or map data |
| Refinement Contract | smelt/process/refine raw materials |
| Recovery Contract | recover tools, samples, relics, or survey equipment |
| Cross-Guild Contract | one guild requests goods or services from another guild ecosystem |

### Small Work Orders

Small Work Orders are simple single-line contracts.

Examples:

```text
Deliver 250 Iron Ingots.
Craft 10 Pickaxes.
Prepare 20 Miner's Rations.
Report one Frost vein in Ilshenar.
```

### Large Work Orders

Large Work Orders are multi-line project contracts.

Examples:

```text
Compact Quarry Expansion Order:
- 1000 Iron Ingots
- 500 Granite
- 100 Bronze Ingots
- 20 Mining Picks
```

```text
Expedition Supply Order:
- 10 Pickaxes
- 10 Shovels
- 5 Survey Tools
- 20 Trail Rations
```

### Direct Turn-In Rule

Large Work Orders must accept direct item/resource turn-ins.

```text
Large work orders should not require smaller completed deeds to fulfill them.
```

This should eventually inform modernization of vanilla Bulk Order Deeds as well.

## Physical Work Orders and Active Ledger

Phase 3 should support both a physical and UI-based tracking model.

### Physical Contract Items

Potential physical items:

| Item | Purpose |
| --- | --- |
| Small Guild Work Order | simple single-line contract |
| Large Guild Work Order | multi-line contract |
| Expedition Charter | dangerous/remote area assignment |
| Survey Commission | discovery/reporting contract |

Physical contracts keep the system grounded in UO's item-driven feel.

### Guild Contract Ledger / Gump

Physical contracts alone are not enough. Players need a way to view active objectives without digging through bags.

The Active Contract Ledger should show:

- active work orders
- objective text
- progress
- rewards
- issuer guild
- target guild, if any
- required facet/region, if any
- abandon/complete options where appropriate
- completed/recent history if practical

Suggested tabs:

| Tab | Purpose |
| --- | --- |
| Active | current work orders |
| Available | claimable work orders |
| Completed | recent history |
| Surveys | discovery/report contracts |
| Expedition | dangerous/remote contracts |
| Large Orders | project contracts |

## Prospector's Logbook Pilot

The Prospector's Logbook is the Miners' Compact pilot for account-backed discovery records.

### Purpose

- track ore discoveries
- encourage exploration
- unlock contract eligibility
- support future Cartography and ClassicUO map integration

### Acquisition

Issued by the Miners' Compact. Replacement copies should require Mining Vouchers and/or Compact standing.

### Data Ownership

Discovery data should be account-backed, not stored only on the physical item.

### Tracked Data

| Field | Description |
| --- | --- |
| Ore Type | discovered resource |
| Facet | map/facet |
| Region | region or subzone |
| Coordinates | approximate location |
| First Discovery | first time found |
| Quantity Mined | cumulative mined amount |
| State | discovered/logged/reported/copied/verified |

### Logging Defaults

Iron should be excluded from automatic logging by default.

Players should eventually be able to configure logging filters by ore type, facet, or rarity.

### Logbook Tier Upgrades (Future)

Higher-tier logbook versions should unlock additional capabilities. Planned progression:

| Tier | Name | New Capability |
| --- | --- | --- |
| 1 | Prospector's Logbook | Discovery recording and viewing (current) |
| 2 | Surveyor's Field Journal | Recall/gate to approximate discovery coordinates — opens a targeting option from the logbook gump that casts a Recall or opens a moongate near the recorded location |
| 3 | Master Expedition Ledger | Cartography export, discovery pin sharing, ClassicUO map marker integration |

**Tier 2 travel note:** The travel feature should target the *approximate* recorded location, not exact coordinates, to preserve the exploration feel. A small random offset (±10–20 tiles) is preferred over a pinpoint teleport. The feature should respect standard Recall/Gate rules (no Felucca abuse protection bypass, etc.) and cost a charge or reagents consistent with the tier of book held.

## Survey Archivist Pilot

The Survey Archivist is the first discovery-reporting authority.

Functions:

- review Prospector's Logbook entries
- show unreported discoveries
- accept discovery reports
- grant Compact standing and Mining Vouchers
- unlock future contract eligibility
- show survey progress

## Discovery-Based Contract Eligibility

Contracts should not be generated from skill alone.

Eligibility should consider:

- guild rank
- guild standing
- relevant skill level
- reported discoveries
- facet/region progression
- prior contract completion

Example:

```text
A player with 300 Mining but no Obsidian discoveries should not receive Obsidian contracts.
```

```text
A high-skill Initiate should still receive beginner contracts until they build guild trust.
```

## Facet Contract Rules — Mining Pilot

The mining pilot should use these rules, but the pattern should support other professions later.

### Trammel and Felucca

Normal mining contracts should stop at Valorite.

Included:

- Iron
- Dull Copper
- Shadow Iron
- Copper
- Bronze
- Gold
- Agapite
- Verite
- Valorite

Felucca remains higher-yield/higher-risk.

### Other Facets

Other facets should focus on extended ore identities.

| Facet | Contract Focus |
| --- | --- |
| Ilshenar | Toxic, Blaze, Frost |
| Malas | Obsidian, Mythril |
| Tokuno | Platinum, Mythril |
| Ter Mur | Adamantium, Celestial |

Trammel and Felucca may still rarely spawn extended ores, but normal contracts should not request them.

## Compact Ore Satchel Pilot

The Compact Ore Satchel is the first profession logistics container.

It should extend the existing ore satchel behavior rather than replace it.

Known existing ore satchel asset/behavior:

```text
Name: Ore_Satchel_east
Graphic: 0xA272
Stores: ore, ingots, granite, saltpeter
Weight Reduction: 50%
```

### Phase 3 MVP

The Compact Ore Satchel should:

- use or inherit from the existing ore satchel behavior
- preserve ore/ingot/granite/saltpeter support
- preserve or improve weight reduction
- support increased capacity
- support auto-routing of newly mined ore into the satchel when carried by the player
- expose a basic status/gump if practical

Preferred routing behavior:

```text
If a Compact Ore Satchel exists in the player's backpack, newly mined ore attempts to route there automatically.
```

Future routing order:

```text
1. Satchel
2. Backpack
3. Mule overflow later
4. Guild logistics/depository later
```

### Satchel Progression Hooks

| Tier | Name | Purpose |
| --- | --- | --- |
| 1 | Compact Ore Satchel | auto-routing and basic logistics |
| 2 | Reinforced Ore Satchel | more capacity and better reduction |
| 3 | Surveyor's Satchel | survey/logbook integration |
| 4 | Deepdelver Satchel | smelting and fragment recovery hooks |
| 5 | Master Expedition Satchel | mule/depository integration hooks |

### Future Satchel Upgrades

Document but do not fully implement unless trivial:

- Smelt All action
- ore fragment recovery for leftover ore pieces
- sorting/filtering by ore type
- region/source tagging
- arcane compression
- mule overflow
- shared logistics UI
- depository deposit integration

## Guild Logistics and Resource Infrastructure

This is a major roadmap initiative, not a strict Phase 3 completion requirement.

Phase 3 should design work orders and satchels so they can eventually integrate with a resource depository system.

### Future Guild Resource Depositories

First likely implementation:

```text
Compact Material Depository
```

Future guild versions:

- Smiths Guild Foundry Stores
- Alchemical Vaults
- Tinker Supply Crates
- Cartographer Archives
- Ranger Expedition Lockers

### Depository Philosophy

Resources should remain physical, mineable, tradable, and transportable.

Depositories allow players to deposit approved guild materials for future contract fulfillment.

This reduces backpack/bank clutter without creating an infinite magic crafting bag.

### Initial Depository Restrictions

When implemented, prefer refined/processed materials first:

- ingots
- granite
- saltpeter

Avoid storing raw ore initially so smelting/refinement remains meaningful.

### Future Depository Features

- Deposit Eligible Materials
- Deposit from backpack
- Deposit from Ore Satchel
- Deposit from Compact Mule cargo
- fulfill contracts from deposited balances
- reserve resources for active work orders
- shared household/guild contribution pools later
- remote warehouse access as a later high-tier feature

## Compact Mule Hooks

The Compact Mule line is a future reward/logistics system. Phase 3 should not fully implement it, but should keep satchel and work-order design compatible with mule cargo later.

Future hooks:

- satchel overflow to mule cargo
- mule-mounted smelting rig
- shared satchel/mule logistics gump
- cross-guild mule modules
- expedition hauling contracts

See `containers/uo/design/companions/compact_mule.md`.

## Cross-Guild Economy Hooks

Phase 3 should lay groundwork for other guilds to participate in future logistics and work-order systems.

| Guild | Future Role |
| --- | --- |
| Smiths | smelting rigs, field forge plates, mule/satchel reinforcement |
| Tinkers | sorting mechanisms, load harnesses, Clockwork logistics |
| Mages | arcane compression, wards, magical storage bindings |
| Rangers | trail gear, wilderness recovery, mule tack |
| Healers | recovery packs, stabilization, expedition medicine |
| Cartographers | survey maps, route charts, map pins |
| Inscription | survey copies, ledger inserts, map exports |
| Alchemists | stabilization dust, blasting supplies, preservation tonics |
| Cooks | rations and expedition meals |

## Unified Loot Category Framework

If not already completed, Phase 3 may include a minimal shared loot/item category framework because future work orders, logistics, scavenger systems, and reward packages will need it.

Minimum scope:

- `LootCategory`
- `LootTag`
- item/resource classifier
- debug classify command

Do not implement corpse claim, Clockwork Scavenger, or loot vacuuming in Phase 3.

## Explicit Non-Goals

Do not include in Phase 3 unless explicitly moved into scope later:

- full Compact Mule implementation
- mule breeding
- full guild depository system
- global infinite storage
- full Cartographers Guild
- ClassicUO marker export
- Clockwork Scavenger
- corpse claim
- Expedition Pack modules
- Tier 3/Tier 4 Jacob's Pickaxe
- Gargoyle Pickaxe redesign
- dungeon chest overhaul
- champion spawn rewrite
- food/drink buff system
- tonics/rations implementation

## Completion Checklist

Phase 3 is complete when:

- [x] reusable Guild Work Order structure exists (`ClusterFWorkOrderSystem.cs` — `WorkOrderDef`, `WorkOrderEntry`, `WorkOrderRequirement`)
- [x] small work orders support direct turn-ins
- [x] large work orders support direct multi-line turn-ins (multi-requirement orders: Forge Resupply, Deep Veins Run, Mixed Mid-Tier Metals, Rare Metals Commission, Grand Forge Tribute)
- [x] active work orders can be viewed through an item/gump/ledger (`GuildContractLedgerGump` — Available/Active/History tabs)
- [x] work order progress persists safely (`ClusterFAccountData` v4 — `ActiveWorkOrders` + `CompletedWorkOrders`)
- [x] Miners' Compact has varied work order examples beyond simple iron delivery (25 orders across Initiate → Deepwarden tiers, including 8 extended ore orders gated by discovery)
- [x] Prospector's Logbook exists as the first discovery record item (`ProspectorsLogbook.cs` — blessed, account-backed, dynamic height gump)
- [x] logbook discovery data is account-backed (`ClusterFAccountData` v5 — `OreDiscoveries` dictionary)
- [x] ore discoveries can be logged (`CompactOreSatchelRoutingHook.cs` — `TryLogDiscovery()` fires on every colored ore yield)
- [x] Survey Archivist can accept discovery reports (`SurveyArchivist.cs` + `SurveyArchivistGump.cs` — Velara Thorne at Trammel 3516, 2747; Report All flow grants Standing + Vouchers)
- [x] reported discoveries can influence contract eligibility (`WorkOrderDef.RequiredDiscovery` + `IsEligible()` gate; all colored ore orders gated by discovery state)
- [x] Compact Ore Satchel extends existing ore satchel behavior (`CompactOreSatchel.cs` — inherits container, 50% weight reduction, 400 max weight)
- [x] newly mined ore can auto-route to the Compact Ore Satchel (`CompactOreSatchelRoutingHook.cs` — partial `Mining.Give()` override)
- [x] satchel preserves ore/ingot/granite/saltpeter support (`CompactOreSatchel.Accepts()` — all ore and ingot types Iron → Celestial, plus Granite)
- [x] satchel/depository/mule hooks are documented (see "Satchel Progression Hooks" and "Future Satchel Upgrades" sections above)
- [x] README and relevant design docs are updated (2026-05-15)

## Artificers' Order — Phase 3 Extensions (2026-05-31)

Work order framework extended to Artificers' Order with a dedicated commission system distinct from the generic `ClusterFWorkOrderSystem`:

- [x] `ArtificerWorkOrderSystem.cs` — 39 fixed-pool commissions (weapons, armor, jewelry, clothing, combo Smith+Artificer orders)
- [x] Guild rank gating on imbuing: discovery threshold → min standing map enforced both UI and server-side
- [x] `ClusterFAccountData` v13 — `ActiveArtificerOrderKey` + `ActiveArtificerItemSerial` track active commission
- [x] Regular orders: item spawned directly into player backpack; tracked by serial; verified on turn-in
- [x] Combo orders: player crafts item via Smith guild, turn-in matches by type + resource + enchantments
- [x] `ArtificersGuildmasterGump` Work Orders button wired; `GetMinStanding` / `GetRequiredRankName` helpers
- [x] Design doc: `containers/uo/design/guilds/artificers_order.md`

## Foresters, Hunters, and Outriders Extensions (2026-05-31)

Guild logistics containers expanded beyond Miners' Compact:

- [x] `ForestersLumberSatchel.cs` (T1), `SeasonedLumberSatchel.cs` (T2), `ArboristLumberSatchel.cs` (T3) — tiered lumber routing containers
- [x] `HuntersSatchel.cs` — hunting materials container
- [x] `PackMule.cs` / `PackMuleBackpack.cs` / `PackMuleDeed.cs` / `BredPackMuleDeed.cs` — rideable pack mule with custom storage, purchasable from Outriders
- [x] `PackMuleBreedingSystem.cs` — stat-averaged pack mule breeding with generation tracking
- [x] `OutridersGuildmasterGump.cs` — full Outriders guild UI (pack mule purchase, breeding submission)
- [x] `ShrunkPet.cs` — shrunken pet item for Ranger/Outrider breeding window
- [x] `CrossGuildExchangeGump.cs` — cross-guild scrip exchange interface

## World Seeding Extensions (2026-05-31)

- [x] `ClusterFRoyalCitySeeder.cs` — Ter Mur royal city NPC placement
- [x] `ClusterFGargoyleVendors.cs` — gargoyle-specific vendor spawns

## Implementation Notes (2026-05-15)

### Permanent Ore Veins

`ClusterFMiningExtension` sets `OreAndStone.RandomizeVeins = false` after registering extended veins.

When `RandomizeVeins = false`, ModernUO's `HarvestDefinition.GetVeinAt()` uses a deterministic coordinate seed:

```csharp
seed = (ulong)(x * 17 + y * 11 + map.MapID * 3)
```

Each 8×8 bank cell's ore type is fixed forever — consistent across respawns and server restarts. This is a prerequisite for the Tier 2 logbook travel feature: the recorded discovery location will always yield the same ore type.

### Work Order Ledger — Rank Tabs (Deferred)

Rank-based tabs in `GuildContractLedgerGump` are deferred until the second guild is onboarded or visible order count exceeds ~8 per player. Current `IsEligible()` filtering keeps the visible list short (2–3 for new members, ~6–7 at Journeyman). Tab infrastructure should be designed once the second guild rank structure is known to avoid premature abstraction.

### Admin Testing Commands Added

| Command | Purpose |
| --- | --- |
| `[CompactWipeLogbook` | Clears all ore discoveries from targeted player |
| `[CompactSeedDiscoveries [Discovered\|Reported]` | Seeds all 16 ore types into logbook at specified state |

## Implementation Notes (2026-05-21)

### Compact Dispatch Ledger — Remote Work Order Access

`CompactDispatchLedger.cs` added. Issued to every player on joining the Miners' Compact (alongside the Ore Satchel and Prospector's Logbook).

- Double-clicking the blessed item opens `GuildContractLedgerGump("mining")` from anywhere in the world.
- Removes the requirement to stand within 4 tiles of Garrett Ashveil to check, accept, or turn in work orders.
- Turn-ins still require the player to have the required materials in their backpack.
- Must be a Compact member to use.

### Progressive Extended Ore Availability

`CompactOreSatchelRoutingHook.cs` updated. Extended ores (Platinum → Celestial) are now gated behind the Survey Archivist reporting flow.

**Flow:**

```text
1. Player mines an extended ore vein for the first time.
   → TryLogDiscovery() creates OreDiscoveryEntry (Discovered state).
   → A one-time message explains the technique is unknown.
   → Player receives iron ore instead (same quantity).

2. Player visits Survey Archivist and reports the discovery.
   → OreDiscoveryEntry state advances to Reported.
   → Work orders for this ore type appear in the ledger.

3. All subsequent mining of this ore type yields the actual extended ore normally.
```

Vanilla ores (DullCopper → Valorite) are not gated — they remain always accessible.

Testing bypass: DevTestingCrystal skips the gate and delivers extended ore directly.

### Work Order Reward Rebalance

Two orders had inverted per-unit returns (smaller order was more efficient per ingot).

Fixed in `ClusterFWorkOrderSystem.cs`:

| Order | Before | After |
| --- | --- | --- |
| `mining.iron_ingots_m` (250 iron) | 400 standing, 18 vouchers (1.6/ingot) | 600 standing, 30 vouchers (2.4/ingot) |
| `mining.valorite_cache` (20 valorite) | 3000 standing, 120 vouchers (150/ingot) | 6000 standing, 240 vouchers (300/ingot) |

Both larger orders now offer a meaningful per-unit premium over their small counterparts.

## Implementation Notes (2026-05-21c)

### Gold Coin Rewards Added to All Mining Work Orders

All mining work orders that previously had `goldReward: 0` now pay gold coins, giving players a guild-backed path to fund pickaxe restorations and upgrades.

Revised gold reward ladder:

| Order | Tier | Gold |
| --- | --- | --- |
| `iron_ingots_s` (50 iron) | Initiate | 75gp |
| `iron_ore_s` (100 iron ore) | Initiate | 60gp |
| `iron_ingots_m` (250 iron) | Initiate/30 Mining | 350gp |
| `dull_copper_ingots_s` (30 DC) | Apprentice | 350gp |
| `shadow_iron_s` (20 SI) | Journeyman | 500gp |
| `copper_s` (20 copper) | Journeyman | 500gp |
| `ore_refinement_s` (150 iron ore) | Journeyman | 400gp |
| `bronze_s` (15 bronze) | Surveyor | 1,000gp |
| `agapite_s` (12 agapite) | Surveyor | 1,500gp |
| `verite_s` (8 verite) | Master Delver | 2,500gp |
| `valorite_s` (6 valorite) | Master Delver | 4,000gp |
| `valorite_cache` (20 valorite) | Deepwarden | 10,000gp |

Orders that already had gold rewards (`forge_resupply_m`, `deep_veins_m`, `mid_tier_metals_m`, `high_metals_m`, `grand_tribute`, and all extended ore orders) were unchanged.

### Replace Starting Kit — Liaison Gump

`MinersCompactLiaisonGump` updated:

- New `View.ReplaceKit` added to the enum.
- "Replace Starting Kit" button added to Member Dashboard (button 38).
- New `DrawReplaceKit()` view shows which of the 3 starting items are present in the player's backpack and offers individual replace buttons for any that are missing.
- `HandleReplaceKitItem<T>()` generic helper handles the cost check, voucher deduction, and item delivery.
- Cost: **5 Mining Vouchers per item**. Bypassed by DevTestingCrystal.
- Only replaces items not currently in the backpack (prevents free duplicates). Players who put their items in a house/container are told to retrieve them instead.
- Back button from `ReplaceKit` view returns to `MemberDashboard` (same as `Restoration`).

## Implementation Notes (2026-05-21b)

### ClusterF Mining Radar — RazorEnhanced Script

`C:\Users\chase.fleming\Downloads\ClusterFMiningRadar.py`

Adapted from CyberPope's Mining Radar 4.1d_F002.euo for RazorEnhanced / IronPython 3.

**What it does:**

- Displays a 9×9 tile grid centered on the player (WinForms window, always-on-top)
- Tiles are colored by ore type once known, dark gray when mineable but uncharted, olive when not mineable
- Click any cave/mineable cell to mine that tile (fires `Player.UseObject` + `Target.SetXYZ`)
- Detects ore type and depletion from journal messages; caches results to `%USERPROFILE%\Documents\RazorEnhanced\clusterf_mining_cache.json`

**How detection works:**

ClusterF's `TryLogDiscovery()` embeds exact coordinates in every discovery message:
```
"Discovery recorded: Valorite ore in Wilderness, Trammel (1234, 5678)."
"New Valorite vein logged: Wilderness, Trammel (1234, 5678)."
```
The radar parses these and caches the ore type at those exact tile coordinates. Because `RandomizeVeins = false`, the cache is permanent — an entry written once is never stale.

Depletion detection uses standard UO messages (`"There is no metal here to mine"`).

**Ore types covered:**

All 17 ClusterF ores: Iron, DullCopper, ShadowIron, Copper, Bronze, Gold, Agapite, Verite, Valorite, Platinum, Toxic, Blaze, Frost, Obsidian, Mythril, Adamantium, Celestial.

**Mineable tile set:**

Uses the exact land tile ID list from CyberPope's original EUO script (`%digable` variable, ~250 tile IDs covering cave floors, rock faces, and packed earth across all facets).

**To install:**

Copy `ClusterFMiningRadar.py` to your RazorEnhanced scripts folder (typically `Documents\RazorEnhanced\Scripts`), add it in the Scripts tab, and click Run. A pickaxe or shovel must be in your backpack or equipped.

## Phase Summary

```text
Phase 2 made the first profession guild loop work.
Phase 3 turns that loop into reusable guild contracts, discovery records, and expedition logistics.
```
