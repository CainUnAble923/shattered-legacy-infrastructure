#Requires -Version 5.1
<#
  cc-P107 (Haven steps H2 and H3): the ops pages (Play tests, Ideas, Bug reports, Shard status) on Haven.

      Deploy-HavenOps.ps1 -Mode Discover|Deploy|Cutover|Rollback [-DryRun] [-Yes]

  Chase runs it from the Shard Console's Websites tab (P101's one-click Haven deploys, after the phrase deploy to haven):
  ops-pages is Deploy, ops-cutover is Cutover, ops-rollback is Rollback, each a one-line launcher in D:\UO\haven-migration.
  CC never runs it against Haven. It reaches Haven with ssh and scp as Chase (his key); every mode starts with Discover,
  which only reads, prints the plan, and asks for a typed word before it changes anything (anything else stops there,
  so a click is a dry run until he types it). -DryRun stops after the plan. -Yes skips the typed word (the proof).

  Discover  reads Haven: docker and compose, whether :8093 and :8094 are free, the stack folder, data, key, token file
            (that it exists, never what is in it), the clones and their stamps. Changes nothing.
  Deploy    makes ~/sl-ops/{stack,repo,data,keys}; copies docker-compose.yml and git-pull.sh; makes a read-only deploy key
            for the private shard-migration repo the first time (and prints the public half for GitHub); writes the
            token file from a prompt the first time (ops.env on Haven, chmod 600, and D:\UO\haven-ops\token.txt here; never
            echoed); starts git-pull only and waits for both clones. Before the cutover the page is not started.
            After it, the page is recreated so it runs the new code.
  Cutover   stops the game PC's sl-play-tests; seeds Haven's data from D:\UO\shard-migration\testing (refuses if Haven
            already has data, so a cutover never overwrites what Haven wrote); starts Haven's page; checks Haven's export
            matches the seed file by file (SHA-256); then writes mode haven. Any failure before that starts the game PC's
            service again and stops Haven's page.
  Rollback  takes Haven's export into testing\ (so nothing marked on Haven is lost), stops Haven's page (git-pull and
            the data stay), starts the game PC's sl-play-tests, and writes mode local.

  Never touched: Vaultwarden, NPM and its hosts, the website, the wiki, Kevin's Parts, and ~/shattered-legacy on Haven
  (its own compose project, sl-ops, and its own folder, ~/sl-ops). Refuses to run as SYSTEM. ASCII only.

  -Local (the proof only): the Haven half runs on this PC's Docker under -OpsRoot, through Git Bash, with scratch ports
  and names; nothing reaches Haven.
#>
[CmdletBinding()]
param(
    [ValidateSet('Discover', 'Deploy', 'Cutover', 'Rollback')][string]$Mode = 'Discover',
    [switch]$DryRun,
    [switch]$Yes,
    [switch]$Force,
    [string]$Config,
    # The proof's switches; the defaults are Haven's.
    [switch]$Local,
    [string]$OpsRoot,
    [string]$Bind = '192.168.1.61',
    [int]$PagePort = 8093,
    [int]$LanPort = 8094,
    # cc-P133: the page's loopback port, for Tailscale Serve. The proof gives it a scratch one (PagePort + 1000).
    [int]$LoopPort = 0,
    [string]$Project = 'sl-ops',
    [string]$PlayTestsName = 'sl-play-tests',
    [string]$GitPullName = 'sl-ops-git-pull',
    [string]$ComposeOverride,
    [string]$GamePcContainer,
    [string]$MirrorRoot = 'D:\UO\shard-migration',
    [string]$StateFile,
    [string]$TokenFile
)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, with his SSH key, never as SYSTEM.' -ForegroundColor Red; exit 2 }
$here = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $here '..\..')).Path
if (-not $Config) { $Config = Join-Path $repo 'scripts\haven-ops.json' }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$ssh = [string]$cfg.haven.ssh
if (-not $StateFile) { $StateFile = [string]$cfg.stateFile }
if (-not $TokenFile) { $TokenFile = [string]$cfg.tokenFile }
if (-not $GamePcContainer) { $GamePcContainer = [string]$cfg.gamePc.container }
$bash = 'C:\Program Files\Git\bin\bash.exe'
if ($Local) {
    if (-not $OpsRoot) { throw '-Local needs -OpsRoot (a scratch folder)' }
    if ($OpsRoot -notmatch '(?i)scratch|proof') { throw '-Local runs only under a scratch or proof folder' }
    $root = '/' + $OpsRoot.Substring(0, 1).ToLower() + ($OpsRoot.Substring(2) -replace '\\', '/')   # D:\UO\x -> /d/UO/x
    $composeRoot = $OpsRoot -replace '\\', '/'                                                        # D:/UO/x for Docker
} else {
    $root = [string]$cfg.haven.root
    $composeRoot = $root
}
$pageBase = $(if ($Local) { 'http://127.0.0.1:' + $PagePort } else { ([string]$cfg.haven.pageUrl).TrimEnd('/') })
$apiBase = $(if ($Local) { 'http://127.0.0.1:' + $LanPort } else { ([string]$cfg.haven.opsApi).TrimEnd('/') })
$where = $(if ($Local) { 'this PC (proof, ' + $OpsRoot + ')' } else { 'Haven (' + $ssh + ')' })

