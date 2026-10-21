#Requires -Version 5.1
# Facts for payload\app\Apply-MapOps.ps1, the map patch applier Play.ps1 runs through Build-UoOverrides.ps1 (cc-P188).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Map-Overrides.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Build-UoOverrides.Tests.ps1.
#
# No EA file is read. The map, statics and index here are synthetic, built by New-MapFacet with the layout the client
# and the server read (a UOP whose one chunk holds 4,096 blocks of 196 bytes; 12-byte index rows; 7-byte statics
# rows), and the expected offsets are written out as numbers, not computed by the code under test. The docs repo's
# tools/tests/test_mapops.py proves this applier writes the same bytes as the Python one that makes the patch; these
# facts prove the pieces a player's launch depends on, on their own.

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$pkg     = Split-Path $here -Parent
$applier = Join-Path $pkg 'payload\app\Apply-MapOps.ps1'
$builder = Join-Path $pkg 'payload\app\Build-UoOverrides.ps1'
$shipped = Join-Path $pkg 'vendor\shattered-legacy-map\trammel.mapops'
$work    = Join-Path $env:TEMP ("sl-map-overrides-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null
. $applier

$results = New-Object 'System.Collections.Generic.List[object]'

function Fact {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Message = '' })
        Write-Host "  PASS  $Name" -ForegroundColor Green
    } catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Message = $_.Exception.Message })
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Because = '')
    if ($Expected -ne $Actual) { throw "expected [$Expected] but got [$Actual]. $Because" }
}

function Assert-True {
    param($Condition, [string]$Because)
    if (-not $Condition) { throw "not true: $Because" }
}

# A facet-1 map with one chunk (blocks 0-4095: x 0-63, every y), every cell tile 3 at z 0, an index of 4,096 empty
# rows and an empty statics file. Returns the folder. The chunk's data starts at 28 (header) + 12 (table head)
# + 34 (one entry) = 74.
function New-MapFacet([string]$dir) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $block = New-Object byte[] 196
    for ($i = 0; $i -lt 64; $i++) { $block[4 + $i * 3] = 3 }
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter($ms)
    # the signature is 0xFD23EC43, written as a decimal: PowerShell parses that hex literal as a negative Int32
    $w.Write([uint32]0x50594D); $w.Write([int]5); $w.Write([uint32]4246727747); $w.Write([int64]28); $w.Write([int]100); $w.Write([int]1)
    $w.Write([int]1); $w.Write([int64]0)
    $w.Write([int64]74); $w.Write([int]0); $w.Write([int](196 * 4096)); $w.Write([int](196 * 4096))
    $w.Write([uint64](Get-UopNameHash 'build/map1legacymul/00000000.dat')); $w.Write([uint32]0); $w.Write([int16]0)
    for ($b = 0; $b -lt 4096; $b++) { $w.Write($block, 0, 196) }
    $w.Flush()
    [IO.File]::WriteAllBytes((Join-Path $dir 'map1LegacyMUL.uop'), $ms.ToArray())
    $idx = New-Object byte[] (4096 * 12)
    for ($b = 0; $b -lt 4096; $b++) { [BitConverter]::GetBytes([int]-1).CopyTo($idx, $b * 12); [BitConverter]::GetBytes([int]-1).CopyTo($idx, $b * 12 + 4) }
    [IO.File]::WriteAllBytes((Join-Path $dir 'staidx1.mul'), $idx)
    [IO.File]::WriteAllBytes((Join-Path $dir 'statics1.mul'), (New-Object byte[] 0))
    return $dir
}

# Puts EA rows into block b of a synthetic facet: statics appended, the index row pointed at them.
function Add-EaRows([string]$dir, [int]$b, [byte[][]]$rows) {
    $idx = [IO.File]::ReadAllBytes((Join-Path $dir 'staidx1.mul'))
    $sta = [IO.File]::ReadAllBytes((Join-Path $dir 'statics1.mul'))
    $ms = New-Object IO.MemoryStream; $ms.Write($sta, 0, $sta.Length)
    [BitConverter]::GetBytes([int]$sta.Length).CopyTo($idx, $b * 12)
    [BitConverter]::GetBytes([int]($rows.Count * 7)).CopyTo($idx, $b * 12 + 4)
    foreach ($r in $rows) { $ms.Write($r, 0, 7) }
    [IO.File]::WriteAllBytes((Join-Path $dir 'statics1.mul'), $ms.ToArray())
    [IO.File]::WriteAllBytes((Join-Path $dir 'staidx1.mul'), $idx)
}

