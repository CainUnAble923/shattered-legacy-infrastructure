#Requires -Version 5.1
<#
  cc-P91: the overnight play-test bot, end to end, in one command. cc-P102: nightly, only when there is work.

      powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\tools\playtest-bot\Run-OvernightPlaytests.ps1
          [-Force] [-SkipBuild] [-SkipSoak] [-Only T-041,T-042] [-SoakMinutes 120] [-Runs 3] [-NoEmail] [-KeepUp]

  0. cc-P102: decides whether tonight has work (uobot\work.py): an OPEN play-test item with a bot check that has not run
     against the code it would test (HEAD, or -Commit). No work: one line in BotRoot\overnight.log, no email, exit 0.
     -Force runs anyway; -Only runs the named checks whatever the work check says (a person asked for them).
     With work: the "starting" email (the item ids, the expected length, how to stop it).
  1. Checks rule 1 first: the live shard (sl-modernuo, 2593) and the real test shard (sl-modernuo-test, 2594) are never
     started, stopped or rebuilt here; their state is recorded before and after, and the run fails if either changed.
  2. Builds sl-modernuo:bot through the gates (docker/uo/build.sh -t sl-modernuo:bot), unless -SkipBuild.
  3. Makes the scratch world in D:\UO\bot-shard (outside both repos): a COPY of the test world's newest complete automatic
     backup, a copy of Configuration-test set to 127.0.0.1:2595 and "Shattered Legacy BOT", and a TestAccountWipe request
     (cc-P73) that keeps the world and leaves one admin, slbot-admin, whose random password lives only in
     tools\playtest-bot\secrets.local.json (git-ignored) and is never printed.
  4. Starts it as compose project sl-uo-bot, container sl-modernuo-bot, published on 127.0.0.1:2595 only.
  5. Runs the checks (python -m uobot.run), each -Runs times; then the soak, unless -SkipSoak.
  6. Tears it down (docker compose -p sl-uo-bot down -t 60), keeps the world folder, writes the report to
     D:\UO\shard-migration\testing\bot-runs\<yyyy-MM-dd-HHmm>\.
     cc-P706: its transcripts\ (10 to 21 MB a night) are git-ignored by the docs repo (.gitignore), as run.log is; only
     report.md, results.json, email.txt and the other small files are committed. Commit-ShardWork.ps1 leaves anything
     else over its 5 MB cap under testing\bot-runs\ out of a commit instead of refusing the merge.
  7. cc-P102: moves transcripts older than retentionDays to iCloud Drive (uobot\archive.py; copied, verified, then
     removed), then emails the summary with the Golem's mail settings (read only), unless -NoEmail.
  If any step throws (a build that fails, a shard that will not boot), the scratch shard is stopped if this run started
  it, the archive still runs, and an after email still goes out saying so, with the last 40 lines of run.log.
  Settings: overnight.json beside this script. -MailOutbox <dir> (tests) writes the mails there instead of sending.
  ASCII only.
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$SkipBuild,
    [switch]$SkipSoak,
    [string[]]$Only,
    [int]$SoakMinutes = 120,
    [int]$Runs = 3,
    [switch]$NoEmail,
    [switch]$KeepUp,
    # Build the scratch shard from this commit instead of the working tree (a detached worktree in BotRoot\src). Used when
    # the working tree's code does not boot: on 2026-10-13 HEAD crashed at start (cc-P91 notes), so the night ran 36c7ae9.
    [string]$Commit,
    [string]$BotRoot = 'D:\UO\bot-shard',
    [string]$RunsRoot = 'D:\UO\shard-migration\testing\bot-runs',
    [string]$Config,     # default: overnight.json beside this script ($PSScriptRoot is empty in a 5.1 param default)
    [string]$PlayTests,
    [string]$ArchiveDrive,
    [string]$MailOutbox
)
# Not 'Stop': in Windows PowerShell 5.1 a native command's stderr (docker's warnings) would become a terminating error.
# Every native call's exit code is checked explicitly instead, and the script's own failures throw.
$ErrorActionPreference = 'Continue'
$here = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $here '..\..')).Path
$compose = Join-Path $here 'docker-compose.bot.yml'
$secrets = Join-Path $here 'secrets.local.json'
$project = 'sl-uo-bot'
$container = 'sl-modernuo-bot'
if (-not $Config) { $Config = Join-Path $PSScriptRoot 'overnight.json' }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if (-not $PlayTests) { $PlayTests = [string]$cfg.playTests }
if (-not $ArchiveDrive) { $ArchiveDrive = [string]$cfg.archiveDrive }
$py = 'python'
New-Item -ItemType Directory -Force -Path $BotRoot | Out-Null
$nightLog = Join-Path $BotRoot 'overnight.log'
$log = $null