function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Step([string]$t) { Write-Host ('== ' + $t) -ForegroundColor Cyan }

function Invoke-Haven {
    # One POSIX shell command on Haven (ssh as Chase) or, with -Local, in Git Bash here. Returns its output lines.
    # Windows PowerShell 5.1 mangles double quotes inside a native command's arguments, so the command travels as base64
    # (letters, digits, + / =) and is decoded there: set -f keeps the shell from globbing the encoded text, and the
    # script itself turns globbing back on. Stdin (the token, only) is never part of any command line.
    param([string]$Command, [switch]$Quiet, [string]$InputText)
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('set +f; ' + $Command))
    $wire = 'set -f; eval $(echo ' + $b64 + ' | base64 -d)'
    # Not 'Stop' here: in Windows PowerShell 5.1 a native command's stderr (docker's progress lines) would end the script.
    # The exit code decides instead.
    $ErrorActionPreference = 'Continue'
    if ($Local) {
        if ($InputText) { $out = $InputText | & $bash -c $wire 2>&1 } else { $out = & $bash -c $wire 2>&1 }
    } else {
        if ($InputText) { $out = $InputText | & ssh -o BatchMode=yes $ssh $wire 2>&1 } else { $out = & ssh -o BatchMode=yes $ssh $wire 2>&1 }
    }
    $code = $LASTEXITCODE
    $lines = @($out | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { [string]$_.Exception.Message } else { [string]$_ } })
    if (-not $Quiet) { foreach ($l in $lines) { Write-Host ('    | ' + $l) } }
    [pscustomobject]@{ Code = $code; Lines = $lines }
}

