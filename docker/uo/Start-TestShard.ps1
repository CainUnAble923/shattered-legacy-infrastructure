#Requires -Version 5.1
<#
  PowerShell front end for docker/uo/test-shard.sh.

      .\docker\uo\Start-TestShard.ps1            build (gated) + bring the test shard up
      .\docker\uo\Start-TestShard.ps1 -Fresh     wipe the test save first
      .\docker\uo\Start-TestShard.ps1 -Down      stop it
      .\docker\uo\Start-TestShard.ps1 -SkipBuild start the image already built, no gates
      .\docker\uo\Start-TestShard.ps1 -Image sl-modernuo:good-1234abc
                                                 start that image instead of latest (latest is not moved)
      .\docker\uo\Start-TestShard.ps1 -Commit 1234abc
                                                 build that commit through the gates (a detached worktree
                                                 outside the repo), then start it

  After a start it waits for the container's health check (docker-compose.test.yml, cc-P96). Healthy: the
  image is tagged sl-modernuo:good-<commit> from its sl.commit label (build.sh), and the commit is written
  to the test world folder (shard-commit.json) as the one that last wrote the save. Crashed: the
  exception's headline is printed, nothing is tagged, and the script exits non-zero.

  Everything happens on the THROWAWAY test shard: sl-modernuo-test, port 2594 (LAN),
  its own world folder (server\lib\uo\modernuo-test, holding Saves) and Configuration-test.
  The live container, the live world save and DNS are never touched.

  build.sh still does the building. It is the only thing that runs the patch check
  and the tests as gates, and reimplementing it here is exactly how a gate gets lost,
  so this script finds a bash and calls it rather than repeating it.
#>
[CmdletBinding()]
param(
    [switch]$Fresh, [switch]$Down, [switch]$SkipBuild,
    # cc-P96. -Image runs one sl-modernuo tag through SL_TEST_IMAGE; -Commit builds one commit and runs it.
    [string]$Image, [string]$Commit,
    # How long to wait for healthy. The health check's start period is 120 s (docker-compose.test.yml).
    [int]$HealthTimeout = 300
)

$ErrorActionPreference = 'Stop'
$repo    = (Resolve-Path "$PSScriptRoot\..\..").Path
$compose = "docker/uo/docker-compose.test.yml"
Set-Location $repo
# cc-P96: the crash and health judgments, shared with the Shard Console.
. (Join-Path $repo 'scripts\Shard-Health.ps1')

if ($Image -and $Commit) { throw "-Image and -Commit together: choose one." }
if ($Image -and $Image -cnotmatch '^sl-modernuo:[A-Za-z0-9._-]+$') { throw "-Image takes an sl-modernuo:<tag> image only: $Image" }
if ($Commit -and $Commit -notmatch '^[0-9a-fA-F]{7,40}$') { throw "-Commit takes a commit hash (7 to 40 hex digits): $Commit" }
if ($Fresh -and ($Image -or $Commit)) { throw "-Fresh with -Image or -Commit: start fresh on latest, or move the save aside first." }

