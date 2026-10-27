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
#
# cc-P674 (SoS swap S1): a shipped base map (-BaseDir, app\map\base-<name>) is patched instead of the player's own
# files, Felucca gets the same land (-AlsoAs 0: map0LegacyMUL.uop with map0's entry names, Trammel's statics), and the
# stamp names the base, so a new one rebuilds.
#
# cc-P713: the base ships only while vendor\sos-trammel-map\switch.txt says on (off until the swap goes live). The
# P713 facts cover both states: the switch file, the README, the launcher with and without app\map\, and gate 4.

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

# cc-P674 (SoS swap S1): a shipped base map (app\map\base-<name>) in place of the player's files, and Felucca's copy.
# Copies a synthetic facet's three files to a folder under the shipped names, with one cell changed so its land differs
# from the "player's" (block 0, cell 0 becomes tile 9 at z 4: the data starts at 74, the block header is 4 bytes).
function New-ShippedBase([string]$facetDir, [string]$dir, [int]$tile = 9) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $m = [IO.File]::ReadAllBytes((Join-Path $facetDir 'map1LegacyMUL.uop'))
    [BitConverter]::GetBytes([uint16]$tile).CopyTo($m, 78); $m[80] = 4
    [IO.File]::WriteAllBytes((Join-Path $dir 'base-map1legacymul.uop'), $m)
    Copy-Item (Join-Path $facetDir 'statics1.mul') (Join-Path $dir 'base-statics1.mul')
    Copy-Item (Join-Path $facetDir 'staidx1.mul') (Join-Path $dir 'base-staidx1.mul')
    return $dir
}

Fact 'P674_AShippedBaseIsPatchedInsteadOfThePlayersFilesAndFeluccaGetsTheSameLand' {
    $ea = New-MapFacet (Join-Path $work 'ea7')
    $base = New-ShippedBase (New-MapFacet (Join-Path $work 'sos7')) (Join-Path $work 'app-map7')
    $ops = Join-Path $work 'seven.mapops'
    [IO.File]::WriteAllText($ops, "F 1 0`nL 10 20 581 -3`n", (New-Object Text.ASCIIEncoding))
    $out = Join-Path $work 'out7'
    $lines = @(Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out -BaseDir $base -AlsoAs 0)
    Assert-Equal 6 $lines.Count ($lines -join ' | ')
    Assert-Equal "map0legacymul.uop=$out\map0LegacyMUL.uop" $lines[3]
    Assert-Equal "statics0.mul=$out\statics1.mul" $lines[4] "Felucca reads Trammel's statics"
    Assert-Equal "staidx0.mul=$out\staidx1.mul" $lines[5]
    $m1 = [IO.File]::ReadAllBytes((Join-Path $out 'map1LegacyMUL.uop'))
    Assert-Equal 9 $m1[78] "the base's cell, not the player's tile 3"
    Assert-Equal 4 $m1[80]
    $o = 74 + 514 * 196 + 4 + 34 * 3
    Assert-Equal 69 $m1[$o] 'our patch on the base'
    $m0 = [IO.File]::ReadAllBytes((Join-Path $out 'map0LegacyMUL.uop'))
    Assert-Equal $m1.Length $m0.Length
    $diff = @(for ($i = 0; $i -lt $m1.Length; $i++) { if ($m1[$i] -ne $m0[$i]) { $i } })
    Assert-Equal 8 $diff.Count 'only the one entry name hash differs'
    Assert-Equal 60 $diff[0] 'the hash of the first entry: table at 28, 12-byte head, 20 bytes into the entry'
    Assert-Equal (Get-UopNameHash 'build/map0legacymul/00000000.dat') ([BitConverter]::ToUInt64($m0, 60))
    Assert-True (-not (Test-Path (Join-Path $out 'statics0.mul'))) 'no second statics file is written'
}

