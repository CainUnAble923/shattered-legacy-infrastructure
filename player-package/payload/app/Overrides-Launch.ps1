#Requires -Version 5.1
<#
  Overrides-Launch.ps1 (cc-P411): how the launcher and the updater get Shattered Legacy's art, text and map ready
  without ever keeping anyone from playing. Dot-sourced by Play.ps1 and Update.ps1; player-package\tests\
  Launch-Overrides.Tests.ps1 calls the same functions.

      Invoke-UoOverrides -App <app folder> -UoDir <Ultima Online folder> [-LimitSeconds 60] [-Reason play|update]

  Returns the path of files-override.txt for TazUO's -uofilesoverride, or $null to start without one. In order:
    1. A staged set that finished while the game was running (app\uo-overrides.next\ with its ready file) is swapped
       in: the current folder becomes uo-overrides.old, the staged one uo-overrides, then the old one is deleted. If
       the game still holds the files, nothing moves and the swap is tried at the next start.
    2. Build-UoOverrides.ps1 -CheckOnly compares the stamps (fast). Up to date: done, the current list.
    3. Otherwise a rebuild in a child PowerShell, into app\uo-overrides.next\ (never into the folder the game reads),
       with its trace in app\logs\overrides-<time>.log (the last 10 are kept) and its progress in
       uo-overrides.next\progress.json. One line before ("Preparing Shattered Legacy art and map, one time after an
       update...") and one after ("...done in N s"), and a progress bar the whole time: the step, its count, the
       seconds, redrawn twice a second even while a step reports nothing.
    4. Done within the limit (app\launch-options.json "overridesTimeLimitSeconds", default 60): swapped in, its list
       returned. Not done: one yellow line, the game starts with the last good set (or none), and the rebuild carries
       on at low priority; its set is swapped in at the next start. A rebuild that stopped without its ready file
       left only a staging folder, which the next start deletes and builds again.

  Test hook (never set on a player's machine): SL_OVERRIDES_LIMIT_SEC=<n> replaces the limit.
  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as Windows-1252.
#>

$script:OverridesProgressShown = New-Object 'System.Collections.Generic.List[string]'

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

# The rebuild child still running for this install, from the pid file it was started with; $null when none.
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

# Step 1: a finished staged set becomes the current one. $true when it was swapped in (or there was nothing to do).
function Complete-StagedOverrides([string]$App) {
    $live = Join-Path $App 'uo-overrides'
    $staging = Join-Path $App 'uo-overrides.next'
    $old = Join-Path $App 'uo-overrides.old'
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue }
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'ready.txt'))) { return $true }
    if (Get-OverridesBuilder $staging) { $script:OverridesSwapError = 'its rebuild is still running'; return $false }
    # Each rename is tried for a few seconds: a scanner can hold a file it just saw written for a moment. One the game
    # holds stays held, and then nothing moves.
    $move = {
        param($from, $to)
        for ($i = 0; $i -lt 15; $i++) {
            try { Rename-Item -LiteralPath $from -NewName $to -ErrorAction Stop; return $true } catch { $script:OverridesSwapError = $_.Exception.Message; Start-Sleep -Milliseconds 300 }
        }
        $false
    }
    if ((Test-Path -LiteralPath $live) -and -not (& $move $live 'uo-overrides.old')) { return $false }
    if (-not (& $move $staging 'uo-overrides')) {
        if (Test-Path -LiteralPath $old) { [void](& $move $old 'uo-overrides') }
        return $false
    }
    foreach ($n in 'ready.txt', 'progress.json', 'builder.pid') { Remove-Item -LiteralPath (Join-Path $live $n) -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue }
    $true
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
    $status = ('{0}{1}, {2:0} s' -f $step, $count, $Seconds)
    $script:OverridesProgressShown.Add(('{0:0.0} {1}' -f $Seconds, $status))
    if ($pct -ge 0) { Write-Progress -Activity $Activity -Status $status -PercentComplete $pct }
    else { Write-Progress -Activity $Activity -Status $status }
}

function Invoke-UoOverrides {
    param([Parameter(Mandatory = $true)][string]$App, [Parameter(Mandatory = $true)][string]$UoDir, [int]$LimitSeconds = -1,
          [ValidateSet('play', 'update')][string]$Reason = 'play')
    $live = Join-Path $App 'uo-overrides'
    $staging = Join-Path $App 'uo-overrides.next'
    $builder = Join-Path $App 'Build-UoOverrides.ps1'
    $liveList = Join-Path $live 'files-override.txt'
    $previous = { if (Test-Path -LiteralPath $liveList) { $liveList } else { $null } }
    if ($LimitSeconds -lt 0) { $LimitSeconds = Get-OverridesTimeLimit $App }

    [void](Complete-StagedOverrides $App)

    # --- 2. up to date? ------------------------------------------------------------------------------
    $running = Get-OverridesBuilder $staging
    if (-not $running) {
        $chk = $null
        try { $chk = & $builder -UoDir $UoDir -OutDir $live -CheckOnly } catch { $chk = $null }
        $chk = @($chk | Where-Object { $_ -and $_.PSObject.Properties['Fresh'] })[0]
        if ($chk -and $chk.Fresh) { return $chk.List }
    }

    # --- 3. a rebuild in a child ---------------------------------------------------------------------
    $activity = 'Preparing Shattered Legacy art and map'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    Write-Host "  Preparing Shattered Legacy art and map, one time after an update$(if ($Reason -eq 'update') { ' (as part of it)' })..." -ForegroundColor Gray
    if (-not $running) {
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
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
    }

    # --- 4. wait, with the bar, up to the limit ------------------------------------------------------
    while (-not $running.HasExited -and $clock.Elapsed.TotalSeconds -lt $LimitSeconds) {
        Show-OverridesProgress $staging $clock.Elapsed.TotalSeconds $activity
        [void]$running.WaitForExit(500)
    }
    Write-Progress -Activity $activity -Completed
    if (-not $running.HasExited) {
        try { $running.PriorityClass = 'BelowNormal' } catch {}
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
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'ready.txt'))) {
        $err = $(if ($tr.Count) { ($tr[-1] -replace '^\S+  ', '') } else { '' })
        Write-Host ("  Shattered Legacy art and map could not be prepared$(if ($err) { ' (the last step it logged: ' + $err + ')' }). " +
            $(if (& $previous) { 'Starting the game with the last good art.' } else { 'Starting the game without it.' })) -ForegroundColor Yellow
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        return (& $previous)
    }
    if (-not (Complete-StagedOverrides $App)) {
        Write-Host "  The new Shattered Legacy art is ready but could not be put in place yet ($script:OverridesSwapError); it is used from the next start." -ForegroundColor Yellow
        return (& $previous)
    }
    Write-Host ("  ...done in {0:0} s." -f $clock.Elapsed.TotalSeconds) -ForegroundColor Gray
    return (& $previous)
}
