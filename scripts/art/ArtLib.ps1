#Requires -Version 5.1
# ArtLib.ps1 (cc-P152): the art request pipeline's core, dot-sourced by Art.ps1, New-ArtRequest.ps1, the console and
# the tests. Headless on purpose: the web console (D:\UO\overseer\web-console-plan.md) calls Art.ps1, which is a thin
# command layer over these functions. ASCII only.
#
# The store lives outside both repos (art masters are large and never go in git):
#   D:\UO\art\requests\<id>.json      one request per need
#   D:\UO\art\review\<id>\v<n>-<backend>.png (+ .preview.png)   every generated or imported variant, until placed
#   D:\UO\art\archive\<id>\           the approved master, every variant and the 2x outputs once placed
#   D:\UO\art\api-ledger.jsonl        one line per OpenAI API call: request id, model, size, quality, cost (never the key)
#   D:\UO\art\art-log.jsonl           one line per pipeline action (generate, import, approve, reject, place)
#   D:\UO\art-inbox\                  anything Chase made elsewhere, to Import
# The OpenAI API key is a DPAPI file (current user), D:\UO\golem\openai-key.xml, like the Golem's SMTP credential.
#
# Nothing here generates on its own: Generate is Chase's click (Art.ps1 generate), and nothing is placed until a
# variant is approved (Approve), then Place processes it into its destination.

$script:ArtInv = [Globalization.CultureInfo]::InvariantCulture
$script:ArtStatuses = @('new', 'generated', 'approved', 'rejected', 'placed')

function Get-ArtConfig {
    param([string]$StoreRoot = 'D:\UO\art', [string]$GolemRoot = 'D:\UO\golem', [string]$Inbox = 'D:\UO\art-inbox',
        [string]$ShardRepo = 'D:\ShatteredLegacy')
    [pscustomobject]@{
        StoreRoot = $StoreRoot; Requests = (Join-Path $StoreRoot 'requests'); Review = (Join-Path $StoreRoot 'review')
        Archive = (Join-Path $StoreRoot 'archive'); Ledger = (Join-Path $StoreRoot 'api-ledger.jsonl'); Log = (Join-Path $StoreRoot 'art-log.jsonl')
        Inbox = $Inbox; KeyFile = (Join-Path $GolemRoot 'openai-key.xml'); SettingsFile = (Join-Path $GolemRoot 'settings.json')
        CodexScript = (Join-Path $GolemRoot 'Codex.ps1'); ShardRepo = $ShardRepo
        ConsoleArt = (Join-Path $ShardRepo 'scripts\console-art'); Process = (Join-Path $PSScriptRoot 'art_process.py')
        Python = 'python'
    }
}

function Get-ArtProp($o, [string]$name, $default) {
    if ($null -ne $o -and $o.PSObject.Properties.Name -contains $name -and $null -ne $o.$name) { return $o.$name }
    return $default
}

