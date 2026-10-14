# Client Patching

## Purpose

Some Shattered Legacy content requires changes to client-side `.mul` files — the binary data files that determine what players see in the world. These structural changes cannot be made through server-side scripting alone.

Client patches are used when:

- A building's static layout needs to change permanently (e.g. converting an existing New Haven building into a new institution)
- New structural layouts are needed that cannot be reproduced with server-side item placement
- Exterior signage, banners, or architectural changes require permanent statics edits

Changes to the client **program** (our TazUO build) are a different kind of patch, recorded in `client\` (see
"Client code patches" at the end).

## What `.mul` Files Are

UO stores world data across several binary file pairs:

| File | Pair | Contents |
| --- | --- | --- |
| `statics0.mul` | `staidx0.mul` | Felucca static objects |
| `statics1.mul` | `staidx1.mul` | Trammel static objects |
| `map0.mul` | `mapdifl0.mul` | Felucca terrain |
| `map1.mul` | `mapdifl1.mul` | Trammel terrain |

Static files hold furniture, fixtures, decorative objects, and structural elements. Map files hold the terrain itself.

Most client patches will be statics-only edits.

## Tooling

Static and map edits are made in **CentrED#** (CentredSharp), a UO world editor.

The workflow is:

1. Connect CentrED# to a test/staging UO installation
2. Make edits at the target coordinates
3. Export the modified `.mul` files
4. Prepare the patch payload on the fileshare
5. Distribute via `Install-UOClientPatch.ps1`

## Why `.mul` Files Are Not In Git

`.mul` files are large binary files representing the entire world's static data. Committing them to Git would:

- Bloat the repository significantly
- Make diffs meaningless
- Slow down clones and pulls for all contributors

The patched `.mul` payloads live on the ClusterF artifact fileshare:

```text
\\enderman.clusterf.lab\clusterf-artifacts\uo\client-patches\<patchId>\<version>\
```

Linux path:

```text
/tank/clusterf-artifacts/uo/client-patches/<patchId>/<version>/
```

The Git repo contains only:

- The installer script (`Install-UOClientPatch.ps1`)
- Per-patch manifests (`manifest.json`)
- Example checksum files
- Patch documentation

## Patch Layout

Each patch version folder follows this structure:

```text
<patchId>/<version>/
├── manifest.json
├── checksums.txt
└── files/
    ├── statics1.mul
    └── staidx1.mul
```

The `manifest.json` describes the patch metadata and the file mapping between the payload and the client folder.

The `checksums.txt` contains SHA256 hashes for each payload file, used by the installer to verify integrity before applying the patch.

## Important: All Players Must Patch

UO static data is read client-side. Players running unpatched clients will see the original building layouts instead of the modified ones. All Shattered Legacy players must apply the same client patches for visual consistency.

Patch instructions should be distributed via:

- League Dispatch announcements
- Shard documentation / player onboarding
- Discord patch release notices

## Installer

The patch installer is:

```text
containers/uo/client-patcher/Install-UOClientPatch.ps1
```

It handles manifest reading, checksum verification, timestamped backup creation, file copy, and install log writing.

See: [`client-patcher/README.md`](../client-patcher/README.md)

## Available Patches

| Patch ID | Version | Facet | Coords | Description |
| --- | --- | --- | --- | --- |
| `newhaven-league-office` | `1.0.0` | Trammel | `3459, 2598` | League of Extraordinary Citizens field office conversion |

## Client code patches

Our TazUO build is upstream TazUO at a pinned commit plus our own source patches. The pin, the patches and the build
script live in [`client/`](../client/README.md); the TazUO source does not. Each patch is one rebasable change on the
pinned upstream commit, applied with `git apply` (no fuzz), so a patch that no longer applies fails the build.

| Patch | Upstream base | Since | Description |
| --- | --- | --- | --- |
| `0001-enhanced-packet-client-command.patch` | `3212623f` (v26.0909.63) | 2026.10.14.10 (cc-P103) | Enhanced packet 0xCE id 4, ClientCommand: the server can run a whitelisted client command, so tutorial buttons open the World Map, the Journal and the options window |
| `0002-paperdoll-bodies-option.patch` | `3212623f` plus 0001 | 2026.10.14.17 (cc-P111) | Options > Paperdoll > Shattered Legacy > **Paperdoll bodies**: SFW, and Topless or Nude only when a local add-on folder holds that set's six bodies (12, 13, 14, 15, 665, 666). Read from the add-on folder at run time, applied live; never copied into ExternalImages or a build. Global setting `sl_paperdoll_bodies` (and `sl_paperdoll_folder`). Client-side only |

The player package ships the stock TazUO until our build goes through the package pipeline (cc-P40's P-a). Until then
these patches change nothing for players.
