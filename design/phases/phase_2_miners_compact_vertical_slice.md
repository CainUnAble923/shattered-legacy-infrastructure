# Phase 2 — Miners' Compact Vertical Slice

## Status

**Complete** — implemented 2026-05-13.

## Objective

Implement the first end-to-end playable loop for the Miners' Compact guild: extended ore infrastructure, member dashboard, work orders, Mining Vouchers, and the full Jacob's Pickaxe lifecycle (acquisition → restoration → tier 2 upgrade).

Phase 2 scope is strictly the Miners' Compact vertical slice. Deferred to Phase 3+: Prospector's Logbook, Tier 3/4 pickaxes, survey systems, Cartography integration, full Guild Work Orders, rations/tonics, Clockwork Scavenger, corpse claim, dungeon chest rewrites, food/drink buffs, Gargoyle Pickaxe redesign.

---

## Delivered Systems

### 2A — Extended Resource Foundation

**Files changed:**
- `patches/ResourceInfo.cs` — full patched replacement
- `customizations/ClusterFExtendedOres.cs` — new

**CraftResource enum additions (values 10–17):**

| Value | Name | Hue | Focus |
| --- | --- | --- | --- |
| 10 | Platinum | 0x0481 (silver) | Physical/Cold, +75 durability |
| 11 | Toxic | 0x04F6 (acid green) | Poison focus, +50 poison dmg |
| 12 | Blaze | 0x0026 (fire red) | Fire focus, +50 fire dmg |
| 13 | Frost | 0x076D (ice blue) | Cold focus, -2 fire resist |
| 14 | Obsidian | 0x0455 (near-black) | Physical, +100 durability |
| 15 | Mythril | 0x022C (purple) | Energy/magic + luck |
| 16 | Adamantium | 0x0413 (dark charcoal) | Physical, +150 durability |
| 17 | Celestial | 0x08AF (radiant gold) | All resists + luck |

All require **GM Mining (100.0 skill)** to mine. Vein weights: Platinum=8 through Celestial=1 (total 36 added to base 1000 pool = ~3.5% combined chance).

**GetType() range extended:** `>= Iron and <= Celestial` → Metal type. All downstream code (hue lookup, attribute lookup, crafting) works without modification.

**Blacksmithy extended via ClusterFMiningExtension.cs** using `EventSink.WorldLoad` hook — no vanilla file patching required for DefBlacksmithy. All 8 extended ingots added as sub-resources at skill 99.0 with string TextDefinition names.

---

### 2B — Miners' Compact Functional Loop

**File changed:** `customizations/MinersCompactLiaisonGump.cs`

New views added to existing gump:
- **MemberDashboard** — shows Compact Standing, Rank, Mining Vouchers; links to Work Orders / Restoration / Upgrade
- **WorkOrders** — current work order display + submit button (with cooldown feedback)
- **Restoration** — Jacob's Pickaxe restoration flow with requirement display
- **Upgrade** — opens `JacobsUpgradeGump` for Tier 2

MainMenu updated: shows "Member Dashboard" button when player is a Compact member; shows "Join" button otherwise.

---

### 2C — Mining Vouchers

Mining Vouchers = `GuildCurrency["mining"]` — already tracked in `ClusterFAccountData`.

Earn: 5 vouchers per completed work order.
Spend: 10 vouchers for Jacob's Pickaxe restoration; 50 vouchers for Tier 2 upgrade.

---

### 2D — First Work Order

**Integrated into MinersCompactLiaisonGump.cs** (WorkOrders view + HandleWorkOrderSubmit).

- **Requirement:** Compact member
- **Deliver:** 50 Iron Ingots (consumed from pack)
- **Award:** +100 Compact Standing, +5 Mining Vouchers
- **Cooldown:** 24 hours (tracked via `mining.workorder.last_completed` flag value)

---

### 2E — Jacob's Pickaxe Restoration

**Integrated into MinersCompactLiaisonGump.cs** (Restoration view + HandleRestorationRequest).

- **Requirements:**
  - Compact member
  - `legacy.jacobs_pickaxe` registry entry unlocked (auto-unlocked on first acquisition)
  - No active copy already registered (HasActiveCopy = false)
  - 10 Mining Vouchers
  - 250 Iron Ingots in pack
  - 5,000 gold in pack
