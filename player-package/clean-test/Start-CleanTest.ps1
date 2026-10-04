#Requires -Version 5.1
<#
  Start a Windows Sandbox that is a clean PC (no UO, no .NET, no Visual C++ runtime, no GPU
  driver) and set it up for one first-run test of the player package. Runs on the host.

      .\player-package\clean-test\Start-CleanTest.ps1                    # Published mode
      .\player-package\clean-test\Start-CleanTest.ps1 -Mode Build        # newest dist\*.zip
      .\player-package\clean-test\Start-CleanTest.ps1 -Mode Build -Zip D:\x\ShatteredLegacy-2026.10.03.1316.zip
      add -VGpu to give the sandbox the host GPU (only if the first pass had a graphics problem)

  -Mode Published (default)  the sandbox's own browser downloads the zip from
                             get.shatteredlegacyuo.com, exactly like a player, so Mark of the
                             Web is real.
  -Mode Build                the zip's dist folder is mapped in READ ONLY; Inside-Start.ps1 copies
                             the zip to the sandbox desktop as ShatteredLegacy-Setup.zip and gives
                             the copy the Zone.Identifier stream a browser download gets
                             (ZoneId=3), so it behaves like a download.

  Each run gets D:\UO\clean-test-results\<yyyyMMdd-HHmmss>\ (outside both repos):
      clean-test.wsb   the sandbox configuration, absolute host paths
      kit\             a copy of this folder with CRLF line ends, mapped READ ONLY at C:\SL\kit
      results\         mapped READ/WRITE at C:\SL\results: baseline.txt, evidence.txt, ...
  Closing the sandbox throws everything inside it away; only results\ survives.

  .wsb elements (Configuration, vGPU, Networking, MemoryInMB, MappedFolders, MappedFolder,
  HostFolder, SandboxFolder, ReadOnly, LogonCommand, Command) are as documented at
  learn.microsoft.com/windows/security/application-security/application-isolation/
  windows-sandbox/windows-sandbox-configure-using-wsb-file. vGPU is Disable by default: the
  clean case is a PC with weak or no graphics, which the sandbox then renders with WARP
  (software).

  -WriteOnly writes the run folder and the .wsb and does not open the sandbox (for checking
  the kit on a PC where Windows Sandbox is off).

  ASCII only: PowerShell 5.1 reads BOM-less UTF-8 as Windows-1252.
#>
[CmdletBinding()]
param(
    [ValidateSet('Published', 'Build')]
    [string]$Mode = 'Published',
    [string]$Zip,
    [switch]$VGpu,
    [string]$ResultsRoot = 'D:\UO\clean-test-results',
    [switch]$WriteOnly
)

$ErrorActionPreference = 'Stop'
$kitSrc  = $PSScriptRoot
$pkgRoot = Split-Path $kitSrc -Parent
$url     = 'https://get.shatteredlegacyuo.com/ShatteredLegacy-Setup.zip'

# --- is Windows Sandbox on? ---------------------------------------------------------------
$sandboxExe = Join-Path $env:windir 'System32\WindowsSandbox.exe'
if (-not (Test-Path -LiteralPath $sandboxExe)) {
    $state = $null
    try { $state = (Get-CimInstance Win32_OptionalFeature -Filter "Name='Containers-DisposableClientVM'").InstallState } catch {}
    $why = switch ($state) { 1 { 'is enabled but WindowsSandbox.exe is missing (a restart may be pending)' } 2 { 'is turned off' } $null { 'is not available on this Windows' } default { "is in state $state" } }
    Write-Host ''
    Write-Host "  Windows Sandbox $why." -ForegroundColor Red
    Write-Host '  To turn it on: Start, type "Turn Windows features on or off", tick "Windows Sandbox",' -ForegroundColor Yellow
    Write-Host '  OK, then restart Windows. (Admin PowerShell instead:' -ForegroundColor Yellow
    Write-Host '  Enable-WindowsOptionalFeature -FeatureName Containers-DisposableClientVM -All -Online)' -ForegroundColor Yellow
    Write-Host '  It needs Windows 10/11 Pro or better and virtualization on in the BIOS.' -ForegroundColor Yellow
    Write-Host ''
    if (-not $WriteOnly) { exit 1 }
    Write-Host '  -WriteOnly: writing the run folder anyway, not opening it.' -ForegroundColor Gray
}

# --- Build mode: which zip, and is it the one its version.json describes ---------------------
$zipInfo = $null
if ($Mode -eq 'Build') {
    if (-not $Zip) {
        $dist = Join-Path $pkgRoot 'dist'
        $Zip = @(Get-ChildItem -LiteralPath $dist -File -Filter 'ShatteredLegacy-*.zip' -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^ShatteredLegacy-\d{4}\.\d{2}\.\d{2}\.\d{4}\.zip$' } |
            Sort-Object Name -Descending | Select-Object -First 1 | ForEach-Object FullName)[0]
        if (-not $Zip) { throw "No ShatteredLegacy-<stamp>.zip in $dist. Build the package first." }
    }
    $Zip = (Resolve-Path -LiteralPath $Zip).Path
    $vj = $Zip -replace '\.zip$', '.version.json'
    if (-not (Test-Path -LiteralPath $vj)) { throw "No $vj beside the zip: cannot prove which build this is." }
    $j = [IO.File]::ReadAllText($vj) | ConvertFrom-Json
    $h = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash.ToLower()
    if ("$($j.sha256)" -cne $h) { throw "$Zip does not match its version.json (sha256 $h, version.json $($j.sha256)). Rebuild it." }
    if ("$($j.bytes)" -ne "$((Get-Item -LiteralPath $Zip).Length)") { throw "$Zip does not match its version.json (bytes)." }
    $zipInfo = [ordered]@{ name = (Split-Path $Zip -Leaf); folder = (Split-Path $Zip -Parent); sha256 = $h; version = "$($j.version)" }
    Write-Host "  zip      $Zip" -ForegroundColor Gray
    Write-Host "           version $($j.version), sha256 matches its version.json" -ForegroundColor Gray
}