function Row([int]$id, [int]$cx, [int]$cy, [int]$z, [int]$hue) {
    $r = New-Object byte[] 7
    [BitConverter]::GetBytes([uint16]$id).CopyTo($r, 0); $r[2] = [byte]$cx; $r[3] = [byte]$cy; $r[4] = [byte](($z + 256) % 256); [BitConverter]::GetBytes([uint16]$hue).CopyTo($r, 5)
    return ,$r
}

Write-Host "Map override facts (cc-P188)" -ForegroundColor Cyan

Fact 'TheEntryNameHashIsJenkinsLookup3AsTheClientAndTheServerKeyUopEntries' {
    # Values from the Python port in the docs repo (scripts/mapbuild/uofiles.py hashlittle2), which reads EA's real map.
    Assert-Equal ([uint64]16288908501016938652) (Get-UopNameHash 'abcd')
    Assert-Equal ([uint64]3278078857962359993) (Get-UopNameHash 'build/map1legacymul/00000000.dat')
    Assert-Equal ([uint64]13883386810121431678) (Get-UopNameHash 'build/map1legacymul/00000112.dat')
}

Fact 'ALandLineWritesExactlyItsThreeBytesAndNothingElse' {
    $ea = New-MapFacet (Join-Path $work 'ea1')
    $ops = Join-Path $work 'one.mapops'
    # block of (10, 20) = (10 shr 3) * 512 + (20 shr 3) = 514; cell (20 and 7) * 8 + (10 and 7) = 34
    [IO.File]::WriteAllText($ops, "F 1 0`nL 10 20 581 -3`n", (New-Object Text.ASCIIEncoding))
    $out = Join-Path $work 'out1'
    $lines = @(Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out)
    Assert-Equal 3 $lines.Count
    Assert-Equal "map1legacymul.uop=$out\map1LegacyMUL.uop" $lines[0]
    $src = [IO.File]::ReadAllBytes((Join-Path $ea 'map1LegacyMUL.uop'))
    $dst = [IO.File]::ReadAllBytes((Join-Path $out 'map1LegacyMUL.uop'))
    Assert-Equal $src.Length $dst.Length
    $o = 74 + 514 * 196 + 4 + 34 * 3
    Assert-Equal 69 $dst[$o]; Assert-Equal 2 $dst[$o + 1]; Assert-Equal 253 $dst[$o + 2]   # 581 = 0x0245 little-endian, z -3
    $diff = 0
    for ($i = 0; $i -lt $src.Length; $i++) { if ($src[$i] -ne $dst[$i]) { $diff++ } }
    Assert-Equal 3 $diff 'only the one cell changed'
    Assert-True ([IO.File]::ReadAllBytes((Join-Path $out 'statics1.mul')).Length -eq 0) 'no statics were touched'
    Assert-True ((Compare-Object ([IO.File]::ReadAllBytes((Join-Path $ea 'staidx1.mul'))) ([IO.File]::ReadAllBytes((Join-Path $out 'staidx1.mul')))) -eq $null) 'the index is unchanged'
}

Fact 'AddedStaticsGoAfterEaRowsAtTheEndAndARemovedOneGoesExactly' {
    $ea = New-MapFacet (Join-Path $work 'ea2')
    # EA's block 514 holds two rows; our patch removes the second and adds one; block 2572 ((40 shr 3) * 512 + (101 shr 3))
    # gets a row of ours alone
    Add-EaRows $ea 514 @((Row 0x0E31 2 4 0 0), (Row 0x0B26 3 4 5 0))
    $ops = Join-Path $work 'two.mapops'
    [IO.File]::WriteAllText($ops, "F 1 0`nS- 11 20 5 2854 0`nS+ 13 20 13 2572 0`nS+ 40 101 20 580 0`n", (New-Object Text.ASCIIEncoding))
    $out = Join-Path $work 'out2'
    $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out
    $sta = [IO.File]::ReadAllBytes((Join-Path $out 'statics1.mul'))
    $idx = [IO.File]::ReadAllBytes((Join-Path $out 'staidx1.mul'))
    Assert-Equal (14 + 14 + 7) $sta.Length 'EA 14 bytes kept, block 514 rewritten (2 rows), block 2572 (1 row)'
    Assert-Equal 14 ([BitConverter]::ToInt32($idx, 514 * 12)); Assert-Equal 14 ([BitConverter]::ToInt32($idx, 514 * 12 + 4))
    Assert-Equal 28 ([BitConverter]::ToInt32($idx, 2572 * 12)); Assert-Equal 7 ([BitConverter]::ToInt32($idx, 2572 * 12 + 4))
    # block 514 now: the kept EA row, then ours (id 2572 = 0x0A0C, cell 5,4, z 13)
    Assert-Equal 0x31 $sta[14]; Assert-Equal 0x0E $sta[15]; Assert-Equal 2 $sta[16]
    Assert-Equal 0x0C $sta[21]; Assert-Equal 0x0A $sta[22]; Assert-Equal 5 $sta[23]; Assert-Equal 4 $sta[24]; Assert-Equal 13 $sta[25]
    # block 2572: id 580 = 0x0244, cell 0,5, z 20
    Assert-Equal 0x44 $sta[28]; Assert-Equal 0x02 $sta[29]; Assert-Equal 0 $sta[30]; Assert-Equal 5 $sta[31]; Assert-Equal 20 $sta[32]
}