function NightLine([string]$Text) {
    # One line per night, outside both repos, so a quiet night leaves no commit behind.
    Add-Content -LiteralPath $nightLog -Value ((Get-Date).ToString('yyyy-MM-dd HH:mm') + '  ' + $Text) -Encoding ASCII
}

function Say([string]$Text, [string]$Color = 'Gray') {
    $line = (Get-Date).ToString('HH:mm:ss') + '  ' + $Text
    Write-Host $line -ForegroundColor $Color
    if ($log) { Add-Content -LiteralPath $log -Value $line -Encoding ASCII }
}

function Text($x) {
    # A native command's stderr line arrives as an ErrorRecord; its message is the line (not "RemoteException").
    if ($x -is [System.Management.Automation.ErrorRecord]) { return [string]$x.Exception.Message }
    [string]$x
}

function Invoke-Py([string[]]$PyArgs) {
    Push-Location $here
    try { $out = @(& $py @PyArgs 2>&1 | ForEach-Object { Text $_ }) } finally { Pop-Location }
    @{ Exit = $LASTEXITCODE; Lines = $out }
}

$script:mailCount = 0
function Send-Mail([string]$TextFile) {
    # Every mail goes through here, so -NoEmail silences all three and -MailOutbox catches all three.
    if ($NoEmail) { return }
    $script:mailCount++
    if ($MailOutbox) {
        New-Item -ItemType Directory -Force -Path $MailOutbox | Out-Null
        Copy-Item -LiteralPath $TextFile -Destination (Join-Path $MailOutbox ('{0:D2}-{1}' -f $script:mailCount, (Split-Path -Leaf $TextFile)))
        Say ('  mail written to the outbox: ' + (Split-Path -Leaf $TextFile))
        return
    }
    $mail = Join-Path $here 'Send-BotReport.ps1'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $mail -RunDir $runDir -TextFile $TextFile 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) }
}

function Invoke-Archive {
    # Part D: never throws; its one line goes into the after email whatever happened.
    $j = Join-Path $runDir 'archive.json'
    $r = Invoke-Py @('-B', '-m', 'uobot.archive', '--runs-root', $RunsRoot, '--drive', $ArchiveDrive, '--path', [string]$cfg.archivePath,
                     '--days', [string]$cfg.retentionDays, '--json', $j)
    $r.Lines | ForEach-Object { Say ('  ' + $_) }
    try { return [string](Get-Content -LiteralPath $j -Raw | ConvertFrom-Json).note } catch { return ('Archive problem: the archive step failed (' + ($r.Lines -join ' ') + '); nothing was removed.') }
    finally { Remove-Item -LiteralPath $j -ErrorAction SilentlyContinue }
}

function Get-RealShards {
    # The two shards this run must never touch: name, state and start time, for the before/after comparison.
    $out = [ordered]@{}
    foreach ($n in @('sl-modernuo', 'sl-modernuo-test')) {
        $s = (& docker inspect --format '{{.State.Status}} {{.State.StartedAt}} {{.Image}}' $n 2>$null)
        if ($LASTEXITCODE -ne 0) { $s = 'absent' }
        $out[$n] = [string]$s
    }
    $out
}

function Assert-Rule1 {
    # The compose file must name only the bot's project, container, image and loopback port. Read it, do not trust it.
    # Comment lines are left out: the file's comments name the real shards on purpose, to say they are not used.
    $text = ((Get-Content -LiteralPath $compose) | Where-Object { $_ -notmatch '^\s*#' } | ForEach-Object { ($_ -split '\s#')[0] }) -join "`n"
    $bad = @()
    if ($text -notmatch '(?m)^name:\s*sl-uo-bot\s*$') { $bad += 'the compose project is not sl-uo-bot' }
    if ($text -notmatch 'container_name:\s*sl-modernuo-bot') { $bad += 'the container is not sl-modernuo-bot' }
    if ($text -notmatch 'image:\s*sl-modernuo:bot') { $bad += 'the image is not sl-modernuo:bot' }
    if ($text -match '2593|2594') { $bad += 'the compose file mentions 2593 or 2594' }
    if ($text -notmatch '"127\.0\.0\.1:2595:2595"') { $bad += 'the port is not 127.0.0.1:2595:2595' }
    # A host-side path into a real shard's world or Configuration (the container-side /var/lib/uo/modernuo is fine).
    if ($text -match '\$\{SERVER_PATH\}/lib/|\$\{SERVER_PATH\}/uo/|modernuo-test|Configuration-test') { $bad += 'the compose file mounts a real shard folder' }
    if ($BotRoot -match 'ShatteredLegacy|shard-migration') { $bad += 'the bot world would be inside a repo' }
    if ($bad) { throw ('Rule 1 refused: ' + ($bad -join '; ')) }
}

