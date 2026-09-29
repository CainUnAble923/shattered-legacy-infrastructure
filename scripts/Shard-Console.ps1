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

  AT A GLANCE. Three tiles across the top (and the window title) say what is running: LIVE, TEST,
  and the STATUS PUBLISHER (the shard's status.json for the website, docker/uo-status/README.md).
  The publisher is judged by the file's generatedAt, fresh within 3 minutes, never by a claim in
  the file. The tile also says whether sl-uo-status is serving it on 8092.

  IT CALLS THE EXISTING SCRIPTS AND DOES NOT REIMPLEMENT THEM.
    docker/uo/Start-TestShard.ps1  starts, stops and rebuilds the test shard, seeds its accounts
                                   (D19) and waits for the listener.
    docker/uo/build.sh             reached only through Start-TestShard.ps1. It is the only thing
                                   that runs the gates.
    Check-Docker.ps1, Check-Shard.ps1   the diagnostics, as they are.

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
    [string]$RepoRoot
)

# =============================================================================================
# CORE. Strings in, objects or strings out. No docker, no files, no WinForms below this line
# until the SHELL marker, so every function here can be given captured output and checked.
# =============================================================================================

$script:Invariant       = [Globalization.CultureInfo]::InvariantCulture
$script:CaptureMarker   = '##### SHARD-CONSOLE-CAPTURE '
$script:SnapshotPattern = '^(\d{8}-\d{6})_([A-Za-z0-9._-]+)$'
$script:NoSaveWarning   = 'A stop does not save: play since the last autosave (every 5 minutes) is lost. [save in game first if that matters.'

function Get-ConsoleConfig {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)
    $lib = Join-Path $RepoRoot 'server\lib\uo\modernuo'
    $shards = [ordered]@{
        test = [pscustomobject]@{
            Key = 'test'; Label = 'TEST'; Container = 'sl-modernuo-test'; IsLive = $false
            Saves = (Join-Path $lib 'Saves-test'); GamePort = 2594
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
        MountTargets = @()
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
        if ($c.Mounts) { $rec.MountTargets = @($c.Mounts | ForEach-Object { [string]$_.Destination } | Where-Object { $_ }) }
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
        default            { 'confirm live' }
    }
}

function Get-ConfirmPreface {
    # D36: a stop saves nothing. When a plan stops a shard, say so first in the confirmation,
    # above the plan, where it is read before the phrase is typed rather than inside step 3.
    param($Plan)
    if (@($Plan | Where-Object { $_.Text -and $_.Text.Contains($script:NoSaveWarning) }).Count) {
        return ('BEFORE YOU TYPE: ' + $script:NoSaveWarning + ' (D36)')
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
        [switch]$StatusFromDocker
    )
    $mode = 'Execute'
    if ($DryRun) { $mode = 'DryRun' }
    $a = '-NoProfile -ExecutionPolicy Bypass -NoExit -File "' + $SelfPath + '" -Mode ' + $mode + ' -Action ' + $ActionKey + ' -Shard ' + $ShardKey
    # A dry child reads a capture file unless told otherwise. When the parent's status came from
    # docker (the toggle, not -DryRun), the child reads docker too, so it prints the same plan.
    if ($DryRun -and $StatusFromDocker) { $a += ' -StatusFromDocker' }
    elseif ($DryRun -and $StatusFrom) { $a += ' -StatusFrom "' + $StatusFrom + '"' }
    if ($SnapshotName) { $a += ' -SnapshotName ' + $SnapshotName }
    if ($Snapshot) { $a += ' -Snapshot ' + $Snapshot }
    if ($ConfirmLive) { $a += ' -ConfirmLive "' + $ConfirmLive + '"' }
    if ($Yes) { $a += ' -Yes' }
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
    switch ($RunMode) {
        'dry' {
            [pscustomobject]@{ Color = 'amber'; Title = ('DRY RUN - ' + $what + ' - Shard Console'); Lines = @(
                $bar, ('  DRY RUN  ' + $what), '  Nothing will be run. This window only prints the plan.', $bar) }
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
    )
}

function New-PlanStep {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('confirm', 'ask', 'say', 'refuse', 'exec', 'window', 'launch', 'move', 'copy', 'verify', 'stopped', 'remove-snapshot', 'log')]
        [string]$Kind,
        [string]$Text, [string]$Exe, [string[]]$Arguments, [string]$From, [string]$To,
        [string]$Path, [string]$Phrase, [string]$Container, [switch]$IfExists
    )
    $a = @()
    if ($Arguments) { $a = @($Arguments) }
    [pscustomobject]@{
        Kind = $Kind; Text = $Text; Exe = $Exe; Arguments = $a; From = $From; To = $To
        Path = $Path; Phrase = $Phrase; Container = $Container; IfExists = [bool]$IfExists
    }
}

