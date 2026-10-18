#Requires -Version 5.1
# Facts for the Legion scripts (cc-P144): the player side (payload\app\LegionScripts.ps1, Update-Scripts.ps1, the
# -ClientDir sync in Launch-Settings.ps1, Update.ps1's hands-off rule, the package gate) and the dev side
# (scripts\legion\: Setup-ScriptsRepo.ps1 and Publish-LegionScripts.ps1 against a scratch target).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Legion-Scripts.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Launch-Settings.Tests.ps1.
#
# Everything runs in a scratch folder under %TEMP% and on this PC: a python http.server on 127.0.0.1 stands in for
# get.shatteredlegacyuo.com/scripts/ and Haven's dev channel, a bare git repo for GitHub, Git Bash for Haven. Nothing
# reaches Haven, GitHub or a real install. Needs python and Git for Windows on PATH.

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$pkg  = Split-Path $here -Parent
$repoRoot = Split-Path $pkg -Parent
$appSrc = Join-Path $pkg 'payload\app'
$legion = Join-Path $repoRoot 'scripts\legion'
$work = Join-Path $env:TEMP ('sl-legion-tests-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$env:SL_LEGION_SETTINGS = Join-Path $work 'no-settings.json'

. (Join-Path $appSrc 'Launch-Settings.ps1')

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
function Assert-Equal { param($Expected, $Actual, [string]$Because = '') if ($Expected -ne $Actual) { throw "expected [$Expected] but got [$Actual]. $Because" } }
function Assert-True { param($Condition, [string]$Because) if (-not $Condition) { throw "not true: $Because" } }
function New-Dir([string]$name) { $d = Join-Path $work $name; New-Item -ItemType Directory -Path $d -Force | Out-Null; $d }
function Sha([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLower() }
function Write-Text([string]$p, [string]$t) { $d = Split-Path $p -Parent; if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }; [IO.File]::WriteAllText($p, $t, (New-Object Text.ASCIIEncoding)) }

# --- the stand-in web server: $www\official\scripts and $www\dev ------------------------------------------------------
$www = New-Dir 'www'
$port = Get-Random -Minimum 20000 -Maximum 40000
$server = Start-Process -FilePath python -ArgumentList @('-m', 'http.server', $port, '--bind', '127.0.0.1', '--directory', $www) -PassThru -WindowStyle Hidden
$base = "http://127.0.0.1:$port/"
$offBase = $base + 'official/scripts/'
$devBase = $base + 'dev/'
for ($i = 0; $i -lt 40; $i++) { try { $null = Invoke-WebRequest -Uri $base -UseBasicParsing -TimeoutSec 1; break } catch { Start-Sleep -Milliseconds 250 } }

# Publishes a channel by hand: the given name = text pairs, and a scripts.json describing them. $Lie names a file whose
# manifest entry carries the wrong hash (a corrupt download, as the client sees it).
function Set-Channel {
    param([string]$Channel, [hashtable]$Files, [string]$Version = '2026.10.18.0900', [string]$Lie)
    $dir = $(if ($Channel -eq 'dev') { Join-Path $www 'dev' } else { Join-Path $www 'official\scripts' })
    if (Test-Path $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $list = @()
    foreach ($k in @($Files.Keys | Sort-Object)) {
        $p = Join-Path $dir $k
        Write-Text $p $Files[$k]
        $sha = Sha $p
        if ($k -eq $Lie) { $sha = ('0' * 64) }
        $list += [ordered]@{ name = $k; sha256 = $sha; bytes = (Get-Item $p).Length; modified = '2026-10-18T09:00:00Z'; publishedFrom = 'TEST'; published = '2026-10-18T09:00:00Z' }
    }
    [IO.File]::WriteAllText((Join-Path $dir 'scripts.json'), ([ordered]@{ version = $Version; channel = $Channel; publishedFrom = 'TEST'; files = $list } | ConvertTo-Json -Depth 5), (New-Object Text.ASCIIEncoding))
}
function Clear-Channel([string]$Channel) {
    $dir = $(if ($Channel -eq 'dev') { Join-Path $www 'dev' } else { Join-Path $www 'official\scripts' })
    if (Test-Path $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
}

# A scratch install: app\ with the real payload scripts and a stub TazUO.exe. Returns its root.
function New-Install([string]$name) {
    $root = New-Dir $name
    $app = Join-Path $root 'app'
    New-Item -ItemType Directory -Path (Join-Path $app 'tazuo') -Force | Out-Null
    foreach ($f in 'LegionScripts.ps1', 'Launch-Settings.ps1', 'Update-Scripts.ps1', 'Paperdoll-Bodies.ps1') { Copy-Item -LiteralPath (Join-Path $appSrc $f) -Destination $app }
    Write-Text (Join-Path $app 'tazuo\TazUO.exe') 'stub'
    $root
}
function Invoke-Updater {
    param([string]$Root, [string[]]$More = @(), [string]$Settings = $env:SL_LEGION_SETTINGS, [string]$Off = $offBase)
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $Root 'app\Update-Scripts.ps1'), '-OfficialBase', $Off, '-SettingsPath', $Settings, '-TimeoutSec', '3') + $More
    $out = (& powershell.exe @a 2>&1 | Out-String)
    [pscustomobject]@{ Out = $out; Code = $LASTEXITCODE }
}
function Get-LS([string]$Root) { Join-Path $Root 'app\tazuo\LegionScripts' }

# ======================================================================================================================
# The updater on an install (Part C)

Fact 'AFreshInstallGetsEveryPublishedScriptAndARecord' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")'; 'SL_B.py' = 'print("b1")' }
    $r = New-Install 'fresh'
    $u = Invoke-Updater $r
    Assert-Equal 0 $u.Code $u.Out
    Assert-Equal 'print("a1")' ([IO.File]::ReadAllText((Join-Path (Get-LS $r) 'SL_A.py'))) 'SL_A.py installed'
    Assert-True (Test-Path (Join-Path (Get-LS $r) 'SL_B.py')) 'SL_B.py installed'
    $rec = Read-LegionRecord (Get-LS $r)
    Assert-Equal (Sha (Join-Path (Get-LS $r) 'SL_A.py')) ([string]$rec['SL_A.py'].sha256) 'the record holds what was installed'
    Assert-True ($u.Out -match 'SL_A\.py: added') $u.Out
}

