#Requires -Version 5.1
<#
  Build the Shattered Legacy player zip from what is already on this machine.

      .\player-package\Build-PlayerPackage.ps1
      .\player-package\Build-PlayerPackage.ps1 -TazUO D:\UO\TazUO\client -Out D:\somewhere

  Runs on Chase's machine only. It assembles:
      payload\                 the in-zip scripts and README (this folder, tracked)
      LICENSE-TazUO.txt        TazUO's BSD 2-Clause licence, which it must travel with
      <TazUO>\                 the TazUO client, copied WITHOUT anything personal
      vendor\fiddle-me-this\   Fiddle-Me-This gump art and XML gumps (CC0), see its SOURCE.txt
      vendor\shattered-legacy-art\   our own art (art\<id>.png, into app\tazuo\ExternalImages\art\)
                               and our tiledata and animdata records (records.json, shipped as
                               app\art-records.json; Play.ps1 writes them into copies of the
                               player's own files). See its SOURCE.txt and registry.csv.
  into player-package\dist\ShatteredLegacy-<stamp>.zip, which is gitignored, where <stamp>
  is the build time, yyyy.MM.dd.HHmm local. Beside it goes ShatteredLegacy-<stamp>.version.json,
  which Publish-PlayerPackage.ps1 serves as get.shatteredlegacyuo.com/version.json and which
  installed packages read to offer themselves the update (app\Play.ps1, app\Update.ps1):
      { "version": "<stamp>", "sha256": "<zip, lowercase>", "bytes": <n>, "url": "<zip URL>" }
  plus "notes" when -Notes is given. The zip carries the same stamp in app\package-version.txt.

  Nothing of EA's goes in. The player's own EA install supplies the game data. Gate 4 enforces it
  on the finished zip: no .mul, .uop, .idx or .def file, no file named like any file in the EA
  folder (-UoData, the shard's client-data by default), and nothing under app\uo-overrides\,
  which Play.ps1 makes on a player's computer from their own EA files. A payload folder that holds
  app\uo-overrides\ (someone ran Play.ps1 from payload\) stops the build before anything is staged.

  Two gates, each of which stops the build:
    1. No personal data. TazUO's folder holds Chase's saved logins (settings*.json),
       his profiles, characters and journals (Data\), his own scripts (LegionScripts\)
       and logs. Those are excluded by name, and then every staged file is scanned for
       a non-empty "password" or "username" value in case an exclusion ever misses.
    2. ASCII-only scripts. PowerShell 5.1 reads BOM-less UTF-8 as Windows-1252, and a
       single em dash once became a string delimiter and broke a script here.

  After the zip is written, gate 4 reopens it and checks what actually shipped (the
  Fiddle-Me-This counts and positions, Data\ holding only XmlGumps, no .unblocked, our art
  exactly the registry's IDs, our records, nothing of EA's). A
  failing zip is deleted.

  -GateSelfTest plants a fake saved login in the staging folder and expects gate 1 to
  stop the build. Run it after changing the exclusions.

  -CheckZip <path> runs gate 4 alone against an existing zip and builds nothing. When the
  zip's .version.json is beside it, gate 5 checks that too.

  Gate 4 also checks the updater's side of the zip: app\package-version.txt holds a stamp,
  app\Update.ps1 is there, and every line of app\retired-files.txt is a path the updater may
  delete (no "..", nothing absolute, no wildcard, nothing under app\tazuo\Data\, nothing the
  package itself ships). Gate 5 checks version.json against the zip: same version, and its
  sha256 and bytes are the zip's.

  -Notes "<one line>" puts a line in version.json that launchers print as "What is new".

  ASCII only, for the same reason as gate 2.
#>
[CmdletBinding()]
param(
    [string]$TazUO = 'D:\UO\TazUO\client',
    [string]$Out,
    [switch]$GateSelfTest,
    [string]$CheckZip,
    [string]$Notes,
    [string]$UoData
)

$ErrorActionPreference = 'Stop'

# --- gate 4: what the zip actually holds ----------------------------------------------
# Reads the finished zip, not the staging folder, so it sees exactly what players get.
# Fiddle-Me-This counts are the vendored tree's (vendor\fiddle-me-this\SOURCE.txt); change
# them only together with that file.
$fmtPngCount = 170     # [Original] 107 + [Custom] 63
$fmtXmlCount = 6
$fmtMaxX = 1280; $fmtMaxY = 720
# Our own art (cc-P26). The registry is the count: one PNG per registered ID, exactly.
$slArt      = Join-Path $PSScriptRoot 'vendor\shattered-legacy-art'
$slRegistry = @(Get-Content -LiteralPath (Join-Path $slArt 'registry.csv') | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv)
$slIds      = @($slRegistry | ForEach-Object { [int]$_.id })
# The EA files' names, for gate 4. The shard's own copy of the client data by default.
if (-not $UoData) { $UoData = Join-Path (Split-Path $PSScriptRoot -Parent) 'client-data\classic-client' }
if (-not (Test-Path -LiteralPath (Join-Path $UoData 'tiledata.mul'))) { throw "No EA client data in $UoData (-UoData): gate 4 needs its file names to prove none ships." }
$eaNames = @(Get-ChildItem -LiteralPath $UoData -Recurse -File | ForEach-Object { $_.Name.ToLowerInvariant() })
# The rule for one app\retired-files.txt line. Update.ps1 carries the same function;
# change both together. Returns $null when the line is allowed.
function Test-RetiredLine([string]$p) {
    if ($p.Contains('..')) { return 'contains ..' }
    if ($p.StartsWith('\') -or $p.Contains(':')) { return 'is an absolute path' }
    if ($p -match '[*?\[\]]') { return 'contains a wildcard' }
    $l = $p.ToLowerInvariant().TrimEnd('\')
    if ($l -eq 'app\tazuo\data' -or $l.StartsWith('app\tazuo\data\')) { return 'is under app\tazuo\Data\' }
    if (@('', '.', 'app', 'app\tazuo', 'update-backup', 'app\uo-path.txt', 'app\uo-overrides') -contains $l -or $l -match '^app\\tazuo\\settings[^\\]*\.json$') { return 'is protected' }
    return $null
}
function Read-ZipText($entry) {
    $sr = New-Object IO.StreamReader($entry.Open())
    try { return $sr.ReadToEnd() } finally { $sr.Dispose() }
}
function Test-PackageZip([string]$path) {
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $fail = @()
    $za = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        # Every entry is "<top folder>/<path>"; compare on the path under the top folder.
        $entries = @($za.Entries | Where-Object { $_.FullName -notmatch '/$' })
        $rel = @{}
        foreach ($e in $entries) { $rel[$e.FullName.Substring($e.FullName.IndexOf('/') + 1)] = $e }
        $names = @($rel.Keys)

        $png = @($names | Where-Object { $_ -match '^app/tazuo/ExternalImages/gumps/[^/]+\.png$' })
        if ($png.Count -ne $fmtPngCount) { $fail += "$($png.Count) PNGs under app/tazuo/ExternalImages/gumps/, expected $fmtPngCount" }
        $xml = @($names | Where-Object { $_ -like 'app/tazuo/Data/XmlGumps/*' })
        if ($xml.Count -ne $fmtXmlCount) { $fail += "$($xml.Count) files under app/tazuo/Data/XmlGumps/, expected $fmtXmlCount" }
        if ($names -notcontains 'app/tazuo/LICENSE-FiddleMeThis.txt') { $fail += 'no app/tazuo/LICENSE-FiddleMeThis.txt' }

        foreach ($n in $xml) {
            $sr = New-Object IO.StreamReader($rel[$n].Open())
            try { $t = $sr.ReadToEnd() } finally { $sr.Dispose() }
            $x = $null; $y = $null
            try {
                $root = ([xml]$t).DocumentElement
                $x = [int]$root.GetAttribute('x'); $y = [int]$root.GetAttribute('y')
            } catch { $fail += "$n does not parse: $($_.Exception.Message)"; continue }
            if ($x -lt 0 -or $x -ge $fmtMaxX -or $y -lt 0 -or $y -ge $fmtMaxY) {
                $fail += "$n opens at $x,$y, outside ${fmtMaxX}x$fmtMaxY"
            }
        }

        # Our art: one PNG per registered ID and nothing else, and our records (cc-P26).
        $art = @($names | Where-Object { $_ -like 'app/tazuo/ExternalImages/art/*' })
        if ($art.Count -ne $slIds.Count) { $fail += "$($art.Count) files under app/tazuo/ExternalImages/art/, expected $($slIds.Count) (registry.csv)" }
        foreach ($n in $art) {
            if ($n -notmatch '/(\d+)\.png$' -or $slIds -notcontains [int]$Matches[1]) { $fail += "$n is not a registered art ID" }
        }
        $ar = $rel['app/art-records.json']
        if (-not $ar) { $fail += 'no app/art-records.json' } else {
            try {
                $j = (Read-ZipText $ar) | ConvertFrom-Json
                foreach ($id in @($j.tiledata | ForEach-Object id) + @($j.animdata | ForEach-Object id)) {
                    if ($slIds -notcontains [int]$id) { $fail += "app/art-records.json has a record for $id, which registry.csv does not list" }
                }
            } catch { $fail += "app/art-records.json does not parse: $($_.Exception.Message)" }
        }

        # Nothing of EA's: no EA data file by type or by name, nothing made from one (cc-P26).
        foreach ($n in $names) {
            $leaf = ($n -split '/')[-1].ToLowerInvariant()
            if ($leaf -match '\.(mul|uop|idx|def)$') { $fail += "$n is an EA data file type" }
            # app/VERSION.txt is this build's own stamp, written below; EA has a Version.txt of its own.
            elseif ($eaNames -contains $leaf -and $n -ne 'app/VERSION.txt') { $fail += "$n has the name of an EA file" }
            if ($n -like 'app/uo-overrides/*') { $fail += "$n is made on a player's computer and must never ship" }
        }

        # Data\ holds only XmlGumps\*.xml: never Profiles\, an account or shard folder,
        # settings.db*, backups\ or gump_positions.db.
        $data = @($names | Where-Object { $_ -like 'app/tazuo/Data/*' -and $_ -notmatch '^app/tazuo/Data/XmlGumps/[^/]+\.xml$' })
        if ($data.Count) { $fail += "app/tazuo/Data/ holds more than XmlGumps: $($data -join ', ')" }

        # The updater's side (cc-P5).
        $pv = $rel['app/package-version.txt']
        if (-not $pv) { $fail += 'no app/package-version.txt' }
        elseif ((Read-ZipText $pv).Trim() -notmatch '^\d{4}\.\d{2}\.\d{2}\.\d{4}$') { $fail += "app/package-version.txt is not a yyyy.MM.dd.HHmm stamp: '$((Read-ZipText $pv).Trim())'" }
        if ($names -notcontains 'app/Update.ps1') { $fail += 'no app/Update.ps1' }
        $rf = $rel['app/retired-files.txt']
        if ($rf) {
            $shipped = @($names | ForEach-Object { $_.Replace('/', '\').ToLowerInvariant() })
            foreach ($line in (Read-ZipText $rf) -split "`r?`n") {
                $p = $line.Trim().Replace('/', '\')
                if (-not $p -or $p.StartsWith('#')) { continue }
                $why = Test-RetiredLine $p
                if (-not $why) {
                    $l = $p.ToLowerInvariant().TrimEnd('\')
                    if ($shipped -contains $l -or @($shipped | Where-Object { $_.StartsWith("$l\") }).Count) { $why = 'is shipped in this package' }
                }
                if ($why) { $fail += "app/retired-files.txt line '$p' $why" }
            }
        }

        # Setup.ps1's test-only options (cc-P44): nothing that ships passes them, and Setup.ps1
        # gives them no default, so a player's Setup always searches the real places.
        foreach ($n in @($names | Where-Object { $_ -match '\.(ps1|bat|cmd)$' -and $_ -ne 'app/Setup.ps1' -and $_ -notlike 'app/tazuo/*' })) {
            if ((Read-ZipText $rel[$n]) -match 'Probe(Folder|Installer)') { $fail += "$n passes Setup.ps1's test-only -ProbeFolder/-ProbeInstaller" }
        }
        $setup = $rel['app/Setup.ps1']
        if (-not $setup) { $fail += 'no app/Setup.ps1' }
        elseif ((Read-ZipText $setup) -match '\$Probe(Folder|Installer)\s*=') { $fail += 'app/Setup.ps1 gives a test-only -Probe option a value' }

        if ($names -contains 'app/.unblocked') { $fail += 'app/.unblocked is in the zip' }
        $play = $rel['app/Play.ps1']
        if (-not $play) { $fail += 'no app/Play.ps1' } else {
            $sr = New-Object IO.StreamReader($play.Open())
            try { $t = $sr.ReadToEnd() } finally { $sr.Dispose() }
            if ($t.Contains('.unblocked')) { $fail += 'app/Play.ps1 still references .unblocked' }
        }
    } finally { $za.Dispose() }
    return ,$fail
}

# --- gate 5: version.json describes this zip -------------------------------------------
$zipUrl = 'https://get.shatteredlegacyuo.com/ShatteredLegacy-Setup.zip'
function Test-VersionJson([string]$zipPath, [string]$jsonPath) {
    $fail = @()
    try { $j = [IO.File]::ReadAllText($jsonPath) | ConvertFrom-Json } catch { return ,@("$jsonPath does not parse: $($_.Exception.Message)") }
    $za = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $pv = @($za.Entries | Where-Object { $_.FullName -match '^[^/]+/app/package-version\.txt$' })
        $zipVersion = if ($pv.Count -eq 1) { (Read-ZipText $pv[0]).Trim() } else { $null }
    } finally { $za.Dispose() }
    if ("$($j.version)" -ne $zipVersion) { $fail += "version.json version '$($j.version)', zip app/package-version.txt '$zipVersion'" }
    $h = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLower()
    if ("$($j.sha256)" -cne $h) { $fail += "version.json sha256 $($j.sha256), zip $h" }
    $n = (Get-Item -LiteralPath $zipPath).Length
    if ("$($j.bytes)" -ne "$n") { $fail += "version.json bytes $($j.bytes), zip $n" }
    if ("$($j.url)" -ne $zipUrl) { $fail += "version.json url $($j.url), expected $zipUrl" }
    $raw = [IO.File]::ReadAllBytes($jsonPath)
    if (@($raw | Where-Object { $_ -gt 127 }).Count) { $fail += 'version.json has non-ASCII bytes' }
    return ,$fail
}

if ($CheckZip) {
    $zp = (Resolve-Path $CheckZip).Path
    $fail = Test-PackageZip $zp
    if ($fail.Count) { throw "GATE 4: $CheckZip fails:`n  $($fail -join "`n  ")" }
    Write-Host "  GATE 4 passed: $CheckZip" -ForegroundColor Green
    $jp = $zp -replace '\.zip$', '.version.json'
    if (Test-Path -LiteralPath $jp) {
        $fail = Test-VersionJson $zp $jp
        if ($fail.Count) { throw "GATE 5: $jp fails:`n  $($fail -join "`n  ")" }
        Write-Host "  GATE 5 passed: $jp" -ForegroundColor Green
    }
    exit 0
}

$here    = $PSScriptRoot
$payload = Join-Path $here 'payload'
# Made by Play.ps1 from a computer's own EA files, with that computer's paths in it. Someone ran
# Play.ps1 from payload\: refuse, rather than leave it to gate 4, because it is not ours to ship.
if (Test-Path -LiteralPath (Join-Path $payload 'app\uo-overrides')) { throw "payload\app\uo-overrides\ exists: Play.ps1 was run from payload\. Delete that folder (it holds patched EA files and local paths). Not building." }
if (-not $Out) { $Out = Join-Path $here 'dist' }
# The build stamp is also the package version players' launchers compare, as a
# [version]: keep it yyyy.MM.dd.HHmm so it only ever grows.
$stamp   = Get-Date -Format 'yyyy.MM.dd.HHmm'
$name    = "ShatteredLegacy-$stamp"
$stage   = Join-Path $env:TEMP "sl-player-package\$name"
$zip     = Join-Path $Out "$name.zip"
$vjson   = Join-Path $Out "$name.version.json"
if ($Notes -match '[^\x20-\x7E]') { throw '-Notes must be one line of plain ASCII (launchers print it in a PowerShell 5.1 console).' }

if (-not (Test-Path (Join-Path $TazUO 'TazUO.exe'))) { throw "No TazUO.exe in $TazUO" }

# --- stage --------------------------------------------------------------------
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -Path (Join-Path $payload '*') -Destination $stage -Recurse

$tazOut = Join-Path $stage 'app\tazuo'
New-Item -ItemType Directory -Path $tazOut -Force | Out-Null

# Excluded by name. Top-level only, matched against the first path segment under TazUO.
$excludeDirs  = @('Data', 'LegionScripts', 'logs', 'Cache', 'osx', 'osx-arm', 'ExternalImages')
#   Data           profiles, characters, journals, screenshots, global settings: all personal.
#                  TazUO recreates it on first start. Only Data\XmlGumps ships, from vendor\.
#   ExternalImages whatever gump and art overrides are installed on this machine. TazUO
#                  creates the folders empty; our overrides come from vendor\ only, so a
#                  local experiment never ships by accident.
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

# --- Fiddle-Me-This -----------------------------------------------------------------
# [Original] then [Custom], into the same places under app\tazuo. -LiteralPath throughout:
# "[Original]" and "[Custom]" are wildcard patterns to -Path.
# A file of the same name in both, or already staged, stops the build: a silent overwrite
# would ship whichever copy happened to land last.
$fmt       = Join-Path $here 'vendor\fiddle-me-this'
$fmtGumps  = Join-Path $tazOut 'ExternalImages\gumps'
$fmtXmlOut = Join-Path $tazOut 'Data\XmlGumps'
$fmtOrig   = @(Get-ChildItem -LiteralPath (Join-Path $fmt '[Original]\ExternalImages\gumps') -File)
$fmtCust   = @(Get-ChildItem -LiteralPath (Join-Path $fmt '[Custom]\ExternalImages\gumps') -File)
$fmtXml    = @(Get-ChildItem -LiteralPath (Join-Path $fmt '[Custom]\Data\XmlGumps') -File)
$both = @($fmtOrig | Where-Object { $fmtCust.Name -contains $_.Name } | ForEach-Object Name)
if ($both.Count) { throw "Fiddle-Me-This: in both [Original] and [Custom] gumps: $($both -join ', '). Not building." }
if (Test-Path -LiteralPath $fmtGumps) {
    $pre = @(Get-ChildItem -LiteralPath $fmtGumps -File | Where-Object { ($fmtOrig + $fmtCust).Name -contains $_.Name } | ForEach-Object Name)
    if ($pre.Count) { throw "Fiddle-Me-This: app\tazuo\ExternalImages\gumps already held $($pre.Count) of its files before the copy, first: $($pre[0]). Not building." }
}
New-Item -ItemType Directory -Path $fmtGumps, $fmtXmlOut -Force | Out-Null
$vendored = @()   # source, destination: checked byte for byte in gate 3
foreach ($f in $fmtOrig + $fmtCust) { $vendored += ,@($f.FullName, (Join-Path $fmtGumps $f.Name)) }
foreach ($f in $fmtXml)             { $vendored += ,@($f.FullName, (Join-Path $fmtXmlOut $f.Name)) }
$vendored += ,@((Join-Path $fmt 'LICENSE-FiddleMeThis.txt'), (Join-Path $tazOut 'LICENSE-FiddleMeThis.txt'))

# --- our own art (cc-P26) -----------------------------------------------------------------
# art\<id>.png into ExternalImages\art\, exactly the registered IDs, never over a file already
# there; records.json as app\art-records.json. Checked byte for byte in gate 3 with the rest.
$slArtOut = Join-Path $tazOut 'ExternalImages\art'
$slPngs   = @(Get-ChildItem -LiteralPath (Join-Path $slArt 'art') -File)
$extra    = @($slPngs | Where-Object { $_.Name -notmatch '^(\d+)\.png$' -or $slIds -notcontains [int]$Matches[1] } | ForEach-Object Name)
if ($extra.Count) { throw "vendor\shattered-legacy-art\art holds files registry.csv does not list: $($extra -join ', '). Not building." }
if ($slPngs.Count -ne $slIds.Count) { throw "vendor\shattered-legacy-art\art holds $($slPngs.Count) PNGs, registry.csv lists $($slIds.Count). Not building." }
if (Test-Path -LiteralPath $slArtOut) {
    $pre = @(Get-ChildItem -LiteralPath $slArtOut -File | Where-Object { $slPngs.Name -contains $_.Name } | ForEach-Object Name)
    if ($pre.Count) { throw "app\tazuo\ExternalImages\art already held $($pre.Count) of our art files before the copy, first: $($pre[0]). Not building." }
}
New-Item -ItemType Directory -Path $slArtOut -Force | Out-Null
foreach ($f in $slPngs) { $vendored += ,@($f.FullName, (Join-Path $slArtOut $f.Name)) }
$vendored += ,@((Join-Path $slArt 'records.json'), (Join-Path $stage 'app\art-records.json'))
foreach ($p in $vendored) { Copy-Item -LiteralPath $p[0] -Destination $p[1] }

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
[IO.File]::WriteAllText((Join-Path $stage 'app\package-version.txt'), "$stamp`r`n", (New-Object Text.ASCIIEncoding))

if ($GateSelfTest) {
    Write-Host '  -GateSelfTest: planting a fake saved login; the build MUST stop below.' -ForegroundColor Yellow
    [IO.File]::WriteAllText((Join-Path $tazOut 'leak-canary.json'), '{ "username": "canary", "password": "not-a-real-one" }')
    Write-Host '  -GateSelfTest: planting a fake Profiles\, settings.db and gump_positions.db under Data\ too.' -ForegroundColor Yellow
    New-Item -ItemType Directory -Path (Join-Path $tazOut 'Data\Profiles\canary') -Force | Out-Null
    foreach ($n in 'Profiles\canary\profile.json', 'settings.db', 'gump_positions.db') {
        [IO.File]::WriteAllText((Join-Path $tazOut "Data\$n"), 'canary')
    }
}

# --- gate 1: nothing personal ---------------------------------------------------
$leaks = @()
# app\tazuo\Data\ may hold only the vendored XmlGumps\*.xml: never Profiles\, an account or
# shard folder, settings.db*, backups\ or gump_positions.db.
$dataDir = Join-Path $tazOut 'Data'
if (Test-Path -LiteralPath $dataDir) {
    foreach ($f in Get-ChildItem -LiteralPath $dataDir -Recurse -Force) {
        $r = $f.FullName.Substring($dataDir.Length + 1)
        if ($r -eq 'XmlGumps') { continue }
        if ($r -match '^XmlGumps\\([^\\]+\.xml)$' -and -not $f.PSIsContainer -and $fmtXml.Name -contains $Matches[1]) { continue }
        $leaks += "app\tazuo\Data\$r"
    }
}
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
foreach ($p in $vendored) {
    if ((Get-FileHash -LiteralPath $p[0]).Hash -ne (Get-FileHash -LiteralPath $p[1]).Hash) { $changed += $p[1].Substring($stage.Length + 1) }
}
if ($changed.Count) { throw "GATE 3: $($changed.Count) vendored files (Fiddle-Me-This, our art) differ from vendor\, first: $($changed[0]). Not building." }

# --- zip ------------------------------------------------------------------------
if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out -Force | Out-Null }
if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $vjson) { Remove-Item $vjson -Force }
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

$fail = Test-PackageZip $zip
if ($fail.Count) {
    Remove-Item $zip -Force
    throw "GATE 4: the zip fails, deleted:`n  $($fail -join "`n  ")"
}

$z = Get-Item $zip
$vj = [ordered]@{
    version = $stamp
    sha256  = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower()
    bytes   = $z.Length
    url     = $zipUrl
}
if ($Notes) { $vj.notes = $Notes }
[IO.File]::WriteAllText($vjson, ($vj | ConvertTo-Json), (New-Object Text.ASCIIEncoding))
$fail = Test-VersionJson $zip $vjson
if ($fail.Count) {
    Remove-Item $zip, $vjson -Force
    throw "GATE 5: version.json does not describe the zip, both deleted:`n  $($fail -join "`n  ")"
}

Write-Host ''
Write-Host "  built $($z.FullName)" -ForegroundColor Green
Write-Host "        $vjson" -ForegroundColor Green
Write-Host ("  {0:N0} MB, {1} files, TazUO {2}" -f ($z.Length / 1MB), ($files.Count), $tazVersion) -ForegroundColor Gray
Write-Host '  unsigned: SmartScreen will warn on first run; README.txt tells players what to do.' -ForegroundColor Gray
