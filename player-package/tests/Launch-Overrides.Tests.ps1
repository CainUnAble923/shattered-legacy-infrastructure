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
#
# cc-P611 (P549, P556 U2): the check is read-only, two starts share one builder, the time limit covers the lock and the
# swap, a set put aside is put back, a slow map parse is named on the bar, and the real Play.ps1 (and the updater
# handing over to it) starts a stub game with the right list in every timeout path, against a loopback listener.

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$pkg     = Split-Path $here -Parent
$appSrc  = Join-Path $pkg 'payload\app'
$records = Join-Path $pkg 'vendor\shattered-legacy-art\records.json'
$work    = Join-Path $env:TEMP ("sl-launch-overrides-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null
Remove-Item Env:SL_OVERRIDES_SLOW, Env:SL_OVERRIDES_LIMIT_SEC, Env:SL_PLAY_SERVER -ErrorAction SilentlyContinue

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
`$took = [Diagnostics.Stopwatch]::StartNew()
`$r = Invoke-UoOverrides -App '$app' -UoDir '$uo' -LimitSeconds $limit
'TOOK=' + `$took.Elapsed.TotalSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
'RESULT=' + `$r
foreach (`$l in `$script:OverridesProgressShown) { 'SHOWN=' + `$l }
"@)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $o = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script 2>&1 | ForEach-Object { "$_" })
    $sec = $sw.Elapsed.TotalSeconds
    [pscustomobject]@{
        Result  = (@($o | Where-Object { $_ -like 'RESULT=*' }) | Select-Object -First 1) -replace '^RESULT=', ''
        Shown   = @($o | Where-Object { $_ -like 'SHOWN=*' } | ForEach-Object { $_ -replace '^SHOWN=', '' })
        Said    = (@($o | Where-Object { $_ -notlike 'RESULT=*' -and $_ -notlike 'SHOWN=*' -and $_ -notlike 'TOOK=*' }) -join ' | ')
        Seconds = $sec
        Took    = [double]::Parse(((@($o | Where-Object { $_ -like 'TOOK=*' }) | Select-Object -First 1) -replace '^TOOK=', '0'), [Globalization.CultureInfo]::InvariantCulture)
    }
}

# cc-P611: the newest staged generation (app\uo-overrides.staging\g<ticks>, or the older app\uo-overrides.next), or $null.
function Get-Gen([string]$app) {
    $g = @()
    $legacy = Join-Path $app 'uo-overrides.next'
    if (Test-Path $legacy) { $g += $legacy }
    $root = Join-Path $app 'uo-overrides.staging'
    if (Test-Path $root) { $g += @(Get-ChildItem -LiteralPath $root -Directory | Sort-Object Name | ForEach-Object { $_.FullName }) }
    if ($g.Count) { return $g[-1] }
    $null
}

function Wait-Builder([string]$app, [int]$seconds = 60) {
    $gen = Get-Gen $app
    if (-not $gen) { return }
    $pf = Join-Path $gen 'builder.pid'
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
    Assert-True (-not (Get-Gen $app)) 'no staging folder left'
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
    Assert-True ((Get-Gen $app) -and (Test-Path (Join-Path (Get-Gen $app) 'builder.pid'))) 'the rebuild carries on in the background'
    Remove-Item Env:SL_OVERRIDES_SLOW
    Wait-Builder $app
    Assert-True (Test-Path (Join-Path (Get-Gen $app) 'ready.txt')) 'it finished, staged'
    $next = Invoke-Prepare $app $uo 60
    Assert-Equal $first.Result $next.Result 'the next start uses it'
    Assert-True ((Get-Hash (Join-Path $live 'stamp.txt')) -ne $before.stamp) 'swapped in: the stamp names the new records'
    Assert-True (-not (Get-Gen $app)) 'no staging folder left'
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
    $gen = Get-Gen $app
    $id = [int](@(Get-Content (Join-Path $gen 'builder.pid'))[0] -split ' ')[0]
    Stop-Process -Id $id -Force
    Start-Sleep -Milliseconds 500
    Assert-Equal $tile (Get-Hash (Join-Path $live 'tiledata.mul')) 'the set the game reads is untouched'
    Assert-True (-not (Test-Path (Join-Path $gen 'ready.txt'))) 'the killed one never says ready'
    Remove-Item Env:SL_OVERRIDES_SLOW
    $next = Invoke-Prepare $app $uo 60
    Assert-True ($next.Said -match 'Preparing') "the next start builds it again: $($next.Said)"
    Assert-True ($next.Said -match '\.\.\.done in') $next.Said
    Assert-True (-not (Get-Gen $app)) 'and swaps it in'
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

# --- cc-P611 (P549 #1-#5, P556 U2) -------------------------------------------------------------------------------

# A file held open with no sharing, as an antivirus scan or a backup tool can, until the returned stream is closed.
function Lock-File([string]$p) { [IO.File]::Open($p, 'Open', 'ReadWrite', 'None') }

Fact 'P611_ACheckThatCannotReadAStampLeavesTheListAndSaysSo' {
    $uo = New-UoDir 'locked-check-uo'
    $app = New-Install 'locked-check'
    $first = Invoke-Prepare $app $uo 60
    $live = Join-Path $app 'uo-overrides'
    $list = Join-Path $live 'files-override.txt'
    Assert-Equal $list $first.Result "built: $($first.Said)"
    $before = @{ list = (Get-Hash $list); tile = (Get-Hash (Join-Path $live 'tiledata.mul')) }
    $builder = Join-Path $app 'Build-UoOverrides.ps1'
    $h = Lock-File (Join-Path $live 'stamp.txt')
    try { $c = & $builder -UoDir $uo -OutDir $live -CheckOnly } finally { $h.Dispose() }
    Assert-True (Test-Path -LiteralPath $list) 'the list is still there after a check that could not read the stamp'
    Assert-Equal $before.list (Get-Hash $list) 'and unchanged'
    Assert-Equal $before.tile (Get-Hash (Join-Path $live 'tiledata.mul')) 'and the art copy too'
    Assert-True (-not $c.Fresh) "and the check does not call that fresh (Fresh=$($c.Fresh), List=$($c.List))"
    # Invalid input: no such Ultima Online folder.
    $c2 = & $builder -UoDir (Join-Path $work 'no-such-uo') -OutDir $live -CheckOnly
    Assert-True (-not $c2.Fresh) "a check of a folder that is not there is not fresh (Fresh=$($c2.Fresh))"
    Assert-Equal $before.list (Get-Hash $list) 'the list unchanged after it'
    # An otherwise fresh set whose list differs from the one a build would write: said, not rewritten.
    [IO.File]::AppendAllText($list, "# a line the builder would not write`r`n")
    $edited = Get-Hash $list
    $c3 = & $builder -UoDir $uo -OutDir $live -CheckOnly
    Assert-True (-not $c3.Fresh) 'a list that is not the one it would write is not fresh'
    Assert-Equal $edited (Get-Hash $list) 'and the check did not rewrite it'
    Assert-True (-not (Test-Path (Join-Path $live 'ready.txt'))) 'no ready file from a check'
}

# Two starts at once. The second comes while the first has started its builder but not yet written its pid file
# (Start-Process is slowed by 3 s there, as P549's probe did), so without one owner it would clear the first one's
# staging and start a second builder into it.
function Start-PrepareAsync([string]$app, [string]$uo, [int]$limit, [string]$tag) {
    $script = Join-Path $work ("prepare-$tag.ps1")
    $out = Join-Path $work ("prepare-$tag.out")
    [IO.File]::WriteAllText($script, @"
`$ErrorActionPreference = 'Stop'
function Start-Process { `$p = Microsoft.PowerShell.Management\Start-Process @args; Start-Sleep -Milliseconds 3000; `$p }
. '$app\Overrides-Launch.ps1'
`$r = Invoke-UoOverrides -App '$app' -UoDir '$uo' -LimitSeconds $limit
'RESULT=' + `$r
"@)
    $p = Start-Process -FilePath 'powershell.exe' -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$script`"" -RedirectStandardOutput $out -WindowStyle Hidden -PassThru
    [pscustomobject]@{ Process = $p; Out = $out }
}

Fact 'P611_TwoStartsAtOnceRunOneBuilderAndSwapInOneCompleteSet' {
    $uo = New-UoDir 'twice-uo'
    $app = New-Install 'twice'
    $first = Invoke-Prepare $app $uo 60
    Set-NewRecords $app
    $env:SL_OVERRIDES_SLOW = 'art.write:4'
    $a = Start-PrepareAsync $app $uo 60 'twice-a'
    Start-Sleep -Milliseconds 1200
    $b = Start-PrepareAsync $app $uo 60 'twice-b'
    foreach ($x in $a, $b) { if (-not $x.Process.WaitForExit(90000)) { $x.Process.Kill(); throw 'a start did not finish in 90 s' } }
    $ra = [IO.File]::ReadAllText($a.Out); $rb = [IO.File]::ReadAllText($b.Out)
    $headers = 0
    foreach ($f in @(Get-ChildItem (Join-Path $app 'logs') -Filter 'overrides-*.log')) { $headers += @(Select-String -LiteralPath $f.FullName -Pattern 'Build-UoOverrides\.ps1 \d{4}-').Count }
    Assert-Equal 2 $headers "one builder for the first build and one for the two starts, not two: $headers. A: $ra B: $rb"
    Assert-True ($ra -match [regex]::Escape("RESULT=$($first.Result)")) "the first start has the list: $ra"
    Assert-True ($rb -match [regex]::Escape("RESULT=$($first.Result)")) "and so has the second: $rb"
    $chk = & (Join-Path $app 'Build-UoOverrides.ps1') -UoDir $uo -OutDir (Join-Path $app 'uo-overrides') -CheckOnly
    Assert-True ($chk.Fresh) "the set in place is complete and current (stale: $($chk.Stale -join ','))"
    Assert-True (-not (Get-Gen $app)) 'no staging left'
}

# Prepares a ready staged set for an install whose current set is older: a rebuild past a 1 s limit, waited for.
function New-ReadyStagedSet([string]$name) {
    $uo = New-UoDir "$name-uo"
    $app = New-Install $name
    $first = Invoke-Prepare $app $uo 60
    Set-NewRecords $app
    $env:SL_OVERRIDES_SLOW = 'art.write:3'
    $null = Invoke-Prepare $app $uo 1
    Remove-Item Env:SL_OVERRIDES_SLOW
    Wait-Builder $app
    $gen = Get-Gen $app
    Assert-True ($gen -and (Test-Path (Join-Path $gen 'ready.txt'))) 'a staged set, ready'
    [pscustomobject]@{ Uo = $uo; App = $app; First = $first; Gen = $gen }
}

Fact 'P611_ASwapThatCannotMoveStaysInsideTheLimitAndKeepsTheLastGoodSet' {
    $s = New-ReadyStagedSet 'held-swap'
    $live = Join-Path $s.App 'uo-overrides'
    $list = Join-Path $live 'files-override.txt'
    $before = Get-Hash $list
    $h = Lock-File (Join-Path $s.Gen 'tiledata.mul')
    try { $r = Invoke-Prepare $s.App $s.Uo 2 } finally { $h.Dispose() }
    Assert-True ($r.Took -lt 3.0) "Invoke-UoOverrides with a 2 s limit took $([Math]::Round($r.Took, 2)) s. It said: $($r.Said)"
    Assert-Equal $s.First.Result $r.Result "the last good list: $($r.Said)"
    Assert-Equal $before (Get-Hash $list) 'unchanged'
    $next = Invoke-Prepare $s.App $s.Uo 60
    Assert-Equal $s.First.Result $next.Result "swapped in at the next start: $($next.Said)"
    Assert-True ((Get-Hash (Join-Path $live 'stamp.txt')) -ne $null) 'a set in place'
    Assert-True (-not (Get-Gen $s.App)) 'no staging left'
}

Fact 'P611_ASwapThatRanOutOfTimeHalfwayPutsTheLastGoodSetBack' {
    $uo = New-UoDir 'half-swap-uo'
    $app = New-Install 'half-swap'
    $first = Invoke-Prepare $app $uo 60
    $live = Join-Path $app 'uo-overrides'
    $tile = Get-Hash (Join-Path $live 'tiledata.mul')
    # What a swap leaves when its time ran out after moving the current set aside.
    Rename-Item -LiteralPath $live -NewName 'uo-overrides.old'
    $r = Invoke-Prepare $app $uo 60
    Assert-Equal $first.Result $r.Result "the last good list: $($r.Said)"
    Assert-True ($r.Said -notmatch 'Preparing') "put back, not built again: $($r.Said)"
    Assert-Equal $tile (Get-Hash (Join-Path $live 'tiledata.mul')) 'the same set'
    Assert-True (-not (Test-Path (Join-Path $app 'uo-overrides.old'))) 'no .old left'
}

Fact 'P611_ASlowMapParseIsOnTheBarByName' {
    $uo = New-UoDir 'map-parse-uo'
    $app = New-Install 'map-parse'
    # A patch with no map files beside it: read, then refused (the part is skipped with one line).
    $ops = New-Object Text.StringBuilder
    [void]$ops.Append("F 1`n")
    for ($i = 0; $i -lt 9000; $i++) { [void]$ops.Append(('L {0} {1} 3 0' -f (1000 + $i % 80), (1000 + [int]($i / 80)))).Append("`n") }
    [IO.File]::WriteAllText((Join-Path $app 'trammel.mapops'), $ops.ToString())
    $env:SL_OVERRIDES_SLOW = 'map.parse:4'
    $r = Invoke-Prepare $app $uo 60
    Assert-True ($r.Result) "built: $($r.Said)"
    Assert-True (@($r.Shown | Where-Object { $_ -match '^\S+ map\.parse' }).Count -ge 3) "the slow map parse shows by name: $($r.Shown -join ' | ')"
    $log = @(Get-ChildItem (Join-Path $app 'logs') -Filter 'overrides-*.log' | Sort-Object LastWriteTimeUtc)[-1].FullName
    $t = [IO.File]::ReadAllText($log)
    Assert-True ($t -match '(?m)  step map\.parse\r?$') "its start is traced before its work: $t"
    Assert-True ($t -match '(?m)map\.parse\s+\d+\.\d{3} s') "and its seconds after: $t"
    Remove-Item Env:SL_OVERRIDES_SLOW
    $again = Invoke-Prepare $app $uo 60
    Assert-True ($again.Said -notmatch 'Preparing') "a part that is skipped every time does not rebuild every start: $($again.Said)"
}

# --- the real Play.ps1, and the updater handing over to it, in a sandbox ----------------------------------------
# The game is a stub TazUO.exe that writes when it started and its command line. The shard is a loopback listener
# (Play.ps1's test hook SL_PLAY_SERVER), the update check is -NoUpdate and the Legion scripts are -NoScripts (or a stub
# Update-Scripts.ps1 for the updater's own Play), so nothing leaves this computer.
$gameStubSrc = @'
using System; using System.IO;
public static class GameStub { public static void Main() {
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "started.txt"),
        DateTime.UtcNow.Ticks + "\r\n" + Environment.CommandLine);
} }
'@
$gameStub = Join-Path $work 'game-stub.exe'
Add-Type -TypeDefinition $gameStubSrc -OutputAssembly $gameStub -OutputType ConsoleApplication
$listener = New-Object Net.Sockets.TcpListener ([Net.IPAddress]::Loopback, 0)
$listener.Start()
$env:SL_PLAY_SERVER = "127.0.0.1:$($listener.LocalEndpoint.Port)"
$env:SL_LEGION_SETTINGS = Join-Path $work 'no-legion-settings.json'

