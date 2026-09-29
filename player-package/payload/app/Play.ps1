#Requires -Version 5.1
<#
  Shattered Legacy player launcher. One launcher, three targets:

      Play.ps1          shatteredlegacyuo.com:2593   the shard, from anywhere
      Play.ps1 -Lan     192.168.1.58:2593            the shard, from inside the house, no DNS
      Play.ps1 -Test    shatteredlegacyuo.com:2594   the throwaway TEST shard, from anywhere

  Runs Setup.ps1 first whenever the Ultima Online files are not known yet.

  Before that it asks get.shatteredlegacyuo.com/version.json whether a newer package is
  out, and offers to install it (Update.ps1). The check gives up silently after 4
  seconds or on any error: the update server can never stop anyone playing.
      -NoUpdate            skip the check
      -Update              offer the update even when this install is current (testing)
      -UpdateBase <url>    ask another server instead (probes; default the real get.)

  Safe to re-run. The saved username and password in each settings file are kept; only
  the server, the port and the client folder are rewritten. The live shard and the test
  shard keep separate settings files so a test login is never offered to live.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as
  Windows-1252, and a non-ASCII character can become syntax.
#>
[CmdletBinding()]
param([switch]$Lan, [switch]$Test, [string]$UoPath,
      [switch]$NoUpdate, [switch]$Update, [string]$UpdateBase = 'https://get.shatteredlegacyuo.com')

$ErrorActionPreference = 'Stop'
$app   = $PSScriptRoot
$root  = Split-Path $app -Parent
$taz   = Join-Path $app 'tazuo'
$tazExe = Join-Path $taz 'TazUO.exe'

if ($Lan -and $Test) { Write-Host '  Pick one of -Lan or -Test.' -ForegroundColor Red; exit 1 }
if     ($Test) { $server = 'shatteredlegacyuo.com'; $port = 2594; $settingsName = 'settings.test.json'; $label = 'TEST SHARD (throwaway)' }
elseif ($Lan)  { $server = '192.168.1.58';          $port = 2593; $settingsName = 'settings.json';      $label = 'Shattered Legacy (house network)' }
else           { $server = 'shatteredlegacyuo.com'; $port = 2593; $settingsName = 'settings.json';      $label = 'Shattered Legacy' }
$settingsPath = Join-Path $taz $settingsName

# --- an update that stopped halfway ------------------------------------------------
# Update.ps1 deletes update-backup\ when it finishes and copies it back when it fails,
# so one still here means the machine stopped mid-update. Put the old version back.
if (Test-Path -LiteralPath (Join-Path $root 'update-backup')) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $app 'Update.ps1') -Target $root -Restore
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
if (Test-Path -LiteralPath (Join-Path $root 'update-backup.done')) {
    Remove-Item -LiteralPath (Join-Path $root 'update-backup.done') -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $tazExe)) {
    Write-Host "  The game client is missing: $tazExe" -ForegroundColor Red
    Write-Host "  Windows' Extract All sometimes stops partway without saying so. Delete this folder and unzip again, or use 7-Zip." -ForegroundColor Red
    Write-Host '  Keep the app folder next to "Play Shattered Legacy.bat".' -ForegroundColor Red
    Write-Host '  Antivirus sometimes removes files from a new download; see README.txt.' -ForegroundColor Red
    exit 1
}

# --- Mark of the Web -------------------------------------------------------------
# Everything unzipped from a downloaded zip is marked as from the internet. TazUO.exe
# would then stop at a SmartScreen prompt on every start, and some DLLs refuse to load.
# Clear the mark on our own files whenever TazUO.exe still carries it. Checked on every
# start and AFTER the missing-client check, so a partial extract is reported, not
# unblocked. There is deliberately no "done" marker file: one survived a re-extract over
# the same folder (2026-09-29) and left ~600 fresh files marked.
if (Get-Item -LiteralPath $tazExe -Stream Zone.Identifier -ErrorAction SilentlyContinue) {
    Get-ChildItem -LiteralPath (Split-Path $app -Parent) -Recurse -File | Unblock-File
}

