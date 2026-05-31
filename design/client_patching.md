# Client Patching

## Purpose

Some Shattered Legacy content requires changes to client-side `.mul` files — the binary data files that determine what players see in the world. These structural changes cannot be made through server-side scripting alone.

Client patches are used when:

- A building's static layout needs to change permanently (e.g. converting an existing New Haven building into a new institution)
- New structural layouts are needed that cannot be reproduced with server-side item placement
- Exterior signage, banners, or architectural changes require permanent statics edits

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
