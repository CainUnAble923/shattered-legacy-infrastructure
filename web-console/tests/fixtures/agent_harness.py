"""cc-P186 tests: the real web console, as a remote Golem's agent sees it, for D:/UO/golem/tests/RemoteAgent.Tests.ps1.

    python -P agent_harness.py <dir>

In <dir>: a registry with the Iron Golem on this "PC" (HARNESS-PC) and the Gold Golem on another (STEPHS-DESKTOP);
Gold's agent secret minted (written to <dir>/agent-Gold.key, as Chase would copy it to that PC); commands queued for
Gold the way the page queues them (Wake on Opus, Rest, Run next P12), plus three no button makes, written straight into
the queue (a key no agent runs, a prompt that is not P<nn>, a model that is not Fable, Opus or Sonnet). The real
handlers listen on a loopback port behind the sidecar's real proof forwarder (tailscale/proof_proxy.py) on another,
as Tailscale Serve would reach them; <dir>/server.url names the forwarder. Serves until <dir>/stop exists (at most
three minutes). Everything stays in <dir>. ASCII only."""
import json
import os
import socket
import sys
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, WEB)
sys.path.insert(0, os.path.join(WEB, "tailscale"))
import panels  # noqa: E402
import proof_proxy  # noqa: E402
import server  # noqa: E402

PROOF = "p186-harness-proof-0123456789abcdef0123456789abcdef"


def free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    p = s.getsockname()[1]
    s.close()
    return p


def main(root):
    data, groot, repo = os.path.join(root, "data"), os.path.join(root, "golem"), os.path.join(root, "repo")
    for d in (data, groot, repo):
        os.makedirs(d, exist_ok=True)
    with open(os.path.join(data, "settings.json"), "w", encoding="utf-8") as f:
        json.dump({"allowedLogins": ["chase@example.com"], "agentCommandMinutes": 30}, f)
    with open(os.path.join(data, "proof.key"), "w", encoding="ascii") as f:
        f.write(PROOF)
    work = {"code": "D:\\ShatteredLegacy", "docs": "D:\\UO\\shard-migration"}
    reg = {"schema": 1, "golems": [
        {"name": "Iron", "machine": "HARNESS-PC", "engine": "claude", "lane": "Opus", "areas": ["*"], "work": work, "enabled": True, "integrator": True, "hours": ""},
        {"name": "Gold", "machine": "STEPHS-DESKTOP", "engine": "claude", "lane": "any", "areas": ["*"], "work": work, "enabled": True, "integrator": False, "hours": ""}]}
    with open(os.path.join(groot, "golems.json"), "w", encoding="utf-8") as f:
        json.dump(reg, f)
    console = server.Console(repo, os.path.join(root, "notes"), data, os.path.join(data, "console-status.json"), collector=False,
                             golem_root=groot, machine="HARNESS-PC")
    with open(os.path.join(root, "agent-Gold.key"), "w", encoding="ascii") as f:
        f.write(console.mint_agent("Gold") + "\n")
    gold = console.golem("Gold")
    buttons = {b["key"]: b for b in console.buttons()}
    for key, typed, inputs in (("wake", "wake the golem", {"model": "Opus"}), ("rest", "rest", {}), ("runnext", "P12", {"prompt": "P12"})):
        code, out = console.queue_remote(buttons[key], typed, inputs, "chase@example.com", gold)
        assert code == 202, (key, code, out)
        time.sleep(0.01)   # ids are by the millisecond: keep the order
    now = time.time()
    for i, (key, inputs) in enumerate((("format", {}), ("runnext", {"prompt": "P1;calc"}), ("wake", {"model": "Haiku"}))):
        cid = "%013d-%06x" % (int(now * 1000) + 100 + i, i)
        console._save_command("Gold", {"id": cid, "golem": "Gold", "key": key, "label": key, "action": "", "inputs": inputs, "who": "test",
                                       "queuedAt": console._iso(now), "expiresAt": console._iso(now + 1800), "minutes": 30, "state": "queued",
                                       "takenAt": None, "endedAt": None, "code": None, "result": "", "lines": []})
    tail, front = free_port(), free_port()
    s = server.ThreadingHTTPServer(("127.0.0.1", tail), server.make_handler(console, panels.TAILNET, os.path.join(data, "access.log")))
    threading.Thread(target=s.serve_forever, daemon=True).start()
    fwd = proof_proxy.make_server("127.0.0.1", front, "127.0.0.1", tail, PROOF)
    threading.Thread(target=fwd.serve_forever, daemon=True).start()
    with open(os.path.join(root, "server.url"), "w", encoding="ascii") as f:
        f.write("http://127.0.0.1:%d\n" % front)
    end = time.time() + 180
    while time.time() < end and not os.path.exists(os.path.join(root, "stop")):
        time.sleep(0.1)
    fwd.shutdown()
    s.shutdown()


if __name__ == "__main__":
    main(sys.argv[1])
