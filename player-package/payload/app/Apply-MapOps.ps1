#Requires -Version 5.1
<#
  Apply-MapOps.ps1 (cc-P188; designed and measured in cc-P40 section B.2): applies Shattered Legacy's map patch to
  copies of the player's own Ultima Online map files, the way Build-UoOverrides.ps1 patches copies of tiledata.mul and
  Cliloc.enu. Dot-source it and call Build-MapOverride; Build-UoOverrides.ps1 does, and so does the shard's own
  docker\uo\Build-MapOverlay.ps1 for the server's data folder, so one implementation serves both.

      . .\Apply-MapOps.ps1
      Build-MapOverride -UoDir <Ultima Online folder> -Ops <trammel.mapops> -OutDir <folder>

  The patch (app\trammel.mapops, shipped; made by scripts\mapbuild\mapops.py in the docs repo) holds only what we
  changed, so nothing of EA's ever ships:
      F <facet> <touched blocks>     the facet (1 is Trammel) and how many B lines follow
      B <block> <fingerprint>        sha256[:16] of EA's land (192 bytes) and statics rows of that block when the patch
                                     was made; a block EA changed since is reported in one yellow line and still patched
      L x y tile z                   the land cell becomes tile at z
      S+ x y z id hue                one static added
      S- x y z id hue                one EA static removed (matched exactly; one EA moved is reported and left)

  How the files are laid out (EA 7.0.117.0; TazUO MapLoader, pinned ModernUO TileMatrix.cs):
      map<N>LegacyMUL.uop  a UOP (MYP) whose entries build/map<N>legacymul/<chunk, 8 digits>.dat are uncompressed runs
                           of 4,096 blocks of 196 bytes (4-byte header, then 64 cells of tile uint16 + z sbyte, cell
                           index (y and 7) * 8 + (x and 7)); block id = (x shr 3) * blocksHigh + (y shr 3), blocksHigh
                           512 for Trammel. Entries are found by Jenkins lookup3 hashlittle2 of the name (below).
      staidx<N>.mul        12 bytes a block: lookup, length, extra. statics<N>.mul: 7-byte rows (id uint16, cell x,
                           cell y, z sbyte, hue uint16). Our blocks are rewritten at the END of the copy: EA's rows
                           first (less the removed ones), ours after, and the index repointed; EA's bytes are never
                           moved, so the output starts with EA's file byte for byte.
  The stamp (map-stamp.txt): the three EA files' sizes and times and the patch's hash; when it matches and the copies
  are there, nothing is rebuilt (every start but the first after an update or an EA patch).

  cc-P674 (the Siege of Sosaria swap, S1): -BaseDir names a folder whose base-<name> files (base-map1legacymul.uop,
  base-statics1.mul, base-staidx1.mul: the shipped app\map\) replace the player's own as the map the patch applies to.
  -AlsoAs 0 then also gives Felucca the result: map0LegacyMUL.uop is a copy of the patched map1LegacyMUL.uop with
  each entry's name hash rewritten from build/map1legacymul/ to build/map0legacymul/ (the client finds a UOP's
  entries by those hashes only; the data is the same), and Felucca's statics lines name Trammel's two files, which
  the client opens read-only and shared. The stamp names the base files and the extra facets, so a new base rebuilds.

  Returns the override lines for files-override.txt (lowercase names, as TazUO's UOFilesOverrideMap keys them):
      map1legacymul.uop=<path>  statics1.mul=<path>  staidx1.mul=<path>
  Throws on anything that is not right (the caller turns that into one yellow line and starts without the map).
  cc-P411: -ListDir names the folder the lines point to (default -OutDir; the launcher builds into a staging folder),
  -CheckOnly returns { Stale = 'map' } instead of building when the stamp does not match, and when the caller defines
  Start-Step and Write-StepProgress (Build-UoOverrides.ps1 does) each step is timed and reported.
  No Add-Type, no inline C#, no Windows API calls; ASCII only, on purpose (PowerShell 5.1 reads a UTF-8 file without
  a BOM as Windows-1252).
