#Requires -Version 5.1
<#
  Runs INSIDE Windows Sandbox when Chase double-clicks Collect-Evidence.bat on its desktop,
  with the game still running. Writes C:\SL\results\evidence.txt (evidence-2.txt and so on if
  run again), which survives the sandbox closing.

  Reads, never changes: the package's version, app\uo-path.txt and the client.exe it points at,
  whether TazUO.exe is running and which native modules it loaded (flagging zlib, vcruntime,
  msvcp, d3d, opengl, vulkan), app\tazuo\settings*.json with the password replaced, recent TazUO
  log or crash files, EA's patcher logs, any Zone.Identifier left on the package's files, the
  downloaded zip's own Zone.Identifier, and Windows' application-error events for TazUO.
  Copies EA's logs\patcher.*.Log files to results\ea-logs\. Never copies the Ultima Online data
  folder. The parameters exist only to dry-run it outside the sandbox. ASCII only
  (PowerShell 5.1).
#>
param(
    [string]$Results    = 'C:\SL\results',
    [string]$SearchRoot = $env:USERPROFILE
)
$ErrorActionPreference = 'Continue'
$results = $Results
$out = New-Object Collections.Generic.List[string]
function Say([string]$s) { $out.Add($s) }
function Section([string]$s) { $out.Add(''); $out.Add("== $s") }
function Tail([string]$path, [int]$n) { Get-Content -LiteralPath $path -Tail $n -ErrorAction SilentlyContinue | ForEach-Object { Say "    $_" } }

Say 'Shattered Legacy clean-PC test: evidence'
Say ("written   {0}" -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
$base = Join-Path $results 'baseline.txt'
if (Test-Path -LiteralPath $base) { Say "baseline  $((Get-Content -LiteralPath $base -TotalCount 2)[1])" }

# --- the package ---------------------------------------------------------------------------
Section 'package'
$home_ = $SearchRoot
$pv = @(Get-ChildItem -LiteralPath $home_ -Recurse -Depth 6 -Filter 'package-version.txt' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Name -eq 'app' } | Sort-Object LastWriteTime -Descending)
if (-not $pv.Count) { Say 'NO extracted package found under the user profile (app\package-version.txt).' }
$root = $null
foreach ($p in $pv) { Say "found $($p.Directory.Parent.FullName), version $((Get-Content -LiteralPath $p.FullName -TotalCount 1).Trim())" }
if ($pv.Count) { $root = $pv[0].Directory.Parent.FullName }

$uoDir = $null
if ($root) {
    $app = Join-Path $root 'app'
    $vt = Join-Path $app 'VERSION.txt'
    if (Test-Path -LiteralPath $vt) { Get-Content -LiteralPath $vt | ForEach-Object { Say "  $_" } }
    $up = Join-Path $app 'uo-path.txt'
    if (Test-Path -LiteralPath $up) {
        $uoDir = (Get-Content -LiteralPath $up -TotalCount 1).Trim()
        Say "uo-path.txt: EXISTS, written $((Get-Item -LiteralPath $up).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')), says: $uoDir"
    } else { Say 'uo-path.txt: absent (Setup did not finish)' }
}

Section 'Ultima Online install'
foreach ($k in 'HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic',
               'HKLM:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic',
               'HKCU:\SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic') {
    $p = Get-ItemProperty $k -ErrorAction SilentlyContinue
    Say ("{0}: {1}" -f $k, $(if ($p) { "InstallDir=$($p.InstallDir)" } else { 'absent' }))
}
if (-not $uoDir) {
    $p = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic' -ErrorAction SilentlyContinue
    if ($p) { $uoDir = $p.InstallDir; Say "(using the registry InstallDir, since uo-path.txt is absent)" }
}
if ($uoDir -and (Test-Path -LiteralPath $uoDir)) {
    $ce = Join-Path $uoDir 'client.exe'
    if (Test-Path -LiteralPath $ce) {
        $vi = (Get-Item -LiteralPath $ce).VersionInfo
        Say "client.exe: file version $($vi.FileVersion), product version $($vi.ProductVersion), written $((Get-Item -LiteralPath $ce).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
    } else { Say "client.exe: absent in $uoDir" }
    $bytes = (Get-ChildItem -LiteralPath $uoDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    Say ("folder size: {0:N0} bytes" -f $bytes)
    Section "EA patcher logs ($uoDir\logs), copied to results\ea-logs\"
    $logs = @(Get-ChildItem -LiteralPath (Join-Path $uoDir 'logs') -File -ErrorAction SilentlyContinue)
    if (-not $logs.Count) { Say 'none' }
    $eaOut = Join-Path $results 'ea-logs'
    foreach ($l in $logs) {
        New-Item -ItemType Directory -Path $eaOut -Force | Out-Null
        Copy-Item -LiteralPath $l.FullName -Destination (Join-Path $eaOut $l.Name) -Force
        Say "$($l.Name), $($l.Length) bytes, last 25 lines:"
        Tail $l.FullName 25
    }
} else { Say 'no Ultima Online folder found' }

# --- the game ----------------------------------------------------------------------------
Section 'TazUO process'
$procs = @(Get-Process TazUO -ErrorAction SilentlyContinue)
if (-not $procs.Count) { Say 'TazUO.exe is NOT running.' }
foreach ($p in $procs) {
    Say "TazUO.exe running, pid $($p.Id), started $($p.StartTime.ToString('yyyy-MM-dd HH:mm:ss')), window '$($p.MainWindowTitle)'"
    Say "path $($p.Path)"
    $mods = @($p.Modules)
    Say "$($mods.Count) modules loaded. Flagged (zlib, vcruntime, msvcp, d3d, opengl, vulkan):"
    foreach ($m in $mods | Where-Object { $_.ModuleName -match 'zlib|vcruntime|msvcp|d3d|opengl|vulkan' }) { Say "  FLAG $($m.ModuleName)  $($m.FileName)" }
    Say 'All loaded modules (anything outside the package folder came from Windows):'
    foreach ($m in $mods | Sort-Object FileName) { Say "  $($m.FileName)" }
}

Section 'Windows application errors in the last 3 hours (TazUO, .NET, EA)'
$ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddHours(-3); Level = 1, 2 } -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match 'TazUO|\.NET Runtime|UO\.exe|UO\.bin|client\.exe|zlib|vcruntime' -or $_.ProviderName -match '\.NET Runtime|Application Error' })
if (-not $ev.Count) { Say 'none' }
foreach ($e in $ev | Select-Object -First 10) { Say "$($e.TimeCreated.ToString('HH:mm:ss')) $($e.ProviderName) $($e.Id):"; ($e.Message -split "`r?`n" | Select-Object -First 12) | ForEach-Object { Say "    $_" } }

