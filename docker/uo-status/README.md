# uo-status: the shard's own status feed

> **This runs on the Windows box (`192.168.1.58`), not on Haven,** because it serves files the
> shard writes on this machine. Start or recreate it with `Start-UoStatus.ps1` in this folder
> (it sets `SERVER_PATH`, which this folder has no `.env` for). Haven reaches it through NPM's
> `/api/` route. `sl-shard-probe` (up/down for ports 2593 and 2594) runs beside it.

The website runs on **Haven**. The shard runs on the **Windows box**. So the page cannot read
Docker, and something has to carry the facts across. This is that something, and it is deliberately
the smallest thing that works: **the shard writes a file, nginx serves the file, the proxy routes to
it.** No Docker socket, no scraping, no second source of truth.

## Why the shard writes it and not a watcher

The obvious design is a watcher that reads `docker logs` and counts connections. ModernUO does log
them, at `Projects/Server/Network/NetState/NetState.cs:118` and `:1093`:

```
Client: {ip}: Connected. [{N} Online]
Client: {ip}: Disconnected. [{N} Online]
```

That gives a count, and it is the wrong count. `_instances.Count` counts **NetStates**, so somebody
sitting at the character-select screen is "Online". Worse, **character names are never logged at
all.** There is no line anywhere in the tree that says who entered the world.

So a watcher cannot answer the question Chase asked. The shard can, because it is holding the
answer in memory. `ShardStatusPublisher` in `server/customizations` writes it out.

## The contract

`status.json`, served at `/api/status.json`. Schema 1:

```json
{
  "schema": 1,
  "generatedAt": "2026-09-29T17:30:00Z",
  "shard":   { "name": "Shattered Legacy", "startedAt": "2026-09-28T14:07:18Z", "uptimeSeconds": 98765 },
  "players": { "count": 3, "names": ["Cain", "Elowen", "Bram"] },
  "world":   { "lastSaveAt": "2026-09-29T17:20:01Z" }
}
```

**There is no `"online": true` field, on purpose.** A file the shard writes itself cannot report its
own death: if the shard stops, the file simply stops changing and keeps claiming everything is fine.

So liveness is the **reader's** job, and the rule is one line: **if `generatedAt` is older than
three minutes, the shard is offline.** The publisher rewrites the file every 30 seconds whether or
not anything changed, precisely so that a stale timestamp means something. Any consumer that skips
this check will show a dead shard as online, indefinitely.

`names` is capped and may be shorter than `count`. Render `count` as the truth and `names` as a
courtesy.

## Moving parts

| Piece | Machine | What it does |
|---|---|---|
| `ShardStatusPublisher.cs` | in `sl-modernuo` | writes `status.json` every 30s |
| bind mount | Windows box | `server/lib/uo/modernuo/status` to `/var/lib/uo/modernuo/status` |
| `sl-uo-status` | Windows box | nginx, serves that folder on **8092**, read-only |
| NPM custom location | Haven | `/api/` to `http://192.168.1.58:8092/` |
| the panel | the page | fetches `/api/status.json`, same origin, no CORS |

Same origin is why the proxy route exists rather than the page calling `192.168.1.58:8092`
directly. A direct call would need CORS, would leak a LAN address into public HTML, and would fail
for anyone off the LAN.

## What this is not

It is not an admin feed. It carries no image IDs, no container states, no save file counts, no
drift. Those live in the Shard Console and on the dashboard, both of which stay on the LAN.
