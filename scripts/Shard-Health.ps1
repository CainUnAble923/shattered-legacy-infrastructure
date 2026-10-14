#Requires -Version 5.1
<#
  cc-P96: is the test shard really up, and if it crashed, why. Pure functions: strings and plain values in,
  objects out. No docker, no files, no git. Two callers dot-source this file:

    scripts/Shard-Console.ps1      the tile, the Fix test shard tab, its plans (and its facts)
    docker/uo/Start-TestShard.ps1  the wait after a start, and the good-<commit> tag (Part C)

  Why it exists: on 2026-10-14 07:07 the test shard threw at start (bug-list D89) and sat on ModernUO's
  "This exception is fatal, press return to exit" (Server/Main.cs:433) while Docker showed the container "Up".
  The process waits on stdin there, so it never exits and nothing outside it fails.

  ASCII only, saved as UTF-8 with no BOM: PowerShell 5.1 reads a BOM-less file as Windows-1252.
  cc-P106 adds the console questions a shard can wait on (Get-ShardQuestion and its answer table, at the end).
  Facts: scripts/tests/Shard-Console.Tests.ps1 (the P96_ and P106_ ones).
#>

# Printed by pinned Server/Main.cs:433 for a terminating exception, after the exception itself (:410).
$script:FatalLine = 'This exception is fatal'
# The sl-modernuo tag a healthy start earns (Start-TestShard.ps1), and the ones prune never offers.
$script:GoodTagPrefix = 'sl-modernuo:good-'
$script:ImageRepoPrefix = 'sl-modernuo:'

function Remove-HealthAnsi {
    # The console's own Remove-Ansi, repeated so Start-TestShard.ps1 can use this file alone.
    param([string]$Text)
    ([string]$Text -replace "\x1b\[[0-9;?=]*[A-Za-z]", '') -replace "\x1b[=>]", ''
}

function Get-CrashReport {
    # The last terminating exception in a docker log: its innermost type and message (the cause, not
    # the reflection wrapper AssemblyHandler.Invoke adds), its first frames, and whether the shard had
    # reached Listening first. Crashed is $false when the log holds no fatal line.
    param([string]$LogText, [int]$Frames = 5)
    $lines = @((Remove-HealthAnsi $LogText) -replace "`r", '' -split "`n")
    $fatalAt = -1
    for ($i = $lines.Count - 1; $i -ge 0; $i--) { if ($lines[$i].Contains($script:FatalLine)) { $fatalAt = $i; break } }
    if ($fatalAt -lt 0) {
        return [pscustomobject]@{ Crashed = $false; AtStart = $false; Type = $null; Message = $null; Chain = @(); Frames = @(); Headline = ''; Text = '' }
    }
    $from = 0
    for ($i = $fatalAt - 1; $i -ge 0; $i--) { if ($lines[$i] -match '^\s*Error:\s*$') { $from = $i + 1; break } }
    $chain = New-Object 'System.Collections.Generic.List[object]'
    $frameList = New-Object 'System.Collections.Generic.List[string]'
    $body = New-Object 'System.Collections.Generic.List[string]'
    for ($i = $from; $i -lt $fatalAt; $i++) {
        $l = $lines[$i]
        $body.Add($l)
        if ($l -match '^\s*(?:---> )?([A-Za-z_][\w.`]*(?:Exception|Error)):\s?(.*)$') {
            $chain.Add([pscustomobject]@{ Type = $Matches[1]; Message = $Matches[2].Trim() })
        } elseif ($l -match '^\s+at (.+)$') {
            $frameList.Add($Matches[1].Trim())
        }
    }
    $listenedBefore = $false
    for ($i = 0; $i -lt $fatalAt; $i++) { if ($lines[$i] -match 'Listening: \S+:\d+' -and $lines[$i] -notmatch '\(Pings\)') { $listenedBefore = $true; break } }
    $inner = $null
    if ($chain.Count) { $inner = $chain[$chain.Count - 1] }
    $type = $null; $msg = $null
    if ($inner) { $type = $inner.Type; $msg = $inner.Message }
    $first = @($frameList | Select-Object -First $Frames)
    $head = 'unrecognised exception text'
    if ($type) {
        $short = ($type -split '\.')[-1]
        $head = $short + ': ' + $msg
        if ($first.Count) { $head += ' at ' + ($first[0] -replace ' in /\S+$', '') }
    }
    [pscustomobject]@{
        Crashed  = $true
        AtStart  = (-not $listenedBefore)
        Type     = $type
        Message  = $msg
        Chain    = $chain.ToArray()
        Frames   = $first
        Headline = $head
        Text     = ($body -join "`n")
    }
}

