#Requires -Version 5.1
# Facts for cc-P719: a new character starts walking, with its paperdoll closed (Launch-Settings.ps1's
# Initialize-NewProfileDefaults, its call in Play.ps1, and client patch 0009).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\New-Profile-Defaults.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Launch-Settings.Tests.ps1. Needs nothing
# but a scratch folder: no client, no server.

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$pkg  = Split-Path $here -Parent
$repo = Split-Path $pkg -Parent
$appSrc = Join-Path $pkg 'payload\app'
$work = Join-Path $env:TEMP ("sl-new-profile-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
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

function New-Dir([string]$name) {
    $d = Join-Path $work $name
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    return $d
}

# TazUO's profile key for a property name (Profile.cs, SnakeCaseNamingPolicy: an underscore before each capital
# but the first, all lower case).
function ConvertTo-TazKey([string]$name) {
    return ([regex]::Replace($name, '(?<!^)([A-Z])', '_$1')).ToLowerInvariant()
}

Fact 'A_new_install_gets_a_default_profile_that_walks_and_keeps_the_paperdoll_closed' {
    $taz = New-Dir 'fresh'
    $wrote = Initialize-NewProfileDefaults -ProfilesDir (Join-Path $taz 'Data\Profiles')
    $f = Join-Path $taz 'Data\Profiles\default.json'
    Assert-True (Test-Path -LiteralPath $f) 'default.json written'
    Assert-Equal $true $wrote 'says it wrote it'
    $bytes = [IO.File]::ReadAllBytes($f)
    Assert-True (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) 'no BOM'
    $j = [IO.File]::ReadAllText($f) | ConvertFrom-Json
    Assert-Equal $false $j.always_run 'always_run off'
    Assert-Equal $false $j.sl_open_paperdoll_at_login 'paperdoll not opened at login'
    Assert-Equal 2 @($j.PSObject.Properties).Count 'only those two keys: everything else keeps TazUO''s own default'
}

Fact 'A_default_profile_the_player_saved_is_never_changed' {
    $taz = New-Dir 'saved'
    $dir = Join-Path $taz 'Data\Profiles'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $f = Join-Path $dir 'default.json'
    $mine = '{"always_run": true, "game_window_size": "800, 600"}'
    [IO.File]::WriteAllText($f, $mine, (New-Object Text.UTF8Encoding $false))
    $wrote = Initialize-NewProfileDefaults -ProfilesDir $dir
    Assert-Equal $false $wrote 'says it left it'
    Assert-Equal $mine ([IO.File]::ReadAllText($f)) 'byte for byte'
}

Fact 'A_character_that_already_has_a_profile_is_left_alone' {
    $taz = New-Dir 'returning'
    $dir = Join-Path $taz 'Data\Profiles'
    $char = Join-Path $dir 'chase\Shattered Legacy\Chase'
    New-Item -ItemType Directory -Path $char -Force | Out-Null
    $p = Join-Path $char 'profile.json'
    $mine = '{"always_run": true}'
    [IO.File]::WriteAllText($p, $mine, (New-Object Text.UTF8Encoding $false))
    $null = Initialize-NewProfileDefaults -ProfilesDir $dir
    Assert-Equal $mine ([IO.File]::ReadAllText($p)) 'the character''s own profile.json untouched'
    Assert-True (Test-Path -LiteralPath (Join-Path $dir 'default.json')) 'the next new character still gets the default'
}

Fact 'Play_writes_the_new_profile_defaults_into_the_profiles_TazUO_reads' {
    $play = [IO.File]::ReadAllText((Join-Path $appSrc 'Play.ps1'))
    Assert-True ($play -match "Initialize-NewProfileDefaults\s+-ProfilesDir\s+\(Join-Path\s+\`$taz\s+'Data\\Profiles'\)") 'Play.ps1 calls it on app\tazuo\Data\Profiles'
    # The same folder Get-TazUOLaunch gives a -ClientDir client as -profilespath, so that client reads it too.
    $launch = Get-TazUOLaunch -TazDir 'C:\sl\app\tazuo' -Target ([pscustomobject]@{ Profile = ''; ClientDir = 'C:\other' }) -SettingsPath 'C:\sl\app\tazuo\settings.json'
    Assert-True (($launch.Arguments -join ' ') -match [regex]::Escape('-profilespath "C:\sl\app\tazuo\Data\Profiles"')) ($launch.Arguments -join ' ')
}

Fact 'The_default_keys_are_the_client_properties_TazUO_saves' {
    $profileCs = 'src/ClassicUO.Client/Configuration/Profile.cs'
    $patch = Join-Path $repo 'client\patches\0009-new-character-defaults.patch'
    Assert-True (Test-Path -LiteralPath $patch) "client patch 0009 is there: $patch"
    $text = [IO.File]::ReadAllText($patch)
    Assert-True ($text.Contains("+++ b/$profileCs")) 'the patch adds to Profile.cs'
    $m = [regex]::Match($text, '(?m)^\+\s*public bool (\w+) \{ get; set => SetProperty\(ref field, value\); \} = true;.*cc-P719')
    Assert-True $m.Success 'a new bool profile property, on unless a profile says otherwise (so a returning player''s profile keeps opening it)'
    Assert-Equal 'sl_open_paperdoll_at_login' (ConvertTo-TazKey $m.Groups[1].Value) 'its saved key is the one default.json sets'
    Assert-True ($text -match "(?m)^\+.*if \(ProfileManager\.CurrentProfile\.$($m.Groups[1].Value)\)") 'LoginComplete asks for the paperdoll only when it is on'
    Assert-Equal 'always_run' (ConvertTo-TazKey 'AlwaysRun') 'TazUO''s own Profile.AlwaysRun'
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