function Get-ActionPlan {
    # Every button is a plan: a list of steps, built here from the status and nothing else. The
    # shell runs it or, under -DryRun, prints it. So what -DryRun prints is exactly what runs.
    param(
        [string]$Action, [string]$ShardKey = 'test', $Config, $State,
        [string]$SnapshotName, [string]$Snapshot, [datetime]$Now = [datetime]::MinValue
    )
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
            $steps.Add((New-PlanStep -Kind say -Text ('Start fresh. Start-TestShard.ps1 -Fresh would DELETE Saves-test, and no button here deletes a world save, so instead: stop the test shard, move Saves-test aside, and start without -Fresh. Start-TestShard.ps1 then finds no Saves-test and seeds a new one exactly as -Fresh does (accounts and SerializedTypes.db from the live save, D19). No build.')))
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
            if (@($s.Arguments) -contains '-Fresh') { $v.Add('-Fresh deletes Saves-test; this console moves it aside instead') }
            if (@($s.Arguments) -contains $live.Container -and -not $confirmed) { $v.Add('touches ' + $live.Container + ' without a typed confirmation: ' + $line) }
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
            Save         = (Get-SaveSummary (ConvertFrom-FileListing $Sections['saves ' + $sh.Key]))
            Snapshots    = @(ConvertFrom-SnapshotListing $Sections['snapshots ' + $sh.Key])
            Asides       = $asides
        }
    }
    [pscustomobject]@{
        Now         = $now
        Source      = $Source
        Latest      = $latest
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
        if ($c.Exists -and (@($c.MountTargets) -contains $p.MountTarget)) { $writers += $k }
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
    $lines.Insert(0, 'written by : ' + $wtext + ' (containers with ' + $p.MountTarget + ' mounted)')
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

function Get-WorldSetupChecklist {
    # A fresh world does not populate itself: notes/reachability-audit.md section 5 found 282 of
    # the 296 reachable ported content types exist only where someone ran these by hand. The
    # console cannot run in-game commands; it can stop them being forgotten.
    # Syntax from docker/uo/SERVER.md:355-357. Access levels from the source that registers them.
    $spawn = @(
        'post-uoml/termur/Abyss.json', 'post-uoml/termur/TerMur.json', 'post-uoml/termur/Underworld.json',
        'shared/malas/Citadel.json', 'shared/malas/Labyrinth.json',
        'shared/trammel/Sanctuary.json', 'shared/felucca/Sanctuary.json',
        'shared/trammel/PrismOfLight.json', 'shared/felucca/PrismOfLight.json',
        'post-uoml/trammel/Vendors.json', 'post-uoml/felucca/Vendors.json',
        'post-uoml/trammel/Outdoors.json'
    )
    foreach ($f in $spawn) {
        [pscustomobject]@{ Command = '[GenerateSpawners Data/Spawns/' + $f; Access = 'Developer'; Why = 'a spawn file ported content comes through. Re-running replaces, not duplicates.' }
    }
    [pscustomobject]@{ Command = '[GenerateNewShame'; Access = 'Administrator'; Why = 'Shame Revamped on Trammel and Felucca: altars, walls, spawners. Nothing in the spawn data hints at it. Safe to re-run: it skips what exists.' }
    [pscustomobject]@{ Command = '[SetupDespise'; Access = 'GameMaster'; Why = 'Despise: controller, ankhs, gates, spawners. Nothing in the spawn data hints at it. Refuses to run twice.' }
    [pscustomobject]@{ Command = '[save'; Access = 'Administrator'; Why = 'save after generation (docker/uo/SERVER.md:360).' }
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
    $psi = New-Object Diagnostics.ProcessStartInfo 'docker'
    $psi.Arguments = (@($Arguments | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    try { $p = [Diagnostics.Process]::Start($psi) }
    catch { return [pscustomobject]@{ Out = ''; Err = ('error: docker could not be started: ' + $_.Exception.Message); Code = -1 } }
    $o = $p.StandardOutput.ReadToEndAsync()
    $e = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($TimeoutMs)) {
        try { $p.Kill() } catch { }
        return [pscustomobject]@{ Out = ''; Err = ('error: docker did not answer within ' + ($TimeoutMs / 1000) + ' s'); Code = -1 }
    }
    [pscustomobject]@{ Out = $o.Result; Err = $e.Result; Code = $p.ExitCode }
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
            Write-ConsoleLine ("Refused: this is a live action and needs the typed confirmation '" + $s.Phrase + "' (-ConfirmLive '" + $s.Phrase + "')." ) 'red'
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
                    Write-ConsoleLine ('      > ' + (Format-CommandLine $s.Exe $s.Arguments)) 'gray'
                    $a = @($s.Arguments)
                    & $s.Exe @a
                    if ($LASTEXITCODE -ne 0) { throw ((Format-CommandLine $s.Exe $s.Arguments) + ' exited with ' + $LASTEXITCODE) }
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
            if ($logged) { Write-ConsoleLog $Config ('FAILED ' + $ActionKey + ' shard=' + $ShardKey + ' at step ' + $i + ': ' + $_.Exception.Message) }
            return $false
        }
    }
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
    param([string]$Phrase, [string]$Text, $Plan)
    $d = New-Object Windows.Forms.Form
    $d.Text = 'LIVE shard: type to confirm'
    if ($script:dry) { $d.Text = 'DRY RUN - LIVE shard: type to confirm (the window it opens only prints)' }
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
    param([string]$ActionKey, [string]$ShardKey, [string]$SnapshotName, [string]$Snapshot)
    try {
        $def = @(Get-ConsoleActions | Where-Object { $_.Key -eq $ActionKey })[0]
        if (-not $ShardKey) { $ShardKey = 'test'; if ($ActionKey -like 'live.*') { $ShardKey = 'live' } }
        # Two flags since the toggle: $script:dry is "buttons print", $script:fromCapture is "the
        # status is a capture file". Only the second decides where the state comes from.
        if ($script:fromCapture -and -not $script:dry) { throw 'the status is from a capture file, so nothing may run for real. Relaunch without -DryRun.' }
        $state = Get-ConsoleState -Config $script:cfg -DryRun:$script:fromCapture -StatusFrom $script:statusFrom
        $script:lastState = $state
        $now = Get-Date
        if ($script:fromCapture) { $now = $state.Now.ToLocalTime() }
        $plan = @(Get-ActionPlan -Action $ActionKey -ShardKey $ShardKey -Config $script:cfg -State $state -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now)
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
            if (-not (Show-TypedConfirm -Phrase $conf[0].Phrase -Text ($dryTag + $conf[0].Text) -Plan $plan)) { Write-ConsoleLine ($ActionKey + ': cancelled.') 'gray'; return }
            $phrase = $conf[0].Phrase
        }
        $answeredYes = $false
        $ask = @($plan | Where-Object { $_.Kind -eq 'ask' })
        if ($ask.Count) {
            if ((Show-YesNo ($dryTag + $ask[0].Text)) -ne 'Yes') { Write-ConsoleLine ($ActionKey + ': cancelled.') 'gray'; return }
            $answeredYes = $true
        }
        if ($def.RunIn -eq 'window') {
            $argLine = Get-ChildArgumentLine -SelfPath $script:SelfPath -ActionKey $ActionKey -ShardKey $ShardKey -SnapshotName $SnapshotName -Snapshot $Snapshot -ConfirmLive $phrase -Yes:$answeredYes -DryRun:$script:dry -StatusFrom $script:statusFrom -StatusFromDocker:(-not $script:fromCapture)
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
    $glance.ColumnCount = 3
    $glance.Margin = New-Object Windows.Forms.Padding(0)
    foreach ($i in 1..3) { [void]$glance.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent', 33.33))) }
    $script:ui.Tiles = @()
    $col = 0
    foreach ($title in @('LIVE', 'TEST', 'STATUS PUBLISHER')) {
        $tile = New-StatusTile $title
        $glance.Controls.Add($tile.Panel, $col, 0)
        $script:ui.Tiles += $tile
        $col++
    }

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

    $testPage = New-ButtonPage 'Test shard' 'The throwaway test shard (sl-modernuo-test, port 2594, its own Saves-test). No confirmation: it is throwaway. Start, Stop and Rebuild call docker/uo/Start-TestShard.ps1; Rebuild is the only one that builds, and it builds through build.sh and its gates.' @(
        (New-ConsoleButton 'Start' 'test.start'),
        (New-ConsoleButton 'Start fresh (old save moved aside)' 'test.fresh'),
        (New-ConsoleButton 'Stop' 'test.stop'),
        (New-ConsoleButton 'Rebuild through the gates and start' 'test.rebuild' 300),
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
    $head = New-Caption ('In-game commands, run by hand once per world, in this order. The console cannot run them and cannot see whether this world has had them: that is a fact about the save. The ticks are for this session only and are not saved. Copy puts one line on the clipboard. docker/uo/SERVER.md:355-357 has three broad patterns that cover every spawn file below.') 1100
    $worldTable.Controls.Add($head, 0, 0)
    $worldTable.SetColumnSpan($head, 3)
    $row = 1
    foreach ($item in Get-WorldSetupChecklist) {
        $cb = New-Object Windows.Forms.CheckBox
        $cb.Text = $item.Command
        $cb.AutoSize = $true
        $cb.Font = $script:MonoFont
        $cb.Margin = New-Object Windows.Forms.Padding(6, 5, 3, 3)
        $copy = New-Object Windows.Forms.Button
        $copy.Text = 'Copy'
        $copy.Width = 60
        $copy.Tag = $item.Command
        $copy.Add_Click({ [Windows.Forms.Clipboard]::SetText([string]$this.Tag); Write-ConsoleLine ('copied: ' + $this.Tag) 'gray' })
        $why = New-Object Windows.Forms.Label
        $why.Text = $item.Access + ': ' + $item.Why
        $why.AutoSize = $true
        $why.ForeColor = [Drawing.Color]::DimGray
        $why.Margin = New-Object Windows.Forms.Padding(6, 8, 3, 3)
        $worldTable.Controls.Add($cb, 0, $row)
        $worldTable.Controls.Add($copy, 1, $row)
        $worldTable.Controls.Add($why, 2, $row)
        $row++
    }
    $worldPage.Controls.Add($worldTable)

    $diagPage = New-ButtonPage 'Diagnostics' ('Each writes one timestamped file in ' + $Config.DiagFolder + ' (check-HHmmss.txt, shard-HHmmss.txt). Check-Shard.ps1 reads the TEST container only.') @(
        (New-ConsoleButton 'Run Check-Docker.ps1' 'diag.docker'),
        (New-ConsoleButton 'Run Check-Shard.ps1' 'diag.shard'),
        (New-ConsoleButton 'Open the folder they write to' 'diag.folder')
    )

    $livePage = New-ButtonPage 'LIVE shard' 'LIVE: sl-modernuo, the real world save, real players. Every button here asks you to type a phrase first. Start, Stop and Restart are docker start / stop / restart on the existing container; this console never runs compose on the live file (D29) and never deploys. A stop does not save: play since the last autosave is lost.' @(
        (New-ConsoleButton 'Start' 'live.start'),
        (New-ConsoleButton 'Stop' 'live.stop'),
        (New-ConsoleButton 'Restart' 'live.restart'),
        (New-ConsoleButton 'Tail the log' 'live.tail'),
        (New-ConsoleButton 'Open a client' 'live.client')
    ) ([Drawing.Color]::Firebrick)

    $tabs.TabPages.AddRange(@($testPage, $snapPage, $worldPage, $diagPage, $livePage))

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
    $form.Add_FormClosed({ $script:ui.Timer.Stop(); $script:OutputBox = $null })
    $script:ui.Timer = $timer

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
    # The mode is decided and shown before anything is read or run (D37).
    $run = Resolve-RunMode -Action $Action -Mode $Mode -DryRun:$DryRun
    $banner = Get-ModeBanner -RunMode $run -Action $Action -ShardKey $sk
    try { $Host.UI.RawUI.WindowTitle = $banner.Title } catch { }
    foreach ($l in $banner.Lines) { Write-ConsoleLine $l $banner.Color }
    if ($run -eq 'refuse') { exit 2 }
    $isDry = ($run -eq 'dry')
    $fromCapture = $isDry -and -not $StatusFromDocker
    $st = Get-ConsoleState -Config $config -DryRun:$fromCapture -StatusFrom $StatusFrom
    $now = Get-Date
    if ($fromCapture) { $now = $st.Now.ToLocalTime() }
    $plan = @(Get-ActionPlan -Action $Action -ShardKey $Shard -Config $config -State $st -SnapshotName $SnapshotName -Snapshot $Snapshot -Now $now)
    $viol = @(Test-PlanSafety $plan $config)
    $header = 'PLAN'
    if ($isDry) { $header = 'DRY RUN' }
    foreach ($l in (Format-Plan -Action $Action -ShardKey $sk -Plan $plan -Violations $viol -Header $header)) { Write-ConsoleLine $l 'normal' }
    if ($isDry) { Write-ConsoleLine '   (dry run: nothing was run)' 'amber'; exit 0 }
    Write-ConsoleLine '' 'normal'
    $ok = Invoke-Plan -Plan $plan -Config $config -ActionKey $Action -ShardKey $sk -ConfirmLive $ConfirmLive -Yes:$Yes
    if ($ok) { Write-ConsoleLine ($Action + ': done.') 'green'; exit 0 }
    exit 1
}

$form = New-ConsoleForm -Config $config -DryRun:$DryRun -StatusFrom $StatusFrom
[void]$form.ShowDialog()
$form.Dispose()
