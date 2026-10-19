#Requires -Version 5.1
<#
  cc-P155: installs the web console on the game PC. Chase runs it; CC never does (it would start a service).

      Install-WebConsole.ps1 [-DryRun]                 the service: a scheduled task "SL web console" that starts
                                                       web-console\server.py with pythonw (no window) at Chase's logon,
                                                       and starts it now (a running one is stopped first, so an update
                                                       takes effect). It listens on 127.0.0.1:8095 and :8096 only.
      Install-WebConsole.ps1 -Tailscale [-DryRun]      also the uoconsole sidecar (web-console\tailscale): the node and,
                                                       cc-P182, its proof forwarder; once D:\UO\web-console\tailscale.env
                                                       holds TS_AUTHKEY=<the key> (or the node has logged in before)
      Install-WebConsole.ps1 -Uninstall [-Purge]       stops and removes the task and the sidecar; -Purge also removes
                                                       the sidecar's state volume (the uoconsole node then has to be
                                                       removed in the Tailscale admin console too)

  Why a scheduled task at logon and not a Windows service: the console reads Chase's own files, Docker Desktop (which
  runs in his session) and his Tailscale login, as the Shard Console and the Golem do; a service would run as another
  account. Data outside git: D:\UO\web-console (settings.json, tailscale.env, access.log, collector.log).
  cc-P182: makes D:\UO\web-console\proof.key once (64 random hex characters, never printed): the secret the sidecar's
  forwarder adds to every request and the console demands on its tailnet listener (P155's Deviation 2).
  docker's own output is shown as plain text and success is judged by its exit code only (Invoke-Docker).
  Never prints the auth key or the proof. Refuses to run as SYSTEM. ASCII only.
#>
[CmdletBinding()]
param([switch]$Tailscale, [switch]$Uninstall, [switch]$Purge, [switch]$DryRun)
$ErrorActionPreference = 'Stop'
if ([Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { Write-Host 'REFUSED: runs as Chase, never as SYSTEM.' -ForegroundColor Red; exit 2 }
$here = $PSScriptRoot
$data = 'D:\UO\web-console'
$task = 'SL web console'
$envFile = Join-Path $data 'tailscale.env'
$proofFile = Join-Path $data 'proof.key'
$compose = Join-Path $here 'tailscale\docker-compose.yml'
function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Plan([string]$t) { Say ('  ' + $t) }
function Invoke-Docker([string[]]$Arguments) {
    # docker writes its normal progress ("Container uoconsole-ts Running") to stderr. Under 'Stop', 2>&1 turns each such
    # line into a red NativeCommandError although docker succeeded (Chase's install, 2026-10-19 06:49): so the lines are
    # shown as plain text, and success is the exit code only, as the Shard Console's Invoke-ExecStep does.
    $eap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & docker @Arguments 2>&1 | ForEach-Object {
            $line = $(if ($_ -is [Management.Automation.ErrorRecord]) { $_.Exception.Message } else { [string]$_ })
            Say ('    | ' + $line)
        }
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $eap }
    $code
}
function Stop-WebConsoleServer {
    try { Stop-ScheduledTask -TaskName $task -ErrorAction Stop } catch { }
    Get-CimInstance Win32_Process -Filter "Name like 'python%'" | Where-Object { $_.CommandLine -match [regex]::Escape((Join-Path $here 'server.py')) } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Say ('  stopped server pid ' + $_.ProcessId) }
}

if ($Uninstall) {
    Say 'Plan: uninstall' 'Cyan'
    Plan ('stop and remove the scheduled task "' + $task + '" (and the server it started)')
    Plan ('docker compose -f ' + $compose + ' down' + $(if ($Purge) { ' -v (removes the uoconsole node''s state)' } else { ' (the node''s state volume is kept)' }))
    Plan ('keep ' + $data + ' (settings, the key file, the logs)')
    if ($DryRun) { Say 'DRY RUN: nothing was changed.' 'Yellow'; exit 0 }
    Stop-WebConsoleServer
    try { Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction Stop; Say '  task removed' } catch { Say '  no task to remove' }
    $env:UOCONSOLE_ENV = $(if (Test-Path -LiteralPath $envFile) { $envFile } else { (New-TemporaryFile).FullName })
    $env:UOCONSOLE_PROOF = $(if (Test-Path -LiteralPath $proofFile) { $proofFile } else { (New-TemporaryFile).FullName })
    $down = @('compose', '-f', $compose, 'down')
    if ($Purge) { $down += '-v' }
    $code = Invoke-Docker $down
    if ($code -ne 0) { Say ('  docker compose down exited with ' + $code + ' (above)') 'Yellow' }
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
Plan ('make ' + $proofFile + ' once: the sidecar''s proof (never printed); ' + $(if (Test-Path -LiteralPath $proofFile) { 'it exists, kept' } else { 'not there yet, made now' }))
Plan 'stop a running web console first, so this version is the one that starts'
Plan ('scheduled task "' + $task + '": at logon of ' + $env:USERNAME + ', run "' + $pyw + '" "' + $server + '" (only while logged on, no window, restarted if it fails)')
Plan 'start it now; it listens on http://127.0.0.1:8095/ (this PC) and 127.0.0.1:8096 (the sidecar) only'
Plan 'it starts the status collector (scripts\Write-ConsoleStatus.ps1 -Loop) itself'
if ($Tailscale) {
    $hasKey = (Test-Path -LiteralPath $envFile) -and (Select-String -LiteralPath $envFile -Pattern '^TS_AUTHKEY=\S+' -Quiet)
    if (-not $hasKey) { Say ('REFUSED: ' + $envFile + ' has no TS_AUTHKEY=<key> line. Make the key first (notes/cc-P155-web-console-phase1-status.md, For Chase).') 'Red'; exit 1 }
    Plan ('the uoconsole sidecar: docker compose -f ' + $compose + ' up -d --force-recreate (the node, and cc-P182''s proof forwarder; key read from ' + $envFile + ', never printed; no port published; the node''s state volume is kept, so it stays the same uoconsole)')
}
if ($DryRun) { Say 'DRY RUN: nothing was changed.' 'Yellow'; exit 0 }

New-Item -ItemType Directory -Path $data -Force | Out-Null
if (-not (Test-Path -LiteralPath $proofFile)) {
    # 32 random bytes as hex, from the system's cryptographic generator; written without a newline and never printed.
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    [IO.File]::WriteAllText($proofFile, (($bytes | ForEach-Object { $_.ToString('x2') }) -join ''), [Text.Encoding]::ASCII)
    Say ('  made ' + $proofFile + ' (the sidecar''s proof)')
}
Stop-WebConsoleServer
$action = New-ScheduledTaskAction -Execute $pyw -Argument ('"' + $server + '"') -WorkingDirectory $here
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $task -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'cc-P155, cc-P182: the web console and its Golem buttons, on 127.0.0.1 only' -Force | Out-Null
Start-ScheduledTask -TaskName $task
Say '  task registered and started'
$ok = $false
for ($i = 0; $i -lt 20 -and -not $ok; $i++) { Start-Sleep -Seconds 1; try { $ok = ((Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8095/api/health' -TimeoutSec 3).StatusCode -eq 200) } catch { } }
if ($ok) { Say '  http://127.0.0.1:8095/ answers' 'Green' } else { Say '  http://127.0.0.1:8095/ did not answer in 20 s (see D:\UO\web-console\collector.log)' 'Yellow' }
if ($Tailscale) {
    $env:UOCONSOLE_ENV = $envFile
    $env:UOCONSOLE_PROOF = $proofFile
    $code = Invoke-Docker @('compose', '-f', $compose, 'up', '-d', '--force-recreate')
    if ($code -ne 0) { Say ('  the sidecar did not start: docker compose exited with ' + $code + ' (above)') 'Red'; exit 1 }
    Say '  sidecar started; give it a minute to log in, then open https://uoconsole.tail81098d.ts.net/' 'Green'
}
Say 'Installed.' 'Green'
