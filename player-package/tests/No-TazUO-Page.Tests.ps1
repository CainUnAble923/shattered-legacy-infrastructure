#Requires -Version 5.1
# Facts for cc-P729: a new character's first login opens no TazUO page. The page is TazUO's own "TazUO Version History"
# gump (VersionHistory: a changelog note, the TazUO Wiki and Discord links). TazUO 3212623f opens it when the world view
# is built, whenever the profile's LastVersionHistoryShown is not the client's version, then stores the version
# (src/ClassicUO.Client/Game/UI/Gumps/WorldViewportGump.cs:108-114; the version is TazUO.dll's AssemblyVersion,
# CUOEnviroment.Version). A new character's profile is built from <profiles>\default.json (ProfileManager.NewFromDefault),
# so the launcher writes the version it is starting into default.json (Launch-Settings.ps1, Initialize-NewProfileDefaults).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\No-TazUO-Page.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Needs nothing but a scratch folder: each client folder holds a
# stand-in TazUO.dll compiled here with a known AssemblyVersion.

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$pkg  = Split-Path $here -Parent
$appSrc = Join-Path $pkg 'payload\app'
$work = Join-Path $env:TEMP ("sl-no-tazuo-page-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
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

# A client folder whose TazUO.dll has AssemblyVersion $Version (the version TazUO compares, CUOEnviroment.Version).
function New-ClientDir([string]$name, [string]$Version) {
    $d = New-Dir $name
    Add-Type -TypeDefinition ("[assembly: System.Reflection.AssemblyVersion(""$Version"")] public class SlP729Stand" + $name.Replace('-', '') + " {}") `
        -OutputAssembly (Join-Path $d 'TazUO.dll') -OutputType Library
    return $d
}

# TazUO's rule at the first world view of a character (WorldViewportGump.cs:108): a NEW character's profile is
# default.json (or TazUO's own defaults when there is none, where LastVersionHistoryShown is null), and the page opens
# unless its last_version_history_shown is the client's version.
function Test-OpensVersionPage([string]$ProfilesDir, [string]$ClientVersion) {
    $f = Join-Path $ProfilesDir 'default.json'
    $shown = $null
    if (Test-Path -LiteralPath $f) { $shown = ([IO.File]::ReadAllText($f) | ConvertFrom-Json).last_version_history_shown }
    return ($shown -ne $ClientVersion)
}

$ours = '{"always_run": false, "sl_open_paperdoll_at_login": false}'   # what the P719 launcher wrote

Fact 'A_fresh_profile_opens_no_TazUO_page_at_start' {
    $client = New-ClientDir 'fresh' '5.31.2.0'
    $dir = Join-Path (New-Dir 'fresh-data') 'Profiles'
    Assert-True (Test-OpensVersionPage $dir '5.31.2.0') 'control: with no default.json TazUO opens its page'
    $null = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $client
    Assert-True (-not (Test-OpensVersionPage $dir '5.31.2.0')) 'a new character''s first login opens no TazUO page'
    $j = [IO.File]::ReadAllText((Join-Path $dir 'default.json')) | ConvertFrom-Json
    Assert-Equal '5.31.2.0' $j.last_version_history_shown 'the version of the TazUO.dll being started'
    Assert-Equal $false $j.always_run 'P719''s defaults kept'
    Assert-Equal $false $j.sl_open_paperdoll_at_login 'P719''s defaults kept'
    $bytes = [IO.File]::ReadAllBytes((Join-Path $dir 'default.json'))
    Assert-True (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) 'no BOM'
}

Fact 'Our_own_default_from_an_earlier_launcher_gets_the_version_too' {
    $client = New-ClientDir 'upgrade' '5.31.2.0'
    $dir = New-Dir 'upgrade-data'
    [IO.File]::WriteAllText((Join-Path $dir 'default.json'), $ours, (New-Object Text.UTF8Encoding $false))
    Assert-True (Test-OpensVersionPage $dir '5.31.2.0') 'control: P719''s file alone still opens the page'
    $wrote = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $client
    Assert-Equal $true $wrote 'it rewrote its own file'
    Assert-True (-not (Test-OpensVersionPage $dir '5.31.2.0')) 'no page for the next new character'
    $j = [IO.File]::ReadAllText((Join-Path $dir 'default.json')) | ConvertFrom-Json
    Assert-Equal $false $j.always_run 'still walks'
}

Fact 'A_newer_TazUO_moves_the_version_in_our_own_default' {
    $old = New-ClientDir 'v1' '5.31.2.0'
    $new = New-ClientDir 'v2' '5.40.0.0'
    $dir = New-Dir 'bump-data'
    $null = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $old
    $null = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $new
    Assert-True (-not (Test-OpensVersionPage $dir '5.40.0.0')) 'after a client update a new character still opens no page'
    $again = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $new
    Assert-Equal $false $again 'nothing to write when it already says so'
}

Fact 'A_default_profile_the_player_saved_is_never_changed' {
    $client = New-ClientDir 'saved' '5.31.2.0'
    $dir = New-Dir 'saved-data'
    $f = Join-Path $dir 'default.json'
    # Options > Profile, "Set as default" saves a whole profile: keys the launcher never writes.
    $mine = '{"always_run": true, "game_window_size": "800, 600", "last_version_history_shown": "5.20.0.0"}'
    [IO.File]::WriteAllText($f, $mine, (New-Object Text.UTF8Encoding $false))
    $wrote = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $client
    Assert-Equal $false $wrote 'says it left it'
    Assert-Equal $mine ([IO.File]::ReadAllText($f)) 'byte for byte'
}

Fact 'A_character_that_already_has_a_profile_is_left_alone' {
    $client = New-ClientDir 'returning' '5.31.2.0'
    $dir = New-Dir 'returning-data'
    $char = Join-Path $dir 'chase\Shattered Legacy\Chase'
    New-Item -ItemType Directory -Path $char -Force | Out-Null
    $p = Join-Path $char 'profile.json'
    $mine = '{"always_run": true, "last_version_history_shown": "5.20.0.0"}'
    [IO.File]::WriteAllText($p, $mine, (New-Object Text.UTF8Encoding $false))
    $null = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $client
    Assert-Equal $mine ([IO.File]::ReadAllText($p)) 'the character''s own profile.json untouched'
}

Fact 'A_client_folder_with_no_readable_TazUO_dll_still_gets_the_other_defaults' {
    $client = New-Dir 'no-dll'
    $dir = New-Dir 'no-dll-data'
    $null = Initialize-NewProfileDefaults -ProfilesDir $dir -ClientDir $client
    $j = [IO.File]::ReadAllText((Join-Path $dir 'default.json')) | ConvertFrom-Json
    Assert-Equal $false $j.always_run 'P719''s defaults written'
    Assert-True ($null -eq $j.last_version_history_shown) 'no version it could not read'
}

Fact 'Play_passes_the_folder_of_the_client_it_starts' {
    $play = [IO.File]::ReadAllText((Join-Path $appSrc 'Play.ps1'))
    Assert-True ($play -match "Initialize-NewProfileDefaults\s+-ProfilesDir\s+\(Join-Path\s+\`$taz\s+'Data\\Profiles'\)\s+-ClientDir\s+\`$clientDir") 'Play.ps1 gives it the client folder'
    Assert-True ($play -match "\`$clientDir\s*=\s*\`$taz") 'the package client by default'
    Assert-True ($play -match "if\s*\(\`$target\.ClientDir\)\s*\{\s*\`$clientDir\s*=\s*\`$target\.ClientDir") 'a -ClientDir client is the one it reads'
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
