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
param([switch]$DryRun, [switch]$Force, [string]$Api, [string]$TokenFile, [string]$Config,
      # cc-P112: tonight's bot plan, for the play-test page's robot icons. -NoBotPlan skips it (the tests).
      [string]$BotPlan = 'D:\UO\shard-migration\testing\bot-tonight.json', [switch]$NoBotPlan)
$ErrorActionPreference = 'Stop'
if (-not $Config) { $Config = Join-Path $PSScriptRoot 'haven-ops.json' }
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, never as SYSTEM.'; exit 2 }

# cc-P112: refresh tonight's bot plan (tools\playtest-bot\uobot\tonight.py, which asks the runner's own work check, read
# only), at most every 5 minutes, in every mode: the game PC's play-test page reads this file, and after the cutover
# Haven's reads the same plan from this snapshot (botTonight). It never stops the snapshot: a failure leaves the file
# saying the plan is unavailable, or the old file in place.
if (-not $NoBotPlan) {
    $bot = Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\playtest-bot'
    $migration = Split-Path (Split-Path $BotPlan -Parent) -Parent
    Push-Location -LiteralPath $bot
    try {
        $ErrorActionPreference = 'Continue'
        $null = & python -B -m uobot.tonight --repo (Split-Path $PSScriptRoot -Parent) --plays (Join-Path $migration 'testing\play-tests.json') `
            --runs-root (Join-Path $migration 'testing\bot-runs') --triage (Join-Path $migration 'testing\bot-triage.md') --out $BotPlan --max-age 300 2>&1
    } catch { } finally { $ErrorActionPreference = 'Stop'; Pop-Location }
}

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
# cc-P133: what Haven's dashboard shows for the game PC comes from docker\haven\services.json: every tile with a snapshot
# key is reported in services (up or not, and a few words why), so the inventory, the dashboard and this snapshot name
# the same things. A container, a process (the tower's print queue) or Tailscale's view of a node (Haven).
$watch = @()
$tailnetOf = @{}
try {
    $inv = Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'docker\haven\services.json') -Raw | ConvertFrom-Json
    foreach ($s in @($inv.services)) {
        foreach ($t in @($s.tiles)) {
            if (-not $t.snapshot) { continue }
            $watch += [pscustomobject]@{ Key = [string]$t.snapshot; Container = $(if ($s.host -eq 'gamepc' -and $s.kind -eq 'container') { [string]$s.container } else { '' })
                Process = [string]$s.process; Node = $(if ($s.kind -eq 'host-service' -and $s.key -eq 'tailscale') { [string]$inv.hosts.([string]$s.host).tailnet } else { '' }) }
        }
    }
} catch { $watch = @() }
foreach ($w in $watch) { if ($w.Container -and $names -notcontains $w.Container) { $names += $w.Container } }
$containers = @()
$ps = & docker ps -a --format '{{.Names}}|{{.State}}|{{.Status}}|{{.Image}}' 2>$null
if ($LASTEXITCODE -eq 0) {
    foreach ($l in @($ps)) {
        $p = ([string]$l) -split '\|', 4
        if ($p.Count -eq 4 -and $names -contains $p[0]) { $containers += [ordered]@{ name = $p[0]; state = $p[1]; status = $p[2]; image = $p[3] } }
    }
}
$services = [ordered]@{}
foreach ($w in $watch) {
    if ($w.Container) {
        $c = @($containers | Where-Object { $_.name -eq $w.Container })[0]
        $services[$w.Key] = $(if ($c) { [ordered]@{ up = ($c.state -eq 'running'); detail = [string]$c.status } } else { [ordered]@{ up = $false; detail = 'no such container' } })
    } elseif ($w.Process) {
        $hit = @(Get-CimInstance Win32_Process -Filter ("CommandLine like '%" + $w.Process + "%'") -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^(py|python)' })
        $services[$w.Key] = [ordered]@{ up = ($hit.Count -gt 0); detail = $(if ($hit) { 'running (pid ' + $hit[0].ProcessId + ')' } else { 'not running' }) }
    } elseif ($w.Node) {
        $node = $null
        try {
            $tsj = (& 'C:\Program Files\Tailscale\tailscale.exe' status --json 2>$null | Out-String) | ConvertFrom-Json
            $node = @($tsj.Peer.PSObject.Properties | ForEach-Object { $_.Value } | Where-Object { ([string]$_.DNSName).TrimEnd('.') -eq $w.Node })[0]
        } catch { $node = $null }
        $services[$w.Key] = $(if ($node) { [ordered]@{ up = [bool]$node.Online; detail = $(if ($node.Online) { 'online on the tailnet' } else { 'offline on the tailnet' }) } } else { [ordered]@{ up = $false; detail = 'not seen by this PC''s Tailscale' } })
    }
}
$snap = [ordered]@{
    schema     = 1
    at         = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    host       = $env:COMPUTERNAME
    golem      = $golemFacts
    containers = $containers
    services   = $services
    docker     = ($LASTEXITCODE -eq 0)
}
$json = $snap | ConvertTo-Json -Depth 5 -Compress
# cc-P112: the bot plan rides along as botTonight, as the file's own JSON text (valid JSON checked first; no PowerShell
# round trip, which would reshape it), and only while it leaves the snapshot well under Haven's 64 KB limit.
if (-not $NoBotPlan) {
    $planText = ''
    try { $planText = [IO.File]::ReadAllText($BotPlan); $null = $planText | ConvertFrom-Json } catch { $planText = '' }
    # One line, as the rest of the snapshot: JSON escapes every newline inside a string, so the only raw newlines are
    # between tokens, and a reader of the dry run's single line still gets the whole document.
    $planText = ($planText -replace '\r?\n\s*', '').Trim()
    if ($planText -and ($json.Length + $planText.Length) -lt 48KB) { $json = $json.Substring(0, $json.Length - 1) + ',"botTonight":' + $planText + '}' }
}
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
