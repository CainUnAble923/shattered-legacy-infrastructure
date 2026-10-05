#Requires -Version 5.1
# Facts for the client text half of payload\app\Build-UoOverrides.ps1 (cc-P57 Part D): our own clilocs added to a copy
# of the player's Cliloc.enu, and the package's cliloc gate (Build-PlayerPackage.ps1, Test-ClilocEntries).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Cliloc-Overrides.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Build-UoOverrides.Tests.ps1.
#
# Every Cliloc.enu written here is synthetic, built by New-Cliloc in the layout TazUO reads (ClilocLoader.ReadCliloc:
# int32, int16, then int32 number, byte flag, uint16 length, UTF-8 text), except the one fact that reads EA's real
# file, which it only reads (its hash is checked before and after) and which it skips, saying so, when the file is not
# on this machine. The entries are the shipped ones, vendor\shattered-legacy-cliloc\entries.json.
#
# The last fact runs the real Play.ps1 in a sandbox, as Build-UoOverrides.Tests.ps1 does, so it needs the test shard up.

$ErrorActionPreference = 'Stop'
$here     = $PSScriptRoot
$pkg      = Split-Path $here -Parent
$builder  = Join-Path $pkg 'payload\app\Build-UoOverrides.ps1'
$entries  = Join-Path $pkg 'vendor\shattered-legacy-cliloc\entries.json'
$registry = Join-Path $pkg 'vendor\shattered-legacy-cliloc\registry.csv'
$records  = Join-Path $pkg 'vendor\shattered-legacy-art\records.json'
$eaDir    = 'D:\UO\UltimaOnlineVanilla'
$work     = Join-Path $env:TEMP ("sl-cliloc-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
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

# A Cliloc.enu table, uncompressed: header 2, 1, then each pair of number and text.
function New-Cliloc([object[]]$pairs) {
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter($ms)
    $w.Write([int]2); $w.Write([int16]1)
    foreach ($p in $pairs) {
        $b = [Text.Encoding]::UTF8.GetBytes([string]$p[1])
        $w.Write([int]$p[0]); $w.Write([byte]0); $w.Write([uint16]$b.Length); $w.Write($b)
    }
    $w.Flush()
    return ,$ms.ToArray()
}

# Reads an uncompressed table here, independently of the builder's own reader.
function Read-Cliloc([byte[]]$d) {
    $out = New-Object 'System.Collections.Generic.List[object]'
    $o = 6
    while ($o -lt $d.Length) {
        $n = [BitConverter]::ToInt32($d, $o); $len = [BitConverter]::ToUInt16($d, $o + 5)
        $out.Add(@($n, [Text.Encoding]::UTF8.GetString($d, $o + 7, $len)))
        $o += 7 + $len
    }
    return ,$out
}

function New-UoDir([string]$name, [byte[]]$cliloc) {
    $dir = Join-Path $work $name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    if ($cliloc) { [IO.File]::WriteAllBytes((Join-Path $dir 'Cliloc.enu'), $cliloc) }
    return $dir
}

function Invoke-BuilderSaying([string]$uo, [string]$out, [string]$ent = $entries, [string]$rec = (Join-Path $work 'no-records.json')) {
    $said = New-Object 'System.Collections.Generic.List[string]'
    $res = $null
    foreach ($x in @(& $builder -UoDir $uo -Records $rec -Clilocs $ent -OutDir $out 6>&1)) {
        if ($x -is [Management.Automation.InformationRecord]) { $said.Add("$x") } else { $res = $x }
    }
    return [pscustomobject]@{ Result = $res; Said = ($said -join ' | ') }
}

$ours = @(([IO.File]::ReadAllText($entries) | ConvertFrom-Json).entries)
$sample = @(@(500000, 'Five hundred thousand'), @(1166081, 'Last below the block'), @(3000000, 'Three million'),
            @(3006132, 'Use'), @(3011032, 'The last'))

# ------------------------------------------------------------------------------------- read, add, write

Fact 'OurEntriesAreAddedInNumberOrderAndEveryOtherByteIsTheSame' {
    $src = New-Cliloc $sample
    $uo = New-UoDir 'round' $src
    $out = Join-Path $work 'round-out'
    $r = Invoke-BuilderSaying $uo $out
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result
    Assert-Equal '' $r.Said 'a clean build says nothing'
    $d = [IO.File]::ReadAllBytes((Join-Path $out 'Cliloc.enu'))

    # Uncompressed, as TazUO reads it: decompressed only when the fourth byte is 0x8E.
    Assert-Equal '02 00 00 00 01 00' ((0..5 | ForEach-Object { '{0:X2}' -f $d[$_] }) -join ' ')
    $got = Read-Cliloc $d
    $want = @($sample[0], $sample[1]) + @($ours | ForEach-Object { , @([int]$_.number, "$($_.text)") }) + @($sample[2], $sample[3], $sample[4])
    Assert-Equal (($want | ForEach-Object { "$($_[0])=$($_[1])" }) -join '; ') (($got | ForEach-Object { "$($_[0])=$($_[1])" }) -join '; ')

    # The bytes before and after our entries are the source's own.
    $before = (New-Cliloc @($sample[0], $sample[1])).Length
    $oursBytes = 0; foreach ($e in $ours) { $oursBytes += 7 + [Text.Encoding]::UTF8.GetByteCount("$($e.text)") }
    Assert-Equal ($src.Length + $oursBytes) $d.Length 'the source plus our entries'
    for ($i = 0; $i -lt $before; $i++) { if ($d[$i] -ne $src[$i]) { throw "byte $i differs before our entries" } }
    for ($i = $before; $i -lt $src.Length; $i++) { if ($d[$i + $oursBytes] -ne $src[$i]) { throw "byte $i differs after our entries" } }

    # The player's file is untouched, and the list names the copy.
    Assert-Equal ((Get-FileHash -InputStream ([IO.MemoryStream]::new($src))).Hash) (Get-FileHash (Join-Path $uo 'Cliloc.enu')).Hash 'source'
    Assert-True ([IO.File]::ReadAllLines((Join-Path $out 'files-override.txt')) -contains "cliloc.enu=$out\Cliloc.enu") 'the override line'
}

Fact 'ANumberThePlayersFileAlreadyUsesIsSkippedAndSaidAndTheOthersAreAdded' {
    $taken = [int]$ours[1].number
    $pairs = @(); $pairs += , $sample[0]; $pairs += , @($taken, 'Something of EA'); $pairs += , $sample[2]
    $uo = New-UoDir 'taken' (New-Cliloc $pairs)
    $out = Join-Path $work 'taken-out'
    $r = Invoke-BuilderSaying $uo $out
    Assert-True ($r.Said -match "skipping entry ${taken}: your Ultima Online files now use that number") $r.Said
    $got = Read-Cliloc ([IO.File]::ReadAllBytes((Join-Path $out 'Cliloc.enu')))
    Assert-Equal 'Something of EA' (@($got | Where-Object { $_[0] -eq $taken })[0][1]) 'EA keeps its number'
    Assert-Equal 1 (@($got | Where-Object { $_[0] -eq $taken }).Count) 'once'
    foreach ($e in $ours | Where-Object { [int]$_.number -ne $taken }) {
        Assert-Equal "$($e.text)" (@($got | Where-Object { $_[0] -eq [int]$e.number })[0][1]) "entry $($e.number)"
    }
}

Fact 'NothingChangedSkipsTheRebuildFastAndAChangedFileOrNewEntriesRebuild' {
    $uo = New-UoDir 'stamp' (New-Cliloc $sample)
    $out = Join-Path $work 'stamp-out'
    [void](Invoke-BuilderSaying $uo $out)
    $t0 = (Get-Item (Join-Path $out 'Cliloc.enu')).LastWriteTimeUtc
    Start-Sleep -Milliseconds 50
    $ms = (Measure-Command { $r = Invoke-BuilderSaying $uo $out }).TotalMilliseconds
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result
    Assert-Equal $t0 (Get-Item (Join-Path $out 'Cliloc.enu')).LastWriteTimeUtc 'not rewritten when nothing changed'
    Assert-True ($ms -lt 500) "the skip took $ms ms"

    (Get-Item (Join-Path $uo 'Cliloc.enu')).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(1)   # an EA patch
    [void](Invoke-BuilderSaying $uo $out)
    $t1 = (Get-Item (Join-Path $out 'Cliloc.enu')).LastWriteTimeUtc
    Assert-True ($t1 -gt $t0) 'rebuilt after the source changed'

    $ent2 = Join-Path $work 'entries2.json'
    [IO.File]::WriteAllText($ent2, ([IO.File]::ReadAllText($entries) + ' '))
    Start-Sleep -Milliseconds 50
    [void](Invoke-BuilderSaying $uo $out $ent2)
    Assert-True ((Get-Item (Join-Path $out 'Cliloc.enu')).LastWriteTimeUtc -gt $t1) 'rebuilt after the entries changed'
}

Fact 'AMissingClilocIsOneYellowLineAndTheArtStillShips' {
    # The two halves are independent: no Cliloc.enu here, art present (synthetic files, as Build-UoOverrides.Tests.ps1).
    $uo = New-UoDir 'no-cliloc' $null
    $tile = New-Object byte[] (493568 + 500 * 1316)
    $anim = New-Object byte[] ([int](15616 / 8) * (4 + 8 * 68))
    [IO.File]::WriteAllBytes((Join-Path $uo 'tiledata.mul'), $tile)
    [IO.File]::WriteAllBytes((Join-Path $uo 'animdata.mul'), $anim)
    $out = Join-Path $work 'no-cliloc-out'
    $r = Invoke-BuilderSaying $uo $out $entries $records
    Assert-True ($r.Said -match '^\s*Shattered Legacy text could not be prepared \(Cliloc\.enu is not in .*\)\. Starting without it\.$') $r.Said
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result 'the art still gets its list'
    $list = [IO.File]::ReadAllLines((Join-Path $out 'files-override.txt'))
    Assert-True ($list -contains "tiledata.mul=$out\tiledata.mul") ($list -join ' | ')
    Assert-True (-not ($list -match '^cliloc\.enu=')) ($list -join ' | ')

    # And a Cliloc.enu that is not one: the same, with the reason.
    $uo2 = New-UoDir 'bad-cliloc' ([byte[]](1, 2, 3, 4, 5, 6, 7, 8))
    $r = Invoke-BuilderSaying $uo2 (Join-Path $work 'bad-cliloc-out')
    Assert-True ($r.Said -match 'Shattered Legacy text could not be prepared \(.*cliloc header') $r.Said
    Assert-Equal $null $r.Result 'nothing to list'
}

Fact 'EAsOwnClilocIsReadWithoutChangeAndOurEntriesAreAdded' {
    $ea = Join-Path $eaDir 'Cliloc.enu'
    if (-not (Test-Path -LiteralPath $ea)) { Write-Host "        SKIP: no $ea on this machine" -ForegroundColor Yellow; return }
    $h0 = (Get-FileHash -LiteralPath $ea).Hash
    $out = Join-Path $work 'ea-out'
    $ms = (Measure-Command { $r = Invoke-BuilderSaying $eaDir $out }).TotalMilliseconds
    Assert-Equal $h0 (Get-FileHash -LiteralPath $ea).Hash 'EA file unchanged'
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result
    $src = [ShatteredLegacy.Cliloc]::Read([ShatteredLegacy.Cliloc]::Table([IO.File]::ReadAllBytes($ea)))
    $got = Read-Cliloc ([IO.File]::ReadAllBytes((Join-Path $out 'Cliloc.enu')))
    Write-Host "        EA's Cliloc.enu: $($src.Count) entries; ours: $($got.Count) ($([int]$ms) ms)" -ForegroundColor Gray
    Assert-True ([ShatteredLegacy.Cliloc]::IsCompressed([IO.File]::ReadAllBytes($ea))) 'EA ships it compressed'
    Assert-Equal ($src.Count + $ours.Count) $got.Count
    # Every one of EA's entries is in the copy, the same; the copy is in number order.
    $map = @{}; foreach ($e in $got) { $map[$e[0]] = $e[1] }
    foreach ($e in $src) { if ($map[$e.Key] -cne $e.Value) { throw "EA entry $($e.Key) differs in the copy" } }
    for ($i = 1; $i -lt $got.Count; $i++) { if ($got[$i][0] -le $got[$i - 1][0]) { throw "out of order at $($got[$i][0])" } }
    foreach ($n in 3006132, 3006143, 1112530, 3006152) { Assert-True ($map.ContainsKey($n)) "EA's $n" }
    Assert-Equal 'Use' $map[3006132]
    foreach ($e in $ours) { Assert-Equal "$($e.text)" $map[[int]$e.number] "ours $($e.number)" }
}

# ------------------------------------------------------------------------------------- the cliloc gate

$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $pkg 'Build-PlayerPackage.ps1'), [ref]$null, [ref]$null)
$fn = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Test-ClilocEntries' }, $true)
$slClilocRegistry = @(Get-Content -LiteralPath $registry | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv)
if ($fn) { . ([scriptblock]::Create($fn.Extent.Text)) }

