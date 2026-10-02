#Requires -Version 5.1
# Facts for payload\app\Launch-Settings.ps1 and Play.ps1's -Account profiles (cc-P28).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Launch-Settings.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Build-UoOverrides.Tests.ps1.
#
# The function facts write settings files into a scratch folder and need nothing else. The end-to-end
# facts run the real Play.ps1 (and Play-Test.bat, the way a desktop shortcut does) in a sandbox: a stub
# TazUO.exe that writes the command line it was given, a stub Setup.ps1, -NoUpdate. A refusal happens
# before anything is checked or sent, so those facts need no server. A launch that is allowed checks the
# TEST shard's port (shatteredlegacyuo.com:2594) first, as Play.ps1 always does, so those facts need the
# test shard up; nothing else is sent to it.

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$pkg  = Split-Path $here -Parent
$appSrc = Join-Path $pkg 'payload\app'
$work = Join-Path $env:TEMP ("sl-launch-settings-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null

. (Join-Path $appSrc 'Launch-Settings.ps1')

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

function Write-Json([string]$path, $obj) {
    [IO.File]::WriteAllText($path, ($obj | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
}

function Read-Json([string]$path) { [IO.File]::ReadAllText($path) | ConvertFrom-Json }

function New-Dir([string]$name) {
    $d = Join-Path $work $name
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    return $d
}

# Write-LaunchSettings as Play.ps1 calls it, for a target from Get-LaunchTarget.
function Write-For([string]$dir, [hashtable]$targetArgs) {
    $t = Get-LaunchTarget @targetArgs
    $p = Join-Path $dir $t.SettingsName
    Write-LaunchSettings -Path $p -Target $t -UoDir 'C:\uo' -ClientVersion '7.0.109.8' -Test:([bool]$targetArgs.Test)
    return $p
}

# A saved login as TazUO leaves it: the password is TazUO's own encoding, never plain text.
$savedLogin = [ordered]@{ username = 'gargtester'; password = '1-2-3-encoded'; saveaccount = $true; autologin = $true; window_size = '1024, 768' }

# --- functions ----------------------------------------------------------------------------------

Fact 'AProfileGetsItsOwnTestSettingsFileAndALabelThatNamesIt' {
    $t = Get-LaunchTarget -Test -Account 'gargoyle'
    Assert-Equal 'settings.test.gargoyle.json' $t.SettingsName
    Assert-Equal 'gargoyle' $t.Profile
    Assert-Equal 'TEST SHARD (throwaway), profile: gargoyle' $t.Label
    Assert-Equal 2594 $t.Port 'the test shard'
    Assert-Equal 'Shattered Legacy TEST' $t.ServerName
    foreach ($n in 'admin', 'human', 'elf', 'a', 'x-2', ('a' * 24)) {
        Assert-Equal "settings.test.$n.json" (Get-LaunchTarget -Test -Account $n).SettingsName $n
    }
}

Fact 'AProfileKeepsItsSavedLoginAcrossRelaunchesAndTurnsOnSaveAndAutologin' {
    $d = New-Dir 'keep'
    $p = Join-Path $d 'settings.test.gargoyle.json'
    $old = [ordered]@{} + $savedLogin; $old.saveaccount = $false; $old.autologin = $false
    Write-Json $p $old
    foreach ($launch in 1, 2) {
        [void](Write-For $d @{ Test = $true; Account = 'gargoyle' })
        $s = Read-Json $p
        Assert-Equal 'gargtester' $s.username "launch $launch"
        Assert-Equal '1-2-3-encoded' $s.password "launch $launch"
        Assert-Equal $true $s.saveaccount "launch $launch"
        Assert-Equal $true $s.autologin "launch $launch"
        Assert-Equal '1024, 768' $s.window_size "launch ${launch}: a setting the launcher does not own is kept"
        Assert-Equal 2594 $s.port "launch $launch"
    }
}

Fact 'AFirstLaunchOfAProfileHasAnEmptyLoginWithSaveAndAutologinOn' {
    $d = New-Dir 'fresh'
    $p = Write-For $d @{ Test = $true; Account = 'elf' }
    $s = Read-Json $p
    Assert-Equal '' $s.username 'nothing to log in with yet: TazUO shows the login screen'
    Assert-Equal '' $s.password
    Assert-Equal $true $s.saveaccount 'Save Account is already ticked on the login screen'
    Assert-Equal $true $s.autologin
}

Fact 'WithoutAProfileTestStillForcesSaveAccountAndAutologinOff' {
    $d = New-Dir 'plain-test'
    Write-Json (Join-Path $d 'settings.test.json') $savedLogin
    $p = Write-For $d @{ Test = $true }
    Assert-Equal 'settings.test.json' (Split-Path $p -Leaf)
    $s = Read-Json $p
    Assert-Equal $false $s.saveaccount
    Assert-Equal $false $s.autologin
    Assert-Equal 'TEST SHARD (throwaway)' (Get-LaunchTarget -Test).Label 'the label is unchanged'
    Assert-Equal '' (Get-LaunchTarget -Test).Profile
}

Fact 'LiveAndLanAreUnchanged' {
    foreach ($a in @{}, @{ Lan = $true }) {
        $d = New-Dir ('live-' + $a.Count)
        Write-Json (Join-Path $d 'settings.json') $savedLogin
        $p = Write-For $d $a
        Assert-Equal 'settings.json' (Split-Path $p -Leaf)
        $s = Read-Json $p
        Assert-Equal 'gargtester' $s.username
        Assert-Equal $true $s.saveaccount 'live never forced it off'
        Assert-Equal $true $s.autologin 'live keeps what the player chose'
    }
}

Fact 'TwoProfilesDoNotShareAUsername' {
    $d = New-Dir 'two'
    Write-Json (Join-Path $d 'settings.test.gargoyle.json') $savedLogin
    $before = (Get-FileHash (Join-Path $d 'settings.test.gargoyle.json')).Hash
    $p = Write-For $d @{ Test = $true; Account = 'elf' }
    Assert-Equal '' (Read-Json $p).username 'elf starts empty, not with the gargoyle login'
    Assert-Equal $before (Get-FileHash (Join-Path $d 'settings.test.gargoyle.json')).Hash 'writing elf did not touch gargoyle'
    # Nor does a new profile inherit the plain test login.
    Write-Json (Join-Path $d 'settings.test.json') ([ordered]@{ username = 'plaintester'; password = 'x' })
    $p = Write-For $d @{ Test = $true; Account = 'human' }
    Assert-Equal '' (Read-Json $p).username 'human starts empty, not with the settings.test.json login'
}

Fact 'AccountWithoutTestIsRefused' {
    foreach ($a in @{ Account = 'gargoyle' }, @{ Lan = $true; Account = 'gargoyle' }) {
        $msg = $null
        try { [void](Get-LaunchTarget @a) } catch { $msg = $_.Exception.Message }
        Assert-True ($msg -match '^-Account works only with -Test') "refused: [$msg]"
    }
}

Fact 'ABadProfileNameIsRefused' {
    foreach ($n in '', '../x', '..\x', 'Admin!', 'Admin', 'GARGOYLE', 'a b', 'a.b', 'a_b', ('a' * 25), 'elf/x', 'c:') {
        $msg = $null
        try { [void](Get-LaunchTarget -Test -Account $n) } catch { $msg = $_.Exception.Message }
        Assert-True ($msg -match '^-Account ''.*'' is not a profile name') "[$n] refused: [$msg]"
    }
}

# --- Play.ps1 and Play-Test.bat, end to end in a sandbox -------------------------------------------
$stubSrc = @'
using System; using System.IO;
public static class Stub { public static void Main() {
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "args.txt"), Environment.CommandLine);
} }
'@
$stubExe = Join-Path $work 'stub.exe'
Add-Type -TypeDefinition $stubSrc -OutputAssembly $stubExe -OutputType ConsoleApplication

function New-Sandbox([string]$name) {
    $root = Join-Path $work $name
    $app = Join-Path $root 'app'
    New-Item -ItemType Directory -Path (Join-Path $app 'tazuo') -Force | Out-Null
    foreach ($f in 'Play.ps1', 'Launch-Settings.ps1', 'Play-Test.bat', 'Play-Lan.bat', 'Update.ps1') { Copy-Item (Join-Path $appSrc $f) $app }
    Copy-Item $stubExe (Join-Path $app 'tazuo\TazUO.exe')
    $uo = Join-Path $work "$name-uo"
    New-Item -ItemType Directory -Path $uo -Force | Out-Null
    Copy-Item $stubExe (Join-Path $uo 'client.exe')
    # Stub Setup.ps1: the fake Ultima Online folder is "found". No Build-UoOverrides.ps1: Play.ps1's
    # catch starts the game without our art, which is not what these facts are about.
    [IO.File]::WriteAllText((Join-Path $app 'Setup.ps1'), "[IO.File]::WriteAllText((Join-Path `$PSScriptRoot 'uo-path.txt'), '$uo'); exit 0")
    [IO.File]::WriteAllText((Join-Path $app 'package-version.txt'), '2026.10.02.0000')
    return $root
}

function Get-SettingsFiles([string]$root) {
    @(Get-ChildItem -LiteralPath (Join-Path $root 'app\tazuo') -Filter 'settings*.json' | ForEach-Object { $_.Name } | Sort-Object)
}

# Runs a command line exactly as given (no PowerShell argument mangling) and waits for it.
# Returns what it printed, its exit code, and TazUO's command line if the game was started.
function Invoke-Raw([string]$root, [string]$exe, [string]$arguments, [switch]$ExpectGame) {
    $argsFile = Join-Path $root 'app\tazuo\args.txt'
    if (Test-Path $argsFile) { Remove-Item $argsFile -Force }
    $psi = New-Object Diagnostics.ProcessStartInfo $exe, $arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $true    # a .bat that pauses on failure reads EOF and goes on
    $psi.WorkingDirectory = Join-Path $root 'app'
    $pr = [Diagnostics.Process]::Start($psi)
    $pr.StandardInput.Close()
    $errTask = $pr.StandardError.ReadToEndAsync()
    $out = $pr.StandardOutput.ReadToEnd()
    if (-not $pr.WaitForExit(60000)) { $pr.Kill(); throw "$exe $arguments did not finish in 60 s" }
    $out += $errTask.Result
    if ($ExpectGame) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path $argsFile) -and $sw.ElapsedMilliseconds -lt 10000) { Start-Sleep -Milliseconds 100 }
    }
    $gameArgs = $null
    if (Test-Path $argsFile) { $gameArgs = [IO.File]::ReadAllText($argsFile) }
    return [pscustomobject]@{ Out = $out; Exit = $pr.ExitCode; Args = $gameArgs }
}

