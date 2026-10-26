#Requires -Version 5.1
<#
  cc-P668: a restart must not bring back an sl-ddns that can publish this PC's VPN address.

      powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\ddns\Test-DdnsSafe.Tests.ps1 [-OldCompose <file>]

  Facts about docker\uo\Test-DdnsSafe.ps1 and where it runs:
    StartTestShardRunsTheGuard   Start-TestShard.ps1 runs Test-DdnsSafe.ps1 -Fix before both its stop and its start
    ComposeFileIsSafe            the guard passes docker\uo\docker-compose.yml
    OldComposeFileIsUnsafe       the guard fails the pre-P668 file (PUBLICIP_FETCHERS: all), given as -OldCompose
    UnsafeContainerIsStopped     a running container with PUBLICIP_FETCHERS=all: exit 3, stopped, restart=no
    SafeContainerIsLeftAlone     a running container with the gate's settings: exit 0, still running
  The containers are throwaway alpine ones named sl-ddns-proof-*, with only the environment the guard reads.
  ASCII only.
#>
[CmdletBinding()]
param([string]$OldCompose)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $here '..\..\..')).Path
$guard = Join-Path $repo 'docker\uo\Test-DdnsSafe.ps1'
$fail = 0
function Check([string]$Name, [bool]$Ok, [string]$Said) {
    if ($Ok) { Write-Host ('PASS ' + $Name + ': ' + $Said) -ForegroundColor Green }
    else { $script:fail++; Write-Host ('FAIL ' + $Name + ': ' + $Said) -ForegroundColor Red }
}
function Run-Guard([string[]]$GuardArgs) {
    $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $guard @GuardArgs
    [pscustomobject]@{ Code = $LASTEXITCODE; Out = ($out -join ' | ') }
}
function Docker-Quiet([string]$Line) { [void](& cmd.exe /c ('docker ' + $Line + ' >NUL 2>&1')) }

# --- StartTestShardRunsTheGuard --------------------------------------------------------------------------------------
$sts = [IO.File]::ReadAllText((Join-Path $repo 'docker\uo\Start-TestShard.ps1'))
$guardAt = $sts.IndexOf('Test-DdnsSafe.ps1')
$downAt = $sts.IndexOf('if ($Down) {')
Check 'StartTestShardRunsTheGuard' (($guardAt -ge 0) -and ($downAt -gt $guardAt) -and ($sts -match 'Test-DdnsSafe\.ps1''?\)?\s+-Fix')) `
    ('guard at ' + $guardAt + ', the -Down branch at ' + $downAt)

if (-not (Test-Path -LiteralPath $guard)) {
    Check 'GuardExists' $false ($guard + ' is missing')
} else {
    # --- ComposeFileIsSafe / OldComposeFileIsUnsafe -------------------------------------------------------------------
    $r = Run-Guard @('-Container', 'sl-ddns-proof-none')
    Check 'ComposeFileIsSafe' ($r.Code -eq 0) ('exit ' + $r.Code + ': ' + $r.Out)
    if ($OldCompose) {
        $r = Run-Guard @('-Container', 'sl-ddns-proof-none', '-Compose', $OldCompose)
        Check 'OldComposeFileIsUnsafe' ($r.Code -eq 3 -and $r.Out -match 'PUBLICIP_FETCHERS is ''all''') ('exit ' + $r.Code + ': ' + $r.Out)
    }

    # --- UnsafeContainerIsStopped / SafeContainerIsLeftAlone ------------------------------------------------------------
    foreach ($n in 'sl-ddns-proof-old', 'sl-ddns-proof-new') { Docker-Quiet ('rm -f ' + $n) }
    Docker-Quiet 'run -d --name sl-ddns-proof-old --restart unless-stopped -e PUBLICIP_FETCHERS=all -e PUBLICIP_HTTP_PROVIDERS=all alpine:latest sleep 600'
    $g = 'url:https://ddns-gate:8443/ip'
    Docker-Quiet ('run -d --name sl-ddns-proof-new --restart unless-stopped -e PUBLICIP_FETCHERS=http -e PUBLICIP_HTTP_PROVIDERS=' + $g +
        ' -e PUBLICIPV4_HTTP_PROVIDERS=' + $g + ' -e PUBLICIPV6_HTTP_PROVIDERS=' + $g + ' -e SSL_CERT_FILE=/tls/bundle.pem alpine:latest sleep 600')

    $r = Run-Guard @('-Container', 'sl-ddns-proof-old', '-Compose', 'none', '-Fix')
    $state = (& docker inspect --format '{{.State.Running}} {{.HostConfig.RestartPolicy.Name}}' sl-ddns-proof-old) -join ''
    Check 'UnsafeContainerIsStopped' ($r.Code -eq 3 -and $state -eq 'false no') ('exit ' + $r.Code + ', now ' + $state + ': ' + $r.Out)

    $r = Run-Guard @('-Container', 'sl-ddns-proof-new', '-Compose', 'none', '-Fix')
    $state = (& docker inspect --format '{{.State.Running}} {{.HostConfig.RestartPolicy.Name}}' sl-ddns-proof-new) -join ''
    Check 'SafeContainerIsLeftAlone' ($r.Code -eq 0 -and $state -eq 'true unless-stopped') ('exit ' + $r.Code + ', now ' + $state + ': ' + $r.Out)

    foreach ($n in 'sl-ddns-proof-old', 'sl-ddns-proof-new') { Docker-Quiet ('rm -f ' + $n) }
}
if ($fail) { Write-Host ([string]$fail + ' FAILED') -ForegroundColor Red; exit 1 }
Write-Host 'all passed' -ForegroundColor Green
exit 0
