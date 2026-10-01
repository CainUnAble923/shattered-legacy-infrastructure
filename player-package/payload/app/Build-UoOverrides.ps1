#Requires -Version 5.1
<#
  Builds patched copies of the player's own tiledata.mul and animdata.mul, so TazUO can animate
  Shattered Legacy's own art (cc-P26, F-8). Play.ps1 runs it before starting the game.

      Build-UoOverrides.ps1 -UoDir <Ultima Online folder>
      Build-UoOverrides.ps1 -UoDir <dir> -Records <json> -OutDir <dir>      (tests)

  Writes the path of files-override.txt to the pipeline, which Play.ps1 hands TazUO as
  -uofilesoverride, or $null when the game should start without one. It never throws: anything
  that goes wrong is one yellow line and $null, because custom art must never stop anyone playing.

  What it does:
    1. Reads our records, app\art-records.json (shipped; ours, nothing of EA's in it).
    2. If app\uo-overrides\stamp.txt matches the player's two files (size and time) and the records
       (SHA-256), and both copies are there at the right size, it is done. That is every start but
       the first after an update or an EA patch.
    3. Otherwise copies the player's tiledata.mul and animdata.mul into memory, checks their sizes,
       and checks every slot a record will use is still free in THEIR files (the test
       scripts\find-free-art.py applies to the art IDs before we pick them: tiledata with no flags and
       no name but "UNUSED", animdata with no frames, and no other animation drawing it as a frame).
       A stone whose slots EA has since used is skipped, and the line says so.
    4. Writes the records into the copies, writes them to app\uo-overrides\, then files-override.txt,
       then the stamp, last.

  The player's EA files are only ever read. app\uo-overrides\ is made here and never shipped; the
  updater leaves it alone and the package build refuses a payload that holds it.

  File layouts (TazUO 26.0909.63, the version we ship):
    tiledata.mul, client 7.0.9.0 and later: 512 land groups of 4 + 32 x 30 bytes (493,568), then
      static groups of 4 + 32 x 41 bytes. A static entry: flags (8), weight (1), layer (1), count (4),
      animId (2), hue (2), light (2), height (1), name (20, zero padded). TileDataLoader.Load.
    animdata.mul: for item i, 68 bytes at i x 68 + 4 x (i / 8 + 1): 64 signed frame offsets from i,
      then unknown (1), frame count (1), frame interval (1), start (1). AnimDataLoader,
      AnimatedStaticsManager.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as Windows-1252.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UoDir,
    [string]$Records,
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
if (-not $Records) { $Records = Join-Path $PSScriptRoot 'art-records.json' }
if (-not $OutDir)  { $OutDir  = Join-Path $PSScriptRoot 'uo-overrides' }

$LandBytes   = 512 * (4 + 32 * 30)
$StaticGroup = 4 + 32 * 41
$AnimEntry   = 68
$StampFormat = 'uo-overrides 1'

function Get-TileOffset([int]$id) { $LandBytes + [Math]::Floor($id / 32) * $StaticGroup + 4 + ($id % 32) * 41 }
function Get-AnimOffset([int]$id) { $id * $AnimEntry + 4 * ([Math]::Floor($id / 8) + 1) }

function Say-Skip([string]$m) { Write-Host "  $m" -ForegroundColor Yellow }

# The stamp: what the copies were built from. Sizes and times of the player's files, and the
# records' hash, so a new package with new records rebuilds, and so does an EA patch.
function Get-Stamp([string]$tile, [string]$anim, [string]$rec) {
    $t = Get-Item -LiteralPath $tile
    $a = Get-Item -LiteralPath $anim
    $h = (Get-FileHash -LiteralPath $rec -Algorithm SHA256).Hash
    return "$StampFormat`ntiledata.mul $($t.Length) $($t.LastWriteTimeUtc.Ticks)`nanimdata.mul $($a.Length) $($a.LastWriteTimeUtc.Ticks)`nrecords $h"
}

function Test-TileFree([byte[]]$d, [int]$id) {
    $o = Get-TileOffset $id
    if ($o + 41 -gt $d.Length) { return 'past the end of tiledata.mul' }
    if ([BitConverter]::ToUInt64($d, $o) -ne 0) { return 'has tiledata flags' }
    $name = [Text.Encoding]::ASCII.GetString($d, $o + 21, 20).Split([char]0)[0].Trim()
    if ($name -and $name -ne 'UNUSED') { return "is named '$name' in tiledata" }
    return $null
}

# Every item ID some animation draws as one of its frames, other than itself. Arithmetic inlined: a
# function call per entry costs seconds over 65,536 entries in 5.1.
function Get-AnimTargets([byte[]]$d) {
    $t = New-Object 'System.Collections.Generic.HashSet[int]'
    $i = 0
    while ($true) {
        $o = $i * 68 + 4 * ([int][Math]::Floor($i / 8) + 1)
        if ($o + 68 -gt $d.Length) { break }
        $c = [int]$d[$o + 65]
        if ($c -gt 64) { $c = 64 }
        for ($k = 0; $k -lt $c; $k++) {
            $v = [int]$d[$o + $k]
            if ($v -gt 127) { $v -= 256 }
            if ($v -ne 0) { [void]$t.Add($i + $v) }
        }
        $i++
    }
    return ,$t
}

function Write-TileRecord([byte[]]$d, $r) {
    $o = Get-TileOffset $r.id
    $flags = [Convert]::ToUInt64(("$($r.flags)" -replace '^0x', ''), 16)
    [Array]::Copy([BitConverter]::GetBytes($flags), 0, $d, $o, 8)
    $d[$o + 8]  = [byte]$r.weight
    $d[$o + 9]  = [byte]$r.layer
    [Array]::Copy([BitConverter]::GetBytes([int]$r.count), 0, $d, $o + 10, 4)
    [Array]::Copy([BitConverter]::GetBytes([uint16]$r.animId), 0, $d, $o + 14, 2)
    [Array]::Copy([BitConverter]::GetBytes([uint16]$r.hue), 0, $d, $o + 16, 2)
    [Array]::Copy([BitConverter]::GetBytes([uint16]$r.light), 0, $d, $o + 18, 2)
    $d[$o + 20] = [byte]$r.height
    $name = New-Object byte[] 20
    $b = [Text.Encoding]::ASCII.GetBytes("$($r.name)")
    [Array]::Copy($b, 0, $name, 0, [Math]::Min($b.Length, 19))
    [Array]::Copy($name, 0, $d, $o + 21, 20)
}

function Write-AnimRecord([byte[]]$d, $r) {
    $o = Get-AnimOffset $r.id
    $frames = @($r.frames)
    for ($k = 0; $k -lt 64; $k++) {
        $v = 0
        if ($k -lt $frames.Count) { $v = [int]$frames[$k] }
        $d[$o + $k] = [byte]($v -band 0xFF)
    }
    $d[$o + 64] = 0
    $d[$o + 65] = [byte]$frames.Count
    $d[$o + 66] = [byte]$r.interval
    $d[$o + 67] = [byte]$r.start
}

try {
    if (-not (Test-Path -LiteralPath $Records)) { return $null }    # a package with no art of ours
    $tileSrc = Join-Path $UoDir 'tiledata.mul'
    $animSrc = Join-Path $UoDir 'animdata.mul'
    foreach ($f in $tileSrc, $animSrc) {
        if (-not (Test-Path -LiteralPath $f)) { throw "$(Split-Path $f -Leaf) is not in $UoDir" }
    }
    $tileOut  = Join-Path $OutDir 'tiledata.mul'
    $animOut  = Join-Path $OutDir 'animdata.mul'
    $listOut  = Join-Path $OutDir 'files-override.txt'
    $stampOut = Join-Path $OutDir 'stamp.txt'
    # TazUO splits each override line on '=' and needs exactly two parts (UOFilesOverrideMap.Load).
    if ($OutDir.Contains('=')) { throw "the game folder's path has an '=' in it, which TazUO cannot read in an override list" }

    $stamp = Get-Stamp $tileSrc $animSrc $Records

    # --- 2. nothing changed: done ----------------------------------------------------------
    if ((Test-Path -LiteralPath $stampOut) -and (Test-Path -LiteralPath $listOut) -and
        (Test-Path -LiteralPath $tileOut) -and (Test-Path -LiteralPath $animOut) -and
        [IO.File]::ReadAllText($stampOut) -ceq $stamp -and
        (Get-Item -LiteralPath $tileOut).Length -eq (Get-Item -LiteralPath $tileSrc).Length -and
        (Get-Item -LiteralPath $animOut).Length -eq (Get-Item -LiteralPath $animSrc).Length) {
        return $listOut
    }

    # --- 3. rebuild ---------------------------------------------------------------------------
    $rec  = [IO.File]::ReadAllText($Records) | ConvertFrom-Json
    $tile = [IO.File]::ReadAllBytes($tileSrc)
    $anim = [IO.File]::ReadAllBytes($animSrc)
    if ($tile.Length -le $LandBytes -or ($tile.Length - $LandBytes) % $StaticGroup -ne 0) {
        throw "tiledata.mul is $($tile.Length) bytes, not the layout of client 7.0.9.0 or later"
    }
    $targets = Get-AnimTargets $anim

    foreach ($a in @($rec.animdata)) {
        $t = @($rec.tiledata | Where-Object { $_.id -eq $a.id })
        $ids = @(@($a.frames) | ForEach-Object { [int]$a.id + [int]$_ } | Select-Object -Unique)
        $why = $null
        foreach ($id in $ids) {
            $why = Test-TileFree $tile $id
            if (-not $why) {
                $o = Get-AnimOffset $id
                if ($o + $AnimEntry -gt $anim.Length) { $why = 'is past the end of animdata.mul' }
                elseif ($anim[$o + 65]) { $why = 'already animates in animdata.mul' }
                elseif ($targets.Contains($id)) { $why = 'is a frame of another animation' }
            }
            if ($why) { $why = "art ID $id $why"; break }
        }
        if ($why) {
            Say-Skip "Shattered Legacy art: skipping item $($a.id): your Ultima Online files now use it ($why)."
            continue
        }
        foreach ($r in $t) { Write-TileRecord $tile $r }
        Write-AnimRecord $anim $a
    }

    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    [IO.File]::WriteAllBytes($tileOut, $tile)
    [IO.File]::WriteAllBytes($animOut, $anim)
    $full = (Resolve-Path -LiteralPath $OutDir).Path
    $list = "# Made by Build-UoOverrides.ps1 from this computer's own Ultima Online files. Never shipped.`r`n" +
            "tiledata.mul=$(Join-Path $full 'tiledata.mul')`r`nanimdata.mul=$(Join-Path $full 'animdata.mul')`r`n"
    # UTF-8 without a BOM: the path may not be ASCII, and TazUO reads the list with a default StreamReader.
    [IO.File]::WriteAllText($listOut, $list, (New-Object Text.UTF8Encoding $false))
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    # With every stone skipped the copies equal the player's files, and the override is harmless.
    return $listOut
} catch {
    Say-Skip "Shattered Legacy art could not be prepared ($($_.Exception.Message)). Starting without it."
    return $null
}
