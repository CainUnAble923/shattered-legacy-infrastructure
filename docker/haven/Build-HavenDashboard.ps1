#Requires -Version 5.1
<#
  cc-P133: writes Haven's dashboard config from the inventory.

      Build-HavenDashboard.ps1 [-Check] [-Discovery <file>] [-OutDir <folder>]

  Reads docker/haven/services.json and writes services.yaml and settings.yaml into docker/dashboard/haven/config (or
  -OutDir). -Check writes nothing and exits 1 when the files in the folder differ from what the inventory makes (the
  console facts run the same comparison). -Discovery adds a tile for any running Haven container the inventory does not
  list (the Mega dashboard deploy passes the discovery it just made), so a container nobody wrote down still shows.
  Exit 2 when the inventory itself is unsound (the problems are printed). ASCII only.
#>
[CmdletBinding()]
param([switch]$Check, [string]$Discovery, [string]$OutDir, [string]$Inventory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HavenInventory.ps1')
if (-not $Inventory) { $Inventory = Join-Path $PSScriptRoot 'services.json' }
if (-not $OutDir) { $OutDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'dashboard\haven\config' }
$inv = Read-HavenInventory -Path $Inventory
if ($inv.Problems.Count) { foreach ($p in $inv.Problems) { Write-Host ('INVENTORY: ' + $p) -ForegroundColor Red }; exit 2 }
$disc = $null
if ($Discovery) { $disc = ConvertFrom-HavenDiscovery -Text ([IO.File]::ReadAllText($Discovery)) }
$want = [ordered]@{
    'services.yaml' = (ConvertTo-HomepageServices -Inventory $inv -Discovery $disc) + "`n"
    'settings.yaml' = (ConvertTo-HomepageSettings -Inventory $inv) + "`n"
}
$differ = @()
foreach ($name in $want.Keys) {
    $path = Join-Path $OutDir $name
    $have = $(if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllText($path) } else { '' })
    if ($have -ne $want[$name]) { $differ += $name }
    if (-not $Check) { [IO.File]::WriteAllText($path, $want[$name], (New-Object Text.UTF8Encoding $false)) }
}
$tiles = @($inv.services | ForEach-Object { @($_.tiles) }).Count
if ($Check) {
    if ($differ) { Write-Host ('OUT OF DATE: ' + ($differ -join ', ') + ' in ' + $OutDir + ' differ from services.json; run Build-HavenDashboard.ps1') -ForegroundColor Red; exit 1 }
    Write-Host ('up to date: ' + @($inv.services).Count + ' services, ' + $tiles + ' tiles') -ForegroundColor Green; exit 0
}
Write-Host ('written: ' + ($want.Keys -join ', ') + ' in ' + $OutDir + ' (' + @($inv.services).Count + ' services, ' + $tiles + ' tiles' + $(if ($differ) { '; changed: ' + ($differ -join ', ') } else { '; no change' }) + ')')
exit 0
