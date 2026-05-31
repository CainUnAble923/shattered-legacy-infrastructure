# Shattered Legacy Wiki

## Overview

The Shattered Legacy wiki is a lightweight DokuWiki instance for player-facing Ultima Online documentation, shard notes, and in-world references.

Operational source of truth remains in Git. The wiki is for content that benefits from faster edits by admins and players.

## Current State

| Item | Value |
| --- | --- |
| URL | `http://wiki.clusterf.lab` |
| Container ID | `110` |
| Node | `enderman` |
| IP | `10.7.4.121` |
| OS | Debian 12 |
| App | DokuWiki |
| Web server | Nginx |
| PHP runtime | PHP-FPM |
| Health check | `http://10.7.4.121/healthz` |
| Auth | Local DokuWiki admin; FreeIPA LDAP planned |
| Upload limit | 32 MB |

The Debian DokuWiki package can pull in Apache. The deployment script disables `apache2` and serves the wiki through Nginx.

## Architecture

```text
Browser -> Nginx :80 -> DokuWiki/PHP-FPM
```

The wiki is intended for LAN and VPN access only.

## Deployment

Run on `enderman` after copying Boblin's public key to `/tmp/clusterf_boblin.pub`:

```bash
sudo ./scripts/deploy/wiki/deploy-dokuwiki-enderman-lxc.sh
```

Run from the Windows workstation:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\scripts\deploy\wiki\deploy-dokuwiki-enderman-lxc.sh `
  boblin@enderman.clusterf.lab:/tmp/

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@enderman.clusterf.lab `
  "sudo bash /tmp/deploy-dokuwiki-enderman-lxc.sh"
```

The script is safe to re-run. It preserves existing wiki pages and non-admin local users.

## Seed Content

Starter wiki content is tracked in Git:

```text
services/wiki/content/pages
services/wiki/content/media
```

Deploy seed content on `enderman` from a repository clone:

```bash
sudo SYNC_MODE=seed ./scripts/deploy/wiki/sync-dokuwiki-content.sh
```

Use `SYNC_MODE=seed` for normal operation because it preserves live wiki edits. Use `SYNC_MODE=overwrite` only when intentionally replacing matching wiki pages/media from Git.

If running from the Windows workstation, copy the content bundle and run the sync through Boblin:

```powershell
scp -i $env:USERPROFILE\.ssh\clusterf_boblin `
  .\scripts\deploy\wiki\sync-dokuwiki-content.sh `
  boblin@enderman.clusterf.lab:/tmp/

scp -i $env:USERPROFILE\.ssh\clusterf_boblin -r `
  .\services\wiki\content `
  boblin@enderman.clusterf.lab:/tmp/wiki-content

ssh -i $env:USERPROFILE\.ssh\clusterf_boblin boblin@enderman.clusterf.lab `
  "sudo CONTENT_ROOT=/tmp/wiki-content SYNC_MODE=seed bash /tmp/sync-dokuwiki-content.sh"
```

The current seed includes:

- a main Shattered Legacy landing page
- UO hub page
- world, systems, professions, guides, lore, and media namespaces
- sidebar navigation
- Shattered Legacy logo media for page content, DokuWiki header, and favicon
- starter SVG images for route, progression, and media guide pages

## Key Paths

| Purpose | Path |
| --- | --- |
| DokuWiki app | `/usr/share/dokuwiki` |
| DokuWiki config | `/etc/dokuwiki` |
| Wiki pages/media | `/var/lib/dokuwiki/data` |
| Nginx site | `/etc/nginx/sites-available/wiki.clusterf.lab` |
| Bootstrap admin secret | `/root/clusterf-wiki-admin.txt` |

Retrieve the bootstrap admin password on `enderman`:

```bash
sudo pct exec 110 -- cat /root/clusterf-wiki-admin.txt
```

## DNS

Current state: DNS record pending. The wiki is reachable by IP at `http://10.7.4.121` until `wiki.clusterf.lab` exists in UniFi DNS or FreeIPA DNS.

Preferred FreeIPA record when an IPA admin ticket is available:

```bash
ipa dnsrecord-add clusterf.lab wiki --a-rec=10.7.4.121
```

## Health Checks

From `enderman`:

```bash
sudo pct status 110
sudo pct exec 110 -- systemctl is-active nginx
sudo pct exec 110 -- systemctl list-units 'php*-fpm.service'
curl http://10.7.4.121/healthz
```

From a LAN or VPN client:

```powershell
curl.exe http://10.7.4.121/healthz
curl.exe http://wiki.clusterf.lab/healthz
```

Check image upload limits inside the container:

```bash
php -i | grep -E 'upload_max_filesize|post_max_size|max_file_uploads'
```

## Backups

Back up both config and data:

```bash
sudo pct exec 110 -- tar -C / -czf /root/wiki-backup.tgz etc/dokuwiki var/lib/dokuwiki/data
sudo pct pull 110 /root/wiki-backup.tgz /tank/clusterf-artifacts/wiki/wiki-backup-$(date -u +%Y%m%dT%H%M%SZ).tgz
```

## Future Improvements

- Add FreeIPA-issued TLS certificate and serve HTTPS.
- Integrate DokuWiki LDAP auth with FreeIPA groups.
- Add scheduled backups to Enderman artifact storage.
- Replace starter SVGs with real in-game screenshots and annotated maps.
- Create an admin-only operations namespace.

## Upstream Links

- DokuWiki: <https://www.dokuwiki.org/dokuwiki>
- DokuWiki ACLs: <https://www.dokuwiki.org/acl>
