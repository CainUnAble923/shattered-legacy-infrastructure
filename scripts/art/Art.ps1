#Requires -Version 5.1
<#
  Art.ps1 (cc-P152): the art pipeline's commands. Every command prints one JSON object ({ ok, why, ... }) and exits 0
  when ok, 1 when not, so the console now and the web console later call the same thing.

    list      [-Status new|generated|approved|rejected|placed]   requests with status, variants and a thumbnail path
    show      -Id <id>                                           one request in full
    prompt    -Id <id> [-Note <text>] [-Clip]                    the text for ChatGPT by hand (-Clip: to the clipboard)
    new       -File <request.json>                               file a request (New-ArtRequest.ps1 is the friendlier way)
    estimate  -Id <id>                                           what Generate would cost: Codex plan usage, API price and cap
    generate  -Id <id> [-Backend codex|api] [-Note <text>] [-Override yes] -Go
                                                                 without -Go: the estimate only, nothing generated
    regenerate -Id <id> -Note <text> [-Backend codex|api] [-Override yes] -Go
                                                                 generate again with the note (Codex sees the last variant)
    import    -Id <id> -File <path or a file name in D:\UO\art-inbox>   a picture made elsewhere, as a variant
    inbox                                                        what is waiting in D:\UO\art-inbox
    approve   -Id <id> [-Variant v2]                             (default: the newest variant not rejected)
    reject    -Id <id> [-Variant v2] [-Note <why>]
    preview   -Id <id> [-Variant v2]                             (re)draw the review sheet for a variant
    place     -Id <id>                                           process the approved variant into its destination
    check     -Id <id>                                           the slot checks on what is placed now
    import-spec [-SpecFile notes\console-art-spec.md] [-Write]   the console's 20 slots as requests (-Write files new ones)
    set-key                                                      asks for the OpenAI API key, stores it with DPAPI
    test-key                                                     one free call (GET /v1/models) with the stored key
    key-status                                                   whether a key is stored (never the key)

  Generate and regenerate run only with -Go, which only Chase's click passes: the Golem and agents never pass it.
  Nothing is placed until a variant is approved.
#>
param(
    [Parameter(Position = 0)][string]$Command = 'list',
    [string]$Id, [string]$Status, [string]$Variant, [string]$Note, [string]$File, [string]$SpecFile,
    [ValidateSet('codex', 'api')][string]$Backend = 'codex', [string]$Override = '', [string]$By = 'Chase',
    [switch]$Go, [switch]$Clip, [switch]$Write,
    [string]$StoreRoot = 'D:\UO\art', [string]$GolemRoot = 'D:\UO\golem', [string]$Inbox = 'D:\UO\art-inbox', [string]$ShardRepo = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ArtLib.ps1')
if (-not $ShardRepo) { $ShardRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
$cfg = Get-ArtConfig -StoreRoot $StoreRoot -GolemRoot $GolemRoot -Inbox $Inbox -ShardRepo $ShardRepo

function Out-Result($o) {
    $o | ConvertTo-Json -Depth 8
    exit $(if ($o.ok) { 0 } else { 1 })
}
function Need-Id { if (-not $Id) { Out-Result ([pscustomobject]@{ ok = $false; why = ($Command + ' needs -Id') }) } }
function Log-Art([string]$What, [hashtable]$More = @{}) {
    $o = [ordered]@{ at = (Get-ArtNow); action = $What; id = $Id; by = $By }
    foreach ($k in $More.Keys) { $o[$k] = $More[$k] }
    Add-ArtLine $cfg.Log $o
}

function Get-Thumb($r) {
    $v = @($r.variants | Where-Object { $_.file })
    foreach ($x in @($v | Where-Object { $_.name -eq $r.approved }) + @($v | Select-Object -Last 1)) {
        if (-not $x) { continue }
        $p = [IO.Path]::ChangeExtension($x.file, '.preview.png')
        if (Test-Path -LiteralPath $p) { return $p }
        if (Test-Path -LiteralPath $x.file) { return $x.file }
    }
    foreach ($p in @($r.placed)) { if ($p.to -and (Test-Path -LiteralPath $p.to)) { return $p.to } }
    ''
}

function Get-CodexStatusJson {
    $codex = $cfg.CodexScript
    if (-not (Test-Path -LiteralPath $codex)) { return $null }
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $t = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $codex -Action status 2>$null) -join "`n" } finally { $ErrorActionPreference = $old }
    try { $t | ConvertFrom-Json } catch { $null }
}

