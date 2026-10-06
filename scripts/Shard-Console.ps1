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
  grey UNKNOWN, never red. Get-DnsGlance says which signals it reads and why.

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
    docker tag + Start-TestShard.ps1   Test shard > Deploy built image (cc-P63): sl-modernuo:cc-p*
                                   onto sl-modernuo:latest after 'deploy to test', then -Down and
                                   -SkipBuild. The only docker tag the console makes; never live.

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
    [string]$ImageTag
)

# =============================================================================================
# CORE. Strings in, objects or strings out. No docker, no files, no WinForms below this line
# until the SHELL marker, so every function here can be given captured output and checked.
# =============================================================================================

$script:Invariant       = [Globalization.CultureInfo]::InvariantCulture
$script:CaptureMarker   = '##### SHARD-CONSOLE-CAPTURE '
$script:SnapshotPattern = '^(\d{8}-\d{6})_([A-Za-z0-9._-]+)$'
$script:NoSaveWarning   = 'A stop does not save: play since the last autosave (every 5 minutes) is lost. [save in game first if that matters.'
# D36 fixed: said instead for a container whose log since this start has $script:SavesOnStopLine.
$script:SavesOnStopNote = 'The shard saves on stop (D36 fix, seen in its log since this start). If the stop times out, play since the last autosave is lost.'
# Printed by server/customizations/Misc/SaveOnShutdown.cs (RegisteredLine) once it listens for SIGTERM. Change both together.
$script:SavesOnStopLine = '[SaveOnShutdown] listening for SIGTERM: a stop saves the world first (D36)'
# cc-P60: said above the phrase before a commit, the way D36's warning is said before a stop.
$script:CommitPublicWarning = 'Both repos are PUBLIC on GitHub: a commit that is pushed cannot be taken back.'
# cc-P63: said above the phrase before a publish. Publish-PlayerPackage.ps1 serves version.json, which every launcher reads.
$script:PublishPublicWarning = 'This is PUBLIC: every player''s launcher is offered this package at its next start.'

function Get-ConsoleConfig {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)
    $lib = Join-Path $RepoRoot 'server\lib\uo\modernuo'
    $shards = [ordered]@{
        test = [pscustomobject]@{
            Key = 'test'; Label = 'TEST'; Container = 'sl-modernuo-test'; IsLive = $false
            # cc-P37: the test world has its own folder, mounted whole (docker-compose.test.yml).
            Saves = (Join-Path (Join-Path $RepoRoot 'server\lib\uo\modernuo-test') 'Saves'); GamePort = 2594
            Client = 'D:\UO\Play-SL-Admin-TEST.bat'
        }
        live = [pscustomobject]@{
            Key = 'live'; Label = 'LIVE'; Container = 'sl-modernuo'; IsLive = $true
            Saves = (Join-Path $lib 'Saves'); GamePort = 2593
            Client = 'D:\UO\Play-SL-Admin.bat'
        }
    }
    [pscustomobject]@{
        RepoRoot         = $RepoRoot
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
        DefaultCapture   = (Join-Path $RepoRoot 'scripts\tests\fixtures\console-capture-2026-09-29.txt')
        StopTimeout      = 60
        LogWindowMinutes = 15
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
        )
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
        PublishScript      = 'D:\UO\haven-migration\Publish-PlayerPackage.ps1'
        # The images Deploy built image offers: build.sh -t sl-modernuo:cc-pNN, one per prompt.
        DeployTagPattern   = '^sl-modernuo:cc-p[A-Za-z0-9._-]*$'
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
        MountTargets = @(); Mounts = @()
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
        return [pscustomobject]@{ Exists = $false; Id = $null; RepoTags = @(); Created = $null; Error = $r.Error }
    }
    $i = $r.Items[0]
    [pscustomobject]@{
        Exists = $true; Id = $i.Id; RepoTags = @($i.RepoTags)
        Created = (ConvertFrom-DockerTime $i.Created); Error = $null
    }
}

function ConvertFrom-ImageTagListing {
    # cc-P63. `docker images sl-modernuo --format {{.Repository}}:{{.Tag}}|{{.ID}}|{{.CreatedAt}}`, one
    # image per line: sl-modernuo:cc-p62|b04ed61adb5a|2026-10-05 14:19:04 -0500 CDT. Newest first.
    param([string]$Text)
    $out = foreach ($l in @((Remove-Ansi $Text) -split "`n")) {
        if ($l.Trim() -notmatch '^([^|\s]+)\|([0-9a-f]{12,})\|(\d{4}-\d\d-\d\d) (\d\d:\d\d:\d\d) ([+-]\d\d)(\d\d)') { continue }
        $created = [DateTimeOffset]::Parse($Matches[3] + 'T' + $Matches[4] + $Matches[5] + ':' + $Matches[6], $script:Invariant).UtcDateTime
        [pscustomobject]@{ Tag = $Matches[1]; Id = (Format-ShortId $Matches[2]); Created = $created }
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
        'test.deploy'      { 'deploy to test' }
        default            { 'confirm live' }
    }
}

function Get-ConfirmSubject {
    # The typed-confirmation dialog's title: what the phrase is for.
    param([string]$Action)
    switch ($Action) {
        'commit.run'      { 'Commit and push' }
        'package.publish' { 'Publish player package' }
        'test.deploy'     { 'Deploy to test' }
        default           { 'LIVE shard' }
    }
}

function Get-ConfirmPreface {
    # D36: a stop saves nothing. When a plan stops a shard, say so first in the confirmation,
    # above the plan, where it is read before the phrase is typed rather than inside step 3.
    param($Plan)
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
        [string]$ImageTag, [switch]$CommitForce
    )
    $mode = 'Execute'
    if ($DryRun) { $mode = 'DryRun' }
    $a = '-NoProfile -ExecutionPolicy Bypass -NoExit -File "' + $SelfPath + '" -Mode ' + $mode + ' -Action ' + $ActionKey
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
    $a
}

function Resolve-RunMode {
    # 'dry', 'execute' or 'refuse' for a headless -Action. Any sign of a dry run wins. With no mode
    # at all, an action that changes something is refused rather than run, so a mode lost on the
    # way from the parent fails safe (D37). Actions that only read or open still run.
    param([string]$Action, [string]$Mode, [switch]$DryRun)
    if ($DryRun -or $Mode -eq 'DryRun') { return 'dry' }
    if ($Mode -eq 'Execute') { return 'execute' }
    $def = @(Get-ConsoleActions | Where-Object { $_.Key -eq $Action })[0]
    if ($def -and -not $def.Mutates) { return 'execute' }
    'refuse'
}

