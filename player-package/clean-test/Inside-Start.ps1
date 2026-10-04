#Requires -Version 5.1
<#
  Runs INSIDE Windows Sandbox at logon (the .wsb LogonCommand that Start-CleanTest.ps1 writes).

  1. Writes C:\SL\results\baseline.txt BEFORE anything is installed, proving the box is clean:
     Windows version and build, dotnet (expected: not found), Visual C++ runtime (expected:
     none), EA's Ultima Online key (expected: none), the GPU adapter, and whether each DLL on
     the package's gate 6 allowlist (native-import-allowlist.txt) is really in this fresh
     Windows.
  2. Build mode: copies the zip to the desktop as ShatteredLegacy-Setup.zip, checks its
     sha256, and gives it the Zone.Identifier stream a browser download gets (ZoneId=3).
     Published mode: puts a shortcut to the download URL on the desktop.
  3. Puts READ ME FIRST.txt, Collect-Evidence.bat and Collect-Evidence.ps1 on the desktop.

  Installs nothing and clicks nothing. The parameters exist only to dry-run it outside the
  sandbox; the sandbox uses the defaults. ASCII only (PowerShell 5.1).
#>
param(
    [string]$Kit     = 'C:\SL\kit',
    [string]$Results = 'C:\SL\results',
    [string]$Dist    = 'C:\SL\dist',
    [string]$Desktop = [Environment]::GetFolderPath('Desktop'),
    [switch]$NoNotepad
)
$ErrorActionPreference = 'Stop'
$kit = $Kit; $results = $Results; $desktop = $Desktop
$out     = New-Object Collections.Generic.List[string]
function Say([string]$s) { $out.Add($s) }
function Section([string]$s) { $out.Add(''); $out.Add("== $s") }

