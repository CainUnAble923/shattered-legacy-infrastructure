#Requires -Version 5.1
<#
  Legion scripts (cc-P144): the published script sets, what an install has, and the dev PCs' repo links.
  Functions only; it reads nothing when loaded. Dot-sourced by Launch-Settings.ps1 (so Play.ps1 has it),
  Update-Scripts.ps1, Build-PlayerPackage.ps1 and the dev tools in scripts\legion\.
  player-package\tests\Legion-Scripts.Tests.ps1 calls the same functions.

  Two channels, each a folder of .py files and a scripts.json describing them:
      official  https://get.shatteredlegacyuo.com/scripts/          every install, and new packages
      dev       http://haven.tail81098d.ts.net:8097/                Chase's PCs only (Tailscale only)
  scripts.json: { version, channel, publishedFrom, commit, files: [ { name, sha256, bytes, modified,
  publishedFrom, published } ], notes }.

  What an install has: app\tazuo\LegionScripts\.sl-scripts.json records the SHA-256 of each script as the
  updater last put it there. A script whose hash matches neither that record nor the new one was edited by
  the player; it is copied to LegionScripts\backup\<name>.<yyyyMMdd-HHmm>.py before it is replaced. A
  script the manifest does not list is never touched.

  This PC's own settings, never in the package and never committed: %LOCALAPPDATA%\ShatteredLegacy\
  legion-scripts.json (SL_LEGION_SETTINGS moves it, for tests). { dev, repo, shardRepo, devUrl,
  declinedSetup }. A file holding just the word dev is the one-line form: dev on, nothing else set.
  On a PC with a scripts repo (Setup-ScriptsRepo.ps1), every TazUO client's LegionScripts is a directory
  junction to that clone, and the updater leaves it alone: the scripts come from git.

  ASCII only, on purpose: PowerShell 5.1 reads a UTF-8 file without a BOM as Windows-1252.
#>

$script:LegionOfficialBase = 'https://get.shatteredlegacyuo.com/scripts/'
$script:LegionDevBase      = 'http://haven.tail81098d.ts.net:8097/'
$script:LegionRecordName   = '.sl-scripts.json'
# Each client writes these two itself; neither is ever published, installed or linked.
$script:LegionClientOwned  = @('API.py', '__builtins__.py')

function Get-LegionSettingsPath {
    if ($env:SL_LEGION_SETTINGS) { return $env:SL_LEGION_SETTINGS }
    Join-Path $env:LOCALAPPDATA 'ShatteredLegacy\legion-scripts.json'
}

# This PC's settings, with every field present. A missing or unreadable file is a player's PC: dev off.
function Read-LegionSettings {
    param([string]$Path = (Get-LegionSettingsPath))
    $s = [pscustomobject]@{ Dev = $false; Repo = ''; ShardRepo = ''; DevUrl = $script:LegionDevBase; DeclinedSetup = $false; Path = $Path }
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $s }
    $text = ''
    try { $text = [IO.File]::ReadAllText($Path).Trim().TrimStart([char]0xFEFF) } catch { return $s }
    if ($text -ieq 'dev') { $s.Dev = $true; return $s }
    try { $j = $text | ConvertFrom-Json } catch { return $s }
    if ($null -eq $j) { return $s }
    if ($j.dev -eq $true) { $s.Dev = $true }
    if ($j.repo) { $s.Repo = [string]$j.repo }
    if ($j.shardRepo) { $s.ShardRepo = [string]$j.shardRepo }
    if ($j.devUrl) { $s.DevUrl = [string]$j.devUrl }
    if ($j.declinedSetup -eq $true) { $s.DeclinedSetup = $true }
    $s
}

