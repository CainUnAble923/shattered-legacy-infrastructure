/* Shattered Legacy shared menu (cc-P88). The ONE source of the menu bar on the three home-network pages:

     Play tests     sl-play-tests  http://<host>:8093/        loads it as <script src="/nav.js" data-sl-page="playtests">
     Shard status   sl-play-tests  http://<host>:8093/status  dashboard.html: tools/render_dashboard.py copies this
                                                              whole file into its DATA:NAV block on every run, so the
                                                              menu also works when the file is opened as file:///
     Dashboard      Homepage       http://<host>:3000/        docker/dashboard/config/custom.js loads it from :8093

   cc-P105 (Haven step H1): a row may give a whole url instead of host, port and path (the Haven pages behind
   Cloudflare Access will: https://ops.shatteredlegacyuo.com/...), and lanOnly: true shows a row only when this page
   was reached from a home-network or tailnet address (Homepage is never published outside). With the rows below,
   nothing changes: every page is still host and port, and they are all reached on the LAN.
   A fourth page is one more row in PAGES. Moving a page (to Haven, say) is that page's row: set its host, and its
   port and path if they change. host null means "the host the browser used to reach this page", so the links work
   from a phone on the LAN, never only from this PC. A page with no host at all (file:///) uses FALLBACK_HOST.
   Plain script, no build step, no dependencies. ASCII only. */
