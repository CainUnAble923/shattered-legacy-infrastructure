#Requires -Version 5.1
<#
  PowerShell front end for docker/uo/test-shard.sh.

      .\docker\uo\Start-TestShard.ps1            build (gated) + bring the test shard up
      .\docker\uo\Start-TestShard.ps1 -Fresh     wipe the test save first
      .\docker\uo\Start-TestShard.ps1 -Down      stop it

  Everything happens on the THROWAWAY test shard: sl-modernuo-test, 127.0.0.1:2594,
  its own Saves-test and Configuration-test. The live container, the live world save
  and DNS are never touched.

  build.sh still does the building. It is the only thing that runs the patch check
  and the tests as gates, and reimplementing it here is exactly how a gate gets lost,
  so this script finds a bash and calls it rather than repeating it.
#>
[CmdletBinding()]
param([switch]$Fresh, [switch]$Down)

$ErrorActionPreference = 'Stop'
$repo    = (Resolve-Path "$PSScriptRoot\..\..").Path
$compose = "docker/uo/docker-compose.test.yml"
Set-Location $repo

if ($Down) {
    docker compose -f $compose down
    if ($LASTEXITCODE -ne 0) { throw "docker compose down failed ($LASTEXITCODE)" }
    Write-Host "test shard stopped." -ForegroundColor Cyan
    return
}

# --- find a bash for build.sh -------------------------------------------------
$bash = $null
foreach ($p in @(
    "$env:ProgramFiles\Git\bin\bash.exe",
    "${env:ProgramFiles(x86)}\Git\bin\bash.exe",
    "$env:LOCALAPPDATA\Programs\Git\bin\bash.exe"
)) { if (Test-Path $p) { $bash = $p; break } }

if (-not $bash) {
    $wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue
    if ($wsl) { $bash = 'wsl' }
}
if (-not $bash) {
    throw "No bash found. Install Git for Windows, or run: docker/uo/test-shard.sh from any bash."
}

# --- seed the test-only directories ------------------------------------------
$testSaves = "$repo\server\lib\uo\modernuo\Saves-test"
$testConf  = "$repo\server\uo\modernuo\Configuration-test"
$liveConf  = "$repo\server\uo\modernuo\Configuration"

if ($Fresh -and (Test-Path $testSaves)) {
    Write-Host "wiping the test save (the live save is never touched)" -ForegroundColor Yellow
    Remove-Item $testSaves -Recurse -Force
}
if (-not (Test-Path $testSaves)) { New-Item -ItemType Directory -Path $testSaves -Force | Out-Null }
# A fresh, empty world has no accounts, so ModernUO's AccountPrompt asks "create the
# owner account? (y/n)" on stdin and BLOCKS THERE FOREVER. docker ps still says Up, the
# listener never opens, and the client hangs at "verifying account". Cost a week,
# 2026-09-19. Seed the accounts so it never asks.
$liveAccts = "$repo\server\lib\uo\modernuo\Saves\Accounts"
if (-not (Test-Path "$testSaves\Accounts") -and (Test-Path $liveAccts)) {
    Write-Host "seeding test accounts from the live save (one-directional copy)" -ForegroundColor Gray
    Copy-Item $liveAccts "$testSaves\Accounts" -Recurse
}
if (-not (Test-Path $testConf)) {
    Write-Host "seeding Configuration-test from the live Configuration" -ForegroundColor Gray
    Copy-Item $liveConf $testConf -Recurse
    # The live config advertises 192.168.1.58. A client connecting to the TEST shard
    # would be relayed straight onto the LIVE shard and never know. Point it at itself.
    $mj = "$testConf\modernuo.json"
    $j  = Get-Content $mj -Raw
    $j  = $j -replace '"serverListing\.address":\s*"[^"]*"', '"serverListing.address": "127.0.0.1"'
    $j  = $j -replace '"serverListing\.serverName":\s*"[^"]*"', '"serverListing.serverName": "Shattered Legacy TEST"'
    Set-Content $mj $j -Encoding UTF8
}

# --- build through the gates --------------------------------------------------
Write-Host ""
Write-Host "== building through the gates (patches, then tests, then image) ==" -ForegroundColor Cyan
if ($bash -eq 'wsl') { & wsl bash docker/uo/build.sh }
else                 { & $bash -lc "cd '$($repo -replace '\\','/')' && docker/uo/build.sh" }
if ($LASTEXITCODE -ne 0) { throw "build.sh failed ($LASTEXITCODE). Nothing was started." }

# --- start the throwaway shard ------------------------------------------------
Write-Host ""
Write-Host "== starting the throwaway test shard on 127.0.0.1:2594 ==" -ForegroundColor Cyan
docker compose -f $compose up -d
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed ($LASTEXITCODE)" }

Write-Host ""
Write-Host "  test shard up.  connect with:  D:\UO\Play-SL-Admin-TEST.bat" -ForegroundColor Green
Write-Host "  live shard:     untouched"                                   -ForegroundColor Gray
Write-Host "  test save:      $testSaves"                                  -ForegroundColor Gray
Write-Host "  stop it with:   .\docker\uo\Start-TestShard.ps1 -Down"       -ForegroundColor Gray
Write-Host ""
