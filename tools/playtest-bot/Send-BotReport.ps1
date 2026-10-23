#Requires -Version 5.1
<#
  cc-P91: emails the overnight bot's headline, and its Fail and Flaky lines, to Chase.

      powershell -NoProfile -ExecutionPolicy Bypass -File Send-BotReport.ps1 -RunDir <run folder> [-WhatIf]

  Uses the Golem's own mail settings, read only, exactly as D:\UO\golem\Send-TestEmail.ps1 does:
  email.json (from, to) and smtp-cred.xml (DPAPI). Changes nothing in the Golem or its config. cc-P99: D:\UO\golem
  when its email.json is there, else D:\UO\cc-autopilot (the Golem's predecessor, before the cutover). The text it sends is
  email.txt in the run folder (written by uobot\report.py); its first line is the subject,
  "Overnight play tests: N pass, N fail, N flaky". -WhatIf prints the mail instead of sending it. ASCII only.
  cc-P102: -TextFile sends another text of the same shape (the "starting" and "crashed" mails, uobot\notify.py).
#>
param([Parameter(Mandatory = $true)][string]$RunDir, [switch]$WhatIf, [string]$TextFile)
$Root = 'D:\UO\golem'
if (-not (Test-Path -LiteralPath (Join-Path $Root 'email.json'))) { $Root = 'D:\UO\cc-autopilot' }
$CfgFile = Join-Path $Root 'email.json'
$CredFile = Join-Path $Root 'smtp-cred.xml'
$textFile = $(if ($TextFile) { $TextFile } else { Join-Path $RunDir 'email.txt' })
if (-not (Test-Path -LiteralPath $textFile)) { Write-Host ('No ' + $textFile + ': nothing sent.'); exit 1 }
$lines = @(Get-Content -LiteralPath $textFile)
$subject = $lines[0]
$body = ($lines | Select-Object -Skip 2) -join "`r`n"
if ($WhatIf) { Write-Host ('Would send: ' + $subject); Write-Host $body; exit 0 }
# cc-P410: only the Tinker mails Chase. With the Golem's outbox in reach the mail is an event for it: a crash at once,
# the start and the morning report in its next brief. Its settings.json tinkerOnlyMail false (or no Tinker) sends here.
$ob = Join-Path $Root 'GolemOutbox.ps1'
if (Test-Path -LiteralPath $ob) {
    . $ob
    $crash = [bool]($subject -match '(?i)stopped early|failed')
    $kind = $(if ($crash) { 'bot-crashed' } elseif ($subject -match '(?i)starting') { 'bot-start' } else { 'bot-report' })
    if (Add-OutboxMail -Root $Root -Golem 'Bot' -MailKind $kind -Subject $subject -Body $body -NeedsYou $crash -Key ($kind + '|Bot|' + (Get-Date -Format 'yyyy-MM-dd'))) { Write-Host ('Handed to the Tinker: ' + $subject); exit 0 }
}
if (-not (Test-Path -LiteralPath $CfgFile) -or -not (Test-Path -LiteralPath $CredFile)) {
    Write-Host 'Not sent: the Golem email is not set up (email.json and smtp-cred.xml).'
    exit 1
}
try {
    $cfg = Get-Content -LiteralPath $CfgFile -Raw | ConvertFrom-Json
    $cred = Import-Clixml -LiteralPath $CredFile
    $msg = New-Object System.Net.Mail.MailMessage($cfg.from, $cfg.to, $subject, $body)
    $smtp = New-Object System.Net.Mail.SmtpClient('smtp.gmail.com', 587)
    $smtp.EnableSsl = $true
    $smtp.Credentials = New-Object System.Net.NetworkCredential($cred.UserName, $cred.GetNetworkCredential().Password)
    $smtp.Send($msg)
    $msg.Dispose(); $smtp.Dispose()
    Write-Host ('Report emailed: ' + $subject)
    exit 0
} catch {
    Write-Host ('Report email FAILED: ' + $_.Exception.Message)
    exit 2
}
