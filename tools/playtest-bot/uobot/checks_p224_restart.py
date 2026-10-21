"""cc-P224: P100-6, the Visual Builder's undo history survives a save and a restart. Imported last, because it restarts
the shard the whole run is talking to: every long-lived client is dropped and reconnects (ctx.ensure_* make new ones).

Only the test shard this run is configured for is restarted (safety.TARGET must be "test"; the container name comes
from the run's --container, which the runner sets to sl-modernuo-test). On the scratch shard (the nightly runner) the
check reports that it did not run, rather than restart a container it does not own.
"""
import subprocess
import time

from .checks import check
from .checks_p224 import wait_text
from . import safety
from .checks_p224_vbuild import go_square, place_set, two_corners, undo_all, box_items, close_builder, B_WIPE, BENCH, FLOOR


def restart_container(ctx):
    """docker restart with the shutdown save's grace (D50: 60 s), then wait for the shard to listen again."""
    name = ctx.container
    t0 = time.time()
    r = subprocess.run(["docker", "restart", "-t", "60", name], capture_output=True, text=True, timeout=180)
    if r.returncode != 0:
        raise RuntimeError("docker restart %s failed: %s %s" % (name, r.stdout, r.stderr))
    # RFC 3339 with the Z: without it docker reads the stamp as local time and returns nothing (seen 2026-10-20).
    since = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(t0 - 2))
    end = time.time() + 300
    while time.time() < end:
        logs = ctx.docker_logs_since(since)
        if "Listening:" in logs and "2594" in logs:
            time.sleep(3.0)
            return round(time.time() - t0, 1)
        time.sleep(3.0)
    raise RuntimeError("the shard did not listen again within 300 s of the restart")


@check("P100-6", "After [save and a restart, [VBuild undo still puts a wiped corner back")
def p100_6(ctx):
    # ClusterFVisualBuilderStore (ClusterFVisualBuilder.cs:613-700) saves the sessions' steps with the world, so undo
    # after a restart re-creates the wiped records (:275-291). World mode is not saved (off after a load).
    if safety.TARGET != "test":
        raise RuntimeError("not run: this check restarts the test shard (sl-modernuo-test) and the run targets %s" % safety.describe())
    a = ctx.ensure_admin()
    bx, by, z = go_square(a)
    a.sleep(1.0)
    undo_all(a)
    placed, _ = place_set(a, bx, by, z)
    said = two_corners(a, B_WIPE, bx - 2, by - 2, bx + 6, by + 6, z)
    wiped = next((t for t in said if t.startswith("Wiped ")), None)
    close_builder(a)
    a.sleep(0.5)
    n = len(a.messages)
    a.command("[save", 1.0)
    save_said = a.texts_since(n)[:2]
    a.sleep(4.0)
    # The restart drops every client. Close ours first so their accounts are free when the shard is back.
    for c in (ctx.admin, ctx.player):
        if c is not None:
            c.close()
    ctx.admin = ctx.player = None
    secs = restart_container(ctx)
    a = ctx.ensure_admin()
    bx2, by2, z2 = go_square(a)
    a.sleep(1.5)
    n = len(a.messages)
    a.command("[VBuild undo", 1.0)
    undo_msg = wait_text(a, r"^(Undone|Nothing of yours)", n, 8)
    a.sleep(1.5)
    back = [it for it in box_items(a, bx2 - 2, by2 - 2, 9).values() if it.get("graphic") in (BENCH, FLOOR)]
    rest = undo_all(a)
    a.sleep(1.0)
    # Whatever the undo history could not take back (seen 2026-10-20: after a restart the paint steps undo 0 items and
    # their benches stay) is deleted by hand, so the next run starts clean; the count is evidence.
    orphans = [s for s, it in box_items(a, bx2 - 2, by2 - 2, 9).items() if it.get("graphic") in (BENCH, FLOOR)]
    for s_ in orphans:
        a.command("[Delete", 0.2)
        if a.wait_target(6):
            a.target_obj(s_)
        a.sleep(0.3)
    ok = len(placed) == 11 and wiped == "Wiped 11 item(s). Undo puts back them." and undo_msg == "Undone: 11 item(s). 3 step(s) left."         and len(back) == 11 and not orphans
    return ok, "placed %d, wipe said %r, [save said %s, shard back in %s s; after the restart [VBuild undo said %r and %d of the 11 pieces are back; then %s; left in the corner after every undo and deleted by hand: %d" % (
        len(placed), wiped, save_said, secs, undo_msg, len(back), rest[:4], len(orphans))