function Find-KnownCrash {
    # The bug-list row a crash matches: the first signature found in its exception text. A signature is
    # plain text (no pattern), so it can be copied straight out of a stack frame.
    param($Report, $Signatures)
    if (-not $Report -or -not $Report.Crashed) { return $null }
    foreach ($s in @($Signatures)) {
        if ($s -and $s.Match -and $Report.Text.Contains([string]$s.Match)) { return $s }
    }
    $null
}

function Get-HealthState {
    # One word for the test shard, from docker's own health check (docker-compose.test.yml) and the log.
    # A crash outranks health: the process at "press return to exit" stays up, and until the check's
    # start period runs out docker still says "starting".
    param([bool]$Exists, [bool]$Running, [string]$Health, $Crash, $Question)
    $crashed = [bool]($Crash -and $Crash.Crashed)
    if (-not $Exists) { return [pscustomobject]@{ State = 'NO CONTAINER'; Detail = 'there is no container' } }
    # cc-P106: a shard waiting on its console is never healthy, whatever docker's check says. A fatal is still CRASHED.
    if ($Running -and $Question -and $Question.Kind -ne 'fatal') {
        return [pscustomobject]@{ State = 'WAITING'; Detail = 'waiting for an answer on its console: ' + (Get-QuestionLine $Question) + ' (Shard question tab)' }
    }
    if ($crashed) {
        $where = 'after it had started'
        if ($Crash.AtStart) { $where = 'at start' }
        $d = 'crashed ' + $where + ': ' + $Crash.Headline
        if ($Running) { $d += ' (the process waits at "press return to exit"; Docker still says Up)' }
        return [pscustomobject]@{ State = 'CRASHED'; Detail = $d }
    }
    if (-not $Running) { return [pscustomobject]@{ State = 'STOPPED'; Detail = 'the container is not running' } }
    switch ($Health) {
        'healthy'   { return [pscustomobject]@{ State = 'HEALTHY'; Detail = 'listening on its game port (docker health check)' } }
        'unhealthy' { return [pscustomobject]@{ State = 'UNHEALTHY'; Detail = 'not listening on its game port (docker health check)' } }
        'starting'  { return [pscustomobject]@{ State = 'STARTING'; Detail = 'not listening yet; the health check is in its start period' } }
    }
    [pscustomobject]@{ State = 'NO HEALTH CHECK'; Detail = 'this container was created without the cc-P96 health check; recreate it (Start) to get one' }
}

function Get-GoodTag {
    # sl-modernuo:good-<7 hex>, and -dirty when the image was built from a working tree with changes.
    param([string]$Commit, [bool]$Dirty)
    if ($Commit -notmatch '^[0-9a-f]{7,40}$') { return $null }
    $t = $script:GoodTagPrefix + $Commit.Substring(0, 7)
    if ($Dirty) { $t += '-dirty' }
    $t
}

function Get-GoodTagCommit {
    # The short commit a good tag names, or $null.
    param([string]$Tag)
    if ($Tag -cmatch ('^' + [regex]::Escape($script:GoodTagPrefix) + '([0-9a-f]{7})(-dirty)?$')) { return $Matches[1] }
    $null
}

function Get-LastGoodImage {
    # The newest image tagged sl-modernuo:good-*, from rows with Tag, Id and Created.
    param($Images)
    @(@($Images) | Where-Object { $_.Tag -and (Get-GoodTagCommit $_.Tag) } | Sort-Object Created -Descending | Select-Object -First 1)[0]
}

