#Requires -Version 5.1
# Facts for the pure core of scripts/Shard-Console.ps1.
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\Shard-Console.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green.
#
# Same shape as server/tests: one behaviour per fact, named for the behaviour, with the reason
# it exists written beside it. Not xUnit, because the thing under test is PowerShell 5.1 and
# the dotnet test gate runs in a Linux container that has neither 5.1 nor WinForms; and not
# Pester, because the Pester that ships with Windows is 3.4 and anything newer would be an
# install. A fact is a scriptblock that throws on failure.
#
# THE FIXTURES ARE REAL. fixtures/console-capture-2026-09-29.txt was written by
# `Shard-Console.ps1 -Capture` against the running Docker on 2026-09-29: docker inspect of both
# containers, docker image inspect of every image they name, docker logs from each container's
# StartedAt, the save folder listings, and docker/uo/docker-compose.yml. The only edit the
# capture makes is dropping log lines that name accounts or clients (Remove-AccountLines),
# because this repo is public. fixtures/console-capture-probe-2026-09-29.txt is the same for a
# throwaway container of sl-modernuo:latest on an empty save with no network, which is the only
# way to get a real owner-account prompt without touching either shard. Invented docker output
# agrees with an invented parser; these do not have to.
#
# Snapshot names and plans are inputs this tool generates itself, so those facts use literals.

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$script  = Join-Path $here '..\Shard-Console.ps1'
$fixture = Join-Path $here 'fixtures'
. $script -LoadOnly

$results = New-Object 'System.Collections.Generic.List[object]'

function Fact {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Message = '' })
        Write-Host "  PASS  $Name" -ForegroundColor Green
    } catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Message = $_.Exception.Message })
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Because = '')
    if ($Expected -ne $Actual) { throw "expected [$Expected] but got [$Actual]. $Because" }
}

function Assert-True {
    param($Condition, [string]$Because)
    if (-not $Condition) { throw "not true: $Because" }
}

function Read-Fixture {
    param([string]$Name)
    ConvertFrom-CaptureFile ([IO.File]::ReadAllText((Join-Path $fixture $Name)))
}

$repo   = (Resolve-Path (Join-Path $here '..\..')).Path
$config = Get-ConsoleConfig -RepoRoot $repo
$cap    = Read-Fixture 'console-capture-2026-09-29.txt'
$probe  = Read-Fixture 'console-capture-probe-2026-09-29.txt'
$state  = New-ConsoleState -Config $config -Sections $cap
$now    = $state.Now
$test   = $state.Shards['test']
$live   = $state.Shards['live']

$probeContainer = ConvertFrom-ContainerInspect $probe['inspect sl-console-probe']
$probeLatest    = ConvertFrom-ImageInspect $probe['image sl-modernuo:latest']

function Get-Plan {
    param([string]$Action, [string]$Shard = 'test', [string]$SnapshotName, [string]$Snapshot, $State = $state)
    @(Get-ActionPlan -Action $Action -ShardKey $Shard -Config $config -State $State -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now)
}

# A copy of the captured state with one snapshot on each shard, for the restore and delete plans.
# The live container is running in the capture and the test container is exited, so between
# them these cover both halves of rule 1 (stop only what is running, start only what was).
function New-StateWithSnapshots {
    param([string[]]$Names)
    $s = New-ConsoleState -Config $config -Sections $cap
    foreach ($k in 'test', 'live') {
        $s.Shards[$k].Snapshots = @($Names | ForEach-Object {
            $p = ConvertFrom-SnapshotFolderName $_
            $p | Add-Member -NotePropertyName Bytes -NotePropertyValue 1000 -PassThru |
                 Add-Member -NotePropertyName Files -NotePropertyValue 20 -PassThru |
                 Add-Member -NotePropertyName HasAccounts -NotePropertyValue $true -PassThru
        })
    }
    $s
}
$snapState = New-StateWithSnapshots @('20260929-101500_before-gargoyle-fix')
$twoState  = New-StateWithSnapshots @('20260929-101500_before-gargoyle-fix', '20260929-111500_PRE-RESTORE_before-restoring-x')

Write-Host ""
Write-Host "Shard-Console core facts" -ForegroundColor Cyan

# --- parsing docker inspect -----------------------------------------------------------------

Fact 'TheTestContainerInspectParsesToItsImageStateAndPort' {
    $c = $test.Container
    Assert-True $c.Exists 'the test container is in the capture'
    Assert-Equal 'sl-modernuo-test' $c.Name
    Assert-Equal 'sha256:2e7dcae65e54852cbb07ee1dd45779d5362c327b97f5e7c3b139026b11eb24db' $c.ImageId
    Assert-Equal 'sl-modernuo:latest' $c.ConfigImage
    Assert-Equal 'exited' $c.Status
    Assert-Equal $false $c.Running
    Assert-Equal 255 $c.ExitCode
    Assert-Equal 2594 $c.GamePort 'the published tcp port, not the 2593 the Dockerfile EXPOSEs'
    Assert-Equal 'sl-uo-test' $c.ComposeProject 'D22: its own compose project'
}

Fact 'TheLiveContainerInspectParsesToTheTagItWasCreatedFrom' {
    $c = $live.Container
    Assert-Equal 'sl-modernuo' $c.Name
    Assert-Equal 'uo-modernuo' $c.ConfigImage 'bug-list D25: not sl-modernuo:latest'
    Assert-Equal $true $c.Running
    Assert-Equal 2593 $c.GamePort
    Assert-Equal 'uo' $c.ComposeProject
}

Fact 'DockerTimestampsWithNanosecondsParseAsUtc' {
    # .NET parses at most seven fractional digits; docker writes nine.
    $t = $live.Container.StartedAt
    Assert-Equal ([datetime]::new(2026, 9, 28, 19, 7, 18, [DateTimeKind]::Utc)) ($t.AddTicks(-($t.Ticks % 10000000)))
    Assert-Equal ([DateTimeKind]::Utc) $t.Kind
}

Fact 'DockersZeroTimeMeansNever' {
    # A running container that has never finished reports FinishedAt as year 1.
    Assert-Equal $true $probeContainer.Running
    Assert-Equal $null $probeContainer.FinishedAt
}

Fact 'AContainerThatDoesNotExistParsesAsMissing' {
    $c = ConvertFrom-ContainerInspect $cap['inspect sl-console-missing']
    Assert-Equal $false $c.Exists
    Assert-True ($c.Error -match 'No such') "the daemon's error is kept: $($c.Error)"
}

Fact 'AnImageThatIsNoLongerInTheStoreParsesAsMissing' {
    # Both running images are gone: sl-modernuo:latest moved off the test one, and D29 records
    # the live one.
    Assert-Equal $false $test.RunningImage.Exists
    Assert-Equal $false $live.RunningImage.Exists
}

Fact 'TheLatestImageParses' {
    Assert-Equal 'sha256:8304d4a24fd2b6bcfe37496d1a3cb85a97b14a1cff7b069b97ed5fc2d165c712' $state.Latest.Id
    Assert-Equal ([datetime]::new(2026, 9, 29, 11, 53, 29, [DateTimeKind]::Utc)) ($state.Latest.Created.AddTicks(-($state.Latest.Created.Ticks % 10000000)))
}

# --- drift -------------------------------------------------------------------------------------

Fact 'DriftComparesImageIdsNotTagNames' {
    # The failure this panel exists for. The test container was created from the name
    # sl-modernuo:latest, so a check on names says it is current. It is running 2e7dcae6 and the
    # name now means 8304d4a2: a rebuild plus a restart left the old code running.
    Assert-Equal 'sl-modernuo:latest' $test.Container.ConfigImage
    Assert-Equal 'DRIFTED' $test.Drift.Verdict
}

Fact 'TheTestContainerSaysTheTagMovedUnderIt' {
    $d = $test.Drift
    Assert-Equal $true $d.CreatedFromLatest
    Assert-Equal $true $d.TagMoved
    Assert-Equal $true $d.RunningImageGone
    Assert-Equal $true $d.Ambiguous 'a tag that moved is the ambiguity the brief asked to be shown, not resolved'
    Assert-True ($d.Advice -match 'Start') 'the test shard is told how to fix it'
}

Fact 'LiveDriftShowsTheTagAndEveryResolvedIdAndPicksNone' {
    $d = $live.Drift
    Assert-Equal 'DRIFTED' $d.Verdict
    Assert-Equal $true $d.Ambiguous
    Assert-Equal $false $d.CreatedFromLatest
    $text = $d.Lines -join "`n"
    foreach ($want in 'uo-modernuo', '36cc709a82a6', 'bc04a35bf344', '8304d4a24fd2') {
        Assert-True ($text -match [regex]::Escape($want)) "the drift lines show $want"
    }
}

Fact 'LiveDriftAdviceOffersNoDeploy' {
    Assert-True ($live.Drift.Advice -match 'not a button') $live.Drift.Advice
}