Fact 'TheStampSkipsASecondRunAndANewPatchRebuilds' {
    $ea = New-MapFacet (Join-Path $work 'ea3')
    $ops = Join-Path $work 'three.mapops'
    [IO.File]::WriteAllText($ops, "F 1 0`nL 1 1 5 0`n", (New-Object Text.ASCIIEncoding))
    $out = Join-Path $work 'out3'
    $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out
    $t1 = (Get-Item (Join-Path $out 'map1LegacyMUL.uop')).LastWriteTimeUtc
    Start-Sleep -Milliseconds 50
    $lines = @(Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out)
    Assert-Equal 3 $lines.Count
    Assert-Equal $t1 (Get-Item (Join-Path $out 'map1LegacyMUL.uop')).LastWriteTimeUtc 'nothing rewritten'
    [IO.File]::WriteAllText($ops, "F 1 0`nL 1 1 6 0`n", (New-Object Text.ASCIIEncoding))
    $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out
    $dst = [IO.File]::ReadAllBytes((Join-Path $out 'map1LegacyMUL.uop'))
    Assert-Equal 6 $dst[74 + 0 * 196 + 4 + 9 * 3] 'the new patch is applied (cell 1,1 is block 0, cell 9)'
}

Fact 'ADriftedBlockIsReportedAndStillPatchedAndAMovedRemovalIsLeft' {
    $ea = New-MapFacet (Join-Path $work 'ea4')
    $ops = Join-Path $work 'four.mapops'
    # the fingerprint of a block whose EA land is all tile 3 at z 0 and whose statics are empty, as the patch maker
    # would have written it; here it is deliberately wrong, so the block reads as drifted
    [IO.File]::WriteAllText($ops, "F 1 1`nB 514 0000000000000000`nL 10 20 7 0`nS- 10 20 0 2000 0`n", (New-Object Text.ASCIIEncoding))
    $out = Join-Path $work 'out4'
    $said = @(Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out 6>&1 | ForEach-Object { "$_" })
    Assert-True (($said -join ' ') -match 'changed 1 of our blocks') "drift reported: $($said -join ' | ')"
    Assert-True (($said -join ' ') -match '1 piece\(s\) the patch removes') "missing removal reported: $($said -join ' | ')"
    $dst = [IO.File]::ReadAllBytes((Join-Path $out 'map1LegacyMUL.uop'))
    Assert-Equal 7 $dst[74 + 514 * 196 + 4 + 34 * 3] 'patched anyway'
}

Fact 'NoPatchMeansNoLinesAndNoErrorFromTheBuilder' {
    $ea = New-MapFacet (Join-Path $work 'ea5')
    $r = @(Build-MapOverride -UoDir $ea -Ops (Join-Path $work 'none.mapops') -OutDir (Join-Path $work 'out5'))
    Assert-Equal 0 $r.Count
}

Fact 'TheShippedPatchAppliesToASyntheticTrammelWithoutErrorWhenItsChunksExist' {
    # The real patch names chunks 1 and up (x >= 64); on the one-chunk facet the applier must say so, not corrupt it.
    $ea = New-MapFacet (Join-Path $work 'ea6')
    $threw = $null
    try { $null = Build-MapOverride -UoDir $ea -Ops $shipped -OutDir (Join-Path $work 'out6') } catch { $threw = $_.Exception.Message }
    Assert-True ($threw -match 'has no chunk') "a missing chunk is an error, got: $threw"
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host "$($results.Count) facts, $failed failed" -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
