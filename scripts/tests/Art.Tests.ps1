#Requires -Version 5.1
# Facts for the art pipeline (cc-P152): scripts\art\ArtLib.ps1, Art.ps1 and New-ArtRequest.ps1.
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\Art.Tests.ps1
#
# Exit code is the number of failed facts. -ArtDir runs them against another copy of scripts\art (how they were seen
# failing first: a copy whose ArtLib functions only throw). Every store, key file and console folder is a temp folder;
# the API client is a fake, so no call leaves this PC. The image processing itself has its own Python tests
# (test_art_process.py); the place fact here runs it once end to end.
param([string]$ArtDir = (Join-Path $PSScriptRoot '..\art'), [string]$Spec = 'D:\UO\shard-migration\notes\console-art-spec.md')
$ErrorActionPreference = 'Stop'
$ArtDir = (Resolve-Path $ArtDir).Path
. (Join-Path $ArtDir 'ArtLib.ps1')

$results = New-Object 'System.Collections.Generic.List[object]'
function Fact {
    param([string]$Name, [scriptblock]$Body)
    if ($env:SL_FACT_FILTER -and $Name -notmatch $env:SL_FACT_FILTER) { return }
    try { & $Body; $results.Add([pscustomobject]@{ Name = $Name; Passed = $true }); Write-Host "  PASS  $Name" -ForegroundColor Green }
    catch { $results.Add([pscustomobject]@{ Name = $Name; Passed = $false }); Write-Host "  FAIL  $Name" -ForegroundColor Red; Write-Host "        $($_.Exception.Message)" -ForegroundColor Red }
}
function Assert-Equal($Expected, $Actual, [string]$Because = '') { if ($Expected -ne $Actual) { throw "expected [$Expected] but got [$Actual]. $Because" } }
function Assert-True($Condition, [string]$Because) { if (-not $Condition) { throw "not true: $Because" } }

