#Requires -Version 5.1
<#
  cc-P163: Portainer CE on Haven (Chase, 2026-10-18 09:02: "install it"), the way the other Haven deploys work.

      Deploy-PortainerHaven.ps1 -Mode Discover|Deploy|Rollback [-DryRun] [-Yes]

  Chase runs it from the Shard Console's Websites tab, after the phrase deploy to haven: portainer-haven is Deploy,
  portainer-haven-rollback is Rollback, each a one-line launcher in D:\UO\haven-migration. CC never runs it against
  Haven. It reaches Haven with ssh and scp as Chase (his key); every mode starts with Discover, which only reads and
  prints, then the plan, then waits for a typed word (anything else stops, changing nothing). -DryRun stops after the
  plan. -Yes skips the typed word (the proof only).

  Discover  prints every container on Haven (name, state, image, ports, compose folder), every listening port, Tailscale
            (installed, who may change Serve, what Serve does now), and the facts this deploy needs: a container named
            portainer and the compose project it belongs to, the data volume portainer_data, ~/sl-portainer, and whether
            the loopback ports answer. Changes nothing.
  Deploy    refuses if a portainer container exists that this deploy did not make (another project), or if 9443 is
            taken by something else. Otherwise: backs up the data volume first when it exists (Portainer stopped, the
            volume tarred to ~/sl-portainer/backup-<time>.tgz, chmod 600); copies docker/portainer-haven's compose to
            ~/sl-portainer; docker compose -p sl-portainer up -d (portainer/portainer-ce:2.45.2, HTTPS on Haven's LAN
            address and loopback 9443, plain HTTP on loopback 9000 only); waits for https://192.168.1.61:9443/api/
            system/status from this PC; points Tailscale Serve's tailnet port 9000 at loopback 9000 (services.json
            tailscaleServe); then, when Portainer has no admin user yet, prints the first-login link, the 5-minute
            limit and the setup token from Portainer's log, in big letters. Any failure before Portainer answers stops
            it again; the backup stays in ~/sl-portainer.
  Rollback  stops Portainer (stopped, not removed; the data volume is kept) and turns off Serve's port 9000.

  Never touched: every other container, NPM and its hosts, the dashboard's dashboard.env (Chase pastes the token there;
  see notes/haven-runbook.md). Never Tailscale Funnel. Never all interfaces. The setup token is printed only while no
  admin exists: it is single-use and dead once the admin user is made. Refuses to run as SYSTEM. ASCII only.

  -Local (the proof only): the Haven half runs on this PC's Docker under -Root (a scratch or proof folder), through Git
  Bash, with scratch names and ports (-Name, -Volume, -Port, -LoopPort, -HttpLoopPort; never portainer or
  portainer_data); -ProofBin puts a stand-in tailscale first on the PATH.
