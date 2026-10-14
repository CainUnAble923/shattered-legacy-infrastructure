#Requires -Version 5.1
# cc-P108. Reads scripts\live.json, the one switch for the live shard, for every script that knows about live.
# Dot-source it, then Get-LiveSwitch. Pure apart from reading the one file; ASCII only (PowerShell 5.1).
#
# Missing or unreadable, the switch reads as retired: the safe side is never to start, probe or deploy to live
# by accident. Problem says why, so a broken file is visible rather than silently live.

$script:LiveRetiredText = 'Live is retired'

function ConvertFrom-LiveSwitchJson {
    # The switch from live.json's text. $null or broken text gives retired, with Problem set.
    param([string]$Json)
    $r = [ordered]@{ Enabled = $false; RetiredOn = '2026-10-14'; Returns = 'returns as a fresh 1.0 world'; Problem = $null }
    if (-not $Json) { $r.Problem = 'scripts\live.json is missing: live reads as retired.' }
    else {
        try {
            $d = $Json | ConvertFrom-Json
            if ($null -eq $d.liveEnabled -or -not ($d.liveEnabled -is [bool])) { $r.Problem = 'scripts\live.json has no true/false liveEnabled: live reads as retired.' }
            else { $r.Enabled = [bool]$d.liveEnabled }
            if ($d.retiredOn) { $r.RetiredOn = [string]$d.retiredOn }
            if ($d.returns) { $r.Returns = [string]$d.returns }
        } catch { $r.Problem = 'scripts\live.json is not JSON (' + $_.Exception.Message + '): live reads as retired.' }
    }
    $o = [pscustomobject]$r
    # The words the console's LIVE tile and every refusal use, in one place.
    $o | Add-Member -NotePropertyName Line -NotePropertyValue ('retired ' + $o.RetiredOn + ' (' + $o.Returns + ')')
    $o | Add-Member -NotePropertyName Refusal -NotePropertyValue ($script:LiveRetiredText + ' (' + $o.RetiredOn + ', ' + $o.Returns + '). scripts\live.json liveEnabled is false.')
    $o
}

function Get-LiveSwitch {
    param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)
    $f = Join-Path $RepoRoot 'scripts\live.json'
    $t = $null
    if (Test-Path -LiteralPath $f) { try { $t = [IO.File]::ReadAllText($f) } catch { $t = $null } }
    ConvertFrom-LiveSwitchJson $t
}
