# sl.ps1 — Shattered Legacy service manager
# Usage:
#   .\sl.ps1 up [service]      # bring up one or all services
#   .\sl.ps1 down [service]    # bring down one or all services
#   .\sl.ps1 restart [service] # restart one or all services
#   .\sl.ps1 logs [service]    # tail logs for a service
#   .\sl.ps1 status            # show status of all containers
#   .\sl.ps1 pull              # pull latest images for all services
#
# Examples:
#   .\sl.ps1 up              # bring everything up in order
#   .\sl.ps1 up uo           # bring up only the UO service
#   .\sl.ps1 down wiki       # bring down only the wiki
#   .\sl.ps1 logs uo         # tail UO + ddns logs
#   .\sl.ps1 restart proxy   # restart proxy

param(
    [Parameter(Position=0, Mandatory=$true)]
    [ValidateSet("up","down","restart","logs","status","pull")]
    [string]$Command,

    [Parameter(Position=1)]
    [ValidateSet("proxy","uo","wiki","dashboard","downloads","website","")]
    [string]$Service = ""
)

$ErrorActionPreference = "Stop"
$Root = "D:\ShatteredLegacy\docker"

# Startup order matters — proxy must be first, others can follow freely
$OrderedServices = @("proxy","uo","wiki","dashboard","downloads","website")

function Compose {
    param([string]$svc, [string[]]$composeArgs)
    $path = Join-Path $Root $svc
    if (-not (Test-Path (Join-Path $path "docker-compose.yml"))) {
        Write-Warning "No docker-compose.yml found for '$svc' — skipping"
        return
    }
    Write-Host ""
    Write-Host "==> [$svc] docker compose $($composeArgs -join ' ')" -ForegroundColor Cyan
    Push-Location $path
    try {
        docker compose @composeArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "[$svc] exited with code $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }
}

function Check-EnvFile {
    param([string]$svc)
    $envExample = Join-Path $Root $svc ".env.example"
    $envFile    = Join-Path $Root $svc ".env"
    if ((Test-Path $envExample) -and (-not (Test-Path $envFile))) {
        Write-Warning "[$svc] .env not found — copy .env.example to .env and fill in secrets before running"
        return $false
    }
    return $true
}

switch ($Command) {

    "up" {
        $targets = if ($Service) { @($Service) } else { $OrderedServices }
        foreach ($svc in $targets) {
            if (-not (Check-EnvFile $svc)) { continue }
            Compose $svc @("up","-d","--build")
        }
        Write-Host ""
        Write-Host "Done. Run '.\sl.ps1 status' to verify." -ForegroundColor Green
    }

    "down" {
        # Bring down in reverse order
        if ($Service) {
            $targets = @($Service)
        } else {
            $reversed = [System.Linq.Enumerable]::Reverse($OrderedServices)
            $targets  = @($reversed)
        }
        foreach ($svc in $targets) {
            Compose $svc @("down")
        }
    }

    "restart" {
        $targets = if ($Service) { @($Service) } else { $OrderedServices }
        foreach ($svc in $targets) {
            Compose $svc @("restart")
        }
    }

    "logs" {
        if (-not $Service) {
            Write-Error "Specify a service: .\sl.ps1 logs <service>"
            exit 1
        }
        Compose $Service @("logs","--tail=100","-f")
    }

    "status" {
        Write-Host ""
        Write-Host "==> All Shattered Legacy containers" -ForegroundColor Cyan
        docker ps --filter "name=sl-" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    }

    "pull" {
        $targets = if ($Service) { @($Service) } else { $OrderedServices }
        foreach ($svc in $targets) {
            Compose $svc @("pull")
        }
    }
}
