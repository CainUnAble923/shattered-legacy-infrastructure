#Requires -Version 5.1
<#
  Overrides-Launch.ps1 (cc-P411): how the launcher and the updater get Shattered Legacy's art, text and map ready
  without ever keeping anyone from playing. Dot-sourced by Play.ps1 and Update.ps1; player-package\tests\
  Launch-Overrides.Tests.ps1 calls the same functions.

      Invoke-UoOverrides -App <app folder> -UoDir <Ultima Online folder> [-LimitSeconds 60] [-Reason play|update]
                         [-NoWaitForRunning]

  Returns the path of files-override.txt for TazUO's -uofilesoverride, or $null to start without one. In order:
    1. A staged set that finished while the game was running (a generation under app\uo-overrides.staging\ with its
       ready file, its builder gone) is swapped in: the current folder becomes uo-overrides.old, the staged one
       uo-overrides, then the old one is deleted. If the game still holds the files, nothing moves and the swap is
       tried at the next start. A start that finds uo-overrides.old with no uo-overrides puts the old set back.
    2. Build-UoOverrides.ps1 -CheckOnly compares the stamps (fast, and read-only). Up to date: done, the current list.
    3. Otherwise a rebuild in a child PowerShell, into a new generation app\uo-overrides.staging\g<ticks>\ (never into
       the folder the game reads, never into another start's), with its trace in app\logs\overrides-<time>.log (the
       last 10 are kept) and its progress in the generation's progress.json. One line before ("Preparing Shattered
       Legacy art and map, one time after an update...") and one after ("...done in N s"), and a progress bar the
       whole time: the step, its count, the seconds, redrawn twice a second even while a step reports nothing.
    4. Done within the limit (app\launch-options.json "overridesTimeLimitSeconds", default 60): swapped in, its list
       returned. Not done: one yellow line, the game starts with the last good set (or none), and the rebuild carries
       on at low priority; its set is swapped in at the next start. A rebuild that stopped without its ready file
       left only its generation, which the next start deletes and builds again.

  cc-P611 (P549 #1-#3, P556 U2): the limit is one deadline for the whole call, from its first line: the lock, the
  check, starting the child, the wait and the swap (each rename retried only while time is left). The check runs on a
  second runspace and is abandoned at the deadline, so a file that blocks on read cannot hold the game. One start at a
  time owns staging (a mutex per install, waited for only while time is left): it finds a running rebuild, makes a
  generation, starts the child and writes its pid file before the next start looks. A generation is swapped in only
  once its builder has exited (with exit code 0, when this start knows it) and its ready file is there.
  app\uo-overrides.next\ is the layout before cc-P611, still swapped in or cleared as a generation.
  -NoWaitForRunning (Play.ps1 after the updater already waited, $script:OverridesStillBuilding): a rebuild that is
  still running is not waited for a second time.

  Test hooks (never set on a player's machine): SL_OVERRIDES_LIMIT_SEC=<n> replaces the limit.
  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as Windows-1252.
#>

$script:OverridesProgressShown = New-Object 'System.Collections.Generic.List[string]'
$script:OverridesStillBuilding = $false

function Get-OverridesTimeLimit([string]$App, [int]$Default = 60) {
    $e = [string]$env:SL_OVERRIDES_LIMIT_SEC
    if ($e -match '^\d+$') { return [int]$e }
    $f = Join-Path $App 'launch-options.json'
    if (Test-Path -LiteralPath $f) {
        try {
            $j = [IO.File]::ReadAllText($f) | ConvertFrom-Json
            if ($null -ne $j.overridesTimeLimitSeconds -and [int]$j.overridesTimeLimitSeconds -gt 0) { return [int]$j.overridesTimeLimitSeconds }
        } catch {}
    }
    $Default
}

# The rebuild child still running for this generation, from the pid file it was started with; $null when none.
function Get-OverridesBuilder([string]$Staging) {
    $pf = Join-Path $Staging 'builder.pid'
    if (-not (Test-Path -LiteralPath $pf)) { return $null }
    try {
        $parts = @([IO.File]::ReadAllLines($pf))[0].Trim() -split ' '
        $p = Get-Process -Id ([int]$parts[0]) -ErrorAction Stop
        # An ended process is still found while a handle to it is open (this process's own Start-Process object).
        if ($p.HasExited) { return $null }
        if ($parts.Count -gt 1 -and [Math]::Abs(($p.StartTime.ToUniversalTime().Ticks - [long]$parts[1]) / 1e7) -gt 2) { return $null }
        return $p
    } catch { return $null }
}

# cc-P611: every staged generation, oldest first: the pre-P611 app\uo-overrides.next, then uo-overrides.staging\g*.
function Get-OverridesGenerations([string]$App) {
    $gens = @()
    $legacy = Join-Path $App 'uo-overrides.next'
    if (Test-Path -LiteralPath $legacy -PathType Container) { $gens += $legacy }
    $root = Join-Path $App 'uo-overrides.staging'
    if (Test-Path -LiteralPath $root -PathType Container) {
        $gens += @(Get-ChildItem -LiteralPath $root -Directory -Filter 'g*' -ErrorAction SilentlyContinue | Sort-Object Name | ForEach-Object { $_.FullName })
    }
    $gens
}

# A generation another start may still be writing: its builder is running, or it is too new to have its pid file.
function Test-OverridesGenerationLive([string]$Gen) {
    if (Get-OverridesBuilder $Gen) { return $true }
    if (Test-Path -LiteralPath (Join-Path $Gen 'builder.pid')) { return $false }
    try { return ([DateTime]::UtcNow - (Get-Item -LiteralPath $Gen).CreationTimeUtc).TotalSeconds -lt 120 } catch { return $false }
}

# A finished set: the ready file the builder writes last, and the list it names.
function Test-OverridesGenerationReady([string]$Gen) {
    $rf = Join-Path $Gen 'ready.txt'
    if (-not (Test-Path -LiteralPath $rf)) { return $false }
    try { $line = @([IO.File]::ReadAllLines($rf))[0] } catch { return $false }
    if ($line -notmatch '^ready( (.*))?$') { return $false }
    if ($Matches[2] -and -not (Test-Path -LiteralPath (Join-Path $Gen 'files-override.txt'))) { return $false }
    $true
}

# The seconds left before the deadline (a stopwatch and the limit).
function Get-OverridesRemaining($Clock, [double]$Limit) { [Math]::Max(0, $Limit - $Clock.Elapsed.TotalSeconds) }

# A rename (or a move out of uo-overrides.staging, on the same drive), tried every 300 ms while time is left: a scanner
# can hold a file it just saw written for a moment. One the game holds stays held, and then nothing moves. -Once:
# tried once more even when the time is up (putting the last good set back).
function Move-OverridesFolder([string]$From, [string]$To, $Clock, [double]$Limit, [switch]$Once) {
    for ($i = 0; $i -lt 15; $i++) {
        try { [IO.Directory]::Move($From, $To); return $true } catch { $script:OverridesSwapError = $_.Exception.InnerException.Message; if (-not $script:OverridesSwapError) { $script:OverridesSwapError = $_.Exception.Message } }
        if ((Get-OverridesRemaining $Clock $Limit) -lt 0.3) { break }
        Start-Sleep -Milliseconds 300
    }
    if ($Once -and $i -gt 0) { try { [IO.Directory]::Move($From, $To); return $true } catch {} }
    $false
}

# Step 1, under the lock: a finished staged set becomes the current one. $true when it was swapped in (or there was
# nothing to do).
function Complete-StagedOverrides([string]$App, $Clock = $null, [double]$Limit = 60) {
    if (-not $Clock) { $Clock = [Diagnostics.Stopwatch]::StartNew() }
    $live = Join-Path $App 'uo-overrides'
    $old = Join-Path $App 'uo-overrides.old'
    # A swap that ran out of time left the last good set as .old: put it back, never delete the only one.
    if (Test-Path -LiteralPath $old) {
        if (Test-Path -LiteralPath $live) { Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue }
        elseif (-not (Move-OverridesFolder $old $live $Clock $Limit -Once)) { return $false }
    }
    $ready = $null
    foreach ($g in @(Get-OverridesGenerations $App)) {
        if (Test-OverridesGenerationLive $g) { continue }
        if (Test-OverridesGenerationReady $g) { $ready = $g }
    }
    # Older generations no one is writing go, ready or not: the newest ready one is the set to use.
    foreach ($g in @(Get-OverridesGenerations $App)) {
        if ($g -eq $ready -or (Test-OverridesGenerationLive $g)) { continue }
        if ($ready -or -not (Test-OverridesGenerationReady $g)) { Remove-Item -LiteralPath $g -Recurse -Force -ErrorAction SilentlyContinue }
    }
    if (-not $ready) { Remove-OverridesStagingRoot $App; return $true }
    if ((Test-Path -LiteralPath $live) -and -not (Move-OverridesFolder $live $old $Clock $Limit)) { return $false }
    if (-not (Move-OverridesFolder $ready $live $Clock $Limit)) {
        if ((Test-Path -LiteralPath $old) -and -not (Test-Path -LiteralPath $live)) { [void](Move-OverridesFolder $old $live $Clock $Limit -Once) }
        return $false
    }
    foreach ($n in 'ready.txt', 'progress.json', 'builder.pid') { Remove-Item -LiteralPath (Join-Path $live $n) -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-OverridesStagingRoot $App
    $true
}

function Remove-OverridesStagingRoot([string]$App) {
    $root = Join-Path $App 'uo-overrides.staging'
    if ((Test-Path -LiteralPath $root) -and @(Get-ChildItem -LiteralPath $root -Force -ErrorAction SilentlyContinue).Count -eq 0) {
        Remove-Item -LiteralPath $root -Force -ErrorAction SilentlyContinue
    }
}

# cc-P611: one start at a time owns staging for this install. Waited for only while time is left; $null when it was not
# had in time. A start that died holding it leaves it abandoned, which the next one takes over.
function Enter-OverridesLock([string]$App, $Clock, [double]$Limit) {
    $full = [IO.Path]::GetFullPath($App).TrimEnd('\').ToLowerInvariant()
    $sha = [Security.Cryptography.SHA1]::Create()
    try { $h = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($full))) -replace '-', '').Substring(0, 16) } finally { $sha.Dispose() }
    $m = New-Object Threading.Mutex($false, "Local\ShatteredLegacy-overrides-$h")
    $ms = [int]((Get-OverridesRemaining $Clock $Limit) * 1000)
    try { if ($m.WaitOne($ms)) { return $m } }
    catch [Threading.AbandonedMutexException] { return $m }
    $m.Dispose()
    $null
}

function Exit-OverridesLock($Lock) {
    if (-not $Lock) { return }
    try { $Lock.ReleaseMutex() } catch {}
    $Lock.Dispose()
}

# cc-P611 (the earlier P411 review: the check itself can block on a file): Build-UoOverrides.ps1 -CheckOnly on a second
# runspace, given only the time left. Its result, or $null when it failed or did not answer in time
# ($script:OverridesCheckTimedOut says which). A runspace left behind ends with this process.
function Invoke-OverridesCheck([string]$Builder, [string]$UoDir, [string]$Live, $Clock, [double]$Limit) {
    $script:OverridesCheckTimedOut = $false
    $ps = [PowerShell]::Create()
    [void]$ps.AddCommand($Builder).AddParameter('UoDir', $UoDir).AddParameter('OutDir', $Live).AddParameter('CheckOnly', $true)
    $h = $ps.BeginInvoke()
    $ms = [int]((Get-OverridesRemaining $Clock $Limit) * 1000)
    if (-not $h.AsyncWaitHandle.WaitOne($ms)) { $script:OverridesCheckTimedOut = $true; return $null }
    try { $out = @($ps.EndInvoke($h)) } catch { $out = @() }
    # What it said (a part it could not check, in yellow) goes to this window as it would have.
    foreach ($rec in @($ps.Streams.Information)) {
        $m = $rec.MessageData
        if ($m -is [Management.Automation.HostInformationMessage]) {
            if ($null -ne $m.ForegroundColor) { Write-Host $m.Message -ForegroundColor $m.ForegroundColor } else { Write-Host $m.Message }
        } else { Write-Host "$m" }
    }
    $ps.Dispose()
    @($out | Where-Object { $_ -and $_.PSObject.Properties['Fresh'] })[0]
}

# One redraw of the bar from the child's progress file. Also kept in $script:OverridesProgressShown, for the tests.
function Show-OverridesProgress([string]$Staging, [double]$Seconds, [string]$Activity) {
    $step = 'starting'; $done = -1; $total = -1
    try {
        $j = [IO.File]::ReadAllText((Join-Path $Staging 'progress.json')) | ConvertFrom-Json
        $step = [string]$j.step; $done = [long]$j.done; $total = [long]$j.total
    } catch {}
    $pct = -1
    $count = ''
    if ($total -gt 0 -and $done -ge 0) { $pct = [int][Math]::Min(100, $done * 100 / $total); $count = " $done of $total" }
    elseif ($done -ge 0) { $count = " $done" }   # cc-P611: a step that counts with no total yet (the map's lines)
    $status = ('{0}{1}, {2:0} s' -f $step, $count, $Seconds)
    $script:OverridesProgressShown.Add(('{0:0.0} {1}' -f $Seconds, $status))
    if ($pct -ge 0) { Write-Progress -Activity $Activity -Status $status -PercentComplete $pct }
    else { Write-Progress -Activity $Activity -Status $status }
}

function Invoke-UoOverrides {
    param([Parameter(Mandatory = $true)][string]$App, [Parameter(Mandatory = $true)][string]$UoDir, [int]$LimitSeconds = -1,
          [ValidateSet('play', 'update')][string]$Reason = 'play', [switch]$NoWaitForRunning)
    $clock = [Diagnostics.Stopwatch]::StartNew()   # cc-P611: one deadline, from here
    $script:OverridesStillBuilding = $false
    $live = Join-Path $App 'uo-overrides'
    $builder = Join-Path $App 'Build-UoOverrides.ps1'
    $liveList = Join-Path $live 'files-override.txt'
    $previous = { if (Test-Path -LiteralPath $liveList) { $liveList } else { $null } }
    if ($LimitSeconds -lt 0) { $LimitSeconds = Get-OverridesTimeLimit $App }
    $lastGood = { if (& $previous) { 'Starting the game with the last good art.' } else { 'Starting the game without it this time.' } }

    $lock = Enter-OverridesLock $App $clock $LimitSeconds
    if (-not $lock) {
        Write-Host "  Shattered Legacy art is being prepared by another start of the game. $(& $lastGood)" -ForegroundColor Yellow
        return (& $previous)
    }
    $staging = $null; $running = $null; $started = $false
    try {
        [void](Complete-StagedOverrides $App $clock $LimitSeconds)

        # --- 2. up to date? --------------------------------------------------------------------------
        foreach ($g in @(Get-OverridesGenerations $App)) { $b = Get-OverridesBuilder $g; if ($b) { $staging = $g; $running = $b } }
        if (-not $running) {
            $chk = Invoke-OverridesCheck $builder $UoDir $live $clock $LimitSeconds
            if ($script:OverridesCheckTimedOut) {
                Write-Host "  Shattered Legacy art could not be checked within $LimitSeconds s on this computer. $(& $lastGood)" -ForegroundColor Yellow
                return (& $previous)
            }
            if ($chk -and $chk.Fresh) { return $chk.List }
        } elseif ($NoWaitForRunning) {
            $script:OverridesStillBuilding = $true
            Write-Host "  Shattered Legacy art is still being prepared in the background. $(& $lastGood)" -ForegroundColor Yellow
            return (& $previous)
        }

        # --- 3. a rebuild in a child, in a generation of its own -----------------------------------------
        if (-not $running) {
            $root = Join-Path $App 'uo-overrides.staging'
            $staging = Join-Path $root ('g' + [DateTime]::UtcNow.Ticks)
            New-Item -ItemType Directory -Path $staging -Force | Out-Null
            $logs = Join-Path $App 'logs'
            if (-not (Test-Path -LiteralPath $logs)) { New-Item -ItemType Directory -Path $logs -Force | Out-Null }
            $trace = Join-Path $logs ('overrides-' + (Get-Date).ToString('yyyyMMdd-HHmmss') + '.log')
            $q = { param($p) if ($p.EndsWith('\')) { $p += '.' }; '"' + $p + '"' }
            $a = "-NoProfile -ExecutionPolicy Bypass -File $(& $q $builder) -UoDir $(& $q $UoDir) -OutDir $(& $q $staging) -ListDir $(& $q $live) " +
                 "-TraceLog $(& $q $trace) -ProgressFile $(& $q (Join-Path $staging 'progress.json')) -ReadyFile $(& $q (Join-Path $staging 'ready.txt'))"
            # No output redirection: that would hand the child this process's handles (a caller reading this window's output
            # would then wait for the whole rebuild). What it says goes into its trace ("said: ..."), read back below.
            $running = Start-Process -FilePath 'powershell.exe' -ArgumentList $a -WindowStyle Hidden -PassThru
            [IO.File]::WriteAllText((Join-Path $staging 'builder.pid'), "$($running.Id) $($running.StartTime.ToUniversalTime().Ticks)`r`n$trace", (New-Object Text.ASCIIEncoding))
            $started = $true
        }
    } finally { Exit-OverridesLock $lock }

    $activity = 'Preparing Shattered Legacy art and map'
    Write-Host "  Preparing Shattered Legacy art and map, one time after an update$(if ($Reason -eq 'update') { ' (as part of it)' })..." -ForegroundColor Gray

    # --- 4. wait, with the bar, up to the deadline -----------------------------------------------------
    while (-not $running.HasExited -and $clock.Elapsed.TotalSeconds -lt $LimitSeconds) {
        Show-OverridesProgress $staging $clock.Elapsed.TotalSeconds $activity
        [void]$running.WaitForExit(500)
    }
    Write-Progress -Activity $activity -Completed
    if (-not $running.HasExited) {
        try { $running.PriorityClass = 'BelowNormal' } catch {}
        $script:OverridesStillBuilding = $true
        Write-Host ("  Shattered Legacy art and map are taking longer than $LimitSeconds s on this computer. " +
            $(if (& $previous) { 'Starting the game with the last good art; the new art is used from the next start.' } else { 'Starting the game without them this time; they are ready from the next start.' })) -ForegroundColor Yellow
        Write-Host "  (It carries on in the background. Its log: $(Join-Path $App 'logs'))" -ForegroundColor DarkGray
        return (& $previous)
    }
    # The child's own lines (a part it skipped, in yellow) as it said them, from the trace its pid file names (this
    # run's, or an earlier start's when this one found it still running).
    $io = @(Get-Content -LiteralPath (Join-Path $staging 'builder.pid') -ErrorAction SilentlyContinue)
    $tr = @($(if ($io.Count -ge 2) { Get-Content -LiteralPath $io[1] -ErrorAction SilentlyContinue }))
    foreach ($l in @($tr | Where-Object { $_ -match '^\S+  said: ' })) { Write-Host ('  ' + ($l -replace '^\S+  said: ', '')) -ForegroundColor Yellow }
    $failed = $started -and $running.ExitCode -ne 0
    if ((Test-Path -LiteralPath $staging) -and ($failed -or -not (Test-OverridesGenerationReady $staging))) {
        $err = $(if ($tr.Count) { ($tr[-1] -replace '^\S+  ', '') } else { '' })
        Write-Host ("  Shattered Legacy art and map could not be prepared$(if ($err) { ' (the last step it logged: ' + $err + ')' }). " +
            $(if (& $previous) { 'Starting the game with the last good art.' } else { 'Starting the game without it.' })) -ForegroundColor Yellow
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        return (& $previous)
    }
    $lock = Enter-OverridesLock $App $clock $LimitSeconds
    if (-not $lock) {
        Write-Host '  The new Shattered Legacy art is ready but could not be put in place yet (another start of the game holds it); it is used from the next start.' -ForegroundColor Yellow
        return (& $previous)
    }
    try { $swapped = Complete-StagedOverrides $App $clock $LimitSeconds } finally { Exit-OverridesLock $lock }
    if (-not $swapped) {
        Write-Host "  The new Shattered Legacy art is ready but could not be put in place yet ($script:OverridesSwapError); it is used from the next start." -ForegroundColor Yellow
        return (& $previous)
    }
    Write-Host ("  ...done in {0:0} s." -f $clock.Elapsed.TotalSeconds) -ForegroundColor Gray
    return (& $previous)
}