function New-TempWorld {
    $d = Join-Path ([IO.Path]::GetTempPath()) ('art-facts-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    foreach ($s in @('store', 'golem', 'inbox', 'repo\scripts\console-art')) { New-Item -ItemType Directory -Force -Path (Join-Path $d $s) | Out-Null }
    $c = Get-ArtConfig -StoreRoot (Join-Path $d 'store') -GolemRoot (Join-Path $d 'golem') -Inbox (Join-Path $d 'inbox') -ShardRepo (Join-Path $d 'repo')
    $c.Process = Join-Path $ArtDir 'art_process.py'
    [pscustomobject]@{ Dir = $d; Cfg = $c }
}

function New-SampleRequest([string]$Id = 'sample-crystal', [string]$Kind = 'animated') {
    $slot = $(if ($Kind -eq 'animated') { New-ArtSlot -File 'crystal-green.png' -Sizes @(32, 64) -Kind animated -Frames 12 -Fps 8 -Animation 'pulse-turn' -BudgetKB 120 -Glow @(120, 255, 150) }
        else { New-ArtSlot -File 'icon-x.png' -Sizes @(32) -BudgetKB 40 })
    New-ArtRequestObject -Id $Id -Title 'A sample' -AskedBy 'P152' -Style 'Dark slate and bronze.' -Subject 'A green crystal on its point.' -Slots @($slot) `
        -Destination ([pscustomobject]@{ kind = 'console'; folder = 'scripts\console-art' })
}

# A transparent PNG with a filled circle, drawn with System.Drawing (no art file is read).
function New-SamplePng([string]$Path, [int]$Side = 256) {
    Add-Type -AssemblyName System.Drawing
    $b = New-Object Drawing.Bitmap($Side, $Side, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($b)
    $g.Clear([Drawing.Color]::Transparent)
    $g.FillEllipse((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255, 80, 200, 120))), [int]($Side * 0.3), [int]($Side * 0.2), [int]($Side * 0.4), [int]($Side * 0.6))
    $g.Dispose(); $b.Save($Path, [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

function Invoke-ArtCli([object[]]$A) {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $t = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ArtDir 'Art.ps1') @A 2>&1 | ForEach-Object { [string]$_ }) -join "`n" } finally { $ErrorActionPreference = $old }
    $i = $t.IndexOf('{')
    if ($i -lt 0) { throw ('Art.ps1 printed no JSON: ' + $t) }
    $t.Substring($i) | ConvertFrom-Json
}

Fact 'P152_ARequestFileRoundTripsAndABadOneIsRefused' {
    # One request per need, as a file outside the repos; what is saved is what is read back, and a malformed request
    # (bad id, no slots, an animated slot without fps, an unknown destination) is never written.
    $w = New-TempWorld
    $r = New-SampleRequest
    Save-ArtRequest $w.Cfg $r
    $back = Read-ArtRequest $w.Cfg 'sample-crystal'
    Assert-Equal 'new' $back.status
    Assert-Equal 'P152' $back.askedBy
    Assert-Equal 'crystal-green.png' $back.slots[0].file
    Assert-Equal 2 @($back.slots[0].sizes).Count
    Assert-Equal 64 $back.slots[0].sizes[1][0]
    Assert-Equal 12 $back.slots[0].frames
    Assert-Equal 'console' $back.destination.kind
    Assert-Equal 0 @($back.variants).Count
    Assert-Equal 1 @(Get-ArtRequests $w.Cfg 'new').Count
    $bad = $back.PSObject.Copy(); $bad.id = 'Bad Id!'
    Assert-True ((Test-ArtRequest $bad) -match 'id must be').Count 'a bad id is named'
    $threw = $false
    try { New-ArtRequestObject -Id 'x-anim' -Title 't' -AskedBy 'P1' -Subject 's' -Slots @(New-ArtSlot -File 'a.png' -Sizes @(32) -Kind animated -Frames 8) -Destination ([pscustomobject]@{ kind = 'console' }) | Out-Null } catch { $threw = $_.Exception.Message -match 'fps' }
    Assert-True $threw 'an animated slot with no fps is refused'
    $threw = $false
    try { New-ArtRequestObject -Id 'x-dest' -Title 't' -AskedBy 'P1' -Subject 's' -Slots @(New-ArtSlot -File 'a.png' -Sizes @(32)) -Destination ([pscustomobject]@{ kind = 'nowhere' }) | Out-Null } catch { $threw = $true }
    Assert-True $threw 'an unknown destination is refused'
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_TheConsoleSpecImportsAsTwentySlotRequests' {
    # notes\console-art-spec.md (P132): twenty slots, each one request, with the style paragraph, the sizes, the frames,
    # the budgets, opaque textures and their tone ranges. Read from the real spec, not a copy.
    $rs = @(ConvertFrom-ConsoleArtSpec ([IO.File]::ReadAllText($Spec)))
    Assert-Equal 20 $rs.Count
    Assert-Equal 20 @($rs | ForEach-Object { $_.slots[0].file } | Sort-Object -Unique).Count 'twenty different files'
    Assert-True ($rs[0].style -like 'Ultima Online Enhanced Client interface art*') 'the shared style paragraph'
    $by = @{}; foreach ($r in $rs) { $by[$r.slots[0].file] = $r.slots[0] }
    Assert-Equal 'animated' $by['crystal-green.png'].kind
    Assert-Equal 12 $by['crystal-amber.png'].frames
    Assert-Equal 8 $by['crystal-amber.png'].fps 'the amber row has no fps: the crystal default'
    Assert-Equal 'halo-sparks' $by['golem-running.png'].animation
    Assert-Equal 6 $by['golem-running.png'].fps
    Assert-Equal 'spinner' $by['spinner.png'].animation
    Assert-Equal 'static' $by['crystal-grey.png'].kind
    Assert-Equal 300 $by['golem-running.png'].budgetKB
    Assert-Equal 128 $by['golem-idle.png'].sizes[1][0]
    Assert-Equal 1024 $by['header-band.png'].sizes[0][0]
    Assert-Equal 64 $by['header-band.png'].sizes[0][1]
    Assert-Equal 'band' $by['header-band.png'].kind
    Assert-Equal $false $by['header-band.png'].transparent
    Assert-Equal 'tile' $by['stone.png'].kind
    Assert-Equal '26,28,32' ($by['stone.png'].toneLow -join ',')
    Assert-Equal '60,60,66' ($by['stone.png'].toneHigh -join ',')
    Assert-Equal '44,46,52' ($by['page.png'].toneLow -join ',')
    Assert-Equal $true $by['icon-dns.png'].transparent
    Assert-Equal '120,255,150' ($by['crystal-green.png'].glow -join ',')
    Assert-Equal 5 @($rs | Where-Object { $_.slots[0].kind -eq 'animated' }).Count 'three crystals, the running Golem, the spinner'
}

Fact 'P152_ApproveRejectAndPlaceFollowTheStatuses' {
    # Nothing is placed until a variant is approved; a rejection clears the approval; the next variant is v<n+1>.
    $w = New-TempWorld
    $r = New-SampleRequest
    $png = Join-Path $w.Dir 'a.png'; New-SamplePng $png
    $res = Invoke-ArtPlace $w.Cfg $r
    Assert-Equal $false $res.ok 'no approved variant, no place'
    $v1 = Add-ArtVariant $w.Cfg $r $png 'import' 'Chase'
    Assert-Equal 'v1' $v1.name
    Assert-Equal 'generated' $r.status
    Assert-True (Test-Path -LiteralPath $v1.file) 'the variant is copied into the review folder'
    Assert-True ($v1.file -like (Join-Path $w.Cfg.Review 'sample-crystal\*')) 'under review\<id>'
    Assert-Equal 'v2' (Add-ArtVariant $w.Cfg $r $png 'codex' 'Chase').name
    Deny-ArtVariant $r 'v2' 'Chase' 'make it squatter'
    Assert-Equal 'rejected' $r.status
    Assert-Equal 'make it squatter' $r.notes[-1].text
    $a = Approve-ArtVariant $r '' 'Chase'
    Assert-Equal 'v1' $a.name 'the newest variant not rejected'
    Assert-Equal 'approved' $r.status
    Assert-Equal 'v3' (Get-NextVariantName $r)
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_PlaceProcessesTheApprovedStillIntoTheConsoleFolderAndArchivesTheRest' {
    # End to end on a sample image: the strip lands under the slot's name, art.json gets its frames and fps, the 2x and
    # the variants go to the archive, the review folder is emptied, the request says placed.
    $w = New-TempWorld
    $r = New-SampleRequest
    $png = Join-Path $w.Dir 'a.png'; New-SamplePng $png
    [void](Add-ArtVariant $w.Cfg $r $png 'import' 'Chase')
    [void](Approve-ArtVariant $r 'v1' 'Chase')
    [IO.File]::WriteAllText((Join-Path $w.Cfg.ConsoleArt 'art.json'), '{ "slots": { "golem-running": { "frames": 8, "fps": 6 } } }')
    $res = Invoke-ArtPlace $w.Cfg $r
    Assert-True $res.ok ('placed: ' + $res.why)
    $placed = Join-Path $w.Cfg.ConsoleArt 'crystal-green.png'
    Assert-True (Test-Path -LiteralPath $placed) 'the strip is in the console folder'
    Add-Type -AssemblyName System.Drawing
    $img = [Drawing.Image]::FromFile($placed); $size = $img.Width.ToString() + 'x' + $img.Height; $img.Dispose()
    Assert-Equal '384x32' $size 'twelve 32 px frames side by side'
    $m = [IO.File]::ReadAllText((Join-Path $w.Cfg.ConsoleArt 'art.json')) | ConvertFrom-Json
    Assert-Equal 12 $m.slots.'crystal-green'.frames
    Assert-Equal 6 $m.slots.'golem-running'.fps 'other slots kept'
    Assert-Equal 'placed' $r.status
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $w.Cfg.Review 'sample-crystal'))) 'the review folder is emptied'
    Assert-True (@(Get-ChildItem -LiteralPath (Join-Path $w.Cfg.Archive 'sample-crystal') -Recurse -Filter 'crystal-green@64x64.png').Count -eq 1) 'the 2x is archived'
    Assert-True (Test-Path -LiteralPath (Join-Path $w.Cfg.Archive 'sample-crystal\v1-import.png')) 'the master is archived'
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_TheCostEstimateComesFromTheDocumentedPrices' {
    $e = Get-ArtCostEstimate 'gpt-image-1.5' '1024x1024' 'high'
    Assert-Equal 0.133 $e.Usd
    $e = Get-ArtCostEstimate 'gpt-image-2' '1536x1024' 'medium'
    Assert-Equal 0.041 $e.Usd
    $e = Get-ArtCostEstimate 'gpt-image-2.5-flare' '1024x1024' 'high'
    Assert-Equal 0.211 $e.Usd 'GPT Image 2.5 is estimated from GPT Image 2''s row'
    Assert-True ($e.Basis -match 'GPT Image 2') 'and says so'
    Assert-True ([bool](Get-ArtCostEstimate 'gpt-image-2.5-flare' '2048x2048' 'high').Error) 'an unpriced size has no estimate'
    Assert-True ([bool](Get-ArtCostEstimate 'gpt-image-2.5-flare' '1024x1024' 'xhigh').Error) 'an unpriced quality has no estimate'
    Assert-Equal 0.0302 (Get-ArtActualCost 'gpt-image-2.5-flare' ([pscustomobject]@{ output_tokens = 1000; input_tokens = 40 }) 0.211) 'the actual cost from the usage: 1000 x $30/M + 40 x $5/M'
    Assert-Equal 0.211 (Get-ArtActualCost 'gpt-image-2.5-flare' $null 0.211) 'no usage: the estimate is recorded'
}

