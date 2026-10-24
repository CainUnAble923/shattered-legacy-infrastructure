#Requires -Version 5.1
# Facts for payload\app\Overrides-Launch.ps1 and the parts of Build-UoOverrides.ps1, Update.ps1 it leans on
# (cc-P411: the launcher must always start the game).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Launch-Overrides.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Build-UoOverrides.Tests.ps1. Each fact builds an
# install in a temp folder: the package's own scripts, the shipped art records, and a synthetic Ultima Online folder
# (tiledata.mul and animdata.mul in TazUO's layouts, as Build-UoOverrides.Tests.ps1 makes them; no EA file is read).
# Only the art part is exercised: it is the part that hung on the work PC. A slow rebuild is made with the builder's
# test hook SL_OVERRIDES_SLOW=<step>:<seconds>.

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$pkg     = Split-Path $here -Parent
$appSrc  = Join-Path $pkg 'payload\app'
$records = Join-Path $pkg 'vendor\shattered-legacy-art\records.json'
$work    = Join-Path $env:TEMP ("sl-launch-overrides-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null
Remove-Item Env:SL_OVERRIDES_SLOW, Env:SL_OVERRIDES_LIMIT_SEC -ErrorAction SilentlyContinue

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
    } finally {
        Remove-Item Env:SL_OVERRIDES_SLOW, Env:SL_OVERRIDES_LIMIT_SEC, Env:SL_UPDATE_NO_PLAY -ErrorAction SilentlyContinue
    }
}
function Assert-Equal { param($Expected, $Actual, [string]$Because = '') if ($Expected -ne $Actual) { throw "expected [$Expected] but got [$Actual]. $Because" } }
function Assert-True { param($Condition, [string]$Because) if (-not $Condition) { throw "not true: $Because" } }

# Synthetic files, as Build-UoOverrides.Tests.ps1's New-Tiledata and New-Animdata (1,460 static groups, 46,720 entries).
function New-UoDir([string]$name) {
    $dir = Join-Path $work $name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $t = New-Object byte[] (493568 + 1460 * 1316)
    for ($i = 0; $i -lt 493568; $i += 7) { $t[$i] = 0x5A }
    for ($g = 0; $g -lt 1460; $g++) { $o = 493568 + $g * 1316; $t[$o] = 0xA5; $t[$o + 1] = 0x5A }
    $a = New-Object byte[] ([int](46720 / 8) * (4 + 8 * 68))
    for ($b = 0; $b -lt 46720 / 8; $b++) { $a[$b * 548] = 0xC3 }
    [IO.File]::WriteAllBytes((Join-Path $dir 'tiledata.mul'), $t)
    [IO.File]::WriteAllBytes((Join-Path $dir 'animdata.mul'), $a)
    return $dir
}

# An install: app\ with the package's scripts and the shipped art records.
function New-Install([string]$name) {
    $app = Join-Path (Join-Path $work $name) 'app'
    New-Item -ItemType Directory -Path $app -Force | Out-Null
    Copy-Item (Join-Path $appSrc '*.ps1') $app
    Copy-Item $records (Join-Path $app 'art-records.json')
    return $app
}

# Changes the records, so the stamp is stale (what a new package with new art does).
function Set-NewRecords([string]$app) { $f = Join-Path $app 'art-records.json'; [IO.File]::WriteAllText($f, [IO.File]::ReadAllText($f) + ' ') }

function Get-Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }

# Invoke-UoOverrides in a fresh PowerShell, so each call is a launch of its own. Returns the result, what it said, the
# progress it showed ("<seconds> <status>" lines) and how long it took.
function Invoke-Prepare([string]$app, [string]$uo, [int]$limit) {
    $script = Join-Path $work ('prepare-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.ps1')
    [IO.File]::WriteAllText($script, @"
`$ErrorActionPreference = 'Stop'
. '$app\Overrides-Launch.ps1'
`$r = Invoke-UoOverrides -App '$app' -UoDir '$uo' -LimitSeconds $limit
'RESULT=' + `$r
foreach (`$l in `$script:OverridesProgressShown) { 'SHOWN=' + `$l }
"@)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $o = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script 2>&1 | ForEach-Object { "$_" })
    $sec = $sw.Elapsed.TotalSeconds
    [pscustomobject]@{
        Result  = (@($o | Where-Object { $_ -like 'RESULT=*' }) | Select-Object -First 1) -replace '^RESULT=', ''
        Shown   = @($o | Where-Object { $_ -like 'SHOWN=*' } | ForEach-Object { $_ -replace '^SHOWN=', '' })
        Said    = (@($o | Where-Object { $_ -notlike 'RESULT=*' -and $_ -notlike 'SHOWN=*' }) -join ' | ')
        Seconds = $sec
    }
}