function ConvertFrom-ImageSize {
    # docker images' human size (1.29GB, 512MB, 900kB, 12B) as bytes, or $null.
    param([string]$Text)
    if ([string]$Text -notmatch '^\s*([\d.]+)\s*(B|kB|KB|MB|GB|TB)\s*$') { return $null }
    $n = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
    $mul = @{ 'b' = 1; 'kb' = 1e3; 'mb' = 1e6; 'gb' = 1e9; 'tb' = 1e12 }[$Matches[2].ToLowerInvariant()]
    [int64]($n * $mul)
}

function Get-PruneSet {
    # Which sl-modernuo tags prune keeps, with the reason, and which it offers to remove. Kept: latest
    # (the compose file's default), the newest KeepGood good-* tags, rollback-* and pre-bump-*, any tag in
    # KeepTags, and every tag on an image id in ProtectIds (what the test and live containers run). Only
    # sl-modernuo:* rows are ever considered, so uo-modernuo (live's image name) cannot be listed.
    # FreedBytes counts an image only when every one of its tags goes, at its full size: an upper bound, since
    # most layers are shared. AtLeastBytes sums the same images' unique sizes (docker system df -v, by short id)
    # when given: the layers only that image uses, a lower bound. $null without them.
    param($Images, [int]$KeepGood = 5, [string[]]$KeepTags = @(), [string[]]$ProtectIds = @(), [hashtable]$UniqueSizes)
    $rows = @(@($Images) | Where-Object { $_.Tag -and $_.Tag.StartsWith($script:ImageRepoPrefix, [StringComparison]::Ordinal) })
    $protect = @($ProtectIds | Where-Object { $_ } | ForEach-Object { ([string]$_ -replace '^sha256:', '').Substring(0, [Math]::Min(12, ([string]$_ -replace '^sha256:', '').Length)) })
    $good = @($rows | Where-Object { Get-GoodTagCommit $_.Tag } | Sort-Object Created -Descending | Select-Object -First $KeepGood | ForEach-Object { $_.Tag })
    $keep = New-Object 'System.Collections.Generic.List[object]'
    $remove = New-Object 'System.Collections.Generic.List[object]'
    foreach ($r in $rows) {
        $name = $r.Tag.Substring($script:ImageRepoPrefix.Length)
        $why = $null
        if ($name -ceq 'latest') { $why = 'latest: the test compose file''s default image' }
        elseif ($good -contains $r.Tag) { $why = 'one of the newest ' + $KeepGood + ' good-* tags' }
        elseif ($name -clike 'rollback-*') { $why = 'a rollback-* tag' }
        elseif ($name -clike 'pre-bump-*') { $why = 'a pre-bump-* tag' }
        elseif (@($KeepTags) -ccontains $r.Tag -or @($KeepTags) -ccontains $name) { $why = 'named in the keep list (fix-test-shard.json keepTags)' }
        elseif ($protect -contains ([string]$r.Id).Substring(0, [Math]::Min(12, ([string]$r.Id).Length))) { $why = 'a container runs this image' }
        if ($why) { $keep.Add([pscustomobject]@{ Tag = $r.Tag; Id = $r.Id; Why = $why }) }
        else { $remove.Add($r) }
    }
    $removedTags = @($remove | ForEach-Object { $_.Tag })
    $freed = [int64]0
    $unknown = $false
    $atLeast = $null
    if ($UniqueSizes) { $atLeast = [int64]0 }
    foreach ($g in @($remove | Group-Object Id)) {
        $all = @($rows | Where-Object { $_.Id -eq $g.Name })
        if (@($all | Where-Object { $removedTags -notcontains $_.Tag }).Count) { continue }
        $sz = $g.Group[0].Size
        if ($null -eq $sz) { $unknown = $true } else { $freed += [int64]$sz }
        if ($UniqueSizes -and $UniqueSizes.ContainsKey([string]$g.Name)) { $atLeast += [int64]$UniqueSizes[[string]$g.Name] }
    }
    [pscustomobject]@{ Keep = $keep.ToArray(); Remove = $remove.ToArray(); FreedBytes = $freed; AtLeastBytes = $atLeast; SizeUnknown = $unknown }
}

