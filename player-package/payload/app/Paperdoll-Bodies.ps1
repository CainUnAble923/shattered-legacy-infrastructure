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

# Before each start: the chosen local set when it is on and present, else the shipped SFW six. Returns a line to show,
# or $null. Never throws for a missing folder: the game always starts.
function Sync-PaperdollBodies {
    param([string]$App, [string]$Root)
    $gumps = Join-Path $App 'tazuo\ExternalImages\gumps'
    $sfw   = Join-Path $App 'paperdoll-sfw'
    if (-not (Test-Path -LiteralPath $gumps) -or -not (Test-Path -LiteralPath $sfw)) { return $null }

    $s = Get-PaperdollSetting -Root $Root
    $useLocal = $s.Mode -ne 'sfw' -and (Test-PaperdollAddon -Folder $s.Folder -Mode $s.Mode)
    foreach ($id in $script:PaperdollBodyIds) {
        $from = if ($useLocal) { Join-Path $s.Folder "$($s.Mode)\$id.png" } else { Join-Path $sfw "$id.png" }
        $to   = Join-Path $gumps "$id.png"
        if (-not (Test-Path -LiteralPath $from)) { continue }
        $same = (Test-Path -LiteralPath $to) -and
                (Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash
        if (-not $same) { Copy-Item -LiteralPath $from -Destination $to -Force }
    }
    if ($useLocal) { return "  Paperdoll bodies: local $($s.Mode) set (this computer only)." }
    if ($s.Mode -ne 'sfw') { return "  Paperdoll bodies: '$($s.Mode)' is set but its add-on is missing; showing SFW." }
    return $null
}