# A whole install: every package script, the art records, a stub game and a stub Setup.ps1 that "finds" $uo.
function New-PlayInstall([string]$name, [string]$uo) {
    $app = New-Install $name
    New-Item -ItemType Directory -Path (Join-Path $app 'tazuo') -Force | Out-Null
    Copy-Item (Join-Path $appSrc '*.bat') $app -ErrorAction SilentlyContinue
    Copy-Item $gameStub (Join-Path $app 'tazuo\TazUO.exe')
    Copy-Item $gameStub (Join-Path $uo 'client.exe')
    [IO.File]::WriteAllText((Join-Path $app 'Setup.ps1'), "[IO.File]::WriteAllText((Join-Path `$PSScriptRoot 'uo-path.txt'), '$uo'); exit 0")
    [IO.File]::WriteAllText((Join-Path $app 'package-version.txt'), '2026.10.22.0855')
    [IO.File]::WriteAllText((Join-Path $app 'uo-path.txt'), $uo)
    return $app
}

function Get-GameStart([string]$app) {
    $f = Join-Path $app 'tazuo\started.txt'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path $f) -and $sw.ElapsedMilliseconds -lt 10000) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path $f)) { return $null }
    $l = @([IO.File]::ReadAllLines($f))
    [pscustomobject]@{ At = [datetime]::new([long]$l[0], [DateTimeKind]::Utc); Args = ($l[1..($l.Count - 1)] -join ' ') }
}