Fact 'AnUpToDateInstallChangesNothing' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' }
    $r = New-Install 'uptodate'
    $null = Invoke-Updater $r
    $f = Join-Path (Get-LS $r) 'SL_A.py'
    $t0 = (Get-Item $f).LastWriteTimeUtc
    Start-Sleep -Milliseconds 1100
    $u = Invoke-Updater $r
    Assert-Equal $t0 (Get-Item $f).LastWriteTimeUtc 'not rewritten'
    Assert-True ($u.Out -match 'up to date') $u.Out
    Assert-True (-not (Test-Path (Join-Path (Get-LS $r) 'backup'))) 'no backup'
}

Fact 'AScriptChangedUpstreamIsUpdatedWithNoBackup' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' }
    $r = New-Install 'changed'
    $null = Invoke-Updater $r
    Set-Channel official @{ 'SL_A.py' = 'print("a2")' } -Version '2026.10.18.1000'
    $u = Invoke-Updater $r
    Assert-Equal 'print("a2")' ([IO.File]::ReadAllText((Join-Path (Get-LS $r) 'SL_A.py'))) $u.Out
    Assert-True (-not (Test-Path (Join-Path (Get-LS $r) 'backup'))) 'the copy was the one installed: no backup'
    Assert-True ($u.Out -match 'SL_A\.py: updated \(official 2026\.10\.18\.1000\)') $u.Out
}

Fact 'AScriptEditedLocallyIsBackedUpBeforeItIsReplaced' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' }
    $r = New-Install 'edited'
    $null = Invoke-Updater $r
    Write-Text (Join-Path (Get-LS $r) 'SL_A.py') 'print("my own edit")'
    Set-Channel official @{ 'SL_A.py' = 'print("a2")' } -Version '2026.10.18.1000'
    $u = Invoke-Updater $r
    Assert-Equal 'print("a2")' ([IO.File]::ReadAllText((Join-Path (Get-LS $r) 'SL_A.py'))) 'replaced'
    $b = @(Get-ChildItem -LiteralPath (Join-Path (Get-LS $r) 'backup') -Filter 'SL_A.*.py')
    Assert-Equal 1 $b.Count 'one backup'
    Assert-Equal 'print("my own edit")' ([IO.File]::ReadAllText($b[0].FullName)) 'the edit is in it'
    Assert-True ($b[0].Name -match '^SL_A\.\d{8}-\d{4}\.py$') $b[0].Name
    Assert-True ($u.Out -match 'your own edits were kept in backup\\SL_A\.') $u.Out
}

Fact 'AScriptTheManifestDoesNotListIsNeverTouched' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' }
    $r = New-Install 'localonly'
    Write-Text (Join-Path (Get-LS $r) 'MyOwn.py') 'print("mine")'
    $null = Invoke-Updater $r
    Set-Channel official @{ 'SL_B.py' = 'print("b1")' } -Version '2026.10.18.1000'
    $null = Invoke-Updater $r
    Assert-Equal 'print("mine")' ([IO.File]::ReadAllText((Join-Path (Get-LS $r) 'MyOwn.py'))) 'the player''s own script'
    Assert-True (Test-Path (Join-Path (Get-LS $r) 'SL_A.py')) 'a script dropped from the manifest is not deleted either'
}

Fact 'OfflineNeverBlocksPlay' {
    $r = New-Install 'offline'
    $closed = Get-Random -Minimum 40001 -Maximum 50000
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $u = Invoke-Updater $r -Off "http://127.0.0.1:$closed/scripts/"
    Assert-Equal 0 $u.Code 'exit 0'
    Assert-True ($sw.Elapsed.TotalSeconds -lt 30) ('took ' + $sw.Elapsed.TotalSeconds)
    Assert-Equal 1 @(($u.Out -split "`n") | Where-Object { $_ -match 'Legion scripts not checked' }).Count $u.Out
    Clear-Channel official
    $u = Invoke-Updater $r
    Assert-Equal 0 $u.Code '404 too'
    Assert-True ($u.Out -match 'none published yet') $u.Out
}

Fact 'ACorruptDownloadIsNotApplied' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' }
    $r = New-Install 'corrupt'
    $null = Invoke-Updater $r
    Set-Channel official @{ 'SL_A.py' = 'print("a2")' } -Version '2026.10.18.1000' -Lie 'SL_A.py'
    $u = Invoke-Updater $r
    Assert-Equal 0 $u.Code $u.Out
    Assert-Equal 'print("a1")' ([IO.File]::ReadAllText((Join-Path (Get-LS $r) 'SL_A.py'))) 'the old copy stays'
    Assert-True ($u.Out -match 'SL_A\.py: the download is not the published file.*Not applied') $u.Out
    Assert-Equal 0 @(Get-ChildItem -LiteralPath (Get-LS $r) -Filter '*.sl-new').Count 'no half file left'
}

