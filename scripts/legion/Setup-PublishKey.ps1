#Requires -Version 5.1
<#
  Setup-PublishKey.ps1 (cc-P144): once per PC, so Publish to dev and Promote to official can reach Haven with this
  PC's own SSH key. The pattern of D:\UO\haven-migration\Setup-HavenSshKey.ps1, for any PC: Haven by its LAN address
  when it answers, else its Tailscale name (docker\haven\services.json), and the key named for this PC.
  The Shard Console's Set up publish key runs it. Safe to re-run: it skips anything already done.

  1. Makes ~\.ssh\id_ed25519 if this PC has none (it asks for a passphrase; set one).
  2. Adds the public half to Haven's ~/.ssh/authorized_keys. This is the one time it asks Haven's password.
  3. Starts ssh-agent if it can (an admin PowerShell), so the passphrase is typed once.
  4. Proves it: a login that is not allowed to ask for a password.

  ASCII only, on purpose.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LegionDev.ps1')
function Say([string]$m, [string]$c = 'Gray') { Write-Host "  $m" -ForegroundColor $c }

$t = Get-HavenTarget
if (-not $t.Host) { Write-Host "STOPPED: $($t.Error)" -ForegroundColor Red; exit 1 }
Say ("Haven: " + $t.Ssh + ' (' + $t.Via + ')')
$sshDir = Join-Path $env:USERPROFILE '.ssh'
$key = Join-Path $sshDir 'id_ed25519'
if (-not (Test-Path -LiteralPath $sshDir)) { New-Item -ItemType Directory -Path $sshDir | Out-Null }

$prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
if (Test-Path -LiteralPath $key) { Say "1. Key already exists at $key, using it." }
else {
    Say '1. Creating a key. It asks for a passphrase: set one (step 3 means you type it once).'
    & ssh-keygen -t ed25519 -f $key -C ("chase@" + $env:COMPUTERNAME.ToLower())
    if ($LASTEXITCODE -ne 0) { Write-Host 'STOPPED: ssh-keygen failed' -ForegroundColor Red; exit 1 }
}

$already = $false
& ssh -o BatchMode=yes -o ConnectTimeout=8 $t.Ssh 'true' 2>$null
if ($LASTEXITCODE -eq 0) { $already = $true; Say '2. Haven already accepts this PC''s key.' }
else {
    Say "2. Installing the public key on Haven (enter Haven's password one last time)..."
    $remote = 'umask 077; mkdir -p ~/.ssh; touch ~/.ssh/authorized_keys; k=$(cat); grep -qxF "$k" ~/.ssh/authorized_keys || echo "$k" >> ~/.ssh/authorized_keys; echo installed'
    Get-Content -LiteralPath "$key.pub" | & ssh $t.Ssh $remote
    if ($LASTEXITCODE -ne 0) { Write-Host 'STOPPED: could not install the key on Haven' -ForegroundColor Red; exit 1 }
}

$svc = Get-Service ssh-agent -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -ne 'Running') {
    try { Set-Service ssh-agent -StartupType Automatic -ErrorAction Stop; Start-Service ssh-agent -ErrorAction Stop; Say '3. ssh-agent enabled and started.' }
    catch { Say '3. Could not start ssh-agent (needs an admin PowerShell). Re-run this as admin for that step.' Yellow }
}
if ((Get-Service ssh-agent -ErrorAction SilentlyContinue).Status -eq 'Running' -and -not $already) { & ssh-add $key }

Say '4. Testing key login (no password allowed)...'
& ssh -o BatchMode=yes -o ConnectTimeout=8 $t.Ssh 'echo KEY LOGIN OK on $(hostname)'
$ok = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $prev
if ($ok) { Say 'Done. This PC can publish Legion scripts.' Green; exit 0 }
Say 'Key login did not work yet. If ssh-agent is not running, it may be waiting for the passphrase.' Yellow
exit 1
