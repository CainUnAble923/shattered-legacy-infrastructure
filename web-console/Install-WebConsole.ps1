#Requires -Version 5.1
<#
  cc-P155: installs the web console on the game PC. Chase runs it; CC never does (it would start a service).

      Install-WebConsole.ps1 [-DryRun]                 the service: a scheduled task "SL web console" that starts
                                                       web-console\server.py with pythonw (no window) at Chase's logon,
                                                       and starts it now. It listens on 127.0.0.1:8095 and :8096 only.
      Install-WebConsole.ps1 -Tailscale [-DryRun]      also the uoconsole sidecar (web-console\tailscale), once
                                                       D:\UO\web-console\tailscale.env holds TS_AUTHKEY=<the key>
      Install-WebConsole.ps1 -Uninstall [-Purge]       stops and removes the task and the sidecar; -Purge also removes
                                                       the sidecar's state volume (the uoconsole node then has to be
                                                       removed in the Tailscale admin console too)

  Why a scheduled task at logon and not a Windows service: the console reads Chase's own files, Docker Desktop (which
  runs in his session) and his Tailscale login, as the Shard Console and the Golem do; a service would run as another
  account. Data outside git: D:\UO\web-console (settings.json, tailscale.env, access.log, collector.log).
  Never prints the auth key. Refuses to run as SYSTEM. ASCII only.
#>
[CmdletBinding()]
param([switch]$Tailscale, [switch]$Uninstall, [switch]$Purge, [switch]$DryRun)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, never as SYSTEM.' -ForegroundColor Red; exit 2 }
$here = $PSScriptRoot
$data = 'D:\UO\web-console'
$task = 'SL web console'
$envFile = Join-Path $data 'tailscale.env'
$compose = Join-Path $here 'tailscale\docker-compose.yml'
function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Plan([string]$t) { Say ('  ' + $t) }

if ($Uninstall) {
    Say 'Plan: uninstall' 'Cyan'
    Plan ('stop and remove the scheduled task "' + $task + '" (and the server it started)')
    Plan ('docker compose -f ' + $compose + ' down' + $(if ($Purge) { ' -v (removes the uoconsole node''s state)' } else { ' (the node''s state volume is kept)' }))
    Plan ('keep ' + $data + ' (settings, the key file, the logs)')
    if ($DryRun) { Say 'DRY RUN: nothing was changed.' 'Yellow'; exit 0 }
    try { Stop-ScheduledTask -TaskName $task -ErrorAction Stop } catch { }
    try { Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction Stop; Say '  task removed' } catch { Say '  no task to remove' }
    Get-CimInstance Win32_Process -Filter "Name like 'python%'" | Where-Object { $_.CommandLine -match [regex]::Escape((Join-Path $here 'server.py')) } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Say ('  stopped server pid ' + $_.ProcessId) }
    $env:UOCONSOLE_ENV = $(if (Test-Path -LiteralPath $envFile) { $envFile } else { (New-TemporaryFile).FullName })
    & docker compose -f $compose down $(if ($Purge) { '-v' }) 2>&1 | ForEach-Object { Say ('    | ' + $_) }
    Say 'Uninstalled.' 'Green'
    exit 0
}

# The real Python, not the Microsoft Store alias, and its windowless twin.
$py = $null
foreach ($c in @('py', 'python')) {
    try { $p = (& $c -c 'import sys; print(sys.executable)' 2>$null | Select-Object -First 1); if ($p -and (Test-Path -LiteralPath $p)) { $py = $p; break } } catch { }
}
if (-not $py) { Say 'REFUSED: no Python 3 found (py or python). Install Python 3.11 or newer first.' 'Red'; exit 1 }
$pyw = Join-Path (Split-Path $py -Parent) 'pythonw.exe'
if (-not (Test-Path -LiteralPath $pyw)) { $pyw = $py }
$server = Join-Path $here 'server.py'

Say 'Plan: install the web console' 'Cyan'
Plan ('make ' + $data + ' (settings and logs; outside git)')
Plan ('scheduled task "' + $task + '": at logon of ' + $env:USERNAME + ', run "' + $pyw + '" "' + $server + '" (only while logged on, no window, restarted if it fails)')
Plan 'start it now; it listens on http://127.0.0.1:8095/ (this PC) and 127.0.0.1:8096 (the sidecar) only'
Plan 'it starts the status collector (scripts\Write-ConsoleStatus.ps1 -Loop) itself'
if ($Tailscale) {
    $hasKey = (Test-Path -LiteralPath $envFile) -and (Select-String -LiteralPath $envFile -Pattern '^TS_AUTHKEY=\S+' -Quiet)
    if (-not $hasKey) { Say ('REFUSED: ' + $envFile + ' has no TS_AUTHKEY=<key> line. Make the key first (notes/cc-P155-web-console-phase1-status.md, For Chase).') 'Red'; exit 1 }
    Plan ('the uoconsole sidecar: docker compose -f ' + $compose + ' up -d (key read from ' + $envFile + ', never printed; no port published)')
}
if ($DryRun) { Say 'DRY RUN: nothing was changed.' 'Yellow'; exit 0 }

New-Item -ItemType Directory -Path $data -Force | Out-Null
$action = New-ScheduledTaskAction -Execute $pyw -Argument ('"' + $server + '"') -WorkingDirectory $here
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $task -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'cc-P155: the web console (read only), on 127.0.0.1 only' -Force | Out-Null
Start-ScheduledTask -TaskName $task
Say '  task registered and started'
$ok = $false
for ($i = 0; $i -lt 20 -and -not $ok; $i++) { Start-Sleep -Seconds 1; try { $ok = ((Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8095/api/health' -TimeoutSec 3).StatusCode -eq 200) } catch { } }
if ($ok) { Say '  http://127.0.0.1:8095/ answers' 'Green' } else { Say '  http://127.0.0.1:8095/ did not answer in 20 s (see D:\UO\web-console\collector.log)' 'Yellow' }
if ($Tailscale) {
    $env:UOCONSOLE_ENV = $envFile
    & docker compose -f $compose up -d 2>&1 | ForEach-Object { Say ('    | ' + $_) }
    if ($LASTEXITCODE -ne 0) { Say '  the sidecar did not start (above)' 'Red'; exit 1 }
    Say '  sidecar started; give it a minute to log in, then open https://uoconsole.tail81098d.ts.net/' 'Green'
}
Say 'Installed.' 'Green'
