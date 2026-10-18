#Requires -Version 5.1
<#
  cc-P144: what the dev tools in scripts\legion\ share. Functions only; it reads nothing when loaded.
  Dot-sources the player package's LegionScripts.ps1 (settings, manifests, links), so both sides read one rule.

  Haven, in one place: docker\haven\services.json, hosts.haven (lan, then tailnet) and its login user, and the
  scripts-dev service's port. Get-HavenTarget tries the LAN address first and the Tailscale name second, so the
  work PC publishes over Tailscale and the tower over the LAN, with nothing hard-coded per PC.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as Windows-1252.
#>

$script:LegionDevRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
. (Join-Path $script:LegionDevRoot 'player-package\payload\app\LegionScripts.ps1')
$script:LegionRepoUrl = 'https://github.com/CainUnAble923/shattered-legacy-scripts.git'

# Haven's addresses, its SSH user and the dev channel's port, from the inventory.
function Get-HavenInfo {
    param([string]$Inventory = (Join-Path $script:LegionDevRoot 'docker\haven\services.json'))
    $j = [IO.File]::ReadAllText($Inventory) | ConvertFrom-Json
    $h = $j.hosts.haven
    $user = 'chase'
    if ([string]$h.login -match 'ssh\s+([a-z_][a-z0-9_-]*)@') { $user = $Matches[1] }
    $dev = @($j.services | Where-Object { $_.key -eq 'scripts-dev' })[0]
    $port = 8097
    if ($dev -and [string]@($dev.ports)[0].lan -match ':(\d+)$') { $port = [int]$Matches[1] }
    [pscustomobject]@{ Lan = [string]$h.lan; Tailnet = [string]$h.tailnet; User = $user; DevPort = $port }
}

function Test-TcpPort {
    param([string]$HostName, [int]$Port, [int]$TimeoutMs = 1500)
    $c = New-Object Net.Sockets.TcpClient
    try {
        $ar = $c.BeginConnect($HostName, $Port, $null, $null)
        return ($ar.AsyncWaitHandle.WaitOne($TimeoutMs) -and $c.Connected)
    } catch { return $false } finally { $c.Close() }
}

# Which address answers SSH: the LAN first, then Tailscale. Ssh is user@host, DevBase the dev channel's URL there.
function Get-HavenTarget {
    param($Info = (Get-HavenInfo))
    foreach ($c in @(@($Info.Lan, 'LAN'), @($Info.Tailnet, 'Tailscale'))) {
        if ($c[0] -and (Test-TcpPort $c[0] 22)) {
            return [pscustomobject]@{ Host = $c[0]; Via = $c[1]; Ssh = ($Info.User + '@' + $c[0]); DevBase = ('http://' + $c[0] + ':' + $Info.DevPort + '/'); Error = '' }
        }
    }
    [pscustomobject]@{ Host = ''; Via = ''; Ssh = ''; DevBase = ''; Error = ('Haven not reachable at ' + $Info.Lan + ' (LAN) or ' + $Info.Tailnet + ' (Tailscale): is Tailscale running?') }
}

# Can this PC publish? '' when the key works, else the sentence to show instead of a raw ssh error.
function Test-HavenKey {
    param($Target)
    if (-not $Target.Ssh) { return $Target.Error }
    $out = (& ssh -o BatchMode=yes -o ConnectTimeout=8 $Target.Ssh 'echo legion-key-ok' 2>&1 | Out-String)
    if ($LASTEXITCODE -eq 0 -and $out -match 'legion-key-ok') { return '' }
    if ($out -match 'Permission denied|publickey') { return ('This PC cannot publish yet: no SSH key on Haven. Click Set up publish key (once), then try again.') }
    if ($out -match 'Host key verification failed') { return ('Haven''s host key is not known on this PC yet: run ssh ' + $Target.Ssh + ' once and accept it.') }
    'Haven not reachable over SSH at ' + $Target.Host + ' (' + $Target.Via + '): is Tailscale running? (' + ($out.Trim() -split "`n")[0] + ')'
}

