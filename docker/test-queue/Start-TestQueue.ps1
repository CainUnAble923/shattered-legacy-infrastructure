# Start-TestQueue.ps1 - starts (or recreates) sl-test-queue on the Windows box, then checks it.
# LAN only: http://192.168.1.58:8093/ . Touches only this container. Safe to run again.
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Docker daemon:' (docker info --format '{{.Name}}')
docker compose up -d --force-recreate test-queue 2>&1 | ForEach-Object { "$_" }
Start-Sleep -Seconds 3
foreach ($p in '/', '/client-test-queue.md') {
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:8093$p" -TimeoutSec 5
        Write-Host ("  {0}: {1}, {2:N0} bytes" -f $p, $r.StatusCode, $r.RawContentLength)
    } catch {
        Write-Host ("  {0}: {1}" -f $p, $_.Exception.Message) -ForegroundColor Red
    }
}
Write-Host ''
Write-Host 'Open http://192.168.1.58:8093/ (or http://localhost:8093/ on this PC).' -ForegroundColor Cyan