function Test-SaveNewerThanImage {
    # Whether the test save was last written by a newer commit than the image a rollback would run.
    # Ancestry is git's (merge-base --is-ancestor), passed in: $true when the image's commit is an
    # ancestor of the save's. Unknown when either commit is.
    param([string]$SaveCommit, [string]$ImageCommit, $ImageIsAncestorOfSave)
    if (-not $SaveCommit -or -not $ImageCommit -or $null -eq $ImageIsAncestorOfSave) { return $null }
    $a = $SaveCommit.Substring(0, [Math]::Min(7, $SaveCommit.Length))
    $b = $ImageCommit.Substring(0, [Math]::Min(7, $ImageCommit.Length))
    if ($a -eq $b) { return $false }
    [bool]$ImageIsAncestorOfSave
}

# --- cc-P106: a shard waiting for an answer on its console (bug-list D94) --------------------------------------------
# On 2026-10-14 live (sl-modernuo) sat from 08:06 at "Delete the object and continue? (y/n/a)" after a bad deserialize,
# with Docker showing it Up. Every console read pinned ModernUO makes at start or save (D:\UO\ModernUO-pinned, read for
# cc-P106), what it prints first, and what each answer does:
#
#   object   Server/Serialization/GenericEntityPersistence.cs:756-771  "***** Bad deserialize of <Type> (<serial>) *****",
#            the exception, then Console.Write "Delete the object and continue? (y/n/a): ". y deletes that object and
#            loads on; a deletes it and every later bad object of that file without asking; anything else throws
#            "Deserialization failed.", and the load ends at the fatal line below.
#   file     GenericPersistence.cs:123-130 and AdhocPersistence.cs:101-107  "***** Bad deserialize of <file> *****", the
#            exception, then "Skip this file and continue? (y/n): ". y skips the file (what it held is gone at the next
#            save); anything else throws, as above.
#   type     GenericEntityPersistence.cs:399-415  "failed", then "Error: Type '<name>' was not found|marked abstract.
#            Delete all of those types? (y/n): ". y drops every saved object of that type; anything else throws "Bad type".
#   fatal    Server/Main.cs:433-434  "This exception is fatal, press return to exit", then ReadLine; any line ends the
#            process (DoKill), and Docker's restart policy (unless-stopped) starts it again.
#   owner    UOContent/Misc/AccountPrompt.cs:15-31  only with no accounts: "Do you want to create the owner account now?
#            (y/n):". y asks a username and a password on the console (not answered here); anything else goes on with none.
#   restore  UOContent/World Saves/AutoArchive.cs:230-389, 454-510  only when there is no valid save: a list of backups,
#            then Console.Write "... or Enter to start fresh: " (or "[1]> "). A number restores it; Enter starts with no
#            world. Only Stop is offered here: picking a backup is done at the console.
#   (UOContent/Engines/Pathing/PathCacheCommands.cs:73 asks too, but only after a staff command, never on its own.)
#
# docker logs records a line only once it ends, so a Console.Write prompt never shows: a question is known by what it
# prints before it, on a running container that has then been silent for QuietSeconds.

$script:QuestionPrompts = @{
    object  = 'Delete the object and continue? (y/n/a)'
    file    = 'Skip this file and continue? (y/n)'
    type    = 'Delete all of those types? (y/n)'
    fatal   = 'This exception is fatal, press return to exit'
    owner   = 'Do you want to create the owner account now? (y/n)'
    restore = 'Restore a backup, or press Enter to start with no world?'
}

