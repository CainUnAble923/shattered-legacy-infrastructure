#Requires -Version 5.1
<#
  Invoke-WebsiteDeploy.ps1 (cc-P101) - runs ONE named Haven deploy script from scripts\websites.json's "deploys"
  allowlist, in this window, and keeps everything it prints.

      .\scripts\Invoke-WebsiteDeploy.ps1 -Deploy website            run it
      .\scripts\Invoke-WebsiteDeploy.ps1 -Deploy website -DryRun    print the exact command and run nothing

  The Shard Console's Websites tab opens this in a window of its own after Chase types the phrase 'deploy to haven'.
  It is a second check, not the only one: the console refuses a name that is not in the allowlist, and so does this.
  The deploy scripts themselves (D:\UO\haven-migration) reach Haven with scp and ssh, so they use Chase's own SSH key
  (the agent or ~\.ssh). Nothing here holds or asks for a key, password or token.

  The transcript goes to scripts\shard-console-output\deploys\<time>_<name>.log (git-ignored: *.log).
  Exit: 0 the script ran and exited 0; 1 it failed; 2 refused (not in the allowlist, or not found).
  ASCII only on purpose (PowerShell 5.1).
#>
param(
    [Parameter(Mandatory = $true)][string]$Deploy,
    [switch]$DryRun,
    [string]$ConfigFile,      # default: websites.json beside this script
    [string]$LogFolder,       # default: shard-console-output\deploys beside this script
    # Only scripts in this folder may run, whatever the config says.
    [string]$AllowedFolder = 'D:\UO\haven-migration'
)
$ErrorActionPreference = 'Stop'
# Set here, not as parameter defaults: PowerShell 5.1 has no $PSScriptRoot while it binds parameters.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ConfigFile) { $ConfigFile = Join-Path $here 'websites.json' }
if (-not $LogFolder) { $LogFolder = Join-Path $here 'shard-console-output\deploys' }

function Refuse([string]$why) { Write-Host ('REFUSED: ' + $why) -ForegroundColor Red; exit 2 }

if ($Deploy -notmatch '^[a-z0-9][a-z0-9-]{0,39}$') { Refuse "'$Deploy' is not a deploy name (lower-case letters, digits and -)" }
try { $cfg = Get-Content -LiteralPath $ConfigFile -Raw | ConvertFrom-Json } catch { Refuse ("cannot read ${ConfigFile}: " + $_.Exception.Message) }
$entry = $null
if ($cfg.deploys -and $cfg.deploys.PSObject.Properties.Name -ccontains $Deploy) { $entry = $cfg.deploys.$Deploy }
if (-not $entry -or -not [string]$entry.script) {
    $names = @(); if ($cfg.deploys) { $names = @($cfg.deploys.PSObject.Properties.Name) }
    Refuse ("'$Deploy' is not in the deploys allowlist in $ConfigFile (it has: " + $(if ($names.Count) { $names -join ', ' } else { 'nothing' }) + ')')
}
$script = [IO.Path]::GetFullPath([string]$entry.script)
$allowed = [IO.Path]::GetFullPath($AllowedFolder).TrimEnd('\') + '\'
if (-not $script.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or $script -notmatch '\.ps1$') { Refuse "$script is not a .ps1 in $allowed" }
if (-not (Test-Path -LiteralPath $script -PathType Leaf)) { Refuse "$script was not found" }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $LogFolder ($stamp + '_' + $Deploy + '.log')
Write-Host ('Deploy ' + $Deploy + ': ' + [string]$entry.what) -ForegroundColor Cyan
Write-Host ('  command : & ' + "'" + $script + "'")
Write-Host ('  log     : ' + $log)
if ($DryRun) { Write-Host 'DRY RUN: nothing was run.' -ForegroundColor Yellow; exit 0 }

New-Item -ItemType Directory -Force -Path $LogFolder | Out-Null
Start-Transcript -LiteralPath $log | Out-Null
$code = 1
try {
    & $script
    $code = $(if ($?) { $LASTEXITCODE } else { 1 })
    if ($null -eq $code) { $code = 0 }
} catch {
    Write-Host ('FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    $code = 1
} finally {
    Write-Host ('exit ' + $code)
    Stop-Transcript | Out-Null
}
if ($code -eq 0) { Write-Host ('Done. Everything it printed is in ' + $log) -ForegroundColor Green } else { Write-Host ('FAILED (exit ' + $code + '). Read ' + $log) -ForegroundColor Red; $code = 1 }
exit $code