Fact 'AClientDirClientGetsAChangedPublishedScriptWithTheSameBackupRule' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' }
    $r = New-Install 'withclient'
    $cli = New-Dir 'client1'
    Write-Text (Join-Path $cli 'TazUO.exe') 'stub'
    Write-Text (Join-Path $r 'app\client-dirs.txt') $cli
    $null = Invoke-Updater $r
    Assert-Equal 'print("a1")' ([IO.File]::ReadAllText((Join-Path $cli 'LegionScripts\SL_A.py'))) 'copied to the client'
    Set-Channel official @{ 'SL_A.py' = 'print("a2")' } -Version '2026.10.18.1000'
    $u = Invoke-Updater $r
    Assert-Equal 'print("a2")' ([IO.File]::ReadAllText((Join-Path $cli 'LegionScripts\SL_A.py'))) ('updated in the client: ' + $u.Out)
    Write-Text (Join-Path $cli 'LegionScripts\SL_A.py') 'print("edited in the client")'
    Set-Channel official @{ 'SL_A.py' = 'print("a3")' } -Version '2026.10.18.1100'
    $null = Invoke-Updater $r
    Assert-Equal 'print("a3")' ([IO.File]::ReadAllText((Join-Path $cli 'LegionScripts\SL_A.py'))) 'replaced'
    $b = @(Get-ChildItem -LiteralPath (Join-Path $cli 'LegionScripts\backup') -Filter 'SL_A.*.py')
    Assert-True ($b.Count -eq 1 -and [IO.File]::ReadAllText($b[0].FullName) -eq 'print("edited in the client")') 'the client edit was backed up'
    # Play.ps1's own call, offline: the install already holds a3, the client gets it from there.
    $cli2 = New-Dir 'client2'
    Write-Text (Join-Path $cli2 'TazUO.exe') 'stub'
    Write-Text (Join-Path $cli2 'LegionScripts\SL_A.py') 'print("a1")'
    $got = @(Sync-ClientDirData -TazDir (Join-Path $r 'app\tazuo') -ClientDir $cli2)
    Assert-Equal 'print("a3")' ([IO.File]::ReadAllText((Join-Path $cli2 'LegionScripts\SL_A.py'))) ('Sync-ClientDirData updates a stale published copy: ' + ($got -join ' | '))
    Assert-True (Test-Path (Join-Path $cli2 'LegionScripts\backup')) 'unrecorded and different: kept as the player''s, backed up'
}

Fact 'ADevPcGetsDevOnTopOfOfficialAndAPlayerPcNeverReadsDev' {
    Set-Channel official @{ 'SL_A.py' = 'print("a-official")'; 'SL_B.py' = 'print("b-official")' }
    Set-Channel dev @{ 'SL_A.py' = 'print("a-dev")' }
    $devSet = Join-Path $work 'dev-settings.json'
    [IO.File]::WriteAllText($devSet, (@{ dev = $true; devUrl = $devBase } | ConvertTo-Json))
    $r = New-Install 'devpc'
    $u = Invoke-Updater $r -Settings $devSet
    Assert-Equal 'print("a-dev")' ([IO.File]::ReadAllText((Join-Path (Get-LS $r) 'SL_A.py'))) ('dev replaces official: ' + $u.Out)
    Assert-Equal 'print("b-official")' ([IO.File]::ReadAllText((Join-Path (Get-LS $r) 'SL_B.py'))) 'official where dev has none'
    $p = New-Install 'playerpc'
    $null = Invoke-Updater $p
    Assert-Equal 'print("a-official")' ([IO.File]::ReadAllText((Join-Path (Get-LS $p) 'SL_A.py'))) 'a PC not flagged never reads dev'
    # The one-line form of the flag.
    $one = Join-Path $work 'one-line.txt'
    [IO.File]::WriteAllText($one, "dev`r`n")
    Assert-True (Read-LegionSettings -Path $one).Dev 'a file holding just dev turns it on'
    # Tailscale off: the dev channel does not answer. Official only, one quiet line, exit 0.
    $deadSet = Join-Path $work 'dead-dev.json'
    $closed = Get-Random -Minimum 40001 -Maximum 50000
    [IO.File]::WriteAllText($deadSet, (@{ dev = $true; devUrl = "http://127.0.0.1:$closed/" } | ConvertTo-Json))
    $q = New-Install 'devoffline'
    $u = Invoke-Updater $q -Settings $deadSet
    Assert-Equal 0 $u.Code $u.Out
    Assert-Equal 'print("a-official")' ([IO.File]::ReadAllText((Join-Path (Get-LS $q) 'SL_A.py'))) 'official only'
    Assert-True ($u.Out -match 'Dev scripts not reachable \(is Tailscale running\?\): official scripts only') $u.Out
}

Fact 'AnInstallLinkedToTheScriptsRepoIsNeverDownloadedInto' {
    Set-Channel official @{ 'SL_A.py' = 'print("a-official")' }
    $clone = New-Dir 'linked-clone'
    New-Item -ItemType Directory -Path (Join-Path $clone '.git') -Force | Out-Null
    Write-Text (Join-Path $clone 'SL_A.py') 'print("from git")'
    $r = New-Install 'linked'
    New-Item -ItemType Junction -Path (Get-LS $r) -Target $clone | Out-Null
    # Not set up on this PC's settings: the updater still sees the link and leaves it alone.
    $u = Invoke-Updater $r
    Assert-Equal 'print("from git")' ([IO.File]::ReadAllText((Join-Path $clone 'SL_A.py'))) 'the repo was not written'
    Assert-True ($u.Out -match 'come from a repo link: use Pull scripts') $u.Out
    # Set up: it says so and changes nothing.
    $set = Join-Path $work 'linked-settings.json'
    [IO.File]::WriteAllText($set, (@{ dev = $true; repo = $clone } | ConvertTo-Json))
    $u = Invoke-Updater $r -Settings $set
    Assert-True ($u.Out -match 'Legion scripts come from your repo: use Pull scripts') $u.Out
    Assert-Equal 'print("from git")' ([IO.File]::ReadAllText((Join-Path $clone 'SL_A.py'))) 'still not written'
    Assert-Equal 0 @(Get-ChildItem -LiteralPath $clone -Force | Where-Object { $_.Name -eq '.sl-scripts.json' }).Count 'no record written into the repo'
}