# -- 0. does tonight have work? (cc-P102 Part A). Nothing is built, started or written into a repo before this answers.
$workFile = Join-Path $BotRoot 'work.json'
$wargs = @('-B', '-m', 'uobot.work', '--repo', $repo, '--plays', $PlayTests, '--runs-root', $RunsRoot,
           '--commit', $(if ($Commit) { $Commit } else { 'HEAD' }), '--runs', [string]$Runs,
           '--soak-minutes', $(if ($SkipSoak) { '0' } else { [string]$SoakMinutes }), '--json', $workFile)
if ($Force -or $Only) { $wargs += '--force' }
$wr = Invoke-Py $wargs
if ($wr.Exit -ne 0) {
    # The check itself failed (a bad -Commit, an unreadable play-tests.json): no run, but say so by mail.
    NightLine ('work check FAILED: ' + (($wr.Lines | Select-Object -Last 1) -join ''))
    $wr.Lines | ForEach-Object { Write-Host $_ }
    $tail = Join-Path $BotRoot 'work-check.log'
    [IO.File]::WriteAllLines($tail, [string[]]$wr.Lines)
    $runDir = $BotRoot
    $crashText = Join-Path $BotRoot 'email-crash.txt'
    $null = Invoke-Py @('-B', '-m', 'uobot.notify', 'crash', '--out', $crashText, '--short', $(if ($Commit) { $Commit } else { 'HEAD' }),
                        '--reason', 'the work check failed, nothing ran', '--log', $tail, '--run-dir', $BotRoot)
    Send-Mail $crashText
    exit 2
}
$work = Get-Content -LiteralPath $workFile -Raw | ConvertFrom-Json
if (-not $work.run) {
    NightLine ('no work on ' + $work.short + ': every open item with a bot check has run on this code; nothing built, no email')
    Write-Host ('No work tonight on ' + $work.short + '. (-Force runs anyway.)')
    exit 0
}

$stamp = (Get-Date).ToString('yyyy-MM-dd-HHmm')
$runDir = Join-Path $RunsRoot $stamp
New-Item -ItemType Directory -Force -Path $runDir | Out-Null
$log = Join-Path $runDir 'run.log'
Say ('== cc-P91 overnight play tests, run ' + $stamp) 'Cyan'
if ($work.work) { foreach ($i in @($work.items)) { Say ('  work: ' + $i.id + ': ' + $i.reason) } }
else { Say ('  no item has work; running because of ' + $(if ($Only) { '-Only' } else { '-Force' })) }
$beforeText = Join-Path $runDir 'email-before.txt'
$null = Invoke-Py @('-B', '-m', 'uobot.notify', 'before', '--work', $workFile, '--task', [string]$cfg.taskName, '--out', $beforeText)
Send-Mail $beforeText

