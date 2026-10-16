#Requires -Version 5.1
<#
  cc-P132: before-and-after screenshots of the typed-confirmation dialog, for the notes.

      powershell -NoProfile -ExecutionPolicy Bypass -File design\console-enhanced\Take-ConfirmScreenshots.ps1

  "After" is the console's own New-TypedConfirmForm in each look. "Before" is the dialog as cc-P84 to cc-P131 drew it,
  rebuilt here from that code (a bold mono Firebrick label on a transparent panel over the window's stone, which the
  old theme walker recolored to PageWarn, 112,16,12): the old script has no builder that returns the form unshown.
  Both are drawn with DrawToBitmap (nothing is shown on screen). The window shots come from the smoke build.
  ASCII only.
#>
param([string]$RepoRoot)
$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
$shotRepo = $RepoRoot
$shotHere = $PSScriptRoot
. (Join-Path $shotRepo 'scripts\Shard-Console.ps1') -LoadOnly
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$script:cfg = Get-ConsoleConfig -RepoRoot $shotRepo
$plan = @((New-PlanStep -Kind confirm -Phrase 'publish player package' -Text 'This publishes the zip.'), (New-PlanStep -Kind window -Exe 'powershell.exe' -Arguments @('-File', 'Publish-PlayerPackage.ps1') -Text 'Publish-PlayerPackage.ps1 runs in its own window.'))
$text = 'This publishes ShatteredLegacy-2026.10.15.1354.zip (version 2026.10.15.1354) to get.shatteredlegacyuo.com and the website, and its version.json tells every installed launcher to offer it.'

function Save-Form($form, $path) {
    $form.StartPosition = 'Manual'; $form.Location = New-Object Drawing.Point(-8000, 0); $form.ShowInTaskbar = $false
    $form.Show(); [Windows.Forms.Application]::DoEvents()
    $bmp = New-Object Drawing.Bitmap($form.Width, $form.Height)
    $form.DrawToBitmap($bmp, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $form.Close(); $form.Dispose()
    Write-Host ('wrote ' + $path)
}

foreach ($look in 'UO', 'Enhanced', 'Plain') {
    $script:theme = Get-ConsoleTheme $look
    $script:themeImages = Get-ThemeImages $script:cfg $script:theme
    $script:MonoFont = Get-ThemeFont 'Mono'
    $script:BoldFont = Get-ThemeFont 'Mono' ([Drawing.FontStyle]::Bold)
    $c = New-TypedConfirmForm -Phrase 'publish player package' -Text $text -Plan $plan -What 'Player package'
    Save-Form $c.Form (Join-Path $shotHere ('confirm-after-' + $look.ToLower() + '.png'))
}

# Before (UO only: Enhanced did not exist, Plain was and is a dark red on light grey).
$script:theme = Get-ConsoleTheme 'UO'
$script:themeImages = Get-ThemeImages $script:cfg $script:theme
$script:MonoFont = Get-ThemeFont 'Mono'
$script:BoldFont = Get-ThemeFont 'Mono' ([Drawing.FontStyle]::Bold)
$d = New-Object Windows.Forms.Form
$d.Text = 'Player package: type to confirm'
$d.Size = New-Object Drawing.Size(640, 420); $d.FormBorderStyle = 'FixedDialog'; $d.MaximizeBox = $false; $d.MinimizeBox = $false
$body = New-Object Windows.Forms.TextBox
$body.Multiline = $true; $body.ReadOnly = $true; $body.ScrollBars = 'Vertical'; $body.Font = $script:MonoFont; $body.Dock = 'Fill'
$body.Text = $text + "`r`n`r`nThis will run:`r`n" + ((Format-Plan -Action '' -ShardKey 'live' -Plan $plan -Header 'PLAN' | Select-Object -Skip 1) -join "`r`n")
$bottom = New-Object Windows.Forms.Panel; $bottom.Dock = 'Bottom'; $bottom.Height = 76
$label = New-Object Windows.Forms.Label; $label.Text = 'Type exactly:  publish player package'; $label.Font = $script:BoldFont; $label.ForeColor = [Drawing.Color]::Firebrick; $label.SetBounds(10, 6, 600, 20)
$tb = New-Object Windows.Forms.TextBox; $tb.SetBounds(10, 30, 360, 24)
$ok = New-Object Windows.Forms.Button; $ok.Text = 'Do it'; $ok.Enabled = $false; $ok.SetBounds(390, 28, 100, 28)
$cancel = New-Object Windows.Forms.Button; $cancel.Text = 'Cancel'; $cancel.SetBounds(500, 28, 100, 28)
$bottom.Controls.AddRange(@($label, $tb, $ok, $cancel))
$d.Controls.Add($body); $d.Controls.Add($bottom)
# The old walker: Firebrick became PageWarn wherever it sat; the panel went transparent over the stone.
Set-ControlTheme $d $script:theme $script:themeImages
$label.ForeColor = ConvertTo-DrawingColor $script:theme.Chrome.PageWarn
$bottom.BackColor = [Drawing.Color]::Transparent
Save-Form $d (Join-Path $shotHere 'confirm-before-uo.png')

# The numbers, for the notes.
$t = Get-ConsoleTheme 'UO'
$under = Get-BlendedColor $t.Chrome.Backing $t.Chrome.BackingAlpha $t.Gumps.StoneLight
Write-Host ('before: PageWarn 112,16,12 on the stone, lightest art pixel: ' + [math]::Round((Get-ContrastRatio $t.Chrome.PageWarn $t.Gumps.StoneLight), 2) + ':1; on the backing over it: ' + [math]::Round((Get-ContrastRatio $t.Chrome.PageWarn $under), 2) + ':1')
Write-Host ('after (UO): plate text on plate: ' + [math]::Round((Get-ContrastRatio $t.Plate.Text $t.Plate.Back), 2) + ':1')
$e = Get-ConsoleTheme 'Enhanced'
Write-Host ('after (Enhanced): plate text on plate: ' + [math]::Round((Get-ContrastRatio $e.Plate.Text $e.Plate.Back), 2) + ':1')
