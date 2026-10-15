#Requires -Version 5.1
# Facts for which TazUO the player package ships (cc-P124): player-package\PackageClient.ps1 and its use in
# Build-PlayerPackage.ps1.
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Package-Client.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Cliloc-Overrides.Tests.ps1.
#
# The refusals run the real Build-PlayerPackage.ps1 in a child Windows PowerShell 5.1 against made-up client folders
# (a TazUO.exe and an sl-build.txt), so they stop before anything is staged. The add-on fact runs one REAL build, from a
# copy of our client build (D:\UO\tazuo-sl-build\client) with the local NSFW add-on planted inside it, into a folder
# under %TEMP%; it needs that build and takes a minute or two. Nothing is written under the repo.

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$pkg     = Split-Path $here -Parent
$repo    = Split-Path $pkg -Parent
$builder = Join-Path $pkg 'Build-PlayerPackage.ps1'
$helper  = Join-Path $pkg 'PackageClient.ps1'
$ourBuild = 'D:\UO\tazuo-sl-build\client'
$work    = Join-Path $env:TEMP ("sl-package-client-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null
. $helper

$results = New-Object 'System.Collections.Generic.List[object]'

function Fact {
    param([string]$Name, [scriptblock]$Body)
    if ($env:SL_FACT_FILTER -and $Name -notmatch $env:SL_FACT_FILTER) { return }
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Message = '' })
        Write-Host "  PASS  $Name" -ForegroundColor Green
    } catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Message = $_.Exception.Message })
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Because = '')
    if ($Expected -ne $Actual) { throw "expected [$Expected] but got [$Actual]. $Because" }
}

function Assert-True {
    param($Condition, [string]$Because)
    if (-not $Condition) { throw "not true: $Because" }
}

# The repo's patches as sl-build.txt lines, the way client\Build-Client.ps1 writes them.
$repoPatches = Get-RepoClientPatches $repo
function New-SlBuild([string[]]$PatchLines) {
    @(('TazUO upstream ' + $repoPatches.Upstream), 'built 2026-10-15 09:00') + $PatchLines
}
$currentLines = @($repoPatches.Patches | ForEach-Object { 'patch ' + $_.Name + ' sha256 ' + $_.Sha256 })

# A client folder holding only a fake TazUO.exe and, when given, an sl-build.txt.
function New-FakeClient([string]$Name, [string[]]$SlBuild) {
    $d = Join-Path $work $Name
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $d 'TazUO.exe'), 'not a real exe')
    if ($SlBuild) { [IO.File]::WriteAllLines((Join-Path $d 'sl-build.txt'), $SlBuild) }
    $d
}

# Runs the real builder in a child Windows PowerShell 5.1; returns its exit code, its output and the zips it left.
function Invoke-Builder([string[]]$Arguments) {
    $out = Join-Path $work ('out-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
    $args2 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $builder, '-Out', $out) + $Arguments
    # Continue, not Stop: under Stop, Windows PowerShell 5.1 turns the child's first stderr line into a terminating error.
    $ErrorActionPreference = 'Continue'
    $text = & powershell.exe @args2 2>&1 | Out-String
    [pscustomobject]@{ Code = $LASTEXITCODE; Text = $text; Out = $out; Zips = @(Get-ChildItem -LiteralPath $out -Filter '*.zip' -ErrorAction SilentlyContinue) }
}

function Read-ZipEntryText($za, [string]$suffix) {
    $e = @($za.Entries | Where-Object { $_.FullName.EndsWith($suffix) })
    if ($e.Count -ne 1) { return $null }
    $sr = New-Object IO.StreamReader($e[0].Open())
    try { $sr.ReadToEnd() } finally { $sr.Dispose() }
}

# --- the default -------------------------------------------------------------------------------------------------------

Fact 'P124_TheDefaultClientIsOurBuildAndTheBuilderTakesItFromTheHelper' {
    Assert-Equal 'D:\UO\tazuo-sl-build\client' (Get-PackageClientDefault) 'one default, our build'
    $ast = [Management.Automation.Language.Parser]::ParseFile($builder, [ref]$null, [ref]$null)
    $p = @($ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'TazUO' })
    Assert-Equal 1 $p.Count 'Build-PlayerPackage.ps1 has -TazUO'
    $default = [string]$p[0].DefaultValue
    Assert-True ($default -notmatch 'TazUO\\client') ('-TazUO no longer defaults to the stock folder: ' + $default)
    Assert-True ([IO.File]::ReadAllText($builder) -match '\.\s+\(Join-Path \$PSScriptRoot ''PackageClient\.ps1''\)') 'the builder dot-sources PackageClient.ps1'
    Assert-True ($default -match 'Get-PackageClientDefault|\$null' -or -not $p[0].DefaultValue) ('the default comes from the helper, not a literal: ' + $default)
}

