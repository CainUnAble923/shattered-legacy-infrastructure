#Requires -Version 5.1
<#
  cc-P103: builds our TazUO: upstream at the commit in TAZUO_COMMIT, plus every patch in patches\, in file order.

      powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\client\Build-Client.ps1
          [-Source D:\UO\cc-p40-work\tazuo-3212623f] [-Out D:\UO\tazuo-sl-build] [-SkipTests]

  1. A detached git worktree of -Source (a TazUO clone with its submodules) at the pinned commit, in <Out>\src. The
     clone's own working tree is not touched.
  2. The six submodules (external\*) are copied from -Source, after checking each is at exactly the commit the pin
     records. Nothing is fetched.
  3. Our nuget.config (nuget.org only; this machine has no NuGet source, cc-P40 C.2).
  4. Each patch: git apply --check, then git apply. git apply takes no fuzz, so a patch that does not apply fails
     the build.
  5. The client's unit tests (dotnet test), unless -SkipTests.
  6. dotnet publish -c Release -r win-x64 into <Out>\client, and <Out>\client\sl-build.txt: the pin and each patch's
     SHA-256.
  Out stays outside both repos. The full pipeline (version stamp, smoke start, package gates) is cc-P40's P-a. ASCII only.
#>
[CmdletBinding()]
param(
    [string]$Source = 'D:\UO\cc-p40-work\tazuo-3212623f',
    [string]$Out = 'D:\UO\tazuo-sl-build',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Continue'
$here = $PSScriptRoot
function Step([string]$t) { Write-Host ('== ' + $t) -ForegroundColor Cyan }
function Fail([string]$t) { Write-Host ('BUILD FAILED: ' + $t) -ForegroundColor Red; exit 1 }

if ($Out -match 'ShatteredLegacy|shard-migration') { Fail 'the build output would be inside a repo' }
$pin = (Get-Content -LiteralPath (Join-Path $here 'TAZUO_COMMIT') -TotalCount 1).Trim()
if ($pin -notmatch '^[0-9a-f]{40}$') { Fail ('TAZUO_COMMIT is not a full commit: ' + $pin) }
$work = Join-Path $Out 'src'
$client = Join-Path $Out 'client'

Step ('worktree of ' + $Source + ' at ' + $pin)
& git -C $Source worktree remove --force $work 2>$null | Out-Null
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
& git -C $Source worktree add --detach $work $pin
if ($LASTEXITCODE -ne 0) { Fail 'git worktree add' }

Step 'submodules, copied from the source clone at the pinned commits'
$paths = @(& git -C $work config -f .gitmodules --get-regexp '\.path$' | ForEach-Object { ($_ -split ' ', 2)[1] })
foreach ($p in $paths) {
    $want = ((& git -C $work ls-tree $pin $p) -split '\s+')[2]
    $have = (& git -C (Join-Path $Source $p) rev-parse HEAD 2>$null)
    if ($want -ne $have) { Fail ("submodule $p is at $have in the source clone, the pin records $want") }
    $dst = Join-Path $work $p
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $Source $p) -Force | Where-Object { $_.Name -ne '.git' } |
        Copy-Item -Destination $dst -Recurse -Force
    Write-Host ("  $p $want")
}
Copy-Item -LiteralPath (Join-Path $here 'nuget.config') -Destination (Join-Path $work 'nuget.config') -Force

Step 'patches'
$patches = @(Get-ChildItem -LiteralPath (Join-Path $here 'patches') -Filter '*.patch' | Sort-Object Name)
foreach ($f in $patches) {
    & git -C $work apply --check $f.FullName
    if ($LASTEXITCODE -ne 0) { Fail ('patch does not apply: ' + $f.Name) }
    & git -C $work apply $f.FullName
    if ($LASTEXITCODE -ne 0) { Fail ('patch failed: ' + $f.Name) }
    Write-Host ('  applied ' + $f.Name)
}

if (-not $SkipTests) {
    Step 'client unit tests'
    & dotnet test (Join-Path $work 'tests\ClassicUO.UnitTests\ClassicUO.UnitTests.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { Fail 'unit tests' }
}

Step ('publish to ' + $client)
if (Test-Path -LiteralPath $client) { Remove-Item -LiteralPath $client -Recurse -Force }
& dotnet publish (Join-Path $work 'src\ClassicUO.Client\ClassicUO.Client.csproj') -c Release -r win-x64 -o $client --nologo
if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish' }
$lines = @('TazUO upstream ' + $pin, 'built ' + (Get-Date).ToString('yyyy-MM-dd HH:mm'))
foreach ($f in $patches) { $lines += ('patch ' + $f.Name + ' sha256 ' + (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) }
[IO.File]::WriteAllLines((Join-Path $client 'sl-build.txt'), [string[]]$lines)
Write-Host ('Built: ' + (Join-Path $client 'TazUO.exe')) -ForegroundColor Green
