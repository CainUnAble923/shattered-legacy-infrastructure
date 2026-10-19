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
    # cc-P96: SL_FACT_FILTER (a regex on the name) runs a subset while working; unset, every fact runs.
    if ($env:SL_FACT_FILTER -and $Name -notmatch $env:SL_FACT_FILTER) { return }
    # cc-P116: each fact starts from the pinned cc-autopilot folder. A form built on $config re-picks the active folder
    # on its refresh (Update-AutopilotView, cc-P99), and since the real Golem cutover that swapped $config.Autopilot for
    # D:\UO\golem for every fact after it (P99_MoveToTheGolem... then failed in a full run, though it passed alone).
    if ($script:PinnedAutopilot) { $config.Autopilot = $script:PinnedAutopilot }
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
# cc-P108: scripts\live.json retires live, and every fact before P108 was written with live in service. Those facts
# pin liveEnabled true, which is also the proof that true brings today's behaviour back unchanged; the P108 facts
# pass -LiveEnabled $false, or $null to read the real file. A child process a fact starts reads the real file.
$script:RealGetConsoleConfig = ${function:Get-ConsoleConfig}
function Get-ConsoleConfig {
    param([string]$RepoRoot, [Nullable[bool]]$LiveEnabled = $true)
    & $script:RealGetConsoleConfig -RepoRoot $RepoRoot -LiveEnabled $LiveEnabled
}
# cc-P144: Get-ConsoleConfig reads this PC's Legion scripts settings (a third commit repo where there is a clone). The
# facts pin "no clone" so they read the same on every PC; the P144_ facts point it at scratch files of their own.
$env:SL_LEGION_SETTINGS = Join-Path ([IO.Path]::GetTempPath()) 'p144-no-legion-settings.json'
$config = Get-ConsoleConfig -RepoRoot $repo
# The window facts click every window action and expect a window; Publish to dev and Pull scripts need a clone to plan
# one, so $config has a scratch one (an empty folder with .git: nothing in these facts runs git in it).
$p144FactClone = Join-Path ([IO.Path]::GetTempPath()) 'p144-fact-clone'
New-Item -ItemType Directory -Path (Join-Path $p144FactClone '.git') -Force | Out-Null
$config.LegionRepo = $p144FactClone
# cc-P99: Get-ConsoleConfig picks D:\UO\golem once it is ACTIVE. The facts were written against cc-autopilot's
# folder, so they pin it; the Golem's own config has facts of its own (P99_*).
$config.Autopilot = New-AutopilotConfig 'D:\UO\cc-autopilot' 'D:\UO\cc-prompts\queue'
$script:PinnedAutopilot = $config.Autopilot
$cap    = Read-Fixture 'console-capture-2026-09-29.txt'
$probe  = Read-Fixture 'console-capture-probe-2026-09-29.txt'
$state  = New-ConsoleState -Config $config -Sections $cap
$now    = $state.Now
$test   = $state.Shards['test']
$live   = $state.Shards['live']

$probeContainer = ConvertFrom-ContainerInspect $probe['inspect sl-console-probe']
$probeLatest    = ConvertFrom-ImageInspect $probe['image sl-modernuo:latest']

# cc-P60. What Get-CommitFacts would read, written out: the commit plans are built from these and
# never from the real repos. The default is the state a commit goes ahead in: the script is there,
# the console is not SYSTEM, both repos have work, every note numbered 54 or above says DONE or
# STOPPED, and nothing pending changed in the last 5 minutes.
$p60Now = [datetime]::SpecifyKind([datetime]'2026-10-05 15:00:00', 'Utc')
function New-CommitFacts {
    param([switch]$System, [switch]$Clean, [switch]$NoScript, $Notes, $Changed, $Repos)
    if ($null -eq $Notes) {
        $Notes = @(
            [pscustomobject]@{ Name = 'cc-P12-audit-correction.md'; LastLine = '- No D-number or Q-number taken.' },
            [pscustomobject]@{ Name = 'cc-P57-batch-6.md'; LastLine = 'P57 DONE' },
            [pscustomobject]@{ Name = 'cc-P58-changelog-catch-up.md'; LastLine = 'P58 STOPPED: Chase asked to wait' })
    }
    if ($null -eq $Changed) { $Changed = @([pscustomobject]@{ Repo = 'code'; Path = 'scripts/Shard-Console.ps1'; LastWriteUtc = $p60Now.AddMinutes(-30) }) }
    if ($null -eq $Repos) {
        $Repos = @(
            [pscustomobject]@{ Name = 'code'; Path = 'D:\ShatteredLegacy'; Paths = @('scripts/Shard-Console.ps1', 'CHANGELOG.md'); Error = $null },
            [pscustomobject]@{ Name = 'docs'; Path = 'D:\UO\shard-migration'; Paths = @('notes/cc-P57-batch-6.md'); Error = $null })
        if ($Clean) { foreach ($r in $Repos) { $r.Paths = @() }; $Changed = @() }
    }
    $who = 'HOUSE\chase'
    if ($System) { $who = 'NT AUTHORITY\SYSTEM' }
    [pscustomobject]@{ Identity = $who; IsSystem = [bool]$System; ScriptExists = (-not $NoScript); Repos = $Repos; Notes = $Notes; Changed = $Changed; Now = $p60Now }
}
$commitFactsOk = New-CommitFacts

# cc-P63. What Get-PackageFacts would read, written out: the default is the state a build or a
# publish goes ahead in. The repo is clean, both scripts are there, and the one zip's version.json
# describes it. The changelog and the newest note say nothing about rollout order.
$pkgZipName = 'ShatteredLegacy-2026.10.05.1329.zip'
$pkgSha = ('ab' * 32)
function New-PackageZipRow {
    param([string]$Name = $pkgZipName, [string]$Version = '2026.10.05.1329', [string]$Notes = 'Smith deeds name their ingots.', [string]$Sha = $pkgSha, [string]$JsonBytes = '83886080', [switch]$NoJson, [string]$JsonError)
    $j = [pscustomobject]@{ Exists = (-not $NoJson); Error = $null; Version = $Version; Notes = $Notes; Sha256 = $Sha; Bytes = $JsonBytes }
    if ($NoJson) { $j.Version = $null; $j.Notes = $null; $j.Sha256 = $null; $j.Bytes = $null }
    if ($JsonError) { $j.Error = $JsonError }
    [pscustomobject]@{ Name = $Name; FullName = (Join-Path $config.PackageDist $Name); Bytes = 83886080; LastWriteUtc = $p60Now.AddHours(-1); Json = $j }
}
function New-PackageFacts {
    param([string[]]$Dirty = @(), [string]$RepoError, [switch]$NoBuild, [switch]$NoPublish, $Zips, $Picked, [string]$Changelog, $LatestNote, [string]$BuildDiff = 'same', $Client)
    if ($null -eq $Zips) { $Zips = @(New-PackageZipRow) }
    if ($null -eq $Picked) { $Picked = [pscustomobject]@{ Name = $pkgZipName; Sha256 = $pkgSha; InnerVersion = '2026.10.05.1329'; Error = $null } }
    if (-not $Changelog) { $Changelog = "# Shattered Legacy changelog`n<!--`n## 2026.10.02`n-->`n`n## 2026.10.05.2`n`n### Fixed`n- Smith deeds say which ingots they need.`n" }
    if ($null -eq $LatestNote) { $LatestNote = [pscustomobject]@{ Name = 'cc-P62-kitsune-walk-test.md'; Text = "P62 DONE`n" } }
    [pscustomobject]@{
        BuildExists = (-not $NoBuild); PublishExists = (-not $NoPublish); RepoPath = 'D:\ShatteredLegacy'; RepoPaths = @($Dirty)
        RepoError = $RepoError; Head = '9ab57e4'; BuildDiff = $BuildDiff; Zips = @($Zips); Picked = $Picked; Changelog = $Changelog; LatestNote = $LatestNote
        Client = $Client   # cc-P124: Get-PackageClientFacts; $null in the facts written before it, which then say nothing of it
    }
}
$pkgFactsOk = New-PackageFacts

# The captured state plus a real `docker images sl-modernuo` listing (fixtures, 2026-10-05). The
# capture's latest (8304d4a24fd2) is in no listed tag, so the rollback is by id; $deployStateP61 has
# latest on cc-p61's image, so the rollback names that tag.
$imagesText = [IO.File]::ReadAllText((Join-Path $fixture 'docker-images-sl-modernuo-2026-10-05.txt'))
function New-DeployState {
    param([string]$LatestId)
    $sec = [ordered]@{}
    foreach ($k in $cap.Keys) { $sec[$k] = $cap[$k] }
    $sec['images sl-modernuo'] = $imagesText
    $s = New-ConsoleState -Config $config -Sections $sec
    if ($LatestId) { $s.Latest.Id = $LatestId }
    $s
}
$deployState = New-DeployState
$deployStateP61 = New-DeployState 'sha256:9349dc9c9826aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'

# cc-P73. The captured state with the test shard running, which a wipe needs (it asks the running shard for
# its counts), and what Get-WipeFacts would read: not SYSTEM, no request waiting, room for the backup, and a
# census the shard logged. The census line is TestAccountWipe.cs's, as the shard prints it.
$wipeState = New-ConsoleState -Config $config -Sections $cap
$wipeState.Shards['test'].Container.Running = $true
$wipeState.Shards['test'].Container.Status = 'running'
$wipeCensusLog = "2026-10-12T15:00:01.123456789Z [AccountWipe] census: 6 accounts, 11 characters, 2 houses"
function New-WipeFacts {
    param([switch]$System, [switch]$RequestExists, $FreeBytes = 50GB, [switch]$NoCensus)
    $who = 'HOUSE\chase'
    if ($System) { $who = 'NT AUTHORITY\SYSTEM' }
    $census = $null
    if (-not $NoCensus) { $census = (ConvertFrom-WipeLog $wipeCensusLog).Census }
    [pscustomobject]@{ Identity = $who; IsSystem = [bool]$System; RequestExists = [bool]$RequestExists; FreeBytes = $FreeBytes; Census = $census }
}
$wipeFactsOk = New-WipeFacts

function Get-Plan {
    param([string]$Action, [string]$Shard = 'test', [string]$SnapshotName, [string]$Snapshot, $State = $state, [string]$CommitMessage, $CommitFacts = $commitFactsOk,
        [string]$PackageNotes, [string]$PackageZip, $PackageFacts = $pkgFactsOk, [string]$ImageTag)
    @(Get-ActionPlan -Action $Action -ShardKey $Shard -Config $config -State $State -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now -CommitMessage $CommitMessage -CommitFacts $CommitFacts -PackageNotes $PackageNotes -PackageZip $PackageZip -PackageFacts $PackageFacts -ImageTag $ImageTag)
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

# cc-P96, Fix test shard. Real fixtures: this morning's D89 crash log (the test container's own, 2026-10-14
# 07:07, account lines dropped), a healthy start's log (the bot's scratch shard on the P95 fix, 08:19, cut after
# its listeners), the test container's inspect while crashed and unhealthy (cc-P96's health check, 08:34) and
# while healthy, the pre-P96 exited one (no health check), and a real `docker images sl-modernuo` listing with
# sizes. Good tags do not exist yet in any real listing, so six are added as literals, one hour apart.
$p96CrashLog   = [IO.File]::ReadAllText((Join-Path $fixture 'test-shard-crash-d89-2026-10-14.txt'))
$p96HealthyLog = [IO.File]::ReadAllText((Join-Path $fixture 'test-shard-start-healthy-2026-10-14.txt'))
$p96ImagesText = [IO.File]::ReadAllText((Join-Path $fixture 'docker-images-sl-modernuo-2026-10-14.txt'))
$p96GoodLines  = @(1..6 | ForEach-Object { 'sl-modernuo:good-{0}|{1}|2026-10-14 {2:00}:00:00 -0500 CDT|1.29GB' -f ([string]$_ * 7), ('{0}' -f ([string]$_ * 12)), (8 + $_) })
$p96LiveId     = 'sha256:bc04a35bf344c49b3f7599703b9c135d63730e2e4784b4356ee85b4e9a4df2ef'
$p96Facts = [pscustomobject]@{
    Head       = '61db33c0a1b2c3d4e5f60718293a4b5c6d7e8f90'
    Commits    = @(
        [pscustomobject]@{ Sha = '61db33c0a1b2c3d4e5f60718293a4b5c6d7e8f90'; Short = '61db33c'; Date = '2026-10-14'; Subject = 'CC task P95: Fix D89' },
        [pscustomobject]@{ Sha = '10f4088aa1b2c3d4e5f60718293a4b5c6d7e8f90'; Short = '10f4088'; Date = '2026-10-14'; Subject = 'Shard work, 2026-10-14 07:05' }
    )
    SaveRecord = $null
    SaveNewer  = $null
}
function New-P96State {
    # The captured state with the 2026-10-14 image listing (and good tags unless -NoGood), optionally the test
    # container running with a given log since its start.
    param([string]$TestLog, [switch]$TestRunning, [switch]$NoGood)
    $sec = [ordered]@{}
    foreach ($k in $cap.Keys) { $sec[$k] = $cap[$k] }
    $lines = @($p96ImagesText.TrimEnd())
    if (-not $NoGood) { $lines = @($p96GoodLines) + $lines }
    $sec['images sl-modernuo'] = $lines -join "`n"
    if ($TestLog) { $sec['logs sl-modernuo-test'] = $TestLog }
    $s = New-ConsoleState -Config $config -Sections $sec
    if ($TestRunning) { $s.Shards['test'].Container.Running = $true; $s.Shards['test'].Container.Status = 'running' }
    $s
}
$p96GoodState  = New-P96State
$p96CrashState = New-P96State -TestLog $p96CrashLog -TestRunning
function Get-FixActionPlan {
    param([string]$Action, $State = $p96GoodState, $Facts = $p96Facts, [string]$Commit, [string]$ImageTag)
    @(Get-ActionPlan -Action $Action -ShardKey 'test' -Config $config -State $State -Now $now -FixFacts $Facts -Commit $Commit -ImageTag $ImageTag)
}

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
    # cc-P84: FanfareMute (cc-P79's player command) added; this list had gone stale since P79.
    Assert-Equal 'achievements,cleanup,cleanupall,FanfareMute,guild,ResetMyAccount,SelfRes,stats,TCFill,version' ((@($players | ForEach-Object { $_.Name }) | Sort-Object) -join ',')
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
        Assert-Equal 'Test shard,Fix test shard,Shard question,Snapshots,World setup,Map and Art,Diagnostics,Commit,Player package,Golem,Websites,Art,LIVE shard' ($tabs -join ',') 'cc-P74 adds Autopilot (Golem since cc-P99), cc-P101 Websites, cc-P96 Fix test shard beside Test shard, cc-P106 Shard question after it, cc-P135 Map and Art after World setup, cc-P152 Art before LIVE; LIVE stays last'
        Assert-Equal 'Preview commit,Commit and push' (@($script:ui.CommitButtons | ForEach-Object { $_.Text }) -join ',') 'cc-P60'
        Assert-Equal 'package.build,package.publish,package.check' (@($script:ui.PackageButtons | ForEach-Object { $_.Tag }) -join ',') 'cc-P63, and cc-P68''s Check selected'
        Assert-True ($null -ne $script:ui.CommitForce -and -not $script:ui.CommitForce.Checked) 'cc-P68: the Force box, unticked'
        Assert-True ($null -ne $script:ui.CommitCountdown -and $null -ne $script:ui.CountdownTimer) 'cc-P68: the countdown line and its timer'
        Assert-True ($null -ne $script:ui.PackageNotes -and $null -ne $script:ui.PackageList) 'cc-P63: the notes box and the zip list'
        $testTags = @($script:ui.Tabs.TabPages[0].Controls[0].Controls | Where-Object { $_ -is [Windows.Forms.Button] } | ForEach-Object { $_.Tag })
        Assert-True ($testTags -contains 'test.deploy') ('cc-P63: Deploy built image on the Test shard tab: ' + ($testTags -join ','))
        Assert-True ($null -ne $script:ui.CommitMessage) 'the message box'
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

# cc-P135: throwaway files for the Map and Art plans: a .vd with a valid header and empty records (int16 6, int16 layout
# 0, 110 records of -1), and a few bytes named hues.mul. The plans check the name and that the file is there; nothing
# here runs validate-art on them.
$p135Dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p135-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $p135Dir -Force | Out-Null
$p135Vd = Join-Path $p135Dir 'Sample.vd'
$p135VdBytes = New-Object byte[] (4 + 110 * 12)
$p135VdBytes[0] = 6
for ($i = 4; $i -lt $p135VdBytes.Length; $i++) { $p135VdBytes[$i] = 0xFF }
[IO.File]::WriteAllBytes($p135Vd, $p135VdBytes)
$p135Hues = Join-Path $p135Dir 'hues.mul'
[IO.File]::WriteAllBytes($p135Hues, (New-Object byte[] 64))

function Invoke-ClickCapture {
    # FromCapture defaults to Dry: a console launched with -DryRun. Dry with FromCapture false is
    # the toggle in a console whose status is live from docker.
    param([string]$ActionKey, [string]$ShardKey, [bool]$Dry, $State = $snapState, $FromCapture = $null, $CommitFacts = $commitFactsOk, [string]$CommitMessage,
        $PackageFacts = $pkgFactsOk, [string]$PackageNotes, [string]$PackageZip, [string]$ImageTag, [string]$Picked = 'sl-modernuo:cc-p61', [switch]$CommitForce,
        $WipeFacts = $wipeFactsOk, [string]$WipeName = 'newadmin', $FixFacts = $p96Facts, [string]$PickedCommit = '61db33c0a1b2c3d4e5f60718293a4b5c6d7e8f90',
        [string]$VdFile, [string]$Body, [string]$HuesFile)
    if ($null -eq $FromCapture) { $FromCapture = $Dry }
    # cc-P135: Check a .vd and Compare a hues.mul need a file to open a window; the facts' throwaway ones unless given.
    if ($ActionKey -eq 'art.checkvd' -and -not $PSBoundParameters.ContainsKey('VdFile')) { $VdFile = $p135Vd }
    if ($ActionKey -eq 'art.hues' -and -not $PSBoundParameters.ContainsKey('HuesFile')) { $HuesFile = $p135Hues }
    $rec = [pscustomobject]@{
        Spawned = New-Object 'System.Collections.Generic.List[string]'
        Lines = New-Object 'System.Collections.Generic.List[string]'
        Executed = New-Object 'System.Collections.Generic.List[string]'
        Confirms = New-Object 'System.Collections.Generic.List[string]'
        ConfirmTexts = New-Object 'System.Collections.Generic.List[string]'
        Pickers = 0
        WipeDialogs = New-Object 'System.Collections.Generic.List[string]'
    }
    # Defined here, so they shadow the real ones for Invoke-ConsoleButton only (dynamic scope).
    function Start-Process { param($FilePath, $ArgumentList) $rec.Spawned.Add([string]$ArgumentList) }
    function Invoke-Plan { $rec.Executed.Add('ran'); $true }
    function Show-TypedConfirm { param($Phrase, $Text, $Plan, $What) $rec.Confirms.Add($Phrase); $rec.ConfirmTexts.Add($Text); $true }
    function Show-YesNo { 'Yes' }
    function Get-ConsoleState { $State }
    function Get-CommitFacts { $CommitFacts }
    function Get-PackageFacts { $PackageFacts }
    function Show-ImagePicker { $rec.Pickers++; $Picked }
    function Get-WipeFacts { $WipeFacts }
    function Get-FixFacts { $FixFacts }
    function Show-CommitPicker { $rec.Pickers++; $PickedCommit }
    function Show-WipeAdminPicker { param($Summary) $rec.WipeDialogs.Add([string]$Summary); if ($WipeName) { $WipeName } else { $null } }
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $rec.Lines.Add($Text) }
    $saved = @($script:dry, $script:cfg, $script:statusFrom, $script:SelfPath, $script:fromCapture)
    $script:dry = $Dry; $script:cfg = $config; $script:statusFrom = $null; $script:fromCapture = [bool]$FromCapture
    $script:SelfPath = (Resolve-Path $script).Path
    $sn = $null; $s = $null
    if ($ActionKey -eq 'snapshot.create') { $sn = 'd37-check' }
    if ($ActionKey -in @('snapshot.restore', 'snapshot.delete')) { $s = $snapName }
    # cc-P63: the package buttons need notes or a zip to open a window; a picked image is the picker's.
    if ($ActionKey -eq 'package.build' -and -not $PSBoundParameters.ContainsKey('PackageNotes')) { $PackageNotes = 'P63 window check' }
    if ($ActionKey -in @('package.publish', 'package.check') -and -not $PSBoundParameters.ContainsKey('PackageZip')) { $PackageZip = $pkgZipName }
    try { Invoke-ConsoleButton -ActionKey $ActionKey -ShardKey $ShardKey -SnapshotName $sn -Snapshot $s -CommitMessage $CommitMessage -PackageNotes $PackageNotes -PackageZip $PackageZip -ImageTag $ImageTag -CommitForce:$CommitForce -VdFile $VdFile -Body $Body -HuesFile $HuesFile }
    finally { $script:dry, $script:cfg, $script:statusFrom, $script:SelfPath, $script:fromCapture = $saved }
    $rec
}

# cc-P106: the question states the window cases and the P106_ facts use (more on them with the P106_ facts below).
function Read-P106Text { param([string]$Name) [IO.File]::ReadAllText((Join-Path $fixture $Name)) }
$p106D94 = Read-P106Text 'live-shard-question-d94-2026-10-14.txt'
function Add-P106Stamps {
    # A log captured without -t, as docker logs -t would print it: one line every 10 ms from a start time.
    param([string]$Text, [datetime]$Start)
    $i = 0
    (@(($Text -replace "`r", '') -split "`n") | ForEach-Object { $Start.AddMilliseconds(10 * $i++).ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'", $script:Invariant) + ' ' + $_ }) -join "`n"
}
$p106T0 = [datetime]::SpecifyKind([datetime]'2026-10-14T13:06:17', 'Utc')
$p106Fatal = Add-P106Stamps (Read-P106Text 'test-shard-crash-d89-2026-10-14.txt') $p106T0
$p106Healthy = Add-P106Stamps (Read-P106Text 'test-shard-start-healthy-2026-10-14.txt') $p106T0
function New-P106State {
    # The capture, with a shard running and its timestamped log tail, read at $At.
    param([string]$Key = 'live', [string]$Tail, [datetime]$At = $p106T0.AddMinutes(5), [switch]$Stopped)
    $sec = [ordered]@{}
    foreach ($k in $cap.Keys) { $sec[$k] = $cap[$k] }
    $sec['meta'] = 'captured-at ' + (Format-DockerTime $At) + "`nsource docker"
    $c = $config.Shards[$Key].Container
    if ($Tail) { $sec['logtail ' + $c] = $Tail }
    $s = New-ConsoleState -Config $config -Sections $sec
    $s.Shards[$Key].Container.Running = -not $Stopped
    $s.Shards[$Key].Container.Status = $(if ($Stopped) { 'exited' } else { 'running' })
    # The question is read again with the container's state as the test sets it.
    $s.Shards[$Key].Question = Get-ShardQuestion -LogText $Tail -Running (-not $Stopped) -Now $At -QuietSeconds $config.QuestionQuietSeconds
    $s
}
# Every window action on every shard it can act on.
$windowCases = @(foreach ($a in @(Get-ConsoleActions | Where-Object { $_.RunIn -eq 'window' })) {
    $shards = @('test')
    if ($a.Key -like 'live.*') { $shards = @('live') }
    if ($a.Key -like 'snapshot.*') { $shards = @('test', 'live') }
    if ($a.Key -like 'commit.*') { $shards = @('') }   # no shard: the click names both repos
    if ($a.Key -like 'package.*') { $shards = @('') }  # nor the player package (cc-P63)
    if ($a.Key -like 'question.*') { $shards = @('live', 'test') }  # cc-P106: the shard picked on its tab
    foreach ($sk in $shards) {
        $st = $snapState
        if ($a.Key -eq 'live.start') { $st = $liveStoppedState }
        if ($a.Key -eq 'test.deploy') { $st = $deployState }
        if ($a.Key -eq 'test.wipe') { $st = $wipeState }
        if ($a.Key -like 'fix.*') { $st = $p96CrashState }  # cc-P96: good tags, images to prune, a crash
        # cc-P106: a shard asking D94's question (Enter: the fatal one), on each shard.
        if ($a.Key -like 'question.*') { $st = $(if ($a.Key -eq 'question.enter') { New-P106State -Key $sk -Tail $p106Fatal } else { New-P106State -Key $sk -Tail $p106D94 }) }
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

# --- cc-P60: the Commit tab, which runs D:\UO\Commit-ShardWork.ps1 --------------------------------
# The plans are built from New-CommitFacts, never the real repos. The two facts that run something
# run a throwaway stand-in for the commit script, or a real child under -Mode DryRun, which prints
# and exits before anything could run: no fact here can commit or push.

# Quotes of both kinds, $, ;, & and a word that looks like one of the script's own switches.
$p60Message = 'it''s "quoted" $HOME; Remove-Item -Recurse x & echo %PATH% -NoPush'
# The form facts above leave the console's output box pointing at a disposed form; the executor
# facts below write to the screen instead, as a headless window does.
$script:OutputBox = $null

Fact 'P60_TheTwoCommitActionsExistWithTheirGroupMutatesAndRunIn' {
    $a = @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Commit' })
    Assert-Equal 'commit.preview,commit.run' (@($a | ForEach-Object { $_.Key }) -join ',')
    Assert-Equal $false $a[0].Mutates 'preview changes nothing'
    Assert-Equal $true $a[1].Mutates 'commit and push does'
    Assert-Equal 'window,window' (@($a | ForEach-Object { $_.RunIn }) -join ',') 'both in a window of their own'
    Assert-Equal 'commit shard work' (Get-ConfirmPhrase 'commit.run')
}

Fact 'P60_WithNoModeCommitRunIsRefusedAndThePreviewRuns' {
    # D37: a lost -Mode fails safe.
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'commit.run')
    Assert-Equal 'execute' (Resolve-RunMode -Action 'commit.preview') 'it only reads'
    Assert-Equal 'dry' (Resolve-RunMode -Action 'commit.run' -Mode Execute -DryRun)
    $b = Get-ModeBanner 'refuse' 'commit.run' 'repos'
    Assert-True ($b.Lines[1] -match '^  REFUSED  commit\.run') $b.Lines[1]
}

# The real-child facts below take the phrase off the line as well. Without it Invoke-Plan refuses
# before any step, so a broken mode check fails the fact instead of reaching the real script: there
# is no recorder for git as there is for docker. (Found by breaking D37 on purpose: the child ran
# commit.run for real and only the in-flight guard stopped it.)
function Remove-Phrase { param([string]$Line) $Line -replace ' -ConfirmLive "[^"]*"', '' }

Fact 'P60_TheRealChildGivenNoModeRefusesCommitRun' {
    $line = Remove-Phrase ((Invoke-ClickCapture 'commit.run' '' $false).Spawned[0] -replace ' -Mode Execute', '')
    Assert-True (-not ($line -match '-Mode|-ConfirmLive')) $line
    $r = Invoke-Child $line
    Assert-Equal 2 $r.Exit (($r.Out -join ' | ') + $r.Err)
    Assert-True ($r.Out[1] -match '^  REFUSED  commit\.run') ($r.Out -join ' | ')
    Assert-Equal 0 @($r.Out | Where-Object { $_ -match 'Commit-ShardWork' }).Count ('nothing past the banner: ' + ($r.Out -join ' | '))
}

Fact 'P60_TheDryRunPlanPrintsTheCommandLineWithTheMessageQuoted' {
    $p = Get-Plan 'commit.run' -CommitMessage $p60Message
    $text = (Format-Plan -Action 'commit.run' -ShardKey 'repos' -Plan $p -Header 'DRY RUN') -join "`n"
    $want = "& 'D:\UO\Commit-ShardWork.ps1' -Message 'it''s `"quoted`" `$HOME; Remove-Item -Recurse x & echo %PATH% -NoPush'"
    Assert-True ($text.Contains('RUN      ' + $want)) $text
    # And the printed line means the same to PowerShell: parse it and read the value back.
    $errs = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($want, [ref]$null, [ref]$errs)
    Assert-Equal 0 @($errs).Count (@($errs) -join '; ')
    $cmd = $ast.Find({ param($n) $n -is [Management.Automation.Language.CommandAst] }, $true)
    Assert-Equal $p60Message $cmd.CommandElements[2].Value 'the quoted value is the message'
    $preview = Get-Plan 'commit.preview' -CommitMessage $p60Message
    $ptext = (Format-Plan -Action 'commit.preview' -ShardKey 'repos' -Plan $preview) -join "`n"
    Assert-True ($ptext.Contains("& 'D:\UO\Commit-ShardWork.ps1' -DryRun -Message 'it''s")) $ptext
    $blank = (Format-Plan -Action 'commit.run' -ShardKey 'repos' -Plan (Get-Plan 'commit.run')) -join "`n"
    Assert-True ($blank.Contains("RUN      & 'D:\UO\Commit-ShardWork.ps1'`n")) ('blank: no -Message, the script''s default: ' + $blank)
}

Fact 'P60_AMessageCrossesTheChildCommandLineUnchanged' {
    # The click's real argument line, run by a real child under -Mode DryRun: it prints its plan
    # and exits before anything runs. The child reads the real repos' git status (read-only).
    Assert-True (Test-Path -LiteralPath $config.CommitScript) ($config.CommitScript + ' is needed for its plan line')
    $line = (Invoke-ClickCapture 'commit.run' '' $true -CommitMessage $p60Message).Spawned[0]
    Assert-True ($line -match ' -Mode DryRun -Action commit\.run -ConfirmLive "commit shard work" -CommitMessageB64 [A-Za-z0-9+/=]+$') $line
    Assert-True (-not ($line -match '-Shard')) ('no shard for the repos: ' + $line)
    $line = Remove-Phrase $line
    Assert-True (-not ($line -match '-ConfirmLive')) $line
    $r = Invoke-Child $line
    Assert-Equal 0 $r.Exit (($r.Out -join ' | ') + $r.Err)
    Assert-True ($r.Out[1] -match '^  DRY RUN  commit\.run \(both repos\)') $r.Out[1]
    $want = "& 'D:\UO\Commit-ShardWork.ps1' -Message 'it''s `"quoted`" `$HOME; Remove-Item -Recurse x & echo %PATH% -NoPush'"
    Assert-Equal 1 @($r.Out | Where-Object { $_.Contains('RUN      ' + $want) }).Count ($r.Out -join ' | ')
    Assert-Equal 0 $r.DockerCalls.Count 'the commit buttons never read docker'
}

function Invoke-P60Stand {
    # Commit-ShardWork.ps1 swapped for a stand-in that writes what it was given and exits with Exit.
    param([string]$Action, [string]$Message, [string]$Phrase, [int]$Exit = 0)
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p60-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $stand = Join-Path $dir 'Commit-ShardWork.ps1'
    $got = Join-Path $dir 'got.txt'
    Set-Content -LiteralPath $stand -Encoding ASCII -Value @(
        'param([string]$Message, [switch]$DryRun, [switch]$NoPush)',
        ("[IO.File]::WriteAllText('" + $got + "', ('dry=' + [bool]`$DryRun + ' nopush=' + [bool]`$NoPush + '|' + `$Message))"),
        "Write-Host 'stand-in: OK' -ForegroundColor Green",
        ('exit ' + $Exit))
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.CommitScript = $stand
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    try {
        $plan = @(Get-ActionPlan -Action $Action -Config $cfg -CommitFacts $commitFactsOk -CommitMessage $Message)
        $ok = @(Invoke-Plan -Plan $plan -Config $cfg -ActionKey $Action -ShardKey 'repos' -ConfirmLive $Phrase 6>$null)
        $text = $null
        if (Test-Path -LiteralPath $got) { $text = [IO.File]::ReadAllText($got) }
        $out = @(Get-ChildItem -LiteralPath (Join-Path $dir 'shard-console-output') -Filter '*.log' -ErrorAction SilentlyContinue | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join ''
        [pscustomobject]@{ Ok = $ok; Got = $text; Output = $out }
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P60_AMessageReachesTheScriptUnchanged' {
    $r = Invoke-P60Stand 'commit.run' $p60Message 'commit shard work'
    Assert-Equal 1 $r.Ok.Count ('one result: ' + ($r.Ok -join ' | '))
    Assert-Equal $true $r.Ok[0] $r.Output
    Assert-Equal ('dry=False nopush=False|' + $p60Message) $r.Got 'every character, and -NoPush inside it is only text'
    Assert-True ($r.Output -match 'stand-in: OK') ('its own output is kept: ' + $r.Output)
    $p = Invoke-P60Stand 'commit.preview' $p60Message $null
    Assert-Equal ('dry=True nopush=False|' + $p60Message) $p.Got 'the preview passes -DryRun'
    $d = Invoke-P60Stand 'commit.run' '' 'commit shard work'
    Assert-Equal 'dry=False nopush=False|' $d.Got 'blank: no -Message, so the script uses its own default'
}

Fact 'P60_TheCommitRunsOnlyWithThePhraseAndItsFailureIsAFailure' {
    foreach ($given in $null, '', 'Commit shard work', 'commit', 'yes') {
        $r = Invoke-P60Stand 'commit.run' 'x' $given
        Assert-Equal $false $r.Ok[0] ("phrase '" + $given + "'")
        Assert-Equal $null $r.Got ("phrase '" + $given + "' ran it")
    }
    $bad = Invoke-P60Stand 'commit.run' 'x' 'commit shard work' 2
    Assert-Equal $false $bad.Ok[0] 'the script exiting 2 (a repo refused or FAILED) is a failure here too'
    Assert-True ($bad.Output -match 'exited with 2') $bad.Output
}

Fact 'P60_TheGuardFlagsANoteThatHasNotSaidDone' {
    $notes = @(
        [pscustomobject]@{ Name = 'cc-P57-batch-6.md'; LastLine = '- the last bullet of section 4' },
        [pscustomobject]@{ Name = 'cc-P59-evo-pet-body-sheet.md'; LastLine = '' })
    $f = @(Get-CommitInFlight -Notes $notes -Changed @() -Now $p60Now)
    Assert-Equal 2 $f.Count (@($f | ForEach-Object { $_.Text }) -join ' | ')
    Assert-Equal 'P57 has not said DONE: cc-P57-batch-6.md ends "- the last bullet of section 4". Committing now may publish half its work.' $f[0].Text
    Assert-True ($f[1].Text -like 'P59 has not said DONE:*empty*') $f[1].Text
}

Fact 'P60_TheGuardPassesANoteEndingDoneOrStopped' {
    foreach ($last in 'P57 DONE', 'P57 STOPPED: x', '  P57 DONE  ') {
        $f = @(Get-CommitInFlight -Notes @([pscustomobject]@{ Name = 'cc-P57-batch-6.md'; LastLine = $last }) -Changed @() -Now $p60Now)
        Assert-Equal 0 $f.Count ("'" + $last + "'")
    }
    foreach ($last in 'P57 done', 'P56 DONE', 'P57 DONE.', 'P57 STOPPED:', 'P570 DONE', '**P57 DONE**') {
        $f = @(Get-CommitInFlight -Notes @([pscustomobject]@{ Name = 'cc-P57-batch-6.md'; LastLine = $last }) -Changed @() -Now $p60Now)
        Assert-Equal 1 $f.Count ("'" + $last + "' is not the finishing line")
    }
    # Notes from before the finishing rule (P54) are not read as running; nor is anything else.
    $old = @([pscustomobject]@{ Name = 'cc-P53-two-hundred-cap.md'; LastLine = '10. Promote D-113' },
             [pscustomobject]@{ Name = 'shard-console.md'; LastLine = 'x' },
             [pscustomobject]@{ Name = 'cc-P-f12-xp-curve.md'; LastLine = 'x' })
    Assert-Equal 0 @(Get-CommitInFlight -Notes $old -Changed @() -Now $p60Now).Count
}

Fact 'P60_TheGuardFlagsAFileChangedInTheLastFiveMinutes' {
    $changed = @(
        [pscustomobject]@{ Repo = 'code'; Path = 'scripts/Shard-Console.ps1'; LastWriteUtc = $p60Now.AddSeconds(-90) },
        [pscustomobject]@{ Repo = 'docs'; Path = 'notes/old.md'; LastWriteUtc = $p60Now.AddMinutes(-5).AddSeconds(-1) })
    # cc-P68: the window is seconds now (60 by default); P60's five minutes is 300 of them.
    $f = @(Get-CommitInFlight -Notes @() -Changed $changed -Now $p60Now -Seconds 300)
    Assert-Equal 1 $f.Count (@($f | ForEach-Object { $_.Text }) -join ' | ')
    Assert-Equal 'code: scripts/Shard-Console.ps1 changed 90 s ago, inside the last 5 minutes. Something may still be writing it.' $f[0].Text
}

Fact 'P60_ANoteWithoutDoneBlocksCommitRunAndThePreviewNamesItAndRuns' {
    $busy = New-CommitFacts -Notes @([pscustomobject]@{ Name = 'cc-P57-batch-6.md'; LastLine = 'still writing' }) -Changed @(
        [pscustomobject]@{ Repo = 'code'; Path = 'CHANGELOG.md'; LastWriteUtc = $p60Now.AddSeconds(-10) })
    $run = Get-Plan 'commit.run' -CommitFacts $busy
    $refused = @($run | Where-Object { $_.Kind -eq 'refuse' } | ForEach-Object { $_.Text })
    Assert-True (@($refused | Where-Object { $_ -like 'P57 has not said DONE*' }).Count -eq 1) ($refused -join ' | ')
    Assert-True (@($refused | Where-Object { $_ -like 'code: CHANGELOG.md changed 10 s ago*' }).Count -eq 1) ($refused -join ' | ')
    $r = Invoke-ClickCapture 'commit.run' '' $false -CommitFacts $busy
    Assert-Equal 0 $r.Spawned.Count 'no window'
    Assert-Equal 0 $r.Confirms.Count 'and no phrase asked for: there is no override'
    Assert-True (@($r.Lines | Where-Object { $_ -match 'P57 has not said DONE' }).Count -eq 1) ($r.Lines -join ' | ')
    $prev = Get-Plan 'commit.preview' -CommitFacts $busy
    Assert-Equal 0 @($prev | Where-Object { $_.Kind -eq 'refuse' }).Count 'the preview is not blocked'
    Assert-True (@($prev | Where-Object { $_.Kind -eq 'say' -and $_.Text -like 'WARNING: P57 has not said DONE*' }).Count -eq 1) 'and names it'
    Assert-Equal 1 (Invoke-ClickCapture 'commit.preview' '' $false -CommitFacts $busy).Spawned.Count 'and runs'
}

Fact 'P60_SystemRefusesCommitRunAndThePreviewSaysSo' {
    $sys = New-CommitFacts -System
    $run = Get-Plan 'commit.run' -CommitFacts $sys
    $refused = @($run | Where-Object { $_.Kind -eq 'refuse' })
    Assert-Equal 1 $refused.Count (@($run | ForEach-Object { $_.Kind + ' ' + $_.Text }) -join ' | ')
    Assert-True ($refused[0].Text -like 'This console is running as NT AUTHORITY\SYSTEM. Git refuses*') $refused[0].Text
    Assert-Equal 0 (Invoke-ClickCapture 'commit.run' '' $false -CommitFacts $sys).Spawned.Count
    $prev = Get-Plan 'commit.preview' -CommitFacts $sys
    Assert-Equal 0 @($prev | Where-Object { $_.Kind -eq 'refuse' }).Count
    Assert-True (@($prev | Where-Object { $_.Text -like 'WARNING: This console is running as NT AUTHORITY\SYSTEM*' }).Count -eq 1) 'the preview says the same'
}

Fact 'P60_BothReposCleanSaysSoInsteadOfAskingForThePhrase' {
    $clean = New-CommitFacts -Clean
    $r = Invoke-ClickCapture 'commit.run' '' $false -CommitFacts $clean
    Assert-Equal 0 $r.Confirms.Count 'no phrase asked for'
    Assert-Equal 0 $r.Spawned.Count
    Assert-True (@($r.Lines | Where-Object { $_ -eq 'commit.run: Nothing to commit: both repos are clean.' }).Count -eq 1) ($r.Lines -join ' | ')
    # A git failure is not clean (D66): one repo unreadable is not "nothing to commit".
    $broken = New-CommitFacts -Repos @(
        [pscustomobject]@{ Name = 'code'; Path = 'D:\ShatteredLegacy'; Paths = @(); Error = 'fatal: detected dubious ownership' },
        [pscustomobject]@{ Name = 'docs'; Path = 'D:\UO\shard-migration'; Paths = @(); Error = $null }) -Changed @()
    $p = Get-Plan 'commit.run' -CommitFacts $broken
    Assert-Equal 0 @($p | Where-Object { $_.Text -match 'Nothing to commit' }).Count
    Assert-True (@($p | Where-Object { $_.Text -like 'WARNING: git status failed in code*dubious ownership*' }).Count -eq 1) 'it says so'
}

Fact 'P60_AMissingScriptMakesBothButtonsSaySoAndDoNothing' {
    foreach ($a in 'commit.preview', 'commit.run') {
        $r = Invoke-ClickCapture $a '' $false -CommitFacts (New-CommitFacts -NoScript)
        Assert-Equal 0 $r.Spawned.Count $a
        Assert-Equal 0 $r.Confirms.Count $a
        Assert-True (@($r.Lines | Where-Object { $_ -like ($a + ': D:\UO\Commit-ShardWork.ps1 is not there*') }).Count -eq 1) ($a + ': ' + ($r.Lines -join ' | '))
    }
}

Fact 'P60_ACleanCommitAsksForThePhraseSaysPublicFirstAndOpensItsWindow' {
    $r = Invoke-ClickCapture 'commit.run' '' $false
    Assert-Equal 'commit shard work' ($r.Confirms -join ',')
    Assert-Equal 1 $r.Spawned.Count ($r.Lines -join ' | ')
    Assert-True ($r.Spawned[0] -match ' -NoExit ') ('the window stays open: ' + $r.Spawned[0])
    Assert-True ($r.Spawned[0] -match ' -Mode Execute -Action commit\.run -ConfirmLive "commit shard work"$') $r.Spawned[0]
    Assert-Equal ('BEFORE YOU TYPE: ' + $script:CommitPublicWarning) (Get-ConfirmPreface (Get-Plan 'commit.run'))
    Assert-Equal '' (Get-ConfirmPreface (Get-Plan 'commit.preview')) 'the preview asks nothing'
    $prev = Invoke-ClickCapture 'commit.preview' '' $false
    Assert-Equal 0 $prev.Confirms.Count
    Assert-True ($prev.Spawned[0] -match ' -NoExit .* -Mode Execute -Action commit\.preview$') $prev.Spawned[0]
}

Fact 'P60_TheSafetyCheckRefusesARealCommitWithoutThePhraseOrAnotherScript' {
    $real = New-PlanStep -Kind script -Path $config.CommitScript -Named ([ordered]@{ Message = 'x' })
    Assert-Refused @($real) 'runs the commit for real without a typed confirmation' 'no phrase'
    Assert-Equal 0 @(Test-PlanSafety @((New-PlanStep -Kind confirm -Phrase 'commit shard work'), $real) $config).Count 'with the phrase'
    $dry = New-PlanStep -Kind script -Path $config.CommitScript -Named ([ordered]@{ DryRun = $true })
    Assert-Equal 0 @(Test-PlanSafety @($dry) $config).Count 'the preview needs none'
    $other = New-PlanStep -Kind script -Path 'D:\UO\Something-Else.ps1' -Named ([ordered]@{ DryRun = $true })
    Assert-Refused @($other) 'the only script this console runs' 'another script'
}

Fact 'P60_PorcelainPathsAreReadAsTheCommitScriptReadsThem' {
    $p = @(ConvertFrom-GitPorcelain @(' M scripts/Shard-Console.ps1', '?? notes/new file.md', 'R  old.md -> docs/new.md', '?? "with space.txt"', ''))
    Assert-Equal 'scripts/Shard-Console.ps1|notes/new file.md|docs/new.md|with space.txt' ($p -join '|')
}

# --- cc-P63: Player package (build, publish) and Test shard > Deploy built image ------------------
# Plans are built from New-PackageFacts and New-DeployState, never the real dist\ or docker. What
# runs runs stand-ins (a build script, a docker that only records), or a real child under no mode or
# -Mode DryRun. No fact here can publish, retag sl-modernuo:latest or restart a shard.

# Quotes of both kinds, $, ; and & in one line of printable ASCII, as a notes line must be.
$p63Notes = 'It''s "new" $HOME; Remove-Item x & echo %PATH% -GateSelfTest'

Fact 'P63_TheThreeActionsExistWithTheirGroupMutatesAndRunIn' {
    $want = @{ 'package.build' = 'Player package'; 'package.publish' = 'Player package'; 'test.deploy' = 'Test shard' }
    foreach ($k in $want.Keys) {
        $a = @(Get-ConsoleActions | Where-Object { $_.Key -eq $k })
        Assert-Equal 1 $a.Count $k
        Assert-Equal $want[$k] $a[0].Group $k
        Assert-Equal $true $a[0].Mutates $k
        Assert-Equal 'window' $a[0].RunIn $k
    }
    Assert-Equal 'publish player package' (Get-ConfirmPhrase 'package.publish')
    Assert-Equal 'deploy to test' (Get-ConfirmPhrase 'test.deploy')
    Assert-Equal 'Publish player package' (Get-ConfirmSubject 'package.publish')
    Assert-Equal 'Deploy to test' (Get-ConfirmSubject 'test.deploy')
}

Fact 'P63_D37WithNoModeEachIsRefused' {
    foreach ($k in 'package.build', 'package.publish', 'test.deploy') {
        Assert-Equal 'refuse' (Resolve-RunMode -Action $k) $k
        Assert-Equal 'dry' (Resolve-RunMode -Action $k -Mode Execute -DryRun) $k
        Assert-True ((Get-ModeBanner 'refuse' $k 'x').Lines[1] -match ('^  REFUSED  ' + [regex]::Escape($k))) $k
    }
    Assert-True ((Get-ModeBanner 'execute' 'package.publish' 'package').Lines[2] -match 'PUBLIC') 'the publish window says public first'
    Assert-True ((Get-ModeBanner 'execute' 'package.build' 'package').Lines[2] -match 'Nothing is published') 'the build window says it publishes nothing'
}

Fact 'P63_TheRealChildGivenNoModeRefusesEachAndCallsNoDocker' {
    # The click's real line minus its mode and its phrase. For the build, minus its notes too, so a
    # broken mode check is refused by the plan before any build could start.
    foreach ($k in 'package.build', 'package.publish', 'test.deploy') {
        $c = [pscustomobject]@{ Key = $k }
        $st = $snapState
        if ($k -eq 'test.deploy') { $st = $deployState }
        $line = (Invoke-ClickCapture $k '' $false $st).Spawned[0] -replace ' -PackageNotesB64 \S+', ''
        $line = Remove-Phrase ($line -replace ' -Mode Execute', '')
        Assert-True (-not ($line -match ' -Mode | -ConfirmLive | -PackageNotesB64 ')) ($c.Key + ': ' + $line)
        $r = Invoke-Child $line
        Assert-Equal 2 $r.Exit ($c.Key + ': ' + ($r.Out -join ' | ') + $r.Err)
        Assert-True ($r.Out[1] -match ('^  REFUSED  ' + [regex]::Escape($c.Key))) ($c.Key + ': ' + ($r.Out -join ' | '))
        Assert-Equal 0 $r.DockerCalls.Count ($c.Key + ' called docker: ' + ($r.DockerCalls -join ' | '))
        Assert-Equal 0 @($r.Out | Where-Object { $_ -match 'PLAN|Build-PlayerPackage|Publish-PlayerPackage|docker tag' }).Count ($c.Key + ': nothing past the banner: ' + ($r.Out -join ' | '))
    }
}

Fact 'P63_TheDryRunPlansPrintTheCommandLinesWithNotesQuoted' {
    $p = Get-Plan 'package.build' -PackageNotes $p63Notes
    $text = (Format-Plan -Action 'package.build' -ShardKey 'package' -Plan $p -Header 'DRY RUN') -join "`n"
    $want = '& ' + (ConvertTo-PsLiteral $config.BuildPackage) + " -Notes 'It''s `"new`" `$HOME; Remove-Item x & echo %PATH% -GateSelfTest'"
    Assert-True ($text.Contains('RUN      ' + $want)) $text
    $errs = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($want, [ref]$null, [ref]$errs)
    Assert-Equal 0 @($errs).Count (@($errs) -join '; ')
    $cmd = $ast.Find({ param($n) $n -is [Management.Automation.Language.CommandAst] }, $true)
    Assert-Equal $p63Notes $cmd.CommandElements[2].Value 'the quoted value is the notes, -GateSelfTest inside it only text'
    Assert-True ($text -match 'CHECK    & .*Build-PlayerPackage\.ps1'' -CheckZip ''<the one zip in .*dist written since this plan started>''') $text
    $pub = (Format-Plan -Action 'package.publish' -ShardKey 'package' -Plan (Get-Plan 'package.publish' -PackageZip $pkgZipName)) -join "`n"
    Assert-True ($pub.Contains("RUN      & 'D:\UO\haven-migration\Publish-PlayerPackage.ps1' -Zip '" + (Join-Path $config.PackageDist $pkgZipName) + "'")) $pub
    $dep = (Format-Plan -Action 'test.deploy' -ShardKey 'test' -Plan (Get-Plan 'test.deploy' -State $deployState -ImageTag 'sl-modernuo:cc-p61')) -join "`n"
    Assert-True ($dep.Contains('RUN      docker tag sl-modernuo:cc-p61 sl-modernuo:latest')) $dep
    Assert-True ($dep -match 'RUN      powershell\.exe .*Start-TestShard\.ps1 -Down\n') $dep
    Assert-True ($dep -match 'RUN      powershell\.exe .*Start-TestShard\.ps1 -SkipBuild\n') $dep
    Assert-True ($dep.IndexOf('docker tag') -lt $dep.IndexOf('-Down') -and $dep.IndexOf('-Down') -lt $dep.IndexOf('-SkipBuild')) 'tag, down, start, in that order'
}

Fact 'P63_NotesCrossTheChildCommandLineUnchanged' {
    # The click's real line, run by a real child under -Mode DryRun: it prints the plan and exits. It
    # reads the real repo's git status and dist\ (read-only), and no docker.
    $line = (Invoke-ClickCapture 'package.build' '' $true -PackageNotes $p63Notes).Spawned[0]
    Assert-True ($line -match ' -Mode DryRun -Action package\.build -PackageNotesB64 [A-Za-z0-9+/=]+$') $line
    $r = Invoke-Child $line
    Assert-Equal 0 $r.Exit (($r.Out -join ' | ') + $r.Err)
    Assert-True ($r.Out[1] -match '^  DRY RUN  package\.build \(player package\)') $r.Out[1]
    $want = " -Notes 'It''s `"new`" `$HOME; Remove-Item x & echo %PATH% -GateSelfTest'"
    Assert-Equal 1 @($r.Out | Where-Object { $_.Contains('Build-PlayerPackage.ps1''' + $want) }).Count ($r.Out -join ' | ')
    Assert-Equal 0 $r.DockerCalls.Count 'the package buttons never read docker'
}

Fact 'P63_PackageBuildRefusesOnADirtyTreeAndNamesTheFiles' {
    $dirty = New-PackageFacts -Dirty @('scripts/Shard-Console.ps1', 'CHANGELOG.md')
    $p = Get-Plan 'package.build' -PackageNotes 'x' -PackageFacts $dirty
    $ref = @($p | Where-Object { $_.Kind -eq 'refuse' })
    Assert-Equal 1 $ref.Count (@($p | ForEach-Object { $_.Kind + ' ' + $_.Text }) -join ' | ')
    Assert-Equal 'D:\ShatteredLegacy has 2 uncommitted file(s), so the package would not be built from a commit: scripts/Shard-Console.ps1, CHANGELOG.md. Commit first (Commit tab), then build.' $ref[0].Text
    Assert-Equal 0 @($p | Where-Object { $_.Text -match 'committed and clean' }).Count 'it does not say clean'
    $r = Invoke-ClickCapture 'package.build' '' $false -PackageFacts $dirty
    Assert-Equal 0 $r.Spawned.Count 'no window'
    Assert-Equal 0 $r.Confirms.Count 'no override'
    Assert-True (@($r.Lines | Where-Object { $_ -like 'package.build: D:\ShatteredLegacy has 2 uncommitted file(s)*' }).Count -eq 1) ($r.Lines -join ' | ')
    $many = New-PackageFacts -Dirty @(1..13 | ForEach-Object { 'f' + $_ + '.txt' })
    $t = @(Get-Plan 'package.build' -PackageNotes 'x' -PackageFacts $many | Where-Object { $_.Kind -eq 'refuse' })[0].Text
    Assert-True ($t -match 'has 13 uncommitted file\(s\).*f1\.txt, .*f10\.txt and 3 more\.') $t
    $broken = New-PackageFacts -RepoError 'fatal: detected dubious ownership'
    Assert-True (@(Get-Plan 'package.build' -PackageNotes 'x' -PackageFacts $broken | Where-Object { $_.Kind -eq 'refuse' -and $_.Text -match 'git status failed.*dubious' }).Count -eq 1) 'a git failure is not clean (D66)'
    $clean = Get-Plan 'package.build' -PackageNotes 'x'
    Assert-Equal 0 @($clean | Where-Object { $_.Kind -eq 'refuse' }).Count 'clean and with notes, it builds'
    Assert-Equal 'say,script,checkzip,log' (@($clean | ForEach-Object { $_.Kind }) -join ',')
    Assert-Equal 1 (Invoke-ClickCapture 'package.build' '' $false).Spawned.Count 'and opens its window'
}

Fact 'P63_PackageBuildNeedsOneLineOfAsciiNotes' {
    foreach ($n in '', '   ', ('two' + "`n" + 'lines'), ('curly ' + [char]0x2019), ('em ' + [char]0x2014)) {
        $p = Get-Plan 'package.build' -PackageNotes $n
        Assert-Equal 1 @($p | Where-Object { $_.Kind -eq 'refuse' }).Count ("notes '" + $n + "'")
    }
    $r = Invoke-ClickCapture 'package.build' '' $false -PackageNotes ''
    Assert-Equal 0 $r.Spawned.Count 'blank notes open nothing'
    Assert-True (@($r.Lines | Where-Object { $_ -like 'package.build: A package needs notes*' }).Count -eq 1) ($r.Lines -join ' | ')
}

Fact 'P63_TheZipPickerReadsVersionJson' {
    # A real dist\ folder: three zips written oldest to newest, one with a version.json, one with none,
    # one whose version.json does not parse.
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p63-dist-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    try {
        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        $mk = {
            param([string]$Name, [string]$Stamp, [int]$AgeMin)
            $zp = Join-Path $dir $Name
            $za = [IO.Compression.ZipFile]::Open($zp, 'Create')
            try {
                $e = $za.CreateEntry(($Name -replace '\.zip$', '') + '/app/package-version.txt')
                $w = New-Object IO.StreamWriter($e.Open()); try { $w.Write($Stamp + "`r`n") } finally { $w.Dispose() }
            } finally { $za.Dispose() }
            (Get-Item -LiteralPath $zp).LastWriteTimeUtc = [datetime]::UtcNow.AddMinutes(-$AgeMin)
            $zp
        }
        $a = & $mk 'ShatteredLegacy-2026.10.05.1010.zip' '2026.10.05.1010' 30
        $b = & $mk 'ShatteredLegacy-2026.10.05.1100.zip' '2026.10.05.1100' 20
        $c = & $mk 'ShatteredLegacy-2026.10.05.1200.zip' '2026.10.05.1200' 10
        $sha = (Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash.ToLower()
        $len = (Get-Item -LiteralPath $a).Length
        [IO.File]::WriteAllText(($a -replace '\.zip$', '.version.json'), ('{ "version": "2026.10.05.1010", "sha256": "' + $sha + '", "bytes": ' + $len + ', "url": "x", "notes": "It''s \"new\"" }'))
        [IO.File]::WriteAllText(($c -replace '\.zip$', '.version.json'), '{ not json')
        Set-Content -LiteralPath (Join-Path $dir 'notes.txt') -Value 'not a zip'
        $rows = @(Get-PackageZips $dir)
        Assert-Equal 'ShatteredLegacy-2026.10.05.1200.zip,ShatteredLegacy-2026.10.05.1100.zip,ShatteredLegacy-2026.10.05.1010.zip' (@($rows | ForEach-Object { $_.Name }) -join ',') 'newest first, zips only'
        Assert-True ($rows[0].Json.Exists -and $rows[0].Json.Error) 'unparsable json is an error, not empty'
        Assert-Equal $false $rows[1].Json.Exists 'no version.json'
        Assert-Equal '2026.10.05.1010' $rows[2].Json.Version
        Assert-Equal 'It''s "new"' $rows[2].Json.Notes 'the notes, as launchers print them'
        Assert-Equal $sha $rows[2].Json.Sha256
        Assert-Equal '2026.10.05.1010' (Read-ZipPackageVersion $a) 'the stamp inside the zip'
        # The guard: each of the three zips against its own hash and stamp.
        $picked = { param($p) [pscustomobject]@{ Name = (Split-Path $p -Leaf); Sha256 = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLower(); InnerVersion = (Read-ZipPackageVersion $p); Error = $null } }
        Assert-Equal 0 @(Get-PackageZipProblems $rows[2] (& $picked $a)).Count 'a zip its version.json describes passes'
        Assert-True ((@(Get-PackageZipProblems $rows[1] (& $picked $b)))[0] -match 'has no ShatteredLegacy-2026\.10\.05\.1100\.version\.json') 'no json'
        Assert-True ((@(Get-PackageZipProblems $rows[0] (& $picked $c)))[0] -match 'does not parse') 'bad json'
        $rows[2].Json.Version = '2026.10.05.9999'; $rows[2].Json.Sha256 = ('0' * 64); $rows[2].Json.Bytes = '1'
        $pr = @(Get-PackageZipProblems $rows[2] (& $picked $a))
        Assert-Equal 3 $pr.Count ($pr -join ' | ')
        Assert-True ($pr[0] -match 'version 2026\.10\.05\.9999.*says 2026\.10\.05\.1010') $pr[0]
        Assert-True ($pr[1] -match 'sha256') $pr[1]
        Assert-True ($pr[2] -match ('says 1 bytes, the zip is ' + $len)) $pr[2]
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P63_APublishWhoseVersionJsonDoesNotMatchIsBlockedBeforeThePhrase' {
    $bad = New-PackageFacts -Picked ([pscustomobject]@{ Name = $pkgZipName; Sha256 = ('cd' * 32); InnerVersion = '2026.10.05.1329'; Error = $null })
    $r = Invoke-ClickCapture 'package.publish' '' $false -PackageFacts $bad
    Assert-Equal 0 $r.Confirms.Count 'no phrase asked for'
    Assert-Equal 0 $r.Spawned.Count 'no window'
    Assert-True (@($r.Lines | Where-Object { $_ -like 'package.publish: Not publishing: version.json says sha256 abab*the zip is cdcd*' }).Count -eq 1) ($r.Lines -join ' | ')
    $none = New-PackageFacts -Zips @(New-PackageZipRow -NoJson)
    Assert-Equal 0 (Invoke-ClickCapture 'package.publish' '' $false -PackageFacts $none).Spawned.Count 'no version.json'
    Assert-Equal 0 (Invoke-ClickCapture 'package.publish' '' $false -PackageZip '').Spawned.Count 'nothing chosen'
    Assert-Equal 0 (Invoke-ClickCapture 'package.publish' '' $false -PackageZip 'ShatteredLegacy-nope.zip').Spawned.Count 'not in dist'
    Assert-Equal 0 (Invoke-ClickCapture 'package.publish' '' $false -PackageFacts (New-PackageFacts -NoPublish)).Spawned.Count 'no script'
}

Fact 'P63_PublishAsksThePhraseSaysPublicFirstAndOpensItsWindow' {
    $r = Invoke-ClickCapture 'package.publish' '' $false
    Assert-Equal 'publish player package' ($r.Confirms -join ',')
    Assert-Equal 1 $r.Spawned.Count ($r.Lines -join ' | ')
    Assert-True ($r.Spawned[0] -match (' -NoExit .* -Mode Execute -Action package\.publish -ConfirmLive "publish player package" -PackageZip "' + [regex]::Escape($pkgZipName) + '"$')) $r.Spawned[0]
    $p = Get-Plan 'package.publish' -PackageZip $pkgZipName
    Assert-Equal ('BEFORE YOU TYPE: ' + $script:PublishPublicWarning) (Get-ConfirmPreface $p)
    Assert-Equal 'confirm' $p[0].Kind 'the phrase comes first'
    $info = @($p | Where-Object { $_.Kind -eq 'say' })[0].Text
    Assert-True ($info -match '^zip ShatteredLegacy-2026\.10\.05\.1329\.zip, version 2026\.10\.05\.1329, 80\.0 MB, built .*What is new: Smith deeds name their ingots\.$') $info
    Assert-Equal 0 @($p | Where-Object { $_.Text -match '^Reminder' }).Count 'no rollout note when nothing says so'
    Assert-Equal '' (Get-ConfirmPreface (Get-Plan 'package.build' -PackageNotes 'x')) 'the build is not public'
    Assert-Equal ('BEFORE YOU TYPE: ' + $script:CommitPublicWarning) (Get-ConfirmPreface (Get-Plan 'commit.run')) 'and the commit keeps its own'
}

Fact 'P63_ThePublishRemindsThatTheServerFollowsWhenTheNotesSaySo' {
    $n = New-PackageFacts -LatestNote ([pscustomobject]@{ Name = 'cc-P62-kitsune-walk-test.md'; Text = "...`nPackage first, then the server, then the client check.`nP62 DONE`n" })
    $t = @(Get-Plan 'package.publish' -PackageZip $pkgZipName -PackageFacts $n | Where-Object { $_.Text -like 'Reminder*' })
    Assert-Equal 1 $t.Count 'from the newest note'
    Assert-Equal 'Reminder: cc-P62-kitsune-walk-test.md says package first, then the server. The server deploy follows this publish; it is not part of this button.' $t[0].Text
    $cl = "<!--`n## 2026.10.02`n- package first, then server (an example in the header)`n-->`n## 2026.10.05.3`n### Staff`n- Rollout: publish the package before deploying the server.`n## 2026.10.05.2`n"
    $t2 = @(Get-Plan 'package.publish' -PackageZip $pkgZipName -PackageFacts (New-PackageFacts -Changelog $cl) | Where-Object { $_.Text -like 'Reminder*' })
    Assert-True ($t2.Count -eq 1 -and $t2[0].Text -like 'Reminder: CHANGELOG.md 2026.10.05.3 says*') ($t2 | ForEach-Object { $_.Text })
    $old = "<!--`n- package first (header example)`n-->`n## 2026.10.05.3`n- nothing about order`n## 2026.10.05.2`n- package first, then server`n"
    Assert-Equal $null (Get-RolloutReminder -Changelog $old -LatestNote $null) 'only the newest section counts, and never the header'
    Assert-Equal 0 @(Get-Plan 'package.publish' -PackageZip $pkgZipName -PackageFacts $n | Where-Object { $_.Kind -eq 'refuse' }).Count 'a warning only'
}

Fact 'P63_ImageTagListingParsesRealDockerOutput' {
    $rows = @(ConvertFrom-ImageTagListing $imagesText)
    Assert-True ($rows.Count -ge 30) ('rows: ' + $rows.Count)
    $p62 = @($rows | Where-Object { $_.Tag -eq 'sl-modernuo:cc-p62' })[0]
    Assert-Equal 'b04ed61adb5a' $p62.Id
    Assert-Equal ([datetime]::SpecifyKind([datetime]'2026-10-05 19:19:04', 'Utc')) $p62.Created '14:19:04 -0500 is 19:19:04 UTC'
    $c = @(Get-DeployCandidates $deployStateP61 $config)
    Assert-True (@($c | Where-Object { $_.Tag -notlike 'sl-modernuo:cc-p*' }).Count -eq 0) 'cc-p tags only: not latest, pre-bump or <none>'
    Assert-Equal 'sl-modernuo:cc-p62' $c[0].Tag 'newest first'
    for ($i = 1; $i -lt $c.Count; $i++) { Assert-True ($c[$i - 1].Created -ge $c[$i].Created) ('order at ' + $i) }
    Assert-Equal 'sl-modernuo:cc-p61' (@($c | Where-Object { $_.IsLatest }) | ForEach-Object { $_.Tag }) 'and which one latest points at now'
}

Fact 'P63_TestDeployShowsTheOldLatestAndHowToRollBack' {
    $p = Get-Plan 'test.deploy' -State $deployStateP61 -ImageTag 'sl-modernuo:cc-p62'
    Assert-Equal 'confirm,say,exec,say,exec,exec,log' (@($p | ForEach-Object { $_.Kind }) -join ',')
    Assert-True ($p[1].Text -like 'sl-modernuo:latest before this deploy: 9349dc9c9826, built *') $p[1].Text
    Assert-Equal 'To roll back: deploy sl-modernuo:cc-p61 (9349dc9c9826).' $p[3].Text
    $q = Get-Plan 'test.deploy' -State $deployState -ImageTag 'sl-modernuo:cc-p62'
    Assert-Equal 'To roll back: docker tag 8304d4a24fd2 sl-modernuo:latest, then Test shard > Start (no cc-p tag names 8304d4a24fd2).' $q[3].Text
    Assert-True ($p[0].Text -like 'This points sl-modernuo:latest at sl-modernuo:cc-p62 (b04ed61adb5a, built *) and restarts the TEST shard on it. *') $p[0].Text
    # Refusals: the image latest already is, a tag that is not cc-p, one docker does not have, none.
    $same = @(Get-Plan 'test.deploy' -State $deployStateP61 -ImageTag 'sl-modernuo:cc-p61' | Where-Object { $_.Kind -eq 'refuse' })
    Assert-True ($same.Count -eq 1 -and $same[0].Text -match 'already is') 'already latest'
    foreach ($t in 'sl-modernuo:latest', 'sl-modernuo:pre-bump-7c9215d97', 'uo-modernuo:latest', 'sl-modernuo:cc-p99', '') {
        Assert-Equal 'refuse' ((Get-Plan 'test.deploy' -State $deployState -ImageTag $t)[0].Kind) ("'" + $t + "'")
    }
    $r = Invoke-ClickCapture 'test.deploy' 'test' $false $deployState
    Assert-Equal 1 $r.Pickers 'the click asks which image'
    Assert-Equal 'deploy to test' ($r.Confirms -join ',')
    Assert-True ($r.Spawned[0] -match ' -Mode Execute -Action test\.deploy -Shard test -ConfirmLive "deploy to test" -ImageTag "sl-modernuo:cc-p61"$') $r.Spawned[0]
    $cancel = Invoke-ClickCapture 'test.deploy' 'test' $false $deployState -Picked ''
    Assert-Equal 0 $cancel.Spawned.Count 'cancelling the picker runs nothing'
    Assert-Equal 0 $cancel.Confirms.Count
}

Fact 'P63_TheLiveLineIsReadFromWhatLiveWasCreatedFrom' {
    # From the capture: live was created from 'uo-modernuo' (docker-compose.yml builds its own image,
    # D29), as it still is on 2026-10-05. Moving sl-modernuo:latest changes nothing live.
    Assert-Equal 'uo-modernuo' $deployState.Shards['live'].Container.ConfigImage
    $line = Get-DeployLiveLine $deployState $config
    Assert-Equal "Live does not use this tag: sl-modernuo was created from 'uo-modernuo', and a restart keeps the image it runs, so moving sl-modernuo:latest changes nothing live. Only the live drift line compares against it." $line
    Assert-True ((Get-Plan 'test.deploy' -State $deployState -ImageTag 'sl-modernuo:cc-p62')[0].Text.EndsWith($line)) 'in the confirmation'
    $s = New-DeployState
    $s.Shards['live'].Container.ConfigImage = 'sl-modernuo:latest'
    Assert-True ((Get-DeployLiveLine $s $config) -like 'LIVE was created from sl-modernuo:latest. A restart keeps the image it runs; the next time live is recreated*') 'if it were'
}

Fact 'P63_TestDeploysPlanNeverNamesTheLiveContainer' {
    $live = $config.Shards['live'].Container
    foreach ($c in @(Get-DeployCandidates $deployState $config)) {
        $p = Get-Plan 'test.deploy' -State $deployState -ImageTag $c.Tag
        if ($p[0].Kind -eq 'refuse') { continue }
        $words = @($p | ForEach-Object { @($_.Exe) + @($_.Arguments) + @($_.Container) + @($_.Path) } | Where-Object { $_ })
        Assert-Equal 0 @($words | Where-Object { [string]$_ -ceq $live }).Count ($c.Tag + ': ' + ($words -join ' '))
        Assert-Equal 0 @($words | Where-Object { [string]$_ -match 'docker-compose\.yml|uo-modernuo' }).Count ($c.Tag + ' names live''s compose file or image')
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ($c.Tag + ' passes the safety check')
        $tags = @($p | Where-Object { $_.Kind -eq 'exec' -and $_.Exe -eq 'docker' })
        Assert-Equal 1 $tags.Count 'one docker command, the tag'
        Assert-Equal ('tag ' + $c.Tag + ' sl-modernuo:latest') ($tags[0].Arguments -join ' ')
    }
    # And the safety check refuses any plan that would: a deploy with a live step slipped in, a tag
    # onto anything but latest or from anything but cc-p, and a tag with no phrase.
    $ok = Get-Plan 'test.deploy' -State $deployState -ImageTag 'sl-modernuo:cc-p62'
    Assert-Refused (@($ok) + @(New-PlanStep -Kind exec -Exe 'docker' -Arguments @('restart', $live))) 'names the live container' 'a live restart in a deploy'
    Assert-Refused (@($ok) + @(New-PlanStep -Kind stopped -Container $live)) 'names the live container' 'a live check in a deploy'
    $conf = New-PlanStep -Kind confirm -Phrase 'deploy to test'
    Assert-Refused @($conf, (New-PlanStep -Kind exec -Exe 'docker' -Arguments @('tag', 'sl-modernuo:cc-p62', 'uo-modernuo:latest'))) 'the only tag this console makes' 'onto live''s image name'
    Assert-Refused @($conf, (New-PlanStep -Kind exec -Exe 'docker' -Arguments @('tag', 'uo-modernuo:latest', 'sl-modernuo:latest'))) 'the only tag this console makes' 'from a non cc-p image'
    Assert-Refused @(New-PlanStep -Kind exec -Exe 'docker' -Arguments @('tag', 'sl-modernuo:cc-p62', 'sl-modernuo:latest')) 'without a typed confirmation' 'no phrase'
}

Fact 'P63_ADeployStopsAtTheFirstFailureAndSaysHowToRollBack' {
    # The plan run by the real executor with the recording docker first on PATH, which exits 1: the
    # tag fails, so Start-TestShard.ps1 is never reached. Its log goes to a temp folder.
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p63-deploy-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    $cfg.StartTestShard = Join-Path $dir 'Start-TestShard.ps1'   # never there: reaching it would fail differently
    $oldPath = $env:PATH
    $env:PATH = $shimDir + ';' + $env:PATH
    $env:SL_DOCKER_SHIM_LOG = $shimLog
    $p63Lines = New-Object 'System.Collections.Generic.List[string]'
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $p63Lines.Add($Text) }
    try {
        Assert-Equal (Join-Path $shimDir 'docker.exe') (Get-Command docker -CommandType Application | Select-Object -First 1).Source 'the recorder is first on PATH'
        Remove-Item -LiteralPath $shimLog -ErrorAction SilentlyContinue
        $p = @(Get-ActionPlan -Action 'test.deploy' -Config $cfg -State $deployStateP61 -ImageTag 'sl-modernuo:cc-p62')
        $ok = Invoke-Plan -Plan $p -Config $cfg -ActionKey 'test.deploy' -ShardKey 'test' -ConfirmLive 'deploy to test'
        Assert-Equal $false $ok ($p63Lines -join ' | ')
        Assert-Equal 'tag sl-modernuo:cc-p62 sl-modernuo:latest' ((Get-Content -LiteralPath $shimLog) -join ' | ') 'docker saw the tag and nothing else'
        Assert-True (@($p63Lines | Where-Object { $_ -like 'FAILED at step 3:*' }).Count -eq 1) ($p63Lines -join ' | ')
        Assert-True (@($p63Lines | Where-Object { $_ -like '*sl-modernuo:latest before this deploy: 9349dc9c9826*' }).Count -eq 1) 'the old id was said before the tag'
        Assert-Equal 0 @($p63Lines | Where-Object { $_ -match 'Start-TestShard|To roll back' }).Count ('nothing after the failed step: ' + ($p63Lines -join ' | '))
        Assert-True ((Get-Content -LiteralPath $cfg.LogFile -Raw) -match 'FAILED test\.deploy shard=test at step 3') 'logged'
        $none = Invoke-Plan -Plan $p -Config $cfg -ActionKey 'test.deploy' -ShardKey 'test' -ConfirmLive 'deploy test'
        Assert-Equal $false $none 'a wrong phrase runs nothing'
        Assert-Equal 1 @(Get-Content -LiteralPath $shimLog).Count 'still the one call'
    } finally {
        $env:PATH = $oldPath
        Remove-Item Env:\SL_DOCKER_SHIM_LOG -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Fact 'P63_ABuildRunsTheScriptWithTheNotesThenChecksTheZipItWrote' {
    # Build-PlayerPackage.ps1 swapped for a stand-in that, given -Notes, writes a zip and records the
    # notes, and given -CheckZip records the path. dist\ is a temp folder holding an older zip, which
    # the check must not pick.
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p63-build-' + [guid]::NewGuid().ToString('N'))
    $dist = Join-Path $dir 'dist'
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $old = Join-Path $dist 'ShatteredLegacy-2026.10.01.0000.zip'
    Set-Content -LiteralPath $old -Value 'old'
    (Get-Item -LiteralPath $old).LastWriteTimeUtc = [datetime]::UtcNow.AddHours(-1)
    $stand = Join-Path $dir 'Build-PlayerPackage.ps1'
    $got = Join-Path $dir 'got.txt'
    Set-Content -LiteralPath $stand -Encoding ASCII -Value @(
        'param([string]$Notes, [string]$CheckZip, [switch]$GateSelfTest)',
        ('$got = ''' + $got + ''''),
        'if ($CheckZip) { [IO.File]::AppendAllText($got, "check=$CheckZip`n"); Write-Host "  GATE 4 passed: $CheckZip" -ForegroundColor Green; exit 0 }',
        '[IO.File]::AppendAllText($got, "notes=$Notes|self=$GateSelfTest`n")',
        ('$z = ''' + (Join-Path $dist 'ShatteredLegacy-2026.10.05.2359.zip') + ''''),
        'Set-Content -LiteralPath $z -Value new',
        ('[IO.File]::WriteAllText(($z -replace ''\.zip$'', ''.version.json''), ''{ "version": "2026.10.05.2359", "notes": "n" }'')'),
        'Write-Host "  built $z" -ForegroundColor Green')
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.BuildPackage = $stand
    $cfg.PackageDist = $dist
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    $p63Lines = New-Object 'System.Collections.Generic.List[string]'
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $p63Lines.Add($Text) }
    try {
        $p = @(Get-ActionPlan -Action 'package.build' -Config $cfg -PackageFacts $pkgFactsOk -PackageNotes $p63Notes)
        Assert-Equal 0 @(Test-PlanSafety $p $cfg).Count (@(Test-PlanSafety $p $cfg) -join ' | ')
        $ok = Invoke-Plan -Plan $p -Config $cfg -ActionKey 'package.build' -ShardKey 'package'
        Assert-Equal $true $ok ($p63Lines -join ' | ')
        $g = @(Get-Content -LiteralPath $got)
        Assert-Equal ('notes=' + $p63Notes + '|self=False') $g[0] 'every character of the notes, and -GateSelfTest inside them only text'
        Assert-Equal ('check=' + (Join-Path $dist 'ShatteredLegacy-2026.10.05.2359.zip')) $g[1] 'the check got the zip just written, not the older one'
        Assert-True (@($p63Lines | Where-Object { $_ -match 'zip     .*ShatteredLegacy-2026\.10\.05\.2359\.zip' }).Count -eq 1) ($p63Lines -join ' | ')
        Assert-True (@($p63Lines | Where-Object { $_ -match 'version 2026\.10\.05\.2359, .*What is new: n$' }).Count -eq 1) ($p63Lines -join ' | ')
        Assert-True (@($p63Lines | Where-Object { $_ -match 'GATE 4 passed' }).Count -eq 1) 'the gate results are shown'
        # A build that wrote no zip fails at the check, and says so.
        Remove-Item -Path (Join-Path $dist 'ShatteredLegacy-2026.10.05.2359.*')
        Set-Content -LiteralPath $stand -Encoding ASCII -Value 'param([string]$Notes, [string]$CheckZip) Write-Host built-nothing'
        $p63Lines.Clear()
        Assert-Equal $false (Invoke-Plan -Plan $p -Config $cfg -ActionKey 'package.build' -ShardKey 'package') ($p63Lines -join ' | ')
        Assert-True (@($p63Lines | Where-Object { $_ -like 'FAILED at step 3: expected one zip*found 0*' }).Count -eq 1) ($p63Lines -join ' | ')
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P63_TheSafetyCheckGuardsTheNewScripts' {
    $conf = New-PlanStep -Kind confirm -Phrase 'publish player package'
    $zip = Join-Path $config.PackageDist $pkgZipName
    Assert-Refused @(New-PlanStep -Kind script -Path $config.BuildPackage -Named ([ordered]@{ GateSelfTest = $true })) 'with -Notes or -CheckZip only' 'the gate self-test'
    Assert-Equal 0 @(Test-PlanSafety @(New-PlanStep -Kind script -Path $config.BuildPackage -Named ([ordered]@{ Notes = 'x' })) $config).Count 'a build needs no phrase'
    Assert-Refused @(New-PlanStep -Kind script -Path $config.PublishScript -Named ([ordered]@{ Zip = $zip })) 'publishes a player package without a typed confirmation' 'no phrase'
    Assert-Equal 0 @(Test-PlanSafety @($conf, (New-PlanStep -Kind script -Path $config.PublishScript -Named ([ordered]@{ Zip = $zip }))) $config).Count 'with it'
    Assert-Refused @($conf, (New-PlanStep -Kind script -Path $config.PublishScript -Named ([ordered]@{ Zip = 'D:\Downloads\x.zip' }))) 'runs only with -Zip on a zip in' 'a zip outside dist'
    Assert-Refused @($conf, (New-PlanStep -Kind script -Path $config.PublishScript -Named ([ordered]@{}))) 'runs only with -Zip' 'no -Zip: the script would pick the newest itself'
    Assert-Refused @(New-PlanStep -Kind checkzip -Path 'D:\UO\Other.ps1' -To $config.PackageDist) 'a zip is checked only by' 'another checker'
    Assert-Refused @(New-PlanStep -Kind script -Path 'D:\UO\Other.ps1') 'the only script this console runs' 'another script'
    foreach ($a in 'package.build', 'package.publish') {
        $p = Get-Plan $a -PackageNotes 'x' -PackageZip $pkgZipName
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ($a + ': ' + (@(Test-PlanSafety $p $config) -join ' | '))
    }
}

# --- cc-P68: Check selected, a cancelled publish, the quiet window and its countdown, Force ---------
# As above: plans from written-out facts, stand-in scripts in temp folders. Nothing publishes or commits.

Fact 'P68_TheCheckActionExistsNotMutatingInAWindow' {
    $a = @(Get-ConsoleActions | Where-Object { $_.Key -eq 'package.check' })
    Assert-Equal 1 $a.Count
    Assert-Equal 'Player package' $a[0].Group
    Assert-Equal $false $a[0].Mutates 'it only reads'
    Assert-Equal 'window' $a[0].RunIn 'in a window that stays open'
    Assert-Equal 'execute' (Resolve-RunMode -Action 'package.check') 'so a child with no mode still runs it (D37 refuses only what changes something)'
    $b = Get-ModeBanner 'execute' 'package.check' 'package'
    Assert-True ($b.Lines[1] -match '^  CHECKING  package\.check \(player package\)' -and $b.Lines[2] -match 'changes nothing') ($b.Lines -join ' | ')
    $r = Invoke-ClickCapture 'package.check' '' $false
    Assert-Equal 0 $r.Confirms.Count 'no phrase'
    Assert-Equal 1 $r.Spawned.Count ($r.Lines -join ' | ')
    Assert-True ($r.Spawned[0] -match (' -NoExit .* -Mode Execute -Action package\.check -PackageZip "' + [regex]::Escape($pkgZipName) + '"$')) $r.Spawned[0]
}

Fact 'P68_TheCheckPlanNamesCheckZipAndTheSelectedZipQuoted' {
    # A name with a single quote in it, as a person could rename a zip in dist\.
    $odd = 'ShatteredLegacy-it''s $x; y.zip'
    $f = New-PackageFacts -Zips @((New-PackageZipRow), (New-PackageZipRow -Name $odd))
    $p = Get-Plan 'package.check' -PackageZip $odd -PackageFacts $f
    Assert-Equal 'gatecheck' (@($p | ForEach-Object { $_.Kind }) -join ',')
    $full = Join-Path $config.PackageDist $odd
    $text = (Format-Plan -Action 'package.check' -ShardKey 'package' -Plan $p) -join "`n"
    $want = '& ' + (ConvertTo-PsLiteral $config.BuildPackage) + ' -CheckZip ' + (ConvertTo-PsLiteral $full)
    Assert-True ($text.Contains('CHECK    ' + $want)) $text
    $errs = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($want, [ref]$null, [ref]$errs)
    Assert-Equal 0 @($errs).Count (@($errs) -join '; ')
    $cmd = $ast.Find({ param($n) $n -is [Management.Automation.Language.CommandAst] }, $true)
    Assert-Equal 'CheckZip' $cmd.CommandElements[1].ParameterName
    Assert-Equal $full $cmd.CommandElements[2].Value 'the quoted value is the selected zip, whole'
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count (@(Test-PlanSafety $p $config) -join ' | ')
}

Fact 'P68_TheCheckIsNotRefusedOnADirtyTreeAndSaysWhenTheBuildScriptDiffersFromHead' {
    $dirty = New-PackageFacts -Dirty @('player-package/Build-PlayerPackage.ps1') -BuildDiff 'differs'
    $p = Get-Plan 'package.check' -PackageZip $pkgZipName -PackageFacts $dirty
    Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'refuse' }).Count 'it only reads'
    $say = @($p | Where-Object { $_.Kind -eq 'say' })
    Assert-Equal 1 $say.Count (@($p | ForEach-Object { $_.Kind + ' ' + $_.Text }) -join ' | ')
    Assert-Equal 'Note: the working tree''s player-package\Build-PlayerPackage.ps1 differs from HEAD''s (9ab57e4), and it is the script doing this check.' $say[0].Text
    Assert-Equal 1 (Invoke-ClickCapture 'package.check' '' $false -PackageFacts $dirty).Spawned.Count 'and it opens its window'
    Assert-Equal 0 @(Get-Plan 'package.check' -PackageZip $pkgZipName | Where-Object { $_.Kind -eq 'say' }).Count 'nothing said when it is HEAD''s'
    $unk = @(Get-Plan 'package.check' -PackageZip $pkgZipName -PackageFacts (New-PackageFacts -BuildDiff 'git diff exited with 128 fatal: x') | Where-Object { $_.Kind -eq 'say' })
    Assert-True ($unk.Count -eq 1 -and $unk[0].Text -like 'Note: could not tell whether*128*') 'a git failure is said, not read as the same (D66)'
}

Fact 'P68_TheCheckSaysGate5WillFailWithNoVersionJsonAndRefusesWhatItCannotCheck' {
    $none = New-PackageFacts -Zips @(New-PackageZipRow -NoJson)
    $p = Get-Plan 'package.check' -PackageZip $pkgZipName -PackageFacts $none
    Assert-Equal 'say,gatecheck' (@($p | ForEach-Object { $_.Kind }) -join ',') 'not refused: the check is how you find out'
    Assert-Equal 'No ShatteredLegacy-2026.10.05.1329.version.json beside it, so gate 5 will fail.' $p[0].Text
    foreach ($c in @(@('', $pkgFactsOk, 'Choose a zip'), @('ShatteredLegacy-nope.zip', $pkgFactsOk, 'no zip'), @($pkgZipName, (New-PackageFacts -NoBuild), 'is not there'))) {
        $r = Invoke-ClickCapture 'package.check' '' $false -PackageZip $c[0] -PackageFacts $c[1]
        Assert-Equal 0 $r.Spawned.Count $c[2]
        Assert-True (@($r.Lines | Where-Object { $_ -like ('package.check: *' + $c[2] + '*') }).Count -eq 1) ($c[2] + ': ' + ($r.Lines -join ' | '))
    }
}

Fact 'P68_ACheckShowsEachGateLineThenPassedOrFailed' {
    # Build-PlayerPackage.ps1 swapped for a stand-in shaped like its -CheckZip: GATE 4 and 6 lines,
    # GATE 5 only when version.json is there, and a throw for a zip named *bad*.
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p68-check-' + [guid]::NewGuid().ToString('N'))
    $dist = Join-Path $dir 'dist'
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $stand = Join-Path $dir 'Build-PlayerPackage.ps1'
    Set-Content -LiteralPath $stand -Encoding ASCII -Value @(
        'param([string]$Notes, [string]$CheckZip, [switch]$GateSelfTest)',
        'if ($CheckZip -match ''bad'') { throw "GATE 4: $CheckZip fails:`n  app\Data\x.mul is EA''s" }',
        'Write-Host "  GATE 4 passed: $CheckZip" -ForegroundColor Green',
        'Write-Host "  GATE 6 passed: $CheckZip" -ForegroundColor Green',
        '$jp = $CheckZip -replace ''\.zip$'', ''.version.json''',
        'if (Test-Path -LiteralPath $jp) { Write-Host "  GATE 5 passed: $jp" -ForegroundColor Green }',
        'exit 0')
    $good = Join-Path $dist 'ShatteredLegacy-2026.10.05.1606.zip'
    $nojs = Join-Path $dist 'ShatteredLegacy-2026-09-26.zip'
    $bad  = Join-Path $dist 'ShatteredLegacy-bad.zip'
    foreach ($z in $good, $nojs, $bad) { Set-Content -LiteralPath $z -Value 'zip' }
    Set-Content -LiteralPath ($good -replace '\.zip$', '.version.json') -Value '{}'
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.BuildPackage = $stand
    $cfg.PackageDist = $dist
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    $p68Lines = New-Object 'System.Collections.Generic.List[string]'
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $p68Lines.Add($Text) }
    try {
        $run = {
            param([string]$Zip)
            $p68Lines.Clear()
            $facts = [pscustomobject]@{ BuildExists = $true; Head = 'abc1234'; BuildDiff = 'same'; Zips = @(Get-PackageZips $dist) }
            $p = @(Get-ActionPlan -Action 'package.check' -Config $cfg -PackageFacts $facts -PackageZip $Zip)
            Assert-Equal 0 @(Test-PlanSafety $p $cfg).Count (@(Test-PlanSafety $p $cfg) -join ' | ')
            Invoke-Plan -Plan $p -Config $cfg -ActionKey 'package.check' -ShardKey 'package'
        }
        Assert-Equal $true (& $run (Split-Path $good -Leaf)) ($p68Lines -join ' | ')
        foreach ($g in 'GATE 4 passed', 'GATE 6 passed', 'GATE 5 passed') { Assert-True (@($p68Lines | Where-Object { $_ -match $g }).Count -eq 1) ($g + ': ' + ($p68Lines -join ' | ')) }
        Assert-Equal 'PASSED: ShatteredLegacy-2026.10.05.1606.zip passed gates 4, 6 and 5.' $p68Lines[$p68Lines.Count - 1]
        Assert-Equal $false (& $run (Split-Path $nojs -Leaf)) ($p68Lines -join ' | ')
        Assert-True (@($p68Lines | Where-Object { $_ -eq '  GATE 5 failed: there is no ShatteredLegacy-2026-09-26.version.json beside the zip, so launchers could not be told about it.' }).Count -eq 1) ($p68Lines -join ' | ')
        Assert-True ($p68Lines[$p68Lines.Count - 1] -like 'FAILED: ShatteredLegacy-2026-09-26.zip did not pass the package gates*') $p68Lines[$p68Lines.Count - 1]
        Assert-Equal $false (& $run (Split-Path $bad -Leaf)) ($p68Lines -join ' | ')
        Assert-True (@($p68Lines | Where-Object { $_ -match '^  GATE 4: .*bad\.zip fails:' }).Count -eq 1) ('the gate''s own words: ' + ($p68Lines -join ' | '))
        Assert-True ($p68Lines[$p68Lines.Count - 1] -like 'FAILED: ShatteredLegacy-bad.zip*') $p68Lines[$p68Lines.Count - 1]
        Assert-Equal 3 @(Get-ChildItem -LiteralPath $dist -Filter '*.zip').Count 'nothing in dist\ changed'
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P68_TheSafetyCheckKeepsTheCheckToTheBuildScriptAndDist' {
    $in = Join-Path $config.PackageDist $pkgZipName
    Assert-Equal 0 @(Test-PlanSafety @(New-PlanStep -Kind gatecheck -Path $config.BuildPackage -From $in) $config).Count 'the build script on a zip in dist'
    Assert-Refused @(New-PlanStep -Kind gatecheck -Path 'D:\UO\Other.ps1' -From $in) 'a zip is checked only by' 'another script'
    Assert-Refused @(New-PlanStep -Kind gatecheck -Path $config.BuildPackage -From 'D:\Downloads\x.zip') 'checks only a zip in' 'a zip outside dist'
    Assert-Refused @(New-PlanStep -Kind gatecheck -Path $config.BuildPackage -From (Join-Path $config.PackageDist 'x.txt')) 'checks only a zip in' 'not a zip'
}

Fact 'P68_ThePublishScriptsThreeExitCodesReadAsTheyMean' {
    Assert-Equal 'published' (Get-PublishOutcome 0).Result
    $c = Get-PublishOutcome 3
    Assert-Equal 'cancelled' $c.Result
    Assert-Equal 'Publish cancelled. Nothing was published.' $c.Text
    foreach ($x in 1, 2, 255) { $o = Get-PublishOutcome $x; Assert-True ($o.Result -eq 'failed' -and $o.Text -match (' exited with ' + $x + '\.')) ([string]$x) }
    # And through the executor, with a stand-in publish script that exits each code.
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p68-pub-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $stand = Join-Path $dir 'Publish-PlayerPackage.ps1'
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.PublishScript = $stand
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    $p68Lines = New-Object 'System.Collections.Generic.List[string]'
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $p68Lines.Add($Text) }
    try {
        $plan = @((New-PlanStep -Kind confirm -Phrase 'publish player package'),
            (New-PlanStep -Kind script -Path $stand -Named ([ordered]@{ Zip = (Join-Path $cfg.PackageDist $pkgZipName) }) -Text 'publish'),
            (New-PlanStep -Kind log -Text 'package.publish x'))
        Assert-Equal 0 @(Test-PlanSafety $plan $cfg).Count (@(Test-PlanSafety $plan $cfg) -join ' | ')
        $go = {
            param([int]$Code, [string]$Say)
            Set-Content -LiteralPath $stand -Encoding ASCII -Value @('param([string]$Zip)', ("Write-Host '" + $Say + "'"), ('exit ' + $Code))
            $p68Lines.Clear()
            $script:PlanCancelled = $false
            $ok = Invoke-Plan -Plan $plan -Config $cfg -ActionKey 'package.publish' -ShardKey 'package' -ConfirmLive 'publish player package'
            [pscustomobject]@{ Ok = $ok; Cancelled = [bool]$script:PlanCancelled; Lines = @($p68Lines) }
        }
        $no = & $go 3 'Nothing done.'
        Assert-Equal $false $no.Ok ($no.Lines -join ' | ')
        Assert-Equal $true $no.Cancelled 'cancelled, so the window exits 3, not 1'
        Assert-Equal '      Publish cancelled. Nothing was published.' $no.Lines[$no.Lines.Count - 1] ($no.Lines -join ' | ')
        Assert-Equal 0 @($no.Lines | Where-Object { $_ -match 'FAILED' }).Count 'a cancel is not a failure'
        Assert-True ((Get-Content -LiteralPath $cfg.LogFile -Raw) -match 'CANCELLED package\.publish') 'logged as cancelled'
        $yes = & $go 0 'PUBLISHED'
        Assert-Equal $true $yes.Ok ($yes.Lines -join ' | ')
        Assert-True (@($yes.Lines | Where-Object { $_ -match 'Published: Publish-PlayerPackage\.ps1 exited 0\.' }).Count -eq 1) ($yes.Lines -join ' | ')
        $bad = & $go 1 'STOPPED: x'
        Assert-Equal $false $bad.Ok
        Assert-Equal $false $bad.Cancelled
        Assert-True (@($bad.Lines | Where-Object { $_ -like 'FAILED at step 2: Publish failed: Publish-PlayerPackage.ps1 exited with 1.*' }).Count -eq 1) ($bad.Lines -join ' | ')
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P68_TheQuietWindowComesFromConfigAndTheRefusalQuotesIt' {
    Assert-Equal 60 $config.CommitQuietSeconds 'the default is 60 seconds'
    $f = New-CommitFacts -Changed @([pscustomobject]@{ Repo = 'code'; Path = 'design/x.png'; LastWriteUtc = $p60Now.AddSeconds(-30) })
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $refusals = { param($c) @(Get-ActionPlan -Action 'commit.run' -Config $c -CommitFacts $f | Where-Object { $_.Kind -eq 'refuse' } | ForEach-Object { $_.Text }) }
    $r60 = & $refusals $cfg
    Assert-Equal 2 $r60.Count ($r60 -join ' | ')
    Assert-Equal 'code: design/x.png changed 30 s ago, inside the last 60 seconds. Something may still be writing it.' $r60[0]
    Assert-True ($r60[1] -match 'has changed for 60 seconds\.' -and $r60[1] -notmatch 'minutes') $r60[1]
    $cfg.CommitQuietSeconds = 20
    Assert-Equal 0 (& $refusals $cfg).Count 'at 20 seconds, 30 s ago is quiet'
    $cfg.CommitQuietSeconds = 300
    Assert-True ((& $refusals $cfg)[1] -match 'has changed for 5 minutes\.') 'the window, as configured'
    Assert-Equal '90 seconds' (Format-QuietWindow 90)
}

Fact 'P68_AFileChanged30sAgoBlocksAt60AndNotAt20' {
    $c = @([pscustomobject]@{ Repo = 'code'; Path = 'design/x.png'; LastWriteUtc = $p60Now.AddSeconds(-30) })
    Assert-Equal 1 @(Get-CommitInFlight -Notes @() -Changed $c -Now $p60Now -Seconds 60).Count 'at 60'
    Assert-Equal 0 @(Get-CommitInFlight -Notes @() -Changed $c -Now $p60Now -Seconds 20).Count 'at 20'
    Assert-Equal 0 @(Get-CommitInFlight -Notes @() -Changed $c -Now $p60Now -Seconds 30).Count 'at exactly the window it is quiet, as the countdown reaches 0:00'
}

Fact 'P68_TheCountdownTextForAGivenAge' {
    $two = @([pscustomobject]@{ Repo = 'code'; Path = 'scripts/old.ps1'; LastWriteUtc = $p60Now.AddSeconds(-50) },
             [pscustomobject]@{ Repo = 'code'; Path = 'design/x.png'; LastWriteUtc = $p60Now.AddSeconds(-18) })
    $f = New-CommitFacts -Changed $two
    Assert-Equal 'Commit allowed in 0:42 (waiting on design/x.png)' (Get-CommitCountdown -Facts $f -Config $config -Now $p60Now) 'the newest file names the wait'
    Assert-Equal 'Commit allowed in 0:41 (waiting on design/x.png)' (Get-CommitCountdown -Facts $f -Config $config -Now $p60Now.AddSeconds(1)) 'and it ticks down'
    Assert-Equal 'Commit allowed in 0:01 (waiting on design/x.png)' (Get-CommitCountdown -Facts $f -Config $config -Now $p60Now.AddSeconds(41.5)) 'rounded up, never 0:00 while refused'
    Assert-Equal 'Ready to commit' (Get-CommitCountdown -Facts $f -Config $config -Now $p60Now.AddSeconds(42)) 'at zero'
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.CommitQuietSeconds = 90
    Assert-Equal 'Commit allowed in 1:12 (waiting on design/x.png)' (Get-CommitCountdown -Facts $f -Config $cfg -Now $p60Now)
    Assert-Equal 'Nothing to commit: both repos are clean.' (Get-CommitCountdown -Facts (New-CommitFacts -Clean) -Config $config -Now $p60Now)
    Assert-True ((Get-CommitCountdown -Facts $f -Config $config -Now $p60Now -Force) -like '*(waiting on design/x.png). Force is ticked: Commit and push skips this wait') 'with Force ticked'
}

Fact 'P68_TheNotDoneBlockStillWinsAtZero' {
    $notes = @([pscustomobject]@{ Name = 'cc-P65-shard-thing.md'; LastLine = 'still writing' })
    $f = New-CommitFacts -Notes $notes -Changed @([pscustomobject]@{ Repo = 'code'; Path = 'design/x.png'; LastWriteUtc = $p60Now.AddSeconds(-18) })
    Assert-Equal 'Commit allowed in 0:42 (waiting on design/x.png). Also blocked: P65 has not said DONE (cc-P65-shard-thing.md)' (Get-CommitCountdown -Facts $f -Config $config -Now $p60Now)
    Assert-Equal 'Not ready: P65 has not said DONE (cc-P65-shard-thing.md).' (Get-CommitCountdown -Facts $f -Config $config -Now $p60Now.AddSeconds(60)) 'at zero the prompt is what blocks'
    Assert-Equal 'Not ready: P65 has not said DONE (cc-P65-shard-thing.md).' (Get-CommitCountdown -Facts $f -Config $config -Now $p60Now.AddSeconds(60) -Force) 'and Force does not lift it'
    $sys = New-CommitFacts -System
    Assert-True ((Get-CommitCountdown -Facts $sys -Config $config -Now $p60Now) -like 'Not ready: this console runs as NT AUTHORITY\SYSTEM*') 'SYSTEM too'
}

Fact 'P68_ForceSkipsOnlyTheRecentFileWaitAndTheConfirmationNamesTheFiles' {
    $recent = @([pscustomobject]@{ Repo = 'code'; Path = 'CHANGELOG.md'; LastWriteUtc = $p60Now.AddSeconds(-10) },
                [pscustomobject]@{ Repo = 'docs'; Path = 'notes/cc-P68-console-package-check.md'; LastWriteUtc = $p60Now.AddSeconds(-25) })
    $f = New-CommitFacts -Changed $recent
    $plain = @(Get-ActionPlan -Action 'commit.run' -Config $config -CommitFacts $f)
    Assert-Equal 3 @($plain | Where-Object { $_.Kind -eq 'refuse' }).Count 'without Force: both files and the summary'
    $p = @(Get-ActionPlan -Action 'commit.run' -Config $config -CommitFacts $f -CommitForce)
    Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'refuse' }).Count (@($p | ForEach-Object { $_.Kind + ' ' + $_.Text }) -join ' | ')
    Assert-Equal 'confirm' $p[0].Kind 'the phrase is still asked first'
    Assert-Equal 'commit shard work' $p[0].Phrase
    Assert-Equal 'This commits and pushes both repos, D:\ShatteredLegacy and D:\UO\shard-migration. FORCE is ticked: this skips the wait for files to be unchanged for 60 seconds, for code: CHANGELOG.md (changed 10 s ago), docs: notes/cc-P68-console-package-check.md (changed 25 s ago). Something may still be writing them.' $p[0].Text
    Assert-Equal 2 @($p | Where-Object { $_.Kind -eq 'say' -and $_.Text -like 'FORCE: not waiting on *' }).Count 'each file said in the plan too'
    Assert-Equal ('BEFORE YOU TYPE: ' + $script:CommitPublicWarning) (Get-ConfirmPreface $p) 'the public warning is unchanged'
    # Force never lifts a prompt that has not said DONE, or SYSTEM.
    $busy = New-CommitFacts -Changed $recent -Notes @([pscustomobject]@{ Name = 'cc-P65-shard-thing.md'; LastLine = 'still writing' })
    $bp = @(Get-ActionPlan -Action 'commit.run' -Config $config -CommitFacts $busy -CommitForce | Where-Object { $_.Kind -eq 'refuse' } | ForEach-Object { $_.Text })
    Assert-Equal 2 $bp.Count ($bp -join ' | ')
    Assert-True ($bp[0] -like 'P65 has not said DONE*') $bp[0]
    Assert-True ($bp[1] -like '*Force skips only the wait for recently changed files*') $bp[1]
    $sp = @(Get-ActionPlan -Action 'commit.run' -Config $config -CommitFacts (New-CommitFacts -System -Changed $recent) -CommitForce | Where-Object { $_.Kind -eq 'refuse' })
    Assert-True ($sp.Count -eq 1 -and $sp[0].Text -like 'This console is running as NT AUTHORITY\SYSTEM*') (@($sp | ForEach-Object { $_.Text }) -join ' | ')
    # The click: the dialog says it, the child is told -CommitForce, and the phrase still guards the run.
    $r = Invoke-ClickCapture 'commit.run' '' $false -CommitFacts $f -CommitForce
    Assert-Equal 'commit shard work' ($r.Confirms -join ',')
    Assert-True ($r.ConfirmTexts[0] -match 'FORCE is ticked: .*code: CHANGELOG\.md \(changed 10 s ago\)') $r.ConfirmTexts[0]
    Assert-True ($r.Spawned[0] -match ' -Mode Execute -Action commit\.run -ConfirmLive "commit shard work" -CommitForce$') $r.Spawned[0]
    Assert-Equal 0 (Invoke-ClickCapture 'commit.run' '' $false -CommitFacts $f).Spawned.Count 'unticked, the same state is refused'
    Assert-True (-not ((Invoke-ClickCapture 'commit.preview' '' $false -CommitFacts $f -CommitForce).Spawned[0] -match 'CommitForce')) 'the preview is never forced'
    Assert-Equal $false (Invoke-Plan -Plan $p -Config $config -ActionKey 'commit.run' -ShardKey 'repos' -ConfirmLive '' 6>$null) 'a forced plan still runs nothing without the phrase'
}

# --- cc-P73: Test shard > Wipe test accounts ------------------------------------------------------

function Get-WipePlanFor {
    param($Facts = $wipeFactsOk, [string]$Admin = 'newadmin', $State = $wipeState, $Config = $config)
    @(Get-ActionPlan -Action 'test.wipe' -Config $Config -State $State -WipeFacts $Facts -WipeAdmin $Admin -Now $now)
}

Fact 'P73_WipeIsATestShardWindowButtonWithItsOwnTypedPhrase' {
    $a = @(Get-ConsoleActions | Where-Object { $_.Key -eq 'test.wipe' })
    Assert-Equal 1 $a.Count
    Assert-Equal 'Test shard' $a[0].Group
    Assert-True ($a[0].Mutates -and $a[0].RunIn -eq 'window') 'it changes things and runs in its own window'
    Assert-Equal 'WIPE TEST ACCOUNTS' (Get-ConfirmPhrase 'test.wipe')
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'test.wipe') 'no -Mode: refused (D37)'
    $p = @(Get-WipePlanFor)
    Assert-Equal 'confirm' $p[0].Kind 'the phrase is the first step'
    Assert-Equal 'WIPE TEST ACCOUNTS' $p[0].Phrase
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count (@(Test-PlanSafety $p $config) -join ' | ')
}

Fact 'P73_ThePlanAsksThenStopsBacksUpPlacesStartsAndWaitsInThatOrderOnTestOnly' {
    $p = @(Get-WipePlanFor)
    Assert-Equal 'confirm,secret,exec,stopped,copy,verify,wiperequest,exec,wipewait,log' (@($p | ForEach-Object { $_.Kind }) -join ',')
    Assert-True ($p[2].Arguments -contains '-Down') 'the normal stop, which saves (D50)'
    Assert-Equal 'sl-modernuo-test' $p[3].Container
    Assert-Equal $config.Shards['test'].Saves $p[4].From
    Assert-Equal (New-AsideName $config.Shards['test'].Saves $now) $p[4].To 'the whole save, beside it, with the aside naming'
    Assert-Equal $config.WipeRequestFile $p[6].Path
    Assert-True ($p[6].Path -like '*\server\lib\uo\modernuo-test\account-wipe-request.json') $p[6].Path
    Assert-True ($p[7].Arguments -contains '-SkipBuild') 'starts the image already deployed'
    $all = (Format-Plan -Action 'test.wipe' -ShardKey 'test' -Plan $p) -join "`n"
    Assert-True (-not ($all -match 'sl-modernuo(\s|$|")')) 'never names the live container'
    Assert-True (-not ($all -match '\\modernuo\\Saves')) 'never names the live save'
}

Fact 'P73_WhatGoesIsShownWithTheShardsOwnCountsAndHowItDiffersFromStartFresh' {
    $t = (Get-WipePlanFor)[0].Text
    Assert-True ($t -match 'all 6 accounts and 11 characters') $t
    Assert-True ($t -match '2 houses') $t
    Assert-True ($t -match 'aetherion included') $t
    Assert-True ($t -match 'The world stays') $t
    Assert-True ($t -match "'newadmin'") $t
    Assert-True ($t -match 'This is not Start fresh: Start fresh replaces the whole test world .* LIVE accounts in; this keeps the world and copies nothing from live') $t
    Assert-True ((Get-ConfirmPreface (Get-WipePlanFor)) -like 'BEFORE YOU TYPE: this deletes every account and character on the TEST shard.*') (Get-ConfirmPreface (Get-WipePlanFor))
    # Start fresh is left as it was: it still moves the save aside and seeds from live.
    Assert-True ((Get-Plan 'test.fresh')[0].Text -match 'accounts and SerializedTypes.db from the live save') 'test.fresh unchanged'
    $form = New-ConsoleForm -Config $config
    try {
        $texts = New-Object 'System.Collections.Generic.List[string]'
        $walk = { param($c) $texts.Add([string]$c.Text); foreach ($x in $c.Controls) { & $walk $x } }
        & $walk $form
        $cap = @($texts | Where-Object { $_ -match 'START FRESH and WIPE TEST ACCOUNTS are different' })
        Assert-Equal 1 $cap.Count 'the Test shard tab says how the two buttons differ'
        Assert-True ($cap[0] -match 'NEW WORLD, with the LIVE accounts copied in' -and $cap[0] -match 'keeps the WORLD' -and $cap[0] -match 'copies nothing from live') $cap[0]
        Assert-True (@($texts | Where-Object { $_ -eq 'Wipe test accounts (keep world)...' }).Count -eq 1) 'the button'
        Assert-True (@($texts | Where-Object { $_ -eq 'Start fresh (new world, live accounts)' }).Count -eq 1) 'and Start fresh says what it does'
    } finally { $form.Dispose(); $script:OutputBox = $null }
}

Fact 'P73_RefusedAsSystemLikeTheCommitTabAndBeforeAnyDialog' {
    $p = @(Get-WipePlanFor -Facts (New-WipeFacts -System))
    Assert-Equal 'refuse' $p[0].Kind
    Assert-True ($p[0].Text -like 'This console is running as NT AUTHORITY\SYSTEM*own Windows login*') $p[0].Text
    Assert-Equal 0 @($p | Where-Object { $_.Kind -ne 'refuse' }).Count 'nothing else is planned'
    $r = Invoke-ClickCapture 'test.wipe' 'test' $false $wipeState -WipeFacts (New-WipeFacts -System)
    Assert-Equal 0 $r.WipeDialogs.Count 'no name dialog'
    Assert-Equal 0 $r.Confirms.Count 'no phrase dialog'
    Assert-Equal 0 $r.Spawned.Count 'no window'
    Assert-True (@($r.Lines | Where-Object { $_ -match 'SYSTEM' }).Count -eq 1) ($r.Lines -join ' | ')
}

Fact 'P73_RefusedWithoutTheShardsCountsWithARequestWaitingOrWithoutRoomForTheBackup' {
    $stopped = @(Get-WipePlanFor -State $state)
    Assert-True ($stopped[0].Kind -eq 'refuse' -and $stopped[0].Text -match 'not running') $stopped[0].Text
    $old = @(Get-WipePlanFor -Facts (New-WipeFacts -NoCensus))
    Assert-True ($old[0].Kind -eq 'refuse' -and $old[0].Text -match 'census.*Deploy built image') $old[0].Text
    $waiting = @(Get-WipePlanFor -Facts (New-WipeFacts -RequestExists))
    Assert-True ($waiting[0].Kind -eq 'refuse' -and $waiting[0].Text -match 'already waiting') $waiting[0].Text
    $full = @(Get-WipePlanFor -Facts (New-WipeFacts -FreeBytes 100MB))
    Assert-True ($full[0].Kind -eq 'refuse' -and $full[0].Text -match 'needs .* free') $full[0].Text
    $unknown = @(Get-WipePlanFor -Facts (New-WipeFacts -FreeBytes $null))
    Assert-True ($unknown[0].Kind -eq 'refuse' -and $unknown[0].Text -match 'free space') $unknown[0].Text
    foreach ($f in @((New-WipeFacts -NoCensus), (New-WipeFacts -RequestExists), (New-WipeFacts -FreeBytes 100MB))) {
        $r = Invoke-ClickCapture 'test.wipe' 'test' $false $wipeState -WipeFacts $f
        Assert-True ($r.WipeDialogs.Count -eq 0 -and $r.Spawned.Count -eq 0) ($r.Lines -join ' | ')
    }
}

Fact 'P73_TheNewAdminNameIsOneWordTheShardAccepts' {
    foreach ($ok in 'aetherion', 'admin', 'Chase.Admin', 'a', 'x_y-z9') { Assert-Equal $null (Test-WipeAdminName $ok) $ok }
    foreach ($bad in '', ' admin', 'ad min', 'admin.', 'a/b', 'a"b', '-admin', ('a' * 31)) { Assert-True ([bool](Test-WipeAdminName $bad)) ("'" + $bad + "'") }
    $p = @(Get-WipePlanFor -Admin 'bad name')
    Assert-True ($p.Count -eq 1 -and $p[0].Kind -eq 'refuse') ($p | ForEach-Object { $_.Text })
}

Fact 'P73_AClickAsksTheNameThenThePhraseAndHandsTheChildNoPassword' {
    $r = Invoke-ClickCapture 'test.wipe' 'test' $false $wipeState
    Assert-Equal 1 $r.WipeDialogs.Count 'the name dialog'
    Assert-True ($r.WipeDialogs[0] -match 'all 6 accounts and 11 characters') 'with the counts, before anything is asked'
    Assert-Equal 'WIPE TEST ACCOUNTS' ($r.Confirms -join ',') 'then the phrase'
    Assert-Equal 1 $r.Spawned.Count
    Assert-True ($r.Spawned[0] -match ' -Mode Execute -Action test\.wipe -Shard test -ConfirmLive "WIPE TEST ACCOUNTS" -WipeAdmin "newadmin"$') $r.Spawned[0]
    Assert-True (-not ($r.Spawned[0] -match 'Password|Pbkdf2|[0-9A-F]{40}')) 'no password or hash crosses the command line'
    $dry = Invoke-ClickCapture 'test.wipe' 'test' $true $wipeState $false
    Assert-True ($dry.Spawned[0] -match ' -Mode DryRun -Action test\.wipe ') $dry.Spawned[0]
}

Fact 'P73_CancelAtTheNameOrThePhraseOpensNothing' {
    $r = Invoke-ClickCapture 'test.wipe' 'test' $false $wipeState -WipeName ''
    Assert-Equal 0 $r.Confirms.Count 'no phrase asked'
    Assert-Equal 0 $r.Spawned.Count
    Assert-True (@($r.Lines | Where-Object { $_ -match 'cancelled. Nothing was changed' }).Count -eq 1) ($r.Lines -join ' | ')
    # The phrase dialog answered Cancel.
    $rec = [pscustomobject]@{ Spawned = New-Object 'System.Collections.Generic.List[string]' }
    function Start-Process { param($FilePath, $ArgumentList) $rec.Spawned.Add([string]$ArgumentList) }
    function Show-TypedConfirm { $false }
    function Get-ConsoleState { $wipeState }
    function Get-WipeFacts { $wipeFactsOk }
    function Show-WipeAdminPicker { 'newadmin' }
    function Write-ConsoleLine { }
    $saved = @($script:dry, $script:cfg, $script:fromCapture)
    $script:dry = $false; $script:cfg = $config; $script:fromCapture = $false
    try { Invoke-ConsoleButton -ActionKey 'test.wipe' } finally { $script:dry, $script:cfg, $script:fromCapture = $saved }
    Assert-Equal 0 $rec.Spawned.Count 'Cancel at the phrase opens no window'
}

# A world folder in TEMP, laid out as the repo's, so a wipe plan can run for real against it with docker,
# the password prompt and the two Start-TestShard calls answered by stand-ins.
function New-WipeStand {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('sl-p73-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $saves = Join-Path $root 'server\lib\uo\modernuo-test\Saves'
    New-Item -ItemType Directory -Path (Join-Path $saves 'Accounts') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root 'scripts') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $saves 'Accounts\Accounts.bin'), 'accounts')
    [IO.File]::WriteAllText((Join-Path $saves 'SerializedTypes.db'), 'types')
    $cfg = Get-ConsoleConfig -RepoRoot $root
    [pscustomobject]@{ Root = $root; Config = $cfg; Plan = (Get-WipePlanFor -Config $cfg) }
}

function Invoke-WipeStand {
    # Runs the stand's plan. $OnStart is what the stand-in for Start-TestShard -SkipBuild does (the shard booting);
    # $Log is what docker logs then says. Returns what happened.
    param($Stand, [string]$Phrase = 'WIPE TEST ACCOUNTS', $Password = 'correct horse', [scriptblock]$OnStart = { 0 }, [string]$Log = '')
    $rec = [pscustomobject]@{ Ok = $null; Execs = New-Object 'System.Collections.Generic.List[string]'; Lines = New-Object 'System.Collections.Generic.List[string]'; Asked = 0; RequestAtStart = $null; Cancelled = $false }
    function Invoke-ExecStep { param($Exe, $Arguments, $OutFile, $Header) $rec.Execs.Add((@($Arguments) -join ' ')); if (@($Arguments) -contains '-SkipBuild') { if (Test-Path -LiteralPath $Stand.Config.WipeRequestFile) { $rec.RequestAtStart = [IO.File]::ReadAllText($Stand.Config.WipeRequestFile) }; return (& $OnStart) }; 0 }
    function Invoke-DockerRead { param($Arguments, $TimeoutMs) if (@($Arguments)[0] -eq 'logs') { [pscustomobject]@{ Out = $Log; Err = ''; Code = 0 } } else { [pscustomobject]@{ Out = 'false'; Err = ''; Code = 0 } } }
    function Read-WipePassword { param($Admin) $rec.Asked++; $Password }
    function Start-Sleep { }
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $rec.Lines.Add($Text) }
    $Stand.Config.WipeWaitSeconds = 1
    $rec.Ok = Invoke-Plan -Plan $Stand.Plan -Config $Stand.Config -ActionKey 'test.wipe' -ShardKey 'test' -ConfirmLive $Phrase
    $rec.Cancelled = $script:PlanCancelled
    $rec
}

Fact 'P73_TheTypedPhraseIsExactAndAWrongOneRunsNothing' {
    $s = New-WipeStand
    try {
        foreach ($typed in 'wipe test accounts', 'WIPE TEST ACCOUNT', '', 'confirm live') {
            $r = Invoke-WipeStand $s -Phrase $typed
            Assert-Equal $false $r.Ok $typed
            Assert-Equal 0 $r.Asked ($typed + ': no password asked')
            Assert-Equal 0 $r.Execs.Count ($typed + ': nothing run')
        }
        Assert-Equal 0 @(Get-ChildItem -LiteralPath (Split-Path $s.Config.Shards['test'].Saves -Parent) -Filter 'Saves.aside-*').Count 'no backup made'
    } finally { Remove-Item -LiteralPath $s.Root -Recurse -Force }
}

Fact 'P73_CancelAtThePasswordLeavesEverythingAsItWas' {
    $s = New-WipeStand
    try {
        $r = Invoke-WipeStand $s -Password $null
        Assert-Equal $false $r.Ok
        Assert-True $r.Cancelled 'a cancel, exit 3, not a failure'
        Assert-Equal 1 $r.Asked
        Assert-Equal 0 $r.Execs.Count 'the shard was not stopped'
        Assert-True (-not (Test-Path -LiteralPath $s.Config.WipeRequestFile)) 'no request'
        Assert-Equal 0 @(Get-ChildItem -LiteralPath (Split-Path $s.Config.Shards['test'].Saves -Parent) -Filter 'Saves.aside-*').Count 'no backup'
        Assert-True (@($r.Lines | Where-Object { $_ -match 'cancelled at the password. Nothing was changed' }).Count -eq 1) ($r.Lines -join ' | ')
    } finally { Remove-Item -LiteralPath $s.Root -Recurse -Force }
}

Fact 'P73_ARunBacksUpWritesOnlyTheHashAndEndsOnTheShardsDoneLine' {
    $s = New-WipeStand
    $req = $s.Config.WipeRequestFile
    # The stand-in shard: reads the request and removes it, as TestAccountWipe.cs does once its save is published.
    $start = { Remove-Item -LiteralPath $req; 0 }.GetNewClosure()
    $log = "2026-10-12T15:00:01Z [AccountWipe] census: 6 accounts, 11 characters, 2 houses`n" +
           "2026-10-12T15:01:30Z [AccountWipe] DONE: request x: deleted 6 accounts, 11 characters, 900 items, 2 houses, 3 pets, 0 boats, 1 vendor, 0 guilds; kept 0 houses with no owner; admin 'newadmin' made (Owner); world saved; request removed`n" +
           "2026-10-12T15:01:30Z [AccountWipe] census: 1 account, 0 characters, 0 houses"
    try {
        $r = Invoke-WipeStand $s -OnStart $start -Log $log
        Assert-Equal $true $r.Ok ($r.Lines -join ' | ')
        Assert-Equal '-NoProfile -ExecutionPolicy Bypass -File ' ($r.Execs[0] -replace '\S+Start-TestShard\.ps1 -Down$', '') 'stopped first'
        $aside = @(Get-ChildItem -LiteralPath (Split-Path $s.Config.Shards['test'].Saves -Parent) -Directory -Filter 'Saves.aside-*')
        Assert-Equal 1 $aside.Count 'the backup is there'
        Assert-True (Test-Path -LiteralPath (Join-Path $aside[0].FullName 'Accounts\Accounts.bin')) 'with the accounts'
        $j = $r.RequestAtStart | ConvertFrom-Json
        Assert-Equal 'newadmin' $j.admin
        Assert-Equal 84 $j.passwordPbkdf2.Length
        Assert-True ($j.passwordPbkdf2 -cmatch '^[0-9A-F]{84}$') 'upper-case hex'
        Assert-True (-not ($r.RequestAtStart -match 'correct horse')) 'the password itself is never written'
        $salt = [byte[]](0..7 | ForEach-Object { [Convert]::ToByte($j.passwordPbkdf2.Substring(4 + 2 * $_, 2), 16) })
        $it = [Convert]::ToInt32($j.passwordPbkdf2.Substring(2, 2) + $j.passwordPbkdf2.Substring(0, 2), 16)
        Assert-Equal $j.passwordPbkdf2 (New-WipePasswordHash 'correct horse' -Salt $salt -Iterations $it) 'and it is the hash of the password typed'
        Assert-True (@($r.Lines | Where-Object { $_ -match '\[AccountWipe\] DONE: ' }).Count -eq 1) 'the shard''s counts are shown'
        Assert-True (@($r.Lines | Where-Object { $_ -match 'now has 1 account\(s\), 0 character\(s\)' }).Count -eq 1) ($r.Lines -join ' | ')
        Assert-True (-not (Test-Path -LiteralPath $req)) 'no request is left'
    } finally { Remove-Item -LiteralPath $s.Root -Recurse -Force }
}

Fact 'P73_ARefusalAFailedStartOrSilenceEndsInFailureAndNoRequestStaysOnDisk' {
    $cases = @(
        @{ Name = 'refused'; Start = { 0 }; Log = '2026-10-12T15:01:30Z [AccountWipe] REFUSED: SL_SHARD_ROLE is ''(unset)''. Nothing was wiped; the request was removed.'; Expect = 'REFUSED' },
        @{ Name = 'start failed'; Start = { 1 }; Log = ''; Expect = 'exited with 1' },
        @{ Name = 'no answer'; Start = { 0 }; Log = '2026-10-12T15:00:01Z [AccountWipe] census: 6 accounts, 11 characters, 2 houses'; Expect = 'said nothing about the wipe' }
    )
    foreach ($c in $cases) {
        $s = New-WipeStand
        try {
            $r = Invoke-WipeStand $s -OnStart $c.Start -Log $c.Log
            Assert-Equal $false $r.Ok $c.Name
            Assert-True ($null -ne $r.RequestAtStart) ($c.Name + ': the request was placed')
            Assert-True (-not (Test-Path -LiteralPath $s.Config.WipeRequestFile)) ($c.Name + ': and is not left on disk')
            Assert-True (@($r.Lines | Where-Object { $_ -match [regex]::Escape($c.Expect) }).Count -ge 1) ($c.Name + ': ' + ($r.Lines -join ' | '))
            Assert-Equal 1 @(Get-ChildItem -LiteralPath (Split-Path $s.Config.Shards['test'].Saves -Parent) -Directory -Filter 'Saves.aside-*').Count ($c.Name + ': the backup is kept')
        } finally { Remove-Item -LiteralPath $s.Root -Recurse -Force }
    }
}

Fact 'P73_TheSafetyCheckPlacesARequestOnlyInTheTestWorldAndOnlyAfterThePhrase' {
    $p = @(Get-WipePlanFor)
    $live = $config.Shards['live']
    $bad = @($p | ForEach-Object { $_ })
    $bad[6] = New-PlanStep -Kind wiperequest -Path (Join-Path (Split-Path $live.Saves -Parent) 'account-wipe-request.json') -Named ([ordered]@{ Admin = 'x' })
    $v = @(Test-PlanSafety $bad $config)
    Assert-True (@($v | Where-Object { $_ -match 'never placed in the live world folder' }).Count -eq 1) ($v -join ' | ')
    $nophrase = @($p | Where-Object { $_.Kind -ne 'confirm' })
    Assert-True (@(Test-PlanSafety $nophrase $config | Where-Object { $_ -match 'only after the typed phrase' }).Count -ge 1) 'no phrase, no wipe'
    $other = @($p | ForEach-Object { $_ })
    $other[0] = New-PlanStep -Kind confirm -Phrase 'deploy to test' -Text 'x'
    Assert-True (@(Test-PlanSafety $other $config | Where-Object { $_ -match 'only after the typed phrase' }).Count -ge 1) 'another action''s phrase does not count'
    $w = @($p | ForEach-Object { $_ })
    $w[8] = New-PlanStep -Kind wipewait -Container $live.Container -Path $config.WipeRequestFile
    Assert-True (@(Test-PlanSafety $w $config | Where-Object { $_ -match 'names the live container|waited for only on' }).Count -ge 1) 'never waits on live'
}

Fact 'P73_TheHashIsPinnedsPbkdf2FormatAndMatchesAnotherImplementation' {
    # Made with Python's hashlib.pbkdf2_hmac, a separate implementation; server/tests TestAccountWipeVerification checks
    # the same two strings verify in pinned's PBKDF2PasswordProtection.
    $salt = [byte[]](1..8)
    Assert-Equal 'B0040102030405060708AD8E61DCEDE035E57B71FBFF78B61BAA4D7A54B257FDB5A7D4DF9BEA21B9B45F' (New-WipePasswordHash 'correct horse' -Salt $salt -Iterations 1200)
    Assert-Equal '00060102030405060708C791FCAC584D61D7399B74241102E40492EA3EFBA683594D1F269F400DCC2564' (New-WipePasswordHash ('p' + [char]0xE4 + 'ss w' + [char]0xF6 + 'rd ' + [char]0x20AC) -Salt $salt -Iterations 1536)
    $a = New-WipePasswordHash 'same'; $b = New-WipePasswordHash 'same'
    Assert-True ($a -ne $b) 'a fresh salt each time'
    $it = [Convert]::ToInt32($a.Substring(2, 2) + $a.Substring(0, 2), 16)
    Assert-True ($it -ge 1024 -and $it -le 1536) ('iterations ' + $it)
    $t = New-WipeRequestText -Admin 'newadmin' -Hash $a -Id 'id-1' -Now $now -By 'HOUSE\chase' | ConvertFrom-Json
    Assert-Equal 1 $t.version
    Assert-Equal $a $t.passwordPbkdf2
}

Fact 'P73_TheShardsLinesAreReadWithOrWithoutTimestamps' {
    $x = ConvertFrom-WipeLog ("[AccountWipe] census: 1 account, 0 characters, 1 house`nnoise`n12:00:01 [AccountWipe] REFUSED: no.")
    Assert-Equal 1 $x.Census.Accounts
    Assert-Equal 1 $x.Census.Houses
    Assert-Equal 'refused' $x.Outcome
    Assert-Equal '[AccountWipe] REFUSED: no.' $x.Line
    $y = ConvertFrom-WipeLog $wipeCensusLog
    Assert-Equal 11 $y.Census.Characters
    Assert-True ($null -ne $y.Census.At) 'the time it was said'
    Assert-Equal $null $y.Outcome 'a census is not an outcome'
    Assert-Equal 'already' (ConvertFrom-WipeLog '[AccountWipe] ALREADY DONE: request x').Outcome
    Assert-Equal 'failed' (ConvertFrom-WipeLog '[AccountWipe] FAILED: the wipe threw').Outcome
}

# --- cc-P74: the AUTOPILOT tile and the Autopilot tab ---------------------------------------------
# autopilot-log-2026-10-12.txt and autopilot-state-running-2026-10-12.json are copies of the real
# D:\UO\cc-autopilot\autopilot.log and state.json from the moment the hook handed CC this prompt.
# The other autopilot-log-*.txt are built line by line from the hook's own format strings
# (cc-autopilot.ps1:51 for the line, :79 and :81 for mail, :139 handed, :181 STOPPED, :204 DONE,
# :214 commit exit, :229 stopped without DONE), with the real output of Commit-ShardWork.ps1 -DryRun
# (2026-10-12) pasted in where the hook pastes a commit's output. Nothing here reads the real folder.

function Read-ApLog([string]$Name) { @(Get-Content -LiteralPath (Join-Path $fixture $Name)) }
function Read-ApText([string]$Name) { [IO.File]::ReadAllText((Join-Path $fixture $Name)) }
$apRunning = Read-ApText 'autopilot-state-running-2026-10-12.json'
$apStarted = [datetime]::new(2026, 10, 12, 15, 49, 41)
$apIdleAfterStop = '{ "current": "", "currentFile": "", "lastHandled": 74, "startedAt": "2026-10-12T15:49:41" }'

function Get-ApTile {
    param($Enabled = $true, $StateText, [string]$Log, [string[]]$Queue = @(), [string[]]$Errors, [int]$Minutes = 42)
    $lines = @()
    if ($Log) { $lines = Read-ApLog $Log }
    Get-AutopilotGlance -Enabled $Enabled -StateText $StateText -LogLines $lines -QueueNames $Queue -Errors $Errors -Now $apStarted.AddMinutes($Minutes)
}

Fact 'P74_OffIsGreyAndSaysTheQueue' {
    $t = Get-ApTile -Enabled $false -StateText $apRunning -Log 'autopilot-log-2026-10-12.txt' -Queue @('cc-P83-play-test-queue.md')
    Assert-Equal 'OFF' $t.Word
    Assert-Equal 'gray' $t.Color
    Assert-Equal 'not handing out prompts; 1 queued' $t.Detail
}

Fact 'P74_RunningIsGreenWithThePromptItsAgeAndTheQueue' {
    # The real state.json and log at hand-off, with P83 queued behind it.
    $t = Get-ApTile -StateText $apRunning -Log 'autopilot-log-2026-10-12.txt' -Queue @('cc-P83-play-test-queue.md', 'cc-P90-later.md')
    Assert-Equal 'RUNNING' $t.Word
    Assert-Equal 'green' $t.Color $t.Detail
    Assert-Equal 'P74 for 42m 0s, 2 queued' $t.Detail
}

Fact 'P74_IdleSaysGoOnlyWhenSomethingIsQueued' {
    # No state.json is the hook's own default (Get-State): nothing running, nothing handled yet.
    $t = Get-ApTile -StateText $null -Log 'autopilot-log-2026-10-12.txt' -Queue @('cc-P75-next.md')
    Assert-Equal 'IDLE' $t.Word
    Assert-Equal 'gray' $t.Color
    Assert-Equal '1 queued; send CC "go" to start' $t.Detail
    $e = Get-ApTile -StateText $null -Queue @()
    Assert-Equal 'IDLE' $e.Word
    Assert-Equal 'queue empty' $e.Detail
}

Fact 'P74_AnUnnumberedFileIsNamedAsIgnored' {
    # Exactly what happened on 2026-10-12 15:30: the draft was in the queue before the overseer numbered it.
    $t = Get-ApTile -StateText $null -Log 'autopilot-log-2026-10-12.txt' -Queue @('cc-P-autopilot-console.md')
    Assert-Equal 'queue empty; 1 unnumbered file(s) ignored' $t.Detail
    $v = Get-AutopilotView -Enabled $true -StateText $null -LogLines (Read-ApLog 'autopilot-log-2026-10-12.txt') -QueueNames @('cc-P-autopilot-console.md', 'cc-P74-x.md')
    $panel = @(Format-AutopilotPanel -View $v -Config $config -Now $apStarted) | ForEach-Object { $_.Text }
    Assert-True (@($panel | Where-Object { $_ -match 'IGNORED cc-P-autopilot-console\.md \(unnumbered' }).Count -eq 1) ($panel -join ' | ')
    Assert-True (@($panel | Where-Object { $_ -match '1\. cc-P74-x\.md' }).Count -eq 1) 'the numbered one is in run order'
}

Fact 'P74_TheQueueRunsInNumberOrderNotNameOrder' {
    $q = Get-AutopilotQueue @('cc-P10-b.md', 'cc-P9-a.md', 'notes.txt', 'cc-P100-c.md', 'readme.md', 'cc-P11-d.md.txt')
    Assert-Equal 'cc-P9-a.md,cc-P10-b.md,cc-P100-c.md' ($q.Runs -join ',') 'the hook sorts by the number'
    Assert-Equal 'readme.md' ($q.Ignored -join ',') 'only .md files are logged as ignored; other files are not prompts'
}

Fact 'P74_NeedsYouWhenCcStoppedMidTask' {
    $t = Get-ApTile -StateText $apRunning -Log 'autopilot-log-needs-you.txt' -Queue @('cc-P83-play-test-queue.md')
    Assert-Equal 'NEEDS YOU' $t.Word
    Assert-Equal 'amber' $t.Color
    Assert-Equal 'NEEDS YOU: P74 stopped mid-task' $t.Detail
}

Fact 'P74_NeedsYouAfterStoppedAndCommitFailed' {
    # Both clear current in state.json, so without the log the tile would say IDLE.
    $s = Get-ApTile -StateText $apIdleAfterStop -Log 'autopilot-log-stopped.txt' -Queue @('cc-P83-play-test-queue.md')
    Assert-Equal 'NEEDS YOU' $s.Word
    Assert-Equal 'P74 STOPPED, needs you' $s.Detail
    $c = Get-ApTile -StateText $apIdleAfterStop -Log 'autopilot-log-commit-failed.txt' -Queue @('cc-P83-play-test-queue.md')
    Assert-Equal 'NEEDS YOU' $c.Word
    Assert-Equal 'amber' $c.Color
    Assert-Equal 'P74 done but COMMIT FAILED, needs you' $c.Detail
}

Fact 'P74_AFailedEmailTurnsTheTileAmber' {
    $t = Get-ApTile -StateText $apRunning -Log 'autopilot-log-email-failed.txt'
    Assert-Equal 'RUNNING' $t.Word 'the newest mail that went is not an alert'
    Assert-Equal 'amber' $t.Color
    Assert-True ($t.Detail.EndsWith('; email failing')) $t.Detail
}

Fact 'P74_CommitOutputIsSkippedAndAHandOffClearsAnOldAlert' {
    $lines = Read-ApLog 'autopilot-log-commit-output.txt'
    $acts = @(ConvertFrom-AutopilotLog $lines)
    Assert-Equal 4 $acts.Count ('the pasted Commit-ShardWork.ps1 output is not actions: ' + (($acts | ForEach-Object { $_.Text }) -join ' | '))
    Assert-True ($lines.Count -gt 10) 'the fixture does carry the pasted output'
    Assert-Equal 'Notification' $acts[1].Event
    $st = '{ "current": "P83", "currentFile": "cc-P83-play-test-queue.md", "lastHandled": 74, "startedAt": "2026-10-12T17:02:31" }'
    $t = Get-AutopilotGlance -Enabled $true -StateText $st -LogLines $lines -QueueNames @() -Now ([datetime]::new(2026, 10, 12, 17, 12, 31))
    Assert-Equal 'RUNNING' $t.Word 'the NEEDS YOU before the hand-off was answered'
    Assert-Equal 'P83 for 10m 0s, queue empty' $t.Detail
    $v = Get-AutopilotView -Enabled $true -StateText $st -LogLines $lines -QueueNames @()
    $log = @(Format-AutopilotPanel -View $v -Config $config) | Where-Object { $_.Text -match '^\d{4}-' }
    Assert-Equal 4 @($log).Count 'the panel lists actions only'
}

Fact 'P74_AnythingUnreadableIsGreyUnknownNeverRed' {
    $bad = Read-ApText 'autopilot-state-unreadable.json'
    $cases = @(
        @{ Name = 'unreadable state.json'; Tile = (Get-ApTile -StateText $bad -Log 'autopilot-log-2026-10-12.txt') },
        @{ Name = 'empty state.json'; Tile = (Get-ApTile -StateText '' -Log 'autopilot-log-2026-10-12.txt') },
        @{ Name = 'ENABLED unknown'; Tile = (Get-ApTile -Enabled $null -StateText $apRunning -Errors @('ENABLED: access denied')) },
        @{ Name = 'queue unlistable'; Tile = (Get-AutopilotGlance -Enabled $true -StateText $apRunning -LogLines @() -QueueNames $null -Errors @('queue: not found')) },
        @{ Name = 'log unreadable'; Tile = (Get-ApTile -StateText $apRunning -Errors @('autopilot.log: in use')) })
    foreach ($c in $cases) {
        Assert-Equal 'UNKNOWN' $c.Tile.Word ($c.Name + ': ' + $c.Tile.Detail)
        Assert-Equal 'gray' $c.Tile.Color $c.Name
    }
    Assert-True ($cases[0].Tile.Detail -match 'state\.json unreadable') $cases[0].Tile.Detail
}

Fact 'P74_AnEmptyLogIsNotAProblem' {
    $t = Get-AutopilotGlance -Enabled $true -StateText $apRunning -LogLines @() -QueueNames @() -Now $apStarted.AddMinutes(5)
    Assert-Equal 'RUNNING' $t.Word
    Assert-Equal 'green' $t.Color
    $v = Get-AutopilotView -Enabled $true -StateText $null -LogLines @() -QueueNames @()
    $panel = @(Format-AutopilotPanel -View $v -Config $config) | ForEach-Object { $_.Text }
    Assert-True ($panel -contains '(nothing logged)') ($panel -join ' | ')
    Assert-True ($panel -contains 'last      : none yet') 'lastHandled 0'
}

Fact 'P74_TheStatusReportListsTheAutopilotTileAfterTheThree' {
    $t = Get-ApTile -StateText $apRunning -Log 'autopilot-log-needs-you.txt'
    $lines = @(Format-StatusReport $state $config @($t))
    Assert-True ($lines[4].Text -match '^GOLEM\s+: NEEDS YOU\s+NEEDS YOU: P74 stopped mid-task$') $lines[4].Text
    Assert-Equal 'amber' $lines[4].Color
    Assert-Equal 'STATUS PUBLISHER' (($lines[3].Text -split ':')[0].Trim()) 'after the three'
    Assert-Equal (@(Format-StatusReport $state $config).Count + 1) $lines.Count 'one line more, nothing else changes'
}

$apFactsOn = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; Present = @{} }
foreach ($f in @($config.Autopilot.Start, $config.Autopilot.Stop, $config.Autopilot.Setup, $config.Autopilot.TestEmail)) { $apFactsOn.Present[$f] = $true }

Fact 'P74_TurnOnIsRefusedUntilEmailIsSetUp' {
    $no = [pscustomobject]@{ EmailConfigured = $false; CredStored = $false; LogExists = $true; Present = $apFactsOn.Present }
    $p = @(Get-ActionPlan -Action 'autopilot.on' -Config $config -AutopilotFacts $no)
    Assert-Equal 1 $p.Count
    Assert-Equal 'refuse' $p[0].Kind
    Assert-True ($p[0].Text.StartsWith('Run Golem email setup first')) $p[0].Text
    $t = @(Get-ActionPlan -Action 'autopilot.test' -Config $config -AutopilotFacts $no)
    Assert-Equal 'refuse' $t[0].Kind 'nothing to send with'
    $cfgOnly = [pscustomobject]@{ EmailConfigured = $true; CredStored = $false; LogExists = $true; Present = $apFactsOn.Present }
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'autopilot.test' -Config $config -AutopilotFacts $cfgOnly))[0].Kind 'no password stored'
}

Fact 'P74_EachButtonRunsItsOwnScriptInAWindowOfItsOwn' {
    $want = @{ 'autopilot.on' = $config.Autopilot.Start; 'autopilot.off' = $config.Autopilot.Stop; 'autopilot.email' = $config.Autopilot.Setup; 'autopilot.test' = $config.Autopilot.TestEmail }
    foreach ($k in $want.Keys) {
        $p = @(Get-ActionPlan -Action $k -Config $config -AutopilotFacts $apFactsOn)
        $w = @($p | Where-Object { $_.Kind -eq 'window' })
        Assert-Equal 1 $w.Count $k
        Assert-Equal ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File ' + $want[$k]) (Format-CommandLine $w[0].Exe $w[0].Arguments) $k
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ($k + ': ' + (@(Test-PlanSafety $p $config) -join '; '))
        Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('exec', 'script', 'confirm') }).Count ($k + ': nothing runs in the console itself')
        Assert-Equal 1 @($p | Where-Object { $_.Kind -eq 'log' }).Count ($k + ': logged')
    }
    $off = (@(Get-ActionPlan -Action 'autopilot.off' -Config $config -AutopilotFacts $apFactsOn) | Where-Object { $_.Kind -eq 'say' })[0].Text
    Assert-True ($off -match 'still finishes') $off
    $q = @(Get-ActionPlan -Action 'autopilot.queue' -Config $config -AutopilotFacts $apFactsOn)
    Assert-Equal 'explorer.exe' $q[0].Path
    Assert-Equal $config.Autopilot.Queue $q[0].Arguments[0]
    $l = @(Get-ActionPlan -Action 'autopilot.log' -Config $config -AutopilotFacts $apFactsOn)
    Assert-Equal $config.Autopilot.Notepad $l[0].Path
    Assert-Equal $config.Autopilot.LogFile $l[0].Arguments[0]
    Assert-Equal 0 @(Test-PlanSafety $l $config).Count 'Notepad on the log'
    $none = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $false; Present = @{} }
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'autopilot.log' -Config $config -AutopilotFacts $none))[0].Kind 'no log yet'
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'autopilot.off' -Config $config -AutopilotFacts $none))[0].Kind 'a missing script is named, not run'
}

Fact 'P74_TheSafetyCheckNeverRunsTheHookOrAnythingElseInItsFolder' {
    $ap = $config.Autopilot
    $hook = Join-Path $ap.Root 'cc-autopilot.ps1'
    foreach ($bad in @(
        @{ Name = 'the hook, in a window'; Step = (New-PlanStep -Kind window -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $hook)) },
        @{ Name = 'the hook, exec'; Step = (New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-File', $hook, '-Event', 'Stop')) },
        @{ Name = 'a start script run in the console'; Step = (New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ap.Start)) },
        @{ Name = 'another file in its folder'; Step = (New-PlanStep -Kind window -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $ap.Root 'Other.ps1'))) },
        @{ Name = 'opening the credential'; Step = (New-PlanStep -Kind launch -Path $ap.Notepad -Arguments @($ap.CredFile)) },
        @{ Name = 'Notepad on anything else'; Step = (New-PlanStep -Kind launch -Path $ap.Notepad -Arguments @('C:\Windows\win.ini')) })) {
        Assert-True (@(Test-PlanSafety @($bad.Step) $config).Count -ge 1) $bad.Name
    }
}

Fact 'P74_TurningOnOrOffNeedsAModeHeadless' {
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'autopilot.on') 'D37: a lost mode fails safe'
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'autopilot.test') 'sending mail changes something outside'
    Assert-Equal 'execute' (Resolve-RunMode -Action 'autopilot.log') 'only opens'
    $b = Get-ModeBanner -RunMode 'execute' -Action 'autopilot.on' -ShardKey 'autopilot'
    # cc-P99: autopilot.on is an alias, and the banner names what it resolved to.
    Assert-True ($b.Title.StartsWith('GOLEM - golem.on (golem)')) $b.Title
    Assert-True (-not (($b.Lines -join ' ') -match 'shard\.$')) 'it does not say it acts on a shard'
}

Fact 'P74_TheFactsTailTheLogAndOnlyCheckTheCredentialExists' {
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p74-' + [guid]::NewGuid().ToString('N'))
    $q = Join-Path $dir 'queue'
    New-Item -ItemType Directory -Path $q -Force | Out-Null
    try {
        $cfg = Get-ConsoleConfig -RepoRoot $repo
        $cfg.Autopilot = New-AutopilotConfig (Join-Path $dir 'ap') $q
        $cfg.Autopilot.TailLines = 50
        $f = Get-AutopilotFacts $cfg
        Assert-Equal $false $f.Enabled 'no folder: off, not an error'
        Assert-Equal $null $f.StateText
        Assert-Equal 0 @($f.Errors).Count (@($f.Errors) -join '; ')
        New-Item -ItemType Directory -Path $cfg.Autopilot.Root -Force | Out-Null
        Set-Content -LiteralPath $cfg.Autopilot.Enabled -Value '' -Encoding ASCII
        Set-Content -LiteralPath $cfg.Autopilot.StateFile -Value $apRunning -Encoding ASCII
        Set-Content -LiteralPath $cfg.Autopilot.CredFile -Value 'not a clixml: opening it would throw' -Encoding ASCII
        Set-Content -LiteralPath $cfg.Autopilot.EmailFile -Value '{}' -Encoding ASCII
        $many = @(1..300 | ForEach-Object { '2026-10-12 15:{0:00}:{1:00}  Stop         idle stop, queue empty' -f [int][Math]::Floor($_ / 60), ($_ % 60) })
        Set-Content -LiteralPath $cfg.Autopilot.LogFile -Value $many -Encoding ASCII
        Set-Content -LiteralPath (Join-Path $q 'cc-P83-play-test-queue.md') -Value '# x' -Encoding ASCII
        Set-Content -LiteralPath (Join-Path $q 'cc-P-draft.md') -Value '# x' -Encoding ASCII
        $f = Get-AutopilotFacts $cfg
        Assert-Equal $true $f.Enabled
        Assert-Equal 50 @($f.LogLines).Count 'the tail, not the whole log'
        Assert-Equal $many[-1] @($f.LogLines)[-1] 'the newest lines'
        Assert-Equal 2 @($f.QueueNames).Count (@($f.QueueNames) -join ',')
        Assert-True (@($f.QueueNames) -contains 'cc-P-draft.md' -and @($f.QueueNames) -contains 'cc-P83-play-test-queue.md') 'both names, as they are'
        Assert-True ($f.EmailConfigured -and $f.CredStored) 'both exist'
        Assert-Equal 0 @($f.Errors).Count (@($f.Errors) -join '; ')
        $a = Get-AutopilotViewNow $cfg
        Assert-Equal 'RUNNING' $a.Tile.Word $a.Tile.Detail
        Assert-True ($a.Tile.Detail -match ', 1 queued; 1 unnumbered file\(s\) ignored$') $a.Tile.Detail
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P74_TheTileAndTheTabAreBuiltAndDrawn' {
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p74-form-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $dir 'ap') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $dir 'queue') -Force | Out-Null
    $script:NetProbeDisabled = $true
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    # cc-P116: a Golem root of its own with no ACTIVE file. Update-AutopilotView re-picks the active folder on every
    # refresh (cc-P99), and since the real cutover (D:\UO\golem\ACTIVE, 2026-10-14) the default root picked the real
    # Golem, so this fact read its live status (RUNNING) instead of the fixture.
    $cfg.Autopilot = New-AutopilotConfig (Join-Path $dir 'ap') (Join-Path $dir 'queue') -GolemRoot (Join-Path $dir 'golem')
    Set-Content -LiteralPath $cfg.Autopilot.Enabled -Value '' -Encoding ASCII
    Copy-Item -LiteralPath (Join-Path $fixture 'autopilot-log-needs-you.txt') -Destination $cfg.Autopilot.LogFile
    Set-Content -LiteralPath $cfg.Autopilot.StateFile -Value $apRunning -Encoding ASCII
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-True ($null -ne $script:ui.AutopilotTile) 'the fifth tile exists'
        Assert-Equal 'GOLEM: NEEDS YOU' $script:ui.AutopilotTile.Word.Text
        Assert-Equal (Get-TileColors 'amber')[0] $script:ui.AutopilotTile.Panel.BackColor 'amber tile'
        Assert-Equal 'Golem' $script:ui.AutopilotPage.Text
        Assert-Equal 'golem.on,golem.off,golem.email,golem.test,golem.queue,golem.log,golem.settings,golem.folder,golem.move' (($script:ui.AutopilotButtons | ForEach-Object { $_.Tag }) -join ',') 'cc-P94 adds Golem settings; cc-P99 adds the folder and the move'
        $apCap = @($script:ui.AutopilotPage.Controls | ForEach-Object { @($_.Controls) } | Where-Object { $_.Text -like 'The Golem hands CC*' })
        Assert-Equal 1 $apCap.Count 'the caption'
        Assert-True ($apCap[0].Text -match 'After maxPerRun prompts \(settings\.json\) the Golem rests and keeps the queue: start a new CC session before waking it\.') $apCap[0].Text
        Assert-True ($script:ui.Tabs.TabPages.Contains($script:ui.AutopilotPage)) 'the tab is in the tab control'
        Assert-True ($script:ui.AutopilotPanel.Text -match 'needs you : NEEDS YOU: P74 stopped mid-task') 'the panel is drawn'
        Assert-True ($script:ui.Status.Text -match 'GOLEM\s+: NEEDS YOU') 'the status panel lists it too'
        $cap = $script:ui.CommitPage.Controls[0].Controls[0].Text
        Assert-True ($cap -match 'While the Golem is awake it commits and pushes after each P<nn> DONE') $cap
        Remove-Item -LiteralPath $cfg.Autopilot.Enabled
        [void](Update-AutopilotView)
        Assert-Equal 'GOLEM: OFF' $script:ui.AutopilotTile.Word.Text 'redrawn from disk'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false; Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

# --- cc-P94: the AUTOPILOT tile and tab read status.json ------------------------------------------
# autopilot-status-running-2026-10-14.json is the real D:\UO\cc-autopilot\status.json as this prompt
# started (P94 handed, no task list yet, Bash running), and autopilot-settings-2026-10-14.json the real
# settings.json. Every other shape is that file with fields changed by Set-ApStatus, the way the hook's
# Write-Status (cc-autopilot.ps1:187) would write them. Times are set as minutes before $apsNow, because
# the hook writes startedAt and generatedAt in local time and the activity times in UTC: built from one
# clock, the facts hold in any time zone.

$apsReal     = Read-ApText 'autopilot-status-running-2026-10-14.json'
$apsSettings = Read-ApText 'autopilot-settings-2026-10-14.json'
$apsNow      = [datetime]::new(2026, 10, 14, 6, 40, 0)

function Set-ApStatus {
    param([hashtable]$Set = @{}, $Started, $Generated = 0, $Activity, $Tool)
    $d = $apsReal | ConvertFrom-Json
    $loc = { param($m) $apsNow.AddMinutes(-$m).ToString('s') }
    $utc = { param($m) $apsNow.AddMinutes(-$m).ToUniversalTime().ToString('s') }
    if ($null -ne $Started) { $d.startedAt = & $loc $Started }
    $d.generatedAt = & $loc $Generated
    if ($null -ne $Activity) { $d.lastActivityUtc = & $utc $Activity }
    $d.toolStartedUtc = $(if ($null -ne $Tool) { & $utc $Tool } else { '' })
    foreach ($k in $Set.Keys) { $d.$k = $Set[$k] }
    $d | ConvertTo-Json -Depth 4
}

# cc-P116: -Queue is the folder listing; $null (the default) is a folder that could not be listed, so the tile counts
# status.json's own queued, which is what these cc-P94 facts are about. The folder rule has facts of its own (P116_*).
function Get-ApsView([string]$Status, [string[]]$Log = @(), $Settings = $apsSettings, $Queue = $null) {
    Get-AutopilotView -Enabled $true -StateText $null -LogLines $Log -QueueNames $Queue -StatusText $Status -SettingsText $Settings
}
function Get-ApsTile([string]$Status, [string[]]$Log = @(), $Settings = $apsSettings) {
    Get-AutopilotGlance -View (Get-ApsView $Status $Log $Settings) -Now $apsNow
}

$apsSteps = @{ current = 'P92'; currentFile = 'cc-P92-port-list-target.md'; position = 2; total = 5; runDone = 1
    stepsDone = 3; stepsTotal = 6; stepNow = '  Writing the tile  '; queued = @('cc-P93-a.md', 'cc-P94-b.md', 'cc-P95-c.md') }

Fact 'P94_TheRealStatusJsonReadsAsSchemaOne' {
    $s = ConvertFrom-AutopilotStatus $apsReal
    Assert-Equal 'RUNNING' $s.State
    Assert-Equal 'P94' $s.Current
    Assert-Equal '1 of 10' ([string]$s.Position + ' of ' + $s.Total)
    Assert-Equal 9 @($s.Queued).Count
    Assert-Equal 'cc-P95-imbue-boot-fix.md' @($s.Queued)[0] 'in run order, as the hook lists them'
    Assert-Equal ([DateTimeKind]::Utc) $s.LastActivity.Kind 'lastActivityUtc is UTC'
    Assert-Equal ([datetime]::new(2026, 10, 14, 6, 23, 34)) $s.StartedAt 'startedAt is local, as written'
    Assert-Equal 6 $s.MaxPerRun
}

Fact 'P94_RunningWithStepsSaysPositionStepAndAgeOverTheStepAndLastActivity' {
    $t = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 14 -Activity 2)
    Assert-Equal 'RUNNING' $t.Word
    Assert-Equal 'green' $t.Color
    Assert-Equal ("P92  2 of 5  step 3/6  14m`nWriting the tile; last activity 2m ago") $t.Detail 'stepNow is trimmed'
}

Fact 'P94_RunningWithoutStepsOmitsTheStepPart' {
    # The real file: P94 just handed, CC has made no task list.
    $t = Get-ApsTile (Set-ApStatus -Started 14 -Activity 2)
    Assert-Equal 'RUNNING' $t.Word
    Assert-Equal ("P94  1 of 10  14m`nlast activity 2m ago") $t.Detail
    Assert-True ($t.Detail -notmatch '0/0') 'never step 0/0'
}

Fact 'P94_ARunningToolIsNamedWithHowLongItHasRun' {
    $t = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 30 -Activity 9 -Tool 9)
    Assert-Equal 'green' $t.Color '9 minutes is under stallMinutes 45'
    Assert-Equal ("P92  2 of 5  step 3/6  30m`nWriting the tile; running Bash for 9m") $t.Detail
}

Fact 'P94_QuietPastStallMinutesTurnsTheTileAmber' {
    $t = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 60 -Activity 52)
    Assert-Equal 'RUNNING' $t.Word 'still running: the watch task emails, the tile only shows it'
    Assert-Equal 'amber' $t.Color
    Assert-Equal ("P92  2 of 5  step 3/6  1h 0m`nWriting the tile; quiet for 52m") $t.Detail
    $tool = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 60 -Activity 52 -Tool 52)
    Assert-True ($tool.Detail.EndsWith('quiet for 52m (Bash still running)')) $tool.Detail
    $patient = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 60 -Activity 52) -Settings '{ "stallMinutes": 60 }'
    Assert-Equal 'green' $patient.Color 'stallMinutes comes from settings.json'
    Assert-True ($patient.Detail.EndsWith('last activity 52m ago')) $patient.Detail
}

Fact 'P94_NeedsYouIsAmberWithTheHooksOwnWords' {
    # cc-autopilot.ps1:501 writes this needsYou when CC stops without DONE.
    $t = Get-ApsTile (Set-ApStatus -Set @{ state = 'NEEDS YOU'; needsYou = 'P94 stopped mid-task (question?)' } -Started 5 -Activity 1)
    Assert-Equal 'NEEDS YOU' $t.Word
    Assert-Equal 'amber' $t.Color
    Assert-Equal 'P94 stopped mid-task (question?)' $t.Detail
}

Fact 'P94_AStaleStatusWhileRunningIsAmberAndSaysWhy' {
    $t = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 50 -Activity 41 -Generated 40)
    Assert-Equal 'RUNNING' $t.Word
    Assert-Equal 'amber' $t.Color 'amber, not red'
    Assert-True ($t.Detail.EndsWith("`nstatus 40 min old; watch task not running?")) $t.Detail
    $fresh = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 50 -Activity 2 -Generated 14)
    Assert-Equal 'green' $fresh.Color '14 minutes old is within the watch task''s 10 minute beat'
    $idle = Get-ApsTile (Set-ApStatus -Set @{ state = 'IDLE'; current = ''; position = 0 } -Generated 300)
    Assert-Equal 'gray' $idle.Color 'nothing rewrites status.json while idle, so its age means nothing then'
}

Fact 'P94_AMissingOrUnreadableStatusJsonFallsBackToTheOldReading' {
    $lines = Read-ApLog 'autopilot-log-2026-10-12.txt'
    $q = @('cc-P83-play-test-queue.md', 'cc-P90-later.md')
    foreach ($c in @(
        @{ Name = 'missing'; Text = $null; Note = '^no status\.json' },
        @{ Name = 'not JSON'; Text = '{ "schema": 1, "state": '; Note = '^status\.json unreadable' },
        @{ Name = 'another schema'; Text = '{ "schema": 2, "state": "RUNNING" }'; Note = 'schema 2, not 1' },
        @{ Name = 'an unknown state'; Text = '{ "schema": 1, "state": "BUSY" }'; Note = 'not one of OFF' })) {
        $v = Get-AutopilotView -Enabled $true -StateText $apRunning -LogLines $lines -QueueNames $q -StatusText $c.Text -SettingsText $apsSettings
        Assert-Equal $null $v.Status $c.Name
        Assert-True ($v.StatusNote -match $c.Note) ($c.Name + ': ' + $v.StatusNote)
        $t = Get-AutopilotGlance -View $v -Now $apStarted.AddMinutes(42)
        Assert-Equal 'RUNNING' $t.Word $c.Name
        Assert-Equal 'P74 for 42m 0s, 2 queued' $t.Detail ($c.Name + ': exactly the cc-P74 tile')
        Assert-Equal 0 @($v.Problems).Count ($c.Name + ': a fallback is not a problem')
    }
    $e = Get-AutopilotView -Enabled $true -StateText $apRunning -LogLines $lines -QueueNames $q -StatusError 'in use by another process'
    Assert-True ($e.StatusNote -match 'could not be read \(in use') $e.StatusNote
    $panel = @(Format-AutopilotPanel -View $e -Config $config -Now $apStarted) | ForEach-Object { $_.Text }
    Assert-True (@($panel | Where-Object { $_ -like 'status    : status.json could not be read*' }).Count -eq 1) ($panel -join ' | ')
}

Fact 'P94_QueuedWithOneItemAndWithNone' {
    $idle = @{ state = 'IDLE'; current = ''; currentFile = ''; position = 0; total = 1; queued = @('cc-P95-imbue-boot-fix.md') }
    $one = Get-ApsTile (Set-ApStatus -Set $idle)
    Assert-Equal 'IDLE' $one.Word
    Assert-Equal '1 queued; send CC "go" to start' $one.Detail
    $idle.queued = @(); $idle.total = 0
    $none = Get-ApsTile (Set-ApStatus -Set $idle)
    Assert-Equal 'queue empty' $none.Detail
    $idle.runDone = 3
    Assert-Equal 'queue empty; 3 done this run' (Get-ApsTile (Set-ApStatus -Set $idle)).Detail 'runDone when above 0'
    $off = Get-ApsTile (Set-ApStatus -Set @{ state = 'OFF'; enabled = $false; current = ''; position = 0; runDone = 2; queued = @('cc-P95-imbue-boot-fix.md') })
    Assert-Equal 'OFF' $off.Word
    Assert-Equal 'gray' $off.Color
    Assert-Equal 'not handing out prompts; 1 queued; 2 done this run' $off.Detail
    $lone = '{ "schema": 1, "state": "IDLE", "queued": "cc-P95-imbue-boot-fix.md" }'
    Assert-Equal 1 @((ConvertFrom-AutopilotStatus $lone).Queued).Count 'a one-item array written as a bare string still counts one'
    Assert-Equal 0 @((ConvertFrom-AutopilotStatus '{ "schema": 1, "state": "IDLE" }').Queued).Count 'no queued at all is none'
}

Fact 'P94_FailingEmailStillTurnsTheStatusTileAmber' {
    $t = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 14 -Activity 2) (Read-ApLog 'autopilot-log-email-failed.txt')
    Assert-Equal 'amber' $t.Color
    Assert-True ($t.Detail.EndsWith('last activity 2m ago; email failing')) $t.Detail
}

Fact 'P94_ThePanelSaysTheRunTheStepsTheActivityAndTheSettings' {
    $v = Get-ApsView (Set-ApStatus -Set $apsSteps -Started 14 -Activity 2)
    $p = @(Format-AutopilotPanel -View $v -Config $config -Now $apsNow) | ForEach-Object { $_.Text }
    $want = @(
        'GOLEM     : RUNNING. P92  2 of 5  step 3/6  14m',
        '            Writing the tile; last activity 2m ago',
        'run       : 2 of 5, cap 6, 1 done, started 2026-10-14 05:49:20',
        'steps     : 3 of 6 done, now: Writing the tile',
        'activity  : last activity 2m ago (Bash)',
        'settings  : progressEmails true, maxPerRun 6, stallMinutes 45 (settings.json)')
    foreach ($w in $want) { Assert-True ($p -contains $w) ('missing [' + $w + '] in: ' + ($p -join ' | ')) }
    Assert-True (@($p | Where-Object { $_ -like 'status    : D:\UO\cc-autopilot\status.json, written 2026-10-14 06:40:00 (0s ago)' }).Count -eq 1) ($p -join ' | ')
    $none = Get-ApsView (Set-ApStatus -Set @{ maxPerRun = 0 } -Started 1) -Settings $null
    $q = @(Format-AutopilotPanel -View $none -Config $config -Now $apsNow) | ForEach-Object { $_.Text }
    Assert-True ($q -contains 'steps     : none reported (CC''s task list is empty for this prompt)') ($q -join ' | ')
    Assert-True (@($q | Where-Object { $_ -like 'run       : 1 of 10, no cap,*' }).Count -eq 1) 'maxPerRun 0 is no cap'
    Assert-True ($q -contains 'settings  : progressEmails true, maxPerRun 6, stallMinutes 45 (no settings.json: the hook''s defaults)') ($q -join ' | ')
}

Fact 'P94_TheStatusReportIndentsTheTilesSecondLine' {
    $t = Get-ApsTile (Set-ApStatus -Set $apsSteps -Started 14 -Activity 2)
    $lines = @(Format-StatusReport $state $config @($t))
    Assert-True ($lines[4].Text -match '^GOLEM\s+: RUNNING\s+P92  2 of 5  step 3/6  14m$') $lines[4].Text
    Assert-Equal ((' ' * 35) + 'Writing the tile; last activity 2m ago') $lines[5].Text
    Assert-Equal 'green' $lines[5].Color
}

# --- cc-P99: the Golem ------------------------------------------------------------------------------
# Autopilot became the Golem (Chase, 2026-10-14). The labels, the tile, the tab and the window titles say Golem;
# autopilot.* keys are aliases of golem.*; the console reads D:\UO\golem once it is ACTIVE, else cc-autopilot.

Fact 'P99_TheButtonsHaveTheGolemsLabels' {
    $want = [ordered]@{ 'golem.on' = 'Wake the Golem'; 'golem.off' = 'Rest the Golem'; 'golem.email' = 'Golem email setup'; 'golem.test' = 'Send test email';
        'golem.queue' = 'Open the queue folder'; 'golem.log' = 'Golem log'; 'golem.settings' = 'Golem settings'; 'golem.folder' = 'Open Golem folder'; 'golem.move' = 'Move to the Golem' }
    $acts = @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem' })
    Assert-Equal (@($want.Keys) -join ',') (($acts | ForEach-Object { $_.Key }) -join ',') 'the Golem group, in order'
    foreach ($a in $acts) { Assert-Equal $want[$a.Key] $a.Label $a.Key }
    Assert-Equal 0 @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Autopilot' -or $_.Key -like 'autopilot.*' -or $_.Label -match '(?i)autopilot' }).Count 'no Autopilot button is left'
    $tips = Get-ConsoleTooltips
    foreach ($k in @($want.Keys) + @('tile.GOLEM', 'tab.Golem')) { Assert-True ($tips.Contains($k) -and $tips[$k]) ('a tooltip for ' + $k) }
    Assert-True (-not $tips.Contains('tile.AUTOPILOT') -and -not $tips.Contains('tab.Autopilot')) 'no old tooltip keys'
}

Fact 'P99_AutopilotKeysAreAliasesOfTheGolemsKeys' {
    foreach ($k in 'on', 'off', 'email', 'test', 'queue', 'log', 'settings', 'folder', 'move') { Assert-Equal ('golem.' + $k) (Resolve-ActionAlias ('autopilot.' + $k)) $k }
    Assert-Equal 'test.start' (Resolve-ActionAlias 'test.start') 'other keys are left alone'
    $apFacts = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; SettingsExists = $true; Present = @{}; Running = '' }
    foreach ($f in @($config.Autopilot.Start, $config.Autopilot.Stop, $config.Autopilot.Setup, $config.Autopilot.TestEmail)) { $apFacts.Present[$f] = $true }
    foreach ($k in 'on', 'off', 'email', 'test', 'queue', 'log', 'settings') {
        $old = @(Get-ActionPlan -Action ('autopilot.' + $k) -Config $config -AutopilotFacts $apFacts) | ForEach-Object { $_.Kind + ':' + $_.Text + ':' + (@($_.Arguments) -join ' ') }
        $new = @(Get-ActionPlan -Action ('golem.' + $k) -Config $config -AutopilotFacts $apFacts) | ForEach-Object { $_.Kind + ':' + $_.Text + ':' + (@($_.Arguments) -join ' ') }
        Assert-Equal ($new -join '|') ($old -join '|') ('autopilot.' + $k + ' plans exactly what golem.' + $k + ' does')
        Assert-True (@($old | Where-Object { $_ -like 'refuse:Unknown action*' }).Count -eq 0) ('autopilot.' + $k + ' is known')
    }
    Assert-Equal (Resolve-RunMode -Action 'golem.on') (Resolve-RunMode -Action 'autopilot.on') 'the same mode rule'
    Assert-Equal 'execute' (Resolve-RunMode -Action 'autopilot.folder') 'only opens'
}

Fact 'P99_WindowTitlesSayGolem' {
    foreach ($k in 'golem.on', 'autopilot.off', 'golem.move') {
        $b = Get-ModeBanner -RunMode 'execute' -Action $k -ShardKey 'golem'
        Assert-True ($b.Title -match '^GOLEM - golem\.\w+ \(golem\) - Shard Console$') $b.Title
        Assert-True (($b.Lines -join ' ') -match 'the Golem''s own script') ($b.Lines -join ' ')
    }
    $d = Get-ModeBanner -RunMode 'dry' -Action 'autopilot.log' -ShardKey 'golem'
    Assert-True ($d.Title -eq 'DRY RUN - golem.log (golem) - Shard Console') $d.Title
}

Fact 'P99_TheConsoleReadsTheGolemFirstAndFallsBackToTheAutopilot' {
    Assert-Equal 'golem' (Select-GolemKind -GolemStartPresent $true -GolemActive $true).Kind 'after the cutover'
    Assert-Equal 'autopilot' (Select-GolemKind -GolemStartPresent $true -GolemActive $false).Kind 'built, not moved to'
    Assert-Equal 'autopilot' (Select-GolemKind -GolemStartPresent $false -GolemActive $true).Kind 'an ACTIVE file with no Golem scripts is not trusted'
    Assert-Equal 'autopilot' (Select-GolemKind -GolemStartPresent $false -GolemActive $false).Kind 'no Golem at all'
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p99-' + [guid]::NewGuid().ToString('N'))
    $g = Join-Path $dir 'golem'; $a = Join-Path $dir 'cc-autopilot'; $q = Join-Path $dir 'queue'
    foreach ($d in $g, $a, $q) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    try {
        $c0 = Get-GolemConfig $g $a $q
        Assert-Equal 'autopilot' $c0.Kind 'nothing in the Golem folder'
        Assert-Equal (Join-Path $a 'Start-Autopilot.ps1') $c0.Start
        Assert-Equal (Join-Path $a 'autopilot.log') $c0.LogFile
        Set-Content -LiteralPath (Join-Path $g 'Start-Golem.ps1') -Value '' -Encoding ASCII
        $c1 = Get-GolemConfig $g $a $q
        Assert-Equal 'autopilot' $c1.Kind 'built but no ACTIVE'
        Assert-Equal (Join-Path $g 'Move-ToGolem.ps1') $c1.MoveScript 'the move is the Golem''s script even before'
        Set-Content -LiteralPath (Join-Path $g 'ACTIVE') -Value 'x' -Encoding ASCII
        $c2 = Get-GolemConfig $g $a $q
        Assert-Equal 'golem' $c2.Kind 'ACTIVE: the Golem'
        Assert-Equal $g $c2.Root
        Assert-Equal (Join-Path $g 'Start-Golem.ps1') $c2.Start
        Assert-Equal (Join-Path $g 'Stop-Golem.ps1') $c2.Stop
        Assert-Equal (Join-Path $g 'golem.log') $c2.LogFile
        Assert-Equal (Join-Path $g 'status.json') $c2.StatusFile
        Assert-Equal 'Golem Watch' $c2.WatchTask
        # The form re-reads it on every refresh: a cutover made while it is open shows at the next one.
        $cfg = Get-ConsoleConfig -RepoRoot $repo
        $cfg.Autopilot = $c1
        $script:cfg = $cfg
        $script:ui = @{}
        [void](Update-AutopilotView)
        Assert-Equal 'golem' $script:cfg.Autopilot.Kind 'Update-AutopilotView picked up ACTIVE'
        # The panel says which one is active, and the start phrase.
        $v = Get-AutopilotView -Enabled $false -StateText $null -LogLines @() -QueueNames @() -Kind 'golem'
        $pg = @(Format-AutopilotPanel -View $v -Config ([pscustomobject]@{ Autopilot = $c2 }) -Now (Get-Date)) | ForEach-Object { $_.Text }
        Assert-True ($pg -contains 'In a CC session, say: "Golem, please work on the shard tasks"') ($pg -join ' | ')
        Assert-True (@($pg | Where-Object { $_ -like ('active    : the Golem (' + $g + ')*') }).Count -eq 1) ($pg -join ' | ')
        Assert-True ($pg -contains '== golem.log, the last 20 actions (commit output skipped) ====================') 'the log is named by its own name'
        $va = Get-AutopilotView -Enabled $false -StateText $null -LogLines @() -QueueNames @()
        $pa = @(Format-AutopilotPanel -View $va -Config ([pscustomobject]@{ Autopilot = $c1 }) -Now (Get-Date)) | ForEach-Object { $_.Text }
        Assert-True (@($pa | Where-Object { $_ -like ('active    : cc-autopilot (' + $a + '), the Golem''s predecessor.*Move to the Golem*') }).Count -eq 1) ($pa -join ' | ')
        # And the buttons run the active one's scripts.
        $facts = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; SettingsExists = $true; Present = @{ $c2.Start = $true; $c1.Start = $true }; Running = '' }
        $pw = @(Get-ActionPlan -Action 'golem.on' -Config ([pscustomobject]@{ Autopilot = $c2; PowerShell = 'powershell.exe' }) -AutopilotFacts $facts | Where-Object { $_.Kind -eq 'window' })
        Assert-Equal (Join-Path $g 'Start-Golem.ps1') @($pw[0].Arguments)[4] 'the Golem''s Start-Golem.ps1'
        $pa2 = @(Get-ActionPlan -Action 'golem.on' -Config ([pscustomobject]@{ Autopilot = $c1; PowerShell = 'powershell.exe' }) -AutopilotFacts $facts | Where-Object { $_.Kind -eq 'window' })
        Assert-Equal (Join-Path $a 'Start-Autopilot.ps1') @($pa2[0].Arguments)[4] 'before the cutover, cc-autopilot''s'
    } finally { $script:ui = $null; Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P99_MoveToTheGolemAsksAPhraseAndRunsTheCutoverInAWindow' {
    $ap = $config.Autopilot
    $facts = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; SettingsExists = $true; Present = @{ $ap.MoveScript = $true }; Running = '' }
    $p = @(Get-ActionPlan -Action 'golem.move' -Config $config -AutopilotFacts $facts)
    Assert-Equal 'confirm,window,log' (($p | ForEach-Object { $_.Kind }) -join ',') ($p | ForEach-Object { $_.Kind + ' ' + $_.Text })
    Assert-Equal 'move to the golem' $p[0].Phrase
    Assert-Equal 'Move to the Golem' (Get-ConfirmSubject 'golem.move')
    Assert-Equal ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File ' + $ap.MoveScript) (Format-CommandLine $p[1].Exe $p[1].Arguments) 'no -Yes: the script shows its dry run and asks again'
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count (@(Test-PlanSafety $p $config) -join '; ')
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'golem.move') 'it changes something: refused headless without a mode'
    $busy = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; SettingsExists = $true; Present = @{ $ap.MoveScript = $true }; Running = 'P99' }
    $r = @(Get-ActionPlan -Action 'golem.move' -Config $config -AutopilotFacts $busy)
    Assert-True ($r.Count -eq 1 -and $r[0].Kind -eq 'refuse' -and $r[0].Text -match 'A prompt is running \(P99\)') ($r | ForEach-Object { $_.Text })
    $gone = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; SettingsExists = $true; Present = @{}; Running = '' }
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'golem.move' -Config $config -AutopilotFacts $gone))[0].Kind 'no script, no move'
    $active = [pscustomobject]@{ Autopilot = (New-AutopilotConfig 'D:\UO\golem' 'D:\UO\cc-prompts\queue' -Kind golem); PowerShell = 'powershell.exe' }
    $x = @(Get-ActionPlan -Action 'golem.move' -Config $active -AutopilotFacts $facts)
    Assert-True ($x[0].Kind -eq 'refuse' -and $x[0].Text -match 'already active.*-Rollback') $x[0].Text
}

Fact 'P99_TheSafetyCheckNeverRunsEitherHook' {
    $g = New-AutopilotConfig 'D:\UO\golem' 'D:\UO\cc-prompts\queue' -Kind golem
    $cfgG = Get-ConsoleConfig -RepoRoot $repo
    $cfgG.Autopilot = $g
    foreach ($hook in 'D:\UO\golem\golem.ps1', 'D:\UO\cc-autopilot\cc-autopilot.ps1') {
        foreach ($c in $cfgG, $config) {
            $st = New-PlanStep -Kind window -Exe $c.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $hook)
            Assert-True (@(Test-PlanSafety @($st) $c) -match 'hook is never run').Count ($hook + ' under ' + $c.Autopilot.Kind)
        }
    }
    $ok = New-PlanStep -Kind window -Exe $cfgG.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $g.Start)
    Assert-Equal 0 @(Test-PlanSafety @($ok) $cfgG).Count 'Start-Golem.ps1 in a window is fine'
    $dir = New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($g.Root)
    Assert-Equal 0 @(Test-PlanSafety @($dir) $cfgG).Count 'Explorer on the folder is fine'
    $other = New-PlanStep -Kind window -Exe $cfgG.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'D:\UO\golem\tests\Golem.Tests.ps1')
    Assert-True (@(Test-PlanSafety @($other) $cfgG).Count -ge 1) 'nothing else in its folder'
    $yes = New-PlanStep -Kind window -Exe $cfgG.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $g.MoveScript, '-Yes')
    Assert-True (@(Test-PlanSafety @($yes) $cfgG).Count -ge 1) 'the move never skips its own question'
}

Fact 'P99_TheTileSaysGolemAndShowsANote' {
    $j = (Set-ApStatus -Set $apsSteps -Started 14 -Activity 2) | ConvertFrom-Json
    $j | Add-Member -NotePropertyName note -NotePropertyValue 'waiting on background agents' -Force
    $v = Get-ApsView ($j | ConvertTo-Json -Depth 4)
    $t = Get-AutopilotGlance -View $v -Now $apsNow
    Assert-Equal 'GOLEM' $t.Title
    Assert-Equal 'RUNNING' $t.Word
    Assert-True ($t.Detail -match 'waiting on background agents') $t.Detail
    $p = @(Format-AutopilotPanel -View $v -Config $config -Now $apsNow) | ForEach-Object { $_.Text }
    Assert-True ($p -contains 'note      : waiting on background agents') ($p -join ' | ')
    $idle = Get-ApsView (Set-ApStatus -Set @{ state = 'IDLE'; current = ''; position = 0; queued = @('cc-P100-a.md') })
    $idle.Kind = 'golem'
    Assert-True ((Get-AutopilotGlance -View $idle -Now $apsNow).Detail -match 'say "Golem, work" in CC to start') 'idle, the Golem: the phrase'
    $old = New-Object 'System.Collections.Generic.List[string]'
    foreach ($l in @('2026-10-14 10:00:00  Stop         handed P100 (cc-P100-a.md) x', '2026-10-14 10:05:00  Stop         emailed: [Golem] NEEDS YOU: P100 stopped mid-task')) { $old.Add($l) }
    $vv = Get-AutopilotView -Enabled $true -StateText '{ "current": "P100", "currentFile": "cc-P100-a.md", "lastHandled": 99, "startedAt": "2026-10-14T10:00:00" }' -LogLines $old.ToArray() -QueueNames @()
    Assert-Equal 'NEEDS YOU: P100 stopped mid-task' $vv.Alert 'a [Golem] subject is read like a [Shard CC] one'
}

Fact 'P94_OpenSettingsOpensOnlySettingsJsonInNotepad' {
    $ap = $config.Autopilot
    $yes = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; SettingsExists = $true; Present = @{} }
    $p = @(Get-ActionPlan -Action 'autopilot.settings' -Config $config -AutopilotFacts $yes)
    $l = @($p | Where-Object { $_.Kind -eq 'launch' })
    Assert-Equal 1 $l.Count
    Assert-Equal $ap.Notepad $l[0].Path
    Assert-Equal $ap.SettingsFile @($l[0].Arguments)[0]
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count (@(Test-PlanSafety $p $config) -join '; ')
    Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('exec', 'script', 'confirm', 'window') }).Count 'it opens, nothing more'
    Assert-Equal 'execute' (Resolve-RunMode -Action 'autopilot.settings') 'only opens, so no mode is needed'
    $no = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; SettingsExists = $false; Present = @{} }
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'autopilot.settings' -Config $config -AutopilotFacts $no))[0].Kind 'no file: the hook uses its defaults'
    foreach ($bad in @($ap.StateFile, $ap.StatusFile, $ap.CredFile, (Join-Path $ap.Root 'email.json'))) {
        Assert-True (@(Test-PlanSafety @((New-PlanStep -Kind launch -Path $ap.Notepad -Arguments @($bad))) $config).Count -ge 1) ('Notepad on ' + $bad)
    }
}

Fact 'P94_TheFactsReadStatusAndSettingsAndTheTileUsesThem' {
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p94-' + [guid]::NewGuid().ToString('N'))
    $q = Join-Path $dir 'queue'
    New-Item -ItemType Directory -Path $q -Force | Out-Null
    try {
        $cfg = Get-ConsoleConfig -RepoRoot $repo
        $cfg.Autopilot = New-AutopilotConfig (Join-Path $dir 'ap') $q
        New-Item -ItemType Directory -Path $cfg.Autopilot.Root -Force | Out-Null
        Set-Content -LiteralPath $cfg.Autopilot.Enabled -Value '' -Encoding ASCII
        Set-Content -LiteralPath $cfg.Autopilot.StateFile -Value $apRunning -Encoding ASCII
        $f = Get-AutopilotFacts $cfg
        Assert-Equal $null $f.StatusText 'no status.json'
        Assert-Equal $false $f.SettingsExists
        Assert-Equal 'RUNNING' (Get-AutopilotViewNow $cfg).Tile.Word 'the fallback reading'
        $now = [datetime]::Now
        $d = $apsReal | ConvertFrom-Json
        $d.generatedAt = $now.ToString('s'); $d.startedAt = $now.AddMinutes(-3).AddSeconds(-5).ToString('s')
        $d.lastActivityUtc = $now.ToUniversalTime().ToString('s'); $d.toolStartedUtc = ''
        Set-Content -LiteralPath $cfg.Autopilot.StatusFile -Value ($d | ConvertTo-Json -Depth 4) -Encoding ASCII
        Set-Content -LiteralPath $cfg.Autopilot.SettingsFile -Value '{ "stallMinutes": 30 }' -Encoding ASCII
        $f = Get-AutopilotFacts $cfg
        Assert-True ($null -ne $f.StatusText) 'status.json read'
        Assert-True $f.SettingsExists 'settings.json seen'
        $a = Get-AutopilotViewNow $cfg
        Assert-Equal 'RUNNING' $a.Tile.Word
        Assert-True ($a.Tile.Detail -match '^P94  1 of 10  3m') $a.Tile.Detail
        Assert-Equal 30 $a.View.Settings.StallMinutes
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

# --- cc-P83: the PLAY TESTS tile and the two links --------------------------------------------------
# play-tests-2026-10-12.json is the real D:\UO\shard-migration\testing\play-tests.json as first imported
# (P56 to P74 and the 49 T-rows of docs/client-test-queue.md). The other shapes are that file, changed.

$ptReal = [IO.File]::ReadAllText((Join-Path $fixture 'play-tests-2026-10-12.json'))
function Set-PtStatus([string]$Text, [hashtable]$Status, [hashtable]$Bug) {
    $d = $Text | ConvertFrom-Json
    foreach ($i in $d.items) {
        if ($Status.ContainsKey($i.id)) { $i.status = $Status[$i.id] }
        if ($Bug -and $Bug.ContainsKey($i.id)) { $i.bug = $Bug[$i.id] }
    }
    $d | ConvertTo-Json -Depth 6
}

Fact 'P83_PlayTestsCountsTheRealBacklog' {
    $t = Get-PlayTestGlance $ptReal
    Assert-Equal 'PLAY TESTS' $t.Title
    Assert-Equal '111 OPEN' $t.Word $t.Detail
    Assert-Equal 'gray' $t.Color 'T-040 failed but is logged as D41'
    Assert-Equal '111 open, 1 failed, 18 passed' $t.Detail
}

Fact 'P83_AFailNotYetLoggedTurnsItAmber' {
    $t = Get-PlayTestGlance (Set-PtStatus $ptReal @{ 'P71-2' = 'fail' } @{})
    Assert-Equal 'amber' $t.Color
    Assert-Equal '1 FAIL NOT LOGGED' $t.Word
    Assert-Equal '110 open, 2 failed (1 not logged), 18 passed' $t.Detail
    $logged = Get-PlayTestGlance (Set-PtStatus $ptReal @{ 'P71-2' = 'fail' } @{ 'P71-2' = 'D86' })
    Assert-Equal 'gray' $logged.Color 'logged in the bug list: no longer amber'
}

Fact 'P83_LaterAndBlockedStillCountAsOpenAndNothingOpenIsGreen' {
    $st = @{}
    foreach ($i in ($ptReal | ConvertFrom-Json).items) { if ($i.status -in @('open', 'blocked', 'later')) { $st[$i.id] = 'pass' } }
    $st['P56-1'] = 'later'
    Assert-Equal '1 OPEN' (Get-PlayTestGlance (Set-PtStatus $ptReal $st @{})).Word 'Later is still waiting'
    $st['P56-1'] = 'pass'
    $g = Get-PlayTestGlance (Set-PtStatus $ptReal $st @{})
    Assert-Equal 'ALL TESTED' $g.Word
    Assert-Equal 'green' $g.Color
}

Fact 'P83_NoFileOrABadOneIsGreyUnknownNeverRed' {
    foreach ($c in @(
        @{ Name = 'no file'; Tile = (Get-PlayTestGlance $null) },
        @{ Name = 'not JSON'; Tile = (Get-PlayTestGlance '{ "schema": 1, "items": [') },
        @{ Name = 'another schema'; Tile = (Get-PlayTestGlance '{ "schema": 2, "items": [] }') },
        @{ Name = 'read failed'; Tile = (Get-PlayTestGlance $null -ReadError 'in use') })) {
        Assert-Equal 'UNKNOWN' $c.Tile.Word $c.Name
        Assert-Equal 'gray' $c.Tile.Color $c.Name
    }
    Assert-True ((Get-PlayTestGlance $null).Detail -match 'Import-PlayTests\.ps1') 'says how to make one'
}

Fact 'P90_NewIdeasAreAddedToThePlayTestsDetailNeverToItsWord' {
    $ideas = '{ "schema": 1, "nextId": 4, "ideas": [ { "id": "I-001", "status": "new" }, { "id": "I-002", "status": "taken" }, { "id": "I-003", "status": "new" } ] }'
    $plain = Get-PlayTestGlance $ptReal
    $with = Get-PlayTestGlance $ptReal -IdeasText $ideas
    Assert-Equal $plain.Word $with.Word 'the word is the play tests alone'
    Assert-Equal $plain.Color $with.Color
    Assert-Equal ($plain.Detail + '; 2 new ideas') $with.Detail
    Assert-Equal ($plain.Detail + '; 1 new idea') (Get-PlayTestGlance $ptReal -IdeasText '{ "schema": 1, "ideas": [ { "id": "I-009", "status": "new" } ] }').Detail
    foreach ($bad in @($null, '{ "schema": 1, "ideas": [', '{ "schema": 2, "ideas": [ { "status": "new" } ] }', '{ "schema": 1, "ideas": [] }')) {
        Assert-Equal $plain.Detail (Get-PlayTestGlance $ptReal -IdeasText $bad).Detail 'no ideas, no file or a bad one: nothing added'
    }
}

Fact 'P97_NewBugsAreAddedToThePlayTestsDetailBesideTheIdeas' {
    # cc-P97: bug-reports.json sits beside feature-ideas.json; its 'new' reports go in the detail, never the word.
    $bugs = '{ "schema": 1, "nextId": 3, "copiedAt": "", "bugs": [ { "id": "B-001", "status": "new" }, { "id": "B-002", "status": "taken" } ], "deleted": [] }'
    $oneIdea = '{ "schema": 1, "ideas": [ { "id": "I-009", "status": "new" } ] }'
    $plain = Get-PlayTestGlance $ptReal
    $with = Get-PlayTestGlance $ptReal -IdeasText $oneIdea -BugsText $bugs
    Assert-Equal $plain.Word $with.Word 'the word is the play tests alone'
    Assert-Equal $plain.Color $with.Color
    Assert-Equal ($plain.Detail + '; 1 new idea; 1 new bug') $with.Detail
    $two = $bugs -replace '"taken"', '"new"'
    Assert-Equal ($plain.Detail + '; 2 new bugs') (Get-PlayTestGlance $ptReal -BugsText $two).Detail
    foreach ($bad in @($null, '{ "schema": 1, "bugs": [', '{ "schema": 2, "bugs": [ { "status": "new" } ] }', '{ "schema": 1, "bugs": [] }')) {
        Assert-Equal $plain.Detail (Get-PlayTestGlance $ptReal -BugsText $bad).Detail 'no reports, no file or a bad one: nothing added'
    }
}

Fact 'P83_TheLinksOpenTheTwoPagesAndNothingElse' {
    foreach ($k in @('links.playtests', 'links.dashboard', 'links.status')) {
        $p = @(Get-ActionPlan -Action $k -Config $config)
        Assert-Equal 1 $p.Count $k
        Assert-Equal 'launch' $p[0].Kind $k
        Assert-Equal 'explorer.exe' $p[0].Path $k
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count $k
        Assert-Equal 'execute' (Resolve-RunMode -Action $k) ($k + ': only opens, so no mode is needed')
    }
    Assert-Equal 'http://localhost:8093/' (@(Get-ActionPlan -Action 'links.playtests' -Config $config))[0].Arguments[0]
    Assert-Equal 'http://192.168.1.61:3000/' (@(Get-ActionPlan -Action 'links.dashboard' -Config $config))[0].Arguments[0] 'cc-P110: Haven''s Homepage'
    Assert-Equal 'http://localhost:8093/status' (@(Get-ActionPlan -Action 'links.status' -Config $config))[0].Arguments[0] 'cc-P85'
}

Fact 'P83_TheTileHasItsTwoLinksAndTheAutopilotTabHasThemToo' {
    $script:NetProbeDisabled = $true
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.PlayTestsFile = Join-Path $fixture 'play-tests-2026-10-12.json'
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-Equal 'PLAY TESTS: 111 OPEN' $script:ui.PlayTestTile.Word.Text
        # cc-P85 adds Status beside the two.
        Assert-Equal 'links.playtests,links.dashboard,links.status' (($script:ui.LinkButtons | ForEach-Object { $_.Tag }) -join ',')
        Assert-True ($script:ui.PlayTestTile.Panel.Controls.Contains($script:ui.LinkButtons[0].Parent)) 'the buttons sit in the tile'
        $onTab = @($script:ui.AutopilotPage.Controls[1].Controls | Where-Object { $_.Tag -like 'links.*' } | ForEach-Object { $_.Tag })
        Assert-Equal 'links.playtests,links.dashboard,links.status' ($onTab -join ',') 'also on the Golem tab'
        Assert-Equal 9 @($script:ui.AutopilotButtons).Count 'P74''s own six, cc-P94''s Golem settings, cc-P99''s Open Golem folder and Move to the Golem; the links are not among them'
        Assert-True ($script:ui.Status.Text -match 'PLAY TESTS\s+: 111 OPEN') 'the status panel lists it'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

# --- cc-P84: the UO look, tooltips and the Action output pane ---------------------------------------------------
# The theme table, the tooltip table and the routing are CORE and checked as data. The forms are built from the
# capture, as above. The pane facts run real child processes: a cmd echo, a sleeping PowerShell, and this script as
# a -Mode DryRun child, which prints a plan and runs nothing. No fact here touches docker, git or either shard.

$p84Rgb = @{ Black = @(0, 0, 0); Navy = @(0, 0, 128); ForestGreen = @(34, 139, 34); DarkOrange = @(255, 140, 0); Firebrick = @(178, 34, 34); DimGray = @(105, 105, 105) }

Fact 'P84_EveryActionHasATooltipOfOneOrTwoPlainSentences' {
    $tips = Get-ConsoleTooltips
    foreach ($a in Get-ConsoleActions) {
        Assert-True $tips.Contains($a.Key) ($a.Key + ' has no tooltip')
    }
    foreach ($k in $tips.Keys) {
        $t = [string]$tips[$k]
        Assert-True ($t.Length -gt 10) ($k + ': empty')
        $sentences = @(($t -split '(?<=[.!?])\s+') | Where-Object { $_ })
        Assert-True ($sentences.Count -le 3) ($k + ': ' + $sentences.Count + ' sentences')
        Assert-True (-not ($t -match '[^\x20-\x7E]')) ($k + ': not plain ASCII')
        Assert-True (-not ($t -match '\bcc-P\d|\bD\d{2}\b')) ($k + ': internal codes belong in the notes')
    }
}

Fact 'P84_EveryTileStateKeepsItsMeaningAndAt45ToOneInBothLooks' {
    foreach ($name in 'Plain', 'UO') {
        $t = Get-ConsoleTheme $name
        Assert-Equal 'green,amber,red,grey' (@($t.Tiles.Keys) -join ',') ($name + ': the four states')
        foreach ($k in $t.Tiles.Keys) {
            $r = Get-ContrastRatio $t.Tiles[$k].Fore $t.Tiles[$k].Back
            Write-Host ('        ' + $name + ' tile ' + $k + ': ' + [math]::Round($r, 2) + ':1')
            Assert-True ($r -ge 4.5) ($name + ' ' + $k + ': ' + $r)
        }
        # The hues keep their meaning: green is greenest, red is reddest, amber is between, grey is grey.
        $g = $t.Tiles['green'].Back; $rd = $t.Tiles['red'].Back; $am = $t.Tiles['amber'].Back; $gy = $t.Tiles['grey'].Back
        Assert-True ($g[1] -gt $g[0] -and $g[1] -gt $g[2]) ($name + ' green')
        Assert-True ($rd[0] -gt $rd[1] -and $rd[0] -gt $rd[2]) ($name + ' red')
        Assert-True ($am[0] -gt $am[2] -and $am[1] -gt $am[2]) ($name + ' amber')
        Assert-True ([math]::Abs($gy[0] - $gy[2]) -le 30) ($name + ' grey')
    }
    $saved = $script:theme
    try {
        $script:theme = Get-ConsoleTheme 'UO'
        Assert-Equal (ConvertTo-DrawingColor $script:theme.Tiles['grey'].Back) (Get-TileColors 'unknown')[0] 'any other state is grey'
        Assert-Equal (ConvertTo-DrawingColor $script:theme.Tiles['red'].Fore) (Get-TileColors 'red')[1]
    } finally { $script:theme = $saved }
}

Fact 'P84_UOTextKeepsAt45ToOneOnEveryBackgroundAndOnTheArt' {
    $t = Get-ConsoleTheme 'UO'
    foreach ($k in $t.Lines.Keys) {
        $r = Get-ContrastRatio $t.Lines[$k] $t.LineBack
        Write-Host ('        UO line ' + $k + ': ' + [math]::Round($r, 2) + ':1')
        Assert-True ($r -ge 4.5) ('line ' + $k + ': ' + $r)
    }
    $c = $t.Chrome
    $pairs = @(
        @('StoneText on StoneBack', $c.StoneText, $c.StoneBack), @('Gold on StoneBack', $c.Gold, $c.StoneBack),
        @('PageText on PageBack', $c.PageText, $c.PageBack), @('PageDim on PageBack', $c.PageDim, $c.PageBack),
        @('PageWarn on PageBack', $c.PageWarn, $c.PageBack), @('ButtonText on ButtonBack', $c.ButtonText, $c.ButtonBack),
        @('ButtonText on ButtonHover', $c.ButtonText, $c.ButtonHover), @('FieldText on FieldBack', $c.FieldText, $c.FieldBack),
        # Measured on the gumps themselves: the pixel of each that is closest to the text drawn on it.
        @('StoneText on the stone gump', $c.StoneText, $t.Gumps.StoneWorst), @('Gold on the stone gump', $c.Gold, $t.Gumps.StoneWorst),
        @('PageText on the parchment gump', $c.PageText, $t.Gumps.ParchmentWorst), @('PageDim on the parchment gump', $c.PageDim, $t.Gumps.ParchmentWorst),
        @('PageWarn on the parchment gump', $c.PageWarn, $t.Gumps.ParchmentWorst)
    )
    foreach ($p in $pairs) {
        $r = Get-ContrastRatio $p[1] $p[2]
        Write-Host ('        UO ' + $p[0] + ': ' + [math]::Round($r, 2) + ':1')
        Assert-True ($r -ge 4.5) ($p[0] + ': ' + $r)
    }
    Assert-Equal 21 ([math]::Round((Get-ContrastRatio @(0, 0, 0) @(255, 255, 255)), 2)) 'the WCAG formula'
}

Fact 'P84_PlainIsTheOldLookValueForValue' {
    $t = Get-ConsoleTheme 'Plain'
    Assert-Equal $null $t.Chrome 'no chrome: every control keeps the WinForms default'
    Assert-Equal $null $t.Gumps 'no client art'
    Assert-Equal 'Segoe UI|9|Segoe UI|13|Consolas|9' (@($t.Fonts.Ui, $t.Fonts.UiSize, $t.Fonts.Tile, $t.Fonts.TileSize, $t.Fonts.Mono, $t.Fonts.MonoSize) -join '|')
    Assert-Equal '222,244,226|20,100,40|255,238,204|150,80,0|250,218,218|150,20,20|234,234,234|80,80,80' ((@('green', 'amber', 'red', 'grey') | ForEach-Object { ($t.Tiles[$_].Back -join ','), ($t.Tiles[$_].Fore -join ',') }) -join '|')
    $named = @{ normal = 'Black'; head = 'Navy'; green = 'ForestGreen'; amber = 'DarkOrange'; red = 'Firebrick'; gray = 'DimGray' }
    foreach ($k in $named.Keys) {
        Assert-Equal ($p84Rgb[$named[$k]] -join ',') ($t.Lines[$k] -join ',') ('line ' + $k + ' is ' + $named[$k])
        $d = [Drawing.Color]::($named[$k])
        Assert-Equal (@($d.R, $d.G, $d.B) -join ',') ($t.Lines[$k] -join ',') ('and that is the named color')
        # Plain's own line colors, as they were, against white (DarkOrange and ForestGreen are below 4.5: see the notes).
        Write-Host ('        Plain line ' + $k + ': ' + [math]::Round((Get-ContrastRatio $t.Lines[$k] $t.LineBack), 2) + ':1')
    }
}

Fact 'P84_APlainFormIsBuiltAsBefore' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-Equal 'Plain' $script:theme.Name 'Plain unless asked'
        Assert-Equal 1 $form.Controls.Count 'no header: the grid only'
        Assert-Equal ([Drawing.SystemColors]::Control) $form.BackColor 'the default back color'
        Assert-Equal 'Segoe UI' $form.Font.Name
        Assert-Equal 'Normal' ([string]$script:ui.Tabs.DrawMode) 'tabs drawn by Windows'
        Assert-Equal ([Drawing.Color]::White) $script:ui.Status.BackColor
        Assert-Equal $null $script:ui.Pane 'no pane without -Pane'
        $b = @($script:ui.Tabs.TabPages[0].Controls[0].Controls | Where-Object { $_ -is [Windows.Forms.Button] })[0]
        Assert-Equal 'Standard' ([string]$b.FlatStyle) 'buttons as before'
        Assert-Equal ([Drawing.Color]::FromArgb(234, 234, 234)) (Get-TileColors 'grey')[0] 'Plain grey'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

Fact 'P84_AUOFormIsThemedAndFallsBackWithoutTheClientArt' {
    $script:NetProbeDisabled = $true
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.GumpFile = Join-Path $env:TEMP ('no-such-client-' + [guid]::NewGuid().ToString('N') + '.uop')
    $cfg.CacheFolder = Join-Path $env:TEMP ('sl-console-cache-' + [guid]::NewGuid().ToString('N'))
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Theme UO
    try {
        $t = Get-ConsoleTheme 'UO'
        Assert-Equal 'UO' $script:theme.Name
        Assert-Equal (ConvertTo-DrawingColor $t.Chrome.StoneBack) $form.BackColor 'stone, drawn: the art is missing'
        Assert-Equal $null $form.BackgroundImage 'no art, no image'
        Assert-Equal 'Palatino Linotype' $form.Font.Name
        Assert-Equal 'OwnerDrawFixed' ([string]$script:ui.Tabs.DrawMode)
        Assert-True ($null -ne $script:ui.Header) 'the crest header'
        Assert-Equal 'Shattered Legacy  -  Shard Console' $script:ui.Header.Controls[1].Text
        $b = @($script:ui.Tabs.TabPages[0].Controls[0].Controls | Where-Object { $_ -is [Windows.Forms.Button] })[0]
        Assert-Equal 'Flat' ([string]$b.FlatStyle)
        Assert-Equal (ConvertTo-DrawingColor $t.Chrome.ButtonBorder) $b.FlatAppearance.BorderColor 'gold border'
        Assert-Equal (ConvertTo-DrawingColor $t.LineBack) $script:ui.Status.BackColor
        Assert-Equal (ConvertTo-DrawingColor $t.Tiles['red'].Back) (Get-TileColors 'red')[0]
        Assert-Equal 'TEST: STOPPED' $script:ui.Tiles[1].Word.Text 'the same words'
        # Behavior is untouched: the same buttons with the same keys, in the same tabs.
        $tags = @($script:ui.Tabs.TabPages[0].Controls[0].Controls | Where-Object { $_ -is [Windows.Forms.Button] } | ForEach-Object { $_.Tag })
        Assert-Equal 'test.start,test.fresh,test.wipe,test.stop,test.rebuild,test.deploy,test.tail,test.client' ($tags -join ',')
        Assert-True (-not (Test-Path -LiteralPath $cfg.CacheFolder)) 'nothing cached when there is nothing to read'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false; $script:theme = Get-ConsoleTheme 'Plain' }
}

Fact 'P84_EveryControlInTheWindowHasATooltip' {
    $script:NetProbeDisabled = $true
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.SettingsFile = Join-Path $env:TEMP ('sl-console-settings-' + [guid]::NewGuid().ToString('N') + '.json')
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Pane -Settings (ConvertFrom-ConsoleSettings $null)
    try {
        $missing = New-Object 'System.Collections.Generic.List[string]'
        $stack = New-Object System.Collections.Stack
        $stack.Push($form)
        $n = 0
        while ($stack.Count) {
            $x = $stack.Pop()
            $want = $x -is [Windows.Forms.ButtonBase] -or $x -is [Windows.Forms.TextBox] -or $x -is [Windows.Forms.ListView] -or $x -is [Windows.Forms.ComboBox] -or ($x.Name -like 'tile.*')
            if ($want) {
                $n++
                if (-not $script:ui.ToolTip.GetToolTip($x)) { $missing.Add(($x.GetType().Name + ' ' + $x.Name + ' ' + $x.Text)) }
            }
            if ($x -is [Windows.Forms.TabPage] -and -not $x.ToolTipText) { $missing.Add('tab ' + $x.Text) }
            foreach ($k in $x.Controls) { $stack.Push($k) }
        }
        Write-Host ('        ' + $n + ' controls checked')
        Assert-Equal 0 $missing.Count ($missing -join '; ')
        Assert-True $script:ui.Tabs.ShowToolTips 'tab tooltips are shown'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false; $script:PaneReady = $false; $script:ui.Pane = $null }
}

Fact 'P84_InteractiveActionsNeverGoToThePaneAndTheRestOnlyWhenAsked' {
    foreach ($a in Get-ConsoleActions) {
        foreach ($setting in 'pane', 'window') {
            $r = Get-OutputRoute -ActionKey $a.Key -OutputIn $setting
            if ($a.Key -in $script:InteractiveActions) { Assert-Equal 'window' $r ($a.Key + ' asks a person something') }
            elseif ($setting -eq 'window') { Assert-Equal 'window' $r ($a.Key + ': the setting says windows') }
            elseif ($a.RunIn -eq 'window' -or $a.Key -like '*.tail') { Assert-Equal 'pane' $r $a.Key }
            else { Assert-Equal 'window' $r ($a.Key + ' opens something; it has nothing to stream') }
        }
    }
    # cc-P144: and the four Legion scripts buttons that ask something in their own window.
    Assert-Equal 'test.wipe,package.publish,package.scripts.dev,package.scripts.promote,package.scripts.setup,package.scripts.key' ($script:InteractiveActions -join ',')
    Assert-Equal 'window' (Get-OutputRoute -ActionKey 'test.stop' -PaneBusy $true) 'a busy pane sends it to a window'
    Assert-Equal 'window' (Get-OutputRoute -ActionKey 'test.stop' -PaneAvailable $false) 'no pane, a window'
    Assert-Equal 'window' (Get-OutputRoute -ActionKey 'no.such.action') 'an unknown key, a window'
    # The two interactive ones, from their code: the wipe's password and the publish script's "Type yes".
    $self = Get-Content -Raw (Resolve-Path $script).Path
    Assert-True ($self -match "function Read-WipePassword[\s\S]{0,400}Read-Host -AsSecureString") 'the wipe asks a password'
    if (Test-Path -LiteralPath $config.PublishScript) { Assert-True ((Get-Content -Raw $config.PublishScript) -match 'Read-Host') 'publish asks yes' }
    # And the scripts the paneable window actions run ask nothing, so the pane cannot strand one at a prompt.
    foreach ($f in @($config.StartTestShard, $config.CheckDocker, $config.CheckShard, $config.CommitScript, $config.BuildPackage)) {
        if (Test-Path -LiteralPath $f) { Assert-True (-not ((Get-Content -Raw $f) -match 'Read-Host|PromptForChoice|Get-Credential|ReadKey')) ($f + ' prompts') }
    }
}

Fact 'P84_APaneChildEndsByItselfAndMarksEachLineWithItsColor' {
    $plainLine = Get-ChildArgumentLine -SelfPath 'C:\x.ps1' -ActionKey 'test.stop' -ShardKey 'test' -DryRun
    $paneLine = Get-ChildArgumentLine -SelfPath 'C:\x.ps1' -ActionKey 'test.stop' -ShardKey 'test' -DryRun -ForPane
    Assert-True ($plainLine -match ' -NoExit ') 'a window stays open, as before'
    Assert-Equal ($plainLine -replace ' -NoExit ', ' -NonInteractive ') $paneLine 'the pane line differs only there'
    Assert-Equal 'amber|hello' ((ConvertFrom-PaneLine (Format-PaneLine 'hello' 'amber')) | ForEach-Object { $_.Color + '|' + $_.Text })
    Assert-Equal 'gray|docker progress' ((ConvertFrom-PaneLine 'docker progress' $true) | ForEach-Object { $_.Color + '|' + $_.Text }) 'stderr is quiet, not red'
    Assert-Equal 'normal|[[shard-console:purple]]x' ((ConvertFrom-PaneLine '[[shard-console:purple]]x') | ForEach-Object { $_.Color + '|' + $_.Text }) 'an unknown color is just text'
    Initialize-ConsoleNative
    $args2 = Get-ChildArgumentLine -SelfPath (Resolve-Path $script).Path -ActionKey 'test.stop' -ShardKey 'test' -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -ForPane
    $job = New-Object ShardConsoleNative.PaneJob('powershell.exe', $args2, 'SHARD_CONSOLE_PANE')
    $deadline = (Get-Date).AddSeconds(60)
    while (-not $job.Finished -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
    Assert-True $job.Finished 'the dry child ended by itself'
    $lines = New-Object 'System.Collections.Generic.List[object]'
    $l = $null
    while ($job.Lines.TryDequeue([ref]$l)) { $lines.Add((ConvertFrom-PaneLine $l.Substring(1) ($l[0] -eq 'E'))) }
    Assert-Equal 0 $job.ExitCode
    $banner = @($lines | Where-Object { $_.Text -like '  DRY RUN  test.stop (test)*' })
    Assert-Equal 1 $banner.Count (($lines | ForEach-Object { $_.Color + ' ' + $_.Text }) -join ' / ')
    Assert-Equal 'amber' $banner[0].Color 'the banner keeps its color'
}

Fact 'P84_StopEndsTheWholeProcessTree' {
    Initialize-ConsoleNative
    $job = New-Object ShardConsoleNative.PaneJob('powershell.exe', '-NoProfile -NonInteractive -Command "ping -n 60 127.0.0.1"', $null)
    $kid = $null
    $deadline = (Get-Date).AddSeconds(20)
    while (-not $kid -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
        $kid = @(Get-CimInstance Win32_Process -Filter ('ParentProcessId=' + $job.Id) | Where-Object { $_.Name -eq 'PING.EXE' })[0]
    }
    Assert-True ($null -ne $kid) 'the child started its own child'
    Assert-Equal 0 $job.KillTree() 'taskkill /T'
    $deadline = (Get-Date).AddSeconds(15)
    while (-not $job.Finished -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
    Assert-True $job.Finished 'the job ended'
    Assert-Equal $null (Get-Process -Id $kid.ProcessId -ErrorAction SilentlyContinue) 'and so did its child'
}

Fact 'P84_ThePaneStreamsWhileTheWindowRunsAndAClickGoesThere' {
    $script:NetProbeDisabled = $true
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.SettingsFile = Join-Path $env:TEMP ('sl-console-settings-' + [guid]::NewGuid().ToString('N') + '.json')
    $saved = $script:SelfPath
    $script:SelfPath = (Resolve-Path $script).Path
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Pane -Settings (ConvertFrom-ConsoleSettings $null)
    try {
        $pane = $script:ui.Pane
        Assert-True $pane.Split.Panel2Collapsed 'collapsed until a job'
        Start-PaneJob -Exe 'cmd.exe' -Arguments '/c echo one& echo two 1>&2& exit 0' -Title 'echo'
        Assert-True (-not $pane.Split.Panel2Collapsed) 'shown when a job starts'
        Assert-True $pane.Stop.Enabled 'Stop while it runs'
        $deadline = (Get-Date).AddSeconds(20)
        while ($script:paneJob -and (Get-Date) -lt $deadline) { [Windows.Forms.Application]::DoEvents(); Update-PaneJob; Start-Sleep -Milliseconds 50 }
        Assert-True ($pane.Box.Text -match 'one' -and $pane.Box.Text -match 'two') $pane.Box.Text
        Assert-True ($pane.Box.Text -match '== exited with 0 \(done\)') 'the exit code at the end'
        Assert-True (-not $pane.Stop.Enabled) 'Stop off when it ends'
        # A window action clicked dry in the default setting: the same dry child, in the pane.
        Invoke-ConsoleButton -ActionKey 'test.stop'
        Assert-True ($null -ne $script:paneJob) 'the click started a pane job'
        $deadline = (Get-Date).AddSeconds(60)
        while ($script:paneJob -and (Get-Date) -lt $deadline) { [Windows.Forms.Application]::DoEvents(); Update-PaneJob; Start-Sleep -Milliseconds 50 }
        Assert-True ($pane.Box.Text -match 'DRY RUN  test\.stop \(test\)') $pane.Box.Text
        Assert-True ($script:OutputBox.Text -match 'test\.stop \(test\): DRY RUN in the Action output pane') 'the console says where it went'
    } finally {
        if ($script:paneJob) { [void]$script:paneJob.KillTree() }
        $form.Dispose(); $script:NetProbeDisabled = $false; $script:PaneReady = $false; $script:ui.Pane = $null; $script:paneJob = $null
        $script:SelfPath = $saved; $script:OutputBox = $null
    }
}

Fact 'P84_TheGumpCacheFillsFromTheClientOnceAndFallsBackWithoutIt' {
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.CacheFolder = Join-Path $env:TEMP ('sl-console-cache-' + [guid]::NewGuid().ToString('N'))
    try {
        $real = $cfg.GumpFile
        $cfg.GumpFile = Join-Path $env:TEMP 'no-such-client.uop'
        Assert-Equal $null (Get-GumpImage $cfg 3604) 'no client, no cache: null, and the drawn color is used'
        if (-not (Test-Path -LiteralPath $real)) { Write-Host '        (this PC has no client data: the extraction half is skipped)'; return }
        $cfg.GumpFile = $real
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $img = Get-GumpImage $cfg 3604
        Write-Host ('        first read, from the client: ' + $sw.ElapsedMilliseconds + ' ms')
        Assert-True ($null -ne $img -and $img.Width -eq 128) 'gump 3604 read'
        Assert-True (Test-Path -LiteralPath (Join-Path $cfg.CacheFolder 'gumps\3604.png')) 'and cached'
        $cfg.GumpFile = Join-Path $env:TEMP 'no-such-client.uop'
        $sw.Restart()
        $again = Get-GumpImage $cfg 3604
        Write-Host ('        second read, from the cache: ' + $sw.ElapsedMilliseconds + ' ms')
        Assert-True ($null -ne $again -and $again.Width -eq 128) 'the cache alone is enough'
        $p = Get-GumpImage $cfg 9304
        Assert-Equal $null $p 'a gump not cached, with no client: null'
    } finally { Remove-Item -LiteralPath $cfg.CacheFolder -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P84_TheSettingsDefaultToUOAndThePaneAndABadFileFallsBack' {
    # cc-P132: the default look is Enhanced now; UO and Plain are kept and a saved UO still loads.
    $d = ConvertFrom-ConsoleSettings $null
    Assert-Equal 'Enhanced|pane' ($d.Theme + '|' + $d.OutputIn)
    $s = ConvertFrom-ConsoleSettings '{ "Theme": "Plain", "OutputIn": "window" }'
    Assert-Equal 'Plain|window' ($s.Theme + '|' + $s.OutputIn)
    Assert-Equal 'UO' (ConvertFrom-ConsoleSettings '{ "Theme": "UO" }').Theme 'a saved UO look still loads'
    $back = ConvertFrom-ConsoleSettings (ConvertTo-ConsoleSettings $s)
    Assert-Equal 'Plain|window' ($back.Theme + '|' + $back.OutputIn) 'round trip'
    foreach ($bad in @('not json', '{ "Theme": "Neon", "OutputIn": "printer" }', '[]', '')) {
        $b = ConvertFrom-ConsoleSettings $bad
        Assert-Equal 'Enhanced|pane' ($b.Theme + '|' + $b.OutputIn) $bad
    }
}

Fact 'P84_TheUOLookAddsUnderASecondToTheStart' {
    $script:NetProbeDisabled = $true
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.CacheFolder = Join-Path $env:TEMP ('sl-console-cache-' + [guid]::NewGuid().ToString('N'))
    try {
        # Warm: the cache filled once, as after the first start.
        [void](Get-GumpImage $cfg 3604); [void](Get-GumpImage $cfg 9304)
        $times = @{}
        foreach ($name in 'Plain', 'UO', 'Plain', 'UO') {
            $sw = [Diagnostics.Stopwatch]::StartNew()
            $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Theme $name
            # The fastest of two builds per look: a 15 s build swings by a second on a busy PC (cc-P85 saw 1,063 ms once).
            if (-not $times.ContainsKey($name) -or $sw.ElapsedMilliseconds -lt $times[$name]) { $times[$name] = $sw.ElapsedMilliseconds }
            $form.Dispose()
        }
        Write-Host ('        build the window: Plain ' + $times['Plain'] + ' ms, UO ' + $times['UO'] + ' ms (warm cache)')
        Assert-True (($times['UO'] - $times['Plain']) -lt 1000) ('UO adds ' + ($times['UO'] - $times['Plain']) + ' ms')
    } finally { $script:NetProbeDisabled = $false; $script:theme = Get-ConsoleTheme 'Plain'; Remove-Item -LiteralPath $cfg.CacheFolder -Recurse -Force -ErrorAction SilentlyContinue }
}

# --- cc-P86: Disconnect VPN -----------------------------------------------------------------------------------------
# Facts are built from hand-made VpnFacts (Proton up, a WireGuard tunnel up, another app's tunnel, an unknown VPN,
# none). Nothing here connects or disconnects a VPN; the one real read is Get-VpnFacts on this PC with no VPN up.

function New-VpnFacts {
    param([switch]$Admin, [switch]$System, $Adapters = @(), $Tunnels = @(), [switch]$ProtonTunnel, $RouteIsLan = $true)
    $id = 'DESKTOP\chase'
    if ($System) { $id = 'NT AUTHORITY\SYSTEM' }
    [pscustomobject]@{ Identity = $id; IsSystem = [bool]$System; IsAdmin = [bool]$Admin; RouteIsLan = $RouteIsLan
        VpnAdapters = @($Adapters); WireGuardTunnels = @($Tunnels); ProtonTunnelRunning = [bool]$ProtonTunnel
        ProtonLauncher = 'C:\Program Files\Proton\VPN\ProtonVPN.Launcher.exe' }
}
$p86Proton = [pscustomobject]@{ Name = 'ProtonVPN'; Description = 'ProtonVPN Tunnel'; Type = 'Unknown'; Up = $true }
$p86WgOwn = [pscustomobject]@{ Name = 'WireGuardTunnel$home'; Tunnel = 'home'; Running = $true; Exe = 'C:\Program Files\WireGuard\wireguard.exe'; Owner = 'WireGuard' }
$p86WgApp = [pscustomobject]@{ Name = 'WireGuardTunnel$wg30c06d17'; Tunnel = 'wg30c06d17'; Running = $true; Exe = 'C:\Program Files\WiFiman Desktop\WireGuard\wireguard.exe'; Owner = 'WiFiman Desktop' }
$p86Other = [pscustomobject]@{ Name = 'NordLynx'; Description = 'NordLynx Tunnel'; Type = 'Unknown'; Up = $true }

Fact 'P86_TheButtonIsOnOnlyWhenAVpnIsUp' {
    Assert-True (-not (Test-VpnButtonEnabled ([pscustomobject]@{ Word = 'OK' }) (New-VpnFacts))) 'nothing up: greyed'
    Assert-True (-not (Test-VpnButtonEnabled $null $null)) 'nothing known: greyed'
    Assert-True (Test-VpnButtonEnabled ([pscustomobject]@{ Word = 'VPN ON' }) (New-VpnFacts)) 'the tile says VPN ON'
    Assert-True (Test-VpnButtonEnabled ([pscustomobject]@{ Word = 'OK' }) (New-VpnFacts -Adapters @($p86Other))) 'an adapter is up'
    Assert-True (Test-VpnButtonEnabled $null (New-VpnFacts -Tunnels @($p86WgOwn))) 'a tunnel is running'
    Assert-True (Test-VpnButtonEnabled $null (New-VpnFacts -ProtonTunnel)) 'Proton''s tunnel service runs'
    Assert-True (Test-VpnButtonEnabled $null (New-VpnFacts -RouteIsLan $false)) 'the route leaves the LAN'
    $stopped = [pscustomobject]@{ Name = 'WireGuardTunnel$x'; Tunnel = 'x'; Running = $false; Exe = 'C:\Program Files\WireGuard\wireguard.exe'; Owner = 'WireGuard' }
    Assert-True (-not (Test-VpnButtonEnabled $null (New-VpnFacts -Tunnels @($stopped)))) 'a stopped tunnel service is not a VPN up'
}

Fact 'P86_WithNoVpnUpThePlanIsACleanNoOp' {
    $p = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts))
    Assert-Equal 1 $p.Count
    Assert-Equal 'refuse' $p[0].Kind
    Assert-Equal 'No VPN is up: nothing to disconnect.' $p[0].Text
}

Fact 'P86_ProtonIsOpenedWithWhatToClickAndNeverStopped' {
    $p = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts -Adapters @($p86Proton) -RouteIsLan $false))
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
    Assert-True ($p[0].Kind -eq 'say' -and $p[0].Text -match 'no supported way to disconnect from a script' -and $p[0].Text -match 'Advanced kill switch') $p[0].Text
    $l = @($p | Where-Object { $_.Kind -eq 'launch' })
    Assert-Equal 1 $l.Count 'opens Proton'
    Assert-Equal 'C:\Program Files\Proton\VPN\ProtonVPN.Launcher.exe' $l[0].Path
    Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'exec' }).Count 'runs nothing against Proton'
    # The same through Proton's WireGuard protocol service, with no adapter named Proton.
    $q = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts -ProtonTunnel))
    Assert-Equal 1 @($q | Where-Object { $_.Kind -eq 'launch' }).Count
}

Fact 'P86_AWireGuardTunnelIsEndedByItsOwnExeOnlyWhenElevated' {
    $p = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts -Admin -Tunnels @($p86WgOwn)))
    $e = @($p | Where-Object { $_.Kind -eq 'exec' })
    Assert-Equal 1 $e.Count
    Assert-Equal 'C:\Program Files\WireGuard\wireguard.exe /uninstalltunnelservice home' (($e[0].Exe, $e[0].Arguments) | ForEach-Object { $_ }) -join ' '
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
    $n = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts -Tunnels @($p86WgOwn)))
    Assert-Equal 0 @($n | Where-Object { $_.Kind -eq 'exec' }).Count 'not elevated: nothing runs'
    Assert-True (@($n | Where-Object { $_.Text -match 'needs an elevated console' }).Count -eq 1) 'and it says so'
    Assert-True (Test-VpnNeedsElevation (New-VpnFacts -Tunnels @($p86WgOwn))) 'the console offers to relaunch elevated'
    Assert-True (-not (Test-VpnNeedsElevation (New-VpnFacts -Admin -Tunnels @($p86WgOwn)))) 'not when it already is'
}

Fact 'P86_AnotherAppsTunnelAndAnUnknownVpnAreNamedNotTouched' {
    $p = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts -Admin -Tunnels @($p86WgApp) -Adapters @($p86Other)))
    Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('exec', 'launch', 'window') }).Count 'runs nothing'
    Assert-True (@($p | Where-Object { $_.Text -eq 'The WireGuard tunnel wg30c06d17 belongs to WiFiman Desktop: disconnect it in that app.' }).Count -eq 1) 'WiFiman named'
    Assert-True (@($p | Where-Object { $_.Text -like 'NordLynx (NordLynx Tunnel) looks like a VPN: disconnect it in its app.' }).Count -eq 1) 'the unknown one named'
    $r = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts -RouteIsLan $false))
    Assert-True (@($r | Where-Object { $_.Text -like 'Traffic leaves through a route that is not the LAN*' }).Count -eq 1) 'a route with no name'
}

Fact 'P86_RefusedAsSystem' {
    $p = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts (New-VpnFacts -System -Adapters @($p86Proton)))
    Assert-Equal 1 $p.Count
    Assert-True ($p[0].Kind -eq 'refuse' -and $p[0].Text -like '*NT AUTHORITY\SYSTEM*') $p[0].Text
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'vpn.disconnect') 'it changes something, so a lost mode fails safe'
}

Fact 'P86_TheSafetyCheckRefusesEveryUnsafeWayToEndAVpn' {
    foreach ($bad in @(
            (New-PlanStep -Kind exec -Exe 'net' -Arguments @('stop', 'ProtonVPN Service')),
            (New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-Command', 'Stop-Service ProtonVPNService')),
            (New-PlanStep -Kind exec -Exe 'taskkill.exe' -Arguments @('/IM', 'ProtonVPN.Client.exe', '/F')),
            (New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-Command', 'Disable-NetAdapter ProtonVPN')),
            (New-PlanStep -Kind launch -Path 'C:\Program Files\Proton\VPN\v5.1.8\ProtonVPN.RestoreInternet.exe'),
            (New-PlanStep -Kind exec -Exe 'C:\Program Files\WireGuard\wireguard.exe' -Arguments @('/installtunnelservice', 'x.conf')),
            (New-PlanStep -Kind exec -Exe 'C:\Program Files\WireGuard\wireguard.exe' -Arguments @('/uninstalltunnelservice', 'a b'))
        )) {
        Assert-True (@(Test-PlanSafety @($bad) $config).Count -ge 1) ('allowed: ' + $bad.Exe + ' ' + ($bad.Arguments -join ' ') + ' ' + $bad.Path)
    }
}

Fact 'P86_TheDdnsRestartIsOfferedAsASafePlan' {
    $p = @(Get-DdnsRestartPlan $config)
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
    $e = @($p | Where-Object { $_.Kind -eq 'exec' })
    Assert-Equal 'docker restart sl-ddns' ((@($e[0].Exe) + $e[0].Arguments) -join ' ')
    $self = Get-Content -Raw (Resolve-Path $script).Path
    Assert-True ($self -match "Show-YesNo 'The VPN is off, but public DNS still has the VPN''s address") 'only after a yes'
}

Fact 'P86_TheButtonsSitByTheTileAndOnDiagnosticsGreyedWithNoVpn' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-Equal 2 @($script:ui.VpnButtons).Count
        Assert-True ($script:ui.NetTile.Panel.Controls.Contains($script:ui.VpnButtons[0].Parent)) 'in the PUBLIC DNS tile'
        Assert-Equal 'vpn.disconnect' $script:ui.VpnButtons[1].Tag
        Assert-True (@($script:ui.Tabs.TabPages | Where-Object { $_.Text -eq 'Diagnostics' })[0].Controls[0].Controls.Contains($script:ui.VpnButtons[1])) 'on the Diagnostics tab'
        foreach ($b in $script:ui.VpnButtons) { Assert-True (-not $b.Enabled) 'greyed: no VPN is known' }
        $script:netResult = [pscustomobject]@{ RouteIsLan = $false; RouteInterface = 'ProtonVPN'; VpnAdapters = @('ProtonVPN'); DnsIps = @('159.26.100.68') }
        Update-VpnButtons
        foreach ($b in $script:ui.VpnButtons) { Assert-True $b.Enabled 'on when the tile says VPN ON' }
    } finally { $form.Dispose(); $script:netResult = $null; $script:NetProbeDisabled = $false }
}

Fact 'P86_OnThisPcWithNoVpnUpTheRealFactsGiveANoOp' {
    $f = Get-VpnFacts $config
    Write-Host ('        this PC: route on LAN ' + $f.RouteIsLan + ', ' + @($f.VpnAdapters).Count + ' VPN adapter(s) up, ' + @($f.WireGuardTunnels | Where-Object { $_.Running }).Count + ' tunnel(s) running of ' + @($f.WireGuardTunnels).Count + ', Proton tunnel ' + $f.ProtonTunnelRunning + ', admin ' + $f.IsAdmin)
    foreach ($t in $f.WireGuardTunnels) { Write-Host ('        tunnel ' + $t.Tunnel + ' owner ' + $t.Owner + ' running ' + $t.Running) }
    $up = Test-VpnButtonEnabled $null $f
    if ($up) { Write-Host '        (a VPN is up on this PC now; the no-op half is skipped)'; return }
    $p = @(Get-ActionPlan -Action 'vpn.disconnect' -Config $config -VpnFacts $f)
    Assert-Equal 'No VPN is up: nothing to disconnect.' $p[0].Text
}

# --- cc-P87: the UO look's readability pass ---------------------------------------------------

function New-P87Form {
    # A UO form without the client art (the facts never need EA's art), shown off screen so that layout and
    # painting run as they do for Chase.
    param([switch]$Pane)
    $script:NetProbeDisabled = $true
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.GumpFile = Join-Path $env:TEMP ('no-such-client-' + [guid]::NewGuid().ToString('N') + '.uop')
    $cfg.CacheFolder = Join-Path $env:TEMP ('sl-console-cache-' + [guid]::NewGuid().ToString('N'))
    $cfg.SettingsFile = Join-Path $env:TEMP ('sl-console-settings-' + [guid]::NewGuid().ToString('N') + '.json')
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Theme UO -Pane:$Pane -Settings (ConvertFrom-ConsoleSettings $null)
    $form.StartPosition = 'Manual'
    $form.Location = New-Object Drawing.Point(-6000, -6000)
    $form.ShowInTaskbar = $false
    $form.Show()
    foreach ($i in 1..5) { [Windows.Forms.Application]::DoEvents() }
    $form
}

function Test-P87TileFits {
    # Nothing in the tile is cut off: the title fits its label, the detail fits what is left, and every button
    # lies wholly inside the tile.
    param($Tile, [string]$Label)
    $p = $Tile.Panel
    $w = $Tile.Word
    $m = [Windows.Forms.TextRenderer]::MeasureText($w.Text, $w.Font, (New-Object Drawing.Size(($w.ClientSize.Width - $w.Padding.Horizontal), 0)), [Windows.Forms.TextFormatFlags]'WordBreak')
    Assert-True ($m.Height + $w.Padding.Vertical -le $w.ClientSize.Height + 1) ($Label + ': title "' + $w.Text + '" needs ' + ($m.Height + $w.Padding.Vertical) + ' px, has ' + $w.ClientSize.Height)
    Assert-True ($m.Width -le $w.ClientSize.Width - $w.Padding.Horizontal) ($Label + ': title "' + $w.Text + '" is ' + $m.Width + ' px wide in ' + ($w.ClientSize.Width - $w.Padding.Horizontal))
    $d = $Tile.Detail
    $dm = [Windows.Forms.TextRenderer]::MeasureText($d.Text, $d.Font, (New-Object Drawing.Size(($d.ClientSize.Width - $d.Padding.Horizontal), 0)), [Windows.Forms.TextFormatFlags]'WordBreak')
    Assert-True ($dm.Height + $d.Padding.Vertical -le $d.ClientSize.Height + 1) ($Label + ': detail needs ' + ($dm.Height + $d.Padding.Vertical) + ' px, has ' + $d.ClientSize.Height + ' at ' + $d.Font.Size + ' pt')
    foreach ($bar in @($p.Controls | Where-Object { $_ -is [Windows.Forms.FlowLayoutPanel] })) {
        foreach ($b in $bar.Controls) {
            $r = $p.RectangleToClient($b.RectangleToScreen($b.ClientRectangle))
            Assert-True ($p.ClientRectangle.Contains($r)) ($Label + ': button "' + $b.Text + '" at ' + $r + ' is outside the tile ' + $p.ClientRectangle)
            Assert-True ($bar.ClientRectangle.Contains($bar.RectangleToClient($b.RectangleToScreen($b.ClientRectangle)))) ($Label + ': button "' + $b.Text + '" is cut by its bar')
        }
    }
}

Fact 'P87_EveryUOTextPairMeetsItsFloorOnTheArtsLightestAndDarkest' {
    $pairs = @(Get-ThemeContrastPairs (Get-ConsoleTheme 'UO'))
    foreach ($q in $pairs) {
        Write-Host ('        ' + $q.Where + ': ' + $q.Ratio + ':1 (floor ' + $q.Floor + ')')
        Assert-True ($q.Ratio -ge $q.Floor) ($q.Where + ': ' + $q.Ratio + ' under ' + $q.Floor)
    }
    # cc-P132: 68 (was 35): the named text colors on each page extreme, warning and danger words on the stone, the plate.
    Assert-Equal 68 $pairs.Count 'every pair the UO look draws, the two Windows draws itself included'
    foreach ($k in 'normal', 'large', 'disabled') { Assert-True (@($pairs | Where-Object { $_.Kind -eq $k }).Count) ('a ' + $k + ' pair') }
    # The colored lines read as easily as a black one: 7:1, not just 4.5.
    $t = Get-ConsoleTheme 'UO'
    foreach ($k in 'green', 'amber', 'red', 'gray') { Assert-True ((Get-ContrastRatio $t.Lines[$k] $t.LineBack) -ge 7) ('line ' + $k + ' at 7:1') }
    Assert-Equal 0 @(Get-ThemeContrastPairs (Get-ConsoleTheme 'Plain')).Count 'Plain draws no chrome of its own'
    Assert-Equal '35,33,32' ((Get-BlendedColor @(24, 22, 20) 200 @(74, 74, 74)) -join ',') 'the blend'
}

Fact 'P87_TextOnTheStoneSitsOnTheBacking' {
    $form = New-P87Form -Pane
    try {
        $c = (Get-ConsoleTheme 'UO').Chrome
        $want = [Drawing.Color]::FromArgb($c.BackingAlpha, $c.Backing[0], $c.Backing[1], $c.Backing[2])
        # cc-P132: the two groups sit in the splitters' panels now (Status in the first's top, the output in the second's bottom).
        $groups = @(@($script:ui.Split1.Panel1.Controls) + @($script:ui.Split2.Panel2.Controls) | Where-Object { $_ -is [Windows.Forms.GroupBox] })
        Assert-Equal 2 $groups.Count 'Status, and Console output'
        foreach ($g in $groups) { Assert-Equal $want.ToArgb() $g.BackColor.ToArgb() ($g.Text + ' on the backing') }
        Assert-Equal $want.ToArgb() $script:ui.Header.BackColor.ToArgb() 'the header'
        Assert-True ($script:ui.OwnWindows.Visible -and $script:ui.Look.Visible) 'the options row is all there'
        # The options row wraps and grows rather than hide its last box, at the narrowest window.
        $form.Size = New-Object Drawing.Size(900, 700)
        foreach ($i in 1..5) { [Windows.Forms.Application]::DoEvents() }
        $bar = $script:ui.OwnWindows.Parent
        $r = $bar.RectangleToClient($script:ui.OwnWindows.RectangleToScreen($script:ui.OwnWindows.ClientRectangle))
        Assert-True ($bar.ClientRectangle.Contains($r)) ('"actions in their own windows" at ' + $r + ' outside its bar ' + $bar.ClientRectangle)
    } finally { $form.Close(); $form.Dispose(); $script:NetProbeDisabled = $false; $script:theme = Get-ConsoleTheme 'Plain' }
}

Fact 'P87_ADisabledUOButtonIsDrawnInItsOwnLegibleColors' {
    $form = New-P87Form
    try {
        $c = (Get-ConsoleTheme 'UO').Chrome
        $b = $script:ui.VpnButtons[1]
        Assert-True (-not $b.Enabled) 'Disconnect VPN is greyed: no VPN is known'
        $script:ui.Tabs.SelectedIndex = 3
        foreach ($i in 1..5) { [Windows.Forms.Application]::DoEvents() }
        $bmp = New-Object Drawing.Bitmap($b.Width, $b.Height)
        $b.DrawToBitmap($bmp, (New-Object Drawing.Rectangle(0, 0, $b.Width, $b.Height)))
        $seen = @{}
        for ($x = 1; $x -lt $b.Width - 1; $x++) { for ($y = 1; $y -lt $b.Height - 1; $y++) { $q = $bmp.GetPixel($x, $y); $seen[('{0},{1},{2}' -f $q.R, $q.G, $q.B)] = 1 } }
        $bmp.Dispose()
        Assert-True $seen.ContainsKey(($c.DisabledBack -join ',')) 'its face is DisabledBack'
        Assert-True $seen.ContainsKey(($c.DisabledText -join ',')) 'its text is DisabledText'
        Assert-True (-not $seen.ContainsKey(($c.ButtonBack -join ','))) 'not the enabled face'
        # Enabled again, it is an ordinary button on the wood face.
        $b.Enabled = $true
        $bmp = New-Object Drawing.Bitmap($b.Width, $b.Height)
        $b.DrawToBitmap($bmp, (New-Object Drawing.Rectangle(0, 0, $b.Width, $b.Height)))
        $mid = $bmp.GetPixel(3, [int]($b.Height / 2)); $bmp.Dispose()
        Assert-Equal ($c.ButtonBack -join ',') ('{0},{1},{2}' -f $mid.R, $mid.G, $mid.B) 'the enabled face'
    } finally { $form.Close(); $form.Dispose(); $script:NetProbeDisabled = $false; $script:theme = Get-ConsoleTheme 'Plain' }
}

Fact 'P87_UOTilesNeverClipFromTheNarrowestWindowToAWideOne' {
    $form = New-P87Form
    try {
        $all = @($script:ui.Tiles) + @($script:ui.NetTile, $script:ui.AutopilotTile, $script:ui.PlayTestTile, $script:ui.WebsitesTile)
        # The words and details these tiles said on 2026-10-13, when Chase found them clipped (cc-P101 adds WEBSITES).
        $long = @(
            @('LIVE', 'STARTING?', 'up 23h 40m, not listening yet; OLD IMAGE'),
            @('TEST', 'RUNNING', 'up 13h 47m, listening on :2594; image current'),
            @('STATUS PUBLISHER', 'NOT PUBLISHING', 'no status.json; feed up, nothing to serve'),
            @('PUBLIC DNS', 'OK', 'shatteredlegacyuo.com = 216.247.206.76, this house. No VPN.'),
            @('GOLEM', 'NEEDS YOU', 'P86 done but COMMIT FAILED, needs you'),
            @('PLAY TESTS', '152 OPEN', '152 open, 1 failed, 18 passed'),
            @('WEBSITES', '6 OF 7 UP', 'down: Public website (no answer: ConnectFailure)')
        )
        # 900 is the smallest window. PowerShell's WinForms is DPI-unaware, so at 125% and 150% Windows scales the
        # whole window: 1024 x 768 is 1280 x 960 at 125%, and 900 x 700 is 1350 x 1050 at 150%.
        foreach ($size in @(@(900, 700), @(1024, 768), @(1280, 1000), @(1300, 1000), @(1600, 1000), @(2400, 1000))) {
            $form.Size = New-Object Drawing.Size($size[0], $size[1])
            for ($k = 0; $k -lt $all.Count; $k++) { Set-TileLook $all[$k] ([pscustomobject]@{ Color = 'amber'; Title = $long[$k][0]; Word = $long[$k][1]; Detail = $long[$k][2] }) }
            foreach ($i in 1..5) { [Windows.Forms.Application]::DoEvents() }
            $cols = $script:ui.Glance.ColumnCount
            Write-Host ('        ' + $size[0] + ' x ' + $size[1] + ': ' + $cols + ' across, tile row ' + $script:ui.Grid.RowStyles[0].Height + ' px, smallest title ' + (($all | ForEach-Object { $_.Word.Font.Size } | Measure-Object -Minimum).Minimum) + ' pt, smallest detail ' + (($all | ForEach-Object { $_.Detail.Font.Size } | Measure-Object -Minimum).Minimum) + ' pt')
            foreach ($tile in $all) { Test-P87TileFits $tile ([string]$size[0] + ' wide, ' + ($tile.Word.Text -split ':')[0]) }
            $gr = $script:ui.Glance
            Assert-True ($gr.Right -le $form.ClientSize.Width) ('the tiles end at ' + $gr.Right + ' inside ' + $form.ClientSize.Width)
        }
        Assert-Equal 7 $script:ui.Glance.ColumnCount 'all seven across on a wide window (cc-P101; six before)'
        $form.Size = New-Object Drawing.Size(1280, 1000)
        foreach ($i in 1..5) { [Windows.Forms.Application]::DoEvents() }
        Assert-Equal 4 $script:ui.Glance.ColumnCount 'four across, two rows, at 1280 (cc-P101; three before)'
    } finally { $form.Close(); $form.Dispose(); $script:NetProbeDisabled = $false; $script:theme = Get-ConsoleTheme 'Plain' }
}

Fact 'P87_PlainKeepsItsOneRowOfSixAndItsOwnLook' {
    # cc-P101: seven now, with WEBSITES; still one row, never wrapped.
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-Equal 7 $script:ui.Glance.ColumnCount
        Assert-Equal 104 $script:ui.Grid.RowStyles[0].Height
        Assert-Equal 0 $script:ui.Grid.ColumnStyles.Count 'the grid has no column style, as before'
        $form.Size = New-Object Drawing.Size(900, 700)
        Update-TileFit
        Assert-Equal 7 $script:ui.Glance.ColumnCount 'Plain never wraps its tiles'
        foreach ($g in @($script:ui.Grid.Controls | Where-Object { $_ -is [Windows.Forms.GroupBox] })) { Assert-Equal ([Drawing.SystemColors]::Control).ToArgb() $g.BackColor.ToArgb() ($g.Text + ': the WinForms default') }
        Assert-Equal 'Normal' ([string]$script:ui.Tabs.DrawMode) 'Windows draws the Plain tabs'
        Assert-Equal 'Consolas|9' ($script:MonoFont.Name + '|' + $script:MonoFont.Size)
        Assert-Equal 'Segoe UI|8.25' ($script:ui.VpnButtons[0].Font.Name + '|' + $script:ui.VpnButtons[0].Font.Size) 'the tile buttons as before'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

# --- cc-P96: Fix test shard -------------------------------------------------------------------------------------

Write-Host ""
Write-Host "cc-P96 Fix test shard facts" -ForegroundColor Cyan

Fact 'P96_TheD89LogGivesItsInnermostExceptionAndFirstFiveFrames' {
    $r = Get-CrashReport $p96CrashLog
    Assert-True $r.Crashed 'the log ends at "This exception is fatal"'
    Assert-True $r.AtStart 'it never reached Listening'
    Assert-Equal 'System.IndexOutOfRangeException' $r.Type 'the cause, not the reflection wrapper'
    Assert-Equal 'Index was outside the bounds of the array.' $r.Message
    Assert-Equal 'System.Reflection.TargetInvocationException,System.TypeInitializationException,System.IndexOutOfRangeException' ((@($r.Chain) | ForEach-Object { $_.Type }) -join ',')
    Assert-Equal 5 @($r.Frames).Count
    Assert-Equal 'Server.ImbueCatalogue.SkillRowName(SkillName skill)' $r.Frames[0]
    Assert-Equal 'Server.ImbueCatalogue..cctor()' $r.Frames[2]
    Assert-Equal 'Server.ImbuingTable.get_Defaults()' $r.Frames[4]
    Assert-Equal 'IndexOutOfRangeException: Index was outside the bounds of the array. at Server.ImbueCatalogue.SkillRowName(SkillName skill)' $r.Headline
}

Fact 'P96_AHealthyStartIsNoCrashAndACrashAfterListeningIsNotAtStart' {
    Assert-True (-not (Get-CrashReport $p96HealthyLog).Crashed) 'the healthy start log has no fatal line'
    $lines = @($p96CrashLog -split "`n")
    $at = [array]::IndexOf(@($lines | ForEach-Object { (Remove-Ansi $_).Trim() }), 'Error:')
    Assert-True ($at -gt 0) 'the fixture has its Error: line'
    $tail = ($lines | Select-Object -Skip $at) -join "`n"
    $late = Get-CrashReport ($p96HealthyLog + "`n" + $tail)
    Assert-True $late.Crashed 'a crash after the listener is still a crash'
    Assert-True (-not $late.AtStart) 'but not at start'
}

Fact 'P96_HealthReadsListeningCrashedAndStarting' {
    $starting = ($p96HealthyLog -split "`n" | Select-Object -First 20) -join "`n"
    Assert-Equal 'HEALTHY' (Get-HealthState $true $true 'healthy' (Get-CrashReport $p96HealthyLog)).State
    Assert-Equal 'STARTING' (Get-HealthState $true $true 'starting' (Get-CrashReport $starting)).State
    $c = Get-HealthState $true $true 'starting' (Get-CrashReport $p96CrashLog)
    Assert-Equal 'CRASHED' $c.State 'a crash outranks starting: docker says starting for its 120 s start period'
    Assert-True ($c.Detail -match '^crashed at start: IndexOutOfRangeException' -and $c.Detail -match 'Docker still says Up') $c.Detail
    Assert-Equal 'CRASHED' (Get-HealthState $true $true 'unhealthy' (Get-CrashReport $p96CrashLog)).State
    Assert-Equal 'UNHEALTHY' (Get-HealthState $true $true 'unhealthy' $null).State
    Assert-Equal 'NO HEALTH CHECK' (Get-HealthState $true $true '' $null).State 'a container from before the health check'
    Assert-Equal 'STOPPED' (Get-HealthState $true $false '' $null).State
    Assert-Equal 'NO CONTAINER' (Get-HealthState $false $false '' $null).State
}

Fact 'P96_TheRealInspectsGiveTheHealthDockerReported' {
    $u = ConvertFrom-ContainerInspect ([IO.File]::ReadAllText((Join-Path $fixture 'test-shard-inspect-unhealthy-2026-10-14.json')))
    Assert-Equal 'unhealthy' $u.Health 'docker ps said "Up 2 minutes (unhealthy)"'
    Assert-True $u.Running 'and Up: the crashed process waits on stdin'
    $h = ConvertFrom-ContainerInspect ([IO.File]::ReadAllText((Join-Path $fixture 'test-shard-inspect-healthy-2026-10-14.json')))
    Assert-Equal 'healthy' $h.Health
    $x = ConvertFrom-ContainerInspect ([IO.File]::ReadAllText((Join-Path $fixture 'test-shard-inspect-exited-2026-10-14.json')))
    Assert-Equal '' ([string]$x.Health) 'created before cc-P96: no health check'
}

Fact 'P96_KnownBugD89IsMatchedFromTheConfigTable' {
    Assert-True (@($config.Fix.KnownCrashes).Count -ge 1) 'scripts/fix-test-shard.json is read into the config'
    $k = Find-KnownCrash (Get-CrashReport $p96CrashLog) $config.Fix.KnownCrashes
    Assert-Equal 'D89' $k.Bug
    $other = Get-CrashReport ($p96CrashLog -replace 'ImbueCatalogue', 'SomethingElse')
    Assert-True $other.Crashed
    Assert-True ($null -eq (Find-KnownCrash $other $config.Fix.KnownCrashes)) 'another crash is not D89'
    Assert-True ($null -eq (Find-KnownCrash (Get-CrashReport $p96HealthyLog) $config.Fix.KnownCrashes)) 'no crash, no bug'
}

Fact 'P96_LastGoodIsTheNewestGoodTag' {
    $g = Get-LastGoodImage $p96GoodState.Images
    Assert-Equal 'sl-modernuo:good-6666666' $g.Tag
    Assert-True ($null -eq (Get-LastGoodImage (New-P96State -NoGood).Images)) 'the real 2026-10-14 listing has no good tag'
    Assert-Equal 'sl-modernuo:good-61db33c' (Get-GoodTag '61db33c0a1b2c3d4e5f60718293a4b5c6d7e8f90' $false)
    Assert-Equal 'sl-modernuo:good-61db33c-dirty' (Get-GoodTag '61db33c0a1b2c3d4e5f60718293a4b5c6d7e8f90' $true)
    Assert-True ($null -eq (Get-GoodTag 'unknown' $false)) 'no label, no tag'
    Assert-Equal '1234567' (Get-GoodTagCommit 'sl-modernuo:good-1234567-dirty')
    Assert-True ($null -eq (Get-GoodTagCommit 'sl-modernuo:goodish')) 'only good-<7 hex>'
}

Fact 'P96_PruneKeepsLatestGoodRollbackPreBumpNamedAndWhatContainersRun' {
    $rows = @($p96GoodState.Images) + @(
        [pscustomobject]@{ Tag = 'sl-modernuo:same-as-live'; Id = 'bc04a35bf344'; Created = [datetime]'2026-09-01'; Size = 1290000000 },
        [pscustomobject]@{ Tag = 'uo-modernuo:latest'; Id = 'bc04a35bf344'; Created = [datetime]'2026-09-01'; Size = 1290000000 })
    $set = Get-PruneSet -Images $rows -KeepGood 5 -KeepTags @('sl-modernuo:cc-p82', 'cc-p80') -ProtectIds @('sha256:178e2bdafef4aaaa', $p96LiveId)
    $keep = @($set.Keep | ForEach-Object { $_.Tag })
    $gone = @($set.Remove | ForEach-Object { $_.Tag })
    foreach ($t in 'latest', 'rollback-live-20260927', 'pre-bump-7c9215d97', 'pre-bump-purge-7c9215d97', 'good-6666666', 'good-2222222', 'cc-p82', 'cc-p80', 'same-as-live') {
        Assert-True ($keep -contains ('sl-modernuo:' + $t)) ('kept: ' + $t)
    }
    Assert-True ($gone -contains 'sl-modernuo:good-1111111') 'the sixth newest good tag is offered'
    Assert-True ($gone -contains 'sl-modernuo:s1' -and $gone -contains 'sl-modernuo:cc-p79') 'old builds are offered'
    Assert-Equal 0 @(@($keep) + @($gone) | Where-Object { $_ -notlike 'sl-modernuo:*' }).Count 'uo-modernuo is never listed, kept or removed'
    Assert-Equal (@($rows).Count - 1) ($keep.Count + $gone.Count) 'every sl-modernuo row is kept or offered'
    Assert-True ($set.FreedBytes -gt 0) 'some space is freed'
    Assert-Equal 'a container runs this image' (@($set.Keep | Where-Object { $_.Tag -eq 'sl-modernuo:same-as-live' })[0].Why)
}

Fact 'P96_TheTestTileSaysCrashedAtStartInRedWithTheKnownBug' {
    $g = Get-ShardGlance -Shard $config.Shards['test'] -ShardState $p96CrashState.Shards['test'] -Now $p96CrashState.Now -Config $config
    Assert-Equal 'CRASHED AT START' $g.Word
    Assert-Equal 'red' $g.Color
    Assert-True ($g.Detail -match 'IndexOutOfRangeException' -and $g.Detail -match 'Known bug D89') $g.Detail
    $tiles = Get-AtAGlance -State $p96CrashState -Config $config
    Assert-Equal 'CRASHED AT START' $tiles[1].Word 'and the tile row says it'
}

Fact 'P96_DiagnoseShowsStatusHealthImageUptimeTheCrashAndTheKnownBug' {
    $p = Get-FixActionPlan 'fix.diagnose' -State $p96CrashState
    $t = (@($p | Where-Object { $_.Kind -eq 'say' }) | ForEach-Object { $_.Text }) -join "`n"
    foreach ($want in '^Container: sl-modernuo-test, running, up ', '(?m)^Health: CRASHED', '(?m)^Image: ', '(?m)^Last good: sl-modernuo:good-6666666',
                      'IndexOutOfRangeException: Index was outside the bounds of the array\.', 'TypeInitializationException',
                      '(?m)^  at Server\.ImbueCatalogue\.SkillRowName', '(?m)^  at Server\.ImbuingTable\.get_Defaults', '(?m)^Known bug D89') {
        Assert-True ($t -match $want) ('Diagnose says ' + $want + "`n" + $t)
    }
    Assert-Equal 5 @(([regex]::Matches($t, '(?m)^  at '))).Count 'five frames'
    Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('confirm', 'exec', 'copy', 'move') }).Count 'it reads; it changes nothing'
    Assert-Equal 1 @($p | Where-Object { $_.Kind -eq 'log' }).Count 'and logs that it ran'
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
}

Fact 'P96_RollbackBacksUpTheTestSaveBeforeItRunsTheLastGoodImage' {
    Assert-True $p96GoodState.Shards['test'].Save.Exists 'the capture has a test save'
    $p = Get-FixActionPlan 'fix.rollback'
    Assert-Equal 'roll back test' @($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase
    $kinds = @($p | ForEach-Object { $_.Kind })
    $down  = [array]::IndexOf(@($p | ForEach-Object { ($_.Kind -eq 'exec') -and (@($_.Arguments) -contains '-Down') }), $true)
    $copy  = [array]::IndexOf($kinds, 'copy')
    $ver   = [array]::IndexOf($kinds, 'verify')
    $run   = [array]::IndexOf(@($p | ForEach-Object { ($_.Kind -eq 'exec') -and (@($_.Arguments) -contains '-Image') }), $true)
    Assert-True ($down -ge 0 -and $copy -gt $down -and $ver -gt $copy -and $run -gt $ver) ('stop, back up, verify, then run: ' + ($kinds -join ','))
    $c = $p[$copy]
    Assert-Equal ([IO.Path]::GetFullPath($config.Shards['test'].Saves)) ([IO.Path]::GetFullPath($c.From))
    Assert-True ($c.To.StartsWith((Join-Path $config.SnapshotRoot 'test') + '\') -and $c.To -match '_pre-rollback-6666666$') $c.To
    Assert-True ((@($p | Where-Object { $_.Kind -eq 'confirm' })[0].Text).Contains($c.To)) 'the backup path is shown before the phrase'
    $args = @($p[$run].Arguments)
    Assert-Equal 'sl-modernuo:good-6666666' $args[[array]::IndexOf($args, '-Image') + 1]
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
}

Fact 'P96_RollbackWarnsWhenTheSaveIsNewerThanTheImage' {
    $newer = $p96Facts.PSObject.Copy(); $newer.SaveNewer = $true
    $newer.SaveRecord = [pscustomobject]@{ commit = '61db33c0a1b2c3d4e5f60718293a4b5c6d7e8f90'; good = 'sl-modernuo:good-61db33c'; at = '2026-10-14T13:40:00Z' }
    $t = (@(Get-FixActionPlan 'fix.rollback' -Facts $newer | Where-Object { $_.Kind -in @('say', 'confirm') }) | ForEach-Object { $_.Text }) -join "`n"
    Assert-True ($t -match 'WARNING: the test save was last written by 61db33c, a newer commit than 6666666') $t
    $older = $p96Facts.PSObject.Copy(); $older.SaveNewer = $false
    $t2 = (@(Get-FixActionPlan 'fix.rollback' -Facts $older | Where-Object { $_.Kind -in @('say', 'confirm') }) | ForEach-Object { $_.Text }) -join "`n"
    Assert-True ($t2 -notmatch 'WARNING') $t2
    $t3 = (@(Get-FixActionPlan 'fix.rollback' | Where-Object { $_.Kind -eq 'say' }) | ForEach-Object { $_.Text }) -join "`n"
    Assert-True ($t3 -match 'not known which commit last wrote the test save') $t3
}

Fact 'P96_RollbackRefusesWithNoGoodImage' {
    $p = Get-FixActionPlan 'fix.rollback' -State (New-P96State -NoGood)
    Assert-Equal 1 @($p | Where-Object { $_.Kind -eq 'refuse' }).Count
    Assert-True ($p[0].Text -match 'no sl-modernuo:good-\* image') $p[0].Text
    Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('exec', 'copy') }).Count
}

Fact 'P96_RebuildBuildsAPickedCommitAndRefusesOthers' {
    $p = Get-FixActionPlan 'fix.rebuild' -Commit '10f4088aa1b2c3d4e5f60718293a4b5c6d7e8f90'
    Assert-Equal 'rebuild test' @($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase
    $e = @($p | Where-Object { $_.Kind -eq 'exec' })
    Assert-Equal 1 $e.Count
    Assert-Equal ($config.StartTestShard + '|-Commit|10f4088aa1b2c3d4e5f60718293a4b5c6d7e8f90') ((@($e[0].Arguments) | Select-Object -Skip 4) -join '|')
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
    foreach ($bad in 'deadbeefdeadbeefdeadbeefdeadbeefdeadbeef', '', 'HEAD; rm -rf') {
        $q = Get-FixActionPlan 'fix.rebuild' -Commit $bad
        Assert-Equal 1 @($q | Where-Object { $_.Kind -eq 'refuse' }).Count ('refused: [' + $bad + ']')
    }
    $g = Get-FixActionPlan 'fix.rebuild' -Commit '6666666'
    Assert-Equal 0 @($g | Where-Object { $_.Kind -eq 'refuse' }).Count 'the last good commit is on the list too'
}

Fact 'P96_PrunePlanRemovesTagsOneByOneAfterThePhrase' {
    $p = Get-FixActionPlan 'fix.prune'
    Assert-Equal 'prune images' @($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase
    $e = @($p | Where-Object { $_.Kind -eq 'exec' })
    Assert-True ($e.Count -gt 50) ('found ' + $e.Count)
    foreach ($s in $e) {
        Assert-Equal 'docker' $s.Exe
        Assert-Equal 2 @($s.Arguments).Count ('one tag per step: ' + ($s.Arguments -join ' '))
        Assert-Equal 'rmi' $s.Arguments[0]
        Assert-True ($s.Arguments[1] -like 'sl-modernuo:*' -and $s.Arguments[1] -notin @('sl-modernuo:latest', 'sl-modernuo:good-6666666', 'sl-modernuo:rollback-live-20260927')) $s.Arguments[1]
    }
    Assert-True (((@($p | Where-Object { $_.Kind -eq 'confirm' })[0]).Text) -match 'up to [\d.]+ GB') 'the space freed is shown before the phrase'
    # With docker system df -v's unique sizes (a real capture, 2026-10-14), a lower bound too.
    $uf = $p96Facts.PSObject.Copy()
    $uf | Add-Member -NotePropertyName UniqueSizes -NotePropertyValue (ConvertFrom-DockerDfImages ([IO.File]::ReadAllText((Join-Path $fixture 'docker-system-df-images-2026-10-14.json')))) -Force
    Assert-True ($uf.UniqueSizes.Count -gt 50) ('unique sizes read: ' + $uf.UniqueSizes.Count)
    $t = (@(Get-FixActionPlan 'fix.prune' -Facts $uf | Where-Object { $_.Kind -eq 'confirm' })[0]).Text
    Assert-True ($t -match 'at least [\d.]+ [GM]B \(layers only those images use\) and up to [\d.]+ GB') $t
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
}

Fact 'P96_TheSafetyCheckAllowsOnlyAConfirmedRmiOfOneSlModernuoTag' {
    $ok = New-PlanStep -Kind confirm -Phrase 'prune images'
    $rmi = { param([string[]]$a) New-PlanStep -Kind exec -Exe 'docker' -Arguments (@('rmi') + $a) }
    Assert-Equal 0 @(Test-PlanSafety @($ok, (& $rmi @('sl-modernuo:cc-p50'))) $config).Count 'the one allowed shape'
    foreach ($bad in @(@('uo-modernuo:latest'), @('sl-modernuo:latest'), @('-f', 'sl-modernuo:cc-p50'), @('sl-modernuo:cc-p50', 'sl-modernuo:cc-p51'), @('sl-modernuo:rollback-live-20260927'), @('sl-modernuo:pre-bump-7c9215d97'), @('bc04a35bf344'))) {
        Assert-True (@(Test-PlanSafety @($ok, (& $rmi $bad)) $config).Count -gt 0) ('refused: rmi ' + ($bad -join ' '))
    }
    Assert-True (@(Test-PlanSafety @((& $rmi @('sl-modernuo:cc-p50'))) $config).Count -gt 0) 'refused without the phrase'
    $liveNamed = @($ok, (& $rmi @('sl-modernuo:cc-p50')), (New-PlanStep -Kind say -Text 'x'), (New-PlanStep -Kind exec -Exe 'docker' -Arguments @('inspect', 'sl-modernuo')))
    Assert-True (@(Test-PlanSafety $liveNamed $config).Count -gt 0) 'refused in a plan that names the live container'
}

Fact 'P96_RestartRestartsOnlyTheTestContainerAfterThePhraseThenWaits' {
    $p = Get-FixActionPlan 'fix.restart' -State $p96CrashState
    Assert-Equal 'restart test' @($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase
    $e = @($p | Where-Object { $_.Kind -eq 'exec' })
    Assert-Equal 'docker restart -t 60 sl-modernuo-test' (Format-CommandLine $e[0].Exe $e[0].Arguments)
    $w = @($p | Where-Object { $_.Kind -eq 'healthwait' })
    Assert-Equal 1 $w.Count 'it waits for healthy or a crash'
    Assert-Equal 'sl-modernuo-test' $w[0].Container
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count
    $live = @((New-PlanStep -Kind confirm -Phrase 'restart test'), (New-PlanStep -Kind healthwait -Container 'sl-modernuo'))
    Assert-True (@(Test-PlanSafety $live $config).Count -gt 0) 'a health wait is only ever on the test container'
    $gone = New-P96State
    $gone.Shards['test'].Container = ConvertFrom-ContainerInspect $cap['inspect sl-console-missing']
    Assert-Equal 1 @(Get-FixActionPlan 'fix.restart' -State $gone | Where-Object { $_.Kind -eq 'refuse' }).Count 'no container, nothing to restart'
}

Fact 'P96_EveryFixButtonHasAPhraseExceptDiagnoseAndIsOnItsOwnTab' {
    $fix = @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Fix test shard' })
    Assert-Equal 'fix.diagnose,fix.restart,fix.rollback,fix.rebuild,fix.prune' (($fix | ForEach-Object { $_.Key }) -join ',')
    Assert-Equal 'restart test|roll back test|rebuild test|prune images' ((@('fix.restart', 'fix.rollback', 'fix.rebuild', 'fix.prune') | ForEach-Object { Get-ConfirmPhrase $_ }) -join '|')
    Assert-True (-not ($fix | Where-Object { $_.Key -eq 'fix.diagnose' }).Mutates) 'Diagnose only reads'
}

Fact 'P96_EveryFixButtonClickedDryPrintsItsPlanAndRunsNothing' {
    foreach ($k in 'fix.diagnose', 'fix.restart', 'fix.rollback', 'fix.rebuild', 'fix.prune') {
        $r = Invoke-ClickCapture $k 'test' $true $p96CrashState
        Assert-Equal 0 $r.Executed.Count ($k + ' runs nothing in the console')
        Assert-True (@($r.Lines | Where-Object { $_ -match '^== DRY RUN: ' + [regex]::Escape($k) }).Count -eq 1) ($k + ' prints its plan: ' + ($r.Lines -join ' | '))
        if ($k -eq 'fix.diagnose') { Assert-Equal 0 $r.Spawned.Count 'Diagnose prints here' }
        else { Assert-True ($r.Spawned.Count -eq 1 -and $r.Spawned[0] -match ' -Mode DryRun ') ($k + ': ' + ($r.Spawned -join ' | ')) }
    }
}

Fact 'P96_EveryFixButtonRunHeadlessWithDryRunRunsNothing' {
    # The child a click opens, run for real with -DryRun: it reads the capture file, prints the plan, runs nothing.
    foreach ($k in 'fix.diagnose', 'fix.restart', 'fix.rollback', 'fix.rebuild', 'fix.prune') {
        $extra = @()
        if ($k -eq 'fix.rebuild') { $extra = @('-Commit', 'HEAD') }
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Resolve-Path $script).Path -DryRun -Action $k @extra 2>&1 | Out-String)
        Assert-Equal 0 $LASTEXITCODE ($k + ': ' + $out)
        Assert-True ($out -match ('== DRY RUN: ' + [regex]::Escape($k))) ($k + ': ' + $out)
        Assert-True ($out -match '\(dry run: nothing was run\)') ($k + ': ' + $out)
    }
}

# --- cc-P101: the Websites tab, the WEBSITES tile, Open NPM and Haven deploys --------------------------------------
# fixtures\websites-2026-10-14.json is scripts\websites.json as P101 wrote it. Other shapes are that file changed in
# memory. Nothing here reaches a real site: the probe fact serves its own answers on 127.0.0.1.

$webText = [IO.File]::ReadAllText((Join-Path $fixture 'websites-2026-10-14.json'))
function Get-WebFixture { param([scriptblock]$Change) $j = $webText | ConvertFrom-Json; if ($Change) { & $Change $j }; $j | ConvertTo-Json -Depth 8 }
function Get-WebConfig { param($Text = $webText) $c = Get-ConsoleConfig -RepoRoot $repo; $c.Websites = ConvertFrom-WebsiteConfig $Text $repo; $c }
function Get-WebResults { param([hashtable]$Codes) $r = @{}; foreach ($k in $Codes.Keys) { $v = $Codes[$k]; $r[$k] = $(if ($v -eq 'timeout') { [pscustomobject]@{ Code = 0; Error = ''; TimedOut = $true; Ms = 4000 } } elseif ($v -is [string]) { [pscustomobject]@{ Code = 0; Error = $v; TimedOut = $false; Ms = 3 } } else { [pscustomobject]@{ Code = $v; Error = ''; TimedOut = $false; Ms = 12 } }) }; $r }
$webAllUp = @{ playtests = 200; status = 200; homepage = 200; website = 200; wiki = 200; vault = 200; npm = 301; printqueue = 200; megadash = 200 }

Fact 'P101_TheRealConfigReadsAsSevenSitesTwoDeploysAndTheNpmTunnel' {
    # cc-P109 added the eighth site (Print queue, on Haven) and cc-P110 the ninth (Mega dashboard), each with two deploys;
    # the name keeps P101's count.
    $w = ConvertFrom-WebsiteConfig $webText $repo
    Assert-Equal 0 @($w.Problems).Count (@($w.Problems) -join ' | ')
    Assert-Equal 'playtests,status,homepage,website,wiki,vault,npm,printqueue,megadash' ((@($w.Sites) | ForEach-Object { $_.Key }) -join ',')
    Assert-Equal 'pc,pc,pc,haven,haven,haven,haven,haven,haven' ((@($w.Sites) | ForEach-Object { $_.Where }) -join ',')
    Assert-Equal (Join-Path $repo 'docker\dashboard\Start-Dashboard.ps1') (@($w.Sites)[2]).Start
    Assert-Equal 'http://192.168.1.61/' (@($w.Sites)[6]).Check 'the NPM light checks the proxy port, not the tunnel'
    Assert-True ((@($w.Sites)[6]).IsNpm) 'the NPM row'
    Assert-Equal 'website,wiki-macros,ops-pages,ops-cutover,ops-rollback,print-queue,print-queue-rollback,mega-dashboard,mega-dashboard-rollback,portainer-haven,portainer-haven-rollback' (@($w.Deploys.Keys) -join ',') 'cc-P107 added the three ops deploys, cc-P109 the print queue''s two, cc-P110 the mega dashboard''s two, cc-P163 Portainer''s two'
    Assert-Equal 'chase@192.168.1.61' $w.HavenSsh
    Assert-Equal 'http://localhost:8081/|8081|localhost:81' ($w.Npm.Url + '|' + $w.Npm.LocalPort + '|' + $w.Npm.Remote)
    Assert-True (Test-Path -LiteralPath (Join-Path $repo 'scripts\websites.json')) 'the console reads scripts\websites.json'
    Assert-Equal $webText ([IO.File]::ReadAllText((Join-Path $repo 'scripts\websites.json'))) 'the fixture is the real file'
}

Fact 'P101_AConfigMissingAFieldLeavesThatSiteOutAndSaysWhy' {
    $t = Get-WebFixture {
        param($j)
        $j.sites[0].PSObject.Properties.Remove('url')
        $j.sites[2].PSObject.Properties.Remove('container')
        $j.sites[4].deploy = 'haven-ops'
        $j.sites[5].key = 'website'
    }
    $w = ConvertFrom-WebsiteConfig $t $repo
    Assert-Equal 'status,website,npm,printqueue,megadash' ((@($w.Sites) | ForEach-Object { $_.Key }) -join ',') 'the four bad ones are left out, not guessed (printqueue and megadash, cc-P109 and cc-P110, are untouched)'
    $p = @($w.Problems) -join ' | '
    Assert-True ($p -match 'site 1 \(Play tests\): missing or wrong url; left out') $p
    Assert-True ($p -match 'site 3 \(Homepage\): missing or wrong container') $p
    Assert-True ($p -match 'site 5 \(Wiki\): missing or wrong deploy \(haven-ops is not in deploys\)') $p
    Assert-True ($p -match 'site 6 \(Vaultwarden\): missing or wrong key \(used twice\)') $p
    $tile = Get-WebsitesGlance $w (Get-WebResults @{ status = 200; website = 200; npm = 200 })
    Assert-Equal 'amber' $tile.Color 'a broken config is amber even when the rest are up'
    Assert-True ($tile.Detail -match 'websites.json: 4 problem\(s\)') $tile.Detail
    foreach ($bad in @($null, 'not json {', '{ "schema": 2, "sites": [] }')) {
        $x = ConvertFrom-WebsiteConfig $bad $repo
        Assert-Equal 0 @($x.Sites).Count ('no sites from ' + $bad)
        $g = Get-WebsitesGlance $x $null
        Assert-Equal 'UNKNOWN' $g.Word
        Assert-Equal 'gray' $g.Color
    }
    $n = ConvertFrom-WebsiteConfig (Get-WebFixture { param($j) $j.npm.tunnelLocalPort = 80; $j.haven.ssh = 'not a target' }) $repo
    Assert-True ($null -eq $n.Npm) 'a privileged port is refused'
    Assert-True ((@($n.Problems) -join ' | ') -match 'haven.ssh is not user@host') (@($n.Problems) -join ' | ')
}

Fact 'P101_EachResultReadsAsUpDownOrTimeout' {
    $r = Get-WebResults @{ a = 200; b = 301; c = 404; d = 502; e = 'timeout'; f = 'ConnectFailure' }
    Assert-Equal 'up,up,down,down,timeout,down' ((@('a', 'b', 'c', 'd', 'e', 'f') | ForEach-Object { Get-WebsiteState $r[$_] }) -join ',')
    Assert-Equal 'unknown' (Get-WebsiteState $null)
    Assert-Equal 'HTTP 502 in 12 ms' (Format-WebsiteResult $r['d'])
    Assert-Equal 'no answer within 4 s' (Format-WebsiteResult $r['e'])
    Assert-Equal 'no answer: ConnectFailure' (Format-WebsiteResult $r['f'])
}

Fact 'P101_TheTileCountsTheSitesUpAndTurnsAmberNamingTheDownOnes' {
    $w = ConvertFrom-WebsiteConfig $webText $repo
    $all = Get-WebsitesGlance $w (Get-WebResults $webAllUp)
    Assert-Equal 'WEBSITES|9 OF 9 UP|green|all answering' ($all.Title + '|' + $all.Word + '|' + $all.Color + '|' + $all.Detail)
    $codes = $webAllUp.Clone(); $codes.website = 'ConnectFailure'
    $one = Get-WebsitesGlance $w (Get-WebResults $codes)
    Assert-Equal '8 OF 9 UP|amber|down: Public website (no answer: ConnectFailure)' ($one.Word + '|' + $one.Color + '|' + $one.Detail)
    $codes.vault = 'timeout'; $codes.homepage = 502
    $three = Get-WebsitesGlance $w (Get-WebResults $codes)
    Assert-Equal '6 OF 9 UP' $three.Word
    Assert-Equal 'down: Homepage (HTTP 502 in 12 ms), Public website (no answer: ConnectFailure), Vaultwarden (no answer within 4 s)' $three.Detail 'in the file''s order'
    $first = Get-WebsitesGlance $w $null
    Assert-Equal 'CHECKING|gray' ($first.Word + '|' + $first.Color) 'before the first check finishes'
    $missing = Get-WebsitesGlance $w (Get-WebResults @{ playtests = 200 })
    Assert-Equal '1 OF 9 UP' $missing.Word 'a site with no result yet is not counted up'
    $panel = @(Format-WebsitesPanel $w (Get-WebResults $codes) '11:30:00') | ForEach-Object { $_.Text }
    Assert-True ($panel[0] -eq ('WEBSITES : 6 OF 9 UP. ' + $three.Detail)) $panel[0]
    Assert-True (@($panel | Where-Object { $_ -like '`[DOWN`]   Homepage*game PC, sl-dashboard*HTTP 502 in 12 ms' }).Count -eq 1) ($panel -join ' | ')
    Assert-True (@($panel | Where-Object { $_ -like '`[NO ANS`] Vaultwarden*Haven*no answer within 4 s' }).Count -eq 1) ($panel -join ' | ')
    Assert-True (@($panel | Where-Object { $_ -like '*Nginx Proxy Manager*(checked at http://192.168.1.61/)*' }).Count -eq 1) 'the NPM row says what its light checks'
    Assert-True ($panel -contains '           website: D:\UO\haven-migration\Deploy-Website.ps1 (copies the website page, its images and the intro video from docker\website\html to Haven (scp, then chmod))') ($panel -join ' | ')
}

Fact 'P101_DeployIsRefusedForAScriptNotInTheAllowlist' {
    $c = Get-WebConfig
    # A Haven site with no deploy, a game-PC site, and a site whose deploy is not in the list (built by hand: the
    # config reader already drops such a site, so this is the plan's own check, not the reader's).
    Assert-True ((@(Get-ActionPlan -Action 'web.deploy' -Config $c -Site 'vault'))[0].Text -match '^Vaultwarden has no deploy in websites.json\. The allowlist: website, wiki-macros, ops-pages, ops-cutover, ops-rollback, print-queue, print-queue-rollback, mega-dashboard, mega-dashboard-rollback, portainer-haven, portainer-haven-rollback\.$') 'no deploy'
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'web.deploy' -Config $c -Site 'homepage'))[0].Kind 'a game-PC site'
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'web.deploy' -Config $c -Site 'nope'))[0].Kind 'no such site'
    @($c.Websites.Sites | Where-Object { $_.Key -eq 'wiki' })[0].Deploy = 'haven-ops'
    $p = @(Get-ActionPlan -Action 'web.deploy' -Config $c -Site 'wiki')
    Assert-Equal 1 $p.Count; Assert-Equal 'refuse' $p[0].Kind
    Assert-True ($p[0].Text -match 'haven-ops is not in the deploys allowlist') $p[0].Text
    # The safety check holds whatever a plan says: the helper with a name not in the list, or with no phrase.
    $ps = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $c.WebsiteDeployScript)
    $confirm = New-PlanStep -Kind confirm -Phrase 'deploy to haven' -Text 'x'
    $bad = New-PlanStep -Kind window -Exe $c.PowerShell -Arguments ($ps + @('-Deploy', 'haven-ops'))
    Assert-True ((@(Test-PlanSafety @($confirm, $bad) $c) -join ' | ') -match 'only as -Deploy <a name in the deploys allowlist>') 'not in the list'
    $good = New-PlanStep -Kind window -Exe $c.PowerShell -Arguments ($ps + @('-Deploy', 'website'))
    Assert-Equal 0 @(Test-PlanSafety @($confirm, $good) $c).Count (@(Test-PlanSafety @($confirm, $good) $c) -join ' | ')
    Assert-True ((@(Test-PlanSafety @($good) $c) -join ' | ') -match 'only after the typed phrase deploy to haven') 'no phrase'
    $extra = New-PlanStep -Kind window -Exe $c.PowerShell -Arguments ($ps + @('-Deploy', 'website', '-AllowedFolder', 'C:\'))
    Assert-True (@(Test-PlanSafety @($confirm, $extra) $c).Count -ge 1) 'no extra arguments'
    # And the helper refuses on its own, with exit 2, when run against a list that lacks the name.
    $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $c.WebsiteDeployScript -Deploy 'haven-ops' -ConfigFile (Join-Path $fixture 'websites-2026-10-14.json') 2>&1 | Out-String
    Assert-Equal 2 $LASTEXITCODE $out
    Assert-True ($out -match "REFUSED: 'haven-ops' is not in the deploys allowlist") $out
}

Fact 'P101_DeployDryRunPrintsTheExactCommandAndRunsNothing' {
    $c = Get-WebConfig
    $p = @(Get-ActionPlan -Action 'web.deploy' -Config $c -Site 'website')
    Assert-Equal 'confirm,window,log' (($p | ForEach-Object { $_.Kind }) -join ',')
    Assert-Equal 'deploy to haven' $p[0].Phrase
    Assert-Equal 'Deploy to Haven' (Get-ConfirmSubject 'web.deploy')
    Assert-Equal ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File ' + $c.WebsiteDeployScript + ' -Deploy website') (Format-CommandLine $p[1].Exe $p[1].Arguments)
    Assert-Equal 0 @(Test-PlanSafety $p $c).Count (@(Test-PlanSafety $p $c) -join ' | ')
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'web.deploy') 'it changes something: no mode, no run'
    # The helper's own -DryRun, against a stand-in deploy script that would leave a mark if it ran.
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p101-' + [guid]::NewGuid().ToString('N'))
    $allowed = Join-Path $dir 'haven'
    New-Item -ItemType Directory -Path $allowed -Force | Out-Null
    try {
        $mark = Join-Path $dir 'ran.txt'
        Set-Content -LiteralPath (Join-Path $allowed 'Deploy-Stub.ps1') -Value ("Set-Content -LiteralPath '" + $mark + "' -Value ran") -Encoding ASCII
        $cfgFile = Join-Path $dir 'websites.json'
        Get-WebFixture { param($j) $j.deploys.website.script = (Join-Path $allowed 'Deploy-Stub.ps1') } | Set-Content -LiteralPath $cfgFile -Encoding ASCII
        $logs = Join-Path $dir 'logs'
        $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $c.WebsiteDeployScript -Deploy website -DryRun -ConfigFile $cfgFile -LogFolder $logs -AllowedFolder $allowed 2>&1 | Out-String
        Assert-Equal 0 $LASTEXITCODE $out
        Assert-True ($out -match [regex]::Escape("command : & '" + (Join-Path $allowed 'Deploy-Stub.ps1') + "'")) $out
        Assert-True ($out -match 'DRY RUN: nothing was run\.') $out
        Assert-True (-not (Test-Path -LiteralPath $mark)) 'the deploy script did not run'
        Assert-True (-not (Test-Path -LiteralPath $logs)) 'no log folder was made'
        # For real, the same stub runs and its transcript is kept.
        $out2 = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $c.WebsiteDeployScript -Deploy website -ConfigFile $cfgFile -LogFolder $logs -AllowedFolder $allowed 2>&1 | Out-String
        Assert-Equal 0 $LASTEXITCODE $out2
        Assert-True (Test-Path -LiteralPath $mark) 'run for real, it ran'
        $log = @(Get-ChildItem -LiteralPath $logs -Filter '*_website.log')
        Assert-Equal 1 $log.Count 'one transcript'
        # A script outside the allowed folder is refused even when the list names it.
        Get-WebFixture { param($j) $j.deploys.website.script = 'C:\Windows\System32\evil.ps1' } | Set-Content -LiteralPath $cfgFile -Encoding ASCII
        $out3 = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $c.WebsiteDeployScript -Deploy website -DryRun -ConfigFile $cfgFile -AllowedFolder $allowed 2>&1 | Out-String
        Assert-Equal 2 $LASTEXITCODE $out3
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
    Assert-True ((Get-Content -LiteralPath (Join-Path $repo '.gitignore') -Raw) -match '(?m)^\*\.log\s*$') 'deploy logs are *.log, which .gitignore ignores'
}

Fact 'P101_OpenNpmUsesTheConfigUrlAndTunnel' {
    $c = Get-WebConfig (Get-WebFixture { param($j) $j.npm.url = 'http://localhost:9181/'; $j.npm.tunnelLocalPort = 9181; $j.haven.ssh = 'admin@10.0.0.9' })
    foreach ($p in @((Get-ActionPlan -Action 'web.npm' -Config $c), (Get-ActionPlan -Action 'web.open' -Config $c -Site 'npm'))) {
        $p = @($p)
        Assert-Equal 'say,window,launch' (($p | ForEach-Object { $_.Kind }) -join ',')
        Assert-Equal 'ssh -N -L 9181:localhost:81 admin@10.0.0.9' (Format-CommandLine $p[1].Exe $p[1].Arguments)
        Assert-Equal 'explorer.exe|http://localhost:9181/' ($p[2].Path + '|' + $p[2].Arguments[0])
        Assert-Equal 0 @(Test-PlanSafety $p $c).Count (@(Test-PlanSafety $p $c) -join ' | ')
    }
    Assert-Equal 'execute' (Resolve-RunMode -Action 'web.npm') 'only opens'
    $other = New-PlanStep -Kind window -Exe 'ssh' -Arguments @('admin@10.0.0.9', 'rm -rf ~')
    Assert-True ((@(Test-PlanSafety @($other) $c) -join ' | ') -match 'ssh runs here only as NPM''s tunnel') 'no other ssh'
    $none = Get-WebConfig (Get-WebFixture { param($j) $j.PSObject.Properties.Remove('npm') })
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'web.npm' -Config $none))[0].Kind 'no npm in the file: refused, not guessed'
}

Fact 'P101_RestartRedeployAndLogsAreForGamePcSitesAndNeverAShard' {
    $c = Get-WebConfig
    $r = @(Get-ActionPlan -Action 'web.restart' -Config $c -Site 'homepage')
    Assert-Equal 'docker restart sl-dashboard' (Format-CommandLine @($r | Where-Object { $_.Kind -eq 'window' })[0].Exe @($r | Where-Object { $_.Kind -eq 'window' })[0].Arguments)
    Assert-Equal 0 @(Test-PlanSafety $r $c).Count (@(Test-PlanSafety $r $c) -join ' | ')
    $d = @(Get-ActionPlan -Action 'web.redeploy' -Config $c -Site 'homepage')
    Assert-Equal ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File ' + (Join-Path $repo 'docker\dashboard\Start-Dashboard.ps1')) (Format-CommandLine @($d | Where-Object { $_.Kind -eq 'window' })[0].Exe @($d | Where-Object { $_.Kind -eq 'window' })[0].Arguments)
    Assert-Equal 0 @(Test-PlanSafety $d $c).Count (@(Test-PlanSafety $d $c) -join ' | ')
    Assert-True (Test-Path -LiteralPath (Join-Path $repo 'docker\dashboard\Start-Dashboard.ps1')) 'the start script exists'
    $l = @(Get-ActionPlan -Action 'web.logs' -Config $c -Site 'playtests')
    Assert-Equal 'docker logs --tail 200 sl-play-tests' (Format-CommandLine $l[0].Exe $l[0].Arguments)
    foreach ($k in 'web.restart', 'web.redeploy', 'web.logs') { Assert-True ((@(Get-ActionPlan -Action $k -Config $c -Site 'wiki'))[0].Text -match '^Wiki runs on Haven, not this PC') $k }
    foreach ($ct in 'sl-modernuo', 'sl-modernuo-test', 'sl-uo-status') {
        $bad = New-PlanStep -Kind window -Exe 'docker' -Arguments @('restart', $ct)
        Assert-True ((@(Test-PlanSafety @($bad) $c) -join ' | ') -match 'a website restart names only a game-PC site') $ct
    }
    Assert-Equal 'execute' (Resolve-RunMode -Action 'web.logs') 'Logs only reads'
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'web.restart') 'Restart changes something'
}

Fact 'P101_TheProbeReadsUpDownAndNoAnswerFromRealSockets' {
    # Three listeners on 127.0.0.1: one answers 200, one 503, one accepts and never answers. The probe runs in a
    # runspace while this thread answers.
    $ls = @(1..3 | ForEach-Object { $t = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0); $t.Start(); $t })
    $port = { param($i) $ls[$i].LocalEndpoint.Port }
    $checks = @(
        [pscustomobject]@{ Key = 'ok'; Url = ('http://127.0.0.1:' + (& $port 0) + '/') },
        [pscustomobject]@{ Key = 'bad'; Url = ('http://127.0.0.1:' + (& $port 1) + '/') },
        [pscustomobject]@{ Key = 'mute'; Url = ('http://127.0.0.1:' + (& $port 2) + '/') },
        [pscustomobject]@{ Key = 'closed'; Url = 'http://127.0.0.1:1/' })
    $ps = [PowerShell]::Create()
    [void]$ps.AddScript('param($c) function Get-WebsiteProbe {' + (Get-Item 'function:Get-WebsiteProbe').Definition + '}; Get-WebsiteProbe $c 1').AddArgument($checks)
    $h = $ps.BeginInvoke()
    $held = @()
    try {
        $answers = @("HTTP/1.1 200 OK`r`nContent-Length: 2`r`nConnection: close`r`n`r`nok", "HTTP/1.1 503 Service Unavailable`r`nContent-Length: 0`r`nConnection: close`r`n`r`n", $null)
        for ($i = 0; $i -lt 3; $i++) {
            $sw = [Diagnostics.Stopwatch]::StartNew()
            while (-not $ls[$i].Pending() -and $sw.ElapsedMilliseconds -lt 5000) { Start-Sleep -Milliseconds 20 }
            $cl = $ls[$i].AcceptTcpClient()
            $st = $cl.GetStream()
            Start-Sleep -Milliseconds 100
            if ($answers[$i]) { $b = [Text.Encoding]::ASCII.GetBytes($answers[$i]); $st.Write($b, 0, $b.Length); $st.Flush(); $cl.Close() } else { $held += $cl }
        }
        $sw = [Diagnostics.Stopwatch]::StartNew()
        while (-not $h.IsCompleted -and $sw.ElapsedMilliseconds -lt 10000) { Start-Sleep -Milliseconds 50 }
        $r = @($ps.EndInvoke($h))[-1]
        if ($r -is [psobject]) { $r = $r.psobject.BaseObject }
        Assert-Equal 'up|200' ((Get-WebsiteState $r['ok']) + '|' + $r['ok'].Code)
        Assert-Equal 'down|503' ((Get-WebsiteState $r['bad']) + '|' + $r['bad'].Code)
        Assert-Equal 'timeout' (Get-WebsiteState $r['mute'])
        # Windows retries a refused connection for about 2 s, past this 1 s timeout, so a closed port reads either way.
        Assert-True ((Get-WebsiteState $r['closed']) -in @('down', 'timeout')) (Get-WebsiteState $r['closed'])
    } finally {
        foreach ($c in $held) { $c.Close() }
        foreach ($t in $ls) { $t.Stop() }
        $ps.Dispose()
    }
}

Fact 'P101_TheTileAndTheTabAreBuiltAndDrawn' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-True ($null -ne $script:ui.WebsitesTile) 'the seventh tile'
        Assert-Equal 7 $script:ui.Glance.ColumnCount 'Plain: one row of seven'
        Assert-True ($script:ui.WebsitesTile.Word.Text -match '^WEBSITES: (CHECKING|UNKNOWN)$') $script:ui.WebsitesTile.Word.Text
        Assert-Equal 'Websites' $script:ui.WebsitesPage.Text
        Assert-True ($script:ui.Tabs.TabPages.Contains($script:ui.WebsitesPage)) 'in the tab control'
        Assert-Equal 'web.open,web.restart,web.redeploy,web.logs,web.deploy,web.npm,web.ops-pages,web.ops-cutover,web.ops-rollback,web.print-queue,web.print-queue-rollback,web.mega-dashboard,web.mega-dashboard-rollback,web.portainer-haven,web.portainer-haven-rollback' (($script:ui.WebsiteButtons | ForEach-Object { $_.Tag }) -join ',')
        Assert-Equal 'Open,Restart,Redeploy,Logs (last 200),Deploy to Haven,Open NPM,Ops pages: deploy,Ops pages: cut over,Ops pages: roll back,Print queue: deploy,Print queue: roll back,Mega dashboard: deploy,Mega dashboard: roll back,Portainer (Haven): deploy,Portainer (Haven): roll back' (($script:ui.WebsiteButtons | ForEach-Object { $_.Text }) -join ',')
        Assert-Equal @($config.Websites.Sites).Count $script:ui.WebsitePick.Items.Count 'one line per site'
        Assert-Equal 'Play tests  (this PC)' $script:ui.WebsitePick.Items[0]
        $script:ui.WebsitePick.SelectedIndex = 3
        Assert-Equal 'website' (Get-SelectedWebsite)
        # A finished check, drawn.
        $codes = $webAllUp.Clone(); $codes.website = 'ConnectFailure'
        $script:webResults = Get-WebResults $codes; $script:webResultAt = [datetime]::UtcNow
        Update-WebsitesView
        Assert-Equal 'WEBSITES: 8 OF 9 UP' $script:ui.WebsitesTile.Word.Text
        Assert-Equal (Get-TileColors 'amber')[0] $script:ui.WebsitesTile.Panel.BackColor 'amber'
        Assert-True ($script:ui.WebsitesPanel.Text -match '\[DOWN\]   Public website') 'the panel is drawn'
        $tips = Get-ConsoleTooltips
        foreach ($k in 'tile.WEBSITES', 'tab.Websites', 'web.open', 'web.restart', 'web.redeploy', 'web.logs', 'web.deploy', 'web.npm') { Assert-True ($tips.Contains($k)) ('a tooltip for ' + $k) }
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false; $script:webResults = $null; $script:webResultAt = $null }
}

Fact 'P101_NothingSecretIsInTheConfigOrTheNewScripts' {
    foreach ($f in 'scripts\websites.json', 'scripts\Invoke-WebsiteDeploy.ps1', 'docker\dashboard\Start-Dashboard.ps1') {
        $t = [IO.File]::ReadAllText((Join-Path $repo $f))
        Assert-True ($t -notmatch '(?i)"(password|passwd|token|secret|apikey|api_key|private_key)"\s*:') ($f + ': a secret-looking key')
        Assert-True ($t -notmatch '-----BEGIN [A-Z ]*PRIVATE KEY-----') ($f + ': a private key')
        Assert-True ($t -notmatch '(?i)\b(ssh-rsa|ssh-ed25519) AAAA') ($f + ': a key')
        Assert-True ($t -notmatch '(?i)sshpass|-pw\s|PasswordAuthentication|ConvertTo-SecureString') ($f + ': a password route')
    }
}

Fact 'P101_EveryWebsiteButtonRunHeadlessWithDryRunRunsNothing' {
    foreach ($a in @(@('web.open', 'homepage'), @('web.restart', 'homepage'), @('web.redeploy', 'playtests'), @('web.logs', 'status'), @('web.deploy', 'wiki'), @('web.npm', 'npm'))) {
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Resolve-Path $script).Path -DryRun -Action $a[0] -Site $a[1] 2>&1 | Out-String)
        Assert-Equal 0 $LASTEXITCODE ($a[0] + ': ' + $out)
        Assert-True ($out -match ('== DRY RUN: ' + [regex]::Escape($a[0]) + ' \(websites\)')) ($a[0] + ': ' + $out)
        Assert-True ($out -match '\(dry run: nothing was run\)') ($a[0] + ': ' + $out)
        Assert-True ($out -notmatch 'REFUSE') ($a[0] + ': ' + $out)
    }
}

# --- cc-P106: a shard waiting for an answer on its console (D94) --------------------------------------------------
# fixtures\live-shard-question-d94-2026-10-14.txt is `docker logs -t --tail 60 sl-modernuo` read on 2026-10-14 while live
# sat at "Delete the object and continue? (y/n/a)" (bug-list D94), with the two lines naming accounts dropped
# (Remove-AccountLines). The prompt itself is Console.Write with no newline, so docker logs never shows it: the reader
# knows it by the banner and the silence after it. The other questions are the real D89 and healthy-start logs, given
# timestamps here because they were captured without -t.

function Get-P106Plan { param([string]$Action, [string]$Key = 'live', $State) @(Get-ActionPlan -Action $Action -ShardKey $Key -Config $config -State $State -Now $State.Now) }

Fact 'P106_TheD94LogIsADeserializeQuestionWithItsTypeSerialExceptionAndFrame' {
    $q = Get-ShardQuestion -LogText $p106D94 -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20
    Assert-True ($null -ne $q) 'live at 08:06 was waiting'
    Assert-Equal 'object' $q.Kind
    Assert-Equal 'Delete the object and continue? (y/n/a)' $q.Prompt
    Assert-Equal 'Server.ClusterFAccountPersistence' $q.Type
    Assert-Equal '0x40128E03' $q.Serial
    Assert-True ($q.Exception -match '^System\.ArgumentOutOfRangeException: Ticks must be between') $q.Exception
    Assert-Equal 'System.DateTime.ThrowTicksOutOfRange()' $q.Frame
    Assert-True ($q.Frames -contains 'Server.WoodDiscoveryEntry..ctor(IGenericReader r)') 'the frames name the field that failed'
    Assert-Equal '2026-10-14T13:06:17' $q.Since.ToString('yyyy-MM-ddTHH:mm:ss', $script:Invariant) 'since the banner'
    Assert-Equal 'n,y,a' ((@($q.Answers) | ForEach-Object { $_.Key }) -join ',') 'the safe answer first'
}

Fact 'P106_AShardStillTalkingOrStoppedIsNotWaiting' {
    Assert-True ($null -eq (Get-ShardQuestion -LogText $p106D94 -Running $true -Now $p106T0.AddSeconds(5) -QuietSeconds 20)) 'five seconds after the banner it may still be printing'
    Assert-True ($null -eq (Get-ShardQuestion -LogText $p106D94 -Running $false -Now $p106T0.AddMinutes(5) -QuietSeconds 20)) 'a stopped container asks nothing'
    Assert-True ($null -eq (Get-ShardQuestion -LogText '' -Running $true -Now $p106T0 -QuietSeconds 20)) 'no log, no question'
}

Fact 'P106_TheFatalPressReturnIsAQuestionAndAHealthyStartAsksNothing' {
    $q = Get-ShardQuestion -LogText $p106Fatal -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20
    Assert-Equal 'fatal' $q.Kind
    Assert-Equal 'This exception is fatal, press return to exit' $q.Prompt
    Assert-True ($q.Exception -match 'IndexOutOfRangeException') $q.Exception
    Assert-Equal 'enter' ((@($q.Answers) | ForEach-Object { $_.Key }) -join ',')
    Assert-True ($null -eq (Get-ShardQuestion -LogText $p106Healthy -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20)) 'listening, asking nothing'
}

Fact 'P106_AnAnsweredQuestionIsNoLongerWaiting' {
    $later = $p106T0.AddMinutes(2).ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'", $script:Invariant)
    foreach ($after in @('Loading world done (12.3 seconds)', 'Types will not be deleted. An exception will be thrown.', '[SL] Starting Shattered Legacy ModernUO...')) {
        $t = $p106D94 + "`n" + $later + ' ' + $after
        Assert-True ($null -eq (Get-ShardQuestion -LogText $t -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20)) ('then: ' + $after)
    }
}

Fact 'P106_EveryStartupQuestionIsKnownByWhatItPrints' {
    $stamp = { param([string[]]$Lines) Add-P106Stamps ($Lines -join "`n") $p106T0 }
    $cases = @(
        ,@('file', 'Skip this file and continue? (y/n)', 'n,y', @('Loading world', '***** Bad deserialize of /var/lib/uo/modernuo/Saves/Misc/Thing.bin *****', 'System.IO.EndOfStreamException: Unable to read beyond the end of the stream.', '   at System.IO.BinaryReader.ReadByte()'))
        ,@('type', "Delete all of those types? (y/n)", 'n,y', @('Loading world', 'failed'))
        ,@('owner', 'Do you want to create the owner account now? (y/n)', 'n', @('Loading world done', 'Do you want to create the owner account now? (y/n):'))
        ,@('restore', 'Restore a backup, or press Enter to start with no world?', '', @('No valid save data found.', 'Available backups and archives (2 total):', '    1) 2026-10-13 19:25:00  2026-10-13-19-25-00', '    2) 2026-10-12 19:25:00  2026-10-12-19-25-00'))
    )
    foreach ($c in $cases) {
        $q = Get-ShardQuestion -LogText (& $stamp $c[3]) -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20
        Assert-True ($null -ne $q) ($c[0] + ' is a question')
        Assert-Equal $c[0] $q.Kind
        Assert-Equal $c[1] $q.Prompt
        Assert-Equal $c[2] ((@($q.Answers) | ForEach-Object { $_.Key }) -join ',') ($c[0] + ' answers')
    }
}

Fact 'P106_TheAnswerTableSaysWhatEachKeyDoesAndWhichDelete' {
    $q = Get-ShardQuestion -LogText $p106D94 -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20
    $byKey = @{}; foreach ($a in $q.Answers) { $byKey[$a.Key] = $a }
    Assert-Equal 'n' $byKey['n'].Send
    Assert-Equal $false $byKey['n'].Deletes
    Assert-True ($byKey['n'].Explain -match 'press return to exit') $byKey['n'].Explain
    Assert-True ($byKey['y'].Deletes -and $byKey['a'].Deletes) 'y and a delete'
    Assert-True ($byKey['y'].Explain -match 'Server\.ClusterFAccountPersistence 0x40128E03') $byKey['y'].Explain
    Assert-True ($byKey['a'].Explain -match 'every other bad object') $byKey['a'].Explain
    $f = Get-ShardQuestion -LogText $p106Fatal -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20
    Assert-Equal '' $f.Answers[0].Send 'Enter is an empty line'
    Assert-True ($f.Answers[0].Explain -match 'restart') $f.Answers[0].Explain
    $loss = Get-QuestionLoss -Question $q -Config $config
    Assert-True ($loss -match "every account's ClusterF data") $loss
}

Fact 'P106_TheTileSaysWaitingForAnAnswerInAmberWithTheQuestion' {
    $s = New-P106State -Tail $p106D94
    $g = Get-ShardGlance -Shard $config.Shards['live'] -ShardState $s.Shards['live'] -Now $s.Now -Config $config
    Assert-Equal 'WAITING FOR AN ANSWER' $g.Word
    Assert-Equal 'amber' $g.Color
    Assert-True ($g.Detail -match '^Delete the object and continue\? \(y/n/a\) Server\.ClusterFAccountPersistence 0x40128E03: ArgumentOutOfRangeException') $g.Detail
    Assert-True ($g.Detail -match 'Shard question tab') $g.Detail
    $f = New-P106State -Key 'test' -Tail $p106Fatal
    $gt = Get-ShardGlance -Shard $config.Shards['test'] -ShardState $f.Shards['test'] -Now $f.Now -Config $config
    Assert-True ($gt.Word -like 'CRASHED*' -and $gt.Color -eq 'red') ('a fatal stays CRASHED: ' + $gt.Word)
    Assert-True ($gt.Detail -match 'press return') $gt.Detail
}

Fact 'P106_TheHealthCheckNeverCallsAWaitingShardHealthy' {
    $q = Get-ShardQuestion -LogText $p106D94 -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20
    foreach ($h in 'healthy', 'starting', 'unhealthy', '') {
        $r = Get-HealthState -Exists $true -Running $true -Health $h -Crash $null -Question $q
        Assert-Equal 'WAITING' $r.State ('docker said ' + $h)
        Assert-True ($r.Detail -match 'Delete the object and continue') $r.Detail
    }
    Assert-Equal 'HEALTHY' (Get-HealthState -Exists $true -Running $true -Health 'healthy' -Crash $null).State 'no question: as before'
}

Fact 'P106_OneEmailPerQuestion' {
    $s = New-P106State -Tail $p106D94
    $m = @(Get-QuestionMails -State $s -Config $config -Sent @())
    Assert-Equal 1 $m.Count
    Assert-Equal '[Shard] Live is waiting for an answer: Delete the object and continue? (y/n/a)' $m[0].Subject
    Assert-True ($m[0].Body -match 'Server\.ClusterFAccountPersistence' -and $m[0].Body -match 'Shard question tab') $m[0].Body
    Assert-Equal 0 @(Get-QuestionMails -State $s -Config $config -Sent @($m[0].Signature)).Count 'the same question is not mailed twice'
    $s2 = New-P106State -Tail ($p106D94 -replace '13:06:17\.988048937Z', '13:07:17.988048937Z')
    Assert-Equal 1 @(Get-QuestionMails -State $s2 -Config $config -Sent @($m[0].Signature)).Count 'a new question is'
}

Fact 'P106_DeleteAnswersNeedTheTypedPhraseAndAVerifiedBackupFirst' {
    $s = New-P106State -Tail $p106D94
    foreach ($k in 'question.y', 'question.a') {
        $p = Get-P106Plan $k -State $s
        Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'refuse' }).Count ($k + ' is allowed for this question')
        $conf = @($p | Where-Object { $_.Kind -eq 'confirm' })
        Assert-Equal 'delete on live' $conf[0].Phrase
        Assert-True ($conf[0].Text -match "every account's ClusterF data") ('it says what is lost first: ' + $conf[0].Text)
        $kinds = @($p | ForEach-Object { $_.Kind })
        $copy = [array]::IndexOf($kinds, 'copy'); $ver = [array]::IndexOf($kinds, 'verify'); $ans = [array]::IndexOf($kinds, 'answer')
        Assert-True ($copy -ge 0 -and $ver -gt $copy -and $ans -gt $ver) ($k + ': copy, verify, then answer: ' + ($kinds -join ','))
        Assert-True ($p[$copy].To -like ((Join-Path $config.SnapshotRoot 'live') + '\*_pre-answer-*')) $p[$copy].To
        Assert-Equal $config.Shards['live'].Saves $p[$copy].From
        Assert-Equal $k.Substring(9) $p[$ans].Phrase 'the answer step carries the key it sends'
        Assert-Equal 1 @($p | Where-Object { $_.Kind -eq 'log' }).Count 'logged'
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ((Test-PlanSafety $p $config) -join ' | ')
    }
}

Fact 'P106_TheSafetyCheckRefusesADeleteAnswerWithoutItsBackupOrPhrase' {
    $s = New-P106State -Tail $p106D94
    $p = Get-P106Plan 'question.y' -State $s
    $noBackup = @($p | Where-Object { $_.Kind -notin @('copy', 'verify') })
    Assert-True (@(Test-PlanSafety $noBackup $config) -match 'backup').Count 'no backup: refused'
    $noPhrase = @($p | Where-Object { $_.Kind -ne 'confirm' })
    Assert-True (@(Test-PlanSafety $noPhrase $config) -match 'delete on live').Count 'no phrase: refused'
    $weak = @($p | ForEach-Object { if ($_.Kind -eq 'confirm') { $c = $_.PSObject.Copy(); $c.Phrase = 'answer live'; $c } else { $_ } })
    Assert-True (@(Test-PlanSafety $weak $config) -match 'delete on live').Count 'the answer phrase is not enough for a delete'
    $odd = @($p | ForEach-Object { if ($_.Kind -eq 'answer') { $c = $_.PSObject.Copy(); $c.Container = 'sl-ddns'; $c } else { $_ } })
    Assert-True (@(Test-PlanSafety $odd $config) -match 'only to a shard').Count 'an answer goes only to a shard container'
}

Fact 'P106_OtherAnswersAndStopNeedTheirOwnPhrase' {
    $s = New-P106State -Tail $p106D94
    $p = Get-P106Plan 'question.n' -State $s
    Assert-Equal 'answer live' @($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase
    Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('copy', 'verify') }).Count 'n deletes nothing, so nothing to back up'
    Assert-Equal 'n' @($p | Where-Object { $_.Kind -eq 'answer' })[0].Phrase
    Assert-Equal 0 @(Test-PlanSafety $p $config).Count ((Test-PlanSafety $p $config) -join ' | ')
    $st = Get-P106Plan 'question.stop' -State $s
    Assert-Equal 'stop live' @($st | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase
    $x = @($st | Where-Object { $_.Kind -eq 'exec' })[0]
    Assert-Equal 'docker stop -t 60 sl-modernuo' ((@($x.Exe) + $x.Arguments) -join ' ') 'never compose for live (D29)'
    Assert-Equal 0 @(Test-PlanSafety $st $config).Count ((Test-PlanSafety $st $config) -join ' | ')
}

Fact 'P106_AnAnswerTheQuestionDoesNotOfferIsRefused' {
    $f = New-P106State -Key 'test' -Tail $p106Fatal
    foreach ($k in 'question.y', 'question.a', 'question.n') { Assert-True (@(Get-P106Plan $k 'test' $f | Where-Object { $_.Kind -eq 'refuse' }).Count) ($k + ' on a fatal') }
    Assert-Equal 0 @(Get-P106Plan 'question.enter' 'test' $f | Where-Object { $_.Kind -eq 'refuse' }).Count 'Enter is its answer'
    $none = New-P106State -Tail $p106Healthy
    Assert-True (@(Get-P106Plan 'question.n' -State $none | Where-Object { $_.Kind -eq 'refuse' }).Count) 'no question, nothing to answer'
    Assert-Equal 0 @(Get-P106Plan 'question.stop' -State $none | Where-Object { $_.Kind -eq 'refuse' }).Count 'Stop works whenever the shard runs'
}

Fact 'P106_ShowPrintsTheQuestionItsContextAndEveryAnswerAndChangesNothing' {
    $s = New-P106State -Tail $p106D94
    $p = Get-P106Plan 'question.show' -State $s
    $t = (@($p | Where-Object { $_.Kind -eq 'say' }) | ForEach-Object { $_.Text }) -join "`n"
    foreach ($want in 'Delete the object and continue\? \(y/n/a\)', 'Server\.ClusterFAccountPersistence', 'ArgumentOutOfRangeException', '(?m)^  n: ', '(?m)^  y: ', '(?m)^  a: ', "every account's ClusterF data") {
        Assert-True ($t -match $want) ('Show says ' + $want + "`n" + $t)
    }
    Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('confirm', 'exec', 'copy', 'answer') }).Count 'it only reads'
}

Fact 'P106_TheAnswerIsSentOnlyIfTheQuestionIsStillTheSame' {
    $q = Get-ShardQuestion -LogText $p106D94 -Running $true -Now $p106T0.AddMinutes(5) -QuietSeconds 20
    Assert-True (Test-QuestionStillAsked -Signature $q.Signature -Question $q) 'the same'
    $moved = Get-ShardQuestion -LogText ($p106D94 -replace '13:06:17\.988048937Z', '13:09:17.988048937Z') -Running $true -Now $p106T0.AddMinutes(9) -QuietSeconds 20
    Assert-True (-not (Test-QuestionStillAsked -Signature $q.Signature -Question $moved)) 'a later question is another one'
    Assert-True (-not (Test-QuestionStillAsked -Signature $q.Signature -Question $null)) 'gone'
    Assert-Equal ('n' + "`r") (Get-AnswerKeystrokes 'n') 'a key and Enter'
    Assert-Equal "`r" (Get-AnswerKeystrokes '') 'Enter alone'
}

Fact 'P106_EveryQuestionButtonIsOnItsOwnTabWithAPhraseAndATooltip' {
    $acts = @(Get-ConsoleActions | Where-Object { $_.Key -like 'question.*' })
    Assert-Equal 'question.show,question.n,question.y,question.a,question.enter,question.stop' (($acts | ForEach-Object { $_.Key }) -join ',')
    foreach ($a in $acts) {
        Assert-Equal 'Shard question' $a.Group $a.Key
        Assert-True ([string](Get-ConsoleTooltips)[$a.Key]) ($a.Key + ' has a tooltip')
    }
}

Fact 'P106_EveryQuestionButtonRunHeadlessWithDryRunRunsNothing' {
    # A capture with a shard waiting at D94, as -Capture would write it, read by the child exactly as a click runs it.
    # cc-P108: the child reads the real scripts\live.json, so this is the TEST shard (it was live until live retired).
    $capText = Read-P106Text 'console-capture-2026-09-29.txt'
    $capText = $capText -replace 'captured-at \S+', ('captured-at ' + (Format-DockerTime $p106T0.AddMinutes(5)))
    $capText = $capText -replace '(?s)("State": \{\s*"Status": )"\w+",(\s*"Running": )\w+', '$1"running",$2true'
    $capText += "`n" + $script:CaptureMarker + 'logtail sl-modernuo-test #####' + "`n" + $p106D94 + "`n"
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p106-capture-' + [guid]::NewGuid().ToString('N') + '.txt')
    [IO.File]::WriteAllText($tmp, $capText, (New-Object Text.UTF8Encoding $false))
    try {
        foreach ($k in 'question.show', 'question.n', 'question.y', 'question.a', 'question.stop') {
            $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Resolve-Path $script).Path -DryRun -StatusFrom $tmp -Action $k -Shard test 2>&1 | Out-String)
            Assert-Equal 0 $LASTEXITCODE ($k + ': ' + $out)
            Assert-True ($out -match ('== DRY RUN: ' + [regex]::Escape($k))) ($k + ': ' + $out)
            Assert-True ($out -match '\(dry run: nothing was run\)') ($k + ': ' + $out)
            Assert-True ($out -cnotmatch '(?m)^\s*\d+\. REFUSE ') ($k + ': ' + $out)
        }
    } finally { Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue }
}

# --- cc-P107: the ops pages on Haven (H2 and H3) ----------------------------------------------------------------------
# scripts\haven-ops.json is the real file. A mode is a state.json written to a temp folder here; nothing reaches Haven.

$p107Ops = ''
if (Test-Path -LiteralPath (Join-Path $repo 'scripts\haven-ops.json')) { $p107Ops = [IO.File]::ReadAllText((Join-Path $repo 'scripts\haven-ops.json')) }
function New-P107Config {
    # The console's config with its state file in a temp folder, in the given mode ('' for no state file).
    param([string]$Mode)
    $c = Get-ConsoleConfig -RepoRoot $repo
    $c.Ops.StateFile = Join-Path ([IO.Path]::GetTempPath()) ('p107-state-' + [guid]::NewGuid().ToString('N') + '.json')
    if ($Mode) { [IO.File]::WriteAllText($c.Ops.StateFile, ('{"mode": "' + $Mode + '"}')) }
    # As Get-ConsoleConfig does at start, now that the mode is this one.
    Set-OpsSites -Websites $c.Websites -Mode (Get-OpsMode $c)
    $c
}

Fact 'P107_TheRealOpsConfigNamesHavensPagesTheGamePcFallbackAndTheStateFile' {
    $o = ConvertFrom-OpsConfig $p107Ops
    Assert-Equal $null $o.Error
    Assert-Equal 'http://192.168.1.61:8093/' $o.HavenPageUrl
    Assert-Equal 'http://192.168.1.61:8093/status' $o.HavenStatusUrl
    Assert-Equal 'http://192.168.1.61:8094' $o.OpsApi
    Assert-Equal 'http://localhost:8093/' $o.GamePcPageUrl
    Assert-Equal 'D:\UO\haven-ops\state.json' $o.StateFile
    Assert-True ($p107Ops -notmatch '(?i)"(password|secret|key)"\s*:|PLAYTESTS_TOKEN=') 'no secret in the file'
    $none = ConvertFrom-OpsConfig $null
    Assert-Equal '' $none.HavenPageUrl 'no file: the game PC only'
}

Fact 'P107_TheModeIsHavenOnlyWhenTheCutoverWroteIt' {
    Assert-Equal 'local' (Get-OpsMode $config -StateText $null) 'no state file'
    Assert-Equal 'haven' (Get-OpsMode $config -StateText '{"mode": "haven", "since": "2026-10-14T15:00:00-05:00"}')
    Assert-Equal 'local' (Get-OpsMode $config -StateText '{"mode": "local"}')
    Assert-Equal 'local' (Get-OpsMode $config -StateText 'not json')
    Assert-Equal 'local' (Get-OpsMode $config -StateText '{"mode": "HAVEN"}') 'only exactly haven'
}

Fact 'P107_LinksAndSitesPointAtHavenAfterTheCutoverAndAtThisPcBefore' {
    $before = Get-OpsLinks $config 'local'
    Assert-Equal 'http://localhost:8093/' $before.PlayTests
    Assert-Equal 'http://localhost:8093/status' $before.Status
    $after = Get-OpsLinks $config 'haven'
    Assert-Equal 'http://192.168.1.61:8093/' $after.PlayTests
    Assert-Equal 'http://192.168.1.61:8093/status' $after.Status
    Assert-Equal 'http://localhost:8093/' $after.Fallback 'the game PC stays the fallback'
    $h = New-P107Config 'haven'
    try {
        $p = @(Get-ActionPlan -Action 'links.playtests' -Config $h -State $state)
        Assert-Equal 'http://192.168.1.61:8093/' $p[0].Arguments[0]
        Assert-True ($p[0].Text -match 'on Haven' -and $p[0].Text -match 'ops-rollback') $p[0].Text
        Assert-Equal 'http://192.168.1.61:8093/status' @(Get-ActionPlan -Action 'links.status' -Config $h -State $state)[0].Arguments[0]
        $site = @($h.Websites.Sites | Where-Object { $_.Key -eq 'playtests' })[0]
        Assert-Equal 'http://192.168.1.61:8093/' $site.Url
        Assert-Equal 'haven' $site.Where 'Restart and Redeploy (game PC only) no longer apply'
        Assert-Equal 'http://localhost:3000/' @($h.Websites.Sites | Where-Object { $_.Key -eq 'homepage' })[0].Url 'Homepage does not move (H5)'
    } finally { Remove-Item -LiteralPath $h.Ops.StateFile -ErrorAction SilentlyContinue }
    $l = New-P107Config ''
    Assert-Equal 'http://localhost:8093/' @(Get-ActionPlan -Action 'links.playtests' -Config $l -State $state)[0].Arguments[0] 'no state file: as before'
    Assert-Equal 'pc' @($l.Websites.Sites | Where-Object { $_.Key -eq 'playtests' })[0].Where
}

Fact 'P107_ThePlayTestsTileFallsBackToTheMirrorWhenHavenDoesNotAnswer' {
    $h = New-P107Config 'haven'
    try {
        $h.Ops.HavenPageUrl = 'http://127.0.0.1:9/'      # nothing listens on the discard port
        $h.PlayTestsFile = Join-Path $fixture 'play-tests-2026-10-12.json'
        $g = Get-PlayTestGlanceNow $h
        Assert-True ($g.Detail -match 'from the last commit \(Haven unreachable\)$') $g.Detail
        Assert-True ($g.Word -ne 'UNKNOWN') ('the mirror is read: ' + $g.Word)
    } finally { Remove-Item -LiteralPath $h.Ops.StateFile -ErrorAction SilentlyContinue }
    $l = New-P107Config ''
    $l.PlayTestsFile = Join-Path $fixture 'play-tests-2026-10-12.json'
    Assert-True ((Get-PlayTestGlanceNow $l).Detail -notmatch 'Haven') 'before the cutover the tile is as it was'
}

Fact 'P107_TheThreeOpsDeploysAreInTheAllowlistAndRunOnlyTheirLaunchers' {
    $w = $config.Websites
    foreach ($k in 'ops-pages', 'ops-cutover', 'ops-rollback') {
        Assert-True $w.Deploys.Contains($k) ($k + ' is a deploy')
        Assert-True ($w.Deploys[$k].Script -like 'D:\UO\haven-migration\Deploy-HavenOps-*.ps1') $w.Deploys[$k].Script
    }
    foreach ($f in 'Pages', 'Cutover', 'Rollback') {
        $t = [IO.File]::ReadAllText('D:\UO\haven-migration\Deploy-HavenOps-' + $f + '.ps1')
        $mode = $(if ($f -eq 'Pages') { 'Deploy' } else { $f })
        Assert-True ($t -match ([regex]::Escape("& 'D:\ShatteredLegacy\docker\ops\Deploy-HavenOps.ps1' -Mode " + $mode))) ('the ' + $f + ' launcher runs only its mode')
    }
    foreach ($k in 'web.ops-pages', 'web.ops-cutover', 'web.ops-rollback') {
        $p = @(Get-ActionPlan -Action $k -Config $config -State $state)
        Assert-Equal 'confirm,window,log' (($p | ForEach-Object { $_.Kind }) -join ',') $k
        Assert-Equal 'deploy to haven' $p[0].Phrase
        Assert-Equal ('-Deploy ' + $k.Substring(4)) ((@($p[1].Arguments) | Select-Object -Last 2) -join ' ')
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ((Test-PlanSafety $p $config) -join ' | ')
        Assert-True ([string](Get-ConsoleTooltips)[$k]) ($k + ' has a tooltip')
    }
    $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\Invoke-WebsiteDeploy.ps1') -Deploy ops-cutover -DryRun 2>&1 | Out-String)
    Assert-Equal 0 $LASTEXITCODE $out
    Assert-True ($out -match 'Deploy-HavenOps-Cutover\.ps1' -and $out -match 'DRY RUN: nothing was run') $out
}

Fact 'P107_TheHavenScriptsRefuseSystemHaveADryRunAndKeepTheTokenOffTheCommandLine' {
    $d = [IO.File]::ReadAllText((Join-Path $repo 'docker\ops\Deploy-HavenOps.ps1'))
    foreach ($want in 'IsSystem', '\[switch\]\$DryRun', "Confirm-Go 'deploy'", "Confirm-Go 'cut over'", "Confirm-Go 'roll back'", 'Read-Host -AsSecureString', '-InputText \(''PLAYTESTS_TOKEN=''', 'umask 077') {
        Assert-True ($d -match $want) ('Deploy-HavenOps.ps1 has ' + $want)
    }
    Assert-True ($d -notmatch 'Write-Host[^\r\n]*\$tok\b') 'the token is never printed'
    foreach ($s in 'Publish-GamePcSnapshot.ps1', 'Install-GamePcSnapshotTask.ps1') {
        $t = [IO.File]::ReadAllText((Join-Path $repo ('scripts\' + $s)))
        Assert-True ($t -match 'IsSystem' -and $t -match '\[switch\]\$DryRun') ($s + ': refuses SYSTEM, has -DryRun')
    }
    $c = [IO.File]::ReadAllText((Join-Path $repo 'docker\ops\docker-compose.yml'))
    Assert-True ($c -match '\$\{BIND:-192\.168\.1\.61\}:\$\{PAGE_PORT:-8093\}:8000' -and $c -match '\$\{BIND:-192\.168\.1\.61\}:\$\{LAN_PORT:-8094\}:8001') 'LAN address only'
    Assert-True ($c -match '/repo:ro' -and $c -match 'PLAYTESTS_DATA: /data/ops') 'clones read only; data apart'
    Assert-True ($c -notmatch 'PLAYTESTS_TOKEN:') 'the token comes from ops.env, not the file'
}

Fact 'P107_TheSnapshotSendsNothingBeforeTheCutoverAndDryRunPrintsItsJson' {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p107-ops-' + [guid]::NewGuid().ToString('N') + '.json')
    $j = $p107Ops | ConvertFrom-Json
    $j.stateFile = Join-Path ([IO.Path]::GetTempPath()) 'p107-no-such-state.json'
    [IO.File]::WriteAllText($tmp, ($j | ConvertTo-Json -Depth 5))
    try {
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\Publish-GamePcSnapshot.ps1') -Config $tmp 2>&1 | Out-String)
        Assert-Equal 0 $LASTEXITCODE $out
        Assert-True ($out -match 'not cut over to Haven \(mode local\): nothing sent') $out
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\Publish-GamePcSnapshot.ps1') -Config $tmp -DryRun 2>&1 | Out-String)
        Assert-Equal 0 $LASTEXITCODE $out
        Assert-True ($out -match 'DRY RUN: would POST \d+ bytes to http://192\.168\.1\.61:8094/api/ingest/gamepc') $out
        $json = @(($out -split "`n") | Where-Object { $_ -match '^\{' })[0] | ConvertFrom-Json
        Assert-True ($json.golem.PSObject.Properties.Name -contains 'queue' -and $json.PSObject.Properties.Name -contains 'containers') 'the Golem and the containers'
        # cc-P116: "registered nothing" is the task as it was before the dry run, not "no task": Chase has since
        # installed it for real (cc-P107 For Chase step 2), and the old "no such task" assertion then always failed.
        $taskXml = { try { Export-ScheduledTask -TaskName 'SL game PC snapshot' -ErrorAction Stop } catch { '(none)' } }
        $taskBefore = & $taskXml
        $i = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\Install-GamePcSnapshotTask.ps1') -DryRun 2>&1 | Out-String)
        Assert-True ($i -match 'RepetitionInterval \(New-TimeSpan -Minutes 1\)' -and $i -match 'DRY RUN: nothing registered') $i
        Assert-Equal $taskBefore (& $taskXml) 'the dry run registered nothing: the task is exactly as it was'
    } finally { Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue }
}

Fact 'P107_TheMenuFollowsTheHostThatServedItAndHomepageTriesHavenFirst' {
    $nav = [IO.File]::ReadAllText((Join-Path $repo 'docker\test-queue\html\nav.js'))
    Assert-True ($nav -match 'var host = p\.host \|\| SCRIPT_HOST \|\| window\.location\.hostname \|\| FALLBACK_HOST;') 'host null: the host that served nav.js'
    Assert-True ($nav -match "key: 'dashboard', label: 'Dashboard',    host: '192\.168\.1\.61', port: 3000") 'cc-P110 (H5): the menu''s Dashboard is Haven''s Homepage'
    Assert-True ($nav -match "key: 'playtests', label: 'Play tests',   host: null, port: 8093") 'Play tests follows the page'
    $cj = [IO.File]::ReadAllText((Join-Path $repo 'docker\dashboard\config\custom.js'))
    Assert-True ($cj -match "NAV_AT = \[\{ host: '192\.168\.1\.61', port: 8093 \}, \{ host: null, port: 8093 \}\]" -and $cj -match 'onerror') 'Haven first, then this PC'
}

# --- cc-P108: live retired. scripts\live.json liveEnabled false greys it; true brings it back -------------------------
# The capture (console-capture-2026-09-29.txt) still holds live's sections, which is the point: a retired live must ignore
# them. $p108Off and $p108On are the same repo's config with the switch pinned each way.

$p108Off = Get-ConsoleConfig -RepoRoot $repo -LiveEnabled $false
$p108On  = Get-ConsoleConfig -RepoRoot $repo -LiveEnabled $true
$p108Line = 'retired 2026-10-14 (returns as a fresh 1.0 world)'

Fact 'P108_TheRepoFileSaysLiveIsRetired' {
    $c = Get-ConsoleConfig -RepoRoot $repo -LiveEnabled $null
    Assert-Equal $false $c.LiveEnabled 'scripts\live.json liveEnabled'
    Assert-True ($c.Shards['live'].Retired -and -not $c.Shards['test'].Retired) 'live retired, test not'
    Assert-Equal $p108Line $c.Live.Line
    Assert-True ($null -eq $c.Live.Problem) ('the file reads cleanly: ' + $c.Live.Problem)
    Assert-True ($c.Live.Refusal -like 'Live is retired*') $c.Live.Refusal
}

Fact 'P108_TheSwitchReadsTrueAndFalseAndAnythingElseIsRetired' {
    Assert-True (ConvertFrom-LiveSwitchJson '{"liveEnabled": true}').Enabled 'true'
    Assert-True (-not (ConvertFrom-LiveSwitchJson '{"liveEnabled": false}').Enabled) 'false'
    foreach ($bad in @($null, '', 'not json', '{}', '{"liveEnabled": "true"}', '{"liveEnabled": 1}')) {
        $s = ConvertFrom-LiveSwitchJson $bad
        Assert-True (-not $s.Enabled -and $s.Problem) ('retired, and says why, for: ' + $bad)
    }
}

Fact 'P108_RetiredLiveIsOneGreyTileAndNoChecks' {
    $st = New-ConsoleState -Config $p108Off -Sections $cap
    $t = @(Get-AtAGlance $st $p108Off)[0]
    Assert-Equal 'LIVE' $t.Title
    Assert-Equal 'RETIRED' $t.Word
    Assert-Equal 'gray' $t.Color
    Assert-Equal $p108Line $t.Detail
    $text = (@(Format-StatusReport -State $st -Config $p108Off) | ForEach-Object { $_.Text }) -join "`n"
    Assert-True ($text -match ('(?m)^LIVE\s+: RETIRED\s+' + [regex]::Escape($p108Line) + '$')) $text
    Assert-True ($text -notmatch '== LIVE shard') 'no LIVE section'
    Assert-True ($text -notmatch 'LIVE COMPOSE FILE') 'no compose warning'
    Assert-True ($text -cnotmatch 'sl-modernuo(?![-:])') ('nothing names the live container: ' + $text)
    Assert-True ($text -notmatch '(?i)live[^\n]*(down|missing|unknown|no container)') 'live is never down, missing or unknown'
}

Fact 'P108_LiveEnabledTrueShowsLiveExactlyAsBefore' {
    $st = New-ConsoleState -Config $p108On -Sections $cap
    $t = @(Get-AtAGlance $st $p108On)[0]
    Assert-Equal 'LIVE' $t.Title
    Assert-True ($t.Word -ne 'RETIRED') $t.Word
    $text = (@(Format-StatusReport -State $st -Config $p108On) | ForEach-Object { $_.Text }) -join "`n"
    Assert-True ($text -match '== LIVE shard: sl-modernuo ') 'the LIVE section is back'
    Assert-True ($st.Shards['live'].Container.Exists -and -not $st.Shards['live'].Retired) 'read from the capture'
}

Fact 'P108_ARetiredLiveIgnoresWhatACaptureSaysAboutIt' {
    $sec = [ordered]@{}
    foreach ($k in $cap.Keys) { $sec[$k] = $cap[$k] }
    $sec['inspect sl-modernuo'] = 'error during connect: open //./pipe/docker_engine: The system cannot find the file specified.'
    $sec['logtail sl-modernuo'] = $p106D94
    $off = New-ConsoleState -Config $p108Off -Sections $sec
    $l = $off.Shards['live']
    Assert-True ($l.Retired -and -not $l.Container.Exists -and $null -eq $l.Question -and -not $l.Save.Exists -and -not @($l.Snapshots).Count) 'nothing read'
    Assert-True ($null -eq $off.DockerError) 'a live read error is not a docker error'
    $on = New-ConsoleState -Config $p108On -Sections $sec
    Assert-True ($null -ne $on.DockerError) 'enabled, the same section is a docker error, as before'
}

Fact 'P108_EveryLiveActionRefusesWhileRetired' {
    $st = New-ConsoleState -Config $p108Off -Sections $cap
    $acts = @(@(Get-ConsoleActions | Where-Object { $_.Key -like 'live.*' } | ForEach-Object { $_.Key }) +
              @('snapshot.create', 'snapshot.restore', 'snapshot.delete', 'snapshot.list', 'question.show', 'question.n', 'question.y', 'question.a', 'question.enter', 'question.stop'))
    Assert-Equal 15 $acts.Count ('the five live.* buttons and ten snapshot and question actions: ' + ($acts -join ','))
    foreach ($a in $acts) {
        $plan = @(Get-ActionPlan -Action $a -ShardKey 'live' -Config $p108Off -State $st -SnapshotName 'x' -Snapshot 'y')
        Assert-Equal 1 $plan.Count ($a + ': one step')
        Assert-Equal 'refuse' $plan[0].Kind $a
        Assert-True ($plan[0].Text -like 'Live is retired*') ($a + ': ' + $plan[0].Text)
    }
    $on = New-ConsoleState -Config $p108On -Sections $cap
    $plan = @(Get-ActionPlan -Action 'live.restart' -Config $p108On -State $on)
    Assert-True (-not @($plan | Where-Object { $_.Text -like 'Live is retired*' }).Count -and @($plan | Where-Object { $_.Kind -eq 'confirm' }).Count) 'enabled: the typed confirmation, as before'
    $plan = @(Get-ActionPlan -Action 'snapshot.list' -ShardKey 'test' -Config $p108Off -State $st)
    Assert-True (-not @($plan | Where-Object { $_.Kind -eq 'refuse' }).Count) 'the test shard is untouched by the switch'
}

Fact 'P108_StartFreshRefusesWhileRetiredBecauseItsAccountsCameFromLive' {
    $st = New-ConsoleState -Config $p108Off -Sections $cap
    $plan = @(Get-ActionPlan -Action 'test.fresh' -Config $p108Off -State $st)
    Assert-Equal 1 $plan.Count
    Assert-True ($plan[0].Kind -eq 'refuse' -and $plan[0].Text -match 'owner account prompt \(D19\)' -and $plan[0].Text -match 'Wipe test accounts') $plan[0].Text
    $on = New-ConsoleState -Config $p108On -Sections $cap
    Assert-True (@(Get-ActionPlan -Action 'test.fresh' -Config $p108On -State $on | Where-Object { $_.Kind -eq 'move' }).Count) 'enabled: moves the save aside, as before'
}

Fact 'P108_NoQuestionEmailForARetiredLive' {
    $sec = [ordered]@{}
    foreach ($k in $cap.Keys) { $sec[$k] = $cap[$k] }
    $sec['meta'] = 'captured-at ' + (Format-DockerTime $p106T0.AddMinutes(5)) + "`nsource capture"
    $sec['inspect sl-modernuo'] = $sec['inspect sl-modernuo'] -replace '(?s)("State": \{\s*"Status": )"\w+",(\s*"Running": )\w+', '$1"running",$2true'
    $sec['logtail sl-modernuo'] = $p106D94
    $on = New-ConsoleState -Config $p108On -Sections $sec
    Assert-True ($null -ne $on.Shards['live'].Question) 'enabled: live is waiting at D94'
    Assert-Equal 1 @(Get-QuestionMails -State $on -Config $p108On).Count 'enabled: one email, as before'
    $off = New-ConsoleState -Config $p108Off -Sections $sec
    Assert-Equal 0 @(Get-QuestionMails -State $off -Config $p108Off).Count 'retired: none'
    Assert-Equal 0 @(Get-QuestionMails -State $on -Config $p108Off).Count 'retired, even given a state that has the question'
}

Fact 'P108_TheDockerReadNeverNamesARetiredLive' {
    $script:p108Reads = New-Object 'System.Collections.Generic.List[string]'
    # Shims for this fact only (PowerShell resolves functions through the caller's scope): no docker, no network.
    function Invoke-DockerRead { param([string[]]$Arguments, [int]$TimeoutMs) $script:p108Reads.Add($Arguments -join ' '); [pscustomobject]@{ Out = ''; Err = 'Error: No such object'; Code = 1 } }
    function Get-FeedHttpCode { param($Url) '' }
    $sec = Get-DockerSections -Config $p108Off
    $reads = $script:p108Reads -join "`n"
    Assert-True ($reads -match 'inspect sl-modernuo-test') ('the test shard is read: ' + $reads)
    # 'images sl-modernuo' lists the image repository the test shard runs from; no read names the live container.
    $named = @($script:p108Reads | Where-Object { $_ -notlike 'images sl-modernuo *' -and $_ -cmatch '(^| )sl-modernuo( |$)' })
    Assert-Equal 0 $named.Count ('reads naming sl-modernuo: ' + ($named -join ' | '))
    Assert-True (-not @($sec.Keys | Where-Object { $_ -match '^(inspect|logs|logtail) sl-modernuo$| live$' }).Count) ('no live section: ' + (@($sec.Keys) -join ', '))
    $script:p108Reads.Clear()
    [void](Get-DockerSections -Config $p108On)
    Assert-True (($script:p108Reads -join "`n") -match '(?m)^inspect sl-modernuo$') 'enabled: live is read, as before'
}

Fact 'P108_TheWindowGreysEveryLiveControlAndPicksTheTestShard' {
    $script:NetProbeDisabled = $true
    foreach ($mode in 'off', 'on') {
        $cfg = $(if ($mode -eq 'off') { $p108Off } else { $p108On })
        $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
        try {
            $all = New-Object 'System.Collections.Generic.List[object]'
            $stack = New-Object System.Collections.Stack; $stack.Push($form)
            while ($stack.Count) { $x = $stack.Pop(); $all.Add($x); foreach ($k in $x.Controls) { $stack.Push($k) } }
            $liveCtl = @($all | Where-Object { ($_.Tag -is [string] -and ($_.Tag -like 'live.*' -or $_.Tag -eq 'test.fresh')) -or $_.Name -in @('qLive', 'snapLive') })
            Assert-Equal 8 $liveCtl.Count ('five live buttons, Start fresh, and the two LIVE choices: ' + (@($liveCtl | ForEach-Object { [string]$_.Tag + $_.Name }) -join ','))
            $page = @($script:ui.Tabs.TabPages | Where-Object { $_.Text -eq 'LIVE shard' })[0]
            Assert-True ($null -ne $page) 'the LIVE shard tab is still there'
            if ($mode -eq 'off') {
                foreach ($x in $liveCtl) {
                    Assert-True (-not $x.Enabled) ([string]$x.Tag + $x.Name + ' is disabled')
                    Assert-True ($script:ui.ToolTip.GetToolTip($x) -like 'Live is retired*') ([string]$x.Tag + $x.Name + ': ' + $script:ui.ToolTip.GetToolTip($x))
                }
                Assert-True ($script:ui.Tiles[0].Word.Text -match 'RETIRED') $script:ui.Tiles[0].Word.Text
                Assert-Equal 'test' (Get-SelectedQuestionShard) 'Shard question picks the test shard'
                Assert-Equal 'test' (Get-SelectedSnapshotShard) 'Snapshots picks the test shard'
                Assert-True ((@($page.Controls[0].Controls | Where-Object { $_ -is [Windows.Forms.Label] }) | ForEach-Object { $_.Text }) -join ' ' -match ('LIVE ' + [regex]::Escape($p108Line))) 'the tab caption says it'
            } else {
                foreach ($x in $liveCtl) { Assert-True $x.Enabled ([string]$x.Tag + $x.Name + ' is enabled, as before') }
                Assert-Equal 'live' (Get-SelectedQuestionShard) 'Shard question picks LIVE first, as before'
            }
        } finally { $form.Dispose() }
    }
    $script:NetProbeDisabled = $false
}

Fact 'P108_TheProbeListsTheTestShardOnlyUnlessLiveIsEnabled' {
    $probeDir = Join-Path $repo 'docker\uo-status'
    $py = "import sys; sys.path.insert(0, r'" + $probeDir + "'); import probe; print(probe.targets(False)); print(probe.targets(True)); print(probe.LIVE_ENABLED)"
    $out = (& python -I -c $py 2>&1 | Out-String)
    Assert-Equal 0 $LASTEXITCODE $out
    $l = @($out -split "`r?`n" | Where-Object { $_ })
    Assert-Equal "[('test', 'Test shard', 2594)]" $l[0]
    Assert-Equal "[('live', 'Live shard', 2593), ('test', 'Test shard', 2594)]" $l[1]
    Assert-Equal 'False' $l[2] 'SL_LIVE_ENABLED unset reads as retired'
    $dc = [IO.File]::ReadAllText((Join-Path $probeDir 'docker-compose.yml'))
    Assert-True ($dc -match 'SL_LIVE_ENABLED: \$\{SL_LIVE_ENABLED:-0\}') 'compose passes it, retired by default'
    $su = [IO.File]::ReadAllText((Join-Path $probeDir 'Start-UoStatus.ps1'))
    Assert-True ($su -match 'Get-LiveSwitch' -and $su -match 'env:SL_LIVE_ENABLED') 'Start-UoStatus.ps1 sets it from scripts\live.json'
}

Fact 'P108_NothingCanComposeTheOldLiveContainer' {
    $uo = [IO.File]::ReadAllText((Join-Path $repo 'docker\uo\docker-compose.yml'))
    Assert-True ($uo -notmatch '(?m)^\s+container_name:\s*sl-modernuo\s*$' -and $uo -notmatch '(?m)^  modernuo:') 'docker/uo/docker-compose.yml has no live service'
    Assert-True ($uo -match '(?m)^  ddns-updater:' -and $uo -match 'container_name: sl-ddns') 'it keeps sl-ddns'
    $ret = [IO.File]::ReadAllText((Join-Path $repo 'docker\uo\retired\docker-compose.live.yml'))
    Assert-True ($ret -notmatch '(?m)^services:' -and $ret -match '(?m)^x-retired-live:') 'the retired file has no services: key'
    Assert-True ($ret -match 'RETIRED 2026-10-14') 'and says why and when'
}

Fact 'P108_TheLiveDeployAndPublishPathsRefuseWhileRetired' {
    # Copies of the two scripts beside a live.json that says false, so the refusal is proved without any chance of a build,
    # a docker exec or a restart, whatever the real file says.
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p108-' + [guid]::NewGuid().ToString('N'))
    $sd = Join-Path $tmp 'scripts'
    New-Item -ItemType Directory -Path $sd -Force | Out-Null
    try {
        foreach ($f in 'Deploy-StatusPublisher.ps1', 'Publish-PatchNotes.ps1', 'Live-Switch.ps1') { Copy-Item (Join-Path $repo ('scripts\' + $f)) $sd }
        [IO.File]::WriteAllText((Join-Path $sd 'live.json'), '{"liveEnabled": false, "retiredOn": "2026-10-14", "returns": "returns as a fresh 1.0 world"}')
        $o = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $sd 'Deploy-StatusPublisher.ps1') 2>&1 | Out-String)
        Assert-Equal 2 $LASTEXITCODE $o
        Assert-True ($o -match 'Live is retired' -and $o -match 'nothing was built or restarted') $o
        $o = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $sd 'Publish-PatchNotes.ps1') -Container sl-modernuo 2>&1 | Out-String)
        Assert-Equal 2 $LASTEXITCODE $o
        Assert-True ($o -match 'Live is retired' -and $o -notmatch 'Version in') $o
    } finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}

# --- cc-P109: the Sunflower print queue's one-click Haven deploys ------------------------------------------------------
# The print queue's code is Sunflower's, in C:\dev\sunflower\print-queue (outside both shard repos); the console only
# allowlists its two launchers in D:\UO\haven-migration, as cc-P107 did for the ops pages.

Fact 'P109_ThePrintQueueDeploysAreAllowlistedAndRunOnlyTheirLaunchers' {
    $w = $config.Websites
    $s = @($w.Sites | Where-Object { $_.Key -eq 'printqueue' })[0]
    Assert-True ($s -and $s.Where -eq 'haven' -and $s.Url -eq 'http://192.168.1.61:8787/' -and $s.Deploy -eq 'print-queue') 'the Print queue row, on Haven, LAN only'
    foreach ($k in 'print-queue', 'print-queue-rollback') {
        Assert-True $w.Deploys.Contains($k) ($k + ' is a deploy')
        Assert-True ($w.Deploys[$k].Script -like 'D:\UO\haven-migration\Deploy-PrintQueue*.ps1') $w.Deploys[$k].Script
    }
    foreach ($f in @(@('Deploy-PrintQueue.ps1', 'Deploy'), @('Deploy-PrintQueue-Rollback.ps1', 'Rollback'))) {
        $t = [IO.File]::ReadAllText('D:\UO\haven-migration\' + $f[0])
        Assert-True ($t -match ([regex]::Escape("& 'C:\dev\sunflower\print-queue\haven\Deploy-PrintQueue.ps1' -Mode " + $f[1]) + '\r?\n')) ($f[0] + ' runs only its mode')
    }
    foreach ($k in 'web.print-queue', 'web.print-queue-rollback') {
        $p = @(Get-ActionPlan -Action $k -Config $config -State $state)
        Assert-Equal 'confirm,window,log' (($p | ForEach-Object { $_.Kind }) -join ',') $k
        Assert-Equal 'deploy to haven' $p[0].Phrase
        Assert-Equal ('-Deploy ' + $k.Substring(4)) ((@($p[1].Arguments) | Select-Object -Last 2) -join ' ')
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ((Test-PlanSafety $p $config) -join ' | ')
        Assert-True ([string](Get-ConsoleTooltips)[$k]) ($k + ' has a tooltip')
        Assert-Equal 'Print queue on Haven' (Get-ConfirmSubject $k)
    }
    $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\Invoke-WebsiteDeploy.ps1') -Deploy print-queue -DryRun 2>&1 | Out-String)
    Assert-Equal 0 $LASTEXITCODE $out
    Assert-True ($out -match 'Deploy-PrintQueue\.ps1' -and $out -match 'DRY RUN: nothing was run') $out
}

Fact 'P109_TheDeployScriptRefusesSystemPinsTheHostKeyAndNeverCopiesChasesKey' {
    $d = [IO.File]::ReadAllText('C:\dev\sunflower\print-queue\haven\Deploy-PrintQueue.ps1')
    Assert-True ($d -match 'IsSystem\) \{ Write-Host .REFUSED') 'refuses SYSTEM'
    Assert-True ($d -match '\[switch\]\$DryRun' -and $d -match "'DRY RUN: stopping here; nothing was changed\.'") 'has a dry run'
    Assert-True ($d -match "Mode = 'Discover'" -and $d -match 'Confirm-Go .deploy.' -and $d -match 'Confirm-Go .roll back.') 'Discover by default; typed words before Deploy and Rollback'
    Assert-True ($d -match 'StrictHostKeyChecking yes' -and $d -match 'ssh-keygen -F') 'the host key is pinned from known_hosts, never accepted blindly'
    Assert-True ($d -notmatch 'accept-new|StrictHostKeyChecking=no|StrictHostKeyChecking no') 'no blind acceptance'
    Assert-True ($d -notmatch '(?i)\.ssh\\id_|id_ed25519|id_rsa') 'Chase''s key is never named, so never copied'
    Assert-True ($d -match 'ssh-keygen -q -t ed25519 -N "" -C "sunflower print queue on haven') 'the container''s own key is made on Haven'
    $gi = [IO.File]::ReadAllText('C:\dev\sunflower\print-queue\.gitignore')
    foreach ($x in 'data/', 'data.moved-*/', 'logs/', 'keys/') { Assert-True ($gi -match ('(?m)^' + [regex]::Escape($x) + '$')) ('the print queue repo ignores ' + $x) }
}

# --- cc-P110: the one Homepage on Haven (docker\dashboard\haven) ----------------------------------------------------

Fact 'P110_TheMegaDashboardDeploysAreAllowlistedAndRunOnlyTheirLaunchers' {
    $w = $config.Websites
    $s = @($w.Sites | Where-Object { $_.Key -eq 'megadash' })[0]
    Assert-True ($s -and $s.Where -eq 'haven' -and $s.Url -eq 'http://192.168.1.61:3000/' -and $s.Deploy -eq 'mega-dashboard') 'the Mega dashboard row, on Haven'
    foreach ($f in @(@('Deploy-MegaDashboard.ps1', 'Deploy', 'mega-dashboard'), @('Deploy-MegaDashboard-Rollback.ps1', 'Rollback', 'mega-dashboard-rollback'))) {
        Assert-Equal ('D:\UO\haven-migration\' + $f[0]) $w.Deploys[$f[2]].Script $f[2]
        $t = [IO.File]::ReadAllText('D:\UO\haven-migration\' + $f[0])
        Assert-True ($t -match ([regex]::Escape("& 'D:\ShatteredLegacy\docker\dashboard\haven\Deploy-MegaDashboard.ps1' -Mode " + $f[1]) + '\r?\n')) ($f[0] + ' runs only its mode')
    }
    foreach ($k in 'web.mega-dashboard', 'web.mega-dashboard-rollback') {
        $p = @(Get-ActionPlan -Action $k -Config $config -State $state)
        Assert-Equal 'confirm,window,log' (($p | ForEach-Object { $_.Kind }) -join ',') $k
        Assert-Equal 'deploy to haven' $p[0].Phrase
        Assert-Equal ('-Deploy ' + $k.Substring(4)) ((@($p[1].Arguments) | Select-Object -Last 2) -join ' ')
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ((Test-PlanSafety $p $config) -join ' | ')
        Assert-True ([string](Get-ConsoleTooltips)[$k]) ($k + ' has a tooltip')
        Assert-Equal 'Mega dashboard on Haven' (Get-ConfirmSubject $k)
    }
}

Fact 'P110_TheHavenHomepageIsPinnedLanOnlyAndReadsDockerOnlyThroughTheProxy' {
    $d = Join-Path $repo 'docker\dashboard\haven'
    $c = [IO.File]::ReadAllText((Join-Path $d 'docker-compose.yml'))
    Assert-True ($c -match 'image: ghcr\.io/gethomepage/homepage:v1\.13\.1' -and $c -match 'image: tecnativa/docker-socket-proxy:v0\.4\.1') 'both images pinned'
    $hp = ($c -split '(?m)^  dockerproxy:')[0]
    Assert-True ($hp -notmatch 'docker\.sock') 'Homepage itself never mounts the Docker socket'
    Assert-True ($c -match 'CONTAINERS: 1' -and $c -match 'POST: 0' -and $c -match '/var/run/docker\.sock:/var/run/docker\.sock:ro') 'the proxy: containers only, no POST, socket read-only'
    $ah = @($c -split "`n" | Where-Object { $_ -match '^\s+HOMEPAGE_ALLOWED_HOSTS:' })
    Assert-True ($ah.Count -eq 1 -and $ah[0] -notmatch 'shatteredlegacyuo\.com' -and $ah[0] -match '192\.168\.1\.61:3000') ('LAN names only in HOMEPAGE_ALLOWED_HOSTS: ' + $ah)
    $dk = [IO.File]::ReadAllText((Join-Path $d 'config\docker.yaml'))
    Assert-True ($dk -match '(?m)^haven:\s*$' -and $dk -match 'host: dockerproxy' -and $dk -notmatch '2375[\s\S]*2375' -and $dk -notmatch 'host\.docker\.internal|192\.168\.1\.58') 'one Docker: Haven''s, through the proxy'
    $sv = [IO.File]::ReadAllText((Join-Path $d 'config\services.yaml'))
    # cc-P133: the five sections of the inventory replace P110's three groups (P133_TheDashboardIsGenerated... checks them).
    foreach ($g in '- Sunflower:', '- Filament:', '- Monitoring:', '- "Ultima Online / Shattered Legacy":', '- Admin:') { Assert-True ($sv.Contains($g)) $g }
    Assert-True ($sv -match '- "Public progress page":' -and $sv -notmatch 'Migration status') 'decision 11'
    Assert-True ($sv -notmatch 'server: my-docker') 'the game PC''s Docker is not read from Haven (decision 6)'
    Assert-True ($sv -match 'Bearer \{\{HOMEPAGE_VAR_OPS_TOKEN\}\}') 'the token comes from dashboard.env (cc-P133: Spoolman''s address is known, so P110''s URL placeholders are gone)'
    $s = [IO.File]::ReadAllText((Join-Path $d 'Deploy-MegaDashboard.ps1'))
    Assert-True ($s -match 'IsSystem\) \{ Write-Host .REFUSED' -and $s -match "'DRY RUN: stopping here; nothing was changed\.'" -and $s -match "Mode = 'Discover'") 'refuses SYSTEM, has a dry run, Discover by default'
    Assert-True ($s -match 'docker ps (-a )?--format' -and $s -match 'listening ports') 'Discover prints Haven''s containers and listening ports'
    Assert-True ($s -match 'api/revalidate' -and $s -match "notmatch 'Ultima Online / Shattered Legacy'") 'the deploy rebuilds the page and checks it carries this config (the proof found the image''s sample page served first)'
    Assert-True ($s -match "StaleName -eq 'sl-dashboard' -or \`$PcDashboard -eq 'sl-dashboard'") 'the proof can never stop a real sl-dashboard'
}

# --- cc-P113: the window icon is the desktop shortcut's ----------------------------------------------------------------

Fact 'P113_TheIconIsTheShortcutsFileResolvedFromTheRepoRoot' {
    $want = Join-Path $repo 'docker\website\html\favicon.ico'
    Assert-Equal $want $config.WindowIconFile 'from the repo root, not an absolute path'
    Assert-True (Test-Path -LiteralPath $want -PathType Leaf) 'the file is in the repo'
    $lnk = 'C:\Users\ckfle\Desktop\Shard Console.lnk'
    if (Test-Path -LiteralPath $lnk) {
        $loc = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk).IconLocation
        Assert-True ($loc -like ('*\docker\website\html\favicon.ico,0')) ('the same file as the shortcut: ' + $loc)
    }
}

Fact 'P113_TheMainFormAndEveryDialogGetTheIcon' {
    $script:ConsoleIcons = @{}
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        $icon = Get-ConsoleIcon $config
        Assert-True ($null -ne $icon) 'the icon loads'
        Assert-True ([object]::ReferenceEquals($form.Icon, $icon)) 'the main form shows it'
        Assert-Equal 1 @($script:ConsoleIcons.Keys).Count 'loaded once and shared'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
    # The three dialogs block on ShowDialog, so their wiring is read: every form the console makes is followed by the helper.
    $src = [IO.File]::ReadAllLines($script)
    $made = 0
    for ($i = 0; $i -lt $src.Length; $i++) {
        if ($src[$i] -match '^\s*(\$\w+) = New-Object Windows\.Forms\.Form\s*$') {
            $made++
            $var = $Matches[1]   # before -notmatch below, which overwrites $Matches
            $next = @($src[($i + 1)..($i + 2)] | Where-Object { $_ -notmatch '^\s*#' })[0]
            Assert-True ($next -match ('^\s*Set-ConsoleIcon ' + [regex]::Escape($var) + ' ')) ('line ' + ($i + 1) + ' makes a form without the icon')
        }
    }
    Assert-Equal 4 $made 'the main form and three dialogs (Show-TypedConfirm, Show-ListPicker, Show-WipeAdminPicker)'
}

Fact 'P113_AMissingIconKeepsTheDefaultAndLogsOneLine' {
    $script:ConsoleIcons = @{}
    $log = Join-Path ([IO.Path]::GetTempPath()) ('p113-' + [guid]::NewGuid().ToString('N') + '.log')
    $c = [pscustomobject]@{ WindowIconFile = (Join-Path ([IO.Path]::GetTempPath()) 'no-such-icon.ico'); LogFile = $log }
    $f = New-Object Windows.Forms.Form
    try {
        $before = $f.Icon
        Set-ConsoleIcon $f $c
        Set-ConsoleIcon $f $c
        Assert-True ([object]::ReferenceEquals($f.Icon, $before)) 'the default icon stays'
        $lines = @(Get-Content -LiteralPath $log)
        Assert-Equal 1 $lines.Count ('one line, however many forms: ' + ($lines -join ' | '))
        Assert-True ($lines[0] -match 'icon not loaded: no file at .*no-such-icon\.ico$') $lines[0]
        $bad = Join-Path ([IO.Path]::GetTempPath()) ('p113-' + [guid]::NewGuid().ToString('N') + '.ico')
        [IO.File]::WriteAllText($bad, 'not an icon')
        $c.WindowIconFile = $bad
        Set-ConsoleIcon $f $c
        Assert-True ([object]::ReferenceEquals($f.Icon, $before)) 'an unreadable file keeps the default too'
        Assert-True ((@(Get-Content -LiteralPath $log)[1]) -match '^.* icon not loaded: ') 'and logs its own line'
        Remove-Item -LiteralPath $bad -ErrorAction SilentlyContinue
    } finally { $f.Dispose(); Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue; $script:ConsoleIcons = @{} }
}

# --- cc-P116: every tab page scrolls when its content does not fit; the GOLEM tile counts the queue folder ----------
# The form is shown off screen (it has to be, for the layout to run), at the console's minimum and default sizes, in
# the UO look with the Action output pane (a normal start) and in Plain without it (the facts' forms). A control is
# reachable when every ancestor up to its tab page either shows it inside its client area or scrolls (AutoScroll)
# with a viewport at least one button tall.
$script:P116Sizes = @(
    @{ Look = 'UO'; Pane = $true; W = 900; H = 700 }, @{ Look = 'UO'; Pane = $true; W = 1200; H = 1000 },
    @{ Look = 'Plain'; Pane = $false; W = 900; H = 700 }, @{ Look = 'Plain'; Pane = $false; W = 1200; H = 1000 })

function Show-P116Form {
    param([string]$Look, [bool]$Pane, [int]$W, [int]$H)
    $script:NetProbeDisabled = $true
    $f = New-ConsoleForm -Config $config -DryRun -Theme $Look -Pane:$Pane -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    $f.StartPosition = 'Manual'
    $f.Location = New-Object Drawing.Point(-8000, 0)
    $f.ShowInTaskbar = $false
    $f.Size = New-Object Drawing.Size($W, $H)
    $f.Show()
    [Windows.Forms.Application]::DoEvents()
    $f
}

function Close-P116Form($f) {
    if ($script:ui.Pane -and $script:ui.Pane.Timer) { $script:ui.Pane.Timer.Stop() }
    $f.Close(); $f.Dispose(); $script:NetProbeDisabled = $false
}

function Get-P116Unreachable {
    param($Page)
    $bad = New-Object 'System.Collections.Generic.List[string]'
    $stack = New-Object System.Collections.Stack
    foreach ($c in $Page.Controls) { $stack.Push($c) }
    while ($stack.Count) {
        $c = $stack.Pop()
        foreach ($k in $c.Controls) { $stack.Push($k) }
        if (-not ($c -is [Windows.Forms.ButtonBase] -or $c -is [Windows.Forms.TextBox] -or $c -is [Windows.Forms.ComboBox])) { continue }
        if (-not $c.Visible -or ($c -is [Windows.Forms.TextBox] -and $c.Multiline)) { continue }
        $x = $c; $why = ''
        while ($x -ne $Page -and -not $why) {
            $p = $x.Parent
            if (($p -is [Windows.Forms.ScrollableControl]) -and $p.AutoScroll) {
                if ($p.ClientSize.Height -lt [Math]::Min(36, $x.Height) -or $p.ClientSize.Width -lt 150) { $why = 'its scrolling parent is ' + $p.ClientSize }
            } else {
                $r = $x.Bounds; $cr = $p.ClientRectangle
                if ($r.Bottom -gt $cr.Bottom -or $r.Right -gt $cr.Right -or $r.Top -lt 0 -or $r.Left -lt 0) { $why = 'outside ' + $p.GetType().Name + ' ' + $cr.Size + ' at ' + $r }
            }
            $x = $p
        }
        if ($why) { $bad.Add(('[' + ($c.Text -replace '\s+', ' ') + '] ' + $why)) }
    }
    $bad.ToArray()
}

Fact 'P116_EveryTabScrollsOrFitsAtTheMinimumAndDefaultSize' {
    # Chase, 2026-10-14 20:12, 1187x1200: "Can't see all of the Golem tab menu, no scroll bar".
    foreach ($sz in $script:P116Sizes) {
        $f = Show-P116Form @sz
        try {
            $where = $sz.Look + $(if ($sz.Pane) { '+pane' } else { '' }) + ' ' + $sz.W + 'x' + $sz.H
            foreach ($pg in $script:ui.Tabs.TabPages) {
                $script:ui.Tabs.SelectedTab = $pg
                [Windows.Forms.Application]::DoEvents()
                $bad = @(Get-P116Unreachable $pg)
                Assert-Equal 0 $bad.Count ($where + ', ' + $pg.Text + ': ' + ($bad -join '; '))
            }
            if ($sz.Look -eq 'UO') {
                # cc-P87's one column is the window's width, so no tab runs off the right edge either.
                Assert-True ($script:ui.Tabs.Right -le $f.ClientSize.Width) ($where + ': the tabs are ' + $script:ui.Tabs.Width + ' wide in a ' + $f.ClientSize.Width + ' window')
            }
        } finally { Close-P116Form $f }
    }
}

Fact 'P116_TheGolemButtonsShowAtTheDefaultSizeAndScrollIntoViewAtTheMinimum' {
    $f = Show-P116Form -Look 'UO' -Pane $true -W 1200 -H 1000
    try {
        $pg = $script:ui.AutopilotPage
        $script:ui.Tabs.SelectedTab = $pg
        [Windows.Forms.Application]::DoEvents()
        Assert-Equal 0 $pg.VerticalScroll.Value 'the page opens at its top, caption and all, not scrolled to its first button'
        foreach ($b in @($script:ui.AutopilotButtons)) {
            $r = $pg.RectangleToClient($b.RectangleToScreen($b.ClientRectangle))
            Assert-True ($r.Top -ge 0 -and $r.Bottom -le $pg.ClientSize.Height) ('default size, no scrolling: [' + $b.Text + '] at ' + $r + ' in a page ' + $pg.ClientSize)
        }
    } finally { Close-P116Form $f }
    $f = Show-P116Form -Look 'UO' -Pane $true -W 900 -H 700
    try {
        $pg = $script:ui.AutopilotPage
        $script:ui.Tabs.SelectedTab = $pg
        [Windows.Forms.Application]::DoEvents()
        $btns = @($pg.Controls | ForEach-Object { $_.Controls } | Where-Object { $_ -is [Windows.Forms.Button] })
        $last = $btns | Sort-Object { $_.Bottom }, { $_.Right } | Select-Object -Last 1
        Assert-True ($pg.AutoScroll -and $pg.VerticalScroll.Visible) ('the page scrolls at the minimum: ' + $pg.ClientSize + ', need ' + $pg.AutoScrollMinSize)
        $pg.ScrollControlIntoView($last)
        [Windows.Forms.Application]::DoEvents()
        $r = $pg.RectangleToClient($last.RectangleToScreen($last.ClientRectangle))
        Assert-True ($r.Top -ge 0 -and $r.Bottom -le $pg.ClientSize.Height) ('the bottom button [' + $last.Text + '] scrolled into view: ' + $r + ' in ' + $pg.ClientSize)
    } finally { Close-P116Form $f }
}

if (-not ('P116Wheel' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class P116Wheel { [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l); }'
}

Fact 'P116_TheMouseWheelOverAPageScrollsThatPage' {
    $f = Show-P116Form -Look 'UO' -Pane $true -W 900 -H 700
    try {
        $pg = $script:ui.AutopilotPage
        $script:ui.Tabs.SelectedTab = $pg
        [Windows.Forms.Application]::DoEvents()
        $b = @($script:ui.AutopilotButtons)[0]
        $before = $pg.VerticalScroll.Value
        $pt = $b.PointToScreen((New-Object Drawing.Point(5, 5)))
        $l = [IntPtr](($pt.Y -band 0xFFFF) * 65536 + ($pt.X -band 0xFFFF))
        # WM_MOUSEWHEEL, one notch down (-120 in the high word), as Windows sends it to the control under the mouse.
        [void][P116Wheel]::SendMessage($b.Handle, 0x020A, [IntPtr]([int]0xFF880000), $l)
        [Windows.Forms.Application]::DoEvents()
        Assert-True ($pg.VerticalScroll.Value -gt $before) ('one notch over a button scrolls its page: ' + $before + ' -> ' + $pg.VerticalScroll.Value)
    } finally { Close-P116Form $f }
}

Fact 'P116_LongCaptionsWrapToThePageWidth' {
    foreach ($sz in @(@{ Look = 'UO'; Pane = $true; W = 900; H = 700 }, @{ Look = 'Plain'; Pane = $false; W = 900; H = 700 })) {
        $f = Show-P116Form @sz
        try {
            foreach ($pg in $script:ui.Tabs.TabPages) {
                $script:ui.Tabs.SelectedTab = $pg
                [Windows.Forms.Application]::DoEvents()
                $stack = New-Object System.Collections.Stack
                foreach ($c in $pg.Controls) { $stack.Push($c) }
                while ($stack.Count) {
                    $c = $stack.Pop()
                    foreach ($k in $c.Controls) { $stack.Push($k) }
                    if ($c -isnot [Windows.Forms.Label] -or $c.MaximumSize.Width -lt 300 -or -not $c.Visible) { continue }
                    $r = $pg.RectangleToClient($c.RectangleToScreen($c.ClientRectangle))
                    Assert-True ($r.Right -le $pg.ClientSize.Width) ($sz.Look + ' ' + $pg.Text + ': a caption ' + $r.Width + ' wide runs to ' + $r.Right + ' in a page ' + $pg.ClientSize.Width + ' wide')
                }
            }
        } finally { Close-P116Form $f }
    }
}

Fact 'P116_TheGolemTileCountsTheQueueFolderWhileStatusJsonIsStale' {
    # 2026-10-14 20:12: "queue empty" while D:\UO\cc-prompts\queue held P114 and P115. status.json is written by the
    # hook and the watch task only, so it goes stale while the Golem rests; the folder is listed on every refresh.
    $root = Join-Path ([IO.Path]::GetTempPath()) ('p116-' + [guid]::NewGuid().ToString('N'))
    $queue = Join-Path $root 'queue'
    New-Item -ItemType Directory -Path $queue -Force | Out-Null
    try {
        $c = [pscustomobject]@{ Autopilot = (New-AutopilotConfig $root $queue -Kind golem) }
        [IO.File]::WriteAllText((Join-Path $root 'ENABLED'), '')
        foreach ($state in @('IDLE', 'OFF')) {
            [IO.File]::WriteAllText((Join-Path $root 'status.json'), ('{ "schema": 1, "state": "' + $state + '", "enabled": ' + $(if ($state -eq 'OFF') { 'false' } else { 'true' }) + ', "queued": [], "generatedAt": "2026-10-14T18:00:00" }'))
            Get-ChildItem -LiteralPath $queue -File | Remove-Item
            Assert-True ((Get-AutopilotViewNow $c).Tile.Detail -match 'queue empty') ($state + ': an empty folder is empty')
            foreach ($n in 'cc-P114-test-shortcuts-our-build.md', 'cc-P115-playtest-who-and-how.md', 'notes for later.md') {
                [IO.File]::WriteAllText((Join-Path $queue $n), '# a prompt')
            }
            $t = (Get-AutopilotViewNow $c).Tile
            Assert-Equal $state $t.Word
            Assert-True ($t.Detail -match '(^|; )2 queued') ($state + ': the two numbered files, though status.json still says none: ' + $t.Detail)
            Assert-True ($t.Detail.EndsWith('; 1 unnumbered file(s) ignored')) ($state + ': ' + $t.Detail)
        }
        # The folder cannot be listed: status.json's own list is the best there is.
        $gone = [pscustomobject]@{ Autopilot = (New-AutopilotConfig $root (Join-Path $root 'no-such-queue') -Kind golem) }
        [IO.File]::WriteAllText((Join-Path $root 'status.json'), '{ "schema": 1, "state": "IDLE", "enabled": true, "queued": ["cc-P120-x.md"] }')
        Assert-True ((Get-AutopilotViewNow $gone).Tile.Detail -match '^1 queued') 'no folder listing: status.json''s list'
    } finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}

# --- cc-P124: the Player package tab ships our client build, and shows it ----------------------------------------------
# A made-up client folder (a TazUO.exe and an sl-build.txt) in a temp folder; the patch list is the repo's own.

function New-P124Client {
    param([string]$Name, [switch]$Stale, [switch]$NoSlBuild)
    $d = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p124-' + $Name + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $d 'TazUO.exe'), 'x')
    $rp = Get-RepoClientPatches $repo
    $lines = @(('TazUO upstream ' + $rp.Upstream), 'built 2026-10-15 09:00') + @($rp.Patches | ForEach-Object { 'patch ' + $_.Name + ' sha256 ' + $_.Sha256 })
    if ($Stale) { $lines = $lines[0..2] }
    if (-not $NoSlBuild) { [IO.File]::WriteAllLines((Join-Path $d 'sl-build.txt'), [string[]]$lines) }
    $d
}

Fact 'P124_TheConsoleShipsOurClientBuildByDefault' {
    $c = Get-ConsoleConfig -RepoRoot $repo
    Assert-Equal 'D:\UO\tazuo-sl-build\client' $c.PackageClient 'the Player package tab and Build-PlayerPackage.ps1 read the one default'
    Assert-Equal (Get-PackageClientDefault) $c.PackageClient 'from PackageClient.ps1'
}

Fact 'P124_ThePackageTabShowsTheClientFolderAndItsSlBuildTxt' {
    $script:NetProbeDisabled = $true
    $good = New-P124Client 'tab'
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.PackageClient = $good
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        $box = $script:ui.PackageClient
        Assert-True ($null -ne $box) 'the Player package tab has the client box'
        Assert-True ($box.Text.Contains($good)) ('it names the folder: ' + $box.Text)
        foreach ($l in [IO.File]::ReadAllLines((Join-Path $good 'sl-build.txt'))) { Assert-True ($box.Text.Contains($l)) ('and shows sl-build.txt line ' + $l + ': ' + $box.Text) }
        Assert-True ($box.Text -match 'TazUO [0-9a-f]{8} \+ \d+ patch') ('and the version: ' + $box.Text)
        # A stale build says so in the tab, before Build runs.
        $cfg.PackageClient = New-P124Client 'tabstale' -Stale
        Update-PackageList
        Assert-True ($script:ui.PackageClient.Text -match 'STALE CLIENT BUILD') ('stale: ' + $script:ui.PackageClient.Text)
        $cfg.PackageClient = New-P124Client 'tabstock' -NoSlBuild
        Update-PackageList
        Assert-True ($script:ui.PackageClient.Text -match 'has no sl-build\.txt') ('stock: ' + $script:ui.PackageClient.Text)
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

Fact 'P124_ABuildPlanNamesTheClientAndRefusesAStaleOne' {
    $good = Get-PackageClientFacts -ClientDir (New-P124Client 'plan') -RepoRoot $repo
    $p = Get-Plan 'package.build' -PackageNotes 'x' -PackageFacts (New-PackageFacts -Client $good)
    Assert-Equal 'say,script,checkzip,log' (@($p | ForEach-Object { $_.Kind }) -join ',') 'the same steps'
    $say = @($p | Where-Object { $_.Kind -eq 'say' })[0].Text
    Assert-True ($say.Contains($good.Dir) -and $say -match 'TazUO [0-9a-f]{8} \+ \d+ patches') ('the plan names the client: ' + $say)
    $stale = Get-PackageClientFacts -ClientDir (New-P124Client 'planstale' -Stale) -RepoRoot $repo
    $ref = @(Get-Plan 'package.build' -PackageNotes 'x' -PackageFacts (New-PackageFacts -Client $stale) | Where-Object { $_.Kind -eq 'refuse' })
    Assert-Equal 1 $ref.Count 'a stale client is refused before the script runs'
    Assert-True ($ref[0].Text -match 'STALE CLIENT BUILD' -and $ref[0].Text -match 'Build-Client\.ps1') $ref[0].Text
    $r = Invoke-ClickCapture 'package.build' '' $false -PackageNotes 'x' -PackageFacts (New-PackageFacts -Client $stale)
    Assert-Equal 0 $r.Spawned.Count 'no window'
}

# --- cc-P125: the Golem's long run, winding down, usage bars and what each prompt is -----------------------------------
# Status texts and prompt files are written out here; the plans point at D:\UO\golem's scripts (the Golem config), and
# nothing is run.

$p125Now = [datetime]::new(2026, 10, 15, 9, 45, 0)
function New-P125Status {
    param([string]$State = 'RUNNING', [string]$Current = 'P125', [string]$Winding = '', [string]$RestReason = '', [string]$RestAfter = '',
        [string]$WaitingFor = '', [bool]$StopTold = $false, $LongRun = $null)
    $o = [ordered]@{
        schema = 1; generatedAt = $p125Now.AddMinutes(-1).ToString('s'); state = $State; enabled = $true; current = $Current
        currentFile = $(if ($Current) { 'cc-' + $Current + '-golem-long-run-toggle.md' } else { '' }); startedAt = $p125Now.AddMinutes(-42).ToString('s')
        position = 3; total = 8; runDone = 2; runStarted = $p125Now.AddHours(-3).ToString('s'); maxPerRun = 12; lastHandled = 124; queued = @('cc-P126-a.md')
        stepsDone = 2; stepsTotal = 9; stepNow = 'Console buttons'; lastActivityUtc = $p125Now.ToUniversalTime().AddMinutes(-1).ToString('s'); lastTool = 'Bash'
        toolStartedUtc = ''; needsYou = ''; note = ''; winding = $Winding; stopTold = $StopTold; restReason = $RestReason; restAfter = $RestAfter
        waitingForModel = $WaitingFor; longRun = $LongRun
    }
    $o | ConvertTo-Json -Depth 4
}
$p125Prompt = "# CC task P125: A ""Long run"" toggle for the Golem, for overnight and weekend runs`n`n**Model:** Opus`n`n" +
    "Written 2026-10-15 by the overseer. Chase wants to maximize output.`nMore of the same paragraph.`n`n**Kind:** golem.ps1 and the console. Medium.`n`n## Before you start`n"
function New-P125View {
    param([string]$StatusText, $Extra)
    if (-not $Extra) { $Extra = [pscustomobject]@{ PromptTexts = @{ 'cc-P125-golem-long-run-toggle.md' = $p125Prompt }; TreeClean = $null } }
    Get-AutopilotView -Enabled $true -StateText '{"current":"P125","currentFile":"cc-P125-golem-long-run-toggle.md","lastHandled":124}' -LogLines @() -QueueNames @('cc-P126-a.md') -Errors @() -StatusText $StatusText -Kind golem -Extra $Extra
}
$p125Cfg = Get-ConsoleConfig -RepoRoot $repo
$p125Cfg.Autopilot = New-AutopilotConfig 'D:\UO\golem' 'D:\UO\cc-prompts\queue' -Kind golem
$p125Facts = [pscustomobject]@{ EmailConfigured = $true; CredStored = $true; LogExists = $true; Present = @{}; Running = 'P125'; StatusText = (New-P125Status); Enabled = $true; QueueNames = @('cc-P126-a.md'); SettingsText = $null; Extra = $null }
foreach ($f in @($p125Cfg.Autopilot.LongRunStart, $p125Cfg.Autopilot.LongRunEnd, $p125Cfg.Autopilot.WrapUp, $p125Cfg.Autopilot.StopPoint, $p125Cfg.Autopilot.WakeScript, $p125Cfg.Autopilot.AutoWakeScript, $p125Cfg.Autopilot.NightShiftScript, $p125Cfg.Autopilot.CliCheckScript)) { if ($f) { $p125Facts.Present[$f] = $true } }

Fact 'P125_APromptParsesItsTitleModelKindAndFirstParagraph' {
    $i = Get-PromptInfo 'cc-P125-golem-long-run-toggle.md' $p125Prompt
    Assert-Equal 125 $i.Number
    Assert-Equal 'A "Long run" toggle for the Golem, for overnight and weekend runs' $i.Title
    Assert-Equal 'Opus' $i.Model
    Assert-Equal 'golem.ps1 and the console. Medium.' $i.Kind
    Assert-Equal 'Written 2026-10-15 by the overseer. Chase wants to maximize output. More of the same paragraph.' $i.Para
    $odd = Get-PromptInfo 'cc-P126-odd.md' "no heading here`n"
    Assert-Equal 'cc-P126-odd.md' $odd.Title 'an odd file shows its file name'
    Assert-Equal 'cc-P127-gone.md' (Get-PromptInfo 'cc-P127-gone.md' $null).Title 'and a missing one too'
    Assert-True (Get-PromptInfo 'cc-P140-cont-x.md' '# CC task P140: x').Cont 'a continuation is marked'
}

Fact 'P125_TheNotesHeadlineIsTheFirstBulletUnderHeadlines' {
    $n = "# cc-P124: x`n`n## Headlines`n`n- **Build package now ships our client.** More.`n- second`n`n## What changed`n- not this`n"
    Assert-Equal 'Build package now ships our client. More.' (Get-NotesHeadline $n)
    Assert-Equal '' (Get-NotesHeadline "# no headlines`n- a bullet")
}

Fact 'P125_TheQueueIsListedInTheGolemsOrderContinuationsFirst' {
    $q = Get-AutopilotQueue @('cc-P101-b.md', 'cc-P140-cont-z.md', 'cc-P99-a.md', 'cc-P130-cont-y.md')
    Assert-Equal 'cc-P130-cont-y.md,cc-P140-cont-z.md,cc-P99-a.md,cc-P101-b.md' ($q.Runs -join ',')
}

Fact 'P125_UsageBarsShowKnownStaleAndUnknownData' {
    $reset = [DateTimeOffset]::new($p125Now.AddHours(2).AddMinutes(14)).ToUnixTimeSeconds()
    $week = [DateTimeOffset]::new($p125Now.AddDays(5)).ToUnixTimeSeconds()
    $fresh = '{ "updatedUtc": "' + $p125Now.ToUniversalTime().AddMinutes(-3).ToString('s') + '", "fiveHour": { "pct": 12, "resetsAt": ' + $reset + ' }, "sevenDay": { "pct": 8, "resetsAt": ' + $week + ' } }'
    $r = @(Get-UsageBars -UsageText $fresh -Now $p125Now)
    Assert-Equal 'head,Current session,This week,Fable this week' (($r | ForEach-Object { $_.Name }) -join ',') 'the app''s order'
    Assert-True ($r[1].Text -match '12% used  resets in 2 h 14 m, at 11:59') $r[1].Text
    Assert-True ($r[1].Text -match '^Current session\s+\[##\.{18}\]') ('a bar: ' + $r[1].Text)
    Assert-True ($r[2].Text -match '8% used  resets in 5 d 0 h, at Tue 09:45') $r[2].Text
    Assert-True ($r[3].Text -match 'not reported' -and $r[3].Color -eq 'gray') 'Fable: Claude Code does not report it'
    $old = $fresh.Replace($p125Now.ToUniversalTime().AddMinutes(-3).ToString('s'), $p125Now.ToUniversalTime().AddMinutes(-25).ToString('s'))
    $s = @(Get-UsageBars -UsageText $old -Now $p125Now)
    Assert-True ($s[1].Text -match 'unknown' -and $s[1].Color -eq 'gray' -and $null -eq $s[1].Pct) ('older than 10 min: ' + $s[1].Text)
    Assert-True ($s[0].Text -match 'too old') $s[0].Text
    $none = @(Get-UsageBars -UsageText $null -Now $p125Now)
    Assert-True ($none[1].Text -match 'unknown' -and $none[0].Text -match 'not reported yet') $none[0].Text
}

Fact 'P125_TheEstimateIsLabelledAndHasNoPercentage' {
    $r = @(Get-UsageBars -UsageText $null -Now $p125Now -Estimate ([pscustomobject]@{ Tokens = 1234567; Sessions = 2 }))
    $e = @($r | Where-Object { $_.Name -eq 'estimate' })
    Assert-Equal 1 $e.Count
    Assert-True ($e[0].Text.StartsWith('estimate, not Anthropic''s figure: 1,234,567 tokens')) $e[0].Text
    Assert-True ($e[0].Text -notmatch '\d+%') 'no percentage'
}

# cc-P160: the plan usage page (plan-usage.json, the overseer's reader of claude.ai/settings/usage) on the Golem tab: the
# Fable week, a pace marker in each bar, the pace warning, when the reader ran, the free reset and the credits.
$p160Now = [datetime]::new(2026, 10, 17, 18, 45, 0)
function New-P160Usage([double]$Week = 53) {
    $five = [DateTimeOffset]::new([datetime]::new(2026, 10, 17, 21, 50, 0)).ToUnixTimeSeconds()
    $wk = [DateTimeOffset]::new([datetime]::new(2026, 10, 21, 16, 0, 0)).ToUnixTimeSeconds()
    '{ "updatedUtc": "' + $p160Now.ToUniversalTime().AddMinutes(-1).ToString('s') + '", "fiveHour": { "pct": 14, "resetsAt": ' + $five + ' }, "sevenDay": { "pct": ' + $Week + ', "resetsAt": ' + $wk + ' } }'
}
function New-P160Plan([double]$AgeHours = (2 / 60), [string]$Warning = 'Heads up. At this pace you will run out Tuesday afternoon, before the reset.') {
    $off = { param($t) [DateTimeOffset]::new($t).ToString('yyyy-MM-ddTHH:mm:sszzz') }
    $reset = & $off ([datetime]::new(2026, 10, 21, 16, 0, 0))
    [ordered]@{ schema = 1; readAt = (& $off $p160Now.AddHours(-$AgeHours)); readBy = 'overseer'; plan = 'Max (20x)'
        session = @{ pct = 11; resets = (& $off ([datetime]::new(2026, 10, 17, 21, 50, 0))) }; week = @{ pct = 53; resets = $reset }; fableWeek = @{ pct = 44; resets = $reset }
        paceWarning = $Warning; limitResets = @{ full = @{ available = $true; expires = '2026-10-22' }; fiveHour = @{ available = $false } }
        cloudSessionCredit = @{ left = 250; of = 250; expires = '2026-11-05T01:59:00-06:00' }; usageCredits = @{ balance = 0; toggleOn = $true } } | ConvertTo-Json -Depth 4
}

Fact 'P160_UsageBarsShowTheFableWeekPaceMarkersAndThePlanLines' {
    $set = ConvertFrom-AutopilotSettings '{}'
    Assert-Equal 3 $set.DayWeekAheadPct 'the head start default'
    Assert-Equal 10 $set.WeekReservePct 'the reserve default'
    Assert-Equal 4 $set.PlanUsageMaxAgeHours 'the plan file age default'
    $r = @(Get-UsageBars -UsageText (New-P160Usage) -Now $p160Now -PlanText (New-P160Plan) -Settings $set)
    Assert-Equal 'head,Current session,This week,Fable this week,pace warning,plan read,plan extras' (($r | ForEach-Object { $_.Name }) -join ',') 'three bars, then the plan lines'
    Assert-Equal 'True,True,True' ((@($r[1..3]) | ForEach-Object { [string]$_.Bar }) -join ',') 'the three bars are bars'
    Assert-True ($r[2].Text -match '53% used \(pace 47\.5%, ahead\)  resets in 3 d 21 h, at Wed 16:00' -and $r[2].Color -eq 'amber' -and $r[2].Marker -eq 47.5) ('the week, ahead: ' + $r[2].Text + ' / ' + $r[2].Color)
    Assert-True ($r[2].Text -match '\[#*\|[#.]*\]') ('a pace marker in the bar: ' + $r[2].Text)
    Assert-True ($r[1].Text -match '14% used \(pace 38\.3%\)' -and $r[1].Color -eq 'normal') ('the 5-hour window: ' + $r[1].Text)
    Assert-True ($r[3].Pct -eq 44 -and $r[3].Text -match '44% used \(pace 47\.5%\)' -and $r[3].Color -eq 'normal') ('the Fable week from the plan page: ' + $r[3].Text)
    Assert-True ($r[4].Text -match '^pace warning from claude\.ai: Heads up\.' -and $r[4].Color -eq 'amber') $r[4].Text
    Assert-True ($r[5].Text -match '^plan usage page last read Sat 18:43 \(2m ago, overseer\)') $r[5].Text
    Assert-True ($r[6].Text -match 'free full reset: available until 2026-10-22' -and $r[6].Text -match 'cloud session credit: 250 of 250 left, until Nov 5' -and $r[6].Text -match 'usage credits: 0, on \(the Golem stops at the plan limit and never spends them\)') $r[6].Text
    Assert-True ($r[0].Text -match 'Plan: Max \(20x\)') $r[0].Text
}

Fact 'P160_AStalePlanPageGreysTheFableWeekAndDropsTheWarning' {
    $set = ConvertFrom-AutopilotSettings '{}'
    $r = @(Get-UsageBars -UsageText (New-P160Usage) -Now $p160Now -PlanText (New-P160Plan -AgeHours 6) -Settings $set)
    $f = @($r | Where-Object { $_.Name -eq 'Fable this week' })[0]
    Assert-True ($null -eq $f.Pct -and $f.Color -eq 'gray' -and $f.Text -match 'unknown: the plan usage page was read 6h 0m ago \(over planUsageMaxAgeHours 4\)') ('stale: ' + $f.Text)
    Assert-Equal 0 @($r | Where-Object { $_.Name -eq 'pace warning' }).Count 'a stale warning is not shown'
    Assert-True (@($r | Where-Object { $_.Name -eq 'plan read' })[0].Text -match 'too old') 'the read line says so'
    # No plan file at all: as before P160.
    $n = @(Get-UsageBars -UsageText (New-P160Usage) -Now $p160Now -Settings $set)
    Assert-Equal 'head,Current session,This week,Fable this week' (($n | ForEach-Object { $_.Name }) -join ',') 'no plan lines'
    Assert-True ($n[3].Text -match 'not reported') $n[3].Text
}

Fact 'P125_TheTileSaysEachStateAndKeepsTheRunInfo' {
    $lr = [ordered]@{ cap = 5; until = $p125Now.AddDays(3).ToString('s'); done = 2; active = $true }
    $cases = @(
        @{ S = (New-P125Status -LongRun $lr); Want = 'long run: 2 of 5, until Sun 09:45'; Color = 'green' },
        @{ S = (New-P125Status -Winding 'after-current'); Want = 'Wrapping up after P125'; Color = 'amber' },
        @{ S = (New-P125Status -Winding 'stopping-point'); Want = 'Finding a stopping point in P125 (CC is told at its next tool use)'; Color = 'amber' },
        @{ S = (New-P125Status -Winding 'stopping-point' -StopTold $true); Want = 'Finding a stopping point in P125 (CC has been told)'; Color = 'amber' },
        @{ S = (New-P125Status -State 'IDLE' -Current '' -RestReason 'wrap up' -RestAfter 'P125'); Want = 'Resting after P125 (wrap up)'; Color = 'gray' },
        @{ S = (New-P125Status -State 'IDLE' -Current '' -RestReason 'stopping point' -RestAfter 'P125'); Want = 'Paused at a stopping point in P125'; Color = 'gray' },
        @{ S = (New-P125Status -State 'IDLE' -Current '' -RestReason 'model' -WaitingFor 'Fable'); Want = 'Waiting for a Fable session'; Color = 'gray' }
    )
    foreach ($c in $cases) {
        $t = Get-AutopilotGlance -View (New-P125View $c.S) -Now $p125Now
        Assert-True ($t.Detail.Contains($c.Want)) ($c.Want + ' | tile: ' + $t.Detail)
        Assert-Equal $c.Color $t.Color ($c.Want)
    }
    # The running prompt's number, title, model and time, and its run position, are still there.
    $t = Get-AutopilotGlance -View (New-P125View (New-P125Status -Winding 'after-current')) -Now $p125Now
    Assert-True ($t.Detail -match 'P125 A "Long run" toggle for the Golem, for overnight and weekend runs \(Opus\)') $t.Detail
    Assert-True ($t.Detail -match '3 of 8' -and $t.Detail -match 'last activity|running') $t.Detail
    # At rest with both repos clean.
    $x = [pscustomobject]@{ PromptTexts = @{}; TreeClean = $true }
    $t2 = Get-AutopilotGlance -View (New-P125View (New-P125Status -State 'IDLE' -Current '' -RestReason 'wrap up' -RestAfter 'P125') $x) -Now $p125Now
    Assert-True ($t2.Detail -match 'Resting after P125 \(wrap up\)\. Tree clean, safe to package') $t2.Detail
}

Fact 'P125_StartLongRunRunsItsScriptWithTheCapAndEndAfterThePhrase' {
    $f = $p125Facts.PSObject.Copy()
    $f | Add-Member -NotePropertyName LongRunCap -NotePropertyValue 'all' -Force
    $f | Add-Member -NotePropertyName LongRunUntil -NotePropertyValue '2099-10-19 06:00' -Force
    $p = @(Get-ActionPlan -Action 'golem.longrun' -Config $p125Cfg -AutopilotFacts $f)
    Assert-Equal 'start long run' (@($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase)
    $w = @($p | Where-Object { $_.Kind -eq 'window' })
    Assert-Equal ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File D:\UO\golem\Start-LongRun.ps1 -Cap 0 -Until 2099-10-19T06:00:00') (Format-CommandLine $w[0].Exe $w[0].Arguments)
    Assert-Equal 0 @(Test-PlanSafety $p $p125Cfg).Count (@(Test-PlanSafety $p $p125Cfg) -join '; ')
    $f.LongRunCap = '4'
    $w = @(@(Get-ActionPlan -Action 'golem.longrun' -Config $p125Cfg -AutopilotFacts $f) | Where-Object { $_.Kind -eq 'window' })
    Assert-True ((Format-CommandLine $w[0].Exe $w[0].Arguments).Contains('-Cap 4 -Until')) 'a number'
    foreach ($bad in @(@('x', '2099-10-19 06:00'), @('4', 'monday'), @('4', '2001-01-01 06:00'))) {
        $f.LongRunCap = $bad[0]; $f.LongRunUntil = $bad[1]
        $r = @(Get-ActionPlan -Action 'golem.longrun' -Config $p125Cfg -AutopilotFacts $f)
        Assert-Equal 'refuse' $r[0].Kind ('refused: ' + ($bad -join ' / '))
    }
    $e = @(Get-ActionPlan -Action 'golem.endlongrun' -Config $p125Cfg -AutopilotFacts $p125Facts)
    Assert-Equal ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File D:\UO\golem\End-LongRun.ps1') (Format-CommandLine @($e | Where-Object { $_.Kind -eq 'window' })[0].Exe @($e | Where-Object { $_.Kind -eq 'window' })[0].Arguments)
}

Fact 'P125_WrapUpAndStoppingPointAskThePhraseAndToggleToCancel' {
    $p = @(Get-ActionPlan -Action 'golem.wrapup' -Config $p125Cfg -AutopilotFacts $p125Facts)
    Assert-Equal 'wrap up golem' (@($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase)
    Assert-True ((Format-CommandLine @($p | Where-Object { $_.Kind -eq 'window' })[0].Exe @($p | Where-Object { $_.Kind -eq 'window' })[0].Arguments).EndsWith('Wrap-Up.ps1'))
    $wound = $p125Facts.PSObject.Copy(); $wound.StatusText = (New-P125Status -Winding 'after-current')
    $c = @(Get-ActionPlan -Action 'golem.wrapup' -Config $p125Cfg -AutopilotFacts $wound)
    Assert-Equal 0 @($c | Where-Object { $_.Kind -eq 'confirm' }).Count 'cancelling asks nothing'
    Assert-True ((Format-CommandLine @($c | Where-Object { $_.Kind -eq 'window' })[0].Exe @($c | Where-Object { $_.Kind -eq 'window' })[0].Arguments).EndsWith('Wrap-Up.ps1 -Cancel'))
    $s = @(Get-ActionPlan -Action 'golem.stoppoint' -Config $p125Cfg -AutopilotFacts $p125Facts)
    Assert-Equal 'find a stopping point' (@($s | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase)
    $idle = $p125Facts.PSObject.Copy(); $idle.Running = ''; $idle.StatusText = (New-P125Status -State 'IDLE' -Current '')
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'golem.stoppoint' -Config $p125Cfg -AutopilotFacts $idle))[0].Kind 'nothing running'
    $told = $p125Facts.PSObject.Copy(); $told.StatusText = (New-P125Status -Winding 'stopping-point' -StopTold $true)
    $t = @(Get-ActionPlan -Action 'golem.stoppoint' -Config $p125Cfg -AutopilotFacts $told)
    Assert-True (@($t | Where-Object { $_.Kind -eq 'say' -and $_.Text -match 'already been told' }).Count -eq 1) 'cancelling after CC was told says it changes nothing'
    foreach ($pl in @($p, $c, $s, $t)) { Assert-Equal 0 @(Test-PlanSafety $pl $p125Cfg).Count (@(Test-PlanSafety $pl $p125Cfg) -join '; ') }
    # cc-P131: Wake the Golem (CC session) exists now; with P125 running in a live session it refuses for that reason.
    $r = @(Get-ActionPlan -Action 'golem.resume' -Config $p125Cfg -AutopilotFacts $p125Facts -Now $p125Now)
    Assert-True ($r[0].Kind -eq 'refuse' -and $r[0].Text -match 'P125 is running') ('the wake refuses while P125 runs: ' + $r[0].Text)
}

Fact 'P125_TheGolemHookItselfIsStillNeverRunFromTheConsole' {
    $bad = @(New-PlanStep -Kind window -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'D:\UO\golem\golem.ps1', '-Event', 'LongRun'))
    Assert-True (@(Test-PlanSafety $bad $p125Cfg) -match 'never run from the console').Count 'golem.ps1 itself is refused'
    $extra = @(New-PlanStep -Kind window -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'D:\UO\golem\Wrap-Up.ps1', '-Root', 'C:\x'))
    Assert-True (@(Test-PlanSafety $extra $p125Cfg).Count -gt 0) 'and a button script only with its own arguments'
}

Fact 'P125_TheGolemTabHasTheNewButtonsTheListsAndAGreyResume' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $p125Cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        # Their own row and group ('Golem run'): the nine Golem buttons cc-P74 to cc-P99 pinned stay exactly as they were.
        $tags = @($script:ui.GolemRunButtons | ForEach-Object { $_.Tag })
        Assert-Equal 'golem.longrun,golem.endlongrun,golem.wrapup,golem.stoppoint' ($tags -join ',') 'the four run buttons, in order'
        Assert-Equal 9 @($script:ui.AutopilotButtons).Count 'the Golem group is unchanged'
        $tips = Get-ConsoleTooltips
        foreach ($k in @($tags) + @('golem.resume', 'golem.cap', 'golem.until', 'golemQueue', 'golemPreview', 'golemFinished', 'golem.openprompt', 'golem.opennotes')) { Assert-True ($tips.Contains($k) -and $tips[$k]) ('a tooltip for ' + $k) }
        # cc-P131: the wake button is real now; it is dark while the fixture's P125 runs in a live session.
        $res = $script:ui.GolemResume
        Assert-True ($null -ne $res -and -not $res.Enabled -and $res.Text -eq 'Wake the Golem (CC session)') 'Wake the Golem (CC session) is shown and dark while P125 runs'
        Assert-True ($null -ne $script:ui.LongRunCap -and $null -ne $script:ui.LongRunUntil) 'the cap and end-time boxes'
        Assert-True ($null -ne $script:ui.GolemQueueList -and $null -ne $script:ui.GolemPreview -and $null -ne $script:ui.GolemFinishedList) 'the queue, its preview and the finished list'
        Assert-True ($script:ui.GolemPreview.ReadOnly) 'the preview is read-only'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

# --- cc-P131: waking a CC session from the console, self-wake and the night shift, resting said plainly -------------------
# Status texts carry the Golem's new fields; the plans point at D:\UO\golem's scripts; nothing is run and no CLI is touched
# (the CLI facts are given to the plan, never looked up here).

function New-P131Status {
    # New-P125Status plus the cc-P131 fields (readyAt local 's', readyHow, stoppedPrompt, autoWake, nightShift, cli).
    param([string]$State = 'RUNNING', [string]$Current = 'P125', [string]$RestReason = '', [string]$RestAfter = '', [string]$WaitingFor = '',
        $ReadyAt = $null, [string]$ReadyHow = '', [string]$StoppedPrompt = '', [string]$NeedsYou = '', [bool]$AutoWake = $true, $NightShift = $null, $Cli = $null, $WakePending = $null)
    $j = (New-P125Status -State $(if ($State -in @('RESTING', 'STOPPED')) { 'IDLE' } else { $State }) -Current $Current -RestReason $RestReason -RestAfter $RestAfter -WaitingFor $WaitingFor) | ConvertFrom-Json
    $j.state = $State
    $j.needsYou = $NeedsYou
    $j | Add-Member -NotePropertyName readyAt -NotePropertyValue $(if ($ReadyAt) { ([datetime]$ReadyAt).ToString('s') } else { $null }) -Force
    $j | Add-Member -NotePropertyName readyHow -NotePropertyValue $ReadyHow -Force
    $j | Add-Member -NotePropertyName stoppedPrompt -NotePropertyValue $StoppedPrompt -Force
    $j | Add-Member -NotePropertyName autoWake -NotePropertyValue $AutoWake -Force
    $j | Add-Member -NotePropertyName wakePending -NotePropertyValue $WakePending -Force
    $j | Add-Member -NotePropertyName lastSelfWake -NotePropertyValue $null -Force
    $j | Add-Member -NotePropertyName nightShift -NotePropertyValue $NightShift -Force
    $j | Add-Member -NotePropertyName cli -NotePropertyValue $Cli -Force
    $j | ConvertTo-Json -Depth 5
}
$p131Night = [ordered]@{ enabled = $true; start = '22:30'; end = '06:30'; next = $p125Now.Date.AddHours(22).AddMinutes(30).ToString('s'); until = $p125Now.Date.AddDays(1).AddHours(6).AddMinutes(30).ToString('s'); whyNot = ''; running = $false }
$p131Settings = '{ "progressEmails": true, "maxPerRun": 12, "stallMinutes": 45, "autoWake": true, "nightShiftEnabled": true, "nightShiftStart": "22:30", "nightShiftEnd": "06:30" }'
$p131Prompt = "# CC task P126: a thing`n`n**Model:** Opus`n`nWritten by the overseer.`n`n**Kind:** small.`n"
function New-P131Facts {
    param([bool]$Enabled = $true, $Queue = @('cc-P126-a.md'), [string]$Running = '', [string]$StatusText = (New-P131Status -State 'IDLE' -Current ''),
        [string]$SettingsText = $p131Settings, $CliCheck = ([pscustomobject]@{ Path = 'C:\Users\x\.local\bin\claude.exe'; SignedIn = $true; Note = '' }), [string]$WakeModel = '', [string]$NightStart = '', [string]$NightEnd = '')
    $f = [pscustomobject]@{ Enabled = $Enabled; EmailConfigured = $true; CredStored = $true; LogExists = $true; Present = @{}; Running = $Running; StatusText = $StatusText
        SettingsText = $SettingsText; QueueNames = $Queue; Extra = ([pscustomobject]@{ PromptTexts = @{ 'cc-P126-a.md' = $p131Prompt }; TreeClean = $null })
        CliCheck = $CliCheck; WakeModel = $WakeModel; NightStart = $NightStart; NightEnd = $NightEnd; OpenFile = '' }
    $ap = $p125Cfg.Autopilot
    foreach ($s in @($ap.LongRunStart, $ap.LongRunEnd, $ap.WrapUp, $ap.StopPoint, $ap.WakeScript, $ap.AutoWakeScript, $ap.NightShiftScript, $ap.CliCheckScript)) { if ($s) { $f.Present[$s] = $true } }
    $f
}
function Get-P131Window($plan) { $w = @($plan | Where-Object { $_.Kind -eq 'window' }); if ($w.Count) { Format-CommandLine $w[0].Exe $w[0].Arguments } else { '' } }

Fact 'P131_WakeTheGolemAsksThePhraseAndRunsWakeGolemWithTheModel' {
    $p = @(Get-ActionPlan -Action 'golem.resume' -Config $p125Cfg -AutopilotFacts (New-P131Facts -WakeModel 'Fable'))
    Assert-Equal 'wake the golem' (@($p | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase)
    $c = @($p | Where-Object { $_.Kind -eq 'confirm' })[0].Text
    Assert-True ($c.Contains('claude --model fable --permission-mode auto --name "Golem P126" "Golem, please work on the shard tasks."')) ('the confirm shows the command line it will run: ' + $c)
    Assert-True ($c -match 'never skipped') 'and says what happens to a prompt mid-run when the window is closed'
    Assert-Equal 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File D:\UO\golem\Wake-Golem.ps1 -Model Fable' (Get-P131Window $p)
    Assert-Equal 0 @(Test-PlanSafety $p $p125Cfg).Count (@(Test-PlanSafety $p $p125Cfg) -join '; ')
    Assert-True (@($p | Where-Object { $_.Kind -eq 'log' -and $_.Text -eq 'golem.resume' }).Count -eq 1) 'logged'
    # No model picked: the next prompt's Model line (P126 asks for Opus).
    $p2 = @(Get-ActionPlan -Action 'golem.resume' -Config $p125Cfg -AutopilotFacts (New-P131Facts))
    Assert-True ((Get-P131Window $p2).EndsWith('Wake-Golem.ps1 -Model Opus')) ('the prompt''s model: ' + (Get-P131Window $p2))
    Assert-Equal 'refuse' (@(Get-ActionPlan -Action 'golem.resume' -Config $p125Cfg -AutopilotFacts (New-P131Facts -WakeModel 'Haiku')))[0].Kind 'an unknown model is refused'
}

Fact 'P131_WakeTheGolemRefusesForEachReasonWithItsReasonOnScreen' {
    $cases = @(
        @{ Name = 'off';          Facts = (New-P131Facts -Enabled $false);                                                                                  Want = 'The Golem is off' },
        @{ Name = 'queue empty';  Facts = (New-P131Facts -Queue @());                                                                                       Want = 'The queue is empty' },
        @{ Name = 'running';      Facts = (New-P131Facts -Running 'P125' -StatusText (New-P131Status));                                                    Want = 'P125 is running in a CC session' },
        @{ Name = 'no CLI';       Facts = (New-P131Facts -CliCheck ([pscustomobject]@{ Path = ''; SignedIn = $null; Note = 'not installed' }));              Want = 'not installed' },
        @{ Name = 'not signed';   Facts = (New-P131Facts -CliCheck ([pscustomobject]@{ Path = 'C:\x\claude.exe'; SignedIn = $false; Note = 'not signed in' })); Want = 'is not signed in' }
    )
    foreach ($c in $cases) {
        $p = @(Get-ActionPlan -Action 'golem.resume' -Config $p125Cfg -AutopilotFacts $c.Facts -Now $p125Now)
        Assert-Equal 'refuse' $p[0].Kind ($c.Name + ': refused')
        Assert-True ($p[0].Text.Contains($c.Want)) ($c.Name + ': says why: ' + $p[0].Text)
        Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'window' }).Count ($c.Name + ': nothing opened')
    }
    # A prompt whose session has gone quiet past stallMinutes (closed?) is handed again, said so.
    $quiet = (New-P131Status) | ConvertFrom-Json
    $quiet.lastActivityUtc = $p125Now.ToUniversalTime().AddMinutes(-90).ToString('s'); $quiet.generatedAt = $p125Now.AddMinutes(-80).ToString('s')
    $p = @(Get-ActionPlan -Action 'golem.resume' -Config $p125Cfg -AutopilotFacts (New-P131Facts -Running 'P125' -StatusText ($quiet | ConvertTo-Json -Depth 5)) -Now $p125Now)
    $cf = @($p | Where-Object { $_.Kind -eq 'confirm' })
    Assert-True ($cf.Count -eq 1 -and $cf[0].Text -match 'P125 is the running prompt but its session has been quiet' -and $cf[0].Text -match 'nothing is skipped') ('quiet: handed again: ' + ($p | ForEach-Object { $_.Kind + ':' + $_.Text }) -join ' | ')
    Assert-True ((Get-P131Window $p).EndsWith('-Model Opus')) 'and the window line is built'
}

Fact 'P131_TheSafetyCheckAllowsOnlyTheExactWakeAndToggleArguments' {
    $ps = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File')
    $ok = @(
        @('D:\UO\golem\Wake-Golem.ps1', '-Model', 'Fable'), @('D:\UO\golem\Wake-Golem.ps1', '-Model', 'Opus'),
        @('D:\UO\golem\Set-AutoWake.ps1', '-On'), @('D:\UO\golem\Set-AutoWake.ps1', '-Off'),
        @('D:\UO\golem\Set-NightShift.ps1', '-Off'), @('D:\UO\golem\Set-NightShift.ps1', '-On', '-Start', '22:30', '-End', '06:30'),
        @('D:\UO\golem\Check-Cli.ps1'))
    foreach ($a in $ok) { $v = @(Test-PlanSafety @(New-PlanStep -Kind window -Exe 'powershell.exe' -Arguments ($ps + $a)) $p125Cfg); Assert-Equal 0 $v.Count (($a -join ' ') + ': ' + ($v -join '; ')) }
    $bad = @(
        @('D:\UO\golem\Wake-Golem.ps1'), @('D:\UO\golem\Wake-Golem.ps1', '-Model', 'Haiku'), @('D:\UO\golem\Wake-Golem.ps1', '-Model', 'Fable', '-Root', 'C:\x'),
        @('D:\UO\golem\Set-AutoWake.ps1'), @('D:\UO\golem\Set-AutoWake.ps1', '-Maybe'),
        @('D:\UO\golem\Set-NightShift.ps1', '-On'), @('D:\UO\golem\Set-NightShift.ps1', '-On', '-Start', 'tonight', '-End', '06:30'), @('D:\UO\golem\Set-NightShift.ps1', '-Off', '-Start', '22:30'),
        @('D:\UO\golem\Check-Cli.ps1', '-Root', 'C:\x'))
    foreach ($a in $bad) { Assert-True (@(Test-PlanSafety @(New-PlanStep -Kind window -Exe 'powershell.exe' -Arguments ($ps + $a)) $p125Cfg).Count -gt 0) (($a -join ' ') + ' is refused') }
}

Fact 'P131_StatusParsesRestingAndStoppedWithWhenItCanRunAgain' {
    $s = ConvertFrom-AutopilotStatus (New-P131Status -State 'RESTING' -Current '' -RestReason 'usage limit' -RestAfter 'P125' -ReadyAt $p125Now.AddHours(2) -ReadyHow 'Ready at 11:45 AM (in 2 h 0 m)' -NightShift $p131Night -Cli ([ordered]@{ path = 'C:\x\claude.exe'; signedIn = $true; checkedUtc = $p125Now.ToUniversalTime().ToString('s'); note = '' }))
    Assert-Equal 'RESTING' $s.State
    Assert-True ($s.ReadyAt -eq $p125Now.AddHours(2) -and $s.ReadyAt.Kind -eq 'Local') ('readyAt as a local time: ' + $s.ReadyAt)
    Assert-Equal 'Ready at 11:45 AM (in 2 h 0 m)' $s.ReadyHow
    Assert-True ($s.NightShift.enabled -and $s.NightShift.start -eq '22:30' -and $s.Cli.signedIn) 'the night shift and the CLI are carried'
    $t = ConvertFrom-AutopilotStatus (New-P131Status -State 'STOPPED' -Current '' -StoppedPrompt 'P125' -NeedsYou 'P125 stopped early: which layout?')
    Assert-True ($t.State -eq 'STOPPED' -and $t.StoppedPrompt -eq 'P125' -and $null -eq $t.ReadyAt -and $t.AutoWake) ('stopped: ' + $t.State + ' / ' + $t.StoppedPrompt)
    Assert-True ((ConvertFrom-AutopilotStatus (New-P125Status)).AutoWake) 'a cc-P125 status (no autoWake field) reads as on'
}

Fact 'P131_TheTileSaysRestingOrStoppedWithTheReasonAndACountdown' {
    $x = [pscustomobject]@{ PromptTexts = @{}; TreeClean = $null }
    $cases = @(
        @{ S = (New-P131Status -State 'RESTING' -Current '' -RestReason 'usage limit' -RestAfter 'P125' -ReadyAt $p125Now.AddHours(2).AddMinutes(14) -ReadyHow 'Ready at 11:59 AM');
           Word = 'RESTING'; Want = @('Paused: usage limit', 'ready in 2h 14m (11:59 AM)'); Color = 'amber' },
        @{ S = (New-P131Status -State 'RESTING' -Current '' -RestReason 'usage limit' -RestAfter 'P125' -ReadyAt $p125Now.AddMinutes(-5) -ReadyHow 'Ready now');
           Word = 'RESTING'; Want = @('ready now (since 9:40 AM)'); Color = 'amber' },
        @{ S = (New-P131Status -State 'RESTING' -Current '' -RestReason 'run cap' -RestAfter 'P125' -ReadyHow 'Ready now: new session, then the phrase (a new run)');
           Word = 'RESTING'; Want = @('Resting after P125 (run cap)', 'Ready now: new session, then the phrase (a new run)'); Color = 'gray' },
        @{ S = (New-P131Status -State 'RESTING' -Current '' -RestReason 'context' -RestAfter 'P125' -ReadyHow 'Waking itself at 3:00 AM (the overnight bot has the working tree until 03:00)');
           Word = 'RESTING'; Want = @('Resting after P125 (session full)', 'Waking itself at 3:00 AM'); Color = 'gray' },
        @{ S = (New-P131Status -State 'RESTING' -Current '' -RestReason 'night shift ended' -RestAfter 'P125' -ReadyAt $p125Now.Date.AddHours(22).AddMinutes(30) -ReadyHow 'Next night shift 10:30 PM');
           Word = 'RESTING'; Want = @('Resting after P125 (night shift ended)', 'ready in 12h 45m (10:30 PM)'); Color = 'gray' },
        @{ S = (New-P131Status -State 'RESTING' -Current '' -RestReason 'bot window' -RestAfter 'P125' -ReadyAt $p125Now.AddMinutes(80) -ReadyHow 'Resumes at 03:00 by itself');
           Word = 'RESTING'; Want = @('Resting after P125 (overnight bot window)', 'ready in 1h 20m'); Color = 'gray' },
        @{ S = (New-P131Status -State 'STOPPED' -Current '' -StoppedPrompt 'P125' -NeedsYou 'P125 stopped early: which layout do you want?');
           Word = 'STOPPED'; Want = @('P125 stopped early: which layout do you want?'); Color = 'amber' }
    )
    foreach ($c in $cases) {
        $t = Get-AutopilotGlance -View (New-P125View $c.S $x) -Now $p125Now
        Assert-Equal $c.Word $t.Word ($c.Want[0])
        foreach ($w in $c.Want) { Assert-True ($t.Detail.Contains($w)) ($w + ' | tile: ' + $t.Detail) }
        Assert-Equal $c.Color $t.Color ($c.Want[0])
    }
    # The panel: the ready line, the night shift line, self-wake and the CLI.
    $st = (New-P131Status -State 'RESTING' -Current '' -RestReason 'usage limit' -RestAfter 'P125' -ReadyAt $p125Now.AddHours(2).AddMinutes(14) -ReadyHow 'Ready at 11:59 AM' -NightShift $p131Night -Cli ([ordered]@{ path = 'C:\x\claude.exe'; signedIn = $false; checkedUtc = $p125Now.ToUniversalTime().AddMinutes(-3).ToString('s'); note = 'not signed in' }))
    $v = Get-AutopilotView -Enabled $true -StateText '{"current":"","currentFile":"","lastHandled":125}' -LogLines @() -QueueNames @('cc-P126-a.md') -Errors @() -StatusText $st -SettingsText $p131Settings -Kind golem -Extra $x
    $lines = @(Format-AutopilotPanel -View $v -Config $p125Cfg -Now $p125Now | ForEach-Object { $_.Text })
    Assert-True (@($lines | Where-Object { $_ -match '^ready     : ready in 2h 14m \(11:59 AM\)' }).Count -eq 1) ('the ready line: ' + (($lines | Where-Object { $_ -like 'ready*' }) -join ' | '))
    Assert-True (@($lines | Where-Object { $_ -eq 'night     : Night shift: on, 22:30 to 06:30, next run tonight at 22:30' }).Count -eq 1) ('the night line: ' + (($lines | Where-Object { $_ -like 'night*' }) -join ' | '))
    Assert-True (@($lines | Where-Object { $_ -eq 'self-wake : on' }).Count -eq 1) 'self-wake'
    Assert-True (@($lines | Where-Object { $_ -match '^cli       : C:\\x\\claude\.exe, NOT signed in: run claude auth login' }).Count -eq 1) ('the cli line: ' + (($lines | Where-Object { $_ -like 'cli*' }) -join ' | '))
    Assert-True (@($lines | Where-Object { $_ -match 'autoWake true, nightShift true 22:30 to 06:30' }).Count -eq 1) 'the settings line'
}

Fact 'P131_TheNightShiftLineSaysOnOffRunningOrWhyNot' {
    $set = ConvertFrom-AutopilotSettings $p131Settings
    $on = ConvertFrom-AutopilotStatus (New-P131Status -State 'IDLE' -Current '' -NightShift $p131Night)
    Assert-Equal 'Night shift: on, 22:30 to 06:30, next run tonight at 22:30' (Get-NightShiftLine $on $set $p125Now)
    $why = [ordered]@{} + $p131Night; $why.whyNot = 'the queue is empty'; $why.next = $p125Now.Date.AddDays(1).AddHours(22).AddMinutes(30).ToString('s')
    Assert-Equal 'Night shift: on, 22:30 to 06:30, next run tomorrow at 22:30; will not run as things stand: the queue is empty' (Get-NightShiftLine (ConvertFrom-AutopilotStatus (New-P131Status -State 'IDLE' -Current '' -NightShift $why)) $set $p125Now)
    $off = [ordered]@{} + $p131Night; $off.enabled = $false
    Assert-Equal 'Night shift: off (22:30 to 06:30 when on)' (Get-NightShiftLine (ConvertFrom-AutopilotStatus (New-P131Status -State 'IDLE' -Current '' -NightShift $off)) $set $p125Now)
    $run = [ordered]@{} + $p131Night; $run.running = $true
    Assert-True ((Get-NightShiftLine (ConvertFrom-AutopilotStatus (New-P131Status -NightShift $run)) $set $p125Now) -match '^Night shift: running now, until \w{3} 06:30$') 'running'
    # No status (a cc-P125 status.json): settings.json alone.
    Assert-Equal 'Night shift: on, 22:30 to 06:30' (Get-NightShiftLine (ConvertFrom-AutopilotStatus (New-P125Status)) $set $p125Now)
}

Fact 'P131_WakeTheGolemLightsOnlyWhenTheGolemIsReady' {
    $x = [pscustomobject]@{ PromptTexts = @{}; TreeClean = $null }
    $view = { param($st, $en = $true, $q = @('cc-P126-a.md')) Get-AutopilotView -Enabled $en -StateText '{"current":"","currentFile":"","lastHandled":125}' -LogLines @() -QueueNames $q -Errors @() -StatusText $st -SettingsText $p131Settings -Kind golem -Extra $x }
    Assert-True (Test-GolemWakeReady -View (& $view (New-P131Status -State 'IDLE' -Current '')) -Now $p125Now) 'idle with a queue: ready'
    Assert-True (Test-GolemWakeReady -View (& $view (New-P131Status -State 'RESTING' -Current '' -RestReason 'run cap' -ReadyHow 'Ready now')) -Now $p125Now) 'resting, ready now: ready'
    Assert-True (-not (Test-GolemWakeReady -View (& $view (New-P131Status -State 'RESTING' -Current '' -RestReason 'usage limit' -ReadyAt $p125Now.AddHours(1))) -Now $p125Now)) 'resting until later: not yet'
    Assert-True (Test-GolemWakeReady -View (& $view (New-P131Status -State 'RESTING' -Current '' -RestReason 'usage limit' -ReadyAt $p125Now.AddHours(-1))) -Now $p125Now) 'the reset time passed: ready'
    Assert-True (-not (Test-GolemWakeReady -View (& $view (New-P131Status)) -Now $p125Now)) 'P125 running in a live session: not ready'
    $quiet = (New-P131Status) | ConvertFrom-Json; $quiet.lastActivityUtc = $p125Now.ToUniversalTime().AddMinutes(-90).ToString('s')
    Assert-True (Test-GolemWakeReady -View (& $view ($quiet | ConvertTo-Json -Depth 5)) -Now $p125Now) 'running but quiet past stallMinutes: ready (handed again)'
    Assert-True (-not (Test-GolemWakeReady -View (& $view (New-P131Status -State 'STOPPED' -Current '' -StoppedPrompt 'P125')) -Now $p125Now)) 'STOPPED: not ready (waits for Chase)'
    Assert-True (-not (Test-GolemWakeReady -View (& $view (New-P131Status -State 'IDLE' -Current '') $false) -Now $p125Now)) 'the Golem off: not ready'
    Assert-True (-not (Test-GolemWakeReady -View (& $view (New-P131Status -State 'IDLE' -Current '') $true @()) -Now $p125Now)) 'an empty queue: not ready'
}

Fact 'P131_TheTogglesRunTheirScriptsWithExactArgumentsAndAskThePhraseToTurnTheNightShiftOn' {
    $onSet = $p131Settings
    $offSet = '{ "progressEmails": true, "maxPerRun": 12, "stallMinutes": 45, "autoWake": false, "nightShiftEnabled": false, "nightShiftStart": "22:30", "nightShiftEnd": "06:30" }'
    $a = @(Get-ActionPlan -Action 'golem.autowake' -Config $p125Cfg -AutopilotFacts (New-P131Facts -SettingsText $onSet))
    Assert-True ((Get-P131Window $a).EndsWith('Set-AutoWake.ps1 -Off') -and @($a | Where-Object { $_.Kind -eq 'confirm' }).Count -eq 0) ('on -> off, no phrase: ' + (Get-P131Window $a))
    $a2 = @(Get-ActionPlan -Action 'golem.autowake' -Config $p125Cfg -AutopilotFacts (New-P131Facts -SettingsText $offSet))
    Assert-True ((Get-P131Window $a2).EndsWith('Set-AutoWake.ps1 -On')) ('off -> on: ' + (Get-P131Window $a2))
    $n = @(Get-ActionPlan -Action 'golem.nightshift' -Config $p125Cfg -AutopilotFacts (New-P131Facts -SettingsText $onSet))
    Assert-True ((Get-P131Window $n).EndsWith('Set-NightShift.ps1 -Off') -and @($n | Where-Object { $_.Kind -eq 'confirm' }).Count -eq 0) ('on -> off: ' + (Get-P131Window $n))
    $n2 = @(Get-ActionPlan -Action 'golem.nightshift' -Config $p125Cfg -AutopilotFacts (New-P131Facts -SettingsText $offSet -NightStart '23:00' -NightEnd '05:30'))
    Assert-Equal 'night shift on' (@($n2 | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase)
    Assert-True ((Get-P131Window $n2).EndsWith('Set-NightShift.ps1 -On -Start 23:00 -End 05:30')) ('off -> on with the boxes: ' + (Get-P131Window $n2))
    $n3 = @(Get-ActionPlan -Action 'golem.nightshift' -Config $p125Cfg -AutopilotFacts (New-P131Facts -SettingsText $offSet -NightStart 'tonight'))
    Assert-Equal 'refuse' $n3[0].Kind 'a bad time is refused'
    $c = @(Get-ActionPlan -Action 'golem.clicheck' -Config $p125Cfg -AutopilotFacts (New-P131Facts))
    Assert-True ((Get-P131Window $c).EndsWith('Check-Cli.ps1')) ('the check: ' + (Get-P131Window $c))
    foreach ($pl in @($a, $a2, $n, $n2, $c)) { Assert-Equal 0 @(Test-PlanSafety $pl $p125Cfg).Count (@(Test-PlanSafety $pl $p125Cfg) -join '; ') }
}

Fact 'P131_TheGolemTabHasTheWakeRowAndTheToggleLabelsFollowTheSettings' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $p125Cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        $tags = @($script:ui.GolemWakeButtons | ForEach-Object { $_.Tag })
        Assert-Equal 'golem.resume,golem.autowake,golem.nightshift,golem.clicheck' ($tags -join ',') 'the wake row, in order'
        Assert-Equal 'golem.longrun,golem.endlongrun,golem.wrapup,golem.stoppoint' (@($script:ui.GolemRunButtons | ForEach-Object { $_.Tag }) -join ',') 'the cc-P125 run row is unchanged'
        Assert-True ($null -ne $script:ui.GolemModel -and (@($script:ui.GolemModel.Items) -join ',') -eq 'Fable,Opus') 'the model picker: Fable, Opus'
        Assert-True ($null -ne $script:ui.NightStart -and $null -ne $script:ui.NightEnd) 'the night shift''s two boxes'
        $tips = Get-ConsoleTooltips
        foreach ($k in @('golem.resume', 'golem.model', 'golem.autowake', 'golem.nightshift', 'golem.nightstart', 'golem.nightend', 'golem.clicheck')) { Assert-True ($tips.Contains($k) -and $tips[$k]) ('a tooltip for ' + $k) }
        $x = [pscustomobject]@{ PromptTexts = @{ 'cc-P126-a.md' = $p131Prompt }; TreeClean = $null }
        $offSet = '{ "autoWake": false, "nightShiftEnabled": false, "nightShiftStart": "23:00", "nightShiftEnd": "05:30" }'
        $v = Get-AutopilotView -Enabled $true -StateText '{"current":"","currentFile":"","lastHandled":125}' -LogLines @() -QueueNames @('cc-P126-a.md') -Errors @() -StatusText (New-P131Status -State 'IDLE' -Current '') -SettingsText $offSet -Kind golem -Extra $x
        Update-GolemRunControls $v
        $labels = @{}; foreach ($b in @($script:ui.GolemWakeButtons)) { $labels[[string]$b.Tag] = $b.Text }
        Assert-Equal 'Self-wake: off (turn on)' $labels['golem.autowake']
        Assert-Equal 'Night shift: off (turn on)' $labels['golem.nightshift']
        Assert-True ($script:ui.GolemResume.Enabled) 'idle with a queue: the wake button lights'
        Assert-Equal 'Opus' ([string]$script:ui.GolemModel.SelectedItem) 'the picker follows the next prompt''s Model line'
        Assert-True ($script:ui.NightStart.Text -eq '23:00' -and $script:ui.NightEnd.Text -eq '05:30') 'the boxes show the settings'
        # The form judges "live" from the real clock, so this status is active now.
        $live = (New-P131Status) | ConvertFrom-Json; $live.lastActivityUtc = (Get-Date).ToUniversalTime().ToString('s'); $live.generatedAt = (Get-Date).ToString('s')
        $v2 = Get-AutopilotView -Enabled $true -StateText '{"current":"P125","currentFile":"cc-P125-x.md","lastHandled":124}' -LogLines @() -QueueNames @('cc-P126-a.md') -Errors @() -StatusText ($live | ConvertTo-Json -Depth 5) -SettingsText $p131Settings -Kind golem -Extra $x
        Update-GolemRunControls $v2
        Assert-True (-not $script:ui.GolemResume.Enabled) 'P125 running: dark'
        $labels = @{}; foreach ($b in @($script:ui.GolemWakeButtons)) { $labels[[string]$b.Tag] = $b.Text }
        Assert-Equal 'Self-wake: on (turn off)' $labels['golem.autowake']
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

# --- cc-P132: resizing, the Enhanced look, art slots, readable text, the rebuild and package lights ------------------------
# Forms are built from the capture fixture with no art folder (every slot falls back) and placed off screen; nothing is
# run and no site is reached (the package light is not judged under a capture).

function New-P132Config {
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.Autopilot = New-AutopilotConfig 'D:\UO\cc-autopilot' 'D:\UO\cc-prompts\queue'
    $cfg.ArtFolder = Join-Path $env:TEMP ('sl-art-none-' + [guid]::NewGuid().ToString('N'))
    $cfg.WindowStateFile = Join-Path $env:TEMP ('sl-window-' + [guid]::NewGuid().ToString('N') + '.json')
    $cfg.LightsFile = Join-Path $env:TEMP ('sl-lights-' + [guid]::NewGuid().ToString('N') + '.json')
    $cfg.SettingsFile = Join-Path $env:TEMP ('sl-settings-' + [guid]::NewGuid().ToString('N') + '.json')
    $cfg
}
function Show-P132Form {
    param([string]$Look, [int]$W = 1200, [int]$H = 1000, $Cfg)
    $script:NetProbeDisabled = $true
    if (-not $Cfg) { $Cfg = New-P132Config }
    $f = New-ConsoleForm -Config $Cfg -DryRun -Theme $Look -Pane -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Settings (ConvertFrom-ConsoleSettings ('{"Theme":"' + $Look + '"}'))
    $f.StartPosition = 'Manual'; $f.Location = New-Object Drawing.Point(-8000, 0); $f.ShowInTaskbar = $false
    $f.Size = New-Object Drawing.Size($W, $H)
    $f.Show(); [Windows.Forms.Application]::DoEvents()
    $f
}
function Close-P132Form($f) {
    if ($script:ui.Pane -and $script:ui.Pane.Timer) { $script:ui.Pane.Timer.Stop() }
    $f.Close(); $f.Dispose(); $script:NetProbeDisabled = $false; $script:PaneReady = $false; $script:ui.Pane = $null
}
function Find-P132Control($Root, [string]$Name) {
    $stack = New-Object System.Collections.Stack; $stack.Push($Root)
    while ($stack.Count) { $x = $stack.Pop(); if ($x.Name -eq $Name) { return $x }; foreach ($k in $x.Controls) { $stack.Push($k) } }
    $null
}
$p132Commit = { param($short, $subject, $files) [pscustomobject]@{ Sha = ($short * 8).Substring(0, 40); Short = $short; Subject = $subject; Prompt = $(if ($subject -match '\bP(\d+)\b') { 'P' + $Matches[1] } else { '' }); Files = @($files) } }

Fact 'P132_EveryLookPassesAAOnItsTextColorsTilesAndPlate' {
    # Seen failing first: on the console before this change Get-ConsoleTheme had no Enhanced look, no Text and no Plate.
    foreach ($name in 'Plain', 'UO', 'Enhanced') {
        $t = Get-ConsoleTheme $name
        Assert-Equal 'green,amber,red,grey' (@($t.Tiles.Keys) -join ',') ($name + ': the four states')
        foreach ($k in $t.Tiles.Keys) { Assert-True ((Get-ContrastRatio $t.Tiles[$k].Fore $t.Tiles[$k].Back) -ge 4.5) ($name + ' tile ' + $k) }
        Assert-Equal 'Normal,Muted,Warning,Danger,Success,Link' (@($t.Text.PSObject.Properties.Name) -join ',') ($name + ': the six named text colors')
        $bad = @(Get-ThemeContrastPairs $t | Where-Object { $_.Ratio -lt $_.Floor })
        Assert-Equal 0 $bad.Count ($name + ': ' + (($bad | ForEach-Object { $_.Where + ' ' + $_.Ratio }) -join '; '))
        if ($t.Chrome) {
            Assert-True ($null -ne $t.Plate) ($name + ' has a plate')
            Assert-True ((Get-ContrastRatio $t.Plate.Text $t.Plate.Back) -ge 7) ($name + ': the plate at 7:1 or better')
            Assert-True ((Get-ContrastRatio $t.Chrome.StoneDanger $t.Chrome.StoneBack) -ge 4.5) ($name + ': danger words on the stone')
        }
    }
    $e = Get-ConsoleTheme 'Enhanced'
    Assert-True ($e.Crystals -and $e.Glow -and $e.Texture -and $null -eq $e.Gumps) 'Enhanced draws its own stone and has crystals'
    foreach ($k in $e.Lines.Keys) { Assert-True ((Get-ContrastRatio $e.Lines[$k] $e.LineBack) -ge 7) ('Enhanced line ' + $k + ' at 7:1') }
    Assert-True (@(Get-ThemeContrastPairs $e | Where-Object { $_.Where -like 'Text.* on drawn page*' }).Count -ge 12) 'the named colors are checked on the drawn page''s lightest and darkest pixel'
    # Today's colors, the example Chase gave: the dark page red on the stone, which the old dialog drew.
    $u = Get-ConsoleTheme 'UO'
    Assert-True ((Get-ContrastRatio $u.Chrome.PageWarn $u.Gumps.StoneLight) -lt 3) 'the old confirm-phrase color on the stone fails AA (why the plate exists)'
}

Fact 'P132_TheConfirmPhraseIsLargeAndBrightOnAPlateInEveryLook' {
    $saved = @($script:theme, $script:themeImages, $script:MonoFont, $script:BoldFont, $script:cfg)
    try {
        $script:cfg = Get-ConsoleConfig -RepoRoot $repo
        $plan = @((New-PlanStep -Kind confirm -Phrase 'publish player package' -Text 'x'), (New-PlanStep -Kind say -Text 'y'))
        foreach ($look in 'UO', 'Enhanced', 'Plain') {
            $script:theme = Get-ConsoleTheme $look
            $script:themeImages = @{}
            $script:MonoFont = Get-ThemeFont 'Mono'; $script:BoldFont = Get-ThemeFont 'Mono' ([Drawing.FontStyle]::Bold)
            $c = New-TypedConfirmForm -Phrase 'publish player package' -Text 'This publishes the zip.' -Plan $plan -What 'Player package'
            try {
                Assert-True ($c.Label.Text -eq 'Type exactly:  publish player package') ($look + ': the phrase line')
                Assert-True ($c.Label.Font.Bold -and $c.Label.Font.Size -ge 12) ($look + ': large and bold: ' + $c.Label.Font.Size)
                if ($script:theme.Plate) {
                    Assert-Equal (ConvertTo-DrawingColor $script:theme.Plate.Back) $c.Plate.BackColor ($look + ': on the plate')
                    Assert-Equal (ConvertTo-DrawingColor $script:theme.Plate.Text) $c.Label.ForeColor ($look + ': bright on it (the walker left it alone)')
                    Assert-True ($c.Label.Font.Size -ge 14) ($look + ': 14 pt')
                    $plateColor = @($c.Plate.BackColor.R, $c.Plate.BackColor.G, $c.Plate.BackColor.B); $textColor = @($c.Label.ForeColor.R, $c.Label.ForeColor.G, $c.Label.ForeColor.B)
                    Assert-True ((Get-ContrastRatio $textColor $plateColor) -ge 7) ($look + ': measured on the controls themselves')
                } else {
                    Assert-Equal (ConvertTo-DrawingColor $script:theme.Text.Danger) $c.Label.ForeColor 'Plain: the named danger color'
                }
                Assert-True (-not $c.Ok.Enabled) ($look + ': Do it waits for the phrase')
                $c.Box.Text = 'publish player package'
                Assert-True ($c.Ok.Enabled) ($look + ': the typed phrase enables it')
            } finally { $c.Form.Dispose() }
        }
    } finally { $script:theme = $saved[0]; $script:themeImages = $saved[1]; $script:MonoFont = $saved[2]; $script:BoldFont = $saved[3]; $script:cfg = $saved[4] }
}

Fact 'P132_NamedTextColorsReplaceTheHardCodedOnes' {
    $saved = $script:theme
    try {
        foreach ($look in 'UO', 'Enhanced') {
            $script:theme = Get-ConsoleTheme $look
            $t = $script:theme
            Assert-Equal (ConvertTo-DrawingColor $t.Text.Success) (Get-StateTextColor 'Success') ($look + ': success')
            $rgb = { param($c) @($c) -join ',' }
            Assert-Equal (& $rgb $t.Text.Danger) (& $rgb (Get-ThemedTextColor $t ([Drawing.Color]::Firebrick) $true)) ($look + ': Firebrick on a page is the named danger')
            Assert-Equal (& $rgb $t.Chrome.StoneDanger) (& $rgb (Get-ThemedTextColor $t ([Drawing.Color]::Firebrick) $false)) ($look + ': Firebrick on the stone is the light danger')
            Assert-Equal (& $rgb $t.Text.Success) (& $rgb (Get-ThemedTextColor $t ([Drawing.Color]::ForestGreen) $true)) ($look + ': ForestGreen is success')
            Assert-Equal (& $rgb $t.Text.Warning) (& $rgb (Get-ThemedTextColor $t ([Drawing.Color]::DarkOrange) $true)) ($look + ': DarkOrange is warning')
            Assert-Equal (& $rgb $t.Text.Muted) (& $rgb (Get-ThemedTextColor $t ([Drawing.Color]::DimGray) $true)) ($look + ': DimGray is muted')
            Assert-Equal (& $rgb $t.Chrome.StoneText) (& $rgb (Get-ThemedTextColor $t ([Drawing.Color]::Black) $false)) ($look + ': anything else on the stone is the stone text')
        }
        # The spot that measured under AA before: ForestGreen on parchment.
        $u = Get-ConsoleTheme 'UO'
        Assert-True ((Get-ContrastRatio @(34, 139, 34) $u.Gumps.ParchmentWorst) -lt 4.5) 'ForestGreen on the darkest parchment failed AA'
        Assert-True ((Get-ContrastRatio $u.Text.Success $u.Gumps.ParchmentWorst) -ge 4.5) 'Text.Success passes there'
    } finally { $script:theme = $saved }
}

Fact 'P132_EachLookRendersWithItsOwnChromeAndTheTabsStretch' {
    foreach ($look in 'Enhanced', 'UO', 'Plain') {
        $f = Show-P132Form -Look $look
        try {
            $t = $script:theme
            Assert-Equal $look $t.Name ($look + ' is in force')
            Assert-True ($null -ne $script:ui.Split1 -and $null -ne $script:ui.Split2) ($look + ': the two splitters')
            Assert-True ($script:ui.Split1.SplitterDistance -gt 0 -and $script:ui.Split2.SplitterDistance -gt 0) ($look + ': placed')
            Assert-True ([math]::Abs($script:ui.Tabs.Width - $script:ui.Split2.Panel1.ClientSize.Width) -le 2) ($look + ': the tabs stretch to the splitter panel: ' + $script:ui.Tabs.Width + ' vs ' + $script:ui.Split2.Panel1.ClientSize.Width)
            $crystal = $script:ui.Tiles[1].Crystal
            if ($look -eq 'Enhanced') {
                Assert-True ($null -ne $f.BackgroundImage) 'the drawn stone is tiled behind the window'
                Assert-True ($null -ne $crystal -and $null -ne $crystal.Image) 'a crystal on the TEST tile'
                Assert-True ($null -ne $script:ui.AutopilotTile.Figure -and $null -ne $script:ui.AutopilotTile.Figure.Image) 'the Golem figure on its tile'
                Assert-True ($null -ne $script:ui.GolemFigure) 'and on its tab'
                Assert-True ($null -ne (Find-P132Control $f 'headerRune')) 'the header rune'
                Assert-Equal 28 $script:ui.Tabs.Padding.X 'the tabs are padded for the rune'
                $pix = $f.BackgroundImage.GetPixel(10, 10)
                Assert-True ($pix.R -ge $t.Texture.StoneDark[0] -and $pix.R -le $t.Texture.StoneLight[0]) ('the stone''s pixels stay inside the measured bounds: ' + $pix)
            } else {
                Assert-True ($null -eq $crystal) ($look + ': no crystal')
                Assert-True ($null -eq $script:ui.AutopilotTile.Figure) ($look + ': no figure')
            }
            Assert-True ($script:ui.RebuildLight.Label.Text.StartsWith('Rebuild: ')) ($look + ': the rebuild light says something: ' + $script:ui.RebuildLight.Label.Text)
            Assert-True ($script:ui.PackageLight.Label.Text -match '^Package: UNKNOWN: not judged under DRY RUN') ($look + ': the package light is not judged from a capture')
            Assert-True ($script:ui.Tiles[1].Detail.Text -match "`nrebuild: ") ($look + ': the TEST tile''s second line')
            Assert-True (Test-Path -LiteralPath $script:cfg.LightsFile) ($look + ': lights.json written')
        } finally { Close-P132Form $f }
    }
}

Fact 'P132_TilesWrapAtNarrowWidths' {
    $f = Show-P132Form -Look 'Enhanced' -W 900 -H 700
    try {
        Update-TileFit; [Windows.Forms.Application]::DoEvents()
        Assert-True ($script:ui.Glance.ColumnCount -le 4 -and $script:ui.Glance.RowCount -ge 2) ('narrow: two rows, ' + $script:ui.Glance.ColumnCount + ' across')
        foreach ($tile in $script:ui.Tiles) { Assert-True ($tile.Panel.Width -ge 150) ('a tile keeps its width: ' + $tile.Panel.Width) }
        $f.Size = New-Object Drawing.Size(1700, 1000); [Windows.Forms.Application]::DoEvents(); Update-TileFit; [Windows.Forms.Application]::DoEvents()
        Assert-Equal 7 $script:ui.Glance.ColumnCount 'wide: one row of seven'
        Assert-True ($script:ui.Tiles[1].Crystal.Right -le $script:ui.Tiles[1].Word.Width) 'the crystal stays inside its tile'
    } finally { Close-P132Form $f }
}

Fact 'P132_SplittersAndTheWindowStatePersistAndSurviveACorruptFile' {
    # Pure: the file's values, with their floors, and a bad file.
    $d = ConvertFrom-WindowState 'this is not json'
    Assert-Equal '1200|1000|False|0' ([string]$d.Width + '|' + $d.Height + '|' + $d.Maximized + '|' + $d.Split1) 'garbage: the defaults'
    $x = ConvertFrom-WindowState '{ "Width": 50, "Height": -3, "X": 100, "Y": 200, "Maximized": true, "Split1": "x", "Split2": 300, "PaneSplit": 7 }'
    Assert-Equal '1200|1000|100|200|True|0|300|7' ([string]$x.Width + '|' + $x.Height + '|' + $x.X + '|' + $x.Y + '|' + $x.Maximized + '|' + $x.Split1 + '|' + $x.Split2 + '|' + $x.PaneSplit) 'a too-small size falls back, the rest is kept'
    $rt = ConvertFrom-WindowState (ConvertTo-WindowState $x)
    Assert-Equal 300 $rt.Split2 'round trip'
    $screens = @([pscustomobject]@{ X = 0; Y = 0; Width = 1920; Height = 1040 })
    $on = Resolve-WindowBounds ([pscustomobject]@{ Width = 1200; Height = 1000; X = 300; Y = 20; Maximized = $false }) $screens
    Assert-True ($on.Placed -and $on.X -eq 300 -and $on.Y -eq 20) 'on screen: kept'
    $off = Resolve-WindowBounds ([pscustomobject]@{ Width = 1200; Height = 1000; X = -5000; Y = 20; Maximized = $false }) $screens
    Assert-True (-not $off.Placed -and $off.X -eq 360 -and $off.Y -eq 20) ('off screen (an unplugged monitor): centered: ' + $off.X + ',' + $off.Y)
    $big = Resolve-WindowBounds ([pscustomobject]@{ Width = 4000; Height = 3000; X = $null; Y = $null; Maximized = $false }) $screens
    Assert-True ($big.Width -eq 1920 -and $big.Height -eq 1040) 'never larger than the screen'
    # The form: drag a splitter, save, read back; a corrupt file reads as the defaults; the splitters restore.
    $cfg = New-P132Config
    $f = Show-P132Form -Look 'Enhanced' -Cfg $cfg
    try {
        Set-SplitterSafely $script:ui.Split1 300
        Assert-Equal 300 $script:ui.Split1.SplitterDistance 'dragged'
        Assert-True (Save-WindowState $cfg $f) 'saved'
        $r = Read-WindowState $cfg
        Assert-True ($r.Split1 -eq 300 -and $r.Width -eq 1200 -and $r.Height -eq 1000 -and -not $r.Maximized) ('read back: ' + (ConvertTo-WindowState $r))
        [IO.File]::WriteAllText($cfg.WindowStateFile, '{ not json at all')
        $c = Read-WindowState $cfg
        Assert-Equal 1200 $c.Width 'a corrupt file: the defaults, no error'
        Restore-WindowSplitters ([pscustomobject]@{ Split1 = 260; Split2 = 0; PaneSplit = 0 })
        Assert-Equal 260 $script:ui.Split1.SplitterDistance 'restored'
        Set-SplitterSafely $script:ui.Split1 99999
        Assert-Equal 260 $script:ui.Split1.SplitterDistance 'a value that does not fit is left alone'
    } finally { Close-P132Form $f; Remove-Item -LiteralPath $cfg.WindowStateFile -Force -ErrorAction SilentlyContinue }
}

Fact 'P132_EverySlotLoadsASamplePngAndFallsBackWhenMissingAndABadFileLogsOneLine' {
    $saved = $script:theme
    $script:theme = Get-ConsoleTheme 'Enhanced'
    $dir = Join-Path $env:TEMP ('sl-art-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $cfg = New-P132Config; $cfg.ArtFolder = $dir
    try {
        $slots = @(Get-ArtSlots)
        Assert-Equal 20 $slots.Count 'twenty slots'
        Assert-True (($slots | Measure-Object -Property Budget -Sum).Sum * 1024 -lt 2.5MB) 'the budgets stay far under the 5 MB rule'
        # A sample PNG in every slot (the drawn fallbacks, saved), two of them strips with an art.json entry.
        foreach ($s in $slots) {
            $img = New-FallbackImage $s.Name $(if ($s.Kind -eq 'tile') { 64 } else { $s.Size })
            if ($s.Name -in @('crystal-green', 'spinner')) {
                $strip = New-Object Drawing.Bitmap(($img.Width * 4), $img.Height)
                $g = [Drawing.Graphics]::FromImage($strip); foreach ($i in 0..3) { $g.DrawImage($img, ($i * $img.Width), 0) }; $g.Dispose()
                $strip.Save((Join-Path $dir $s.File), [Drawing.Imaging.ImageFormat]::Png); $strip.Dispose()
            } else { $img.Save((Join-Path $dir $s.File), [Drawing.Imaging.ImageFormat]::Png) }
            $img.Dispose()
        }
        [IO.File]::WriteAllText((Join-Path $dir 'art.json'), '{ "slots": { "crystal-green": { "frames": 4, "fps": 8 }, "spinner": { "frames": 4, "fps": 10 } } }')
        $art = Get-ConsoleArt $cfg
        Assert-Equal 20 $art.Slots.Count ('every slot loaded: missing ' + ($art.Missing -join ',') + ' bad ' + ($art.Bad -join ';'))
        Assert-Equal 'strip' $art.Slots['crystal-green'].Kind 'a strip with an art.json entry'
        Assert-Equal 4 $art.Slots['crystal-green'].Frames
        Assert-True ($art.Lines[0] -match '^console-art: 20 of 20 slots have art') $art.Lines[0]
        $script:art = $art; $script:artCache = @{}
        $first = Get-ArtImage 'crystal-green'
        Assert-True ($null -ne $first -and $first.Width -eq 32) 'the first frame is cut from the strip'
        Assert-True ($null -ne (Get-ArtOrFallback 'golem-idle' 64)) 'the art is what the tile gets'
        # Nothing in the folder: every slot falls back, said in one line that names them.
        Remove-Item -Path (Join-Path $dir '*') -Force
        $none = Get-ConsoleArt $cfg
        Assert-Equal 0 $none.Slots.Count 'nothing loaded'
        Assert-Equal 20 $none.Missing.Count 'twenty missing'
        Assert-Equal 1 $none.Lines.Count 'one line'
        Assert-True ($none.Lines[0] -match '0 of 20 slots have art' -and $none.Lines[0] -match 'crystal-green') $none.Lines[0]
        $script:art = $none; $script:artCache = @{}
        Assert-True ($null -eq (Get-ArtImage 'crystal-green')) 'no art'
        Assert-True ($null -ne (Get-ArtOrFallback 'crystal-green' 28)) 'so the drawn crystal'
        # Two bad files: text named as a PNG, and an oversized one. Each is one line; the fallback is drawn for both.
        [IO.File]::WriteAllText((Join-Path $dir 'crystal-red.png'), 'this is not a picture')
        $bytes = New-Object byte[] (130 * 1024); (New-Object Random(1)).NextBytes($bytes); [IO.File]::WriteAllBytes((Join-Path $dir 'golem-dark.png'), $bytes)
        $bad = Get-ConsoleArt $cfg
        Assert-Equal 2 $bad.Bad.Count ($bad.Bad -join '; ')
        Assert-True (@($bad.Lines | Where-Object { $_ -like 'console-art: bad file, drawn fallback used: *' }).Count -eq 2) 'one line per bad file'
        Assert-True (@($bad.Bad | Where-Object { $_ -match 'crystal-red\.png: could not be read' }).Count -eq 1) ($bad.Bad -join '; ')
        Assert-True (@($bad.Bad | Where-Object { $_ -match 'golem-dark\.png: 130 KB is over the slot''s 120 KB budget' }).Count -eq 1) ($bad.Bad -join '; ')
        Assert-Equal 18 $bad.Missing.Count 'the other eighteen are missing, not bad'
        # The judge itself.
        $slot = @($slots | Where-Object { $_.Name -eq 'crystal-grey' })[0]
        Assert-True (-not (Test-ArtFile -Slot $slot -Exists $true -Length 100 -Extension '.gif' -Width 32 -Height 32).Ok) 'a GIF in a static slot is refused'
        $anim = @($slots | Where-Object { $_.Name -eq 'crystal-green' })[0]
        Assert-True (Test-ArtFile -Slot $anim -Exists $true -Length 100 -Extension '.gif' -Width 32 -Height 32).Ok 'a GIF in an animated slot is fine'
        Assert-True (-not (Test-ArtFile -Slot $anim -Exists $true -Length 100 -Extension '.png' -Width 100 -Height 32 -Frames 3).Ok) 'a strip whose width is not frames x width is refused'
        Assert-True (Test-ArtFile -Slot $anim -Exists $true -Length 100 -Extension '.png' -Width 96 -Height 32 -Frames 3).Ok 'and one that is, is fine'
        Assert-True (Test-ArtFile -Slot $slot -Exists $false).Missing 'a missing file is missing, not bad'
        $m = ConvertFrom-ArtManifest '{ "slots": { "x": { "frames": 0, "fps": 999 } } }'
        Assert-True ($m.Slots['x'].Frames -eq 1 -and $m.Slots['x'].Fps -eq 8) 'silly values fall back'
        Assert-True ((ConvertFrom-ArtManifest 'nope').Problem -match 'does not parse') 'a broken art.json says so'
    } finally { $script:theme = $saved; $script:art = $null; $script:artCache = @{}; Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P132_TheGolemFigureFollowsTheGolemsState' {
    $saved = @($script:theme, $script:ui)
    try {
        $script:theme = Get-ConsoleTheme 'Enhanced'; $script:ui = @{}; $script:artCache = @{}; $script:art = $null
        $x = [pscustomobject]@{ PromptTexts = @{}; TreeClean = $null }
        Assert-Equal 'running' (Set-GolemFigure (New-P125View (New-P125Status) $x)) 'running a prompt: glowing'
        Assert-Equal 'idle' (Set-GolemFigure (New-P125View (New-P125Status -State 'IDLE' -Current '') $x)) 'awake, nothing running: a soft glow'
        Assert-Equal 'dark' (Set-GolemFigure (New-P125View (New-P131Status -State 'RESTING' -Current '' -RestReason 'run cap') $x)) 'resting: dark'
        Assert-Equal 'dark' (Set-GolemFigure (New-P125View (New-P131Status -State 'STOPPED' -Current '' -StoppedPrompt 'P125') $x)) 'stopped: dark'
        Assert-Equal 'dark' (Set-GolemFigure (New-P125View (New-P131Status -State 'OFF' -Current '') $x)) 'off: dark'
        foreach ($s in 'running', 'idle', 'dark') { Assert-True ($null -ne (New-FallbackImage ('golem-' + $s) 64)) ('a drawn golem, ' + $s) }
    } finally { $script:theme = $saved[0]; $script:ui = $saved[1] }
}

Fact 'P132_TheRebuildLightSaysEachColourFromFixtures' {
    $head = 'b' * 40
    $img = 'a' * 40
    $server = @((& $p132Commit 'c127c127' 'CC task P127: Port list, Treasure maps' @('server/customizations/Items/TreasureMap.cs', 'CHANGELOG.md')),
                (& $p132Commit 'c128c128' 'CC task P128: Consumables' @('server/customizations/Items/Food.cs')))
    $docs = @((& $p132Commit 'd001d001' 'CC task P131: Wake the Golem' @('scripts/Shard-Console.ps1', 'scripts/tests/Shard-Console.Tests.ps1', 'VERSION')),
              (& $p132Commit 'd002d002' 'notes only' @('docs/x.md')))
    $g = Get-RebuildLight -ImageCommit $head -Head $head -Commits @() -Running $true
    Assert-True ($g.Color -eq 'green' -and $g.Word -eq 'LATEST' -and $g.Detail -eq 'built from HEAD') ('built from HEAD: ' + $g.Detail)
    $g2 = Get-RebuildLight -ImageCommit $img -Head $head -Commits @((& $p132Commit 'd002d002' 'notes only' @('docs/x.md')), (& $p132Commit 'd003d003' 'console' @('scripts/Shard-Console.ps1'))) -Running $true
    Assert-True ($g2.Color -eq 'green' -and $g2.Detail -eq '2 commits since the build, none touch the server') ('docs and console only: ' + $g2.Detail)
    $a = Get-RebuildLight -ImageCommit $img -Head $head -Commits ($server + $docs) -Running $true
    Assert-True ($a.Color -eq 'amber' -and $a.Word -eq 'REBUILD') ('server changes: ' + $a.Detail)
    Assert-True ($a.Detail -match '^3 commits: P127, P128, P131 touch the server since the running build: Rebuild can go now') ('counted with their prompts (VERSION counts as a server file): ' + $a.Detail)
    Assert-True ($a.Hover -match 'c127c127 CC task P127' -and $a.Hover -match 'server/customizations/Items/TreasureMap\.cs' -and $a.Hover -notmatch 'docs/x\.md') 'the hover lists the commits and the server files'
    $r1 = Get-RebuildLight -ImageCommit $img -Head $head -Commits $server -GolemRunning 'P130' -Running $true
    Assert-True ($r1.Color -eq 'red' -and $r1.Detail -match 'the Golem is running P130') ('blocked by the Golem: ' + $r1.Detail)
    $r2 = Get-RebuildLight -ImageCommit $img -Head $head -Commits $server -Uncommitted 3 -Running $true
    Assert-True ($r2.Color -eq 'red' -and $r2.Detail -match '3 uncommitted file\(s\) in the code repo: commit first') ('blocked by uncommitted changes: ' + $r2.Detail)
    $r3 = Get-RebuildLight -ImageCommit $img -Head $head -Commits $server -BuildRunning $true -Running $true
    Assert-True ($r3.Color -eq 'red' -and $r3.Detail -match 'a build is already running') ('blocked by a build: ' + $r3.Detail)
    $u = Get-RebuildLight -ImageCommit '' -Head $head -Commits @() -Running $true
    Assert-True ($u.Color -eq 'grey' -and $u.Detail -match 'no sl\.commit label') ('no label: ' + $u.Detail)
    $n = Get-RebuildLight -ImageCommit $img -Head $head -Commits $server -Running $false
    Assert-True ($n.Color -eq 'grey' -and $n.Detail -match 'no running test shard') 'nothing running'
    $dirty = Get-RebuildLight -ImageCommit $head -ImageDirty $true -Head $head -Commits @() -Running $true
    Assert-True ($dirty.Detail -match 'built with uncommitted changes') 'a dirty image says so'
    $j = ConvertTo-LightsJson $a $r1 ([datetime]'2026-10-16T12:00:00') | ConvertFrom-Json
    Assert-True ($j.rebuild.color -eq 'amber' -and @($j.rebuild.commits).Count -eq 3 -and $j.package.color -eq 'red' -and $j.schema -eq 1) 'the status.json-style output carries both'
}

Fact 'P132_ThePackageLightSaysEachColourFromFixtures' {
    $head = 'h' * 40
    $ship = @((& $p132Commit 'p124p124' 'CC task P124: the package ships our TazUO' @('player-package/Build-PlayerPackage.ps1', 'TAZUO_COMMIT')))
    $notShip = @((& $p132Commit 'p130p130' 'CC task P130: Pet training' @('server/customizations/x.cs', 'docs/y.md')))
    $g = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234' -NewestZipVersion '2026.10.15.1354' -NewestZipCommit 'abc1234' -Head $head -Commits $notShip -CommitsSinceZip $notShip
    Assert-True ($g.Color -eq 'green' -and $g.Detail -match 'published 2026\.10\.15\.1354 holds every player-facing change') ('server-only changes do not count: ' + $g.Detail)
    $b = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234' -NewestZipVersion '2026.10.15.1354' -NewestZipCommit 'abc1234' -Head $head -Commits ($ship + $notShip) -CommitsSinceZip ($ship + $notShip)
    Assert-True ($b.Color -eq 'amber' -and $b.Word -eq 'BUILD' -and $b.Detail -eq 'changes since the published package: Build package (1 commit: P124)') ('build: ' + $b.Detail)
    $p = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234' -NewestZipVersion '2026.10.16.0900' -NewestZipCommit $head.Substring(0, 7) -Head $head -Commits $ship -CommitsSinceZip @()
    Assert-True ($p.Color -eq 'amber' -and $p.Word -eq 'PUBLISH' -and $p.Detail -match '^built, not published: Publish package \(2026\.10\.16\.0900 holds 1 commit: P124\)') ('publish: ' + $p.Detail)
    $r1 = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234' -Head $head -Commits $ship -GolemRunning 'P132'
    Assert-True ($r1.Color -eq 'red' -and $r1.Detail -match 'the Golem is running P132') ('golem: ' + $r1.Detail)
    $r2 = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234' -Head $head -Commits $ship -Uncommitted 2
    Assert-True ($r2.Color -eq 'red' -and $r2.Detail -match '2 uncommitted') ('uncommitted: ' + $r2.Detail)
    $r3 = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234' -Head $head -Commits $ship -ClientStale 'STALE CLIENT BUILD: a patch changed after the build'
    Assert-True ($r3.Color -eq 'red' -and $r3.Detail -match 'the client is stale: STALE CLIENT BUILD') ('stale client: ' + $r3.Detail)
    $r4 = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234' -Head $head -Commits $ship -BuildRunning $true
    Assert-True ($r4.Color -eq 'red' -and $r4.Detail -match 'a build is already running') ('build running: ' + $r4.Detail)
    $r5 = Get-PackageLight -SiteReachable $false -SiteError 'timed out'
    Assert-True ($r5.Color -eq 'red' -and $r5.Detail -match 'could not be reached to compare \(timed out\)') ('site down: ' + $r5.Detail)
    $u = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit '' -Head $head -Commits @()
    Assert-True ($u.Color -eq 'grey' -and $u.Detail -match 'no zip in dist') ('no zip to read: ' + $u.Detail)
    $un = Get-PackageLight -PublishedVersion '2026.10.15.1354' -PublishedCommit 'abc1234 + uncommitted player-package changes' -Head $head -Commits @()
    Assert-True ($un.Color -eq 'amber' -and $un.Detail -match 'built from uncommitted changes') ('VERSION.txt says uncommitted: ' + $un.Detail)
    # The git log parser the shell feeds both lights with.
    $log = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa|aaaaaaa|CC task P127: Treasure maps`nserver/customizations/Items/TreasureMap.cs`nCHANGELOG.md`n`nbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb|bbbbbbb|notes`ndocs/x.md`n"
    $c = @(ConvertFrom-GitLogWithFiles $log)
    Assert-True ($c.Count -eq 2 -and $c[0].Prompt -eq 'P127' -and @($c[0].Files).Count -eq 2 -and $c[1].Files[0] -eq 'docs/x.md') 'two commits with their files'
    Assert-Equal 1 @(Select-CommitsTouching $c @('server/')).Count 'one touches the server'
    Assert-True ((Read-ZipBuiltFrom (Join-Path $env:TEMP 'no-such-package.zip')) -eq $null) 'a missing zip reads as unknown, not an error'
}

# --- cc-P133: the Haven intranet. One inventory (docker\haven\services.json), the dashboard generated from it, Tailscale only

. (Join-Path $repo 'docker\haven\HavenInventory.ps1')
$p133Inv = Read-HavenInventory -Path (Join-Path $repo 'docker\haven\services.json')
$p133Cfg = Join-Path $repo 'docker\dashboard\haven\config'
function Get-P133Yaml([string]$Path) {
    # Homepage's own parser, from the pinned image, when Docker answers; the facts below need the structure, not the text.
    $out = & docker run --rm -v ((Split-Path $Path -Parent) + ':/cfg:ro') --entrypoint node ghcr.io/gethomepage/homepage:v1.13.1 -e ("const y=require('/app/node_modules/js-yaml');process.stdout.write(JSON.stringify(y.load(require('fs').readFileSync('/cfg/" + (Split-Path $Path -Leaf) + "','utf8'))))") 2>$null
    if ($LASTEXITCODE -ne 0) { throw ('js-yaml could not read ' + $Path + ' (is Docker running?)') }
    ($out -join '') | ConvertFrom-Json
}
$p133Tiles = { param($doc) foreach ($g in @($doc)) { $sec = @($g.PSObject.Properties)[0]; foreach ($t in @($sec.Value)) { $p = @($t.PSObject.Properties)[0]; [pscustomobject]@{ Section = $sec.Name; Name = $p.Name; Tile = $p.Value } } } }

Fact 'P133_TheInventoryIsSoundAndHoldsNamesNeverSecretValues' {
    Assert-Equal 0 @($p133Inv.Problems).Count (($p133Inv.Problems) -join ' | ')
    Assert-Equal 'Sunflower|Filament|Monitoring|Ultima Online / Shattered Legacy|Admin' (@($p133Inv.sections) -join '|') 'the five sections, in order'
    $text = [IO.File]::ReadAllText((Join-Path $repo 'docker\haven\services.json'))
    Assert-True ($text -notmatch '-----BEGIN|ssh-(ed25519|rsa) AAAA|eyJ[A-Za-z0-9_-]{20,}|glsa_|ptr_[A-Za-z0-9]{10,}|"(password|token|apiKey|api_key|secret)"\s*:\s*"[^"{]') 'no key material, token or password value'
    foreach ($s in @($p133Inv.services)) {
        foreach ($e in @($s.env)) { Assert-True ([string]$e -match '^([A-Z][A-Z0-9_]*|see .+)$') ($s.key + ': env holds names only, not ' + $e) }
        foreach ($f in 'compose', 'health', 'verified') { Assert-True ($s.PSObject.Properties.Name -contains $f) ($s.key + ' says ' + $f) }
        Assert-True (@($s.backup).Count -ge 0) $s.key
    }
    foreach ($k in 'spoolman', 'vaultwarden', 'npm') { Assert-True ([string](@($p133Inv.services | Where-Object { $_.key -eq $k })[0].neverMove)) ($k + ' says what must never move') }
    $vw = @($p133Inv.services | Where-Object { $_.key -eq 'vaultwarden' })[0]
    Assert-True (@($vw.tiles | Where-Object { $_.widget }).Count -eq 0) 'Vaultwarden is a link tile only, never a widget'
}

Fact 'P133_TheDashboardIsGeneratedFromTheInventoryWithEverySectionAndTile' {
    Assert-Equal (ConvertTo-HomepageServices -Inventory $p133Inv) ([IO.File]::ReadAllText((Join-Path $p133Cfg 'services.yaml')).TrimEnd("`n")) 'services.yaml is exactly what services.json makes (run docker\haven\Build-HavenDashboard.ps1)'
    Assert-Equal (ConvertTo-HomepageSettings -Inventory $p133Inv) ([IO.File]::ReadAllText((Join-Path $p133Cfg 'settings.yaml')).TrimEnd("`n")) 'settings.yaml likewise'
    $doc = Get-P133Yaml (Join-Path $p133Cfg 'services.yaml')
    Assert-Equal 'Sunflower|Filament|Monitoring|Ultima Online / Shattered Legacy|Admin' ((@($doc) | ForEach-Object { @($_.PSObject.Properties)[0].Name }) -join '|') 'the five sections, in order, as Homepage reads them'
    $tiles = @(& $p133Tiles $doc)
    foreach ($s in @($p133Inv.services)) {
        if ((Get-HavenTileMode $s) -eq 'none') { continue }   # cc-P163: planned, retired, not installed yet: no tile
        foreach ($t in @($s.tiles)) { Assert-True (@($tiles | Where-Object { $_.Section -eq $t.section -and $_.Name -eq $t.name }).Count -eq 1) ($t.name + ' is in ' + $t.section) }
        if ($s.host -eq 'haven' -and $s.container) { Assert-True (@($tiles | Where-Object { $_.Tile.container -eq $s.container -and $_.Tile.server -eq 'haven' }).Count -ge 1) ('Haven container ' + $s.container + ' has a tile with its state') }
    }
    $set = Get-P133Yaml (Join-Path $p133Cfg 'settings.yaml')
    Assert-Equal 'Sunflower|Filament|Monitoring|Ultima Online / Shattered Legacy|Admin' (@($set.layout.PSObject.Properties.Name) -join '|') 'the layout names the same five'
    $old = [IO.File]::ReadAllText((Join-Path $p133Cfg 'services.yaml'))
    Assert-True ($old -notmatch 'HOMEPAGE_VAR_SPOOLMAN_URL|HOMEPAGE_VAR_GRAPHS_URL|Graphing app') 'P110''s placeholders are gone: Spoolman''s address is known'
}

Fact 'P133_EveryTileShowsUpOrDown' {
    $tiles = @(& $p133Tiles (Get-P133Yaml (Join-Path $p133Cfg 'services.yaml')))
    Assert-True ($tiles.Count -ge 30) ('tiles: ' + $tiles.Count)
    foreach ($t in $tiles) {
        $w = $t.Tile.widget
        $fields = @($(if ($w) { @($w.mappings) | ForEach-Object { [string]$_.field } }))
        $live = ($t.Tile.container -and $t.Tile.server -eq 'haven') -or [string]$t.Tile.siteMonitor -or
            ($fields | Where-Object { $_ -like 'up.*' -or $_ -eq 'offline' -or $_ -eq 'shards.0.up' })
        # cc-P163: a service stopped by design (kind on-demand) says so in words instead of a red dot.
        $byHand = ([string]$t.Tile.description).StartsWith('Stopped, started by hand. ') -and -not $t.Tile.siteMonitor -and -not $t.Tile.container -and -not $t.Tile.widget
        Assert-True ($live -or $byHand) ($t.Section + ' / ' + $t.Name + ' shows no up or down')
        if ($t.Tile.widget -and $t.Tile.widget.url -like '*/api/gamepc') { Assert-True ([string]$t.Tile.widget.headers.Authorization -eq 'Bearer {{HOMEPAGE_VAR_OPS_TOKEN}}') ($t.Name + ': the snapshot needs the ops token') }
    }
    $pc = @($tiles | Where-Object { $_.Name -eq 'Game PC' })[0]
    Assert-True ($pc -and @($pc.Tile.widget.mappings | Where-Object { $_.field -eq 'offlineSince' }).Count -eq 1) 'the game PC tile says offline since when it sleeps'
    foreach ($k in 'sl-modernuo-test', 'sl-uo-status', 'sl-shard-probe', 'sl-ddns', 'sl-portainer') {
        $svc = @($p133Inv.services | Where-Object { $_.container -eq $k -and $_.host -eq 'gamepc' })[0]
        Assert-True $svc ('the game PC''s ' + $k + ' is in the inventory')
    }
}

Fact 'P133_WidgetsThatNeedAKeyReadItFromTheEnvironmentNeverTheFile' {
    $tiles = @(& $p133Tiles (Get-P133Yaml (Join-Path $p133Cfg 'services.yaml')))
    $used = @{}
    foreach ($t in $tiles) {
        $w = $t.Tile.widget
        if (-not $w) { continue }
        foreach ($p in @($w.PSObject.Properties)) {
            if ($p.Name -match '^(key|password|token|apiKey|username|env)$') { Assert-True ([string]$p.Value -match '^\{\{HOMEPAGE_VAR_[A-Z0-9_]+\}\}$') ($t.Name + ': ' + $p.Name + ' comes from the environment') }
        }
        if ($w.headers) { foreach ($h in @($w.headers.PSObject.Properties)) { Assert-True ([string]$h.Value -match '^(Bearer )?\{\{HOMEPAGE_VAR_[A-Z0-9_]+\}\}$') ($t.Name + ': header ' + $h.Name + ' comes from the environment') } }
    }
    $text = [IO.File]::ReadAllText((Join-Path $p133Cfg 'services.yaml'))
    foreach ($m in [regex]::Matches($text, '\{\{(HOMEPAGE_VAR_[A-Z0-9_]+)\}\}')) { $used[$m.Groups[1].Value] = $true }
    Assert-Equal 'HOMEPAGE_VAR_GRAFANA_TOKEN|HOMEPAGE_VAR_OPS_TOKEN|HOMEPAGE_VAR_PORTAINER_ENV|HOMEPAGE_VAR_PORTAINER_KEY' ((@($used.Keys) | Sort-Object) -join '|') 'the four names the dashboard reads from dashboard.env'
    $d = [IO.File]::ReadAllText((Join-Path $repo 'docker\dashboard\haven\Deploy-MegaDashboard.ps1'))
    foreach ($n in @($used.Keys)) { Assert-True ($d -match [regex]::Escape($n)) ('the deploy makes sure dashboard.env has ' + $n) }
    Assert-True ($d -match 'grep -q "\^\$n=" ' -and $d -notmatch '(?i)Read-Host[^\r\n]*(token|key)') 'the deploy only adds missing names, empty, and never asks for or prints a token or key'
}

Fact 'P133_NoComposeOfOursPublishesOnAPublicInterface' {
    # cc-P144: and the Legion scripts' dev channel.
    $files = @((Join-Path $repo 'docker\dashboard\haven\docker-compose.yml'), (Join-Path $repo 'docker\ops\docker-compose.yml'), 'C:\dev\sunflower\print-queue\docker-compose.yml', (Join-Path $repo 'docker\scripts-dev\docker-compose.yml'), (Join-Path $repo 'docker\portainer-haven\docker-compose.yml'))
    foreach ($f in $files) {
        $c = [IO.File]::ReadAllText($f)
        $inPorts = $false
        $n = 0
        foreach ($l in ($c -split "\r?\n")) {
            if ($l -match '^\s+ports:\s*$') { $inPorts = $true; continue }
            if ($inPorts -and $l -match '^\s+-\s*"?([^"]+)"?\s*$') {
                $n++
                $spec = $Matches[1] -replace '\$\{[A-Z_]+:-([^}]*)\}', '$1'
                Assert-True ($spec -match '^(192\.168\.\d+\.\d+|127\.0\.0\.1):\d+:\d+$') ((Split-Path $f -Leaf) + ': ' + $Matches[1] + ' publishes on a LAN or loopback address only (it read as ' + $spec + ')')
                continue
            }
            if ($inPorts -and $l -notmatch '^\s+#') { $inPorts = $false }
        }
        Assert-True ($n -ge 1) ('ports read from ' + $f)
        Assert-True ($c -notmatch '0\.0\.0\.0|cloudflared|network_mode:\s*host') ($f + ': no all-interfaces bind, no tunnel, no host network')
    }
    $d = [IO.File]::ReadAllText((Join-Path $repo 'docker\dashboard\haven\Deploy-MegaDashboard.ps1'))
    Assert-True ($d -notmatch "DASH_BIND=' \+ \`$\(if \(\`$Local\) \{ '127\.0\.0\.1' \} else \{ '0\.0\.0\.0' \}\)" -and $d -notmatch "'0\.0\.0\.0'") 'the deploy never passes 0.0.0.0'
    Assert-True ($d -match 'tailscale serve --bg --http=' -and -not @($d -split "\r?\n" | Where-Object { $_ -match 'Invoke-Haven' -and $_ -match '(?i)funnel' }).Count) 'Tailscale Serve (tailnet only), never Funnel (public)'
}

Fact 'P133_TheDiscoveryIsParsedIntoTilesAndEveryRunningContainerIsInTheInventory' {
    $real = @(Get-ChildItem -LiteralPath (Join-Path $repo 'docker\haven\discovery') -Filter 'haven-*.txt')
    Assert-True ($real.Count -ge 2) 'the real discoveries on record'
    $latest = 'D:\UO\haven-ops\discovery-latest.txt'
    $files = @($real | ForEach-Object { $_.FullName }) + @($(if (Test-Path -LiteralPath $latest) { $latest }))
    foreach ($f in $files) {
        $d = ConvertFrom-HavenDiscovery -Text ([IO.File]::ReadAllText($f))
        Assert-True (@($d.Containers | Where-Object { $_.Running }).Count -ge 1) ('containers read from ' + $f)
        $cov = Test-HavenInventoryCoverage -Inventory $p133Inv -Discovery $d
        Assert-Equal '' ($cov.Missing -join ', ') ('running on Haven in ' + (Split-Path $f -Leaf) + ' but not in docker\haven\services.json: add them')
        $yaml = ConvertTo-HomepageServices -Inventory $p133Inv -Discovery $d
        foreach ($c in @($d.Containers | Where-Object { $_.Running })) { Assert-True ($yaml -match ('container: "' + [regex]::Escape($c.Name) + '"')) ($c.Name + ' has a tile') }
    }
    $d = ConvertFrom-HavenDiscovery -Text ([IO.File]::ReadAllText($real[0].FullName))
    Assert-Equal 'running' (@($d.Containers | Where-Object { $_.Name -eq 'sl-proxy' })[0].State) 'the docker ps table'
    Assert-True (-not @($d.Containers | Where-Object { $_.Name -eq 'sweet_payne' })[0].Running) 'an exited container is not running'
    # The deploy's own shape (cc-P133), literal because this tool writes it.
    $mine = "host=haven`n-- containers`n  sl-proxy|running|Up 3 days|jc21/nginx-proxy-manager:latest|0.0.0.0:80->80/tcp|/home/chase/shattered-legacy/docker/proxy`n  mystery-box|running|Up 2 hours|nginx:alpine||/home/chase/x`n  old-thing|exited|Exited (0) 2 days ago|alpine||`n-- listening ports`n  0.0.0.0:80  `n  192.168.1.61:3000  `n-- tailscale`n  installed: yes`n  ip: 100.98.32.16`n  serve operator: no`n"
    $d = ConvertFrom-HavenDiscovery -Text $mine
    Assert-Equal 3 @($d.Containers).Count 'three containers'
    Assert-Equal '/home/chase/shattered-legacy/docker/proxy' $d.Containers[0].Compose 'its compose folder'
    Assert-Equal '0.0.0.0:80|192.168.1.61:3000' (@($d.Listening | ForEach-Object { ($_ -split '\s+')[0] }) -join '|') 'the listening addresses'
    Assert-Equal '100.98.32.16' $d.Tailscale['ip'] 'Tailscale''s address'
    $cov = Test-HavenInventoryCoverage -Inventory $p133Inv -Discovery $d
    Assert-Equal 'mystery-box' ($cov.Missing -join ',') 'a running container nobody listed is named; an exited one is not'
    $yaml = ConvertTo-HomepageServices -Inventory $p133Inv -Discovery $d
    Assert-True ($yaml -match '"mystery-box \(not in the inventory\)":' -and $yaml -match 'container: "mystery-box"') 'and still gets a tile, in Admin, until the inventory has it'
}

Fact 'P133_TheDeployServesOnTheTailnetSavesItsDiscoveryAndBuildsFromTheInventory' {
    $d = [IO.File]::ReadAllText((Join-Path $repo 'docker\dashboard\haven\Deploy-MegaDashboard.ps1'))
    Assert-True ($d -match 'ss -tlnp' -and $d -match 'docker ps -a --format' -and $d -match 'tailscale ip -4') 'Discover prints every container, every listening port and Tailscale'
    Assert-True ($d -match 'discovery-latest\.txt') 'and keeps it, outside both repos, for the coverage fact'
    Assert-True ($d -match 'Build-HavenDashboard\.ps1' -and $d -match 'Test-HavenInventoryCoverage') 'the config is built from services.json and drift is printed in the plan'
    Assert-True ($d -match "tailscaleServe" -and $d -match "http://127\.0\.0\.1:") 'Serve targets come from the inventory and are loopback only'
    Assert-True ($d -match 'cp -p ' -and $d -match 'backup-') 'the compose, config and env on Haven are backed up before they are replaced'
    foreach ($f in @((Join-Path $repo 'docker\dashboard\haven\docker-compose.yml'), (Join-Path $repo 'docker\ops\docker-compose.yml'), 'C:\dev\sunflower\print-queue\docker-compose.yml', (Join-Path $repo 'docker\scripts-dev\docker-compose.yml'), (Join-Path $repo 'docker\portainer-haven\docker-compose.yml'))) {
        $c = [IO.File]::ReadAllText($f)
        Assert-True (@([regex]::Matches($c, 'restart: unless-stopped')).Count -ge 1) ($f + ' survives a restart')
    }
    $c = [IO.File]::ReadAllText((Join-Path $repo 'docker\dashboard\haven\docker-compose.yml'))
    Assert-True ($c -match 'haven\.tail81098d\.ts\.net') 'Homepage allows its tailnet name'
    foreach ($f in @((Join-Path $repo 'docker\ops\docker-compose.yml'), (Join-Path $repo 'docker\ops\README.md'), (Join-Path $repo 'docker\test-queue\html\nav.js'))) {
        Assert-True ([IO.File]::ReadAllText($f) -notmatch '(?i)ops\.shatteredlegacyuo\.com|Access is Haven step H4|behind\s+Cloudflare Access will') ($f + ': no Cloudflare tunnel or Access in the next steps')
    }
}

Fact 'P133_ThePrintQueueProvesItsKeyFromHavenBeforeTheTowerStops' {
    $s = [IO.File]::ReadAllText('C:\dev\sunflower\print-queue\haven\Deploy-PrintQueue.ps1')
    $check = $s.IndexOf('Check the key from Haven')
    $stop = $s.IndexOf('Stop-PcServer', $s.IndexOf("Step 'Move the data'"))
    Assert-True ($check -gt 0 -and $stop -gt $check) 'the SSH check from Haven runs before the tower copy is stopped'
    Assert-True ($s -match 'sunflower-live true' -and $s -match 'REFUSED: Hostinger does not accept') 'a refused key stops the move and the tower keeps pushing'
    Assert-True ($s -match "Invoke-WebRequest -UseBasicParsing -Method Post -Uri \(\`$pageUrl \+ 'api/push'\)") 'the first push from Haven is made and checked right after the move'
    Assert-True ($s -notmatch '(?i)\.ssh\\id_|id_ed25519|id_rsa|accept-new') 'still: never Chase''s key, never a blind host key'
}

Fact 'P133_TheSnapshotReportsEveryGamePcServiceTheDashboardShows' {
    $keys = @($p133Inv.services | ForEach-Object { @($_.tiles) } | Where-Object { $_.snapshot } | ForEach-Object { [string]$_.snapshot } | Sort-Object -Unique)
    $s = [IO.File]::ReadAllText((Join-Path $repo 'scripts\Publish-GamePcSnapshot.ps1'))
    Assert-True ($s -match 'docker\\haven\\services\.json' -and $s -match 'services\s*=') 'the snapshot reads its list from the inventory and sends services'
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p133-ops-' + [guid]::NewGuid().ToString('N') + '.json')
    $j = $p107Ops | ConvertFrom-Json
    $j.stateFile = Join-Path ([IO.Path]::GetTempPath()) 'p133-no-such-state.json'
    [IO.File]::WriteAllText($tmp, ($j | ConvertTo-Json -Depth 5))
    try {
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\Publish-GamePcSnapshot.ps1') -Config $tmp -DryRun -NoBotPlan 2>&1 | Out-String)
        $json = @(($out -split "`n") | Where-Object { $_ -match '^\{' })[0] | ConvertFrom-Json
        foreach ($k in $keys) { Assert-True ($json.services.PSObject.Properties.Name -contains $k) ('the snapshot reports ' + $k + ': ' + $out) }
    } finally { Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue }
}

Fact 'P133_TheRunbookAndTheRuleKeepTheInventoryAndTheDocsInStep' {
    $rb = [IO.File]::ReadAllText('D:\UO\shard-migration\notes\haven-runbook.md')
    foreach ($s in @($p133Inv.services)) { Assert-True ($rb -match ('(?m)^### .*`' + [regex]::Escape($s.key) + '`')) ('the runbook has a section for ' + $s.key) }
    foreach ($h in '## The machines', '## Deploy buttons', '## Tokens and keys', '## Never move or expose', '## Changed on') { Assert-True ($rb.Contains($h)) ('the runbook has ' + $h) }
    foreach ($a in @((Join-Path $repo 'AGENTS.md'), 'D:\UO\shard-migration\AGENTS.md')) {
        Assert-True ([IO.File]::ReadAllText($a) -match 'services\.json[^\r\n]*haven-runbook\.md|haven-runbook\.md[^\r\n]*services\.json') ($a + ' carries the rule')
    }
}

# --- cc-P163: the dashboard after the first Haven deploy (2026-10-18), and Portainer on Haven -------------------------

$p163Yaml = [IO.File]::ReadAllText((Join-Path $p133Cfg 'services.yaml'))
$p163Disc = ConvertFrom-HavenDiscovery -Text ([IO.File]::ReadAllText((Join-Path $repo 'docker\haven\discovery\haven-2026-10-18.txt')))
function Get-P163Svc([string]$Key) { @($p133Inv.services | Where-Object { $_.key -eq $Key })[0] }
function Get-P163Tile([string]$Yaml, [string]$Name) {
    # One generated tile's lines, from '    - "Name":' to the next tile or section. The YAML writer is ours (cc-P133), so
    # its text is the contract here; P133's facts read the same file with Homepage's own parser.
    $lines = $Yaml -split "`n"
    $out = New-Object 'System.Collections.Generic.List[string]'
    $in = $false
    foreach ($l in $lines) {
        if ($l -match ('^    - "?' + [regex]::Escape($Name) + '"?:$')) { $in = $true; $out.Add($l); continue }
        if ($in -and $l -match '^(- |    - )') { break }
        if ($in) { $out.Add($l) }
    }
    $(if ($out.Count) { $out -join "`n" } else { $null })
}

Fact 'P163_TheTowerPrintQueueIsRetiredAndHavensRuns' {
    $tower = Get-P163Svc 'print-queue-tower'
    Assert-True ([string]$tower.state -like 'retired 2026-10-18*') ('the tower copy is retired: ' + $tower.state)
    Assert-Equal 0 @($tower.tiles).Count 'and has no tile'
    $pq = Get-P163Svc 'print-queue'
    Assert-Equal 'running' $pq.state
    Assert-True ([string]$pq.verified -match '2026-10-18 08:56' -and [string]$pq.verified -notmatch 'not deployed') ('verified says the 10-18 deploy: ' + $pq.verified)
    Assert-True ($p163Yaml -notmatch 'tower copy' -and $p163Yaml -notmatch 'localhost:8787') 'no tile for the tower copy, and nothing points at the tower''s port'
    Assert-True (Get-P163Tile $p163Yaml 'Print queue') 'Haven''s print queue keeps its tile'
}

Fact 'P163_PlannedRetiredAndNotInstalledServicesGetNoTile' {
    Assert-Equal 'none' (Get-HavenTileMode ([pscustomobject]@{ state = 'planned'; kind = 'container' })) 'planned'
    Assert-Equal 'none' (Get-HavenTileMode ([pscustomobject]@{ state = 'retired 2026-10-18 (moved)'; kind = 'process' })) 'retired'
    Assert-Equal 'none' (Get-HavenTileMode ([pscustomobject]@{ state = 'built, not installed yet'; kind = 'process' })) 'not installed yet'
    Assert-Equal 'by-hand' (Get-HavenTileMode ([pscustomobject]@{ state = 'stopped (started by hand when needed)'; kind = 'on-demand' })) 'stopped by design'
    Assert-Equal 'live' (Get-HavenTileMode ([pscustomobject]@{ state = 'running'; kind = 'container' })) 'running'
    foreach ($s in @($p133Inv.services | Where-Object { (Get-HavenTileMode $_) -eq 'none' })) {
        foreach ($tile in @($s.tiles)) { Assert-True (-not (Get-P163Tile $p163Yaml ([string]$tile.name))) ($s.key + ' (' + $s.state + ') has no tile, but ' + $tile.name + ' is there') }
    }
    # The rule on a copy: the Legion scripts' dev channel planned again loses its tile, and running gets it back.
    $copy = [IO.File]::ReadAllText((Join-Path $repo 'docker\haven\services.json')) | ConvertFrom-Json
    $dev = @($copy.services | Where-Object { $_.key -eq 'scripts-dev' })[0]
    $dev.state = 'planned'
    Assert-True (-not (Get-P163Tile (ConvertTo-HomepageServices -Inventory $copy) 'Legion scripts (dev)')) 'planned: no tile'
    $dev.state = 'running'
    Assert-True ((Get-P163Tile (ConvertTo-HomepageServices -Inventory $copy) 'Legion scripts (dev)') -match 'siteMonitor: "http://192\.168\.1\.61:8097/scripts\.json"') 'running: the tile, with its monitor'
    Assert-Equal 'running' (Get-P163Svc 'scripts-dev').state 'the dev channel runs on Haven since 10-18 (Discover 09:46)'
}

Fact 'P163_TheDevStoreSaysStoppedStartedByHandInsteadOfARedMonitor' {
    $t = Get-P163Tile $p163Yaml 'Dev store'
    Assert-True $t 'the Dev store keeps its tile'
    Assert-True ($t -match 'description: "Stopped, started by hand\. ') ('it says so: ' + $t)
    Assert-True ($t -notmatch 'siteMonitor|widget|container:|server:') ('and has no monitor: ' + $t)
    Assert-True ($t -match 'href: "http://192\.168\.1\.58:8080"') 'the link still opens it when it runs'
}

Fact 'P163_NpmIsWatchedAtItsFrontDoorAndItsAdminLinkStaysTheTunnel' {
    $t = Get-P163Tile $p163Yaml 'Nginx Proxy Manager'
    Assert-True ($t -match 'siteMonitor: "http://192\.168\.1\.61:80/"') ('NPM''s front door, which answers from inside Homepage''s container: ' + $t)
    Assert-True ($t -match ('href: "' + [regex]::Escape([string]$config.Websites.Npm.Url) + '"') -and $config.Websites.Npm.Url -eq 'http://localhost:8081/') 'the admin link is still the console''s SSH tunnel'
    Assert-True ($p163Yaml -notmatch 'siteMonitor: "https?://(localhost|127\.0\.0\.1)') 'no tile monitors localhost: inside Homepage''s container that is Homepage itself'
}

Fact 'P163_TheContainersDiscoverFoundAreInTheInventoryWithTheirRealPorts' {
    foreach ($k in 'monitoring-grafana', 'monitoring-prometheus', 'npm-admin-tailnet') {
        $s = Get-P163Svc $k
        Assert-True $s ('services.json has ' + $k)
        Assert-True ($s.container -eq $k -and $s.host -eq 'haven' -and $s.owner -eq 'home lab' -and $s.state -eq 'running') ($k + ': container, host, owner, state')
        Assert-True ([string]$s.verified -match 'Discover 2026-10-18' -and @($s.backup).Count -ge 1 -and @($s.ports).Count -ge 1) ($k + ': where it came from, ports, backup')
        foreach ($tile in @($s.tiles)) { Assert-True ((Get-P163Tile $p163Yaml ([string]$tile.name)) -match 'siteMonitor: "http://100\.98\.32\.16:') ($tile.name + ' is watched on Haven''s tailnet address, where it listens') }
    }
    Assert-Equal 0 @($p133Inv.services | Where-Object { $_.key -in 'grafana', 'prometheus' -or $_.container -in 'grafana', 'prometheus' }).Count 'the planned names are renamed, not kept beside the real ones'
    Assert-Equal 'monitoring-prometheus' ((Get-P163Svc 'spoolman').neededBy -join ',') 'and the links follow the rename'
    Assert-Equal '' ((Test-HavenInventoryCoverage -Inventory $p133Inv -Discovery $p163Disc).Missing -join ', ') 'every running container on 10-18 is in the inventory'
    Assert-Equal '' (@(Compare-HavenDiscoveredPorts -Inventory $p133Inv -Discovery $p163Disc | ForEach-Object { $_.Name }) -join ', ') 'and publishes what its entry says, with the image it names'
    $all = @(Compare-HavenDiscoveredPorts -Inventory $p133Inv -Discovery $p163Disc -All)
    Assert-Equal '100.98.32.16:3001' (@($all | Where-Object { $_.Name -eq 'monitoring-grafana' })[0].Published -join ',') 'Grafana, from the 10-18 text'
    Assert-Equal '0.0.0.0:443,0.0.0.0:80,127.0.0.1:81' (@($all | Where-Object { $_.Name -eq 'sl-proxy' })[0].Published -join ',') 'NPM, the [::] twins left out'
    Assert-Equal '127.0.0.1:8093,192.168.1.61:8093,192.168.1.61:8094' (@($all | Where-Object { $_.Name -eq 'sl-play-tests' })[0].Published -join ',') 'the ops pages'
}

Fact 'P163_DiscoverParsesPortsAndMountsAndThePlanSaysWhatToFillIn' {
    $p = @(ConvertFrom-DockerPorts '0.0.0.0:80-81->80-81/tcp, [::]:7912->8000/tcp, 2375/tcp')
    Assert-Equal '0.0.0.0:80>80|0.0.0.0:81>81|[::]:7912>8000|:0>2375' (($p | ForEach-Object { $_.Bind + ':' + $_.HostPort + '>' + $_.Port }) -join '|') 'a range, an IPv6 twin and an exposed port'
    Assert-Equal 0 @(ConvertFrom-DockerPorts '').Count 'no ports'
    $text = [IO.File]::ReadAllText((Join-Path $repo 'docker\haven\discovery\haven-2026-10-18.txt'))
    $moved = ($text -replace '100\.98\.32\.16:3001->3000', '100.98.32.16:3002->3000' -replace 'grafana/grafana:13\.1\.8', 'other/grafana:1') -replace '-- containers\r?\n', ("-- containers`n" + 'stray-box|running|Up 1 minute|nginx:alpine|192.168.1.61:8099->80/tcp|/home/chase/x' + "`n")
    $r = @(Compare-HavenDiscoveredPorts -Inventory $p133Inv -Discovery (ConvertFrom-HavenDiscovery -Text $moved))
    $g = @($r | Where-Object { $_.Name -eq 'monitoring-grafana' })[0]
    Assert-True ($g -and ($g.NotListed -join ',') -eq '100.98.32.16:3002' -and ($g.NotPublished -join ',') -eq '100.98.32.16:3001' -and -not $g.ImageOk) 'a moved port and another image are named'
    $x = @($r | Where-Object { $_.Name -eq 'stray-box' })[0]
    Assert-True ($x -and $x.Key -eq '' -and ($x.Published -join ',') -eq '192.168.1.61:8099') 'a container with no entry is named with its ports'
    Assert-Equal 2 $r.Count ('only those two: ' + (($r | ForEach-Object { $_.Name }) -join ', '))
    $m = ConvertFrom-HavenDiscovery -Text ("-- mounts`n  /monitoring-grafana|grafana-data>/var/lib/grafana;/opt/monitoring/grafana.ini>/etc/grafana/grafana.ini;`n  /npm-admin-tailnet|`n")
    Assert-Equal 'grafana-data>/var/lib/grafana|/opt/monitoring/grafana.ini>/etc/grafana/grafana.ini' ($m.Mounts['monitoring-grafana'] -join '|') 'mounts: a volume''s name, a bind''s path'
    Assert-Equal 0 @($m.Mounts['npm-admin-tailnet']).Count 'a container with none'
    $d = [IO.File]::ReadAllText((Join-Path $repo 'docker\dashboard\haven\Deploy-MegaDashboard.ps1'))
    Assert-True ($d -match 'echo "-- mounts"' -and $d -match '\{\{range \.Mounts\}\}') 'Discover prints every running container''s mounts'
    Assert-True ($d -match 'Compare-HavenDiscoveredPorts' -and $d -match 'PORTS OR IMAGES DIFFER' -and $d -match "like 'from Discover\*'") 'and the plan prints what differs and the mounts to fill in'
    Assert-Equal 3 @($p133Inv.services | Where-Object { @(@($_.volumes) | Where-Object { [string]$_.host -like 'from Discover*' }).Count }).Count 'the three entries waiting on Discover say so'
}

Fact 'P163_PortainerOnHavenHasADeployAndARollBackLikeTheOthers' {
    $w = $config.Websites
    foreach ($f in @(@('Deploy-PortainerHaven.ps1', 'Deploy', 'portainer-haven'), @('Deploy-PortainerHaven-Rollback.ps1', 'Rollback', 'portainer-haven-rollback'))) {
        Assert-Equal ('D:\UO\haven-migration\' + $f[0]) $w.Deploys[$f[2]].Script $f[2]
        $t = [IO.File]::ReadAllText('D:\UO\haven-migration\' + $f[0])
        Assert-True ($t -match ([regex]::Escape("& 'D:\ShatteredLegacy\docker\portainer-haven\Deploy-PortainerHaven.ps1' -Mode " + $f[1]) + '\r?\n')) ($f[0] + ' runs only its mode')
    }
    foreach ($k in 'web.portainer-haven', 'web.portainer-haven-rollback') {
        $p = @(Get-ActionPlan -Action $k -Config $config -State $state)
        Assert-Equal 'confirm,window,log' (($p | ForEach-Object { $_.Kind }) -join ',') $k
        Assert-Equal 'deploy to haven' $p[0].Phrase
        Assert-Equal ('-Deploy ' + $k.Substring(4)) ((@($p[1].Arguments) | Select-Object -Last 2) -join ' ')
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ((Test-PlanSafety $p $config) -join ' | ')
        Assert-True ([string](Get-ConsoleTooltips)[$k]) ($k + ' has a tooltip')
        Assert-Equal 'Portainer on Haven' (Get-ConfirmSubject $k)
        Assert-True ($p[2].Text -eq ('web.deploy portainer-haven ' + $k.Substring(4))) ('the log line: ' + $p[2].Text)
    }
    $c = [IO.File]::ReadAllText((Join-Path $repo 'docker\portainer-haven\docker-compose.yml'))
    Assert-True ($c -match '(?m)^\s+image: portainer/portainer-ce:2\.45\.2\s*$') 'Portainer CE, pinned (2.45.2 is lts, the docs'' tag)'
    Assert-True ($c -match 'portainer_data:/data' -and $c -match 'name: \$\{PT_VOLUME:-portainer_data\}' -and $c -match '/var/run/docker\.sock:/var/run/docker\.sock') 'the docs'' data volume, by its own name, and the Docker socket'
    Assert-True ($c -notmatch ':8000' -and $c -match '\$\{PT_LOOP_BIND:-127\.0\.0\.1\}:\$\{PT_HTTP_LOOP_PORT:-9000\}:9000') 'no Edge port; plain HTTP on loopback only'
    $s = Get-P163Svc 'portainer-haven'
    Assert-True ([string]$s.image -eq 'portainer/portainer-ce:2.45.2' -and (@($s.backup) -join ' ') -match 'portainer_data' -and [string]$s.compose -match 'docker\\portainer-haven') 'the inventory names the same image, the volume in its backup, and the compose'
    $serve = @($p133Inv.tailscaleServe | Where-Object { $_.service -eq 'portainer-haven' })[0]
    Assert-True ($serve -and $serve.tailnetPort -eq 9000 -and $serve.target -eq 'http://127.0.0.1:9000') 'Serve: the tailnet''s 9000 to loopback HTTP (cc-P133''s rule)'
    $t = Get-P163Tile $p163Yaml 'Portainer (Haven)'
    Assert-True ($t -match 'href: "http://haven\.tail81098d\.ts\.net:9000/"' -and $t -match 'siteMonitor: "https://192\.168\.1\.61:9443/api/system/status"' -and $t -match 'url: "https://192\.168\.1\.61:9443"' -and $t -match 'key: "\{\{HOMEPAGE_VAR_PORTAINER_KEY\}\}"') ('the tile and widget point at it: ' + $t)
    $d = [IO.File]::ReadAllText((Join-Path $repo 'docker\portainer-haven\Deploy-PortainerHaven.ps1'))
    Assert-True ($d -match 'IsSystem\) \{ Write-Host .REFUSED' -and $d -match "'DRY RUN: stopping here; nothing was changed\.'" -and $d -match "Mode = 'Discover'") 'refuses SYSTEM, has a dry run, Discover by default'
    Assert-True ($d -match 'MAKE THE ADMIN USER WITHIN 5 MINUTES' -and $d -match 'setup_token=' -and $d -match 'api/users/admin/check') 'the first login: the 5 minutes in big letters, with the setup token, only while no admin exists'
    $tar = $d.IndexOf('tar czf'); $up = $d.IndexOf("Compose 'up -d")
    Assert-True ($tar -gt 0 -and $up -gt $tar) 'the data volume is backed up before anything is replaced'
    Assert-True ($d -match "is not this deploy''s" -and $d -notmatch '0\.0\.0\.0' -and -not @($d -split "\r?\n" | Where-Object { $_ -match 'Invoke-Haven' -and $_ -match '(?i)funnel' }).Count) 'never another project''s Portainer, never all interfaces, never Funnel'
}

Fact 'P163_ThePortainerDeployDryRunReadsPlansAndChangesNothing' {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('p163-proof-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $bin = Join-Path $root 'bin'
    New-Item -ItemType Directory -Force -Path $bin | Out-Null
    # A stand-in tailscale, so the dry run's Discover never reads or changes this PC's real one.
    $ts = "#!/bin/sh`ncase `"`$1`" in`n  debug) echo '{ `"OperatorUser`": `"'`$(id -un)'`" }' ;;`n  serve) echo 'No serve config' ;;`n  ip) echo 100.64.0.9 ;;`nesac`n"
    [IO.File]::WriteAllText((Join-Path $bin 'tailscale'), $ts, (New-Object Text.UTF8Encoding $false))
    try {
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'docker\portainer-haven\Deploy-PortainerHaven.ps1') -Mode Deploy -DryRun -Local -Root $root -ProofBin $bin `
            -Name 'p163-proof-portainer' -Volume 'p163_proof_data' -Project 'p163-proof' -Port 19443 -HttpLoopPort 19000 2>&1 | Out-String)
        $code = $LASTEXITCODE
        Assert-Equal 0 $code $out
        foreach ($want in '== Discover: this PC \(proof', '-- containers', '== Plan: Deploy', 'nothing to back up: p163_proof_data does not exist yet', '3\. docker compose -p p163-proof up -d \(portainer/portainer-ce:2\.45\.2',
            'tailscale serve --bg --http=9000 http://127\.0\.0\.1:19000', 'first-login link, the 5-minute limit', 'DRY RUN: stopping here; nothing was changed\.') {
            Assert-True ($out -match $want) ('the dry run says ' + $want + ': ' + $out)
        }
        Assert-Equal '' ((& docker ps -a --filter 'name=p163-proof-portainer' --format '{{.Names}}' 2>$null) -join ',') 'no container was made'
        $ErrorActionPreference = 'Continue'
        & docker volume inspect p163_proof_data 2>&1 | Out-Null
        $vcode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        Assert-True ($vcode -ne 0) 'no volume was made'
        Assert-Equal 'bin' ((@(Get-ChildItem -LiteralPath $root) | ForEach-Object { $_.Name }) -join ',') 'nothing was copied'
    } finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}

# --- cc-P154: the console stays responsive. Reads in runspaces of their own; the window only draws -------------------

function New-P154Form {
    # A form as the console's own start builds it (-BackgroundRefresh), on the capture, never reaching the network, shown
    # off screen so its timers tick. $Stub: the refresh runspace's script in place of Get-ConsoleRefreshData.
    param([string]$Stub)
    $script:NetProbeDisabled = $true
    $script:RefreshScript = $Stub
    $script:LastRefresh = $null
    $f = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -BackgroundRefresh
    $script:ui.Timer.Stop()
    $f.StartPosition = 'Manual'
    $f.Location = New-Object Drawing.Point(-3000, -3000)
    $f
}

function Get-P154Controls($Root) {
    $stack = New-Object System.Collections.Stack
    $stack.Push($Root)
    while ($stack.Count) { $x = $stack.Pop(); $x; foreach ($k in $x.Controls) { $stack.Push($k) } }
}

function Close-P154Form($f) {
    try { $f.Close() } catch { }
    try { $f.Dispose() } catch { }
    Stop-ConsoleJobs
    $script:RefreshScript = $null
    $script:BackgroundRefresh = $false
    $script:NetProbeDisabled = $false
}

function Wait-P154 {
    # Pumps the window's messages until $Until is true or $Seconds pass; returns the longest gap between two ticks of a
    # 15 ms timer on the window's thread (how long the window could not answer) and how many ticks there were.
    param([double]$Seconds, [scriptblock]$Until)
    $gaps = New-Object 'System.Collections.Generic.List[double]'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $probe = New-Object Windows.Forms.Timer
    $probe.Interval = 15
    $probe.Add_Tick({ $gaps.Add($sw.Elapsed.TotalMilliseconds); $sw.Restart() }.GetNewClosure())
    $probe.Start()
    $w = [Diagnostics.Stopwatch]::StartNew()
    try {
        while ($w.Elapsed.TotalSeconds -lt $Seconds) {
            [Windows.Forms.Application]::DoEvents()
            if ($Until -and (& $Until)) { break }
            Start-Sleep -Milliseconds 5
        }
    } finally { $probe.Stop(); $probe.Dispose() }
    [pscustomobject]@{ Ticks = $gaps.Count; Longest = $(if ($gaps.Count) { ($gaps | Measure-Object -Maximum).Maximum } else { [double]::MaxValue }); Ms = $w.ElapsedMilliseconds }
}

$p154Sleep = 'param($cfg, $fromCapture, $statusFrom, $parts, $dry) $t0 = [datetime]::UtcNow; Start-Sleep -Milliseconds 2000; [pscustomobject]@{ Parts = @(); Marker = ''stub''; Started = $t0; Ended = [datetime]::UtcNow; Thread = [Threading.Thread]::CurrentThread.ManagedThreadId }'

Fact 'P154_TheRefreshNeverRunsOnTheWindowsThread' {
    # A stub source that sleeps 2 s: the build returns without waiting for it, and while it sleeps the window's thread
    # still answers its timer; the stub ran on another thread, and its result is drawn when it is back.
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $f = New-P154Form $p154Sleep
    try {
        $built = $sw.ElapsedMilliseconds
        Assert-True (Test-Path variable:script:rsJobs) 'the jobs table'
        Assert-True ($script:rsJobs['refresh']) 'the build started a refresh and did not wait for it'
        $f.Show()
        $p = Wait-P154 15 { $null -ne $script:LastRefresh }
        Assert-True ($script:LastRefresh -and $script:LastRefresh.Data.Marker -eq 'stub') ('the stub''s result was drawn (waited ' + $p.Ms + ' ms)')
        Assert-True ($script:LastRefresh.Data.Thread -ne [Threading.Thread]::CurrentThread.ManagedThreadId) 'it ran on a thread of its own'
        Assert-True ($p.Ticks -ge 40) ('the window''s timer ticked ' + $p.Ticks + ' times while the source slept 2 s')
        Assert-True ($p.Longest -lt 500) ('the longest the window could not answer: ' + [int]$p.Longest + ' ms (the source slept 2000)')
        Assert-True ($built -lt 2000 + 15000) ('built in ' + $built + ' ms')
    } finally { Close-P154Form $f }
}

Fact 'P154_NeverTwoRefreshesAtOnce' {
    # While one runs a tick is skipped (and counted); a queued one starts only after the first has finished.
    $f = New-P154Form $p154Sleep
    try {
        $f.Show()
        [void](Wait-P154 15 { $null -ne $script:LastRefresh })
        $script:LastRefresh = $null
        $script:RefreshSkipped = 0
        Assert-True (Start-ConsoleRefresh -Parts @('tiles')) 'the first starts'
        $first = $script:rsJobs['refresh']
        Assert-True (-not (Start-ConsoleRefresh -Parts @('tiles'))) 'a tick while it runs is skipped'
        Assert-Equal 1 $script:RefreshSkipped 'and counted'
        Assert-True ($script:rsJobs['refresh'] -eq $first) 'the one running is still the first'
        Assert-Equal $null $script:refreshQueued 'a skipped tick asks for nothing more'
        Assert-True (-not (Start-ConsoleRefresh -Parts @('commit') -Queue)) 'a click while it runs does not start a second either'
        Assert-Equal 'commit' (@($script:refreshQueued) -join ',') 'it is queued'
        $seen = New-Object 'System.Collections.Generic.List[object]'
        [void](Wait-P154 20 { if ($script:LastRefresh -and ($seen.Count -eq 0 -or $seen[$seen.Count - 1] -ne $script:LastRefresh)) { $seen.Add($script:LastRefresh) }; $seen.Count -ge 2 })
        Assert-Equal 2 $seen.Count 'the first, then the queued one'
        Assert-True ($seen[1].Data.Started -ge $seen[0].Data.Ended) ('the second started ' + $seen[1].Data.Started.ToString('HH:mm:ss.fff') + ', after the first ended ' + $seen[0].Data.Ended.ToString('HH:mm:ss.fff'))
        Assert-Equal 'commit' (@($seen[1].Parts) -join ',') 'with the parts it was asked for'
    } finally { Close-P154Form $f }
}

Fact 'P154_OnlyTheVisibleTabsSourcesRun' {
    # The tiles always; the Commit tab's git only on the Commit tab, the package light's facts on the Player package tab
    # (and when two minutes old); and a refresh reads exactly the parts it was given.
    $f = New-P154Form $p154Sleep
    try {
        $f.Show()
        $script:packageFactsAt = [datetime]::UtcNow
        $script:ui.Tabs.SelectedIndex = 0
        Assert-Equal 'tiles,vpn' ((Get-RefreshParts) -join ',') 'the Test shard tab: the tiles only'
        $script:ui.Tabs.SelectedTab = $script:ui.CommitPage
        Assert-Equal 'tiles,vpn,commit' ((Get-RefreshParts) -join ',') 'the Commit tab: and its git'
        $script:ui.Tabs.SelectedTab = $script:ui.PackagePage
        Assert-Equal 'tiles,vpn,package' ((Get-RefreshParts) -join ',') 'the Player package tab: and the package light''s facts'
        $script:ui.Tabs.SelectedTab = $script:ui.AutopilotPage
        Assert-Equal 'tiles,vpn' ((Get-RefreshParts) -join ',') 'the Golem tab: its view is a tile''s, read with the tiles'
        $script:packageFactsAt = [datetime]::UtcNow.AddSeconds(-1 - $script:PackageFactsMaxAge)
        Assert-Equal 'tiles,vpn,package' ((Get-RefreshParts) -join ',') 'package facts two minutes old are read again, for the lights file'
        $d = Get-ConsoleRefreshData -Config $config -FromCapture $true -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Parts @('tiles') -Dry $true
        Assert-Equal 'docker,golem,rebuild light,play tests,total' (@($d.Timings.Keys) -join ',') 'the tiles read docker, the Golem, the rebuild light and play-tests.json, and nothing else'
        Assert-True ($d.State -and $d.Autopilot -and $null -eq $d.CommitFacts -and $null -eq $d.PackageFacts -and $null -eq $d.VpnFacts) 'and return only those'
        $d = Get-ConsoleRefreshData -Config $config -FromCapture $true -StatusFrom '' -Parts @('golem') -Dry $true
        Assert-Equal 'golem,total' (@($d.Timings.Keys) -join ',') 'the Golem alone reads the Golem alone'
        # The Golem tab's panel is drawn only while its tab shows.
        $script:ui.Tabs.SelectedIndex = 0
        $script:ui.AutopilotPanel.Text = 'untouched'
        [void](Show-AutopilotView $d.Autopilot)
        Assert-Equal 'untouched' $script:ui.AutopilotPanel.Text 'not drawn while another tab shows'
        $script:ui.Tabs.SelectedTab = $script:ui.AutopilotPage
        [void](Show-AutopilotView $d.Autopilot)
        Assert-True ($script:ui.AutopilotPanel.Text -ne 'untouched' -and $script:ui.AutopilotPanel.TextLength -gt 0) 'drawn when it shows'
    } finally { Close-P154Form $f }
}

Fact 'P154_TheFileCachesReadAgainOnlyWhenAFileChanges' {
    $d = Join-Path ([IO.Path]::GetTempPath()) ('p154-cache-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $d | Out-Null
    try {
        $p = Join-Path $d 'cc-P7-x.md'
        [IO.File]::WriteAllText($p, "one`ntwo`n")
        Assert-Equal "one`ntwo" (Get-FileHeadCached $p 40) 'read'
        # Same length and write time: the cached text, so the file is not read again.
        $t = (Get-Item -LiteralPath $p).LastWriteTimeUtc
        [IO.File]::WriteAllText($p, "uno`ntwo`n")
        (Get-Item -LiteralPath $p).LastWriteTimeUtc = $t
        Assert-Equal "one`ntwo" (Get-FileHeadCached $p 40) 'unchanged by its write time and length: from the cache'
        # A new write time: read again.
        (Get-Item -LiteralPath $p).LastWriteTimeUtc = $t.AddSeconds(5)
        Assert-Equal "uno`ntwo" (Get-FileHeadCached $p 40) 'a file changed is read again'
        [IO.File]::WriteAllText($p, "uno`ntwo`nthree`n")
        (Get-Item -LiteralPath $p).LastWriteTimeUtc = $t.AddSeconds(5)
        Assert-Equal "uno`ntwo`nthree" (Get-FileHeadCached $p 40) 'and a new length is a change too'
        Assert-Equal $null (Get-FileHeadCached (Join-Path $d 'none.md') 40) 'no file: null'
        # The folder listing: again only when the folder's own write time changed (a file added, removed, renamed).
        Assert-Equal $p (Find-FileCached $d 'cc-P7-*.md') 'found'
        Assert-Equal $null (Find-FileCached $d 'cc-P8-*.md') 'not there yet'
        $q = Join-Path $d 'cc-P8-y.md'
        [IO.File]::WriteAllText($q, 'x')
        $dt = (Get-Item -LiteralPath $d).LastWriteTimeUtc
        (Get-Item -LiteralPath $d).LastWriteTimeUtc = $dt.AddSeconds(5)
        Assert-Equal $q (Find-FileCached $d 'cc-P8-*.md') 'a file added is found'
        Remove-Item -LiteralPath $q
        (Get-Item -LiteralPath $d).LastWriteTimeUtc = $dt.AddSeconds(10)
        Assert-Equal $null (Find-FileCached $d 'cc-P8-*.md') 'and a file removed is gone'
    } finally { Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P154_APanelFilledInOneGoLooksAsLineByLine' {
    # Set-RichLines (one .Rtf) in place of Add-RichLine per line: the same text, colors, bold and font, in each look.
    $f = New-Object Windows.Forms.Form
    $a = New-Object Windows.Forms.RichTextBox; $b = New-Object Windows.Forms.RichTextBox
    $f.Controls.AddRange(@($a, $b))
    $f.StartPosition = 'Manual'; $f.Location = New-Object Drawing.Point(-3000, -3000)
    $keep = @($script:theme, $script:MonoFont, $script:BoldFont)
    try {
        $f.Show()
        $lines = foreach ($i in 0..13) { [pscustomobject]@{ Text = ('line {' + $i + '} a\b ' + [char]0x2192 + " x`ty"); Color = @('normal', 'head', 'green', 'amber', 'red', 'gray', 'nosuch')[$i % 7] } }
        foreach ($look in 'Plain', 'UO', 'Enhanced') {
            $script:theme = Get-ConsoleTheme $look
            $script:MonoFont = Get-ThemeFont 'Mono'
            $script:BoldFont = Get-ThemeFont 'Mono' ([Drawing.FontStyle]::Bold)
            $a.Clear(); foreach ($l in $lines) { Add-RichLine $a $l.Text $l.Color }
            Set-RichLines $b $lines
            Assert-True ($a.Text -ceq $b.Text) ($look + ': the same text')
            for ($i = 0; $i -lt $a.TextLength; $i++) {
                $a.Select($i, 1); $b.Select($i, 1)
                if ($a.SelectionColor.ToArgb() -ne $b.SelectionColor.ToArgb() -or $a.SelectionFont.Bold -ne $b.SelectionFont.Bold -or $a.SelectionFont.Name -ne $b.SelectionFont.Name -or [math]::Abs($a.SelectionFont.Size - $b.SelectionFont.Size) -gt 0.01) {
                    throw ($look + ': character ' + $i + ' differs: ' + $a.SelectionColor + ' ' + $a.SelectionFont + ' against ' + $b.SelectionColor + ' ' + $b.SelectionFont)
                }
            }
        }
    } finally { $script:theme, $script:MonoFont, $script:BoldFont = $keep; $f.Close(); $f.Dispose() }
}

Fact 'P154_TheWorldSetupTabIsBuiltWhenFirstOpenedAndOnlyThen' {
    $f = New-P154Form $p154Sleep
    try {
        Assert-True ($script:worldPending) 'not built with the window'
        Assert-Equal 'worldLoading' $script:ui.WorldPage.Controls[0].Name 'it says loading'
        $f.Show()
        [void](Wait-P154 30 { $null -ne $script:worldCmds })
        Assert-True ($null -ne $script:worldCmds) 'the registrations were read in the background'
        Assert-True ($script:worldPending) 'and still not built: the tab is not open'
        $script:ui.Tabs.SelectedTab = $script:ui.WorldPage
        Assert-True (-not $script:worldPending) 'built on opening'
        $ticks = @(Get-P154Controls $script:ui.WorldPage | Where-Object { $_.Name -eq 'worldTick' }).Count
        Assert-True ($ticks -ge 1) ('ticks: ' + $ticks)
        $eager = New-WorldSetupTable $config
        Assert-Equal @(Get-P154Controls $eager | Where-Object { $_.Name -eq 'worldTick' }).Count $ticks 'the same rows the window used to build at its start'
        $eager.Dispose()
    } finally { Close-P154Form $f }
}

# --- cc-P155: the status file both consoles read (the web console reads only it) --------------------------------------

function Test-P155PythonShape([string]$Path) {
    # The web console's own check of the file (web-console/panels.py status_problems): '' when it reads it as schema 1.
    $code = "import json,sys; sys.path.insert(0, r'" + (Join-Path $repo 'web-console') + "'); import panels; print(' | '.join(panels.status_problems(json.load(open(r'" + $Path + "', encoding='utf-8-sig')))))"
    ((& python -I -c $code 2>&1) | Out-String).Trim()
}

Fact 'P155_ARefreshBecomesTheStatusFileAndBothConsolesReadIt' {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p155-status-' + [guid]::NewGuid().ToString('N') + '.json')
    try {
        $d = Get-ConsoleRefreshData -Config $config -FromCapture $true -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt') -Parts @('tiles') -Dry $true
        $net = [pscustomobject]@{ RouteIsLan = $true; RouteError = $null; VpnAdapters = @(); DnsIps = @('203.0.113.9'); DnsError = $null; PublicIp = '203.0.113.9'; PublicIpError = $null; At = [datetime]::UtcNow }
        $p = Write-ConsoleStatusFile -Data $d -Config $config -NetResult $net -WebResults @{} -By 'console' -Path $tmp
        Assert-Equal $tmp $p 'written where asked'
        Assert-Equal $tmp (Write-ConsoleStatusFile -Data $d -Config $config -NetResult $net -WebResults @{} -By 'console' -Path $tmp) 'and written over, the second time (every refresh after the first)'
        Assert-Equal 0 @(Get-ChildItem -LiteralPath (Split-Path $tmp -Parent) -Filter ((Split-Path $tmp -Leaf) + '.*.tmp')).Count 'no temporary file left beside it'
        $j = [IO.File]::ReadAllText($tmp) | ConvertFrom-Json
        Assert-Equal 1 $j.schema 'schema 1'
        Assert-Equal 'console' $j.generatedBy 'who wrote it'
        Assert-Equal 'live,test,publisher,dns,golem,playtests,websites' ((@($j.tiles) | ForEach-Object { $_.key }) -join ',') 'every tile the console shows, in its order'
        $tiles = @(Get-AtAGlance $d.State $config)
        Assert-Equal ($tiles[1].Title + ': ' + $tiles[1].Word) ($j.tiles[1].title + ': ' + $j.tiles[1].word) 'the TEST tile as the console judges it'
        Assert-True ($j.golem -and $j.golem.state -and $null -ne $j.golem.queue) 'the Golem, with its queue'
        Assert-True ($j.lights.PSObject.Properties.Name -contains 'rebuild' -and $j.lights.PSObject.Properties.Name -contains 'package') 'both lights'
        Assert-Equal '' (Test-P155PythonShape $tmp) 'the web console reads the same file as schema 1'
        $back = Read-ConsoleStatusFile $tmp
        Assert-True ($back -and $back.AgeSeconds -ge 0 -and $back.AgeSeconds -lt 60) 'and this console reads it back, with its age'
        [IO.File]::WriteAllText($tmp, '{"schema": 2}')
        Assert-Equal $null (Read-ConsoleStatusFile $tmp) 'another schema is not read'
        Assert-True (-not ([IO.File]::ReadAllText($p) -match '(?i)token|password')) 'nothing secret in it'
    } finally { Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue }
}

Fact 'P155_TheConsolePaintsItsTilesFromTheStatusFileAtItsStart' {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p155-tiles-' + [guid]::NewGuid().ToString('N') + '.json')
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        $doc = [ordered]@{ schema = 1; generatedAt = ([datetime]::Now.AddSeconds(-30)).ToString('s'); generatedBy = 'collector'
            tiles = @([ordered]@{ key = 'test'; title = 'TEST'; word = 'FROMFILE'; detail = 'read from the status file'; color = 'green' },
                      [ordered]@{ key = 'golem'; title = 'GOLEM'; word = 'RESTING'; detail = 'until 16:00'; color = 'gray' }) }
        [IO.File]::WriteAllText($tmp, ($doc | ConvertTo-Json -Depth 5))
        Assert-True (Show-StatusFileTiles $tmp) 'a fresh file is painted'
        Assert-Equal 'TEST: FROMFILE' $script:ui.Tiles[1].Word.Text 'the TEST tile, from the file'
        Assert-True ($script:ui.Tiles[1].Detail.Text -match '\(as of \d+s ago\)') ('marked with its age: ' + $script:ui.Tiles[1].Detail.Text)
        Assert-Equal 'GOLEM: RESTING' $script:ui.AutopilotTile.Word.Text 'the GOLEM tile too'
        $doc.generatedAt = ([datetime]::Now.AddMinutes(-11)).ToString('s')
        $doc.tiles[0].word = 'STALE'
        [IO.File]::WriteAllText($tmp, ($doc | ConvertTo-Json -Depth 5))
        Assert-True (-not (Show-StatusFileTiles $tmp)) 'a file older than ten minutes is left alone'
        Assert-Equal 'TEST: FROMFILE' $script:ui.Tiles[1].Word.Text 'and the tile keeps what it had'
        Assert-True (-not (Show-StatusFileTiles (Join-Path ([IO.Path]::GetTempPath()) 'p155-none.json'))) 'no file: nothing'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false; Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue }
}

Fact 'P155_TheConsolesOwnRefreshWritesTheStatusFile' {
    # The console's refresh runspace writes it after a refresh of the tiles (never from a capture: a dry run is not this PC).
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p155-own-' + [guid]::NewGuid().ToString('N') + '.json')
    $f = New-P154Form $null
    try {
        $f.Show()
        [void](Wait-P154 30 { $null -ne $script:LastRefresh })
        $c = $config.PSObject.Copy()
        $c.StatusFile = $tmp
        $script:cfg = $c
        $script:fromCapture = $false
        $script:LastRefresh = $null
        Assert-True (Start-ConsoleRefresh -Parts @('tiles')) 'a refresh of the tiles'
        [void](Wait-P154 60 { $null -ne $script:LastRefresh })
        Assert-True (Test-Path -LiteralPath $tmp) 'wrote the status file'
        $j = [IO.File]::ReadAllText($tmp) | ConvertFrom-Json
        Assert-Equal 'console' $j.generatedBy 'as the console'
        Assert-Equal '' (Test-P155PythonShape $tmp) 'in the shape the web console reads'
        Remove-Item -LiteralPath $tmp
        $script:fromCapture = $true
        $script:LastRefresh = $null
        [void](Start-ConsoleRefresh -Parts @('tiles'))
        [void](Wait-P154 60 { $null -ne $script:LastRefresh })
        Assert-True (-not (Test-Path -LiteralPath $tmp)) 'from a capture it writes nothing'
    } finally { Close-P154Form $f; Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue }
}

Fact 'P155_ThePromptsSummaryAndSizeAreRead' {
    $t = "# CC task P155: Web console, phase 1`n`n**Model:** Opus`n`n**Summary:** The new web console's first phase.`n`n**Kind:** a new Python service, tests. Large.`n`nWritten by the overseer for Chase.`n"
    $i = Get-PromptInfo 'cc-P155-cont-web.md' $t
    Assert-Equal "The new web console's first phase." $i.Summary 'the Summary line'
    Assert-Equal 'Written by the overseer for Chase.' $i.Para 'the first paragraph is still the prose after it'
    Assert-Equal 'large' (Get-PromptSize $i.Kind) 'the size from the Kind line'
    Assert-Equal 'small to medium' (Get-PromptSize 'scripts and tests. Small to medium.') 'a range'
    Assert-Equal '' (Get-PromptInfo 'cc-P1-x.md' "# CC task P1: x`n`n**Model:** Opus`n").Summary 'none: empty'
}

Fact 'P155_TheCollectorScriptWritesTheStatusFile' {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p155-collector-' + [guid]::NewGuid().ToString('N') + '.json')
    try {
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts\Write-ConsoleStatus.ps1') -Out $tmp 2>&1 | Out-String)
        Assert-True (Test-Path -LiteralPath $tmp) ('written: ' + $out)
        Assert-Equal 'collector' (([IO.File]::ReadAllText($tmp) | ConvertFrom-Json).generatedBy) 'as the collector'
        Assert-Equal '' (Test-P155PythonShape $tmp) 'in the shape the web console reads'
        $s = [IO.File]::ReadAllText((Join-Path $repo 'scripts\Write-ConsoleStatus.ps1'))
        Assert-True ($s -match 'Get-ConsoleRefreshData' -and $s -match 'Write-ConsoleStatusFile' -and $s -notmatch 'function Get-(AtAGlance|ShardGlance|RebuildLight)') 'the console''s own functions, no second copy'
    } finally { Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue }
}

Fact 'P155_TheWebConsoleListensOnTheLoopbackOnlyAndPublishesNothing' {
    $py = [IO.File]::ReadAllText((Join-Path $repo 'web-console\server.py'))
    Assert-True ($py -match 'default="127\.0\.0\.1"' -and $py -match 'if a\.bind not in \("127\.0\.0\.1", "::1"\)') 'server.py: 127.0.0.1 by default, and any other bind refused'
    $out = & { $ErrorActionPreference = 'Continue'; (& python -I (Join-Path $repo 'web-console\server.py') --bind 0.0.0.0 --no-collector 2>&1 | ForEach-Object { [string]$_ }) -join "`n" }
    Assert-True ($out -match 'refused: the web console listens on the loopback only') ('0.0.0.0 refused: ' + $out)
    $c = [IO.File]::ReadAllText((Join-Path $repo 'web-console\tailscale\docker-compose.yml'))
    Assert-True ($c -notmatch '(?m)^\s+ports:' -and $c -notmatch 'network_mode:\s*host') 'the sidecar publishes no port'
    $sv = [IO.File]::ReadAllText((Join-Path $repo 'web-console\tailscale\serve.json')) | ConvertFrom-Json
    Assert-True (@($sv.AllowFunnel.PSObject.Properties | Where-Object { $_.Value -ne $false }).Count -eq 0 -and @($sv.AllowFunnel.PSObject.Properties).Count -eq 1) 'Funnel off'
    Assert-True ($c -match 'image: tailscale/tailscale:v\d+\.\d+\.\d+' -and $c -notmatch 'TS_AUTHKEY:\s*\S') 'a pinned image, and the key only from the env file'
}

# --- cc-P135: the Map and Art tab. validate-art.py's checks from the console, read only; the in-game map commands' syntax ---

Write-Host "cc-P135 Map and Art facts" -ForegroundColor Cyan

function Get-MapArtActionPlan { param([string]$Action, [string]$VdFile, [string]$Body, [string]$HuesFile) @(Get-ActionPlan -Action $Action -ShardKey 'mapart' -Config $config -State $state -Now $now -VdFile $VdFile -Body $Body -HuesFile $HuesFile) }

Fact 'P135_TheFourArtChecksRunOnlyValidateArtReadOnlyAndReportExitOne' {
    $va = $config.ValidateArt
    Assert-True (Test-Path -LiteralPath $va) $va
    $want = @{ 'art.registry' = @('check-registry'); 'art.bodies' = @('bodies') }
    foreach ($k in $want.Keys) {
        $p = Get-MapArtActionPlan $k
        $ex = @($p | Where-Object { $_.Kind -eq 'exec' })
        Assert-Equal 1 $ex.Count ($k + ': one exec step')
        Assert-Equal $config.Python $ex[0].Exe $k
        Assert-Equal ($va + ' ' + ($want[$k] -join ' ')) ($ex[0].Arguments -join ' ') $k
        Assert-Equal 1 $ex[0].Named['ReportExit'] ($k + ': exit 1 is problems found, not a failure')
        Assert-Equal 0 @($p | Where-Object { $_.Kind -in @('confirm', 'ask', 'window', 'script', 'copy', 'move', 'remove-snapshot', 'log') }).Count ($k + ': reads only, confirms nothing, logs nothing')
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count ($k + ': ' + (@(Test-PlanSafety $p $config) -join '; '))
    }
    $p = Get-MapArtActionPlan 'art.checkvd' -VdFile $p135Vd -Body '2052'
    $ex = @($p | Where-Object { $_.Kind -eq 'exec' })[0]
    Assert-Equal ($va + ' check-vd ' + $p135Vd + ' 2052') ($ex.Arguments -join ' ') 'a body: check-vd on the file and body'
    $p = Get-MapArtActionPlan 'art.checkvd' -VdFile $p135Vd
    $ex = @($p | Where-Object { $_.Kind -eq 'exec' })[0]
    Assert-Equal ($va + ' propose ' + $p135Vd) ($ex.Arguments -join ' ') 'no body: propose one'
    Assert-True ((@($p | Where-Object { $_.Kind -eq 'say' })[0]).Text -match 'propose one') 'and the plan says so'
    $p = Get-MapArtActionPlan 'art.hues' -HuesFile $p135Hues
    $ex = @($p | Where-Object { $_.Kind -eq 'exec' })[0]
    Assert-Equal ($va + ' hues-diff ' + $config.OurHues + ' ' + $p135Hues) ($ex.Arguments -join ' ') 'ours first, theirs second'
    foreach ($k in 'art.checkvd', 'art.hues') { Assert-Equal 0 @(Test-PlanSafety (Get-MapArtActionPlan $k -VdFile $p135Vd -HuesFile $p135Hues) $config).Count $k }
}

Fact 'P135_CheckVdAndHuesRefuseAMissingWrongOrOwnFileAndANonNumericBody' {
    $refuse = { param($p) @($p | Where-Object { $_.Kind -eq 'refuse' }) }
    Assert-True ((& $refuse (Get-MapArtActionPlan 'art.checkvd'))[0].Text -match 'Pick a \.vd file first') 'no file'
    Assert-True ((& $refuse (Get-MapArtActionPlan 'art.checkvd' -VdFile (Join-Path $p135Dir 'missing.vd')))[0].Text -match 'Not a \.vd file that exists') 'a missing file'
    Assert-True ((& $refuse (Get-MapArtActionPlan 'art.checkvd' -VdFile $p135Hues))[0].Text -match 'Not a \.vd file') 'the wrong kind of file'
    Assert-True ((& $refuse (Get-MapArtActionPlan 'art.checkvd' -VdFile $p135Vd -Body '0x804'))[0].Text -match 'number in decimal') 'hex is refused'
    Assert-True ((& $refuse (Get-MapArtActionPlan 'art.checkvd' -VdFile $p135Vd -Body 'kirin'))[0].Text -match 'number in decimal') 'a word is refused'
    Assert-True ((& $refuse (Get-MapArtActionPlan 'art.hues'))[0].Text -match 'Pick the other hues\.mul first') 'no hues file'
    Assert-True ((& $refuse (Get-MapArtActionPlan 'art.hues' -HuesFile $p135Vd))[0].Text -match 'Not a \.mul file') 'a .vd is not a hues file'
    if (Test-Path -LiteralPath $config.OurHues) { Assert-True ((& $refuse (Get-MapArtActionPlan 'art.hues' -HuesFile $config.OurHues))[0].Text -match 'our own hues\.mul') 'ours against ours is refused' }
    foreach ($p in @((Get-MapArtActionPlan 'art.checkvd'), (Get-MapArtActionPlan 'art.hues'), (Get-MapArtActionPlan 'art.checkvd' -VdFile $p135Vd -Body 'kirin'))) {
        Assert-Equal 0 @($p | Where-Object { $_.Kind -eq 'exec' }).Count 'a refused plan runs nothing'
    }
}

Fact 'P135_TheSafetyCheckRefusesPythonOutsideValidateArtAndRenderOrAnyOtherSubcommand' {
    $va = $config.ValidateArt
    $bad = @(
        @{ Why = 'render writes PNGs';        Step = (New-PlanStep -Kind exec -Exe 'python' -Arguments @($va, 'render', '132', '0', '2', '--out', 'D:\x')) },
        @{ Why = 'another script';            Step = (New-PlanStep -Kind exec -Exe 'python' -Arguments @((Join-Path $repo 'scripts\find-free-art.py'), 'bodies')) },
        @{ Why = 'python with -c';            Step = (New-PlanStep -Kind exec -Exe 'python' -Arguments @('-c', 'print(1)')) },
        @{ Why = 'python3.exe too';           Step = (New-PlanStep -Kind exec -Exe 'C:\Python\python3.exe' -Arguments @('-m', 'http.server')) },
        @{ Why = 'validate-art in a window';  Step = (New-PlanStep -Kind window -Exe 'python' -Arguments @($va, 'bodies')) },
        @{ Why = 'validate-art through cmd';  Step = (New-PlanStep -Kind exec -Exe 'cmd.exe' -Arguments @('/c', 'python', $va, 'bodies')) },
        @{ Why = 'a subcommand not listed';   Step = (New-PlanStep -Kind exec -Exe 'python' -Arguments @($va, 'actions', '132')) },
        @{ Why = 'propose with --out';        Step = (New-PlanStep -Kind exec -Exe 'python' -Arguments @($va, 'propose', $p135Vd, '--out', 'D:\x')) }
    )
    foreach ($b in $bad) {
        $v = @(Test-PlanSafety @($b.Step) $config)
        Assert-True ($v.Count -ge 1 -and ($v -join ' ') -match 'python runs here only as') ($b.Why + ': ' + ($v -join '; '))
    }
    foreach ($sub in $config.ArtSubcommands) {
        $args = @($va, $sub)
        if ($sub -in @('check-vd', 'propose')) { $args += $p135Vd }
        if ($sub -eq 'check-vd') { $args += '2052' }
        if ($sub -eq 'hues-diff') { $args += @($config.OurHues, $p135Hues) }
        Assert-Equal 0 @(Test-PlanSafety @((New-PlanStep -Kind exec -Exe 'python' -Arguments $args)) $config).Count ($sub + ' is allowed')
    }
    Assert-Equal 'bodies,check-registry,check-vd,propose,hues-diff' ($config.ArtSubcommands -join ',') 'and render is not among them'
}

Fact 'P135_EveryMapButtonPrintsItsCommandsSyntaxAndRunsNothing' {
    $src = [IO.File]::ReadAllText((Join-Path $repo 'server\customizations\StaffHub\ClusterFMapCheckCommands.cs'))
    $keys = @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Map commands' } | ForEach-Object { $_.Key })
    Assert-Equal 'map.footprint,map.spots,map.statics,map.walkable,map.shore,map.export' ($keys -join ',')
    foreach ($c in Get-MapCommandSyntax) {
        Assert-True ($keys -contains $c.Key) ($c.Key + ' is a button')
        Assert-True ($src -match ('CommandSystem\.Register\("' + [regex]::Escape($c.Command) + '", AccessLevel\.GameMaster')) ($c.Command + ' is registered GameMaster in ClusterFMapCheckCommands.cs')
        Assert-True ($src -match ('\[Usage\("' + [regex]::Escape($c.Usage.Substring(1)) + '"\)')) ($c.Command + ': the usage printed is the one the command declares: ' + $c.Usage)
        $p = Get-MapArtActionPlan $c.Key
        Assert-Equal 'say,say,say' (@($p | ForEach-Object { $_.Kind }) -join ',') ($c.Key + ' only says')
        Assert-True ($p[0].Text.StartsWith($c.Usage)) ($c.Key + ': ' + $p[0].Text)
        Assert-True ($p[2].Text -match 'the console cannot run in-game commands') $c.Key
        Assert-Equal 0 @(Test-PlanSafety $p $config).Count $c.Key
        $r = Invoke-ClickCapture $c.Key 'mapart' $false
        Assert-Equal 0 $r.Spawned.Count ($c.Key + ' opens no window')
        Assert-Equal 1 $r.Executed.Count ($c.Key + ' prints here')
    }
    Assert-Equal 6 @(Get-MapCommandSyntax).Count
}

Fact 'P135_TheChildLineCarriesTheVdFileBodyAndHuesFileAndOnlyDigitsForTheBody' {
    $l = Get-ChildArgumentLine -SelfPath 'C:\x\Shard-Console.ps1' -ActionKey 'art.checkvd' -ShardKey 'mapart' -VdFile 'D:\art\my kirin.vd' -Body '2052'
    Assert-True ($l -match '-Mode Execute -Action art\.checkvd ') $l
    Assert-True ($l -match ' -VdFile "D:\\art\\my kirin\.vd"') $l
    Assert-True ($l -match ' -Body 2052') $l
    Assert-True ($l -notmatch '-Shard ') 'no shard'
    $l2 = Get-ChildArgumentLine -SelfPath 'C:\x\Shard-Console.ps1' -ActionKey 'art.checkvd' -ShardKey 'mapart' -VdFile 'D:\a".vd' -Body '0x804'
    Assert-True ($l2 -match ' -VdFile "D:\\a\.vd"') ('a quote is dropped: ' + $l2)
    Assert-True ($l2 -notmatch '-Body') ('a body that is not digits does not cross: ' + $l2)
    $l3 = Get-ChildArgumentLine -SelfPath 'C:\x\Shard-Console.ps1' -ActionKey 'art.hues' -ShardKey 'mapart' -HuesFile 'E:\other\hues.mul' -DryRun
    Assert-True ($l3 -match '-Mode DryRun -Action art\.hues ' -and $l3 -match ' -HuesFile "E:\\other\\hues\.mul"') $l3
    # And a click hands the boxes over: dry, one window, with the file and body.
    $r = Invoke-ClickCapture 'art.checkvd' 'mapart' $true -VdFile $p135Vd -Body '2052'
    Assert-Equal 1 $r.Spawned.Count 'one window'
    Assert-True ($r.Spawned[0] -match (' -VdFile "' + [regex]::Escape($p135Vd) + '" -Body 2052')) $r.Spawned[0]
    Assert-True ($r.Spawned[0] -match '-Mode DryRun -Action art\.checkvd') $r.Spawned[0]
    $r = Invoke-ClickCapture 'art.hues' 'mapart' $true -HuesFile $p135Hues
    Assert-True ($r.Spawned[0] -match (' -HuesFile "' + [regex]::Escape($p135Hues) + '"')) $r.Spawned[0]
    $r = Invoke-ClickCapture 'art.checkvd' 'mapart' $true -VdFile ''
    Assert-Equal 0 $r.Spawned.Count 'no file, no window'
    Assert-True (@($r.Lines | Where-Object { $_ -match 'Pick a \.vd file first' }).Count -ge 1) ('and the refusal is said: ' + ($r.Lines -join ' | '))
}

Fact 'P135_AReportedExitCodeIsSaidInAmberAndTheRunIsDoneAnyOtherFails' {
    # A throwaway script that exits as told, as the D38 facts do; never validate-art itself.
    $dir = Join-Path $p135Dir 'exit'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $ps1 = Join-Path $dir 'child.ps1'
    Set-Content -LiteralPath $ps1 -Encoding ASCII -Value @("Write-Output 'p135 found two problems'", 'exit ([int]$args[0])')
    $cfg = Get-ConsoleConfig -RepoRoot $repo
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    $lines = New-Object 'System.Collections.Generic.List[string]'
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $lines.Add($Color + '|' + $Text) }
    $one = New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ps1, '1') -Text 'exits 1' -Named ([ordered]@{ ReportExit = 1 })
    $two = New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ps1, '2') -Text 'exits 2' -Named ([ordered]@{ ReportExit = 1 })
    $plain = New-PlanStep -Kind exec -Exe 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ps1, '1') -Text 'exits 1, not reported'
    Assert-Equal $true (Invoke-Plan -Plan @($one) -Config $cfg -ActionKey 'fact' -ShardKey 'mapart') 'the reported exit is done'
    Assert-True (@($lines | Where-Object { $_ -match '^amber\|.*exited with 1: it found something' }).Count -eq 1) ('said in amber: ' + ($lines -join ' | '))
    Assert-True (@($lines | Where-Object { $_ -match 'p135 found two problems' }).Count -ge 1) 'the output is shown'
    $lines.Clear()
    Assert-Equal $false (Invoke-Plan -Plan @($two) -Config $cfg -ActionKey 'fact' -ShardKey 'mapart') 'another exit code fails'
    Assert-True (@($lines | Where-Object { $_ -match 'exited with 2' }).Count -ge 1) ($lines -join ' | ')
    $lines.Clear()
    Assert-Equal $false (Invoke-Plan -Plan @($plain) -Config $cfg -ActionKey 'fact' -ShardKey 'mapart') 'exit 1 with nothing declared fails, as before'
}

Fact 'P135_TheTabIsBuiltWithItsBoxesButtonsAndTooltips' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $config -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-Equal 'Map and Art' $script:ui.MapArtPage.Text
        Assert-True ($script:ui.Tabs.TabPages.Contains($script:ui.MapArtPage)) 'in the tab control'
        Assert-Equal ([array]::IndexOf(@($script:ui.Tabs.TabPages | ForEach-Object { $_.Text }), 'World setup') + 1) ([array]::IndexOf(@($script:ui.Tabs.TabPages | ForEach-Object { $_.Text }), 'Map and Art')) 'right after World setup'
        Assert-Equal 'art.registry,art.bodies,art.checkvd,art.hues,map.footprint,map.spots,map.statics,map.walkable,map.shore,map.export' (($script:ui.MapArtButtons | ForEach-Object { $_.Tag }) -join ',')
        Assert-Equal 'Check the art registry,Body table,Check a .vd,Compare a hues.mul with ours,[CheckFootprint,[ValidateSpots,[FindStaticProblems,[Walkable,[ShoreCheck,[ExportPoints' (($script:ui.MapArtButtons | ForEach-Object { $_.Text }) -join ',')
        Assert-Equal 'art.vdfile,art.body,art.huesfile' (@($script:ui.VdFileBox.Name, $script:ui.BodyBox.Name, $script:ui.HuesFileBox.Name) -join ',')
        $script:ui.VdFileBox.Text = ' ' + $p135Vd + ' '
        $script:ui.BodyBox.Text = '2052'
        $script:ui.HuesFileBox.Text = $p135Hues
        $i = Get-MapArtInputs
        Assert-Equal $p135Vd $i.VdFile 'trimmed'
        Assert-Equal '2052' $i.Body
        Assert-Equal $p135Hues $i.HuesFile
        $tips = Get-ConsoleTooltips
        foreach ($k in 'tab.Map and Art', 'art.registry', 'art.bodies', 'art.checkvd', 'art.hues', 'art.vdfile', 'art.browsevd', 'art.body', 'art.huesfile', 'art.browsehues', 'map.footprint', 'map.spots', 'map.statics', 'map.walkable', 'map.shore', 'map.export') { Assert-True ($tips.Contains($k)) ('a tooltip for ' + $k) }
        $browse = @($script:ui.MapArtPage.Controls[0].Controls | Where-Object { $_.Name -in @('art.browsevd', 'art.browsehues') })
        Assert-Equal 2 $browse.Count 'two Browse buttons'
    } finally { $form.Dispose(); $script:NetProbeDisabled = $false }
}

Fact 'P135_EveryMapAndArtButtonRunHeadlessWithDryRunRunsNothing' {
    $cases = @(@('art.registry', @()), @('art.bodies', @()), @('art.checkvd', @('-VdFile', $p135Vd, '-Body', '2052')), @('art.checkvd', @('-VdFile', $p135Vd)), @('art.hues', @('-HuesFile', $p135Hues)), @('map.footprint', @()), @('map.walkable', @()), @('map.export', @()))
    foreach ($c in $cases) {
        $extra = @($c[1])
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Resolve-Path $script).Path -DryRun -Action $c[0] @extra 2>&1 | Out-String)
        Assert-Equal 0 $LASTEXITCODE ($c[0] + ': ' + $out)
        Assert-True ($out -match ('== DRY RUN: ' + [regex]::Escape($c[0]) + ' \(mapart\)')) ($c[0] + ': ' + $out)
        Assert-True ($out -match '\(dry run: nothing was run\)') ($c[0] + ': ' + $out)
        Assert-True ($out -notmatch 'REFUSE') ($c[0] + ': ' + $out)
        if ($c[0] -like 'art.*') { Assert-True ($out -match 'RUN      python ') ($c[0] + ': the RUN line names python: ' + $out) }
    }
    # And without a mode, a read-only action still runs (none of these changes anything); its banner says so.
    $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Resolve-Path $script).Path -Action map.shore 2>&1 | Out-String)
    Assert-Equal 0 $LASTEXITCODE $out
    Assert-True ($out -match 'MAP AND ART  map\.shore \(map and art\)' -and $out -match '\[ShoreCheck \[stepThreshold\]') $out
}

# --- cc-P161: the Golem tab says which Golem sessions are open, kept open for Chase, and last closed -----------------------
Fact 'P161_TheGolemTabListsItsSessionsAndSaysWhichAreKeptOpen' {
    $sx = [ordered]@{ closeFinished = $true; closeAfterMinutes = 2; window = 'tabs'
        open = @([ordered]@{ name = 'Golem P160'; pid = 11; openedAt = '2026-10-17T20:00:00'; why = 'context'; reported = $true }, [ordered]@{ name = 'Golem P161'; pid = 12; openedAt = '2026-10-17T21:22:56'; why = 'console'; reported = $true })
        kept = @([ordered]@{ name = 'Golem P160'; pid = 11; why = 'P160 STOPPED: left open until you have seen it' })
        closed = @([ordered]@{ name = 'Golem P159'; at = '2026-10-17T19:00:00'; why = 'superseded by Golem P160' }) }
    $j = (New-P131Status -State 'RUNNING' -Current 'P161') | ConvertFrom-Json
    $j | Add-Member -NotePropertyName sessions -NotePropertyValue ([pscustomobject]$sx) -Force
    $v = Get-AutopilotView -Enabled $true -StateText '{"current":"P161","currentFile":"cc-P161-a.md","lastHandled":160}' -LogLines @() -QueueNames @() -Errors @() -StatusText ($j | ConvertTo-Json -Depth 6) -SettingsText $p131Settings -Kind golem -Extra $x
    $rows = @(Format-AutopilotPanel -View $v -Config $p125Cfg -Now $p125Now)
    $row = @($rows | Where-Object { $_.Text -like 'sessions  :*' }) | Select-Object -First 1
    Assert-True ($row) ('a sessions line: ' + (($rows | ForEach-Object { $_.Text }) -join ' | '))
    if ($row) {
        Assert-Equal 'sessions  : 2 open (Golem P160, Golem P161); finished ones close 2 min after their last turn; KEPT OPEN: Golem P160 (P160 STOPPED: left open until you have seen it); last closed Golem P159: superseded by Golem P160' $row.Text 'the line'
        Assert-Equal 'amber' $row.Color 'amber while one is kept for Chase'
    }
    # No sessions field (a Golem before cc-P161): no line.
    $v2 = Get-AutopilotView -Enabled $true -StateText '{"current":"P161","currentFile":"cc-P161-a.md","lastHandled":160}' -LogLines @() -QueueNames @() -Errors @() -StatusText (New-P131Status -State 'RUNNING' -Current 'P161') -SettingsText $p131Settings -Kind golem -Extra $x
    Assert-True (-not @(Format-AutopilotPanel -View $v2 -Config $p125Cfg -Now $p125Now | Where-Object { $_.Text -like 'sessions  :*' }).Count) 'no sessions field: no line'
}

# --- cc-P162: the Golem tab says when the end-of-week window is spending what is left -----------------------------------
Fact 'P162_TheGolemTabSaysWhenTheEndOfWeekWindowSpendsWhatIsLeft' {
    $mk = { param($e) $j = (New-P131Status -State 'RUNNING' -Current 'P162') | ConvertFrom-Json; if ($e) { $j | Add-Member -NotePropertyName endOfWeek -NotePropertyValue ([pscustomobject]$e) -Force }
        $v = Get-AutopilotView -Enabled $true -StateText '{"current":"P162","currentFile":"cc-P162-a.md","lastHandled":161}' -LogLines @() -QueueNames @() -Errors @() -StatusText ($j | ConvertTo-Json -Depth 6) -SettingsText $p131Settings -Kind golem -Extra $x
        @(Format-AutopilotPanel -View $v -Config $p125Cfg -Now $p125Now | Where-Object { $_.Text -like 'end week  :*' }) }
    $in = @(& $mk ([ordered]@{ on = $true; hours = 24; cap = 97; active = $true; until = '2026-10-21T16:00:00'; fableActive = $true; fableUntil = '2026-10-21T16:00:00'; opensAt = '2026-10-20T16:00:00'; weekPct = 80; fablePct = 45 }))
    Assert-Equal 1 $in.Count 'one line inside the window'
    if ($in.Count) {
        Assert-Equal 'end week  : End of week: spending what is left until Wed 4:00 PM (week 80%, 17% left; Fable week 45%, 52% left)' $in[0].Text 'inside'
        Assert-Equal 'green' $in[0].Color 'green inside'
    }
    $out = @(& $mk ([ordered]@{ on = $true; hours = 24; cap = 97; active = $false; until = $null; fableActive = $false; fableUntil = $null; opensAt = '2026-10-20T16:00:00'; weekPct = 40; fablePct = $null }))
    Assert-True ($out.Count -eq 1 -and $out[0].Text -eq 'end week  : opens Tue 4:00 PM (24 h before the weekly reset): then it spends what is left, up to 97%') ('outside: ' + ($out | ForEach-Object { $_.Text }))
    $off = @(& $mk ([ordered]@{ on = $false; hours = 24; cap = 97; active = $false }))
    Assert-True ($off.Count -eq 1 -and $off[0].Text -match 'off \(endOfWeekSpend false\)') ('off: ' + ($off | ForEach-Object { $_.Text }))
    Assert-Equal 0 @(& $mk $null).Count 'no endOfWeek field (a Golem before cc-P162): no line'
}

# --- cc-P144: the Legion scripts row on the Player package tab -----------------------------------------------------------
$p144Dir = Join-Path ([IO.Path]::GetTempPath()) ('sl-console-p144-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path (Join-Path $p144Dir 'clone\.git') -Force | Out-Null
$p144Clone = Join-Path $p144Dir 'clone'
$p144Settings = Join-Path $p144Dir 'legion-scripts.json'
[IO.File]::WriteAllText($p144Settings, (@{ dev = $true; repo = $p144Clone } | ConvertTo-Json))
function Get-P144Plan([string]$Action, $Cfg, $Facts = $pkgFactsOk) {
    @(Get-ActionPlan -Action $Action -ShardKey 'package' -Config $Cfg -State $state -Now $now -PackageFacts $Facts)
}
function Get-P144Config([switch]$WithRepo) {
    $old = $env:SL_LEGION_SETTINGS
    try {
        if ($WithRepo) { $env:SL_LEGION_SETTINGS = $p144Settings }
        Get-ConsoleConfig -RepoRoot $repo
    } finally { $env:SL_LEGION_SETTINGS = $old }
}

Fact 'P144_TheLegionScriptsRowHasItsSixButtonsTooltipsPhrasesAndWindows' {
    $a = @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Legion scripts' })
    Assert-Equal 'package.scripts.check,package.scripts.dev,package.scripts.promote,package.scripts.pull,package.scripts.setup,package.scripts.key' (($a | ForEach-Object { $_.Key }) -join ',') 'the six, in order'
    Assert-Equal 'Check scripts|Publish to dev|Promote to official|Pull scripts|Set up scripts repo|Set up publish key' (($a | ForEach-Object { $_.Label }) -join '|') 'their labels'
    Assert-Equal 'False,True,True,True,True,True' (($a | ForEach-Object { $_.Mutates }) -join ',') 'Check only reads'
    foreach ($x in $a) { Assert-True ([string](Get-ConsoleTooltips)[$x.Key]) ($x.Key + ' has a tooltip'); Assert-Equal 'window' $x.RunIn $x.Key }
    Assert-Equal 'publish scripts to dev' (Get-ConfirmPhrase 'package.scripts.dev')
    Assert-Equal 'promote scripts to official' (Get-ConfirmPhrase 'package.scripts.promote')
    Assert-Equal 'Promote scripts to official' (Get-ConfirmSubject 'package.scripts.promote')
    foreach ($k in 'package.scripts.dev', 'package.scripts.promote', 'package.scripts.setup', 'package.scripts.key') { Assert-True ($script:InteractiveActions -contains $k) ($k + ' asks something, so it gets a window of its own') }
    foreach ($k in 'package.scripts.check', 'package.scripts.pull') { Assert-True ($script:InteractiveActions -notcontains $k) ($k + ' asks nothing') }
    $b = Get-ModeBanner 'execute' 'package.scripts.promote' 'package'
    Assert-True ($b.Color -eq 'red' -and ($b.Lines -join ' ') -match 'EVERY player' -and ($b.Lines -join ' ') -match 'No shard is touched') ($b.Lines -join ' | ')
    Assert-True ((Get-ModeBanner 'execute' 'package.scripts.check' 'package').Lines[2] -match 'changes nothing') 'Check says it changes nothing'
    Assert-Equal 'execute' (Resolve-RunMode -Action 'package.scripts.check') 'a read-only button runs with no mode'
    Assert-Equal 'refuse' (Resolve-RunMode -Action 'package.scripts.dev') 'D37: one that changes something does not'
}

Fact 'P144_PublishAndPromoteRunTheScriptAfterTheirPhraseAndNeverForAShardRepoReason' {
    $cfg = Get-P144Config -WithRepo
    Assert-Equal $p144Clone $cfg.LegionRepo 'the clone from this PC''s settings'
    $dev = @(Get-P144Plan 'package.scripts.dev' $cfg (New-PackageFacts -Dirty @('server/x.cs')))
    Assert-Equal 'confirm,script,log' (($dev | ForEach-Object { $_.Kind }) -join ',') (Format-Plan -Action 'package.scripts.dev' -ShardKey 'package' -Plan $dev)
    Assert-Equal 'publish scripts to dev' $dev[0].Phrase
    Assert-Equal 'Dev' ([string]$dev[1].Named['Mode'])
    Assert-Equal $cfg.LegionPublish $dev[1].Path
    Assert-Equal 0 @(Test-PlanSafety $dev $cfg).Count (@(Test-PlanSafety $dev $cfg) -join ' | ')
    $pro = @(Get-P144Plan 'package.scripts.promote' $cfg)
    Assert-True ($pro[0].Phrase -eq 'promote scripts to official' -and [string]$pro[1].Named['Mode'] -eq 'Promote') (Format-Plan -Action 'package.scripts.promote' -ShardKey 'package' -Plan $pro)
    $pull = @(Get-P144Plan 'package.scripts.pull' $cfg)
    Assert-Equal ('-C|' + $p144Clone + '|pull|--ff-only') (@($pull[0].Arguments) -join '|') 'git pull --ff-only in the clone'
    Assert-Equal 0 @(Test-PlanSafety $pull $cfg).Count (@(Test-PlanSafety $pull $cfg) -join ' | ')
    # The safety check: no phrase, another mode, or git doing anything else, is refused whatever the plan says.
    $noPhrase = @($dev | Where-Object { $_.Kind -ne 'confirm' })
    Assert-True (@(Test-PlanSafety $noPhrase $cfg) -match 'only after the typed phrase publish scripts to dev').Count 'Dev without its phrase'
    $other = @((New-PlanStep -Kind script -Path $cfg.LegionPublish -Named ([ordered]@{ Mode = 'SetupDev' }) -Text 'x'))
    Assert-True (@(Test-PlanSafety $other $cfg) -match 'Check, Dev or Promote').Count 'SetupDev never from a button'
    $push = @((New-PlanStep -Kind exec -Exe 'git' -Arguments @('-C', $p144Clone, 'push', '--force') -Text 'x'))
    Assert-True (@(Test-PlanSafety $push $cfg) -match 'git runs here only as').Count 'git push is not a console step'
    # Without a clone on this PC: Publish to dev and Pull say Set up first; Check, Promote and Setup still run.
    $none = Get-P144Config
    Assert-Equal '' $none.LegionRepo 'no clone'
    foreach ($k in 'package.scripts.dev', 'package.scripts.pull') { $p = @(Get-P144Plan $k $none); Assert-True ($p.Count -eq 1 -and $p[0].Kind -eq 'refuse' -and $p[0].Text -match 'Set up scripts repo first') $k }
    foreach ($k in 'package.scripts.check', 'package.scripts.promote', 'package.scripts.setup', 'package.scripts.key') { Assert-Equal 0 @(Get-P144Plan $k $none | Where-Object { $_.Kind -eq 'refuse' }).Count $k }
    Assert-True (@(Get-P144Plan 'package.scripts.key' $none)[0].Path -like '*\scripts\legion\Setup-PublishKey.ps1') 'the key script'
}

Fact 'P144_ACancelledTypeYesIsACancelNotAFailure' {
    $dir = Join-Path $p144Dir 'exec'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $stand = Join-Path $dir 'Publish-LegionScripts.ps1'
    $cfg = Get-P144Config -WithRepo
    $cfg.LegionPublish = $stand
    $cfg.LogFile = Join-Path $dir 'shard-console.log'
    $p144Lines = New-Object 'System.Collections.Generic.List[string]'
    function Write-ConsoleLine { param([string]$Text, [string]$Color) $p144Lines.Add($Text) }
    $plan = @((New-PlanStep -Kind confirm -Phrase 'publish scripts to dev'), (New-PlanStep -Kind script -Path $stand -Named ([ordered]@{ Mode = 'Dev' }) -Text 'dev'), (New-PlanStep -Kind log -Text 'package.scripts.dev'))
    $go = {
        param([int]$Code)
        Set-Content -LiteralPath $stand -Encoding ASCII -Value @('param([string]$Mode)', "Write-Host 'stand-in'", ('exit ' + $Code))
        $p144Lines.Clear(); $script:PlanCancelled = $false
        $ok = Invoke-Plan -Plan $plan -Config $cfg -ActionKey 'package.scripts.dev' -ShardKey 'package' -ConfirmLive 'publish scripts to dev'
        [pscustomobject]@{ Ok = $ok; Cancelled = [bool]$script:PlanCancelled; Lines = @($p144Lines) }
    }
    $no = & $go 3
    Assert-True (-not $no.Ok -and $no.Cancelled) ($no.Lines -join ' | ')
    Assert-True (@($no.Lines | Where-Object { $_ -match 'Cancelled at its "Type yes"' }).Count) ($no.Lines -join ' | ')
    $bad = & $go 1
    Assert-True (-not $bad.Ok -and -not $bad.Cancelled) ('a failure: ' + ($bad.Lines -join ' | '))
    $yes = & $go 0
    Assert-True ($yes.Ok) ($yes.Lines -join ' | ')
}

Fact 'P144_TheCommitTabAndCommitShardWorkTakeTheScriptsRepoWithTheSameChecks' {
    Assert-Equal 'code,docs' ((@((Get-P144Config).CommitRepos) | ForEach-Object { $_.Name }) -join ',') 'no clone: the two'
    Assert-Equal 'code,docs,scripts' ((@((Get-P144Config -WithRepo).CommitRepos) | ForEach-Object { $_.Name }) -join ',') 'a clone: the third'
    # Commit-ShardWork.ps1 -DryRun against a scratch scripts repo holding a zip: the same refusal as the shard repos.
    $sr = Join-Path $p144Dir 'scripts-repo'
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & git init --quiet $sr 2>&1 | Out-Null
    $ErrorActionPreference = $prev
    [IO.File]::WriteAllText((Join-Path $sr 'SL_New.py'), 'print(1)')
    [IO.File]::WriteAllText((Join-Path $sr 'oops.zip'), 'not really')
    $set = Join-Path $p144Dir 'commit-settings.json'
    [IO.File]::WriteAllText($set, (@{ dev = $true; repo = $sr } | ConvertTo-Json))
    $old = $env:SL_LEGION_SETTINGS
    try {
        $env:SL_LEGION_SETTINGS = $set
        $out = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\UO\Commit-ShardWork.ps1' -DryRun 2>&1 | Out-String)
    } finally { $env:SL_LEGION_SETTINGS = $old }
    Assert-True ($out -match '=== scripts') ('the third repo is read: ' + $out)
    Assert-True ($out -match 'pattern\s+oops\.zip') ('and refused like the others: ' + $out)
    Assert-True ($out -match 'scripts\s+BLOCKED') 'BLOCKED in the result table'
}

Fact 'P144_ThePlayerPackageTabHasTheScriptsRow' {
    $f = New-ConsoleForm -Config (Get-P144Config -WithRepo) -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        Assert-Equal 'package.scripts.check,package.scripts.dev,package.scripts.promote,package.scripts.pull,package.scripts.setup,package.scripts.key' (@($script:ui.ScriptsButtons | ForEach-Object { $_.Tag }) -join ',') 'six buttons'
        Assert-True ($script:ui.ScriptsButtons[0].Parent.Parent.Text -eq 'Player package') 'on the Player package tab'
        Assert-Equal 'package.build,package.publish,package.check' (@($script:ui.PackageButtons | ForEach-Object { $_.Tag }) -join ',') 'the zip buttons are as they were'
    } finally { $f.Dispose() }
}

# cc-P152: the ChatGPT plan (Codex) rows on the Golem tab, and the thin Art tab.
function New-P152CodexText([datetime]$Now, [double]$P = 12, [double]$W = 20, [double]$AgeHours = 1) {
    $pr = [DateTimeOffset]::new($Now.AddHours(3)).ToUnixTimeSeconds(); $wr = [DateTimeOffset]::new($Now.AddDays(4)).ToUnixTimeSeconds()
    ([ordered]@{ schema = 1; updatedAt = $Now.ToString('s'); limits = [ordered]@{ at = $Now.AddHours(-$AgeHours).ToString('yyyy-MM-ddTHH:mm:sszzz'); planType = 'team'
            primary = [ordered]@{ usedPct = $P; windowMinutes = 300; resetsAt = $pr }; secondary = [ordered]@{ usedPct = $W; windowMinutes = 10080; resetsAt = $wr } }
        today = [ordered]@{ date = $Now.ToString('yyyy-MM-dd'); runs = 2; images = 2; tokens = 439212 }
        runs = @([ordered]@{ at = 'x'; primaryBefore = [ordered]@{ usedPct = 2; resetsAt = $pr }; primaryAfter = [ordered]@{ usedPct = 7; resetsAt = $pr }; secondaryBefore = [ordered]@{ usedPct = 19; resetsAt = $wr }; secondaryAfter = [ordered]@{ usedPct = 20; resetsAt = $wr } }) } | ConvertTo-Json -Depth 6)
}

Fact 'P152_TheCodexRowsSayBothWindowsTheGolemsShareAndToday' {
    $now = [datetime]'2026-10-19T09:00:00'
    $rows = @(Get-CodexUsageRows -CodexText (New-P152CodexText $now) -Now $now -Settings ([pscustomobject]@{ codexMaxPctPerWindow = 40; codexWeekReservePct = 30 }))
    Assert-True ($rows[0].Text -match '^ChatGPT plan \(Codex\): reported 1h( 0m)? ago, plan team') $rows[0].Text
    $five = @($rows | Where-Object { $_.Name -eq 'Codex 5-hour' })[0]; $week = @($rows | Where-Object { $_.Name -eq 'Codex week' })[0]
    Assert-Equal 12 $five.Pct
    Assert-True ($five.Text -match '12% used, the Golem 5%' -and $five.Bar) $five.Text
    Assert-True ($week.Text -match '20% used, the Golem 1%') $week.Text
    Assert-Equal 'normal' $week.Color
    Assert-True ($rows[-1].Text -match '2 run\(s\), 2 image\(s\), 439,212 tokens' -and $rows[-1].Text -match 'leaves 30% of the week') $rows[-1].Text
    $hot = @(Get-CodexUsageRows -CodexText (New-P152CodexText $now 50 75) -Now $now -Settings ([pscustomobject]@{ codexMaxPctPerWindow = 40; codexWeekReservePct = 30 }))
    Assert-Equal 'amber' @($hot | Where-Object { $_.Name -eq 'Codex week' })[0].Color 'the week inside the reserve is amber'
}

Fact 'P152_TheCodexRowsAreUnknownWhenTheReadIsOldOrMissing' {
    $now = [datetime]'2026-10-19T09:00:00'
    $old = @(Get-CodexUsageRows -CodexText (New-P152CodexText $now -AgeHours 30) -Now $now)
    Assert-True ($old[0].Text -match 'too old') $old[0].Text
    Assert-True (@($old | Where-Object { $_.Bar -and $null -eq $_.Pct }).Count -eq 2) 'both bars unknown'
    $none = @(Get-CodexUsageRows -CodexText $null -Now $now)
    Assert-True ($none.Count -eq 1 -and $none[0].Text -match 'no codex-usage.json yet') $none[0].Text
    $bad = @(Get-CodexUsageRows -CodexText '{nope' -Now $now)
    Assert-True ($bad[0].Text -match 'unreadable') $bad[0].Text
}

Fact 'P152_TheGolemPanelShowsTheCodexRowsAfterTheClaudeBars' {
    $now = [datetime]'2026-10-19T09:00:00'
    $x = [pscustomobject]@{ PromptTexts = @{}; CompletedTexts = @{}; NotesTexts = @{}; UsageText = $null; PlanUsageText = $null; CodexUsageText = (New-P152CodexText $now); Estimate = $null; TreeClean = $null }
    $v = Get-AutopilotView -Enabled $true -StateText $null -LogLines @() -QueueNames @() -Errors @() -Kind golem -Extra $x
    $panel = @(Format-AutopilotPanel -View $v -Config $config -Now $now) | ForEach-Object { $_.Text }
    $iu = [array]::FindLastIndex([string[]]$panel, [Predicate[string]]{ param($t) $t -like 'usage     :*' })
    $ic = [array]::FindIndex([string[]]$panel, [Predicate[string]]{ param($t) $t -like 'codex     : ChatGPT plan (Codex)*' })
    Assert-True ($iu -ge 0 -and $ic -eq $iu + 1) ('the codex rows follow the usage rows: ' + $iu + ', ' + $ic)
    Assert-True (@($panel | Where-Object { $_ -match '^codex     : Codex week' }).Count -eq 1) 'the week bar'
}

Fact 'P152_ArtRowsPutWhatWaitsForReviewFirstAndFindAThumbnail' {
    $t1 = '{"id":"b-placed","title":"B","status":"placed","askedBy":"P132","variants":[{"name":"v1","backend":"codex-app","verdict":"approved","file":"D:\\arch\\b\\v1.png"}],"approved":"v1","placed":[{"to":"D:\\console\\b.png"}]}'
    $t2 = '{"id":"a-waiting","title":"A","status":"generated","askedBy":"P152","variants":[{"name":"v1","backend":"codex","verdict":"rejected","file":"D:\\rev\\a\\v1-codex.png"},{"name":"v2","backend":"api","verdict":"","file":"D:\\rev\\a\\v2-api.png"}],"approved":""}'
    $exists = { param($p) $p -in @('D:\rev\a\v2-api.preview.png', 'D:\console\b.png') }
    $rows = @(Get-ArtRequestRows @($t1, 'not json', $t2) $exists)
    Assert-Equal 'a-waiting,b-placed,?' (($rows | ForEach-Object { $_.Id }) -join ',')
    Assert-Equal 'D:\rev\a\v2-api.preview.png' $rows[0].Thumb 'the newest variant''s preview sheet'
    Assert-Equal 'v1 codex (rejected), v2 api' $rows[0].Variants
    Assert-Equal 'D:\console\b.png' $rows[1].Thumb 'no review file: the placed one'
    Assert-Equal 'unreadable' $rows[2].Status
}

Fact 'P152_TheArtTabHasOneButtonPerCommandAndTheGolemPanelItsTooltip' {
    $f = Show-P132Form -Look 'Enhanced'
    try {
        $tabs = $null; $stack = New-Object System.Collections.Stack; $stack.Push($f)
        $byName = @{}
        while ($stack.Count) { $x = $stack.Pop(); if ($x.Name) { $byName[$x.Name] = $x }; if ($x -is [Windows.Forms.TabControl]) { $tabs = $x }; foreach ($k in $x.Controls) { $stack.Push($k) } }
        $titles = @($tabs.TabPages | ForEach-Object { $_.Text })
        Assert-True ($titles -contains 'Art') ('an Art tab: ' + ($titles -join ', '))
        Assert-Equal 'LIVE shard' $titles[-1] 'live stays last'
        foreach ($k in @('art.refresh', 'art.prompt', 'art.generate', 'art.generateApi', 'art.import', 'art.approve', 'art.reject', 'art.regenerate', 'art.place', 'art.review')) { Assert-True $byName.ContainsKey($k) ('button ' + $k) }
        Assert-True ($byName['artList'] -is [Windows.Forms.ListView]) 'the list'
        $tips = Get-ConsoleTooltips
        Assert-True ($tips['golemPanel'] -match 'ChatGPT plan') 'the Golem panel says what the codex rows are'
        foreach ($k in @('tab.Art', 'art.generate', 'art.place')) { Assert-True ($tips.Contains($k)) ('tooltip ' + $k) }
        Assert-True ($byName['golemPanel'] -is [Windows.Forms.RichTextBox]) 'the panel is named for its tooltip'
    } finally { Close-P132Form $f }
}

# ---- cc-P182: the web console's Golem buttons, and Poke, Run next and Retry stopped prompt in the console too ---------
$p182Actions = (Get-Content -LiteralPath (Join-Path $repo 'web-console\golem-actions.json') -Raw | ConvertFrom-Json).actions
function New-P182Facts {
    param([string]$StatusText = (New-P131Status -State 'IDLE' -Current ''), [string]$SettingsText = $p131Settings, [string]$RunNext = '', $Queue = @('cc-P126-a.md', 'cc-P130-b.md'), [bool]$Enabled = $true)
    $f = New-P131Facts -StatusText $StatusText -SettingsText $SettingsText -Queue $Queue -Enabled $Enabled
    $f | Add-Member -NotePropertyName RunNext -NotePropertyValue $RunNext -Force
    $ap = $p125Cfg.Autopilot
    foreach ($s in @($ap.PokeScript, $ap.RunNextScript, $ap.RetryScript, $ap.Start, $ap.Stop)) { if ($s) { $f.Present[$s] = $true } }
    $f
}
function Get-P182Entry([string]$key) { @($p182Actions | Where-Object { $_.key -eq $key })[0] }

Fact 'P182_PokeRunNextAndRetryRunTheGolemsOwnScripts' {
    $ap = $p125Cfg.Autopilot
    Assert-True ($ap.PokeScript -and $ap.RunNextScript -and $ap.RetryScript) 'the three scripts are in the Golem config'
    $p = @(Get-ActionPlan -Action 'golem.poke' -Config $p125Cfg -AutopilotFacts (New-P182Facts))
    Assert-True ((Get-P131Window $p).EndsWith('Poke-Golem.ps1')) ('poke: ' + (Get-P131Window $p))
    Assert-True (@($p | Where-Object { $_.Kind -eq 'log' -and $_.Text -eq 'golem.poke' }).Count -eq 1) 'poke is logged'
    $f = New-P182Facts; $f | Add-Member -NotePropertyName RunNextPrompt -NotePropertyValue 'p130' -Force
    $r = @(Get-ActionPlan -Action 'golem.runnext' -Config $p125Cfg -AutopilotFacts $f)
    Assert-Equal 'P130' (@($r | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase) 'Run next asks the prompt number'
    Assert-True ((Get-P131Window $r).EndsWith('Set-RunNext.ps1 -Prompt P130')) ('run next: ' + (Get-P131Window $r))
    Assert-True (@($r | Where-Object { $_.Kind -eq 'confirm' })[0].Text -match 'P130, then P126') 'the new order is shown before confirming'
    $f.RunNextPrompt = 'P125'
    $x = @(Get-ActionPlan -Action 'golem.runnext' -Config $p125Cfg -AutopilotFacts $f)
    Assert-True ($x[0].Kind -eq 'refuse' -and $x[0].Text -match 'not in the queue') ('a prompt not queued is refused: ' + $x[0].Text)
    $c = New-P182Facts -RunNext 'cc-P130-b.md'; $c | Add-Member -NotePropertyName RunNextPrompt -NotePropertyValue 'cancel' -Force
    $cp = @(Get-ActionPlan -Action 'golem.runnext' -Config $p125Cfg -AutopilotFacts $c)
    Assert-True ((Get-P131Window $cp).EndsWith('Set-RunNext.ps1 -Cancel')) ('cancel: ' + (Get-P131Window $cp))
    $st = New-P182Facts -StatusText (New-P131Status -State 'STOPPED' -Current '' -StoppedPrompt 'P120')
    $t = @(Get-ActionPlan -Action 'golem.retry' -Config $p125Cfg -AutopilotFacts $st)
    Assert-Equal 'P120' (@($t | Where-Object { $_.Kind -eq 'confirm' })[0].Phrase) 'Retry asks the stopped prompt'
    Assert-True ((Get-P131Window $t).EndsWith('Retry-Stopped.ps1 -Prompt P120')) ('retry: ' + (Get-P131Window $t))
    $none = @(Get-ActionPlan -Action 'golem.retry' -Config $p125Cfg -AutopilotFacts (New-P182Facts))
    Assert-True ($none[0].Kind -eq 'refuse' -and $none[0].Text -match 'No prompt is STOPPED') ('nothing stopped: ' + $none[0].Text)
    foreach ($pl in @($p, $r, $cp, $t)) { Assert-Equal 0 @(Test-PlanSafety $pl $p125Cfg).Count (@(Test-PlanSafety $pl $p125Cfg) -join '; ') }
    $bad = @(New-PlanStep -Kind window -Exe $p125Cfg.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ap.RunNextScript, '-Prompt', 'P1; rm x'))
    Assert-True (@(Test-PlanSafety $bad $p125Cfg).Count -gt 0) 'Run next only with a prompt number'
    $bad2 = @(New-PlanStep -Kind window -Exe $p125Cfg.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ap.RetryScript, '-Prompt', 'P120', '-NoWake'))
    Assert-True (@(Test-PlanSafety $bad2 $p125Cfg).Count -gt 0) 'Retry only with its prompt'
}

Fact 'P182_TheQueueShowsTheRunNextPickFirst' {
    $q = Get-AutopilotQueue @('cc-P101-b.md', 'cc-P140-cont-z.md', 'cc-P99-a.md') -RunNext 'cc-P101-b.md'
    Assert-Equal 'cc-P101-b.md,cc-P140-cont-z.md,cc-P99-a.md' (@($q.Runs) -join ',')
    $q2 = Get-AutopilotQueue @('cc-P101-b.md', 'cc-P99-a.md') -RunNext 'cc-P7-gone.md'
    Assert-Equal 'cc-P99-a.md,cc-P101-b.md' (@($q2.Runs) -join ',') 'a pick no longer queued is ignored'
}

Fact 'P182_EveryWebButtonIsAConsoleActionAndItsWordIsTheConsolesPhrase' {
    $keys = @(Get-ConsoleActions | ForEach-Object { $_.Key })
    foreach ($e in @($p182Actions)) {
        if ($e.disabled) { Assert-True (-not $e.action) ('a disabled button runs nothing: ' + $e.key); continue }
        Assert-True ($keys -contains $e.action) ('a console action: ' + $e.key + ' -> ' + $e.action)
        $phrase = Get-ConfirmPhrase $e.action
        if ($phrase -ne 'confirm live' -and $e.word -notlike '#*' -and $e.key -notmatch 'cancel|off$') { Assert-Equal $phrase $e.word ('the console phrase for ' + $e.key) }
    }
    foreach ($k in @('wake', 'poke', 'rest', 'wrapup', 'stoppoint', 'longrun', 'nightshift-on', 'nightshift-off', 'runnext', 'away-on', 'away-off', 'retry')) { Assert-True (@($p182Actions | Where-Object { $_.key -eq $k }).Count -eq 1) ('a button ' + $k) }
}

Fact 'P182_AWebButtonCallsTheConsolesOwnPlanAndChecksTheWordOnTheServer' {
    # A fake Get-ActionPlan records each call: every button reaches the console's own plan, with the console key.
    $script:p182Calls = New-Object 'System.Collections.Generic.List[string]'
    function Get-ActionPlan { param($Action, $Config, $AutopilotFacts, $Now) $script:p182Calls.Add($Action); @(New-PlanStep -Kind say -Text 'fake') }
    try {
        foreach ($e in @($p182Actions | Where-Object { -not $_.disabled })) {
            $f = New-P182Facts -RunNext 'cc-P130-b.md' -StatusText (New-P131Status -State 'RUNNING' -Current 'P125')
            $inputs = @{ prompt = 'P130'; model = 'Opus'; cap = '3'; until = '2026-10-20 06:00'; start = '22:30'; end = '06:30' }
            $null = Get-WebActionPlan -Entry $e -Typed 'x' -Inputs $inputs -Config $p125Cfg -Facts $f -Now $p125Now
        }
    } finally { Remove-Item function:Get-ActionPlan -ErrorAction SilentlyContinue }
    foreach ($e in @($p182Actions | Where-Object { -not $_.disabled -and -not $_.when })) { Assert-True ($script:p182Calls -contains $e.action) ('called for ' + $e.key + ': ' + ($script:p182Calls -join ',')) }
    # The word, checked here as well as in the page and server.py: case ignored, anything else refused.
    $f = New-P182Facts
    $ok = Get-WebActionPlan -Entry (Get-P182Entry 'poke') -Typed ' Poke ' -Inputs @{} -Config $p125Cfg -Facts $f -Now $p125Now
    Assert-True ($ok.Ok -and $ok.ActionKey -eq 'golem.poke') ('poke with its word: ' + $ok.Refusal)
    $no = Get-WebActionPlan -Entry (Get-P182Entry 'poke') -Typed '' -Inputs @{} -Config $p125Cfg -Facts $f -Now $p125Now
    Assert-True (-not $no.Ok -and $no.Refusal -match 'type poke') ('no word: ' + $no.Refusal)
    $w = Get-WebActionPlan -Entry (Get-P182Entry 'wrapup') -Typed 'Wrap up golem' -Inputs @{} -Config $p125Cfg -Facts (New-P182Facts -StatusText (New-P125Status)) -Now $p125Now
    Assert-True ($w.Ok -and $w.Phrase -ceq 'wrap up golem') ('the console phrase goes on to Invoke-Plan: ' + $w.Phrase + ' ' + $w.Refusal)
    $wound = Get-WebActionPlan -Entry (Get-P182Entry 'wrapup') -Typed 'wrap up golem' -Inputs @{} -Config $p125Cfg -Facts (New-P182Facts -StatusText (New-P125Status -Winding 'after-current')) -Now $p125Now
    Assert-True (-not $wound.Ok -and $wound.Refusal -match 'Cancel wrap up') ('a stale page cannot wrap up twice: ' + $wound.Refusal)
    $rn = Get-WebActionPlan -Entry (Get-P182Entry 'runnext') -Typed 'p130' -Inputs @{ prompt = 'P130' } -Config $p125Cfg -Facts (New-P182Facts) -Now $p125Now
    Assert-True ($rn.Ok -and $rn.Phrase -eq 'P130') ('Run next with the prompt number: ' + $rn.Refusal)
    $rn2 = Get-WebActionPlan -Entry (Get-P182Entry 'runnext') -Typed 'P126' -Inputs @{ prompt = 'P130' } -Config $p125Cfg -Facts (New-P182Facts) -Now $p125Now
    Assert-True (-not $rn2.Ok) 'another number is refused'
    $aw = Get-WebActionPlan -Entry (Get-P182Entry 'away-on') -Typed 'away on' -Inputs @{} -Config $p125Cfg -Facts (New-P182Facts) -Now $p125Now
    Assert-True (-not $aw.Ok -and $aw.Refusal -match 'P149') ('away mode is refused until P149: ' + $aw.Refusal)
}

Fact 'P182_WindowStepsRunInThisProcessForTheWebConsole' {
    # The web console has no window to open: the Golem script runs here and its output comes back on stdout.
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('p182-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    try {
        $s = Join-Path $tmp 'say.ps1'
        Set-Content -LiteralPath $s -Value "Write-Host 'ran inline'; exit 0" -Encoding ASCII
        $cfg = Get-ConsoleConfig -RepoRoot $repo
        $cfg.Autopilot = $p125Cfg.Autopilot
        $cfg.LogFile = (Join-Path $tmp 'shard-console.log')
        $plan = @((New-PlanStep -Kind confirm -Phrase 'poke'), (New-PlanStep -Kind window -Exe $cfg.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $s) -Text 'In a window of its own.'))
        $script:WindowStepsInline = $true
        try { $out = (& { Invoke-Plan -Plan $plan -Config $cfg -ActionKey 'golem.poke' -ShardKey 'golem' -ConfirmLive 'poke' } 6>&1 | Out-String) } finally { $script:WindowStepsInline = $false }
        Assert-True ($out -match 'ran inline') ('the script ran here: ' + $out)
        Set-Content -LiteralPath $s -Value "Write-Host 'refused'; exit 1" -Encoding ASCII
        $bad = @((New-PlanStep -Kind window -Exe $cfg.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $s)), (New-PlanStep -Kind say -Text 'after'))
        $script:WindowStepsInline = $true
        try { $out2 = (& { Invoke-Plan -Plan $bad -Config $cfg -ActionKey 'golem.poke' -ShardKey 'golem' } 6>&1 | Out-String) } finally { $script:WindowStepsInline = $false }
        Assert-True ($out2 -match 'FAILED at step 1' -and $out2 -notmatch '\] after') ('a script that exits non-zero fails the plan: ' + $out2)
    } finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}

Fact 'P182_TheGolemTabHasPokeRunNextAndRetry' {
    $script:NetProbeDisabled = $true
    $form = New-ConsoleForm -Config $p125Cfg -DryRun -StatusFrom (Join-Path $fixture 'console-capture-2026-09-29.txt')
    try {
        $tags = @($script:ui.GolemQueueButtons | ForEach-Object { $_.Tag })
        Assert-Equal 'golem.poke,golem.runnext,golem.retry' ($tags -join ',') 'their own row'
        $tips = Get-ConsoleTooltips
        foreach ($k in $tags) { Assert-True ($tips.Contains($k) -and $tips[$k]) ('a tooltip for ' + $k) }
        Assert-True (@($script:ui.GolemRunButtons).Count -eq 4) 'the run row is unchanged'
    } finally { $form.Dispose() }
}

Remove-Item -LiteralPath $p144Dir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $shimDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $p135Dir -Recurse -Force -ErrorAction SilentlyContinue

$failed =@($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
