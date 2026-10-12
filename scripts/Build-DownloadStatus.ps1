<#
.SYNOPSIS
    Emits downloads.json for the Progress page and the LAN dashboard, from the art and code
    download queue (D:\UO\third-party\download-queue.md).

.DESCRIPTION
    servuo.dev allows 5 downloads a day, so the "ServUO art downloads" scheduled task works the
    queue top down every night. This script turns that queue into counts so progress shows up
    without anyone opening the markdown.

    Status is read from each table row's second column:
      done ...            downloaded
      todo                waiting its turn
      blocked: ...        needs something first (the Vita-Nex Vault Pass)
      failed: ...         the nightly task could not get it
      skip ...            Chase passed on it
    Rows whose Folder starts with code/ count as scripts; everything else counts as art.

    Only counts and the titles of the next few public servuo.dev resources are published.
    No terms, notes, paths or "Held for Chase" text.

.NOTES
    Writes BOM-less UTF-8 (PowerShell 5.1's -Encoding UTF8 writes a BOM). Run by the nightly
    download task after it finishes, and safe to run by hand any time.
#>
[CmdletBinding()]
param(
    [string] $Queue   = 'D:\UO\third-party\download-queue.md',
    [string] $OutFile = 'D:\ShatteredLegacy\server\lib\uo\modernuo\status\downloads.json',
    [int]    $PerDay  = 5,
    [int]    $NextCount = 5
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Queue)) { throw "Queue not found: $Queue" }

function New-Bucket { [ordered]@{ done = 0; todo = 0; blocked = 0; failed = 0; skipped = 0; total = 0; percent = 0 } }
$art  = New-Bucket
$code = New-Bucket
$next = New-Object System.Collections.Generic.List[object]
$lastDone = $null

foreach ($line in (Get-Content -LiteralPath $Queue -Encoding UTF8)) {
    # | 12 | todo | Title | slug.id | folder | For |
    if ($line -notmatch '^\|\s*\d+\s*\|') { continue }
    $c = $line.Split('|') | ForEach-Object { $_.Trim() }
    if ($c.Count -lt 7) { continue }
    $status = $c[2]; $title = $c[3]; $folder = $c[5]
    $b = if ($folder -like 'code/*') { $code } else { $art }
    $b.total++
    switch -Regex ($status) {
        '^done'    { $b.done++
                     if ($status -match '(\d{4}-\d{2}-\d{2})' -and ($null -eq $lastDone -or $Matches[1] -gt $lastDone)) { $lastDone = $Matches[1] }
                     break }
        '^todo$'   { $b.todo++
                     if ($next.Count -lt $NextCount) { $next.Add([ordered]@{ title = $title; kind = $(if ($b -eq $code) { 'code' } else { 'art' }) }) }
                     break }
        '^blocked' { $b.blocked++; break }
        '^failed'  { $b.failed++; break }
        '^skip'    { $b.skipped++; break }
        default    { $b.todo++ }
    }
}

foreach ($b in @($art, $code)) {
    $wanted = $b.total - $b.skipped
    $b.percent = if ($wanted -gt 0) { [math]::Round($b.done * 100.0 / $wanted, 1) } else { 0 }
}
$todoAll = $art.todo + $code.todo

$payload = [ordered]@{
    schema      = 1
    generatedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    perDay      = $PerDay
    lastDownload = $lastDone
    nightsLeft  = [math]::Ceiling($todoAll / [double]$PerDay)
    art         = $art
    code        = $code
    next        = $next
}
$json = $payload | ConvertTo-Json -Depth 5
$dir = [System.IO.Path]::GetDirectoryName($OutFile)
if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllText($OutFile, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Wrote $OutFile"
Write-Host $json