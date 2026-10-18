#Requires -Version 5.1
<#
  cc-P133: the functions behind docker/haven/services.json, dot-sourced by Build-HavenDashboard.ps1, by
  docker/dashboard/haven/Deploy-MegaDashboard.ps1 and by the console facts (scripts/tests/Shard-Console.Tests.ps1).

    Read-HavenInventory [-Path]                    services.json as objects, with Problems (empty when it is sound)
    ConvertTo-HomepageServices -Inventory [-Discovery]
                                                   the text of Homepage's services.yaml: the five sections in order,
                                                   one tile per inventory tile, live up or down on every tile, and a
                                                   tile for any running container the discovery found that the
                                                   inventory does not list
    ConvertTo-HomepageSettings -Inventory          the text of settings.yaml (title and the five sections' layout)
    ConvertFrom-HavenDiscovery -Text               what Discover printed: containers (name, state, status, image,
                                                   ports, compose folder), listening addresses, Tailscale facts
    Test-HavenInventoryCoverage -Inventory -Discovery
                                                   Missing: running containers with no inventory entry for that host;
                                                   Absent: inventory containers on that host the discovery did not see
    Get-HavenTileMode -Service                     cc-P163: live, by-hand (stopped by design) or none (no tile)
    ConvertFrom-DockerPorts -Text                  cc-P163: docker ps's PORTS column, one object per port
    Compare-HavenDiscoveredPorts -Inventory -Discovery [-All]
                                                   cc-P163: what each running container publishes and runs, against
                                                   the inventory: the lines the deploy's plan prints

  Up or down, per tile (Chase's decision 2, 2026-10-15):
    a Haven container          server: haven + container (Homepage's dot, through the read-only socket proxy)
    an HTTP service            siteMonitor (Homepage's dot)
    a game PC service          a customapi widget on Haven's /api/gamepc, field up.<snapshot key> ("Up", "Down: ...",
                               "Game PC offline since ..."), from the snapshot the game PC pushes every minute
  cc-P163: a planned, retired or not yet installed service gets no tile (the inventory, the runbook and Discover's "in the
  inventory, not on Haven" still name it), and an on-demand one (stopped by design) says "Stopped, started by hand" with
  no monitor instead of a red dot.
  Pure functions: no network, no Docker, no file writes. ASCII only.
#>

function Read-HavenInventory {
    param([string]$Path = (Join-Path $PSScriptRoot 'services.json'))
    $problems = New-Object 'System.Collections.Generic.List[string]'
    $inv = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($inv.schema -ne 1) { $problems.Add('schema is not 1') }
    $sections = @($inv.sections)
    $keys = @{}
    $req = 'key', 'name', 'host', 'owner', 'kind', 'state', 'container', 'image', 'compose', 'ports', 'volumes', 'env', 'secrets', 'health', 'dependsOn', 'neededBy', 'backup', 'verified', 'tiles'
    foreach ($s in @($inv.services)) {
        foreach ($f in $req) { if (-not ($s.PSObject.Properties.Name -contains $f)) { $problems.Add(([string]$s.key) + ': no ' + $f) } }
        if ($keys.ContainsKey([string]$s.key)) { $problems.Add('duplicate key ' + $s.key) }
        $keys[[string]$s.key] = $s
        if (-not ($inv.hosts.PSObject.Properties.Name -contains [string]$s.host)) { $problems.Add($s.key + ': unknown host ' + $s.host) }
        if (@('Shattered Legacy', 'Sunflower', 'home lab') -notcontains [string]$s.owner) { $problems.Add($s.key + ': owner must be Shattered Legacy, Sunflower or home lab') }
        foreach ($t in @($s.tiles)) { if ($sections -notcontains [string]$t.section) { $problems.Add($s.key + ': tile ' + $t.name + ' in unknown section ' + $t.section) } }
    }
    # dependsOn and neededBy are kept both ways so a reader of one entry sees both; they must mirror each other.
    foreach ($s in @($inv.services)) {
        foreach ($d in @($s.dependsOn)) {
            if (-not $keys.ContainsKey([string]$d)) { $problems.Add($s.key + ': dependsOn unknown ' + $d); continue }
            if (@($keys[[string]$d].neededBy) -notcontains $s.key) { $problems.Add($d + ': neededBy lacks ' + $s.key + ' (which dependsOn it)') }
        }
        foreach ($d in @($s.neededBy)) {
            if (-not $keys.ContainsKey([string]$d)) { $problems.Add($s.key + ': neededBy unknown ' + $d); continue }
            if (@($keys[[string]$d].dependsOn) -notcontains $s.key) { $problems.Add($d + ': dependsOn lacks ' + $s.key + ' (which says it is needed by it)') }
        }
    }
    $inv | Add-Member -NotePropertyName Problems -NotePropertyValue $problems.ToArray() -Force
    $inv
}

# --- a small YAML writer: every string double-quoted, so nothing in a description can change the structure ----------

function ConvertTo-YamlScalar($v) {
    if ($null -eq $v) { return 'null' }
    if ($v -is [bool]) { return $(if ($v) { 'true' } else { 'false' }) }
    if ($v -is [int] -or $v -is [long] -or $v -is [double] -or $v -is [decimal]) { return [string]$v }
    '"' + (([string]$v) -replace '\\', '\\' -replace '"', '\"') + '"'
}

function Test-YamlComplex($v) { ($v -is [System.Collections.IDictionary]) -or ($v -is [System.Management.Automation.PSCustomObject]) -or (($v -is [array]) -or ($v -is [System.Collections.IList])) }

function Get-YamlPairs($v) {
    # One list of [key, value] pairs, returned whole: a function that emitted one pair would have it unrolled.
    $pairs = New-Object 'System.Collections.Generic.List[object]'
    if ($v -is [System.Collections.IDictionary]) { foreach ($e in $v.GetEnumerator()) { $pairs.Add(@($e.Key, $e.Value)) } }
    else { foreach ($p in $v.PSObject.Properties) { $pairs.Add(@($p.Name, $p.Value)) } }
    , $pairs
}

function ConvertTo-YamlLines {
    param($Value, [int]$Indent)
    $pad = ' ' * $Indent
    $out = New-Object 'System.Collections.Generic.List[string]'
    if (($Value -is [array]) -or ($Value -is [System.Collections.IList])) {
        foreach ($item in $Value) {
            if (Test-YamlComplex $item) {
                $inner = @(ConvertTo-YamlLines $item ($Indent + 2))
                $out.Add($pad + '- ' + $inner[0].Substring($Indent + 2))
                for ($i = 1; $i -lt $inner.Count; $i++) { $out.Add($inner[$i]) }
            } else { $out.Add($pad + '- ' + (ConvertTo-YamlScalar $item)) }
        }
    } else {
        foreach ($pair in (Get-YamlPairs $Value)) {
            $k = [string]$pair[0]; $v = $pair[1]
            $name = $(if ($k -match '^[A-Za-z][A-Za-z0-9_]*$') { $k } else { ConvertTo-YamlScalar $k })
            if (Test-YamlComplex $v) {
                $out.Add($pad + $name + ':')
                foreach ($l in (ConvertTo-YamlLines $v ($Indent + 2))) { $out.Add($l) }
            } else { $out.Add($pad + $name + ': ' + (ConvertTo-YamlScalar $v)) }
        }
    }
    $out
}

# --- the dashboard ----------------------------------------------------------------------------------------------------

function Get-HavenSnapshotUrl($Inventory) { 'http://' + $Inventory.hosts.haven.lan + ':8094/api/gamepc' }

function New-SnapshotWidget($Inventory, [string]$Key) {
    [ordered]@{
        type            = 'customapi'
        url             = (Get-HavenSnapshotUrl $Inventory)
        refreshInterval = 60000
        headers         = [ordered]@{ Authorization = 'Bearer {{HOMEPAGE_VAR_OPS_TOKEN}}' }
        mappings        = @(
            [ordered]@{ field = ('up.' + $Key); label = 'State'; remap = @([ordered]@{ value = $null; to = 'Not reported yet' }) },
            [ordered]@{ field = 'receivedAt'; label = 'As of'; format = 'relativeDate' }
        )
    }
}

function Get-HavenTileMode($Service) {
    # cc-P163 (Chase, 2026-10-18: "fix anything that seems broken"): what a service's tiles show besides their link.
    #   none     planned, retired, or built and not installed yet: no tile, rather than a tile that reads down
    #   by-hand  kind on-demand (the Sunflower dev store): stopped is its usual state, so no monitor and the words
    #   live     everything else: a dot or a State line (cc-P133)
    if ([string]$Service.state -match '^(planned|retired|built, not installed)') { return 'none' }
    if ([string]$Service.kind -eq 'on-demand') { return 'by-hand' }
    'live'
}

function ConvertTo-HomepageTile($Inventory, $Service, $Tile) {
    $hostName = [string]$Inventory.hosts.([string]$Service.host).name
    $t = [ordered]@{}
    if ($Tile.icon) { $t.icon = [string]$Tile.icon }
    if ($Tile.href) { $t.href = [string]$Tile.href }
    $t.description = [string]$Tile.description + ' [' + ($hostName -replace ' \(.*$', '') + ']'
    if ((Get-HavenTileMode $Service) -eq 'by-hand') { $t.description = 'Stopped, started by hand. ' + $t.description; return $t }
    if ($Service.host -eq 'haven' -and $Service.kind -eq 'container' -and $Service.container) { $t.server = 'haven'; $t.container = [string]$Service.container }
    if ($Tile.siteMonitor) { $t.siteMonitor = [string]$Tile.siteMonitor }
    if ($Tile.widget) { $t.widget = $Tile.widget }
    elseif ($Tile.snapshot) { $t.widget = New-SnapshotWidget $Inventory ([string]$Tile.snapshot) }
    $t
}

function ConvertTo-HomepageServices {
    param($Inventory, $Discovery)
    $groups = [ordered]@{}
    foreach ($sec in @($Inventory.sections)) { $groups[[string]$sec] = New-Object 'System.Collections.Generic.List[object]' }
    foreach ($s in @($Inventory.services)) {
        if ((Get-HavenTileMode $s) -eq 'none') { continue }
        foreach ($tile in @($s.tiles)) { $groups[[string]$tile.section].Add([ordered]@{ ([string]$tile.name) = (ConvertTo-HomepageTile $Inventory $s $tile) }) }
    }
    if ($Discovery) {
        foreach ($m in @((Test-HavenInventoryCoverage -Inventory $Inventory -Discovery $Discovery -HostKey 'haven').Missing)) {
            $groups['Admin'].Add([ordered]@{ ($m + ' (not in the inventory)') = [ordered]@{
                icon = 'mdi-help-box'; description = 'Running on Haven but missing from docker/haven/services.json: add it there [Haven]'
                server = 'haven'; container = $m } })
        }
    }
    $doc = New-Object 'System.Collections.Generic.List[object]'
    foreach ($sec in @($Inventory.sections)) { $k = [string]$sec; $doc.Add([ordered]@{ $k = $groups[$k].ToArray() }) }
    $head = @(
        '# GENERATED by docker/haven/Build-HavenDashboard.ps1 from docker/haven/services.json (cc-P133). Do not edit by hand:',
        '# change services.json and run the build (Mega dashboard: deploy runs it too). The console facts P133_* fail when this',
        '# file and the inventory disagree. Secrets are {{HOMEPAGE_VAR_...}} names, filled from dashboard.env on Haven. ASCII only.'
    )
    # The list's own array: @($doc) as an argument failed with 'Argument types do not match' in PowerShell 5.1.
    ($head + @(ConvertTo-YamlLines $doc.ToArray() 0)) -join "`n"
}

function ConvertTo-HomepageSettings {
    param($Inventory)
    $layout = [ordered]@{}
    foreach ($sec in @($Inventory.sections)) { $layout[[string]$sec] = [ordered]@{ style = 'row'; columns = 3 } }
    $doc = [ordered]@{
        title = 'Haven - the home intranet (Tailscale only)'; theme = 'dark'; color = 'slate'; headerStyle = 'boxed'; statusStyle = 'dot'
        layout = $layout
    }
    ((@('# GENERATED by docker/haven/Build-HavenDashboard.ps1 from docker/haven/services.json (cc-P133). The five sections, in order. ASCII only.') +
        @(ConvertTo-YamlLines $doc 0)) -join "`n")
}

# --- what Discover saw --------------------------------------------------------------------------------------------------

function ConvertFrom-HavenDiscovery {
    <#
      Reads three shapes, so every discovery on record can be checked:
        the Mega dashboard deploy (cc-P133):  '-- containers' then '  name|state|status|image|ports|compose dir'
        cc-P110's Discover:                   '-- running containers' then '  name  status  ports'
        cc-P163's mounts:                     '-- mounts' then '  /name|source>destination;...'
        a plain docker ps table:              a header line 'NAMES  STATUS  IMAGE  PORTS' (haven-discovery-output.txt)
      and '-- listening ports' (one address per line) and '-- tailscale' ('key: value' lines).
    #>
    param([string]$Text)
    $containers = New-Object 'System.Collections.Generic.List[object]'
    $listen = New-Object 'System.Collections.Generic.List[string]'
    $mounts = @{}
    $ts = [ordered]@{}
    $section = ''
    $cols = $null
    foreach ($raw in ($Text -split "\r?\n")) {
        $line = $raw.TrimEnd()
        if ($line -match '^-- (.+)$') { $section = $Matches[1].Trim(); $cols = $null; continue }
        if ($line -match '^=====') { $section = ''; $cols = $null; continue }
        if ($line -match '^\s*NAMES\s+.*STATUS') {
            $cols = @{}
            foreach ($c in 'NAMES', 'STATUS', 'IMAGE', 'PORTS') { $cols[$c] = $line.IndexOf($c) }
            $section = 'table'; continue
        }
        if (-not $line.Trim()) { if ($section -eq 'table') { $section = ''; $cols = $null }; continue }
        if ($section -eq 'table' -and $cols) {
            $order = @($cols.GetEnumerator() | Where-Object { $_.Value -ge 0 } | Sort-Object Value)
            $f = @{}
            for ($i = 0; $i -lt $order.Count; $i++) {
                $a = $order[$i].Value
                $b = $(if ($i + 1 -lt $order.Count) { $order[$i + 1].Value } else { $line.Length })
                $f[$order[$i].Key] = $(if ($a -lt $line.Length) { $line.Substring($a, [Math]::Min($b, $line.Length) - $a).Trim() } else { '' })
            }
            $st = [string]$f['STATUS']
            $containers.Add([pscustomobject]@{ Name = [string]$f['NAMES']; State = $(if ($st -like 'Up*') { 'running' } else { 'exited' }); Status = $st; Image = [string]$f['IMAGE']; Ports = [string]$f['PORTS']; Compose = ''; Running = ($st -like 'Up*') })
        } elseif ($section -eq 'containers') {
            $p = $line.Trim() -split '\|'
            if ($p.Count -ge 3) {
                $containers.Add([pscustomobject]@{ Name = $p[0]; State = $p[1]; Status = $p[2]; Image = $(if ($p.Count -gt 3) { $p[3] } else { '' }); Ports = $(if ($p.Count -gt 4) { $p[4] } else { '' }); Compose = $(if ($p.Count -gt 5) { $p[5] } else { '' }); Running = ($p[1] -eq 'running') })
            }
        } elseif ($section -eq 'running containers') {
            $p = $line.Trim() -split '\s{2,}'
            if ($p.Count -ge 2) { $containers.Add([pscustomobject]@{ Name = $p[0]; State = 'running'; Status = $p[1]; Image = ''; Ports = $(if ($p.Count -gt 2) { $p[2] } else { '' }); Compose = ''; Running = $true }) }
        } elseif ($section -eq 'mounts') {
            # cc-P163: '  /name|source>destination;source>destination;' (a volume's name, or a bind's host path)
            $p = $line.Trim() -split '\|', 2
            if ($p.Count -eq 2) { $mounts[$p[0].TrimStart('/')] = @($p[1] -split ';' | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }) }
        } elseif ($section -eq 'listening ports') {
            $listen.Add($line.Trim())
        } elseif ($section -eq 'tailscale') {
            if ($line.Trim() -match '^([^:]+):\s*(.*)$') { $ts[$Matches[1].Trim()] = $Matches[2].Trim() }
        }
    }
    [pscustomobject]@{ Containers = $containers.ToArray(); Listening = $listen.ToArray(); Tailscale = $ts; Mounts = $mounts }
}

function Test-HavenInventoryCoverage {
    param($Inventory, $Discovery, [string]$HostKey = 'haven')
    $known = @($Inventory.services | Where-Object { $_.host -eq $HostKey -and $_.container } | ForEach-Object { [string]$_.container })
    $running = @($Discovery.Containers | Where-Object { $_.Running } | ForEach-Object { $_.Name })
    $seen = @($Discovery.Containers | ForEach-Object { $_.Name })
    [pscustomobject]@{
        Missing = @($running | Where-Object { $known -notcontains $_ } | Sort-Object -Unique)
        Absent  = @($known | Where-Object { $seen -notcontains $_ } | Sort-Object -Unique)
    }
}

# --- what each container publishes (cc-P163) ---------------------------------------------------------------------------

function ConvertFrom-DockerPorts {
    <#
      docker ps's PORTS column, as Discover prints it:
        '100.98.32.16:3001->3000/tcp, [::]:7912->8000/tcp, 0.0.0.0:80-81->80-81/tcp, 2375/tcp'
      One object per port: Bind (the host address; '' when only exposed), HostPort (0 when only exposed), Port (in the
      container), Proto. A range is one object per port.
    #>
    param([string]$Text)
    $out = New-Object 'System.Collections.Generic.List[object]'
    foreach ($item in ($Text -split ',')) {
        $i = $item.Trim()
        if ($i -match '^(\[[^\]]*\]|[^:\[]+):(\d+)(?:-(\d+))?->(\d+)(?:-\d+)?/(\w+)$') {
            $bind = $Matches[1]; $h = [int]$Matches[2]; $c = [int]$Matches[4]; $proto = $Matches[5]
            $n = $(if ($Matches[3]) { [int]$Matches[3] - $h } else { 0 })
            for ($k = 0; $k -le $n; $k++) { $out.Add([pscustomobject]@{ Bind = $bind; HostPort = $h + $k; Port = $c + $k; Proto = $proto }) }
        } elseif ($i -match '^(\d+)(?:-(\d+))?/(\w+)$') {
            $c = [int]$Matches[1]; $proto = $Matches[3]
            $n = $(if ($Matches[2]) { [int]$Matches[2] - $c } else { 0 })
            for ($k = 0; $k -le $n; $k++) { $out.Add([pscustomobject]@{ Bind = ''; HostPort = 0; Port = $c + $k; Proto = $proto }) }
        }
    }
    $out.ToArray()
}

function Compare-HavenDiscoveredPorts {
    <#
      For each running container Discover saw: the IPv4 addresses it publishes on (bind:port; the [::] twins left out)
      and its image, against the inventory entry for that container (its ports' lan, loopback and tailnetBind, and its
      image text, which must contain the image's name). Returns the containers that differ, or every running one with
      -All: Name, Key ('' when the inventory has no entry), Image, Published, NotListed (published, not in the
      inventory), NotPublished (in the inventory, not published), ImageOk. The Mega dashboard deploy prints these in its
      plan, so the next deploy says what to fill in.
    #>
    param($Inventory, $Discovery, [string]$HostKey = 'haven', [switch]$All)
    foreach ($c in @($Discovery.Containers | Where-Object { $_.Running })) {
        $ports = ConvertFrom-DockerPorts ([string]$c.Ports)
        $pub = @($ports | Where-Object { $_.HostPort -and $_.Bind -notlike '[[]*' } | ForEach-Object { $_.Bind + ':' + $_.HostPort } | Sort-Object -Unique)
        $svc = @($Inventory.services | Where-Object { $_.host -eq $HostKey -and $_.container -eq $c.Name })[0]
        $listed = @()
        $imageOk = $true
        if ($svc) {
            $listed = @(foreach ($p in @($svc.ports)) { foreach ($f in 'lan', 'loopback', 'tailnetBind') { foreach ($m in [regex]::Matches([string]$p.$f, '\d{1,3}(?:\.\d{1,3}){3}:\d+')) { $m.Value } } })
            $listed = @($listed | Sort-Object -Unique)
            $name = ([string]$c.Image -replace ':[^:/]+$', '')
            $imageOk = (-not $name) -or ([string]$svc.image).Contains($name)
        }
        $notListed = @($pub | Where-Object { $listed -notcontains $_ })
        $notPublished = @($listed | Where-Object { $pub -notcontains $_ })
        if ($All -or -not $svc -or $notListed.Count -or $notPublished.Count -or -not $imageOk) {
            [pscustomobject]@{ Name = $c.Name; Key = $(if ($svc) { [string]$svc.key } else { '' }); Image = [string]$c.Image; Published = $pub; NotListed = $notListed; NotPublished = $notPublished; ImageOk = $imageOk }
        }
    }
}