$botStarted = $false
$before = $null
try {
    Assert-Rule1
    $before = Get-RealShards
    foreach ($k in $before.Keys) { Say ("  untouched: {0}: {1}" -f $k, $before[$k]) }
    [IO.File]::WriteAllText((Join-Path $runDir 'real-shards-before.json'), ($before | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))

    # The compose file's variables, set before any compose call (down included), so compose never blanks a path.
    $env:SERVER_PATH = (Join-Path $repo 'server') -replace '\\', '/'
    $env:BOT_ROOT = $BotRoot -replace '\\', '/'
    $envFile = Join-Path $repo 'docker\uo\.env'
    $env:CLIENT_DATA_PATH = (Select-String -LiteralPath $envFile -Pattern '^CLIENT_DATA_PATH=(.*)$' | Select-Object -First 1).Matches[0].Groups[1].Value

    # -- 2. the image
    $src = $repo
    $builtFrom = 'working tree at ' + ((& git -C $repo rev-parse --short HEAD) | Out-String).Trim()
    if ($Commit) {
        # A detached worktree outside both repos' working trees; the working tree itself is never checked out or changed.
        $src = Join-Path $BotRoot 'src'
        & git -C $repo worktree remove --force $src 2>$null | Out-Null
        if (Test-Path -LiteralPath $src) { Remove-Item -LiteralPath $src -Recurse -Force -ErrorAction Stop }
        & git -C $repo worktree add --detach $src $Commit 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) }
        if ($LASTEXITCODE -ne 0) { throw ('git worktree add failed for ' + $Commit) }
        $builtFrom = 'commit ' + ((& git -C $src rev-parse --short HEAD) | Out-String).Trim() + ' (-Commit, a worktree in ' + $src + ')'
        $env:SERVER_PATH = (Join-Path $src 'server') -replace '\\', '/'
    }
    $commitSha = ((& git -C $src rev-parse HEAD) | Out-String).Trim()
    Say ('  code: ' + $builtFrom)
    if (-not $SkipBuild) {
        Say '== building sl-modernuo:bot through the gates' 'Cyan'
        $bash = 'C:\Program Files\Git\bin\bash.exe'
        & $bash -lc ("cd '" + ($src -replace '\\', '/') + "' && docker/uo/build.sh -t sl-modernuo:bot") 2>&1 | ForEach-Object { Add-Content -LiteralPath $log -Value (Text $_) -Encoding ASCII }
        if ($LASTEXITCODE -ne 0) { throw 'build.sh failed: see run.log' }
    }
    & docker image inspect sl-modernuo:bot --format '{{.Id}}' 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'no sl-modernuo:bot image: run without -SkipBuild' }

    # -- 3. the scratch world
    Say '== making the scratch world' 'Cyan'
    & docker compose -f $compose -p $project down -t 60 2>&1 | Out-Null    # a previous run left up by -KeepUp
    $world = Join-Path $BotRoot 'world'
    $conf = Join-Path $BotRoot 'Configuration'
    $testWorld = Join-Path $repo 'server\lib\uo\modernuo-test'
    $auto = Join-Path $testWorld 'Backups\Automatic'
    # The test shard may be mid-save, so the live Saves folder is never copied: the newest automatic backup is a whole save.
    $backup = Get-ChildItem -LiteralPath $auto -Directory | Sort-Object Name -Descending |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'Accounts') } | Select-Object -First 1
    if (-not $backup) { throw ('no automatic backup with Accounts in ' + $auto) }
    if (Test-Path -LiteralPath $world) { Remove-Item -LiteralPath $world -Recurse -Force -ErrorAction Stop }
    New-Item -ItemType Directory -Force -Path (Join-Path $world 'Saves') | Out-Null
    Copy-Item -Path (Join-Path $backup.FullName '*') -Destination (Join-Path $world 'Saves') -Recurse -ErrorAction Stop
    Say ('  copied the test world backup ' + $backup.Name + ' (Backups\Automatic) into ' + $world + '\Saves')
    # cc-P215: every Golem's own test accounts (hashes only), so a Golem running the bot (GOLEM_NAME) logs in as its own
    # '<golem>-admin' and '<golem>-player': uobot/run.py seeds them as slbot-admin after the wipe ([SeedGolemAccounts).
    $golemAccounts = 'D:\UO\golem\golem-accounts.json'
    if (Test-Path -LiteralPath $golemAccounts) { Copy-Item -LiteralPath $golemAccounts -Destination (Join-Path $world 'golem-accounts.json') -Force }
    if (Test-Path -LiteralPath $conf) { Remove-Item -LiteralPath $conf -Recurse -Force -ErrorAction Stop }
    Copy-Item -LiteralPath (Join-Path $repo 'server\uo\modernuo\Configuration-test') -Destination $conf -Recurse -ErrorAction Stop
    $mj = Join-Path $conf 'modernuo.json'
    $j = [IO.File]::ReadAllText($mj)
    $j = $j -replace '"0\.0\.0\.0:2594"', '"0.0.0.0:2595"'
    $j = $j -replace '"serverListing\.address":\s*("[^"]*"|null)', '"serverListing.address": "127.0.0.1"'
    $j = $j -replace '"serverListing\.serverName":\s*"[^"]*"', '"serverListing.serverName": "Shattered Legacy BOT"'
    if ($j -notmatch '"clusterf\.testShard\.allowAccountWipe"') { $j = $j -replace '"settings":\s*\{', ('"settings": {' + "`n" + '    "clusterf.testShard.allowAccountWipe": "True",') }
    else { $j = $j -replace '"clusterf\.testShard\.allowAccountWipe":\s*"[^"]*"', '"clusterf.testShard.allowAccountWipe": "True"' }
    [IO.File]::WriteAllText($mj, $j, (New-Object Text.UTF8Encoding $false))
    $cfgShard = $j | ConvertFrom-Json
    $listeners = @($cfgShard.listeners)
    if ($listeners.Count -ne 1 -or $listeners[0] -ne '0.0.0.0:2595') { throw ('Rule 1 refused: the bot Configuration listens on ' + ($listeners -join ',')) }
    $set = $cfgShard.settings
    foreach ($k in @('serverListing.address', 'serverListing.serverName', 'accountHandler.enableAutoAccountCreation', 'testCenter.enable', 'testCenter.skillGainMultiplier', 'clusterf.testShard.allowAccountWipe', 'assistants.enableNegotiation', 'clientVerification.minRequired')) {
        Say ("  setting {0} = {1}" -f $k, $set.$k)
    }
    if ($set.'serverListing.address' -ne '127.0.0.1') { throw 'Rule 1 refused: serverListing.address is not 127.0.0.1' }
    # commitSha (cc-P102): the full commit, so the next night's work check can tell whether the code has changed.
    $facts = [ordered]@{ code = $builtFrom; commitSha = $commitSha; backup = $backup.Name; testCenterEnable = $set.'testCenter.enable'; testCenterSkillGainMultiplier = $set.'testCenter.skillGainMultiplier';
        autoAccountCreation = $set.'accountHandler.enableAutoAccountCreation'; skillGainAmountMultiplier = $set.'clusterf.skillGain.amountMultiplier' }
    [IO.File]::WriteAllText((Join-Path $runDir 'world.json'), ($facts | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))

    # The account wipe (cc-P73), through the console's own request format.
    . (Join-Path $repo 'scripts\Shard-Console.ps1') -LoadOnly
    $bytes = New-Object byte[] 18
    ([Security.Cryptography.RandomNumberGenerator]::Create()).GetBytes($bytes)
    $adminPw = ([Convert]::ToBase64String($bytes) -replace '[+/=]', 'x')
    $playerBytes = New-Object byte[] 18
    ([Security.Cryptography.RandomNumberGenerator]::Create()).GetBytes($playerBytes)
    $playerPw = ([Convert]::ToBase64String($playerBytes) -replace '[+/=]', 'y')
    $sec = [ordered]@{ admin = 'slbot-admin'; adminPassword = $adminPw; player = 'slbot-player'; playerPassword = $playerPw; madeAt = $stamp }
    [IO.File]::WriteAllText($secrets, ($sec | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
    $reqId = 'bot-' + $stamp + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
    $req = New-WipeRequestText -Admin 'slbot-admin' -Hash (New-WipePasswordHash -Password $adminPw) -Id $reqId -Now (Get-Date) -By 'Run-OvernightPlaytests.ps1'
    [IO.File]::WriteAllText((Join-Path $world 'account-wipe-request.json'), $req, (New-Object Text.UTF8Encoding $false))
    Say ('  account wipe requested (' + $reqId + '): keeps the world, leaves slbot-admin; the password is in secrets.local.json only')

    # -- 4. start it
    Say '== starting sl-modernuo-bot on 127.0.0.1:2595' 'Cyan'
    $botStarted = $true
    & docker compose -f $compose -p $project up -d 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) }
    if ($LASTEXITCODE -ne 0) { throw 'compose up failed' }
    $deadline = (Get-Date).AddMinutes(10)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
        $logs = (& docker logs $container 2>&1 | Out-String)
        # Both: the listener, and the wipe's outcome line (DONE is printed once the wiped world is saved).
        if ($logs -match 'Listening:.*2595' -and (ConvertFrom-WipeLog $logs).Outcome) { $ready = $true; break }
        if ((& docker inspect --format '{{.State.Status}}' $container) -ne 'running') { break }
    }
    & docker logs $container 2>&1 | Out-File -LiteralPath (Join-Path $runDir 'shard-start.log') -Encoding ascii
    $wipe = ConvertFrom-WipeLog (Get-Content -Raw -LiteralPath (Join-Path $runDir 'shard-start.log'))
    Say ('  wipe: ' + $wipe.Outcome + ' | ' + $wipe.Line)
    if (-not $ready) { throw 'the scratch shard did not start listening on 2595 in 10 minutes: see shard-start.log' }
    if ($wipe.Outcome -ne 'done') { throw ('the account wipe did not report done (' + $wipe.Outcome + '): see shard-start.log') }

    # -- 5. the checks and the soak
    $runArgs = @('-B', '-m', 'uobot.run', '--secrets', $secrets, '--out', $runDir, '--runs', $Runs, '--container', $container)
    if ($Only) { $runArgs += @('--only', ($Only -join ',')) }
    if (-not $SkipSoak) { $runArgs += @('--soak-minutes', $SoakMinutes) }
    Say ('== running the checks' + $(if ($SkipSoak) { '' } else { ' and the soak' })) 'Cyan'
    Push-Location $here
    try { & $py @runArgs 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) } } finally { Pop-Location }
    $checksExit = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath (Join-Path $runDir 'results.json'))) { throw ('the checks wrote no results.json (exit ' + $checksExit + ')') }

    # -- 6. down, compare, report
    & docker logs $container 2>&1 | Out-File -LiteralPath (Join-Path $runDir 'shard.log') -Encoding ascii
    if (-not $KeepUp) {
        Say '== stopping the scratch shard (60 s for its save)' 'Cyan'
        & docker compose -f $compose -p $project down -t 60 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) }
    }
    $after = Get-RealShards
    [IO.File]::WriteAllText((Join-Path $runDir 'real-shards-after.json'), ($after | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
    $changed = @($before.Keys | Where-Object { $before[$_] -ne $after[$_] })
    foreach ($k in $after.Keys) { Say ("  after: {0}: {1}" -f $k, $after[$k]) }
    if ($changed) { Say ('  RULE 1: these changed during the run: ' + ($changed -join ', ') + '. Read the report.') 'Red' }
    else { Say '  rule 1 held: both real shards are exactly as they were' 'Green' }
} catch {
    # cc-P102: a crash or a shard that will not boot still gets an after email, with the log's last 40 lines.
    $reason = ([string]$_.Exception.Message -split "`n")[0].Trim()
    if ($reason.Length -gt 140) { $reason = $reason.Substring(0, 137) + '...' }
    Say ('== FAILED: ' + $reason) 'Red'
    if ($botStarted -and -not $KeepUp) {
        # Only the bot's own project, and only when this run started it.
        & docker logs $container 2>&1 | Out-File -LiteralPath (Join-Path $runDir 'shard.log') -Encoding ascii
        Say '== stopping the scratch shard after the failure' 'Cyan'
        & docker compose -f $compose -p $project down -t 60 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) }
    }
    if ($before) {
        $after = Get-RealShards
        $changed = @($before.Keys | Where-Object { $before[$_] -ne $after[$_] })
        if ($changed) { Say ('  RULE 1: these changed during the run: ' + ($changed -join ', ')) 'Red' } else { Say '  rule 1 held' 'Green' }
    }
    $note = Invoke-Archive
    $crashText = Join-Path $runDir 'email-crash.txt'
    # Windows PowerShell 5.1 drops an empty-string argument to a native command, so an empty note is left out.
    $crashArgs = @('-B', '-m', 'uobot.notify', 'crash', '--out', $crashText, '--short', [string]$work.short, '--reason', $reason,
                   '--log', $log, '--run-dir', $runDir)
    if ($note) { $crashArgs += @('--archive-note', $note) }
    $null = Invoke-Py $crashArgs
    Send-Mail $crashText
    NightLine ('FAILED on ' + $work.short + ': ' + $reason + ' (' + $runDir + ')')
    exit 3
}

# -- 7. the archive, the report, the after email
Say ('== archiving transcripts older than ' + $cfg.retentionDays + ' days') 'Cyan'
$note = Invoke-Archive
$reportArgs = @('-B', '-m', 'uobot.report', '--out', $runDir)
if ($note) { $reportArgs += @('--archive-note', $note) }
if ($changed) { $reportArgs += @('--rule1-changed', ($changed -join ',')) }
$rr = Invoke-Py $reportArgs
$rr.Lines | ForEach-Object { Say ('  ' + $_) }
Send-Mail (Join-Path $runDir 'email.txt')
NightLine ('ran on ' + $work.short + ': ' + (($rr.Lines | Select-Object -Last 1) -join '') + ' (' + $runDir + ')')
Say ('== done: ' + (Join-Path $runDir 'report.md')) 'Cyan'
exit $checksExit