function Copy-ToHaven {
    param([string]$From, [string]$To)
    if ($Local) {
        $dest = $OpsRoot + ($To.Substring($root.Length) -replace '/', '\')
        Copy-Item -LiteralPath $From -Destination $dest -Force
    } else {
        & scp -q -o BatchMode=yes $From ($ssh + ':' + $To)
        if ($LASTEXITCODE -ne 0) { throw ('scp failed: ' + $From) }
    }
}

if (-not $LoopPort) { $LoopPort = $(if ($Local) { $PagePort + 1000 } else { 8093 }) }
$composeEnv = 'OPS_ROOT=' + $composeRoot + ' BIND=' + $Bind + ' PAGE_PORT=' + $PagePort + ' LAN_PORT=' + $LanPort + ' LOOP_PAGE_PORT=' + $LoopPort +
    ' OPS_PROJECT=' + $Project + ' PLAY_TESTS_NAME=' + $PlayTestsName + ' GIT_PULL_NAME=' + $GitPullName
$composeFiles = '-f ' + $root + '/stack/docker-compose.yml'
if ($ComposeOverride) { $composeFiles += ' -f ' + $root + '/stack/override.yml' }
function Compose([string]$What) { Invoke-Haven ('cd ' + $root + '/stack && ' + $composeEnv + ' docker compose ' + $composeFiles + ' -p ' + $Project + ' ' + $What) }

function Read-Mode { try { [string](Get-Content -LiteralPath $StateFile -Raw | ConvertFrom-Json).mode } catch { 'local' } }
function Write-Mode([string]$m) {
    $dir = Split-Path $StateFile -Parent
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $rec = [ordered]@{ mode = $m; since = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK'); by = $env:USERNAME; page = $(if ($m -eq 'haven') { $pageBase + '/' } else { [string]$cfg.gamePc.pageUrl }) }
    [IO.File]::WriteAllText($StateFile, ($rec | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
    Say ('  mode is now ' + $m + ' (' + $StateFile + ')') 'Green'
}
function Read-Token { try { (Get-Content -LiteralPath $TokenFile -Raw -ErrorAction Stop).Trim() } catch { '' } }
function Wait-Http([string]$Url, [int]$Seconds = 120, [hashtable]$Headers = @{}) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try { $r = Invoke-WebRequest -UseBasicParsing -Uri $Url -Headers $Headers -TimeoutSec 5; if ($r.StatusCode -eq 200) { return $r } } catch { }
        Start-Sleep -Seconds 3
    }
    $null
}
function Get-SeedFiles {
    $t = Join-Path $MirrorRoot 'testing'
    $list = @()
    foreach ($n in 'play-tests.json', 'SUMMARY.md', 'feature-ideas.json', 'bug-reports.json') { $p = Join-Path $t $n; if (Test-Path -LiteralPath $p) { $list += [pscustomobject]@{ Rel = 'testing/' + $n; Path = $p } } }
    $f = Join-Path $t 'bug-reports\files'
    if (Test-Path -LiteralPath $f) { foreach ($x in Get-ChildItem -LiteralPath $f -File) { if ($x.Name -match '^B-\d{3,}-\d+\.(png|jpg)$') { $list += [pscustomobject]@{ Rel = 'testing/bug-reports/files/' + $x.Name; Path = $x.FullName } } } }
    $list
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
    'for p in ' + $PagePort + ' ' + $LanPort + '; do if (ss -ltn 2>/dev/null || netstat -an 2>/dev/null) | grep -q "[:.]$p "; then echo "port $p: in use"; else echo "port $p: free"; fi; done; ' +
    'for d in stack repo data keys; do test -d ' + $root + '/$d && echo "folder $d: yes" || echo "folder $d: no"; done; ' +
    'test -f ' + $root + '/ops.env && echo "token file: yes" || echo "token file: no"; ' +
    'test -f ' + $root + '/keys/shard-migration && echo "deploy key: yes" || echo "deploy key: no"; ' +
    'test -f ' + $root + '/data/testing/play-tests.json && echo "data: has play-tests.json" || echo "data: empty"; ' +
    'for n in shard-migration shattered-legacy; do test -f ' + $root + '/repo/stamp-$n.txt && echo "clone $n: $(cat ' + $root + '/repo/stamp-$n.txt)" || echo "clone $n: none"; done; ' +
    'docker ps -a --format "container {{.Names}}: {{.Status}}" | grep -E "container (' + $PlayTestsName + '|' + $GitPullName + '):" || echo "containers: none of ours"')
foreach ($l in $probe.Lines) { Say ('    ' + $l) }
if ($probe.Code -ne 0 -and -not ($probe.Lines -match '^host=')) { Say ('Cannot reach ' + $where + '. Nothing was changed.') 'Red'; exit 1 }
$facts = @{}
foreach ($l in $probe.Lines) { if ($l -match '^([a-z ]+[a-z]): (.*)$') { $facts[$Matches[1]] = $Matches[2] } }
$opsMode = Read-Mode
Say ('    this PC: mode ' + $opsMode + ', token file ' + $(if (Read-Token) { 'present' } else { 'absent' }) + ' (' + $TokenFile + ')')
if ($Mode -eq 'Discover') { Say 'Discover only: nothing was changed.' 'Green'; exit 0 }

# --- Deploy ---------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Deploy') {
    Step 'Plan: Deploy'
    $ourPage = [bool]($probe.Lines -match ('^container ' + [regex]::Escape($PlayTestsName) + ':'))
    foreach ($p in $PagePort, $LanPort) { if ($facts['port ' + $p] -eq 'in use' -and -not $ourPage) { Say ('REFUSED: port ' + $p + ' is in use on ' + $where + ' by something else. Nothing was changed.') 'Red'; exit 1 } }
    Say ('  1. mkdir -p ' + $root + '/{stack,repo,data,keys}')
    Say ('  2. copy docker/ops/docker-compose.yml and git-pull.sh to ' + $root + '/stack/')
    if (-not $Local) { Say ('  3. ' + $(if ($facts['deploy key'] -eq 'yes') { 'deploy key: already made' } else { 'make a read-only deploy key: ssh-keygen -t ed25519 -f ' + $root + '/keys/shard-migration (its public half is printed, for GitHub)' })) }
    Say ('  4. ' + $(if ($facts['token file'] -eq 'yes') { 'token file: already there' } else { 'token: asked here (or made), written to ' + $root + '/ops.env (chmod 600) and ' + $TokenFile + '; never shown' }))
    Say ('  5. docker compose -p ' + $Project + ' up -d git-pull, then wait for both clones')
    if ($opsMode -eq 'haven') { Say ('  6. docker compose -p ' + $Project + ' up -d play-tests (after the cutover: the page runs the new code)') } else { Say '  6. the page is not started: that is the cutover (ops-cutover)' }
    Confirm-Go 'deploy'
    Step 'Deploy'
    $r = Invoke-Haven ('mkdir -p ' + $root + '/stack ' + $root + '/repo ' + $root + '/data/testing ' + $root + '/keys && chmod 700 ' + $root + '/keys')
    if ($r.Code -ne 0) { throw 'mkdir failed' }
    Copy-ToHaven (Join-Path $here 'docker-compose.yml') ($root + '/stack/docker-compose.yml')
    Copy-ToHaven (Join-Path $here 'git-pull.sh') ($root + '/stack/git-pull.sh')
    if ($ComposeOverride) { Copy-ToHaven $ComposeOverride ($root + '/stack/override.yml') }
    if (-not $Local) {
        $k = Invoke-Haven -Quiet ('test -f ' + $root + '/keys/shard-migration || ssh-keygen -q -t ed25519 -N "" -C "sl-ops read-only on Haven (cc-P107)" -f ' + $root + '/keys/shard-migration; chmod 600 ' + $root + '/keys/shard-migration; cat ' + $root + '/keys/shard-migration.pub')
        Say '  The deploy key (public half). If this is the first deploy, add it on GitHub: shard-migration > Settings > Deploy keys > Add deploy key, title "sl-ops on Haven", leave "Allow write access" OFF.' 'Yellow'
        foreach ($l in $k.Lines) { if ($l -like 'ssh-ed25519 *') { Say ('    ' + $l) 'White' } }
    }
    if ($facts['token file'] -ne 'yes') {
        if ((Read-Token) -and -not $Force) { Say ('REFUSED: ' + $TokenFile + ' exists but Haven has no token file. Remove it (or pass -Force) to make a new pair.') 'Red'; exit 1 }
        $tok = ''
        if (-not $Yes) {
            $sec = Read-Host -AsSecureString 'Token for the ops listener (Enter makes a random one)'
            $tok = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
        }
        if (-not $tok) { $b = New-Object byte[] 32; ([Security.Cryptography.RandomNumberGenerator]::Create()).GetBytes($b); $tok = [Convert]::ToBase64String($b) -replace '[+/=]', 'x' }
        if ($tok -notmatch '^[A-Za-z0-9._~-]{24,128}$') { throw 'a token is 24 to 128 letters, digits or . _ ~ -' }
        $r = Invoke-Haven -Quiet -InputText ('PLAYTESTS_TOKEN=' + $tok) ('umask 077 && tr -d ''\r'' > ' + $root + '/ops.env')
        if ($r.Code -ne 0) { throw 'writing the token file on Haven failed' }
        $d = Split-Path $TokenFile -Parent
        if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
        [IO.File]::WriteAllText($TokenFile, $tok, (New-Object Text.UTF8Encoding $false))
        Say ('  token written: ' + $root + '/ops.env and ' + $TokenFile + ' (not shown)') 'Green'
    }
    Compose 'up -d git-pull' | Out-Null
    Say '  waiting for both clones (up to 3 minutes)...'
    $ok = $false
    for ($i = 0; $i -lt 60; $i++) {
        $s = Invoke-Haven -Quiet ('test -f ' + $root + '/repo/stamp-shard-migration.txt && test -f ' + $root + '/repo/stamp-shattered-legacy.txt && cat ' + $root + '/repo/stamp-*.txt')
        if ($s.Code -eq 0) { foreach ($l in $s.Lines) { Say ('    clone: ' + $l) }; $ok = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $ok) {
        Invoke-Haven ('docker logs --tail 10 ' + $GitPullName) | Out-Null
        Say 'The clones are not there yet. If the log says the shard-migration clone failed, add the deploy key above on GitHub, then click Ops pages again.' 'Yellow'
        exit 1
    }
    if ($opsMode -eq 'haven') { Compose 'up -d play-tests' | Out-Null; if (Wait-Http ($pageBase + '/api/summary') 120) { Say ('  page up: ' + $pageBase + '/') 'Green' } else { Say '  the page did not answer in 2 minutes' 'Red'; exit 1 } }
    Say 'Deploy done.' 'Green'
    exit 0
}

# --- Cutover --------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Cutover') {
    Step 'Plan: Cutover'
    $token = Read-Token
    $why = @()
    if ($opsMode -eq 'haven' -and -not $Force) { $why += 'this PC already says mode haven' }
    if (-not $token) { $why += ('no token in ' + $TokenFile + ' (run Ops pages first)') }
    if ($facts['clone shard-migration'] -eq 'none' -or $facts['clone shattered-legacy'] -eq 'none') { $why += 'the clones are not there (run Ops pages first)' }
    if ($facts['data'] -ne 'empty' -and -not $Force) { $why += 'Haven already has play-test data; a cutover never overwrites it (roll back first, or -Force)' }
    if ($why) { foreach ($w in $why) { Say ('REFUSED: ' + $w) 'Red' }; Say 'Nothing was changed.' 'Red'; exit 1 }
    $seed = @(Get-SeedFiles)
    Say ('  1. docker stop -t 30 ' + $GamePcContainer + ' on this PC (the game PC''s page stops; nothing is written in the gap)')
    Say ('  2. copy ' + $seed.Count + ' files from ' + $MirrorRoot + '\testing to ' + $root + '/data/testing')
    Say ('  3. docker compose -p ' + $Project + ' up -d play-tests, and wait for ' + $pageBase + '/api/summary')
    Say ('  4. check ' + $apiBase + '/api/export matches the seed, file by file (SHA-256)')
    Say ('  5. write mode haven to ' + $StateFile)
    Say '  If 2 to 4 fail, the game PC''s page is started again and Haven''s stopped.'
    Confirm-Go 'cut over'
    Step 'Cutover'
    $stopped = $false
    try {
        & docker stop -t 30 $GamePcContainer 2>&1 | ForEach-Object { Say ('    | ' + $_) }
        $stopped = $true
        Invoke-Haven ('mkdir -p ' + $root + '/data/testing/bug-reports/files') | Out-Null
        foreach ($f in $seed) { Copy-ToHaven $f.Path ($root + '/data/' + $f.Rel) }
        Say ('  seeded ' + $seed.Count + ' files')
        Compose 'up -d play-tests' | Out-Null
        if (-not (Wait-Http ($pageBase + '/api/summary') 120)) { throw ('Haven''s page did not answer at ' + $pageBase + ' in 2 minutes') }
        $r = Wait-Http ($apiBase + '/api/export') 30 @{ Authorization = 'Bearer ' + $token }
        if (-not $r) { throw 'Haven''s export did not answer with the token' }
        $bundle = $r.Content | ConvertFrom-Json
        $bad = @()
        foreach ($f in $seed) {
            $want = (Get-FileHash -LiteralPath $f.Path -Algorithm SHA256).Hash.ToLowerInvariant()
            $got = $bundle.files.($f.Rel)
            if (-not $got -or [string]$got.sha256 -ne $want) { $bad += $f.Rel }
        }
        if ($bad) { throw ('Haven''s copy differs from the seed: ' + ($bad -join ', ')) }
        Say ('  Haven''s export matches all ' + $seed.Count + ' seeded files') 'Green'
        Write-Mode 'haven'
    } catch {
        Say ('FAILED: ' + $_.Exception.Message) 'Red'
        Compose 'stop play-tests' | Out-Null
        if ($stopped) { & docker start $GamePcContainer 2>&1 | ForEach-Object { Say ('    | ' + $_) }; Say ('  ' + $GamePcContainer + ' started again; the game PC still serves the pages (mode ' + (Read-Mode) + ')') 'Yellow' }
        exit 1
    }
    Say ''
    Say 'Cut over. What you should see now:' 'Green'
    Say ('  - ' + $pageBase + '/ is the Play tests page, with the same items, ideas and bug reports as before.')
    Say ('  - ' + $pageBase + '/status is Shard status; the menu''s Play tests and Shard status links go to Haven.')
    Say '  - The Shard Console''s PLAY TESTS tile ends "on Haven" (reopen the console), and its links open Haven.'
    Say '  - http://192.168.1.58:8093/ no longer answers (the game PC''s page is stopped, not removed).'
    Say '  - The next commit prints "play-test mirror refreshed from Haven".'
    exit 0
}

# --- Rollback -------------------------------------------------------------------------------------------------------
if ($Mode -eq 'Rollback') {
    Step 'Plan: Rollback'
    $token = Read-Token
    Say ('  1. take ' + $apiBase + '/api/export into ' + $MirrorRoot + '\testing (shard-migration tools\opsmirror.py)')
    Say ('  2. docker compose -p ' + $Project + ' stop play-tests on ' + $where + ', then set Haven''s data aside (data/testing.before-rollback-<time>, kept) so a later cutover seeds fresh; git-pull stays')
    Say ('  3. docker start ' + $GamePcContainer + ' on this PC (or docker\test-queue\Start-TestQueue.ps1 if it is gone), and wait for http://localhost:8093/')
    Say ('  4. write mode local to ' + $StateFile)
    Confirm-Go 'roll back'
    Step 'Rollback'
    $tmpState = Join-Path ([IO.Path]::GetTempPath()) ('ops-rollback-' + [guid]::NewGuid().ToString('N') + '.json')
    [IO.File]::WriteAllText($tmpState, '{"mode": "haven"}', (New-Object Text.UTF8Encoding $false))
    $mirror = & python 'D:\UO\shard-migration\tools\opsmirror.py' --root $MirrorRoot --config $Config --state $tmpState --token-file $TokenFile --api $apiBase 2>&1
    Remove-Item -LiteralPath $tmpState -ErrorAction SilentlyContinue
    $mirror | ForEach-Object { Say ('    ' + $_) }
    if (-not (@($mirror) -match 'mirror refreshed') -and -not $Yes) {
        $t = Read-Host "Haven's data could not be taken (above). Marks made on Haven since the last commit would be lost. Type 'roll back anyway' to go on"
        if ($t -cne 'roll back anyway') { Say 'Stopped: nothing else was changed.' 'Yellow'; exit 1 }
    }
    Compose 'stop play-tests' | Out-Null
    # The game PC's copy is the current one from here on, so Haven's is kept but moved out of the way: a later cutover
    # then seeds from testing\ again instead of refusing (it never overwrites data it finds).
    $aside = 'testing.before-rollback-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
    Invoke-Haven ('if [ -d ' + $root + '/data/testing ]; then mv ' + $root + '/data/testing ' + $root + '/data/' + $aside + ' && echo "Haven data set aside: data/' + $aside + '"; fi') | Out-Null
    & docker start $GamePcContainer 2>&1 | ForEach-Object { Say ('    | ' + $_) }
    if ($LASTEXITCODE -ne 0) {
        Say ('  ' + $GamePcContainer + ' is not there; starting it with Start-TestQueue.ps1') 'Yellow'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo ([string]$cfg.gamePc.start))
    }
    if (-not $Local -and -not (Wait-Http 'http://localhost:8093/api/summary' 90)) { Say '  the game PC''s page did not answer in 90 s; check docker logs sl-play-tests' 'Red' }
    Write-Mode 'local'
    if (@($mirror) -match 'mirror refreshed') { Say 'Rolled back: the game PC serves the pages again, with everything marked on Haven.' 'Green' }
    else { Say ('Rolled back: the game PC serves the pages again. Haven''s data was NOT taken into testing\; it is kept on Haven in data/' + $aside + '.') 'Yellow' }
    exit 0
}