Fact 'P674_TheStampNamesTheBaseSoANewBaseRebuildsAndAnUnchangedOneDoesNot' {
    $ea = New-MapFacet (Join-Path $work 'ea8')
    $sos = New-MapFacet (Join-Path $work 'sos8')
    $base = New-ShippedBase $sos (Join-Path $work 'app-map8')
    $ops = Join-Path $work 'eight.mapops'
    [IO.File]::WriteAllText($ops, "F 1 0`nL 1 1 5 0`n", (New-Object Text.ASCIIEncoding))
    $out = Join-Path $work 'out8'
    $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out
    Assert-True (-not (Test-Path (Join-Path $out 'map0LegacyMUL.uop'))) 'no base: no Felucca copy (EA Felucca as before)'
    $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out -BaseDir $base -AlsoAs 0
    Assert-Equal 9 ([IO.File]::ReadAllBytes((Join-Path $out 'map1LegacyMUL.uop'))[78]) 'a base arriving rebuilds'
    $t0 = (Get-Item (Join-Path $out 'map0LegacyMUL.uop')).LastWriteTimeUtc
    Start-Sleep -Milliseconds 50
    $lines = @(Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out -BaseDir $base -AlsoAs 0)
    Assert-Equal 6 $lines.Count
    Assert-Equal $t0 (Get-Item (Join-Path $out 'map0LegacyMUL.uop')).LastWriteTimeUtc 'the same base: nothing rewritten'
    $null = New-ShippedBase $sos (Join-Path $work 'app-map8') 11
    $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out -BaseDir $base -AlsoAs 0
    Assert-Equal 11 ([IO.File]::ReadAllBytes((Join-Path $out 'map0LegacyMUL.uop'))[78]) 'a new base rebuilds Felucca too'
    Remove-Item -LiteralPath (Join-Path $out 'map0LegacyMUL.uop')
    $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir $out -BaseDir $base -AlsoAs 0
    Assert-True (Test-Path (Join-Path $out 'map0LegacyMUL.uop')) 'a missing Felucca copy is rebuilt'
}

Fact 'P674_AFeluccaCopyRefusesAFileThatIsNotTrammelsMapAndAFacetOfAnotherSize' {
    $ea = New-MapFacet (Join-Path $work 'ea9')
    $out = Join-Path $work 'out9'
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    Assert-Equal 1 (Copy-UopRenamed (Join-Path $ea 'map1LegacyMUL.uop') (Join-Path $out 'map0.uop') 1 0)
    $threw = $null
    try { $null = Copy-UopRenamed (Join-Path $out 'map0.uop') (Join-Path $out 'again.uop') 1 0 } catch { $threw = $_.Exception.Message }
    Assert-True ($threw -match 'not a map1 chunk') "a Felucca file is not Trammel's map: $threw"
    $ops = Join-Path $work 'nine.mapops'
    [IO.File]::WriteAllText($ops, "F 1 0`nL 1 1 5 0`n", (New-Object Text.ASCIIEncoding))
    $threw = $null
    try { $null = Build-MapOverride -UoDir $ea -Ops $ops -OutDir (Join-Path $work 'out9b') -AlsoAs 2 } catch { $threw = $_.Exception.Message }
    Assert-True ($threw -match 'cannot read') "Ilshenar is not Trammel's size: $threw"
}

# cc-P713: the Siege of Sosaria land behind one switch, vendor\sos-trammel-map\switch.txt, off until the swap goes live.
# Off, the package ships no app\map\ and the launcher patches the player's own stock Trammel exactly as before cc-P674;
# on, it ships the base. The build script's own functions are lifted out of it by name (it cannot be dot-sourced: it
# builds when run), and gate 4 runs as the build runs it, with -CheckZip and -SosMap.
$package = Join-Path $pkg 'Build-PlayerPackage.ps1'
$switch  = Join-Path $pkg 'vendor\sos-trammel-map\switch.txt'
function Import-BuildFunction([string]$name) {
    $ast = [Management.Automation.Language.Parser]::ParseFile($package, [ref]$null, [ref]$null)
    $fn = $ast.Find({ param($a) $a -is [Management.Automation.Language.FunctionDefinitionAst] -and $a.Name -eq $name }, $true)
    if (-not $fn) { throw "Build-PlayerPackage.ps1 has no function $name" }
    . ([scriptblock]::Create($fn.Extent.Text.Replace("function $name", "function global:$name")))
}

