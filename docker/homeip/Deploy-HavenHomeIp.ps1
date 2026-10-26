#Requires -Version 5.1
<#
  cc-P668: sl-homeip on Haven, the house's public IPv4 for the game PC's DDNS gate (docker/uo/ddns-gate).

      Deploy-HavenHomeIp.ps1 -Mode Discover|Deploy|Rollback [-DryRun] [-Yes]

  Run by Chase (or the overseer, under the Golem's haven lock) in a PowerShell window; it reaches Haven with ssh and
  scp as Chase (his key). Every mode starts with Discover, which only reads, then prints the plan and waits for a typed
  word (anything else stops, changing nothing). -DryRun stops after the plan. -Yes skips the typed word (the proof).

  Discover  Haven's docker and compose, whether 8098 is free on its LAN address and loopback, a container named
            sl-homeip and its compose project, ~/sl-homeip, Tailscale (installed, who may change Serve). Changes nothing.
  Deploy    refuses if an sl-homeip exists that this deploy did not make, or 8098 is taken by something else.
            Otherwise: copies docker-compose.yml and homeip.py to ~/sl-homeip; docker compose -p sl-homeip up -d
            (python:3-alpine; 192.168.1.61:8098 and 127.0.0.1:8098 only); waits for http://192.168.1.61:8098/ip from
            this PC to answer an address; points Tailscale Serve's tailnet port 8098 at loopback 8098 (services.json
            tailscaleServe). A failure stops it again.
  Rollback  stops sl-homeip and turns off Serve's port 8098. sl-ddns then gets no address and leaves the record alone
            (the gate refuses), which is safe but means the record stops following the house.

  Never touched: every other container, NPM and its hosts. Never Tailscale Funnel, never all interfaces, never public.
  Refuses to run as SYSTEM. ASCII only.

  -Local (the proof only): the Haven half runs on this PC's Docker under -Root (a scratch or proof folder), through Git
  Bash, with scratch -Name, -Project and -Port; Tailscale Serve is skipped.
#>
[CmdletBinding()]
param(
    [ValidateSet('Discover', 'Deploy', 'Rollback')][string]$Mode = 'Discover',
    [switch]$DryRun,
    [switch]$Yes,
    [string]$Ssh = 'chase@192.168.1.61',
    [string]$HavenRoot = '/home/chase/sl-homeip',
    [string]$Bind = '192.168.1.61',
    [int]$Port = 8098,
    [string]$Project = 'sl-homeip',
    [string]$Name = 'sl-homeip',
    [string]$Inventory,
    # The proof's switches.
    [switch]$Local,
    [string]$Root
)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, with his SSH key, never as SYSTEM.' -ForegroundColor Red; exit 2 }
$src = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $src '..\..')).Path
$bash = 'C:\Program Files\Git\bin\bash.exe'
. (Join-Path $repo 'docker\haven\HavenInventory.ps1')
if (-not $Inventory) { $Inventory = Join-Path $repo 'docker\haven\services.json' }
$inv = Read-HavenInventory -Path $Inventory
if ($inv.Problems.Count) { foreach ($p in $inv.Problems) { Write-Host ('INVENTORY: ' + $p) -ForegroundColor Red }; Write-Host 'docker\haven\services.json is unsound; nothing was read or changed.' -ForegroundColor Red; exit 2 }
$serveEntry = @($inv.tailscaleServe | Where-Object { $_.service -eq 'homeip' })[0]
if (-not $serveEntry -or [string]$serveEntry.target -notmatch '^http://127\.0\.0\.1:(\d+)$') { Write-Host 'REFUSED: services.json tailscaleServe has no loopback HTTP entry for homeip.' -ForegroundColor Red; exit 2 }
$serveLoop = [int]$Matches[1]
$servePort = [int]$serveEntry.tailnetPort
$tailnetUrl = 'http://' + [string]$inv.hosts.haven.tailnet + ':' + $servePort + '/ip'
if ($Local) {
    if (-not $Root -or $Root -notmatch '(?i)scratch|proof') { throw '-Local runs only under a -Root scratch or proof folder' }
    if ($Name -eq 'sl-homeip' -or $Project -eq 'sl-homeip' -or $Port -eq 8098) { throw '-Local needs scratch -Name, -Project and -Port, never the real ones' }
    # PowerShell names ignore case: $root and $Root are ONE variable, so the Windows path is kept first (cc-P109's bug).
    $winRoot = $Root
    $root = '/' + $winRoot.Substring(0, 1).ToLower() + ($winRoot.Substring(2) -replace '\\', '/')
    $hostAddr = '127.0.0.1'
} else {
    $root = $HavenRoot
    $hostAddr = $Bind
    if ($Port -ne $serveLoop) { throw ('-Port ' + $Port + ' is not the loopback port services.json serves (' + $serveLoop + ')') }
}
$ipUrl = 'http://' + $hostAddr + ':' + $Port + '/ip'
$where = $(if ($Local) { 'this PC (proof, ' + $winRoot + ')' } else { 'Haven (' + $Ssh + ')' })