- **Action:** consumes costs, calls `TryRestore()`, delivers fresh `JacobsPickaxe` to pack

---

### 2F — Jacob's Pickaxe Tier 1 Normalization

**File:** `patches/JacobsPickaxe.cs` (full patched replacement)

Changes from original:
- Mining SkillBonus: +5 (was +10)
- UsesRemaining: 150 (was 20)
- New: `OnAdded` auto-unlocks `legacy.jacobs_pickaxe` in restoration registry
- New: `OnDoubleClick` blocks use when `Exhausted = true`
- New: `GetProperties` shows "(Exhausted)" when exhausted

Serialization bumped v0 → v1 (`_exhausted` bool added). Migration file: `Migrations/Server.Items.JacobsPickaxe.v0.json`.

---

### 2G — Jacob's Reinforced Pickaxe (Tier 2)

**File:** `customizations/JacobsReinforcedPickaxe.cs` (new)

Stats:
- Mining SkillBonus: +10
- UsesRemaining: 400
- Blessed
- Hue: 0x0481 (silver tint)

**Upgrade gump** (`JacobsUpgradeGump`) opened from Member Dashboard:

Requirements:
- Compact member
- Apprentice rank (1,000 Standing)
- Mining skill 75.0+
- 50 Mining Vouchers
- 1,000 Iron Ingots
- 250 Dull Copper Ingots
- 25,000 gold
- Non-exhausted Jacob's Pickaxe in pack (consumed)

Action: consumes all costs, creates `JacobsReinforcedPickaxe` in pack.

---

### 2H — Durability MVP

**Implemented in:** `patches/JacobsPickaxe.cs`

