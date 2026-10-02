# Shattered Legacy Infrastructure

Infrastructure-as-code for the **Shattered Legacy** Ultima Online shard. Everything here is the
**source**; it runs on two machines, and which one matters.

## Where things run

| Machine | Address | Runs |
|---|---|---|
| **Windows box** (CHASES-TOWER, Docker Desktop) | `192.168.1.58` | the shard `sl-modernuo` (2593), the test shard `sl-modernuo-test` (2594, when up), `sl-ddns`, and the status feed `sl-uo-status` + `sl-shard-probe` (8092). This repo lives here, at `D:\ShatteredLegacy`. |
| **Haven** (Lenovo ThinkCentre, Debian 13) | `192.168.1.61` | **all the web stuff**: `sl-proxy` (Nginx Proxy Manager), `sl-website`, `sl-wiki`, `sl-downloads`, `sl-dashboard`. Its copies of these folders are in `~/shattered-legacy/docker/` there. `ssh chase@192.168.1.61` |

The router forwards 80 and 443 to Haven and 2593 to the Windows box.

**Web folders (`docker/website`, `wiki`, `proxy`, `downloads`, `dashboard`) run on Haven.** Edit them
here, deploy them with the scripts in `D:\UO\haven-migration\`, and **never run `docker compose` for
them on the Windows box.** Each folder's README says how it deploys.

## Services

| Service | Folder | Machine | Access |
|---|---|---|---|
| ModernUO game server | `docker/uo` | Windows box | Public, `shatteredlegacyuo.com:2593` |
| Test shard | `docker/uo` (`docker-compose.test.yml`) | Windows box | `shatteredlegacyuo.com:2594` |
| Cloudflare DDNS | `docker/uo` (`sl-ddns`) | Windows box | Internal |
| Status feed and shard probe | `docker/uo-status` | Windows box | via NPM, `https://shatteredlegacyuo.com/api/` |
| Client test queue viewer | `docker/test-queue` | Windows box | LAN only, `http://192.168.1.58:8093/` |
| Website | `docker/website` | Haven | Public, `https://shatteredlegacyuo.com` |
| Wiki (DokuWiki) | `docker/wiki` | Haven | Public, `https://wiki.shatteredlegacyuo.com` |
| Downloads | `docker/downloads` | Haven | Public, `https://get.shatteredlegacyuo.com` |
| Nginx Proxy Manager | `docker/proxy` | Haven | Admin UI on Haven only (`D:\UO\Open-NPM-Admin.ps1`) |
| Homepage dashboard | `docker/dashboard` | Haven | LAN only |

`docker/atm10-site`, `mc-status` and `portainer` are home lab services, not shard content (gitignored).

## Directory Structure

```
ShatteredLegacy/
|-- docker/          # compose files, Dockerfiles, configs (see "Where things run")
|-- server/          # ModernUO config + custom scripts (saves excluded from git)
|-- player-package/  # builds the player zip (TazUO + launcher)
|-- scripts/         # helper scripts (e.g. Build-MigrationStatus.ps1)
|-- design/          # system design notes
|-- downloads/       # binary assets (excluded from git)
`-- backups/         # server backups (excluded from git)
```

Working agreements for agents are in `AGENTS.md`. Do not run `docker compose up` in `docker/`
as a whole: there is no single stack any more, and the live shard's project must never be taken
down (`docker/uo` holds both `sl-modernuo` and `sl-ddns`).

## Shard Details

- **Name:** Shattered Legacy
- **Engine:** ModernUO, pinned upstream commit `d4531cd94` (.NET 10; `7c9215d97` until cc-P38, 2026-10-02)
- **Client:** TazUO, shipped in the player package
- **Port:** 2593

## Migration Source

Migrated from ClusterF homelab: ModernUO LXC CT 101 on `creeper.clusterf.lab` (10.7.4.120).