Fact 'AContainerCreatedFromTheCurrentLatestIsCurrent' {
    $d = Get-ImageDrift -Shard $config.Shards['test'] -Container $probeContainer -Latest $probeLatest `
        -ConfigRef $probeLatest -RunningImage $probeLatest -LatestTag 'sl-modernuo:latest'
    Assert-Equal 'CURRENT' $d.Verdict
    Assert-Equal $false $d.Ambiguous
}

# --- the listener ----------------------------------------------------------------------------

Fact 'TheLiveLogShowsTheGameListenerNotThePingListener' {
    Assert-Equal 'LISTENING' $live.Listener.State
    Assert-Equal '127.0.0.1:2593,172.19.0.3:2593' ($live.Listener.Addresses -join ',')
}

Fact 'AnExitedContainerIsNotListeningThoughItsLastRunSaidSo' {
    # docker logs still carries the last run's Listening line after the container exits.
    Assert-Equal 'STOPPED' $test.Listener.State
    Assert-True ($test.Listener.Detail -match 'last run') $test.Listener.Detail
}

Fact 'AListenerOnAnotherPortDoesNotCount' {
    $running = $live.Container
    $l = Get-ListenerState -Container $running -LogText $cap['logs sl-modernuo'] -GamePort 2594
    Assert-Equal 'NOT LISTENING' $l.State
}

Fact 'AWorldWithNoAccountsIsTheOwnerPrompt' {
    # D19. docker ps says Up and nothing listens.
    $l = Get-ListenerState -Container $probeContainer -LogText $probe['logs sl-console-probe'] -GamePort 2594
    Assert-Equal 'OWNER PROMPT' $l.State
    Assert-True ($l.Detail -match 'docker attach') $l.Detail
}

Fact 'TheLogIsReadFromStartedAtNotFromATail' {
    # After a day of autosaves the Listening line is thousands of lines back; a --tail misses it.
    $a = @(Get-LogWindowArgs -Container $live.Container -Minutes 15)
    Assert-Equal 'logs' $a[0]
    Assert-True ($a -contains '--since') 'reads from --since'
    Assert-True ($a -contains $live.Container.StartedAtRaw) 'since the container StartedAt'
    Assert-True ($a -contains '--until') 'bounded, so a month-old container is not read whole'
    Assert-True (-not ($a -contains '--tail')) 'no --tail'
    Assert-Equal 'sl-modernuo' $a[-1]
}

# --- saves and snapshots -----------------------------------------------------------------------

Fact 'TheSaveSummaryFindsTheNewestFileAndTheAccounts' {
    $s = $test.Save
    Assert-Equal $true $s.Exists
    Assert-Equal 21 $s.Count
    Assert-Equal $true $s.HasAccounts 'accounts live inside Saves'
    Assert-Equal ([datetime]::new(2026, 9, 28, 15, 45, 1, [DateTimeKind]::Utc)) $s.Newest.LastWriteUtc
}

Fact 'SnapshotNamesRejectPathsAndTheReservedWord' {
    foreach ($bad in '', '..\x', 'a/b', 'a b', 'pre-restore-mine', 'PreRestore', ('x' * 41), '.hidden') {
        Assert-True (Test-SnapshotLabel $bad) "'$bad' is rejected"
    }
    foreach ($good in 'before-gargoyle-fix', 'd25_check.2', ('x' * 40)) {
        Assert-Equal $null (Test-SnapshotLabel $good) "'$good' is accepted"
    }
}

Fact 'ASnapshotFolderNameRoundTrips' {
    $n = New-SnapshotFolderName -Label 'before-gargoyle-fix' -Now ([datetime]::new(2026, 9, 29, 10, 15, 0))
    Assert-Equal '20260929-101500_before-gargoyle-fix' $n
    $p = ConvertFrom-SnapshotFolderName $n
    Assert-Equal 'before-gargoyle-fix' $p.Label
    Assert-Equal ([datetime]::new(2026, 9, 29, 10, 15, 0)) $p.Taken
    Assert-Equal $false $p.IsPreRestore
}

Fact 'ThePreRestoreSnapshotIsObviouslyThePreRestoreOne' {
    $n = New-PreRestoreFolderName -RestoringFrom '20260929-101500_before-gargoyle-fix' -Now ([datetime]::new(2026, 9, 29, 11, 0, 0))
    Assert-True ($n -cmatch '_PRE-RESTORE_') $n
    Assert-Equal $true (ConvertFrom-SnapshotFolderName $n).IsPreRestore
}

Fact 'DeletingTheLastSnapshotSaysItIsTheLast' {
    $w = Get-SnapshotDeleteWarning -Snapshots $snapState.Shards['test'].Snapshots -Name '20260929-101500_before-gargoyle-fix' -Shard $config.Shards['test']
    Assert-True ($w -cmatch 'LAST') $w
    $w2 = Get-SnapshotDeleteWarning -Snapshots $twoState.Shards['test'].Snapshots -Name '20260929-101500_before-gargoyle-fix' -Shard $config.Shards['test']
    Assert-True (-not ($w2 -cmatch 'LAST')) 'not said when another remains'
}

# --- plans -------------------------------------------------------------------------------------

Fact 'ARestoreStopsThenSnapshotsThenMovesAsideThenCopies' {
    # Live is the running container in the capture, so its plan shows the whole sequence.
    $p = Get-Plan 'snapshot.restore' 'live' -Snapshot '20260929-101500_before-gargoyle-fix' -State $snapState
    $kinds = @($p | ForEach-Object { $_.Kind })
    $order = @('confirm', 'exec', 'stopped', 'copy', 'verify', 'move', 'copy', 'verify', 'exec', 'log')
    Assert-Equal ($order -join ',') (@($kinds | Where-Object { $_ -notin 'say' }) -join ',')
    $stop = @($p | Where-Object { $_.Kind -eq 'exec' })[0]
    Assert-Equal 'stop' $stop.Arguments[0]
    $pre = @($p | Where-Object { $_.Kind -eq 'copy' })[0]
    Assert-True ($pre.To -cmatch 'PRE-RESTORE') 'the first copy is the automatic pre-restore snapshot'
    $move = @($p | Where-Object { $_.Kind -eq 'move' })[0]
    Assert-True ($move.To -like '*.aside-*') 'the current save is moved aside'
}

Fact 'ARestoreNeverDeletesAnything' {
    foreach ($k in 'test', 'live') {
        $p = Get-Plan 'snapshot.restore' $k -Snapshot '20260929-101500_before-gargoyle-fix' -State $snapState
        Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'remove-snapshot' }).Count
    }
}

Fact 'ASnapshotOfAStoppedContainerNeitherStopsNorStartsIt' {
    $p = Get-Plan 'snapshot.create' 'test' -SnapshotName 'x'
    Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'exec' }).Count
}

Fact 'ASnapshotOfARunningContainerStopsItAndStartsItAgain' {
    $p = Get-Plan 'snapshot.create' 'live' -SnapshotName 'x'
    $exec = @($p | Where-Object { $_.Kind -eq 'exec' })
    Assert-Equal 'stop,start' (@($exec | ForEach-Object { $_.Arguments[0] }) -join ',')
    Assert-Equal 'confirm' $p[0].Kind
}

Fact 'EveryLiveActionBeginsWithATypedConfirmation' {
    foreach ($a in 'live.start', 'live.stop', 'live.restart', 'live.tail', 'live.client') {
        Assert-Equal 'confirm' (Get-Plan $a 'live')[0].Kind $a
    }
    foreach ($a in 'snapshot.create', 'snapshot.restore', 'snapshot.delete') {
        $p = Get-Plan $a 'live' -SnapshotName 'x' -Snapshot '20260929-101500_before-gargoyle-fix' -State $snapState
        Assert-Equal 'confirm' $p[0].Kind "$a on live"
    }
    foreach ($a in 'test.start', 'test.stop', 'snapshot.create') {
        Assert-Equal 0 @(Get-Plan $a 'test' -SnapshotName 'x' | Where-Object { $_.Kind -eq 'confirm' }).Count "$a on test needs none"
    }
}

Fact 'TypedConfirmationMustMatchExactly' {
    Assert-Equal $true  (Test-TypedConfirmation 'stop live' 'stop live')
    Assert-Equal $true  (Test-TypedConfirmation 'stop live' '  stop live ')
    Assert-Equal $false (Test-TypedConfirmation 'stop live' 'Stop live')
    Assert-Equal $false (Test-TypedConfirmation 'stop live' 'yes')
    Assert-Equal $false (Test-TypedConfirmation 'stop live' 'stop')
    Assert-Equal $false (Test-TypedConfirmation 'stop live' $null)
}

Fact 'NoPlanRunsComposeItself' {
    # Compose is reached only through Start-TestShard.ps1, which only ever names the test file.
    foreach ($a in Get-ConsoleActions) {
        foreach ($k in 'test', 'live') {
            $p = Get-Plan $a.Key $k -SnapshotName 'x' -Snapshot '20260929-101500_before-gargoyle-fix' -State $snapState
            foreach ($s in $p) {
                Assert-True (-not (@($s.Exe) + @($s.Arguments) -match 'compose')) "$($a.Key) $k"
            }
        }
    }
}

Fact 'LiveStartRefusesWhenTheContainerIsGone' {
    $gone = New-ConsoleState -Config $config -Sections $cap
    $gone.Shards['live'].Container = ConvertFrom-ContainerInspect $cap['inspect sl-console-missing']
    $p = Get-Plan 'live.start' 'live' -State $gone
    Assert-Equal 1 @($p | Where-Object { $_.Kind -eq 'refuse' }).Count
    Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'exec' }).Count
}

Fact 'StartFreshMovesTheTestSaveAsideAndNeverPassesFresh' {
    $p = Get-Plan 'test.fresh' 'test'
    $move = @($p | Where-Object { $_.Kind -eq 'move' })
    Assert-Equal 1 $move.Count
    Assert-Equal $config.Shards['test'].Saves $move[0].From
    foreach ($s in $p) { Assert-True (-not ($s.Arguments -contains '-Fresh')) 'no -Fresh: it deletes the test save' }
}

Fact 'StartSkipsTheBuildAndRebuildGoesThroughTheGates' {
    $start = @(Get-Plan 'test.start' | Where-Object { $_.Kind -eq 'exec' })[0]
    Assert-True ($start.Arguments -contains $config.StartTestShard) 'calls Start-TestShard.ps1'
    Assert-True ($start.Arguments -contains '-SkipBuild') 'start is the image already built'
    $rebuild = @(Get-Plan 'test.rebuild' | Where-Object { $_.Kind -eq 'exec' })[0]
    Assert-True ($rebuild.Arguments -contains $config.StartTestShard) 'calls Start-TestShard.ps1'
    Assert-True (-not ($rebuild.Arguments -contains '-SkipBuild')) 'so build.sh runs its gates'
}

# A count of violations is not enough: on the first run an empty result came back as one
# phantom violation, and these three facts passed on it while every clean plan was refused.
# So each names the reason it expects.
function Assert-Refused {
    param($Plan, [string]$Reason, [string]$Because)
    $v = @(Test-PlanSafety @($Plan) $config)
    Assert-True (@($v | Where-Object { $_ -is [string] -and $_ -match $Reason }).Count -gt 0) "$Because : refused for '$Reason'; got [$($v -join ' | ')]"
}

Fact 'TheSafetyCheckRejectsComposeOnTheLiveFile' {
    $bad = @(New-PlanStep -Kind exec -Exe 'docker' -Arguments @('compose', '-f', 'docker/uo/docker-compose.yml', 'up', '-d'))
    Assert-Refused $bad 'runs docker compose itself' 'compose up on the live file'
    $down = @(New-PlanStep -Kind exec -Exe 'docker' -Arguments @('compose', 'down'))
    Assert-Refused $down 'runs docker compose itself' 'compose down, which would stop sl-ddns'
}

Fact 'TheSafetyCheckRejectsDeletingAWorldSave' {
    foreach ($k in 'test', 'live') {
        $bad = @(New-PlanStep -Kind remove-snapshot -Path $config.Shards[$k].Saves)
        Assert-Refused $bad 'deletes a world save' "$k save"
        $inside = @(New-PlanStep -Kind remove-snapshot -Path (Join-Path $config.Shards[$k].Saves 'Accounts'))
        Assert-Refused $inside 'deletes a world save' "$k save contents"
        $move = @(New-PlanStep -Kind move -From $config.Shards[$k].Saves -To 'D:\elsewhere')
        Assert-Refused $move 'the only move' "$k save moved anywhere but aside"
    }
    $root = @(New-PlanStep -Kind remove-snapshot -Path $config.SnapshotRoot)
    Assert-Refused $root 'not one snapshot folder' 'the whole snapshot folder'
    $ok = @(New-PlanStep -Kind remove-snapshot -Path (Join-Path $config.SnapshotRoot 'test\20260929-101500_x'))
    Assert-Equal 0 @(Test-PlanSafety $ok $config).Count 'one snapshot is fine'
}

Fact 'TheSafetyCheckRejectsBuildsAndTouchingLiveUnconfirmed' {
    $build = @(New-PlanStep -Kind exec -Exe 'docker' -Arguments @('build', '.'))
    Assert-Refused $build 'docker build is not' 'docker build'
    $sh = @(New-PlanStep -Kind exec -Exe 'bash' -Arguments @('docker/uo/build.sh'))
    Assert-Refused $sh 'build\.sh is reached only' 'build.sh directly'
    $unconfirmed = @(New-PlanStep -Kind exec -Exe 'docker' -Arguments @('stop', 'sl-modernuo'))
    Assert-Refused $unconfirmed 'without a typed confirmation' 'live without a typed confirmation'
    $confirmed = @((New-PlanStep -Kind confirm -Phrase 'stop live'), (New-PlanStep -Kind exec -Exe 'docker' -Arguments @('stop', 'sl-modernuo')))
    Assert-Equal 0 @(Test-PlanSafety $confirmed $config).Count 'the same step with a typed confirmation passes'
}

Fact 'EveryRealPlanPassesTheSafetyCheck' {
    foreach ($a in Get-ConsoleActions) {
        foreach ($k in 'test', 'live') {
            $p = Get-Plan $a.Key $k -SnapshotName 'x' -Snapshot '20260929-101500_before-gargoyle-fix' -State $snapState
            $v = @(Test-PlanSafety $p $config)
            Assert-Equal 0 $v.Count "$($a.Key) $k : $($v -join '; ')"
        }
    }
}

Fact 'TheExecutorRunsNothingWithoutTheTypedPhraseOrTheYes' {
    # The executor, not only the plan. The step it would run writes a marker file, so a bug here
    # leaves evidence and touches nothing: never a real docker command against the live shard.
    $marker = Join-Path ([IO.Path]::GetTempPath()) ('shard-console-fact-' + [guid]::NewGuid().ToString('N'))
    # A throwaway log, so the exec step's output file (D38) lands beside it and not in scripts\.
    $config = Get-ConsoleConfig -RepoRoot $repo
    $config.LogFile = Join-Path ($marker + '-log') 'shard-console.log'
    New-Item -ItemType Directory -Path ($marker + '-log') -Force | Out-Null
    $write = New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-NoProfile', '-Command', "Set-Content -LiteralPath '$marker' -Value x")
    $typed = @((New-PlanStep -Kind confirm -Phrase 'stop live'), $write)
    $asked = @((New-PlanStep -Kind ask -Text 'Delete it?'), $write)
    try {
        foreach ($given in $null, '', 'Stop live', 'yes', 'stop') {
            Assert-Equal $false (Invoke-Plan -Plan $typed -Config $config -ActionKey 'fact' -ShardKey 'live' -ConfirmLive $given 6>$null) "phrase '$given'"
        }
        Assert-Equal $false (Invoke-Plan -Plan $asked -Config $config -ActionKey 'fact' -ShardKey 'test' 6>$null) 'no -Yes'
        Assert-True (-not (Test-Path -LiteralPath $marker)) 'nothing ran'
        Assert-Equal $true (Invoke-Plan -Plan $typed -Config $config -ActionKey 'fact' -ShardKey 'live' -ConfirmLive 'stop live' 6>$null) 'the exact phrase runs it'
        Assert-True (Test-Path -LiteralPath $marker) 'and it did run'
    } finally {
        Remove-Item -LiteralPath $marker -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath ($marker + '-log') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# D38. An exec step's output used to become Invoke-Plan's return value: the headless entry
# assigned it to $ok and the form threw it away, so a failed rebuild said only "exited with 1".
# The step here is a throwaway script, so neither fact can reach docker.
function New-D38Step {
    param([int]$Exit)
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-d38-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $ps1 = Join-Path $dir 'child.ps1'
    Set-Content -LiteralPath $ps1 -Encoding ASCII -Value @(
        "Write-Output 'd38 out one'",
        "Write-Output 'd38 out two'",
        "[Console]::Error.WriteLine('d38 err line')",
        ('exit ' + $Exit))
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    [pscustomobject]@{
        Dir = $dir
        Config = $cfg
        Step = New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ps1) -Text 'd38 child'
    }
}

Fact 'AnExecStepThatSucceedsLeavesTheExecutorReturningOneTrue' {
    $d = New-D38Step 0
    try {
        $r = @(Invoke-Plan -Plan @($d.Step) -Config $d.Config -ActionKey 'fact' -ShardKey 'test' 6>$null)
        Assert-Equal 1 $r.Count ('Invoke-Plan returned ' + $r.Count + ' objects: ' + ($r -join ' | '))
        Assert-True ($r[0] -is [bool]) ('a ' + $r[0].GetType().FullName)
        Assert-Equal $true $r[0]
    } finally { Remove-Item -LiteralPath $d.Dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'AFailedExecStepShowsItsOutputInAHeadlessRunAndTheResultIsOneFalse' {
    # A real headless process: no form, so Write-ConsoleLine goes to Write-Host, which a
    # redirected powershell.exe writes to stdout. Same path as a window action's child.
    $d = New-D38Step 1
    try {
        $cmd = @(
            "`$ErrorActionPreference = 'Stop'",
            (". '" + (Resolve-Path $script).Path + "' -LoadOnly"),
            ("`$c = Get-ConsoleConfig -RepoRoot '" + $repo + "'"),
            ("`$c.LogFile = '" + $d.Config.LogFile + "'"),
            ("`$s = New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', '" + (Join-Path $d.Dir 'child.ps1') + "') -Text 'd38 child'"),
            "`$ok = Invoke-Plan -Plan @(`$s) -Config `$c -ActionKey 'fact' -ShardKey 'test'",
            "'RESULT count=' + @(`$ok).Count + ' type=' + `$ok.GetType().FullName + ' value=' + `$ok"
        )
        $runner = Join-Path $d.Dir 'headless.ps1'
        Set-Content -LiteralPath $runner -Encoding ASCII -Value $cmd
        $o = Join-Path $d.Dir 'out.txt'
        $e = Join-Path $d.Dir 'err.txt'
        $p = Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $runner + '"') -Wait -NoNewWindow -PassThru -RedirectStandardOutput $o -RedirectStandardError $e
        $out = @(Get-Content -LiteralPath $o) + @(Get-Content -LiteralPath $e)
        $all = ($out -join ' | ') + ' (exit ' + $p.ExitCode + ')'
        foreach ($l in 'd38 out one', 'd38 out two', 'd38 err line') {
            Assert-True (@($out | Where-Object { $_ -match [regex]::Escape($l) }).Count -eq 1) ("'" + $l + "' on screen: " + $all)
        }
        Assert-True (@($out | Where-Object { $_ -match 'FAILED at step 1: .* exited with 1' }).Count -eq 1) ('the failure is named: ' + $all)
        Assert-True (@($out | Where-Object { $_ -eq 'RESULT count=1 type=System.Boolean value=False' }).Count -eq 1) ('$ok is one false: ' + $all)
        # And it can be read after the window is closed.
        $tee = @(Get-ChildItem -LiteralPath (Join-Path $d.Dir 'shard-console-output') -Filter '*.log' -ErrorAction SilentlyContinue)
        Assert-Equal 1 $tee.Count ('one output file beside the log: ' + $all)
        $text = [IO.File]::ReadAllText($tee[0].FullName)
        foreach ($l in 'd38 out one', 'd38 out two', 'd38 err line', 'exited with 1') {
            Assert-True ($text -match [regex]::Escape($l)) ("'" + $l + "' in " + $tee[0].Name + ': ' + $text)
        }
        Assert-True (@($out | Where-Object { $_ -match [regex]::Escape($tee[0].FullName) }).Count -ge 1) ('the failure names the file: ' + $all)
    } finally { Remove-Item -LiteralPath $d.Dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'AnOldSaveIsOnlyStaleOnceTheServerHasHadTimeToSave' {
    # Found by the end-to-end run: right after a restore the save's files carry the snapshot's
    # timestamps, and the panel said NOT SAVING 13 seconds after the start.
    $s = New-ConsoleState -Config $config -Sections $cap
    $lv = $s.Shards['live']
    $lv.Save.Newest.LastWriteUtc = $s.Now.AddDays(-1)
    $lv.Container.StartedAt = $s.Now.AddSeconds(-13)
    $text = (Format-StatusReport $s $config | ForEach-Object { $_.Text }) -join "`n"
    Assert-True (-not ($text -match 'NOT SAVING')) 'just started: no warning'
    $lv.Container.StartedAt = $s.Now.AddHours(-2)
    $text = (Format-StatusReport $s $config | ForEach-Object { $_.Text }) -join "`n"
    Assert-True ($text -match 'NOT SAVING') 'up two hours with a day-old save: warned'
}

# --- the rest ----------------------------------------------------------------------------------

Fact 'TheCapturedLiveComposeFileIsInTheD29State' {
    Assert-Equal $true  $state.Compose.HasBuild
    Assert-Equal $false $state.Compose.HasImage
    Assert-True ($state.Compose.Warning -match 'D29') $state.Compose.Warning
    $t = Test-LiveComposeFile -Text ([IO.File]::ReadAllText((Join-Path $repo 'docker\uo\docker-compose.test.yml'))) -Service 'modernuo-test'
    Assert-Equal $true $t.HasImage 'the test file names its image'
    Assert-Equal $null $t.Warning
}

Fact 'RedactionKeepsTheListenerAndPromptAndDropsAccountNames' {
    $raw = "[16:03:33 WRN] This server has no accounts. <s:Server.Misc.AccountPrompt>`n" +
           "[16:03:33 INF] Do you want to create the owner account now? (y/n): <s:Server.Misc.AccountPrompt>`n" +
           "[19:06:01 INF] Protected accounts registered: someone <s:Server.Misc.ServerAccess>`n" +
           "[19:06:01 INF] Listening: 127.0.0.1:2594 <s:Server.Network.NetState>"
    $r = Remove-AccountLines $raw
    Assert-True ($r -match 'no accounts') 'kept'
    Assert-True ($r -match 'owner account') 'kept'
    Assert-True ($r -match 'Listening') 'kept'
    Assert-True (-not ($r -match 'someone')) 'dropped'
}

# --- world commands (cc-P21) ------------------------------------------------------------------
# The tab lists what server/customizations declares with [ShardCommand], read from the source
# itself. These facts read the real tree, so they fail when the tree and the tab disagree. Literal
# sources are used only for shapes the real tree should never contain (no declaration, a handler
# in another file, a declared dry run its [Usage] does not list).

$custom    = Join-Path $repo 'server\customizations'
$sources   = Get-CommandSourceFiles $custom
$worldCmds = @(ConvertFrom-CommandSources $sources)
$groups    = @(Get-WorldCommandGroups $worldCmds)

function Get-WorldEntry { param([string]$Name) @($worldCmds | Where-Object { $_.Name -ceq $Name })[0] }

Fact 'DiscoveryFindsEveryRegisterCallInTheTree' {
    # Counted without the parser: a line that calls CommandSystem.Register( and is not a comment.
    $lines = 0
    foreach ($k in $sources.Keys) {
        $lines += @(($sources[$k] -split "`n") | Where-Object { $_ -match 'CommandSystem\.Register\(' -and $_ -notmatch '^\s*(//|\*)' }).Count
    }
    Assert-True ($lines -ge 58) "the tree had 58 registrations on 2026-09-30; counted $lines"
    Assert-Equal $lines $worldCmds.Count 'one entry per Register call'
    Assert-Equal 0 @($worldCmds | Where-Object { -not $_.Name }).Count 'every entry has a name'
    Assert-True ($null -ne (Get-WorldEntry 'TCFill')) 'a name held in a const (TestCenterFillCommand.Command) is resolved'
}

Fact 'TheElevenSeedersTheBriefNamesAreAllFoundAndDeclaredWorldSetup' {
    $eleven = 'ClusterFSeedGuildHalls', 'ClusterFSeedNewHaven', 'ClusterFSeedOldHaven', 'ClusterFSeedInstitutions',
              'ClusterFSeedNewHavenServices', 'ClusterFSeedResetStone', 'ClusterFSeedMineCamp', 'ClusterFSouthMineDecor',
              'ClusterFOldHavenCleanup', 'ClusterFMoveMineCampNpcs', 'SeedRoyalCity'
    foreach ($n in $eleven) {
        $e = Get-WorldEntry $n
        Assert-True ($null -ne $e) "$n discovered"
        Assert-Equal 'world-setup' $e.Category $n
        Assert-Equal 'Administrator' $e.Access $n
        Assert-True ([bool]$e.Summary) "$n has a one-line summary"
    }
}

Fact 'ACommandWithNoUsageStillAppears' {
    $src = @{ 'X.cs' = "public static class X {`n  public static void Initialize() { CommandSystem.Register(`"Bare`", AccessLevel.Seer, Bare_OnCommand); }`n  public static void Bare_OnCommand(CommandEventArgs e) { }`n}" }
    $e = @(ConvertFrom-CommandSources $src)
    Assert-Equal 1 $e.Count
    Assert-Equal 'Bare' $e[0].Name
    Assert-Equal '' $e[0].Usage
    Assert-Equal 'unclassified' $e[0].Category
    Assert-Equal '[Bare' $e[0].Command
    # and the real ones: no [Usage] on addclone, SetupDespise, GenerateNewShame, SeedRoyalCity
    foreach ($n in 'addclone', 'SetupDespise', 'GenerateNewShame', 'SeedRoyalCity') {
        $r = Get-WorldEntry $n
        Assert-True ($null -ne $r) "$n discovered"
        Assert-Equal '' $r.Usage $n
    }
}

Fact 'AnUnclassifiedCommandSurfacesInTheNewGroupAndNeverVanishes' {
    $src = @{
        'A.cs' = "  public static void Configure() { CommandSystem.Register(`"Undeclared`", AccessLevel.Administrator, OnA); }`n    [Usage(`"Undeclared [dryrun]`")]`n    private static void OnA(CommandEventArgs e) { }"
        'B.cs' = "  public static void Configure() { CommandSystem.Register(`"Elsewhere`", AccessLevel.GameMaster, HandlerInAnotherFile); }"
    }
    $e = @(ConvertFrom-CommandSources $src)
    Assert-Equal 2 $e.Count
    Assert-Equal 'unclassified,unclassified' ((@($e | Sort-Object Name) | ForEach-Object { $_.Category }) -join ',')
    $g = @(Get-WorldCommandGroups $e)
    $new = @($g | Where-Object { $_.Key -eq 'unclassified' })[0]
    Assert-Equal 'Elsewhere,Undeclared' ((@($new.Items | ForEach-Object { $_.Name }) | Sort-Object) -join ',')
    Assert-True ($new.Title -match 'new, unclassified') $new.Title
    Assert-True (-not $new.CopyAll) 'no Copy-all for commands nobody has classified'
    Assert-True (@($e | Where-Object { $_.Name -eq 'Undeclared' })[0].DryRunCommand -eq '') 'an undeclared command gets no dry run, even when its usage lists one'
}

