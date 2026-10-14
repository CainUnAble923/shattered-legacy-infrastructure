#Requires -Version 5.1
<#
  cc-P91: the overnight play-test bot, end to end, in one command.

      powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\tools\playtest-bot\Run-OvernightPlaytests.ps1
          [-SkipBuild] [-SkipSoak] [-Only T-041,T-042] [-SoakMinutes 120] [-Runs 3] [-NoEmail] [-KeepUp]

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
     D:\UO\shard-migration\testing\bot-runs\<yyyy-MM-dd-HHmm>\, and emails the headline with the Golem's mail
     settings (read only), unless -NoEmail.
  ASCII only.
#>
param(
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
    [string]$RunsRoot = 'D:\UO\shard-migration\testing\bot-runs'
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
$stamp = (Get-Date).ToString('yyyy-MM-dd-HHmm')
$runDir = Join-Path $RunsRoot $stamp
New-Item -ItemType Directory -Force -Path $runDir | Out-Null
$log = Join-Path $runDir 'run.log'

function Say([string]$Text, [string]$Color = 'Gray') {
    $line = (Get-Date).ToString('HH:mm:ss') + '  ' + $Text
    Write-Host $line -ForegroundColor $Color
    Add-Content -LiteralPath $log -Value $line -Encoding ASCII
}

function Text($x) {
    # A native command's stderr line arrives as an ErrorRecord; its message is the line (not "RemoteException").
    if ($x -is [System.Management.Automation.ErrorRecord]) { return [string]$x.Exception.Message }
    [string]$x
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

# The compose file's variables, set before any compose call (down included), so compose never blanks a path.
$env:SERVER_PATH = (Join-Path $repo 'server') -replace '\\', '/'
$env:BOT_ROOT = $BotRoot -replace '\\', '/'
$envFile = Join-Path $repo 'docker\uo\.env'
$env:CLIENT_DATA_PATH = (Select-String -LiteralPath $envFile -Pattern '^CLIENT_DATA_PATH=(.*)$' | Select-Object -First 1).Matches[0].Groups[1].Value

Say ('== cc-P91 overnight play tests, run ' + $stamp) 'Cyan'
Assert-Rule1
$before = Get-RealShards
foreach ($k in $before.Keys) { Say ("  untouched: {0}: {1}" -f $k, $before[$k]) }
[IO.File]::WriteAllText((Join-Path $runDir 'real-shards-before.json'), ($before | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))

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
$cfg = $j | ConvertFrom-Json
$listeners = @($cfg.listeners)
if ($listeners.Count -ne 1 -or $listeners[0] -ne '0.0.0.0:2595') { throw ('Rule 1 refused: the bot Configuration listens on ' + ($listeners -join ',')) }
$set = $cfg.settings
foreach ($k in @('serverListing.address', 'serverListing.serverName', 'accountHandler.enableAutoAccountCreation', 'testCenter.enable', 'testCenter.skillGainMultiplier', 'clusterf.testShard.allowAccountWipe', 'assistants.enableNegotiation', 'clientVerification.minRequired')) {
    Say ("  setting {0} = {1}" -f $k, $set.$k)
}
if ($set.'serverListing.address' -ne '127.0.0.1') { throw 'Rule 1 refused: serverListing.address is not 127.0.0.1' }
$facts = [ordered]@{ code = $builtFrom; backup = $backup.Name; testCenterEnable = $set.'testCenter.enable'; testCenterSkillGainMultiplier = $set.'testCenter.skillGainMultiplier';
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
$py = 'python'
$args = @('-B', '-m', 'uobot.run', '--secrets', $secrets, '--out', $runDir, '--runs', $Runs, '--container', $container)
if ($Only) { $args += @('--only', ($Only -join ',')) }
if (-not $SkipSoak) { $args += @('--soak-minutes', $SoakMinutes) }
Say ('== running the checks' + $(if ($SkipSoak) { '' } else { ' and the soak' })) 'Cyan'
Push-Location $here
try { & $py @args 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) } } finally { Pop-Location }
$checksExit = $LASTEXITCODE

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

$reportArgs = @('-B', '-m', 'uobot.report', '--out', $runDir)
if ($changed) { $reportArgs += @('--rule1-changed', ($changed -join ',')) }
Push-Location $here
try { & $py @reportArgs 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) } } finally { Pop-Location }

if (-not $NoEmail) {
    $mail = Join-Path $here 'Send-BotReport.ps1'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $mail -RunDir $runDir 2>&1 | ForEach-Object { Say ('  ' + (Text $_)) }
}
Say ('== done: ' + (Join-Path $runDir 'report.md')) 'Cyan'
exit $checksExit