#>
[CmdletBinding()]
param(
    [ValidateSet('Discover', 'Deploy', 'Rollback')][string]$Mode = 'Discover',
    [switch]$DryRun,
    [switch]$Yes,
    [string]$Ssh = 'chase@192.168.1.61',
    [string]$HavenRoot = '/home/chase/sl-portainer',
    [string]$Bind = '192.168.1.61',
    [int]$Port = 9443,
    [int]$LoopPort = 0,
    [int]$HttpLoopPort = 9000,
    [string]$Project = 'sl-portainer',
    [string]$Name = 'portainer',
    [string]$Volume = 'portainer_data',
    [string]$Inventory,
    # The proof's switches.
    [switch]$Local,
    [string]$Root,
    [string]$ProofBin
)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, with his SSH key, never as SYSTEM.' -ForegroundColor Red; exit 2 }
$src = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $src '..\..')).Path
$bash = 'C:\Program Files\Git\bin\bash.exe'
. (Join-Path $repo 'docker\haven\HavenInventory.ps1')
if (-not $Inventory) { $Inventory = Join-Path $repo 'docker\haven\services.json' }
$inv = Read-HavenInventory -Path $Inventory
if ($inv.Problems.Count) { foreach ($p in $inv.Problems) { Write-Host ('INVENTORY: ' + $p) -ForegroundColor Red }; Write-Host 'docker\haven\services.json is unsound; nothing was read or changed.' -ForegroundColor Red; exit 2 }
$tailnetName = [string]$inv.hosts.haven.tailnet
# Tailscale Serve: the inventory's entry for this service, loopback HTTP only (cc-P133's rule).
$serveEntry = @($inv.tailscaleServe | Where-Object { $_.service -eq 'portainer-haven' })[0]
if (-not $serveEntry -or [string]$serveEntry.target -notmatch '^http://127\.0\.0\.1:(\d+)$') { Write-Host 'REFUSED: services.json tailscaleServe has no loopback HTTP entry for portainer-haven.' -ForegroundColor Red; exit 2 }
$serveLoop = [int]$Matches[1]
$servePort = [int]$serveEntry.tailnetPort
if ($Local) {
    if (-not $Root -or $Root -notmatch '(?i)scratch|proof') { throw '-Local runs only under a -Root scratch or proof folder' }
    if ($Name -eq 'portainer' -or $Volume -eq 'portainer_data' -or $Project -eq 'sl-portainer') { throw '-Local needs scratch -Name, -Volume and -Project, never the real ones' }
    # PowerShell names ignore case: $root and $Root are ONE variable, so the Windows path is kept first (cc-P109's bug).
    $winRoot = $Root
    $root = '/' + $winRoot.Substring(0, 1).ToLower() + ($winRoot.Substring(2) -replace '\\', '/')
    if (-not $LoopPort) { $LoopPort = $Port + 1 }
    $serveLoop = $HttpLoopPort
} else {
    $root = $HavenRoot
    if (-not $LoopPort) { $LoopPort = $Port }
    if ($HttpLoopPort -ne $serveLoop) { throw ('-HttpLoopPort ' + $HttpLoopPort + ' is not the loopback port services.json serves (' + $serveLoop + ')') }
}
$hostAddr = $(if ($Local) { '127.0.0.1' } else { $Bind })
$pageUrl = 'https://' + $hostAddr + ':' + $Port + '/'
$tailnetUrl = 'http://' + $tailnetName + ':' + $servePort + '/'
$where = $(if ($Local) { 'this PC (proof, ' + $winRoot + ')' } else { 'Haven (' + $Ssh + ')' })

