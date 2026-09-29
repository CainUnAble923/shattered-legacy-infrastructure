<#
.SYNOPSIS
    Emits migration.json for the public migration page, from the bug list itself.

.DESCRIPTION
    The numbers on that page come from docs/bug-list.md and nowhere else. Nothing here
    is typed by hand, because a hand-typed number on a public page is a number that goes
    stale the first time somebody forgets to update it.

    The bug list has no status column, and its SECTIONS cannot be trusted to carry status
    either: as of 2026-09-29, six rows reading FIXED or CLOSED were still filed under
    "1. Open, ours, worth fixing" (D19, D20, D24, D26, D27, D28). Counting by section would
    have published 27 open defects when 21 was the truth.

    So status is read from the ROW TEXT. A row is closed if it says FIXED or CLOSED anywhere,
    or if it is filed under section 2 or 4. Everything else is open.

    The script also reports misfiled rows on the way past. That count is deliberately NOT
    published: it is a message to whoever runs this, not to the public.

    Only counts and headline figures are published. No defect text, no file paths, no
    internal notes. If you find yourself wanting to add a row's prose here, do not.

.NOTES
    Writes BOM-less UTF-8. PowerShell 5.1's -Encoding UTF8 writes a BOM, which anything
    reading this as JSON is entitled to choke on.
#>
[CmdletBinding()]
param(
    [string] $BugList = 'D:\UO\shard-migration\docs\bug-list.md',
    [string] $OutFile = 'D:\ShatteredLegacy\server\lib\uo\modernuo\status\migration.json',

    # From notes/reachability-audit.md. Passed in rather than parsed, because the audit
    # states them in prose and a regex over prose is a lie waiting to happen. Update these
    # when the audit is re-run, and say so in the notes when you do.
    [int] $PortedFiles = 570,
    [int] $UnreachableFiles = 340
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $BugList)) {
    throw "Bug list not found: $BugList"
}

$lines = Get-Content -LiteralPath $BugList -Encoding UTF8

$sectionOf = @{}
$closed    = @{}
$current   = $null
foreach ($line in $lines) {
    if ($line -match '^##\s+(\d+b?)\.') {
        $current = $Matches[1]
        continue
    }
    # A defect row looks like: | **D34** | ... |
    if ($current -and $line -match '^\|\s*\*\*(D\d+)\*\*') {
        $id = $Matches[1]
        $sectionOf[$id] = $current
        $closed[$id] = ($line -match '\bFIXED\b' -or $line -match '\bCLOSED\b' -or $current -in @('2','4'))
    }
}

$tracked    = $sectionOf.Count
$fixedCount = @($closed.GetEnumerator() | Where-Object { $_.Value }).Count
$openCount  = $tracked - $fixedCount

# Rows that read as fixed but are still filed under an "open" section. Not published.
$misfiled = @($closed.GetEnumerator() | Where-Object {
    $_.Value -and $sectionOf[$_.Key] -notin @('2','4')
}) | ForEach-Object { $_.Key } | Sort-Object

$counts = @{}
foreach ($s in '1','1b','2','3','4','5') {
    $counts[$s] = @($sectionOf.GetEnumerator() | Where-Object { $_.Value -eq $s }).Count
}

$reachable = $PortedFiles - $UnreachableFiles

$payload = [ordered]@{
    schema      = 1
    generatedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    defects     = [ordered]@{
        open              = $openCount
        fixed             = $fixedCount
        copiedOnPurpose   = $counts['3']
        upstream          = $counts['5']
        tracked           = $tracked
    }
    content     = [ordered]@{
        portedFiles      = $PortedFiles
        reachableFiles   = $reachable
        unreachableFiles = $UnreachableFiles
        reachablePercent = if ($PortedFiles -gt 0) { [math]::Round($reachable * 100.0 / $PortedFiles, 1) } else { 0 }
    }
}

$json = $payload | ConvertTo-Json -Depth 5

# Split-Path -LiteralPath -Parent is ambiguous in Windows PowerShell 5.1, so use .NET.
$dir = [System.IO.Path]::GetDirectoryName($OutFile)
if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllText($OutFile, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Wrote $OutFile"
Write-Host $json

if ($misfiled.Count -gt 0) {
    Write-Warning ("Filed under an open section but reading as fixed: {0}. " -f ($misfiled -join ', ') +
                   "The counts above are right; the bug list's own sections are not. Move these rows.")
}