function Get-Estimate($r) {
    $set = Get-ArtApiSettings $cfg
    $est = Get-ArtCostEstimate ([string]$set.artApiModel) ([string]$set.artApiSize) ([string]$set.artApiQuality)
    $month = Get-ArtMonthTotal (Read-ArtLedger $cfg.Ledger) (Get-Date)
    $cap = Test-ArtApiCap $month $est ([double]$set.artApiMonthlyCapUsd)
    $cs = Get-CodexStatusJson
    [pscustomobject]@{
        codex = [pscustomobject]@{ available = [bool]($cs -and $cs.exe -and $cs.login.ok); login = $(if ($cs) { $cs.login.text } else { 'Codex.ps1 not found' })
            budget = $(if ($cs) { $cs.budget } else { $null }); today = $(if ($cs) { $cs.today } else { $null }); cost = 'no money: the ChatGPT plan''s Codex limits (an image uses them 3 to 5 times faster than a text turn, per OpenAI)' }
        api = [pscustomobject]@{ keyStored = (Test-ArtApiKeyStored $cfg); model = $set.artApiModel; size = $set.artApiSize; quality = $set.artApiQuality
            estimateUsd = $est.Usd; basis = $(if ($est.Error) { $est.Error } else { $est.Basis }); monthUsd = $month; capUsd = [double]$set.artApiMonthlyCapUsd; allowed = $cap.Allowed; why = $cap.Why }
    }
}

function Invoke-CodexGenerate($r, [string]$NoteText) {
    $dir = Join-Path $cfg.Review $r.id
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $ref = @()
    if ($NoteText) { $last = @($r.variants | Where-Object { $_.file -and (Test-Path -LiteralPath $_.file) }); if ($last.Count) { $ref = @($last[-1].file) } }
    $text = Get-ArtCodexPrompt $r $NoteText ($ref.Count -gt 0)
    $pf = Join-Path $dir ((Get-NextVariantName $r) + '-codex.prompt.txt')
    [IO.File]::WriteAllText($pf, $text, (New-Object Text.UTF8Encoding($false)))
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $cfg.CodexScript, '-Action', 'run', '-Purpose', 'art', '-RequestId', $r.id, '-PromptFile', $pf)
    if ($Override) { $a += @('-Override', $Override) }
    if ($ref.Count) { $a += @('-Images', $ref[0]) }
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $t = (& powershell.exe @a 2>$null) -join "`n" } finally { $ErrorActionPreference = $old }
    try { $res = $t | ConvertFrom-Json } catch { return [pscustomobject]@{ ok = $false; why = ('Codex.ps1 did not answer in JSON: ' + $t) } }
    if (-not $res.ok -or -not @($res.images).Count) { return [pscustomobject]@{ ok = $false; why = ('Codex: ' + $res.why); budget = $res.budget } }
    $v = Add-ArtVariant $cfg $r (@($res.images)[-1]) 'codex' $By $text $NoteText $null ('Codex thread ' + $res.threadId)
    [pscustomobject]@{ ok = $true; why = ($v.name + ' from Codex'); variant = $v; budget = $res.budget; threadId = $res.threadId }
}

