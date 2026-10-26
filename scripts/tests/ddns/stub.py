#!/usr/bin/env python3
"""cc-P668 proof only: a stand-in for Haven's sl-homeip and for the public "what is my IP" services.

ROUTES is "path=answer;path=answer", for example "/haven=8.8.4.4;/egress=159.26.100.174". Each path answers 200 and
its text; any other path 404. Runs on the proof's own compose network; never on a host port. ASCII only.
"""
import os
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROUTES = dict(p.split('=', 1) for p in os.environ.get('ROUTES', '').split(';') if '=' in p)


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        body = ROUTES.get(self.path.split('?')[0])
        data = ((body if body is not None else 'not found') + '\n').encode('ascii')
        self.send_response(200 if body is not None else 404)
        self.send_header('Content-Type', 'text/plain')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def log_message(self, fmt, *args):
        pass


ThreadingHTTPServer(('0.0.0.0', 8000), Handler).serve_forever()
