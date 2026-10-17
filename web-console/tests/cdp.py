"""cc-P155, tests only: just enough of the Chrome DevTools Protocol, over a standard-library WebSocket, to show a page in
Microsoft Edge (headless) as a phone shows it. Headless Edge will not make a window narrower than about 500 px
(--window-size=390 gave a 492 px viewport), so the phone's 390 x 844 is set with Emulation.setDeviceMetricsOverride.

    with Browser(edge_exe) as b:
        b.phone(390, 844)
        b.open(url)
        b.wait_for("document.documentElement.getAttribute('data-drawn')")
        b.eval("window.innerWidth"); b.screenshot(path)
"""
import base64
import json
import os
import shutil
import socket
import struct
import subprocess
import tempfile
import time
import urllib.request


class Browser:
    def __init__(self, exe):
        self.exe, self.proc, self.sock, self.n = exe, None, None, 0
        self.prof = tempfile.mkdtemp(prefix="p155-cdp-")

    def __enter__(self):
        self.proc = subprocess.Popen([self.exe, "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
                                      "--remote-debugging-port=0", "--user-data-dir=" + self.prof, "about:blank"],
                                     stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        port_file = os.path.join(self.prof, "DevToolsActivePort")
        for _ in range(200):
            if os.path.exists(port_file) and os.path.getsize(port_file):
                break
            time.sleep(0.05)
        with open(port_file, encoding="ascii") as f:
            port = int(f.read().split()[0])
        pages = json.loads(urllib.request.urlopen("http://127.0.0.1:%d/json/list" % port, timeout=10).read())
        ws = [p for p in pages if p.get("type") == "page"][0]["webSocketDebuggerUrl"]
        self._connect(ws)
        return self

    def __exit__(self, *a):
        try:
            self.sock.close()
        except Exception:
            pass
        try:
            self.proc.kill()
            self.proc.wait(10)
        except Exception:
            pass
        shutil.rmtree(self.prof, ignore_errors=True)

    # --- the WebSocket (RFC 6455, client side: masked frames out, unmasked in) -------------------------------------------
    def _connect(self, url):
        rest = url.split("://", 1)[1]
        hostport, path = rest.split("/", 1)
        host, port = hostport.split(":")
        self.sock = socket.create_connection((host, int(port)), timeout=30)
        key = base64.b64encode(os.urandom(16)).decode()
        self.sock.sendall(("GET /%s HTTP/1.1\r\nHost: %s\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                           "Sec-WebSocket-Key: %s\r\nSec-WebSocket-Version: 13\r\n\r\n" % (path, hostport, key)).encode())
        buf = b""
        while b"\r\n\r\n" not in buf:
            buf += self.sock.recv(4096)
        if b" 101 " not in buf.split(b"\r\n")[0]:
            raise RuntimeError("no WebSocket: " + buf[:80].decode("latin-1"))

    def _send(self, text):
        data = text.encode()
        head = bytearray([0x81])
        n = len(data)
        if n < 126:
            head.append(0x80 | n)
        elif n < 65536:
            head.append(0x80 | 126)
            head += struct.pack(">H", n)
        else:
            head.append(0x80 | 127)
            head += struct.pack(">Q", n)
        mask = os.urandom(4)
        self.sock.sendall(bytes(head) + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(data)))

    def _exact(self, n):
        out = b""
        while len(out) < n:
            chunk = self.sock.recv(n - len(out))
            if not chunk:
                raise RuntimeError("the browser closed the connection")
            out += chunk
        return out

    def _recv(self):
        msg = b""
        while True:
            b0, b1 = self._exact(2)
            n = b1 & 0x7F
            if n == 126:
                n = struct.unpack(">H", self._exact(2))[0]
            elif n == 127:
                n = struct.unpack(">Q", self._exact(8))[0]
            payload = self._exact(n)
            if (b0 & 0x0F) in (1, 0):
                msg += payload
                if b0 & 0x80:
                    return msg.decode("utf-8", "replace")

    def call(self, method, **params):
        self.n += 1
        my = self.n
        self._send(json.dumps({"id": my, "method": method, "params": params}))
        while True:
            m = json.loads(self._recv())
            if m.get("id") == my:
                if "error" in m:
                    raise RuntimeError("%s: %s" % (method, m["error"]))
                return m.get("result", {})

    # --- what the tests use -----------------------------------------------------------------------------------------
    def phone(self, width=390, height=844):
        self.call("Emulation.setDeviceMetricsOverride", width=width, height=height, deviceScaleFactor=3, mobile=True)

    def open(self, url):
        self.call("Page.enable")
        self.call("Page.navigate", url=url)

    def eval(self, expr):
        r = self.call("Runtime.evaluate", expression=expr, returnByValue=True)
        return (r.get("result") or {}).get("value")

    def wait_for(self, expr, seconds=15):
        end = time.time() + seconds
        while time.time() < end:
            v = self.eval(expr)
            if v:
                return v
            time.sleep(0.2)
        return None

    def screenshot(self, path):
        r = self.call("Page.captureScreenshot", format="png")
        with open(path, "wb") as f:
            f.write(base64.b64decode(r["data"]))
