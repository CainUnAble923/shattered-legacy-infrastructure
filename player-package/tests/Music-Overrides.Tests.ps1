#Requires -Version 5.1
# Facts for the shard music the player package ships (cc-P368): vendor\shattered-legacy-music\registry.csv and its
# tracks, the server's copy of the list (server\customizations\Music\ShardMusicTracks.cs), the music part of
# payload\app\Build-UoOverrides.ps1 (the Config.txt copy and the track overrides), and gate 4's music checks in
# Build-PlayerPackage.ps1 (-CheckZip).
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File player-package\tests\Music-Overrides.Tests.ps1
#
# Exit code is the number of failed facts, so 0 is green. Same shape as Cliloc-Overrides.Tests.ps1.
#
# No EA file is read. The Config.txt files here are made up in the client's shape ("<id> <file>,loop"), the mp3s are a
# few bytes of pattern (the launcher never reads inside one), and the zip for the gate fact is built by hand.

$ErrorActionPreference = 'Stop'
$here     = $PSScriptRoot
$pkg      = Split-Path $here -Parent
$repo     = Split-Path $pkg -Parent
$vendor   = Join-Path $pkg 'vendor\shattered-legacy-music'
$registry = Join-Path $vendor 'registry.csv'
$builder  = Join-Path $pkg 'payload\app\Build-UoOverrides.ps1'
$package  = Join-Path $pkg 'Build-PlayerPackage.ps1'
$tracksCs = Join-Path $repo 'server\customizations\Music\ShardMusicTracks.cs'
$work     = Join-Path $env:TEMP ("sl-music-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null

$results = New-Object 'System.Collections.Generic.List[object]'

function Fact {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Message = '' })
        Write-Host "  PASS  $Name" -ForegroundColor Green
    } catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Message = $_.Exception.Message })
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Because = '')
    if ($Expected -ne $Actual) { throw "expected [$Expected] but got [$Actual]. $Because" }
}

function Assert-True {
    param($Condition, [string]$Because)
    if (-not $Condition) { throw "not true: $Because" }
}

function Read-Registry { @(Get-Content -LiteralPath $registry | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv) }

# A made-up mp3: a few hundred bytes of pattern, distinct per seed. Nothing reads inside it.
function New-FakeMp3([string]$path, [int]$seed) {
    $d = New-Object byte[] 600
    for ($i = 0; $i -lt $d.Length; $i++) { $d[$i] = ($i * 7 + $seed) % 251 }
    New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
    [IO.File]::WriteAllBytes($path, $d)
}

# A player's Ultima Online folder with a Music\Digital\Config.txt of the given lines (the stock shape: CRLF, ".mp3").
$stockLines = @('0 oldult01.mp3,loop', '1 create1.mp3', '9 britain1.mp3,loop', '103 Townlife.mp3,loop', '104 DungeonOutpost.mp3,loop')
function New-UoDir([string]$name, [string[]]$lines = $stockLines, [string]$ending = "`r`n", [switch]$NoFinalBreak) {
    $dir = Join-Path $work $name
    New-Item -ItemType Directory -Path (Join-Path $dir 'Music\Digital') -Force | Out-Null
    $text = ($lines | ForEach-Object { "$_$ending" }) -join ''
    if ($NoFinalBreak) { $text = $text.TrimEnd("`r", "`n") }
    [IO.File]::WriteAllText((Join-Path $dir 'Music\Digital\Config.txt'), $text, (New-Object Text.ASCIIEncoding))
    return $dir
}

# The package's app\music\: shard-music.txt of the given lines and an mp3 per name.
function New-MusicDir([string]$name, [string[]]$lines, [string[]]$mp3s) {
    $dir = Join-Path $work $name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    if ($null -ne $lines) {
        $text = "# made up for the tests`r`n" + (($lines | ForEach-Object { "$_`r`n" }) -join '')
        [IO.File]::WriteAllText((Join-Path $dir 'shard-music.txt'), $text, (New-Object Text.ASCIIEncoding))
    }
    $seed = 1
    foreach ($m in @($mp3s)) { New-FakeMp3 (Join-Path $dir $m) ($seed++) }
    return $dir
}