try {
    $run = [IO.File]::ReadAllText((Join-Path $kit 'run.json')) | ConvertFrom-Json

    # --- baseline -----------------------------------------------------------------------------
    Say "Shattered Legacy clean-PC test: baseline, written before anything was installed"
    Say ("written   {0}" -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
    Say "mode      $($run.mode)$(if ($run.zip) { ", zip $($run.zip) (version $($run.version))" })"
    Say "vGPU      $(if ($run.vgpu) { 'on (host GPU)' } else { 'off (software rendering)' })"
    Say "user      $env:USERNAME on $env:COMPUTERNAME"

    Section 'Windows'
    $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $os = Get-CimInstance Win32_OperatingSystem
    Say "$($os.Caption), version $($cv.DisplayVersion), build $($cv.CurrentBuildNumber).$($cv.UBR), $($os.OSArchitecture)"

    Section 'dotnet --list-runtimes (expected: not found)'
    $dn = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dn) { Say "dotnet FOUND at $($dn.Source)"; (& $dn.Source --list-runtimes 2>&1) | ForEach-Object { Say "  $_" } }
    else { Say 'dotnet: not found' }
    foreach ($d in "$env:ProgramFiles\dotnet\shared", "${env:ProgramFiles(x86)}\dotnet\shared") {
        Say ("{0}: {1}" -f $d, $(if (Test-Path -LiteralPath $d) { 'EXISTS: ' + ((Get-ChildItem -LiteralPath $d -Directory | ForEach-Object Name) -join ', ') } else { 'absent' }))
    }

    Section 'Visual C++ runtime (expected: none)'
    $vc = @()
    foreach ($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*') {
        $vc += @(Get-ItemProperty $k -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match 'Visual C\+\+' } | ForEach-Object { "$($_.DisplayName) $($_.DisplayVersion)" })
    }
    if ($vc.Count) { $vc | ForEach-Object { Say "uninstall key: $_" } } else { Say 'uninstall keys: none' }
    foreach ($k in 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X64') {
        $p = Get-ItemProperty $k -ErrorAction SilentlyContinue
        Say ("{0}: {1}" -f $k, $(if ($p) { "PRESENT, version $($p.Version)" } else { 'absent' }))
    }
    foreach ($n in 'vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll') {
        $f = Join-Path $env:windir "System32\$n"
        Say ("System32\{0}: {1}" -f $n, $(if (Test-Path -LiteralPath $f) { 'PRESENT, ' + (Get-Item -LiteralPath $f).VersionInfo.FileVersion } else { 'absent' }))
    }

    Section 'EA Ultima Online Classic (expected: none)'
    foreach ($k in 'HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic',
                   'HKLM:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic',
                   'HKCU:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic') {
        Say ("{0}: {1}" -f $k, $(if (Test-Path $k) { 'PRESENT' } else { 'absent' }))
    }
    foreach ($d in "${env:ProgramFiles(x86)}\Electronic Arts", "$env:ProgramFiles\Electronic Arts") {
        Say ("{0}: {1}" -f $d, $(if (Test-Path -LiteralPath $d) { 'EXISTS' } else { 'absent' }))
    }

    Section 'GPU'
    foreach ($g in Get-CimInstance Win32_VideoController) { Say "$($g.Name), driver $($g.DriverVersion), $($g.VideoModeDescription)" }

    # Each name the package's native files may import without shipping it. A plain DLL must be
    # in System32; an API set is resolved by Windows, so it is tested by loading it.
    Section 'gate 6 allowlist on this fresh Windows (every line should say ok)'
    Add-Type -Namespace SL -Name Native -MemberDefinition '[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr LoadLibraryW(string name);'
    $missing = 0
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $kit 'native-import-allowlist.txt'))) {
        $t = $line.Trim(); if (-not $t -or $t.StartsWith('#')) { continue }
        $n = ($t -split '\s+')[0]
        $file = Test-Path -LiteralPath (Join-Path $env:windir "System32\$n")
        $loads = [SL.Native]::LoadLibraryW($n) -ne [IntPtr]::Zero
        $ok = if ($n -like 'api-ms-win-*') { $loads } else { $file -and $loads }
        if (-not $ok) { $missing++ }
        Say ("{0,-40} {1}  (System32 file: {2}, loads: {3})" -f $n, $(if ($ok) { 'ok' } else { 'MISSING' }), $(if ($file) { 'yes' } else { 'no' }), $(if ($loads) { 'yes' } else { 'no' }))
    }
    Say "allowlist: $missing missing"
    $vcLoads = [SL.Native]::LoadLibraryW('vcruntime140.dll') -ne [IntPtr]::Zero
    Say "vcruntime140.dll loads from the system search path: $(if ($vcLoads) { 'YES (this box is not clean of it)' } else { 'no (expected)' })"

    Section 'verdict'
    $clean = (-not $dn) -and (-not $vc.Count) -and (-not $vcLoads) -and -not (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic')
    Say $(if ($clean) { 'CLEAN: no .NET, no Visual C++ runtime, no Ultima Online.' } else { 'NOT CLEAN: see above.' })

    [IO.File]::WriteAllLines((Join-Path $results 'baseline.txt'), $out, (New-Object Text.ASCIIEncoding))

    # --- the download ---------------------------------------------------------------------
    if ($run.mode -eq 'Build') {
        $dst = Join-Path $desktop 'ShatteredLegacy-Setup.zip'
        Copy-Item -LiteralPath (Join-Path $Dist $run.zip) -Destination $dst
        $h = (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash.ToLower()
        if ($h -cne $run.sha256) { throw "the copied zip's sha256 is $h, expected $($run.sha256)" }
        # What Edge and Chrome write on a download: ASCII, CRLF between lines, a NUL after the
        # last (read from a real browser download on the host, cc-P35 notes section 5).
        $zone = "[ZoneTransfer]`r`nZoneId=3`r`nHostUrl=$($run.url)" + [char]0
        Set-Content -LiteralPath $dst -Stream Zone.Identifier -Value ([Text.Encoding]::ASCII.GetBytes($zone)) -Encoding Byte
    } else {
        [IO.File]::WriteAllText((Join-Path $desktop 'Download Shattered Legacy.url'), "[InternetShortcut]`r`nURL=$($run.url)`r`n", (New-Object Text.ASCIIEncoding))
    }
    foreach ($n in 'READ ME FIRST.txt', 'Collect-Evidence.bat', 'Collect-Evidence.ps1') {
        Copy-Item -LiteralPath (Join-Path $kit $n) -Destination (Join-Path $desktop $n)
    }
    if (-not $NoNotepad) { Start-Process notepad.exe -ArgumentList "`"$(Join-Path $desktop 'READ ME FIRST.txt')`"" }
} catch {
    $out.Add(''); $out.Add("INSIDE-START FAILED: $($_.Exception.Message)")
    [IO.File]::WriteAllLines((Join-Path $results 'inside-start-error.txt'), $out, (New-Object Text.ASCIIEncoding))
}