Fact 'AnInstallOnAPcWithTheRepoIsLinkedByItself' {
    $clone = New-Dir 'autolink-clone'
    New-Item -ItemType Directory -Path (Join-Path $clone '.git') -Force | Out-Null
    $set = Join-Path $work 'autolink-settings.json'
    [IO.File]::WriteAllText($set, (@{ dev = $true; repo = $clone } | ConvertTo-Json))
    $r = New-Install 'autolink'
    Write-Text (Join-Path (Get-LS $r) 'SL_New.py') 'print("shipped")'
    $u = Invoke-Updater $r -Settings $set
    Assert-True (Test-LegionLink (Get-LS $r)) ('linked: ' + $u.Out)
    Assert-Equal $clone (Get-LegionLinkTarget (Get-LS $r)) 'to the clone'
    Assert-True (Test-Path (Join-Path $clone 'SL_New.py')) 'its script the repo lacked was brought in'
    Assert-True ($u.Out -match 'brought into the repo \(not committed\): SL_New\.py') $u.Out
}

Fact 'ANonDevInstallNeverSeesTheRepoOffer' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' }
    $r = New-Install 'nooffer'
    $u = Invoke-Updater $r -More @('-Offer')
    Assert-True ($u.Out -notmatch 'dev PC|scripts repo') $u.Out
}

# ======================================================================================================================
# Update.ps1 never touches LegionScripts; the package ships only the published set

function Get-ScriptFunction([string]$Path, [string]$Name) {
    $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$null)
    $f = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $Name }, $true)
    if (-not $f) { throw "$Name is not in $Path" }
    $f.Extent.Text
}

Fact 'APackageUpdateNeverWritesBacksUpOrRetiresLegionScripts' {
    $root = 'C:\nowhere-' + [Guid]::NewGuid().ToString('N')
    . ([scriptblock]::Create((Get-ScriptFunction (Join-Path $appSrc 'Update.ps1') 'Test-Kept')))
    . ([scriptblock]::Create((Get-ScriptFunction (Join-Path $appSrc 'Update.ps1') 'Test-RetiredLine')))
    Assert-True (Test-Kept 'app\tazuo\LegionScripts\SL_Mining.py') 'a shipped script never overwrites the folder Update-Scripts.ps1 owns'
    Assert-True (Test-Kept 'app\tazuo\LegionScripts\.sl-scripts.json') 'nor its record'
    Assert-True (-not (Test-Kept 'app\tazuo\TazUO.exe')) 'the client still updates'
    Assert-True ([string](Test-RetiredLine 'app\tazuo\LegionScripts') -match 'LegionScripts') 'no retired-files.txt line removes it'
    Assert-True ([string](Test-RetiredLine 'app\tazuo\LegionScripts\SL_A.py') -match 'LegionScripts') 'or anything in it'
    $u = [IO.File]::ReadAllText((Join-Path $appSrc 'Update.ps1'))
    Assert-True ($u -notmatch "Copy-Item -LiteralPath \(Join-Path \`$root 'app'\) -Destination \(Join-Path \`$backup 'app'\) -Recurse") 'the backup no longer copies app\ whole (it would follow a repo junction)'
    Assert-True ($u -match 'StartsWith\(\$legion \+ ''\\''') 'the backup skips app\tazuo\LegionScripts'
    $b = [IO.File]::ReadAllText((Join-Path $pkg 'Build-PlayerPackage.ps1'))
    . ([scriptblock]::Create((Get-ScriptFunction (Join-Path $pkg 'Build-PlayerPackage.ps1') 'Test-RetiredLine')))
    Assert-True ([string](Test-RetiredLine 'app\tazuo\LegionScripts') -match 'LegionScripts') 'the build gate refuses the same retired line'
}

Fact 'TheZipMayHoldOnlyThePublishedSetItsRecordNames' {
    $rec = (@{ schema = 1; installed = @{ 'SL_A.py' = @{ sha256 = ('a' * 64) } } } | ConvertTo-Json -Depth 4)
    Assert-Equal 0 @(Test-LegionShipped -Files @{} -RecordText $null).Count 'no scripts: fine'
    Assert-Equal 0 @(Test-LegionShipped -Files @{ 'SL_A.py' = ('a' * 64); '.sl-scripts.json' = 'x' } -RecordText $rec).Count 'the published set and its record'
    Assert-True (@(Test-LegionShipped -Files @{ 'SL_A.py' = ('a' * 64) } -RecordText $null) -match 'no \.sl-scripts\.json').Count 'scripts without a record'
    Assert-True (@(Test-LegionShipped -Files @{ 'SL_A.py' = ('b' * 64); '.sl-scripts.json' = 'x' } -RecordText $rec) -match 'not the file').Count 'a changed file'
    Assert-True (@(Test-LegionShipped -Files @{ 'SL_A.py' = ('a' * 64); 'Mine.py' = ('c' * 64); '.sl-scripts.json' = 'x' } -RecordText $rec) -match 'Mine\.py is not in').Count 'Chase''s own unpublished script'
    Assert-True (@(Test-LegionShipped -Files @{ 'SL_A.py' = ('a' * 64); 'API.py' = ('c' * 64); '.sl-scripts.json' = 'x' } -RecordText $rec) -match 'API\.py is written by each client').Count 'API.py'
    Assert-True (@(Test-LegionShipped -Files @{ '.sl-scripts.json' = 'x' } -RecordText $rec) -match 'lists SL_A\.py, which the zip does not hold').Count 'a record naming a missing file'
    $b = [IO.File]::ReadAllText((Join-Path $pkg 'Build-PlayerPackage.ps1'))
    Assert-True ($b -match 'Copy-LegionPublishedSet -Base \$ScriptsBase' -and $b -match 'Test-LegionShipped') 'the build ships the published set and gate 4 checks it'
}

