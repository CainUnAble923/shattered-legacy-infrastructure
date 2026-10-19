"""cc-P182: the uoconsole sidecar's proof forwarder. Closes P155's Deviation 2.

The web console's tailnet listener (127.0.0.1:8096 on the game PC) is reachable from any container on the game PC's
Docker (as host.docker.internal), and such a container could forge Tailscale-User-Login. So the console also demands
X-UOConsole-Proof: a secret (D:\\UO\\web-console\\proof.key, outside git) that only this forwarder and the console hold.

    Tailscale Serve (https://uoconsole...:443, sets Tailscale-User-Login)
      -> http://127.0.0.2:8097  this forwarder, in the sidecar's own network namespace (network_mode: service:tailscale)
      -> http://host.docker.internal:8096  the web console, with X-UOConsole-Proof set (any the caller sent is dropped)

Why 127.0.0.2: in userspace mode tailscaled hands a tailnet connection to a port it does not serve to 127.0.0.1 in this
namespace, so a peer dialing uoconsole:8097 directly would reach a forwarder bound there and get the proof added to its
own forged login. Bound to 127.0.0.2 it is reachable only from inside this namespace, which is Serve. No other
container shares the namespace, and no port is published. Standard library only (python:3.12 alpine). ASCII only.
"""
import argparse
import http.client
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PROOF_HEADER = "X-UOConsole-Proof"
SKIP = {"connection", "keep-alive", "proxy-authenticate", "proxy-authorization", "te", "trailers", "transfer-encoding",
        "upgrade", PROOF_HEADER.lower()}
SKIP_BACK = SKIP | {"server", "date"}
MAX_BODY = 1_000_000


def make_server(bind, port, up_host, up_port, proof):
    class Forward(BaseHTTPRequestHandler):
        server_version = "uoconsole-proof"
        sys_version = ""

        def log_message(self, fmt, *a):  # nothing logged: the console keeps the access log
            pass

        def _forward(self):
            try:
                n = int(self.headers.get("Content-Length") or 0)
            except ValueError:
                n = -1
            if n < 0 or n > MAX_BODY:
                self.send_error(413)
                return
            body = self.rfile.read(n) if n else None
            headers = {k: v for k, v in self.headers.items() if k.lower() not in SKIP}
            headers[PROOF_HEADER] = proof
            conn = http.client.HTTPConnection(up_host, up_port, timeout=60)
            try:
                conn.request(self.command, self.path, body=body, headers=headers)
                r = conn.getresponse()
                self.send_response(r.status, r.reason)
                for k, v in r.getheaders():
                    if k.lower() not in SKIP_BACK:
                        self.send_header(k, v)
                self.end_headers()
                while True:
                    chunk = r.read(65536)
                    if not chunk:
                        break
                    if self.command != "HEAD":
                        self.wfile.write(chunk)
            except OSError:
                try:
                    self.send_error(502, "the web console did not answer")
                except OSError:
                    pass
            finally:
                conn.close()

        do_GET = do_HEAD = do_POST = do_PUT = do_DELETE = do_PATCH = do_OPTIONS = _forward

    return ThreadingHTTPServer((bind, port), Forward)


def bind_refusal(bind):
    """Why the forwarder must not listen on bind, or '': only a loopback address other than 127.0.0.1."""
    if not bind.startswith("127.") or bind == "127.0.0.1":
        return "refused: bind a loopback address other than 127.0.0.1 (tailscaled forwards tailnet connections there)"
    return ""


def main(argv=None):
    ap = argparse.ArgumentParser(description="the uoconsole sidecar's proof forwarder (cc-P182)")
    ap.add_argument("--bind", default="127.0.0.2")
    ap.add_argument("--port", type=int, default=8097)
    ap.add_argument("--upstream", default="host.docker.internal:8096")
    ap.add_argument("--proof-file", default="/run/uoconsole/proof.key")
    a = ap.parse_args(argv)
    why = bind_refusal(a.bind)
    if why:
        sys.exit(why)
    try:
        with open(a.proof_file, "r", encoding="ascii") as f:
            proof = f.read().strip()
    except OSError as e:
        sys.exit("refused: cannot read the proof file: %s" % e)
    if len(proof) < 32:
        sys.exit("refused: the proof file holds no proof (32 characters or more); run Install-WebConsole.ps1")
    host, _, port = a.upstream.rpartition(":")
    srv = make_server(a.bind, a.port, host, int(port), proof)
    print("proof forwarder on %s:%d -> %s" % (a.bind, a.port, a.upstream), flush=True)
    srv.serve_forever()


if __name__ == "__main__":
    main()
