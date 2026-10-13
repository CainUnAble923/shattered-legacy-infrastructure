/* Shattered Legacy shared menu (cc-P88). The ONE source of the menu bar on the three home-network pages:

     Play tests     sl-play-tests  http://<host>:8093/        loads it as <script src="/nav.js" data-sl-page="playtests">
     Shard status   sl-play-tests  http://<host>:8093/status  dashboard.html: tools/render_dashboard.py copies this
                                                              whole file into its DATA:NAV block on every run, so the
                                                              menu also works when the file is opened as file:///
     Dashboard      Homepage       http://<host>:3000/        docker/dashboard/config/custom.js loads it from :8093

   A fourth page is one more row in PAGES. Moving a page (to Haven, say) is that page's row: set its host, and its
   port and path if they change. host null means "the host the browser used to reach this page", so the links work
   from a phone on the LAN and never point at localhost. A page with no host at all (file:///) uses FALLBACK_HOST.
   Plain script, no build step, no dependencies. ASCII only. */
(function () {
  'use strict';

  var PAGES = [
    { key: 'playtests', label: 'Play tests',   host: null, port: 8093, path: '/' },
    { key: 'status',    label: 'Shard status', host: null, port: 8093, path: '/status' },
    { key: 'dashboard', label: 'Dashboard',    host: null, port: 3000, path: '/' }
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
    '#sl-menu a[aria-current="page"]{background:#d9a93a;color:#1b2530}';

  function here() {
    var l = window.location;
    var port = l.port || (l.protocol === 'https:' ? '443' : l.protocol === 'http:' ? '80' : '');
    var path = l.pathname.replace(/\/index\.html$/, '/').replace(/(.)\/$/, '$1');
    return { port: port, path: path || '/' };
  }

  function url(p) {
    var host = p.host || window.location.hostname || FALLBACK_HOST;
    var proto = (!p.host && window.location.protocol === 'https:') ? 'https:' : 'http:';
    return proto + '//' + host + ':' + p.port + p.path;
  }

  // Which page is this: the script tag's data-sl-page, else the page whose port and path match the address.
  function current(tag) {
    var k = tag && tag.getAttribute('data-sl-page');
    if (k) return k;
    var h = here();
    for (var i = 0; i < PAGES.length; i++) {
      var p = PAGES[i];
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

  var tag = document.currentScript;
  var key = current(tag);
  window.SLMenu = { pages: PAGES, url: url, fallbackHost: FALLBACK_HOST, current: key };
  if (document.body) mount(key);
  else document.addEventListener('DOMContentLoaded', function () { mount(key); });
})();
