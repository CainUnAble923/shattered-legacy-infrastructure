#Requires -Version 5.1
<#
  Which server and settings file a launch uses, and what the launcher writes into that file.
  Dot-sourced by Play.ps1; player-package\tests\Launch-Settings.Tests.ps1 calls the same
  functions, so the launcher's choices can be tested without starting the game.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as
  Windows-1252, and a non-ASCII character can become syntax.
#>

# The target for -Lan, -Test or neither, and for -Test -Account <profile>. Throws one line,
# meant to be shown in red, when the switches do not make sense together.
#
# A profile is a name for one saved login, not the UO account name: admin, human, gargoyle,
# elf, or any 1 to 24 of a-z, 0-9 and "-". Lower case only, because Windows file names are
# not case-sensitive and "Elf" and "elf" would share a file. It becomes part of a file name,
# so nothing else gets through. TEST only: a shortcut must never log anyone in to live.
function Get-LaunchTarget {
    param([switch]$Lan, [switch]$Test, [string]$Account)
    if ($Lan -and $Test) { throw 'Pick one of -Lan or -Test.' }
    $hasAccount = $PSBoundParameters.ContainsKey('Account')
    if ($hasAccount -and -not $Test) { throw '-Account works only with -Test, so the live shard is never logged in automatically.' }
    if ($hasAccount -and $Account -cnotmatch '^[a-z0-9-]{1,24}$') { throw "-Account '$Account' is not a profile name: use 1 to 24 of a-z, 0-9 and -." }
    if     ($Test) { $t = @{ Server = 'shatteredlegacyuo.com'; Port = 2594; SettingsName = 'settings.test.json'; Label = 'TEST SHARD (throwaway)';            ServerName = 'Shattered Legacy TEST' } }
    elseif ($Lan)  { $t = @{ Server = '192.168.1.58';          Port = 2593; SettingsName = 'settings.json';      Label = 'Shattered Legacy (house network)'; ServerName = 'Shattered Legacy' } }
    else           { $t = @{ Server = 'shatteredlegacyuo.com'; Port = 2593; SettingsName = 'settings.json';      Label = 'Shattered Legacy';                 ServerName = 'Shattered Legacy' } }
    $t.Profile = ''
    if ($hasAccount) {
        $t.Profile      = $Account
        $t.SettingsName = "settings.test.$Account.json"
        $t.Label        = "TEST SHARD (throwaway), profile: $Account"
    }
    return [pscustomobject]$t
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
function Start-TazUO {
    param([string]$Exe, [string[]]$Arguments, [string]$TazDir, [string]$SettingsPath, [int]$WaitSeconds = 20)
    $logs = Join-Path $TazDir 'Logs'

    if ((Get-ForceDriver $SettingsPath) -eq 0 -and (Find-OpenGlCrash $logs)) {
        $null = Set-GraphicsFallback $TazDir
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

    $null = Set-GraphicsFallback $TazDir
    Write-Host '  This computer has no OpenGL 2.1 driver. Starting the game again with its automatic graphics driver.' -ForegroundColor Yellow
    Start-Process -FilePath $Exe -ArgumentList $Arguments -WorkingDirectory $TazDir | Out-Null
    return 'fallback'
}