function Get-ArtNow { (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz', $script:ArtInv) }

function Write-ArtText([string]$Path, [string]$Text) {
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $tmp = $Path + '.tmp'
    [IO.File]::WriteAllText($tmp, $Text, (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $tmp -Destination $Path -Force
}

function Add-ArtLine([string]$Path, $Obj) {
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::AppendAllText($Path, (($Obj | ConvertTo-Json -Depth 6 -Compress) + "`n"), (New-Object Text.UTF8Encoding($false)))
}

# ---- requests ---------------------------------------------------------------------------------------------------

# A slot: one output file. Sizes are [w, h] pairs (a square slot gives [n, n]); the first is the one placed under the
# slot's file name, the rest are kept in the archive (the console shows the first; P132's spec asks for both).
function New-ArtSlot {
    param([Parameter(Mandatory)][string]$File, [Parameter(Mandatory)][object[]]$Sizes, [ValidateSet('static', 'animated', 'tile', 'band')][string]$Kind = 'static',
        [int]$Frames = 1, [double]$Fps = 0, [string]$Animation = '', [bool]$Transparent = $true, [int]$BudgetKB = 100, [int]$Margin = 2,
        [int[]]$Glow = $null, [int[]]$ToneLow = $null, [int[]]$ToneHigh = $null)
    $sz = @(foreach ($s in $Sizes) { if ($s -is [array]) { , @([int]$s[0], [int]$s[1]) } else { , @([int]$s, [int]$s) } })
    [pscustomobject]@{ file = $File; sizes = $sz; kind = $Kind; frames = $Frames; fps = $Fps; animation = $Animation
        transparent = $Transparent; budgetKB = $BudgetKB; margin = $Margin; glow = $Glow; toneLow = $ToneLow; toneHigh = $ToneHigh }
}

function New-ArtRequestObject {
    param([Parameter(Mandatory)][string]$Id, [Parameter(Mandatory)][string]$Title, [Parameter(Mandatory)][string]$AskedBy,
        [string]$Style = '', [Parameter(Mandatory)][string]$Subject, [Parameter(Mandatory)][object[]]$Slots,
        [Parameter(Mandatory)]$Destination, [string]$Status = 'new', [string]$Note = '')
    $now = Get-ArtNow
    $r = [pscustomobject]@{ schema = 1; id = $Id; title = $Title; askedBy = $AskedBy; created = $now; updated = $now; status = $Status
        style = $Style; subject = $Subject; slots = @($Slots); destination = $Destination
        notes = @($(if ($Note) { [pscustomobject]@{ at = $now; by = $AskedBy; text = $Note } }))
        variants = @(); approved = ''; placed = @()
        history = @([pscustomobject]@{ at = $now; status = $Status; by = $AskedBy; why = 'filed' }) }
    $bad = Test-ArtRequest $r
    if ($bad.Count) { throw ('bad art request: ' + ($bad -join '; ')) }
    $r
}

# Every reason a request is not well formed ('' list when it is).
function Test-ArtRequest($r) {
    $why = New-Object 'System.Collections.Generic.List[string]'
    if (-not $r) { $why.Add('no request'); return , $why.ToArray() }
    if ([string]$r.id -notmatch '^[a-z0-9][a-z0-9-]{1,63}$') { $why.Add('id must be lower-case letters, digits and dashes: ' + $r.id) }
    if (-not $r.title) { $why.Add('no title') }
    if (-not $r.askedBy) { $why.Add('no askedBy (a prompt number such as P147, or Chase)') }
    if (-not $r.subject) { $why.Add('no subject prompt') }
    if ($script:ArtStatuses -notcontains [string]$r.status) { $why.Add('status must be one of ' + ($script:ArtStatuses -join ', ') + ': ' + $r.status) }
    if (-not @($r.slots).Count) { $why.Add('no output slots') }
    foreach ($s in @($r.slots)) {
        if (-not $s.file) { $why.Add('a slot has no file name'); continue }
        if (-not @($s.sizes).Count) { $why.Add($s.file + ': no sizes') }
        if ($s.kind -eq 'animated' -and ([int]$s.frames -lt 2 -or [double]$s.fps -le 0)) { $why.Add($s.file + ': an animated slot needs frames (2 or more) and fps') }
        if ([int]$s.budgetKB -le 0) { $why.Add($s.file + ': no byte budget') }
    }
    $d = $r.destination
    if (-not $d -or @('console', 'game-gump', 'game-item', 'game-anim', 'web') -notcontains [string]$d.kind) { $why.Add('destination.kind must be console, game-gump, game-item, game-anim or web') }
    , $why.ToArray()
}

function Get-ArtRequestPath($Cfg, [string]$Id) { Join-Path $Cfg.Requests ($Id + '.json') }

function Save-ArtRequest($Cfg, $Req) {
    $bad = Test-ArtRequest $Req
    if ($bad.Count) { throw ('not saved, bad art request: ' + ($bad -join '; ')) }
    $Req.updated = Get-ArtNow
    Write-ArtText (Get-ArtRequestPath $Cfg $Req.id) ($Req | ConvertTo-Json -Depth 8)
}

function Read-ArtRequest($Cfg, [string]$Id) {
    $p = Get-ArtRequestPath $Cfg $Id
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw ('no art request ' + $Id + ' (' + $p + ')') }
    $r = [IO.File]::ReadAllText($p) | ConvertFrom-Json
    # ConvertFrom-Json leaves one-element arrays as arrays but turns [] into $null on 5.1: put the lists back.
    foreach ($k in @('slots', 'notes', 'variants', 'placed', 'history')) { if ($null -eq $r.$k) { $r.$k = @() } else { $r.$k = @($r.$k) } }
    $r
}

function Get-ArtRequests($Cfg, [string]$Status = '') {
    if (-not (Test-Path -LiteralPath $Cfg.Requests)) { return @() }
    $all = @(Get-ChildItem -LiteralPath $Cfg.Requests -Filter '*.json' -File | ForEach-Object { try { Read-ArtRequest $Cfg $_.BaseName } catch { $null } } | Where-Object { $_ })
    if ($Status) { $all = @($all | Where-Object { $_.status -eq $Status }) }
    $order = @{ generated = 0; new = 1; approved = 2; rejected = 3; placed = 4 }
    @($all | Sort-Object @{ Expression = { $order[[string]$_.status] } }, @{ Expression = { $_.id } })
}

function Set-ArtStatus($Req, [string]$Status, [string]$By, [string]$Why) {
    if ($script:ArtStatuses -notcontains $Status) { throw ('no status ' + $Status) }
    $Req.status = $Status
    $Req.history = @($Req.history) + [pscustomobject]@{ at = (Get-ArtNow); status = $Status; by = $By; why = $Why }
}

function Add-ArtNote($Req, [string]$By, [string]$Text) {
    if ($Text) { $Req.notes = @($Req.notes) + [pscustomobject]@{ at = (Get-ArtNow); by = $By; text = $Text } }
}

# The next variant name: v1, v2 ... over everything the request has had.
function Get-NextVariantName($Req) {
    $n = 0
    foreach ($v in @($Req.variants)) { if ([string]$v.name -match '^v(\d+)') { $n = [Math]::Max($n, [int]$Matches[1]) } }
    'v' + ($n + 1)
}

# Records a result file as a new variant: copied into review\<id>\<v>-<backend>.png, status generated.
function Add-ArtVariant($Cfg, $Req, [string]$SourceFile, [string]$Backend, [string]$By, [string]$Prompt = '', [string]$Note = '', $Cost = $null, [string]$Source = '') {
    if (-not (Test-Path -LiteralPath $SourceFile -PathType Leaf)) { throw ('no such file ' + $SourceFile) }
    $ext = [IO.Path]::GetExtension($SourceFile).ToLowerInvariant()
    if (@('.png', '.webp', '.jpg', '.jpeg', '.gif') -notcontains $ext) { throw ('not an image: ' + $SourceFile) }
    $name = Get-NextVariantName $Req
    $dir = Join-Path $Cfg.Review $Req.id
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $dest = Join-Path $dir ($name + '-' + $Backend + $ext)
    Copy-Item -LiteralPath $SourceFile -Destination $dest -Force
    $v = [pscustomobject]@{ name = $name; file = $dest; backend = $Backend; at = (Get-ArtNow); by = $By; source = $(if ($Source) { $Source } else { $SourceFile })
        prompt = $Prompt; note = $Note; costUsd = $Cost; verdict = '' }
    $Req.variants = @($Req.variants) + $v
    Set-ArtStatus $Req 'generated' $By ($name + ' from ' + $Backend)
    $v
}

function Get-ArtVariant($Req, [string]$Name) {
    $v = @($Req.variants | Where-Object { $_.name -eq $Name })
    if (-not $v.Count) { throw ('request ' + $Req.id + ' has no variant ' + $Name + ' (it has: ' + ((@($Req.variants) | ForEach-Object { $_.name }) -join ', ') + ')') }
    $v[0]
}

function Approve-ArtVariant($Req, [string]$Name, [string]$By) {
    if (-not $Name) { $last = @($Req.variants | Where-Object { $_.verdict -ne 'rejected' }); if ($last.Count) { $Name = $last[-1].name } }
    $v = Get-ArtVariant $Req $Name
    foreach ($x in @($Req.variants)) { if ($x.verdict -eq 'approved') { $x.verdict = '' } }
    $v.verdict = 'approved'
    $Req.approved = $v.name
    Set-ArtStatus $Req 'approved' $By ($v.name + ' approved')
    $v
}

function Deny-ArtVariant($Req, [string]$Name, [string]$By, [string]$Note) {
    if ($Name) { (Get-ArtVariant $Req $Name).verdict = 'rejected' }
    elseif (@($Req.variants).Count) { $Req.variants[-1].verdict = 'rejected' }
    if ($Req.approved -and $Name -eq $Req.approved) { $Req.approved = '' }
    Add-ArtNote $Req $By $Note
    Set-ArtStatus $Req 'rejected' $By $(if ($Note) { $Note } else { 'rejected' })
}

# ---- prompts ----------------------------------------------------------------------------------------------------

function Get-ArtSlotLine($s) {
    $sz = (@($s.sizes) | ForEach-Object { '{0}x{1}' -f $_[0], $_[1] }) -join ' and '
    $what = $(switch ([string]$s.kind) { 'animated' { 'animated (' + $s.frames + ' frames at ' + $s.fps + ' fps, built from one still)' } 'tile' { 'a seamless tile' } 'band' { 'a band that tiles left to right' } default { 'static' } })
    $bg = $(if ($s.transparent) { 'transparent background' } else { 'opaque' })
    ($s.file + ': ' + $sz + ' px, ' + $what + ', ' + $bg + ', under ' + $s.budgetKB + ' KB')
}

# The text to paste into ChatGPT (Copy prompt): the style paragraph, the subject, and what the result must be.
function Get-ArtPrompt($Req, [string]$Note = '') {
    $b = New-Object Text.StringBuilder
    if ($Req.style) { [void]$b.AppendLine($Req.style.Trim()); [void]$b.AppendLine() }
    [void]$b.AppendLine($Req.subject.Trim())
    if ($Note) { [void]$b.AppendLine(); [void]$b.AppendLine('Change from the last version: ' + $Note.Trim()) }
    [void]$b.AppendLine()
    $t = @($Req.slots | Where-Object { $_.transparent }).Count
    $big = $(if (@($Req.slots | Where-Object { $_.kind -eq 'band' }).Count) { 'Make it large and wide (about 1536x512 or wider)' } else { 'Make it large and square (1024x1024 or bigger)' })
    [void]$b.AppendLine($big + ': it is cut down to the sizes below afterwards. One image.' + $(if ($t) { ' The background must be truly transparent (alpha), with nothing opaque outside the object.' } else { '' }))
    foreach ($s in @($Req.slots)) { [void]$b.AppendLine('- ' + (Get-ArtSlotLine $s)) }
    $b.ToString().TrimEnd()
}

# What Codex is told: the same prompt, through its built-in image_gen tool, and nothing else.
function Get-ArtCodexPrompt($Req, [string]$Note = '', [bool]$HasReference = $false) {
    $t = @($Req.slots | Where-Object { $_.transparent }).Count
    $lines = @(
        'Use the $imagegen skill with the built-in image_gen tool to generate exactly one image, then stop.'
        $(if ($HasReference) { 'Image 1 (attached) is the previous version: keep its design and change only what the note below asks.' } else { '' })
        ''
        (Get-ArtPrompt $Req $Note)
        ''
        $(if ($t) { 'Ask image_gen for a genuinely transparent background and keep the alpha.' } else { 'The image is opaque, edge to edge.' })
        'Do not run any shell commands, do not write or copy any files, and do not ask questions.'
        'When it is done, reply with one line: the full path of the generated image file.'
    )
    ($lines | Where-Object { $null -ne $_ }) -join "`n"
}

# ---- the API path: cost, cap, key -------------------------------------------------------------------------------

# Per-image USD from OpenAI's image generation guide (developers.openai.com/api/docs/guides/image-generation, read
# 2026-10-19): GPT Image 2, 1.5, 1 and 1 Mini, at 1024x1024 / 1024x1536 / 1536x1024. GPT Image 2.5 has no per-image
# table there ("Token rates match GPT Image 2"; it "can use different token counts"), so its estimate is GPT Image 2's
# row, said as such. Output image tokens per 1M from the pricing page, for the actual cost from a call's usage.
$script:ArtImagePrices = @{
    'gpt-image-2'      = @{ low = @(0.006, 0.005, 0.005); medium = @(0.053, 0.041, 0.041); high = @(0.211, 0.165, 0.165) }
    'gpt-image-1.5'    = @{ low = @(0.009, 0.013, 0.013); medium = @(0.034, 0.05, 0.05); high = @(0.133, 0.2, 0.2) }
    'gpt-image-1'      = @{ low = @(0.011, 0.016, 0.016); medium = @(0.042, 0.063, 0.063); high = @(0.167, 0.25, 0.25) }
    'gpt-image-1-mini' = @{ low = @(0.005, 0.006, 0.006); medium = @(0.011, 0.015, 0.015); high = @(0.036, 0.052, 0.052) }
}
$script:ArtTokenRates = @{   # USD per 1M tokens: image output, text input
    'gpt-image-2' = @(30.0, 5.0); 'gpt-image-2.5-flare' = @(30.0, 5.0); 'gpt-image-2.5-sunburst' = @(30.0, 5.0)
    'gpt-image-1.5' = @(32.0, 5.0); 'gpt-image-1' = @(40.0, 5.0); 'gpt-image-1-mini' = @(8.0, 2.0)
}

function Get-ArtApiSettings($Cfg) {
    $s = [pscustomobject]@{ artApiModel = 'gpt-image-2.5-flare'; artApiQuality = 'high'; artApiSize = '1024x1024'; artApiMonthlyCapUsd = 10.0 }
    if ($Cfg -and (Test-Path -LiteralPath $Cfg.SettingsFile -PathType Leaf)) {
        try { $f = [IO.File]::ReadAllText($Cfg.SettingsFile) | ConvertFrom-Json; foreach ($k in @($s.PSObject.Properties.Name)) { if ($f.PSObject.Properties.Name -contains $k -and $null -ne $f.$k) { $s.$k = $f.$k } } } catch { }
    }
    $s
}

# The estimate before a call: Usd, Basis (where the figure comes from), or Error when no documented price fits.
function Get-ArtCostEstimate([string]$Model, [string]$Size, [string]$Quality) {
    $col = @{ '1024x1024' = 0; '1024x1536' = 1; '1536x1024' = 2 }
    if (-not $col.ContainsKey($Size)) { return [pscustomobject]@{ Usd = $null; Basis = ''; Error = ('no documented price for size ' + $Size + ' (only 1024x1024, 1024x1536, 1536x1024 are priced)') } }
    $table = $Model; $basis = $Model + ' ' + $Quality + ' ' + $Size + ', OpenAI image generation guide'
    if ($Model -like 'gpt-image-2.5-*') { $table = 'gpt-image-2'; $basis = $Model + ' estimated from GPT Image 2''s table (' + $Quality + ' ' + $Size + '): the same token rates, and OpenAI says 2.5 can use different token counts' }
    elseif ($Model -like 'gpt-image-2-*') { $table = 'gpt-image-2' }
    if (-not $script:ArtImagePrices.ContainsKey($table)) { return [pscustomobject]@{ Usd = $null; Basis = ''; Error = ('no documented per-image price for ' + $Model) } }
    $row = $script:ArtImagePrices[$table][$Quality]
    if (-not $row) { return [pscustomobject]@{ Usd = $null; Basis = ''; Error = ('no documented price for quality ' + $Quality + ' (low, medium and high are priced)') } }
    [pscustomobject]@{ Usd = [double]$row[$col[$Size]]; Basis = $basis; Error = '' }
}

# The actual cost from a call's usage block (output tokens at the image output rate, input at the text rate).
function Get-ArtActualCost([string]$Model, $Usage, $Estimate) {
    $rates = $script:ArtTokenRates[$Model]
    if (-not $rates -and $Model -like 'gpt-image-2*') { $rates = $script:ArtTokenRates['gpt-image-2'] }
    if ($Usage -and $rates -and $null -ne (Get-ArtProp $Usage 'output_tokens' $null)) {
        return [Math]::Round(([double]$Usage.output_tokens * $rates[0] + [double](Get-ArtProp $Usage 'input_tokens' 0) * $rates[1]) / 1e6, 4)
    }
    $Estimate
}

function Read-ArtLedger([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }
    @([IO.File]::ReadAllLines($Path) | Where-Object { $_.Trim() } | ForEach-Object { try { $_ | ConvertFrom-Json } catch { $null } } | Where-Object { $_ })
}

function Get-ArtMonthTotal($Ledger, [datetime]$Now) {
    $m = $Now.ToString('yyyy-MM', $script:ArtInv)
    $sum = 0.0
    foreach ($e in @($Ledger)) { if ([string]$e.at -like ($m + '*') -and $null -ne $e.costUsd) { $sum += [double]$e.costUsd } }
    [Math]::Round($sum, 4)
}

# The cap verdict before an API call: refused when this month's calls plus this estimate would pass the cap.
function Test-ArtApiCap([double]$MonthTotal, $Estimate, [double]$Cap) {
    if ($Estimate.Error) { return [pscustomobject]@{ Allowed = $false; Why = ('refused: ' + $Estimate.Error + ', so the cost cannot be shown before the call') } }
    $after = [Math]::Round($MonthTotal + [double]$Estimate.Usd, 4)
    if ($after -gt $Cap) { return [pscustomobject]@{ Allowed = $false; Why = ('refused: this month''s API spend is ${0:0.000}; this call (about ${1:0.000}) would take it to ${2:0.000}, past the ${3:0.00} monthly cap (artApiMonthlyCapUsd)' -f $MonthTotal, $Estimate.Usd, $after, $Cap) } }
    [pscustomobject]@{ Allowed = $true; Why = ('about ${0:0.000} ({1}); ${2:0.000} spent this month of the ${3:0.00} cap' -f $Estimate.Usd, $Estimate.Basis, $MonthTotal, $Cap) }
}

# The key: a PSCredential exported with DPAPI (only this Windows login on this PC can read it). Never printed, logged
# or written in clear; only Get-ArtApiKeyPlain hands it to the HTTP call.
function Set-ArtApiKey($Cfg, [Security.SecureString]$Key) {
    if (-not $Key -or $Key.Length -lt 20) { throw 'that does not look like an OpenAI API key (too short)' }
    $dir = Split-Path -Parent $Cfg.KeyFile
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    (New-Object Management.Automation.PSCredential('openai-api', $Key)) | Export-Clixml -LiteralPath $Cfg.KeyFile
}

function Test-ArtApiKeyStored($Cfg) { Test-Path -LiteralPath $Cfg.KeyFile -PathType Leaf }

function Get-ArtApiKeyPlain($Cfg) {
    if (-not (Test-ArtApiKeyStored $Cfg)) { throw 'no OpenAI API key stored: run Art.ps1 set-key (it asks for the key and keeps it in a DPAPI file)' }
    (Import-Clixml -LiteralPath $Cfg.KeyFile).GetNetworkCredential().Password
}

# Takes the key out of any text that might carry it (an error message from a failed call).
function Remove-ArtSecret([string]$Text, [string]$Secret) {
    if (-not $Text) { return $Text }
    if ($Secret) { $Text = $Text.Replace($Secret, '[key]') }
    $Text -replace 'sk-[A-Za-z0-9_\-]{8,}', 'sk-[redacted]'
}

# The real API client: POST /v1/images/generations. The tests pass their own client instead.
$script:ArtApiClient = {
    param([string]$Key, $Body)
    $h = @{ Authorization = ('Bearer ' + $Key) }
    Invoke-RestMethod -Method Post -Uri 'https://api.openai.com/v1/images/generations' -Headers $h -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 4) -TimeoutSec 300
}
$script:ArtApiTestClient = {
    param([string]$Key)
    Invoke-RestMethod -Method Get -Uri 'https://api.openai.com/v1/models' -Headers @{ Authorization = ('Bearer ' + $Key) } -TimeoutSec 30
}

# One API generation for a request. Refuses past the cap; writes the variant to review; logs the call (no key).
function Invoke-ArtApiGenerate($Cfg, $Req, [string]$Note = '', [string]$By = 'Chase', $Client = $script:ArtApiClient, [datetime]$Now = (Get-Date)) {
    $set = Get-ArtApiSettings $Cfg
    $model = [string]$set.artApiModel; $size = [string]$set.artApiSize; $quality = [string]$set.artApiQuality
    $est = Get-ArtCostEstimate $model $size $quality
    $month = Get-ArtMonthTotal (Read-ArtLedger $Cfg.Ledger) $Now
    $cap = Test-ArtApiCap $month $est ([double]$set.artApiMonthlyCapUsd)
    if (-not $cap.Allowed) { return [pscustomobject]@{ ok = $false; why = $cap.Why; estimate = $est; monthUsd = $month } }
    $key = Get-ArtApiKeyPlain $Cfg
    $transparent = @($Req.slots | Where-Object { $_.transparent }).Count -gt 0
    $body = [ordered]@{ model = $model; prompt = (Get-ArtPrompt $Req $Note); size = $size; quality = $quality; n = 1; output_format = 'png'; background = $(if ($transparent) { 'transparent' } else { 'opaque' }) }
    try { $resp = & $Client $key $body }
    catch {
        $msg = Remove-ArtSecret $_.Exception.Message $key
        Add-ArtLine $Cfg.Ledger ([ordered]@{ at = $Now.ToString('yyyy-MM-ddTHH:mm:sszzz', $script:ArtInv); requestId = $Req.id; model = $model; size = $size; quality = $quality; estimateUsd = $est.Usd; costUsd = 0; ok = $false; error = $msg })
        return [pscustomobject]@{ ok = $false; why = ('the API call failed: ' + $msg); estimate = $est; monthUsd = $month }
    }
    $b64 = $null
    if ($resp -and $resp.data) { $b64 = @($resp.data)[0].b64_json }
    $cost = Get-ArtActualCost $model (Get-ArtProp $resp 'usage' $null) $est.Usd
    Add-ArtLine $Cfg.Ledger ([ordered]@{ at = $Now.ToString('yyyy-MM-ddTHH:mm:sszzz', $script:ArtInv); requestId = $Req.id; model = $model; size = $size; quality = $quality; background = $body.background; estimateUsd = $est.Usd; costUsd = $cost; ok = [bool]$b64; usage = (Get-ArtProp $resp 'usage' $null) })
    if (-not $b64) { return [pscustomobject]@{ ok = $false; why = 'the API answered without an image'; estimate = $est; monthUsd = $month } }
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('art-api-' + [guid]::NewGuid().ToString('N') + '.png')
    [IO.File]::WriteAllBytes($tmp, [Convert]::FromBase64String($b64))
    try { $v = Add-ArtVariant $Cfg $Req $tmp 'api' $By $body.prompt $Note $cost ('OpenAI API ' + $model) } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    [pscustomobject]@{ ok = $true; why = ('{0} from the API, ${1:0.000}' -f $v.name, $cost); variant = $v; estimate = $est; monthUsd = (Get-ArtMonthTotal (Read-ArtLedger $Cfg.Ledger) $Now) }
}

# ---- importing the console spec (P132) --------------------------------------------------------------------------

function ConvertFrom-ArtRgb([string]$t) { if ($t -match '(\d+)\s*,\s*(\d+)\s*,\s*(\d+)') { @([int]$Matches[1], [int]$Matches[2], [int]$Matches[3]) } else { $null } }

# notes\console-art-spec.md -> one request per slot (20), with the shared style paragraph. Pure: text in, objects out.
function ConvertFrom-ConsoleArtSpec([string]$Text) {
    $style = (([regex]::Matches($Text, '(?m)^>\s?(.*)$') | ForEach-Object { $_.Groups[1].Value.Trim() }) -join ' ').Trim()
    $glows = @{ green = (ConvertFrom-ArtRgb ([regex]::Match($style, 'green (\d+,\d+,\d+)').Groups[1].Value)); amber = (ConvertFrom-ArtRgb ([regex]::Match($style, 'amber (\d+,\d+,\d+)').Groups[1].Value))
        red = (ConvertFrom-ArtRgb ([regex]::Match($style, 'red (\d+,\d+,\d+)').Groups[1].Value)); golem = (ConvertFrom-ArtRgb ([regex]::Match($style, 'glow (\d+,\d+,\d+)').Groups[1].Value)) }
    $out = New-Object 'System.Collections.Generic.List[object]'
    foreach ($m in [regex]::Matches($Text, '(?m)^\|\s*`([a-z0-9-]+\.png)`\s*\|([^|]*)\|([^|]*)\|([^|]*)\|([^|]*)\|\s*"?(.*?)"?\s*\|\s*$')) {
        $file = $m.Groups[1].Value; $sizeT = $m.Groups[2].Value.Trim(); $frT = $m.Groups[4].Value.Trim(); $budT = $m.Groups[5].Value; $subj = $m.Groups[6].Value.Trim().Trim('"')
        $name = $file -replace '\.png$', ''
        $frames = [int]([regex]::Match($frT, '^\d+').Value)
        $fps = $(if ($frT -match '~?(\d+)\s*fps') { [double]$Matches[1] } else { 0 })
        $anim = $(if ($name -like 'crystal-*') { 'pulse-turn' } elseif ($name -eq 'golem-running') { 'halo-sparks' } elseif ($name -eq 'spinner') { 'spinner' } else { '' })
        if ($frames -gt 1 -and $fps -le 0) { $fps = @{ 'pulse-turn' = 8; 'halo-sparks' = 6; 'spinner' = 10 }[$anim] }
        $sizes = @()
        if ($sizeT -match '(\d+)\s*tall by\s*(\d+)\s*wide') { $sizes = @(, @([int]$Matches[2], [int]$Matches[1])) }
        elseif ($sizeT -match '^(\d+)\s*and\s*(\d+)') { $sizes = @(@([int]$Matches[1], [int]$Matches[1]), @([int]$Matches[2], [int]$Matches[2])) }
        elseif ($sizeT -match '^(\d+)') { $sizes = @(, @([int]$Matches[1], [int]$Matches[1])) }
        $kind = $(if ($frames -gt 1) { 'animated' } elseif ($sizeT -match 'seamless') { 'tile' } elseif ($sizeT -match 'tiles horizontally') { 'band' } else { 'static' })
        $opaque = ($subj -match '\bopaque\b' -and $subj -notmatch 'nothing opaque')
        $glow = $(if ($name -like 'crystal-*') { $glows[$name.Substring(8)] } elseif ($name -like 'golem-*') { $glows.golem } elseif ($name -eq 'spinner') { @(220, 178, 98) } else { $null })
        $lo = $null; $hi = $null
        if ($subj -match 'lighter than (\d+,\d+,\d+) or darker than (\d+,\d+,\d+)') { $hi = ConvertFrom-ArtRgb $Matches[1]; $lo = ConvertFrom-ArtRgb $Matches[2] }
        elseif ($subj -match 'between (\d+,\d+,\d+) and (\d+,\d+,\d+)') { $lo = ConvertFrom-ArtRgb $Matches[1]; $hi = ConvertFrom-ArtRgb $Matches[2] }
        $slot = New-ArtSlot -File $file -Sizes $sizes -Kind $kind -Frames $(if ($frames -gt 0) { $frames } else { 1 }) -Fps $fps -Animation $anim -Transparent (-not $opaque) `
            -BudgetKB ([int]([regex]::Match($budT, '\d+').Value)) -Margin $(if ($opaque) { 0 } else { 2 }) -Glow $glow -ToneLow $lo -ToneHigh $hi
        $out.Add((New-ArtRequestObject -Id ('console-' + $name) -Title ('Console art: ' + $name) -AskedBy 'P132' -Style $style -Subject $subj -Slots @($slot) `
            -Destination ([pscustomobject]@{ kind = 'console'; folder = 'scripts\console-art'; manifest = 'scripts\console-art\art.json' })))
    }
    $out.ToArray()
}