# --- the stale-build gate ------------------------------------------------------------------------------------------------

Fact 'P124_AClientWithNoSlBuildTxtIsRefusedWithHowToRebuild' {
    $d = New-FakeClient 'stock' $null
    $r = Invoke-Builder @('-TazUO', $d)
    Assert-True ($r.Code -ne 0) 'the build stops'
    Assert-True ($r.Text -match 'sl-build\.txt') ('it names sl-build.txt: ' + $r.Text)
    Assert-True ($r.Text -match 'Build-Client\.ps1') 'and says how to rebuild the client'
    Assert-Equal 0 $r.Zips.Count 'no zip'
}

Fact 'P124_ABuildMissingAPatchIsRefusedAsStale' {
    $d = New-FakeClient 'missing' (New-SlBuild @($currentLines[0]))
    $r = Invoke-Builder @('-TazUO', $d)
    Assert-True ($r.Code -ne 0) 'the build stops'
    Assert-True ($r.Text -match 'STALE CLIENT BUILD') ('it says stale: ' + $r.Text)
    Assert-True ($r.Text -match [regex]::Escape($repoPatches.Patches[-1].Name)) 'and names the patch the build lacks'
    Assert-True ($r.Text -match 'Build-Client\.ps1') 'and how to rebuild'
    Assert-Equal 0 $r.Zips.Count 'no zip'
}

Fact 'P124_ABuildWithAnOlderPatchOrAnotherCommitIsRefusedAsStale' {
    $older = @($currentLines | ForEach-Object { $_ })
    $older[0] = $older[0] -replace 'sha256 [0-9a-f]{64}$', ('sha256 ' + ('0' * 64))
    $d = New-FakeClient 'older' (New-SlBuild $older)
    $r = Invoke-Builder @('-TazUO', $d)
    Assert-True ($r.Code -ne 0 -and $r.Text -match 'STALE CLIENT BUILD' -and $r.Text -match 'older') ('an older patch: ' + $r.Text)

    $other = @('TazUO upstream ' + ('1' * 40)) + $currentLines
    $d2 = New-FakeClient 'commit' $other
    $r2 = Invoke-Builder @('-TazUO', $d2)
    Assert-True ($r2.Code -ne 0 -and $r2.Text -match 'STALE CLIENT BUILD' -and $r2.Text -match 'TAZUO_COMMIT') ('another commit: ' + $r2.Text)
}

Fact 'P124_TheGateReadsTodaysAndTheOlderSlBuildTxtShape' {
    # Build-Client.ps1 writes "built" on its own line; the build of 2026-10-14 has it on the upstream line.
    $a = New-FakeClient 'shape-a' (New-SlBuild $currentLines)
    $b = New-FakeClient 'shape-b' (@('TazUO upstream ' + $repoPatches.Upstream + ' built 2026-10-14 17:35') + $currentLines)
    Assert-Equal 0 @(Test-PackageClient $a $repo).Count ('separate line: ' + (Test-PackageClient $a $repo))
    Assert-Equal 0 @(Test-PackageClient $b $repo).Count ('one line: ' + (Test-PackageClient $b $repo))
    Assert-Equal '2026-10-14 17:35' (Read-ClientBuildInfo $b).Built
}

Fact 'P124_TheClientVersionComesFromSlBuildTxt' {
    $d = New-FakeClient 'version' (New-SlBuild $currentLines)
    $n = $repoPatches.Patches.Count
    Assert-Equal ($repoPatches.Upstream.Substring(0, 8) + ' + ' + $n + ' patches') (Get-ClientBuildVersion $d)
    Assert-Equal $null (Get-ClientBuildVersion (New-FakeClient 'noversion' $null)) 'no sl-build.txt, no version'
}

# --- a real build: the add-on art gate and the reported version -------------------------------------------------------

