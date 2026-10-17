#Requires -Version 5.1
<#
  cc-P155: the status collector. Writes the status file both consoles read (Get-ConsoleConfig's StatusFile,
  scripts\shard-console-cache\console-status.json), with the Shard Console's own reads and judgments: it dot-sources
  Shard-Console.ps1 and calls the same Get-ConsoleRefreshData and Write-ConsoleStatusFile the console's refresh calls,
  so there is one copy of the logic. Read only: docker, git, processes, the Golem's files, play-tests.json, the
  websites and the DNS check. Like the console's refresh it emails a shard's new console question once (cc-P106).

      Write-ConsoleStatus.ps1                 one refresh, then exit
      Write-ConsoleStatus.ps1 -Loop [-IntervalSeconds 30] [-ParentPid <pid>]
                                              every IntervalSeconds, and at once when the request file appears
                                              (StatusFile + '.request', which the web console touches for "refresh
                                              now"); a turn is skipped while the Shard Console itself wrote the file
                                              less than IntervalSeconds ago, so the two never both gather. Ends when
                                              ParentPid (the web console) is gone.

  The web console (web-console\server.py) starts it with -Loop. ASCII only.
#>
[CmdletBinding()]
param([switch]$Loop, [int]$IntervalSeconds = 30, [int]$ParentPid = 0, [string]$RepoRoot, [string]$Out)
$ErrorActionPreference = 'Stop'
# Kept under names of their own: dot-sourcing the console runs its param block here, which resets its own names
# (RepoRoot among them) to their defaults.
$collect = [pscustomobject]@{ Loop = [bool]$Loop; Interval = $IntervalSeconds; Parent = $ParentPid; Out = $Out
    Root = $(if ($RepoRoot) { $RepoRoot } else { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }) }
. (Join-Path $PSScriptRoot 'Shard-Console.ps1') -LoadOnly
$config = Get-ConsoleConfig -RepoRoot $collect.Root
$Out = $(if ($collect.Out) { $collect.Out } else { $config.StatusFile })
$IntervalSeconds = $collect.Interval
$ParentPid = $collect.Parent
$request = $Out + '.request'

function Invoke-OneCollect {
    $data = Get-ConsoleRefreshData -Config $config -FromCapture $false -StatusFrom '' -Parts @('tiles', 'package') -Dry $false
    $net = $null
    try { $net = Get-NetworkProbe } catch { }
    $web = @{}
    try { $web = Get-WebsiteProbe (Get-WebsiteChecks $config) $script:WebsiteTimeoutSec } catch { }
    $p = Write-ConsoleStatusFile -Data $data -Config $config -NetResult $net -WebResults $web -By 'collector' -Path $Out
    Write-Host ((Get-Date -Format 's') + ' wrote ' + $p + ' in ' + $data.Timings['total'] + ' ms')
}

if (-not $collect.Loop) { Invoke-OneCollect; exit 0 }

$last = [datetime]::MinValue
while ($true) {
    if ($ParentPid -and -not (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue)) { exit 0 }
    $asked = Test-Path -LiteralPath $request
    $due = (([datetime]::UtcNow - $last).TotalSeconds -ge $IntervalSeconds)
    if ($asked -or $due) {
        if ($asked) { Remove-Item -LiteralPath $request -ErrorAction SilentlyContinue }
        $doc = Read-ConsoleStatusFile $Out
        if (-not $asked -and $doc -and $doc.generatedBy -eq 'console' -and $doc.AgeSeconds -lt $IntervalSeconds) {
            # The Shard Console is open and gathered just now: its file is as fresh as ours would be.
            $last = [datetime]::UtcNow.AddSeconds(-$doc.AgeSeconds)
        } else {
            try { Invoke-OneCollect } catch { Write-Host ((Get-Date -Format 's') + ' collect failed: ' + $_.Exception.Message) }
            $last = [datetime]::UtcNow
        }
    }
    Start-Sleep -Seconds 1
}
