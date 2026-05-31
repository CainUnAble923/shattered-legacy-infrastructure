# Dashboard (Homepage)

## Overview

Homepage is deployed in a Proxmox LXC container on Enderman. It provides a central UI for ClusterF service status with per-service status dots and auto-refresh.

## Current State

| Item | Value |
| --- | --- |
| URL | `https://dashboard.clusterf.lab` |
| Container ID | `100` |
| Node | `enderman` |
| IP | `10.7.4.103` |
| OS | Debian 12 |
| App | Homepage (Next.js standalone) |
| Status style | Green/yellow/red dots (`statusStyle: dot`) |
| Auto-refresh | 10 seconds |

## Stack

- Homepage (Next.js standalone server)
- Nginx reverse proxy
- FreeIPA-issued TLS certificate
- systemd service (`homepage.service`)

## Architecture

```text
Browser -> Nginx :443 (TLS, FreeIPA cert) -> Homepage :3000 (standalone server)
```

## Key Paths

| Purpose | Path |
| --- | --- |
| Application root | `/opt/homepage` |
| Standalone server | `/opt/homepage/.next/standalone/server.js` |
| Config (effective) | `/opt/homepage/.next/standalone/config/` |
| Config (source, not read at runtime) | `/opt/homepage/config/` |
| Static assets | `/opt/homepage/.next/standalone/.next/static/` |
| Public assets / locales | `/opt/homepage/.next/standalone/public/` |
| systemd unit | `/etc/systemd/system/homepage.service` |
| Nginx site | `/etc/nginx/sites-available/dashboard` |

> Important: Homepage standalone mode reads config from `.next/standalone/config/`, not from the source `config/` directory. Always edit config files under the `standalone/` path on the container.

## Repository Config

Config files are tracked in the repository:

```text
containers/dashboard/config/settings.yaml
containers/dashboard/config/services.yaml
```

Deploy config changes through Enderman:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\dashboard\config\settings.yaml `
  boblin@enderman.clusterf.lab:/tmp/homepage-settings.yaml

scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\containers\dashboard\config\services.yaml `
  boblin@enderman.clusterf.lab:/tmp/homepage-services.yaml

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@enderman.clusterf.lab `
  "sudo pct push 100 /tmp/homepage-settings.yaml /tmp/settings.yaml --perms 0644 && sudo pct push 100 /tmp/homepage-services.yaml /tmp/services.yaml --perms 0644 && sudo pct exec 100 -- bash -lc 'cp /tmp/settings.yaml /tmp/services.yaml /opt/homepage/.next/standalone/config/ && systemctl restart homepage'"
```

## settings.yaml

```yaml
title: ClusterF
theme: dark
statusStyle: "dot"
refresh: 10000
```

`statusStyle: "dot"` enables the green/yellow/red status indicators.
`refresh: 10000` auto-refreshes the page every 10 seconds.

## services.yaml

Current service entries:

- **Shattered Legacy / UO Shard** - `ping: 10.7.4.120` (ICMP) + `siteMonitor: http://10.7.4.120:12001` (HTTP health check for `uo.service` state)
- **Shattered Legacy / UO Wiki** - `href: http://10.7.4.121`, `ping: 10.7.4.121` (ICMP) + `siteMonitor: http://10.7.4.121/healthz` (HTTP health check)
- **Proxmox (Creeper)** - `ping: 10.7.4.100`
- **Proxmox (Enderman)** - `ping: 10.7.4.101`
- **FreeIPA (Warden)** - `ping: 10.7.4.102`

The `siteMonitor` on the UO entry uses the `uo-healthcheck` service (port 12001). This distinguishes "container alive" from "game service running"; the dot goes red if `uo.service` is inactive even if the container is reachable.

## Status Dot Behaviour

| Dot colour | Meaning |
| --- | --- |
| Green | Response < 200 ms (ping) or HTTP 2xx (siteMonitor) |
| Yellow | Response slow but present |
| Red | No response or HTTP non-2xx |

## Required Environment Variable

`HOMEPAGE_ALLOWED_HOSTS` in the systemd unit must include the FQDN:

```text
HOMEPAGE_ALLOWED_HOSTS=dashboard.clusterf.lab
```

If this is missing or wrong, the UI loads with broken styling and raw translation keys instead of text.

Verify:

```bash
systemctl cat homepage | grep ALLOWED_HOSTS
```

## Standalone Deployment Notes

Next.js standalone mode does not automatically include static and public assets. These must be copied into the standalone directory manually during deployment:

```bash
cp -r /opt/homepage/.next/static /opt/homepage/.next/standalone/.next/static
cp -r /opt/homepage/public       /opt/homepage/.next/standalone/public
```

If locales are missing, text can show raw keys like `common:bookmarks` or `common:search`. Verify that `/opt/homepage/.next/standalone/public/locales/en/` exists and is populated.

## Service Commands

```bash
systemctl status homepage
journalctl -u homepage -n 50
systemctl restart homepage
```

## Nginx Commands

```bash
nginx -t
systemctl status nginx
systemctl reload nginx
```

## Troubleshooting

### Broken UI (raw translation keys, no CSS)

1. Check `HOMEPAGE_ALLOWED_HOSTS`; it must match the hostname used in the browser.
2. Check that `.next/static` is present under `.next/standalone/`.
3. Check that `public/locales/en/` is present under `.next/standalone/public/`.

### UO Shard dot is red but container is up

The `siteMonitor` on port 12001 checks `uo.service` state, not just host reachability. If `uo.service` failed to start, the dot is correctly red. Check on the UO container:

```bash
systemctl status uo.service
journalctl -u uo.service -n 100
```

### Wiki dot is red

The `siteMonitor` checks `http://10.7.4.121/healthz`. Check the wiki container:

```bash
systemctl status nginx
systemctl status php8.2-fpm
curl http://127.0.0.1/healthz
```

### IP / DNS

| Item | Value |
| --- | --- |
| Dashboard container IP | `10.7.4.103` |
| Wiki container IP | `10.7.4.121` |
| Dashboard DNS | `dashboard.clusterf.lab -> 10.7.4.103` |
| Wiki DNS | pending: `wiki.clusterf.lab -> 10.7.4.121` |
| DNS set via | Dashboard has existing DNS; wiki DNS is pending, FreeIPA preferred later |