function Wait-Builder([string]$app, [int]$seconds = 60) {
    $pf = Join-Path $app 'uo-overrides.next\builder.pid'
    if (-not (Test-Path $pf)) { return }
    $id = [int](@(Get-Content $pf)[0] -split ' ')[0]
    $p = Get-Process -Id $id -ErrorAction SilentlyContinue
    if ($p) { [void]$p.WaitForExit($seconds * 1000) }
}

# --- facts -------------------------------------------------------------------------------------------------------

Fact 'P411_ARebuildIsStagedThenSwappedInAndTheNextStartOnlyChecks' {
    $uo = New-UoDir 'swap-uo'
    $app = New-Install 'swap'
    $r = Invoke-Prepare $app $uo 60
    $list = Join-Path $app 'uo-overrides\files-override.txt'
    Assert-Equal $list $r.Result "the list in the final folder. It said: $($r.Said)"
    Assert-True ($r.Said -match 'Preparing Shattered Legacy art and map, one time after an update\.\.\.') "the line before: $($r.Said)"
    Assert-True ($r.Said -match '\.\.\.done in \d+ s\.') "the line after: $($r.Said)"
    Assert-True (-not (Test-Path (Join-Path $app 'uo-overrides.next'))) 'no staging folder left'
    $l = [IO.File]::ReadAllText($list)
    Assert-True ($l -match [regex]::Escape("tiledata.mul=$(Join-Path $app 'uo-overrides\tiledata.mul')")) "the list names the final folder, not the staging one: $l"
    Assert-True ($l -notmatch 'uo-overrides\.next') $l
    $again = Invoke-Prepare $app $uo 60
    Assert-Equal $list $again.Result 'the same list'
    Assert-True ($again.Said -notmatch 'Preparing') "nothing rebuilt the second time: $($again.Said)"
}

Fact 'P411_TheTimeLimitStartsTheGameWithThePreviousOverridesAndLeavesThemIntact' {
    $uo = New-UoDir 'limit-uo'
    $app = New-Install 'limit'
    $first = Invoke-Prepare $app $uo 60
    $live = Join-Path $app 'uo-overrides'
    $before = @{ tile = (Get-Hash (Join-Path $live 'tiledata.mul')); stamp = (Get-Hash (Join-Path $live 'stamp.txt')); list = (Get-Hash (Join-Path $live 'files-override.txt')) }
    Set-NewRecords $app
    $env:SL_OVERRIDES_SLOW = 'art.targets:12'
    $r = Invoke-Prepare $app $uo 3
    Assert-Equal $first.Result $r.Result "the previous list, for the game to start with. It said: $($r.Said)"
    Assert-True ($r.Seconds -lt 12) "it did not wait for the rebuild: $($r.Seconds) s"
    Assert-True ($r.Said -match 'taking longer than 3 s on this computer\. Starting the game with the last good art') "one yellow line: $($r.Said)"
    Assert-Equal $before.tile (Get-Hash (Join-Path $live 'tiledata.mul')) 'the previous tiledata copy is untouched'
    Assert-Equal $before.stamp (Get-Hash (Join-Path $live 'stamp.txt')) 'and its stamp'
    Assert-Equal $before.list (Get-Hash (Join-Path $live 'files-override.txt')) 'and its list'
    Assert-True (Test-Path (Join-Path $app 'uo-overrides.next\builder.pid')) 'the rebuild carries on in the background'
    Remove-Item Env:SL_OVERRIDES_SLOW
    Wait-Builder $app
    Assert-True (Test-Path (Join-Path $app 'uo-overrides.next\ready.txt')) 'it finished, staged'
    $next = Invoke-Prepare $app $uo 60
    Assert-Equal $first.Result $next.Result 'the next start uses it'
    Assert-True ((Get-Hash (Join-Path $live 'stamp.txt')) -ne $before.stamp) 'swapped in: the stamp names the new records'
    Assert-True (-not (Test-Path (Join-Path $app 'uo-overrides.next'))) 'no staging folder left'
}