Fact 'TheBuildShipsThePublishedSetCheckedAndRecorded' {
    Set-Channel official @{ 'SL_A.py' = 'print("a1")'; 'SL_B.py' = 'print("b1")' }
    $dest = Join-Path (New-Dir 'ship') 'LegionScripts'
    $r = Copy-LegionPublishedSet -Base $offBase -Folder $dest
    Assert-Equal 2 $r.Count $r.Text
    Assert-Equal 0 @(Get-LegionFolderPlan -Folder $dest -Entries @((Merge-LegionManifests -Official (Get-LegionManifest -Base $offBase).Manifest -OfficialBase $offBase)) | Where-Object { $_.Action -ne 'same' }).Count 'a new install starts current'
    Clear-Channel official
    $r = Copy-LegionPublishedSet -Base $offBase -Folder (Join-Path (New-Dir 'ship2') 'LegionScripts')
    Assert-Equal 0 $r.Count 'nothing published yet: nothing shipped, said'
    Set-Channel official @{ 'SL_A.py' = 'print("a1")' } -Lie 'SL_A.py'
    $threw = $false
    try { $null = Copy-LegionPublishedSet -Base $offBase -Folder (Join-Path (New-Dir 'ship3') 'LegionScripts') } catch { $threw = $true }
    Assert-True $threw 'a file that is not the published one stops the build'
}

Fact 'PlayRunsTheScriptsCheckAfterThePackageCheckAndKeepsItsParameters' {
    $p = [IO.File]::ReadAllText((Join-Path $appSrc 'Play.ps1'))
    $pkgCheck = $p.IndexOf('$remote = Get-RemotePackage $UpdateBase')
    $scripts = $p.IndexOf("& (Join-Path `$app 'Update-Scripts.ps1') @usArgs")
    $setup = $p.IndexOf("& (Join-Path `$app 'Setup.ps1') @setupArgs")
    Assert-True ($pkgCheck -gt 0 -and $scripts -gt $pkgCheck -and $setup -gt $scripts) 'package check, then scripts, then Setup'
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $appSrc 'Play.ps1'), [ref]$null, [ref]$null)
    $names = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
    foreach ($n in 'Lan', 'Test', 'Account', 'UoPath', 'ClientDir', 'NoUpdate', 'Update', 'UpdateBase', 'PaperdollBodies', 'PaperdollFolder', 'NoScripts') { Assert-True ($names -contains $n) "Play.ps1 keeps -$n" }
    Assert-True ($p -match "\`$usArgs = @\{ Root = \`$root; Quiet = \`$true; Offer = \`$true \}") 'quietly, applied without a prompt'
    Assert-True (Test-Path -LiteralPath (Join-Path $pkg 'payload\Update Scripts.bat')) 'and a double-click Update Scripts.bat'
}

# ======================================================================================================================
# The dev side: Setup-ScriptsRepo.ps1

function Invoke-Git { $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'; & git @args 2>&1 | Out-Null; $ErrorActionPreference = $prev }
function New-Origin([string]$name, [hashtable]$Files) {
    $o = Join-Path $work ($name + '-origin.git')
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & git init --bare --quiet $o 2>&1 | Out-Null
    $seed = Join-Path $work ($name + '-seed')
    & git clone --quiet $o $seed 2>&1 | Out-Null
    foreach ($k in $Files.Keys) { Write-Text (Join-Path $seed $k) $Files[$k] }
    Write-Text (Join-Path $seed '.gitignore') "API.py`n__builtins__.py`n*.log`n__pycache__/`n*.csproj`n_ScriptContext.cs`nbackup/`n.sl-scripts.json`n"
    & git -C $seed add -A 2>&1 | Out-Null
    & git -C $seed -c user.email=t@t -c user.name=t commit --quiet -m seed 2>&1 | Out-Null
    & git -C $seed push --quiet -u origin HEAD 2>&1 | Out-Null
    $ErrorActionPreference = $prev
    $o
}
function Invoke-Setup {
    param([string[]]$More)
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $legion 'Setup-ScriptsRepo.ps1'), '-Yes', '-ShortcutDirs', (Join-Path $work 'no-shortcuts')) + $More
    $out = (& powershell.exe @a 2>&1 | Out-String)
    [pscustomobject]@{ Out = $out; Code = $LASTEXITCODE }
}

Fact 'SetupClonesSavesThePathAndLinksAFreshInstall' {
    $o = New-Origin 'setup1' @{ 'SL_A.py' = 'print("repo a")' }
    $installs = New-Dir 'setup1-installs'
    $i = Join-Path $installs 'ShatteredLegacy-2026.10.01.0000'
    New-Item -ItemType Directory -Path (Join-Path $i 'app\tazuo') -Force | Out-Null
    Write-Text (Join-Path $i 'app\tazuo\TazUO.exe') 'stub'
    $clone = Join-Path $work 'setup1-clone'
    $set = Join-Path $work 'setup1-settings.json'
    $s = Invoke-Setup -More @('-Path', $clone, '-RepoUrl', $o, '-InstallsRoot', $installs, '-SettingsPath', $set)
    Assert-Equal 0 $s.Code $s.Out
    Assert-True (Test-Path (Join-Path $clone '.git')) 'cloned'
    $st = Read-LegionSettings -Path $set
    Assert-Equal $clone $st.Repo 'the path is saved for this PC'
    Assert-True $st.Dev 'and dev is on'
    $ls = Join-Path $i 'app\tazuo\LegionScripts'
    Assert-True (Test-LegionLink $ls) $s.Out
    Assert-Equal 'print("repo a")' ([IO.File]::ReadAllText((Join-Path $ls 'SL_A.py'))) 'the client sees the repo'
}