$realZip = $null
$realRun = $null
$addons = Join-Path $repo 'client-addons\paperdoll-nsfw'
Fact 'P124_ARealBuildFromAClientHoldingTheNsfwAddOnShipsNoneOfIt' {
    if (-not (Test-Path -LiteralPath (Join-Path $ourBuild 'sl-build.txt'))) { throw "no client build at $ourBuild" }
    $copy = Join-Path $work 'client'
    & robocopy.exe $ourBuild $copy /E /NFL /NDL /NJH /NJS /NP | Out-Null
    # Plant the add-on inside the client folder, and swap a Topless body over the client's own ExternalImages body, as a
    # local test client might hold them. Without the add-on on this machine, made-up art stands in for it.
    $plant = Join-Path $copy 'client-addons\paperdoll-nsfw'
    New-Item -ItemType Directory -Path $plant -Force | Out-Null
    if (Test-Path -LiteralPath $addons) { & robocopy.exe $addons $plant /E /NFL /NDL /NJH /NJS /NP | Out-Null }
    else { [IO.File]::WriteAllBytes((Join-Path $plant 'topless-12.png'), [byte[]](1..64)) }
    $gumps = Join-Path $copy 'ExternalImages\gumps'
    New-Item -ItemType Directory -Path $gumps -Force | Out-Null
    $nsfwBody = @(Get-ChildItem -LiteralPath $plant -Recurse -File -Filter '*.png' | Select-Object -First 1)
    Copy-Item -LiteralPath $nsfwBody[0].FullName -Destination (Join-Path $gumps '12.png') -Force
    $planted = @{}
    foreach ($f in Get-ChildItem -LiteralPath $plant -Recurse -File) { $planted[(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()] = $f.Name }

    $script:realRun = Invoke-Builder @('-TazUO', $copy, '-Notes', 'P124 test build')
    Assert-Equal 0 $script:realRun.Code ('the build passes every gate: ' + $script:realRun.Text)
    Assert-Equal 1 $script:realRun.Zips.Count 'one zip'
    $script:realZip = $script:realRun.Zips[0].FullName

    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $za = [IO.Compression.ZipFile]::OpenRead($script:realZip)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bad = @()
        foreach ($e in $za.Entries) {
            if ($e.FullName -match '(?i)client-addons|paperdoll-nsfw|topless|nude') { $bad += $e.FullName; continue }
            if ($e.FullName.EndsWith('/')) { continue }
            $st = $e.Open()
            try { $h = -join ($sha.ComputeHash($st) | ForEach-Object { $_.ToString('x2') }) } finally { $st.Dispose() }
            if ($planted.ContainsKey($h)) { $bad += ($e.FullName + ' (' + $planted[$h] + ')') }
        }
        Assert-Equal 0 $bad.Count ('add-on art in the zip: ' + ($bad -join ', '))
    } finally { $sha.Dispose(); $za.Dispose() }
}

Fact 'P124_TheBuildReportsTheClientVersionFromSlBuildTxt' {
    if (-not $script:realZip) { throw 'the real build above did not produce a zip' }
    $want = Get-ClientBuildVersion $ourBuild
    Assert-True ($script:realRun.Text -match [regex]::Escape('TazUO ' + $want)) ('the summary says TazUO ' + $want + ': ' + $script:realRun.Text)
    Assert-True ($script:realRun.Text -notmatch 'TazUO unknown') 'not unknown'
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $za = [IO.Compression.ZipFile]::OpenRead($script:realZip)
    try {
        $v = Read-ZipEntryText $za '/app/VERSION.txt'
        Assert-True ($v -match [regex]::Escape('TazUO ' + $want)) ('app\VERSION.txt: ' + $v)
        $sl = Read-ZipEntryText $za '/app/tazuo/sl-build.txt'
        Assert-True ($sl -and $sl -match 'TazUO upstream') 'and the client''s sl-build.txt ships beside TazUO.exe (the launcher reads it)'
    } finally { $za.Dispose() }
}

Fact 'P124_AZipWhoseBodyArtIsNotTheSfwSetFailsTheCheck' {
    if (-not $script:realZip) { throw 'the real build above did not produce a zip' }
    # The same zip with one shipped body gump replaced by art the known-hash list has never seen: any body that is not
    # the vendored SFW one must stop it.
    $bad = Join-Path $work 'bodyswap\ShatteredLegacy-2099.01.01.0000.zip'
    New-Item -ItemType Directory -Path (Split-Path $bad -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $script:realZip -Destination $bad
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $za = [IO.Compression.ZipFile]::Open($bad, 'Update')
    try {
        $e = @($za.Entries | Where-Object { $_.FullName -match '/app/paperdoll-sfw/12\.png$' })
        Assert-Equal 1 $e.Count 'the zip has app/paperdoll-sfw/12.png'
        $name = $e[0].FullName
        $e[0].Delete()
        $n = $za.CreateEntry($name)
        $s = $n.Open()
        try { $b = [byte[]](0..255); $s.Write($b, 0, $b.Length) } finally { $s.Dispose() }
    } finally { $za.Dispose() }
    $args2 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $builder, '-CheckZip', $bad)
    $ErrorActionPreference = 'Continue'
    $text = & powershell.exe @args2 2>&1 | Out-String
    Assert-True ($LASTEXITCODE -ne 0) ('-CheckZip fails: ' + $text)
    Assert-True ($text -match 'NSFW GATE' -and $text -match 'paperdoll-sfw/12\.png') ('and says which body: ' + $text)
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed })
Write-Host ''
Write-Host ("  {0} of {1} facts passed" -f ($results.Count - $failed.Count), $results.Count)
exit $failed.Count
