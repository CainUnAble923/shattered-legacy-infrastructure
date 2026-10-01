#Requires -Version 5.1
<#
  Publish-PatchNotes.ps1 - puts the shard's patch notes on the wiki page uo:patch_notes (F-15, cc-P22).

      .\scripts\Publish-PatchNotes.ps1                 dry run: shows the page and whether the wiki differs
      .\scripts\Publish-PatchNotes.ps1 -Publish        writes the page on the wiki
      .\scripts\Publish-PatchNotes.ps1 -Container sl-modernuo -Publish     from the live shard instead

  Nothing here parses CHANGELOG.md. The server does that at start (server/customizations/ShardVersion.cs) and writes
  the rendered DokuWiki page inside its container, at /modernuo/Data/ShatteredLegacy/patch_notes.txt. So the page
  this publishes is exactly the notes of the build the shard is running: deploy the build first, then publish.

  Where it goes: the sl-wiki container on Haven, /storage/data/pages/uo/patch_notes.txt, owned like uo/start.txt.
  DokuWiki renders a page file whose time is newer than its cache, so no restart is needed. The page has no edit
  history entry for this write (it is generated; edits on the wiki are overwritten at the next publish).
#>
param(
    [string]$Container = 'sl-modernuo-test',
    [string]$Haven = 'chase@192.168.1.61',
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$Source = '/modernuo/Data/ShatteredLegacy/patch_notes.txt'
$Page = '/storage/data/pages/uo/patch_notes.txt'
$Url = 'https://wiki.shatteredlegacyuo.com/uo:patch_notes'
$Tmp = Join-Path $env:TEMP 'sl-patch_notes.txt'

function Stop-IfFailed($what) {
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $what" -ForegroundColor Red; exit 1 }
}

$version = (docker exec $Container cat /modernuo/Data/ShatteredLegacy/VERSION) -join ''
Stop-IfFailed "reading VERSION from $Container (is it running this build?)"

$text = (docker exec $Container cat $Source) -join "`n"
Stop-IfFailed "reading $Source from $Container"
if (-not $text.Trim()) { Write-Host "The page from $Container is empty; nothing to publish." -ForegroundColor Yellow; exit 1 }
[System.IO.File]::WriteAllText($Tmp, $text + "`n", (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Version in $Container : $(if ($version.Trim()) { $version.Trim() } else { '(none)' })"
Write-Host "---- page ($((Get-Item $Tmp).Length) bytes) ----"
Get-Content $Tmp
Write-Host "---- end ----"

$current = (ssh $Haven "docker exec sl-wiki sh -c 'cat $Page 2>/dev/null'") -join "`n"
if ($current.Trim() -eq $text.Trim()) {
    Write-Host "The wiki already has this page. Nothing to do." -ForegroundColor Green
    exit 0
}
Write-Host ($(if ($current.Trim()) { 'The wiki page differs from this.' } else { 'The wiki has no patch notes page yet.' }))

if (-not $Publish) {
    Write-Host "Dry run. Run again with -Publish to write it to $Url" -ForegroundColor Yellow
    exit 0
}

scp $Tmp "${Haven}:sl-patch_notes.txt"
Stop-IfFailed 'copy to Haven'
ssh $Haven "docker exec -i sl-wiki sh -c 'mkdir -p /storage/data/pages/uo && cat > $Page && chown --reference=/storage/data/pages/uo/start.txt $Page' < ~/sl-patch_notes.txt && rm ~/sl-patch_notes.txt"
Stop-IfFailed 'write the page in sl-wiki'
Write-Host "Published: $Url" -ForegroundColor Green