Fact 'SetupMovesAnExistingFolderAsideAndBringsItsScriptsInUncommitted' {
    $o = New-Origin 'setup2' @{ 'SL_A.py' = 'print("repo a")' }
    $installs = New-Dir 'setup2-installs'
    $old = Join-Path $installs 'ShatteredLegacy-2026.09.01.0000'
    $new = Join-Path $installs 'ShatteredLegacy-2026.10.01.0000'
    foreach ($i in $old, $new) { Write-Text (Join-Path $i 'app\tazuo\TazUO.exe') 'stub' }
    Write-Text (Join-Path $new 'app\tazuo\LegionScripts\SL_A.py') 'print("install a, different")'
    Write-Text (Join-Path $new 'app\tazuo\LegionScripts\SL_Extra.py') 'print("extra")'
    Write-Text (Join-Path $new 'app\tazuo\LegionScripts\API.py') 'tazuo writes this'
    $cli = New-Dir 'setup2-client'
    Write-Text (Join-Path $cli 'TazUO.exe') 'stub'
    $clone = Join-Path $work 'setup2-clone'
    $set = Join-Path $work 'setup2-settings.json'
    $s = Invoke-Setup -More @('-Path', $clone, '-RepoUrl', $o, '-InstallsRoot', $installs, '-SettingsPath', $set, '-ClientDir', $cli)
    Assert-Equal 0 $s.Code $s.Out
    $ls = Join-Path $new 'app\tazuo\LegionScripts'
    Assert-True (Test-LegionLink $ls) 'the newest install is linked'
    Assert-True (-not (Test-Path (Join-Path $old 'app\tazuo\LegionScripts'))) 'an older install is left alone'
    $aside = @(Get-ChildItem -LiteralPath (Join-Path $new 'app\tazuo') -Directory -Filter 'LegionScripts.before-repo-*')
    Assert-Equal 1 $aside.Count 'moved aside'
    Assert-True (Test-Path (Join-Path $aside[0].FullName 'API.py')) 'everything kept there'
    Assert-Equal 'print("extra")' ([IO.File]::ReadAllText((Join-Path $clone 'SL_Extra.py'))) 'a script the repo lacked is copied in'
    Assert-Equal 'print("repo a")' ([IO.File]::ReadAllText((Join-Path $clone 'SL_A.py'))) 'the repo''s own copy is never overwritten'
    $fi = @(Get-ChildItem -LiteralPath (Join-Path $clone 'from-installs') -Recurse -Filter 'SL_A.py')
    Assert-True ($fi.Count -eq 1 -and [IO.File]::ReadAllText($fi[0].FullName) -eq 'print("install a, different")') 'the differing one goes to from-installs\'
    Assert-True (-not (Test-Path (Join-Path $clone 'API.py'))) 'API.py is not brought in'
    Assert-True (Test-LegionLink (Join-Path $cli 'LegionScripts')) 'the -ClientDir client is linked'
    Assert-True ($s.Out -match 'NOT committed') $s.Out
    $log = (& git -C $clone log --oneline 2>$null | Measure-Object).Count
    Assert-Equal 1 $log 'nothing committed'
}

Fact 'SetupAgainIsANoOpAndRepairsABrokenLink' {
    $o = New-Origin 'setup3' @{ 'SL_A.py' = 'print("repo a")' }
    $installs = New-Dir 'setup3-installs'
    $i = Join-Path $installs 'ShatteredLegacy-2026.10.01.0000'
    Write-Text (Join-Path $i 'app\tazuo\TazUO.exe') 'stub'
    $clone = Join-Path $work 'setup3-clone'
    $set = Join-Path $work 'setup3-settings.json'
    $null = Invoke-Setup -More @('-Path', $clone, '-RepoUrl', $o, '-InstallsRoot', $installs, '-SettingsPath', $set)
    $ls = Join-Path $i 'app\tazuo\LegionScripts'
    $before = @(Get-ChildItem -LiteralPath (Join-Path $i 'app\tazuo') -Force | ForEach-Object { $_.Name }) -join ','
    $s = Invoke-Setup -More @('-InstallsRoot', $installs, '-SettingsPath', $set, '-RepoUrl', $o)
    Assert-Equal 0 $s.Code $s.Out
    Assert-True ($s.Out -match 'already linked') $s.Out
    Assert-Equal $before (@(Get-ChildItem -LiteralPath (Join-Path $i 'app\tazuo') -Force | ForEach-Object { $_.Name }) -join ',') 'nothing else changed'
    Assert-Equal '' ((& git -C $clone status --porcelain 2>$null) -join ',') 'the clone is untouched'
    # Broken: the link points at a folder that is gone.
    [IO.Directory]::Delete($ls, $false)
    $gone = New-Dir 'setup3-gone'
    New-Item -ItemType Junction -Path $ls -Target $gone | Out-Null
    Remove-Item -LiteralPath $gone -Recurse -Force
    Assert-True (-not (Test-Path (Join-Path $ls 'SL_A.py'))) 'the link is broken (5.1''s Test-Path still says True for the link itself)'
    $s = Invoke-Setup -More @('-InstallsRoot', $installs, '-SettingsPath', $set, '-RepoUrl', $o)
    Assert-True ($s.Out -match 'link repaired') $s.Out
    Assert-Equal 'print("repo a")' ([IO.File]::ReadAllText((Join-Path $ls 'SL_A.py'))) 'it reaches the repo again'
    Assert-True (Test-Path (Join-Path $clone 'SL_A.py')) 'and the clone lost nothing'
}

