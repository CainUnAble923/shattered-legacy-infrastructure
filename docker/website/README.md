# Website

> **This runs on Haven, not on this machine.**
> Haven is the Debian box at `192.168.1.61` (`ssh chase@192.168.1.61`). The container
> (`sl-website`) and its live copy of this folder, `~/shattered-legacy/docker/website/`, are there.
> This folder in the repo is the **source**: edit here, then deploy with `D:\UO\haven-migration\Deploy-Website.ps1` (copies `html\*.html`, `mark.svg` and the favicons).
> **Never run `docker compose` for this on the Windows box.** A second copy of the web stack there
> is how the site went unreachable for months (the stray Windows `sl-proxy`, stopped 2026-09-29).

The public site at https://shatteredlegacyuo.com (and `www.`): the landing page and the Progress page.
The live status on the pages comes from `docker/uo-status`, which runs on the Windows box and
is reached through NPM's `/api/` route. The download zip in `html/download/` is **not** deployed
from here; `D:\UO\haven-migration\Publish-PlayerPackage.ps1` puts it on Haven.

`preview-*.html` and `index.html.prelive.bak` in this folder are scratch files, not deployed.
