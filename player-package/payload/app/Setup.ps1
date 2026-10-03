#Requires -Version 5.1
<#
  Shattered Legacy player package: find the Ultima Online client data, or fetch EA's free
  installer, then wait while EA's own launcher downloads the game, and carry on.

      Setup.ps1                     find it, or offer to install it
      Setup.ps1 -UoPath "E:\UO"     use this folder
      Setup.ps1 -DownloadOnly       fetch and check EA's installer, print where it is, run nothing

  We ship none of EA's files. The data comes from EA's own free Classic Client, which
  the player installs with EA's installer. On success the folder is remembered in
  app\uo-path.txt and this script prints nothing more on the next run.

  Exit codes: 0 = data found and usable. 1 = not found or not usable (message printed).

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as
  Windows-1252, and a non-ASCII character can become syntax.
#>
[CmdletBinding()]
param([string]$UoPath, [switch]$DownloadOnly, [double]$StallMinutes = 10, [string]$ProbeFolder, [string]$ProbeInstaller)

$ErrorActionPreference = 'Stop'
$app      = $PSScriptRoot
$pathFile = Join-Path $app 'uo-path.txt'

# The live shard's data auto-detects as 7.0.114.40 and clientVerification kicks anything
# older (modernuo.json: clientVerification.enable True, invalidClientResponse Kick). EA's
# installer is 7.0.24.0 until its launcher patches it, so an unpatched install connects
# and is kicked 20 seconds later. Refuse it here, with the reason, instead.
$minVersion = [version]'7.0.114.40'

# Files TazUO cannot run without. A folder missing any of these is not a UO install.
$required = @('client.exe','tiledata.mul','hues.mul','cliloc.enu',
              'artLegacyMUL.uop','gumpartLegacyMUL.uop','map0LegacyMUL.uop')

# Verified 2026-09-26: https://uo.com/client-download/ links exactly this file, served
# 18,827,017 bytes, Last-Modified 2021-03-26. It is NOT Authenticode-signed, so the only
# integrity check available is this hash. If EA replaces the file the hash changes and
# setup stops and says so, rather than running something it cannot vouch for.
$pageUrl       = 'https://uo.com/client-download/'
$knownUrl      = 'https://downloads.eamythic.com/uo/installers/UOClassicSetup_7_0_24_0.exe'
$knownSha256   = 'c19f93f979b105b7e5e515369e87968cdac9d950b8de6b7a9442985ba1ced7a6'

function Get-UoVersion([string]$dir) {
    try { $fv = (Get-Item -LiteralPath (Join-Path $dir 'client.exe')).VersionInfo } catch { return [version]'0.0.0.0' }
    return New-Object Version $fv.FileMajorPart, $fv.FileMinorPart, $fv.FileBuildPart, $fv.FilePrivatePart
}

# Returns $null when usable, otherwise the reason it is not.
function Test-UoFolder([string]$dir) {
    if (-not $dir -or -not (Test-Path -LiteralPath $dir -PathType Container)) { return 'folder does not exist' }
    $missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $dir $_)) })
    if ($missing.Count) { return "missing $($missing -join ', ')" }
    $v = Get-UoVersion $dir
    if ($v -lt $minVersion) {
        return "client is version $v, the server needs $minVersion or newer"
    }
    return $null
}

# --- EA's launcher -------------------------------------------------------------------
# EA's installer carries no game data. It installs only EA's launcher, UO.exe, which then
# downloads the game (cc-P40 A.1: 1,765,126,031 bytes, about 10 minutes, 2.74 GB on disk).
# So a folder holding UO.exe that fails Test-UoFolder is "installed, not patched yet", or
# patched to a version below $minVersion, and waiting for that launcher is the fix (cc-P44).
#   - UO.exe asks for administrator (its manifest: requireAdministrator), so starting it
#     brings up Windows' permission prompt.
#   - It restarts itself as a copy named UO.bin, so both names count as "running".
#   - It logs to <folder>\logs\patcher.<MMddyy>.Log, lines "[yyyy/MM/dd HH:mm:ss] text":
#     "Patch size is <bytes>" near the start, "Patch Operation Complete." at the end.
# Nothing here writes into EA's folder: EA's launcher must keep owning it (cc-P26).
#
# Test-only parameters (cc-P44 probes; Play.ps1 passes none of them, gate 4 checks that):
#   -ProbeFolder <dir>      look only in this folder: no remembered path, registry or defaults
#   -ProbeInstaller <file>  with -ProbeFolder only: run this instead of EA's checked installer
#   -StallMinutes <n>       how long nothing may change before asking (default 10)
$gameBytesGuess   = 2744000000   # on disk after EA's patch: 2,743,994,359 measured (cc-P40)
$diskPerPatchByte = 1.55     # 2,743,994,359 on disk / 1,765,126,031 "Patch size" (same run)
$installerGraceS  = 30       # EA's installer starts its launcher itself, 8 s after it finished
$goneSeconds      = 15       # UO.exe hands over to UO.bin; a short gap is not an exit
$closeSeconds     = 15
$pollMs           = 2000
if ($ProbeInstaller -and -not $ProbeFolder) { Write-Host '  -ProbeInstaller is a test option and needs -ProbeFolder.' -ForegroundColor Red; exit 1 }

