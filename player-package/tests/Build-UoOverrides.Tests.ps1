#Requires -Version 5.1
# Facts for payload\app\Build-UoOverrides.ps1 and the launch it feeds in payload\app\Play.ps1 (cc-P26, F-8).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Build-UoOverrides.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as scripts\tests\Shard-Console.Tests.ps1:
# a fact is a scriptblock that throws on failure.
#
# No EA file is read. Every tiledata.mul and animdata.mul here is synthetic, built by New-Tiledata and
# New-Animdata with the layout TazUO reads (TileDataLoader, AnimDataLoader), and the expected offsets are
# written out as numbers, not computed by the code under test. The records are the shipped ones,
# vendor\shattered-legacy-art\records.json: they are ours, and they are what players get.
#
# The last facts run the real Play.ps1 in a sandbox: a stub TazUO.exe that writes the command line it
# was given, a stub Setup.ps1 that names a fake Ultima Online folder, and -Test -NoUpdate. Play.ps1 checks
# the TEST shard's port (shatteredlegacyuo.com:2594) before it launches, so those facts need the test shard
# up; nothing else is sent to it.

$ErrorActionPreference = 'Stop'
$here     = $PSScriptRoot
$pkg      = Split-Path $here -Parent
$builder  = Join-Path $pkg 'payload\app\Build-UoOverrides.ps1'
$records  = Join-Path $pkg 'vendor\shattered-legacy-art\records.json'
$work     = Join-Path $env:TEMP ("sl-uo-overrides-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null

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

# 512 land groups (493,568 bytes), then $groups static groups of 4 + 32 x 41 bytes. Every byte is a
# filler pattern except where a test says otherwise, so "nothing else changed" means something.
function New-Tiledata([int]$groups = 500) {
    $n = 493568 + $groups * 1316
    $d = New-Object byte[] $n
    for ($i = 0; $i -lt 493568; $i += 7) { $d[$i] = 0x5A }
    # Static entries: flags 0 and empty names (free), a header word per group.
    for ($g = 0; $g -lt $groups; $g++) { $o = 493568 + $g * 1316; $d[$o] = 0xA5; $d[$o + 1] = 0x5A }
    return ,$d
}

# Entries of 68 bytes in blocks of 8 with a 4-byte header, enough for item $count - 1.
function New-Animdata([int]$count = 15616) {
    $n = [int]($count / 8) * (4 + 8 * 68)
    $d = New-Object byte[] $n
    for ($b = 0; $b -lt $count / 8; $b++) { $d[$b * 548] = 0xC3 }
    return ,$d
}

function New-UoDir([string]$name, [byte[]]$tile, [byte[]]$anim) {
    $dir = Join-Path $work $name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    if ($tile) { [IO.File]::WriteAllBytes((Join-Path $dir 'tiledata.mul'), $tile) }
    if ($anim) { [IO.File]::WriteAllBytes((Join-Path $dir 'animdata.mul'), $anim) }
    return $dir
}

# Runs the builder; returns its result and what it wrote to the host.
function Invoke-BuilderSaying([string]$uo, [string]$out, [string]$rec = $records) {
    $said = New-Object 'System.Collections.Generic.List[string]'
    $res = $null
    foreach ($x in @(& $builder -UoDir $uo -Records $rec -OutDir $out 6>&1)) {
        if ($x -is [Management.Automation.InformationRecord]) { $said.Add("$x") } else { $res = $x }
    }
    return [pscustomobject]@{ Result = $res; Said = ($said -join ' | ') }
}

function Get-Bytes([string]$path, [int]$offset, [int]$n) {
    $d = [IO.File]::ReadAllBytes($path)
    return ($d[$offset..($offset + $n - 1)] | ForEach-Object { '{0:X2}' -f $_ }) -join ' '
}

# Offsets of the four base IDs, worked out by hand from the layouts (Build-UoOverrides.ps1 header):
#   tiledata: 493568 + floor(id / 32) x 1316 + 4 + (id mod 32) x 41
#   animdata: id x 68 + 4 x (floor(id / 8) + 1)
$tileAt = @{ 15376 = 1125908; 15387 = 1126359; 15398 = 1126814; 15409 = 1127265 }
$animAt = @{ 15376 = 1053260; 15387 = 1054012; 15398 = 1054764; 15409 = 1055520 }
$tileExpected = '40 00 04 01 00 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 03 ' +
                '73 68 61 74 74 65 72 65 64 20 72 75 6E 65 73 74 6F 6E 65 00'   # flags, weight, layer, count, animId, hue, light, height, "shattered runestone"
$animExpected = '00 01 02 03 04 05 06 07 08 09 0A' + (' 00' * 53) + ' 00 0B 01 00'  # 11 offsets, 53 zeros, unknown, count 11, interval 1, start 0

Fact 'TheRecordsLandAsTheExpectedBytesAtTheExpectedOffsetsAndNothingElseChanges' {
    $tile = New-Tiledata; $anim = New-Animdata
    $uo = New-UoDir 'writer' $tile $anim
    $out = Join-Path $work 'writer-out'
    $r = Invoke-BuilderSaying $uo $out
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result
    Assert-Equal '' $r.Said 'a clean build says nothing'
    $t2 = [IO.File]::ReadAllBytes((Join-Path $out 'tiledata.mul'))
    $a2 = [IO.File]::ReadAllBytes((Join-Path $out 'animdata.mul'))
    Assert-Equal $tile.Length $t2.Length 'tiledata size'
    Assert-Equal $anim.Length $a2.Length 'animdata size'
    foreach ($id in $tileAt.Keys) {
        Assert-Equal $tileExpected (Get-Bytes (Join-Path $out 'tiledata.mul') $tileAt[$id] 41) "tiledata record $id"
        Assert-Equal $animExpected (Get-Bytes (Join-Path $out 'animdata.mul') $animAt[$id] 68) "animdata record $id"
    }
    $changedT = 0; for ($i = 0; $i -lt $tile.Length; $i++) { if ($tile[$i] -ne $t2[$i]) { $changedT++ } }
    $changedA = 0; for ($i = 0; $i -lt $anim.Length; $i++) { if ($anim[$i] -ne $a2[$i]) { $changedA++ } }
    Assert-Equal 96 $changedT 'tiledata: 4 records x 24 non-zero bytes, nothing else'
    Assert-Equal 48 $changedA 'animdata: 4 records x 12 non-zero bytes, nothing else'
    # The player's own files are untouched.
    Assert-Equal ((Get-FileHash -InputStream ([IO.MemoryStream]::new($tile))).Hash) (Get-FileHash (Join-Path $uo 'tiledata.mul')).Hash 'source tiledata'
    $list = [IO.File]::ReadAllLines((Join-Path $out 'files-override.txt'))
    Assert-True ($list -contains "tiledata.mul=$out\tiledata.mul") ($list -join ' | ')
    Assert-True ($list -contains "animdata.mul=$out\animdata.mul") ($list -join ' | ')
}

Fact 'AStoneWhoseSlotEAHasNamedIsSkippedAndSaidAndTheOthersAreWritten' {
    $tile = New-Tiledata; $anim = New-Animdata
    # Item 15390 (design 2, frame 4) named by "EA": 493568 + 480 x 1316 + 4 + 30 x 41 + 21.
    $o = 493568 + 480 * 1316 + 4 + 30 * 41
    $b = [Text.Encoding]::ASCII.GetBytes('new thing'); [Array]::Copy($b, 0, $tile, $o + 21, $b.Length)
    $uo = New-UoDir 'taken' $tile $anim
    $out = Join-Path $work 'taken-out'
    $r = Invoke-BuilderSaying $uo $out
    Assert-True ($r.Said -match "skipping item 15387: .*art ID 15390 is named 'new thing'") $r.Said
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result
    Assert-Equal ('00 ' * 40 + '00') (Get-Bytes (Join-Path $out 'tiledata.mul') $tileAt[15387] 41) 'design 2 not written'
    Assert-Equal $tileExpected (Get-Bytes (Join-Path $out 'tiledata.mul') $tileAt[15376] 41) 'design 1 written'
    Assert-Equal $tileExpected (Get-Bytes (Join-Path $out 'tiledata.mul') $tileAt[15409] 41) 'design 4 written'
}

Fact 'AStoneAnotherAnimationDrawsAsAFrameIsSkipped' {
    $tile = New-Tiledata; $anim = New-Animdata
    # Item 15370 animates with frames 0 and +8: it would draw 15378 (design 1, frame 3).
    $o = 15370 * 68 + 4 * ([Math]::Floor(15370 / 8) + 1)
    $anim[$o] = 0; $anim[$o + 1] = 8; $anim[$o + 65] = 2; $anim[$o + 66] = 1
    $uo = New-UoDir 'framed' $tile $anim
    $out = Join-Path $work 'framed-out'
    $r = Invoke-BuilderSaying $uo $out
    Assert-True ($r.Said -match 'skipping item 15376: .*art ID 15378 is a frame of another animation') $r.Said
    Assert-True ($r.Said -notmatch '15387|15398|15409') 'only design 1 skipped'
    Assert-Equal $animExpected (Get-Bytes (Join-Path $out 'animdata.mul') $animAt[15398] 68) 'design 3 written'
}

Fact 'NothingChangedSkipsTheRebuildFastAndAChangeOfEitherFileOrTheRecordsRebuilds' {
    $uo = New-UoDir 'stamp' (New-Tiledata) (New-Animdata)
    $out = Join-Path $work 'stamp-out'
    [void](Invoke-BuilderSaying $uo $out)
    $t0 = (Get-Item (Join-Path $out 'tiledata.mul')).LastWriteTimeUtc
    Start-Sleep -Milliseconds 50
    $ms = (Measure-Command { $r = Invoke-BuilderSaying $uo $out }).TotalMilliseconds
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result
    Assert-Equal $t0 (Get-Item (Join-Path $out 'tiledata.mul')).LastWriteTimeUtc 'not rewritten when nothing changed'
    Assert-True ($ms -lt 500) "the skip took $ms ms"

    # The player's tiledata changes (an EA patch): rebuilt.
    (Get-Item (Join-Path $uo 'tiledata.mul')).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(1)
    [void](Invoke-BuilderSaying $uo $out)
    $t1 = (Get-Item (Join-Path $out 'tiledata.mul')).LastWriteTimeUtc
    Assert-True ($t1 -gt $t0) 'rebuilt after the source changed'

    # New records in an update: rebuilt.
    $rec2 = Join-Path $work 'records2.json'
    [IO.File]::WriteAllText($rec2, ([IO.File]::ReadAllText($records) + ' '))
    Start-Sleep -Milliseconds 50
    [void](Invoke-BuilderSaying $uo $out $rec2)
    Assert-True ((Get-Item (Join-Path $out 'tiledata.mul')).LastWriteTimeUtc -gt $t1) 'rebuilt after the records changed'

    # A copy deleted by hand (T-row 3): rebuilt.
    Remove-Item -LiteralPath $out -Recurse -Force
    $r = Invoke-BuilderSaying $uo $out $rec2
    Assert-True (Test-Path (Join-Path $out 'animdata.mul')) 'rebuilt after the folder was deleted'
}

Fact 'AMissingFileAWrongSizeOrAFailedWriteGivesNoOverrideAndOneYellowLine' {
    $out = Join-Path $work 'fail-out'
    $r = Invoke-BuilderSaying (New-UoDir 'no-tile' $null (New-Animdata)) $out
    Assert-Equal $null $r.Result 'missing tiledata'
    Assert-True ($r.Said -match '^\s*Shattered Legacy art could not be prepared \(tiledata\.mul is not in .*\)\. Starting without it\.$') $r.Said

    $r = Invoke-BuilderSaying (New-UoDir 'odd-tile' ((New-Tiledata) + [byte[]](1, 2, 3)) (New-Animdata)) $out
    Assert-Equal $null $r.Result 'odd size'
    Assert-True ($r.Said -match 'not the layout of client 7\.0\.9\.0') $r.Said

    $blocked = Join-Path $work 'a-file-not-a-folder'
    [IO.File]::WriteAllText($blocked, 'x')
    $r = Invoke-BuilderSaying (New-UoDir 'ok' (New-Tiledata) (New-Animdata)) $blocked
    Assert-Equal $null $r.Result 'unwritable output'
    Assert-True ($r.Said -match 'Starting without it') $r.Said
}

Fact 'TheServersCopyOfTheRecordsIsRecordsJsonAndEveryIdIsRegistered' {
    # The server cannot read records.json (it is not in the image), so ShatteredRunestone.cs carries the same
    # values (ShatteredLegacyArt). This keeps the two from drifting.
    $cs  = [IO.File]::ReadAllText((Join-Path (Split-Path $pkg -Parent) 'server\customizations\ShatteredRunestone.cs'))
    $rec = [IO.File]::ReadAllText($records) | ConvertFrom-Json
    $reg = @(Get-Content (Join-Path $pkg 'vendor\shattered-legacy-art\registry.csv') | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv)
    $ids = [regex]::Match($cs, 'DesignItemIds = \{ ([^}]+) \}').Groups[1].Value.Split(',') | ForEach-Object { [Convert]::ToInt32($_.Trim(), 16) }
    Assert-Equal (@($rec.tiledata.id) -join ',') ($ids -join ',') 'design base IDs'
    Assert-Equal (@($rec.animdata.id) -join ',') ($ids -join ',') 'animdata IDs'
    Assert-True ($cs -match 'RunestoneFlags = TileFlag\.Animation \| TileFlag\.PartialHue \| TileFlag\.Impassable;') 'flags in C#'
    $w = [int][regex]::Match($cs, 'RunestoneWeight = (\d+);').Groups[1].Value
    $h = [int][regex]::Match($cs, 'RunestoneHeight = (\d+);').Groups[1].Value
    $n = [regex]::Match($cs, 'RunestoneName = "([^"]+)";').Groups[1].Value
    foreach ($t in $rec.tiledata) {
        Assert-Equal '0x01040040' $t.flags 'Animation 0x01000000 | PartialHue 0x00040000 | Impassable 0x40'
        Assert-Equal $w $t.weight 'weight'
        Assert-Equal $h $t.height 'height'
        Assert-Equal $n $t.name 'name'
    }
    foreach ($a in $rec.animdata) {
        foreach ($f in $a.frames) {
            Assert-True (@($reg | Where-Object { [int]$_.id -eq ([int]$a.id + [int]$f) }).Count -eq 1) "frame $([int]$a.id + [int]$f) is registered"
        }
    }
    Assert-Equal 44 $reg.Count 'registry rows'
}

# --- Play.ps1, end to end in a sandbox ------------------------------------------------------
$stubSrc = @'
using System; using System.IO;
public static class Stub { public static void Main() {
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "args.txt"), Environment.CommandLine);
} }
'@
$stubExe = Join-Path $work 'stub.exe'
Add-Type -TypeDefinition $stubSrc -OutputAssembly $stubExe -OutputType ConsoleApplication

