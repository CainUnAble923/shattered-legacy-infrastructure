/* cc-P88: the shared Shattered Legacy menu (Play tests, Shard status, Dashboard) at the top of Homepage. The menu
   itself is docker/test-queue/html/nav.js, served by sl-play-tests at /nav.js; this file only loads it. Homepage
   serves this file from /app/config on every request, so a browser reload picks up a change; no restart.
   cc-P110: on Haven's Homepage the menu comes from the ops service on Haven (cc-P107); the game PC's copy is only the
   fallback, for before the ops cutover or after its rollback. If neither answers, Homepage shows with no menu.
   ASCII only. */
(function () {
  var NAV_AT = [{ host: '192.168.1.61', port: 8093 }, { host: '192.168.1.58', port: 8093 }];
  function load(i) {
    if (i >= NAV_AT.length) return;
    var s = document.createElement('script');
    s.src = 'http://' + NAV_AT[i].host + ':' + NAV_AT[i].port + '/nav.js';
    s.async = true;
    s.setAttribute('data-sl-page', 'dashboard');
    s.onerror = function () { s.parentNode && s.parentNode.removeChild(s); load(i + 1); };
    document.head.appendChild(s);
  }
  load(0);
})();
