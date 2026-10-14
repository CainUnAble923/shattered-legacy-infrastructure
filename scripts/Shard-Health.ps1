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
  Facts: scripts/tests/Shard-Console.Tests.ps1 (the P96_ ones).
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
    param([bool]$Exists, [bool]$Running, [string]$Health, $Crash)
    $crashed = [bool]($Crash -and $Crash.Crashed)
    if (-not $Exists) { return [pscustomobject]@{ State = 'NO CONTAINER'; Detail = 'there is no container' } }
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