#>

$MapStampFormat = 'uo-map 2'   # cc-P674: 2 names each base file by its full path and adds the facets it is also
$MapBlockBytes = 196
$MapBlocksPerChunk = 4096
$MapBlocksHigh = @{ 0 = 512; 1 = 512; 2 = 200; 3 = 256; 4 = 181; 5 = 160 }   # facet height in blocks (FACETS in uofiles.py)

# Jenkins lookup3 hashlittle2 over the entry name, as the client and pinned ModernUO (UOPFiles) key UOP entries.
# int64 arithmetic masked to 32 bits after every step (a uint32 sum past 2^32 or a difference below 0 would leave
# PowerShell 5.1's integer types); the two 32-bit halves are joined as a uint64 at the end.
function Get-UopNameHash([string]$name) {
    $s = [Text.Encoding]::ASCII.GetBytes($name)
    $M = [int64]4294967295          # not 0xFFFFFFFF: PowerShell parses that hex literal as the Int32 -1
    $len = $s.Length; $k = 0
    [int64]$a = ([int64]3735928559 + $len) -band $M   # 0xDEADBEEF, as a decimal for the same reason
    [int64]$b = $a; [int64]$c = $a
    while ($len -gt 12) {
        $a = ($a + [int64][BitConverter]::ToUInt32($s, $k)) -band $M
        $b = ($b + [int64][BitConverter]::ToUInt32($s, $k + 4)) -band $M
        $c = ($c + [int64][BitConverter]::ToUInt32($s, $k + 8)) -band $M
        $a = ($a - $c) -band $M; $a = $a -bxor ((($c -shl 4) -bor ($c -shr 28)) -band $M); $c = ($c + $b) -band $M
        $b = ($b - $a) -band $M; $b = $b -bxor ((($a -shl 6) -bor ($a -shr 26)) -band $M); $a = ($a + $c) -band $M
        $c = ($c - $b) -band $M; $c = $c -bxor ((($b -shl 8) -bor ($b -shr 24)) -band $M); $b = ($b + $a) -band $M
        $a = ($a - $c) -band $M; $a = $a -bxor ((($c -shl 16) -bor ($c -shr 16)) -band $M); $c = ($c + $b) -band $M
        $b = ($b - $a) -band $M; $b = $b -bxor ((($a -shl 19) -bor ($a -shr 13)) -band $M); $a = ($a + $c) -band $M
        $c = ($c - $b) -band $M; $c = $c -bxor ((($b -shl 4) -bor ($b -shr 28)) -band $M); $b = ($b + $a) -band $M
        $len -= 12; $k += 12
    }
    if ($len -eq 0) { return ([uint64]$b -shl 32) -bor [uint64]$c }
    $t = New-Object byte[] 12
    [Array]::Copy($s, $k, $t, 0, $len)
    $c = ($c + (([int64]$t[8]) -bor ([int64]$t[9] -shl 8) -bor ([int64]$t[10] -shl 16) -bor ([int64]$t[11] -shl 24))) -band $M
    $b = ($b + [int64][BitConverter]::ToUInt32($t, 4)) -band $M
    $a = ($a + [int64][BitConverter]::ToUInt32($t, 0)) -band $M
    $c = $c -bxor $b; $c = ($c - ((($b -shl 14) -bor ($b -shr 18)) -band $M)) -band $M
    $a = $a -bxor $c; $a = ($a - ((($c -shl 11) -bor ($c -shr 21)) -band $M)) -band $M
    $b = $b -bxor $a; $b = ($b - ((($a -shl 25) -bor ($a -shr 7)) -band $M)) -band $M
    $c = $c -bxor $b; $c = ($c - ((($b -shl 16) -bor ($b -shr 16)) -band $M)) -band $M
    $a = $a -bxor $c; $a = ($a - ((($c -shl 4) -bor ($c -shr 28)) -band $M)) -band $M
    $b = $b -bxor $a; $b = ($b - ((($a -shl 14) -bor ($a -shr 18)) -band $M)) -band $M
    $c = $c -bxor $b; $c = ($c - ((($b -shl 24) -bor ($b -shr 8)) -band $M)) -band $M
    return ([uint64]$b -shl 32) -bor [uint64]$c
}

