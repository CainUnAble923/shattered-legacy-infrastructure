# Start-Dashboard.ps1 - starts (or recreates) sl-dashboard (Homepage) on the Windows box, then checks it (cc-P101).
# LAN only: http://192.168.1.58:3000/ . Touches only this service. Safe to run again. The Shard Console's
# Websites tab runs it as Homepage's Redeploy, the way Start-TestQueue.ps1 is Play tests' Redeploy.
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Docker daemon:' (docker info --format '{{.Name}}')
docker compose up -d --force-recreate dashboard 2>&1 | ForEach-Object { "$_" }
Start-Sleep -Seconds 4
try {
    $r = Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:3000/' -TimeoutSec 10
    Write-Host ('  /: {0}, {1:N0} bytes' -f $r.StatusCode, $r.RawContentLength)
} catch {
    Write-Host ('  /: {0}' -f $_.Exception.Message) -ForegroundColor Red
}
Write-Host ''
Write-Host 'Open http://192.168.1.58:3000/ (or http://localhost:3000/ on this PC).' -ForegroundColor Cyan
Write-Host 'Logs: docker logs -f sl-dashboard'