# --- is there a newer package? ------------------------------------------------------
# version.json is { version, sha256, bytes, url[, notes] }, written by
# Build-PlayerPackage.ps1 and published beside the zip. Returns $null on ANY failure.
# The request runs on a second runspace and is abandoned after 4 seconds, because
# Invoke-WebRequest's own -TimeoutSec does not cover the DNS lookup: an offline PC or a
# dead resolver can hold it for 15 seconds or more before it fails.
function Get-RemotePackage([string]$base) {
    try {
        $u = $base.TrimEnd('/') + '/version.json?t=' + [DateTime]::UtcNow.Ticks
        $ps = [PowerShell]::Create()
        [void]$ps.AddScript({
            param($u)
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            $r = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 4
            # RawContentStream, not Content: Content is a byte array when the server
            # does not call the type text.
            [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
        }).AddArgument($u)
        $h = $ps.BeginInvoke()
        if (-not $h.AsyncWaitHandle.WaitOne(4000)) { return $null }   # left behind; dies with this process
        $text = @($ps.EndInvoke($h))[0]
        $ps.Dispose()
        $j = $text.TrimStart([char]0xFEFF) | ConvertFrom-Json
        if ("$($j.version)" -notmatch '^\d{4}\.\d{2}\.\d{2}\.\d{4}$') { return $null }
        if ("$($j.sha256)" -notmatch '^[0-9a-f]{64}$') { return $null }
        if (-not ("$($j.bytes)" -as [long])) { return $null }
        # The zip must come from the server that vouched for it.
        if (([uri]"$($j.url)").Host -ne ([uri]$base).Host) { return $null }
        return $j
    } catch { return $null }
}

# Downloads, checks and unpacks the package, then hands over to its Update.ps1 in a new
# window and ends this script. Returns only if something failed, having said what, with
# nothing in the install touched.
function Install-Package($j) {
    $ProgressPreference = 'SilentlyContinue'   # the progress bar slows 5.1's download 10x
    $work = Join-Path $env:TEMP 'sl-update'
    try {
        if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
        New-Item -ItemType Directory -Path $work -Force | Out-Null
        $zip = Join-Path $work 'package.zip'
        Write-Host ("  Downloading {0:N0} MB..." -f ([long]$j.bytes / 1MB)) -ForegroundColor Gray
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $j.url -OutFile $zip -UseBasicParsing -TimeoutSec 600
        $len = (Get-Item -LiteralPath $zip).Length
        $sha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower()
        if ($len -ne [long]$j.bytes -or $sha -ne $j.sha256) {
            throw "the download is not the file the server described ($len bytes, expected $($j.bytes); SHA-256 $(if ($sha -eq $j.sha256) { 'matches' } else { 'differs' }))."
        }
        Write-Host '  Unpacking...' -ForegroundColor Gray
        $x = Join-Path $work 'x'
        Expand-Archive -LiteralPath $zip -DestinationPath $x -Force
        Remove-Item -LiteralPath $zip -Force
        $tops = @(Get-ChildItem -LiteralPath $x -Force)
        if ($tops.Count -ne 1 -or -not $tops[0].PSIsContainer) { throw 'the package does not have the one top folder it should.' }
        $src = $tops[0].FullName
        foreach ($need in 'app\tazuo\TazUO.exe', 'app\package-version.txt', 'app\Update.ps1') {
            if (-not (Test-Path -LiteralPath (Join-Path $src $need))) { throw "the package has no $need." }
        }
        $got = (Get-Content -LiteralPath (Join-Path $src 'app\package-version.txt') -TotalCount 1).Trim()
        if ($got -ne $j.version) { throw "the package says it is $got, the server said $($j.version)." }

        # The new package's updater, run from %TEMP%, so nothing it replaces is running.
        $upd = Join-Path $work 'Update.ps1'
        Copy-Item -LiteralPath (Join-Path $src 'app\Update.ps1') -Destination $upd
        # It waits for this process and for the cmd.exe running the .bat that started it:
        # cmd reads a batch file by offset while it runs, so it must be gone first.
        $pids = "$PID"
        try {
            $ppid = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId
            if ((Get-Process -Id $ppid -ErrorAction Stop).ProcessName -eq 'cmd') { $pids += ",$ppid" }
        } catch {}
        # Start-Process joins these with spaces and quotes nothing. A path ending in "\"
        # would escape its closing quote, hence the trailing ".".
        $q = { param($p) if ($p.EndsWith('\')) { $p += '.' }; '"' + $p + '"' }
        $a = "-NoProfile -ExecutionPolicy Bypass -File $(& $q $upd) -Source $(& $q $src) -Target $(& $q $root) -Stage $(& $q $work) -WaitPid $pids"
        if ($Test) { $a += ' -Test' }
        if ($Lan)  { $a += ' -Lan' }
        Start-Process -FilePath 'powershell.exe' -ArgumentList $a
        Write-Host '  The update carries on in a new window.' -ForegroundColor Gray
        exit 0
    } catch {
        Write-Host "  The update did not work: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host '  Nothing in your game folder was changed. Starting the version you have.' -ForegroundColor Yellow
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (-not $NoUpdate) {
    $remote = Get-RemotePackage $UpdateBase
    if ($remote) {
        $localText = 'an older one'
        $local = [version]'0.0'
        try {
            $localText = (Get-Content -LiteralPath (Join-Path $app 'package-version.txt') -TotalCount 1).Trim()
            $local = [version]$localText
        } catch {}
        if ([version]$remote.version -gt $local -or $Update) {
            Write-Host ''
            Write-Host "  A newer Shattered Legacy package is out: $($remote.version) (you have $localText)." -ForegroundColor Cyan
            if ($remote.notes) { Write-Host "  What is new: $($remote.notes)" -ForegroundColor Cyan }
            Write-Host ("  About {0:N0} MB. Your account, settings, Ultima Online folder and gump positions are kept." -f ([long]$remote.bytes / 1MB)) -ForegroundColor Gray
            $ans = Read-Host '  Update now? [Y/n]'
            if ($ans -match '^\s*(y|yes)?\s*$') { Install-Package $remote }
            else { Write-Host '  Skipped. You will be asked again next time.' -ForegroundColor Gray }
        }
    }
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
    if ($Test) { Write-Host '  The test shard only runs when Chase starts it. Ask him if it is up.' -ForegroundColor Red }
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
