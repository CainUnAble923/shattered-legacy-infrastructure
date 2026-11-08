#Requires -Version 5.1
# Facts for the creation screen's professions (cc-P817, Chase 2026-11-07: "I also want to remove the trade choice"): the
# Prof.txt part of payload\app\Build-UoOverrides.ps1, which writes a copy of the player's own Prof.txt without the
# professions the server refuses at creation, and its agreement with the server's list
# (server\customizations\ClusterFStartChoices.cs, RemovedProfessions).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Prof-Overrides.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Music-Overrides.Tests.ps1.
#
# No EA file is read. The Prof.txt here is made up in the client's shape (Begin ... End blocks, tab separated, CRLF).

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$pkg     = Split-Path $here -Parent
$repo    = Split-Path $pkg -Parent
$builder = Join-Path $pkg 'payload\app\Build-UoOverrides.ps1'
$startCs = Join-Path $repo 'server\customizations\ClusterFStartChoices.cs'
$work    = Join-Path $env:TEMP ("sl-prof-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
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

# One profession block in the client's shape.
function Block([string]$name, [int]$desc) {
    "Begin`r`n`tName`t`t`t$name`r`n`tTrueName`t`t`"$name`"`r`n`tDesc`t`t`t$desc`r`n`tTopLevel`t`ttrue`r`n`tType`t`t`tProfession`r`n`tSkill`t`t`tTactics`t`t30`r`nEnd`r`n`r`n"
}

$stockNames = @(@('Samurai', 6), @('Ninja', 7), @('Paladin', 5), @('Necromancer', 4), @('Warrior', 1), @('Mage', 2), @('Blacksmith', 3))
$header = "# made up for the tests: the professions on the creation screen`r`n`r`n"

function New-UoDir([string]$name, [object[]]$profs = $stockNames, [switch]$NoProf) {
    $dir = Join-Path $work $name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    if (-not $NoProf) {
        $text = $header + (($profs | ForEach-Object { Block $_[0] $_[1] }) -join '')
        [IO.File]::WriteAllText((Join-Path $dir 'Prof.txt'), $text, (New-Object Text.ASCIIEncoding))
    }
    return $dir
}

function Invoke-Builder([string]$uo, [string]$out) {
    $none = Join-Path $work 'no-such-file'
    $res = $null
    foreach ($x in @(& $builder -UoDir $uo -OutDir $out -Music $none -Records $none -Clilocs $none -MapOps $none -AnimMul $none -AnimIdx $none 6>&1)) {
        if ($x -isnot [Management.Automation.InformationRecord]) { $res = $x }
    }
    $lines = @()
    if ($res -and (Test-Path -LiteralPath $res)) { $lines = @([IO.File]::ReadAllLines($res) | Where-Object { $_ -and -not $_.StartsWith('#') }) }
    return [pscustomobject]@{ Result = $res; Lines = $lines }
}

Fact 'TheCopyLeavesOutTheRefusedProfessionsAndKeepsEverythingElseByteForByte' {
    $uo = New-UoDir 'stock'
    $out = Join-Path $work 'out-stock'
    $r = Invoke-Builder $uo $out
    $copy = Join-Path $out 'Prof.txt'
    Assert-True (Test-Path -LiteralPath $copy) 'the copy is written'
    Assert-Equal "prof.txt=$copy" (@($r.Lines | Where-Object { $_ -like 'prof.txt=*' }) -join '') 'the list names it'
    $want = $header + (Block 'Warrior' 1) + (Block 'Mage' 2)
    Assert-Equal $want ([IO.File]::ReadAllText($copy)) 'Warrior and Mage, as they were; the trade (Blacksmith) and the four schools gone'
    $orig = [IO.File]::ReadAllText((Join-Path $uo 'Prof.txt'))
    Assert-True ($orig -match 'Blacksmith') 'the player''s own file is untouched'
}

Fact 'ASecondRunWithNothingChangedKeepsTheCopy' {
    $uo = New-UoDir 'twice'
    $out = Join-Path $work 'out-twice'
    [void](Invoke-Builder $uo $out)
    $copy = Join-Path $out 'Prof.txt'
    $before = (Get-Item -LiteralPath $copy).LastWriteTimeUtc
    Start-Sleep -Milliseconds 1100
    $r = Invoke-Builder $uo $out
    Assert-Equal $before (Get-Item -LiteralPath $copy).LastWriteTimeUtc 'not rewritten'
    Assert-True (@($r.Lines | Where-Object { $_ -like 'prof.txt=*' }).Count -eq 1) 'still listed'
}

Fact 'AProfTxtWithNothingToTakeOutAndNoProfTxtGiveNoLine' {
    $r1 = Invoke-Builder (New-UoDir 'clean' @(@('Warrior', 1), @('Mage', 2))) (Join-Path $work 'out-clean')
    Assert-Equal 0 @($r1.Lines | Where-Object { $_ -like 'prof.txt=*' }).Count 'the player''s own file serves as it is'
    $r2 = Invoke-Builder (New-UoDir 'none' -NoProf) (Join-Path $work 'out-none')
    Assert-Equal 0 @($r2.Lines | Where-Object { $_ -like 'prof.txt=*' }).Count 'no Prof.txt, no line'
}

Fact 'TheScreenLeavesOutExactlyWhatTheServerRefuses' {
    $ps = [IO.File]::ReadAllText($builder)
    $client = @([regex]::Match($ps, '\$ProfRemoved = @\(([^)]*)\)').Groups[1].Value.Split(',') | ForEach-Object { [int]$_.Trim() } | Sort-Object)
    $cs = [IO.File]::ReadAllText($startCs)
    $ids = @{}
    foreach ($m in [regex]::Matches($cs, 'public const int (\w+)Id = (\d+);')) { $ids[$m.Groups[1].Value] = [int]$m.Groups[2].Value }
    $names = [regex]::Match($cs, 'RemovedProfessions = \[([^\]]*)\]').Groups[1].Value.Split(',') | ForEach-Object { $_.Trim() -replace 'Id$', '' }
    $server = @($names | ForEach-Object { $ids[$_] } | Sort-Object)
    Assert-Equal ($server -join ',') ($client -join ',') 'Build-UoOverrides.ps1 and ClusterFStartChoices.cs'
    Assert-True ($server -contains 3) 'the Maker (the client''s Blacksmith, 3) is refused: the trade choice'
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