Fact 'TheRenderedDryRunIsAWordOfThatCommandsOwnUsage' {
    $withDry = @($worldCmds | Where-Object { $_.DryRunCommand })
    # Counted 2026-09-30: nine declare one. A new one is welcome; one of these losing it is not.
    $nine = 'ClusterFMoveMineCampNpcs', 'ClusterFOldHavenCleanup', 'ClusterFSeedGuildHalls', 'ClusterFSeedMineCamp', 'ClusterFSeedNewHaven',
            'ClusterFSeedNewHavenServices', 'ClusterFSeedOldHaven', 'ClusterFSeedResetStone', 'ClusterFSouthMineDecor'
    foreach ($n in $nine) { Assert-True ([bool](Get-WorldEntry $n).DryRunCommand) "$n renders its dry run" }
    foreach ($e in $withDry) {
        Assert-Equal ('[' + $e.Name + ' ' + $e.DryRun) $e.DryRunCommand $e.Name
        $words = @($e.Usage -split '[^A-Za-z0-9_]+')
        Assert-True ($words -ccontains $e.DryRun) ($e.Name + ': "' + $e.DryRun + '" is not a word of "' + $e.Usage + '"')
    }
    Assert-Equal '[ClusterFSeedMineCamp dryrun' (Get-WorldEntry 'ClusterFSeedMineCamp').DryRunCommand
    Assert-Equal '[ClusterFSeedNewHaven dryrun' (Get-WorldEntry 'ClusterFSeedNewHaven').DryRunCommand 'repair moves NPCs; it is not the dry run'
    Assert-Equal '' (Get-WorldEntry 'SetupDespise').DryRunCommand 'declared NoDryRun'
}