function Get-QuestionAnswers {
    # The answers this console offers for a question, the safe one first. Send is what is typed before Enter.
    # Rows go through List.Add, one at a time: an array literal of one row would be unrolled into its fields.
    param([string]$Kind, [string]$What = 'this object')
    $fails = 'Loading stops: the server prints "This exception is fatal, press return to exit" and waits again.'
    $rows = New-Object 'System.Collections.Generic.List[object]'
    switch ($Kind) {
        'object' {
            $rows.Add(@('n', 'n', $false, ('Do not delete. ' + $fails + ' Nothing is lost; the save stays as it is.')))
            $rows.Add(@('y', 'y', $true, ('Delete this one object (' + $What + ') and keep loading. What it held is gone once the world saves.')))
            $rows.Add(@('a', 'a', $true, 'Delete this object and every other bad object in the same save file without asking again, and keep loading.'))
        }
        'file' {
            $rows.Add(@('n', 'n', $false, ('Do not skip. ' + $fails + ' Nothing is lost.')))
            $rows.Add(@('y', 'y', $true, 'Skip this file and keep loading. Everything it held is gone once the world saves.'))
        }
        'type' {
            $rows.Add(@('n', 'n', $false, ('Do not delete. ' + $fails + ' Nothing is lost.')))
            $rows.Add(@('y', 'y', $true, 'Delete every saved object of the missing type and keep loading.'))
        }
        'fatal' {
            $rows.Add(@('enter', '', $false, 'Let the server exit. Live (restart: unless-stopped) is started again by Docker and will most likely stop at the same place; the test shard (restart: no) stays stopped. Stop the shard instead to be sure it stays down.'))
        }
        'owner' {
            $rows.Add(@('n', 'n', $false, 'Do not create an owner account now; the server keeps starting with no accounts. (To create one, answer y at the console: it asks a name and a password.)'))
        }
    }
    foreach ($r in $rows) { [pscustomobject]@{ Key = $r[0]; Send = $r[1]; Deletes = $r[2]; Explain = $r[3] } }
}

function ConvertFrom-StampedLog {
    # docker logs -t lines as Time (UTC, or $null for an unstamped line) and Text (colour codes removed).
    param([string]$LogText)
    foreach ($l in @((Remove-HealthAnsi $LogText) -replace "`r", '' -split "`n")) {
        if ($l -match '^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(\.\d+)?Z (.*)$') {
            $t = [datetime]::ParseExact($Matches[1], 'yyyy-MM-ddTHH:mm:ss', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]'AssumeUniversal,AdjustToUniversal')
            [pscustomobject]@{ Time = $t; Text = $Matches[3] }
        } else {
            [pscustomobject]@{ Time = $null; Text = $l }
        }
    }
}

