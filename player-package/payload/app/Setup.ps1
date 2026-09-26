#Requires -Version 5.1
<#
  Shattered Legacy player package: find the Ultima Online client data, or fetch EA's
  free Classic Client installer and hand it to the player.

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
param([string]$UoPath, [switch]$DownloadOnly)

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
    $fv = (Get-Item (Join-Path $dir 'client.exe')).VersionInfo
    return New-Object Version $fv.FileMajorPart, $fv.FileMinorPart, $fv.FileBuildPart, $fv.FilePrivatePart
}

# Returns $null when usable, otherwise the reason it is not.
function Test-UoFolder([string]$dir) {
    if (-not $dir -or -not (Test-Path -LiteralPath $dir -PathType Container)) { return 'folder does not exist' }
    $missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $dir $_)) })
    if ($missing.Count) { return "missing $($missing -join ', ')" }
    $v = Get-UoVersion $dir
    if ($v -lt $minVersion) {
        return "client is version $v, the server needs $minVersion or newer. Open the Ultima Online launcher from the Start menu once and let it finish updating"
    }
    return $null
}

function Find-UoFolder {
    $candidates = New-Object System.Collections.Generic.List[string]
    if (Test-Path $pathFile) { $candidates.Add((Get-Content $pathFile -TotalCount 1).Trim()) }
    foreach ($k in @(
        'HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic',
        'HKLM:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic',
        'HKCU:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic')) {
        $p = Get-ItemProperty -Path $k -Name InstallDir -ErrorAction SilentlyContinue
        if ($p) { $candidates.Add($p.InstallDir) }
    }
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, 'C:\Program Files (x86)', 'C:\Program Files')) {
        if ($base) {
            $candidates.Add((Join-Path $base 'Electronic Arts\Ultima Online Classic'))
            $candidates.Add((Join-Path $base 'EA Games\Ultima Online Classic'))
        }
    }
    $firstReason = $null
    foreach ($c in ($candidates | Select-Object -Unique)) {
        if (-not $c) { continue }
        $why = Test-UoFolder $c
        if (-not $why) { return @{ Path = $c; Reason = $null } }
        if ((Test-Path -LiteralPath $c) -and -not $firstReason) { $firstReason = "$c : $why" }
    }
    return @{ Path = $null; Reason = $firstReason }
}

function Save-UoPath([string]$dir) {
    [IO.File]::WriteAllText($pathFile, $dir, (New-Object Text.UTF8Encoding $false))
}

function Stop-WithHelp([string]$msg) {
    Write-Host ''
    Write-Host "  $msg" -ForegroundColor Red
    Write-Host ''
    Write-Host '  To install Ultima Online by hand:' -ForegroundColor Yellow
    Write-Host "    1. Open $pageUrl in a web browser." -ForegroundColor Yellow
    Write-Host '    2. Download and run the "Classic Client" installer.' -ForegroundColor Yellow
    Write-Host '    3. When the Ultima Online launcher opens, let it finish updating, then close it.' -ForegroundColor Yellow
    Write-Host '    4. Double-click "Play Shattered Legacy" again.' -ForegroundColor Yellow
    Write-Host '  Installed it somewhere unusual? Tell this package where, then play:' -ForegroundColor Yellow
    Write-Host '    app\Setup.bat "D:\Games\Ultima Online Classic"' -ForegroundColor Yellow
    Write-Host ''
    exit 1
}

# --- 1. an explicit folder wins ------------------------------------------------
if ($UoPath) {
    $why = Test-UoFolder $UoPath
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
    if ($found.Reason) {
        # An install exists but is not usable. The usual case: installed, never patched.
        Stop-WithHelp "Found Ultima Online, but it cannot be used yet: $($found.Reason)."
    }

    # --- 3. nothing installed: fetch EA's installer and hand it over -----------
    Write-Host ''
    Write-Host '  Ultima Online is not installed on this computer yet.' -ForegroundColor Cyan
    Write-Host '  It is free, from EA. This will download EA''s installer (about 19 MB) and start it.' -ForegroundColor Cyan
    Write-Host '  After it installs, EA''s launcher downloads the game itself (about 2.5 GB).' -ForegroundColor Cyan
    Write-Host ''
    $answer = Read-Host '  Download and run EA''s installer now? (y/n)'
    if ($answer -notmatch '^[Yy]') { Stop-WithHelp 'Not installed. Nothing was downloaded.' }
}

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

Write-Host ''
Write-Host '  Starting EA''s installer. This part is EA''s, not ours:' -ForegroundColor Cyan
Write-Host '    - Windows may ask for permission. That request comes from EA''s installer.' -ForegroundColor Cyan
Write-Host '    - Accept the defaults. When the Ultima Online launcher opens, let it finish' -ForegroundColor Cyan
Write-Host '      updating completely (this is the 2.5 GB part), then close it.' -ForegroundColor Cyan
Write-Host '    - Shattered Legacy does not need an EA account or a subscription.' -ForegroundColor Cyan
Write-Host ''
Start-Process -FilePath $exe -Wait

$found = Find-UoFolder
if ($found.Path) {
    Save-UoPath $found.Path
    Write-Host "  Ultima Online is ready: $($found.Path)" -ForegroundColor Green
    exit 0
}
if ($found.Reason) { Stop-WithHelp "Ultima Online is installed but not ready: $($found.Reason)." }
Stop-WithHelp 'The installer finished, but no Ultima Online folder was found.'
