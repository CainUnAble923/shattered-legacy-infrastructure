# Proxy (Nginx Proxy Manager)

> **This runs on Haven, not on this machine.**
> Haven is the Debian box at `192.168.1.61` (`ssh chase@192.168.1.61`). The container
> (`sl-proxy`) and its live copy of this folder, `~/shattered-legacy/docker/proxy/`, are there.
> This folder in the repo is the **source**: edit here, then deploy with a recreate run on Haven (the scripts in `D:\UO\haven-migration\` show how).
> **Never run `docker compose` for this on the Windows box.** A second copy of the web stack there
> is how the site went unreachable for months (the stray Windows `sl-proxy`, stopped 2026-09-29).

Routes every public name to its container: the apex and `www.`, `wiki.`, `get.`, and
`vault.chasekfleming.com`. The apex also has a custom location `/api/` to
`http://192.168.1.58:8092/` (the status feed on the Windows box).

The proxy hosts and certificates live in NPM's data volume on Haven, not in this folder.
The admin UI (port 81) listens on Haven only: open it with `D:\UO\Open-NPM-Admin.ps1`
(SSH tunnel to `localhost:8181`). This compose file declares the external networks that
connect NPM to each site; recreating from any other copy takes every site dark.
