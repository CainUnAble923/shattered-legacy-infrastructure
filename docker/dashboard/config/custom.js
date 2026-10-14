/* cc-P88: the shared Shattered Legacy menu (Play tests, Shard status, Dashboard) at the top of Homepage.
   The menu itself, its labels, addresses and CSS, is docker/test-queue/html/nav.js, served by sl-play-tests at
   /nav.js; this file only loads it. Homepage serves this file from /app/config on every request
   (/api/config/custom.js), so a browser reload picks up a change; no restart.
   If sl-play-tests is down or has not been restarted with the /nav.js route, the script fails to load and
   Homepage shows as before, with no menu. cc-P107: NAV_AT tries Haven first, then this PC. ASCII only. */
(function () {
  // cc-P107: the menu is loaded from Haven's sl-play-tests (after the cutover), else from the game PC's (before it, or
  // after a rollback): the first that answers. host null is the host the browser used to reach Homepage.
  var NAV_AT = [{ host: '192.168.1.61', port: 8093 }, { host: null, port: 8093 }];
  function load(i) {
    if (i >= NAV_AT.length) return;
    var host = NAV_AT[i].host || window.location.hostname || '192.168.1.58';
    var s = document.createElement('script');
    s.src = 'http://' + host + ':' + NAV_AT[i].port + '/nav.js';
    s.async = true;
    s.setAttribute('data-sl-page', 'dashboard');
    s.onerror = function () { s.parentNode && s.parentNode.removeChild(s); load(i + 1); };
    document.head.appendChild(s);
  }
  load(0);
})();
