# Artificers' Order — Design Document

## Identity

**Guild Key:** `artificers`
**Display Name:** Artificers' Order
**Guildmaster NPC:** Thornwick Vesper, "the Artificers' Guildmaster"
**Location:** Royal City, Ter Mur — `793, 3468, Z=-20, Map.TerMur`
**Also spawned:** New Haven — `3490, 2627, Z=0, Map.Trammel`
**Currency:** Essence Shards
**Primary Skill:** Imbuing (join requires Imbuing 20+ OR 3 Diamonds)

---

## Rank Thresholds

| Standing | Rank |
| --- | --- |
| 0 | Apprentice |
| 1,000 | Journeyman |
| 5,000 | Artificer |
| 15,000 | Master Artificer |
| 40,000 | Arcane Artisan |

---

## Imbuing System

### Property Catalogue (38 properties across 4 groups)

**General:** HCI, DCI, LMC, LRC, SDI, FCR, FC, SpellChanneling, NightSight, Luck, EnhancePotions, ReflectPhysical

**Stats:** Str, Dex, Int, HP Regen, Stam Regen, Mana Regen

**Weapon:** SSI, Damage Increase, Mana Leech, Life Leech, Stamina Leech, Hit Lower Attack, Hit Lower Defense, Hit Fireball, Hit Lightning, Hit Harm, Hit Dispel, Hit Magic Arrow, Hit Curse, Hit Fatigue, Hit Mana Drain, Hit Confusion, Hit Cold Area, Hit Fire Area, Self Repair, Lower Stat Req

**Defense:** Physical Resist, Fire Resist, Cold Resist, Poison Resist, Energy Resist, Self Repair, Lower Stat Req, MageArmor

### Vanilla Cap (no rank gate)

- 5 properties per item
- 100% intensity cap

### Guild Rank Unlocks

| Rank | Properties | Intensity Cap |
| --- | --- | --- |
| Apprentice (0) | 5 | 100% |
| Journeyman (1,000) | 6 | 120% |
| Artificer (5,000) | 7 | 150% |
| Master Artificer (15,000) | 8 | 175% |
| Arcane Artisan (40,000) | 8 | 200% |

### Discovery Threshold → Rank Gate

Higher-tier properties require minimum standing to imbue:

| Discovery Threshold | Min Standing | Min Rank |
| --- | --- | --- |
| 3 or lower | 0 | Apprentice |
| 5 | 1,000 | Journeyman |
| 8 | 5,000 | Artificer |
| 10 | 15,000 | Master Artificer |

### Imbue Mechanics

- Target any weapon, armor, jewelry, or clothing item
- Skill check vs Imbuing skill; failure removes 10 durability from item (no cost consumed)
- Success deducts Essence Shards + gold, earns Artificer standing
- Rank-locked properties show greyed in UI with `[req: Master Artificer]` label; hard server-side check prevents bypass

### Disenchanting

Items can be disenchanted to recover Essence Shards (partial yield based on item tier and properties).

---

## Work Orders / Commissions (ArtificerWorkOrderSystem.cs)

**Pool size:** 39 orders
**Design:** Fixed pool — player gets blank/base item spawned into backpack; must imbue to meet requirements; turn in at guildmaster

### Order Types

**Regular Orders** — item spawned directly (tracked by `ActiveArtificerItemSerial`)
- Weapons: longsword, katana, kryss, war hammer, bow, crossbow, halberd, etc.
- Armor: plate, chain, leather, studded pieces
- Jewelry: rings, bracelets
- Clothing: robe, wizard's hat

**Combo Orders** — player crafts item via Smith guild, then enchants via Artificers
- Require membership in BOTH `artificers` AND `smithing` guilds
- Match on `item.GetType() == RequiredItemType && bw.Resource == RequiredResource`
- Examples: Valorite plate breastplate with Strength + Fire Resist imbues; Agapite katana with SSI + Damage; Verite kite shield with resistances

### Availability Gate

All orders require mastery of every required property (discovered + mastered via imbuing repetition). Combo orders additionally require Smith guild membership.

### Turn-In Flow

1. Player opens Work Orders gump from Artificers' Guildmaster
2. Selects available order → item spawned in backpack (regular) or player crafts item (combo)
3. `ActiveArtificerOrderKey` + `ActiveArtificerItemSerial` stored in `ClusterFAccountData` (v13)
4. Player imbues required properties onto item
5. Returns to guildmaster → Turn In → gold + Essence Scrip + Artificer standing paid out; item removed

### Rewards

Each order pays gold + `artificerScrip` (Essence Shards) + `artificerRep` (standing). Combo orders also pay `smithScrip` + `smithRep` to the Smith guild simultaneously.

---

## ClusterFAccountData Changes

| Version | Change |
| --- | --- |
| v13 | `ActiveArtificerOrderKey`, `ActiveArtificerItemSerial`, `HasActiveArtificerOrder` |

Methods: `AcceptArtificerOrder(key, serial)`, `ClearArtificerOrder()`

---

## Files

| File | Purpose |
| --- | --- |
| `ArtificersGuildmaster.cs` | NPC class — extends BaseGuildmaster |
| `ArtificersGuildmasterGump.cs` | 6-view gump; GetMinStanding/GetRequiredRankName rank helpers |
| `ArtificersImbueGump.cs` | Full imbuing engine with rank gating |
| `ArtificerWorkOrderSystem.cs` | Work order defs, catalogue (39 orders), gump, turn-in logic |
| `ClusterFInstitutionSeeder.cs` | Spawns Thornwick Vesper at TerMur + New Haven |

---

## Pending / Future Work

- Mastery progression: how many successful imbues unlock a property at each threshold? (TBD — currently discovery-based)
- Essence Satchel / Toolkit: issued on join (implemented in `ArtificersEssenceSatchel.cs`)
- Higher-tier imbuing reciprocals (e.g. dual-resist, combo properties)
- Arcane Artisan rank services and rewards
- Legacy Item: "Thornwick's Crucible" or similar imbuing relic from New Haven quest chain
- Disenchanting gump and yield tables
