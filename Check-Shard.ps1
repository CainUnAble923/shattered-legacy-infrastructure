$ErrorActionPreference = 'Continue'
$out = Join-Path $PSScriptRoot ("shard-{0}.txt" -f (Get-Date -Format 'HHmmss'))
$L = New-Object Collections.Generic.List[string]
$L.Add("checked $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
# cc-P96: health, not just "Up". Docker's health check (docker-compose.test.yml) says whether the shard listens on
# 2594; the log since this start says whether it threw ("This exception is fatal", which leaves the container Up).
. (Join-Path $PSScriptRoot 'scripts\Shard-Health.ps1')
$st = (cmd /c "docker inspect sl-modernuo-test --format ""{{.State.Running}}|{{.State.StartedAt}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}"" 2>&1" | Out-String).Trim()
$running, $started, $health = $st -split '\|', 3
$crash = $null
if ($started -match '^\d{4}-') { $crash = Get-CrashReport (cmd /c "docker logs --since $started sl-modernuo-test 2>&1" | Out-String) }
$h = Get-HealthState -Exists ($started -match '^\d{4}-') -Running ($running -eq 'true') -Health ([string]$health) -Crash $crash
$L.Add(""); $L.Add("=== HEALTH: sl-modernuo-test ===")
$L.Add("$($h.State): $($h.Detail)")
if ($crash -and $crash.Crashed) { foreach ($f in $crash.Frames) { $L.Add("    at $f") } }
Write-Host ("test shard: " + $h.State + ": " + $h.Detail) -ForegroundColor $(if ($h.State -eq 'HEALTHY') { 'Green' } elseif ($h.State -in @('CRASHED', 'UNHEALTHY')) { 'Red' } else { 'Yellow' })
$L.Add(""); $L.Add("=== LOGS: sl-modernuo-test (last 120) ===")
$L.Add((cmd /c "docker logs --tail 120 sl-modernuo-test 2>&1" | Out-String))
$L.Add("=== when was sl-modernuo:latest built? ===")
$L.Add((cmd /c "docker image inspect sl-modernuo:latest --format {{.Created}} 2>&1" | Out-String))
$L.Add("=== what is the test container actually mounting? ===")
$L.Add((cmd /c "docker inspect sl-modernuo-test --format ""{{range .Mounts}}{{.Source}} -> {{.Destination}} ({{.Mode}}){{println}}{{end}}"" 2>&1" | Out-String))
$L.Add("=== which image is the test container running? ===")
$L.Add((cmd /c "docker inspect sl-modernuo-test --format ""{{.Config.Image}} / {{.Image}}"" 2>&1" | Out-String))
$L | Set-Content $out -Encoding utf8
Write-Host "wrote $out" -ForegroundColor Green
