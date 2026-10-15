#Requires -Version 5.1
<#
  cc-P124: which TazUO the player package ships, in one place. Dot-sourced by Build-PlayerPackage.ps1 (its -TazUO
  default and its client gate) and by scripts\Shard-Console.ps1 (the Player package tab shows the folder and its
  sl-build.txt before Build runs).

  The package ships OUR build: TazUO upstream at client\TAZUO_COMMIT plus every patch in client\patches\, built by
  client\Build-Client.ps1 into D:\UO\tazuo-sl-build\client, which writes sl-build.txt beside TazUO.exe:

      TazUO upstream <40-hex commit>             (older builds put " built <date>" on the same line)
      built yyyy-MM-dd HH:mm
      patch <name>.patch sha256 <64-hex>         one line per patch, in order

  A client folder is shippable only when that file is there and names exactly the repo's patches with the same
  SHA-256, on the repo's pinned commit. Anything else is a stale (or stock) build, and the build stops with how to
  rebuild it. ASCII only (PowerShell 5.1 reads BOM-less UTF-8 as Windows-1252).
#>

# The one default. The console's Player package tab and Build-PlayerPackage.ps1 both read it from here.
$script:PackageClientDefault = 'D:\UO\tazuo-sl-build\client'

function Get-PackageClientDefault { $script:PackageClientDefault }

function Get-PackageClientRebuildHint {
    param([string]$RepoRoot)
    'Rebuild it: powershell -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $RepoRoot 'client\Build-Client.ps1') +
        '" (it applies client\patches\ to the pinned TazUO and writes sl-build.txt), then build the package again.'
}

function Read-ClientBuildInfo {
    # The client folder's sl-build.txt, parsed. Exists is false when there is no file; Error names a line it could not read.
    param([string]$ClientDir)
    $info = [pscustomobject]@{
        ClientDir = $ClientDir; Path = $null; Exists = $false; Text = $null
        Upstream = $null; Built = $null; Patches = @(); Error = $null
    }
    if (-not $ClientDir) { return $info }
    $info.Path = Join-Path $ClientDir 'sl-build.txt'
    if (-not (Test-Path -LiteralPath $info.Path -PathType Leaf)) { return $info }
    $info.Exists = $true
    $info.Text = [IO.File]::ReadAllText($info.Path)
    $patches = @()
    foreach ($raw in ($info.Text -split "`r?`n")) {
        $line = $raw.Trim()
        if (-not $line) { continue }
        if ($line -match '^TazUO upstream ([0-9a-fA-F]{7,40})(?:\s+built\s+(.+))?$') {
            $info.Upstream = $Matches[1].ToLowerInvariant()
            if ($Matches[2]) { $info.Built = $Matches[2].Trim() }
        } elseif ($line -match '^built\s+(.+)$') {
            $info.Built = $Matches[1].Trim()
        } elseif ($line -match '^patch\s+(\S+)\s+sha256\s+([0-9a-fA-F]{64})$') {
            $patches += [pscustomobject]@{ Name = $Matches[1]; Sha256 = $Matches[2].ToLowerInvariant() }
        } elseif (-not $info.Error) {
            $info.Error = 'sl-build.txt has a line it does not understand: ' + $line
        }
    }
    $info.Patches = $patches
    if (-not $info.Upstream -and -not $info.Error) { $info.Error = 'sl-build.txt names no "TazUO upstream <commit>" line.' }
    $info
}

function Get-RepoClientPatches {
    # The repo's TazUO pin and patch list: client\TAZUO_COMMIT and client\patches\*.patch with their SHA-256.
    param([string]$RepoRoot)
    $pinFile = Join-Path $RepoRoot 'client\TAZUO_COMMIT'
    $pin = $null
    if (Test-Path -LiteralPath $pinFile -PathType Leaf) { $pin = ([IO.File]::ReadAllText($pinFile)).Trim().ToLowerInvariant() }
    $dir = Join-Path $RepoRoot 'client\patches'
    $patches = @()
    if (Test-Path -LiteralPath $dir) {
        foreach ($f in @(Get-ChildItem -LiteralPath $dir -File | Where-Object { $_.Extension -eq '.patch' } | Sort-Object Name)) {
            $patches += [pscustomobject]@{ Name = $f.Name; Sha256 = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
    [pscustomobject]@{ Upstream = $pin; Patches = $patches }
}

function Test-PackageClient {
    # Why this client folder may not ship, as a list of lines (empty: it may). The client gate.
    param([string]$ClientDir, [string]$RepoRoot)
    $fail = @()
    if (-not $ClientDir -or -not (Test-Path -LiteralPath (Join-Path $ClientDir 'TazUO.exe') -PathType Leaf)) {
        return @('No TazUO.exe in ' + $ClientDir + '.')
    }
    $info = Read-ClientBuildInfo $ClientDir
    if (-not $info.Exists) {
        return @($ClientDir + ' has no sl-build.txt, so it is not a Shattered Legacy build (the stock TazUO has none). ' + (Get-PackageClientRebuildHint $RepoRoot))
    }
    if ($info.Error) { return @($info.Error + ' ' + (Get-PackageClientRebuildHint $RepoRoot)) }
    $repo = Get-RepoClientPatches $RepoRoot
    if ($repo.Upstream -and $info.Upstream -and -not $repo.Upstream.StartsWith($info.Upstream)) {
        $fail += ('sl-build.txt says TazUO upstream ' + $info.Upstream + ', client\TAZUO_COMMIT pins ' + $repo.Upstream + '.')
    }
    $have = @($info.Patches | ForEach-Object { $_.Name + ' ' + $_.Sha256 })
    $want = @($repo.Patches | ForEach-Object { $_.Name + ' ' + $_.Sha256 })
    foreach ($w in $repo.Patches) {
        $h = @($info.Patches | Where-Object { $_.Name -eq $w.Name })
        if (-not $h.Count) { $fail += ('the build lacks ' + $w.Name + ', which client\patches\ holds.') }
        elseif ($h[0].Sha256 -ne $w.Sha256) { $fail += ('the build has an older ' + $w.Name + ' (sha256 ' + $h[0].Sha256.Substring(0, 12) + ', the repo''s is ' + $w.Sha256.Substring(0, 12) + ').') }
    }
    foreach ($h in $info.Patches) {
        if (-not @($repo.Patches | Where-Object { $_.Name -eq $h.Name }).Count) { $fail += ('the build has ' + $h.Name + ', which client\patches\ no longer holds.') }
    }
    if ($fail.Count -eq 0 -and (($have -join '|') -ne ($want -join '|'))) { $fail += 'the build applied the patches in a different order from client\patches\.' }
    if ($fail.Count) {
        $fail = @('STALE CLIENT BUILD in ' + $ClientDir + ': ' + ($fail -join ' ') + ' ' + (Get-PackageClientRebuildHint $RepoRoot))
    }
    $fail
}

function Get-ClientBuildVersion {
    # The client version the package reports: "<upstream short commit> + N patches" from sl-build.txt, or $null.
    param([string]$ClientDir)
    $info = Read-ClientBuildInfo $ClientDir
    if (-not $info.Exists -or -not $info.Upstream) { return $null }
    $n = @($info.Patches).Count
    $short = $info.Upstream.Substring(0, [Math]::Min(8, $info.Upstream.Length))
    if ($n -eq 1) { return ($short + ' + 1 patch') }
    $short + ' + ' + $n + ' patches'
}