Fact 'ADeclaredDryRunThatIsNotInItsUsageRendersNoDryRunAndSaysSo' {
    $src = @{ 'C.cs' = "CommandSystem.Register(`"Liar`", AccessLevel.Administrator, OnC);`n    [Usage(`"Liar [missing]`")]`n    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = `"dryrun`", Summary = `"x`")]`n    private static void OnC(CommandEventArgs e) { }" }
    $e = @(ConvertFrom-CommandSources $src)[0]
    Assert-Equal 'world-setup' $e.Category
    Assert-Equal '' $e.DryRunCommand 'never render a flag the usage does not list'
    Assert-True ((@($e.Problems) -join ' ') -match 'not a word of') (@($e.Problems) -join ' | ')
}

Fact 'PlayerCommandsNeverAppearInTheWorldTab' {
    $shown = @($groups | ForEach-Object { $_.Items } | ForEach-Object { $_.Name } | Where-Object { $_ })
    $players = @($worldCmds | Where-Object { $_.Category -eq 'player' })
    foreach ($p in $players) {
        Assert-True ($shown -notcontains $p.Name) "$($p.Name) is a player command"
    }
    Assert-Equal 'achievements,cleanup,cleanupall,guild,ResetMyAccount,SelfRes,stats,TCFill,version' ((@($players | ForEach-Object { $_.Name }) | Sort-Object) -join ',')
}

Fact 'EveryStepOfTheOrderListIsARegisteredWorldCommandWithAFlagItsUsageLists' {
    foreach ($seq in $script:WorldSetupOrder) {
        Assert-True ([bool]$seq.Source) "$($seq.Key) says where its order came from"
        foreach ($s in @($seq.Steps | Where-Object { -not $_.Stock })) {
            $e = Get-WorldEntry $s.Name
            Assert-True ($null -ne $e) "$($s.Name) in the order list is registered"
            Assert-True ($e.Category -in 'world-setup', 'world-generation') "$($s.Name) is $($e.Category)"
            if ($s.Args) {
                $words = @($e.Usage -split '[^A-Za-z0-9_]+')
                Assert-True ($words -ccontains $s.Args) "$($s.Name) $($s.Args): not a word of `"$($e.Usage)`""
            }
        }
    }
}

Fact 'TheFreshWorldGroupIsTheTwelveSpawnFilesThenShameThenDespiseThenSave' {
    $fresh = @($groups | Where-Object { $_.Key -eq 'fresh' })[0]
    $c = @($fresh.Items)
    $spawn = @($c | Where-Object { $_.Command -like '`[GenerateSpawners *' })
    Assert-Equal 12 $spawn.Count 'reachability-audit section 5 lists twelve paths; the brief said ten'
    $pinned = 'D:\UO\ModernUO-pinned\Distribution'
    if (Test-Path $pinned) {
        foreach ($s in $spawn) {
            $rel = $s.Command.Substring('[GenerateSpawners '.Length)
            Assert-True (Test-Path (Join-Path $pinned $rel)) "$rel exists in pinned Distribution"
        }
    }
    $rest = @($c | Select-Object -Skip 12 | ForEach-Object { $_.Command })
    Assert-Equal '[GenerateNewShame,[SetupDespise,[save' ($rest -join ',')
    Assert-True $fresh.CopyAll 'the fresh-world group copies as a whole'
}

Fact 'CopyGroupIsEveryCommandOfTheGroupOneLineEachInOrder' {
    $copyable = @($groups | Where-Object { $_.CopyAll })
    Assert-True ($copyable.Count -ge 3) 'fresh world, New Haven and the mine camp'
    foreach ($g in $copyable) {
        Assert-Equal ((@($g.Items | ForEach-Object { $_.Command }) -join "`r`n") + "`r`n") (Get-GroupClipboardText $g) $g.Key
    }
    $mine = @($groups | Where-Object { $_.Key -eq 'minecamp' })[0]
    Assert-Equal '[ClusterFSeedMineCamp dryrun,[ClusterFSeedMineCamp,[ClusterFMoveMineCampNpcs dryrun,[ClusterFMoveMineCampNpcs' ((@($mine.Items) | ForEach-Object { $_.Command }) -join ',')
}

