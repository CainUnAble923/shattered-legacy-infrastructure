$ErrorActionPreference = 'Continue'
$out = Join-Path $PSScriptRoot ("shard-{0}.txt" -f (Get-Date -Format 'HHmmss'))
$L = New-Object Collections.Generic.List[string]
$L.Add("checked $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
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
