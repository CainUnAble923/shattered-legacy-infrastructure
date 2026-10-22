# Shattered Legacy — Command Reference

All custom in-game commands for the Shattered Legacy shard. Commands are entered in the UO client chat bar.

Access levels:
- **Player** — any logged-in player
- **GM** — `AccessLevel.GameMaster` (staff / test accounts)
- **Admin** — `AccessLevel.Administrator` (owner account)

---

## Player Commands

| Command | Description |
| --- | --- |
| `[achievements` | Opens your achievement records gump (AP, Renown, earned/locked list, New Haven quest tracker) |
| `[guild` | Opens the Guild Progress gump — overview of all 12 guilds with membership status |
| `[stats` | Opens the stat inspector gump — full breakdown of stats, skill modifiers, and item bonuses |
| `[artificerrank` | Says your Artificers' Order rank and the imbue properties your account knows, in `[Artificer]` lines the SL_Combat loot helper reads from the journal (cc-P145) |
| `[SelfRes` | Resurrects yourself from ghost form. Use this instead of waiting for a healer. |

---

## Miners' Compact Admin Tools

All require **GM** access. Target a player after typing the command unless stated otherwise.

| Command | Args | Description |
| --- | --- | --- |
| `[CompactStanding` | `[amount]` | Inspect or set Compact Standing (mining guild reputation) on the targeted player. Omit `amount` to inspect. |
| `[CompactVouchers` | `[amount]` | Inspect or set Mining Vouchers (mining guild currency) on the targeted player. Omit `amount` to inspect. |
| `[CompactRank` | — | Show the Compact rank name for the targeted player's current standing. |
| `[CompactUnlockPickaxe` | — | Grant the `legacy.jacobs_pickaxe` restoration registry unlock to the targeted player. |
| `[CompactGivePickaxe` | — | Give a fresh Jacob's Pickaxe (T1) directly to the targeted player's pack. |
| `[CompactGiveReinforcedPickaxe` | — | Give a fresh Jacob's Reinforced Pickaxe (T2) directly to the targeted player's pack. |
| `[CompactExhaust` | — | Exhaust the Jacob's Pickaxe (T1 or T2) in the targeted player's pack. Used to test the durability and restoration flows. |
| `[CompactClearActive` | — | Clear the `legacy.jacobs_pickaxe` active-copy flag so the player can request a T1 restoration again. |
| `[CompactOreInfo` | — | Print extended ore `CraftResource` enum values and hues to the server console. No target needed. |
| `[CompactGiveTestingToken` | — | Give a Compact Testing Token to the targeted player. While carried, all material/voucher/gold/ingot/skill/standing costs for Compact interactions are bypassed. Registry and membership checks still apply. |

---

## Testing / Reset Tools

All require **GM** access. Target a player after typing the command.

| Command | Args | Description |
| --- | --- | --- |
| `[TestingReset` | — | **Full testing reset** — wipes all guild membership, League flags, Compact standing, vouchers, and restoration registry for the targeted player. Leaves skills and stats untouched. |
| `[TestingZeroSkills` | — | Sets all skills to `0.0` on the targeted player. Use in combination with `[TestingReset` for a completely clean slate. |

---

## Restoration Registry

All require **GM** access. Target a player after typing the command.

| Command | Args | Description |
| --- | --- | --- |
| `[RestorationUnlock` | `<key> [source]` | Grant a restoration registry unlock to the targeted player. Source defaults to `"admin"`. Example: `[RestorationUnlock legacy.jacobs_pickaxe` |
| `[RestorationRevoke` | `<key>` | Revoke an unlock (and its active-copy state) from the targeted player. |
| `[RestorationClear` | `<key>` | Clear the active-copy flag only — keeps the unlock intact. Use when an item is confirmed lost without going through the liaison. Example: `[RestorationClear legacy.jacobs_pickaxe` |
| `[RestorationList` | — | List all restoration registry entries for the targeted player. |

**Registry keys in use:**

| Key | Item |
| --- | --- |
| `legacy.jacobs_pickaxe` | Jacob's Pickaxe (T1) |
| `legacy.jacobs_reinforced_pickaxe` | Jacob's Reinforced Pickaxe (T2) |

---

## Achievement Admin

All require **Admin** access.