function New-Sandbox([string]$name, [byte[]]$tile, [byte[]]$anim) {
    $root = Join-Path $work $name
    $app = Join-Path $root 'app'
    New-Item -ItemType Directory -Path (Join-Path $app 'tazuo') -Force | Out-Null
    Copy-Item (Join-Path $pkg 'payload\app\Play.ps1') $app
    Copy-Item $builder $app
    Copy-Item $records (Join-Path $app 'art-records.json')
    Copy-Item $stubExe (Join-Path $app 'tazuo\TazUO.exe')
    $uo = New-UoDir "$name-uo" $tile $anim
    Copy-Item $stubExe (Join-Path $uo 'client.exe')
    # Stub Setup.ps1: the fake Ultima Online folder is "found".
    [IO.File]::WriteAllText((Join-Path $app 'Setup.ps1'), "[IO.File]::WriteAllText((Join-Path `$PSScriptRoot 'uo-path.txt'), '$uo'); exit 0")
    return $root
}

function Invoke-Play([string]$root) {
    $argsFile = Join-Path $root 'app\tazuo\args.txt'
    if (Test-Path $argsFile) { Remove-Item $argsFile -Force }
    $o = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'app\Play.ps1') -Test -NoUpdate 2>&1 | Out-String
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path $argsFile) -and $sw.ElapsedMilliseconds -lt 10000) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path $argsFile)) { throw "the game was not started. Play.ps1 said: $o" }
    return [pscustomobject]@{ Out = $o; Args = [IO.File]::ReadAllText($argsFile) }
}

