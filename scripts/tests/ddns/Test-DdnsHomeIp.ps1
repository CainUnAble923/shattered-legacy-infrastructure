#Requires -Version 5.1
<#
  cc-P668: the facts for "sl-ddns publishes Haven's address, never this PC's VPN exit".

      powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\ddns\Test-DdnsHomeIp.ps1 [-Compose <file>] [-Only <name>]

  Runs docker\uo\docker-compose.yml (or -Compose, for the red run against the old file) with proof.override.yml as its
  own compose project, sl-ddns-proof, with a dummy Cloudflare token, a dummy zone and the domain example.com: nothing
  here can change shatteredlegacyuo.com. A stub plays Haven (/haven) and the public lookups (/egress, /a, /b); havenip
  is the real docker\homeip\homeip.py asking the stub. Each scenario starts the stack, waits for ddns-updater's first
  pass, reads its log and the gate's, and takes the stack down with its volumes.

    VpnOn            Haven says 8.8.4.4, this PC goes out as 159.26.100.174: the updater's IP is 8.8.4.4
    VpnOff           Haven and this PC both 8.8.4.4: the updater's IP is 8.8.4.4
    LookupsDisagree  Haven's two lookups say 8.8.4.4 and 8.8.8.8: no update, and the gate's log says they disagree
    HavenSaysVpnExit Haven reports 159.26.100.174 (a ProtonVPN exit): no update, the gate says it is a VPN exit
    HavenDown        Haven does not answer: no update, the gate says Haven did not answer

  Exit 0 when every scenario passes. Runs no build and needs no lock: four small containers, about a minute each.
  ASCII only.
#>
[CmdletBinding()]
param(
    [string]$Compose,
    [string]$Only,
    [int]$WaitSeconds = 90
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $here '..\..\..')).Path
$uo = Join-Path $repo 'docker\uo'
if (-not $Compose) { $Compose = Join-Path $uo 'docker-compose.yml' }
$override = Join-Path $here 'proof.override.yml'
$project = 'sl-ddns-proof'
$updater = 'sl-ddns-proof'
$gate = 'sl-ddns-proof-gate'

# Never the real token: an env file of dummies, so compose reads no .env at all.
$envFile = Join-Path ([IO.Path]::GetTempPath()) 'sl-ddns-proof.env'
[IO.File]::WriteAllText($envFile, "CF_ZONE_ID=00000000000000000000000000000000`nCF_API_TOKEN=p668-dummy-token-not-real`nDOMAIN=example.com`n")

$scenarios = @(
    @{ Name = 'VpnOn'; Routes = '/haven=8.8.4.4;/egress=159.26.100.174'; Haven = 'http://stub:8000/haven'
       Expect = 'ip'; Ip = '8.8.4.4' },
    @{ Name = 'VpnOff'; Routes = '/haven=8.8.4.4;/egress=8.8.4.4'; Haven = 'http://stub:8000/haven'
       Expect = 'ip'; Ip = '8.8.4.4' },
    @{ Name = 'LookupsDisagree'; Routes = '/a=8.8.4.4;/b=8.8.8.8;/egress=159.26.100.174'; Haven = 'http://havenip:8000/ip'
       Expect = 'refused'; Why = 'lookups disagree' },
    @{ Name = 'HavenSaysVpnExit'; Routes = '/haven=159.26.100.174;/egress=159.26.100.174'; Haven = 'http://stub:8000/haven'
       Expect = 'refused'; Why = 'known VPN exit' },
    @{ Name = 'HavenDown'; Routes = '/egress=159.26.100.174'; Haven = 'http://stub:8000/nothing-here'
       Expect = 'refused'; Why = 'Haven did not answer' }
)
if ($Only) { $scenarios = @($scenarios | Where-Object { $_.Name -eq $Only }) }

function Invoke-Compose([string[]]$More) {
    $a = @('compose', '-p', $project, '--project-directory', $uo, '--env-file', $envFile, '-f', $Compose, '-f', $override) + $More
    $out = & cmd.exe /c ('docker ' + (($a | ForEach-Object { '"' + $_ + '"' }) -join ' ') + ' 2>&1')
    [pscustomobject]@{ Code = $LASTEXITCODE; Out = ($out -join "`n") }
}
function Get-Log([string]$Name) { (& cmd.exe /c ('docker logs ' + $Name + ' 2>&1')) -join "`n" }

