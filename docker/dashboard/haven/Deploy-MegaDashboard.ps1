#Requires -Version 5.1
<#
  cc-P110 (P89 H5, widened), finished by cc-P133: the one Homepage on Haven, the home intranet, Tailscale only.

      Deploy-MegaDashboard.ps1 -Mode Discover|Deploy|Rollback [-DryRun] [-Yes]

  Chase runs it from the Shard Console's Websites tab (P101's one-click Haven deploys, after the phrase deploy to haven):
  mega-dashboard is Deploy, mega-dashboard-rollback is Rollback, each a one-line launcher in D:\UO\haven-migration. CC
  never runs it against Haven. It reaches Haven with ssh and scp as Chase (his key); every mode starts with Discover,
  which only reads and prints, then the plan, then waits for a typed word (anything else stops, changing nothing).
  -DryRun stops after the plan. -Yes skips the typed word (the proof only).

  Discover  prints every container on Haven (name, state, image, ports, compose folder), every running container's
            mounts (cc-P163), every listening port (ss -tlnp), Tailscale (installed, its address, who may change Serve,
            what Serve does now), the stale Homepage, ~/sl-home and its dashboard.env, and this PC's sl-dashboard. Then
            checks it against docker/haven/services.json: a running container the inventory does not list is named (and
            gets a tile anyway), as is an inventory container Haven does not have; and (cc-P163) any running container
            whose published ports or image differ from its entry, with the mounts of entries whose volumes say "from
            Discover". The whole discovery is kept in D:\UO\haven-ops\discovery-latest.txt, outside both repos, where
            the console fact P133_TheDiscoveryIsParsed... fails until the inventory lists everything.
            Changes nothing.
  Deploy    builds the config from docker/haven/services.json (Build-HavenDashboard.ps1, with this discovery); backs up
            what is on Haven first (~/sl-home/backup-<time>: compose, config, dashboard.env); copies the compose and the
            config to ~/sl-home; makes sure dashboard.env (chmod 600) has every name the tiles read, adding a missing one
            EMPTY and never touching a value (the ops token is copied from ~/sl-ops/ops.env on Haven itself; the Grafana
            and Portainer ones Chase pastes, see notes/haven-runbook.md); stops the stale Homepage (stopped, not
            removed); starts Homepage and its read-only socket proxy on Haven's LAN address and loopback only; waits for
            the page; points Tailscale Serve at the loopback ports listed in services.json (tailscaleServe: the tailnet's
            80 to Homepage, 8093 to the ops page, 8787 to the print queue, each only once something answers there);
            checks http://haven.tail81098d.ts.net/ from this PC (it is on the tailnet); then stops this PC's
            sl-dashboard. Any failure before the page answers stops the new one and starts the stale one again.
  Rollback  stops the new Homepage and its proxy, turns off Serve's port 80 (the ops page and print queue keep theirs),
            starts the stale one on Haven again, and starts this PC's sl-dashboard.

  Never touched: Vaultwarden, NPM and its hosts, the website, the wiki, Spoolman and its data, Kevin's Parts,
  ~/shattered-legacy's files, ~/sl-ops (read: ops.env's token line, on Haven only). Never Tailscale Funnel: nothing here is
  ever public. Never prints a token. Refuses to run as SYSTEM. ASCII only.

  -Local (the proof only): the Haven half runs on this PC's Docker under -Root (a scratch or proof folder), through Git
  Bash, with scratch names and ports; -StaleName and -PcDashboard name scratch stand-ins, never sl-dashboard; -ProofBin
  puts a stand-in tailscale first on the PATH.