Fact 'P152_TheMonthlyCapRefusesBeforeAnyCallAndAnAllowedCallIsLoggedWithoutTheKey' {
    # A fake API client counts its calls. Past the cap: refused, no call. Under it: one call, the variant in review,
    # one ledger line with the cost, and the key nowhere in the store.
    $w = New-TempWorld
    $key = 'sk-test-' + ('A1b2C3d4' * 5)
    Set-ArtApiKey $w.Cfg (ConvertTo-SecureString $key -AsPlainText -Force)
    $r = New-SampleRequest -Id 'sample-icon' -Kind static
    $png = Join-Path $w.Dir 'a.png'; New-SamplePng $png 64
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($png))
    $script:calls = 0; $script:sentKey = ''; $script:sentBody = $null
    $fake = { param($k, $body) $script:calls++; $script:sentKey = $k; $script:sentBody = $body; [pscustomobject]@{ data = @([pscustomobject]@{ b64_json = $b64 }); usage = [pscustomobject]@{ output_tokens = 7000; input_tokens = 100 } } }
    $now = Get-Date
    Add-ArtLine $w.Cfg.Ledger ([ordered]@{ at = $now.ToString('yyyy-MM-ddTHH:mm:sszzz'); requestId = 'earlier'; costUsd = 9.9 })
    Add-ArtLine $w.Cfg.Ledger ([ordered]@{ at = $now.AddMonths(-1).ToString('yyyy-MM-ddTHH:mm:sszzz'); requestId = 'last-month'; costUsd = 50 })
    $res = Invoke-ArtApiGenerate $w.Cfg $r '' 'Chase' $fake $now
    Assert-Equal $false $res.ok
    Assert-True ($res.why -match 'cap') ('the reason names the cap: ' + $res.why)
    Assert-Equal 0 $script:calls 'refused before any call'
    Assert-Equal 9.9 $res.monthUsd 'last month does not count'
    [IO.File]::WriteAllText($w.Cfg.Ledger, '')
    $res = Invoke-ArtApiGenerate $w.Cfg $r 'brighter' 'Chase' $fake $now
    Assert-True $res.ok $res.why
    Assert-Equal 1 $script:calls
    Assert-Equal $key $script:sentKey 'the stored key reaches the client'
    Assert-Equal 'transparent' $script:sentBody.background 'a transparent slot asks for a transparent background'
    Assert-Equal 'gpt-image-2.5-flare' $script:sentBody.model
    Assert-Equal 1 $script:sentBody.n
    Assert-Equal 0.2105 $res.variant.costUsd '7000 x $30/M + 100 x $5/M'
    Assert-True (Test-Path -LiteralPath $res.variant.file) 'the result is in review'
    $led = @(Read-ArtLedger $w.Cfg.Ledger)
    Assert-Equal 1 $led.Count
    Assert-Equal 'sample-icon' $led[0].requestId
    Assert-Equal 'high' $led[0].quality
    $failing = { param($k, $body) throw ('401 Unauthorized: incorrect API key provided: ' + $k) }
    $res = Invoke-ArtApiGenerate $w.Cfg $r '' 'Chase' $failing $now
    Assert-Equal $false $res.ok
    Assert-True ($res.why -notmatch [regex]::Escape($key)) 'the error shown has no key'
    foreach ($f in @(Get-ChildItem -LiteralPath $w.Dir -Recurse -File | Where-Object { $_.Extension -in '.json', '.jsonl', '.txt', '.xml' })) {
        Assert-True (-not ([IO.File]::ReadAllText($f.FullName).Contains($key))) ('the key in clear in ' + $f.Name)
    }
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_TheKeyIsStoredWithDpapiAndNeverInClear' {
    $w = New-TempWorld
    $key = 'sk-proj-' + ('Zx9Yw8Vu7' * 6)
    Set-ArtApiKey $w.Cfg (ConvertTo-SecureString $key -AsPlainText -Force)
    $raw = [IO.File]::ReadAllText($w.Cfg.KeyFile)
    Assert-True (-not $raw.Contains($key)) 'not in clear'
    Assert-True (-not $raw.Contains([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($key)))) 'not as base64 (UTF-8)'
    Assert-True (-not $raw.Contains([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($key)))) 'not as base64 (UTF-16)'
    Assert-True ($raw -match '<SS N="Password">[0-9a-f]+</SS>') 'a DPAPI SecureString'
    Assert-Equal $key (Get-ArtApiKeyPlain $w.Cfg) 'read back for the call'
    Assert-Equal 'error: sk-[redacted] and [key]' (Remove-ArtSecret ('error: sk-abcdefghijklmnop and ' + $key) $key)
    $threw = $false
    try { Set-ArtApiKey $w.Cfg (ConvertTo-SecureString 'short' -AsPlainText -Force) } catch { $threw = $true }
    Assert-True $threw 'a too-short key is refused'
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_GenerateWithoutGoGeneratesNothing' {
    # Only Chase's click passes -Go. Without it, generate shows the estimate (the API price and the cap, and the Codex
    # usage when Codex.ps1 is there) and leaves the request as it was.
    $w = New-TempWorld
    Save-ArtRequest $w.Cfg (New-SampleRequest)
    $o = Invoke-ArtCli @('generate', '-Id', 'sample-crystal', '-Backend', 'api', '-StoreRoot', $w.Cfg.StoreRoot, '-GolemRoot', (Join-Path $w.Dir 'golem'), '-Inbox', $w.Cfg.Inbox, '-ShardRepo', $w.Cfg.ShardRepo)
    Assert-True $o.ok $o.why
    Assert-True ($o.why -match 'estimate only') $o.why
    Assert-Equal 0.211 $o.estimate.api.estimateUsd
    Assert-Equal $false $o.estimate.api.keyStored
    Assert-Equal 10 $o.estimate.api.capUsd
    Assert-Equal 0 @((Read-ArtRequest $w.Cfg 'sample-crystal').variants).Count
    Assert-Equal 'new' (Read-ArtRequest $w.Cfg 'sample-crystal').status
    $o = Invoke-ArtCli @('generate', '-Id', 'sample-crystal', '-Backend', 'api', '-Go', '-StoreRoot', $w.Cfg.StoreRoot, '-GolemRoot', (Join-Path $w.Dir 'golem'), '-Inbox', $w.Cfg.Inbox, '-ShardRepo', $w.Cfg.ShardRepo)
    Assert-Equal $false $o.ok 'with -Go and no key stored: refused'
    Assert-True ($o.why -match 'set-key') $o.why
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_TheCommandsListImportApproveAndRejectInJson' {
    # The interface the web console will call: each command one JSON object; import takes a file from the inbox.
    $w = New-TempWorld
    Save-ArtRequest $w.Cfg (New-SampleRequest -Id 'sample-icon' -Kind static)
    New-SamplePng (Join-Path $w.Cfg.Inbox 'made-in-chatgpt.png') 128
    $common = @('-StoreRoot', $w.Cfg.StoreRoot, '-GolemRoot', (Join-Path $w.Dir 'golem'), '-Inbox', $w.Cfg.Inbox, '-ShardRepo', $w.Cfg.ShardRepo)
    $o = Invoke-ArtCli (@('inbox') + $common)
    Assert-Equal 'made-in-chatgpt.png' $o.files[0].name
    $o = Invoke-ArtCli (@('import', '-Id', 'sample-icon', '-File', 'made-in-chatgpt.png') + $common)
    Assert-True $o.ok $o.why
    Assert-True (Test-Path -LiteralPath $o.preview) 'a review sheet is drawn'
    $o = Invoke-ArtCli (@('list') + $common)
    Assert-Equal 'generated' $o.requests[0].status
    Assert-True ($o.requests[0].thumbnail -like '*.preview.png') 'the list carries the thumbnail'
    $o = Invoke-ArtCli (@('prompt', '-Id', 'sample-icon', '-Note', 'more gold') + $common)
    Assert-True ($o.prompt -match 'Dark slate and bronze\.' -and $o.prompt -match 'more gold' -and $o.prompt -match 'icon-x\.png: 32x32') $o.prompt
    $o = Invoke-ArtCli (@('reject', '-Id', 'sample-icon', '-Note', 'too busy') + $common)
    Assert-True $o.ok $o.why
    $o = Invoke-ArtCli (@('approve', '-Id', 'sample-icon', '-Variant', 'v1') + $common)
    Assert-True $o.ok $o.why
    Assert-Equal 'approved' (Read-ArtRequest $w.Cfg 'sample-icon').status
    $o = Invoke-ArtCli (@('nonsense') + $common)
    Assert-Equal $false $o.ok
    Assert-True (@([IO.File]::ReadAllLines($w.Cfg.Log)).Count -ge 3) 'each action is in art-log.jsonl'
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_NewArtRequestFilesOneAndRefusesADuplicate' {
    $w = New-TempWorld
    $h = Join-Path $ArtDir 'New-ArtRequest.ps1'
    $run = {
        $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        try {
            $t = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ("& '" + $h + "' -Id 'p999-badge' -Title 'A badge' -AskedBy P999 -Subject 'A small badge.' -Slots @(@{ file = 'badge.png'; sizes = @(32, 64); budgetKB = 40 }) -Destination @{ kind = 'web'; folder = 'D:\UO\web' } -StoreRoot '" + $w.Cfg.StoreRoot + "'") 2>&1 | ForEach-Object { [string]$_ }) -join "`n"
        } finally { $ErrorActionPreference = $old }
        $t.Substring($t.IndexOf('{')) | ConvertFrom-Json
    }
    $o = & $run
    Assert-True $o.ok $o.why
    Assert-Equal 'new' (Read-ArtRequest $w.Cfg 'p999-badge').status
    Assert-Equal 64 (Read-ArtRequest $w.Cfg 'p999-badge').slots[0].sizes[1][1]
    $o = & $run
    Assert-Equal $false $o.ok
    Assert-True ($o.why -match 'already exists') $o.why
    Remove-Item -LiteralPath $w.Dir -Recurse -Force
}

