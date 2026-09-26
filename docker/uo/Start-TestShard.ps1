#Requires -Version 5.1
<#
  PowerShell front end for docker/uo/test-shard.sh.

      .\docker\uo\Start-TestShard.ps1            build (gated) + bring the test shard up
      .\docker\uo\Start-TestShard.ps1 -Fresh     wipe the test save first
      .\docker\uo\Start-TestShard.ps1 -Down      stop it
      .\docker\uo\Start-TestShard.ps1 -SkipBuild start the image already built, no gates

  Everything happens on the THROWAWAY test shard: sl-modernuo-test, port 2594 (LAN),
  its own Saves-test and Configuration-test. The live container, the live world save
  and DNS are never touched.

  build.sh still does the building. It is the only thing that runs the patch check
  and the tests as gates, and reimplementing it here is exactly how a gate gets lost,
  so this script finds a bash and calls it rather than repeating it.
#>
[CmdletBinding()]
param([switch]$Fresh, [switch]$Down, [switch]$SkipBuild)

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
$liveSaves = "$repo\server\lib\uo\modernuo\Saves"
if (-not (Test-Path "$testSaves\Accounts") -and (Test-Path "$liveSaves\Accounts")) {
    Write-Host "seeding test accounts from the live save (one-directional copy)" -ForegroundColor Gray
    Copy-Item "$liveSaves\Accounts" "$testSaves\Accounts" -Recurse
}
# D19: the Accounts folder alone is not enough. Accounts.bin names each account's type by
# a hash that only SerializedTypes.db resolves; without it ModernUO skips every account
# without a word, Accounts.Count is 0, and the owner prompt above blocks anyway. That is
# what -Fresh did until 2026-09-26. Seed the type table with the accounts.
if (-not (Test-Path "$testSaves\SerializedTypes.db") -and (Test-Path "$liveSaves\SerializedTypes.db")) {
    Copy-Item "$liveSaves\SerializedTypes.db" "$testSaves\SerializedTypes.db"
}
if (-not (Test-Path $testConf)) {
    Write-Host "seeding Configuration-test from the live Configuration" -ForegroundColor Gray
    Copy-Item $liveConf $testConf -Recurse
}

# Listen on 2594 INSIDE the container and advertise the LAN address. The relay packet
# then says 192.168.1.58:2594, which is this shard and nothing else; the live shard is
# 192.168.1.58:2593. docker-compose.test.yml explains why the two ports must match.
# Applied on every run, so a Configuration-test seeded before 2026-09-26 (listening on
# 2593, advertising 127.0.0.1) is migrated in place rather than left behind.
$mj = "$testConf\modernuo.json"
$j0 = [IO.File]::ReadAllText($mj)
$j  = $j0 -replace '"0\.0\.0\.0:2593"', '"0.0.0.0:2594"'
$j  = $j  -replace '"serverListing\.address":\s*("[^"]*"|null)', '"serverListing.address": "192.168.1.58"'
$j  = $j  -replace '"serverListing\.serverName":\s*"[^"]*"', '"serverListing.serverName": "Shattered Legacy TEST"'
if ($j -notmatch '"0\.0\.0\.0:2594"' -or $j -match ':2593"') {
    throw "$mj does not listen on 0.0.0.0:2594 only. Fix its listeners by hand; not starting."
}
if ($j -ne $j0) {
    Write-Host "Configuration-test: listening on 2594, advertising 192.168.1.58" -ForegroundColor Gray
    # No BOM: the old Set-Content -Encoding UTF8 here wrote one.
    [IO.File]::WriteAllText($mj, $j, (New-Object Text.UTF8Encoding $false))
}

# --- build through the gates --------------------------------------------------
if ($SkipBuild) {
    Write-Host ""
    Write-Host "== -SkipBuild: starting the image already tagged sl-modernuo:latest, no gates run ==" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "== building through the gates (patches, then tests, then image) ==" -ForegroundColor Cyan
    if ($bash -eq 'wsl') { & wsl bash docker/uo/build.sh }
    else                 { & $bash -lc "cd '$($repo -replace '\\','/')' && docker/uo/build.sh" }
    if ($LASTEXITCODE -ne 0) { throw "build.sh failed ($LASTEXITCODE). Nothing was started." }
}

# --- start the throwaway shard ------------------------------------------------
Write-Host ""
Write-Host "== starting the throwaway test shard on port 2594 (LAN) ==" -ForegroundColor Cyan
docker compose -f $compose up -d
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed ($LASTEXITCODE)" }

# --- wait until it is actually listening --------------------------------------
# "docker ps says Up" proved nothing on 2026-09-19 or on 2026-09-26: a shard stuck at the
# owner prompt is Up and never listens. Read the log until one of the two appears.
# Only this start's lines: compose up leaves an unchanged container running, so read from
# its own StartedAt rather than from now.
$since = (docker inspect -f '{{.State.StartedAt}}' sl-modernuo-test).Trim()
$state = 'timeout'
for ($i = 0; $i -lt 180; $i++) {
    $log = (docker logs --since $since sl-modernuo-test 2>&1 | Out-String) -replace "\x1b\[[0-9;?]*[A-Za-z]", ''
    if ($log -match 'create the owner account') { $state = 'prompt';    break }
    if ($log -match 'Listening: [0-9.]+:2594')  { $state = 'listening'; break }
    Start-Sleep -Seconds 1
}
if ($state -eq 'prompt') {
    Write-Host ""
    Write-Host "  THE TEST SHARD IS WAITING FOR AN OWNER ACCOUNT and will not listen until it gets one." -ForegroundColor Red
    Write-Host "  The world has no accounts it can load. Answer the prompt:" -ForegroundColor Red
    Write-Host "      docker attach sl-modernuo-test" -ForegroundColor White
    Write-Host "  type y, a username, a password (each then Enter), then detach with Ctrl+P Ctrl+Q." -ForegroundColor Red
    Write-Host "  (Ctrl+C would stop the shard.)" -ForegroundColor Red
    throw "test shard blocked at the owner-account prompt"
}
if ($state -ne 'listening') {
    throw "test shard did not report 'Listening' on 2594 within 180 s. Read: docker logs sl-modernuo-test"
}

Write-Host ""
Write-Host "  test shard up.  connect with:  D:\UO\Play-SL-Admin-TEST.bat" -ForegroundColor Green
Write-Host "  from the LAN:   192.168.1.58:2594"                           -ForegroundColor Green
Write-Host "  live shard:     untouched"                                   -ForegroundColor Gray
Write-Host "  test save:      $testSaves"                                  -ForegroundColor Gray
Write-Host "  stop it with:   .\docker\uo\Start-TestShard.ps1 -Down"       -ForegroundColor Gray
Write-Host ""
