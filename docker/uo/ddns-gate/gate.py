#!/usr/bin/env python3
"""cc-P668: sl-ddns-gate, the ONLY place sl-ddns gets the address it publishes.

On 2026-10-26 sl-ddns asked public "what is my IP" services from the game PC, which goes out through Chase's VPN,
and published the VPN's exit as shatteredlegacyuo.com. Now ddns-updater runs with PUBLICIP_FETCHERS=http and one
provider, url:https://ddns-gate:8443/ip (ddns-updater takes https only), and this gate answers it:

  1. Haven's address (docker/homeip, sl-homeip), from the first of HAVEN_URLS (LAN, then tailnet) that answers with
     a public IPv4. Haven is on the home connection, so the game PC's VPN does not matter. No answer: refuse.
     Two Haven URLs answering differently: refuse.
  2. The game PC's own egress (EGRESS_URLS, two lookups that must agree). When it differs from Haven's for longer
     than LEARN_AFTER seconds, the game PC is on a VPN (or another route) and that egress is remembered as a VPN exit.
  3. Refuse an address in BLOCK (CIDRs: known VPN exits, ProtonVPN's seen so far) or remembered as a VPN exit.
  4. Otherwise answer 200 with Haven's address.

A refusal answers 403 with no address in its text, so ddns-updater fails to get an IP and leaves the record alone;
the gate logs a loud "*** REFUSED ***" line saying why. 403, not 503: ddns-updater treats only 403 and 429 as errors
and reads an address out of any other answer's body (pkg/publicip/http/fetch.go:36-40 at 64996182, the image's
revision); the proof's HavenSaysVpnExit caught a 503 whose reason named the VPN address, which sl-ddns then used. Served over HTTPS with a self-signed certificate made by entrypoint.sh; sl-ddns
trusts it through SSL_CERT_FILE (a bundle of the public CAs plus this one). Never published on a host port: only
containers on the uo compose network reach it. Standard library only. ASCII only.

  GET /ip      200 and the address, or 403 and why (addresses masked)
  GET /status  JSON of the last decision
  GET /health  200 "ok"
"""
import ipaddress
import json
import re
import os
import ssl
import sys
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def csv(name, default):
    return [x.strip() for x in os.environ.get(name, default).split(',') if x.strip()]


HAVEN_URLS = csv('HAVEN_URLS', 'http://192.168.1.61:8098/ip,http://haven.tail81098d.ts.net:8098/ip')
EGRESS_URLS = csv('EGRESS_URLS', 'https://api.ipify.org,https://ipv4.icanhazip.com')
# ProtonVPN exits that have published themselves as shatteredlegacyuo.com: 146.70.217.106 (2026-10-04, Shard-Console's
# VPN note) and 159.26.100.174 (Dallas, 2026-10-26, sl-ddns's own log). Learned exits are added at run time.
BLOCK = csv('BLOCK', '146.70.217.106/32,159.26.100.174/32')
LEARN_AFTER = int(os.environ.get('LEARN_AFTER', '900'))
GAP = int(os.environ.get('GAP', '900'))
EGRESS_CACHE = int(os.environ.get('EGRESS_CACHE', '120'))
TIMEOUT = float(os.environ.get('TIMEOUT', '8'))
PORT = int(os.environ.get('PORT', '8443'))
TLS_DIR = os.environ.get('TLS_DIR', '/tls')
STATE_FILE = os.environ.get('STATE_FILE', '/data/gate-state.json')

# Any dotted quad, masked out of a refusal's text so ddns-updater can never read one as the address to publish.
IPV4_TEXT = re.compile(r'\d{1,3}(?:\.\d{1,3}){3}')
lock = threading.Lock()
last = {'decision': None}
egress_cache = {'at': 0.0, 'ip': None, 'why': None}


def log(msg):
    print(time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()) + ' ' + msg, flush=True)


def as_ipv4(text):
    try:
        ip = ipaddress.ip_address(text.strip())
    except ValueError:
        return None
    if ip.version != 4 or not ip.is_global:
        return None
    return str(ip)


def fetch(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'sl-ddns-gate (cc-P668)'})
    with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
        return r.read(64).decode('ascii', 'replace').strip()


def load_state():
    try:
        with open(STATE_FILE) as f:
            s = json.load(f)
        return {'vpnExits': dict(s.get('vpnExits', {})), 'differing': dict(s.get('differing', {}))}
    except (OSError, ValueError):
        return {'vpnExits': {}, 'differing': {}}


def save_state(s):
    try:
        tmp = STATE_FILE + '.tmp'
        with open(tmp, 'w') as f:
            json.dump(s, f, indent=1)
        os.replace(tmp, STATE_FILE)
    except OSError as e:
        log('could not save ' + STATE_FILE + ': ' + str(e))


def blocked(ip, vpn_exits):
    """Why ip must not be published, or None."""
    addr = ipaddress.ip_address(ip)
    for cidr in BLOCK:
        try:
            if addr in ipaddress.ip_network(cidr, strict=False):
                return ip + ' is in BLOCK ' + cidr + ' (a known VPN exit)'
        except ValueError:
            continue
    if ip in vpn_exits:
        return ip + ' was the game PC\'s VPN egress (seen ' + vpn_exits[ip] + ')'
    return None


