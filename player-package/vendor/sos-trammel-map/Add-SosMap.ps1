#Requires -Version 5.1
<#
  cc-P674 (SoS swap S1): makes files\ (git-ignored) from Zigholtul's Siege of Sosaria Trammel Map download, for the
  player package. SOURCE.txt says what it is; manifest.csv is every file's size and SHA-256.

      .\Add-SosMap.ps1 [-Source <the download folder>] [-MapOps <docs repo>\scripts\mapbuild\mapops.py] [-Python python]

  1. Checks the author's Map1.mul, Staidx1.mul and Statics1.mul (manifest kind source) by size and SHA-256.
  2. Copies Statics1.mul and Staidx1.mul to files\base-statics1.mul and files\base-staidx1.mul.
  3. Packs Map1.mul as facet 1's UOP, files\base-map1legacymul.uop (mapops.py pack: 112 entries of 4,096 blocks).
  4. Checks every file it wrote against the manifest (kind ship). On any mismatch it deletes what it wrote and stops.
  A file already there and matching is left alone. ASCII only.
#>
[CmdletBinding()]
param(
    [string]$Source = 'D:\UO\third-party\sos-trammel-map',
    [string]$MapOps = 'D:\UO\shard-migration\scripts\mapbuild\mapops.py',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$files = Join-Path $here 'files'
$rows = @(Get-Content -LiteralPath (Join-Path $here 'manifest.csv') | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv)

# True when the file is there and is the manifest row's size and SHA-256.
function Test-SosFile([string]$path, $row) {
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    if ((Get-Item -LiteralPath $path).Length -ne [int64]$row.bytes) { return $false }
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq "$($row.sha256)".ToUpperInvariant()
}

function Get-Row([string]$kind, [string]$file) {
    $r = @($rows | Where-Object { $_.kind -eq $kind -and $_.file -eq $file })
    if ($r.Count -ne 1) { throw "manifest.csv has $($r.Count) $kind rows for $file" }
    return $r[0]
}

foreach ($n in 'Map1.mul', 'Staidx1.mul', 'Statics1.mul') {
    $row = Get-Row 'source' $n
    $p = Join-Path $Source $row.from
    if (-not (Test-SosFile $p $row)) { throw "$p is missing or is not the file manifest.csv records (size and SHA-256). Not copying." }
}
New-Item -ItemType Directory -Path $files -Force | Out-Null

$plan = @(
    @{ Ship = 'base-statics1.mul'; From = 'Statics1.mul'; Pack = $false },
    @{ Ship = 'base-staidx1.mul'; From = 'Staidx1.mul'; Pack = $false },
    @{ Ship = 'base-map1legacymul.uop'; From = 'Map1.mul'; Pack = $true }
)
foreach ($p in $plan) {
    $row = Get-Row 'ship' $p.Ship
    $out = Join-Path $files $p.Ship
    if (Test-SosFile $out $row) { Write-Host "  $($p.Ship): already there and right."; continue }
    $src = Join-Path $Source (Get-Row 'source' $p.From).from
    try {
        if ($p.Pack) {
            if (-not (Test-Path -LiteralPath $MapOps)) { throw "no mapops.py at $MapOps (the docs repo's scripts\mapbuild\mapops.py; pass -MapOps)" }
            & $Python -P $MapOps pack $src $out 1
            if ($LASTEXITCODE -ne 0) { throw "mapops.py pack failed (exit $LASTEXITCODE)" }
        } else {
            Copy-Item -LiteralPath $src -Destination $out -Force
        }
        if (-not (Test-SosFile $out $row)) { throw "$($p.Ship) is not the file manifest.csv records (size and SHA-256)" }
        Write-Host "  $($p.Ship): written and checked." -ForegroundColor Green
    } catch {
        Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue
        throw
    }
}
Write-Host "files\ matches manifest.csv: the package build can ship the Siege of Sosaria map." -ForegroundColor Green
