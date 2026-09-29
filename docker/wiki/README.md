# Shattered Legacy Wiki

> **This runs on Haven, not on this machine.**
> Haven is the Debian box at `192.168.1.61` (`ssh chase@192.168.1.61`). The container
> (`sl-wiki`) and its live copy of this folder, `~/shattered-legacy/docker/wiki/`, are there.
> This folder in the repo is the **source**: edit here, then deploy with the scripts in `D:\UO\haven-migration\` (skin: `D:\UO\Install-WikiSkin.ps1`).
> **Never run `docker compose` for this on the Windows box.** A second copy of the web stack there
> is how the site went unreachable for months (the stray Windows `sl-proxy`, stopped 2026-09-29).

Player-facing DokuWiki for the Shattered Legacy shard: guides, maps, systems and lore.
Operational documentation stays in Git; the wiki is for what players need.

## Where it runs

| Item | Value |
| --- | --- |
| URL | https://wiki.shatteredlegacyuo.com |
| Host | Haven, `192.168.1.61` (Debian), `ssh chase@192.168.1.61` |
| Container | `sl-wiki`, image `ghcr.io/dokuwiki/dokuwiki:latest` |
| Compose | `~/shattered-legacy/docker/wiki/docker-compose.yml` on Haven (copy of this folder) |
| Data | Docker volume `sl-wiki-storage`, mounted at `/storage` |
| Network | `sl-wiki-net`; Nginx Proxy Manager (`sl-proxy`) reaches it as `sl-wiki:8080` |
| TLS | Terminated by `sl-proxy` on Haven |

Everything DokuWiki keeps lives in `/storage`: pages and media (`/storage/data`), configuration
(`/storage/conf`), and installed plugins and templates (`/storage/lib`). The image installs DokuWiki
itself to `/var/www/html` and refreshes the bundled plugins and template on every update.

## Updating DokuWiki

Pull and recreate. Data stays in `sl-wiki-storage`.

```bash
cd ~/shattered-legacy/docker/wiki
docker compose pull && docker compose up -d
```

Never use `docker compose down -v`. Plain `down` is safe now that the volume is named, but there is
no reason to use it.

## Content

`content/pages` and `content/media` are the seed copy of the wiki, kept in Git. They are **not**
mounted into the container; the live pages are in the volume and can be edited in the wiki.
To push seed pages to Haven without overwriting live edits, copy them into
`/storage/data/pages` with `tar -xk` (keep existing), as `D:\UO\haven-migration\haven-wiki-fix.sh` does.

## Skin

`skin/` holds the Shattered Legacy look (colors, type, logo, footer) from the Shattered Legacy
design system. It changes only config and media, so updates do not undo it.
Install or reinstall with `D:\UO\Install-WikiSkin.ps1`. See `skin/README.md`.

## Backups

The whole wiki is one volume:

```bash
docker exec sl-wiki tar -C /storage -czf - . > ~/wiki-storage-$(date +%Y%m%d).tgz
```

Restore by extracting into `/storage` of a stopped container, or into a fresh `sl-wiki-storage`.