Fact 'EveryDiscoveredWorldCommandIsInTheTabAndOnlyTheUnorderedOnesOnce' {
    $world = @($worldCmds | Where-Object { $_.Category -in 'world-generation', 'world-setup', 'world-removal', 'unclassified' })
    $shown = @($groups | ForEach-Object { $_.Items } | ForEach-Object { $_.Name } | Where-Object { $_ })
    foreach ($w in $world) {
        Assert-True ($shown -contains $w.Name) "$($w.Name) is in the tab"
    }
    $unordered = @($groups | Where-Object { -not $_.CopyAll } | ForEach-Object { $_.Items } | ForEach-Object { $_.Command })
    Assert-Equal @($unordered).Count @($unordered | Select-Object -Unique).Count 'an unordered command, and its dry run, are each listed once'
    $noOrder = @($groups | Where-Object { $_.Key -eq 'noorder' })[0]
    Assert-True ($noOrder.Title -match 'no order') $noOrder.Title
}

Fact 'EveryRowIsOneCommandWithOneCopyAndADryRunIsItsOwnRowJustAbove' {
    # One shape in every group: a row is exactly what Copy puts on the clipboard. No row carries a
    # second command, and a command with a dry run has the dry run as the row directly above it,
    # the way the runbooks and Copy group already list them.
    foreach ($g in $groups) {
        $items = @($g.Items)
        foreach ($i in $items) {
            Assert-True (-not ($i.PSObject.Properties.Name -contains 'DryRunCommand') -or -not $i.DryRunCommand) "$($g.Key): $($i.Command) carries a second command"
        }
        for ($k = 0; $k -lt $items.Count; $k++) {
            if (-not $items[$k].Name) { continue }
            $e = Get-WorldEntry $items[$k].Name
            if ($null -eq $e -or -not $e.DryRunCommand) { continue }
            if ($items[$k].Command -ceq $e.Command) {
                Assert-True ($k -gt 0 -and $items[$k - 1].Command -ceq $e.DryRunCommand) "$($g.Key): $($e.Command) has its dry run on the row above"
            }
        }
    }
    $noOrder = @($groups | Where-Object { $_.Key -eq 'noorder' })[0]
    $cmds = @($noOrder.Items | ForEach-Object { $_.Command })
    Assert-True ($cmds -ccontains '[ClusterFOldHavenCleanup dryrun') ($cmds -join ' | ')
}

Fact 'ATestOnlyCommandSaysSoInItsLine' {
    $e = Get-WorldEntry 'ClusterFSeedResetStone'
    Assert-Equal 'test only' $e.ShardText
    Assert-True ((Format-WorldCommandLine $e) -cmatch '^TEST SHARD ONLY') (Format-WorldCommandLine $e)
}

Fact 'RedMeansTestOnlyUnclassifiedOrAPlainRunThatDeletesAndNothingElse' {
    $red = @($groups | ForEach-Object { $_.Items } | Where-Object { $_.Warn } | ForEach-Object { $_.Command }) | Sort-Object -Unique
    Assert-Equal '[ClearRoyalCityVendors,[ClusterFDespiseStockCleanup,[ClusterFOldHavenCleanup,[ClusterFSeedResetStone,[ClusterFSeedResetStone dryrun,[DeleteDespise,[DeleteShame,[SeedRoyalCity' ($red -join ',') 'deletes nothing must not be red, and a dry run deletes nothing'
}

Fact 'TheScriptAndItsFactsAreAsciiWithNoBom' {
    foreach ($f in $script, $MyInvocation.MyCommand.Path, $PSCommandPath) {
        if (-not $f) { continue }
        $b = [IO.File]::ReadAllBytes($f)
        Assert-True (-not ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)) "$f has a BOM"
        $hi = @($b | Where-Object { $_ -gt 0x7E -or ($_ -lt 0x20 -and $_ -notin 9, 10, 13) })
        Assert-Equal 0 $hi.Count "$f has non-ASCII bytes"
    }
}

# --- at a glance and the status publisher -----------------------------------------------------
# status.json is this project's own contract (docker/uo-status/README.md), so these use literals
# for it. The docker side stays the real capture.

function New-PublisherSections {
    param([string]$Json, [string]$Meta = '2026-09-29T17:00:00Z|300', [string]$Http = '200', [switch]$NoFeed, [switch]$Missing)
    $s = [ordered]@{}
    foreach ($k in $cap.Keys) { $s[$k] = $cap[$k] }
    if (-not $NoFeed) { $s['inspect sl-uo-status'] = $cap['inspect sl-modernuo'] }
    if ($Missing) { $s['status meta'] = 'missing'; $s['status file'] = '' } else { $s['status meta'] = $Meta; $s['status file'] = $Json }
    $s['feed http'] = $Http
    $s
}
function New-StatusJson {
    param([datetime]$GeneratedUtc, [int]$Count = 2)
    '{"schema":1,"generatedAt":"' + (Format-DockerTime $GeneratedUtc) + '","shard":{"name":"Shattered Legacy","startedAt":"2026-09-28T19:07:18Z","uptimeSeconds":1},"players":{"count":' + $Count + ',"names":["Cain","Bram"]},"world":{"lastSaveAt":"2026-09-29T16:55:00Z"}}'
}

Fact 'ACaptureFromBeforeThePublisherCheckSaysUnknownNotOffline' {
    # The 2026-09-29 capture has no status sections. Saying NOT PUBLISHING there would be a guess.
    Assert-Equal 'UNKNOWN' $state.Publisher.State
    Assert-Equal 'UNKNOWN' $state.Publisher.FeedState
}

Fact 'TheGlanceShowsLiveRunningAndTestStoppedFromTheCapture' {
    $t = @(Get-AtAGlance $state $config)
    Assert-Equal 'LIVE,TEST,STATUS PUBLISHER' (($t | ForEach-Object { $_.Title }) -join ',')
    Assert-Equal 'STOPPED' $t[1].Word 'the test container is exited in the capture'
    Assert-True ($t[0].Word -in @('RUNNING', 'STARTING?')) ('live is running in the capture, got ' + $t[0].Word)
    Assert-True ($t[0].Detail -match 'OLD IMAGE') 'live runs a drifted image in the capture (D25), and the tile says so'
    Assert-True ($t[0].Color -ne 'green') 'a drifted live shard is not shown all-green'
}

Fact 'NoStatusFileAndNoMountSaysNotPublishingAndWhy' {
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections -Missing -Http '503')
    Assert-Equal 'NOT PUBLISHING' $s.Publisher.State
    Assert-Equal 0 @($s.Publisher.Writers).Count 'neither captured container has the status folder mounted'
    Assert-True ($s.Publisher.Detail -match 'mounted') 'it says the mount is what is missing'
    Assert-Equal 'NOT SERVING' $s.Publisher.FeedState
}

Fact 'AFreshStatusFileIsPublishingWithThePlayerCount' {
    $json = New-StatusJson $now.AddSeconds(-20) 2
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections $json)
    Assert-Equal 'PUBLISHING' $s.Publisher.State
    Assert-Equal 2 $s.Publisher.Players
    Assert-Equal 'SERVING' $s.Publisher.FeedState
    $tile = @(Get-AtAGlance $s $config)[2]
    Assert-True ($tile.Detail -match '^2 playing') $tile.Detail
}

Fact 'AStatusFileOlderThanThreeMinutesIsStale' {
    # The README's one liveness rule. The file never says it is offline; its age does.
    $json = New-StatusJson $now.AddMinutes(-4)
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections $json)
    Assert-Equal 'STALE' $s.Publisher.State
    Assert-Equal 'red' $s.Publisher.Color
}

Fact 'AHalfWrittenStatusFileIsUnreadableNotPublishing' {
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections '{"schema":1,"generatedAt":')
    Assert-Equal 'UNREADABLE' $s.Publisher.State
}

Fact 'PublishingWithTheFeedStoppedIsAmberBecauseTheWebsiteCannotSeeIt' {
    $json = New-StatusJson $now.AddSeconds(-20)
    $s2 = New-ConsoleState -Config $config -Sections (New-PublisherSections $json -Http '200')
    Assert-Equal 'green' $s2.Publisher.Color
    $sec = New-PublisherSections $json
    $sec['inspect sl-uo-status'] = $cap['inspect sl-modernuo-test']
    $s3 = New-ConsoleState -Config $config -Sections $sec
    Assert-Equal 'STOPPED' $s3.Publisher.FeedState
    Assert-Equal 'amber' $s3.Publisher.Color
}

# cc-P42 Part E (cc-P37 section 7.3). A container's mounts as Docker reports them: both the destinations the old check
# read and the sources the new one follows.
function Set-Mounts {
    param($Container, [object[]]$Pairs)
    $Container.Mounts = @($Pairs | ForEach-Object { [pscustomobject]@{ Source = $_[0]; Destination = $_[1] } })
    $Container.MountTargets = @($Pairs | ForEach-Object { $_[1] })
}
$statusDir = $config.Publisher.Folder -replace '\\', '/'
$libDir    = Split-Path $config.Publisher.Folder -Parent

Fact 'OnlyTheTestShardMountingTheStatusFolderIsAWarning' {
    # The website would show the test shard's players as if they were live.
    $json = New-StatusJson $now.AddSeconds(-20)
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections $json)
    Set-Mounts $s.Shards['test'].Container @(, @($statusDir, '/var/lib/uo/modernuo/status'))
    $p = Get-PublisherState -Config $config -Sections (New-PublisherSections $json) -Shards $s.Shards -Now $now
    Assert-Equal 'test' (@($p.Writers) -join ',')
    Assert-Equal 'amber' $p.Color
    Assert-True (@($p.Lines | Where-Object { $_ -like 'WARNING*TEST*' }).Count -eq 1) 'the warning names the test shard'
}

Fact 'LiveMountingTheParentFolderIsTheStatusWriter' {
    # cc-P37's live mount, ${SERVER_PATH}/lib/uo/modernuo:/var/lib/uo/modernuo, and the test shard's own parent,
    # modernuo-test, at the same destination. Only live's source leads to the folder sl-uo-status serves.
    $json = New-StatusJson $now.AddSeconds(-20)
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections $json)
    Set-Mounts $s.Shards['live'].Container @(, @(($libDir -replace '\\', '/'), '/var/lib/uo/modernuo'))
    Set-Mounts $s.Shards['test'].Container @(, @((($libDir + '-test') -replace '\\', '/'), '/var/lib/uo/modernuo'))
    $p = Get-PublisherState -Config $config -Sections (New-PublisherSections $json) -Shards $s.Shards -Now $now
    Assert-Equal 'live' (@($p.Writers) -join ',')
    Assert-Equal 'green' $p.Color
    Assert-True ($p.Lines[0] -like 'written by : LIVE (sl-modernuo)*') $p.Lines[0]
    Assert-Equal 0 @($p.Lines | Where-Object { $_ -like 'WARNING*' }).Count 'no test-only warning'
}

