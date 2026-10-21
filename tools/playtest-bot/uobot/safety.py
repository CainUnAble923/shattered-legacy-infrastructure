"""The bot's one target, hard-coded (cc-P91 rule 4). It refuses anything else.

Two targets exist, and the run picks one with configure() (cc-P224):

- "scratch" (the default, cc-P91): the scratch shard listens on 0.0.0.0:2595 inside its container and is published only
  on 127.0.0.1:2595 (tools/playtest-bot/docker-compose.bot.yml). The live shard is 2593 and the real test shard 2594:
  the bot must never reach either. So the host must be 127.0.0.1, the port 2595, and after login the relay packet
  (0x8C, which carries the address and port the server believes it listens on; docker/uo/docker-compose.test.yml
  explains why that matters, bug D8) must say 127.0.0.1:2595 too.
- "test" (cc-P224): THIS PC's own test shard, sl-modernuo-test on 2594, started by docker/uo/Start-TestShard.ps1 on a
  Golem's PC. The host is still 127.0.0.1 and nothing else; the port is 2594; 2593 (live) stays forbidden. The relay
  must name port 2594, but its address is only recorded: Start-TestShard.ps1 writes serverListing.address as the game
  PC's 192.168.1.58 on every PC, so on another PC the relay points at another machine's test shard. The bot never dials
  the relay's address in either target; it reconnects to 127.0.0.1 and the port it was configured for.

Anything else disconnects, is logged, and aborts the run.
"""

HOST = "127.0.0.1"
PORT = 2595
FORBIDDEN_PORTS = (2593, 2594)
TARGET = "scratch"
TARGETS = {"scratch": (2595, (2593, 2594), "the scratch shard"), "test": (2594, (2593,), "this PC's test shard")}
LIVE_PORT = 2593


# cc-P215: the Golem login checks (uobot/golemcheck.py) also talk to TEST shards: a PC's own (2594) and the game PC's real
# Test Shard (2594, over the LAN or the tailnet), and scratch shards (2595-2599). Never the live port, from any host. The
# overnight run keeps the scratch-only rule above; only golemcheck uses these two.
LIVE_PORT = 2593
TEST_PORTS = (2594, 2595, 2596, 2597, 2598, 2599)


class UnsafeTarget(Exception):
    """Raised before or instead of talking to anything but the chosen shard."""


def configure(target):
    """Picks the target: "scratch" (127.0.0.1:2595) or "test" (127.0.0.1:2594). Nothing else is accepted."""
    global PORT, FORBIDDEN_PORTS, TARGET
    if target not in TARGETS:
        raise UnsafeTarget("refused: %r is not a target the bot knows (%s)" % (target, ", ".join(sorted(TARGETS))))
    PORT, FORBIDDEN_PORTS, _ = TARGETS[target]
    TARGET = target


def describe():
    return "%s %s:%d" % (TARGETS[TARGET][2], HOST, PORT)


def check_test_target(host, port):
    """A Golem login check's address: a test or scratch port on any host, never the live port. Returns None, or raises."""
    if port == LIVE_PORT:
        raise UnsafeTarget("refused: port %s is the live shard; Golem login checks talk to test shards only" % port)
    if port not in TEST_PORTS:
        raise UnsafeTarget("refused: port %s is not a test shard port %s" % (port, TEST_PORTS))
    if not host:
        raise UnsafeTarget("refused: no host")


def check_test_relay(ip_bytes, port, target_host, target_port):
    """The relay after a Golem login check's account login: its port must be the one we dialled (a test shard advertises
    its LAN address, which another PC may not reach, so the game connection goes back to the host we dialled). Returns
    the host to connect to, or raises."""
    if len(ip_bytes) != 4:
        raise UnsafeTarget("refused: relay address is not 4 bytes")
    if port == LIVE_PORT or port != target_port:
        raise UnsafeTarget("relay %s:%s: refused, not the test shard port %s we dialled" % (".".join(str(b) for b in ip_bytes), port, target_port))
    check_test_target(target_host, port)
    return target_host


def check_target(host, port):
    """The address the bot is about to connect to. Returns None, or raises UnsafeTarget."""
    if port == LIVE_PORT:
        raise UnsafeTarget("refused: port %s is the live shard; the bot only talks to %s:%d" % (port, HOST, PORT))
    if port in FORBIDDEN_PORTS:
        raise UnsafeTarget("refused: port %s is a real shard (2593 live, 2594 test); the bot only talks to %s:%d" % (port, HOST, PORT))
    if host != HOST or port != PORT:
        raise UnsafeTarget("refused: %s:%s is not %s" % (host, port, describe()))


def check_relay(ip_bytes, port):
    """The 0x8C relay's address (4 bytes, as sent) and port. Returns the address as a string, or raises.

    scratch: the address must be 127.0.0.1 and the port 2595 (as before).
    test: the port must be 2594 and never 2593; the address is returned for the transcript and is NOT dialled (the bot
    reconnects to 127.0.0.1, see the module header)."""
    if len(ip_bytes) != 4:
        raise UnsafeTarget("refused: relay address is not 4 bytes")
    host = ".".join(str(b) for b in ip_bytes)
    if TARGET == "test":
        if port != PORT:
            raise UnsafeTarget("relay %s:%s: refused: the relay does not name port %d (%s)" % (host, port, PORT, describe()))
        return host
    try:
        check_target(host, port)
    except UnsafeTarget as e:
        raise UnsafeTarget("relay %s:%s: %s" % (host, port, e))
    return host