# hash -> absolute offset of the entry's data, for every entry of a UOP (uncompressed entries only; the map's are).
function Get-UopEntries([string]$path) {
    $map = @{}
    $fs = [IO.File]::Open($path, 'Open', 'Read', 'Read')
    try {
        $r = New-Object IO.BinaryReader($fs)
        if ($r.ReadUInt32() -ne 0x50594D) { throw "$path is not a UOP file" }
        $fs.Position = 12
        $next = $r.ReadInt64()
        while ($next -ne 0) {
            $fs.Position = $next
            $count = $r.ReadInt32(); $next = $r.ReadInt64()
            for ($i = 0; $i -lt $count; $i++) {
                $off = $r.ReadInt64(); $hl = $r.ReadInt32(); $cl = $r.ReadInt32(); $dl = $r.ReadInt32()
                $h = $r.ReadUInt64(); [void]$r.ReadUInt32(); $flag = $r.ReadInt16()
                if ($off -eq 0) { continue }
                if ($flag -ne 0 -or $cl -ne $dl) { throw "$path holds a compressed map entry, which this patcher does not read" }
                $map[$h] = $off + $hl
            }
        }
    } finally { $fs.Close() }
    return $map
}

function Get-MapStamp([string[]]$paths, [string]$ops, [int[]]$also) {
    $parts = @($MapStampFormat)
    foreach ($p in $paths) {
        $i = Get-Item -LiteralPath $p
        $parts += "$([IO.Path]::GetFullPath($p)) $($i.Length) $($i.LastWriteTimeUtc.Ticks)"
    }
    $parts += "ops $((Get-FileHash -LiteralPath $ops -Algorithm SHA256).Hash)"
    $parts += "also $(@($also) -join ',')"
    return ($parts -join "`n")
}

# cc-P674: the shipped base's name for one of the facet's files (base-map1legacymul.uop and so on). Never an EA name,
# so the package's gate 4 can still refuse any file named like one of EA's.
function Get-MapBaseName([string]$f) { 'base-' + $f.ToLowerInvariant() }

# cc-P674: copies a facet's UOP map to another facet's name: the same bytes, with every entry's name hash
# build/map<From>legacymul/<chunk>.dat rewritten to build/map<To>legacymul/<chunk>.dat. Throws on an entry whose hash
# is none of the From names (a file this was not made for).
function Copy-UopRenamed([string]$Source, [string]$Destination, [int]$From, [int]$To) {
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    $fs = [IO.File]::Open($Destination, 'Open', 'ReadWrite', 'None')
    try {
        $r = New-Object IO.BinaryReader($fs)
        $w = New-Object IO.BinaryWriter($fs)
        if ($r.ReadUInt32() -ne 0x50594D) { throw "$Source is not a UOP file" }
        $fs.Position = 12
        $next = $r.ReadInt64()
        $rename = @{}; $named = 0; $done = 0
        while ($next -ne 0) {
            $fs.Position = $next
            $count = $r.ReadInt32(); $next = $r.ReadInt64()
            $table = $fs.Position
            for ($i = 0; $i -lt $count; $i++) {
                $fs.Position = $table + $i * 34
                $off = $r.ReadInt64()
                if ($off -eq 0) { continue }
                $fs.Position = $table + $i * 34 + 20
                $h = $r.ReadUInt64()
                while (-not $rename.ContainsKey($h) -and $named -lt 4096) {
                    $rename[(Get-UopNameHash ('build/map{0}legacymul/{1:D8}.dat' -f $From, $named))] = Get-UopNameHash ('build/map{0}legacymul/{1:D8}.dat' -f $To, $named)
                    $named++
                }
                if (-not $rename.ContainsKey($h)) { throw "$Source holds an entry that is not a map$From chunk" }
                $fs.Position = $table + $i * 34 + 20
                $w.Write([uint64]$rename[$h])
                $done++
            }
        }
        $w.Flush()
    } finally { $fs.Close() }
    return $done
}