function Write-LegionSettings {
    param($Settings, [string]$Path = (Get-LegionSettingsPath))
    $o = [ordered]@{ dev = [bool]$Settings.Dev; repo = [string]$Settings.Repo; shardRepo = [string]$Settings.ShardRepo; devUrl = [string]$Settings.DevUrl; declinedSetup = [bool]$Settings.DeclinedSetup }
    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [IO.File]::WriteAllText($Path, ($o | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
}

# True when this PC has a usable scripts repo: the settings name one and it is a git clone.
function Test-LegionRepo {
    param($Settings)
    [bool]($Settings.Repo -and (Test-Path -LiteralPath (Join-Path $Settings.Repo '.git')))
}

function Get-LegionSha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLower() }

# The scripts a folder publishes: *.py at its top level, never the two each client writes, never anything in a
# subfolder. Returns Files (FileInfo) and Skipped (the other top-level names, for the report).
function Get-LegionSourceFiles {
    param([string]$Folder)
    $files = @(); $skipped = @()
    foreach ($i in @(Get-ChildItem -LiteralPath $Folder -Force | Sort-Object Name)) {
        if ($i.Name -eq '.git') { continue }
        if ($i.PSIsContainer) { $skipped += ($i.Name + '\'); continue }
        if ($i.Extension -ieq '.py' -and $script:LegionClientOwned -notcontains $i.Name) { $files += $i } else { $skipped += $i.Name }
    }
    [pscustomobject]@{ Files = $files; Skipped = $skipped }
}

# Why a manifest cannot be trusted, or an empty list. Names are plain file names, so nothing a manifest says can
# reach outside LegionScripts.
function Test-LegionManifest {
    param($Manifest)
    $p = @()
    if ($null -eq $Manifest) { return @('no manifest') }
    if ("$($Manifest.version)" -notmatch '^\d{4}\.\d{2}\.\d{2}\.\d{4}$') { $p += "version '$($Manifest.version)' is not yyyy.MM.dd.HHmm" }
    $seen = @{}
    foreach ($f in @($Manifest.files)) {
        if ($null -eq $f) { continue }
        $n = [string]$f.name
        if ($n -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]*\.py$') { $p += "file name '$n' is not a plain .py name"; continue }
        if ($script:LegionClientOwned -contains $n) { $p += "$n is written by each client and is never published" }
        if ($seen.ContainsKey($n.ToLowerInvariant())) { $p += "$n is listed twice" }
        $seen[$n.ToLowerInvariant()] = $true
        if ("$($f.sha256)" -notmatch '^[0-9a-f]{64}$') { $p += "$n has no sha256" }
        if (-not ("$($f.bytes)" -as [long]) -and "$($f.bytes)" -ne '0') { $p += "$n has no byte count" }
    }
    $p
}

# Fetches a scripts.json. Returns Manifest, Error ('' when fine) and Missing (the server answered 404: nothing has
# been published on that channel yet). Never throws. The request runs on a second runspace and is abandoned after
# $TimeoutSec, because Invoke-WebRequest's own timeout does not cover the DNS lookup (Play.ps1's Get-RemotePackage).
function Get-LegionManifest {
    param([string]$Base, [int]$TimeoutSec = 4)
    $r = [pscustomobject]@{ Manifest = $null; Error = ''; Missing = $false; Url = '' }
    try {
        $r.Url = $Base.TrimEnd('/') + '/scripts.json'
        $ps = [PowerShell]::Create()
        [void]$ps.AddScript({
            param($u, $t)
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            try {
                $w = Invoke-WebRequest -Uri ($u + '?t=' + [DateTime]::UtcNow.Ticks) -UseBasicParsing -TimeoutSec $t
                @{ Text = [Text.Encoding]::UTF8.GetString($w.RawContentStream.ToArray()); Code = 200 }
            } catch {
                $code = 0
                try { $code = [int]$_.Exception.Response.StatusCode } catch {}
                @{ Text = ''; Code = $code; Error = $_.Exception.Message }
            }
        }).AddArgument($r.Url).AddArgument($TimeoutSec)
        $h = $ps.BeginInvoke()
        if (-not $h.AsyncWaitHandle.WaitOne($TimeoutSec * 1000 + 500)) { $r.Error = "no answer from $($r.Url) in $TimeoutSec s"; return $r }
        $o = @($ps.EndInvoke($h))[0]
        $ps.Dispose()
        if ($o.Code -eq 404) { $r.Missing = $true; $r.Error = "nothing published yet ($($r.Url) is 404)"; return $r }
        if ($o.Code -ne 200) { $r.Error = "$($r.Url): $($o.Error)"; return $r }
        $j = $o.Text.TrimStart([char]0xFEFF) | ConvertFrom-Json
        $bad = @(Test-LegionManifest $j)
        if ($bad.Count) { $r.Error = "$($r.Url) is not a usable manifest: $($bad -join '; ')"; return $r }
        $r.Manifest = $j
    } catch { $r.Error = "$($r.Url): $($_.Exception.Message)" }
    $r
}

# Official first, then dev on top: a dev script replaces the official copy of the same name. Each entry carries
# its channel and the base URL it downloads from.
function Merge-LegionManifests {
    param($Official, [string]$OfficialBase, $Dev, [string]$DevBase)
    $by = [ordered]@{}
    foreach ($pair in @(@($Official, $OfficialBase, 'official'), @($Dev, $DevBase, 'dev'))) {
        if (-not $pair[0]) { continue }
        foreach ($f in @($pair[0].files)) {
            if (-not $f) { continue }
            $by[([string]$f.name).ToLowerInvariant()] = [pscustomobject]@{
                Name = [string]$f.name; Sha256 = ([string]$f.sha256).ToLower(); Bytes = [long]$f.bytes
                Version = [string]$pair[0].version; Channel = $pair[2]; Url = ($pair[1].TrimEnd('/') + '/' + [string]$f.name)
            }
        }
    }
    @($by.Values)
}

function Read-LegionRecord {
    param([string]$Folder)
    $rec = @{}
    $p = Join-Path $Folder $script:LegionRecordName
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { return $rec }
    try {
        $j = [IO.File]::ReadAllText($p) | ConvertFrom-Json
        foreach ($prop in @($j.installed.PSObject.Properties)) { $rec[$prop.Name] = $prop.Value }
    } catch {}
    $rec
}

function Write-LegionRecord {
    param([string]$Folder, [hashtable]$Record)
    $o = [ordered]@{ schema = 1; about = 'cc-P144: the SHA-256 of each Legion script as the Shattered Legacy updater last installed it. A script that matches neither this nor the published one was edited here, and is backed up before it is replaced.'; installed = [ordered]@{} }
    foreach ($k in @($Record.Keys | Sort-Object)) { $o.installed[$k] = $Record[$k] }
    [IO.File]::WriteAllText((Join-Path $Folder $script:LegionRecordName), ($o | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
}

# A directory junction or symbolic link, broken or not. Test-Path says False for a broken one, so the
# attributes of the link itself are read.
function Test-LegionLink {
    param([string]$Path)
    try { return [bool]([IO.File]::GetAttributes($Path) -band [IO.FileAttributes]::ReparsePoint) } catch { return $false }
}

function Get-LegionLinkTarget {
    param([string]$Path)
    try {
        $i = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
        $t = @($i.Target)[0]
        if ($t) { return ([string]$t).TrimEnd('\') }
    } catch {}
    ''
}

# The plan for one folder: for each entry, what the updater does. Action: same, install (missing), update
# (the copy is the one last installed, or unknown and unrecorded), backup (edited here: backed up, then updated).
function Get-LegionFolderPlan {
    param([string]$Folder, [object[]]$Entries)
    $rec = Read-LegionRecord $Folder
    foreach ($e in @($Entries)) {
        $dest = Join-Path $Folder $e.Name
        $have = ''
        if (Test-Path -LiteralPath $dest -PathType Leaf) { $have = Get-LegionSha $dest }
        $last = ''
        if ($rec.ContainsKey($e.Name)) { $last = [string]$rec[$e.Name].sha256 }
        $act = 'same'
        if (-not $have) { $act = 'install' }
        elseif ($have -ne $e.Sha256) {
            # No record (an install from before cc-P144, or a client the updater never touched): a copy that is not
            # the published one is treated as the player's, and kept.
            if ($last -and $have -eq $last) { $act = 'update' } else { $act = 'backup' }
        }
        [pscustomobject]@{ Entry = $e; Dest = $dest; Have = $have; Last = $last; Action = $act }
    }
}

# Puts $Entries into $Folder. $Fetch is a scriptblock: param($entry, $tempPath) that writes the entry's bytes to
# $tempPath (a download, or a copy from another folder). Every file is checked for size and SHA-256 before it is put
# in place; one that does not match is not applied and is reported. Returns one line per change, and Failed.
function Install-LegionSet {
    param([string]$Folder, [object[]]$Entries, [scriptblock]$Fetch, [string]$Stamp = (Get-Date -Format 'yyyyMMdd-HHmm'))
    $lines = @(); $failed = @()
    if (Test-LegionLink $Folder) { return [pscustomobject]@{ Lines = @(); Failed = @(); Linked = $true } }
    $plan = @(Get-LegionFolderPlan -Folder $Folder -Entries $Entries)
    $rec = Read-LegionRecord $Folder
    $recChanged = $false
    foreach ($p in $plan) {
        $e = $p.Entry
        if ($p.Action -eq 'same') {
            if (-not $rec.ContainsKey($e.Name) -or [string]$rec[$e.Name].sha256 -ne $e.Sha256) { $rec[$e.Name] = [ordered]@{ sha256 = $e.Sha256; version = $e.Version; channel = $e.Channel }; $recChanged = $true }
            continue
        }
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ('sl-script-' + [Guid]::NewGuid().ToString('N') + '.py')
        try {
            & $Fetch $e $tmp
            $len = (Get-Item -LiteralPath $tmp).Length
            $sha = Get-LegionSha $tmp
            if ($len -ne $e.Bytes -or $sha -ne $e.Sha256) {
                $failed += "$($e.Name): the download is not the published file ($len bytes, expected $($e.Bytes); SHA-256 $(if ($sha -eq $e.Sha256) { 'matches' } else { 'differs' })). Not applied."
                continue
            }
            if (-not (Test-Path -LiteralPath $Folder)) { New-Item -ItemType Directory -Path $Folder -Force | Out-Null }
            if ($p.Action -eq 'backup') {
                $bdir = Join-Path $Folder 'backup'
                New-Item -ItemType Directory -Path $bdir -Force | Out-Null
                $bname = [IO.Path]::GetFileNameWithoutExtension($e.Name) + '.' + $Stamp + '.py'
                Copy-Item -LiteralPath $p.Dest -Destination (Join-Path $bdir $bname) -Force
                $lines += "$($e.Name): your own edits were kept in backup\$bname"
            }
            # In place through a temp name beside it, so a crash never leaves half a script.
            $side = $p.Dest + '.sl-new'
            Copy-Item -LiteralPath $tmp -Destination $side -Force
            Move-Item -LiteralPath $side -Destination $p.Dest -Force
            $rec[$e.Name] = [ordered]@{ sha256 = $e.Sha256; version = $e.Version; channel = $e.Channel }
            $recChanged = $true
            $word = 'updated'
            if ($p.Action -eq 'install') { $word = 'added' }
            $lines += "$($e.Name): $word ($($e.Channel) $($e.Version))"
        } catch {
            $failed += "$($e.Name): $($_.Exception.Message). Not applied."
        } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }
    if ($recChanged -and (Test-Path -LiteralPath $Folder)) { Write-LegionRecord -Folder $Folder -Record $rec }
    [pscustomobject]@{ Lines = $lines; Failed = $failed; Linked = $false }
}

# Downloads one entry (Install-LegionSet's $Fetch for a manifest). 30 seconds is generous for a 200 KB script.
function Save-LegionDownload {
    param($Entry, [string]$Path)
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $req = [Net.HttpWebRequest][Net.WebRequest]::Create($Entry.Url + '?t=' + [DateTime]::UtcNow.Ticks)
    $req.Timeout = 30000
    $req.UserAgent = 'ShatteredLegacy-Scripts'
    $resp = $req.GetResponse()
    try {
        $in = $resp.GetResponseStream()
        $out = [IO.File]::Create($Path)
        try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
    } finally { $resp.Close() }
}

# The published scripts an install holds, as entries another folder can be given (Sync-ClientDirData): every
# script its record lists whose copy is still the one the updater put there. A script the player edited in the
# install is theirs, not a published one, and is not passed on.
function Get-LegionInstalledEntries {
    param([string]$Folder)
    if (-not (Test-Path -LiteralPath $Folder)) { return @() }
    $rec = Read-LegionRecord $Folder
    foreach ($k in @($rec.Keys | Sort-Object)) {
        $f = Join-Path $Folder $k
        if (-not (Test-Path -LiteralPath $f -PathType Leaf)) { continue }
        $sha = [string]$rec[$k].sha256
        if ((Get-LegionSha $f) -ne $sha) { continue }
        [pscustomobject]@{ Name = $k; Sha256 = $sha; Bytes = (Get-Item -LiteralPath $f).Length; Version = [string]$rec[$k].version; Channel = [string]$rec[$k].channel; Url = $f }
    }
}

# What a new package ships (Build-PlayerPackage.ps1): the official set as published now, each file checked against
# the manifest, and the record to match, so the first start of a new install changes nothing. Nothing published yet
# (404): an empty result, said. Any other failure throws: a package must not quietly ship without the scripts.
function Copy-LegionPublishedSet {
    param([string]$Base = $script:LegionOfficialBase, [string]$Folder, [int]$TimeoutSec = 15)
    $m = Get-LegionManifest -Base $Base -TimeoutSec $TimeoutSec
    if ($m.Missing) { return [pscustomobject]@{ Count = 0; Version = ''; Text = $m.Error } }
    if (-not $m.Manifest) { throw "the published Legion scripts could not be read: $($m.Error)" }
    $entries = @(Merge-LegionManifests -Official $m.Manifest -OfficialBase $Base)
    if (-not $entries.Count) { return [pscustomobject]@{ Count = 0; Version = [string]$m.Manifest.version; Text = 'the official manifest lists no scripts' } }
    New-Item -ItemType Directory -Path $Folder -Force | Out-Null
    $res = Install-LegionSet -Folder $Folder -Entries $entries -Fetch { param($e, $t) Save-LegionDownload -Entry $e -Path $t }
    if (@($res.Failed).Count) { throw ('a published Legion script did not download as described: ' + (@($res.Failed) -join ' ')) }
    [pscustomobject]@{ Count = $entries.Count; Version = [string]$m.Manifest.version; Text = ((@($entries | ForEach-Object { $_.Name }) -join ', ') + ' (official ' + $m.Manifest.version + ')') }
}

# Why the LegionScripts a zip holds is wrong, or nothing (Build-PlayerPackage.ps1 gate 4). $Files maps each name under
# app/tazuo/LegionScripts/ to its SHA-256; $RecordText is the shipped .sl-scripts.json. Only the published scripts
# the record lists, each matching it, and the record itself.
function Test-LegionShipped {
    param([hashtable]$Files, [string]$RecordText)
    $fail = @()
    if (-not $Files.Count) { return $fail }
    $rec = @{}
    # [string] turns a missing record ($null) into '', so empty is what "none" looks like here.
    if (-not $RecordText) { $fail += 'app/tazuo/LegionScripts/ ships scripts but no .sl-scripts.json'; return $fail }
    try { foreach ($p in @(($RecordText | ConvertFrom-Json).installed.PSObject.Properties)) { $rec[$p.Name] = [string]$p.Value.sha256 } } catch { return @('app/tazuo/LegionScripts/.sl-scripts.json does not parse') }
    foreach ($n in @($Files.Keys)) {
        if ($n -eq $script:LegionRecordName) { continue }
        if ($n.Contains('/')) { $fail += "app/tazuo/LegionScripts/$n is in a subfolder; only published scripts ship"; continue }
        if ($script:LegionClientOwned -contains $n) { $fail += "app/tazuo/LegionScripts/$n is written by each client and must not ship"; continue }
        if (-not $rec.ContainsKey($n)) { $fail += "app/tazuo/LegionScripts/$n is not in .sl-scripts.json: only the published set ships"; continue }
        if ($rec[$n] -ne $Files[$n]) { $fail += "app/tazuo/LegionScripts/$n is not the file .sl-scripts.json records" }
    }
    foreach ($n in @($rec.Keys)) { if (-not $Files.ContainsKey($n)) { $fail += ".sl-scripts.json lists $n, which the zip does not hold" } }
    $fail
}

# --- dev PCs: every client's LegionScripts is a junction to the scripts repo -----------------------------------------

# Links $Folder (a client's LegionScripts) to the clone at $Repo. Already linked there: nothing changes. A link that
# is broken or points elsewhere is replaced (only the link; never what it pointed at). A real folder is moved aside
# to LegionScripts.before-repo-<yyyyMMdd-HHmm>, and each of its scripts is brought into the clone: one the clone
# lacks is copied in beside the others; one that differs is copied to from-installs\<Label>\<name>, never over the
# clone's copy. Nothing is committed. Returns State (linked, relinked, moved, created) and Brought (clone paths).
function Set-LegionLink {
    param([string]$Folder, [string]$Repo, [string]$Label, [string]$Stamp = (Get-Date -Format 'yyyyMMdd-HHmm'))
    $repoFull = (Resolve-Path -LiteralPath $Repo).ProviderPath.TrimEnd('\')
    $r = [pscustomobject]@{ Folder = $Folder; State = ''; Brought = @(); Aside = '' }
    if (Test-LegionLink $Folder) {
        $t = Get-LegionLinkTarget $Folder
        if ($t -and $t -ieq $repoFull -and (Test-Path -LiteralPath $Folder)) { $r.State = 'linked'; return $r }
        # Directory.Delete without recursion removes the junction itself and nothing it points at.
        [IO.Directory]::Delete($Folder, $false)
        $r.State = 'relinked'
    } elseif (Test-Path -LiteralPath $Folder) {
        $aside = $Folder + '.before-repo-' + $Stamp
        $n = 1
        while (Test-Path -LiteralPath $aside) { $aside = $Folder + '.before-repo-' + $Stamp + '-' + $n; $n++ }
        Move-Item -LiteralPath $Folder -Destination $aside
        $r.Aside = $aside
        $r.State = 'moved'
        foreach ($f in @((Get-LegionSourceFiles $aside).Files)) {
            $in = Join-Path $repoFull $f.Name
            if (-not (Test-Path -LiteralPath $in)) {
                Copy-Item -LiteralPath $f.FullName -Destination $in
                $r.Brought += $f.Name
            } elseif ((Get-LegionSha $in) -ne (Get-LegionSha $f.FullName)) {
                $safe = ($Label -replace '[^A-Za-z0-9._-]', '_')
                $dir = Join-Path $repoFull ('from-installs\' + $safe)
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $dir $f.Name) -Force
                $r.Brought += ('from-installs\' + $safe + '\' + $f.Name)
            }
        }
    } else {
        $r.State = 'created'
        $parent = Split-Path $Folder -Parent
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    }
    New-Item -ItemType Junction -Path $Folder -Target $repoFull | Out-Null
    $r
}
