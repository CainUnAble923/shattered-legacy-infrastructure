# Downloads (get.)

> **This runs on Haven, not on this machine.**
> Haven is the Debian box at `192.168.1.61` (`ssh chase@192.168.1.61`). The container
> (`sl-downloads`) and its live copy of this folder, `~/shattered-legacy/docker/downloads/`, are there.
> This folder in the repo is the **source**: edit here, then deploy with `D:\UO\haven-migration\Publish-PlayerPackage.ps1`.
> **Never run `docker compose` for this on the Windows box.** A second copy of the web stack there
> is how the site went unreachable for months (the stray Windows `sl-proxy`, stopped 2026-09-29).

Serves https://get.shatteredlegacyuo.com: the player zip and `version.json`, which installed
launchers read to offer updates. Files are in `/home/chase/shattered-legacy/downloads` on Haven.
