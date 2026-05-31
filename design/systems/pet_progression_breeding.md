# Pet Progression, Leveling, and Breeding System

## Overview

All tameable creatures on Shattered Legacy support:

1. **Gender** — male/female assigned at spawn, visible on cursor hover
2. **Leveling** — XP from kills, level cap 20, Training Points per level
3. **Breeding** — stat-averaged offspring with trait/ability inheritance; managed via Rangers' Lodge / Outriders guild

---

## Gender System (PetGenderSystem.cs — PENDING)

### Assignment

- Assigned at spawn via `OnAfterSpawn()` in `BaseCreature.cs` (2-line hook)
- `Mobile.Female` used as the gender flag (already serialized by ModernUO)
- 50/50 random unless species has skewed ratio (rare — e.g. alpha creatures)
- WorldLoad sweep assigns gender to any existing tameable that lacks it

### Species-Aware Names

| Species | Male | Female |
| --- | --- | --- |
| Horse/War Horse | Stallion | Mare |
| Cat | Tom / Tomcat | Queen |
| Dog/Wolf | Male | Female |
| Bull | Bull | Cow |
| Dragon/Drake | Male | Female |
| All others | Male | Female |

### Hover Display

`GetProperties()` hook appends gender line below name:

```
Ashhoof the Faithful
Mare
```

Implemented via `PetGenderSystem.AppendGenderProperty(this, list)` in `BaseCreature.GetProperties()`.

---

## Leveling System (PetProgressionSystem.cs — PENDING)

### XP Sources

- **Kill XP:** `EventSink.CreatureDeath` — scans aggressors for tamed pets; XP awarded to pet based on target's difficulty (fame/karma/hit points)
- **Bonus XP:** kills in dungeons or against champions yield bonus multiplier

### Level Progression

| Level | XP Required (cumulative) |
| --- | --- |
| 1→2 | 100 |
| 2→3 | 250 |
| ... | scales exponentially |
| 19→20 | ~50,000 |

Level cap: **20**

### Training Points

Each level grants **5 Training Points** (TP) in addition to automatic baseline gains.

| TP Cost | Stat Gain |
| --- | --- |
| 1 TP | +2 Str, Dex, or Int |
| 1 TP | +5 HP, Stam, or Mana |
| 2 TP | +0.5 to a combat skill |
| 3 TP | Unlock / upgrade a trait slot |

### Baseline Auto-Gains

Each level also grants small automatic increases:
- +1 to lowest primary stat (Str/Dex/Int)
- +3 HP max
- Minor skill gain in primary combat skill

### Persistence

Stored on the creature directly via serialized fields (uses SerializationGenerator pattern):
- `int PetLevel`
- `int PetXP`
- `int PetTrainingPoints`

---

## Trait System

Pets have up to **3 trait slots** (unlocked by spending Training Points).

### Example Traits

| Trait | Effect |
| --- | --- |
| Flameblood | +15% fire damage; resists fire better |
| Stonehide | +20 physical resist; slower movement |
| Swiftpaw | +10 Dex; +5% speed |
| Deepdelver | Bonus XP in underground/dungeon zones |
| Moonmarked | Enhanced magic skill gains; +SDI |
| Wildborn | Bonus XP vs wilderness creatures |
| Guardian | Reduced HP damage when owner is attacked |
| Loyal | Faster re-bond if killed and resurrected |

### Trait Inheritance

At breeding: each parent's trait has a **55% chance** of passing to offspring per trait slot.

---

## Breeding System (PetBreedingSystem.cs — PENDING)

### Requirements

- Both pets must be tamed and bonded to same owner
- Requires access to Rangers' League or Outriders guild (ranger scrip cost TBD)
- Both pets shrunk via `ShrunkPet` item and submitted to Outriders guild NPC
- **Breeding window:** 24 hours (normal) / 48 hours (cross-species)
- Pets returned after window regardless of outcome

### Species Rules

- **Same-species breeding:** always produces same-species offspring
- **Cross-species breeding:** allowed — all tameables can breed with each other
  - Offspring body graphic: 50/50 random from either parent
  - Offspring hue: 40% parent A / 40% parent B / 20% mutation
  - Max 2 inherited abilities (tied to trait slots)

### Stat Calculation

All numeric stats (Str, Dex, Int, HP, Stam, Mana, skills):

```
offspring_stat = (parent_a_stat + parent_b_stat) / 2  ±5% random variance
```