# The clone against its origin: Dirty (porcelain lines), Ahead, Behind, Head, Error. Fetches first, so a push from
# the other PC is seen.
function Get-RepoSync {
    param([string]$Repo, [switch]$NoFetch)
    $r = [pscustomobject]@{ Repo = $Repo; Dirty = @(); Ahead = 0; Behind = 0; Head = ''; Branch = ''; Error = '' }
    if (-not (Test-Path -LiteralPath (Join-Path $Repo '.git'))) { $r.Error = "$Repo is not a git clone. Click Set up scripts repo first."; return $r }
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try {
        if (-not $NoFetch) {
            $f = (& git -C $Repo fetch --quiet origin 2>&1 | Out-String)
            if ($LASTEXITCODE -ne 0) { $r.Error = "git fetch failed in $Repo`: $($f.Trim())"; return $r }
        }
        $r.Dirty = @(& git -C $Repo status --porcelain --untracked-files=all 2>$null | Where-Object { $_ })
        $r.Head = [string](& git -C $Repo rev-parse HEAD 2>$null)
        $r.Branch = [string](& git -C $Repo rev-parse --abbrev-ref HEAD 2>$null)
        $c = [string](& git -C $Repo rev-list --left-right --count 'HEAD...@{u}' 2>$null)
        if ($LASTEXITCODE -ne 0 -or $c -notmatch '^(\d+)\s+(\d+)$') { $r.Error = "$Repo has no upstream branch to compare with (git push -u origin $($r.Branch) once)."; return $r }
        $r.Ahead = [int]$Matches[1]; $r.Behind = [int]$Matches[2]
    } finally { $ErrorActionPreference = $prev }
    $r
}

# Why the clone cannot be published from, or ''.
function Get-RepoSyncProblem {
    param($Sync)
    if ($Sync.Error) { return $Sync.Error }
    if (@($Sync.Dirty).Count) { return ($Sync.Repo + ' has ' + @($Sync.Dirty).Count + ' uncommitted change(s): ' + ((@($Sync.Dirty) | Select-Object -First 8 | ForEach-Object { $_.Trim() }) -join ', ') + '. Commit and push first, so both PCs match.') }
    if ($Sync.Ahead -and $Sync.Behind) { return ($Sync.Repo + ' and GitHub have both moved (' + $Sync.Ahead + ' ahead, ' + $Sync.Behind + ' behind). Pull scripts, commit and push first, so both PCs match.') }
    if ($Sync.Ahead) { return ($Sync.Repo + ' is ' + $Sync.Ahead + ' commit(s) ahead of GitHub. Commit and push first, so both PCs match.') }
    if ($Sync.Behind) { return ($Sync.Repo + ' is ' + $Sync.Behind + ' commit(s) behind GitHub. Pull scripts first, so both PCs match.') }
    ''
}

function Get-LegionStamp { param([datetime]$When = (Get-Date)) $When.ToString('yyyy.MM.dd.HHmm', [Globalization.CultureInfo]::InvariantCulture) }
function Get-IsoUtc { param([datetime]$When) $When.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture) }

# A new manifest for the files in $Folder, keeping each unchanged file's publishedFrom and published time from
# $Live (so Check scripts says who first published that exact file). Returns Manifest and Diff (New, Changed,
# Unchanged, Removed: names).
function New-LegionManifest {
    param([object[]]$Files, $Live, [string]$Channel, [string]$Machine = $env:COMPUTERNAME, [string]$Commit = '', [datetime]$Now = (Get-Date))
    $liveBy = @{}
    foreach ($f in @($(if ($Live) { $Live.files }))) { if ($f) { $liveBy[[string]$f.name] = $f } }
    $diff = [pscustomobject]@{ New = @(); Changed = @(); Unchanged = @(); Removed = @() }
    $list = @()
    foreach ($f in @($Files | Sort-Object Name)) {
        $sha = Get-LegionSha $f.FullName
        $old = $liveBy[$f.Name]
        $from = $Machine; $when = Get-IsoUtc $Now
        if ($old -and [string]$old.sha256 -eq $sha) {
            $diff.Unchanged += $f.Name
            if ($old.publishedFrom) { $from = [string]$old.publishedFrom }
            if ($old.published) { $when = [string]$old.published }
        } elseif ($old) { $diff.Changed += $f.Name } else { $diff.New += $f.Name }
        $list += [ordered]@{ name = $f.Name; sha256 = $sha; bytes = $f.Length; modified = (Get-IsoUtc $f.LastWriteTime); publishedFrom = $from; published = $when }
    }
    foreach ($n in @($liveBy.Keys | Sort-Object)) { if (@($Files | Where-Object { $_.Name -eq $n }).Count -eq 0) { $diff.Removed += $n } }
    $m = [ordered]@{ version = (Get-LegionStamp $Now); channel = $Channel; publishedFrom = $Machine; published = (Get-IsoUtc $Now); commit = $Commit; files = $list; notes = '' }
    [pscustomobject]@{ Manifest = $m; Diff = $diff }
}

