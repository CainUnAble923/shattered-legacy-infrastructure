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