#>
[CmdletBinding()]
param(
    [ValidateSet('Discover', 'Deploy', 'Rollback')][string]$Mode = 'Discover',
    [switch]$DryRun,
    [switch]$Yes,
    [string]$Ssh = 'chase@192.168.1.61',
    [string]$HavenRoot = '/home/chase/sl-home',
    [string]$OpsEnv = '/home/chase/sl-ops/ops.env',
    [string]$Bind = '192.168.1.61',
    [int]$Port = 3000,
    [int]$LoopPort = 0,
    [string]$Project = 'sl-home',
    [string]$Name = 'sl-homepage',
    [string]$ProxyName = 'sl-homepage-dockerproxy',
    [string]$Network = 'sl-home-net',
    [string]$StaleName = 'sl-dashboard',
    [string]$PcDashboard = 'sl-dashboard',
    [string]$AllowedHosts,
    [string]$Inventory,
    [string]$DiscoveryOut = 'D:\UO\haven-ops\discovery-latest.txt',
    # The proof's switches.
    [switch]$Local,
    [string]$Root,
    [string]$ProofBin
)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, with his SSH key, never as SYSTEM.' -ForegroundColor Red; exit 2 }
$src = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $src '..\..\..')).Path
$bash = 'C:\Program Files\Git\bin\bash.exe'
. (Join-Path $repo 'docker\haven\HavenInventory.ps1')
if (-not $Inventory) { $Inventory = Join-Path $repo 'docker\haven\services.json' }
$inv = Read-HavenInventory -Path $Inventory
if ($inv.Problems.Count) { foreach ($p in $inv.Problems) { Write-Host ('INVENTORY: ' + $p) -ForegroundColor Red }; Write-Host 'docker\haven\services.json is unsound; nothing was read or changed.' -ForegroundColor Red; exit 2 }
$tailnetName = [string]$inv.hosts.haven.tailnet
if ($Local) {
    if (-not $Root -or $Root -notmatch '(?i)scratch|proof') { throw '-Local runs only under a -Root scratch or proof folder' }
    if ($StaleName -eq 'sl-dashboard' -or $PcDashboard -eq 'sl-dashboard') { throw '-Local needs scratch -StaleName and -PcDashboard, never sl-dashboard' }
    if ($DiscoveryOut -notmatch '(?i)scratch|proof') { $DiscoveryOut = Join-Path $Root 'discovery-latest.txt' }
    # PowerShell names ignore case: $root and $Root are ONE variable, so the Windows path is kept first (cc-P109's bug).
    $winRoot = $Root
    $root = '/' + $winRoot.Substring(0, 1).ToLower() + ($winRoot.Substring(2) -replace '\\', '/')
    $composeRoot = $winRoot -replace '\\', '/'
    $OpsEnv = $root + '/ops.env'
    if (-not $LoopPort) { $LoopPort = $Port + 1 }
} else {
    $root = $HavenRoot
    $composeRoot = $HavenRoot
    if (-not $LoopPort) { $LoopPort = $Port }
}
$hostAddr = $(if ($Local) { '127.0.0.1' } else { $Bind })
$pageUrl = 'http://' + $hostAddr + ':' + $Port + '/'
$tailnetUrl = $(if ($Local) { 'http://127.0.0.1:' + $LoopPort + '/' } else { 'http://' + $tailnetName + '/' })
if (-not $AllowedHosts) {
    $AllowedHosts = $(if ($Local) { '127.0.0.1:' + $Port + ',127.0.0.1:' + $LoopPort + ',localhost:' + $Port }
        else { $Bind + ':' + $Port + ',' + $Bind + ',haven:' + $Port + ',' + $tailnetName + ',' + $tailnetName + ':80,' + [string]$inv.hosts.haven.tailnetIp + ',127.0.0.1:' + $LoopPort + ',localhost:' + $Port })
}
$where = $(if ($Local) { 'this PC (proof, ' + $winRoot + ')' } else { 'Haven (' + $Ssh + ')' })
# Every name the generated tiles read from dashboard.env. Values are never written here, read back, or printed.
$envNames = @('HOMEPAGE_VAR_OPS_TOKEN', 'HOMEPAGE_VAR_GRAFANA_TOKEN', 'HOMEPAGE_VAR_PORTAINER_KEY', 'HOMEPAGE_VAR_PORTAINER_ENV')