| Command | Args | Description |
| --- | --- | --- |
| `[ClusterFAchievement grant` | `<key> [username]` | Grant an achievement to a player (default: self). |
| `[ClusterFAchievement revoke` | `<key> [username]` | Revoke an achievement from a player. |
| `[ClusterFAchievement info` | `<key>` | Show the definition, AP reward, and Renown reward for an achievement key. |
| `[ClusterFAchievement list` | — | List all defined achievement keys. |

---

## League Dispatch / Bulletins

Requires **Admin** access.

| Command | Args | Description |
| --- | --- | --- |
| `[ClusterFBulletin add` | `<text>` | Add a new bulletin entry to the League Dispatch. |
| `[ClusterFBulletin list` | — | List all current bulletins with their IDs. |
| `[ClusterFBulletin remove` | `<id>` | Remove a bulletin by ID. |

Bulletins can also be authored directly in `/etc/uo/modernuo/Configuration/bulletins.txt` and reloaded without a restart.

---

## Dev Tools

Requires **Admin** access.

| Command | Args | Description |
| --- | --- | --- |
| `[ClusterFReset` | `[username]` | Opens the developer reset gump for the account (default: your own). Six independent checkboxes: Compact data, League flags, Guild memberships, Achievement data, Restoration registry, Account flags. |
| `[ClusterFDeleteChar` | — | Target a player character to force-delete, bypassing the normal 7-day wait. |

---

## Configuration Reapplication

Requires **Admin** access. These commands reapply server config to online (or all) players — useful after changing `modernuo.json` settings mid-session.

| Command | Args | Description |
| --- | --- | --- |
| `[ClusterFSkillCaps` | `[all]` | Reapply skill cap config to yourself (no arg) or all online players (`all`). Caps are also applied automatically on login. |
| `[ClusterFStatCaps` | `[all]` | Reapply stat cap config to yourself (no arg) or all online players (`all`). Caps are also applied automatically on login. |
| `[ClusterFSkillGain` | — | Print current skill gain config to the console. Gain multipliers apply automatically at runtime; this is inspect-only. |
| `[ClusterFHealerPolicy` | — | Print/reapply the healer resurrection policy config. |

---

## World Seeders

Requires **Admin** access. These are safe to run multiple times — all seeders are duplicate-safe unless `replace` is specified.

