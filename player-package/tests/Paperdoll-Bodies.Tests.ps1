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
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host "$($results.Count - $failed) of $($results.Count) facts passed."
exit $failed