function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Step([string]$t) { Write-Host ('== ' + $t) -ForegroundColor Cyan }
function Invoke-Haven {
    # One POSIX shell command on Haven (ssh as Chase) or, with -Local, in Git Bash here; base64 on the wire because
    # Windows PowerShell 5.1 mangles quotes in a native command's arguments (docker/ops/Deploy-HavenOps.ps1). Stdin is
    # never part of a command line.
    param([string]$Command, [switch]$Quiet, [string]$InputText)
    $pre = $(if ($Local -and $ProofBin) { 'export PATH="' + ('/' + $ProofBin.Substring(0, 1).ToLower() + ($ProofBin.Substring(2) -replace '\\', '/')) + ':$PATH"; ' } else { '' })
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('set +f; ' + $pre + $Command))
    $wire = 'set -f; eval $(echo ' + $b64 + ' | base64 -d)'
    $ErrorActionPreference = 'Continue'
    # A pipe into a native command uses $OutputEncoding; pinned so no byte-order mark reaches Haven (cc-P110's bug 1).
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
# Never all interfaces: Haven's LAN address and its loopback (the proof: two loopback ports).
$composeEnv = 'DASH_ROOT=' + $composeRoot + ' DASH_BIND=' + $hostAddr + ' DASH_PORT=' + $Port + ' DASH_LOOP_PORT=' + $LoopPort +
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
function Get-ServePlan {
    # services.json's tailscaleServe, each with the loopback port it forwards to (the proof's Homepage: its scratch port).
    foreach ($e in @($inv.tailscaleServe)) {
        $t = [string]$e.target
        if ($t -notmatch '^http://127\.0\.0\.1:(\d+)$') { throw ('services.json tailscaleServe: only loopback targets, not ' + $t) }
        $p = [int]$Matches[1]
        if ($Local -and $e.service -eq 'homepage') { $p = $LoopPort }
        [pscustomobject]@{ TailnetPort = [int]$e.tailnetPort; LoopPort = $p; Target = ('http://127.0.0.1:' + $p); Service = [string]$e.service }
    }
}
$serve = @(Get-ServePlan)

# --- Discover (every mode starts here; it only reads) ---------------------------------------------------------------
Step ('Discover: ' + $where + ' (read only)')
$probeOpen = (($serve | ForEach-Object { 'if (exec 3<>/dev/tcp/127.0.0.1/' + $_.LoopPort + ') 2>/dev/null; then echo "loopback ' + $_.LoopPort + ': open"; else echo "loopback ' + $_.LoopPort + ': closed"; fi' }) -join '; ')
$probe = Invoke-Haven -Quiet ('echo host=$(hostname); docker --version; docker compose version; ' +
    'echo "-- containers"; docker ps -a --format "  {{.Names}}|{{.State}}|{{.Status}}|{{.Image}}|{{.Ports}}|{{.Label \"com.docker.compose.project.working_dir\"}}"; ' +
    'echo "-- listening ports"; if command -v ss >/dev/null 2>&1; then ss -tlnp | tail -n +2 | awk ''{print "  " $4 "  " $6}''; else netstat -an | grep LISTEN | awk ''{print "  " $2}''; fi | sort -u | head -80; ' +
    'echo "-- mounts"; for c in $(docker ps -q); do docker inspect -f ''  {{.Name}}|{{range .Mounts}}{{if eq .Type "volume"}}{{.Name}}{{else}}{{.Source}}{{end}}>{{.Destination}};{{end}}'' $c; done; ' +
    'echo "-- tailscale"; if command -v tailscale >/dev/null 2>&1; then echo "  installed: yes"; echo "  ip: $(tailscale ip -4 2>/dev/null | head -1)"; ' +
    'o=$(tailscale debug prefs 2>/dev/null | grep -o "\"OperatorUser\": *\"[^\"]*\"" | sed "s/.*: *//; s/\"//g"); echo "  operator: ${o:-none}"; echo "  me: $(id -un)"; ' +
    'tailscale serve status 2>&1 | head -20 | sed "s/^/  serve now=  /"; else echo "  installed: no"; fi; ' +
    'echo "-- facts"; ' + $probeOpen + '; ' +
    'docker inspect -f "stale homepage: {{.State.Status}} {{index .Config.Labels \"com.docker.compose.project.working_dir\"}}" ' + $StaleName + ' 2>/dev/null || echo "stale homepage: none"; ' +
    'docker inspect -f "new homepage: {{.State.Status}}" ' + $Name + ' 2>/dev/null || echo "new homepage: none"; ' +
    'test -d ' + $root + '/config && echo "folder: yes" || echo "folder: no"; ' +
    'test -f ' + $root + '/dashboard.env && echo "env file: yes" || echo "env file: no"; ' +
    'grep -q "^PLAYTESTS_TOKEN=" ' + $OpsEnv + ' 2>/dev/null && echo "ops token: yes" || echo "ops token: no"')
