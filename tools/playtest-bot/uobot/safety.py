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


class UnsafeTarget(Exception):
    """Raised before or instead of talking to anything but the scratch shard."""


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
