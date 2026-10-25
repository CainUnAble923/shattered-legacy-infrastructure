# Our TazUO build (cc-P103)

Shattered Legacy's client is TazUO (BSD 2-Clause, `player-package\LICENSE-TazUO.txt`) at a pinned upstream commit,
plus our own small patches. This folder holds the pin and the patches, never the TazUO source.

| File | What |
|---|---|
| `TAZUO_COMMIT` | The upstream commit every patch is based on: `3212623f63436be1c0f4e3b308b202b29597c026` (v26.0909.63, the TazUO the player package ships) |
| `patches\*.patch` | Our changes, applied in file order with `git apply` (no fuzz). One change per patch, each rebasable on its own |
| `nuget.config` | nuget.org only (the build PC has no NuGet source) |
| `Build-Client.ps1` | Worktree at the pin, submodules copied and checked, patches, unit tests, `dotnet publish` to `D:\UO\tazuo-sl-build\client` |

```
powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\client\Build-Client.ps1
```

It needs a TazUO clone with its submodules (`-Source`, default `D:\UO\cc-p40-work\tazuo-3212623f`) and the .NET 10 SDK.

## Patches

| Patch | Upstream base | What | Server side |
|---|---|---|---|
| `0001-enhanced-packet-client-command.patch` | `3212623f` | Enhanced packet 0xCE id 4, **ClientCommand**: the server runs one whitelisted client command (`optlink`, `paperdoll`, `marktile`, `radius`, and three new ones, `worldmap`, `journal`, `options`, which open the World Map, the Journal and the options window). Behind TazUO's enhanced-packet opt-in; anything else is ignored and logged once. The client answers id 4 when the server's EnableEnhancedPacket asks for it, so the server can tell our client from a stock TazUO. New file `EnhancedClientCommand.cs`, 9 lines in three upstream files, 21 unit tests | `server\customizations\TazUO\TazUOClientCommand.cs`; the tutorial stations' buttons (`Tutorial\TutorialGumps.cs`) |
| `0002-paperdoll-bodies-option.patch` | `3212623f` plus 0001 | Options > Paperdoll > Shattered Legacy > **Paperdoll bodies**: SFW always; Topless and Nude only when the local add-on folder (`sl_paperdoll_folder`, else `<client>\..\client-addons\paperdoll-nsfw` or `<client>\..\..\client-addons\paperdoll-nsfw`) holds all six bodies of that set. The six body gumps are read from the add-on where it is (`ExternalImageLoader.SetGumpOverrides`) and redrawn at once (`Gump.Invalidate`); nothing is copied. New `Game\Managers\PaperdollBodies.cs`, small hooks in five upstream files, 8 unit tests | none (client-side only); the launcher's cc-P81 switch stands down for this build (`player-package\payload\app\Paperdoll-Bodies.ps1`) |
| `0003-shard-buttons-achievements.patch` | `3212623f` plus 0001 and 0002 | **Shard buttons**: an **Achievements** button in the classic paperdoll's Help slot (own paperdoll only; gumps 22456 to 22458 from `player-package\vendor\shattered-legacy-art\gumps`, Help again if they are missing), a line in the modern paperdoll's menu, and a button at the end of the top bar. Each sends 0xCE id 4 **with** the command (`achievements`) to the server; the bare id 4 stays 0001's acknowledgement. Shown only while this connection's server asked for id 4. New `Network\EnhancedPackets\ShardButtons.cs` (a table: command, label, paperdoll gumps), hooks in four upstream files, 10 unit tests | `server\customizations\TazUO\TazUOClientCommand.cs` (`ShardButtons`, `HandleShardButton`); the page is `ClusterFAchievements.cs` (`RegisterShardButton`) |
| `0004-world-map-fog-of-war.patch` | `3212623f` plus 0001 to 0003 | **Fog of war** (F-6) on the World Map: the shard's packet 0xF4 (variable length, no upstream handler) carries each facet's walked 8x8 chunks, full at login (gzip of the bit set) and as deltas while walking; every chunk not walked is drawn dark (black at 216/255), one texel per chunk, rotated and scaled with the map at every zoom and with Flip map. **Fog of war** in the World Map's right-click menu turns it off (saved in the profile, on by default). Reset at each login, so a shard that sends no 0xF4 shows no fog. Mining depletion (subs 0x03, 0x04) is ignored. New `Game\Managers\ShardFogOfWar.cs`, hooks in four upstream files, 10 unit tests | `server\customizations\ClusterFExplorationManager.cs` (also duplicated in `ClusterFExplorationSync.cs`, cc-P240) |
| `0005-ui-skins-appearance.patch` | `3212623f` plus 0001 to 0004 | **UI skins** (cc-P362): Options > Interface > **Appearance** picks a look per kind (backpack, bags, metal chests and the bank box, crates, spell scrolls), "Classic (stock)" first and the default, with a small preview. Saved per profile (`Profile.SlSkins`). A pick remaps the stock gump (or a scroll's art) to one of our PNGs under a NEW ID (`ExternalImageLoader.SetGumpRemap` and `SetArtRemap`, after 0002's override and before the ID's own PNG) and redraws it (`Gump.Invalidate`, `Art.Invalidate`); stock IDs are never overwritten, and a skin whose files are not here is not offered. The table `Game\Managers\SlSkins.cs` is `player-package\vendor\shattered-legacy-art\skins.json` (`player-package\tests\Ui-Skins.Tests.ps1` keeps them the same). New `SlSkins.cs` and `AppearanceTab.cs`, hooks in five upstream files, 6 unit tests | none (client-side only) |
| `0006-exhausted-trees.patch` | `3212623f` plus 0001 to 0005 | **Exhausted trees** (cc-P412): the shard's packet 0xF4 sub 0x05 (routed by 0004's handler) lists each chopped-out map tree: facet, X, Y, the seconds until its bank refills (0 = it regrew) and its item IDs (trunk and foliage). Those statics, and only those IDs at that tile, are not drawn and cannot be clicked (`StaticView.Draw` and `CheckMouseSelection`, matched on the original graphic) until the deadline or the regrowth message; the server's stump item stays. Empty at each login (EnterWorld), and the shard resends every live tree at login. New `Game\Managers\ShardExhaustedTrees.cs`, hooks in three upstream files and 0004's `ShardFogOfWar.cs`, 7 unit tests | `server\customizations\ClusterFLumberStumps.cs` (`ClusterFExhaustedTrees`) |
| `0007-animated-external-art.patch` | `3212623f` plus 0001 to 0006 | **Animated add-on art** (cc-P374): an animated static (tiledata Animation flag, an animdata record) whose art exists only as an `ExternalImagesrt` PNG never changed frame: AnimatedStaticsManager keeps the current frame offset on the art index entry, and the item, static and sorting views read it through `GetValidRefEntry`, which returns the shared Invalid entry (offset always 0) for an ID with no art.mul data. A new `UOFile.GetAnimOffset` reads the entry itself; the three views use it. Stock art animates as before. Found by cc-P374's in-game proof: the guild hall decor's vat, tanks, fire pit, crystals and orbs stood still. Hooks in three upstream files, 3 unit tests | none (client-side only; the art is cc-P265's) |

## Rebasing onto a newer upstream

In a TazUO clone: `git switch -c sl/rebase <new upstream commit>`, then `git am client\patches\*.patch`. Fix any
conflict, run the unit tests, `git format-patch` the result back into `patches\`, and put the new commit in `TAZUO_COMMIT`.

## Not yet

The player package still ships the stock TazUO (`Build-PlayerPackage.ps1 -TazUO D:\UO\TazUO\client`). Shipping this
build is cc-P40's P-a pipeline: version stamp `-sl.N`, a smoke start, the package gates, and our own crash webhook or none.
