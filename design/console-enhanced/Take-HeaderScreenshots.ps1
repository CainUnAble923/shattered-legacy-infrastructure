#Requires -Version 5.1
<#
  cc-P153: the header above the tiles, before and after the console's own icon took the painted crest's place.

      powershell -NoProfile -ExecutionPolicy Bypass -File design\console-enhanced\Take-HeaderScreenshots.ps1 -Name after
      ... -Script <a copy of the old Shard-Console.ps1> -Name before

  Builds the form (New-ConsoleForm, dry run on the 2026-09-29 capture, so nothing is read from Docker) in the UO and
  Enhanced looks, draws it with DrawToBitmap off screen and keeps the header band: header-<name>-<look>.png here.
  ASCII only.
#>
param([string]$Script, [string]$Name = 'after', [string]$RepoRoot)
$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
if (-not $Script) { $Script = Join-Path $RepoRoot 'scripts\Shard-Console.ps1' }
$shotHere = $PSScriptRoot
$shotRepo = $RepoRoot
$shotName = $Name
. $Script -LoadOnly
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$cfg0 = Get-ConsoleConfig -RepoRoot $shotRepo
$cap = Join-Path $shotRepo 'scripts\tests\fixtures\console-capture-2026-09-29.txt'
foreach ($look in 'UO', 'Enhanced') {
    $form = New-ConsoleForm -Config $cfg0 -DryRun -StatusFrom $cap -Theme $look
    $form.StartPosition = 'Manual'; $form.Location = New-Object Drawing.Point(-8000, 0); $form.ShowInTaskbar = $false
    $form.Size = New-Object Drawing.Size(1100, 760)
    $form.Show(); [Windows.Forms.Application]::DoEvents()
    $bmp = New-Object Drawing.Bitmap($form.Width, $form.Height)
    $form.DrawToBitmap($bmp, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
    $h = $(if ($script:ui.Header) { $script:ui.Header.Bottom + 40 } else { 120 })
    $crop = $bmp.Clone((New-Object Drawing.Rectangle(0, 0, [Math]::Min(560, $bmp.Width), [Math]::Min($h, $bmp.Height))), $bmp.PixelFormat)
    $out = Join-Path $shotHere ('header-' + $shotName + '-' + $look.ToLower() + '.png')
    $crop.Save($out, [Drawing.Imaging.ImageFormat]::Png); $crop.Dispose(); $bmp.Dispose()
    $form.Close(); $form.Dispose()
    Write-Host ('wrote ' + $out)
}
