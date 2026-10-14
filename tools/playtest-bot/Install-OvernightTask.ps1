#Requires -Version 5.1
<#
  cc-P102: registers the nightly play-test run as a Windows scheduled task. Chase runs it once; nothing else registers it.

      powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\tools\playtest-bot\Install-OvernightTask.ps1
          [-DryRun] [-Uninstall]

  The task, "SL overnight play tests" (taskName in overnight.json): daily at 01:30 (scheduleTime), as the user who runs
  this, only when that user is logged on (so docker desktop is up and the DPAPI mail credential can be read), waking
  the PC if Windows allows it (-WakeToRun; the power plan's "Allow wake timers" decides), one at a time (IgnoreNew),
  stopped after 6 hours. It runs Run-OvernightPlaytests.ps1 with no arguments: the script decides whether the night has
  work. -Uninstall removes the task. -DryRun prints the call and changes nothing. ASCII only.
#>
[CmdletBinding()]
param([switch]$DryRun, [switch]$Uninstall, [string]$Config)   # default: overnight.json beside this script
$ErrorActionPreference = 'Stop'
if (-not $Config) { $Config = Join-Path $PSScriptRoot 'overnight.json' }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
$name = [string]$cfg.taskName
$at = [string]$cfg.scheduleTime
$runner = Join-Path $PSScriptRoot 'Run-OvernightPlaytests.ps1'
$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$tag = $(if ($DryRun) { 'DRY RUN: ' } else { '' })

if ($Uninstall) {
    Write-Host ($tag + "Unregister-ScheduledTask -TaskName '" + $name + "' -Confirm:`$false")
    if (-not $DryRun) {
        if (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue) {
            Unregister-ScheduledTask -TaskName $name -Confirm:$false
            Write-Host ('Removed the scheduled task "' + $name + '".') -ForegroundColor Green
        } else { Write-Host ('There is no scheduled task "' + $name + '": nothing to remove.') }
    }
    exit 0
}

$arg = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $runner + '"'
# Each element in parentheses: in PowerShell the comma binds tighter than +.
$lines = @(
    ("`$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '" + $arg + "' -WorkingDirectory '" + $PSScriptRoot + "'"),
    ("`$trigger = New-ScheduledTaskTrigger -Daily -At '" + $at + "'"),
    ("`$settings = New-ScheduledTaskSettingsSet -WakeToRun -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 6) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries"),
    ("`$principal = New-ScheduledTaskPrincipal -UserId '" + $user + "' -LogonType Interactive -RunLevel Limited"),
    ("Register-ScheduledTask -TaskName '" + $name + "' -Action `$action -Trigger `$trigger -Settings `$settings -Principal `$principal -Description 'cc-P102: the overnight play-test bot, only on nights with work' -Force")
)
Write-Host ($tag + 'registering "' + $name + '" daily at ' + $at + ' as ' + $user + ':')
$lines | ForEach-Object { Write-Host ('  ' + $_) }
if ($DryRun) { Write-Host 'DRY RUN: nothing registered.'; exit 0 }

$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -Daily -At $at
$settings = New-ScheduledTaskSettingsSet -WakeToRun -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 6) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'cc-P102: the overnight play-test bot, only on nights with work' -Force | Out-Null
$t = Get-ScheduledTask -TaskName $name
Write-Host ('Registered "' + $name + '": ' + $t.State + ', next run ' + (Get-ScheduledTaskInfo -TaskName $name).NextRunTime) -ForegroundColor Green
Write-Host 'Wake timers: Control Panel > Power Options > Change plan settings > Advanced > Sleep > Allow wake timers = Enable.'
