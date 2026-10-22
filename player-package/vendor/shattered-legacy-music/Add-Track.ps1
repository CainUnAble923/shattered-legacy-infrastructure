#Requires -Version 5.1
<#
  cc-P368: copies a chosen music track into the package source tree and writes its registry row.

      .\Add-Track.ps1 -Source "D:\UO\third-party\sos-music-vol2\UO Classic Music (SOS Mix) Vol.2\Digital\Dungeon.mp3" `
                      -Id 105 -File sl-vol2-dungeon -Loop -What "Dungeon (SOS Vol.2): the Rootdeep Burrows" `
                      -Author Zigholtul [-License "..."] [-Checked] [-WhatIf]

  Only for a track Chase marked Use in the Music Room, and only after its terms were checked (SOURCE.txt here).
  Refuses: an ID outside 105-149 or already in registry.csv; a file stem not sl-<lower case, digits, dashes> or already
  used; a source that is not an .mp3, or that is inside a repo; a source whose ID3 album tag does not name Ultima
  (another game's music, SOURCE.txt's flag) unless -Checked says Chase cleared it. Then copies the file to
  tracks\<File>.mp3 (git-ignored), appends the row with the SHA-256 and the album tag, and prints the line to add to
  server\customizations\Music\ShardMusicTracks.cs. ASCII only.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][int]$Id,
    [Parameter(Mandatory = $true)][string]$File,
    [Parameter(Mandatory = $true)][string]$What,
    [Parameter(Mandatory = $true)][string]$Author,
    [switch]$Loop,
    [string]$License = 'none stated; see SOURCE.txt',
    [switch]$Checked
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$registry = Join-Path $here 'registry.csv'
$tracks = Join-Path $here 'tracks'

# The album the file's own tag names: ID3v2 TALB (v2.3 plain sizes, v2.4 syncsafe), else ID3v1's album field, else ''.
function Get-Mp3Album([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        $head = New-Object byte[] 10
        if ($fs.Read($head, 0, 10) -eq 10 -and $head[0] -eq 0x49 -and $head[1] -eq 0x44 -and $head[2] -eq 0x33) {
            $ver = $head[3]
            $size = ($head[6] -shl 21) -bor ($head[7] -shl 14) -bor ($head[8] -shl 7) -bor $head[9]
            $tag = New-Object byte[] $size
            $got = $fs.Read($tag, 0, $size)
            $i = 0
            if (($head[5] -band 0x40) -and $got -ge 4) {   # an extended header: skip it
                $eh = if ($ver -ge 4) { ($tag[0] -shl 21) -bor ($tag[1] -shl 14) -bor ($tag[2] -shl 7) -bor $tag[3] } else { ([int]$tag[0] -shl 24) -bor ([int]$tag[1] -shl 16) -bor ([int]$tag[2] -shl 8) -bor $tag[3] + 4 }
                $i = $eh
            }
            while ($i + 10 -le $got) {
                if ($tag[$i] -eq 0) { break }
                $fid = [Text.Encoding]::ASCII.GetString($tag, $i, 4)
                $fsz = if ($ver -ge 4) { ($tag[$i + 4] -shl 21) -bor ($tag[$i + 5] -shl 14) -bor ($tag[$i + 6] -shl 7) -bor $tag[$i + 7] } else { ([int]$tag[$i + 4] -shl 24) -bor ([int]$tag[$i + 5] -shl 16) -bor ([int]$tag[$i + 6] -shl 8) -bor $tag[$i + 7] }
                if ($fsz -le 0 -or $i + 10 + $fsz -gt $got) { break }
                if ($fid -eq 'TALB') {
                    $enc = $tag[$i + 10]
                    $text = $tag[($i + 11)..($i + 9 + $fsz)]
                    $s = switch ($enc) { 1 { [Text.Encoding]::Unicode.GetString($text) } 2 { [Text.Encoding]::BigEndianUnicode.GetString($text) } 3 { [Text.Encoding]::UTF8.GetString($text) } default { [Text.Encoding]::GetEncoding(1252).GetString($text) } }
                    return $s.Trim([char]0, [char]0xFEFF, ' ')
                }
                $i += 10 + $fsz
            }
        }
        if ($fs.Length -ge 128) {
            $fs.Seek(-128, [IO.SeekOrigin]::End) | Out-Null
            $v1 = New-Object byte[] 128
            if ($fs.Read($v1, 0, 128) -eq 128 -and [Text.Encoding]::ASCII.GetString($v1, 0, 3) -eq 'TAG') {
                return [Text.Encoding]::GetEncoding(1252).GetString($v1, 63, 30).Trim([char]0, ' ')
            }
        }
        return ''
    } finally { $fs.Dispose() }
}

