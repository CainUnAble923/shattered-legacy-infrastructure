#Requires -Version 5.1
# Facts for the optional UI skins (cc-P362): vendor\shattered-legacy-art\skins.json, its rows in registry.csv, its PNGs,
# and the client's copy of the table (client\patches\0005-ui-skins-appearance.patch, SlSkins.cs).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Ui-Skins.Tests.ps1 [-UoData <EA client folder>]
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Paperdoll-Bodies.Tests.ps1. The last fact, that
# no registered ID is a stock gump or art ID, reads the EA client's files through scripts\find-free-art.py
# --check-registry; without a client folder (-UoData, else $env:SL_UO_DATA, else D:\UO\UltimaOnlineVanilla) it says SKIP
# and passes, and the other facts still hold the skins to new IDs only.

param([string]$UoData)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$pkg = Split-Path $here -Parent
$repo = Split-Path $pkg -Parent
$art = Join-Path $pkg 'vendor\shattered-legacy-art'
$skinsPath = Join-Path $art 'skins.json'
$patchPath = Join-Path $repo 'client\patches\0005-ui-skins-appearance.patch'

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

function Read-Skins { [IO.File]::ReadAllText($skinsPath) | ConvertFrom-Json }
function Read-Registry { @(Get-Content -LiteralPath (Join-Path $art 'registry.csv') | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv) }
function Skin-Kind($skins, [string]$kind) { @($skins.kinds | Where-Object { $_.key -eq $kind })[0] }
# Every (kind, key, stock, ours) the table names.
function Skin-Pairs($skins) {
    foreach ($s in $skins.skins) {
        foreach ($p in $s.map.PSObject.Properties) {
            [pscustomobject]@{ Kind = $s.kind; Key = $s.key; Stock = [int]$p.Name; Ours = [int]$p.Value }
        }
    }
}

Fact 'EverySkinDrawsFromANewRegisteredIdWithItsPngAndNeverFromAStockOne' {
    $skins = Read-Skins
    $reg = Read-Registry
    $pairs = @(Skin-Pairs $skins)
    Assert-Equal 19 @($skins.skins).Count 'skins: 8 backpacks, 3 bags, 5 chest linings, 2 crates, the scrolls'
    Assert-Equal 'backpack,bag,chest,crate,scroll' (@($skins.kinds | ForEach-Object { $_.key }) -join ',') 'kinds'
    $stock = @($pairs | ForEach-Object { $_.Stock } | Sort-Object -Unique)
    foreach ($p in $pairs) {
        $space = (Skin-Kind $skins $p.Kind).space
        Assert-True ($stock -notcontains $p.Ours) "$($p.Kind)/$($p.Key): $($p.Ours) is a stock ID another skin draws over"
        $row = @($reg | Where-Object { [int]$_.id -eq $p.Ours -and $_.kind -eq $space })
        Assert-Equal 1 $row.Count "$($p.Kind)/$($p.Key): $space $($p.Ours) registered once"
        Assert-True ($row[0].what -like 'UI skin (cc-P362)*') "$($p.Ours) is a cc-P362 row: $($row[0].what)"
        $folder = if ($space -eq 'art') { 'art' } else { 'gumps' }
        Assert-True (Test-Path -LiteralPath (Join-Path $art "$folder\$($p.Ours).png")) "$folder\$($p.Ours).png"
        $reserved = @($reg | Where-Object { [int]$_.id -eq $p.Stock -and $_.kind -eq $space })
        Assert-Equal 0 $reserved.Count "stock $space $($p.Stock) is not one of our registry IDs"
    }
    # Every cc-P362 row is drawn by a skin (no stray art), and the ranges are as registered.
    $mine = @($reg | Where-Object { $_.what -like 'UI skin (cc-P362)*' })
    Assert-Equal 93 $mine.Count 'cc-P362 rows: 29 gumps, 64 art'
    $used = @($pairs | ForEach-Object { $_.Ours } | Sort-Object -Unique)
    foreach ($r in $mine) { Assert-True ($used -contains [int]$r.id) "row $($r.id) is drawn by no skin" }
    $gumps = @($mine | Where-Object { $_.kind -eq 'gump' } | ForEach-Object { [int]$_.id } | Sort-Object)
    $arts = @($mine | Where-Object { $_.kind -eq 'art' } | ForEach-Object { [int]$_.id } | Sort-Object)
    Assert-Equal '23052-23080' "$($gumps[0])-$($gumps[-1])" 'gump range'
    Assert-Equal 29 $gumps.Count 'gump rows'
    Assert-Equal '13049-13112' "$($arts[0])-$($arts[-1])" 'art range'
    Assert-Equal 64 $arts.Count 'art rows'
    # The scroll skin covers exactly the 64 Magery scrolls, in stock order.
    $scroll = @($pairs | Where-Object { $_.Kind -eq 'scroll' } | Sort-Object Stock)
    Assert-Equal 64 $scroll.Count 'scrolls'
    for ($i = 0; $i -lt 64; $i++) {
        Assert-Equal (0x1F2D + $i) $scroll[$i].Stock "stock scroll $i"
        Assert-Equal (13049 + $i) $scroll[$i].Ours "our scroll $i"
    }
}