function Get-ModeBanner {
    # The first thing a spawned window says, before it reads any state or runs anything.
    param([string]$RunMode, [string]$Action, [string]$ShardKey)
    $bar = '=' * 78
    $what = $Action + ' (' + $ShardKey + ')'
    if ($Action -like 'commit.*') { $what = $Action + ' (both repos)' }
    if ($Action -like 'package.*') { $what = $Action + ' (player package)' }
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
        [pscustomobject]@{ Key = 'test.stop';        Group = 'Test shard'; Label = 'Stop';                                Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.rebuild';     Group = 'Test shard'; Label = 'Rebuild through the gates and start'; Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.deploy';      Group = 'Test shard'; Label = 'Deploy built image';                  Mutates = $true;  RunIn = 'window' }
        [pscustomobject]@{ Key = 'test.tail';        Group = 'Test shard'; Label = 'Tail the log';                        Mutates = $false; RunIn = 'here' }
        [pscustomobject]@{ Key = 'test.client';      Group = 'Test shard'; Label = 'Open a client';                       Mutates = $false; RunIn = 'here' }
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
    )
}

function New-PlanStep {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('confirm', 'ask', 'say', 'refuse', 'exec', 'script', 'checkzip', 'gatecheck', 'window', 'launch', 'move', 'copy', 'verify', 'stopped', 'remove-snapshot', 'log')]
        [string]$Kind,
        [string]$Text, [string]$Exe, [string[]]$Arguments, [string]$From, [string]$To,
        [string]$Path, [string]$Phrase, [string]$Container, [switch]$IfExists,
        [Collections.IDictionary]$Named
    )
    $a = @()
    if ($Arguments) { $a = @($Arguments) }
    # Named: a 'script' step's parameters, by name. The script runs in this process with them
    # splatted, so no command line sits between the value and the script (cc-P60).
    $n = [ordered]@{}
    if ($Named) { foreach ($k in $Named.Keys) { $n[$k] = $Named[$k] } }
    [pscustomobject]@{
        Kind = $Kind; Text = $Text; Exe = $Exe; Arguments = $a; From = $From; To = $To
        Path = $Path; Phrase = $Phrase; Container = $Container; IfExists = [bool]$IfExists; Named = $n
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
        if (-not $Facts.RepoError -and -not @($Facts.RepoPaths).Count) {
            $steps.Add((New-PlanStep -Kind say -Text ('Building from ' + $Facts.RepoPath + ' at ' + $Facts.Head + ', committed and clean. The zip goes to ' + $Config.PackageDist + '; nothing is published.')))
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

function Get-ActionPlan {
    # Every button is a plan: a list of steps, built here from the status and nothing else. The
    # shell runs it or, under -DryRun, prints it. So what -DryRun prints is exactly what runs.
    param(
        [string]$Action, [string]$ShardKey = 'test', $Config, $State,
        [string]$SnapshotName, [string]$Snapshot, [datetime]$Now = [datetime]::MinValue,
        [string]$CommitMessage, $CommitFacts,
        [string]$PackageNotes, [string]$PackageZip, $PackageFacts, [string]$ImageTag, [switch]$CommitForce
    )
    # The commit buttons read git and the notes, not docker, so they need no $State (cc-P60).
    if ($Action -like 'commit.*') { return (Get-CommitPlan -Action $Action -Config $Config -Facts $CommitFacts -Message $CommitMessage -Force:$CommitForce) }
    # Nor do the package buttons; they read git, dist\ and the notes (cc-P63, cc-P68).
    if ($Action -in @('package.build', 'package.publish', 'package.check')) { return (Get-PackagePlan -Action $Action -Config $Config -Facts $PackageFacts -Notes $PackageNotes -Zip $PackageZip) }
    if ($Action -eq 'test.deploy') { return (Get-DeployPlan -Config $Config -State $State -ImageTag $ImageTag) }
    if ($Now -eq [datetime]::MinValue) { $Now = $State.Now }
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
        if ($s.Kind -in @('exec', 'window')) {
            if ($line -match '\bcompose\b') { $v.Add('runs docker compose itself; compose is reached only through Start-TestShard.ps1: ' + $line) }
            if ($s.Exe -eq 'docker' -and @($s.Arguments).Count -and $s.Arguments[0] -in $forbidden) { $v.Add('docker ' + $s.Arguments[0] + ' is not something this console runs: ' + $line) }
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
            } else {
                $v.Add('the only script this console runs in-process is one of ' + $Config.CommitScript + ', ' + $Config.BuildPackage + ' and ' + $Config.PublishScript + ': ' + $call)
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
    foreach ($sh in $Config.Shards.Values) {
        $c = ConvertFrom-ContainerInspect $Sections['inspect ' + $sh.Container]
        if (-not $c.Exists -and $c.Error -match 'connect|pipe|daemon is not running|could not be started') { $dockerError = $c.Error }
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
            Save         = (Get-SaveSummary (ConvertFrom-FileListing $Sections['saves ' + $sh.Key]))
            Snapshots    = @(ConvertFrom-SnapshotListing $Sections['snapshots ' + $sh.Key])
            Asides       = $asides
        }
    }
    [pscustomobject]@{
        Now         = $now
        Source      = $Source
        Latest      = $latest
        # cc-P63: every sl-modernuo image, newest first, for Deploy built image.
        Images      = @(ConvertFrom-ImageTagListing ([string]$Sections['images sl-modernuo']))
        Compose     = (Test-LiveComposeFile $Sections['compose live'])
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
    if ($writers -contains 'test' -and -not ($writers -contains 'live')) {
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
    param($Shard, $ShardState, [datetime]$Now)
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

function Get-AtAGlance {
    # The three tiles: LIVE, TEST, and the status publisher. Same facts as the report below them.
    param($State, $Config)
    $tiles = New-Object 'System.Collections.Generic.List[object]'
    foreach ($k in @('live', 'test')) {
        $tiles.Add((Get-ShardGlance -Shard $Config.Shards[$k] -ShardState $State.Shards[$k] -Now $State.Now))
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
        if (-not $p.Writers.Count) { $pd += '; no shard has the status mount' }
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

function Format-StatusReport {
    # Lines with a colour name: normal, head, green, amber, red, gray.
    param($State, $Config)
    $o = New-Object 'System.Collections.Generic.List[object]'
    $add = { param($t, $c) $o.Add([pscustomobject]@{ Text = $t; Color = $(if ($c) { $c } else { 'normal' }) }) }
    $ind = '            '
    & $add ('Status as of ' + (Format-LocalTime $State.Now) + ', read from ' + $State.Source) 'gray'
    foreach ($t in (Get-AtAGlance $State $Config)) {
        & $add (('{0,-17}: {1,-15} {2}' -f $t.Title, $t.Word, $t.Detail)) $t.Color
    }
    if ($State.DockerError) { & $add ('DOCKER NOT REACHABLE: ' + $State.DockerError) 'red' }
    if ($State.Latest.Exists) {
        & $add ($Config.LatestTag + ' = ' + (Format-ShortId $State.Latest.Id) + ', built ' + (Format-LocalTime $State.Latest.Created) + ' (' + (Format-Age ($State.Now - $State.Latest.Created)) + ' ago)') 'normal'
    } else {
        & $add ('There is no ' + $Config.LatestTag + ' image.') 'red'
    }
    if ($State.Compose.Warning) { & $add ('LIVE COMPOSE FILE: ' + $State.Compose.Warning) 'amber' }

    foreach ($sh in $Config.Shards.Values) {
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
            # Syntax from docker/uo/SERVER.md:355-357; the twelve paths from notes/reachability-audit.md section 5.
            foreach ($f in @(
                'post-uoml/termur/Abyss.json', 'post-uoml/termur/TerMur.json', 'post-uoml/termur/Underworld.json',
                'shared/malas/Citadel.json', 'shared/malas/Labyrinth.json',
                'shared/trammel/Sanctuary.json', 'shared/felucca/Sanctuary.json',
                'shared/trammel/PrismOfLight.json', 'shared/felucca/PrismOfLight.json',
                'post-uoml/trammel/Vendors.json', 'post-uoml/felucca/Vendors.json',
                'post-uoml/trammel/Outdoors.json')) {
                New-StockStep ('[GenerateSpawners Data/Spawns/' + $f) 'Developer' 'pinned. A spawn file ported content comes through. Re-running replaces, not duplicates.'
            }
            (New-OrderStep 'GenerateNewShame')
            (New-OrderStep 'SetupDespise')
            (New-StockStep '[save' 'Administrator' 'pinned. Save after generation (docker/uo/SERVER.md:360).')
        )
    }
    [pscustomobject]@{
        Key    = 'newhaven'
        Title  = 'Existing world: New Haven services, NPCs and guild halls, in this order'
        Source = 'notes/cc-P17-playtest-bugs-1.md:624-637 (a fresh world runs the first two after the spawn files, :634-637)'
        Steps  = @(
            (New-OrderStep 'ClusterFSeedNewHavenServices' 'dryrun')
            (New-OrderStep 'ClusterFSeedNewHavenServices')
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
    } else {
        $map = @{ normal = 'Gray'; head = 'Cyan'; green = 'Green'; amber = 'Yellow'; red = 'Red'; gray = 'DarkGray' }
        Write-Host $Text -ForegroundColor $map[$Color]
    }
}

function Write-ConsoleLog {
    param($Config, [string]$Text)
    $line = '{0} {1} {2}' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant), $env:USERNAME, $Text
    [IO.File]::AppendAllText($Config.LogFile, $line + "`r`n", (New-Object Text.UTF8Encoding $false))
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
    foreach ($sh in $Config.Shards.Values) {
        $r = Invoke-DockerRead @('inspect', $sh.Container)
        $sec['inspect ' + $sh.Container] = ($r.Out + "`n" + $r.Err).Trim()
        $c = ConvertFrom-ContainerInspect $sec['inspect ' + $sh.Container]
        if ($c.Exists) {
            foreach ($ref in @($c.ConfigImage, $c.ImageId)) { if ($ref -and -not $refs.Contains($ref)) { $refs.Add($ref) } }
            $l = Invoke-DockerRead (Get-LogWindowArgs $c $Config.LogWindowMinutes)
            $sec['logs ' + $sh.Container] = ($l.Out + "`n" + $l.Err).Trim()
        }
    }
    foreach ($ref in $refs) {
        $r = Invoke-DockerRead @('image', 'inspect', $ref)
        $sec['image ' + $ref] = ($r.Out + "`n" + $r.Err).Trim()
    }
    # cc-P63: the images Deploy built image chooses from.
    $r = Invoke-DockerRead @('images', 'sl-modernuo', '--format', '{{.Repository}}:{{.Tag}}|{{.ID}}|{{.CreatedAt}}')
    $sec['images sl-modernuo'] = ($r.Out + "`n" + $r.Err).Trim()
    foreach ($sh in $Config.Shards.Values) {
        $sec['saves ' + $sh.Key] = Get-FileListing $sh.Saves
        $sec['snapshots ' + $sh.Key] = Get-SnapshotListingText (Join-Path $Config.SnapshotRoot $sh.Key)
        $parent = Split-Path $sh.Saves -Parent
        $leaf = Split-Path $sh.Saves -Leaf
        $sec['asides ' + $sh.Key] = @(Get-ChildItem -LiteralPath $parent -Directory -Filter ($leaf + '.aside-*') -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) -join "`n"
    }
    if (Test-Path -LiteralPath $Config.LiveCompose) { $sec['compose live'] = [IO.File]::ReadAllText($Config.LiveCompose) }
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
                    if ($code -ne 0) { throw ($line + ' exited with ' + $code) }
                }
                'script' {
                    Write-ConsoleLine ($tag + $s.Text) 'head'
                    $line = Format-ScriptCall $s.Path $s.Named
                    Write-ConsoleLine ('      > ' + $line) 'gray'
                    if (-not (Test-Path -LiteralPath $s.Path -PathType Leaf)) { throw ('not found: ' + $s.Path) }
                    if (-not $outFile) { $outFile = New-ExecOutputFile $Config $ActionKey $ShardKey }
                    $code = Invoke-ScriptStep -Path $s.Path -Named $s.Named -OutFile $outFile -Header ($tag + $line)
                    if ([IO.Path]::GetFullPath($s.Path) -ieq [IO.Path]::GetFullPath($Config.PublishScript)) {
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
                'window' {
                    Write-ConsoleLine ($tag + $s.Text) 'normal'
                    $cmd = "`$host.UI.RawUI.WindowTitle = '" + ((Format-CommandLine $s.Exe $s.Arguments) -replace "'", "''") + "'; " + (Format-CommandLine $s.Exe $s.Arguments)
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

# --- the form ------------------------------------------------------------------------------

function Add-RichLine {
    param($Box, [string]$Text, [string]$Color = 'normal')
    $map = @{
        normal = [Drawing.Color]::Black; head = [Drawing.Color]::Navy; green = [Drawing.Color]::ForestGreen
        amber = [Drawing.Color]::DarkOrange; red = [Drawing.Color]::Firebrick; gray = [Drawing.Color]::DimGray
    }
    $Box.SelectionStart = $Box.TextLength
    $Box.SelectionLength = 0
    $Box.SelectionColor = $map[$Color]
    if ($Color -eq 'head' -or $Color -eq 'red') { $Box.SelectionFont = $script:BoldFont } else { $Box.SelectionFont = $script:MonoFont }
    $Box.AppendText($Text + "`n")
    $Box.SelectionColor = $Box.ForeColor
}

function Get-TileColors {
    param([string]$Color)
    switch ($Color) {
        'green' { @([Drawing.Color]::FromArgb(222, 244, 226), [Drawing.Color]::FromArgb(20, 100, 40)) }
        'amber' { @([Drawing.Color]::FromArgb(255, 238, 204), [Drawing.Color]::FromArgb(150, 80, 0)) }
        'red'   { @([Drawing.Color]::FromArgb(250, 218, 218), [Drawing.Color]::FromArgb(150, 20, 20)) }
        default { @([Drawing.Color]::FromArgb(234, 234, 234), [Drawing.Color]::FromArgb(80, 80, 80)) }
    }
}

function New-StatusTile {
    param([string]$Title)
    $panel = New-Object Windows.Forms.Panel
    $panel.Dock = 'Fill'
    $panel.Margin = New-Object Windows.Forms.Padding(4)
    $panel.BorderStyle = 'FixedSingle'
    $word = New-Object Windows.Forms.Label
    $word.Dock = 'Top'
    $word.Height = 30
    $word.Font = New-Object Drawing.Font('Segoe UI', 13, [Drawing.FontStyle]::Bold)
    $word.Text = $Title + ': ...'
    $word.Padding = New-Object Windows.Forms.Padding(6, 4, 0, 0)
    $detail = New-Object Windows.Forms.Label
    $detail.Dock = 'Fill'
    $detail.Font = New-Object Drawing.Font('Segoe UI', 9)
    $detail.Padding = New-Object Windows.Forms.Padding(8, 2, 4, 0)
    $panel.Controls.Add($detail)
    $panel.Controls.Add($word)
    [pscustomobject]@{ Panel = $panel; Word = $word; Detail = $detail }
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
        $short = $t.Title
        if ($short -eq 'STATUS PUBLISHER') { $short = 'publisher' }
        $titleBits += ($short + ' ' + $t.Word.ToLower())
    }
    # The window title carries the same three words, so the taskbar shows them too.
    $prefix = 'Shard Console'
    if ($script:dry) { $prefix = 'Shard Console - DRY RUN' }
    if ($script:ui.Form) { $script:ui.Form.Text = $prefix + ' - ' + ($titleBits -join ' | ') }
}

function Update-ConsoleStatus {
    try {
        $state = Get-ConsoleState -Config $script:cfg -DryRun:$script:fromCapture -StatusFrom $script:statusFrom
        $script:lastState = $state
        Update-GlanceTiles $state
        $box = $script:ui.Status
        $box.Clear()
        foreach ($l in (Format-StatusReport $state $script:cfg)) { Add-RichLine $box $l.Text $l.Color }
        $box.SelectionStart = 0
        $box.ScrollToCaret()
        Update-SnapshotList
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
        $cols = Get-TileColors $t.Color
        $script:ui.NetTile.Panel.BackColor = $cols[0]
        $script:ui.NetTile.Word.ForeColor = $cols[1]
        $script:ui.NetTile.Detail.ForeColor = $cols[1]
        $script:ui.NetTile.Word.Text = $t.Title + ': ' + $t.Word
        $script:ui.NetTile.Detail.Text = $t.Detail
    }
    if (-not $script:NetProbeDisabled -and -not $script:netProbe -and (-not $script:netResultAt -or ([datetime]::UtcNow - $script:netResultAt).TotalSeconds -ge 30)) {
        Start-NetworkProbe
    }
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
    foreach ($s in @($script:lastState.Shards[$k].Snapshots)) {
        $kind = 'named'
        if ($s.IsPreRestore) { $kind = 'pre-restore' }
        $acc = 'yes'
        if (-not $s.HasAccounts) { $acc = 'NO' }
        $item = New-Object Windows.Forms.ListViewItem($s.Name)
        [void]$item.SubItems.Add($s.Taken.ToString('yyyy-MM-dd HH:mm:ss', $script:Invariant))
        [void]$item.SubItems.Add((Format-Bytes $s.Bytes))
        [void]$item.SubItems.Add([string]$s.Files)
        [void]$item.SubItems.Add($acc)
        [void]$item.SubItems.Add($kind)
        [void]$lv.Items.Add($item)
    }
    $lv.EndUpdate()
    $script:ui.SnapPath.Text = 'save: ' + $script:cfg.Shards[$k].Saves + '    snapshots: ' + (Join-Path $script:cfg.SnapshotRoot $k)
}

function Show-TypedConfirm {
    param([string]$Phrase, [string]$Text, $Plan, [string]$What = 'LIVE shard')
    $d = New-Object Windows.Forms.Form
    $d.Text = $What + ': type to confirm'
    if ($script:dry) { $d.Text = 'DRY RUN - ' + $What + ': type to confirm (the window it opens only prints)' }
    $d.Size = New-Object Drawing.Size(640, 420)
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
    $bottom.Height = 76
    $label = New-Object Windows.Forms.Label
    $label.Text = "Type exactly:  $Phrase"
    $label.Font = $script:BoldFont
    $label.ForeColor = [Drawing.Color]::Firebrick
    $label.SetBounds(10, 6, 600, 20)
    $tb = New-Object Windows.Forms.TextBox
    $tb.SetBounds(10, 30, 360, 24)
    $ok = New-Object Windows.Forms.Button
    $ok.Text = 'Do it'
    $ok.Enabled = $false
    $ok.DialogResult = 'OK'
    $ok.SetBounds(390, 28, 100, 28)
    $cancel = New-Object Windows.Forms.Button
    $cancel.Text = 'Cancel'
    $cancel.DialogResult = 'Cancel'
    $cancel.SetBounds(500, 28, 100, 28)
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
    $result = $d.ShowDialog()
    $typed = $tb.Text
    $d.Dispose()
    ($result -eq 'OK') -and (Test-TypedConfirmation $Phrase $typed)
}

function Show-ListPicker {
    # A small dialog: a list with columns, OK and Cancel. Returns the Tag of the row picked, or $null.
    param([string]$Title, [string]$Caption, [object[]]$Columns, [object[]]$Rows)
    $d = New-Object Windows.Forms.Form
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
    $result = $d.ShowDialog()
    $pick = $null
    if ($result -eq 'OK' -and $lv.SelectedItems.Count) { $pick = [string]$lv.SelectedItems[0].Tag }
    $d.Dispose()
    $pick
}

function Show-ImagePicker {
    # Test shard > Deploy built image: which sl-modernuo:cc-p* to put on the test shard (cc-P63).
    param($State)
    $rows = @(Get-DeployCandidates $State $script:cfg | ForEach-Object {
        $now = ''
        if ($_.IsLatest) { $now = '<- latest now' }
        [pscustomobject]@{ Tag = $_.Tag; Cells = @($_.Tag, $_.Id, (Format-LocalTime $_.Created), $now) }
    })
    if (-not $rows.Count) { Write-ConsoleLine 'test.deploy: there is no sl-modernuo:cc-p* image to deploy (docker images sl-modernuo).' 'amber'; return $null }
    $was = '(none)'
    if ($State.Latest.Exists) { $was = Format-ShortId $State.Latest.Id }
    Show-ListPicker -Title 'Deploy built image to the TEST shard' -Caption ($script:cfg.LatestTag + ' is ' + $was + ' now. Pick the image to point it at; the test shard is restarted on it after you type the phrase.') -Columns @(@('Image', 260), @('Id', 120), @('Created', 160), @('', 120)) -Rows $rows
}

function Update-PackageList {
    # The Player package tab's list of zips in dist\, newest first.
    $lv = $script:ui.PackageList
    if (-not $lv) { return }
    $lv.BeginUpdate()
    $lv.Items.Clear()
    foreach ($z in @(Get-PackageZips $script:cfg.PackageDist)) {
        $ver = '(no version.json)'; $nt = ''
        if ($z.Json.Exists) { $ver = [string]$z.Json.Version; $nt = [string]$z.Json.Notes; if ($z.Json.Error) { $ver = '(version.json unreadable)' } }
        $item = New-Object Windows.Forms.ListViewItem($z.Name)
        [void]$item.SubItems.Add($ver)
        [void]$item.SubItems.Add((Format-Bytes $z.Bytes))
        [void]$item.SubItems.Add((Format-LocalTime $z.LastWriteUtc))
        [void]$item.SubItems.Add($nt)
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
        try { $script:commitFactsCache = Get-CommitFacts $script:cfg; $script:commitFactsAt = $now }
        catch { $ui.CommitCountdown.Text = 'Could not read the repos: ' + $_.Exception.Message; $ui.CommitCountdown.ForeColor = [Drawing.Color]::Firebrick; return }
    }
    $t = Get-CommitCountdown -Facts $script:commitFactsCache -Config $script:cfg -Now $now -Force:$ui.CommitForce.Checked
    if ($ui.CommitCountdown.Text -ne $t) { $ui.CommitCountdown.Text = $t }
    if ($t -eq 'Ready to commit') { $ui.CommitCountdown.ForeColor = [Drawing.Color]::ForestGreen } else { $ui.CommitCountdown.ForeColor = [Drawing.Color]::DarkOrange }
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
        if ($On) { $box.BackColor = [Drawing.Color]::FromArgb(255, 238, 204); $box.ForeColor = [Drawing.Color]::FromArgb(150, 80, 0) }
        else { $box.BackColor = [Drawing.Color]::Transparent; $box.ForeColor = [Drawing.Color]::Black }
    }
    if ($script:lastState) { Update-GlanceTiles $script:lastState }
    if ($changed) {
        if ($On) { Write-ConsoleLine 'DRY RUN on: buttons print the plan, and a window action opens a window that only prints. Nothing runs.' 'amber' }
        else { Write-ConsoleLine 'DRY RUN off: buttons run for real.' 'head' }
    }
}

function Show-YesNo {
    param([string]$Text)
    $title = 'Shard Console'
    if ($script:dry) { $title = 'Shard Console - DRY RUN' }
    [string][Windows.Forms.MessageBox]::Show($Text, $title, 'YesNo', 'Warning', 'Button2')
}

function Invoke-ConsoleButton {
    param([string]$ActionKey, [string]$ShardKey, [string]$SnapshotName, [string]$Snapshot, [string]$CommitMessage,
        [string]$PackageNotes, [string]$PackageZip, [string]$ImageTag, [switch]$CommitForce)
    try {
        $def = @(Get-ConsoleActions | Where-Object { $_.Key -eq $ActionKey })[0]
        $isCommit = ($ActionKey -like 'commit.*')
        $isPackage = ($ActionKey -like 'package.*')
        if (-not $ShardKey) { $ShardKey = 'test'; if ($ActionKey -like 'live.*') { $ShardKey = 'live' }; if ($isCommit) { $ShardKey = 'repos' }; if ($isPackage) { $ShardKey = 'package' } }
        # Two flags since the toggle: $script:dry is "buttons print", $script:fromCapture is "the
        # status is a capture file". Only the second decides where the state comes from.
        if ($script:fromCapture -and -not $script:dry) { throw 'the status is from a capture file, so nothing may run for real. Relaunch without -DryRun.' }
        $facts = $null
        $pkgFacts = $null
        $now = Get-Date
        if ($isCommit) {
            # The commit buttons read git and the notes, never docker (cc-P60).
            $facts = Get-CommitFacts $script:cfg
        } elseif ($isPackage) {
            # So do the package buttons, and dist\ (cc-P63).
            $pkgFacts = Get-PackageFacts $script:cfg $PackageZip
        } else {
            $state = Get-ConsoleState -Config $script:cfg -DryRun:$script:fromCapture -StatusFrom $script:statusFrom
            $script:lastState = $state
            if ($script:fromCapture) { $now = $state.Now.ToLocalTime() }
        }
        if ($ActionKey -eq 'test.deploy' -and -not $ImageTag) {
            # cc-P63 Part B: the picker lists sl-modernuo:cc-p* from the state just read.
            $ImageTag = Show-ImagePicker $state
            if (-not $ImageTag) { Write-ConsoleLine ($ActionKey + ': cancelled.') 'gray'; return }
        }
        $plan = @(Get-ActionPlan -Action $ActionKey -ShardKey $ShardKey -Config $script:cfg -State $state -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now -CommitMessage $CommitMessage -CommitFacts $facts -PackageNotes $PackageNotes -PackageZip $PackageZip -PackageFacts $pkgFacts -ImageTag $ImageTag -CommitForce:$CommitForce)
        $viol = @(Test-PlanSafety $plan $script:cfg)
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
        if ($def.RunIn -eq 'window') {
            $argLine = Get-ChildArgumentLine -SelfPath $script:SelfPath -ActionKey $ActionKey -ShardKey $ShardKey -SnapshotName $SnapshotName -Snapshot $Snapshot -ConfirmLive $phrase -Yes:$answeredYes -DryRun:$script:dry -StatusFrom $script:statusFrom -StatusFromDocker:(-not $script:fromCapture) -CommitMessage $CommitMessage -PackageNotes $PackageNotes -PackageZip $PackageZip -ImageTag $ImageTag -CommitForce:($CommitForce -and $ActionKey -eq 'commit.run')
            Start-Process -FilePath $script:cfg.PowerShell -ArgumentList $argLine | Out-Null
            if ($script:dry) { Write-ConsoleLine ($ActionKey + ' (' + $ShardKey + '): DRY RUN in its own window. It prints the plan and runs nothing.') 'amber' }
            else { Write-ConsoleLine ($ActionKey + ' (' + $ShardKey + '): running in its own window. Refresh the status when it finishes.') 'head' }
        } else {
            if ($script:dry) { throw 'a dry run reached the executor' }
            [void](Invoke-Plan -Plan $plan -Config $script:cfg -ActionKey $ActionKey -ShardKey $ShardKey -ConfirmLive $phrase -Yes:$answeredYes)
        }
    } catch {
        Write-ConsoleLine ($ActionKey + ' failed: ' + $_.Exception.Message) 'red'
    }
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

function New-ConsoleForm {
    param($Config, [switch]$DryRun, [string]$StatusFrom)
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [Windows.Forms.Application]::EnableVisualStyles()
    $script:cfg = $Config
    $script:dry = [bool]$DryRun
    $script:fromCapture = [bool]$DryRun
    $script:statusFrom = $StatusFrom
    $script:lastState = $null
    $script:ui = @{}
    $script:MonoFont = New-Object Drawing.Font('Consolas', 9)
    $script:BoldFont = New-Object Drawing.Font('Consolas', 9, [Drawing.FontStyle]::Bold)

    $form = New-Object Windows.Forms.Form
    $form.Text = 'Shard Console'
    if ($DryRun) { $form.Text = 'Shard Console - DRY RUN (buttons print, status from a capture file)' }
    $form.Size = New-Object Drawing.Size(1200, 1000)
    $form.MinimumSize = New-Object Drawing.Size(900, 700)
    $form.StartPosition = 'CenterScreen'
    $form.Font = New-Object Drawing.Font('Segoe UI', 9)

    $script:ui.Form = $form

    $grid = New-Object Windows.Forms.TableLayoutPanel
    $grid.Dock = 'Fill'
    $grid.RowCount = 4
    $grid.ColumnCount = 1
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Absolute', 84)))
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', 48)))
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', 32)))
    [void]$grid.RowStyles.Add((New-Object Windows.Forms.RowStyle('Percent', 20)))

    # at a glance: what is running, big enough to read from across the room
    $glance = New-Object Windows.Forms.TableLayoutPanel
    $glance.Dock = 'Fill'
    $glance.RowCount = 1
    $glance.ColumnCount = 4
    $glance.Margin = New-Object Windows.Forms.Padding(0)
    foreach ($i in 1..4) { [void]$glance.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent', 25))) }
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
    $glance.Controls.Add($script:ui.NetTile.Panel, $col, 0)
    $script:netProbe = $null; $script:netResult = $null; $script:netResultAt = $null

    # status
    $statusGroup = New-Object Windows.Forms.GroupBox
    $statusGroup.Text = 'Status'
    $statusGroup.Dock = 'Fill'
    $bar = New-Object Windows.Forms.FlowLayoutPanel
    $bar.Dock = 'Top'
    $bar.Height = 34
    $refresh = New-Object Windows.Forms.Button
    $refresh.Text = 'Refresh'
    $refresh.Width = 100
    $refresh.Add_Click({ Update-ConsoleStatus })
    $auto = New-Object Windows.Forms.CheckBox
    $auto.Text = 'refresh every 30 s'
    $auto.AutoSize = $true
    $auto.Checked = -not $DryRun
    $auto.Margin = New-Object Windows.Forms.Padding(12, 8, 3, 3)
    $dryBox = New-Object Windows.Forms.CheckBox
    $dryBox.Text = 'DRY RUN: buttons print, nothing runs'
    $dryBox.AutoSize = $true
    $dryBox.Font = New-Object Drawing.Font('Segoe UI', 9, [Drawing.FontStyle]::Bold)
    $dryBox.Margin = New-Object Windows.Forms.Padding(24, 8, 3, 3)
    if ($DryRun) {
        # Launched with -DryRun the status is a capture file, so this stays on.
        $dryBox.Text = 'DRY RUN (locked on: the status is from a capture file)'
        $dryBox.Enabled = $false
    }
    $dryBox.Add_CheckedChanged({ Set-ConsoleDryRun $this.Checked })
    $bar.Controls.AddRange(@($refresh, $auto, $dryBox))
    $status = New-Object Windows.Forms.RichTextBox
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

    $testPage = New-ButtonPage 'Test shard' 'The throwaway test shard (sl-modernuo-test, port 2594, its own save in modernuo-test\Saves). No confirmation: it is throwaway. Start, Stop and Rebuild call docker/uo/Start-TestShard.ps1; Rebuild is the only one that builds, and it builds through build.sh and its gates. Deploy built image (cc-P63) points sl-modernuo:latest at an image a prompt already built through the gates (sl-modernuo:cc-pNN), after the phrase ''deploy to test'', then stops and starts the test shard on it. It never touches the live container.' @(
        (New-ConsoleButton 'Start' 'test.start'),
        (New-ConsoleButton 'Start fresh (old save moved aside)' 'test.fresh'),
        (New-ConsoleButton 'Stop' 'test.stop'),
        (New-ConsoleButton 'Rebuild through the gates and start' 'test.rebuild' 300),
        (New-ConsoleButton 'Deploy built image...' 'test.deploy'),
        (New-ConsoleButton 'Tail the log' 'test.tail'),
        (New-ConsoleButton 'Open a client' 'test.client')
    )

    # snapshots
    $snapPage = New-Object Windows.Forms.TabPage('Snapshots')
    $snapTop = New-Object Windows.Forms.FlowLayoutPanel
    $snapTop.Dock = 'Top'
    $snapTop.Height = 32
    $rTest = New-Object Windows.Forms.RadioButton
    $rTest.Text = 'Test shard'
    $rTest.Checked = $true
    $rTest.AutoSize = $true
    $rLive = New-Object Windows.Forms.RadioButton
    $rLive.Text = 'LIVE shard (every action asks you to type a phrase)'
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
    $nameLabel = New-Object Windows.Forms.Label
    $nameLabel.Text = 'Name:'
    $nameLabel.AutoSize = $true
    $nameLabel.Margin = New-Object Windows.Forms.Padding(6, 14, 3, 3)
    $nameBox = New-Object Windows.Forms.TextBox
    $nameBox.Width = 220
    $nameBox.Margin = New-Object Windows.Forms.Padding(3, 10, 3, 3)
    $create = New-Object Windows.Forms.Button
    $create.Text = 'Create named snapshot'
    $create.Width = 170
    $create.Height = 32
    $create.Add_Click({ Invoke-ConsoleButton -ActionKey 'snapshot.create' -ShardKey (Get-SelectedSnapshotShard) -SnapshotName $script:ui.SnapName.Text.Trim() })
    $restore = New-Object Windows.Forms.Button
    $restore.Text = 'Restore selected'
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
    $worldTable = New-Object Windows.Forms.TableLayoutPanel
    $worldTable.Dock = 'Fill'
    $worldTable.AutoScroll = $true
    $worldTable.ColumnCount = 3
    [void]$worldTable.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('AutoSize')))
    [void]$worldTable.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('AutoSize')))
    [void]$worldTable.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent', 100)))
    $worldCmds = @()
    try { $worldCmds = @(ConvertFrom-CommandSources (Get-CommandSourceFiles $Config.Customizations)) } catch { }
    $head = New-Caption ('In-game commands, run by hand in a client. The console cannot run them and cannot see whether this world has had them: that is a fact about the save. Read from the [ShardCommand] each command declares in ' + $Config.Customizations + ' (' + $worldCmds.Count + ' registrations; the tab shows only those that change a world). Fresh-world generation is a different job from changing a world that already exists: check which shard your client is on before you paste. Each row is one command: its Copy puts that line on the clipboard, and a dry run is its own row just above the command it previews. Copy group puts the whole group, one line each, for a text file beside the client. The ticks are for this session only.') 1100
    $worldTable.Controls.Add($head, 0, 0)
    $worldTable.SetColumnSpan($head, 3)
    $row = 1
    $copyClick = { [Windows.Forms.Clipboard]::SetText([string]$this.Tag); Write-ConsoleLine ('copied: ' + ([string]$this.Tag).Trim()) 'gray' }
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
            $cb.AutoSize = $true
            $cb.Font = $script:MonoFont
            $cb.Margin = New-Object Windows.Forms.Padding(6, 5, 3, 3)
            $copy = New-Object Windows.Forms.Button
            $copy.Text = 'Copy'
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
    $worldPage.Controls.Add($worldTable)

    $diagPage = New-ButtonPage 'Diagnostics' ('Each writes one timestamped file in ' + $Config.DiagFolder + ' (check-HHmmss.txt, shard-HHmmss.txt). Check-Shard.ps1 reads the TEST container only.') @(
        (New-ConsoleButton 'Run Check-Docker.ps1' 'diag.docker'),
        (New-ConsoleButton 'Run Check-Shard.ps1' 'diag.shard'),
        (New-ConsoleButton 'Open the folder they write to' 'diag.folder')
    )

    $livePage = New-ButtonPage 'LIVE shard' 'LIVE: sl-modernuo, the real world save, real players. Every button here asks you to type a phrase first. Start, Stop and Restart are docker start / stop / restart on the existing container; this console never runs compose on the live file (D29) and never deploys. Whether a stop saves (D36) depends on what the container runs; each plan says which.' @(
        (New-ConsoleButton 'Start' 'live.start'),
        (New-ConsoleButton 'Stop' 'live.stop'),
        (New-ConsoleButton 'Restart' 'live.restart'),
        (New-ConsoleButton 'Tail the log' 'live.tail'),
        (New-ConsoleButton 'Open a client' 'live.client')
    ) ([Drawing.Color]::Firebrick)

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
    $commitPage = New-ButtonPage 'Commit' ('Runs ' + $where + ' in a window of its own, which stays open so you can read its own table at the end. Preview passes its -DryRun and changes nothing. Commit and push asks for the phrase ''commit shard work'' first: both repos are public on GitHub, so a pushed commit cannot be taken back. It is refused when this console runs as SYSTEM, when both repos are clean, or while a CC prompt has not said DONE or a pending file changed in the last ' + (Format-QuietWindow $Config.CommitQuietSeconds) + '; Preview still runs and names them. Force skips only that last wait, for one commit; the confirmation names the files.') $commitBtns
    $msgLabel = New-Caption 'Message (blank: the script''s default, "Shard work, <date> <time>"):' 400
    $msgBox = New-Object Windows.Forms.TextBox
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
    $pubWhere = $Config.PublishScript
    if (-not (Test-Path -LiteralPath $Config.PublishScript -PathType Leaf)) { $pubWhere += ' (NOT FOUND: Publish will say so and do nothing)' }
    $pkgCap = New-Caption ('Build package runs player-package\Build-PlayerPackage.ps1 -Notes, then its -CheckZip on the new zip, in a window of its own; nothing is published, and it is refused while D:\ShatteredLegacy has uncommitted changes. Publish package runs ' + $pubWhere + ' -Zip on the zip chosen below, after the phrase ''publish player package'' (it is public: every launcher is offered it), and the script then asks you to type yes in its window.') 1100
    $pkgTop.Controls.Add($pkgCap)
    $pkgTop.SetFlowBreak($pkgCap, $true)
    $notesLabel = New-Caption 'Notes ("What is new", one line):' 300
    $notesBox = New-Object Windows.Forms.TextBox
    $notesBox.Width = 600
    $notesBox.Margin = New-Object Windows.Forms.Padding(6)
    $buildBtn = New-Object Windows.Forms.Button
    $buildBtn.Text = 'Build package'
    $buildBtn.Tag = 'package.build'
    $buildBtn.Width = 160
    $buildBtn.Height = 30
    $buildBtn.Add_Click({ Invoke-ConsoleButton -ActionKey 'package.build' -PackageNotes $script:ui.PackageNotes.Text.Trim() })
    $pkgTop.Controls.AddRange(@($notesLabel, $notesBox, $buildBtn))
    $pkgList = New-Object Windows.Forms.ListView
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
    $pkgRefresh = New-Object Windows.Forms.Button
    $pkgRefresh.Text = 'Refresh list'
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
    $pkgPage.Controls.Add($pkgList)
    $pkgPage.Controls.Add($pkgTop)
    $pkgPage.Controls.Add($pkgBottom)
    $script:ui.PackageNotes = $notesBox
    $script:ui.PackageList = $pkgList
    $script:ui.PackageButtons = @($buildBtn, $publishBtn, $checkBtn)
    Update-PackageList

    $tabs.TabPages.AddRange(@($testPage, $snapPage, $worldPage, $diagPage, $commitPage, $pkgPage, $livePage))

    # output
    $outGroup = New-Object Windows.Forms.GroupBox
    $outGroup.Text = 'Output'
    $outGroup.Dock = 'Fill'
    $out = New-Object Windows.Forms.RichTextBox
    $out.Dock = 'Fill'
    $out.ReadOnly = $true
    $out.BackColor = [Drawing.Color]::White
    $out.Font = $script:MonoFont
    $out.DetectUrls = $false
    $outGroup.Controls.Add($out)
    $script:OutputBox = $out

    $grid.Controls.Add($glance, 0, 0)
    $grid.Controls.Add($statusGroup, 0, 1)
    $grid.Controls.Add($tabs, 0, 2)
    $grid.Controls.Add($outGroup, 0, 3)
    $form.Controls.Add($grid)

    $timer = New-Object Windows.Forms.Timer
    $timer.Interval = 30000
    $timer.Add_Tick({ if ($script:ui.Auto.Checked) { Update-ConsoleStatus } })
    $timer.Start()
    # The DNS tile: a 1 s tick that only collects a finished check and starts the next every 30 s.
    $netTimer = New-Object Windows.Forms.Timer
    $netTimer.Interval = 1000
    $netTimer.Add_Tick({ Update-NetworkTile })
    $netTimer.Start()
    # cc-P68: the Commit tab's countdown. A 1 s tick that does nothing unless the Commit tab is the
    # one showing; the repos are re-read every CommitPollSeconds, so a new change restarts it.
    $cdTimer = New-Object Windows.Forms.Timer
    $cdTimer.Interval = 1000
    $cdTimer.Add_Tick({ Update-CommitCountdown })
    $cdTimer.Start()
    $tabs.Add_SelectedIndexChanged({ Update-CommitCountdown -Reread })
    $script:commitFactsCache = $null
    $script:commitFactsAt = $null
    $form.Add_FormClosed({
        $script:ui.Timer.Stop(); $script:ui.NetTimer.Stop(); $script:ui.CountdownTimer.Stop(); $script:OutputBox = $null
        if ($script:netProbe) { try { $script:netProbe.PS.Stop(); $script:netProbe.PS.Dispose() } catch { } }
    })
    $script:ui.Timer = $timer
    $script:ui.NetTimer = $netTimer
    $script:ui.CountdownTimer = $cdTimer
    Update-NetworkTile

    if ($DryRun) { Write-ConsoleLine 'DRY RUN: every button prints the plan it would run. The status panel is read from a capture file, not docker.' 'amber' }
    Set-ConsoleDryRun ([bool]$DryRun)
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
    foreach ($k in @($sec.Keys)) { if ($k -like 'logs *') { $sec[$k] = Remove-AccountLines $sec[$k] } }
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
    Write-Host 'test.deploy takes -ImageTag sl-modernuo:cc-pNN and -ConfirmLive ''deploy to test''.'
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
    foreach ($l in (Format-StatusReport $st $config)) { Write-ConsoleLine $l.Text $l.Color }
    exit 0
}

if ($Action) {
    $sk = $Shard
    if ($Action -like 'live.*') { $sk = 'live' }
    if ($Action -like 'test.*') { $sk = 'test' }
    if ($Action -like 'commit.*') { $sk = 'repos' }
    if ($Action -like 'package.*') { $sk = 'package' }
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
    $now = Get-Date
    if ($Action -like 'commit.*') {
        # From a click the message comes as base64 (Get-ChildArgumentLine); -CommitMessage is for a person.
        if ($CommitMessageB64) { $CommitMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($CommitMessageB64)) }
        $facts = Get-CommitFacts $config
    } elseif ($Action -like 'package.*') {
        # cc-P63: the notes come as base64 from a click, as a commit message does.
        if ($PackageNotesB64) { $PackageNotes = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($PackageNotesB64)) }
        $pkgFacts = Get-PackageFacts $config $PackageZip
    } else {
        $fromCapture = $isDry -and -not $StatusFromDocker
        $st = Get-ConsoleState -Config $config -DryRun:$fromCapture -StatusFrom $StatusFrom
        if ($fromCapture) { $now = $st.Now.ToLocalTime() }
    }
    $plan = @(Get-ActionPlan -Action $Action -ShardKey $Shard -Config $config -State $st -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now -CommitMessage $CommitMessage -CommitFacts $facts -PackageNotes $PackageNotes -PackageZip $PackageZip -PackageFacts $pkgFacts -ImageTag $ImageTag -CommitForce:$CommitForce)
    $viol = @(Test-PlanSafety $plan $config)
    $header = 'PLAN'
    if ($isDry) { $header = 'DRY RUN' }
    foreach ($l in (Format-Plan -Action $Action -ShardKey $sk -Plan $plan -Violations $viol -Header $header)) { Write-ConsoleLine $l 'normal' }
    if ($isDry) { Write-ConsoleLine '   (dry run: nothing was run)' 'amber'; exit 0 }
    Write-ConsoleLine '' 'normal'
    $ok = Invoke-Plan -Plan $plan -Config $config -ActionKey $Action -ShardKey $sk -ConfirmLive $ConfirmLive -Yes:$Yes
    if ($ok) { Write-ConsoleLine ($Action + ': done.') 'green'; exit 0 }
    # cc-P68: a declined publish already said "Publish cancelled. Nothing was published." last.
    if ($script:PlanCancelled) { exit 3 }
    exit 1
}

$form = New-ConsoleForm -Config $config -DryRun:$DryRun -StatusFrom $StatusFrom
[void]$form.ShowDialog()
$form.Dispose()
