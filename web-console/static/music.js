// cc-P367: the Music Room. Lists every track from /api/music, plays one at a time in the footer's player (the server
// streams it with Range requests, so the seek bar works), and saves Chase's rating, tags, target and note for a track
// as he types (debounced) with POST /api/music/note. Save to repo commits music-notes.json (POST /api/music/commit).
// cc-P456: a track whose save failed stays unsaved until a save of it succeeds (see saving, below).
// Every value from the server is set as text, never as HTML.
(function () {
  'use strict';
  var SAVE_MS = 800;
  var data = null, notes = {}, rows = {}, order = [], current = '', timers = {}, inflight = {}, again = {}, failed = {};
  var PLACEHOLDER = 'where it fits, what it feels like, world-building notes';

  function $(id) { return document.getElementById(id); }
  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text !== undefined && text !== null) e.textContent = String(text);
    return e;
  }
  function clear(e) { while (e.firstChild) e.removeChild(e.firstChild); return e; }
  function mmss(s) {
    if (s === null || s === undefined) return '?:??';
    s = Math.round(s);
    return Math.floor(s / 60) + ':' + ('0' + (s % 60)).slice(-2);
  }
  function packLabel(id) {
    for (var i = 0; i < data.packs.length; i++) if (data.packs[i].id === id) return data.packs[i].label;
    return id;
  }
  function fileUrl(t) { return '/music-file/' + t.pack + '/' + encodeURIComponent(t.file); }
  function stockTrack(name) {
    var want = String(name).toLowerCase();
    for (var i = 0; i < data.tracks.length; i++) {
      var t = data.tracks[i];
      if (t.pack === 'stock' && t.name.toLowerCase() === want) return t;
    }
    return null;
  }
  function post(path, body) {
    return fetch(path, { method: 'POST', credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-UOConsole-Action': '1' }, body: JSON.stringify(body) })
      .then(function (r) { return r.json().then(function (j) { if (!r.ok) throw new Error(j.error || ('HTTP ' + r.status)); return j; }); });
  }
  function note(key) { return notes[key] || { rating: '', tags: [], note: '', target: '', rev: 0 }; }
  function has(o, k) { return Object.prototype.hasOwnProperty.call(o, k); }
  function keys(o) { var a = []; for (var k in o) if (has(o, k)) a.push(k); return a; }

  // --- one track ----------------------------------------------------------------------------------------------
  function origin(t) {
    if (t.remixKind === 'stock') return 'The stock client track';
    if (t.remixKind === 'replaces') return 'Same name as the stock track ' + t.remixOf;
    if (t.remixKind === 'midi') return 'Remix of the stock MIDI ' + t.remixOf + ' (no digital track of that name)';
    return 'New: no stock track of this name';
  }
  function buildRow(t) {
    var r = el('article', 'track');
    r.setAttribute('data-key', t.key);
    var top = el('div', 'top');
    var play = el('button', 'play', 'Play');
    play.type = 'button';
    play.addEventListener('click', function () { toggle(t.key); });
    top.appendChild(play);
    top.appendChild(el('span', 'name', t.name));
    top.appendChild(el('span', 'pill', packLabel(t.pack)));
    top.appendChild(el('span', 'pill', mmss(t.seconds)));
    if (t.notUltima) {
      var f = el('span', 'pill flag', 'not tagged Ultima');
      f.title = 'The file\'s own album tag names something other than Ultima: check where it is from before shipping.';
      top.appendChild(f);
    }
    var st = (t.remixKind === 'replaces') ? stockTrack(t.remixOf) : null;
    if (st && t.pack !== 'stock') {
      var cmp = el('button', 'cmp', 'Play the stock one');
      cmp.type = 'button';
      cmp.addEventListener('click', function () { playKey(st.key); });
      top.appendChild(cmp);
    }
    r.appendChild(top);
    var m1 = el('div', 'meta');
    m1.appendChild(el('span', '', origin(t)));
    if (t.slots && t.slots.length) m1.appendChild(el('span', '', ' - client slot ' + t.slots.join(', ')));
    m1.appendChild(el('span', '', ' - ' + t.file));
    r.appendChild(m1);
    if (t.title || t.artist || t.album) {
      var m2 = el('div', 'meta');
      m2.appendChild(el('span', '', 'File tag: '));
      m2.appendChild(el('b', '', [t.title, t.artist].filter(Boolean).join(' - ') || '(no title)'));
      if (t.album) m2.appendChild(el('span', '', ', from ' + t.album));
      r.appendChild(m2);
    }
    var n = note(t.key);
    var rate = el('div', 'row rate');
    data.ratings.forEach(function (v) {
      var b = el('button', v, v.charAt(0).toUpperCase() + v.slice(1));
      b.type = 'button';
      b.setAttribute('data-rating', v);
      b.addEventListener('click', function () {
        var cur = note(t.key).rating;
        edit(t.key, { rating: cur === v ? '' : v });
      });
      rate.appendChild(b);
    });
    r.appendChild(rate);
    var tags = el('div', 'row tags');
    data.tags.forEach(function (tg) {
      var l = el('label', '');
      var c = document.createElement('input');
      c.type = 'checkbox';
      c.value = tg.id;
      c.addEventListener('change', function () {
        var have = note(t.key).tags.slice(), i = have.indexOf(tg.id);
        if (c.checked && i < 0) have.push(tg.id);
        if (!c.checked && i >= 0) have.splice(i, 1);
        edit(t.key, { tags: have });
      });
      l.appendChild(c);
      l.appendChild(document.createTextNode(tg.label));
      tags.appendChild(l);
    });
    r.appendChild(tags);
    var fields = el('div', 'fields');
    var target = document.createElement('input');
    target.type = 'text';
    target.className = 'target';
    target.maxLength = 120;
    target.setAttribute('list', 'targets');
    target.placeholder = 'Target: an area, monster or boss';
    target.setAttribute('aria-label', 'Target for ' + t.name);
    target.addEventListener('input', function () { edit(t.key, { target: target.value }, true); });
    var ta = document.createElement('textarea');
    ta.className = 'note';
    ta.maxLength = data.noteMax || 4000;
    ta.placeholder = PLACEHOLDER;
    ta.setAttribute('aria-label', 'Note for ' + t.name);
    ta.addEventListener('input', function () { edit(t.key, { note: ta.value }, true); });
    fields.appendChild(target);
    fields.appendChild(ta);
    r.appendChild(fields);
    r.appendChild(el('div', 'saved', ''));
    var retry = el('button', 'retry', 'Retry');
    retry.type = 'button';
    retry.hidden = true;
    retry.addEventListener('click', function () { send(t.key); });
    r.appendChild(retry);
    target.value = n.target || '';
    ta.value = n.note || '';
    paint(t.key, r);
    return r;
  }
  function paint(key, r) {
    r = r || rows[key];
    var n = note(key);
    r.classList.remove('r-use', 'r-maybe', 'r-pass');
    if (n.rating) r.classList.add('r-' + n.rating);
    var bs = r.querySelectorAll('.rate button');
    for (var i = 0; i < bs.length; i++) bs[i].classList.toggle('sel', bs[i].getAttribute('data-rating') === n.rating);
    var cs = r.querySelectorAll('.tags input');
    for (var j = 0; j < cs.length; j++) {
      cs[j].checked = n.tags.indexOf(cs[j].value) >= 0;
      cs[j].parentNode.classList.toggle('sel', cs[j].checked);
    }
  }

  // --- saving -------------------------------------------------------------------------------------------------
  // A change is kept here at once (so filters and Next unrated see it) and sent after SAVE_MS of quiet; a rating or a
  // tag goes at once. Each send carries the whole record for that track and the revision it was made from (the server
  // refuses a stale one). cc-P456: one save per track is in flight at a time, and an edit made meanwhile is sent once it
  // is back, so a late answer can never put an older note over a newer one. A failed save keeps the track in failed:
  // it counts in the status line and on leaving the page until a save of it succeeds (the next edit, Retry, or Save to
  // repo, which stops when one still fails).
  function edit(key, change, typed) {
    var n = note(key), rec = { rating: n.rating, tags: n.tags.slice(), note: n.note, target: n.target, rev: n.rev || 0 };
    for (var k in change) if (has(change, k)) rec[k] = change[k];
    notes[key] = rec;
    paint(key);
    mark(key, 'saving...');
    if (timers[key]) clearTimeout(timers[key]);
    timers[key] = setTimeout(function () { send(key); }, typed ? SAVE_MS : 0);
    status();
  }
  function mark(key, text, err) {
    var s = rows[key].querySelector('.saved');
    s.className = err ? 'saved err' : 'saved';
    s.textContent = text;
    rows[key].querySelector('.retry').hidden = !err;
  }
  // The promise resolves (never rejects) once this save, and any follow-up it owes, is back.
  function send(key) {
    if (timers[key]) { clearTimeout(timers[key]); delete timers[key]; }
    if (inflight[key]) { again[key] = true; return inflight[key]; }
    var rec = notes[key];
    mark(key, 'saving...');
    var p = post('/api/music/note', { key: key, rev: rec.rev || 0, rating: rec.rating, tags: rec.tags, note: rec.note, target: rec.target })
      .then(function (j) {
        delete failed[key];
        data.notesError = '';
        notes[key].rev = j.record.rev;   // notes[key] may be a newer edit by now: it is made from this revision
        if (!timers[key] && !again[key]) mark(key, 'saved ' + new Date().toLocaleTimeString());
      })
      .catch(function (e) { failed[key] = e.message; mark(key, 'not saved: ' + e.message, true); })
      .then(function () {
        delete inflight[key];
        if (again[key]) { delete again[key]; return send(key); }
        status();
      });
    inflight[key] = p;
    status();
    return p;
  }
  // Sends every waiting edit and retries every failed one, waits for every save in flight, and rejects when a track
  // is still not saved.
  function flush() {
    var waits = [], todo = keys(timers).concat(keys(failed), keys(inflight)), seen = {};
    todo.forEach(function (key) {
      if (seen[key]) return;
      seen[key] = true;
      waits.push(timers[key] || !inflight[key] ? send(key) : inflight[key]);
    });
    return Promise.all(waits).then(function () {
      var bad = keys(failed);
      if (bad.length) throw new Error(bad.length + (bad.length === 1 ? ' track' : ' tracks') + ' not saved (' + failed[bad[0]] + ')');
    });
  }
  function saveRepo() {
    var b = $('save-repo');
    b.disabled = true;
    $('commit-line').textContent = 'Saving to the repo...';
    flush().then(function () { return post('/api/music/commit', {}); })
      .then(function (j) { $('commit-line').textContent = (j.ok ? 'Saved to the repo: ' : 'Not committed: ') + j.result; })
      .catch(function (e) { $('commit-line').textContent = 'Not committed: ' + e.message; })
      .then(function () { b.disabled = false; });
  }
  window.addEventListener('beforeunload', function (e) {
    if (keys(timers).length || keys(inflight).length || keys(failed).length) {
      flush().catch(function () {});
      e.preventDefault();
      e.returnValue = '';
    }
  });

  // --- filters ------------------------------------------------------------------------------------------------
  function shown(t) {
    var n = note(t.key), q = $('q').value.trim().toLowerCase();
    var pack = $('f-pack').value, tag = $('f-tag').value, rating = $('f-rating').value;
    if (pack && t.pack !== pack) return false;
    if (tag === '-' && n.tags.length) return false;
    if (tag && tag !== '-' && n.tags.indexOf(tag) < 0) return false;
    if (rating === '-' && n.rating) return false;
    if (rating && rating !== '-' && n.rating !== rating) return false;
    if (q) {
      var hay = [t.name, t.file, t.title, t.artist, t.album, t.remixOf, packLabel(t.pack), n.note, n.target].join(' ').toLowerCase();
      var words = q.split(/\s+/);
      for (var i = 0; i < words.length; i++) if (hay.indexOf(words[i]) < 0) return false;
    }
    return true;
  }
  function filter() {
    var n = 0;
    order.forEach(function (key) {
      var t = byKey(key), vis = shown(t);
      rows[key].hidden = !vis;
      if (vis) n++;
    });
    $('count').textContent = n + ' of ' + order.length + ' tracks shown';
  }
  function byKey(key) { return data.byKey[key]; }

  // --- playing ------------------------------------------------------------------------------------------------
  function playKey(key) {
    var t = byKey(key), a = $('audio');
    if (!t) return;
    if (current && rows[current]) rows[current].classList.remove('playing');
    current = key;
    rows[key].classList.add('playing');
    markButtons();
    $('now').textContent = t.name + ' - ' + packLabel(t.pack) + ' (' + mmss(t.seconds) + ')';
    a.src = fileUrl(t);
    var p = a.play();
    if (p && p.catch) p.catch(function () {});
  }
  function toggle(key) {
    var a = $('audio');
    if (current === key && a.src) {
      if (a.paused) { var p = a.play(); if (p && p.catch) p.catch(function () {}); } else a.pause();
      return;
    }
    playKey(key);
  }
  function markButtons() {
    var a = $('audio');
    order.forEach(function (key) {
      var b = rows[key].querySelector('.play');
      b.textContent = (key === current && !a.paused) ? 'Pause' : 'Play';
    });
  }
  function visibleKeys() { return order.filter(function (k) { return !rows[k].hidden; }); }
  function nextAfter(test) {
    var keys = visibleKeys(), start = keys.indexOf(current);
    for (var i = 1; i <= keys.length; i++) {
      var k = keys[(start + i) % keys.length];
      if (test(k)) return k;
    }
    return '';
  }
  function nextUnrated() {
    var k = nextAfter(function (key) { return !note(key).rating && key !== current; });
    if (!k) { $('count').textContent = 'Every track shown here is rated.'; return; }
    playKey(k);
    rows[k].scrollIntoView({ block: 'center', behavior: 'smooth' });
  }

  // --- summary ------------------------------------------------------------------------------------------------
  function loadSummary() {
    var box = clear($('summary'));
    box.appendChild(el('p', 'muted', 'loading...'));
    flush().catch(function () {}).then(function () { return fetch('/api/music/summary', { credentials: 'same-origin' }); })
      .then(function (r) { return r.json(); })
      .then(function (s) {
        clear(box);
        if (s.notesError) box.appendChild(el('p', 'saved err', s.notesError));
        if (!s.count) { box.appendChild(el('p', 'muted', 'No track is marked Use yet.')); return; }
        box.appendChild(el('p', 'muted', s.count + (s.count === 1 ? ' track' : ' tracks') + ' marked Use.'));
        s.groups.forEach(function (g) {
          var gd = el('div', 'group');
          gd.appendChild(el('h2', '', g.label));
          g.targets.forEach(function (tg) {
            gd.appendChild(el('h3', '', tg.target));
            tg.tracks.forEach(function (t) {
              var it = el('div', 'item');
              var b = el('button', 'play', 'Play');
              b.type = 'button';
              b.addEventListener('click', function () { playKey(t.key); });
              if (!t.onDisk) b.disabled = true;
              it.appendChild(b);
              it.appendChild(el('b', '', t.name));
              it.appendChild(el('span', 'pill', packLabel(t.pack)));
              it.appendChild(el('span', 'pill', mmss(t.seconds)));
              if (t.notUltima) it.appendChild(el('span', 'pill flag', 'not tagged Ultima'));
              if (t.note) it.appendChild(el('div', 'note', t.note));
              gd.appendChild(it);
            });
          });
          box.appendChild(gd);
        });
      })
      .catch(function (e) { clear(box).appendChild(el('p', 'muted', 'Could not load the summary: ' + e.message)); });
  }
  function view(which) {
    var bs = document.querySelectorAll('#views button');
    for (var i = 0; i < bs.length; i++) bs[i].classList.toggle('on', bs[i].getAttribute('data-view') === which);
    $('tracks-view').hidden = which !== 'tracks';
    $('summary-view').hidden = which !== 'summary';
    if (which === 'summary') loadSummary();
  }

  function status() {
    if (!data) return;
    var rated = 0, bad = keys(failed).length, busy = keys(timers).length + keys(inflight).filter(function (k) { return !timers[k]; }).length;
    for (var k in notes) if (has(notes, k) && notes[k].rating) rated++;
    $('state').textContent = order.length + ' tracks, ' + rated + ' rated' +
      (bad ? ', ' + bad + ' not saved' : busy ? ', saving...' : ', all saved') + ' (' + data.notesFile + ')' +
      (data.notesError ? ': ' + data.notesError : '');
  }

  // --- start --------------------------------------------------------------------------------------------------
  function draw() {
    var list = clear($('list'));
    data.byKey = {};
    rows = {};
    order = [];
    data.tracks.forEach(function (t) {
      data.byKey[t.key] = t;
      var r = buildRow(t);
      rows[t.key] = r;
      order.push(t.key);
      list.appendChild(r);
    });
    var fp = $('f-pack');
    data.packs.forEach(function (p) {
      var o = el('option', '', p.label + ' (' + p.count + (p.found ? '' : ', folder not found') + ')');
      o.value = p.id;
      fp.appendChild(o);
    });
    var ft = $('f-tag');
    data.tags.forEach(function (tg) { var o = el('option', '', tg.label); o.value = tg.id; ft.appendChild(o); });
    var dl = clear($('targets'));
    (data.suggestions.bosses || []).forEach(function (n) { var o = el('option', ''); o.value = n; o.label = 'boss'; dl.appendChild(o); });
    (data.suggestions.regions || []).forEach(function (n) { var o = el('option', ''); o.value = n; o.label = 'region'; dl.appendChild(o); });
    if (data.lastCommit && data.lastCommit.at) $('commit-line').textContent = 'Last save to repo ' + data.lastCommit.at + ': ' + data.lastCommit.result;
    filter();
    status();
    document.documentElement.setAttribute('data-drawn', '1');
  }
  function start() {
    ['q', 'f-pack', 'f-tag', 'f-rating'].forEach(function (id) { $(id).addEventListener('input', filter); });
    $('next-unrated').addEventListener('click', nextUnrated);
    $('save-repo').addEventListener('click', saveRepo);
    var bs = document.querySelectorAll('#views button');
    for (var i = 0; i < bs.length; i++) bs[i].addEventListener('click', function (e) { view(e.currentTarget.getAttribute('data-view')); });
    var a = $('audio');
    a.addEventListener('play', markButtons);
    a.addEventListener('pause', markButtons);
    a.addEventListener('ended', function () {
      markButtons();
      if (!$('autoplay').checked) return;
      var k = nextAfter(function (key) { return key !== current; });
      if (k) { playKey(k); rows[k].scrollIntoView({ block: 'center', behavior: 'smooth' }); }
    });
    fetch('/api/music', { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (j) {
        data = j;
        notes = {};
        for (var k in j.notes) if (Object.prototype.hasOwnProperty.call(j.notes, k)) {
          var n = j.notes[k];
          notes[k] = { rating: n.rating || '', tags: n.tags || [], note: n.note || '', target: n.target || '', rev: n.rev || 0 };
        }
        draw();
      })
      .catch(function (e) { $('state').textContent = 'Could not load the tracks: ' + e.message; });
  }
  document.addEventListener('DOMContentLoaded', start);
})();