# --- the run folder ---------------------------------------------------------------------------
$stamp   = Get-Date -Format 'yyyyMMdd-HHmmss'
$run     = Join-Path $ResultsRoot $stamp
$kit     = Join-Path $run 'kit'
$results = Join-Path $run 'results'
New-Item -ItemType Directory -Path $kit, $results -Force | Out-Null

# The kit, with CRLF line ends: the repo stores LF (.gitattributes) and cmd.exe misparses some
# LF-only batch files. This is gate 2's CRLF half for clean-test\ (Build-PlayerPackage.ps1
# checks the ASCII half where the files live). Start-CleanTest.ps1 itself stays on the host.
$bad = @()
foreach ($f in Get-ChildItem -LiteralPath $kitSrc -File | Where-Object { $_.Name -ne 'Start-CleanTest.ps1' }) {
    $b = [IO.File]::ReadAllBytes($f.FullName)
    if (@($b | Where-Object { $_ -gt 127 }).Count) { $bad += $f.Name; continue }
    $t = [Text.Encoding]::ASCII.GetString($b) -replace "`r`n", "`n" -replace "`n", "`r`n"
    [IO.File]::WriteAllText((Join-Path $kit $f.Name), $t, (New-Object Text.ASCIIEncoding))
}
if ($bad.Count) { throw "Non-ASCII bytes in $($bad -join ', '). Fix them; PowerShell 5.1 in the sandbox would misread them." }
foreach ($f in Get-ChildItem -LiteralPath $kit -File) {
    $t = [IO.File]::ReadAllText($f.FullName)
    if ($t -match "(?<!`r)`n") { throw "kit\$($f.Name) still has a bare LF after the CRLF copy." }
}
# The gate 6 allowlist, so baseline.txt can show each DLL is really in a fresh Windows.
Copy-Item -LiteralPath (Join-Path $pkgRoot 'native-import-allowlist.txt') -Destination (Join-Path $kit 'native-import-allowlist.txt')

$runInfo = [ordered]@{ mode = $Mode; url = $url; vgpu = [bool]$VGpu; started = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'); host = $env:COMPUTERNAME }
if ($zipInfo) { $runInfo.zip = $zipInfo.name; $runInfo.sha256 = $zipInfo.sha256; $runInfo.version = $zipInfo.version }
[IO.File]::WriteAllText((Join-Path $kit 'run.json'), ($runInfo | ConvertTo-Json), (New-Object Text.ASCIIEncoding))

# --- the .wsb ---------------------------------------------------------------------------------
function Esc([string]$s) { [Security.SecurityElement]::Escape($s) }
$maps = @(
    @{ Host = $kit;     Box = 'C:\SL\kit';     RO = 'true'  },
    @{ Host = $results; Box = 'C:\SL\results'; RO = 'false' }
)
if ($zipInfo) { $maps += @{ Host = $zipInfo.folder; Box = 'C:\SL\dist'; RO = 'true' } }
$mapXml = ($maps | ForEach-Object {
@"
    <MappedFolder>
      <HostFolder>$(Esc $_.Host)</HostFolder>
      <SandboxFolder>$(Esc $_.Box)</SandboxFolder>
      <ReadOnly>$($_.RO)</ReadOnly>
    </MappedFolder>
"@
}) -join "`r`n"
$wsb = @"
<Configuration>
  <vGPU>$(if ($VGpu) { 'Enable' } else { 'Disable' })</vGPU>
  <Networking>Enable</Networking>
  <MemoryInMB>8192</MemoryInMB>
  <MappedFolders>
$mapXml
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\SL\kit\Inside-Start.ps1</Command>
  </LogonCommand>
</Configuration>
"@
$wsbPath = Join-Path $run 'clean-test.wsb'
[IO.File]::WriteAllText($wsbPath, ($wsb -replace "`r?`n", "`r`n"), (New-Object Text.ASCIIEncoding))

Write-Host ''
Write-Host "  run folder  $run" -ForegroundColor Cyan
Write-Host "  mode        $Mode$(if ($VGpu) { ', host GPU (vGPU on)' } else { ', no GPU (vGPU off)' })" -ForegroundColor Gray
Write-Host "  results     $results" -ForegroundColor Gray
Write-Host ''
if ($WriteOnly) { Write-Host "  -WriteOnly: not opening $wsbPath" -ForegroundColor Gray; exit 0 }
Write-Host '  Opening Windows Sandbox. When its desktop shows "READ ME FIRST.txt", follow it.' -ForegroundColor Green
Write-Host '  Closing the sandbox throws everything in it away; close it only after evidence.txt' -ForegroundColor Yellow
Write-Host "  is in $results." -ForegroundColor Yellow
Start-Process -FilePath $wsbPath