function Test-Patchable([string]$dir) {
    return [bool]($dir -and (Test-Path -LiteralPath (Join-Path $dir 'UO.exe') -PathType Leaf))
}

function Find-UoFolder {
    $candidates = New-Object System.Collections.Generic.List[string]
    if ($ProbeFolder) { $candidates.Add($ProbeFolder) } else {
        if (Test-Path $pathFile) { $candidates.Add((Get-Content $pathFile -TotalCount 1).Trim()) }
        foreach ($k in @(
            'HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic',
            'HKLM:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic',
            'HKCU:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic')) {
            $p = Get-ItemProperty -Path $k -Name InstallDir -ErrorAction SilentlyContinue
            if ($p) { $candidates.Add($p.InstallDir) }
        }
        # EA's installer also registers its launcher here; the default value is UO.exe's path.
        foreach ($k in @(
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\UO.exe',
            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\UO.exe')) {
            $exePath = $null
            try { $exePath = (Get-Item -LiteralPath $k -ErrorAction Stop).GetValue('') } catch {}
            if ($exePath) { $candidates.Add((Split-Path $exePath.Trim('"') -Parent)) }
        }
        foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, 'C:\Program Files (x86)', 'C:\Program Files')) {
            if ($base) {
                $candidates.Add((Join-Path $base 'Electronic Arts\Ultima Online Classic'))
                $candidates.Add((Join-Path $base 'EA Games\Ultima Online Classic'))
            }
        }
    }
    $firstReason = $null
    $patch = $null
    foreach ($c in ($candidates | Select-Object -Unique)) {
        if (-not $c) { continue }
        $why = Test-UoFolder $c
        if (-not $why) { return @{ Path = $c; Patch = $null; Reason = $null } }
        if (Test-Path -LiteralPath $c) {
            if (-not $firstReason) { $firstReason = "$c : $why" }
            if (-not $patch -and (Test-Patchable $c)) { $patch = $c }
        }
    }
    return @{ Path = $null; Patch = $patch; Reason = $firstReason }
}

function Save-UoPath([string]$dir) {
    [IO.File]::WriteAllText($pathFile, $dir, (New-Object Text.UTF8Encoding $false))
}

function Stop-WithHelp([string]$msg, [string]$PatchDir) {
    Write-Host ''
    Write-Host "  $msg" -ForegroundColor Red
    Write-Host ''
    if ($PatchDir) {
        Write-Host '  To finish it:' -ForegroundColor Yellow
        Write-Host "    1. Check the internet connection, and that the drive with $PatchDir has about 3 GB free." -ForegroundColor Yellow
        Write-Host '    2. Double-click "Play Shattered Legacy" again. It starts EA''s launcher and waits for it.' -ForegroundColor Yellow
        Write-Host "  EA's launcher keeps its own log in $PatchDir\logs, if Chase asks for it." -ForegroundColor Yellow
        Write-Host ''
        exit 1
    }
    Write-Host '  To install Ultima Online by hand:' -ForegroundColor Yellow
    Write-Host "    1. Open $pageUrl in a web browser." -ForegroundColor Yellow
    Write-Host '    2. Download and run the "Classic Client" installer.' -ForegroundColor Yellow
    Write-Host '    3. Double-click "Play Shattered Legacy" again. It waits for EA''s launcher to finish, then starts the game.' -ForegroundColor Yellow
    Write-Host '  Installed it somewhere unusual? Tell this package where, then play:' -ForegroundColor Yellow
    Write-Host '    app\Setup.bat "D:\Games\Ultima Online Classic"' -ForegroundColor Yellow
    Write-Host ''
    exit 1
}

