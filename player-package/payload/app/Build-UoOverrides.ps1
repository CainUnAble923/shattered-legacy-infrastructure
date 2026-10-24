#Requires -Version 5.1
<#
  Builds patched copies of the player's own Ultima Online files, so TazUO shows Shattered Legacy's own
  art and text. Play.ps1 runs it before starting the game.

      Build-UoOverrides.ps1 -UoDir <Ultima Online folder>
      Build-UoOverrides.ps1 -UoDir <dir> -Records <json> -Clilocs <json> -OutDir <dir>      (tests)
      Build-UoOverrides.ps1 -UoDir <dir> -CheckOnly       (cc-P411) only compares the stamps: returns
                                                          { Fresh, Stale, List }, builds nothing
      Build-UoOverrides.ps1 -UoDir <dir> -OutDir <staging> -ListDir <final> -TraceLog <log> -ProgressFile <json>
                            -ReadyFile <file>             (cc-P411) the rebuild Overrides-Launch.ps1 runs in a child:
                                                          into a staging folder whose list already names the final
                                                          folder, a trace line per step, a progress file, and the
                                                          ready file last, so a half-built set is never used

  cc-P411: every step of a rebuild is timed into -TraceLog (one line each: art.read, art.targets, ...,
  map.write, then the total), with a header naming the Ultima Online files, their sizes and their file
  attributes (an Offline or RecallOnDataAccess attribute is a cloud placeholder that downloads on read).
  The last 10 logs are kept. -ProgressFile is rewritten at most four times a second with the step, a count
  for it and the seconds so far, for the progress bar. Test hook (never set on a player's machine):
  SL_OVERRIDES_SLOW=<step>:<seconds> makes that step take that long, still reporting progress.

  Writes the path of files-override.txt to the pipeline, which Play.ps1 hands TazUO as
  -uofilesoverride, or $null when the game should start without one. It never throws: anything
  that goes wrong is one yellow line, and that part is left out, because neither must ever stop
  anyone playing. The two parts are independent: art failing still ships the text, and the other way.

  Art (cc-P26, F-8): tiledata.mul and animdata.mul, so our art animates.
    1. Reads our records, app\art-records.json (shipped; ours, nothing of EA's in it).
    2. If app\uo-overrides\stamp.txt matches the player's two files (size and time) and the records
       (SHA-256), and both copies are there at the right size, it is done. That is every start but
       the first after an update or an EA patch.
    3. Otherwise copies the player's tiledata.mul and animdata.mul into memory, checks their sizes,
       and checks every slot a record will use is still free in THEIR files (the test
       scripts\find-free-art.py applies to the art IDs before we pick them: tiledata with no flags and
       no name but "UNUSED", animdata with no frames, and no other animation drawing it as a frame).
       A stone whose slots EA has since used is skipped, and the line says so.
    4. Writes the records into the copies, writes them to app\uo-overrides\, then the stamp, last.

  Text (cc-P57 Part D): Cliloc.enu, so our own context-menu words show ("Breed", "Smelt Ore", ...).
    1. Reads our entries, app\cliloc-entries.json (shipped; ours: number and text, nothing of EA's).
    2. If app\uo-overrides\cliloc-stamp.txt matches the player's Cliloc.enu (size and time) and the
       entries (SHA-256), and the copy is there, it is done.
    3. Otherwise reads the player's Cliloc.enu (EA ships it compressed; it is decompressed here as
       TazUO does), adds our entries in number order, and writes the table uncompressed to
       app\uo-overrides\Cliloc.enu, then the stamp. A number of ours that the player's file already
       uses is skipped, and the line says so. TazUO reads an uncompressed file as is: it decompresses
       only when the fourth byte is 0x8E (ClilocLoader.ReadCliloc), and an uncompressed table starts
       02 00 00 00. Uncompressed costs no space: EA's file is 5,078,062 bytes compressed and
       5,061,666 bytes uncompressed (cc-P56 Part C).

  Worn-item animations (cc-P62, F-35): the kitsune's fox ears and tail.
    A worn item's tiledata names an Animation number N, and TazUO draws body N's frames over the wearer
    (MobileView.Draw). Ours are in app\sl-anim7.bin and app\sl-anim7-idx.bin (scripts\build-equip-anims.py;
    entirely ours), which the override list hands TazUO as anim7.mul and anim7.idx: EA's files stop at anim6, and
    TazUO opens anim.mul to anim10.mul by name through the list (AnimationsLoader.Load). Bodyconv.def sends body N
    there: a line "N -1 -1 -1 -1 -1 S" means slot S of anim7.mul (AnimationsLoader.ProcessBodyConvDef).
    cc-P139: a creature body (records.json "bodyAnims": body N, slot S, TYPE, flags, as a .vd) goes the same way, with
    a monster or animal slot (below 400) and one more line, "N TYPE FLAGS", in a copy of the player's mobtypes.txt
    (TazUO reads mobtypes.txt by name through the same list, AnimationsLoader.Load), so the client types the body
    as the server does (bodyTable.cfg, ShardBodyTypes.cs) and reads its flags.
    1. Reads records.json's "equipAnims" (anim N, slot S) and "bodyAnims" (body N, slot S, type, flags).
    2. If app\uo-overrides\equip-stamp.txt matches the player's Bodyconv.def and mobtypes.txt (size and time),
       the records and both anim files (SHA-256), and the copy is there, it is done.
    3. Otherwise refuses if the player's folder has an anim7.mul or anim7.idx of its own (EA has begun to use the
       file), and skips, with a yellow line, any N that the player's Bodyconv.def or mobtypes.txt already names
       (mobtypes would send N to the UOP files instead), and any creature body N the player's files already name.
       Then writes the player's Bodyconv.def, unchanged, with our lines added at the end, to
       app\uo-overrides\Bodyconv.def, the player's mobtypes.txt the same way with our creature lines (only when a
       creature body is shipped), then the stamp.

  The map (cc-P188): the Rootdeep Burrows and the way in from Old Haven, as a patch of our changes only.
    app\trammel.mapops (shipped; one line per land cell or static we changed, nothing of EA's) is applied to copies
    of the player's map1LegacyMUL.uop, statics1.mul and staidx1.mul by Apply-MapOps.ps1 beside this script, which
    keeps its own stamp (map-stamp.txt) and says in one yellow line when EA changed one of our blocks since the patch
    was made. About 116 MB more under app\uo-overrides\ (cc-P40 B.2).

  Skill names (cc-P239, F-14): skills.mul and Skills.idx, so Item Identification reads "Appraisal" for everyone.
    1. The renames are $SkillRenames below (ours: an index and a name, nothing of EA's).
    2. If app\uo-overrides\skills-stamp.txt matches the player's two files (size and time) and the renames, and both
       copies are there, it is done.
    3. Otherwise reads the player's two files, rewrites each renamed entry (its action byte kept, our name, a NUL) only
       where the player's file still holds the name we expect, packs every entry again in index order, and writes the
       two copies to app\uo-overrides\, then the stamp. An entry whose name EA has changed is left alone, with a
       yellow line. A folder with neither file (only the tests' made-up folders) is skipped quietly.
    Layout (TazUO SkillsLoader.Load): Skills.idx is 12-byte entries (int32 offset, int32 length, int32 extra; offset
    -1 for none); a skills.mul entry is one action byte, the ASCII name and a NUL, and its length counts all three.

  Music (cc-P368): Music\Digital\Config.txt, so our own tracks (app\music\, client music IDs 105 up) play.
    1. Reads our lines, app\music\shard-music.txt ("<id> <file>.mp3[,loop]", ours; nothing of EA's in it).
    2. If app\uo-overrides\music-stamp.txt matches the player's Config.txt (size and time) and our lines (SHA-256), and
       the copy is there, it is done.
    3. Otherwise writes the player's Config.txt, unchanged, with our lines added at the end, to
       app\uo-overrides\Music\Digital\Config.txt, then the stamp. An ID the player's file already uses is skipped, and
       so is a track whose mp3 is missing, each with a yellow line. The override list then names the copy
       ("music/digital/config.txt=...") and each track ("music/digital/<file>.mp3=<app\music\file.mp3>"): TazUO asks its
       override map for both (Sound.GetMusic, SoundsLoader.Load), so no mp3 is ever written into the player's folder.

  Then files-override.txt lists whichever copies were made. The player's EA files are only ever
  read. app\uo-overrides\ is made here and never shipped; the updater leaves it alone and the
  package build refuses a payload that holds it.

  File layouts (TazUO 26.0909.63, the version we ship):
    tiledata.mul, client 7.0.9.0 and later: 512 land groups of 4 + 32 x 30 bytes (493,568), then
      static groups of 4 + 32 x 41 bytes. A static entry: flags (8), weight (1), layer (1), count (4),
      animId (2), hue (2), light (2), height (1), name (20, zero padded). TileDataLoader.Load.
    animdata.mul: for item i, 68 bytes at i x 68 + 4 x (i / 8 + 1): 64 signed frame offsets from i,
      then unknown (1), frame count (1), frame interval (1), start (1). AnimDataLoader,
      AnimatedStaticsManager.
    Cliloc.enu: int32 2 and int16 1, then per entry int32 number, byte flag, uint16 length, UTF-8
      text. Compressed: a 4-byte header and a move-to-front pass, then TazUO's BwtDecompress
      (ClassicUO.Utility/BwtDecompress.cs, BSD 2-Clause, LICENSE-TazUO.txt), ported below.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as Windows-1252.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UoDir,
    [string]$Records,
    [string]$Clilocs,
    [string]$OutDir,
    [string]$AnimMul,
    [string]$AnimIdx,
    [string]$MapOps,
    [string]$Music,    # cc-P368: the folder holding shard-music.txt and our mp3 files (app\music)
    [string]$ListDir,      # cc-P411: the folder the override list names (the final one when -OutDir is staging)
    [switch]$CheckOnly,    # cc-P411: compare the stamps only
    [string]$TraceLog,
    [string]$ProgressFile,
    [string]$ReadyFile
)

$ErrorActionPreference = 'Stop'
if (-not $Records) { $Records = Join-Path $PSScriptRoot 'art-records.json' }
if (-not $AnimMul) { $AnimMul = Join-Path $PSScriptRoot 'sl-anim7.bin' }
if (-not $AnimIdx) { $AnimIdx = Join-Path $PSScriptRoot 'sl-anim7-idx.bin' }
if (-not $Clilocs) { $Clilocs = Join-Path $PSScriptRoot 'cliloc-entries.json' }
if (-not $OutDir)  { $OutDir  = Join-Path $PSScriptRoot 'uo-overrides' }
if (-not $MapOps)  { $MapOps  = Join-Path $PSScriptRoot 'trammel.mapops' }
if (-not $Music)   { $Music   = Join-Path $PSScriptRoot 'music' }
if (-not $ListDir) { $ListDir = $OutDir }
$ListRoot = [IO.Path]::GetFullPath($ListDir)
$script:Stale = New-Object 'System.Collections.Generic.List[string]'

$LandBytes   = 512 * (4 + 32 * 30)
$StaticGroup = 4 + 32 * 41
$AnimEntry   = 68
$StampFormat = 'uo-overrides 1'
$ClilocStampFormat = 'uo-cliloc 1'
$EquipStampFormat = 'uo-equip 1'
$SkillsStampFormat = 'uo-skills 1'
$MusicStampFormat = 'uo-music 1'
# cc-P239 (F-14): the skills we rename for everyone. Index (SkillName), the name the player's file must hold, ours.
$SkillRenames = @(@{ Index = 3; From = 'Item Identification'; To = 'Appraisal' })

function Get-TileOffset([int]$id) { $LandBytes + [Math]::Floor($id / 32) * $StaticGroup + 4 + ($id % 32) * 41 }
function Get-AnimOffset([int]$id) { $id * $AnimEntry + 4 * ([Math]::Floor($id / 8) + 1) }

function Say-Skip([string]$m) { Write-Host "  $m" -ForegroundColor Yellow; Write-Trace "said: $m" }

# cc-P411: the trace (one line per step with its seconds) and the progress file the launcher's bar reads.
$script:RunClock = [Diagnostics.Stopwatch]::StartNew()
$script:ProgressAt = [long]-1000
$script:StepName = 'starting'
$script:StepClock = $null
$script:Inv = [Globalization.CultureInfo]::InvariantCulture
function Write-Trace([string]$m) {
    if (-not $TraceLog) { return }
    try { [IO.File]::AppendAllText($TraceLog, (Get-Date).ToString('HH:mm:ss.fff', $script:Inv) + '  ' + $m + "`r`n", (New-Object Text.ASCIIEncoding)) } catch {}
}
# The step, a count for it (-1 when it has none) and the seconds so far, at most every 250 ms unless -Force.
function Write-StepProgress([long]$done = -1, [long]$total = -1, [switch]$Force) {
    if (-not $ProgressFile) { return }
    $ms = $script:RunClock.ElapsedMilliseconds
    if (-not $Force -and $ms - $script:ProgressAt -lt 250) { return }
    $script:ProgressAt = $ms
    $j = '{"step":"' + $script:StepName + '","done":' + $done + ',"total":' + $total + ',"seconds":' + ($ms / 1000).ToString('0.0', $script:Inv) + '}'
    try { [IO.File]::WriteAllText($ProgressFile, $j, (New-Object Text.ASCIIEncoding)) } catch {}
}
function Stop-Step {
    if ($script:StepClock) { Write-Trace (('{0,-20} {1,8} s' -f $script:StepName, $script:StepClock.Elapsed.TotalSeconds.ToString('0.000', $script:Inv))); $script:StepClock = $null }
}
function Start-Step([string]$name) {
    Stop-Step
    $script:StepName = $name
    $script:StepClock = [Diagnostics.Stopwatch]::StartNew()
    Write-StepProgress -Force
    # The test hook: this step takes the given seconds, reporting progress meanwhile.
    $h = [string]$env:SL_OVERRIDES_SLOW
    if ($h -and $h -match '^([a-z.-]+):(\d+)$' -and $Matches[1] -eq $name) {
        $sec = [int]$Matches[2]
        $w = [Diagnostics.Stopwatch]::StartNew()
        while ($w.Elapsed.TotalSeconds -lt $sec) { Start-Sleep -Milliseconds 100; Write-StepProgress ([long]$w.Elapsed.TotalSeconds) $sec }
    }
}
# The last 10 trace logs in that folder are kept.
function Limit-TraceLogs {
    if (-not $TraceLog) { return }
    try {
        $d = Split-Path -Parent $TraceLog
        @(Get-ChildItem -LiteralPath $d -Filter 'overrides-*.log' -File | Sort-Object LastWriteTimeUtc, Name -Descending | Select-Object -Skip 10) | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue }
    } catch {}
}
function Write-TraceHeader {
    if (-not $TraceLog) { return }
    try {
        $d = Split-Path -Parent $TraceLog
        if ($d -and -not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    } catch {}
    Write-Trace ('Build-UoOverrides.ps1 ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss', $script:Inv) + ' on ' + $env:COMPUTERNAME + ', PowerShell ' + $PSVersionTable.PSVersion + ', ' + [Environment]::OSVersion.VersionString)
    Write-Trace ('UO files ' + $UoDir)
    Write-Trace ('out ' + [IO.Path]::GetFullPath($OutDir) + '; list names ' + $ListRoot)
    foreach ($n in 'tiledata.mul', 'animdata.mul', 'Cliloc.enu', 'Bodyconv.def', 'skills.mul', 'map1LegacyMUL.uop', 'statics1.mul', 'staidx1.mul') {
        $f = Join-Path $UoDir $n
        try { $i = Get-Item -LiteralPath $f -ErrorAction Stop; Write-Trace ("  {0} {1} bytes, {2}" -f $n, $i.Length, $i.Attributes) } catch { Write-Trace "  $n not there" }
    }
    Limit-TraceLogs
}

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
    $n = [long]($d.Length / 68.5)
    while ($true) {
        $o = $i * 68 + 4 * ([int][Math]::Floor($i / 8) + 1)
        if ($o + 68 -gt $d.Length) { break }
        if (($i -band 4095) -eq 0) { Write-StepProgress $i $n }
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

# Returns the art's override lines ("name=path"), or none.
function Build-Art {
    if (-not (Test-Path -LiteralPath $Records)) { return }    # a package with no art of ours
    $tileSrc = Join-Path $UoDir 'tiledata.mul'
    $animSrc = Join-Path $UoDir 'animdata.mul'
    foreach ($f in $tileSrc, $animSrc) {
        if (-not (Test-Path -LiteralPath $f)) { throw "$(Split-Path $f -Leaf) is not in $UoDir" }
    }
    $tileOut  = Join-Path $OutDir 'tiledata.mul'
    $animOut  = Join-Path $OutDir 'animdata.mul'
    $stampOut = Join-Path $OutDir 'stamp.txt'
    $lines = { "tiledata.mul=$(Join-Path $ListRoot 'tiledata.mul')"; "animdata.mul=$(Join-Path $ListRoot 'animdata.mul')" }

    $stamp = Get-Stamp $tileSrc $animSrc $Records

    # --- nothing changed: done ---------------------------------------------------------------
    if ((Test-Path -LiteralPath $stampOut) -and
        (Test-Path -LiteralPath $tileOut) -and (Test-Path -LiteralPath $animOut) -and
        [IO.File]::ReadAllText($stampOut) -ceq $stamp -and
        (Get-Item -LiteralPath $tileOut).Length -eq (Get-Item -LiteralPath $tileSrc).Length -and
        (Get-Item -LiteralPath $animOut).Length -eq (Get-Item -LiteralPath $animSrc).Length) {
        & $lines
        return
    }
    if ($CheckOnly) { $script:Stale.Add('art'); return }

    # --- rebuild ------------------------------------------------------------------------------
    Start-Step 'art.read'
    $rec  = [IO.File]::ReadAllText($Records) | ConvertFrom-Json
    $tile = [IO.File]::ReadAllBytes($tileSrc)
    $anim = [IO.File]::ReadAllBytes($animSrc)
    if ($tile.Length -le $LandBytes -or ($tile.Length - $LandBytes) % $StaticGroup -ne 0) {
        throw "tiledata.mul is $($tile.Length) bytes, not the layout of client 7.0.9.0 or later"
    }
    Start-Step 'art.targets'
    $targets = Get-AnimTargets $anim

    Start-Step 'art.anim-records'
    $animRecs = @($rec.animdata); $k = 0
    foreach ($a in $animRecs) {
        Write-StepProgress (++$k) $animRecs.Count
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

    # Tiledata records with no animation of their own (cc-P62: a worn item's icon, which names its Animation
    # number). Same tests on the one slot: tiledata free, and not drawn as another animation's frame.
    Start-Step 'art.tile-records'
    $animated = @(@($rec.animdata) | ForEach-Object { [int]$_.id })
    $tileRecs = @($rec.tiledata | Where-Object { $animated -notcontains [int]$_.id }); $k = 0
    foreach ($r in $tileRecs) {
        Write-StepProgress (++$k) $tileRecs.Count
        $why = Test-TileFree $tile ([int]$r.id)
        if (-not $why -and $targets.Contains([int]$r.id)) { $why = 'is a frame of another animation' }
        if ($why) {
            Say-Skip "Shattered Legacy art: skipping item $($r.id): your Ultima Online files now use it (art ID $($r.id) $why)."
            continue
        }
        Write-TileRecord $tile $r
    }

    Start-Step 'art.write'
    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    [IO.File]::WriteAllBytes($tileOut, $tile)
    [IO.File]::WriteAllBytes($animOut, $anim)
    Start-Step 'art.stamp'
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    Stop-Step
    # With every stone skipped the copies equal the player's files, and the override is harmless.
    & $lines
}

# The first number of every line a .def or mobtypes.txt file has, read as TazUO reads them: trimmed lines that
# start with a digit (DefReader.Parse; AnimationsLoader.Load for mobtypes.txt).
function Get-DefIndexes([string]$path) {
    $h = New-Object 'System.Collections.Generic.HashSet[int]'
    if (Test-Path -LiteralPath $path) {
        foreach ($l in [IO.File]::ReadAllLines($path, [Text.Encoding]::GetEncoding(28591))) {
            $t = $l.Trim()
            if ($t.Length -and [char]::IsDigit($t[0]) -and $t -match '^(\d+)') { [void]$h.Add([int]$Matches[1]) }
        }
    }
    return ,$h
}

function Get-FileStamp([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return 'none' }
    $i = Get-Item -LiteralPath $path
    return "$($i.Length) $($i.LastWriteTimeUtc.Ticks)"
}

# Returns the override lines ("name=path") of our worn-item animations (cc-P62) and creature bodies (cc-P139), or none.
function Build-EquipAnim {
    if (-not (Test-Path -LiteralPath $Records)) { return }
    $recJson = [IO.File]::ReadAllText($Records) | ConvertFrom-Json
    $eq = @($recJson.equipAnims | Where-Object { $_ })
    $bodies = @($recJson.bodyAnims | Where-Object { $_ })
    if ($eq.Count -eq 0 -and $bodies.Count -eq 0) { return }
    foreach ($f in $AnimMul, $AnimIdx) {
        if (-not (Test-Path -LiteralPath $f)) { throw "$(Split-Path $f -Leaf) is missing from the game folder" }
        if ($f.Contains('=')) { throw "the game folder's path has an '=' in it, which TazUO cannot read in an override list" }
    }
    foreach ($n in 'anim7.mul', 'anim7.idx') {
        if (Test-Path -LiteralPath (Join-Path $UoDir $n)) { throw "your Ultima Online folder now has its own $n" }
    }
    $bcSrc = Join-Path $UoDir 'Bodyconv.def'
    $mtSrc = Join-Path $UoDir 'mobtypes.txt'
    if (-not (Test-Path -LiteralPath $bcSrc)) { throw "Bodyconv.def is not in $UoDir" }
    $bcOut    = Join-Path $OutDir 'Bodyconv.def'
    $mtOut    = Join-Path $OutDir 'mobtypes.txt'
    $stampOut = Join-Path $OutDir 'equip-stamp.txt'
    $lines = {
        "bodyconv.def=$(Join-Path $ListRoot 'Bodyconv.def')"; "anim7.mul=$AnimMul"; "anim7.idx=$AnimIdx"
        if (Test-Path -LiteralPath $mtOut) { "mobtypes.txt=$(Join-Path $ListRoot 'mobtypes.txt')" }
    }
    $hash = { param($p) (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
    $stamp = "$EquipStampFormat`nBodyconv.def $(Get-FileStamp $bcSrc)`nmobtypes.txt $(Get-FileStamp $mtSrc)`nrecords $(& $hash $Records)`nanim $(& $hash $AnimMul) $(& $hash $AnimIdx)"

    if ((Test-Path -LiteralPath $stampOut) -and (Test-Path -LiteralPath $bcOut) -and
        [IO.File]::ReadAllText($stampOut) -ceq $stamp) {
        & $lines
        return
    }
    if ($CheckOnly) { $script:Stale.Add('equip'); return }

    Start-Step 'equip'
    $used = Get-DefIndexes $bcSrc
    $mob  = Get-DefIndexes $mtSrc
    $idxLength = (Get-Item -LiteralPath $AnimIdx).Length
    $add = @()
    foreach ($e in $eq) {
        $n = [int]$e.anim; $s = [int]$e.slot
        $why = $null
        if ($s -lt 400 -or $idxLength -lt 12 * (35000 + ($s - 399) * 175)) { $why = "its slot $s is not in sl-anim7-idx.bin" }
        elseif ($used.Contains($n)) { $why = 'Bodyconv.def already names it' }
        elseif ($mob.Contains($n)) { $why = 'mobtypes.txt already names it' }
        if ($why) {
            Say-Skip "Shattered Legacy art: skipping worn animation ${n}: $why."
            continue
        }
        $add += "$n`t-1`t-1`t-1`t-1`t-1`t$s`t# $($e.name)"
    }
    # cc-P139: creature bodies. The slot's layout is TazUO's for anim7 (CalculateTypeByGraphic, file index 6): below
    # 200 monster (110 entries at slot x 110), below 400 animal (65 at 22000 + (slot - 200) x 65), else people.
    $mobAdd = @()
    foreach ($b in $bodies) {
        $n = [int]$b.body; $s = [int]$b.slot
        $why = $null
        $need = if ($s -lt 1 -or $s -ge 2048) { [long]::MaxValue }
                elseif ($s -lt 200) { 12 * (($s + 1) * 110) }
                elseif ($s -lt 400) { 12 * (22000 + ($s - 199) * 65) }
                else { 12 * (35000 + ($s - 399) * 175) }
        if ($idxLength -lt $need) { $why = "its slot $s is not in sl-anim7-idx.bin" }
        elseif ($used.Contains($n)) { $why = 'Bodyconv.def already names it' }
        elseif ($mob.Contains($n)) { $why = 'mobtypes.txt already names it' }
        if ($why) {
            Say-Skip "Shattered Legacy art: skipping creature body ${n}: $why."
            continue
        }
        $add += "$n`t-1`t-1`t-1`t-1`t-1`t$s`t# $($b.name)"
        $mobAdd += "$n`t$(([string]$b.type).ToUpperInvariant())`t$('{0:X}' -f [int]$b.flags)`t# $($b.name)"
    }

    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    if (Test-Path -LiteralPath $mtOut) { Remove-Item -LiteralPath $mtOut -Force }
    if ($add.Count -eq 0) {
        if (Test-Path -LiteralPath $bcOut) { Remove-Item -LiteralPath $bcOut -Force }
        return
    }
    # The player's file byte for byte, then ours. A line break first if their last line has none.
    [IO.File]::WriteAllBytes($bcOut, (Add-OurLines $bcSrc "# Shattered Legacy's animations (app\Build-UoOverrides.ps1): body N is slot S of anim7.mul." $add))
    if ($mobAdd.Count) {
        # The player's mobtypes.txt the same way (an empty file when they have none; TazUO reads ours either way).
        [IO.File]::WriteAllBytes($mtOut, (Add-OurLines $mtSrc "# Shattered Legacy's creature bodies (app\Build-UoOverrides.ps1): body TYPE flags (hex)." $mobAdd))
    }
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    Stop-Step
    & $lines
}

# The player's file byte for byte (none: nothing), a line break if its last line has none, our comment, our lines.
function Add-OurLines([string]$srcPath, [string]$comment, [string[]]$ours) {
    $src = if (Test-Path -LiteralPath $srcPath) { [IO.File]::ReadAllBytes($srcPath) } else { New-Object byte[] 0 }
    $tail = ''
    if ($src.Length -and $src[$src.Length - 1] -ne 10) { $tail = "`r`n" }
    $tail += "$comment`r`n"
    $tail += (($ours | ForEach-Object { "$_`r`n" }) -join '')
    $b = [Text.Encoding]::ASCII.GetBytes($tail)
    $all = New-Object byte[] ($src.Length + $b.Length)
    [Array]::Copy($src, 0, $all, 0, $src.Length)
    [Array]::Copy($b, 0, $all, $src.Length, $b.Length)
    return ,$all
}

# The cliloc reader and writer, compiled once per PowerShell session (only when a copy is rebuilt).
# C# 5, for the compiler Windows PowerShell 5.1 has. Decompress is TazUO's BwtDecompress
# (ClassicUO.Utility/BwtDecompress.cs at 3212623f, BSD 2-Clause): its 65,536-entry table, built and
# sorted, is the numbers 0 to 65535 in order, so it is filled that way here.
$ClilocSource = @'
using System;
using System.Collections.Generic;
using System.Text;

namespace ShatteredLegacy
{
    public static class Cliloc
    {
        public static bool IsCompressed(byte[] file) { return file.Length > 3 && file[3] == 0x8E; }

        // The uncompressed table for a file as the client reads it: compressed when the fourth byte is 0x8E.
        public static byte[] Table(byte[] file)
        {
            byte[] t = IsCompressed(file) ? Decompress(file) : file;
            if (t.Length < 6 || BitConverter.ToInt32(t, 0) != 2 || BitConverter.ToInt16(t, 4) != 1)
                throw new InvalidOperationException("Cliloc.enu does not start with the cliloc header (2, 1)");
            return t;
        }

        public static List<KeyValuePair<int, string>> Read(byte[] table)
        {
            List<KeyValuePair<int, string>> r = new List<KeyValuePair<int, string>>();
            int o = 6;
            while (o < table.Length)
            {
                if (o + 7 > table.Length) throw new InvalidOperationException("Cliloc.enu ends inside an entry at " + o);
                int n = BitConverter.ToInt32(table, o);
                int len = BitConverter.ToUInt16(table, o + 5);
                if (o + 7 + len > table.Length) throw new InvalidOperationException("Cliloc.enu entry " + n + " runs past the end");
                r.Add(new KeyValuePair<int, string>(n, Encoding.UTF8.GetString(table, o + 7, len)));
                o += 7 + len;
            }
            return r;
        }

        // The table with our entries added, each before the first entry with a higher number (EA's are in
        // number order). A number the table already holds is left as it is and named in skipped.
        public static byte[] Add(byte[] table, int[] numbers, string[] texts, List<string> skipped)
        {
            List<int> starts = new List<int>();
            List<int> nums = new List<int>();
            int o = 6;
            while (o < table.Length)
            {
                starts.Add(o);
                nums.Add(BitConverter.ToInt32(table, o));
                o += 7 + BitConverter.ToUInt16(table, o + 5);
            }
            HashSet<int> have = new HashSet<int>(nums);

            List<int> order = new List<int>();
            for (int i = 0; i < numbers.Length; i++) order.Add(i);
            order.Sort(delegate(int a, int b) { return numbers[a].CompareTo(numbers[b]); });

            List<byte> output = new List<byte>(table.Length + 64 * numbers.Length);
            int copied = 0;
            int at = 0;
            foreach (int i in order)
            {
                if (have.Contains(numbers[i])) { skipped.Add(numbers[i].ToString()); continue; }
                while (at < nums.Count && nums[at] < numbers[i]) at++;
                int cut = at < starts.Count ? starts[at] : table.Length;
                for (int k = copied; k < cut; k++) output.Add(table[k]);
                copied = cut;
                byte[] text = Encoding.UTF8.GetBytes(texts[i]);
                output.AddRange(BitConverter.GetBytes(numbers[i]));
                output.Add(0);
                output.AddRange(BitConverter.GetBytes((ushort)text.Length));
                output.AddRange(text);
                have.Add(numbers[i]);
            }
            for (int k = copied; k < table.Length; k++) output.Add(table[k]);
            return output.ToArray();
        }

        public static byte[] Decompress(byte[] buffer)
        {
            int pos = 4;
            byte firstChar = buffer[pos++];
            ushort[] table = new ushort[256];
            for (int k = 0; k < 256; k++) table[k] = (ushort)k;

            byte[] list = new byte[buffer.Length - 4];
            int i = 0;
            while (pos < buffer.Length)
            {
                byte current = firstChar;
                ushort value = table[current];
                if (current > 0)
                {
                    do { table[current] = table[current - 1]; } while (--current > 0);
                }
                table[0] = value;
                list[i++] = (byte)value;
                firstChar = buffer[pos++];
            }
            return InternalDecompress(list);
        }

        private static byte[] InternalDecompress(byte[] input)
        {
            char[] symbolTable = new char[256];
            char[] frequency = new char[256];
            int[] partial = new int[256 * 3];
            for (int k = 0; k < 256; k++) symbolTable[k] = (char)k;
            for (int k = 0; k < 256; k++) partial[k] = BitConverter.ToInt32(input, k * 4);

            int len = 0;
            for (int k = 0; k < 256; k++) len += partial[k];
            byte[] output = new byte[len];

            int nonZero = 0;
            for (int k = 0; k < 256; k++) if (partial[k] != 0) nonZero++;

            Frequency(partial, frequency);

            for (int k = 0, m = 0; k < nonZero; ++k)
            {
                byte freq = (byte)frequency[k];
                symbolTable[input[m + 1024]] = (char)freq;
                partial[freq + 256] = m + 1;
                m += partial[freq];
                partial[freq + 512] = m;
            }

            byte val = (byte)symbolTable[0];
            int count = 0;
            while (count < len)
            {
                output[count] = val;
                if (partial[val + 256] >= partial[val + 512])
                {
                    if (nonZero-- > 0)
                    {
                        ShiftLeft(symbolTable, nonZero);
                        val = (byte)symbolTable[0];
                    }
                }
                else
                {
                    char idx = (char)input[partial[val + 256] + 1024];
                    partial[val + 256]++;
                    if (idx != 0)
                    {
                        ShiftLeft(symbolTable, idx);
                        symbolTable[(byte)idx] = (char)val;
                        val = (byte)symbolTable[0];
                    }
                }
                count++;
            }
            return output;
        }

        private static void Frequency(int[] input, char[] output)
        {
            int[] tmp = new int[256];
            Array.Copy(input, tmp, 256);
            for (int i = 0; i < 256; i++)
            {
                uint value = 0;
                byte index = 0;
                for (int j = 0; j < 256; j++)
                {
                    if (tmp[j] > value) { index = (byte)j; value = (uint)tmp[j]; }
                }
                if (value == 0) break;
                output[i] = (char)index;
                tmp[index] = 0;
            }
        }

        private static void ShiftLeft(char[] input, int max)
        {
            for (int i = 0; i < max; ++i) input[i] = input[i + 1];
        }
    }
}
'@

function Use-ClilocType {
    if (-not ('ShatteredLegacy.Cliloc' -as [type])) { Add-Type -TypeDefinition $ClilocSource -Language CSharp }
}

# Returns the text's override line ("cliloc.enu=path"), or none.
function Build-Cliloc {
    if (-not (Test-Path -LiteralPath $Clilocs)) { return }    # a package with no text of ours
    $src = Join-Path $UoDir 'Cliloc.enu'
    if (-not (Test-Path -LiteralPath $src)) { throw "Cliloc.enu is not in $UoDir" }
    $out      = Join-Path $OutDir 'Cliloc.enu'
    $stampOut = Join-Path $OutDir 'cliloc-stamp.txt'
    $s = Get-Item -LiteralPath $src
    $stamp = "$ClilocStampFormat`nCliloc.enu $($s.Length) $($s.LastWriteTimeUtc.Ticks)`nentries $((Get-FileHash -LiteralPath $Clilocs -Algorithm SHA256).Hash)"

    if ((Test-Path -LiteralPath $stampOut) -and (Test-Path -LiteralPath $out) -and
        [IO.File]::ReadAllText($stampOut) -ceq $stamp) {
        "cliloc.enu=$(Join-Path $ListRoot 'Cliloc.enu')"
        return
    }
    if ($CheckOnly) { $script:Stale.Add('cliloc'); return }

    $ours = @(([IO.File]::ReadAllText($Clilocs) | ConvertFrom-Json).entries)
    if ($ours.Count -eq 0) { return }
    Start-Step 'cliloc.compile'
    Use-ClilocType
    Start-Step 'cliloc.read'
    $table = [ShatteredLegacy.Cliloc]::Table([IO.File]::ReadAllBytes($src))
    Start-Step 'cliloc.merge'
    $skipped = New-Object 'System.Collections.Generic.List[string]'
    $numbers = [int[]]@($ours | ForEach-Object { [int]$_.number })
    $texts   = [string[]]@($ours | ForEach-Object { "$($_.text)" })
    $merged  = [ShatteredLegacy.Cliloc]::Add($table, $numbers, $texts, $skipped)
    foreach ($n in $skipped) {
        Say-Skip "Shattered Legacy text: skipping entry ${n}: your Ultima Online files now use that number."
    }

    Start-Step 'cliloc.write'
    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    [IO.File]::WriteAllBytes($out, $merged)
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    Stop-Step
    "cliloc.enu=$(Join-Path $ListRoot 'Cliloc.enu')"
}

# Returns the skill names' two override lines ("skills.mul=path", "skills.idx=path"), or none (cc-P239).
function Build-Skills {
    $srcMul = Join-Path $UoDir 'skills.mul'
    $srcIdx = Join-Path $UoDir 'Skills.idx'
    if (-not (Test-Path -LiteralPath $srcMul) -and -not (Test-Path -LiteralPath $srcIdx)) { return }
    if (-not (Test-Path -LiteralPath $srcMul)) { throw "skills.mul is not in $UoDir" }
    if (-not (Test-Path -LiteralPath $srcIdx)) { throw "Skills.idx is not in $UoDir" }
    $outMul   = Join-Path $OutDir 'skills.mul'
    $outIdx   = Join-Path $OutDir 'Skills.idx'
    $stampOut = Join-Path $OutDir 'skills-stamp.txt'
    $m = Get-Item -LiteralPath $srcMul
    $x = Get-Item -LiteralPath $srcIdx
    $renames = ($SkillRenames | ForEach-Object { "$($_.Index) $($_.From) > $($_.To)" }) -join '; '
    $stamp = "$SkillsStampFormat`nskills.mul $($m.Length) $($m.LastWriteTimeUtc.Ticks)`nSkills.idx $($x.Length) $($x.LastWriteTimeUtc.Ticks)`nrenames $renames"
    $lines = { "skills.mul=$(Join-Path $ListRoot 'skills.mul')"; "skills.idx=$(Join-Path $ListRoot 'Skills.idx')" }

    if ((Test-Path -LiteralPath $stampOut) -and (Test-Path -LiteralPath $outMul) -and (Test-Path -LiteralPath $outIdx) -and
        [IO.File]::ReadAllText($stampOut) -ceq $stamp) {
        & $lines
        return
    }
    if ($CheckOnly) { $script:Stale.Add('skills'); return }

    Start-Step 'skills'
    $mul = [IO.File]::ReadAllBytes($srcMul)
    $idx = [IO.File]::ReadAllBytes($srcIdx)
    if ($idx.Length % 12 -ne 0) { throw "Skills.idx is $($idx.Length) bytes, not whole 12-byte entries" }
    $ascii  = New-Object Text.ASCIIEncoding
    $newIdx = [byte[]]$idx.Clone()
    $packed = New-Object IO.MemoryStream
    $changed = 0
    for ($i = 0; $i -lt $idx.Length / 12; $i++) {
        $off = [BitConverter]::ToInt32($idx, $i * 12)
        $len = [BitConverter]::ToInt32($idx, $i * 12 + 4)
        if ($off -lt 0 -or $len -le 0) { continue }    # no skill in this slot: its index entry is kept as it is
        if ($off + $len -gt $mul.Length) { throw "skill $i points past the end of skills.mul" }
        $entry = New-Object byte[] $len
        [Array]::Copy($mul, $off, $entry, 0, $len)
        $r = @($SkillRenames | Where-Object { $_.Index -eq $i })
        if ($r.Count) {
            $name = $ascii.GetString($entry, 1, $len - 1).TrimEnd([char]0)
            if ($name -ceq $r[0].From) {
                $to = $ascii.GetBytes($r[0].To)
                $entry = New-Object byte[] ($to.Length + 2)
                $entry[0] = $mul[$off]
                [Array]::Copy($to, 0, $entry, 1, $to.Length)
                $changed++
            } elseif ($name -cne $r[0].To) {
                Say-Skip "Shattered Legacy skill names: skill $i stays '$name': your Ultima Online files no longer call it '$($r[0].From)'."
            }
        }
        [Array]::Copy([BitConverter]::GetBytes([int]$packed.Position), 0, $newIdx, $i * 12, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]$entry.Length), 0, $newIdx, $i * 12 + 4, 4)
        $packed.Write($entry, 0, $entry.Length)
    }
    if ($changed -eq 0) { Stop-Step; return }

    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    [IO.File]::WriteAllBytes($outMul, $packed.ToArray())
    [IO.File]::WriteAllBytes($outIdx, $newIdx)
    [IO.File]::WriteAllText($stampOut, $stamp, $ascii)
    Stop-Step
    & $lines
}

# cc-P368: returns the music's override lines, or none (no track shipped): "music/digital/config.txt=<copy>" and one
# "music/digital/<file>.mp3=<app\music\file.mp3>" per track. The copy is the player's own Music\Digital\Config.txt with
# our lines (app\music\shard-music.txt, "<id> <file>.mp3[,loop]", IDs 105 up) added at the end; a stock line is never
# changed. An ID the player's file already uses is skipped and said, and so is a track whose mp3 is not in app\music.
# TazUO resolves both paths through its override map (Sound.GetMusic -> GetUOFilePath("Music/Digital/<name>.mp3"),
# SoundsLoader.Load -> GetUOFilePath("Music/Digital/Config.txt"); the map's keys are lower case), so the mp3 plays from
# app\music\ and the player's Music folder is only ever read.
function Build-Music {
    $list = Join-Path $Music 'shard-music.txt'
    if (-not (Test-Path -LiteralPath $list)) { return }    # a package with no music of ours
    $ours = @(Get-Content -LiteralPath $list | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
    if ($ours.Count -eq 0) { return }
    $src = Join-Path $UoDir 'Music\Digital\Config.txt'
    if (-not (Test-Path -LiteralPath $src)) { throw "Music\Digital\Config.txt is not in $UoDir" }
    $outDir   = Join-Path $OutDir 'Music\Digital'
    $out      = Join-Path $outDir 'Config.txt'
    $stampOut = Join-Path $OutDir 'music-stamp.txt'
    $s = Get-Item -LiteralPath $src
    $stamp = "$MusicStampFormat`nConfig.txt $($s.Length) $($s.LastWriteTimeUtc.Ticks)`nours $((Get-FileHash -LiteralPath $list -Algorithm SHA256).Hash)"

    # Ours: id, file name (with .mp3), the whole line. The player's file is read either way, for the IDs it uses.
    $theirs = @(Get-Content -LiteralPath $src)
    $used = @{}
    foreach ($t in $theirs) {
        $m = [regex]::Match($t.Trim(), '^(\d+)\s')
        if ($m.Success) { $used[[int]$m.Groups[1].Value] = $t.Trim() }
    }
    $adds = @()
    foreach ($o in $ours) {
        $m = [regex]::Match($o, '^(\d+)\s+([^,\s]+\.mp3)(,loop)?$')
        if (-not $m.Success) { Say-Skip "Shattered Legacy music: skipping a line it cannot read: $o"; continue }
        $id = [int]$m.Groups[1].Value; $file = $m.Groups[2].Value
        if ($used.ContainsKey($id)) { Say-Skip "Shattered Legacy music: skipping track ${id}: your Ultima Online files now use that number."; continue }
        $mp3 = Join-Path $Music $file
        if (-not (Test-Path -LiteralPath $mp3)) { Say-Skip "Shattered Legacy music: skipping track ${id}: $file is not in app\music."; continue }
        if ($mp3.Contains('=')) { throw "the game folder's path has an '=' in it, which TazUO cannot read in an override list" }
        $adds += [pscustomobject]@{ Line = $o; Key = "music/digital/$($file.ToLowerInvariant())"; Path = (Resolve-Path -LiteralPath $mp3).Path }
    }
    if ($adds.Count -eq 0) { return }
    $lines = { "music/digital/config.txt=$(Join-Path $ListRoot 'Music\Digital\Config.txt')"; foreach ($a in $adds) { "$($a.Key)=$($a.Path)" } }

    if ((Test-Path -LiteralPath $stampOut) -and (Test-Path -LiteralPath $out) -and
        [IO.File]::ReadAllText($stampOut) -ceq $stamp) {
        & $lines
        return
    }
    if ($CheckOnly) { $script:Stale.Add('music'); return }

    Start-Step 'music'
    if (-not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    $text = [IO.File]::ReadAllText($src)
    if ($text.Length -and -not $text.EndsWith("`n")) { $text += "`r`n" }
    $text += (($adds | ForEach-Object { "$($_.Line)`r`n" }) -join '')
    [IO.File]::WriteAllText($out, $text, (New-Object Text.ASCIIEncoding))
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    Stop-Step
    & $lines
}

# Returns the map's three override lines, or none (no patch shipped). Apply-MapOps.ps1 does the work (cc-P188).
function Build-Map {
    if (-not (Test-Path -LiteralPath $MapOps)) { return }
    . (Join-Path $PSScriptRoot 'Apply-MapOps.ps1')
    # cc-P411: the list names the final folder; with -CheckOnly a stale map is said, not built.
    $r = @(Build-MapOverride -UoDir $UoDir -Ops $MapOps -OutDir $OutDir -ListDir $ListRoot -CheckOnly:$CheckOnly)
    if (@($r | Where-Object { $_ -isnot [string] }).Count) { $script:Stale.Add('map'); return }
    Stop-Step
    $r
}

try {
    Write-TraceHeader
    # TazUO splits each override line on '=' and needs exactly two parts (UOFilesOverrideMap.Load).
    if ($OutDir.Contains('=') -or $ListRoot.Contains('=')) { throw "the game folder's path has an '=' in it, which TazUO cannot read in an override list" }
    $listOut = Join-Path $OutDir 'files-override.txt'
    $lines = @()

    $art = @()
    try { $art = @(Build-Art | Where-Object { $_ }) }
    catch { Say-Skip "Shattered Legacy art could not be prepared ($($_.Exception.Message)). Starting without it." }
    $lines += $art

    # The worn-item animations only mean something through the tiledata copy (it is what names each Animation
    # number), so without the art part they are left out too, with no second line.
    if ($art.Count) {
        try { $lines += @(Build-EquipAnim) }
        catch { Say-Skip "Shattered Legacy worn-item art could not be prepared ($($_.Exception.Message)). Starting without it." }
    }

    try { $lines += @(Build-Cliloc) }
    catch { Say-Skip "Shattered Legacy text could not be prepared ($($_.Exception.Message)). Starting without it." }

    try { $lines += @(Build-Skills) }
    catch { Say-Skip "Shattered Legacy skill names could not be prepared ($($_.Exception.Message)). Starting without them." }

    try { $lines += @(Build-Map) }
    catch { Say-Skip "Shattered Legacy map (the Rootdeep Burrows) could not be prepared ($($_.Exception.Message)). Starting without it." }

    try { $lines += @(Build-Music) }
    catch { Say-Skip "Shattered Legacy music could not be prepared ($($_.Exception.Message)). Starting without it." }

    $lines = @($lines | Where-Object { $_ })
    # cc-P411: -CheckOnly says what is out of date and builds nothing; the list is the current one.
    if ($CheckOnly -and $script:Stale.Count) { return [pscustomobject]@{ Fresh = $false; Stale = @($script:Stale); List = $null } }
    $done = {
        param($result)
        Stop-Step
        Write-Trace ('total {0} s' -f ($script:RunClock.Elapsed.TotalSeconds.ToString('0.000', $script:Inv)))
        if ($ReadyFile) { [IO.File]::WriteAllText($ReadyFile, "ready $result`r`n", (New-Object Text.ASCIIEncoding)) }
        if ($CheckOnly) { return [pscustomobject]@{ Fresh = $true; Stale = @(); List = $result } }
        $result
    }
    if ($lines.Count -eq 0) {
        if (Test-Path -LiteralPath $listOut) { Remove-Item -LiteralPath $listOut -Force }
        return (& $done $null)
    }

    $list = "# Made by Build-UoOverrides.ps1 from this computer's own Ultima Online files. Never shipped.`r`n" +
            (($lines | ForEach-Object { "$_`r`n" }) -join '')
    # UTF-8 without a BOM: the path may not be ASCII, and TazUO reads the list with a default StreamReader.
    if (-not (Test-Path -LiteralPath $listOut) -or [IO.File]::ReadAllText($listOut) -cne $list) {
        if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
        [IO.File]::WriteAllText($listOut, $list, (New-Object Text.UTF8Encoding $false))
    }
    # The list names the final folder, which is where it will be once a staged set is swapped in.
    return (& $done (Join-Path $ListRoot 'files-override.txt'))
} catch {
    Say-Skip "Shattered Legacy art and text could not be prepared ($($_.Exception.Message)). Starting without them."
    if ($CheckOnly) { return [pscustomobject]@{ Fresh = $true; Stale = @(); List = $null } }
    return $null
}