Fact 'TheCreditsAndTermsAreOnEveryRow' {
    $reg = Read-Registry
    $byAuthor = @{ 'Carl Hamilton' = 0; 'Safera' = 0; 'Castiel' = 0; 'Erachiro' = 0; 'CorvaeOboro' = 0 }
    foreach ($r in @($reg | Where-Object { $_.what -like 'UI skin (cc-P362)*' })) {
        $who = ($r.source -split ':')[0]
        Assert-True ($byAuthor.ContainsKey($who)) "row $($r.id) names its author: $($r.source)"
        $byAuthor[$who]++
        Assert-True ($r.license -like "*$who*") "row $($r.id): the terms name $who"
        Assert-True ($r.license -like '*stock*') "row $($r.id): the terms say stock IDs keep stock art"
        if ($r.source -match 'backpack-sets\\(Celtic|Dragon|Fancy)\\') { Assert-True ($r.license -like '*EA-derived*never as an EA file*') "row $($r.id) is EA-derived: $($r.license)" }
    }
    Assert-Equal 'CarlHamilton=8 Castiel=3 CorvaeOboro=64 Erachiro=7 Safera=11' ((@($byAuthor.Keys | Sort-Object) | ForEach-Object { ($_ -replace ' ', '') + '=' + $byAuthor[$_] }) -join ' ') 'rows per author'
    $source = [IO.File]::ReadAllText((Join-Path $art 'SOURCE.txt'))
    foreach ($who in $byAuthor.Keys) { Assert-True ($source -match [regex]::Escape($who)) "SOURCE.txt credits $who" }
    Assert-True ($source -match 'The UI skins: gumps 23052-23080 and art 13049-13112') 'SOURCE.txt has the cc-P362 section'
}

Fact 'TheClientsTableIsSkinsJson' {
    Assert-True (Test-Path -LiteralPath $patchPath) 'client\patches\0005-ui-skins-appearance.patch'
    $patch = [IO.File]::ReadAllText($patchPath)
    $rows = @([regex]::Matches($patch, '\+\s+new\("([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)", new Dictionary<uint, uint> \{ ([^}]*) \}\),') | ForEach-Object {
        $map = @([regex]::Matches($_.Groups[5].Value, '\[(\d+)\] = (\d+)') | ForEach-Object { "$($_.Groups[1].Value)=$($_.Groups[2].Value)" }) -join ','
        "$($_.Groups[1].Value)|$($_.Groups[2].Value)|$($_.Groups[3].Value)|$($_.Groups[4].Value)|$map"
    })
    $skins = Read-Skins
    $json = @($skins.skins | ForEach-Object {
        $map = @($_.map.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ','
        "$($_.kind)|$($_.key)|$($_.label)|$($_.author)|$map"
    })
    Assert-Equal $json.Count $rows.Count 'skins in SlSkins.cs'
    for ($i = 0; $i -lt $json.Count; $i++) { Assert-Equal $json[$i] $rows[$i] "skin $i" }
    Assert-True ($patch -match 'Shattered Legacy patch \(cc-P362\)') 'the patch files carry our header'
    Assert-True ($patch -match 'public const string ClassicLabel = "Classic \(stock\)";') 'Classic (stock) is the first choice'
}

Fact 'NoRegisteredIdIsAStockGumpOrArtId' {
    $data = if ($UoData) { $UoData } elseif ($env:SL_UO_DATA) { $env:SL_UO_DATA } else { 'D:\UO\UltimaOnlineVanilla' }
    $py = Get-Command python -ErrorAction SilentlyContinue
    if (-not (Test-Path -LiteralPath (Join-Path $data 'gumpartLegacyMUL.uop')) -or -not $py) {
        Write-Host "        SKIP: no EA client folder at $data (or no python); the other facts still hold" -ForegroundColor Yellow
        return
    }
    $tool = Join-Path $repo 'scripts\find-free-art.py'
    $ErrorActionPreference = 'Continue'   # the tool's progress lines go to stderr
    $out = & $py.Source -P $tool --check-registry --data $data 2>$null | Out-String
    $n = @(Read-Registry).Count
    Assert-True ($out -match "0 of $n registered IDs are no longer free") "find-free-art --check-registry: $($out.Trim().Split("`n")[-1])"
}

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
