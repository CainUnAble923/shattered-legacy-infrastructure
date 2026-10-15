#Requires -Version 5.1
# Facts for payload\app\Paperdoll-Bodies.ps1 (cc-P81): the paperdoll body gumps are SFW by default, and a local add-on
# swaps them only on a computer that has one.
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Paperdoll-Bodies.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Launch-Settings.Tests.ps1. Works in a scratch
# folder with stand-in files (a few bytes each, no art), so it needs nothing else.

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$appSrc = Join-Path (Split-Path $here -Parent) 'payload\app'
$work = Join-Path $env:TEMP ("sl-paperdoll-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))

. (Join-Path $appSrc 'Paperdoll-Bodies.ps1')

$results = New-Object 'System.Collections.Generic.List[object]'
function Fact {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true })
        Write-Host "  PASS  $Name" -ForegroundColor Green
    } catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false })
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
    }
}
function Assert-True([bool]$c, [string]$m) { if (-not $c) { throw $m } }

$ids = @(12, 13, 14, 15, 665, 666)

# An install: app\tazuo\ExternalImages\gumps and app\paperdoll-sfw hold the six SFW stand-ins, as the package ships.
function New-Install {
    $root = Join-Path $work ([Guid]::NewGuid().ToString('N').Substring(0, 6))
    $app = Join-Path $root 'app'
    $g = Join-Path $app 'tazuo\ExternalImages\gumps'
    $s = Join-Path $app 'paperdoll-sfw'
    New-Item -ItemType Directory -Path $g, $s -Force | Out-Null
    foreach ($id in $ids) {
        [IO.File]::WriteAllText((Join-Path $g "$id.png"), "sfw-$id")
        [IO.File]::WriteAllText((Join-Path $s "$id.png"), "sfw-$id")
    }
    return [pscustomobject]@{ Root = $root; App = $app; Gumps = $g }
}
function New-Addon([string]$root, [string]$mode) {
    $d = Join-Path $root "client-addons\paperdoll-nsfw\$mode"
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    foreach ($id in $ids) { [IO.File]::WriteAllText((Join-Path $d "$id.png"), "$mode-$id") }
}
function Bodies($i) { $ids | ForEach-Object { [IO.File]::ReadAllText((Join-Path $i.Gumps "$_.png")) } }

