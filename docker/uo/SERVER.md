# Ultima Online Server

This folder tracks the Ultima Online server emulator deployment.

For future gameplay systems, shard design direction, NPC guild concepts, legacy items, extended resources, and long-term roadmap ideas, see:

- `containers/uo/roadmap.md`

## Environment

| Item | Value |
| --- | --- |
| Platform | Proxmox LXC |
| Proxmox node | `creeper.clusterf.lab` |
| Container ID | `101` |
| Container hostname | `uo` |
| Planned FQDN | `uo.clusterf.lab` |
| Static IP | `10.7.4.120/16` |
| Gateway | `10.7.0.1` |
| Bridge | `vmbr0` |
| OS | Debian 12 |
| Storage | `local-lvm`, 16 GiB |
| CPU / memory | 2 cores, 2 GiB RAM, 512 MiB swap |
| Runtime | .NET 10 SDK |
| Emulator | ModernUO |
| Identity | FreeIPA client enrolled |

The first deployment is a base container plus ModernUO build baseline. Game data, initial shard configuration, and the final systemd service are separate steps so private assets and owner credentials are not stored in Git.

IP selection note: `10.7.4.110` was tested first but is already used by another LAN device, so UO uses `10.7.4.120`.

## Current State

| Item | Value |
| --- | --- |
| .NET SDK | `10.0.203` |
| .NET runtime | `10.0.7` |
| ModernUO version | `0.15.6.52` |
| ModernUO commit | `7c9215d97` |
| Published build path | `/opt/uo/modernuo/Distribution` |
| Enderman build artifact | `/tank/clusterf-artifacts/uo/modernuo/builds/modernuo-distribution-20260506T080202Z.tar.zst` |
| Enderman client data | `/tank/clusterf-artifacts/uo/modernuo/client-data/Classic Client` |
| Runtime client data | `/var/lib/uo/client-data/classic-client` |
| Client files synced | `516` |
| Config path | `/etc/uo/modernuo/Configuration` |
| Environment file | `/etc/uo/modernuo.env` |
| Owner account | `Aetherion` created interactively |
| Client connection | ClassicUO can connect to `10.7.4.120:2593` |
| Spawn data | Built-in JSON spawn definitions discovered and generated |
| Service | `uo.service` enabled and running |
| Auto account creation | Enabled, `5` accounts per source IP |
| Latest manual backup | `/tank/clusterf-artifacts/uo/modernuo/backups/uo-modernuo-pre-newhaven-repair-full-20260507T175211Z.tar.zst` |
| Skill caps | `300` per skill, dynamic total cap for all skills |
| Skill gain | ClusterF accelerated: `3` chance attempts, `5x` successful player gain amount |
| Base stat caps | `500` STR, `500` DEX, `500` INT, `1500` total |
| TerMur spawners | Generated via `[GenerateSpawners Data/Spawns/post-uoml/termur/TerMur.json` |
| New Haven quest NPCs | `ClusterFNewHavenSeeder` manages `40` named NPCs including League Registrar |
| Institution NPCs | `ClusterFInstitutionSeeder` manages League-affiliated liaisons; `[ClusterFSeedInstitutions` admin command |
| Healer policy | All players resurrectable (criminals and murderers allowed) |
| Self-resurrect | `[SelfRes` player command enabled |
| Shard name | `Shattered Legacy` (set via `serverListing.serverName` in `modernuo.json`) |
| Shard status | Operational and playable; Phase 1 League onboarding + Phase 2 Miners' Compact vertical slice + Phase 3 guild contracts/discovery/logistics complete |
| Account persistence | `ClusterFAccountData` v5 — Renown, AP, guild rep/currency, restoration registry, last-seen bulletin, account flags/flag-values, active/completed work orders, ore discoveries |
| Bulletin system | `ClusterFBulletin` — League Dispatch MOTD; file-based authoring via `bulletins.txt` |
| Achievement system | `ClusterFAchievements` — event-driven framework; 16 achievements (incl. 4 League); GUI gump with two-row tab bar (8 category tabs + Quests tab on row 2), progress bars, earn popup, New Haven quest tracker; `[achievements` command |
| Dev tools | `ClusterFDevTools` — selective reset gump (`[ClusterFReset [username]`) and force-delete character (`[ClusterFDeleteChar`) |
| Guild system | `ClusterFGuildSystem` — 12 guilds, account-wide membership (`JoinedGuilds`), context menu join flow, `[guild` command; fires `ClusterFLeagueSystem.OnGuildJoined` on every join |
| League system | `ClusterFLeagueSystem` — citizen status (Unregistered / Registered Citizen / Recognized Citizen), account flags, join/dispatch/referral/guild-join hooks, achievement triggers |
| Restoration registry | `ClusterFRestorationRegistry` — account-wide legacy item unlock tracking; `RestorationEntry` metadata; one-active-copy enforcement via `TryRestore`; admin commands `[RestorationUnlock`, `[RestorationRevoke`, `[RestorationClear`, `[RestorationList` |
| League Registrar NPC | **Elara Voss** (`LeagueRegistrar`) at Trammel `3459, 2601 Z18` — full dialogue gump (join, status, renown, achievements, guilds, dispatch, referrals, guidance); ambient speech every 25–45s; proximity greeting on first approach per session; Talk context menu; quest arrow to Miners' Compact Liaison |
| Miners' Compact Liaison NPC | **Garrett Ashveil** (`MinersCompactLiaison`) at Trammel `3510, 2748 Z0` — full member dashboard (standing, rank, vouchers), work orders, restoration (T1 + T2), tier 2 upgrade; Talk context menu |
| Extended ores | 8 extended ore/ingot types (Platinum–Celestial, tiers 10–17); GM Mining required; Mining veins extended at WorldLoad; Blacksmithy sub-resources extended at ServerStarted; `RandomizeVeins=false` — veins are permanent per 8×8 cell via coordinate seed |
| Compact Work Orders | 25 orders across Initiate → Deepwarden tiers (iron, colored, and extended ore orders); discovery-gated eligibility — colored ore orders require the player to have reported that ore type to the Survey Archivist; `GuildContractLedgerGump` — Available/Active/History tabs, back button, accept/turn-in flow |
| Prospector's Logbook | `ProspectorsLogbook` — blessed item issued on guild join; records first-find date, location, facet, region, quantity, and state per ore type; account-backed (`ClusterFAccountData.OreDiscoveries`); discovery auto-logged via `CompactOreSatchelRoutingHook` on every colored ore yield |
| Survey Archivist NPC | **Velara Thorne** (`SurveyArchivist`) at Trammel `3516, 2747 Z1` (temporary — south mine entrance); accepts ore discovery reports; grants Compact Standing + Mining Vouchers per ore tier; `SurveyArchivistGump` — MainMenu and Discoveries views, Report All flow |
| Compact Ore Satchel | `CompactOreSatchel` — 50% weight reduction, 400 max weight; accepts all ore/ingot types Iron → Celestial + Granite; auto-routes newly mined ore via `CompactOreSatchelRoutingHook` (partial `Mining.Give()` override); issued on guild join |
| Jacob's Pickaxe | Tier 1 legacy tool — +5 Mining, 150 uses, blessed, non-disposable (exhausted state instead of deletion), registry-tracked, restorable via Compact Liaison; `OnDelete` handles both pack and equipped-layer state; `OnEquip` blocks equipping while exhausted; `ShowUsesRemaining` suppressed via `IUsesRemaining` interface cast when exhausted |
| Jacob's Reinforced Pickaxe | Tier 2 upgrade — +10 Mining, 400 uses, blessed, Dull Copper hue (0x8A5C); requires Apprentice rank + 50 vouchers + ingots + gold; full durability MVP; `OnEquip` blocks while exhausted; restorable via Compact Liaison at reduced cost (25V + 500 Iron + 100 DC + 12,500gp); upgrade scan checks equipped layer and backpack |
| Compact admin tools | `[CompactStanding`, `[CompactVouchers`, `[CompactRank`, `[CompactUnlockPickaxe`, `[CompactGivePickaxe`, `[CompactGiveReinforcedPickaxe`, `[CompactExhaust`, `[CompactClearActive`, `[CompactWipeLogbook`, `[CompactSeedDiscoveries`, `[CompactGiveSatchel`, `[CompactGiveLogbook`, `[CompactOreInfo`, `[TestingReset`, `[TestingZeroSkills` |
| Client patcher | `Install-UOClientPatch.ps1` — PowerShell 5.1+ patch installer; manifest/checksum verification, timestamped backup, install log; patch `newhaven-league-office` v`1.0.0` deployed to `\\enderman.clusterf.lab\uo-client-patches` (guest read-only, no credentials required on LAN/VPN) |
| Health check | `uo-healthcheck` HTTP service on port `12001` — reports `uo.service` state |

## Command Reference

All custom in-game commands (player, GM, admin, seeders, testing) are documented in **[COMMANDS.md](COMMANDS.md)**.

---

## ModernUO Decision

ClusterF uses ModernUO for the first UO shard.

Reasons:

- ModernUO targets modern .NET and has native Linux support.
- Debian 12 is a supported platform.
- The build flow supports scripted Linux publishing with `./publish.sh release linux x64`.
- It fits the LXC model better than older Mono-first server stacks.

References:

- `https://modernuo.com/docs/getting-started/installation/`
- `https://modernuo.com/docs/getting-started/building/`
- `https://modernuo.com/docs/getting-started/starting/`
- `https://modernuo.com/commands.html`


## Deployment

Run from the repository root on the Windows workstation:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\scripts\deploy\uo\deploy-uo-creeper-lxc.sh `
  boblin@creeper.clusterf.lab:/tmp/deploy-uo-creeper-lxc.sh

scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  $env:USERPROFILE\.ssh\clusterf_boblin.pub `
  boblin@creeper.clusterf.lab:/tmp/clusterf_boblin.pub

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@creeper.clusterf.lab `
  "sudo bash /tmp/deploy-uo-creeper-lxc.sh"
```

The script:

- downloads the Debian 12 LXC template if needed
- creates CT `101` on `creeper`
- configures static networking
- installs baseline admin tools
- creates the `boblin` deploy user
- creates UO directories

Prepare Enderman artifact folders:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\scripts\deploy\uo\prepare-modernuo-artifacts-enderman.sh `
  boblin@enderman.clusterf.lab:/tmp/prepare-modernuo-artifacts-enderman.sh

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@enderman.clusterf.lab `
  "sudo bash /tmp/prepare-modernuo-artifacts-enderman.sh"
```

Prepare ModernUO on the UO container:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\scripts\deploy\uo\deploy-modernuo-uo-container.sh `
  boblin@10.7.4.120:/tmp/deploy-modernuo-uo-container.sh

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo bash /tmp/deploy-modernuo-uo-container.sh"
```

The ModernUO script:

- installs ModernUO Linux prerequisites
- installs the Microsoft package feed for Debian 12
- installs `dotnet-sdk-10.0`
- clones `https://github.com/modernuo/ModernUO.git` into `/opt/uo/modernuo`
- publishes a Linux x64 release build when `BUILD_MODERNUO=1`
- links mutable runtime paths from the distribution into `/etc/uo`, `/var/lib/uo`, and `/var/log/uo`

