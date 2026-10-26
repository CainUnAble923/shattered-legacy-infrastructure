#!/usr/bin/env python3
"""cc-P668: Haven's public IPv4, as plain text, for the game PC's DDNS gate (docker/uo/ddns-gate).

Haven goes out on the home connection, never through a VPN, so its public address is the house's. Every REFRESH
seconds it asks each of LOOKUPS (two public "what is my IP" services) and keeps the answer only when they all agree.

  GET /ip      200 and the address (text) when the last agreed answer is younger than MAX_AGE; otherwise 503 and why
  GET /status  JSON: ip, agreed, checkedAt, age, each lookup's answer or error
  GET /health  200 "ok" (the process is up; says nothing about the address)

Published on Haven's LAN address and loopback only (Tailscale Serve forwards the tailnet port to loopback). Never
routed by NPM, never public. Standard library only. ASCII only.
"""
import ipaddress
import json
import os
import sys
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

LOOKUPS = [u.strip() for u in os.environ.get(
    'LOOKUPS', 'https://api.ipify.org,https://ipv4.icanhazip.com').split(',') if u.strip()]
REFRESH = int(os.environ.get('REFRESH', '180'))
MAX_AGE = int(os.environ.get('MAX_AGE', '900'))
TIMEOUT = float(os.environ.get('TIMEOUT', '10'))
PORT = int(os.environ.get('PORT', '8000'))

state = {'ip': None, 'agreed': False, 'checkedAt': None, 'agreedAt': None, 'lookups': {}, 'error': 'not checked yet'}
lock = threading.Lock()


def log(msg):
    print(time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()) + ' ' + msg, flush=True)


def public_ipv4(text):
    """The text as a public IPv4 address string, or None."""
    try:
        ip = ipaddress.ip_address(text.strip())
    except ValueError:
        return None
    if ip.version != 4 or not ip.is_global:
        return None
    return str(ip)


def ask(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'sl-homeip (cc-P668)'})
    with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
        return r.read(64).decode('ascii', 'replace').strip()


def decide(answers):
    """answers: {url: ip-or-None}. Returns (ip, error): ip only when every lookup gave the same public IPv4."""
    if len(answers) < 2:
        return None, 'needs at least two lookups, has ' + str(len(answers))
    bad = [u for u, ip in answers.items() if not ip]
    if bad:
        return None, 'no usable answer from ' + ', '.join(bad)
    ips = sorted(set(answers.values()))
    if len(ips) != 1:
        return None, 'lookups disagree: ' + ', '.join(u + '=' + ip for u, ip in answers.items())
    return ips[0], None


def check_once():
    answers, shown = {}, {}
    for url in LOOKUPS:
        try:
            raw = ask(url)
            ip = public_ipv4(raw)
            answers[url] = ip
            shown[url] = ip if ip else 'not an IPv4 address: ' + raw[:40]
        except Exception as e:  # any failure is just "no answer" from this lookup
            answers[url] = None
            shown[url] = 'error: ' + str(e)[:120]
    ip, err = decide(answers)
    now = time.time()
    with lock:
        state['checkedAt'] = now
        state['lookups'] = shown
        if ip:
            if state['ip'] != ip:
                log('home IP is ' + ip + (' (was ' + state['ip'] + ')' if state['ip'] else ''))
            state.update(ip=ip, agreed=True, agreedAt=now, error=None)
        else:
            state.update(agreed=False, error=err)
            log('NOT UPDATED: ' + err + ' (still serving ' + str(state['ip']) + ' until it is ' + str(MAX_AGE) + ' s old)')


def current():
    """(ip, None) when there is an agreed answer younger than MAX_AGE, else (None, why)."""
    with lock:
        if not state['ip'] or state['agreedAt'] is None:
            return None, 'no agreed address yet: ' + str(state['error'])
        age = time.time() - state['agreedAt']
        if age > MAX_AGE:
            return None, 'last agreed address is %d s old (limit %d): %s' % (age, MAX_AGE, state['error'])
        return state['ip'], None


def loop():
    while True:
        try:
            check_once()
        except Exception as e:
            log('check failed: ' + str(e))
        time.sleep(REFRESH)


class Handler(BaseHTTPRequestHandler):
    def reply(self, code, body, ctype='text/plain; charset=ascii'):
        data = body.encode('ascii', 'replace')
        self.send_response(code)
        self.send_header('Content-Type', ctype)
        self.send_header('Content-Length', str(len(data)))
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        path = self.path.split('?')[0]
        if path == '/ip':
            ip, why = current()
            if ip:
                self.reply(200, ip + '\n')
            else:
                self.reply(503, why + '\n')
        elif path == '/status':
            with lock:
                s = dict(state)
            s['age'] = None if s['agreedAt'] is None else int(time.time() - s['agreedAt'])
            s['maxAge'] = MAX_AGE
            self.reply(200, json.dumps(s, indent=1) + '\n', 'application/json')
        elif path == '/health':
            self.reply(200, 'ok\n')
        else:
            self.reply(404, 'not found\n')

    def log_message(self, fmt, *args):
        pass  # one line per check is enough; requests are not logged


if __name__ == '__main__':
    if len(LOOKUPS) < 2:
        log('REFUSED: LOOKUPS needs at least two URLs')
        sys.exit(2)
    log('sl-homeip: lookups ' + ', '.join(LOOKUPS) + '; every ' + str(REFRESH) + ' s; stale after ' + str(MAX_AGE) + ' s')
    threading.Thread(target=loop, daemon=True).start()
    ThreadingHTTPServer(('0.0.0.0', PORT), Handler).serve_forever()
