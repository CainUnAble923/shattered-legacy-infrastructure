#Requires -Version 5.1
# Facts for the opt-in redrawn world art (cc-P670): vendor\zigholtul-redrawn-statics (Zigholtul's PNGs that fit, and
# manifest.csv), the package build that stages them, and the client patch that draws them only when ticked
# (client\patches\0008-redrawn-world-art.patch).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Redrawn-Art.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Ui-Skins.Tests.ps1.

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$pkg = Split-Path $here -Parent
$repo = Split-Path $pkg -Parent
$rd = Join-Path $pkg 'vendor\zigholtul-redrawn-statics'
$patchPath = Join-Path $repo 'client\patches\0008-redrawn-world-art.patch'
$buildPath = Join-Path $pkg 'Build-PlayerPackage.ps1'

$results = New-Object 'System.Collections.Generic.List[object]'
function Fact {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true })
        Write-Host "  PASS  $Name" -ForegroundColor Green
    } catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false })
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
    }
}
function Assert-True([bool]$c, [string]$m) { if (-not $c) { throw $m } }
function Assert-Equal($want, $got, [string]$m) { if ("$want" -ne "$got") { throw "$m`: expected '$want', got '$got'" } }
function Read-Manifest { @(Get-Content -LiteralPath (Join-Path $rd 'manifest.csv') | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv) }

Write-Host 'Redrawn world art (cc-P670)' -ForegroundColor Cyan

Fact 'TheManifestListsEveryFileOfTheDownloadOnceWithAReason' {
    $m = Read-Manifest
    Assert-Equal 1019 $m.Count 'files in the download (SOURCE.txt: 1,019 PNGs)'
    Assert-Equal $m.Count @($m | ForEach-Object file | Sort-Object -Unique).Count 'each file once'
    foreach ($r in $m) { Assert-True ([bool]$r.reason) ("no reason for " + $r.file) }
    Assert-True (@($m | Where-Object { $_.verdict -eq 'ok' -and $_.file -notmatch '^\d+\.png$' }).Count -eq 0) 'only plain decimal names ship'
}

Fact 'TheVendoredFolderIsExactlyTheManifestsShippedFilesByteForByte' {
    $ship = @{}
    foreach ($r in (Read-Manifest)) { if ($r.verdict -eq 'ok') { $ship[$r.file] = $r.sha256 } }
    $files = @(Get-ChildItem -LiteralPath (Join-Path $rd 'art') -File)
    Assert-Equal $ship.Count $files.Count 'PNGs in art\ against shipped rows'
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        foreach ($f in $files) {
            Assert-True ($ship.ContainsKey($f.Name)) ($f.Name + ' is not a shipped row')
            $h = -join ($sha.ComputeHash([IO.File]::ReadAllBytes($f.FullName)) | ForEach-Object { $_.ToString('x2') })
            Assert-Equal $ship[$f.Name] $h ($f.Name + ' SHA-256')
        }
    } finally { $sha.Dispose() }
    Assert-True (@($files | Where-Object { $_.Length -gt 5MB }).Count -eq 0) 'no file over 5 MB'
}

Fact 'NoShippedIdIsOneOfOurOwnArtOrAUiSkinsId' {
    $reg = @(Get-Content -LiteralPath (Join-Path $pkg 'vendor\shattered-legacy-art\registry.csv') | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv | ForEach-Object { [int]$_.id })
    $skins = [IO.File]::ReadAllText((Join-Path $pkg 'vendor\shattered-legacy-art\skins.json')) | ConvertFrom-Json
    $skinIds = New-Object 'System.Collections.Generic.HashSet[int]'
    # Only the art-space skins (the scrolls) share item IDs with statics; gump skins are another ID space.
    $artKinds = @($skins.kinds | Where-Object { $_.space -eq 'art' } | ForEach-Object key)
    foreach ($s in @($skins.skins | Where-Object { $artKinds -contains $_.kind })) { foreach ($p in $s.map.PSObject.Properties) { [void]$skinIds.Add([int]$p.Name); [void]$skinIds.Add([int]$p.Value) } }
    Assert-True ($skinIds.Count -gt 0) 'read the art skins (none found: skins.json changed shape?)'
    foreach ($r in (Read-Manifest)) {
        if ($r.verdict -ne 'ok') { continue }
        $id = [int]$r.itemId
        Assert-True ($reg -notcontains $id) ("$id is one of our registered art IDs")
        Assert-True (-not $skinIds.Contains($id)) ("$id is a UI skin's ID")
    }
}

Fact 'TheClientDrawsThemOnlyFromItsOwnFolderAndOnlyWhenTicked' {
    $p = [IO.File]::ReadAllText($patchPath)
    Assert-True ($p -match 'public const string FolderName = "sl-redrawn-art";') 'the client folder is sl-redrawn-art'
    Assert-True ($p -match 'public bool SlRedrawnArt \{ get; set => SetProperty\(ref field, value\); \} //') 'a profile bool with no initializer: off by default'
    Assert-True ($p -match 'Redrawn world art \(Zigholtul\)') 'the Appearance label'
    Assert-True ($p -match 'SearchOption\.TopDirectoryOnly') 'its own folder only'
}

Fact 'ThePackageStagesThemThereAndGate4HoldsTheZipToTheManifest' {
    $b = [IO.File]::ReadAllText($buildPath)
    Assert-True ($b -match "vendor\\zigholtul-redrawn-statics") 'the build reads the vendor folder'
    Assert-True ($b -match "ExternalImages\\sl-redrawn-art") 'it stages into app\tazuo\ExternalImages\sl-redrawn-art'
    Assert-True ($b -match "app/tazuo/ExternalImages/sl-redrawn-art/") 'gate 4 checks that folder in the zip'
    Assert-True ($b -match 'redrawn') 'gate 4 compares SHA-256 against manifest.csv'
}

Fact 'TheReadmeCreditsZigholtulForIt' {
    $t = [IO.File]::ReadAllText((Join-Path $pkg 'payload\README.txt'))
    Assert-True ($t -match 'Redrawn' -and $t -match "Zigholtul's UO Art Static Replacements for TazUO") 'README credit'
}

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed)
exit $failed