# A zip under one top folder holding the given entries (name -> text), and gate 4's output on it with -SosMap.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
function New-GateZip([string]$name, [hashtable]$entries) {
    $zip = Join-Path $work $name
    $fs = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
    $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($k in $entries.Keys) { $w = New-Object IO.StreamWriter(($za.CreateEntry("ShatteredLegacy/$k")).Open()); $w.Write($entries[$k]); $w.Dispose() }
    } finally { $za.Dispose(); $fs.Dispose() }
    return $zip
}
function Invoke-Gate4([string]$zip, [string]$sos) {
    $ea = Join-Path $work 'ea-names'
    if (-not (Test-Path $ea)) { New-Item -ItemType Directory -Path $ea -Force | Out-Null; [IO.File]::WriteAllBytes((Join-Path $ea 'tiledata.mul'), (New-Object byte[] 8)) }
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $package, '-CheckZip', $zip, '-UoData', $ea)
    if ($sos) { $a += @('-SosMap', $sos) }
    $ErrorActionPreference = 'Continue'
    $text = & powershell.exe @a 2>&1 | Out-String
    $ErrorActionPreference = 'Stop'
    Assert-True ($text -match 'GATE 4') "gate 4 ran: $text"
    return $text
}
$credit = "  app\map\     the land of Trammel and Felucca: `"Siege of Sosaria Trammel Map 3.0`"`r`n"

Fact 'P713_TheSwitchIsOffUntilTheSwapGoesLive' {
    Import-BuildFunction 'Get-SosMapSwitch'
    Assert-Equal 'off' (Get-SosMapSwitch $switch) 'switch.txt in the repo'
}

Fact 'P713_ASwitchThatIsNotOneWordOffOrOnStopsTheBuild' {
    Import-BuildFunction 'Get-SosMapSwitch'
    $f = Join-Path $work 'switch.txt'
    [IO.File]::WriteAllText($f, "# a comment`r`n  on  `r`n", (New-Object Text.ASCIIEncoding))
    Assert-Equal 'on' (Get-SosMapSwitch $f) 'comments and spaces are skipped'
    foreach ($bad in @("On`r`n", "yes`r`n", "off`r`non`r`n", "# nothing but a comment`r`n")) {
        [IO.File]::WriteAllText($f, $bad, (New-Object Text.ASCIIEncoding))
        $threw = $null
        try { $null = Get-SosMapSwitch $f } catch { $threw = $_.Exception.Message }
        Assert-True ($threw -match 'SOS MAP SWITCH') "refused [$($bad.Trim())]: $threw"
    }
    $threw = $null
    try { $null = Get-SosMapSwitch (Join-Path $work 'no-switch.txt') } catch { $threw = $_.Exception.Message }
    Assert-True ($threw -match 'missing') "a missing switch is refused: $threw"
}

Fact 'P713_SwitchedOffTheReadmeLosesOnlyItsAppMapEntry' {
    Import-BuildFunction 'Remove-SosMapReadme'
    $text = [IO.File]::ReadAllText((Join-Path $pkg 'payload\README.txt'))
    $off = Remove-SosMapReadme $text
    Assert-True ($text -match 'Siege of Sosaria Trammel Map') 'payload\README.txt credits the map (for a package with the switch on)'
    Assert-True ($off -notmatch 'Siege of Sosaria Trammel Map' -and $off -notmatch 'app\\map\\') 'the off README names neither'
    $gone = @($text -split "`n").Count - @($off -split "`n").Count
    Assert-Equal 5 $gone 'the entry is five lines'
    Assert-True ($off -match '(?m)^  app\\music\\ ' -and $off -match '(?m)^  app\\         the scripts') 'the entries around it stay'
    $threw = $null
    try { $null = Remove-SosMapReadme $off } catch { $threw = $_.Exception.Message }
    Assert-True ($threw -match 'no single app\\map\\ entry') "a README with no entry stops the build: $threw"
}

Fact 'P713_WithNoAppMapTheLauncherPatchesThePlayersOwnStockFilesAsBefore' {
    # The launcher's builder, as Play.ps1 runs it, with a package that has no app\map\ (switch off): three lines, our
    # patch on the player's own Trammel, no Felucca copy. With app\map\ (switch on): six, on the base.
    $ea = New-MapFacet (Join-Path $work 'ea10')
    $ops = Join-Path $work 'ten.mapops'
    [IO.File]::WriteAllText($ops, "F 1 0`nL 10 20 581 -3`n", (New-Object Text.ASCIIEncoding))
    $none = Join-Path $work 'no-such-file'
    $run = {
        param($out, $mapBase)
        $res = @(& $builder -UoDir $ea -OutDir $out -MapOps $ops -MapBase $mapBase -Records $none -Clilocs $none -Music $none -AnimMul $none -AnimIdx $none 6>$null)[-1]
        @([IO.File]::ReadAllLines($res) | Where-Object { $_ -and -not $_.StartsWith('#') })
    }
    $out = Join-Path $work 'out10'
    $lines = & $run $out (Join-Path $work 'no-app-map')
    Assert-Equal 3 $lines.Count ($lines -join ' | ')
    Assert-True (-not ($lines -match '^map0')) 'no Felucca line: EA Felucca as before'
    $m1 = [IO.File]::ReadAllBytes((Join-Path $out 'map1LegacyMUL.uop'))
    Assert-Equal 3 $m1[78] "the player's own tile"
    Assert-Equal 69 $m1[74 + 514 * 196 + 4 + 34 * 3] 'our patch on it'
    $base = New-ShippedBase (New-MapFacet (Join-Path $work 'sos10')) (Join-Path $work 'app-map10')
    $out2 = Join-Path $work 'out10b'
    $lines2 = & $run $out2 $base
    Assert-Equal 6 $lines2.Count ($lines2 -join ' | ')
    Assert-Equal 9 ([IO.File]::ReadAllBytes((Join-Path $out2 'map1LegacyMUL.uop'))[78]) "the base's tile"
}

Fact 'P713_Gate4SwitchedOffRefusesAnyAppMapFileAndTheCredit' {
    $zip = New-GateZip 'off-with-map.zip' @{ 'app/map/base-map1legacymul.uop' = 'x'; 'README.txt' = $credit }
    $text = Invoke-Gate4 $zip 'off'
    Assert-True ($text -match 'app/map/base-map1legacymul\.uop shipped, but the Siege of Sosaria map is switched off') $text
    Assert-True ($text -match 'README\.txt credits the Siege of Sosaria Trammel Map, which is switched off') $text
    Assert-True ($text -match 'app/map/base-map1legacymul\.uop is an EA data file type') 'no exemption with the switch off'
    $zip2 = New-GateZip 'off-clean.zip' @{ 'README.txt' = 'no map here' }
    $text2 = Invoke-Gate4 $zip2 ''
    Assert-True ($text2 -notmatch 'app/map|Siege of Sosaria') "the repo's switch (off): a zip with no app/map/ has no map line: $text2"
}

Fact 'P713_Gate4SwitchedOnWantsTheThreeFilesAndTheCredit' {
    $zip = New-GateZip 'on-empty.zip' @{ 'README.txt' = 'no map here' }
    $text = Invoke-Gate4 $zip 'on'
    foreach ($f in 'base-map1legacymul.uop', 'base-statics1.mul', 'base-staidx1.mul') {
        Assert-True ($text -match "no app/map/$([regex]::Escape($f)) \(the Siege of Sosaria map\)") "$f wanted: $text"
    }
    Assert-True ($text -match 'README\.txt does not credit the Siege of Sosaria Trammel Map') $text
    $zip2 = New-GateZip 'on-wrong.zip' @{ 'app/map/base-statics1.mul' = 'not his'; 'README.txt' = $credit }
    $text2 = Invoke-Gate4 $zip2 'on'
    Assert-True ($text2 -match 'app/map/base-statics1\.mul is not the file vendor\\sos-trammel-map\\manifest\.csv records') $text2
    Assert-True ($text2 -notmatch 'README\.txt (does not )?credit') "the credit is there: $text2"
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host "$($results.Count) facts, $failed failed" -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