try {
    Fact 'A fresh install is SFW and stays SFW' {
        $i = New-Install
        Assert-True ((Get-PaperdollSetting -Root $i.Root).Mode -eq 'sfw') 'the default mode is not sfw'
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'sfw-*' }).Count -eq 0) 'a body is not SFW'
        Assert-True (-not (Test-Path (Join-Path $i.Root 'paperdoll-bodies.txt'))) 'a setting file appeared on its own'
    }

    Fact 'Without the add-on the setting does nothing' {
        $i = New-Install
        $line = Set-PaperdollSetting -Root $i.Root -Mode 'topless'
        Assert-True ($line -like 'No local paperdoll add-on*') "the switch was not refused: $line"
        Assert-True ((Get-PaperdollSetting -Root $i.Root).Mode -eq 'sfw') 'the mode changed'
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'sfw-*' }).Count -eq 0) 'a body changed'
    }

    Fact 'With the add-on the chosen set is swapped in, and sfw puts the shipped six back' {
        $i = New-Install
        New-Addon $i.Root 'topless'
        New-Addon $i.Root 'nude'
        $null = Set-PaperdollSetting -Root $i.Root -Mode 'nude'
        $line = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True ($line -like '*local nude set*') "no line for the local set: $line"
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'nude-*' }).Count -eq 0) 'the nude set did not go in'

        $null = Set-PaperdollSetting -Root $i.Root -Mode 'sfw'
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'sfw-*' }).Count -eq 0) 'sfw was not restored'
    }

    Fact 'A removed add-on falls back to SFW and says so' {
        $i = New-Install
        New-Addon $i.Root 'topless'
        $null = Set-PaperdollSetting -Root $i.Root -Mode 'topless'
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Remove-Item -LiteralPath (Join-Path $i.Root 'client-addons') -Recurse -Force
        $line = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True ($line -like '*add-on is missing*') "no fallback line: $line"
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'sfw-*' }).Count -eq 0) 'not back to SFW'
    }

    Fact 'An add-on folder elsewhere can be named' {
        $i = New-Install
        $elsewhere = Join-Path $work 'elsewhere'
        New-Addon $elsewhere 'topless'
        $folder = Join-Path $elsewhere 'client-addons\paperdoll-nsfw'
        $null = Set-PaperdollSetting -Root $i.Root -Mode 'topless' -Folder $folder
        Assert-True ((Get-PaperdollSetting -Root $i.Root).Folder -eq $folder) 'the folder was not kept'
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'topless-*' }).Count -eq 0) 'the named folder was not used'
    }

    # cc-P111: a client with the in-game option. The build stamp is the line client\Build-Client.ps1 writes.
    function Set-Stamp($i, [string[]]$patches) {
        $lines = @('TazUO upstream 3212623f63436be1c0f4e3b308b202b29597c026 built 2026-10-14 17:35') + @($patches | ForEach-Object { "patch $_ sha256 $('0' * 64)" })
        Set-Content -LiteralPath (Join-Path $i.App 'tazuo\sl-build.txt') -Value $lines -Encoding ASCII
    }

    Fact 'Only a client built with the paperdoll option patch counts as having it' {
        $i = New-Install
        Assert-True (-not (Test-ClientPaperdollOption -App $i.App)) 'no stamp (the stock TazUO) counted'
        Set-Stamp $i @('0001-enhanced-packet-client-command.patch')
        Assert-True (-not (Test-ClientPaperdollOption -App $i.App)) 'a build from before the option counted'
        Set-Stamp $i @('0001-enhanced-packet-client-command.patch', '0002-paperdoll-bodies-option.patch')
        Assert-True (Test-ClientPaperdollOption -App $i.App) 'our build with the option did not count'
    }

    Fact 'With the in-game option the launcher stands down: SFW files back, the choice left to the client' {
        $i = New-Install
        New-Addon $i.Root 'nude'
        $null = Set-PaperdollSetting -Root $i.Root -Mode 'nude'
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'nude-*' }).Count -eq 0) 'setup: the old path should swap nude in'
        Set-Stamp $i @('0002-paperdoll-bodies-option.patch')
        $line = Sync-PaperdollBodies -App $i.App -Root $i.Root
        Assert-True (@(Bodies $i | Where-Object { $_ -notlike 'sfw-*' }).Count -eq 0) 'the shipped SFW six were not put back'
        Assert-True ($line -like '*chosen in the game now*') "no line saying where the choice is now: $line"
        $null = Set-PaperdollSetting -Root $i.Root -Mode 'sfw'
        Assert-True ($null -eq (Sync-PaperdollBodies -App $i.App -Root $i.Root)) 'an SFW install with the option should say nothing'
    }

    Fact 'With the in-game option a named add-on folder is carried into the launch settings, once' {
        $i = New-Install
        Set-Stamp $i @('0002-paperdoll-bodies-option.patch')
        $folder = Join-Path $work 'elsewhere2\client-addons\paperdoll-nsfw'
        New-Addon (Join-Path $work 'elsewhere2') 'topless'
        $null = Set-PaperdollSetting -Root $i.Root -Mode 'topless' -Folder $folder
        $settings = Join-Path $i.App 'tazuo\settings.test.json'
        [IO.File]::WriteAllText($settings, '{"username":"keepme","port":2594}')
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root -SettingsPath $settings
        $j = [IO.File]::ReadAllText($settings) | ConvertFrom-Json
        Assert-True ($j.sl_paperdoll_folder -eq $folder) "the folder was not carried: $($j.sl_paperdoll_folder)"
        Assert-True ($j.username -eq 'keepme' -and $j.port -eq 2594) 'other settings were not kept'
        Assert-True (-not $j.PSObject.Properties['sl_paperdoll_bodies']) 'the mode was carried; the client chooses it'
        $j | Add-Member -NotePropertyName sl_paperdoll_folder -NotePropertyValue 'D:\set-in-game' -Force
        [IO.File]::WriteAllText($settings, ($j | ConvertTo-Json))
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root -SettingsPath $settings
        Assert-True ((([IO.File]::ReadAllText($settings)) | ConvertFrom-Json).sl_paperdoll_folder -eq 'D:\set-in-game') 'a folder already set was overwritten'
    }
    # cc-P114: Play.ps1 -Test -ClientDir starts our build from its own folder, with the package's settings file.
    Fact 'A client started from another folder is the one checked, and is told where the install''s add-on is' {
        $i = New-Install
        Remove-Item -LiteralPath (Join-Path $i.App 'paperdoll-sfw') -Recurse -Force   # an install from before cc-P81
        New-Addon $i.Root 'topless'
        $client = Join-Path $work ([Guid]::NewGuid().ToString('N').Substring(0, 6) + '-ourbuild')
        $cg = Join-Path $client 'ExternalImages\gumps'
        New-Item -ItemType Directory -Path $cg -Force | Out-Null
        foreach ($id in $ids) { [IO.File]::WriteAllText((Join-Path $cg "$id.png"), "sfw-$id") }
        $stamp = @('TazUO upstream 3212623f63436be1c0f4e3b308b202b29597c026 built 2026-10-14 17:35', "patch 0002-paperdoll-bodies-option.patch sha256 $('0' * 64)")
        Set-Content -LiteralPath (Join-Path $client 'sl-build.txt') -Value $stamp -Encoding ASCII
        Assert-True (Test-ClientPaperdollOption -TazDir $client) 'the client folder''s stamp was not read'
        Assert-True (-not (Test-ClientPaperdollOption -App $i.App)) 'the package''s stock client counted'

        $settings = Join-Path $i.App 'tazuo\settings.test.human.json'
        [IO.File]::WriteAllText($settings, '{"username":"keepme","autologin":true}')
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root -SettingsPath $settings -TazDir $client
        $j = [IO.File]::ReadAllText($settings) | ConvertFrom-Json
        $default = Join-Path $i.Root 'client-addons\paperdoll-nsfw'
        Assert-True ($j.sl_paperdoll_folder -eq $default) "the install's add-on was not named for a client that cannot find it beside itself: [$($j.sl_paperdoll_folder)]"
        Assert-True ($j.username -eq 'keepme' -and $j.autologin) 'other settings were not kept'
        Assert-True (@($ids | ForEach-Object { [IO.File]::ReadAllText((Join-Path $cg "$_.png")) } | Where-Object { $_ -notlike 'sfw-*' }).Count -eq 0) 'the client''s bodies are not SFW'

        # A client elsewhere, and no add-on in the install: nothing is named.
        $j2 = Join-Path $i.App 'tazuo\settings.test.elf.json'
        [IO.File]::WriteAllText($j2, '{"username":"elf"}')
        Remove-Item -LiteralPath (Join-Path $i.Root 'client-addons') -Recurse -Force
        $null = Sync-PaperdollBodies -App $i.App -Root $i.Root -SettingsPath $j2 -TazDir $client
        Assert-True (-not (([IO.File]::ReadAllText($j2)) | ConvertFrom-Json).PSObject.Properties['sl_paperdoll_folder']) 'a missing add-on was named'
    }
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host "$($results.Count - $failed) of $($results.Count) facts passed."
exit $failed