Fact 'P411_AFirstStartPastTheLimitStartsWithoutOverridesNotHalfOfThem' {
    $uo = New-UoDir 'first-uo'
    $app = New-Install 'first'
    $env:SL_OVERRIDES_SLOW = 'art.write:8'
    $r = Invoke-Prepare $app $uo 2
    Assert-Equal '' $r.Result "no list: there is no earlier set. It said: $($r.Said)"
    Assert-True ($r.Said -match 'Starting the game without them this time') $r.Said
    Assert-True (-not (Test-Path (Join-Path $app 'uo-overrides\files-override.txt'))) 'nothing half-built where the game reads'
    Wait-Builder $app
}

Fact 'P411_AKilledRebuildNeverLeavesAHalfBuiltSetAndTheNextStartBuildsAgain' {
    $uo = New-UoDir 'kill-uo'
    $app = New-Install 'kill'
    $first = Invoke-Prepare $app $uo 60
    $live = Join-Path $app 'uo-overrides'
    $tile = Get-Hash (Join-Path $live 'tiledata.mul')
    Set-NewRecords $app
    $env:SL_OVERRIDES_SLOW = 'art.write:20'
    $r = Invoke-Prepare $app $uo 2
    Assert-Equal $first.Result $r.Result 'the previous list'
    $id = [int](@(Get-Content (Join-Path $app 'uo-overrides.next\builder.pid'))[0] -split ' ')[0]
    Stop-Process -Id $id -Force
    Start-Sleep -Milliseconds 500
    Assert-Equal $tile (Get-Hash (Join-Path $live 'tiledata.mul')) 'the set the game reads is untouched'
    Assert-True (-not (Test-Path (Join-Path $app 'uo-overrides.next\ready.txt'))) 'the killed one never says ready'
    Remove-Item Env:SL_OVERRIDES_SLOW
    $next = Invoke-Prepare $app $uo 60
    Assert-True ($next.Said -match 'Preparing') "the next start builds it again: $($next.Said)"
    Assert-True ($next.Said -match '\.\.\.done in') $next.Said
    Assert-True (-not (Test-Path (Join-Path $app 'uo-overrides.next'))) 'and swaps it in'
}

Fact 'P411_ARebuildReportsProgressEveryFewSecondsAndASlowStepStillShowsTheBar' {
    $uo = New-UoDir 'progress-uo'
    $app = New-Install 'progress'
    $env:SL_OVERRIDES_SLOW = 'art.targets:5'
    $r = Invoke-Prepare $app $uo 60
    Assert-True ($r.Result) "built: $($r.Said)"
    $times = @($r.Shown | ForEach-Object { [double]::Parse(($_ -split ' ')[0], [Globalization.CultureInfo]::InvariantCulture) })
    Assert-True ($times.Count -ge 8) "the bar was redrawn all along: $($times.Count) times in $([Math]::Round($r.Seconds, 1)) s"
    for ($i = 1; $i -lt $times.Count; $i++) { Assert-True (($times[$i] - $times[$i - 1]) -le 2.0) "never more than 2 s without a redraw: $($r.Shown -join ' | ')" }
    Assert-True (@($r.Shown | Where-Object { $_ -match 'art\.targets \d+ of 5' }).Count -ge 3) "the slow step shows, with its count: $($r.Shown -join ' | ')"
}