# The new official manifest for a promote: the official set with the picked dev entries in place of theirs, the exact
# dev hashes, publishedFrom and times. Never removes an official script.
function New-PromotedManifest {
    param($Official, $Dev, [string[]]$Names, [string]$Machine = $env:COMPUTERNAME, [datetime]$Now = (Get-Date))
    $by = [ordered]@{}
    foreach ($f in @($(if ($Official) { $Official.files }))) { if ($f) { $by[[string]$f.name] = $f } }
    foreach ($n in $Names) {
        $d = @($Dev.files | Where-Object { [string]$_.name -eq $n })[0]
        if (-not $d) { throw "$n is not on the dev channel" }
        $by[$n] = $d
    }
    $list = @()
    foreach ($k in @($by.Keys | Sort-Object)) {
        $f = $by[$k]
        $list += [ordered]@{ name = [string]$f.name; sha256 = [string]$f.sha256; bytes = [long]$f.bytes; modified = [string]$f.modified; publishedFrom = [string]$f.publishedFrom; published = [string]$f.published }
    }
    [ordered]@{ version = (Get-LegionStamp $Now); channel = 'official'; publishedFrom = $Machine; published = (Get-IsoUtc $Now); commit = [string]$Dev.commit; promotedFrom = [string]$Dev.version; files = $list; notes = '' }
}

# Dev entries that differ from official (or that official lacks), for Promote and Check.
function Get-PromoteCandidates {
    param($Official, $Dev)
    $off = @{}
    foreach ($f in @($(if ($Official) { $Official.files }))) { if ($f) { $off[[string]$f.name] = [string]$f.sha256 } }
    foreach ($d in @($(if ($Dev) { $Dev.files }))) {
        if (-not $d) { continue }
        $o = ''
        if ($off.ContainsKey([string]$d.name)) { $o = $off[[string]$d.name] }
        if ($o -ne [string]$d.sha256) { [pscustomobject]@{ Name = [string]$d.name; Dev = [string]$d.sha256; Official = $o; From = [string]$d.publishedFrom; When = [string]$d.published } }
    }
}

# Writes scripts.json (ASCII, no BOM) and files.txt (sha256sum's format, LF) into $Dir. Returns scripts.json's sha256.
function Write-LegionStage {
    param([string]$Dir, $Manifest)
    New-Item -ItemType Directory -Path $Dir -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $Dir 'scripts.json'), ($Manifest | ConvertTo-Json -Depth 6), (New-Object Text.ASCIIEncoding))
    $lines = @($Manifest.files | ForEach-Object { [string]$_.sha256 + '  ' + [string]$_.name })
    [IO.File]::WriteAllText((Join-Path $Dir 'files.txt'), (($lines -join "`n") + "`n"), (New-Object Text.ASCIIEncoding))
    Get-LegionSha (Join-Path $Dir 'scripts.json')
}

function ConvertTo-PosixPath { param([string]$Path) $p = [IO.Path]::GetFullPath($Path); '/' + $p.Substring(0, 1).ToLower() + $p.Substring(2).Replace('\', '/') }

# Git for Windows' own bash (never WSL's bash.exe in System32): the scratch target runs legion-scripts.sh with it.
function Get-GitBash {
    $git = (Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if ($git) {
        foreach ($rel in @('..\bin\bash.exe', '..\..\bin\bash.exe', '..\usr\bin\bash.exe')) {
            $b = [IO.Path]::GetFullPath((Join-Path (Split-Path $git -Parent) $rel))
            if (Test-Path -LiteralPath $b) { return $b }
        }
    }
    foreach ($b in @('C:\Program Files\Git\bin\bash.exe', 'C:\Program Files (x86)\Git\bin\bash.exe')) { if (Test-Path -LiteralPath $b) { return $b } }
    throw 'Git for Windows'' bash.exe was not found (the scratch target needs it).'
}
