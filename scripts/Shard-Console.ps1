#Requires -Version 5.1
<#
  Shard-Console.ps1 - one window for the things done twenty times a day while testing the shard.

      .\scripts\Shard-Console.ps1                  the console
      .\scripts\Shard-Console.ps1 -DryRun          every button prints what it would run instead of
                                                   running it, and the status panel reads a captured
                                                   file instead of docker. A button that runs in its
                                                   own window still asks and still opens the window,
                                                   which says DRY RUN at the top and only prints.
                                                   Launched without it, the "DRY RUN" box by the
                                                   Refresh button does the same for the buttons while
                                                   the status stays live from docker. Launched with
                                                   it, the box is locked on: a plan built from a
                                                   capture file must never run for real
      .\scripts\Shard-Console.ps1 -Status          print the status panel here and exit
      .\scripts\Shard-Console.ps1 -Action list     every button, by key
      .\scripts\Shard-Console.ps1 -Mode Execute -Action test.start
                                                   one button, headless. The console runs every
                                                   button that changes anything this way, in a
                                                   window of its own, so you can watch each step.
                                                   -Mode DryRun (or -DryRun) prints it instead.
                                                   With no -Mode, a button that changes anything
                                                   is refused (D37), so a lost flag fails safe
      .\scripts\Shard-Console.ps1 -Capture FILE    write a status capture from docker (read-only)
      .\scripts\Shard-Console.ps1 -WorldCommands   print the World setup tab here and exit

  AT A GLANCE. Three tiles across the top (and the window title) say what is running: LIVE, TEST,
  and the STATUS PUBLISHER (the shard's status.json for the website, docker/uo-status/README.md).
  The publisher is judged by the file's generatedAt, fresh within 3 minutes, never by a claim in
  the file. The tile also says whether sl-uo-status is serving it on 8092. A fourth tile, PUBLIC
  DNS (cc-P52, bug-list D47), goes red when a VPN carries this PC's traffic or public DNS for
  shatteredlegacyuo.com is not this house: "VPN on: public DNS points at <ip>. Turn the VPN off."
  Its check runs in a runspace of its own, so it never holds the window; a check that failed is
  grey UNKNOWN, never red. Get-DnsGlance says which signals it reads and why. A fifth, GOLEM
  (cc-P74 as AUTOPILOT, renamed by cc-P99), says what the Golem, Chase's CC queue runner, is doing: OFF,
  IDLE, RUNNING (P74 for 42m, 3 queued) or NEEDS YOU (the hook emailed him since it last handed CC a
  prompt). It reads the active folder's ENABLED, state.json and the tail of its log, and lists
  D:\UO\cc-prompts\queue, on every refresh; Get-AutopilotGlance judges them. A file it could not read is
  grey UNKNOWN, never red. The active folder (cc-P99): D:\UO\golem once Move-ToGolem.ps1 has written its
  ACTIVE file, else D:\UO\cc-autopilot, the Golem's predecessor, so the console works before and after
  the cutover (Get-GolemConfig). The Golem tab says which one it is reading.
  cc-P94: status.json (the hook's own summary, schema 1) is read first when it is there and readable:
  RUNNING says "P92  2 of 5  step 3/6  14m" over the step and the last activity, amber when CC has
  been quiet past stallMinutes or status.json is over 15 minutes old. Without it, as above.
  A sixth, PLAY TESTS (cc-P83), counts what is waiting in D:\UO\shard-migration\testing\play-tests.json
  (sl-play-tests, docker/test-queue) and turns amber while a Fail is not yet logged in the bug list. Its
  two buttons open the play-test page (8093) and the Homepage dashboard (3000); so do two on the
  Golem tab.
  A seventh, WEBSITES (cc-P101), checks every site in scripts\websites.json (one GET each, 4 s timeout, every
  60 s, in a runspace of its own): "6 OF 7 UP", amber with the down ones named. The Websites tab lists them with
  Open, Restart, Redeploy and Logs (game-PC sites), Deploy (a Haven site with an allowlisted deploy script, after
  the phrase 'deploy to haven', through scripts\Invoke-WebsiteDeploy.ps1) and Open NPM (an SSH tunnel with
  Chase's own key, then the admin page). Get-WebsitesGlance and Get-WebsitePlan judge; the site list is config. Get-PlayTestGlance judges the file.

  THE LOOK, TOOLTIPS AND THE ACTION OUTPUT PANE (cc-P84). Look: UO (stone, parchment, gold, the crest, Palatino
  Linotype) or Plain (the console as it was), chosen in the status bar and remembered in
  scripts\shard-console-settings.json (git-ignored); every color and font is in Get-ConsoleTheme. UO draws the client's
  gumps 3604 (stone) and 9304 (paper) behind the window and the tabs, read once from
  client-data\classic-client\gumpartLegacyMUL.uop into scripts\shard-console-cache\ (git-ignored: EA's art), and its
  own colors when they are missing. Every control has a tooltip from Get-ConsoleTooltips. A 'window' action, and a log
  tail, streams into the Action output pane (Copy, Clear, Stop, Show/Hide) instead of a window of its own, unless it
  asks a person something ($script:InteractiveActions: the wipe's password, the publish script's "Type yes") or
  "actions in their own windows" is ticked. Get-OutputRoute decides. Nothing a button does changes with either.

  IT CALLS THE EXISTING SCRIPTS AND DOES NOT REIMPLEMENT THEM.
    docker/uo/Start-TestShard.ps1  starts, stops and rebuilds the test shard, seeds its accounts
                                   (D19) and waits for the listener.
    docker/uo/build.sh             reached only through Start-TestShard.ps1. It is the only thing
                                   that runs the gates.
    Check-Docker.ps1, Check-Shard.ps1   the diagnostics, as they are.
    D:\UO\Commit-ShardWork.ps1     the Commit tab (cc-P60): Preview runs it with -DryRun, Commit and
                                   push runs it for real after the phrase 'commit shard work'. Both
                                   in a window of their own. Commit and push is refused when the
                                   console runs as SYSTEM, when both repos are clean, or while a CC
                                   prompt has not said DONE or a pending file changed inside the
                                   quiet window, CommitQuietSeconds (60 s, cc-P68), Get-CommitInFlight;
                                   the preview still runs and names them. The tab counts down to when
                                   the commit is allowed. Force (cc-P68) skips only the quiet window.
    player-package/Build-PlayerPackage.ps1   the Player package tab (cc-P63): Build package runs it
                                   with -Notes, then -CheckZip on the zip it wrote. Refused while
                                   the shard repo has uncommitted changes. Check selected (cc-P68)
                                   runs -CheckZip on any zip in dist\ and only reads.
    D:\UO\haven-migration\Publish-PlayerPackage.ps1   Publish package runs it with -Zip after the
                                   phrase 'publish player package'; it asks its own "Type yes".
                                   Refused when the zip's version.json does not describe the zip.
    scripts\legion\*.ps1           the Player package tab's Legion scripts row (cc-P144): Check scripts and
                                   Publish to dev / Promote to official run Publish-LegionScripts.ps1 -Mode
                                   Check, Dev or Promote (the last two after their phrases, and each asks "Type
                                   yes" itself); Set up scripts repo runs Setup-ScriptsRepo.ps1 and Set up
                                   publish key Setup-PublishKey.ps1, in windows of their own; Pull scripts is
                                   git -C <the clone> pull --ff-only. Not refused for an uncommitted shard repo:
                                   the scripts come from their own repo (CainUnAble923/shattered-legacy-scripts).
    docker tag + Start-TestShard.ps1   Test shard > Deploy built image (cc-P63): sl-modernuo:cc-p*
                                   onto sl-modernuo:latest after 'deploy to test', then -Down and
                                   -SkipBuild. The only docker tag the console makes; never live.
    Start-TestShard.ps1 + a request file   Test shard > Wipe test accounts (cc-P73): after the phrase
                                   'WIPE TEST ACCOUNTS' and a new admin name, its window asks the
                                   password (never on a command line; only a PBKDF2 hash is written),
                                   stops the test shard, backs up its save to Saves.aside-<time>, writes
                                   modernuo-test\account-wipe-request.json and starts it. The shard
                                   wipes before it listens (server/customizations/Misc/TestAccountWipe.cs)
                                   and refuses any request on live. Not Start fresh: the world stays.
    D:\UO\golem\*.ps1              the Golem tab (cc-P74 as Autopilot, cc-P99): Wake the Golem, Poke the Golem (cc-P153)
    (or D:\UO\cc-autopilot\*.ps1    (Start-Golem.ps1, refused with no email.json), Rest the Golem
     before the cutover)            (Stop-Golem.ps1), Golem email setup (Setup-Email.ps1, which asks
                                   the password in its own window), Send test email (Send-TestEmail.ps1)
                                   and Move to the Golem (Move-ToGolem.ps1, after the typed phrase 'move
                                   to the golem'; it prints its dry run and asks again), each in a
                                   PowerShell window of its own that stays open. Golem log and (cc-P94)
                                   Golem settings open in Notepad; Open Golem folder opens Explorer. The
                                   hooks, golem.ps1 and cc-autopilot.ps1, which commit and push, are
                                   never run from here (Test-PlanSafety). autopilot.* keys still work
                                   as aliases of golem.*.

  WHAT NO BUTTON DOES
    - docker compose on docker/uo/docker-compose.yml. That file has build: and no image:
      (bug-list D29), so compose up there can build an image with no test gate, and the project
      also owns sl-ddns, so compose down would stop public DNS. The live buttons are plain
      docker start / stop / restart on the existing sl-modernuo container.
    - delete a world save. A restore and "start fresh" move the save aside. Deleting one is
      something a person does deliberately in Explorer.
    - touch the live shard without a typed confirmation.
  Test-PlanSafety checks every plan for these before anything runs, dry or not.

  ASCII only, saved as UTF-8 with no BOM: PowerShell 5.1 reads a BOM-less file as Windows-1252.
  Facts: scripts/tests/Shard-Console.Tests.ps1. Notes: shard-migration/notes/shard-console.md.
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    # cc-P101: a Websites action's site, by its key in scripts\websites.json.
    [string]$Site,
    [ValidateSet('DryRun', 'Execute')][string]$Mode,
    [string]$StatusFrom,
    [switch]$StatusFromDocker,
    [string]$Action,
    [ValidateSet('test', 'live')][string]$Shard = 'test',
    [string]$SnapshotName,
    [string]$Snapshot,
    [string]$ConfirmLive,
    [switch]$Yes,
    [switch]$Status,
    [string]$Capture,
    [switch]$LoadOnly,
    [switch]$WorldCommands,
    [string]$RepoRoot,
    [string]$CommitMessage,
    [string]$CommitMessageB64,
    [switch]$CommitForce,
    [string]$PackageNotes,
    [string]$PackageNotesB64,
    [string]$PackageZip,
    [string]$ImageTag,
    [string]$WipeAdmin,
    # cc-P96: Fix test shard > Rebuild's commit (a hash from its list, or HEAD).
    [string]$Commit,
    # cc-P135: Map and Art. The .vd file and body for art.checkvd, the other hues.mul for art.hues.
    [string]$VdFile,
    [string]$Body,
    [string]$HuesFile
)

# cc-P124: which TazUO the player package ships (the one default, and reading and checking its sl-build.txt), shared
# with player-package\Build-PlayerPackage.ps1. Functions only; it reads nothing when loaded.
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'player-package\PackageClient.ps1')

# =============================================================================================
# CORE. Strings in, objects or strings out. No docker, no files, no WinForms below this line
# until the SHELL marker, so every function here can be given captured output and checked.
# =============================================================================================

$script:Invariant       = [Globalization.CultureInfo]::InvariantCulture
$script:CaptureMarker   = '##### SHARD-CONSOLE-CAPTURE '
$script:SnapshotPattern = '^(\d{8}-\d{6})_([A-Za-z0-9._-]+)$'
# cc-P106: what answering y or a to D94's question deletes (ClusterFAccountData.cs, one object for every account).
$script:D94Loss = "every account's ClusterF data: renown and achievement points, learned recipes, guild membership, ranks, reputation and work orders, ore, wood and imbuing discoveries, the tutorial record, and exploration. It is one object (ClusterFAccountPersistence) holding all of it for every account."
$script:NoSaveWarning   = 'A stop does not save: play since the last autosave (every 5 minutes) is lost. [save in game first if that matters.'
# D36 fixed: said instead for a container whose log since this start has $script:SavesOnStopLine.
$script:SavesOnStopNote = 'The shard saves on stop (D36 fix, seen in its log since this start). If the stop times out, play since the last autosave is lost.'
# Printed by server/customizations/Misc/SaveOnShutdown.cs (RegisteredLine) once it listens for SIGTERM. Change both together.
$script:SavesOnStopLine = '[SaveOnShutdown] listening for SIGTERM: a stop saves the world first (D36)'
# cc-P60: said above the phrase before a commit, the way D36's warning is said before a stop.
$script:CommitPublicWarning = 'Both repos are PUBLIC on GitHub: a commit that is pushed cannot be taken back.'
# cc-P63: said above the phrase before a publish. Publish-PlayerPackage.ps1 serves version.json, which every launcher reads.
$script:PublishPublicWarning = 'This is PUBLIC: every player''s launcher is offered this package at its next start.'
# cc-P96: the test shard's health and crash judgments, shared with docker/uo/Start-TestShard.ps1. Pure functions.
. (Join-Path $PSScriptRoot 'Shard-Health.ps1')
# cc-P108: scripts\live.json, the one switch for the live shard (Get-LiveSwitch). Pure apart from reading that file.
. (Join-Path $PSScriptRoot 'Live-Switch.ps1')

function New-AutopilotConfig {
    # cc-P74. The queue runner's files, by its folder and its queue, so the facts can point both at fixtures.
    # cc-P99: -Kind golem is D:\UO\golem's names (golem.log, Start-Golem.ps1, golem.ps1); autopilot is the old
    # folder's. -GolemRoot is where Move-ToGolem.ps1 lives whichever one is active; -Why says why this one.
    param([string]$Root, [string]$Queue, [ValidateSet('autopilot', 'golem')][string]$Kind = 'autopilot', [string]$GolemRoot, [string]$AutopilotRoot, [string]$Why)
    $g = ($Kind -eq 'golem')
    if (-not $GolemRoot) { $GolemRoot = $(if ($g) { $Root } else { 'D:\UO\golem' }) }
    if (-not $AutopilotRoot) { $AutopilotRoot = $(if ($g) { 'D:\UO\cc-autopilot' } else { $Root }) }
    [pscustomobject]@{
        Kind      = $Kind
        Name      = $(if ($g) { 'the Golem' } else { 'cc-autopilot' })
        Why       = $Why
        Root      = $Root
        GolemRoot = $GolemRoot
        AutopilotRoot = $AutopilotRoot
        MoveScript = (Join-Path $GolemRoot 'Move-ToGolem.ps1')
        GolemActiveFile = (Join-Path $GolemRoot 'ACTIVE')
        Hooks     = @((Join-Path $GolemRoot 'golem.ps1'), (Join-Path $AutopilotRoot 'cc-autopilot.ps1'))
        WatchTask = $(if ($g) { 'Golem Watch' } else { 'Shard CC Autopilot Watch' })
        Enabled   = (Join-Path $Root 'ENABLED')
        StateFile = (Join-Path $Root 'state.json')
        LogName   = $(if ($g) { 'golem.log' } else { 'autopilot.log' })
        LogFile   = (Join-Path $Root $(if ($g) { 'golem.log' } else { 'autopilot.log' }))
        EmailFile = (Join-Path $Root 'email.json')
        CredFile  = (Join-Path $Root 'smtp-cred.xml')
        Start     = (Join-Path $Root $(if ($g) { 'Start-Golem.ps1' } else { 'Start-Autopilot.ps1' }))
        Stop      = (Join-Path $Root $(if ($g) { 'Stop-Golem.ps1' } else { 'Stop-Autopilot.ps1' }))
        Setup     = (Join-Path $Root 'Setup-Email.ps1')
        TestEmail = (Join-Path $Root 'Send-TestEmail.ps1')
        # cc-P94: the hook's own summary (status.json, schema 1) and its three settings.
        StatusFile   = (Join-Path $Root 'status.json')
        SettingsFile = (Join-Path $Root 'settings.json')
        Queue     = $Queue
        # cc-P125: the run buttons' scripts (the Golem's only), the plan usage the status line writes, and the prompts.
        LongRunStart = (Join-Path $Root 'Start-LongRun.ps1')
        LongRunEnd   = (Join-Path $Root 'End-LongRun.ps1')
        WrapUp       = (Join-Path $Root 'Wrap-Up.ps1')
        StopPoint    = (Join-Path $Root 'Find-StoppingPoint.ps1')
        UsageFile    = (Join-Path $Root 'usage.json')
        # cc-P160: claude.ai/settings/usage as the overseer's reader writes it (the Fable week, the pace warning).
        PlanUsageFile = (Join-Path $Root 'plan-usage.json')
        # cc-P152: the ChatGPT plan's Codex limits, written by Codex.ps1 after every run (and by its usage action).
        CodexUsageFile = (Join-Path $Root 'codex-usage.json')
        # cc-P131: waking a CC session from here, the two toggles, the sign-in check, and the CLI facts the Golem keeps.
        WakeScript   = (Join-Path $Root 'Wake-Golem.ps1')
        AutoWakeScript = (Join-Path $Root 'Set-AutoWake.ps1')
        NightShiftScript = (Join-Path $Root 'Set-NightShift.ps1')
        # cc-P200: marathon mode's setting (auto, on, off).
        MarathonScript = (Join-Path $Root 'Set-Marathon.ps1')
        CliCheckScript = (Join-Path $Root 'Check-Cli.ps1')
        CliFile      = (Join-Path $Root 'cli.json')
        # cc-P182: Poke (the watch now), Run next (RUN-NEXT holds the picked queue file) and Retry stopped prompt.
        PokeScript    = (Join-Path $Root 'Poke-Golem.ps1')
        RunNextScript = (Join-Path $Root 'Set-RunNext.ps1')
        RetryScript   = (Join-Path $Root 'Retry-Stopped.ps1')
        AnswerScript  = (Join-Path $Root 'Answer-Question.ps1')   # cc-P216: Chase's answer to a Golem's question
        LaneNowScript = (Join-Path $Root 'Lane-Now.ps1')   # cc-P159: Send to the second PC (a Codex lane may start now)
        RunNextFile   = (Join-Path $Root 'RUN-NEXT')
        IntegrateScript = (Join-Path $Root 'Integrate.ps1')   # cc-P184: the integrator merges the other Golems' branches now
        MergeScript   = (Join-Path $Root 'ShadowIron.ps1')   # cc-P208: the merge Golem's runner (-Pause, -Resume)
        TinkerScript  = (Join-Path $Root 'Tinker.ps1')       # cc-P222: the watchdog's runner (a round now, -Pause, -Resume)
        AwayScript    = (Join-Path $Root 'Set-Away.ps1')     # cc-P149: away mode (-Until <when>, -Off)
        SkipDayScript = (Join-Path $Root 'Skip-Day.ps1')     # cc-P287: Owner works <date> (-Golem <name> -Date <yyyy-MM-dd> -By console)
        AssignScript  = (Join-Path $Root 'Assign-Prompt.ps1') # cc-P187: a queued prompt's **Golem:** line (and Run next for it)
        PromptDir    = $(if ($Queue) { Split-Path -Parent $Queue } else { '' })
        CompletedDir = $(if ($Queue) { Join-Path (Split-Path -Parent $Queue) 'completed' } else { '' })
        ClaudeProjects = $(if ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.claude\projects' } else { '' })
        Notepad   = (Join-Path $env:WINDIR 'notepad.exe')
        # The log is tailed, never read whole: Commit-ShardWork.ps1's output is pasted into it after every commit.
        TailLines = 400
    }
}

function Select-GolemKind {
    # cc-P99. Pure: which folder the console reads and whose scripts its buttons run. The Golem once the cutover
    # (Move-ToGolem.ps1) has written D:\UO\golem\ACTIVE and its Start-Golem.ps1 is there; cc-autopilot before
    # that, or after -Rollback (which removes ACTIVE).
    param([bool]$GolemStartPresent, [bool]$GolemActive)
    if ($GolemStartPresent -and $GolemActive) { return [pscustomobject]@{ Kind = 'golem'; Why = 'the Golem is active (ACTIVE is there)' } }
    if ($GolemStartPresent) { return [pscustomobject]@{ Kind = 'autopilot'; Why = 'the Golem is built but not moved to yet (no ACTIVE): Move to the Golem' } }
    [pscustomobject]@{ Kind = 'autopilot'; Why = 'there is no Golem yet (no Start-Golem.ps1)' }
}

function Get-GolemConfig {
    # cc-P99. The shell side of Select-GolemKind: two Test-Paths, so it is re-read on every refresh and a
    # cutover made while the console is open shows at the next one.
    param([string]$GolemRoot, [string]$AutopilotRoot, [string]$Queue)
    $k = Select-GolemKind -GolemStartPresent ([bool](Test-Path -LiteralPath (Join-Path $GolemRoot 'Start-Golem.ps1') -PathType Leaf)) `
        -GolemActive ([bool](Test-Path -LiteralPath (Join-Path $GolemRoot 'ACTIVE') -PathType Leaf))
    $root = $(if ($k.Kind -eq 'golem') { $GolemRoot } else { $AutopilotRoot })
    New-AutopilotConfig -Root $root -Queue $Queue -Kind $k.Kind -GolemRoot $GolemRoot -AutopilotRoot $AutopilotRoot -Why $k.Why
}

function Resolve-ActionAlias {
    # cc-P99. The Golem's buttons were the autopilot's: autopilot.on is golem.on, and so on, so old shortcuts,
    # scripts and tests still resolve. Every other key is returned as it is.
    param([string]$Action)
    if ($Action -like 'autopilot.*') { return ('golem.' + $Action.Substring(10)) }
    $Action
}

function Test-LiveRetired {
    # cc-P108: true while scripts\live.json says liveEnabled false (Get-ConsoleConfig puts it on the live shard).
    param($Config)
    [bool]($Config -and $Config.Shards -and $Config.Shards.Contains('live') -and $Config.Shards['live'].Retired)
}

function Test-LiveActionKey {
    # cc-P108: an action that acts on live: live.*, or a snapshot or Shard question action with the LIVE shard picked.
    param([string]$Action, [string]$ShardKey)
    ($Action -like 'live.*') -or ($ShardKey -eq 'live' -and ($Action -like 'snapshot.*' -or $Action -like 'question.*'))
}

function Get-LegionRepoSetting {
    # cc-P144: the scripts clone named in this PC's legion-scripts.json (SL_LEGION_SETTINGS moves it), when it is a git
    # clone; else ''. The file is player-package\payload\app\LegionScripts.ps1's (Read-LegionSettings); only repo is read.
    $p = $env:SL_LEGION_SETTINGS
    if (-not $p -and $env:LOCALAPPDATA) { $p = Join-Path $env:LOCALAPPDATA 'ShatteredLegacy\legion-scripts.json' }
    if (-not $p -or -not (Test-Path -LiteralPath $p -PathType Leaf)) { return '' }
    try {
        $r = [string]([IO.File]::ReadAllText($p) | ConvertFrom-Json).repo
        if ($r -and (Test-Path -LiteralPath (Join-Path $r '.git'))) { return $r }
    } catch {}
    ''
}

function Get-ConsoleConfig {
    # cc-P108: -LiveEnabled overrides scripts\live.json (the facts pin both states with it); unset, the file decides.
    param([Parameter(Mandatory = $true)][string]$RepoRoot, [Nullable[bool]]$LiveEnabled = $null)
    $liveSwitch = Get-LiveSwitch -RepoRoot $RepoRoot
    if ($null -ne $LiveEnabled) { $liveSwitch.Enabled = [bool]$LiveEnabled; $liveSwitch.Problem = $null }
    # cc-P96: the one file this reads. Missing or broken, ConvertFrom-FixConfig gives the defaults.
    $fixFile = Join-Path $RepoRoot 'scripts\fix-test-shard.json'
    $fixJson = $null
    if (Test-Path -LiteralPath $fixFile) { $fixJson = [IO.File]::ReadAllText($fixFile) }
    # cc-P101: and the Websites tab's site list. Missing or broken, the tile says so and nothing is guessed.
    $webFile = Join-Path $RepoRoot 'scripts\websites.json'
    $webJson = $null
    if (Test-Path -LiteralPath $webFile) { $webJson = [IO.File]::ReadAllText($webFile) }
    # cc-P144: this PC's Legion scripts clone, if Set up scripts repo made one.
    $legionRepo = Get-LegionRepoSetting
    $lib = Join-Path $RepoRoot 'server\lib\uo\modernuo'
    $shards = [ordered]@{
        test = [pscustomobject]@{
            Key = 'test'; Label = 'TEST'; Container = 'sl-modernuo-test'; IsLive = $false; Retired = $false
            # cc-P37: the test world has its own folder, mounted whole (docker-compose.test.yml).
            Saves = (Join-Path (Join-Path $RepoRoot 'server\lib\uo\modernuo-test') 'Saves'); GamePort = 2594
            Client = 'D:\UO\Play-SL-Admin-TEST.bat'
        }
        live = [pscustomobject]@{
            # cc-P108: Retired while scripts\live.json says liveEnabled false. It stays in Shards so the safety checks
            # still name its container and save; nothing reads, probes, mails or acts on a retired shard.
            Key = 'live'; Label = 'LIVE'; Container = 'sl-modernuo'; IsLive = $true; Retired = (-not $liveSwitch.Enabled)
            Saves = (Join-Path $lib 'Saves'); GamePort = 2593
            Client = 'D:\UO\Play-SL-Admin.bat'
        }
    }
    # cc-P107: where the ops pages live (Haven or this PC), and which one is serving now (its state file).
    $opsFile = Join-Path $RepoRoot 'scripts\haven-ops.json'
    $opsJson = $null
    if (Test-Path -LiteralPath $opsFile) { $opsJson = [IO.File]::ReadAllText($opsFile) }
    $c = [pscustomobject]@{
        RepoRoot         = $RepoRoot
        # cc-P108: the live switch (Enabled, RetiredOn, Returns, Line, Refusal, Problem) and its shorthand.
        Live             = $liveSwitch
        LiveEnabled      = [bool]$liveSwitch.Enabled
        LatestTag        = 'sl-modernuo:latest'
        PowerShell       = 'powershell.exe'
        StartTestShard   = (Join-Path $RepoRoot 'docker\uo\Start-TestShard.ps1')
        LiveCompose      = (Join-Path $RepoRoot 'docker\uo\docker-compose.yml')
        CheckDocker      = (Join-Path $RepoRoot 'Check-Docker.ps1')
        CheckShard       = (Join-Path $RepoRoot 'Check-Shard.ps1')
        Customizations   = (Join-Path $RepoRoot 'server\customizations')
        DiagFolder       = $RepoRoot
        SnapshotRoot     = (Join-Path $lib 'Snapshots')
        LogFile          = (Join-Path $RepoRoot 'scripts\shard-console.log')
        # cc-P113: the window icon, the same file the desktop shortcut "Shard Console" shows (the website's favicon),
        # from the repo, not copied. Loaded once by Get-ConsoleIcon; missing or unreadable keeps the default icon.
        WindowIconFile   = (Join-Path $RepoRoot 'docker\website\html\favicon.ico')
        DefaultCapture   = (Join-Path $RepoRoot 'scripts\tests\fixtures\console-capture-2026-09-29.txt')
        StopTimeout      = 60
        LogWindowMinutes = 15
        # cc-P106: a running shard silent this long after a console question's lead-in is waiting on it; the tail read
        # for it (docker logs -t); and the questions already emailed, outside both repos.
        QuestionQuietSeconds = 20
        QuestionTailLines = 80
        QuestionMailFile = (Join-Path $env:LOCALAPPDATA 'ShardConsole\question-mail.json')
        StaleSaveMinutes = 15
        Shards           = $shards
        # The shard status publisher (docker/uo-status/README.md). The shard writes status.json
        # every 30 s into this folder; sl-uo-status serves it on 8092 for the website. The file
        # has no "online" field on purpose: older than StatusStaleMinutes means the shard is not
        # publishing, which is the same rule the website uses.
        Publisher        = [pscustomobject]@{
            Folder         = (Join-Path $lib 'status')
            File           = (Join-Path (Join-Path $lib 'status') 'status.json')
            MountTarget    = '/var/lib/uo/modernuo/status'
            FeedContainer  = 'sl-uo-status'
            FeedUrl        = 'http://localhost:8092/status.json'
            StaleMinutes   = 3
            StartFeed      = (Join-Path $RepoRoot 'docker\uo-status\Start-UoStatus.ps1')
        }
        # cc-P60, the Commit tab. The repos are the two Commit-ShardWork.ps1 commits ($Repos there,
        # not this RepoRoot); change both together. A note numbered NotesDoneFrom or above must end
        # "P<nn> DONE" or "P<nn> STOPPED: ..."; the notes before P54 predate that rule.
        CommitScript       = 'D:\UO\Commit-ShardWork.ps1'
        CommitRepos        = @(
            [pscustomobject]@{ Name = 'code'; Path = 'D:\ShatteredLegacy' },
            [pscustomobject]@{ Name = 'docs'; Path = 'D:\UO\shard-migration' }
        ) + @($(if ($legionRepo) { [pscustomobject]@{ Name = 'scripts'; Path = $legionRepo } }))
        # cc-P144, the Legion scripts row on the Player package tab. LegionRepo is this PC's scripts clone, from its
        # legion-scripts.json (Get-LegionRepoSetting; '' on a PC without one); Commit-ShardWork.ps1 reads the same file
        # and commits it as a third repo, so CommitRepos lists it too.
        LegionPublish      = (Join-Path $RepoRoot 'scripts\legion\Publish-LegionScripts.ps1')
        LegionSetup        = (Join-Path $RepoRoot 'scripts\legion\Setup-ScriptsRepo.ps1')
        LegionKey          = (Join-Path $RepoRoot 'scripts\legion\Setup-PublishKey.ps1')
        LegionRepo         = $legionRepo
        NotesFolder        = 'D:\UO\shard-migration\notes'
        NotesDoneFrom      = 54
        # cc-P68: a pending file written less than this many seconds ago blocks Commit and push (was 5 minutes).
        CommitQuietSeconds = 60
        # How often the Commit tab's countdown re-reads both repos while that tab is showing.
        CommitPollSeconds  = 5
        # cc-P63, the Player package tab and Test shard > Deploy built image. The publish script is
        # outside the repo and overseer-owned; the console runs it as it is.
        BuildPackage       = (Join-Path $RepoRoot 'player-package\Build-PlayerPackage.ps1')
        PackageDist        = (Join-Path $RepoRoot 'player-package\dist')
        # cc-P124: the client folder Build package ships, from PackageClient.ps1 (Build-PlayerPackage.ps1's own default).
        PackageClient      = (Get-PackageClientDefault)
        PublishScript      = 'D:\UO\haven-migration\Publish-PlayerPackage.ps1'
        # The images Deploy built image offers: build.sh -t sl-modernuo:cc-pNN, one per prompt.
        DeployTagPattern   = '^sl-modernuo:cc-p[A-Za-z0-9._-]*$'
        # cc-P73, Test shard > Wipe test accounts. The request goes in the TEST world folder, the one
        # docker-compose.test.yml mounts as /var/lib/uo/modernuo; TestAccountWipe.cs reads it there
        # (RequestFileName). Test-PlanSafety allows no other path.
        WipeRequestFile    = (Join-Path (Join-Path $RepoRoot 'server\lib\uo\modernuo-test') 'account-wipe-request.json')
        WipeWaitSeconds    = 300
        # cc-P74, the GOLEM tile and the Golem tab (AUTOPILOT until cc-P99). The Golem lives outside both
        # repos (D:\UO\golem\README.md); the console runs its scripts as they are and reads its files.
        # golem.ps1 (and cc-autopilot.ps1 before it) is the hook and is never run from here: it commits and pushes.
        # Get-GolemConfig picks D:\UO\golem once it is active, else D:\UO\cc-autopilot; re-read on every refresh.
        Autopilot          = (Get-GolemConfig 'D:\UO\golem' 'D:\UO\cc-autopilot' 'D:\UO\cc-prompts\queue')
        # cc-P83, the PLAY TESTS tile and its two links. The data is written by sl-play-tests
        # (docker/test-queue) and by shard-migration\tools\Import-PlayTests.ps1; the console only reads it.
        PlayTestsFile      = 'D:\UO\shard-migration\testing\play-tests.json'
        PlayTestsUrl       = 'http://localhost:8093/'
        # cc-P110: Haven's Homepage (H5). Haven's :3000 answers before the Mega dashboard deploy (its stale copy) too.
        DashboardUrl       = 'http://192.168.1.61:3000/'
        # cc-P85: the shard status dashboard, which sl-play-tests serves at /status.
        StatusUrl          = 'http://localhost:8093/status'
        # cc-P101, the WEBSITES tile and the Websites tab: the sites, the deploy allowlist, Haven's SSH target and NPM's
        # tunnel, all from scripts\websites.json; Deploy runs scripts\Invoke-WebsiteDeploy.ps1, which checks it again.
        WebsitesFile       = $webFile
        Websites           = (ConvertFrom-WebsiteConfig $webJson $RepoRoot)
        WebsiteDeployScript = (Join-Path $RepoRoot 'scripts\Invoke-WebsiteDeploy.ps1')
        # cc-P84: the look and where actions show their output, remembered on this PC (git-ignored); the gump cache
        # (git-ignored: it holds EA's art); the client file the gumps are read from (read only); and our own crest.
        SettingsFile       = (Join-Path $RepoRoot 'scripts\shard-console-settings.json')
        CacheFolder        = (Join-Path $RepoRoot 'scripts\shard-console-cache')
        GumpFile           = (Join-Path $RepoRoot 'client-data\classic-client\gumpartLegacyMUL.uop')
        CrestFile          = (Join-Path $RepoRoot 'branding\intro\crest-128.png')
        # cc-P132: the art slots (Get-ArtSlots), the per-user window state (outside the repo), the lights' JSON (in the
        # git-ignored cache folder), and the published package's version.json.
        ArtFolder          = (Join-Path $RepoRoot 'scripts\console-art')
        # cc-P152: the art request pipeline (headless; the Art tab only calls it) and its store outside the repos.
        ArtScript          = (Join-Path $RepoRoot 'scripts\art\Art.ps1')
        ArtStore           = 'D:\UO\art'
        ArtInbox           = 'D:\UO\art-inbox'
        WindowStateFile    = $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'ShatteredLegacy\shard-console-window.json' } else { Join-Path $RepoRoot 'scripts\shard-console-cache\window.json' })
        LightsFile         = (Join-Path $RepoRoot 'scripts\shard-console-cache\lights.json')
        # cc-P155: the status file both consoles read (Write-ConsoleStatusFile), git-ignored with the cache.
        StatusFile         = (Join-Path $RepoRoot 'scripts\shard-console-cache\console-status.json')
        PublishedVersionUrl = 'https://get.shatteredlegacyuo.com/version.json'
        # cc-P96, Fix test shard: known crash signatures and the prune keep list (scripts/fix-test-shard.json), where
        # Start-TestShard.ps1 builds a chosen commit, and the record of what last wrote the test save.
        Fix                = (ConvertFrom-FixConfig $fixJson)
        FixConfigFile      = $fixFile
        FixWorktree        = 'D:\UO\test-shard-src'
        SaveRecordFile     = (Join-Path (Join-Path $RepoRoot 'server\lib\uo\modernuo-test') 'shard-commit.json')
        # cc-P107, Haven H2 and H3: the ops pages' two homes (scripts\haven-ops.json) and the mode file.
        OpsFile            = $opsFile
        Ops                = (ConvertFrom-OpsConfig $opsJson)
        # cc-P135, the Map and Art tab: validate-art.py (cc-P134; read only), the python it runs with, our own hues.mul
        # the diff compares against, and the subcommands the console may run (never render, the one that writes PNGs).
        ValidateArt        = (Join-Path $RepoRoot 'scripts\validate-art.py')
        Python             = 'python'
        OurHues            = (Join-Path $RepoRoot 'client-data\classic-client-7.0.117.0\hues.mul')
        ArtSubcommands     = @('bodies', 'check-registry', 'check-vd', 'propose', 'hues-diff')
    }
    # After the cutover the Play tests and Shard status sites are Haven's: their lights check Haven, and Restart and
    # Redeploy (game-PC only) no longer apply to them. The game PC URLs come back with the rollback.
    Set-OpsSites -Websites $c.Websites -Mode (Get-OpsMode $c)
    $c
}

function ConvertFrom-OpsConfig {
    # cc-P107: scripts\haven-ops.json in, the two homes' URLs and the state and token file paths out. Missing or broken:
    # the game PC only, as before P107.
    param($Json)
    $o = [pscustomobject]@{
        HavenPageUrl = ''; HavenStatusUrl = ''; OpsApi = ''; HavenSsh = ''
        GamePcPageUrl = 'http://localhost:8093/'; GamePcStatusUrl = 'http://localhost:8093/status'
        StateFile = ''; TokenFile = ''; Error = $null
    }
    if (-not $Json) { return $o }
    try { $j = $Json | ConvertFrom-Json } catch { $o.Error = 'haven-ops.json is not JSON: ' + $_.Exception.Message; return $o }
    if ($j.haven) {
        $o.HavenPageUrl = [string]$j.haven.pageUrl; $o.HavenStatusUrl = [string]$j.haven.statusUrl
        $o.OpsApi = [string]$j.haven.opsApi; $o.HavenSsh = [string]$j.haven.ssh
    }
    if ($j.gamePc -and $j.gamePc.pageUrl) { $o.GamePcPageUrl = [string]$j.gamePc.pageUrl; $o.GamePcStatusUrl = [string]$j.gamePc.statusUrl }
    $o.StateFile = [string]$j.stateFile; $o.TokenFile = [string]$j.tokenFile
    $o
}

function Get-OpsMode {
    # cc-P107: 'haven' once the cutover wrote it to the state file, else 'local' (also with no file, or a broken one).
    param($Config, [string]$StateText)
    # A [string] parameter turns $null into '', so "was it given" is the question, not "is it null".
    if (-not $PSBoundParameters.ContainsKey('StateText') -and $Config.Ops -and $Config.Ops.StateFile) {
        try { if (Test-Path -LiteralPath $Config.Ops.StateFile) { $StateText = [IO.File]::ReadAllText($Config.Ops.StateFile) } } catch { $StateText = $null }
    }
    try { if ($StateText -and ([string]($StateText | ConvertFrom-Json).mode) -ceq 'haven') { return 'haven' } } catch { }
    'local'
}

function Get-OpsLinks {
    # cc-P107: the Play tests and Shard status addresses for a mode, with the game PC's as the fallback.
    param($Config, [string]$Mode)
    $o = $Config.Ops
    if ($Mode -ceq 'haven' -and $o.HavenPageUrl) {
        return [pscustomobject]@{ PlayTests = $o.HavenPageUrl; Status = $o.HavenStatusUrl; Where = 'Haven'; Fallback = $o.GamePcPageUrl }
    }
    [pscustomobject]@{ PlayTests = $Config.PlayTestsUrl; Status = $Config.StatusUrl; Where = 'this PC'; Fallback = '' }
}

function Set-OpsSites {
    # cc-P107: in Haven mode a site with a havenUrl (websites.json) is Haven's. Changes the parsed sites in place.
    param($Websites, [string]$Mode)
    if ($Mode -cne 'haven' -or -not $Websites) { return }
    foreach ($site in @($Websites.Sites)) {
        if ($site.HavenUrl) { $site.Url = $site.HavenUrl; $site.Check = $site.HavenUrl; $site.Where = 'haven' }
    }
}

function Remove-Ansi {
    param([string]$Text)
    if ($null -eq $Text) { return '' }
    ($Text -replace "\x1b\[[0-9;?]*[A-Za-z]", '') -replace "`r", ''
}

function ConvertFrom-DockerTime {
    # Docker writes nine fractional digits and .NET parses at most seven. Year 1 means never.
    param([string]$Text)
    if (-not $Text -or $Text.StartsWith('0001-')) { return $null }
    if ($Text -notmatch '^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(\.\d+)?(Z|[+-]\d\d:\d\d)$') { return $null }
    $frac = ''
    if ($Matches[2]) { $frac = $Matches[2]; if ($frac.Length -gt 8) { $frac = $frac.Substring(0, 8) } }
    $zone = $Matches[3]
    if ($zone -eq 'Z') { $zone = '+00:00' }
    [DateTimeOffset]::Parse($Matches[1] + $frac + $zone, $script:Invariant).UtcDateTime
}

function Format-DockerTime {
    param([datetime]$Utc)
    $Utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", $script:Invariant)
}

function ConvertFrom-DockerJson {
    # docker inspect prints a JSON array on stdout and "Error: No such ..." on stderr. Captures
    # hold both, so lines that begin with Error are the error and the rest is the array.
    param([string]$Text)
    $lines = @((Remove-Ansi $Text) -split "`n")
    $err   = (@($lines | Where-Object { $_ -match '^\s*error' }) -join ' ').Trim()
    $body  = (@($lines | Where-Object { $_ -notmatch '^\s*error' }) -join "`n").Trim()
    $items = @()
    if ($body) {
        try { $items = @($body | ConvertFrom-Json | ForEach-Object { $_ }) }
        catch { if (-not $err) { $err = 'unreadable docker output: ' + $_.Exception.Message } }
    } elseif (-not $err) {
        $err = 'no output'
    }
    [pscustomobject]@{ Items = $items; Error = $err }
}

function ConvertFrom-ContainerInspect {
    param([string]$Text)
    $r = ConvertFrom-DockerJson $Text
    $rec = [ordered]@{
        Exists = $false; Error = $r.Error; Name = $null; ImageId = $null; ConfigImage = $null
        Status = 'missing'; Running = $false; StartedAtRaw = $null; StartedAt = $null
        FinishedAt = $null; ExitCode = $null; GamePort = $null; ComposeProject = $null
        MountTargets = @(); Mounts = @(); Health = ''
    }
    if ($r.Items.Count -gt 0) {
        $c = $r.Items[0]
        $port = $null
        if ($c.HostConfig -and $c.HostConfig.PortBindings) {
            foreach ($p in $c.HostConfig.PortBindings.PSObject.Properties) {
                if ($p.Name -match '^(\d+)/tcp$') { $port = [int]$Matches[1]; break }
            }
        }
        $project = $null
        if ($c.Config.Labels) { $project = $c.Config.Labels.'com.docker.compose.project' }
        $rec.Exists = $true; $rec.Error = $null
        $rec.Name = $c.Name.TrimStart('/')
        $rec.ImageId = $c.Image
        $rec.ConfigImage = $c.Config.Image
        $rec.Status = $c.State.Status
        $rec.Running = [bool]$c.State.Running
        $rec.StartedAtRaw = $c.State.StartedAt
        $rec.StartedAt = ConvertFrom-DockerTime $c.State.StartedAt
        $rec.FinishedAt = ConvertFrom-DockerTime $c.State.FinishedAt
        $rec.ExitCode = $c.State.ExitCode
        $rec.GamePort = $port
        $rec.ComposeProject = $project
        # cc-P96: docker's own health check (docker-compose.test.yml); empty for a container made without one.
        if ($c.State.Health) { $rec.Health = [string]$c.State.Health.Status }
        if ($c.Mounts) {
            $rec.MountTargets = @($c.Mounts | ForEach-Object { [string]$_.Destination } | Where-Object { $_ })
            # cc-P42 Part E: the host side too, so a parent mount can be traced to the folder it serves.
            $rec.Mounts = @($c.Mounts | Where-Object { $_.Destination } | ForEach-Object {
                [pscustomobject]@{ Source = [string]$_.Source; Destination = [string]$_.Destination } })
        }
    }
    [pscustomobject]$rec
}

function ConvertFrom-ImageInspect {
    param([string]$Text)
    $r = ConvertFrom-DockerJson $Text
    if ($r.Items.Count -eq 0) {
        return [pscustomobject]@{ Exists = $false; Id = $null; RepoTags = @(); Created = $null; Error = $r.Error; Commit = $null; Dirty = $false }
    }
    $i = $r.Items[0]
    # cc-P96: build.sh labels the image with the commit it was built from (sl.commit) and whether the tree had changes.
    $commit = $null; $dirty = $false
    if ($i.Config -and $i.Config.Labels) {
        $lc = [string]$i.Config.Labels.'sl.commit'
        if ($lc -match '^[0-9a-f]{40}$') { $commit = $lc }
        $dirty = ([string]$i.Config.Labels.'sl.dirty' -eq '1')
    }
    [pscustomobject]@{
        Exists = $true; Id = $i.Id; RepoTags = @($i.RepoTags)
        Created = (ConvertFrom-DockerTime $i.Created); Error = $null; Commit = $commit; Dirty = $dirty
    }
}

function ConvertFrom-ImageTagListing {
    # cc-P63. `docker images sl-modernuo --format {{.Repository}}:{{.Tag}}|{{.ID}}|{{.CreatedAt}}`, one
    # image per line: sl-modernuo:cc-p62|b04ed61adb5a|2026-10-05 14:19:04 -0500 CDT. Newest first.
    param([string]$Text)
    $out = foreach ($l in @((Remove-Ansi $Text) -split "`n")) {
        if ($l.Trim() -notmatch '^([^|\s]+)\|([0-9a-f]{12,})\|(\d{4}-\d\d-\d\d) (\d\d:\d\d:\d\d) ([+-]\d\d)(\d\d)') { continue }
        $created = [DateTimeOffset]::Parse($Matches[3] + 'T' + $Matches[4] + $Matches[5] + ':' + $Matches[6], $script:Invariant).UtcDateTime
        $tag = $Matches[1]; $id = (Format-ShortId $Matches[2])
        # cc-P96: a fourth field, {{.Size}}, for Prune images. Listings from before it have none.
        $parts = @($l.Trim() -split '\|')
        $size = $null
        if ($parts.Count -ge 4) { $size = ConvertFrom-ImageSize $parts[3] }
        [pscustomobject]@{ Tag = $tag; Id = $id; Created = $created; Size = $size }
    }
    @($out | Sort-Object Created -Descending)
}

function Format-ShortId {
    param([string]$Id)
    if (-not $Id) { return '(none)' }
    $x = $Id -replace '^sha256:', ''
    if ($x.Length -gt 12) { $x = $x.Substring(0, 12) }
    $x
}

function ConvertTo-ImageRef {
    # "uo-modernuo" and "uo-modernuo:latest" name the same thing.
    param([string]$Ref)
    if (-not $Ref -or $Ref -match '^sha256:') { return $Ref }
    if ($Ref.Split('/')[-1] -notmatch ':') { return $Ref + ':latest' }
    $Ref
}

function Format-LocalTime {
    param($Utc)
    if (-not $Utc) { return '(unknown)' }
    ([datetime]$Utc).ToLocalTime().ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant)
}

function Format-Age {
    param([TimeSpan]$Span)
    if ($Span.TotalSeconds -lt 0) { $Span = [TimeSpan]::Zero }
    if ($Span.TotalDays -ge 1)    { return ('{0}d {1}h' -f [int][Math]::Floor($Span.TotalDays), $Span.Hours) }
    if ($Span.TotalHours -ge 1)   { return ('{0}h {1}m' -f [int][Math]::Floor($Span.TotalHours), $Span.Minutes) }
    if ($Span.TotalMinutes -ge 1) { return ('{0}m {1}s' -f $Span.Minutes, $Span.Seconds) }
    '{0}s' -f [int]$Span.TotalSeconds
}

function Format-Bytes {
    param([long]$Bytes)
    if ($Bytes -ge 1GB) { return ('{0:N1} GB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N1} MB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N0} KB' -f ($Bytes / 1KB)) }
    '{0} B' -f $Bytes
}

function Get-ImageDrift {
    # Drift is judged by image ID against the current sl-modernuo:latest. Docker never swaps the
    # image under a running container, so a rebuild plus a restart leaves the old code running;
    # a check on the NAME a container was created from cannot see that, because the name moved
    # and the container did not. When the name and the ID disagree, every one of them is shown
    # and none is picked: guessing which one a person meant is how D25 went unnoticed.
    param($Shard, $Container, $Latest, $ConfigRef, $RunningImage, [string]$LatestTag = 'sl-modernuo:latest')
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $r = [ordered]@{
        Verdict = 'UNKNOWN'; CreatedFromLatest = $false; TagMoved = $false; TagGone = $false
        RunningImageGone = $false; Ambiguous = $false; Lines = $null; Advice = $null
    }
    if (-not $Container.Exists) {
        $r.Verdict = 'NO CONTAINER'
        $lines.Add('no container named ' + $Shard.Container)
        $r.Lines = $lines.ToArray()
        return [pscustomobject]$r
    }
    $cfg = $Container.ConfigImage
    $r.CreatedFromLatest = (ConvertTo-ImageRef $cfg) -eq (ConvertTo-ImageRef $LatestTag)
    $r.RunningImageGone  = -not ($RunningImage -and $RunningImage.Exists)
    $r.TagGone           = -not ($ConfigRef -and $ConfigRef.Exists)
    $r.TagMoved          = (-not $r.TagGone) -and ($ConfigRef.Id -ne $Container.ImageId)

    $running = 'running    : ' + (Format-ShortId $Container.ImageId)
    if ($r.RunningImageGone) { $running += '  (no longer in the image store: untagged or removed since)' }
    $lines.Add($running)
    $tag = "created as : '" + $cfg + "', which now means "
    if ($r.TagGone) { $tag += 'nothing (no image has that name now)' } else { $tag += (Format-ShortId $ConfigRef.Id) }
    if ($r.TagMoved) { $tag += '  <- the name has moved since this container was created' }
    $lines.Add($tag)
    if ($Latest -and $Latest.Exists) {
        $lines.Add('latest     : ' + (Format-ShortId $Latest.Id) + ' = ' + $LatestTag + ', built ' + (Format-LocalTime $Latest.Created))
        if ($Container.ImageId -eq $Latest.Id) { $r.Verdict = 'CURRENT' } else { $r.Verdict = 'DRIFTED' }
    } else {
        $lines.Add('latest     : ' + $LatestTag + ' does not exist, so there is nothing to compare against')
    }
    if (-not $r.CreatedFromLatest) {
        $r.Ambiguous = $true
        $lines.Add("AMBIGUOUS  : created from '" + $cfg + "', not " + $LatestTag + '. Drift is judged by image ID against ' +
            $LatestTag + '; the name it was created from is shown above and is not assumed to be what anyone meant.')
    }
    if ($r.TagMoved -or $r.TagGone) {
        $r.Ambiguous = $true
        $lines.Add("AMBIGUOUS  : the name '" + $cfg + "' and the image this container runs no longer agree. Both are shown; the console does not pick one.")
    }
    if ($r.Verdict -eq 'DRIFTED') {
        if ($Shard.IsLive) {
            $r.Advice = "Deploying is Chase's decision and is not a button here (bug-list D25, D29). Until it happens, anything tested on live will look missing: test on the test shard."
        } else {
            $r.Advice = 'Test shard > Start recreates the container on the current ' + $LatestTag + ' (Start-TestShard.ps1 -SkipBuild). ' +
                'A docker restart does NOT: it keeps the old image. If the code changed after ' + (Format-LocalTime $Latest.Created) +
                ', use Rebuild through the gates and start instead.'
        }
    }
    $r.Lines = $lines.ToArray()
    [pscustomobject]$r
}

function Get-LogWindowArgs {
    # The listener line is written seconds after start. After a day of autosaves it is thousands
    # of lines back, so a --tail misses it; read from StartedAt, bounded so an old container is
    # not read whole.
    param($Container, [int]$Minutes = 15)
    if (-not $Container -or -not $Container.Exists -or -not $Container.StartedAtRaw) { return @() }
    $a = @('logs', '--since', $Container.StartedAtRaw)
    if ($Container.StartedAt) { $a += @('--until', (Format-DockerTime $Container.StartedAt.AddMinutes($Minutes))) }
    $a + @($Container.Name)
}

function Get-ListenerState {
    # docker ps saying Up proves nothing, and neither does Test-NetConnection against a published
    # port, because Docker's own forwarder accepts the connection. The server's log line does.
    param($Container, [string]$LogText, [int]$GamePort)
    $lines = @((Remove-Ansi $LogText) -split "`n")
    $promptAt = -1; $listenAt = -1
    $addresses = New-Object 'System.Collections.Generic.List[string]'
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match 'create the owner account') { $promptAt = $i }
        if ($l -match '\(Pings\)') { continue }
        foreach ($m in [regex]::Matches($l, 'Listening: (\S+):(\d+)')) {
            if ([int]$m.Groups[2].Value -eq $GamePort) {
                $listenAt = $i
                $a = $m.Groups[1].Value + ':' + $m.Groups[2].Value
                if (-not $addresses.Contains($a)) { $addresses.Add($a) }
            }
        }
    }
    $state = 'NOT LISTENING'; $detail = ''
    if (-not $Container -or -not $Container.Exists) {
        $state = 'NO CONTAINER'; $detail = 'there is no container'
    } elseif (-not $Container.Running) {
        $state = 'STOPPED'
        $detail = 'the container is ' + $Container.Status + '; nothing is listening.'
        if ($listenAt -ge 0) { $detail += ' (Its last run did reach Listening on :' + $GamePort + '; docker logs keeps that line after the exit.)' }
    } elseif ($promptAt -ge 0 -and $listenAt -lt $promptAt) {
        $state = 'OWNER PROMPT'
        $detail = "blocked at 'create the owner account now? (y/n)'. The world has no accounts it can load (bug-list D19): " +
            'docker ps says Up and nothing listens. Answer it: docker attach ' + $Container.Name +
            ', then y, a username, a password, each with Enter; detach with Ctrl+P Ctrl+Q (Ctrl+C stops the shard).'
    } elseif ($listenAt -ge 0) {
        $state = 'LISTENING'; $detail = 'on ' + ($addresses -join ', ')
    } else {
        $detail = "no 'Listening: ...:" + $GamePort + "' line since this start (" + $lines.Count + ' log lines read). Still starting, or stuck: tail the log.'
    }
    [pscustomobject]@{ State = $state; Addresses = $addresses.ToArray(); Detail = $detail }
}

function Test-SavesOnStop {
    # D36. Whether this run of the shard registered the save-on-stop handler. Read from the log
    # the process wrote since it started, not from the image, so it is true of what is running.
    param([string]$LogText)
    (Remove-Ansi $LogText).Contains($script:SavesOnStopLine)
}

function ConvertFrom-FileListing {
    # One file per line: 2026-09-29T16:00:00Z|12345|Accounts\Accounts.bin
    param([string]$Text)
    foreach ($l in @((Remove-Ansi $Text) -split "`n")) {
        if ($l -match '^(\S+)\|(\d+)\|(.+)$') {
            [pscustomobject]@{ LastWriteUtc = (ConvertFrom-DockerTime $Matches[1]); Length = [long]$Matches[2]; RelPath = $Matches[3] }
        }
    }
}

function Get-SaveSummary {
    param($Files)
    $f = @($Files | Where-Object { $_ })
    if ($f.Count -eq 0) {
        return [pscustomobject]@{ Exists = $false; Count = 0; Bytes = 0; Newest = $null; HasAccounts = $false; HasTypes = $false }
    }
    $bytes = 0L
    foreach ($x in $f) { $bytes += $x.Length }
    [pscustomobject]@{
        Exists      = $true
        Count       = $f.Count
        Bytes       = $bytes
        Newest      = @($f | Sort-Object LastWriteUtc -Descending)[0]
        HasAccounts = [bool](@($f | Where-Object { $_.RelPath -match '^Accounts[\\/]' }).Count)
        HasTypes    = [bool](@($f | Where-Object { $_.RelPath -eq 'SerializedTypes.db' }).Count)
    }
}

function Test-SnapshotLabel {
    # Returns why a name is refused, or $null. The name becomes part of a folder name and of a
    # command line, so only characters that are safe in both.
    param([string]$Label)
    if (-not $Label) { return 'a snapshot needs a name' }
    if ($Label.Length -gt 40) { return 'names are at most 40 characters' }
    if ($Label -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { return 'use letters, digits, dot, dash and underscore, starting with a letter or digit' }
    if ($Label -match 'pre-?restore') { return "'pre-restore' is kept for the snapshot a restore takes by itself" }
    $null
}

function New-SnapshotFolderName {
    param([string]$Label, [datetime]$Now)
    '{0}_{1}' -f $Now.ToString('yyyyMMdd-HHmmss', $script:Invariant), $Label
}

function New-PreRestoreFolderName {
    param([string]$RestoringFrom, [datetime]$Now)
    $from = $RestoringFrom -replace '[^A-Za-z0-9._-]', ''
    if ($from.Length -gt 60) { $from = $from.Substring(0, 60) }
    '{0}_PRE-RESTORE_before-restoring-{1}' -f $Now.ToString('yyyyMMdd-HHmmss', $script:Invariant), $from
}

function ConvertFrom-SnapshotFolderName {
    param([string]$Name)
    if ($Name -cnotmatch $script:SnapshotPattern) { return $null }
    $label = $Matches[2]
    [pscustomobject]@{
        Name         = $Name
        Taken        = [datetime]::ParseExact($Matches[1], 'yyyyMMdd-HHmmss', $script:Invariant)
        Label        = $label
        IsPreRestore = $label.StartsWith('PRE-RESTORE')
    }
}

function ConvertFrom-SnapshotListing {
    # One snapshot per line: name|bytes|files|accounts(1 or 0)
    param([string]$Text)
    $out = foreach ($l in @((Remove-Ansi $Text) -split "`n")) {
        if ($l -match '^([^|]+)\|(\d+)\|(\d+)\|([01])$') {
            $bytes = [long]$Matches[2]; $files = [int]$Matches[3]; $acc = $Matches[4] -eq '1'
            $p = ConvertFrom-SnapshotFolderName $Matches[1]
            if ($p) {
                $p | Add-Member -NotePropertyName Bytes -NotePropertyValue $bytes -PassThru |
                     Add-Member -NotePropertyName Files -NotePropertyValue $files -PassThru |
                     Add-Member -NotePropertyName HasAccounts -NotePropertyValue $acc -PassThru
            }
        }
    }
    @($out | Sort-Object Taken -Descending)
}

function Get-SnapshotDeleteWarning {
    param($Snapshots, [string]$Name, $Shard)
    $all = @($Snapshots)
    $t = @($all | Where-Object { $_.Name -eq $Name })
    $w = New-Object 'System.Collections.Generic.List[string]'
    if ($all.Count -eq 1 -and $t.Count -eq 1) {
        $w.Add('This is the LAST snapshot of the ' + $Shard.Label + ' shard. After this there are none.')
    }
    if ($t.Count -and $t[0].IsPreRestore) {
        $w.Add('It is an automatic pre-restore snapshot: the world as it was just before a restore.')
    }
    $w -join ' '
}

function New-AsideName {
    param([string]$Saves, [datetime]$Now)
    $Saves.TrimEnd('\') + '.aside-' + $Now.ToString('yyyyMMdd-HHmmss', $script:Invariant)
}

function Get-ConfirmPhrase {
    param([string]$Action)
    switch ($Action) {
        'live.start'       { 'start live' }
        'live.stop'        { 'stop live' }
        'live.restart'     { 'restart live' }
        'live.tail'        { 'tail live' }
        'live.client'      { 'open live client' }
        'snapshot.create'  { 'snapshot live' }
        'snapshot.restore' { 'restore live' }
        'snapshot.delete'  { 'delete live snapshot' }
        'commit.run'       { 'commit shard work' }
        'package.publish'  { 'publish player package' }
        'package.scripts.dev'     { 'publish scripts to dev' }
        'package.scripts.promote' { 'promote scripts to official' }
        'test.deploy'      { 'deploy to test' }
        'test.wipe'        { 'WIPE TEST ACCOUNTS' }
        'fix.restart'      { 'restart test' }
        'fix.rollback'     { 'roll back test' }
        'fix.rebuild'      { 'rebuild test' }
        'fix.prune'        { 'prune images' }
        'golem.move'       { 'move to the golem' }
        'golem.longrun'    { 'start long run' }
        'golem.wrapup'     { 'wrap up golem' }
        'golem.stoppoint'  { 'find a stopping point' }
        'golem.resume'     { 'poke the golem' }   # cc-P153: Poke the Golem (it was Wake the Golem (CC session))
        'golem.nightshift' { 'night shift on' }
        'golem.integrate'  { 'merge branches' }
        'golem.mergepause'  { 'pause merging' }    # cc-P208
        'golem.away'        { 'away on' }          # cc-P149
        'golem.awayoff'     { 'away off' }
        'golem.mergeresume' { 'resume merging' }
        'golem.tinkerpause'  { 'pause tinker' }    # cc-P222
        'golem.tinkerresume' { 'resume tinker' }
        'golem.tinkerrun'    { 'run tinker' }
        'golem.lanenow'      { 'send to the second pc' }   # cc-P159
        'web.deploy'       { 'deploy to haven' }
        { $_ -in @('web.ops-pages', 'web.ops-cutover', 'web.ops-rollback') } { 'deploy to haven' }
        { $_ -in @('web.print-queue', 'web.print-queue-rollback') } { 'deploy to haven' }
        { $_ -in @('web.mega-dashboard', 'web.mega-dashboard-rollback') } { 'deploy to haven' }
        { $_ -in @('web.portainer-haven', 'web.portainer-haven-rollback') } { 'deploy to haven' }
        default            { 'confirm live' }
    }
}

function Get-ConfirmSubject {
    # The typed-confirmation dialog's title: what the phrase is for.
    param([string]$Action)
    switch ($Action) {
        'commit.run'      { 'Commit and push' }
        'package.publish' { 'Publish player package' }
        'package.scripts.dev'     { 'Publish scripts to dev' }
        'package.scripts.promote' { 'Promote scripts to official' }
        'test.deploy'     { 'Deploy to test' }
        'test.wipe'       { 'Wipe test accounts' }
        'fix.restart'     { 'Restart the test shard' }
        'golem.move'      { 'Move to the Golem' }
        'golem.integrate' { 'Merge branches now' }
        'golem.mergepause'  { 'Pause merging' }    # cc-P208
        'golem.away'        { 'Away mode on' }     # cc-P149
        'golem.skipday'     { 'Owner works on a date' }   # cc-P287
        'golem.awayoff'     { 'Away mode off' }
        'golem.answer'      { 'Answer a Golem''s question' }   # cc-P216
        'golem.mergeresume' { 'Resume merging' }
        'golem.tinkerpause'  { 'Pause the Tinker' }    # cc-P222
        'golem.tinkerresume' { 'Resume the Tinker' }
        'golem.tinkerrun'    { 'Tinker: run now' }
        'web.deploy'      { 'Deploy to Haven' }
        { $_ -in @('web.ops-pages', 'web.ops-cutover', 'web.ops-rollback') } { 'Ops pages on Haven' }
        { $_ -in @('web.print-queue', 'web.print-queue-rollback') } { 'Print queue on Haven' }
        { $_ -in @('web.mega-dashboard', 'web.mega-dashboard-rollback') } { 'Mega dashboard on Haven' }
        { $_ -in @('web.portainer-haven', 'web.portainer-haven-rollback') } { 'Portainer on Haven' }
        'fix.rollback'    { 'Roll back the test shard' }
        'fix.rebuild'     { 'Rebuild the test shard' }
        'fix.prune'       { 'Prune images' }
        { $_ -like 'question.*' } { 'Answer the shard''s question' }
        default           { 'LIVE shard' }
    }
}

function Get-ConfirmPreface {
    # D36: a stop saves nothing. When a plan stops a shard, say so first in the confirmation,
    # above the plan, where it is read before the phrase is typed rather than inside step 3.
    param($Plan)
    # cc-P73: a wipe says what it deletes first, and the D36 warning with it when the stop does not save.
    if (@($Plan | Where-Object { $_.Kind -eq 'wiperequest' }).Count) {
        $w = 'BEFORE YOU TYPE: this deletes every account and character on the TEST shard. The whole test save is backed up first.'
        if (@($Plan | Where-Object { $_.Text -and $_.Text.Contains($script:NoSaveWarning) }).Count) { $w += ' ' + $script:NoSaveWarning + ' (D36)' }
        return $w
    }
    if (@($Plan | Where-Object { $_.Text -and $_.Text.Contains($script:NoSaveWarning) }).Count) {
        return ('BEFORE YOU TYPE: ' + $script:NoSaveWarning + ' (D36)')
    }
    if (@($Plan | Where-Object { $_.Kind -eq 'script' -and $_.Path -and (Split-Path $_.Path -Leaf) -eq 'Commit-ShardWork.ps1' -and -not $_.Named['DryRun'] }).Count) {
        return ('BEFORE YOU TYPE: ' + $script:CommitPublicWarning)
    }
    if (@($Plan | Where-Object { $_.Kind -eq 'script' -and $_.Path -and (Split-Path $_.Path -Leaf) -eq 'Publish-PlayerPackage.ps1' }).Count) {
        return ('BEFORE YOU TYPE: ' + $script:PublishPublicWarning)
    }
    ''
}

function Test-TypedConfirmation {
    param([string]$Expected, [string]$Typed)
    if ($null -eq $Typed -or -not $Expected) { return $false }
    [string]::Equals($Expected, $Typed.Trim(), [StringComparison]::Ordinal)
}

function Get-ChildArgumentLine {
    # D37. A window action runs in a child process of this script, which cannot see the parent's
    # -DryRun. So the parent always names the mode, dry or not, and it goes first where a person
    # reading the command line sees it. Resolve-RunMode is the other half.
    param(
        [string]$SelfPath, [string]$ActionKey, [string]$ShardKey, [string]$SnapshotName,
        [string]$Snapshot, [string]$ConfirmLive, [switch]$Yes, [switch]$DryRun, [string]$StatusFrom,
        [switch]$StatusFromDocker, [string]$CommitMessage, [string]$PackageNotes, [string]$PackageZip,
        [string]$ImageTag, [switch]$CommitForce, [string]$WipeAdmin, [switch]$ForPane, [string]$Commit,
        [string]$VdFile, [string]$Body, [string]$HuesFile
    )
    $mode = 'Execute'
    if ($DryRun) { $mode = 'DryRun' }
    # cc-P84: a run streamed into the Action output pane must end when its work does, and must never wait on a
    # keyboard it does not have: -NonInteractive instead of -NoExit. Everything after it is the same line.
    $keep = '-NoExit'
    if ($ForPane) { $keep = '-NonInteractive' }
    $a = '-NoProfile -ExecutionPolicy Bypass ' + $keep + ' -File "' + $SelfPath + '" -Mode ' + $mode + ' -Action ' + $ActionKey
    if ($ShardKey -in @('test', 'live')) { $a += ' -Shard ' + $ShardKey }
    # A dry child reads a capture file unless told otherwise. When the parent's status came from
    # docker (the toggle, not -DryRun), the child reads docker too, so it prints the same plan.
    if ($DryRun -and $StatusFromDocker) { $a += ' -StatusFromDocker' }
    elseif ($DryRun -and $StatusFrom) { $a += ' -StatusFrom "' + $StatusFrom + '"' }
    if ($SnapshotName) { $a += ' -SnapshotName ' + $SnapshotName }
    if ($Snapshot) { $a += ' -Snapshot ' + $Snapshot }
    if ($ConfirmLive) { $a += ' -ConfirmLive "' + $ConfirmLive + '"' }
    if ($Yes) { $a += ' -Yes' }
    # A commit message is typed by a person and can hold quotes, $ and ;. It crosses this command
    # line as base64 of its UTF-8, which has none of them, so no quoting rule can change it (cc-P60).
    if ($CommitMessage) { $a += ' -CommitMessageB64 ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($CommitMessage)) }
    # cc-P68: the Force box. The child rebuilds the plan, so it must skip the same wait the dialog said.
    if ($CommitForce) { $a += ' -CommitForce' }
    # cc-P63: package notes are typed too, and cross the same way. A zip name and an image tag are
    # picked from lists the console read; the plan refuses anything outside those, and they are
    # quoted here only so a stray space cannot split them.
    if ($PackageNotes) { $a += ' -PackageNotesB64 ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($PackageNotes)) }
    if ($PackageZip) { $a += ' -PackageZip "' + ($PackageZip -replace '"', '') + '"' }
    if ($ImageTag) { $a += ' -ImageTag "' + ($ImageTag -replace '"', '') + '"' }
    # cc-P73: the new admin's name, which Test-WipeAdminName keeps to one word. Never the password:
    # the child asks for that itself.
    if ($WipeAdmin) { $a += ' -WipeAdmin "' + ($WipeAdmin -replace '"', '') + '"' }
    # cc-P96: a commit picked from Rebuild's list; only hex or HEAD crosses, and the plan refuses one not on the list.
    if ($Commit -match '^([0-9a-fA-F]{7,40}|HEAD)$') { $a += ' -Commit ' + $Commit }
    # cc-P135: the .vd and hues.mul paths are picked in a file dialog or typed; quoted so a space cannot split them, a
    # quote in a path dropped (the plan then refuses a file that is not there). The body crosses only as digits.
    if ($VdFile) { $a += ' -VdFile "' + ($VdFile -replace '"', '') + '"' }
    if ($Body -match '^\d{1,5}$') { $a += ' -Body ' + $Body }
    if ($HuesFile) { $a += ' -HuesFile "' + ($HuesFile -replace '"', '') + '"' }
    $a
}

function Resolve-RunMode {
    # 'dry', 'execute' or 'refuse' for a headless -Action. Any sign of a dry run wins. With no mode
    # at all, an action that changes something is refused rather than run, so a mode lost on the
    # way from the parent fails safe (D37). Actions that only read or open still run.
    param([string]$Action, [string]$Mode, [switch]$DryRun)
    $Action = Resolve-ActionAlias $Action
    if ($DryRun -or $Mode -eq 'DryRun') { return 'dry' }
    if ($Mode -eq 'Execute') { return 'execute' }
    $def = @(Get-ConsoleActions | Where-Object { $_.Key -eq $Action })[0]
    if ($def -and -not $def.Mutates) { return 'execute' }
    'refuse'
}

function Get-ModeBanner {
    # The first thing a spawned window says, before it reads any state or runs anything.
    param([string]$RunMode, [string]$Action, [string]$ShardKey)
    $Action = Resolve-ActionAlias $Action
    $bar = '=' * 78
    $what = $Action + ' (' + $ShardKey + ')'
    if ($Action -like 'commit.*') { $what = $Action + ' (both repos)' }
    if ($Action -like 'package.*') { $what = $Action + ' (player package)' }
    if ($Action -like 'package.scripts.*') { $what = $Action + ' (Legion scripts)' }
    if ($Action -like 'golem.*') { $what = $Action + ' (golem)' }
    if ($Action -like 'web.*') { $what = $Action + ' (websites)' }
    if ($Action -like 'vpn.*') { $what = $Action + ' (this PC''s VPN)' }
    if ($Action -like 'art.*' -or $Action -like 'map.*') { $what = $Action + ' (map and art)' }
    switch ($RunMode) {
        'dry' {
            [pscustomobject]@{ Color = 'amber'; Title = ('DRY RUN - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  DRY RUN  ' + $what), '  Nothing will be run. This window only prints the plan.', $bar) }
        }
        { $_ -eq 'execute' -and $Action -eq 'commit.preview' } {
            [pscustomobject]@{ Color = 'amber'; Title = ('PREVIEW - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  PREVIEW  ' + $what), '  Commit-ShardWork.ps1 -DryRun: it lists what would be committed and changes nothing.', $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -eq 'commit.run' } {
            [pscustomobject]@{ Color = 'red'; Title = ('RUNNING FOR REAL - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  RUNNING FOR REAL  ' + $what), ('  This commits and pushes both repos. ' + $script:CommitPublicWarning), $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -eq 'package.build' } {
            [pscustomobject]@{ Color = 'amber'; Title = ('BUILDING - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  BUILDING  ' + $what), '  Build-PlayerPackage.ps1 writes a zip into player-package\dist. Nothing is published.', $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -eq 'package.check' } {
            [pscustomobject]@{ Color = 'amber'; Title = ('CHECKING - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  CHECKING  ' + $what), '  Build-PlayerPackage.ps1 -CheckZip: it reads the zip and changes nothing.', $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -eq 'test.wipe' } {
            [pscustomobject]@{ Color = 'red'; Title = ('RUNNING FOR REAL - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  RUNNING FOR REAL  ' + $what), '  This deletes every account and character on the TEST shard. It asks for the new admin password below.', $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -like 'golem.*' } {
            [pscustomobject]@{ Color = 'amber'; Title = ('GOLEM - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  GOLEM  ' + $what), '  Opens the Golem''s own script in a window of its own, or its folder or log. No shard is touched.', $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -like 'vpn.*' } {
            # cc-P86: no shard is touched; a WireGuard tunnel may be ended, Proton is only opened.
            [pscustomobject]@{ Color = 'amber'; Title = ('VPN - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  VPN  ' + $what), '  Ends a WireGuard for Windows tunnel, opens Proton VPN, or names the VPN to disconnect in its app. No shard is touched.', $bar) }
            break
        }
        { $_ -eq 'execute' -and ($Action -like 'art.*' -or $Action -like 'map.*') } {
            # cc-P135: validate-art.py reads the client data and writes nothing; the map buttons only print.
            [pscustomobject]@{ Color = 'amber'; Title = ('MAP AND ART - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  MAP AND ART  ' + $what), '  Runs scripts\validate-art.py, which reads the client data and writes nothing, or prints a command''s syntax. No shard is touched.', $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -like 'package.scripts.*' } {
            # cc-P144: no shard is touched. Dev and Promote change what Haven serves; each asks "Type yes" itself.
            $why = switch ($Action) {
                'package.scripts.check'   { 'Publish-LegionScripts.ps1 -Mode Check: your repo vs dev vs official. It changes nothing.' }
                'package.scripts.dev'     { 'Publish-LegionScripts.ps1 -Mode Dev: your PCs'' dev channel on Haven. It asks "Type yes" itself, below.' }
                'package.scripts.promote' { 'Publish-LegionScripts.ps1 -Mode Promote: EVERY player''s install pulls the official channel. It asks "Type yes" itself, below.' }
                'package.scripts.pull'    { 'git pull --ff-only in your scripts repo.' }
                'package.scripts.setup'   { 'Setup-ScriptsRepo.ps1: the clone and every client''s link to it. It may ask where the clone lives.' }
                default                   { 'Setup-PublishKey.ps1: this PC''s SSH key on Haven. It asks Haven''s password once.' }
            }
            $col = 'amber'
            if ($Action -eq 'package.scripts.promote') { $col = 'red' }
            [pscustomobject]@{ Color = $col; Title = ('LEGION SCRIPTS - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  LEGION SCRIPTS  ' + $what), ('  ' + $why + ' No shard is touched.'), $bar) }
            break
        }
        { $_ -eq 'execute' -and $Action -eq 'package.publish' } {
            [pscustomobject]@{ Color = 'red'; Title = ('RUNNING FOR REAL - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  RUNNING FOR REAL  ' + $what), ('  ' + $script:PublishPublicWarning + ' Publish-PlayerPackage.ps1 asks "Type yes" itself, below.'), $bar) }
            break
        }
        'execute' {
            [pscustomobject]@{ Color = 'red'; Title = ('RUNNING FOR REAL - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  RUNNING FOR REAL  ' + $what), ('  This acts on the ' + $ShardKey.ToUpper() + ' shard.'), $bar) }
        }
        default {
            [pscustomobject]@{ Color = 'red'; Title = ('REFUSED - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  REFUSED  ' + $what),
                '  No -Mode was given, and this action changes something. Nothing was run.',
                '  Pass -Mode Execute to run it, or -DryRun to print it. (D37: a missing mode fails safe.)', $bar) }
        }
    }
}

function Get-ConsoleActions {
    # RunIn: 'window' runs headless in a console window of its own, so a long step can be watched
    # and the form stays responsive; 'here' only opens something or prints.
    @(
        [pscustomobject]@{ Key = 'test.start';       Group = 'Test shard'; Label = 'Start';                               Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.fresh';       Group = 'Test shard'; Label = 'Start fresh';                         Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.wipe';        Group = 'Test shard'; Label = 'Wipe test accounts';                  Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.stop';        Group = 'Test shard'; Label = 'Stop';                                Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.rebuild';     Group = 'Test shard'; Label = 'Rebuild through the gates and start'; Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.deploy';      Group = 'Test shard'; Label = 'Deploy built image';                  Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.tail';        Group = 'Test shard'; Label = 'Tail the log';                        Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'test.client';      Group = 'Test shard'; Label = 'Open a client';                       Mutates = $false; RunIn = 'here' }
        # cc-P96: its own tab. Diagnose only reads and prints here; the other four confirm with a phrase and run in a window.
        [pscustomobject]@{ Key = 'fix.diagnose';     Group = 'Fix test shard'; Label = 'Diagnose';                        Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'fix.restart';      Group = 'Fix test shard'; Label = 'Restart';                         Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'fix.rollback';     Group = 'Fix test shard'; Label = 'Roll back to last good';          Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'fix.rebuild';      Group = 'Fix test shard'; Label = 'Rebuild a commit';                Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'fix.prune';        Group = 'Fix test shard'; Label = 'Prune images';                    Mutates = $true;  RunIn = 'window' }
        # cc-P106: its own tab, for both shards (the shard picked on the tab). Show only reads; each answer and Stop confirm with a phrase.
        [pscustomobject]@{ Key = 'question.show';    Group = 'Shard question'; Label = 'Show the question';               Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'question.n';       Group = 'Shard question'; Label = 'Answer n';                        Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'question.y';       Group = 'Shard question'; Label = 'Answer y (deletes)';              Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'question.a';       Group = 'Shard question'; Label = 'Answer a (deletes all)';          Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'question.enter';   Group = 'Shard question'; Label = 'Press Enter';                     Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'question.stop';    Group = 'Shard question'; Label = 'Stop the shard';                  Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'live.start';       Group = 'LIVE shard'; Label = 'Start';                               Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'live.stop';        Group = 'LIVE shard'; Label = 'Stop';                                Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'live.restart';     Group = 'LIVE shard'; Label = 'Restart';                             Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'live.tail';        Group = 'LIVE shard'; Label = 'Tail the log';                        Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'live.client';      Group = 'LIVE shard'; Label = 'Open a client';                       Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'snapshot.create';  Group = 'Snapshots';  Label = 'Create named snapshot';               Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'snapshot.list';    Group = 'Snapshots';  Label = 'List';                                Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'snapshot.restore'; Group = 'Snapshots';  Label = 'Restore selected';                    Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'snapshot.delete';  Group = 'Snapshots';  Label = 'Delete selected';                     Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'diag.docker';      Group = 'Diagnostics'; Label = 'Run Check-Docker.ps1';               Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'diag.shard';       Group = 'Diagnostics'; Label = 'Run Check-Shard.ps1';                Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'diag.folder';      Group = 'Diagnostics'; Label = 'Open the folder they write to';      Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'commit.preview';   Group = 'Commit';      Label = 'Preview commit';                     Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'commit.run';       Group = 'Commit';      Label = 'Commit and push';                    Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.build';    Group = 'Player package'; Label = 'Build package';                   Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.publish';  Group = 'Player package'; Label = 'Publish package';                 Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.check';    Group = 'Player package'; Label = 'Check selected';                  Mutates = $false; RunIn = 'window' }
        # cc-P144: the Legion scripts row on the Player package tab. Dev and Promote ask a phrase here and "Type yes" in
        # their own window; Setup and the key ask in theirs. None is refused for an uncommitted shard repo.
        [pscustomobject]@{ Key = 'package.scripts.check';   Group = 'Legion scripts'; Label = 'Check scripts';           Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.scripts.dev';     Group = 'Legion scripts'; Label = 'Publish to dev';          Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.scripts.promote'; Group = 'Legion scripts'; Label = 'Promote to official';     Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.scripts.pull';    Group = 'Legion scripts'; Label = 'Pull scripts';            Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.scripts.setup';   Group = 'Legion scripts'; Label = 'Set up scripts repo';     Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'package.scripts.key';     Group = 'Legion scripts'; Label = 'Set up publish key';      Mutates = $true;  RunIn = 'window' }
        # cc-P74. Each opens one of the Golem's own scripts in a window of its own (a 'window' step),
        # so RunIn is 'here': the console only opens it, and the script's window stays open to be read.
        # cc-P99: the autopilot.* keys of cc-P74 are aliases of these (Resolve-ActionAlias).
        [pscustomobject]@{ Key = 'golem.on';         Group = 'Golem';       Label = 'Wake the Golem';                     Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.off';        Group = 'Golem';       Label = 'Rest the Golem';                     Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.email';      Group = 'Golem';       Label = 'Golem email setup';                  Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.test';       Group = 'Golem';       Label = 'Send test email';                    Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.queue';      Group = 'Golem';       Label = 'Open the queue folder';              Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.log';        Group = 'Golem';       Label = 'Golem log';                          Mutates = $false; RunIn = 'here' }
        # cc-P94: opens settings.json in Notepad; the console itself changes nothing.
        [pscustomobject]@{ Key = 'golem.settings';   Group = 'Golem';       Label = 'Golem settings';                     Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.folder';     Group = 'Golem';       Label = 'Open Golem folder';                  Mutates = $false; RunIn = 'here' }
        # cc-P99: the cutover, after a typed phrase; the script shows its own dry run and asks again.
        [pscustomobject]@{ Key = 'golem.move';       Group = 'Golem';       Label = 'Move to the Golem';                  Mutates = $true;  RunIn = 'here' }
        # cc-P125: the run buttons, a row of their own on the Golem tab. Wrap up and Find a stopping point toggle to Cancel.
        [pscustomobject]@{ Key = 'golem.longrun';    Group = 'Golem run';   Label = 'Start long run';                     Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.endlongrun'; Group = 'Golem run';   Label = 'End long run';                       Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.wrapup';     Group = 'Golem run';   Label = 'Wrap up after this task';            Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.stoppoint';  Group = 'Golem run';   Label = 'Find a stopping point';              Mutates = $true;  RunIn = 'here' }
        # cc-P131: waking a CC session from here (the Claude Code CLI), self-wake and the night shift toggles, the sign-in check.
        [pscustomobject]@{ Key = 'golem.resume';     Group = 'Golem wake';  Label = 'Poke the Golem';                     Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.autowake';   Group = 'Golem wake';  Label = 'Self-wake: on';                      Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.nightshift'; Group = 'Golem wake';  Label = 'Night shift: on';                    Mutates = $true;  RunIn = 'here' }
        # cc-P200: marathon mode (auto, on or off: the pick beside it).
        [pscustomobject]@{ Key = 'golem.marathon';   Group = 'Golem wake';  Label = 'Marathon: set';                      Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.clicheck';   Group = 'Golem wake';  Label = 'Check CLI sign-in';                  Mutates = $false; RunIn = 'here' }
        # cc-P182: a row of their own: the watch now, the picked queue row first, and the STOPPED prompt back.
        [pscustomobject]@{ Key = 'golem.poke';       Group = 'Golem queue'; Label = 'Check now (run the watch)';          Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.runnext';    Group = 'Golem queue'; Label = 'Run next (picked row)';              Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.retry';      Group = 'Golem queue'; Label = 'Retry stopped prompt';               Mutates = $true;  RunIn = 'here' }
        # cc-P159: a Codex lane (the picked Golem) may start now, outside its night window.
        [pscustomobject]@{ Key = 'golem.lanenow';    Group = 'Golem queue'; Label = 'Send to the second PC';              Mutates = $true;  RunIn = 'here' }
        # cc-P184: the integrator merges the other Golems' finished branches into main now (otherwise between its own prompts).
        [pscustomobject]@{ Key = 'golem.integrate';  Group = 'Golem queue'; Label = 'Merge branches now';                 Mutates = $true;  RunIn = 'here' }
        # cc-P216: Chase's answer to a Golem's open question (which question, a choice, his words: a picker), beside the queue row.
        [pscustomobject]@{ Key = 'golem.answer';     Group = 'Golem answer'; Label = 'Answer a Golem''s question';        Mutates = $true;  RunIn = 'here' }
        # cc-P208: Shadow Iron, the merge Golem: no round runs while paused (its tickets wait); Resume runs one at once.
        [pscustomobject]@{ Key = 'golem.mergepause';  Group = 'Golem merge'; Label = 'Pause merging';                     Mutates = $true;  RunIn = 'here' }
        # cc-P149: away mode, until the time in the long run's Until box.
        [pscustomobject]@{ Key = 'golem.away';        Group = 'Golem merge'; Label = 'Away mode on (until)';              Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.awayoff';     Group = 'Golem merge'; Label = 'Away mode off';                     Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.mergeresume'; Group = 'Golem merge'; Label = 'Resume merging';                    Mutates = $true;  RunIn = 'here' }
        # cc-P222: the Tinker, the watchdog: a round now, pause and resume (its card on both consoles).
        [pscustomobject]@{ Key = 'golem.tinkerrun';    Group = 'Golem merge'; Label = 'Tinker: run now';                  Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.tinkerpause';  Group = 'Golem merge'; Label = 'Pause the Tinker';                 Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'golem.tinkerresume'; Group = 'Golem merge'; Label = 'Resume the Tinker';                Mutates = $true;  RunIn = 'here' }
        # cc-P187: the Golem picker's row: the picked queued prompt goes to the picked Golem (its **Golem:** line).
        [pscustomobject]@{ Key = 'golem.assign';     Group = 'Golem pick';  Label = 'Assign picked row to this Golem';    Mutates = $true;  RunIn = 'here' }
        # cc-P287: the picked Golem keeps to its hours on the Until box's date (the owner works): its days.skip.
        [pscustomobject]@{ Key = 'golem.skipday';    Group = 'Golem pick';  Label = 'Owner works (Until box date)';       Mutates = $true;  RunIn = 'here' }
        # cc-P83: the two pages, opened in the default browser. Beside the PLAY TESTS tile and on the Autopilot tab.
        [pscustomobject]@{ Key = 'links.playtests';  Group = 'Links';       Label = 'Play tests';                         Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'links.dashboard';  Group = 'Links';       Label = 'Dashboard';                          Mutates = $false; RunIn = 'here' }
        # cc-P85: the shard status page, beside the two above.
        [pscustomobject]@{ Key = 'links.status';     Group = 'Links';       Label = 'Status';                             Mutates = $false; RunIn = 'here' }
        # cc-P101: the Websites tab. Each acts on the site picked there (-Site from the command line).
        [pscustomobject]@{ Key = 'web.open';         Group = 'Websites';    Label = 'Open';                               Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.restart';      Group = 'Websites';    Label = 'Restart';                            Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.redeploy';     Group = 'Websites';    Label = 'Redeploy';                           Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.logs';         Group = 'Websites';    Label = 'Logs (last 200)';                    Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.deploy';       Group = 'Websites';    Label = 'Deploy to Haven';                    Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.npm';          Group = 'Websites';    Label = 'Open NPM';                           Mutates = $false; RunIn = 'here' }
        # cc-P107: the ops pages' three Haven deploys. Not tied to the picked site: each runs its named deploy.
        [pscustomobject]@{ Key = 'web.ops-pages';    Group = 'Websites';    Label = 'Ops pages: deploy';                  Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.ops-cutover';  Group = 'Websites';    Label = 'Ops pages: cut over';                Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.ops-rollback'; Group = 'Websites';    Label = 'Ops pages: roll back';               Mutates = $true;  RunIn = 'here' }
        # cc-P109: the Sunflower print queue's two Haven deploys, the same way.
        [pscustomobject]@{ Key = 'web.print-queue';  Group = 'Websites';    Label = 'Print queue: deploy';                Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.print-queue-rollback'; Group = 'Websites'; Label = 'Print queue: roll back';        Mutates = $true;  RunIn = 'here' }
        # cc-P110: the one Homepage on Haven, the same way.
        [pscustomobject]@{ Key = 'web.mega-dashboard'; Group = 'Websites';  Label = 'Mega dashboard: deploy';             Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.mega-dashboard-rollback'; Group = 'Websites'; Label = 'Mega dashboard: roll back';  Mutates = $true;  RunIn = 'here' }
        # cc-P163: Portainer CE on Haven, the same way.
        [pscustomobject]@{ Key = 'web.portainer-haven'; Group = 'Websites'; Label = 'Portainer (Haven): deploy';       Mutates = $true;  RunIn = 'here' }
        [pscustomobject]@{ Key = 'web.portainer-haven-rollback'; Group = 'Websites'; Label = 'Portainer (Haven): roll back'; Mutates = $true; RunIn = 'here' }
        # cc-P86: beside the PUBLIC DNS tile and on the Diagnostics tab. 'here': it opens Proton or ends a WireGuard
        # tunnel from this console, then the tile re-checks.
        [pscustomobject]@{ Key = 'vpn.disconnect';   Group = 'Network';     Label = 'Disconnect VPN';                     Mutates = $true;  RunIn = 'here' }
        # cc-P135: the Map and Art tab. The four art checks run scripts\validate-art.py (read only) in the pane or a window
        # of their own; the six map buttons only print an in-game command's syntax here.
        [pscustomobject]@{ Key = 'art.registry';     Group = 'Map and Art';  Label = 'Check the art registry';            Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'art.bodies';       Group = 'Map and Art';  Label = 'Body table';                        Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'art.checkvd';      Group = 'Map and Art';  Label = 'Check a .vd';                       Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'art.hues';         Group = 'Map and Art';  Label = 'Compare a hues.mul with ours';      Mutates = $false; RunIn = 'window' }
        [pscustomobject]@{ Key = 'map.footprint';    Group = 'Map commands'; Label = '[CheckFootprint';                   Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'map.spots';        Group = 'Map commands'; Label = '[ValidateSpots';                    Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'map.statics';      Group = 'Map commands'; Label = '[FindStaticProblems';               Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'map.walkable';     Group = 'Map commands'; Label = '[Walkable';                         Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'map.shore';        Group = 'Map commands'; Label = '[ShoreCheck';                       Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'map.export';       Group = 'Map commands'; Label = '[ExportPoints';                     Mutates = $false; RunIn = 'here' }
    )
}

# --- cc-P84: the theme table, the tooltip table, the settings and where an action's output goes ----------------
# Everything the drawing code needs to know about a look is in Get-ConsoleTheme; nothing else in this file names a
# color or a font for a themed control. Colors are R,G,B integers here (CORE has no System.Drawing); the SHELL turns
# them into Drawing.Color. 'Plain' is the console as it looked before cc-P84, value for value: a Plain field that is
# $null means "leave the WinForms default", which is what the form did then.

function Get-ConsoleTheme {
    # cc-P132: three looks. Plain (the console as it was), UO (cc-P84, client gump art), Enhanced (the default: dark slate
    # and stone drawn in code, bronze trim, runes, crystals and a Golem figure, with art slots in scripts\console-art).
    # Every look carries Text, the named text colors (section 5): normal, muted, warning, danger, success, link, each
    # chosen to pass WCAG AA on the page it is drawn on; a themed look also carries Plate, the plain dark plate a
    # confirm phrase sits on, and StoneWarn / StoneDanger for warning words drawn on the stone rather than a page.
    param([ValidateSet('UO', 'Plain', 'Enhanced')][string]$Name = 'Plain')
    $plain = [pscustomobject]@{
        Name   = 'Plain'
        Fonts  = [pscustomobject]@{ Ui = 'Segoe UI'; UiSize = 9; Tile = 'Segoe UI'; TileSize = 13; Detail = 'Segoe UI'; DetailSize = 9; Mono = 'Consolas'; MonoSize = 9; Title = $null; TitleSize = 0 }
        # cc-P132: the named text colors, on Plain's white and light-grey backgrounds (the WinForms defaults).
        Text   = [pscustomobject]@{ Normal = @(0, 0, 0); Muted = @(96, 96, 96); Warning = @(140, 72, 0); Danger = @(170, 28, 28); Success = @(24, 110, 24); Link = @(0, 0, 128) }
        Plate  = $null
        Crystals = $false
        # A tile's state color: its background and its text. The meaning is the same in every theme.
        Tiles  = [ordered]@{
            green = [pscustomobject]@{ Back = @(222, 244, 226); Fore = @(20, 100, 40) }
            amber = [pscustomobject]@{ Back = @(255, 238, 204); Fore = @(150, 80, 0) }
            red   = [pscustomobject]@{ Back = @(250, 218, 218); Fore = @(150, 20, 20) }
            grey  = [pscustomobject]@{ Back = @(234, 234, 234); Fore = @(80, 80, 80) }
        }
        # The output, status and autopilot boxes: their background and each line color Write-ConsoleLine names.
        LineBack = @(255, 255, 255)
        Lines  = [ordered]@{
            normal = @(0, 0, 0); head = @(0, 0, 128); green = @(34, 139, 34); amber = @(255, 140, 0); red = @(178, 34, 34); gray = @(105, 105, 105)
        }
        # The window's own chrome. $null: the WinForms default (Plain draws none of it).
        Chrome = $null
        # Client gump art (read at start from the client data into the ignored cache; never in git). None for Plain.
        Gumps  = $null
        Header = $false
    }
    if ($Name -eq 'Plain') { return $plain }
    if ($Name -eq 'Enhanced') { return (Get-EnhancedTheme) }
    [pscustomobject]@{
        Name   = 'UO'
        # cc-P132: the named text colors on parchment (PageText, PageDim and PageWarn were already these roles), the
        # warning words on stone (on the backing, where the dark PageWarn could not be read: Chase, 10-16 07:34), and
        # the plate a confirm phrase sits on: the backing, solid, with the gold on it.
        Text   = [pscustomobject]@{ Normal = @(60, 38, 20); Muted = @(72, 54, 34); Warning = @(96, 48, 0); Danger = @(112, 16, 12); Success = @(12, 66, 18); Link = @(24, 40, 110) }
        Plate  = [pscustomobject]@{ Back = @(24, 22, 20); Text = @(246, 214, 120); Size = 14 }
        Crystals = $false
        # Palatino Linotype ships with Windows (Vista and later), is a book serif in the spirit of the UO client's
        # gump text, and stays legible at 9 to 10 points, where Book Antiqua's narrow strokes start to break up.
        # cc-P87: the boxes' Consolas up from 9 to 9.75, the tiles' buttons 9 bold (were 8.25), and the floors a tile's
        # title and detail may shrink to so that they fit their tile (Update-TileFit) rather than clip.
        Fonts  = [pscustomobject]@{ Ui = 'Palatino Linotype'; UiSize = 9.75; Tile = 'Palatino Linotype'; TileSize = 11.5; Detail = 'Palatino Linotype'; DetailSize = 9.75; Mono = 'Consolas'; MonoSize = 9.75; Title = 'Palatino Linotype'; TitleSize = 17; TileButtonSize = 9; TileFloor = 9; DetailFloor = 8.25 }
        Tiles  = [ordered]@{
            green = [pscustomobject]@{ Back = @(214, 232, 200); Fore = @(24, 84, 30) }
            amber = [pscustomobject]@{ Back = @(246, 226, 180); Fore = @(122, 62, 0) }
            red   = [pscustomobject]@{ Back = @(240, 206, 196); Fore = @(128, 20, 16) }
            grey  = [pscustomobject]@{ Back = @(220, 212, 196); Fore = @(70, 62, 52) }
        }
        LineBack = @(246, 238, 218)
        # cc-P87: green, amber and gray darkened to at least 7:1 on LineBack (were 6.30, 5.93 and 6.60): Consolas is thin,
        # and a colored line has to read as easily as a black one.
        Lines  = [ordered]@{
            normal = @(40, 28, 16); head = @(24, 40, 110); green = @(18, 86, 24); amber = @(120, 62, 0); red = @(150, 22, 18); gray = @(84, 74, 62)
        }
        Chrome = [pscustomobject]@{
            # cc-P87: a dark, semi-opaque backing behind every piece of text that sits on the stone (the header, the
            # Status and output groups and their bars). The art still shows through it, but its speckle no longer
            # does: over the stone's lightest pixel, 74,74,74, it is 35,33,32.
            Backing = @(24, 22, 20); BackingAlpha = 200
            # cc-P87: a disabled button is drawn by hand, flat and grey-brown with no gold, so it looks off and still reads.
            DisabledBack = @(72, 66, 58); DisabledText = @(190, 178, 156); DisabledBorder = @(120, 108, 90)
            # The frame and header: dark stone, with parchment-colored text and gold trim.
            StoneBack = @(46, 44, 42); StoneText = @(238, 226, 196); Gold = @(232, 196, 104)
            # Tab pages, captions and group boxes: parchment, with dark brown ink.
            PageBack = @(230, 206, 160); PageText = @(60, 38, 20); PageDim = @(72, 54, 34); PageWarn = @(112, 16, 12)
            # Gump-style buttons: dark wood, pale gold lettering, a gold border.
            ButtonBack = @(92, 70, 44); ButtonText = @(246, 226, 170); ButtonHover = @(118, 92, 58); ButtonBorder = @(232, 196, 104)
            # Text boxes and lists the person types into or reads from.
            FieldBack = @(250, 244, 228); FieldText = @(40, 28, 16)
            # cc-P132: warning and danger words drawn on the stone (under the backing): light, so they read on the dark.
            StoneWarn = @(255, 200, 120); StoneDanger = @(255, 160, 140)
        }
        # Gump IDs from the classic client's gumpartLegacyMUL.uop: 3604, the middle tile of the dark stone background
        # 3600-3608, behind the header and the window; 9304, the middle tile of the paper background 9300-9308, behind
        # the tab pages. Each tiles, so only the middle piece is needed. StoneBack and PageBack above are their
        # average colors (measured 2026-10-13: 44,44,44 and 228,202,155) and stand in when the art is missing.
        # Worst: the pixel of each that is closest to the text drawn on it (measured the same day), so the facts can
        # check that text on the real art keeps its contrast without the art being in git.
        # cc-P87: the lightest and darkest pixel of each (measured 2026-10-13 from the cached PNGs, single pixels and 3x3
        # and 6x6 averages; the single pixels are the extremes). Text is checked against both ends, not the average.
        Gumps  = [pscustomobject]@{ Stone = 3604; Parchment = 9304; StoneWorst = @(74, 74, 74); ParchmentWorst = @(222, 164, 131); StoneLight = @(74, 74, 74); StoneDark = @(16, 16, 16); ParchmentLight = @(246, 222, 172); ParchmentDark = @(222, 164, 131) }
        Header = $true
    }
}

function Get-EnhancedTheme {
    # cc-P132: the Enhanced Client look (Chase, 10-15 11:07: "more Enhanced Client, thinking runes/sigils, a glowing
    # golem when golem is working, dark when deactivated, spinning green red amber crystals for statuses"). Dark slate
    # stone drawn in code (Texture: a seeded speckle between StoneDark and StoneLight, so the contrast facts can check
    # text against its real extremes), bronze trim, slate tab pages with light type, a glow color per state, crystals
    # on every tile and a Golem figure. Art drops into scripts\console-art (Get-ArtSlots) a piece at a time.
    [pscustomobject]@{
        Name   = 'Enhanced'
        Fonts  = [pscustomobject]@{ Ui = 'Segoe UI'; UiSize = 9.75; Tile = 'Segoe UI'; TileSize = 11.5; Detail = 'Segoe UI'; DetailSize = 9.75; Mono = 'Consolas'; MonoSize = 9.75; Title = 'Palatino Linotype'; TitleSize = 17; TileButtonSize = 9; TileFloor = 9; DetailFloor = 8.25 }
        # Dark plates, light text, the hue in both. The same four meanings as every look.
        Tiles  = [ordered]@{
            green = [pscustomobject]@{ Back = @(30, 58, 38);  Fore = @(196, 246, 204) }
            amber = [pscustomobject]@{ Back = @(72, 52, 18);  Fore = @(255, 218, 136) }
            red   = [pscustomobject]@{ Back = @(76, 28, 28);  Fore = @(255, 176, 166) }
            grey  = [pscustomobject]@{ Back = @(48, 48, 54);  Fore = @(200, 200, 204) }
        }
        LineBack = @(24, 25, 29)
        # Every line color at 7:1 or better on LineBack (cc-P87's rule for thin Consolas).
        Lines  = [ordered]@{
            normal = @(232, 228, 218); head = @(160, 198, 255); green = @(140, 226, 150); amber = @(255, 202, 112); red = @(255, 156, 146); gray = @(180, 178, 172)
        }
        Text   = [pscustomobject]@{ Normal = @(234, 230, 220); Muted = @(188, 182, 170); Warning = @(255, 196, 118); Danger = @(255, 150, 140); Success = @(156, 232, 166); Link = @(156, 200, 255) }
        Plate  = [pscustomobject]@{ Back = @(14, 14, 17); Text = @(250, 214, 120); Size = 14 }
        Crystals = $true
        # A glow color per state: the crystal and the Golem figure are lit with it.
        Glow   = [ordered]@{ green = @(120, 255, 150); amber = @(255, 200, 90); red = @(255, 110, 100); grey = @(130, 130, 140); golem = @(255, 196, 96) }
        Chrome = [pscustomobject]@{
            Backing = @(14, 15, 18); BackingAlpha = 215
            DisabledBack = @(44, 44, 50); DisabledText = @(156, 156, 160); DisabledBorder = @(92, 92, 100)
            # The window: slate, with parchment-pale text and bronze trim (Gold is the trim color in every themed look).
            StoneBack = @(40, 42, 47); StoneText = @(228, 222, 210); Gold = @(220, 178, 98)
            # Tab pages: darker slate with light type (not parchment: the Enhanced Client's panels are dark).
            PageBack = @(50, 52, 58); PageText = @(234, 230, 220); PageDim = @(188, 182, 170); PageWarn = @(255, 196, 118)
            # Buttons: dark bronze, pale lettering, a bronze border.
            ButtonBack = @(70, 60, 48); ButtonText = @(244, 232, 200); ButtonHover = @(98, 86, 68); ButtonBorder = @(220, 178, 98)
            FieldBack = @(28, 29, 33); FieldText = @(228, 226, 216)
            StoneWarn = @(255, 200, 120); StoneDanger = @(255, 160, 140)
        }
        Gumps  = $null
        # The drawn textures (Get-ThemeImages): the stone's and the page's speckle stays inside these, measured, bounds.
        Texture = [pscustomobject]@{ StoneBase = @(40, 42, 47); StoneSpread = 10; StoneLight = @(50, 52, 57); StoneDark = @(30, 32, 37); PageBase = @(50, 52, 58); PageSpread = 6; PageLight = @(56, 58, 64); PageDark = @(44, 46, 52); Seed = 1327 }
        Header = $true
    }
}

function Get-RelativeLuminance {
    # WCAG 2.x relative luminance of an R,G,B triple (0 to 255 each).
    param([int[]]$Rgb)
    $ch = foreach ($v in $Rgb[0..2]) {
        $s = $v / 255.0
        if ($s -le 0.03928) { $s / 12.92 } else { [math]::Pow(($s + 0.055) / 1.055, 2.4) }
    }
    0.2126 * $ch[0] + 0.7152 * $ch[1] + 0.0722 * $ch[2]
}

function Get-ContrastRatio {
    # WCAG contrast ratio between two colors, 1 to 21. 4.5 is the floor for body text.
    param([int[]]$A, [int[]]$B)
    $la = Get-RelativeLuminance $A
    $lb = Get-RelativeLuminance $B
    ([math]::Max($la, $lb) + 0.05) / ([math]::Min($la, $lb) + 0.05)
}

function Get-BlendedColor {
    # cc-P87: what the eye sees where a color at Alpha (0 to 255) is painted over another: the backing over the stone.
    param([int[]]$Color, [int]$Alpha, [int[]]$Over)
    $a = $Alpha / 255.0
    @(0..2 | ForEach-Object { [int][math]::Round($a * $Color[$_] + (1 - $a) * $Over[$_]) })
}

# cc-P87: two pairs the console does not color itself, measured from screenshots on 2026-10-13 (Windows 11, 100%): a
# tooltip (Windows draws it: 87,87,87 on 249,249,249) and the DRY RUN box locked on under -DryRun (a disabled check
# box: Windows draws its text 128,94,15 on the amber the console gives it).
$script:MeasuredPairs = @(
    [pscustomobject]@{ Where = 'Tooltip text (drawn by Windows)'; Fore = @(87, 87, 87); Back = @(249, 249, 249); Kind = 'normal' },
    [pscustomobject]@{ Where = 'DRY RUN box, locked on (drawn disabled by Windows)'; Fore = @(128, 94, 15); Back = @(246, 226, 180); Kind = 'disabled' }
)

function Get-ThemeContrastPairs {
    # cc-P87: every text and background pair the UO look draws, each with the floor it must meet: 'normal' 4.5:1,
    # 'large' 3:1 (18 px and up, or 14 px and up in bold), 'disabled' 3:1. Text on the art is checked against the
    # art's lightest and darkest pixel, under the backing where there is one. Plain draws none of its own chrome.
    param($Theme)
    $c = $Theme.Chrome
    if (-not $c) { return @() }
    $gm = $Theme.Gumps
    $floor = @{ normal = 4.5; large = 3.0; disabled = 3.0 }
    $p = New-Object System.Collections.ArrayList
    $add = { param($w, $f, $b, $k) [void]$p.Add([pscustomobject]@{ Where = $w; Fore = $f; Back = $b; Kind = $k; Floor = $floor[$k]; Ratio = [math]::Round((Get-ContrastRatio $f $b), 2) }) }
    # cc-P132: the stone's extremes come from the client art (UO) or the drawn texture (Enhanced); the page's likewise.
    $stones = [ordered]@{}
    $pages = [ordered]@{}
    if ($gm) {
        $stones['stone art, lightest'] = $gm.StoneLight; $stones['stone art, darkest'] = $gm.StoneDark
        $pages['parchment art, lightest'] = $gm.ParchmentLight; $pages['parchment art, darkest'] = $gm.ParchmentDark
    }
    if ($Theme.Texture) {
        $stones['drawn stone, lightest'] = $Theme.Texture.StoneLight; $stones['drawn stone, darkest'] = $Theme.Texture.StoneDark
        $pages['drawn page, lightest'] = $Theme.Texture.PageLight; $pages['drawn page, darkest'] = $Theme.Texture.PageDark
    }
    $stones['stone, drawn (no art)'] = $c.StoneBack
    $pages['page, drawn (no art)'] = $c.PageBack
    foreach ($s in $stones.Keys) {
        $under = Get-BlendedColor $c.Backing $c.BackingAlpha $stones[$s]
        & $add ('Window title (gold, 17 pt bold) on backing over ' + $s) $c.Gold $under 'large'
        & $add ('Group titles (gold, bold) on backing over ' + $s) $c.Gold $under 'normal'
        & $add ('Options row and pane label text on backing over ' + $s) $c.StoneText $under 'normal'
        # cc-P132: warning and danger words on the stone (a caption, the DRY RUN box, a countdown) and on the bare stone too.
        & $add ('Warning words on backing over ' + $s) $c.StoneWarn $under 'normal'
        & $add ('Danger words on backing over ' + $s) $c.StoneDanger $under 'normal'
        & $add ('Stone text on bare ' + $s) $c.StoneText $stones[$s] 'normal'
        & $add ('Danger words on bare ' + $s) $c.StoneDanger $stones[$s] 'normal'
    }
    foreach ($s in $pages.Keys) {
        & $add ('Page text on ' + $s) $c.PageText $pages[$s] 'normal'
        & $add ('Page dim text on ' + $s) $c.PageDim $pages[$s] 'normal'
        & $add ('Page warning text on ' + $s) $c.PageWarn $pages[$s] 'normal'
        # cc-P132: the six named text colors, on the page.
        foreach ($k in 'Normal', 'Muted', 'Warning', 'Danger', 'Success', 'Link') { & $add ('Text.' + $k + ' on ' + $s) $Theme.Text.$k $pages[$s] 'normal' }
    }
    # cc-P132: the confirm phrase, large and bold on its plate (Show-TypedConfirm).
    if ($Theme.Plate) { & $add 'Confirm phrase (bold, 14 pt) on its plate' $Theme.Plate.Text $Theme.Plate.Back 'large' }
    & $add 'Button text on button' $c.ButtonText $c.ButtonBack 'normal'
    & $add 'Button text on button, hover' $c.ButtonText $c.ButtonHover 'normal'
    & $add 'Button text, disabled' $c.DisabledText $c.DisabledBack 'disabled'
    & $add 'Tab, not selected' $c.ButtonText $c.ButtonBack 'normal'
    & $add 'Tab, selected' $c.PageText $c.PageBack 'normal'
    & $add 'Tab, LIVE shard selected' $c.PageWarn $c.PageBack 'normal'
    & $add 'Field and list text' $c.FieldText $c.FieldBack 'normal'
    foreach ($k in $Theme.Lines.Keys) { & $add ('Status and output line, ' + $k) $Theme.Lines[$k] $Theme.LineBack 'normal' }
    foreach ($k in $Theme.Tiles.Keys) { & $add ('Tile, ' + $k) $Theme.Tiles[$k].Fore $Theme.Tiles[$k].Back 'normal' }
    foreach ($m in $script:MeasuredPairs) { & $add $m.Where $m.Fore $m.Back $m.Kind }
    $p.ToArray()
}

# --- cc-P132: art slots, the window state, and the rebuild and package lights (CORE: strings and facts in, objects out) ---

function Get-ArtSlots {
    # The art the console reads from scripts\console-art at start: one fixed file name per slot (PNG, or for an
    # animated slot a horizontal sprite strip PNG with its frames and fps in art.json, or an animated GIF). Size is
    # the displayed size at 100%; Budget is the file's size cap in KB (the folder stays far under the 5 MB rule:
    # the budgets sum to about 1.9 MB). A missing or bad file is drawn in code instead (New-FallbackImage).
    @(
        [pscustomobject]@{ Name = 'crystal-green';  File = 'crystal-green.png';  Kind = 'animated'; Size = 32;  Budget = 120; Purpose = 'the green crystal: running, healthy, latest' }
        [pscustomobject]@{ Name = 'crystal-amber';  File = 'crystal-amber.png';  Kind = 'animated'; Size = 32;  Budget = 120; Purpose = 'the amber crystal: warning, waiting, needs a rebuild' }
        [pscustomobject]@{ Name = 'crystal-red';    File = 'crystal-red.png';    Kind = 'animated'; Size = 32;  Budget = 120; Purpose = 'the red crystal: down, failed, blocked' }
        [pscustomobject]@{ Name = 'crystal-grey';   File = 'crystal-grey.png';   Kind = 'static';   Size = 32;  Budget = 60;  Purpose = 'the dull grey crystal: off, retired, unknown' }
        [pscustomobject]@{ Name = 'golem-running';  File = 'golem-running.png';  Kind = 'animated'; Size = 64;  Budget = 300; Purpose = 'the Golem, glowing: running a prompt' }
        [pscustomobject]@{ Name = 'golem-idle';     File = 'golem-idle.png';     Kind = 'static';   Size = 64;  Budget = 120; Purpose = 'the Golem, a soft glow: awake, nothing running' }
        [pscustomobject]@{ Name = 'golem-dark';     File = 'golem-dark.png';     Kind = 'static';   Size = 64;  Budget = 120; Purpose = 'the Golem, dark: resting or off' }
        [pscustomobject]@{ Name = 'rune-tab';       File = 'rune-tab.png';       Kind = 'static';   Size = 16;  Budget = 30;  Purpose = 'the rune before each tab header' }
        [pscustomobject]@{ Name = 'rune-corner';    File = 'rune-corner.png';    Kind = 'static';   Size = 16;  Budget = 30;  Purpose = 'the sigil in a tile corner' }
        [pscustomobject]@{ Name = 'spinner';        File = 'spinner.png';        Kind = 'animated'; Size = 24;  Budget = 100; Purpose = 'the working sigil: something is running' }
        [pscustomobject]@{ Name = 'header-band';    File = 'header-band.png';    Kind = 'static';   Size = 64;  Budget = 250; Purpose = 'the band behind the title, tiled across' }
        [pscustomobject]@{ Name = 'stone';          File = 'stone.png';          Kind = 'tile';     Size = 256; Budget = 200; Purpose = 'the window stone, tiled' }
        [pscustomobject]@{ Name = 'page';           File = 'page.png';           Kind = 'tile';     Size = 256; Budget = 200; Purpose = 'the tab pages, tiled' }
        [pscustomobject]@{ Name = 'icon-live';      File = 'icon-live.png';      Kind = 'static';   Size = 32;  Budget = 40;  Purpose = 'the LIVE tile icon' }
        [pscustomobject]@{ Name = 'icon-test';      File = 'icon-test.png';      Kind = 'static';   Size = 32;  Budget = 40;  Purpose = 'the TEST tile icon' }
        [pscustomobject]@{ Name = 'icon-publisher'; File = 'icon-publisher.png'; Kind = 'static';   Size = 32;  Budget = 40;  Purpose = 'the STATUS PUBLISHER tile icon' }
        [pscustomobject]@{ Name = 'icon-dns';       File = 'icon-dns.png';       Kind = 'static';   Size = 32;  Budget = 40;  Purpose = 'the PUBLIC DNS tile icon' }
        [pscustomobject]@{ Name = 'icon-golem';     File = 'icon-golem.png';     Kind = 'static';   Size = 32;  Budget = 40;  Purpose = 'the GOLEM tile icon' }
        [pscustomobject]@{ Name = 'icon-playtests'; File = 'icon-playtests.png'; Kind = 'static';   Size = 32;  Budget = 40;  Purpose = 'the PLAY TESTS tile icon' }
        [pscustomobject]@{ Name = 'icon-websites';  File = 'icon-websites.png';  Kind = 'static';   Size = 32;  Budget = 40;  Purpose = 'the WEBSITES tile icon' }
    )
}

function ConvertFrom-ArtManifest {
    # scripts\console-art\art.json: { "slots": { "crystal-green": { "frames": 12, "fps": 8 } } }. Only animated slots need
    # an entry, and only when the file is a sprite strip (a GIF carries its own timing). Missing or broken: no entries,
    # every strip is taken as one frame, and the reason comes back in Problem.
    param([string]$Text)
    $o = [pscustomobject]@{ Slots = @{}; Problem = $null }
    if (-not $Text) { return $o }
    try { $j = $Text | ConvertFrom-Json } catch { $o.Problem = 'art.json does not parse: ' + $_.Exception.Message; return $o }
    if (-not $j -or -not $j.slots) { return $o }
    foreach ($p in $j.slots.PSObject.Properties) {
        $frames = 1; $fps = 8
        if ($null -ne $p.Value.frames) { [void][int]::TryParse([string]$p.Value.frames, [ref]$frames) }
        if ($null -ne $p.Value.fps) { [void][int]::TryParse([string]$p.Value.fps, [ref]$fps) }
        if ($frames -lt 1) { $frames = 1 }
        if ($fps -lt 1 -or $fps -gt 60) { $fps = 8 }
        $o.Slots[[string]$p.Name] = [pscustomobject]@{ Frames = $frames; Fps = $fps }
    }
    $o
}

function Test-ArtFile {
    # One slot's file, judged from what the shell read (never a popup): Ok, or Why. A missing file is not an error
    # (Missing), it is the fallback. Bad: the wrong extension, over the slot's budget, unreadable, or too small to be
    # the piece; a strip whose width is not frames x height is bad too (the frames would tear).
    param($Slot, [bool]$Exists, [long]$Length = 0, [string]$Extension = '', [int]$Width = 0, [int]$Height = 0, [bool]$Readable = $true, [int]$Frames = 1)
    $r = [pscustomobject]@{ Ok = $false; Missing = (-not $Exists); Why = '' }
    if (-not $Exists) { return $r }
    $ext = $Extension.ToLowerInvariant()
    if ($ext -notin @('.png', '.gif')) { $r.Why = $Slot.File + ': not a PNG or GIF (' + $Extension + ')'; return $r }
    if ($ext -eq '.gif' -and $Slot.Kind -ne 'animated') { $r.Why = $Slot.File + ': a GIF in a static slot (use a PNG)'; return $r }
    if ($Length -gt ($Slot.Budget * 1024)) { $r.Why = $Slot.File + ': ' + [math]::Round($Length / 1024) + ' KB is over the slot''s ' + $Slot.Budget + ' KB budget'; return $r }
    if (-not $Readable) { $r.Why = $Slot.File + ': could not be read as an image'; return $r }
    if ($Width -lt 8 -or $Height -lt 8) { $r.Why = $Slot.File + ': ' + $Width + 'x' + $Height + ' is too small'; return $r }
    if ($Slot.Kind -eq 'animated' -and $ext -eq '.png' -and $Frames -gt 1 -and ($Width % $Frames) -ne 0) { $r.Why = $Slot.File + ': ' + $Width + ' px wide is not ' + $Frames + ' frames of equal width'; return $r }
    $r.Ok = $true
    $r
}

function ConvertFrom-WindowState {
    # The per-user window file (outside the repo): size, position, maximized, the two splitters and the pane's. Any
    # missing, wrong or broken value falls back to its default, so a bad file never stops the console.
    param([string]$Text)
    $s = [pscustomobject]@{ Width = 1200; Height = 1000; X = $null; Y = $null; Maximized = $false; Split1 = 0; Split2 = 0; PaneSplit = 0 }
    if (-not $Text) { return $s }
    try { $j = $Text | ConvertFrom-Json } catch { return $s }
    if (-not $j) { return $s }
    $int = { param($v, $min, $max) $n = 0; if ($null -ne $v -and [int]::TryParse([string]$v, [ref]$n) -and $n -ge $min -and $n -le $max) { return $n }; $null }
    $w = & $int $j.Width 900 20000; if ($null -ne $w) { $s.Width = $w }
    $h = & $int $j.Height 700 20000; if ($null -ne $h) { $s.Height = $h }
    $s.X = & $int $j.X -20000 20000
    $s.Y = & $int $j.Y -20000 20000
    if ($null -ne $j.Maximized) { $s.Maximized = ([string]$j.Maximized -eq 'True' -or [string]$j.Maximized -eq 'true') }
    foreach ($k in 'Split1', 'Split2', 'PaneSplit') { $v = & $int $j.$k 0 20000; if ($null -ne $v) { $s.$k = $v } }
    $s
}

function ConvertTo-WindowState {
    param($State)
    ([ordered]@{ Width = $State.Width; Height = $State.Height; X = $State.X; Y = $State.Y; Maximized = [bool]$State.Maximized; Split1 = $State.Split1; Split2 = $State.Split2; PaneSplit = $State.PaneSplit } | ConvertTo-Json)
}

function Resolve-WindowBounds {
    # Where the window goes: the saved place when at least 200x100 of it is on one of the screens (a monitor that was
    # unplugged is not a reason to open the console off screen), else centered on the first screen. $Screens are
    # rectangles (X, Y, Width, Height); the shell passes each screen's working area.
    param($State, [object[]]$Screens)
    $w = [math]::Max(900, [int]$State.Width); $h = [math]::Max(700, [int]$State.Height)
    $first = $null
    if ($Screens -and @($Screens).Count) { $first = @($Screens)[0] }
    if ($first) { $w = [math]::Min($w, [int]$first.Width); $h = [math]::Min($h, [int]$first.Height) }
    $onScreen = $false
    if ($null -ne $State.X -and $null -ne $State.Y) {
        foreach ($sc in @($Screens)) {
            $ix = [math]::Min([int]$State.X + $w, [int]$sc.X + [int]$sc.Width) - [math]::Max([int]$State.X, [int]$sc.X)
            $iy = [math]::Min([int]$State.Y + $h, [int]$sc.Y + [int]$sc.Height) - [math]::Max([int]$State.Y, [int]$sc.Y)
            if ($ix -ge 200 -and $iy -ge 100) { $onScreen = $true; break }
        }
    }
    if ($onScreen) { return [pscustomobject]@{ X = [int]$State.X; Y = [int]$State.Y; Width = $w; Height = $h; Placed = $true } }
    $x = 0; $y = 0
    if ($first) { $x = [int]$first.X + [int](([int]$first.Width - $w) / 2); $y = [int]$first.Y + [int](([int]$first.Height - $h) / 2) }
    [pscustomobject]@{ X = $x; Y = $y; Width = $w; Height = $h; Placed = $false }
}

# The paths whose commits change what the test shard runs (section 6) and what players get (section 7). A commit
# counts when any file it touched starts with one of these.
$script:ServerPaths = @('server/', 'docker/uo/', 'CHANGELOG.md', 'VERSION')
$script:ShipPaths = @('player-package/', 'client/patches/', 'TAZUO_COMMIT', 'client/Build-Client.ps1')

function ConvertFrom-GitLogWithFiles {
    # `git log --format=%H|%h|%s --name-only <from>..HEAD`: blocks of one header line and the files it touched, blank
    # line between. Newest first, as git prints them.
    param([string]$Text)
    $out = New-Object 'System.Collections.Generic.List[object]'
    $cur = $null
    foreach ($raw in @(([string]$Text) -split "`n")) {
        $l = $raw.TrimEnd("`r")
        if ($l -match '^([0-9a-f]{40})\|([0-9a-f]{4,})\|(.*)$') {
            $cur = [pscustomobject]@{ Sha = $Matches[1]; Short = $Matches[2]; Subject = $Matches[3].Trim(); Files = @(); Prompt = '' }
            if ($cur.Subject -match '\bP(\d+)\b') { $cur.Prompt = 'P' + $Matches[1] }
            $out.Add($cur)
        } elseif ($cur -and $l.Trim()) { $cur.Files += $l.Trim() }
    }
    $out.ToArray()
}

function Select-CommitsTouching {
    # The commits (from ConvertFrom-GitLogWithFiles) that touch any of $Paths, with the files that did.
    param([object[]]$Commits, [string[]]$Paths)
    @(foreach ($c in @($Commits)) {
        $hit = @($c.Files | Where-Object { $f = $_; @($Paths | Where-Object { $f -eq $_ -or $f.StartsWith($_) }).Count })
        if ($hit.Count) { [pscustomobject]@{ Sha = $c.Sha; Short = $c.Short; Subject = $c.Subject; Prompt = $c.Prompt; Files = $hit } }
    })
}

function Format-CommitPrompts {
    # "3 commits: P127, P128, P129" (a commit with no P number is named by its short hash).
    param([object[]]$Commits)
    $n = @($Commits).Count
    $names = @($Commits | ForEach-Object { if ($_.Prompt) { $_.Prompt } else { $_.Short } } | Select-Object -Unique)
    [string]$n + ' commit' + $(if ($n -ne 1) { 's' } else { '' }) + $(if ($names.Count) { ': ' + ($names -join ', ') } else { '' })
}

function Get-RebuildLight {
    # Section 6: does the test shard need a rebuild? Green: built from HEAD, or nothing since its build touches the
    # server. Amber: server commits since the build and a rebuild can go now. Red: it is needed but blocked (a build
    # already running, the Golem running a prompt, uncommitted changes). Grey: the running image's commit is unknown
    # (no sl.commit label: built before cc-P96) or there is no running shard. Hover lists the commits and files.
    param([string]$ImageCommit, [bool]$ImageDirty, [string]$Head, [object[]]$Commits, [string]$GolemRunning, [int]$Uncommitted, [bool]$BuildRunning, [bool]$Running = $true, [string[]]$Paths = $script:ServerPaths)
    $o = [pscustomobject]@{ Color = 'grey'; Word = 'UNKNOWN'; Detail = ''; Commits = @(); Hover = '' }
    if (-not $Running) { $o.Detail = 'no running test shard to compare'; return $o }
    if (-not $ImageCommit) { $o.Detail = 'the running image has no sl.commit label (built before cc-P96): rebuild once to get one'; return $o }
    $server = @(Select-CommitsTouching $Commits $Paths)
    $o.Commits = $server
    $hover = @($server | ForEach-Object { $_.Short + ' ' + $_.Subject + "`n    " + ($_.Files -join "`n    ") })
    $o.Hover = $(if ($hover.Count) { 'Server changes since the running build (' + $ImageCommit.Substring(0, 7) + '):' + "`n" + ($hover -join "`n") } else { 'No server change since the running build (' + $ImageCommit.Substring(0, 7) + ').' })
    $dirtyNote = $(if ($ImageDirty) { '; the running image was built with uncommitted changes' } else { '' })
    if (-not $server.Count) {
        $o.Color = 'green'; $o.Word = 'LATEST'
        $all = @($Commits).Count
        $o.Detail = $(if ($Head -and $ImageCommit -eq $Head) { 'built from HEAD' } elseif ($all) { [string]$all + ' commit' + $(if ($all -ne 1) { 's' } else { '' }) + ' since the build, none touch the server' } else { 'built from HEAD' }) + $dirtyNote
        return $o
    }
    $what = Format-CommitPrompts $server
    if ($BuildRunning) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'rebuild needed (' + $what + ') but a build is already running'; return $o }
    if ($GolemRunning) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'rebuild needed (' + $what + ') but the Golem is running ' + $GolemRunning; return $o }
    if ($Uncommitted -gt 0) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'rebuild needed (' + $what + ') but ' + $Uncommitted + ' uncommitted file(s) in the code repo: commit first'; return $o }
    $o.Color = 'amber'; $o.Word = 'REBUILD'
    $o.Detail = $what + ' touch the server since the running build: Rebuild can go now' + $dirtyNote
    $o
}

function Get-PackageLight {
    # Section 7: does the player package need a build or a publish? Green: the published package was built from the
    # newest commit that changes what players get. Amber: "changes since the published package: Build package" (the
    # commits), or "built, not published: Publish package" when the newest zip already holds them. Red, with the reason:
    # the Golem running, uncommitted changes, a stale client, a build running, or the download site unreachable.
    param([string]$PublishedVersion, [string]$PublishedCommit, [string]$NewestZipVersion, [string]$NewestZipCommit, [string]$Head, [object[]]$Commits, [object[]]$CommitsSinceZip,
        [string]$GolemRunning, [int]$Uncommitted, [string]$ClientStale, [bool]$BuildRunning, [bool]$SiteReachable = $true, [string]$SiteError = '', [string[]]$Paths = $script:ShipPaths)
    $o = [pscustomobject]@{ Color = 'grey'; Word = 'UNKNOWN'; Detail = ''; Commits = @(); Hover = '' }
    if (-not $SiteReachable) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'the download site could not be reached to compare' + $(if ($SiteError) { ' (' + $SiteError + ')' } else { '' }); return $o }
    if (-not $PublishedVersion) { $o.Detail = 'no published package (version.json has no version)'; return $o }
    if (-not $PublishedCommit) { $o.Detail = 'published ' + $PublishedVersion + ', but no zip in dist\ says what it was built from (' + $PublishedVersion + ')'; return $o }
    if ($PublishedCommit -match 'uncommitted') { $o.Color = 'amber'; $o.Word = 'BUILD'; $o.Detail = 'the published package ' + $PublishedVersion + ' was built from uncommitted changes (its VERSION.txt says so): Build package from a commit'; return $o }
    $ship = @(Select-CommitsTouching $Commits $Paths)
    $o.Commits = $ship
    $hover = @($ship | ForEach-Object { $_.Short + ' ' + $_.Subject + "`n    " + ($_.Files -join "`n    ") })
    $o.Hover = $(if ($hover.Count) { 'Player-facing changes since the published package (' + $PublishedVersion + ', ' + $PublishedCommit.Substring(0, [math]::Min(7, $PublishedCommit.Length)) + '):' + "`n" + ($hover -join "`n") } else { 'Nothing player-facing has changed since the published package (' + $PublishedVersion + ').' })
    if (-not $ship.Count) { $o.Color = 'green'; $o.Word = 'LATEST'; $o.Detail = 'published ' + $PublishedVersion + ' holds every player-facing change'; return $o }
    $what = Format-CommitPrompts $ship
    if ($BuildRunning) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'changes since the published package (' + $what + ') but a build is already running'; return $o }
    if ($GolemRunning) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'changes since the published package (' + $what + ') but the Golem is running ' + $GolemRunning; return $o }
    if ($Uncommitted -gt 0) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'changes since the published package (' + $what + ') but ' + $Uncommitted + ' uncommitted file(s) in the code repo: commit first'; return $o }
    if ($ClientStale) { $o.Color = 'red'; $o.Word = 'BLOCKED'; $o.Detail = 'changes since the published package (' + $what + ') but the client is stale: ' + $ClientStale; return $o }
    # Does the newest zip already hold them? Only when it was built from a commit no shipping change follows.
    $sinceZip = @(Select-CommitsTouching $CommitsSinceZip $Paths)
    if ($NewestZipCommit -and $NewestZipCommit -notmatch 'uncommitted' -and $NewestZipVersion -ne $PublishedVersion -and -not $sinceZip.Count) {
        $o.Color = 'amber'; $o.Word = 'PUBLISH'; $o.Detail = 'built, not published: Publish package (' + $NewestZipVersion + ' holds ' + $what + ')'; return $o
    }
    $o.Color = 'amber'; $o.Word = 'BUILD'; $o.Detail = 'changes since the published package: Build package (' + $what + ')'
    $o
}

# --- cc-P153: the suggested "What is new" line for Build package ----------------------------------------------------
# From what changed for players since the published package: the Added, Changed and Fixed lines of the CHANGELOG.md
# versions that are new since then (CHANGELOG.md at the published commit against HEAD's), and the package-shipping
# commits (the package light's, Select-CommitsTouching $script:ShipPaths). Staff lines never; no version or prompt
# numbers. Pure: text in, a line and its sources out.

$script:WhatsNewMax = 120
# A line's first word is lowered inside the sentence only when it is an ordinary word, never a name (Rootdeep, TazUO).
$script:WhatsNewLowerFirst = '^(The|A|An|Your|You|Our|New|Each|Every|All|Players?|Pets?|Monsters?|Vendors?|Crafters?)\b'

function ConvertFrom-ChangelogItems {
    # Every "- " line of CHANGELOG.md with its version and list ('Added', 'Changed', 'Fixed', 'Staff'). The comment at
    # the top is skipped, as the server's parse skips it.
    param([string]$Text)
    $out = New-Object 'System.Collections.Generic.List[object]'
    $ver = ''; $list = ''; $inComment = $false
    foreach ($raw in @(([string]$Text) -split "`r?`n")) {
        $l = $raw.TrimEnd()
        if ($inComment) { if ($l -match '-->') { $inComment = $false }; continue }
        if ($l -match '^\s*<!--') { if ($l -notmatch '-->') { $inComment = $true }; continue }
        if ($l -match '^##\s+(\S+)\s*$' -and $l -notmatch '^###') { $ver = $Matches[1]; $list = ''; continue }
        if ($l -match '^###\s+(\w+)') { $list = $Matches[1]; continue }
        if ($ver -and $list -and $l -match '^-\s+(.+)$') { $out.Add([pscustomobject]@{ Version = $ver; List = $list; Text = $Matches[1].Trim() }) }
    }
    $out.ToArray()
}

function Get-WhatsNewPhrase {
    # A player line cut to its lead phrase (its first sentence, before a colon, semicolon or bracket; a long one at its
    # first comma), with version and prompt numbers taken out. '' when fewer than three words are left.
    param([string]$Text)
    $t = [string]$Text
    $t = $t -replace '\(?\b(?:cc-)?P\d{2,4}\b\)?', '' -replace '\b\d{4}\.\d{2}\.\d{2}(?:\.\d+)?\b', '' -replace '`', ''
    $t = ($t -split '(?<=[a-z0-9)])\.\s+')[0]
    $t = ($t -split '\s*[:;(]\s*|\s+-{1,2}\s+')[0]
    if ($t.Length -gt 60 -and $t.Contains(', ')) { $t = $t.Substring(0, $t.IndexOf(', ')) }
    $t = ($t -replace '\s{2,}', ' ').Trim().TrimEnd('.', ',')
    # A lead of one or two words ("Leads", "Imbuing table") says nothing on its own.
    if (@($t -split '\s+' | Where-Object { $_ }).Count -lt 3) { return '' }
    $t
}

function Get-PackageShipPhrase {
    # What a package-shipping commit gives players, from the paths it touched (cc-P288: only for a commit with neither a
    # prompt title nor a player line of CHANGELOG.md, such as a hand commit).
    param([string[]]$Files)
    $f = @($Files)
    if (@($f | Where-Object { $_ -match '^client/|TAZUO_COMMIT' }).Count) { return 'an updated game client' }
    if (@($f | Where-Object { $_ -match '(?i)shattered-legacy-map/' }).Count) { return 'map changes' }
    if (@($f | Where-Object { $_ -match '(?i)shattered-legacy-music/' }).Count) { return 'new music' }
    if (@($f | Where-Object { $_ -match '(?i)launcher' }).Count) { return 'launcher updates' }
    if (@($f | Where-Object { $_ -match '(?i)\.(png|gif|bmp|mul|uop|vd)$|art|gump' }).Count) { return 'new art in the package' }
    'package updates'
}

# --- cc-P288: the What is new line, made per commit that changes what players get -------------------------------------
# A commit counts when it touches a path the package ships ($script:WhatsNewKinds; tests, SOURCE.txt and .md files never
# ship). The CHANGELOG.md lines it added say whether players see it: Staff lines only, and it is left out. Its prompt
# title gives the short name ("CC task P236: Ship Chase's approved satchel and guild bag icons" is "satchel and guild bag
# icons"); a commit with no prompt title takes its first player line's lead phrase. A server-only commit counts only when
# a player line it added says it reaches the player's install ($script:WhatsNewMarked). Biggest first: the kind of thing
# shipped, then the player lines added (Added 3, Changed 2, Fixed 1), then files, then newest. Pure from here to
# Get-PackageSuggestion; Get-WhatsNewFacts reads git.
$script:WhatsNewNotShipped = '(^|/)tests/|(^|/)SOURCE\.txt$|\.md$'
$script:WhatsNewMarked = '(?i)\b(player package|launcher|update your game)\b'
$script:WhatsNewKinds = @(
    @{ Kind = 'map'; Weight = 6; Pattern = '^player-package/vendor/shattered-legacy-map/' },
    @{ Kind = 'client'; Weight = 5; Pattern = '^client/(patches/|Build-Client\.ps1$|TAZUO_COMMIT$)|^TAZUO_COMMIT$' },
    @{ Kind = 'bodies'; Weight = 4; Pattern = '^player-package/vendor/shattered-legacy-art/anim-src/' },
    @{ Kind = 'art'; Weight = 3; Pattern = '^player-package/vendor/(shattered-legacy-art|paperdoll-sos|fiddle-me-this)/' },
    @{ Kind = 'music'; Weight = 3; Pattern = '^player-package/vendor/shattered-legacy-music/' },
    @{ Kind = 'text'; Weight = 2; Pattern = '^player-package/vendor/shattered-legacy-cliloc/' },
    @{ Kind = 'launcher'; Weight = 1; Pattern = '^player-package/' }
)
$script:WhatsNewPoints = @{ Added = 3; Changed = 2; Fixed = 1 }

function Get-WhatsNewShipKind {
    # The biggest kind of thing $Files ship, as @{ Kind; Weight; Files }, or $null when none of them ships.
    param([string[]]$Files)
    $best = $null; $n = 0
    foreach ($f in @($Files)) {
        if (-not $f -or $f -match $script:WhatsNewNotShipped) { continue }
        foreach ($k in $script:WhatsNewKinds) {
            if ($f -match $k.Pattern) { $n++; if (-not $best -or $k.Weight -gt $best.Weight) { $best = $k }; break }
        }
    }
    if (-not $best) { return $null }
    [pscustomobject]@{ Kind = $best.Kind; Weight = $best.Weight; Files = $n }
}

function Get-ChangelogItemsAdded {
    # The CHANGELOG.md lines $After has and $Before does not (by list and text): what one commit added.
    param([string]$Before, [string]$After)
    $had = @{}
    foreach ($i in @(ConvertFrom-ChangelogItems $Before)) { $had[$i.List + '|' + $i.Text] = $true }
    @(ConvertFrom-ChangelogItems $After | Where-Object { -not $had.ContainsKey($_.List + '|' + $_.Text) })
}

function Get-PromptTitleName {
    # A commit's prompt title cut to a short name, '' for a commit that is not a CC task. A fix-up names the prompt it
    # fixes; brackets, prompt and F- numbers, and what follows a colon or the first comma go; so do a leading verb
    # (port, add, ship ...) and article, and a closing "goes in".
    param([string]$Subject)
    if ([string]$Subject -notmatch '^(?:Merge \S+: )?CC task P\d+:\s*(.+)$') { return '' }
    $t = $Matches[1]
    if ($t -match '^fix-up for P\d+ \(CC task P\d+:\s*([^)]*)') { $t = $Matches[1] }
    $t = $t -replace '\([^)]*\)?', '' -replace '\b(?:cc-)?P\d{2,4}(?:-D\d+)?\b', '' -replace '\bF-\d+\b', ''
    $t = ($t -split '\s*[:;]\s*|\s+-{1,2}\s+')[0]
    $t = ($t -split ',\s*')[0]
    $t = ($t -replace '\s{2,}', ' ').Trim().TrimEnd('.', ',')
    $t = $t -replace '(?i)^(?:port|ports|add|adds|added|ship|ships|build|builds|bring|brings|make|makes|fix|fixes|give|gives)\s+', ''
    do { $was = $t; $t = $t -replace "(?i)^(?:the|a|an|our|OSI's|Chase's|approved)\s+", '' } while ($t -ne $was)
    $t = $t -replace '(?i)\s+(?:goes|go|comes|come)\s+(?:in|out|back)$', ''
    $t.Trim()
}

function Get-WhatsNewShortPhrase {
    # A CHANGELOG line's short name, when there is no prompt title: its lead phrase (Get-WhatsNewPhrase), at most 50
    # characters, cut at a word that is not a small one.
    param([string]$Text)
    $t = Get-WhatsNewPhrase $Text
    if (-not $t) { $t = ((@(([string]$Text -replace '\b(?:cc-)?P\d{2,4}\b', '') -split '\s+' | Where-Object { $_ }) | Select-Object -First 6) -join ' ').TrimEnd('.', ',', ':', ';') }
    if ($t.Length -gt 50) {
        $t = $t.Substring(0, 50); if ($t.Contains(' ')) { $t = $t.Substring(0, $t.LastIndexOf(' ')) }
        # A cut never ends on a small word ("... alone until you").
        do { $was = $t; $t = $t -replace '(?i)[\s,]+(?:a|an|and|at|by|for|from|in|it|of|on|or|the|to|until|when|with|you|your)$', '' } while ($t -ne $was)
    }
    $t.TrimEnd(',', ' ')
}

function Format-WhatsNewName {
    # A name as it sits inside the line: plain ASCII (launchers print it in a PowerShell 5.1 console), its first word
    # lowered when it is an ordinary word (one $Corpus, CHANGELOG.md, also writes in lower case), never a name: "New
    # Haven" (two capitals) and "Rootdeep" stay. A one-word name that ships a map gets " map".
    param([string]$Name, [string]$Corpus, [string]$Kind)
    $t = [string]$Name
    $t = $t -replace '[\u2018\u2019]', "'" -replace '[\u201C\u201D]', '"' -replace '[\u2013\u2014]', '-' -replace '[^\x20-\x7E]', ''
    $t = ($t -replace '\s{2,}', ' ').Trim()
    $w = @($t -split ' ' | Where-Object { $_ })
    if ($Kind -eq 'map' -and $w.Count -eq 1) { $t += ' map'; $w += 'map' }
    if (-not $w.Count -or $w[0] -cnotmatch '^[A-Z][a-z]+$') { return $t }
    if ($w.Count -gt 1 -and $w[1] -cmatch '^[A-Z]') { return $t }
    $low = $w[0].ToLower()
    if ($t -cmatch $script:WhatsNewLowerFirst -or ($Corpus -and $Corpus -cmatch ('\b' + [regex]::Escape($low) + '\b'))) { return $low + $t.Substring($w[0].Length) }
    $t
}

function Join-WhatsNew {
    # "a, b, c": as many as fit in $Max, biggest first; " and more" when some are left out and it still fits. The first
    # letter is raised.
    param([string[]]$Phrases, [int]$Max = $script:WhatsNewMax)
    $take = New-Object 'System.Collections.Generic.List[string]'
    foreach ($p in @($Phrases)) {
        if (-not $p) { continue }
        if (((@($take) + @($p)) -join ', ').Length -le $Max) { $take.Add($p) }
    }
    $line = (@($take) -join ', ')
    if ($take.Count -lt @($Phrases | Where-Object { $_ }).Count -and $take.Count) {
        $more = $line + ' and more'
        while ($more.Length -gt $Max -and $take.Count -gt 1) { $take.RemoveAt($take.Count - 1); $more = (@($take) -join ', ') + ' and more' }
        if ($more.Length -le $Max) { $line = $more }
    }
    if ($line) { $line = $line.Substring(0, 1).ToUpper() + $line.Substring(1) }
    $line
}

function Get-PackageWhatsNew {
    # The suggested What is new line, the fuller list (one line per item, biggest first) and what was left out.
    #   $Version         the published package's version ("No client changes since <version>")
    #   $Commits         ConvertFrom-GitLogWithFiles of <published commit>..HEAD, newest first
    #   $AddedBy         sha -> the CHANGELOG.md lines that commit added (Get-ChangelogItemsAdded)
    #   $ChangelogThen   CHANGELOG.md at the published commit, and $ChangelogNow at HEAD (marked server-only lines)
    #   $Scripts         the official Legion scripts changed since then (file names)
    param([string]$Version, [object[]]$Commits, [hashtable]$AddedBy = @{}, [string]$ChangelogThen = '', [string]$ChangelogNow = '',
        [string[]]$Scripts = @(), [int]$Max = $script:WhatsNewMax)
    if ($null -eq $AddedBy) { $AddedBy = @{} }
    $items = New-Object 'System.Collections.Generic.List[object]'
    $left = New-Object 'System.Collections.Generic.List[string]'
    $claimed = @{}
    $byName = @{}
    $order = 0
    $addItem = {
        param($Name, $Kind, $Weight, $Score, $Files, $From, $Lead)
        $key = $Name.ToLower()
        if ($byName.ContainsKey($key)) {
            $it = $byName[$key]
            if ($Weight -gt $it.Weight) { $it.Weight = $Weight; $it.Kind = $Kind }
            $it.Score += $Score; $it.Files += $Files; $it.From = @($it.From) + @($From)
            if (-not $it.Lead -and $Lead) { $it.Lead = $Lead }
            return
        }
        $it = [pscustomobject]@{ Name = $Name; Kind = $Kind; Weight = $Weight; Score = $Score; Files = $Files; Order = $items.Count; From = @($From); Lead = $Lead }
        $byName[$key] = $it; $items.Add($it)
    }
    foreach ($c in @($Commits)) {
        $kind = Get-WhatsNewShipKind $c.Files
        if (-not $kind) { continue }
        $added = @(if ($AddedBy.ContainsKey([string]$c.Sha)) { @($AddedBy[[string]$c.Sha]) | Where-Object { $_ } })
        foreach ($a in $added) { $claimed[$a.Text] = $true }
        $player = @($added | Where-Object { $_.List -in @('Added', 'Changed', 'Fixed') })
        $label = $(if ($c.Prompt) { [string]$c.Prompt } else { [string]$c.Short })
        $title = Get-PromptTitleName $c.Subject
        if ($added.Count -and -not $player.Count) { $left.Add('Left out, staff only in CHANGELOG.md: ' + $label + ' ' + $(if ($title) { $title } else { $c.Subject })); continue }
        $name = $title
        if (-not $name -and $player.Count) { $name = Get-WhatsNewShortPhrase $player[0].Text }
        if (-not $name) { $name = Get-PackageShipPhrase $c.Files }
        $name = Format-WhatsNewName $name $ChangelogNow $kind.Kind
        if (-not $name) { continue }
        $score = 0; foreach ($p in $player) { $score += $script:WhatsNewPoints[$p.List] }
        $lead = $(if ($player.Count) { Get-WhatsNewPhrase $player[0].Text } else { '' })
        & $addItem $name $kind.Kind $kind.Weight $score $kind.Files $label $lead
    }
    # Server-only lines that say they reach the install (a line some shipping commit added is already counted).
    $had = @{}
    foreach ($i in @(ConvertFrom-ChangelogItems $ChangelogThen)) { $had[$i.List + '|' + $i.Text] = $true }
    foreach ($i in @(ConvertFrom-ChangelogItems $ChangelogNow)) {
        if ($i.List -notin @('Added', 'Changed', 'Fixed') -or $had.ContainsKey($i.List + '|' + $i.Text) -or $claimed.ContainsKey($i.Text) -or $i.Text -notmatch $script:WhatsNewMarked) { continue }
        $name = Format-WhatsNewName (Get-WhatsNewShortPhrase $i.Text) $ChangelogNow 'server'
        if (-not $name) { continue }
        # The server half of something a shipping commit already names ("the world map now has a fog of war" and the
        # client's "fog of war") is that item, not a second one.
        $low = $name.ToLower()
        $same = @($items | Where-Object { $n = $_.Name.ToLower(); $low.Contains($n) -or $n.Contains($low) })
        if ($same.Count) { $same[0].Score += $script:WhatsNewPoints[$i.List]; $same[0].From = @($same[0].From) + @('CHANGELOG ' + $i.Version); if (-not $same[0].Lead) { $same[0].Lead = Get-WhatsNewPhrase $i.Text }; continue }
        & $addItem $name 'server' 1 $script:WhatsNewPoints[$i.List] 0 ('CHANGELOG ' + $i.Version) (Get-WhatsNewPhrase $i.Text)
    }
    $sc = @($Scripts | Where-Object { $_ })
    if ($sc.Count) { & $addItem 'updated Legion scripts' 'scripts' 1 0 $sc.Count 'official scripts' ($sc -join ', ') }
    $sorted = @($items | Sort-Object @{ Expression = 'Weight'; Descending = $true }, @{ Expression = 'Score'; Descending = $true }, @{ Expression = 'Files'; Descending = $true }, @{ Expression = 'Order'; Descending = $false })
    $list = @($sorted | ForEach-Object { $_.Name + ' (' + (@($_.From | Select-Object -Unique) -join ', ') + ', ' + $_.Kind + ')' + $(if ($_.Lead) { ': ' + $_.Lead } else { '' }) })
    $line = $(if ($sorted.Count) { Join-WhatsNew @($sorted | ForEach-Object { $_.Name }) $Max } elseif ($Version) { 'No client changes since ' + $Version } else { 'No client changes since the published package' })
    [pscustomobject]@{ Line = $line; Items = $sorted; List = $list; LeftOut = $left.ToArray(); Sources = @($list) + @($left.ToArray()); Empty = (-not $sorted.Count); Version = $Version }
}

function Resolve-PackageBaseCommit {
    # The commit a published package was built from: its zip's VERSION.txt when dist\ still holds the zip ('zip'), else
    # the newest commit on HEAD's first-parent line at or before the version's own build time ('time': the version is
    # yyyy.MM.dd.HHmm in the building PC's local time, this PC's). $FirstParentLog: git log --first-parent
    # --format=%H|%ct, newest first. $null when neither can say.
    param([string]$Version, [string]$ZipCommit, [string]$FirstParentLog)
    if ($ZipCommit -and $ZipCommit -notmatch 'uncommitted') { return [pscustomobject]@{ Sha = (($ZipCommit.Trim()) -split '\s+')[0]; How = 'zip' } }
    if ($Version -notmatch '^(\d{4})\.(\d{2})\.(\d{2})\.(\d{2})(\d{2})$') { return $null }
    try { $at = New-Object DateTime ([int]$Matches[1]), ([int]$Matches[2]), ([int]$Matches[3]), ([int]$Matches[4]), ([int]$Matches[5]), 59, ([DateTimeKind]::Local) } catch { return $null }
    $epoch = [long](($at.ToUniversalTime() - (New-Object DateTime 1970, 1, 1, 0, 0, 0, ([DateTimeKind]::Utc))).TotalSeconds)
    foreach ($raw in @(([string]$FirstParentLog) -split "`n")) {
        if ($raw.Trim() -match '^([0-9a-f]{40})\|(\d+)$' -and [long]$Matches[2] -le $epoch) { return [pscustomobject]@{ Sha = $Matches[1]; How = 'time' } }
    }
    $null
}

function Select-OfficialScriptTags {
    # The official Legion scripts tags (for-each-ref --format=%(refname:short)|%(creatordate:unix)) around a time: the
    # newest at or before it (From, '' when none) and the newest after it (To, '' when none is newer).
    param([string]$TagText, [long]$Since)
    $tags = @(foreach ($raw in @(([string]$TagText) -split "`n")) { if ($raw.Trim() -match '^(official-\S+)\|(\d+)$') { [pscustomobject]@{ Tag = $Matches[1]; At = [long]$Matches[2] } } })
    $from = @($tags | Where-Object { $_.At -le $Since } | Sort-Object At | Select-Object -Last 1)
    $to = @($tags | Where-Object { $_.At -gt $Since } | Sort-Object At | Select-Object -Last 1)
    [pscustomobject]@{ From = $(if ($from.Count) { $from[0].Tag } else { '' }); To = $(if ($to.Count) { $to[0].Tag } else { '' }) }
}

function Get-PackageSuggestion {
    # The suggested What is new line from the package light's facts ($Facts.WhatsNew, Get-WhatsNewFacts), or $null when
    # they cannot say (no published version, or no commit for it).
    param($Facts)
    $w = $(if ($Facts) { $Facts.WhatsNew })
    if (-not $w -or -not $w.Base) { return $null }
    $s = Get-PackageWhatsNew -Version $w.Version -Commits @($w.Commits) -AddedBy $w.AddedBy -ChangelogThen ([string]$w.ChangelogThen) -ChangelogNow ([string]$w.ChangelogNow) -Scripts @($w.Scripts)
    $s | Add-Member -NotePropertyName Base -NotePropertyValue $w.Base
    $s | Add-Member -NotePropertyName BaseHow -NotePropertyValue $w.BaseHow
    $s
}

function ConvertTo-WhatsNewDocument {
    # cc-P288: the suggestion as the web console's Player package page takes it (Get-WebConsoleList.ps1 -Page package):
    # the line, the fuller list, what was left out, and what it was compared with. $Note says why there is none.
    param($Suggestion, [string]$Note = '')
    if (-not $Suggestion) { return [ordered]@{ line = ''; list = @(); leftOut = @(); version = ''; base = ''; baseHow = ''; note = $(if ($Note) { $Note } else { 'the published package could not be read' }) } }
    [ordered]@{ line = [string]$Suggestion.Line; list = @($Suggestion.List | ForEach-Object { [string]$_ }); leftOut = @($Suggestion.LeftOut | ForEach-Object { [string]$_ })
        version = [string]$Suggestion.Version; base = ([string]$Suggestion.Base).Substring(0, [math]::Min(7, ([string]$Suggestion.Base).Length)); baseHow = [string]$Suggestion.BaseHow; note = '' }
}

function Format-WhatsNewList {
    # cc-P288: the box under the notes: what the suggested line was made from, one line per item, biggest first, then
    # what was left out. $Note says why there is no suggestion.
    param($Suggestion, [string]$Note = '')
    if (-not $Suggestion) { return ('No suggestion: ' + $(if ($Note) { $Note } else { 'the published package could not be read' }) + ' (see the package light).') }
    $b = ([string]$Suggestion.Base).Substring(0, [math]::Min(7, ([string]$Suggestion.Base).Length))
    $head = 'What changed for players since ' + $Suggestion.Version + ' (commit ' + $b + $(if ($Suggestion.BaseHow -eq 'time') { ', found by its build time: no zip in dist\ names its commit' } else { '' }) + '), biggest first:'
    $lines = @(@($Suggestion.List) | ForEach-Object { '- ' + $_ })
    if (-not $lines.Count) { $lines = @('- nothing: no commit since then changes what the package ships') }
    $out = @($head) + $lines
    if (@($Suggestion.LeftOut).Count) { $out += @($Suggestion.LeftOut | ForEach-Object { '  ' + $_ }) }
    $out -join "`r`n"
}

function Get-LightsFromFacts {
    # cc-P155: the rebuild and package lights judged from their facts, in one place for this console's lights and the
    # status file (ConvertTo-ConsoleStatusDocument). A light with no facts is $null.
    param($RebuildFacts, $PackageFacts)
    $rb = $null; $pk = $null
    $rf = $RebuildFacts; $pf = $PackageFacts
    if ($rf) { $rb = Get-RebuildLight -ImageCommit $rf.ImageCommit -ImageDirty $rf.ImageDirty -Head $rf.Head -Commits $rf.Commits -GolemRunning $rf.GolemRunning -Uncommitted $rf.Uncommitted -BuildRunning $rf.BuildRunning -Running $rf.Running }
    if ($pf) { $pk = Get-PackageLight -PublishedVersion $pf.PublishedVersion -PublishedCommit $pf.PublishedCommit -NewestZipVersion $pf.NewestZipVersion -NewestZipCommit $pf.NewestZipCommit -Head $pf.Head -Commits $pf.Commits -CommitsSinceZip $pf.CommitsSinceZip -GolemRunning $pf.GolemRunning -Uncommitted $pf.Uncommitted -ClientStale $pf.ClientStale -BuildRunning $pf.BuildRunning -SiteReachable $pf.SiteReachable -SiteError $pf.SiteError }
    [pscustomobject]@{ Rebuild = $rb; Package = $pk }
}

function ConvertTo-LightsJson {
    # status.json-style output for P143's remote console and the status page: both lights, as judged, with their commits.
    param($Rebuild, $Package, [datetime]$Now)
    $one = { param($l) if (-not $l) { return $null }; [ordered]@{ color = $l.Color; word = $l.Word; detail = $l.Detail; commits = @($l.Commits | ForEach-Object { [ordered]@{ short = $_.Short; prompt = $_.Prompt; subject = $_.Subject; files = @($_.Files) } }) } }
    ([ordered]@{ schema = 1; generatedAt = $Now.ToString('s'); rebuild = (& $one $Rebuild); package = (& $one $Package) } | ConvertTo-Json -Depth 6)
}

function Get-ConsoleTooltips {
    # One or two plain sentences per button, tile, tab, box and field: what it does, whether it changes
    # anything (live, test, git, a save), and what it refuses. Keyed by action key (Get-ConsoleActions) or by
    # the control's name; the drawing code only looks them up.
    [ordered]@{
        'test.start'       = 'Starts the test shard (sl-modernuo-test) on the image it already has. Changes only the test shard; it builds nothing.'
        'test.fresh'       = 'Moves the test save aside and starts a NEW WORLD with the LIVE accounts copied in. The old test save is kept beside it, never deleted.'
        'test.wipe'        = 'Keeps the test WORLD and deletes every test account and character, then makes one new admin. Backs up the test save first, asks for WIPE TEST ACCOUNTS and asks the password in its own window. Refused on live.'
        'test.stop'        = 'Stops the test shard. Play since the last autosave is lost unless the shard saves on stop (the plan says which).'
        'test.rebuild'     = 'Builds a new image through build.sh and all its test gates, then starts the test shard on it. Long; nothing changes if a gate fails.'
        'test.deploy'      = 'Points sl-modernuo:latest at an image a prompt already built (sl-modernuo:cc-pNN), after the phrase deploy to test, then restarts the TEST shard on it. Never touches live.'
        'test.tail'        = 'Follows the test shard log as it is written. Read-only; Stop (or closing its window) ends it.'
        'test.client'      = 'Opens a game client pointed at the test shard (port 2594). Changes nothing.'
        'fix.diagnose'     = 'Reads the test shard: status, health, image and commit, uptime, and for a crash its exception, first five frames and the bug-list row it matches. Changes nothing.'
        'tab.Shard question' = 'When a shard waits for an answer on its console (a save it cannot load, a fatal error), this shows the question and sends an answer. Delete answers back up the save first and need the phrase delete on live or delete on test.'
        'qLive'            = 'Answer the LIVE shard''s question. Every answer asks for a typed phrase.'
        'qTest'            = 'Answer the test shard''s question. Every answer asks for a typed phrase.'
        'qText'            = 'The question the picked shard is waiting on, what failed, and what each answer does.'
        'question.show'    = 'Shows the question the picked shard waits on, what failed, and what each answer does. It only reads.'
        'question.n'       = 'Types n and Enter at the picked shard''s question after the phrase answer live or answer test: nothing is deleted and loading stops. It is sent only if the shard is still asking that question.'
        'question.y'       = 'Types y after the phrase delete on live or delete on test and a verified backup of the save: the one bad object (or file) is deleted and loading goes on. The confirmation says first what is lost.'
        'question.a'       = 'Types a after the phrase delete on live or delete on test and a verified backup: this and every later bad object in that file are deleted without asking.'
        'question.enter'   = 'Presses Enter at "press return to exit" after the phrase answer live or answer test: the server exits and Docker starts it again.'
        'question.stop'    = 'Stops the picked shard with docker stop -t 60 after the phrase stop live or stop test. Safe while it waits: nothing has loaded.'
        'fix.restart'      = 'Restarts the test container on the image it has, after the phrase restart test, then waits for it to listen or crash. Never touches live.'
        'fix.rollback'     = 'Backs up the test save, then starts the test shard on the newest sl-modernuo:good-* image, after the phrase roll back test. Warns if the save is newer than that image. latest is not moved.'
        'fix.rebuild'      = 'Builds a commit you pick through every test gate (in a worktree outside the repo) and starts the test shard on it, after the phrase rebuild test. Minutes.'
        'fix.prune'        = 'Removes old sl-modernuo tags after the phrase prune images, listing them and the space freed. Keeps latest, the newest good-* tags, rollback-*, pre-bump-*, the keep list and what any container runs.'
        'live.start'       = 'docker start on the existing live container, after a typed phrase. Never creates a container and never runs compose.'
        'live.stop'        = 'docker stop on the live container, after a typed phrase. Real players are disconnected; the plan says whether the stop saves first.'
        'live.restart'     = 'docker restart on the live container, after a typed phrase. Real players are disconnected.'
        'live.tail'        = 'Follows the live shard log. Read-only.'
        'live.client'      = 'Opens a game client pointed at the live shard (port 2593). Changes nothing.'
        'snapshot.create'  = 'Copies the selected shard''s Saves folder into a named snapshot. Stops the shard while it copies and starts it again; live asks for a phrase.'
        'snapshot.list'    = 'Prints the snapshots of the selected shard with their size and accounts. Read-only.'
        'snapshot.restore' = 'Replaces the selected shard''s save with the selected snapshot. The current save is snapshotted first (PRE-RESTORE) and moved aside, never deleted; live asks for a phrase.'
        'snapshot.delete'  = 'Deletes the selected snapshot folder, after you confirm. The only thing this console ever deletes.'
        'diag.docker'      = 'Runs Check-Docker.ps1 and writes check-HHmmss.txt in the repo folder. Read-only.'
        'diag.shard'       = 'Runs Check-Shard.ps1 on the TEST container and writes shard-HHmmss.txt. Read-only.'
        'diag.folder'      = 'Opens the folder the two checks write to. Changes nothing.'
        'commit.preview'   = 'Runs Commit-ShardWork.ps1 -DryRun: lists what would be committed in both repos and changes nothing.'
        'commit.run'       = 'Commits and PUSHES both repos to GitHub after the phrase commit shard work; a pushed commit cannot be taken back. Refused as SYSTEM, when both repos are clean, or while a prompt has not said DONE.'
        'package.build'    = 'Builds a player package zip in player-package\dist and checks it. Publishes nothing; refused while the shard repo has uncommitted changes.'
        'package.publish'  = 'Publishes the selected zip: every player''s launcher is offered it. Asks for publish player package, then the script asks you to type yes in its own window.'
        'package.check'    = 'Runs the package gates on the selected zip. Read-only.'
        'package.scripts.check'   = 'Shows your scripts repo, the dev channel and the official channel side by side, per script, with who published each and when, and whether this PC can publish. Changes nothing.'
        'package.scripts.dev'     = 'Publishes your Legion scripts to the dev channel, which only your own PCs read. Shows what changed and asks you to type yes. Refused until the scripts repo is committed and pushed.'
        'package.scripts.promote' = 'Copies the dev scripts you pick to the official channel, which every player''s install pulls, and tags the commit official-<version>. Shows hashes and asks you to type yes.'
        'package.scripts.pull'    = 'git pull --ff-only in your scripts repo: what your other PC pushed. Every client here sees it at once (they link to the repo).'
        'package.scripts.setup'   = 'Clones the scripts repo (asks where, once) and links every TazUO client on this PC to it. Safe to run again: it repairs a broken link and changes nothing else.'
        'package.scripts.key'     = 'Once per PC: puts this PC''s SSH key on Haven so it can publish scripts. Asks Haven''s password one time.'
        'golem.on'         = 'Wake the Golem: turns the Golem on (ENABLED), so at CC''s next stop it hands the next queued prompt and commits and pushes after each DONE. It opens no session: Poke the Golem does that. Refused without email.json; runs in its own window.'
        'golem.off'        = 'Rests the Golem now: no more hand-offs, and a prompt CC is running still finishes but is not committed (saying Golem, stop in CC rests after it is committed).'
        'golem.email'      = 'Opens Setup-Email.ps1 in its own window, which asks for the mail password there. Writes the Golem''s email settings.'
        'golem.test'       = 'Sends one test email through the Golem''s settings. Changes nothing else.'
        'golem.queue'      = 'Opens the prompt queue folder in Explorer. Changes nothing.'
        'golem.log'        = 'Opens the Golem''s log in Notepad. Changes nothing.'
        'golem.settings'   = 'Opens the Golem''s settings.json in Notepad: progressEmails, maxPerRun, stallMinutes. The console changes nothing; what you save there counts from the hook''s next event.'
        'golem.folder'     = 'Opens the Golem''s folder in Explorer. Changes nothing.'
        'web.pick'         = 'The site the buttons beside it act on, from websites.json.'
        'web.check'        = 'Checks every site again now instead of waiting for the next 60 s check. Changes nothing.'
        'web.open'         = 'Opens the picked site in your browser. Changes nothing.'
        'web.restart'      = 'Restarts the picked game-PC site''s container (docker restart), in its own window. The page is down for a few seconds.'
        'web.redeploy'     = 'Runs the picked game-PC site''s own start script, which recreates its container and checks it answers, in its own window.'
        'web.logs'         = 'Shows the last 200 lines of the picked game-PC site''s container in a window. Changes nothing.'
        'web.deploy'       = 'Deploys the picked Haven site with its allowlisted script from websites.json, after the phrase deploy to haven, using your own SSH key. The window keeps a log.'
        'web.ops-pages'    = 'Puts the ops pages stack on Haven (docker/ops) and keeps it current, after the phrase deploy to haven. Its window reads Haven first, prints the plan, and changes nothing until you type deploy there.'
        'web.ops-cutover'  = 'Moves Play tests, Ideas, Bug reports and Shard status from this PC to Haven, after the phrase deploy to haven. Its window shows the plan and waits for cut over.'
        'web.ops-rollback' = 'Brings the ops pages back to this PC with everything marked on Haven, after the phrase deploy to haven. Its window shows the plan and waits for roll back.'
        'web.print-queue'  = 'Puts the Sunflower print queue on Haven, and the first time moves its data there from this PC, after the phrase deploy to haven. Its window reads Haven first, prints the plan, and changes nothing until you type deploy there.'
        'web.print-queue-rollback' = 'Brings the Sunflower print queue back to this PC with every edit made on Haven, after the phrase deploy to haven. Its window shows the plan and waits for roll back.'
        'web.mega-dashboard' = 'Puts the one Homepage (Shattered Legacy, Sunflower, home lab) on Haven and stops the stale one there and this PC''s, after the phrase deploy to haven. Its window prints Haven''s containers and ports first, then the plan, and changes nothing until you type deploy there.'
        'web.mega-dashboard-rollback' = 'Stops Haven''s new Homepage and starts the stale one and this PC''s dashboard again, after the phrase deploy to haven. Its window shows the plan and waits for roll back.'
        'web.portainer-haven' = 'Puts Portainer CE on Haven (HTTPS on 192.168.1.61:9443, the tailnet on :9000), backing up its data volume first when it exists, after the phrase deploy to haven. Its window reads Haven first, prints the plan, and changes nothing until you type deploy there. The first time, it prints the first-login link: make the admin user within 5 minutes.'
        'web.portainer-haven-rollback' = 'Stops Portainer on Haven (its data volume is kept) and turns off its tailnet port, after the phrase deploy to haven. Its window shows the plan and waits for roll back.'
        'web.npm'          = 'Opens Nginx Proxy Manager: an SSH tunnel to Haven in its own window (your own key), then the admin page in your browser. No password is stored here.'
        'golem.longrun'    = 'Starts a long run: up to the cap (a number, or all for until the queue is empty) until the end time, then back to maxPerRun by itself. Asks start long run; runs Start-LongRun.ps1 in its own window.'
        'golem.endlongrun' = 'Ends a long run now: maxPerRun counts again. Runs End-LongRun.ps1 in its own window.'
        'golem.wrapup'     = 'Wrap up after this task: the running prompt goes to DONE, is committed and pushed, then the Golem rests with ENABLED kept. Asks wrap up golem, and says Cancel wrap up while set.'
        'golem.stoppoint'  = 'Find a stopping point: CC finishes its step, leaves both repos green, writes a continuation prompt and ends PAUSED, then the Golem commits and rests. Asks find a stopping point, and says Cancel stopping point while set.'
        'golem.resume'     = 'Poke the Golem: opens a new Claude Code session in a terminal window on this PC, which says the start phrase, so the Golem hands it the next prompt. Wake the Golem only turns the Golem on. Asks poke the golem; when greyed out, the reason comes first.'
        'golem.model'      = 'The model Poke the Golem uses: Auto (the default) reads the next prompt''s Model line (no line: Opus), as the self-wake does. Fable or Opus overrides it for one Poke, then it is Auto again. Changes nothing by itself.'
        'golem.autowake'   = 'Self-wake on or off (settings.json autoWake). On, the Golem opens the next CC session itself after a context rest, a model wait or the run cap, within its limits; never after a STOPPED or NEEDS YOU. Runs Set-AutoWake.ps1 in its own window.'
        'golem.nightshift' = 'Night shift on or off: from the start to the end time the Golem checks the usage at the start, every nightCheckMinutes and just after each 5-hour reset, and runs a batch of prompts whenever the week has room under its pace, each ending at a stopping point once its budget is spent (never in the bot window or the last nightMinStartMinutes). Asks night shift on when turning it on; runs Set-NightShift.ps1 in its own window.'
        'golem.marathon'   = 'Marathon mode: sets settings.json marathon to the pick beside it (auto, on or off), asking marathon <mode> first; auto turns it on when the week''s projection falls short of 95% with over 6 hours left, and off at 95% or at usageLimitPct. While on, every awake Golem is woken on its lane with no rest after a Large prompt, no run cap, the night shift at full and the extra PCs also in the day, but the 5-hour window still holds.'
        'golem.marathonpick' = 'The marathon mode Marathon: set writes: auto, on or off. Starts on the current setting.'
        'golem.nightstart' = 'When the night shift starts (HH:mm, default 22:30).'
        'golem.nightend'   = 'When the night shift ends (HH:mm, default 06:30): no new prompt is handed after it.'
        'golem.pick'       = 'Which Golem the Golem buttons act for: every Golem in golems.json, the integrator first. Another Golem on this PC gets its own files and name; one on another PC, a disabled one, or any but the integrator while multiGolem is off says why. Wake''s model follows the picked Golem''s lane.'
        'golem.assignnext' = 'With Assign: the prompt also goes next for that Golem (Run next).'
        'golem.assign'     = 'Assign: the picked queued prompt gets the line **Golem:** <the picked Golem>, so only that Golem claims it (Assign-Prompt.ps1 checks its lane, areas and the prompt''s Touches first, and refuses with the reason). Tick "and run it next" to put it first for that Golem too.'
        'golem.lanenow'    = 'Send to the second PC: the picked Codex lane may start now, outside its night window, for the next 3 hours (Lane-Now.ps1 on its own PC; the game PC wakes that PC if it can). Her using the PC, a game running and the Codex budget still hold it back.'
        'golem.poke'       = 'Check now: runs the Golem''s 10-minute watch at once (Poke-Golem.ps1), so it looks again at a stall, a rest whose time has come or a pending wake. Changes nothing the watch would not. It opens no session (Poke the Golem does).'
        'golem.runnext'    = 'Run next: the queue row you picked is handed at the next hand-off, ahead of continuations (Set-RunNext.ps1; nothing is renamed). Asks the prompt number. With no row picked, it clears the pick.'
        'golem.retry'      = 'Retry stopped prompt: puts the STOPPED prompt back first in the queue, clears the stop and wakes a session on its own model (Retry-Stopped.ps1). Asks the prompt number. Greyed out while no prompt is STOPPED.'
        'golem.answer'     = 'Answer a Golem''s question: pick the question, a choice and (where it takes them) your own words, then type answer <golem> <prompt>, and the Golem resumes with your answer as data (Answer-Question.ps1). Greyed out while no Golem on this PC has an open question; a Golem on another PC is answered on the web console.'
        'golem.clicheck'   = 'Checks the Claude Code CLI: installed, and signed in (claude auth status). Run it after signing in so the Golem tab sees it. Read-only; runs Check-Cli.ps1 in its own window.'
        'golem.integrate'  = 'Merge branches now (several Golems): the integrator merges every finished Golem branch into main, one at a time, builds when the server changed, commits and pushes; a conflict or a red build becomes a fix-up prompt for that Golem. Only while nothing runs on the integrator and its trees are clean (with Shadow Iron, settings.json mergeGolem, one of its rounds now). Asks merge branches; runs Integrate.ps1 in its own window.'
        'golem.skipday'     = 'Owner works: the Golem picked on the Golem tab keeps to its hours on the date in the Until box (YYYY-MM-DD) instead of working all day, for a Golem with all-day weekdays (Stone''s Sat, Sun, Mon). Asks skip <golem> and runs Skip-Day.ps1 in its own window (email: skip day <Golem> <date>).'
        'golem.away'        = 'Away mode on, until the time in the Until box: the Golems work around the clock within the night shift''s usage rule, park a prompt that stops for a decision and go on, and email a digest each morning. Asks away on; runs Set-Away.ps1 in its own window.'
        'golem.awayoff'     = 'Away mode off now: the Golems go back to their usual pace and rests; parked prompts stay parked until you unpark them. Asks away off; runs Set-Away.ps1 -Off in its own window.'
        'golem.mergepause'  = 'Pause merging: Shadow Iron, the merge Golem, runs no round until Resume merging; finished branches wait in merge-queue. Asks pause merging; runs ShadowIron.ps1 -Pause in its own window.'
        'golem.mergeresume' = 'Resume merging: Shadow Iron merges again, one round at once and then every 10 minutes. Asks resume merging; runs ShadowIron.ps1 -Resume in its own window.'
        'golem.tinkerrun'    = 'Tinker: run now: one round of the watchdog at once (Tinker.ps1): every PC''s kit brought in step, every Golem looked at, a known stall fixed and anything else asked. Asks run tinker; may commit a Golem''s own WIP on its own branch, clear a flag or wake a Golem.'
        'golem.tinkerpause'  = 'Pause the Tinker: no round runs until Resume the Tinker; nothing else changes. Asks pause tinker; runs Tinker.ps1 -Pause in its own window.'
        'golem.tinkerresume' = 'Resume the Tinker: rounds run again, one at once and then every 10 minutes. Asks resume tinker; runs Tinker.ps1 -Resume in its own window.'
        # cc-P132: the lights, the Golem figure and the header rune.
        'rebuild.light'    = 'Whether the test shard needs a rebuild: green built from the latest server change, amber server commits since the running build (Rebuild can go), red needed but blocked with the reason. Hover for the commits and files; it only reads and never builds.'
        'package.light'    = 'Whether the player package needs a build or a publish: green the published package holds every player-facing change, amber Build package (or Publish package when a built zip holds them), red blocked with the reason. Hover for the commits; it only reads.'
        'golemFigure'      = 'The Golem: glowing while it runs a prompt, a soft glow when awake with nothing running, dark when resting or off. Art drops into scripts\console-art (notes\console-art-spec.md).'
        'headerRune'       = 'The header sigil (a drawn stand-in until art lands in scripts\console-art\header-band.png).'
        'golem.cap'        = 'The long run''s cap: a number of prompts, or all (until the queue is empty).'
        'golem.until'      = 'When the long run ends, a local date and time like 2026-10-19 06:00. Then the Golem rests; the phrase starts a new run.'
        'golemQueue'       = 'The queue in run order: the running prompt on top, the next one marked next, each with its Summary line (grey: no Summary line, so its first sentence), model and size. Click one for its preview. Read only.'
        'golemPreview'     = 'The picked prompt''s title, its note and its first paragraph (who asked for it and why). Read only.'
        'golemFinished'    = 'The last 10 finished prompts: DONE, STOPPED or PAUSED, when, and the first headline of each one''s notes. Read only.'
        'golem.openprompt' = 'Opens the picked queued prompt in Notepad. The console never edits, reorders or deletes a prompt.'
        'golem.opennotes'  = 'Opens the picked finished prompt''s notes in Notepad.'
        'golem.move'       = 'Moves the CC queue from cc-autopilot to the Golem with Move-ToGolem.ps1: the hooks, the scheduled task and the state. Asks for move to the golem, then shows its dry run in its own window and asks you to type move; refused while a prompt runs.'
        'links.playtests'  = 'Opens the play-test page (port 8093) in your browser. Changes nothing here.'
        'links.dashboard'  = 'Opens the Homepage dashboard (port 3000) in your browser. Changes nothing here.'
        'vpn.disconnect'   = 'Turns off the VPN this PC is on, so sl-ddns stops publishing the VPN''s address. Ends a WireGuard for Windows tunnel (elevated); opens Proton VPN with what to click; names any other VPN to disconnect in its app. Never stops a VPN service or disables an adapter.'
        'links.status'     = 'Opens the shard status page (port 8093, /status): playable gates, the CC queue, play tests and how old the judgment is. Changes nothing.'
        # controls without an action key
        'refresh'          = 'Reads docker and the disk again and redraws the tiles and the status. Read-only.'
        'autoRefresh'      = 'Refreshes the status every 30 seconds while ticked. Read-only.'
        'dryRun'           = 'While ticked, every button prints the plan it would run and runs nothing; a window action opens a window that only prints.'
        'theme'            = 'The look: UO (stone, parchment, gold) or Plain (the old look). Remembered on this PC; takes effect the next time the console opens. Changes no behavior.'
        'outputInWindows'  = 'Ticked: every action runs in a PowerShell window of its own, as before. Unticked: non-interactive actions stream into the Action output pane here.'
        'snapTest'         = 'Snapshots of the test shard.'
        'snapLive'         = 'Snapshots of the LIVE shard. Every action here asks you to type a phrase.'
        'snapName'         = 'The name for a new snapshot: letters, digits, dot, dash and underscore.'
        'snapList'         = 'The snapshots of the selected shard. Select one for Restore or Delete.'
        'commitMessage'    = 'The commit message. Blank uses the script''s default, Shard work, <date> <time>.'
        'commitForce'      = 'For one commit, skips only the wait for files changed in the last minute. Not the DONE check, SYSTEM or the phrase.'
        'packageNotes'     = 'One line of What is new for the package, required to build. Filled with a suggestion: what changed for players since the published package, biggest first (the list is under it); edit or replace it. Changes nothing until Build.'
        'packageNotesRefill' = 'Puts the suggested What is new line back in the notes box, over what is there. Changes nothing else.'
        # cc-P153: the header's icon, the suggestion's sources and the four read-only panels.
        'headerIcon'       = 'The Shard Console''s own icon (the window''s, from the site''s favicon). Changes nothing.'
        'packageNotesFrom' = 'What the suggested What is new line was made from, one line per item, biggest first: each commit since the published package that changes what the package ships (map, client, art, music, launcher, official Legion scripts), named from its prompt title, and what was left out (staff only in CHANGELOG.md). Read only.'
        'statusPanel'      = 'The status of both shards, the publisher and the Golem, as last read (Refresh reads it again). Read only.'
        'websitesPanel'    = 'Each website''s last check and what the deploy buttons above would run. Read only.'
        'actionOutput'     = 'What each button did: its plan, its output and the result. Read only; a new action adds to the bottom.'
        'paneOutput'       = 'The Action output pane: a running action''s output as it is written. Read only; Stop ends the action.'
        'packageList'      = 'The zips in player-package\dist, newest first. Select one to check or publish.'
        'packageRefresh'   = 'Reads player-package\dist again. Read-only.'
        'packageClient'    = 'The client folder Build package ships (our TazUO build), its sl-build.txt, and whether it matches client\patches. Refresh list reads it again. Read-only.'
        'worldCopy'        = 'Copies this one command to the clipboard, to paste in a game client. The console cannot run it.'
        'worldCopyGroup'   = 'Copies the whole group, one command a line.'
        'worldTick'        = 'A tick for your own checklist, this session only. Saves nothing.'
        'paneCopy'         = 'Copies everything in the Action output pane to the clipboard.'
        'paneClear'        = 'Clears the Action output pane. Stops nothing.'
        'paneStop'         = 'Ends the running action and every process it started. A build or tail stops where it is; a half-run plan says what it had done.'
        'paneHide'         = 'Hides or shows the Action output pane. A running action keeps running.'
        'tile.LIVE'        = 'The live container: running or not, and whether its image drifted from latest.'
        'tile.TEST'        = 'The test container: running or not, and whether its image drifted from latest.'
        'tile.STATUS PUBLISHER' = 'Whether status.json for the website is fresh (written in the last 3 minutes) and served on 8092.'
        'tile.PUBLIC DNS'  = 'Whether public DNS for shatteredlegacyuo.com points at this house. Red usually means a VPN is on. Grey: the check failed.'
        'tile.WEBSITES'    = 'How many of the sites in websites.json answer, checked every 60 s; amber names the ones that do not.'
        'tile.GOLEM'       = 'What the Golem (the CC queue runner) is doing: off, idle, running a prompt, or NEEDS YOU after it emailed you.'
        'tile.PLAY TESTS'  = 'How many play tests wait in the queue; amber while a Fail is not in the bug list yet.'
        # cc-P135: the Map and Art tab.
        'art.registry'     = 'Runs the art registry check of validate-art: rows that collide, art ids with EA art in the slot, anim and body rows the client would misread. Reads only.'
        'art.bodies'       = 'Runs the body table of validate-art: every body the client data names or draws, with its type, file, slot, layout and actions. About 1,300 lines; reads only.'
        'art.checkvd'      = 'Checks the animation file (.vd) against the body given, or proposes a free body and anim7 slot when the body box is empty. Registers nothing.'
        'art.hues'         = 'Compares the hues file given with our own: the hues whose colours differ, and those that differ only in name. Changes neither file.'
        'art.vdfile'       = 'The animation file (.vd) to check or to find a body for; Browse picks one.'
        'art.browsevd'     = 'Picks an animation file (.vd).'
        'art.body'         = 'The body number to check the file against, in decimal (2052); empty proposes a free one.'
        'art.huesfile'     = 'The other hues file (hues.mul), compared against ours in client-data; Browse picks one.'
        'art.browsehues'   = 'Picks a hues file (hues.mul).'
        'map.footprint'    = 'Prints the syntax of [CheckFootprint: can a building footprint stand where you target. Typed in a client; the console only prints it.'
        'map.spots'        = 'Prints the syntax of [ValidateSpots: every spot in a spawn, decoration or x,y file checked, the bad ones named. Typed in a client.'
        'map.statics'      = 'Prints the syntax of [FindStaticProblems: duplicate, buried and unknown map statics in a box. Typed in a client.'
        'map.walkable'     = 'Prints the syntax of [Walkable: blocked tiles in a box, listed or marked with runes that delete themselves (test shard). Typed in a client.'
        'map.shore'        = 'Prints the syntax of [ShoreCheck: water touching land in a box, with coast heights. Typed in a client.'
        'map.export'       = 'Prints the syntax of [ExportPoints: a targeted rectangle or points written as spawn-file JSON. Typed in a client.'
        'tab.Test shard'   = 'The throwaway test shard: start, stop, rebuild, deploy, wipe, tail and a client.'
        'tab.Fix test shard' = 'When the test shard will not start: diagnose it, restart it, roll back to the last good image, rebuild a commit, prune old images.'
        'tab.Snapshots'    = 'Named copies of a shard''s save: create, restore, delete.'
        'tab.World setup'  = 'In-game commands to paste into a client, read from the code. The console cannot run them.'
        'tab.Map and Art'  = 'Art checks that run validate-art here, and the syntax of the in-game map checks to type in a client.'
        'tab.Diagnostics'  = 'The two read-only health checks and their folder, and Disconnect VPN.'
        'tab.Commit'       = 'Preview, or commit and push, both repos.'
        'tab.Player package' = 'Build, check and publish the player package.'
        'tab.Websites'     = 'The websites, on this PC and on Haven: status, open, restart, redeploy, logs, deploy, and NPM.'
        'tab.Golem'        = 'The Golem, the CC queue runner: its state, queue and log, its own scripts, and the move from cc-autopilot.'
        'golemPanel'       = 'The usage rows are the Claude plan (Claude Code''s status line and the plan usage page); the codex rows are the ChatGPT plan''s Codex limits, which art, the review before each commit and the Codex lane share, read from Codex''s own session logs. The Golem stops Codex at codexMaxPctPerWindow of a window, leaves codexWeekReservePct of the week to you, and runs the lane only in the night window while codexNightOnly is on (D:\UO\golem\settings.json).'
        'tab.Art'          = 'Art requests: what is waiting, generated, approved and placed, with a thumbnail. Every button runs scripts\art\Art.ps1 in the output pane; nothing is placed until you approve it.'
        'artList'          = 'Every art request, waiting for your review first, with a thumbnail of its review sheet or its placed file. Select one, then press a button below.'
        'art.refresh'      = 'Reads the requests in D:\UO\art\requests again. Changes nothing.'
        'art.prompt'       = 'Copies the selected request''s style and subject, ready to paste into ChatGPT, to the clipboard. Changes nothing.'
        'art.generate'     = 'Generates one image for the selected request with Codex, on your ChatGPT plan: it shows the plan usage first and refuses past the budget unless you type yes. The result goes to review, never into place.'
        'art.generateApi'  = 'Generates one image through the OpenAI API with the stored key: it shows the estimated cost first and refuses past the monthly cap. The result goes to review.'
        'art.import'       = 'Adds a picture you made elsewhere (D:\UO\art-inbox, or any file) to the selected request as a variant for review.'
        'art.approve'      = 'Approves the selected request''s newest variant that is not rejected. Approving places nothing; Place does.'
        'art.reject'       = 'Rejects the selected request''s newest variant, with a note of why.'
        'art.regenerate'   = 'Generates again with Codex with your note (for example make the golem squatter); Codex sees the last variant. Same budget check as Generate.'
        'art.place'        = 'Processes the approved variant into its destination (sizes, trim, checks, strips) and archives the rest. Refused until a variant is approved.'
        'art.review'       = 'Opens the selected request''s review folder in Explorer. Changes nothing.'
        'tab.LIVE shard'   = 'The real shard with real players. Every button asks for a typed phrase.'
    }
}

function ConvertFrom-ConsoleSettings {
    # The console's own settings file (cc-P84): Theme UO or Plain (default UO) and OutputIn pane or window
    # (default pane). Anything missing or unreadable falls back to the default, so a bad file never stops it.
    param([string]$Text)
    # cc-P132: Enhanced is the default look; UO and Plain are kept.
    $s = [pscustomobject]@{ Theme = 'Enhanced'; OutputIn = 'pane' }
    if (-not $Text) { return $s }
    try { $j = $Text | ConvertFrom-Json } catch { return $s }
    if ($j.Theme -in @('UO', 'Plain', 'Enhanced')) { $s.Theme = [string]$j.Theme }
    if ($j.OutputIn -in @('pane', 'window')) { $s.OutputIn = [string]$j.OutputIn }
    $s
}

function ConvertTo-ConsoleSettings {
    param($Settings)
    ([ordered]@{ Theme = $Settings.Theme; OutputIn = $Settings.OutputIn } | ConvertTo-Json)
}

# The actions whose run asks a person something in its own console, so they always get a window of their own and
# never the output pane (which has no keyboard). Decided from the code each one runs:
#   test.wipe        its 'secret' step, Read-WipePassword: Read-Host -AsSecureString, twice.
#   package.publish  Publish-PlayerPackage.ps1 line 70: Read-Host '... Type yes'.
# Every other 'window' action was read: Start-TestShard.ps1, Check-Docker.ps1, Check-Shard.ps1,
# Commit-ShardWork.ps1 and Build-PlayerPackage.ps1 have no Read-Host, prompt or -it docker call; the typed phrases
# and yes/no questions are asked by this console's own dialogs before the run and passed as -ConfirmLive / -Yes.
# The Golem buttons (RunIn 'here') open the Golem's own scripts in windows of their own on purpose
# (Setup-Email.ps1 asks a password) and are not routed either.
# cc-P144, from Publish-LegionScripts.ps1, Setup-ScriptsRepo.ps1 and Setup-PublishKey.ps1:
#   package.scripts.dev, .promote   Read-Host '... Type yes' (and Promote's "which?").
#   package.scripts.setup           Read-Host for where the clone lives, the first time.
#   package.scripts.key             ssh-keygen's passphrase and Haven's password.
# Check scripts and Pull scripts ask nothing.
$script:InteractiveActions = @('test.wipe', 'package.publish', 'package.scripts.dev', 'package.scripts.promote', 'package.scripts.setup', 'package.scripts.key')

function Get-OutputRoute {
    # Where an action's run is shown: 'pane' (streamed into the console) or 'window' (a PowerShell window of its
    # own, as before cc-P84). Only 'window' actions and the two log tails can use the pane, never an interactive
    # one, and only when the pane exists and is free.
    param([string]$ActionKey, [string]$OutputIn = 'pane', [bool]$PaneAvailable = $true, [bool]$PaneBusy = $false)
    $def = @(Get-ConsoleActions | Where-Object { $_.Key -eq $ActionKey })[0]
    if (-not $def) { return 'window' }
    if ($ActionKey -in $script:InteractiveActions) { return 'window' }
    $paneable = ($def.RunIn -eq 'window') -or ($ActionKey -in @('test.tail', 'live.tail'))
    if (-not $paneable -or $OutputIn -ne 'pane' -or -not $PaneAvailable -or $PaneBusy) { return 'window' }
    'pane'
}

# A child run streaming into the pane marks each line with its color, so the pane draws it as the window would.
$script:PaneMarker = '[[shard-console:'

function Format-PaneLine {
    param([string]$Text, [string]$Color = 'normal')
    $script:PaneMarker + $Color + ']]' + $Text
}

function ConvertFrom-PaneLine {
    # A line from a pane child: its color and text. An unmarked line (a native command's own output) is normal on
    # stdout and gray on stderr, which docker uses for progress, not for errors.
    param([string]$Line, [bool]$IsError = $false)
    if ($null -ne $Line -and $Line.StartsWith($script:PaneMarker)) {
        $end = $Line.IndexOf(']]', $script:PaneMarker.Length)
        if ($end -gt 0) {
            $c = $Line.Substring($script:PaneMarker.Length, $end - $script:PaneMarker.Length)
            if ($c -in @('normal', 'head', 'green', 'amber', 'red', 'gray')) {
                return [pscustomobject]@{ Color = $c; Text = $Line.Substring($end + 2) }
            }
        }
    }
    $color = 'normal'
    if ($IsError) { $color = 'gray' }
    [pscustomobject]@{ Color = $color; Text = [string]$Line }
}

function Format-PaneExit {
    # The pane's last line for a run that ended by itself.
    param([int]$Code)
    switch ($Code) {
        0 { [pscustomobject]@{ Color = 'green'; Text = '== exited with 0 (done)' } }
        2 { [pscustomobject]@{ Color = 'red'; Text = '== exited with 2 (refused: no mode)' } }
        3 { [pscustomobject]@{ Color = 'amber'; Text = '== exited with 3 (cancelled, nothing changed)' } }
        default { [pscustomobject]@{ Color = 'red'; Text = ('== exited with ' + $Code + ' (failed)') } }
    }
}

function New-PlanStep {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('confirm', 'ask', 'say', 'refuse', 'exec', 'script', 'checkzip', 'gatecheck', 'window', 'launch', 'move', 'copy', 'verify', 'stopped', 'remove-snapshot', 'log', 'secret', 'wiperequest', 'wipewait', 'healthwait', 'answer')]
        [string]$Kind,
        [string]$Text, [string]$Exe, [string[]]$Arguments, [string]$From, [string]$To,
        [string]$Path, [string]$Phrase, [string]$Container, [switch]$IfExists,
        [Collections.IDictionary]$Named, [int]$Seconds, [string]$Signature
    )
    $a = @()
    if ($Arguments) { $a = @($Arguments) }
    # Named: a 'script' step's parameters, by name. The script runs in this process with them
    # splatted, so no command line sits between the value and the script (cc-P60).
    $n = [ordered]@{}
    if ($Named) { foreach ($k in $Named.Keys) { $n[$k] = $Named[$k] } }
    [pscustomobject]@{
        Kind = $Kind; Text = $Text; Exe = $Exe; Arguments = $a; From = $From; To = $To
        Path = $Path; Phrase = $Phrase; Container = $Container; IfExists = [bool]$IfExists; Named = $n; Seconds = $Seconds
        # cc-P106: an answer step's question (Get-ShardQuestion's Signature); the answer goes only to that question.
        Signature = $Signature
    }
}

function ConvertTo-PsLiteral {
    # A PowerShell single-quoted string: everything in it is literal, $ and ; and " included. Only
    # a single quote is special, and PowerShell counts the curly ones as single quotes too.
    param([string]$Text)
    $q = "(['" + [char]0x2018 + [char]0x2019 + [char]0x201A + [char]0x201B + "])"
    "'" + ($Text -replace $q, '$1$1') + "'"
}

function Format-ScriptCall {
    # How a 'script' step reads in a plan: a line that pastes into PowerShell and does the same.
    param([string]$Path, [Collections.IDictionary]$Named)
    $s = '& ' + (ConvertTo-PsLiteral $Path)
    if ($Named) {
        foreach ($k in $Named.Keys) {
            $v = $Named[$k]
            if ($v -is [bool] -or $v -is [switch]) { if ($v) { $s += ' -' + $k } }
            else { $s += ' -' + $k + ' ' + (ConvertTo-PsLiteral ([string]$v)) }
        }
    }
    $s
}

function ConvertFrom-GitPorcelain {
    # The paths in `git status --porcelain --untracked-files=all`, read the way Commit-ShardWork.ps1
    # reads them: a rename keeps its new name, and git's quotes come off.
    param([string[]]$Lines)
    foreach ($l in @($Lines)) {
        if (-not $l -or $l.Length -lt 4) { continue }
        $p = $l.Substring(3).Trim()
        if ($p -match ' -> ') { $p = ($p -split ' -> ')[-1] }
        $p.Trim('"')
    }
}

function Get-CommitInFlight {
    # cc-P60 Part B: what says work is still being written. Two listings in, so it is checked
    # without the real folders. Notes: Name and LastLine (the last non-empty line) of each
    # notes\cc-P<nn>-*.md. Changed: Repo, Path and LastWriteUtc of each file git would commit.
    # A note numbered DoneFrom or above that does not end "P<nn> DONE" or "P<nn> STOPPED: <why>"
    # is a prompt still running; notes numbered below it predate that rule and are not read. A
    # pending file written less than Seconds ago may be mid-write (cc-P68: seconds, from
    # CommitQuietSeconds; it was a fixed 5 minutes). Commit and push refuses on any of these; the
    # preview names them and runs.
    param($Notes, $Changed, [datetime]$Now, [int]$Seconds = 60, [int]$DoneFrom = 54)
    $out = New-Object 'System.Collections.Generic.List[object]'
    foreach ($n in @($Notes)) {
        if (-not $n -or [string]$n.Name -notmatch '^cc-P(\d+)-.+\.md$') { continue }
        $num = [int]$Matches[1]
        if ($num -lt $DoneFrom) { continue }
        $last = ([string]$n.LastLine).Trim()
        if ($last -cmatch ('^P' + $num + ' (DONE|STOPPED: .+)$')) { continue }
        $shown = $last
        if ($shown.Length -gt 70) { $shown = $shown.Substring(0, 67) + '...' }
        if (-not $shown) { $shown = '(the file is empty)' }
        $out.Add([pscustomobject]@{ Kind = 'prompt'; Name = $n.Name; Number = $num; Path = $null; LastWriteUtc = $null
            Text = ('P' + $num + ' has not said DONE: ' + $n.Name + ' ends "' + $shown + '". Committing now may publish half its work.') })
    }
    $since = $Now.AddSeconds(-$Seconds)
    foreach ($c in @($Changed)) {
        if (-not $c -or $null -eq $c.LastWriteUtc -or $c.LastWriteUtc -le $since) { continue }
        $ago = [int][Math]::Max(0, [Math]::Floor(($Now - $c.LastWriteUtc).TotalSeconds))
        $out.Add([pscustomobject]@{ Kind = 'file'; Name = ($c.Repo + ': ' + $c.Path); Number = $null; Path = $c.Path; LastWriteUtc = $c.LastWriteUtc; Ago = $ago
            Text = ($c.Repo + ': ' + $c.Path + ' changed ' + $ago + ' s ago, inside the last ' + (Format-QuietWindow $Seconds) + '. Something may still be writing it.') })
    }
    $out.ToArray()
}

function Format-QuietWindow {
    # How the quiet window is said: "60 seconds", "90 seconds", "5 minutes" (cc-P68).
    param([int]$Seconds)
    if ($Seconds -ge 120 -and ($Seconds % 60) -eq 0) { return ([string]($Seconds / 60) + ' minutes') }
    [string]$Seconds + ' seconds'
}

function Format-Countdown {
    # m:ss, rounded up, so the line never reads 0:00 while the commit is still refused.
    param([double]$Seconds)
    $s = [int][Math]::Max(0, [Math]::Ceiling($Seconds))
    '{0}:{1:00}' -f [Math]::Floor($s / 60), ($s % 60)
}

function Get-CommitCountdown {
    # cc-P68 Part D: the Commit tab's live line, from the facts Get-CommitPlan reads and the time
    # now. While a pending file is inside the quiet window it counts down to when the newest one
    # leaves it, naming that file. After that it shows whatever still blocks Commit and push (a
    # prompt that has not said DONE, SYSTEM, both repos clean), else "Ready to commit".
    param($Facts, $Config, [datetime]$Now, [switch]$Force)
    if (-not $Facts) { return 'The repos have not been read yet.' }
    $win = [int]$Config.CommitQuietSeconds
    $busy = @(Get-CommitInFlight -Notes $Facts.Notes -Changed $Facts.Changed -Now $Now -Seconds $win -DoneFrom $Config.NotesDoneFrom)
    $other = New-Object 'System.Collections.Generic.List[string]'
    if ($Facts.IsSystem) { $other.Add('this console runs as ' + $Facts.Identity + '; run it from your own Windows login') }
    foreach ($p in @($busy | Where-Object { $_.Kind -eq 'prompt' })) { $other.Add('P' + $p.Number + ' has not said DONE (' + $p.Name + ')') }
    $read = @(@($Facts.Repos) | Where-Object { -not $_.Error })
    if ($read.Count -eq @($Facts.Repos).Count -and @($read | ForEach-Object { @($_.Paths) }).Count -eq 0) { return 'Nothing to commit: both repos are clean.' }
    $files = @($busy | Where-Object { $_.Kind -eq 'file' } | Sort-Object LastWriteUtc -Descending)
    if ($files.Count) {
        $left = $win - ($Now - $files[0].LastWriteUtc).TotalSeconds
        $t = 'Commit allowed in ' + (Format-Countdown $left) + ' (waiting on ' + $files[0].Path + ')'
        if ($Force) { $t += '. Force is ticked: Commit and push skips this wait' }
        if ($other.Count) { $t += '. Also blocked: ' + ($other -join '; ') }
        return $t
    }
    if ($other.Count) { return 'Not ready: ' + ($other -join '; ') + '.' }
    'Ready to commit'
}

function Get-CommitPlan {
    # commit.preview and commit.run. Both call Commit-ShardWork.ps1 and reimplement none of it; the
    # preview passes its own -DryRun. Facts come from Get-CommitFacts: who is running, both repos'
    # git status, the CC notes and when each pending file last changed. What makes a commit unsafe
    # refuses commit.run, with no override, and is only reported by the preview, which still runs.
    # cc-P68: -Force (the Force box) skips only the quiet window for recently changed files. A
    # prompt that has not said DONE, SYSTEM, clean repos and the phrase are all as before, and the
    # confirmation names the files whose wait it skips.
    param([string]$Action, $Config, $Facts, [string]$Message, [switch]$Force)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $run = ($Action -eq 'commit.run')
    if (-not $Facts) { $steps.Add((New-PlanStep -Kind refuse -Text 'The state of the repos was not read, so nothing is run.')); return $steps.ToArray() }
    if (-not $Facts.ScriptExists) {
        $steps.Add((New-PlanStep -Kind refuse -Text ($Config.CommitScript + ' is not there, so there is nothing to run. Its path is CommitScript in Get-ConsoleConfig.')))
        return $steps.ToArray()
    }
    # Blocks commit.run; a warning in the preview.
    $flag = {
        param([string]$Text)
        if ($run) { $steps.Add((New-PlanStep -Kind refuse -Text $Text)) }
        else { $steps.Add((New-PlanStep -Kind say -Text ('WARNING: ' + $Text))) }
    }
    $win = Format-QuietWindow $Config.CommitQuietSeconds
    $busy = @(Get-CommitInFlight -Notes $Facts.Notes -Changed $Facts.Changed -Now $Facts.Now -Seconds $Config.CommitQuietSeconds -DoneFrom $Config.NotesDoneFrom)
    $forced = @()
    if ($run -and $Force) { $forced = @($busy | Where-Object { $_.Kind -eq 'file' }) }
    if ($run) {
        # Get-ConfirmPreface says $script:CommitPublicWarning above this, before the phrase.
        $ct = 'This commits and pushes both repos, D:\ShatteredLegacy and D:\UO\shard-migration.'
        if ($forced.Count) {
            $ct += ' FORCE is ticked: this skips the wait for files to be unchanged for ' + $win + ', for ' + (@($forced | ForEach-Object { $_.Name + ' (changed ' + $_.Ago + ' s ago)' }) -join ', ') + '. Something may still be writing them.'
        }
        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text $ct))
    }
    if ($Facts.IsSystem) {
        & $flag ('This console is running as ' + $Facts.Identity + '. Git refuses both repos for that account (dubious ownership) and it has no push credential. Run the console from your own Windows login.')
    }
    $read = 0
    $pending = 0
    foreach ($r in @($Facts.Repos)) {
        if ($r.Error) {
            $steps.Add((New-PlanStep -Kind say -Text ('WARNING: git status failed in ' + $r.Name + ' (' + $r.Path + '): ' + $r.Error + '. Commit-ShardWork.ps1 will say FAILED there and commit nothing in it.')))
        } else {
            $read++
            $pending += @($r.Paths).Count
            $steps.Add((New-PlanStep -Kind say -Text ($r.Name + ' (' + $r.Path + '): ' + @($r.Paths).Count + ' file(s) to commit.')))
        }
    }
    if ($read -eq @($Facts.Repos).Count -and $pending -eq 0) {
        if ($run) { $steps.Add((New-PlanStep -Kind refuse -Text 'Nothing to commit: both repos are clean.')) }
        else { $steps.Add((New-PlanStep -Kind say -Text 'Nothing to commit: both repos are clean.')) }
    }
    foreach ($b in $busy) {
        if ($forced -contains $b) { $steps.Add((New-PlanStep -Kind say -Text ('FORCE: not waiting on ' + $b.Text))) }
        else { & $flag $b.Text }
    }
    $blocking = @($busy | Where-Object { $forced -notcontains $_ })
    if ($run -and $blocking.Count) {
        if ($Force) { $steps.Add((New-PlanStep -Kind refuse -Text 'Commit and push stays refused until every prompt above ends DONE or STOPPED. Force skips only the wait for recently changed files. Preview commit still runs.')) }
        else { $steps.Add((New-PlanStep -Kind refuse -Text ('Commit and push stays refused until every prompt above ends DONE or STOPPED and no pending file has changed for ' + $win + '. Preview commit still runs.'))) }
    }
    $named = [ordered]@{}
    if (-not $run) { $named['DryRun'] = $true }
    if ($Message) { $named['Message'] = $Message }
    if ($run) {
        if ($Message) { $steps.Add((New-PlanStep -Kind say -Text ('Commit message: ' + $Message))) }
        else { $steps.Add((New-PlanStep -Kind say -Text 'Commit message: the script''s default, "Shard work, <date> <time>".')) }
        $steps.Add((New-PlanStep -Kind script -Path $Config.CommitScript -Named $named -Text 'Commit-ShardWork.ps1: add, commit and push both repos. Its own table at the end says pushed, clean, BLOCKED or FAILED for each.'))
        $steps.Add((New-PlanStep -Kind log -Text 'commit.run'))
    } else {
        $steps.Add((New-PlanStep -Kind script -Path $Config.CommitScript -Named $named -Text 'Commit-ShardWork.ps1 -DryRun: lists what would be committed in each repo and changes nothing.'))
    }
    $steps.ToArray()
}

# --- the player package (cc-P63) ---------------------------------------------------------------
# Build package runs player-package\Build-PlayerPackage.ps1 -Notes, then its -CheckZip on the zip it
# just wrote. Publish package runs D:\UO\haven-migration\Publish-PlayerPackage.ps1 -Zip, which asks
# its own "Type yes". Neither is reimplemented. Facts come from Get-PackageFacts (SHELL): the shard
# repo's git status and HEAD, the zips in dist\ with their version.json, the picked zip's own hash
# and app\package-version.txt, the newest committed CHANGELOG.md and the newest cc-P notes.

function Test-PackageNotes {
    # Why a notes line is refused, or $null. Build-PlayerPackage.ps1 refuses the same (one line of
    # printable ASCII: launchers print it in a PowerShell 5.1 console); said here before anything runs.
    param([string]$Notes)
    if (-not $Notes -or -not $Notes.Trim()) { return 'A package needs notes: the one line launchers show as "What is new". Type it in the Notes box.' }
    if ($Notes -match '[^\x20-\x7E]') { return 'Notes must be one line of plain ASCII (no line breaks, curly quotes or dashes other than -): launchers print it in a PowerShell 5.1 console.' }
    $null
}

function Get-PackageZipProblems {
    # cc-P63 Part A item 2, the guard: the zip's version.json must describe this zip. The same three
    # checks Publish-PlayerPackage.ps1 and gate 5 make (version, sha256, bytes), made before the
    # phrase is asked for. $Zip is a listing row; $Picked is the zip's own hash and package-version.
    param($Zip, $Picked)
    $p = New-Object 'System.Collections.Generic.List[string]'
    $j = $Zip.Json
    if (-not $j -or -not $j.Exists) {
        $p.Add($Zip.Name + ' has no ' + ($Zip.Name -replace '\.zip$', '.version.json') + ' beside it, so launchers could not be told about it. Build a new one.')
        return $p.ToArray()
    }
    if ($j.Error) { $p.Add(($Zip.Name -replace '\.zip$', '.version.json') + ' does not parse: ' + $j.Error); return $p.ToArray() }
    if (-not $Picked) { $p.Add('The zip itself was not read, so it cannot be checked against its version.json.'); return $p.ToArray() }
    if ($Picked.Error) { $p.Add($Zip.Name + ' could not be read: ' + $Picked.Error) }
    elseif ([string]$j.Version -ne [string]$Picked.InnerVersion) { $p.Add('version.json says version ' + $j.Version + ', the zip''s app\package-version.txt says ' + $Picked.InnerVersion + '.') }
    if ($Picked.Sha256 -and [string]$j.Sha256 -cne [string]$Picked.Sha256) { $p.Add('version.json says sha256 ' + $j.Sha256 + ', the zip is ' + $Picked.Sha256 + '.') }
    if ([string]$j.Bytes -ne [string]$Zip.Bytes) { $p.Add('version.json says ' + $j.Bytes + ' bytes, the zip is ' + $Zip.Bytes + '.') }
    $p.ToArray()
}

function Get-RolloutReminder {
    # Part A item 2, the note: when the newest committed CHANGELOG.md section or the newest cc-P notes
    # file says the package goes first and the server after ("Package first, then the server",
    # "publish the package before deploying the server"), one line reminding that the server deploy
    # follows. A warning only. The changelog's own header comment holds an example section, so it is
    # cut out before the newest "## " section is found.
    param([string]$Changelog, $LatestNote)
    $rx = '(?i)\bpackage\s+(first|before)\b'
    $where = @()
    if ($Changelog) {
        $body = [regex]::Replace($Changelog, '(?s)<!--.*?-->', '')
        $m = [regex]::Match($body, '(?ms)^## (\S+)[^\n]*\n(.*?)(?=^## |\z)')
        if ($m.Success -and $m.Groups[2].Value -match $rx) { $where += ('CHANGELOG.md ' + $m.Groups[1].Value) }
    }
    if ($LatestNote -and [string]$LatestNote.Text -match $rx) { $where += [string]$LatestNote.Name }
    if (-not $where.Count) { return $null }
    'Reminder: ' + ($where -join ' and ') + ' says package first, then the server. The server deploy follows this publish; it is not part of this button.'
}

function Format-PackageClientText {
    # cc-P124: what the Player package tab says about the client Build package will ship, from Get-PackageClientFacts:
    # the folder, its sl-build.txt as it is, and whether it may ship (the client gate's own words when not).
    param($Client)
    if (-not $Client) { return 'The client folder was not read.' }
    $l = New-Object 'System.Collections.Generic.List[string]'
    $ver = ''
    if ($Client.Version) { $ver = ' (TazUO ' + $Client.Version + ')' }
    $l.Add('Build package ships the client in ' + $Client.Dir + $ver + '.')
    if ($Client.SlBuildText) {
        $l.Add('Its sl-build.txt:')
        foreach ($x in @($Client.SlBuildText -split "`r?`n" | Where-Object { $_.Trim() })) { $l.Add('    ' + $x.Trim()) }
    }
    if (@($Client.Problems).Count) { foreach ($x in @($Client.Problems)) { $l.Add('NOT BUILDABLE: ' + $x) } }
    else { $l.Add('It matches client\patches\ and client\TAZUO_COMMIT: ready to build.') }
    $l -join "`r`n"
}

function Get-PackagePlan {
    param([string]$Action, $Config, $Facts, [string]$Notes, [string]$Zip)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    if (-not $Facts) { $steps.Add((New-PlanStep -Kind refuse -Text 'The package folder and the repo were not read, so nothing is run.')); return $steps.ToArray() }
    if ($Action -eq 'package.build') {
        if (-not $Facts.BuildExists) { $steps.Add((New-PlanStep -Kind refuse -Text ($Config.BuildPackage + ' is not there, so there is nothing to run.'))); return $steps.ToArray() }
        $bad = Test-PackageNotes $Notes
        if ($bad) { $steps.Add((New-PlanStep -Kind refuse -Text $bad)) }
        # The package should be built from a commit (P60's refusal style: every file named, no override).
        if ($Facts.RepoError) {
            $steps.Add((New-PlanStep -Kind refuse -Text ('git status failed in ' + $Facts.RepoPath + ': ' + $Facts.RepoError + '. The package is built only from a tree known to be committed.')))
        } elseif (@($Facts.RepoPaths).Count) {
            $all = @($Facts.RepoPaths)
            $shown = @($all | Select-Object -First 10)
            $more = ''
            if ($all.Count -gt $shown.Count) { $more = ' and ' + ($all.Count - $shown.Count) + ' more' }
            $steps.Add((New-PlanStep -Kind refuse -Text ($Facts.RepoPath + ' has ' + $all.Count + ' uncommitted file(s), so the package would not be built from a commit: ' + ($shown -join ', ') + $more + '. Commit first (Commit tab), then build.')))
        }
        # cc-P124: the client it ships, said before the script runs; a stale or stock client is refused here as the
        # script's client gate would refuse it.
        $clientSay = ''
        if ($Facts.Client) {
            if (@($Facts.Client.Problems).Count) {
                foreach ($x in @($Facts.Client.Problems)) { $steps.Add((New-PlanStep -Kind refuse -Text ('Not building: ' + $x))) }
            } else {
                $clientSay = ' The client is ' + $Facts.Client.Dir + ', TazUO ' + $Facts.Client.Version + ' (its sl-build.txt matches client\patches\).'
            }
        }
        if (-not $Facts.RepoError -and -not @($Facts.RepoPaths).Count) {
            $steps.Add((New-PlanStep -Kind say -Text ('Building from ' + $Facts.RepoPath + ' at ' + $Facts.Head + ', committed and clean. The zip goes to ' + $Config.PackageDist + '; nothing is published.' + $clientSay)))
        }
        $steps.Add((New-PlanStep -Kind script -Path $Config.BuildPackage -Named ([ordered]@{ Notes = $Notes }) -Text 'Build-PlayerPackage.ps1: stage, gates 1 to 3, zip, gates 4 to 6, version.json. A failing gate stops it and deletes the zip.'))
        $steps.Add((New-PlanStep -Kind checkzip -Path $Config.BuildPackage -To $Config.PackageDist -Text 'Build-PlayerPackage.ps1 -CheckZip on the zip the step above wrote: gates 4, 6 and 5 again, on the file itself.'))
        $steps.Add((New-PlanStep -Kind log -Text 'package.build'))
        return $steps.ToArray()
    }
    if ($Action -eq 'package.check') {
        # cc-P68 Part A: -CheckZip on any zip in dist\, only reading it, so not refused on a dirty tree.
        if (-not $Facts.BuildExists) { $steps.Add((New-PlanStep -Kind refuse -Text ($Config.BuildPackage + ' is not there, so there is nothing to check with.'))); return $steps.ToArray() }
        if (-not $Zip) { $steps.Add((New-PlanStep -Kind refuse -Text ('Choose a zip from ' + $Config.PackageDist + ' to check.'))); return $steps.ToArray() }
        $row = @(@($Facts.Zips) | Where-Object { $_.Name -eq $Zip })
        if ($row.Count -ne 1) { $steps.Add((New-PlanStep -Kind refuse -Text ("There is no zip '" + $Zip + "' in " + $Config.PackageDist + '.'))); return $steps.ToArray() }
        $z = $row[0]
        # P63 section 7 item 3: the working tree's build script is the one doing the gating.
        if ($Facts.BuildDiff -eq 'differs') { $steps.Add((New-PlanStep -Kind say -Text ('Note: the working tree''s player-package\Build-PlayerPackage.ps1 differs from HEAD''s (' + $Facts.Head + '), and it is the script doing this check.'))) }
        elseif ($Facts.BuildDiff -and $Facts.BuildDiff -ne 'same') { $steps.Add((New-PlanStep -Kind say -Text ('Note: could not tell whether player-package\Build-PlayerPackage.ps1 differs from HEAD''s: ' + $Facts.BuildDiff))) }
        if (-not $z.Json -or -not $z.Json.Exists) { $steps.Add((New-PlanStep -Kind say -Text ('No ' + ($z.Name -replace '\.zip$', '.version.json') + ' beside it, so gate 5 will fail.'))) }
        $steps.Add((New-PlanStep -Kind gatecheck -Path $Config.BuildPackage -From $z.FullName -Text ('Build-PlayerPackage.ps1 -CheckZip on ' + $z.Name + ': gates 4 and 6 on the zip, and 5 on its version.json. It reads the zip and changes nothing.')))
        return $steps.ToArray()
    }
    # package.publish
    if (-not $Facts.PublishExists) { $steps.Add((New-PlanStep -Kind refuse -Text ($Config.PublishScript + ' is not there, so there is nothing to run. Its path is PublishScript in Get-ConsoleConfig.'))); return $steps.ToArray() }
    if (-not $Zip) { $steps.Add((New-PlanStep -Kind refuse -Text ('Choose a zip from ' + $Config.PackageDist + ' to publish.'))); return $steps.ToArray() }
    $row = @(@($Facts.Zips) | Where-Object { $_.Name -eq $Zip })
    if ($row.Count -ne 1) { $steps.Add((New-PlanStep -Kind refuse -Text ("There is no zip '" + $Zip + "' in " + $Config.PackageDist + '.'))); return $steps.ToArray() }
    $z = $row[0]
    foreach ($x in @(Get-PackageZipProblems -Zip $z -Picked $Facts.Picked)) { $steps.Add((New-PlanStep -Kind refuse -Text ('Not publishing: ' + $x))) }
    $ver = '(unknown)'; $nt = '(none)'
    if ($z.Json -and $z.Json.Version) { $ver = [string]$z.Json.Version }
    if ($z.Json -and $z.Json.Notes) { $nt = [string]$z.Json.Notes }
    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This publishes ' + $z.Name + ' (version ' + $ver + ') to get.shatteredlegacyuo.com and the website, and its version.json tells every installed launcher to offer it.')))
    $steps.Add((New-PlanStep -Kind say -Text ('zip ' + $z.Name + ', version ' + $ver + ', ' + (Format-Bytes $z.Bytes) + ', built ' + (Format-LocalTime $z.LastWriteUtc) + '. What is new: ' + $nt)))
    $remind = Get-RolloutReminder -Changelog $Facts.Changelog -LatestNote $Facts.LatestNote
    if ($remind) { $steps.Add((New-PlanStep -Kind say -Text $remind)) }
    $steps.Add((New-PlanStep -Kind script -Path $Config.PublishScript -Named ([ordered]@{ Zip = $z.FullName }) -Text 'Publish-PlayerPackage.ps1: shows the zip, asks you to type yes HERE, then gates, copies to Haven, swaps in and checks both URLs, rolling back on a mismatch. Exit 0 is published, 3 is cancelled (not yes), anything else failed.'))
    $steps.Add((New-PlanStep -Kind log -Text ('package.publish ' + $z.Name)))
    $steps.ToArray()
}

# --- the Legion scripts (cc-P144) ------------------------------------------------------------------------------------
# Each button runs one of scripts\legion\'s scripts as it is, or git pull in the clone. The scripts check the clone
# against GitHub and Haven themselves; the console only refuses what cannot run at all (a script or the clone missing).

function Get-ScriptsPlan {
    param([string]$Action, $Config)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $need = @{ 'package.scripts.check' = $Config.LegionPublish; 'package.scripts.dev' = $Config.LegionPublish; 'package.scripts.promote' = $Config.LegionPublish
               'package.scripts.setup' = $Config.LegionSetup; 'package.scripts.key' = $Config.LegionKey }
    if (-not $need.ContainsKey($Action) -and $Action -ne 'package.scripts.pull') { $steps.Add((New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'. -Action list shows them."))); return $steps.ToArray() }
    if ($need.ContainsKey($Action) -and -not (Test-Path -LiteralPath $need[$Action] -PathType Leaf)) { $steps.Add((New-PlanStep -Kind refuse -Text ($need[$Action] + ' is not there, so there is nothing to run.'))); return $steps.ToArray() }
    if ($Action -in @('package.scripts.dev', 'package.scripts.pull') -and -not $Config.LegionRepo) {
        $steps.Add((New-PlanStep -Kind refuse -Text 'This PC has no scripts repo yet. Click Set up scripts repo first.')); return $steps.ToArray()
    }
    switch ($Action) {
        'package.scripts.check' {
            $steps.Add((New-PlanStep -Kind script -Path $Config.LegionPublish -Named ([ordered]@{ Mode = 'Check' }) -Text 'Publish-LegionScripts.ps1 -Mode Check: your repo (fetched from GitHub), the dev channel and the official channel, per script, and whether this PC can publish. Changes nothing.'))
        }
        'package.scripts.dev' {
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This publishes the scripts in ' + $Config.LegionRepo + ' to the dev channel on Haven, which only PCs flagged dev read (yours). It is refused unless that repo is committed and level with GitHub.')))
            $steps.Add((New-PlanStep -Kind script -Path $Config.LegionPublish -Named ([ordered]@{ Mode = 'Dev' }) -Text 'Publish-LegionScripts.ps1 -Mode Dev: lists new, changed and removed scripts, asks you to type yes HERE, then stages, swaps and checks each file back from Haven, scripts.json last, rolling back on a mismatch. Exit 0 is done, 3 is cancelled (not yes), anything else failed.'))
            $steps.Add((New-PlanStep -Kind log -Text 'package.scripts.dev'))
        }
        'package.scripts.promote' {
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text 'This copies dev scripts you pick to the official channel on Haven, which EVERY player''s install pulls on its next start, and tags the commit official-<version> in the scripts repo. The exact dev bytes go out, never this PC''s files.'))
            $steps.Add((New-PlanStep -Kind script -Path $Config.LegionPublish -Named ([ordered]@{ Mode = 'Promote' }) -Text 'Publish-LegionScripts.ps1 -Mode Promote: lists the dev scripts that differ from official with their hashes, asks which (Enter for all) and to type yes HERE, then copies on Haven with the same checks and rollback. Exit 0 is done, 3 is cancelled, anything else failed.'))
            $steps.Add((New-PlanStep -Kind log -Text 'package.scripts.promote'))
        }
        'package.scripts.pull' {
            $steps.Add((New-PlanStep -Kind exec -Exe 'git' -Arguments @('-C', $Config.LegionRepo, 'pull', '--ff-only') -Text ('git pull --ff-only in ' + $Config.LegionRepo + ': what your other PC pushed. A pull that is not a fast-forward stops and says so; nothing is merged for you.')))
            $steps.Add((New-PlanStep -Kind log -Text 'package.scripts.pull'))
        }
        'package.scripts.setup' {
            $steps.Add((New-PlanStep -Kind script -Path $Config.LegionSetup -Text 'Setup-ScriptsRepo.ps1: asks where the clone lives (once), clones it, and links every TazUO client on this PC to it, moving each old LegionScripts folder aside first. Safe to run again.'))
            $steps.Add((New-PlanStep -Kind log -Text 'package.scripts.setup'))
        }
        'package.scripts.key' {
            $steps.Add((New-PlanStep -Kind script -Path $Config.LegionKey -Text 'Setup-PublishKey.ps1: makes this PC''s SSH key if it has none and puts it on Haven (asks Haven''s password once), then proves a login without a password.'))
            $steps.Add((New-PlanStep -Kind log -Text 'package.scripts.key'))
        }
    }
    $steps.ToArray()
}

function Get-PublishOutcome {
    # cc-P68 Part B: what Publish-PlayerPackage.ps1's exit code means. Since 2026-10-05 a declined
    # "Type yes" ("Nothing done.") exits 3; a publish exits 0; a failure 1 or anything else.
    param([int]$Code)
    switch ($Code) {
        0       { [pscustomobject]@{ Result = 'published'; Color = 'green'; Text = 'Published: Publish-PlayerPackage.ps1 exited 0.' } }
        3       { [pscustomobject]@{ Result = 'cancelled'; Color = 'amber'; Text = 'Publish cancelled. Nothing was published.' } }
        default { [pscustomobject]@{ Result = 'failed'; Color = 'red'; Text = ('Publish failed: Publish-PlayerPackage.ps1 exited with ' + $Code + '. Read its last lines above.') } }
    }
}

# --- Test shard > Deploy built image (cc-P63 Part B) ----------------------------------------------

function Get-DeployCandidates {
    # The picker's rows: sl-modernuo:cc-p* only, newest first, and which one latest points at now.
    param($State, $Config)
    $latest = $null
    if ($State.Latest -and $State.Latest.Exists) { $latest = Format-ShortId $State.Latest.Id }
    @(@($State.Images) | Where-Object { $_.Tag -cmatch $Config.DeployTagPattern } | Sort-Object Created -Descending | ForEach-Object {
        [pscustomobject]@{ Tag = $_.Tag; Id = $_.Id; Created = $_.Created; IsLatest = [bool]($latest -and $_.Id -eq $latest) }
    })
}

function Get-DeployLiveLine {
    # The one plain line in the deploy confirmation about live. Read from what the live container was
    # created from, not assumed: on 2026-10-05 sl-modernuo was created from 'uo-modernuo', which
    # docker/uo/docker-compose.yml (build: and no image:, D29) names after its own project.
    param($State, $Config)
    # cc-P108: while live is retired the deploy confirmation does not mention it at all.
    if (Test-LiveRetired $Config) { return $null }
    $c = $State.Shards['live'].Container
    if (-not $c -or -not $c.Exists) { return 'There is no live container (sl-modernuo), so nothing live uses this tag.' }
    if ((ConvertTo-ImageRef $c.ConfigImage) -eq (ConvertTo-ImageRef $Config.LatestTag)) {
        return ('LIVE was created from ' + $Config.LatestTag + '. A restart keeps the image it runs; the next time live is recreated (a deploy) it would get this image.')
    }
    'Live does not use this tag: sl-modernuo was created from ''' + $c.ConfigImage + ''', and a restart keeps the image it runs, so moving ' + $Config.LatestTag + ' changes nothing live. Only the live drift line compares against it.'
}

function Get-DeployPlan {
    param($Config, $State, [string]$ImageTag)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $sh = $Config.Shards['test']
    if (-not $ImageTag) { $steps.Add((New-PlanStep -Kind refuse -Text 'Choose an image to deploy.')); return $steps.ToArray() }
    if ($ImageTag -cnotmatch $Config.DeployTagPattern) { $steps.Add((New-PlanStep -Kind refuse -Text ("'" + $ImageTag + "' is not an image this button deploys: only sl-modernuo:cc-p* tags, as build.sh -t makes them."))); return $steps.ToArray() }
    $pick = @(@($State.Images) | Where-Object { $_.Tag -ceq $ImageTag })
    if ($pick.Count -ne 1) { $steps.Add((New-PlanStep -Kind refuse -Text ('There is no image tagged ' + $ImageTag + ' (docker images sl-modernuo).'))); return $steps.ToArray() }
    $pick = $pick[0]
    $old = $null
    if ($State.Latest -and $State.Latest.Exists) { $old = Format-ShortId $State.Latest.Id }
    if ($old -and $old -eq $pick.Id) { $steps.Add((New-PlanStep -Kind refuse -Text ($Config.LatestTag + ' already is ' + $ImageTag + ' (' + $old + '). Nothing to deploy; Test shard > Start starts the test shard on it.'))); return $steps.ToArray() }
    # Rollback: a cc-p tag on the old image if there is one, else its id.
    $back = 'there was no ' + $Config.LatestTag + ' before this, so there is nothing to roll back to.'
    if ($old) {
        $oldTags = @(@($State.Images) | Where-Object { $_.Id -eq $old -and $_.Tag -cmatch $Config.DeployTagPattern } | ForEach-Object { $_.Tag })
        if ($oldTags.Count) { $back = 'deploy ' + $oldTags[0] + ' (' + $old + ').' }
        else { $back = 'docker tag ' + $old + ' ' + $Config.LatestTag + ', then Test shard > Start (no cc-p tag names ' + $old + ').' }
    }
    $was = '(none)'
    if ($old) { $was = $old + ', built ' + (Format-LocalTime $State.Latest.Created) }
    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase 'test.deploy') -Text ('This points ' + $Config.LatestTag + ' at ' + $ImageTag + ' (' + $pick.Id + ', built ' + (Format-LocalTime $pick.Created) + ') and restarts the TEST shard on it. ' + (Get-DeployLiveLine $State $Config))))
    $steps.Add((New-PlanStep -Kind say -Text ($Config.LatestTag + ' before this deploy: ' + $was + '.')))
    $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('tag', $ImageTag, $Config.LatestTag) -Text ('Point ' + $Config.LatestTag + ' at ' + $ImageTag + '. A tag only: no build, no gates (they ran when ' + $ImageTag + ' was built).')))
    $steps.Add((New-PlanStep -Kind say -Text ('To roll back: ' + $back)))
    $sts = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Config.StartTestShard)
    $noSave = $script:NoSaveWarning
    if ($State.Shards['test'].SavesOnStop) { $noSave = $script:SavesOnStopNote }
    $steps.Add((New-PlanStep -Kind exec -Exe $Config.PowerShell -Arguments ($sts + '-Down') -Text ('docker/uo/Start-TestShard.ps1 -Down: compose down on the sl-uo-test project only (D22). ' + $noSave)))
    $steps.Add((New-PlanStep -Kind exec -Exe $Config.PowerShell -Arguments ($sts + '-SkipBuild') -Text ('docker/uo/Start-TestShard.ps1 -SkipBuild: ' + $sh.Container + ' on the new ' + $Config.LatestTag + ', then waits for the listener.')))
    $steps.Add((New-PlanStep -Kind log -Text ('test.deploy ' + $ImageTag + ' (' + $pick.Id + '), ' + $Config.LatestTag + ' was ' + $was)))
    $steps.ToArray()
}

# --- the test account wipe (cc-P73) ------------------------------------------------------------
# Test shard > Wipe test accounts. Every account and character on the TEST shard goes, with what they
# own; the world stays. The wipe itself is server/customizations/Misc/TestAccountWipe.cs, run by the
# shard at its next start from a request file in the test world folder. The console only stops the
# shard, backs up the save, places the request and starts it again. Not Start fresh: that replaces the
# whole world and copies the LIVE accounts in (D19); this keeps the world and copies nothing from live.
# The password is asked for in the action's own window, never on a command line, and only a PBKDF2 hash
# of it is written (New-WipePasswordHash). The census and outcome lines are TestAccountWipe.cs's
# CensusLine, DoneLine, RefusedLine and FailedLine: change both together.

$script:WipeTag = '[AccountWipe]'

function Test-WipeAdminName {
    # Returns why a new admin name is refused, or $null. A subset of what the shard accepts
    # (AccountHandler.IsValidUsername: no <>:"/\|?*, no space at either end, no final dot), so it also
    # crosses a command line as one word.
    param([string]$Name)
    if (-not $Name) { return 'name the new admin account' }
    if ($Name.Length -gt 30) { return 'account names here are at most 30 characters' }
    if ($Name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { return 'use letters, digits, dot, dash and underscore, starting with a letter or digit' }
    if ($Name.EndsWith('.')) { return 'an account name cannot end with a dot (the shard refuses it)' }
    $null
}

function New-WipePasswordHash {
    # The hash the shard installs on the new admin: pinned's PBKDF2PasswordProtection format
    # (UOContent/Accounting/Security/PBKDF2PasswordProtection.cs): iterations as two bytes little-endian,
    # an 8-byte salt, then 32 bytes of PBKDF2-HMAC-SHA256 over the UTF-8 password, as UPPERCASE hex
    # (HexStringConverter.GetBytes reads only A-F). The first login rehashes it to Argon2.
    param([Parameter(Mandatory = $true)][string]$Password, [byte[]]$Salt, [int]$Iterations = 0)
    if (-not $Salt) { $Salt = New-Object byte[] 8; ([Security.Cryptography.RandomNumberGenerator]::Create()).GetBytes($Salt) }
    if ($Salt.Length -ne 8) { throw 'the salt is 8 bytes' }
    if ($Iterations -eq 0) { $Iterations = 1024 + (Get-Random -Minimum 0 -Maximum 513) }
    if ($Iterations -lt 1024 -or $Iterations -gt 1536) { throw 'iterations are 1024 to 1536, as the shard writes them' }
    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes($Password, $Salt, $Iterations, [Security.Cryptography.HashAlgorithmName]::SHA256)
    try { $hash = $kdf.GetBytes(32) } finally { $kdf.Dispose() }
    $out = New-Object byte[] 42
    $out[0] = [byte]($Iterations -band 0xFF); $out[1] = [byte](($Iterations -shr 8) -band 0xFF)
    [Array]::Copy($Salt, 0, $out, 2, 8)
    [Array]::Copy($hash, 0, $out, 10, 32)
    -join ($out | ForEach-Object { $_.ToString('X2') })
}

function New-WipeRequestText {
    # The request file, as TestAccountWipe.cs reads it. It names the admin and carries the hash,
    # never the password. The id lets the shard recognise a request it has already carried out.
    param([string]$Admin, [string]$Hash, [string]$Id, [datetime]$Now, [string]$By)
    $o = [ordered]@{
        version        = 1
        id             = $Id
        admin          = $Admin
        passwordPbkdf2 = $Hash
        requestedAt    = $Now.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', $script:Invariant)
        requestedBy    = $By
    }
    $o | ConvertTo-Json
}

function ConvertFrom-WipeLog {
    # What the shard said about wipes in a docker log (with or without --timestamps): the newest
    # census, and how the newest request ended. Outcome is done, refused, failed, already or $null.
    param([string]$Text)
    $r = [ordered]@{ Census = $null; Outcome = $null; Line = $null; Audit = $null }
    $tag = [regex]::Escape($script:WipeTag)
    foreach ($l in @((Remove-Ansi $Text) -split "`n")) {
        $at = $null
        if ($l -match '^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z) ') { $at = ConvertFrom-DockerTime $Matches[1] }
        if ($l -match ($tag + ' census: (\d+) accounts?, (\d+) characters?, (\d+) houses?')) {
            $r.Census = [pscustomobject]@{ Accounts = [int]$Matches[1]; Characters = [int]$Matches[2]; Houses = [int]$Matches[3]; At = $at }
        }
        $i = $l.IndexOf($script:WipeTag)
        if ($i -lt 0) { continue }
        $msg = $l.Substring($i).Trim()
        if ($msg -match ($tag + ' audit: ')) { $r.Audit = $msg }
        elseif ($msg -match ($tag + ' DONE: ')) { $r.Outcome = 'done'; $r.Line = $msg }
        elseif ($msg -match ($tag + ' REFUSED: ')) { $r.Outcome = 'refused'; $r.Line = $msg }
        elseif ($msg -match ($tag + ' FAILED: ')) { $r.Outcome = 'failed'; $r.Line = $msg }
        elseif ($msg -match ($tag + ' ALREADY DONE: ')) { $r.Outcome = 'already'; $r.Line = $msg }
    }
    [pscustomobject]$r
}

function Get-WipeRefusals {
    # Why the wipe cannot go ahead, from the facts Get-WipeFacts read. Empty means it can.
    param($Config, $State, $Facts)
    $out = New-Object 'System.Collections.Generic.List[string]'
    if (-not $Facts) { $out.Add('The test shard''s state was not read, so nothing is run.'); return $out.ToArray() }
    if ($Facts.IsSystem) {
        $out.Add('This console is running as ' + $Facts.Identity + '. The wipe asks for the new admin password in its own window and writes into the test world folder as you; run the console from your own Windows login.')
    }
    $sh = $Config.Shards['test']
    $c = $State.Shards['test'].Container
    if (-not ($c.Exists -and $c.Running)) {
        $out.Add('The test shard is not running, so it cannot say how many accounts and characters it has. Start it (Test shard > Start), then wipe.')
    } elseif (-not $Facts.Census) {
        $out.Add('The running test shard has not reported its accounts and characters (no "' + $script:WipeTag + ' census" line in its log since it started). Its image has no wipe (cc-P73): deploy one that does (Test shard > Deploy built image), then wipe.')
    }
    if ($Facts.RequestExists) {
        $out.Add('A wipe request is already waiting at ' + $Config.WipeRequestFile + '. The shard carries it out at its next start; start the test shard, or delete that file, before asking again.')
    }
    $need = [long]($State.Shards['test'].Save.Bytes * 1.1) + 1GB
    if (-not $State.Shards['test'].Save.Exists) {
        $out.Add('There is no test save at ' + $sh.Saves + ' to wipe.')
    } elseif ($null -eq $Facts.FreeBytes) {
        $out.Add('Could not read the free space on the drive of ' + $sh.Saves + ', so the backup is not started.')
    } elseif ($Facts.FreeBytes -lt $need) {
        $out.Add('The backup of the test save needs ' + (Format-Bytes $need) + ' free (the save is ' + (Format-Bytes $State.Shards['test'].Save.Bytes) + ', plus room for the next save) and the drive has ' + (Format-Bytes $Facts.FreeBytes) + '.')
    }
    $out.ToArray()
}

function Get-WipeSummary {
    # What goes, what stays, and how it differs from Start fresh. Said in the dialogs and the plan.
    param($Facts, [string]$Admin)
    $n = $Facts.Census
    $when = ''
    if ($n.At) { $when = ' (as the test shard reported at ' + (Format-LocalTime $n.At) + ')' }
    $who = 'the new admin'
    if ($Admin) { $who = "'" + $Admin + "'" }
    'This deletes all ' + $n.Accounts + ' accounts and ' + $n.Characters + ' characters on the TEST shard' + $when + ', aetherion included, with everything they own: packs, banks, pets and stabled pets, vendors, boats and their ' + $n.Houses + ' houses with what is in them, and their League, achievements, discoveries and recipes. ' +
    'The world stays: spawners, decorations, server-placed buildings, staff travel spots and the NPC guilds. ' +
    'The shard then makes one Owner account, ' + $who + ', whose password is asked for in the next window. ' +
    'This is not Start fresh: Start fresh replaces the whole test world with a new one and copies the LIVE accounts in; this keeps the world and copies nothing from live.'
}

function Get-WipePlan {
    param($Config, $State, $Facts, [string]$Admin, [datetime]$Now)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    foreach ($x in @(Get-WipeRefusals -Config $Config -State $State -Facts $Facts)) { $steps.Add((New-PlanStep -Kind refuse -Text $x)) }
    if ($steps.Count) { return $steps.ToArray() }
    $bad = Test-WipeAdminName $Admin
    if ($bad) { $steps.Add((New-PlanStep -Kind refuse -Text ("New admin '" + $Admin + "': " + $bad))); return $steps.ToArray() }
    $sh = $Config.Shards['test']
    $ss = $State.Shards['test']
    $ps = $Config.PowerShell
    $sts = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Config.StartTestShard)
    $aside = New-AsideName $sh.Saves $Now
    $stopNote = $script:NoSaveWarning
    if ($ss.SavesOnStop) { $stopNote = $script:SavesOnStopNote }
    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase 'test.wipe') -Text (Get-WipeSummary $Facts $Admin)))
    $steps.Add((New-PlanStep -Kind secret -Named ([ordered]@{ Admin = $Admin }) -Text ("The new admin password for '" + $Admin + "', typed twice and not shown. Nothing has changed yet: leave it empty to cancel. Only a PBKDF2 hash of it is written, never the password.")))
    $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments ($sts + '-Down') -Text ('docker/uo/Start-TestShard.ps1 -Down: stops the test shard (compose down on sl-uo-test only, D22). ' + $stopNote)))
    $steps.Add((New-PlanStep -Kind stopped -Container $sh.Container -Text ('Checking ' + $sh.Container + ' is not running.')))
    $steps.Add((New-PlanStep -Kind copy -From $sh.Saves -To $aside -Text 'Backing up the whole test save beside it, accounts included. The wipe can be undone by putting this back (Snapshots or Explorer).'))
    $steps.Add((New-PlanStep -Kind verify -From $sh.Saves -To $aside -Text 'Checking the backup has the same files and bytes.'))
    $steps.Add((New-PlanStep -Kind wiperequest -Path $Config.WipeRequestFile -Named ([ordered]@{ Admin = $Admin }) -Text ("Placing the wipe request for admin '" + $Admin + "'. Up to here nothing in the world has changed; from here the shard wipes at its next start.")))
    $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments ($sts + '-SkipBuild') -Text 'docker/uo/Start-TestShard.ps1 -SkipBuild: starts the test shard, which wipes before it listens, then waits for the listener.'))
    $steps.Add((New-PlanStep -Kind wipewait -Container $sh.Container -Path $Config.WipeRequestFile -Text ('Waiting up to ' + $Config.WipeWaitSeconds + ' seconds for the shard to say the wipe is saved, and showing its counts.')))
    $steps.Add((New-PlanStep -Kind log -Text ('test.wipe admin=' + $Admin + ', backup ' + $aside)))
    $steps.ToArray()
}

# --- cc-P96: Fix test shard ----------------------------------------------------------------------------------
# Chase, 2026-10-14 07:11: "we need a test shard fix button. it will not start right now." That morning the
# 07:07 deploy built HEAD, the shard threw at start (bug-list D89) and sat at "press return to exit" while
# Docker said Up. The tab: Diagnose (reads), Restart, Roll back to last good, Rebuild a commit, Prune images.
# The health and crash judgments are scripts/Shard-Health.ps1's, shared with Start-TestShard.ps1, which tags
# sl-modernuo:good-<commit> after a healthy start (Part C) and writes shard-commit.json in the test world.

function ConvertFrom-FixConfig {
    # scripts/fix-test-shard.json: known crash signatures, how many good tags prune keeps, and tags it never offers.
    # A missing or unreadable file gives the defaults, with D89's signature, so the tile still names it.
    param([string]$Json)
    $cfg = [pscustomobject]@{
        KnownCrashes = @([pscustomobject]@{ Match = 'ImbueCatalogue..cctor'; Bug = 'D89'; Title = 'the Imbuing catalogue read the skill table too early' })
        KeepGood     = 5
        KeepTags     = @()
        # cc-P106: for a question's delete answers, what is lost when the object or file named contains Match.
        QuestionLosses = @([pscustomobject]@{ Match = 'ClusterFAccountPersistence'; Lost = $script:D94Loss })
        Error        = $null
    }
    if (-not $Json) { return $cfg }
    try { $j = $Json | ConvertFrom-Json } catch { $cfg.Error = 'fix-test-shard.json is not JSON: ' + $_.Exception.Message; return $cfg }
    if ($j.knownCrashes) {
        $cfg.KnownCrashes = @($j.knownCrashes | Where-Object { $_.match -and $_.bug } | ForEach-Object {
            [pscustomobject]@{ Match = [string]$_.match; Bug = [string]$_.bug; Title = [string]$_.title } })
    }
    if ($j.keepGood -is [int] -or $j.keepGood -is [long]) { $cfg.KeepGood = [Math]::Max(1, [int]$j.keepGood) }
    if ($j.keepTags) { $cfg.KeepTags = @($j.keepTags | ForEach-Object { [string]$_ } | Where-Object { $_ }) }
    if ($j.questionLosses) {
        $cfg.QuestionLosses = @($j.questionLosses | Where-Object { $_.match -and $_.lost } | ForEach-Object {
            [pscustomobject]@{ Match = [string]$_.match; Lost = [string]$_.lost } })
    }
    $cfg
}

function ConvertFrom-GitLogLines {
    # git log --format=%H|%cs|%s, one commit per line, newest first.
    param([string]$Text)
    @(foreach ($l in @(([string]$Text -replace "`r", '') -split "`n")) {
        if ($l -match '^([0-9a-f]{40})\|(\d{4}-\d\d-\d\d)\|(.*)$') {
            [pscustomobject]@{ Sha = $Matches[1]; Short = $Matches[1].Substring(0, 7); Date = $Matches[2]; Subject = $Matches[3] }
        }
    })
}

function ConvertFrom-DockerDfImages {
    # `docker system df -v --format {{json .Images}}`: each image's UniqueSize, by 12-character id. $null if unreadable.
    param([string]$Text)
    # Windows PowerShell 5.1 hands a JSON array on as one object, so enumerate it.
    try { $rows = @(([string]$Text).Trim() | ConvertFrom-Json | ForEach-Object { $_ }) } catch { return $null }
    $h = @{}
    foreach ($r in $rows) {
        $id = Format-ShortId ([string]$r.ID)
        $b = ConvertFrom-ImageSize ([string]$r.UniqueSize)
        if ($id -and $null -ne $b) { $h[$id] = $b }
    }
    $h
}

function Get-ImageTagsFor {
    # Every sl-modernuo tag on one image id (short or long), from the state's listing.
    param($State, [string]$Id)
    if (-not $Id) { return @() }
    $short = Format-ShortId $Id
    @(@($State.Images) | Where-Object { $_.Id -eq $short } | ForEach-Object { $_.Tag })
}

function Get-FixDiagnosis {
    # Diagnose's lines: the test container, its health, its image and commit, uptime, the last good image, what
    # last wrote the save, and for a crash its type, message, chain, first five frames and the bug it matches.
    param($Config, $State, $Facts)
    $sh = $Config.Shards['test']
    $ss = $State.Shards['test']
    $c = $ss.Container
    $out = New-Object 'System.Collections.Generic.List[string]'
    $crash = $ss.Crash
    if (-not $c.Exists) {
        $out.Add('Container: there is no ' + $sh.Container + ' container. Rebuild or Roll back creates it.')
    } elseif ($c.Running) {
        $out.Add('Container: ' + $sh.Container + ', running, up ' + (Format-Age ($State.Now - $c.StartedAt)) + ' (started ' + (Format-LocalTime $c.StartedAt) + ')')
    } else {
        $f = ''
        if ($c.FinishedAt) { $f = ', stopped ' + (Format-Age ($State.Now - $c.FinishedAt)) + ' ago' }
        $out.Add('Container: ' + $sh.Container + ', ' + $c.Status + ' (exit code ' + $c.ExitCode + ')' + $f)
    }
    $h = Get-HealthState -Exists $c.Exists -Running $c.Running -Health ([string]$c.Health) -Crash $crash
    $out.Add('Health: ' + $h.State + ' - ' + $h.Detail)
    if ($c.Exists) {
        $tags = @(Get-ImageTagsFor $State $c.ImageId)
        $tagText = 'no sl-modernuo tag'
        if ($tags.Count) { $tagText = $tags -join ', ' }
        $img = 'Image: ' + $tagText + ' (' + (Format-ShortId $c.ImageId) + ')'
        $ri = $ss.RunningImage
        if ($ri -and $ri.Exists -and $ri.Created) { $img += ', built ' + (Format-LocalTime $ri.Created) }
        if ($ri -and $ri.Commit) {
            $img += '; commit ' + $ri.Commit.Substring(0, [Math]::Min(7, $ri.Commit.Length))
            if ($ri.Dirty) { $img += ' with uncommitted changes' }
        } else {
            $fromTag = @($tags | ForEach-Object { Get-GoodTagCommit $_ } | Where-Object { $_ })
            if ($fromTag.Count) { $img += '; commit ' + $fromTag[0] + ' (from its good tag)' }
            else { $img += '; commit unknown (no sl.commit label: built before cc-P96)' }
        }
        $out.Add($img)
    }
    $lg = Get-LastGoodImage $State.Images
    if ($lg) { $out.Add('Last good: ' + $lg.Tag + ' (' + $lg.Id + ', built ' + (Format-LocalTime $lg.Created) + '). Roll back runs it.') }
    else { $out.Add('Last good: none yet. Start-TestShard.ps1 tags sl-modernuo:good-<commit> after each healthy start (cc-P96).') }
    if ($Facts -and $Facts.SaveRecord -and $Facts.SaveRecord.commit) {
        $out.Add('Save last written by: commit ' + ([string]$Facts.SaveRecord.commit).Substring(0, 7) + ' (' + [string]$Facts.SaveRecord.image + ', healthy start at ' + [string]$Facts.SaveRecord.at + ')')
    } else {
        $out.Add('Save last written by: not known (shard-commit.json is written by a healthy start from cc-P96 on).')
    }
    if ($crash -and $crash.Crashed) {
        $where = 'after it had started'
        if ($crash.AtStart) { $where = 'at start' }
        $out.Add('Crashed ' + $where + ': ' + $crash.Type + ': ' + $crash.Message)
        if (@($crash.Chain).Count -gt 1) { $out.Add('Exception chain: ' + ((@($crash.Chain) | ForEach-Object { ($_.Type -split '\.')[-1] }) -join ' -> ')) }
        foreach ($f in @($crash.Frames)) { $out.Add('  at ' + $f) }
        $k = Find-KnownCrash $crash $Config.Fix.KnownCrashes
        if ($k) { $out.Add('Known bug ' + $k.Bug + ': ' + $k.Title + '. Roll back runs the last good image; Rebuild a commit with the fix.') }
        else { $out.Add('No known bug matches. If it is one, add a line of its stack to scripts\fix-test-shard.json (knownCrashes).') }
    } elseif ($h.State -eq 'UNHEALTHY') {
        $out.Add('Not listening, and no crash in the log since this start: tail the log, or Restart.')
    }
    $out.ToArray()
}

function Get-FixPlan {
    param([string]$Action, $Config, $State, $Facts, [string]$ImageTag, [string]$Commit, [datetime]$Now)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $sh = $Config.Shards['test']
    $ss = $State.Shards['test']
    $c = $ss.Container
    $sts = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Config.StartTestShard)
    $noSave = $script:NoSaveWarning
    if ($ss.SavesOnStop) { $noSave = $script:SavesOnStopNote }
    switch ($Action) {
        'fix.diagnose' {
            foreach ($l in (Get-FixDiagnosis -Config $Config -State $State -Facts $Facts)) { $steps.Add((New-PlanStep -Kind say -Text $l)) }
            $steps.Add((New-PlanStep -Kind log -Text 'fix.diagnose'))
        }
        'fix.restart' {
            if (-not $c.Exists) { $steps.Add((New-PlanStep -Kind refuse -Text ('There is no ' + $sh.Container + ' container to restart. Rebuild or Roll back creates one.'))); break }
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This restarts the TEST shard (' + $sh.Container + ') on the image it has, then waits for it to listen or crash. ' + $noSave)))
            $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('restart', '-t', [string]$Config.StopTimeout, $sh.Container) -Text ('docker restart: the same container and the same image. ' + $noSave)))
            $steps.Add((New-PlanStep -Kind healthwait -Container $sh.Container -Seconds 300 -Text 'Waiting for the health check to say healthy, or for a crash in the log.'))
            $steps.Add((New-PlanStep -Kind log -Text 'fix.restart'))
        }
        'fix.rollback' {
            $good = @(@($State.Images) | Where-Object { Get-GoodTagCommit $_.Tag })
            $pick = $null
            if ($ImageTag) { $pick = @($good | Where-Object { $_.Tag -ceq $ImageTag })[0] }
            else { $pick = Get-LastGoodImage $State.Images }
            if (-not $pick) {
                $t = 'There is no sl-modernuo:good-* image to roll back to. Start-TestShard.ps1 tags one after each healthy start (cc-P96); until then, Test shard > Deploy built image runs a cc-p* build.'
                if ($ImageTag) { $t = $ImageTag + ' is not a good-* image in docker images sl-modernuo.' }
                $steps.Add((New-PlanStep -Kind refuse -Text $t)); break
            }
            $imgCommit = Get-GoodTagCommit $pick.Tag
            $dest = Join-Path (Join-Path $Config.SnapshotRoot $sh.Key) (New-SnapshotFolderName ('pre-rollback-' + $imgCommit) $Now)
            $back = 'backs up its save to ' + $dest
            if (-not $ss.Save.Exists) { $back = 'has no save to back up' }
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This stops the TEST shard, ' + $back + ', and starts it on ' + $pick.Tag + ' (' + $pick.Id + ', built ' + (Format-LocalTime $pick.Created) + '). ' + $Config.LatestTag + ' is not moved and nothing live is touched.')))
            if ($Facts -and $Facts.SaveNewer -eq $true) {
                $sc = ([string]$Facts.SaveRecord.commit).Substring(0, 7)
                $steps.Add((New-PlanStep -Kind say -Text ('WARNING: the test save was last written by ' + $sc + ', a newer commit than ' + $imgCommit + '. An older build may not read what a newer one saved (a new type or field). The backup below is the way back.')))
            } elseif (-not $Facts -or $null -eq $Facts.SaveNewer) {
                $steps.Add((New-PlanStep -Kind say -Text ('It is not known which commit last wrote the test save (shard-commit.json is written by a healthy start from cc-P96 on), so whether it is newer than ' + $imgCommit + ' is unknown.')))
            }
            $steps.Add((New-PlanStep -Kind exec -Exe $Config.PowerShell -Arguments ($sts + '-Down') -Text ('docker/uo/Start-TestShard.ps1 -Down, so nothing writes the save while it is copied. ' + $noSave)))
            $steps.Add((New-PlanStep -Kind stopped -Container $sh.Container -Text ('Checking ' + $sh.Container + ' is not running.')))
            if ($ss.Save.Exists) {
                $steps.Add((New-PlanStep -Kind copy -From $sh.Saves -To $dest -Text ('Backing up the test save to ' + $dest)))
                $steps.Add((New-PlanStep -Kind verify -From $sh.Saves -To $dest -Text 'Checking the backup has the same files and bytes.'))
            } else {
                $steps.Add((New-PlanStep -Kind say -Text ('There is no test save at ' + $sh.Saves + ', so there is nothing to back up.')))
            }
            $steps.Add((New-PlanStep -Kind exec -Exe $Config.PowerShell -Arguments ($sts + @('-Image', $pick.Tag)) -Text ('docker/uo/Start-TestShard.ps1 -Image ' + $pick.Tag + ': starts the test shard on it and waits for healthy or a crash.')))
            $steps.Add((New-PlanStep -Kind log -Text ('fix.rollback to ' + $pick.Tag + ' (' + $pick.Id + '); save backed up to ' + $dest)))
        }
        'fix.rebuild' {
            $rows = @()
            if ($Facts) { $rows = @($Facts.Commits) }
            $want = $Commit
            if ($want -eq 'HEAD' -and $Facts) { $want = $Facts.Head }
            $lgc = Get-GoodTagCommit ([string](Get-LastGoodImage $State.Images).Tag)
            $hit = $null
            if ($want -match '^[0-9a-f]{7,40}$') {
                $hit = @($rows | Where-Object { $_.Sha.StartsWith($want) -or ($want.Length -ge 7 -and $want.StartsWith($_.Short)) })[0]
                if (-not $hit -and $lgc -and $want.StartsWith($lgc)) { $hit = [pscustomobject]@{ Sha = $want; Short = $lgc; Subject = 'the last good image''s commit' } }
            }
            if (-not $hit) {
                $steps.Add((New-PlanStep -Kind refuse -Text ('Choose a commit from the list (HEAD, the recent commits, or the last good one): ''' + $Commit + ''' is not one of them.'))); break
            }
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This builds commit ' + $hit.Short + ' (' + $hit.Subject + ') through build.sh and every test gate, in a worktree outside the repo (' + $Config.FixWorktree + '), as sl-modernuo:test-' + $hit.Short + ', then restarts the TEST shard on it and waits for healthy or a crash. Minutes. ' + $Config.LatestTag + ' is not moved; nothing live is touched. ' + $noSave)))
            $steps.Add((New-PlanStep -Kind exec -Exe $Config.PowerShell -Arguments ($sts + @('-Commit', $hit.Sha)) -Text ('docker/uo/Start-TestShard.ps1 -Commit ' + $hit.Short + ': build, start, wait; tagged good-' + $hit.Short + ' if it comes up healthy.')))
            $steps.Add((New-PlanStep -Kind log -Text ('fix.rebuild ' + $hit.Sha)))
        }
        'fix.prune' {
            $protect = @()
            foreach ($k in 'test', 'live') {
                # cc-P108: a retired live has no container to protect (and is never read), so only test's image is.
                if ($Config.Shards[$k].Retired) { continue }
                $cc = $State.Shards[$k].Container
                if ($cc -and $cc.Exists -and $cc.ImageId) { $protect += $cc.ImageId }
            }
            $uniq = $null
            if ($Facts -and $Facts.UniqueSizes) { $uniq = $Facts.UniqueSizes }
            $set = Get-PruneSet -Images $State.Images -KeepGood $Config.Fix.KeepGood -KeepTags $Config.Fix.KeepTags -ProtectIds $protect -UniqueSizes $uniq
            if (-not @($set.Remove).Count) { $steps.Add((New-PlanStep -Kind refuse -Text ('Nothing to prune: every sl-modernuo tag is kept (' + @($set.Keep).Count + ' tags).'))); break }
            # docker images' sizes count every shared layer again, so their sum is only an upper bound; the unique
            # sizes docker system df -v reports are the layers no other image uses, a lower bound.
            $freed = 'up to ' + (Format-Bytes $set.FreedBytes) + ' (full image sizes; most layers are shared, so the real figure is lower)'
            if ($null -ne $set.AtLeastBytes) { $freed = 'at least ' + (Format-Bytes $set.AtLeastBytes) + ' (layers only those images use) and ' + $freed }
            if ($set.SizeUnknown) { $freed += ' (some sizes unknown)' }
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This removes ' + @($set.Remove).Count + ' sl-modernuo tags and keeps ' + @($set.Keep).Count + ', freeing ' + $freed + '. An image that still has another tag only loses this one; layers shared with a kept image stay. uo-modernuo and anything a container runs are never removed.')))
            foreach ($k in $set.Keep) { $steps.Add((New-PlanStep -Kind say -Text ('keep    ' + $k.Tag + '  (' + $k.Why + ')'))) }
            foreach ($r in $set.Remove) {
                $sz = ''
                if ($null -ne $r.Size) { $sz = ', ' + (Format-Bytes $r.Size) }
                $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('rmi', $r.Tag) -Text ('remove ' + $r.Tag + ' (' + $r.Id + $sz + ', built ' + (Format-LocalTime $r.Created) + ')')))
            }
            $steps.Add((New-PlanStep -Kind log -Text ('fix.prune removed ' + @($set.Remove).Count + ' tags, ' + $freed)))
        }
        default { $steps.Add((New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'."))) }
    }
    $steps.ToArray()
}

# --- cc-P135: the Map and Art tab --------------------------------------------------------------------------------
# Four buttons run scripts\validate-art.py (cc-P134) and show its output: the registry check, the body table, a .vd
# check (or a proposal when no body is given) and a hues diff against our own hues.mul. The script reads the client data
# and the art registry and writes nothing; render, its one subcommand that writes PNGs, is never run from here
# (Test-PlanSafety refuses it). Six more print the syntax of the in-game map commands
# (server/customizations/StaffHub/ClusterFMapCheckCommands.cs), which the console cannot run: they are typed in a client,
# as the World setup tab's are. Nothing here reads docker or touches a shard.

function Get-MapCommandSyntax {
    # The six in-game commands: usage and what each says. Kept in step with ClusterFMapCheckCommands.cs by hand; a fact
    # checks each name against that source.
    @(
        [pscustomobject]@{ Key = 'map.footprint'; Command = 'CheckFootprint';     Usage = '[CheckFootprint <width> <height> [maxZSpread=5]'; Then = 'then target the top-left corner'; What = 'CAN or CANNOT a w x h building stand there: the blocked tiles (water, impassable land, an impassable non-surface static within 16 Z) and the land Z spread. Max 64x64.' }
        [pscustomobject]@{ Key = 'map.spots';     Command = 'ValidateSpots';      Usage = '[ValidateSpots <file under Data> [map]';              Then = 'no target';                        What = 'Names each bad spot in a spawn .json, a decoration .cfg (give the map, or Britannia for both) or x,y[,z][,map] lines; at most 500 a file. The full report goes to the map-checks folder beside the saves.' }
        [pscustomobject]@{ Key = 'map.statics';   Command = 'FindStaticProblems'; Usage = '[FindStaticProblems';                                Then = 'then target two corners (max 512x512)'; What = 'Duplicate statics (the same id and Z on a tile), statics buried 10 or more Z under the land, and ids with no tiledata entry.' }
        [pscustomobject]@{ Key = 'map.walkable';  Command = 'Walkable';           Usage = '[Walkable [list|overlay [seconds]|clear]';            Then = 'then target two corners (max 512x512)'; What = 'The tiles a mobile cannot stand on, by cause. overlay marks each with a red rune that deletes itself (default 60 s, max 600; test shard only); clear removes every marker now.' }
        [pscustomobject]@{ Key = 'map.shore';     Command = 'ShoreCheck';         Usage = '[ShoreCheck [stepThreshold]';                         Then = 'then target two corners (max 512x512)'; What = 'Water touching non-water land: coast heights in buckets, the water and shore tile ids, and any water surface outside -120..120.' }
        [pscustomobject]@{ Key = 'map.export';    Command = 'ExportPoints';       Usage = '[ExportPoints <name> [rect|point|clear]';             Then = 'rect: two corners; point: one spot at a time'; What = 'Writes the rectangle (spawnBounds and its centre) or the points as spawn-file JSON to <world folder>\map-checks\<name>.json. [ValidateSpots <name>.json reads it back.' }
    )
}

function Get-MapArtPlan {
    param([string]$Action, $Config, [string]$VdFile, [string]$Body, [string]$HuesFile)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    if ($Action -like 'map.*') {
        $c = @(Get-MapCommandSyntax | Where-Object { $_.Key -eq $Action })[0]
        if (-not $c) { $steps.Add((New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'. -Action list shows them."))); return $steps.ToArray() }
        $steps.Add((New-PlanStep -Kind say -Text ($c.Usage + '   (GameMaster; ' + $c.Then + ')')))
        $steps.Add((New-PlanStep -Kind say -Text $c.What))
        $steps.Add((New-PlanStep -Kind say -Text 'Type it in a client on the shard you mean; the console cannot run in-game commands. It reads and reports, and nothing it places outlives its timer.'))
        return $steps.ToArray()
    }
    $py = $Config.Python
    $va = $Config.ValidateArt
    if (-not (Test-Path -LiteralPath $va -PathType Leaf)) { $steps.Add((New-PlanStep -Kind refuse -Text ('scripts\validate-art.py is not there: ' + $va))); return $steps.ToArray() }
    $note = 'validate-art.py reads the client data and the art registry and writes nothing. Exit 1 means it found problems to read above, not that it failed.'
    $report = [ordered]@{ ReportExit = 1 }
    switch ($Action) {
        'art.registry' {
            $steps.Add((New-PlanStep -Kind say -Text 'Check the art registry: rows registered twice, art ids with EA art in the slot, anim rows mobtypes names or records.json does not route, body rows with no mobtypes entry.'))
            $steps.Add((New-PlanStep -Kind exec -Exe $py -Arguments @($va, 'check-registry') -Text $note -Named $report))
        }
        'art.bodies' {
            $steps.Add((New-PlanStep -Kind say -Text 'The body table: every body the client data names or draws (about 1,300 lines): type, flags, classic or UOP, file, slot, layout, the actions it has frames for, and which line defines it. Copy from the pane, or read the run file in scripts\shard-console-output.'))
            $steps.Add((New-PlanStep -Kind exec -Exe $py -Arguments @($va, 'bodies') -Text $note -Named $report))
        }
        'art.checkvd' {
            if (-not $VdFile) { $steps.Add((New-PlanStep -Kind refuse -Text 'Pick a .vd file first (Browse, or type its path).')); break }
            if ($VdFile -notmatch '(?i)\.vd$' -or -not (Test-Path -LiteralPath $VdFile -PathType Leaf)) { $steps.Add((New-PlanStep -Kind refuse -Text ('Not a .vd file that exists: ' + $VdFile))); break }
            if ($Body) {
                if ($Body -notmatch '^\d{1,5}$') { $steps.Add((New-PlanStep -Kind refuse -Text ('The body is a number in decimal, e.g. 2052: ' + $Body))); break }
                $steps.Add((New-PlanStep -Kind say -Text ('Check ' + (Split-Path -Leaf $VdFile) + ' against body ' + $Body + ': its layout and actions, whether the body is free, where it lands, whether the layout fits, and what mobtypes must say.')))
                $steps.Add((New-PlanStep -Kind exec -Exe $py -Arguments @($va, 'check-vd', $VdFile, $Body) -Text $note -Named $report))
            } else {
                $steps.Add((New-PlanStep -Kind say -Text ('No body given, so propose one: a free server body (from 0x800) and a free anim7 slot of the right layout for ' + (Split-Path -Leaf $VdFile) + ', then the check on it. Registers nothing.')))
                $steps.Add((New-PlanStep -Kind exec -Exe $py -Arguments @($va, 'propose', $VdFile) -Text $note -Named $report))
            }
        }
        'art.hues' {
            if (-not $HuesFile) { $steps.Add((New-PlanStep -Kind refuse -Text 'Pick the other hues.mul first (Browse, or type its path).')); break }
            if ($HuesFile -notmatch '(?i)\.mul$' -or -not (Test-Path -LiteralPath $HuesFile -PathType Leaf)) { $steps.Add((New-PlanStep -Kind refuse -Text ('Not a .mul file that exists: ' + $HuesFile))); break }
            if (-not (Test-Path -LiteralPath $Config.OurHues -PathType Leaf)) { $steps.Add((New-PlanStep -Kind refuse -Text ('Our own hues.mul is not there: ' + $Config.OurHues))); break }
            if ([IO.Path]::GetFullPath($HuesFile) -ieq [IO.Path]::GetFullPath($Config.OurHues)) { $steps.Add((New-PlanStep -Kind refuse -Text 'That is our own hues.mul; pick the other one.')); break }
            $steps.Add((New-PlanStep -Kind say -Text ('Compare ' + $HuesFile + ' with ours (' + $Config.OurHues + '): the hues whose colours differ, and those that differ only in name or table range. Report only; neither file is changed.')))
            $steps.Add((New-PlanStep -Kind exec -Exe $py -Arguments @($va, 'hues-diff', $Config.OurHues, $HuesFile) -Text $note -Named $report))
        }
        default { $steps.Add((New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'. -Action list shows them."))) }
    }
    $steps.ToArray()
}

function Get-ActionPlan {
    # Every button is a plan: a list of steps, built here from the status and nothing else. The
    # shell runs it or, under -DryRun, prints it. So what -DryRun prints is exactly what runs.
    param(
        [string]$Action, [string]$ShardKey = 'test', $Config, $State,
        [string]$SnapshotName, [string]$Snapshot, [datetime]$Now = [datetime]::MinValue,
        [string]$CommitMessage, $CommitFacts,
        [string]$PackageNotes, [string]$PackageZip, $PackageFacts, [string]$ImageTag, [switch]$CommitForce,
        $WipeFacts, [string]$WipeAdmin, $AutopilotFacts, $VpnFacts, $FixFacts, [string]$Commit, [string]$Site,
        [string]$VdFile, [string]$Body, [string]$HuesFile
    )
    # cc-P101: the Websites buttons read websites.json only (it is in $Config).
    if ($Action -like 'web.*') { return (Get-WebsitePlan -Action $Action -Config $Config -Site $Site) }
    # cc-P135: the Map and Art buttons read nothing but the file names they are given.
    if ($Action -like 'art.*' -or $Action -like 'map.*') { return (Get-MapArtPlan -Action $Action -Config $Config -VdFile $VdFile -Body $Body -HuesFile $HuesFile) }
    # cc-P83: the two links read nothing.
    if ($Action -like 'links.*') { return (Get-LinkPlan -Action $Action -Config $Config) }
    # cc-P86: the VPN button reads this PC's adapters, route and services (VpnFacts), never docker.
    if ($Action -eq 'vpn.disconnect') { return (Get-VpnPlan -Config $Config -Facts $VpnFacts) }
    # cc-P74: the Golem buttons read only the Golem's folder (cc-P99: autopilot.* are aliases).
    $Action = Resolve-ActionAlias $Action
    if ($Action -like 'golem.*') { return (Get-AutopilotPlan -Action $Action -Config $Config -Facts $AutopilotFacts -Now $Now) }
    # The commit buttons read git and the notes, not docker, so they need no $State (cc-P60).
    if ($Action -like 'commit.*') { return (Get-CommitPlan -Action $Action -Config $Config -Facts $CommitFacts -Message $CommitMessage -Force:$CommitForce) }
    # Nor do the package buttons; they read git, dist\ and the notes (cc-P63, cc-P68).
    if ($Action -in @('package.build', 'package.publish', 'package.check')) { return (Get-PackagePlan -Action $Action -Config $Config -Facts $PackageFacts -Notes $PackageNotes -Zip $PackageZip) }
    # cc-P144: the Legion scripts row reads only its config (the scripts' paths and this PC's clone).
    if ($Action -like 'package.scripts.*') { return (Get-ScriptsPlan -Action $Action -Config $Config) }
    if ($Action -eq 'test.deploy') { return (Get-DeployPlan -Config $Config -State $State -ImageTag $ImageTag) }
    if ($Now -eq [datetime]::MinValue) { $Now = $State.Now }
    # cc-P108: while live is retired, every action on it refuses before anything is read or planned.
    if ((Test-LiveRetired $Config) -and (Test-LiveActionKey -Action $Action -ShardKey $ShardKey)) {
        return @(New-PlanStep -Kind refuse -Text $Config.Live.Refusal)
    }
    if ($Action -eq 'test.wipe') { return (Get-WipePlan -Config $Config -State $State -Facts $WipeFacts -Admin $WipeAdmin -Now $Now) }
    # cc-P96: the Fix test shard tab. Its facts are git's and the test world's save record (Get-FixFacts).
    # cc-P106: the Shard question tab, for the shard picked on it (-Shard).
    if ($Action -like 'question.*') { return (Get-QuestionPlan -Action $Action -ShardKey $ShardKey -Config $Config -State $State -Now $Now) }
    if ($Action -like 'fix.*') { return (Get-FixPlan -Action $Action -Config $Config -State $State -Facts $FixFacts -ImageTag $ImageTag -Commit $Commit -Now $Now) }
    if ($Action -like 'live.*') { $ShardKey = 'live' }
    if ($Action -like 'test.*') { $ShardKey = 'test' }
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $sh  = $Config.Shards[$ShardKey]
    $ss  = $State.Shards[$ShardKey]
    $c   = $ss.Container
    $ps  = $Config.PowerShell
    $sts = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Config.StartTestShard)
    $stopWait = [string]$Config.StopTimeout
    $snapDir = Join-Path $Config.SnapshotRoot $sh.Key
    $noSave = $script:NoSaveWarning
    if ($ss.SavesOnStop) { $noSave = $script:SavesOnStopNote }

    $isLiveOp = ($Action -like 'live.*') -or ($sh.IsLive -and $Action -in @('snapshot.create', 'snapshot.restore', 'snapshot.delete'))
    if ($isLiveOp) {
        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This acts on the LIVE shard (' + $sh.Container + ').')))
    }

    # Rule 1: nothing copies or replaces a save while the server can write it. Stop what is
    # running, check it stopped, and afterwards start only what was running before.
    $wasRunning = [bool]($c.Exists -and $c.Running)
    $kick = ''
    if ($sh.IsLive) { $kick = ' Players are disconnected.' }
    $addStop = {
        if ($wasRunning) {
            $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('stop', '-t', $stopWait, $sh.Container) -Text ('Stopping ' + $sh.Container + ' so nothing writes the save while it is copied. It is started again at the end. ' + $noSave + $kick)))
            $steps.Add((New-PlanStep -Kind stopped -Container $sh.Container -Text ('Checking ' + $sh.Container + ' really is stopped.')))
        } elseif ($c.Exists) {
            $steps.Add((New-PlanStep -Kind say -Text ($sh.Container + ' is ' + $c.Status + ', so nothing is writing the save. It stays stopped.')))
        } else {
            $steps.Add((New-PlanStep -Kind say -Text ('There is no ' + $sh.Container + ' container, so nothing is writing the save.')))
        }
    }
    $addStart = {
        if ($wasRunning) {
            $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('start', $sh.Container) -Text ('Starting ' + $sh.Container + ' again, as it was before. Same container, same image.')))
        }
    }

    switch ($Action) {
        'test.start' {
            $steps.Add((New-PlanStep -Kind say -Text 'Start the test shard on the image already tagged sl-modernuo:latest: no build, no gates. Start-TestShard.ps1 recreates the container if that image has changed, then waits for the listener and reports the owner prompt if the world has no accounts.'))
            $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments ($sts + '-SkipBuild') -Text 'docker/uo/Start-TestShard.ps1 -SkipBuild'))
            $steps.Add((New-PlanStep -Kind log -Text 'test.start'))
        }
        'test.fresh' {
            # cc-P108: a new test world took its accounts from the live save, which went with live. Without them it blocks
            # at the owner account prompt for ever (D19), so this refuses rather than make a world nobody can log in to.
            if (Test-LiveRetired $Config) {
                $steps.Add((New-PlanStep -Kind refuse -Text ('Start fresh copied the accounts from the live save, and ' + $Config.Live.Refusal + ' A new world with no accounts blocks at the owner account prompt (D19). Wipe test accounts keeps the world and makes one new admin instead.')))
                break
            }
            $aside = New-AsideName $sh.Saves $Now
            $steps.Add((New-PlanStep -Kind say -Text ('Start fresh. Start-TestShard.ps1 -Fresh would DELETE the test save (modernuo-test\Saves), and no button here deletes a world save, so instead: stop the test shard, move that save aside, and start without -Fresh. Start-TestShard.ps1 then finds no test save and seeds a new one exactly as -Fresh does (accounts and SerializedTypes.db from the live save, D19). No build.')))
            $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments ($sts + '-Down') -Text 'docker/uo/Start-TestShard.ps1 -Down'))
            $steps.Add((New-PlanStep -Kind stopped -Container $sh.Container -Text ('Checking ' + $sh.Container + ' is not running.')))
            $steps.Add((New-PlanStep -Kind move -From $sh.Saves -To $aside -IfExists -Text 'Moving the test save aside (if there is one). It is kept, not deleted.'))
            $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments ($sts + '-SkipBuild') -Text 'docker/uo/Start-TestShard.ps1 -SkipBuild'))
            $steps.Add((New-PlanStep -Kind log -Text ('test.fresh, old save moved to ' + $aside)))
        }
        'test.stop' {
            $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments ($sts + '-Down') -Text ('docker/uo/Start-TestShard.ps1 -Down: compose down on the sl-uo-test project only (D22). ' + $noSave)))
            $steps.Add((New-PlanStep -Kind log -Text 'test.stop'))
        }
        'test.rebuild' {
            $steps.Add((New-PlanStep -Kind say -Text 'Rebuild through the gates, then start. build.sh runs the patch gate, then the shard tests, then builds the image; if a gate fails nothing is started. This takes minutes.'))
            $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments $sts -Text 'docker/uo/Start-TestShard.ps1 (no switches: build.sh, then start)'))
            $steps.Add((New-PlanStep -Kind log -Text 'test.rebuild'))
        }
        { $_ -in @('test.tail', 'live.tail') } {
            $steps.Add((New-PlanStep -Kind window -Exe 'docker' -Arguments @('logs', '-f', '--tail', '200', $sh.Container) -Text ('Following the ' + $sh.Container + ' log in its own window. Read-only; close the window to stop.')))
        }
        { $_ -in @('test.client', 'live.client') } {
            $steps.Add((New-PlanStep -Kind launch -Path $sh.Client -Text ('Opening a client for the ' + $sh.Label + ' shard.')))
        }
        'live.start' {
            if (-not $c.Exists) {
                $steps.Add((New-PlanStep -Kind refuse -Text 'There is no sl-modernuo container, and this console will not create one. docker/uo/docker-compose.yml has build: and no image: (bug-list D29), so compose could build an image with no test gate, and creating the live container is a deploy, which is Chase''s decision. See notes/deploy-readiness.md.'))
            } elseif ($c.Running) {
                $steps.Add((New-PlanStep -Kind refuse -Text 'sl-modernuo is already running. Nothing to do.'))
            } else {
                $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('start', $sh.Container) -Text 'docker start on the existing container: the same container and the same image it had. Never compose (D29).'))
                $steps.Add((New-PlanStep -Kind log -Text 'live.start'))
            }
        }
        'live.stop' {
            if (-not ($c.Exists -and $c.Running)) {
                $steps.Add((New-PlanStep -Kind refuse -Text ('sl-modernuo is not running (' + $c.Status + '). Nothing to do.')))
            } else {
                $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('stop', '-t', $stopWait, $sh.Container) -Text ('docker stop, waiting up to ' + $stopWait + ' seconds for a save in progress to finish. ' + $noSave + ' Players are disconnected.')))
                $steps.Add((New-PlanStep -Kind log -Text 'live.stop'))
            }
        }
        'live.restart' {
            if (-not $c.Exists) {
                $steps.Add((New-PlanStep -Kind refuse -Text 'There is no sl-modernuo container to restart. This console will not create one (D29).'))
            } else {
                $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('restart', '-t', $stopWait, $sh.Container) -Text ('docker restart: the same container and the SAME IMAGE. It deploys nothing and the drift line will not change. ' + $noSave + ' Players are disconnected.')))
                $steps.Add((New-PlanStep -Kind log -Text 'live.restart'))
            }
        }
        'snapshot.create' {
            $bad = Test-SnapshotLabel $SnapshotName
            if ($bad) { $steps.Add((New-PlanStep -Kind refuse -Text ("Snapshot name '" + $SnapshotName + "': " + $bad))); break }
            if (-not $ss.Save.Exists) { $steps.Add((New-PlanStep -Kind refuse -Text ('There is no save at ' + $sh.Saves + ' to snapshot.'))); break }
            $dest = Join-Path $snapDir (New-SnapshotFolderName $SnapshotName $Now)
            & $addStop
            $steps.Add((New-PlanStep -Kind copy -From $sh.Saves -To $dest -Text ('Copying the ' + $sh.Label + ' save, accounts included, to the snapshot.')))
            $steps.Add((New-PlanStep -Kind verify -From $sh.Saves -To $dest -Text 'Checking the copy has the same files and bytes.'))
            & $addStart
            $steps.Add((New-PlanStep -Kind log -Text ('snapshot.create ' + $sh.Key + ' -> ' + $dest)))
        }
        'snapshot.list' {
            $list = @($ss.Snapshots)
            if ($list.Count -eq 0) { $steps.Add((New-PlanStep -Kind say -Text ('No snapshots of the ' + $sh.Label + ' shard in ' + $snapDir + '.'))) }
            foreach ($s in $list) {
                $acc = 'accounts'
                if (-not $s.HasAccounts) { $acc = 'NO ACCOUNTS' }
                $steps.Add((New-PlanStep -Kind say -Text ('{0}  {1}  {2}  {3} files  {4}' -f $s.Name, $s.Taken.ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant), (Format-Bytes $s.Bytes), $s.Files, $acc)))
            }
        }
        'snapshot.restore' {
            if (-not $Snapshot) { $steps.Add((New-PlanStep -Kind refuse -Text 'Choose a snapshot to restore.')); break }
            $snap = @(@($ss.Snapshots) | Where-Object { $_.Name -eq $Snapshot })
            if ($snap.Count -eq 0) { $steps.Add((New-PlanStep -Kind refuse -Text ("There is no snapshot '" + $Snapshot + "' of the " + $sh.Label + ' shard.'))); break }
            $src   = Join-Path $snapDir $Snapshot
            $pre   = Join-Path $snapDir (New-PreRestoreFolderName $Snapshot $Now)
            $aside = New-AsideName $sh.Saves $Now
            $steps.Add((New-PlanStep -Kind say -Text ('Restoring ' + $Snapshot + ' onto the ' + $sh.Label + ' shard. The current save is snapshotted first, then moved aside. Nothing is deleted.')))
            if (-not $snap[0].HasAccounts) {
                $steps.Add((New-PlanStep -Kind say -Text 'WARNING: this snapshot has no Accounts folder. The shard will start with no accounts and block at the owner prompt (D19).'))
            }
            & $addStop
            if ($ss.Save.Exists) {
                $steps.Add((New-PlanStep -Kind copy -From $sh.Saves -To $pre -Text 'The automatic pre-restore snapshot of the current save.'))
                $steps.Add((New-PlanStep -Kind verify -From $sh.Saves -To $pre -Text 'Checking the pre-restore snapshot has the same files and bytes.'))
                $steps.Add((New-PlanStep -Kind move -From $sh.Saves -To $aside -Text 'Moving the current save aside. It is kept, not deleted.'))
            } else {
                $steps.Add((New-PlanStep -Kind say -Text ('There is no current save at ' + $sh.Saves + ', so there is nothing to snapshot or move aside.')))
            }
            $steps.Add((New-PlanStep -Kind copy -From $src -To $sh.Saves -Text ('Copying ' + $Snapshot + ' in as the save.')))
            $steps.Add((New-PlanStep -Kind verify -From $src -To $sh.Saves -Text 'Checking the restored save has the same files and bytes as the snapshot.'))
            & $addStart
            $steps.Add((New-PlanStep -Kind log -Text ('snapshot.restore ' + $sh.Key + ' <- ' + $Snapshot + '; pre-restore ' + $pre + '; old save ' + $aside)))
        }
        'snapshot.delete' {
            if (-not $Snapshot) { $steps.Add((New-PlanStep -Kind refuse -Text 'Choose a snapshot to delete.')); break }
            $snap = @(@($ss.Snapshots) | Where-Object { $_.Name -eq $Snapshot })
            if ($snap.Count -eq 0) { $steps.Add((New-PlanStep -Kind refuse -Text ("There is no snapshot '" + $Snapshot + "' of the " + $sh.Label + ' shard.'))); break }
            $warn = Get-SnapshotDeleteWarning -Snapshots $ss.Snapshots -Name $Snapshot -Shard $sh
            $steps.Add((New-PlanStep -Kind ask -Text (('Delete the snapshot ' + $Snapshot + ' of the ' + $sh.Label + ' shard (' + (Format-Bytes $snap[0].Bytes) + ')? ' + $warn).Trim())))
            $steps.Add((New-PlanStep -Kind remove-snapshot -Path (Join-Path $snapDir $Snapshot) -Text 'Deleting the snapshot folder. A snapshot, never a world save.'))
            $steps.Add((New-PlanStep -Kind log -Text ('snapshot.delete ' + $sh.Key + ' ' + $Snapshot)))
        }
        'diag.docker' {
            $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Config.CheckDocker) -Text ('Check-Docker.ps1: writes check-HHmmss.txt in ' + $Config.DiagFolder)))
        }
        'diag.shard' {
            $steps.Add((New-PlanStep -Kind exec -Exe $ps -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Config.CheckShard) -Text ('Check-Shard.ps1: writes shard-HHmmss.txt in ' + $Config.DiagFolder + '. It reads the TEST container only.')))
        }
        'diag.folder' {
            $steps.Add((New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($Config.DiagFolder) -Text 'Opening the folder the diagnostics write to.'))
        }
        default {
            $steps.Add((New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'. -Action list shows them.")))
        }
    }
    $steps.ToArray()
}

function Test-PlanSafety {
    # The rules that hold whatever a plan says. Returns the reasons a plan is refused, if any.
    param($Plan, $Config)
    $v = New-Object 'System.Collections.Generic.List[string]'
    $plan = @($Plan)
    $confirmed = @($plan | Where-Object { $_.Kind -eq 'confirm' }).Count -gt 0
    $live = $Config.Shards['live']
    $saves = @($Config.Shards.Values | ForEach-Object { [IO.Path]::GetFullPath($_.Saves).TrimEnd('\') })
    $snapRoot = [IO.Path]::GetFullPath($Config.SnapshotRoot).TrimEnd('\') + '\'
    $forbidden = @('build', 'buildx', 'compose', 'rm', 'rmi', 'system', 'volume', 'image', 'container', 'commit', 'run', 'create', 'kill', 'prune')
    foreach ($s in $plan) {
        $words = @(@($s.Exe) + @($s.Arguments) | Where-Object { $_ })
        $line = $words -join ' '
        # cc-P86: a VPN is never ended by stopping its service, killing a process or disabling an adapter: Proton's
        # kill switch can leave the PC offline, and a disabled adapter stays disabled. A WireGuard tunnel is ended only
        # by its own wireguard.exe /uninstalltunnelservice <tunnel>.
        if ($s.Kind -in @('exec', 'window', 'launch') -and ($line + ' ' + [string]$s.Path) -match '(?i)ProtonVPNService|ProtonVPN\.WireGuardService|RestoreInternet|taskkill|\bnet(\.exe)?\s+stop|Stop-Service|Stop-Process|Disable-NetAdapter|netsh\s+interface') {
            $v.Add('a VPN is never ended by stopping a service, killing a process or disabling an adapter: ' + ($line + ' ' + [string]$s.Path).Trim())
        }
        if ($s.Kind -eq 'exec' -and [string]$s.Exe -match '(?i)\\wireguard\.exe$' -and -not (@($s.Arguments).Count -eq 2 -and $s.Arguments[0] -ceq '/uninstalltunnelservice' -and [string]$s.Arguments[1] -match '^[A-Za-z0-9_=+.-]{1,32}$')) {
            $v.Add('wireguard.exe runs here only as /uninstalltunnelservice <tunnel>: ' + $line)
        }
        if ($s.Kind -in @('exec', 'window')) {
            if ($line -match '\bcompose\b') { $v.Add('runs docker compose itself; compose is reached only through Start-TestShard.ps1: ' + $line) }
            if ($s.Exe -eq 'docker' -and @($s.Arguments).Count -and $s.Arguments[0] -ceq 'rmi') {
                # cc-P96, Prune images: the one removal this console makes. One sl-modernuo:<tag> per step, never -f (so
                # docker itself refuses an image a container uses), never latest, rollback-*, pre-bump-* or a kept tag,
                # only after the phrase, and in a plan that names nothing live.
                $ra = @($s.Arguments)
                $keepNamed = @(@($Config.Fix.KeepTags) | ForEach-Object { if ([string]$_ -like 'sl-modernuo:*') { [string]$_ } else { 'sl-modernuo:' + $_ } })
                if ($ra.Count -ne 2 -or [string]$ra[1] -cnotmatch '^sl-modernuo:[A-Za-z0-9._-]+$' -or $ra[1] -ceq $Config.LatestTag -or [string]$ra[1] -cmatch '^sl-modernuo:(rollback-|pre-bump-)' -or $keepNamed -ccontains $ra[1]) {
                    $v.Add('the only image this console removes is one sl-modernuo:<tag> per step, never latest, rollback-*, pre-bump-* or a kept tag, and never with -f: ' + $line)
                }
                if (-not $confirmed) { $v.Add('removes an image without a typed confirmation: ' + $line) }
                $liveWords = @($plan | ForEach-Object { @($_.Arguments) + @($_.Container) + @($_.Path) } | Where-Object { $_ -and ([string]$_ -ceq $live.Container) })
                if ($liveWords.Count) { $v.Add('a prune names the live container ' + $live.Container) }
            } elseif ($s.Exe -eq 'docker' -and @($s.Arguments).Count -and $s.Arguments[0] -in $forbidden) { $v.Add('docker ' + $s.Arguments[0] + ' is not something this console runs: ' + $line) }
            if ($line -match 'build\.sh') { $v.Add('build.sh is reached only through Start-TestShard.ps1: ' + $line) }
            if (@($s.Arguments) -contains '-Fresh') { $v.Add('-Fresh deletes the test save; this console moves it aside instead') }
            if (@($s.Arguments) -contains $live.Container -and -not $confirmed) { $v.Add('touches ' + $live.Container + ' without a typed confirmation: ' + $line) }
            # cc-P63. The one tag this console makes: a built cc-p image onto sl-modernuo:latest, typed,
            # in a plan that names nothing live. Live's own image is never retagged.
            if ($s.Exe -eq 'docker' -and @($s.Arguments).Count -and $s.Arguments[0] -eq 'tag') {
                if (@($s.Arguments).Count -ne 3 -or $s.Arguments[2] -cne $Config.LatestTag -or [string]$s.Arguments[1] -cnotmatch $Config.DeployTagPattern) { $v.Add('the only tag this console makes is sl-modernuo:cc-p* onto ' + $Config.LatestTag + ': ' + $line) }
                if (-not $confirmed) { $v.Add('moves ' + $Config.LatestTag + ' without a typed confirmation: ' + $line) }
                $liveWords = @($plan | ForEach-Object { @($_.Arguments) + @($_.Container) + @($_.Path) } | Where-Object { $_ -and ([string]$_ -ceq $live.Container) })
                if ($liveWords.Count) { $v.Add('a deploy to test names the live container ' + $live.Container) }
            }
        }
        if ($Config.Websites) {
            # cc-P101, the Websites tab. A window's docker restart only ever names a game-PC site's container from
            # websites.json, never a shard; ssh only ever runs as NPM's tunnel to haven.ssh; and the deploy helper only
            # with an allowlisted name, after the typed phrase, exactly as Get-WebsitePlan writes it.
            $web = $Config.Websites
            $sa = @($s.Arguments)
            $shardCs = @($Config.Shards.Values | ForEach-Object { $_.Container })
            if ($s.Kind -eq 'window' -and $s.Exe -eq 'docker' -and $sa.Count -and $sa[0] -ceq 'restart') {
                $webCs = @(@($web.Sites) | Where-Object { $_.Where -ceq 'pc' } | ForEach-Object { $_.Container })
                if ($sa.Count -ne 2 -or $webCs -cnotcontains $sa[1] -or $shardCs -ccontains $sa[1]) { $v.Add('a website restart names only a game-PC site''s container from websites.json, never a shard: ' + $line) }
            }
            if ([string]$s.Exe -match '(?i)(^|\\)ssh(\.exe)?$') {
                $want = @('-N', '-L', ([string]$web.Npm.LocalPort + ':' + $web.Npm.Remote), $web.HavenSsh)
                if (-not $web.Npm -or $s.Kind -ne 'window' -or ($sa -join ' ') -cne ($want -join ' ')) { $v.Add('ssh runs here only as NPM''s tunnel, ssh ' + ($want -join ' ') + ', in a window: ' + $line) }
            }
            $named = @(@($s.Exe) + $sa + @($s.Path) | Where-Object { $_ } | ForEach-Object { [string]$_ })
            if ($Config.WebsiteDeployScript -and @($named | Where-Object { $_ -ieq $Config.WebsiteDeployScript }).Count) {
                $ok = ($s.Kind -eq 'window' -and $s.Exe -eq $Config.PowerShell -and $sa.Count -eq 7 -and (($sa[0..3]) -join ' ') -ceq '-NoProfile -ExecutionPolicy Bypass -File' -and
                    $sa[4] -ieq $Config.WebsiteDeployScript -and $sa[5] -ceq '-Deploy' -and $web.Deploys.Contains([string]$sa[6]))
                if (-not $ok) { $v.Add('the deploy helper runs only as -Deploy <a name in the deploys allowlist>, in a window: ' + $line) }
                if (-not @($plan | Where-Object { $_.Kind -eq 'confirm' -and $_.Phrase -ceq (Get-ConfirmPhrase 'web.deploy') }).Count) { $v.Add('a Haven deploy runs only after the typed phrase ' + (Get-ConfirmPhrase 'web.deploy')) }
            }
        }
        # cc-P216: Answer-Question.ps1 runs only as the Answer button writes it (a question id, a choice, the words as base64,
        # who, the source, a Golem name) and only after its typed phrase answer <golem> <prompt>.
        if ($s.Kind -in @('exec', 'window') -and $line -match '(?i)Answer-Question\.ps1') {
            $aq = @($s.Arguments); $i = [Array]::FindIndex([object[]]$aq, [Predicate[object]]{ param($x) [string]$x -match '(?i)Answer-Question\.ps1$' })
            $rest = @($(if ($i -ge 0) { $aq | Select-Object -Skip ($i + 1) } else { @() }))
            $okA = ($rest.Count % 2 -eq 0)
            for ($k = 0; $okA -and $k -lt $rest.Count; $k += 2) {
                $n = [string]$rest[$k]; $val = [string]$rest[$k + 1]
                $okA = switch -CaseSensitive ($n) {
                    '-Id'     { $val -cmatch '^[A-Za-z][A-Za-z0-9]{0,23}-P\d{1,4}-\d{8}-\d{6}-[0-9a-f]{4}$' }
                    '-Choice' { $val -cmatch '^(-1|[0-3])$' }
                    '-TextB64' { $val -cmatch '^[A-Za-z0-9+/]+={0,2}$' }
                    '-Who'    { $val -cmatch '^[A-Za-z0-9@._+][A-Za-z0-9@._+ -]{0,79}$' }
                    '-Source' { $val -ceq 'shard-console' }
                    '-Golem'  { $val -cmatch '^[A-Za-z][A-Za-z0-9]{0,23}$' }
                    default   { $false }
                }
            }
            if (-not $okA) { $v.Add('Answer-Question.ps1 runs only as -Id <question> -Choice <n> [-TextB64 <words>] -Who <name> -Source shard-console [-Golem <name>]: ' + $line) }
            if (-not @($plan | Where-Object { $_.Kind -eq 'confirm' -and [string]$_.Phrase -cmatch '^answer [a-z][a-z0-9]{0,23} p\d{1,4}$' }).Count) { $v.Add('an answer goes to a Golem only after the typed phrase answer <golem> <prompt>') }
        }
        # cc-P144: Publish-LegionScripts.ps1 changes what Haven serves only after its typed phrase, and git runs here only
        # as the Pull scripts button writes it: git -C <this PC's scripts clone> pull --ff-only.
        if ($s.Kind -eq 'script' -and $Config.LegionPublish -and $s.Path -and [IO.Path]::GetFullPath([string]$s.Path) -ieq [IO.Path]::GetFullPath($Config.LegionPublish)) {
            $lm = $(if ($s.Named) { [string]$s.Named['Mode'] } else { '' })
            if ($lm -notin @('Check', 'Dev', 'Promote')) { $v.Add('Publish-LegionScripts.ps1 runs here only as -Mode Check, Dev or Promote, not ' + $lm) }
            $lp = $(if ($lm -eq 'Dev') { Get-ConfirmPhrase 'package.scripts.dev' } elseif ($lm -eq 'Promote') { Get-ConfirmPhrase 'package.scripts.promote' } else { '' })
            if ($lp -and -not @($plan | Where-Object { $_.Kind -eq 'confirm' -and $_.Phrase -ceq $lp }).Count) { $v.Add('Publish-LegionScripts.ps1 -Mode ' + $lm + ' runs only after the typed phrase ' + $lp) }
        }
        if ($s.Kind -in @('exec', 'window') -and [string]$s.Exe -match '(?i)(^|[\\/])git(\.exe)?$') {
            $ga = @($s.Arguments)
            if (-not $Config.LegionRepo -or $ga.Count -ne 4 -or $ga[0] -cne '-C' -or [string]$ga[1] -ine $Config.LegionRepo -or $ga[2] -cne 'pull' -or $ga[3] -cne '--ff-only') { $v.Add('git runs here only as git -C <the scripts clone> pull --ff-only: ' + $line) }
        }
        # cc-P135: python runs here only as scripts\validate-art.py with one of its read-only subcommands, as an exec step
        # (so its output lands in the pane or the window), never render (the one that writes files) and never another
        # script; and that script is reached only that way.
        $named135 = @(@($s.Exe) + @($s.Arguments) + @($s.Path) | Where-Object { $_ } | ForEach-Object { [string]$_ })
        $isPy = ([string]$s.Exe -match '(?i)(^|[\\/])python(\d(\.\d+)?)?(\.exe)?$')
        $namesVa = [bool]($Config.ValidateArt -and @($named135 | Where-Object { $_ -ieq $Config.ValidateArt -or $_ -match '(?i)validate-art\.py$' }).Count)
        if ($isPy -or $namesVa) {
            $sa = @($s.Arguments)
            $sub = ''
            if ($sa.Count -ge 2) { $sub = [string]$sa[1] }
            $ok = ($s.Kind -eq 'exec' -and $isPy -and $Config.ValidateArt -and $sa.Count -ge 2 -and [string]$sa[0] -ieq $Config.ValidateArt -and @($Config.ArtSubcommands) -ccontains $sub -and -not @($sa | Where-Object { $_ -match '^--out' }).Count)
            if (-not $ok) { $v.Add('python runs here only as ' + $Config.ValidateArt + ' with one of ' + (@($Config.ArtSubcommands) -join ', ') + ', never render: ' + $line) }
        }
        if ($s.Kind -eq 'answer') {
            # cc-P106: an answer goes only to a shard's console, as n, y, a or Enter, after its phrase; a delete answer (y,
            # a) only after delete on <shard> and a verified copy of that shard's save into the snapshot folder.
            $owner = @($Config.Shards.Values | Where-Object { $_.Container -ceq $s.Container })[0]
            if (-not $owner) { $v.Add('an answer goes only to a shard container (' + ((@($Config.Shards.Values) | ForEach-Object { $_.Container }) -join ', ') + '): ' + $s.Container) }
            else {
                if (@('n', 'y', 'a', 'enter') -cnotcontains $s.Phrase) { $v.Add('an answer is n, y, a or Enter: ' + $s.Phrase) }
                $deletes = @('y', 'a') -ccontains $s.Phrase
                $need = 'answer ' + $owner.Key
                if ($deletes) { $need = 'delete on ' + $owner.Key }
                if (-not @($plan | Where-Object { $_.Kind -eq 'confirm' -and $_.Phrase -ceq $need }).Count) { $v.Add('the answer ' + $s.Phrase + ' is sent only after the typed phrase ' + $need) }
                if (-not $s.Signature) { $v.Add('an answer names the question it answers') }
                if ($deletes) {
                    $at = [array]::IndexOf($plan, $s)
                    $pre = @(); if ($at -gt 0) { $pre = @($plan[0..($at - 1)]) }
                    $savesFull = [IO.Path]::GetFullPath($owner.Saves).TrimEnd('\')
                    $under = $snapRoot + $owner.Key + '\'
                    $copies = @($pre | Where-Object { $_.Kind -eq 'copy' -and $_.From -and $_.To -and [IO.Path]::GetFullPath($_.From).TrimEnd('\') -ieq $savesFull -and [IO.Path]::GetFullPath($_.To).StartsWith($under, [StringComparison]::OrdinalIgnoreCase) })
                    $verified = @($copies | Where-Object { $t0 = $_.To; @($pre | Where-Object { $_.Kind -eq 'verify' -and $_.To -eq $t0 }).Count })
                    if (-not $verified.Count) { $v.Add('a delete answer is sent only after a backup: the ' + $owner.Label + ' save copied under ' + $under + ' and the copy verified') }
                }
            }
        }
        if ($s.Kind -eq 'healthwait' -and $s.Container -cne $Config.Shards['test'].Container) { $v.Add('a health wait is only ever on ' + $Config.Shards['test'].Container + ': ' + $s.Container) }
        if ($s.Kind -eq 'gatecheck') {
            # cc-P68: Check selected runs the build script's -CheckZip on one zip in dist\, nothing else.
            $full = $null
            if ($s.Path) { $full = [IO.Path]::GetFullPath($s.Path) }
            if (-not $full -or $full -ine [IO.Path]::GetFullPath($Config.BuildPackage)) { $v.Add('a zip is checked only by ' + $Config.BuildPackage + ': ' + $s.Path) }
            $dist = [IO.Path]::GetFullPath($Config.PackageDist).TrimEnd('\') + '\'
            if (-not $s.From -or $s.From -notmatch '\.zip$' -or -not ([IO.Path]::GetFullPath($s.From)).StartsWith($dist, [StringComparison]::OrdinalIgnoreCase)) { $v.Add('Check selected checks only a zip in ' + $dist + ': ' + $s.From) }
        }
        if ($s.Kind -in @('script', 'checkzip')) {
            # cc-P60, cc-P63. The scripts run in-process: the commit script (for real only once typed),
            # the package build (never its -GateSelfTest), and the publish script (only once typed, only
            # on a zip in dist\). A checkzip step runs the build script's -CheckZip.
            $call = Format-ScriptCall $s.Path $s.Named
            $full = $null
            if ($s.Path) { $full = [IO.Path]::GetFullPath($s.Path) }
            $is = { param($p) $p -and $full -and $full -ieq [IO.Path]::GetFullPath($p) }
            if ($s.Kind -eq 'checkzip') {
                if (-not (& $is $Config.BuildPackage)) { $v.Add('a zip is checked only by ' + $Config.BuildPackage + ': ' + $s.Path) }
            } elseif (& $is $Config.CommitScript) {
                if (-not $s.Named['DryRun'] -and -not $confirmed) { $v.Add('runs the commit for real without a typed confirmation: ' + $call) }
            } elseif (& $is $Config.BuildPackage) {
                $other = @($s.Named.Keys | Where-Object { $_ -notin @('Notes', 'CheckZip') })
                if ($other.Count) { $v.Add('the package build runs with -Notes or -CheckZip only: ' + $call) }
            } elseif (& $is $Config.PublishScript) {
                if (-not $confirmed) { $v.Add('publishes a player package without a typed confirmation: ' + $call) }
                $zp = [string]$s.Named['Zip']
                $dist = [IO.Path]::GetFullPath($Config.PackageDist).TrimEnd('\') + '\'
                if (-not $zp -or -not ([IO.Path]::GetFullPath($zp)).StartsWith($dist, [StringComparison]::OrdinalIgnoreCase) -or $zp -notmatch '\.zip$' -or @($s.Named.Keys).Count -ne 1) { $v.Add('the publish script runs only with -Zip on a zip in ' + $dist + ': ' + $call) }
            } elseif ($Config.LegionPublish -and (& $is $Config.LegionPublish)) {
                # cc-P144: -Mode only; Dev and Promote also need their phrase (the rule further down).
                if (@($s.Named.Keys | Where-Object { $_ -ne 'Mode' }).Count) { $v.Add('Publish-LegionScripts.ps1 runs here with -Mode only: ' + $call) }
            } elseif (($Config.LegionSetup -and (& $is $Config.LegionSetup)) -or ($Config.LegionKey -and (& $is $Config.LegionKey))) {
                # cc-P144: Setup-ScriptsRepo.ps1 and Setup-PublishKey.ps1 with no parameters; each asks what it needs itself.
                if ($s.Named -and @($s.Named.Keys).Count) { $v.Add((Split-Path ([string]$s.Path) -Leaf) + ' runs here with no parameters: ' + $call) }
            } else {
                $v.Add('the only script this console runs in-process is one of ' + $Config.CommitScript + ', ' + $Config.BuildPackage + ', ' + $Config.PublishScript + ' and the Legion scripts'' three (scripts\legion\): ' + $call)
            }
        }
        if ($s.Kind -eq 'launch' -and $s.Path -eq $live.Client -and -not $confirmed) { $v.Add('opens a live client without a typed confirmation') }
        if ($s.Kind -eq 'remove-snapshot') {
            $full = [IO.Path]::GetFullPath($s.Path).TrimEnd('\')
            $inside = $full.StartsWith($snapRoot, [StringComparison]::OrdinalIgnoreCase)
            if (-not $inside -or @($full.Substring($snapRoot.Length).Split('\')).Count -ne 2) {
                $v.Add('deletes ' + $full + ', which is not one snapshot folder under ' + $snapRoot)
            }
            foreach ($sv in $saves) {
                if ($full -ieq $sv -or $full.StartsWith($sv + '\', [StringComparison]::OrdinalIgnoreCase)) { $v.Add('deletes a world save: ' + $full) }
            }
        }
        if ($s.Kind -in @('wiperequest', 'secret', 'wipewait')) {
            # cc-P73. A wipe request is written only to the TEST world folder, only after the phrase, in a
            # plan that names nothing live. TestAccountWipe.cs refuses one on live anyway: the second lock.
            if (-not @($plan | Where-Object { $_.Kind -eq 'confirm' -and $_.Phrase -ceq (Get-ConfirmPhrase 'test.wipe') }).Count) { $v.Add('a wipe step runs only after the typed phrase ' + (Get-ConfirmPhrase 'test.wipe') + ': ' + $s.Kind) }
            $liveWords = @($plan | ForEach-Object { @($_.Arguments) + @($_.Container) + @($_.Path) + @($_.From) + @($_.To) } | Where-Object { $_ -and ([string]$_ -ceq $live.Container) })
            if ($liveWords.Count) { $v.Add('a wipe plan names the live container ' + $live.Container) }
            $liveRoot = [IO.Path]::GetFullPath((Split-Path $live.Saves -Parent)).TrimEnd('\') + '\'
            if ($s.Kind -ne 'secret') {
                $full = [IO.Path]::GetFullPath([string]$s.Path)
                if ($full -ine [IO.Path]::GetFullPath($Config.WipeRequestFile)) { $v.Add('a wipe request goes only to ' + $Config.WipeRequestFile + ': ' + $s.Path) }
                if ($full.StartsWith($liveRoot, [StringComparison]::OrdinalIgnoreCase)) { $v.Add('a wipe request is never placed in the live world folder: ' + $s.Path) }
            }
            if ($s.Kind -eq 'wipewait' -and $s.Container -cne $Config.Shards['test'].Container) { $v.Add('a wipe is waited for only on ' + $Config.Shards['test'].Container + ': ' + $s.Container) }
        }
        if ($Config.Autopilot) {
            # cc-P74. The Golem's scripts run only as themselves, each in a window of its own, and its
            # hook (golem.ps1, or cc-autopilot.ps1 before it, which commit and push) never. Notepad opens only its
            # log or settings. cc-P99: both folders count, and Move-ToGolem.ps1 is the one script outside the active one.
            $ap = $Config.Autopilot
            $roots = @(@($ap.Root, $ap.GolemRoot, $ap.AutopilotRoot) | Where-Object { $_ } | Select-Object -Unique)
            $named = @(@($s.Exe) + @($s.Arguments) + @($s.Path) + @($s.From) + @($s.To) | Where-Object { $_ } | ForEach-Object { [string]$_ })
            $inRoot = @($named | Where-Object { $n0 = $_; @($roots | Where-Object { $n0.StartsWith($_.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $n0 -ieq $_ }).Count })
            if (@($named | Where-Object { $_ -match '(?i)(cc-autopilot|(^|[\\/])golem)\.ps1' }).Count) { $v.Add('the Golem''s hook is never run from the console: it commits and pushes on its own') }
            elseif ($inRoot.Count) {
                $allowed = @($ap.Start, $ap.Stop, $ap.Setup, $ap.TestEmail, $ap.MoveScript, $ap.LongRunEnd, $ap.WrapUp, $ap.StopPoint, $ap.LongRunStart, $ap.WakeScript, $ap.AutoWakeScript, $ap.NightShiftScript, $ap.CliCheckScript, $ap.PokeScript, $ap.RunNextScript, $ap.RetryScript, $ap.IntegrateScript, $ap.AssignScript, $ap.MarathonScript, $ap.MergeScript, $ap.AwayScript, $ap.TinkerScript, $ap.LaneNowScript, $ap.AnswerScript, $ap.SkipDayScript) | Where-Object { $_ }
                $sa = @($s.Arguments)
                # cc-P186: a Golem script may also name one Golem of several (-Golem <name>, last); the rules below hold for the rest.
                if ($sa.Count -ge 7 -and $sa[-2] -ceq '-Golem' -and $sa[-1] -cmatch '^[A-Za-z][A-Za-z0-9]{0,23}$' -and @(Get-GolemNamedScripts $ap) -icontains $sa[4]) { $sa = @($sa[0..($sa.Count - 3)]) }
                $head = ($s.Kind -eq 'window' -and $s.Exe -eq $Config.PowerShell -and $sa.Count -ge 5 -and (($sa[0..3]) -join ' ') -ceq '-NoProfile -ExecutionPolicy Bypass -File' -and $allowed -icontains $sa[4])
                # The scripts that take arguments run only with them (below); the others run bare.
                $withArgs = @($ap.LongRunStart, $ap.WakeScript, $ap.AutoWakeScript, $ap.NightShiftScript, $ap.RunNextScript, $ap.RetryScript, $ap.AssignScript, $ap.MarathonScript, $ap.MergeScript, $ap.AwayScript, $ap.TinkerScript, $ap.AnswerScript, $ap.SkipDayScript) | Where-Object { $_ }
                $ok = ($head -and $sa.Count -eq 5 -and $withArgs -inotcontains $sa[4])
                # cc-P125: Start-LongRun.ps1 only with -Cap <n> -Until <time>; Wrap-Up.ps1 and Find-StoppingPoint.ps1 also with -Cancel.
                $tmpT = [datetime]::MinValue
                if ($head -and $sa[4] -ieq $ap.LongRunStart -and $sa.Count -eq 9 -and $sa[5] -ceq '-Cap' -and $sa[6] -match '^\d+$' -and $sa[7] -ceq '-Until' -and [datetime]::TryParse($sa[8], [ref]$tmpT)) { $ok = $true }
                if ($head -and $sa.Count -eq 6 -and $sa[5] -ceq '-Cancel' -and @($ap.WrapUp, $ap.StopPoint) -icontains $sa[4]) { $ok = $true }
                # cc-P131: Wake-Golem.ps1 only with -Model Auto|Fable|Opus|Sonnet (cc-P153 Auto); Set-AutoWake.ps1 with -On or -Off; Set-NightShift.ps1
                # with -Off, or -On -Start HH:mm -End HH:mm. The CLI itself (claude.exe) is never run from here: the script opens it.
                if ($head -and $sa[4] -ieq $ap.WakeScript -and $sa.Count -eq 7 -and $sa[5] -ceq '-Model' -and $sa[6] -cin @('Auto', 'Fable', 'Opus', 'Sonnet')) { $ok = $true }   # cc-P153: Auto
                if ($head -and $sa[4] -ieq $ap.AutoWakeScript -and $sa.Count -eq 6 -and $sa[5] -cin @('-On', '-Off')) { $ok = $true }
                # cc-P200: Set-Marathon.ps1 only with -Mode auto|on|off.
                if ($head -and $ap.MarathonScript -and $sa[4] -ieq $ap.MarathonScript -and $sa.Count -eq 7 -and $sa[5] -ceq '-Mode' -and $sa[6] -cin @('auto', 'on', 'off')) { $ok = $true }
                # cc-P208: ShadowIron.ps1 only with -Pause or -Resume.
                if ($head -and $ap.MergeScript -and $sa[4] -ieq $ap.MergeScript -and $sa.Count -eq 6 -and $sa[5] -cin @('-Pause', '-Resume')) { $ok = $true }
                # cc-P222: Tinker.ps1 alone (a round), or with -Pause or -Resume.
                if ($head -and $ap.TinkerScript -and $sa[4] -ieq $ap.TinkerScript -and ($sa.Count -eq 5 -or ($sa.Count -eq 6 -and $sa[5] -cin @('-Pause', '-Resume')))) { $ok = $true }
                # cc-P149: Set-Away.ps1 only with -Off, or -Until <yyyy-MM-dd HH:mm>.
                if ($head -and $ap.AwayScript -and $sa[4] -ieq $ap.AwayScript -and (($sa.Count -eq 6 -and $sa[5] -ceq '-Off') -or ($sa.Count -eq 7 -and $sa[5] -ceq '-Until' -and $sa[6] -match '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$'))) { $ok = $true }
                if ($head -and $sa[4] -ieq $ap.NightShiftScript -and (($sa.Count -eq 6 -and $sa[5] -ceq '-Off') -or ($sa.Count -eq 10 -and $sa[5] -ceq '-On' -and $sa[6] -ceq '-Start' -and $sa[7] -match '^\d{1,2}:\d{2}$' -and $sa[8] -ceq '-End' -and $sa[9] -match '^\d{1,2}:\d{2}$'))) { $ok = $true }
                # cc-P287: Skip-Day.ps1 only with -Golem <name> -Date <yyyy-MM-dd> -By console.
                if ($head -and $ap.SkipDayScript -and $sa[4] -ieq $ap.SkipDayScript -and $sa.Count -eq 11 -and $sa[5] -ceq '-Golem' -and $sa[6] -cmatch '^[A-Za-z][A-Za-z0-9]{1,23}$' -and $sa[7] -ceq '-Date' -and $sa[8] -cmatch '^\d{4}-\d{2}-\d{2}$' -and $sa[9] -ceq '-By' -and $sa[10] -ceq 'console') { $ok = $true }
                # cc-P182: Set-RunNext.ps1 only with -Prompt P<nn> or -Cancel; Retry-Stopped.ps1 only with -Prompt P<nn>.
                if ($head -and $ap.RunNextScript -and $sa[4] -ieq $ap.RunNextScript -and (($sa.Count -eq 7 -and $sa[5] -ceq '-Prompt' -and $sa[6] -cmatch '^P\d+$') -or ($sa.Count -eq 6 -and $sa[5] -ceq '-Cancel'))) { $ok = $true }
                if ($head -and $ap.RetryScript -and $sa[4] -ieq $ap.RetryScript -and $sa.Count -eq 7 -and $sa[5] -ceq '-Prompt' -and $sa[6] -cmatch '^P\d+$') { $ok = $true }
                # cc-P216: Answer-Question.ps1, its arguments held to the button's shape by the rule for it above.
                if ($head -and $ap.AnswerScript -and $sa[4] -ieq $ap.AnswerScript -and $sa.Count -ge 13) { $ok = $true }
                # cc-P187: Assign-Prompt.ps1 only with -Prompt P<nn> -Golem <name>, and -RunNext last or not at all.
                if ($head -and $ap.AssignScript -and $sa[4] -ieq $ap.AssignScript -and ($sa.Count -eq 9 -or ($sa.Count -eq 10 -and $sa[9] -ceq '-RunNext')) -and $sa[5] -ceq '-Prompt' -and $sa[6] -cmatch '^P\d+$' -and $sa[7] -ceq '-Golem' -and $sa[8] -cmatch '^[A-Za-z][A-Za-z0-9]{0,23}$') { $ok = $true }
                # cc-P94: and settings.json, the same way.
            $readable = @($ap.LogFile, $ap.SettingsFile) | Where-Object { $_ }
            $okLog = ($s.Kind -eq 'launch' -and $s.Path -ieq $ap.Notepad -and $sa.Count -eq 1 -and $readable -icontains $sa[0])
                # cc-P99: and Explorer on the folder itself (Open Golem folder).
                $okDir = ($s.Kind -eq 'launch' -and $s.Path -ieq 'explorer.exe' -and $sa.Count -eq 1 -and $sa[0] -ieq $ap.Root)
                if (-not $ok -and -not $okLog -and -not $okDir) { $v.Add('the Golem''s folder is reached only by its five scripts, each in a window of its own, its log or settings.json in Notepad, or the folder in Explorer: ' + ($named -join ' ')) }
            }
            # cc-P125: and a prompt (queue, running or completed) or a cc-P notes file, read only.
            $np = @($s.Arguments)
            $okRead = ($np.Count -eq 1 -and [string]$np[0] -match '[\\/]cc-P\d+-[^\\/]*\.md$' -and (
                @(@($ap.PromptDir, $ap.Queue, $ap.CompletedDir, $Config.NotesFolder) | Where-Object { $_ } | Where-Object { (Split-Path -Parent ([string]$np[0])) -ieq $_ }).Count))
            if ($s.Kind -eq 'launch' -and $s.Path -ieq $ap.Notepad -and -not $okRead -and -not ($np.Count -eq 1 -and (@($ap.LogFile, $ap.SettingsFile) | Where-Object { $_ }) -icontains $np[0])) { $v.Add('Notepad opens only ' + $ap.LogFile + ', ' + $ap.SettingsFile + ', a cc-P prompt or a cc-P notes file') }
        }
        if ($s.Kind -eq 'move') {
            $from = [IO.Path]::GetFullPath($s.From).TrimEnd('\')
            if (-not ($saves -icontains $from) -or -not $s.To.StartsWith($s.From.TrimEnd('\') + '.aside-')) {
                $v.Add('the only move this console makes is a save to <save>.aside-<time>: ' + $s.From + ' -> ' + $s.To)
            }
        }
    }
    $v.ToArray()
}

function Format-CommandLine {
    param([string]$Exe, [string[]]$Arguments)
    $parts = @($Exe) + @($Arguments | ForEach-Object {
        if ($_ -eq '' -or $_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    })
    ($parts | Where-Object { $_ -ne $null }) -join ' '
}

function Format-PlanStep {
    param($Step, [int]$Number)
    $n = ('{0,3}. ' -f $Number)
    $pad = '       '
    $out = New-Object 'System.Collections.Generic.List[string]'
    switch ($Step.Kind) {
        'confirm' { $out.Add($n + "CONFIRM  typed: you must type exactly '" + $Step.Phrase + "'. " + $Step.Text) }
        'ask'     { $out.Add($n + 'ASK      yes/no: ' + $Step.Text) }
        'say'     { $out.Add($n + 'SAY      ' + $Step.Text) }
        'refuse'  { $out.Add($n + 'REFUSE   ' + $Step.Text) }
        'exec'    { $out.Add($n + 'RUN      ' + (Format-CommandLine $Step.Exe $Step.Arguments)); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'script'  { $out.Add($n + 'RUN      ' + (Format-ScriptCall $Step.Path $Step.Named)); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'checkzip' { $out.Add($n + 'CHECK    ' + (Format-ScriptCall $Step.Path ([ordered]@{ CheckZip = '<the one zip in ' + $Step.To + ' written since this plan started>' }))); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'gatecheck' { $out.Add($n + 'CHECK    ' + (Format-ScriptCall $Step.Path ([ordered]@{ CheckZip = $Step.From }))); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'window'  { $out.Add($n + 'WINDOW   a new PowerShell window running: ' + (Format-CommandLine $Step.Exe $Step.Arguments)); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'launch'  { $out.Add($n + 'OPEN     ' + (Format-CommandLine $Step.Path $Step.Arguments)); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'move'    {
            $m = $n + 'MOVE     ' + $Step.From + '  ->  ' + $Step.To
            if ($Step.IfExists) { $m += '   (if it exists)' }
            $out.Add($m); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) }
        }
        'copy'    { $out.Add($n + 'COPY     ' + $Step.From + '  ->  ' + $Step.To + '   (refuses if the target exists)'); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'verify'  { $out.Add($n + 'VERIFY   ' + $Step.To + ' has the same file count and bytes as ' + $Step.From + '; stop here if not') }
        'stopped' { $out.Add($n + 'CHECK    docker inspect says ' + $Step.Container + ' is not running; stop here if it is') }
        'remove-snapshot' { $out.Add($n + 'DELETE   ' + $Step.Path); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'log'     { $out.Add($n + 'LOG      one timestamped line: ' + $Step.Text) }
        'answer'  { $out.Add($n + 'ANSWER   type ' + $(if ($Step.Phrase -eq 'enter') { 'Enter' } else { $Step.Phrase + ' and Enter' }) + ' on ' + $Step.Container + " console (Docker engine API, attached for this only), only if it is still asking the same question; stop here if not"); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'healthwait' { $out.Add($n + 'WAIT     up to ' + $Step.Seconds + ' s for ' + $Step.Container + ' to be healthy (listening) or to crash; stop here unless healthy'); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'secret'  { $out.Add($n + 'ASK      in this window, not shown: ' + $Step.Text) }
        'wiperequest' { $out.Add($n + 'WRITE    ' + $Step.Path + '   (refuses if it exists)'); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
        'wipewait' { $out.Add($n + 'WAIT     for the ' + $script:WipeTag + ' DONE line in the ' + $Step.Container + ' log; stop here on REFUSED, FAILED or no answer, and remove ' + $Step.Path + ' if it is still there'); if ($Step.Text) { $out.Add($pad + '  # ' + $Step.Text) } }
    }
    $out.ToArray()
}

function Format-Plan {
    param([string]$Action, [string]$ShardKey, $Plan, $Violations, [string]$Header = 'DRY RUN')
    $out = New-Object 'System.Collections.Generic.List[string]'
    $out.Add('== ' + $Header + ': ' + $Action + ' (' + $ShardKey + ') ' + ('=' * [Math]::Max(3, 70 - $Action.Length - $ShardKey.Length)))
    $i = 0
    foreach ($s in @($Plan)) { $i++; foreach ($l in (Format-PlanStep $s $i)) { $out.Add($l) } }
    foreach ($x in @($Violations)) { if ($x) { $out.Add('  REFUSED BY THE SAFETY CHECK: ' + $x) } }
    $out.ToArray()
}

function Test-LiveComposeFile {
    # D29: a compose file with build: and no image: builds on `up`, with no test gate.
    param([string]$Text, [string]$Service = 'modernuo')
    $r = [ordered]@{ Read = $false; HasBuild = $false; HasImage = $false; Warning = $null }
    if (-not $Text) { $r.Warning = 'could not read docker/uo/docker-compose.yml'; return [pscustomobject]$r }
    $r.Read = $true
    $in = $false
    foreach ($l in @((Remove-Ansi $Text) -split "`n")) {
        if ($l -match '^  ([^\s:#][^:]*):\s*$') { $in = ($Matches[1] -eq $Service); continue }
        if ($l -match '^\S') { $in = $false; continue }
        if ($in -and $l -match '^    build:') { $r.HasBuild = $true }
        if ($in -and $l -match '^    image:') { $r.HasImage = $true }
    }
    if ($r.HasBuild -and -not $r.HasImage) {
        $r.Warning = 'docker/uo/docker-compose.yml gives ' + $Service + ' build: and no image: (bug-list D29). compose up there can build an image with no test gate, and would recreate sl-modernuo. This console never runs compose on it.'
    } elseif (-not $r.HasBuild -and -not $r.HasImage) {
        $r.Warning = 'docker/uo/docker-compose.yml has no ' + $Service + ' service that this console can read.'
    }
    [pscustomobject]$r
}

function Remove-AccountLines {
    # Captures go into a public repo, so lines naming accounts or clients are dropped. Nothing
    # else is changed, colour codes included.
    param([string]$Text)
    $keep = foreach ($l in @(($Text -replace "`r", '') -split "`n")) {
        if ((Remove-Ansi $l) -notmatch 'Protected accounts registered|Login: |Client: |AccountHandler>|account=') { $l }
    }
    @($keep) -join "`n"
}

function ConvertTo-CaptureFile {
    param([System.Collections.IDictionary]$Sections)
    $sb = New-Object System.Text.StringBuilder
    foreach ($k in $Sections.Keys) {
        [void]$sb.Append($script:CaptureMarker + $k + " #####`n")
        $t = [string]$Sections[$k]
        if ($t) { [void]$sb.Append(($t -replace "`r", '').TrimEnd("`n") + "`n") }
    }
    $sb.ToString()
}

function ConvertFrom-CaptureFile {
    param([string]$Text)
    $sections = [ordered]@{}
    $key = $null
    $buf = New-Object 'System.Collections.Generic.List[string]'
    foreach ($l in @(($Text -replace "`r", '') -split "`n")) {
        if ($l.StartsWith($script:CaptureMarker) -and $l.EndsWith(' #####')) {
            if ($null -ne $key) { $sections[$key] = $buf -join "`n" }
            $key = $l.Substring($script:CaptureMarker.Length, $l.Length - $script:CaptureMarker.Length - 6)
            $buf.Clear()
        } elseif ($null -ne $key) {
            $buf.Add($l)
        }
    }
    if ($null -ne $key) { $sections[$key] = $buf -join "`n" }
    $sections
}

function New-ConsoleState {
    # The whole status panel, from a set of captured sections. The shell fills the sections from
    # docker and the disk, or -DryRun reads them from a file; this cannot tell the difference.
    param($Config, [System.Collections.IDictionary]$Sections, [string]$Source = 'capture')
    $meta = [string]$Sections['meta']
    $now = $null
    if ($meta -match 'captured-at (\S+)') { $now = ConvertFrom-DockerTime $Matches[1] }
    if (-not $now) { $now = [datetime]::UtcNow }
    $latest = ConvertFrom-ImageInspect $Sections['image ' + $Config.LatestTag]
    $dockerError = $null
    $shards = [ordered]@{}
    $all = $Sections
    foreach ($sh in $Config.Shards.Values) {
        # cc-P108: a retired shard is read from nothing, even when a capture holds sections for it, so it can never
        # be judged down, missing, crashed or waiting.
        $Sections = $(if ($sh.Retired) { @{} } else { $all })
        $c = ConvertFrom-ContainerInspect $Sections['inspect ' + $sh.Container]
        if (-not $sh.Retired -and -not $c.Exists -and $c.Error -match 'connect|pipe|daemon is not running|could not be started') { $dockerError = $c.Error }
        $cfgRef = $null; $run = $null
        if ($c.Exists) {
            $cfgRef = ConvertFrom-ImageInspect $Sections['image ' + $c.ConfigImage]
            $run    = ConvertFrom-ImageInspect $Sections['image ' + $c.ImageId]
        }
        $port = $sh.GamePort
        if ($c.GamePort) { $port = $c.GamePort }
        $asides = @(@((Remove-Ansi ([string]$Sections['asides ' + $sh.Key])) -split "`n") | Where-Object { $_ })
        $shards[$sh.Key] = [pscustomobject]@{
            Container    = $c
            ConfigRef    = $cfgRef
            RunningImage = $run
            Drift        = (Get-ImageDrift -Shard $sh -Container $c -Latest $latest -ConfigRef $cfgRef -RunningImage $run -LatestTag $Config.LatestTag)
            Listener     = (Get-ListenerState -Container $c -LogText $Sections['logs ' + $sh.Container] -GamePort $port)
            SavesOnStop  = [bool]($c.Exists -and (Test-SavesOnStop ([string]$Sections['logs ' + $sh.Container])))
            # cc-P96: a terminating exception in the log since this start ("This exception is fatal").
            Crash        = (Get-CrashReport ([string]$Sections['logs ' + $sh.Container]))
            # cc-P106: the console question the shard is waiting on, from its timestamped tail, or $null.
            Question     = (Get-ShardQuestion -LogText ([string]$Sections['logtail ' + $sh.Container]) -Running ([bool]($c.Exists -and $c.Running)) -Now $now -QuietSeconds $Config.QuestionQuietSeconds)
            Save         = (Get-SaveSummary (ConvertFrom-FileListing $Sections['saves ' + $sh.Key]))
            Snapshots    = @(ConvertFrom-SnapshotListing $Sections['snapshots ' + $sh.Key])
            Asides       = $asides
            Retired      = [bool]$sh.Retired
        }
    }
    $Sections = $all
    # cc-P108: no warning about the live compose file while live is retired (it is in docker\uo\retired, not runnable).
    $compose = [pscustomobject]@{ Read = $false; HasBuild = $false; HasImage = $false; Warning = $null }
    if (-not (Test-LiveRetired $Config)) { $compose = Test-LiveComposeFile $Sections['compose live'] }
    [pscustomobject]@{
        Now         = $now
        Source      = $Source
        Latest      = $latest
        # cc-P63: every sl-modernuo image, newest first, for Deploy built image.
        Images      = @(ConvertFrom-ImageTagListing ([string]$Sections['images sl-modernuo']))
        Compose     = $compose
        DockerError = $dockerError
        Shards      = $shards
        Publisher   = (Get-PublisherState -Config $Config -Sections $Sections -Shards $shards -Now $now)
    }
}

function ConvertFrom-StatusJsonTime {
    # 5.1's ConvertFrom-Json leaves ISO dates as strings; 7 turns them into DateTime. Take both.
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [datetime]) { return $Value.ToUniversalTime() }
    ConvertFrom-DockerTime ([string]$Value)
}

function ConvertTo-HostPath {
    # A bind mount's Source as Docker reports it, as a comparable Windows path: 'D:/x/y' and the WSL forms
    # '/run/desktop/mnt/host/d/x/y', '/host_mnt/d/x/y' and '/mnt/d/x/y' all become 'D:\x\y'. No trailing separator.
    param([string]$Path)
    if (-not $Path) { return $null }
    $p = $Path -replace '\\', '/'
    if ($p -match '^(?:/run/desktop/mnt/host|/host_mnt|/mnt)/([a-zA-Z])(/.*)?$') { $p = $Matches[1].ToUpper() + ':' + $Matches[2] }
    $p = $p.TrimEnd('/')
    $p -replace '/', '\'
}

function Test-StatusWriterMount {
    # cc-P42 Part E (cc-P37 section 7.3). A container writes the publisher's folder when one of its mounts has the
    # status target as its destination, or an ancestor of it (live mounts the parent, /var/lib/uo/modernuo, since
    # cc-P37), and that mount's source, followed down to the target, is the publisher's folder. The test shard mounts
    # its own parent (modernuo-test), so its status folder is not the one served and it is not a writer.
    param($Mount, [string]$Target, [string]$Folder)
    $dest = ([string]$Mount.Destination).TrimEnd('/')
    $target = $Target.TrimEnd('/')
    if (-not $dest) { return $false }
    if ($dest -eq $target) { $rest = '' }
    elseif ($target.StartsWith($dest + '/')) { $rest = $target.Substring($dest.Length + 1) }
    else { return $false }
    $src = ConvertTo-HostPath ([string]$Mount.Source)
    if (-not $src) { return $false }
    if ($rest) { $src = $src + '\' + ($rest -replace '/', '\') }
    return ($src -ieq (ConvertTo-HostPath $Folder))
}

function Get-PublisherState {
    # Is the shard status publisher running? Read from the file it writes, never from a claim in
    # the file: the file cannot report its own death (docker/uo-status/README.md). Fresh within
    # StaleMinutes means publishing. Also: which shard container has the status folder mounted,
    # because that is the one whose players the website shows, and whether sl-uo-status serves it.
    param($Config, [System.Collections.IDictionary]$Sections, $Shards, [datetime]$Now)
    $p = $Config.Publisher
    $r = [ordered]@{
        State = 'UNKNOWN'; Color = 'gray'; Detail = ''; GeneratedAt = $null; Age = $null
        Players = $null; Names = @(); ShardStartedAt = $null; LastSaveAt = $null
        Writers = @(); FeedState = 'UNKNOWN'; FeedDetail = ''; Lines = @()
    }
    $lines = New-Object 'System.Collections.Generic.List[string]'

    # Who can write it: a shard container with the status folder bind-mounted.
    $writers = @()
    foreach ($k in $Shards.Keys) {
        $c = $Shards[$k].Container
        if ($c.Exists -and @(@($c.Mounts) | Where-Object { Test-StatusWriterMount $_ $p.MountTarget $p.Folder }).Count) { $writers += $k }
    }
    $r.Writers = $writers

    # The feed: sl-uo-status, which hands the file to the website through Haven's /api/ route.
    $feedKey = 'inspect ' + $p.FeedContainer
    if ($Sections.Contains($feedKey)) {
        $fc = ConvertFrom-ContainerInspect $Sections[$feedKey]
        $http = ([string]$Sections['feed http']).Trim()
        if (-not $fc.Exists) {
            $r.FeedState = 'NO CONTAINER'
            $r.FeedDetail = $p.FeedContainer + ' does not exist; the website cannot read status.json. docker\uo-status\Start-UoStatus.ps1 creates it.'
        } elseif (-not $fc.Running) {
            $r.FeedState = 'STOPPED'
            $r.FeedDetail = $p.FeedContainer + ' is ' + $fc.Status + '; the website cannot read status.json.'
        } elseif ($http -eq '200') {
            $r.FeedState = 'SERVING'
            $r.FeedDetail = $p.FeedContainer + ' is up and ' + $p.FeedUrl + ' answers 200.'
        } elseif ($http) {
            $r.FeedState = 'NOT SERVING'
            $r.FeedDetail = $p.FeedContainer + ' is up but ' + $p.FeedUrl + ' returned: ' + $http + '. 503 means there is no status.json in the folder it serves.'
        } else {
            $r.FeedState = 'UP'
            $r.FeedDetail = $p.FeedContainer + ' is up (the address was not checked).'
        }
    } else {
        $r.FeedDetail = 'not in this capture.'
    }

    if (-not $Sections.Contains('status meta')) {
        $r.State = 'UNKNOWN'; $r.Detail = 'this capture predates the publisher check, so there is nothing to read.'
    } else {
        $meta = ([string]$Sections['status meta']).Trim()
        if ($meta -eq 'missing' -or -not $meta) {
            $r.State = 'NOT PUBLISHING'; $r.Color = 'red'
            $r.Detail = 'there is no status.json in ' + $p.Folder + '.'
            if ($writers.Count -eq 0) {
                $r.Detail += ' No shard container has the status folder mounted, so nothing can write it (Status-Feed-Runbook.md step 1; ShardStatusPublisher.cs must also be in the image).'
            } else {
                $r.Detail += ' ' + (($writers | ForEach-Object { $Config.Shards[$_].Label }) -join ' and ') + ' has the folder mounted, so the image it runs has no publisher, or the publisher cannot write.'
            }
        } else {
            $doc = $null
            try { $doc = ([string]$Sections['status file']) | ConvertFrom-Json } catch { $doc = $null }
            $gen = $null
            if ($doc) { $gen = ConvertFrom-StatusJsonTime $doc.generatedAt }
            if (-not $doc -or -not $gen) {
                $r.State = 'UNREADABLE'; $r.Color = 'red'
                $r.Detail = 'status.json is there but is not the schema 1 document (no readable generatedAt). The website will call the shard offline.'
            } else {
                $age = $Now - $gen
                $r.GeneratedAt = $gen; $r.Age = $age
                if ($doc.players) { $r.Players = $doc.players.count; $r.Names = @($doc.players.names | Where-Object { $_ }) }
                if ($doc.shard) { $r.ShardStartedAt = ConvertFrom-StatusJsonTime $doc.shard.startedAt }
                if ($doc.world) { $r.LastSaveAt = ConvertFrom-StatusJsonTime $doc.world.lastSaveAt }
                if ($age.TotalMinutes -le $p.StaleMinutes) {
                    $r.State = 'PUBLISHING'; $r.Color = 'green'
                    $r.Detail = 'written ' + (Format-Age $age) + ' ago, ' + $r.Players + ' playing'
                    if (@($r.Names).Count) { $r.Detail += ' (' + (@($r.Names) -join ', ') + ')' }
                    $r.Detail += '.'
                } else {
                    $r.State = 'STALE'; $r.Color = 'red'
                    $r.Detail = 'last written ' + (Format-Age $age) + ' ago (' + (Format-LocalTime $gen) + '). Older than ' + $p.StaleMinutes + ' minutes means the publisher has stopped, and the website shows the shard offline.'
                }
            }
        }
    }

    # What the website ends up showing depends on the feed too.
    if ($r.State -eq 'PUBLISHING' -and $r.FeedState -notin @('SERVING', 'UP', 'UNKNOWN')) { $r.Color = 'amber' }
    # cc-P108: live was the only writer. While it is retired, no writer is expected: grey and said so, never red.
    $liveRetired = Test-LiveRetired $Config
    if ($liveRetired -and -not $writers.Count -and $r.State -in @('NOT PUBLISHING', 'STALE')) {
        $r.Color = 'gray'
        $why = 'Live is retired and was its only writer, so nothing publishes it and the website shows the shard offline.'
        if ($r.State -eq 'NOT PUBLISHING') { $r.Detail = 'there is no status.json in ' + $p.Folder + '. ' + $why }
        else { $r.Detail += ' ' + $why }
    }
    if (-not $liveRetired -and $writers -contains 'test' -and -not ($writers -contains 'live')) {
        if ($r.Color -eq 'green') { $r.Color = 'amber' }
        $lines.Add('WARNING    : only the TEST container has the status folder mounted, so the website shows the TEST shard, not live.')
    }
    $wtext = 'none'
    if ($writers.Count) { $wtext = ($writers | ForEach-Object { $Config.Shards[$_].Label + ' (' + $Config.Shards[$_].Container + ')' }) -join ', ' }
    $lines.Insert(0, 'written by : ' + $wtext + ' (containers with ' + $p.Folder + ' at ' + $p.MountTarget + ', directly or through a parent mount)')
    $lines.Add('feed       : ' + $r.FeedState + '. ' + $r.FeedDetail)
    if ($r.ShardStartedAt) { $lines.Add('shard says : started ' + (Format-LocalTime $r.ShardStartedAt) + ', last world save ' + (Format-LocalTime $r.LastSaveAt)) }
    $r.Lines = $lines.ToArray()
    [pscustomobject]$r
}

function Get-ShardGlance {
    # One word and one line per shard, for the tiles at the top of the window.
    param($Shard, $ShardState, [datetime]$Now, $Config)
    # cc-P108: a retired shard is one grey line and nothing else: not down, not missing, not unknown.
    if ($Shard.Retired) {
        $line = 'retired 2026-10-14 (returns as a fresh 1.0 world)'
        if ($Config -and $Config.Live) { $line = $Config.Live.Line }
        return [pscustomobject]@{ Title = $Shard.Label; Word = 'RETIRED'; Color = 'gray'; Detail = $line; Retired = $true }
    }
    $c = $ShardState.Container
    $l = $ShardState.Listener
    $word = 'UNKNOWN'; $color = 'gray'; $detail = ''
    if (-not $c.Exists) {
        $word = 'NO CONTAINER'; $color = 'gray'; $detail = $Shard.Container + ' does not exist'
    } elseif (-not $c.Running) {
        $word = 'STOPPED'; $color = 'gray'
        $detail = $c.Status
        if ($c.FinishedAt) { $detail += ' ' + (Format-Age ($Now - $c.FinishedAt)) + ' ago' }
    } elseif ($l.State -eq 'LISTENING') {
        $word = 'RUNNING'; $color = 'green'
        $detail = 'up ' + (Format-Age ($Now - $c.StartedAt)) + ', listening on :' + $Shard.GamePort
    } elseif ($l.State -eq 'OWNER PROMPT') {
        $word = 'BLOCKED'; $color = 'red'; $detail = 'waiting at the owner account prompt (D19)'
    } else {
        $word = 'STARTING?'; $color = 'amber'
        $detail = 'up ' + (Format-Age ($Now - $c.StartedAt)) + ', not listening yet'
    }
    # cc-P96: a crash outranks everything else the tile could say. The process waits at "press return to exit", so
    # the container is Up; and a running shard docker's health check calls unhealthy is not RUNNING.
    $crash = $ShardState.Crash
    if ($c.Exists -and $crash -and $crash.Crashed) {
        $word = 'CRASHED'; $color = 'red'
        if ($crash.AtStart) { $word = 'CRASHED AT START' }
        $detail = $crash.Headline
        $known = $null
        if ($Config -and $Config.Fix) { $known = Find-KnownCrash $crash $Config.Fix.KnownCrashes }
        if ($known) { $detail = 'Known bug ' + $known.Bug + '. ' + $detail }
        $detail += ' (Fix test shard tab)'
    } elseif ($c.Exists -and $c.Running -and $c.Health -eq 'unhealthy') {
        $word = 'UNHEALTHY'; $color = 'red'; $detail = 'up ' + (Format-Age ($Now - $c.StartedAt)) + ', not listening on :' + $Shard.GamePort + ' (docker health check)'
    } elseif ($c.Exists -and $c.Running -and $c.Health -eq 'healthy' -and $word -eq 'RUNNING') {
        $detail += ', healthy'
    }
    # cc-P106: a question the shard waits on outranks everything but a crash, and a crash's own "press return to exit" says
    # that it waits. Neither is RUNNING, whatever docker's health check says.
    $q = $ShardState.Question
    if ($c.Exists -and $c.Running -and $q) {
        if ($q.Kind -eq 'fatal') {
            if ($word -notlike 'CRASHED*') { $word = 'CRASHED'; $color = 'red'; $detail = Get-QuestionLine $q }
            $detail += '; waiting at "press return to exit" (Shard question tab)'
        } else {
            $word = 'WAITING FOR AN ANSWER'; $color = 'amber'
            $detail = (Get-QuestionLine $q) + ' (Shard question tab)'
        }
    }
    if ($c.Exists) {
        if ($ShardState.Drift.Verdict -eq 'DRIFTED') {
            $detail += '; OLD IMAGE'
            if ($color -eq 'green') { $color = 'amber' }
        } elseif ($ShardState.Drift.Verdict -eq 'CURRENT') {
            $detail += '; image current'
        }
    }
    [pscustomobject]@{ Title = $Shard.Label; Word = $word; Color = $color; Detail = $detail }
}

function Get-QuestionLoss {
    # cc-P106: what a delete answer to this question loses, in plain words.
    param($Question, $Config)
    if (-not $Question) { return '' }
    $named = [string]$Question.Type + ' ' + [string]$Question.File
    foreach ($l in @($Config.Fix.QuestionLosses)) { if ($l -and $l.Match -and $named.Contains([string]$l.Match)) { return [string]$l.Lost } }
    switch ($Question.Kind) {
        'object' { return ('the whole object ' + $Question.Type + ' ' + $Question.Serial + ' and everything it holds') }
        'file'   { return ('everything in ' + $Question.File) }
        'type'   { return 'every saved object of the missing type (the type is named in the question line, which docker logs does not show)' }
    }
    ''
}

function Get-QuestionText {
    # cc-P106: the Shard question panel, Show the question and the email: the question, what failed, and each answer.
    param($Question, $Shard, $Config)
    $out = New-Object 'System.Collections.Generic.List[string]'
    $since = ''
    if ($Question.Since) { $since = ' since ' + $Question.Since.ToLocalTime().ToString('yyyy-MM-dd HH:mm', $script:Invariant) }
    $out.Add('The ' + $Shard.Label + ' shard (' + $Shard.Container + ') is waiting on its console' + $since + '.')
    $out.Add('Question: ' + $Question.Prompt)
    if ($Question.Type) { $out.Add('Could not load: ' + $Question.Type + ' ' + $Question.Serial) }
    if ($Question.File) { $out.Add('Could not load: ' + $Question.File) }
    if ($Question.Exception) { $out.Add('Error: ' + $Question.Exception) }
    foreach ($f in @($Question.Frames | Select-Object -First 4)) { $out.Add('  at ' + $f) }
    $out.Add('Answers:')
    $deletes = $false
    foreach ($a in @($Question.Answers)) {
        $need = ' (phrase: answer ' + $Shard.Key + ')'
        if ($a.Deletes) { $deletes = $true; $need = ' (phrase: delete on ' + $Shard.Key + '; the save is backed up first)' }
        $out.Add('  ' + $a.Key + ': ' + $a.Explain + $need)
    }
    if (-not @($Question.Answers).Count) { $out.Add('  none from here: answer it at the console (docker attach ' + $Shard.Container + '), or stop the shard.') }
    if ($deletes) { $out.Add('A delete answer loses ' + (Get-QuestionLoss $Question $Config)) }
    $out.Add('Or Stop the shard (phrase: stop ' + $Shard.Key + '): safe while it waits, since nothing has loaded.')
    $out.ToArray()
}

function Get-QuestionMails {
    # cc-P106: one email per new question. Sent holds the signatures already mailed (container|kind|since|object).
    param($State, $Config, [string[]]$Sent = @())
    foreach ($k in @('live', 'test')) {
        $ss = $State.Shards[$k]
        if (-not $ss -or -not $ss.Question) { continue }
        $sh = $Config.Shards[$k]
        # cc-P108: no email for a retired shard (its state never has a question anyway).
        if ($sh.Retired) { continue }
        $sig = $sh.Container + '|' + $ss.Question.Signature
        if (@($Sent) -contains $sig) { continue }
        $name = $sh.Label.Substring(0, 1) + $sh.Label.Substring(1).ToLowerInvariant()
        # cc-P157: the Golem emails' shape: what happened in words, What to do, then the technical part under Details.
        $body = @(('The ' + $name + ' shard stopped while it was starting and is asking a question on its console: ' + $ss.Question.Prompt + ' It waits until someone answers.'), '',
            "What to do: answer it in the Shard Console, on the Shard question tab. Stopping the shard there is safe too, since nothing has loaded yet.", '',
            'Details', '-------') + @(Get-QuestionText $ss.Question $sh $Config) + @('', 'This email is sent once per question.')
        [pscustomobject]@{ Shard = $k; Signature = $sig; Subject = ('[Shard] Needs you: the ' + $name + ' shard is waiting for an answer'); Body = ($body -join "`r`n") }
    }
}

function Get-QuestionPlan {
    # cc-P106: Show the question, the answers and Stop, for either shard.
    param([string]$Action, [string]$ShardKey, $Config, $State, [datetime]$Now)
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $sh = $Config.Shards[$ShardKey]
    $ss = $State.Shards[$ShardKey]
    $c = $ss.Container
    $q = $ss.Question
    $who = 'The ' + $sh.Label + ' shard (' + $sh.Container + ')'
    switch ($Action) {
        'question.show' {
            if ($q) { foreach ($l in (Get-QuestionText $q $sh $Config)) { $steps.Add((New-PlanStep -Kind say -Text $l)) } }
            elseif ($c.Exists -and $c.Running) { $steps.Add((New-PlanStep -Kind say -Text ($who + ' is not waiting on a console question.'))) }
            else { $steps.Add((New-PlanStep -Kind say -Text ($who + ' is not running, so it is not asking anything.'))) }
            $steps.Add((New-PlanStep -Kind log -Text ('question.show ' + $ShardKey + ': ' + $(if ($q) { $q.Kind + ' ' + (Get-QuestionLine $q) } else { 'no question' }))))
        }
        'question.stop' {
            if (-not $c.Exists -or -not $c.Running) { $steps.Add((New-PlanStep -Kind refuse -Text ($who + ' is not running; there is nothing to stop.'))); break }
            $t = 'This stops the ' + $sh.Label + ' shard (' + $sh.Container + ') with docker stop -t ' + $Config.StopTimeout + '. '
            if ($q) { $t += 'It is waiting at "' + $q.Prompt + '", so nothing has loaded and stopping loses nothing. ' }
            else { $t += $script:NoSaveWarning + ' ' }
            $t += 'It stays stopped (restart: unless-stopped does not restart a container that was stopped) until it is started again.'
            $steps.Add((New-PlanStep -Kind confirm -Phrase ('stop ' + $ShardKey) -Text $t))
            $steps.Add((New-PlanStep -Kind exec -Exe 'docker' -Arguments @('stop', '-t', [string]$Config.StopTimeout, $sh.Container) -Text ('docker stop, never compose (D29). ' + $(if ($q) { 'Nothing is loaded yet.' } else { '' }))))
            $steps.Add((New-PlanStep -Kind stopped -Container $sh.Container -Text ('Checking ' + $sh.Container + ' is not running.')))
            $steps.Add((New-PlanStep -Kind log -Text ('question.stop ' + $ShardKey + ': ' + $(if ($q) { $q.Kind + ' ' + $q.Signature } else { 'no question' }))))
        }
        default {
            $key = $Action.Substring('question.'.Length)
            if (-not $q) { $steps.Add((New-PlanStep -Kind refuse -Text ($who + ' is not waiting on a console question, so there is nothing to answer.'))); break }
            $ans = @(@($q.Answers) | Where-Object { $_.Key -ceq $key })[0]
            if (-not $ans) {
                $keys = (@($q.Answers) | ForEach-Object { $_.Key }) -join ', '
                if (-not $keys) { $keys = 'none from here' }
                $steps.Add((New-PlanStep -Kind refuse -Text ('"' + $key + '" is not an answer this console sends to "' + $q.Prompt + '". Its answers: ' + $keys + '.'))); break
            }
            $shown = $key
            if ($key -eq 'enter') { $shown = 'Enter' }
            $text = $who + ' is asking: ' + (Get-QuestionLine $q) + '. Answer ' + $shown + ': ' + $ans.Explain
            $phrase = 'answer ' + $ShardKey
            $dest = $null
            if ($ans.Deletes) {
                if (-not $ss.Save.Exists) { $steps.Add((New-PlanStep -Kind refuse -Text ('There is no save at ' + $sh.Saves + ' to back up, so a delete answer is not sent.'))); break }
                $phrase = 'delete on ' + $ShardKey
                $dest = Join-Path (Join-Path $Config.SnapshotRoot $sh.Key) (New-SnapshotFolderName ('pre-answer-' + $key) $Now)
                $text = 'BEFORE YOU TYPE: this deletes ' + (Get-QuestionLoss $q $Config) + ' The ' + $sh.Label + ' save is backed up first, to ' + $dest + '. ' + $text
            }
            $steps.Add((New-PlanStep -Kind confirm -Phrase $phrase -Text $text))
            if ($dest) {
                $steps.Add((New-PlanStep -Kind copy -From $sh.Saves -To $dest -Text ('Backing up the ' + $sh.Label + ' save to ' + $dest + '. The shard is blocked in its load, so it is writing nothing.')))
                $steps.Add((New-PlanStep -Kind verify -From $sh.Saves -To $dest -Text 'Checking the backup has the same files and bytes.'))
            }
            $steps.Add((New-PlanStep -Kind answer -Container $sh.Container -Phrase $key -Signature $q.Signature -Text ('Typing ' + $shown + ' at "' + $q.Prompt + '" if the shard is still asking exactly that question.')))
            $steps.Add((New-PlanStep -Kind log -Text ('question.' + $key + ' ' + $ShardKey + ' answered ' + $shown + ' to ' + $q.Kind + ': ' + (Get-QuestionLine $q) + $(if ($dest) { '; save backed up to ' + $dest } else { '' }))))
        }
    }
    $steps.ToArray()
}

function Get-AtAGlance {
    # The three tiles: LIVE, TEST, and the status publisher. Same facts as the report below them.
    param($State, $Config)
    $tiles = New-Object 'System.Collections.Generic.List[object]'
    foreach ($k in @('live', 'test')) {
        $tiles.Add((Get-ShardGlance -Shard $Config.Shards[$k] -ShardState $State.Shards[$k] -Now $State.Now -Config $Config))
    }
    $p = $State.Publisher
    $pd = $p.Detail
    if ($p.State -eq 'PUBLISHING') {
        $pd = [string]$p.Players + ' playing, written ' + (Format-Age $p.Age) + ' ago'
        if ($p.Writers.Count) { $pd += ' by ' + (($p.Writers | ForEach-Object { $Config.Shards[$_].Label }) -join '+') }
    } elseif ($p.State -eq 'STALE') {
        $pd = 'last written ' + (Format-Age $p.Age) + ' ago; website shows offline'
    } elseif ($p.State -eq 'NOT PUBLISHING') {
        $pd = 'no status.json'
        if (-not $p.Writers.Count) { $pd += $(if (Test-LiveRetired $Config) { '; live, its only writer, is retired' } else { '; no shard has the status mount' }) }
    } elseif ($p.State -eq 'UNREADABLE') {
        $pd = 'status.json is not schema 1'
    }
    if ($p.FeedState -in @('NO CONTAINER', 'STOPPED')) { $pd += '; feed ' + $p.FeedState.ToLower() }
    elseif ($p.FeedState -eq 'SERVING') { $pd += '; feed serving' }
    elseif ($p.FeedState -eq 'NOT SERVING') { $pd += '; feed up, nothing to serve' }
    $tiles.Add([pscustomobject]@{ Title = 'STATUS PUBLISHER'; Word = $p.State; Color = $p.Color; Detail = $pd })
    $tiles.ToArray()
}

# --- public DNS and VPN (cc-P52 Part H, bug-list D47) ------------------------------------------
# Twice Chase's VPN made sl-ddns publish the VPN's exit as shatteredlegacyuo.com (146.70.217.106 on
# 2026-10-02, 159.26.100.68 on 2026-10-04), taking the site, the updater and the shard off the
# internet. ddns is not changed (Chase, 2026-10-04); the console says so plainly instead.
#
# The signals, gathered off the UI thread by Get-NetworkProbe (SHELL), judged here:
#   route    the interface Windows would use to reach 1.1.1.1 (Find-NetRoute). Not the LAN NIC (the
#            one holding HouseLanIp) means a VPN is carrying this PC's traffic. This is the signal
#            that decides "VPN on": VPN clients either replace the default route or add 0/1 and
#            128/1, and Find-NetRoute answers through either.
#   adapters up network adapters that look like a VPN (PPP or tunnel type, or a VPN client's name).
#            Shown, not decisive on its own: a split-tunnel VPN or Tailscale can be up while this
#            PC's traffic still leaves through the LAN.
#   dns      shatteredlegacyuo.com's A record from public DNS over HTTPS (Cloudflare, then Google).
#            Not Resolve-DnsName: inside the house the UniFi router answers every port-53 query
#            for it, even one sent to 1.1.1.1, with 192.168.1.58 (shard-migration
#            notes/player-package.md).
#   publicIp this PC's public address as Cloudflare sees it (cdn-cgi/trace). With no VPN it is
#            this house's address, so DNS must equal it. With a VPN on it is the VPN's exit, so it
#            says nothing about the house, and is not compared. No fetch made from inside the
#            house can be shown to bypass a full-tunnel VPN, and the router's WAN address needs
#            UniFi credentials the console does not have, so the house address is known only
#            while the VPN is off.
# A check that failed is grey UNKNOWN, never red. Red needs a signal that worked.

$script:VpnAdapterPattern = '(?i)vpn|wireguard|wintun|tap-windows|\btun\b|openvpn|nordlynx|proton|mullvad|anyconnect|fortinet|forticlient|globalprotect|pangp|surfshark|expressvpn|windscribe|\bpia\b|private internet access|cyberghost'

function Test-VpnAdapter {
    # An adapter (anything with Name, Description, Type and Up) that looks like a VPN's.
    param($Adapter)
    if (-not $Adapter -or -not $Adapter.Up) { return $false }
    if ([string]$Adapter.Type -in @('Ppp', 'Tunnel')) { return $true }
    return (([string]$Adapter.Name + ' ' + [string]$Adapter.Description) -match $script:VpnAdapterPattern)
}

function ConvertFrom-DohJson {
    # The A records in a DNS-over-HTTPS JSON answer (Cloudflare and Google share the format: type 1 is A).
    param([string]$Json)
    $doc = $Json | ConvertFrom-Json
    if ($null -eq $doc -or $doc.Status -ne 0) { throw ('DNS answered status ' + $(if ($doc) { $doc.Status } else { 'nothing' })) }
    @(@($doc.Answer) | Where-Object { $_ -and $_.type -eq 1 } | ForEach-Object { [string]$_.data })
}

function ConvertFrom-TraceText {
    # The ip= line of Cloudflare's cdn-cgi/trace.
    param([string]$Text)
    foreach ($line in ($Text -split "`n")) {
        if ($line.Trim() -match '^ip=(.+)$') { return $Matches[1].Trim() }
    }
    throw 'no ip= line in the trace'
}

function Get-DnsGlance {
    # The fourth tile. $Probe is Get-NetworkProbe's result, or $null while the first one runs.
    param($Probe, [string]$HostName = 'shatteredlegacyuo.com')
    $t = [ordered]@{ Title = 'PUBLIC DNS'; Word = 'CHECKING'; Color = 'gray'; Detail = 'checking public DNS and this PC''s route...' }
    if (-not $Probe) { return [pscustomobject]$t }

    $dns = @($Probe.DnsIps)
    $dnsText = 'unknown'
    if (-not $Probe.DnsError -and $dns.Count) { $dnsText = $dns -join ', ' }
    $adapters = @($Probe.VpnAdapters)
    $adapterText = ''
    if ($adapters.Count) { $adapterText = ' VPN adapter up: ' + ($adapters -join ', ') + '.' }

    if ($Probe.RouteIsLan -eq $false) {
        $t.Word = 'VPN ON'; $t.Color = 'red'
        $t.Detail = 'VPN on: public DNS points at ' + $dnsText + '. Turn the VPN off. (Traffic leaves through ' + $Probe.RouteInterface + ', not the LAN.' + $adapterText + ')'
        return [pscustomobject]$t
    }
    if ($null -eq $Probe.RouteIsLan) {
        $t.Word = 'UNKNOWN'
        $t.Detail = 'could not read this PC''s route (' + $Probe.RouteError + ').' + $adapterText
        return [pscustomobject]$t
    }
    if ($Probe.DnsError -or -not $dns.Count) {
        $t.Word = 'UNKNOWN'
        $why = $Probe.DnsError
        if (-not $why) { $why = 'no A record' }
        $t.Detail = 'public DNS for ' + $HostName + ' could not be read (' + $why + '). No VPN route.' + $adapterText
        return [pscustomobject]$t
    }
    if ($Probe.PublicIpError -or -not $Probe.PublicIp) {
        $t.Word = 'UNKNOWN'
        $t.Detail = 'public DNS points at ' + $dnsText + '; this house''s address could not be read (' + $Probe.PublicIpError + '). No VPN route.'
        return [pscustomobject]$t
    }
    if ($dns -contains $Probe.PublicIp) {
        $t.Word = 'OK'; $t.Color = 'green'
        $t.Detail = $HostName + ' = ' + $Probe.PublicIp + ', this house. No VPN.' + $adapterText
        return [pscustomobject]$t
    }
    $t.Word = 'WRONG IP'; $t.Color = 'red'
    $t.Detail = 'VPN on: public DNS points at ' + $dnsText + ', not this house (' + $Probe.PublicIp + '). Turn the VPN off; ddns puts it back within 10 minutes.'
    [pscustomobject]$t
}

# --- Disconnect VPN (cc-P86) ---------------------------------------------------------------------------------------
# The PUBLIC DNS tile says "VPN ON ... Turn the VPN off"; this button does what can be done safely, per VPN, and says
# the rest. Research: shard-migration notes/cc-P86-console-vpn-disconnect.md, Part A.
#   Proton VPN   Proton's Windows app (v5.1.8 here) has no command line, no local API and no working URI command for
#                a disconnect (its protonvpn: and proton-vpn: keys carry no shell\open\command). Stopping or killing
#                ProtonVPNService is refused: Proton's Advanced kill switch keeps blocking every connection outside
#                the tunnel after a disconnect or a stopped service, which would leave Chase offline. So the button
#                brings Proton's window up (its own launcher) and says what to click, and that the Advanced kill
#                switch must be off first.
#   WireGuard    a tunnel service run by WireGuard for Windows (C:\Program Files\WireGuard\wireguard.exe) is ended by
#                wireguard.exe /uninstalltunnelservice <tunnel>, what WireGuard's own Deactivate does; its config
#                stays, and the tunnel's firewall rules go with the service. Needs an elevated console.
#   Other apps   a WireGuard tunnel another app installed (WiFiman Desktop here) and any other VPN adapter: the
#                button names it and says to disconnect it in its app. No adapter is ever disabled (that needs admin
#                and can leave the adapter off).
# Never as SYSTEM: Proton's app runs in Chase's session.

$script:ProtonPattern = '(?i)proton'

function Test-VpnButtonEnabled {
    # On when the tile says VPN ON or anything VPN-shaped is up; otherwise the button is greyed.
    param($Glance, $Facts)
    if ($Glance -and $Glance.Word -eq 'VPN ON') { return $true }
    if (-not $Facts) { return $false }
    return (@($Facts.VpnAdapters).Count -gt 0) -or (@($Facts.WireGuardTunnels | Where-Object { $_.Running }).Count -gt 0) -or [bool]$Facts.ProtonTunnelRunning -or ($Facts.RouteIsLan -eq $false)
}

function Get-VpnParts {
    # What is up, sorted by who can end it: Proton, WireGuard for Windows tunnels, another app's tunnels, other adapters.
    param($Facts)
    $tunnels = @($Facts.WireGuardTunnels | Where-Object { $_.Running })
    $own = @($tunnels | Where-Object { $_.Owner -eq 'WireGuard' })
    $apps = @($tunnels | Where-Object { $_.Owner -ne 'WireGuard' })
    $adapters = @($Facts.VpnAdapters)
    $proton = [bool]$Facts.ProtonTunnelRunning -or @($adapters | Where-Object { ([string]$_.Name + ' ' + [string]$_.Description) -match $script:ProtonPattern }).Count -gt 0
    $tunnelNames = @($tunnels | ForEach-Object { $_.Tunnel })
    $other = @($adapters | Where-Object { -not (([string]$_.Name + ' ' + [string]$_.Description) -match $script:ProtonPattern) -and ($tunnelNames -notcontains [string]$_.Name) })
    [pscustomobject]@{ Proton = $proton; OwnTunnels = $own; AppTunnels = $apps; OtherAdapters = $other
        RouteOnly = (-not $proton -and -not $tunnels.Count -and -not $other.Count -and $Facts.RouteIsLan -eq $false) }
}

function Test-VpnNeedsElevation {
    param($Facts)
    $p = Get-VpnParts $Facts
    return ($p.OwnTunnels.Count -gt 0 -and -not $Facts.IsAdmin)
}

function Get-VpnPlan {
    param($Config, $Facts)
    if (-not $Facts) { return @(New-PlanStep -Kind refuse -Text 'Could not read this PC''s VPN state. Nothing was done.') }
    if ($Facts.IsSystem) { return @(New-PlanStep -Kind refuse -Text ('This console is running as ' + $Facts.Identity + '. Proton''s app runs in your own Windows session, so disconnect from your own login.')) }
    $p = Get-VpnParts $Facts
    if (-not $p.Proton -and -not $p.OwnTunnels.Count -and -not $p.AppTunnels.Count -and -not $p.OtherAdapters.Count -and -not $p.RouteOnly) {
        return @(New-PlanStep -Kind refuse -Text 'No VPN is up: nothing to disconnect.')
    }
    $steps = New-Object 'System.Collections.Generic.List[object]'
    if ($p.Proton) {
        $steps.Add((New-PlanStep -Kind say -Text 'Proton VPN is connected. Its Windows app has no supported way to disconnect from a script, so the console opens it: click Disconnect. If its Advanced kill switch is on, turn it off first (Settings > Connection > Kill switch), or this PC stays offline after the disconnect.'))
        if ($Facts.ProtonLauncher) { $steps.Add((New-PlanStep -Kind launch -Path $Facts.ProtonLauncher -Text 'Opening Proton VPN.')) }
        else { $steps.Add((New-PlanStep -Kind say -Text 'Proton VPN''s launcher was not found where it installs (C:\Program Files\Proton\VPN). Open it from the tray.')) }
    }
    foreach ($t in $p.OwnTunnels) {
        if ($Facts.IsAdmin) {
            $steps.Add((New-PlanStep -Kind exec -Exe $t.Exe -Arguments @('/uninstalltunnelservice', $t.Tunnel) -Text ('Ending the WireGuard tunnel ' + $t.Tunnel + ' (its config stays in WireGuard).')))
        } else {
            $steps.Add((New-PlanStep -Kind say -Text ('The WireGuard tunnel ' + $t.Tunnel + ' needs an elevated console to end: run the console as administrator, or deactivate it in WireGuard.')))
        }
    }
    foreach ($t in $p.AppTunnels) {
        $steps.Add((New-PlanStep -Kind say -Text ('The WireGuard tunnel ' + $t.Tunnel + ' belongs to ' + $t.Owner + ': disconnect it in that app.')))
    }
    foreach ($a in $p.OtherAdapters) {
        $steps.Add((New-PlanStep -Kind say -Text ($a.Name + ' (' + $a.Description + ') looks like a VPN: disconnect it in its app.')))
    }
    if ($p.RouteOnly) {
        $steps.Add((New-PlanStep -Kind say -Text 'Traffic leaves through a route that is not the LAN, but no VPN adapter or tunnel could be named: disconnect the VPN in its app.'))
    }
    $steps.Add((New-PlanStep -Kind say -Text 'The PUBLIC DNS tile checks again in a few seconds and says whether the VPN is off.'))
    $steps.ToArray()
}

function Get-DdnsRestartPlan {
    # Offered, never automatic: after the VPN is off, a restart makes sl-ddns publish this house's address now instead
    # of within its interval. The same container, restarted; nothing else changes.
    # cc-P668: docker\uo\Test-DdnsSafe.ps1 first. It exits 3 for an sl-ddns that takes its address from this PC (the VPN's
    # exit when the VPN is on), and the plan stops there: only the gate-fed sl-ddns is ever restarted from here.
    param($Config)
    $guard = Join-Path (Split-Path -Parent $Config.LiveCompose) 'Test-DdnsSafe.ps1'
    @(
        (New-PlanStep -Kind say -Text 'Restarting sl-ddns so it publishes this house''s address now (it checks on start).'),
        (New-PlanStep -Kind exec -Exe $Config.PowerShell -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $guard) -Text 'Test-DdnsSafe.ps1: sl-ddns takes Haven''s address only (cc-P668); stops here if not'),
        (New-PlanStep -Kind exec -Exe 'docker' -Arguments @('restart', 'sl-ddns') -Text 'docker restart sl-ddns')
    )
}

# --- the Golem, the CC queue runner (cc-P74 as the autopilot; renamed by cc-P99) -------------------
# The Golem (D:\UO\golem; D:\UO\cc-autopilot before the cutover) hands CC the next queued prompt when the last one ends
# "P<nn> DONE", running Commit-ShardWork.ps1 in between, and emails him when CC needs him. Its hook,
# cc-autopilot.ps1, keeps everything this reads:
#   ENABLED        present means on. The hook removes it itself when the queue runs dry.
#   state.json     current (P74, empty when idle), currentFile, lastHandled, startedAt (local, 's').
#                  No file is the hook's own default: nothing running, nothing handled yet.
#   golem.log      "yyyy-MM-dd HH:mm:ss  <Event padded to 12> <text>", one line per action, with
#   (autopilot.log) Commit-ShardWork.ps1's raw output pasted in after a commit. Mail it sent is logged
#                  "emailed: [Golem] <subject>" ([Shard CC] before cc-P99), mail that failed "EMAIL FAILED: <subject>  (why)".
#   queue\         cc-P<nn>-*.md run lowest number first; any other .md is ignored (and logged).
# The shell tails the log and lists the queue (Get-AutopilotFacts); everything is judged here.

$script:AutopilotLinePattern = '^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})  ([A-Za-z]+) +(.*)$'
$script:AutopilotSubjects    = @('[Golem] ', '[Shard CC] ')

function ConvertFrom-AutopilotLog {
    # The action lines of a log tail, oldest first. Anything else is pasted commit output, skipped.
    param([string[]]$Lines)
    $out = New-Object 'System.Collections.Generic.List[object]'
    foreach ($l in @($Lines)) {
        if ($null -eq $l) { continue }
        $m = [regex]::Match([string]$l, $script:AutopilotLinePattern)
        if (-not $m.Success) { continue }
        $out.Add([pscustomobject]@{
            Time  = [datetime]::ParseExact($m.Groups[1].Value, 'yyyy-MM-dd HH:mm:ss', $script:Invariant)
            Event = $m.Groups[2].Value
            Text  = $m.Groups[3].Value.TrimEnd()
            Line  = ([string]$l).TrimEnd()
        })
    }
    $out.ToArray()
}

function Get-AutopilotQueue {
    # The queue folder's file names, as the hook reads them: what runs, in order, and what it ignores.
    param([string[]]$Names, [string]$RunNext)
    # cc-P125: the Golem's order: continuations (cc-P<nn>-cont-*.md) first, then lowest number first.
    $runs = @(@($Names) | Where-Object { $_ -match '^cc-P(\d+)-.*\.md$' } |
        Sort-Object { if ($_ -match '^cc-P\d+-cont-') { 0 } else { 1 } }, { [int]([regex]::Match($_, '^cc-P(\d+)-').Groups[1].Value) }, { $_ })
    # cc-P182: Run next's pick (the Golem's RUN-NEXT) goes first; a name no longer queued is ignored, as the Golem does.
    if ($RunNext -and $runs -contains $RunNext) { $runs = @(@($RunNext) + @($runs | Where-Object { $_ -ne $RunNext })) }
    $ignored = @(@($Names) | Where-Object { $_ -match '\.md$' -and $_ -notmatch '^cc-P\d+-' } | Sort-Object)
    [pscustomobject]@{ Runs = $runs; Ignored = $ignored }
}

function ConvertFrom-AutopilotState {
    # state.json, or the hook's own default when there is no file ($null). Throws when it cannot be read.
    param($Text)
    $s = [pscustomobject]@{ Current = ''; CurrentFile = ''; LastHandled = 0; StartedAt = $null }
    if ($null -eq $Text) { return $s }
    $Text = [string]$Text
    if (-not $Text.Trim()) { throw 'state.json is empty' }
    $j = $Text | ConvertFrom-Json
    if ($null -eq $j -or $j -isnot [pscustomobject]) { throw 'state.json is not a JSON object' }
    $s.Current = [string]$j.current
    $s.CurrentFile = [string]$j.currentFile
    $n = 0
    if ($null -ne $j.lastHandled -and -not [int]::TryParse([string]$j.lastHandled, [ref]$n)) { throw ('lastHandled is not a number: ' + $j.lastHandled) }
    $s.LastHandled = $n
    if ($j.startedAt -is [datetime]) { $s.StartedAt = $j.startedAt }
    elseif ([string]$j.startedAt) {
        $t = [datetime]::MinValue
        if ([datetime]::TryParseExact([string]$j.startedAt, 'yyyy-MM-ddTHH:mm:ss', $script:Invariant, [Globalization.DateTimeStyles]::None, [ref]$t)) { $s.StartedAt = $t }
    }
    $s
}

function Test-AutopilotAlert {
    # An emailed subject that means Chase is needed: the hook's NEEDS YOU, STOPPED and COMMIT FAILED mail.
    param([string]$Subject)
    ($Subject -match '^NEEDS YOU' -or $Subject -cmatch '\bSTOPPED\b' -or $Subject -cmatch '\bCOMMIT FAILED\b')
}

# cc-P94: status.json, the one file cc-autopilot.ps1 (Write-Status) rewrites, tmp then rename, on every hand-off,
# stop, tool use and every 10 minutes from the scheduled task "Golem Watch" ("Shard CC Autopilot Watch" before cc-P99). Schema 1:
#   state (OFF | IDLE | RUNNING | NEEDS YOU), enabled, current, currentFile, startedAt (local 's'), position and
#   total (prompt N of M in this run), runDone, runStarted, maxPerRun, lastHandled, queued (names, in run order),
#   stepsDone, stepsTotal, stepNow (CC's task list), lastActivityUtc, lastTool, toolStartedUtc (set while a tool
#   runs; all three UTC 's'), needsYou (empty when nothing is needed), generatedAt (local 's').
# settings.json: progressEmails, maxPerRun, stallMinutes; the hook's defaults (Get-Settings) when a key is missing.
$script:AutopilotStaleMinutes = 15
# cc-P131: autoWake and the night shift keys too (the hook's defaults, Get-Settings in golem.ps1).
$script:AutopilotSettingDefaults = [ordered]@{ progressEmails = $true; maxPerRun = 6; stallMinutes = 45; autoWake = $true; nightShiftEnabled = $true; nightShiftStart = '22:30'; nightShiftEnd = '06:30'
    dayWeekAheadPct = 3; weekReservePct = 10; planUsageMaxAgeHours = 4   # cc-P160: the Golem's day pace rule
    marathon = 'auto'   # cc-P200: marathon mode (auto, on, off)
    mailMode = 'all' }   # cc-P479: which emails go out (all, briefs, off)

function ConvertFrom-AutopilotTime {
    # A 's' time from the hook, as written ($Kind Local or Utc), or $null. ConvertFrom-Json in PowerShell 7
    # already makes a DateTime of it; 5.1 leaves the string.
    param($Value, [DateTimeKind]$Kind)
    if ($null -eq $Value) { return $null }
    if ($Value -is [datetime]) { return [datetime]::SpecifyKind($Value, $Kind) }
    $t = [datetime]::MinValue
    if ([datetime]::TryParseExact([string]$Value, 'yyyy-MM-ddTHH:mm:ss', $script:Invariant, [Globalization.DateTimeStyles]::None, [ref]$t)) { return [datetime]::SpecifyKind($t, $Kind) }
    $null
}

function ConvertFrom-AutopilotStatus {
    # status.json, or $null when there is none. Throws when it cannot be read or is not schema 1.
    param($Text)
    if ($null -eq $Text) { return $null }
    $Text = [string]$Text
    if (-not $Text.Trim()) { throw 'status.json is empty' }
    $j = $Text | ConvertFrom-Json
    if ($null -eq $j -or $j -isnot [pscustomobject]) { throw 'status.json is not a JSON object' }
    if ([string]$j.schema -ne '1') { throw ('status.json is schema ' + $(if ($null -eq $j.schema) { '(none)' } else { [string]$j.schema }) + ', not 1') }
    $state = ([string]$j.state).Trim().ToUpperInvariant()
    # cc-P131: RESTING (the Golem pausing on purpose; restReason says why) and STOPPED (a prompt that needs Chase).
    if (@('OFF', 'IDLE', 'RUNNING', 'NEEDS YOU', 'RESTING', 'STOPPED') -notcontains $state) { throw ('status.json state is not one of OFF, IDLE, RUNNING, NEEDS YOU, RESTING, STOPPED: ' + $j.state) }
    $int = { param($v) $n = 0; if ($null -ne $v) { [void][int]::TryParse([string]$v, [ref]$n) }; $n }
    # The hook writes queued as an array; a one-item array written by another ConvertTo-Json can arrive as a string.
    $queued = @(@($j.queued) | Where-Object { $_ } | ForEach-Object { [string]$_ })
    [pscustomobject]@{
        State          = $state
        Enabled        = [bool]$j.enabled
        Current        = [string]$j.current
        CurrentFile    = [string]$j.currentFile
        StartedAt      = (ConvertFrom-AutopilotTime $j.startedAt 'Local')
        Position       = (& $int $j.position)
        Total          = (& $int $j.total)
        RunDone        = (& $int $j.runDone)
        RunStarted     = (ConvertFrom-AutopilotTime $j.runStarted 'Local')
        MaxPerRun      = (& $int $j.maxPerRun)
        LastHandled    = (& $int $j.lastHandled)
        Queued         = $queued
        StepsDone      = (& $int $j.stepsDone)
        StepsTotal     = (& $int $j.stepsTotal)
        StepNow        = ([string]$j.stepNow).Trim()
        # cc-P153: 'tasks' (CC's task list), 'set-step' (Set-Step.ps1) or ''; and the time estimate from the Golem's history.
        StepsSource    = ([string]$j.stepsSource).Trim()
        Estimate       = $j.estimate
        LastActivity   = (ConvertFrom-AutopilotTime $j.lastActivityUtc 'Utc')
        LastTool       = [string]$j.lastTool
        ToolStarted    = (ConvertFrom-AutopilotTime $j.toolStartedUtc 'Utc')
        NeedsYou       = ([string]$j.needsYou).Trim()
        # cc-P99 (D91): a plain word for a running prompt that is not a question, e.g. 'waiting on background agents'.
        Note           = ([string]$j.note).Trim()
        GeneratedAt    = (ConvertFrom-AutopilotTime $j.generatedAt 'Local')
        # cc-P125: winding down and resting with ENABLED kept, the long run, the model the next prompt waits for.
        Winding         = ([string]$j.winding).Trim()
        StopTold        = [bool]$j.stopTold
        RestReason      = ([string]$j.restReason).Trim()
        RestAfter       = ([string]$j.restAfter).Trim()
        WaitingForModel = ([string]$j.waitingForModel).Trim()
        # cc-P183: the prompt the model rest waits for, from the Golem's fresh read of the queue.
        WaitingForPrompt = ([string]$j.waitingForPrompt).Trim()
        LongRun         = $j.longRun
        # cc-P131: when a rest can run again (readyAt local, readyHow words), the prompt that said STOPPED, the Golem's
        # own wakes, the night shift and the CLI as the Golem last saw them.
        ReadyAt         = (ConvertFrom-AutopilotTime $j.readyAt 'Local')
        ReadyHow        = ([string]$j.readyHow).Trim()
        StoppedPrompt   = ([string]$j.stoppedPrompt).Trim()
        AutoWake        = $(if ($null -eq $j.autoWake) { $true } else { [bool]$j.autoWake })
        WakePending     = $j.wakePending
        LastSelfWake    = $j.lastSelfWake
        NightShift      = $j.nightShift
        Cli             = $j.cli
        # cc-P161: the sessions the Golem opened (open, kept open for Chase after a PAUSED or STOPPED, last closed).
        Sessions        = $j.sessions
        # cc-P162: the end-of-week window (use it or lose it): on, active, until, the cap, the week and Fable week now.
        EndOfWeek       = $j.endOfWeek
        # cc-P200: the week at its reset at the current rate (text, the figures, the free reset advice) and marathon mode.
        Projection      = $j.projection
        Marathon        = $j.marathon
        # cc-P184: several Golems: the switch, which Golem wrote this file, one card per Golem, and a registry problem.
        MultiGolem      = [bool]$j.multiGolem
        Away            = $j.away           # cc-P149: on, until, the parked prompts
        MailCommands    = $j.mailCommands   # cc-P149: on, set up, the last commands (accepted and rejected)
        GolemName       = [string]$j.golemName
        Golems          = @(@($j.golems) | Where-Object { $_ })
        RegistryError   = [string]$j.registryError
    }
}

function ConvertTo-GolemCardDocument {
    # cc-P184: one card of status.json's golems as the status document carries it for the web console. cc-P186: with
    # its last readiness check (verdict, at, summary, machine, the checks that failed, the laptop facts), remote,
    # online and lastSeen (a remote Golem's agent), its portrait path ('' for none: the web console serves it only from
    # scripts\console-art), and winding and stoppedPrompt, for its buttons on its own card.
    param($Card)
    $c = $Card
    $r = $c.ready
    [ordered]@{ name = [string]$c.name; machine = [string]$c.machine; engine = [string]$c.engine; lane = [string]$c.lane; state = [string]$c.state; current = [string]$c.current
        branch = [string]$c.branch; locks = @(@($c.locks) | Where-Object { $_ } | ForEach-Object { [string]$_ }); usageSource = [string]$c.usageSource; integrator = [bool]$c.integrator
        enabled = [bool]$c.enabled; restReason = [string]$c.restReason; needsYou = [string]$c.needsYou; hours = [string]$c.hours
        marathonDay = [bool]$c.marathonDay; noDaytimeMarathon = [bool]$c.noDaytimeMarathon   # cc-P200
        winding = [string]$c.winding; stoppedPrompt = [string]$c.stoppedPrompt; remote = [bool]$c.remote; online = [bool]$c.online
        lastSeen = $(if ($c.lastSeen) { [string]$c.lastSeen } else { $null }); portrait = [string]$c.portrait
        # cc-P208: home (the console's own Golem), script (the merge Golem, Shadow Iron) and its merge block, its display name.
        home = [bool]$c.home; script = [bool]$c.script; display = $(if ($c.display) { [string]$c.display } else { [string]$c.name })
        laneLine = [string]$c.laneLine   # cc-P159: a Codex lane's line (the step, paused, in use, asleep)
        merge = $(if ($c.merge) { $c.merge } else { $null })
        ready = $(if ($r -and $r.verdict) { [ordered]@{ verdict = [string]$r.verdict; at = [string]$r.at; summary = [string]$r.summary; machine = [string]$r.machine
                    failed = [object[]]@(@($r.failed) | Where-Object { $_ } | ForEach-Object { [string]$_ }); laptop = $(if ($r.laptop) { $r.laptop } else { $null }) } } else { $null }) }
}

function Get-GolemScopedConfig {
    # cc-P186: the console's config for another Golem of several on this PC (golems.json): its own ENABLED-<name> and
    # state-, status- and golem-<name> files, as golem.ps1 names them (Get-GolemFileName); the same scripts. A copy:
    # $Config is unchanged. The web console's runner (Invoke-WebConsoleAction.ps1 -Golem) reads that Golem's facts
    # with it, so its plan refuses and asks as the console would for that Golem.
    param($Config, [string]$Name)
    $c = $Config.PSObject.Copy()
    $ap = $Config.Autopilot.PSObject.Copy()
    $ap.Enabled = Join-Path $ap.Root ('ENABLED-' + $Name)
    $ap.StateFile = Join-Path $ap.Root ('state-' + $Name + '.json')
    $ap.StatusFile = Join-Path $ap.Root ('status-' + $Name + '.json')
    $ap.LogName = 'golem-' + $Name + '.log'
    $ap.LogFile = Join-Path $ap.Root $ap.LogName
    $ap | Add-Member -NotePropertyName GolemName -NotePropertyValue $Name -Force
    $c.Autopilot = $ap
    $c
}

function Get-GolemPickRows {
    # cc-P187: the Golem picker's rows (the Shard Console's Golem tab; server.py builds the web console's from the same
    # golems.json): every Golem in the registry with its lane and machine, and whether the console can act for it here
    # and why not: disabled in golems.json; multiGolem off (only the integrator works the queue); on another PC (the
    # web console queues its buttons for that PC's agent). The integrator first. Pure: the two files' texts in.
    param([string]$RegistryText, [string]$SettingsText, [string]$Machine = $env:COMPUTERNAME)
    $reg = $null
    try { $reg = $RegistryText | ConvertFrom-Json } catch { }
    $multi = $false
    try { $multi = [bool](($SettingsText | ConvertFrom-Json).multiGolem) } catch { }
    # cc-P208: the console's own Golem is the home one: the integrator, or with a script integrator (Shadow Iron, which
    # has no buttons but its card's) the first Golem that claims on its PC. A script Golem is not in the picker.
    $homeName = Get-GolemHomeName $reg
    $rows = foreach ($g in @($(if ($reg) { $reg.golems } else { @() }))) {
        if (-not $g -or [string]$g.name -notmatch '^[A-Za-z][A-Za-z0-9]{0,23}$') { continue }
        if ([string]$g.engine -eq 'script') { continue }
        $int = ([string]$g.name -ieq $homeName)
        $why = ''
        if (-not $int) {
            if (-not [bool]$g.enabled) { $why = 'disabled in golems.json' }
            elseif (-not $multi) { $why = 'multiGolem is off (settings.json): only the integrator works the queue' }
            elseif (-not [string]$g.machine) { $why = 'golems.json names no machine for it yet' }
            elseif ([string]$g.machine -ine $Machine) { $why = 'on ' + [string]$g.machine + ': the web console queues its buttons for that PC''s agent' }
        }
        $where = ((@([string]$g.lane, [string]$g.machine) | Where-Object { $_ }) -join ', ')
        # cc-P373: Remote (enabled, on another PC) and Engine let Send to the second PC reach a Codex lane there.
        $remote = ([bool]$g.enabled -and $multi -and [string]$g.machine -and [string]$g.machine -ine $Machine)
        [pscustomobject]@{ Name = [string]$g.name; Lane = [string]$g.lane; Machine = [string]$g.machine; Integrator = $int; Usable = (-not $why); Why = $why
            Remote = $remote; Engine = [string]$g.engine
            Text = ([string]$g.name + ' (' + $where + ')' + $(if ($int) { ', integrator' } else { '' }) + $(if ($why) { ': ' + $why } else { '' })) }
    }
    @($rows | Sort-Object @{ Expression = { -not $_.Integrator } }, Name)
}

function Test-MergePaused {
    # cc-P208: is Shadow Iron (settings.json mergeGolem) paused? Its PAUSED-<name> file in the Golem folder.
    param($Config, [string]$SettingsText)
    $n = ''; try { $n = [string]($SettingsText | ConvertFrom-Json).mergeGolem } catch { }
    if (-not $n -or -not $Config.Autopilot.GolemRoot) { return $false }
    Test-Path -LiteralPath (Join-Path $Config.Autopilot.GolemRoot ('PAUSED-' + $n))
}

function Get-GolemHomeName {
    # cc-P208: golems.json's home Golem (Golems.ps1 Get-GolemHome): the integrator, or with a script integrator the first
    # Golem that claims on its PC. '' when there is no registry.
    param($Registry)
    $list = @($(if ($Registry) { @($Registry.golems) } else { @() }) | Where-Object { $_ })
    $int = @($list | Where-Object { [bool]$_.integrator }) | Select-Object -First 1
    if (-not $int) { return '' }
    if ([string]$int.engine -ne 'script') { return [string]$int.name }
    $h = @($list | Where-Object { [string]$_.engine -ne 'script' -and [string]$_.machine -ieq [string]$int.machine }) | Select-Object -First 1
    $(if ($h) { [string]$h.name } else { [string]$int.name })
}

function Get-AnyGolemActionKeys {
    # cc-P187: the console actions the web console runs for any Golem (golem-actions.json's anyGolem buttons); the other
    # Golem buttons are the integrator's only.
    param([string]$ActionsFile)
    try { @(@((Get-Content -LiteralPath $ActionsFile -Raw | ConvertFrom-Json).actions) | Where-Object { $_.anyGolem -and $_.action } | ForEach-Object { [string]$_.action } | Select-Object -Unique) } catch { @() }
}

function Get-GolemActionConfig {
    # cc-P187: a Golem button acting for the Golem picked on the Golem tab. The integrator, or nothing picked: the
    # console's own config, as before. Another Golem on this PC: its own files (Get-GolemScopedConfig) and, for each
    # Golem script in the plan, its name (Add-GolemToPlan), for the buttons the web console runs for any Golem; the
    # rest are the integrator's only. Refusal (with the reason) when the picked Golem cannot be acted for from here.
    param($Config, [string]$ActionKey, [string]$Golem, $Rows, [string[]]$AnyGolemActions)
    $o = [pscustomobject]@{ Config = $Config; Name = ''; Refusal = '' }
    if (-not $Golem) { return $o }
    $r = @(@($Rows) | Where-Object { $_ -and $_.Name -ieq $Golem }) | Select-Object -First 1
    if (-not $r) { $o.Refusal = 'there is no ' + $Golem + ' Golem in golems.json. Nothing was run.'; return $o }
    if ($r.Integrator) { return $o }
    # cc-P373: Send to the second PC for a Codex lane on another PC: Lane-Now.ps1 here queues it for that PC's agent
    # (Remote-Agent.ps1 runs Lane-Now.ps1 there), as the web console does. P159's review: the button refused it. The
    # other buttons for another PC stay the web console's.
    $isRemoteLane = ($r.PSObject.Properties.Name -contains 'Remote' -and [bool]$r.Remote -and [string]$r.Engine -eq 'codex')
    if ($ActionKey -eq 'golem.lanenow' -and -not $r.Usable -and $isRemoteLane) {
        $o.Config = Get-GolemScopedConfig -Config $Config -Name $r.Name
        $o.Name = $r.Name
        return $o
    }
    if (-not $r.Usable) { $o.Refusal = 'the ' + $r.Name + ' Golem: ' + $r.Why + '. Nothing was run.'; return $o }
    if (@($AnyGolemActions) -notcontains $ActionKey) { $o.Refusal = 'that button is the integrator''s only, not the ' + $r.Name + ' Golem''s: pick the integrator for it. Nothing was run.'; return $o }
    $o.Config = Get-GolemScopedConfig -Config $Config -Name $r.Name
    $o.Name = $r.Name
    $o
}

function Get-GolemNamedScripts {
    # cc-P186: the Golem scripts that take -Golem <name> (one Golem of several); the others are the integrator's only.
    param($Ap)
    @($Ap.Start, $Ap.Stop, $Ap.WakeScript, $Ap.PokeScript, $Ap.WrapUp, $Ap.StopPoint, $Ap.RunNextScript, $Ap.RetryScript, $Ap.LaneNowScript) | Where-Object { $_ }
}

function Add-GolemToPlan {
    # cc-P186: the same plan with each Golem script it runs told which Golem (-Golem <name>, last), so Test-PlanSafety
    # still knows every argument. Steps are copied; the plan given is unchanged.
    param($Plan, $Config, [string]$Name)
    $scripts = @(Get-GolemNamedScripts $Config.Autopilot)
    foreach ($s in @($Plan)) {
        if ($s.Kind -eq 'window' -and @($s.Arguments).Count -ge 5 -and $scripts -icontains [string]@($s.Arguments)[4]) {
            $n = $s.PSObject.Copy()
            $n.Arguments = @(@($s.Arguments) + @('-Golem', $Name))
            $n
        } else { $s }
    }
}

function Get-OpenGolemQuestions {
    # cc-P216: every Golem's open questions for Chase: this PC's (questions\<id>.json, written by Questions.ps1) and the
    # remote Golems' as their agents last posted them to the web console (remote\<name>\questions.json). Newest first:
    # { Id, Golem, Prompt, Kind, Question, Choices, FreeText, Context, AskedAt, Irreversible, IrreversibleWhy, Remote }.
    param([string]$Root)
    $out = New-Object 'System.Collections.Generic.List[object]'
    if (-not $Root) { return @() }
    $rx = '^([A-Za-z][A-Za-z0-9]{0,23})-P\d{1,4}-\d{8}-\d{6}-[0-9a-f]{4}$'
    $add = {
        param($q, [bool]$remote)
        $m = $(if ($q) { [regex]::Match([string]$q.id, $rx) } else { $null })
        if (-not $q -or [string]$q.state -ne 'open' -or -not $m.Success) { return }
        $out.Add([pscustomobject]@{ Id = [string]$q.id; Golem = $m.Groups[1].Value; Prompt = [string]$q.prompt; Kind = [string]$q.kind; Question = [string]$q.question
            Choices = @($q.choices | Where-Object { $_ }); FreeText = [bool]$q.freeText; Context = [string]$q.context; AskedAt = [string]$q.askedAt
            Irreversible = [bool]$q.irreversible; IrreversibleWhy = [string]$q.irreversibleWhy; Remote = $remote })
    }
    $qd = Join-Path $Root 'questions'
    foreach ($f in @(Get-ChildItem -LiteralPath $qd -Filter '*.json' -File -ErrorAction SilentlyContinue)) {
        $q = $null; try { $q = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8) | ConvertFrom-Json } catch { }
        if ($q -and [string]$q.id -ceq $f.BaseName) { & $add $q $false }
    }
    foreach ($d in @(Get-ChildItem -LiteralPath (Join-Path $Root 'remote') -Directory -ErrorAction SilentlyContinue)) {
        $r = $null; try { $r = [IO.File]::ReadAllText((Join-Path $d.FullName 'questions.json'), [Text.Encoding]::UTF8) | ConvertFrom-Json } catch { }
        foreach ($q in @($(if ($r) { @($r.open) } else { @() }) | Where-Object { $_ })) { if ([string]$q.id -like ($d.Name + '-P*')) { & $add $q $true } }
    }
    @($out | Sort-Object AskedAt -Descending)
}

function Get-GolemQuestionLines {
    # cc-P216: the Golem tab's lines for the open questions, in full: who asks on which prompt, the question, each choice
    # and what the Golem then does, free text, and CANNOT BE UNDONE when it says so. Rows { Text, Color }.
    param([object[]]$Questions)
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $qs = @($Questions | Where-Object { $_ })
    if (-not $qs.Count) { return $rows.ToArray() }
    $rows.Add([pscustomobject]@{ Text = ('questions : ' + $qs.Count + ' open for you (answer: Answer a Golem''s question here, or the web console''s Golem card)'); Color = 'amber' })
    foreach ($q in $qs) {
        $at = ''; try { $at = ([datetime]::Parse($q.AskedAt)).ToString('ddd HH:mm') } catch { }
        $rows.Add([pscustomobject]@{ Text = ('            ' + $q.Golem + ' on ' + $q.Prompt + ' (' + $(if ($q.Kind -eq 'needsyou') { 'waiting on you' } else { $q.Kind }) + ', ' + $at + $(if ($q.Remote) { ', another PC: answer on the web console' } else { '' }) + '): ' + (($q.Question -replace '\s+', ' ').Trim())); Color = 'amber' })
        $i = 0
        foreach ($c in @($q.Choices)) { $i++; $rows.Add([pscustomobject]@{ Text = ('              ' + $i + '. ' + [string]$c.label + $(if ([string]$c.does) { ': ' + [string]$c.does } else { '' })); Color = 'normal' }) }
        if ($q.FreeText) { $rows.Add([pscustomobject]@{ Text = '              or your own words'; Color = 'gray' }) }
        if ($q.Irreversible) { $rows.Add([pscustomobject]@{ Text = ('              CANNOT BE UNDONE: ' + $q.IrreversibleWhy); Color = 'red' }) }
    }
    $rows.ToArray()
}

function Show-AnswerPicker {
    # cc-P216: Answer a Golem's question: a row per choice of each open question on this PC (and "my own words only"
    # where it takes text), then the words in a box. Returns { Id, Choice, Text } or $null (cancelled).
    param([string]$Root)
    $qs = @(Get-OpenGolemQuestions -Root $Root | Where-Object { -not $_.Remote })
    if (-not $qs.Count) { Write-ConsoleLine 'golem.answer: no Golem on this PC has an open question (a remote Golem''s are answered on the web console).' 'amber'; return $null }
    $rows = @(foreach ($q in $qs) {
        $i = 0
        foreach ($c in @($q.Choices)) { [pscustomobject]@{ Tag = [pscustomobject]@{ Id = $q.Id; Choice = $i; FreeText = $q.FreeText; Q = $q }; Cells = @(($q.Golem + ' ' + $q.Prompt), [string]$c.label, [string]$c.does, (($q.Question -replace '\s+', ' ').Trim())) }; $i++ }
        if ($q.FreeText) { [pscustomobject]@{ Tag = [pscustomobject]@{ Id = $q.Id; Choice = -1; FreeText = $true; Q = $q }; Cells = @(($q.Golem + ' ' + $q.Prompt), '(my own words only)', '', (($q.Question -replace '\s+', ' ').Trim())) } }
    })
    $pick = Show-ListPicker -Title 'Answer a Golem''s question' -Caption 'Pick the answer. Where the question takes your own words, a box for them comes next (with a choice they are optional).' -Columns @(@('Golem', 130), @('Answer', 200), @('The Golem then', 260), @('Question', 900)) -Rows $rows
    if (-not $pick) { return $null }
    $text = ''
    if ($pick.FreeText) {
        Add-Type -AssemblyName Microsoft.VisualBasic
        $text = [Microsoft.VisualBasic.Interaction]::InputBox(('The ' + $pick.Q.Golem + ' Golem asks, on ' + $pick.Q.Prompt + ":`r`n" + (Clip-ConsoleText $pick.Q.Question 600) + "`r`n`r`nYour own words" + $(if ($pick.Choice -ge 0) { ' (optional)' } else { '' }) + ':'), 'Answer a Golem''s question', '')
        if ($pick.Choice -lt 0 -and -not $text.Trim()) { return $null }
    }
    [pscustomobject]@{ Id = $pick.Id; Choice = $pick.Choice; Text = $text }
}

function Clip-ConsoleText([string]$s, [int]$n) { if ($s.Length -le $n) { $s } else { $s.Substring(0, $n) + ' ...' } }

function Get-AwayLines {
    # cc-P149: away mode (until when), each parked prompt (why, where its work is) and the last email commands, accepted
    # or rejected. Pure: status in, rows out; nothing when away mode was never used and no command ever came.
    param($Status)
    $rows = New-Object 'System.Collections.Generic.List[object]'
    if (-not $Status) { return $rows.ToArray() }
    $a = $Status.Away
    if ($a -and $a.on) { $u = ''; try { $u = ([datetime]::Parse([string]$a.until)).ToString('ddd yyyy-MM-dd HH:mm') } catch { $u = [string]$a.until }; $rows.Add([pscustomobject]@{ Text = ('away      : ON until ' + $u + ' (around the clock within the night usage rule; a digest each morning)'); Color = 'amber' }) }
    foreach ($p in @($(if ($a) { @($a.parked) } else { @() }) | Where-Object { $_ })) {
        $rows.Add([pscustomobject]@{ Text = ('parked    : ' + [string]$p.prompt + ': ' + [string]$p.why + $(if ($p.branch) { ' (its work: ' + [string]$p.branch + ')' } else { '' }) + '; unpark it to queue it again'); Color = 'amber' })
    }
    $m = $Status.MailCommands
    if ($m -and $m.on -and -not $m.setUp) { $rows.Add([pscustomobject]@{ Text = 'email     : commands on, but the IMAP app password is not saved (Mail-Commands.ps1 -SaveCredential)'; Color = 'amber' }) }
    foreach ($e in @($(if ($m) { @($m.last) } else { @() }) | Where-Object { $_ } | Select-Object -Last 5)) {
        $at = ''; try { $at = ([datetime]::Parse([string]$e.at)).ToString('ddd HH:mm') } catch { }
        $rows.Add([pscustomobject]@{ Text = ('email     : ' + $at + ' ' + [string]$e.result + ': ' + $(if ($e.line) { "'" + [string]$e.line + "'" } else { '(no line)' }) + $(if ($e.why) { ' (' + [string]$e.why + ')' } else { '' })); Color = $(if ([string]$e.result -eq 'rejected') { 'amber' } else { 'gray' }) })
    }
    $rows.ToArray()
}

function Get-GolemCardLines {
    # cc-P184: one line per Golem in the registry (status.json's golems, written by whichever Golem's hook ran last):
    # name, lane, machine, state, the running prompt and its branch, the locks it holds, where its usage figure comes
    # from. Pure: status in, rows ({ Text, Color }) out. Nothing while multiGolem is off; the registry error when there
    # is one.
    param($Status, [datetime]$Now = [datetime]::Now)
    $rows = New-Object 'System.Collections.Generic.List[object]'
    if (-not $Status) { return $rows.ToArray() }
    if ($Status.RegistryError) { $rows.Add([pscustomobject]@{ Text = ('golems    : REGISTRY INVALID: ' + $Status.RegistryError); Color = 'amber' }); return $rows.ToArray() }
    if (-not $Status.MultiGolem) { return $rows.ToArray() }
    $cards = @($Status.Golems)
    $int = @($cards | Where-Object { $_.integrator } | ForEach-Object { [string]$_.name })
    $rows.Add([pscustomobject]@{ Text = ('golems    : ' + $cards.Count + ' in the registry (multiGolem on); the integrator is ' + $(if ($int.Count) { $int[0] } else { 'nobody' }) + ', this file is ' + $(if ($Status.GolemName) { $Status.GolemName + '''s' } else { 'unsigned' })); Color = 'head' })
    foreach ($c in $cards) {
        $state = [string]$c.state
        if ($c.script) {
            # cc-P208: Shadow Iron, the merge Golem: no lane, usage or Wake; its last round, what waits, the last merge and
            # failure, a dirty tree and a pause. Pause merging / Resume merging and Merge branches now are its buttons.
            $m = $c.merge
            $t = '            ' + ('{0,-8}' -f [string]$c.name) + ('{0,-13}' -f 'merges') + ('{0,-14} ' -f [string]$c.machine) + $state
            if ($m) {
                $lr = ''; try { $lr = ([datetime]::Parse([string]$m.lastRound)).ToString('HH:mm') } catch { }
                $t += '; last round ' + $lr + $(if ($m.result) { ' (' + [string]$m.result + ')' } else { '' }) + '; ' + [int]$m.waiting + ' waiting'
                if ($m.lastMerge) { $t += '; last merge ' + [string]$m.lastMerge.prompt + ' from ' + [string]$m.lastMerge.golem }
                if ($m.lastFailure) { $t += '; last failure ' + [string]$m.lastFailure.prompt + ': ' + [string]$m.lastFailure.why }
                if ($m.paused) { $t += '; PAUSED (Resume merging)' }
                if ($m.dirty) { $t += '; NOT CLEAN, waits: ' + [string]$m.dirty }
            } else { $t += '; no round yet (the scheduled task Shadow Iron runs every 10 minutes)' }
            if ($c.needsYou) { $t += '; needs you: ' + [string]$c.needsYou }
            $rows.Add([pscustomobject]@{ Text = $t; Color = $(if ($state -eq 'NEEDS YOU' -or ($m -and ($m.dirty -or $m.paused))) { 'amber' } elseif ($state -eq 'DISABLED') { 'gray' } else { 'green' }) })
            continue
        }
        $t = '            ' + ('{0,-8}' -f [string]$c.name) + ('{0,-6}' -f [string]$c.lane) + ('{0,-7}' -f [string]$c.engine) + ('{0,-14} ' -f [string]$c.machine) + $state
        if ($c.current) { $t += ' ' + [string]$c.current + $(if ($c.branch -and [string]$c.branch -ne 'main') { ' on ' + [string]$c.branch } else { '' }) }
        if ($c.restReason) { $t += ' (' + [string]$c.restReason + ')' }
        if ($c.laneLine) { $t += '; ' + [string]$c.laneLine }   # cc-P159
        if ($c.needsYou) { $t += '; needs you: ' + [string]$c.needsYou }
        $locks = @($c.locks | Where-Object { $_ })
        if ($locks.Count) { $t += '; holds ' + ($locks -join ', ') }
        if ($c.usageSource) { $t += '; usage from the ' + [string]$c.usageSource }
        # cc-P200: while marathon is on, an extra PC's hours are widened to the whole day (its back-off still holds).
        if ($c.hours) { $t += '; hours ' + [string]$c.hours + $(if ($c.marathonDay) { ' (all day now: marathon)' } elseif ($c.noDaytimeMarathon -and $Status.Marathon -and $Status.Marathon.on) { ' (kept: noDaytimeMarathon)' } else { '' }) }
        if ($c.integrator) { $t += '; integrator' }
        if ($null -ne $c.ageMin -and [int]$c.ageMin -gt 15 -and $state -eq 'RUNNING') { $t += '; status ' + [int]$c.ageMin + ' min old' }
        # cc-P186: a Golem on another PC says whether its agent is online (it polls the web console); every Golem says
        # its last readiness check (Test-GolemReady.ps1). The tab is text: a portrait shows only on the web console.
        $offline = $false
        if ($c.remote) {
            $seen = $null; if ($c.lastSeen) { try { $seen = [datetime]::Parse([string]$c.lastSeen) } catch { } }
            $ago = $(if ($seen) { $m = [Math]::Max(0, [Math]::Round(($Now - $seen).TotalMinutes)); $(if ($m -lt 120) { [string]$m + ' min ago' } elseif ($m -lt 2880) { [string][Math]::Round($m / 60) + ' h ago' } else { $seen.ToString('yyyy-MM-dd HH:mm') }) } else { '' })
            if ($c.online) { $t += '; on another PC, online' + $(if ($ago) { ' (seen ' + $ago + ')' } else { '' }) }
            elseif ($seen) { $t += '; on another PC, offline (last seen ' + $ago + ')'; $offline = $true }
            else { $t += '; on another PC, its agent has not called in yet'; $offline = $true }
        }
        $notReady = $false
        if ($c.ready -and $c.ready.verdict) {
            $at = ''; try { $at = ' ' + ([datetime]::Parse([string]$c.ready.at)).ToString('HH:mm') } catch { }
            $notReady = ([string]$c.ready.verdict -ne 'READY')
            $failed = @(@($c.ready.failed) | Where-Object { $_ })
            $t += '; ready check ' + [string]$c.ready.verdict + $at + $(if ($notReady -and $c.ready.summary) { ': ' + [string]$c.ready.summary } else { '' }) + $(if ($notReady -and $failed.Count) { ' (' + ($failed -join ', ') + ')' } else { '' })
        }
        $color = switch ($state) { 'RUNNING' { 'green' } 'NEEDS YOU' { 'amber' } 'STOPPED' { 'amber' } 'UNKNOWN' { 'amber' } 'DISABLED' { 'gray' } default { 'gray' } }
        if ($state -eq 'RUNNING' -and $null -ne $c.ageMin -and [int]$c.ageMin -gt 15) { $color = 'amber' }
        if ($state -ne 'DISABLED' -and ($offline -or $notReady)) { $color = 'amber' }
        $rows.Add([pscustomobject]@{ Text = $t; Color = $color })
    }
    $rows.ToArray()
}

function ConvertFrom-AutopilotSettings {
    # settings.json as the hook reads it: each key it has, the hook's default for each it lacks. $Text $null is
    # no file. Source says which, for the tab. Never throws: an unreadable file is the defaults, as in the hook.
    param($Text)
    $s = [ordered]@{}
    foreach ($k in $script:AutopilotSettingDefaults.Keys) { $s[$k] = $script:AutopilotSettingDefaults[$k] }
    $source = 'no settings.json: the hook''s defaults'
    if ($null -ne $Text) {
        try {
            $j = ([string]$Text) | ConvertFrom-Json
            if ($j -isnot [pscustomobject]) { throw 'not an object' }
            foreach ($k in @($s.Keys)) { if ($j.PSObject.Properties.Name -contains $k -and $null -ne $j.$k) { $s[$k] = $j.$k } }
            $source = 'settings.json'
        } catch { $source = 'settings.json unreadable: the hook''s defaults' }
    }
    $mar = ([string]$s.marathon).Trim().ToLowerInvariant(); if ($mar -notin @('auto', 'on', 'off')) { $mar = 'auto' }
    $mm = ([string]$s.mailMode).Trim().ToLowerInvariant(); if ($mm -notin @('all', 'briefs', 'off')) { $mm = 'all' }
    [pscustomobject]@{ ProgressEmails = [bool]$s.progressEmails; MaxPerRun = [int]$s.maxPerRun; StallMinutes = [int]$s.stallMinutes; Source = $source; Marathon = $mar; MailMode = $mm
        AutoWake = [bool]$s.autoWake; NightShiftEnabled = [bool]$s.nightShiftEnabled; NightShiftStart = [string]$s.nightShiftStart; NightShiftEnd = [string]$s.nightShiftEnd
        DayWeekAheadPct = [double]$s.dayWeekAheadPct; WeekReservePct = [double]$s.weekReservePct; PlanUsageMaxAgeHours = [double]$s.planUsageMaxAgeHours }
}

function Format-ShortAge {
    # '14m', '1h 5m', '2d 3h', '40s': the tile's short form of Format-Age.
    param([TimeSpan]$Span)
    if ($Span.TotalSeconds -lt 0) { $Span = [TimeSpan]::Zero }
    if ($Span.TotalDays -ge 1)    { return ('{0}d {1}h' -f [int][Math]::Floor($Span.TotalDays), $Span.Hours) }
    if ($Span.TotalHours -ge 1)   { return ('{0}h {1}m' -f [int][Math]::Floor($Span.TotalHours), $Span.Minutes) }
    if ($Span.TotalMinutes -ge 1) { return ('{0}m' -f [int][Math]::Floor($Span.TotalMinutes)) }
    '{0}s' -f [int][Math]::Floor($Span.TotalSeconds)
}

function Get-AutopilotView {
    # Everything the tile and the tab say, from the four facts. $Enabled is $true, $false, or $null when
    # it could not be checked. $StateText is $null when there is no state.json. $QueueNames is $null when
    # the queue could not be listed. $Errors are the shell's read failures, each a short sentence.
    # cc-P94: $StatusText is status.json ($null: none), preferred when it reads as schema 1; otherwise Status is
    # $null and StatusNote says why, and the four facts are judged as before. $SettingsText is settings.json.
    param($Enabled, $StateText, [string[]]$LogLines, $QueueNames, [string[]]$Errors,
        $StatusText, [string]$StatusError, $SettingsText, [string]$Kind = 'autopilot', $Extra, [string]$RunNext = '')
    $status = $null
    $statusNote = 'no status.json (an autopilot from before 2026-10-13): read ENABLED, state.json and the log'
    if ($StatusError) { $statusNote = 'status.json could not be read (' + $StatusError + '): read ENABLED, state.json and the log' }
    elseif ($null -ne $StatusText) {
        try { $status = ConvertFrom-AutopilotStatus $StatusText; $statusNote = '' }
        catch { $statusNote = 'status.json unreadable (' + $_.Exception.Message + '): read ENABLED, state.json and the log' }
    }
    $problems = New-Object 'System.Collections.Generic.List[string]'
    foreach ($e in @($Errors)) { if ($e) { $problems.Add($e) } }
    $state = $null
    try { $state = ConvertFrom-AutopilotState $StateText }
    catch { $problems.Add('state.json unreadable: ' + $_.Exception.Message) }
    $queue = $null
    if ($null -ne $QueueNames) { $queue = Get-AutopilotQueue @($QueueNames) -RunNext $RunNext }
    $actions = @(ConvertFrom-AutopilotLog $LogLines)
    # The hand-off is the line the hook writes when it gives CC a prompt: "handed P74 (file) why".
    $since = $actions
    for ($i = $actions.Count - 1; $i -ge 0; $i--) {
        if ($actions[$i].Text -match '^handed P\d+') { $since = @($actions | Select-Object -Skip ($i + 1)); break }
    }
    $alert = $null
    $mail = @($since | Where-Object { $_.Text -like 'emailed: *' })
    if ($mail.Count) {
        $subject = $mail[-1].Text.Substring(9).Trim()
        foreach ($pre in $script:AutopilotSubjects) { if ($subject.StartsWith($pre)) { $subject = $subject.Substring($pre.Length).Trim(); break } }
        if (Test-AutopilotAlert $subject) { $alert = $subject }
    }
    $failing = @($since | Where-Object { $_.Text -like 'EMAIL FAILED*' })
    [pscustomobject]@{
        Status       = $status
        StatusNote   = $statusNote
        Settings     = (ConvertFrom-AutopilotSettings $SettingsText)
        Enabled      = $Enabled
        State        = $state
        Queue        = $queue
        Actions      = $actions
        Alert        = $alert
        EmailFailing = ($failing.Count -gt 0)
        LastFailure  = $(if ($failing.Count) { $failing[-1].Text } else { $null })
        Problems     = $problems.ToArray()
        # cc-P99: whose folder this was read from, golem or autopilot.
        Kind         = $Kind
        # cc-P125 ($Extra, from Get-AutopilotFacts): what each prompt is, the finished ones, the plan usage, the trees.
        CurrentPrompt = $(if ($status -and $status.CurrentFile) { Get-PromptInfo $status.CurrentFile (Get-ExtraText $Extra 'PromptTexts' $status.CurrentFile) } elseif ($state -and $state.CurrentFile) { Get-PromptInfo $state.CurrentFile (Get-ExtraText $Extra 'PromptTexts' $state.CurrentFile) } else { $null })
        QueueInfo     = @(if ($queue) { foreach ($r in @($queue.Runs)) { Get-PromptInfo $r (Get-ExtraText $Extra 'PromptTexts' $r) } })
        RunNext       = $(if ($queue -and $RunNext -and @($queue.Runs) -contains $RunNext) { $RunNext } else { '' })
        Finished      = @(foreach ($fp in @(Get-FinishedPrompts $actions 10)) {
                            $ct = Get-ExtraText $Extra 'CompletedTexts' ([string]$fp.Number)
                            $info = Get-PromptInfo ('cc-P' + $fp.Number + '-') $ct
                            [pscustomobject]@{ Number = $fp.Number; Result = $fp.Result; Time = $fp.Time; Title = $(if ($info.Readable) { $info.Title } else { 'P' + $fp.Number }); Headline = (Get-NotesHeadline (Get-ExtraText $Extra 'NotesTexts' ([string]$fp.Number))) }
                        })
        UsageText     = $(if ($Extra) { $Extra.UsageText } else { $null })
        PlanUsageText = $(if ($Extra) { $Extra.PlanUsageText } else { $null })
        CodexUsageText = $(if ($Extra -and $Extra.PSObject.Properties.Name -contains 'CodexUsageText') { $Extra.CodexUsageText } else { $null })
        Estimate      = $(if ($Extra) { $Extra.Estimate } else { $null })
        TreeClean     = $(if ($Extra) { $Extra.TreeClean } else { $null })
    }
}

function Get-ExtraText {
    # cc-P125: one text from Get-AutopilotFacts' Extra (a hashtable by file name or prompt number), or $null.
    param($Extra, [string]$Table, [string]$Key)
    if (-not $Extra -or -not $Extra.$Table) { return $null }
    $t = $Extra.$Table
    if ($t.Contains($Key)) { return $t[$Key] }
    $null
}

# --- cc-P125: long runs, winding down, usage bars and what each prompt is -------------------------------------------------
# The Golem (golem.ps1) writes status.json's winding, restReason, restAfter, waitingForModel, stopTold and longRun, and
# usage.json from Claude Code's documented status line JSON (rate_limits.five_hour / seven_day: used_percentage 0-100 and
# resets_at, Unix seconds; https://code.claude.com/docs/en/statusline.md). Everything below is pure: text in, lines out.

$script:UsageStaleMinutes = 10

function Get-PromptInfo {
    # A prompt file's number, title ("# CC task Pnnn: <title>"), Model and Kind lines and its first paragraph (who asked
    # for it and why). A missing or odd file gives its file name as the title, never an error.
    param([string]$Name, $Text)
    $num = 0
    $m = [regex]::Match([string]$Name, '^cc-P(\d+)-')
    if ($m.Success) { $num = [int]$m.Groups[1].Value }
    $o = [ordered]@{ Name = $Name; Number = $num; Title = $Name; Model = ''; Kind = ''; Para = ''; Cont = ([string]$Name -match '^cc-P\d+-cont-'); Readable = $false; Summary = '' }
    if ($null -eq $Text) { return [pscustomobject]$o }
    $lines = @(([string]$Text) -split "`r?`n")
    $h = @($lines | Where-Object { $_ -match '^\s*#\s+' } | Select-Object -First 1)
    if ($h.Count) {
        $t = ($h[0] -replace '^\s*#\s+', '').Trim()
        $t = ($t -replace '^CC task\s+P\d+\s*:\s*', '').Trim()
        if ($t) { $o.Title = $t; $o.Readable = $true }
    }
    foreach ($l in $lines) {
        if (-not $o.Model -and $l -match '^\s*\*\*Model:\*\*\s*(\w+)') { $o.Model = $Matches[1] }
        if (-not $o.Kind -and $l -match '^\s*\*\*Kind:\*\*\s*(.+)$') { $o.Kind = ($Matches[1] -replace '\*\*', '').Trim() }
        # cc-P155: the one plain line the overseer writes under the Model line (cc-P153's queue notes).
        if (-not $o.Summary -and $l -match '^\s*\*\*Summary:\*\*\s*(.+)$') { $o.Summary = ($Matches[1] -replace '\*\*', '').Trim() }
    }
    # The first paragraph after the title that is prose: not the Model line, not a heading.
    $buf = New-Object 'System.Collections.Generic.List[string]'
    $started = $false
    foreach ($l in @($lines | Select-Object -Skip 1)) {
        $x = $l.Trim()
        if (-not $x) { if ($started) { break } else { continue } }
        if ($x -match '^#' -or $x -match '^\*\*(Model|Kind|Summary):\*\*') { if ($started) { break } else { continue } }
        $started = $true
        $buf.Add($x)
    }
    $o.Para = ($buf -join ' ')
    [pscustomobject]$o
}

function Get-FirstSentence {
    # cc-P153: the first sentence of a paragraph (to its first ". ", "? " or "! "), for a prompt with no Summary line.
    param([string]$Text)
    $t = ([string]$Text -replace '\*\*|`', '').Trim()
    $m = [regex]::Match($t, '^(.+?[.!?])(?:\s|$)')
    if ($m.Success) { $m.Groups[1].Value } else { $t }
}

function Get-QueueRows {
    # cc-P153: the Golem tab's queue, in run order: the running prompt on top (marked running), then the queue with the
    # one that goes next marked next (Run next's pick when it is queued, else the first). Each row's note is the
    # prompt's Summary line, or (Grey) the first sentence of its opening paragraph. Pure; status.json carries it too.
    param([object[]]$QueueInfo, $Current, [string]$RunNext)
    $row = { param($p, [string]$mark)
        $sum = [string]$p.Summary
        $grey = -not $sum
        $note = $(if ($sum) { $sum } elseif ($p.Para) { Get-FirstSentence $p.Para } else { [string]$p.Title })
        [pscustomobject]@{ Number = [int]$p.Number; Label = ('P' + $p.Number); Mark = $mark; Note = $note; Grey = $grey; Title = [string]$p.Title
            Model = $(if ($p.Model) { [string]$p.Model } else { 'Opus' }); Size = (Get-PromptSize $p.Kind); Name = [string]$p.Name; Para = [string]$p.Para; Cont = [bool]$p.Cont }
    }
    $out = New-Object 'System.Collections.Generic.List[object]'
    if ($Current) { $out.Add((& $row $Current 'running')) }
    $q = @(@($QueueInfo) | Where-Object { $_ })
    $pick = @($q | Where-Object { $RunNext -and [string]$_.Name -eq $RunNext })
    if ($pick.Count) { $q = @($pick[0]) + @($q | Where-Object { [string]$_.Name -ne $RunNext }) }
    $first = $true
    foreach ($p in $q) { $out.Add((& $row $p $(if ($first) { 'next' } else { '' }))); $first = $false }
    $out.ToArray()
}

function Get-NotesHeadline {
    # The first bullet under "## Headlines" in a notes file, without its markdown, or ''.
    param($Text)
    if ($null -eq $Text) { return '' }
    $in = $false
    foreach ($l in @(([string]$Text) -split "`r?`n")) {
        if ($l -match '^##\s+Headlines') { $in = $true; continue }
        if ($in -and $l -match '^##\s') { break }
        if ($in -and $l -match '^\s*-\s+(.+)$') { return (($Matches[1] -replace '\*\*|`', '').Trim()) }
    }
    ''
}

function Get-FinishedPrompts {
    # The last $Count prompts the Golem finished, newest first, from its log's action lines: "P124 DONE, committing: ...",
    # "P123 STOPPED, no commit", and (cc-P125) PAUSED, which the log writes as "resting (stopping point) after P<nn>".
    param($Actions, [int]$Count = 10)
    $out = New-Object 'System.Collections.Generic.List[object]'
    $seen = @{}
    foreach ($a in @(@($Actions) | Sort-Object Time -Descending)) {
        $n = 0; $how = ''
        if ($a.Text -match '^P(\d+) DONE, committing') { $n = [int]$Matches[1]; $how = 'DONE' }
        elseif ($a.Text -match '^P(\d+) STOPPED') { $n = [int]$Matches[1]; $how = 'STOPPED' }
        elseif ($a.Text -match '^resting \(stopping point\) after P(\d+)') { $n = [int]$Matches[1]; $how = 'PAUSED' }
        if (-not $n -or $seen.ContainsKey($n)) { continue }
        $seen[$n] = $true
        $out.Add([pscustomobject]@{ Number = $n; Result = $how; Time = $a.Time })
        if ($out.Count -ge $Count) { break }
    }
    $out.ToArray()
}

function Format-ResetIn {
    # "resets in 2 h 14 m, at 11:59" (and the day when it is not today).
    param([datetime]$At, [datetime]$Now)
    $span = $At - $Now
    if ($span.TotalSeconds -lt 0) { return ('reset at ' + $At.ToString('HH:mm', $script:Invariant)) }
    $d = [int][Math]::Floor($span.TotalDays); $hh = $span.Hours; $mm = $span.Minutes
    $in = $(if ($d -gt 0) { [string]$d + ' d ' + $hh + ' h' } elseif ($hh -gt 0) { [string]$hh + ' h ' + $mm + ' m' } else { [string]$mm + ' m' })
    $when = $At.ToString('HH:mm', $script:Invariant)
    if ($At.Date -ne $Now.Date) { $when = $At.ToString('ddd HH:mm', $script:Invariant) }
    'resets in ' + $in + ', at ' + $when
}

function Format-UsageBar {
    # cc-P160: $Marker (a percent, or $null) draws the pace marker '|' where the pace allows the window to be now.
    param([double]$Pct, [int]$Width = 20, $Marker = $null)
    $p = [Math]::Max(0, [Math]::Min(100, $Pct))
    $f = [int][Math]::Round($p / 100 * $Width, [MidpointRounding]::AwayFromZero)
    $c = (('#' * $f) + ('.' * ($Width - $f))).ToCharArray()
    if ($null -ne $Marker) {
        $i = [int][Math]::Round([Math]::Max([double]0, [Math]::Min([double]100, [double]$Marker)) / 100 * $Width, [MidpointRounding]::AwayFromZero) - 1
        $c[[Math]::Max(0, [Math]::Min($Width - 1, $i))] = '|'
    }
    '[' + (-join $c) + ']'
}

function Get-UsagePace {
    # cc-P160: where the Golem's pace rule (golem.ps1 Get-PaceAllowance) allows a window to be now: the share of the
    # window gone since its last reset, plus the head start and capped by the reserve for the weekly ones. $null when the
    # reset time is unknown.
    param($ResetsAt, [datetime]$Now, [double]$Hours, [double]$Ahead = 0, [double]$Reserve = 0)
    if (-not ($ResetsAt -is [datetime])) { return $null }
    $since = $Hours - ($ResetsAt - $Now).TotalHours
    if ($since -lt 0) { $since = 0 }; if ($since -gt $Hours) { $since = $Hours }
    $a = ($since / $Hours) * 100 + $Ahead
    if ($Reserve -gt 0 -and $a -gt 100 - $Reserve) { $a = 100 - $Reserve }
    [Math]::Round([Math]::Max([double]0, [double]$a), 1)
}

function ConvertFrom-PlanUsage {
    # cc-P160: plan-usage.json (claude.ai/settings/usage, written by the overseer's reader) as plain values: each window's
    # pct and local reset time, the read time, Stale past $MaxAgeHours, the pace warning, the free full reset, the cloud
    # session credit and the usage credits. $null for no file; Error for an unreadable one. Never throws.
    param($Text, [datetime]$Now, [double]$MaxAgeHours = 4)
    if ($null -eq $Text) { return $null }
    try { $j = ([string]$Text) | ConvertFrom-Json } catch { return [pscustomobject]@{ Error = $_.Exception.Message } }
    $local = { param($v) if (-not $v) { return $null }; if ($v -is [datetime]) { return $v.ToLocalTime() }; try { [DateTimeOffset]::Parse([string]$v, $script:Invariant).LocalDateTime } catch { $null } }
    $win = { param($x) if (-not $x) { return $null }; $at = & $local $x.resets; $pct = [double]$x.pct; if ($at -and $at -le $Now) { $pct = 0 }; [pscustomobject]@{ Pct = $pct; At = $at } }
    $read = & $local $j.readAt
    $age = $(if ($read) { $Now - $read } else { $null })
    [pscustomobject]@{
        Error = ''; ReadAt = $read; Age = $age; Stale = ((-not $read) -or $age.TotalHours -gt $MaxAgeHours); MaxAgeHours = $MaxAgeHours; ReadBy = [string]$j.readBy; Plan = [string]$j.plan
        Session = (& $win $j.session); Week = (& $win $j.week); Fable = (& $win $j.fableWeek)
        Warning = ([string]$j.paceWarning).Trim()
        FullReset = $(if ($j.limitResets -and $j.limitResets.full) { [pscustomobject]@{ Available = [bool]$j.limitResets.full.available; Expires = [string]$j.limitResets.full.expires } } else { $null })
        Cloud = $(if ($j.cloudSessionCredit) { [pscustomobject]@{ Left = $j.cloudSessionCredit.left; Of = $j.cloudSessionCredit.of; Expires = (& $local $j.cloudSessionCredit.expires) } } else { $null })
        Credits = $(if ($j.usageCredits) { [pscustomobject]@{ Balance = [double]$j.usageCredits.balance; On = [bool]$j.usageCredits.toggleOn } } else { $null })
    }
}

function Get-UsageBars {
    # The Golem tab's usage rows, in the Claude app's order: the current session (Claude Code's five_hour window), the
    # week (seven_day, all models), then a separate weekly limit for Fable, which Claude Code does not report (no field in
    # the status line JSON). $UsageText is usage.json ($null: none). Each row: Name, Pct ($null when unknown), Text, Color.
    # Data older than $script:UsageStaleMinutes is greyed out as unknown. $Estimate, when given and there is no usage.json,
    # is the local token count over the last 5 hours, labelled as an estimate, with no percentage.
    # cc-P160: $PlanText is plan-usage.json (claude.ai/settings/usage, from the overseer's reader): the only source of the
    # Fable week and the pace warning, and of the session and the week when it was read after the status line last
    # reported. $Settings (ConvertFrom-AutopilotSettings) turns on the pace markers: '|' in each bar where the Golem's pace
    # rule allows that window to be now (Marker), amber when a weekly window is past it. Then three plain lines: the pace
    # warning, when the reader last ran, and the free reset with the cloud and usage credits. Bar says which rows are bars.
    param($UsageText, [datetime]$Now, $Estimate, $PlanText, $Settings)
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $u = $null; $err = ''
    if ($null -ne $UsageText) { try { $u = ([string]$UsageText) | ConvertFrom-Json } catch { $err = $_.Exception.Message } }
    $updated = $null
    if ($u) { $updated = ConvertFrom-AutopilotTime $u.updatedUtc 'Utc' }
    $stale = (-not $updated) -or (($Now.ToUniversalTime() - $updated).TotalMinutes -gt $script:UsageStaleMinutes)
    $age = $(if ($updated) { Format-ShortAge ($Now.ToUniversalTime() - $updated) } else { '' })
    $maxH = $(if ($Settings -and $Settings.PlanUsageMaxAgeHours) { [double]$Settings.PlanUsageMaxAgeHours } else { 4 })
    $plan = ConvertFrom-PlanUsage $PlanText $Now $maxH
    $planOk = ($plan -and -not $plan.Error -and -not $plan.Stale)
    # The plan page stands in for the session and the week when it was read after the status line last reported.
    $planNewer = ($planOk -and $plan.ReadAt -and ((-not $updated) -or $stale -or $plan.ReadAt.ToUniversalTime() -gt $updated))
    $head = 'plan usage: from Claude Code''s status line (usage.json)'
    if ($u) { $head += $(if ($stale) { ', last reported ' + $age + ' ago: too old, shown as unknown' } else { ', reported ' + $age + ' ago' }) }
    elseif ($err) { $head = 'plan usage: usage.json unreadable (' + $err + ')' }
    else { $head = 'plan usage: not reported yet (Claude Code''s status line writes usage.json once a session has had an answer)' }
    if ($planNewer) { $head += '; the session and the week from the plan usage page (read later)' }
    $rows.Add([pscustomobject]@{ Name = 'head'; Pct = $null; Text = $head + $(if ($plan -and $plan.Plan) { '. Plan: ' + $plan.Plan } else { '. The plan name is not in Claude Code''s data.' }); Color = 'head'; Bar = $false; Marker = $null })
    $ahead = $(if ($Settings) { [double]$Settings.DayWeekAheadPct } else { 0 })
    $reserve = $(if ($Settings) { [double]$Settings.WeekReservePct } else { 0 })
    $bar = {
        param([string]$Name, [double]$Pct, $At, [string]$Window, [double]$Hours, [bool]$Weekly)
        $label = ('{0,-16}' -f $Name)
        $mk = $null
        if ($Settings) { $mk = $(if ($Weekly) { Get-UsagePace $At $Now $Hours $ahead $reserve } else { Get-UsagePace $At $Now $Hours }) }
        $c = $(if ($Pct -ge 90) { 'red' } elseif ($Pct -ge 70) { 'amber' } elseif ($Weekly -and $null -ne $mk -and $Pct -gt $mk) { 'amber' } else { 'normal' })
        $paceText = $(if ($null -ne $mk) { ' (pace ' + $mk.ToString('0.#', $script:Invariant) + '%' + $(if ($Weekly -and $Pct -gt $mk) { ', ahead' } else { '' }) + ')' } else { '' })
        $reset = $(if ($At -is [datetime]) { Format-ResetIn $At $Now } else { 'reset time unknown' })
        [pscustomobject]@{ Name = $Name; Pct = $Pct; Text = ($label + (Format-UsageBar $Pct -Marker $mk) + (' {0,3}% used{1}  ' -f [int][Math]::Round($Pct, [MidpointRounding]::AwayFromZero), $paceText) + $reset + '  (' + $Window + ')'); Color = $c; Bar = $true; Marker = $mk }
    }
    foreach ($w in @(@('fiveHour', 'Current session', '5-hour window', 5, $false, 'Session'), @('sevenDay', 'This week', '7-day window, all models', 168, $true, 'Week'))) {
        $x = $null
        if ($u) { $x = $u.($w[0]) }
        $label = ('{0,-16}' -f $w[1])
        if ($planNewer -and $plan.($w[5])) {
            $px = $plan.($w[5])
            $rows.Add((& $bar $w[1] $px.Pct $px.At ($w[2] + ', plan usage page') $w[3] $w[4]))
        } elseif ($x -and -not $stale) {
            $at = [DateTimeOffset]::FromUnixTimeSeconds([long]$x.resetsAt).LocalDateTime
            $rows.Add((& $bar $w[1] ([double]$x.pct) $at $w[2] $w[3] $w[4]))
        } else {
            $rows.Add([pscustomobject]@{ Name = $w[1]; Pct = $null; Text = ($label + '[' + ('-' * 20) + '] unknown  (' + $w[2] + ')'); Color = 'gray'; Bar = $true; Marker = $null })
        }
    }
    $fl = ('{0,-16}' -f 'Fable this week')
    # cc-P274: usage.json's own Fable window when the plan page has none: a model-specific window from the status line
    # (while usage.json is fresh), or the mark a Fable session's limit hit left (until its reset, however old the file).
    $uf = $(if ($u -and $u.PSObject.Properties.Name -contains 'fable') { $u.fable } else { $null })
    $ufAt = $(if ($uf -and [long]$uf.resetsAt -gt 0) { [DateTimeOffset]::FromUnixTimeSeconds([long]$uf.resetsAt).LocalDateTime } else { $null })
    $ufHit = ($uf -and [string]$uf.source -eq 'limit hit')
    $ufOk = ($ufAt -and $ufAt -gt $Now -and ($ufHit -or -not $stale))
    if ($planOk -and $plan.Fable) { $rows.Add((& $bar 'Fable this week' $plan.Fable.Pct $plan.Fable.At 'separate weekly limit for Fable, plan usage page' 168 $true)) }
    elseif ($ufOk) {
        $fwhy = $(if ($ufHit) { 'separate weekly limit for Fable: a Fable session hit it' + $(if ($uf.hitAt) { ' ' + ([datetime]::Parse([string]$uf.hitAt)).ToString('ddd HH:mm', $script:Invariant) } else { '' }) } else { 'separate weekly limit for Fable, Claude Code''s status line' })
        $rows.Add((& $bar 'Fable this week' ([double]$uf.pct) $ufAt $fwhy 168 $true))
    }
    elseif ($planOk) { $rows.Add([pscustomobject]@{ Name = 'Fable this week'; Pct = $null; Text = ($fl + '[' + ('-' * 20) + '] unknown: claude.ai/settings/usage shows no separate Fable line, and no Fable session has hit its limit this week; the Golem does not hold Fable prompts for it'); Color = 'gray'; Bar = $true; Marker = $null }) }
    elseif ($plan -and $plan.Error) { $rows.Add([pscustomobject]@{ Name = 'Fable this week'; Pct = $null; Text = ($fl + '[' + ('-' * 20) + '] unknown: plan-usage.json unreadable (' + $plan.Error + ')'); Color = 'gray'; Bar = $true; Marker = $null }) }
    elseif ($plan -and $plan.Stale) { $rows.Add([pscustomobject]@{ Name = 'Fable this week'; Pct = $null; Text = ($fl + '[' + ('-' * 20) + '] unknown: the plan usage page was read ' + $(if ($plan.Age) { (Format-ShortAge $plan.Age) + ' ago' } else { 'at an unknown time' }) + ' (over planUsageMaxAgeHours ' + $plan.MaxAgeHours + '); the Golem does not hold Fable prompts for it'); Color = 'gray'; Bar = $true; Marker = $null }) }
    else { $rows.Add([pscustomobject]@{ Name = 'Fable this week'; Pct = $null; Text = ($fl + '[' + ('-' * 20) + '] not reported: Claude Code''s status line has no per-model weekly limit'); Color = 'gray'; Bar = $true; Marker = $null }) }
    if ($plan -and -not $plan.Error) {
        if ($planOk -and $plan.Warning) { $rows.Add([pscustomobject]@{ Name = 'pace warning'; Pct = $null; Text = ('pace warning from claude.ai: ' + $plan.Warning + ' The Golem counts this as ahead of pace and holds new hand-offs until a read clears it.'); Color = 'amber'; Bar = $false; Marker = $null }) }
        $rd = $(if ($plan.ReadAt) { 'plan usage page last read ' + $plan.ReadAt.ToString('ddd HH:mm', $script:Invariant) + ' (' + (Format-ShortAge $plan.Age) + ' ago' + $(if ($plan.ReadBy) { ', ' + $plan.ReadBy } else { '' }) + ')' } else { 'plan usage page: read time unknown' })
        if ($plan.Stale) { $rd += ': too old (planUsageMaxAgeHours ' + $plan.MaxAgeHours + '), the Fable week is unknown' }
        $rows.Add([pscustomobject]@{ Name = 'plan read'; Pct = $null; Text = $rd; Color = $(if ($plan.Stale) { 'gray' } else { 'normal' }); Bar = $false; Marker = $null })
        $bits = @()
        if ($plan.FullReset) { $bits += $(if ($plan.FullReset.Available) { 'free full reset: available until ' + $plan.FullReset.Expires + ' (yours to use; the Golem never uses it and asks you once a day when the week is short)' } else { 'free full reset: none' }) }
        if ($plan.Cloud) { $bits += ('cloud session credit: ' + $plan.Cloud.Left + ' of ' + $plan.Cloud.Of + ' left' + $(if ($plan.Cloud.Expires) { ', until ' + $plan.Cloud.Expires.ToString('MMM d', $script:Invariant) } else { '' })) }
        if ($plan.Credits) { $bits += ('usage credits: ' + $plan.Credits.Balance.ToString('0.##', $script:Invariant) + ', ' + $(if ($plan.Credits.On) { 'on' } else { 'off' }) + ' (the Golem stops at the plan limit and never spends them)') }
        if ($bits.Count) { $rows.Add([pscustomobject]@{ Name = 'plan extras'; Pct = $null; Text = ($bits -join ' | '); Color = 'gray'; Bar = $false; Marker = $null }) }
    }
    if (-not $u -and $Estimate) {
        $rows.Add([pscustomobject]@{ Name = 'estimate'; Pct = $null; Text = ('estimate, not Anthropic''s figure: ' + ('{0:N0}' -f [long]$Estimate.Tokens) + ' tokens in ' + $Estimate.Sessions + ' session log(s) over the last 5 hours (no percentage: the limit is not readable)'); Color = 'gray'; Bar = $false; Marker = $null })
    }
    $rows.ToArray()
}

function Get-CodexUsageRows {
    # cc-P152: the ChatGPT plan's Codex limits from codex-usage.json (D:\UO\golem\Codex.ps1 writes it after every run,
    # from Codex's own session logs), labelled "ChatGPT plan (Codex)" beside the Claude bars: the 5-hour window and the
    # week, each with the Golem's share of it (the rise in used_percent across its runs), then today's runs and images.
    # Same row shape as Get-UsageBars. A window whose reset has passed shows 0; a read older than $MaxAgeHours is greyed
    # as unknown (Codex only reports when something runs, Chase's own desktop use included). Never throws.
    param($CodexText, [datetime]$Now, $Settings, [double]$MaxAgeHours = 12)
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $lab = 'ChatGPT plan (Codex)'
    if ($null -eq $CodexText) { $rows.Add([pscustomobject]@{ Name = 'codex head'; Pct = $null; Text = ($lab + ': no codex-usage.json yet (Codex.ps1 writes it after its first run)'); Color = 'gray'; Bar = $false; Marker = $null }); return $rows.ToArray() }
    try { $j = ([string]$CodexText) | ConvertFrom-Json } catch { $rows.Add([pscustomobject]@{ Name = 'codex head'; Pct = $null; Text = ($lab + ': codex-usage.json unreadable (' + $_.Exception.Message + ')'); Color = 'gray'; Bar = $false; Marker = $null }); return $rows.ToArray() }
    $l = $j.limits
    $at = $null
    if ($l -and $l.at) { try { $at = [DateTimeOffset]::Parse([string]$l.at, $script:Invariant).LocalDateTime } catch { } }
    $stale = (-not $at) -or (($Now - $at).TotalHours -gt $MaxAgeHours)
    $nowU = [DateTimeOffset]::new($Now).ToUnixTimeSeconds()
    $head = $lab + ': ' + $(if (-not $l) { 'Codex has reported no limits; only runs and tokens are counted' } elseif ($stale) { 'last reported ' + $(if ($at) { (Format-ShortAge ($Now - $at)) + ' ago' } else { 'at an unknown time' }) + ', too old: shown as unknown' } else { 'reported ' + (Format-ShortAge ($Now - $at)) + ' ago' + $(if ($l.planType) { ', plan ' + $l.planType } else { '' }) })
    $rows.Add([pscustomobject]@{ Name = 'codex head'; Pct = $null; Text = $head; Color = 'head'; Bar = $false; Marker = $null })
    $maxPct = $(if ($Settings -and $Settings.PSObject.Properties.Name -contains 'codexMaxPctPerWindow') { [double]$Settings.codexMaxPctPerWindow } else { 40 })
    $reserve = $(if ($Settings -and $Settings.PSObject.Properties.Name -contains 'codexWeekReservePct') { [double]$Settings.codexWeekReservePct } else { 30 })
    foreach ($w in @(@('primary', 'Codex 5-hour', '5-hour window'), @('secondary', 'Codex week', '7-day window'))) {
        $x = $(if ($l) { $l.($w[0]) } else { $null })
        $label = ('{0,-16}' -f $w[1])
        if (-not $x -or $stale) { $rows.Add([pscustomobject]@{ Name = $w[1]; Pct = $null; Text = ($label + '[' + ('-' * 20) + '] unknown  (' + $w[2] + ')'); Color = 'gray'; Bar = $true; Marker = $null }); continue }
        $reset = [long]$x.resetsAt
        $pct = $(if ($reset -gt 0 -and $reset -le $nowU) { 0.0 } else { [double]$x.usedPct })
        $share = 0.0
        foreach ($r in @($j.runs)) {
            $b = $r.($w[0] + 'Before'); $a = $r.($w[0] + 'After')
            if ($a -and $b -and [long]$a.resetsAt -eq $reset) { $d = [double]$a.usedPct - $(if ([long]$b.resetsAt -eq $reset) { [double]$b.usedPct } else { 0 }); if ($d -gt 0) { $share += $d } }
        }
        $limit = $(if ($w[0] -eq 'secondary') { 100 - $reserve } else { 100 })
        $c = $(if ($pct -ge 90) { 'red' } elseif ($pct -ge $limit -or $share -ge $maxPct -or $pct -ge 70) { 'amber' } else { 'normal' })
        $resetText = $(if ($reset -gt 0) { Format-ResetIn ([DateTimeOffset]::FromUnixTimeSeconds($reset).LocalDateTime) $Now } else { 'reset time unknown' })
        $rows.Add([pscustomobject]@{ Name = $w[1]; Pct = $pct; Text = ($label + (Format-UsageBar $pct) + (' {0,3}% used, the Golem {1}%  ' -f [int][Math]::Round($pct, [MidpointRounding]::AwayFromZero), [Math]::Round($share, 1)) + $resetText + '  (' + $w[2] + ')'); Color = $c; Bar = $true; Marker = $null })
    }
    $t = $j.today
    # cc-P158: today's runs by use: the review before each commit, the Codex lane, art.
    $by = ''
    if ($t -and $t.date) {
        $tr = @(@($j.runs) | Where-Object { $_ -and $_.at -and ([string]$_.at).StartsWith([string]$t.date) })
        $n = { param($u) @($tr | Where-Object { [string]$_.purpose -eq $u }).Count }
        if ($tr.Count -and @($tr | Where-Object { $_.purpose }).Count) { $by = ' (' + (& $n 'review') + ' review(s), ' + (& $n 'lane') + ' lane run(s), ' + (& $n 'art') + ' art run(s))' }
    }
    $rows.Add([pscustomobject]@{ Name = 'codex today'; Pct = $null; Text = ('Codex today: ' + $(if ($t) { [string]$t.runs + ' run(s), ' + $t.images + ' image(s), ' + ('{0:N0}' -f [long]$t.tokens) + ' tokens' + $by } else { 'nothing' }) + '. Budget: the Golem stops at ' + $maxPct + '% of a window, leaves ' + $reserve + '% of the week to you, never spends credits.'); Color = 'gray'; Bar = $false; Marker = $null })
    $rows.ToArray()
}

function Get-ArtRequestRows {
    # cc-P152: the Art tab's list from the request files' texts (D:\UO\art\requests\*.json): id, title, status, who
    # asked, the variants, and a thumbnail (the newest preview sheet, else the placed file). Generated first, then new,
    # approved, rejected, placed. A file that does not read is a row saying so. Pure: texts in, rows out.
    param([string[]]$Texts, [scriptblock]$Exists = { param($p) Test-Path -LiteralPath $p -PathType Leaf })
    $order = @{ generated = 0; new = 1; approved = 2; rejected = 3; placed = 4 }
    $rows = foreach ($t in @($Texts)) {
        try { $r = $t | ConvertFrom-Json } catch { [pscustomobject]@{ Id = '?'; Title = 'unreadable request file'; Status = 'unreadable'; AskedBy = ''; Variants = ''; Approved = ''; Thumb = ''; Order = 9 }; continue }
        $thumb = ''
        foreach ($v in @(@($r.variants) | Where-Object { $_ -and $_.name -eq $r.approved }) + @(@($r.variants) | Select-Object -Last 1)) {
            if (-not $v -or -not $v.file) { continue }
            $pv = [IO.Path]::ChangeExtension([string]$v.file, '.preview.png')
            if (& $Exists $pv) { $thumb = $pv; break }
            if (& $Exists ([string]$v.file)) { $thumb = [string]$v.file; break }
        }
        if (-not $thumb) { foreach ($pl in @($r.placed)) { if ($pl -and $pl.to -and (& $Exists ([string]$pl.to))) { $thumb = [string]$pl.to; break } } }
        [pscustomobject]@{ Id = [string]$r.id; Title = [string]$r.title; Status = [string]$r.status; AskedBy = [string]$r.askedBy
            Variants = ((@($r.variants) | Where-Object { $_ } | ForEach-Object { $_.name + ' ' + $_.backend + $(if ($_.verdict) { ' (' + $_.verdict + ')' } else { '' }) }) -join ', ')
            Approved = [string]$r.approved; Thumb = $thumb; Order = $(if ($order.ContainsKey([string]$r.status)) { $order[[string]$r.status] } else { 9 }) }
    }
    @($rows | Sort-Object Order, Id)
}

function Get-LongRunLine {
    # "long run: 2 of 5, until Mon 06:00" while one is on; '' otherwise.
    param($LongRun, [datetime]$Now)
    if (-not $LongRun) { return '' }
    $until = ConvertFrom-AutopilotTime $LongRun.until 'Local'
    if (-not $until -or $until -le $Now) { return '' }
    $done = 0; [void][int]::TryParse([string]$LongRun.done, [ref]$done)
    $cap = 0; [void][int]::TryParse([string]$LongRun.cap, [ref]$cap)
    $of = $(if ($cap -gt 0) { [string]$done + ' of ' + $cap } else { [string]$done + ', until the queue is empty' })
    'long run: ' + $of + ', until ' + $until.ToString('ddd HH:mm', $script:Invariant)
}

function Get-WindingLine {
    # cc-P125: the one line under the tile's and the tab's run lines while the Golem winds down or rests with ENABLED kept.
    # $TreeClean: $true when both repos are clean (read only at rest), $null when not read.
    param($Status, $TreeClean)
    if (-not $Status) { return '' }
    $cur = [string]$Status.Current
    $l = ''
    switch ([string]$Status.Winding) {
        'after-current'  { $l = 'Wrapping up after ' + $cur }
        'stopping-point' { $l = 'Finding a stopping point in ' + $cur + $(if ($Status.StopTold) { ' (CC has been told)' } else { ' (CC is told at its next tool use)' }) }
    }
    if (-not $l) {
        $after = [string]$Status.RestAfter
        $aft = $(if ($after) { ' after ' + $after } else { '' })
        switch ([string]$Status.RestReason) {
            'wrap up'        { $l = 'Resting after ' + $after + ' (wrap up)' }
            'stopping point' { $l = 'Paused at a stopping point in ' + $after }
            'context'        { $l = 'Resting' + $aft + ' (session full)' }
            'model'          { $l = 'Waiting for a ' + [string]$Status.WaitingForModel + ' session' + $(if ($Status.PSObject.Properties.Name -contains 'WaitingForPrompt' -and $Status.WaitingForPrompt) { ' for ' + $Status.WaitingForPrompt } else { '' }) }
            'usage limit'    { $l = 'Paused: usage limit' + $(if ($cur) { ' in ' + $cur } else { '' }) }
            # cc-P131: the reasons the Golem gained.
            'run cap'           { $l = 'Resting' + $aft + ' (run cap)' }
            'bot window'        { $l = 'Resting' + $aft + ' (overnight bot window)' }
            'night shift ended' { $l = 'Resting' + $aft + ' (night shift ended)' }
            'long run ended'    { $l = 'Resting' + $aft + ' (long run ended)' }
        }
        if ($l -and $Status.RestReason -ne 'usage limit' -and $TreeClean -eq $true) { $l += '. Tree clean, safe to package' }
    }
    $l
}

function Get-ReadyLine {
    # cc-P131: when a rest can run again, counted from $Now on every refresh: "ready in 2 h 14 m (4:00 PM)" from
    # status.json's readyAt, else its readyHow ("Ready now: new session, then the phrase", "Waking itself at 3:00 AM
    # (...)"). '' when nothing rests.
    param($Status, [datetime]$Now)
    if (-not $Status -or -not $Status.RestReason) { return '' }
    if ($Status.ReadyAt) {
        $at = $Status.ReadyAt
        if ($at -gt $Now) { return ('ready in ' + (Format-ShortAge ($at - $Now)) + ' (' + $at.ToString('h:mm tt', $script:Invariant) + ')') }
        return ('ready now (since ' + $at.ToString('h:mm tt', $script:Invariant) + ')' + $(if ($Status.RestReason -eq 'usage limit') { ': the phrase in the same or a new session' } else { '' }))
    }
    if ($Status.ReadyHow) { return $Status.ReadyHow }
    'ready now'
}

# cc-P162: "End of week: spending what is left until Wed 4:00 PM (week 80%, 17% left; Fable week 45%, 52% left)" inside
# the window (green); when it opens otherwise; or that it is off.
# cc-P200: the week's projection ("on track for 82% by Wed 4:00 PM ...") and, with a free reset on offer, when to press it.
function Get-ProjectionLines($p) {
    if (-not $p) { return @() }
    # cc-P274: the text names both weeks first ("week 71%, Fable 98%: on track for ..."), so the label is projection.
    $rows = @([pscustomobject]@{ Text = ('projection: ' + [string]$p.text); Color = $(if ($p.known -and $p.week -and [double]$p.week.projected -lt [double]$p.target) { 'amber' } elseif ($p.known) { 'green' } else { 'gray' }) })
    if ($p.PSObject.Properties.Name -contains 'fableWarning' -and $p.fableWarning) { $rows += [pscustomobject]@{ Text = ('            Fable: ' + [string]$p.fableWarning); Color = 'amber' } }
    if ($p.resetAdvice) { $rows += [pscustomobject]@{ Text = ('            free reset: ' + [string]$p.resetAdvice); Color = 'normal' } }
    $rows
}
# cc-P200: marathon mode: the setting, on or off now, since when and why, and the low-queue emails sent in this spell.
function Get-MarathonLine($m, [datetime]$Now) {
    if (-not $m) { return $null }
    $on = [bool]$m.on
    $t = [string]$m.mode + ', ' + $(if ($on) { 'ON' } else { 'off' })
    $since = ConvertFrom-AutopilotTime $m.since 'Local'
    if ($m.why) { $t += $(if ($since) { ' since ' + $since.ToString('ddd h:mm tt', [Globalization.CultureInfo]::InvariantCulture) } else { '' }) + ': ' + [string]$m.why }
    if ([string]$m.mode -eq 'auto') { $t += ' (auto: on under ' + [string]$m.target + '% with more than ' + [string]$m.minHours + ' h left)' }
    $low = @(@($m.queueLow) | Where-Object { $_ })
    if ($on -and $low.Count) { $t += '; queue low told for ' + ($low -join ', ') }
    [pscustomobject]@{ Text = ('marathon  : ' + $t); Color = $(if ($on) { 'green' } else { 'gray' }) }
}
function Get-EndOfWeekLine($e, [datetime]$Now) {
    if (-not $e) { return $null }
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $clock = { param($t) $d = ConvertFrom-AutopilotTime $t 'Local'; if ($d) { $d.ToString('ddd h:mm tt', $inv) } else { '' } }
    $cap = [double]$e.cap
    $left = { param($p) if ($null -eq $p) { 'unknown' } else { ('{0}%, {1}% left' -f [int][Math]::Round([double]$p, [MidpointRounding]::AwayFromZero), [int][Math]::Round([Math]::Max(0, $cap - [double]$p), [MidpointRounding]::AwayFromZero)) } }
    if (-not [bool]$e.on) { return [pscustomobject]@{ Text = 'off (endOfWeekSpend false): the week keeps its even pace to the end'; Color = 'gray' } }
    if ([bool]$e.active) {
        return [pscustomobject]@{ Text = ('End of week: spending what is left until {0} (week {1}; Fable week {2}{3})' -f (& $clock $e.until), (& $left $e.weekPct), (& $left $e.fablePct), $(if ([bool]$e.fableActive) { '' } else { ', on its own pace' })); Color = 'green' }
    }
    $o = & $clock $e.opensAt
    [pscustomobject]@{ Text = ('opens {0} ({1} h before the weekly reset): then it spends what is left, up to {2}%' -f $(if ($o) { $o } else { 'when the reset time is known' }), [double]$e.hours, $cap); Color = 'gray' }
}

# cc-P161: the Golem's own sessions: how many are open, which are kept open for Chase (amber), and the last one closed.
function Get-SessionsLine($s) {
    if (-not $s) { return $null }
    $open = @($s.open | Where-Object { $_ })
    $kept = @($s.kept | Where-Object { $_ })
    $last = @($s.closed | Where-Object { $_ }) | Select-Object -Last 1
    $t = [string]$open.Count + ' open' + $(if ($open.Count) { ' (' + ((@($open | ForEach-Object { [string]$_.name })) -join ', ') + ')' } else { '' })
    $t += $(if ([bool]$s.closeFinished) { '; finished ones close ' + [string]$s.closeAfterMinutes + ' min after their last turn' } else { '; closing is off (closeFinishedSessions false)' })
    if ($kept.Count) { $t += '; KEPT OPEN: ' + ((@($kept | ForEach-Object { [string]$_.name + ' (' + [string]$_.why + ')' })) -join ', ') }
    if ($last) { $t += '; last closed ' + [string]$last.name + ': ' + [string]$last.why }
    [pscustomobject]@{ Text = $t; Color = $(if ($kept.Count) { 'amber' } else { 'gray' }) }
}

function Get-NightShiftLine {
    # cc-P131: "Night shift: on, 22:30 to 06:30, next run tonight at 22:30", or off, or why it will not run (status.json's
    # nightShift, written by the Golem; settings.json when the status has none).
    param($Status, $Settings, [datetime]$Now)
    $ns = $(if ($Status) { $Status.NightShift } else { $null })
    $on = $(if ($ns -and $null -ne $ns.enabled) { [bool]$ns.enabled } elseif ($Settings) { [bool]$Settings.NightShiftEnabled } else { $true })
    $start = $(if ($ns -and $ns.start) { [string]$ns.start } elseif ($Settings -and $Settings.NightShiftStart) { [string]$Settings.NightShiftStart } else { '22:30' })
    $end = $(if ($ns -and $ns.end) { [string]$ns.end } elseif ($Settings -and $Settings.NightShiftEnd) { [string]$Settings.NightShiftEnd } else { '06:30' })
    if (-not $on) { return ('Night shift: off (' + $start + ' to ' + $end + ' when on)') }
    # cc-P156: inside the night's window, the Golem's own line: the batch and its budget, or the next check point and why.
    if ($ns -and $ns.PSObject.Properties.Name -contains 'window' -and [bool]$ns.window -and [string]$ns.line) {
        $until = ConvertFrom-AutopilotTime $ns.until 'Local'
        return ('Night shift: ' + [string]$ns.line + $(if ($until) { ', until ' + $until.ToString('ddd HH:mm', $script:Invariant) } else { '' }))
    }
    if ($ns -and [bool]$ns.running) {
        $until = ConvertFrom-AutopilotTime $ns.until 'Local'
        return ('Night shift: running now' + $(if ($until) { ', until ' + $until.ToString('ddd HH:mm', $script:Invariant) } else { '' }))
    }
    $when = ''
    $next = $(if ($ns) { ConvertFrom-AutopilotTime $ns.next 'Local' } else { $null })
    if ($next) {
        $day = $(if ($next.Date -eq $Now.Date) { 'tonight' } elseif ($next.Date -eq $Now.Date.AddDays(1)) { 'tomorrow' } else { $next.ToString('ddd', $script:Invariant) })
        $when = ', next run ' + $day + ' at ' + $next.ToString('HH:mm', $script:Invariant)
    }
    $l = 'Night shift: on, ' + $start + ' to ' + $end + $when
    $why = $(if ($ns) { [string]$ns.whyNot } else { '' })
    if ($why) { $l += '; will not run as things stand: ' + $why }
    $l
}

function Format-AutopilotQueueCount {
    param($Queue)
    if ($null -eq $Queue) { return 'queue unreadable' }
    $n = @($Queue.Runs).Count
    if ($n -eq 0) { return 'queue empty' }
    [string]$n + ' queued'
}

function Get-AutopilotGlance {
    # The AUTOPILOT tile. Pure: ENABLED, state.json's text, the log tail and the queue listing in, a
    # tile out. A fact that could not be read makes it grey UNKNOWN, never red (the DNS tile's rule).
    param($Enabled, $StateText, [string[]]$LogLines, $QueueNames, [string[]]$Errors,
        [datetime]$Now = [datetime]::Now, $View)
    if (-not $View) { $View = Get-AutopilotView -Enabled $Enabled -StateText $StateText -LogLines $LogLines -QueueNames $QueueNames -Errors $Errors }
    if ($View.Status) { return (Get-AutopilotStatusGlance -View $View -Now $Now) }
    $t = [ordered]@{ Title = 'GOLEM'; Word = 'UNKNOWN'; Color = 'gray'; Detail = '' }
    $q = $View.Queue
    if ($null -eq $View.Enabled) {
        $t.Detail = 'could not tell whether it is on'
        if (@($View.Problems).Count) { $t.Detail += ' (' + @($View.Problems)[0] + ')' }
        return [pscustomobject]$t
    }
    if (-not $View.Enabled) {
        $t.Word = 'OFF'
        $t.Detail = 'not handing out prompts; ' + (Format-AutopilotQueueCount $q)
        return [pscustomobject]$t
    }
    if (@($View.Problems).Count -or $null -eq $View.State -or $null -eq $q) {
        $t.Detail = 'on, but ' + (@($View.Problems) + @('the queue could not be listed'))[0]
        return [pscustomobject]$t
    }
    $s = $View.State
    $ignored = ''
    if (@($q.Ignored).Count) { $ignored = '; ' + @($q.Ignored).Count + ' unnumbered file(s) ignored' }
    if ($View.Alert) {
        $t.Word = 'NEEDS YOU'; $t.Color = 'amber'
        $a = $View.Alert
        if ($a.Length -gt 90) { $a = $a.Substring(0, 87) + '...' }
        $t.Detail = $a
    } elseif ($s.Current) {
        $t.Word = 'RUNNING'; $t.Color = 'green'
        $t.Detail = $s.Current
        if ($s.StartedAt) { $t.Detail += ' for ' + (Format-Age ($Now - $s.StartedAt)) }
        $t.Detail += ', ' + (Format-AutopilotQueueCount $q) + $ignored
    } else {
        $t.Word = 'IDLE'
        $t.Detail = Format-AutopilotQueueCount $q
        if (@($q.Runs).Count) { $t.Detail += '; send CC "go" to start' }
        $t.Detail += $ignored
    }
    if ($View.EmailFailing) {
        $t.Color = 'amber'
        $t.Detail += '; email failing'
    }
    [pscustomobject]$t
}

function Format-StepProgress {
    # cc-P153: "step 4 of 9: Wiring the Homepage widgets" and a bar ("[####......]"), or '' when no steps are reported.
    # The estimate ("about 40 min left", labelled an estimate by the Golem) rides along when known.
    param($Status, [int]$Width = 10)
    $o = [ordered]@{ Text = ''; Bar = ''; Pct = $null; Estimate = '' }
    if ($Status -and $Status.StepsTotal -gt 0) {
        $o.Text = 'step ' + $Status.StepsDone + ' of ' + $Status.StepsTotal + $(if ($Status.StepNow) { ': ' + $Status.StepNow } else { '' })
        $o.Pct = [int][Math]::Round(100.0 * $Status.StepsDone / $Status.StepsTotal)
        $o.Bar = Format-UsageBar -Pct $o.Pct -Width $Width   # '[####......]'
    }
    if ($Status -and $Status.PSObject.Properties.Name -contains 'Estimate' -and $Status.Estimate -and [bool]$Status.Estimate.known) { $o.Estimate = [string]$Status.Estimate.text }
    [pscustomobject]$o
}

function Get-AutopilotActivity {
    # cc-P94: what CC is doing, from status.json: the step, the tool or the last activity, and whether it has
    # been quiet past stallMinutes. Shared by the tile and the tab.
    param($Status, $Settings, [datetime]$Now)
    $nowUtc = $Now.ToUniversalTime()
    $a = [ordered]@{ Steps = ''; Activity = ''; Quiet = $false; QuietFor = $null; Stale = $false; StaleFor = $null }
    # cc-P153: the bar and "step 4 of 9"; the step's words stay on the tile's second line, as before.
    if ($Status.StepsTotal -gt 0) { $sp = Format-StepProgress $Status; $a.Steps = $sp.Bar + ' step ' + $Status.StepsDone + ' of ' + $Status.StepsTotal }
    if ($Status.LastActivity) {
        $since = $nowUtc - $Status.LastActivity
        if ($Status.State -eq 'RUNNING' -and $Settings.StallMinutes -gt 0 -and $since.TotalMinutes -gt $Settings.StallMinutes) {
            $a.Quiet = $true; $a.QuietFor = $since
        }
        if ($Status.ToolStarted -and $Status.LastTool) { $a.Activity = 'running ' + $Status.LastTool + ' for ' + (Format-ShortAge ($nowUtc - $Status.ToolStarted)) }
        else { $a.Activity = 'last activity ' + (Format-ShortAge $since) + ' ago' }
        if ($a.Quiet) { $a.Activity = 'quiet for ' + (Format-ShortAge $since) + $(if ($Status.ToolStarted -and $Status.LastTool) { ' (' + $Status.LastTool + ' still running)' } else { '' }) }
    }
    if ($Status.GeneratedAt) {
        $old = $Now - $Status.GeneratedAt
        if ($Status.State -eq 'RUNNING' -and $old.TotalMinutes -gt $script:AutopilotStaleMinutes) { $a.Stale = $true; $a.StaleFor = $old }
    }
    [pscustomobject]$a
}

function Get-AutopilotStatusGlance {
    # cc-P94: the AUTOPILOT tile from status.json. Two lines when running: where the run is, then what CC is
    # doing. Amber for NEEDS YOU, for quiet past stallMinutes, for a status.json the watch task stopped
    # rewriting, and for failing email (from the log). Never red: the hook emails about all of these itself.
    param($View, [datetime]$Now = [datetime]::Now)
    $s = $View.Status
    $t = [ordered]@{ Title = 'GOLEM'; Word = $s.State; Color = 'gray'; Detail = '' }
    # cc-P116: the queue folder as listed on this refresh (Get-AutopilotQueue, the runner's cc-P<nn>-*.md rule), not
    # status.json's queued: the hook and the watch task write that, so it goes stale while the Golem rests (it said
    # "queue empty" on 2026-10-14 with P114 and P115 waiting). status.json's list only when the folder could not be read.
    $n = $(if ($View.Queue) { @($View.Queue.Runs).Count } else { @($s.Queued).Count })
    $queued = $(if ($n) { [string]$n + ' queued' } else { 'queue empty' })
    $ignored = ''
    if ($View.Queue -and @($View.Queue.Ignored).Count) { $ignored = '; ' + @($View.Queue.Ignored).Count + ' unnumbered file(s) ignored' }
    $doneRun = ''
    if ($s.RunDone -gt 0) { $doneRun = '; ' + $s.RunDone + ' done this run' }
    $act = Get-AutopilotActivity -Status $s -Settings $View.Settings -Now $Now
    $lines = New-Object 'System.Collections.Generic.List[string]'
    switch ($s.State) {
        'OFF' { $lines.Add('not handing out prompts; ' + $queued + $doneRun + $ignored) }
        'IDLE' {
            $l = $queued
            if ($n) { $l += $(if ($View.Kind -eq 'golem') { '; say "Golem, work" in CC to start' } else { '; send CC "go" to start' }) }
            $lines.Add($l + $doneRun + $ignored)
        }
        'NEEDS YOU' {
            $t.Color = 'amber'
            $a = $s.NeedsYou
            if (-not $a) { $a = 'status.json says NEEDS YOU' }
            if ($a.Length -gt 90) { $a = $a.Substring(0, 87) + '...' }
            $lines.Add($a)
        }
        # cc-P131: STOPPED is a prompt that needs Chase (the reason, then the state line); RESTING is the Golem pausing
        # on purpose: the reason and when it can run again come from the winding and ready lines below.
        'STOPPED' {
            $t.Color = 'amber'
            $a = $s.NeedsYou
            if (-not $a) { $a = $(if ($s.StoppedPrompt) { $s.StoppedPrompt + ' stopped early, needs you' } else { 'a prompt stopped early, needs you' }) }
            if ($a.Length -gt 90) { $a = $a.Substring(0, 87) + '...' }
            $lines.Add($a)
        }
        'RESTING' {
            $t.Color = $(if ($s.RestReason -eq 'usage limit') { 'amber' } else { 'gray' })
            if ($s.Current) { $lines.Add($s.Current + ' kept as the running prompt; ' + $queued + $doneRun + $ignored) }
            else { $lines.Add($queued + $doneRun + $ignored) }
        }
        'RUNNING' {
            $t.Color = 'green'
            $parts = New-Object 'System.Collections.Generic.List[string]'
            # cc-P125: the running prompt's number, title and model.
            $who = $(if ($s.Current) { $s.Current } else { 'a prompt' })
            $cp = $View.CurrentPrompt
            if ($cp -and $cp.Readable) { $who += ' ' + $cp.Title }
            if ($cp -and $cp.Model) { $who += ' (' + $cp.Model + ')' }
            $parts.Add($who)
            if ($s.Position -gt 0 -and $s.Total -gt 0) { $parts.Add([string]$s.Position + ' of ' + $s.Total) }
            if ($act.Steps) { $parts.Add($act.Steps) }
            if ($s.StartedAt) { $parts.Add((Format-ShortAge ($Now - $s.StartedAt))) }
            $lines.Add(($parts -join '  ') + $ignored)
            $second = @(@($s.StepNow, $act.Activity, $s.Note) | Where-Object { $_ }) -join '; '
            if ($second) { $lines.Add($second) }
            if ($act.Quiet) { $t.Color = 'amber' }
            if ($act.Stale) {
                $t.Color = 'amber'
                $lines.Add('status ' + [int][Math]::Floor($act.StaleFor.TotalMinutes) + ' min old; watch task not running?')
            }
        }
    }
    # cc-P125: the long run, then winding down (amber) or resting with ENABLED kept (dark).
    $lrl = Get-LongRunLine $s.LongRun $Now
    if ($lrl) { $lines.Add($lrl) }
    $wl = Get-WindingLine $s $View.TreeClean
    if ($wl) {
        $lines.Add($wl)
        if ($s.Winding) { $t.Color = 'amber' }
        elseif ($s.State -notin @('NEEDS YOU', 'STOPPED') -and $s.RestReason -ne 'usage limit') { $t.Color = 'gray' }
        elseif ($s.RestReason -eq 'usage limit') { $t.Color = 'amber' }
    }
    # cc-P131: when the rest can run again, counted down from now.
    $rl = Get-ReadyLine $s $Now
    if ($rl) { $lines.Add($rl) }
    if ($View.EmailFailing) {
        $t.Color = 'amber'
        $lines[$lines.Count - 1] += '; email failing'
    }
    $t.Detail = $lines -join "`n"
    [pscustomobject]$t
}

function Format-AutopilotPanel {
    # The Golem tab's read-only panel, as lines with a colour name like Format-StatusReport's.
    param($View, $Config, [datetime]$Now = [datetime]::Now, [int]$LogLines = 20)
    $o = New-Object 'System.Collections.Generic.List[object]'
    $add = { param($t, $c) $o.Add([pscustomobject]@{ Text = $t; Color = $(if ($c) { $c } else { 'normal' }) }) }
    $ap = $Config.Autopilot
    $tile = Get-AutopilotGlance -View $View -Now $Now
    $tl = @(([string]$tile.Detail) -split "`n")
    & $add ('GOLEM     : ' + $tile.Word + '. ' + $tl[0]) $tile.Color
    foreach ($x in @($tl | Select-Object -Skip 1)) { & $add ('            ' + $x) $tile.Color }
    # cc-P99: the start phrase, and which folder is active (the console reads it, the buttons run its scripts).
    & $add 'In a CC session, say: "Golem, please work on the shard tasks"' 'head'
    # cc-P125: the plan usage, as the Claude app shows it (Claude Code's status line figures), refreshed with the tab.
    foreach ($ub in @(Get-UsageBars -UsageText $View.UsageText -Now $Now -Estimate $View.Estimate -PlanText $View.PlanUsageText -Settings $View.Settings)) { & $add ('usage     : ' + $ub.Text) $ub.Color }
    # cc-P152: the ChatGPT plan (Codex) beside them: art now, reviews and the lane later share these limits.
    $cxSet = $null
    if ($Config.Autopilot.SettingsFile -and (Test-Path -LiteralPath $Config.Autopilot.SettingsFile -PathType Leaf)) { try { $cxSet = [IO.File]::ReadAllText($Config.Autopilot.SettingsFile) | ConvertFrom-Json } catch { } }
    foreach ($cb in @(Get-CodexUsageRows -CodexText $View.CodexUsageText -Now $Now -Settings $cxSet)) { & $add ('codex     : ' + $cb.Text) $cb.Color }
    if ($ap.Kind -eq 'golem') { & $add ('active    : the Golem (' + $ap.Root + '). ' + $ap.Why + '.') 'green' }
    else { & $add ('active    : cc-autopilot (' + $ap.Root + '), the Golem''s predecessor. ' + $ap.Why + '. Until then the phrase is not hooked up; send CC "go".') 'amber' }
    foreach ($p in @($View.Problems)) { & $add ('could not read: ' + $p) 'gray' }
    if ($null -eq $View.Enabled) { & $add 'on or off: unknown' 'gray' }
    elseif ($View.Enabled) { & $add ('on or off: ON (' + $ap.Enabled + ' is there)') 'green' }
    else { & $add 'on or off: OFF (no ENABLED file). Wake the Golem starts it; CC then picks up the queue at its next stop.' 'gray' }
    # cc-P94: the run, the steps and the activity, from status.json; the three settings, from settings.json.
    $st = $View.Status
    if ($st) {
        $fmt = { param($d) $d.ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant) }
        $act = Get-AutopilotActivity -Status $st -Settings $View.Settings -Now $Now
        $line = 'status    : ' + $ap.StatusFile
        if ($st.GeneratedAt) { $line += ', written ' + (& $fmt $st.GeneratedAt) + ' (' + (Format-ShortAge ($Now - $st.GeneratedAt)) + ' ago)' }
        if ($act.Stale) { & $add ($line + '. Older than ' + $script:AutopilotStaleMinutes + ' min while running: is the scheduled task "' + $ap.WatchTask + '" running?') 'amber' }
        else { & $add $line 'gray' }
        $cap = $(if ($st.MaxPerRun -gt 0) { 'cap ' + $st.MaxPerRun } else { 'no cap' })
        $run = $(if ($st.Position -gt 0) { [string]$st.Position + ' of ' + $st.Total + ', ' + $cap } else { 'no prompt running, ' + $cap })
        $run += ', ' + $st.RunDone + ' done'
        if ($st.RunStarted) { $run += ', started ' + (& $fmt $st.RunStarted) }
        & $add ('run       : ' + $run) 'normal'
        $lrl = Get-LongRunLine $st.LongRun $Now
        if ($lrl) { & $add ('            ' + $lrl) 'green' }
        $wl = Get-WindingLine $st $View.TreeClean
        if ($wl) { & $add ('winding   : ' + $wl) $(if ($st.Winding) { 'amber' } else { 'gray' }) }
        # cc-P131: when it can run again, the night shift, self-wake, and the CLI as the Golem last saw it.
        $rl = Get-ReadyLine $st $Now
        if ($rl) { & $add ('ready     : ' + $rl) $(if ($st.RestReason -eq 'usage limit' -and $st.ReadyAt -and $st.ReadyAt -gt $Now) { 'amber' } else { 'green' }) }
        & $add ('night     : ' + (Get-NightShiftLine $st $View.Settings $Now)) 'normal'
        $sw = 'self-wake : ' + $(if ($st.AutoWake) { 'on' } else { 'off' })
        if ($st.WakePending) { $sw += '; pending for ' + [string]$st.WakePending.for + ' (' + [string]$st.WakePending.why + ')' }
        if ($st.LastSelfWake -and $st.LastSelfWake.at) { $lsw = ConvertFrom-AutopilotTime $st.LastSelfWake.at 'Local'; $sw += '; last woke a ' + [string]$st.LastSelfWake.model + ' session for ' + [string]$st.LastSelfWake.for + $(if ($lsw) { ' ' + (Format-ShortAge ($Now - $lsw)) + ' ago' } else { '' }) }
        & $add $sw 'normal'
        $sl = Get-SessionsLine $st.Sessions
        if ($sl) { & $add ('sessions  : ' + $sl.Text) $sl.Color }
        $el = Get-EndOfWeekLine $st.EndOfWeek $Now
        if ($el) { & $add ('end week  : ' + $el.Text) $el.Color }
        foreach ($pl in @(Get-ProjectionLines $st.Projection)) { & $add $pl.Text $pl.Color }
        $ml = Get-MarathonLine $st.Marathon $Now
        if ($ml) { & $add $ml.Text $ml.Color }
        if ($st.Cli -and $st.Cli.path) {
            $cc = ConvertFrom-AutopilotTime $st.Cli.checkedUtc 'Utc'
            & $add ('cli       : ' + [string]$st.Cli.path + $(if ([bool]$st.Cli.signedIn) { ', signed in' } else { ', NOT signed in: run claude auth login in a terminal' }) + $(if ($cc) { ' (checked ' + (Format-ShortAge ($Now.ToUniversalTime() - $cc)) + ' ago)' } else { '' })) $(if ([bool]$st.Cli.signedIn) { 'green' } else { 'amber' })
        } else { & $add 'cli       : not checked yet: Check CLI sign-in (or the first wake) records it' 'gray' }
        $sp = Format-StepProgress $st 20   # cc-P153: the bar, the step and the estimate
        if ($st.StepsTotal -gt 0) {
            & $add ('steps     : ' + $sp.Bar + ' ' + $sp.Text + $(if ($st.StepsSource -eq 'set-step') { ' (Set-Step)' } else { '' })) 'normal'
        } else {
            & $add 'steps     : none reported (no task list and no Set-Step yet for this prompt)' 'gray'
        }
        if ($sp.Estimate) { & $add ('estimate  : ' + $sp.Estimate) $(if ($st.Estimate.over) { 'amber' } else { 'normal' }) }
        if ($st.LastActivity) {
            $line = 'activity  : ' + $act.Activity
            if ($st.LastTool -and -not $st.ToolStarted) { $line += ' (' + $st.LastTool + ')' }
            & $add $line $(if ($act.Quiet) { 'amber' } else { 'normal' })
        } else {
            & $add 'activity  : none reported yet' 'gray'
        }
        if ($st.Note) { & $add ('note      : ' + $st.Note) 'normal' }
    } else {
        & $add ('status    : ' + $View.StatusNote) 'gray'
    }
    $set = $View.Settings
    & $add ('settings  : progressEmails ' + $set.ProgressEmails.ToString().ToLowerInvariant() + ', maxPerRun ' + $set.MaxPerRun + ', stallMinutes ' + $set.StallMinutes + ' (' + $set.Source + ')') 'normal'
    & $add ('            autoWake ' + $set.AutoWake.ToString().ToLowerInvariant() + ', nightShift ' + $set.NightShiftEnabled.ToString().ToLowerInvariant() + ' ' + $set.NightShiftStart + ' to ' + $set.NightShiftEnd) 'normal'
    & $add ('            mailMode ' + $set.MailMode + $(if ($set.MailMode -eq 'briefs') { ' (only the Tinker''s briefs are emailed)' } elseif ($set.MailMode -eq 'off') { ' (no email at all)' } else { '' })) 'normal'   # cc-P479
    $s = $View.State
    if ($s) {
        if ($s.Current) {
            $run = 'running   : ' + $s.Current
            if ($s.CurrentFile) { $run += ' (' + $s.CurrentFile + ')' }
            if ($s.StartedAt) { $run += ' for ' + (Format-Age ($Now - $s.StartedAt)) + ', since ' + $s.StartedAt.ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant) }
            & $add $run 'green'
        } else {
            & $add 'running   : nothing' 'normal'
        }
        $last = 'none yet'
        if ($s.LastHandled) { $last = 'P' + $s.LastHandled + ' (said DONE or STOPPED)' }
        & $add ('last      : ' + $last) 'normal'
    }
    if ($View.Alert) { & $add ('needs you : ' + $View.Alert + '. Answer CC; it was emailed.') 'amber' }
    if ($View.EmailFailing) { & $add ('email     : FAILING since the last hand-off. ' + $View.LastFailure) 'red' }
    # cc-P184: one card per Golem while several work the queue (status.json golems).
    foreach ($gl in @(Get-GolemCardLines -Status $View.Status -Now $Now)) { & $add $gl.Text $gl.Color }
    foreach ($gl in @(Get-AwayLines -Status $View.Status)) { & $add $gl.Text $gl.Color }   # cc-P149
    foreach ($gl in @(Get-GolemQuestionLines -Questions @(Get-OpenGolemQuestions -Root ([string]$ap.GolemRoot)))) { & $add $gl.Text $gl.Color }   # cc-P216
    $q = $View.Queue
    if ($null -eq $q) {
        & $add ('queue     : could not be listed (' + $ap.Queue + ')') 'gray'
    } else {
        $head = 'queue     : ' + $ap.Queue + ', ' + (Format-AutopilotQueueCount $q) + ', in run order'
        if (-not @($q.Runs).Count) { $head = 'queue     : ' + $ap.Queue + ' is empty' }
        & $add $head 'normal'
        $i = 0
        foreach ($r in @($q.Runs)) {
            $i++
            # cc-P125: number, title, model and Kind (size); the list below the panel shows each one's first paragraph.
            $pi = @(@($View.QueueInfo) | Where-Object { $_.Name -eq $r } | Select-Object -First 1)
            $line = '            ' + $i + '. ' + $r
            if ($pi.Count -and $pi[0].Readable) { $line = '            ' + $i + '. P' + $pi[0].Number + '  ' + $pi[0].Title + $(if ($pi[0].Model) { '  [' + $pi[0].Model + ']' } else { '' }) + $(if ($pi[0].Kind) { '  ' + $pi[0].Kind } else { '' }) + $(if ($pi[0].Cont) { '  (continuation: goes first)' } else { '' }) }
            & $add $line 'normal'
        }
        foreach ($x in @($q.Ignored)) { & $add ('            IGNORED ' + $x + ' (unnumbered: only cc-P<nn>-*.md runs; the overseer numbers it)') 'amber' }
    }
    # cc-P125: the last ten finished prompts, with the headline of each one's notes.
    $fin = @($View.Finished)
    if ($fin.Count) {
        & $add ('finished  : the last ' + $fin.Count + ', newest first') 'normal'
        foreach ($x in $fin) {
            $c = $(if ($x.Result -eq 'STOPPED') { 'amber' } else { 'normal' })
            & $add ('            P' + $x.Number + ' ' + $x.Result + '  ' + $x.Time.ToString('MM-dd HH:mm', $script:Invariant) + '  ' + $x.Title) $c
            if ($x.Headline) { & $add ('                ' + $(if ($x.Headline.Length -gt 140) { $x.Headline.Substring(0, 137) + '...' } else { $x.Headline })) 'gray' }
        }
    }
    & $add '' 'normal'
    & $add ('== ' + $ap.LogName + ', the last ' + $LogLines + ' actions (commit output skipped) ' + ('=' * 20)) 'head'
    $acts = @($View.Actions)
    if (-not $acts.Count) { & $add '(nothing logged)' 'gray' }
    foreach ($a in @($acts | Select-Object -Last $LogLines)) {
        $c = 'normal'
        if ($a.Text -like 'EMAIL FAILED*') { $c = 'red' }
        elseif ($a.Text -like 'emailed: *' -and (Test-AutopilotAlert ($a.Text -replace '^emailed: (\[(Shard CC|Golem)\] )?', ''))) { $c = 'amber' }
        elseif ($a.Text -match '^handed P\d+|DONE, committing') { $c = 'green' }
        elseif ($a.Text -match '^(queue: ignoring|idle stop|dry run:)') { $c = 'gray' }
        & $add $a.Line $c
    }
    $o.ToArray()
}

function Get-WebActionPlan {
    # cc-P182. One web console Golem button (an entry of web-console\golem-actions.json) to the Shard Console's own plan:
    # the console action's Get-ActionPlan, with the page's inputs where the console's boxes would put them, refused when
    # the button is disabled, when the Golem is not in the state the button is for (a stale page), when the plan
    # refuses, or when the typed word is not the expected one (case ignored: phones capitalize). Phrase is the console's
    # own confirm phrase (or the word), which goes on to Invoke-Plan unchanged so its own check holds too.
    # cc-P187: -GolemName (another Golem than the integrator, picked on the page): a button with a golemWord then wants
    # that word and the Golem's name ("wake bronze"; '#prompt' or '#stopped' is the plan's phrase and the name), so the
    # typed confirm says which Golem it acts on.
    param($Entry, [string]$Typed, [hashtable]$Inputs, $Config, $Facts, [datetime]$Now = [datetime]::Now, [string]$GolemName)
    $o = [pscustomobject]@{ Ok = $false; Refusal = ''; Plan = @(); Phrase = ''; ActionKey = [string]$Entry.action; Label = [string]$Entry.label }
    if ($Entry.disabled) { $o.Refusal = [string]$Entry.disabled; return $o }
    if (-not $o.ActionKey) { $o.Refusal = 'This button runs nothing.'; return $o }
    if (-not $Inputs) { $Inputs = @{} }
    if ($Entry.fixed) { foreach ($p in $Entry.fixed.PSObject.Properties) { $Inputs[$p.Name] = [string]$p.Value } }
    $f = $Facts.PSObject.Copy()
    $map = @{ model = 'WakeModel'; cap = 'LongRunCap'; until = 'LongRunUntil'; start = 'NightStart'; end = 'NightEnd'; prompt = 'RunNextPrompt'; assignTo = 'AssignGolem'; next = 'AssignRunNext'; marathon = 'MarathonMode'; skipDate = 'SkipDate'; skipGolem = 'SkipGolem' }   # cc-P287 skipDate, skipGolem
    foreach ($k in $map.Keys) { if ($Inputs.ContainsKey($k)) { $f | Add-Member -NotePropertyName $map[$k] -NotePropertyValue ([string]$Inputs[$k]) -Force } }
    $st = $null
    if ($f.StatusText) { try { $st = ConvertFrom-AutopilotStatus $f.StatusText } catch { } }
    $gs = ConvertFrom-AutopilotSettings $f.SettingsText
    $when = [string]$Entry.when
    if ($when) {
        $kind, $val = $when.Split(':', 2)
        $wind = $(if ($st) { [string]$st.Winding } else { '' })
        $ok = switch ($kind) {
            'winding'    { $wind -eq $val }
            'notWinding' { $wind -ne $val }
            'nightShift' { [bool]$gs.NightShiftEnabled -eq ($val -eq 'on') }
            'autoWake'   { [bool]$gs.AutoWake -eq ($val -eq 'on') }
            'marathonNot' { [string]$gs.Marathon -ne $val }   # cc-P200
            'runNext'    { [bool][string]$f.RunNext }
            'mergePaused' { (Test-MergePaused $Config $f.SettingsText) -eq ($val -eq 'on') }   # cc-P208
            default      { $false }
        }
        if (-not $ok) {
            $why = switch ($kind) {
                'winding'    { 'it is not set now' }
                'notWinding' { $(if ($val -eq 'after-current') { 'wrap up is already set: the button is Cancel wrap up now' } else { 'a stopping point is already asked: the button is Cancel stopping point now' }) }
                'nightShift' { 'the night shift is ' + $(if ($gs.NightShiftEnabled) { 'already on' } else { 'already off' }) }
                'autoWake'   { 'self-wake is ' + $(if ($gs.AutoWake) { 'already on' } else { 'already off' }) }
                'marathonNot' { 'marathon is already ' + $val }
                'runNext'    { 'nothing is picked to go next' }
                'mergePaused' { 'merging is ' + $(if ($val -eq 'on') { 'not paused' } else { 'already paused' }) }
                default      { 'the Golem is not in that state' }
            }
            $o.Refusal = 'Not now: ' + $why + '. Nothing was run; the page refreshes.'
            return $o
        }
    }
    $plan = @(Get-ActionPlan -Action $o.ActionKey -Config $Config -AutopilotFacts $f -Now $Now)
    $o.Plan = $plan
    $refuse = @($plan | Where-Object { $_.Kind -eq 'refuse' })
    if ($refuse.Count) { $o.Refusal = [string]$refuse[0].Text; return $o }
    $confirm = @($plan | Where-Object { $_.Kind -eq 'confirm' } | Select-Object -First 1)
    $word = [string]$Entry.word
    $expected = $(if ($word -like '#*') { $(if ($confirm.Count) { [string]$confirm[0].Phrase } else { '' }) } else { $word })
    if ($word -eq '#prompt' -and $Inputs.ContainsKey('prompt') -and ([string]$Inputs['prompt']).Trim() -and $expected -and ($expected -ine (([string]$Inputs['prompt']).Trim() -replace '^(?i)(\d)', 'P$1'))) { $o.Refusal = 'The picked prompt and the plan disagree; refresh the page.'; return $o }
    if (-not $expected) { $o.Refusal = 'This button has no word to confirm with; nothing was run.'; return $o }
    if ($GolemName -and $Entry.golemWord) {
        $expected = $(if ([string]$Entry.golemWord -like '#*') { $expected } else { [string]$Entry.golemWord }) + ' ' + $GolemName.ToLowerInvariant()
    }
    if ($null -eq $Typed -or -not [string]::Equals($expected, $Typed.Trim(), [StringComparison]::OrdinalIgnoreCase)) {
        $o.Refusal = 'Refused: type ' + $expected + ' to confirm. Nothing was run.'
        return $o
    }
    $o.Phrase = $(if ($confirm.Count) { [string]$confirm[0].Phrase } else { $expected })
    $o.Ok = $true
    $o
}

function Resolve-ActionShardKey {
    # cc-P187: which shard (or area) an action is for, as the headless -Action run names it (and its banner and the
    # action log say); -Shard is the test or live pick of the actions that take either (snapshots, the shard question).
    param([string]$Action, [string]$Shard = 'test')
    $Action = Resolve-ActionAlias $Action
    $sk = $Shard
    if ($Action -like 'live.*') { $sk = 'live' }
    if ($Action -like 'test.*') { $sk = 'test' }
    if ($Action -like 'commit.*') { $sk = 'repos' }
    if ($Action -like 'package.*') { $sk = 'package' }
    if ($Action -like 'golem.*') { $sk = 'golem' }
    if ($Action -like 'links.*') { $sk = 'links' }
    if ($Action -like 'vpn.*') { $sk = 'network' }
    if ($Action -like 'web.*') { $sk = 'websites' }
    if ($Action -like 'art.*' -or $Action -like 'map.*') { $sk = 'mapart' }
    $sk
}

function Get-WebPagePlan {
    # cc-P187. One web console page button (an entry of web-console\console-actions.json) to the Shard Console's own
    # plan: the page's inputs where the window's boxes and lists put them, the facts Get-ActionFacts read ($Pre), the
    # console's Get-ActionPlan, and the checks the window makes before it runs: a refusal; a step that opens something
    # on this PC or asks a password in a window (PC only); the typed phrase (case ignored: phones capitalize); the yes
    # or no question. Expect is the phrase the plan asks for, the console's own ('' when it asks none). A dangerous
    # button, and any button on the LIVE shard or with a yes/no question, also needs -Second (the page's second confirm
    # screen), which is also the yes to that question. -Dry builds the plan for the page's look first and checks no word.
    param($Entry, [string]$Typed, [hashtable]$Inputs, $Config, $Pre, [switch]$Second, [switch]$Dry, [datetime]$Now = [datetime]::Now)
    $o = [pscustomobject]@{ Ok = $false; Refusal = ''; Plan = @(); Phrase = ''; Expect = ''; ConfirmText = ''; Asks = ''; Yes = $false; Danger = $false
        ActionKey = [string]$Entry.action; ShardKey = ''; Label = [string]$Entry.label }
    if ($Entry.pcOnly) { $o.Refusal = 'PC only: ' + [string]$Entry.pcOnly; return $o }
    if ($Entry.disabled) { $o.Refusal = [string]$Entry.disabled; return $o }
    if (-not $o.ActionKey) { $o.Refusal = 'This button runs nothing.'; return $o }
    if (-not $Inputs) { $Inputs = @{} }
    $in = @{}
    foreach ($k in @('shard', 'snapshotName', 'snapshot', 'commitMessage', 'force', 'packageNotes', 'packageZip', 'imageTag', 'commit', 'site', 'vdFile', 'body', 'huesFile')) {
        $in[$k] = $(if ($Inputs.ContainsKey($k) -and $null -ne $Inputs[$k]) { ([string]$Inputs[$k]).Trim() } else { '' })
    }
    $shard = $(if ($in.shard -in @('test', 'live')) { $in.shard } else { 'test' })
    $o.ShardKey = Resolve-ActionShardKey -Action $o.ActionKey -Shard $shard
    $o.Danger = ([string]$Entry.kind -eq 'dangerous' -or $o.ShardKey -eq 'live')
    $state = $(if ($Pre) { $Pre.State } else { $null })
    $image = $in.imageTag
    if ($o.ActionKey -eq 'test.deploy' -and -not $image) { $o.Refusal = 'Pick the image to deploy first. Nothing was run.'; return $o }
    # As the window does: Roll back runs the newest good image, which it pins before the confirm.
    if ($o.ActionKey -eq 'fix.rollback' -and -not $image -and $state) { $lg = Get-LastGoodImage $state.Images; if ($lg) { $image = $lg.Tag } }
    if ($o.ActionKey -eq 'fix.rebuild' -and -not $in.commit) { $o.Refusal = 'Pick the commit to rebuild first. Nothing was run.'; return $o }
    $plan = @(Get-ActionPlan -Action $o.ActionKey -ShardKey $shard -Config $Config -State $state -SnapshotName $in.snapshotName -Snapshot $in.snapshot -Now $Now `
        -CommitMessage $in.commitMessage -CommitFacts $(if ($Pre) { $Pre.Facts }) -PackageNotes $in.packageNotes -PackageZip $in.packageZip -PackageFacts $(if ($Pre) { $Pre.PackageFacts }) `
        -ImageTag $image -CommitForce:($in.force -eq 'yes' -and $o.ActionKey -eq 'commit.run') -WipeFacts $(if ($Pre) { $Pre.WipeFacts }) -AutopilotFacts $(if ($Pre) { $Pre.AutopilotFacts }) `
        -VpnFacts $(if ($Pre) { $Pre.VpnFacts }) -FixFacts $(if ($Pre) { $Pre.FixFacts }) -Commit $in.commit -Site $in.site -VdFile $in.vdFile -Body $in.body -HuesFile $in.huesFile)
    $o.Plan = $plan
    $refuse = @($plan | Where-Object { $_.Kind -eq 'refuse' })
    if ($refuse.Count) { $o.Refusal = [string]$refuse[0].Text; return $o }
    $launch = @($plan | Where-Object { $_.Kind -eq 'launch' })
    if ($launch.Count) { $o.Refusal = 'PC only: this opens ' + (Split-Path -Leaf ([string]$launch[0].Path)) + ' on the game PC. Nothing was run.'; return $o }
    if (@($plan | Where-Object { $_.Kind -eq 'secret' }).Count) { $o.Refusal = 'PC only: this asks a password in a window on the game PC, and a password never goes through a web page. Nothing was run.'; return $o }
    $confirm = @($plan | Where-Object { $_.Kind -eq 'confirm' } | Select-Object -First 1)
    if ($confirm.Count) { $o.Expect = [string]$confirm[0].Phrase; $o.ConfirmText = [string]$confirm[0].Text }
    $ask = @($plan | Where-Object { $_.Kind -eq 'ask' } | Select-Object -First 1)
    if ($ask.Count) { $o.Asks = [string]$ask[0].Text; $o.Danger = $true }
    if ($Dry) { $o.Ok = $true; return $o }
    if ($o.Expect -and ($null -eq $Typed -or -not [string]::Equals($o.Expect, $Typed.Trim(), [StringComparison]::OrdinalIgnoreCase))) {
        $o.Refusal = 'Refused: type ' + $o.Expect + ' to confirm. Nothing was run.'
        return $o
    }
    if ($o.Danger -and -not $Second) { $o.Refusal = 'Refused: this needs the second confirm (what it does and to what, then Yes). Nothing was run.'; return $o }
    $o.Phrase = $o.Expect
    $o.Yes = [bool]($ask.Count -and $Second)
    $o.Ok = $true
    $o
}

function Get-AutopilotPlan {
    # The Golem tab's buttons. Each opens one of the Golem's own scripts in a PowerShell window
    # of its own, which stays open to be read, or opens the queue folder, its folder or its log. $Facts is
    # Get-AutopilotFacts': only whether files exist is used here. cc-P99: $Config.Autopilot is whichever of the
    # Golem and cc-autopilot is active (Get-GolemConfig), so before the cutover these run cc-autopilot's scripts.
    param([string]$Action, $Config, $Facts, [datetime]$Now = [datetime]::Now)
    $Action = Resolve-ActionAlias $Action
    $ap = $Config.Autopilot
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $ps = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File')
    $need = {
        param($Path, $What)
        if (-not ($Facts.Present -and $Facts.Present[$Path])) { $steps.Add((New-PlanStep -Kind refuse -Text ($What + ' was not found: ' + $Path + '. Nothing was run.'))); return $false }
        $true
    }
    $who = $(if ($ap.Kind -eq 'golem') { 'the Golem' } else { 'cc-autopilot (the Golem is not moved to yet)' })
    switch ($Action) {
        'golem.on' {
            if (-not $Facts.EmailConfigured) { $steps.Add((New-PlanStep -Kind refuse -Text ('Run Golem email setup first: there is no ' + $ap.EmailFile + ', and the Golem emails you whenever CC needs you.'))); break }
            if (-not (& $need $ap.Start (Split-Path -Leaf $ap.Start))) { break }
            $steps.Add((New-PlanStep -Kind say -Text ('Wake the Golem (' + $who + '): ' + (Split-Path -Leaf $ap.Start) + ' creates ENABLED and lists the queue. Nothing runs yet: at CC''s next stop the hook hands it the lowest-numbered prompt, and after each P<nn> DONE it commits and pushes both repos to GitHub before handing over the next.')))
            $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.Start) -Text 'In a window of its own, which stays open.'))
            $steps.Add((New-PlanStep -Kind log -Text 'golem.on'))
        }
        'golem.off' {
            if (-not (& $need $ap.Stop (Split-Path -Leaf $ap.Stop))) { break }
            $steps.Add((New-PlanStep -Kind say -Text ('Rest the Golem (' + $who + '): ' + (Split-Path -Leaf $ap.Stop) + ' removes ENABLED. A prompt CC is running still finishes, but nothing is committed after it and nothing new is handed over; commit by hand.')))
            $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.Stop) -Text 'In a window of its own, which stays open.'))
            $steps.Add((New-PlanStep -Kind log -Text 'golem.off'))
        }
        'golem.email' {
            if (-not (& $need $ap.Setup 'Setup-Email.ps1')) { break }
            $steps.Add((New-PlanStep -Kind say -Text 'Golem email setup: Setup-Email.ps1 asks for the sender Gmail address, the address to alert and the sender''s app password, in its own window and never on a command line. The password is stored with Windows DPAPI in smtp-cred.xml. It then sends a test email.'))
            $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.Setup) -Text 'In a window of its own, which stays open.'))
            $steps.Add((New-PlanStep -Kind log -Text 'golem.email'))
        }
        'golem.test' {
            if (-not $Facts.EmailConfigured -or -not $Facts.CredStored) { $steps.Add((New-PlanStep -Kind refuse -Text ('Run Golem email setup first: ' + $ap.EmailFile + ' and ' + $ap.CredFile + ' are both needed to send.'))); break }
            if (-not (& $need $ap.TestEmail 'Send-TestEmail.ps1')) { break }
            $steps.Add((New-PlanStep -Kind say -Text 'Send test email: Send-TestEmail.ps1 sends one email to the address in email.json the way the hook does, and prints sent or the error. It changes nothing.'))
            $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.TestEmail) -Text 'In a window of its own, which stays open.'))
            $steps.Add((New-PlanStep -Kind log -Text 'golem.test'))
        }
        'golem.queue' {
            $steps.Add((New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($ap.Queue) -Text 'Opening the queue folder. Only cc-P<nn>-*.md runs, lowest number first; numbers belong to the overseer.'))
        }
        'golem.log' {
            if (-not $Facts.LogExists) { $steps.Add((New-PlanStep -Kind refuse -Text ('There is no ' + $ap.LogFile + ' yet: the hook writes it the first time it acts.'))); break }
            $steps.Add((New-PlanStep -Kind launch -Path $ap.Notepad -Arguments @($ap.LogFile) -Text ('Opening ' + $ap.LogName + ' in Notepad. Read it there; the hook appends to it.')))
        }
        'golem.settings' {
            # cc-P94. Opens it and nothing more: whatever is saved there the hook reads at its next event.
            if (-not $Facts.SettingsExists) { $steps.Add((New-PlanStep -Kind refuse -Text ('There is no ' + $ap.SettingsFile + ': the hook uses its defaults (progressEmails true, maxPerRun 6, stallMinutes 45). Nothing was opened.'))); break }
            $steps.Add((New-PlanStep -Kind launch -Path $ap.Notepad -Arguments @($ap.SettingsFile) -Text 'Opening settings.json in Notepad. A saved change counts from the hook''s next event: progressEmails (true or false), maxPerRun (0 is no cap), stallMinutes.'))
        }
        'golem.folder' {
            $steps.Add((New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($ap.Root) -Text ('Opening ' + $ap.Root + ' (' + $who + ') in Explorer.')))
        }
        # cc-P125: the run buttons, the Golem's only. Each runs its own small script (which runs one event of the hook)
        # in a window of its own; the hook itself never runs from here.
        { $_ -in @('golem.longrun', 'golem.endlongrun', 'golem.wrapup', 'golem.stoppoint', 'golem.resume', 'golem.openprompt', 'golem.opennotes', 'golem.autowake', 'golem.nightshift', 'golem.marathon', 'golem.clicheck', 'golem.poke', 'golem.runnext', 'golem.retry', 'golem.integrate', 'golem.assign', 'golem.mergepause', 'golem.mergeresume', 'golem.away', 'golem.awayoff', 'golem.tinkerrun', 'golem.tinkerpause', 'golem.tinkerresume', 'golem.lanenow', 'golem.answer', 'golem.skipday') } {
            if ($ap.Kind -ne 'golem') { $steps.Add((New-PlanStep -Kind refuse -Text 'Only the Golem has this (Move to the Golem first).')); break }
            $st = $null
            if ($Facts.StatusText) { try { $st = ConvertFrom-AutopilotStatus $Facts.StatusText } catch { } }
            $running = [string]$Facts.Running
            $gset = ConvertFrom-AutopilotSettings $Facts.SettingsText
            switch ($Action) {
                'golem.longrun' {
                    if (-not (& $need $ap.LongRunStart 'Start-LongRun.ps1')) { break }
                    $capText = ([string]$Facts.LongRunCap).Trim()
                    $cap = -1
                    if ($capText -match '^(?i)(all|0|until.*empty|)$') { $cap = 0 } elseif (-not [int]::TryParse($capText, [ref]$cap) -or $cap -lt 0) { $steps.Add((New-PlanStep -Kind refuse -Text ("The cap is a number of prompts, or all (until the queue is empty): '" + $capText + "'."))); break }
                    $until = [datetime]::MinValue
                    if (-not [datetime]::TryParse(([string]$Facts.LongRunUntil).Trim(), $script:Invariant, [Globalization.DateTimeStyles]::None, [ref]$until)) { $steps.Add((New-PlanStep -Kind refuse -Text ("The end time is a date and time, like 2026-10-19 06:00: '" + $Facts.LongRunUntil + "'."))); break }
                    if ($until -le [datetime]::Now) { $steps.Add((New-PlanStep -Kind refuse -Text ('The end time ' + $until.ToString('yyyy-MM-dd HH:mm', $script:Invariant) + ' is in the past.'))); break }
                    $what = $(if ($cap -gt 0) { [string]$cap + ' prompts' } else { 'until the queue is empty' })
                    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('A long run: ' + $what + ', until ' + $until.ToString('ddd yyyy-MM-dd HH:mm', $script:Invariant) + '. The Golem then drops back to maxPerRun by itself; settings.json is not changed. It still commits and pushes after every DONE, and rests for a fresh session when the context is full.')))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.LongRunStart + @('-Cap', [string]$cap, '-Until', $until.ToString('s', $script:Invariant))) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.longrun'))
                }
                'golem.endlongrun' {
                    if (-not (& $need $ap.LongRunEnd 'End-LongRun.ps1')) { break }
                    $steps.Add((New-PlanStep -Kind say -Text 'End long run: maxPerRun counts again from the next hand-off.'))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.LongRunEnd) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.endlongrun'))
                }
                'golem.wrapup' {
                    if (-not (& $need $ap.WrapUp 'Wrap-Up.ps1')) { break }
                    if ($st -and $st.Winding -eq 'after-current') {
                        $steps.Add((New-PlanStep -Kind say -Text ('Cancel wrap up: the queue goes on after ' + $running + '.')))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.WrapUp + @('-Cancel')) -Text 'In a window of its own, which stays open.'))
                    } else {
                        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ($(if ($running) { $running + ' runs to DONE, is committed and pushed, then the Golem rests (it stays ENABLED). ' } else { 'Nothing is running: the Golem rests now (it stays ENABLED). ' }) + 'Both repos are then committed: a good moment to build or publish a package. Resume: say "Golem, please work on the shard tasks" in CC.')))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.WrapUp) -Text 'In a window of its own, which stays open.'))
                    }
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.wrapup'))
                }
                'golem.stoppoint' {
                    if (-not (& $need $ap.StopPoint 'Find-StoppingPoint.ps1')) { break }
                    if ($st -and $st.Winding -eq 'stopping-point') {
                        if ($st.StopTold) { $steps.Add((New-PlanStep -Kind say -Text ('CC has already been told to find a stopping point in ' + $running + ': cancelling changes nothing for ' + $running + '; it will still end PAUSED.'))) }
                        else { $steps.Add((New-PlanStep -Kind say -Text 'Cancel stopping point: CC has not been told yet, so nothing changes.')) }
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.StopPoint + @('-Cancel')) -Text 'In a window of its own, which stays open.'))
                    } else {
                        if (-not $running) { $steps.Add((New-PlanStep -Kind refuse -Text 'Nothing is running, so there is nothing to stop. Wrap up rests the Golem now.')); break }
                        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('At its next tool use CC is told to finish the step it is on in ' + $running + ', leave both repos building and green, write a continuation prompt (cc-P<next>-cont-<topic>.md, which goes first when the Golem resumes) and end ' + $running + ' PAUSED. The Golem then commits, pushes and rests (ENABLED kept). There is no hard stop.')))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.StopPoint) -Text 'In a window of its own, which stays open.'))
                    }
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.stoppoint'))
                }
                'golem.resume' {
                    # cc-P131. A new Claude Code CLI session in the shard-migration folder, on the picked model, opened with the
                    # start phrase as its first message (Wake-Golem.ps1 -Model X, which runs golem.ps1 -Event SelfWake -Manual).
                    # cc-P153: Poke the Golem. The picker's Auto (the default) passes -Model Auto: the Golem picks the model from
                    # the prompt's Model line with the function its self-wake uses (Get-WakeTarget); Fable or Opus overrides it.
                    # Refused, each with its reason: the Golem off, the queue empty, a prompt running in a live session, the
                    # CLI missing or not signed in.
                    if (-not (& $need $ap.WakeScript 'Wake-Golem.ps1')) { break }
                    if (-not $Facts.Enabled) { $steps.Add((New-PlanStep -Kind refuse -Text 'The Golem is off (no ENABLED file): press Wake the Golem in the Golem group first (it turns the Golem on), then Poke the Golem.')); break }
                    $q = $(if ($null -ne $Facts.QueueNames) { Get-AutopilotQueue @($Facts.QueueNames) } else { $null })
                    if (-not $q -or -not @($q.Runs).Count) { $steps.Add((New-PlanStep -Kind refuse -Text ('The queue is empty (' + $ap.Queue + '): nothing to open a session for.'))); break }
                    $carry = ''
                    if ($running) {
                        $quiet = $false; $since = ''
                        if ($st -and $st.State -in @('RUNNING', 'NEEDS YOU')) {
                            $act = Get-AutopilotActivity -Status $st -Settings $gset -Now $Now
                            $quiet = ($act.Quiet -or $act.Stale)
                            if ($act.QuietFor) { $since = Format-ShortAge $act.QuietFor } elseif ($act.StaleFor) { $since = Format-ShortAge $act.StaleFor }
                        } elseif ($st) { $quiet = $true }
                        if (-not $quiet) { $steps.Add((New-PlanStep -Kind refuse -Text ($running + ' is running in a CC session (the Golem''s state says so). Wait for its DONE, or close that session: once it has been quiet past stallMinutes (' + $gset.StallMinutes + ' min) this button hands ' + $running + ' to a new session.'))); break }
                        $carry = $running + ' is the running prompt but its session has been quiet' + $(if ($since) { ' for ' + $since } else { '' }) + ' (closed?): the new session is told to carry on with it, so nothing is skipped. '
                    }
                    # The fresh check from the click, else cli.json as the Golem last wrote it (a dry run, or an older console).
                    $cli = $Facts.CliCheck
                    if (-not $cli -and $Facts.CliInfo) { $cli = [pscustomobject]@{ Path = [string]$Facts.CliInfo.path; SignedIn = [bool]$Facts.CliInfo.signedIn; Note = [string]$Facts.CliInfo.note } }
                    if (-not $cli) { $cli = [pscustomobject]@{ Path = ''; SignedIn = $null; Note = 'not checked yet' } }
                    if (-not $cli.Path) { $steps.Add((New-PlanStep -Kind refuse -Text 'The Claude Code CLI is not installed on this PC. In PowerShell run the official installer: irm https://claude.ai/install.ps1 | iex, then sign in (claude auth login) and press Check CLI sign-in.')); break }
                    if ($cli.SignedIn -ne $true) { $steps.Add((New-PlanStep -Kind refuse -Text ('The Claude Code CLI (' + $cli.Path + ') is not signed in' + $(if ($cli.Note -and $cli.Note -ne 'not signed in') { ' (' + $cli.Note + ')' } else { '' }) + ': open a terminal, run claude auth login and follow the browser prompts, then press Check CLI sign-in.'))); break }
                    $model = ([string]$Facts.WakeModel).Trim()
                    $auto = (-not $model -or $model -eq 'Auto')
                    if ($auto) { $model = '' }
                    $nextP = ''
                    $first = @(@($Facts.Extra.PromptTexts.Keys) | Where-Object { $_ -eq $q.Runs[0] })
                    $pi = Get-PromptInfo $q.Runs[0] $(if ($Facts.Extra -and $Facts.Extra.PromptTexts -and $Facts.Extra.PromptTexts.Contains($q.Runs[0])) { $Facts.Extra.PromptTexts[$q.Runs[0]] } else { $null })
                    if ($pi.Number) { $nextP = 'P' + $pi.Number }
                    if (-not $model -and $pi.Model) { $model = $pi.Model }
                    if (-not $model) { $model = 'Opus' }
                    $model = $model.Substring(0, 1).ToUpperInvariant() + $model.Substring(1).ToLowerInvariant()
                    if ($model -notin @('Fable', 'Opus', 'Sonnet')) { $steps.Add((New-PlanStep -Kind refuse -Text ("The model is Fable or Opus: '" + $Facts.WakeModel + "'."))); break }
                    $for = $(if ($running) { $running } else { $nextP })
                    $how = $(if ($auto) { 'Auto: ' + $model + ', for ' + $for + ' (its Model line' + $(if (-not $pi.Model) { ' is missing, so Opus' } else { '' }) + '; the Golem reads it again as it wakes). ' } else { 'Override: ' + $model + ' (the prompt''s Model line is ' + $(if ($pi.Model) { $pi.Model } else { 'missing, Opus' }) + '). ' })
                    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ($how + $carry + 'Opens a terminal window on this PC running the Claude Code CLI in D:\UO\shard-migration: claude --model ' + $model.ToLowerInvariant() + ' --permission-mode auto --name "Golem ' + $for + '" "Golem, please work on the shard tasks." The session says the phrase itself and the Golem hands it ' + $for + '. Closing that window ends the session; a prompt mid-run is handed again at the next wake, never skipped.')))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.WakeScript + @('-Model', $(if ($auto) { 'Auto' } else { $model }))) -Text 'In a window of its own, which stays open and prints the command line it ran.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.resume'))
                }
                'golem.autowake' {
                    if (-not (& $need $ap.AutoWakeScript 'Set-AutoWake.ps1')) { break }
                    if ($gset.AutoWake) {
                        $steps.Add((New-PlanStep -Kind say -Text 'Self-wake off: the Golem will rest and email after a context rest, a model wait or the run cap; you open the next session (the phrase, or Wake the Golem here).'))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.AutoWakeScript + @('-Off')) -Text 'In a window of its own, which stays open.'))
                    } else {
                        $steps.Add((New-PlanStep -Kind say -Text 'Self-wake on: after a context rest, a model wait or the run cap the Golem opens the next CC session itself (never after a STOPPED or NEEDS YOU, never in the bot window, never past a long run''s end, never when usage is at the limit or unknown; one per 5 minutes, 20 a day, in settings.json).'))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.AutoWakeScript + @('-On')) -Text 'In a window of its own, which stays open.'))
                    }
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.autowake'))
                }
                'golem.nightshift' {
                    if (-not (& $need $ap.NightShiftScript 'Set-NightShift.ps1')) { break }
                    if ($gset.NightShiftEnabled) {
                        $steps.Add((New-PlanStep -Kind say -Text 'Night shift off: the scheduled task "Golem Night Shift" is removed; nothing starts at night by itself.'))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.NightShiftScript + @('-Off')) -Text 'In a window of its own, which stays open.'))
                    } else {
                        $nStart = ([string]$Facts.NightStart).Trim(); $nEnd = ([string]$Facts.NightEnd).Trim()
                        if (-not $nStart) { $nStart = $gset.NightShiftStart }; if (-not $nEnd) { $nEnd = $gset.NightShiftEnd }
                        foreach ($x in @($nStart, $nEnd)) { if ($x -notmatch '^\d{1,2}:\d{2}$') { $steps.Add((New-PlanStep -Kind refuse -Text ("A night shift time is HH:mm, like 22:30: '" + $x + "'."))); break } }
                        if (@($steps | Where-Object { $_.Kind -eq 'refuse' }).Count) { break }
                        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('Night shift on: a scheduled task ("Golem Night Shift", daily at ' + $nStart + ', while you are logged on) wakes the Golem for a long run until ' + $nEnd + ', only when the queue has prompts, nothing is running or STOPPED, the CLI is signed in, and the usage leaves room (session window under 50%, the week at or under its even pace minus 10%). It emails whether it started or why not, and a summary when it ends.')))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.NightShiftScript + @('-On', '-Start', $nStart, '-End', $nEnd)) -Text 'In a window of its own, which stays open.'))
                    }
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.nightshift'))
                }
                'golem.marathon' {
                    # cc-P200: the pick (auto, on, off) to settings.json marathon, through Set-Marathon.ps1 (golem.ps1 -Event
                    # MarathonSet, which switches at once and emails a change). Refused when it is already that mode.
                    if (-not (& $need $ap.MarathonScript 'Set-Marathon.ps1')) { break }
                    $mm = ([string]$Facts.MarathonMode).Trim().ToLowerInvariant()
                    if ($mm -notin @('auto', 'on', 'off')) { $steps.Add((New-PlanStep -Kind refuse -Text ("Marathon is auto, on or off, not '" + [string]$Facts.MarathonMode + "'. Nothing was run."))); break }
                    if ($mm -eq [string]$gset.Marathon) { $steps.Add((New-PlanStep -Kind refuse -Text ('Marathon is already ' + $mm + '. Nothing was run.'))); break }
                    $what = switch ($mm) {
                        'on'   { 'Marathon on now, whatever the projection says: every awake Golem is woken on its lane when it has a prompt, no rest after a Large prompt, no run cap or daily wake cap, the week runs to usageLimitPct without the even pace, the night shift ignores its reserves, and the extra PCs may claim in the day too (their back-off still holds). The 5-hour window still holds. Chase is emailed that it is on.' }
                        'off'  { 'Marathon off: the Golems pace themselves as before (paceMode, maxPerRun, their hours), whatever the projection says. Emailed if it was on.' }
                        default { 'Marathon auto (the default): on when the week''s projection falls short of 95% with more than 6 hours left, off at 95% or at usageLimitPct; each switch is emailed once.' }
                    }
                    $steps.Add((New-PlanStep -Kind confirm -Phrase ('marathon ' + $mm) -Text ('Now ' + [string]$gset.Marathon + '. ' + $what)))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.MarathonScript + @('-Mode', $mm)) -Text 'In a window of its own, which stays open and prints the week''s projection.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.marathon'))
                }
                # cc-P182: Poke, Run next and Retry stopped prompt (also the web console's buttons, through Get-WebActionPlan).
                'golem.integrate' {
                    # cc-P184: the integrator's merge round, now. The script says plainly when there is nothing to merge.
                    if (-not (& $need $ap.IntegrateScript 'Integrate.ps1')) { break }
                    $steps.Add((New-PlanStep -Kind say -Text 'Merge branches now: the integrator merges every finished Golem branch (merge-queue) into main, one at a time: a build when the server changed, then Commit-ShardWork.ps1 with every check it has, then a push. A conflict or a red build leaves main as it is and writes a fix-up prompt for the Golem that made the branch. Nothing happens while a prompt runs on the integrator, while its trees are dirty, or while multiGolem is off.'))
                    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text 'This writes main in both public repos (merge commits, pushed).'))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.IntegrateScript) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.integrate'))
                }
                { $_ -in @('golem.away', 'golem.awayoff') } {
                    # cc-P149: away mode until the Until box's time (the long run's), or off.
                    if (-not (& $need $ap.AwayScript 'Set-Away.ps1')) { break }
                    if ($Action -eq 'golem.awayoff') {
                        $steps.Add((New-PlanStep -Kind say -Text 'Away mode off: the Golems go back to their usual pace and rests. Parked prompts stay parked; the Golem tab lists them.'))
                        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text 'Away mode ends now.'))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.AwayScript + @('-Off')) -Text 'In a window of its own, which stays open.'))
                    } else {
                        $until = [datetime]::MinValue
                        if (-not [datetime]::TryParse(([string]$Facts.LongRunUntil).Trim(), $script:Invariant, [Globalization.DateTimeStyles]::None, [ref]$until)) { $steps.Add((New-PlanStep -Kind refuse -Text ("Away until when? Put a date and time in the Until box, like 2026-10-25 18:00: '" + $Facts.LongRunUntil + "'."))); break }
                        if ($until -le [datetime]::Now -or $until -gt [datetime]::Now.AddDays(14)) { $steps.Add((New-PlanStep -Kind refuse -Text ('Away until ' + $until.ToString('yyyy-MM-dd HH:mm', $script:Invariant) + ' is not in the next 14 days.'))); break }
                        $steps.Add((New-PlanStep -Kind say -Text ('Away mode until ' + $until.ToString('ddd yyyy-MM-dd HH:mm', $script:Invariant) + ': the Golems work around the clock within the night shift''s usage rule (never ahead of the week''s pace), park a prompt that stops for a decision and go on with the next one that does not depend on it, and email a digest each morning.')))
                        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text 'Away mode stays on until then, or until Away mode off.'))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.AwayScript + @('-Until', $until.ToString('yyyy-MM-dd HH:mm', $script:Invariant))) -Text 'In a window of its own, which stays open.'))
                    }
                    $steps.Add((New-PlanStep -Kind log -Text $Action))
                }
                { $_ -in @('golem.tinkerrun', 'golem.tinkerpause', 'golem.tinkerresume') } {
                    # cc-P222: the Tinker's round now, its pause (PAUSED-Tinker) and resume.
                    if (-not (& $need $ap.TinkerScript 'Tinker.ps1')) { break }
                    $arg = $(switch ($Action) { 'golem.tinkerpause' { @('-Pause') } 'golem.tinkerresume' { @('-Resume') } default { @() } })
                    $steps.Add((New-PlanStep -Kind say -Text $(switch ($Action) { 'golem.tinkerpause' { 'Pause the Tinker: no round runs until Resume the Tinker. Nothing else changes.' } 'golem.tinkerresume' { 'Resume the Tinker: one round now, then every 10 minutes again.' } default { 'Tinker: one round now: every PC''s kit in step with this one, every Golem looked at; a known stall is fixed (a WIP commit on the Golem''s own branch, a flag cleared, a retry or a wake), anything else becomes a question on its card.' } })))
                    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text $(if ($Action -eq 'golem.tinkerpause') { 'The Tinker stops looking until you resume it.' } else { 'It never pushes main, deletes work or resets a branch.' })))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.TinkerScript + $arg) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text $Action))
                }
                { $_ -in @('golem.mergepause', 'golem.mergeresume') } {
                    # cc-P208: Shadow Iron's pause (PAUSED-ShadowIron) and resume; the script says when mergeGolem is off.
                    if (-not (& $need $ap.MergeScript 'ShadowIron.ps1')) { break }
                    $pause = ($Action -eq 'golem.mergepause')
                    $steps.Add((New-PlanStep -Kind say -Text $(if ($pause) { 'Pause merging: Shadow Iron, the merge Golem, runs no round until Resume merging. Finished branches wait in merge-queue; nothing else changes.' } else { 'Resume merging: Shadow Iron runs one round now (it merges what waits, oldest first) and then every 10 minutes again.' })))
                    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text $(if ($pause) { 'Merging stops until you resume it.' } else { 'This may write main in both public repos (merge commits, pushed).' })))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.MergeScript + @($(if ($pause) { '-Pause' } else { '-Resume' }))) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text $Action))
                }
                'golem.lanenow' {
                    # cc-P159: only for a Codex lane picked on the Golem tab (or the web console's lane card): Add-GolemToPlan
                    # tells Lane-Now.ps1 which one.
                    if (-not ($ap.PSObject.Properties.Name -contains 'GolemName' -and [string]$ap.GolemName)) { $steps.Add((New-PlanStep -Kind refuse -Text 'Send to the second PC is a Codex lane''s: pick a Codex lane (Valorite, Verite) on the Golem tab first. Nothing was run.')); break }
                    if (-not (& $need $ap.LaneNowScript 'Lane-Now.ps1')) { break }
                    $steps.Add((New-PlanStep -Kind say -Text ('Send to the second PC: the Codex lane ' + [string]$ap.GolemName + ' may start now, outside its night window, for the next few hours (laneRunNowHours). If its PC is in use or the Codex budget is spent, it waits for that first. A lane on another PC gets it through that PC''s agent (cc-P373).')))
                    $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text 'The lane may run a prompt by day.'))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.LaneNowScript) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.lanenow'))
                }
                'golem.poke' {
                    if (-not (& $need $ap.PokeScript 'Poke-Golem.ps1')) { break }
                    $steps.Add((New-PlanStep -Kind say -Text 'Check now: the Golem''s 10-minute watch runs now, so it looks again at once (a stall, a rest whose time has come, a pending wake, its finished sessions). It changes nothing the watch would not change by itself.'))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.PokeScript) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.poke'))
                }
                'golem.runnext' {
                    if (-not (& $need $ap.RunNextScript 'Set-RunNext.ps1')) { break }
                    $want = ([string]$Facts.RunNextPrompt).Trim()
                    if ($want -ieq 'cancel') {
                        if (-not [string]$Facts.RunNext) { $steps.Add((New-PlanStep -Kind refuse -Text 'Nothing is picked to go next: pick a prompt in the queue list first.')); break }
                        $steps.Add((New-PlanStep -Kind say -Text ('Clear Run next: ' + $Facts.RunNext + ' goes back to its own place in the queue.')))
                        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.RunNextScript + @('-Cancel')) -Text 'In a window of its own, which stays open.'))
                        $steps.Add((New-PlanStep -Kind log -Text 'golem.runnext'))
                        break
                    }
                    if ($want -notmatch '^(?i)P?(\d+)$') { $steps.Add((New-PlanStep -Kind refuse -Text ("Pick a queued prompt (P<nn>) first: '" + $want + "'."))); break }
                    $num = [int]$Matches[1]
                    $q = $(if ($null -ne $Facts.QueueNames) { Get-AutopilotQueue @($Facts.QueueNames) -RunNext ([string]$Facts.RunNext) } else { $null })
                    $file = @(@($(if ($q) { $q.Runs } else { @() })) | Where-Object { $_ -match ('^cc-P' + $num + '-') }) | Select-Object -First 1
                    if (-not $file) { $steps.Add((New-PlanStep -Kind refuse -Text ('P' + $num + ' is not in the queue (' + $ap.Queue + '): only a queued prompt can go next; the running one and finished ones cannot.'))); break }
                    $order = @(@($file) + @($q.Runs | Where-Object { $_ -ne $file }) | Select-Object -First 6 | ForEach-Object { 'P' + [regex]::Match($_, '^cc-P(\d+)-').Groups[1].Value })
                    $steps.Add((New-PlanStep -Kind confirm -Phrase ('P' + $num) -Text ('Run next: P' + $num + ' is handed at the next hand-off' + $(if ($running) { ' (after ' + $running + ')' } else { '' }) + ', ahead of continuations. The new order: ' + ($order -join ', then ') + $(if (@($q.Runs).Count -gt 6) { ', ...' } else { '' }) + '. Nothing is renamed or renumbered: the Golem keeps the pick in RUN-NEXT and uses it once.')))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.RunNextScript + @('-Prompt', ('P' + $num))) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.runnext'))
                }
                'golem.skipday' {
                    # cc-P287: Owner works <date>: the date goes into the Golem's days.skip in golems.json (Skip-Day.ps1, kept on
                    # this PC; the Tinker copies it out), so on that date it keeps to its hours instead of working all day.
                    # The Golem: the web console's card (SkipGolem) or the one picked on the Golem tab; the date: the web
                    # console's box (SkipDate) or the Until box's date.
                    if (-not (& $need $ap.SkipDayScript 'Skip-Day.ps1')) { break }
                    $sg = $(if ([string]$Facts.SkipGolem) { [string]$Facts.SkipGolem } elseif ($ap.PSObject.Properties.Name -contains 'GolemName' -and [string]$ap.GolemName) { [string]$ap.GolemName } else { '' })
                    if ($sg -notmatch '^[A-Za-z][A-Za-z0-9]{1,23}$') { $steps.Add((New-PlanStep -Kind refuse -Text 'Owner works is for one Golem with all-day weekdays (Stone): pick it on the Golem tab, or use its card on the web console. Nothing was run.')); break }
                    $sd = ([string]$Facts.SkipDate).Trim()
                    if (-not $sd) { $sd = @((([string]$Facts.LongRunUntil).Trim() -split '\s+'))[0] }
                    $sdt = [datetime]::MinValue
                    if (-not [datetime]::TryParseExact($sd, 'yyyy-MM-dd', $script:Invariant, [Globalization.DateTimeStyles]::None, [ref]$sdt)) { $steps.Add((New-PlanStep -Kind refuse -Text ("Which date does the owner work? Put it in the Until box as YYYY-MM-DD (the time is not used): '" + $sd + "'."))); break }
                    if ($sdt.Date -lt [datetime]::Today) { $steps.Add((New-PlanStep -Kind refuse -Text ($sdt.ToString('yyyy-MM-dd', $script:Invariant) + ' is in the past.'))); break }
                    $sdi = $sdt.ToString('yyyy-MM-dd', $script:Invariant)
                    $steps.Add((New-PlanStep -Kind say -Text ($sg + ' keeps to its hours on ' + $sdt.ToString('dddd yyyy-MM-dd', $script:Invariant) + ' instead of working all day: the owner works that day. The date goes into its days.skip in golems.json on this PC, and the Tinker copies it to the Golem''s PC within 10 minutes. Skip-Day.ps1 says so and changes nothing when that date is not one of its all-day weekdays.')))
                    $steps.Add((New-PlanStep -Kind confirm -Phrase ('skip ' + $sg.ToLowerInvariant()) -Text 'golems.json changes (the old one is kept as golems.json.bak-<time>-skipday).'))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.SkipDayScript + @('-Golem', $sg, '-Date', $sdi, '-By', 'console')) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.skipday'))
                }
                'golem.assign' {
                    # cc-P187: Assign-Prompt.ps1 writes P184's **Golem:** line into the queued prompt (and, with -RunNext, picks
                    # it to go next for that Golem). It checks the Golem's lane, areas and the prompt's Touches itself.
                    if (-not (& $need $ap.AssignScript 'Assign-Prompt.ps1')) { break }
                    $want = ([string]$Facts.RunNextPrompt).Trim()
                    if ($want -notmatch '^(?i)P?(\d+)$') { $steps.Add((New-PlanStep -Kind refuse -Text ("Pick a queued prompt (P<nn>) to assign first: '" + $want + "'."))); break }
                    $num = [int]$Matches[1]
                    $to = ([string]$Facts.AssignGolem).Trim()
                    if ($to -notmatch '^[A-Za-z][A-Za-z0-9]{0,23}$') { $steps.Add((New-PlanStep -Kind refuse -Text 'Pick the Golem to give it to first.')); break }
                    $q = $(if ($null -ne $Facts.QueueNames) { Get-AutopilotQueue @($Facts.QueueNames) -RunNext ([string]$Facts.RunNext) } else { $null })
                    $file = @(@($(if ($q) { $q.Runs } else { @() })) | Where-Object { $_ -match ('^cc-P' + $num + '-') }) | Select-Object -First 1
                    if (-not $file) { $steps.Add((New-PlanStep -Kind refuse -Text ('P' + $num + ' is not in the queue (' + $ap.Queue + '): only a queued prompt can be assigned.'))); break }
                    $next = ([string]$Facts.AssignRunNext -eq 'yes')
                    $steps.Add((New-PlanStep -Kind confirm -Phrase ('P' + $num + ' on ' + $to.ToLowerInvariant()) -Text ('Assign: P' + $num + ' (' + $file + ') gets the line **Golem:** ' + $to + ', so only the ' + $to + ' Golem claims it. Assign-Prompt.ps1 first checks that ' + $to + ' is in golems.json and enabled, that its lane takes the prompt''s Model line and that its areas take the prompt''s Touches, and refuses with the reason otherwise.' + $(if ($next) { ' Then P' + $num + ' goes next for ' + $to + ' (Run next).' } else { '' }))))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.AssignScript + @('-Prompt', ('P' + $num), '-Golem', $to) + $(if ($next) { @('-RunNext') } else { @() })) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.assign'))
                }
                'golem.retry' {
                    if (-not (& $need $ap.RetryScript 'Retry-Stopped.ps1')) { break }
                    $sp = $(if ($st) { [string]$st.StoppedPrompt } else { '' })
                    if (-not $sp) { $steps.Add((New-PlanStep -Kind refuse -Text 'No prompt is STOPPED: there is nothing to retry.')); break }
                    if (-not $Facts.Enabled) { $steps.Add((New-PlanStep -Kind refuse -Text ('The Golem is off (no ENABLED file): wake it first, then retry ' + $sp + '.'))); break }
                    $steps.Add((New-PlanStep -Kind confirm -Phrase $sp -Text ($sp + ' goes back into the queue and goes next, stoppedPrompt is cleared, and a new CC session opens on ' + $sp + '''s own model (a terminal window on this PC). Read why it stopped first: its notes and the Golem''s STOPPED email. Its DONE is committed as usual.')))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.RetryScript + @('-Prompt', $sp)) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.retry'))
                }
                'golem.answer' {
                    # cc-P216: the question, choice and words picked (Show-AnswerPicker), checked again against the open
                    # question; the answer is written by the Golem's own Answer-Question.ps1 (Submit-GolemAnswer).
                    if (-not (& $need $ap.AnswerScript 'Answer-Question.ps1')) { break }
                    $qid = [string]$Facts.AnswerId
                    if (-not $qid) { $steps.Add((New-PlanStep -Kind refuse -Text 'Pick a question to answer first: no Golem on this PC has an open question, or the pick was cancelled.')); break }
                    $q = @(Get-OpenGolemQuestions -Root $ap.GolemRoot | Where-Object { $_.Id -ceq $qid }) | Select-Object -First 1
                    if (-not $q) { $steps.Add((New-PlanStep -Kind refuse -Text ($qid + ' is closed now (answered, or the stop it asked about was cleared): there is nothing to answer.'))); break }
                    if ($q.Remote) { $steps.Add((New-PlanStep -Kind refuse -Text ('The ' + $q.Golem + ' Golem is on another PC: answer it on the web console, which sends the answer to its agent.'))); break }
                    $ch = -1; try { $ch = [int]$Facts.AnswerChoice } catch { }
                    $words = ([string]$Facts.AnswerText).Trim()
                    if ($ch -lt -1 -or $ch -ge @($q.Choices).Count) { $steps.Add((New-PlanStep -Kind refuse -Text 'That is not one of its choices.')); break }
                    if ($words -and -not $q.FreeText) { $steps.Add((New-PlanStep -Kind refuse -Text 'This question takes one of its choices, not your own words.')); break }
                    if ($words.Length -gt 2000) { $steps.Add((New-PlanStep -Kind refuse -Text 'The answer is longer than 2000 characters.')); break }
                    if ($ch -lt 0 -and -not $words) { $steps.Add((New-PlanStep -Kind refuse -Text 'Pick a choice or write an answer.')); break }
                    $said = (@($(if ($ch -ge 0) { '"' + [string]@($q.Choices)[$ch].label + '"' + $(if ([string]@($q.Choices)[$ch].does) { ' (the Golem then: ' + [string]@($q.Choices)[$ch].does + ')' } else { '' }) }), $(if ($words) { 'your words: "' + $words + '"' })) | Where-Object { $_ }) -join ' and '
                    $then = $(if ($q.Kind -eq 'needsyou') { 'NEEDS YOU is cleared and a new session carries on with ' + $q.Prompt + ', reading the answer first.' } else { $q.Prompt + ' goes back first in the queue and a new session starts it again, reading the answer first.' })
                    $steps.Add((New-PlanStep -Kind confirm -Phrase ('answer ' + $q.Golem.ToLowerInvariant() + ' ' + $q.Prompt.ToLowerInvariant()) -Text ('The ' + $q.Golem + ' Golem asks, on ' + $q.Prompt + ': ' + $q.Question + "`r`n`r`nYour answer: " + $said + '. It goes to the Golem as data (answers\' + $qid + '.json), never run as a command. ' + $then + $(if ($q.Irreversible) { "`r`n`r`nCANNOT BE UNDONE: " + $q.IrreversibleWhy + '.' } else { '' }))))
                    $who = ([Environment]::UserName -replace '[^A-Za-z0-9@._+ -]', '').TrimStart('-', '.', ' ')
                    $aa = @('-Id', $qid, '-Choice', [string]$ch, '-Who', $(if ($who) { $who } else { 'shard-console' }), '-Source', 'shard-console')
                    if ($words) { $aa += @('-TextB64', [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($words))) }
                    if ($q.Golem -cne 'Golem') { $aa += @('-Golem', $q.Golem) }
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.AnswerScript + $aa) -Text 'In a window of its own, which stays open.'))
                    $steps.Add((New-PlanStep -Kind log -Text 'golem.answer'))
                }
                'golem.clicheck' {
                    if (-not (& $need $ap.CliCheckScript 'Check-Cli.ps1')) { break }
                    $steps.Add((New-PlanStep -Kind say -Text 'Check CLI sign-in: runs claude auth status and records the answer in cli.json for the Golem and this tab. Changes nothing else.'))
                    $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.CliCheckScript) -Text 'In a window of its own, which stays open.'))
                }
                'golem.openprompt' {
                    $f = [string]$Facts.OpenFile
                    if (-not $f) { $steps.Add((New-PlanStep -Kind refuse -Text 'Pick a prompt in the queue list first.')); break }
                    $steps.Add((New-PlanStep -Kind launch -Path $ap.Notepad -Arguments @($f) -Text ('Opening ' + $f + ' in Notepad. Read only here: nothing in the console edits, reorders or deletes a prompt.')))
                }
                'golem.opennotes' {
                    $f = [string]$Facts.OpenFile
                    if (-not $f) { $steps.Add((New-PlanStep -Kind refuse -Text 'Pick a finished prompt that has notes first.')); break }
                    $steps.Add((New-PlanStep -Kind launch -Path $ap.Notepad -Arguments @($f) -Text ('Opening ' + $f + ' in Notepad.')))
                }
            }
        }
        'golem.move' {
            # cc-P99. The cutover. Its own script refuses while a prompt runs; it is asked here first so the phrase is not wasted.
            if ($ap.Kind -eq 'golem') { $steps.Add((New-PlanStep -Kind refuse -Text ('The Golem is already active (' + $ap.GolemActiveFile + '). To go back: ' + $ap.MoveScript + ' -Rollback, in a PowerShell window.'))); break }
            if (-not (& $need $ap.MoveScript 'Move-ToGolem.ps1')) { break }
            if ($Facts.Running) { $steps.Add((New-PlanStep -Kind refuse -Text ('A prompt is running (' + $Facts.Running + '): the move waits until it has said DONE and been committed, or STOPPED. Nothing was run.'))); break }
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This moves the CC queue from cc-autopilot to the Golem: it rests cc-autopilot, points the CC hooks in both repos at ' + (Join-Path $ap.GolemRoot 'golem.ps1') + ', replaces the scheduled task "Shard CC Autopilot Watch" with "Golem Watch", copies the state and wakes the Golem. Move-ToGolem.ps1 then prints its dry run in its own window and asks you to type move before it changes anything. Rollback: Move-ToGolem.ps1 -Rollback.')))
            $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $ap.MoveScript) -Text 'In a window of its own, which stays open. Start a new CC session afterwards: hooks load at start.'))
            $steps.Add((New-PlanStep -Kind log -Text 'golem.move'))
        }
        default { $steps.Add((New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'. -Action list shows them."))) }
    }
    $steps.ToArray()
}

# --- the play-test queue (cc-P83) ---------------------------------------------------------------
# D:\UO\shard-migration\testing\play-tests.json, written by sl-play-tests (docker/test-queue/server.py)
# and tools\Import-PlayTests.ps1 through the same tools\playtests.py. Statuses: open, later and blocked
# are waiting; pass, fail, moot are settled. A Fail with an empty "bug" has not been logged in the bug
# list yet, which is the overseer's job: that is what turns the tile amber.

function Get-PlayTestGlance {
    # The PLAY TESTS tile. Pure: the file's text in ($null when there is no file), a tile out. The counts
    # come from the items, not from the summary block, so a hand-edited file cannot disagree with itself.
    # cc-P90: -IdeasText is feature-ideas.json beside it; its 'new' ideas are added to the detail, never to the word.
    # cc-P97: -BugsText is bug-reports.json, added the same way after the ideas.
    param($JsonText, [string]$ReadError, $IdeasText, $BugsText)
    $t = [ordered]@{ Title = 'PLAY TESTS'; Word = 'UNKNOWN'; Color = 'gray'; Detail = '' }
    if ($ReadError) { $t.Detail = 'play-tests.json could not be read (' + $ReadError + ')'; return [pscustomobject]$t }
    if ($null -eq $JsonText) { $t.Detail = 'no play-tests.json yet: start sl-play-tests or run Import-PlayTests.ps1'; return [pscustomobject]$t }
    try {
        $doc = ([string]$JsonText) | ConvertFrom-Json
        if ($null -eq $doc -or $doc.schema -ne 1 -or $null -eq $doc.items) { throw 'not schema 1' }
    } catch {
        $t.Detail = 'play-tests.json is not readable (' + $_.Exception.Message + ')'
        return [pscustomobject]$t
    }
    $items = @($doc.items)
    $open = @($items | Where-Object { $_.status -in @('open', 'later', 'blocked') }).Count
    $fails = @($items | Where-Object { $_.status -eq 'fail' })
    $unlogged = @($fails | Where-Object { -not [string]$_.bug }).Count
    $passed = @($items | Where-Object { $_.status -eq 'pass' }).Count
    $t.Word = [string]$open + ' OPEN'
    $t.Detail = [string]$open + ' open, ' + $fails.Count + ' failed'
    if ($unlogged) { $t.Detail += ' (' + $unlogged + ' not logged)' }
    $t.Detail += ', ' + $passed + ' passed'
    $newIdeas = Get-NewIdeaCount $IdeasText
    if ($newIdeas) { $t.Detail += '; ' + $newIdeas + ' new idea' + $(if ($newIdeas -eq 1) { '' } else { 's' }) }
    $newBugs = Get-NewBugCount $BugsText
    if ($newBugs) { $t.Detail += '; ' + $newBugs + ' new bug' + $(if ($newBugs -eq 1) { '' } else { 's' }) }
    if ($unlogged) { $t.Color = 'amber'; $t.Word = [string]$unlogged + ' FAIL NOT LOGGED' }
    elseif ($open -eq 0) { $t.Color = 'green'; $t.Word = 'ALL TESTED' }
    [pscustomobject]$t
}

function Get-NewIdeaCount {
    # cc-P90: how many ideas in feature-ideas.json are still 'new'. 0 for no file or a file that cannot be read: the
    # count is a hint on the tile, never a reason to call the tile unknown.
    param($JsonText)
    if ($null -eq $JsonText) { return 0 }
    try {
        $doc = ([string]$JsonText) | ConvertFrom-Json
        if ($null -eq $doc -or $doc.schema -ne 1) { return 0 }
        @($doc.ideas | Where-Object { $_.status -eq 'new' }).Count
    } catch { 0 }
}

function Get-NewBugCount {
    # cc-P97: how many reports in bug-reports.json are still 'new'. 0 for no file or one that cannot be read, as above.
    param($JsonText)
    if ($null -eq $JsonText) { return 0 }
    try {
        $doc = ([string]$JsonText) | ConvertFrom-Json
        if ($null -eq $doc -or $doc.schema -ne 1) { return 0 }
        @($doc.bugs | Where-Object { $_.status -eq 'new' }).Count
    } catch { 0 }
}

function Get-LinkPlan {
    # Play tests and Dashboard: open the page in the default browser. Read nothing, change nothing.
    param([string]$Action, $Config)
    switch ($Action) {
        'links.playtests' { $l = Get-OpsLinks $Config (Get-OpsMode $Config); @(New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($l.PlayTests) -Text ('Opening the play-test page (on ' + $l.Where + ') in the default browser.' + $(if ($l.Fallback) { ' If Haven does not answer, the rollback (Websites tab, ops-rollback) brings back ' + $l.Fallback + '.' } else { '' }))) }
        'links.dashboard' { @(New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($Config.DashboardUrl) -Text 'Opening the Homepage dashboard on Haven (port 3000) in the default browser.') }
        'links.status'    { $l = Get-OpsLinks $Config (Get-OpsMode $Config); @(New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($l.Status) -Text ('Opening the shard status page (on ' + $l.Where + ', /status) in the default browser.')) }
        default { @(New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'. -Action list shows them.")) }
    }
}

# --- the websites (cc-P101) -----------------------------------------------------------------------
# scripts\websites.json lists every site the Websites tab and the WEBSITES tile show: where it runs (the game PC or
# Haven), its URL, the URL its light checks if that differs, and for a game-PC site its container and start script.
# "deploys" is the allowlist of Haven deploy scripts the Deploy button may run (Invoke-WebsiteDeploy.ps1 checks it
# again). A site the file gets wrong is left out and named in Problems, never guessed. Nothing in the file is secret.

$script:WebsiteKeyPattern = '^[a-z0-9][a-z0-9-]{0,39}$'
$script:WebsiteTimeoutSec = 4

function ConvertFrom-WebsiteConfig {
    # websites.json's text ($null: no file) in; the sites, the deploy allowlist, Haven's SSH target, NPM's tunnel and
    # every problem out. Pure: $RepoRoot only turns a game-PC site's relative start script into a full path.
    param($Text, [string]$RepoRoot = '.')
    $problems = New-Object 'System.Collections.Generic.List[string]'
    $sites = New-Object 'System.Collections.Generic.List[object]'
    $deploys = [ordered]@{}
    $out = { param($ssh, $npm) [pscustomobject]@{ Sites = $sites.ToArray(); Deploys = $deploys; HavenSsh = $ssh; Npm = $npm; Problems = $problems.ToArray() } }
    if ($null -eq $Text) { $problems.Add('there is no websites.json'); return (& $out $null $null) }
    try { $j = ([string]$Text) | ConvertFrom-Json } catch { $problems.Add('websites.json is not JSON: ' + $_.Exception.Message); return (& $out $null $null) }
    if ($null -eq $j -or [string]$j.schema -ne '1') { $problems.Add('websites.json is not schema 1'); return (& $out $null $null) }
    $str = { param($o, $n) if ($null -ne $o -and $o.PSObject.Properties.Name -contains $n -and $null -ne $o.$n) { ([string]$o.$n).Trim() } else { '' } }

    $ssh = & $str $j.haven 'ssh'
    if ($ssh -and $ssh -notmatch '^[a-z0-9._-]+@[A-Za-z0-9.-]+$') { $problems.Add('haven.ssh is not user@host: ' + $ssh); $ssh = '' }
    if (-not $ssh) { $problems.Add('haven.ssh is missing: Open NPM needs it for the tunnel') }

    $npm = $null
    $nu = & $str $j.npm 'url'; $np = & $str $j.npm 'tunnelLocalPort'; $nr = & $str $j.npm 'tunnelRemote'
    $port = 0
    if (-not $nu -or $nu -notmatch '^https?://' -or -not [int]::TryParse($np, [ref]$port) -or $port -lt 1024 -or $port -gt 65535 -or $nr -notmatch '^[A-Za-z0-9.-]+:\d{1,5}$') {
        $problems.Add('npm needs url (http or https), tunnelLocalPort (1024 to 65535) and tunnelRemote (host:port)')
    } else { $npm = [pscustomobject]@{ Url = $nu; LocalPort = $port; Remote = $nr } }

    if ($j.deploys) {
        foreach ($p in $j.deploys.PSObject.Properties) {
            $sc = & $str $p.Value 'script'
            if ($p.Name -notmatch $script:WebsiteKeyPattern) { $problems.Add('deploy ' + $p.Name + ': the name must be lower-case letters, digits and -') }
            elseif ($sc -notmatch '(?i)^[A-Z]:\\.+\.ps1$') { $problems.Add('deploy ' + $p.Name + ': script must be a full path to a .ps1') }
            else { $deploys[$p.Name] = [pscustomobject]@{ Name = $p.Name; Script = $sc; What = (& $str $p.Value 'what') } }
        }
    }

    $seen = @{}
    $i = 0
    foreach ($s in @($j.sites)) {
        $i++
        $key = & $str $s 'key'; $name = & $str $s 'name'; $url = & $str $s 'url'; $where = & $str $s 'where'
        $why = @()
        if ($key -notmatch $script:WebsiteKeyPattern) { $why += 'key' }
        elseif ($seen.ContainsKey($key)) { $why += 'key (used twice)' }
        if (-not $name) { $why += 'name' }
        if ($url -notmatch '^https?://') { $why += 'url' }
        if ($where -cnotin @('pc', 'haven')) { $why += 'where (pc or haven)' }
        $check = & $str $s 'check'; if (-not $check) { $check = $url } elseif ($check -notmatch '^https?://') { $why += 'check' }
        $container = & $str $s 'container'; $start = & $str $s 'start'; $deploy = & $str $s 'deploy'
        if ($where -ceq 'pc') {
            if ($container -notmatch '^sl-[a-z0-9-]+$') { $why += 'container (sl-...)' }
            if ($start -notmatch '(?i)^[^:]+\.ps1$') { $why += 'start (a .ps1 in the repo)' }
        }
        if ($deploy -and -not $deploys.Contains($deploy)) { $why += ('deploy (' + $deploy + ' is not in deploys)') }
        if ($why.Count) { $problems.Add(('site ' + $i + ' (' + $(if ($name) { $name } elseif ($key) { $key } else { 'no name' }) + '): missing or wrong ' + ($why -join ', ') + '; left out')); continue }
        $seen[$key] = $true
        $sites.Add([pscustomobject]@{
            Key = $key; Name = $name; Url = $url; Check = $check; Where = $where; HavenUrl = (& $str $s 'havenUrl')
            Container = $container; Start = $(if ($start) { Join-Path $RepoRoot $start } else { '' })
            Deploy = $deploy; IsNpm = [bool]($s.PSObject.Properties.Name -contains 'npm' -and $s.npm -eq $true)
        })
    }
    if (-not $sites.Count) { $problems.Add('no usable site in websites.json') }
    & $out $ssh $npm
}

function Get-WebsiteState {
    # One check's result in, up / down / timeout / unknown out. Any 2xx or 3xx is up: a redirect is a site answering.
    param($Result)
    if ($null -eq $Result) { return 'unknown' }
    if ($Result.TimedOut) { return 'timeout' }
    if ([int]$Result.Code -ge 200 -and [int]$Result.Code -lt 400) { return 'up' }
    'down'
}

function Format-WebsiteResult {
    param($Result)
    switch (Get-WebsiteState $Result) {
        'unknown' { 'not checked yet' }
        'timeout' { 'no answer within ' + $script:WebsiteTimeoutSec + ' s' }
        default {
            if ([int]$Result.Code -gt 0) { 'HTTP ' + $Result.Code + ' in ' + [int]$Result.Ms + ' ms' }
            else { 'no answer: ' + $Result.Error }
        }
    }
}

function Get-WebsitesGlance {
    # The WEBSITES tile. $Results: site key to check result, or $null before the first check finished.
    param($Web, $Results)
    $t = [ordered]@{ Title = 'WEBSITES'; Word = 'UNKNOWN'; Color = 'gray'; Detail = '' }
    $sites = @($Web.Sites)
    $prob = @($Web.Problems)
    $probNote = $(if ($prob.Count) { 'websites.json: ' + $prob.Count + ' problem(s), ' + $prob[0] } else { '' })
    if (-not $sites.Count) { $t.Detail = $probNote; return [pscustomobject]$t }
    if ($null -eq $Results) { $t.Word = 'CHECKING'; $t.Detail = [string]$sites.Count + ' sites, first check running'; return [pscustomobject]$t }
    $up = @($sites | Where-Object { (Get-WebsiteState $Results[$_.Key]) -eq 'up' })
    $down = @($sites | Where-Object { (Get-WebsiteState $Results[$_.Key]) -ne 'up' })
    $t.Word = [string]$up.Count + ' OF ' + $sites.Count + ' UP'
    if ($down.Count) {
        $t.Color = 'amber'
        $t.Detail = 'down: ' + (($down | ForEach-Object { $_.Name + ' (' + (Format-WebsiteResult $Results[$_.Key]) + ')' }) -join ', ')
    } else {
        $t.Color = 'green'
        $t.Detail = 'all answering'
    }
    if ($probNote) { $t.Color = 'amber'; $t.Detail += '; ' + $probNote }
    [pscustomobject]$t
}

function Format-WebsitesPanel {
    # The Websites tab's read-only panel, as lines with a colour name like Format-StatusReport's.
    param($Web, $Results, [string]$CheckedAt)
    $o = New-Object 'System.Collections.Generic.List[object]'
    $add = { param($t, $c) $o.Add([pscustomobject]@{ Text = $t; Color = $(if ($c) { $c } else { 'normal' }) }) }
    $tile = Get-WebsitesGlance $Web $Results
    & $add ('WEBSITES : ' + $tile.Word + '. ' + $tile.Detail) $tile.Color
    if ($CheckedAt) { & $add ('checked  : ' + $CheckedAt + ', every 60 s (Check now to check again)') 'gray' }
    & $add '' 'normal'
    foreach ($s in @($Web.Sites)) {
        $r = $null; if ($Results) { $r = $Results[$s.Key] }
        $st = Get-WebsiteState $r
        $mark = @{ up = '[UP]     '; down = '[DOWN]   '; timeout = '[NO ANS] '; unknown = '[ ? ]    ' }[$st]
        $where = $(if ($s.Where -ceq 'pc') { 'game PC, ' + $s.Container } else { 'Haven' })
        $line = $mark + $s.Name.PadRight(20) + ' ' + $where.PadRight(24) + ' ' + $s.Url
        if ($s.Check -ne $s.Url) { $line += ' (checked at ' + $s.Check + ')' }
        $line += '  ' + (Format-WebsiteResult $r)
        if ($s.Deploy) { $line += '  deploy: ' + $s.Deploy }
        & $add $line $(if ($st -eq 'up') { 'green' } elseif ($st -eq 'unknown') { 'gray' } else { 'amber' })
    }
    & $add '' 'normal'
    if ($Web.Npm) { & $add ('NPM      : ' + $Web.Npm.Url + ' through ssh -N -L ' + $Web.Npm.LocalPort + ':' + $Web.Npm.Remote + ' ' + $Web.HavenSsh + ' (Open NPM opens both; your own key)') 'normal' }
    if (@($Web.Deploys.Keys).Count) {
        & $add 'deploys  : the allowlist, from websites.json; each runs only after the phrase "deploy to haven"' 'normal'
        foreach ($d in $Web.Deploys.Values) { & $add ('           ' + $d.Name + ': ' + $d.Script + ' (' + $d.What + ')') 'normal' }
    } else { & $add 'deploys  : none in websites.json, so Deploy refuses every site' 'gray' }
    foreach ($p in @($Web.Problems)) { & $add ('problem  : ' + $p) 'amber' }
    $o.ToArray()
}

function Get-WebsitePlan {
    # The Websites tab's buttons, for one site by key. Open and Open NPM only open; Restart, Redeploy and Logs are
    # for game-PC sites, each in a window of its own; Deploy is for a Haven site with an allowlisted deploy, after
    # the phrase, through Invoke-WebsiteDeploy.ps1 in a window of its own.
    param([string]$Action, $Config, [string]$Site)
    $web = $Config.Websites
    $steps = New-Object 'System.Collections.Generic.List[object]'
    $ps = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File')
    if ($Action -eq 'web.npm') {
        if (-not $web.Npm -or -not $web.HavenSsh) { $steps.Add((New-PlanStep -Kind refuse -Text ('websites.json has no usable npm and haven.ssh: ' + ((@($web.Problems) | Where-Object { $_ -match 'npm|haven' }) -join '; ')))); return $steps.ToArray() }
        $steps.Add((New-PlanStep -Kind say -Text ('Open NPM: its admin page listens only on Haven itself, so an SSH tunnel with your own key carries it to ' + $web.Npm.Url + '. Leave the tunnel window open while you use NPM; close it when done. Log in with NPM''s own account.')))
        $steps.Add((New-PlanStep -Kind window -Exe 'ssh' -Arguments @('-N', '-L', ([string]$web.Npm.LocalPort + ':' + $web.Npm.Remote), $web.HavenSsh) -Text 'The tunnel, in a window of its own. If it asks for a passphrase, that is your SSH key''s, in that window.'))
        $steps.Add((New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($web.Npm.Url) -Text ('Opening ' + $web.Npm.Url + ' in the default browser. Refresh once the tunnel has connected.')))
        return $steps.ToArray()
    }
    # cc-P107: the ops pages' three deploys, whatever site is picked: the same helper, allowlist and phrase as Deploy.
    # cc-P109: and the print queue's two; cc-P110: and the mega dashboard's two; cc-P163: and Portainer's two.
    if ($Action -in @('web.ops-pages', 'web.ops-cutover', 'web.ops-rollback', 'web.print-queue', 'web.print-queue-rollback', 'web.mega-dashboard', 'web.mega-dashboard-rollback', 'web.portainer-haven', 'web.portainer-haven-rollback')) {
        $d = $web.Deploys[$Action.Substring(4)]
        if (-not $d) { $steps.Add((New-PlanStep -Kind refuse -Text ($Action.Substring(4) + ' is not in the deploys allowlist in websites.json. Nothing was run.'))); return $steps.ToArray() }
        $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase 'web.deploy') -Text ('This runs ' + $d.Name + ': ' + $d.Script + ' ' + $d.What + '. Its window first reads Haven and prints the plan, and changes nothing until you type its word there. It uses your own SSH key; the window keeps a log under scripts\shard-console-output\deploys.')))
        $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + @($Config.WebsiteDeployScript, '-Deploy', $d.Name)) -Text 'In a window of its own, which stays open. It checks the allowlist again before it runs anything.'))
        $steps.Add((New-PlanStep -Kind log -Text ('web.deploy ' + $(if ($d.Name -like 'print-queue*') { 'print-queue ' } elseif ($d.Name -like 'mega-dashboard*') { 'mega-dashboard ' } elseif ($d.Name -like 'portainer-haven*') { 'portainer-haven ' } else { 'ops ' }) + $d.Name)))
        return $steps.ToArray()
    }
    $s = @($web.Sites | Where-Object { $_.Key -ceq $Site })[0]
    if (-not $s) {
        $steps.Add((New-PlanStep -Kind refuse -Text ("No site '" + $Site + "' in websites.json. Pick one on the Websites tab, or -Site with one of: " + ((@($web.Sites) | ForEach-Object { $_.Key }) -join ', ') + '.')))
        return $steps.ToArray()
    }
    $pcOnly = {
        if ($s.Where -cne 'pc') { $steps.Add((New-PlanStep -Kind refuse -Text ($s.Name + ' runs on Haven, not this PC: only Open' + $(if ($s.Deploy) { ' and Deploy' } else { '' }) + ' apply to it.'))); return $false }
        $true
    }
    switch ($Action) {
        'web.open' {
            if ($s.IsNpm) { return (Get-WebsitePlan -Action 'web.npm' -Config $Config) }
            $steps.Add((New-PlanStep -Kind launch -Path 'explorer.exe' -Arguments @($s.Url) -Text ('Opening ' + $s.Name + ' (' + $s.Url + ') in the default browser.')))
        }
        'web.restart' {
            if (-not (& $pcOnly)) { break }
            $steps.Add((New-PlanStep -Kind say -Text ('Restart ' + $s.Name + ': docker restart ' + $s.Container + '. The page is down for a few seconds; nothing else is touched.')))
            $steps.Add((New-PlanStep -Kind window -Exe 'docker' -Arguments @('restart', $s.Container) -Text 'In a window of its own.'))
            $steps.Add((New-PlanStep -Kind log -Text ('web.restart ' + $s.Key)))
        }
        'web.redeploy' {
            if (-not (& $pcOnly)) { break }
            $steps.Add((New-PlanStep -Kind say -Text ('Redeploy ' + $s.Name + ': ' + (Split-Path -Leaf $s.Start) + ' recreates ' + $s.Container + ' from its compose file and checks it answers. Down for a few seconds.')))
            $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + $s.Start) -Text 'In a window of its own, which stays open.'))
            $steps.Add((New-PlanStep -Kind log -Text ('web.redeploy ' + $s.Key)))
        }
        'web.logs' {
            if (-not (& $pcOnly)) { break }
            $steps.Add((New-PlanStep -Kind window -Exe 'docker' -Arguments @('logs', '--tail', '200', $s.Container) -Text ('The last 200 lines of ' + $s.Container + ', in a window of its own. Changes nothing.')))
        }
        'web.deploy' {
            if ($s.Where -cne 'haven') { $steps.Add((New-PlanStep -Kind refuse -Text ($s.Name + ' runs on this PC: Redeploy is its button, not Deploy.'))); break }
            if (-not $s.Deploy) { $steps.Add((New-PlanStep -Kind refuse -Text ($s.Name + ' has no deploy in websites.json. The allowlist: ' + $(if (@($web.Deploys.Keys).Count) { @($web.Deploys.Keys) -join ', ' } else { 'empty' }) + '.'))); break }
            $d = $web.Deploys[$s.Deploy]
            if (-not $d) { $steps.Add((New-PlanStep -Kind refuse -Text ($s.Deploy + ' is not in the deploys allowlist. Nothing was run.'))); break }
            $steps.Add((New-PlanStep -Kind confirm -Phrase (Get-ConfirmPhrase $Action) -Text ('This deploys ' + $s.Name + ' to Haven: ' + $d.Script + ' ' + $d.What + '. It uses your own SSH key; the window keeps a log under scripts\shard-console-output\deploys.')))
            $steps.Add((New-PlanStep -Kind window -Exe $Config.PowerShell -Arguments ($ps + @($Config.WebsiteDeployScript, '-Deploy', $d.Name)) -Text 'In a window of its own, which stays open. It checks the allowlist again before it runs anything.'))
            $steps.Add((New-PlanStep -Kind log -Text ('web.deploy ' + $s.Key + ' ' + $d.Name)))
        }
        default { $steps.Add((New-PlanStep -Kind refuse -Text ("Unknown action '" + $Action + "'. -Action list shows them."))) }
    }
    $steps.ToArray()
}

function Format-StatusReport {
    # Lines with a colour name: normal, head, green, amber, red, gray. $ExtraTiles (cc-P74) are tiles
    # judged outside the docker state, such as AUTOPILOT, listed after the three.
    param($State, $Config, [object[]]$ExtraTiles)
    $o = New-Object 'System.Collections.Generic.List[object]'
    $add = { param($t, $c) $o.Add([pscustomobject]@{ Text = $t; Color = $(if ($c) { $c } else { 'normal' }) }) }
    $ind = '            '
    & $add ('Status as of ' + (Format-LocalTime $State.Now) + ', read from ' + $State.Source) 'gray'
    foreach ($t in @(@(Get-AtAGlance $State $Config) + @($ExtraTiles | Where-Object { $_ }))) {
        # cc-P94: a tile can say two lines (AUTOPILOT while running); the second is indented under the first.
        $dl = @(([string]$t.Detail) -split "`n")
        & $add (('{0,-17}: {1,-15} {2}' -f $t.Title, $t.Word, $dl[0])) $t.Color
        foreach ($x in @($dl | Select-Object -Skip 1)) { & $add ((' ' * 35) + $x) $t.Color }
    }
    if ($State.DockerError) { & $add ('DOCKER NOT REACHABLE: ' + $State.DockerError) 'red' }
    if ($State.Latest.Exists) {
        & $add ($Config.LatestTag + ' = ' + (Format-ShortId $State.Latest.Id) + ', built ' + (Format-LocalTime $State.Latest.Created) + ' (' + (Format-Age ($State.Now - $State.Latest.Created)) + ' ago)') 'normal'
    } else {
        & $add ('There is no ' + $Config.LatestTag + ' image.') 'red'
    }
    if ($State.Compose.Warning) { & $add ('LIVE COMPOSE FILE: ' + $State.Compose.Warning) 'amber' }

    foreach ($sh in $Config.Shards.Values) {
        # cc-P108: a retired shard has its one tile line above and no section: nothing about it is checked.
        if ($sh.Retired) { continue }
        $s = $State.Shards[$sh.Key]
        $c = $s.Container
        & $add '' 'normal'
        & $add ('== ' + $sh.Label + ' shard: ' + $sh.Container + ' ' + ('=' * (60 - $sh.Container.Length))) 'head'
        if (-not $c.Exists) {
            $hint = ''
            if (-not $sh.IsLive) { $hint = ' Test shard > Start creates it.' }
            & $add ('state     : NO CONTAINER.' + $hint) 'amber'
        } else {
            if ($c.Running) {
                & $add ('state     : running, up ' + (Format-Age ($State.Now - $c.StartedAt)) + ' (since ' + (Format-LocalTime $c.StartedAt) + ')') 'green'
            } else {
                $when = ''
                if ($c.FinishedAt) { $when = ', ' + (Format-Age ($State.Now - $c.FinishedAt)) + ' ago' }
                & $add ('state     : ' + $c.Status + ' (exit code ' + $c.ExitCode + ')' + $when) 'amber'
            }
            $col = 'amber'
            if ($s.Drift.Verdict -eq 'DRIFTED') { $col = 'red' }
            if ($s.Drift.Verdict -eq 'CURRENT') { $col = 'green' }
            & $add ('image     : ' + $s.Drift.Verdict) $col
            foreach ($l in $s.Drift.Lines) {
                $lc = 'normal'
                if ($l -like 'AMBIGUOUS*') { $lc = 'amber' }
                & $add ($ind + $l) $lc
            }
            if ($s.Drift.Advice) { & $add ($ind + 'what to do : ' + $s.Drift.Advice) $col }
            $lcol = 'red'
            if ($s.Listener.State -eq 'LISTENING') { $lcol = 'green' }
            if ($s.Listener.State -eq 'STOPPED') { $lcol = 'gray' }
            & $add ('listener  : ' + $s.Listener.State + ' ' + $s.Listener.Detail) $lcol
        }
        $v = $s.Save
        if ($v.Exists) {
            $acc = 'accounts: yes'
            if (-not $v.HasAccounts) { $acc = 'accounts: NONE (a start will block at the owner prompt, D19)' }
            $age = $State.Now - $v.Newest.LastWriteUtc
            $scol = 'normal'
            if (-not $v.HasAccounts) { $scol = 'red' }
            & $add ('world save: newest ' + $v.Newest.RelPath + ' at ' + (Format-LocalTime $v.Newest.LastWriteUtc) + ' (' + (Format-Age $age) + ' ago), ' + $v.Count + ' files, ' + (Format-Bytes $v.Bytes) + ', ' + $acc) $scol
            # Only once it has been up long enough to have saved: a restored or seeded save keeps
            # its files' old timestamps until the first autosave, which is not a fault.
            if ($c.Exists -and $c.Running -and $age.TotalMinutes -gt $Config.StaleSaveMinutes -and
                ($State.Now - $c.StartedAt).TotalMinutes -gt $Config.StaleSaveMinutes) {
                & $add ($ind + 'NOT SAVING? nothing written for ' + (Format-Age $age) + '; the logs show an autosave every 5 minutes.') 'red'
            }
        } else {
            & $add ('world save: none at ' + $sh.Saves) 'amber'
        }
        $snaps = @($s.Snapshots)
        if ($snaps.Count) {
            & $add ('snapshots : ' + $snaps.Count + ', newest ' + $snaps[0].Name + ' (' + (Format-Bytes $snaps[0].Bytes) + ')') 'normal'
        } else {
            & $add 'snapshots : none' 'gray'
        }
        if (@($s.Asides).Count) {
            & $add ('set aside : ' + @($s.Asides).Count + ' old saves moved aside beside the save folder, newest ' + @($s.Asides | Sort-Object -Descending)[0] + '. The console never deletes them.') 'gray'
        }
    }

    $p = $State.Publisher
    & $add '' 'normal'
    & $add ('== Status publisher: status.json for the website ' + ('=' * 29)) 'head'
    & $add ('state     : ' + $p.State + '. ' + $p.Detail) $p.Color
    foreach ($l in $p.Lines) {
        $lc = 'normal'
        if ($l -like 'WARNING*') { $lc = 'amber' }
        if ($l -like 'feed*') {
            $lc = 'amber'
            if ($p.FeedState -eq 'SERVING') { $lc = 'green' }
            if ($p.FeedState -eq 'UNKNOWN') { $lc = 'gray' }
        }
        & $add ($ind + $l) $lc
    }
    $o.ToArray()
}

# --- world commands (cc-P21) -----------------------------------------------------------------
# The World setup tab lists the in-game commands that change a world. It DISCOVERS them: every
# CommandSystem.Register( call in server/customizations, with the [ShardCommand(...)] its handler
# declares (server/customizations/ShardCommandAttribute.cs). It never decides what a command is.
# A registered command with no declaration is listed as "new, unclassified"; it never vanishes.
# The build gate (server/tests/Misc/CommandDeclarationVerification.cs) fails a command registered
# without one, so that group should be empty on any tree that built.
#
# The console copies text; a GM pastes it into a client. It runs nothing in game and cannot see
# whether a world has had any of these: that is a fact about the save.

$script:CommandCategoryNames = @{
    WorldGeneration = 'world-generation'; WorldSetup = 'world-setup'; WorldRemoval = 'world-removal'
    DevTool = 'dev-tool'; Grant = 'grant'; Diagnostic = 'diagnostic'; Player = 'player'
}
$script:CommandRerunText = @{
    Skips = 're-run skips what exists'; Replaces = 're-run replaces what it placed'
    Duplicates = 'RE-RUN DUPLICATES: run once'; Refuses = 'refuses a second run'
    DeletesAgain = 're-run deletes again whatever matches'; Unverified = 're-run: not established'
}
$script:CommandShardText = @{
    Any = 'either shard'; TestFirst = 'test first, then live'; TestOnly = 'test only'; Unverified = 'shard: not stated'
}

function New-OrderStep {
    param([string]$Name, [string]$Arguments)
    [pscustomobject]@{ Name = $Name; Args = $Arguments; Stock = $false }
}

function New-StockStep {
    # A pinned command. Pinned's source cannot carry our declaration, so these are the one place
    # the console states facts itself, each with where it came from.
    param([string]$Command, [string]$Access, [string]$Why)
    [pscustomobject]@{ Name = $null; Args = $null; Stock = $true; Command = $Command; Access = $Access; Why = $Why }
}

# ORDER. A relationship between commands, not a property of one, so it is not declared at the
# command. One explicit list of sequences, each from a runbook that states it. Between sequences
# no order is established, and nothing here invents one. A world command in no sequence is listed
# under "no order established".
$script:WorldSetupOrder = @(
    [pscustomobject]@{
        Key    = 'fresh'
        Title  = 'Fresh world, once: generation, in this order'
        Source = 'notes/shard-console.md section 8 and notes/reachability-audit.md section 5 (spawn files, then Shame, then Despise, then save); docs/client-test-queue.md:49 (Shame then Despise)'
        Steps  = @(
            # Syntax from docker/uo/SERVER.md:355-357; the twelve paths from notes/reachability-audit.md section 5, and
            # cc-P678's two: New Haven is pre-UOML Haven on SoS, its people from uoml/trammel (ClusterFHavenTownSpawns
            # admits only those inside the town).
            foreach ($f in @(
                'post-uoml/termur/Abyss.json', 'post-uoml/termur/TerMur.json', 'post-uoml/termur/Underworld.json',
                'shared/malas/Citadel.json', 'shared/malas/Labyrinth.json',
                'shared/trammel/Sanctuary.json', 'shared/felucca/Sanctuary.json',
                'shared/trammel/PrismOfLight.json', 'shared/felucca/PrismOfLight.json',
                'post-uoml/trammel/Vendors.json', 'post-uoml/felucca/Vendors.json',
                'post-uoml/trammel/Outdoors.json',
                'uoml/trammel/Vendors.json', 'uoml/trammel/TownsLife.json')) {
                New-StockStep ('[GenerateSpawners Data/Spawns/' + $f) 'Developer' 'pinned. A spawn file ported content comes through. Re-running replaces, not duplicates.'
            }
            (New-OrderStep 'GenerateNewShame')
            (New-OrderStep 'SetupDespise')
            (New-StockStep '[save' 'Administrator' 'pinned. Save after generation (docker/uo/SERVER.md:360).')
        )
    }
    [pscustomobject]@{
        Key    = 'newhaven'
        Title  = 'Existing world: New Haven NPCs and guild halls, in this order'
        # cc-P678: ClusterFSeedNewHavenServices (the ML plaza's decoration and stand-in Healer) retired with the plaza.
        Source = 'notes/cc-P17-playtest-bugs-1.md:624-637 (a fresh world runs the first two after the spawn files, :634-637)'
        Steps  = @(
            (New-OrderStep 'ClusterFSeedNewHaven' 'dryrun')
            (New-OrderStep 'ClusterFSeedNewHaven' 'repair')
            (New-OrderStep 'ClusterFSeedGuildHalls' 'repair')
        )
    }
    [pscustomobject]@{
        Key    = 'minecamp'
        Title  = 'Existing world: mine camp tents, then the NPC move, in this order'
        Source = 'notes/cc-P16-mine-camp-tents.md:321-324 and :331-338'
        Steps  = @(
            (New-OrderStep 'ClusterFSeedMineCamp' 'dryrun')
            (New-OrderStep 'ClusterFSeedMineCamp')
            (New-OrderStep 'ClusterFMoveMineCampNpcs' 'dryrun')
            (New-OrderStep 'ClusterFMoveMineCampNpcs')
        )
    }
)

function Get-AttributeBlock {
    # The attribute lines directly above line $DeclIndex: everything back to the previous line
    # that ends a statement or a block. Comment lines are skipped, not treated as an end.
    param([string[]]$Lines, [int]$DeclIndex)
    $i = $DeclIndex - 1
    $block = New-Object 'System.Collections.Generic.List[string]'
    while ($i -ge 0) {
        $t = $Lines[$i].Trim()
        if ($t -notlike '//*' -and $t -match '[;{}]$') { break }
        $block.Insert(0, $Lines[$i])
        $i--
    }
    $block -join "`n"
}

function ConvertFrom-ShardCommandArgs {
    param([string]$Text)
    $r = [ordered]@{ Category = $null; Rerun = $null; Shard = $null; DryRun = ''; NoDryRun = $false; Summary = '' }
    $str = '"((?:[^"\\]|\\.)*)"'
    $m = [regex]::Match($Text, '^\s*CommandCategory\.(\w+)')
    if ($m.Success) { $r.Category = $m.Groups[1].Value }
    $m = [regex]::Match($Text, '\bRerun\s*=\s*CommandRerun\.(\w+)')
    if ($m.Success) { $r.Rerun = $m.Groups[1].Value }
    $m = [regex]::Match($Text, '\bShard\s*=\s*CommandShard\.(\w+)')
    if ($m.Success) { $r.Shard = $m.Groups[1].Value }
    $m = [regex]::Match($Text, '\bDryRun\s*=\s*' + $str)
    if ($m.Success) { $r.DryRun = $m.Groups[1].Value }
    $r.NoDryRun = [regex]::IsMatch($Text, '\bNoDryRun\s*=\s*true')
    $m = [regex]::Match($Text, '\bSummary\s*=\s*' + $str)
    if ($m.Success) { $r.Summary = $m.Groups[1].Value -replace '\\"', '"' }
    [pscustomobject]$r
}

function ConvertFrom-CommandSources {
    # Source text in (a hashtable of path -> file text), one object per CommandSystem.Register(
    # call out. Pure: the shell reads the files (Get-CommandSourceFiles).
    param([hashtable]$Sources)
    $str = '"((?:[^"\\]|\\.)*)"'
    $reg = [regex]('CommandSystem\.Register\(\s*(?:"(?<lit>[^"]*)"|(?<id>[A-Za-z_][\w.]*))\s*,\s*(?:AccessLevel\.(?<access>\w+)|(?<accessId>[A-Za-z_][\w.]*))\s*,\s*(?<handler>[A-Za-z_]\w*)\s*\)')
    foreach ($path in @($Sources.Keys | Sort-Object)) {
        $text = ([string]$Sources[$path]) -replace "`r`n", "`n"
        $lines = $text -split "`n"
        foreach ($call in [regex]::Matches($text, 'CommandSystem\.Register\(')) {
            $lineStart = $text.LastIndexOf("`n", [Math]::Max(0, $call.Index - 1)) + 1
            if ($call.Index -eq 0) { $lineStart = 0 }
            if ($text.Substring($lineStart, $call.Index - $lineStart).TrimStart() -match '^(//|\*)') { continue }
            $lineNo = ([regex]::Matches($text.Substring(0, $call.Index), "`n")).Count + 1
            $problems = New-Object 'System.Collections.Generic.List[string]'
            $e = [ordered]@{
                Name = ''; Access = ''; Handler = ''; File = $path; Line = $lineNo; Usage = ''; Description = ''
                Declared = $false; Category = 'unclassified'; Rerun = ''; Shard = ''; RerunText = ''; ShardText = ''
                DryRun = ''; NoDryRun = $false; DryRunCommand = ''; Summary = ''; Command = ''; Problems = $problems
            }
            $m = $reg.Match($text, $call.Index)
            if (-not $m.Success -or $m.Index -ne $call.Index) {
                # A shape this reader does not know (a lambda handler, a computed name). Listed, never dropped.
                $lit = [regex]::Match($text.Substring($call.Index, [Math]::Min(300, $text.Length - $call.Index)), $str)
                $e.Name = '(unread)'
                if ($lit.Success) { $e.Name = $lit.Groups[1].Value }
                $problems.Add('the console could not read this registration; look at ' + $path + ':' + $lineNo)
            } else {
                if ($m.Groups['access'].Success) {
                    $e.Access = $m.Groups['access'].Value
                } else {
                    # An access level held in a constant (ClusterFStaffHub.Access, cc-P53), read the way a constant name is.
                    $aid = @($m.Groups['accessId'].Value -split '\.')[-1]
                    $ac = [regex]::Match($text, 'const\s+AccessLevel\s+' + [regex]::Escape($aid) + '\s*=\s*AccessLevel\.(\w+)')
                    if ($ac.Success) { $e.Access = $ac.Groups[1].Value } else { $e.Access = $m.Groups['accessId'].Value; $problems.Add('access is the constant ' + $aid + ', not found in this file') }
                }
                $e.Handler = $m.Groups['handler'].Value
                if ($m.Groups['lit'].Success) {
                    $e.Name = $m.Groups['lit'].Value
                } else {
                    $id = @($m.Groups['id'].Value -split '\.')[-1]
                    $c = [regex]::Match($text, 'const\s+string\s+' + [regex]::Escape($id) + '\s*=\s*' + $str)
                    if ($c.Success) { $e.Name = $c.Groups[1].Value } else { $e.Name = $m.Groups['id'].Value; $problems.Add('name is the constant ' + $id + ', not found in this file') }
                }
                $declIndex = -1
                for ($i = 0; $i -lt $lines.Count; $i++) {
                    if ($lines[$i] -match ('^\s*(?:(?:public|private|internal|protected|static|async)\s+)+void\s+' + [regex]::Escape($e.Handler) + '\s*\(')) { $declIndex = $i; break }
                }
                if ($declIndex -lt 0) {
                    $problems.Add('handler ' + $e.Handler + ' is not in ' + $path)
                } else {
                    $block = Get-AttributeBlock $lines $declIndex
                    $u = [regex]::Match($block, '\bUsage\(\s*' + $str)
                    if ($u.Success) { $e.Usage = $u.Groups[1].Value }
                    $d = [regex]::Match($block, '\bDescription\(\s*' + $str)
                    if ($d.Success) { $e.Description = $d.Groups[1].Value }
                    $sc = [regex]::Match($block, '\bShardCommand\((?<args>(?:"(?:[^"\\]|\\.)*"|[^"])*?)\)\s*\]', 'Singleline')
                    if ($sc.Success) {
                        $a = ConvertFrom-ShardCommandArgs $sc.Groups['args'].Value
                        if ($a.Category -and $script:CommandCategoryNames.ContainsKey($a.Category)) {
                            $e.Declared = $true
                            $e.Category = $script:CommandCategoryNames[$a.Category]
                        } else {
                            $problems.Add('[ShardCommand] names no category the console knows: ' + $a.Category)
                        }
                        $e.Rerun = [string]$a.Rerun
                        $e.Shard = [string]$a.Shard
                        $e.DryRun = $a.DryRun
                        $e.NoDryRun = $a.NoDryRun
                        $e.Summary = $a.Summary
                    }
                }
            }
            if ($e.Rerun -and $script:CommandRerunText.ContainsKey($e.Rerun)) { $e.RerunText = $script:CommandRerunText[$e.Rerun] }
            if ($e.Shard -and $script:CommandShardText.ContainsKey($e.Shard)) { $e.ShardText = $script:CommandShardText[$e.Shard] }
            $e.Command = '[' + $e.Name
            if ($e.DryRun) {
                $words = @($e.Usage -split '[^A-Za-z0-9_]+')
                if (-not $e.Declared) {
                    # only a declaration says a word is a dry run
                } elseif ($words -ccontains $e.DryRun) {
                    $e.DryRunCommand = '[' + $e.Name + ' ' + $e.DryRun
                } else {
                    $problems.Add('declared dry run "' + $e.DryRun + '" is not a word of its [Usage("' + $e.Usage + '")], so none is shown')
                }
            }
            if ($e.Category -in 'world-generation', 'world-setup', 'world-removal') {
                if (-not $e.RerunText) { $problems.Add('declares no re-run behaviour') }
                if (-not $e.ShardText) { $problems.Add('declares no shard') }
                if (-not $e.Summary) { $problems.Add('declares no summary') }
            }
            [pscustomobject]$e
        }
    }
}

function Format-WorldCommandLine {
    # The line beside a command in the tab. What it does, who may run it, where, and what a
    # second run does, in the command's own declared words.
    param($Entry, [string]$Arguments)
    if ($Entry.Category -eq 'unclassified') {
        $t = 'NEW, UNCLASSIFIED: registered at ' + $Entry.File + ':' + $Entry.Line + ' with no [ShardCommand] the console could read. Read its source before running it.'
        if ($Entry.Access) { $t = $Entry.Access + '. ' + $t }
        if ($Entry.Usage) { $t += ' Usage: ' + $Entry.Usage }
        foreach ($p in $Entry.Problems) { $t += ' (' + $p + ')' }
        return $t
    }
    $parts = New-Object 'System.Collections.Generic.List[string]'
    if ($Entry.Shard -eq 'TestOnly') { $parts.Add('TEST SHARD ONLY') }
    $parts.Add($Entry.Access)
    if ($Entry.Shard -ne 'TestOnly' -and $Entry.ShardText) { $parts.Add($Entry.ShardText) }
    if ($Entry.RerunText) { $parts.Add($Entry.RerunText) }
    if ($Arguments -eq $Entry.DryRun -and $Entry.DryRun) {
        $parts.Add('DRY RUN: reports and changes nothing')
    } elseif ($Entry.NoDryRun) {
        $parts.Add('no dry run')
    }
    $t = ($parts -join '. ') + '. ' + $Entry.Summary
    if (-not $Entry.Summary -and $Entry.Description) { $t += $Entry.Description }
    foreach ($p in $Entry.Problems) { $t += ' (' + $p + ')' }
    $t
}

function Test-WorldCommandWarn {
    # Shown in red: nobody has classified it, it is for the test shard only, or its plain form
    # (no argument) deletes something. A dry-run row deletes nothing.
    param($Entry, [string]$Arguments)
    if ($Entry.Category -eq 'unclassified' -or $Entry.Shard -eq 'TestOnly') { return $true }
    if ($Arguments -and $Arguments -ceq $Entry.DryRun) { return $false }
    ($Entry.Rerun -eq 'DeletesAgain') -or ($Entry.Summary -clike 'DELETES*')
}

function Get-WorldCommandGroups {
    # The tab's groups, in the order shown. Sequences first (from $script:WorldSetupOrder), then
    # every other world command, removals, and anything unclassified. Player, dev-tool, grant and
    # diagnostic commands are never in this tab.
    param([object[]]$Commands)
    $byName = @{}
    foreach ($c in $Commands) { if (-not $byName.ContainsKey($c.Name)) { $byName[$c.Name] = $c } }
    $used = @{}
    foreach ($seq in $script:WorldSetupOrder) {
        $items = foreach ($s in $seq.Steps) {
            if ($s.Stock) {
                [pscustomobject]@{ Name = $null; Command = $s.Command; Warn = $false; Text = $s.Access + ': ' + $s.Why }
                continue
            }
            $cmd = '[' + $s.Name
            if ($s.Args) { $cmd += ' ' + $s.Args }
            $e = $byName[$s.Name]
            if ($null -eq $e) {
                [pscustomobject]@{ Name = $s.Name; Command = $cmd; Warn = $true; Text = 'NOT REGISTERED: the order list names a command the source no longer registers. Do not run it until someone looks.' }
                continue
            }
            $used[$s.Name] = $true
            [pscustomobject]@{ Name = $s.Name; Command = $cmd; Warn = (Test-WorldCommandWarn $e $s.Args); Text = (Format-WorldCommandLine $e $s.Args) }
        }
        [pscustomobject]@{ Key = $seq.Key; Title = $seq.Title; Source = $seq.Source; CopyAll = $true; Items = @($items) }
    }
    # Every row is one command and one Copy, in every group. A command with a dry run gets it as its
    # own row directly above, the way the runbooks, and so the sequences above, list them.
    $single = {
        param($e)
        if ($e.DryRunCommand) {
            [pscustomobject]@{ Name = $e.Name; Command = $e.DryRunCommand; Warn = (Test-WorldCommandWarn $e $e.DryRun); Text = (Format-WorldCommandLine $e $e.DryRun) }
        }
        [pscustomobject]@{ Name = $e.Name; Command = $e.Command; Warn = (Test-WorldCommandWarn $e); Text = (Format-WorldCommandLine $e) }
    }
    $rest = @($Commands | Where-Object { $_.Category -in 'world-generation', 'world-setup' -and -not $used.ContainsKey($_.Name) } | Sort-Object Name)
    [pscustomobject]@{
        Key = 'noorder'; Title = 'Existing world: no order established between these'; CopyAll = $false
        Source = 'No runbook states an order for these, and the console does not invent one. Copy them one at a time.'
        Items = @($rest | ForEach-Object { & $single $_ })
    }
    [pscustomobject]@{
        Key = 'removal'; Title = 'Removal: each undoes what another command placed'; CopyAll = $false
        Source = 'Never part of a run sequence.'
        Items = @($Commands | Where-Object { $_.Category -eq 'world-removal' } | Sort-Object Name | ForEach-Object { & $single $_ })
    }
    [pscustomobject]@{
        Key = 'unclassified'; Title = 'Registered but new, unclassified'; CopyAll = $false
        Source = 'A command with no [ShardCommand] the console could read. The build gate should keep this empty; if it is not, the tree has not been through build.sh.'
        Items = @($Commands | Where-Object { $_.Category -eq 'unclassified' } | Sort-Object Name | ForEach-Object { & $single $_ })
    }
}

function Get-GroupClipboardText {
    # Every command of a group, one per line, in order. The client takes one command at a time;
    # this is for a text file beside it, so nobody loses their place.
    param($Group)
    (@($Group.Items | ForEach-Object { $_.Command }) -join "`r`n") + "`r`n"
}

function Format-WorldCommandReport {
    # The tab as text, for -WorldCommands.
    param([object[]]$Groups)
    foreach ($g in $Groups) {
        [pscustomobject]@{ Text = ''; Color = 'normal' }
        [pscustomobject]@{ Text = ('== ' + $g.Title + ' ' + ('=' * [Math]::Max(3, 90 - $g.Title.Length))); Color = 'head' }
        [pscustomobject]@{ Text = ('   ' + $g.Source); Color = 'gray' }
        if (-not @($g.Items).Count) { [pscustomobject]@{ Text = '   (none)'; Color = 'gray' } }
        foreach ($i in $g.Items) {
            $c = 'normal'
            if ($i.Warn) { $c = 'amber' }
            [pscustomobject]@{ Text = ('   {0,-44} {1}' -f $i.Command, $i.Text); Color = $c }
        }
    }
}

# =============================================================================================
# SHELL. Docker, the disk, the log file and the form. Kept thin: it gathers sections, hands
# them to the core, and runs or prints the plans the core returns.
# =============================================================================================

function Write-ConsoleLine {
    param([string]$Text, [string]$Color = 'normal')
    if ($script:OutputBox) {
        Add-RichLine $script:OutputBox $Text $Color
    } elseif ($env:SHARD_CONSOLE_PANE -eq '1') {
        # cc-P84: a run streaming into the console's Action output pane. Each line carries its color.
        [Console]::Out.WriteLine((Format-PaneLine $Text $Color))
    } else {
        $map = @{ normal = 'Gray'; head = 'Cyan'; green = 'Green'; amber = 'Yellow'; red = 'Red'; gray = 'DarkGray' }
        Write-Host $Text -ForegroundColor $map[$Color]
    }
}

function Write-ConsoleLog {
    # cc-P132: never throws: a config with no log file (a fact's, or none) drops the line.
    param($Config, [string]$Text)
    $line = '{0} {1} {2}' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant), $env:USERNAME, $Text
    try { if ($Config -and $Config.LogFile) { [IO.File]::AppendAllText($Config.LogFile, $line + "`r`n", (New-Object Text.UTF8Encoding $false)) } } catch { }
}

# cc-P113: the window icon. Loaded once per file and kept (every form shares the one Icon). Missing or unreadable:
# $null, one line in the action log, and the form keeps Windows' default icon; never a popup, never a failure.
$script:ConsoleIcons = @{}
function Get-ConsoleIcon {
    param($Config)
    $path = [string]$Config.WindowIconFile
    if ($script:ConsoleIcons.ContainsKey($path)) { return $script:ConsoleIcons[$path] }
    $icon = $null
    try {
        if (-not $path) { throw 'no WindowIconFile in the config' }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('no file at ' + $path) }
        Add-Type -AssemblyName System.Drawing
        $icon = New-Object Drawing.Icon $path
    } catch {
        $icon = $null
        try { Write-ConsoleLog $Config ('icon not loaded: ' + $_.Exception.Message) } catch { }
    }
    $script:ConsoleIcons[$path] = $icon
    $icon
}

function Set-ConsoleIcon {
    # Every form the console makes calls this once, right after New-Object Windows.Forms.Form.
    param($Form, $Config)
    $icon = Get-ConsoleIcon $Config
    if ($icon) { $Form.Icon = $icon }
}

function Invoke-DockerRead {
    # Read-only docker calls. stdout and stderr kept apart, so 5.1 cannot wrap either in an
    # ErrorRecord, and a hung daemon cannot hang the form for more than the timeout.
    param([string[]]$Arguments, [int]$TimeoutMs = 20000)
    Invoke-ToolRead -Exe 'docker' -Arguments $Arguments -TimeoutMs $TimeoutMs
}

function Invoke-ToolRead {
    # Invoke-DockerRead for any tool; the Commit tab reads git through it (cc-P60).
    param([string]$Exe, [string[]]$Arguments, [int]$TimeoutMs = 20000)
    $psi = New-Object Diagnostics.ProcessStartInfo $Exe
    $psi.Arguments = (@($Arguments | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    try { $p = [Diagnostics.Process]::Start($psi) }
    catch { return [pscustomobject]@{ Out = ''; Err = ('error: ' + $Exe + ' could not be started: ' + $_.Exception.Message); Code = -1 } }
    $o = $p.StandardOutput.ReadToEndAsync()
    $e = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($TimeoutMs)) {
        try { $p.Kill() } catch { }
        return [pscustomobject]@{ Out = ''; Err = ('error: ' + $Exe + ' did not answer within ' + ($TimeoutMs / 1000) + ' s'); Code = -1 }
    }
    [pscustomobject]@{ Out = $o.Result; Err = $e.Result; Code = $p.ExitCode }
}

function Get-CommitFacts {
    # What Get-CommitPlan reads, all read-only: who this console runs as, each repo's pending files
    # (git status, as Commit-ShardWork.ps1 reads it) and when each last changed, and the last line
    # of every CC notes file. A git failure is kept as an error, never read as clean (D66).
    param($Config)
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $repos = New-Object 'System.Collections.Generic.List[object]'
    $changed = New-Object 'System.Collections.Generic.List[object]'
    foreach ($r in @($Config.CommitRepos)) {
        $g = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $r.Path, '--no-optional-locks', 'status', '--porcelain', '--untracked-files=all')
        $err = $null
        $paths = @()
        if ($g.Code -ne 0) {
            $err = @((($g.Err + "`n" + $g.Out) -split "`n") | Where-Object { $_.Trim() } | Select-Object -First 1)
            $err = ([string]($err -join '')).Trim()
            if (-not $err) { $err = 'git exited with ' + $g.Code }
        } else {
            $paths = @(ConvertFrom-GitPorcelain @(@($g.Out -split "`n") | ForEach-Object { $_.TrimEnd("`r") }))
            foreach ($p in $paths) {
                $full = Join-Path $r.Path ($p -replace '/', '\')
                if (Test-Path -LiteralPath $full -PathType Leaf) {
                    $changed.Add([pscustomobject]@{ Repo = $r.Name; Path = $p; LastWriteUtc = (Get-Item -LiteralPath $full -Force).LastWriteTimeUtc })
                }
            }
        }
        $repos.Add([pscustomobject]@{ Name = $r.Name; Path = $r.Path; Paths = $paths; Error = $err })
    }
    $notes = @()
    if ($Config.NotesFolder -and (Test-Path -LiteralPath $Config.NotesFolder)) {
        $notes = @(foreach ($f in Get-ChildItem -LiteralPath $Config.NotesFolder -Filter 'cc-P*.md' -File) {
            $last = @([IO.File]::ReadAllLines($f.FullName) | Where-Object { $_.Trim() } | Select-Object -Last 1)
            [pscustomobject]@{ Name = $f.Name; LastLine = [string]($last -join '') }
        })
    }
    [pscustomobject]@{
        Identity     = $id.Name
        IsSystem     = [bool]$id.IsSystem
        ScriptExists = [bool]($Config.CommitScript -and (Test-Path -LiteralPath $Config.CommitScript -PathType Leaf))
        Repos        = $repos.ToArray()
        Notes        = $notes
        Changed      = $changed.ToArray()
        Now          = [datetime]::UtcNow
    }
}

function Get-PackageZips {
    # cc-P63: the zips in dist\, newest first, each with what its version.json says. Read-only, and
    # no hashing: the picker lists them all, and only the picked one is hashed (Get-PackageFacts).
    param([string]$Dist)
    if (-not $Dist -or -not (Test-Path -LiteralPath $Dist)) { return @() }
    $rows = foreach ($f in @(Get-ChildItem -LiteralPath $Dist -Filter 'ShatteredLegacy-*.zip' -File | Sort-Object LastWriteTimeUtc -Descending)) {
        $jp = $f.FullName -replace '\.zip$', '.version.json'
        $j = [pscustomobject]@{ Exists = $false; Error = $null; Version = $null; Notes = $null; Sha256 = $null; Bytes = $null }
        if (Test-Path -LiteralPath $jp -PathType Leaf) {
            $j.Exists = $true
            try {
                $d = [IO.File]::ReadAllText($jp) | ConvertFrom-Json
                $j.Version = [string]$d.version; $j.Notes = [string]$d.notes; $j.Sha256 = [string]$d.sha256; $j.Bytes = [string]$d.bytes
            } catch { $j.Error = $_.Exception.Message }
        }
        [pscustomobject]@{ Name = $f.Name; FullName = $f.FullName; Bytes = $f.Length; LastWriteUtc = $f.LastWriteTimeUtc; Json = $j }
    }
    @($rows)
}

function Read-ZipPackageVersion {
    # The stamp in <top>/app/package-version.txt inside a zip, as gate 5 reads it.
    param([string]$Path)
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $za = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $pv = @($za.Entries | Where-Object { $_.FullName -match '^[^/]+/app/package-version\.txt$' })
        if ($pv.Count -ne 1) { return $null }
        $sr = New-Object IO.StreamReader($pv[0].Open())
        try { return $sr.ReadToEnd().Trim() } finally { $sr.Dispose() }
    } finally { $za.Dispose() }
}

function Get-PackageClientFacts {
    # cc-P124: the client folder Build package ships, read: its sl-build.txt, the version it reports and the client
    # gate's verdict (PackageClient.ps1, the same functions Build-PlayerPackage.ps1 runs). Read-only.
    param([string]$ClientDir, [string]$RepoRoot)
    $info = $null; $problems = @(); $ver = $null
    try {
        $info = Read-ClientBuildInfo $ClientDir
        $problems = @(Test-PackageClient $ClientDir $RepoRoot)
        $ver = Get-ClientBuildVersion $ClientDir
    } catch { $problems = @('could not read ' + $ClientDir + ': ' + $_.Exception.Message) }
    $text = $null
    if ($info -and $info.Exists) { $text = $info.Text }
    [pscustomobject]@{ Dir = $ClientDir; SlBuildText = $text; Version = $ver; Problems = $problems }
}

function Get-PackageFacts {
    # What Get-PackagePlan reads, all read-only: the two scripts, the shard repo's git status and
    # HEAD (a git failure is kept as an error, never read as clean, D66), the zips in dist\, the
    # picked zip's own SHA-256 and package-version, the newest committed CHANGELOG.md and the
    # newest cc-P notes file by number.
    param($Config, [string]$Zip)
    $repo = $Config.RepoRoot
    $g = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'status', '--porcelain', '--untracked-files=all')
    $err = $null; $paths = @()
    if ($g.Code -ne 0) {
        $err = ([string](@((($g.Err + "`n" + $g.Out) -split "`n") | Where-Object { $_.Trim() } | Select-Object -First 1) -join '')).Trim()
        if (-not $err) { $err = 'git exited with ' + $g.Code }
    } else {
        $paths = @(ConvertFrom-GitPorcelain @(@($g.Out -split "`n") | ForEach-Object { $_.TrimEnd("`r") }))
    }
    $head = (Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'rev-parse', '--short', 'HEAD')).Out.Trim()
    $zips = @(Get-PackageZips $Config.PackageDist)
    $picked = $null
    if ($Zip) {
        $row = @($zips | Where-Object { $_.Name -eq $Zip })
        if ($row.Count -eq 1) {
            $picked = [pscustomobject]@{ Name = $Zip; Sha256 = $null; InnerVersion = $null; Error = $null }
            try {
                $picked.Sha256 = (Get-FileHash -LiteralPath $row[0].FullName -Algorithm SHA256).Hash.ToLower()
                $picked.InnerVersion = Read-ZipPackageVersion $row[0].FullName
            } catch { $picked.Error = $_.Exception.Message }
        }
    }
    # cc-P68: whether the build script that gates a zip is HEAD's. git diff --quiet: 0 same, 1 differs.
    $bd = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'diff', '--quiet', 'HEAD', '--', 'player-package/Build-PlayerPackage.ps1')
    $buildDiff = 'same'
    if ($bd.Code -eq 1) { $buildDiff = 'differs' }
    elseif ($bd.Code -ne 0) { $buildDiff = ('git diff exited with ' + $bd.Code + ' ' + ([string]$bd.Err).Trim()).Trim() }
    $log = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'show', 'HEAD:CHANGELOG.md')
    $changelog = $null
    if ($log.Code -eq 0) { $changelog = $log.Out }
    $latestNote = $null
    if ($Config.NotesFolder -and (Test-Path -LiteralPath $Config.NotesFolder)) {
        $n = @(Get-ChildItem -LiteralPath $Config.NotesFolder -Filter 'cc-P*.md' -File | Where-Object { $_.Name -match '^cc-P(\d+)-' } |
            Sort-Object @{ Expression = { [int]([regex]::Match($_.Name, '^cc-P(\d+)-').Groups[1].Value) } }, Name | Select-Object -Last 1)
        if ($n.Count) { $latestNote = [pscustomobject]@{ Name = $n[0].Name; Text = [IO.File]::ReadAllText($n[0].FullName) } }
    }
    [pscustomobject]@{
        BuildExists   = [bool](Test-Path -LiteralPath $Config.BuildPackage -PathType Leaf)
        PublishExists = [bool]($Config.PublishScript -and (Test-Path -LiteralPath $Config.PublishScript -PathType Leaf))
        RepoPath      = $repo
        RepoPaths     = $paths
        RepoError     = $err
        Head          = $head
        BuildDiff     = $buildDiff
        Zips          = $zips
        Picked        = $picked
        Changelog     = $changelog
        LatestNote    = $latestNote
        Client        = (Get-PackageClientFacts -ClientDir $Config.PackageClient -RepoRoot $repo)
    }
}

function Get-WipeFacts {
    # What Get-WipePlan reads, all read-only (cc-P73): who this console runs as, whether a request
    # is already waiting, the free space where the backup goes, and the newest census the running
    # test shard logged (TestAccountWipe.cs logs one at start and after any save that changed it).
    # The whole log since this start, not the 15 minutes the status panel reads.
    param($Config, $State)
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $sh = $Config.Shards['test']
    $free = $null
    try { $free = (New-Object IO.DriveInfo ([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($sh.Saves)))).AvailableFreeSpace } catch { $free = $null }
    $census = $null
    $c = $State.Shards['test'].Container
    if ($c.Exists -and $c.Running -and $c.StartedAtRaw) {
        $l = Invoke-DockerRead @('logs', '--timestamps', '--since', $c.StartedAtRaw, $sh.Container) 60000
        $census = (ConvertFrom-WipeLog ($l.Out + "`n" + $l.Err)).Census
    }
    [pscustomobject]@{
        Identity      = $id.Name
        IsSystem      = [bool]$id.IsSystem
        RequestExists = [bool](Test-Path -LiteralPath $Config.WipeRequestFile)
        FreeBytes     = $free
        Census        = $census
    }
}

function Get-ShardQuestionNow {
    # cc-P106: the question a shard is waiting on right now, read from docker (inspect and logs -t).
    param($Config, [string]$Container)
    $r = Invoke-DockerRead @('inspect', '-f', '{{.State.Running}}', $Container)
    $running = ($r.Out.Trim() -eq 'true')
    $t = Invoke-DockerRead @('logs', '-t', '--tail', [string]$Config.QuestionTailLines, $Container)
    Get-ShardQuestion -LogText ($t.Out + "`n" + $t.Err) -Running $running -Now ([datetime]::UtcNow) -QuietSeconds $Config.QuestionQuietSeconds
}

function Send-ContainerInput {
    # cc-P106: writes Text to a container's console, as typing at it would, through the Docker engine API on its named
    # pipe: attach with stdin only, write, close. Nothing stays attached, and the container's stdin stays open
    # (stdin_open: true, tty: true in compose; StdinOnce is false). docker attach itself refuses a script on a tty.
    param([string]$Container, [string]$Text)
    if ($Container -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw ('not a container name: ' + $Container) }
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'docker_engine', [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(5000)
    try {
        $req = 'POST /containers/' + $Container + '/attach?stream=1&stdin=1 HTTP/1.1' + "`r`n" + 'Host: docker' + "`r`n" + 'Upgrade: tcp' + "`r`n" + 'Connection: Upgrade' + "`r`n" + 'Content-Length: 0' + "`r`n`r`n"
        $b = [Text.Encoding]::ASCII.GetBytes($req)
        $pipe.Write($b, 0, $b.Length)
        $pipe.Flush()
        $buf = New-Object byte[] 4096
        $head = ''
        $deadline = [datetime]::UtcNow.AddSeconds(5)
        while ($head -notmatch "`r`n`r`n" -and [datetime]::UtcNow -lt $deadline) {
            $task = $pipe.ReadAsync($buf, 0, $buf.Length)
            if (-not $task.Wait(5000)) { break }
            if ($task.Result -le 0) { break }
            $head += [Text.Encoding]::ASCII.GetString($buf, 0, $task.Result)
        }
        $first = ($head -split "`r`n")[0]
        if ($first -notmatch '^HTTP/1\.[01] (101|200) ') { throw ('the Docker engine would not attach to ' + $Container + ': ' + $first) }
        $k = [Text.Encoding]::ASCII.GetBytes($Text)
        $pipe.Write($k, 0, $k.Length)
        $pipe.Flush()
        Start-Sleep -Milliseconds 500
    } finally { $pipe.Dispose() }
}

function Send-ConsoleMail {
    # cc-P106: one email through the Golem's mail settings (email.json and the DPAPI smtp-cred.xml in its folder, or
    # cc-autopilot's before the cutover), read only, the way tools\playtest-bot\Send-BotReport.ps1 does. True if sent.
    param($Config, [string]$Subject, [string]$Body)
    $ap = $Config.Autopilot
    if (-not $ap -or -not (Test-Path -LiteralPath $ap.EmailFile) -or -not (Test-Path -LiteralPath $ap.CredFile)) { return $false }
    # cc-P410: only the Tinker mails Chase: with the Golem's outbox beside its mail settings, the question is a problem
    # event for it (mailed at once); its settings.json tinkerOnlyMail false, or no Tinker running, sends it here as before.
    $gr = Split-Path -Parent $ap.EmailFile
    $ob = Join-Path $gr 'GolemOutbox.ps1'
    if (Test-Path -LiteralPath $ob) {
        try { . $ob; if (Add-OutboxMail -Root $gr -Golem 'Shard' -MailKind 'shard-question' -Subject $Subject -Body $Body -NeedsYou $true -Key ('shard-question|Shard|' + $Subject)) { return $true } } catch { }
    }
    try {
        $mc = Get-Content -LiteralPath $ap.EmailFile -Raw | ConvertFrom-Json
        $cred = Import-Clixml -LiteralPath $ap.CredFile
        $msg = New-Object System.Net.Mail.MailMessage($mc.from, $mc.to, $Subject, $Body)
        $smtp = New-Object System.Net.Mail.SmtpClient('smtp.gmail.com', 587)
        $smtp.EnableSsl = $true
        $smtp.Credentials = New-Object System.Net.NetworkCredential($cred.UserName, $cred.GetNetworkCredential().Password)
        $smtp.Send($msg)
        $msg.Dispose(); $smtp.Dispose()
        return $true
    } catch { return $false }
}

function Invoke-QuestionMail {
    # cc-P106: emails each new question once (Get-QuestionMails), and remembers it in QuestionMailFile. Only for a state
    # read from docker, never a capture.
    param($Config, $State)
    if (-not $State -or $State.Source -ne 'docker') { return }
    $sent = @()
    try { $sent = @((Get-Content -LiteralPath $Config.QuestionMailFile -Raw -ErrorAction Stop | ConvertFrom-Json).sent) } catch { $sent = @() }
    foreach ($m in @(Get-QuestionMails -State $State -Config $Config -Sent $sent)) {
        if (Send-ConsoleMail -Config $Config -Subject $m.Subject -Body $m.Body) {
            $sent = @(@($sent) + $m.Signature | Select-Object -Last 50)
            $dir = Split-Path $Config.QuestionMailFile -Parent
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            [IO.File]::WriteAllText($Config.QuestionMailFile, (([pscustomobject]@{ sent = $sent }) | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
            Write-ConsoleLog $Config ('MAIL  ' + $m.Subject)
        }
    }
}

function Get-FixFacts {
    # cc-P96: what the Fix test shard plans read besides docker, all read-only. HEAD and the last 15 commits
    # (Rebuild's list), the save record a healthy start writes (Start-TestShard.ps1), and whether that save is
    # newer than the last good image: git merge-base --is-ancestor <image commit> <save commit>.
    param($Config, $State, [switch]$NoDocker)
    $repo = $Config.RepoRoot
    $head = (Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'rev-parse', 'HEAD')).Out.Trim()
    $log = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'log', '-15', '--format=%H|%cs|%s')
    $rec = $null
    if (Test-Path -LiteralPath $Config.SaveRecordFile) {
        try { $rec = [IO.File]::ReadAllText($Config.SaveRecordFile) | ConvertFrom-Json } catch { $rec = $null }
    }
    $newer = $null
    $lg = $null
    if ($State) { $lg = Get-LastGoodImage $State.Images }
    if ($rec -and $rec.commit -and $lg) {
        $ic = Get-GoodTagCommit $lg.Tag
        $a = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'merge-base', '--is-ancestor', $ic, [string]$rec.commit)
        $anc = $null
        if ($a.Code -eq 0) { $anc = $true } elseif ($a.Code -eq 1) { $anc = $false }
        $newer = Test-SaveNewerThanImage ([string]$rec.commit) $ic $anc
    }
    # Prune's lower bound: each image's unique size. Skipped when the status came from a capture file.
    $uniq = $null
    if (-not $NoDocker) { $u = Invoke-DockerRead @('system', 'df', '-v', '--format', '{{json .Images}}') 60000; $uniq = ConvertFrom-DockerDfImages $u.Out }
    [pscustomobject]@{
        UniqueSizes = $uniq
        Head       = $(if ($head -match '^[0-9a-f]{40}$') { $head } else { $null })
        Commits    = @(ConvertFrom-GitLogLines $log.Out)
        SaveRecord = $rec
        SaveNewer  = $newer
    }
}

function Wait-TestShardHealth {
    # cc-P96: a 'healthwait' step. Polls docker's health check and the log since this start every 2 s.
    param([string]$Container, [int]$Seconds = 300)
    $deadline = [datetime]::UtcNow.AddSeconds([Math]::Max(10, $Seconds))
    $h = $null
    do {
        $c = ConvertFrom-ContainerInspect (Invoke-DockerRead @('inspect', $Container)).Out
        $crash = $null
        if ($c.Exists -and $c.StartedAtRaw) {
            $l = Invoke-DockerRead @('logs', '--since', $c.StartedAtRaw, $Container) 60000
            $crash = Get-CrashReport ($l.Out + "`n" + $l.Err)
        }
        $h = Get-HealthState -Exists $c.Exists -Running $c.Running -Health ([string]$c.Health) -Crash $crash
        if ($h.State -notin @('STARTING')) { return $h }
        Start-Sleep -Seconds 2
    } while ([datetime]::UtcNow -lt $deadline)
    [pscustomobject]@{ State = 'TIMEOUT'; Detail = ('still not healthy after ' + $Seconds + ' s (' + $h.Detail + ')') }
}

function Get-AutopilotFacts {
    # cc-P74: what the GOLEM tile and the Golem tab read, on every refresh. Cheap on purpose:
    # three small files, the last TailLines of the log (never all of it) and one folder listing. Only
    # whether email.json and smtp-cred.xml exist is checked; neither is ever opened here. A read that
    # fails is recorded beside it, for Get-AutopilotGlance to say UNKNOWN rather than guess.
    param($Config)
    $ap = $Config.Autopilot
    $errors = New-Object 'System.Collections.Generic.List[string]'
    $enabled = $null
    try { $enabled = [bool](Test-Path -LiteralPath $ap.Enabled -PathType Leaf -ErrorAction Stop) } catch { $errors.Add('ENABLED: ' + $_.Exception.Message) }
    $stateText = $null
    if (Test-Path -LiteralPath $ap.StateFile -PathType Leaf) {
        try { $stateText = [IO.File]::ReadAllText($ap.StateFile) } catch { $errors.Add('state.json: ' + $_.Exception.Message) }
    }
    $logLines = @()
    $logExists = [bool](Test-Path -LiteralPath $ap.LogFile -PathType Leaf)
    if ($logExists) {
        try { $logLines = @(Get-Content -LiteralPath $ap.LogFile -Tail $ap.TailLines -ErrorAction Stop) } catch { $errors.Add($ap.LogName + ': ' + $_.Exception.Message) }
    }
    $names = $null
    try { $names = @(Get-ChildItem -LiteralPath $ap.Queue -File -ErrorAction Stop | ForEach-Object { $_.Name }) } catch { $errors.Add('queue: ' + $_.Exception.Message) }
    # cc-P94: status.json and settings.json. A failed read here is not an error of the tile's: the view falls
    # back to ENABLED, state.json and the log, and says why.
    $statusText = $null; $statusError = $null
    if ($ap.StatusFile -and (Test-Path -LiteralPath $ap.StatusFile -PathType Leaf)) {
        try { $statusText = [IO.File]::ReadAllText($ap.StatusFile) } catch { $statusError = $_.Exception.Message }
    }
    $settingsText = $null
    $settingsExists = [bool]($ap.SettingsFile -and (Test-Path -LiteralPath $ap.SettingsFile -PathType Leaf))
    if ($settingsExists) { try { $settingsText = [IO.File]::ReadAllText($ap.SettingsFile) } catch { } }
    $present = @{}
    foreach ($f in @($ap.Start, $ap.Stop, $ap.Setup, $ap.TestEmail, $ap.MoveScript)) { if ($f) { $present[$f] = [bool](Test-Path -LiteralPath $f -PathType Leaf) } }
    # cc-P99: the prompt running, from state.json, for Move to the Golem (which refuses while one runs).
    $running = ''
    $runningFile = ''
    if ($stateText) { try { $sj = ($stateText | ConvertFrom-Json); $running = [string]$sj.current; $runningFile = [string]$sj.currentFile } catch { } }
    foreach ($f in @($ap.LongRunStart, $ap.LongRunEnd, $ap.WrapUp, $ap.StopPoint, $ap.WakeScript, $ap.AutoWakeScript, $ap.NightShiftScript, $ap.CliCheckScript, $ap.PokeScript, $ap.RunNextScript, $ap.RetryScript, $ap.IntegrateScript, $ap.MarathonScript, $ap.MergeScript, $ap.AwayScript, $ap.TinkerScript, $ap.LaneNowScript)) { if ($f) { $present[$f] = [bool](Test-Path -LiteralPath $f -PathType Leaf) } }
    # cc-P182: Run next's pick, a queue file name ('' when none).
    $runNext = ''
    if ($ap.RunNextFile -and (Test-Path -LiteralPath $ap.RunNextFile -PathType Leaf)) { try { $runNext = ([IO.File]::ReadAllText($ap.RunNextFile)).Trim() } catch { } }
    # cc-P131: the CLI as the Golem last saw it (cli.json); the Wake button checks it afresh at the click (Get-ClaudeCliFacts).
    $cliInfo = $null
    if ($ap.CliFile -and (Test-Path -LiteralPath $ap.CliFile -PathType Leaf)) { try { $cliInfo = [IO.File]::ReadAllText($ap.CliFile) | ConvertFrom-Json } catch { } }
    $extra = $null
    try { $extra = Get-GolemExtra -Config $Config -QueueNames $names -RunningFile $runningFile -LogLines $logLines -StatusText $statusText } catch { $errors.Add('prompt details: ' + $_.Exception.Message) }
    [pscustomobject]@{
        Enabled         = $enabled
        StatusText      = $statusText
        StatusError     = $statusError
        SettingsText    = $settingsText
        SettingsExists  = $settingsExists
        StateText       = $stateText
        LogLines        = $logLines
        LogExists       = $logExists
        QueueNames      = $names
        EmailConfigured = [bool](Test-Path -LiteralPath $ap.EmailFile -PathType Leaf)
        CredStored      = [bool](Test-Path -LiteralPath $ap.CredFile -PathType Leaf)
        Present         = $present
        Running         = $running
        Errors          = $errors.ToArray()
        Extra           = $extra
        CliInfo         = $cliInfo
        RunNext         = $runNext
    }
}

function Get-ClaudeCliFacts {
    # cc-P131, SHELL. Where the Claude Code CLI is (the native install's path, then PATH) and, with -Check, whether it is
    # signed in: "claude auth status" exits 0 when signed in, 1 when not (documented). The check is one process of a few
    # seconds, so it runs only when Wake the Golem is pressed, and its answer is kept for 60 s. Writes nothing.
    param([switch]$Check)
    $path = ''
    $d = $(if ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.local\bin\claude.exe' } else { '' })
    if ($d -and (Test-Path -LiteralPath $d -PathType Leaf)) { $path = $d }
    else { $c = Get-Command claude -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c -and $c.Source) { $path = $c.Source } }
    $o = [pscustomobject]@{ Path = $path; SignedIn = $null; Note = '' }
    if (-not $path) { $o.Note = 'not installed'; return $o }
    if (-not $Check) { return $o }
    if ($script:cliFacts -and $script:cliFacts.Path -eq $path -and $script:cliFactsAt -and ([datetime]::UtcNow - $script:cliFactsAt).TotalSeconds -lt 60) { return $script:cliFacts }
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('console-auth-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
    try {
        $p = Start-Process -FilePath $path -ArgumentList 'auth status' -NoNewWindow -PassThru -RedirectStandardOutput ($tmp + '.out') -RedirectStandardError ($tmp + '.err')
        $null = $p.Handle
        if (-not $p.WaitForExit(15000)) { try { $p.Kill() } catch { }; $o.SignedIn = $false; $o.Note = 'auth status did not answer in 15 s' }
        else { $o.SignedIn = ($p.ExitCode -eq 0); if (-not $o.SignedIn) { $o.Note = 'not signed in' } }
    } catch { $o.SignedIn = $false; $o.Note = 'could not run it: ' + $_.Exception.Message }
    Remove-Item -LiteralPath ($tmp + '.out'), ($tmp + '.err') -Force -ErrorAction SilentlyContinue
    $script:cliFacts = $o; $script:cliFactsAt = [datetime]::UtcNow
    $o
}

# cc-P154: what cannot change between refreshes unless a file does. The prompt and notes heads the Golem tab shows were
# read on every refresh (every queued prompt, ten finished ones and their notes); now each is read again only when its
# write time or length changed, and a folder is listed again only when its own write time changed (a file added,
# removed or renamed in it). Both caches live where the read runs: the refresh runspace, under -BackgroundRefresh.
$script:FileHeadCache = @{}
$script:DirListCache = @{}

function Get-FileHeadCached {
    # The first $Lines lines of a file, joined by newlines; $null when it cannot be read.
    param([string]$Path, [int]$Lines = 40)
    if (-not $Path) { return $null }
    try { $fi = New-Object IO.FileInfo($Path); if (-not $fi.Exists) { return $null } } catch { return $null }
    $key = $Path + '|' + $Lines
    $stamp = [string]$fi.LastWriteTimeUtc.Ticks + '|' + $fi.Length
    $c = $script:FileHeadCache[$key]
    if ($c -and $c.Stamp -eq $stamp) { return $c.Text }
    try { $t = (Get-Content -LiteralPath $Path -TotalCount $Lines -ErrorAction Stop) -join "`n" } catch { return $null }
    $script:FileHeadCache[$key] = @{ Stamp = $stamp; Text = $t }
    $t
}

function Find-FileCached {
    # The full path of the first file (by name) in $Dir matching $Filter (a -like pattern, as Get-ChildItem -Filter is
    # used here); $null when none. The folder is listed again only when its write time changed.
    param([string]$Dir, [string]$Filter)
    if (-not $Dir) { return $null }
    try { $di = New-Object IO.DirectoryInfo($Dir); if (-not $di.Exists) { return $null } } catch { return $null }
    $stamp = $di.LastWriteTimeUtc.Ticks
    $c = $script:DirListCache[$Dir]
    if (-not $c -or $c.Stamp -ne $stamp) {
        $c = @{ Stamp = $stamp; Files = @($di.GetFiles() | Sort-Object Name | ForEach-Object { $_.FullName }) }
        $script:DirListCache[$Dir] = $c
    }
    foreach ($p in $c.Files) { if ([IO.Path]::GetFileName($p) -like $Filter) { return $p } }
    $null
}

function Get-GolemExtra {
    # cc-P125, SHELL. What Get-AutopilotView's Extra holds, read now: the head of each queued and the running prompt, the
    # completed prompt and the notes of each finished one, usage.json, the local token estimate when there is no
    # usage.json (cached 5 minutes), and whether both repos are clean (only while resting, as the tile asks then).
    param($Config, $QueueNames, [string]$RunningFile, [string[]]$LogLines, $StatusText)
    $ap = $Config.Autopilot
    # cc-P154: each head is read again only when its file's write time or length changed (Get-FileHeadCached).
    $head = { param($path) Get-FileHeadCached $path 40 }
    $prompts = @{}
    foreach ($n in @($QueueNames | Where-Object { $_ -match '^cc-P\d+-.*\.md$' })) { $prompts[$n] = & $head (Join-Path $ap.Queue $n) }
    if ($RunningFile -and $ap.PromptDir) { $prompts[$RunningFile] = & $head (Join-Path $ap.PromptDir $RunningFile) }
    $completed = @{}; $notes = @{}
    foreach ($fp in @(Get-FinishedPrompts (ConvertFrom-AutopilotLog $LogLines) 10)) {
        $k = [string]$fp.Number
        foreach ($d in @($ap.CompletedDir, $ap.PromptDir)) {
            if (-not $d -or $completed.ContainsKey($k)) { continue }
            $c = Find-FileCached $d ('cc-P' + $k + '-*.md')
            if ($c) { $completed[$k] = & $head $c }
        }
        if ($Config.NotesFolder) {
            $nf = Find-FileCached $Config.NotesFolder ('cc-P' + $k + '-*.md')
            if ($nf) { $notes[$k] = & $head $nf; $notes[$k + '.path'] = $nf }
        }
    }
    $usage = $null
    if ($ap.UsageFile -and (Test-Path -LiteralPath $ap.UsageFile -PathType Leaf)) { try { $usage = [IO.File]::ReadAllText($ap.UsageFile) } catch { } }
    $plan = $null
    if ($ap.PlanUsageFile -and (Test-Path -LiteralPath $ap.PlanUsageFile -PathType Leaf)) { try { $plan = [IO.File]::ReadAllText($ap.PlanUsageFile) } catch { } }
    $codex = $null
    if ($ap.CodexUsageFile -and (Test-Path -LiteralPath $ap.CodexUsageFile -PathType Leaf)) { try { $codex = [IO.File]::ReadAllText($ap.CodexUsageFile) } catch { } }
    $estimate = $null
    if (-not $usage) { $estimate = Get-UsageEstimate $ap.ClaudeProjects }
    $clean = $null
    $st = $null
    if ($StatusText) { try { $st = ConvertFrom-AutopilotStatus $StatusText } catch { } }
    if ($st -and $st.RestReason -and -not $st.Current) {
        $clean = $true
        foreach ($r in @($Config.CommitRepos)) {
            $g = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $r.Path, '--no-optional-locks', 'status', '--porcelain')
            if ($g.Code -ne 0) { $clean = $null; break }
            if (([string]$g.Out).Trim()) { $clean = $false }
        }
    }
    [pscustomobject]@{ PromptTexts = $prompts; CompletedTexts = $completed; NotesTexts = $notes; UsageText = $usage; PlanUsageText = $plan; CodexUsageText = $codex; Estimate = $estimate; TreeClean = $clean }
}

function Get-UsageEstimate {
    # cc-P125, SHELL. The fallback when Claude Code's status line has not written usage.json: the tokens in Claude Code's
    # own session logs (~\.claude\projects\*\*.jsonl) over the last 5 hours, counted from each answer's usage line. The
    # log format is Claude Code's internal one (not documented), so this is only ever shown as an estimate. Cached 5 min.
    param([string]$Projects)
    if ($script:usageEstimate -and $script:usageEstimateAt -and ([datetime]::UtcNow - $script:usageEstimateAt).TotalMinutes -lt 5) { return $script:usageEstimate }
    $script:usageEstimateAt = [datetime]::UtcNow
    $script:usageEstimate = $null
    if (-not $Projects -or -not (Test-Path -LiteralPath $Projects)) { return $null }
    $since = [datetime]::UtcNow.AddHours(-5)
    $tokens = [long]0; $sessions = 0
    foreach ($f in @(Get-ChildItem -LiteralPath $Projects -Recurse -Filter '*.jsonl' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTimeUtc -ge $since })) {
        $sessions++
        foreach ($m in @(Select-String -LiteralPath $f.FullName -Pattern '"output_tokens":\s*(\d+)' -AllMatches -ErrorAction SilentlyContinue)) {
            $line = $m.Line
            if ($line -match '"timestamp":"([^"]+)"') { $t = [datetime]::MinValue; if ([datetime]::TryParse($Matches[1], [ref]$t) -and $t.ToUniversalTime() -lt $since) { continue } }
            foreach ($k in 'input_tokens', 'output_tokens', 'cache_creation_input_tokens') {
                $x = [regex]::Match($line, '"' + $k + '":\s*(\d+)')
                if ($x.Success) { $tokens += [long]$x.Groups[1].Value }
            }
        }
    }
    $script:usageEstimate = [pscustomobject]@{ Tokens = $tokens; Sessions = $sessions }
    $script:usageEstimate
}

function Get-PlayTestGlanceNow {
    # cc-P83: the PLAY TESTS tile, read now. One small file; never throws.
    # cc-P107: once the pages are on Haven, the same three documents come from Haven's API; if Haven does not answer,
    # the mirror (testing\ as of the last commit) is read and the tile says so.
    param($Config)
    if ((Get-OpsMode $Config) -ceq 'haven' -and $Config.Ops.HavenPageUrl) {
        $base = $Config.Ops.HavenPageUrl.TrimEnd('/')
        try {
            $items = (Invoke-WebRequest -UseBasicParsing -Uri ($base + '/api/items') -TimeoutSec 4).Content
            $hi = $null; $hb = $null
            try { $hi = (Invoke-WebRequest -UseBasicParsing -Uri ($base + '/api/ideas') -TimeoutSec 4).Content } catch { }
            try { $hb = (Invoke-WebRequest -UseBasicParsing -Uri ($base + '/api/bugs') -TimeoutSec 4).Content } catch { }
            $g = Get-PlayTestGlance -JsonText $items -IdeasText $hi -BugsText $hb
            $age = Get-HavenSnapshotAge $Config
            $g.Detail += '; on Haven' + $(if ($null -ne $age) { ', game PC snapshot received ' + (Format-Age ([timespan]::FromSeconds($age))) + ' ago' } else { '' })
            return $g
        } catch {
            $g = Get-PlayTestGlanceFile $Config
            $g.Detail += '; from the last commit (Haven unreachable)'
            return $g
        }
    }
    Get-PlayTestGlanceFile $Config
}

function Get-HavenSnapshotAge {
    # cc-P107: seconds since Haven last received the game PC snapshot, or $null (no token, Haven down, never sent).
    param($Config)
    try {
        $tok = ([IO.File]::ReadAllText($Config.Ops.TokenFile)).Trim()
        if (-not $tok) { return $null }
        $r = Invoke-WebRequest -UseBasicParsing -Uri ($Config.Ops.OpsApi.TrimEnd('/') + '/api/gamepc') -Headers @{ Authorization = 'Bearer ' + $tok } -TimeoutSec 4
        $a = ($r.Content | ConvertFrom-Json).ageSeconds
        if ($null -eq $a) { return $null }
        return [int]$a
    } catch { return $null }
}

function Get-PlayTestGlanceFile {
    param($Config)
    $text = $null; $err = $null
    try { if (Test-Path -LiteralPath $Config.PlayTestsFile -PathType Leaf) { $text = [IO.File]::ReadAllText($Config.PlayTestsFile) } }
    catch { $err = $_.Exception.Message }
    # cc-P90: feature-ideas.json sits beside play-tests.json.
    $ideas = $null
    try {
        $ip = Join-Path (Split-Path -Parent $Config.PlayTestsFile) 'feature-ideas.json'
        if (Test-Path -LiteralPath $ip -PathType Leaf) { $ideas = [IO.File]::ReadAllText($ip) }
    } catch { }
    # cc-P97: and bug-reports.json.
    $bugs = $null
    try {
        $bp = Join-Path (Split-Path -Parent $Config.PlayTestsFile) 'bug-reports.json'
        if (Test-Path -LiteralPath $bp -PathType Leaf) { $bugs = [IO.File]::ReadAllText($bp) }
    } catch { }
    Get-PlayTestGlance -JsonText $text -ReadError $err -IdeasText $ideas -BugsText $bugs
}

function Get-AutopilotViewNow {
    # The facts read now, judged. Never throws: a failure is an UNKNOWN tile.
    param($Config)
    try {
        $f = Get-AutopilotFacts $Config
        $v = Get-AutopilotView -Enabled $f.Enabled -StateText $f.StateText -LogLines $f.LogLines -QueueNames $f.QueueNames -Errors $f.Errors -StatusText $f.StatusText -StatusError $f.StatusError -SettingsText $f.SettingsText -Kind ([string]$Config.Autopilot.Kind) -Extra $f.Extra -RunNext $f.RunNext
    } catch {
        $f = $null
        $v = Get-AutopilotView -Enabled $null -StateText $null -LogLines @() -QueueNames $null -Errors @($_.Exception.Message)
    }
    [pscustomobject]@{ Facts = $f; View = $v; Tile = (Get-AutopilotGlance -View $v -Now ([datetime]::Now)) }
}

function Read-WipePassword {
    # The new admin password, typed twice in this window and not shown. $null is a cancel: empty, or
    # the two differ. The plaintext lives in this process only, as long as New-WipePasswordHash needs it.
    param([string]$Admin)
    $a = Read-Host -AsSecureString ("  New password for '" + $Admin + "' (empty cancels)")
    if ($a.Length -eq 0) { return $null }
    $b = Read-Host -AsSecureString '  The same password again'
    $pa = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($a)
    $pb = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($b)
    try {
        $x = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pa)
        $y = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pb)
        if ($x -cne $y) { Write-ConsoleLine '  The two passwords differ.' 'amber'; return $null }
        return $x
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pa)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pb)
    }
}

function Get-CommandSourceFiles {
    # Every .cs under server/customizations, path relative to it -> text, for
    # ConvertFrom-CommandSources. Read only.
    param([string]$Root)
    $h = @{}
    if (-not (Test-Path -LiteralPath $Root)) { return $h }
    $base = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('') + ''
    foreach ($f in Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.cs') {
        $h[$f.FullName.Substring($base.Length)] = [IO.File]::ReadAllText($f.FullName)
    }
    $h
}

function Get-FileListing {
    param([string]$Dir)
    if (-not (Test-Path -LiteralPath $Dir)) { return '' }
    $root = (Resolve-Path -LiteralPath $Dir).Path.TrimEnd('\') + '\'
    $lines = foreach ($f in Get-ChildItem -LiteralPath $Dir -Recurse -File -Force) {
        '{0}|{1}|{2}' -f (Format-DockerTime $f.LastWriteTimeUtc), $f.Length, $f.FullName.Substring($root.Length)
    }
    @($lines) -join "`n"
}

function Get-TreeTotals {
    param([string]$Dir)
    $files = @(Get-ChildItem -LiteralPath $Dir -Recurse -File -Force -ErrorAction Stop)
    $bytes = 0L
    foreach ($f in $files) { $bytes += $f.Length }
    [pscustomobject]@{ Files = $files.Count; Bytes = $bytes }
}

function Get-SnapshotListingText {
    param([string]$Dir)
    if (-not (Test-Path -LiteralPath $Dir)) { return '' }
    $lines = foreach ($d in Get-ChildItem -LiteralPath $Dir -Directory) {
        if ($d.Name -cnotmatch $script:SnapshotPattern) { continue }
        $t = Get-TreeTotals $d.FullName
        $acc = 0
        if (Test-Path -LiteralPath (Join-Path $d.FullName 'Accounts')) { $acc = 1 }
        '{0}|{1}|{2}|{3}' -f $d.Name, $t.Bytes, $t.Files, $acc
    }
    @($lines) -join "`n"
}

function Get-DockerSections {
    # Everything the status panel reads, gathered read-only. -Capture writes exactly this.
    param($Config)
    $sec = [ordered]@{}
    $sec['meta'] = 'captured-at ' + (Format-DockerTime ([datetime]::UtcNow)) + "`nsource docker"
    $refs = New-Object 'System.Collections.Generic.List[string]'
    $refs.Add($Config.LatestTag)
    # cc-P108: a retired shard is never read: no docker inspect, no logs, no save or snapshot listing.
    $active = @($Config.Shards.Values | Where-Object { -not $_.Retired })
    foreach ($sh in $active) {
        $r = Invoke-DockerRead @('inspect', $sh.Container)
        $sec['inspect ' + $sh.Container] = ($r.Out + "`n" + $r.Err).Trim()
        $c = ConvertFrom-ContainerInspect $sec['inspect ' + $sh.Container]
        if ($c.Exists) {
            foreach ($ref in @($c.ConfigImage, $c.ImageId)) { if ($ref -and -not $refs.Contains($ref)) { $refs.Add($ref) } }
            $l = Invoke-DockerRead (Get-LogWindowArgs $c $Config.LogWindowMinutes)
            $sec['logs ' + $sh.Container] = ($l.Out + "`n" + $l.Err).Trim()
            # cc-P106: the last lines with docker's timestamps: a console question, and how long the shard has been quiet.
            $t = Invoke-DockerRead @('logs', '-t', '--tail', [string]$Config.QuestionTailLines, $sh.Container)
            $sec['logtail ' + $sh.Container] = ($t.Out + "`n" + $t.Err).Trim()
        }
    }
    foreach ($ref in $refs) {
        $r = Invoke-DockerRead @('image', 'inspect', $ref)
        $sec['image ' + $ref] = ($r.Out + "`n" + $r.Err).Trim()
    }
    # cc-P63: the images Deploy built image chooses from.
    $r = Invoke-DockerRead @('images', 'sl-modernuo', '--format', '{{.Repository}}:{{.Tag}}|{{.ID}}|{{.CreatedAt}}|{{.Size}}')
    $sec['images sl-modernuo'] = ($r.Out + "`n" + $r.Err).Trim()
    foreach ($sh in $active) {
        $sec['saves ' + $sh.Key] = Get-FileListing $sh.Saves
        $sec['snapshots ' + $sh.Key] = Get-SnapshotListingText (Join-Path $Config.SnapshotRoot $sh.Key)
        $parent = Split-Path $sh.Saves -Parent
        $leaf = Split-Path $sh.Saves -Leaf
        $sec['asides ' + $sh.Key] = @(Get-ChildItem -LiteralPath $parent -Directory -Filter ($leaf + '.aside-*') -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) -join "`n"
    }
    if (-not (Test-LiveRetired $Config) -and (Test-Path -LiteralPath $Config.LiveCompose)) { $sec['compose live'] = [IO.File]::ReadAllText($Config.LiveCompose) }
    # The status publisher: the file it writes, the feed that serves it, and what the feed says.
    $p = $Config.Publisher
    $r = Invoke-DockerRead @('inspect', $p.FeedContainer)
    $sec['inspect ' + $p.FeedContainer] = ($r.Out + "`n" + $r.Err).Trim()
    if (Test-Path -LiteralPath $p.File) {
        $fi = Get-Item -LiteralPath $p.File -Force
        $sec['status meta'] = (Format-DockerTime $fi.LastWriteTimeUtc) + '|' + $fi.Length
        try { $sec['status file'] = [IO.File]::ReadAllText($p.File) } catch { $sec['status file'] = '' }
    } else {
        $sec['status meta'] = 'missing'
        $sec['status file'] = ''
    }
    $sec['feed http'] = Get-FeedHttpCode $p.FeedUrl
    $sec
}

function Get-FeedHttpCode {
    # The HTTP status sl-uo-status answers with, as text: 200, 503, or why there was no answer.
    param([string]$Url)
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3 -ErrorAction Stop
        return [string][int]$resp.StatusCode
    } catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { $code = $null }
        if ($code) { return [string]$code }
        return 'no answer within 3 s'
    }
}

function Get-ConsoleState {
    param($Config, [switch]$DryRun, [string]$StatusFrom)
    if ($DryRun) {
        $file = $StatusFrom
        if (-not $file) { $file = $Config.DefaultCapture }
        return (New-ConsoleState -Config $Config -Sections (ConvertFrom-CaptureFile ([IO.File]::ReadAllText($file))) -Source ('capture file ' + (Split-Path $file -Leaf) + ' (DRY RUN, not docker)'))
    }
    $s = New-ConsoleState -Config $Config -Sections (Get-DockerSections $Config) -Source 'docker'
    $s.Now = [datetime]::UtcNow
    $s
}

function New-ExecOutputFile {
    # One file per run that has exec steps, in shard-console-output\ beside shard-console.log, so
    # a failure can be read after its window is closed (D38). Ignored by git as *.log.
    param($Config, [string]$ActionKey, [string]$ShardKey)
    $dir = Join-Path (Split-Path -Parent $Config.LogFile) 'shard-console-output'
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $name = (Get-Date).ToString('yyyyMMdd-HHmmss', $script:Invariant) + '_' + $ActionKey + '_' + $ShardKey + '.log'
    Join-Path $dir ($name -replace '[^A-Za-z0-9._-]', '_')
}

function Invoke-ExecStep {
    # Runs one command with its stdout and stderr on the screen as they arrive (D38: they used to
    # become Invoke-Plan's return value) and in OutFile. Returns only the exit code.
    # 2>&1 on a native command wraps each stderr line in an ErrorRecord, and under
    # ErrorActionPreference Stop the first one throws, so the preference is Continue here: docker
    # writes its progress to stderr. Failure is the exit code, never stderr.
    param([string]$Exe, [string[]]$Arguments, [string]$OutFile, [string]$Header)
    $w = New-Object IO.StreamWriter($OutFile, $true, (New-Object Text.UTF8Encoding $false))
    $w.AutoFlush = $true
    $eap = $ErrorActionPreference
    try {
        $w.WriteLine('== ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant) + ' ' + $Header)
        $ErrorActionPreference = 'Continue'
        & $Exe @Arguments 2>&1 | ForEach-Object {
            $t = [string]$_
            if ($_ -is [Management.Automation.ErrorRecord]) { $t = $_.Exception.Message }
            $w.WriteLine($t)
            Write-ConsoleLine $t 'normal'
        }
        $code = $LASTEXITCODE
        $w.WriteLine('== exited with ' + $code)
    } finally {
        $ErrorActionPreference = $eap
        $w.Dispose()
    }
    $code
}

function Invoke-ScriptStep {
    # A 'script' step (cc-P60): the PowerShell script runs in this process with its parameters
    # splatted by name, so a commit message with quotes, $ or ; reaches it unchanged. As with an
    # exec step (D38) everything it prints is on screen and in OutFile, its Write-Host lines in
    # their own colours, so its OK, BLOCKED or FAILED reads the same as in a window of its own.
    # The result is its exit code: what it passed to exit, else 0 unless a native command it ran
    # last failed.
    param([string]$Path, [Collections.IDictionary]$Named, [string]$OutFile, [string]$Header)
    $splat = @{}
    if ($Named) { foreach ($k in $Named.Keys) { $splat[$k] = $Named[$k] } }
    $colors = @{ Red = 'red'; DarkRed = 'red'; Green = 'green'; DarkGreen = 'green'; Yellow = 'amber'; DarkYellow = 'amber'; Cyan = 'head'; DarkCyan = 'head'; Gray = 'normal'; DarkGray = 'gray' }
    $w = New-Object IO.StreamWriter($OutFile, $true, (New-Object Text.UTF8Encoding $false))
    $w.AutoFlush = $true
    $eap = $ErrorActionPreference
    $fmt = New-Object 'System.Collections.Generic.List[object]'
    try {
        $w.WriteLine('== ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant) + ' ' + $Header)
        $ErrorActionPreference = 'Continue'
        $global:LASTEXITCODE = 0
        & $Path @splat *>&1 | ForEach-Object {
            $o = $_
            $color = 'normal'
            if ($o.GetType().FullName -like 'Microsoft.PowerShell.Commands.Internal.Format.*') {
                # Format-Table arrives as pieces; render them together once the table ends.
                $fmt.Add($o)
                if ($o.GetType().Name -ne 'FormatEndData') { return }
                $lines = @($fmt | Out-String -Stream -Width 160)
                $fmt.Clear()
            } elseif ($o -is [Management.Automation.InformationRecord]) {
                $lines = @([string]$o.MessageData)
                $fc = $null
                try { $fc = [string]$o.MessageData.ForegroundColor } catch { $fc = $null }
                if ($fc -and $colors.ContainsKey($fc)) { $color = $colors[$fc] }
            } elseif ($o -is [Management.Automation.ErrorRecord]) {
                $lines = @($o.Exception.Message)
            } else {
                $lines = @([string]$o)
            }
            foreach ($t in $lines) { $w.WriteLine($t); Write-ConsoleLine $t $color }
        }
        $code = $global:LASTEXITCODE
        if ($null -eq $code) { $code = 0 }
        $w.WriteLine('== exited with ' + $code)
    } finally {
        $ErrorActionPreference = $eap
        $w.Dispose()
    }
    $code
}

function Invoke-Plan {
    # Runs a plan, one step at a time, saying what each step is before it runs it. Stops at the
    # first failure and says what has already been moved, so nothing is left somewhere unknown.
    param($Plan, $Config, [string]$ActionKey, [string]$ShardKey, [string]$ConfirmLive, [switch]$Yes)
    $plan = @($Plan)
    $viol = @(Test-PlanSafety $plan $Config)
    if ($viol.Count) { foreach ($x in $viol) { Write-ConsoleLine ('REFUSED BY THE SAFETY CHECK: ' + $x) 'red' }; return $false }
    $refuse = @($plan | Where-Object { $_.Kind -eq 'refuse' })
    if ($refuse.Count) { foreach ($x in $refuse) { Write-ConsoleLine $x.Text 'amber' }; return $false }
    foreach ($s in $plan) {
        if ($s.Kind -eq 'confirm' -and -not (Test-TypedConfirmation $s.Phrase $ConfirmLive)) {
            Write-ConsoleLine ("Refused: this needs the typed confirmation '" + $s.Phrase + "' (-ConfirmLive '" + $s.Phrase + "')." ) 'red'
            return $false
        }
        if ($s.Kind -eq 'ask' -and -not $Yes) {
            Write-ConsoleLine ('Refused: ' + $s.Text + ' Pass -Yes to answer yes.') 'red'
            return $false
        }
    }
    $logged = @($plan | Where-Object { $_.Kind -eq 'log' }).Count -gt 0
    if ($logged) { Write-ConsoleLog $Config ('START ' + $ActionKey + ' shard=' + $ShardKey) }
    $moved = New-Object 'System.Collections.Generic.List[string]'
    $outFile = $null
    # A checkzip step finds the zip a build step in this plan wrote by its time (cc-P63). Less a
    # second, for file systems that round a write time down.
    $planStarted = [datetime]::UtcNow.AddSeconds(-1)
    $script:PlanCancelled = $false
    $checkFailed = $false
    $wipeHash = $null
    $wipePlaced = $false
    $i = 0
    foreach ($s in $plan) {
        $i++
        $tag = '[' + $i + '/' + $plan.Count + '] '
        try {
            switch ($s.Kind) {
                'confirm' { Write-ConsoleLine ($tag + 'typed confirmation given.') 'gray' }
                'ask'     { Write-ConsoleLine ($tag + 'confirmed.') 'gray' }
                'say'     { Write-ConsoleLine ($tag + $s.Text) 'normal' }
                'log'     { Write-ConsoleLog $Config ('DONE  ' + $s.Text); Write-ConsoleLine ($tag + 'logged to ' + $Config.LogFile) 'gray' }
                'exec' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $line = Format-CommandLine $s.Exe $s.Arguments
                    Write-ConsoleLine ('      > ' + $line) 'gray'
                    if (-not $outFile) { $outFile = New-ExecOutputFile $Config $ActionKey $ShardKey }
                    $code = Invoke-ExecStep -Exe $s.Exe -Arguments @($s.Arguments) -OutFile $outFile -Header ($tag + $line)
                    # cc-P135: a step may name one exit code that means "found something" (validate-art exits 1 when it
                    # finds problems). That is said, in amber, and the plan goes on; any other non-zero exit fails it.
                    if ($code -ne 0 -and $s.Named -and $s.Named.Contains('ReportExit') -and $code -eq [int]$s.Named['ReportExit']) { Write-ConsoleLine ('      exited with ' + $code + ': it found something; read the lines above. Not a failure.') 'amber' }
                    elseif ($code -ne 0) { throw ($line + ' exited with ' + $code) }
                }
                'script' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $line = Format-ScriptCall $s.Path $s.Named
                    Write-ConsoleLine ('      > ' + $line) 'gray'
                    if (-not (Test-Path -LiteralPath $s.Path -PathType Leaf)) { throw ('not found: ' + $s.Path) }
                    if (-not $outFile) { $outFile = New-ExecOutputFile $Config $ActionKey $ShardKey }
                    $code = Invoke-ScriptStep -Path $s.Path -Named $s.Named -OutFile $outFile -Header ($tag + $line)
                    $isLegionPublish = ($Config.LegionPublish -and [IO.Path]::GetFullPath($s.Path) -ieq [IO.Path]::GetFullPath($Config.LegionPublish) -and $s.Named -and [string]$s.Named['Mode'] -in @('Dev', 'Promote'))
                    if ($isLegionPublish) {
                        # cc-P144: Publish-LegionScripts.ps1 exits 3 when its "Type yes" was declined, as the package's does.
                        if ($code -eq 3) {
                            Write-ConsoleLine '      Cancelled at its "Type yes". Nothing was published.' 'amber'
                            $script:PlanCancelled = $true
                            if ($logged) { Write-ConsoleLog $Config ('CANCELLED ' + $ActionKey + ' shard=' + $ShardKey + ': Publish-LegionScripts.ps1 exited 3, nothing was published') }
                            return $false
                        }
                        if ($code -ne 0) { throw ($line + ' exited with ' + $code + '. Read its last lines above.') }
                        Write-ConsoleLine '      Publish-LegionScripts.ps1 exited 0.' 'green'
                    } elseif ([IO.Path]::GetFullPath($s.Path) -ieq [IO.Path]::GetFullPath($Config.PublishScript)) {
                        # cc-P68 Part B: 3 is a declined "Type yes", which is neither done nor failed.
                        $o = Get-PublishOutcome $code
                        if ($o.Result -eq 'failed') { throw $o.Text }
                        Write-ConsoleLine ('      ' + $o.Text) $o.Color
                        if ($o.Result -eq 'cancelled') {
                            $script:PlanCancelled = $true
                            if ($logged) { Write-ConsoleLog $Config ('CANCELLED ' + $ActionKey + ' shard=' + $ShardKey + ': the publish script exited 3, nothing was published') }
                            return $false
                        }
                    } elseif ($code -ne 0) { throw ($line + ' exited with ' + $code) }
                }
                'gatecheck' {
                    # cc-P68 Part A. Every gate line the script prints is shown; a gate that throws is
                    # shown in red; a zip with no version.json fails gate 5 here, because -CheckZip
                    # skips gate 5 when there is none. The last line is PASSED or FAILED.
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $named = [ordered]@{ CheckZip = $s.From }
                    $line = Format-ScriptCall $s.Path $named
                    Write-ConsoleLine ('      > ' + $line) 'gray'
                    if (-not (Test-Path -LiteralPath $s.Path -PathType Leaf)) { throw ('not found: ' + $s.Path) }
                    if (-not $outFile) { $outFile = New-ExecOutputFile $Config $ActionKey $ShardKey }
                    $why = New-Object 'System.Collections.Generic.List[string]'
                    try {
                        $code = Invoke-ScriptStep -Path $s.Path -Named $named -OutFile $outFile -Header ($tag + $line)
                        if ($code -ne 0) { $why.Add($line + ' exited with ' + $code) }
                    } catch {
                        $why.Add($_.Exception.Message)
                        foreach ($l in @(([string]$_.Exception.Message) -split "`n")) { Write-ConsoleLine ('  ' + $l.TrimEnd("`r")) 'red' }
                    }
                    $jp = $s.From -replace '\.zip$', '.version.json'
                    if (-not (Test-Path -LiteralPath $jp -PathType Leaf)) {
                        $g5 = 'GATE 5 failed: there is no ' + (Split-Path $jp -Leaf) + ' beside the zip, so launchers could not be told about it.'
                        $why.Add($g5)
                        Write-ConsoleLine ('  ' + $g5) 'red'
                    }
                    $zn = Split-Path $s.From -Leaf
                    if ($why.Count) { Write-ConsoleLine ('FAILED: ' + $zn + ' did not pass the package gates (' + $why.Count + ' problem(s) above).') 'red'; $checkFailed = $true }
                    else { Write-ConsoleLine ('PASSED: ' + $zn + ' passed gates 4, 6 and 5.') 'green' }
                }
                'checkzip' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $new = @(Get-PackageZips $s.To | Where-Object { $_.LastWriteUtc -ge $planStarted })
                    if ($new.Count -ne 1) { throw ('expected one zip in ' + $s.To + ' written since this plan started, found ' + $new.Count + ': ' + (@($new | ForEach-Object { $_.Name }) -join ', ')) }
                    $z = $new[0]
                    $line = Format-ScriptCall $s.Path ([ordered]@{ CheckZip = $z.FullName })
                    Write-ConsoleLine ('      > ' + $line) 'gray'
                    if (-not $outFile) { $outFile = New-ExecOutputFile $Config $ActionKey $ShardKey }
                    $code = Invoke-ScriptStep -Path $s.Path -Named ([ordered]@{ CheckZip = $z.FullName }) -OutFile $outFile -Header ($tag + $line)
                    if ($code -ne 0) { throw ($line + ' exited with ' + $code) }
                    $nt = '(none)'
                    if ($z.Json.Notes) { $nt = $z.Json.Notes }
                    Write-ConsoleLine ('      zip     ' + $z.FullName) 'green'
                    Write-ConsoleLine ('      version ' + $z.Json.Version + ', ' + (Format-Bytes $z.Bytes) + '. What is new: ' + $nt) 'green'
                    Write-ConsoleLine '      gates   1 to 6 passed in the build, and 4, 6 and 5 again on the zip just above. Nothing is published: Player package > Publish package does that.' 'green'
                }
                'stopped' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $r = Invoke-DockerRead @('inspect', '-f', '{{.State.Running}}', $s.Container)
                    if ($r.Out.Trim() -eq 'true') { throw ($s.Container + ' is still running') }
                }
                'answer' {
                    # cc-P106: the question is read again now; the keys go only to the one this plan was made for.
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $nowQ = Get-ShardQuestionNow -Config $Config -Container $s.Container
                    if (-not (Test-QuestionStillAsked -Signature $s.Signature -Question $nowQ)) { throw ('the question has changed or was answered since this plan was made; nothing was sent. Show the question reads it again.') }
                    $send = @(@($nowQ.Answers) | Where-Object { $_.Key -ceq $s.Phrase })[0]
                    if (-not $send) { throw ($s.Phrase + ' is not an answer to ' + $nowQ.Prompt + '; nothing was sent') }
                    $sentAt = [datetime]::UtcNow
                    Send-ContainerInput -Container $s.Container -Text (Get-AnswerKeystrokes $send.Send)
                    Write-ConsoleLine ('      sent. What the shard printed in the next 10 s:') 'gray'
                    Start-Sleep -Seconds 10
                    $after = Invoke-DockerRead @('logs', '--since', (Format-DockerTime $sentAt.AddSeconds(-1)), $s.Container)
                    foreach ($l in @(((Remove-Ansi ($after.Out + "`n" + $after.Err)) -replace "`r", '' -split "`n") | Where-Object { $_.Trim() } | Select-Object -First 30)) { Write-ConsoleLine ('      | ' + $l) 'gray' }
                }
                'healthwait' {
                    # cc-P96: healthy, or stop here and say why (crashed, unhealthy, stopped, no health check, timeout).
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $h = Wait-TestShardHealth -Container $s.Container -Seconds $s.Seconds
                    if ($h.State -eq 'HEALTHY') { Write-ConsoleLine ('      HEALTHY: ' + $h.Detail) 'green' }
                    else { throw ($s.Container + ' is ' + $h.State + ': ' + $h.Detail) }
                }
                'move' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    if (-not (Test-Path -LiteralPath $s.From)) {
                        if ($s.IfExists) { Write-ConsoleLine ('      nothing at ' + $s.From + '; nothing to move.') 'gray'; break }
                        throw ('there is nothing at ' + $s.From)
                    }
                    if (Test-Path -LiteralPath $s.To) { throw ($s.To + ' already exists; not moving over it') }
                    Write-ConsoleLine ('      ' + $s.From + '  ->  ' + $s.To) 'gray'
                    Move-Item -LiteralPath $s.From -Destination $s.To -ErrorAction Stop
                    $moved.Add($s.From + '  ->  ' + $s.To)
                }
                'copy' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    if (-not (Test-Path -LiteralPath $s.From)) { throw ('there is nothing at ' + $s.From) }
                    if (Test-Path -LiteralPath $s.To) { throw ($s.To + ' already exists; not copying over it') }
                    $parent = Split-Path $s.To -Parent
                    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
                    Write-ConsoleLine ('      ' + $s.From + '  ->  ' + $s.To) 'gray'
                    Copy-Item -LiteralPath $s.From -Destination $s.To -Recurse -ErrorAction Stop
                }
                'verify' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $a = Get-TreeTotals $s.From
                    $b = Get-TreeTotals $s.To
                    Write-ConsoleLine ('      ' + $a.Files + ' files, ' + $a.Bytes + ' bytes  vs  ' + $b.Files + ' files, ' + $b.Bytes + ' bytes') 'gray'
                    if ($a.Files -ne $b.Files -or $a.Bytes -ne $b.Bytes) { throw ('the copy at ' + $s.To + ' does not match ' + $s.From) }
                }
                'remove-snapshot' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    Write-ConsoleLine ('      ' + $s.Path) 'gray'
                    Remove-Item -LiteralPath $s.Path -Recurse -Force -ErrorAction Stop
                }
                'secret' {
                    # cc-P73: nothing has changed before this step, so a cancel here leaves everything as it was.
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $pw = Read-WipePassword ([string]$s.Named['Admin'])
                    if (-not $pw) {
                        Write-ConsoleLine 'Wipe cancelled at the password. Nothing was changed.' 'amber'
                        $script:PlanCancelled = $true
                        if ($logged) { Write-ConsoleLog $Config ('CANCELLED ' + $ActionKey + ' shard=' + $ShardKey + ': no password given, nothing changed') }
                        return $false
                    }
                    $wipeHash = New-WipePasswordHash $pw
                    $pw = $null
                    Write-ConsoleLine '      password taken; only its hash is kept.' 'gray'
                }
                'wiperequest' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    if (-not $wipeHash) { throw 'there is no password hash: the password step did not run' }
                    if (Test-Path -LiteralPath $s.Path) { throw ($s.Path + ' already exists; not writing over it') }
                    $rid = (Get-Date).ToString('yyyyMMdd-HHmmss', $script:Invariant) + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
                    $body = New-WipeRequestText -Admin ([string]$s.Named['Admin']) -Hash $wipeHash -Id $rid -Now (Get-Date) -By ([Security.Principal.WindowsIdentity]::GetCurrent().Name)
                    [IO.File]::WriteAllText($s.Path, $body, (New-Object Text.UTF8Encoding $false))
                    $wipeHash = $null
                    $wipePlaced = $true
                    Write-ConsoleLine ('      request ' + $rid + ' placed at ' + $s.Path) 'gray'
                }
                'wipewait' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $since = (Invoke-DockerRead @('inspect', '-f', '{{.State.StartedAt}}', $s.Container)).Out.Trim()
                    $seen = $null
                    $deadline = (Get-Date).AddSeconds($Config.WipeWaitSeconds)
                    while ((Get-Date) -lt $deadline) {
                        $l = Invoke-DockerRead @('logs', '--since', $since, $s.Container) 60000
                        $seen = ConvertFrom-WipeLog ($l.Out + "`n" + $l.Err)
                        if ($seen.Outcome) { break }
                        Start-Sleep -Seconds 3
                    }
                    $left = Test-Path -LiteralPath $s.Path
                    if ($left) {
                        # Whatever happened, no hash stays on disk once this run is over.
                        Remove-Item -LiteralPath $s.Path -Force -ErrorAction Stop
                        Write-ConsoleLine ('      ' + $s.Path + ' was still there; removed, so no password hash stays on disk.') 'amber'
                    }
                    if (-not $seen -or -not $seen.Outcome) { throw ('the shard said nothing about the wipe within ' + $Config.WipeWaitSeconds + ' s. Read: docker logs ' + $s.Container + '. The backup is the aside named in this window.') }
                    if ($seen.Outcome -ne 'done') { throw $seen.Line }
                    if ($left) { throw ('the shard wiped (' + $seen.Line + ') but left its request behind, so its save may not have finished. Read: docker logs ' + $s.Container) }
                    Write-ConsoleLine ('      ' + $seen.Line) 'green'
                    if ($seen.Audit) { Write-ConsoleLine ('      ' + $seen.Audit) 'green' }
                    if ($seen.Census) { Write-ConsoleLine ('      the test shard now has ' + $seen.Census.Accounts + ' account(s), ' + $seen.Census.Characters + ' character(s), ' + $seen.Census.Houses + ' player house(s).') 'green' }
                }
                'window' {
                    if ($script:WindowStepsInline) {
                        # cc-P182: the web console (Invoke-WebConsoleAction.ps1). The same command, run in this process like an exec
                        # step: its output on stdout as it comes and in the action's transcript, its exit code judged.
                        Write-ConsoleLine ($tag + 'Running here for the web console: ' + (Format-CommandLine $s.Exe $s.Arguments)) 'head'
                        if (-not $outFile) { $outFile = New-ExecOutputFile $Config $ActionKey $ShardKey }
                        $code = Invoke-ExecStep -Exe $s.Exe -Arguments @($s.Arguments) -OutFile $outFile -Header ($tag + (Format-CommandLine $s.Exe $s.Arguments))
                        if ($code -ne 0) { throw ((Split-Path -Leaf ([string]@($s.Arguments)[4])) + ' exited with ' + $code + ': read its lines above.') }
                        break
                    }
                    Write-ConsoleLine ($tag + $s.Text) 'normal'
                    $cmd ="`$host.UI.RawUI.WindowTitle = '" + ((Format-CommandLine $s.Exe $s.Arguments) -replace "'", "''") + "'; " + (Format-CommandLine $s.Exe $s.Arguments)
                    Start-Process -FilePath $Config.PowerShell -ArgumentList ('-NoProfile -NoExit -Command ' + $cmd) | Out-Null
                }
                'launch' {
                    Write-ConsoleLine ($tag + $s.Text) 'normal'
                    if ($s.Path -ne 'explorer.exe' -and -not (Test-Path -LiteralPath $s.Path)) { throw ('not found: ' + $s.Path) }
                    if (@($s.Arguments).Count) { Start-Process -FilePath $s.Path -ArgumentList $s.Arguments | Out-Null }
                    else { Start-Process -FilePath $s.Path | Out-Null }
                }
            }
        } catch {
            Write-ConsoleLine ('FAILED at step ' + $i + ': ' + $_.Exception.Message) 'red'
            # cc-P73: a wipe request this run placed never outlives it, so no password hash stays on disk.
            # If the shard had not read it yet, nothing was wiped; if it had, it carries on without it.
            foreach ($wr in @($plan | Where-Object { $_.Kind -eq 'wiperequest' })) {
                if ($wipePlaced -and (Test-Path -LiteralPath $wr.Path)) {
                    try { Remove-Item -LiteralPath $wr.Path -Force -ErrorAction Stop; Write-ConsoleLine ('Removed the wipe request ' + $wr.Path + ' so no hash stays on disk.') 'red' }
                    catch { Write-ConsoleLine ('Could not remove the wipe request ' + $wr.Path + ': ' + $_.Exception.Message + '. Delete it by hand.') 'red' }
                }
            }
            if ($moved.Count) {
                Write-ConsoleLine 'Already moved (nothing was deleted). To undo, rename these back in Explorer:' 'red'
                foreach ($m in $moved) { Write-ConsoleLine ('      ' + $m) 'red' }
            }
            Write-ConsoleLine 'Nothing after that step was run.' 'red'
            if ($outFile) { Write-ConsoleLine ('Everything the commands printed is kept in ' + $outFile) 'red' }
            if ($logged) { Write-ConsoleLog $Config ('FAILED ' + $ActionKey + ' shard=' + $ShardKey + ' at step ' + $i + ': ' + $_.Exception.Message) }
            return $false
        }
    }
    if ($checkFailed) { return $false }
    $true
}

# --- cc-P84: settings, the native helper, gump art, the theme, tooltips and the Action output pane --------------

function Read-ConsoleSettings {
    param($Config)
    $text = $null
    try { if (Test-Path -LiteralPath $Config.SettingsFile -PathType Leaf) { $text = [IO.File]::ReadAllText($Config.SettingsFile) } } catch { $text = $null }
    ConvertFrom-ConsoleSettings $text
}

function Save-ConsoleSettings {
    param($Config, $Settings)
    try {
        [IO.File]::WriteAllText($Config.SettingsFile, (ConvertTo-ConsoleSettings $Settings), (New-Object Text.UTF8Encoding $false))
        $true
    } catch {
        Write-ConsoleLine ('Could not save ' + $Config.SettingsFile + ': ' + $_.Exception.Message) 'amber'
        $false
    }
}

# The C# behind the pane (reading a process without blocking the window) and the gump reader. Compiled on first
# use only: the pane's first job, or a gump missing from the cache. A normal start with a full cache compiles nothing.
$script:NativeSource = @'
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ShardConsoleNative
{
    // cc-P84: one action's process, read without blocking the window. stdout and stderr arrive on thread-pool
    // threads through the Process events and wait in Lines ("O" or "E" and the line); the console's 150 ms timer
    // takes them on its own thread. stdin is closed at once: the pane has no keyboard, so a prompt fails instead of
    // waiting forever (the interactive actions never come here; this is the second guard).
    public sealed class PaneJob
    {
        public readonly Process Proc;
        public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
        private volatile bool _outDone;
        private volatile bool _errDone;

        public PaneJob(string exe, string args, string envName)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            psi.CreateNoWindow = true;
            if (!String.IsNullOrEmpty(envName)) { psi.EnvironmentVariables[envName] = "1"; }
            Proc = new Process();
            Proc.StartInfo = psi;
            Proc.EnableRaisingEvents = true;
            Proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data == null) { _outDone = true; } else { Lines.Enqueue("O" + e.Data); } };
            Proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data == null) { _errDone = true; } else { Lines.Enqueue("E" + e.Data); } };
            Proc.Start();
            Proc.StandardInput.Close();
            Proc.BeginOutputReadLine();
            Proc.BeginErrorReadLine();
        }

        public int Id { get { return Proc.Id; } }

        // Ended, and both streams read to their end, so no line is still on its way.
        public bool Finished { get { return _outDone && _errDone && Proc.HasExited; } }

        public int ExitCode { get { return Proc.ExitCode; } }

        // Ends the process and every process it started (taskkill /T). Returns taskkill's exit code.
        public int KillTree()
        {
            try
            {
                var psi = new ProcessStartInfo("taskkill.exe", "/PID " + Proc.Id + " /T /F");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                var k = Process.Start(psi);
                k.WaitForExit(15000);
                return k.ExitCode;
            }
            catch (Exception) { return -1; }
        }
    }

    // cc-P84: reads one gump from the classic client's gumpartLegacyMUL.uop. Ported from TazUO at 3212623f
    // (ClassicUO.IO/UOFileUop.cs CreateHash, ClassicUO.Utility/BwtDecompress.cs; BSD 2-Clause, Copyright (c) 2025,
    // andreakarasho), and the gump format from ClassicUO.Assets/GumpsLoader.cs. The art is EA's: it is written only
    // to the console's git-ignored cache, never to the repo.
    public static class Gumps
    {
        public static ulong CreateHash(string s)
        {
            uint eax, ecx, edx, ebx, esi, edi;
            eax = ecx = edx = ebx = esi = edi = 0;
            ebx = edi = esi = (uint)s.Length + 0xDEADBEEF;
            int i = 0;
            for (i = 0; i + 12 < s.Length; i += 12)
            {
                edi = (uint)((s[i + 7] << 24) | (s[i + 6] << 16) | (s[i + 5] << 8) | s[i + 4]) + edi;
                esi = (uint)((s[i + 11] << 24) | (s[i + 10] << 16) | (s[i + 9] << 8) | s[i + 8]) + esi;
                edx = (uint)((s[i + 3] << 24) | (s[i + 2] << 16) | (s[i + 1] << 8) | s[i]) - esi;
                edx = (edx + ebx) ^ (esi >> 28) ^ (esi << 4);
                esi += edi;
                edi = (edi - edx) ^ (edx >> 26) ^ (edx << 6);
                edx += esi;
                esi = (esi - edi) ^ (edi >> 24) ^ (edi << 8);
                edi += edx;
                ebx = (edx - esi) ^ (esi >> 16) ^ (esi << 16);
                esi += edi;
                edi = (edi - ebx) ^ (ebx >> 13) ^ (ebx << 19);
                ebx += esi;
                esi = (esi - edi) ^ (edi >> 28) ^ (edi << 4);
                edi += ebx;
            }

            int r = s.Length - i;
            if (r > 0)
            {
                if (r >= 12) esi += (uint)s[i + 11] << 24;
                if (r >= 11) esi += (uint)s[i + 10] << 16;
                if (r >= 10) esi += (uint)s[i + 9] << 8;
                if (r >= 9) esi += s[i + 8];
                if (r >= 8) edi += (uint)s[i + 7] << 24;
                if (r >= 7) edi += (uint)s[i + 6] << 16;
                if (r >= 6) edi += (uint)s[i + 5] << 8;
                if (r >= 5) edi += s[i + 4];
                if (r >= 4) ebx += (uint)s[i + 3] << 24;
                if (r >= 3) ebx += (uint)s[i + 2] << 16;
                if (r >= 2) ebx += (uint)s[i + 1] << 8;
                ebx += s[i];

                esi = (esi ^ edi) - ((edi >> 18) ^ (edi << 14));
                ecx = (esi ^ ebx) - ((esi >> 21) ^ (esi << 11));
                edi = (edi ^ ecx) - ((ecx >> 7) ^ (ecx << 25));
                esi = (esi ^ edi) - ((edi >> 16) ^ (edi << 16));
                edx = (esi ^ ecx) - ((esi >> 28) ^ (esi << 4));
                edi = (edi ^ edx) - ((edx >> 18) ^ (edx << 14));
                eax = (esi ^ edi) - ((edi >> 8) ^ (edi << 24));
                return ((ulong)edi << 32) | eax;
            }

            return ((ulong)esi << 32) | eax;
        }

        // The gump, or null when the file does not hold it.
        public static Bitmap Read(string uopPath, int gumpId)
        {
            ulong want = CreateHash("build/gumpartlegacymul/" + gumpId.ToString("D8") + ".tga");
            using (var fs = File.OpenRead(uopPath))
            using (var br = new BinaryReader(fs))
            {
                br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32();
                long next = br.ReadInt64();
                while (next != 0)
                {
                    fs.Seek(next, SeekOrigin.Begin);
                    int n = br.ReadInt32();
                    next = br.ReadInt64();
                    for (int i = 0; i < n; i++)
                    {
                        long off = br.ReadInt64();
                        int headerLen = br.ReadInt32();
                        int compLen = br.ReadInt32();
                        int decompLen = br.ReadInt32();
                        ulong hash = br.ReadUInt64();
                        br.ReadUInt32();
                        short flag = br.ReadInt16();
                        if (off == 0 || hash != want) continue;
                        fs.Seek(off + headerLen, SeekOrigin.Begin);
                        byte[] data = br.ReadBytes(compLen);
                        if (flag == 1 || flag == 3) data = Inflate(data, decompLen);
                        if (flag == 3) data = Bwt(data);
                        return Decode(data);
                    }
                }
            }

            return null;
        }

        private static byte[] Inflate(byte[] data, int size)
        {
            // zlib: a two byte header, then deflate.
            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var z = new DeflateStream(ms, CompressionMode.Decompress))
            using (var o = new MemoryStream(size > 0 ? size : 4096))
            {
                z.CopyTo(o);
                return o.ToArray();
            }
        }

        private static Bitmap Decode(byte[] d)
        {
            int w = (int)BitConverter.ToUInt32(d, 0);
            int h = (int)BitConverter.ToUInt32(d, 4);
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) return null;
            var argb = new int[w * h];
            for (int y = 0; y < h; y++)
            {
                int off = 8 + (int)BitConverter.ToUInt32(d, 8 + y * 4) * 4;
                int x = 0;
                while (x < w && off + 4 <= d.Length)
                {
                    int c = BitConverter.ToUInt16(d, off);
                    int run = BitConverter.ToUInt16(d, off + 2);
                    off += 4;
                    if (c != 0)
                    {
                        int r = ((c >> 10) & 31) * 255 / 31, g = ((c >> 5) & 31) * 255 / 31, b = (c & 31) * 255 / 31;
                        int px = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
                        for (int k = 0; k < run && x + k < w; k++) argb[y * w + x + k] = px;
                    }
                    x += run;
                }
            }

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var bits = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++) Marshal.Copy(argb, y * w, IntPtr.Add(bits.Scan0, y * bits.Stride), w);
            bmp.UnlockBits(bits);
            return bmp;
        }

        private static byte[] Bwt(byte[] buf)
        {
            var table = new ushort[65536];
            for (int t = 0; t < 65536; t++) table[t] = (ushort)t; // BuildTable's values, sorted
            var list = new byte[buf.Length - 4];
            int i = 0;
            byte first = buf[4];
            int pos = 5;
            while (pos < buf.Length)
            {
                int c = first;
                ushort val = table[c];
                for (int k = c; k > 0; k--) table[k] = table[k - 1];
                table[0] = val;
                list[i++] = (byte)val;
                first = buf[pos++];
            }

            return BwtInternal(list);
        }

        private static byte[] BwtInternal(byte[] input)
        {
            var sym = new int[256];
            var part = new int[256 * 3];
            for (int k = 0; k < 256; k++) { sym[k] = k; part[k] = BitConverter.ToInt32(input, k * 4); }
            int total = 0;
            for (int k = 0; k < 256; k++) total += part[k];
            var output = new byte[total];
            var tmp = new int[256];
            Array.Copy(part, tmp, 256);
            var freq = new int[256];
            for (int k = 0; k < 256; k++)
            {
                int value = 0, index = 0;
                for (int j = 0; j < 256; j++) if (tmp[j] > value) { index = j; value = tmp[j]; }
                if (value == 0) break;
                freq[k] = index;
                tmp[index] = 0;
            }

            int nz = 0;
            for (int k = 0; k < 256; k++) if (part[k] != 0) nz++;
            for (int k = 0, m = 0; k < nz; k++)
            {
                int f = freq[k];
                sym[input[m + 1024]] = f;
                part[f + 256] = m + 1;
                m += part[f];
                part[f + 512] = m;
            }

            int val = sym[0];
            for (int count = 0; count < total; count++)
            {
                output[count] = (byte)val;
                if (part[val + 256] >= part[val + 512])
                {
                    if (nz > 0)
                    {
                        nz--;
                        for (int k = 0; k < nz; k++) sym[k] = sym[k + 1];
                        val = sym[0];
                    }
                }
                else
                {
                    int idx = input[part[val + 256] + 1024];
                    part[val + 256]++;
                    if (idx != 0)
                    {
                        for (int k = 0; k < idx; k++) sym[k] = sym[k + 1];
                        sym[idx] = val;
                        val = sym[0];
                    }
                }
            }

            return output;
        }
    }
}
'@

function Initialize-ConsoleNative {
    if (-not ('ShardConsoleNative.PaneJob' -as [type])) {
        Add-Type -TypeDefinition $script:NativeSource -ReferencedAssemblies System.Drawing
    }
}

function Get-GumpImage {
    # One client gump as an image: from the cache (CacheFolder\gumps\<id>.png), or read once from the client's
    # gumpartLegacyMUL.uop into it. $null when neither exists or anything fails: the theme's solid color is drawn
    # instead. The cache folder is git-ignored; EA's art never goes in either repo.
    param($Config, [int]$Id)
    try {
        $dir = Join-Path $Config.CacheFolder 'gumps'
        $png = Join-Path $dir ([string]$Id + '.png')
        if (-not (Test-Path -LiteralPath $png -PathType Leaf)) {
            if (-not (Test-Path -LiteralPath $Config.GumpFile -PathType Leaf)) { return $null }
            Initialize-ConsoleNative
            $bmp = [ShardConsoleNative.Gumps]::Read($Config.GumpFile, $Id)
            if (-not $bmp) { return $null }
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            try { $bmp.Save($png, [Drawing.Imaging.ImageFormat]::Png) } finally { $bmp.Dispose() }
        }
        # From a copy in memory, so the cache file is never held open.
        $ms = New-Object IO.MemoryStream(, [IO.File]::ReadAllBytes($png))
        [Drawing.Image]::FromStream($ms)
    } catch { $null }
}

function ConvertTo-DrawingColor {
    param([int[]]$Rgb)
    [Drawing.Color]::FromArgb($Rgb[0], $Rgb[1], $Rgb[2])
}

# --- cc-P132 SHELL: drawn textures, the art folder and its fallbacks, DPI, the window state, the lights' facts ----------

function New-SpeckleTexture {
    # A tileable stone or slate texture drawn in code: every pixel is Base plus a seeded speckle within +/- Spread, so
    # its lightest and darkest pixel are exactly Base +/- Spread (the theme's Texture bounds the contrast facts use).
    param([int[]]$Base, [int]$Spread, [int]$Size = 160, [int]$Seed = 1)
    $bmp = New-Object Drawing.Bitmap($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rect = New-Object Drawing.Rectangle(0, 0, $Size, $Size)
    $data = $bmp.LockBits($rect, [Drawing.Imaging.ImageLockMode]::WriteOnly, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $Size)
    $rnd = New-Object System.Random($Seed)
    for ($y = 0; $y -lt $Size; $y++) {
        $row = $y * $data.Stride
        for ($x = 0; $x -lt $Size; $x++) {
            # A faint grain: every fourth row leans dark, the rest is speckle. Both stay inside the spread.
            $d = $rnd.Next(-$Spread, $Spread + 1)
            if (($y % 4) -eq 0 -and $d -gt 0) { $d = -$d }
            $i = $row + $x * 4
            $bytes[$i] = [byte][math]::Max(0, [math]::Min(255, $Base[2] + $d))
            $bytes[$i + 1] = [byte][math]::Max(0, [math]::Min(255, $Base[1] + $d))
            $bytes[$i + 2] = [byte][math]::Max(0, [math]::Min(255, $Base[0] + $d))
            $bytes[$i + 3] = 255
        }
    }
    [Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    $bmp.UnlockBits($data)
    $bmp
}

function Get-ThemeImages {
    # The window's and the pages' backgrounds: the client gumps (UO), or the drawn textures (Enhanced), or art dropped
    # into the stone and page slots.
    param($Config, $Theme)
    $im = @{}
    if ($Theme.Gumps) {
        $im.Stone = Get-GumpImage $Config $Theme.Gumps.Stone
        $im.Parchment = Get-GumpImage $Config $Theme.Gumps.Parchment
    }
    if ($Theme.Texture) {
        $t = $Theme.Texture
        $im.Stone = Get-ArtImage 'stone'
        if (-not $im.Stone) { $im.Stone = New-SpeckleTexture $t.StoneBase $t.StoneSpread 160 $t.Seed }
        $im.Parchment = Get-ArtImage 'page'
        if (-not $im.Parchment) { $im.Parchment = New-SpeckleTexture $t.PageBase $t.PageSpread 160 ($t.Seed + 1) }
    }
    $im
}

function Get-ConsoleArt {
    # Reads scripts\console-art once at start: each slot's file (Get-ArtSlots), judged by Test-ArtFile. A bad file is
    # one line in the log and the drawn fallback; the missing slots are one line naming them. Never a popup.
    param($Config)
    $dir = $Config.ArtFolder
    $manifest = ConvertFrom-ArtManifest $(if ($dir -and (Test-Path -LiteralPath (Join-Path $dir 'art.json') -PathType Leaf)) { [IO.File]::ReadAllText((Join-Path $dir 'art.json')) } else { $null })
    $slots = @{}; $missing = New-Object 'System.Collections.Generic.List[string]'; $bad = New-Object 'System.Collections.Generic.List[string]'
    foreach ($slot in Get-ArtSlots) {
        $path = $(if ($dir) { Join-Path $dir $slot.File } else { '' })
        if ($slot.Kind -eq 'animated' -and $path -and -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $gif = $path -replace '\.png$', '.gif'
            if (Test-Path -LiteralPath $gif -PathType Leaf) { $path = $gif }
        }
        $exists = [bool]($path -and (Test-Path -LiteralPath $path -PathType Leaf))
        $len = 0; $ext = ''; $img = $null; $readable = $true; $w = 0; $h = 0
        $frames = 1; $fps = 8
        if ($manifest.Slots.ContainsKey($slot.Name)) { $frames = $manifest.Slots[$slot.Name].Frames; $fps = $manifest.Slots[$slot.Name].Fps }
        if ($exists) {
            try {
                $fi = Get-Item -LiteralPath $path
                $len = $fi.Length; $ext = $fi.Extension
                $img = [Drawing.Image]::FromStream((New-Object IO.MemoryStream(, [IO.File]::ReadAllBytes($path))))
                $w = $img.Width; $h = $img.Height
                if ($ext -ieq '.gif') { try { $frames = $img.GetFrameCount([Drawing.Imaging.FrameDimension]::Time) } catch { $frames = 1 } }
            } catch { $readable = $false }
        }
        $v = Test-ArtFile -Slot $slot -Exists $exists -Length $len -Extension $ext -Width $w -Height $h -Readable $readable -Frames $frames
        if ($v.Ok) {
            $kind = $(if ($ext -ieq '.gif') { 'gif' } elseif ($slot.Kind -eq 'animated' -and $frames -gt 1) { 'strip' } else { 'static' })
            $slots[$slot.Name] = [pscustomobject]@{ Name = $slot.Name; Image = $img; Frames = $frames; Fps = $fps; Kind = $kind; File = $path }
        } elseif ($v.Missing) { $missing.Add($slot.Name) }
        else { $bad.Add($v.Why); if ($img) { $img.Dispose() } }
    }
    $total = @(Get-ArtSlots).Count
    $line = 'console-art: ' + $slots.Count + ' of ' + $total + ' slots have art in ' + $dir + $(if ($missing.Count) { '; drawn fallbacks for ' + ($missing -join ', ') } else { '' })
    Write-ConsoleLog $Config $line
    foreach ($b in $bad) { Write-ConsoleLog $Config ('console-art: bad file, drawn fallback used: ' + $b) }
    if ($manifest.Problem) { Write-ConsoleLog $Config ('console-art: ' + $manifest.Problem) }
    [pscustomobject]@{ Folder = $dir; Slots = $slots; Missing = $missing.ToArray(); Bad = $bad.ToArray(); Manifest = $manifest; Lines = @(@($line) + @($bad | ForEach-Object { 'console-art: bad file, drawn fallback used: ' + $_ })) }
}

function Get-ArtImage {
    # The art for a slot (its first frame, at the slot's size) or $null when the slot has none; see Get-ArtOrFallback
    # for the drawn stand-in. A strip's first frame is cut out; a GIF's first frame is the image itself.
    param([string]$Name)
    if (-not $script:art -or -not $script:art.Slots.ContainsKey($Name)) { return $null }
    $s = $script:art.Slots[$Name]
    if ($s.Kind -eq 'strip' -and $s.Frames -gt 1) {
        $fw = [int]($s.Image.Width / $s.Frames)
        $bmp = New-Object Drawing.Bitmap($fw, $s.Image.Height)
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.DrawImage($s.Image, (New-Object Drawing.Rectangle(0, 0, $fw, $s.Image.Height)), (New-Object Drawing.Rectangle(0, 0, $fw, $s.Image.Height)), [Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        return $bmp
    }
    $s.Image
}

function Get-ArtOrFallback {
    # The image a slot shows: its art, else the drawn fallback at $Size px (cached by name and size).
    param([string]$Name, [int]$Size = 32)
    if (-not $script:artCache) { $script:artCache = @{} }
    $key = $Name + '@' + $Size
    if ($script:artCache.ContainsKey($key)) { return $script:artCache[$key] }
    $img = Get-ArtImage $Name
    if (-not $img) { $img = New-FallbackImage $Name $Size }
    $script:artCache[$key] = $img
    $img
}

function Get-GlowColor {
    param([string]$Key)
    $t = $script:theme
    if ($t -and $t.Glow -and $t.Glow.Contains($Key)) { return $t.Glow[$Key] }
    @{ green = @(120, 255, 150); amber = @(255, 200, 90); red = @(255, 110, 100); grey = @(130, 130, 140); golem = @(255, 196, 96) }[$Key]
}

function New-FallbackImage {
    # The drawn stand-in for a slot: a faceted crystal per color (a dull one for grey), a Golem silhouette whose glow
    # follows its state, a rune circle, a spinner sigil, a header band, a tile icon (the rune circle with the tile's
    # letter), or a speckle texture. Transparent background, anti-aliased, $Size square (the band is 8x wide).
    param([string]$Name, [int]$Size = 32)
    if ($Name -in @('stone', 'page')) {
        $t = $script:theme.Texture
        if (-not $t) { $t = (Get-EnhancedTheme).Texture }
        if ($Name -eq 'stone') { return (New-SpeckleTexture $t.StoneBase $t.StoneSpread 160 $t.Seed) }
        return (New-SpeckleTexture $t.PageBase $t.PageSpread 160 ($t.Seed + 1))
    }
    $w = $Size; if ($Name -eq 'header-band') { $w = $Size * 8 }
    $bmp = New-Object Drawing.Bitmap($w, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::Transparent)
    $r = New-Object Drawing.Rectangle(0, 0, $w, $Size)
    switch -Regex ($Name) {
        '^crystal-(\w+)$' { Draw-CrystalFallback $g $r $Matches[1] }
        '^golem-(\w+)$'   { Draw-GolemFallback $g $r $Matches[1] }
        '^rune-'          { Draw-RuneFallback $g $r '' }
        '^spinner$'       { Draw-SpinnerFallback $g $r }
        '^header-band$'   { Draw-HeaderBandFallback $g $r }
        '^icon-(\w+)$'    { Draw-RuneFallback $g $r (@{ live = 'L'; test = 'T'; publisher = 'P'; dns = 'D'; golem = 'G'; playtests = 'Y'; websites = 'W' }[$Matches[1]]) }
        default           { Draw-RuneFallback $g $r '?' }
    }
    $g.Dispose()
    $bmp
}

function Draw-CrystalFallback {
    # A six-faceted gem standing on its point, lit from the upper left, with a soft halo in its glow color. Grey is dull:
    # darker, no halo.
    param($G, [Drawing.Rectangle]$R, [string]$Color)
    $glow = Get-GlowColor $Color
    $dull = ($Color -eq 'grey')
    $cx = $R.X + $R.Width / 2.0; $cy = $R.Y + $R.Height / 2.0
    $hw = $R.Width * 0.30; $hh = $R.Height * 0.46
    if (-not $dull) {
        $halo = New-Object Drawing.Drawing2D.GraphicsPath
        $halo.AddEllipse([single]($cx - $R.Width * 0.48), [single]($cy - $R.Height * 0.48), [single]($R.Width * 0.96), [single]($R.Height * 0.96))
        $hb = New-Object Drawing.Drawing2D.PathGradientBrush($halo)
        $hb.CenterColor = [Drawing.Color]::FromArgb(150, $glow[0], $glow[1], $glow[2])
        $hb.SurroundColors = @([Drawing.Color]::FromArgb(0, $glow[0], $glow[1], $glow[2]))
        $G.FillPath($hb, $halo)
        $hb.Dispose(); $halo.Dispose()
    }
    $pts = @((New-Object Drawing.PointF([single]$cx, [single]($cy - $hh))), (New-Object Drawing.PointF([single]($cx + $hw), [single]($cy - $hh * 0.45))),
             (New-Object Drawing.PointF([single]($cx + $hw * 0.8), [single]($cy + $hh * 0.35))), (New-Object Drawing.PointF([single]$cx, [single]($cy + $hh))),
             (New-Object Drawing.PointF([single]($cx - $hw * 0.8), [single]($cy + $hh * 0.35))), (New-Object Drawing.PointF([single]($cx - $hw), [single]($cy - $hh * 0.45))))
    $dark = @([int]($glow[0] * 0.35), [int]($glow[1] * 0.35), [int]($glow[2] * 0.35))
    $light = @([math]::Min(255, [int]($glow[0] * 0.75 + 90)), [math]::Min(255, [int]($glow[1] * 0.75 + 90)), [math]::Min(255, [int]($glow[2] * 0.75 + 90)))
    if ($dull) { $dark = @(60, 60, 66); $light = @(150, 150, 158) }
    $body = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF([single]($cx - $hw), [single]($cy - $hh))), (New-Object Drawing.PointF([single]($cx + $hw), [single]($cy + $hh))), (ConvertTo-DrawingColor $light), (ConvertTo-DrawingColor $dark))
    $G.FillPolygon($body, $pts)
    $body.Dispose()
    # The facets: a line from the top point and the bottom point to each shoulder, and the waist.
    $pen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(140, 255, 255, 255), 1)
    $G.DrawLine($pen, $pts[0], $pts[2]); $G.DrawLine($pen, $pts[0], $pts[4]); $G.DrawLine($pen, $pts[3], $pts[1]); $G.DrawLine($pen, $pts[3], $pts[5])
    $G.DrawLine($pen, $pts[1], $pts[5])
    $pen.Dispose()
    $edge = New-Object Drawing.Pen([Drawing.Color]::FromArgb(200, $dark[0], $dark[1], $dark[2]), 1)
    $G.DrawPolygon($edge, $pts)
    $edge.Dispose()
}

function Draw-GolemFallback {
    # A golem silhouette: a squat body, a round head, two arms and two legs. Running: bronze, lit, a halo and bright
    # eyes. Idle: a softer light and dim eyes. Dark: slate, no light, no eyes.
    param($G, [Drawing.Rectangle]$R, [string]$State)
    $glow = Get-GlowColor 'golem'
    $s = $R.Width / 64.0
    $body = @(120, 100, 70); $eyes = @(255, 236, 140); $haloA = 170
    if ($State -eq 'idle') { $body = @(96, 82, 60); $eyes = @(200, 170, 90); $haloA = 70 }
    if ($State -eq 'dark') { $body = @(56, 54, 52); $eyes = $null; $haloA = 0 }
    if ($haloA -gt 0) {
        $halo = New-Object Drawing.Drawing2D.GraphicsPath
        $halo.AddEllipse([single]($R.X + 2 * $s), [single]($R.Y + 2 * $s), [single](60 * $s), [single](60 * $s))
        $hb = New-Object Drawing.Drawing2D.PathGradientBrush($halo)
        $hb.CenterColor = [Drawing.Color]::FromArgb($haloA, $glow[0], $glow[1], $glow[2])
        $hb.SurroundColors = @([Drawing.Color]::FromArgb(0, $glow[0], $glow[1], $glow[2]))
        $G.FillPath($hb, $halo); $hb.Dispose(); $halo.Dispose()
    }
    $br = New-Object Drawing.SolidBrush((ConvertTo-DrawingColor $body))
    $ox = $R.X; $oy = $R.Y
    # Legs, body, arms, head: proportions of a 64 px figure.
    $G.FillRectangle($br, [single]($ox + 20 * $s), [single]($oy + 44 * $s), [single](9 * $s), [single](16 * $s))
    $G.FillRectangle($br, [single]($ox + 35 * $s), [single]($oy + 44 * $s), [single](9 * $s), [single](16 * $s))
    $G.FillRectangle($br, [single]($ox + 17 * $s), [single]($oy + 22 * $s), [single](30 * $s), [single](24 * $s))
    $G.FillRectangle($br, [single]($ox + 8 * $s), [single]($oy + 24 * $s), [single](8 * $s), [single](20 * $s))
    $G.FillRectangle($br, [single]($ox + 48 * $s), [single]($oy + 24 * $s), [single](8 * $s), [single](20 * $s))
    $G.FillEllipse($br, [single]($ox + 22 * $s), [single]($oy + 4 * $s), [single](20 * $s), [single](18 * $s))
    $br.Dispose()
    $line = New-Object Drawing.Pen([Drawing.Color]::FromArgb(120, 0, 0, 0), [single][math]::Max(1, $s))
    $G.DrawLine($line, [single]($ox + 17 * $s), [single]($oy + 34 * $s), [single]($ox + 47 * $s), [single]($oy + 34 * $s))
    $line.Dispose()
    if ($eyes) {
        $eb = New-Object Drawing.SolidBrush((ConvertTo-DrawingColor $eyes))
        $G.FillEllipse($eb, [single]($ox + 27 * $s), [single]($oy + 11 * $s), [single](3 * $s), [single](3 * $s))
        $G.FillEllipse($eb, [single]($ox + 34 * $s), [single]($oy + 11 * $s), [single](3 * $s), [single](3 * $s))
        $eb.Dispose()
    }
}

function Draw-RuneFallback {
    # A rune circle in the trim color: a ring and three strokes (or a letter, for a tile icon).
    param($G, [Drawing.Rectangle]$R, [string]$Letter)
    $gold = $(if ($script:theme -and $script:theme.Chrome) { $script:theme.Chrome.Gold } else { @(220, 178, 98) })
    $pen = New-Object Drawing.Pen((ConvertTo-DrawingColor $gold), [single][math]::Max(1, $R.Width / 16.0))
    $m = [single]($R.Width * 0.08)
    $G.DrawEllipse($pen, [single]($R.X + $m), [single]($R.Y + $m), [single]($R.Width - 2 * $m), [single]($R.Height - 2 * $m))
    if ($Letter) {
        $f = New-Object Drawing.Font('Palatino Linotype', [single][math]::Max(6, $R.Height * 0.5), [Drawing.FontStyle]::Bold)
        $fmt = New-Object Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
        $b = New-Object Drawing.SolidBrush((ConvertTo-DrawingColor $gold))
        $G.DrawString($Letter, $f, $b, (New-Object Drawing.RectangleF($R.X, $R.Y, $R.Width, $R.Height)), $fmt)
        $b.Dispose(); $f.Dispose(); $fmt.Dispose()
    } else {
        $cx = $R.X + $R.Width / 2.0; $cy = $R.Y + $R.Height / 2.0; $q = $R.Width * 0.22
        $G.DrawLine($pen, [single]$cx, [single]($cy - $q * 1.2), [single]$cx, [single]($cy + $q * 1.2))
        $G.DrawLine($pen, [single]$cx, [single]($cy - $q * 1.2), [single]($cx + $q), [single]($cy - $q * 0.3))
        $G.DrawLine($pen, [single]$cx, [single]($cy - $q * 0.2), [single]($cx + $q), [single]($cy + $q * 0.7))
    }
    $pen.Dispose()
}

function Draw-SpinnerFallback {
    # Eight spokes fading around the ring: the working sigil.
    param($G, [Drawing.Rectangle]$R)
    $gold = $(if ($script:theme -and $script:theme.Chrome) { $script:theme.Chrome.Gold } else { @(220, 178, 98) })
    $cx = $R.X + $R.Width / 2.0; $cy = $R.Y + $R.Height / 2.0; $r1 = $R.Width * 0.22; $r2 = $R.Width * 0.46
    for ($i = 0; $i -lt 8; $i++) {
        $a = $i * [math]::PI / 4
        $pen = New-Object Drawing.Pen([Drawing.Color]::FromArgb([int](40 + 215 * $i / 7), $gold[0], $gold[1], $gold[2]), [single][math]::Max(1, $R.Width / 12.0))
        $G.DrawLine($pen, [single]($cx + $r1 * [math]::Cos($a)), [single]($cy + $r1 * [math]::Sin($a)), [single]($cx + $r2 * [math]::Cos($a)), [single]($cy + $r2 * [math]::Sin($a)))
        $pen.Dispose()
    }
}

function Draw-HeaderBandFallback {
    # The band behind the title: the backing, a bronze rule along the bottom, and a rune at each end.
    param($G, [Drawing.Rectangle]$R)
    $c = $(if ($script:theme -and $script:theme.Chrome) { $script:theme.Chrome } else { (Get-EnhancedTheme).Chrome })
    $b = New-Object Drawing.SolidBrush((ConvertTo-DrawingColor $c.Backing))
    $G.FillRectangle($b, $R); $b.Dispose()
    $pen = New-Object Drawing.Pen((ConvertTo-DrawingColor $c.Gold), 2)
    $G.DrawLine($pen, $R.X, $R.Bottom - 2, $R.Right, $R.Bottom - 2); $pen.Dispose()
    $sz = [int]($R.Height * 0.6)
    Draw-RuneFallback $G (New-Object Drawing.Rectangle(($R.Right - $sz - 10), [int](($R.Height - $sz) / 2), $sz, $sz)) ''
}

function Set-ConsoleDpiAwareness {
    # Per-monitor DPI before any window exists, so text and art are drawn crisp at 125% and 150% instead of scaled up
    # by Windows. Shcore's SetProcessDpiAwareness (Windows 8.1+), else user32's SetProcessDPIAware. Says which.
    try {
        if (-not ('ShardConsole.DpiNative' -as [type])) {
            Add-Type -Namespace ShardConsole -Name DpiNative -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int value);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
'@
        }
        $hr = [ShardConsole.DpiNative]::SetProcessDpiAwareness(2)
        if ($hr -eq 0) { return 'per-monitor DPI aware' }
        if ($hr -eq -2147024891 -or $hr -eq 0x80070005) { return 'DPI awareness was already set by the host' }
        if ([ShardConsole.DpiNative]::SetProcessDPIAware()) { return 'system DPI aware (shcore said ' + $hr + ')' }
        'DPI awareness unchanged (' + $hr + ')'
    } catch { 'DPI awareness unchanged: ' + $_.Exception.Message }
}

function Read-WindowState {
    param($Config)
    $text = $null
    try { if ($Config.WindowStateFile -and (Test-Path -LiteralPath $Config.WindowStateFile -PathType Leaf)) { $text = [IO.File]::ReadAllText($Config.WindowStateFile) } } catch { $text = $null }
    ConvertFrom-WindowState $text
}

function Save-WindowState {
    # On closing: the window's normal bounds (RestoreBounds while maximized), whether it is maximized, and the splitters.
    param($Config, $Form)
    try {
        $b = $Form.Bounds
        $max = ($Form.WindowState -eq 'Maximized')
        if ($max) { $b = $Form.RestoreBounds }
        $s = [pscustomobject]@{ Width = $b.Width; Height = $b.Height; X = $b.X; Y = $b.Y; Maximized = $max; Split1 = 0; Split2 = 0; PaneSplit = 0 }
        if ($script:ui.Split1) { $s.Split1 = $script:ui.Split1.SplitterDistance }
        if ($script:ui.Split2) { $s.Split2 = $script:ui.Split2.SplitterDistance }
        if ($script:ui.Pane -and $script:ui.Pane.Split -and -not $script:ui.Pane.Split.Panel2Collapsed) { $s.PaneSplit = $script:ui.Pane.Split.SplitterDistance }
        $dir = Split-Path -Parent $Config.WindowStateFile
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [IO.File]::WriteAllText($Config.WindowStateFile, (ConvertTo-WindowState $s), (New-Object Text.UTF8Encoding $false))
        $true
    } catch { Write-ConsoleLog $Config ('window state not saved: ' + $_.Exception.Message); $false }
}

function Restore-WindowBounds {
    # The saved size and place when it is on a screen (Resolve-WindowBounds), maximized if it was; else the default.
    param($Form, $State)
    try {
        $screens = @([Windows.Forms.Screen]::AllScreens | ForEach-Object { $wa = $_.WorkingArea; [pscustomobject]@{ X = $wa.X; Y = $wa.Y; Width = $wa.Width; Height = $wa.Height } })
        $b = Resolve-WindowBounds $State $screens
        if ($b.Placed) { $Form.StartPosition = 'Manual'; $Form.Location = New-Object Drawing.Point($b.X, $b.Y) }
        $Form.Size = New-Object Drawing.Size($b.Width, $b.Height)
        if ($State.Maximized) { $Form.WindowState = 'Maximized' }
    } catch { Write-ConsoleLog $script:cfg ('window state not restored: ' + $_.Exception.Message) }
}

function Restore-WindowSplitters {
    param($State)
    if (-not $State) { return }
    Set-SplitterSafely $script:ui.Split1 ([int]$State.Split1)
    Set-SplitterSafely $script:ui.Split2 ([int]$State.Split2)
    if ($script:ui.Pane -and $script:ui.Pane.Split -and [int]$State.PaneSplit -gt 0) { Set-SplitterSafely $script:ui.Pane.Split ([int]$State.PaneSplit) }
}

function Set-SplitterSafely {
    # A splitter distance inside what the container allows now; a value that does not fit is left alone.
    param($Split, [int]$Distance)
    if (-not $Split -or $Distance -le 0) { return }
    $len = $(if ($Split.Orientation -eq 'Horizontal') { $Split.Height } else { $Split.Width })
    $max = $len - $Split.Panel2MinSize - $Split.SplitterWidth
    if ($Distance -ge $Split.Panel1MinSize -and $Distance -le $max) { try { $Split.SplitterDistance = $Distance } catch { } }
}

function Get-RebuildFacts {
    # Section 6, SHELL: what Get-RebuildLight judges. The running test image's commit and dirty flag (its sl.commit and
    # sl.dirty labels, already in the state), the commits since it with their files, uncommitted files in the code
    # repo, the Golem's running prompt, and whether a build is running (a Start-TestShard.ps1, build.sh or docker build
    # process, or this console's own test.rebuild child).
    param($Config, $State)
    $f = [pscustomobject]@{ Running = $false; ImageCommit = ''; ImageDirty = $false; Head = ''; Commits = @(); Uncommitted = 0; GolemRunning = ''; BuildRunning = $false; Note = '' }
    try {
        $t = $State.Shards['test']
        if ($t -and $t.Container -and $t.Container.Exists -and $t.Container.Running) { $f.Running = $true }
        if ($t -and $t.RunningImage -and $t.RunningImage.Commit) { $f.ImageCommit = [string]$t.RunningImage.Commit; $f.ImageDirty = [bool]$t.RunningImage.Dirty }
        $repo = $Config.RepoRoot
        $f.Head = (Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'rev-parse', 'HEAD')).Out.Trim()
        if ($f.ImageCommit) {
            $log = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'log', '--format=%H|%h|%s', '--name-only', ($f.ImageCommit + '..HEAD'))
            if ($log.Code -eq 0) { $f.Commits = @(ConvertFrom-GitLogWithFiles $log.Out) } else { $f.Note = 'git log failed: ' + ([string]$log.Err).Trim(); $f.ImageCommit = '' }
        }
        $st = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'status', '--porcelain', '--untracked-files=all')
        if ($st.Code -eq 0) { $f.Uncommitted = @(@($st.Out -split "`n") | Where-Object { $_.Trim() }).Count }
        $f.GolemRunning = Get-GolemRunningPrompt $Config
        $f.BuildRunning = Test-ProcessRunning 'Start-TestShard\.ps1|build\.sh|docker(\.exe)?\s+build|-Action\s+(test\.rebuild|fix\.rebuild)'
    } catch { $f.Note = $_.Exception.Message }
    $f
}

function Get-GolemRunningPrompt {
    # The prompt the Golem is running, from its status.json, or ''.
    param($Config)
    try {
        $ap = $Config.Autopilot
        if ($ap -and $ap.StatusFile -and (Test-Path -LiteralPath $ap.StatusFile -PathType Leaf)) {
            $s = ConvertFrom-AutopilotStatus ([IO.File]::ReadAllText($ap.StatusFile))
            if ($s -and $s.State -in @('RUNNING', 'NEEDS YOU') -and $s.Current) { return [string]$s.Current }
        }
    } catch { }
    ''
}

function Test-ProcessRunning {
    # Whether any process on this PC has a command line matching $Pattern (this console's own process excluded).
    param([string]$Pattern)
    try {
        $me = $PID
        foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name='powershell.exe' OR Name='pwsh.exe' OR Name='bash.exe' OR Name='docker.exe' OR Name='cmd.exe'" -ErrorAction Stop)) {
            if ($p.ProcessId -eq $me) { continue }
            if ([string]$p.CommandLine -match $Pattern) { return $true }
        }
    } catch { }
    $false
}

function Read-ZipBuiltFrom {
    # "built from ShatteredLegacy <commit>" in <top>/app/VERSION.txt inside a package zip (cc-P124 writes it), or $null.
    param([string]$Path)
    try {
        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        $za = [IO.Compression.ZipFile]::OpenRead($Path)
        try {
            $e = @($za.Entries | Where-Object { $_.FullName -match '^[^/]+/app/VERSION\.txt$' })
            if ($e.Count -ne 1) { return $null }
            $sr = New-Object IO.StreamReader($e[0].Open())
            try { $txt = $sr.ReadToEnd() } finally { $sr.Dispose() }
            if ($txt -match 'built from ShatteredLegacy\s+(.+)') { return $Matches[1].Trim() }
            $null
        } finally { $za.Dispose() }
    } catch { $null }
}

function Get-PublishedPackageVersion {
    # The live version.json at the download site, kept 10 minutes (one GET of 4 s at most; the refresh runs every 30 s).
    param($Config)
    if ($script:publishedAt -and ([datetime]::UtcNow - $script:publishedAt).TotalMinutes -lt 10) { return $script:published }
    $o = [pscustomobject]@{ Reachable = $false; Version = ''; Error = '' }
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri $Config.PublishedVersionUrl -TimeoutSec 4
        $j = $r.Content | ConvertFrom-Json
        $o.Reachable = $true; $o.Version = [string]$j.version
    } catch { $o.Error = $_.Exception.Message }
    $script:published = $o; $script:publishedAt = [datetime]::UtcNow
    $o
}

function Get-WhatsNewFacts {
    # cc-P288, SHELL: what Get-PackageWhatsNew is made from, all read only. The published package's commit
    # (Resolve-PackageBaseCommit), every commit since it with its files, the CHANGELOG.md lines each shipping commit
    # added (CHANGELOG.md at it against its first parent), CHANGELOG.md then and now, and the official Legion scripts
    # changed since then (this PC's scripts clone, when it has one).
    param([string]$Repo, [string]$Version, [string]$ZipCommit, [string]$LegionRepo)
    $w = [pscustomobject]@{ Version = $Version; Base = ''; BaseHow = ''; Commits = @(); AddedBy = @{}; ChangelogThen = $null; ChangelogNow = $null; Scripts = @(); Note = '' }
    if (-not $Version) { $w.Note = 'no published version'; return $w }
    $git = { param([string[]]$a) Invoke-ToolRead -Exe 'git' -Arguments (@('-C', $Repo, '--no-optional-locks') + $a) }
    $fp = & $git @('log', '--first-parent', '--format=%H|%ct', '-n', '3000', 'HEAD')
    $b = Resolve-PackageBaseCommit -Version $Version -ZipCommit $ZipCommit -FirstParentLog $(if ($fp.Code -eq 0) { [string]$fp.Out } else { '' })
    if (-not $b) { $w.Note = 'no commit found for ' + $Version; return $w }
    $log = & $git @('log', '--format=%H|%h|%s', '--name-only', ($b.Sha + '..HEAD'))
    if ($log.Code -ne 0) { $w.Note = 'git log failed: ' + ([string]$log.Err).Trim(); return $w }
    $w.Base = $b.Sha; $w.BaseHow = $b.How
    $w.Commits = @(ConvertFrom-GitLogWithFiles $log.Out)
    foreach ($c in @($w.Commits)) {
        if (-not (Get-WhatsNewShipKind $c.Files)) { continue }
        $now = & $git @('show', ($c.Sha + ':CHANGELOG.md'))
        if ($now.Code -ne 0) { continue }
        $was = & $git @('show', ($c.Sha + '^:CHANGELOG.md'))
        $w.AddedBy[$c.Sha] = @(Get-ChangelogItemsAdded $(if ($was.Code -eq 0) { [string]$was.Out } else { '' }) ([string]$now.Out))
    }
    $then = & $git @('show', ($b.Sha + ':CHANGELOG.md'))
    $head = & $git @('show', 'HEAD:CHANGELOG.md')
    if ($then.Code -eq 0 -and $head.Code -eq 0) { $w.ChangelogThen = [string]$then.Out; $w.ChangelogNow = [string]$head.Out }
    if ($LegionRepo -and (Test-Path -LiteralPath (Join-Path $LegionRepo '.git'))) {
        $at = & $git @('show', '-s', '--format=%ct', $b.Sha)
        $tags = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $LegionRepo, '--no-optional-locks', 'for-each-ref', 'refs/tags/official-*', '--format=%(refname:short)|%(creatordate:unix)')
        if ($at.Code -eq 0 -and $tags.Code -eq 0 -and ([string]$at.Out).Trim() -match '^\d+$') {
            $r = Select-OfficialScriptTags ([string]$tags.Out) ([long]([string]$at.Out).Trim())
            if ($r.To) {
                $files = $(if ($r.From) { Invoke-ToolRead -Exe 'git' -Arguments @('-C', $LegionRepo, '--no-optional-locks', 'diff', '--name-only', $r.From, $r.To) } else { Invoke-ToolRead -Exe 'git' -Arguments @('-C', $LegionRepo, '--no-optional-locks', 'ls-tree', '--name-only', $r.To) })
                if ($files.Code -eq 0) { $w.Scripts = @(@(([string]$files.Out) -split "`n") | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^[^/]+\.py$' }) }
            }
        }
    }
    $w
}

function Get-PackageLightFacts {
    # Section 7, SHELL: what Get-PackageLight judges. The published version and the zip in dist\ it came from (its
    # VERSION.txt names the commit), the newest zip's commit, the shipping commits since each, the client gate, and the
    # same running-prompt, uncommitted and build-running facts as the rebuild light.
    param($Config)
    $f = [pscustomobject]@{ SiteReachable = $false; SiteError = ''; PublishedVersion = ''; PublishedCommit = ''; NewestZipVersion = ''; NewestZipCommit = ''; Head = ''; Commits = @(); CommitsSinceZip = @(); GolemRunning = ''; Uncommitted = 0; ClientStale = ''; BuildRunning = $false; Note = ''; WhatsNew = $null }
    try {
        $pub = Get-PublishedPackageVersion $Config
        $f.SiteReachable = $pub.Reachable; $f.SiteError = $pub.Error; $f.PublishedVersion = $pub.Version
        $zips = @(Get-PackageZips $Config.PackageDist)
        if ($f.PublishedVersion) {
            $pz = @($zips | Where-Object { $_.Json -and $_.Json.Version -eq $f.PublishedVersion })
            if ($pz.Count) { $f.PublishedCommit = [string](Read-ZipBuiltFrom $pz[0].FullName) }
        }
        if ($zips.Count) { $f.NewestZipVersion = [string]$zips[0].Json.Version; $f.NewestZipCommit = [string](Read-ZipBuiltFrom $zips[0].FullName) }
        $repo = $Config.RepoRoot
        $f.Head = (Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'rev-parse', 'HEAD')).Out.Trim()
        foreach ($pair in @(@('Commits', $f.PublishedCommit), @('CommitsSinceZip', $f.NewestZipCommit))) {
            $c = [string]$pair[1]
            if ($c -and $c -notmatch 'uncommitted') {
                $sha = ($c -split '\s+')[0]
                $log = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'log', '--format=%H|%h|%s', '--name-only', ($sha + '..HEAD'))
                if ($log.Code -eq 0) { $f.($pair[0]) = @(ConvertFrom-GitLogWithFiles $log.Out) } else { $f.Note = 'git log failed: ' + ([string]$log.Err).Trim() }
            }
        }
        # cc-P288 (was cc-P153's two CHANGELOG.md reads): what the suggested What is new line is made from.
        $f.WhatsNew = Get-WhatsNewFacts -Repo $repo -Version $f.PublishedVersion -ZipCommit $f.PublishedCommit -LegionRepo $Config.LegionRepo
        $st = Invoke-ToolRead -Exe 'git' -Arguments @('-C', $repo, '--no-optional-locks', 'status', '--porcelain', '--untracked-files=all')
        if ($st.Code -eq 0) { $f.Uncommitted = @(@($st.Out -split "`n") | Where-Object { $_.Trim() }).Count }
        $cf = Get-PackageClientFacts -ClientDir $Config.PackageClient -RepoRoot $repo
        $stale = @($cf.Problems | Where-Object { $_ -match 'STALE|stale' })
        if ($stale.Count) { $f.ClientStale = [string]$stale[0] }
        $f.GolemRunning = Get-GolemRunningPrompt $Config
        $f.BuildRunning = Test-ProcessRunning 'Build-PlayerPackage\.ps1|-Action\s+package\.build'
    } catch { $f.Note = $_.Exception.Message }
    $f
}

function Get-ThemeFont {
    # 'Ui', 'Tile', 'Detail', 'Mono' or 'Title', from the theme in force (Plain until the form sets one).
    param([string]$Which, [Drawing.FontStyle]$Style = [Drawing.FontStyle]::Regular)
    $t = $script:theme
    if (-not $t) { $t = Get-ConsoleTheme 'Plain' }
    New-Object Drawing.Font($t.Fonts.$Which, [single]$t.Fonts.($Which + 'Size'), $Style)
}

function Get-StateTextColor {
    # cc-P132: a Drawing.Color for a meaning ('Normal', 'Muted', 'Warning', 'Danger', 'Success', 'Link') on a tab page in
    # the theme in force (Plain's named colors when no form has set one).
    param([string]$Kind)
    $t = $script:theme
    if (-not $t) { $t = Get-ConsoleTheme 'Plain' }
    ConvertTo-DrawingColor $t.Text.$Kind
}

function Get-ThemedTextColor {
    # cc-P132: the theme's color for a control's meaning, read from the WinForms color it was given (Firebrick or DarkRed:
    # danger; DarkOrange: warning; ForestGreen: success; DimGray: muted; Navy: link; else normal), on a page or on the
    # stone. The meaning stays; only the color changes to one that reads where it sits.
    param($Theme, [Drawing.Color]$Was, [bool]$OnPage)
    $c = $Theme.Chrome
    $kind = 'Normal'
    if ($Was -eq [Drawing.Color]::Firebrick -or $Was -eq [Drawing.Color]::DarkRed -or $Was -eq [Drawing.Color]::Red) { $kind = 'Danger' }
    elseif ($Was -eq [Drawing.Color]::DarkOrange) { $kind = 'Warning' }
    elseif ($Was -eq [Drawing.Color]::ForestGreen -or $Was -eq [Drawing.Color]::Green) { $kind = 'Success' }
    elseif ($Was -eq [Drawing.Color]::DimGray -or $Was -eq [Drawing.Color]::Gray) { $kind = 'Muted' }
    elseif ($Was -eq [Drawing.Color]::Navy) { $kind = 'Link' }
    if ($OnPage -and $Theme.Text) { return $Theme.Text.$kind }
    switch ($kind) {
        'Danger'  { return $c.StoneDanger }
        'Warning' { return $c.StoneWarn }
        default   { return $c.StoneText }
    }
}

function Test-OnPage {
    # Whether a control sits on a tab page (parchment) rather than on the window itself (stone).
    param($Control)
    for ($p = $Control.Parent; $p; $p = $p.Parent) { if ($p -is [Windows.Forms.TabPage]) { return $true } }
    $false
}

function Set-ControlTheme {
    # Draws one control and everything inside it in the theme's chrome. Plain has none, so Plain draws nothing
    # here and every control keeps the WinForms look it had before cc-P84. Tiles (Tag 'tile') are drawn by
    # Set-TileLook and kept out; the boxes keep their own line colors.
    param($Control, $Theme, $Images)
    if (-not $Theme.Chrome) { return }
    $c = $Theme.Chrome
    $ui = Get-ThemeFont 'Ui'
    $stack = New-Object System.Collections.Stack
    $stack.Push($Control)
    while ($stack.Count) {
        $x = $stack.Pop()
        if ($x.Tag -eq 'tile') {
            # A status tile: its own colors. Only its link buttons take the button look.
            foreach ($k in $x.Controls) { if ($k -is [Windows.Forms.FlowLayoutPanel]) { $k.BackColor = [Drawing.Color]::Transparent; foreach ($b in $k.Controls) { $stack.Push($b) } } }
            continue
        }
        # cc-P132: a plate (the confirm phrase's) keeps its own colors; what sits on it (the box, the buttons) is themed.
        if ($x.Tag -eq 'plate') {
            if ($x -is [Windows.Forms.Panel]) { foreach ($k in $x.Controls) { if ($k.Tag -ne 'plate') { $stack.Push($k) } } }
            continue
        }
        $page = Test-OnPage $x
        $text = $c.StoneText; if ($page) { $text = $c.PageText }
        switch ($x) {
            { $_ -is [Windows.Forms.PictureBox] } { $x.BackColor = [Drawing.Color]::Transparent; break }
            { $_ -is [Windows.Forms.Form] } {
                $x.BackColor = ConvertTo-DrawingColor $c.StoneBack
                $x.ForeColor = ConvertTo-DrawingColor $c.StoneText
                if ($Images.Stone) { $x.BackgroundImage = $Images.Stone; $x.BackgroundImageLayout = 'Tile' }
                $x.Font = $ui
                break
            }
            { $_ -is [Windows.Forms.TabPage] } {
                $x.UseVisualStyleBackColor = $false
                $x.BackColor = ConvertTo-DrawingColor $c.PageBack
                $x.ForeColor = ConvertTo-DrawingColor $c.PageText
                if ($Images.Parchment) { $x.BackgroundImage = $Images.Parchment; $x.BackgroundImageLayout = 'Tile' }
                break
            }
            { $_ -is [Windows.Forms.Button] } {
                $small = ($x.Parent -and $x.Parent.Parent -and $x.Parent.Parent.Tag -eq 'tile')
                $x.FlatStyle = 'Flat'
                $x.UseVisualStyleBackColor = $false
                $x.BackColor = ConvertTo-DrawingColor $c.ButtonBack
                $x.ForeColor = ConvertTo-DrawingColor $c.ButtonText
                $x.FlatAppearance.BorderColor = ConvertTo-DrawingColor $c.ButtonBorder
                $x.FlatAppearance.MouseOverBackColor = ConvertTo-DrawingColor $c.ButtonHover
                # cc-P87: a tile's buttons in bold at TileButtonSize (they were 8.25 regular and hard to read).
                if ($small) { $x.Font = New-Object Drawing.Font($Theme.Fonts.Ui, [single]$Theme.Fonts.TileButtonSize, [Drawing.FontStyle]::Bold) } else { $x.Font = $ui }
                # cc-P87: WinForms draws a disabled flat button's text in its own grey, close to invisible on dark wood.
                # Drawn over by hand when disabled; only the colors are captured, so the closure needs no function.
                $dBack = ConvertTo-DrawingColor $c.DisabledBack; $dText = ConvertTo-DrawingColor $c.DisabledText; $dLine = ConvertTo-DrawingColor $c.DisabledBorder
                $x.Add_Paint({
                    param($s, $e)
                    if ($s.Enabled) { return }
                    $r = $s.ClientRectangle
                    $br = New-Object Drawing.SolidBrush($dBack)
                    $e.Graphics.FillRectangle($br, $r)
                    $pen = New-Object Drawing.Pen($dLine)
                    $e.Graphics.DrawRectangle($pen, 0, 0, $r.Width - 1, $r.Height - 1)
                    [Windows.Forms.TextRenderer]::DrawText($e.Graphics, $s.Text, $s.Font, $r, $dText, [Windows.Forms.TextFormatFlags]'HorizontalCenter, VerticalCenter, WordBreak')
                    $br.Dispose(); $pen.Dispose()
                }.GetNewClosure())
                break
            }
            { $_ -is [Windows.Forms.RichTextBox] } {
                $x.BackColor = ConvertTo-DrawingColor $Theme.LineBack
                $x.ForeColor = ConvertTo-DrawingColor $Theme.Lines.normal
                break
            }
            { $_ -is [Windows.Forms.TextBox] -or $_ -is [Windows.Forms.ListView] -or $_ -is [Windows.Forms.ComboBox] } {
                $x.BackColor = ConvertTo-DrawingColor $c.FieldBack
                $x.ForeColor = ConvertTo-DrawingColor $c.FieldText
                break
            }
            { $_ -is [Windows.Forms.DataGridView] } {
                # cc-P153: the Golem tab's queue grid, in the same field colors as the lists.
                $x.BackgroundColor = ConvertTo-DrawingColor $c.FieldBack
                $x.GridColor = ConvertTo-DrawingColor $c.FieldBack
                $x.DefaultCellStyle.BackColor = ConvertTo-DrawingColor $c.FieldBack
                $x.DefaultCellStyle.ForeColor = ConvertTo-DrawingColor $c.FieldText
                $x.EnableHeadersVisualStyles = $false
                $x.ColumnHeadersDefaultCellStyle.BackColor = ConvertTo-DrawingColor $c.FieldBack
                $x.ColumnHeadersDefaultCellStyle.ForeColor = ConvertTo-DrawingColor $c.FieldText
                $x.Font = $ui
                break
            }
            { $_ -is [Windows.Forms.GroupBox] } {
                # cc-P87: on the stone, the group (its title and its bar of buttons and boxes) sits on the backing.
                $x.BackColor = [Drawing.Color]::Transparent
                if (-not $page) { $x.BackColor = [Drawing.Color]::FromArgb($c.BackingAlpha, $c.Backing[0], $c.Backing[1], $c.Backing[2]) }
                $x.ForeColor = ConvertTo-DrawingColor $c.Gold
                $x.Font = Get-ThemeFont 'Ui' ([Drawing.FontStyle]::Bold)
                break
            }
            { $_ -is [Windows.Forms.Label] -or $_ -is [Windows.Forms.CheckBox] -or $_ -is [Windows.Forms.RadioButton] } {
                $x.BackColor = [Drawing.Color]::Transparent
                # The meaning a caption's color carried stays: a warning stays a warning, a quiet line stays quiet.
                # cc-P132: through the theme's named text colors, and on the stone through StoneWarn / StoneDanger (the
                # dark page colors could not be read there: Chase's confirm-phrase example, 10-16 07:34).
                $x.ForeColor = ConvertTo-DrawingColor (Get-ThemedTextColor $Theme $x.ForeColor $page)
                if ($x.Font.Name -ne $Theme.Fonts.Mono) { $x.Font = New-Object Drawing.Font($Theme.Fonts.Ui, [single]$Theme.Fonts.UiSize, $x.Font.Style) }
                break
            }
            { $_ -is [Windows.Forms.Panel] -or $_ -is [Windows.Forms.SplitContainer] } {
                $x.BackColor = [Drawing.Color]::Transparent
                break
            }
        }
        foreach ($k in $x.Controls) { $stack.Push($k) }
    }
}

function Set-TabTheme {
    # The tab headers: drawn by hand in UO (WinForms draws them in the system's colors otherwise). Plain: untouched.
    param($Tabs, $Theme)
    if (-not $Theme.Chrome) { return }
    $script:tabTheme = $Theme
    $Tabs.DrawMode = 'OwnerDrawFixed'
    # cc-P87: the labels are drawn bold (they were thin), so each tab is padded wider to hold the bolder word; the
    # pages keep the regular font. The selected tab is parchment with a thick gold bar along its top.
    # cc-P132: a look with crystals draws the rune slot before each header, so the tabs are padded wider for it.
    $Tabs.Padding = New-Object Drawing.Point($(if ($Theme.Crystals) { 28 } else { 20 }), 6)
    $script:tabFont = Get-ThemeFont 'Ui' ([Drawing.FontStyle]::Bold)
    $script:tabRune = $(if ($Theme.Crystals) { Get-ArtOrFallback 'rune-tab' 16 } else { $null })
    $Tabs.Add_DrawItem({
        param($s, $e)
        $c = $script:tabTheme.Chrome
        $page = $s.TabPages[$e.Index]
        $selected = ($e.Index -eq $s.SelectedIndex)
        $back = ConvertTo-DrawingColor $c.ButtonBack; $fore = ConvertTo-DrawingColor $c.ButtonText
        if ($selected) { $back = ConvertTo-DrawingColor $c.PageBack; $fore = ConvertTo-DrawingColor $c.PageText }
        # The LIVE tab keeps its warning color, as the LIVE caption does.
        if ($page.Text -eq 'LIVE shard' -and $selected) { $fore = ConvertTo-DrawingColor $c.PageWarn }
        $gold = ConvertTo-DrawingColor $c.Gold
        $b = New-Object Drawing.SolidBrush($back)
        $e.Graphics.FillRectangle($b, $e.Bounds)
        $pen = New-Object Drawing.Pen($gold)
        $e.Graphics.DrawRectangle($pen, $e.Bounds.X, $e.Bounds.Y, $e.Bounds.Width - 1, $e.Bounds.Height - 1)
        if ($selected) {
            $bar = New-Object Drawing.SolidBrush($gold)
            $e.Graphics.FillRectangle($bar, $e.Bounds.X, $e.Bounds.Y, $e.Bounds.Width, 4)
            $bar.Dispose()
        }
        $textBounds = $e.Bounds
        if ($script:tabRune) {
            $e.Graphics.DrawImage($script:tabRune, (New-Object Drawing.Rectangle(($e.Bounds.X + 6), ($e.Bounds.Y + [int](($e.Bounds.Height - 16) / 2)), 16, 16)))
            $textBounds = New-Object Drawing.Rectangle(($e.Bounds.X + 20), $e.Bounds.Y, ($e.Bounds.Width - 20), $e.Bounds.Height)
        }
        [Windows.Forms.TextRenderer]::DrawText($e.Graphics, $page.Text, $script:tabFont, $textBounds, $fore, [Windows.Forms.TextFormatFlags]'HorizontalCenter, VerticalCenter, NoPadding')
        $b.Dispose(); $pen.Dispose()
    })
}

function Get-LiveRetiredControls {
    # cc-P108: the controls greyed while live is retired, by action key (a button's Tag) or name, with their tooltip.
    # Every live.* button, the LIVE choice on Shard question and Snapshots, and Start fresh (it seeded from live's save).
    param($Config)
    $t = [ordered]@{}
    if (-not (Test-LiveRetired $Config)) { return $t }
    $why = $script:LiveRetiredText + ': ' + $Config.Live.Line + '.'
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Key -like 'live.*' })) { $t[$a.Key] = $why }
    $t['qLive'] = $why
    $t['snapLive'] = $why
    $t['test.fresh'] = $why + ' Start fresh copied its accounts from the live save; Wipe test accounts makes a new admin instead.'
    $t
}

function Set-LiveRetiredLook {
    # cc-P108: greys and disables every control Get-LiveRetiredControls names and gives it that tooltip. WinForms shows no
    # tooltip over a disabled control, so the LIVE shard tab's caption says the same words.
    param($Root, $Config, $ToolTip)
    $keys = Get-LiveRetiredControls $Config
    if (-not $keys.Count) { return 0 }
    $n = 0
    $stack = New-Object System.Collections.Stack
    $stack.Push($Root)
    while ($stack.Count) {
        $x = $stack.Pop()
        $key = $null
        if ($x.Tag -is [string] -and $keys.Contains($x.Tag)) { $key = $x.Tag }
        elseif ($x.Name -and $keys.Contains($x.Name)) { $key = $x.Name }
        if ($key) {
            if ($x -is [Windows.Forms.RadioButton] -and $x.Checked) { $x.Checked = $false }
            $x.Enabled = $false
            $x.ForeColor = [Drawing.Color]::Gray
            if ($ToolTip) { $ToolTip.SetToolTip($x, $keys[$key]) }
            $n++
        }
        foreach ($k in $x.Controls) { $stack.Push($k) }
    }
    $n
}

function Set-ConsoleTooltips {
    # Every control with an action key (its Tag) or a name in Get-ConsoleTooltips gets that text; a tab page gets
    # 'tab.<its title>'. One ToolTip for the window.
    param($Root, $Tips, $ToolTip)
    $stack = New-Object System.Collections.Stack
    $stack.Push($Root)
    while ($stack.Count) {
        $x = $stack.Pop()
        $key = $null
        if ($x.Tag -is [string] -and $Tips.Contains($x.Tag)) { $key = $x.Tag }
        elseif ($x.Name -and $Tips.Contains($x.Name)) { $key = $x.Name }
        if ($key) { $ToolTip.SetToolTip($x, $Tips[$key]) }
        if ($x -is [Windows.Forms.TabPage] -and $Tips.Contains('tab.' + $x.Text)) { $x.ToolTipText = $Tips['tab.' + $x.Text] }
        foreach ($k in $x.Controls) { $stack.Push($k) }
    }
}

function Get-ControlsWithoutTooltip {
    # cc-P153: every control a person points at (buttons, boxes, pickers, lists, check boxes, tabs, tiles, lights and
    # pictures) that has no hover text, as {Type, Name, Tag, Text}. The facts fail on any, so a new button cannot ship
    # without one. Captions (plain labels) are text already and are not counted; a tile counts once, as its panel.
    param($Root, $ToolTip)
    $out = New-Object 'System.Collections.Generic.List[object]'
    $stack = New-Object System.Collections.Stack
    $stack.Push($Root)
    while ($stack.Count) {
        $x = $stack.Pop()
        $need = ($x -is [Windows.Forms.ButtonBase] -or $x -is [Windows.Forms.TextBoxBase] -or $x -is [Windows.Forms.ComboBox] -or $x -is [Windows.Forms.ListView] -or
            $x -is [Windows.Forms.DataGridView] -or $x -is [Windows.Forms.NumericUpDown] -or $x -is [Windows.Forms.TrackBar] -or ($x -is [Windows.Forms.PictureBox] -and $x.Name) -or ($x -is [Windows.Forms.Panel] -and $x.Tag -eq 'tile'))
        if ($x -is [Windows.Forms.TabPage]) { if (-not $x.ToolTipText) { $out.Add([pscustomobject]@{ Type = 'TabPage'; Name = $x.Name; Tag = ''; Text = $x.Text }) } }
        elseif ($need -and -not $ToolTip.GetToolTip($x)) { $out.Add([pscustomobject]@{ Type = $x.GetType().Name; Name = $x.Name; Tag = $(if ($x.Tag -is [string]) { $x.Tag } else { '' }); Text = ([string]$x.Text -replace '\s+', ' ') }) }
        # A DataGridView's editing box, a ComboBox's or a NumericUpDown's inner parts are one control to a person.
        if ($x -is [Windows.Forms.DataGridView] -or $x -is [Windows.Forms.NumericUpDown] -or $x -is [Windows.Forms.ComboBox]) { continue }
        foreach ($k in $x.Controls) { $stack.Push($k) }
    }
    $out.ToArray()
}

function Enable-DisabledTooltips {
    # cc-P153: WinForms shows no tooltip over a greyed-out control, so the reason it is greyed ("greyed out: the Golem is
    # running P133") would never be seen. Each container shows its disabled children's tooltips itself, on hover.
    param($Root, $ToolTip)
    $stack = New-Object System.Collections.Stack
    $stack.Push($Root)
    while ($stack.Count) {
        $x = $stack.Pop()
        if ($x -is [Windows.Forms.Panel] -or $x -is [Windows.Forms.TabPage] -or $x -is [Windows.Forms.GroupBox] -or $x -is [Windows.Forms.Form] -or $x -is [Windows.Forms.SplitterPanel]) {
            $tt = $ToolTip
            $x.Add_MouseMove({
                param($s, $e)
                $c = $s.GetChildAtPoint($e.Location)
                if ($c -and -not $c.Enabled -and $tt.GetToolTip($c)) {
                    if ($script:DisabledTipFor -ne $c) { $script:DisabledTipFor = $c; $tt.Show($tt.GetToolTip($c), $s, $c.Left, $c.Bottom + 2, 8000) }
                } elseif ($script:DisabledTipFor -and $script:DisabledTipFor.Parent -eq $s) { $tt.Hide($s); $script:DisabledTipFor = $null }
            }.GetNewClosure())
            $x.Add_MouseLeave({ param($s, $e) if ($script:DisabledTipFor -and $script:DisabledTipFor.Parent -eq $s) { $tt.Hide($s); $script:DisabledTipFor = $null } }.GetNewClosure())
        }
        foreach ($k in $x.Controls) { $stack.Push($k) }
    }
}

function Format-ArgumentList {
    # A plan step's arguments as one Windows command line (a space or quote is quoted).
    param([string[]]$Arguments)
    (@($Arguments) | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' '
}

function Start-PaneJob {
    # Starts a process whose output streams into the Action output pane. The window never waits on it: the pane's
    # timer (Update-PaneJob) takes what has arrived every 150 ms.
    param([string]$Exe, [string]$Arguments, [string]$Title)
    Initialize-ConsoleNative
    $p = $script:ui.Pane
    $p.Box.Clear()
    Add-RichLine $p.Box ('== ' + $Title + '  (' + (Get-Date).ToString('HH:mm:ss', $script:Invariant) + ')') 'head'
    $script:paneStopped = $false
    $script:paneJob = New-Object ShardConsoleNative.PaneJob($Exe, $Arguments, 'SHARD_CONSOLE_PANE')
    $script:paneJobTitle = $Title
    $p.State.Text = 'Running: ' + $Title + ' (PID ' + $script:paneJob.Id + ')'
    $p.Stop.Enabled = $true
    Show-OutputPane $true
    $p.Timer.Start()
}

function Update-PaneJob {
    $job = $script:paneJob
    if (-not $job) { $script:ui.Pane.Timer.Stop(); return }
    $box = $script:ui.Pane.Box
    $line = $null
    $n = 0
    while ($n -lt 400 -and $job.Lines.TryDequeue([ref]$line)) {
        $l = ConvertFrom-PaneLine $line.Substring(1) ($line[0] -eq 'E')
        Add-RichLine $box $l.Text $l.Color
        $n++
    }
    if ($n) { $box.SelectionStart = $box.TextLength; $box.ScrollToCaret() }
    if ($job.Finished -and $job.Lines.IsEmpty) {
        $code = $job.ExitCode
        if ($script:paneStopped) { Add-RichLine $box ('== stopped (exit ' + $code + ')') 'amber' }
        else { $e = Format-PaneExit $code; Add-RichLine $box $e.Text $e.Color }
        $script:ui.Pane.State.Text = 'Last: ' + $script:paneJobTitle + ', exit ' + $code
        $script:ui.Pane.Stop.Enabled = $false
        $script:paneJob = $null
        $script:ui.Pane.Timer.Stop()
        Write-ConsoleLine ($script:paneJobTitle + ': finished in the Action output pane (exit ' + $code + '). Refresh the status if it changed anything.') 'gray'
    }
}

function Stop-PaneJob {
    $job = $script:paneJob
    if (-not $job) { return }
    $script:paneStopped = $true
    $rc = $job.KillTree()
    $what = 'ended ' + $script:paneJobTitle + ' (PID ' + $job.Id + ') and every process it started'
    if ($rc -ne 0) { $what = 'tried to end ' + $script:paneJobTitle + ' (taskkill said ' + $rc + '; it may have finished already)' }
    Add-RichLine $script:ui.Pane.Box ('== Stop: ' + $what + '.') 'amber'
    Write-ConsoleLine ('Stopped: ' + $what + '. A plan stopped part way says above what it had done.') 'amber'
}

function Show-OutputPane {
    param([bool]$Show)
    $p = $script:ui.Pane
    if (-not $p) { return }
    $p.Split.Panel2Collapsed = -not $Show
    $p.Hide.Text = 'Hide'
    if (-not $Show) { $p.Hide.Text = 'Show' }
}

function New-OutputPane {
    # The Action output pane (cc-P84): read-only, monospaced, scrolling, with Copy, Clear, Stop and Show/Hide on
    # a bar that stays visible. It sits beside the console's own output, collapsed until a job starts.
    param($OutBox, $Group)
    $split = New-Object Windows.Forms.SplitContainer
    $split.Dock = 'Fill'
    $split.Orientation = 'Vertical'
    $split.Panel1.Controls.Add($OutBox)
    $box = New-Object Windows.Forms.RichTextBox
    $box.Name = 'paneOutput'   # cc-P153: for its tooltip
    $box.Dock = 'Fill'
    $box.ReadOnly = $true
    $box.BackColor = ConvertTo-DrawingColor $script:theme.LineBack
    $box.Font = $script:MonoFont
    $box.WordWrap = $false
    $box.DetectUrls = $false
    $split.Panel2.Controls.Add($box)
    $bar = New-Object Windows.Forms.FlowLayoutPanel
    $bar.Dock = 'Top'
    $bar.Height = 30
    $state = New-Object Windows.Forms.Label
    $state.Text = 'Action output: nothing has run here yet'
    $state.AutoSize = $true
    $state.Margin = New-Object Windows.Forms.Padding(6, 7, 12, 0)
    $mk = {
        param($t, $n)
        $b = New-Object Windows.Forms.Button
        $b.Text = $t; $b.Name = $n; $b.Width = 70; $b.Height = 24
        $b.Margin = New-Object Windows.Forms.Padding(3, 2, 3, 2)
        $b
    }
    $hide = & $mk 'Show' 'paneHide'
    $copy = & $mk 'Copy' 'paneCopy'
    $clear = & $mk 'Clear' 'paneClear'
    $stop = & $mk 'Stop' 'paneStop'
    $stop.Enabled = $false
    $hide.Add_Click({ Show-OutputPane $script:ui.Pane.Split.Panel2Collapsed })
    $copy.Add_Click({ if ($script:ui.Pane.Box.TextLength) { [Windows.Forms.Clipboard]::SetText($script:ui.Pane.Box.Text); Write-ConsoleLine 'copied the Action output pane' 'gray' } })
    $clear.Add_Click({ $script:ui.Pane.Box.Clear() })
    $stop.Add_Click({ Stop-PaneJob })
    $bar.Controls.AddRange(@($state, $hide, $copy, $clear, $stop))
    $Group.Controls.Add($split)
    $Group.Controls.Add($bar)
    $timer = New-Object Windows.Forms.Timer
    $timer.Interval = 150
    $timer.Add_Tick({ Update-PaneJob })
    $split.Panel2Collapsed = $true
    [pscustomobject]@{ Split = $split; Box = $box; Bar = $bar; State = $state; Hide = $hide; Copy = $copy; Clear = $clear; Stop = $stop; Timer = $timer }
}

# --- the form ------------------------------------------------------------------------------

function Add-RichLine {
    param($Box, [string]$Text, [string]$Color = 'normal')
    # cc-P84: the line colors are the theme's (Plain: Black, Navy, ForestGreen, DarkOrange, Firebrick, DimGray, as before).
    $t = $script:theme
    if (-not $t) { $t = Get-ConsoleTheme 'Plain' }
    $rgb = $t.Lines[$Color]
    if (-not $rgb) { $rgb = $t.Lines['normal'] }
    $Box.SelectionStart = $Box.TextLength
    $Box.SelectionLength = 0
    $Box.SelectionColor = ConvertTo-DrawingColor $rgb
    if ($Color -eq 'head' -or $Color -eq 'red') { $Box.SelectionFont = $script:BoldFont } else { $Box.SelectionFont = $script:MonoFont }
    $Box.AppendText($Text + "`n")
    $Box.SelectionColor = $Box.ForeColor
}

function ConvertTo-RichLinesRtf {
    # cc-P154: the RTF for a box of console lines ({Text, Color}), in one string, so a panel is filled with one .Rtf
    # assignment: Add-RichLine took about 4 ms a line on the window's thread (the Golem panel's 75 lines: 0.8 s). The same
    # look as Add-RichLine: the theme's line colors (normal for an unknown name), the mono font, head and red in bold.
    # Bold holds each bold line's (start, length) in the box's text, for Set-RichLines to give the exact fonts (RTF
    # sizes are in half points, and the theme's mono is 9.75).
    param($Lines, $Theme, [string]$Font, [single]$Size)
    if (-not $Theme) { $Theme = Get-ConsoleTheme 'Plain' }
    $names = New-Object 'System.Collections.Generic.List[string]'
    $sb = New-Object Text.StringBuilder
    $bold = New-Object 'System.Collections.Generic.List[object]'
    $at = 0
    foreach ($l in @($Lines)) {
        $c = [string]$l.Color
        if (-not $Theme.Lines[$c]) { $c = 'normal' }
        $i = $names.IndexOf($c)
        if ($i -lt 0) { $names.Add($c); $i = $names.Count - 1 }
        $isBold = ($c -eq 'head' -or $c -eq 'red')
        [void]$sb.Append('\cf').Append($i + 1)
        if ($isBold) { [void]$sb.Append('\b ') } else { [void]$sb.Append('\b0 ') }
        $text = ([string]$l.Text).Replace("`r", '')
        if ($isBold) { $bold.Add(@($at, ($text.Length + 1))) }
        $at += $text.Length + 1
        foreach ($ch in $text.ToCharArray()) {
            $n = [int]$ch
            if ($ch -eq '\' -or $ch -eq '{' -or $ch -eq '}') { [void]$sb.Append('\').Append($ch) }
            elseif ($ch -eq "`n") { [void]$sb.Append('\par ') }
            elseif ($ch -eq "`t") { [void]$sb.Append('\tab ') }
            elseif ($n -gt 126) { [void]$sb.Append('\u').Append($(if ($n -gt 32767) { $n - 65536 } else { $n })).Append('?') }
            else { [void]$sb.Append($ch) }
        }
        [void]$sb.Append('\par').Append("`n")
    }
    $tbl = New-Object Text.StringBuilder
    foreach ($c in $names) { $rgb = $Theme.Lines[$c]; [void]$tbl.Append('\red').Append([int]$rgb[0]).Append('\green').Append([int]$rgb[1]).Append('\blue').Append([int]$rgb[2]).Append(';') }
    # RichEdit drops the last paragraph mark, so one more: the text ends in a newline, as Add-RichLine's does.
    [pscustomobject]@{
        Rtf  = '{\rtf1\ansi\deff0{\fonttbl{\f0\fmodern ' + $Font + ';}}{\colortbl ;' + $tbl.ToString() + '}\f0\fs' + [int][math]::Round($Size * 2) + ' ' + $sb.ToString() + '\par}'
        Bold = $bold.ToArray()
    }
}

function Set-RichLines {
    # cc-P154: replaces a box's text with these lines in one go (ConvertTo-RichLinesRtf), as clearing it and calling
    # Add-RichLine for each would: same text, colors and fonts.
    param($Box, $Lines)
    $f = $script:MonoFont
    if (-not $f) { $f = $Box.Font }
    $b = $script:BoldFont
    if (-not $b) { $b = New-Object Drawing.Font($f, [Drawing.FontStyle]::Bold) }
    $r = ConvertTo-RichLinesRtf -Lines $Lines -Theme $script:theme -Font $f.Name -Size $f.SizeInPoints
    $Box.Rtf = $r.Rtf
    $Box.SelectAll()
    $Box.SelectionFont = $f
    foreach ($x in $r.Bold) { $Box.Select($x[0], $x[1]); $Box.SelectionFont = $b }
    $Box.Select(0, 0)
}

function Get-TileColors {
    # Background and text for a tile state, from the theme (cc-P84). Any state but green, amber and red is grey.
    param([string]$Color)
    $t = $script:theme
    if (-not $t) { $t = Get-ConsoleTheme 'Plain' }
    $k = $Color
    if ($k -notin @('green', 'amber', 'red')) { $k = 'grey' }
    @((ConvertTo-DrawingColor $t.Tiles[$k].Back), (ConvertTo-DrawingColor $t.Tiles[$k].Fore))
}

function New-StatusTile {
    # cc-P132: in a look with crystals (Enhanced) the tile's state is also a crystal in its top-right corner (a child of the
    # word label, so Update-TileFit's measuring is untouched), and -Figure adds the Golem figure at the right of the detail.
    param([string]$Title, [switch]$Figure)
    $crystals = [bool]($script:theme -and $script:theme.Crystals)
    $panel = New-Object Windows.Forms.Panel
    $panel.Dock = 'Fill'
    $panel.Margin = New-Object Windows.Forms.Padding(4)
    $panel.BorderStyle = 'FixedSingle'
    # cc-P84: named for its tooltip, tagged so the theme leaves its state colors alone.
    $panel.Name = 'tile.' + $Title
    $panel.Tag = 'tile'
    $word = New-Object Windows.Forms.Label
    $word.Dock = 'Top'
    $word.Height = 30
    $word.Name = 'tile.' + $Title
    $word.Font = Get-ThemeFont 'Tile' ([Drawing.FontStyle]::Bold)
    $word.Text = $Title + ': ...'
    $word.Padding = New-Object Windows.Forms.Padding(6, 4, $(if ($crystals) { 34 } else { 0 }), 0)
    $detail = New-Object Windows.Forms.Label
    $detail.Dock = 'Fill'
    $detail.Name = 'tile.' + $Title
    $detail.Font = Get-ThemeFont 'Detail'
    $detail.Padding = New-Object Windows.Forms.Padding(8, 2, $(if ($crystals -and $Figure) { 62 } else { 4 }), 0)
    $crystal = $null; $fig = $null
    if ($crystals) {
        $crystal = New-Object Windows.Forms.PictureBox
        $crystal.Name = 'tile.' + $Title
        $crystal.Size = New-Object Drawing.Size(28, 28)
        $crystal.SizeMode = 'Zoom'
        $crystal.BackColor = [Drawing.Color]::Transparent
        $crystal.Anchor = 'Top, Right'
        $crystal.Location = New-Object Drawing.Point([math]::Max(0, $word.Width - 32), 1)
        $crystal.Image = Get-ArtOrFallback 'crystal-grey' 28
        $word.Controls.Add($crystal)
        if ($Figure) {
            $fig = New-Object Windows.Forms.PictureBox
            $fig.Name = 'tile.' + $Title
            $fig.Size = New-Object Drawing.Size(56, 56)
            $fig.SizeMode = 'Zoom'
            $fig.BackColor = [Drawing.Color]::Transparent
            $fig.Anchor = 'Top, Right'
            $fig.Location = New-Object Drawing.Point([math]::Max(0, $detail.Width - 60), 2)
            $fig.Image = Get-ArtOrFallback 'golem-dark' 56
            $detail.Controls.Add($fig)
        }
    }
    $panel.Controls.Add($detail)
    $panel.Controls.Add($word)
    [pscustomobject]@{ Panel = $panel; Word = $word; Detail = $detail; Crystal = $crystal; Figure = $fig }
}

function Set-TileCrystal {
    # cc-P132: the crystal that matches a tile's state (green, amber, red; anything else is the dull grey one).
    param($Tile, [string]$Color)
    if (-not $Tile -or -not $Tile.Crystal) { return }
    $k = $Color
    if ($k -notin @('green', 'amber', 'red')) { $k = 'grey' }
    $img = Get-ArtOrFallback ('crystal-' + $k) 28
    if ($Tile.Crystal.Image -ne $img) { $Tile.Crystal.Image = $img }
}

function Update-GlanceTiles {
    param($State)
    $tiles = @(Get-AtAGlance $State $script:cfg)
    $titleBits = @()
    for ($i = 0; $i -lt $tiles.Count -and $i -lt $script:ui.Tiles.Count; $i++) {
        $t = $tiles[$i]; $ui = $script:ui.Tiles[$i]
        $cols = Get-TileColors $t.Color
        $ui.Panel.BackColor = $cols[0]
        $ui.Word.ForeColor = $cols[1]
        $ui.Detail.ForeColor = $cols[1]
        $ui.Word.Text = $t.Title + ': ' + $t.Word
        $ui.Detail.Text = $t.Detail
        Set-TileCrystal $ui $t.Color
        $short = $t.Title
        if ($short -eq 'STATUS PUBLISHER') { $short = 'publisher' }
        $titleBits += ($short + ' ' + $t.Word.ToLower())
    }
    # The window title carries the same three words, so the taskbar shows them too.
    $prefix = 'Shard Console'
    if ($script:dry) { $prefix = 'Shard Console - DRY RUN' }
    if ($script:ui.Form) { $script:ui.Form.Text = $prefix + ' - ' + ($titleBits -join ' | ') }
    Update-TileFit
}

function Set-TileLook {
    param($Tile, $Glance)
    $cols = Get-TileColors $Glance.Color
    $Tile.Panel.BackColor = $cols[0]
    $Tile.Word.ForeColor = $cols[1]
    $Tile.Detail.ForeColor = $cols[1]
    $Tile.Word.Text = $Glance.Title + ': ' + $Glance.Word
    $Tile.Detail.Text = $Glance.Detail
    Set-TileCrystal $Tile $Glance.Color
    Update-TileFit
}

function Set-GolemFigure {
    # cc-P132: the Golem figure on the GOLEM tile and the Golem tab follows the Golem's state: glowing while it runs a
    # prompt, a soft glow when awake with nothing running, dark when resting, stopped or off.
    param($View)
    $state = 'dark'
    $s = $(if ($View) { $View.Status } else { $null })
    $word = $(if ($s) { $s.State } elseif ($View -and $View.Enabled) { 'IDLE' } else { 'OFF' })
    if ($word -in @('RUNNING', 'NEEDS YOU')) { $state = 'running' }
    elseif ($word -eq 'IDLE') { $state = 'idle' }
    $img = Get-ArtOrFallback ('golem-' + $state) 56
    if ($script:ui.AutopilotTile -and $script:ui.AutopilotTile.Figure -and $script:ui.AutopilotTile.Figure.Image -ne $img) { $script:ui.AutopilotTile.Figure.Image = $img }
    if ($script:ui.GolemFigure) { $big = Get-ArtOrFallback ('golem-' + $state) 64; if ($script:ui.GolemFigure.Image -ne $big) { $script:ui.GolemFigure.Image = $big } }
    $state
}

function New-LightLabel {
    # cc-P132: a status light beside a button: a crystal (in a look with crystals) and a line of words, named for its
    # tooltip. Update-Lights fills it.
    param([string]$Name)
    $row = New-Object Windows.Forms.FlowLayoutPanel
    $row.Name = $Name
    $row.AutoSize = $true; $row.AutoSizeMode = 'GrowAndShrink'
    $row.WrapContents = $false
    $row.Margin = New-Object Windows.Forms.Padding(6, 8, 6, 6)
    $row.BackColor = [Drawing.Color]::Transparent
    $pic = New-Object Windows.Forms.PictureBox
    $pic.Name = $Name
    $pic.Size = New-Object Drawing.Size(22, 22)
    $pic.SizeMode = 'Zoom'
    $pic.BackColor = [Drawing.Color]::Transparent
    $pic.Margin = New-Object Windows.Forms.Padding(0, 2, 4, 0)
    $pic.Visible = [bool]($script:theme -and $script:theme.Crystals)
    $lbl = New-Object Windows.Forms.Label
    $lbl.Name = $Name
    $lbl.AutoSize = $true
    $lbl.MaximumSize = New-Object Drawing.Size(700, 0)
    $lbl.Margin = New-Object Windows.Forms.Padding(0, 4, 0, 0)
    $lbl.Font = Get-ThemeFont 'Ui' ([Drawing.FontStyle]::Bold)
    $lbl.Text = '...'
    $row.Controls.AddRange(@($pic, $lbl))
    [pscustomobject]@{ Row = $row; Picture = $pic; Label = $lbl }
}

function Get-IcoFrameSizes {
    # cc-P153. Pure: the frame sizes an .ico holds (its directory: a count at byte 4, then 16 bytes a frame, width first,
    # 0 meaning 256), largest first.
    param([byte[]]$Bytes)
    if (-not $Bytes -or $Bytes.Length -lt 6 -or $Bytes[2] -ne 1 -or $Bytes[3] -ne 0) { return @() }
    $n = [int]$Bytes[4] + 256 * [int]$Bytes[5]
    @(for ($i = 0; $i -lt $n -and (6 + 16 * $i) -lt $Bytes.Length; $i++) { $w = [int]$Bytes[6 + 16 * $i]; if ($w -eq 0) { 256 } else { $w } }) | Sort-Object -Unique -Descending
}

function Select-IcoFrameSize {
    # cc-P153. Pure: the smallest frame at or above $Want (drawn 1:1 or scaled down, never blown up), else the largest.
    param([int[]]$Sizes, [int]$Want)
    $up = @(@($Sizes) | Where-Object { $_ -ge $Want } | Sort-Object)
    if ($up.Count) { return $up[0] }
    (@($Sizes) | Sort-Object -Descending | Select-Object -First 1)
}

function Get-HeaderIconImage {
    # cc-P153: the header's icon: the window's .ico at the frame Select-IcoFrameSize picks, as a bitmap; $null if unreadable.
    param([string]$Path, [int]$Size = 48)
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $bytes = [IO.File]::ReadAllBytes($Path)
    $sz = Select-IcoFrameSize @(Get-IcoFrameSizes $bytes) $Size
    if (-not $sz) { return $null }
    # This favicon's frames are PNG inside the .ico, which Icon.ToBitmap cannot read on .NET Framework ("Requested range
    # extends past the end of the array"): a PNG frame is read as the PNG it is; an old-style (BMP) frame through Icon.
    $n = [int]$bytes[4] + 256 * [int]$bytes[5]
    for ($i = 0; $i -lt $n; $i++) {
        $e = 6 + 16 * $i
        $w = [int]$bytes[$e]; if ($w -eq 0) { $w = 256 }
        if ($w -ne $sz) { continue }
        $len = [BitConverter]::ToInt32($bytes, $e + 8); $off = [BitConverter]::ToInt32($bytes, $e + 12)
        if ($off + $len -gt $bytes.Length) { return $null }
        if ($bytes[$off] -eq 0x89 -and $bytes[$off + 1] -eq 0x50) { return [Drawing.Image]::FromStream((New-Object IO.MemoryStream($bytes, $off, $len))) }
        $ico = New-Object Drawing.Icon($Path, $sz, $sz)
        try { return $ico.ToBitmap() } finally { $ico.Dispose() }
    }
    $null
}

function Update-PackageSuggestion {
    # cc-P153: puts the suggested What is new line in the notes box when it is empty or still holds the last suggestion
    # (an edit of Chase's is never written over), and the list it was made from under the box (cc-P288: Format-WhatsNewList).
    param($Facts)
    $box = $script:ui.PackageNotes
    if (-not $box) { return }
    $sg = Get-PackageSuggestion $Facts
    $script:ui.PackageSuggestion = $sg
    if ($sg) {
        if (-not $box.Focused -and (-not $box.Text.Trim() -or $box.Text -eq [string]$script:ui.PackageSuggested)) { $box.Text = $sg.Line; $script:ui.PackageSuggested = $sg.Line }
    }
    $from = $script:ui.PackageNotesFrom
    if ($from) { $from.Text = Format-WhatsNewList $sg $(if ($Facts -and $Facts.WhatsNew) { [string]$Facts.WhatsNew.Note } else { '' }) }
}

function Set-LightLabel {
    # The words and the crystal of a light, in the theme's state text color (the words carry the meaning; the color
    # and the crystal only repeat it).
    param($Light, $Judged, [string]$Lead)
    if (-not $Light -or -not $Judged) { return }
    $Light.Label.Text = $Lead + $Judged.Word + ': ' + $Judged.Detail
    $t = $script:theme
    $key = @{ green = 'Success'; amber = 'Warning'; red = 'Danger' }[$Judged.Color]
    if ($t -and $t.Text) { $Light.Label.ForeColor = ConvertTo-DrawingColor $(if ($key) { $t.Text.$key } else { $t.Text.Muted }) }
    else { $Light.Label.ForeColor = @{ green = [Drawing.Color]::ForestGreen; amber = [Drawing.Color]::DarkOrange; red = [Drawing.Color]::Firebrick }[$Judged.Color]; if (-not $key) { $Light.Label.ForeColor = [Drawing.Color]::DimGray } }
    $k = $Judged.Color; if ($k -notin @('green', 'amber', 'red')) { $k = 'grey' }
    $img = Get-ArtOrFallback ('crystal-' + $k) 22
    if ($Light.Picture.Image -ne $img) { $Light.Picture.Image = $img }
    # cc-P153: a light not judged yet (no hover) keeps its own tooltip, so it never goes blank.
    $hv = $(if ($Judged.Hover) { $Judged.Hover } else { (Get-ConsoleTooltips)[[string]$Light.Picture.Name] })
    if ($script:ui.ToolTip) { $script:ui.ToolTip.SetToolTip($Light.Label, $hv); $script:ui.ToolTip.SetToolTip($Light.Picture, $hv) }
}

function Update-Lights {
    # cc-P132 sections 6 and 7: the rebuild light (TEST tile and the Test shard tab) and the package light (the Player
    # package tab), judged from facts read now; both written to LightsFile for the status page and P143.
    # cc-P154: -Gathered draws from -RebuildFacts and -PackageFacts read elsewhere (the refresh runspace); with no package
    # facts in this refresh (they are read on the Player package tab and every two minutes) the last package light stays.
    param($State, $RebuildFacts, $PackageFacts, [switch]$Gathered)
    if (-not $State) { return }
    try {
        $rf = $(if ($Gathered) { $RebuildFacts } else { Get-RebuildFacts $script:cfg $State })
        if (-not $rf) { return }
        $rb = (Get-LightsFromFacts -RebuildFacts $rf).Rebuild
        Set-LightLabel $script:ui.RebuildLight $rb 'Rebuild: '
        # The TEST tile's second line.
        if ($script:ui.Tiles -and $script:ui.Tiles.Count -gt 1 -and $script:ui.Tiles[1].Word.Text -like 'TEST*') {
            $d = $script:ui.Tiles[1].Detail
            $first = (([string]$d.Text) -split "`n")[0]
            $d.Text = $first + "`n" + 'rebuild: ' + $rb.Word.ToLower() + ', ' + $rb.Detail
        }
        $pf = $null; $pk = $null
        if (-not $script:fromCapture -and $Gathered -and -not $PackageFacts) {
            if ($script:lights -and $script:lights.Package) { $pk = $script:lights.Package; $pf = $script:lights.PackageFacts }
            else { $pk = [pscustomobject]@{ Color = 'grey'; Word = 'CHECKING'; Detail = 'read on the Player package tab, and within two minutes'; Commits = @(); Hover = '' } }
        } elseif (-not $script:fromCapture) {
            $pf = $(if ($Gathered) { $PackageFacts } else { Get-PackageLightFacts $script:cfg })
            $pk = (Get-LightsFromFacts -PackageFacts $pf).Package
        } else {
            $pk = [pscustomobject]@{ Color = 'grey'; Word = 'UNKNOWN'; Detail = 'not judged under DRY RUN (the status is a capture file)'; Commits = @(); Hover = '' }
        }
        Set-LightLabel $script:ui.PackageLight $pk 'Package: '
        if ($pf) { Update-PackageSuggestion $pf }
        $script:lights = [pscustomobject]@{ Rebuild = $rb; Package = $pk; RebuildFacts = $rf; PackageFacts = $pf }
        if ($script:cfg.LightsFile) {
            $dir = Split-Path -Parent $script:cfg.LightsFile
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            [IO.File]::WriteAllText($script:cfg.LightsFile, (ConvertTo-LightsJson $rb $pk ([datetime]::Now)), (New-Object Text.UTF8Encoding $false))
        }
        Update-TileFit
    } catch { Write-ConsoleLine ('lights: ' + $_.Exception.Message) 'gray' }
}

$script:TileFitSizes = @{}

# cc-P154: the window's drawing turned off around a change of layout, then one repaint. In the themed looks every
# see-through control repainted its parent as the layout moved it: a tile row 10 px taller held the window about 2 s,
# and 0.19 s with drawing off (measured, notes/cc-P154-console-responsive.md). WM_SETREDRAW through WinForms' own
# SendMessage, so nothing has to be compiled. Nested holds draw once, at the last release.
$script:DrawHold = 0
$script:SendMessageMethod = $null

function Suspend-WindowDraw {
    # $true when drawing was held (the caller then calls Resume-WindowDraw); $false with no window yet (the facts' forms).
    $c = $(if ($script:ui) { $script:ui.Form } else { $null })
    if (-not $c -or -not $c.IsHandleCreated) { return $false }
    if (-not $script:SendMessageMethod) { $script:SendMessageMethod = [Windows.Forms.Control].GetMethod('SendMessage', [Reflection.BindingFlags]'NonPublic, Instance', $null, [type[]]@([int], [int], [int]), $null) }
    if (-not $script:SendMessageMethod) { return $false }
    if ($script:DrawHold -eq 0) { [void]$script:SendMessageMethod.Invoke($c, @(11, 0, 0)) }
    $script:DrawHold++
    $true
}

function Resume-WindowDraw {
    # -Only: the one control whose part of the window changed (a tab switch: the tabs); unset, the whole window.
    param($Only)
    if ($script:DrawHold -le 0) { return }
    $script:DrawHold--
    $c = $(if ($script:ui) { $script:ui.Form } else { $null })
    if ($script:DrawHold -eq 0 -and $c -and $c.IsHandleCreated -and $script:SendMessageMethod) {
        [void]$script:SendMessageMethod.Invoke($c, @(11, 1, 0))
        if ($Only) { $Only.Invalidate($true) } else { $c.Invalidate($true) }
    }
}

function Get-TileFitFont {
    # The largest size from $Size down to $Floor (0.25 point steps) at which $Text fits $Width, and $Height when
    # it is not 0 (wrapping at word breaks). The floor when none does.
    param([string]$Text, [string]$Family, [single]$Size, [single]$Floor, [Drawing.FontStyle]$Style, [int]$Width, [int]$Height = 0)
    # cc-P154: the size found is remembered for the same text, font and box, so a tile whose words did not change is not
    # measured again at every step down (the first fill of the window spent 2.4 s here). At most 500 kept.
    $key = [string]$Family + '|' + $Size + '|' + $Floor + '|' + [int]$Style + '|' + $Width + '|' + $Height + '|' + $Text
    $known = $script:TileFitSizes[$key]
    if ($known) { return (New-Object Drawing.Font($Family, [single]$known, $Style)) }
    if ($script:TileFitSizes.Count -ge 500) { $script:TileFitSizes.Clear() }
    $flags = [Windows.Forms.TextFormatFlags]'WordBreak'
    if (-not $Height) { $flags = [Windows.Forms.TextFormatFlags]'SingleLine' }
    for ($s = $Size; $s -ge $Floor; $s -= 0.25) {
        $f = New-Object Drawing.Font($Family, [single]$s, $Style)
        $m = [Windows.Forms.TextRenderer]::MeasureText($Text, $f, (New-Object Drawing.Size($Width, 0)), $flags)
        if ($m.Width -le $Width -and (-not $Height -or $m.Height -le $Height)) { $script:TileFitSizes[$key] = [single]$s; return $f }
        $f.Dispose()
    }
    $script:TileFitSizes[$key] = [single]$Floor
    New-Object Drawing.Font($Family, [single]$Floor, $Style)
}

function Update-CaptionFit {
    # cc-P87: a wide caption (made 1,100 px wide by New-Caption) wraps to its page's width instead of running past it
    # when the window is narrower than that. cc-P116: in both looks (Plain too), to the tab page's own width less a
    # scroll bar, and then each page's scrolling is worked out again (Update-PageScroll), since the captions' heights
    # changed. A caption that has been fitted keeps its 1100 in Tag, so it can widen again.
    $tabs = $script:ui.Tabs
    if (-not $tabs -or -not $script:ui.Form) { return }
    $page = $tabs.DisplayRectangle.Width
    if ($page -le 0) { $page = $script:ui.Form.ClientSize.Width }
    $w = [math]::Max(300, [math]::Min(1100, [math]::Min($page, $script:ui.Form.ClientSize.Width) - 40))
    $stack = New-Object System.Collections.Stack
    foreach ($pg in $tabs.TabPages) { $stack.Push($pg) }
    while ($stack.Count) {
        $x = $stack.Pop()
        if ($x -is [Windows.Forms.Label] -and ($x.MaximumSize.Width -ge 1000 -or [string]$x.Tag -eq 'caption')) {
            $x.Tag = 'caption'
            if ($x.MaximumSize.Width -ne $w) { $x.MaximumSize = New-Object Drawing.Size($w, 0) }
        }
        foreach ($k in $x.Controls) { $stack.Push($k) }
    }
    Update-PageScroll
}

$script:PageFillMin = 80

function Update-PageScroll {
    # cc-P116: every tab page whose content does not fit gets scroll bars (Chase, 2026-10-14: the Golem tab's buttons
    # were cut off with no scroll bar). A page that is one scrolling panel already (the button pages, World setup)
    # is left to it. Any other page scrolls once it is smaller than what its docked parts need: each top or bottom
    # part at its own height (a wrapping one at the height it wraps to at this width), PageFillMin for the list or
    # text that fills the rest, and as wide as its widest single control. The mouse wheel over a page scrolls it:
    # a button or label passes the wheel up to its page (Windows sends it to the control under the mouse).
    $tabs = $script:ui.Tabs
    if (-not $tabs) { return }
    foreach ($pg in $tabs.TabPages) {
        $kids = @($pg.Controls)
        $fill = @($kids | Where-Object { $_.Dock -eq 'Fill' })
        if ($kids.Count -eq 1 -and $fill.Count -eq 1 -and $fill[0] -is [Windows.Forms.ScrollableControl] -and $fill[0].AutoScroll) {
            if ($pg.AutoScroll) { $pg.AutoScroll = $false }
            continue
        }
        $w = $pg.ClientSize.Width
        $h = 0; $minW = 0
        foreach ($k in $kids) {
            if ($k.Dock -eq 'Fill') { $h += $script:PageFillMin; continue }
            $kh = $k.Height
            if ($k.AutoSize) { $kh = $k.GetPreferredSize((New-Object Drawing.Size([math]::Max(1, $w), 0))).Height }
            $h += $kh
            foreach ($c in @($k.Controls)) {
                if ($c -is [Windows.Forms.Label] -and [string]$c.Tag -eq 'caption') { continue }
                $cw = $c.Width + $c.Margin.Horizontal + $k.Padding.Horizontal
                if ($cw -gt $minW) { $minW = $cw }
            }
        }
        $need = New-Object Drawing.Size($minW, $h)
        if (-not $pg.AutoScroll) { $pg.AutoScroll = $true }
        if ($pg.AutoScrollMinSize -ne $need) { $pg.AutoScrollMinSize = $need }
    }
}

function Update-TileFit {
    # cc-P87, UO only (Plain keeps its fixed row of six, as before): no tile ever clips. Six across while every
    # tile's buttons fit a sixth of the width, otherwise three across in two rows. Then in each tile the title
    # shrinks toward Fonts.TileFloor and wraps onto a second line only if it still does not fit, the buttons wrap
    # onto a second row if they must, and the detail shrinks toward Fonts.DetailFloor until it fits what is left.
    $g = $script:ui.Glance
    $t = $script:theme
    if (-not $g -or -not $t -or -not $t.Chrome -or $script:tileFitting) { return }
    # cc-P154: while a refresh draws, once at its end (Show-ConsoleRefresh), not once per tile that changed.
    if ($script:tileFitHold) { $script:tileFitWanted = $true; return }
    $script:tileFitting = $true
    $held = $false
    try {
        $tiles = @(@($script:ui.Tiles) + @($script:ui.NetTile, $script:ui.AutopilotTile, $script:ui.PlayTestTile, $script:ui.WebsitesTile) | Where-Object { $_ })
        if ($g.ClientSize.Width -le 0) { return }
        # The widest row of buttons a tile holds, unwrapped; a tile is its 4 px margins and a 1 px border wider.
        $need = 0
        foreach ($tile in $tiles) {
            foreach ($bar in @($tile.Panel.Controls | Where-Object { $_ -is [Windows.Forms.FlowLayoutPanel] })) {
                $w = $bar.Padding.Horizontal
                foreach ($k in $bar.Controls) { $w += $k.PreferredSize.Width + $k.Margin.Horizontal }
                if ($w -gt $need) { $need = $w }
            }
        }
        # cc-P101: seven tiles now. All in one row while each fits, otherwise half (rounded up) in each of two rows.
        $n = [math]::Max(1, $tiles.Count)
        $cols = $n
        if (($g.ClientSize.Width / $n) - 10 -lt [math]::Max($need, 190)) { $cols = [int][math]::Ceiling($n / 2) }
        $rows = [int][math]::Ceiling($n / $cols)
        # Each row as tall as its tallest tile needs at full size (title, detail and buttons at this width), from 96
        # to 150 px (cc-P101; was 130); past that the detail shrinks instead.
        $colW = [int]($g.ClientSize.Width / $cols) - 10
        $tall = 96
        foreach ($tile in $tiles) {
            $tf = New-Object Drawing.Font($t.Fonts.Tile, [single]$t.Fonts.TileFloor, [Drawing.FontStyle]::Bold)
            $h = [math]::Max(30, [Windows.Forms.TextRenderer]::MeasureText($tile.Word.Text, $tf, (New-Object Drawing.Size(($colW - $tile.Word.Padding.Horizontal - 2), 0)), [Windows.Forms.TextFormatFlags]'WordBreak').Height + $tile.Word.Padding.Vertical + 2)
            $tf.Dispose()
            $df = New-Object Drawing.Font($t.Fonts.Detail, [single]$t.Fonts.DetailSize)
            $h += [Windows.Forms.TextRenderer]::MeasureText($tile.Detail.Text, $df, (New-Object Drawing.Size(($colW - $tile.Detail.Padding.Horizontal - 2), 0)), [Windows.Forms.TextFormatFlags]'WordBreak').Height + $tile.Detail.Padding.Vertical
            $df.Dispose()
            foreach ($bar in @($tile.Panel.Controls | Where-Object { $_ -is [Windows.Forms.FlowLayoutPanel] })) { $h += [math]::Max(30, $bar.GetPreferredSize((New-Object Drawing.Size($colW, 0))).Height) }
            if ($h + 12 -gt $tall) { $tall = $h + 12 }
        }
        # cc-P101: 150 (was 130): four across at 900 px is narrower than three was, so a detail needs a line more.
        $tall = [math]::Min(150, $tall)
        # cc-P154: a change of rows or columns lays the whole window out: with its drawing off (Suspend-WindowDraw).
        if (($script:ui.Grid -and $script:ui.Grid.RowStyles[0].Height -ne ($tall * $rows + 8)) -or $g.ColumnCount -ne $cols) { $held = Suspend-WindowDraw }
        if ($script:ui.Grid -and $script:ui.Grid.RowStyles[0].Height -ne ($tall * $rows + 8)) {
            $script:ui.Grid.RowStyles[0].Height = $tall * $rows + 8
            $script:ui.Grid.PerformLayout()
        }
        if ($g.ColumnCount -ne $cols) {
            $g.SuspendLayout()
            $g.ColumnStyles.Clear(); $g.RowStyles.Clear()
            $g.ColumnCount = $cols; $g.RowCount = $rows
            foreach ($i in 1..$cols) { [void]$g.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent', (100.0 / $cols)))) }
            foreach ($i in 1..$rows) { [void]$g.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', (100.0 / $rows)))) }
            for ($i = 0; $i -lt $tiles.Count; $i++) {
                $g.SetCellPosition($tiles[$i].Panel, (New-Object Windows.Forms.TableLayoutPanelCellPosition(($i % $cols), [math]::Floor($i / $cols))))
            }
            $g.ResumeLayout($true)
            if ($script:ui.Grid) { $script:ui.Grid.PerformLayout() }
        }
        $bold = [Drawing.FontStyle]::Bold
        foreach ($tile in $tiles) {
            $inner = $tile.Panel.ClientSize.Width
            if ($inner -le 0) { continue }
            $word = $tile.Word
            $ww = $inner - $word.Padding.Horizontal - 2
            $f = Get-TileFitFont $word.Text $t.Fonts.Tile $t.Fonts.TileSize $t.Fonts.TileFloor $bold $ww
            if ($word.Font.Size -ne $f.Size) { $word.Font = $f } else { $f.Dispose() }
            $one = [Windows.Forms.TextRenderer]::MeasureText($word.Text, $word.Font, (New-Object Drawing.Size($ww, 0)), [Windows.Forms.TextFormatFlags]'WordBreak')
            $word.Height = [math]::Max(30, $one.Height + $word.Padding.Vertical + 2)
            $barH = 0
            foreach ($bar in @($tile.Panel.Controls | Where-Object { $_ -is [Windows.Forms.FlowLayoutPanel] })) {
                $bar.Height = [math]::Max(30, $bar.GetPreferredSize((New-Object Drawing.Size($inner, 0))).Height)
                $barH += $bar.Height
            }
            $d = $tile.Detail
            $dw = $inner - $d.Padding.Horizontal - 2
            $dh = $tile.Panel.ClientSize.Height - $word.Height - $barH - $d.Padding.Vertical
            $f = Get-TileFitFont $d.Text $t.Fonts.Detail $t.Fonts.DetailSize $t.Fonts.DetailFloor ([Drawing.FontStyle]::Regular) $dw ([math]::Max($dh, 1))
            if ($d.Font.Size -ne $f.Size) { $d.Font = $f } else { $f.Dispose() }
        }
    } finally {
        $script:tileFitting = $false
        if ($held) { Resume-WindowDraw }
    }
}

function Update-AutopilotView {
    # cc-P74: the GOLEM tile and, when it exists, the Golem tab's panel. Read from disk on
    # every status refresh (and when the tab is opened), never from the capture file: it is not docker.
    # cc-P99: which folder is active is re-read too, so a cutover shows at the next refresh.
    $g = $script:cfg.Autopilot
    if ($g -and $g.GolemRoot -and $g.AutopilotRoot) { $script:cfg.Autopilot = Get-GolemConfig $g.GolemRoot $g.AutopilotRoot $g.Queue }
    Show-AutopilotView (Get-AutopilotViewNow $script:cfg)
}

function Show-AutopilotView {
    # cc-P154: draws a Golem view read elsewhere (Get-AutopilotViewNow, often in the refresh runspace): the tile, the
    # figure, the run controls, and the Golem tab's panel. Under -BackgroundRefresh the panel is drawn only while its tab
    # is showing; opening the tab draws the last view at once. Returns the view (the last one when given none).
    param($A)
    if (-not $A) { return $script:autopilot }
    $script:autopilot = $A
    if ($script:ui.AutopilotTile) { Set-TileLook $script:ui.AutopilotTile $A.Tile }
    # cc-P132: the Golem figure follows the state.
    [void](Set-GolemFigure $A.View)
    $box = $script:ui.AutopilotPanel
    $shown = (-not $script:BackgroundRefresh) -or ($script:ui.Tabs -and $script:ui.Tabs.SelectedTab -eq $script:ui.AutopilotPage)
    if ($box -and $shown) {
        # cc-P154: in one go (Set-RichLines); line by line it held the window 0.8 s.
        $lines = $(if ($A.PSObject.Properties['PanelLines']) { @($A.PanelLines) } else { @(Format-AutopilotPanel -View $A.View -Config $script:cfg -Now ([datetime]::Now)) })
        Set-RichLines $box $lines
        $box.SelectionStart = 0
        $box.ScrollToCaret()
    }
    Update-GolemRunControls $A.View
    $A
}

function Get-GolemWakeBlock {
    # cc-P153. Pure: why Poke the Golem is greyed out, in plain words for its tooltip, or '' when it lights. The rules
    # are cc-P131's: the Golem on, a prompt queued, nothing running in a live session (a session quiet past stallMinutes
    # counts as gone), nothing STOPPED, and any rest's readyAt passed. The plan re-checks all of it, and the CLI, at the click.
    param($View, [datetime]$Now)
    if (-not $View -or $View.Kind -ne 'golem') { return 'greyed out: the Golem is not set up on this PC' }
    if (-not $View.Enabled) { return 'greyed out: the Golem is off (press Wake the Golem first)' }
    if (-not $View.Queue -or -not @($View.Queue.Runs).Count) { return 'greyed out: the queue is empty' }
    $s = $View.Status
    if (-not $s) { return '' }
    if ($s.State -eq 'STOPPED') { return ('greyed out: ' + $(if ($s.StoppedPrompt) { $s.StoppedPrompt } else { 'a prompt' }) + ' is STOPPED and needs you (Retry stopped prompt, or answer it)') }
    if ($s.State -in @('RUNNING', 'NEEDS YOU') -and $s.Current) {
        $act = Get-AutopilotActivity -Status $s -Settings $View.Settings -Now $Now
        if (-not ($act.Quiet -or $act.Stale)) { return ('greyed out: the Golem is running ' + $s.Current + ' in a live session') }
    }
    if ($s.ReadyAt -and $s.ReadyAt -gt $Now) { return ('greyed out: the Golem is resting until ' + $s.ReadyAt.ToString('HH:mm', $script:Invariant) + $(if ($s.RestReason) { ' (' + $s.RestReason + ')' } else { '' })) }
    ''
}

function Test-GolemWakeReady {
    # cc-P131. Pure: does Poke the Golem (cc-P153; it was Wake the Golem (CC session)) light? Get-GolemWakeBlock says why not.
    param($View, [datetime]$Now)
    -not (Get-GolemWakeBlock -View $View -Now $Now)
}

function Find-HandedPromptPath {
    # cc-P153: where a running prompt's file waits for its commit: the prompts folder, or claimed\<Golem>\ (several Golems).
    param([string]$PromptDir, [string]$Name)
    if (-not $PromptDir -or -not $Name) { return '' }
    $p = Join-Path $PromptDir $Name
    if (Test-Path -LiteralPath $p) { return $p }
    $c = @(Get-ChildItem -LiteralPath (Join-Path $PromptDir 'claimed') -Recurse -Filter $Name -File -ErrorAction SilentlyContinue | Select-Object -First 1)
    if ($c.Count) { return $c[0].FullName }
    $p
}

function Get-GolemQueuePick {
    # cc-P153: the queue grid's picked row ({Label 'P153', Path, Mark, Preview}), or $null.
    $q = $script:ui.GolemQueueList
    if (-not $q -or -not $q.SelectedRows -or $q.SelectedRows.Count -lt 1) { return $null }
    $q.SelectedRows[0].Tag
}

function Update-GolemRunControls {
    # cc-P125: Wrap up and Find a stopping point say Cancel while set; the queue and finished lists from the view.
    param($View)
    $st = $View.Status
    foreach ($b in @($script:ui.GolemRunButtons)) {
        if ($b.Tag -eq 'golem.wrapup') { $b.Text = $(if ($st -and $st.Winding -eq 'after-current') { 'Cancel wrap up' } else { 'Wrap up after this task' }) }
        if ($b.Tag -eq 'golem.stoppoint') { $b.Text = $(if ($st -and $st.Winding -eq 'stopping-point') { 'Cancel stopping point' } else { 'Find a stopping point' }) }
    }
    # cc-P182: Retry lights only while a prompt is STOPPED, and names it.
    foreach ($b in @($script:ui.GolemQueueButtons)) {
        if ($b.Tag -eq 'golem.retry') { $sp = $(if ($st) { [string]$st.StoppedPrompt } else { '' }); $b.Enabled = [bool]$sp
            if ($script:ui.ToolTip) { $script:ui.ToolTip.SetToolTip($b, $(if ($sp) { '' } else { 'Greyed out: no prompt is STOPPED. ' }) + (Get-ConsoleTooltips)['golem.retry']) }; $b.Text = $(if ($sp) { 'Retry stopped prompt (' + $sp + ')' } else { 'Retry stopped prompt' }) }
        if ($b.Tag -eq 'golem.runnext') { $b.Text = $(if ($View.PSObject.Properties.Name -contains 'RunNext' -and $View.RunNext) { 'Run next (picked: ' + ([regex]::Match([string]$View.RunNext, '^cc-(P\d+)-').Groups[1].Value) + ')' } else { 'Run next (picked row)' }) }
    }
    # cc-P216: Answer lights while a Golem on this PC has an open question, and says how many.
    # cc-P356: only the buttons that exist: in PowerShell 5.1 @($null).Count is 1, so a window (or a test) without
    # them would set Enabled on nothing and throw.
    $answerButtons = @($script:ui.GolemAnswerButtons | Where-Object { $_ })
    if ($answerButtons.Count) {
        $nq = @(Get-OpenGolemQuestions -Root ([string]$script:cfg.Autopilot.GolemRoot) | Where-Object { -not $_.Remote }).Count
        foreach ($b in $answerButtons) { $b.Enabled = ($nq -gt 0); $b.Text = $(if ($nq) { 'Answer a Golem''s question (' + $nq + ')' } else { 'Answer a Golem''s question' }) }
    }
    # cc-P131: the toggles say what they would do next; Wake the Golem lights when the Golem is ready for a new session.
    $gs = $View.Settings
    foreach ($b in @($script:ui.GolemWakeButtons)) {
        if ($b.Tag -eq 'golem.autowake') { $b.Text = $(if ($gs -and -not $gs.AutoWake) { 'Self-wake: off (turn on)' } else { 'Self-wake: on (turn off)' }) }
        if ($b.Tag -eq 'golem.nightshift') { $b.Text = $(if ($gs -and -not $gs.NightShiftEnabled) { 'Night shift: off (turn on)' } else { 'Night shift: on (turn off)' }) }
        # cc-P200: the mode and whether marathon is on now; Set writes the pick beside it.
        if ($b.Tag -eq 'golem.marathon') { $mon = ($View.Status -and $View.Status.Marathon -and [bool]$View.Status.Marathon.on); $b.Text = 'Marathon: ' + $(if ($gs) { $gs.Marathon } else { 'auto' }) + $(if ($mon) { ', ON' } else { '' }) + ' (set)' }
        if ($b.Tag -eq 'golem.resume') {
            # cc-P153: the tooltip says why it is greyed (Enable-DisabledTooltips shows it on hover).
            $why = Get-GolemWakeBlock -View $View -Now ([datetime]::Now)
            $b.Enabled = (-not $why)
            if ($script:ui.ToolTip) { $script:ui.ToolTip.SetToolTip($b, $(if ($why) { $why.Substring(0, 1).ToUpper() + $why.Substring(1) + '. ' } else { '' }) + (Get-ConsoleTooltips)['golem.resume']) }
        }
    }
    if ($script:ui.GolemModel -and $script:ui.GolemModel.Items.Count) {
        # cc-P153: the Auto item says what it will use.
        $at = Format-AutoModelItem -QueueInfo @($View.QueueInfo) -Current $View.CurrentPrompt -RunNext $(if ($View.PSObject.Properties.Name -contains 'RunNext') { [string]$View.RunNext } else { '' })
        if ([string]$script:ui.GolemModel.Items[0] -ne $at) { $si = $script:ui.GolemModel.SelectedIndex; $script:ui.GolemModel.Items[0] = $at; $script:ui.GolemModel.SelectedIndex = $si }
    }
    if ($gs -and $script:ui.NightStart -and -not $script:ui.NightStart.Focused -and $gs.NightShiftStart -and $script:ui.NightStart.Text -eq '22:30') { $script:ui.NightStart.Text = $gs.NightShiftStart }
    if ($gs -and $script:ui.MarathonPick -and -not $script:ui.MarathonPick.Focused -and -not $script:ui.MarathonPicked -and [string]$script:ui.MarathonPick.SelectedItem -ne $gs.Marathon) { $script:ui.MarathonPick.SelectedItem = $gs.Marathon }
    if ($gs -and $script:ui.NightEnd -and -not $script:ui.NightEnd.Focused -and $gs.NightShiftEnd -and $script:ui.NightEnd.Text -eq '06:30') { $script:ui.NightEnd.Text = $gs.NightShiftEnd }
    $q = $script:ui.GolemQueueList
    if ($q) {
        # cc-P153: Get-QueueRows' rows; the picked row stays picked across refreshes.
        $sel = $(if (Get-GolemQueuePick) { (Get-GolemQueuePick).Label } else { '' })
        $rows = @(Get-QueueRows -QueueInfo @($View.QueueInfo) -Current $View.CurrentPrompt -RunNext $(if ($View.PSObject.Properties.Name -contains 'RunNext') { [string]$View.RunNext } else { '' }))
        $fore = $q.DefaultCellStyle.ForeColor; $back = $q.DefaultCellStyle.BackColor
        $grey = [Drawing.Color]::FromArgb([int](($fore.R * 3 + $back.R * 2) / 5), [int](($fore.G * 3 + $back.G * 2) / 5), [int](($fore.B * 3 + $back.B * 2) / 5))
        $q.SuspendLayout(); $q.Rows.Clear()
        foreach ($r in $rows) {
            $note = $(if ($r.Cont) { '(continuation) ' } else { '' }) + $r.Note
            $i = $q.Rows.Add($r.Label, $r.Mark, $note, $r.Model, $r.Size)
            $row = $q.Rows[$i]
            $row.Tag = [pscustomobject]@{ Label = $r.Label; Path = $(if ($r.Mark -eq 'running') { Find-HandedPromptPath $script:cfg.Autopilot.PromptDir $r.Name } else { (Join-Path $script:cfg.Autopilot.Queue $r.Name) }); Mark = $r.Mark
                Preview = ($r.Label + ': ' + $r.Title + "`r`n" + $(if ($r.Grey) { '(no Summary line; its first sentence)' } else { $r.Note }) + "`r`n`r`n" + $(if ($r.Para) { $r.Para } else { '(no first paragraph: ' + $r.Name + ')' })) }
            $tip = $r.Label + ' (' + $r.Model + $(if ($r.Size) { ', ' + $r.Size } else { '' }) + ')' + $(if ($r.Mark) { ', ' + $r.Mark } else { '' }) + ': ' + $r.Note
            foreach ($cell in $row.Cells) { $cell.ToolTipText = $tip }
            if ($r.Grey) { $row.Cells['note'].Style.ForeColor = $grey }
            if ($r.Mark -eq 'running') { $row.DefaultCellStyle.Font = New-Object Drawing.Font($q.Font, [Drawing.FontStyle]::Bold) }
        }
        $q.ClearSelection()
        foreach ($row in $q.Rows) { if ($row.Tag -and $row.Tag.Label -eq $sel) { $row.Selected = $true } }
        $q.ResumeLayout()
    }
    $f = $script:ui.GolemFinishedList
    if ($f) {
        $f.BeginUpdate(); $f.Items.Clear()
        foreach ($x in @($View.Finished)) {
            $it = New-Object Windows.Forms.ListViewItem(('P' + $x.Number))
            [void]$it.SubItems.Add($x.Result)
            [void]$it.SubItems.Add($x.Time.ToString('MM-dd HH:mm', $script:Invariant))
            [void]$it.SubItems.Add($x.Title)
            [void]$it.SubItems.Add($x.Headline)
            # cc-P154: from the notes folder's listing, read again only when the folder changed (Find-FileCached).
            $np = Find-FileCached $script:cfg.NotesFolder ('cc-P' + $x.Number + '-*.md')
            $it.Tag = $(if ($np) { $np } else { '' })
            [void]$f.Items.Add($it)
        }
        $f.EndUpdate()
    }
}

# --- the status file both consoles read (cc-P155) ------------------------------------------------------------------
# One collector, one file. Get-ConsoleRefreshData (cc-P154) reads everything; ConvertTo-ConsoleStatusDocument turns one
# refresh into plain data (schema 1) and Write-ConsoleStatusFile puts it in StatusFile atomically. Two writers, one
# logic: the console's refresh runspace after each refresh, and scripts\Write-ConsoleStatus.ps1 (the web console's
# collector loop), which skips a turn while the console has written the file recently. The web console
# (web-console\server.py) reads only this file for its tiles, lights and Golem; this console paints its tiles from it at
# its start (Show-StatusFileTiles), before its own first refresh is back. Nothing secret goes in it: tiles, lights, the
# Golem's state and queue, play-test counts and the link checks.

function Get-StatusTileKey {
    # 'STATUS PUBLISHER' -> 'publisher', 'PUBLIC DNS' -> 'dns', 'PLAY TESTS' -> 'playtests'; otherwise lower case.
    param([string]$Title)
    switch ($Title) { 'STATUS PUBLISHER' { 'publisher' } 'PUBLIC DNS' { 'dns' } 'PLAY TESTS' { 'playtests' } default { ($Title.ToLower() -replace '[^a-z0-9]', '') } }
}

function Get-PromptSize {
    # The size word at the end of a prompt's Kind line ("... Small to medium."), or ''.
    param([string]$Kind)
    $m = [regex]::Matches([string]$Kind, '(?i)\b(small to medium|medium to large|small|medium|large)\b')
    if ($m.Count) { $m[$m.Count - 1].Value.ToLower() } else { '' }
}

function ConvertTo-ConsoleStatusDocument {
    # One refresh as the status file's schema 1 (an ordered hashtable, ready for ConvertTo-Json). $Data is
    # Get-ConsoleRefreshData's; $NetResult the DNS tile's probe; $WebResults the websites probe (by site key).
    param($Data, $Config, $NetResult, $WebResults, [datetime]$Now = [datetime]::Now, [string]$By = 'console')
    $iso = { param($t) if ($t -is [datetime]) { $t.ToString('s') } else { $null } }
    $tile = { param($g) if (-not $g) { return $null }; [ordered]@{ key = (Get-StatusTileKey ([string]$g.Title)); title = [string]$g.Title; word = [string]$g.Word; detail = [string]$g.Detail; color = [string]$g.Color } }
    $prompt = { param($p) if (-not $p) { return $null }; [ordered]@{ number = [int]$p.Number; name = [string]$p.Name; title = [string]$p.Title; summary = [string]$p.Summary; model = [string]$p.Model; kind = [string]$p.Kind; size = (Get-PromptSize $p.Kind); cont = [bool]$p.Cont; para = [string]$p.Para } }
    $tiles = New-Object 'System.Collections.Generic.List[object]'
    if ($Data.State) { foreach ($g in @(Get-AtAGlance $Data.State $Config)) { $tiles.Add((& $tile $g)) } }
    $tiles.Add((& $tile (Get-DnsGlance $NetResult)))
    $v = $null
    if ($Data.Autopilot) { $tiles.Add((& $tile $Data.Autopilot.Tile)); $v = $Data.Autopilot.View }
    if ($Data.PlayTest) { $tiles.Add((& $tile $Data.PlayTest)) }
    if ($Config.Websites) { $tiles.Add((& $tile (Get-WebsitesGlance $Config.Websites $WebResults))) }
    $lights = Get-LightsFromFacts -RebuildFacts $Data.RebuildFacts -PackageFacts $Data.PackageFacts
    $light = { param($l) if (-not $l) { return $null }; [ordered]@{ color = [string]$l.Color; word = [string]$l.Word; detail = [string]$l.Detail; commits = @(@($l.Commits) | ForEach-Object { [ordered]@{ short = [string]$_.Short; prompt = [string]$_.Prompt; subject = [string]$_.Subject } }) } }
    $golem = $null
    if ($v) {
        $s = $v.Status
        $state = $(if ($s -and $s.State) { [string]$s.State } elseif ($v.Enabled -eq $false) { 'OFF' } else { 'UNKNOWN' })
        $cur = $null
        if ($v.CurrentPrompt) {
            $cur = & $prompt $v.CurrentPrompt
            if ($s) { $cur.startedAt = (& $iso $s.StartedAt); $cur.stepsDone = $s.StepsDone; $cur.stepsTotal = $s.StepsTotal; $cur.stepNow = $s.StepNow; $cur.lastActivity = (& $iso $s.LastActivity)
                # cc-P153: the step line, its percent and the estimate, for the web console (P155).
                $spx = Format-StepProgress $s; $cur.stepText = $spx.Text; $cur.stepPct = $spx.Pct; $cur.stepsSource = $(if ($s.PSObject.Properties.Name -contains 'StepsSource') { [string]$s.StepsSource } else { '' }); $cur.estimate = $spx.Estimate }
        }
        # cc-P160: the bars (with the pace marker, a percent) in usage; the plan lines (pace warning, last read, resets and
        # credits) in usageNotes.
        $ubs = @(Get-UsageBars -UsageText $v.UsageText -Now $Now -Estimate $v.Estimate -PlanText $v.PlanUsageText -Settings $v.Settings)
        $usage = @($ubs | Where-Object { $_.Bar } | ForEach-Object { [ordered]@{ name = [string]$_.Name; pct = $_.Pct; text = ([string]$_.Text).Trim(); color = [string]$_.Color; marker = $_.Marker } })
        $usageNotes = @($ubs | Where-Object { -not $_.Bar -and $_.Name -ne 'head' } | ForEach-Object { [ordered]@{ name = [string]$_.Name; text = ([string]$_.Text).Trim(); color = [string]$_.Color } })
        # cc-P152: the ChatGPT plan (Codex) rows, in the same shape.
        $codexUsage = @(Get-CodexUsageRows -CodexText $(if ($v.PSObject.Properties.Name -contains 'CodexUsageText') { $v.CodexUsageText } else { $null }) -Now $Now | ForEach-Object { [ordered]@{ name = [string]$_.Name; pct = $_.Pct; text = ([string]$_.Text).Trim(); color = [string]$_.Color; bar = [bool]$_.Bar } })
        $golem = [ordered]@{
            kind = [string]$v.Kind; state = $state; enabled = $v.Enabled
            reason = [string]$(if ($s) { @(@($s.RestReason, $s.NeedsYou, $s.Note) | Where-Object { $_ }) | Select-Object -First 1 } else { $v.StatusNote })
            needsYou = $(if ($s) { [string]$s.NeedsYou } else { '' })
            readyAt = $(if ($s) { & $iso $s.ReadyAt } else { $null }); readyHow = $(if ($s) { [string]$s.ReadyHow } else { '' })
            stoppedPrompt = $(if ($s) { [string]$s.StoppedPrompt } else { '' }); winding = $(if ($s) { [string]$s.Winding } else { '' })
            current = $cur
            queue = @(@($v.QueueInfo) | ForEach-Object { & $prompt $_ }); runNext = $(if ($v.PSObject.Properties.Name -contains 'RunNext') { [string]$v.RunNext } else { '' })
            # cc-P153: the Golem tab's list, in run order with its notes (running on top, next marked), for P143 and P148.
            queueRows = @(Get-QueueRows -QueueInfo @($v.QueueInfo) -Current $v.CurrentPrompt -RunNext $(if ($v.PSObject.Properties.Name -contains 'RunNext') { [string]$v.RunNext } else { '' }) | ForEach-Object { [ordered]@{ number = $_.Number; label = $_.Label; mark = $_.Mark; note = $_.Note; grey = $_.Grey; title = $_.Title; model = $_.Model; size = $_.Size; name = $_.Name; cont = $_.Cont } })
            finished = @(@($v.Finished) | ForEach-Object { [ordered]@{ number = [int]$_.Number; result = [string]$_.Result; time = (& $iso $_.Time); title = [string]$_.Title; headline = [string]$_.Headline } })
            autoWake = [bool]$v.Settings.AutoWake
            nightShift = [ordered]@{ enabled = [bool]$v.Settings.NightShiftEnabled; start = [string]$v.Settings.NightShiftStart; end = [string]$v.Settings.NightShiftEnd }
            # cc-P200: the week's projection and marathon mode, for the web console's Golem page.
            projection = $(if ($s -and $s.Projection) { [ordered]@{ known = [bool]$s.Projection.known; text = [string]$s.Projection.text; resetAdvice = [string]$s.Projection.resetAdvice; target = $s.Projection.target
                fableWarning = $(if ($s.Projection.PSObject.Properties.Name -contains 'fableWarning') { [string]$s.Projection.fableWarning } else { '' })   # cc-P274
                projected = $(if ($s.Projection.week) { $s.Projection.week.projected } else { $null }); resetsAt = $(if ($s.Projection.week) { [string]$s.Projection.week.resetsAt } else { $null }) } } else { $null })
            marathon = [ordered]@{ mode = [string]$v.Settings.Marathon; on = $(if ($s -and $s.Marathon) { [bool]$s.Marathon.on } else { $false }); since = $(if ($s -and $s.Marathon) { [string]$s.Marathon.since } else { '' })
                why = $(if ($s -and $s.Marathon) { [string]$s.Marathon.why } else { '' }) }
            usage = $usage
            usageNotes = $usageNotes
            codexUsage = $codexUsage
            alert = [string]$v.Alert; emailFailing = [bool]$v.EmailFailing
            # cc-P184: the Golem cards (status.json golems), for the web console's Golems card.
            multiGolem = $(if ($s) { [bool]$s.MultiGolem } else { $false })
            golems = @($(if ($s) { @($s.Golems) } else { @() }) | ForEach-Object { ConvertTo-GolemCardDocument $_ })
            # cc-P149: away mode, the parked prompts and the last email commands, for the web console's Golem page.
            away = $(if ($s -and $s.Away) { $s.Away } else { $null }); mailCommands = $(if ($s -and $s.MailCommands) { $s.MailCommands } else { $null })
        }
    }
    $web = [ordered]@{}
    foreach ($site in @($Config.Websites.Sites)) {
        $r = $(if ($WebResults) { $WebResults[[string]$site.Key] } else { $null })
        $web[[string]$site.Key] = [ordered]@{ name = [string]$site.Name; url = [string]$site.Url; code = $(if ($r) { [int]$r.Code } else { $null }); error = $(if ($r) { [string]$r.Error } else { '' }); timedOut = $(if ($r) { [bool]$r.TimedOut } else { $false }) }
    }
    [ordered]@{
        schema = 1; generatedAt = $Now.ToString('s'); generatedBy = $By; host = $env:COMPUTERNAME
        tiles = $tiles.ToArray()
        lights = [ordered]@{ rebuild = (& $light $lights.Rebuild); package = (& $light $lights.Package) }
        golem = $golem
        websites = $web
        timings = $Data.Timings
        errors = @($Data.Errors)
    }
}

function Write-ConsoleStatusFile {
    # The document as JSON in $Config.StatusFile, written to a temporary file beside it and moved over it, so a reader
    # never sees half a file. Returns the path.
    param($Data, $Config, $NetResult, $WebResults, [string]$By = 'console', [string]$Path)
    if (-not $Path) { $Path = $Config.StatusFile }
    if (-not $Path) { return $null }
    $doc = ConvertTo-ConsoleStatusDocument -Data $Data -Config $Config -NetResult $NetResult -WebResults $WebResults -Now ([datetime]::Now) -By $By
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $tmp = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($tmp, ($doc | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding $false))
        # [NullString]::Value: a plain $null reaches .NET as '' here, which Replace refuses ("not of a legal form").
        if (Test-Path -LiteralPath $Path) { [IO.File]::Replace($tmp, $Path, [NullString]::Value) } else { [IO.File]::Move($tmp, $Path) }
    } finally { if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue } }
    $Path
}

function Read-ConsoleStatusFile {
    # The status file as objects, with AgeSeconds; $null when there is none or it cannot be read.
    param([string]$Path)
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try {
        $j = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
        if ($j.schema -ne 1) { return $null }
        $at = [datetime]::Parse([string]$j.generatedAt, [Globalization.CultureInfo]::InvariantCulture)
        $j | Add-Member -NotePropertyName AgeSeconds -NotePropertyValue ([int]([datetime]::Now - $at).TotalSeconds) -Force
        $j
    } catch { $null }
}

function Show-StatusFileTiles {
    # cc-P155: at the start, the tiles as the status file last said, marked with its age, so the window shows something
    # real before its own first refresh is back (a file older than $MaxAgeMinutes is left alone).
    param([string]$Path, [int]$MaxAgeMinutes = 10)
    $doc = Read-ConsoleStatusFile $Path
    if (-not $doc -or $doc.AgeSeconds -gt $MaxAgeMinutes * 60 -or -not $script:ui) { return $false }
    $byKey = @{}
    foreach ($t in @($doc.tiles)) { if ($t) { $byKey[[string]$t.key] = $t } }
    $age = Format-ShortAge ([TimeSpan]::FromSeconds([math]::Max(0, $doc.AgeSeconds)))
    $one = { param($ui, $t) if ($ui -and $t) { Set-TileLook $ui ([pscustomobject]@{ Title = $t.title; Word = $t.word; Detail = ($t.detail + "`n(as of " + $age + ' ago)'); Color = $t.color }) } }
    $uiTiles = @($script:ui.Tiles)
    $order = @('live', 'test', 'publisher')
    for ($i = 0; $i -lt $order.Count -and $i -lt $uiTiles.Count; $i++) { & $one $uiTiles[$i] $byKey[$order[$i]] }
    & $one $script:ui.AutopilotTile $byKey['golem']
    & $one $script:ui.PlayTestTile $byKey['playtests']
    $true
}

# --- the refresh, off the window's thread (cc-P154) -----------------------------------------------------------------
# Chase, 2026-10-17 13:10: "right now console is a little unresponsive". Measured on this PC: a status refresh held the
# window for about 3.4 s (docker 1.0, the package light 0.9, the rebuild light 0.8, play-tests.json 0.4, the Golem up to
# 0.25), the VPN facts 0.25 to 1.2 s every 30 s, and on the Commit tab git 0.7 s every 5 s. Under -BackgroundRefresh (the
# console's own start) all of it is read in a runspace of its own and the window only draws what comes back; a click's
# reads before its plan likewise (Start-ActionPrecheck). The facts' forms keep the old order: read and draw at once.

$script:ConsoleScriptFile = $PSCommandPath
$script:BackgroundRefresh = $false
$script:rsJobs = @{}
$script:rsPool = @{}
$script:RefreshSkipped = 0
# The facts' stub source: a script for the refresh runspace in place of Get-ConsoleRefreshData (same five arguments).
$script:RefreshScript = $null
# The package light's facts (git and the download site) are read on the Player package tab, and at least this often so
# the lights file the status page reads stays current.
$script:PackageFactsMaxAge = 120

function Get-ConsoleRefreshData {
    # cc-P154: everything one refresh reads, with no window, timed per source (Timings, ms). $Parts: 'tiles' (docker, the
    # question mail, the Golem, the rebuild light, play-tests.json), 'golem' (the Golem alone), 'package' (the package
    # light's git and the download site), 'commit' (the Commit tab's git), 'vpn' (this PC's adapters, route, services).
    # Never throws: a source that fails is named in Errors and the others still come back.
    param($Config, [bool]$FromCapture, [string]$StatusFrom, [string[]]$Parts = @('tiles'), [bool]$Dry)
    $times = [ordered]@{}
    $errs = New-Object 'System.Collections.Generic.List[string]'
    $r = [ordered]@{ Parts = $Parts; At = [datetime]::UtcNow; State = $null; Autopilot = $null; AutopilotConfig = $null; RebuildFacts = $null; PackageFacts = $null; PlayTest = $null; VpnFacts = $null; CommitFacts = $null; Errors = $null; Timings = $times }
    $sw = New-Object Diagnostics.Stopwatch
    $all = [Diagnostics.Stopwatch]::StartNew()
    $run = {
        param([string]$Name, [scriptblock]$Read)
        $sw.Restart()
        try { & $Read } catch { $errs.Add($Name + ': ' + $_.Exception.Message) }
        $times[$Name] = $sw.ElapsedMilliseconds
    }
    $tiles = (@($Parts) -contains 'tiles')
    if ($tiles) {
        & $run 'docker' { $r.State = Get-ConsoleState -Config $Config -DryRun:$FromCapture -StatusFrom $StatusFrom }
        if (-not $Dry -and $r.State) { & $run 'question mail' { Invoke-QuestionMail -Config $Config -State $r.State } }
    }
    if ($tiles -or @($Parts) -contains 'golem') {
        & $run 'golem' {
            # cc-P99: which folder is active is re-read too. On a copy: the window's config changes only when it draws.
            $c = $Config.PSObject.Copy()
            $g = $Config.Autopilot
            if ($g -and $g.GolemRoot -and $g.AutopilotRoot) { $c.Autopilot = Get-GolemConfig $g.GolemRoot $g.AutopilotRoot $g.Queue }
            $r.AutopilotConfig = $c.Autopilot
            $r.Autopilot = Get-AutopilotViewNow $c
            # The Golem tab's lines too (pure, but 70 ms of the window's time when drawn there).
            $r.Autopilot | Add-Member -NotePropertyName PanelLines -NotePropertyValue @(Format-AutopilotPanel -View $r.Autopilot.View -Config $c -Now ([datetime]::Now)) -Force
        }
    }
    if ($tiles) {
        if ($r.State) { & $run 'rebuild light' { $r.RebuildFacts = Get-RebuildFacts $Config $r.State } }
        & $run 'play tests' { $r.PlayTest = Get-PlayTestGlanceNow $Config }
    }
    if (@($Parts) -contains 'package' -and -not $FromCapture) { & $run 'package light' { $r.PackageFacts = Get-PackageLightFacts $Config } }
    if (@($Parts) -contains 'commit') { & $run 'commit' { $r.CommitFacts = Get-CommitFacts $Config } }
    if (@($Parts) -contains 'vpn') { & $run 'vpn' { $r.VpnFacts = Get-VpnFacts $Config } }
    $times['total'] = $all.ElapsedMilliseconds
    $r.Errors = $errs.ToArray()
    [pscustomobject]$r
}

function Show-ConsoleRefresh {
    # cc-P154: draws one refresh (Get-ConsoleRefreshData) on the window's thread. Only drawing: nothing here reads docker,
    # git, a process or the network. A refresh of some parts only (the Commit tab's, the VPN facts) draws only those.
    param($Data)
    if (-not $Data) { return }
    foreach ($e in @($Data.Errors)) { Write-ConsoleLine ('Status refresh failed: ' + $e) 'red' }
    if ($Data.VpnFacts) { $script:vpnFacts = $Data.VpnFacts; $script:vpnFactsAt = $Data.At }
    if ($Data.CommitFacts) { $script:commitFactsCache = $Data.CommitFacts; $script:commitFactsAt = $Data.At }
    if ($Data.PackageFacts) { $script:packageFactsAt = $Data.At }
    $script:tileFitHold = $true
    $script:tileFitWanted = $false
    try {
        if ($Data.AutopilotConfig) { $script:cfg.Autopilot = $Data.AutopilotConfig }
        $state = $Data.State
        if ($state) {
            $script:lastState = $state
            Update-GlanceTiles $state
            # cc-P106: the Shard question tab (the email went from the read, Get-ConsoleRefreshData).
            Update-QuestionPanel $state
        }
        $ap = $script:autopilot
        if ($Data.Autopilot) { $ap = Show-AutopilotView $Data.Autopilot }
        # cc-P132: the rebuild and package lights, after the tiles and the Golem.
        if ($state) { Update-Lights $state -RebuildFacts $Data.RebuildFacts -PackageFacts $Data.PackageFacts -Gathered }
        $pt = $script:playTestGlance
        if ($Data.PlayTest) {
            $pt = $Data.PlayTest
            $script:playTestGlance = $pt
            if ($script:ui.PlayTestTile) { Set-TileLook $script:ui.PlayTestTile $pt }
        }
        if ($state) {
            $box = $script:ui.Status
            Set-RichLines $box @(Format-StatusReport $state $script:cfg @($(if ($ap) { $ap.Tile }), $pt))
            $box.SelectionStart = 0
            $box.ScrollToCaret()
            Update-SnapshotList
        }
    } catch {
        Write-ConsoleLine ('Status refresh failed: ' + $_.Exception.Message) 'red'
    } finally {
        $script:tileFitHold = $false
        if ($script:tileFitWanted) { $script:tileFitWanted = $false; Update-TileFit }
    }
    if ($Data.CommitFacts) { Update-CommitCountdown }
    if ($Data.VpnFacts -and $script:ui.VpnButtons) { Update-VpnButtons }
}

function Get-RefreshParts {
    # cc-P154: what a 30 s refresh reads: the tiles always, and the open tab's detail besides (the package light's facts
    # on the Player package tab, the Commit tab's on the Commit tab). The package facts also when older than
    # PackageFactsMaxAge, so the lights file stays current with the tab closed.
    $p = @('tiles', 'vpn')
    $sel = $null
    if ($script:ui -and $script:ui.Tabs) { $sel = $script:ui.Tabs.SelectedTab }
    $old = (-not $script:packageFactsAt -or ([datetime]::UtcNow - $script:packageFactsAt).TotalSeconds -ge $script:PackageFactsMaxAge)
    if (($sel -and $sel -eq $script:ui.PackagePage) -or $old) { $p += 'package' }
    if ($sel -and $sel -eq $script:ui.CommitPage) { $p += 'commit' }
    $p
}

function Start-ConsoleJob {
    # cc-P154: runs $Script in the runspace kept for $Slot ('refresh', 'precheck'), loading the console's functions there
    # the first time (about 0.2 s, once), and returns at once. One job per slot: $null while that slot is busy.
    # Receive-ConsoleJobs hands the result to $Then on the window's thread, as & $Then <result> <job>.
    param([string]$Slot, [string]$Script, [object[]]$Arguments, [scriptblock]$Then)
    if ($script:rsJobs[$Slot]) { return $null }
    $rs = $script:rsPool[$Slot]
    if (-not $rs -or $rs.RunspaceStateInfo.State -ne 'Opened') {
        $rs = [runspacefactory]::CreateRunspace()
        $rs.Open()
        $script:rsPool[$Slot] = $rs
    }
    $ps = [PowerShell]::Create()
    $ps.Runspace = $rs
    $q = [string][char]39
    $boot = 'if (-not (Test-Path function:Get-ConsoleRefreshData)) { . ' + $q + ($script:ConsoleScriptFile -replace $q, ($q + $q)) + $q + ' -LoadOnly }'
    [void]$ps.AddScript($boot).AddStatement().AddScript($Script)
    foreach ($a in $Arguments) { [void]$ps.AddArgument($a) }
    $job = [pscustomobject]@{ Slot = $Slot; PS = $ps; Handle = $null; Started = [datetime]::UtcNow; Then = $Then; Parts = @(); Bound = $null; Action = '' }
    $job.Handle = $ps.BeginInvoke()
    $script:rsJobs[$Slot] = $job
    $job
}

function Receive-ConsoleJobs {
    # cc-P154: on the window's 250 ms timer. Collects each finished job (never waits for one) and hands its result to its
    # Then; a job running past 120 s is stopped and its runspace dropped, so the next one starts clean.
    foreach ($slot in @($script:rsJobs.Keys)) {
        $j = $script:rsJobs[$slot]
        if (-not $j) { continue }
        if ($j.Handle.IsCompleted) {
            $script:rsJobs.Remove($slot)
            $data = $null
            $err = ''
            try {
                $out = @($j.PS.EndInvoke($j.Handle))
                if ($out.Count) { $data = $out[-1] }
                if (-not $data -and $j.PS.Streams.Error.Count) { $err = [string]$j.PS.Streams.Error[0] }
            } catch { $err = $_.Exception.Message }
            $j.PS.Dispose()
            if ($err) { Write-ConsoleLine ('The background read (' + $slot + ') failed: ' + $err) 'red' }
            try { & $j.Then $data $j } catch { Write-ConsoleLine ('Drawing the ' + $slot + ' read failed: ' + $_.Exception.Message) 'red' }
        } elseif (([datetime]::UtcNow - $j.Started).TotalSeconds -gt 120) {
            $script:rsJobs.Remove($slot)
            Stop-ConsoleJob $j
            try { $script:rsPool[$slot].Dispose() } catch { }
            $script:rsPool[$slot] = $null
            Write-ConsoleLine ('The ' + $slot + ' read took longer than 2 minutes and was abandoned; the next one starts afresh.') 'amber'
        }
    }
}

function Stop-ConsoleJob {
    # cc-P186: one job stopped, then its async result waited for before Dispose. Disposing a pipeline whose worker was
    # still writing (often while the boot dot-sources the console) crashed the whole process (Windows Error Reporting:
    # PSDataCollection InternalInsertItem, and InvalidRunspaceState ConcurrentSessionStateProxyCallInProgress).
    param($Job)
    try { $Job.PS.Stop() } catch { }
    try { [void]$Job.PS.EndInvoke($Job.Handle) } catch { }
    try { $Job.PS.Dispose() } catch { }
}

function Stop-ConsoleJobs {
    # cc-P154: with the window: every job stopped and every runspace closed, so nothing reads on unseen.
    foreach ($slot in @($script:rsJobs.Keys)) { $j = $script:rsJobs[$slot]; if ($j) { Stop-ConsoleJob $j } }
    $script:rsJobs = @{}
    foreach ($slot in @($script:rsPool.Keys)) { try { $script:rsPool[$slot].Dispose() } catch { } }
    $script:rsPool = @{}
    $script:refreshQueued = $null
}

function Start-ConsoleRefresh {
    # cc-P154: one refresh in the background; the window draws it when it is back. Never two at once: while one runs, a
    # tick is skipped (RefreshSkipped). -Queue (a click, a tab opened), or a tick that finds only a part-refresh running,
    # asks for one more right after it.
    param([string[]]$Parts = @('tiles', 'vpn'), [switch]$Queue)
    $busy = $script:rsJobs['refresh']
    if ($busy) {
        $script:RefreshSkipped++
        if ($Queue -or (($Parts -contains 'tiles') -and -not (@($busy.Parts) -contains 'tiles'))) {
            $script:refreshQueued = @(@($script:refreshQueued) + $Parts | Where-Object { $_ } | Select-Object -Unique)
        }
        return $false
    }
    $code = $script:RefreshScript
    # cc-P155: a refresh of the tiles also writes the status file (Write-ConsoleStatusFile), there, not on the window's
    # thread; never from a capture (a dry run's state is not this PC's).
    if (-not $code) { $code = 'param($cfg, $fromCapture, $statusFrom, $parts, $dry, $net, $web) $d = Get-ConsoleRefreshData -Config $cfg -FromCapture $fromCapture -StatusFrom $statusFrom -Parts $parts -Dry $dry; if ((@($parts) -contains ''tiles'') -and -not $fromCapture) { try { [void](Write-ConsoleStatusFile -Data $d -Config $cfg -NetResult $net -WebResults $web -By ''console'') } catch { } }; $d' }
    $then = {
        param($Data, $Job)
        $sw = [Diagnostics.Stopwatch]::StartNew()
        Show-ConsoleRefresh $Data
        $script:LastRefresh = [pscustomobject]@{ Data = $Data; Parts = $Job.Parts; ReadMs = [int]([datetime]::UtcNow - $Job.Started).TotalMilliseconds; DrawMs = $sw.ElapsedMilliseconds }
        if ($script:refreshQueued) { $q = [string[]]@($script:refreshQueued); $script:refreshQueued = $null; [void](Start-ConsoleRefresh -Parts $q) }
    }
    $j = Start-ConsoleJob -Slot 'refresh' -Script $code -Arguments @($script:cfg, [bool]$script:fromCapture, [string]$script:statusFrom, ([string[]]$Parts), [bool]$script:dry, $script:netResult, $script:webResults) -Then $then
    if (-not $j) { return $false }
    $j.Parts = @($Parts)
    $true
}

function Get-ActionFacts {
    # cc-P154: what a button reads before its plan (docker, git, dist\, the Golem's folder, this PC's adapters), with no
    # window, so a click reads in the background (Start-ActionPrecheck) and carries on when it is back. As before cc-P154,
    # the same reads for the same keys (Invoke-ConsoleButton read them inline).
    param($Config, [string]$ActionKey, [string]$PackageZip, [bool]$FromCapture, [string]$StatusFrom, [bool]$Dry)
    $f = [ordered]@{ State = $null; Facts = $null; PackageFacts = $null; AutopilotFacts = $null; VpnFacts = $null; FixFacts = $null; WipeFacts = $null; CliCheck = $null }
    if ($ActionKey -like 'links.*' -or $ActionKey -like 'web.*' -or $ActionKey -like 'art.*' -or $ActionKey -like 'map.*') {
        # cc-P83: a link reads nothing. cc-P101: nor does a Websites button (websites.json is in the config). cc-P135: nor a Map and Art one.
    } elseif ($ActionKey -like 'vpn.*') {
        # cc-P86: this PC's adapters, route and services, read now.
        $f.VpnFacts = Get-VpnFacts $Config
    } elseif ($ActionKey -like 'golem.*') {
        # cc-P74: the Golem's folder, never docker. cc-P131: a fresh sign-in check for the wake (not in a dry run).
        $f.AutopilotFacts = Get-AutopilotFacts $Config
        if ($ActionKey -eq 'golem.resume' -and -not $Dry) { $f.CliCheck = Get-ClaudeCliFacts -Check }
    } elseif ($ActionKey -like 'commit.*') {
        # The commit buttons read git and the notes, never docker (cc-P60).
        $f.Facts = Get-CommitFacts $Config
    } elseif ($ActionKey -like 'package.scripts.*') {
        # cc-P144: the Legion scripts row reads nothing here; its scripts read the clone and Haven themselves.
    } elseif ($ActionKey -like 'package.*') {
        # So do the package buttons, and dist\ (cc-P63).
        $f.PackageFacts = Get-PackageFacts $Config $PackageZip
    } else {
        $f.State = Get-ConsoleState -Config $Config -DryRun:$FromCapture -StatusFrom $StatusFrom
        # cc-P96: git and the test world's save record. cc-P73: the wipe's facts.
        if ($ActionKey -like 'fix.*') { $f.FixFacts = Get-FixFacts $Config $f.State -NoDocker:$FromCapture }
        if ($ActionKey -eq 'test.wipe') { $f.WipeFacts = Get-WipeFacts $Config $f.State }
    }
    [pscustomobject]$f
}

function Start-ActionPrecheck {
    # cc-P154: a click's reads in the background; the click carries on (Invoke-ConsoleButton -Prefetched) on the window's
    # thread when they are back, dialogs and all. One at a time: a second click meanwhile is told to wait, never queued.
    param([hashtable]$Bound, $UseConfig)
    $key = [string]$Bound['ActionKey']
    # cc-P187: a Golem button for the Golem picked on the Golem tab reads that Golem's files.
    $readCfg = $(if ($UseConfig) { $UseConfig } else { $script:cfg })
    $busy = $script:rsJobs['precheck']
    if ($busy) { Write-ConsoleLine ('Still reading for ' + $busy.Action + '; click ' + $key + ' again when that is done.') 'amber'; return $false }
    $code = 'param($cfg, $key, $zip, $fromCapture, $statusFrom, $dry) Get-ActionFacts -Config $cfg -ActionKey $key -PackageZip $zip -FromCapture $fromCapture -StatusFrom $statusFrom -Dry $dry'
    $then = {
        param($Data, $Job)
        if (-not $Data) { Write-ConsoleLine ($Job.Action + ': the reads before its plan failed (above). Nothing was run.') 'red'; return }
        $a = $Job.Bound
        Invoke-ConsoleButton @a -Prefetched $Data
    }
    $j = Start-ConsoleJob -Slot 'precheck' -Script $code -Arguments @($readCfg, $key, [string]$Bound['PackageZip'], [bool]$script:fromCapture, [string]$script:statusFrom, [bool]$script:dry) -Then $then
    if (-not $j) { return $false }
    $j.Bound = $Bound
    $j.Action = $key
    Write-ConsoleLine ($key + ': reading what its plan needs...') 'gray'
    $true
}

function Update-ConsoleStatus {
    # cc-P154: under -BackgroundRefresh this starts a refresh (Start-ConsoleRefresh) and returns at once; -Queue asks for
    # one after a refresh still running. The facts' forms read and draw here, as before.
    param([switch]$Queue)
    if ($script:BackgroundRefresh) { [void](Start-ConsoleRefresh -Parts (Get-RefreshParts) -Queue:$Queue); return }
    try {
        Show-ConsoleRefresh (Get-ConsoleRefreshData -Config $script:cfg -FromCapture ([bool]$script:fromCapture) -StatusFrom $script:statusFrom -Parts @('tiles', 'package') -Dry ([bool]$script:dry))
    } catch {
        Write-ConsoleLine ('Status refresh failed: ' + $_.Exception.Message) 'red'
    }
}

function Get-NetworkProbe {
    # cc-P52 Part H: the signals Get-DnsGlance judges. Reads only: the route table, the adapter list,
    # and two HTTPS GETs with a short timeout. Never throws: each failure is recorded beside its
    # signal. The scriptblocks are for the facts, which hand in recorded or failing answers.
    param(
        [string]$HostName = 'shatteredlegacyuo.com',
        [string]$LanIp = '192.168.1.58',
        [int]$TimeoutSec = 4,
        [scriptblock]$Http = { param($Url, $Timeout) (Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec $Timeout -Headers @{ accept = 'application/dns-json' } -ErrorAction Stop).Content },
        [scriptblock]$Route = { Find-NetRoute -RemoteIPAddress '1.1.1.1' -ErrorAction Stop },
        [scriptblock]$Adapters = {
            [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | ForEach-Object {
                [pscustomobject]@{ Name = $_.Name; Description = $_.Description; Type = [string]$_.NetworkInterfaceType; Up = ($_.OperationalStatus -eq 'Up') }
            }
        }
    )
    # Windows PowerShell 5.1 may not offer TLS 1.2 by default; both DNS services require it.
    try { [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12 } catch { }
    $r = [ordered]@{
        At = [datetime]::UtcNow; RouteInterface = $null; RouteSource = $null; RouteIsLan = $null; RouteError = $null
        VpnAdapters = @(); DnsIps = @(); DnsError = $null; PublicIp = $null; PublicIpError = $null
    }
    try {
        $found = @(& $Route)
        $src = $found | Where-Object { $_.IPAddress } | Select-Object -First 1
        $rt = $found | Where-Object { $_.DestinationPrefix } | Select-Object -First 1
        if (-not $src) { throw 'no route to 1.1.1.1' }
        $r.RouteSource = [string]$src.IPAddress
        $r.RouteInterface = [string]$src.InterfaceAlias
        if ($rt -and $rt.InterfaceAlias) { $r.RouteInterface = [string]$rt.InterfaceAlias }
        $r.RouteIsLan = ($r.RouteSource -eq $LanIp)
    } catch { $r.RouteError = $_.Exception.Message }
    try {
        $r.VpnAdapters = @(@(& $Adapters) | Where-Object { Test-VpnAdapter $_ } | ForEach-Object { [string]$_.Name })
    } catch { $r.VpnAdapters = @() }
    $errs = @()
    foreach ($url in @("https://cloudflare-dns.com/dns-query?name=$HostName&type=A", "https://dns.google/resolve?name=$HostName&type=A")) {
        try { $r.DnsIps = @(ConvertFrom-DohJson ([string](& $Http $url $TimeoutSec))); $r.DnsError = $null; break }
        catch { $errs += ((([uri]$url).Host) + ': ' + $_.Exception.Message); $r.DnsError = $errs -join '; ' }
    }
    try { $r.PublicIp = ConvertFrom-TraceText ([string](& $Http 'https://cloudflare.com/cdn-cgi/trace' $TimeoutSec)) }
    catch { $r.PublicIpError = $_.Exception.Message }
    [pscustomobject]$r
}

function Get-VpnFacts {
    # cc-P86: what the Disconnect VPN plan needs, read now. Read only: the route, the adapters, the services and who
    # this console runs as. Never throws.
    param($Config)
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $f = [ordered]@{
        Identity = $id.Name; IsSystem = [bool]$id.IsSystem
        IsAdmin = ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        RouteIsLan = $null; VpnAdapters = @(); WireGuardTunnels = @(); ProtonTunnelRunning = $false; ProtonLauncher = $null
    }
    try {
        $src = @(Find-NetRoute -RemoteIPAddress '1.1.1.1' -ErrorAction Stop | Where-Object { $_.IPAddress }) | Select-Object -First 1
        if ($src) { $f.RouteIsLan = ([string]$src.IPAddress -eq '192.168.1.58') }
    } catch { }
    try {
        $f.VpnAdapters = @([System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | ForEach-Object {
                [pscustomobject]@{ Name = $_.Name; Description = $_.Description; Type = [string]$_.NetworkInterfaceType; Up = ($_.OperationalStatus -eq 'Up') }
            } | Where-Object { Test-VpnAdapter $_ })
    } catch { }
    try {
        $wgHome = Join-Path $env:ProgramFiles 'WireGuard'
        $f.WireGuardTunnels = @(Get-CimInstance Win32_Service -Filter "Name LIKE 'WireGuardTunnel$%'" -ErrorAction Stop | ForEach-Object {
                $exe = ''
                if ([string]$_.PathName -match '^"([^"]+)"') { $exe = $Matches[1] } elseif ([string]$_.PathName -match '^(\S+)') { $exe = $Matches[1] }
                $owner = 'WireGuard'
                if (-not $exe.StartsWith($wgHome + '\', [StringComparison]::OrdinalIgnoreCase)) {
                    $owner = Split-Path (Split-Path $exe -Parent) -Leaf
                    if ($owner -ieq 'WireGuard') { $owner = Split-Path (Split-Path (Split-Path $exe -Parent) -Parent) -Leaf }
                }
                [pscustomobject]@{ Name = $_.Name; Tunnel = ($_.Name -replace '^WireGuardTunnel\$', ''); Running = ($_.State -eq 'Running'); Exe = $exe; Owner = $owner }
            })
    } catch { }
    try { $f.ProtonTunnelRunning = (@(Get-Service -Name 'ProtonVPN WireGuard' -ErrorAction Stop | Where-Object { $_.Status -eq 'Running' }).Count -gt 0) } catch { }
    $launcher = Join-Path $env:ProgramFiles 'Proton\VPN\ProtonVPN.Launcher.exe'
    if (Test-Path -LiteralPath $launcher -PathType Leaf) { $f.ProtonLauncher = $launcher }
    [pscustomobject]$f
}

function Update-VpnButtons {
    # Enabled only when the tile says VPN ON or a VPN is up; the facts are re-read at most every 30 s.
    $btns = @($script:ui.VpnButtons)
    if (-not $btns.Count) { return }
    if (-not $script:vpnFactsAt -or ([datetime]::UtcNow - $script:vpnFactsAt).TotalSeconds -ge 30) {
        if ($script:BackgroundRefresh) {
            # cc-P154: every refresh reads them in the refresh runspace; this asks for them alone only while Auto refresh
            # is off (asked at the start, it held up the first status behind it).
            if (-not $script:NetProbeDisabled -and $script:ui.Auto -and -not $script:ui.Auto.Checked) { [void](Start-ConsoleRefresh -Parts @('vpn')) }
        } else {
            if (-not $script:NetProbeDisabled) { $script:vpnFacts = Get-VpnFacts $script:cfg }
            $script:vpnFactsAt = [datetime]::UtcNow
        }
    }
    $on = Test-VpnButtonEnabled (Get-DnsGlance $script:netResult) $script:vpnFacts
    $tip = (Get-ConsoleTooltips)['vpn.disconnect']
    if (-not $on) { $tip = 'No VPN is on: nothing to disconnect. ' + $tip }
    foreach ($b in $btns) {
        $b.Enabled = $on
        if ($script:ui.ToolTip) { $script:ui.ToolTip.SetToolTip($b, $tip) }
    }
}

function Update-VpnRecheck {
    # After a Disconnect VPN click: report the next tile result that says the VPN is off, or what is still up after
    # two minutes. When public DNS still points elsewhere, offer to restart sl-ddns (never automatic).
    if (-not $script:vpnRecheckSince -or -not $script:netResultAt -or $script:netResultAt -le $script:vpnRecheckSince) { return }
    $t = Get-DnsGlance $script:netResult
    $elapsed = ([datetime]::UtcNow - $script:vpnRecheckSince).TotalSeconds
    if ($t.Word -eq 'OK') {
        $script:vpnRecheckSince = $null
        Write-ConsoleLine 'VPN off: PUBLIC DNS is green, and public DNS points at this house.' 'green'
    } elseif ($t.Word -eq 'WRONG IP') {
        $script:vpnRecheckSince = $null
        Write-ConsoleLine ('VPN off, but public DNS still points elsewhere: ' + $t.Detail) 'amber'
        if ((Show-YesNo 'The VPN is off, but public DNS still has the VPN''s address. Restart sl-ddns now so it publishes this house''s address? (Otherwise ddns corrects it within its interval.)') -eq 'Yes') {
            [void](Invoke-Plan -Plan @(Get-DdnsRestartPlan $script:cfg) -Config $script:cfg -ActionKey 'vpn.ddns' -ShardKey 'network')
            $script:netResultAt = $null
        } else { Write-ConsoleLine 'sl-ddns left alone: it corrects public DNS within its interval.' 'gray' }
    } elseif ($elapsed -gt 120) {
        $script:vpnRecheckSince = $null
        Write-ConsoleLine ('Still up after two minutes: ' + $t.Detail) 'amber'
    } else {
        # Keep checking: Proton is disconnected by hand. Ask for the next probe now rather than in 30 s.
        if (([datetime]::UtcNow - $script:netResultAt).TotalSeconds -ge 5) { $script:netResultAt = $null }
    }
}

# Set by the facts so a form built in a test never reaches the network.
$script:NetProbeDisabled = $false

function Start-NetworkProbe {
    # Runs Get-NetworkProbe in a runspace of its own, so a slow or dead network never holds the form.
    # The functions it needs are handed over as text: a runspace starts empty.
    $ps = [PowerShell]::Create()
    $defs = @('Test-VpnAdapter', 'ConvertFrom-DohJson', 'ConvertFrom-TraceText', 'Get-NetworkProbe') | ForEach-Object {
        'function ' + $_ + ' {' + (Get-Item ('function:' + $_)).Definition + '}'
    }
    $code = '$script:VpnAdapterPattern = ' + "'" + ($script:VpnAdapterPattern -replace "'", "''") + "'" + "`n" + ($defs -join "`n") + "`nGet-NetworkProbe"
    [void]$ps.AddScript($code)
    $script:netProbe = [pscustomobject]@{ PS = $ps; Handle = $ps.BeginInvoke(); Started = [datetime]::UtcNow }
}

function Update-NetworkTile {
    # On the UI timer: collect a finished probe (never waits for one), draw the tile, start the next
    # one when the last is older than a refresh. A probe that ran past 30 s is abandoned as UNKNOWN.
    $p = $script:netProbe
    if ($p) {
        if ($p.Handle.IsCompleted) {
            try {
                $out = @($p.PS.EndInvoke($p.Handle))
                if ($out.Count) { $script:netResult = $out[-1] } else { $script:netResult = [pscustomobject]@{ RouteIsLan = $null; RouteError = 'the check returned nothing'; VpnAdapters = @() } }
            } catch {
                $script:netResult = [pscustomobject]@{ RouteIsLan = $null; RouteError = $_.Exception.Message; VpnAdapters = @() }
            }
            $p.PS.Dispose(); $script:netProbe = $null; $script:netResultAt = [datetime]::UtcNow
        } elseif (([datetime]::UtcNow - $p.Started).TotalSeconds -gt 30) {
            try { $p.PS.Stop(); $p.PS.Dispose() } catch { }
            $script:netProbe = $null; $script:netResultAt = [datetime]::UtcNow
            $script:netResult = [pscustomobject]@{ RouteIsLan = $null; RouteError = 'the check took longer than 30 s'; VpnAdapters = @() }
        }
    }
    if ($script:ui.NetTile) {
        $t = Get-DnsGlance $script:netResult
        # cc-P101: through Set-TileLook (which re-fits the tiles) when the words change. Four across, a title such as
        # PUBLIC DNS: CHECKING wraps, and set directly it was never re-fitted. Same words: only the colours, as before.
        if ($script:ui.NetTile.Word.Text -cne ($t.Title + ': ' + $t.Word) -or $script:ui.NetTile.Detail.Text -cne $t.Detail) { Set-TileLook $script:ui.NetTile $t }
        else {
            $cols = Get-TileColors $t.Color
            $script:ui.NetTile.Panel.BackColor = $cols[0]
            $script:ui.NetTile.Word.ForeColor = $cols[1]
            $script:ui.NetTile.Detail.ForeColor = $cols[1]
        }
    }
    Update-VpnRecheck
    Update-VpnButtons
    if (-not $script:NetProbeDisabled -and -not $script:netProbe -and (-not $script:netResultAt -or ([datetime]::UtcNow - $script:netResultAt).TotalSeconds -ge 30)) {
        Start-NetworkProbe
    }
}

# --- the websites (cc-P101) -----------------------------------------------------------------------
# One GET per site, redirects not followed (a redirect is a site answering), a short timeout each. Run in a runspace
# of its own every 60 s (Start-WebsiteProbe), as the DNS tile's check is, so a dead site never holds the window.

function Get-WebsiteProbe {
    param($Checks, [int]$TimeoutSec = 4)
    $out = @{}
    foreach ($c in @($Checks)) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $r = [pscustomobject]@{ Code = 0; Error = ''; TimedOut = $false; Ms = 0 }
        try {
            $req = [Net.HttpWebRequest]::Create([string]$c.Url)
            $req.Method = 'GET'
            $req.AllowAutoRedirect = $false
            $req.Timeout = $TimeoutSec * 1000
            $req.ReadWriteTimeout = $TimeoutSec * 1000
            $req.UserAgent = 'ShardConsole-Websites'
            $resp = $req.GetResponse()
            $r.Code = [int]$resp.StatusCode
            $resp.Close()
        } catch [Net.WebException] {
            $e = $_.Exception
            if ($e.Response) { $r.Code = [int]$e.Response.StatusCode; $e.Response.Close() }
            elseif ($e.Status -eq [Net.WebExceptionStatus]::Timeout) { $r.TimedOut = $true }
            else { $r.Error = [string]$e.Status }
        } catch {
            $r.Error = $_.Exception.Message
        }
        $r.Ms = [int]$sw.ElapsedMilliseconds
        $out[[string]$c.Key] = $r
    }
    $out
}

function Get-WebsiteChecks {
    param($Config)
    @($Config.Websites.Sites | ForEach-Object { [pscustomobject]@{ Key = $_.Key; Url = $_.Check } })
}

function Start-WebsiteProbe {
    $ps = [PowerShell]::Create()
    $code = 'param($checks, $t) function Get-WebsiteProbe {' + (Get-Item 'function:Get-WebsiteProbe').Definition + '}' + "`nGet-WebsiteProbe `$checks `$t"
    [void]$ps.AddScript($code).AddArgument((Get-WebsiteChecks $script:cfg)).AddArgument($script:WebsiteTimeoutSec)
    $script:webProbe = [pscustomobject]@{ PS = $ps; Handle = $ps.BeginInvoke(); Started = [datetime]::UtcNow }
}

function Update-WebsitesView {
    # The WEBSITES tile and, when it exists, the Websites tab's panel, from the last finished check.
    $t = Get-WebsitesGlance $script:cfg.Websites $script:webResults
    if ($script:ui.WebsitesTile) { Set-TileLook $script:ui.WebsitesTile $t }
    $box = $script:ui.WebsitesPanel
    if ($box) {
        $box.Clear()
        $at = $(if ($script:webResultAt) { $script:webResultAt.ToLocalTime().ToString('HH:mm:ss', $script:Invariant) } else { '' })
        Set-RichLines $box @(Format-WebsitesPanel $script:cfg.Websites $script:webResults $at)
        $box.SelectionStart = 0
        $box.ScrollToCaret()
    }
}

function Update-WebsitesTile {
    # On the 1 s UI timer: collect a finished check (never waits), draw, start the next every 60 s or when asked.
    $p = $script:webProbe
    if ($p) {
        if ($p.Handle.IsCompleted) {
            try {
                $out = @($p.PS.EndInvoke($p.Handle))
                $r0 = $(if ($out.Count) { $out[-1] } else { @{} })
                if ($r0 -is [psobject]) { $r0 = $r0.psobject.BaseObject }
                $script:webResults = $r0
            } catch { $script:webResults = @{} }
            $p.PS.Dispose(); $script:webProbe = $null; $script:webResultAt = [datetime]::UtcNow
            Update-WebsitesView
        } elseif (([datetime]::UtcNow - $p.Started).TotalSeconds -gt 60) {
            try { $p.PS.Stop(); $p.PS.Dispose() } catch { }
            $script:webProbe = $null; $script:webResultAt = [datetime]::UtcNow
        }
    }
    if (-not $script:NetProbeDisabled -and -not $script:webProbe -and @($script:cfg.Websites.Sites).Count -and (-not $script:webResultAt -or ([datetime]::UtcNow - $script:webResultAt).TotalSeconds -ge 60)) {
        Start-WebsiteProbe
    }
}

function Get-SelectedWebsite {
    $pick = $script:ui.WebsitePick
    if (-not $pick -or $pick.SelectedIndex -lt 0) { return '' }
    [string]@($script:cfg.Websites.Sites)[$pick.SelectedIndex].Key
}

function New-WebsiteButton {
    # A Websites tab button: the same as New-ConsoleButton, but it hands over the site picked in the list.
    param([string]$Text, [string]$ActionKey, [int]$Width = 170)
    $b = New-Object Windows.Forms.Button
    $b.Text = $Text
    $b.Tag = $ActionKey
    $b.Width = $Width
    $b.Height = 36
    $b.Margin = New-Object Windows.Forms.Padding(6)
    $b.Add_Click({ Invoke-ConsoleButton -ActionKey $this.Tag -Site (Get-SelectedWebsite) })
    $b
}

function Get-MapArtInputs {
    # cc-P135: the Map and Art tab's boxes, as a click hands them over.
    $r = @{ VdFile = ''; Body = ''; HuesFile = '' }
    if ($script:ui.VdFileBox) { $r.VdFile = ([string]$script:ui.VdFileBox.Text).Trim() }
    if ($script:ui.BodyBox) { $r.Body = ([string]$script:ui.BodyBox.Text).Trim() }
    if ($script:ui.HuesFileBox) { $r.HuesFile = ([string]$script:ui.HuesFileBox.Text).Trim() }
    $r
}

function New-MapArtButton {
    # cc-P135: a Map and Art button: as New-ConsoleButton, but it hands over the .vd, body and hues.mul boxes.
    param([string]$Text, [string]$ActionKey, [int]$Width = 230)
    $b = New-Object Windows.Forms.Button
    $b.Text = $Text
    $b.Tag = $ActionKey
    $b.Width = $Width
    $b.Height = 36
    $b.Margin = New-Object Windows.Forms.Padding(6)
    $b.Add_Click({ $i = Get-MapArtInputs; Invoke-ConsoleButton -ActionKey $this.Tag -VdFile $i.VdFile -Body $i.Body -HuesFile $i.HuesFile })
    $b
}

function Show-FilePicker {
    # cc-P135: one existing file, picked in the standard dialog; its path, or $null on Cancel.
    param([string]$Filter, [string]$Title)
    $d = New-Object Windows.Forms.OpenFileDialog
    $d.Filter = $Filter
    $d.Title = $Title
    $d.CheckFileExists = $true
    $d.Multiselect = $false
    if ($d.ShowDialog() -eq [Windows.Forms.DialogResult]::OK) { return $d.FileName }
    $null
}

function Get-SelectedQuestionShard {
    # cc-P106: the shard picked on the Shard question tab.
    if ($script:ui.QuestionLive -and $script:ui.QuestionLive.Checked) { return 'live' }
    'test'
}

function Update-QuestionPanel {
    # cc-P106: the picked shard's question and answers, and only its answers' buttons enabled (Show and Stop always).
    param($State)
    if (-not $script:ui -or -not $script:ui.QuestionText -or -not $State) { return }
    $p = Get-QuestionPanelInfo $State (Get-SelectedQuestionShard) $script:cfg
    $script:ui.QuestionText.Text = $p.Lines -join "`r`n"
    foreach ($b in @($script:ui.QuestionButtons)) { $b.Enabled = ($p.Enabled -contains $b.Tag) }
}

function Get-QuestionPanelInfo {
    # cc-P187: the Shard question tab's text for one shard and which of its buttons apply (Show and Stop always, and the
    # answers the shard's question takes). Update-QuestionPanel's and the web console's. Pure: the state in.
    param($State, [string]$Key, $Config)
    $sh = $Config.Shards[$Key]
    $q = $State.Shards[$Key].Question
    $lines = @('The ' + $sh.Label + ' shard (' + $sh.Container + ') is not waiting on a console question.')
    if ($q) { $lines = @(Get-QuestionText $q $sh $Config) }
    $keys = @(@($(if ($q) { $q.Answers } else { @() })) | ForEach-Object { 'question.' + $_.Key })
    [pscustomobject]@{ Lines = @($lines); Enabled = @(@('question.show', 'question.stop') + $keys); Asking = [bool]$q }
}

function Get-SelectedSnapshotShard {
    if ($script:ui.SnapLive.Checked) { 'live' } else { 'test' }
}

function Update-SnapshotList {
    $lv = $script:ui.SnapList
    if (-not $lv -or -not $script:lastState) { return }
    $k = Get-SelectedSnapshotShard
    $lv.BeginUpdate()
    $lv.Items.Clear()
    foreach ($r in @(Get-SnapshotRows $script:lastState $k)) {
        $item = New-Object Windows.Forms.ListViewItem($r.Name)
        foreach ($c in @($r.Taken, $r.Size, $r.Files, $r.Accounts, $r.Kind)) { [void]$item.SubItems.Add([string]$c) }
        [void]$lv.Items.Add($item)
    }
    $lv.EndUpdate()
    $script:ui.SnapPath.Text = 'save: ' + $script:cfg.Shards[$k].Saves + '    snapshots: ' + (Join-Path $script:cfg.SnapshotRoot $k)
}

function Get-SnapshotRows {
    # cc-P187: the Snapshots tab's list for one shard (Update-SnapshotList's rows, and the web console's Snapshots page):
    # { Name, Taken, Size, Files, Accounts (yes or NO), Kind (named or pre-restore) }. Pure: the state in.
    param($State, [string]$Key)
    @(foreach ($s in @($State.Shards[$Key].Snapshots)) {
        [pscustomobject]@{ Name = $s.Name; Taken = $s.Taken.ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant); Size = (Format-Bytes $s.Bytes); Files = [string]$s.Files
            Accounts = $(if ($s.HasAccounts) { 'yes' } else { 'NO' }); Kind = $(if ($s.IsPreRestore) { 'pre-restore' } else { 'named' }) }
    })
}

function New-TypedConfirmForm {
    # cc-P132: the typed-confirmation dialog, built but not shown (Show-TypedConfirm shows it; the facts and the
    # screenshot script build it). The phrase line is large and bright on a plain plate (the theme's Plate), never a
    # dark red on the stone (Chase, 10-16 07:34: "Type exactly: publish player package" could barely be read).
    param([string]$Phrase, [string]$Text, $Plan, [string]$What = 'LIVE shard')
    $d = New-Object Windows.Forms.Form
    Set-ConsoleIcon $d $script:cfg
    $d.Text = $What + ': type to confirm'
    if ($script:dry) { $d.Text = 'DRY RUN - ' + $What + ': type to confirm (the window it opens only prints)' }
    $d.Size = New-Object Drawing.Size(640, 440)
    $d.FormBorderStyle = 'FixedDialog'
    $d.StartPosition = 'CenterParent'
    $d.MaximizeBox = $false
    $d.MinimizeBox = $false
    $body = New-Object Windows.Forms.TextBox
    $body.Multiline = $true
    $body.ReadOnly = $true
    $body.ScrollBars = 'Vertical'
    $body.Font = $script:MonoFont
    $body.Dock = 'Fill'
    $pre = Get-ConfirmPreface $Plan
    if ($pre) { $pre += "`r`n`r`n" }
    $body.Text = ($pre + $Text + "`r`n`r`nThis will run:`r`n" + ((Format-Plan -Action '' -ShardKey 'live' -Plan $Plan -Header 'PLAN' | Select-Object -Skip 1) -join "`r`n"))
    $bottom = New-Object Windows.Forms.Panel
    $bottom.Dock = 'Bottom'
    $bottom.Height = 92
    $bottom.Name = 'confirmPlate'
    $label = New-Object Windows.Forms.Label
    $label.Text = "Type exactly:  $Phrase"
    $label.Name = 'confirmPhrase'
    $label.SetBounds(10, 8, 610, 32)
    $plate = $(if ($script:theme) { $script:theme.Plate } else { $null })
    $t = $script:theme
    if ($plate) {
        $bottom.Tag = 'plate'; $label.Tag = 'plate'
        $bottom.BackColor = ConvertTo-DrawingColor $plate.Back
        $label.ForeColor = ConvertTo-DrawingColor $plate.Text
        $label.BackColor = [Drawing.Color]::Transparent
        $label.Font = New-Object Drawing.Font($t.Fonts.Ui, [single]$plate.Size, [Drawing.FontStyle]::Bold)
    } else {
        $label.Font = New-Object Drawing.Font('Segoe UI', 12, [Drawing.FontStyle]::Bold)
        $label.ForeColor = ConvertTo-DrawingColor $(if ($t -and $t.Text) { $t.Text.Danger } else { @(170, 28, 28) })
    }
    $tb = New-Object Windows.Forms.TextBox
    $tb.Name = 'confirmTyped'
    $tb.Font = New-Object Drawing.Font($(if ($t) { $t.Fonts.Ui } else { 'Segoe UI' }), 11)
    $tb.SetBounds(10, 48, 360, 28)
    $ok = New-Object Windows.Forms.Button
    $ok.Text = 'Do it'
    $ok.Name = 'confirmDo'
    $ok.Enabled = $false
    $ok.DialogResult = 'OK'
    $ok.SetBounds(390, 46, 100, 30)
    $cancel = New-Object Windows.Forms.Button
    $cancel.Text = 'Cancel'
    $cancel.Name = 'confirmCancel'
    $cancel.DialogResult = 'Cancel'
    $cancel.SetBounds(500, 46, 100, 30)
    $script:confirmPhrase = $Phrase
    $script:confirmBox = $tb
    $script:confirmOk = $ok
    $tb.Add_TextChanged({ $script:confirmOk.Enabled = Test-TypedConfirmation $script:confirmPhrase $script:confirmBox.Text })
    $bottom.Controls.AddRange(@($label, $tb, $ok, $cancel))
    $d.Controls.Add($body)
    $d.Controls.Add($bottom)
    $d.AcceptButton = $ok
    $d.CancelButton = $cancel
    $d.Add_Shown({ $script:confirmBox.Focus() })
    Set-ControlTheme $d $script:theme $script:themeImages # cc-P84: nothing for Plain
    [pscustomobject]@{ Form = $d; Body = $body; Plate = $bottom; Label = $label; Box = $tb; Ok = $ok; Cancel = $cancel }
}

function Show-TypedConfirm {
    param([string]$Phrase, [string]$Text, $Plan, [string]$What = 'LIVE shard')
    $c = New-TypedConfirmForm -Phrase $Phrase -Text $Text -Plan $Plan -What $What
    $d = $c.Form
    $result = $d.ShowDialog()
    $typed = $c.Box.Text
    $d.Dispose()
    ($result -eq 'OK') -and (Test-TypedConfirmation $Phrase $typed)
}

function Show-ListPicker {
    # A small dialog: a list with columns, OK and Cancel. Returns the Tag of the row picked, or $null.
    param([string]$Title, [string]$Caption, [object[]]$Columns, [object[]]$Rows)
    $d = New-Object Windows.Forms.Form
    Set-ConsoleIcon $d $script:cfg
    $d.Text = $Title
    if ($script:dry) { $d.Text = 'DRY RUN - ' + $Title }
    $d.Size = New-Object Drawing.Size(900, 460)
    $d.StartPosition = 'CenterParent'
    $d.MinimizeBox = $false
    $cap = New-Object Windows.Forms.Label
    $cap.Text = $Caption
    $cap.Dock = 'Top'
    $cap.Height = 40
    $cap.Padding = New-Object Windows.Forms.Padding(6)
    $lv = New-Object Windows.Forms.ListView
    $lv.Dock = 'Fill'
    $lv.View = 'Details'
    $lv.FullRowSelect = $true
    $lv.MultiSelect = $false
    $lv.HideSelection = $false
    $lv.Font = $script:MonoFont
    foreach ($c in $Columns) { [void]$lv.Columns.Add($c[0], $c[1]) }
    foreach ($r in $Rows) {
        $item = New-Object Windows.Forms.ListViewItem([string]$r.Cells[0])
        foreach ($x in @($r.Cells | Select-Object -Skip 1)) { [void]$item.SubItems.Add([string]$x) }
        $item.Tag = $r.Tag
        [void]$lv.Items.Add($item)
    }
    $bottom = New-Object Windows.Forms.FlowLayoutPanel
    $bottom.Dock = 'Bottom'
    $bottom.Height = 44
    $bottom.FlowDirection = 'RightToLeft'
    $cancel = New-Object Windows.Forms.Button
    $cancel.Text = 'Cancel'; $cancel.DialogResult = 'Cancel'; $cancel.Width = 100; $cancel.Height = 30
    $ok = New-Object Windows.Forms.Button
    $ok.Text = 'Choose'; $ok.DialogResult = 'OK'; $ok.Width = 100; $ok.Height = 30
    $bottom.Controls.AddRange(@($cancel, $ok))
    $d.Controls.Add($lv)
    $d.Controls.Add($cap)
    $d.Controls.Add($bottom)
    $d.AcceptButton = $ok
    $d.CancelButton = $cancel
    $lv.Add_DoubleClick({ $this.FindForm().DialogResult = 'OK' })
    Set-ControlTheme $d $script:theme $script:themeImages # cc-P84: nothing for Plain
    $result = $d.ShowDialog()
    $pick = $null
    if ($result -eq 'OK' -and $lv.SelectedItems.Count) { $pick = [string]$lv.SelectedItems[0].Tag }
    $d.Dispose()
    $pick
}

function Show-CommitPicker {
    # cc-P96, Fix test shard > Rebuild: HEAD, the recent commits, and the last good image's commit.
    param($State, $Facts)
    $rows = @(Get-CommitPickerRows $State $Facts)
    if (-not $rows.Count) { Write-ConsoleLine 'fix.rebuild: git gave no commits to choose from.' 'amber'; return $null }
    Show-ListPicker -Title 'Rebuild the TEST shard from a commit' -Caption 'Pick the commit to build through every test gate. The test shard restarts on it after you type the phrase; latest is not moved.' -Columns @(@('Commit', 90), @('Date', 100), @('', 110), @('Subject', 560)) -Rows $rows
}

function Get-CommitPickerRows {
    # cc-P187: Rebuild's list (Show-CommitPicker's, and the web console's Fix page): { Tag (the commit), Cells (short,
    # date, HEAD and last good, subject) }. Pure: the state and Get-FixFacts in, rows out.
    param($State, $Facts)
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $lg = Get-LastGoodImage $State.Images
    $lgc = $null
    if ($lg) { $lgc = Get-GoodTagCommit $lg.Tag }
    foreach ($c in @($Facts.Commits)) {
        $note = ''
        if ($c.Sha -eq $Facts.Head) { $note = 'HEAD' }
        if ($lgc -and $c.Sha.StartsWith($lgc)) { $note = ($note + ' last good').Trim() }
        $rows.Add([pscustomobject]@{ Tag = $c.Sha; Cells = @($c.Short, $c.Date, $note, $c.Subject) })
    }
    if ($lgc -and -not @($Facts.Commits | Where-Object { $_.Sha.StartsWith($lgc) }).Count) {
        $rows.Add([pscustomobject]@{ Tag = $lgc; Cells = @($lgc, '', 'last good', ('the commit of ' + $lg.Tag)) })
    }
    $rows.ToArray()
}

function Show-ImagePicker {
    # Test shard > Deploy built image: which sl-modernuo:cc-p* to put on the test shard (cc-P63).
    param($State)
    $rows = @(Get-ImagePickerRows $State $script:cfg)
    if (-not $rows.Count) { Write-ConsoleLine 'test.deploy: there is no sl-modernuo:cc-p* image to deploy (docker images sl-modernuo).' 'amber'; return $null }
    $was = '(none)'
    if ($State.Latest.Exists) { $was = Format-ShortId $State.Latest.Id }
    Show-ListPicker -Title 'Deploy built image to the TEST shard' -Caption ($script:cfg.LatestTag + ' is ' + $was + ' now. Pick the image to point it at; the test shard is restarted on it after you type the phrase.') -Columns @(@('Image', 260), @('Id', 120), @('Created', 160), @('', 120)) -Rows $rows
}

function Get-ImagePickerRows {
    # cc-P187: Deploy built image's list (Show-ImagePicker's, and the web console's Test shard page): { Tag, Cells (tag,
    # id, built, whether latest points at it now) }. Pure: the state in, rows out.
    param($State, $Config)
    @(Get-DeployCandidates $State $Config | ForEach-Object {
        $now = ''
        if ($_.IsLatest) { $now = '<- latest now' }
        [pscustomobject]@{ Tag = $_.Tag; Cells = @($_.Tag, $_.Id, (Format-LocalTime $_.Created), $now) }
    })
}

function Get-PackageListRows {
    # cc-P187: the Player package tab's zips in dist\, newest first (Update-PackageList's rows, and the web console's
    # Player package page): { Name, Version, Size, Built, Notes }.
    param($Config)
    @(foreach ($z in @(Get-PackageZips $Config.PackageDist)) {
        $ver = '(no version.json)'; $nt = ''
        if ($z.Json.Exists) { $ver = [string]$z.Json.Version; $nt = [string]$z.Json.Notes; if ($z.Json.Error) { $ver = '(version.json unreadable)' } }
        [pscustomobject]@{ Name = $z.Name; Version = $ver; Size = (Format-Bytes $z.Bytes); Built = (Format-LocalTime $z.LastWriteUtc); Notes = $nt }
    })
}

function Update-PackageList {
    # The Player package tab's list of zips in dist\, newest first, and (cc-P124) the client Build package ships.
    if ($script:ui.PackageClient) {
        $script:ui.PackageClient.Text = Format-PackageClientText (Get-PackageClientFacts -ClientDir $script:cfg.PackageClient -RepoRoot $script:cfg.RepoRoot)
    }
    $lv = $script:ui.PackageList
    if (-not $lv) { return }
    $lv.BeginUpdate()
    $lv.Items.Clear()
    foreach ($r in @(Get-PackageListRows $script:cfg)) {
        $item = New-Object Windows.Forms.ListViewItem($r.Name)
        foreach ($c in @($r.Version, $r.Size, $r.Built, $r.Notes)) { [void]$item.SubItems.Add([string]$c) }
        [void]$lv.Items.Add($item)
    }
    $lv.EndUpdate()
}

function Update-CommitCountdown {
    # cc-P68 Part D: the Commit tab's line. Nothing is read while another tab is showing. The time
    # left is worked out every tick from the facts last read; the repos are read again every
    # CommitPollSeconds (or on -Reread), so a file changed meanwhile restarts the count.
    param([switch]$Reread)
    $ui = $script:ui
    if (-not $ui -or -not $ui.CommitCountdown -or -not $ui.Tabs -or $ui.Tabs.SelectedTab -ne $ui.CommitPage) { return }
    $now = [datetime]::UtcNow
    if ($Reread -or -not $script:commitFactsCache -or -not $script:commitFactsAt -or ($now - $script:commitFactsAt).TotalSeconds -ge $script:cfg.CommitPollSeconds) {
        if ($script:BackgroundRefresh) {
            # cc-P154: git in the refresh runspace, asked once per CommitPollSeconds; this tick counts from the last read.
            if ($Reread -or -not $script:commitAskedAt -or ($now - $script:commitAskedAt).TotalSeconds -ge $script:cfg.CommitPollSeconds) {
                $script:commitAskedAt = $now
                [void](Start-ConsoleRefresh -Parts @('commit') -Queue)
            }
            if (-not $script:commitFactsCache) { if ($ui.CommitCountdown.Text -ne 'Reading the repos...') { $ui.CommitCountdown.Text = 'Reading the repos...' }; return }
        } else {
            try { $script:commitFactsCache = Get-CommitFacts $script:cfg; $script:commitFactsAt = $now }
            catch { $ui.CommitCountdown.Text = 'Could not read the repos: ' + $_.Exception.Message; $ui.CommitCountdown.ForeColor = Get-StateTextColor 'Danger'; return }
        }
    }
    $t = Get-CommitCountdown -Facts $script:commitFactsCache -Config $script:cfg -Now $now -Force:$ui.CommitForce.Checked
    if ($ui.CommitCountdown.Text -ne $t) { $ui.CommitCountdown.Text = $t }
    # cc-P132: the theme's success and warning colors (ForestGreen on parchment measured 3.7:1, under AA).
    if ($t -eq 'Ready to commit') { $ui.CommitCountdown.ForeColor = Get-StateTextColor 'Success' } else { $ui.CommitCountdown.ForeColor = Get-StateTextColor 'Warning' }
}

function Set-ConsoleDryRun {
    # The DRY RUN box. Buttons print instead of run; the status panel is unaffected.
    param([bool]$On)
    if ($script:fromCapture -and -not $On) { $On = $true }
    $changed = ([bool]$script:dry -ne $On)
    $script:dry = $On
    $box = $script:ui.DryBox
    if ($box) {
        if ($box.Checked -ne $On) { $box.Checked = $On }
        # cc-P84: the amber tile's colors when on; off, the theme's text color for where the box sits (Plain: Black).
        $cols = Get-TileColors 'amber'
        if ($On) { $box.BackColor = $cols[0]; $box.ForeColor = $cols[1] }
        else {
            $box.BackColor = [Drawing.Color]::Transparent
            # cc-P132: the theme's normal text for where the box sits (the stone in a themed look).
            $box.ForeColor = ConvertTo-DrawingColor $(if ($script:theme -and $script:theme.Chrome) { $script:theme.Chrome.StoneText } elseif ($script:theme -and $script:theme.Text) { $script:theme.Text.Normal } else { @(0, 0, 0) })
        }
    }
    if ($script:lastState) { Update-GlanceTiles $script:lastState }
    if ($changed) {
        if ($On) { Write-ConsoleLine 'DRY RUN on: buttons print the plan, and a window action opens a window that only prints. Nothing runs.' 'amber' }
        else { Write-ConsoleLine 'DRY RUN off: buttons run for real.' 'head' }
    }
}

function Show-WipeAdminPicker {
    # Test shard > Wipe test accounts (cc-P73), first dialog: what goes and what stays, with the counts
    # the test shard reported, and the name of the new admin. Returns the name, or $null on Cancel.
    # The phrase is asked next, by Show-TypedConfirm as for every other action; the password last, in
    # the action's own window, so it never sits in this form or crosses a command line.
    param([string]$Summary)
    $d = New-Object Windows.Forms.Form
    Set-ConsoleIcon $d $script:cfg
    $d.Text = 'Wipe test accounts: the new admin'
    if ($script:dry) { $d.Text = 'DRY RUN - ' + $d.Text }
    $d.Size = New-Object Drawing.Size(640, 360)
    $d.FormBorderStyle = 'FixedDialog'
    $d.StartPosition = 'CenterParent'
    $d.MaximizeBox = $false
    $d.MinimizeBox = $false
    $body = New-Object Windows.Forms.TextBox
    $body.Multiline = $true; $body.ReadOnly = $true; $body.ScrollBars = 'Vertical'; $body.Dock = 'Fill'
    $body.Text = $Summary
    $bottom = New-Object Windows.Forms.Panel
    $bottom.Dock = 'Bottom'
    $bottom.Height = 76
    $label = New-Object Windows.Forms.Label
    $label.Text = 'Name of the new Owner account (letters, digits, . _ -):'
    $label.SetBounds(10, 6, 600, 20)
    $tb = New-Object Windows.Forms.TextBox
    $tb.SetBounds(10, 30, 360, 24)
    $ok = New-Object Windows.Forms.Button
    $ok.Text = 'Next'; $ok.Enabled = $false; $ok.DialogResult = 'OK'; $ok.SetBounds(390, 28, 100, 28)
    $cancel = New-Object Windows.Forms.Button
    $cancel.Text = 'Cancel'; $cancel.DialogResult = 'Cancel'; $cancel.SetBounds(500, 28, 100, 28)
    $script:wipeNameBox = $tb
    $script:wipeNameOk = $ok
    $tb.Add_TextChanged({ $script:wipeNameOk.Enabled = -not (Test-WipeAdminName $script:wipeNameBox.Text) })
    $bottom.Controls.AddRange(@($label, $tb, $ok, $cancel))
    $d.Controls.Add($body)
    $d.Controls.Add($bottom)
    $d.AcceptButton = $ok
    $d.CancelButton = $cancel
    $d.Add_Shown({ $script:wipeNameBox.Focus() })
    Set-ControlTheme $d $script:theme $script:themeImages # cc-P84: nothing for Plain
    $result = $d.ShowDialog()
    $name = $tb.Text
    $d.Dispose()
    if ($result -ne 'OK' -or (Test-WipeAdminName $name)) { return $null }
    $name
}

function Show-YesNo {
    param([string]$Text)
    $title = 'Shard Console'
    if ($script:dry) { $title = 'Shard Console - DRY RUN' }
    [string][Windows.Forms.MessageBox]::Show($Text, $title, 'YesNo', 'Warning', 'Button2')
}

function Invoke-ConsoleButton {
    param([string]$ActionKey, [string]$ShardKey, [string]$SnapshotName, [string]$Snapshot, [string]$CommitMessage,
        [string]$PackageNotes, [string]$PackageZip, [string]$ImageTag, [switch]$CommitForce, [string]$WipeAdmin, [string]$Commit, [string]$Site,
        $Prefetched, [string]$VdFile, [string]$Body, [string]$HuesFile, [string]$Golem)
    try {
        $ActionKey = Resolve-ActionAlias $ActionKey
        $def = @(Get-ConsoleActions | Where-Object { $_.Key -eq $ActionKey })[0]
        $isCommit = ($ActionKey -like 'commit.*')
        $isPackage = ($ActionKey -like 'package.*')
        $isAutopilot = ($ActionKey -like 'golem.*')
        $isVpn = ($ActionKey -like 'vpn.*')
        if (-not $ShardKey -and $isVpn) { $ShardKey = 'network' }
        if (-not $ShardKey) { $ShardKey = 'test'; if ($ActionKey -like 'live.*') { $ShardKey = 'live' }; if ($isCommit) { $ShardKey = 'repos' }; if ($isPackage) { $ShardKey = 'package' }; if ($isAutopilot) { $ShardKey = 'golem' }; if ($ActionKey -like 'links.*') { $ShardKey = 'links' }; if ($ActionKey -like 'web.*') { $ShardKey = 'websites' }; if ($ActionKey -like 'art.*' -or $ActionKey -like 'map.*') { $ShardKey = 'mapart' } }
        # cc-P187: the Golem tab's picker. A Golem button acts for the picked Golem (Get-GolemActionConfig): the console's
        # own config for the integrator; that Golem's own files and name for another on this PC; refused otherwise.
        # Assign is the integrator's, for the picked Golem (it writes the prompt's Golem line).
        $cfgUse = $script:cfg
        $golemName = ''
        if ($isAutopilot -and $ActionKey -notin @('golem.assign', 'golem.skipday')) {
            if (-not $PSBoundParameters.ContainsKey('Golem')) { $Golem = Get-PickedGolem }
            $ga = Get-GolemActionConfig -Config $script:cfg -ActionKey $ActionKey -Golem $Golem -Rows $script:ui.GolemPickRows -AnyGolemActions (Get-AnyGolemActionKeys (Join-Path $script:cfg.RepoRoot 'web-console\golem-actions.json'))
            if ($ga.Refusal) { Write-ConsoleLine ($ActionKey + ': ' + $ga.Refusal) 'amber'; return }
            $cfgUse = $ga.Config
            $golemName = $ga.Name
        }
        # Two flags since the toggle: $script:dry is "buttons print", $script:fromCapture is "the
        # status is a capture file". Only the second decides where the state comes from.
        if ($script:fromCapture -and -not $script:dry) { throw 'the status is from a capture file, so nothing may run for real. Relaunch without -DryRun.' }
        if (-not $Prefetched -and $script:BackgroundRefresh -and $ActionKey -notlike 'links.*' -and $ActionKey -notlike 'web.*' -and $ActionKey -notlike 'art.*' -and $ActionKey -notlike 'map.*') {
            # cc-P154: the reads below (docker, git, dist\, the Golem's folder, the adapters) in the background first;
            # this click carries on with them when they are back (Start-ActionPrecheck), and the window keeps painting.
            $bound = @{}
            foreach ($k in $PSBoundParameters.Keys) { $bound[$k] = $PSBoundParameters[$k] }
            $bound['ActionKey'] = $ActionKey
            if ($isAutopilot -and $ActionKey -notin @('golem.assign', 'golem.skipday')) { $bound['Golem'] = $Golem }
            [void](Start-ActionPrecheck -Bound $bound -UseConfig $cfgUse)
            return
        }
        $now = Get-Date
        $pre = $Prefetched
        if (-not $pre) { $pre = Get-ActionFacts -Config $cfgUse -ActionKey $ActionKey -PackageZip $PackageZip -FromCapture ([bool]$script:fromCapture) -StatusFrom $script:statusFrom -Dry ([bool]$script:dry) }
        $facts = $pre.Facts
        $pkgFacts = $pre.PackageFacts
        $apFacts = $pre.AutopilotFacts
        $vpnFacts = $pre.VpnFacts
        $state = $pre.State
        if ($ActionKey -like 'links.*' -or $ActionKey -like 'web.*' -or $ActionKey -like 'art.*' -or $ActionKey -like 'map.*') {
            # cc-P83: a link reads nothing. cc-P101: nor does a Websites button (websites.json is in the config). cc-P135: nor a Map and Art one.
        } elseif ($isVpn) {
            # cc-P86: this PC's adapters, route and services (Get-ActionFacts).
            $script:vpnFacts = $vpnFacts; $script:vpnFactsAt = [datetime]::UtcNow
        } elseif ($isAutopilot) {
            # cc-P74: the Golem's folder, never docker (Get-ActionFacts).
            # cc-P125: the long run's two boxes, and the file the Open buttons open (the picked queue or finished row).
            if ($script:ui.LongRunCap) { $apFacts | Add-Member -NotePropertyName LongRunCap -NotePropertyValue $script:ui.LongRunCap.Text -Force }
            if ($script:ui.LongRunUntil) { $apFacts | Add-Member -NotePropertyName LongRunUntil -NotePropertyValue $script:ui.LongRunUntil.Text -Force }
            $openFile = ''
            if ($ActionKey -eq 'golem.openprompt' -and (Get-GolemQueuePick)) { $openFile = [string](Get-GolemQueuePick).Path }
            if ($ActionKey -eq 'golem.opennotes' -and $script:ui.GolemFinishedList -and $script:ui.GolemFinishedList.SelectedItems.Count) { $openFile = [string]$script:ui.GolemFinishedList.SelectedItems[0].Tag }
            $apFacts | Add-Member -NotePropertyName OpenFile -NotePropertyValue $openFile -Force
            # cc-P182: Run next takes the picked queue row; with none picked it clears the pick.
            if ($ActionKey -eq 'golem.runnext') {
                $rp = 'cancel'
                if (Get-GolemQueuePick) { $rp = [string](Get-GolemQueuePick).Label }
                $apFacts | Add-Member -NotePropertyName RunNextPrompt -NotePropertyValue $rp -Force
            }
            # cc-P287: Owner works takes the picked Golem (the date is the Until box's, LongRunUntil above).
            if ($ActionKey -eq 'golem.skipday') { $apFacts | Add-Member -NotePropertyName SkipGolem -NotePropertyValue (Get-PickedGolem) -Force }
            # cc-P187: Assign takes the picked queue row, the picked Golem and the "and run it next" tick.
            if ($ActionKey -eq 'golem.assign') {
                $rp = ''
                if (Get-GolemQueuePick) { $rp = [string](Get-GolemQueuePick).Label }
                $apFacts | Add-Member -NotePropertyName RunNextPrompt -NotePropertyValue $rp -Force
                $apFacts | Add-Member -NotePropertyName AssignGolem -NotePropertyValue (Get-PickedGolem) -Force
                $apFacts | Add-Member -NotePropertyName AssignRunNext -NotePropertyValue $(if ($script:ui.AssignNext -and $script:ui.AssignNext.Checked) { 'yes' } else { 'no' }) -Force
            }
            # cc-P216: Answer a Golem's question: which question, which choice and the words, picked before the plan.
            if ($ActionKey -eq 'golem.answer') {
                $ans = Show-AnswerPicker -Root ([string]$script:cfg.Autopilot.GolemRoot)
                if (-not $ans) { Write-ConsoleLine 'golem.answer: cancelled.' 'gray'; return }
                $apFacts | Add-Member -NotePropertyName AnswerId -NotePropertyValue $ans.Id -Force
                $apFacts | Add-Member -NotePropertyName AnswerChoice -NotePropertyValue $ans.Choice -Force
                $apFacts | Add-Member -NotePropertyName AnswerText -NotePropertyValue $ans.Text -Force
            }
            # cc-P131: the picked model, the night shift's two times, and (for the wake only) a fresh sign-in check.
            if ($script:ui.GolemModel) {
                # cc-P153: Auto (index 0) or the override; after a Poke the picker is back on Auto.
                $apFacts | Add-Member -NotePropertyName WakeModel -NotePropertyValue $(if ($script:ui.GolemModel.SelectedIndex -le 0) { 'Auto' } else { [string]$script:ui.GolemModel.SelectedItem }) -Force
                if ($ActionKey -eq 'golem.resume') { $script:ui.GolemModel.SelectedIndex = 0 }
            }
            if ($script:ui.NightStart) { $apFacts | Add-Member -NotePropertyName NightStart -NotePropertyValue $script:ui.NightStart.Text -Force }
            if ($script:ui.NightEnd) { $apFacts | Add-Member -NotePropertyName NightEnd -NotePropertyValue $script:ui.NightEnd.Text -Force }
            if ($script:ui.MarathonPick) { $apFacts | Add-Member -NotePropertyName MarathonMode -NotePropertyValue ([string]$script:ui.MarathonPick.SelectedItem) -Force; $script:ui.MarathonPicked = $false }   # cc-P200
            # A dry run runs nothing, the sign-in check included: the plan then reads cli.json as the Golem last wrote it.
            if ($ActionKey -eq 'golem.resume' -and -not $script:dry) { $apFacts | Add-Member -NotePropertyName CliCheck -NotePropertyValue $pre.CliCheck -Force }
        } elseif ($isCommit -or $isPackage) {
            # The commit buttons read git and the notes (cc-P60), the package buttons dist\ too (cc-P63); never docker.
        } else {
            $script:lastState = $state
            if ($script:fromCapture) { $now = $state.Now.ToLocalTime() }
        }
        if ($ActionKey -eq 'test.deploy' -and -not $ImageTag) {
            # cc-P63 Part B: the picker lists sl-modernuo:cc-p* from the state just read.
            $ImageTag = Show-ImagePicker $state
            if (-not $ImageTag) { Write-ConsoleLine ($ActionKey + ': cancelled.') 'gray'; return }
        }
        $fxFacts = $null
        if ($ActionKey -like 'fix.*') {
            # cc-P96: git and the test world's save record, read now. Roll back pins the image it will run, so the
            # window it opens cannot pick a different one; Rebuild asks which commit.
            $fxFacts = $(if ($pre.FixFacts) { $pre.FixFacts } else { Get-FixFacts $script:cfg $state -NoDocker:$script:fromCapture })
            if ($ActionKey -eq 'fix.rollback' -and -not $ImageTag) {
                $lg = Get-LastGoodImage $state.Images
                if ($lg) { $ImageTag = $lg.Tag }
            }
            if ($ActionKey -eq 'fix.rebuild' -and -not $Commit) {
                $Commit = Show-CommitPicker $state $fxFacts
                if (-not $Commit) { Write-ConsoleLine ($ActionKey + ': cancelled.') 'gray'; return }
            }
        }
        $wFacts = $null
        if ($ActionKey -eq 'test.wipe') {
            # cc-P73: refused before any dialog when the facts already rule it out (SYSTEM, no census,
            # a waiting request, no room for the backup); otherwise the counts, then the new admin's name.
            $wFacts = $(if ($pre.WipeFacts) { $pre.WipeFacts } else { Get-WipeFacts $script:cfg $state })
            $no = @(Get-WipeRefusals -Config $script:cfg -State $state -Facts $wFacts)
            if ($no.Count) { foreach ($x in $no) { Write-ConsoleLine ($ActionKey + ': ' + $x) 'amber' }; return }
            if (-not $WipeAdmin) {
                $WipeAdmin = Show-WipeAdminPicker (Get-WipeSummary $wFacts '')
                if (-not $WipeAdmin) { Write-ConsoleLine ($ActionKey + ': cancelled. Nothing was changed.') 'gray'; return }
            }
        }
        $plan = @(Get-ActionPlan -Action $ActionKey -ShardKey $ShardKey -Config $cfgUse -State $state -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now -CommitMessage $CommitMessage -CommitFacts $facts -PackageNotes $PackageNotes -PackageZip $PackageZip -PackageFacts $pkgFacts -ImageTag $ImageTag -CommitForce:$CommitForce -WipeFacts $wFacts -WipeAdmin $WipeAdmin -AutopilotFacts $apFacts -VpnFacts $vpnFacts -FixFacts $fxFacts -Commit $Commit -Site $Site -VdFile $VdFile -Body $Body -HuesFile $HuesFile)
        if ($golemName) { $plan = @(Add-GolemToPlan -Plan $plan -Config $cfgUse -Name $golemName) }
        $viol = @(Test-PlanSafety $plan $cfgUse)
        Write-ConsoleLine '' 'normal'
        $dryTag = ''
        if ($script:dry) {
            $dryTag = 'DRY RUN: '
            foreach ($l in (Format-Plan -Action $ActionKey -ShardKey $ShardKey -Plan $plan -Violations $viol)) { Write-ConsoleLine $l 'normal' }
            # A window action goes on through the same confirmation and the same spawned window as a
            # real click, so a dry run exercises them; the child is told -Mode DryRun and only prints.
            if ($def.RunIn -ne 'window') { Write-ConsoleLine '   (dry run: nothing was run)' 'gray'; return }
        }
        if ($viol.Count) { foreach ($x in $viol) { Write-ConsoleLine ('REFUSED BY THE SAFETY CHECK: ' + $x) 'red' }; return }
        $refuse = @($plan | Where-Object { $_.Kind -eq 'refuse' })
        if ($refuse.Count) { foreach ($x in $refuse) { Write-ConsoleLine ($ActionKey + ': ' + $x.Text) 'amber' }; return }
        $phrase = $null
        $conf = @($plan | Where-Object { $_.Kind -eq 'confirm' })
        if ($conf.Count) {
            $confWhat = Get-ConfirmSubject $ActionKey
            if (-not (Show-TypedConfirm -Phrase $conf[0].Phrase -Text ($dryTag + $conf[0].Text) -Plan $plan -What $confWhat)) { Write-ConsoleLine ($ActionKey + ': cancelled.') 'gray'; return }
            $phrase = $conf[0].Phrase
        }
        $answeredYes = $false
        $ask = @($plan | Where-Object { $_.Kind -eq 'ask' })
        if ($ask.Count) {
            if ((Show-YesNo ($dryTag + $ask[0].Text)) -ne 'Yes') { Write-ConsoleLine ($ActionKey + ': cancelled.') 'gray'; return }
            $answeredYes = $true
        }
        # cc-P84: the Action output pane, when the console has one (a normal start; not the facts' forms) and the
        # setting says so. Get-OutputRoute keeps every interactive action in a window of its own.
        $route = 'window'
        $wanted = $false
        if ($script:PaneReady -and $script:ui.Pane) {
            $route = Get-OutputRoute -ActionKey $ActionKey -OutputIn $script:settings.OutputIn -PaneAvailable $true -PaneBusy ([bool]$script:paneJob)
            $wanted = ((Get-OutputRoute -ActionKey $ActionKey -OutputIn $script:settings.OutputIn -PaneAvailable $true) -eq 'pane')
        }
        if ($def.RunIn -eq 'window') {
            $argLine = Get-ChildArgumentLine -SelfPath $script:SelfPath -ActionKey $ActionKey -ShardKey $ShardKey -SnapshotName $SnapshotName -Snapshot $Snapshot -ConfirmLive $phrase -Yes:$answeredYes -DryRun:$script:dry -StatusFrom $script:statusFrom -StatusFromDocker:(-not $script:fromCapture) -CommitMessage $CommitMessage -PackageNotes $PackageNotes -PackageZip $PackageZip -ImageTag $ImageTag -CommitForce:($CommitForce -and $ActionKey -eq 'commit.run') -WipeAdmin $WipeAdmin -ForPane:($route -eq 'pane') -Commit $Commit -VdFile $VdFile -Body $Body -HuesFile $HuesFile
            if ($route -eq 'pane') {
                Start-PaneJob -Exe $script:cfg.PowerShell -Arguments $argLine -Title ($ActionKey + ' (' + $ShardKey + ')')
                if ($script:dry) { Write-ConsoleLine ($ActionKey + ' (' + $ShardKey + '): DRY RUN in the Action output pane. It prints the plan and runs nothing.') 'amber' }
                else { Write-ConsoleLine ($ActionKey + ' (' + $ShardKey + '): running in the Action output pane. Refresh the status when it finishes.') 'head' }
            } else {
                Start-Process -FilePath $script:cfg.PowerShell -ArgumentList $argLine | Out-Null
                if ($wanted) { Write-ConsoleLine 'The Action output pane is busy, so this one runs in a window of its own.' 'gray' }
                if ($script:dry) { Write-ConsoleLine ($ActionKey + ' (' + $ShardKey + '): DRY RUN in its own window. It prints the plan and runs nothing.') 'amber' }
                else { Write-ConsoleLine ($ActionKey + ' (' + $ShardKey + '): running in its own window. Refresh the status when it finishes.') 'head' }
            }
        } else {
            if ($script:dry) { throw 'a dry run reached the executor' }
            $tail = @($plan | Where-Object { $_.Kind -eq 'window' })
            if ($route -eq 'pane' -and $tail.Count -eq 1 -and @($plan | Where-Object { $_.Kind -notin @('window', 'say') }).Count -eq 0) {
                # A log tail: the same docker logs -f, streamed into the pane, ended by Stop.
                Write-ConsoleLine $tail[0].Text 'normal'
                Start-PaneJob -Exe $tail[0].Exe -Arguments (Format-ArgumentList $tail[0].Arguments) -Title ($ActionKey + ': ' + (Format-CommandLine $tail[0].Exe $tail[0].Arguments))
            } else {
                if ($wanted -and $tail.Count) { Write-ConsoleLine 'The Action output pane is busy, so this one opens a window of its own.' 'gray' }
                [void](Invoke-Plan -Plan $plan -Config $cfgUse -ActionKey $ActionKey -ShardKey $ShardKey -ConfirmLive $phrase -Yes:$answeredYes)
            }
            # cc-P154: the Golem's files in the background under -BackgroundRefresh, drawn when they are back.
            if ($isAutopilot) { if ($script:BackgroundRefresh) { [void](Start-ConsoleRefresh -Parts @('golem') -Queue) } else { [void](Update-AutopilotView) } }
            if ($isVpn -and -not @($plan | Where-Object { $_.Kind -eq 'refuse' }).Count) {
                # cc-P86: re-probe now and report the result (Update-VpnRecheck, on the tile's timer).
                $script:vpnRecheckSince = [datetime]::UtcNow
                $script:netResultAt = $null
                $script:vpnFactsAt = $null
                if ((Test-VpnNeedsElevation $vpnFacts) -and (Show-YesNo 'A WireGuard tunnel needs an elevated console to end. Open the Shard Console again as administrator? (This one stays open.)') -eq 'Yes') {
                    Start-Process -FilePath $script:cfg.PowerShell -Verb RunAs -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $script:SelfPath + '"') | Out-Null
                }
            }
        }
    } catch {
        Write-ConsoleLine ($ActionKey + ' failed: ' + $_.Exception.Message) 'red'
    }
}

function Get-ConsoleGolemPickRows {
    # cc-P187: the Golem tab's picker rows from this PC's golems.json and settings.json (Get-GolemPickRows); none
    # without a registry (one Golem: the picker is empty and every button is the console's own Golem's, as before).
    param($Config)
    $root = $Config.Autopilot.GolemRoot
    if (-not $root) { return @() }
    $rt = ''; $st = ''
    try { $rt = [IO.File]::ReadAllText((Join-Path $root 'golems.json')) } catch { }
    try { $st = [IO.File]::ReadAllText((Join-Path $root 'settings.json')) } catch { }
    if (-not $rt) { return @() }
    @(Get-GolemPickRows -RegistryText $rt -SettingsText $st)
}

function Get-PickedGolem {
    # cc-P187: the Golem picked on the Golem tab, or '' (no registry).
    if (-not $script:ui -or -not $script:ui.GolemPick -or $script:ui.GolemPick.SelectedIndex -lt 0) { return '' }
    $r = @($script:ui.GolemPickRows)[$script:ui.GolemPick.SelectedIndex]
    $(if ($r) { [string]$r.Name } else { '' })
}

function Update-GolemPickModel {
    # cc-P187: Wake's model followed the picked Golem's lane (Bronze: Fable). cc-P153: Poke's picker goes back to Auto for
    # any Golem: the Golem's own Get-WakeTarget picks that Golem's next prompt (its lane) and reads its Model line.
    if (-not $script:ui.GolemPick -or -not $script:ui.GolemModel -or $script:ui.GolemPick.SelectedIndex -lt 0) { return }
    $script:ui.GolemModel.SelectedIndex = 0
}

function Format-AutoModelItem {
    # cc-P153. Pure: "Auto: Opus, for P134": the model Poke the Golem will use, from the prompt that goes next (the
    # running one when a prompt runs, else Run next's pick, else the first queued). No Model line means Opus.
    param([object[]]$QueueInfo, $Current, [string]$RunNext)
    $rows = @(Get-QueueRows -QueueInfo $QueueInfo -Current $Current -RunNext $RunNext)
    if (-not $rows.Count) { return 'Auto (best for the next prompt)' }
    'Auto: ' + $rows[0].Model + ', for ' + $rows[0].Label
}

function New-ConsoleButton {
    param([string]$Text, [string]$ActionKey, [int]$Width = 230)
    $b = New-Object Windows.Forms.Button
    $b.Text = $Text
    $b.Tag = $ActionKey
    $b.Width = $Width
    $b.Height = 36
    $b.Margin = New-Object Windows.Forms.Padding(6)
    $b.Add_Click({ Invoke-ConsoleButton -ActionKey $this.Tag })
    $b
}

function New-ArtPage {
    # cc-P152: the request list (thumbnail, status, title, variants) and one button per Art.ps1 command.
    $page = New-Object Windows.Forms.TabPage('Art')
    $list = New-Object Windows.Forms.ListView
    $list.Name = 'artList'; $list.Dock = 'Fill'; $list.View = 'Details'; $list.FullRowSelect = $true; $list.MultiSelect = $false; $list.HideSelection = $false
    $imgs = New-Object Windows.Forms.ImageList
    $imgs.ImageSize = New-Object Drawing.Size(48, 48); $imgs.ColorDepth = 'Depth32Bit'
    $list.SmallImageList = $imgs
    foreach ($col in @(@('Request', 260), @('Status', 90), @('Asked by', 80), @('Variants', 300), @('Title', 420))) { [void]$list.Columns.Add($col[0], $col[1]) }
    $bar = New-Object Windows.Forms.FlowLayoutPanel
    # Two rows of buttons that wrap: an auto-sizing row would widen the tabs past the window at 900 px (cc-P116).
    $bar.Dock = 'Bottom'; $bar.WrapContents = $true; $bar.Height = 84
    $mk = {
        param([string]$Key, [string]$Text, [scriptblock]$Click)
        $b = New-Object Windows.Forms.Button
        $b.Text = $Text; $b.Name = $Key; $b.Tag = $Key; $b.AutoSize = $true; $b.Height = 32
        $b.Add_Click($Click)
        $b
    }
    $bar.Controls.AddRange(@(
        (& $mk 'art.refresh' 'Refresh' { Update-ArtList }),
        (& $mk 'art.prompt' 'Copy prompt for ChatGPT' { Invoke-ArtButton 'prompt' }),
        (& $mk 'art.generate' 'Generate (Codex)' { Invoke-ArtButton 'generate' }),
        (& $mk 'art.generateApi' 'Generate (API)' { Invoke-ArtButton 'generate-api' }),
        (& $mk 'art.import' 'Import...' { Invoke-ArtButton 'import' }),
        (& $mk 'art.approve' 'Approve' { Invoke-ArtButton 'approve' }),
        (& $mk 'art.reject' 'Reject...' { Invoke-ArtButton 'reject' }),
        (& $mk 'art.regenerate' 'Regenerate with a note...' { Invoke-ArtButton 'regenerate' }),
        (& $mk 'art.place' 'Place' { Invoke-ArtButton 'place' }),
        (& $mk 'art.review' 'Open review folder' { Invoke-ArtButton 'review' })))
    $cap = New-Caption 'Art requests from D:\UO\art\requests. Generate and Regenerate use Codex on your ChatGPT plan (or the API with your key and its monthly cap); results wait in review until you Approve, and Place puts them where they go. Prompts file requests with scripts\art\New-ArtRequest.ps1.' 820
    $cap.Dock = 'Top'
    $page.Controls.Add($list)
    $page.Controls.Add($cap)
    $page.Controls.Add($bar)
    $script:ui.ArtList = $list
    $page.Add_Enter({ if (-not $script:ui.ArtList.Items.Count) { Update-ArtList } })
    $page
}

function Update-ArtList {
    $list = $script:ui.ArtList
    if (-not $list) { return }
    $dir = Join-Path $script:cfg.ArtStore 'requests'
    $texts = @(if (Test-Path -LiteralPath $dir) { Get-ChildItem -LiteralPath $dir -Filter '*.json' -File | ForEach-Object { try { [IO.File]::ReadAllText($_.FullName) } catch { '' } } })
    $rows = Get-ArtRequestRows $texts
    $list.BeginUpdate()
    $list.Items.Clear(); $list.SmallImageList.Images.Clear()
    foreach ($r in $rows) {
        $it = New-Object Windows.Forms.ListViewItem($r.Id)
        foreach ($x in @($r.Status, $r.AskedBy, $r.Variants, $r.Title)) { [void]$it.SubItems.Add([string]$x) }
        if ($r.Thumb) {
            try {
                $src = [Drawing.Image]::FromFile($r.Thumb)
                $th = New-Object Drawing.Bitmap(48, 48)
                $g = [Drawing.Graphics]::FromImage($th); $g.InterpolationMode = 'HighQualityBicubic'
                $sc = [Math]::Min(48.0 / $src.Width, 48.0 / $src.Height)
                $g.DrawImage($src, [int]((48 - $src.Width * $sc) / 2), [int]((48 - $src.Height * $sc) / 2), [int]($src.Width * $sc), [int]($src.Height * $sc))
                $g.Dispose(); $src.Dispose()
                $list.SmallImageList.Images.Add($r.Id, $th); $it.ImageKey = $r.Id
            } catch { }
        }
        $it.ToolTipText = $r.Title
        [void]$list.Items.Add($it)
    }
    $list.EndUpdate()
    Write-ConsoleLine ('Art: ' + $rows.Count + ' request(s), ' + @($rows | Where-Object { $_.Status -eq 'generated' }).Count + ' waiting for your review.') 'gray'
}

function Get-ArtButtonCommand {
    # cc-P187: one Art tab button as Art.ps1's arguments (Invoke-ArtButton's, and the web console's Art page): prompt
    # (-Clip only in the window: the web shows the text), approve, place, reject with its note, import with its file,
    # generate and regenerate with -Go, the note and an override. '' for a button Art.ps1 does not run (review).
    param([string]$What, [string]$Id, [string]$Note, [string]$File, [string]$Backend = 'codex', [string]$Override, [switch]$Clip)
    switch ($What) {
        'prompt' { return @(@('prompt', '-Id', $Id) + $(if ($Clip) { @('-Clip') } else { @() })) }
        'approve' { return @('approve', '-Id', $Id) }
        'place' { return @('place', '-Id', $Id) }
        'reject' { return @('reject', '-Id', $Id, '-Note', $Note) }
        'import' { return @('import', '-Id', $Id, '-File', $File) }
        { $_ -in 'generate', 'generate-api', 'regenerate' } {
            $a = @($(if ($What -eq 'regenerate') { 'regenerate' } else { 'generate' }), '-Id', $Id, '-Backend', $Backend, '-Go')
            if ($Note) { $a += @('-Note', $Note) }
            if ($Override) { $a += @('-Override', $Override) }
            return $a
        }
    }
    @()
}

function Get-ArtGenerateChoice {
    # cc-P187: what Generate does with Art.ps1's estimate (Invoke-ArtButton's dialogs, and the web console's sheet):
    # Refusal (nothing to ask: not available, no key, refused and not overridable); Message (the estimate, said first);
    # Override (past the budget but overridable: only after the person types yes, once). Pure: the estimate in.
    param($Estimate, [string]$Backend, [string]$ArtScript)
    $o = [pscustomobject]@{ Refusal = ''; Message = ''; Override = $false; Title = '' }
    if (-not $Estimate -or -not $Estimate.estimate) { $o.Refusal = 'no estimate from Art.ps1'; return $o }
    if ($Backend -eq 'codex') {
        $b = $Estimate.estimate.codex.budget
        $o.Message = 'Codex, on your ChatGPT plan (no money; an image uses the limits 3 to 5 times faster than a text turn).' + "`n`n" + ((@($b.lines) | ForEach-Object { [string]$_ }) -join "`n") + "`n`n" + [string]$b.why
        $o.Title = 'Generate with Codex'
        if (-not $Estimate.estimate.codex.available) { $o.Refusal = 'Codex is not available: ' + $Estimate.estimate.codex.login; return $o }
        if (-not $b.allowed) {
            if (-not $b.overridable) { $o.Refusal = $o.Message; $o.Title = 'Generate: refused'; return $o }
            $o.Override = $true; $o.Title = 'Generate: past the budget'
        }
        return $o
    }
    $api = $Estimate.estimate.api
    $o.Message = 'OpenAI API, ' + $api.model + ' ' + $api.quality + ' ' + $api.size + ': ' + [string]$api.why
    $o.Title = 'Generate with the API'
    if (-not $api.keyStored) { $o.Refusal = 'No API key stored. Run: powershell -File ' + $ArtScript + ' set-key'; $o.Title = 'Generate (API)'; return $o }
    if (-not $api.allowed) { $o.Refusal = $o.Message; $o.Title = 'Generate (API): refused' }
    $o
}

function Invoke-ArtButton {
    # cc-P152: each button is one Art.ps1 command, run in the Action output pane. Generate asks Art.ps1 for the estimate
    # first (no -Go: nothing is generated), shows it, and only then runs with -Go; a refusal it may override asks for yes.
    param([string]$What)
    $sel = $(if ($script:ui.ArtList.SelectedItems.Count) { $script:ui.ArtList.SelectedItems[0].Text } else { '' })
    if (-not $sel) { Write-ConsoleLine 'Art: select a request first.' 'amber'; return }
    $art = $script:cfg.ArtScript
    $run = { param([string[]]$CmdArgs, [string]$Title) Start-PaneJob -Exe $script:cfg.PowerShell -Arguments (Format-ArgumentList (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $art) + $CmdArgs)) -Title $Title }
    $ask = { param([string]$Text, [string]$Title) Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.Interaction]::InputBox($Text, $Title, '') }
    switch ($What) {
        'prompt' { & $run (Get-ArtButtonCommand 'prompt' $sel -Clip) ('Art: copy prompt ' + $sel) }
        'approve' { & $run (Get-ArtButtonCommand 'approve' $sel) ('Art: approve ' + $sel) }
        'place' { & $run (Get-ArtButtonCommand 'place' $sel) ('Art: place ' + $sel) }
        'review' { $d = Join-Path (Join-Path $script:cfg.ArtStore 'review') $sel; if (Test-Path -LiteralPath $d) { Start-Process explorer.exe $d } else { Write-ConsoleLine ('Art: no review folder for ' + $sel + ' (placed requests are in D:\UO\art\archive).') 'amber' } }
        'reject' { $n = & $ask 'Why? (kept in the request''s notes)' ('Reject ' + $sel); & $run (Get-ArtButtonCommand 'reject' $sel -Note $n) ('Art: reject ' + $sel) }
        'import' {
            $dlg = New-Object Windows.Forms.OpenFileDialog
            $dlg.Filter = 'Pictures (*.png;*.webp;*.jpg)|*.png;*.webp;*.jpg;*.jpeg'
            $dlg.InitialDirectory = $script:cfg.ArtInbox
            if ($dlg.ShowDialog() -eq 'OK') { & $run (Get-ArtButtonCommand 'import' $sel -File $dlg.FileName) ('Art: import into ' + $sel) }
        }
        { $_ -in 'generate', 'generate-api', 'regenerate' } {
            $backend = $(if ($What -eq 'generate-api') { 'api' } else { 'codex' })
            $note = ''
            if ($What -eq 'regenerate') { $note = & $ask 'What should change? (for example: make the golem squatter)' ('Regenerate ' + $sel); if (-not $note) { return } }
            $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
            try { $t = (& $script:cfg.PowerShell -NoProfile -ExecutionPolicy Bypass -File $art generate -Id $sel -Backend $backend 2>$null) -join "`n" } finally { $ErrorActionPreference = $old }
            $e = $null; try { $e = ($t.Substring([Math]::Max(0, $t.IndexOf('{')))) | ConvertFrom-Json } catch { }
            if (-not $e -or -not $e.estimate) { Write-ConsoleLine ('Art: no estimate from Art.ps1: ' + $t) 'red'; return }
            $override = ''
            $c = Get-ArtGenerateChoice -Estimate $e -Backend $backend -ArtScript $art
            if ($c.Refusal) {
                if ($backend -eq 'codex' -and -not $e.estimate.codex.available) { Write-ConsoleLine ('Art: ' + $c.Refusal) 'red'; return }
                [void][Windows.Forms.MessageBox]::Show($c.Refusal, $c.Title, 'OK', 'Warning'); return
            }
            if ($c.Override) {
                if ((& $ask ($c.Message + "`n`nType yes to run it anyway, once.") $c.Title) -ne 'yes') { return }
                $override = 'yes'
            } elseif ([string][Windows.Forms.MessageBox]::Show($c.Message + "`n`nGenerate one image for " + $sel + '?', $c.Title, 'YesNo', 'Question') -ne 'Yes') { return }
            & $run (Get-ArtButtonCommand $What $sel -Note $note -Backend $backend -Override $override) ('Art: ' + $What + ' ' + $sel)
        }
    }
}

function New-Caption {
    param([string]$Text, [int]$Width = 1100, $Color)
    $l = New-Object Windows.Forms.Label
    $l.Text = $Text
    $l.AutoSize = $true
    $l.MaximumSize = New-Object Drawing.Size($Width, 0)
    $l.Margin = New-Object Windows.Forms.Padding(6, 6, 6, 2)
    if ($Color) { $l.ForeColor = $Color }
    $l
}

function New-ButtonPage {
    param([string]$Title, [string]$Caption, [object[]]$Buttons, $CaptionColor)
    $page = New-Object Windows.Forms.TabPage($Title)
    $flow = New-Object Windows.Forms.FlowLayoutPanel
    $flow.Dock = 'Fill'
    $flow.AutoScroll = $true
    $flow.FlowDirection = 'LeftToRight'
    $flow.WrapContents = $true
    $cap = New-Caption $Caption 1100 $CaptionColor
    $flow.Controls.Add($cap)
    $flow.SetFlowBreak($cap, $true)
    foreach ($b in $Buttons) { $flow.Controls.Add($b) }
    $page.Controls.Add($flow)
    $page
}

function New-WorldSetupTable {
    # The World setup tab's table: every world-changing command registered in server/customizations, grouped, each with a
    # tick, a Copy button and why. cc-P154: its own function so the console can build it on first open (Show-WorldSetupPage).
    # -Commands: the registrations read already (the console reads them in the background); unset, read here.
    param($Config, $Commands)
    $worldTable = New-Object Windows.Forms.TableLayoutPanel
    $worldTable.Dock = 'Fill'
    $worldTable.AutoScroll = $true
    $worldTable.ColumnCount = 3
    [void]$worldTable.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('AutoSize')))
    [void]$worldTable.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('AutoSize')))
    [void]$worldTable.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent', 100)))
    $worldCmds = @()
    if ($null -ne $Commands) { $worldCmds = @($Commands) }
    else { try { $worldCmds = @(ConvertFrom-CommandSources (Get-CommandSourceFiles $Config.Customizations)) } catch { } }
    $head = New-Caption ('In-game commands to paste into a client by hand: the console cannot run them, and cannot see whether this world has had them. Read from the code (' + $worldCmds.Count + ' registrations; only those that change a world are shown). Check which shard your client is on before you paste. A dry run is its own row just above the command it previews; the ticks are for this session only.') 1100
    $worldTable.Controls.Add($head, 0, 0)
    $worldTable.SetColumnSpan($head, 3)
    $row = 1
    $copyClick = { [Windows.Forms.Clipboard]::SetText([string]$this.Tag); Write-ConsoleLine ('copied: ' + ([string]$this.Tag).Trim()) 'gray' }
    # cc-P154: laid out once, after every row is in. Each Controls.Add on a laid-out table laid the whole table out again,
    # and this loop alone took 26 of the 29 s the window took to build (measured, notes/cc-P154-console-responsive.md).
    $worldTable.SuspendLayout()
    foreach ($group in @(Get-WorldCommandGroups $worldCmds)) {
        $title = New-Object Windows.Forms.Label
        $title.Text = $group.Title
        $title.AutoSize = $true
        $title.Font = New-Object Drawing.Font($worldTable.Font, [Drawing.FontStyle]::Bold)
        $title.Margin = New-Object Windows.Forms.Padding(6, 14, 3, 2)
        if ($group.Key -eq 'unclassified' -and @($group.Items).Count) { $title.ForeColor = [Drawing.Color]::Firebrick }
        $worldTable.Controls.Add($title, 0, $row)
        $worldTable.SetColumnSpan($title, 3)
        $row++
        $src = New-Caption $group.Source 760 ([Drawing.Color]::DimGray)
        if ($group.CopyAll -and @($group.Items).Count) {
            $all = New-Object Windows.Forms.Button
            $all.Text = 'Copy group'
            $all.Name = 'worldCopyGroup'
            $all.Width = 90
            $all.Tag = Get-GroupClipboardText $group
            $all.Add_Click({ [Windows.Forms.Clipboard]::SetText([string]$this.Tag); Write-ConsoleLine ('copied a group of ' + @(([string]$this.Tag).Trim() -split "`r`n").Count + ' commands') 'gray' })
            $worldTable.Controls.Add($all, 1, $row)
            $worldTable.Controls.Add($src, 2, $row)
        } else {
            $worldTable.Controls.Add($src, 0, $row)
            $worldTable.SetColumnSpan($src, 3)
        }
        $row++
        if (-not @($group.Items).Count) {
            $none = New-Caption '(none)' 300 ([Drawing.Color]::DimGray)
            $worldTable.Controls.Add($none, 0, $row)
            $row++
        }
        foreach ($item in $group.Items) {
            $cb = New-Object Windows.Forms.CheckBox
            $cb.Text = $item.Command
            $cb.Name = 'worldTick'
            $cb.AutoSize = $true
            $cb.Font = $script:MonoFont
            $cb.Margin = New-Object Windows.Forms.Padding(6, 5, 3, 3)
            $copy = New-Object Windows.Forms.Button
            $copy.Text = 'Copy'
            $copy.Name = 'worldCopy'
            $copy.Width = 60
            $copy.Tag = $item.Command
            $copy.Add_Click($copyClick)
            $worldTable.Controls.Add($cb, 0, $row)
            $worldTable.Controls.Add($copy, 1, $row)
            $why = New-Object Windows.Forms.Label
            $why.Text = $item.Text
            $why.AutoSize = $true
            $why.MaximumSize = New-Object Drawing.Size(760, 0)
            $why.ForeColor = [Drawing.Color]::DimGray
            if ($item.Warn) { $why.ForeColor = [Drawing.Color]::DarkRed }
            $why.Margin = New-Object Windows.Forms.Padding(6, 8, 3, 3)
            $worldTable.Controls.Add($why, 2, $row)
            $row++
        }
    }
    $worldTable.ResumeLayout()
    $worldTable
}

function Show-WorldSetupPage {
    # cc-P154: builds the World setup page the first time it is opened (-BackgroundRefresh), in the look and with the
    # tooltips the rest of the window was given at its start.
    # The registrations come from the background read started with the window (Start-WorldCommandsRead); until they are
    # back the page keeps saying loading, and they build it when they arrive with the page open.
    if (-not $script:worldPending -or -not $script:ui.WorldPage -or $null -eq $script:worldCmds) { return }
    $script:worldPending = $false
    $pg = $script:ui.WorldPage
    $tbl = New-WorldSetupTable $script:cfg $script:worldCmds
    # Laid out once: themed while suspended (each themed font would otherwise lay the whole table out again), and drawn
    # once (Suspend-WindowDraw).
    $held = Suspend-WindowDraw
    $tbl.SuspendLayout()
    $pg.SuspendLayout()
    $pg.Controls.Clear()
    $pg.Controls.Add($tbl)
    Set-ControlTheme $pg $script:theme $script:themeImages
    if ($script:ui.ToolTip) { Set-ConsoleTooltips $pg (Get-ConsoleTooltips) $script:ui.ToolTip }
    $tbl.ResumeLayout()
    $pg.ResumeLayout()
    Update-CaptionFit
    if ($held) { Resume-WindowDraw }
}

function Start-WorldCommandsRead {
    # cc-P154: the World setup tab's registrations (every .cs under server/customizations, parsed) read in the background
    # when the window opens; the tab is built from them when it is first opened.
    $then = {
        param($Data, $Job)
        $script:worldCmds = $(if ($Data) { @($Data.Commands) } else { @() })
        if ($script:ui.Tabs -and $script:ui.Tabs.SelectedTab -eq $script:ui.WorldPage) { Show-WorldSetupPage }
    }
    [void](Start-ConsoleJob -Slot 'world' -Script 'param($root) $c = @(); try { $c = @(ConvertFrom-CommandSources (Get-CommandSourceFiles $root)) } catch { }; [pscustomobject]@{ Commands = $c }' -Arguments @($script:cfg.Customizations) -Then $then)
}

function New-ConsoleForm {
    # cc-P84: -Theme is the look (Plain unless the entry passes the setting; Plain is the console as it was) and
    # -Pane builds the Action output pane and lets actions use it. The facts build forms without either.
    # cc-P154: -BackgroundRefresh (the console's own start) reads the status, the lights, the Golem and a click's facts in
    # runspaces of their own and only draws here, and builds the World setup tab when it is first opened.
    param($Config, [switch]$DryRun, [string]$StatusFrom, [ValidateSet('UO', 'Plain', 'Enhanced')][string]$Theme = 'Plain', [switch]$Pane, $Settings, [switch]$RestoreWindow,
        [switch]$BackgroundRefresh)
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [Windows.Forms.Application]::EnableVisualStyles()
    $script:cfg = $Config
    $script:dry = [bool]$DryRun
    $script:fromCapture = [bool]$DryRun
    $script:statusFrom = $StatusFrom
    $script:lastState = $null
    $script:ui = @{}
    $script:BackgroundRefresh = [bool]$BackgroundRefresh
    $script:refreshQueued = $null
    $script:RefreshSkipped = 0
    $script:packageFactsAt = $null
    $script:commitAskedAt = $null
    $script:playTestGlance = $null
    $script:autopilot = $null
    $script:worldPending = $false
    $script:theme = Get-ConsoleTheme $Theme
    # cc-P132: the art folder, read once; then the window's backgrounds (gumps, drawn textures, or art).
    $script:artCache = @{}
    $script:art = Get-ConsoleArt $Config
    $script:themeImages = Get-ThemeImages $Config $script:theme
    $script:lights = $null
    $script:settings = $Settings
    if (-not $script:settings) { $script:settings = ConvertFrom-ConsoleSettings $null }
    $script:PaneReady = [bool]$Pane
    $script:paneJob = $null
    $script:MonoFont = Get-ThemeFont 'Mono'
    $script:BoldFont = Get-ThemeFont 'Mono' ([Drawing.FontStyle]::Bold)

    $form = New-Object Windows.Forms.Form
    # cc-P113: the shortcut's icon in the title bar, the taskbar and Alt+Tab.
    Set-ConsoleIcon $form $Config
    $form.Text = 'Shard Console'
    if ($DryRun) { $form.Text = 'Shard Console - DRY RUN (buttons print, status from a capture file)' }
    $form.Size = New-Object Drawing.Size(1200, 1000)
    $form.MinimumSize = New-Object Drawing.Size(900, 700)
    $form.StartPosition = 'CenterScreen'
    # cc-P132: laid out for 96 DPI and scaled by WinForms at 125% and 150% (the process is made DPI aware at the entry).
    $form.AutoScaleDimensions = New-Object Drawing.SizeF(96, 96)
    $form.AutoScaleMode = 'Dpi'
    $form.Font = Get-ThemeFont 'Ui'

    $script:ui.Form = $form

    $grid = New-Object Windows.Forms.TableLayoutPanel
    $grid.Dock = 'Fill'
    $grid.RowCount = 4
    $grid.ColumnCount = 1
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Absolute', 104)))
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', 48)))
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', 32)))
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', 20)))
    # cc-P87, UO only: the one column is the window's width. Without a style it grows to its widest caption
    # (1,100 px), so below that the tiles' third column and the bars ran off the right edge. Plain is left as it was.
    if ($script:theme.Chrome) { [void]$grid.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent', 100))) }

    # at a glance: what is running, big enough to read from across the room
    $glance = New-Object Windows.Forms.TableLayoutPanel
    $glance.Dock = 'Fill'
    $glance.RowCount = 1
    $glance.ColumnCount = 7
    $glance.Margin = New-Object Windows.Forms.Padding(0)
    foreach ($i in 1..7) { [void]$glance.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent', 14.28))) }
    # cc-P87: in UO the tiles wrap to two rows rather than clip (Update-TileFit); Plain keeps one row of six.
    $script:ui.Glance = $glance
    $script:ui.Grid = $grid
    $glance.Add_Resize({ Update-TileFit })
    $form.Add_Resize({ Update-CaptionFit })
    $form.Add_Shown({ Update-TileFit; Update-CaptionFit })
    $script:ui.Tiles = @()
    $col = 0
    foreach ($title in @('LIVE', 'TEST', 'STATUS PUBLISHER')) {
        $tile = New-StatusTile $title
        $glance.Controls.Add($tile.Panel, $col, 0)
        $script:ui.Tiles += $tile
        $col++
    }
    # cc-P52 Part H: public DNS and VPN. Drawn by Update-NetworkTile, not by the status refresh.
    $script:ui.NetTile = New-StatusTile 'PUBLIC DNS'
    # cc-P86: Disconnect VPN, under the tile's line, as P83's links sit in the PLAY TESTS tile.
    $vpnBar = New-Object Windows.Forms.FlowLayoutPanel
    $vpnBar.Dock = 'Bottom'
    $vpnBar.Height = 30
    $vpnBar.Margin = New-Object Windows.Forms.Padding(0)
    $vpnBtn = New-ConsoleButton 'Disconnect VPN' 'vpn.disconnect' 60
    $vpnBtn.AutoSize = $true
    $vpnBtn.AutoSizeMode = 'GrowAndShrink'
    $vpnBtn.Font = New-Object Drawing.Font($form.Font.FontFamily, 8.25)
    $vpnBtn.Height = 26
    $vpnBtn.Margin = New-Object Windows.Forms.Padding(4, 0, 0, 2)
    $vpnBtn.Enabled = $false
    $vpnBar.Controls.Add($vpnBtn)
    $script:ui.NetTile.Panel.Controls.Add($vpnBar)
    $script:ui.NetTile.Detail.BringToFront()
    $script:ui.VpnButtons = @($vpnBtn)
    $script:vpnFacts = $null; $script:vpnFactsAt = $null; $script:vpnRecheckSince = $null
    $glance.Controls.Add($script:ui.NetTile.Panel, $col, 0)
    $col++
    $script:netProbe = $null; $script:netResult = $null; $script:netResultAt = $null
    # cc-P74: the Golem (AUTOPILOT until cc-P99). Drawn by Update-AutopilotView on every status refresh.
    # cc-P132: with the Golem figure, lit by its state.
    $script:ui.AutopilotTile = New-StatusTile 'GOLEM' -Figure
    $glance.Controls.Add($script:ui.AutopilotTile.Panel, $col, 0)
    $col++
    # cc-P83: PLAY TESTS, with the two links beside it (inside the tile, under its line).
    $script:ui.PlayTestTile = New-StatusTile 'PLAY TESTS'
    $linkBar = New-Object Windows.Forms.FlowLayoutPanel
    $linkBar.Dock = 'Bottom'
    $linkBar.Height = 30
    $linkBar.Margin = New-Object Windows.Forms.Padding(0)
    $script:ui.LinkButtons = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Links' })) {
        # cc-P85: three now (Status beside Play tests and Dashboard), so each is sized to its word to fit the tile.
        $lb = New-ConsoleButton $a.Label $a.Key 60
        $lb.AutoSize = $true
        $lb.AutoSizeMode = 'GrowAndShrink'
        $lb.Font = New-Object Drawing.Font($form.Font.FontFamily, 8.25)
        $lb.Height = 26
        $lb.Margin = New-Object Windows.Forms.Padding(4, 0, 0, 2)
        $linkBar.Controls.Add($lb)
        $script:ui.LinkButtons += $lb
    }
    $script:ui.PlayTestTile.Panel.Controls.Add($linkBar)
    $script:ui.PlayTestTile.Detail.BringToFront()
    $glance.Controls.Add($script:ui.PlayTestTile.Panel, $col, 0)
    $col++
    # cc-P101: WEBSITES. Drawn by Update-WebsitesTile on the 1 s timer, from a check that runs every 60 s.
    $script:ui.WebsitesTile = New-StatusTile 'WEBSITES'
    $glance.Controls.Add($script:ui.WebsitesTile.Panel, $col, 0)
    $script:webProbe = $null; $script:webResults = $null; $script:webResultAt = $null

    # status
    $statusGroup = New-Object Windows.Forms.GroupBox
    $statusGroup.Text = 'Status'
    $statusGroup.Dock = 'Fill'
    $bar = New-Object Windows.Forms.FlowLayoutPanel
    $bar.Dock = 'Top'
    $bar.Height = 34
    $refresh = New-Object Windows.Forms.Button
    $refresh.Text = 'Refresh'
    $refresh.Name = 'refresh'
    $refresh.Width = 100
    $refresh.Add_Click({ Update-ConsoleStatus -Queue })
    $auto = New-Object Windows.Forms.CheckBox
    $auto.Text = 'refresh every 30 s'
    $auto.Name = 'autoRefresh'
    $auto.AutoSize = $true
    $auto.Checked = -not $DryRun
    $auto.Margin = New-Object Windows.Forms.Padding(12, 8, 3, 3)
    $dryBox = New-Object Windows.Forms.CheckBox
    $dryBox.Text = 'DRY RUN: buttons print, nothing runs'
    $dryBox.Name = 'dryRun'
    $dryBox.AutoSize = $true
    $dryBox.Font = Get-ThemeFont 'Ui' ([Drawing.FontStyle]::Bold)
    $dryBox.Margin = New-Object Windows.Forms.Padding(24, 8, 3, 3)
    if ($DryRun) {
        # Launched with -DryRun the status is a capture file, so this stays on.
        $dryBox.Text = 'DRY RUN (locked on: the status is from a capture file)'
        $dryBox.Enabled = $false
    }
    $dryBox.Add_CheckedChanged({ Set-ConsoleDryRun $this.Checked })
    $bar.Controls.AddRange(@($refresh, $auto, $dryBox))
    # cc-P84: the look and where actions show their output, remembered in SettingsFile. Only with the pane (a normal
    # start): the facts' forms have neither.
    if ($Pane) {
        $lookLabel = New-Object Windows.Forms.Label
        $lookLabel.Text = 'Look:'
        $lookLabel.AutoSize = $true
        $lookLabel.Margin = New-Object Windows.Forms.Padding(24, 9, 0, 3)
        $look = New-Object Windows.Forms.ComboBox
        $look.Name = 'theme'
        $look.DropDownStyle = 'DropDownList'
        $look.Width = 92
        $look.Margin = New-Object Windows.Forms.Padding(3, 6, 3, 3)
        [void]$look.Items.AddRange(@('Enhanced', 'UO', 'Plain'))
        $look.SelectedItem = $script:theme.Name
        $look.Add_SelectedIndexChanged({
            $script:settings.Theme = [string]$this.SelectedItem
            if (Save-ConsoleSettings $script:cfg $script:settings) { Write-ConsoleLine ('Look set to ' + $script:settings.Theme + '. It takes effect the next time the console opens.') 'gray' }
        })
        $own = New-Object Windows.Forms.CheckBox
        $own.Name = 'outputInWindows'
        $own.Text = 'actions in their own windows'
        $own.AutoSize = $true
        $own.Checked = ($script:settings.OutputIn -eq 'window')
        $own.Margin = New-Object Windows.Forms.Padding(12, 8, 3, 3)
        $own.Add_CheckedChanged({
            $script:settings.OutputIn = 'pane'
            if ($this.Checked) { $script:settings.OutputIn = 'window' }
            [void](Save-ConsoleSettings $script:cfg $script:settings)
            if ($this.Checked) { Write-ConsoleLine 'Actions now run in windows of their own.' 'gray' }
            else { Write-ConsoleLine 'Non-interactive actions now stream into the Action output pane; the interactive ones keep their own windows.' 'gray' }
        })
        $bar.Controls.AddRange(@($lookLabel, $look, $own))
        $script:ui.Look = $look
        $script:ui.OwnWindows = $own
    }
    # cc-P87, UO only: the bar wraps onto a second line, and grows to show it, when the window is too narrow for it.
    if ($script:theme.Chrome) { $bar.AutoSize = $true; $bar.AutoSizeMode = 'GrowAndShrink'; $bar.MinimumSize = New-Object Drawing.Size(0, 34) }
    $status = New-Object Windows.Forms.RichTextBox
    $status.Name = 'statusPanel'   # cc-P153: for its tooltip
    $status.Dock = 'Fill'
    $status.ReadOnly = $true
    $status.BackColor = [Drawing.Color]::White
    $status.Font = $script:MonoFont
    $status.WordWrap = $true
    $status.DetectUrls = $false
    $statusGroup.Controls.Add($status)
    $statusGroup.Controls.Add($bar)
    $script:ui.Status = $status
    $script:ui.Auto = $auto
    $script:ui.DryBox = $dryBox

    # tabs
    $tabs = New-Object Windows.Forms.TabControl
    $tabs.Dock = 'Fill'
    $script:ui.Tabs = $tabs

    $testCaption = 'The throwaway test shard (sl-modernuo-test, port 2594, its own save). No confirmation except Deploy and Wipe: it is throwaway, and nothing here touches live. Rebuild is the only button that builds, through every test gate. START FRESH and WIPE TEST ACCOUNTS are different: Start fresh starts a NEW WORLD, with the LIVE accounts copied in; Wipe test accounts keeps the WORLD, deletes every test account and copies nothing from live. Hover a button for what it does.'
    # cc-P108: while live is retired, the caption says nothing about it but why Start fresh is greyed.
    if (Test-LiveRetired $Config) { $testCaption = 'The throwaway test shard (sl-modernuo-test, port 2594, its own save). No confirmation except Deploy and Wipe: it is throwaway. Rebuild is the only button that builds, through every test gate. Start fresh is greyed: it copied its accounts from the live save, and live is retired. WIPE TEST ACCOUNTS keeps the WORLD, deletes every test account and makes one new admin. Hover a button for what it does.' }
    $testPage = New-ButtonPage 'Test shard' $testCaption @(
        (New-ConsoleButton 'Start' 'test.start'),
        (New-ConsoleButton 'Start fresh (new world, live accounts)' 'test.fresh' 260),
        (New-ConsoleButton 'Wipe test accounts (keep world)...' 'test.wipe' 260),
        (New-ConsoleButton 'Stop' 'test.stop'),
        (New-ConsoleButton 'Rebuild through the gates and start' 'test.rebuild' 300),
        (New-ConsoleButton 'Deploy built image...' 'test.deploy'),
        (New-ConsoleButton 'Tail the log' 'test.tail'),
        (New-ConsoleButton 'Open a client' 'test.client')
    )
    # cc-P132: the rebuild light, right after the Rebuild button (Update-Lights fills it on every refresh).
    $script:ui.RebuildLight = New-LightLabel 'rebuild.light'
    $testFlow = $testPage.Controls[0]
    $testFlow.Controls.Add($script:ui.RebuildLight.Row)
    $rbBtn = @($testFlow.Controls | Where-Object { $_.Tag -eq 'test.rebuild' })
    if ($rbBtn.Count) { $testFlow.Controls.SetChildIndex($script:ui.RebuildLight.Row, $testFlow.Controls.GetChildIndex($rbBtn[0]) + 1) }

    # cc-P96: Fix test shard, beside the Test shard tab.
    $fixPage = New-ButtonPage 'Fix test shard' 'When the test shard will not start. DIAGNOSE first: it reads the container, its health, its image and commit, and any crash, and names a known bug. RESTART restarts it as it is. ROLL BACK backs up the test save, then runs the newest good image (each healthy start tags one, sl-modernuo:good-<commit>). REBUILD builds a commit you pick through every test gate. PRUNE removes old image tags. Each of the four asks for a phrase; none touches live.' @(
        (New-ConsoleButton 'Diagnose' 'fix.diagnose'),
        (New-ConsoleButton 'Restart' 'fix.restart'),
        (New-ConsoleButton 'Roll back to last good' 'fix.rollback'),
        (New-ConsoleButton 'Rebuild a commit...' 'fix.rebuild'),
        (New-ConsoleButton 'Prune images' 'fix.prune')
    )

    # cc-P106: Shard question, for both shards: what the picked shard is waiting on, and one button per answer.
    $qPage = New-Object Windows.Forms.TabPage('Shard question')
    $qTop = New-Object Windows.Forms.FlowLayoutPanel
    $qTop.Dock = 'Top'
    $qTop.Height = 32
    $qLive = New-Object Windows.Forms.RadioButton
    $qLive.Text = 'LIVE shard'
    $qLive.Name = 'qLive'
    $qLive.AutoSize = $true
    $qLive.ForeColor = [Drawing.Color]::Firebrick
    $qTest = New-Object Windows.Forms.RadioButton
    $qTest.Text = 'Test shard'
    $qTest.Name = 'qTest'
    $qTest.AutoSize = $true
    $qLive.Checked = $true
    # cc-P108: the test shard is the one picked while live is retired (its choice is greyed by Set-LiveRetiredLook).
    if (Test-LiveRetired $Config) { $qTest.Checked = $true }
    $qLive.Add_CheckedChanged({ Update-QuestionPanel $script:lastState })
    $qTop.Controls.AddRange(@($qLive, $qTest))
    $qText = New-Object Windows.Forms.TextBox
    $qText.Name = 'qText'
    $qText.Multiline = $true
    $qText.ReadOnly = $true
    $qText.ScrollBars = 'Vertical'
    $qText.Dock = 'Fill'
    $qText.Font = New-Object Drawing.Font('Consolas', 9)
    $qButtons = New-Object Windows.Forms.FlowLayoutPanel
    $qButtons.Dock = 'Bottom'
    $qButtons.Height = 52
    # cc-P116: the six answers wrap onto a second row, and the row grows to show it, when the page is narrow.
    $qButtons.AutoSize = $true; $qButtons.AutoSizeMode = 'GrowAndShrink'
    $script:ui.QuestionButtons = @()
    foreach ($pair in @(@('Show the question', 'question.show'), @('Answer n', 'question.n'), @('Answer y (deletes)', 'question.y'), @('Answer a (deletes all)', 'question.a'), @('Press Enter', 'question.enter'), @('Stop the shard', 'question.stop'))) {
        $qb = New-Object Windows.Forms.Button
        $qb.Text = $pair[0]
        $qb.Tag = $pair[1]
        $qb.Width = 180
        $qb.Height = 36
        $qb.Margin = New-Object Windows.Forms.Padding(6)
        $qb.Add_Click({ Invoke-ConsoleButton -ActionKey $this.Tag -ShardKey (Get-SelectedQuestionShard) })
        $qButtons.Controls.Add($qb)
        $script:ui.QuestionButtons += $qb
    }
    $qPage.Controls.Add($qText)
    $qPage.Controls.Add($qButtons)
    $qPage.Controls.Add($qTop)
    $script:ui.QuestionLive = $qLive
    $script:ui.QuestionText = $qText

    # snapshots
    $snapPage = New-Object Windows.Forms.TabPage('Snapshots')
    $snapTop = New-Object Windows.Forms.FlowLayoutPanel
    $snapTop.Dock = 'Top'
    $snapTop.Height = 32
    $rTest = New-Object Windows.Forms.RadioButton
    $rTest.Text = 'Test shard'
    $rTest.Name = 'snapTest'
    $rTest.Checked = $true
    $rTest.AutoSize = $true
    $rLive = New-Object Windows.Forms.RadioButton
    $rLive.Text = 'LIVE shard (every action asks you to type a phrase)'
    $rLive.Name = 'snapLive'
    $rLive.AutoSize = $true
    $rLive.ForeColor = [Drawing.Color]::Firebrick
    $rTest.Add_CheckedChanged({ Update-SnapshotList })
    $snapPath = New-Object Windows.Forms.Label
    $snapPath.AutoSize = $true
    $snapPath.ForeColor = [Drawing.Color]::DimGray
    $snapPath.Margin = New-Object Windows.Forms.Padding(12, 6, 3, 3)
    $snapTop.Controls.AddRange(@($rTest, $rLive, $snapPath))
    $list = New-Object Windows.Forms.ListView
    $list.Dock = 'Fill'
    $list.Name = 'snapList'
    $list.View = 'Details'
    $list.FullRowSelect = $true
    $list.MultiSelect = $false
    $list.HideSelection = $false
    foreach ($col in @(@('Snapshot', 380), @('Taken', 150), @('Size', 90), @('Files', 60), @('Accounts', 75), @('Kind', 100))) {
        [void]$list.Columns.Add($col[0], $col[1])
    }
    $snapBottom = New-Object Windows.Forms.FlowLayoutPanel
    $snapBottom.Dock = 'Bottom'
    $snapBottom.Height = 46
    $snapBottom.AutoSize = $true; $snapBottom.AutoSizeMode = 'GrowAndShrink'  # cc-P116, as the Shard question buttons
    $nameLabel = New-Object Windows.Forms.Label
    $nameLabel.Text = 'Name:'
    $nameLabel.AutoSize = $true
    $nameLabel.Margin = New-Object Windows.Forms.Padding(6, 14, 3, 3)
    $nameBox = New-Object Windows.Forms.TextBox
    $nameBox.Name = 'snapName'
    $nameBox.Width = 220
    $nameBox.Margin = New-Object Windows.Forms.Padding(3, 10, 3, 3)
    $create = New-Object Windows.Forms.Button
    $create.Text = 'Create named snapshot'
    $create.Name = 'snapshot.create'
    $create.Width = 170
    $create.Height = 32
    $create.Add_Click({ Invoke-ConsoleButton -ActionKey 'snapshot.create' -ShardKey (Get-SelectedSnapshotShard) -SnapshotName $script:ui.SnapName.Text.Trim() })
    $restore = New-Object Windows.Forms.Button
    $restore.Text = 'Restore selected'
    $restore.Name = 'snapshot.restore'
    $restore.Width = 150
    $restore.Height = 32
    $restore.Margin = New-Object Windows.Forms.Padding(40, 3, 3, 3)
    $restore.Add_Click({
        $sel = $null
        if ($script:ui.SnapList.SelectedItems.Count) { $sel = $script:ui.SnapList.SelectedItems[0].Text }
        Invoke-ConsoleButton -ActionKey 'snapshot.restore' -ShardKey (Get-SelectedSnapshotShard) -Snapshot $sel
    })
    $delete = New-Object Windows.Forms.Button
    $delete.Text = 'Delete selected'
    $delete.Name = 'snapshot.delete'
    $delete.Width = 130
    $delete.Height = 32
    $delete.Margin = New-Object Windows.Forms.Padding(40, 3, 3, 3)
    $delete.Add_Click({
        $sel = $null
        if ($script:ui.SnapList.SelectedItems.Count) { $sel = $script:ui.SnapList.SelectedItems[0].Text }
        Invoke-ConsoleButton -ActionKey 'snapshot.delete' -ShardKey (Get-SelectedSnapshotShard) -Snapshot $sel
    })
    $listBtn = New-Object Windows.Forms.Button
    $listBtn.Text = 'List'
    $listBtn.Name = 'snapshot.list'
    $listBtn.Width = 80
    $listBtn.Height = 32
    $listBtn.Add_Click({ Invoke-ConsoleButton -ActionKey 'snapshot.list' -ShardKey (Get-SelectedSnapshotShard) })
    $snapBottom.Controls.AddRange(@($nameLabel, $nameBox, $create, $restore, $delete, $listBtn))
    $snapNote = New-Caption 'A snapshot is a copy of the Saves folder, accounts included. Creating or restoring one stops the container first if it is running and starts it again afterwards. A restore snapshots the current save first (PRE-RESTORE) and moves it aside; nothing is deleted except a snapshot you delete here.' 1100
    $snapNote.Dock = 'Bottom'
    $snapPage.Controls.Add($list)
    $snapPage.Controls.Add($snapTop)
    $snapPage.Controls.Add($snapNote)
    $snapPage.Controls.Add($snapBottom)
    $script:ui.SnapList = $list
    $script:ui.SnapLive = $rLive
    $script:ui.SnapName = $nameBox
    $script:ui.SnapPath = $snapPath

    # world setup
    $worldPage = New-Object Windows.Forms.TabPage('World setup')
    # cc-P154: under -BackgroundRefresh (the console's own start) the page is built the first time it is opened, so the
    # window does not wait for 274 .cs files to be read and the table laid out; until then it says loading.
    if ($BackgroundRefresh) {
        $worldWait = New-Caption 'loading the world commands...' 600 ([Drawing.Color]::DimGray)
        $worldWait.Name = 'worldLoading'
        $worldPage.Controls.Add($worldWait)
        $script:worldPending = $true
        $script:worldCmds = $null
    } else { $worldPage.Controls.Add((New-WorldSetupTable $Config)) }

    # cc-P135: Map and Art. The art checks run validate-art.py and show its output in the Action output pane (or a
    # window); the map buttons print an in-game command's syntax. The boxes feed Check a .vd and Compare a hues.mul.
    $artPage = New-Object Windows.Forms.TabPage('Map and Art')
    $artFlow = New-Object Windows.Forms.FlowLayoutPanel
    $artFlow.Dock = 'Fill'
    $artFlow.AutoScroll = $true
    $artFlow.FlowDirection = 'LeftToRight'
    $artFlow.WrapContents = $true
    $artCap = New-Caption ('ART CHECKS run scripts\validate-art.py (read only) and show its output in the Action output pane. Check the art registry and Body table need nothing. Check a .vd takes the file below and a body number (empty: a free body and anim7 slot are proposed). Compare a hues.mul takes the other hues.mul and compares it with ' + $Config.OurHues + '. Exit 1 in the pane means it found problems to read, not that it failed. MAP COMMANDS print the syntax of the six in-game map checks (GameMaster), which are typed in a client: the console cannot run them.') 1100
    $artFlow.Controls.Add($artCap)
    $artFlow.SetFlowBreak($artCap, $true)
    $vdBox = New-Object Windows.Forms.TextBox
    $vdBox.Name = 'art.vdfile'
    $vdBox.Width = 520
    $vdBox.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    $vdBrowse = New-Object Windows.Forms.Button
    $vdBrowse.Text = 'Browse .vd'
    $vdBrowse.Name = 'art.browsevd'
    $vdBrowse.Width = 110
    $vdBrowse.Height = 28
    $vdBrowse.Margin = New-Object Windows.Forms.Padding(6, 10, 6, 6)
    $vdBrowse.Add_Click({ $f = Show-FilePicker 'Animation file (*.vd)|*.vd|All files (*.*)|*.*' 'Pick a .vd file'; if ($f) { $script:ui.VdFileBox.Text = $f } })
    $bodyLabel = New-Caption 'body' 60
    $bodyLabel.Margin = New-Object Windows.Forms.Padding(6, 16, 0, 6)
    $bodyBox = New-Object Windows.Forms.TextBox
    $bodyBox.Name = 'art.body'
    $bodyBox.Width = 70
    $bodyBox.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    $artFlow.Controls.AddRange(@($vdBox, $vdBrowse, $bodyLabel, $bodyBox))
    $artFlow.SetFlowBreak($bodyBox, $true)
    $huesBox = New-Object Windows.Forms.TextBox
    $huesBox.Name = 'art.huesfile'
    $huesBox.Width = 520
    $huesBox.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    $huesBrowse = New-Object Windows.Forms.Button
    $huesBrowse.Text = 'Browse hues.mul'
    $huesBrowse.Name = 'art.browsehues'
    $huesBrowse.Width = 130
    $huesBrowse.Height = 28
    $huesBrowse.Margin = New-Object Windows.Forms.Padding(6, 10, 6, 6)
    $huesBrowse.Add_Click({ $f = Show-FilePicker 'Hues file (*.mul)|*.mul|All files (*.*)|*.*' 'Pick the other hues.mul'; if ($f) { $script:ui.HuesFileBox.Text = $f } })
    $artFlow.Controls.AddRange(@($huesBox, $huesBrowse))
    $artFlow.SetFlowBreak($huesBrowse, $true)
    $artBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Map and Art' })) {
        $b = New-MapArtButton $a.Label $a.Key 230
        $artBtns += $b
        $artFlow.Controls.Add($b)
    }
    $artFlow.SetFlowBreak($artBtns[-1], $true)
    $mapHead = New-Caption 'MAP COMMANDS (in-game, GameMaster): each button prints the syntax and what the command says.' 1100 ([Drawing.Color]::DimGray)
    $artFlow.Controls.Add($mapHead)
    $artFlow.SetFlowBreak($mapHead, $true)
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Map commands' })) {
        $b = New-ConsoleButton $a.Label $a.Key 170
        $artBtns += $b
        $artFlow.Controls.Add($b)
    }
    $artPage.Controls.Add($artFlow)
    $script:ui.MapArtPage = $artPage
    $script:ui.MapArtButtons = $artBtns
    $script:ui.VdFileBox = $vdBox
    $script:ui.BodyBox = $bodyBox
    $script:ui.HuesFileBox = $huesBox

    $diagPage = New-ButtonPage 'Diagnostics' ('Two read-only health checks. Each writes one timestamped file in ' + $Config.DiagFolder + '. The shard check reads the TEST container only. Disconnect VPN turns off a VPN that is making sl-ddns publish the wrong address.') @(
        (New-ConsoleButton 'Check Docker' 'diag.docker'),
        (New-ConsoleButton 'Check the test shard' 'diag.shard'),
        (New-ConsoleButton 'Open the folder they write to' 'diag.folder'),
        (New-ConsoleButton 'Disconnect VPN' 'vpn.disconnect')
    )
    # cc-P86: greyed until a VPN is up, like the one in the PUBLIC DNS tile.
    $diagVpn = @($diagPage.Controls[0].Controls | Where-Object { $_.Tag -eq 'vpn.disconnect' })[0]
    $diagVpn.Enabled = $false
    $script:ui.VpnButtons += $diagVpn

    $liveCaption = 'LIVE: sl-modernuo, the real world save, real players. Every button here asks you to type a phrase first. Start, Stop and Restart act on the existing container only; this console never creates it, never runs compose on it and never deploys. Each plan says whether a stop saves first.'
    $liveColor = [Drawing.Color]::Firebrick
    # cc-P108: the tab stays, grey, with every button greyed (Set-LiveRetiredLook); scripts\live.json brings it back.
    if (Test-LiveRetired $Config) { $liveCaption = 'LIVE ' + $Config.Live.Line + '. ' + $script:LiveRetiredText + ': every button here is greyed and nothing checks, probes or emails about live. scripts\live.json liveEnabled true brings this tab back as it was.'; $liveColor = [Drawing.Color]::Gray }
    $livePage = New-ButtonPage 'LIVE shard' $liveCaption @(
        (New-ConsoleButton 'Start' 'live.start'),
        (New-ConsoleButton 'Stop' 'live.stop'),
        (New-ConsoleButton 'Restart' 'live.restart'),
        (New-ConsoleButton 'Tail the log' 'live.tail'),
        (New-ConsoleButton 'Open a client' 'live.client')
    ) $liveColor

    # commit (cc-P60): Commit-ShardWork.ps1, as it is, in a window of its own
    $commitBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Commit' })) {
        # New-ConsoleButton's look, with a click that also hands over the message box.
        $b = New-Object Windows.Forms.Button
        $b.Text = $a.Label
        $b.Tag = $a.Key
        $b.Width = 230
        $b.Height = 36
        $b.Margin = New-Object Windows.Forms.Padding(6)
        $b.Add_Click({
            $m = $script:ui.CommitMessage.Text
            if (-not $m.Trim()) { $m = '' }
            $force = [bool]($this.Tag -eq 'commit.run' -and $script:ui.CommitForce.Checked)
            Invoke-ConsoleButton -ActionKey $this.Tag -CommitMessage $m -CommitForce:$force
            # Force is for one commit: it never carries over to the next click.
            if ($this.Tag -eq 'commit.run') { $script:ui.CommitForce.Checked = $false }
            Update-CommitCountdown -Reread
        })
        $commitBtns += $b
    }
    $where = $Config.CommitScript
    if (-not (Test-Path -LiteralPath $Config.CommitScript -PathType Leaf)) { $where += ' (NOT FOUND: both buttons will say so and do nothing)' }
    $commitPage = New-ButtonPage 'Commit' ('Runs ' + $where + '. Preview changes nothing. Commit and push asks for the phrase ''commit shard work'': both repos are public on GitHub, so a pushed commit cannot be taken back. It waits ' + (Format-QuietWindow $Config.CommitQuietSeconds) + ' after the last change and until every CC prompt has said DONE; Preview names what it waits on.' + "`r`n" + 'While the Golem is awake it commits and pushes after each P<nn> DONE by itself (Golem tab), so a manual commit is rarely needed.') $commitBtns
    $msgLabel = New-Caption 'Message (blank: the script''s default, "Shard work, <date> <time>"):' 400
    $msgBox = New-Object Windows.Forms.TextBox
    $msgBox.Name = 'commitMessage'
    $msgBox.Width = 700
    $msgBox.Margin = New-Object Windows.Forms.Padding(6)
    $flow = $commitPage.Controls[0]
    $flow.Controls.Add($msgLabel)
    $flow.Controls.Add($msgBox)
    $flow.Controls.SetChildIndex($msgLabel, 1)
    $flow.Controls.SetChildIndex($msgBox, 2)
    $flow.SetFlowBreak($msgBox, $true)
    # cc-P68: the Force box, after the buttons, and the live countdown under them.
    $forceBox = New-Object Windows.Forms.CheckBox
    $forceBox.Name = 'commitForce'
    $forceBox.Text = ('Force: skip the ' + (Format-QuietWindow $Config.CommitQuietSeconds) + ' wait for recently changed files (not the DONE check, SYSTEM or the phrase)')
    $forceBox.AutoSize = $true
    $forceBox.Margin = New-Object Windows.Forms.Padding(12, 14, 6, 6)
    $forceBox.Add_CheckedChanged({ Update-CommitCountdown })
    $flow.Controls.Add($forceBox)
    $flow.SetFlowBreak($forceBox, $true)
    $countdown = New-Caption 'Commit: checking...' 1100
    $countdown.Font = $script:BoldFont
    $flow.Controls.Add($countdown)
    $script:ui.CommitMessage = $msgBox
    $script:ui.CommitButtons = $commitBtns
    $script:ui.CommitForce = $forceBox
    $script:ui.CommitCountdown = $countdown
    $script:ui.CommitPage = $commitPage

    # player package (cc-P63): Build-PlayerPackage.ps1 and Publish-PlayerPackage.ps1, as they are
    $pkgPage = New-Object Windows.Forms.TabPage('Player package')
    $pkgTop = New-Object Windows.Forms.FlowLayoutPanel
    $pkgTop.Dock = 'Top'
    $pkgTop.Height = 96
    $pkgTop.WrapContents = $true
    # cc-P116: as tall as its caption, notes box and Build button wrap to (96 px cut Build package off in the UO look).
    $pkgTop.AutoSize = $true; $pkgTop.AutoSizeMode = 'GrowAndShrink'
    $pubWhere = $Config.PublishScript
    if (-not (Test-Path -LiteralPath $Config.PublishScript -PathType Leaf)) { $pubWhere += ' (NOT FOUND: Publish will say so and do nothing)' }
    $pkgCap = New-Caption ('Build package makes and checks a zip in player-package\dist and publishes nothing; it is refused while the shard repo has uncommitted changes. Publish selected runs ' + $pubWhere + ' after the phrase ''publish player package'': it is public, every launcher is offered it, and the script asks you to type yes in its own window.') 1100
    $pkgTop.Controls.Add($pkgCap)
    $pkgTop.SetFlowBreak($pkgCap, $true)
    $notesLabel = New-Caption 'Notes ("What is new", one line):' 300
    $notesBox = New-Object Windows.Forms.TextBox
    $notesBox.Name = 'packageNotes'
    $notesBox.Width = 600
    $notesBox.Margin = New-Object Windows.Forms.Padding(6)
    $buildBtn = New-Object Windows.Forms.Button
    $buildBtn.Text = 'Build package'
    $buildBtn.Tag = 'package.build'
    $buildBtn.Width = 160
    $buildBtn.Height = 30
    $buildBtn.Add_Click({
        # cc-P153: an empty box takes the suggestion first, for Chase to check before he presses again.
        if (-not $script:ui.PackageNotes.Text.Trim() -and $script:ui.PackageSuggestion) {
            $script:ui.PackageNotes.Text = $script:ui.PackageSuggestion.Line; $script:ui.PackageSuggested = $script:ui.PackageSuggestion.Line
            Write-ConsoleLine 'Build package: the notes box was empty, so it now holds the suggested line (the list it came from is under it). Edit it if you like, then press Build package again.' 'amber'
            return
        }
        Invoke-ConsoleButton -ActionKey 'package.build' -PackageNotes $script:ui.PackageNotes.Text.Trim()
    })
    # cc-P288: Refill puts the suggestion back in the box, over an edit (Chase's own choice).
    $refillBtn = New-Object Windows.Forms.Button
    $refillBtn.Text = 'Refill'
    $refillBtn.Name = 'packageNotesRefill'
    $refillBtn.Width = 90
    $refillBtn.Height = 30
    $refillBtn.Add_Click({
        if ($script:ui.PackageSuggestion) { $script:ui.PackageNotes.Text = $script:ui.PackageSuggestion.Line; $script:ui.PackageSuggested = $script:ui.PackageSuggestion.Line }
        else { Write-ConsoleLine 'Refill: no suggestion yet (it is read with the package light; the box under the notes says why).' 'amber' }
    })
    $script:ui.PackageNotesRefill = $refillBtn
    $pkgTop.Controls.AddRange(@($notesLabel, $notesBox, $refillBtn, $buildBtn))
    # cc-P132: the package light beside Build package.
    $script:ui.PackageLight = New-LightLabel 'package.light'
    $pkgTop.Controls.Add($script:ui.PackageLight.Row)
    $pkgTop.SetFlowBreak($script:ui.PackageLight.Row, $true)
    # cc-P153: what the suggested line was made from, so Chase can check it.
    $notesFrom = New-Object Windows.Forms.TextBox
    $notesFrom.Name = 'packageNotesFrom'; $notesFrom.Multiline = $true; $notesFrom.ReadOnly = $true; $notesFrom.ScrollBars = 'Vertical'
    $notesFrom.Width = 1060; $notesFrom.Height = 84; $notesFrom.Margin = New-Object Windows.Forms.Padding(6, 0, 6, 6)
    $notesFrom.Text = 'Suggested from: read with the package light (this tab''s Refresh list, and every two minutes).'
    $pkgTop.Controls.Add($notesFrom)
    $pkgTop.SetFlowBreak($notesFrom, $true)
    $script:ui.PackageNotesFrom = $notesFrom
    # cc-P124: the client Build package will ship, and its sl-build.txt, before Build runs. Refresh list reads it again.
    $clientBox = New-Object Windows.Forms.TextBox
    $clientBox.Name = 'packageClient'
    $clientBox.Multiline = $true
    $clientBox.ReadOnly = $true
    $clientBox.ScrollBars = 'Vertical'
    $clientBox.Width = 1080
    $clientBox.Height = 96
    $clientBox.Margin = New-Object Windows.Forms.Padding(6)
    $pkgTop.Controls.Add($clientBox)
    $pkgList = New-Object Windows.Forms.ListView
    $pkgList.Name = 'packageList'
    $pkgList.Dock = 'Fill'
    $pkgList.View = 'Details'
    $pkgList.FullRowSelect = $true
    $pkgList.MultiSelect = $false
    $pkgList.HideSelection = $false
    foreach ($col in @(@('Zip in dist\ (newest first)', 300), @('Version', 130), @('Size', 80), @('Built', 140), @('What is new (version.json notes)', 420))) {
        [void]$pkgList.Columns.Add($col[0], $col[1])
    }
    $pkgBottom = New-Object Windows.Forms.FlowLayoutPanel
    $pkgBottom.Dock = 'Bottom'
    $pkgBottom.Height = 42
    $pkgBottom.AutoSize = $true; $pkgBottom.AutoSizeMode = 'GrowAndShrink'  # cc-P116
    $pkgRefresh = New-Object Windows.Forms.Button
    $pkgRefresh.Text = 'Refresh list'
    $pkgRefresh.Name = 'packageRefresh'
    $pkgRefresh.Width = 110
    $pkgRefresh.Height = 30
    $pkgRefresh.Add_Click({ Update-PackageList })
    $publishBtn = New-Object Windows.Forms.Button
    $publishBtn.Text = 'Publish selected'
    $publishBtn.Tag = 'package.publish'
    $publishBtn.Width = 160
    $publishBtn.Height = 30
    $publishBtn.Margin = New-Object Windows.Forms.Padding(40, 3, 3, 3)
    $publishBtn.Add_Click({
        $sel = $null
        if ($script:ui.PackageList.SelectedItems.Count) { $sel = $script:ui.PackageList.SelectedItems[0].Text }
        Invoke-ConsoleButton -ActionKey 'package.publish' -PackageZip $sel
    })
    # cc-P68 Part A: -CheckZip on the selected zip, in a window that stays open. It only reads.
    $checkBtn = New-Object Windows.Forms.Button
    $checkBtn.Text = 'Check selected'
    $checkBtn.Tag = 'package.check'
    $checkBtn.Width = 140
    $checkBtn.Height = 30
    $checkBtn.Add_Click({
        $sel = $null
        if ($script:ui.PackageList.SelectedItems.Count) { $sel = $script:ui.PackageList.SelectedItems[0].Text }
        Invoke-ConsoleButton -ActionKey 'package.check' -PackageZip $sel
    })
    $pkgBottom.Controls.AddRange(@($pkgRefresh, $checkBtn, $publishBtn))
    # cc-P144: the Legion scripts row, under the zip buttons. Its own caption says where the clone is on this PC.
    $scrBar = New-Object Windows.Forms.FlowLayoutPanel
    $scrBar.Dock = 'Bottom'
    $scrBar.AutoSize = $true; $scrBar.AutoSizeMode = 'GrowAndShrink'
    $scrBar.WrapContents = $true
    $scrWhere = 'This PC has no scripts repo yet: Set up scripts repo makes one.'
    if ($Config.LegionRepo) { $scrWhere = 'This PC''s scripts repo: ' + $Config.LegionRepo + '. Every TazUO client here links to it, so editing a script edits the repo.' }
    $scrCap = New-Caption ('Legion scripts (their own repo, CainUnAble923/shattered-legacy-scripts). ' + $scrWhere + ' Publish to dev reaches only your own PCs; Promote to official reaches every player''s install. Neither needs the shard repo committed, only the scripts repo.') 1100
    $scrBar.Controls.Add($scrCap)
    $scrBar.SetFlowBreak($scrCap, $true)
    $scrBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Legion scripts' })) {
        $b = New-ConsoleButton $a.Label $a.Key 170
        $scrBtns += $b
        $scrBar.Controls.Add($b)
    }
    $script:ui.ScriptsButtons = $scrBtns
    $pkgPage.Controls.Add($pkgList)
    $pkgPage.Controls.Add($pkgTop)
    $pkgPage.Controls.Add($pkgBottom)
    $pkgPage.Controls.Add($scrBar)
    $script:ui.PackageNotes = $notesBox
    $script:ui.PackageList = $pkgList
    $script:ui.PackageClient = $clientBox
    $script:ui.PackageButtons = @($buildBtn, $publishBtn, $checkBtn)
    Update-PackageList

    # Golem (cc-P74 as Autopilot, cc-P99): the CC queue runner, through its own scripts. The panel only reads.
    $apPage = New-Object Windows.Forms.TabPage('Golem')
    $apTop = New-Object Windows.Forms.FlowLayoutPanel
    $apTop.Dock = 'Top'
    $apTop.AutoSize = $true
    $apTop.WrapContents = $true
    $apCap = New-Caption ('The Golem hands CC the next prompt in ' + $Config.Autopilot.Queue + ' each time the running one ends "P<nn> DONE", and emails you when CC needs you. After every DONE it commits AND PUSHES both repos to GitHub on its own. In a CC session, say: "Golem, please work on the shard tasks" (or "Golem, stop" to rest after the running prompt). Rest the Golem stops the hand-offs at once; a running prompt still finishes. Each button opens the Golem''s own script in a window of its own; the console never reorders the queue. After maxPerRun prompts (settings.json) the Golem rests and keeps the queue: start a new CC session before waking it. Until Move to the Golem, the buttons run cc-autopilot''s scripts; the panel says which is active.') 1100
    $apTop.Controls.Add($apCap)
    $apTop.SetFlowBreak($apCap, $true)
    $apBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem' })) {
        $b = New-ConsoleButton $a.Label $a.Key 170
        $apBtns += $b
        $apTop.Controls.Add($b)
    }
    # cc-P83: the play-test page and the dashboard, reachable from here too. Not in AutopilotButtons (the Golem's).
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Links' })) {
        $lb = New-ConsoleButton ('Open ' + $a.Label.ToLower()) $a.Key 170
        $apTop.Controls.Add($lb)
    }
    # cc-P125: the run row. A long run's cap and end, its two buttons, Wrap up and Find a stopping point (each toggles to
    # Cancel while set), and Resume Golem, shown grey until cc-P131's console wake exists.
    if ($apTop.Controls.Count) { $apTop.SetFlowBreak($apTop.Controls[$apTop.Controls.Count - 1], $true) }
    $capLabel = New-Caption 'Long run: cap' 100
    $capBox = New-Object Windows.Forms.TextBox
    $capBox.Name = 'golem.cap'; $capBox.Width = 60; $capBox.Text = 'all'; $capBox.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    $untilLabel = New-Caption 'until' 40
    $untilBox = New-Object Windows.Forms.TextBox
    $untilBox.Name = 'golem.until'; $untilBox.Width = 140; $untilBox.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    $mon = [datetime]::Today.AddDays(((8 - [int][datetime]::Today.DayOfWeek) % 7)); if ($mon -le [datetime]::Today) { $mon = $mon.AddDays(7) }
    $untilBox.Text = $mon.AddHours(6).ToString('yyyy-MM-dd HH:mm', $script:Invariant)
    $apTop.Controls.AddRange(@($capLabel, $capBox, $untilLabel, $untilBox))
    $runBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem run' })) {
        $b = New-ConsoleButton $a.Label $a.Key 170
        $runBtns += $b
        $apTop.Controls.Add($b)
    }
    # cc-P131: the wake row. A model picker (Fable or Opus, defaulting to the next prompt's Model line), Wake the Golem
    # (CC session), the Self-wake and Night shift toggles with the night shift's two times, and Check CLI sign-in.
    $apTop.SetFlowBreak($apTop.Controls[$apTop.Controls.Count - 1], $true)
    $modelLabel = New-Caption 'Model' 50
    $modelPick = New-Object Windows.Forms.ComboBox
    $modelPick.Name = 'golem.model'; $modelPick.DropDownStyle = 'DropDownList'; $modelPick.Width = 80; $modelPick.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    # cc-P153: Auto first and the default ("Auto: Opus, for P134", Format-AutoModelItem); Fable and Opus override it for
    # one Poke, then the picker goes back to Auto.
    $modelPick.Width = 190
    [void]$modelPick.Items.AddRange(@('Auto (best for the next prompt)', 'Fable', 'Opus'))
    $modelPick.SelectedIndex = 0
    $apTop.Controls.AddRange(@($modelLabel, $modelPick))
    $wakeBtns = @()
    $resume = $null
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem wake' })) {
        $b = New-ConsoleButton $a.Label $a.Key 200
        if ($a.Key -eq 'golem.resume') { $resume = $b }
        $wakeBtns += $b
        $apTop.Controls.Add($b)
        if ($a.Key -eq 'golem.autowake') {
            $nsLabel = New-Caption 'Night shift' 80
            $nsStart = New-Object Windows.Forms.TextBox
            $nsStart.Name = 'golem.nightstart'; $nsStart.Width = 50; $nsStart.Text = '22:30'; $nsStart.Margin = New-Object Windows.Forms.Padding(6, 12, 2, 6)
            $nsTo = New-Caption 'to' 20
            $nsEnd = New-Object Windows.Forms.TextBox
            $nsEnd.Name = 'golem.nightend'; $nsEnd.Width = 50; $nsEnd.Text = '06:30'; $nsEnd.Margin = New-Object Windows.Forms.Padding(2, 12, 6, 6)
            $apTop.Controls.AddRange(@($nsLabel, $nsStart, $nsTo, $nsEnd))
        }
        if ($a.Key -eq 'golem.marathon') {
            # cc-P200: the mode Marathon: set writes.
            $mpick = New-Object Windows.Forms.ComboBox
            $mpick.Name = 'golem.marathonpick'; $mpick.DropDownStyle = 'DropDownList'; $mpick.Width = 70; $mpick.Margin = New-Object Windows.Forms.Padding(2, 12, 6, 6)
            [void]$mpick.Items.AddRange(@('auto', 'on', 'off'))
            $mpick.SelectedIndex = 0
            $mpick.Add_SelectionChangeCommitted({ $script:ui.MarathonPicked = $true })
            $apTop.Controls.Add($mpick)
            $script:ui.MarathonPick = $mpick
        }
    }
    # cc-P182: Poke, Run next and Retry stopped prompt, a row of their own.
    $apTop.SetFlowBreak($apTop.Controls[$apTop.Controls.Count - 1], $true)
    $queueBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem queue' })) {
        $b = New-ConsoleButton $a.Label $a.Key 200
        $queueBtns += $b
        $apTop.Controls.Add($b)
    }
    $script:ui.GolemQueueButtons = $queueBtns
    # cc-P216: Answer a Golem's question, beside them (lit while a Golem on this PC has an open question).
    $ansBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem answer' })) { $b = New-ConsoleButton $a.Label $a.Key 230; $ansBtns += $b; $apTop.Controls.Add($b) }
    $script:ui.GolemAnswerButtons = $ansBtns
    # cc-P208: Shadow Iron's Pause merging and Resume merging, beside Merge branches now.
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem merge' })) { $apTop.Controls.Add((New-ConsoleButton $a.Label $a.Key 200)) }
    # cc-P187: the Golem picker, a row of its own: which Golem the Golem buttons act for (golems.json; the integrator
    # first), Wake's model following its lane, and Assign (the picked queue row to the picked Golem, and run it next).
    $apTop.SetFlowBreak($apTop.Controls[$apTop.Controls.Count - 1], $true)
    $pickRows = @(Get-ConsoleGolemPickRows $Config)
    $pickLabel = New-Caption 'Golem' 50
    $golemPick = New-Object Windows.Forms.ComboBox
    $golemPick.Name = 'golem.pick'; $golemPick.DropDownStyle = 'DropDownList'; $golemPick.Width = 420; $golemPick.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    foreach ($r in $pickRows) { [void]$golemPick.Items.Add($r.Text) }
    if ($golemPick.Items.Count) { $golemPick.SelectedIndex = 0 }
    $golemPick.Add_SelectionChangeCommitted({ Update-GolemPickModel })
    $apTop.Controls.AddRange(@($pickLabel, $golemPick))
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Golem pick' })) { $apTop.Controls.Add((New-ConsoleButton $a.Label $a.Key 230)) }
    $assignNext = New-Object Windows.Forms.CheckBox
    $assignNext.Name = 'golem.assignnext'; $assignNext.Text = 'and run it next'; $assignNext.AutoSize = $true; $assignNext.Margin = New-Object Windows.Forms.Padding(6, 14, 6, 6)
    $apTop.Controls.Add($assignNext)
    $script:ui.GolemPick = $golemPick
    $script:ui.GolemPickRows = $pickRows
    $script:ui.AssignNext = $assignNext
    # cc-P132: the Golem figure on its tab too (a look with crystals).
    $script:ui.GolemFigure = $null
    if ($script:theme.Crystals) {
        $gf = New-Object Windows.Forms.PictureBox
        $gf.Name = 'golemFigure'
        $gf.Size = New-Object Drawing.Size(64, 64); $gf.SizeMode = 'Zoom'; $gf.BackColor = [Drawing.Color]::Transparent
        $gf.Margin = New-Object Windows.Forms.Padding(12, 2, 6, 2)
        $gf.Image = Get-ArtOrFallback 'golem-dark' 64
        $apTop.Controls.Add($gf)
        $script:ui.GolemFigure = $gf
    }
    $script:ui.GolemModel = $modelPick
    $script:ui.GolemWakeButtons = $wakeBtns
    $script:ui.NightStart = $nsStart
    $script:ui.NightEnd = $nsEnd
    # The queue (hand-off order), the picked prompt's first paragraph, and the last 10 finished prompts.
    $apLists = New-Object Windows.Forms.TableLayoutPanel
    $apLists.Dock = 'Fill'
    $apLists.ColumnCount = 3
    $apLists.RowCount = 1
    [void]$apLists.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle([Windows.Forms.SizeType]::Percent, 38)))
    [void]$apLists.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle([Windows.Forms.SizeType]::Percent, 24)))
    [void]$apLists.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle([Windows.Forms.SizeType]::Percent, 38)))
    [void]$apLists.RowStyles.Add((New-Object Windows.Forms.RowStyle([Windows.Forms.SizeType]::Percent, 100)))
    # cc-P153: the queue with a note per prompt, in run order (Get-QueueRows): a grid, so the note wraps instead of being
    # cut to a few words. The running prompt on top, the next one marked; a prompt with no Summary line in grey.
    $qList = New-Object Windows.Forms.DataGridView
    $qList.Name = 'golemQueue'; $qList.Dock = 'Fill'; $qList.ReadOnly = $true; $qList.MultiSelect = $false; $qList.SelectionMode = 'FullRowSelect'
    $qList.AllowUserToAddRows = $false; $qList.AllowUserToDeleteRows = $false; $qList.AllowUserToResizeRows = $false; $qList.AllowUserToOrderColumns = $false
    $qList.RowHeadersVisible = $false; $qList.AutoSizeRowsMode = 'AllCells'; $qList.ColumnHeadersHeightSizeMode = 'AutoSize'; $qList.BorderStyle = 'None'
    $qList.ShowCellToolTips = $true; $qList.StandardTab = $true
    foreach ($col in @(@('num', '#', 52), @('mark', '', 62), @('note', 'Queue, in run order: what each prompt is', 0), @('model', 'Model', 56), @('size', 'Size', 96))) {
        $gc = New-Object Windows.Forms.DataGridViewTextBoxColumn
        $gc.Name = $col[0]; $gc.HeaderText = $col[1]; $gc.SortMode = 'NotSortable'
        if ($col[2]) { $gc.Width = $col[2] } else { $gc.AutoSizeMode = 'Fill'; $gc.MinimumWidth = 160; $gc.DefaultCellStyle.WrapMode = 'True' }
        [void]$qList.Columns.Add($gc)
    }
    $preview = New-Object Windows.Forms.TextBox
    $preview.Name = 'golemPreview'; $preview.Dock = 'Fill'; $preview.Multiline = $true; $preview.ReadOnly = $true; $preview.ScrollBars = 'Vertical'
    $fList = New-Object Windows.Forms.ListView
    $fList.Name = 'golemFinished'; $fList.Dock = 'Fill'; $fList.View = 'Details'; $fList.FullRowSelect = $true; $fList.MultiSelect = $false; $fList.HideSelection = $false
    foreach ($col in @(@('#', 50), @('Result', 70), @('When', 90), @('Title', 230), @('Headline', 400))) { [void]$fList.Columns.Add($col[0], $col[1]) }
    $qList.Add_SelectionChanged({
        $pk = Get-GolemQueuePick
        if ($pk) { $script:ui.GolemPreview.Text = $pk.Preview }
    })
    # The two Open buttons sit in the run row, which the page scrolls (cc-P116); the lists below it may be short.
    $openPrompt = New-ConsoleButton 'Open prompt in Notepad' 'golem.openprompt' 200
    $openNotes = New-ConsoleButton 'Open notes in Notepad' 'golem.opennotes' 200
    $apTop.Controls.AddRange(@($openPrompt, $openNotes))
    $apLists.Controls.Add($qList, 0, 0); $apLists.Controls.Add($preview, 1, 0); $apLists.Controls.Add($fList, 2, 0)
    $apPanel = New-Object Windows.Forms.RichTextBox
    $apPanel.Name = 'golemPanel'
    $apPanel.Dock = 'Fill'
    $apPanel.ReadOnly = $true
    $apPanel.BackColor = [Drawing.Color]::White
    $apPanel.Font = $script:MonoFont
    $apPanel.WordWrap = $false
    $apPanel.DetectUrls = $false
    # The panel above, the lists below, in the space the panel alone had (cc-P116's scrolling of the top row is kept).
    $apSplit = New-Object Windows.Forms.SplitContainer
    $apSplit.Name = 'golemSplit'
    $apSplit.Dock = 'Fill'
    $apSplit.Orientation = 'Horizontal'
    $apSplit.Panel1MinSize = 40
    $apSplit.Panel2MinSize = 40
    $apSplit.Panel1.Controls.Add($apPanel)
    $apSplit.Panel2.Controls.Add($apLists)
    $apSplit.Add_SizeChanged({ try { if ($this.Height -gt 300) { $this.SplitterDistance = $this.Height - 240 } } catch { } })
    $apPage.Controls.Add($apSplit)
    $apPage.Controls.Add($apTop)
    $script:ui.AutopilotPage = $apPage
    $script:ui.AutopilotPanel = $apPanel
    $script:ui.AutopilotButtons = $apBtns
    $script:ui.GolemRunButtons = $runBtns
    $script:ui.GolemResume = $resume
    $script:ui.LongRunCap = $capBox
    $script:ui.LongRunUntil = $untilBox
    $script:ui.GolemQueueList = $qList
    $script:ui.GolemPreview = $preview
    $script:ui.GolemFinishedList = $fList

    # cc-P101: Websites. Pick a site; each button acts on it. The panel only reads.
    $webPage = New-Object Windows.Forms.TabPage('Websites')
    $webTop = New-Object Windows.Forms.FlowLayoutPanel
    $webTop.Dock = 'Top'
    $webTop.AutoSize = $true
    $webTop.WrapContents = $true
    $webCap = New-Caption ('The websites in ' + $Config.WebsitesFile + ', on this PC and on Haven. The lights check each one every 60 s. Pick a site, then: Open; for a game-PC site, Restart, Redeploy (its own start script) or Logs; for a Haven site with a deploy in the allowlist, Deploy to Haven (asks for deploy to haven, then runs it in a window with your own SSH key and keeps a log). Open NPM opens an SSH tunnel to Haven and then its admin page. No key or password is stored by the console.') 1100
    $webTop.Controls.Add($webCap)
    $webTop.SetFlowBreak($webCap, $true)
    $webPick = New-Object Windows.Forms.ComboBox
    $webPick.DropDownStyle = 'DropDownList'
    $webPick.Name = 'web.pick'
    $webPick.Width = 300
    $webPick.Margin = New-Object Windows.Forms.Padding(6, 12, 6, 6)
    foreach ($ws in @($Config.Websites.Sites)) { [void]$webPick.Items.Add($ws.Name + $(if ($ws.Where -ceq 'pc') { '  (this PC)' } else { '  (Haven)' })) }
    if ($webPick.Items.Count) { $webPick.SelectedIndex = 0 }
    $webTop.Controls.Add($webPick)
    $script:ui.WebsitePick = $webPick
    $webBtns = @()
    foreach ($a in @(Get-ConsoleActions | Where-Object { $_.Group -eq 'Websites' })) {
        $b = New-WebsiteButton $a.Label $a.Key 150
        $webBtns += $b
        $webTop.Controls.Add($b)
    }
    $webCheck = New-Object Windows.Forms.Button
    $webCheck.Text = 'Check now'
    $webCheck.Name = 'web.check'
    $webCheck.Width = 120
    $webCheck.Height = 36
    $webCheck.Margin = New-Object Windows.Forms.Padding(6)
    $webCheck.Add_Click({ if (-not $script:webProbe) { $script:webResultAt = $null; Update-WebsitesTile; Write-ConsoleLine 'Websites: checking every site now.' 'gray' } })
    $webTop.Controls.Add($webCheck)
    $webPanel = New-Object Windows.Forms.RichTextBox
    $webPanel.Name = 'websitesPanel'   # cc-P153: for its tooltip
    $webPanel.Dock = 'Fill'
    $webPanel.ReadOnly = $true
    $webPanel.BackColor = [Drawing.Color]::White
    $webPanel.Font = $script:MonoFont
    $webPanel.WordWrap = $false
    $webPanel.DetectUrls = $false
    $webPage.Controls.Add($webPanel)
    $webPage.Controls.Add($webTop)
    $script:ui.WebsitesPage = $webPage
    $script:ui.WebsitesPanel = $webPanel
    $script:ui.WebsiteButtons = $webBtns

    # cc-P152: the Art tab, thin on purpose: the list and buttons that run scripts\art\Art.ps1 (the web console will
    # call the same commands).
    $reqPage = New-ArtPage
    $tabs.TabPages.AddRange(@($testPage, $fixPage, $qPage, $snapPage, $worldPage, $artPage, $diagPage, $commitPage, $pkgPage, $apPage, $webPage, $reqPage, $livePage))
    $script:ui.WorldPage = $worldPage
    $script:ui.PackagePage = $pkgPage
    # cc-P116: a page's scrolling is worked out again when it is shown (its wrapping parts only measure true then), and
    # it opens at its top: choosing a tab focuses its first button, and a scrolling page would otherwise scroll down to
    # it and cut off the caption above. Posted, so it runs after that focus change.
    $tabs.Add_SelectedIndexChanged({
        Update-PageScroll
        $script:ui.Form.BeginInvoke([Action]{ $p = $script:ui.Tabs.SelectedTab; if ($p -and $p.AutoScroll) { $p.AutoScrollPosition = New-Object Drawing.Point(0, 0) } }) | Out-Null
    })

    # output
    $outGroup = New-Object Windows.Forms.GroupBox
    $outGroup.Text = 'Output'
    $outGroup.Dock = 'Fill'
    $out = New-Object Windows.Forms.RichTextBox
    $out.Name = 'actionOutput'   # cc-P153: for its tooltip
    $out.Dock = 'Fill'
    $out.ReadOnly = $true
    $out.BackColor = [Drawing.Color]::White
    $out.Font = $script:MonoFont
    $out.DetectUrls = $false
    if ($Pane) {
        $outGroup.Text = 'Console output, and the Action output pane'
        $script:ui.Pane = New-OutputPane $out $outGroup
    } else {
        $outGroup.Controls.Add($out)
    }
    $script:OutputBox = $out

    # cc-P132: the three rows under the tiles are two draggable splitters: Status over (tabs over output). The first
    # layout keeps cc-P116's shares (status 36, tabs 38, output 26 with the pane; 48, 32, 20 without); a saved window
    # state (Restore-WindowState) then puts the splitters where they were.
    $split1 = New-Object Windows.Forms.SplitContainer
    $split1.Name = 'splitStatus'; $split1.Orientation = 'Horizontal'
    $split1.Size = New-Object Drawing.Size(1100, 800)   # a size first: the minimums below must fit inside one
    $split1.Panel1MinSize = 90; $split1.Panel2MinSize = 220; $split1.SplitterWidth = 6
    $split1.Dock = 'Fill'
    $split2 = New-Object Windows.Forms.SplitContainer
    $split2.Name = 'splitOutput'; $split2.Orientation = 'Horizontal'
    $split2.Size = New-Object Drawing.Size(1100, 500)
    $split2.Panel1MinSize = 120; $split2.Panel2MinSize = 90; $split2.SplitterWidth = 6
    $split2.Dock = 'Fill'
    $split1.Panel1.Controls.Add($statusGroup)
    $split2.Panel1.Controls.Add($tabs)
    $split2.Panel2.Controls.Add($outGroup)
    $split1.Panel2.Controls.Add($split2)
    $grid.RowCount = 2
    $grid.RowStyles.Clear()
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Absolute', 104)))
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', 100)))
    $grid.Controls.Add($glance, 0, 0)
    $grid.Controls.Add($split1, 0, 1)
    $form.Controls.Add($grid)
    $script:ui.Split1 = $split1
    $script:ui.Split2 = $split2
    $script:ui.SplitShares = $(if ($Pane) { @(0.36, 0.38, 0.26) } else { @(0.48, 0.32, 0.20) })
    $script:ui.SplitsPlaced = $false
    $placeSplits = {
        # Once, at the first size: the shares above; afterwards the person's drag is kept.
        if ($script:ui.SplitsPlaced -or -not $script:ui.Split1 -or $script:ui.Split1.Height -le 0) { return }
        $sh = $script:ui.SplitShares
        $h = $script:ui.Split1.Height
        Set-SplitterSafely $script:ui.Split1 ([int]($h * $sh[0]))
        $h2 = $script:ui.Split2.Height
        Set-SplitterSafely $script:ui.Split2 ([int]($h2 * ($sh[1] / ($sh[1] + $sh[2]))))
        $script:ui.SplitsPlaced = $true
        if ($script:ui.WindowState) { Restore-WindowSplitters $script:ui.WindowState }
    }
    $split1.Add_SizeChanged($placeSplits)
    $form.Add_Shown($placeSplits)
    & $placeSplits
    if ($script:theme.Header) {
        # cc-P84: the crest and the name, on stone, above the tiles. Our own art (branding\intro), not EA's.
        # cc-P132: in a look with crystals, the band slot (art, or the drawn band with a rune at its right end).
        $header = New-Object Windows.Forms.Panel
        $header.Dock = 'Top'
        $header.Height = 62
        if ($script:theme.Crystals) {
            $band = Get-ArtImage 'header-band'
            if ($band) { $header.BackgroundImage = $band; $header.BackgroundImageLayout = 'Tile' }
            else {
                $rune = New-Object Windows.Forms.PictureBox
                $rune.Name = 'headerRune'
                $rune.Size = New-Object Drawing.Size(56, 56); $rune.SizeMode = 'Zoom'; $rune.BackColor = [Drawing.Color]::Transparent
                $rune.Dock = 'Right'
                $rune.Padding = New-Object Windows.Forms.Padding(8)
                $rune.Image = Get-ArtOrFallback 'rune-corner' 40
                $header.Controls.Add($rune)
            }
        }
        # cc-P153 (Chase, 10-17 13:06: "simple console icon we use, not the fancy website logo"): the window's own icon
        # (WindowIconFile, the site's favicon.ico), its 48 px frame drawn 1:1 at 100% (the nearest frame at or above the
        # box at other scales). The painted crest stays for the website and the big showcase spots.
        $crest = New-Object Windows.Forms.PictureBox
        $crest.Name = 'headerIcon'
        $crest.SetBounds(12, 6, 48, 48)
        $crest.SizeMode = 'Zoom'
        $crest.BackColor = [Drawing.Color]::Transparent
        $dpi = 96; try { $dpi = [int]$form.DeviceDpi } catch { }
        try { $crest.Image = Get-HeaderIconImage -Path $Config.WindowIconFile -Size ([int][Math]::Round(48 * $dpi / 96)) } catch { }
        if (-not $crest.Image) { try { if (Test-Path -LiteralPath $Config.CrestFile -PathType Leaf) { $crest.Image = [Drawing.Image]::FromStream((New-Object IO.MemoryStream(, [IO.File]::ReadAllBytes($Config.CrestFile)))) } } catch { } }
        $name = New-Object Windows.Forms.Label
        $name.Text = 'Shattered Legacy  -  Shard Console'
        $name.AutoSize = $true
        $name.Location = New-Object Drawing.Point(72, 14)
        $name.Font = Get-ThemeFont 'Title' ([Drawing.FontStyle]::Bold)
        $name.Name = 'title'
        $trim = New-Object Windows.Forms.Panel
        $trim.Dock = 'Bottom'
        $trim.Height = 2
        $trim.BackColor = ConvertTo-DrawingColor $script:theme.Chrome.Gold
        $header.Controls.AddRange(@($crest, $name, $trim))
        $form.Controls.Add($header)
        $grid.BringToFront()
        $script:ui.Header = $header
    }
    Set-ControlTheme $form $script:theme $script:themeImages
    Set-TabTheme $tabs $script:theme
    if ($script:theme.Header) {
        # The title in gold, the trim kept gold (the walker made both stone-colored).
        $script:ui.Header.Controls[1].ForeColor = ConvertTo-DrawingColor $script:theme.Chrome.Gold
        $script:ui.Header.Controls[2].BackColor = ConvertTo-DrawingColor $script:theme.Chrome.Gold
        # cc-P87: the header on the same backing as the groups, so the title does not sit on the stone's speckle.
        $bk = $script:theme.Chrome
        if (-not $script:ui.Header.BackgroundImage) { $script:ui.Header.BackColor = [Drawing.Color]::FromArgb($bk.BackingAlpha, $bk.Backing[0], $bk.Backing[1], $bk.Backing[2]) }
    }
    # cc-P132: the window state (a normal start only; the facts' forms are placed by the facts) and its saving on close.
    if ($RestoreWindow) {
        $script:ui.WindowState = Read-WindowState $Config
        Restore-WindowBounds $form $script:ui.WindowState
        $form.Add_FormClosing({ [void](Save-WindowState $script:cfg $script:ui.Form) })
    }
    Update-TileFit
    Update-CaptionFit
    # cc-P84: a tooltip on every button, tile, tab, box and field, in both looks.
    $tips = New-Object Windows.Forms.ToolTip
    $tips.AutoPopDelay = 20000
    $tips.InitialDelay = 500
    $tabs.ShowToolTips = $true
    Set-ConsoleTooltips $form (Get-ConsoleTooltips) $tips
    # cc-P108: after the tooltips, so a greyed live control's tooltip says Live is retired.
    [void](Set-LiveRetiredLook $form $Config $tips)
    $script:ui.ToolTip = $tips
    Enable-DisabledTooltips $form $tips   # cc-P153: a greyed control still says why on hover

    $timer = New-Object Windows.Forms.Timer
    $timer.Interval = 30000
    $timer.Add_Tick({ if ($script:ui.Auto.Checked) { Update-ConsoleStatus } })
    $timer.Start()
    # The DNS tile: a 1 s tick that only collects a finished check and starts the next every 30 s.
    $netTimer = New-Object Windows.Forms.Timer
    $netTimer.Interval = 1000
    $netTimer.Add_Tick({ Update-NetworkTile; Update-WebsitesTile })
    $netTimer.Start()
    # cc-P68: the Commit tab's countdown. A 1 s tick that does nothing unless the Commit tab is the
    # one showing; the repos are re-read every CommitPollSeconds, so a new change restarts it.
    $cdTimer = New-Object Windows.Forms.Timer
    $cdTimer.Interval = 1000
    $cdTimer.Add_Tick({ Update-CommitCountdown })
    $cdTimer.Start()
    $tabs.Add_SelectedIndexChanged({
        Update-CommitCountdown -Reread
        $sel = $script:ui.Tabs.SelectedTab
        if ($script:BackgroundRefresh) {
            # cc-P154: the tab opened shows what was last read at once, and its own reads are asked for in the background.
            if ($sel -eq $script:ui.AutopilotPage) { [void](Show-AutopilotView $script:autopilot); [void](Start-ConsoleRefresh -Parts @('golem') -Queue) }
            if ($sel -eq $script:ui.PackagePage) { [void](Start-ConsoleRefresh -Parts @('package') -Queue) }
            if ($sel -eq $script:ui.WorldPage) { Show-WorldSetupPage }
        } elseif ($sel -eq $script:ui.AutopilotPage) { [void](Update-AutopilotView) }
    })
    if ($BackgroundRefresh) {
        # cc-P154: a tab shown for the first time makes its controls and lays them out; with the window's drawing off
        # until the handlers above are done, so it is drawn once (Suspend-WindowDraw).
        $tabs.Add_Selecting({ if (-not $script:tabHeld) { $script:tabHeld = Suspend-WindowDraw } })
        $tabs.Add_SelectedIndexChanged({ if ($script:ui.Form.IsHandleCreated) { $script:ui.Form.BeginInvoke([Action]{ if ($script:tabHeld) { $script:tabHeld = $false; Resume-WindowDraw -Only $script:ui.Tabs } }) | Out-Null } })
        # cc-P154: collects the background reads (Receive-ConsoleJobs). Cheap: it only looks whether one has finished.
        $rxTimer = New-Object Windows.Forms.Timer
        $rxTimer.Interval = 250
        $rxTimer.Add_Tick({ Receive-ConsoleJobs })
        $rxTimer.Start()
        $script:ui.ReceiveTimer = $rxTimer
    }
    $script:commitFactsCache = $null
    $script:commitFactsAt = $null
    $form.Add_FormClosed({
        $script:ui.Timer.Stop(); $script:ui.NetTimer.Stop(); $script:ui.CountdownTimer.Stop(); $script:OutputBox = $null
        # cc-P154: and the background reads.
        if ($script:ui.ReceiveTimer) { $script:ui.ReceiveTimer.Stop() }
        Stop-ConsoleJobs
        # cc-P84: a job still streaming into the pane is ended with the window, so nothing runs on unseen.
        if ($script:ui.Pane) { $script:ui.Pane.Timer.Stop(); if ($script:paneJob) { [void]$script:paneJob.KillTree(); $script:paneJob = $null } }
        if ($script:netProbe) { try { $script:netProbe.PS.Stop(); $script:netProbe.PS.Dispose() } catch { } }
        if ($script:webProbe) { try { $script:webProbe.PS.Stop(); $script:webProbe.PS.Dispose() } catch { } }
    })
    $script:ui.Timer = $timer
    $script:ui.NetTimer = $netTimer
    $script:ui.CountdownTimer = $cdTimer
    Update-NetworkTile
    Update-WebsitesView

    if ($DryRun) { Write-ConsoleLine 'DRY RUN: every button prints the plan it would run. The status panel is read from a capture file, not docker.' 'amber' }
    Set-ConsoleDryRun ([bool]$DryRun)
    # cc-P154: under -BackgroundRefresh the window opens now and the first status fills in when it is read.
    if ($BackgroundRefresh) {
        # cc-P155: the tiles as the status file last said (the web console's collector keeps it fresh), until this
        # window's own first refresh is back.
        if (-not $DryRun) { [void](Show-StatusFileTiles $Config.StatusFile) }
        Add-RichLine $script:ui.Status 'Reading the status (docker, git, the Golem) in the background...' 'gray'
        Start-WorldCommandsRead
    }
    Update-ConsoleStatus
    $form
}

# --- entry ---------------------------------------------------------------------------------

# -LoadOnly: define every function above and run nothing. The facts dot-source it this way.
if ($LoadOnly) { return }

$script:SelfPath = $MyInvocation.MyCommand.Path
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$config = Get-ConsoleConfig -RepoRoot $RepoRoot

if ($Capture) {
    $sec = Get-DockerSections $config
    foreach ($k in @($sec.Keys)) { if ($k -like 'logs *' -or $k -like 'logtail *') { $sec[$k] = Remove-AccountLines $sec[$k] } }
    [IO.File]::WriteAllText($Capture, (ConvertTo-CaptureFile $sec), (New-Object Text.UTF8Encoding $false))
    Write-Host ('wrote ' + $Capture) -ForegroundColor Green
    exit 0
}

if ($Action -eq 'list') {
    foreach ($a in Get-ConsoleActions) { Write-Host ('{0,-18} {1,-12} {2}' -f $a.Key, $a.Group, $a.Label) }
    Write-Host 'snapshot.* take -Shard test|live (default test), -SnapshotName for create, -Snapshot for restore and delete.'
    Write-Host 'commit.* take -CommitMessage (blank: the script''s default); commit.run needs -Mode Execute and -ConfirmLive ''commit shard work''; -CommitForce skips only the wait for recently changed files.'
    Write-Host 'package.build takes -PackageNotes; package.publish takes -PackageZip <name in player-package\dist> and -ConfirmLive ''publish player package''.'
    Write-Host 'package.check takes -PackageZip <name in player-package\dist>; it only reads.'
    Write-Host 'package.scripts.check only reads. package.scripts.dev and .promote need -Mode Execute and -ConfirmLive ''publish scripts to dev'' or ''promote scripts to official''; each asks "Type yes" itself. .pull, .setup and .key need -Mode Execute.'
    Write-Host 'test.deploy takes -ImageTag sl-modernuo:cc-pNN and -ConfirmLive ''deploy to test''.'
    Write-Host 'test.wipe takes -WipeAdmin <new admin name> and -ConfirmLive ''WIPE TEST ACCOUNTS''; it asks for the password in its own window.'
    Write-Host 'fix.diagnose only reads. fix.restart, fix.rollback, fix.rebuild and fix.prune take -ConfirmLive ''restart test'', ''roll back test'', ''rebuild test'', ''prune images''; fix.rebuild takes -Commit <hash from its list, or HEAD>; fix.rollback takes -ImageTag sl-modernuo:good-<commit> (default: the newest).'
    Write-Host 'vpn.disconnect needs -Mode Execute: it ends a WireGuard for Windows tunnel (elevated), opens Proton VPN, or names the VPN to disconnect in its app.'
    Write-Host 'links.playtests, links.dashboard and links.status open the play-test page (8093), the dashboard (3000) and the status page (8093/status) in the browser.'
    Write-Host 'web.open, .logs and .npm only open; web.restart, .redeploy and .deploy need -Mode Execute. Each takes -Site <key in scripts\websites.json>; web.deploy also -ConfirmLive ''deploy to haven''.'
    Write-Host 'art.registry and art.bodies run scripts\validate-art.py (read only); art.checkvd takes -VdFile <path> and -Body <n> (no body: a free one is proposed); art.hues takes -HuesFile <the other hues.mul>. map.* print an in-game command''s syntax.'
    Write-Host 'golem.poke needs -Mode Execute; golem.runnext and golem.retry are the Golem tab''s (the picked queue row, the STOPPED prompt) and the web console''s (web-console\golem-actions.json).'
    Write-Host 'golem.on, .off, .email, .test and .move need -Mode Execute; each opens the Golem''s own script in a window of its own (golem.move after -ConfirmLive ''move to the golem''). golem.queue, .log, .settings and .folder only open. autopilot.* still work as aliases.'
    exit 0
}

if ($WorldCommands) {
    $cmds = @(ConvertFrom-CommandSources (Get-CommandSourceFiles $config.Customizations))
    Write-ConsoleLine ('World commands, discovered from ' + $config.Customizations + ': ' + $cmds.Count + ' registrations read.') 'head'
    foreach ($l in (Format-WorldCommandReport @(Get-WorldCommandGroups $cmds))) { Write-ConsoleLine $l.Text $l.Color }
    exit 0
}

if ($Status) {
    $st = Get-ConsoleState -Config $config -DryRun:$DryRun -StatusFrom $StatusFrom
    # cc-P106: a question a shard waits on is emailed once, from here too (a scheduled -Status reaches Chase with the console shut).
    if (-not $DryRun) { Invoke-QuestionMail -Config $config -State $st }
    # cc-P74: the GOLEM tile is read from its folder even under -DryRun; it is not docker.
    $ap = Get-AutopilotViewNow $config
    # cc-P83: and PLAY TESTS, from shard-migration\testing\play-tests.json.
    $pt = Get-PlayTestGlanceNow $config
    # cc-P101: and WEBSITES, checked now (one GET each, 4 s timeout).
    $wt = Get-WebsitesGlance $config.Websites (Get-WebsiteProbe (Get-WebsiteChecks $config) $script:WebsiteTimeoutSec)
    foreach ($l in (Format-StatusReport $st $config @($ap.Tile, $pt, $wt))) { Write-ConsoleLine $l.Text $l.Color }
    exit 0
}

if ($Action) {
    # cc-P99: autopilot.* are the Golem's buttons under their old names.
    $Action = Resolve-ActionAlias $Action
    $sk = Resolve-ActionShardKey -Action $Action -Shard $Shard
    # The mode is decided and shown before anything is read or run (D37).
    $run = Resolve-RunMode -Action $Action -Mode $Mode -DryRun:$DryRun
    $banner = Get-ModeBanner -RunMode $run -Action $Action -ShardKey $sk
    try { $Host.UI.RawUI.WindowTitle = $banner.Title } catch { }
    foreach ($l in $banner.Lines) { Write-ConsoleLine $l $banner.Color }
    if ($run -eq 'refuse') { exit 2 }
    $isDry = ($run -eq 'dry')
    $st = $null
    $facts = $null
    $pkgFacts = $null
    $apFacts = $null
    $now = Get-Date
    $vpnFacts = $null
    if ($Action -like 'links.*' -or $Action -like 'web.*' -or $Action -like 'art.*' -or $Action -like 'map.*') {
        # cc-P83: a link reads nothing. cc-P101: nor does a Websites action. cc-P135: nor a Map and Art one.
    } elseif ($Action -like 'vpn.*') {
        $vpnFacts = Get-VpnFacts $config
    } elseif ($Action -like 'golem.*') {
        # cc-P74: the Golem's folder, never docker.
        $apFacts = Get-AutopilotFacts $config
    } elseif ($Action -like 'commit.*') {
        # From a click the message comes as base64 (Get-ChildArgumentLine); -CommitMessage is for a person.
        if ($CommitMessageB64) { $CommitMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($CommitMessageB64)) }
        $facts = Get-CommitFacts $config
    } elseif ($Action -like 'package.scripts.*') {
        # cc-P144: nothing to read first; the scripts read the clone and Haven themselves.
    } elseif ($Action -like 'package.*') {
        # cc-P63: the notes come as base64 from a click, as a commit message does.
        if ($PackageNotesB64) { $PackageNotes = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($PackageNotesB64)) }
        $pkgFacts = Get-PackageFacts $config $PackageZip
    } else {
        $fromCapture = $isDry -and -not $StatusFromDocker
        $st = Get-ConsoleState -Config $config -DryRun:$fromCapture -StatusFrom $StatusFrom
        if ($fromCapture) { $now = $st.Now.ToLocalTime() }
    }
    # cc-P73: the wipe re-reads its facts here, so the child plans from the shard as it is now.
    $wFacts = $null
    if ($Action -eq 'test.wipe') { $wFacts = Get-WipeFacts $config $st }
    # cc-P96: Fix test shard re-reads git and the save record here, as the wipe re-reads its facts.
    $fixFacts = $null
    if ($Action -like 'fix.*') { $fixFacts = Get-FixFacts $config $st -NoDocker:$fromCapture }
    $plan = @(Get-ActionPlan -Action $Action -ShardKey $Shard -Config $config -State $st -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now -CommitMessage $CommitMessage -CommitFacts $facts -PackageNotes $PackageNotes -PackageZip $PackageZip -PackageFacts $pkgFacts -ImageTag $ImageTag -CommitForce:$CommitForce -WipeFacts $wFacts -WipeAdmin $WipeAdmin -AutopilotFacts $apFacts -VpnFacts $vpnFacts -FixFacts $fixFacts -Commit $Commit -Site $Site -VdFile $VdFile -Body $Body -HuesFile $HuesFile)
    $viol = @(Test-PlanSafety $plan $config)
    $header = 'PLAN'
    if ($isDry) { $header = 'DRY RUN' }
    foreach ($l in (Format-Plan -Action $Action -ShardKey $sk -Plan $plan -Violations $viol -Header $header)) { Write-ConsoleLine $l 'normal' }
    if ($isDry) { Write-ConsoleLine '   (dry run: nothing was run)' 'amber'; exit 0 }
    Write-ConsoleLine '' 'normal'
    $ok = Invoke-Plan -Plan $plan -Config $config -ActionKey $Action -ShardKey $sk -ConfirmLive $ConfirmLive -Yes:$Yes
    if ($ok) { Write-ConsoleLine ($Action + ': done.') 'green'; exit 0 }
    # cc-P68: a declined publish already said "Publish cancelled. Nothing was published." last. A wipe
    # cancelled at its password (cc-P73) is the same: exit 3, nothing changed.
    if ($script:PlanCancelled) { exit 3 }
    exit 1
}

# cc-P84: the look and the Action output pane, from the settings remembered on this PC.
$settings = Read-ConsoleSettings $config
# cc-P132: DPI awareness before the first window, and the remembered window state (size, place, splitters).
Write-ConsoleLog $config ('DPI: ' + (Set-ConsoleDpiAwareness))
$form = New-ConsoleForm -Config $config -DryRun:$DryRun -StatusFrom $StatusFrom -Theme $settings.Theme -Pane -Settings $settings -RestoreWindow -BackgroundRefresh
[void]$form.ShowDialog()
$form.Dispose()