foreach ($l in $probe.Lines) { Say ('    ' + $l) }
if ($probe.Code -ne 0 -and -not ($probe.Lines -match '^host=')) { Say ('Cannot reach ' + $where + '. Nothing was changed.') 'Red'; exit 1 }
$discText = ($probe.Lines -join "`n") + "`n"
try {
    $dd = Split-Path $DiscoveryOut -Parent
    if (-not (Test-Path -LiteralPath $dd)) { New-Item -ItemType Directory -Path $dd | Out-Null }
    [IO.File]::WriteAllText($DiscoveryOut, ('# Discover, ' + $where + ', ' + (Get-Date -Format 's') + ' (Deploy-MegaDashboard.ps1, cc-P133)' + "`n" + $discText), (New-Object Text.UTF8Encoding $false))
    Say ('    kept: ' + $DiscoveryOut)
} catch { Say ('    could not keep the discovery: ' + $_.Exception.Message) 'Yellow' }
$facts = @{}
foreach ($l in $probe.Lines) { if ($l -match '^\s*([a-z][a-z0-9 ]*[a-z0-9]): (.*)$') { $facts[$Matches[1]] = $Matches[2] } }
$disc = ConvertFrom-HavenDiscovery -Text $discText
$cov = Test-HavenInventoryCoverage -Inventory $inv -Discovery $disc
Step 'Inventory check (docker\haven\services.json)'
if ($cov.Missing) { Say ('  RUNNING ON HAVEN, NOT IN THE INVENTORY: ' + ($cov.Missing -join ', ') + '. They get a tile in Admin anyway; add them to services.json and the runbook.') 'Yellow' }
else { Say ('  every running container on ' + $where + ' is in the inventory (' + @($disc.Containers | Where-Object { $_.Running }).Count + ' running)') 'Green' }
if ($cov.Absent) { Say ('  in the inventory, not on Haven: ' + ($cov.Absent -join ', ') + ' (planned, or a name to correct in services.json)') }
# cc-P163: what each running container publishes and runs, against services.json, and the mounts of any entry whose
# volumes still say "from Discover", so the plan says what to fill in (and the runbook with it).
$portDiff = @(Compare-HavenDiscoveredPorts -Inventory $inv -Discovery $disc)
if ($portDiff.Count) {
    Say '  PORTS OR IMAGES DIFFER from services.json (fill them in there and in the runbook):' 'Yellow'
    foreach ($p in $portDiff) {
        Say ('    ' + $p.Name + $(if ($p.Key) { ' (' + $p.Key + ')' } else { ' (no entry)' }) + ': image ' + $p.Image + '; publishes ' + $(if ($p.Published.Count) { $p.Published -join ', ' } else { 'nothing' }) +
            $(if ($p.NotPublished.Count) { '; services.json also says ' + ($p.NotPublished -join ', ') } else { '' }) + $(if (-not $p.ImageOk) { '; services.json names another image' } else { '' })) 'Yellow'
    }
} else { Say '  ports and images: every running container matches services.json' 'Green' }
foreach ($s in @($inv.services | Where-Object { $_.host -eq 'haven' -and @(@($_.volumes) | Where-Object { [string]$_.host -like 'from Discover*' }).Count })) {
    $m = $disc.Mounts[[string]$s.container]
    if ($m) { Say ('  mounts of ' + $s.container + ' (for its volumes and backup in services.json): ' + ($m -join '; ')) }
}
$ts = $disc.Tailscale
$tsOk = ($ts['installed'] -eq 'yes')
$canServe = $tsOk -and ($ts['operator'] -eq $ts['me'] -or $ts['me'] -eq 'root')
if (-not $tsOk) { Say '  Tailscale is not installed on Haven: the dashboard is on the LAN only until it is (notes/haven-runbook.md).' 'Yellow' }
elseif (-not $canServe) { Say ('  Tailscale Serve needs a one-time permission: ssh -t ' + $Ssh + ' "sudo tailscale set --operator=$(whoami)" (then deploy again). Until then: LAN only.') 'Yellow' }
$stale = [string]$facts['stale homepage']
$staleRunning = $stale -like 'running*'
$pcState = Get-PcDashboardState
Say ('    this PC: ' + $PcDashboard + ' is ' + $pcState)
if ($Mode -eq 'Discover') { Say 'Discover only: nothing was changed.' 'Green'; exit 0 }