switch ($Command) {
    'list' {
        $rs = Get-ArtRequests $cfg $Status
        Out-Result ([pscustomobject]@{ ok = $true; why = ([string]$rs.Count + ' request(s)'); store = $cfg.StoreRoot
            requests = @($rs | ForEach-Object { [pscustomobject]@{ id = $_.id; title = $_.title; status = $_.status; askedBy = $_.askedBy; updated = $_.updated
                variants = @(@($_.variants) | ForEach-Object { [pscustomobject]@{ name = $_.name; backend = $_.backend; verdict = $_.verdict; file = $_.file } })
                approved = $_.approved; destination = $_.destination.kind; slots = @(@($_.slots) | ForEach-Object { $_.file }); thumbnail = (Get-Thumb $_) } }) })
    }
    'show' { Need-Id; Out-Result ([pscustomobject]@{ ok = $true; why = ''; request = (Read-ArtRequest $cfg $Id); thumbnail = (Get-Thumb (Read-ArtRequest $cfg $Id)) }) }
    'prompt' {
        Need-Id
        $t = Get-ArtPrompt (Read-ArtRequest $cfg $Id) $Note
        if ($Clip) { Add-Type -AssemblyName System.Windows.Forms; [Windows.Forms.Clipboard]::SetText($t) }
        Out-Result ([pscustomobject]@{ ok = $true; why = $(if ($Clip) { 'copied to the clipboard' } else { '' }); prompt = $t })
    }
    'new' {
        if (-not $File) { Out-Result ([pscustomobject]@{ ok = $false; why = 'new needs -File <request.json>' }) }
        $j = [IO.File]::ReadAllText($File) | ConvertFrom-Json
        $slots = @($j.slots | ForEach-Object { New-ArtSlot -File $_.file -Sizes @($_.sizes) -Kind $(if ($_.kind) { $_.kind } else { 'static' }) -Frames $(if ($_.frames) { $_.frames } else { 1 }) -Fps ([double](Get-ArtProp $_ 'fps' 0)) `
            -Animation ([string](Get-ArtProp $_ 'animation' '')) -Transparent ([bool](Get-ArtProp $_ 'transparent' $true)) -BudgetKB ([int](Get-ArtProp $_ 'budgetKB' 100)) -Margin ([int](Get-ArtProp $_ 'margin' 2)) `
            -Glow (Get-ArtProp $_ 'glow' $null) -ToneLow (Get-ArtProp $_ 'toneLow' $null) -ToneHigh (Get-ArtProp $_ 'toneHigh' $null) })
        $r = New-ArtRequestObject -Id $j.id -Title $j.title -AskedBy $j.askedBy -Style ([string]$j.style) -Subject $j.subject -Slots $slots -Destination $j.destination -Note ([string](Get-ArtProp $j 'note' ''))
        if (Test-Path -LiteralPath (Get-ArtRequestPath $cfg $r.id)) { Out-Result ([pscustomobject]@{ ok = $false; why = ('a request ' + $r.id + ' already exists') }) }
        Save-ArtRequest $cfg $r; Log-Art 'new' @{ id = $r.id }
        Out-Result ([pscustomobject]@{ ok = $true; why = ('filed ' + $r.id); request = $r })
    }
    'estimate' { Need-Id; [void](Read-ArtRequest $cfg $Id); Out-Result ([pscustomobject]@{ ok = $true; why = ''; estimate = (Get-Estimate $null) }) }
    { $_ -in 'generate', 'regenerate' } {
        Need-Id
        $r = Read-ArtRequest $cfg $Id
        if ($Command -eq 'regenerate' -and -not $Note) { Out-Result ([pscustomobject]@{ ok = $false; why = 'regenerate needs -Note (what to change, for example "make the golem squatter")' }) }
        if ($r.status -eq 'placed') { Out-Result ([pscustomobject]@{ ok = $false; why = ($Id + ' is placed already') }) }
        $est = Get-Estimate $r
        if (-not $Go) { Out-Result ([pscustomobject]@{ ok = $true; why = 'estimate only: nothing generated (Generate passes -Go on Chase''s click)'; backend = $Backend; estimate = $est }) }
        if ($Note) { Add-ArtNote $r $By $Note }
        if ($Backend -eq 'codex') { $res = Invoke-CodexGenerate $r $Note }
        else {
            if (-not $est.api.keyStored) { Out-Result ([pscustomobject]@{ ok = $false; why = 'no OpenAI API key stored: Art.ps1 set-key' }) }
            $res = Invoke-ArtApiGenerate $cfg $r $Note $By
        }
        if ($res.ok) {
            $pv = New-ArtPreview $cfg $r $res.variant
            Save-ArtRequest $cfg $r
            Log-Art $Command @{ backend = $Backend; variant = $res.variant.name; note = $Note; costUsd = $res.variant.costUsd }
            $res | Add-Member -NotePropertyName preview -NotePropertyValue $(if ($pv.ok) { $pv.preview } else { '' }) -Force
        } else {
            if ($Note) { Save-ArtRequest $cfg $r }
            Log-Art ($Command + ' refused') @{ backend = $Backend; why = $res.why }
        }
        $res | Add-Member -NotePropertyName estimate -NotePropertyValue $est -Force
        Out-Result $res
    }
    'import' {
        Need-Id
        if (-not $File) { Out-Result ([pscustomobject]@{ ok = $false; why = 'import needs -File (a path, or a file name in ' + $cfg.Inbox + ')' }) }
        $src = $(if (Test-Path -LiteralPath $File -PathType Leaf) { (Resolve-Path -LiteralPath $File).Path } else { Join-Path $cfg.Inbox $File })
        $r = Read-ArtRequest $cfg $Id
        $v = Add-ArtVariant $cfg $r $src 'import' $By '' $Note $null $src
        $pv = New-ArtPreview $cfg $r $v
        Save-ArtRequest $cfg $r; Log-Art 'import' @{ variant = $v.name; source = $src }
        Out-Result ([pscustomobject]@{ ok = $true; why = ($v.name + ' imported from ' + $src); variant = $v; preview = $(if ($pv.ok) { $pv.preview } else { '' }) })
    }
    'inbox' {
        $f = @(if (Test-Path -LiteralPath $cfg.Inbox) { Get-ChildItem -LiteralPath $cfg.Inbox -File | Where-Object { $_.Extension -match '^\.(png|webp|jpe?g|gif)$' } | Sort-Object LastWriteTime -Descending })
        Out-Result ([pscustomobject]@{ ok = $true; why = ([string]$f.Count + ' file(s) in ' + $cfg.Inbox); files = @($f | ForEach-Object { [pscustomobject]@{ name = $_.Name; path = $_.FullName; bytes = $_.Length; at = $_.LastWriteTime.ToString('s') } }) })
    }
    'approve' {
        Need-Id; $r = Read-ArtRequest $cfg $Id
        if (-not @($r.variants).Count) { Out-Result ([pscustomobject]@{ ok = $false; why = ($Id + ' has nothing to approve yet') }) }
        $v = Approve-ArtVariant $r $Variant $By
        Save-ArtRequest $cfg $r; Log-Art 'approve' @{ variant = $v.name }
        Out-Result ([pscustomobject]@{ ok = $true; why = ($v.name + ' approved: Place puts it in ' + $r.destination.kind); variant = $v })
    }
    'reject' {
        Need-Id; $r = Read-ArtRequest $cfg $Id
        Deny-ArtVariant $r $Variant $By $Note
        Save-ArtRequest $cfg $r; Log-Art 'reject' @{ variant = $Variant; note = $Note }
        Out-Result ([pscustomobject]@{ ok = $true; why = 'rejected' + $(if ($Note) { ': ' + $Note } else { '' }) })
    }
    'preview' {
        Need-Id; $r = Read-ArtRequest $cfg $Id
        $v = $(if ($Variant) { Get-ArtVariant $r $Variant } else { @($r.variants)[-1] })
        if (-not $v) { Out-Result ([pscustomobject]@{ ok = $false; why = 'no variant to preview' }) }
        Out-Result (New-ArtPreview $cfg $r $v)
    }
    'place' {
        Need-Id; $r = Read-ArtRequest $cfg $Id
        $res = Invoke-ArtPlace $cfg $r $By
        if ($res.ok) { Save-ArtRequest $cfg $r; Log-Art 'place' @{ files = @($res.placed | ForEach-Object { $_.to }) } }
        Out-Result $res
    }
    'check' {
        Need-Id; $r = Read-ArtRequest $cfg $Id
        $out = @()
        foreach ($s in @($r.slots)) {
            $dir = $(if ($r.destination.kind -eq 'console') { $cfg.ConsoleArt } else { Join-Path $cfg.ShardRepo ([string]$r.destination.folder) })
            $p = Join-Path $dir $s.file
            if (-not (Test-Path -LiteralPath $p)) { $out += [pscustomobject]@{ ok = $false; why = 'not placed: ' + $p; file = $p }; continue }
            $sj = Join-Path ([IO.Path]::GetTempPath()) ('art-slot-' + [guid]::NewGuid().ToString('N') + '.json')
            [IO.File]::WriteAllText($sj, (ConvertTo-ArtSlotJson $s))
            try { $out += Invoke-ArtProcess $cfg @('check', '--file', $p, '--slot', $sj) } finally { Remove-Item -LiteralPath $sj -Force -ErrorAction SilentlyContinue }
        }
        $bad = @($out | Where-Object { -not $_.ok })
        Out-Result ([pscustomobject]@{ ok = (-not $bad.Count); why = $(if ($bad.Count) { ($bad | ForEach-Object { $_.why }) -join ' | ' } else { 'all checks passed' }); results = $out })
    }
    'import-spec' {
        if (-not $SpecFile) { $SpecFile = 'D:\UO\shard-migration\notes\console-art-spec.md' }
        $rs = @(ConvertFrom-ConsoleArtSpec ([IO.File]::ReadAllText($SpecFile)))
        $filed = @()
        if ($Write) { foreach ($r in $rs) { if (-not (Test-Path -LiteralPath (Get-ArtRequestPath $cfg $r.id))) { Save-ArtRequest $cfg $r; $filed += $r.id } } }
        Out-Result ([pscustomobject]@{ ok = $true; why = ([string]$rs.Count + ' slot(s) in the spec' + $(if ($Write) { ', ' + $filed.Count + ' filed' } else { ' (not written: -Write files them)' })); ids = @($rs | ForEach-Object { $_.id }); filed = $filed })
    }
    'set-key' {
        Write-Host 'Paste the OpenAI API key (it is not shown) and press Enter. It is stored with Windows DPAPI for this login only.'
        $k = Read-Host -AsSecureString 'OpenAI API key'
        Set-ArtApiKey $cfg $k; Log-Art 'set-key' @{ file = $cfg.KeyFile }
        Out-Result ([pscustomobject]@{ ok = $true; why = ('stored in ' + $cfg.KeyFile + ' (DPAPI, current user)') })
    }
    'key-status' { Out-Result ([pscustomobject]@{ ok = $true; why = ''; keyStored = (Test-ArtApiKeyStored $cfg); file = $cfg.KeyFile }) }
    'test-key' {
        if (-not (Test-ArtApiKeyStored $cfg)) { Out-Result ([pscustomobject]@{ ok = $false; why = 'no key stored: Art.ps1 set-key' }) }
        $key = Get-ArtApiKeyPlain $cfg
        try {
            $m = & $script:ArtApiTestClient $key
            $img = @($m.data | Where-Object { $_.id -like 'gpt-image*' } | ForEach-Object { $_.id })
            Out-Result ([pscustomobject]@{ ok = $true; why = ('the key works (GET /v1/models, no charge); image models it can see: ' + ($img -join ', ')) })
        } catch { Out-Result ([pscustomobject]@{ ok = $false; why = ('the key did not work: ' + (Remove-ArtSecret $_.Exception.Message $key)) }) }
    }
    default { Out-Result ([pscustomobject]@{ ok = $false; why = ('no command ' + $Command + ': list, show, prompt, new, estimate, generate, regenerate, import, inbox, approve, reject, preview, place, check, import-spec, set-key, test-key, key-status') }) }
}
