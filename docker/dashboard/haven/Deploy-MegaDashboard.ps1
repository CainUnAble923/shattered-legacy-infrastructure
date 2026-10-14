#Requires -Version 5.1
<#
  cc-P110 (P89 H5, widened): the one Homepage on Haven (Shattered Legacy, Sunflower Custom Gifts, home lab).

      Deploy-MegaDashboard.ps1 -Mode Discover|Deploy|Rollback [-DryRun] [-Yes] [-SetUrls]

  Chase runs it from the Shard Console's Websites tab (P101's one-click Haven deploys, after the phrase deploy to haven):
  mega-dashboard is Deploy, mega-dashboard-rollback is Rollback, each a one-line launcher in D:\UO\haven-migration. CC
  never runs it against Haven. It reaches Haven with ssh and scp as Chase (his key); every mode starts with Discover,
  which only reads and prints, then the plan, then waits for a typed word (anything else stops, changing nothing).
  -DryRun stops after the plan. -Yes skips the typed word (the proof only).

  Discover  prints Haven's running containers and listening ports (and anything that looks like Spoolman or a graphing
            app), the stale Homepage (decision 9), whether ~/sl-home and its dashboard.env exist, and this PC's
            sl-dashboard. Changes nothing.
  Deploy    copies docker/dashboard/haven (compose and config) to ~/sl-home; writes ~/sl-home/dashboard.env the first
            time (chmod 600; the ops token copied from ~/sl-ops/ops.env on Haven itself, never through this PC or the
            screen; the Spoolman and graphing app URLs asked here, empty allowed; -SetUrls asks again); stops the stale
            Homepage (stopped, not removed: ~/shattered-legacy is left alone); starts Homepage and its read-only socket
            proxy; waits for the page; then stops this PC's sl-dashboard (stopped, not removed). Any failure before the
            page answers stops the new one and starts the stale one again; this PC's dashboard is untouched.
  Rollback  stops the new Homepage and its proxy, starts the stale one on Haven again, and starts this PC's
            sl-dashboard.

  Never touched: Vaultwarden, NPM and its hosts, the website, the wiki, Kevin's Parts, ~/shattered-legacy's files,
  ~/sl-ops (read: ops.env's token line, on Haven only). Refuses to run as SYSTEM. ASCII only.

  -Local (the proof only): the Haven half runs on this PC's Docker under -Root (a scratch or proof folder), through Git
  Bash, with scratch names and port; -StaleName and -PcDashboard name scratch stand-ins, never sl-dashboard.
#>
[CmdletBinding()]
param(
    [ValidateSet('Discover', 'Deploy', 'Rollback')][string]$Mode = 'Discover',
    [switch]$DryRun,
    [switch]$Yes,
    [switch]$SetUrls,
    [string]$Ssh = 'chase@192.168.1.61',
    [string]$HavenRoot = '/home/chase/sl-home',
    [string]$OpsEnv = '/home/chase/sl-ops/ops.env',
    [string]$Bind = '192.168.1.61',
    [int]$Port = 3000,
    [string]$Project = 'sl-home',
    [string]$Name = 'sl-homepage',
    [string]$ProxyName = 'sl-homepage-dockerproxy',
    [string]$Network = 'sl-home-net',
    [string]$StaleName = 'sl-dashboard',
    [string]$PcDashboard = 'sl-dashboard',
    [string]$AllowedHosts,
    # The proof's switches.
    [switch]$Local,
    [string]$Root,
    [string]$SpoolmanUrl,
    [string]$GraphsUrl
)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, with his SSH key, never as SYSTEM.' -ForegroundColor Red; exit 2 }
$src = $PSScriptRoot
$bash = 'C:\Program Files\Git\bin\bash.exe'
if ($Local) {
    if (-not $Root -or $Root -notmatch '(?i)scratch|proof') { throw '-Local runs only under a -Root scratch or proof folder' }
    if ($StaleName -eq 'sl-dashboard' -or $PcDashboard -eq 'sl-dashboard') { throw '-Local needs scratch -StaleName and -PcDashboard, never sl-dashboard' }
    # PowerShell names ignore case: $root and $Root are ONE variable, so the Windows path is kept first (cc-P109's bug).
    $winRoot = $Root
    $root = '/' + $winRoot.Substring(0, 1).ToLower() + ($winRoot.Substring(2) -replace '\\', '/')
    $composeRoot = $winRoot -replace '\\', '/'
    $OpsEnv = $root + '/ops.env'
} else {
    $root = $HavenRoot
    $composeRoot = $HavenRoot
}
$hostAddr = $(if ($Local) { '127.0.0.1' } else { $Bind })
$pageUrl = 'http://' + $hostAddr + ':' + $Port + '/'
if (-not $AllowedHosts) { $AllowedHosts = $hostAddr + ':' + $Port + ',' + $hostAddr + $(if ($Local) { '' } else { ',haven:' + $Port }) + ',localhost:' + $Port }
$where = $(if ($Local) { 'this PC (proof, ' + $winRoot + ')' } else { 'Haven (' + $Ssh + ')' })

function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Step([string]$t) { Write-Host ('== ' + $t) -ForegroundColor Cyan }
function Invoke-Haven {
    # One POSIX shell command on Haven (ssh as Chase) or, with -Local, in Git Bash here; base64 on the wire because
    # Windows PowerShell 5.1 mangles quotes in a native command's arguments (docker/ops/Deploy-HavenOps.ps1). Stdin (the
    # URLs only) is never part of a command line.
    param([string]$Command, [switch]$Quiet, [string]$InputText)
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('set +f; ' + $Command))
    $wire = 'set -f; eval $(echo ' + $b64 + ' | base64 -d)'
    $ErrorActionPreference = 'Continue'
    # A pipe into a native command uses $OutputEncoding. Plain powershell.exe has us-ascii, but a host set to UTF-8 can
    # add a byte-order mark (it broke dashboard.env's first variable name in the proof), so it is pinned here and the
    # receiving side strips a mark as well (the env file's sed).
    $OutputEncoding = New-Object System.Text.UTF8Encoding $false
    if ($Local) { if ($InputText) { $out = $InputText | & $bash -c $wire 2>&1 } else { $out = & $bash -c $wire 2>&1 } }
    else { if ($InputText) { $out = $InputText | & ssh -o BatchMode=yes $Ssh $wire 2>&1 } else { $out = & ssh -o BatchMode=yes $Ssh $wire 2>&1 } }
    $code = $LASTEXITCODE
    $lines = @($out | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { [string]$_.Exception.Message } else { [string]$_ } })
    if (-not $Quiet) { foreach ($l in $lines) { Write-Host ('    | ' + $l) } }
    [pscustomobject]@{ Code = $code; Lines = $lines }
}
function Copy-ToHaven([string]$From, [string]$To) {
    if ($Local) { Copy-Item -LiteralPath $From -Destination ($winRoot + ($To.Substring($root.Length) -replace '/', '\')) -Force }
    else { & scp -q -o BatchMode=yes $From ($Ssh + ':' + $To); if ($LASTEXITCODE -ne 0) { throw ('scp failed: ' + $From) } }
}
$composeEnv = 'DASH_ROOT=' + $composeRoot + ' DASH_BIND=' + $(if ($Local) { '127.0.0.1' } else { '0.0.0.0' }) + ' DASH_PORT=' + $Port +
    ' DASH_PROJECT=' + $Project + ' DASH_NAME=' + $Name + ' DASH_PROXY_NAME=' + $ProxyName + ' DASH_NETWORK=' + $Network +
    ' DASH_ALLOWED_HOSTS=' + $AllowedHosts
function Compose([string]$What) { Invoke-Haven ('cd ' + $root + ' && ' + $composeEnv + ' docker compose -p ' + $Project + ' ' + $What) }
function Wait-Http([string]$Url, [int]$Seconds = 120) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try { $r = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5; if ($r.StatusCode -eq 200) { return $r } } catch { }
        Start-Sleep -Seconds 3
    }
    $null
}
function Get-PcDashboardState { $s = ((& docker inspect -f '{{.State.Status}}' $PcDashboard 2>$null) | Out-String).Trim(); if ($s) { $s } else { 'none' } }
function Confirm-Go([string]$Word) {
    if ($DryRun) { Say 'DRY RUN: stopping here; nothing was changed.' 'Yellow'; exit 0 }
    if ($Yes) { return }
    $typed = Read-Host ("Type '" + $Word + "' to do the above (anything else stops, changing nothing)")
    if ($typed -cne $Word) { Say 'Stopped: nothing was changed.' 'Yellow'; exit 0 }
}

# --- Discover (every mode starts here; it only reads) ---------------------------------------------------------------
Step ('Discover: ' + $where + ' (read only)')
$probe = Invoke-Haven -Quiet ('echo host=$(hostname); docker --version; docker compose version; ' +
    'echo "-- running containers"; docker ps --format "  {{.Names}}  {{.Status}}  {{.Ports}}"; ' +
    'echo "-- listening ports"; if command -v ss >/dev/null 2>&1; then ss -ltnH | awk ''{print "  " $4}''; else netstat -an | grep LISTEN | awk ''{print "  " $2}''; fi | sort -u | head -60; ' +
    'echo "-- facts"; ' +
    'docker inspect -f "stale homepage: {{.State.Status}} {{index .Config.Labels \"com.docker.compose.project.working_dir\"}}" ' + $StaleName + ' 2>/dev/null || echo "stale homepage: none"; ' +
    'docker inspect -f "new homepage: {{.State.Status}}" ' + $Name + ' 2>/dev/null || echo "new homepage: none"; ' +
    'test -d ' + $root + '/config && echo "folder: yes" || echo "folder: no"; ' +
    'test -f ' + $root + '/dashboard.env && echo "env file: yes" || echo "env file: no"; ' +
    'grep -q "^PLAYTESTS_TOKEN=" ' + $OpsEnv + ' 2>/dev/null && echo "ops token: yes" || echo "ops token: no"; ' +
    'c=$(docker ps -a --format "{{.Names}} {{.Image}}" | grep -i -E "spool|grafana|influx|prometheus|chart|graph" | tr "\n" ";"); echo "sunflower candidates: ${c:-none}"')
foreach ($l in $probe.Lines) { Say ('    ' + $l) }
if ($probe.Code -ne 0 -and -not ($probe.Lines -match '^host=')) { Say ('Cannot reach ' + $where + '. Nothing was changed.') 'Red'; exit 1 }
$facts = @{}
foreach ($l in $probe.Lines) { if ($l -match '^([a-z ]+[a-z]): (.*)$') { $facts[$Matches[1]] = $Matches[2] } }
$stale = [string]$facts['stale homepage']
$staleRunning = $stale -like 'running*'
$pcState = Get-PcDashboardState
Say ('    this PC: ' + $PcDashboard + ' is ' + $pcState)
if ($Mode -eq 'Discover') { Say 'Discover only: nothing was changed.' 'Green'; exit 0 }

# --- Deploy ---------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Deploy') {
    Step 'Plan: Deploy'
    Say ('  1. mkdir -p ' + $root + '/config/icons; copy docker-compose.yml and config\* from ' + $src)
    if ($facts['env file'] -ne 'yes' -or $SetUrls) {
        Say ('  2. write ' + $root + '/dashboard.env (chmod 600): the ops token ' + $(if ($facts['ops token'] -eq 'yes') { 'copied from ' + $OpsEnv + ' on Haven itself' } else { 'NOT FOUND in ' + $OpsEnv + ' (the Game PC tiles will say so until the ops pages are deployed)' }) + ', and the Spoolman and graphing app URLs, asked next (empty allowed)')
    } else { Say ('  2. ' + $root + '/dashboard.env: kept (-SetUrls asks the URLs again)') }
    if ($staleRunning) { Say ('  3. docker stop ' + $StaleName + ' (the stale Homepage, ' + $stale + '): stopped, not removed') } else { Say ('  3. stale Homepage: ' + $stale + ' (nothing to stop)') }
    Say ('  4. docker compose -p ' + $Project + ' up -d (Homepage v1.13.1 and the read-only socket proxy), and wait for ' + $pageUrl)
    Say ('  5. docker stop ' + $PcDashboard + ' on this PC (now ' + $pcState + '): stopped, not removed')
    Say '  If 4 fails, the new Homepage is stopped and the stale one started again; this PC''s dashboard is not touched.'
    Confirm-Go 'deploy'
    $sp = $SpoolmanUrl; $gr = $GraphsUrl
    if (($facts['env file'] -ne 'yes' -or $SetUrls) -and -not $Yes) {
        $sp = Read-Host 'Spoolman URL (for example http://192.168.1.x:7912), or Enter to leave it empty'
        $gr = Read-Host 'Graphing app URL, or Enter to leave it empty'
    }
    foreach ($u in @($sp, $gr)) { if ($u -and $u -notmatch '^https?://[A-Za-z0-9.:/_-]+$') { throw ('not a plain http(s) URL: ' + $u) } }

    Step 'Deploy'
    $r = Invoke-Haven ('mkdir -p ' + $root + '/config/icons')
    if ($r.Code -ne 0) { throw 'mkdir failed' }
    Copy-ToHaven (Join-Path $src 'docker-compose.yml') ($root + '/docker-compose.yml')
    foreach ($f in Get-ChildItem -LiteralPath (Join-Path $src 'config') -File) { Copy-ToHaven $f.FullName ($root + '/config/' + $f.Name) }
    foreach ($f in Get-ChildItem -LiteralPath (Join-Path $src 'config\icons') -File) { Copy-ToHaven $f.FullName ($root + '/config/icons/' + $f.Name) }
    Say '  compose and config copied'
    if ($facts['env file'] -ne 'yes' -or $SetUrls) {
        # The token never leaves Haven: it is read from ops.env there. The URLs arrive on stdin.
        $cmd = 'umask 077; t=$(grep "^PLAYTESTS_TOKEN=" ' + $OpsEnv + ' 2>/dev/null | head -1 | cut -d= -f2-); ' +
            '{ echo "HOMEPAGE_VAR_OPS_TOKEN=$t"; tr -d "\r" | sed "1s/^\xEF\xBB\xBF//"; } > ' + $root + '/dashboard.env && chmod 600 ' + $root + '/dashboard.env && echo "env file written"'
        $w = Invoke-Haven -InputText ("HOMEPAGE_VAR_SPOOLMAN_URL=" + $sp + "`nHOMEPAGE_VAR_GRAPHS_URL=" + $gr) $cmd
        if ($w.Code -ne 0) { throw 'writing dashboard.env failed' }
    }
    try {
        if ($staleRunning) { Invoke-Haven ('docker stop ' + $StaleName) | Out-Null }
        $u = Compose 'up -d'
        if ($u.Code -ne 0) { throw 'docker compose up failed' }
        if (-not (Wait-Http $pageUrl 120)) { throw ('Homepage did not answer at ' + $pageUrl + ' in 2 minutes') }
        # Homepage 1.13.1 first serves the page pre-built into its image (its sample groups) and rebuilds it only when
        # a browser sees the config hash change; found in the proof. /api/revalidate rebuilds it from this config now.
        try { Invoke-WebRequest -UseBasicParsing -Uri ($pageUrl + 'api/revalidate') -TimeoutSec 30 | Out-Null } catch { }
        $page = Wait-Http $pageUrl 30
        if (-not $page -or $page.Content -notmatch 'Sunflower Custom Gifts') { throw ('Homepage answers at ' + $pageUrl + ' but not with this config') }
        Say ('  Homepage answers at ' + $pageUrl + ' with the three groups') 'Green'
    } catch {
        Say ('FAILED: ' + $_.Exception.Message) 'Red'
        Compose 'stop' | Out-Null
        if ($staleRunning) { Invoke-Haven ('docker start ' + $StaleName) | Out-Null; Say ('  ' + $StaleName + ' started again') 'Yellow' }
        Say ('This PC''s ' + $PcDashboard + ' was not touched.') 'Yellow'
        exit 1
    }
    if ($pcState -eq 'running') { & docker stop $PcDashboard 2>&1 | ForEach-Object { Say ('    | ' + $_) }; Say ('  ' + $PcDashboard + ' on this PC stopped (not removed)') }
    Say ''
    Say 'Deployed. What you should see now:' 'Green'
    Say ('  - ' + $pageUrl + ' : three groups, Shattered Legacy, Sunflower Custom Gifts and Home lab, with the menu on top.')
    Say '  - Haven''s containers show running or stopped; the game PC tiles come from its snapshot.'
    Say '  - Spoolman and the graphing app are placeholders until their URLs are set (-SetUrls, or edit dashboard.env).'
    exit 0
}

# --- Rollback -------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Rollback') {
    Step 'Plan: Rollback'
    Say ('  1. docker compose -p ' + $Project + ' stop on ' + $where + ' (Homepage and its proxy; files kept)')
    Say ('  2. docker start ' + $StaleName + ' on ' + $where + ' (' + $stale + ')')
    Say ('  3. docker start ' + $PcDashboard + ' on this PC (' + $pcState + ')')
    Confirm-Go 'roll back'
    Step 'Rollback'
    Compose 'stop' | Out-Null
    if ($stale -ne 'none') { Invoke-Haven ('docker start ' + $StaleName) | Out-Null }
    if ($pcState -ne 'none') { & docker start $PcDashboard 2>&1 | ForEach-Object { Say ('    | ' + $_) } }
    Say ('Rolled back: the new Homepage is stopped; ' + $StaleName + ' and this PC''s ' + $PcDashboard + ' are started again.') 'Green'
    exit 0
}