# ======================================================================================================================
# The dev side: Publish-LegionScripts.ps1 against a scratch target

function New-PublishRig([string]$name) {
    $o = New-Origin $name @{ 'SL_A.py' = 'print("a1")'; 'SL_B.py' = 'print("b1")' }
    $clone = Join-Path $work ($name + '-clone')
    Invoke-Git clone --quiet $o $clone
    $set = Join-Path $work ($name + '-settings.json')
    [IO.File]::WriteAllText($set, (@{ dev = $true; repo = $clone } | ConvertTo-Json))
    $scratch = New-Dir ($name + '-target')
    # The target's served folders ARE the stand-in server's.
    [pscustomobject]@{ Origin = $o; Clone = $clone; Settings = $set; Scratch = $scratch }
}
function Invoke-Publish {
    param($Rig, [string]$Mode, [string]$Answer = '', [string[]]$More = @(), [string]$Url = $base)
    # The target's www is the stand-in server's www (a junction), so what the script swaps in is what it downloads back.
    $tw = Join-Path $Rig.Scratch 'www'
    if (-not (Test-LegionLink $tw)) { New-Item -ItemType Junction -Path $tw -Target $www | Out-Null }
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $legion 'Publish-LegionScripts.ps1'), '-Mode', $Mode, '-SettingsPath', $Rig.Settings, '-Scratch', $Rig.Scratch, '-ScratchUrl', $Url) + $More
    if ($Answer) { $a += @('-Answer', $Answer) }
    $out = (& powershell.exe @a 2>&1 | Out-String)
    [pscustomobject]@{ Out = $out; Code = $LASTEXITCODE }
}
function Git-Commit([string]$Clone, [string]$File, [string]$Text, [switch]$Push) {
    Write-Text (Join-Path $Clone $File) $Text
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & git -C $Clone add -A 2>&1 | Out-Null
    & git -C $Clone -c user.email=t@t -c user.name=t commit --quiet -m "edit $File" 2>&1 | Out-Null
    if ($Push) { & git -C $Clone push --quiet 2>&1 | Out-Null }
    $ErrorActionPreference = $prev
}

Fact 'PublishToDevIsRefusedWhenTheRepoIsDirtyAheadOrBehind' {
    Clear-Channel dev
    $g = New-PublishRig 'pubsync'
    Write-Text (Join-Path $g.Clone 'SL_A.py') 'print("uncommitted")'
    $p = Invoke-Publish $g 'Dev' 'yes'
    Assert-Equal 1 $p.Code $p.Out
    Assert-True ($p.Out -match 'uncommitted change.*Commit and push first, so both PCs match') $p.Out
    Git-Commit $g.Clone 'SL_A.py' 'print("committed, not pushed")'
    $p = Invoke-Publish $g 'Dev' 'yes'
    Assert-True ($p.Code -eq 1 -and $p.Out -match 'ahead of GitHub\. Commit and push first') $p.Out
    Invoke-Git -C $g.Clone push --quiet
    # The other PC pushed: this clone is behind.
    $other = Join-Path $work 'pubsync-other'
    Invoke-Git clone --quiet $g.Origin $other
    Git-Commit $other 'SL_B.py' 'print("from the work PC")' -Push
    $p = Invoke-Publish $g 'Dev' 'yes'
    Assert-True ($p.Code -eq 1 -and $p.Out -match 'behind GitHub\. Pull scripts first') $p.Out
    Assert-True (-not (Test-Path (Join-Path $www 'dev\scripts.json'))) 'nothing was published'
}

Fact 'PublishToDevDryRunStopsBeforeTheSwap' {
    Clear-Channel dev
    $g = New-PublishRig 'pubdry'
    $p = Invoke-Publish $g 'Dev' 'yes' -More @('-DryRun')
    Assert-Equal 0 $p.Code $p.Out
    Assert-True ($p.Out -match 'DRY RUN: stopping before the swap') $p.Out
    Assert-True (-not (Test-Path (Join-Path $www 'dev\SL_A.py'))) 'nothing served'
    $p = Invoke-Publish $g 'Dev' 'no'
    Assert-Equal 3 $p.Code ('declined: ' + $p.Out)
    Assert-True (-not (Test-Path (Join-Path $www 'dev\SL_A.py'))) 'declined: nothing served'
}