function Invoke-Play([string]$root, [string]$arguments, [switch]$ExpectGame) {
    Invoke-Raw $root 'powershell.exe' ("-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $root 'app\Play.ps1')`" -NoUpdate " + $arguments) -ExpectGame:$ExpectGame
}

function Get-Lines([string]$text) { @($text -split "`r?`n" | Where-Object { $_.Trim() }) }

Fact 'EndToEnd_AShortcutLaunchUsesTheProfileFileLogsStraightInAndLeavesSettingsTestJsonAlone' {
    $root = New-Sandbox 'e2e-profile'
    $plain = Join-Path $root 'app\tazuo\settings.test.json'
    Write-Json $plain ([ordered]@{ username = 'plaintester'; password = 'x'; saveaccount = $false })
    $plainHash = (Get-FileHash $plain).Hash
    # Exactly what a desktop shortcut runs: the .bat, with -Account after its own -Test.
    $bat = Join-Path $root 'app\Play-Test.bat'
    $r = Invoke-Raw $root 'cmd.exe' "/c `"`"$bat`" -Account gargoyle -NoUpdate`"" -ExpectGame
    Assert-Equal 0 $r.Exit "Play-Test.bat said: $($r.Out)"
    Assert-True ($r.Args) "the game was not started. It said: $($r.Out)"
    $gp = Join-Path $root 'app\tazuo\settings.test.gargoyle.json'
    Assert-True ($r.Args -match ('-settings "' + [regex]::Escape($gp) + '" -skiploginscreen')) "game command line: $($r.Args)"
    Assert-True ($r.Out -match 'TEST SHARD \(throwaway\), profile: gargoyle') $r.Out
    Assert-Equal $plainHash (Get-FileHash $plain).Hash 'settings.test.json untouched'
    Assert-Equal 'settings.test.gargoyle.json|settings.test.json' ((Get-SettingsFiles $root) -join '|') 'no other file'
    $s = Read-Json $gp
    Assert-Equal $true $s.saveaccount
    Assert-Equal $true $s.autologin
    Assert-Equal '' $s.username

    # Chase logs in once with Save Account ticked; TazUO writes the login into the profile's file.
    $s.username = 'gargtester'; $s.password = '1-2-3-encoded'
    Write-Json $gp $s
    $r = Invoke-Play $root '-Test -Account gargoyle' -ExpectGame
    $s = Read-Json $gp
    Assert-Equal 'gargtester' $s.username 'kept across the relaunch'
    Assert-Equal '1-2-3-encoded' $s.password 'kept across the relaunch'
    Assert-Equal $true $s.autologin
    Assert-True ($r.Args -notmatch 'gargtester|1-2-3-encoded|-password|-username') "no login on the command line: $($r.Args)"
}

Fact 'EndToEnd_WithoutAProfileTestIsTodaysLaunch' {
    $root = New-Sandbox 'e2e-plain'
    $r = Invoke-Play $root '-Test' -ExpectGame
    Assert-True ($r.Args -match '-settings "[^"]+\\settings\.test\.json"\s*$') "game command line: $($r.Args)"
    Assert-True ($r.Args -notmatch 'skiploginscreen') $r.Args
    $s = Read-Json (Join-Path $root 'app\tazuo\settings.test.json')
    Assert-Equal $false $s.saveaccount
    Assert-Equal $false $s.autologin
    Assert-True ($r.Out -match '(?m)^\s*TEST SHARD \(throwaway\)\s*$') $r.Out
}

Fact 'EndToEnd_AccountWithoutTestIsRefusedWithOneLineAndWritesNothing' {
    $root = New-Sandbox 'e2e-live'
    foreach ($a in '-Account gargoyle', '-Lan -Account gargoyle') {
        $r = Invoke-Play $root $a
        Assert-Equal 1 $r.Exit "[$a] exit code. It said: $($r.Out)"
        $lines = @(Get-Lines $r.Out)
        Assert-Equal 1 $lines.Count "[$a] one line: $($r.Out)"
        Assert-True ($lines[0] -match '^\s*-Account works only with -Test') "[$a] $($lines[0])"
        Assert-Equal $null $r.Args "[$a] the game must not start"
        Assert-Equal 0 (Get-SettingsFiles $root).Count "[$a] no settings file written"
        Assert-True (-not (Test-Path (Join-Path $root 'app\uo-path.txt'))) "[$a] refused before Setup ran"
    }
}

Fact 'EndToEnd_ABadProfileNameIsRefusedWithOneLineAndWritesNothing' {
    $root = New-Sandbox 'e2e-bad'
    foreach ($n in '../x', '..\x', 'Admin!', 'Admin', '"a b"', '""') {
        $r = Invoke-Play $root "-Test -Account $n"
        Assert-Equal 1 $r.Exit "[$n] exit code. It said: $($r.Out)"
        $lines = @(Get-Lines $r.Out)
        Assert-Equal 1 $lines.Count "[$n] one line: $($r.Out)"
        Assert-True ($lines[0] -match '^\s*-Account ''.*'' is not a profile name') "[$n] $($lines[0])"
        Assert-Equal $null $r.Args "[$n] the game must not start"
        Assert-Equal 0 (Get-SettingsFiles $root).Count "[$n] no settings file written"
    }
    # Nothing escaped the tazuo folder either.
    Assert-Equal 0 @(Get-ChildItem -LiteralPath $root -Recurse -Filter 'settings*.json').Count 'no settings file anywhere'
}

Fact 'EndToEnd_AnUpdateRelaunchesTheSameProfile' {
    # Update.ps1 with nothing to change ("Already up to date") starts the game the way it was started.
    $root = New-Sandbox 'e2e-update'
    $src = Join-Path $work 'e2e-update-src'
    Copy-Item -LiteralPath $root -Destination $src -Recurse
    $stage = New-Dir 'e2e-update-stage'
    $upd = Join-Path $root 'app\Update.ps1'
    $r = Invoke-Raw $root 'powershell.exe' ("-NoProfile -ExecutionPolicy Bypass -File `"$upd`" -Source `"$src`" -Target `"$root`" -Stage `"$stage`" -Test -Account gargoyle") -ExpectGame
    Assert-True ($r.Out -match 'Already up to date') $r.Out
    Assert-True ($r.Args -match 'settings\.test\.gargoyle\.json" -skiploginscreen') "game command line: $($r.Args). Update said: $($r.Out)"
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