# Runs the launcher's builder with only the music part able to do anything (no art, text or map shipped), and returns
# the override list's lines, what it said (yellow lines), and the list path.
function Invoke-Music([string]$uo, [string]$out, [string]$music) {
    $none = Join-Path $work 'no-such-file'
    $said = New-Object 'System.Collections.Generic.List[string]'
    $res = $null
    foreach ($x in @(& $builder -UoDir $uo -OutDir $out -Music $music -Records $none -Clilocs $none -MapOps $none -AnimMul $none -AnimIdx $none 6>&1)) {
        if ($x -is [Management.Automation.InformationRecord]) { $said.Add("$x") } else { $res = $x }
    }
    $lines = @()
    if ($res -and (Test-Path -LiteralPath $res)) { $lines = @([IO.File]::ReadAllLines($res) | Where-Object { $_ -and -not $_.StartsWith('#') }) }
    return [pscustomobject]@{ Result = $res; Lines = $lines; Said = ($said -join ' | ') }
}

# --- the registry and the server's copy ------------------------------------------------------------------------------

Fact 'TheRegistryFollowsItsRulesAndEveryRowHasItsTrackWithItsHash' {
    $header = @(Get-Content -LiteralPath $registry | Where-Object { $_ -and -not $_.StartsWith('#') })[0]
    Assert-Equal 'id,file,loop,fallback,what,source,license,sha256' $header 'the columns'
    $rows = Read-Registry
    $ids = @{}; $files = @{}
    foreach ($r in $rows) {
        $id = [int]$r.id
        Assert-True ($id -ge 105 -and $id -le 149) "id $id is 105 to 149 (stock is 0 to 104; TazUO stops at 150)"
        Assert-True (-not $ids.ContainsKey($id)) "id $id once"; $ids[$id] = 1
        Assert-True ($r.file -cmatch '^sl-[a-z0-9-]+$') "file '$($r.file)' is sl-<lower case, digits, dashes>, never an EA name"
        Assert-True (-not $files.ContainsKey($r.file)) "file $($r.file) once"; $files[$r.file] = 1
        Assert-True ($r.loop -in @('yes', 'no')) "loop '$($r.loop)' is yes or no"
        Assert-True ($r.fallback -cmatch '^[A-Za-z0-9_]+$') "row $id names a stock fallback track (cc-P456)"
        Assert-True ($r.what -and $r.source -and $r.license) "row $id names what, source and license"
        Assert-True ($r.sha256 -match '^[0-9A-Fa-f]{64}$') "row $id has a SHA-256"
        $mp3 = Join-Path $vendor "tracks\$($r.file).mp3"
        Assert-True (Test-Path -LiteralPath $mp3) "tracks\$($r.file).mp3 is there (git-ignored; Add-Track.ps1 copies it in)"
        Assert-Equal $r.sha256.ToUpperInvariant() (Get-FileHash -LiteralPath $mp3 -Algorithm SHA256).Hash "tracks\$($r.file).mp3 is the registered file"
    }
    # The tracks folder is git-ignored (a track is over the 5 MB rule), and nothing under it is tracked.
    $ignored = & git -C $repo check-ignore -q 'player-package/vendor/shattered-legacy-music/tracks/x.mp3'; $code = $LASTEXITCODE
    Assert-Equal 0 $code 'player-package/vendor/shattered-legacy-music/tracks/ is git-ignored'
    $tracked = @(& git -C $repo ls-files 'player-package/vendor/shattered-legacy-music/tracks')
    Assert-Equal 0 $tracked.Count "no track is committed: $($tracked -join ', ')"
}

