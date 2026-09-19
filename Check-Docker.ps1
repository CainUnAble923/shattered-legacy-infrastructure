# Writes one timestamped file. No transcript, no locks, nothing to clean up.
$ErrorActionPreference = 'Continue'
$out = Join-Path $PSScriptRoot ("check-{0}.txt" -f (Get-Date -Format 'HHmmss'))
$L = New-Object Collections.Generic.List[string]

$L.Add("checked $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$p = Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue
$L.Add("Docker Desktop process : " + $(if ($p) { "RUNNING" } else { "NOT RUNNING" }))
$c = Get-Command docker.exe -ErrorAction SilentlyContinue
$L.Add("docker.exe on PATH     : " + $(if ($c) { $c.Source } else { "NO" }))
foreach ($b in @("$env:ProgramFiles\Git\bin\bash.exe","${env:ProgramFiles(x86)}\Git\bin\bash.exe","$env:LOCALAPPDATA\Programs\Git\bin\bash.exe")) {
    if (Test-Path $b) { $L.Add("git bash               : $b"); break }
}
if (-not ($L -match 'git bash')) { $L.Add("git bash               : NOT FOUND") }

$L.Add(""); $L.Add("--- docker version ---")
$L.Add((cmd /c "docker version 2>&1" | Out-String))
$L.Add("--- docker images sl-modernuo ---")
$L.Add((cmd /c "docker images sl-modernuo 2>&1" | Out-String))
$L.Add("--- docker ps -a ---")
$L.Add((cmd /c "docker ps -a 2>&1" | Out-String))

$L | Set-Content $out -Encoding utf8
Write-Host "wrote $out" -ForegroundColor Green