# Runs the command line and waits; returns what it printed and when it started.
function Invoke-Sandboxed([string]$app, [string]$arguments) {
    $f = Join-Path $app 'tazuo\started.txt'
    if (Test-Path $f) { Remove-Item $f -Force }
    $psi = New-Object Diagnostics.ProcessStartInfo 'powershell.exe', $arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $true
    $psi.WorkingDirectory = $app
    $at = [datetime]::UtcNow
    $pr = [Diagnostics.Process]::Start($psi)
    $pr.StandardInput.Close()
    $err = $pr.StandardError.ReadToEndAsync()
    $out = $pr.StandardOutput.ReadToEnd()
    if (-not $pr.WaitForExit(120000)) { $pr.Kill(); throw "powershell $arguments did not finish in 120 s" }
    [pscustomobject]@{ Out = ($out + $err.Result); At = $at; Game = (Get-GameStart $app) }
}

function Invoke-Play([string]$app) { Invoke-Sandboxed $app "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $app 'Play.ps1')`" -NoUpdate -NoScripts" }

Fact 'P611_PlayStartsTheGameWithTheLastGoodSetWhenTheRebuildRunsLong' {
    $uo = New-UoDir 'play-limit-uo'
    $app = New-PlayInstall 'play-limit' $uo
    $first = Invoke-Prepare $app $uo 60
    $list = Join-Path $app 'uo-overrides\files-override.txt'
    $bytes = Get-Hash $list
    Set-NewRecords $app
    $env:SL_OVERRIDES_SLOW = 'art.write:20'
    $env:SL_OVERRIDES_LIMIT_SEC = '3'
    $r = Invoke-Play $app
    Assert-True ($r.Game) "the game started. Play said: $($r.Out)"
    Assert-True ($r.Game.Args -match ('-uofilesoverride "?' + [regex]::Escape($list))) "with the last good list: $($r.Game.Args)"
    Assert-Equal $bytes (Get-Hash $list) 'which is unchanged'
    Assert-True (($r.Game.At - $r.At).TotalSeconds -lt 15) "and soon: $(($r.Game.At - $r.At).TotalSeconds) s after Play began"
    Remove-Item Env:SL_OVERRIDES_SLOW, Env:SL_OVERRIDES_LIMIT_SEC
    Wait-Builder $app
}

Fact 'P611_PlayOnAFirstInstallStartsTheGameWithNoListWhenTheRebuildRunsLong' {
    $uo = New-UoDir 'play-first-uo'
    $app = New-PlayInstall 'play-first' $uo
    $env:SL_OVERRIDES_SLOW = 'art.write:20'
    $env:SL_OVERRIDES_LIMIT_SEC = '3'
    $r = Invoke-Play $app
    Assert-True ($r.Game) "the game started. Play said: $($r.Out)"
    Assert-True ($r.Game.Args -notmatch 'uofilesoverride') "with no list: $($r.Game.Args)"
    Assert-True (-not (Test-Path (Join-Path $app 'uo-overrides\files-override.txt'))) 'nothing half-built where the game reads'
    Remove-Item Env:SL_OVERRIDES_SLOW, Env:SL_OVERRIDES_LIMIT_SEC
    Wait-Builder $app
}

Fact 'P611_PlayWithALockedStampStartsTheGameWithTheLastGoodList' {
    $uo = New-UoDir 'play-locked-uo'
    $app = New-PlayInstall 'play-locked' $uo
    $first = Invoke-Prepare $app $uo 60
    $list = Join-Path $app 'uo-overrides\files-override.txt'
    $bytes = Get-Hash $list
    $env:SL_OVERRIDES_LIMIT_SEC = '4'
    $h = Lock-File (Join-Path $app 'uo-overrides\stamp.txt')
    try { $r = Invoke-Play $app } finally { $h.Dispose() }
    Assert-True ($r.Game) "the game started. Play said: $($r.Out)"
    Assert-True ($r.Game.Args -match ('-uofilesoverride "?' + [regex]::Escape($list))) "with the last good list: $($r.Game.Args)"
    Assert-True (Test-Path -LiteralPath $list) 'still there'
    Assert-Equal $bytes (Get-Hash $list) 'and unchanged'
    Remove-Item Env:SL_OVERRIDES_LIMIT_SEC
    Wait-Builder $app
}

Fact 'P611_PlayWhileAnotherStartHoldsTheArtStartsWithinItsLimit' {
    $uo = New-UoDir 'play-held-uo'
    $app = New-PlayInstall 'play-held' $uo
    $first = Invoke-Prepare $app $uo 60
    $list = Join-Path $app 'uo-overrides\files-override.txt'
    # Another start holds the install's lock and never lets go (a hung start); then it is killed.
    $holder = Join-Path $work 'hold-lock.ps1'
    [IO.File]::WriteAllText($holder, @"
. '$app\Overrides-Launch.ps1'
`$l = Enter-OverridesLock '$app' ([Diagnostics.Stopwatch]::StartNew()) 10
[IO.File]::WriteAllText('$work\held.txt', [string][bool]`$l)
Start-Sleep -Seconds 60
"@)
    $hp = Start-Process -FilePath 'powershell.exe' -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$holder`"" -WindowStyle Hidden -PassThru
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path (Join-Path $work 'held.txt')) -and $sw.ElapsedMilliseconds -lt 15000) { Start-Sleep -Milliseconds 100 }
    try {
        Assert-Equal 'True' ([IO.File]::ReadAllText((Join-Path $work 'held.txt'))) 'the other start has the lock'
        $env:SL_OVERRIDES_LIMIT_SEC = '2'
        $r = Invoke-Play $app
        Assert-True ($r.Game) "the game started. Play said: $($r.Out)"
        Assert-True ($r.Game.Args -match ('-uofilesoverride "?' + [regex]::Escape($list))) "with the last good list: $($r.Game.Args)"
        Assert-True ($r.Out -match 'another start of the game') "it said why: $($r.Out)"
    } finally { if (-not $hp.HasExited) { $hp.Kill() }; [void]$hp.WaitForExit(5000) }
    # The holder died holding it (abandoned): the next start takes the lock over and checks as usual.
    Remove-Item Env:SL_OVERRIDES_LIMIT_SEC
    $after = Invoke-Prepare $app $uo 60
    Assert-Equal $first.Result $after.Result "after the holder is gone: $($after.Said)"
    Assert-True ($after.Said -notmatch 'another start') $after.Said
}

Fact 'P611_TheUpdaterWaitsOnceAndItsPlayDoesNotWaitAgain' {
    $uo = New-UoDir 'handoff-uo'
    $app = New-PlayInstall 'handoff' $uo
    $install = Split-Path $app -Parent
    $first = Invoke-Prepare $app $uo 60
    $list = Join-Path $app 'uo-overrides\files-override.txt'
    $bytes = Get-Hash $list
    # The new package: the same scripts and stubs, new art records and a new version.
    $src = Join-Path $work 'handoff-src'
    New-Item -ItemType Directory -Path (Join-Path $src 'app\tazuo') -Force | Out-Null
    Copy-Item (Join-Path $app '*.ps1') (Join-Path $src 'app')
    [IO.File]::WriteAllText((Join-Path $src 'app\Update-Scripts.ps1'), 'exit 0')   # no scripts channel in a test
    Copy-Item (Join-Path $app 'art-records.json') (Join-Path $src 'app\art-records.json')
    Set-NewRecords (Join-Path $src 'app')
    Copy-Item $gameStub (Join-Path $src 'app\tazuo\TazUO.exe')
    [IO.File]::WriteAllText((Join-Path $src 'app\package-version.txt'), '2026.10.24.0900')
    $stage = Join-Path $work 'handoff-stage'; New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $env:SL_OVERRIDES_SLOW = 'art.write:30'
    $env:SL_OVERRIDES_LIMIT_SEC = '4'
    $r = Invoke-Sandboxed $app "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $app 'Update.ps1')`" -Source `"$src`" -Target `"$install`" -Stage `"$stage`" -Test"
    Assert-True ($r.Out -match 'Updated to 2026\.10\.24\.0900') "updated: $($r.Out)"
    Assert-True ($r.Game) "the game started. It said: $($r.Out)"
    $waits = ([regex]::Matches($r.Out, 'taking longer than 4 s')).Count
    Assert-Equal 1 $waits "the rebuild was waited for once, by the updater, not again by Play: $($r.Out)"
    Assert-True ($r.Game.Args -match ('-uofilesoverride "?' + [regex]::Escape($list))) "with the last good list: $($r.Game.Args)"
    Assert-Equal $bytes (Get-Hash $list) 'which is unchanged'
    Remove-Item Env:SL_OVERRIDES_SLOW, Env:SL_OVERRIDES_LIMIT_SEC
    Wait-Builder $app
}

$listener.Stop()
Remove-Item Env:SL_PLAY_SERVER, Env:SL_LEGION_SETTINGS -ErrorAction SilentlyContinue

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