if ($Id -lt 105 -or $Id -gt 149) { throw "ID $Id is outside our range (105 to 149; 150+ needs a client patch, registry.csv)" }
if ($File -cnotmatch '^sl-[a-z0-9-]+$') { throw "the file stem must be sl-<lower case letters, digits, dashes>: $File" }
if (-not (Test-Path -LiteralPath $Source)) { throw "no such file: $Source" }
if ([IO.Path]::GetExtension($Source) -ne '.mp3') { throw "not an .mp3: $Source" }
$full = (Resolve-Path -LiteralPath $Source).Path
if ($full -match '(?i)\\(ShatteredLegacy|shard-migration)\\') { throw "the source is inside a repo; copy from D:\UO\third-party\ (never commit a track): $full" }
$rows = @(Get-Content -LiteralPath $registry | Where-Object { $_ -and -not $_.StartsWith('#') } | ConvertFrom-Csv)
if ($rows | Where-Object { [int]$_.id -eq $Id }) { throw "ID $Id is already in registry.csv; never reuse or renumber an ID" }
if ($rows | Where-Object { $_.file -eq $File }) { throw "file stem $File is already in registry.csv" }

$album = Get-Mp3Album $full
if ($album -notmatch '(?i)ultima' -and -not $Checked) {
    throw "the file's album tag is '$album', which does not name Ultima: possibly another game's music (SOURCE.txt). Have Chase check it, then run again with -Checked."
}

$dest = Join-Path $tracks "$File.mp3"
if (Test-Path -LiteralPath $dest) { throw "$dest already exists" }
$sha = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash
$src = (Split-Path (Split-Path $full -Parent) -Leaf) + '\' + (Split-Path $full -Leaf)
$sourceText = "$Author, $src" + $(if ($album) { "; album tag '$album'" } else { '; no album tag' }) + $(if ($Checked -and $album -notmatch '(?i)ultima') { '; cleared by Chase' } else { '' })
function Q([string]$s) { '"' + $s.Replace('"', '""') + '"' }
$row = "$Id,$File,$(if ($Loop) { 'yes' } else { 'no' }),$(Q $What),$(Q $sourceText),$(Q $License),$sha"

if ($PSCmdlet.ShouldProcess($dest, "copy $full and append the registry row")) {
    New-Item -ItemType Directory -Path $tracks -Force | Out-Null
    Copy-Item -LiteralPath $full -Destination $dest
    if ((Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash -ne $sha) { Remove-Item -LiteralPath $dest; throw 'the copy differs from the source' }
    [IO.File]::AppendAllText($registry, $row + "`r`n", (New-Object Text.ASCIIEncoding))
}
Write-Host "registry row: $row"
Write-Host ("ShardMusicTracks.cs: new({0}, ""{1}"", {2}, ""{3}"", ""{4}""), " -f $Id, $File, $(if ($Loop) { 'true' } else { 'false' }), $What.Replace('"', '\"'), $Author)
Write-Host "Config.txt line the launcher adds: $Id $File.mp3$(if ($Loop) { ',loop' })"