Fact 'TheShippedEntriesPassTheClilocGate' {
    Assert-True ($null -ne $fn) 'Build-PlayerPackage.ps1 has Test-ClilocEntries'
    $f = @(Test-ClilocEntries ([IO.File]::ReadAllBytes($entries)) 'entries.json')
    Assert-Equal 0 $f.Count ($f -join '; ')
}

Fact 'TheClilocGateFailsEachBadEntriesFile' {
    $good = [IO.File]::ReadAllText($entries)
    $cases = @(
        @('a number outside the block', $good.Replace('1900002', '1200002'), 'outside our block'),
        @('EA''s own number', $good.Replace('1900002', '3006132'), 'outside our block'),
        @('a non-ASCII text', $good.Replace('"Breed"', ('"Br' + [char]0xE9 + 'ed"')), 'not plain ASCII'),
        @('a byte-order mark', ([string][char]0xFEFF + $good), 'not plain ASCII'),
        @('a number twice', $good.Replace('1900001', '1900000'), 'twice'),
        @('a text the registry does not have', $good.Replace('"Smelt Ore"', '"Smelt"'), 'registry.csv says'),
        @('an unregistered number', $good.Replace('1900002', '1900099'), 'does not list once'),
        @('no entries', '{ "block": { "first": 1900000, "last": 1999999 }, "entries": [] }', 'no entries'),
        # cc-P61 Part C: a registered number that does not ship (the Celestial deed line left out).
        @('a registered number missing', ($good -replace ',\s*\{ "number": 1900010, "text": "[^"]*" \}', ''), 'lacks 1900010')
    )
    foreach ($c in $cases) {
        $bytes = [Text.Encoding]::UTF8.GetBytes($c[1])
        $f = @(Test-ClilocEntries $bytes 'x')
        Assert-True (($f -join ' ') -match $c[2]) "$($c[0]): got [$($f -join '; ')]"
    }
}

