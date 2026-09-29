#Requires -Version 5.1
<#
  Shattered Legacy package updater. Play.ps1 starts it; players never run it by hand.

  Play.ps1 downloads the new package, checks its size and SHA-256 against version.json,
  unpacks it under %TEMP%\sl-update\, copies THIS script out of the new package to
  %TEMP%\sl-update\Update.ps1 and starts it in a new window. So the updater that runs is
  always the new package's, and no file it replaces is the script that is running.

      Update.ps1 -Source <unpacked package> -Target <install> -Stage <temp> -WaitPid <pids> [-Test|-Lan]
      Update.ps1 -Target <install> -Restore

  The parameters are a contract with every Play.ps1 already installed on players'
  machines. Add to them; never rename or remove one.

  What it does:
    1. Waits for the launcher's cmd.exe, if it was started from a .bat, saying so, then
       for any TazUO.exe running from this install (and tells the player to close the
       game). If nothing differs from the new package, it changes nothing, makes no
       backup, says "Already up to date" and starts the game.
    2. Backs up app\ in full, plus any file outside app\ it will replace or retire, to
       <install>\update-backup\.
    3. Copies the new package over the install WITHOUT deleting anything, except the
       paths the new package lists in app\retired-files.txt. It never overwrites
       app\uo-path.txt, app\tazuo\settings*.json, or anything under app\tazuo\Data\
       (the player's profiles, and the XmlGumps where TazUO saves gump positions). A
       Data\XmlGumps\*.xml that the install does not have yet is added.
    4. Writes app\package-version.txt last, so an install that stopped halfway never
       claims the new version.
    5. On success deletes the backup; on any error copies it back. Then starts the game.

  If the machine dies partway, update-backup\ is still there; Play.ps1 sees it on the
  next start and runs this script with -Restore.

  Everything shown is also written to <install>\update-log.txt.

  Test hooks (probes only, never set on a player's machine):
    SL_UPDATE_FAIL_AFTER=<n>   throw after copying n files
    SL_UPDATE_PAUSE_SEC=<n>    shorten the 60-second pause after a failure

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as
  Windows-1252, and a non-ASCII character can become syntax.
#>
[CmdletBinding()]
param(
    [string]$Source,
    [Parameter(Mandatory = $true)][string]$Target,
    [string]$Stage,
    [string]$WaitPid,
    [switch]$Test,
    [switch]$Lan,
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'
$root       = (Resolve-Path -LiteralPath $Target).Path.TrimEnd('\')
$backup     = Join-Path $root 'update-backup'
$createdLog = Join-Path $backup '.created.txt'
$tazExe     = Join-Path $root 'app\tazuo\TazUO.exe'

function Say([string]$m, [string]$c = 'Gray') { Write-Host "  $m" -ForegroundColor $c }

# Seconds, or until a key is pressed. Read-Host would hang a probe run in a window
# nobody can type into, and a message that vanishes before it is read is no message.
function Wait-Key([int]$sec) {
    if ($env:SL_UPDATE_PAUSE_SEC) { $sec = [int]$env:SL_UPDATE_PAUSE_SEC }
    Say "Press any key to carry on (it carries on by itself in $sec seconds)."
    $end = (Get-Date).AddSeconds($sec)
    try {
        while ((Get-Date) -lt $end) {
            if ([Console]::KeyAvailable) { [void][Console]::ReadKey($true); return }
            Start-Sleep -Milliseconds 200
        }
    } catch { Start-Sleep -Seconds $sec }
}

# Copies the backup over the install and removes the files this update added.
# Safe to run twice, and safe on a backup that stopped halfway (every file in it is a
# copy of the install as it was).
function Restore-Install {
    if (Test-Path -LiteralPath $createdLog) {
        foreach ($r in Get-Content -LiteralPath $createdLog) {
            if (-not $r) { continue }
            $p = Join-Path $root $r
            if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force }
        }
    }
    foreach ($f in Get-ChildItem -LiteralPath $backup -Recurse -File -Force) {
        $r = $f.FullName.Substring($backup.Length + 1)
        if ($r -eq '.created.txt') { continue }
        $d = Join-Path $root $r
        $dir = Split-Path $d -Parent
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Copy-Item -LiteralPath $f.FullName -Destination $d -Force
    }
    Remove-Item -LiteralPath $backup -Recurse -Force
}

# Files an update never writes. $rel is relative to the install root, "\"-separated.
function Test-Kept([string]$rel) {
    $r = $rel.ToLowerInvariant()
    if ($r -eq 'app\uo-path.txt') { return $true }
    if ($r -eq 'app\package-version.txt') { return $true }      # written last, separately
    if ($r -match '^app\\tazuo\\settings[^\\]*\.json$') { return $true }
    if ($r.StartsWith('app\tazuo\data\')) {
        # A new XML gump is added; an existing one holds the player's saved position.
        if ($r -match '^app\\tazuo\\data\\xmlgumps\\[^\\]+\.xml$' -and -not (Test-Path -LiteralPath (Join-Path $root $rel))) { return $false }
        return $true
    }
    return $false
}

# The same rule Build-PlayerPackage.ps1 gates on. Returns $null when the line is allowed.
function Test-RetiredLine([string]$p) {
    if ($p.Contains('..')) { return 'contains ..' }
    if ($p.StartsWith('\') -or $p.Contains(':')) { return 'is an absolute path' }
    if ($p -match '[*?\[\]]') { return 'contains a wildcard' }
    $l = $p.ToLowerInvariant().TrimEnd('\')
    if ($l -eq 'app\tazuo\data' -or $l.StartsWith('app\tazuo\data\')) { return 'is under app\tazuo\Data\' }
    if (@('', '.', 'app', 'app\tazuo', 'update-backup', 'app\uo-path.txt') -contains $l -or $l -match '^app\\tazuo\\settings[^\\]*\.json$') { return 'is protected' }
    return $null
}

try { Start-Transcript -LiteralPath (Join-Path $root 'update-log.txt') -Append | Out-Null } catch {}

# --- -Restore: an update that stopped without cleaning up ----------------------------
if ($Restore) {
    if (-not (Test-Path -LiteralPath $backup)) { exit 0 }
    try {
        Restore-Install
        Say 'The last update did not finish. The version you had before it is back.' Yellow
        exit 0
    } catch {
        Say "Could not put the previous version back: $($_.Exception.Message)" Red
        Say 'Download the package again from https://get.shatteredlegacyuo.com and unzip it' Red
        Say 'over this folder. Your account and settings are on the server and are safe.' Red
        exit 1
    }
}

$Host.UI.RawUI.WindowTitle = 'Shattered Legacy update'
Write-Host ''
Say 'Shattered Legacy update' Cyan
Write-Host ''

$srcRoot = (Resolve-Path -LiteralPath $Source).Path.TrimEnd('\')
$newVersion = (Get-Content -LiteralPath (Join-Path $srcRoot 'app\package-version.txt') -TotalCount 1).Trim()
$ok = $false

# --- 1. wait: the launcher window, then the game ------------------------------------
# The launcher's cmd.exe must be gone before a .bat is replaced: cmd reads a batch file
# by byte offset as it runs, and a file changed under it executes whatever lands there.
# Only cmd.exe is waited for. Play.ps1 also passes its own PowerShell, but PowerShell
# reads a script whole before running it and Play.ps1 holds no file open, so nothing
# depends on it being gone. When Play.ps1 is run from an open PowerShell prompt, that
# PID is the prompt itself, which never exits, so waiting on it only ever ran out the
# full 60 seconds in a blank window. Every Play.ps1 already installed passes both PIDs,
# so the choice is made here, not there.
foreach ($id in "$WaitPid".Split(',', [StringSplitOptions]::RemoveEmptyEntries)) {
    $p = Get-Process -Id ([int]$id) -ErrorAction SilentlyContinue
    if (-not $p -or $p.ProcessName -ne 'cmd') { continue }
    Say 'Waiting for the launcher window to close...'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try { Wait-Process -Id ([int]$id) -Timeout 60 -ErrorAction Stop } catch {}
    if (Get-Process -Id ([int]$id) -ErrorAction SilentlyContinue) {
        Say ("It is still open after {0:N0} s. Carrying on." -f $sw.Elapsed.TotalSeconds) Yellow
    } else {
        Say ("It closed after {0:N1} s." -f $sw.Elapsed.TotalSeconds)
    }
}
$told = $false
while ($true) {
    # A TazUO with no readable path (another user's, or elevated) counts, to be safe.
    $running = @(Get-Process -Name TazUO -ErrorAction SilentlyContinue | Where-Object { -not $_.Path -or $_.Path -ieq $tazExe })
    if (-not $running.Count) { break }
    if (-not $told) {
        Say 'The game is still open from this folder, and its files cannot be replaced while it runs.' Yellow
        Say 'Log out and close the game window. The update carries on by itself once it is closed.' Yellow
        Say 'To skip the update instead, close this window. Nothing has been changed yet.' Yellow
        $told = $true
    }
    Start-Sleep -Seconds 2
}
if ($told) { Say 'The game is closed. Carrying on.' }

try {
    # An earlier update that died halfway: put it back before starting over.
    if (Test-Path -LiteralPath $backup) {
        Say 'An earlier update did not finish; putting that back first.' Yellow
        Restore-Install
    }

    # --- 2. what changes -------------------------------------------------------------
    $copy = @()
    foreach ($f in Get-ChildItem -LiteralPath $srcRoot -Recurse -File -Force) {
        $rel = $f.FullName.Substring($srcRoot.Length + 1)
        if (Test-Kept $rel) { continue }
        $dest = Join-Path $root $rel
        $isNew = -not (Test-Path -LiteralPath $dest)
        if (-not $isNew -and $f.Length -eq (Get-Item -LiteralPath $dest -Force).Length -and
            (Get-FileHash -LiteralPath $f.FullName).Hash -eq (Get-FileHash -LiteralPath $dest).Hash) { continue }
        $copy += [pscustomobject]@{ Rel = $rel; Src = $f.FullName; Dest = $dest; New = $isNew }
    }
    $retired = @()
    $rf = Join-Path $srcRoot 'app\retired-files.txt'
    if (Test-Path -LiteralPath $rf) {
        foreach ($line in Get-Content -LiteralPath $rf) {
            $p = $line.Trim().Replace('/', '\')
            if (-not $p -or $p.StartsWith('#')) { continue }
            $why = Test-RetiredLine $p
            if ($why) { Say "Ignoring retired-files.txt line '$p': it $why." Yellow; continue }
            if (Test-Path -LiteralPath (Join-Path $root $p)) { $retired += $p.TrimEnd('\') }
        }
    }
    Say ("{0} files to add, {1} to replace, {2} to remove." -f @($copy | Where-Object New).Count, @($copy | Where-Object { -not $_.New }).Count, $retired.Count)

    $pv = Join-Path $root 'app\package-version.txt'
    if (-not $copy.Count -and -not $retired.Count) {
        # Nothing to do: no backup, no copy. This is what -Update on the current version
        # comes to, and it still proves the offer, download, checksum and unpack. The
        # version file is outside $copy (Test-Kept), so write it if it is not already right.
        $have = $null
        if (Test-Path -LiteralPath $pv) { $have = (Get-Content -LiteralPath $pv -TotalCount 1).Trim() }
        if ($have -ne $newVersion) {
            Copy-Item -LiteralPath (Join-Path $srcRoot 'app\package-version.txt') -Destination $pv -Force
            Unblock-File -LiteralPath $pv
        }
        $ok = $true
        Write-Host ''
        Say "Already up to date: $newVersion. Nothing was changed." Green
    } else {
        # --- 3. back up ------------------------------------------------------------------
        Say 'Backing up the current version...'
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        [IO.File]::WriteAllText($createdLog, '')
        Copy-Item -LiteralPath (Join-Path $root 'app') -Destination (Join-Path $backup 'app') -Recurse -Force
        $outside = @($copy | Where-Object { -not $_.New } | ForEach-Object Rel) + $retired | Where-Object { -not $_.ToLowerInvariant().StartsWith('app\') }
        foreach ($r in $outside) {
            $b = Join-Path $backup $r
            $dir = Split-Path $b -Parent
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            Copy-Item -LiteralPath (Join-Path $root $r) -Destination $b -Recurse -Force
        }
        # From here until the end the install claims no version, so a stop anywhere in
        # between is offered the update again rather than taken for finished.
        if (Test-Path -LiteralPath $pv) { Remove-Item -LiteralPath $pv -Force }

        # --- 4. copy and retire ----------------------------------------------------------
        Say 'Installing the new version...'
        $failAfter = 0
        if ($env:SL_UPDATE_FAIL_AFTER) { $failAfter = [int]$env:SL_UPDATE_FAIL_AFTER }
        $n = 0
        foreach ($c in $copy) {
            if ($failAfter -and $n -ge $failAfter) { throw "test failure injected after $n files (SL_UPDATE_FAIL_AFTER)" }
            $dir = Split-Path $c.Dest -Parent
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            # Recorded before it exists, so a restore after a crash removes it too.
            if ($c.New) { [IO.File]::AppendAllText($createdLog, $c.Rel + "`r`n") }
            Copy-Item -LiteralPath $c.Src -Destination $c.Dest -Force
            Unblock-File -LiteralPath $c.Dest
            $n++
        }
        foreach ($r in $retired) {
            Remove-Item -LiteralPath (Join-Path $root $r) -Recurse -Force
            Say "removed $r"
        }
        Copy-Item -LiteralPath (Join-Path $srcRoot 'app\package-version.txt') -Destination $pv -Force
        Unblock-File -LiteralPath $pv

        # --- 5. done ---------------------------------------------------------------------
        # Renamed before it is deleted: a half-deleted backup must never be restored over
        # a finished update. Play.ps1 removes a leftover update-backup.done.
        $done = "$backup.done"
        if (Test-Path -LiteralPath $done) { Remove-Item -LiteralPath $done -Recurse -Force }
        Rename-Item -LiteralPath $backup -NewName (Split-Path $done -Leaf)
        try { Remove-Item -LiteralPath $done -Recurse -Force } catch {}
        $ok = $true
        Write-Host ''
        Say "Updated to $newVersion. Your account, settings and gump positions were kept." Green
    }
} catch {
    Write-Host ''
    Say "The update stopped: $($_.Exception.Message)" Red
    if (Test-Path -LiteralPath $backup) {
        try {
            Restore-Install
            Say 'Everything was put back as it was. You are still on the version you had.' Yellow
        } catch {
            Say "Putting the old version back ALSO failed: $($_.Exception.Message)" Red
            Say "The copy from before the update is in $backup." Red
            Say 'Download the package again from https://get.shatteredlegacyuo.com and unzip it' Red
            Say 'over this folder, and send Chase update-log.txt from this folder.' Red
            Wait-Key 600
            exit 1
        }
    } else {
        Say 'Nothing had been changed yet. You are still on the version you had.' Yellow
    }
    Say 'If this keeps happening, send Chase update-log.txt from this folder.' Yellow
    Wait-Key 60
}

if ($Stage -and (Test-Path -LiteralPath $Stage)) {
    try { Remove-Item -LiteralPath $Stage -Recurse -Force } catch { Say "Could not remove $Stage; it is safe to delete." }
}
try { Stop-Transcript | Out-Null } catch {}

# --- start the game -----------------------------------------------------------------
# Play.ps1 directly, with what "Play Shattered Legacy.bat -NoUpdate" would pass it, and
# the same pause on failure. Quoting a .bat path with spaces through cmd /c from
# PowerShell 5.1 is fragile; this is the same launch without it.
$playArgs = @('-NoUpdate')
if ($Test) { $playArgs += '-Test' }
if ($Lan)  { $playArgs += '-Lan' }
Write-Host ''
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'app\Play.ps1') @playArgs
if ($LASTEXITCODE -ne 0) { Read-Host '  Press Enter to close' | Out-Null }
if ($ok) { exit 0 } else { exit 1 }