Skills averaged the same way — cross-species breeding is how magic skills transfer to non-magic creatures (e.g. horse × dragon can yield a horse-bodied creature with Magery/EvalInt).

### Ability Inheritance

| Ability | Inherit Chance |
| --- | --- |
| Fire Breath | 35% |
| Magery | 50% |
| EvalInt | 50% |
| Poisoning | 45% |
| Wrestling | always (if either parent has it) |
| Magic Resist | 60% |

Abilities only transfer if a trait slot is available on the offspring.

### Generation Tracking

```
offspring.Generation = floor((parent_a.Generation + parent_b.Generation) / 2) + 1
```

**Breeding Cap:** generation 10 (offspring beyond this cannot breed further).

**Fresh Bloodline Mechanic:** If either parent has `Generation = 0` (wild-caught, never bred), the offspring generation resets to `floor(other_parent.Generation / 2) + 1`, allowing continued breeding lines to be refreshed with wild stock.

### Offspring

- Born as a baby/young creature (or hatchling for reptiles) — TBD per species
- Starts at Level 1 with inherited stats
- Gender assigned 50/50 at birth
- Bonding required before use (reduced bond timer applies)

---

## Guild Integration (OutridersBreedingGump.cs — PENDING)

### Submission Flow

1. Player shrinks both pets via `ShrunkPet` item
2. Visits Outriders guild NPC (Rangers' Lodge location TBD)
3. Opens breeding gump → selects Pet A and Pet B from backpack ShrunkPets
4. Pays scrip cost → pets submitted; timestamp stored in `ClusterFAccountData` v14
5. After 24/48 hours, returns to collect offspring (and original pets returned)

### ClusterFAccountData v14 (PENDING)

Fields to add:
- `ActiveBreedingPetA` (ShrunkPet serial or reference)
- `ActiveBreedingPetB` (ShrunkPet serial or reference)
- `BreedingCompletesAt` (DateTime)
- `HasActiveBreeding` (bool derived)

### Compatibility Check Display

Gump shows predicted offspring stats (averaged ranges), chance of each trait/ability inheriting, and whether cross-species rules apply.

---

## Implementation Plan

### Phase A — Gender (Quick, needed for migration)
1. Create `PetGenderSystem.cs` with `AssignGenderIfTameable()` + `AppendGenderProperty()`
2. Add 2 calls in `BaseCreature.cs`: `OnAfterSpawn()` + `GetProperties()`
3. WorldLoad sweep for existing creatures

### Phase B — Leveling
1. Create `PetProgressionSystem.cs` with XP tables, level-up logic, EventSink.CreatureDeath hook
2. Add serialized fields to `BaseCreature` (PetLevel, PetXP, PetTrainingPoints)
3. Create `PetProgressionGump.cs` for viewing/spending TP
4. Hook TP spending to stat changes

### Phase C — Traits
1. Define trait enum and effect application in `PetProgressionSystem`
2. Add trait slot serialization to `BaseCreature`
3. Wire trait bonuses into combat stats at load/apply time

### Phase D — Breeding
1. Create `ShrunkPet.cs` (exists — needs breeding fields)
2. Create `PetBreedingSystem.cs` with stat averaging, ability inheritance, generation math
3. Create `OutridersBreedingGump.cs` with submission/collection UI
4. Update `ClusterFAccountData` to v14 with active breeding tracking
5. Create offspring spawning logic (correct type, inherited stats, wild body/hue handling)

---

## Files (when complete)

| File | Status | Purpose |
| --- | --- | --- |
| `PetGenderSystem.cs` | PENDING | Gender assignment, hover display, WorldLoad sweep |
| `PetProgressionSystem.cs` | PENDING | XP, leveling, training points, EventSink hook |
| `PetProgressionGump.cs` | PENDING | View level/XP/TP, spend TP on stats/traits |
| `PetBreedingSystem.cs` | PENDING | Stat averaging, ability inheritance, generation math |
| `OutridersBreedingGump.cs` | PENDING (skeleton exists) | Submit/collect breeding pairs |
| `ShrunkPet.cs` | EXISTS | Shrunken pet item — needs breeding fields |
| `BaseCreature.cs` (patch) | PENDING | 2 hook calls: OnAfterSpawn + GetProperties |
| `ClusterFAccountData.cs` | v13 live | v14 needed for active breeding tracking |