Fact 'TheServerAndThePackageAgreeOnEveryNumber' {
    $cs = [IO.File]::ReadAllText((Join-Path (Split-Path $pkg -Parent) 'server\customizations\ShardClilocs.cs'))
    $num = { param($name) [int]([regex]::Match($cs, "public const int $name\s*=\s*([\d_]+);").Groups[1].Value -replace '_', '') }
    Assert-Equal 1900000 (& $num 'BlockFirst')
    Assert-Equal 1999999 (& $num 'BlockLast')
    $want = @{ 'Breed' = (& $num 'Breed'); 'Smelt Ore' = (& $num 'SmeltOre'); 'Metal Familiarity' = (& $num 'MetalFamiliarity') }
    # cc-P61 Part C: the post-Valorite deed lines, ShardClilocs.<Metal>Ingots.
    foreach ($m in 'Platinum', 'Toxic', 'Blaze', 'Frost', 'Obsidian', 'Mythril', 'Adamantium', 'Celestial') {
        $want["All items must be made with $m ingots."] = (& $num "$($m)Ingots")
    }
    Assert-Equal $want.Count $ours.Count 'one entry per server constant'
    foreach ($e in $ours) { Assert-Equal $want["$($e.text)"] ([int]$e.number) "$($e.text)" }
    Assert-Equal $ours.Count $slClilocRegistry.Count 'registry rows'
}

