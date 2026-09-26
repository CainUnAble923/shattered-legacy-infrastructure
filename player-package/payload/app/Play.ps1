#Requires -Version 5.1
<#
  Shattered Legacy player launcher. One launcher, three targets:

      Play.ps1          shatteredlegacyuo.com:2593   the shard, from anywhere
      Play.ps1 -Lan     192.168.1.58:2593            the shard, from inside the house, no DNS
      Play.ps1 -Test    192.168.1.58:2594            the throwaway TEST shard (house only)

  Runs Setup.ps1 first whenever the Ultima Online files are not known yet.

  Safe to re-run. The saved username and password in each settings file are kept; only
  the server, the port and the client folder are rewritten. The live shard and the test
  shard keep separate settings files so a test login is never offered to live.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as
  Windows-1252, and a non-ASCII character can become syntax.
#>
[CmdletBinding()]
param([switch]$Lan, [switch]$Test, [string]$UoPath)

$ErrorActionPreference = 'Stop'
$app   = $PSScriptRoot
$taz   = Join-Path $app 'tazuo'
$tazExe = Join-Path $taz 'TazUO.exe'

if ($Lan -and $Test) { Write-Host '  Pick one of -Lan or -Test.' -ForegroundColor Red; exit 1 }
if     ($Test) { $server = '192.168.1.58';          $port = 2594; $settingsName = 'settings.test.json'; $label = 'TEST SHARD (throwaway)' }
elseif ($Lan)  { $server = '192.168.1.58';          $port = 2593; $settingsName = 'settings.json';      $label = 'Shattered Legacy (house network)' }
else           { $server = 'shatteredlegacyuo.com'; $port = 2593; $settingsName = 'settings.json';      $label = 'Shattered Legacy' }
$settingsPath = Join-Path $taz $settingsName

# --- Mark of the Web -------------------------------------------------------------
# Everything unzipped from a downloaded zip is marked as from the internet. TazUO.exe
# would then stop at a SmartScreen prompt on every start, and some DLLs refuse to load.
# Clear the mark on our own files, once.
$unblocked = Join-Path $app '.unblocked'
if (-not (Test-Path $unblocked)) {
    Get-ChildItem -LiteralPath (Split-Path $app -Parent) -Recurse -File | Unblock-File
    [IO.File]::WriteAllText($unblocked, (Get-Date -Format s), (New-Object Text.UTF8Encoding $false))
}

if (-not (Test-Path $tazExe)) {
    Write-Host "  The game client is missing: $tazExe" -ForegroundColor Red
    Write-Host '  Unzip the whole package again, and keep the app folder next to this file.' -ForegroundColor Red
    Write-Host '  Antivirus sometimes removes files from a new download; see README.txt.' -ForegroundColor Red
    exit 1
}

# --- Ultima Online data -------------------------------------------------------
$setupArgs = @{}
if ($UoPath) { $setupArgs.UoPath = $UoPath }
& (Join-Path $app 'Setup.ps1') @setupArgs
if ($LASTEXITCODE -ne 0) { exit 1 }
$uoDir = (Get-Content (Join-Path $app 'uo-path.txt') -TotalCount 1).Trim()
$fv = (Get-Item (Join-Path $uoDir 'client.exe')).VersionInfo
$clientVersion = '{0}.{1}.{2}.{3}' -f $fv.FileMajorPart, $fv.FileMinorPart, $fv.FileBuildPart, $fv.FilePrivatePart

# --- is the server there? -------------------------------------------------------
# A closed port otherwise shows up as a client that sits at "connecting" and then says
# nothing useful. Say it plainly instead.
$reachable = $false
$tcp = New-Object Net.Sockets.TcpClient
try {
    $ar = $tcp.BeginConnect($server, $port, $null, $null)
    $reachable = $ar.AsyncWaitHandle.WaitOne(5000) -and $tcp.Connected
} catch { $reachable = $false } finally { $tcp.Close() }
if (-not $reachable) {
    Write-Host ''
    Write-Host "  Cannot reach $label at $server`:$port." -ForegroundColor Red
    if ($Test) { Write-Host '  The test shard only runs when Chase starts it, and only inside the house.' -ForegroundColor Red }
    else       { Write-Host '  The server may be down or restarting, or this computer is offline. Try again in a few minutes, then ask Chase.' -ForegroundColor Red }
    exit 1
}

# --- settings -------------------------------------------------------------------
$s = [ordered]@{
    username              = ''
    password              = ''
    saveaccount           = $true
    autologin             = $false
    encryption            = 0
    plugins               = @()
}
# Anything not named here takes TazUO's own default (Configuration/Settings.cs).
# Keep what the player already has (their saved username and password, above all).
if (Test-Path $settingsPath) {
    try {
        $old = [IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
        foreach ($p in $old.PSObject.Properties) { $s[$p.Name] = $p.Value }
    } catch {
        Write-Host "  $settingsName could not be read; starting it fresh." -ForegroundColor Yellow
    }
}
# Then force the parts this launcher owns.
$s.ip                    = $server
$s.port                  = $port
$s.ultimaonlinedirectory = $uoDir
$s.clientversion         = $clientVersion
$s.last_server_name      = $(if ($Test) { 'Shattered Legacy TEST' } else { 'Shattered Legacy' })
# After login the server tells the client where the game server is (packet 0x8C). The
# live shard currently names its LAN address there, which is unreachable from outside
# the house. With this true, TazUO ignores that address AND port and reconnects to the
# server it logged in to (LoginHandshake.HandleRelayServerPacket). It is what makes the
# default target work from outside, and what keeps -Test from ever landing on live.
$s.ignore_relay_ip       = $true
# A populated plugins array makes the client Assembly.LoadFile a plugin and can crash it.
$s.plugins               = @()
if ($Test) { $s.saveaccount = $false; $s.autologin = $false }

# PowerShell 5.1's -Encoding UTF8 writes a BOM, and TazUO's settings reader falls back to
# defaults on one WITHOUT SAYING SO: the client starts and connects nowhere. No BOM.
[IO.File]::WriteAllText($settingsPath, ($s | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding $false))

Write-Host ''
Write-Host "  $label" -ForegroundColor Cyan
Write-Host "  server   $server`:$port"            -ForegroundColor Gray
Write-Host "  UO files $uoDir ($clientVersion)"   -ForegroundColor Gray
if ($Test) { Write-Host '  TEST SHARD: nothing here is kept.' -ForegroundColor Yellow }
Write-Host ''
Write-Host '  First time? Type any account name and password you like. The first login' -ForegroundColor Gray
Write-Host '  creates your account, so remember them.' -ForegroundColor Gray
Write-Host ''

Start-Process -FilePath $tazExe -ArgumentList @('-settings', "`"$settingsPath`"") -WorkingDirectory $taz
exit 0
