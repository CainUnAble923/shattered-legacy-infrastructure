#Requires -Version 5.1
<#
  Builds patched copies of the player's own Ultima Online files, so TazUO shows Shattered Legacy's own
  art and text. Play.ps1 runs it before starting the game.

      Build-UoOverrides.ps1 -UoDir <Ultima Online folder>
      Build-UoOverrides.ps1 -UoDir <dir> -Records <json> -Clilocs <json> -OutDir <dir>      (tests)

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
    1. Reads records.json's "equipAnims" (anim N, slot S).
    2. If app\uo-overrides\equip-stamp.txt matches the player's Bodyconv.def and mobtypes.txt (size and time),
       the records and both anim files (SHA-256), and the copy is there, it is done.
    3. Otherwise refuses if the player's folder has an anim7.mul or anim7.idx of its own (EA has begun to use the
       file), and skips, with a yellow line, any N that the player's Bodyconv.def or mobtypes.txt already names
       (mobtypes would send N to the UOP files instead). Then writes the player's Bodyconv.def, unchanged, with
       our lines added at the end, to app\uo-overrides\Bodyconv.def, then the stamp.

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
    [string]$AnimIdx
)

$ErrorActionPreference = 'Stop'
if (-not $Records) { $Records = Join-Path $PSScriptRoot 'art-records.json' }
if (-not $AnimMul) { $AnimMul = Join-Path $PSScriptRoot 'sl-anim7.bin' }
if (-not $AnimIdx) { $AnimIdx = Join-Path $PSScriptRoot 'sl-anim7-idx.bin' }
if (-not $Clilocs) { $Clilocs = Join-Path $PSScriptRoot 'cliloc-entries.json' }
if (-not $OutDir)  { $OutDir  = Join-Path $PSScriptRoot 'uo-overrides' }

$LandBytes   = 512 * (4 + 32 * 30)
$StaticGroup = 4 + 32 * 41
$AnimEntry   = 68
$StampFormat = 'uo-overrides 1'
$ClilocStampFormat = 'uo-cliloc 1'
$EquipStampFormat = 'uo-equip 1'

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
    $lines = { $f = (Resolve-Path -LiteralPath $OutDir).Path; "tiledata.mul=$(Join-Path $f 'tiledata.mul')"; "animdata.mul=$(Join-Path $f 'animdata.mul')" }

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

    # --- rebuild ------------------------------------------------------------------------------
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

    # Tiledata records with no animation of their own (cc-P62: a worn item's icon, which names its Animation
    # number). Same tests on the one slot: tiledata free, and not drawn as another animation's frame.
    $animated = @(@($rec.animdata) | ForEach-Object { [int]$_.id })
    foreach ($r in @($rec.tiledata | Where-Object { $animated -notcontains [int]$_.id })) {
        $why = Test-TileFree $tile ([int]$r.id)
        if (-not $why -and $targets.Contains([int]$r.id)) { $why = 'is a frame of another animation' }
        if ($why) {
            Say-Skip "Shattered Legacy art: skipping item $($r.id): your Ultima Online files now use it (art ID $($r.id) $why)."
            continue
        }
        Write-TileRecord $tile $r
    }

    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    [IO.File]::WriteAllBytes($tileOut, $tile)
    [IO.File]::WriteAllBytes($animOut, $anim)
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
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

