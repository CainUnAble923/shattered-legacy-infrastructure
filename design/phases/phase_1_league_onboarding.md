# Phase 1 — League Onboarding Foundation

## Status

**Complete** — implemented 2026-05-13.

## Objective

Establish the onboarding foundation for Shattered Legacy: the League of Extraordinary Citizens entry flow, citizen status tier system, League-aligned achievements, account flags, and the first profession guild liaison (Miners' Compact).

This phase does **not** include extended ore implementation, Jacob's Pickaxe upgrades, Prospector's Logbook systems, Guild Work Orders, or full Miners' Compact contracts. Those belong to Phase 2+.

---

## Delivered Systems

### 1. Account Flags (`ClusterFAccountData` v3)

Added two new collections to per-account data:

| Field | Type | Purpose |
| --- | --- | --- |
| `Flags` | `HashSet<string>` | Boolean account-wide flags (e.g. `league.joined`) |
| `FlagValues` | `Dictionary<string, string>` | String-valued flags (e.g. timestamps, guild keys) |

Helpers: `HasFlag`, `SetFlag`, `ClearFlag`, `GetFlagValue`, `SetFlagValue`.

Serialization bumped **v2 → v3**. Saves from v0–v2 migrate cleanly (flags and flag values start empty).

---

### 2. League System (`ClusterFLeagueSystem`)

Static class owning all League-of-Extraordinary-Citizens state logic.

#### Flag Constants

| Constant | Key | Meaning |
| --- | --- | --- |
| `FlagJoined` | `league.joined` | Player has registered with the League |
| `FlagIntroCompleted` | `league.intro_completed` | Intro flow complete (reserved Phase 2) |
| `FlagFirstDispatch` | `league.first_dispatch_seen` | Player read the League Dispatch |
| `FlagFirstReferral` | `league.first_guild_referral_seen` | Player received any guild referral |
| `FlagReferredMiners` | `league.referred_to_miners` | Player received Miners' Compact referral |
| `FlagValRegisteredAt` | `league.registered_at` | ISO-8601 UTC join timestamp |
| `FlagValFirstReferralGuild` | `league.first_referral_guild` | Key of first guild referred to |

#### Citizen Status

| Status | Condition |
| --- | --- |
| Unregistered | `league.joined` not set |
| Registered Citizen | Joined League, no guilds yet |
| Recognized Citizen | Joined League + member of at least one guild |

#### Public API

```
JoinLeague(pm)                    -- registers player, sets flag, grants achievement
OnDispatchRead(pm)                -- marks bulletins seen, grants achievement once
OnGuildReferralSeen(pm, guildKey) -- grants referral achievement once, sets miners flag
OnGuildJoined(pm)                 -- grants guildbound achievement on first guild join
GetStatus(data)                   -- returns CitizenStatus enum value
GetStatusLabel(status)            -- human-readable status string
GetStatusColor(status)            -- HTML hex colour for status (no #)
```

---

### 3. League Achievements (4 new)

Added `AchievementCategory.League = 8`.

| Key | Title | AP | Renown | Trigger |
| --- | --- | --- | --- | --- |
| `league.registered_citizen` | Registered Citizen | 10 | 25 | `JoinLeague()` |
| `league.first_dispatch` | First Dispatch | 5 | 10 | `OnDispatchRead()` |
| `league.first_referral` | First Referral | 5 | 10 | `OnGuildReferralSeen()` |
| `league.guildbound` | Guildbound | 15 | 50 | `OnGuildJoined()` |

League achievements appear under the **All** tab in `[achievements`. A dedicated League tab can be added in a future session when the gump tab row has room.

---

### 4. League Registrar Gump (`LeagueRegistrarGump`)

Multi-view gump opened by double-clicking the League Registrar NPC at Trammel `3459, 2601`.

| Button | View / Action |
| --- | --- |
| Join the League | Calls `JoinLeague(pm)`; hidden after join |
| Citizen Status | Explains three tiers; shows player's current status |
| About Renown | Explains Renown and AP; shows current balance |
| Achievements | Opens `AchievementsGump` |
| Guilds Overview | Opens `GuildProgressGump` |
| Guild Referrals | Triggers `OnGuildReferralSeen(pm, "mining")`; lists liaisons |
| League Dispatch | Calls `OnDispatchRead(pm)`; opens `BulletinGump` |
| Where to go first? | Step-by-step guidance for new arrivals |

---

### 5. Miners' Compact Liaison NPC + Gump

**NPC:** `MinersCompactLiaison` — male humanoid, earthy attire, invulnerable, passive AI.
**Location:** Trammel `3510, 2748, Z=0`, facing South.
**Seeded by:** `ClusterFInstitutionSeeder`.

**Gump views:**

| Button | Content |
| --- | --- |
| About the Compact | Guild history and purpose |
| What We Mine | Ore types and resource overview |
| Joining Requirements | Reads from `GuildDef.TaskDescription` for "mining" |
| Rewards and Scrip | Reads join rep and scrip from `GuildDef` |
| Join the Miners' Compact | Opens `GuildTaskDetailGump` for mining guild def |

Opening the gump always calls `OnGuildReferralSeen(pm, "mining")` (idempotent).

---

### 6. Institution Seeder (`ClusterFInstitutionSeeder`)

Separate from `ClusterFNewHavenSeeder`. Seeds League institution NPCs on world load.

- Duplicate-safe: scans `World.Mobiles` for existing instance before spawning.
- Admin command: `[ClusterFSeedInstitutions` — seeds missing NPCs with console output.
- Sets `CantWalk = true`, `RangeHome = 0` on all seeded NPCs.

---

## Completion Checklist

- [x] League Registrar has player-facing gump/dialogue
- [x] Player can join the League from the Registrar
- [x] Player can view citizen status
- [x] Registrar explains Renown and achievement points
- [x] Registrar explains profession guilds
- [x] Registrar opens League Dispatch
- [x] Registrar refers player to the Miners' Compact representative
- [x] Referral sets account flags
- [x] Account flags track intro/referral completion
- [x] Onboarding achievements trigger correctly
- [x] Miners' Compact representative placed near south New Haven mine camp
- [x] Miners' Compact representative has intro dialogue and join flow
- [x] Miners' Compact representative is duplicate-safe seeded
- [x] README documents implemented NPCs, commands, coordinates, and settings
- [x] This Phase 1 design doc reflects the implemented flow

---

## What Is NOT in Phase 1

The following are explicitly deferred:

- Extended ore types (Compact Ore, etc.)
- Jacob's Pickaxe legacy item and upgrade system
- Prospector's Logbook
- Guild Work Orders
- Full Miners' Compact guild contracts
- League Dispatch authoring UI
- Citizen commissions
- Metal rank ladder (Iron → Valorite) beyond status display

---

## Next Phase Handoff

After Phase 1, the next major implementation target is the Miners' Compact vertical slice:

```
Extended ore/resource framework
→ Miners' Compact full guild membership
→ Mining Vouchers
→ First ore work order
→ Jacob's Pickaxe restoration
→ Tier 2 Jacob's Pickaxe upgrade
```

---

## Files Changed

| File | Change |
| --- | --- |
| `ClusterFAccountData.cs` | v2 → v3; added `Flags`, `FlagValues`, helpers |
| `ClusterFLeagueSystem.cs` | New — all League flag logic and citizen status |
| `ClusterFAchievements.cs` | Added `League = 8` category; 4 new achievements |
| `ClusterFGuildSystem.cs` | `Join()` now calls `ClusterFLeagueSystem.OnGuildJoined()` |
| `LeagueRegistrar.cs` | Added `OnDoubleClick` opening `LeagueRegistrarGump` |
| `LeagueRegistrarGump.cs` | New — 5-view dialogue gump for the League Registrar |
| `MinersCompactLiaison.cs` | New — Miners' Compact Liaison NPC |
| `MinersCompactLiaisonGump.cs` | New — 5-view gump with join flow |
| `ClusterFInstitutionSeeder.cs` | New — duplicate-safe world-load seeder for institution NPCs |

---

## NPC Locations Summary

| NPC | Map | X | Y | Z | Direction |
| --- | --- | --- | --- | --- | --- |
| League Registrar | Trammel | 3459 | 2601 | 18 | North |
| Miners' Compact Liaison | Trammel | 3510 | 2748 | 0 | South |

---

## Original Design Specification

The following was the original Phase 1 design spec used to drive this implementation.

### Purpose

Phase 1 completes the first usable Shattered Legacy onboarding loop.

The goal is not to complete every guild, achievement, relic, or work order system. The goal is to make the League of Extraordinary Citizens functional enough that a new player understands the shard's custom progression and is directed toward the first profession path.

### Phase 1 Player Experience Target

A new player should be able to:

```
Log in
→ see or access the League Dispatch
→ find the League Registrar in New Haven
→ join or learn about the League
→ understand Renown, achievements, guilds, and restoration basics
→ receive a first guild referral
→ be directed to the Miners' Compact representative at the south New Haven mine camp
```

### League Data Flags

```
league.joined
league.intro_completed
league.first_dispatch_seen
league.first_guild_referral_seen
league.referred_to_miners

league.registered_at       (value: ISO timestamp)
league.first_referral_guild (value: "mining")
```

### Suggested Onboarding Achievements

| Achievement | Trigger |
| --- | --- |
| Registered Citizen | Join the League |
| First Dispatch | Read or acknowledge a League Dispatch |
| First Referral | Receive a guild referral from the League |
| Guildbound | Join any profession guild |

### Explicit Non-Goals For Phase 1

- Full Miners' Compact contract system
- Jacob's Pickaxe upgrade implementation
- Extended ore harvesting implementation
- Prospector's Logbook implementation
- Full Guild Work Orders implementation
- Full Renown vendor/reward shop
- Citizen's Expedition Pack
- Cartography or ClassicUO map integration