# --- Deploy ---------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Deploy') {
    Step 'Plan: Deploy'
    $taken = @($disc.Listening | Where-Object { ($_ -split '\s+')[0] -match (':' + $Port + '$') })
    if ($taken -and -not $staleRunning -and $facts['new homepage'] -notlike 'running*') { Say ('REFUSED: port ' + $Port + ' is in use on ' + $where + ' by something that is not a Homepage (' + ($taken -join '; ') + '). Nothing was changed.') 'Red'; exit 1 }
    Say ('  1. build the config from docker\haven\services.json (' + @($inv.services).Count + ' services, five sections' + $(if ($cov.Missing) { ', plus a tile for ' + ($cov.Missing -join ', ') } else { '' }) + ')')
    Say ('  2. back up ' + $root + ' (compose, config, dashboard.env) to ' + $root + '/backup-<time>, if there is anything to back up')
    Say ('  3. copy docker-compose.yml and the config to ' + $root)
    Say ('  4. dashboard.env (chmod 600): ' + $(if ($facts['env file'] -eq 'yes') { 'kept; any of ' + ($envNames -join ', ') + ' it lacks is added EMPTY' } else { 'made, with the ops token ' + $(if ($facts['ops token'] -eq 'yes') { 'copied from ' + $OpsEnv + ' on Haven itself' } else { 'NOT FOUND in ' + $OpsEnv }) + ' and the other three names empty' }) + '. No value is printed.')
    if ($staleRunning) { Say ('  5. docker stop ' + $StaleName + ' (the stale Homepage, ' + $stale + '): stopped, not removed') } else { Say ('  5. stale Homepage: ' + $stale + ' (nothing to stop)') }
    Say ('  6. docker compose -p ' + $Project + ' up -d (Homepage v1.13.1 on ' + $hostAddr + ':' + $Port + ' and 127.0.0.1:' + $LoopPort + ', never all interfaces; the read-only socket proxy), and wait for ' + $pageUrl)
    if ($canServe) { foreach ($e in $serve) { Say ('  7. tailscale serve --bg --http=' + $e.TailnetPort + ' ' + $e.Target + '  (' + $e.Service + $(if ($facts[('loopback ' + $e.LoopPort)] -eq 'open' -or $e.Service -eq 'homepage') { '' } else { ': nothing answers there yet, so skipped' }) + ')') } }
    else { Say '  7. Tailscale Serve: skipped (above)' }
    Say ('  8. check ' + $tailnetUrl + ' from this PC')
    Say ('  9. docker stop ' + $PcDashboard + ' on this PC (now ' + $pcState + '): stopped, not removed')
    Say '  If 6 fails, the new Homepage is stopped and the stale one started again; this PC''s dashboard is not touched.'
    Confirm-Go 'deploy'

    Step 'Deploy'
    $gen = Join-Path ([IO.Path]::GetTempPath()) ('sl-home-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $gen | Out-Null
    try {
        Copy-Item -Path (Join-Path $src 'config\*') -Destination $gen -Recurse -Force
        $dfile = Join-Path $gen 'discovery.txt'
        [IO.File]::WriteAllText($dfile, $discText)
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'docker\haven\Build-HavenDashboard.ps1') -OutDir $gen -Discovery $dfile -Inventory $Inventory
        if ($LASTEXITCODE -ne 0) { throw 'building the config from services.json failed' }
        Remove-Item -LiteralPath $dfile
        $r = Invoke-Haven ('mkdir -p ' + $root + '/config/icons && cd ' + $root + ' && if [ -f docker-compose.yml ] || [ -f dashboard.env ] || [ -n "$(ls -A config 2>/dev/null)" ]; then ' +
            'b=backup-$(date +%Y%m%d-%H%M%S); mkdir -p $b && chmod 700 $b; for f in docker-compose.yml dashboard.env; do [ -f $f ] && cp -p $f $b/; done; [ -d config ] && cp -pr config $b/; echo "backed up to ' + $root + '/$b"; else echo "nothing to back up yet"; fi')
        if ($r.Code -ne 0) { throw 'the backup on Haven failed; nothing was replaced' }
        Copy-ToHaven (Join-Path $src 'docker-compose.yml') ($root + '/docker-compose.yml')
        foreach ($f in Get-ChildItem -LiteralPath $gen -File) { Copy-ToHaven $f.FullName ($root + '/config/' + $f.Name) }
        foreach ($f in Get-ChildItem -LiteralPath (Join-Path $gen 'icons') -File) { Copy-ToHaven $f.FullName ($root + '/config/icons/' + $f.Name) }
    } finally { Remove-Item -LiteralPath $gen -Recurse -Force -ErrorAction SilentlyContinue }
    Say '  compose and config copied (config built from services.json)'
    # dashboard.env: the ops token is read from ops.env on Haven and never leaves it; every other name is added empty if
    # missing. Existing lines are never rewritten, so a token Chase pasted survives every deploy. Only set or EMPTY is shown.
    $cmd = 'umask 077; f=' + $root + '/dashboard.env; touch $f; chmod 600 $f; ' +
        'if ! grep -q "^HOMEPAGE_VAR_OPS_TOKEN=" $f; then t=$(grep "^PLAYTESTS_TOKEN=" ' + $OpsEnv + ' 2>/dev/null | head -1 | cut -d= -f2- | tr -d "\r"); echo "HOMEPAGE_VAR_OPS_TOKEN=$t" >> $f; echo "  added HOMEPAGE_VAR_OPS_TOKEN (from ops.env: $([ -n "$t" ] && echo yes || echo NOT FOUND))"; fi; ' +
        'for n in ' + (($envNames | Where-Object { $_ -ne 'HOMEPAGE_VAR_OPS_TOKEN' }) -join ' ') + '; do grep -q "^$n=" $f || { echo "$n=" >> $f; echo "  added $n (empty: paste it, see the runbook)"; }; done; ' +
        'for n in ' + ($envNames -join ' ') + '; do v=$(grep "^$n=" $f | head -1 | cut -d= -f2-); [ -n "$v" ] && echo "  $n: set" || echo "  $n: EMPTY"; done'
    $w = Invoke-Haven $cmd
    if ($w.Code -ne 0) { throw 'dashboard.env failed' }
    try {
        if ($staleRunning) { Invoke-Haven ('docker stop ' + $StaleName) | Out-Null }
        $u = Compose 'up -d --force-recreate'
        if ($u.Code -ne 0) { throw 'docker compose up failed' }
        if (-not (Wait-Http $pageUrl 120)) { throw ('Homepage did not answer at ' + $pageUrl + ' in 2 minutes') }
        # Homepage 1.13.1 first serves the page pre-built into its image (its sample groups) and rebuilds it only when
        # a browser sees the config hash change; found in cc-P110's proof. /api/revalidate rebuilds it from this config.
        try { Invoke-WebRequest -UseBasicParsing -Uri ($pageUrl + 'api/revalidate') -TimeoutSec 30 | Out-Null } catch { }
        $page = Wait-Http $pageUrl 30
        if (-not $page -or $page.Content -notmatch 'Ultima Online / Shattered Legacy') { throw ('Homepage answers at ' + $pageUrl + ' but not with this config') }
        Say ('  Homepage answers at ' + $pageUrl + ' with the five sections') 'Green'
    } catch {
        Say ('FAILED: ' + $_.Exception.Message) 'Red'
        Compose 'stop' | Out-Null
        if ($staleRunning) { Invoke-Haven ('docker start ' + $StaleName) | Out-Null; Say ('  ' + $StaleName + ' started again') 'Yellow' }
        Say ('This PC''s ' + $PcDashboard + ' was not touched. The backup on Haven is in ' + $root + '/backup-*.') 'Yellow'
        exit 1
    }
    # Tailscale Serve: tailnet only (never Funnel), each port forwarded to a loopback port, persisted by tailscaled so it
    # survives a restart. A target nothing answers on yet (the print queue before its deploy) is left for the next run.
    if ($canServe) {
        foreach ($e in $serve) {
            $open = Invoke-Haven -Quiet ('(exec 3<>/dev/tcp/127.0.0.1/' + $e.LoopPort + ') 2>/dev/null && echo open || echo closed')
            if ($open.Lines -notcontains 'open') { Say ('  serve ' + $e.TailnetPort + ': skipped, nothing answers on ' + $e.Target + ' yet (' + $e.Service + '); the next deploy adds it') 'Yellow'; continue }
            $s = Invoke-Haven ('tailscale serve --bg --http=' + $e.TailnetPort + ' ' + $e.Target)
            if ($s.Code -ne 0) { Say ('  serve ' + $e.TailnetPort + ' FAILED (above). The LAN address still works.') 'Red' } else { Say ('  serve ' + $e.TailnetPort + ' -> ' + $e.Target + ' (' + $e.Service + ')') 'Green' }
        }
        $chk = Wait-Http $tailnetUrl 60
        if ($chk -and $chk.Content -match 'Ultima Online / Shattered Legacy') { Say ('  ' + $tailnetUrl + ' answers from this PC over the tailnet') 'Green' }
        else { Say ('  ' + $tailnetUrl + ' did not answer from this PC in 60 s. Is Tailscale on here? The LAN address ' + $pageUrl + ' works.') 'Yellow' }
    } else { Say '  Tailscale Serve skipped (see Discover); the dashboard is on the LAN only for now.' 'Yellow' }
    if ($pcState -eq 'running') { & docker stop $PcDashboard 2>&1 | ForEach-Object { Say ('    | ' + $_) }; Say ('  ' + $PcDashboard + ' on this PC stopped (not removed)') }
    Say ''
    Say 'Deployed. What you should see now:' 'Green'
    Say ('  - ' + $tailnetUrl + ' (and ' + $pageUrl + ' at home): Sunflower, Filament, Monitoring, Ultima Online / Shattered Legacy, Admin.')
    Say '  - Every tile has a dot or a State line: Haven''s containers through the proxy, web services by their health URL, the game PC''s from its snapshot.'
    Say '  - Grafana and Portainer tiles say API Error until their token is pasted into dashboard.env (notes/haven-runbook.md, Tokens and keys).'
    exit 0
}

