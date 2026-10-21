#Requires -Version 5.1
<#
  Build-MapOverlay.ps1 (cc-P188): the server's copy of our map changes.

      .\docker\uo\Build-MapOverlay.ps1                  build client-data\sl-map-overlay\ from the shard's client data
      .\docker\uo\Build-MapOverlay.ps1 -UoData <folder> [-Out <folder>] [-Ops <trammel.mapops>]

  The player package ships our map as a patch of our changes (player-package\vendor\shattered-legacy-map\trammel.mapops,
  the Rootdeep Burrows and the way in from Old Haven) and applies it on each player's PC. The server has to read the same
  map, and it reads map1LegacyMUL.uop, statics1.mul and staidx1.mul from its data folders (pinned Core.FindDataFile,
  Main.cs:346-372: the first folder of dataDirectories that holds the file wins). So this script applies the very same
  patch, with the very same applier the package ships (player-package\payload\app\Apply-MapOps.ps1), to copies of the
  three Trammel files from the shard's client data, into client-data\sl-map-overlay\ (git-ignored under client-data\,
  about 116 MB). docker-compose.test.yml mounts that folder read-only at /var/lib/uo/client-data/sl-map-overlay and
  Start-TestShard.ps1 puts it first in Configuration-test's dataDirectories, so the test shard reads our Trammel and
  EA's everything else. The live stack gets the same two lines when Chase chooses (notes/cc-P188-cont-rootdeep-goes-in.md,
  For Chase); nothing here touches it.

  Idempotent: the applier's stamp (map-stamp.txt in the overlay) skips the work when the EA files and the patch are
  unchanged. The shard's client data is only ever read. ASCII only, no Add-Type.
#>
[CmdletBinding()]
param(
    [string]$UoData = 'D:\ShatteredLegacy\client-data\classic-client-7.0.117.0',
    [string]$Out = 'D:\ShatteredLegacy\client-data\sl-map-overlay',
    [string]$Ops = ''
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
if (-not $Ops) { $Ops = Join-Path $repo 'player-package\vendor\shattered-legacy-map\trammel.mapops' }
if (-not (Test-Path -LiteralPath (Join-Path $UoData 'map1LegacyMUL.uop'))) { throw "no map1LegacyMUL.uop in $UoData (-UoData)" }
if (-not (Test-Path -LiteralPath $Ops)) { throw "no map patch at $Ops" }
. (Join-Path $repo 'player-package\payload\app\Apply-MapOps.ps1')
$lines = @(Build-MapOverride -UoDir $UoData -Ops $Ops -OutDir $Out)
if ($lines.Count -ne 3) { throw "the applier returned $($lines.Count) override lines, not 3" }
Write-Host "map overlay: $Out" -ForegroundColor Cyan
foreach ($l in $lines) { Write-Host "  $l" -ForegroundColor Gray }
Get-Content -LiteralPath (Join-Path $Out 'map-stamp.txt') | ForEach-Object { Write-Host "  stamp: $_" -ForegroundColor DarkGray }