Fact 'TheTestShardsOwnParentFolderIsNotAStatusWriter' {
    # The test shard writes modernuo-test\status, which nothing serves: with live down to no mount, nobody writes.
    $json = New-StatusJson $now.AddSeconds(-20)
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections $json)
    Set-Mounts $s.Shards['live'].Container @()
    Set-Mounts $s.Shards['test'].Container @(, @((($libDir + '-test') -replace '\\', '/'), '/var/lib/uo/modernuo'))
    $p = Get-PublisherState -Config $config -Sections (New-PublisherSections $json) -Shards $s.Shards -Now $now
    Assert-Equal '' (@($p.Writers) -join ',')
    Assert-True ($p.Lines[0] -like 'written by : none*') $p.Lines[0]
}

Fact 'AWslFormSourceMapsToTheSameFolder' {
    # Docker Desktop can report a bind source as /run/desktop/mnt/host/<drive>/...; it is the same folder.
    $wsl = '/run/desktop/mnt/host/' + $libDir.Substring(0, 1).ToLower() + ($libDir.Substring(2) -replace '\\', '/')
    $json = New-StatusJson $now.AddSeconds(-20)
    $s = New-ConsoleState -Config $config -Sections (New-PublisherSections $json)
    Set-Mounts $s.Shards['live'].Container @(, @($wsl, '/var/lib/uo/modernuo/'))
    Set-Mounts $s.Shards['test'].Container @()
    $p = Get-PublisherState -Config $config -Sections (New-PublisherSections $json) -Shards $s.Shards -Now $now
    Assert-Equal 'live' (@($p.Writers) -join ',') $wsl
}

Fact 'TheReportHasAGlanceLineForEachTileAndAPublisherSection' {
    $text = (Format-StatusReport $state $config | ForEach-Object { $_.Text }) -join "`n"
    Assert-True ($text -match '(?m)^LIVE\s+:') 'a LIVE glance line'
    Assert-True ($text -match '(?m)^TEST\s+:') 'a TEST glance line'
    Assert-True ($text -match '(?m)^STATUS PUBLISHER\s*:') 'a publisher glance line'
    Assert-True ($text -match '== Status publisher') 'a publisher section'
}

Fact 'TheScriptParsesUnderWindowsPowerShell51' {
    Assert-Equal 5 $PSVersionTable.PSVersion.Major 'these facts must run under the shell Chase runs'
    $errs = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path $script).Path, [ref]$null, [ref]$errs)
    Assert-Equal 0 @($errs).Count (@($errs) -join '; ')
}

Fact 'TheFormBuildsFromACaptureWithoutBeingShown' {
    # Construction only: every control, every handler attached, nothing shown or clicked.
    $script:NetProbeDisabled = $true # cc-P52: the PUBLIC DNS tile's check would reach the network
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        $tabs = $script:ui.Tabs.TabPages | ForEach-Object { $_.Text }
        Assert-Equal 'Test shard,Snapshots,World setup,Diagnostics,LIVE shard' ($tabs -join ',')
        Assert-True ($script:ui.Status.Text -match 'DRIFTED') 'the status panel rendered the capture'
        Assert-Equal 3 @($script:ui.Tiles).Count 'three tiles: LIVE, TEST, STATUS PUBLISHER'
        Assert-Equal 'TEST: STOPPED' $script:ui.Tiles[1].Word.Text
        Assert-True ($form.Text -match 'LIVE .*\| TEST stopped \| publisher unknown') ('the title carries the glance: ' + $form.Text)
        Assert-True ($null -ne $script:ui.NetTile) 'and the fourth, PUBLIC DNS (cc-P52), drawn apart from them'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

# --- D37: the mode a click hands the window it opens ---------------------------------------------
# On 2026-09-29 a -DryRun console stopped live and restored a snapshot for real (bug-list D37): the
# child window's argument line carried no mode. The section 6 check drove a button that runs in the
# console itself, which never builds that line. These drive Invoke-ConsoleButton, the function a
# click calls, for every action that opens a window. The dialogs are answered yes, the state is
# given, and Start-Process and Invoke-Plan are recorders, so nothing launches and nothing reaches
# docker. What it would have handed Start-Process is the thing under test.

$snapName = '20260929-101500_before-gargoyle-fix'

# The capture has live running, so live.start refuses on it and opens nothing. This copy has live
# stopped, so live.start builds its window too.
$liveStoppedState = New-StateWithSnapshots @($snapName)
$liveStoppedState.Shards['live'].Container.Running = $false
$liveStoppedState.Shards['live'].Container.Status = 'exited'

function Invoke-ClickCapture {
    # FromCapture defaults to Dry: a console launched with -DryRun. Dry with FromCapture false is
    # the toggle in a console whose status is live from docker.
    param([string]$ActionKey, [string]$ShardKey, [bool]$Dry, $State = $snapState, $FromCapture = $null)
    if ($null -eq $FromCapture) { $FromCapture = $Dry }
    $rec = [pscustomobject]@{
        Spawned = New-Object 'System.Collections.Generic.List[string]'
        Lines = New-Object 'System.Collections.Generic.List[string]'
        Executed = New-Object 'System.Collections.Generic.List[string]'
    }
    # Defined here, so they shadow the real ones for Invoke-ConsoleButton only (dynamic scope).
    function Start-Process { param($FilePath, $ArgumentList) $rec.Spawned.Add([string]$ArgumentList) }
    function Invoke-Plan { $rec.Executed.Add('ran'); $true }
    function Show-TypedConfirm { $true }
    function Show-YesNo { 'Yes' }
    function Get-ConsoleState { $State }
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $rec.Lines.Add($Text) }
    $saved = @($script:dry, $script:cfg, $script:statusFrom, $script:SelfPath, $script:fromCapture)
    $script:dry = $Dry; $script:cfg = $config; $script:statusFrom = $null; $script:fromCapture = [bool]$FromCapture
    $script:SelfPath = (Resolve-Path $script).Path
    $sn = $null; $s = $null
    if ($ActionKey -eq 'snapshot.create') { $sn = 'd37-check' }
    if ($ActionKey -in @('snapshot.restore', 'snapshot.delete')) { $s = $snapName }
    try { Invoke-ConsoleButton -ActionKey $ActionKey -ShardKey $ShardKey -SnapshotName $sn -Snapshot $s }
    finally { $script:dry, $script:cfg, $script:statusFrom, $script:SelfPath, $script:fromCapture = $saved }
    $rec
}

# Every window action on every shard it can act on.
$windowCases = @(foreach ($a in @(Get-ConsoleActions | Where-Object { $_.RunIn -eq 'window' })) {
    $shards = @('test')
    if ($a.Key -like 'live.*') { $shards = @('live') }
    if ($a.Key -like 'snapshot.*') { $shards = @('test', 'live') }
    foreach ($sk in $shards) {
        $st = $snapState
        if ($a.Key -eq 'live.start') { $st = $liveStoppedState }
        [pscustomobject]@{ Key = $a.Key; Shard = $sk; State = $st }
    }
})

Fact 'EveryWindowActionClickedDryHandsItsWindowModeDryRun' {
    Assert-True ($windowCases.Count -ge 13) ('found ' + $windowCases.Count + ' window cases')
    foreach ($c in $windowCases) {
        $r = Invoke-ClickCapture $c.Key $c.Shard $true $c.State
        $what = $c.Key + ' ' + $c.Shard
        Assert-Equal 0 @($r.Lines | Where-Object { $_ -match ' failed: ' }).Count ($what + ': ' + ($r.Lines -join ' | '))
        Assert-Equal 1 $r.Spawned.Count ($what + ' opens one window')
        Assert-Equal 0 $r.Executed.Count ($what + ' runs nothing in the console')
        Assert-True ($r.Spawned[0] -match ' -Mode DryRun ') ($what + ': ' + $r.Spawned[0])
        Assert-True (-not ($r.Spawned[0] -match 'Execute')) ($what + ': ' + $r.Spawned[0])
    }
}

Fact 'TheThreeActionsThatHitD37CarryTheModeAndTheirOwnArguments' {
    $stop = (Invoke-ClickCapture 'live.stop' 'live' $true).Spawned[0]
    Assert-True ($stop -match '-Mode DryRun -Action live\.stop -Shard live') $stop
    Assert-True ($stop -match '-ConfirmLive "stop live"') $stop
    $restore = (Invoke-ClickCapture 'snapshot.restore' 'test' $true).Spawned[0]
    Assert-True ($restore -match ('-Mode DryRun -Action snapshot\.restore -Shard test -Snapshot ' + $snapName)) $restore
    $delete = (Invoke-ClickCapture 'snapshot.delete' 'test' $true).Spawned[0]
    Assert-True ($delete -match ('-Mode DryRun -Action snapshot\.delete -Shard test -Snapshot ' + $snapName + ' -Yes')) $delete
}

Fact 'EveryWindowActionClickedForRealHandsItsWindowModeExecute' {
    foreach ($c in $windowCases) {
        $r = Invoke-ClickCapture $c.Key $c.Shard $false $c.State
        $what = $c.Key + ' ' + $c.Shard
        Assert-Equal 1 $r.Spawned.Count ($what + ': ' + ($r.Lines -join ' | '))
        Assert-True ($r.Spawned[0] -match ' -Mode Execute ') ($what + ': ' + $r.Spawned[0])
        Assert-True (-not ($r.Spawned[0] -match 'DryRun')) ($what + ': ' + $r.Spawned[0])
    }
}

# --- the DRY RUN toggle -----------------------------------------------------------------------------
# Launched without -DryRun the status is docker, and the box by Refresh makes the buttons print.

Fact 'WithTheToggleOnEveryWindowActionIsDryAndItsChildReadsDocker' {
    foreach ($c in $windowCases) {
        $r = Invoke-ClickCapture $c.Key $c.Shard $true $c.State $false
        $what = $c.Key + ' ' + $c.Shard
        Assert-Equal 1 $r.Spawned.Count ($what + ': ' + ($r.Lines -join ' | '))
        Assert-Equal 0 $r.Executed.Count $what
        Assert-True ($r.Spawned[0] -match ' -Mode DryRun ') ($what + ': ' + $r.Spawned[0])
        Assert-True ($r.Spawned[0] -match ' -StatusFromDocker') ($what + ' reads the same docker state as the parent: ' + $r.Spawned[0])
    }
    $real = (Invoke-ClickCapture 'live.stop' 'live' $false).Spawned[0]
    Assert-True (-not ($real -match 'StatusFromDocker')) ('only a dry child is told where to read: ' + $real)
}

Fact 'AConsoleWhoseStatusIsACaptureCannotRunAnythingForReal' {
    # The box is locked in that case; this is the check behind the lock.
    foreach ($c in $windowCases) {
        $r = Invoke-ClickCapture $c.Key $c.Shard $false $c.State $true
        Assert-Equal 0 $r.Spawned.Count $c.Key
        Assert-Equal 0 $r.Executed.Count $c.Key
        Assert-True (@($r.Lines | Where-Object { $_ -match 'capture file' }).Count -eq 1) ($c.Key + ': ' + ($r.Lines -join ' | '))
    }
}

