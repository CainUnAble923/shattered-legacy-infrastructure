#Requires -Version 5.1
<#
  The paperdoll body gumps (cc-P81): SFW by default, and a LOCAL-ONLY swap for Chase's own machine.

  The package ships the Siege of Sosaria paperdoll art, SFW only: its six body gumps (12, 13, 14, 15, 665, 666) in
  app\tazuo\ExternalImages\gumps\ and the same six in app\paperdoll-sfw\. Nothing else.

  A machine that holds a local add-on folder may swap those six for an alternate set before each start:
      <add-on folder>\topless\<id>.png   or   <add-on folder>\nude\<id>.png
  The add-on folder is <install>\client-addons\paperdoll-nsfw\ unless paperdoll-bodies.txt names another. It is
  never in the package and never downloaded: someone must put it there by hand. Without it, nothing can be switched
  and the bodies stay SFW.

  The choice lives in <install>\paperdoll-bodies.txt, outside app\, so an update never touches it:
      mode=sfw|topless|nude
      folder=<path to the add-on folder>          (optional)
  Set it with Play.ps1 -PaperdollBodies sfw|topless|nude (and -PaperdollFolder <path>), or edit the file.

  Purely client-side: no server flag, no packet. Other players never see anything different (each client draws its
  own paperdoll art). Switching back to sfw restores the six shipped files from app\paperdoll-sfw\ at the next start.
  The package build refuses any Topless or Nude file in the zip (Build-PlayerPackage.ps1, THE NSFW GATE).

  Dot-sourced by Play.ps1; player-package\tests\Paperdoll-Bodies.Tests.ps1 calls the same functions.
  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as Windows-1252.
#>

$script:PaperdollBodyIds = @(12, 13, 14, 15, 665, 666)

function Get-PaperdollSetting {
    param([string]$Root)
    $s = @{ Mode = 'sfw'; Folder = (Join-Path $Root 'client-addons\paperdoll-nsfw') }
    $file = Join-Path $Root 'paperdoll-bodies.txt'
    if (Test-Path -LiteralPath $file) {
        foreach ($line in Get-Content -LiteralPath $file) {
            if ($line -match '^\s*mode\s*=\s*(sfw|topless|nude)\s*$') { $s.Mode = $Matches[1].ToLowerInvariant() }
            elseif ($line -match '^\s*folder\s*=\s*(.+?)\s*$') { $s.Folder = $Matches[1] }
        }
    }
    return [pscustomobject]$s
}

# True when the add-on folder holds all six bodies for this mode.
function Test-PaperdollAddon {
    param([string]$Folder, [string]$Mode)
    if ($Mode -notin @('topless', 'nude')) { return $false }
    foreach ($id in $script:PaperdollBodyIds) {
        if (-not (Test-Path -LiteralPath (Join-Path $Folder "$Mode\$id.png"))) { return $false }
    }
    return $true
}

# Saves the choice. A non-SFW mode with no add-on to back it is refused, and the setting stays as it was.
function Set-PaperdollSetting {
    param([string]$Root, [string]$Mode, [string]$Folder)
    $current = Get-PaperdollSetting -Root $Root
    if (-not $Folder) { $Folder = $current.Folder }
    if ($Mode -ne 'sfw' -and -not (Test-PaperdollAddon -Folder $Folder -Mode $Mode)) {
        return "No local paperdoll add-on for '$Mode' at $Folder. Nothing switched; the paperdoll stays SFW."
    }
    $lines = @("mode=$Mode")
    if ($Folder -ne (Join-Path $Root 'client-addons\paperdoll-nsfw')) { $lines += "folder=$Folder" }
    Set-Content -LiteralPath (Join-Path $Root 'paperdoll-bodies.txt') -Value $lines -Encoding ASCII
    return "Paperdoll bodies: $Mode."
}

