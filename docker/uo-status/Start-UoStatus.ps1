# Start-UoStatus.ps1 - starts (or recreates) sl-uo-status and sl-shard-probe on the Windows box, then checks them.
# This folder has no .env, so SERVER_PATH is set here. Without it the status folder mount is wrong.
# Touches only those two containers. Never the shards. Safe to run again.
$ErrorActionPreference = 'Continue'
$env:SERVER_PATH = 'D:/ShatteredLegacy/server'
# cc-P108: the probe checks live only while scripts\live.json says liveEnabled true (SL_LIVE_ENABLED in its compose).
. (Join-Path $PSScriptRoot '..\..\scripts\Live-Switch.ps1')
$live = Get-LiveSwitch -RepoRoot (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$env:SL_LIVE_ENABLED = $(if ($live.Enabled) { '1' } else { '0' })
if ($live.Problem) { Write-Host $live.Problem -ForegroundColor Yellow }
Set-Location -LiteralPath $PSScriptRoot

Write-Host 'Docker daemon:' (docker info --format '{{.Name}}')
docker compose up -d --force-recreate uo-status shard-probe 2>&1 | ForEach-Object { "$_" }
Start-Sleep -Seconds 8

Write-Host ''
Write-Host 'Mount:'
docker inspect sl-uo-status --format '{{range .Mounts}}{{.Source}} -> {{.Destination}}{{println}}{{end}}'

if ($live.Enabled) { Write-Host 'Probe log (live should say up if the shard is running; test says down unless you started it):' }
else { Write-Host ('Probe log (test only. ' + $live.Refusal + ' Test says down unless you started it):') }
docker logs --tail 3 sl-shard-probe 2>&1 | ForEach-Object { "  $_" }

Write-Host 'Checks (expect migration.json and shards.json 200; status.json 503 until the shard publisher exists):'
foreach ($f in 'migration.json', 'shards.json', 'status.json') {
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:8092/$f" -TimeoutSec 5
        Write-Host ("  {0}: {1}" -f $f, $r.StatusCode)
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        if (-not $code) { $code = $_.Exception.Message }
        Write-Host ("  {0}: {1}" -f $f, $code)
    }
}