Fact 'P152_TheConsoleManifestKeepsOtherSlots' {
    $t = Set-ConsoleArtManifestSlot '{ "slots": { "spinner": { "frames": 8, "fps": 10 } } }' 'crystal-red' (New-ArtSlot -File 'crystal-red.png' -Sizes @(32) -Kind animated -Frames 12 -Fps 8)
    $j = $t | ConvertFrom-Json
    Assert-Equal 8 $j.slots.spinner.frames
    Assert-Equal 12 $j.slots.'crystal-red'.frames
    $j = (Set-ConsoleArtManifestSlot $t 'spinner' (New-ArtSlot -File 'spinner.png' -Sizes @(24))) | ConvertFrom-Json
    Assert-True (-not ($j.slots.PSObject.Properties.Name -contains 'spinner')) 'a static slot drops its entry'
    Assert-Equal 12 ((Set-ConsoleArtManifestSlot '' 'crystal-red' (New-ArtSlot -File 'crystal-red.png' -Sizes @(32) -Kind animated -Frames 12 -Fps 8)) | ConvertFrom-Json).slots.'crystal-red'.frames 'an empty manifest'
}

Fact 'P152_TheCodexPromptAsksForOneImageAndNothingElse' {
    $t = Get-ArtCodexPrompt (New-SampleRequest) 'squatter' $true
    Assert-True ($t -match '\$imagegen' -and $t -match 'image_gen') 'the built-in tool'
    Assert-True ($t -match 'exactly one image') 'one image'
    Assert-True ($t -match 'Do not run any shell commands') 'no commands'
    Assert-True ($t -match 'Image 1 \(attached\) is the previous version') 'regenerate shows Codex the last variant'
    Assert-True ($t -match 'transparent') 'transparency'
    Assert-True ($t -match 'Change from the last version: squatter') 'the note'
}

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ''
Write-Host ('{0} facts, {1} passed, {2} failed' -f $results.Count, ($results.Count - $failed), $failed)
exit $failed
