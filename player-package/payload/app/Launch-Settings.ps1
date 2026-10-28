#Requires -Version 5.1
<#
  Which server and settings file a launch uses, and what the launcher writes into that file.
  Dot-sourced by Play.ps1; player-package\tests\Launch-Settings.Tests.ps1 calls the same
  functions, so the launcher's choices can be tested without starting the game.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as
  Windows-1252, and a non-ASCII character can become syntax.
#>

# cc-P144: the Legion scripts' functions (published sets, the install record, the dev PCs' repo links).
. (Join-Path $PSScriptRoot 'LegionScripts.ps1')

# The target for -Lan or neither, and for -Account <profile>. Throws one line, meant to be shown in red, when
# the switches do not make sense together.
#
# cc-P411: live is retired (scripts\live.json, 2026-10-14; it returns as a fresh world at 1.0), so every target is
# the test shard, port 2594 and settings.test.json: Play.ps1 with no switch, and -Lan for inside the house
# (192.168.1.58:2594, no DNS). -Test is still taken, and means the same, because the shortcuts and every installed
# Play.ps1's updater pass it. The live target (port 2593, settings.json) is gone until live comes back.
#
# A profile is a name for one saved login, not the UO account name: admin, human, gargoyle,
# elf, or any 1 to 24 of a-z, 0-9 and "-". Lower case only, because Windows file names are
# not case-sensitive and "Elf" and "elf" would share a file. It becomes part of a file name,
# so nothing else gets through. TEST only: a shortcut must never log anyone in to live.
#
# cc-P114: -ClientDir <folder> starts the TazUO.exe in that folder instead of app\tazuo's (our own build, for
# Chase's test shortcuts). TEST only, like -Account, so live never runs a development build. Everything else is
# the same launch: the same settings file, autologin and overrides (Get-TazUOLaunch).
function Get-LaunchTarget {
    param([switch]$Lan, [switch]$Test, [string]$Account, [string]$ClientDir)
    $hasClient = $PSBoundParameters.ContainsKey('ClientDir')
    if ($hasClient -and -not ($ClientDir -and (Test-Path -LiteralPath (Join-Path $ClientDir 'TazUO.exe') -PathType Leaf))) {
        throw "-ClientDir '$ClientDir' holds no TazUO.exe."
    }
    $hasAccount = $PSBoundParameters.ContainsKey('Account')
    if ($hasAccount -and $Account -cnotmatch '^[a-z0-9-]{1,24}$') { throw "-Account '$Account' is not a profile name: use 1 to 24 of a-z, 0-9 and -." }
    if ($Lan) { $t = @{ Server = '192.168.1.58';          Port = 2594; SettingsName = 'settings.test.json'; Label = 'TEST SHARD (throwaway), house network'; ServerName = 'Shattered Legacy TEST' } }
    else      { $t = @{ Server = 'shatteredlegacyuo.com'; Port = 2594; SettingsName = 'settings.test.json'; Label = 'TEST SHARD (throwaway)';                 ServerName = 'Shattered Legacy TEST' } }
    $t.Profile = ''
    if ($hasAccount) {
        $t.Profile      = $Account
        $t.SettingsName = "settings.test.$Account.json"
        $t.Label        = "TEST SHARD (throwaway), profile: $Account"
    }
    $t.ClientDir = ''
    if ($hasClient) { $t.ClientDir = (Resolve-Path -LiteralPath $ClientDir).ProviderPath.TrimEnd('\') }
    return [pscustomobject]$t
}

# What Play.ps1 starts: which TazUO.exe, in which folder, with which arguments. $TazDir is the package's
# app\tazuo, which holds the settings files. A -ClientDir target starts that folder's client with the same
# arguments plus -profilespath, so it reads and writes the package's per-character profiles (Data\Profiles\
# <account>\<server>\<character>: gump layout, macros, hotkeys, spell bars, ...) instead of starting empty
# beside its own exe. TazUO reads the argument after its settings file and never saves it (Settings.cs).
function Get-TazUOLaunch {
    param([string]$TazDir, $Target, [string]$SettingsPath, [string]$UoOverride)
    $a = @('-settings', "`"$SettingsPath`"")
    # A profile logs straight in. TazUO's autologin alone does not: on a cold start it only
    # connects with -skiploginscreen (LoginScene.Load), and then only when the file holds a
    # saved username, so a profile with none yet stops at the login screen.
    if ($Target.Profile) { $a += '-skiploginscreen' }
    if ($UoOverride) { $a += @('-uofilesoverride', "`"$UoOverride`"") }
    $dir = $TazDir
    if ($Target.ClientDir) {
        $dir = $Target.ClientDir
        $a += @('-profilespath', "`"$(Join-Path $TazDir 'Data\Profiles')`"")
    }
    return [pscustomobject]@{ Exe = (Join-Path $dir 'TazUO.exe'); Dir = $dir; Arguments = $a }
}

# cc-P114: what TazUO keeps only beside its own exe, which -profilespath does not move, copied from the package's
# app\tazuo into a -ClientDir client when that client lacks it:
#   - the last character per account and server (Data\Profiles\lastcharacter.json, LastCharacterManager), which
#     autologin enters the world as; entries the client already has are its own and are kept;
#   - the Fiddle-Me-This HUD gumps the package ships (Data\XmlGumps\*.xml);
#   - the player's own Legion scripts (LegionScripts\*.py, but not API.py or __builtins__.py, which each client
#     writes for itself).
# Never overwrites a file in the client folder and never changes the package. Returns what it copied.
# cc-P144: the published Legion scripts are the exception. One the install holds as published (its .sl-scripts.json
# says so and the copy still matches) is updated in the client too when it changed, with the updater's rule: a copy
# the player edited there is kept in LegionScripts\backup\ first (Install-LegionSet). A client whose LegionScripts is
# a junction to the scripts repo (a dev PC) gets no scripts copied at all: they come from git.
function Sync-ClientDirData {
    param([string]$TazDir, [string]$ClientDir)
    $copied = @()

    $pkgLast = Join-Path $TazDir 'Data\Profiles\lastcharacter.json'
    $cliLast = Join-Path $ClientDir 'Data\Profiles\lastcharacter.json'
    if (Test-Path -LiteralPath $pkgLast) {
        # 5.1's ConvertFrom-Json passes a JSON array on as one object; ForEach-Object unrolls it.
        $from = @(); $to = @()
        try { $from = @([IO.File]::ReadAllText($pkgLast) | ConvertFrom-Json | ForEach-Object { $_ }) } catch {}
        if (Test-Path -LiteralPath $cliLast) {
            try { $to = @([IO.File]::ReadAllText($cliLast) | ConvertFrom-Json | ForEach-Object { $_ }) } catch { $from = @() }   # unreadable: leave it to TazUO
        }
        $added = @()
        foreach ($e in $from) {
            if (-not $e -or -not $e.AccountName) { continue }
            # TazUO matches both names exactly (LastCharacterManager.GetLastCharacter).
            if (@($to | Where-Object { $_.AccountName -ceq $e.AccountName -and $_.ServerName -ceq $e.ServerName }).Count) { continue }
            $to += $e; $added += $e.AccountName
        }
        if ($added.Count) {
            New-Item -ItemType Directory -Path (Split-Path $cliLast -Parent) -Force | Out-Null
            # No BOM, as Write-LaunchSettings.
            [IO.File]::WriteAllText($cliLast, (ConvertTo-Json -InputObject @($to) -Depth 5 -Compress), (New-Object Text.UTF8Encoding $false))
            $copied += "Data\Profiles\lastcharacter.json ($($added -join ', '))"
        }
    }

    $cliScripts = Join-Path $ClientDir 'LegionScripts'
    $scriptsLinked = (Test-LegionLink $cliScripts) -or (Test-LegionLink (Join-Path $TazDir 'LegionScripts'))
    $published = @()
    if (-not $scriptsLinked) {
        $published = @(Get-LegionInstalledEntries (Join-Path $TazDir 'LegionScripts'))
        if ($published.Count) {
            $res = Install-LegionSet -Folder $cliScripts -Entries $published -Fetch { param($e, $t) Copy-Item -LiteralPath $e.Url -Destination $t -Force }
            foreach ($l in @($res.Lines) + @($res.Failed)) { $copied += "LegionScripts\$l" }
        }
    }
    foreach ($set in @(@{ Dir = 'Data\XmlGumps'; Filter = '*.xml' }, @{ Dir = 'LegionScripts'; Filter = '*.py' })) {
        if ($set.Dir -eq 'LegionScripts' -and $scriptsLinked) { continue }
        $src = Join-Path $TazDir $set.Dir
        if (-not (Test-Path -LiteralPath $src)) { continue }
        foreach ($f in @(Get-ChildItem -LiteralPath $src -Filter $set.Filter -File | Sort-Object Name)) {
            if ($f.Name -in @('API.py', '__builtins__.py')) { continue }
            if ($set.Dir -eq 'LegionScripts' -and @($published | Where-Object { $_.Name -eq $f.Name }).Count) { continue }
            $dest = Join-Path (Join-Path $ClientDir $set.Dir) $f.Name
            if (Test-Path -LiteralPath $dest) { continue }
            New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $f.FullName -Destination $dest
            $copied += "$($set.Dir)\$($f.Name)"
        }
    }
    return $copied
}

# Writes the TazUO settings file at $Path for $Target. Keeps everything already in it (the
# saved username and password above all) and rewrites only what the launcher owns.
function Write-LaunchSettings {
    param([string]$Path, $Target, [string]$UoDir, [string]$ClientVersion, [switch]$Test)
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
    if (Test-Path -LiteralPath $Path) {
        try {
            $old = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
            foreach ($p in $old.PSObject.Properties) { $s[$p.Name] = $p.Value }
        } catch {
            Write-Host "  $(Split-Path $Path -Leaf) could not be read; starting it fresh." -ForegroundColor Yellow
        }
    }
    # Then force the parts this launcher owns.
    $s.ip                    = $Target.Server
    $s.port                  = $Target.Port
    $s.ultimaonlinedirectory = $UoDir
    $s.clientversion         = $ClientVersion
    $s.last_server_name      = $Target.ServerName
    # After login the server tells the client where the game server is (packet 0x8C). The
    # live shard currently names its LAN address there, which is unreachable from outside
    # the house. With this true, TazUO ignores that address AND port and reconnects to the
    # server it logged in to (LoginHandshake.HandleRelayServerPacket). It is what makes the
    # default target work from outside, and what keeps -Test from ever landing on live.
    $s.ignore_relay_ip       = $true
    # A populated plugins array makes the client Assembly.LoadFile a plugin and can crash it.
    $s.plugins               = @()
    if ($Target.Profile) {
        # A profile's file holds only that profile's TEST login, so it may keep one, and
        # TazUO logs straight in with it: autologin picks the server (last_server_name) and the
        # last character (Data\Profiles\lastcharacter.json). With no login saved yet, TazUO
        # shows the login screen with Save Account already ticked.
        $s.saveaccount = $true; $s.autologin = $true
    } elseif ($Test) {
        # The plain test file: a test login is never saved, so it is never offered to live.
        $s.saveaccount = $false; $s.autologin = $false
    }

    # PowerShell 5.1's -Encoding UTF8 writes a BOM, and TazUO's settings reader falls back to
    # defaults on one WITHOUT SAYING SO: the client starts and connects nowhere. No BOM.
    [IO.File]::WriteAllText($Path, ($s | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding $false))
}

# cc-P719: what a NEW character's TazUO profile starts with. TazUO builds a character's first profile.json from
# <profiles>\default.json when there is one, else from its own defaults, and a key the file leaves out keeps TazUO's
# default (ProfileManager.Load, NewFromDefault). Its own defaults run all the time (Profile.AlwaysRun = true) and
# open the paperdoll at every login (LoginComplete; client patch 0009 adds the switch). New players start walking,
# with the paperdoll closed; Options > Gameplay > Movement > Always run turns running on.
$NewProfileDefaults = [ordered]@{ always_run = $false; sl_open_paperdoll_at_login = $false }

# Writes $ProfilesDir\default.json only where there is none, so a default the player saved (Options > Profile,
# "Set as default") is kept, and a character that already has a profile.json never reads it: a returning player's
# settings and layout stay theirs. Returns $true when it wrote the file.
function Initialize-NewProfileDefaults {
    param([string]$ProfilesDir)
    $f = Join-Path $ProfilesDir 'default.json'
    if (Test-Path -LiteralPath $f) { return $false }
    New-Item -ItemType Directory -Path $ProfilesDir -Force | Out-Null
    # No BOM, as Write-LaunchSettings.
    [IO.File]::WriteAllText($f, ($NewProfileDefaults | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
    return $true
}

# ---- graphics driver fallback (cc-P52 Part C, bug-list D61) ---------------------------------------
# TazUO picks its renderer from force_driver (settings file or -force_driver): 1 OpenGL, 2 Vulkan,
# 3 "SDL/FNA auto-select", and anything else, 0 included, falls through to OpenGL (TazUO 3212623f,
# src/ClassicUO.Client/Main.cs:211-227, "default: case 1"). So every player runs OpenGL today. A PC
# with no OpenGL 2.1 driver (a VM, Windows Sandbox, Microsoft Basic Display) then crashes at start:
# "OpenGL 2.1 support is required!". 3 sets no driver and lets FNA3D try SDL_GPU, then D3D11, then
# OpenGL (FNA3D.c:42-53), which started the game in P35's sandbox on D3D11 through WARP.
#
# On a PC with a real GPU, 3 would move the player off OpenGL to SDL_GPU (Vulkan or Direct3D 12,
# whichever SDL picks), so it is not forced on everyone: the launcher switches to 3 only on a PC
# where TazUO has logged that crash, and only where force_driver is still TazUO's default 0. A
# player's own choice in the client (1, 2 or 3) is never changed.

$GraphicsFallbackDriver = 3
$OpenGlCrashText = 'OpenGL 2.1 support is required!'

# force_driver in a TazUO settings file; 0 (TazUO's default) when the file, the key or the JSON is missing.
function Get-ForceDriver {
    param([string]$Path)
    if (-not $Path -or -not (Test-Path -LiteralPath $Path)) { return 0 }
    try { $j = [IO.File]::ReadAllText($Path) | ConvertFrom-Json } catch { return 0 }
    if ($null -eq $j -or $null -eq $j.force_driver) { return 0 }
    $v = 0
    if ([int]::TryParse([string]$j.force_driver, [ref]$v)) { return $v }
    return 0
}

# The newest crash log TazUO wrote at or after $Since (Logs\<stamp>_crash.txt, Main.cs:88-96) that
# names the missing OpenGL 2.1 driver, or $null.
function Find-OpenGlCrash {
    param([string]$LogsDir, [datetime]$Since = [datetime]::MinValue)
    if (-not $LogsDir -or -not (Test-Path -LiteralPath $LogsDir)) { return $null }
    $logs = @(Get-ChildItem -LiteralPath $LogsDir -Filter '*crash.txt' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $Since } | Sort-Object LastWriteTime -Descending)
    foreach ($log in $logs) {
        try { $text = [IO.File]::ReadAllText($log.FullName) } catch { continue }
        if ($text.Contains($OpenGlCrashText)) { return $log }
    }
    return $null
}

# Writes force_driver 3 into every settings*.json in $TazDir whose force_driver is still 0, keeping
# everything else in it. Returns the names it changed.
function Set-GraphicsFallback {
    param([string]$TazDir)
    $changed = @()
    foreach ($f in @(Get-ChildItem -LiteralPath $TazDir -Filter 'settings*.json' -File -ErrorAction SilentlyContinue)) {
        if ((Get-ForceDriver $f.FullName) -ne 0) { continue }
        try { $j = [IO.File]::ReadAllText($f.FullName) | ConvertFrom-Json } catch { continue }
        if ($null -eq $j) { continue }
        $j | Add-Member -NotePropertyName force_driver -NotePropertyValue $GraphicsFallbackDriver -Force
        # No BOM, as Write-LaunchSettings: TazUO reads a BOM'd file as defaults.
        [IO.File]::WriteAllText($f.FullName, ($j | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding $false))
        $changed += $f.Name
    }
    return $changed
}

# Starts TazUO. Before: a PC that has already logged the OpenGL crash gets force_driver 3 first. After:
# waits up to $WaitSeconds; if TazUO exits having logged that crash, switches to 3 and starts it once
# more. Returns 'running' (still up when the wait ended), 'exited', 'crashed' (the crash, on a player's
# own force_driver, left alone) or 'fallback' (started again on 3).
# $TazDir is the client's folder (its working folder and Logs); $SettingsDir holds the settings files, the same
# folder unless the client was started from another one (-ClientDir, cc-P114).
function Start-TazUO {
    param([string]$Exe, [string[]]$Arguments, [string]$TazDir, [string]$SettingsPath, [int]$WaitSeconds = 20, [string]$SettingsDir)
    $logs = Join-Path $TazDir 'Logs'
    if (-not $SettingsDir) { $SettingsDir = $TazDir }

    if ((Get-ForceDriver $SettingsPath) -eq 0 -and (Find-OpenGlCrash $logs)) {
        $null = Set-GraphicsFallback $SettingsDir
        Write-Host '  This computer has no OpenGL 2.1 driver; the game uses its automatic graphics driver.' -ForegroundColor Yellow
    }

    # Only a crash log written after this start counts (an earlier one was handled above, or was a player's own driver).
    $since = Get-Date
    $p = Start-Process -FilePath $Exe -ArgumentList $Arguments -WorkingDirectory $TazDir -PassThru
    if (-not $p.WaitForExit($WaitSeconds * 1000)) { return 'running' }
    if (-not (Find-OpenGlCrash $logs $since)) { return 'exited' }

    $own = Get-ForceDriver $SettingsPath
    if ($own -ne 0) {
        Write-Host "  The game could not start its graphics with your own setting (force_driver $own)." -ForegroundColor Red
        Write-Host '  Change Force driver in the client''s login options, or ask Chase.' -ForegroundColor Red
        return 'crashed'
    }

    $null = Set-GraphicsFallback $SettingsDir
    Write-Host '  This computer has no OpenGL 2.1 driver. Starting the game again with its automatic graphics driver.' -ForegroundColor Yellow
    Start-Process -FilePath $Exe -ArgumentList $Arguments -WorkingDirectory $TazDir | Out-Null
    return 'fallback'
}
