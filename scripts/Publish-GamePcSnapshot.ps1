#Requires -Version 5.1
<#
  cc-P107 (Haven step H2): what only the game PC knows, pushed to Haven every minute, so Haven's pages can show it and
  say "game PC offline since <time>" when it stops (cc-P89 Part B item 2).

      powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\scripts\Publish-GamePcSnapshot.ps1 [-DryRun]
          [-Force] [-Api http://host:port] [-TokenFile <file>] [-Config <haven-ops.json>]

  The snapshot (about 2 KB of JSON): the Golem's on/off, its running prompt and the queue, the latest DONE, and the state
  of the shard and page containers (docker ps: name, state, status, image). Sent as POST <opsApi>/api/ingest/gamepc
  with "Authorization: Bearer <token>" (the token file, never printed). No Docker API ever leaves this PC.
  Before the cutover (state.json mode is not "haven") it sends nothing and exits 0, unless -Force (the proof uses it).
  -DryRun prints the JSON and sends nothing. Refuses to run as SYSTEM (it reads D:\UO as Chase).
  Installed by Install-GamePcSnapshotTask.ps1 as a scheduled task. ASCII only.
#>
[CmdletBinding()]
param([switch]$DryRun, [switch]$Force, [string]$Api, [string]$TokenFile, [string]$Config)
$ErrorActionPreference = 'Stop'
if (-not $Config) { $Config = Join-Path $PSScriptRoot 'haven-ops.json' }
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, never as SYSTEM.'; exit 2 }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$mode = 'local'
try { $mode = [string](Get-Content -LiteralPath $cfg.stateFile -Raw | ConvertFrom-Json).mode } catch { $mode = 'local' }
if ($mode -ne 'haven' -and -not $Force -and -not $DryRun) { Write-Host ('not cut over to Haven (mode ' + $mode + '): nothing sent'); exit 0 }
if (-not $Api) { $Api = [string]$cfg.haven.opsApi }
if (-not $TokenFile) { $TokenFile = [string]$cfg.tokenFile }

function Read-Json([string]$Path) { try { Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json } catch { $null } }

# The Golem's folder: D:\UO\golem once its cutover wrote ACTIVE (cc-P99), else cc-autopilot's.
$golem = 'D:\UO\golem'
if (-not (Test-Path -LiteralPath (Join-Path $golem 'ACTIVE'))) { $golem = 'D:\UO\cc-autopilot' }
$st = Read-Json (Join-Path $golem 'state.json')
$queueDir = 'D:\UO\cc-prompts\queue'
$queue = @()
if (Test-Path -LiteralPath $queueDir) {
    $queue = @(Get-ChildItem -LiteralPath $queueDir -Filter 'cc-P*.md' -File | Where-Object { $_.Name -match '^cc-P(\d+)-' } |
        Sort-Object { [int]([regex]::Match($_.Name, '^cc-P(\d+)-').Groups[1].Value) } | ForEach-Object { $_.BaseName })
}
$golemFacts = [ordered]@{
    folder     = $golem
    on         = (Test-Path -LiteralPath (Join-Path $golem 'ENABLED'))
    running    = $(if ($st) { [string]$st.current } else { '' })
    lastDone   = $(if ($st -and $st.lastHandled) { 'P' + [string]$st.lastHandled } else { '' })
    runDone    = $(if ($st) { $st.runDone } else { $null })
    queue      = $queue
}

$names = @('sl-modernuo', 'sl-modernuo-test', 'sl-play-tests', 'sl-uo-status', 'sl-shard-probe', 'sl-dashboard', 'atm10')
# cc-P108: live's container is not reported while scripts\live.json says it is retired.
. (Join-Path $PSScriptRoot 'Live-Switch.ps1')
if (-not (Get-LiveSwitch -RepoRoot (Split-Path $PSScriptRoot -Parent)).Enabled) { $names = @($names | Where-Object { $_ -cne 'sl-modernuo' }) }
$containers = @()
$ps = & docker ps -a --format '{{.Names}}|{{.State}}|{{.Status}}|{{.Image}}' 2>$null
if ($LASTEXITCODE -eq 0) {
    foreach ($l in @($ps)) {
        $p = ([string]$l) -split '\|', 4
        if ($p.Count -eq 4 -and $names -contains $p[0]) { $containers += [ordered]@{ name = $p[0]; state = $p[1]; status = $p[2]; image = $p[3] } }
    }
}
$snap = [ordered]@{
    schema     = 1
    at         = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    host       = $env:COMPUTERNAME
    golem      = $golemFacts
    containers = $containers
    docker     = ($LASTEXITCODE -eq 0)
}
$json = $snap | ConvertTo-Json -Depth 5 -Compress
if ($DryRun) { Write-Host ('DRY RUN: would POST ' + $json.Length + ' bytes to ' + $Api + '/api/ingest/gamepc'); Write-Host $json; exit 0 }

$token = ''
try { $token = (Get-Content -LiteralPath $TokenFile -Raw -ErrorAction Stop).Trim() } catch { $token = '' }
if (-not $token) { Write-Host ('no token in ' + $TokenFile + ': nothing sent (the Ops pages deploy writes it)'); exit 1 }
try {
    $r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri ($Api.TrimEnd('/') + '/api/ingest/gamepc') -Body $json `
        -ContentType 'application/json' -Headers @{ Authorization = 'Bearer ' + $token } -TimeoutSec 10
    Write-Host ('sent ' + $json.Length + ' bytes: ' + $r.Content)
    exit 0
} catch {
    Write-Host ('NOT sent (Haven did not take it): ' + $_.Exception.Message)
    exit 1
}
