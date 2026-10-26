# docker/uo

**The live shard was retired on 2026-10-14 (cc-P108) and returns as a fresh 1.0 world, built new then.**
Its service is kept, not runnable, in `retired/docker-compose.live.yml`; `scripts/live.json` is the switch.

| File | What |
|---|---|
| `build.sh` | the build command: patches, the shard's tests, then the image (`AGENTS.md`) |
| `docker-compose.test.yml`, `Start-TestShard.ps1`, `test-shard.sh` | the test shard, `sl-modernuo-test`, LAN port 2594 |
| `docker-compose.yml` | compose project `uo`: only `sl-ddns`, the public DNS updater, since cc-P108, and since cc-P668 its gate `sl-ddns-gate` |
| `ddns-gate/` | cc-P668: the gate, sl-ddns's only address source: Haven's address (`docker/homeip`), or a refusal that leaves the record alone |
| `Test-DdnsSafe.ps1` | cc-P668: is sl-ddns the gate-fed one? `-Fix` stops one that is not; `-Reach` checks the way to Haven. Run by the test shard's start script |
| `retired/docker-compose.live.yml` | the retired live service, a record |
| `SERVER.md`, `COMMANDS.md` | history of the server's setup; written while live ran |