Fact 'TheServersTrackListIsTheRegistryRowForRow' {
    $cs = [IO.File]::ReadAllText($tracksCs)
    Assert-True ($cs -match 'public const int FirstId = 105;') 'FirstId 105'
    Assert-True ($cs -match 'public const int LastId = 149;') 'LastId 149 (TazUO MAX_MUSIC_DATA_INDEX_COUNT 150, less one)'
    Assert-True ($cs -match 'public const int LastStockId = 104;') 'LastStockId 104 (the stock Config.txt ends at DungeonOutpost)'
    $pattern = 'new\((\d+),\s*"([^"]+)",\s*(true|false),'
    # The parser proves itself on a line of the documented shape before it is trusted on the file.
    $probe = [regex]::Matches('new(105, "sl-vol2-dungeon", true, "Dungeon", "Zigholtul"),', $pattern)
    Assert-Equal 1 $probe.Count 'the parser reads a track line'
    Assert-Equal '105|sl-vol2-dungeon|true' ($probe[0].Groups[1].Value + '|' + $probe[0].Groups[2].Value + '|' + $probe[0].Groups[3].Value) 'and its three fields'
    $body = $cs.Substring($cs.IndexOf('public static readonly ShardMusicTrack[] All'))
    $body = $body.Substring(0, $body.IndexOf('];') + 2)
    $server = @([regex]::Matches($body, $pattern) | ForEach-Object { "$($_.Groups[1].Value)|$($_.Groups[2].Value)|$($_.Groups[3].Value)" } | Sort-Object)
    $rows = @(Read-Registry | ForEach-Object { "$([int]$_.id)|$($_.file)|$(if ($_.loop -eq 'yes') { 'true' } else { 'false' })" } | Sort-Object)
    Assert-Equal ($rows -join ';') ($server -join ';') 'ShardMusicTracks.All is registry.csv (id, file, loop)'
}

# --- the launcher -----------------------------------------------------------------------------------------------------

Fact 'TheLauncherAddsOurLinesToACopyOfConfigTxtAndOverridesTheCopyAndEachTrack' {
    $uo = New-UoDir 'add'
    $music = New-MusicDir 'add-music' @('105 sl-test-one.mp3,loop', '106 sl-test-two.mp3') @('sl-test-one.mp3', 'sl-test-two.mp3')
    $out = Join-Path $work 'add-out'
    $r = Invoke-Music $uo $out $music
    Assert-Equal (Join-Path $out 'files-override.txt') $r.Result
    Assert-Equal '' $r.Said 'a clean build says nothing'
    $copy = Join-Path $out 'Music\Digital\Config.txt'
    Assert-Equal 3 $r.Lines.Count ($r.Lines -join ' | ')
    Assert-True ($r.Lines -ccontains "music/digital/config.txt=$copy") ($r.Lines -join ' | ')
    Assert-True ($r.Lines -ccontains "music/digital/sl-test-one.mp3=$music\sl-test-one.mp3") ($r.Lines -join ' | ')
    Assert-True ($r.Lines -ccontains "music/digital/sl-test-two.mp3=$music\sl-test-two.mp3") ($r.Lines -join ' | ')
    # The copy: the player's lines first and unchanged, then ours.
    $got = [IO.File]::ReadAllLines($copy)
    Assert-Equal (($stockLines + @('105 sl-test-one.mp3,loop', '106 sl-test-two.mp3')) -join ';') ($got -join ';') 'the copy'
    Assert-True ([IO.File]::ReadAllText($copy).StartsWith([IO.File]::ReadAllText((Join-Path $uo 'Music\Digital\Config.txt')))) 'the player text is a prefix of the copy, byte for byte'
    # The player's own file is untouched, and the keys TazUO asks for are lower case.
    Assert-Equal (($stockLines | ForEach-Object { "$_`r`n" }) -join '') ([IO.File]::ReadAllText((Join-Path $uo 'Music\Digital\Config.txt'))) 'the source'
    foreach ($l in $r.Lines) { Assert-True ($l.Split('=')[0] -cmatch '^music/digital/[a-z0-9.-]+$') "lower-case key: $l" }

    # A player file with no final line break still gets ours on their own lines.
    $uo2 = New-UoDir 'add-nobreak' $stockLines "`n" -NoFinalBreak
    $r2 = Invoke-Music $uo2 (Join-Path $work 'add-nobreak-out') $music
    Assert-Equal 3 $r2.Lines.Count $r2.Said
    $got2 = [IO.File]::ReadAllLines((Join-Path $work 'add-nobreak-out\Music\Digital\Config.txt'))
    Assert-Equal '104 DungeonOutpost.mp3,loop' $got2[4] 'the last stock line kept whole'
    Assert-Equal '105 sl-test-one.mp3,loop' $got2[5] 'ours on its own line'
}