if ($root) {
    $taz = Join-Path $root 'app\tazuo'
    Section 'TazUO settings (password redacted)'
    foreach ($s in Get-ChildItem -LiteralPath $taz -Filter 'settings*.json' -File -ErrorAction SilentlyContinue) {
        Say $s.Name
        try {
            $j = [IO.File]::ReadAllText($s.FullName) | ConvertFrom-Json
            if ($j.PSObject.Properties['password']) { $j.password = '<redacted>' }
            # PowerShell 5.1 writes < and > as < and >; put them back so it reads <redacted>.
            (($j | ConvertTo-Json -Depth 10) -replace '\\u003c', '<' -replace '\\u003e', '>') -split "`r?`n" | ForEach-Object { Say "    $_" }
        } catch { Say "    does not parse: $($_.Exception.Message)" }
    }

    Section 'TazUO logs and crash files changed in the last 3 hours (last 40 lines each)'
    $since = (Get-Date).AddHours(-3)
    $lf = @(Get-ChildItem -LiteralPath $taz -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -gt $since -and ($_.Extension -match '^\.(log|txt)$' -or $_.Name -match 'crash') -and $_.FullName -notmatch '\\Data\\Client\\' })
    if (-not $lf.Count) { Say 'none' }
    foreach ($f in $lf) { Say $f.FullName.Substring($root.Length + 1); Tail $f.FullName 40 }

    Section 'Zone.Identifier still on package files (expected: none after the first start)'
    $marked = @(Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object { Get-Item -LiteralPath $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue })
    Say "$($marked.Count) files still marked"
    foreach ($f in $marked | Select-Object -First 20) { Say "  $($f.FullName.Substring($root.Length + 1))" }
}

Section 'the downloaded zip and its Zone.Identifier'
foreach ($z in @(Get-ChildItem -LiteralPath $home_ -Recurse -Depth 3 -Filter 'ShatteredLegacy*.zip' -File -ErrorAction SilentlyContinue)) {
    Say "$($z.FullName), $($z.Length) bytes, sha256 $((Get-FileHash -LiteralPath $z.FullName -Algorithm SHA256).Hash.ToLower())"
    $zi = Get-Content -LiteralPath $z.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
    if ($zi) { $zi | ForEach-Object { Say "    $($_ -replace [char]0, '<NUL>')" } } else { Say '    no Zone.Identifier' }
}

Section 'launcher window'
Say 'Play.ps1 and Setup.ps1 do not write what they print to a file (only Update.ps1 keeps'
Say 'update-log.txt). Copy the launcher window by hand or screenshot it; logging is a follow-up.'

$n = 1; $path = Join-Path $results 'evidence.txt'
while (Test-Path -LiteralPath $path) { $n++; $path = Join-Path $results "evidence-$n.txt" }
$ascii = $out | ForEach-Object { $_ -replace '[^\x09\x20-\x7E]', '?' }
[IO.File]::WriteAllLines($path, [string[]]$ascii, (New-Object Text.ASCIIEncoding))
Write-Host ''
Write-Host "  Written: $path" -ForegroundColor Green
Write-Host '  It is on the host too. You can close the sandbox now.' -ForegroundColor Green
Write-Host ''
