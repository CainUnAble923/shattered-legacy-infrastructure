#Requires -Version 5.1
<#
  cc-P91: emails the overnight bot's headline, and its Fail and Flaky lines, to Chase.

      powershell -NoProfile -ExecutionPolicy Bypass -File Send-BotReport.ps1 -RunDir <run folder> [-WhatIf]

  Uses the Golem's own mail settings, read only, exactly as D:\UO\golem\Send-TestEmail.ps1 does:
  email.json (from, to) and smtp-cred.xml (DPAPI). Changes nothing in the Golem or its config. cc-P99: D:\UO\golem
  when its email.json is there, else D:\UO\cc-autopilot (the Golem's predecessor, before the cutover). The text it sends is
  email.txt in the run folder (written by uobot\report.py); its first line is the subject,
  "Overnight play tests: N pass, N fail, N flaky". -WhatIf prints the mail instead of sending it. ASCII only.
#>
param([Parameter(Mandatory = $true)][string]$RunDir, [switch]$WhatIf)
$Root = 'D:\UO\golem'
if (-not (Test-Path -LiteralPath (Join-Path $Root 'email.json'))) { $Root = 'D:\UO\cc-autopilot' }
$CfgFile = Join-Path $Root 'email.json'
$CredFile = Join-Path $Root 'smtp-cred.xml'
$textFile = Join-Path $RunDir 'email.txt'
if (-not (Test-Path -LiteralPath $textFile)) { Write-Host ('No email.txt in ' + $RunDir + ': nothing sent.'); exit 1 }
$lines = @(Get-Content -LiteralPath $textFile)
$subject = $lines[0]
$body = ($lines | Select-Object -Skip 2) -join "`r`n"
if ($WhatIf) { Write-Host ('Would send: ' + $subject); Write-Host $body; exit 0 }
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