function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Step([string]$t) { Write-Host ('== ' + $t) -ForegroundColor Cyan }
function Invoke-Haven {
    # One POSIX shell command on Haven (ssh as Chase) or, with -Local, in Git Bash here; base64 on the wire because
    # Windows PowerShell 5.1 mangles quotes in a native command's arguments (docker/ops/Deploy-HavenOps.ps1).
    param([string]$Command, [switch]$Quiet)
    $pre = $(if ($Local) { 'export MSYS_NO_PATHCONV=1; ' } else { '' })
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('set +f; ' + $pre + $Command))
    $wire = 'set -f; eval $(echo ' + $b64 + ' | base64 -d)'
    $ErrorActionPreference = 'Continue'
    if ($Local) { $out = & $bash -c $wire 2>&1 } else { $out = & ssh -o BatchMode=yes $Ssh $wire 2>&1 }
    $code = $LASTEXITCODE
    $lines = @($out | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { [string]$_.Exception.Message } else { [string]$_ } })
    if (-not $Quiet) { foreach ($l in $lines) { Write-Host ('    | ' + $l) } }
    [pscustomobject]@{ Code = $code; Lines = $lines }
}
function Copy-ToHaven([string]$From, [string]$To) {
    if ($Local) { Copy-Item -LiteralPath $From -Destination ($winRoot + ($To.Substring($root.Length) -replace '/', '\')) -Force }
    else { & scp -q -o BatchMode=yes $From ($Ssh + ':' + $To); if ($LASTEXITCODE -ne 0) { throw ('scp failed: ' + $From) } }
}
# The proof binds 127.0.0.1 for both, so its loopback port is the next one.
$loopPort = $(if ($Local) { $Port + 1 } else { $Port })
$composeEnv = 'HI_PROJECT=' + $Project + ' HI_NAME=' + $Name + ' HI_BIND=' + $hostAddr + ' HI_PORT=' + $Port + ' HI_LOOP_PORT=' + $loopPort
function Compose([string]$What) { Invoke-Haven ('cd ' + $root + ' && ' + $composeEnv + ' docker compose -p ' + $Project + ' ' + $What) }
function Wait-Ip([int]$Seconds = 90) {
    # sl-homeip answers 503 until its first two lookups agree, then 200 and the address.
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try { $r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 -Uri $ipUrl; if ($r.StatusCode -eq 200) { return $r.Content.Trim() } } catch { }
        Start-Sleep -Seconds 3
    }
    $null
}
function Confirm-Go([string]$Word) {
    if ($DryRun) { Say 'DRY RUN: stopping here; nothing was changed.' 'Yellow'; exit 0 }
    if ($Yes) { return }
    $typed = Read-Host ("Type '" + $Word + "' to do the above (anything else stops, changing nothing)")
    if ($typed -cne $Word) { Say 'Stopped: nothing was changed.' 'Yellow'; exit 0 }
}

# --- Discover (every mode starts here; it only reads) ---------------------------------------------------------------
Step ('Discover: ' + $where + ' (read only)')
$probe = Invoke-Haven -Quiet ('echo host=$(hostname); docker --version; docker compose version; ' +
    'echo "-- listening ports"; if command -v ss >/dev/null 2>&1; then ss -tln | tail -n +2 | awk ''{print "  " $4}''; else netstat -an | grep LISTEN | awk ''{print "  " $2}''; fi | sort -u | head -80; ' +
    'echo "-- tailscale"; if command -v tailscale >/dev/null 2>&1; then echo "  installed: yes"; ' +
    'o=$(tailscale debug prefs 2>/dev/null | grep -o "\"OperatorUser\": *\"[^\"]*\"" | sed "s/.*: *//; s/\"//g"); echo "  operator: ${o:-none}"; echo "  me: $(id -un)"; ' +
    'else echo "  installed: no"; fi; ' +
    'echo "-- facts"; ' +
    'docker inspect -f "homeip: {{.State.Status}} project={{index .Config.Labels \"com.docker.compose.project\"}}" ' + $Name + ' 2>/dev/null || echo "homeip: none"; ' +
    'test -f ' + $root + '/docker-compose.yml && echo "folder: yes" || echo "folder: no"')
foreach ($l in $probe.Lines) { Say ('    ' + $l) }
if ($probe.Code -ne 0 -and -not ($probe.Lines -match '^host=')) { Say ('Cannot reach ' + $where + '. Nothing was changed.') 'Red'; exit 1 }
$facts = @{}
foreach ($l in $probe.Lines) { if ($l -match '^\s*([a-z][a-z0-9 ]*[a-z0-9]): (.*)$') { $facts[$Matches[1]] = $Matches[2] } }
$disc = ConvertFrom-HavenDiscovery -Text (($probe.Lines -join "`n") + "`n")
$ts = $disc.Tailscale
$canServe = (-not $Local) -and ($ts['installed'] -eq 'yes') -and ($ts['operator'] -eq $ts['me'] -or $ts['me'] -eq 'root')
$hi = [string]$facts['homeip']
$hiExists = $hi -and $hi -ne 'none'
$hiOurs = $hiExists -and $hi -match (' project=' + [regex]::Escape($Project) + '$')
$hiRunning = $hi -like 'running*'
Say ('    sl-homeip: ' + $(if (-not $hiExists) { 'none yet' } elseif ($hiOurs) { $hi + ' (this deploy''s)' } else { $hi + ' (NOT this deploy''s)' }))
if (-not $Local -and $ts['installed'] -ne 'yes') { Say '  Tailscale is not installed on Haven: sl-homeip is on the LAN only until it is.' 'Yellow' }
elseif (-not $Local -and -not $canServe) { Say ('  Tailscale Serve needs a one-time permission: ssh -t ' + $Ssh + ' "sudo tailscale set --operator=$(whoami)" (then deploy again). Until then: LAN only.') 'Yellow' }
if ($Mode -eq 'Discover') { Say 'Discover only: nothing was changed.' 'Green'; exit 0 }

# --- Deploy ---------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Deploy') {
    Step 'Plan: Deploy'
    if ($hiExists -and -not $hiOurs) { Say ('REFUSED: a container named ' + $Name + ' already exists on ' + $where + ' and is not this deploy''s (' + $hi + '). Nothing was changed.') 'Red'; exit 1 }
    $taken = @($disc.Listening | Where-Object { $_ -match (':' + $Port + '$') })
    if ($taken -and -not $hiRunning) { Say ('REFUSED: port ' + $Port + ' is in use on ' + $where + ' by something that is not sl-homeip (' + ($taken -join '; ') + '). Nothing was changed.') 'Red'; exit 1 }
    Say ('  1. copy docker-compose.yml and homeip.py to ' + $root)
    Say ('  2. docker compose -p ' + $Project + ' up -d (python:3-alpine; ' + $hostAddr + ':' + $Port + ' and 127.0.0.1:' + $loopPort + ' only), and wait for ' + $ipUrl + ' to answer an address')
    if ($canServe) { Say ('  3. tailscale serve --bg --http=' + $servePort + ' http://127.0.0.1:' + $serveLoop + '  (tailnet only: ' + $tailnetUrl + ')') } else { Say '  3. Tailscale Serve: skipped (above)' }
    Say '  If 2 fails, sl-homeip is stopped again.'
    Confirm-Go 'deploy'

    Step 'Deploy'
    $r = Invoke-Haven ('mkdir -p ' + $root + ' && echo "folder ready"')
    if ($r.Code -ne 0) { Say ('FAILED: could not make ' + $root + '. Nothing was changed.') 'Red'; exit 1 }
    Copy-ToHaven (Join-Path $src 'docker-compose.yml') ($root + '/docker-compose.yml')
    Copy-ToHaven (Join-Path $src 'homeip.py') ($root + '/homeip.py')
    Say '  files copied'
    $u = Compose 'up -d --force-recreate'
    $ip = $(if ($u.Code -eq 0) { Wait-Ip 90 } else { $null })
    if (-not $ip) {
        Say ('FAILED: ' + $(if ($u.Code -ne 0) { 'docker compose up failed' } else { $ipUrl + ' gave no address in 90 seconds' })) 'Red'
        Compose 'stop' | Out-Null
        Say 'sl-homeip is stopped.' 'Yellow'
        exit 1
    }
    Say ('  ' + $ipUrl + ' answers ' + $ip) 'Green'
    if ($canServe) {
        $s = Invoke-Haven ('tailscale serve --bg --http=' + $servePort + ' http://127.0.0.1:' + $serveLoop)
        if ($s.Code -ne 0) { Say ('  serve ' + $servePort + ' FAILED (above). The LAN address still works.') 'Red' } else { Say ('  serve ' + $servePort + ' -> http://127.0.0.1:' + $serveLoop + ' (homeip): ' + $tailnetUrl) 'Green' }
    } elseif (-not $Local) { Say '  Tailscale Serve skipped (see Discover); sl-homeip is on the LAN only for now.' 'Yellow' }
    Say 'Deployed. Next, on the game PC: recreate sl-ddns from docker\uo\docker-compose.yml (notes\cc-P668-ddns-home-ip.md).' 'Green'
    exit 0
}

# --- Rollback -------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Rollback') {
    Step 'Plan: Rollback'
    if ($hiExists -and -not $hiOurs) { Say ('REFUSED: the container named ' + $Name + ' is not this deploy''s (' + $hi + '). Nothing was changed.') 'Red'; exit 1 }
    Say ('  1. docker compose -p ' + $Project + ' stop on ' + $where + ' (' + $(if ($hiExists) { $hi } else { 'nothing runs' }) + ')')
    Say ('  2. tailscale serve --http=' + $servePort + ' off' + $(if ($canServe) { '' } else { ': skipped' }))
    Say '  sl-ddns then gets no address and leaves the record alone (safe; the record stops following the house).' 'Yellow'
    Confirm-Go 'roll back'
    Step 'Rollback'
    if ($facts['folder'] -eq 'yes') { Compose 'stop' | Out-Null } elseif ($hiExists) { Invoke-Haven ('docker stop ' + $Name) | Out-Null }
    if ($canServe) { Invoke-Haven ('tailscale serve --http=' + $servePort + ' off') | Out-Null }
    Say 'Rolled back: sl-homeip is stopped.' 'Green'
    exit 0
}
