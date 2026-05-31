# Shattered Legacy Infrastructure

Infrastructure-as-code for the **Shattered Legacy** Ultima Online shard — Docker Compose stack running on Fleming (Windows 11, D drive).

## Services

| Service | Description | Access |
|---|---|---|
| `uo` | ModernUO game server | Public — `shatteredlegacy.com:2593` |
| `nginx-proxy-manager` | Reverse proxy + SSL (Let's Encrypt) | LAN admin UI — port 81 |
| `downloads` | Player client download server | Public — `get.shatteredlegacy.com` |
| `dashboard` | Homepage dashboard | VPN only |
| `wiki` | DokuWiki | VPN only |
| `ddns-updater` | Cloudflare DDNS updater | Internal |

## Directory Structure

```
ShatteredLegacy/
├── docker/          # Docker Compose files, Dockerfiles, configs
├── server/          # ModernUO config + custom scripts (saves excluded from git)
├── website/         # Future public-facing website
├── downloads/       # Player client package (excluded from git, binary assets)
└── backups/         # Server backups (excluded from git)
```

## Phased Hosting Plan

**Phase 1 (current):** All services on Fleming machine.

**Phase 2:** Dashboard, Wiki, and Website move to Lenovo micro desktop. UO stays on Fleming until ClusterF server hardware is migrated.

## Prerequisites

- Docker Desktop for Windows (WSL2 backend)
- Cloudflare account with `shatteredlegacy.com` domain
- `.env` file in `docker/` (see `docker/.env.example`)

## Quick Start

```powershell
cd D:\ShatteredLegacy\docker
cp .env.example .env
# Edit .env with your Cloudflare API token, domain, etc.
docker compose up -d
```

## Shard Details

- **Name:** Shattered Legacy
- **Engine:** ModernUO 0.15.6.52 (.NET 10)
- **Client:** ClassicUO + Razor Enhanced
- **Port:** 2593

## Migration Source

Migrated from ClusterF homelab — ModernUO LXC CT 101 on `creeper.clusterf.lab` (10.7.4.120).
