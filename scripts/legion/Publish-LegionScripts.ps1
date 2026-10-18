#Requires -Version 5.1
<#
  Publish-LegionScripts.ps1 (cc-P144): Chase's Legion scripts, from the scripts repo to the two channels on Haven.

      .\Publish-LegionScripts.ps1 -Mode Check       local vs dev vs official, per script; changes nothing
      .\Publish-LegionScripts.ps1 -Mode Dev         publish the clone's scripts to the dev channel (everyday)
      .\Publish-LegionScripts.ps1 -Mode Promote     copy dev scripts, on Haven, into the official channel
      .\Publish-LegionScripts.ps1 -Mode SetupDev    once: the dev channel's web server on Haven
      -DryRun        Dev and Promote stage and check everything, then stop before the swap; SetupDev only prints
      -Only a,b      Promote just these (default: every dev script that differs from official)

  The channels (LegionScripts.ps1 in the player package says what an install does with them):
      official  https://get.shatteredlegacyuo.com/scripts/   public; every install pulls it; new packages ship it
      dev       http://haven.tail81098d.ts.net:8097/         Tailscale only (sl-scripts-dev, docker\scripts-dev);
                                                             only PCs flagged dev pull it, on top of official
  What is published: *.py at the top of the scripts repo clone (this PC's legion-scripts.json says where), except
  API.py and __builtins__.py, which each client writes. Never .log, __pycache__, .csproj, _ScriptContext.cs,
  backup\ or anything in a subfolder; those are listed as skipped.

  Dev refuses unless the clone is committed and level with GitHub ("commit and push first, so both PCs match"):
  that is what keeps the tower and the work PC from overwriting each other. scripts.json records the publishing
  PC (publishedFrom) and the commit. Promote copies the exact dev bytes on Haven, never this PC's files, and then
  tags that commit official-<version> in the repo and pushes the tag.

  Order of work on Haven (legion-scripts.sh beside this file), stopping at the first failure: stage and compare
  SHA-256; back up the served set to ~/backups/legion-scripts/[dev/]<old version>/; swap the scripts in and
  download each back over HTTP; only then scripts.json, downloaded back too; any mismatch puts the old set back.
  A removed script stays in the backup only. Haven is reached by its LAN address, else its Tailscale name
  (docker\haven\services.json); a PC without its SSH key on Haven is told so plainly.

  Test target: -Scratch <folder> -ScratchUrl <http base> runs every step on this PC with Git Bash against
  <folder>\www\official\scripts and <folder>\www\dev, served by whatever answers at -ScratchUrl; -Answer gives the
  prompts' answers in order, as one string with | between them (only with -Scratch). Nothing reaches Haven.

  Exit: 0 done (or nothing to do), 3 declined at a prompt, 1 failed or refused.
  Output from Haven is also written to %LOCALAPPDATA%\ShatteredLegacy\legion-publish-output.txt.
  ASCII only, on purpose (PowerShell 5.1 reads UTF-8 without a BOM as Windows-1252).
#>
[CmdletBinding()]
param(
    [ValidateSet('Check', 'Dev', 'Promote', 'SetupDev')][string]$Mode = 'Check',
    [switch]$DryRun,
    [string[]]$Only,
    [string]$SettingsPath,
    [string]$Scratch,
    [string]$ScratchUrl,
    [string[]]$Answer
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LegionDev.ps1')
if (-not $SettingsPath) { $SettingsPath = Get-LegionSettingsPath }
$sh = Join-Path $PSScriptRoot 'legion-scripts.sh'
$outFile = Join-Path (Split-Path (Get-LegionSettingsPath) -Parent) 'legion-publish-output.txt'
if ($Scratch) { $outFile = Join-Path $Scratch 'publish-output.txt' }
$script:answers = New-Object 'System.Collections.Generic.Queue[string]'
if ($Answer) {
    if (-not $Scratch) { Write-Host 'STOPPED: -Answer works only with -Scratch.' -ForegroundColor Red; exit 1 }
    # One string with | between answers too, since powershell -File passes no arrays.
    foreach ($a in @($Answer | ForEach-Object { $_ -split '\|' })) { $script:answers.Enqueue($a) }
}

function Say([string]$m, [string]$c = 'Gray') { Write-Host "  $m" -ForegroundColor $c }
function Stop-Here([string]$m) { Write-Host "STOPPED: $m" -ForegroundColor Red; exit 1 }
function Ask([string]$q) {
    if ($script:answers.Count) { $a = $script:answers.Dequeue(); Write-Host "$q $a"; return $a }
    if ($Scratch) { Stop-Here "no -Answer left for: $q" }
    Read-Host $q
}
function Short([string]$sha) { if ($sha) { $sha.Substring(0, [Math]::Min(12, $sha.Length)) } else { '-' } }

# --- where things are -------------------------------------------------------------------------------------------------
$settings = Read-LegionSettings -Path $SettingsPath
if ($Scratch) {
    if (-not $ScratchUrl) { Stop-Here '-Scratch needs -ScratchUrl (the http base serving <scratch>\www).' }
    $base = $ScratchUrl.TrimEnd('/') + '/'
    $officialBase = $base + 'official/scripts/'
    $target = [pscustomobject]@{ Host = 'scratch'; Via = 'scratch'; Ssh = ''; DevBase = ($base + 'dev/'); Error = '' }
} else {
    $officialBase = $script:LegionOfficialBase
    $target = $null
}
function Get-Target {
    if (-not $script:target) {
        $script:target = Get-HavenTarget
        if ($script:target.Host) { Say ("Haven: " + $script:target.Host + ' (' + $script:target.Via + ')') }
    }
    $script:target
}

# Runs legion-scripts.sh on the target with the staged folder. Returns its exit code; its output is shown and kept.
function Invoke-Remote {
    param([string]$StageDir, [string[]]$ShArgs, [switch]$Dry)
    if ($Scratch) {
        $remoteStage = Join-Path $Scratch 'stage'
        if (Test-Path -LiteralPath $remoteStage) { Remove-Item -LiteralPath $remoteStage -Recurse -Force }
        Copy-Item -LiteralPath $StageDir -Destination $remoteStage -Recurse
        Copy-Item -LiteralPath $sh -Destination (Join-Path $remoteStage 'legion-scripts.sh')
        $env:STAGE = ConvertTo-PosixPath $remoteStage
        $env:BAK_ROOT = ConvertTo-PosixPath (Join-Path $Scratch 'backups')
        $env:DEV_DIR = ConvertTo-PosixPath (Join-Path $Scratch 'www\dev')
        $env:OFFICIAL_DIR = ConvertTo-PosixPath (Join-Path $Scratch 'www\official\scripts')
        $env:DEV_URL = $target.DevBase.TrimEnd('/')
        $env:OFFICIAL_URL = $officialBase.TrimEnd('/')
        $env:OFFICIAL_RESOLVE = 'none'
        $env:DRYRUN = $(if ($Dry) { '1' } else { '0' })
        $cmd = "cd '" + $env:STAGE + "' && bash legion-scripts.sh " + ($ShArgs -join ' ')
        $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        & (Get-GitBash) -c $cmd 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath $outFile | Out-Host
        $rc = $LASTEXITCODE
        $ErrorActionPreference = $prev
        foreach ($v in 'STAGE', 'BAK_ROOT', 'DEV_DIR', 'OFFICIAL_DIR', 'DEV_URL', 'OFFICIAL_URL', 'OFFICIAL_RESOLVE', 'DRYRUN') { Remove-Item -LiteralPath ('Env:' + $v) -ErrorAction SilentlyContinue }
        return $rc
    }
    $t = Get-Target
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try {
        & ssh $t.Ssh 'rm -rf ~/legion-staging; mkdir -p ~/legion-staging'
        if ($LASTEXITCODE -ne 0) { Stop-Here 'could not make ~/legion-staging on Haven' }
        $files = @(Get-ChildItem -LiteralPath $StageDir -File | ForEach-Object { $_.FullName }) + @($sh)
        & scp -q @files ($t.Ssh + ':legion-staging/')
        if ($LASTEXITCODE -ne 0) { Stop-Here 'copy to Haven''s staging folder failed' }
        $dryPrefix = $(if ($Dry) { 'DRYRUN=1 ' } else { '' })   # not $dry: PowerShell names are case-insensitive, so that was the [switch]$Dry param
        & ssh $t.Ssh ("cd ~/legion-staging && sed -i 's/\r$//' legion-scripts.sh && " + $dryPrefix + 'bash legion-scripts.sh ' + ($ShArgs -join ' ')) 2>&1 |
            ForEach-Object { "$_" } | Tee-Object -FilePath $outFile | Out-Host
        return $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
}

function Get-Manifests {
    $off = Get-LegionManifest -Base $officialBase -TimeoutSec 10
    $t = $target
    if (-not $t) { $t = Get-Target }
    $dev = [pscustomobject]@{ Manifest = $null; Error = $t.Error; Missing = $false; Url = '' }
    if ($t.DevBase) { $dev = Get-LegionManifest -Base $t.DevBase -TimeoutSec 10 }
    [pscustomobject]@{ Official = $off; Dev = $dev }
}

function Show-Header([string]$what) {
    Write-Host ''
    Write-Host ("  " + $what + ' from ' + $env:COMPUTERNAME) -ForegroundColor Cyan
    Say ('This PC''s settings: ' + $settings.Path + '  (dev ' + $(if ($settings.Dev) { 'on' } else { 'off' }) + ', repo ' + $(if ($settings.Repo) { $settings.Repo } else { 'not set up' }) + ')')
}

function Show-Table($Local, $Dev, $Off) {
    $names = @()
    $names += @($Local.Keys); $names += @($Dev.Keys); $names += @($Off.Keys)
    $names = @($names | Sort-Object -Unique)
    if (-not $names.Count) { Say 'No scripts anywhere yet.'; return }
    Write-Host ('  {0,-26} {1,-13} {2,-13} {3,-13} {4}' -f 'script', 'local (repo)', 'dev', 'official', 'published from')
    foreach ($n in $names) {
        $l = $(if ($Local.ContainsKey($n)) { $Local[$n] } else { '' })
        $d = $Dev[$n]; $o = $Off[$n]
        $ds = $(if ($d) { Short $d.sha256 } else { '-' }); $os = $(if ($o) { Short $o.sha256 } else { '-' })
        $who = @()
        if ($d) { $who += ('dev: ' + $d.publishedFrom + ' ' + $d.published) }
        if ($o) { $who += ('official: ' + $o.publishedFrom + ' ' + $o.published) }
        $c = 'Gray'
        if ($l -and $d -and $l -ne [string]$d.sha256) { $c = 'Yellow' }
        if ($d -and (-not $o -or [string]$o.sha256 -ne [string]$d.sha256)) { $c = 'Cyan' }
        Write-Host ('  {0,-26} {1,-13} {2,-13} {3,-13} {4}' -f $n, (Short $l), $ds, $os, ($who -join '; ')) -ForegroundColor $c
    }
    Say 'Cyan: dev differs from official (Promote to official). Yellow: your repo differs from dev (Publish to dev).' DarkGray
}

function ConvertTo-ByName($m) {
    $h = @{}
    if ($m) { foreach ($f in @($m.files)) { if ($f) { $h[[string]$f.name] = $f } } }
    $h
}

# ======================================================================================================================
if ($Mode -eq 'Check') {
    Show-Header 'Check scripts'
    $local = @{}
    if (Test-LegionRepo $settings) {
        $sync = Get-RepoSync -Repo $settings.Repo
        $p = Get-RepoSyncProblem $sync
        if ($p) { Say ('Repo: ' + $p) Yellow } else { Say ('Repo: ' + $settings.Repo + ' is committed and level with GitHub at ' + (Short $sync.Head) + '.') Green }
        $src = Get-LegionSourceFiles $settings.Repo
        foreach ($f in @($src.Files)) { $local[$f.Name] = Get-LegionSha $f.FullName }
        if (@($src.Skipped).Count) { Say ('Not published from the repo: ' + (@($src.Skipped) -join ', ')) DarkGray }
    } else { Say 'No scripts repo on this PC: Set up scripts repo makes one.' Yellow }
    $m = Get-Manifests
    foreach ($pair in @(@('official', $m.Official), @('dev', $m.Dev))) {
        $r = $pair[1]
        if ($r.Manifest) { Say ($pair[0] + ': version ' + $r.Manifest.version + ', published from ' + $r.Manifest.publishedFrom + ' at ' + $r.Manifest.published + $(if ($r.Manifest.commit) { ', commit ' + (Short ([string]$r.Manifest.commit)) } else { '' }) + '  (' + $r.Url + ')') }
        else { Say ($pair[0] + ': ' + $r.Error) Yellow }
    }
    Write-Host ''
    Show-Table $local (ConvertTo-ByName $m.Dev.Manifest) (ConvertTo-ByName $m.Official.Manifest)
    Write-Host ''
    if (-not $Scratch) {
        $t = Get-Target
        $why = Test-HavenKey $t
        if ($why) { Say ('Publishing: ' + $why) Yellow } else { Say ('Publishing: this PC can publish (SSH key accepted by Haven over ' + $t.Via + ').') Green }
    }
    exit 0
}

# ======================================================================================================================
if ($Mode -eq 'SetupDev') {
    Show-Header 'Set up the dev channel on Haven'
    $compose = Join-Path $script:LegionDevRoot 'docker\scripts-dev\docker-compose.yml'
    Say ('1. ~/sl-scripts-dev/html (the served folder) and ~/sl-scripts-dev/stack/docker-compose.yml from ' + $compose)
    Say '2. docker compose -p sl-scripts-dev up -d: nginx on 192.168.1.61:8097 and 127.0.0.1:8097 only, never 0.0.0.0'
    Say '3. check http://127.0.0.1:8097/ answers on Haven'
    Say '4. afterwards: Websites > Mega dashboard: deploy adds Tailscale Serve for :8097 (services.json tailscaleServe).'
    if ($DryRun -or $Scratch) { Say 'DRY RUN: stopping here; nothing was changed.' Yellow; exit 0 }
    if ((Ask 'Do the above on Haven? Type yes') -ne 'yes') { Say 'Nothing done.'; exit 3 }
    $t = Get-Target
    $why = Test-HavenKey $t
    if ($why) { Stop-Here $why }
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & ssh $t.Ssh 'mkdir -p ~/sl-scripts-dev/html ~/sl-scripts-dev/stack ~/backups/legion-scripts/dev'
    if ($LASTEXITCODE -ne 0) { Stop-Here 'mkdir on Haven' }
    & scp -q $compose ($t.Ssh + ':sl-scripts-dev/stack/docker-compose.yml')
    if ($LASTEXITCODE -ne 0) { Stop-Here 'copy the compose file' }
    & ssh $t.Ssh 'cd ~/sl-scripts-dev/stack && sed -i ''s/\r$//'' docker-compose.yml && docker compose -p sl-scripts-dev up -d && sleep 2 && curl -s -o /dev/null -w "http://127.0.0.1:8097/ answers %{http_code}\n" http://127.0.0.1:8097/' 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath $outFile | Out-Host
    $rc = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($rc -ne 0) { Stop-Here "the dev channel did not start (see above; $outFile)" }
    Say 'The dev channel is up on Haven. Next: Websites > Mega dashboard: deploy, for Tailscale Serve on :8097.' Green
    exit 0
}

# ======================================================================================================================
if ($Mode -eq 'Dev') {
    Show-Header 'Publish to dev'
    if (-not (Test-LegionRepo $settings)) { Stop-Here 'no scripts repo on this PC. Click Set up scripts repo first.' }
    $sync = Get-RepoSync -Repo $settings.Repo
    $p = Get-RepoSyncProblem $sync
    if ($p) { Stop-Here $p }
    Say ('Repo: ' + $settings.Repo + ' at ' + (Short $sync.Head) + ', committed and level with GitHub.')
    $src = Get-LegionSourceFiles $settings.Repo
    if (-not @($src.Files).Count) { Stop-Here ('no .py scripts at the top of ' + $settings.Repo) }
    if (@($src.Skipped).Count) { Say ('Skipped (never published): ' + (@($src.Skipped) -join ', ')) DarkGray }
    if (-not $Scratch) {
        $t = Get-Target
        $why = Test-HavenKey $t
        if ($why) { Stop-Here $why }
    }
    $live = Get-LegionManifest -Base $target.DevBase -TimeoutSec 10
    if (-not $live.Manifest -and -not $live.Missing) { Stop-Here ('the dev channel''s scripts.json could not be read: ' + $live.Error + '. Is the dev channel set up (Publish-LegionScripts.ps1 -Mode SetupDev)?') }
    if ($live.Missing) { Say 'The dev channel has nothing yet: this is its first publish.' }
    else { Say ('Live on dev: version ' + $live.Manifest.version + ', from ' + $live.Manifest.publishedFrom + ' at ' + $live.Manifest.published) }
    $new = New-LegionManifest -Files $src.Files -Live $live.Manifest -Channel 'dev' -Commit $sync.Head
    $d = $new.Diff
    Write-Host ''
    foreach ($n in $d.New) { Say ("new        $n") Green }
    foreach ($n in $d.Changed) {
        $o = @($live.Manifest.files | Where-Object { $_.name -eq $n })[0]
        Say ("changed    $n   (was " + (Short ([string]$o.sha256)) + ' from ' + $o.publishedFrom + ' at ' + $o.published + ')') Cyan
    }
    foreach ($n in $d.Unchanged) { Say ("unchanged  $n") DarkGray }
    foreach ($n in $d.Removed) { Say ("removed    $n   (stays in Haven's backup only)") Yellow }
    if (-not ($d.New.Count + $d.Changed.Count + $d.Removed.Count)) { Say 'Nothing changed since the last dev publish. Nothing to do.' Green; exit 0 }
    Write-Host ''
    if ((Ask 'Publish these to the dev channel (your PCs only)? Type yes') -ne 'yes') { Say 'Nothing done.'; exit 3 }
    $stage = Join-Path ([IO.Path]::GetTempPath()) ('legion-stage-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    try {
        $want = Write-LegionStage -Dir $stage -Manifest $new.Manifest
        foreach ($f in @($src.Files)) { if ($d.New -contains $f.Name -or $d.Changed -contains $f.Name) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $stage $f.Name) } }
        Say ('Staging ' + ($d.New.Count + $d.Changed.Count) + ' script(s) and scripts.json ' + (Short $want) + ' on ' + $target.Host + '...')
        $rc = Invoke-Remote -StageDir $stage -ShArgs @('publish', 'dev', $want) -Dry:$DryRun
    } finally { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
    if ($rc -ne 0) { Write-Host "NOT PUBLISHED (see above). Output: $outFile" -ForegroundColor Red; exit 1 }
    if ($DryRun) { Say 'DRY RUN: nothing served changed.' Yellow; exit 0 }
    $back = Get-LegionManifest -Base $target.DevBase -TimeoutSec 10
    if (-not $back.Manifest -or [string]$back.Manifest.version -ne [string]$new.Manifest.version) { Write-Host ('PUBLISHED on Haven, but this PC reads back ' + $(if ($back.Manifest) { 'version ' + $back.Manifest.version } else { $back.Error }) + ', not ' + $new.Manifest.version + '. Check scripts again in a minute.') -ForegroundColor Yellow; exit 1 }
    Write-Host ('PUBLISHED to dev: version ' + $new.Manifest.version + ', read back from this PC. Your other PC picks it up on its next start (Tailscale on).') -ForegroundColor Green
    exit 0
}

# ======================================================================================================================
if ($Mode -eq 'Promote') {
    Show-Header 'Promote to official'
    if (-not $Scratch) {
        $t = Get-Target
        $why = Test-HavenKey $t
        if ($why) { Stop-Here $why }
    }
    $m = Get-Manifests
    if (-not $m.Dev.Manifest) { Stop-Here ('the dev channel could not be read: ' + $m.Dev.Error) }
    if (-not $m.Official.Manifest -and -not $m.Official.Missing) { Stop-Here ('the official channel could not be read: ' + $m.Official.Error) }
    $cands = @(Get-PromoteCandidates -Official $m.Official.Manifest -Dev $m.Dev.Manifest)
    if (-not $cands.Count) { Say 'Every dev script is already official. Nothing to promote.' Green; exit 0 }
    Say ('dev ' + $m.Dev.Manifest.version + ' (commit ' + (Short ([string]$m.Dev.Manifest.commit)) + ')  ->  official ' + $(if ($m.Official.Manifest) { $m.Official.Manifest.version } else { '(nothing yet)' }))
    Write-Host ''
    $i = 0
    foreach ($c in $cands) {
        $i++
        Write-Host ('  {0,2}. {1,-26} dev {2}  official {3}  from {4} at {5}' -f $i, $c.Name, $c.Dev, $(if ($c.Official) { Short $c.Official } else { '(new)' }), $c.From, $c.When)
    }
    Write-Host ''
    $pick = @($cands | ForEach-Object { $_.Name })
    if ($Only) {
        $pick = @($Only | ForEach-Object { $_.Split(',') } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    } else {
        $a = Ask 'Promote which? Enter (or all) for all, or numbers or names separated by commas'
        if ($a.Trim() -and $a.Trim() -ne 'all') {
            $pick = @()
            foreach ($x in $a.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) {
                if ($x -match '^\d+$' -and [int]$x -ge 1 -and [int]$x -le $cands.Count) { $pick += $cands[[int]$x - 1].Name } else { $pick += $x }
            }
        }
    }
    foreach ($n in $pick) { if (@($cands | Where-Object { $_.Name -eq $n }).Count -eq 0) { Stop-Here "$n is not a dev script that differs from official." } }
    $pick = @($pick | Sort-Object -Unique)
    Say ('Promoting: ' + ($pick -join ', ')) Cyan
    if ((Ask 'Copy these dev scripts to the official channel, which every player''s install pulls? Type yes') -ne 'yes') { Say 'Nothing done.'; exit 3 }
    $newOff = New-PromotedManifest -Official $m.Official.Manifest -Dev $m.Dev.Manifest -Names $pick
    $stage = Join-Path ([IO.Path]::GetTempPath()) ('legion-stage-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    try {
        $want = Write-LegionStage -Dir $stage -Manifest $newOff
        $rc = Invoke-Remote -StageDir $stage -ShArgs (@('promote', $want) + $pick) -Dry:$DryRun
    } finally { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
    if ($rc -ne 0) { Write-Host "NOT PROMOTED (see above). Output: $outFile" -ForegroundColor Red; exit 1 }
    if ($DryRun) { Say 'DRY RUN: nothing served changed, no tag made.' Yellow; exit 0 }
    $back = Get-LegionManifest -Base $officialBase -TimeoutSec 10
    if (-not $back.Manifest -or [string]$back.Manifest.version -ne [string]$newOff.version) { Write-Host ('PROMOTED on Haven, but this PC reads back ' + $(if ($back.Manifest) { 'version ' + $back.Manifest.version } else { $back.Error }) + '. Check scripts again in a minute.') -ForegroundColor Yellow }
    else { Write-Host ('PROMOTED to official: version ' + $newOff.version + ', read back from this PC. Every install picks it up on its next start.') -ForegroundColor Green }
    # The tag: official-<version> on the commit the dev set was published from.
    $tag = 'official-' + $newOff.version
    $commit = [string]$m.Dev.Manifest.commit
    if (-not (Test-LegionRepo $settings) -or -not $commit) { Say ("No tag made: " + $(if (-not $commit) { 'the dev set records no commit' } else { 'no scripts repo on this PC' }) + ". On a PC with the repo: git tag -a $tag $commit; git push origin $tag") Yellow; exit 0 }
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & git -C $settings.Repo fetch --quiet origin 2>&1 | Out-Null
    $o1 = (& git -C $settings.Repo tag -a $tag $commit -m ("Promoted to official " + $newOff.version + " from dev " + $m.Dev.Manifest.version + " on " + $env:COMPUTERNAME + ": " + ($pick -join ', ')) 2>&1 | Out-String)
    $ok1 = ($LASTEXITCODE -eq 0)
    $o2 = ''
    if ($ok1) { $o2 = (& git -C $settings.Repo push --quiet origin $tag 2>&1 | Out-String) }
    $ok2 = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = $prev
    if ($ok1 -and $ok2) { Say ("Tagged $tag on " + (Short $commit) + ' and pushed the tag.') Green }
    else { Say ("The tag did not go through (" + ($o1 + $o2).Trim() + "). By hand: git -C " + $settings.Repo + " tag -a $tag $commit; git -C " + $settings.Repo + " push origin $tag") Yellow }
    exit 0
}