# ---- processing (art_process.py) --------------------------------------------------------------------------------

function Invoke-ArtProcess($Cfg, [string[]]$Arguments) {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $o = (& $Cfg.Python -P $Cfg.Process @Arguments 2>&1 | ForEach-Object { [string]$_ }) -join "`n" } finally { $ErrorActionPreference = $old }
    $j = $o.Substring([Math]::Max(0, $o.IndexOf('{')))
    try { $j | ConvertFrom-Json } catch { [pscustomobject]@{ ok = $false; why = ('art_process.py said: ' + $o) } }
}

function ConvertTo-ArtSlotJson($Slot) { ($Slot | ConvertTo-Json -Depth 5 -Compress) }

# A review picture for one variant: on the slate, at each slot size, every frame of a strip.
function New-ArtPreview($Cfg, $Req, $Variant) {
    $out = [IO.Path]::ChangeExtension($Variant.file, '.preview.png')
    $slots = Join-Path ([IO.Path]::GetTempPath()) ('art-slots-' + [guid]::NewGuid().ToString('N') + '.json')
    [IO.File]::WriteAllText($slots, (@($Req.slots) | ConvertTo-Json -Depth 5 -Compress))
    try { $r = Invoke-ArtProcess $Cfg @('preview', '--master', $Variant.file, '--slots', $slots, '--out', $out) } finally { Remove-Item -LiteralPath $slots -Force -ErrorAction SilentlyContinue }
    $r
}

