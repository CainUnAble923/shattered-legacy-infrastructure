#Requires -Version 5.1
<#
  Brings this install's Legion scripts up to date (cc-P144). Play.ps1 runs it on every start, quietly, after the
  package check; "Update Scripts.bat" runs it on a double-click.

      Update-Scripts.ps1                         this install, and every -ClientDir client it has started
      Update-Scripts.ps1 -Quiet                  one line when something changed, nothing when nothing did
      Update-Scripts.ps1 -ClientDir <folder>     that client too (Play.ps1 passes its own -ClientDir)
      Update-Scripts.ps1 -Offer                  on a dev PC with no scripts repo yet, ask once whether to set it up

  1. scripts.json from the official channel (https://get.shatteredlegacyuo.com/scripts/). Offline, a 404 or a bad
     file never stops anyone playing: one quiet line, and the game starts with the scripts it has.
  2. On a PC flagged dev (LegionScripts.ps1, %LOCALAPPDATA%\ShatteredLegacy\legion-scripts.json), the dev
     channel too, on top: a dev script replaces the official one of the same name. Dev unreachable (Tailscale
     off): official only, one quiet line.
  3. app\tazuo\LegionScripts gets every listed script that is missing or differs, each checked for size and
     SHA-256 before it is put in place. A script the player edited is kept in LegionScripts\backup\ first.
     A script the manifest does not list is never touched or deleted.
  4. Every -ClientDir client this install has started (app\client-dirs.txt, written by Play.ps1) gets the same,
     from the install's copies, by the same rule (Launch-Settings.ps1 Sync-ClientDirData).
  5. TazUO running from this install: the files are still updated, and it says that scripts already running keep
     the old code until they are restarted.

  On a PC with a scripts repo (Setup-ScriptsRepo.ps1 in the shard repo), nothing is downloaded: each client's
  LegionScripts is linked to the clone instead (a link that is missing or broken is repaired), and it says
  "scripts come from your repo: use Pull scripts".

  Exit code: 0, always, unless the arguments themselves are wrong. ASCII only, on purpose.
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string[]]$ClientDir,
    [switch]$Quiet,
    [switch]$Offer,
    [string]$OfficialBase,
    [string]$DevBase,
    [string]$SettingsPath,
    [int]$TimeoutSec = 4
)

$ErrorActionPreference = 'Stop'
$app = $PSScriptRoot
. (Join-Path $app 'Launch-Settings.ps1')
if (-not $Root) { $Root = Split-Path $app -Parent }
if (-not $OfficialBase) { $OfficialBase = $script:LegionOfficialBase }
if (-not $SettingsPath) { $SettingsPath = Get-LegionSettingsPath }
$taz = Join-Path $Root 'app\tazuo'
$installScripts = Join-Path $taz 'LegionScripts'

function Say([string]$m, [string]$c = 'Gray') { Write-Host "  $m" -ForegroundColor $c }
function Loud([string]$m, [string]$c = 'Gray') { if (-not $Quiet) { Say $m $c } }