| Command | Args | Description |
| --- | --- | --- |
| `[ClusterFSeedNewHaven` | `[missing\|dryrun\|repair\|replace]` | Seed/repair the 40 named New Haven quest NPCs. `missing` = add only absent NPCs; `dryrun` = report only; `repair` = fix position/direction; `replace` = delete and respawn all. Default (no arg) = seed missing. |
| `[ClusterFSeedOldHaven` | `[missing\|dryrun\|replace]` | Seed Old Haven content. Same mode flags as above. |
| `[ClusterFSeedInstitutions` | — | Seed League institution NPCs (Garrett Ashveil — Miners' Compact Liaison, etc.) if not already present. Run this after a fresh world or after renaming NPCs. |
| `[ClusterFSouthMineDecor` | `[dryrun\|replace]` | Place south mountain mine decor (forge, anvil, deco props). `dryrun` reports without placing; `replace` removes and re-places. |
| `[ClusterFSeedOldHaven` | — | Place Old Haven mage and boss spawn without removing existing. |
| `[ClusterFHalloweenDecor` | `[status\|preview\|place\|clear] [bank\|facet] [dryrun]` | cc-P267: the Halloween decor at the main banks (stock art, 21 bank sets on Trammel, Felucca, Malas, Tokuno and Ter Mur). It goes up by itself on `clusterf.seasonal.halloween.start` (default 10-24) and comes down after `.end` (default 11-02), server-local dates. `status` = date, season, what is placed; `preview` = what `place` would do; `place` = put up the rows not already up (allowed outside the dates, for the test shard; it says so); `clear` = remove everything it placed, by serial, and nothing else. A bank key (`britain`, `luna`, `new-haven`, `britain-fel` ...) or a facet name limits `preview`, `place` and `clear`. |
| `[ClusterFWinterDecor` | `[status\|preview\|place\|clear] [area] [dryrun]` | cc-P34, cc-P266: New Haven's winter decor (109 rows: the bank inside and out, the square, the main roads, the inn, the Healer's Hall, the field office, the gates and the docks; OSI holiday art plus MrRiots' and Vrark's, shipped in the player package). It goes up by itself on `clusterf.seasonal.winter.start` (default 12-01) and comes down after `.end` (default 01-06), server-local dates; `.automatic` (default true) keeps the hourly check on. `status` = date, season, what is placed; `preview` = what `place` would do; `place` = put up the rows not already up (allowed outside the dates, for the test shard; it says so); `clear` = remove everything it placed, by serial, and nothing else (the advent calendar too). An area (`bank`, `square`, `main road`, `inn`, `entrance`, `healer hall`, `field office`, `docks`) limits `preview`, `place` and `clear`; `up` and `down` still work. The same verbs run on the server console as `winter ...`. |
| `[SorcerersDungeon` | `[status\|on\|off\|remove\|boss <1-9>] [dryrun]` | cc-P408: Treasures of Sorcerer's Dungeon (ServUO's port of OSI's Halloween event, Ilshenar). It turns on by itself on the Halloween dates (`clusterf.seasonal.halloween.start` / `.end`, default 10-24 to 11-02) and off after them. `status` = date, season, researcher, spawner room, what stands in the world, the reward list; `on` = place the researcher at 536,456,-53 and the seven entrance pieces (once; a re-run places nothing twice) and start the boss spawner (it pauses the stock spawners in its nine rooms); `off` = stop the spawner, remove its creatures and boss, restart the paused spawners; the researcher and pieces stay; `remove` = `off`, then delete the researcher and the pieces it placed, by serial; `boss <1-9>` = raise that room's boss now (9 = Jack the Pumpkin King). Also on the server console as `tosd ...`. |
| `[TOSDSpawner` | — | cc-P408: ServUO's spawner gump for Treasures of Sorcerer's Dungeon: the nine rooms, the current one in green with its spawn and kill count; a button moves you into that room. Administrator. |
| `[EnchantedOriginArtifact` | `[budget]` | cc-P408: makes an artifact of Enchanted Origin (hue 1171) in your pack, to test the Sorcerer's Dungeon researcher's turn-in. Game Master. |

---

## Area Inspection

Requires **Admin** access.

| Command | Args | Description |
| --- | --- | --- |
| `[ClusterFAreaScan` | `[haven\|oldhaven\|newhaven]` | Scan a zone and write a full mobile/item inventory to `/tmp/areascan-{zone}.txt` on the server. Useful for auditing NPC placement. |

---

## One-Time World Setup Commands

These were run once during initial server setup and are documented here for reference. Re-running them on a live shard may duplicate spawners or content.

| Command | Notes |
| --- | --- |
| `[GenerateSpawners Data/Spawns/post-uoml/termur/TerMur.json` | Generated TerMur map spawners. Run once on initial setup. |
| `[GenerateSpawners Data/Spawns/shared/**/*.json` | Generated shared spawners for all maps. |
| `[GenerateSpawners Data/Spawns/post-uoml/**/*.json` | Generated post-UO:ML spawners. |

---

## Quick Reference — Testing a New Character

Typical sequence to set up a clean test account from scratch:

```text
[TestingReset          — wipe all progression data (target the character)
[TestingZeroSkills     — zero all skills (target the character)
[CompactStanding 0     — confirm Compact Standing is 0
[CompactVouchers 5     — seed a few vouchers if testing restoration flow
[CompactUnlockPickaxe  — grant the T1 pickaxe registry unlock
[CompactGivePickaxe    — place a fresh T1 Jacob's Pickaxe in pack
```

To test T2 flow from scratch:

```text
[TestingReset
[TestingZeroSkills
[CompactStanding 1000        — set to Apprentice rank threshold
[CompactVouchers 50          — cover the upgrade cost
[CompactUnlockPickaxe        — unlock T1 registry (T2 unlocks on acquisition)
[CompactGivePickaxe          — give T1 to consume during upgrade
```

To test restoration flow:

```text
[CompactGivePickaxe          — give a fresh T1
[CompactExhaust              — exhaust it (triggers OnDelete → exhausted replacement)
[CompactClearActive          — clear active-copy so restoration is offered
                             — then visit Garrett Ashveil and use the Restoration view
```
