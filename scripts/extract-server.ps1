# extract-server.ps1
# Extracts the ModernUO server from the pre-migration backup into D:\ShatteredLegacy\server\
# Run once before first docker compose up in docker/uo/

param(
    [string]$BackupFile = "D:\ShatteredLegacy\backups\uo-modernuo-premigration-full-20260531T130851Z.tar.gz",
    [string]$DestDir   = "D:\ShatteredLegacy\server"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) {
    Write-Error "Backup not found: $BackupFile"
    exit 1
}

if (Test-Path $DestDir) {
    $existing = Get-ChildItem $DestDir | Measure-Object
    if ($existing.Count -gt 0) {
        $confirm = Read-Host "$DestDir already has $($existing.Count) items. Overwrite? (y/N)"
        if ($confirm -ne 'y') { Write-Host "Aborted."; exit 0 }
    }
} else {
    New-Item -ItemType Directory -Path $DestDir | Out-Null
    Write-Host "Created $DestDir"
}

Write-Host "Extracting $BackupFile -> $DestDir ..."
Write-Host "(This may take a few minutes for a 3.5 GB archive)"

# Use tar (built into Windows 10/11)
tar -xzf "$BackupFile" -C "$DestDir" --strip-components=1

if ($LASTEXITCODE -ne 0) {
    Write-Error "tar extraction failed (exit $LASTEXITCODE)"
    exit 1
}

Write-Host ""
Write-Host "Extraction complete. Contents of $DestDir :"
Get-ChildItem $DestDir | Format-Table Name, LastWriteTime -AutoSize

Write-Host ""
Write-Host "Verify ModernUO.dll is present:"
$dll = Join-Path $DestDir "ModernUO.dll"
if (Test-Path $dll) {
    Write-Host "  [OK] ModernUO.dll found"
} else {
    Write-Warning "  ModernUO.dll not found at expected location."
    Write-Host "  Check the archive structure with: tar -tzf '$BackupFile' | Select-String 'ModernUO.dll'"
}