Fact 'ThePostValoriteDeedLinesAreEAsValoriteLineWithTheMetalNamed' {
    # cc-P61 Part C (D85): EA's line for Valorite (1045149), the metal in Title Case (D-114), numbers 1900003-1900010 in
    # the metals' order. The wording is checked against EA's own file when it is on this machine.
    $metals = 'Platinum', 'Toxic', 'Blaze', 'Frost', 'Obsidian', 'Mythril', 'Adamantium', 'Celestial'
    $ea = Join-Path $eaDir 'Cliloc.enu'
    $valorite = 'All items must be made with valorite ingots.'
    if (Test-Path -LiteralPath $ea) {
        $src = [ShatteredLegacy.Cliloc]::Read([ShatteredLegacy.Cliloc]::Table([IO.File]::ReadAllBytes($ea)))
        $valorite = @($src | Where-Object { $_.Key -eq 1045149 })[0].Value
        Write-Host "        EA's 1045149: $valorite" -ForegroundColor Gray
    } else { Write-Host "        (no ${ea}: EA's line as read on 2026-10-05)" -ForegroundColor Yellow }
    for ($i = 0; $i -lt $metals.Count; $i++) {
        $e = @($ours | Where-Object { [int]$_.number -eq 1900003 + $i })
        Assert-Equal 1 $e.Count "1900003 + $i once"
        Assert-Equal ($valorite -creplace 'valorite', $metals[$i]) "$($e[0].text)" "$($metals[$i])"
    }
}

