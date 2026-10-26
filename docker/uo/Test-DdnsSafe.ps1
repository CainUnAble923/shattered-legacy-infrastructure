#Requires -Version 5.1
<#
  cc-P668: is sl-ddns the one that can only publish Haven's address? Stops one that is not.

      .\docker\uo\Test-DdnsSafe.ps1          check the sl-ddns container (and the compose file); say what is wrong
      .\docker\uo\Test-DdnsSafe.ps1 -Fix     the same, and stop an unsafe sl-ddns and set it to never restart
      .\docker\uo\Test-DdnsSafe.ps1 -Reach   only: from inside sl-ddns-gate, does Haven answer (LAN, tailnet), and how
                                           does this PC go out? Run it with the VPN on and off (cc-P668 part 2).

  On 2026-10-26 a restart brought back an sl-ddns that asked "what is my IP" from this PC, through Chase's VPN, and it
  published the VPN's exit as shatteredlegacyuo.com. Safe means ddns-updater's only public-IP source is the gate
  (docker-compose.yml, cc-P668):
      PUBLICIP_FETCHERS=http
      PUBLICIP_HTTP_PROVIDERS, PUBLICIPV4_HTTP_PROVIDERS, PUBLICIPV6_HTTP_PROVIDERS = url:https://ddns-gate:8443/ip
      SSL_CERT_FILE set (the gate's certificate bundle)
  Start-TestShard.ps1 runs it with -Fix on every start and stop. No sl-ddns at all is safe (exit 0).
  Prints only the names and values of those keys, never CONFIG (it holds the Cloudflare token).

  Exit 0 safe (or absent), 3 unsafe (stopped with -Fix), 2 could not tell (docker did not answer). ASCII only.
#>
[CmdletBinding()]
param(
    [string]$Container = 'sl-ddns',
    [switch]$Fix,
    # The compose file to check as well; 'none' skips it. Default: this folder's docker-compose.yml.
    [string]$Compose,
    [switch]$Reach,
    [string]$Gate = 'sl-ddns-gate'
)
$ErrorActionPreference = 'Stop'
if ($Reach) {
    & docker exec $Gate python /app/gate.py --reach
    exit $LASTEXITCODE
}
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $PSBoundParameters.ContainsKey('Compose')) { $Compose = Join-Path $here 'docker-compose.yml' }
$gateUrl = 'url:https://ddns-gate:8443/ip'
$want = [ordered]@{
    'PUBLICIP_FETCHERS'         = 'http'
    'PUBLICIP_HTTP_PROVIDERS'   = $gateUrl
    'PUBLICIPV4_HTTP_PROVIDERS' = $gateUrl
    'PUBLICIPV6_HTTP_PROVIDERS' = $gateUrl
}

function Get-Problems([hashtable]$Env) {
    $p = @()
    foreach ($k in $want.Keys) {
        $v = [string]$Env[$k]
        if ($v -cne $want[$k]) { $p += ($k + ' is ' + $(if ($v) { "'" + $v + "'" } else { 'unset' }) + ', not ' + "'" + $want[$k] + "'") }
    }
    if (-not [string]$Env['SSL_CERT_FILE']) { $p += 'SSL_CERT_FILE is unset (it cannot trust the gate, so nothing would update; not this compose file)' }
    $p
}

$unsafe = $false
# --- the compose file: what the next compose up would create ---------------------------------------------------------
if ($Compose -and $Compose -ne 'none') {
    $text = [IO.File]::ReadAllText($Compose)
    $m = [regex]::Match($text, '(?ms)^  ddns-updater:\s*$(.*?)(?=^  \S|^\S|\z)')
    if (-not $m.Success) { Write-Host ('compose: no ddns-updater service in ' + $Compose) -ForegroundColor Yellow }
    else {
        $fileEnv = @{}
        foreach ($line in ($m.Groups[1].Value -split "`n")) {
            if ($line -match '^\s{6}([A-Z0-9_]+):\s*"?([^"#]*?)"?\s*$') { $fileEnv[$Matches[1]] = $Matches[2] }
        }
        $p = @(Get-Problems $fileEnv)
        if ($p.Count) {
            $unsafe = $true
            Write-Host ('*** UNSAFE compose file *** ' + $Compose + ': a compose up would start an sl-ddns that can publish this PC''s VPN address:') -ForegroundColor Red
            $p | ForEach-Object { Write-Host ('    ' + $_) -ForegroundColor Red }
        } else { Write-Host ('compose: ddns-updater takes its address from the gate only (' + (Split-Path -Leaf $Compose) + ')') -ForegroundColor Green }
    }
}

# --- the container: what is there now, running or able to start -------------------------------------------------------
$ErrorActionPreference = 'Continue'
$names = @(& docker ps -a --format '{{.Names}}' 2>$null)
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) { Write-Host 'Test-DdnsSafe: docker did not answer; sl-ddns not checked.' -ForegroundColor Yellow; exit 2 }
if ($names -notcontains $Container) {
    Write-Host ($Container + ': no such container (safe: nothing publishes DNS from this PC).') -ForegroundColor Green
    if ($unsafe) { exit 3 }
    exit 0
}
# Three plain templates: Windows PowerShell 5.1 strips the quotes of a {{"\n"}} in a native command's argument.
# -InputObject, not a pipe: piped, 5.1's ConvertFrom-Json hands the whole array on as ONE item.
$envList = ConvertFrom-Json -InputObject ((& docker inspect --format '{{json .Config.Env}}' $Container) -join '')
$running = ((& docker inspect --format '{{.State.Running}}' $Container) -join '') -eq 'true'
$policy = (& docker inspect --format '{{.HostConfig.RestartPolicy.Name}}' $Container) -join ''
$cEnv = @{}
foreach ($e in $envList) { $i = $e.IndexOf('='); if ($i -gt 0) { $cEnv[$e.Substring(0, $i)] = $e.Substring($i + 1) } }
$p = @(Get-Problems $cEnv)
if (-not $p.Count) {
    Write-Host ($Container + ': safe, its only address source is the gate (' + $(if ($running) { 'running' } else { 'stopped' }) + ', restart ' + $policy + ').') -ForegroundColor Green
    if ($unsafe) { exit 3 }
    exit 0
}
Write-Host ('*** UNSAFE *** ' + $Container + ' can publish this PC''s own address (a VPN exit when the VPN is on):') -ForegroundColor Red
$p | ForEach-Object { Write-Host ('    ' + $_) -ForegroundColor Red }
if ($Fix) {
    & docker update --restart=no $Container | Out-Null
    if ($running) { & docker stop -t 5 $Container | Out-Null }
    Write-Host ('    stopped it and set restart=no. Recreate it from docker\uo\docker-compose.yml (cc-P668): docker compose -p uo -f docker\uo\docker-compose.yml up -d') -ForegroundColor Yellow
} else {
    Write-Host ('    ' + $(if ($running) { 'it is RUNNING' } else { 'it is stopped (restart ' + $policy + ')' }) + '; -Fix stops it and sets restart=no.') -ForegroundColor Yellow
}
exit 3
