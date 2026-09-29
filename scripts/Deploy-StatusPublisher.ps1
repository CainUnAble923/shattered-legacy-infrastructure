#Requires -Version 5.1
<#
  Deploy-StatusPublisher.ps1 - puts cc-P6's ShardStatusPublisher on the LIVE shard.

      .\scripts\Deploy-StatusPublisher.ps1

  Steps, stopping at the first failure:
    1. Refuses if the red stub is still in the file, or if the publisher is not committed
       (the 13:45 autocommit took the stub; run D:\UO\Commit-ShardWork.ps1 first).
    2. docker/uo/build.sh through its gates (patches, 181 tests, image).
    3. Asks, then recreates ONLY the modernuo service (never down: sl-ddns shares the project).
       Anyone playing is disconnected for the restart.
    4. Waits for "Listening" on 2593, deletes the fake-name status.sample.json,
       then reads status.json back through sl-uo-status and checks its age.
  ASCII only (PowerShell 5.1).
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
Set-Location $repo
$pub = 'server\customizations\ShardStatusPublisher.cs'

# --- 1. the right file, committed ---------------------------------------------
if (Select-String -Path $pub -Pattern 'P6 RED' -Quiet) { throw "$pub still has the P6 RED stub. Not building." }
$dirty = git status --porcelain -- $pub server/tests docker/uo/docker-compose.yml
if ($dirty) {
    Write-Host $dirty
    throw "The publisher, its test or the compose file is not committed. Run D:\UO\Commit-ShardWork.ps1 first."
}
Write-Host "Publisher committed at $(git log -1 --format='%h %s' -- $pub)" -ForegroundColor Gray

# --- 2. build through the gates -----------------------------------------------
$bash = $null
foreach ($p in @("$env:ProgramFiles\Git\bin\bash.exe", "${env:ProgramFiles(x86)}\Git\bin\bash.exe",
                 "$env:LOCALAPPDATA\Programs\Git\bin\bash.exe")) { if (Test-Path $p) { $bash = $p; break } }
if (-not $bash -and (Get-Command wsl.exe -ErrorAction SilentlyContinue)) { $bash = 'wsl' }
if (-not $bash) { throw "No bash found. Install Git for Windows." }

Write-Host ''
Write-Host '== building through the gates (patches, tests, image) ==' -ForegroundColor Cyan
if ($bash -eq 'wsl') { & wsl bash docker/uo/build.sh }
else                 { & $bash -lc "cd '$($repo -replace '\\','/')' && docker/uo/build.sh" }
if ($LASTEXITCODE -ne 0) { throw "build.sh failed ($LASTEXITCODE). Nothing was restarted." }

# --- 3. recreate the live shard, only it --------------------------------------
Write-Host ''
Write-Host 'The build passed. Next: recreate sl-modernuo with the new image and the status mount.' -ForegroundColor Yellow
Write-Host 'Anyone playing is disconnected for about a minute. sl-ddns is not touched.' -ForegroundColor Yellow
$ok = Read-Host 'Restart the live shard now? Type yes'
if ($ok -ne 'yes') { Write-Host 'Stopped before the restart. The new image is built; run this again when ready.'; exit 0 }

Push-Location docker\uo
docker compose up -d --no-deps modernuo
$rc = $LASTEXITCODE
Pop-Location
if ($rc -ne 0) { throw "docker compose up failed ($rc)" }

# --- 4. wait for it, then check the file --------------------------------------
$since = (docker inspect -f '{{.State.StartedAt}}' sl-modernuo).Trim()
$state = 'timeout'
for ($i = 0; $i -lt 240; $i++) {
    $log = (docker logs --since $since sl-modernuo 2>&1 | Out-String) -replace "\x1b\[[0-9;?]*[A-Za-z]", ''
    if ($log -match 'Listening: [0-9.]+:2593') { $state = 'listening'; break }
    Start-Sleep -Seconds 1
}
if ($state -ne 'listening') { throw "sl-modernuo did not report Listening on 2593 within 240 s. Read: docker logs sl-modernuo" }
Write-Host 'Live shard is listening on 2593.' -ForegroundColor Green

$sample = Join-Path $repo 'server\lib\uo\modernuo\status\status.sample.json'
if (Test-Path $sample) { Remove-Item $sample; Write-Host 'Deleted status.sample.json (fake names).' -ForegroundColor Gray }

Start-Sleep -Seconds 5
try {
    $r = Invoke-WebRequest -UseBasicParsing 'http://localhost:8092/status.json' -TimeoutSec 5
    Write-Host ''
    Write-Host $r.Content
    $d = $r.Content | ConvertFrom-Json
    $age = ((Get-Date).ToUniversalTime() - [DateTime]::Parse($d.generatedAt).ToUniversalTime()).TotalSeconds
    Write-Host ("generatedAt is {0:N0} seconds old (should be under 35)." -f $age)
} catch {
    Write-Host "Could not read status.json through sl-uo-status: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Is sl-uo-status running? D:\ShatteredLegacy\docker\uo-status\Start-UoStatus.ps1' -ForegroundColor Red
}
$warn = docker logs --since $since sl-modernuo 2>&1 | Select-String 'Shard status:'
if ($warn) { Write-Host 'Publisher log lines:' -ForegroundColor Yellow; $warn | ForEach-Object { "  $_" } }
else       { Write-Host 'No "Shard status:" warnings in the log.' -ForegroundColor Gray }

Write-Host ''
Write-Host 'Now log in with a character, wait a minute, and look for your name in "Online now" on' -ForegroundColor Cyan
Write-Host 'https://shatteredlegacyuo.com (staff characters are not listed on purpose).' -ForegroundColor Cyan