# Returns the worn-item animations' override lines ("name=path"), or none (cc-P62).
function Build-EquipAnim {
    if (-not (Test-Path -LiteralPath $Records)) { return }
    $eq = @((([IO.File]::ReadAllText($Records) | ConvertFrom-Json).equipAnims) | Where-Object { $_ })
    if ($eq.Count -eq 0) { return }
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
    $stampOut = Join-Path $OutDir 'equip-stamp.txt'
    $lines = { "bodyconv.def=$((Resolve-Path -LiteralPath $bcOut).Path)"; "anim7.mul=$AnimMul"; "anim7.idx=$AnimIdx" }
    $hash = { param($p) (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
    $stamp = "$EquipStampFormat`nBodyconv.def $(Get-FileStamp $bcSrc)`nmobtypes.txt $(Get-FileStamp $mtSrc)`nrecords $(& $hash $Records)`nanim $(& $hash $AnimMul) $(& $hash $AnimIdx)"

    if ((Test-Path -LiteralPath $stampOut) -and (Test-Path -LiteralPath $bcOut) -and
        [IO.File]::ReadAllText($stampOut) -ceq $stamp) {
        & $lines
        return
    }

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

    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    if ($add.Count -eq 0) {
        if (Test-Path -LiteralPath $bcOut) { Remove-Item -LiteralPath $bcOut -Force }
        return
    }
    # The player's file byte for byte, then ours. A line break first if their last line has none.
    $src = [IO.File]::ReadAllBytes($bcSrc)
    $tail = ''
    if ($src.Length -and $src[$src.Length - 1] -ne 10) { $tail = "`r`n" }
    $tail += "# Shattered Legacy's worn-item animations (app\Build-UoOverrides.ps1): body N is slot S of anim7.mul.`r`n"
    $tail += (($add | ForEach-Object { "$_`r`n" }) -join '')
    $b = [Text.Encoding]::ASCII.GetBytes($tail)
    $all = New-Object byte[] ($src.Length + $b.Length)
    [Array]::Copy($src, 0, $all, 0, $src.Length)
    [Array]::Copy($b, 0, $all, $src.Length, $b.Length)
    [IO.File]::WriteAllBytes($bcOut, $all)
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    & $lines
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
        "cliloc.enu=$((Resolve-Path -LiteralPath $out).Path)"
        return
    }

    $ours = @(([IO.File]::ReadAllText($Clilocs) | ConvertFrom-Json).entries)
    if ($ours.Count -eq 0) { return }
    Use-ClilocType
    $table = [ShatteredLegacy.Cliloc]::Table([IO.File]::ReadAllBytes($src))
    $skipped = New-Object 'System.Collections.Generic.List[string]'
    $numbers = [int[]]@($ours | ForEach-Object { [int]$_.number })
    $texts   = [string[]]@($ours | ForEach-Object { "$($_.text)" })
    $merged  = [ShatteredLegacy.Cliloc]::Add($table, $numbers, $texts, $skipped)
    foreach ($n in $skipped) {
        Say-Skip "Shattered Legacy text: skipping entry ${n}: your Ultima Online files now use that number."
    }

    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }
    [IO.File]::WriteAllBytes($out, $merged)
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    "cliloc.enu=$((Resolve-Path -LiteralPath $out).Path)"
}

try {
    # TazUO splits each override line on '=' and needs exactly two parts (UOFilesOverrideMap.Load).
    if ($OutDir.Contains('=')) { throw "the game folder's path has an '=' in it, which TazUO cannot read in an override list" }
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

    $lines = @($lines | Where-Object { $_ })
    if ($lines.Count -eq 0) {
        if (Test-Path -LiteralPath $listOut) { Remove-Item -LiteralPath $listOut -Force }
        return $null
    }

    $list = "# Made by Build-UoOverrides.ps1 from this computer's own Ultima Online files. Never shipped.`r`n" +
            (($lines | ForEach-Object { "$_`r`n" }) -join '')
    # UTF-8 without a BOM: the path may not be ASCII, and TazUO reads the list with a default StreamReader.
    if (-not (Test-Path -LiteralPath $listOut) -or [IO.File]::ReadAllText($listOut) -cne $list) {
        [IO.File]::WriteAllText($listOut, $list, (New-Object Text.UTF8Encoding $false))
    }
    return $listOut
} catch {
    Say-Skip "Shattered Legacy art and text could not be prepared ($($_.Exception.Message)). Starting without them."
    return $null
}