Fact 'TheToggleFlipsTheButtonsAndTheTitleAndNotTheStatusSource' {
    function Get-ConsoleState { param($Config, [switch]$DryRun, $StatusFrom) $script:stateAskedDry.Add([bool]$DryRun); $state }
    $script:stateAskedDry = New-Object 'System.Collections.Generic.List[bool]'
    $form = New-ConsoleForm -Config $config
    try {
        $box = $script:ui.DryBox
        Assert-True ($box.Enabled -and -not $box.Checked) 'off and usable when the status is docker'
        Assert-Equal $false $script:dry
        Assert-True (-not ($form.Text -match 'DRY RUN')) $form.Text
        $box.Checked = $true
        Assert-Equal $true $script:dry 'ticked'
        Assert-True ($form.Text -like 'Shard Console - DRY RUN - *') $form.Text
        Update-ConsoleStatus
        $box.Checked = $false
        Assert-Equal $false $script:dry 'unticked'
        Assert-True (-not ($form.Text -match 'DRY RUN')) $form.Text
        Assert-True ($script:stateAskedDry.Count -ge 2) 'the status was read'
        Assert-Equal 0 @($script:stateAskedDry | Where-Object { $_ }).Count 'the status stayed on docker while ticked'
    } finally { $form.Dispose() }
}

Fact 'LaunchedWithDryRunTheToggleIsLockedOn' {
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        $box = $script:ui.DryBox
        Assert-True ($box.Checked -and -not $box.Enabled) 'ticked and greyed out'
        Set-ConsoleDryRun $false
        Assert-Equal $true $script:dry 'turning it off from code does not take either'
        Assert-True $box.Checked 'still ticked'
    } finally { $form.Dispose() }
}

Fact 'AHereActionClickedDryOpensNothingAndRunsNothing' {
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.RunIn -eq 'here' })) {
        $r = Invoke-ClickCapture $a.Key 'test' $true
        Assert-Equal 0 $r.Spawned.Count $a.Key
        Assert-Equal 0 $r.Executed.Count $a.Key
    }
}

Fact 'AChildGivenNoModeRefusesEveryActionThatChangesSomething' {
    foreach ($a in Get-ConsoleActions) {
        $want = 'execute'
        if ($a.Mutates) { $want = 'refuse' }
        Assert-Equal $want (Resolve-RunMode -Action $a.Key) $a.Key
    }
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'no.such.action') 'an unknown action is not assumed harmless'
}

Fact 'AnySignOfADryRunWins' {
    Assert-Equal 'dry'     (Resolve-RunMode -Action 'live.stop' -Mode DryRun)
    Assert-Equal 'dry'     (Resolve-RunMode -Action 'live.stop' -DryRun)
    Assert-Equal 'dry'     (Resolve-RunMode -Action 'live.stop' -Mode Execute -DryRun)
    Assert-Equal 'execute' (Resolve-RunMode -Action 'live.stop' -Mode Execute)
}

Fact 'TheBannerLeadsWithTheMode' {
    $d = Get-ModeBanner 'dry' 'live.stop' 'live'
    Assert-True ($d.Lines[1] -match '^  DRY RUN  live\.stop \(live\)') $d.Lines[1]
    Assert-True ($d.Title -like 'DRY RUN *') $d.Title
    Assert-True ((Get-ModeBanner 'execute' 'live.stop' 'live').Lines[1] -match 'RUNNING FOR REAL') 'execute'
    Assert-True ((Get-ModeBanner 'refuse' 'live.stop' 'live').Lines[1] -match 'REFUSED') 'refuse'
}

Fact 'APlanThatStopsAShardSaysItDoesNotSaveBeforeThePhraseIsTyped' {
    # D36. The warning used to be inside the stop step, below the plan's first lines.
    foreach ($k in 'live.stop', 'live.restart') {
        Assert-True ((Get-ConfirmPreface (Get-Plan $k 'live')) -like 'BEFORE YOU TYPE: A stop does not save*') $k
    }
    Assert-True ((Get-ConfirmPreface (Get-Plan 'snapshot.restore' 'live' -Snapshot $snapName -State $snapState)) -like 'BEFORE YOU TYPE:*') 'restore stops live first'
    Assert-Equal '' (Get-ConfirmPreface (Get-Plan 'live.start' 'live' -State $liveStoppedState)) 'a start stops nothing'
}

Fact 'AShardWhoseLogSaysAStopSavesGetsThatWordingAndNoWarningFirst' {
    # D36 fixed. server/customizations/Misc/SaveOnShutdown.cs prints this line at start, so the
    # running process itself says whether a stop saves. Only the live log gets it here.
    $sec = @{}
    foreach ($k in $cap.Keys) { $sec[$k] = $cap[$k] }
    $sec['logs sl-modernuo'] = $cap['logs sl-modernuo'] + "`r`n[SaveOnShutdown] listening for SIGTERM: a stop saves the world first (D36)`r`n"
    $fixed = New-ConsoleState -Config $config -Sections $sec
    Assert-True ($fixed.Shards['live'].SavesOnStop -eq $true) 'live says it saves'
    Assert-True ($fixed.Shards['test'].SavesOnStop -eq $false) 'test does not'
    Assert-True ($state.Shards['live'].SavesOnStop -eq $false) 'the captured logs predate the fix'
    foreach ($k in 'live.stop', 'live.restart') {
        $p = Get-Plan $k 'live' -State $fixed
        $text = (@($p | ForEach-Object { $_.Text }) -join ' ')
        Assert-True ($text -match 'saves on stop') ($k + ': ' + $text)
        Assert-True (-not $text.Contains($script:NoSaveWarning)) ($k + ' still warns')
        Assert-Equal '' (Get-ConfirmPreface $p) $k
    }
    $t = (@(Get-Plan 'test.stop' 'test' -State $fixed | ForEach-Object { $_.Text }) -join ' ')
    Assert-True ($t.Contains($script:NoSaveWarning)) ('test keeps the warning: ' + $t)
}

# --- D37 end to end: the real child process, with a docker that only records --------------------
# The facts above stop at the argument line. These run powershell.exe on it, the way Start-Process
# would, with a docker.exe first on PATH that writes its arguments to a file and exits 1. It is
# compiled rather than a .cmd because Invoke-DockerRead starts 'docker' through ProcessStartInfo,
# which only finds .exe. Both resolution paths are checked to reach the shim before any child runs,
# so a broken refusal would stop a recorder, not the live shard.

$shimDir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-docker-shim-' + $PID)
$shimLog = Join-Path $shimDir 'calls.txt'
New-Item -ItemType Directory -Path $shimDir -Force | Out-Null
Add-Type -OutputAssembly (Join-Path $shimDir 'docker.exe') -OutputType ConsoleApplication -TypeDefinition @'
public static class DockerShim {
    public static int Main(string[] a) {
        System.IO.File.AppendAllText(System.Environment.GetEnvironmentVariable("SL_DOCKER_SHIM_LOG"), string.Join(" ", a) + "\n");
        return 1;
    }
}
'@

function Invoke-Child {
    param([string]$ArgLine)
    $oldPath = $env:PATH
    $env:PATH = $shimDir + ';' + $env:PATH
    $env:SL_DOCKER_SHIM_LOG = $shimLog
    try {
        $ps = (Get-Command docker -CommandType Application | Select-Object -First 1).Source
        $pr = @(where.exe docker)[0]
        if ($ps -ne (Join-Path $shimDir 'docker.exe') -or $pr -ne (Join-Path $shimDir 'docker.exe')) {
            throw ('the docker shim is not first on PATH (' + $ps + ', ' + $pr + '); not launching a child')
        }
        Remove-Item -LiteralPath $shimLog -ErrorAction SilentlyContinue
        $out = Join-Path $shimDir 'out.txt'
        $err = Join-Path $shimDir 'err.txt'
        $p = Start-Process -FilePath 'powershell.exe' -ArgumentList ($ArgLine -replace ' -NoExit', '') -Wait -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
        $calls = @()
        if (Test-Path -LiteralPath $shimLog) { $calls = @(Get-Content -LiteralPath $shimLog) }
        [pscustomobject]@{
            Exit = $p.ExitCode
            Out = @(Get-Content -LiteralPath $out | Where-Object { $_.Trim() })
            Err = [IO.File]::ReadAllText($err)
            DockerCalls = $calls
        }
    } finally {
        $env:PATH = $oldPath
        Remove-Item Env:\SL_DOCKER_SHIM_LOG -ErrorAction SilentlyContinue
    }
}

$d37Cases = @(
    [pscustomobject]@{ Key = 'live.stop'; Shard = 'live' },
    [pscustomobject]@{ Key = 'snapshot.restore'; Shard = 'live' },
    [pscustomobject]@{ Key = 'snapshot.delete'; Shard = 'live' }
)

Fact 'TheRealChildGivenTheOldArgumentLineRefusesAndCallsNoDocker' {
    # The line the parent built before this fix: everything but a mode.
    foreach ($c in $d37Cases) {
        $line = (Invoke-ClickCapture $c.Key $c.Shard $false).Spawned[0] -replace ' -Mode Execute', ''
        Assert-True (-not ($line -match '-Mode')) $line
        $r = Invoke-Child $line
        Assert-Equal 0 $r.DockerCalls.Count ($c.Key + ' called docker: ' + ($r.DockerCalls -join ' | '))
        Assert-Equal 2 $r.Exit ($c.Key + ': ' + ($r.Out -join ' | ') + $r.Err)
        Assert-True ($r.Out[1] -match 'REFUSED') ($c.Key + ': ' + ($r.Out -join ' | '))
    }
}

Fact 'TheRealChildOfADryClickSaysDryRunFirstAndCallsNoDocker' {
    foreach ($c in $d37Cases) {
        $line = (Invoke-ClickCapture $c.Key $c.Shard $true).Spawned[0]
        $r = Invoke-Child $line
        Assert-Equal 0 $r.DockerCalls.Count ($c.Key + ' called docker: ' + ($r.DockerCalls -join ' | '))
        Assert-Equal 0 $r.Exit ($c.Key + ': ' + ($r.Out -join ' | ') + $r.Err)
        Assert-True ($r.Out[1] -match ('^  DRY RUN  ' + [regex]::Escape($c.Key))) ($c.Key + ' first line: ' + $r.Out[1])
        Assert-True (@($r.Out | Where-Object { $_ -like ('== DRY RUN: ' + $c.Key + '*') }).Count -eq 1) ($c.Key + ' printed its plan')
    }
}