Fact 'PublishToDevServesTheScriptsWithTheManifestLastAndABackup' {
    Clear-Channel dev
    $g = New-PublishRig 'pubreal'
    $p = Invoke-Publish $g 'Dev' 'yes'
    Assert-Equal 0 $p.Code $p.Out
    $m = [IO.File]::ReadAllText((Join-Path $www 'dev\scripts.json')) | ConvertFrom-Json
    Assert-Equal 'dev' $m.channel
    Assert-Equal $env:COMPUTERNAME $m.publishedFrom 'publishedFrom per set'
    Assert-Equal $env:COMPUTERNAME @($m.files)[0].publishedFrom 'and per file'
    Assert-Equal ((& git -C $g.Clone rev-parse HEAD) -join '') $m.commit 'the commit'
    Assert-Equal (Sha (Join-Path $g.Clone 'SL_A.py')) ([string](@($m.files | Where-Object { $_.name -eq 'SL_A.py' })[0].sha256)) 'the clone''s bytes'
    Assert-True ($p.Out.IndexOf('download every listed script back') -lt $p.Out.IndexOf('put scripts.json in place')) 'scripts verified before the manifest'
    # Unchanged: nothing to do. Then a removed script stays only in the backup.
    $p = Invoke-Publish $g 'Dev' 'yes'
    Assert-True ($p.Code -eq 0 -and $p.Out -match 'Nothing changed since the last dev publish') $p.Out
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & git -C $g.Clone rm --quiet SL_B.py 2>&1 | Out-Null
    & git -C $g.Clone -c user.email=t@t -c user.name=t commit --quiet -m 'drop B' 2>&1 | Out-Null
    & git -C $g.Clone push --quiet 2>&1 | Out-Null
    $ErrorActionPreference = $prev
    $p = Invoke-Publish $g 'Dev' 'yes'
    Assert-Equal 0 $p.Code $p.Out
    Assert-True ($p.Out -match 'removed    SL_B\.py') $p.Out
    Assert-True (-not (Test-Path (Join-Path $www 'dev\SL_B.py'))) 'retired from the served folder'
    Assert-True (@(Get-ChildItem -LiteralPath (Join-Path $g.Scratch 'backups\dev') -Recurse -Filter 'SL_B.py').Count -ge 1) 'kept in the backup'
}

Fact 'APublishWhoseDownloadsDoNotMatchPutsTheOldSetBack' {
    Clear-Channel dev
    Set-Channel dev @{ 'SL_A.py' = 'print("old a")' } -Version '2026.10.18.0500'
    $before = Sha (Join-Path $www 'dev\scripts.json')
    $g = New-PublishRig 'pubroll'
    # A second server serving an empty folder: every download back is a 404, as if NPM routed somewhere else.
    $empty = New-Dir 'empty-www'
    New-Item -ItemType Directory -Path (Join-Path $empty 'dev') -Force | Out-Null
    $p2 = Get-Random -Minimum 20000 -Maximum 40000
    $srv2 = Start-Process -FilePath python -ArgumentList @('-m', 'http.server', $p2, '--bind', '127.0.0.1', '--directory', $empty) -PassThru -WindowStyle Hidden
    try {
        Start-Sleep -Seconds 2
        $p = Invoke-Publish $g 'Dev' 'yes' -Url "http://127.0.0.1:$p2/"
        Assert-Equal 1 $p.Code $p.Out
        Assert-True ($p.Out -match 'MISMATCH: putting the old scripts back') $p.Out
        Assert-Equal 'print("old a")' ([IO.File]::ReadAllText((Join-Path $www 'dev\SL_A.py'))) 'the old script is back'
        Assert-True (-not (Test-Path (Join-Path $www 'dev\SL_B.py'))) 'the new one is gone again'
        Assert-Equal $before (Sha (Join-Path $www 'dev\scripts.json')) 'scripts.json was never touched'
    } finally { Stop-Process -Id $srv2.Id -Force -ErrorAction SilentlyContinue }
}

Fact 'PromoteCopiesTheExactDevBytesToOfficialAndTagsTheCommit' {
    Clear-Channel dev; Clear-Channel official
    $g = New-PublishRig 'promote'
    $p = Invoke-Publish $g 'Dev' 'yes'
    Assert-Equal 0 $p.Code $p.Out
    # This PC's copy changes afterwards: Promote must not care.
    Write-Text (Join-Path $g.Clone 'SL_A.py') 'print("local only, never promoted")'
    $p = Invoke-Publish $g 'Promote' 'SL_A.py|yes'
    Assert-Equal 0 $p.Code $p.Out
    $off = [IO.File]::ReadAllText((Join-Path $www 'official\scripts\scripts.json')) | ConvertFrom-Json
    Assert-Equal 'SL_A.py' ((@($off.files) | ForEach-Object { $_.name }) -join ',') 'only the one picked'
    Assert-Equal (Sha (Join-Path $www 'dev\SL_A.py')) (Sha (Join-Path $www 'official\scripts\SL_A.py')) 'the exact dev bytes'
    $dev = [IO.File]::ReadAllText((Join-Path $www 'dev\scripts.json')) | ConvertFrom-Json
    Assert-Equal $dev.version $off.promotedFrom 'it records the dev set it came from'
    $tags = @(& git -C $g.Origin tag 2>$null)
    Assert-True ($tags -contains ('official-' + $off.version)) ('tag pushed: ' + ($tags -join ','))
    $tagged = ((& git -C $g.Origin rev-list -n 1 ('official-' + $off.version)) -join '')
    Assert-Equal $dev.commit $tagged 'on the commit the dev set was published from'
    $p = Invoke-Publish $g 'Promote' 'all|no'
    Assert-Equal 3 $p.Code ('declined: ' + $p.Out)
    $p = Invoke-Publish $g 'Check'
    Assert-True ($p.Code -eq 0 -and $p.Out -match 'SL_B\.py\s+\S+\s+\S+\s+-') ('Check shows SL_B.py on dev only: ' + $p.Out)
}

# ======================================================================================================================
try { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue } catch {}
Remove-Item Env:SL_LEGION_SETTINGS -ErrorAction SilentlyContinue
$failed = @($results | Where-Object { -not $_.Passed })
Write-Host ''
Write-Host ("  {0} facts, {1} failed" -f $results.Count, $failed.Count) -ForegroundColor $(if ($failed.Count) { 'Red' } else { 'Green' })
if (-not $failed.Count) { try { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction Stop } catch { Write-Host "  (scratch left at $work)" } }
else { Write-Host "  scratch kept at $work" }
exit $failed.Count