Package the published build to Enderman:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\scripts\deploy\uo\Publish-ModernUOBuildArtifact.ps1
```

Sync Classic client data from Enderman into the UO container:

```powershell
wsl -d Debian -- bash -lc "cp /mnt/c/Users/chase.fleming/.ssh/clusterf_boblin /tmp/clusterf_boblin && chmod 600 /tmp/clusterf_boblin && cd /mnt/g/Repos/clusterf-infrastructure && SSH_KEY=/tmp/clusterf_boblin ./scripts/deploy/uo/sync-modernuo-client-data.sh"
```

The sync script copies:

```text
/tank/clusterf-artifacts/uo/modernuo/client-data/Classic Client
```

to:

```text
/var/lib/uo/client-data/classic-client
```

The no-space runtime path avoids quoting problems in service files and repeatable scripts.

## Initial ModernUO Configuration

ModernUO has been launched far enough to validate the client data, write initial config, and create the owner account.

Current config:

| Item | Value |
| --- | --- |
| Client data path | `/var/lib/uo/client-data/classic-client` |
| Client version detected | `7.0.114.40` |
| Listener | `0.0.0.0:2593` |
| Shard name | `ModernUO` |
| Expansion | `Endless Journey` |
| Required client | `7.0.61.0` |
| Enabled maps | Felucca, Trammel, Ilshenar, Malas, Tokuno, TerMur |
| Owner account | `Aetherion` |
| Auto account creation | `accountHandler.enableAutoAccountCreation=True` |
| Accounts per source IP | `accountHandler.maxAccountsPerIP=5` |

Run ModernUO interactively from a local terminal during setup/testing:

```powershell
ssh -t -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 "sudo -u uo -H bash -lc 'cd /opt/uo/modernuo/Distribution && ./ModernUO'"
```

### Runtime permission fix

ModernUO initially failed when running as the `uo` user because `Configuration/throttles.json` was not writable through the runtime symlink path:

```text
/opt/uo/modernuo/Distribution/Configuration -> /etc/uo/modernuo/Configuration
```

Fix mutable runtime ownership before running as `uo`:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 "sudo chown -R uo:uo /etc/uo/modernuo /var/lib/uo/modernuo /var/log/uo/modernuo && sudo chmod -R u+rwX /etc/uo/modernuo /var/lib/uo/modernuo /var/log/uo/modernuo"
```

### Owner account

The owner account was created interactively and should not be stored with credentials in Git.

| Item | Value |
| --- | --- |
| Owner account name | `Aetherion` |
| Access level | Owner/admin account created during ModernUO first launch |
| Client tested | ClassicUO connected successfully to `10.7.4.120:2593` |

### Player account creation

Player accounts are created automatically on first successful login when the client supplies a new account name and password.

Current settings in `/etc/uo/modernuo/Configuration/modernuo.json`:

```text
accountHandler.enableAutoAccountCreation=True
accountHandler.maxAccountsPerIP=5
```

The account-per-IP limit was raised from `1` to `5` because multiple household or VPN users can appear from the same source address. With the default limit of `1`, a second account such as `Cain` is rejected after the owner account has already logged in from the same client IP.

