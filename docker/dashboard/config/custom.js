/* cc-P88: the shared Shattered Legacy menu (Play tests, Shard status, Dashboard) at the top of Homepage.
   The menu itself, its labels, addresses and CSS, is docker/test-queue/html/nav.js, served by sl-play-tests at
   /nav.js; this file only loads it. Homepage serves this file from /app/config on every request
   (/api/config/custom.js), so a browser reload picks up a change; no restart.
   If sl-play-tests is down or has not been restarted with the /nav.js route, the script fails to load and
   Homepage shows as before, with no menu. If the play-test service moves (to Haven, say), change NAV_AT here
   as well as its rows in nav.js. ASCII only. */
(function () {
  var NAV_AT = { host: null, port: 8093 };   // host null: the host the browser used to reach Homepage
  var host = NAV_AT.host || window.location.hostname || '192.168.1.58';
  var s = document.createElement('script');
  s.src = 'http://' + host + ':' + NAV_AT.port + '/nav.js';
  s.async = true;
  s.setAttribute('data-sl-page', 'dashboard');
  document.head.appendChild(s);
})();
