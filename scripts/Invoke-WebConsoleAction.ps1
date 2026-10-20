#Requires -Version 5.1
<#
  cc-P182: runs one of the web console's Golem buttons with the Shard Console's own code. web-console\server.py starts it,
  one at a time, after its own checks (identity, the sidecar's proof, the typed word); this checks the word again
  against the console's plan, then runs the plan with the console's Invoke-Plan. It never re-implements a Golem action:

      the button (web-console\golem-actions.json)  ->  Get-WebActionPlan  ->  Get-ActionPlan (the console's button)
                                                    ->  Test-PlanSafety and Invoke-Plan (the console's), each step said

  Window steps (each Golem script "in a window of its own") run in this process instead ($script:WindowStepsInline),
  so their output comes back to the page as it is printed; each line carries its colour as in the console's pane
  (SHARD_CONSOLE_PANE). The result line is the console's own: "<action>: done.", or its refusal or FAILED line.

      Invoke-WebConsoleAction.ps1 -Key <button> -Typed <word> [-Model m] [-Cap n] [-Until t] [-NightStart hh:mm]
                                  [-NightEnd hh:mm] [-Prompt P<nn>] [-Golem <name>] [-DryRun]

  cc-P186: -Golem names another Golem of several on this PC (golems.json; the integrator is the console's own Golem,
  as without it). Only a button marked anyGolem; the facts are that Golem's own files (Get-GolemScopedConfig), and
  each Golem script in the plan is told its name (Add-GolemToPlan: -Golem <name>, which Test-PlanSafety allows). A
  Golem on another PC never comes here: the web console queues it for that PC's agent (D:\UO\golem\Remote-Agent.ps1).
  cc-P187: the word for another Golem names it (golemWord: "wake bronze"); Assign (-Prompt, -AssignTo, -Next).

  cc-P187: every other tab of the Shard Console (web-console\console-actions.json, the web console's pages) the same way:

      the button  ->  Get-ActionFacts (the console's reads)  ->  Get-WebPagePlan  ->  Get-ActionPlan (the console's button)
                  ->  Test-PlanSafety and Invoke-Plan (the console's), with the Golem sign-up sheet's locks held meanwhile

  with the inputs its window's boxes and lists give (-Shard, -SnapshotName, -Snapshot, -CommitMessage, -CommitForce yes,
  -PackageNotes, -PackageZip, -ImageTag, -Commit, -Site, -VdFile, -Body, -HuesFile). -DryRun is the page's look first:
  the plan, then one [[web-preview]] line (JSON: the phrase the plan asks, its confirm text, whether it is dangerous).
  -Second is the page's second confirm (a dangerous button, the LIVE shard, a yes/no question). The Art tab's buttons
  (-ArtId, -Note) run scripts\art\Art.ps1 with the console's own Get-ArtButtonCommand and Get-ArtGenerateChoice.
  A script's own "Type yes" reads stdin: server.py writes the button's answers there only after the second confirm.

  Exit 0 done, 1 failed, 2 refused (nothing run), 3 cancelled. Never prints a secret: the Golem's scripts print none,
  and server.py redacts every line before it reaches a page or the action log. ASCII only.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Key, [string]$Typed = '', [string]$Model, [string]$Cap, [string]$Until,
    [string]$NightStart, [string]$NightEnd, [string]$Prompt, [string]$Golem, [switch]$DryRun, [string]$RepoRoot, [string]$ActionsFile,
    [string]$AssignTo, [string]$Next, [string]$PagesFile, [switch]$Second,
    [string]$Shard, [string]$SnapshotName, [string]$Snapshot, [string]$CommitMessage, [string]$CommitForce, [string]$PackageNotes,
    [string]$PackageZip, [string]$ImageTag, [string]$Commit, [string]$Site, [string]$VdFile, [string]$Body, [string]$HuesFile,
    [string]$ArtId, [string]$Note, [string]$LockRoot)
$ErrorActionPreference = 'Stop'
# Kept under names of their own: dot-sourcing the console runs its param block here, which resets its own names.
$web = [pscustomobject]@{ Key = $Key; Typed = $Typed; Dry = [bool]$DryRun; Second = [bool]$Second
    Inputs = @{}; Root = $(if ($RepoRoot) { $RepoRoot } else { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path })
    Actions = $ActionsFile; Pages = $PagesFile; Golem = $Golem; GolemName = ''; LockRoot = $LockRoot }
foreach ($p in @(@('model', $Model), @('cap', $Cap), @('until', $Until), @('start', $NightStart), @('end', $NightEnd), @('prompt', $Prompt),
                 @('assignTo', $AssignTo), @('next', $Next), @('shard', $Shard), @('snapshotName', $SnapshotName), @('snapshot', $Snapshot),
                 @('commitMessage', $CommitMessage), @('force', $CommitForce), @('packageNotes', $PackageNotes), @('packageZip', $PackageZip),
                 @('imageTag', $ImageTag), @('commit', $Commit), @('site', $Site), @('vdFile', $VdFile), @('body', $Body), @('huesFile', $HuesFile),
                 @('artId', $ArtId), @('note', $Note))) {
    if ($null -ne $p[1] -and [string]$p[1] -ne '') { $web.Inputs[$p[0]] = [string]$p[1] }
}
if (-not $web.Actions) { $web.Actions = Join-Path $web.Root 'web-console\golem-actions.json' }
if (-not $web.Pages) { $web.Pages = Join-Path $web.Root 'web-console\console-actions.json' }
$env:SHARD_CONSOLE_PANE = '1'
. (Join-Path $PSScriptRoot 'Shard-Console.ps1') -LoadOnly
$config = Get-ConsoleConfig -RepoRoot $web.Root

function Write-WebPreview($o) {
    # cc-P187: the page's look first, one line server.py reads (and keeps off the page's output).
    [Console]::Out.WriteLine('[[web-preview]]' + ([pscustomobject]$o | ConvertTo-Json -Compress -Depth 4))
}

$entry = @((Get-Content -LiteralPath $web.Actions -Raw | ConvertFrom-Json).actions | Where-Object { $_.key -ceq $web.Key }) | Select-Object -First 1
$page = $null
if (-not $entry) {
    # cc-P187: a page button (console-actions.json).
    foreach ($pg in @((Get-Content -LiteralPath $web.Pages -Raw | ConvertFrom-Json).pages)) {
        $hit = @(@($pg.buttons) | Where-Object { $_.key -ceq $web.Key }) | Select-Object -First 1
        if ($hit) { $entry = $hit; $page = $pg; break }
    }
}
if (-not $entry) { Write-ConsoleLine ('REFUSED: no such button: ' + $web.Key) 'red'; exit 2 }

if ($page) {
    Write-ConsoleLine ('Web console: ' + $page.title + ' > ' + $entry.label + $(if ($web.Dry) { ' (look first: a dry run)' } else { '' })) 'head'
    if ($entry.pcOnly) {
        $why = 'PC only: ' + [string]$entry.pcOnly
        if ($web.Dry) { Write-WebPreview @{ refused = $why } }
        Write-ConsoleLine ($why + ' Nothing was run.') 'amber'; exit 2
    }
    $isDanger = ([string]$entry.kind -eq 'dangerous')
    if ($entry.art) {
        # The Art tab: Art.ps1's commands (the console's Get-ArtButtonCommand), Generate after its estimate.
        $id = [string]$web.Inputs['artId']
        if ($id -notmatch '^[a-z0-9][a-z0-9-]{0,63}$') { Write-ConsoleLine 'Refused: pick a request first. Nothing was run.' 'amber'; exit 2 }
        $what = [string]$entry.art
        $backend = $(if ($entry.backend) { [string]$entry.backend } else { 'codex' })
        $note = [string]$web.Inputs['note']
        if ($what -eq 'regenerate' -and -not $note) { Write-ConsoleLine 'Refused: say what should change (the note). Nothing was run.' 'amber'; exit 2 }
        $expect = ''; $msg = ''; $override = ''
        if ($what -in @('generate', 'generate-api', 'regenerate')) {
            $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
            try { $t = (& $config.PowerShell -NoProfile -ExecutionPolicy Bypass -File $config.ArtScript generate -Id $id -Backend $backend 2>$null) -join "`n" } finally { $ErrorActionPreference = $eap }
            $e = $null; try { $e = ($t.Substring([Math]::Max(0, $t.IndexOf('{')))) | ConvertFrom-Json } catch { }
            $c = Get-ArtGenerateChoice -Estimate $e -Backend $backend -ArtScript $config.ArtScript
            if ($c.Refusal) {
                if ($web.Dry) { Write-WebPreview @{ refused = $c.Refusal } }
                Write-ConsoleLine ('Refused: ' + $c.Refusal) 'amber'; exit 2
            }
            $msg = $c.Message
            foreach ($l in @($msg -split "`n")) { Write-ConsoleLine $l 'normal' }
            if ($c.Override) { $expect = 'yes'; $override = 'yes'; Write-ConsoleLine 'Past the budget: type yes to run it anyway, once.' 'amber' }
        }
        $cmd = @(Get-ArtButtonCommand -What $what -Id $id -Note $note -Backend $backend -Override $override)
        if (-not $cmd.Count) { Write-ConsoleLine 'Refused: Art.ps1 has no command for this button. Nothing was run.' 'amber'; exit 2 }
        $line = Format-CommandLine 'Art.ps1' $cmd
        Write-ConsoleLine ('PLAN: ' + $line) 'normal'
        if ($web.Dry) { Write-WebPreview @{ expect = $expect; confirmText = $msg; danger = $isDanger; plan = $line }; Write-ConsoleLine '   (look first: nothing was run)' 'amber'; exit 0 }
        if ($expect -and -not [string]::Equals($expect, $web.Typed.Trim(), [StringComparison]::OrdinalIgnoreCase)) { Write-ConsoleLine ('Refused: type ' + $expect + ' to confirm. Nothing was run.') 'amber'; exit 2 }
        if ($isDanger -and -not $web.Second) { Write-ConsoleLine 'Refused: this needs the second confirm. Nothing was run.' 'amber'; exit 2 }
        $outFile = New-ExecOutputFile $config ('art.' + $what) 'art'
        $code = Invoke-ExecStep -Exe $config.PowerShell -Arguments (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $config.ArtScript) + $cmd) -OutFile $outFile -Header ('web console: ' + $line)
        if ($code -eq 0) { Write-ConsoleLine ('art.' + $what + ' ' + $id + ': done.') 'green'; exit 0 }
        Write-ConsoleLine ('art.' + $what + ' ' + $id + ': Art.ps1 exited ' + $code + ' (its answer is above).') 'red'; exit 1
    }
    # Every other page button: the console's reads, its plan, its checks, its Invoke-Plan.
    $pre = Get-ActionFacts -Config $config -ActionKey ([string]$entry.action) -PackageZip ([string]$web.Inputs['packageZip']) -FromCapture $false -StatusFrom '' -Dry $web.Dry
    $r = Get-WebPagePlan -Entry $entry -Typed $web.Typed -Inputs $web.Inputs -Config $config -Pre $pre -Second:$web.Second -Dry:$web.Dry -Now (Get-Date)
    if (-not $r.Ok) {
        if ($web.Dry) { Write-WebPreview @{ refused = $r.Refusal } }
        Write-ConsoleLine $(if ($r.Refusal -match '^(Refused|Not now|PC only)\b') { $r.Refusal } else { 'Refused: ' + $r.Refusal }) 'amber'; exit 2
    }
    $viol = @(Test-PlanSafety $r.Plan $config)
    foreach ($l in (Format-Plan -Action $r.ActionKey -ShardKey $r.ShardKey -Plan $r.Plan -Violations $viol -Header $(if ($web.Dry) { 'DRY RUN' } else { 'PLAN' }))) { Write-ConsoleLine $l 'normal' }
    if ($viol.Count) {
        if ($web.Dry) { Write-WebPreview @{ refused = ('the safety check: ' + ($viol -join '; ')) } }
        foreach ($x in $viol) { Write-ConsoleLine ('REFUSED BY THE SAFETY CHECK: ' + $x) 'red' }; exit 2
    }
    if ($web.Dry) {
        Write-WebPreview @{ expect = $r.Expect; confirmText = $r.ConfirmText; preface = (Get-ConfirmPreface $r.Plan); asks = $r.Asks; danger = [bool]$r.Danger; shard = $r.ShardKey }
        Write-ConsoleLine '   (look first: nothing was run)' 'amber'; exit 0
    }
    # The Golem sign-up sheet (D:\UO\golem\locks): taken as Chase for the run, so a Golem waits rather than collides.
    $held = New-Object 'System.Collections.Generic.List[string]'
    $lockDir = $(if ($web.LockRoot) { $web.LockRoot } else { Join-Path $config.Autopilot.GolemRoot 'locks' })
    if (@($entry.locks | Where-Object { $_ }).Count) { . (Join-Path $config.Autopilot.GolemRoot 'Golems.ps1') }
    try {
        foreach ($res in @($entry.locks | Where-Object { $_ })) {
            $mins = $(if ($entry.minutes) { [int]$entry.minutes } else { 30 })
            $lk = Enter-GolemLock -LockDir $lockDir -Resource ([string]$res) -Owner 'Chase' -Session 'web console' -Why ($r.ActionKey + ' from the web console') -TtlMinutes ($mins + 5) -now (Get-Date)
            if (-not $lk.Ok) { Write-ConsoleLine ('Refused: ' + $lk.Why + '. Nothing was run; try again when it is given back.') 'amber'; exit 2 }
            $held.Add([string]$res)
            Write-ConsoleLine ('Signed out ' + $res + ' on the Golem sign-up sheet (as Chase) for this run.') 'gray'
        }
        Write-ConsoleLine '' 'normal'
        $script:WindowStepsInline = $true
        $ok = Invoke-Plan -Plan $r.Plan -Config $config -ActionKey $r.ActionKey -ShardKey $r.ShardKey -ConfirmLive $r.Phrase -Yes:$r.Yes
    } finally {
        foreach ($res in $held) { [void](Exit-GolemLock -LockDir $lockDir -Resource $res -Owner 'Chase') }
    }
    if ($ok) { Write-ConsoleLine ($r.ActionKey + ': done.') 'green'; exit 0 }
    if ($script:PlanCancelled) { exit 3 }
    exit 1
}

if ($web.Golem) {
    # cc-P186: which Golem of several. Unknown: refused. The integrator: the console's own, as without -Golem.
    $g = $null
    try { $g = @((Get-Content -LiteralPath (Join-Path $config.Autopilot.GolemRoot 'golems.json') -Raw | ConvertFrom-Json).golems | Where-Object { [string]$_.name -ieq $web.Golem }) | Select-Object -First 1 } catch { }
    if (-not $g) { Write-ConsoleLine ('Refused: there is no ' + $web.Golem + ' Golem in golems.json. Nothing was run.') 'amber'; exit 2 }
    if (-not [bool]$g.integrator) {
        if (-not $entry.anyGolem) { Write-ConsoleLine ('Refused: ' + $entry.label + ' is the integrator''s only, not the ' + [string]$g.name + ' Golem''s. Nothing was run.') 'amber'; exit 2 }
        $web.GolemName = [string]$g.name
        $config = Get-GolemScopedConfig -Config $config -Name $web.GolemName
    }
}
Write-ConsoleLine ('Web console: ' + $entry.label + $(if ($web.GolemName) { ' (the ' + $web.GolemName + ' Golem)' } else { '' }) + $(if ($web.Dry) { ' (dry run)' } else { '' })) 'head'
$facts = $null
if ($entry.action) {
    # The console's own reads for this button (Get-ActionFacts), with the fresh sign-in check for a wake.
    $pre = Get-ActionFacts -Config $config -ActionKey ([string]$entry.action) -FromCapture $false -StatusFrom '' -Dry $web.Dry
    $facts = $pre.AutopilotFacts
    if ($facts -and $pre.CliCheck) { $facts | Add-Member -NotePropertyName CliCheck -NotePropertyValue $pre.CliCheck -Force }
}
$r = Get-WebActionPlan -Entry $entry -Typed $web.Typed -Inputs $web.Inputs -Config $config -Facts $facts -Now (Get-Date) -GolemName $web.GolemName
if (-not $r.Ok) { Write-ConsoleLine $(if ($r.Refusal -match '^(Refused|Not now)\b') { $r.Refusal } else { 'Refused: ' + $r.Refusal }) 'amber'; exit 2 }
if ($web.GolemName) { $r.Plan = @(Add-GolemToPlan -Plan $r.Plan -Config $config -Name $web.GolemName) }
$viol = @(Test-PlanSafety $r.Plan $config)
foreach ($l in (Format-Plan -Action $r.ActionKey -ShardKey 'golem' -Plan $r.Plan -Violations $viol -Header $(if ($web.Dry) { 'DRY RUN' } else { 'PLAN' }))) { Write-ConsoleLine $l 'normal' }
if ($web.Dry) { Write-ConsoleLine '   (dry run: nothing was run)' 'amber'; exit 0 }
Write-ConsoleLine '' 'normal'
$script:WindowStepsInline = $true
$ok = Invoke-Plan -Plan $r.Plan -Config $config -ActionKey $r.ActionKey -ShardKey 'golem' -ConfirmLive $r.Phrase
if ($ok) { Write-ConsoleLine ($r.ActionKey + ': done.') 'green'; exit 0 }
if ($script:PlanCancelled) { exit 3 }
exit 1