(function () {
  'use strict';

  var PAGES = [
    { key: 'playtests', label: 'Play tests',   host: null, port: 8093, path: '/' },
    { key: 'status',    label: 'Shard status', host: null, port: 8093, path: '/status' },
    { key: 'dashboard', label: 'Dashboard',    host: null, port: 3000, path: '/', lanOnly: true }
  ];
  // The Windows box (docker/test-queue/docker-compose.yml), for a page opened from disk.
  var FALLBACK_HOST = '192.168.1.58';

  var CSS =
    '#sl-menu{display:flex;flex-wrap:wrap;align-items:center;gap:4px 6px;margin:0;padding:6px 12px;' +
    'background:#1b2530;border-bottom:3px solid #d9a93a;font:600 15px/1.2 system-ui,-apple-system,"Segoe UI",sans-serif;' +
    'position:relative;z-index:1000;box-sizing:border-box;width:100%}' +
    '#sl-menu .sl-brand{color:#d9a93a;font-weight:700;margin-right:8px;white-space:nowrap}' +
    '#sl-menu a{display:inline-flex;align-items:center;min-height:44px;padding:0 14px;border-radius:6px;' +
    'color:#f1f4f7;text-decoration:none;white-space:nowrap}' +
    '#sl-menu a:hover,#sl-menu a:focus-visible{background:#2d3b4a;outline:none}' +
    '#sl-menu a:focus-visible{box-shadow:0 0 0 2px #d9a93a}' +
    '#sl-menu a[aria-current="page"]{background:#d9a93a;color:#1b2530}' +
    '#sl-menu .sl-badge{margin-left:6px;min-width:1.4em;padding:1px 6px;border-radius:999px;background:#d9a93a;' +
    'color:#1b2530;font-size:12px;text-align:center}' +
    '#sl-menu a[aria-current="page"] .sl-badge{background:#1b2530;color:#d9a93a}';

  function here() {
    var l = window.location;
    var port = l.port || (l.protocol === 'https:' ? '443' : l.protocol === 'http:' ? '80' : '');
    var path = l.pathname.replace(/\/index\.html$/, '/').replace(/(.)\/$/, '$1');
    return { port: port, path: path || '/' };
  }

  // cc-P105: a home-network address: private IPv4, loopback, Tailscale's 100.64.0.0/10, a .home.arpa or .ts.net name,
  // a bare host name, or no host at all (the file opened from disk).
  function onLan(host) {
    var h = String(host == null ? window.location.hostname : host).toLowerCase();
    if (!h || h === 'localhost' || h.indexOf('.') < 0) return true;
    if (/\.(home\.arpa|ts\.net|local)$/.test(h)) return true;
    var m = /^(\d+)\.(\d+)\.\d+\.\d+$/.exec(h);
    if (!m) return false;
    var a = +m[1], b = +m[2];
    return a === 10 || a === 127 || (a === 192 && b === 168) || (a === 172 && b >= 16 && b <= 31) ||
      (a === 100 && b >= 64 && b <= 127);
  }

  function url(p) {
    if (p.url) return p.url;
    var host = p.host || window.location.hostname || FALLBACK_HOST;
    var proto = (!p.host && window.location.protocol === 'https:') ? 'https:' : 'http:';
    return proto + '//' + host + ':' + p.port + p.path;
  }

  // Which page is this: the script tag's data-sl-page, else the page whose port and path match the address.
  function current(tag) {
    var k = tag && tag.getAttribute('data-sl-page');
    if (k) return k;
    var h = here();
    var at = window.location.protocol + '//' + window.location.host + window.location.pathname;
    for (var i = 0; i < PAGES.length; i++) {
      var p = PAGES[i];
      if (p.url) {
        if (p.url.replace(/(.)\/$/, '$1') === at.replace(/\/index\.html$/, '/').replace(/(.)\/$/, '$1')) return p.key;
        continue;
      }
      if (String(p.port) === h.port && (p.path.replace(/(.)\/$/, '$1') === h.path) &&
          (!p.host || p.host === window.location.hostname)) return p.key;
    }
    return '';
  }

  function build(key) {
    var nav = document.createElement('nav');
    nav.id = 'sl-menu';
    nav.setAttribute('aria-label', 'Shattered Legacy pages');
    var brand = document.createElement('span');
    brand.className = 'sl-brand';
    brand.textContent = 'Shattered Legacy';
    nav.appendChild(brand);
    for (var i = 0; i < PAGES.length; i++) {
      if (PAGES[i].lanOnly && !onLan()) continue;
      var a = document.createElement('a');
      a.href = url(PAGES[i]);
      a.textContent = PAGES[i].label;
      a.setAttribute('data-sl-page', PAGES[i].key);
      if (PAGES[i].key === key) a.setAttribute('aria-current', 'page');
      nav.appendChild(a);
    }
    return nav;
  }

  function mount(key) {
    if (!document.getElementById('sl-menu-style')) {
      var st = document.createElement('style');
      st.id = 'sl-menu-style';
      st.appendChild(document.createTextNode(CSS));
      (document.head || document.documentElement).appendChild(st);
    }
    var old = document.getElementById('sl-menu');
    var nav = build(key);
    // Edge to edge whatever margin the page gives its body (the status page keeps the browser's 8 px).
    var cs = window.getComputedStyle(document.body);
    nav.style.margin = '-' + cs.marginTop + ' -' + cs.marginRight + ' ' + cs.marginTop + ' -' + cs.marginLeft;
    nav.style.width = 'calc(100% + ' + cs.marginLeft + ' + ' + cs.marginRight + ')';
    if (old) old.parentNode.replaceChild(nav, old);
    else document.body.insertBefore(nav, document.body.firstChild);
  }

  // cc-P90: a small count beside a page's label (new feature ideas beside Play tests). A page sets its own; n 0 clears.
  function setBadge(pageKey, n, title) {
    var a = document.querySelector('#sl-menu a[data-sl-page="' + pageKey + '"]');
    if (!a) return;
    var b = a.querySelector('.sl-badge');
    if (!n) { if (b) a.removeChild(b); return; }
    if (!b) { b = document.createElement('span'); b.className = 'sl-badge'; a.appendChild(b); }
    b.textContent = String(n);
    b.title = title || '';
    a.setAttribute('aria-label', a.firstChild.nodeValue + ' (' + (title || n) + ')');
  }

  // Another page served by the same service as Play tests (Shard status at /status) asks it for the count. A page
  // on another origin (Homepage) does not: it would need CORS on the API.
  function fetchBadge(key) {
    var pt = PAGES[0];
    for (var i = 0; i < PAGES.length; i++) if (PAGES[i].key === 'playtests') pt = PAGES[i];
    if (key === 'playtests' || !window.fetch || url(pt).indexOf(window.location.protocol + '//' + window.location.host + '/') !== 0) return;
    fetch('/api/summary', { cache: 'no-store' }).then(function (r) { return r.ok ? r.json() : null; }).then(function (j) {
      // cc-P97: new ideas and new bug reports, together.
      var ni = (j && j.ideas_new) || 0, nb = (j && j.bugs_new) || 0, parts = [];
      if (ni) parts.push(ni + ' new idea' + (ni === 1 ? '' : 's'));
      if (nb) parts.push(nb + ' new bug' + (nb === 1 ? '' : 's'));
      if (ni + nb) setBadge('playtests', ni + nb, parts.join(', '));
    }).catch(function () {});
  }

  var tag = document.currentScript;
  var key = current(tag);
  window.SLMenu = { pages: PAGES, url: url, onLan: onLan, fallbackHost: FALLBACK_HOST, current: key, setBadge: setBadge };
  function start() { mount(key); fetchBadge(key); }
  if (document.body) start();
  else document.addEventListener('DOMContentLoaded', start);
})();
