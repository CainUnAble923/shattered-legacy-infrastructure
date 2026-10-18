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

## Rebasing onto a newer upstream

In a TazUO clone: `git switch -c sl/rebase <new upstream commit>`, then `git am client\patches\*.patch`. Fix any
conflict, run the unit tests, `git format-patch` the result back into `patches\`, and put the new commit in `TAZUO_COMMIT`.

## Not yet

The player package still ships the stock TazUO (`Build-PlayerPackage.ps1 -TazUO D:\UO\TazUO\client`). Shipping this
build is cc-P40's P-a pipeline: version stamp `-sl.N`, a smoke start, the package gates, and our own crash webhook or none.
