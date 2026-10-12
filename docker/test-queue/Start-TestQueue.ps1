# Start-TestQueue.ps1 - starts (or recreates) sl-play-tests on the Windows box, then checks it (cc-P83).
# LAN only: http://192.168.1.58:8093/ . Touches only this service. Safe to run again.
# First time only: the old read-only sl-test-queue holds port 8093, so it is stopped and removed here
# (it was nginx over docs/client-test-queue.md; that file is not touched).
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Docker daemon:' (docker info --format '{{.Name}}')
$old = docker ps -a --filter 'name=^sl-test-queue$' --format '{{.Names}}'
if ($old) {
    Write-Host 'Removing the old read-only sl-test-queue (it holds port 8093).'
    docker stop sl-test-queue 2>&1 | ForEach-Object { "  $_" }
    docker rm sl-test-queue 2>&1 | ForEach-Object { "  $_" }
}
New-Item -ItemType Directory -Force -Path 'D:\UO\shard-migration\testing' | Out-Null
docker compose up -d --force-recreate play-tests 2>&1 | ForEach-Object { "$_" }
Start-Sleep -Seconds 4
foreach ($p in '/', '/api/summary') {
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:8093$p" -TimeoutSec 5
        Write-Host ("  {0}: {1}, {2:N0} bytes" -f $p, $r.StatusCode, $r.RawContentLength)
        if ($p -eq '/api/summary') { Write-Host ('  ' + $r.Content) }
    } catch {
        Write-Host ("  {0}: {1}" -f $p, $_.Exception.Message) -ForegroundColor Red
    }
}
Write-Host ''
Write-Host 'Open http://192.168.1.58:8093/ (or http://localhost:8093/ on this PC).' -ForegroundColor Cyan
Write-Host 'Logs: docker logs -f sl-play-tests'
