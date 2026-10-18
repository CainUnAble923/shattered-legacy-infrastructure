#Requires -Version 5.1
<#
  Setup-ScriptsRepo.ps1 (cc-P144): the standard install of the Legion scripts repo on a dev PC (the tower or the
  work PC). The Shard Console's Set up scripts repo runs it. Safe to run again: it repairs a missing or broken link
  and changes nothing else.

      .\Setup-ScriptsRepo.ps1                     asks once where the clone lives (suggests D:\UO\sl-scripts)
      .\Setup-ScriptsRepo.ps1 -Path D:\UO\sl-scripts
      .\Setup-ScriptsRepo.ps1 -DevFlag Off        only turn this PC's dev channel flag off (or On), nothing else

  1. Where the clone lives: asked once, then kept in this PC's legion-scripts.json (never synced or committed),
     with dev on (this PC pulls the dev channel) and where the shard repo is.
  2. Clones github.com/CainUnAble923/shattered-legacy-scripts there, or uses the clone already there.
  3. Links every TazUO client it knows on this PC to the clone with a directory junction: the newest install's
     app\tazuo\LegionScripts (installs under -InstallsRoot, D:\UO\ShatteredLegacy by default) and every -ClientDir
     client (each install's app\client-dirs.txt, the Play.ps1 -ClientDir in the desktop and Start menu shortcuts,
     and -ClientDir here). An existing folder is moved aside to LegionScripts.before-repo-<yyyyMMdd-HHmm> first,
     and each of its scripts the repo lacks is copied into the clone; one that differs goes to
     from-installs\<client>\ in the clone, never over the clone's copy. Both are listed: nothing is committed.
  Then editing a script in TazUO or any editor is editing the repo. Pull scripts, the Commit tab and Publish to dev
  do the rest.

  ASCII only, on purpose.