# The console's art.json with this slot's frames and fps set (animated) or removed (anything else). Pure.
function Set-ConsoleArtManifestSlot([string]$Text, [string]$SlotName, $Slot) {
    $j = $null
    try { if ($Text) { $j = $Text | ConvertFrom-Json } } catch { }
    $slots = [ordered]@{}
    if ($j -and $j.slots) { foreach ($p in $j.slots.PSObject.Properties) { $slots[$p.Name] = $p.Value } }
    if ($Slot.kind -eq 'animated') { $slots[$SlotName] = [ordered]@{ frames = [int]$Slot.frames; fps = [double]$Slot.fps } } else { $slots.Remove($SlotName) }
    ([ordered]@{ slots = $slots } | ConvertTo-Json -Depth 4)
}

# Place: the approved master through art_process.py for every slot, the first size into the destination, the rest and
# the master and every variant into the archive, the console manifest updated, the request marked placed.
function Invoke-ArtPlace($Cfg, $Req, [string]$By = 'Chase') {
    if ($Req.status -ne 'approved' -or -not $Req.approved) { return [pscustomobject]@{ ok = $false; why = ('not placed: ' + $Req.id + ' has no approved variant (status ' + $Req.status + '); Approve one first') } }
    $v = Get-ArtVariant $Req $Req.approved
    $d = $Req.destination
    $dest = $(switch ([string]$d.kind) {
        'console' { $Cfg.ConsoleArt }
        default { if ($d.folder) { if ([IO.Path]::IsPathRooted([string]$d.folder)) { [string]$d.folder } else { Join-Path $Cfg.ShardRepo ([string]$d.folder) } } else { $null } }
    })
    if (-not $dest) { return [pscustomobject]@{ ok = $false; why = 'not placed: the destination has no folder' } }
    $stage = Join-Path $Cfg.Archive (Join-Path $Req.id ('placed-' + (Get-Date).ToString('yyyyMMdd-HHmmss', $script:ArtInv)))
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    $reports = @(); $placed = @()
    foreach ($s in @($Req.slots)) {
        $sj = Join-Path $stage ($s.file + '.slot.json')
        [IO.File]::WriteAllText($sj, (ConvertTo-ArtSlotJson $s))
        $r = Invoke-ArtProcess $Cfg @('place', '--master', $v.file, '--slot', $sj, '--out-dir', $stage)
        $reports += $r
        if (-not $r.ok) { return [pscustomobject]@{ ok = $false; why = ('not placed: ' + $s.file + ': ' + $r.why); reports = $reports } }
    }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    foreach ($r in $reports) {
        $to = Join-Path $dest $r.file
        Copy-Item -LiteralPath $r.primary -Destination $to -Force
        $placed += [pscustomobject]@{ file = $r.file; to = $to; at = (Get-ArtNow); bytes = $r.bytes; checks = $r.checks }
    }
    if ($d.kind -eq 'console') {
        $mf = Join-Path $Cfg.ConsoleArt 'art.json'
        $text = $(if (Test-Path -LiteralPath $mf) { [IO.File]::ReadAllText($mf) } else { '' })
        foreach ($s in @($Req.slots)) { $text = Set-ConsoleArtManifestSlot $text ($s.file -replace '\.png$', '') $s }
        Write-ArtText $mf $text
    }
    # The master and every variant to the archive (outside git); the review folder is emptied for this request.
    $arch = Join-Path $Cfg.Archive $Req.id
    foreach ($x in @($Req.variants)) {
        if ($x.file -and (Test-Path -LiteralPath $x.file)) {
            $to = Join-Path $arch ([IO.Path]::GetFileName($x.file))
            if ($x.file -ne $to) { Copy-Item -LiteralPath $x.file -Destination $to -Force }
            $pv = [IO.Path]::ChangeExtension($x.file, '.preview.png'); if (Test-Path -LiteralPath $pv) { Copy-Item -LiteralPath $pv -Destination (Join-Path $arch ([IO.Path]::GetFileName($pv))) -Force }
            $x.file = $to
        }
    }
    $rv = Join-Path $Cfg.Review $Req.id
    if (Test-Path -LiteralPath $rv) { Remove-Item -LiteralPath $rv -Recurse -Force }
    $Req.placed = @($Req.placed) + $placed
    Set-ArtStatus $Req 'placed' $By ('placed into ' + $dest)
    $how = $(switch ([string]$d.kind) {
        'console' { 'the console reads it the next time it opens' }
        'game-gump' { 'the player package picks it up at its next build (player-package\Build-PlayerPackage.ps1)' }
        'game-item' { 'the player package picks it up at its next build; check the registry row with scripts\find-free-art.py --check-registry' }
        'game-anim' { 'encode it with scripts\build-equip-anims.py, then the next player package build' }
        default { 'copied; publish it with the web page' }
    })
    [pscustomobject]@{ ok = $true; why = ('placed ' + ((@($placed) | ForEach-Object { $_.file }) -join ', ') + ' into ' + $dest + ': ' + $how); reports = $reports; placed = $placed }
}
