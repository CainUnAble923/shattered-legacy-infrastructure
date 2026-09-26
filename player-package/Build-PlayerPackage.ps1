#Requires -Version 5.1
<#
  Build the Shattered Legacy player zip from what is already on this machine.

      .\player-package\Build-PlayerPackage.ps1
      .\player-package\Build-PlayerPackage.ps1 -TazUO D:\UO\TazUO\client -Out D:\somewhere

  Runs on Chase's machine only. It assembles:
      payload\                 the in-zip scripts and README (this folder, tracked)
      LICENSE-TazUO.txt        TazUO's BSD 2-Clause licence, which it must travel with
      <TazUO>\                 the TazUO client, copied WITHOUT anything personal
  into player-package\dist\ShatteredLegacy-<date>.zip, which is gitignored.

  Nothing of EA's goes in. The player's own EA install supplies the game data.

  Two gates, each of which stops the build:
    1. No personal data. TazUO's folder holds Chase's saved logins (settings*.json),
       his profiles, characters and journals (Data\), his own scripts (LegionScripts\)
       and logs. Those are excluded by name, and then every staged file is scanned for
       a non-empty "password" or "username" value in case an exclusion ever misses.
    2. ASCII-only scripts. PowerShell 5.1 reads BOM-less UTF-8 as Windows-1252, and a
       single em dash once became a string delimiter and broke a script here.

  -GateSelfTest plants a fake saved login in the staging folder and expects gate 1 to
  stop the build. Run it after changing the exclusions.

  ASCII only, for the same reason as gate 2.
#>
[CmdletBinding()]
param(
    [string]$TazUO = 'D:\UO\TazUO\client',
    [string]$Out,
    [switch]$GateSelfTest
)

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$payload = Join-Path $here 'payload'
if (-not $Out) { $Out = Join-Path $here 'dist' }
$stamp   = Get-Date -Format 'yyyy-MM-dd'
$name    = "ShatteredLegacy-$stamp"
$stage   = Join-Path $env:TEMP "sl-player-package\$name"
$zip     = Join-Path $Out "$name.zip"

if (-not (Test-Path (Join-Path $TazUO 'TazUO.exe'))) { throw "No TazUO.exe in $TazUO" }

# --- stage --------------------------------------------------------------------
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -Path (Join-Path $payload '*') -Destination $stage -Recurse

$tazOut = Join-Path $stage 'app\tazuo'
New-Item -ItemType Directory -Path $tazOut -Force | Out-Null

# Excluded by name. Top-level only, matched against the first path segment under TazUO.
$excludeDirs  = @('Data', 'LegionScripts', 'logs', 'Cache', 'osx', 'osx-arm')
#   Data           profiles, characters, journals, screenshots, global settings: all personal.
#                  TazUO recreates it on first start.
#   LegionScripts  Chase's own scripts. TazUO writes its API.py there itself.
#   logs, Cache    runtime output.
#   osx, osx-arm   macOS natives; this package is Windows-only.
$excludeFiles = @('settings*.json', '*.pdb')
#   settings*.json  Chase's saved usernames and passwords for every shard he plays.
#   *.pdb           debug symbols.