# ------------------------------------------------------------------------------------- Play.ps1, end to end

$stubSrc = @'
using System; using System.IO;
public static class Stub { public static void Main() {
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "args.txt"), Environment.CommandLine);
} }
'@
$stubExe = Join-Path $work 'stub.exe'
Add-Type -TypeDefinition $stubSrc -OutputAssembly $stubExe -OutputType ConsoleApplication

Fact 'PlayStartsTheGameWithTheTextOverride' {
    $root = Join-Path $work 'play'
    $app = Join-Path $root 'app'
    New-Item -ItemType Directory -Path (Join-Path $app 'tazuo') -Force | Out-Null
    Copy-Item (Join-Path $pkg 'payload\app\Play.ps1') $app
    Copy-Item (Join-Path $pkg 'payload\app\Launch-Settings.ps1') $app
    Copy-Item $builder $app
    Copy-Item $entries (Join-Path $app 'cliloc-entries.json')
    Copy-Item $stubExe (Join-Path $app 'tazuo\TazUO.exe')
    $uo = New-UoDir 'play-uo' (New-Cliloc $sample)
    Copy-Item $stubExe (Join-Path $uo 'client.exe')
    [IO.File]::WriteAllText((Join-Path $app 'Setup.ps1'), "[IO.File]::WriteAllText((Join-Path `$PSScriptRoot 'uo-path.txt'), '$uo'); exit 0")

    $argsFile = Join-Path $app 'tazuo\args.txt'
    $o = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $app 'Play.ps1') -Test -NoUpdate 2>&1 | Out-String
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path $argsFile) -and $sw.ElapsedMilliseconds -lt 10000) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path $argsFile)) { throw "the game was not started. Play.ps1 said: $o" }
    $list = Join-Path $app 'uo-overrides\files-override.txt'
    Assert-True ([IO.File]::ReadAllText($argsFile) -match ('-uofilesoverride "' + [regex]::Escape($list) + '"')) ([IO.File]::ReadAllText($argsFile))
    Assert-True ([IO.File]::ReadAllLines($list) -contains "cliloc.enu=$app\uo-overrides\Cliloc.enu") ([IO.File]::ReadAllText($list))
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