Fact 'AnIdThePlayersFileAlreadyUsesOrATrackWithNoFileIsSkippedAndSaid' {
    # 105 taken by the player's file: skipped and said; 106 still added.
    $uo = New-UoDir 'taken' ($stockLines + @('105 TheirOwn.mp3,loop'))
    $music = New-MusicDir 'taken-music' @('105 sl-test-one.mp3,loop', '106 sl-test-two.mp3') @('sl-test-one.mp3', 'sl-test-two.mp3')
    $r = Invoke-Music $uo (Join-Path $work 'taken-out') $music
    Assert-True ($r.Said -match 'skipping track 105: your Ultima Online files now use that number') $r.Said
    Assert-Equal 2 $r.Lines.Count ($r.Lines -join ' | ')
    Assert-True ($r.Lines -notmatch 'sl-test-one') ($r.Lines -join ' | ')
    $got = [IO.File]::ReadAllLines((Join-Path $work 'taken-out\Music\Digital\Config.txt'))
    Assert-True ($got -ccontains '105 TheirOwn.mp3,loop' -and $got -ccontains '106 sl-test-two.mp3' -and -not ($got -ccontains '105 sl-test-one.mp3,loop')) ($got -join ';')

    # The mp3 missing from app\music: skipped and said.
    $music2 = New-MusicDir 'missing-music' @('105 sl-test-one.mp3,loop', '106 sl-test-two.mp3') @('sl-test-two.mp3')
    $r2 = Invoke-Music (New-UoDir 'missing') (Join-Path $work 'missing-out') $music2
    Assert-True ($r2.Said -match 'skipping track 105: sl-test-one\.mp3 is not in app\\music') $r2.Said
    Assert-Equal 2 $r2.Lines.Count ($r2.Lines -join ' | ')

    # Both skipped: no music lines at all, and no copy made.
    $r3 = Invoke-Music $uo (Join-Path $work 'both-out') (New-MusicDir 'both-music' @('105 sl-test-one.mp3,loop') @())
    Assert-Equal 0 $r3.Lines.Count ($r3.Lines -join ' | ')
    Assert-True (-not (Test-Path (Join-Path $work 'both-out\Music\Digital\Config.txt'))) 'no copy'

    # A line not in Config.txt's shape is skipped and said; a missing Config.txt is one yellow line and no music.
    $r4 = Invoke-Music (New-UoDir 'odd') (Join-Path $work 'odd-out') (New-MusicDir 'odd-music' @('105 sl-test-one.mp3,loop', 'what is this') @('sl-test-one.mp3'))
    Assert-True ($r4.Said -match 'skipping a line it cannot read: what is this') $r4.Said
    Assert-Equal 2 $r4.Lines.Count ($r4.Lines -join ' | ')
    $noUo = Join-Path $work 'no-config'; New-Item -ItemType Directory -Path $noUo -Force | Out-Null
    $r5 = Invoke-Music $noUo (Join-Path $work 'no-config-out') $music
    Assert-True ($r5.Said -match 'Shattered Legacy music could not be prepared \(Music\\Digital\\Config\.txt is not in .*\)\. Starting without it\.') $r5.Said
    Assert-Equal $null $r5.Result 'nothing to override'
}