# The path of a running process, also for one running as administrator (EA's launcher):
# Process.Path needs rights a normal window does not have over an elevated process, this
# asks only for PROCESS_QUERY_LIMITED_INFORMATION. Compiled only when a wait starts, so a
# normal start pays nothing for it.
function Initialize-ProcHelper {
    if ('SlSetupProc' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class SlSetupProc {
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h, int flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    public static string ImagePath(int pid) {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return null;
        try {
            var sb = new StringBuilder(1024); int n = sb.Capacity;
            return QueryFullProcessImageNameW(h, 0, sb, ref n) ? sb.ToString(0, n) : null;
        } finally { CloseHandle(h); }
    }
}
'@
}

# EA's launcher processes (UO.exe or UO.bin) running from $dir. One whose path cannot be
# read at all counts too: better to wait than to start a second copy.
function Get-EaPatcher([string]$dir) {
    $prefix = [IO.Path]::GetFullPath($dir).TrimEnd('\') + '\'
    @(Get-Process -Name 'UO', 'UO.bin' -ErrorAction SilentlyContinue | Where-Object {
        $p = [SlSetupProc]::ImagePath($_.Id)
        (-not $p) -or $p.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
    })
}

function Get-FolderBytes([string]$dir) {
    $sum = [long]0
    try {
        foreach ($f in [IO.Directory]::EnumerateFiles($dir, '*', [IO.SearchOption]::AllDirectories)) {
            try { $sum += (New-Object IO.FileInfo $f).Length } catch {}
        }
    } catch {}
    return $sum
}

# What EA's launcher's own log says. Size: the newest "Patch size" line. Complete and
# Error: only lines stamped $since or later, so an earlier run's "Complete" never counts.
# Stamp changes whenever the log grows. Errors skip the lines every run writes, even a good
# one (patcher.090626.Log: string table, UI copy, module http).
function Read-PatchLog([string]$dir, [datetime]$since) {
    $r = @{ Size = [long]0; Complete = $false; Error = $null; Stamp = '' }
    $logs = @(Get-ChildItem -LiteralPath (Join-Path $dir 'logs') -Filter 'patcher.*.Log' -File -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTimeUtc | Select-Object -Last 2)
    foreach ($log in $logs) {
        $r.Stamp += "$($log.Name):$($log.Length):$($log.LastWriteTimeUtc.Ticks) "
        try {
            $fs = New-Object IO.FileStream($log.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
            $sr = New-Object IO.StreamReader($fs)
            try { $text = $sr.ReadToEnd() } finally { $sr.Dispose() }
        } catch { continue }
        foreach ($line in $text -split "`r?`n") {
            if ($line -notmatch '^\[(\d{4}/\d\d/\d\d \d\d:\d\d:\d\d)\] (.*)$') { continue }
            $msg = $Matches[2]
            $t = [datetime]::ParseExact($Matches[1], 'yyyy/MM/dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
            if ($msg -match '^Patch size is (\d+)') { $r.Size = [long]$Matches[1] }
            if ($t -lt $since) { continue }
            if ($msg -like 'Patch Operation Complete*') { $r.Complete = $true }
            elseif ($msg -like 'Patch Operation Restarting*') { $r.Complete = $false }
            if ($msg -match 'fail|error' -and $msg -notmatch 'string table|UI copy|load module|splash') { $r.Error = $msg }
        }
    }
    return $r
}

function Start-EaPatcher([string]$dir) {
    Write-Host "  Starting EA's launcher ($dir\UO.exe)." -ForegroundColor Cyan
    Write-Host '  Windows will ask for permission: that request is EA''s launcher, which updates its own folder.' -ForegroundColor Cyan
    try {
        Start-Process -FilePath (Join-Path $dir 'UO.exe') -WorkingDirectory $dir | Out-Null
        return $true
    } catch {
        Write-Host "  EA's launcher did not start: $($_.Exception.Message)" -ForegroundColor Yellow
        return $false
    }
}

# Gently: the same as clicking its X. Never killed. Left open, EA's window shows a Play
# button that connects to EA's servers, not to ours.
function Close-EaPatcher([string]$dir) {
    $procs = @(Get-EaPatcher $dir)
    if (-not $procs.Count) { return }
    foreach ($p in $procs) {
        try { if ($p.MainWindowHandle -ne [IntPtr]::Zero) { [void]$p.CloseMainWindow() } } catch {}
    }
    $until = (Get-Date).AddSeconds($closeSeconds)
    while ((Get-Date) -lt $until -and @(Get-EaPatcher $dir).Count) { Start-Sleep -Milliseconds 500 }
    if (@(Get-EaPatcher $dir).Count) {
        Write-Host '  EA''s launcher is still open. Close it yourself; do not press its Play button,' -ForegroundColor Yellow
        Write-Host '  which connects to EA''s servers, not to Shattered Legacy.' -ForegroundColor Yellow
    }
}

# One wait, until EA's launcher is done or gives up. Done means BOTH: the folder passes
# Test-UoFolder AND the launcher logged "Patch Operation Complete." since $since (or has
# exited). client.exe alone proves nothing: it arrives before the rest.
function Watch-EaPatch([string]$dir, [datetime]$since, [bool]$fresh, [string]$activity) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $lastSeen = Get-Date; $lastChange = Get-Date; $lastBytes = [long]-1; $lastStamp = ''; $suspect = 0
    while ($true) {
        $log  = Read-PatchLog $dir $since
        $now  = Get-Date
        if (@(Get-EaPatcher $dir).Count) { $lastSeen = $now }
        $gone = ($now - $lastSeen).TotalSeconds -ge $goneSeconds
        $why  = Test-UoFolder $dir
        if (-not $why -and ($log.Complete -or $gone)) {
            Write-Progress -Activity $activity -Completed
            Close-EaPatcher $dir
            return @{ Kind = 'done' }
        }
        # Finished but not ready. Seen twice, 2 s apart, so a log read racing the last file
        # write is not a failure.
        if ($log.Complete -or $gone) { $suspect++ } else { $suspect = 0 }
        if ($suspect -ge 2) {
            Write-Progress -Activity $activity -Completed
            if ($log.Complete -and -not $gone) { Close-EaPatcher $dir }
            return @{ Kind = $(if ($log.Complete) { 'complete' } else { 'exited' }); Why = $why; Error = $log.Error }
        }

        $bytes = Get-FolderBytes $dir
        if ($bytes -ne $lastBytes -or $log.Stamp -ne $lastStamp) {
            $lastChange = $now; $lastBytes = $bytes; $lastStamp = $log.Stamp
        } elseif (($now - $lastChange).TotalMinutes -ge $StallMinutes) {
            Write-Progress -Activity $activity -Completed
            Write-Host ''
            Write-Host "  Nothing has changed for $StallMinutes minutes: no new files from EA's launcher and nothing new in its log." -ForegroundColor Yellow
            Write-Host '  A slow or dropped connection looks like this. EA''s own window may say more.' -ForegroundColor Yellow
            $ans = Read-Host '  Keep waiting? [Y/n]'
            if ($ans -notmatch '^\s*(y|yes)?\s*$') { return @{ Kind = 'stopped' } }
            $lastChange = Get-Date
        }

        $elapsed = '{0} min {1:00} s' -f [int][Math]::Floor($sw.Elapsed.TotalMinutes), $sw.Elapsed.Seconds
        if ($fresh) {
            $expect = [Math]::Max([double]$gameBytesGuess, $log.Size * $diskPerPatchByte)
            $pct = [int][Math]::Min(99, $bytes * 100 / $expect)
            Write-Progress -Activity $activity -Status ('{0:N0} MB of about {1:N0} MB ({2}%), {3}' -f ($bytes / 1MB), ($expect / 1MB), $pct, $elapsed) -PercentComplete $pct
        } else {
            Write-Progress -Activity $activity -Status "updating, $elapsed"
        }
        Start-Sleep -Milliseconds $pollMs
    }
}

# Returns when $dir is ready; otherwise ends the script with the reason. Runs right after
# EA's installer (-AfterInstaller) and on any later start that finds a folder EA's launcher
# has not finished. Writes nothing anywhere, so Ctrl+C or closing the window leaves nothing
# half-done of ours.
function Wait-EaPatch([string]$dir, [datetime]$since, [switch]$AfterInstaller) {
    Initialize-ProcHelper
    $fresh = (Get-FolderBytes $dir) -lt 1GB
    Write-Host ''
    if ($fresh) {
        $activity = 'EA''s launcher is downloading Ultima Online'
        Write-Host '  EA''s launcher is downloading Ultima Online (about 1.8 GB). Leave it open.' -ForegroundColor Cyan
    } else {
        $activity = 'EA''s launcher is updating Ultima Online'
        Write-Host "  Ultima Online needs an update from EA first: $(Test-UoFolder $dir)." -ForegroundColor Cyan
        Write-Host '  EA''s launcher does that. Leave it open.' -ForegroundColor Cyan
    }
    Write-Host '  This window carries on by itself when it is done. Do not press Play in EA''s window:' -ForegroundColor Cyan
    Write-Host '  that connects to EA''s servers, not to Shattered Legacy.' -ForegroundColor Cyan
    Write-Host ''

    $grace = $(if ($AfterInstaller) { (Get-Date).AddSeconds($installerGraceS) } else { Get-Date })
    $offered = $false
    while ($true) {
        # Never a second copy: start it only when none runs from this folder, and after EA's
        # installer only once the installer has had its chance to start it.
        while (-not @(Get-EaPatcher $dir).Count -and (Get-Date) -lt $grace) { Start-Sleep -Milliseconds 1000 }
        $outcome = $null
        if (-not @(Get-EaPatcher $dir).Count) {
            if (-not (Start-EaPatcher $dir)) { $outcome = @{ Kind = 'notstarted' } }
        }
        if (-not $outcome) { $outcome = Watch-EaPatch $dir $since $fresh $activity }
        if ($outcome.Kind -eq 'done') { return }

        if ($outcome.Kind -eq 'stopped') {
            Stop-WithHelp 'Stopped waiting. EA''s launcher may still finish by itself; leave its window open if it is there.' -PatchDir $dir
        }
        # "missing tiledata.mul, ..." means nothing to a player; a version does.
        $why = $(if ("$($outcome.Why)" -like 'missing *') { 'the download is not complete' } else { $outcome.Why })
        if ($outcome.Kind -eq 'exited')   { Write-Host "  EA's launcher closed before Ultima Online was ready: $why." -ForegroundColor Yellow }
        if ($outcome.Kind -eq 'complete') { Write-Host "  EA's launcher finished, but Ultima Online is not ready: $why." -ForegroundColor Yellow }
        if ($outcome.Error) { Write-Host "  Its log says: $($outcome.Error)" -ForegroundColor Yellow }
        if (-not $offered) {
            $offered = $true
            $ans = Read-Host '  Start EA''s launcher again? [Y/n]'
            if ($ans -match '^\s*(y|yes)?\s*$') {
                $since = (Get-Date).AddSeconds(-2)
                $grace = Get-Date
                continue
            }
        }
        if ($outcome.Kind -eq 'notstarted') {
            Stop-WithHelp 'EA''s launcher did not start. It needs Windows'' permission to download the game: answer Yes when Windows asks.' -PatchDir $dir
        }
        Stop-WithHelp 'Ultima Online is installed, but EA''s launcher has not finished downloading it.' -PatchDir $dir
    }
}

function Complete-AfterPatch([string]$dir, [datetime]$since, [switch]$AfterInstaller) {
    Wait-EaPatch $dir $since -AfterInstaller:$AfterInstaller
    Save-UoPath $dir
    Write-Host "  Ultima Online is ready: $dir (version $(Get-UoVersion $dir))" -ForegroundColor Green
    exit 0
}

# --- 1. an explicit folder wins ------------------------------------------------
if ($UoPath) {
    $why = Test-UoFolder $UoPath
    if ($why -and (Test-Patchable $UoPath)) { Complete-AfterPatch $UoPath (Get-Date).AddSeconds(-2) }
    if ($why) { Stop-WithHelp "That folder cannot be used: $why." }
    Save-UoPath $UoPath
    Write-Host "  Ultima Online files: $UoPath (version $(Get-UoVersion $UoPath))" -ForegroundColor Green
    exit 0
}

# --- 2. look for an existing install ------------------------------------------
if (-not $DownloadOnly) {
    $found = Find-UoFolder
    if ($found.Path) {
        Save-UoPath $found.Path
        exit 0
    }
    # Installed, but EA's launcher has not finished (or not started) downloading the game.
    if ($found.Patch) { Complete-AfterPatch $found.Patch (Get-Date).AddSeconds(-2) }
    if ($found.Reason) {
        # A folder that looks like UO but has no EA launcher in it to finish the job.
        Stop-WithHelp "Found Ultima Online, but it cannot be used: $($found.Reason)."
    }

    # --- 3. nothing installed: fetch EA's installer and hand it over -----------
    Write-Host ''
    Write-Host '  Ultima Online is not installed on this computer yet.' -ForegroundColor Cyan
    Write-Host '  It is free, from EA. This will download EA''s installer (about 19 MB) and start it.' -ForegroundColor Cyan
    Write-Host '  After it installs, EA''s launcher downloads the game itself (about 1.8 GB).' -ForegroundColor Cyan
    Write-Host ''
    $answer = Read-Host '  Download and run EA''s installer now? (y/n)'
    if ($answer -notmatch '^[Yy]') { Stop-WithHelp 'Not installed. Nothing was downloaded.' }
}

if ($ProbeInstaller) {
    # Test only (cc-P44): a stand-in for EA's installer. Never reached from Play.ps1.
    $exe = $ProbeInstaller
} else {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    # Ask the official page which installer it links today, rather than trusting our memory.
    $url = $null
    try {
        $page = Invoke-WebRequest -Uri $pageUrl -UseBasicParsing -TimeoutSec 30
        $m = [regex]::Match($page.Content, 'https://[^"''\s<>]+/UOClassicSetup_[0-9_]+\.exe')
        if ($m.Success) { $url = $m.Value }
    } catch {
        Stop-WithHelp "Could not open $pageUrl ($($_.Exception.Message)). Check the internet connection."
    }
    if (-not $url) { Stop-WithHelp "$pageUrl no longer links a Classic Client installer this script recognises." }
    if ($url -ne $knownUrl) {
        Stop-WithHelp "EA's download page now links a different installer ($url) than the one this package was checked against. Not running an unchecked file."
    }

    $exe = Join-Path $env:TEMP ([IO.Path]::GetFileName($url))
    Write-Host "  downloading $url" -ForegroundColor Gray
    try {
        Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing -TimeoutSec 600
    } catch {
        Stop-WithHelp "The download failed: $($_.Exception.Message)"
    }
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLower()
    if ($hash -ne $knownSha256) {
        Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue
        Stop-WithHelp "The downloaded installer is not the file this package was checked against (SHA-256 $hash). Deleted it without running it."
    }
    Unblock-File -LiteralPath $exe
    if ($DownloadOnly) {
        Write-Host "  EA's installer, checked (SHA-256 matches): $exe" -ForegroundColor Green
        exit 0
    }
}

# EA's license is the player's to accept: EA's installer always runs with its own window,
# never silently (cc-P40 decision 1).
Write-Host ''
Write-Host '  Starting EA''s installer. This part is EA''s, not ours:' -ForegroundColor Cyan
Write-Host '    - Windows may ask for permission. That request comes from EA''s installer.' -ForegroundColor Cyan
Write-Host '    - Read EA''s license and accept it yourself, and accept the default folder.' -ForegroundColor Cyan
Write-Host '    - Then EA''s launcher downloads the game (about 1.8 GB). Leave it open: this' -ForegroundColor Cyan
Write-Host '      window waits for it and starts Shattered Legacy by itself.' -ForegroundColor Cyan
Write-Host '    - Shattered Legacy does not need an EA account or a subscription.' -ForegroundColor Cyan
Write-Host ''
$installStart = (Get-Date).AddSeconds(-2)
Start-Process -FilePath $exe -Wait

$found = Find-UoFolder
if ($found.Path) {
    Save-UoPath $found.Path
    Write-Host "  Ultima Online is ready: $($found.Path)" -ForegroundColor Green
    exit 0
}
if ($found.Patch) { Complete-AfterPatch $found.Patch $installStart -AfterInstaller }
if ($found.Reason) { Stop-WithHelp "Ultima Online is installed but cannot be used: $($found.Reason)." }
Stop-WithHelp 'The installer finished, but no Ultima Online folder was found.'
