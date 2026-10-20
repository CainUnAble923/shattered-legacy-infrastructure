#Requires -Version 5.1
<#
  cc-P187: one web console page's lists, as one JSON object on stdout, from the Shard Console's own functions (the ones
  its window fills the same tab with). web-console\server.py starts it when a page is opened, at most every 20 s per
  page; it reads and changes nothing else.

      Get-WebConsoleList.ps1 -Page test|fix|question|snapshots|world|package|commit|websites|art|live [-StatusFrom <capture>]

    test       images: Deploy built image's list (Get-ImagePickerRows); liveRetired
    fix        commits: Rebuild a commit's list (Get-CommitPickerRows, from Get-FixFacts); lastGood
    question   shards: each shard's question text and the answers it takes (Get-QuestionPanelInfo)
    snapshots  shards: each shard's snapshots (Get-SnapshotRows), its save and snapshot folders
    world      groups: the World setup tab's commands (ConvertFrom-CommandSources, Get-WorldCommandGroups)
    package    zips (Get-PackageListRows) and the client Build package ships (Format-PackageClientText)
    commit     countdown: the Commit tab's line (Get-CommitCountdown)
    websites   sites (websites.json, with each one's deploy)
    art        requests: the Art tab's list (Get-ArtRequestRows), each with whether it has a picture
    live       liveRetired and the LIVE tab's line
  -StatusFrom: a capture file instead of docker (the tests). Never prints a secret: these lists hold none, and
  server.py redacts every string anyway. ASCII only.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][ValidateSet('test', 'fix', 'question', 'snapshots', 'world', 'package', 'commit', 'websites', 'art', 'live')][string]$Page,
    [string]$StatusFrom, [string]$RepoRoot)
$ErrorActionPreference = 'Stop'
# Kept under names of their own: dot-sourcing the console runs its param block here, which resets its own names.
$list = [pscustomobject]@{ Page = $Page; From = $StatusFrom; Root = $(if ($RepoRoot) { $RepoRoot } else { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }) }
. (Join-Path $PSScriptRoot 'Shard-Console.ps1') -LoadOnly
$config = Get-ConsoleConfig -RepoRoot $list.Root
$out = [ordered]@{ page = $list.Page; at = (Get-Date).ToString('s') }
$state = $null
if ($list.Page -in @('test', 'fix', 'question', 'snapshots')) {
    $state = Get-ConsoleState -Config $config -DryRun:([bool]$list.From) -StatusFrom $list.From
}
switch ($list.Page) {
    'test' {
        $out.images = @(Get-ImagePickerRows $state $config | ForEach-Object { [ordered]@{ value = $_.Tag; cells = @($_.Cells) } })
        $out.liveRetired = [bool](Test-LiveRetired $config)
    }
    'fix' {
        $fx = Get-FixFacts $config $state -NoDocker:([bool]$list.From)
        $out.commits = @(Get-CommitPickerRows $state $fx | ForEach-Object { [ordered]@{ value = $_.Tag; cells = @($_.Cells) } })
        $lg = Get-LastGoodImage $state.Images
        $out.lastGood = $(if ($lg) { [string]$lg.Tag } else { '' })
    }
    'question' {
        $sh = [ordered]@{}
        foreach ($k in @('test', 'live')) {
            $p = Get-QuestionPanelInfo $state $k $config
            $sh[$k] = [ordered]@{ lines = @($p.Lines); enabled = @($p.Enabled); asking = $p.Asking; retired = [bool]$config.Shards[$k].Retired }
        }
        $out.shards = $sh
    }
    'snapshots' {
        $sh = [ordered]@{}
        foreach ($k in @('test', 'live')) {
            $sh[$k] = [ordered]@{ rows = @(Get-SnapshotRows $state $k | ForEach-Object { [ordered]@{ value = $_.Name; cells = @($_.Taken, $_.Size, $_.Files, $_.Accounts, $_.Kind) } })
                save = [string]$config.Shards[$k].Saves; folder = (Join-Path $config.SnapshotRoot $k); retired = [bool]$config.Shards[$k].Retired }
        }
        $out.shards = $sh
    }
    'world' {
        $cmds = @()
        try { $cmds = @(ConvertFrom-CommandSources (Get-CommandSourceFiles $config.Customizations)) } catch { }
        $out.count = $cmds.Count
        $out.groups = @(Get-WorldCommandGroups $cmds | ForEach-Object {
            [ordered]@{ key = [string]$_.Key; title = [string]$_.Title; source = [string]$_.Source; copyAll = [bool]$_.CopyAll
                items = @(@($_.Items) | ForEach-Object { [ordered]@{ command = [string]$_.Command; text = [string]$_.Text; warn = [bool]$_.Warn } }) }
        })
    }
    'package' {
        $out.zips = @(Get-PackageListRows $config | ForEach-Object { [ordered]@{ value = $_.Name; cells = @($_.Version, $_.Size, $_.Built, $_.Notes) } })
        $out.client = [string](Format-PackageClientText (Get-PackageClientFacts -ClientDir $config.PackageClient -RepoRoot $config.RepoRoot))
    }
    'commit' {
        $f = Get-CommitFacts $config
        $out.countdown = [string](Get-CommitCountdown -Facts $f -Config $config -Now ([datetime]::UtcNow))
    }
    'websites' {
        $out.sites = @(@($config.Websites.Sites) | ForEach-Object { [ordered]@{ value = [string]$_.Key; cells = @([string]$_.Name, $(if ($_.Where -ceq 'pc') { 'this PC' } else { 'Haven' })); url = [string]$_.Url; where = [string]$_.Where; deploy = [string]$_.Deploy } })
    }
    'art' {
        $dir = Join-Path $config.ArtStore 'requests'
        $texts = @(if (Test-Path -LiteralPath $dir) { Get-ChildItem -LiteralPath $dir -Filter '*.json' -File | ForEach-Object { try { [IO.File]::ReadAllText($_.FullName) } catch { '' } } })
        $out.requests = @(Get-ArtRequestRows $texts | ForEach-Object { [ordered]@{ value = [string]$_.Id; cells = @([string]$_.Status, [string]$_.AskedBy, [string]$_.Variants, [string]$_.Title); thumb = [string]$_.Thumb } })
    }
    'live' {
        $out.liveRetired = [bool](Test-LiveRetired $config)
        $out.line = [string]$config.Live.Line
    }
}
[pscustomobject]$out | ConvertTo-Json -Depth 8 -Compress
