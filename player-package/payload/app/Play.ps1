#Requires -Version 5.1
<#
  Shattered Legacy player launcher. cc-P411: live is retired (since 2026-10-14; it returns as a fresh world at 1.0),
  so it starts the TEST shard:

      Play.ps1          shatteredlegacyuo.com:2594   the TEST shard (settings.test.json)
      Play.ps1 -Lan     192.168.1.58:2594            the same, from inside the house, no DNS
      Play.ps1 -Test    the same as no switch; kept because the shortcuts and the updater pass it

  Test profiles: one saved TEST login per name, for a desktop shortcut each.
      Play.ps1 -Test -Account gargoyle               app\tazuo\settings.test.gargoyle.json
  The name is the profile, not the UO account: 1 to 24 of a-z, 0-9 and -. The first
  launch of a profile shows the login screen; log in once with Save Account ticked and
  every launch after that logs straight in, as the last character played. No password
  ever goes on a command line: it stays in that profile's settings file, as TazUO keeps
  it. Play-Test.bat passes it through: Play-Test.bat -Account gargoyle.

  Another client, TEST only (cc-P114): Chase's test shortcuts start our own TazUO build.
      Play.ps1 -Test -Account human -ClientDir D:\UO\tazuo-sl-build\client
  Starts that folder's TazUO.exe with this install's settings file, autologin and overrides,
  and this install's character profiles (Data\Profiles). Players never need it.

  Runs Setup.ps1 first whenever the Ultima Online files are not known yet.

  Before that it asks get.shatteredlegacyuo.com/version.json whether a newer package is
  out, and offers to install it (Update.ps1). The check gives up silently after 4
  seconds or on any error: the update server can never stop anyone playing.
      -NoUpdate            skip the check
      -Update              offer the update even when this install is current (testing)
      -UpdateBase <url>    ask another server instead (probes; default the real get.)

  Then (cc-P144) it brings the Legion scripts up to date: Update-Scripts.ps1, quietly, applied without a prompt.
      -NoScripts           skip it

  Then (cc-P411) Shattered Legacy's art, text and map: Overrides-Launch.ps1's Invoke-UoOverrides checks the stamps,
  and after an update rebuilds them in the background with a progress bar, for up to a minute
  (app\launch-options.json "overridesTimeLimitSeconds"). Past that the game starts with the last good art and the
  new art is used from the next start. Its trace is app\logs\overrides-<time>.log.
      -SkipArtRebuild      start with the art as it is now, checking and rebuilding nothing (Play-Skip-Art.bat)
      -ArtHandoff          (cc-P611) the updater already waited for the rebuild that is still running: not again

  Every launch is written to app\logs\launch-<time>.log (the last 10 are kept), to send Chase when something is wrong.

  Test hook (never set on a player's machine): SL_PLAY_SERVER=127.0.0.1:<port> checks and connects to that loopback
  port instead of the shard (cc-P611: Launch-Overrides.Tests.ps1's sandboxes, so a test never reaches a server).

  Safe to re-run. The saved username and password in each settings file are kept; only
  the server, the port and the client folder are rewritten. The live shard and the test
  shard keep separate settings files so a test login is never offered to live. Which
  file, and what is rewritten in it, is Launch-Settings.ps1.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as
  Windows-1252, and a non-ASCII character can become syntax.
#>
[CmdletBinding()]
param([switch]$Lan, [switch]$Test, [string]$Account, [string]$UoPath, [string]$ClientDir,
      [switch]$NoUpdate, [switch]$Update, [string]$UpdateBase = 'https://get.shatteredlegacyuo.com',
      [ValidateSet('sfw', 'topless', 'nude')][string]$PaperdollBodies, [string]$PaperdollFolder,
      [switch]$NoScripts, [switch]$SkipArtRebuild, [switch]$ArtHandoff)

$ErrorActionPreference = 'Stop'
$app   = $PSScriptRoot
$root  = Split-Path $app -Parent
$taz   = Join-Path $app 'tazuo'
$tazExe = Join-Path $taz 'TazUO.exe'

# cc-P411: every launch is kept in app\logs\launch-<time>.log, the last 10 (Chase's work-PC Launch-Log.ps1, folded in).
try {
    $logDir = Join-Path $app 'logs'
    if (-not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    @(Get-ChildItem -LiteralPath $logDir -Filter 'launch-*.log' -File | Sort-Object LastWriteTimeUtc, Name -Descending | Select-Object -Skip 9) | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue }
    $null = Start-Transcript -LiteralPath (Join-Path $logDir ('launch-' + (Get-Date).ToString('yyyyMMdd-HHmmss') + '.log')) -Force
} catch {}

. (Join-Path $app 'Launch-Settings.ps1')
. (Join-Path $app 'Paperdoll-Bodies.ps1')
# cc-P81: a local-only paperdoll choice for a computer that holds the add-on (Paperdoll-Bodies.ps1). Never shipped.
if ($PaperdollBodies) { Write-Host "  $(Set-PaperdollSetting -Root $root -Mode $PaperdollBodies -Folder $PaperdollFolder)" -ForegroundColor Gray }
$targetArgs = @{ Lan = $Lan; Test = $Test }
if ($PSBoundParameters.ContainsKey('Account')) { $targetArgs.Account = $Account }
if ($PSBoundParameters.ContainsKey('ClientDir')) { $targetArgs.ClientDir = $ClientDir }
try { $target = Get-LaunchTarget @targetArgs }
catch { Write-Host "  $($_.Exception.Message)" -ForegroundColor Red; exit 1 }
$server = $target.Server; $port = $target.Port; $settingsName = $target.SettingsName; $label = $target.Label
if ([string]$env:SL_PLAY_SERVER -match '^(127\.0\.0\.1):(\d{1,5})$') { $server = $Matches[1]; $port = [int]$Matches[2]; $target.Server = $server; $target.Port = $port }
# cc-P411: every target is the test shard now, with or without -Test (the updater is told -Test either way).
$Test = [switch]$true
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

# Downloads $url to $path with a progress bar. Not Invoke-WebRequest: its bar redraws on
# every chunk and slows 5.1's download about 10x, which is why it used to run with the
# bar off and show nothing for a minute. Here the bar is redrawn at most four times a
# second. Timeout is what Invoke-WebRequest -TimeoutSec 600 set: HttpWebRequest.Timeout,
# which covers connecting and the response headers. A stalled body still fails on
# ReadWriteTimeout (the default, 5 minutes).
function Save-Download([string]$url, [string]$path, [long]$expected) {
    $req = [Net.HttpWebRequest][Net.WebRequest]::Create($url)
    $req.Timeout = 600000
    $req.UserAgent = 'ShatteredLegacy-Play'
    $resp = $req.GetResponse()
    try {
        $total = $resp.ContentLength
        if ($total -le 0) { $total = $expected }
        $in = $resp.GetResponseStream()
        $out = [IO.File]::Create($path)
        try {
            $buf = New-Object byte[] 65536
            $got = [long]0
            $sw = [Diagnostics.Stopwatch]::StartNew()
            $next = 0
            while (($n = $in.Read($buf, 0, $buf.Length)) -gt 0) {
                $out.Write($buf, 0, $n)
                $got += $n
                if ($sw.ElapsedMilliseconds -ge $next) {
                    $next = $sw.ElapsedMilliseconds + 250
                    $pct = 0
                    if ($total -gt 0) { $pct = [int][Math]::Min(100, $got * 100 / $total) }
                    Write-Progress -Activity 'Downloading the update' -Status ("{0:N1} of {1:N1} MB ({2}%)" -f ($got / 1MB), ($total / 1MB), $pct) -PercentComplete $pct
                }
            }
        } finally {
            $out.Dispose()
            $in.Dispose()
            Write-Progress -Activity 'Downloading the update' -Completed
        }
        Write-Host ("  Downloaded {0:N1} MB in {1:N1} s." -f ($got / 1MB), $sw.Elapsed.TotalSeconds) -ForegroundColor Gray
    } finally { $resp.Close() }
}

# Downloads, checks and unpacks the package, then hands over to its Update.ps1 in a new
# window and ends this script. Returns only if something failed, having said what, with
# nothing in the install touched.
function Install-Package($j) {
    $work = Join-Path $env:TEMP 'sl-update'
    try {
        if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
        New-Item -ItemType Directory -Path $work -Force | Out-Null
        $zip = Join-Path $work 'package.zip'
        Write-Host ("  Downloading {0:N0} MB..." -f ([long]$j.bytes / 1MB)) -ForegroundColor Gray
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Save-Download $j.url $zip ([long]$j.bytes)
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
        # So the game comes back as the same profile. Only to an updater that knows -Account
        # (2026-10-02 on): an older one would refuse the whole command line, and then the
        # update would not run at all. Without it the relaunch is plain -Test, once.
        if ($target.Profile) {
            $knows = $false
            try { $knows = (Get-Command -Name $upd -CommandType ExternalScript).Parameters.ContainsKey('Account') } catch {}
            if ($knows) { $a += " -Account $($target.Profile)" }
        }
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
            if ([version]$remote.version -gt $local) {
                Write-Host "  A newer Shattered Legacy package is out: $($remote.version) (you have $localText)." -ForegroundColor Cyan
            } elseif ([version]$remote.version -eq $local) {
                Write-Host "  -Update: reinstalling the current Shattered Legacy package, $($remote.version). You already have it." -ForegroundColor Cyan
            } else {
                Write-Host "  -Update: the server has $($remote.version), which is OLDER than yours ($localText). Installing it anyway." -ForegroundColor Cyan
            }
            if ($remote.notes) { Write-Host "  What is new: $($remote.notes)" -ForegroundColor Cyan }
            Write-Host ("  About {0:N0} MB. Your account, settings, Ultima Online folder and gump positions are kept." -f ([long]$remote.bytes / 1MB)) -ForegroundColor Gray
            $ans = Read-Host '  Update now? [Y/n]'
            if ($ans -match '^\s*(y|yes)?\s*$') { Install-Package $remote }
            else { Write-Host '  Skipped. You will be asked again next time.' -ForegroundColor Gray }
        }
    }
}

# --- Legion scripts (cc-P144) ------------------------------------------------------
# After the package check: the published scripts, quietly, applied without a prompt (low risk: a script the
# player edited is backed up first, and one the manifest does not list is never touched). One line when something
# changed. Offline or a server problem is one grey line and the game starts. Update-Scripts.ps1 says how.
# A -ClientDir client is remembered in app\client-dirs.txt, so the updater (and Update Scripts.bat) keep it current.
if ($target.ClientDir) {
    try {
        $cdFile = Join-Path $app 'client-dirs.txt'
        $cdKnown = @()
        if (Test-Path -LiteralPath $cdFile) { $cdKnown = @(Get-Content -LiteralPath $cdFile | ForEach-Object { $_.Trim() }) }
        if ($cdKnown -notcontains $target.ClientDir) { Add-Content -LiteralPath $cdFile -Value $target.ClientDir -Encoding ascii }
    } catch {}
}
if (-not $NoScripts) {
    try {
        $usArgs = @{ Root = $root; Quiet = $true; Offer = $true }
        if ($target.ClientDir) { $usArgs.ClientDir = @($target.ClientDir) }
        & (Join-Path $app 'Update-Scripts.ps1') @usArgs
    } catch {
        Write-Host "  Legion scripts not checked ($($_.Exception.Message)). Starting anyway." -ForegroundColor DarkGray
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
    Write-Host '  The server may be down or restarting, or this computer is offline. Try again in a few minutes, then ask Chase.' -ForegroundColor Red
    exit 1
}

# --- settings -------------------------------------------------------------------
# Launch-Settings.ps1: keeps the saved login, rewrites only what the launcher owns.
Write-LaunchSettings -Path $settingsPath -Target $target -UoDir $uoDir -ClientVersion $clientVersion -Test:$Test

Write-Host ''
Write-Host "  $label" -ForegroundColor Cyan
Write-Host "  server   $server`:$port"            -ForegroundColor Gray
Write-Host "  UO files $uoDir ($clientVersion)"   -ForegroundColor Gray
if ($target.ClientDir) { Write-Host "  client   $($target.ClientDir)" -ForegroundColor Gray }
if ($Test) { Write-Host '  TEST SHARD: nothing here is kept.' -ForegroundColor Yellow }
Write-Host ''
Write-Host '  First time? Type any account name and password you like. The first login' -ForegroundColor Gray
Write-Host '  creates your account, so remember them.' -ForegroundColor Gray
Write-Host ''

# --- our own art -------------------------------------------------------------------------
# Patched copies of this computer's own tiledata.mul and animdata.mul, so our art animates
# (Build-UoOverrides.ps1, cc-P26). Fast when nothing changed. Any failure there is one yellow
# line and the game starts without them: our art then shows still, never the game not at all.
# cc-P411: through Invoke-UoOverrides (Overrides-Launch.ps1): a rebuild runs in the background with a bar and a time
# limit, into a staging folder, so the game always starts, with the last good set when the new one is not ready.
$uoOverride = $null
try {
    . (Join-Path $app 'Overrides-Launch.ps1')
    if ($SkipArtRebuild) {
        $cur = Join-Path $app 'uo-overrides\files-override.txt'
        if (Test-Path -LiteralPath $cur) { $uoOverride = $cur }
        Write-Host "  -SkipArtRebuild: starting with the art as it is now$(if (-not $uoOverride) { ' (none built yet)' }), nothing checked." -ForegroundColor Yellow
    } else {
        $uoOverride = Invoke-UoOverrides -App $app -UoDir $uoDir -NoWaitForRunning:$ArtHandoff
    }
} catch {
    Write-Host "  Shattered Legacy art could not be prepared ($($_.Exception.Message)). Starting without it." -ForegroundColor Yellow
}
# Launch-Settings.ps1's Get-TazUOLaunch: the settings file, -skiploginscreen for a profile, the overrides, and for
# -ClientDir that folder's client with this install's profiles.
$launch = Get-TazUOLaunch -TazDir $taz -Target $target -SettingsPath $settingsPath -UoOverride $uoOverride
if ($target.ClientDir) {
    try {
        foreach ($c in @(Sync-ClientDirData -TazDir $taz -ClientDir $target.ClientDir)) { Write-Host "  copied to that client: $c" -ForegroundColor Gray }
    } catch {
        Write-Host "  This install's data could not be copied to that client ($($_.Exception.Message)). Starting anyway." -ForegroundColor Yellow
    }
}

# cc-P81: the paperdoll's six body gumps: the shipped SFW set, or this computer's local add-on when chosen.
try {
    $pdLine = Sync-PaperdollBodies -App $app -Root $root -SettingsPath $settingsPath -TazDir $launch.Dir
    if ($pdLine) { Write-Host $pdLine -ForegroundColor Gray }
} catch {
    Write-Host "  Paperdoll bodies could not be checked ($($_.Exception.Message)). Starting anyway." -ForegroundColor Yellow
}

# Launch-Settings.ps1's Start-TazUO: on a PC with no OpenGL 2.1 driver it switches the client to its
# automatic graphics driver (force_driver 3) and starts it again (cc-P52, D61). Waits up to 20 seconds.
Write-Host '  Starting the game...' -ForegroundColor Gray
$null = Start-TazUO -Exe $launch.Exe -Arguments $launch.Arguments -TazDir $launch.Dir -SettingsDir $taz -SettingsPath $settingsPath
exit 0