function Build-MapOverride {
    param([Parameter(Mandatory = $true)][string]$UoDir, [Parameter(Mandatory = $true)][string]$Ops, [Parameter(Mandatory = $true)][string]$OutDir,
          [string]$ListDir, [switch]$CheckOnly,
          [string]$BaseDir,    # cc-P674: the shipped base map (app\map\), in place of the player's own files
          [int[]]$AlsoAs = @())  # cc-P674: the facets that also read the result (0: Felucca on the SoS land)
    if (-not (Test-Path -LiteralPath $Ops)) { return }      # a package with no map patch
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $traced = [bool](Get-Command Start-Step -ErrorAction SilentlyContinue)
    $step = { param($n) if ($traced) { Start-Step $n } }
    $tick = { param($d, $t) if ($traced) { Write-StepProgress $d $t } }

    # --- the patch -------------------------------------------------------------------------
    # cc-P611 (P549 #4): read and counted in a step of its own, so a slow parse is on the bar by name.
    & $step 'map.parse'
    $facet = -1; $fps = @{}; $land = New-Object 'System.Collections.Generic.List[int[]]'
    $adds = @{}; $removes = @{}
    $read = 0
    foreach ($line in [IO.File]::ReadLines((Resolve-Path -LiteralPath $Ops).Path)) {
        if (((++$read) -band 4095) -eq 0) { & $tick $read -1 }
        if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
        $p = $line.Split(' ')
        switch ($p[0]) {
            'F' { $facet = [int]$p[1]; if (-not $MapBlocksHigh.ContainsKey($facet)) { throw "facet $facet is not one this patcher knows" } }
            'B' { $fps[[int]$p[1]] = $p[2] }
            'L' { $land.Add([int[]]@([int]$p[1], [int]$p[2], [int]$p[3], [int]$p[4])) }
            'S+' { $null = 0 }
            'S-' { $null = 0 }
            default { throw "trammel.mapops: a line this patcher does not read: $line" }
        }
        if ($p[0] -eq 'S+' -or $p[0] -eq 'S-') {
            if ($facet -lt 0) { throw 'trammel.mapops: a static line before the F line' }
            $x = [int]$p[1]; $y = [int]$p[2]
            $b = ($x -shr 3) * $MapBlocksHigh[$facet] + ($y -shr 3)
            $rec = New-Object byte[] 7
            [BitConverter]::GetBytes([uint16]$p[4]).CopyTo($rec, 0)
            $rec[2] = [byte]($x -band 7); $rec[3] = [byte]($y -band 7)
            $rec[4] = [byte](([int]$p[3] + 256) % 256)
            [BitConverter]::GetBytes([uint16]$p[5]).CopyTo($rec, 5)
            $t = if ($p[0] -eq 'S+') { $adds } else { $removes }
            if (-not $t.ContainsKey($b)) { $t[$b] = New-Object 'System.Collections.Generic.List[byte[]]' }
            $t[$b].Add($rec)
        }
    }
    if ($facet -lt 0) { throw 'trammel.mapops has no F line' }
    $blocksHigh = $MapBlocksHigh[$facet]
    $files = @("map${facet}LegacyMUL.uop", "statics$facet.mul", "staidx$facet.mul")
    $src = @(foreach ($f in $files) { if ($BaseDir) { Join-Path $BaseDir (Get-MapBaseName $f) } else { Join-Path $UoDir $f } })
    for ($i = 0; $i -lt 3; $i++) { if (-not (Test-Path -LiteralPath $src[$i])) { throw "$(Split-Path $src[$i] -Leaf) is not in $(Split-Path $src[$i] -Parent)" } }
    foreach ($a in $AlsoAs) { if ($a -eq $facet -or -not $MapBlocksHigh.ContainsKey($a) -or $MapBlocksHigh[$a] -ne $MapBlocksHigh[$facet]) { throw "facet $a cannot read facet $facet's map" } }
    $alsoFiles = @(foreach ($a in $AlsoAs) { "map${a}LegacyMUL.uop" })

    # --- the stamp -------------------------------------------------------------------------
    & $step 'map.stamp'   # cc-P611: hashing the patch
    $stampOut = Join-Path $OutDir 'map-stamp.txt'
    $stamp = Get-MapStamp $src $Ops $AlsoAs
    $lines = {
        $o = $(if ($ListDir) { [IO.Path]::GetFullPath($ListDir) } else { (Resolve-Path -LiteralPath $OutDir).Path })
        foreach ($f in $files) { "$($f.ToLowerInvariant())=$(Join-Path $o $f)" }
        foreach ($a in $AlsoAs) {   # cc-P674: Felucca's own map, and Trammel's statics, read by both
            "map${a}legacymul.uop=$(Join-Path $o "map${a}LegacyMUL.uop")"
            "statics$a.mul=$(Join-Path $o $files[1])"
            "staidx$a.mul=$(Join-Path $o $files[2])"
        }
    }
    if ((Test-Path -LiteralPath $stampOut) -and [IO.File]::ReadAllText($stampOut) -ceq $stamp -and
        @($files + $alsoFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $OutDir $_)) }).Count -eq 0) {
        return (& $lines)
    }
    if ($CheckOnly) { return [pscustomobject]@{ Stale = 'map' } }
    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    if (Test-Path -LiteralPath $stampOut) { Remove-Item -LiteralPath $stampOut -Force }

    # --- the land: a copy of EA's file with our cells written in place -----------------------
    $mapOut = Join-Path $OutDir $files[0]
    & $step 'map.copy'
    Copy-Item -LiteralPath $src[0] -Destination $mapOut -Force
    & $step 'map.read'
    $entries = Get-UopEntries $mapOut
    $chunkOffset = @{}   # chunk -> absolute offset of its first block
    function Get-BlockPosition([int]$b) {
        $chunk = [int][Math]::Floor($b / $MapBlocksPerChunk)
        if (-not $chunkOffset.ContainsKey($chunk)) {
            $h = Get-UopNameHash ('build/map{0}legacymul/{1:D8}.dat' -f $facet, $chunk)
            if (-not $entries.ContainsKey($h)) { throw "$($files[0]) has no chunk $chunk, which block $b of the patch needs" }
            $chunkOffset[$chunk] = $entries[$h]
        }
        return $chunkOffset[$chunk] + ($b % $MapBlocksPerChunk) * $MapBlockBytes
    }
    $idx = [IO.File]::ReadAllBytes($src[2])
    $sta = [IO.File]::ReadAllBytes($src[1])
    $drifted = 0
    & $step 'map.ops'
    $sha = [Security.Cryptography.SHA256]::Create()
    $fs = [IO.File]::Open($mapOut, 'Open', 'ReadWrite', 'None')
    try {
        $buf = New-Object byte[] 192
        foreach ($b in @($fps.Keys | Sort-Object)) {
            $fs.Position = (Get-BlockPosition $b) + 4
            if ($fs.Read($buf, 0, 192) -ne 192) { throw "block $b is past the end of $($files[0])" }
            $lk = [BitConverter]::ToInt32($idx, $b * 12); $ln = [BitConverter]::ToInt32($idx, $b * 12 + 4)
            $n = if ($lk -ge 0 -and $ln -gt 0) { $ln } else { 0 }
            $all = New-Object byte[] (192 + $n)
            [Array]::Copy($buf, 0, $all, 0, 192)
            if ($n -gt 0) { [Array]::Copy($sta, $lk, $all, 192, $n) }
            $h = ([BitConverter]::ToString($sha.ComputeHash($all)) -replace '-', '').Substring(0, 16).ToLowerInvariant()
            if ($h -ne $fps[$b]) { $drifted++ }
        }
        $cell = New-Object byte[] 3
        $k = 0
        foreach ($l in $land) {
            if (((++$k) -band 1023) -eq 0) { & $tick $k $land.Count }
            $b = ($l[0] -shr 3) * $blocksHigh + ($l[1] -shr 3)
            $fs.Position = (Get-BlockPosition $b) + 4 + ((($l[1] -band 7) * 8) + ($l[0] -band 7)) * 3
            [BitConverter]::GetBytes([uint16]$l[2]).CopyTo($cell, 0)
            $cell[2] = [byte](($l[3] + 256) % 256)
            $fs.Write($cell, 0, 3)
        }
    } finally { $fs.Close(); $sha.Dispose() }
    if ($drifted) { Write-Host "  Shattered Legacy map: your Ultima Online files changed $drifted of our blocks since this patch was made; patched anyway." -ForegroundColor Yellow }

    # --- the statics: EA's file first, our blocks appended, their index rows repointed -------
    & $step 'map.statics'
    $staOut = New-Object IO.MemoryStream
    $staOut.Write($sta, 0, $sta.Length)
    $missing = 0
    foreach ($b in @(@($adds.Keys) + @($removes.Keys) | Sort-Object -Unique)) {
        $lk = [BitConverter]::ToInt32($idx, $b * 12); $ln = [BitConverter]::ToInt32($idx, $b * 12 + 4)
        $recs = New-Object 'System.Collections.Generic.List[byte[]]'
        if ($lk -ge 0 -and $ln -gt 0) {
            for ($i = 0; $i + 7 -le $ln; $i += 7) { $r = New-Object byte[] 7; [Array]::Copy($sta, $lk + $i, $r, 0, 7); $recs.Add($r) }
        }
        if ($removes.ContainsKey($b)) {
            foreach ($rm in $removes[$b]) {
                $hit = -1
                for ($i = 0; $i -lt $recs.Count; $i++) {
                    $e = $recs[$i]
                    if ($e[0] -eq $rm[0] -and $e[1] -eq $rm[1] -and $e[2] -eq $rm[2] -and $e[3] -eq $rm[3] -and $e[4] -eq $rm[4] -and $e[5] -eq $rm[5] -and $e[6] -eq $rm[6]) { $hit = $i; break }
                }
                if ($hit -ge 0) { $recs.RemoveAt($hit) } else { $missing++ }
            }
        }
        if ($adds.ContainsKey($b)) { $recs.AddRange($adds[$b]) }
        if ($recs.Count -eq 0) {
            [BitConverter]::GetBytes([int]-1).CopyTo($idx, $b * 12); [BitConverter]::GetBytes([int]-1).CopyTo($idx, $b * 12 + 4)
            continue
        }
        [BitConverter]::GetBytes([int]$staOut.Length).CopyTo($idx, $b * 12)
        [BitConverter]::GetBytes([int]($recs.Count * 7)).CopyTo($idx, $b * 12 + 4)
        foreach ($r in $recs) { $staOut.Write($r, 0, 7) }
    }
    if ($missing) { Write-Host "  Shattered Legacy map: $missing piece(s) the patch removes are no longer where your files had them; left as they are." -ForegroundColor Yellow }
    & $step 'map.write'
    [IO.File]::WriteAllBytes((Join-Path $OutDir $files[1]), $staOut.ToArray())
    [IO.File]::WriteAllBytes((Join-Path $OutDir $files[2]), $idx)
    foreach ($a in $AlsoAs) {
        & $step "map.copy$a"   # cc-P674: Felucca's copy of the patched land
        $null = Copy-UopRenamed $mapOut (Join-Path $OutDir "map${a}LegacyMUL.uop") $facet $a
    }
    [IO.File]::WriteAllText($stampOut, $stamp, (New-Object Text.ASCIIEncoding))
    Write-Host ("  Shattered Legacy map: patched copies of {0} ({1} cells, {2} pieces added, {3} removed) in {4:n1} s." -f $files[0], $land.Count,
        (@($adds.Values | ForEach-Object { $_.Count }) | Measure-Object -Sum).Sum, (@($removes.Values | ForEach-Object { $_.Count }) | Measure-Object -Sum).Sum, ($sw.ElapsedMilliseconds / 1000)) -ForegroundColor Gray
    return (& $lines)
}
