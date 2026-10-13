"""cc-P91: the play-test bot's own facts. Standard library only; no shard needed.

    cd D:\\ShatteredLegacy\\tools\\playtest-bot
    python -m unittest discover -s tests

The target refusal (rule 4) is the reason this file exists: the bot must never talk to the live shard (2593) or the
real test shard (2594), and must refuse a relay that points anywhere but 127.0.0.1:2595.
"""
import os
import socket
import struct
import sys
import threading
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from uobot import huffman, packets as P, safety  # noqa: E402
from uobot.client import Client, Transcript, Cliloc  # noqa: E402


class TargetRefusal(unittest.TestCase):
    def test_only_the_scratch_shard_is_allowed(self):
        safety.check_target("127.0.0.1", 2595)
        for host, port in (("127.0.0.1", 2593), ("127.0.0.1", 2594), ("192.168.1.58", 2593), ("192.168.1.58", 2594),
                           ("192.168.1.58", 2595), ("localhost", 2595), ("0.0.0.0", 2595), ("127.0.0.1", 2596),
                           ("shatteredlegacyuo.com", 2593)):
            with self.assertRaises(safety.UnsafeTarget, msg="%s:%s" % (host, port)):
                safety.check_target(host, port)

    def test_a_relay_anywhere_else_is_refused(self):
        self.assertEqual("127.0.0.1", safety.check_relay(bytes([127, 0, 0, 1]), 2595))
        for ip, port in ((bytes([127, 0, 0, 1]), 2593), (bytes([127, 0, 0, 1]), 2594), (bytes([192, 168, 1, 58]), 2595),
                         (bytes([1, 0, 0, 127]), 2595), (bytes([127, 0, 0]), 2595)):
            with self.assertRaises(safety.UnsafeTarget):
                safety.check_relay(ip, port)

    def test_the_client_refuses_before_opening_a_socket(self):
        c = Client("x", "y", Transcript(os.devnull), Cliloc(None), "t")
        # The stand-in never connects anywhere, so even a broken safety.py cannot reach a real shard from this test
        # (on 2026-10-13 an earlier version passed the call through, see the cc-P91 notes).
        opened = []
        real = socket.create_connection

        def never(*a, **k):
            opened.append(a)
            raise ConnectionRefusedError("test stand-in: no real connection")

        socket.create_connection = never
        try:
            for port in (2593, 2594):
                with self.assertRaises(safety.UnsafeTarget):
                    c._connect("127.0.0.1", port)
        finally:
            socket.create_connection = real
        self.assertEqual([], opened, "no connection was even attempted")

    def test_a_server_whose_relay_points_elsewhere_is_disconnected(self):
        # A fake login server on a free port stands in for 2595: it answers 0xA8 and then a relay to a port that is not
        # the scratch shard. NEVER a real shard port: on 2026-10-13 this test relayed to 2593, and when the check was
        # broken on purpose to prove the test red, the client may have connected to the live shard (cc-P91 notes).
        # So the relay names a dead local port, and every connection except to the fake server is refused below, even
        # if safety.py is broken.
        dead = socket.socket()
        dead.bind(("127.0.0.1", 0))
        dead_port = dead.getsockname()[1]
        dead.close()
        srv = socket.socket()
        srv.bind(("127.0.0.1", 0))
        srv.listen(1)
        port = srv.getsockname()[1]
        closed = {}

        def exact(conn, n):
            got = b""
            while len(got) < n:
                more = conn.recv(n - len(got))
                if not more:
                    break
                got += more
            return got

        def serve():
            conn, _ = srv.accept()
            exact(conn, 21 + 62)
            name = P.latin1("BOT", 32)
            entry = struct.pack(">H", 0) + name + struct.pack(">BbI", 0, 0, 0)
            body = struct.pack(">BH", 0x5D, 1) + entry
            conn.sendall(struct.pack(">BH", 0xA8, 3 + len(body)) + body)
            exact(conn, 3)
            conn.sendall(struct.pack(">B4sHi", 0x8C, bytes([127, 0, 0, 1]), dead_port, 1234))
            closed["eof"] = conn.recv(1) == b""
            conn.close()

        t = threading.Thread(target=serve, daemon=True)
        t.start()
        old = safety.PORT
        import uobot.client as uc
        safety.PORT = port
        uc.PORT = port
        real = socket.create_connection
        attempts = []

        def guarded(addr, *a, **k):
            attempts.append(addr)
            if addr != ("127.0.0.1", port):
                raise AssertionError("the bot tried to connect to %s:%s" % addr)
            return real(addr, *a, **k)

        socket.create_connection = guarded
        try:
            c = Client("x", "y", Transcript(os.devnull), Cliloc(None), "t")
            with self.assertRaises(safety.UnsafeTarget):
                c.login()
        finally:
            socket.create_connection = real
            safety.PORT = old
            uc.PORT = old
        self.assertEqual([("127.0.0.1", port)], attempts, "only the fake login server was ever dialled")
        t.join(5)
        srv.close()
        self.assertTrue(closed.get("eof"), "the bot hung up on the server that relayed it elsewhere")