# cc-P111: true when the client in app\tazuo is our TazUO build with the in-game "Paperdoll bodies" option (client patch
# 0002, listed in the build stamp sl-build.txt that client\Build-Client.ps1 writes beside TazUO.exe). A stock TazUO, or
# one of our builds from before that patch, has no stamp line for it. -TazDir names the client's folder instead
# (cc-P114: a client started from its own folder with Play.ps1 -Test -ClientDir).
function Test-ClientPaperdollOption {
    param([string]$App, [string]$TazDir)
    if (-not $TazDir) { $TazDir = Join-Path $App 'tazuo' }
    $stamp = Join-Path $TazDir 'sl-build.txt'
    if (-not (Test-Path -LiteralPath $stamp)) { return $false }
    return [bool](Select-String -LiteralPath $stamp -Pattern '^patch \d+-paperdoll-bodies-option\.patch ' -Quiet)
}

# Before each start: the chosen local set when it is on and present, else the shipped SFW six. Returns a line to show,
# or $null. Never throws for a missing folder: the game always starts.
# cc-P111: with a client that has the in-game option, this stands down: it puts the shipped SFW six back (undoing any
# swap an older launcher made) and leaves the choice to the client, which reads the add-on where it is. A folder named
# in paperdoll-bodies.txt is carried into the settings file being launched (sl_paperdoll_folder), only if that file
# names none, so the client finds the same add-on. The mode is not carried: the client's own choice starts at SFW.
# cc-P114: -TazDir is the client's folder when it is not app\tazuo (Play.ps1 -Test -ClientDir). That client looks for
# the add-on beside its own folder, not in this install, so the install's add-on folder is carried for it even when
# it is the default one, if it is there.
function Sync-PaperdollBodies {
    param([string]$App, [string]$Root, [string]$SettingsPath, [string]$TazDir)
    $ownTaz = Join-Path $App 'tazuo'
    if (-not $TazDir) { $TazDir = $ownTaz }
    $elsewhere = $TazDir.TrimEnd('\') -ne $ownTaz.TrimEnd('\')
    $gumps = Join-Path $TazDir 'ExternalImages\gumps'
    $sfw   = Join-Path $App 'paperdoll-sfw'

    $s = Get-PaperdollSetting -Root $Root
    $inClient = Test-ClientPaperdollOption -TazDir $TazDir
    if ($inClient) {
        $default = Join-Path $Root 'client-addons\paperdoll-nsfw'
        $carry = $s.Folder -ne $default -or ($elsewhere -and (Test-Path -LiteralPath $s.Folder))
        if ($SettingsPath -and $carry -and (Test-Path -LiteralPath $SettingsPath)) {
            $j = [IO.File]::ReadAllText($SettingsPath) | ConvertFrom-Json
            if (-not $j.PSObject.Properties['sl_paperdoll_folder'] -or -not [string]$j.sl_paperdoll_folder) {
                $j | Add-Member -NotePropertyName sl_paperdoll_folder -NotePropertyValue $s.Folder -Force
                [IO.File]::WriteAllText($SettingsPath, ($j | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding $false))
            }
        }
    }
    if (-not (Test-Path -LiteralPath $gumps) -or -not (Test-Path -LiteralPath $sfw)) { return $null }
    $useLocal = -not $inClient -and $s.Mode -ne 'sfw' -and (Test-PaperdollAddon -Folder $s.Folder -Mode $s.Mode)
    foreach ($id in $script:PaperdollBodyIds) {
        $from = if ($useLocal) { Join-Path $s.Folder "$($s.Mode)\$id.png" } else { Join-Path $sfw "$id.png" }
        $to   = Join-Path $gumps "$id.png"
        if (-not (Test-Path -LiteralPath $from)) { continue }
        $same = (Test-Path -LiteralPath $to) -and
                (Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash
        if (-not $same) { Copy-Item -LiteralPath $from -Destination $to -Force }
    }
    if ($inClient) {
        if ($s.Mode -ne 'sfw') { return "  Paperdoll bodies: chosen in the game now (Options, Paperdoll tab); paperdoll-bodies.txt is no longer used." }
        return $null
    }
    if ($useLocal) { return "  Paperdoll bodies: local $($s.Mode) set (this computer only)." }
    if ($s.Mode -ne 'sfw') { return "  Paperdoll bodies: '$($s.Mode)' is set but its add-on is missing; showing SFW." }
    return $null
}
