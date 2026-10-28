#Requires -Version 5.1
<#
  Build-MapOverlay.ps1 (cc-P188): the server's copy of our map changes.

      .\docker\uo\Build-MapOverlay.ps1                  build client-data\sl-map-overlay\ on the Siege of Sosaria map
      .\docker\uo\Build-MapOverlay.ps1 [-Base <folder>] [-UoData <folder>] [-Out <folder>] [-Ops <trammel.mapops>]
      .\docker\uo\Build-MapOverlay.ps1 -Stock -UoData <folder>   the old way: our patch on EA's own Trammel files

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

  cc-P675 (the Siege of Sosaria swap, S2): the base is no longer EA's Trammel but the SoS map by Zigholtul, the same
  three files the player package ships (player-package\vendor\sos-trammel-map\files\, made by Add-SosMap.ps1 and
  checked here against its manifest.csv), and trammel.mapops is re-based onto it. The applier's -BaseDir (cc-P674)
  reads base-map1legacymul.uop, base-statics1.mul and base-staidx1.mul in place of the client's own; the overlay still
  holds map1LegacyMUL.uop, statics1.mul and staidx1.mul, first in dataDirectories. Felucca reads the same three
  (server/customizations/Maps/SosMaps.cs: file index 1), so no map0 file is made.
  The reader's order: pinned's TileMatrix asks for map1.mul FIRST, in every data folder, and only then for
  map1LegacyMUL.uop (TileMatrix.cs:105-113; FindDataFile walks dataDirectories in order, Main.cs:346-372). So the
  overlay's UOP wins over the client's own map1LegacyMUL.uop because it is in the first folder, and a map1.mul in ANY
  data folder would beat both. This script refuses when the client data (-UoData) or the overlay holds one.
  -Stock keeps the old build (our patch on EA's files) for a comparison; the patch's Burrows and isle sit on void that is
  the same in both maps, so it applies there too.
#>
[CmdletBinding()]
param(
    [string]$UoData = 'D:\ShatteredLegacy\client-data\classic-client-7.0.117.0',
    [string]$Out = 'D:\ShatteredLegacy\client-data\sl-map-overlay',
    [string]$Ops = '',
    [string]$Base = '',     # cc-P675: the SoS map's base-* files (default: player-package\vendor\sos-trammel-map\files)
    [switch]$Stock          # cc-P675: our patch on EA's own Trammel (-UoData), as before the swap
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
if (-not $Ops) { $Ops = Join-Path $repo 'player-package\vendor\shattered-legacy-map\trammel.mapops' }
if (-not (Test-Path -LiteralPath $Ops)) { throw "no map patch at $Ops" }
foreach ($d in @($UoData, $Out)) {
    if ($d -and (Test-Path -LiteralPath (Join-Path $d 'map1.mul'))) {
        throw "map1.mul in ${d}: pinned reads map1.mul before map1LegacyMUL.uop from every data folder (TileMatrix.cs:105-113), so it would beat the overlay. Remove it."
    }
}
$override = @{ UoDir = $UoData; Ops = $Ops; OutDir = $Out }
if ($Stock) {
    if (-not (Test-Path -LiteralPath (Join-Path $UoData 'map1LegacyMUL.uop'))) { throw "no map1LegacyMUL.uop in $UoData (-UoData)" }
} else {
    if (-not $Base) { $Base = Join-Path $repo 'player-package\vendor\sos-trammel-map\files' }
    $manifest = Join-Path $repo 'player-package\vendor\sos-trammel-map\manifest.csv'
    $ship = @(Get-Content -LiteralPath $manifest | Where-Object { $_ -notmatch '^#' } | ConvertFrom-Csv | Where-Object { $_.kind -eq 'ship' })
    if ($ship.Count -ne 3) { throw "$manifest lists $($ship.Count) ship rows, not 3" }
    foreach ($r in $ship) {
        $f = Join-Path $Base $r.file
        if (-not (Test-Path -LiteralPath $f)) { throw "the Siege of Sosaria map: $f is missing. Run player-package\vendor\sos-trammel-map\Add-SosMap.ps1." }
        $i = Get-Item -LiteralPath $f
        if ($i.Length -ne [long]$r.bytes -or (Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash -ne $r.sha256) {
            throw "the Siege of Sosaria map: $f is not the manifest's file. Run player-package\vendor\sos-trammel-map\Add-SosMap.ps1."
        }
    }
    $override.BaseDir = $Base
}
. (Join-Path $repo 'player-package\payload\app\Apply-MapOps.ps1')
$lines = @(Build-MapOverride @override)
if ($lines.Count -ne 3) { throw "the applier returned $($lines.Count) override lines, not 3" }
Write-Host "map overlay: $Out ($(if ($Stock) { "EA's Trammel from $UoData" } else { "the Siege of Sosaria map from $Base" }))" -ForegroundColor Cyan
foreach ($l in $lines) { Write-Host "  $l" -ForegroundColor Gray }
Get-Content -LiteralPath (Join-Path $Out 'map-stamp.txt') | ForEach-Object { Write-Host "  stamp: $_" -ForegroundColor DarkGray }