Fact 'TheRealChildOfAToggledClickReadsDockerAndChangesNothing' {
    # A dry child told -StatusFromDocker reads docker the way the status panel does. The recorder
    # sees every call; none may be a verb that changes anything.
    $line = (Invoke-ClickCapture 'live.stop' 'live' $true $snapState $false).Spawned[0]
    $r = Invoke-Child $line
    Assert-Equal 0 $r.Exit (($r.Out -join ' | ') + $r.Err)
    Assert-True ($r.Out[1] -match '^  DRY RUN  live\.stop') $r.Out[1]
    Assert-True ($r.DockerCalls.Count -gt 0) 'it did read docker'
    $bad = @($r.DockerCalls | Where-Object { $_ -match '^(stop|start|restart|rm|kill|compose|exec|commit|tag|run|create|pause)\b' })
    Assert-Equal 0 $bad.Count ($bad -join ' | ')
}

# --- cc-P52 Part H (bug-list D47): the PUBLIC DNS tile -------------------------------------------
# Get-NetworkProbe with the route, the adapter list and the two HTTPS answers handed in, so these
# reach no network. The addresses are the real ones from D47: the house 216.247.206.76, and the VPN
# exits ddns published, 146.70.217.106 (2026-10-02) and 159.26.100.68 (2026-10-04).

$houseIp = '216.247.206.76'

function New-Doh([string[]]$Ips) {
    $answers = @($Ips | ForEach-Object { @{ name = 'shatteredlegacyuo.com'; type = 1; TTL = 300; data = $_ } })
    @{ Status = 0; Answer = $answers } | ConvertTo-Json -Depth 5
}

function New-Http([string[]]$DnsIps, [string]$PublicIp, [switch]$DnsFails, [switch]$TraceFails) {
    $doh = New-Doh $DnsIps
    $trace = "fl=1\nh=cloudflare.com\nip=$PublicIp\nts=1759600000.0\nvisit_scheme=https\n" -replace '\\n', "`n"
    { param($Url, $Timeout)
        if ($Url -like '*cdn-cgi/trace') { if ($TraceFails) { throw 'The operation has timed out.' }; return $trace }
        if ($DnsFails) { throw 'The remote name could not be resolved' }
        return $doh
    }.GetNewClosure()
}

$lanRoute = { @([pscustomobject]@{ IPAddress = '192.168.1.58'; InterfaceAlias = 'Ethernet' }, [pscustomobject]@{ DestinationPrefix = '0.0.0.0/0'; InterfaceAlias = 'Ethernet' }) }
$vpnRoute = { @([pscustomobject]@{ IPAddress = '10.2.0.2'; InterfaceAlias = 'ProtonVPN' }, [pscustomobject]@{ DestinationPrefix = '0.0.0.0/1'; InterfaceAlias = 'ProtonVPN' }) }
$noVpnAdapters = { @(
    [pscustomobject]@{ Name = 'Ethernet'; Description = 'Realtek Gaming 2.5GbE Family Controller'; Type = 'Ethernet'; Up = $true },
    [pscustomobject]@{ Name = 'vEthernet (WSL (Hyper-V firewall))'; Description = 'Hyper-V Virtual Ethernet Adapter #2'; Type = 'Ethernet'; Up = $true },
    [pscustomobject]@{ Name = 'Loopback Pseudo-Interface 1'; Description = 'Software Loopback Interface 1'; Type = 'Loopback'; Up = $true }) }
$vpnAdapters = { @(& $noVpnAdapters) + [pscustomobject]@{ Name = 'ProtonVPN'; Description = 'ProtonVPN Tunnel'; Type = 'Unknown'; Up = $true } }

function Get-Glance($Http, $Route, $Adapters) {
    $p = Get-NetworkProbe -Http $Http -Route $Route -Adapters $Adapters
    [pscustomobject]@{ Probe = $p; Tile = (Get-DnsGlance $p) }
}

Fact 'P52_VpnOnIsRedAndSaysWhereDnsPoints' {
    $g = Get-Glance (New-Http @('159.26.100.68') '159.26.100.68') $vpnRoute $vpnAdapters
    Assert-Equal 'red' $g.Tile.Color $g.Tile.Detail
    Assert-Equal 'VPN ON' $g.Tile.Word
    Assert-True ($g.Tile.Detail.StartsWith('VPN on: public DNS points at 159.26.100.68. Turn the VPN off.')) $g.Tile.Detail
    Assert-True ($g.Tile.Detail -match 'ProtonVPN') 'names the interface and the adapter'
    Assert-Equal $false $g.Probe.RouteIsLan
}

Fact 'P52_VpnOffWithDnsAtTheHouseIsGreen' {
    $g = Get-Glance (New-Http @($houseIp) $houseIp) $lanRoute $noVpnAdapters
    Assert-Equal 'green' $g.Tile.Color $g.Tile.Detail
    Assert-Equal 'OK' $g.Tile.Word
    Assert-True ($g.Tile.Detail -match [regex]::Escape($houseIp)) $g.Tile.Detail
    Assert-Equal 0 @($g.Probe.VpnAdapters).Count 'Hyper-V and loopback are not VPNs'
}

Fact 'P52_DnsNotTheHouseIsRedEvenWithNoVpnSeen' {
    # The VPN just went off, or one the route check cannot see: ddns has the VPN's exit.
    $g = Get-Glance (New-Http @('146.70.217.106') $houseIp) $lanRoute $noVpnAdapters
    Assert-Equal 'red' $g.Tile.Color $g.Tile.Detail
    Assert-True ($g.Tile.Detail.StartsWith('VPN on: public DNS points at 146.70.217.106, not this house (216.247.206.76). Turn the VPN off')) $g.Tile.Detail
}

Fact 'P52_AFailedCheckIsGreyNeverRed' {
    foreach ($case in @(
        @{ Name = 'DNS failed'; Http = (New-Http @() $houseIp -DnsFails); Route = $lanRoute },
        @{ Name = 'public IP failed'; Http = (New-Http @('146.70.217.106') $houseIp -TraceFails); Route = $lanRoute },
        @{ Name = 'both failed'; Http = (New-Http @() '' -DnsFails -TraceFails); Route = $lanRoute },
        @{ Name = 'route failed'; Http = (New-Http @('146.70.217.106') $houseIp); Route = { throw 'Find-NetRoute: access denied' } },
        @{ Name = 'no A record'; Http = (New-Http @() $houseIp); Route = $lanRoute })) {
        $g = Get-Glance $case.Http $case.Route $noVpnAdapters
        Assert-Equal 'gray' $g.Tile.Color ($case.Name + ': ' + $g.Tile.Detail)
        Assert-Equal 'UNKNOWN' $g.Tile.Word $case.Name
    }
    # The adapter list failing as well still never throws.
    $p = Get-NetworkProbe -Http (New-Http @() '' -DnsFails -TraceFails) -Route { throw 'no route' } -Adapters { throw 'no adapters' }
    Assert-Equal 'gray' (Get-DnsGlance $p).Color 'everything failed'
    Assert-True ($p.DnsError -match 'cloudflare-dns.com' -and $p.DnsError -match 'dns.google') ('both DNS services tried: ' + $p.DnsError)
}

Fact 'P52_ASplitTunnelVpnIsJudgedByDns' {
    # A VPN adapter is up but traffic leaves through the LAN: not red on the adapter alone.
    $g = Get-Glance (New-Http @($houseIp) $houseIp) $lanRoute $vpnAdapters
    Assert-Equal 'green' $g.Tile.Color $g.Tile.Detail
    Assert-True ($g.Tile.Detail -match 'VPN adapter up: ProtonVPN') 'still shown'
}

Fact 'P52_VpnAdapterShapes' {
    Assert-True (Test-VpnAdapter ([pscustomobject]@{ Name = 'NordLynx'; Description = 'NordLynx Tunnel'; Type = 'Unknown'; Up = $true })) 'NordLynx'
    Assert-True (Test-VpnAdapter ([pscustomobject]@{ Name = 'Office'; Description = 'WAN Miniport (IKEv2)'; Type = 'Ppp'; Up = $true })) 'a Windows VPN (PPP)'
    Assert-True (Test-VpnAdapter ([pscustomobject]@{ Name = 'Local Area Connection'; Description = 'TAP-Windows Adapter V9'; Type = 'Ethernet'; Up = $true })) 'OpenVPN TAP'
    Assert-True (-not (Test-VpnAdapter ([pscustomobject]@{ Name = 'ProtonVPN'; Description = 'ProtonVPN Tunnel'; Type = 'Unknown'; Up = $false }))) 'down is not on'
    Assert-True (-not (Test-VpnAdapter ([pscustomobject]@{ Name = 'vEthernet (Default Switch)'; Description = 'Hyper-V Virtual Ethernet Adapter'; Type = 'Ethernet'; Up = $true }))) 'Hyper-V'
    Assert-True (-not (Test-VpnAdapter ([pscustomobject]@{ Name = 'Wi-Fi'; Description = 'Intel(R) Wi-Fi 6E AX211 160MHz'; Type = 'Wireless80211'; Up = $true }))) 'Wi-Fi'
}

Fact 'P52_TheParsersReadRealShapes' {
    Assert-Equal '216.247.206.76' ((ConvertFrom-DohJson '{"Status":0,"TC":false,"RD":true,"RA":true,"AD":false,"CD":false,"Question":[{"name":"shatteredlegacyuo.com","type":1}],"Answer":[{"name":"shatteredlegacyuo.com","type":1,"TTL":300,"data":"216.247.206.76"}]}') -join ',')
    $threw = $false
    try { [void](ConvertFrom-DohJson '{"Status":2,"Question":[{"name":"shatteredlegacyuo.com","type":1}]}') } catch { $threw = $true }
    Assert-True $threw 'SERVFAIL is a failure, not an empty answer'
    Assert-Equal '216.247.206.76' (ConvertFrom-TraceText "fl=29f\nh=cloudflare.com\nip=216.247.206.76\nts=1759600000.1\n".Replace('\n', "`n"))
}

Fact 'P52_TheDnsTileIsBuiltAndDrawnWithoutTheNetwork' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-True ($null -ne $script:ui.NetTile) 'the fourth tile exists'
        Assert-Equal 'PUBLIC DNS: CHECKING' $script:ui.NetTile.Word.Text
        Assert-Equal $null $script:netProbe 'no probe was started'
        $script:netResult = Get-NetworkProbe -Http (New-Http @('159.26.100.68') '159.26.100.68') -Route $vpnRoute -Adapters $vpnAdapters
        Update-NetworkTile
        Assert-Equal 'PUBLIC DNS: VPN ON' $script:ui.NetTile.Word.Text
        Assert-True ($script:ui.NetTile.Detail.Text.StartsWith('VPN on: public DNS points at 159.26.100.68. Turn the VPN off.')) $script:ui.NetTile.Detail.Text
        $cols = Get-TileColors 'red'
        Assert-Equal $cols[0] $script:ui.NetTile.Panel.BackColor 'red tile'
    } finally { $form.Dispose(); $script:netResult = $null; $script:NetProbeDisabled = $false }
}

Remove-Item -LiteralPath $shimDir -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