class Huffman(unittest.TestCase):
    def test_round_trip_with_pinned_table_and_packet_boundaries(self):
        msgs = [bytes([0x55]), bytes(range(256)), b"\x1C" + b"hello world" * 40, b""]
        stream = b"".join(huffman.encode(m) for m in msgs)
        d = huffman.Decoder()
        got = []
        for i in range(0, len(stream), 7):     # arriving in awkward pieces
            got += d.feed(stream[i:i + 7])
        self.assertEqual(msgs, got)

    def test_the_table_is_the_pinned_one(self):
        self.assertEqual(257, len(huffman.CODES))
        self.assertEqual(0, huffman.CODES[(2, 0x000)])
        self.assertEqual(256, huffman.CODES[(4, 0x00D)])


class Packets(unittest.TestCase):
    def test_fixed_lengths_of_what_the_bot_sends(self):
        self.assertEqual(21, len(P.seed(5)))
        self.assertEqual(62, len(P.account_login("a", "b")))
        self.assertEqual(3, len(P.play_server(0)))
        self.assertEqual(65, len(P.game_login(1, "a", "b")))
        self.assertEqual(73, len(P.play_character("a", 0)))
        self.assertEqual(39, len(P.delete_character(0)))
        self.assertEqual(7, len(P.move(2, 0)))
        self.assertEqual(19, len(P.target_object(1, 2)))
        self.assertEqual(19, len(P.target_ground(1, 2, 3, 4)))
        self.assertEqual(15, len(P.drop(1, 2, 3, 0, 4)))
        self.assertEqual(10, len(P.equip(1, 2, 3)))
        self.assertEqual(10, len(P.status_request(1)))
        cc = P.create_character("Bot", 6, (60, 10, 10), ((0, 50), (1, 50), (2, 0), (3, 0)), 0, 0, 0, 0, 0, 0, 0, 0)
        self.assertEqual(106, len(cc))
        self.assertEqual(6, cc[70], "genderRace at offset 70 (IncomingAccountPackets.cs:90-103)")
        self.assertEqual(b"Bot", cc[10:13])

    def test_a_chunk_whose_length_disagrees_with_the_table_is_reported(self):
        pk, probs = P.split_chunk(bytes([0x22, 1, 0]))
        self.assertEqual(([bytes([0x22, 1, 0])], []), (pk, probs))
        pk, probs = P.split_chunk(bytes([0x22, 1, 0, 0]))
        self.assertTrue(probs, "a 4-byte 0x22 is not what the table says")

    def test_gump_dd_round_trip(self):
        import zlib
        layout = b"{ text 10 10 0 0 }\x00"
        text = struct.pack(">H", 5) + "Hello".encode("utf-16-be")
        def block(b):
            z = zlib.compress(b)
            return struct.pack(">ii", 4 + len(z), len(b)) + z
        body = struct.pack(">Iiii", 7, 9, 1, 2) + block(layout) + struct.pack(">i", 1) + block(text)
        p = struct.pack(">BH", 0xDD, 3 + len(body)) + body
        g = P.parse_gump_dd(p)
        self.assertEqual((7, 9, ["Hello"]), (g["serial"], g["typeId"], g["lines"]))
        self.assertIn("text 10 10", g["layout"])


if __name__ == "__main__":
    unittest.main()
