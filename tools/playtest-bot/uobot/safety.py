"""The bot's one target, hard-coded (cc-P91 rule 4). It refuses anything else.

The scratch shard listens on 0.0.0.0:2595 inside its container and is published only on 127.0.0.1:2595
(tools/playtest-bot/docker-compose.bot.yml). The live shard is 2593 and the real test shard 2594: the bot must never
reach either. So the host must be 127.0.0.1, the port 2595, and after login the relay packet (0x8C, which carries the
address and port the server believes it listens on; docker/uo/docker-compose.test.yml explains why that matters, bug
D8) must say 127.0.0.1:2595 too. Anything else disconnects, is logged, and aborts the run.
"""

HOST = "127.0.0.1"
PORT = 2595
FORBIDDEN_PORTS = (2593, 2594)


# cc-P215: the Golem login checks (uobot/golemcheck.py) also talk to TEST shards: a PC's own (2594) and the game PC's real
# Test Shard (2594, over the LAN or the tailnet), and scratch shards (2595-2599). Never the live port, from any host. The
# overnight run keeps the scratch-only rule above; only golemcheck uses these two.
LIVE_PORT = 2593
TEST_PORTS = (2594, 2595, 2596, 2597, 2598, 2599)


class UnsafeTarget(Exception):
    """Raised before or instead of talking to anything but the scratch shard."""


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
    if port in FORBIDDEN_PORTS:
        raise UnsafeTarget("refused: port %s is a real shard (2593 live, 2594 test); the bot only talks to %s:%d" % (port, HOST, PORT))
    if host != HOST or port != PORT:
        raise UnsafeTarget("refused: %s:%s is not the scratch shard %s:%d" % (host, port, HOST, PORT))


def check_relay(ip_bytes, port):
    """The 0x8C relay's address (4 bytes, as sent) and port. Returns the address as a string, or raises."""
    if len(ip_bytes) != 4:
        raise UnsafeTarget("refused: relay address is not 4 bytes")
    host = ".".join(str(b) for b in ip_bytes)
    try:
        check_target(host, port)
    except UnsafeTarget as e:
        raise UnsafeTarget("relay %s:%s: %s" % (host, port, e))
    return host