# cc-P456: a track whose mp3 is missing keeps its ID, playing the stock track its fallback column names, and the copy
# follows the mp3 when it comes back (the stamp holds the lines that were written, not only our list's hash).
Fact 'AMissingTrackPlaysItsStockFallbackAndARestoredOneComesBack' {
    $uo = New-UoDir 'fallback'
    New-FakeMp3 (Join-Path $uo 'Music\Digital\britain1.mp3') 40
    $list = @('105 sl-test-one.mp3,loop britain1', '106 sl-test-two.mp3 britain1', '107 sl-test-three.mp3 nosuchstock')
    $music = New-MusicDir 'fallback-music' $list @('sl-test-two.mp3')
    $out = Join-Path $work 'fallback-out'
    $copy = Join-Path $out 'Music\Digital\Config.txt'

    $r = Invoke-Music $uo $out $music
    Assert-True ($r.Said -match 'track 105: sl-test-one\.mp3 is not in app\\music; playing the stock britain1 there instead') $r.Said
    Assert-True ($r.Said -match 'skipping track 107: sl-test-three\.mp3 is not in app\\music') $r.Said
    Assert-Equal 2 $r.Lines.Count ($r.Lines -join ' | ')
    Assert-True ($r.Lines -ccontains "music/digital/sl-test-two.mp3=$music\sl-test-two.mp3") ($r.Lines -join ' | ')
    $got = @([IO.File]::ReadAllLines($copy) | Select-Object -Skip $stockLines.Count)
    Assert-Equal '105 britain1,loop;106 sl-test-two.mp3' ($got -join ';') 'the missing track plays its fallback; no fallback column reaches Config.txt'

    # The mp3 comes back, nothing else changes: the copy plays it again.
    New-FakeMp3 (Join-Path $music 'sl-test-one.mp3') 41
    $r2 = Invoke-Music $uo $out $music
    Assert-True ($r2.Said -notmatch 'track 105') $r2.Said
    Assert-Equal 3 $r2.Lines.Count ($r2.Lines -join ' | ')
    Assert-True ($r2.Lines -ccontains "music/digital/sl-test-one.mp3=$music\sl-test-one.mp3") ($r2.Lines -join ' | ')
    $got2 = @([IO.File]::ReadAllLines($copy) | Select-Object -Skip $stockLines.Count)
    Assert-Equal '105 sl-test-one.mp3,loop;106 sl-test-two.mp3' ($got2 -join ';') 'the restored track is back in the copy'

    # And gone again: back to the fallback.
    Remove-Item -LiteralPath (Join-Path $music 'sl-test-one.mp3')
    [void](Invoke-Music $uo $out $music)
    $got3 = @([IO.File]::ReadAllLines($copy) | Select-Object -Skip $stockLines.Count)
    Assert-Equal '105 britain1,loop;106 sl-test-two.mp3' ($got3 -join ';') 'missing again'
}

# cc-P456: the same with a list of the older shape (no fallback column): the restored track is not stuck out of the copy
# by a stamp that still matches.
Fact 'ARestoredTrackIsBackInTheCopyWithNoFallbackColumn' {
    $uo = New-UoDir 'restored'
    $music = New-MusicDir 'restored-music' @('105 sl-test-one.mp3,loop', '106 sl-test-two.mp3') @('sl-test-two.mp3')
    $out = Join-Path $work 'restored-out'
    $r = Invoke-Music $uo $out $music
    Assert-True ($r.Said -match 'skipping track 105: sl-test-one\.mp3 is not in app\\music') $r.Said
    New-FakeMp3 (Join-Path $music 'sl-test-one.mp3') 42
    $r2 = Invoke-Music $uo $out $music
    Assert-Equal 3 $r2.Lines.Count ($r2.Lines -join ' | ')
    $got = @([IO.File]::ReadAllLines((Join-Path $out 'Music\Digital\Config.txt')) | Select-Object -Skip $stockLines.Count)
    Assert-Equal '105 sl-test-one.mp3,loop;106 sl-test-two.mp3' ($got -join ';') 'the copy names the restored track'
}

Fact 'NothingChangedSkipsTheRebuildAndAChangeOfTheirFileOrOurListRebuilds' {
    $uo = New-UoDir 'stamp'
    $music = New-MusicDir 'stamp-music' @('105 sl-test-one.mp3,loop') @('sl-test-one.mp3')
    $out = Join-Path $work 'stamp-out'
    [void](Invoke-Music $uo $out $music)
    $copy = Join-Path $out 'Music\Digital\Config.txt'
    $t0 = (Get-Item $copy).LastWriteTimeUtc
    Start-Sleep -Milliseconds 50
    $r = Invoke-Music $uo $out $music
    Assert-Equal 2 $r.Lines.Count ($r.Lines -join ' | ')
    Assert-Equal $t0 (Get-Item $copy).LastWriteTimeUtc 'not rewritten when nothing changed'

    (Get-Item (Join-Path $uo 'Music\Digital\Config.txt')).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(1)
    [void](Invoke-Music $uo $out $music)
    $t1 = (Get-Item $copy).LastWriteTimeUtc
    Assert-True ($t1 -gt $t0) 'rebuilt after the player file changed'

    Start-Sleep -Milliseconds 50
    [IO.File]::AppendAllText((Join-Path $music 'shard-music.txt'), "106 sl-test-two.mp3`r`n", (New-Object Text.ASCIIEncoding))
    New-FakeMp3 (Join-Path $music 'sl-test-two.mp3') 9
    $r2 = Invoke-Music $uo $out $music
    Assert-True ((Get-Item $copy).LastWriteTimeUtc -gt $t1) 'rebuilt after our list changed'
    Assert-Equal 3 $r2.Lines.Count ($r2.Lines -join ' | ')
}