# --- Rollback -------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Rollback') {
    Step 'Plan: Rollback'
    Say ('  1. docker compose -p ' + $Project + ' stop on ' + $where + ' (Homepage and its proxy; files and dashboard.env kept)')
    Say ('  2. tailscale serve --http=80 off (the ops page and the print queue keep theirs)' + $(if ($canServe) { '' } else { ': skipped, no permission' }))
    Say ('  3. docker start ' + $StaleName + ' on ' + $where + ' (' + $stale + ')')
    Say ('  4. docker start ' + $PcDashboard + ' on this PC (' + $pcState + ')')
    Confirm-Go 'roll back'
    Step 'Rollback'
    Compose 'stop' | Out-Null
    if ($canServe) { $hp = @($serve | Where-Object { $_.Service -eq 'homepage' })[0]; if ($hp) { Invoke-Haven ('tailscale serve --http=' + $hp.TailnetPort + ' off') | Out-Null } }
    if ($stale -ne 'none') { Invoke-Haven ('docker start ' + $StaleName) | Out-Null }
    if ($pcState -ne 'none') { & docker start $PcDashboard 2>&1 | ForEach-Object { Say ('    | ' + $_) } }
    Say ('Rolled back: the new Homepage is stopped; ' + $StaleName + ' and this PC''s ' + $PcDashboard + ' are started again.') 'Green'
    exit 0
}
