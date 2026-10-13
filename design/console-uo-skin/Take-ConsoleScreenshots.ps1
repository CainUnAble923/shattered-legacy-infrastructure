#Requires -Version 5.1
<#
  cc-P84: screenshots of the Shard Console in both looks, for the notes and for Chase.

      powershell -NoProfile -ExecutionPolicy Bypass -File design\console-uo-skin\Take-ConsoleScreenshots.ps1

  The window is built from the console's own capture fixture (-DryRun), so nothing reads or touches docker. The one
  button pressed, Rebuild through the gates, runs as a dry child (it prints the plan and runs nothing); the streaming
  demo is a PowerShell loop that prints 40 lines.
  Where the screenshots go:
    design\console-uo-skin\           Plain, and UO drawn without client art (our own colors and crest only).
    scripts\shard-console-cache\screenshots\   UO with the client's gump art. EA's art: git-ignored, never committed.
  ASCII only.
#>
param([string]$RepoRoot)
$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
# Kept under other names: dot-sourcing the console binds its own $RepoRoot parameter in this scope.
$shotRepo = $RepoRoot
$shotHere = $PSScriptRoot
$console = Join-Path $shotRepo 'scripts\Shard-Console.ps1'
. $console -LoadOnly
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$committed = $shotHere
$cache = Join-Path $shotRepo 'scripts\shard-console-cache\screenshots'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$capture = Join-Path $shotRepo 'scripts\tests\fixtures\console-capture-2026-09-29.txt'
$script:NetProbeDisabled = $true
$script:SelfPath = $console

function Save-Shot {
    # From the screen: DrawToBitmap leaves a RichTextBox blank, and the status and output are RichTextBoxes.
    param($Control, [string]$Path)
    $Control.Activate()
    $Control.Refresh()
    foreach ($i in 1..5) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 40 }
    Save-Screen $Control.Bounds $Path
}

function Save-Screen {
    # From the screen itself (a tooltip is a window of its own, which DrawToBitmap does not see).
    param([Drawing.Rectangle]$Rect, [string]$Path)
    [Windows.Forms.Application]::DoEvents()
    $bmp = New-Object Drawing.Bitmap($Rect.Width, $Rect.Height)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($Rect.Location, [Drawing.Point]::Empty, $Rect.Size)
    $g.Dispose()
    $bmp.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ('wrote ' + $Path)
}

$looks = @(
    @{ Name = 'plain'; Theme = 'Plain'; Art = $false; Dir = $committed },
    @{ Name = 'uo-drawn'; Theme = 'UO'; Art = $false; Dir = $committed },
    @{ Name = 'uo-art'; Theme = 'UO'; Art = $true; Dir = $cache }
)
foreach ($look in $looks) {
    $cfg = Get-ConsoleConfig -RepoRoot $shotRepo
    $cfg.SettingsFile = Join-Path $env:TEMP 'sl-console-shots-settings.json'
    if (-not $look.Art) { $cfg.GumpFile = Join-Path $env:TEMP 'no-client.uop'; $cfg.CacheFolder = Join-Path $env:TEMP 'sl-console-shots-nocache' }
    $settings = ConvertFrom-ConsoleSettings $null
    $settings.Theme = $look.Theme
    $form = New-ConsoleForm -Config $cfg -DryRun -StatusFrom $capture -Theme $look.Theme -Pane -Settings $settings
    $form.StartPosition = 'Manual'
    $form.Location = New-Object Drawing.Point(20, 20)
    $form.Show()
    $form.Activate()
    [Windows.Forms.Application]::DoEvents()
    $d = $look.Dir
    $n = $look.Name

    for ($i = 0; $i -lt $script:ui.Tabs.TabPages.Count; $i++) {
        $script:ui.Tabs.SelectedIndex = $i
        $tab = ($script:ui.Tabs.TabPages[$i].Text -replace '[^A-Za-z]', '-').ToLower()
        Save-Shot $form (Join-Path $d ($n + '-tab-' + $i + '-' + $tab + '.png'))
    }
    $script:ui.Tabs.SelectedIndex = 0

    # Each tile state, on the six tiles at once: green, amber, red, grey, then green and red again.
    $states = @('green', 'amber', 'red', 'grey', 'green', 'red')
    $all = @($script:ui.Tiles) + @($script:ui.NetTile, $script:ui.AutopilotTile, $script:ui.PlayTestTile)
    for ($k = 0; $k -lt $all.Count; $k++) {
        $title = ($all[$k].Word.Text -split ':')[0]
        Set-TileLook $all[$k] ([pscustomobject]@{ Color = $states[$k]; Title = $title; Word = $states[$k].ToUpper(); Detail = ('This tile drawn ' + $states[$k] + ' for the screenshot.') })
    }
    Save-Shot $form (Join-Path $d ($n + '-tile-states.png'))

    # A tooltip, from the screen.
    $btn = @($script:ui.Tabs.TabPages[0].Controls[0].Controls | Where-Object { $_.Tag -eq 'test.rebuild' })[0]
    $script:ui.ToolTip.Show($script:ui.ToolTip.GetToolTip($btn), $btn, 10, $btn.Height + 4, 8000)
    foreach ($i in 1..30) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
    $p = $btn.PointToScreen([Drawing.Point]::Empty)
    Save-Screen (New-Object Drawing.Rectangle(($p.X - 20), ($p.Y - 20), 760, 140)) (Join-Path $d ($n + '-tooltip.png'))
    $script:ui.ToolTip.Hide($btn)

    # The Action output pane streaming: Rebuild through the gates, clicked under DRY RUN (the console is locked dry:
    # its child prints the plan and runs nothing), then a real read-only job that streams for a few seconds.
    Invoke-ConsoleButton -ActionKey 'test.rebuild'
    $deadline = (Get-Date).AddSeconds(60)
    while ($script:paneJob -and (Get-Date) -lt $deadline) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
    Save-Shot $form (Join-Path $d ($n + '-pane-rebuild-dry.png'))
    Start-PaneJob -Exe 'powershell.exe' -Arguments '-NoProfile -NonInteractive -Command "1..40 | ForEach-Object { Write-Output (''docker build step '' + $_ + '' of 40''); Start-Sleep -Milliseconds 120 }"' -Title 'streaming demo (40 lines, 120 ms apart)'
    $until = (Get-Date).AddSeconds(2.5)
    while ((Get-Date) -lt $until) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 30 }
    Save-Shot $form (Join-Path $d ($n + '-pane-streaming.png'))
    if ($script:paneJob) { Stop-PaneJob }
    $deadline = (Get-Date).AddSeconds(10)
    while ($script:paneJob -and (Get-Date) -lt $deadline) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
    Save-Shot $form (Join-Path $d ($n + '-pane-stopped.png'))

    $form.Close()
    $form.Dispose()
    $script:OutputBox = $null
}