# Every -ClientDir client this install knows: the one given, and the ones Play.ps1 recorded.
$clients = @()
$known = Join-Path $Root 'app\client-dirs.txt'
if (Test-Path -LiteralPath $known) { $clients += @(Get-Content -LiteralPath $known | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$clients += @($ClientDir | Where-Object { $_ })
$clients = @($clients | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'TazUO.exe') -PathType Leaf } | ForEach-Object { $_.TrimEnd('\') } | Sort-Object -Unique)

$settings = Read-LegionSettings -Path $SettingsPath

# --- a dev PC with a scripts repo: link, never download -------------------------------------------------------------
if (Test-LegionRepo $settings) {
    $fixed = @()
    $targets = @(@{ Folder = $installScripts; Label = (Split-Path $Root -Leaf) }) + @($clients | ForEach-Object { @{ Folder = (Join-Path $_ 'LegionScripts'); Label = (Split-Path (Split-Path $_ -Parent) -Leaf) + '-' + (Split-Path $_ -Leaf) } })
    foreach ($t in $targets) {
        try {
            $r = Set-LegionLink -Folder $t.Folder -Repo $settings.Repo -Label $t.Label
            if ($r.State -ne 'linked') { $fixed += "$($t.Folder): $($r.State) to $($settings.Repo)" }
            foreach ($b in @($r.Brought)) { $fixed += "  brought into the repo (not committed): $b" }
        } catch { Say "Could not link $($t.Folder) to $($settings.Repo): $($_.Exception.Message)" Yellow }
    }
    foreach ($l in $fixed) { Say "Legion scripts: $l" Cyan }
    Loud 'Legion scripts come from your repo: use Pull scripts in the Shard Console.'
    exit 0
}

# --- a dev PC without one: offer the setup, once ---------------------------------------------------------------------
if ($Offer -and $settings.Dev -and -not $settings.DeclinedSetup) {
    $setup = ''
    if ($settings.ShardRepo) { $setup = Join-Path $settings.ShardRepo 'scripts\legion\Setup-ScriptsRepo.ps1' }
    if ($setup -and (Test-Path -LiteralPath $setup -PathType Leaf)) {
        $ans = Read-Host '  This is a dev PC with no scripts repo yet. Set it up now, so every client here uses it? [y/N]'
        if ($ans -match '^\s*(y|yes)\s*$') {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $setup
            exit 0
        }
        $settings.DeclinedSetup = $true
        try { Write-LegionSettings -Settings $settings -Path $SettingsPath } catch {}
        Say 'Not now. Set up scripts repo in the Shard Console does it later; you will not be asked again.'
    }
}

# --- the published sets ----------------------------------------------------------------------------------------------
$off = Get-LegionManifest -Base $OfficialBase -TimeoutSec $TimeoutSec
$dev = $null
if ($settings.Dev) {
    if (-not $DevBase) { $DevBase = $settings.DevUrl }
    $dev = Get-LegionManifest -Base $DevBase -TimeoutSec $TimeoutSec
}
if (-not $off.Manifest -and -not $off.Missing) { Say "Legion scripts not checked: $($off.Error)" DarkGray }
if ($dev -and -not $dev.Manifest) {
    if ($dev.Missing) { Loud 'Dev scripts: nothing published on the dev channel yet.' DarkGray }
    else { Say 'Dev scripts not reachable (is Tailscale running?): official scripts only.' DarkGray }
}
$entries = @(Merge-LegionManifests -Official $off.Manifest -OfficialBase $OfficialBase -Dev $(if ($dev) { $dev.Manifest }) -DevBase $DevBase)
if (-not $entries.Count) {
    if ($off.Missing) { Loud 'Legion scripts: none published yet.' DarkGray }
    exit 0
}

$changed = @()
$failed = @()
if (Test-LegionLink $installScripts) {
    Say 'Legion scripts here come from a repo link: use Pull scripts. Nothing downloaded.' DarkGray
} else {
    $res = Install-LegionSet -Folder $installScripts -Entries $entries -Fetch { param($e, $t) Save-LegionDownload -Entry $e -Path $t }
    $changed += @($res.Lines)
    $failed += @($res.Failed)
}
foreach ($c in $clients) {
    try {
        foreach ($l in @(Sync-ClientDirData -TazDir $taz -ClientDir $c | Where-Object { $_ -like 'LegionScripts\*' })) { $changed += ($l.Substring(14) + ' in ' + $c) }
    } catch { $failed += "$c`: $($_.Exception.Message)" }
}

if ($changed.Count) {
    Say ('Legion scripts: ' + ($changed -join '; ')) Cyan
    $exes = @(Join-Path $taz 'TazUO.exe') + @($clients | ForEach-Object { Join-Path $_ 'TazUO.exe' })
    # A TazUO with no readable path (another user's, or elevated) counts, to be safe, as Update.ps1 counts it.
    $running = @(Get-Process -Name TazUO -ErrorAction SilentlyContinue | Where-Object { $p = $_.Path; -not $p -or @($exes | Where-Object { $_ -ieq $p }).Count })
    if ($running.Count) { Say 'The game is running: scripts already running keep the old code until you stop and start them.' Yellow }
} else {
    Loud 'Legion scripts are up to date.' Green
}
foreach ($f in $failed) { Say "Legion scripts: $f" Yellow }
exit 0