def haven_ip():
    """(ip, source, None) or (None, None, why). Every Haven URL that answers must agree."""
    got, errors = {}, []
    for url in HAVEN_URLS:
        try:
            raw = fetch(url)
            ip = as_ipv4(raw)
            if ip:
                got[url] = ip
            else:
                errors.append(url + ' answered ' + repr(raw[:40]))
        except urllib.error.HTTPError as e:
            # sl-homeip says why it has no address (lookups disagree, too old); keep its words in the refusal.
            errors.append(url + ': ' + str(e.code) + ' ' + e.read(300).decode('ascii', 'replace').strip())
        except Exception as e:
            errors.append(url + ': ' + str(e)[:100])
    if not got:
        return None, None, 'Haven did not answer (' + '; '.join(errors) + ')'
    if len(set(got.values())) > 1:
        return None, None, 'Haven\'s URLs disagree: ' + ', '.join(u + '=' + i for u, i in got.items())
    url = next(iter(got))
    return got[url], url, None


def tower_egress():
    """(ip, None) when every egress lookup agrees, else (None, why). Cached EGRESS_CACHE seconds."""
    now = time.time()
    if now - egress_cache['at'] < EGRESS_CACHE:
        return egress_cache['ip'], egress_cache['why']
    seen, errors = set(), []
    for url in EGRESS_URLS:
        try:
            ip = as_ipv4(fetch(url))
            if ip:
                seen.add(ip)
            else:
                errors.append(url + ': not an IPv4 address')
        except Exception as e:
            errors.append(url + ': ' + str(e)[:100])
    if len(seen) == 1 and not errors:
        ip, why = seen.pop(), None
    else:
        ip, why = None, 'egress unknown (' + '; '.join(errors + sorted(seen)) + ')'
    egress_cache.update(at=now, ip=ip, why=why)
    return ip, why


def decide(now=None):
    """One decision: {'ok': bool, 'ip': str|None, 'why': str, ...}. Updates the learned VPN exits."""
    now = time.time() if now is None else now
    stamp = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime(now))
    s = load_state()
    ip, source, why = haven_ip()
    egress, egress_why = tower_egress()
    d = {'at': stamp, 'haven': ip, 'havenSource': source, 'egress': egress, 'ok': False, 'ip': None}
    if egress and ip and egress != ip:
        # Only an unbroken run counts: a different egress, or a gap of more than GAP seconds, starts it again. Haven
        # catches up with a real change of the home address within minutes, far inside LEARN_AFTER.
        run = s['differing'].get(egress)
        first = run[0] if run and now - run[1] <= GAP else now
        s['differing'] = {egress: [first, now]}
        if now - first >= LEARN_AFTER and egress not in s['vpnExits']:
            s['vpnExits'][egress] = stamp
            log('learned VPN exit ' + egress + ': the game PC has gone out there, not through the house (' + ip +
                '), for ' + str(int(now - first)) + ' s')
    elif egress and ip:
        s['differing'] = {}
    save_state(s)
    if not ip:
        d['why'] = why
    else:
        b = blocked(ip, s['vpnExits'])
        if b:
            d['why'] = 'Haven reports ' + b
        else:
            d.update(ok=True, ip=ip, why='Haven\'s address (' + source + ')' +
                     ('' if not egress or egress == ip else '; the game PC goes out as ' + egress + ', not used'))
    if not egress:
        d['egressNote'] = egress_why
    return d


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
            with lock:
                d = decide()
                last['decision'] = d
            if d['ok']:
                log('serving ' + d['ip'] + ': ' + d['why'])
                self.reply(200, d['ip'] + '\n')
            else:
                log('*** REFUSED *** no address for sl-ddns, the record is left alone: ' + d['why'])
                self.reply(403, 'REFUSED: ' + IPV4_TEXT.sub('<address>', d['why']) + '\n')
        elif path == '/status':
            with lock:
                body = json.dumps({'last': last['decision'], 'state': load_state(), 'block': BLOCK,
                                   'havenUrls': HAVEN_URLS, 'egressUrls': EGRESS_URLS}, indent=1)
            self.reply(200, body + '\n', 'application/json')
        elif path == '/health':
            self.reply(200, 'ok\n')
        else:
            self.reply(404, 'not found\n')

    def log_message(self, fmt, *args):
        pass


def reach():
    """--reach: from inside the gate's container, which way reaches Haven, and how this PC goes out. Changes nothing."""
    for url in HAVEN_URLS:
        t = time.time()
        try:
            print('haven  ' + url + ' -> ' + fetch(url) + ' (%.2f s)' % (time.time() - t))
        except Exception as e:
            print('haven  ' + url + ' -> FAILED: ' + str(e)[:120])
    for url in EGRESS_URLS:
        try:
            print('egress ' + url + ' -> ' + fetch(url))
        except Exception as e:
            print('egress ' + url + ' -> FAILED: ' + str(e)[:120])
    ip, source, why = haven_ip()
    print('Haven\'s address: ' + (ip + ' via ' + source if ip else 'NONE, ' + why))
    return 0 if ip else 1


if __name__ == '__main__':
    if '--reach' in sys.argv:
        sys.exit(reach())
    cert, key = os.path.join(TLS_DIR, 'gate.crt'), os.path.join(TLS_DIR, 'gate.key')
    if not (os.path.exists(cert) and os.path.exists(key)):
        log('REFUSED: no certificate in ' + TLS_DIR + ' (entrypoint.sh makes it)')
        sys.exit(2)
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    ctx.load_cert_chain(cert, key)
    srv = ThreadingHTTPServer(('0.0.0.0', PORT), Handler)
    srv.socket = ctx.wrap_socket(srv.socket, server_side=True)
    log('sl-ddns-gate on :' + str(PORT) + '; Haven ' + ', '.join(HAVEN_URLS) + '; egress ' + ', '.join(EGRESS_URLS) +
        '; block ' + ', '.join(BLOCK))
    srv.serve_forever()