$fail = 0
foreach ($s in $scenarios) {
    Write-Host ('== ' + $s.Name) -ForegroundColor Cyan
    $env:DDNS_NAME = $updater; $env:GATE_NAME = $gate
    $env:STUB_ROUTES = $s.Routes
    $env:GATE_HAVEN_URLS = $s.Haven
    $env:GATE_EGRESS_URLS = 'http://stub:8000/egress,http://stub:8000/egress'
    [void](Invoke-Compose @('down', '-v', '-t', '2'))
    $up = Invoke-Compose @('up', '-d', 'stub', 'havenip')
    if ($s.Name -eq 'LookupsDisagree') { Start-Sleep -Seconds 8 }   # havenip's first check, so it answers 503, not "not checked yet"
    $up = Invoke-Compose @('up', '-d')
    if ($up.Code -ne 0) { Write-Host $up.Out; Write-Host ('FAIL ' + $s.Name + ': compose up failed') -ForegroundColor Red; $fail++; continue }
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    $log = ''
    while ((Get-Date) -lt $deadline) {
        $log = Get-Log $updater
        if ($log -match 'your ipv4 address is|(?i)error|cannot|failed') { Start-Sleep -Seconds 3; $log = Get-Log $updater; break }
        Start-Sleep -Seconds 2
    }
    $gateLog = Get-Log $gate
    $chosen = $(if ($log -match 'your ipv4 address is (\d+\.\d+\.\d+\.\d+)') { $Matches[1] } else { '' })
    $updated = $log -match 'Updating record'
    $ok = $false
    if ($s.Expect -eq 'ip') {
        $ok = ($chosen -eq $s.Ip)
        $said = 'updater chose ' + $(if ($chosen) { $chosen } else { '(none)' }) + ', Haven says ' + $s.Ip
    } else {
        $refusal = @($gateLog -split "`n" | Where-Object { $_ -match '\*\*\* REFUSED \*\*\*' }) | Select-Object -First 1
        # The updater's own log carries the gate's 403 reason too, with every address masked.
        $told = $log -match 'REFUSED: '
        $ok = (-not $updated) -and (-not $chosen) -and $refusal -and ($refusal -match [regex]::Escape($s.Why)) -and $told
        $said = 'updater chose ' + $(if ($chosen) { $chosen } else { '(none)' }) + '; update attempted: ' + $updated +
            '; updater logged the refusal: ' + $told +
            '; gate: ' + $(if ($refusal) { $refusal.Trim() } else { '(no refusal logged)' })
    }
    if ($ok) { Write-Host ('PASS ' + $s.Name + ': ' + $said) -ForegroundColor Green }
    else {
        $fail++
        Write-Host ('FAIL ' + $s.Name + ': ' + $said) -ForegroundColor Red
        Write-Host '--- updater (last 15 lines)'; ($log -split "`n" | Select-Object -Last 15) | ForEach-Object { Write-Host ('  ' + $_) }
        Write-Host '--- gate (last 8 lines)'; ($gateLog -split "`n" | Select-Object -Last 8) | ForEach-Object { Write-Host ('  ' + $_) }
    }
    [void](Invoke-Compose @('down', '-v', '-t', '2'))
}
Remove-Item -LiteralPath $envFile -ErrorAction SilentlyContinue
foreach ($v in 'DDNS_NAME', 'GATE_NAME', 'STUB_ROUTES', 'GATE_HAVEN_URLS', 'GATE_EGRESS_URLS') { Remove-Item ('env:' + $v) -ErrorAction SilentlyContinue }
if ($fail) { Write-Host ([string]$fail + ' of ' + $scenarios.Count + ' scenarios FAILED') -ForegroundColor Red; exit 1 }
Write-Host ('all ' + $scenarios.Count + ' scenarios passed') -ForegroundColor Green
exit 0
