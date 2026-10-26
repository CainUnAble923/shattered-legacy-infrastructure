# docker/homeip: sl-homeip on Haven (cc-P668)

The house's public IPv4, as plain text, for the game PC's DDNS gate (`docker/uo/ddns-gate`). Haven goes out on the
home connection, so its address is the house's whatever the game PC's VPN is doing.

| File | What |
|---|---|
| `homeip.py` | asks two public lookups every 3 minutes, keeps the answer only when they agree; `/ip`, `/status`, `/health` |
| `docker-compose.yml` | project `sl-homeip`, `192.168.1.61:8098` and `127.0.0.1:8098` only (Tailscale Serve :8098) |
| `Deploy-HavenHomeIp.ps1` | `-Mode Discover|Deploy|Rollback`: reads Haven first, prints the plan, waits for a typed word |

`/ip` answers 503 when there is no agreed answer younger than 15 minutes; the gate then refuses and sl-ddns leaves the
record alone. Never public, never routed by NPM. The inventory entry is `homeip` in `docker/haven/services.json`; the
prose is `shard-migration/notes/haven-runbook.md`. Notes: `shard-migration/notes/cc-P668-ddns-home-ip.md`.