#>
[CmdletBinding()]
param(
    [string]$Path,
    [string]$RepoUrl,
    [string]$InstallsRoot = 'D:\UO\ShatteredLegacy',
    [string[]]$ClientDir,
    [string[]]$ShortcutDirs,
    [ValidateSet('On', 'Off')][string]$DevFlag,
    [string]$SettingsPath,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LegionDev.ps1')
if (-not $SettingsPath) { $SettingsPath = Get-LegionSettingsPath }
if (-not $RepoUrl) { $RepoUrl = $script:LegionRepoUrl }
if ($null -eq $ShortcutDirs) {
    $ShortcutDirs = @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:PUBLIC 'Desktop'), (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu'))
}
function Say([string]$m, [string]$c = 'Gray') { Write-Host "  $m" -ForegroundColor $c }
function Stop-Here([string]$m) { Write-Host "STOPPED: $m" -ForegroundColor Red; exit 1 }

$settings = Read-LegionSettings -Path $SettingsPath

if ($DevFlag) {
    $settings.Dev = ($DevFlag -eq 'On')
    Write-LegionSettings -Settings $settings -Path $SettingsPath
    Say ("Dev channel on this PC: " + $DevFlag.ToLower() + " (" + $SettingsPath + ").") Green
    exit 0
}

Write-Host ''
Write-Host ("  Set up the Legion scripts repo on " + $env:COMPUTERNAME) -ForegroundColor Cyan

# --- 1. where -------------------------------------------------------------------------------------------------------
if (-not $Path) { $Path = $settings.Repo }
if (-not $Path) {
    $suggest = 'D:\UO\sl-scripts'
    if ($Yes) { $Path = $suggest } else {
        $a = Read-Host "  Where should the scripts repo live on this PC? Enter for $suggest"
        $Path = $(if ($a.Trim()) { $a.Trim().Trim('"') } else { $suggest })
    }
}
$Path = [IO.Path]::GetFullPath($Path).TrimEnd('\')

# --- 2. the clone ---------------------------------------------------------------------------------------------------
$prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
if (Test-Path -LiteralPath (Join-Path $Path '.git')) {
    $origin = [string](& git -C $Path remote get-url origin 2>$null)
    Say ("Using the clone already at $Path (origin $origin).")
} elseif ((Test-Path -LiteralPath $Path) -and @(Get-ChildItem -LiteralPath $Path -Force).Count) {
    $ErrorActionPreference = $prev
    Stop-Here "$Path exists, is not a git clone and is not empty. Pick another folder, or empty that one."
} else {
    Say "Cloning $RepoUrl into $Path..."
    & git clone --quiet $RepoUrl $Path
    if ($LASTEXITCODE -ne 0) {
        $ErrorActionPreference = $prev
        Stop-Here "git clone failed. Is git signed in to GitHub on this PC (gh auth login, or Git Credential Manager), and has Chase added this account to the repo?"
    }
    Say "Cloned." Green
}
$ErrorActionPreference = $prev

$settings.Repo = $Path
$settings.Dev = $true
$settings.ShardRepo = $script:LegionDevRoot
Write-LegionSettings -Settings $settings -Path $SettingsPath
Say ("Saved in $SettingsPath (this PC only): repo $Path, dev channel on.")

# --- 3. the clients -------------------------------------------------------------------------------------------------
$targets = @()
$installs = @()
if (Test-Path -LiteralPath $InstallsRoot) {
    $installs = @(Get-ChildItem -LiteralPath $InstallsRoot -Directory -Filter 'ShatteredLegacy-*' | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'app\tazuo\TazUO.exe') } | Sort-Object Name)
}
if ($installs.Count) {
    $newest = $installs[-1]
    $targets += [pscustomobject]@{ Folder = (Join-Path $newest.FullName 'app\tazuo\LegionScripts'); Label = $newest.Name }
} else { Say "No player package install under $InstallsRoot." Yellow }
$cds = @()
foreach ($i in $installs) {
    $f = Join-Path $i.FullName 'app\client-dirs.txt'
    if (Test-Path -LiteralPath $f) { $cds += @(Get-Content -LiteralPath $f | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
}
$shell = $null
foreach ($d in @($ShortcutDirs | Where-Object { $_ -and (Test-Path -LiteralPath $_) })) {
    foreach ($l in @(Get-ChildItem -LiteralPath $d -Recurse -Filter '*.lnk' -File -ErrorAction SilentlyContinue)) {
        try {
            if (-not $shell) { $shell = New-Object -ComObject WScript.Shell }
            $a = [string]$shell.CreateShortcut($l.FullName).Arguments
            if ($a -match '-ClientDir\s+"([^"]+)"' -or $a -match '-ClientDir\s+(\S+)') { $cds += $Matches[1] }
        } catch {}
    }
}
$cds += @($ClientDir | Where-Object { $_ })
foreach ($c in @($cds | ForEach-Object { $_.TrimEnd('\') } | Sort-Object -Unique)) {
    if (-not (Test-Path -LiteralPath (Join-Path $c 'TazUO.exe') -PathType Leaf)) { Say "Skipped $c (no TazUO.exe there)." DarkGray; continue }
    $targets += [pscustomobject]@{ Folder = (Join-Path $c 'LegionScripts'); Label = ((Split-Path (Split-Path $c -Parent) -Leaf) + '-' + (Split-Path $c -Leaf)) }
}
if (-not $targets.Count) { Say 'No TazUO client found to link. Run this again after installing the player package.' Yellow; exit 0 }

$brought = @()
foreach ($t in $targets) {
    try {
        $r = Set-LegionLink -Folder $t.Folder -Repo $Path -Label $t.Label
        switch ($r.State) {
            'linked'   { Say ("already linked: " + $t.Folder) DarkGray }
            'relinked' { Say ("link repaired:  " + $t.Folder) Green }
            'created'  { Say ("linked (new):   " + $t.Folder) Green }
            'moved'    { Say ("linked:         " + $t.Folder + "   (the old folder is " + (Split-Path $r.Aside -Leaf) + ")") Green }
        }
        $brought += @($r.Brought | ForEach-Object { "$_   (from $($t.Folder))" })
    } catch { Say ("NOT linked: " + $t.Folder + ': ' + $_.Exception.Message) Red }
}
Write-Host ''
if ($brought.Count) {
    Say 'Brought into the repo, NOT committed. Look them over, keep or delete each, then commit (Commit tab):' Yellow
    foreach ($b in $brought) { Say ("  " + $b) Yellow }
    Say 'A script under from-installs\ differed from the repo''s copy; the repo''s copy is unchanged.' Yellow
}
Say ("Done. Every client above now uses " + $Path + ": edit there (or in TazUO), Pull scripts to update, Publish to dev to share.") Green
exit 0