Verify the policy:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo grep -n 'accountHandler' /etc/uo/modernuo/Configuration/modernuo.json"
```

After changing account settings, restart the shard:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

## World, Decoration, and Spawners

The server is reachable, the owner account can enter the shard, and ClassicUO can connect to the ModernUO listener.

Generate command documentation from the owner account if needed:

```text
[docgen
```

The generated command reference is written inside the UO container at:

```text
/opt/uo/modernuo/Distribution/web/commands.html
```

Copy it to the Windows workstation for browser review:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120:/opt/uo/modernuo/Distribution/web/commands.html `
  "$env:USERPROFILE\Downloads\modernuo-commands.html"

Start-Process "$env:USERPROFILE\Downloads\modernuo-commands.html"
```

### Decoration notes

`CreateWorld` is not available in this ModernUO build. Do not document or depend on it for ClusterF setup.

Relevant generation/decorating commands include:

```text
[Decorate
[DecorateMag
[DoorGen
[MoonGen
[SignGen
[TelGen
```

Magincia remains partially inconsistent after `DecorateMag`. This appears to match long-standing RunUO/ServUO ecosystem issues around ruined Magincia, rebuilt Magincia, client statics, and era-specific decoration data.

Current Magincia decision:

- leave Magincia blank or partially undecorated for now
- do not block the shard on Magincia polish
- revisit later with imported or custom rebuilt-Magincia decoration data
- investigate ServUO rebuilt/New Magincia decoration packs, including packages with commands similar to `DecorateMagincia` and `MaginciaDelete`

### Spawner data discovery

ModernUO shipped built-in JSON spawn definitions under:

```text
Data/Spawns/shared
Data/Spawns/post-uoml
Data/Spawns/uoml
```

Discovery command used on the UO container:

```bash
sudo -u uo bash -lc "cd /opt/uo/modernuo/Distribution && find . -type f | grep -Ei 'spawn|spawns|\.json$|\.xml$' | sort"
```

Notable spawn data includes vendors, townspeople, wildlife, outdoor spawns, and dungeon spawns for Felucca, Trammel, Ilshenar, Malas, Tokuno, and expansion-specific areas.

The `GenerateSpawners` command reports this usage:

```text
[GenerateSpawners <relative search pattern to distribution>
```

The argument must be a path or glob pattern relative to:

```text
/opt/uo/modernuo/Distribution
```

Spawner commands have been run for the discovered spawn sets, including the broad shared dataset and expansion layers:

```text
[GenerateSpawners Data/Spawns/shared/**/*.json
[GenerateSpawners Data/Spawns/post-uoml/**/*.json
[GenerateSpawners Data/Spawns/uoml/**/*.json
```

Save after successful generation:

```text
[save
```

Do not run generation/import commands repeatedly without restoring a backup first; repeated runs may duplicate decorations or spawners.

## New Haven Quest Seeding

ModernUO includes New Haven ML quest definitions in:

```text
/opt/uo/modernuo/Distribution/Data/MLQuests.cfg
```

Those definitions wire quests to NPC classes, but the shipped decoration and spawn data did not place the named New Haven quest NPCs in the live world. ClusterF adds a duplicate-safe seeder:

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFNewHavenSeeder.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFNewHavenSeeder.cs` |
| Command | `[ClusterFSeedNewHaven [missing\|dryrun\|repair\|replace]` |
| Enabled setting | `clusterf.newHavenSeeder.enabled=True` |
| World-load setting | `clusterf.newHavenSeeder.seedOnWorldLoad=True` |
| Repair-on-load setting | `clusterf.newHavenSeeder.repairOnWorldLoad=True` |
| Latest pre-repair backup | `/tank/clusterf-artifacts/uo/modernuo/backups/uo-modernuo-pre-newhaven-repair-full-20260507T175211Z.tar.zst` |

The seeder places:

- `SirHelper` in New Haven town square
- 12 basic New Haven training questers
- 26 New Haven skill-training instructors

Default mode creates only missing NPCs:

```text
[ClusterFSeedNewHaven
```

Check what would be created or repaired without changing the world:

```text
[ClusterFSeedNewHaven dryrun
```

Repair the seeded New Haven NPC locations without deleting existing NPCs:

```text
[ClusterFSeedNewHaven repair
```

Delete and rebuild the seeded New Haven NPC set:

```text
[ClusterFSeedNewHaven replace
```

The first live seed pass ran during world load and reported:

```text
ClusterF New Haven seed complete: created 39, skipped 0, deleted 0.
```

The latest live repair pass ran during world load and reported:

```text
ClusterF New Haven seed complete: created 0, repaired 21, skipped 18, deleted 0.
```

The 21 repaired entries reflect the wiki + ServUO coordinate audit applied on 2026-05-08 (see NPC placement table below).

Notable New Haven NPC placement decisions:

| Trainer | Skill | Location | Trammel coords | Notes |
| --- | --- | --- | --- | --- |
| `Jacob Waltz` | Mining | Mine camp (south mountains) | `(3511, 2744, 0)` | Moved from north hills — wiki confirms southern mountains, co-located near Mugg and Gervis at the south mine |
| `Gervis` | Blacksmithing | Mountainside (south) | `(3505, 2749, 0)` | Moved from Forge and Anvil — wiki confirms mountainside south of New Haven; coordinates from ServUO-Lokai reference |
| `Mugg` | Mining | Mine (south mountains) | `(3507, 2747, 0)` | Moved from north mine entrance — wiki confirms mine area; coordinates from ServUO-Lokai reference |
| `Sarsmea Smythe` | Focus | New Haven Bank | `(3492, 2577, 15)` | Moved from town square — wiki confirms in front of the New Haven Bank |
| `Hamato` | Bushido | Hamato Dojo (north) | `(3494, 2414, 55)` | Moved north of town center; coordinates from ServUO-Lokai reference |
| `Mulcivikh` | Necromancy | Necromancers Guild (east) | `(3555, 2457, 15)` | Moved to east edge; coordinates from ServUO-Lokai reference |
| `Morganna` | Spirit Speak | Necromancers Guild (east) | `(3547, 2462, 15)` | Co-located with Mulcivikh at east guild; coordinates from ServUO-Lokai reference |
| `George Hephaestus` | Blacksmithing | Forge and Anvil | `(3471, 2542, 36)` | Adjusted from `(3520, 2529)` — ServUO-Lokai reference |
| `Ryuichi` | Ninjitsu | Ninja Dojo | `(3422, 2520, 21)` | Moved from `(3413, 2602)` — ServUO-Lokai reference |
| `Amelia Youngstone` | Tinkering | Springs N Things | `(3459, 2529, 53)` | Moved from `(3489, 2571)` — ServUO-Lokai reference |

All coordinates for the moved NPCs were sourced from the ServUO-Lokai `Haven.cfg` spawn configuration file as the closest available reference to OSI vanilla placement. The `IsNewHavenArea` bounding box was expanded from `Y:2470-2660` to `Y:2400-2760` to include Hamato (north dojo, Y≈2414) and Gervis/Mugg (south mountains, Y≈2747-2749).

### South mine forge and anvil

The south mountain mine camp (where Jacob Waltz, Mugg, and Gervis stand) has no forge or anvil in the base map data. `ClusterFSouthMineDecor.cs` places a `Forge` and an `Anvil` as world items at the camp so players can smith there.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFSouthMineDecor.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSouthMineDecor.cs` |
| Command | `[ClusterFSouthMineDecor [dryrun\|replace]` |
| Enabled setting | `clusterf.southMineDecor.enabled=True` |
| Forge location | Trammel `(3508, 2746, 0)` |
| Anvil location | Trammel `(3510, 2746, 0)` |

Items are placed on world load if missing, skipped if already present. Use `replace` to delete and re-place them.

### World save before restart

`uo.service` is configured with `KillSignal=SIGINT`. ModernUO's clean-shutdown path (triggered by SIGINT) saves the world automatically. Running `sudo systemctl restart uo.service` is sufficient — no separate in-game `[save` is needed before a scripted restart.

The New Haven escort quests are handled separately by ModernUO's escortable townfolk system when those townfolk spawn on Haven Island.

## Old Haven Ruins

Old Haven is the ruined precursor city to New Haven, located east of New Haven on the same Trammel island (centre ~3677, 2625). The area contains undead spawns (skeletons, zombies) from the base world data and is intended as a mid-difficulty zone for training **Magic Resist** and as a mini-boss encounter location.

### Old Haven creature spawners

ClusterF adds two custom creature types and a spawner seeder:

| Item | Value |
| --- | --- |
| Repository source (mage) | `containers/uo/customizations/OldHavenMage.cs` |
| Repository source (boss) | `containers/uo/customizations/DrelgorTheImpaler.cs` |
| Repository source (seeder) | `containers/uo/customizations/ClusterFOldHavenSeeder.cs` |
| Installed source (mage) | `/opt/uo/modernuo/Projects/UOContent/Misc/OldHavenMage.cs` |
| Installed source (boss) | `/opt/uo/modernuo/Projects/UOContent/Misc/DrelgorTheImpaler.cs` |
| Installed source (seeder) | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFOldHavenSeeder.cs` |
| Seed command | `[ClusterFSeedOldHaven [missing\|dryrun\|replace]` |
| Enabled setting | `clusterf.oldHavenSeeder.enabled=True` |
| World-load setting | `clusterf.oldHavenSeeder.seedOnWorldLoad=False` |

**OldHavenMage** (`AI_Mage`, Magery 65–80, EvalInt 65–80, HP 90–130): spectral caster wearing a blue-grey robe. Casts 3rd–5th circle spells (Fireball, Lightning, Energy Bolt). Low enough HP that players survive while taking hits — designed for **Magic Resist** skill gains. Fame 1500.

**DrelgorTheImpaler** (`AI_Melee`, 450–550 HP): undead warlord in dark plate armour with a viking sword. Mini-boss feel — single instance with an 8–15 minute respawn. Fame 8000, drops Rich + Average loot.

Spawner locations (all Trammel):

| Spawner | Coords | Count | Respawn |
| --- | --- | --- | --- |
| Drelgor the Impaler | `(3622, 2488)` | 1 | 8–15 min |
| Old Haven Mage – north gate | `(3588, 2457)` | 3 | 2–5 min |
| Old Haven Mage – west ruins | `(3556, 2487)` | 2 | 2–5 min |
| Old Haven Mage – town square | `(3600, 2510)` | 3 | 2–5 min |
| Old Haven Mage – south courtyard | `(3618, 2540)` | 2 | 2–5 min |
| Old Haven Mage – east rubble | `(3645, 2495)` | 2 | 2–5 min |

Seed the spawners (first-time or after a fresh world):

```text
[ClusterFSeedOldHaven
```

Replace all spawners (e.g. after a coordinate adjustment):

```text
[ClusterFSeedOldHaven replace
```

### Old Haven NPC cleanup

The base ModernUO world decoration data placed a full complement of functional town vendors and the old pre-ML `Uzeraan` quest hub inside the Old Haven ruins. These were removed on 2026-05-12 as they are thematically wrong for a destroyed, undead-infested city and Uzeraan's quest chain is unreachable now that new players start in New Haven.

Removed categories:

- **5 Newbie Manor guards** (`MansionGuard`) left over from the original Haven starting area
- **Uzeraan** and his two associated blacksmiths — pre-ML Haven quest hub, now orphaned
- **~35 normal town vendors** (baker, banker, tailor, tinker, guildmasters, innkeeper, etc.) stranded in the ruins

Intentionally kept:

- `EvilWanderingHealer` ("Ker", "Connie") — thematically appropriate cursed healers
- `TownCrier "Tod"` — ghost of a once-busy city
- All skeleton/zombie/rat undead spawns from base world data
- Wildlife around the ruins perimeter

The cleanup is recorded in `ClusterFOldHavenCleanup.cs` as a one-shot command matched by exact type + coordinates:

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFOldHavenCleanup.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFOldHavenCleanup.cs` |
| Command | `[ClusterFOldHavenCleanup [dryrun]` |

The cleanup has already been run against the live world. The command is retained so it can be re-run if the world is ever restored from a backup pre-dating the cleanup.

### Area scan diagnostic

`ClusterFAreaScan` writes a full dump of all mobiles and notable items in a named zone to a server-side text file for review:

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFAreaScan.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFAreaScan.cs` |
| Command | `[ClusterFAreaScan [haven\|oldhaven\|newhaven]` |
| Output file | `/tmp/areascan-{zone}.txt` on the UO container |

Retrieve after running:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120:/tmp/areascan-haven.txt "$env:TEMP\areascan-haven.txt"
```

Zones:

| Zone | Bounds | Covers |
| --- | --- | --- |
| `haven` | `(3400,2400)–(3760,2760)` | Entire Haven island |
| `oldhaven` | `(3520,2420)–(3700,2640)` | Old Haven ruins only |
| `newhaven` | `(3400,2490)–(3570,2760)` | New Haven town only |

## Character Stat Inspector

Players can view a full summary of all their character stats, item bonuses, and skill modifiers via the `[stats` command.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFStatInspect.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFStatInspect.cs` |
| Command | `[stats` (player-accessible) |

The gump shows:

- **Core Stats**: Str / Dex / Int (effective, with item bonus callout), HP / Stam / Mana pools, Pet Mimic HP if equipped
- **Resistances**: Physical, Fire, Cold, Poison, Energy (computed totals from all equipped items)
- **Combat**: Hit Chance Inc, Def Chance Inc, Damage Inc, Swing Speed Inc, Reflect Physical, Enhance Potions
- **Magic**: Lower Reg Cost, Lower Mana Cost, Faster Casting, FC Recovery, Spell Dmg Inc, Luck
- **Regeneration**: HP / Stam / Mana regen bonuses from items
- **Skill Bonuses**: All positive skill mods from equipped items, sorted alphabetically, two columns

All values aggregate across every equipped item (weapons, armor, jewelry, clothing, talismans, Pet Mimic). Skill bonuses are read directly from the mobile's active `SkillMod` list so any source (items, potions, temporary buffs) is reflected.

### Note on armor stat absorption

Vanilla armor pieces in UO carry only resistance values — they have no LRC, FC, Damage Increase, or skill bonuses. Feeding standard leather or plate armor to a Pet Mimic correctly absorbs only resistances because those items genuinely have no other AOS mods. Armor with AOS bonuses (artifact armor, runic-crafted pieces, or items modified via `[props`) will have those additional stats absorbed correctly.

## Gameplay Roadmap

ClusterF intentionally diverges from traditional OSI-era progression limits. The shard is planned as a high-power, long-term progression environment with custom PvE scaling and custom progression systems.

Implemented progression policy:

| System | Target |
| --- | --- |
| Individual skill cap | `300` |
| Total skill cap | high enough for every ModernUO skill to reach `300` |
| Skill gain | significantly faster player progression while preserving normal ModernUO skill checks |
| Base stat cap | `500` STR, `500` DEX, `500` INT, `1500` total |

Current implementation:

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFSkillCaps.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSkillCaps.cs` |
| Config file | `/etc/uo/modernuo/Configuration/modernuo.json` |
| Enabled setting | `clusterf.skillCaps.enabled=True` |
| Individual cap setting | `clusterf.skillCaps.individualCap=300` |
| Total cap setting | `clusterf.skillCaps.totalCap=0` |

`clusterf.skillCaps.totalCap=0` means the total cap is calculated from the ModernUO skill table length multiplied by the individual cap. This avoids hard-coding a low total cap such as `700` or `720`, and it keeps the policy aligned if the skill table changes later.

Caps are applied:

- when the world loads
- when a player logs in
- when an administrator runs `[ClusterFSkillCaps]`
- when an administrator runs `[ClusterFSkillCaps all]` for online players

### Stat caps

ClusterF raises the normal player base stat limits to support the high-power progression model.

Current implementation:

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFStatCaps.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFStatCaps.cs` |
| Config file | `/etc/uo/modernuo/Configuration/modernuo.json` |
| Command | `[ClusterFStatCaps [all]` |
| Enabled setting | `clusterf.statCaps.enabled=True` |
| Individual stat cap setting | `clusterf.statCaps.individualCap=500` |
| Total stat cap setting | `clusterf.statCaps.totalCap=1500` |
| ModernUO stat gain max | `stats.statMax=500` |
| Latest pre-change backup | `/tank/clusterf-artifacts/uo/modernuo/backups/uo-modernuo-pre-statcaps-full-20260507T172701Z.tar.zst` |

`ClusterFStatCaps.cs` sets the total stat cap on player mobiles and exposes `ClusterFStatCaps.IndividualStatCap` for use by the engine patch below. The total cap is applied on world load and login; the individual cap is enforced by the `PlayerMobile.cs` engine patch at the property getter level.

#### Individual stat cap bug and engine patch

**Root cause:** `PlayerMobile.cs` in ModernUO core overrides the `Str`, `Int`, and `Dex` property getters and hardcodes an individual cap of `150` when ML-era rules are active (`Core.ML && AccessLevel == AccessLevel.Player`). This override runs regardless of any value set in `ClusterFStatCaps.cs`, which is why individual stats stopped at `150` even though the total cap showed `1500` correctly.

**Fix applied 2026-05-08:**

- Added `public static int IndividualStatCap` to `ClusterFStatCaps.cs`. Returns the configured cap when the module is enabled; falls back to `150` (vanilla) when disabled.
- Patched `PlayerMobile.cs` on the UO container to replace the three hardcoded `150` references with `ClusterFStatCaps.IndividualStatCap`.

The patch is tracked at:

```text
containers/uo/patches/PlayerMobile-individual-stat-cap.patch
```

This patch must be reapplied any time the ModernUO source is updated or re-cloned. See the Engine Patches section below.

Stat caps are applied:

- when the world loads
- when a player logs in
- when an administrator runs `[ClusterFStatCaps]`
- when an administrator runs `[ClusterFStatCaps all]` for online players

After changing `ClusterFStatCaps.cs`, copy it into the ModernUO source tree and rebuild the content assembly:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFStatCaps.cs `
  boblin@10.7.4.120:/tmp/ClusterFStatCaps.cs

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo install -o uo -g uo -m 0644 /tmp/ClusterFStatCaps.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFStatCaps.cs"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

## Resurrection

### Healer vendors

Healer NPCs (`Healer` class) offer resurrection to dead player ghosts when the ghost walks within 4 tiles. The vanilla engine blocks resurrection for criminals and murderers.

ClusterF overrides this via `ClusterFHealerPolicy.cs` — by default, all players can be resurrected by healers regardless of criminal or murder status.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFHealerPolicy.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFHealerPolicy.cs` |
| Config file | `/etc/uo/modernuo/Configuration/modernuo.json` |
| Command | `[ClusterFHealerPolicy` |
| Enabled setting | `clusterf.healerPolicy.enabled=True` |
| Allow criminals setting | `clusterf.healerPolicy.allowCriminals=True` |
| Allow murderers setting | `clusterf.healerPolicy.allowMurderers=True` |
| Engine patch | `containers/uo/patches/Healer-resurrection-policy.patch` |

**Known healer trigger limitation:** the healer only offers resurrection when a ghost walks *into* range from outside 4 tiles. If a player dies while already within range, nothing triggers until the ghost walks away and back. This is a vanilla ModernUO mechanic. Use `[selfres` as a reliable alternative.

### Self-resurrection command

Players can resurrect themselves from ghost form at any time using `[selfres`. This bypasses the healer range mechanic and is the recommended approach for solo testing.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFSelfResurrect.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSelfResurrect.cs` |
| Config file | `/etc/uo/modernuo/Configuration/modernuo.json` |
| Command | `[SelfRes` (player-accessible) |
| Enabled setting | `clusterf.selfResurrect.enabled=True` |

Behavior:

- only works when dead (ghost form)
- plays a resurrection sound and visual effect
- fails gracefully if the current tile cannot fit a living player (move and retry)
- sends a confirmation message on success

After changing either file, copy it into the ModernUO source tree and rebuild:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFHealerPolicy.cs `
  boblin@10.7.4.120:/tmp/ClusterFHealerPolicy.cs

scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFSelfResurrect.cs `
  boblin@10.7.4.120:/tmp/ClusterFSelfResurrect.cs

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo install -o uo -g uo -m 0644 /tmp/ClusterFHealerPolicy.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFHealerPolicy.cs && sudo install -o uo -g uo -m 0644 /tmp/ClusterFSelfResurrect.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSelfResurrect.cs"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

## Engine Patches

ClusterF modifies a small number of core ModernUO source files that cannot be overridden through the normal customization path. These patches are tracked in:

```text
containers/uo/patches/
```

Each patch must be reapplied manually after a ModernUO source update or re-clone. Run the verify step after reapplying to confirm the patch is present before rebuilding.

### PlayerMobile-individual-stat-cap.patch

| Item | Value |
| --- | --- |
| Patch file | `containers/uo/patches/PlayerMobile-individual-stat-cap.patch` |
| Target file | `/opt/uo/modernuo/Projects/UOContent/Mobiles/PlayerMobile.cs` |
| Applied | 2026-05-08 |
| Reason | `PlayerMobile.cs` hardcodes a `150` individual stat cap in the `Str`, `Int`, and `Dex` property getters when ML-era rules are active. This cap runs at the property getter level and cannot be overridden by any external customization file. The patch replaces the hardcoded `150` with `ClusterFStatCaps.IndividualStatCap` so the configured cap is respected. |

Verify the patch is applied on the UO container:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "grep -n 'IndividualStatCap' /opt/uo/modernuo/Projects/UOContent/Mobiles/PlayerMobile.cs"
```

Expected output (three lines):

```text
594:                    return Math.Min(base.Str, ClusterFStatCaps.IndividualStatCap);
609:                    return Math.Min(base.Int, ClusterFStatCaps.IndividualStatCap);
624:                    return Math.Min(base.Dex, ClusterFStatCaps.IndividualStatCap);
```

Apply or reapply the patch from the repository root:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\patches\PlayerMobile-individual-stat-cap.patch `
  boblin@10.7.4.120:/tmp/PlayerMobile-individual-stat-cap.patch

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo patch -p1 -d /opt/uo/modernuo < /tmp/PlayerMobile-individual-stat-cap.patch"
```

After applying, rebuild and restart:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

### HealerGuildmaster-resurrection.patch

| Item | Value |
| --- | --- |
| Patch file | `containers/uo/patches/HealerGuildmaster-resurrection.patch` |
| Target file | `/opt/uo/modernuo/Projects/UOContent/Mobiles/Vendors/NPC/Guildmasters/HealerGuildmaster.cs` |
| Applied | 2026-05-08 |
| Reason | `HealerGuildmaster` (the class used by named healer NPCs such as Kalkin in New Haven) extends `BaseGuildmaster`, not `BaseHealer`. It has no resurrection logic. The patch adds an `OnMovement` override that offers resurrection to dead player ghosts who walk within 4 tiles, using the same logic as `BaseHealer` and respecting `ClusterFHealerPolicy`. |

Verify the patch is applied on the UO container:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "grep -n 'OnMovement' /opt/uo/modernuo/Projects/UOContent/Mobiles/Vendors/NPC/Guildmasters/HealerGuildmaster.cs"
```

Expected output:

```text
31:        public override void OnMovement(Mobile m, Point3D oldLocation)
33:            base.OnMovement(m, oldLocation);
```

Apply or reapply the patch from the repository root:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\patches\HealerGuildmaster-resurrection.patch `
  boblin@10.7.4.120:/tmp/HealerGuildmaster-resurrection.patch

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo patch -p1 -d /opt/uo/modernuo < /tmp/HealerGuildmaster-resurrection.patch"
```

After applying, rebuild and restart:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

### Bandage-pet-mimic-targeting.patch

| Item | Value |
| --- | --- |
| Patch file | `containers/uo/patches/Bandage-pet-mimic-targeting.patch` |
| Target file | `/opt/uo/modernuo/Projects/UOContent/Items/Skill Items/Misc/Bandage.cs` |
| Applied | 2026-05-08 |
| Reason | Vanilla `Bandage.InternalTarget.OnTarget` only accepts `Mobile` and `PlagueBeastInnard` targets. The patch adds a `PetMimic` case so players can double-click a bandage and directly target a mimic in their pack to restore its HP, matching the same experience as bandaging a pet. Also adds the case to `OnNonlocalTarget` for container-carried mimics. |

Verify the patch is applied on the UO container:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "grep -n 'PetMimic' '/opt/uo/modernuo/Projects/UOContent/Items/Skill Items/Misc/Bandage.cs'"
```

Expected output (two lines):

```text
121:            if (targeted is PetMimic mimic)
139:            else if (targeted is PetMimic mimic)
```

Apply or reapply the patch from the repository root:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\patches\Bandage-pet-mimic-targeting.patch `
  boblin@10.7.4.120:/tmp/Bandage-pet-mimic-targeting.patch

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo patch -p1 -d /opt/uo/modernuo < /tmp/Bandage-pet-mimic-targeting.patch"
```

After applying, rebuild and restart:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet publish Projects/Application/Application.csproj -c Build -r linux-x64 --no-restore --self-contained=false"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

### AOS-pet-mimic-attribute-aggregation.patch

| Item | Value |
| --- | --- |
| Patch file | `containers/uo/patches/AOS-pet-mimic-attribute-aggregation.patch` |
| Target file | `/opt/uo/modernuo/Projects/UOContent/Misc/AOS.cs` |
| Applied | 2026-05-08 |
| Reason | `AosAttributes.GetValue` iterates equipped items and aggregates attribute values (LRC, FC, FCR, DI, etc.) for BaseWeapon, BaseArmor, BaseJewel, BaseClothing, Spellbook, BaseQuiver, and BaseTalisman. PetMimic implements `IAosItem` and needs to be included in this aggregation so its accumulated LRC/FC/FCR/DI bonuses reach the wearer's stat totals. The patch adds a `PetMimic` case after the `BaseTalisman` block. |

Verify the patch is applied on the UO container:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "grep -n 'PetMimic' /opt/uo/modernuo/Projects/UOContent/Misc/AOS.cs"
```

Expected output:

```text
583:                else if (obj is PetMimic mimic)
```

Apply or reapply the patch from the repository root:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\patches\AOS-pet-mimic-attribute-aggregation.patch `
  boblin@10.7.4.120:/tmp/AOS-pet-mimic-attribute-aggregation.patch

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo patch -p1 -d /opt/uo/modernuo < /tmp/AOS-pet-mimic-attribute-aggregation.patch"
```

After applying, rebuild and restart:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet publish Projects/Application/Application.csproj -c Build -r linux-x64 --self-contained=false"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

### Healer-resurrection-policy.patch

| Item | Value |
| --- | --- |
| Patch file | `containers/uo/patches/Healer-resurrection-policy.patch` |
| Target file | `/opt/uo/modernuo/Projects/UOContent/Mobiles/Healers/Healer.cs` |
| Applied | 2026-05-08 |
| Reason | Vanilla `Healer.CheckResurrect` unconditionally blocks resurrection for criminals and murderers. The patch makes both checks conditional on `ClusterFHealerPolicy.AllowCriminals` and `ClusterFHealerPolicy.AllowMurderers`, which default to `True` on ClusterF so all players can be resurrected. |

Verify the patch is applied on the UO container:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "grep -n 'ClusterFHealerPolicy' /opt/uo/modernuo/Projects/UOContent/Mobiles/Healers/Healer.cs"
```

Expected output (two lines):

```text
43:        if (m.Criminal && !ClusterFHealerPolicy.AllowCriminals)
50:        if (m.Murderer && !ClusterFHealerPolicy.AllowMurderers)
```

Apply or reapply the patch from the repository root:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\patches\Healer-resurrection-policy.patch `
  boblin@10.7.4.120:/tmp/Healer-resurrection-policy.patch

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo patch -p1 -d /opt/uo/modernuo < /tmp/Healer-resurrection-policy.patch"
```

After applying, rebuild and restart:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

### BaseWeapon-talisman-durability.patch

| Item | Value |
| --- | --- |
| Patch file | `containers/uo/patches/BaseWeapon-talisman-durability.patch` |
| Target file | `/opt/uo/modernuo/Projects/UOContent/Items/Weapons/BaseWeapon.cs` |
| Applied | 2026-05-08 |
| Reason | `BaseWeapon.AbsorbDamageAOS` calls `IWearableDurability.OnHit` on a randomly selected armor slot when the defender takes damage. The talisman slot is not included in that loop. The patch adds a call for the talisman slot item so `PetMimic` (which implements `IWearableDurability`) receives `OnHit` on every combat hit to the wearer, matching the armor durability trigger behavior. |

Verify the patch is applied on the UO container:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "grep -n 'IWearableDurability talisman' /opt/uo/modernuo/Projects/UOContent/Items/Weapons/BaseWeapon.cs"
```

Expected output:

```text
        if (defender.FindItemOnLayer(Layer.Talisman) is IWearableDurability talisman)
```

Apply or reapply the patch from the repository root:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\patches\BaseWeapon-talisman-durability.patch `
  boblin@10.7.4.120:/tmp/BaseWeapon-talisman-durability.patch

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo patch -p1 -d /opt/uo/modernuo < /tmp/BaseWeapon-talisman-durability.patch"
```

After applying, rebuild and restart:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet publish Projects/Application/Application.csproj -c Build -r linux-x64 --self-contained=false"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

### Skill gain acceleration

ClusterF uses a small wrapper around ModernUO's `Mobile.SkillCheck*Handler` delegates. The wrapper keeps the upstream `SkillCheck` implementation intact, then adds player-only acceleration around the normal skill checks.

Current implementation:

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFSkillGain.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSkillGain.cs` |
| Config file | `/etc/uo/modernuo/Configuration/modernuo.json` |
| Command | `[ClusterFSkillGain]` |
| Enabled setting | `clusterf.skillGain.enabled=True` |
| Chance attempts | `clusterf.skillGain.chanceAttempts=3` |
| Gain amount multiplier | `clusterf.skillGain.amountMultiplier=5` |
| Latest pre-change backup | `/tank/clusterf-artifacts/uo/modernuo/backups/uo-modernuo-pre-skillgain-full-20260507T171031Z.tar.zst` |

Behavior:

- applies only to player mobiles
- leaves pets and NPC skill gains at ModernUO defaults
- keeps skill locks, skill caps, total caps, anti-macro checks, and challenge checks in the normal ModernUO path
- stacks with New Haven and accelerated-skill bonuses because the wrapper calls the normal `SkillCheck.Gain` path
- returns the first normal skill-check result so crafting, combat, and spell success behavior does not get multiplied

After changing `ClusterFSkillGain.cs`, copy it into the ModernUO source tree and rebuild the content assembly:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFSkillGain.cs `
  boblin@10.7.4.120:/tmp/ClusterFSkillGain.cs

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo install -o uo -g uo -m 0644 /tmp/ClusterFSkillGain.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSkillGain.cs"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo -u uo -H bash -lc 'cd /opt/uo/modernuo && dotnet build Projects/UOContent/UOContent.csproj -c Release -r linux-x64 --no-restore'"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

After changing `ClusterFSkillCaps.cs`, copy it into the ModernUO source tree and rebuild the content assembly:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFSkillCaps.cs `
  boblin@10.7.4.120:/tmp/ClusterFSkillCaps.cs

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo install -o uo -g uo -m 0644 /tmp/ClusterFSkillCaps.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSkillCaps.cs"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo -u uo -H bash -lc 'cd /opt/uo/modernuo && dotnet build Projects/UOContent/UOContent.csproj -c Release -r linux-x64 --no-restore'"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin `
  boblin@10.7.4.120 `
  "sudo systemctl restart uo.service"
```

Create a manual backup before changing gameplay code or cap values.

Future custom-content and balance areas:

- skill-cap tuning
- total skill-cap tuning
- stat-cap tuning
- skill gain tuning by skill bracket
- powerscroll handling or replacement
- combat scaling
- spell scaling
- crafting progression
- equipment scaling
- loot progression
- AI difficulty
- champion and boss encounters
- endgame PvE
- custom world content

Likely implementation investigation areas for Codex:

```text
SkillInfo
Skills
PlayerMobile
Mobile
skill gain logic
skill cap checks
total skill cap checks
PowerScroll
```

Guiding design note:

```text
ClusterF is moving away from traditional 100/120 OSI-era power ceilings toward a persistent long-term progression model. Stability, backups, and service reliability come before deep gameplay rebalance work.
```

## Pet Mimic

The Pet Mimic is a custom blessed item worn in the talisman slot. It starts as a dormant blue pouch, eats equippable items to absorb their stats and take their form, and decays in combat requiring active healing investment. Through eating and gem-feeding it eventually regens fast enough to be self-sustaining.

| Item | Value |
| --- | --- |
| Repository source (item) | `containers/uo/customizations/PetMimic.cs` |
| Repository source (phantom weapon) | `containers/uo/customizations/PetMimicWeapon.cs` |
| Repository source (status gump) | `containers/uo/customizations/PetMimicStatusGump.cs` |
| Repository source (feed gump) | `containers/uo/customizations/PetMimicFeedGump.cs` |
| Repository source (form journal) | `containers/uo/customizations/PetMimicFormJournalGump.cs` |
| Repository source (purge potion) | `containers/uo/customizations/PetMimicPurgePotion.cs` |
| Repository source (settings) | `containers/uo/customizations/ClusterFPetMimicSettings.cs` |
| Repository source (migrations) | `containers/uo/customizations/migrations/Server.Items.PetMimic.v0.json` |
| Repository source (migrations) | `containers/uo/customizations/migrations/Server.Items.PetMimic.v1.json` |
| Repository source (migrations) | `containers/uo/customizations/migrations/Server.Items.PetMimic.v2.json` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/PetMimic.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicWeapon.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicStatusGump.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicFeedGump.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicFormJournalGump.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicPurgePotion.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFPetMimicSettings.cs` |
| Installed migration | `/opt/uo/modernuo/Projects/UOContent/Migrations/Server.Items.PetMimic.v0.json` |
| Installed migration | `/opt/uo/modernuo/Projects/UOContent/Migrations/Server.Items.PetMimic.v1.json` |
| Installed migration | `/opt/uo/modernuo/Projects/UOContent/Migrations/Server.Items.PetMimic.v2.json` |
| Spawn command | `[add PetMimic` |
| Purge potion spawn | `[add PetMimicPurgePotion` |
| Dormant art | Item ID `0xE79` (vanilla pouch) tinted hue `1154` — visible without client changes |
| Client art (Phase 6) | Custom art injected at a reserved item ID via ClassicUO patcher |

### HP system

| Property | Default | Config key |
| --- | --- | --- |
| Base max HP | `10` | `clusterf.petMimic.baseMaxHP` |
| Base regen rate | `1` min/HP (lazy, scaled by progression) | `clusterf.petMimic.regenMinutesPerHP` |
| Min regen rate (fastest) | `1.0` min/HP | `clusterf.petMimic.minRegenMinutesPerHP` |
| Decay per combat hit | `1` HP | `clusterf.petMimic.decayHPPerHit` |
| Meal regen scale | `0.15` per item eaten | `clusterf.petMimic.mealRegenScale` |
| Gem regen scale | `0.02` per BonusMaxHP point | `clusterf.petMimic.gemRegenScale` |

- Gems permanently increase `MaxHP` and contribute to faster regen
- Potions and bandages restore missing current HP
- Decay fires on every combat hit to the wearer (same trigger as armor durability) — only while equipped
- Regen is passive and scales: `baseRate / (1 + meals × mealScale + bonusMaxHP × gemScale)`, floored at `minRegenMinutesPerHP`
- At high progression (many items eaten + significant gem investment) regen outpaces decay — self-sustaining
- At HP 0 the mimic unequips itself into the backpack and cannot be re-equipped until healed
- Double-click the mimic in your pack to equip it; drag to paperdoll talisman slot also works

### Healing values (all configurable in `modernuo.json`)

| Source | Effect | Config key |
| --- | --- | --- |
| Citrine | +5 MaxHP | `clusterf.petMimic.gemHeal.citrine` |
| Amber | +5 MaxHP | `clusterf.petMimic.gemHeal.amber` |
| Tourmaline | +7 MaxHP | `clusterf.petMimic.gemHeal.tourmaline` |
| Amethyst | +10 MaxHP | `clusterf.petMimic.gemHeal.amethyst` |
| Sapphire | +15 MaxHP | `clusterf.petMimic.gemHeal.sapphire` |
| Star Sapphire | +20 MaxHP | `clusterf.petMimic.gemHeal.starSapphire` |
| Ruby | +20 MaxHP | `clusterf.petMimic.gemHeal.ruby` |
| Emerald | +25 MaxHP | `clusterf.petMimic.gemHeal.emerald` |
| Diamond | +50 MaxHP | `clusterf.petMimic.gemHeal.diamond` |
| Lesser Heal Potion | +5 current HP | `clusterf.petMimic.potionHeal.lesser` |
| Heal Potion | +10 current HP | `clusterf.petMimic.potionHeal.normal` |
| Greater Heal Potion | +20 current HP | `clusterf.petMimic.potionHeal.greater` |
| Bandage | +5 current HP | `clusterf.petMimic.bandageHeal` |

### Phase 2: Equipment eating and stat accumulation

Phase 2 adds category-locked equipment eating and stat accumulation.

- Feed the mimic a weapon or armor piece (shields included with armor)
- A confirmation gump (`PetMimicFeedGump`) shows: item name, detected category and super-category, stats to be absorbed, destruction warning
- On confirm: item is destroyed, stats absorbed, mimic takes the item's appearance
- First eaten item binds the mimic to a super-category permanently — **Weapons** or **Armor** — not an exact type
  - Once bound to Weapons: any weapon type (Swords, Macing, Fencing, Archery, Wrestling) can be eaten
  - Once bound to Armor: any armor piece or shield can be eaten
  - Jewelry, clothing, and tools are not accepted
- Talismans can always be eaten regardless of category binding — talisman stat absorption does not change the mimic's form, category, or appearance (see Phase 5)
- Stats accumulate across every eaten item regardless of active form
- Accumulated stats are granted to the wearer when the mimic is equipped:
  - Str/Dex/Int bonuses via `AosAttributes.AddStatBonuses`
  - LRC/FC/FCR/DI via `IAosItem` + `AOS.cs` `GetValue` (requires `AOS-pet-mimic-attribute-aggregation.patch`)
  - Resistances via property overrides picked up by `Mobile.ComputeResistances`
  - Skill bonuses via named `DefaultSkillMod` objects (supports more than 5 unique skills)
- All stat accumulation caps are configurable in `modernuo.json` via `ClusterFPetMimicSettings`
- Status gump updated to display all accumulated stats: attributes, magic, resistances, skill bonuses

### Feeding mechanics

- Left-click the mimic → **Eat** → targeting cursor → click any item
- Double-click a bandage → target the mimic directly (requires `Bandage-pet-mimic-targeting.patch`)
- Gems permanently grow `MaxHP`; potions and bandages restore current HP
- Weapons and armor trigger the feed confirmation gump (category-binding rules apply)
- Talismans always trigger the feed gump regardless of category binding — absorb stats only, no form or category change
- Dragging items onto the mimic is intentionally disabled

### Phase 3: Form journal and switching

- Right-click mimic → **Talk** → **Forms (N)** button appears once forms exist
- Opens a searchable, paginated journal listing every known form as `Display Name / Base Type`
- Clicking a row switches the mimic's appearance to that form — accumulated stats are unchanged
- Display name is the item's custom name at eat-time; base type is from `TileData` (e.g. "Kryss")
- Existing mimics migrated from Phase 2 have names synthesized from `TileData` on first load

### Phase 4: Combat decay and scaled regen

- Decay fires via `IWearableDurability.OnHit` — same path as armor durability
- `BaseWeapon-talisman-durability.patch` adds the talisman slot to `AbsorbDamageAOS` so the mimic receives the same `OnHit` call as randomly selected armor pieces
- Regen scales with meals eaten and gem HP, converging toward self-sustaining at high progression
- All decay and regen rates are configurable in `modernuo.json` without a rebuild

### Phase 5: Purge potion, weapon forms, talisman eating, and category restriction

**Purge Potion (`PetMimicPurgePotion`)**

- Double-click → target your equipped mimic → confirmation gump warns what is destroyed vs preserved
- On confirm: consumes the potion and calls `Purge()` on the mimic
- `Purge()` zeroes all accumulated stats, resistances, and skill bonuses; detaches the phantom weapon; resets category to `None` and appearance to the dormant pouch
- Preserved through purge: `BonusMaxHP` (gem investment), `CurrentHP`, the form journal (cosmetic collection)
- Vomit scatter effect on use: 6–9 category-matched Static junk items (acid-green hues, 8 s lifetime) + 3–5 `Acid` dungeon puddles (10 s lifetime) scattered within a 3-tile radius; random puke sound (`0x32D` or `0x43F`)
- After a purge the mimic is dormant again and can be re-bound to either Weapons or Armor from scratch

**Phantom weapon (`PetMimicWeapon`)**

When bound to a weapon category the mimic spawns a `PetMimicWeapon` (a non-interactable `BaseWeapon` subclass, `Movable=false`, `Blessed`) in the correct weapon layer:

- `Layer.TwoHanded` for Archery; `Layer.OneHanded` for all other weapon types
- Correct `DefType`, `DefSkill`, `DefAnimation`, `DefHitSound`, `DefMissSound` per weapon sub-category
- The phantom weapon is what routes the correct combat skill (Swords, Macing, Fencing, Archery) — without it the engine defaulted to Wrestling
- Players cannot equip other weapons while the phantom weapon is present
- The phantom weapon graphic syncs to the mimic's active form whenever `SwitchForm` is called
- Serializes its own `MimicSerial` so it re-links to its owning mimic on world load
- Orphaned phantom weapons (mimic deleted) self-delete on `AfterDeserialization`
- `PetMimicWeapon.Configure()` registers a world-load scanner that creates missing phantom weapons for any weapon-bound mimic deployed before this feature shipped

**Talisman eating**

- Talismans can always be fed to the mimic regardless of super-category binding
- Bypasses `TryEat`'s category check entirely
- On confirm: absorbs `AosAttributes` and `AosSkillBonuses` from the talisman; does not change `LockedCategory`, `ActiveFormItemID`, or appearance
- Feed gump shows `"Type: Talisman"` and an explanatory note instead of category lock messaging

**Weapons / Armor two-track restriction**

- Two super-categories: **Weapons** (Swordsmanship, Macing, Fencing, Archery, Wrestling) and **Armor** (Armor, Shield)
- Items outside these super-categories (jewelry, clothing, tools) return `MimicCategory.None` and are silently rejected
- Compatibility check uses `AreCompatibleCategories` — a mace-locked mimic can eat a kryss; an armor-locked mimic can eat a shield
- Rejection message names the super-category: *"Your pet mimic is bound to Weapons and cannot eat Armor items. Use a purge potion to reset it."*
- Tooltip shows both super-category and specific first form: `"Type: Weapons (WeaponSwordsmanship)"`

### Left-click menu

| Entry | Clue ID | Action |
| --- | --- | --- |
| Eat | 6135 | Opens targeting cursor to feed an item |
| Talk | 6146 | Opens the status gump (HP bar, state, category, forms, accumulated stats) |

### Serialization notes

The mimic uses `[SerializationGenerator(3, false)]` (22 serialized properties). Both migration JSON files must be present in `/opt/uo/modernuo/Projects/UOContent/Migrations/` on any fresh deploy. Without them the source generator cannot find `V0Content`/`V1Content` and the build will fail.

| Migration file | Describes schema version | Purpose |
| --- | --- | --- |
| `Server.Items.PetMimic.v0.json` | v0 (2 properties: CurrentHP, LastRegenTicks) | v0 → v1 upgrade path |
| `Server.Items.PetMimic.v1.json` | v1 (3 properties: + BonusMaxHP) | v1 → v2 upgrade path |
| `Server.Items.PetMimic.v2.json` | v2 (20 properties: Phase 2 full schema) | v2 → v3 upgrade path (adds KnownFormNames, KnownFormBaseNames) |

### Planned phases

| Phase | Description |
| --- | --- |
| 1 ✓ | Base item, HP system, gem/potion/bandage feeding, status gump |
| 2 ✓ | Equipment eating, category lock, stat accumulation, confirmation gump, stat grants to wearer |
| 3 ✓ | Form journal gump with search, form switching UI |
| 4 ✓ | Combat decay via `IWearableDurability.OnHit`; regen scales with meals eaten + gem HP |
| 5 ✓ | Purge Potion — wipe stats and reset super-category binding; weapon forms via phantom weapon; talisman stat-only eating; Weapons/Armor two-track restriction |
| 6 | ClassicUO client patcher — inject custom mimic-pouch art at a reserved item ID |

## Phase 1: Custom Progression Framework

Phase 1 establishes the account-wide data backbone and communication tools that all later gameplay systems depend on.

### Account Data Persistence

`ClusterFAccountData.cs` is the account-wide progression bag for Shattered Legacy. One record exists per account, keyed by account username. All data is saved into the world save via the `ClusterFAccountPersistence` singleton Item.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFAccountData.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFAccountData.cs` |

Fields stored per account:

| Field | Type | Purpose |
| --- | --- | --- |
| `Renown` | `int` | Spendable achievement currency |
| `AchievementPoints` | `int` | Permanent prestige score (not spendable) |
| `GuildReputation` | `Dictionary<string,int>` | Reputation per guild key (e.g. `"mining"`, `"smithing"`) |
| `GuildCurrency` | `Dictionary<string,int>` | Scrip earned per guild |
| `RestorationRegistry` | `HashSet<string>` | Unlocked legacy item keys (e.g. `"legacy.jacobs_pickaxe"`) |
| `LastSeenBulletinId` | `int` | Highest bulletin ID seen on login (used by bulletin system) |

Access the data anywhere in server code:

```csharp
// Get or create (safe for login hooks, command handlers, etc.)
var data = ClusterFAccountPersistence.GetOrCreate(mobile.Account);

// Read-only check — returns null if no record yet
var data = ClusterFAccountPersistence.Get(mobile.Account);
```

**Important pattern — singleton Item creation:**

`Configure()` must NOT create the persistence Item (DecayScheduler is not yet initialized at that point). All singleton persistence Items use `EventSink.WorldLoad` instead:

```csharp
public static void Configure()
{
    EventSink.WorldLoad += EnsureExistence;
}
private static void EnsureExistence()
{
    _instance ??= new ClusterFAccountPersistence();
}
```

This is the required pattern for all `ClusterFBulletinPersistence` and any future persistence Items as well.

Deploy or update:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFAccountData.cs `
  boblin@10.7.4.120:/tmp/ClusterFAccountData.cs

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo install -o uo -g uo -m 0644 /tmp/ClusterFAccountData.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFAccountData.cs"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release && sudo systemctl restart uo.service"
```

### League Dispatch Bulletin System

`ClusterFBulletin.cs` is the MOTD / League Dispatch system. Bulletins are global records. On login, any bulletins the account has not yet seen are displayed in a styled gump. Dismissing or closing the gump marks all current bulletins as seen for that account.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFBulletin.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFBulletin.cs` |
| Bulletin file | `/etc/uo/modernuo/Configuration/bulletins.txt` |

**Admin commands:**

```text
[ClusterFBulletin add <category> <message words...>
[ClusterFBulletin list
[ClusterFBulletin remove <id>
[ClusterFBulletin load
```

**Category names:** `critical`, `dispatch`, `guild`, `event`, `personal`

**File-based authoring (recommended for multi-line or long messages):**

Edit `/etc/uo/modernuo/Configuration/bulletins.txt` over SSH:

```text
# Shattered Legacy — League Dispatch bulletin file
# One bulletin per line: category|message text
# Lines starting with # are comments and are ignored.
# Run [ClusterFBulletin load in-game after editing.

dispatch|Welcome to Shattered Legacy! The shard is live.
critical|Server maintenance Saturday at 10pm EST. Save your progress before then.
event|Old Haven has been reclaimed by the undead. Seek glory — and resistance — in the ruins.
```

Then publish in-game:

```text
[ClusterFBulletin load
```

`load` is duplicate-safe — existing bulletins with the same message text are skipped.

**Bulletin gump behavior:**

- Displays up to 7 unread bulletins. If more than 7 are unread, a count note is shown.
- Category labels are colour-coded by hue (Critical = red, Dispatch = gold, Guild = blue, Event = green, Personal = lavender).
- Message text is rendered in light grey (`#DDDDDD`) for legibility on the dark gump background.
- Any response (including closing the window) marks all current bulletins seen for the account.
- Login hook fires with a 2-second delay so the player has fully entered the world before the gump opens.

Deploy or update:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFBulletin.cs `
  boblin@10.7.4.120:/tmp/ClusterFBulletin.cs

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo install -o uo -g uo -m 0644 /tmp/ClusterFBulletin.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFBulletin.cs"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release && sudo systemctl restart uo.service"
```

## Phase 1: Achievement Framework

`ClusterFAchievements.cs` is the Phase 1 achievement framework for Shattered Legacy. Achievements are defined in code, earned state and counters are serialized by `ClusterFAchievementPersistence`, and AP/Renown rewards are applied directly to `ClusterFAccountData`.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFAchievements.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFAchievements.cs` |
| Player command | `[achievements` |
| Admin command | `[ClusterFAchievement grant\|revoke\|info\|list [args]` |

### Achievement categories

| Category | Description |
| --- | --- |
| `Combat` | Kill milestones and named-enemy achievements |
| `Skills` | Skill value threshold achievements |
| `Exploration` | Shard arrival and zone discovery |
| `Mining` | Mining and ore-related milestones |
| `Crafting` | Crafting profession milestones |
| `Legacy` | Legacy item restoration and upgrade milestones |
| `Discovery` | Landmark and secret discovery |
| `Collection` | Collection and gathering milestones |

### Initial achievement definitions (Phase 1)

| Key | Title | Category | AP | Renown | Trigger |
| --- | --- | --- | --- | --- | --- |
| `exploration.citizen` | Citizen of Shattered Legacy | Exploration | 10 | 20 | First login |
| `combat.first_blood` | First Blood | Combat | 5 | 5 | Kill 1 creature |
| `combat.century` | Century Slayer | Combat | 15 | 25 | Kill 100 creatures |
| `combat.thousand` | Veteran of the Hunt | Combat | 35 | 75 | Kill 1,000 creatures |
| `combat.ten_thousand` | Champion of Britannia | Combat | 100 | 250 | Kill 10,000 creatures |
| `combat.oldhaven_mage` | Mage Hunter | Combat | 20 | 50 | Kill 25 Old Haven Mages |
| `combat.drelgor` | The Impaler Falls | Combat | 25 | 100 | Kill Drelgor the Impaler |
| `skills.apprentice` | The Apprentice's Mark | Skills | 5 | 10 | Any skill reaches 50 |
| `skills.journeyman` | Journeyman's Path | Skills | 15 | 35 | Any skill reaches 100 |
| `skills.master` | Master of the Craft | Skills | 40 | 100 | Any skill reaches 200 |
| `skills.grandmaster` | Grandmaster | Skills | 100 | 300 | Any skill reaches 300 |
| `discovery.old_haven` | Whispers of Old Haven | Discovery | 15 | 35 | Visit Old Haven ruins |

### Event hook status

| Hook | Status | Notes |
| --- | --- | --- |
| Login / first-ever login | Wired via `[OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]` | Works immediately |
| Skill scan on login | Wired via login hook | Catches all skill thresholds retroactively |
| Creature kills | Wired via `[OnEvent(nameof(BaseCreature.CreatureDeathEvent))]` | `bc.LastKiller` gives the killer; only PlayerMobile killers count |
| Old Haven Mage kills | Wired via kill hook | Checks `victim is OldHavenMage` |
| Drelgor the Impaler kills | Wired via kill hook | Checks `victim is DrelgorTheImpaler` |
| Old Haven visit | API-only (`NotifyOldHavenVisit`) | Wire to a zone/movement hook when location tracking is added |

**Kill hook:** `BaseCreature.CreatureDeathEvent` is a ModernUO code-generated event that fires when any `BaseCreature` dies. `bc.LastKiller` provides the killing mobile. The `NotifyKill(pm, victim)` static method is also available for direct calls from other systems (e.g. custom creature subclasses).

### Admin commands

```text
[ClusterFAchievement grant <key> [username]   — grant an achievement (default: self)
[ClusterFAchievement revoke <key> [username]  — revoke an achievement
[ClusterFAchievement info <key>               — show definition and rewards
[ClusterFAchievement list                     — list all defined achievements
```

### Player command

```text
[achievements
```

Opens the League of Extraordinary Citizens achievement records gump.

**Gump features:**
- Header shows the player's current AP and Renown totals.
- Tab strip: `All | Combat | Skills | Explore | Mining | Craft | Legacy | Discov | Quests`
- Earned achievements (gold) are listed first; locked achievements (grey) follow with a separator.
- Achievements with progress counters (kill milestones) show a block-character progress bar (`█████░░░░░ 47/100`).
- When a new achievement is earned, a small floating popup gump appears top-right with the title, description, and AP/Renown reward.
- **Quests tab**: Lists all 38 New Haven ML trainer quests with completion status (`[✓]` / `[ ]`), grouped under a header. Completion count shown at the bottom.

### Extending the framework

Add a new achievement by calling `Reg(...)` inside `RegisterAll()`:

```csharp
Reg("mining.first_ore",
    "Mother Lode",
    "Mine your first ore.",
    AchievementCategory.Mining, ap: 5, renown: 10);
```

Then wire the trigger by calling `TryGrant(acct, "mining.first_ore")` from the appropriate event path, or add a `NotifyXxx` static method following the existing pattern.

Deploy or update:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\customizations\ClusterFAchievements.cs `
  boblin@10.7.4.120:/tmp/ClusterFAchievements.cs

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "sudo install -o uo -g uo -m 0644 /tmp/ClusterFAchievements.cs /opt/uo/modernuo/Projects/UOContent/Misc/ClusterFAchievements.cs"

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 `
  "cd /opt/uo/modernuo && sudo dotnet build Projects/UOContent/UOContent.csproj -c Release && sudo systemctl restart uo.service"
```

## Developer Reset Tools

`ClusterFDevTools.cs` provides admin-only tools for resetting character and account state during development.

| Item | Value |
| --- | --- |
| Repository source | `containers/uo/customizations/ClusterFDevTools.cs` |
| Installed source | `/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFDevTools.cs` |

### Commands

```text
[ClusterFReset [username]   — opens the selective reset gump for the account (default: self)
[ClusterFDeleteChar         — target a character to force-delete (bypasses the 7-day wait)
```

### Reset gump

`[ClusterFReset` opens a checkbox gump with six independent reset options:

| Option | What it resets |
| --- | --- |
| **Skills** | All skill values to `0.0` |
| **Stats** | Str/Dex/Int to `10`, Hits/Stam/Mana topped off |
| **Achievements** | All earned achievement keys, kill/event counters, and AP |
| **Account Data** | Renown, guild reputation, guild currency, restoration registry |
| **Bulletins** | Last-seen bulletin ID reset to `0` (all bulletins show again on next login) |
| **Quest History** | All ML Quest completion history (`MLQuestSystem.Contexts.Remove(pm)`) |

Buttons: **Select All** / **Clear All** toggle all checkboxes. **Reset** opens a confirmation gump listing what will be cleared before anything is touched. **Cancel** closes without action.

Note: Options that require an online `PlayerMobile` (Skills, Stats, Quests) are silently skipped for offline accounts.

## Phase 1: Guild System

`ClusterFGuildSystem.cs` implements the Phase 1 guild membership system. Membership is account-wide and stored in `ClusterFAccountData.JoinedGuilds` (a `HashSet<string>` of guild keys).

### Guild definitions

| Key | Guild Name | Guildmaster NPC | Task |
| --- | --- | --- | --- |
| `smithing` | Smiths' Fellowship | `BlacksmithGuildmaster` | Blacksmithing ≥ 50.0 OR 10 Iron Ingots |
| `arcane` | Arcane Society | `MageGuildmaster` | Magery ≥ 50.0 OR 5 Blank Scrolls |
| `tailoring` | Tailors' Circle | `TailorGuildmaster` | Tailoring ≥ 50.0 OR 10 Cloth |
| `tinkers` | Tinkers' Union | `TinkerGuildmaster` | Tinkering ≥ 50.0 OR 5 Gears |
| `healers` | Healers' Covenant | `HealerGuildmaster` | Healing ≥ 50.0 OR 25 Bandages |
| `bards` | Bards' Consortium | `BardGuildmaster` | Musicianship ≥ 30.0 (skill only) |
| `thieves` | Thieves' Den | `ThiefGuildmaster` | Stealing ≥ 20.0 (skill only) |
| `warriors` | Warriors' Brotherhood | `WarriorGuildmaster` | Tactics ≥ 30.0 (skill only) |
| `mining` | Miners' Compact | `MinerGuildmaster` | Mining ≥ 50.0 OR 20 Iron Ore |
| `rangers` | Rangers' League | `RangerGuildmaster` | Archery ≥ 30.0 OR 25 Arrows |
| `maritime` | Maritime Brotherhood | `FisherGuildmaster` | Fishing ≥ 30.0 OR 5 Fish |
| `merchants` | Merchants' Exchange | `MerchantGuildmaster` | 500 Gold (item only) |

All guilds award **25 reputation** and **10 scrip** on join. Accounts can join any combination of guilds — there is no exclusivity.

### Join flow

1. Right-click a guildmaster NPC → **Guild Membership** (cliloc 6133; verify in-game)
2. `GuildTaskDetailGump` opens showing the guild pitch, initiation task, live skill/item progress, and rewards.
3. When requirements are met a **[Join Guild]** button appears.
4. Clicking it calls `ClusterFGuildSystem.Join()` — items are consumed if the skill threshold was not met, membership is added, reputation and scrip are credited.
5. Gump returns to `GuildProgressGump` showing updated status.

### Player command

`[guild` — opens `GuildProgressGump`, an overview of all 12 guilds with Member/Not Joined status and a **[View]** button per guild.

The `[achievements` gump footer also has a **Guilds** button (btn 200) that opens `GuildProgressGump` directly.

### Context menu extension

`ClusterFGuildmasterExtension.cs` is a `partial class BaseGuildmaster` extension (in `namespace Server.Mobiles`) that overrides `AddCustomContextEntries`. It adds `GuildMembershipEntry` to the context menu for any guildmaster whose type is registered in `ClusterFGuildSystem._byNpc`. No vanilla files are modified.

### Files

| File | Purpose |
| --- | --- |
| `customizations/ClusterFGuildSystem.cs` | Guild definitions, `ClusterFGuildSystem` static class, `GuildMembershipEntry`, `GuildProgressGump`, `GuildTaskDetailGump` |
| `customizations/ClusterFGuildmasterExtension.cs` | `partial BaseGuildmaster` — context menu hook |
| `customizations/ClusterFAccountData.cs` | Added `JoinedGuilds HashSet<string>` (serialization version bumped 0→1) |

## UO Service Health Check

A lightweight Python HTTP service runs on the UO container and reports the game service state over HTTP for monitoring tools (Homepage dashboard, Uptime Kuma, etc.).

| Item | Value |
| --- | --- |
| Repository source (script) | `containers/uo/scripts/uo-healthcheck` |
| Repository source (unit) | `containers/uo/scripts/uo-healthcheck.service` |
| Installed script | `/usr/local/bin/uo-healthcheck` |
| Installed unit | `/etc/systemd/system/uo-healthcheck.service` |
| Listen port | `12001` |
| Response: game service active | HTTP `200 OK` — body `OK` |
| Response: game service inactive | HTTP `503 Service Unavailable` — body `SERVICE UNAVAILABLE` |

The check runs `systemctl is-active uo.service` on every request. It distinguishes "container reachable" (ICMP ping) from "game service actually running", which ICMP cannot.

The service runs as root (required for systemctl query) and restarts automatically on failure.

Verify the health check:

```powershell
# From Windows workstation
Invoke-WebRequest -Uri http://10.7.4.120:12001 -Method GET
```

Deploy or update:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\scripts\uo-healthcheck `
  boblin@10.7.4.120:/tmp/uo-healthcheck

scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\uo\scripts\uo-healthcheck.service `
  boblin@10.7.4.120:/tmp/uo-healthcheck.service

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 "sudo bash -s" <<'EOF'
install -o root -g root -m 0755 /tmp/uo-healthcheck /usr/local/bin/uo-healthcheck
install -o root -g root -m 0644 /tmp/uo-healthcheck.service /etc/systemd/system/uo-healthcheck.service
systemctl daemon-reload
systemctl enable --now uo-healthcheck.service
EOF
```

## Ports

ModernUO defaults to TCP `2593` for the game listener. It can also expose a ping/status listener; confirm the final port after first-run configuration.

Initial exposure should be LAN/VPN only.

## Service Management

Preferred management model:

```bash
systemctl start uo
systemctl stop uo
systemctl status uo
journalctl -u uo
```

Use `screen` or `tmux` only as a temporary troubleshooting tool. Long-running service management uses systemd.

The installed unit is:

```text
/etc/systemd/system/uo.service
```

Repository source:

```text
containers/uo/uo.service.example
```

The unit runs ModernUO as `uo`, uses `/opt/uo/modernuo/Distribution` as the working directory, and sends `SIGINT` on stop so ModernUO follows its clean console-cancel shutdown path.

Current verification:

```bash
systemctl is-active uo.service
ss -ltnp | grep 2593
```

Expected listener:

```text
10.7.4.120:2593
```

## Key Paths

Baseline paths:

```text
/opt/uo
/opt/uo/modernuo
/opt/uo/modernuo/Distribution
/etc/uo/modernuo.env
/etc/uo/modernuo/Configuration
/var/lib/uo/client-data/classic-client
/var/lib/uo/modernuo/Saves
/var/lib/uo/modernuo/Backups
/var/lib/uo/modernuo/Archives
/var/log/uo/modernuo
/etc/systemd/system/uo.service
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSkillCaps.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFStatCaps.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSkillGain.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFNewHavenSeeder.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSouthMineDecor.cs
/opt/uo/modernuo/Projects/UOContent/Mobiles/PlayerMobile.cs (engine patch — see containers/uo/patches/)
/opt/uo/modernuo/Projects/UOContent/Mobiles/Healers/Healer.cs (engine patch — see containers/uo/patches/)
/opt/uo/modernuo/Projects/UOContent/Mobiles/Vendors/NPC/Guildmasters/HealerGuildmaster.cs (engine patch — see containers/uo/patches/)
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFHealerPolicy.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFSelfResurrect.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFStatInspect.cs
/opt/uo/modernuo/Projects/UOContent/Misc/PetMimic.cs
/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicWeapon.cs
/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicStatusGump.cs
/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicFeedGump.cs
/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicFormJournalGump.cs
/opt/uo/modernuo/Projects/UOContent/Misc/PetMimicPurgePotion.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFPetMimicSettings.cs
/opt/uo/modernuo/Projects/UOContent/Migrations/Server.Items.PetMimic.v0.json
/opt/uo/modernuo/Projects/UOContent/Migrations/Server.Items.PetMimic.v1.json
/opt/uo/modernuo/Projects/UOContent/Misc/AOS.cs (engine patch — see containers/uo/patches/)
/opt/uo/modernuo/Projects/UOContent/Items/Skill Items/Misc/Bandage.cs (engine patch — see containers/uo/patches/)
/opt/uo/modernuo/Projects/UOContent/Items/Weapons/BaseWeapon.cs (engine patch — see containers/uo/patches/)
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFAccountData.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFBulletin.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFAchievements.cs
/opt/uo/modernuo/Projects/UOContent/Misc/LeagueRegistrar.cs
/opt/uo/modernuo/Projects/UOContent/Misc/LeagueRegistrarGump.cs
/opt/uo/modernuo/Projects/UOContent/Misc/MinersCompactLiaison.cs
/opt/uo/modernuo/Projects/UOContent/Misc/MinersCompactLiaisonGump.cs
/opt/uo/modernuo/Projects/UOContent/Misc/JacobsReinforcedPickaxe.cs
/opt/uo/modernuo/Projects/UOContent/Items/New Haven Quest Rewards/JacobsPickaxe.cs (patch — see containers/uo/patches/)
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFInstitutionSeeder.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFGuildSystem.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFLeagueSystem.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFRestorationRegistry.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFExtendedOres.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFMiningExtension.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFCompactAdminTools.cs
/opt/uo/modernuo/Projects/UOContent/Misc/ClusterFBulletinSystem.cs
/etc/uo/modernuo/Configuration/bulletins.txt
/usr/local/bin/uo-healthcheck
/etc/systemd/system/uo-healthcheck.service
```

Use `/opt/uo/modernuo` for emulator code, `/etc/uo` for environment and service configuration, `/var/lib/uo/client-data/classic-client` for local game data, `/var/lib/uo/modernuo` for shard state, and `/var/log/uo/modernuo` for logs.

Runtime symlinks under `/opt/uo/modernuo/Distribution`:

```text
Configuration -> /etc/uo/modernuo/Configuration
Logs          -> /var/log/uo/modernuo
Saves         -> /var/lib/uo/modernuo/Saves
Backups       -> /var/lib/uo/modernuo/Backups
Archives      -> /var/lib/uo/modernuo/Archives
```

## Artifact Storage

Enderman stores UO artifacts under:

```text
/tank/clusterf-artifacts/uo/modernuo
```

Windows upload path:

```text
\\enderman.clusterf.lab\clusterf-artifacts\uo\modernuo
```

Folders:

| Folder | Purpose |
| --- | --- |
| `client-data` | UO or ClassicUO game data files required by ModernUO |
| `builds` | Published ModernUO release archives |
| `imports` | One-time imports or migration files |
| `backups` | World/account/config backups from the UO container |
| `docs` | Local notes about uploaded artifacts |

Do not commit UO client data, generated builds, or shard backups to Git.

ModernUO requires game data files. Upload a UO or ClassicUO installation's data files to:

```text
\\enderman.clusterf.lab\clusterf-artifacts\uo\modernuo\client-data
```

Then copy or sync them into the container path before first launch:

```text
/var/lib/uo/client-data/classic-client
```

## Access

The `clusterf_boblin` key (`~/.ssh/clusterf_boblin`) is authorized for `boblin` directly on CT 101. Direct SSH and SCP from the Windows workstation work without a jump host.

Connect by IP:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120
```

Connect by hostname (once the FreeIPA DNS record is corrected):

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@uo.clusterf.lab
```

Copy files directly to the container:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin .\some\file boblin@10.7.4.120:/tmp/
```

The key was added to the container on 2026-05-09 via `pct exec` from creeper after CT 101 was found to be missing the Windows workstation key in `authorized_keys`.

## FreeIPA DNS

The container is enrolled as a FreeIPA client:

```text
host/uo.clusterf.lab@CLUSTERF.LAB
```

Verified state:

```bash
systemctl is-active sssd
klist -k /etc/krb5.keytab
getent group homelab-deployers
```

The DNS record currently requires an IPA admin correction because the first record was created for the conflicting `10.7.4.110` address. Boblin and the UO host principal can read the record but cannot modify FreeIPA DNS records.

Run from a shell with an IPA admin Kerberos ticket:

```bash
kinit admin
ipa dnsrecord-mod clusterf.lab uo --a-rec=10.7.4.120
ipa dnsrecord-show clusterf.lab uo
```

Expected result:

```text
A record: 10.7.4.120
```

## Backups

Back up:

- world data
- player data
- account data
- emulator configuration

Primary data path:

```text
/var/lib/uo
```

Before risky world-generation, decoration, spawner-import, or custom-code operations, create a manual backup:

```powershell
ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@10.7.4.120 "sudo mkdir -p /var/lib/uo/modernuo/Backups/manual && sudo tar -C /var/lib/uo/modernuo -caf /var/lib/uo/modernuo/Backups/manual/pre-change-$(date -u +%Y%m%dT%H%M%SZ).tar.zst Saves Archives Backups && sudo ls -lh /var/lib/uo/modernuo/Backups/manual | tail"
```

Latest Enderman artifact backup:

```text
/tank/clusterf-artifacts/uo/modernuo/backups/uo-modernuo-pre-newhaven-repair-full-20260507T175211Z.tar.zst
```

## TODO

- [x] Choose emulator.
- [x] Document runtime version.
- [x] Upload UO or ClassicUO client data to Enderman artifact storage.
- [x] Copy/sync client data into `/var/lib/uo/client-data/classic-client`.
- [x] Run ModernUO once to create initial config.
- [x] Run ModernUO interactively to create the owner account.
- [x] Confirm ClassicUO client can connect to `10.7.4.120:2593`.
- [x] Discover built-in ModernUO JSON spawn definitions.
- [x] Run spawner generation commands for discovered datasets.
- [x] Document that `CreateWorld` is not valid in this build.
- [x] Document Magincia decoration issue and future rebuild direction.
- [x] Back up the current playable shard state to Enderman artifact storage.
- [x] Add systemd service.
- [x] Implement 300 individual skill cap.
- [x] Set total skill cap high enough for all skills to reach 300.
- [x] Implement 500 individual base stat cap and 1500 total base stat cap.
- [x] Implement initial accelerated player skill gain.
- [x] Seed named New Haven quest NPCs.
- [x] Audit New Haven NPC locations against uoguide.com and ServUO-Lokai reference; corrected 22 misplaced NPCs.
- [x] Add forge and anvil to New Haven south mine camp.
- [x] Fix healer vendors not resurrecting players flagged as criminal or murderer.
- [x] Add `[SelfRes` player command for self-resurrection from ghost form.
- [x] Enable TerMur map and generate spawners.
- [x] Implement account-wide progression persistence (`ClusterFAccountData` + `ClusterFAccountPersistence`).
- [x] Implement League Dispatch bulletin system with in-game commands and file-based authoring.
- [x] Deploy UO service health check endpoint (port 12001, systemd service).
- [x] Deploy Homepage dashboard at `dashboard.clusterf.lab` with UO, Proxmox, FreeIPA status dots.
- [x] Phase 1: Implement achievement framework (event hooks, definitions, Renown grants, `[achievements` command).
- [x] Phase 1: Implement guild membership system — 12 guilds, context menu join flow, `[guild` command, GuildProgressGump, GuildTaskDetailGump.
- [ ] Phase 1: Implement restoration registry commands and hooks.
- [x] Phase 2: Miners' Compact vertical slice — extended ores, work orders, Jacob's Pickaxe lifecycle, Jacob's Reinforced Pickaxe, restoration, NPCs, admin tools.
- [x] Phase 2 fix pass (2026-05-14) — crafting menu root cause fix, pickaxe deletion equipped-state fix, T2 name/hue/durability fix, work order Accept→Turn-In redesign, Talk context menus, League Registrar ambient speech + proximity greeting + quest arrow, restoration safety auto-clear, `[CompactClearActive` / `[TestingReset` / `[TestingZeroSkills` admin commands.
- [x] Phase 2 fix pass 2 (2026-05-14) — NPC names (Garrett Ashveil / Elara Voss), Achievements Quests tab overflow fix (two-row tab bar), exhausted pickaxe `OnEquip` block (T1+T2), `ShowUsesRemaining` suppressed via `IUsesRemaining` interface cast when exhausted, T2 upgrade equipped-layer scan, T2 restoration path (25V+500 Iron+100 DC+12,500gp), Back button on upgrade gump + smart back-routing in Liaison gump, work order / restoration action label color fixes.
- [x] Phase 3 (2026-05-15) — Guild Work Order framework (25 orders, Initiate → Deepwarden); `GuildContractLedgerGump` (Available/Active/History + back button); `CompactOreSatchel` (50% weight, auto-routing via partial `Mining.Give()` override); `ProspectorsLogbook` (account-backed, dynamic height, no cap); `SurveyArchivist` Velara Thorne (discovery report + reward flow); discovery-gated contract eligibility (`WorkOrderDef.RequiredDiscovery`); 8 extended ore orders (Platinum → Celestial); permanent ore veins (`RandomizeVeins=false`); `ClusterFAccountData` v5; `[CompactWipeLogbook` + `[CompactSeedDiscoveries` admin commands.
- [ ] Phase 2: Activate existing in-world guildmaster NPCs before adding any new NPCs or guilds.
- [ ] Clean up authoritative DNS for `uo.clusterf.lab` after temporary UniFi DNS override.
- [ ] Decide whether game access is LAN-only or VPN-accessible.
- [ ] Add backup and restore procedure.
- [ ] Add custom content and scaling for expanded skill progression.