Fact 'P411_TheTraceLogIsWrittenAndPrunedToTen' {
    $uo = New-UoDir 'trace-uo'
    $app = New-Install 'trace'
    $logs = Join-Path $app 'logs'
    New-Item -ItemType Directory -Path $logs -Force | Out-Null
    for ($i = 1; $i -le 12; $i++) {
        $f = Join-Path $logs ('overrides-20260101-0000{0:00}.log' -f $i)
        [IO.File]::WriteAllText($f, 'old')
        (Get-Item $f).LastWriteTimeUtc = [datetime]::UtcNow.AddHours(-24 + $i)
    }
    $r = Invoke-Prepare $app $uo 60
    Assert-True ($r.Result) "built: $($r.Said)"
    $all = @(Get-ChildItem -LiteralPath $logs -Filter 'overrides-*.log' | Sort-Object LastWriteTimeUtc)
    Assert-Equal 10 $all.Count "the last 10 kept: $($all.Name -join ', ')"
    Assert-True (-not (Test-Path (Join-Path $logs 'overrides-20260101-000001.log'))) 'the oldest went first'
    $t = [IO.File]::ReadAllText($all[-1].FullName)
    foreach ($step in 'art.read', 'art.targets', 'art.anim-records', 'art.tile-records', 'art.write', 'art.stamp') {
        Assert-True ($t -match ('(?m)' + [regex]::Escape($step) + '\s+\d+\.\d{3} s')) "a timed line for $step`: $t"
    }
    Assert-True ($t -match '(?m)UO files ' + [regex]::Escape($uo)) "the header names the UO folder: $t"
    Assert-True ($t -match '(?m)tiledata\.mul \d+ bytes, ') "and each file's size and attributes: $t"
    Assert-True ($t -match '(?m)total \d+\.\d{3} s') "and the total: $t"
}

Fact 'P411_AStaleStampAfterAnUpdateIsRebuiltByUpdatePs1' {
    $uo = New-UoDir 'update-uo'
    $install = Split-Path (New-Install 'update-install') -Parent
    $app = Join-Path $install 'app'
    [IO.File]::WriteAllText((Join-Path $app 'uo-path.txt'), $uo)
    [IO.File]::WriteAllText((Join-Path $app 'package-version.txt'), '2026.10.22.0855')
    New-Item -ItemType Directory -Path (Join-Path $app 'tazuo') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $app 'tazuo\TazUO.exe'), 'stand-in')
    $first = Invoke-Prepare $app $uo 60
    $stamp = Get-Hash (Join-Path $app 'uo-overrides\stamp.txt')
    # The new package: the same files, new art records and a new version.
    $src = Join-Path $work 'update-src'
    New-Item -ItemType Directory -Path (Join-Path $src 'app\tazuo') -Force | Out-Null
    Copy-Item (Join-Path $appSrc '*.ps1') (Join-Path $src 'app')
    Copy-Item (Join-Path $app 'art-records.json') (Join-Path $src 'app\art-records.json')
    Set-NewRecords (Join-Path $src 'app')
    Copy-Item (Join-Path $app 'tazuo\TazUO.exe') (Join-Path $src 'app\tazuo\TazUO.exe')
    [IO.File]::WriteAllText((Join-Path $src 'app\package-version.txt'), '2026.10.24.0900')
    $stage = Join-Path $work 'update-stage'; New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $env:SL_UPDATE_NO_PLAY = '1'
    $o = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $app 'Update.ps1') -Source $src -Target $install -Stage $stage -Test 2>&1 | Out-String)
    Assert-True ($o -match 'Updated to 2026\.10\.24\.0900') "updated: $o"
    Assert-True ($o -match 'Preparing Shattered Legacy art and map, one time after an update \(as part of it\)\.\.\.') "the updater rebuilt the art: $o"
    Assert-True ((Get-Hash (Join-Path $app 'uo-overrides\stamp.txt')) -ne $stamp) 'the stamp names the new records'
    Assert-True (@(Get-ChildItem (Join-Path $app 'logs') -Filter 'overrides-*.log').Count -ge 2) 'with its trace'
    $chk = & (Join-Path $app 'Build-UoOverrides.ps1') -UoDir $uo -OutDir (Join-Path $app 'uo-overrides') -CheckOnly
    Assert-True ($chk.Fresh) "so the first start after it only checks (stale: $($chk.Stale -join ','))"
}

Fact 'P411_CheckOnlySaysWhatIsStaleAndBuildsNothing' {
    $uo = New-UoDir 'check-uo'
    $app = New-Install 'check'
    $out = Join-Path $app 'uo-overrides'
    $c = & (Join-Path $app 'Build-UoOverrides.ps1') -UoDir $uo -OutDir $out -CheckOnly
    Assert-True (-not $c.Fresh -and @($c.Stale) -contains 'art') "nothing built yet: art is stale ($($c.Stale -join ','))"
    Assert-True (-not (Test-Path (Join-Path $out 'tiledata.mul'))) 'and nothing was written'
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