if ($Down) {
    # -t 60 as well as stop_grace_period in the compose file: D36's shutdown save needs seconds (D50).
    docker compose -f $compose down -t 60
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
# The test world's folder is mounted whole as /var/lib/uo/modernuo (docker-compose.test.yml, cc-P37):
# Saves, and the Backups and Archives the shard writes beside it.
$testRoot  = "$repo\server\lib\uo\modernuo-test"
$testSaves = "$testRoot\Saves"
$testConf  = "$repo\server\uo\modernuo\Configuration-test"
$liveConf  = "$repo\server\uo\modernuo\Configuration"

# Before P37 the test save was server\lib\uo\modernuo\Saves-test. Starting without it would seed a
# NEW world and leave the old one orphaned, so stop and say how to move it instead.
$legacyTestSaves = "$repo\server\lib\uo\modernuo\Saves-test"
if (Test-Path $legacyTestSaves) {
    throw "The test save is still at $legacyTestSaves, the pre-P37 place. Stop the test shard, move that folder to $testSaves, then run this again. Not starting."
}

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
# cc-P38: from upstream d4531cd9 the engine writes idx v5 saves, which carry their types inside
# the .idx and have no SerializedTypes.db. While live stays on pinned 7c9215d97 its save is idx v3
# and still has the file, and this copy is what lets the new engine read those accounts. Once live
# saves on the new engine the file is gone and this is a no-op.
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
# cc-P73: the test account wipe (server/customizations/Misc/TestAccountWipe.cs) runs only where this key is True, and
# only Configuration-test gets it, here. Live's Configuration never has it; the shard reads it without writing it.
if ($j -notmatch '"clusterf\.testShard\.allowAccountWipe"') {
    $j = $j -replace '("settings":\s*\{)', ('$1' + "`n" + '    "clusterf.testShard.allowAccountWipe": "True",')
}
if ($j -notmatch '"clusterf\.testShard\.allowAccountWipe":\s*"True"') {
    throw "$mj does not set clusterf.testShard.allowAccountWipe to True. Fix it by hand; not starting."
}
if ($j -notmatch '"0\.0\.0\.0:2594"' -or $j -match ':2593"') {
    throw "$mj does not listen on 0.0.0.0:2594 only. Fix its listeners by hand; not starting."
}
if ($j -ne $j0) {
    Write-Host "Configuration-test: listening on 2594, advertising 192.168.1.58" -ForegroundColor Gray
    # No BOM: the old Set-Content -Encoding UTF8 here wrote one.
    [IO.File]::WriteAllText($mj, $j, (New-Object Text.UTF8Encoding $false))
}

# --- build through the gates --------------------------------------------------
if ($Commit) {
    # cc-P96: one commit, built in a detached worktree outside the repo, so the working tree is never checked out
    # or changed (the overnight bot's way, tools/playtest-bot/Run-OvernightPlaytests.ps1). Tagged test-<commit>.
    $full = ((& git -C $repo rev-parse --verify ($Commit + '^{commit}')) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $full -notmatch '^[0-9a-f]{40}$') { throw "no commit $Commit in $repo" }
    if ($bash -eq 'wsl') { throw "-Commit needs Git for Windows' bash (the worktree path is a Windows path)." }
    $src = 'D:\UO\test-shard-src'
    # Only a worktree git knows is removed through git. Not `2>$null`: under 'Stop', Windows PowerShell 5.1 turns
    # a redirected native stderr line into a terminating error.
    $known = ((& git -C $repo worktree list --porcelain) | Out-String)
    $srcFwd = $src -replace '\\', '/'
    if ($known -match ('(?m)^worktree ' + [regex]::Escape($srcFwd) + '\s*$')) { & git -C $repo worktree remove --force $src }
    if (Test-Path -LiteralPath $src) { Remove-Item -LiteralPath $src -Recurse -Force -ErrorAction Stop }
    & git -C $repo worktree prune
    & git -C $repo worktree add --detach $src $full
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for $full" }
    $Image = 'sl-modernuo:test-' + $full.Substring(0, 7)
    Write-Host ""
    Write-Host "== building commit $($full.Substring(0, 7)) through the gates in $src (as $Image) ==" -ForegroundColor Cyan
    & $bash -lc "cd '$($src -replace '\\','/')' && docker/uo/build.sh -t $Image"
    if ($LASTEXITCODE -ne 0) { throw "build.sh failed for commit $full ($LASTEXITCODE). Nothing was started." }
} elseif ($Image) {
    Write-Host ""
    Write-Host "== -Image: starting $Image, already built, no gates run (they ran when it was built) ==" -ForegroundColor Yellow
    $have = ((& docker images -q $Image) | Out-String).Trim()
    if (-not $have) { throw "there is no image $Image (docker images sl-modernuo)." }
} elseif ($SkipBuild) {
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
# cc-P96: the compose file reads SL_TEST_IMAGE, defaulting to latest. Set only for -Image and -Commit, and
# cleared otherwise, so a value left in this session cannot pick the image for a plain start.
if ($Image) { $env:SL_TEST_IMAGE = $Image; Write-Host "  image: $Image" -ForegroundColor Gray }
else { Remove-Item Env:SL_TEST_IMAGE -ErrorAction SilentlyContinue }
docker compose -f $compose up -d
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed ($LASTEXITCODE)" }

# --- wait until it is healthy, or crashed -------------------------------------
# "docker ps says Up" proved nothing on 2026-09-19, 2026-09-26 or 2026-10-14: a shard stuck at the owner
# prompt is Up and never listens, and so is one that threw at start (D89: "press return to exit"). cc-P96:
# the health check (listening on 2594) decides up; the log decides the owner prompt and a crash.
# Only this start's lines: compose up leaves an unchanged container running, so read from its own StartedAt.
$since = (docker inspect -f '{{.State.StartedAt}}' sl-modernuo-test).Trim()
$state = 'timeout'
$crash = $null
$health = ''
for ($i = 0; $i -lt $HealthTimeout; $i += 2) {
    $log = (docker logs --since $since sl-modernuo-test 2>&1 | ForEach-Object { [string]$_ }) -join "`n"
    if ((Remove-HealthAnsi $log) -match 'create the owner account') { $state = 'prompt'; break }
    $crash = Get-CrashReport $log
    if ($crash.Crashed) { $state = 'crashed'; break }
    $health = ((docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}|{{.State.Running}}' sl-modernuo-test) | Out-String).Trim()
    if ($health -like 'healthy|*')   { $state = 'healthy'; break }
    if ($health -like 'unhealthy|*') { $state = 'unhealthy'; break }
    if ($health -like '*|false')     { $state = 'exited'; break }
    if ($health -like 'none|*')      { $state = 'nohealth'; break }
    Start-Sleep -Seconds 2
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
if ($state -eq 'crashed') {
    $where = 'after it had started'
    if ($crash.AtStart) { $where = 'AT START' }
    Write-Host ""
    Write-Host "  THE TEST SHARD CRASHED $where. Nothing was tagged good." -ForegroundColor Red
    Write-Host ("  " + $crash.Headline) -ForegroundColor Red
    foreach ($f in $crash.Frames) { Write-Host ("      at " + $f) -ForegroundColor Gray }
    Write-Host "  Shard Console > Fix test shard: Diagnose names a known bug; Roll back runs the last good image." -ForegroundColor Yellow
    throw ("test shard crashed " + $where.ToLower() + ": " + $crash.Headline)
}
if ($state -eq 'nohealth') {
    throw "sl-modernuo-test has no health check: it was not created from the cc-P96 docker-compose.test.yml. Run this again to recreate it."
}
if ($state -ne 'healthy') {
    throw "test shard did not become healthy (listening on 2594) within $HealthTimeout s (last: $health). Read: docker logs sl-modernuo-test"
}

# --- Part C: tag the image good, and record what last wrote the save ----------
$imageId = ((docker inspect -f '{{.Image}}' sl-modernuo-test) | Out-String).Trim()
# The labels from the JSON, not a --format template: Windows PowerShell 5.1 strips the inner quotes from
# {{index .Config.Labels "sl.commit"}} on its way to docker.exe, which then fails to parse it.
$labels  = (((docker image inspect $imageId) | Out-String) | ConvertFrom-Json)[0].Config.Labels
$lc = [string]$labels.'sl.commit'
$ld = [string]$labels.'sl.dirty'
if ($lc -notmatch '^[0-9a-f]{40}$' -and $Commit) {
    # A commit before cc-P96 has a build.sh that writes no label, but -Commit built exactly $full from a clean worktree.
    $lc = $full; $ld = '0'
} elseif ($lc -notmatch '^[0-9a-f]{40}$' -and $Image -and (Get-GoodTagCommit $Image)) {
    # Such an image, run again by its good tag (Roll back): the tag names its commit.
    $lc = Get-GoodTagCommit $Image; $ld = $(if ($Image -like '*-dirty') { '1' } else { '0' })
}
$good = Get-GoodTag $lc ($ld -eq '1')
if ($good) {
    docker tag $imageId $good
    if ($LASTEXITCODE -ne 0) { throw "docker tag $imageId $good failed ($LASTEXITCODE)" }
    Write-Host "  healthy: tagged $good" -ForegroundColor Green
} else {
    Write-Host "  healthy, but the image has no sl.commit label (built before cc-P96), so it is not tagged good." -ForegroundColor Yellow
}
$record = [ordered]@{
    commit  = $(if ($lc -match '^[0-9a-f]{7,40}$') { $lc } else { $null })
    dirty   = ($ld -eq '1')
    image   = $(if ($Image) { $Image } else { 'sl-modernuo:latest' })
    imageId = $imageId
    good    = $good
    at      = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}
# Read by the console's Roll back: a save written by a newer commit than the image it would run is warned about.
[IO.File]::WriteAllText("$testRoot\shard-commit.json", ($record | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))

Write-Host ""
Write-Host "  test shard up.  connect with:  D:\UO\Play-SL-Admin-TEST.bat" -ForegroundColor Green
Write-Host "  from the LAN:   192.168.1.58:2594"                           -ForegroundColor Green
Write-Host "  live shard:     untouched"                                   -ForegroundColor Gray
Write-Host "  test save:      $testSaves"                                  -ForegroundColor Gray
Write-Host "  stop it with:   .\docker\uo\Start-TestShard.ps1 -Down"       -ForegroundColor Gray
Write-Host ""
