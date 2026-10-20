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

  Exit 0 done, 1 failed, 2 refused (nothing run), 3 cancelled. Never prints a secret: the Golem's scripts print none,
  and server.py redacts every line before it reaches a page or the action log. ASCII only.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Key, [string]$Typed = '', [string]$Model, [string]$Cap, [string]$Until,
    [string]$NightStart, [string]$NightEnd, [string]$Prompt, [string]$Golem, [switch]$DryRun, [string]$RepoRoot, [string]$ActionsFile)
$ErrorActionPreference = 'Stop'
# Kept under names of their own: dot-sourcing the console runs its param block here, which resets its own names.
$web = [pscustomobject]@{ Key = $Key; Typed = $Typed; Dry = [bool]$DryRun
    Inputs = @{}; Root = $(if ($RepoRoot) { $RepoRoot } else { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path })
    Actions = $ActionsFile; Golem = $Golem; GolemName = '' }
foreach ($p in @(@('model', $Model), @('cap', $Cap), @('until', $Until), @('start', $NightStart), @('end', $NightEnd), @('prompt', $Prompt))) {
    if ($null -ne $p[1] -and [string]$p[1] -ne '') { $web.Inputs[$p[0]] = [string]$p[1] }
}
if (-not $web.Actions) { $web.Actions = Join-Path $web.Root 'web-console\golem-actions.json' }
$env:SHARD_CONSOLE_PANE = '1'
. (Join-Path $PSScriptRoot 'Shard-Console.ps1') -LoadOnly
$config = Get-ConsoleConfig -RepoRoot $web.Root

$entry = @((Get-Content -LiteralPath $web.Actions -Raw | ConvertFrom-Json).actions | Where-Object { $_.key -ceq $web.Key }) | Select-Object -First 1
if (-not $entry) { Write-ConsoleLine ('REFUSED: no such button: ' + $web.Key) 'red'; exit 2 }
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
$r = Get-WebActionPlan -Entry $entry -Typed $web.Typed -Inputs $web.Inputs -Config $config -Facts $facts -Now (Get-Date)
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