Fact 'PlayStartsTheGameWithTheOverrideWhenTheCopiesAreBuilt' {
    $root = New-Sandbox 'play-ok' (New-Tiledata) (New-Animdata)
    $r = Invoke-Play $root
    $list = Join-Path $root 'app\uo-overrides\files-override.txt'
    Assert-True ($r.Args -match ('-settings "[^"]+settings\.test\.json" -uofilesoverride "' + [regex]::Escape($list) + '"')) $r.Args
    Assert-True ($r.Out -notmatch 'could not be prepared') $r.Out

    # Deleting the folder and starting again rebuilds it (T-row 3).
    Remove-Item (Join-Path $root 'app\uo-overrides') -Recurse -Force
    $r = Invoke-Play $root
    Assert-True (Test-Path $list) 'rebuilt'
    Assert-True ($r.Args -match '-uofilesoverride') $r.Args
}

Fact 'PlayStartsTheGameWithoutTheOverrideWhenAFileIsMissing' {
    $root = New-Sandbox 'play-missing' $null (New-Animdata)
    $r = Invoke-Play $root
    Assert-True ($r.Args -match '-settings "[^"]+settings\.test\.json"\s*$') "started with: $($r.Args)"
    Assert-True ($r.Args -notmatch 'uofilesoverride') $r.Args
    Assert-True ($r.Out -match 'Shattered Legacy art could not be prepared \(tiledata\.mul is not in') $r.Out
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
