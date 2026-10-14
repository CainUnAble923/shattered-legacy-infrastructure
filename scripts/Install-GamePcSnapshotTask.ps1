#Requires -Version 5.1
<#
  cc-P107: registers Publish-GamePcSnapshot.ps1 as a scheduled task, every minute, as the user who runs this (Chase).
  Chase runs it once; nothing else registers it.

      powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\scripts\Install-GamePcSnapshotTask.ps1 [-DryRun] [-Uninstall]

  The task "SL game PC snapshot" runs wscript.exe on Run-Hidden.vbs, so no window flashes each minute. It runs only while
  Chase is logged on (it reads D:\UO and docker as him), one at a time, stopped after 2 minutes. Before the cutover the
  publisher sends nothing, so the task can be installed any time. -DryRun prints the calls; -Uninstall removes it.
  Refuses to run as SYSTEM. ASCII only.
#>
[CmdletBinding()]
param([switch]$DryRun, [switch]$Uninstall)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: install it as Chase, never as SYSTEM.'; exit 2 }
$name = 'SL game PC snapshot'
$tag = $(if ($DryRun) { 'DRY RUN: ' } else { '' })
if ($Uninstall) {
    Write-Host ($tag + "Unregister-ScheduledTask -TaskName '" + $name + "' -Confirm:`$false")
    if (-not $DryRun -and (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue)) { Unregister-ScheduledTask -TaskName $name -Confirm:$false; Write-Host 'Removed.' }
    exit 0
}
$vbs = Join-Path $PSScriptRoot 'Run-Hidden.vbs'
$ps1 = Join-Path $PSScriptRoot 'Publish-GamePcSnapshot.ps1'
$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$arg = '//B //Nologo "' + $vbs + '" "' + $ps1 + '"'
$lines = @(
    ("`$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument '" + $arg + "'"),
    ("`$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 1)"),
    ("`$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries"),
    ("`$principal = New-ScheduledTaskPrincipal -UserId '" + $user + "' -LogonType Interactive -RunLevel Limited"),
    ("Register-ScheduledTask -TaskName '" + $name + "' -Action `$action -Trigger `$trigger -Settings `$settings -Principal `$principal -Description 'cc-P107: the game PC snapshot to Haven, every minute' -Force")
)
Write-Host ($tag + 'registering "' + $name + '" every minute as ' + $user + ':')
$lines | ForEach-Object { Write-Host ('  ' + $_) }
if ($DryRun) { Write-Host 'DRY RUN: nothing registered.'; exit 0 }
$action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument $arg
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 1)
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'cc-P107: the game PC snapshot to Haven, every minute' -Force | Out-Null
Write-Host ('Registered "' + $name + '".') -ForegroundColor Green