function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Step([string]$t) { Write-Host ('== ' + $t) -ForegroundColor Cyan }
function Invoke-Haven {
    # One POSIX shell command on Haven (ssh as Chase) or, with -Local, in Git Bash here; base64 on the wire because
    # Windows PowerShell 5.1 mangles quotes in a native command's arguments (docker/ops/Deploy-HavenOps.ps1).
    param([string]$Command, [switch]$Quiet)
    $pre = $(if ($Local -and $ProofBin) { 'export PATH="' + ('/' + $ProofBin.Substring(0, 1).ToLower() + ($ProofBin.Substring(2) -replace '\\', '/')) + ':$PATH"; ' } else { '' })
    # The proof: Git Bash rewrites /paths in a native command's arguments (docker run -v ...:/backup); Linux does not.
    if ($Local) { $pre = 'export MSYS_NO_PATHCONV=1; ' + $pre }
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
# Never all interfaces: Haven's LAN address and its loopback (the proof: loopback ports only).
$composeEnv = 'PT_PROJECT=' + $Project + ' PT_NAME=' + $Name + ' PT_VOLUME=' + $Volume + ' PT_BIND=' + $hostAddr + ' PT_PORT=' + $Port +
    ' PT_LOOP_PORT=' + $LoopPort + ' PT_HTTP_LOOP_PORT=' + $HttpLoopPort
function Compose([string]$What) { Invoke-Haven ('cd ' + $root + ' && ' + $composeEnv + ' docker compose -p ' + $Project + ' ' + $What) }
function Get-Https([string]$Url) {
    # Portainer's certificate is its own self-signed one, so curl.exe -k (Windows' own curl). Returns the status code.
    $ErrorActionPreference = 'Continue'
    $c = (& curl.exe -sk -o NUL -m 5 -w '%{http_code}' $Url 2>$null | Out-String).Trim()
    $(if ($c -match '^\d{3}$') { [int]$c } else { 0 })
}
function Wait-Https([string]$Url, [int]$Seconds = 120) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { if ((Get-Https $Url) -eq 200) { return $true }; Start-Sleep -Seconds 3 }
    $false
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
    'echo "-- containers"; docker ps -a --format "  {{.Names}}|{{.State}}|{{.Status}}|{{.Image}}|{{.Ports}}|{{.Label \"com.docker.compose.project.working_dir\"}}"; ' +
    'echo "-- listening ports"; if command -v ss >/dev/null 2>&1; then ss -tln | tail -n +2 | awk ''{print "  " $4}''; else netstat -an | grep LISTEN | awk ''{print "  " $2}''; fi | sort -u | head -80; ' +
    'echo "-- tailscale"; if command -v tailscale >/dev/null 2>&1; then echo "  installed: yes"; ' +
    'o=$(tailscale debug prefs 2>/dev/null | grep -o "\"OperatorUser\": *\"[^\"]*\"" | sed "s/.*: *//; s/\"//g"); echo "  operator: ${o:-none}"; echo "  me: $(id -un)"; ' +
    'tailscale serve status 2>&1 | head -20 | sed "s/^/  serve now=  /"; else echo "  installed: no"; fi; ' +
    'echo "-- facts"; ' +
    'docker inspect -f "portainer: {{.State.Status}} project={{index .Config.Labels \"com.docker.compose.project\"}}" ' + $Name + ' 2>/dev/null || echo "portainer: none"; ' +
    'docker volume inspect ' + $Volume + ' >/dev/null 2>&1 && echo "volume: yes" || echo "volume: no"; ' +
    'test -f ' + $root + '/docker-compose.yml && echo "folder: yes" || echo "folder: no"; ' +
    'if (exec 3<>/dev/tcp/127.0.0.1/' + $LoopPort + ') 2>/dev/null; then echo "loopback https: open"; else echo "loopback https: closed"; fi; ' +
    'if (exec 3<>/dev/tcp/127.0.0.1/' + $HttpLoopPort + ') 2>/dev/null; then echo "loopback http: open"; else echo "loopback http: closed"; fi')
foreach ($l in $probe.Lines) { Say ('    ' + $l) }
if ($probe.Code -ne 0 -and -not ($probe.Lines -match '^host=')) { Say ('Cannot reach ' + $where + '. Nothing was changed.') 'Red'; exit 1 }
$facts = @{}
foreach ($l in $probe.Lines) { if ($l -match '^\s*([a-z][a-z0-9 ]*[a-z0-9]): (.*)$') { $facts[$Matches[1]] = $Matches[2] } }
$disc = ConvertFrom-HavenDiscovery -Text (($probe.Lines -join "`n") + "`n")
$ts = $disc.Tailscale
$canServe = ($ts['installed'] -eq 'yes') -and ($ts['operator'] -eq $ts['me'] -or $ts['me'] -eq 'root')
$pt = [string]$facts['portainer']
$ptExists = $pt -and $pt -ne 'none'
$ptOurs = $ptExists -and $pt -match (' project=' + [regex]::Escape($Project) + '$')
$ptRunning = $pt -like 'running*'
$hasVolume = $facts['volume'] -eq 'yes'
Say ('    portainer: ' + $(if (-not $ptExists) { 'none yet' } elseif ($ptOurs) { $pt + ' (this deploy''s)' } else { $pt + ' (NOT this deploy''s)' }) + '; data volume ' + $Volume + ': ' + $(if ($hasVolume) { 'exists' } else { 'none yet' }))
if ($ts['installed'] -ne 'yes') { Say '  Tailscale is not installed on Haven: Portainer is on the LAN only until it is.' 'Yellow' }
elseif (-not $canServe) { Say ('  Tailscale Serve needs a one-time permission: ssh -t ' + $Ssh + ' "sudo tailscale set --operator=$(whoami)" (then deploy again). Until then: LAN only.') 'Yellow' }
if ($Mode -eq 'Discover') { Say 'Discover only: nothing was changed.' 'Green'; exit 0 }