Fact 'NoMusicShippedGivesNoLinesAndNoWords' {
    $uo = New-UoDir 'none'
    $r = Invoke-Music $uo (Join-Path $work 'none-out') (Join-Path $work 'no-such-music-folder')
    Assert-Equal $null $r.Result 'no app\music at all'
    Assert-Equal '' $r.Said
    $r2 = Invoke-Music $uo (Join-Path $work 'none2-out') (New-MusicDir 'none-music' @() @())
    Assert-Equal $null $r2.Result 'a shard-music.txt of comments alone (the package with no track yet)'
    Assert-Equal '' $r2.Said
}

# --- gate 4 ------------------------------------------------------------------------------------------------------------

Fact 'Gate4HoldsAppMusicToExactlyTheListAndTheRegistry' {
    # A zip of the music folder alone, three ways wrong: the list names a track that did not ship, a file not of ours, and
    # one of our shape that registry.csv does not have. Gate 4 fails it for many other reasons too (no client, no art);
    # the music lines are what this fact reads.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zip = Join-Path $work 'gate.zip'
    $fs = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
    $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $w = New-Object IO.StreamWriter(($za.CreateEntry('ShatteredLegacy/app/music/shard-music.txt')).Open())
        $w.Write("# test`r`n105 sl-fake-one.mp3,loop`r`n"); $w.Dispose()
        foreach ($n in 'stray.mp3', 'sl-not-registered.mp3') {
            $s = ($za.CreateEntry("ShatteredLegacy/app/music/$n")).Open(); $s.Write((New-Object byte[] 16), 0, 16); $s.Dispose()
        }
    } finally { $za.Dispose(); $fs.Dispose() }
    $ea = Join-Path $work 'ea'; New-Item -ItemType Directory -Path $ea -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $ea 'tiledata.mul'), (New-Object byte[] 8))
    $ErrorActionPreference = 'Continue'
    $text = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $package -CheckZip $zip -UoData $ea 2>&1 | Out-String
    $ErrorActionPreference = 'Stop'
    Assert-True ($text -match 'GATE 4') $text
    Assert-True ($text -match 'shard-music\.txt names sl-fake-one\.mp3, which did not ship') $text
    Assert-True ($text -match 'app/music/stray\.mp3 is not one of our tracks') $text
    Assert-True ($text -match 'app/music/sl-not-registered\.mp3 is not in registry\.csv') $text
    Assert-True ($text -match 'app/music/stray\.mp3 shipped but app/music/shard-music\.txt does not name it') $text

    # The same zip without the music folder: "no app/music/shard-music.txt", and no music line otherwise.
    $zip2 = Join-Path $work 'gate2.zip'
    $fs = [IO.File]::Open($zip2, [IO.FileMode]::CreateNew)
    $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
    try { $w = New-Object IO.StreamWriter(($za.CreateEntry('ShatteredLegacy/README.txt')).Open()); $w.Write('x'); $w.Dispose() } finally { $za.Dispose(); $fs.Dispose() }
    $ErrorActionPreference = 'Continue'
    $text2 = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $package -CheckZip $zip2 -UoData $ea 2>&1 | Out-String
    $ErrorActionPreference = 'Stop'
    Assert-True ($text2 -match 'no app/music/shard-music\.txt') $text2
}

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed }).Count
Write-Host ""
Write-Host ("{0} facts, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed), $failed) -ForegroundColor $(if ($failed) { 'Red' } else { 'Green' })
exit $failed
