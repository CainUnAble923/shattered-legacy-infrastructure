#Requires -Version 5.1
<#
  Run-PlayTestGoblin.ps1 (cc-P773): the Play Test Goblin's one command on the game PC. Run it nightly at 05:00, outside
  the overnight bot's window (01:15 to 03:00), so new tasks are waiting when the testers play:

      powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\scripts\Run-PlayTestGoblin.ps1

  It runs scripts\playtest_goblin.py (read its top for what one run does), appends what it printed to
  D:\UO\logs\playtest-goblin.log, and when the Goblin wrote an email (only when something changed: new reports or new
  tasks) sends it to Chase the same way the Golem's emails go (D:\UO\golem\email.json and smtp-cred.xml), then removes
  it. Not set up for email: the email file stays for the next run and the log says so. -DryRun writes only the summary.
  Exit 0 ran, 2 the Goblin stopped on a broken file, 3 the email could not be sent. ASCII only (PowerShell 5.1).
#>
param(
    [switch]$DryRun,
    [string]$PlaytestDir = '',
    [string]$Notes = 'D:\UO\shard-migration',
    [string]$GolemRoot = 'D:\UO\golem',
    [string]$Log = 'D:\UO\logs\playtest-goblin.log'
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $here
if (-not $PlaytestDir) {
    $PlaytestDir = if ($env:SL_PLAYTEST_DIR) { $env:SL_PLAYTEST_DIR } else { Join-Path $repo 'server\lib\uo\modernuo-test\playtest' }
}

function Write-Log([string]$text) {
    $line = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $text
    Write-Host $line
    try {
        $dir = Split-Path -Parent $Log
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Add-Content -LiteralPath $Log -Value $line -Encoding ASCII
    } catch { Write-Host ('(could not write the log: ' + $_.Exception.Message + ')') }
}

$pyArgs = @((Join-Path $here 'playtest_goblin.py'), '--playtest-dir', $PlaytestDir, '--notes', $Notes)
if ($DryRun) { $pyArgs += '--dry-run' }
# PowerShell 5.1 turns a native program's stderr into error records: with Stop, a Python traceback would end the
# script before its lines reach the log. Continue for this one call.
$ErrorActionPreference = 'Continue'
$out = & python @pyArgs 2>&1 | ForEach-Object { [string]$_ }
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
foreach ($l in $out) { Write-Log ('goblin: ' + $l) }
if ($code -ne 0) { Write-Log ('The Goblin stopped (exit ' + $code + ').'); exit 2 }

$mail = Join-Path $PlaytestDir 'goblin-mail.txt'
if (-not (Test-Path -LiteralPath $mail)) { Write-Log 'Nothing changed: no email.'; exit 0 }

$cfgFile = Join-Path $GolemRoot 'email.json'
$credFile = Join-Path $GolemRoot 'smtp-cred.xml'
if (-not (Test-Path -LiteralPath $cfgFile) -or -not (Test-Path -LiteralPath $credFile)) {
    Write-Log 'Email is not set up (email.json and smtp-cred.xml): the email waits for the next run.'
    exit 3
}
try {
    $text = [IO.File]::ReadAllText($mail)
    $split = $text.IndexOf("`n`n")
    $subject = $text.Substring(0, $split).Trim()
    $body = $text.Substring($split + 2)
    $cfg = Get-Content -LiteralPath $cfgFile -Raw | ConvertFrom-Json
    $cred = Import-Clixml -LiteralPath $credFile
    $msg = New-Object System.Net.Mail.MailMessage($cfg.from, $cfg.to, $subject, $body)
    $smtp = New-Object System.Net.Mail.SmtpClient('smtp.gmail.com', 587)
    $smtp.EnableSsl = $true
    $smtp.Credentials = New-Object System.Net.NetworkCredential($cred.UserName, $cred.GetNetworkCredential().Password)
    $smtp.Send($msg)
    $msg.Dispose(); $smtp.Dispose()
    Remove-Item -LiteralPath $mail -Force
    Write-Log ('Emailed Chase: ' + $subject)
    exit 0
} catch {
    Write-Log ('The email could not be sent (it waits for the next run): ' + $_.Exception.Message)
    exit 3
}