function Get-ShardQuestion {
    # The question a running shard is waiting on, from its docker logs -t tail, or $null. Only the current run counts
    # (from the last "[SL] Starting"); and a shard that printed within QuietSeconds of Now is still talking.
    param([string]$LogText, [bool]$Running, [datetime]$Now, [int]$QuietSeconds = 20)
    if (-not $Running -or -not $LogText) { return $null }
    $all = @(ConvertFrom-StampedLog $LogText)
    $from = 0
    for ($i = $all.Count - 1; $i -ge 0; $i--) { if ($all[$i].Text -match '^\[SL\] Starting ') { $from = $i; break } }
    $lines = @($all[$from..($all.Count - 1)] | Where-Object { $_.Text.Trim() })
    if (-not $lines.Count) { return $null }
    $last = $lines[$lines.Count - 1]
    if ($last.Time -and $Now -ne [datetime]::MinValue -and ($Now - $last.Time).TotalSeconds -lt $QuietSeconds) { return $null }
    $kind = $null; $since = $last.Time; $type = ''; $serial = ''; $file = ''; $exc = ''; $frames = @()
    $stack = '^\s+at |^\s*---> |^\s*--- End of |^[A-Za-z_][\w.`]*(Exception|Error)\b|^Serialized object was \d+ bytes'
    if ($last.Text.Contains('This exception is fatal')) {
        $kind = 'fatal'
        $rep = Get-CrashReport (($lines | ForEach-Object { $_.Text }) -join "`n")
        if ($rep.Crashed -and $rep.Type) { $exc = $rep.Type + ': ' + $rep.Message; $frames = @($rep.Frames) }
    } elseif ($last.Text -match 'create the owner account now\? \(y/n\)') {
        $kind = 'owner'
    } elseif ($last.Text.Trim() -ceq 'failed') {
        $kind = 'type'
    } else {
        $b = -1
        for ($i = $lines.Count - 1; $i -ge 0; $i--) { if ($lines[$i].Text -match '^\*{5} Bad deserialize of (.+?) \*{5}\s*$') { $b = $i; break } }
        if ($b -ge 0) {
            $after = @(); if ($b + 1 -le $lines.Count - 1) { $after = @($lines[($b + 1)..($lines.Count - 1)]) }
            if (-not @($after | Where-Object { $_.Text -notmatch $stack }).Count) {
                $what = ([regex]::Match($lines[$b].Text, '^\*{5} Bad deserialize of (.+?) \*{5}')).Groups[1].Value
                if ($what -match '^(\S+) \((0x[0-9A-Fa-f]+)\)$') { $kind = 'object'; $type = $Matches[1]; $serial = $Matches[2] }
                else { $kind = 'file'; $file = $what }
                $since = $lines[$b].Time
                $first = @($after | Where-Object { $_.Text -notmatch '^\s+at ' } | Select-Object -First 1)
                if ($first.Count) { $exc = $first[0].Text.Trim() }
                $frames = @($after | Where-Object { $_.Text -match '^\s+at (.+)$' } | ForEach-Object { ($_.Text -replace '^\s+at ', '') -replace ' in /\S+$', '' })
            }
        } else {
            $av = -1
            for ($i = $lines.Count - 1; $i -ge [Math]::Max(0, $lines.Count - 40); $i--) { if ($lines[$i].Text -match '^Available backups and archives') { $av = $i; break } }
            if ($av -ge 0 -and -not @($lines[$av..($lines.Count - 1)] | Where-Object { $_.Text -notmatch '^Available backups|^\s+\d+\) |^\s*\[[np]\]' }).Count) { $kind = 'restore'; $since = $lines[$av].Time }
        }
    }
    if (-not $kind) { return $null }
    $what = 'this object'
    if ($type) { $what = $type + ' ' + $serial }
    $frame = ''
    if ($frames.Count) { $frame = $frames[0] }
    $sig = $kind + '|' + $(if ($since) { $since.ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture) } else { '' }) + '|' + $type + $serial + $file
    [pscustomobject]@{
        Kind = $kind; Prompt = $script:QuestionPrompts[$kind]; Type = $type; Serial = $serial; File = $file
        Exception = $exc; Frame = $frame; Frames = $frames; Since = $since; Signature = $sig
        Answers = @(Get-QuestionAnswers -Kind $kind -What $what)
    }
}

function Get-QuestionLine {
    # The question in one line, for the tile: the prompt, what failed and how, since when.
    param($Question)
    if (-not $Question) { return '' }
    $t = $Question.Prompt
    if ($Question.Type) { $t += ' ' + $Question.Type + ' ' + $Question.Serial }
    elseif ($Question.File) { $t += ' ' + $Question.File }
    if ($Question.Exception) { $t += ': ' + (($Question.Exception -split ':')[0] -split '\.')[-1] }
    if ($Question.Frame) { $t += ' at ' + $Question.Frame }
    if ($Question.Since) { $t += ', since ' + $Question.Since.ToLocalTime().ToString('HH:mm', [Globalization.CultureInfo]::InvariantCulture) }
    $t
}

function Test-QuestionStillAsked {
    # The answer goes only to the question it was chosen for: the same kind, start and object, still waiting.
    param([string]$Signature, $Question)
    [bool]($Question -and $Signature -and $Question.Signature -ceq $Signature)
}

function Get-AnswerKeystrokes {
    # What is written to the console: the answer and Enter (CR, as a terminal sends it; the pty turns it into a newline).
    param([string]$Send)
    [string]$Send + "`r"
}
