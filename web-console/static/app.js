/* cc-P155: the web console page. Polls /api/status every 5 s (a slow answer never stacks: one request at a time, each
   with a 10 s timeout) and draws every panel from it. Text is only ever set with textContent: nothing from the
   status reaches the page as HTML. Read only: there is nothing here that changes anything. ASCII only. */
(function () {
  'use strict';
  var POLL_MS = 5000;
  var last = null, busy = false, art = [];

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text !== undefined && text !== null) e.textContent = String(text);
    return e;
  }
  function $(id) { return document.getElementById(id); }
  function clear(n) { while (n.firstChild) n.removeChild(n.firstChild); return n; }
  function when(iso) {
    if (!iso) return '';
    var d = new Date(iso);
    if (isNaN(d)) return iso;
    var s = (Date.now() - d.getTime()) / 1000, a;
    if (s < 0) { s = -s; a = s < 3600 ? Math.round(s / 60) + 'm' : Math.round(s / 3600) + 'h'; return 'in ' + a; }
    if (s < 90) return Math.round(s) + 's ago';
    if (s < 5400) return Math.round(s / 60) + 'm ago';
    if (s < 172800) return Math.round(s / 3600) + 'h ago';
    return d.toLocaleDateString();
  }
  function color(c) { c = String(c || 'grey').toLowerCase(); return c === 'gray' ? 'grey' : c; }
  function hasArt(name) { return art.indexOf(name) >= 0; }

  // --- tabs -------------------------------------------------------------------------------------------------------
  function showTab() {
    var key = (location.hash || '#today').slice(1);
    if (!$(key) || !$(key).classList.contains('tab')) key = 'today';
    var tabs = document.querySelectorAll('.tab');
    for (var i = 0; i < tabs.length; i++) tabs[i].classList.toggle('on', tabs[i].id === key);
    var links = document.querySelectorAll('#tabs a');
    for (var j = 0; j < links.length; j++) links[j].classList.toggle('on', links[j].getAttribute('data-tab') === key);
    if (key === 'logs' && !$('log-lines').textContent) loadLog();
  }

  // --- panels -----------------------------------------------------------------------------------------------------
  function drawTiles(tiles) {
    var box = clear($('tiles'));
    (tiles || []).forEach(function (t) {
      if (!t) return;
      var c = color(t.color);
      var d = el('div', 'tile ' + c);
      d.setAttribute('data-key', t.key || '');
      var slot = 'crystal-' + c + '.png';
      if (hasArt(slot)) { var img = el('img', 'crystal'); img.src = '/art/' + slot; img.alt = ''; d.appendChild(img); }
      else d.appendChild(el('span', 'crystal'));
      var txt = el('div');
      txt.appendChild(el('div', 'word', (t.title || '') + ': ' + (t.word || '')));
      txt.appendChild(el('div', 'detail', t.detail || ''));
      d.appendChild(txt);
      box.appendChild(d);
    });
  }

  function drawLights(lights) {
    var box = clear($('lights'));
    [['Rebuild (test shard)', (lights || {}).rebuild], ['Player package', (lights || {}).package]].forEach(function (p) {
      var l = p[1];
      var d = el('div', 'light ' + color(l && l.color));
      var b = el('b', null, p[0] + ': ');
      d.appendChild(b);
      d.appendChild(document.createTextNode(l ? (l.word + ', ' + l.detail) : 'not judged yet'));
      box.appendChild(d);
    });
  }

  function promptLine(p, extra) {
    var li = el('li');
    li.appendChild(el('div', null, 'P' + p.number + (p.cont ? ' (continuation)' : '') + ': ' + (p.summary || p.title || p.name)));
    var meta = [p.model, p.size, extra].filter(function (x) { return x; }).join(' | ');
    if (meta) li.appendChild(el('div', 'meta', meta));
    if (!p.summary && p.para) li.appendChild(el('div', 'quiet', p.para));
    return li;
  }

  function golemState(g) {
    var lines = [];
    if (!g) return ['The Golem has not been read yet.'];
    var s = g.state + (g.kind && g.kind !== 'golem' ? ' (' + g.kind + ')' : '');
    if (typeof g.reason === 'string' && g.reason) s += ': ' + g.reason;
    lines.push(s);
    if (g.readyAt) lines.push('Ready ' + when(g.readyAt) + (g.readyHow ? ' (' + g.readyHow + ')' : ''));
    if (g.winding) lines.push('Winding down: ' + g.winding);
    if (g.stoppedPrompt) lines.push('Stopped prompt: ' + g.stoppedPrompt);
    var ns = g.nightShift || {};
    lines.push('Night shift: ' + (ns.enabled ? 'on, ' + ns.start + ' to ' + ns.end : 'off') + '. Self-wake: ' + (g.autoWake ? 'on' : 'off') + '.');
    if (g.alert) lines.push('Alert: ' + g.alert);
    if (g.emailFailing) lines.push('Email is failing (see the Golem log).');
    return lines;
  }

  function drawGolem(g) {
    var st = clear($('golem-state'));
    golemState(g).forEach(function (l) { st.appendChild(el('p', null, l)); });
    var cur = clear($('golem-current'));
    if (g && g.current) {
      var c = g.current;
      cur.appendChild(el('p', null, 'P' + c.number + ': ' + (c.summary || c.title)));
      var bits = [c.model, c.size];
      if (c.stepsTotal) bits.push('step ' + c.stepsDone + ' of ' + c.stepsTotal + (c.stepNow ? ': ' + c.stepNow : ''));
      if (c.startedAt) bits.push('started ' + when(c.startedAt));
      if (c.lastActivity) bits.push('last activity ' + when(c.lastActivity));
      cur.appendChild(el('p', 'meta', bits.filter(function (x) { return x; }).join(' | ')));
    } else cur.appendChild(el('p', 'quiet', 'Nothing running.'));
    var q = clear($('golem-queue'));
    ((g && g.queue) || []).forEach(function (p, i) { q.appendChild(promptLine(p, i === 0 ? 'next' : '')); });
    if (!q.firstChild) q.appendChild(el('li', 'quiet', 'The queue is empty.'));
    var f = clear($('golem-finished'));
    ((g && g.finished) || []).forEach(function (x) {
      var li = el('li');
      li.appendChild(el('div', null, 'P' + x.number + ' ' + x.result + ': ' + x.title));
      if (x.headline) li.appendChild(el('div', 'quiet', x.headline));
      li.appendChild(el('div', 'meta', when(x.time)));
      f.appendChild(li);
    });
    var u = clear($('golem-usage'));
    ((g && g.usage) || []).forEach(function (r) {
      u.appendChild(el('div', null, r.name + (r.pct === null || r.pct === undefined ? ': unknown' : ': ' + Math.round(r.pct) + '% used')));
      var bar = el('div', 'bar ' + (r.color === 'red' ? 'red' : r.color === 'amber' ? 'amber' : ''));
      var fill = el('span'); fill.style.width = (r.pct ? Math.max(0, Math.min(100, r.pct)) : 0) + '%';
      bar.appendChild(fill); u.appendChild(bar);
    });
    // Today's summary card.
    var t = clear(document.querySelector('#today-golem .body'));
    golemState(g).slice(0, 2).forEach(function (l) { t.appendChild(el('p', null, l)); });
    if (g && g.current) t.appendChild(el('p', null, 'Running P' + g.current.number + ': ' + (g.current.summary || g.current.title)));
    if (g && g.queue && g.queue.length) t.appendChild(el('p', 'meta', g.queue.length + ' queued, next P' + g.queue[0].number + ': ' + (g.queue[0].summary || g.queue[0].title)));
  }

  function drawNeeds(items, built) {
    var list = clear($('needs-list')), today = clear(document.querySelector('#today-needs .body'));
    (items || []).forEach(function (n, i) {
      var li = el('li');
      li.appendChild(el('span', 'kind', n.kind));
      li.appendChild(document.createTextNode(n.text));
      if (n.where) li.appendChild(el('div', 'meta', n.where));
      list.appendChild(li);
      if (i < 6) { var t = el('li'); t.appendChild(el('span', 'kind', n.kind)); t.appendChild(document.createTextNode(n.text)); today.appendChild(t); }
    });
    if (!list.firstChild) list.appendChild(el('li', 'quiet', 'Nothing waits on you.'));
    if (!today.firstChild) today.appendChild(el('li', 'quiet', 'Nothing waits on you.'));
    $('needs-count').textContent = (items || []).length ? String(items.length) : '';
    $('needs-note').textContent = built ? '' : 'Decisions to answer arrive with P146 (the play-test decisions inbox), not built yet.';
  }

  function drawTimeline(ev) {
    var list = clear($('timeline-list')), today = clear(document.querySelector('#today-timeline .body'));
    (ev || []).forEach(function (e, i) {
      var li = el('li');
      li.appendChild(el('span', 'kind', e.kind));
      li.appendChild(document.createTextNode(e.text + (e.repo ? ' (' + e.repo + ')' : '')));
      li.appendChild(el('div', 'meta', when(e.time) + (e.who ? ' | ' + e.who : '') + (e.headline ? ' | ' + e.headline : '')));
      list.appendChild(li);
      if (i < 6) { var t = el('li'); t.appendChild(el('span', 'kind', e.kind)); t.appendChild(document.createTextNode(e.text)); t.appendChild(el('div', 'meta', when(e.time))); today.appendChild(t); }
    });
  }

  function drawLinks(ls) {
    var list = clear($('links-list'));
    (ls || []).forEach(function (l) {
      var li = el('li');
      li.appendChild(el('span', 'dot' + (l.up === true ? ' up' : l.up === false ? ' down' : '')));
      var a = el('a', null, l.name); a.href = l.url; a.rel = 'noopener noreferrer'; a.target = '_blank';
      li.appendChild(a);
      if (l.why) li.appendChild(el('span', 'meta', '  ' + l.why));
      list.appendChild(li);
    });
  }

  function draw(j) {
    last = j;
    art = j.art || [];
    document.body.classList.toggle('art-stone', hasArt('stone.png'));
    var s = j.status || {};
    var f = $('fresh');
    if (!j.status) { f.textContent = 'No status yet: the collector has not written it.'; f.classList.add('stale'); }
    else {
      f.textContent = 'Status ' + (j.ageSeconds === null ? '' : (j.ageSeconds < 90 ? j.ageSeconds + 's' : Math.round(j.ageSeconds / 60) + 'm') + ' old') + ', from the ' + (s.generatedBy || '?');
      f.classList.toggle('stale', j.ageSeconds === null || j.ageSeconds > 120);
    }
    drawTiles(s.tiles);
    drawLights(s.lights);
    drawGolem(s.golem);
    var p = j.panels || {};
    drawNeeds(p.needsYou, p.decisionsBuilt);
    drawTimeline(p.timeline);
    drawLinks(p.links);
    // For the phone-size check (tests/test_web_console.py): drawn, and whether anything is wider than the screen.
    document.documentElement.setAttribute('data-drawn', String((document.documentElement.getAttribute('data-drawn') | 0) + 1));
    document.documentElement.setAttribute('data-overflow', document.documentElement.scrollWidth > window.innerWidth + 1 ? 'yes' : 'no');
    document.documentElement.setAttribute('data-viewport', String(window.innerWidth));
    var sel = $('log-source');
    if (!sel.options.length && j.logSources) {
      Object.keys(j.logSources).forEach(function (k) { var o = el('option', null, j.logSources[k]); o.value = k; sel.appendChild(o); });
    }
  }

  function get(url, cb) {
    var x = new XMLHttpRequest();
    x.open('GET', url, true);
    x.timeout = 10000;
    x.onload = function () { if (x.status === 200) { try { cb(null, JSON.parse(x.responseText)); } catch (e) { cb(e); } } else cb(new Error(x.status + ' ' + x.responseText)); };
    x.onerror = x.ontimeout = function () { cb(new Error('no answer')); };
    x.send();
  }

  function poll(refresh) {
    if (busy) return;
    busy = true;
    get('/api/status' + (refresh ? '?refresh=1' : ''), function (err, j) {
      busy = false;
      if (err) { $('fresh').textContent = 'The web console did not answer: ' + err.message; $('fresh').classList.add('stale'); return; }
      draw(j);
    });
  }

  function loadLog(ev) {
    if (ev) ev.preventDefault();
    var src = $('log-source').value || 'golem';
    var url = '/api/logs?source=' + encodeURIComponent(src) + '&n=' + encodeURIComponent($('log-n').value) + '&q=' + encodeURIComponent($('log-q').value);
    $('log-what').textContent = 'reading...';
    get(url, function (err, j) {
      if (err) { $('log-what').textContent = 'could not read it: ' + err.message; return; }
      $('log-what').textContent = (j.what || j.source) + ': ' + j.lines.length + ' lines' + ($('log-q').value ? ' matching' : '');
      $('log-lines').textContent = j.lines.join('\n');
      var pre = $('log-lines'); pre.scrollTop = pre.scrollHeight;
    });
  }

  window.addEventListener('hashchange', showTab);
  document.addEventListener('DOMContentLoaded', function () {
    showTab();
    $('log-form').addEventListener('submit', loadLog);
    $('refresh').addEventListener('click', function (e) { e.preventDefault(); poll(true); });
    poll(false);
    setInterval(function () { poll(false); }, POLL_MS);
  });
  window.UOConsole = { draw: draw, golemState: golemState, last: function () { return last; } };
})();
