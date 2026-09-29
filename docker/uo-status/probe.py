"""Shard up/down probe. Writes shards.json into the status folder every INTERVAL seconds.

Why not a plain TCP connect: Docker Desktop's port proxy ACCEPTS a connection on a published
port whether or not the server behind it is listening, then drops it. So "connected" proves
nothing (see the state doc, "Diagnostics that prove nothing").

The rule used instead: connect, send nothing, and wait HOLD seconds.
  - refused, unreachable or timed out      -> down
  - the other end closes within HOLD       -> down (the proxy stand-in, nothing behind it)
  - still open after HOLD, or sends data   -> up   (a real server waiting for the client seed)

Sends no bytes, so it never reaches login and never creates an account. Each probe does show
in the shard's log as one Connected and one Disconnected line.

Like status.json, the file cannot report the probe's own death, so readers treat a stale
generatedAt (older than 3 minutes) as "unknown", never as "down".
"""
import json, os, socket, time, datetime

HOST = os.environ.get("PROBE_HOST", "host.docker.internal")
TARGETS = [("live", "Live shard", 2593), ("test", "Test shard", 2594)]
INTERVAL = int(os.environ.get("PROBE_INTERVAL", "30"))
HOLD = float(os.environ.get("PROBE_HOLD", "1.5"))
OUT = "/status/shards.json"


def probe(port):
    try:
        s = socket.create_connection((HOST, port), timeout=3)
    except OSError:
        return False
    try:
        s.settimeout(HOLD)
        try:
            data = s.recv(1)
            return bool(data)          # b"" means the other end closed: nothing behind the proxy
        except socket.timeout:
            return True                # held open: a real server waiting for the seed
        except OSError:
            return False               # reset
    finally:
        try:
            s.close()
        except OSError:
            pass


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


while True:
    shards = [{"id": i, "name": n, "port": p, "up": probe(p)} for i, n, p in TARGETS]
    payload = {"schema": 1, "generatedAt": now(), "shards": shards}
    tmp = OUT + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2)
    os.replace(tmp, OUT)
    print(payload["generatedAt"], " ".join("%s=%s" % (s["id"], "up" if s["up"] else "down") for s in shards), flush=True)
    time.sleep(INTERVAL)