$srcRoot = (Resolve-Path $TazUO).Path.TrimEnd('\')
$files = Get-ChildItem -LiteralPath $srcRoot -Recurse -File | Where-Object {
    $rel = $_.FullName.Substring($srcRoot.Length + 1)
    $top = $rel.Split('\')[0]
    if ($rel.Contains('\') -and $excludeDirs -contains $top) { return $false }
    foreach ($pat in $excludeFiles) { if ($_.Name -like $pat) { return $false } }
    return $true
}
foreach ($f in $files) {
    $rel  = $f.FullName.Substring($srcRoot.Length + 1)
    $dest = Join-Path $tazOut $rel
    $dir  = Split-Path $dest -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item -LiteralPath $f.FullName -Destination $dest
}
Copy-Item (Join-Path $here 'LICENSE-TazUO.txt') (Join-Path $tazOut 'LICENSE-TazUO.txt')

$tazVersion = if (Test-Path (Join-Path $srcRoot 'v.txt')) { (Get-Content (Join-Path $srcRoot 'v.txt') -TotalCount 1).Trim() } else { 'unknown' }
$commit = (& git -C (Split-Path $here -Parent) rev-parse --short HEAD 2>$null)
$dirty  = (& git -C (Split-Path $here -Parent) status --porcelain -- player-package 2>$null)
if ($dirty) { $commit = "$commit + uncommitted player-package changes" }
$version = @"
Shattered Legacy player package $stamp
TazUO $tazVersion
built from ShatteredLegacy $commit
"@
[IO.File]::WriteAllText((Join-Path $stage 'app\VERSION.txt'), $version, (New-Object Text.UTF8Encoding $false))

if ($GateSelfTest) {
    Write-Host '  -GateSelfTest: planting a fake saved login; the build MUST stop below.' -ForegroundColor Yellow
    [IO.File]::WriteAllText((Join-Path $tazOut 'leak-canary.json'), '{ "username": "canary", "password": "not-a-real-one" }')
}

# --- gate 1: nothing personal ---------------------------------------------------
$leaks = @()
if (Test-Path (Join-Path $tazOut 'Data')) { $leaks += 'app\tazuo\Data' }
# Filter with Where-Object, NEVER with -Include: PowerShell 5.1 ignores -Include beside
# -LiteralPath and returns every file. That once put all 740 client binaries through the
# CRLF rewrite below and shipped a TazUO.exe Windows refused to start (2026-09-26).
function Get-StagedFiles([string[]]$ext) {
    Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $ext -contains $_.Extension.ToLower() }
}
foreach ($f in Get-StagedFiles '.json','.txt','.ini','.xml','.cfg') {
    $t = [IO.File]::ReadAllText($f.FullName)
    if ($t -match '"(password|username)"\s*:\s*"[^"]+"') { $leaks += $f.FullName.Substring($stage.Length + 1) }
}
if ($leaks.Count) {
    throw "GATE 1: personal data would ship. Not building. Found: $($leaks -join ', ')"
}

# --- gate 2: ASCII-only scripts, and CRLF for cmd.exe and Notepad -----------------
$bad = @()
foreach ($f in Get-StagedFiles '.ps1','.bat','.txt') {
    if ($f.DirectoryName.StartsWith($tazOut) -and $f.Extension -ne '.ps1') { continue }
    $b = [IO.File]::ReadAllBytes($f.FullName)
    if (@($b | Where-Object { $_ -gt 127 }).Count) { $bad += $f.FullName.Substring($stage.Length + 1) }
}
if ($bad.Count) { throw "GATE 2: non-ASCII bytes in $($bad -join ', '). Not building." }
# The repo stores LF (.gitattributes). cmd.exe misparses some LF-only batch files, so the
# shipped .bat and .txt files get CRLF here, at the last moment.
foreach ($f in Get-StagedFiles '.bat','.txt' | Where-Object { -not $_.DirectoryName.StartsWith($tazOut) }) {
    $t = [IO.File]::ReadAllText($f.FullName) -replace "`r`n", "`n" -replace "`n", "`r`n"
    [IO.File]::WriteAllText($f.FullName, $t, (New-Object Text.ASCIIEncoding))
}

# --- gate 3: the client ships exactly as it was built ------------------------------
# Every staged TazUO file must match its source byte for byte. Runs last, after every
# step that rewrites files, which is where the -Include accident above happened.
$changed = @()
foreach ($f in $files) {
    $rel = $f.FullName.Substring($srcRoot.Length + 1)
    if ((Get-FileHash -LiteralPath $f.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $tazOut $rel)).Hash) { $changed += $rel }
}
if ($changed.Count) { throw "GATE 3: $($changed.Count) client files differ from $srcRoot, first: $($changed[0]). Not building." }

# --- zip ------------------------------------------------------------------------
if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out -Force | Out-Null }
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
# Entries by hand, not ZipFile.CreateFromDirectory: on Windows PowerShell 5.1 (.NET
# Framework) that writes "\" separators, which the zip spec forbids. Everything goes under
# one top folder, so "Extract All" never sprays 700 files into Downloads.
$fs = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
$za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        $entry = "$name/" + $f.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $f.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $za.Dispose(); $fs.Dispose() }
Remove-Item (Split-Path $stage -Parent) -Recurse -Force

$z = Get-Item $zip
Write-Host ''
Write-Host "  built $($z.FullName)" -ForegroundColor Green
Write-Host ("  {0:N0} MB, {1} files, TazUO {2}" -f ($z.Length / 1MB), ($files.Count), $tazVersion) -ForegroundColor Gray
Write-Host '  unsigned: SmartScreen will warn on first run; README.txt tells players what to do.' -ForegroundColor Gray