# --- Deploy ---------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Deploy') {
    Step 'Plan: Deploy'
    if ($ptExists -and -not $ptOurs) { Say ('REFUSED: a container named ' + $Name + ' already exists on ' + $where + ' and is not this deploy''s (' + $pt + '). Nothing was changed. Remove or rename it by hand first, or tell the next prompt.') 'Red'; exit 1 }
    $taken = @($disc.Listening | Where-Object { $_ -match (':' + $Port + '$') })
    if ($taken -and -not $ptRunning) { Say ('REFUSED: port ' + $Port + ' is in use on ' + $where + ' by something that is not this Portainer (' + ($taken -join '; ') + '). Nothing was changed.') 'Red'; exit 1 }
    if ($hasVolume) { Say ('  1. back up the data volume ' + $Volume + ': ' + $(if ($ptRunning) { 'stop Portainer, ' } else { '' }) + 'tar it to ' + $root + '/backup-<time>.tgz (chmod 600)') }
    else { Say ('  1. nothing to back up: ' + $Volume + ' does not exist yet (compose makes it)') }
    Say ('  2. copy docker-compose.yml to ' + $root)
    Say ('  3. docker compose -p ' + $Project + ' up -d (portainer/portainer-ce:2.45.2; HTTPS on ' + $hostAddr + ':' + $Port + ' and 127.0.0.1:' + $LoopPort + ', HTTP on 127.0.0.1:' + $HttpLoopPort + ' only; never all interfaces), and wait for ' + $pageUrl + 'api/system/status')
    if ($canServe) { Say ('  4. tailscale serve --bg --http=' + $servePort + ' http://127.0.0.1:' + $serveLoop + '  (tailnet only: ' + $tailnetUrl + ')') } else { Say '  4. Tailscale Serve: skipped (above)' }
    Say '  5. if Portainer has no admin user yet: the first-login link, the 5-minute limit and the setup token, in big letters'
    Say '  If 3 fails, Portainer is stopped again; the backup stays in the folder.'
    Confirm-Go 'deploy'

    Step 'Deploy'
    $r = Invoke-Haven ('mkdir -p ' + $root + ' && chmod 700 ' + $root + ' && echo "folder ready"')
    if ($r.Code -ne 0) { Say ('FAILED: could not make ' + $root + '. Nothing was changed.') 'Red'; exit 1 }
    if ($hasVolume) {
        if ($ptRunning) { Invoke-Haven ('docker stop ' + $Name) | Out-Null }
        $b = Invoke-Haven ('cd ' + $root + ' && b=backup-$(date +%Y%m%d-%H%M%S).tgz && docker run --rm -v ' + $Volume + ':/data:ro -v "$(' + $(if ($Local) { 'pwd -W' } else { 'pwd' }) + ')":/backup alpine:3.20 tar czf /backup/$b -C /data . && chmod 600 $b && echo "backed up to ' + $root + '/$b ($(du -h $b | cut -f1))"')
        if ($b.Code -ne 0) {
            if ($ptRunning) { Invoke-Haven ('docker start ' + $Name) | Out-Null }
            Say 'FAILED: the backup of the data volume failed; nothing was replaced and Portainer runs as before.' 'Red'; exit 1
        }
    }
    Copy-ToHaven (Join-Path $src 'docker-compose.yml') ($root + '/docker-compose.yml')
    Say '  compose copied'
    $u = Compose 'up -d --force-recreate'
    if ($u.Code -ne 0 -or -not (Wait-Https ($pageUrl + 'api/system/status') 120)) {
        Say ('FAILED: ' + $(if ($u.Code -ne 0) { 'docker compose up failed' } else { 'Portainer did not answer at ' + $pageUrl + ' in 2 minutes' })) 'Red'
        Compose 'stop' | Out-Null
        Say ('Portainer is stopped. Any backup is in ' + $root + '/backup-*.tgz.') 'Yellow'
        exit 1
    }
    Say ('  Portainer answers at ' + $pageUrl) 'Green'
    if ($canServe) {
        $s = Invoke-Haven ('tailscale serve --bg --http=' + $servePort + ' http://127.0.0.1:' + $serveLoop)
        if ($s.Code -ne 0) { Say ('  serve ' + $servePort + ' FAILED (above). The LAN address still works.') 'Red' } else { Say ('  serve ' + $servePort + ' -> http://127.0.0.1:' + $serveLoop + ' (portainer-haven)') 'Green' }
    } else { Say '  Tailscale Serve skipped (see Discover); Portainer is on the LAN only for now.' 'Yellow' }
    # Portainer: GET /api/users/admin/check is 204 once an admin exists, 404 before.
    $admin = Get-Https ($pageUrl + 'api/users/admin/check')
    Say ''
    if ($admin -eq 404) {
        $tok = Invoke-Haven -Quiet ('docker logs ' + $Name + ' 2>&1 | grep -o "setup_token=[A-Za-z0-9._-]*" | tail -1 | cut -d= -f2')
        $token = @($tok.Lines | Where-Object { $_ -match '^[A-Za-z0-9._-]+$' })[0]
        $bar = '#' * 78
        Say $bar 'Yellow'
        Say '#' 'Yellow'
        Say '#   FIRST LOGIN: MAKE THE ADMIN USER WITHIN 5 MINUTES, STARTING NOW' 'Yellow'
        Say '#' 'Yellow'
        Say ('#   Open  ' + $pageUrl) 'Yellow'
        Say '#   (your browser warns about the certificate: it is Portainer''s own; accept it)' 'Yellow'
        Say ('#   Setup token:  ' + $(if ($token) { $token } else { 'not found in the log: ssh ' + $Ssh + ' "docker logs ' + $Name + ' 2>&1 | grep setup_token="' })) 'Yellow'
        Say '#   Make the admin user and put its password in the vault.' 'Yellow'
        Say '#' 'Yellow'
        Say '#   Missed the 5 minutes? Portainer refuses the setup after that, for safety:' 'Yellow'
        Say '#   click Portainer (Haven): deploy again. It restarts Portainer: 5 new minutes.' 'Yellow'
        Say '#' 'Yellow'
        Say $bar 'Yellow'
        Say ''
    } elseif ($admin -eq 204) { Say '  Portainer already has its admin user: nothing to set up.' 'Green' }
    else { Say ('  Could not tell whether Portainer has an admin user (HTTP ' + $admin + '). Open ' + $pageUrl + ' now: if it asks for one, make it within 5 minutes of the deploy.') 'Yellow' }
    Say 'Deployed. What you should see now:' 'Green'
    Say ('  - ' + $pageUrl + ' at home, and ' + $tailnetUrl + ' on the tailnet: Portainer, with Haven''s Docker as its local environment.')
    Say '  - The dashboard''s Portainer (Haven) tile goes green; its widget says API Error until the homepage token is in dashboard.env:'
    Say '    Portainer > your user (top right) > My account > Access tokens > Add, name homepage; the environment ID is the number'
    Say '    after /endpoints/ under Environments. Paste both into ~/sl-home/dashboard.env (notes/haven-runbook.md, Tokens and keys),'
    Say '    then click Mega dashboard: deploy.'
    exit 0
}

# --- Rollback -------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Rollback') {
    Step 'Plan: Rollback'
    if ($ptExists -and -not $ptOurs) { Say ('REFUSED: the container named ' + $Name + ' is not this deploy''s (' + $pt + '). Nothing was changed.') 'Red'; exit 1 }
    Say ('  1. docker compose -p ' + $Project + ' stop on ' + $where + ' (' + $(if ($ptExists) { $pt } else { 'nothing runs' }) + '; the data volume ' + $Volume + ' and the backups are kept)')
    Say ('  2. tailscale serve --http=' + $servePort + ' off' + $(if ($canServe) { '' } else { ': skipped, no permission' }))
    Confirm-Go 'roll back'
    Step 'Rollback'
    if ($facts['folder'] -eq 'yes') { Compose 'stop' | Out-Null } elseif ($ptExists) { Invoke-Haven ('docker stop ' + $Name) | Out-Null }
    if ($canServe) { Invoke-Haven ('tailscale serve --http=' + $servePort + ' off') | Out-Null }
    Say ('Rolled back: Portainer is stopped; its data volume ' + $Volume + ' is kept. Portainer (Haven): deploy starts it again.') 'Green'
    exit 0
}