When UsesRemaining hits 0 and HarvestSystem calls `tool.Delete()`:
1. `OnDelete()` fires on JacobsPickaxe
2. Detects item is in a container (player's pack)
3. Calls `ClearActiveCopy()` on the registry so the player can restore
4. Creates an exhausted replacement (Exhausted=true, Hue=0x0415) in the pack
5. Sends player a message: "Jacob's Pickaxe has worn out. Find the Miners' Compact Liaison."

The exhausted pickaxe cannot be used for mining (OnDoubleClick blocks it). It can be brought to the Liaison for restoration.

---

### Admin / Testing Tools

**File:** `customizations/ClusterFCompactAdminTools.cs` (new)

| Command | Function |
| --- | --- |
| `[CompactStanding [amount]` | Inspect or set mining standing |
| `[CompactVouchers [amount]` | Inspect or set mining vouchers |
| `[CompactRank` | Show rank for targeted player |
| `[CompactUnlockPickaxe` | Grant legacy.jacobs_pickaxe registry unlock |
| `[CompactGivePickaxe` | Give fresh Jacob's Pickaxe |
| `[CompactGiveReinforcedPickaxe` | Give Jacob's Reinforced Pickaxe |
| `[CompactExhaust` | Exhaust the Jacob's Pickaxe in player's pack (for testing) |
| `[CompactOreInfo` | Print extended ore resource table to server console |

---

## Ranks and Thresholds

| Rank | Standing Required |
| --- | --- |
| Initiate | 0 |
| Apprentice | 1,000 |
| Journeyman | 5,000 |
| Surveyor | 15,000 |
| Master Delver | 40,000 |
| Deepwarden | 80,000 |
| Legendary Prospector | 150,000 |

---

## Files Changed

| File | Change |
| --- | --- |
| `patches/ResourceInfo.cs` | Extended CraftResource enum (Platinum–Celestial); 8 CraftAttributeInfo; 8 m_MetalInfo entries; GetType() range extended |
| `customizations/ClusterFExtendedOres.cs` | New — 8 ore + 8 ingot classes |
| `customizations/ClusterFMiningExtension.cs` | New — WorldLoad hook to extend Mining veins and DefBlacksmithy sub-resources |
| `customizations/MinersCompactLiaisonGump.cs` | Added MemberDashboard, WorkOrders, Restoration views; work order + restoration logic |
| `patches/JacobsPickaxe.cs` | Phase 2F (+5 Mining, 150 uses, OnAdded registry, OnDoubleClick exhausted block) + Phase 2H (OnDelete exhausted replacement) |
| `Migrations/Server.Items.JacobsPickaxe.v0.json` | Migration for v0→v1 (empty properties) |
| `customizations/JacobsReinforcedPickaxe.cs` | New — Tier 2 pickaxe + upgrade gump |
| `customizations/ClusterFCompactAdminTools.cs` | New — admin commands for Compact testing |

---

## What Is NOT in Phase 2

Deferred to Phase 3+:

- Extended ore smelting recipes (DefBlacksmithy craft items for extended ingots)
- Extended ore elementals
- Prospector's Logbook
- Jacob's Pickaxe Tier 3/4
- Survey system
- Miners' Guild Contracts (multi-step quests beyond work orders)
- Full Guild Work Orders framework (other guilds)
- Mining Voucher vendor/shop
- Cartography integration
- Rations and tonics
- Food/drink buffs
- Clockwork Scavenger
- Gargoyle Pickaxe redesign

---

## Fix Pass — 2026-05-14

A targeted fix and polish pass was applied on 2026-05-14 following the first full testing session. All items below are committed to `main`.

### FP-1 — Crafting menu silent failure (root cause)

**Symptom:** Extended ore types and Blacksmithy sub-resources never appeared in the crafting menu. No server errors; startup log messages from `ClusterFMiningExtension` were completely absent.

**Root cause:** `ClusterFMiningExtension` used `Initialize()` instead of `Configure()`. ModernUO fires `EventSink.WorldLoad` during `World.Load()` which runs between `Configure()` and `Initialize()` in startup order. Because the extension's `EventSink.WorldLoad += OnWorldLoad` subscription happened inside `Initialize()`, the event had already fired by the time the handler was registered — it never ran.

A second issue: `DefBlacksmithy.CraftSystem` is `null` at WorldLoad time because `DefBlacksmithy.Initialize()` (which sets the property) also runs in the `Initialize()` phase. Attempting to extend Blacksmithy at WorldLoad time would have thrown a null reference.

**Fix:** Changed `ClusterFMiningExtension` to use `Configure()` and split the two event subscriptions:

```csharp
public static void Configure()
{
    EventSink.WorldLoad     += OnWorldLoad;     // Mining veins — safe at WorldLoad
    EventSink.ServerStarted += OnServerStarted; // Blacksmithy — needs DefBlacksmithy.Initialize() first
}
```

`EventSink.ServerStarted` fires after ALL `Initialize()` calls complete, including `DefBlacksmithy.Initialize()`, making it the correct hook for extending Blacksmithy sub-resources.

**Verified:** Server startup now logs both:
```
[ClusterFMiningExtension] Extended mining veins registered: 8 new ore types (Platinum through Celestial).
[ClusterFMiningExtension] Blacksmithy sub-resources registered: Platinum through Celestial.
```

---

### FP-2 — Pickaxe deletion bug (equipped state)

**Symptom:** When Jacob's Pickaxe was exhausted while equipped on a layer (rather than sitting in the backpack), the `OnDelete` handler didn't fire the exhausted-replacement flow — the pickaxe just silently disappeared.

**Root cause:** `OnDelete` only checked `Parent is Container pack`. When an item is equipped on a layer, `Parent` is `PlayerMobile`, not `Container`.

**Fix:** Added an `else if (Parent is PlayerMobile layerOwner)` fallback that uses `layerOwner.Backpack` for the replacement drop and `layerOwner.Account` for the registry clear.

---

### FP-3 — Jacob's Reinforced Pickaxe name and hue

**Symptoms:**
- T2 displayed as "Jacob's Pickaxe" instead of "Jacob's Reinforced Pickaxe".
- Hue was `0x0481` (silver/Platinum) — visually indistinguishable from standard metals.
- No durability MVP: when T2 exhausted, it simply deleted (no replacement, no registry clear, no player message).

**Fixes:**
- `Name = "Jacob's Reinforced Pickaxe"` explicitly set in constructor.
- `FunctionalHue = 0x8A5C` (Dull Copper), `ExhaustedHue = 0x0415` (dark worn).
- Full `OnDelete` durability MVP added (same equipped-state logic as T1 fix above).
- `Exhausted` property backed by `Hue == ExhaustedHue`.
- Upgrade gump now sets `srcPickaxe.Exhausted = true; srcPickaxe.Delete()` before creating T2, preventing the deletion from triggering a spurious T1 replacement.

---

### FP-4 — Work order flow redesign

**Problem:** The original single-step "submit" flow had a 24-hour cooldown tracked via `mining.workorder.last_completed`. During testing this was confusing (no UI feedback that a cooldown was active until after clicking submit) and the cooldown served no meaningful progression purpose at current scale.

**Redesign:** Two-step Accept → Turn In flow:
1. **Accept**: player clicks "Accept Work Order" — sets `mining.workorder.active` flag. Dashboard shows current work order status.
2. **Turn In**: player returns with 50 Iron Ingots → consumes ingots, clears flag, awards +100 Compact Standing + 5 Mining Vouchers.

Cooldown removed entirely. Players can chain work orders immediately after completion.

Flag constant: `private const string WorkOrderActiveFlag = "mining.workorder.active"`.

---

### FP-5 — Talk context menu on both Liaison NPCs

**Problem:** Right-clicking the Miners' Compact Liaison or League Registrar showed no "Talk" option. Players had to double-click, which was less intuitive.

**Fix:** Added `GetContextMenuEntries` override to both `MinersCompactLiaison` and `LeagueRegistrar` with a `TalkEntry : ContextMenuEntry` inner class (button ID `6146`).

**API used:**
```csharp
public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
{
    base.GetContextMenuEntries(from, ref list);
    if (from is PlayerMobile pm && pm.InRange(Location, 4))
        list.Add(new TalkEntry(pm, this));
}
```
Requires `using Server.Collections;`. `OnClick` override signature is `OnClick(Mobile from, IEntity target)`.

---

### FP-6 — League Registrar ambient speech and proximity greeting

**Addition:** League Registrar now speaks ambient lines every 25–45 seconds (only when at least one player is within 10 tiles) and delivers a personal greeting the first time each player enters range per session.

- **Ambient lines:** 6 cycling lines drawing players' attention to the League, guilds, and New Haven.
- **Proximity greeting:** fires 2 seconds after a new player enters range 6 (delay avoids overlap with ambient line). Tracked per-session via `HashSet<Serial> _greeted`.
- **Timer pattern:** `ScheduleAmbientSpeech()` reschedules itself inside a try/finally block so the timer always restarts even if an exception occurs in the tick body.
- Started in `OnAfterSpawn()`.

**Note on ModernUO API:** `GetMobilesInRange` returns `Map.MobileBoundsEnumerable<Mobile>` (struct-based, non-pooled). No `.Free()` call needed — just `foreach` directly.

---

### FP-7 — Quest arrow from League Registrar to Miners' Compact Liaison

**Addition:** "Find the Miners' Compact Liaison" button (button ID 20) in the Guild Referrals view of `LeagueRegistrarGump` now sets a quest arrow pointing to the Liaison at Trammel `(3510, 2748)`.

New class: `MinersCompactDirectionArrow : QuestArrow`
- Constructor: `base(m, null, LiaisonX, LiaisonY)` — pass `null` for the mobile arg when pointing to a location.
- Right-click dismisses it.
- Auto-stops after 10 minutes via `Timer.DelayCall`.

---

### FP-8 — Restoration false-positive safety auto-clear

**Problem:** If the registry had `HasActiveCopy = true` but the player's pack contained no live (non-exhausted) Jacob's Pickaxe — due to a stuck state from a previous bug — the Restoration gump showed "you already have an active copy" and blocked restoration indefinitely.

**Fix:** Proactive safety check added at the top of `DrawRestoration`:
```csharp
if (ClusterFRestorationRegistry.HasActiveCopy(acct, "legacy.jacobs_pickaxe"))
{
    bool hasLiveCopy = false;
    // scan pack for non-exhausted JacobsPickaxe
    if (!hasLiveCopy)
        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_pickaxe");
}
```
If no live copy is found, the stale flag is silently cleared before the view renders.

---

### FP-9 — New admin commands

Three new commands added to `ClusterFCompactAdminTools.cs`:

| Command | Function |
| --- | --- |
| `[CompactClearActive` | Clears active-copy flags for both `legacy.jacobs_pickaxe` and `legacy.jacobs_reinforced_pickaxe` on targeted player, allowing restoration without a full reset |
| `[TestingReset` | Full wipe of guild membership, standing, currency, flags, flag values, both pickaxe registry entries (revoke + clear active), and all pickaxes in pack |
| `[TestingZeroSkills` | Sets all skills to 0.0 on targeted player for clean testing starts |

All require GameMaster+ access level.

---

### Files changed in fix pass

| File | Change |
| --- | --- |
| `customizations/ClusterFMiningExtension.cs` | `Configure()` + split WorldLoad/ServerStarted hooks |
| `patches/JacobsPickaxe.cs` | `OnDelete` equipped-state fix |
| `customizations/JacobsReinforcedPickaxe.cs` | Full rewrite: correct name, Dull Copper hue, full durability MVP |
| `customizations/MinersCompactLiaison.cs` | Talk context menu added |
| `customizations/LeagueRegistrar.cs` | Ambient speech, proximity greeting, Talk context menu |
| `customizations/LeagueRegistrarGump.cs` | Quest arrow button wired; `MinersCompactDirectionArrow` class added |
| `customizations/MinersCompactLiaisonGump.cs` | Work order Accept→Turn In flow; restoration safety auto-clear |
| `customizations/ClusterFCompactAdminTools.cs` | `[CompactClearActive`, `[TestingReset`, `[TestingZeroSkills` added |

---

---

## Fix Pass 2 — 2026-05-14

A second targeted fix and polish pass was applied on 2026-05-14 following a deeper testing session. All items committed to `main` (commit `44b61f0`).

### FP2-1 — NPC names

**Problem:** Both Liaison NPCs used generic role names (`"Miners' Compact Liaison"`, `"League Registrar"`) with awkward titles (`"of the Miners' Compact"`, `"of the League"`). Clicking the NPC displayed the name as the title, producing things like "Miners' Compact Liaison of the Miners' Compact" — redundant and immersion-breaking.

**Fix:**
- `MinersCompactLiaison.cs`: `Name = "Garrett Ashveil"`, `Title = "the Miners' Compact Liaison"`
- `LeagueRegistrar.cs`: `Name = "Elara Voss"`, `Title = "the League Registrar"`
- Proximity greeting updated: now says "I am Elara Voss — the League Registrar."
- Seeder unchanged: `ClusterFInstitutionSeeder` deduplicates by type (`typeof(MinersCompactLiaison)`), not by name string. Run `[ClusterFSeedInstitutions` to respawn NPCs with their new names.

---

### FP2-2 — Achievements Quests tab overflow

**Symptom:** The "Quests" tab button appeared off the right edge of the `AchievementsGump` (GumpWidth = 580). With 8 category tabs starting at x=14 and accumulating widths of 46–78px each, `tabX` reached ~550 before the Quests tab was placed — putting it at x≈550, overflowing the gump.

**Fix:** Split tab bar into two rows.
- Row 1 (tabY=60): all 8 category tabs as before.
- Row 2 (tabY=82): "Quests" tab alone at x=14.
- Separator line moved to `tabY2 + 22 = 104`.
- Content `listY` adjusted to `tabY2 + 28 = 110` (was hardcoded 90); `listH` recalculated accordingly (~20px reduction in content area, acceptable).

**File:** `customizations/ClusterFAchievements.cs`

---

### FP2-3 — Exhausted pickaxe shows UsesRemaining in tooltip

**Symptom:** When a pickaxe exhausted (uses ran out), `OnDelete` created a replacement with `Exhausted = true` but `UsesRemaining = 150` (default constructor value). The item tooltip showed both `"(Exhausted)"` and `"uses remaining: 150"` — contradictory and confusing.

**Root cause:** `ShowUsesRemaining` is defined on `IUsesRemaining` (in `BaseHarvestTool.cs`, `namespace Server.Items`) and is implemented by `Pickaxe` via explicit interface. It is NOT a virtual member — `BaseAxe.ShowUsesRemaining` (the generated serialized member from `Pickaxe`'s `[SerializationGenerator]` partial class) is non-virtual, so `override` fails at compile time.

**Fix:** In the `Exhausted` property setter on both T1 and T2, set the interface property directly via cast:
```csharp
((IUsesRemaining)this).ShowUsesRemaining = !value;
```
This value is serialized as part of `IUsesRemaining` state and survives server restarts. Fresh items keep `ShowUsesRemaining = true` (set by `Pickaxe` constructor); exhausted replacements have `ShowUsesRemaining = false`.

**Files:** `patches/JacobsPickaxe.cs`, `customizations/JacobsReinforcedPickaxe.cs`

---

### FP2-4 — Exhausted pickaxes could be equipped

**Problem:** Players could equip an exhausted pickaxe via the paperdoll. This was undesirable (no use while exhausted) and complicated the restoration safety scan (which needed to check the equipped layer as well as the backpack).

**Fix:** `OnEquip` override added to both `JacobsPickaxe` and `JacobsReinforcedPickaxe`. Returns `false` with a descriptive chat message when `Exhausted == true`. This is now the **standard pattern for all future tier tools**.

**Files:** `patches/JacobsPickaxe.cs`, `customizations/JacobsReinforcedPickaxe.cs`

---

### FP2-5 — T2 upgrade scan missed equipped T1 pickaxe

**Problem:** `JacobsUpgradeGump.FindPickaxeInPack()` only scanned `_pm.Backpack.Items`. If the player had their T1 Jacob's Pickaxe equipped in the two-hand slot during the upgrade, it was invisible to the requirement check — the gump showed "Jacob's Pickaxe (non-exhausted) in pack: No" even though the pickaxe was present.

**Fix:** Check `_pm.FindItemOnLayer(Layer.TwoHanded)` first, then fall through to the backpack scan.

**Note:** With the `OnEquip` block now in place (FP2-4), exhausted pickaxes cannot be equipped. The equipped-layer check therefore only needs to find non-exhausted T1 pickaxes, which is what the fix does.

**File:** `customizations/JacobsReinforcedPickaxe.cs`

---

### FP2-6 — T2 restoration path missing

**Problem:** The Restoration view in `MinersCompactLiaisonGump` only handled T1. A player with `legacy.jacobs_reinforced_pickaxe` unlocked (had owned and exhausted a T2) had no way to restore it — the view was silent about T2 entirely.

**Fix:** `DrawRestoration` rewritten to handle both T1 and T2 independently:
- T2 section appears only when `legacy.jacobs_reinforced_pickaxe` is unlocked.
- T2 stale-active-copy auto-clear: scans backpack for live (non-exhausted) T2; clears registry if not found (mirrors existing T1 logic).
- T2 restoration costs (lighter than original upgrade):

| Resource | Original upgrade cost | T2 restoration cost |
| --- | --- | --- |
| Mining Vouchers | 50 | 25 |
| Iron Ingots | 1,000 | 500 |
| Dull Copper Ingots | 250 | 100 |
| Gold | 25,000 | 12,500 |
| Source T1 pickaxe consumed | Yes | No |

- Button 61 wired in `OnResponse`; `HandleT2RestorationRequest` method added.
- T2 button placed at `H - 100`; T1 button remains at `H - 68`. Both only appear when eligible.

**File:** `customizations/MinersCompactLiaisonGump.cs`

---

### FP2-7 — Missing back buttons and incorrect back navigation

**Problems:**
1. `JacobsUpgradeGump` had no Back button — closing and re-opening the Liaison was the only way to return to the Member Dashboard.
2. `MinersCompactLiaisonGump` routed all Back presses to `View.MainMenu`. For WorkOrders and Restoration (sub-views of MemberDashboard), this skipped the dashboard — players had to navigate back through the full menu tree.

**Fixes:**
- `JacobsUpgradeGump`: Added Back button (button ID 1) → `MinersCompactLiaisonGump(View.MemberDashboard)`.
- `MinersCompactLiaisonGump.OnResponse` Back handler: if current view is `WorkOrders` or `Restoration`, route to `MemberDashboard`; otherwise route to `MainMenu`.

**File:** `customizations/JacobsReinforcedPickaxe.cs`, `customizations/MinersCompactLiaisonGump.cs`

---

### FP2-8 — Remaining green-on-black and 1154 label colors

**Problems:** Several action labels still used the wrong color after Fix Pass 1:
- WorkOrders ingot-count progress color: `"#44FF44"` (bright green on dark background, hard to read)
- WorkOrders Accept and Turn-In button labels: hue `1154` (renders teal/cyan, not gold)
- Restoration restore button label: hue `1154`

**Fix:** All three corrected to `"#FFD700"` / `999` respectively, matching the established convention from Fix Pass 1.

**File:** `customizations/MinersCompactLiaisonGump.cs`

---

### Files changed in fix pass 2

| File | Change |
| --- | --- |
| `customizations/MinersCompactLiaison.cs` | Name → "Garrett Ashveil", Title → "the Miners' Compact Liaison" |
| `customizations/LeagueRegistrar.cs` | Name → "Elara Voss", Title → "the League Registrar"; proximity greeting updated |
| `customizations/ClusterFAchievements.cs` | Two-row tab bar; `listY` adjusted to accommodate second row |
| `patches/JacobsPickaxe.cs` | `Exhausted` setter: `((IUsesRemaining)this).ShowUsesRemaining = !value`; `OnEquip` exhausted block |
| `customizations/JacobsReinforcedPickaxe.cs` | Same `Exhausted`/`OnEquip` fixes; `FindPickaxeInPack` equipped-layer scan; Back button on `JacobsUpgradeGump` |
| `customizations/MinersCompactLiaisonGump.cs` | T2 restoration path; smart Back routing; `#44FF44` → `#FFD700`; action labels `1154` → `999` |

---

## Fix Pass 3 — 2026-05-14

### FP3-1 — T2 restoration delivers T1 / T2 button missing when DC ingots absent

**Root cause (three bugs acting together):**

1. `JacobsPickaxe.OnAdded` called `Unlock` but never set `HasActiveCopy = true`. A fresh T1 entering the player's pack had `HasActiveCopy = false`, so the T1 restore button always appeared — even while the T1 was actively in the player's inventory.

2. `JacobsReinforcedPickaxe.OnAdded` had the same flaw for T2. T2's active-copy state was never set on acquisition, only cleared on exhaustion — meaning the registry never correctly reflected "player currently has a live T2."

3. In `DrawRestoration`, the T2 restore button (61) was only rendered when `t2CanRestore = true`, which required all four materials (vouchers + iron + DC + gold) to be present. If the player lacked Dull Copper ingots, the button disappeared — but the T2 status text still showed "Ready for restoration." The only visible button was T1. Players clicked it expecting T2 and received T1 instead.

**Fix applied:**

`JacobsPickaxe.OnAdded` — after `Unlock`, set `HasActiveCopy = true` when the item is **not exhausted**. Exhausted replacement drops skip this (ClearActiveCopy was already called in `OnDelete`).

`JacobsReinforcedPickaxe.OnAdded` — same pattern for the T2 registry key. The T1 safety-net `Unlock` call is kept but does **not** set T1's `HasActiveCopy` (acquiring T2 ≠ having a T1).

`MinersCompactLiaisonGump.DrawRestoration` — decoupled display gate from material availability:
- Both restore buttons now render whenever `t2Unlocked && !t2Active` (or T1 equivalent). Materials are always validated server-side in the handler.
- Button label color signals readiness: gold (`999`) when all materials present, red (`0x22`) when short. Player sees a clearly labeled T2 button even if missing DC ingots, gets a clear error on click.
- False-positive auto-clear checks for both T1 and T2 now include the equipped layer scan (same fix as FP2-5).

**Files changed in fix pass 3:**

| File | Change |
| --- | --- |
| `patches/JacobsPickaxe.cs` | `OnAdded`: set `HasActiveCopy = true` for non-exhausted items |
| `customizations/JacobsReinforcedPickaxe.cs` | `OnAdded`: set T2 `HasActiveCopy = true` for non-exhausted items; T1 unlock kept as safety net but unchanged |
| `customizations/MinersCompactLiaisonGump.cs` | `DrawRestoration`: button display decoupled from materials; label coloring for readiness feedback; equipped-layer scan in both false-positive checks |

---

## Next Phase Handoff

Phase 3 target: expand work orders to multi-step contracts, add Mining Voucher redemption vendor, add extended ore crafting (DefBlacksmithy recipes for Platinum through Celestial gear), and begin Prospector's Logbook foundation.
