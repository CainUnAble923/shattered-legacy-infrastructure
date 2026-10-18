# scripts-dev: the Legion scripts' dev channel on Haven (cc-P144)

Chase writes TazUO Legion scripts (`SL_Mining.py`, `SL_Lumberjack.py`, `SL_Retaliate.py`, ...) in their own private
repo, `CainUnAble923/shattered-legacy-scripts`. They reach installs on two channels, both on Haven:

| Channel | Served at | Who pulls it |
|---|---|---|
| official | `https://get.shatteredlegacyuo.com/scripts/` (sl-downloads, public) | every player package install, on every start; new packages ship it |
| dev | `http://haven.tail81098d.ts.net:8097/` (this folder: `sl-scripts-dev`, Tailscale only) | only PCs flagged dev (Chase's two), on top of official |

Each channel is a folder of `.py` files and a `scripts.json` describing them (name, sha256, bytes, modified,
publishedFrom, published; and the set's version, publishing PC and commit).

- **Publish to dev** (Shard Console, Player package tab) publishes the repo clone's scripts here. Refused unless the
  clone is committed and level with GitHub.
- **Promote to official** copies chosen dev scripts, on Haven, into the official folder (the exact dev bytes) and
  tags the commit `official-<version>` in the scripts repo.
- Backups of every replaced set: `~/backups/legion-scripts/dev/<version>/` and `~/backups/legion-scripts/<version>/`.

The scripts are `scripts\legion\` in this repo; the player side is `player-package\payload\app\LegionScripts.ps1` and
`Update-Scripts.ps1`. Notes: `shard-migration\notes\cc-P144-legion-scripts-updater.md`; the runbook entry is
`scripts-dev` in `shard-migration\notes\haven-runbook.md`.

## Setting it up on Haven (once)

`scripts\legion\Publish-LegionScripts.ps1 -Mode SetupDev` makes `~/sl-scripts-dev/html` and `~/sl-scripts-dev/stack`,
copies this compose file there and runs `docker compose -p sl-scripts-dev up -d`. Then the Mega dashboard deploy adds
Tailscale Serve for `:8097`, from `docker\haven\services.json`.
