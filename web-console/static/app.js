/* cc-P155: the web console page. Polls /api/status every 5 s (a slow answer never stacks: one request at a time, each
   with a 10 s timeout) and draws every panel from it. Text is only ever set with textContent: nothing from the
   status reaches the page as HTML.
   cc-P182: the Golem's buttons. Each opens a sheet (the inputs it takes, then "type <word> to confirm"); Run posts
   /api/action, where the server checks the word again (and the console's runner a third time), and the output is
   polled from /api/action once a second until the result line. Only on the tailnet page (canAct); this PC's own page
   shows them greyed. Every console-art slot is drawn when its file exists (art, artFrames: a strip steps through its
   frames), else its drawn stand-in. ASCII only. */
(function () {
  'use strict';
  var POLL_MS = 5000, JOB_MS = 1000;
  var last = null, busy = false, art = [], frames = {}, sheetFor = null, jobSeen = 0, jobTimer = null, strips = [];

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

  // --- the art (cc-P132's slots in scripts/console-art) ---------------------------------------------------------------
  // artNode('golem-idle', 64, 'figure'): an <img> for a still, a stepping strip for an animated slot named in art.json,
  // or null when the slot has no file yet (the caller draws its stand-in).
  function artNode(slot, size, cls) {
    if (!slot) return null;
    var file = slot + '.png';
    if (!hasArt(file)) {
      if (hasArt(slot + '.gif')) file = slot + '.gif'; else return null;
    }
    var f = frames[slot];
    if (f && f.frames > 1 && /\.png$/.test(file)) {
      var d = el('span', 'strip ' + (cls || ''));
      d.style.width = size + 'px'; d.style.height = size + 'px';
      d.style.backgroundImage = 'url("/art/' + file + '")';
      d.style.backgroundSize = (f.frames * 100) + '% 100%';
      d.setAttribute('aria-hidden', 'true');
      strips.push({ node: d, frames: f.frames, fps: f.fps || 8, at: -1 });
      return d;
    }
    var i = el('img', cls || '');
    i.src = '/art/' + file; i.alt = ''; i.width = size; i.height = size;
    return i;
  }
  function stepStrips() {
    var now = Date.now();
    strips = strips.filter(function (s) { return document.body.contains(s.node); });
    strips.forEach(function (s) {
      var n = Math.floor(now / (1000 / s.fps)) % s.frames;
      if (n !== s.at) { s.at = n; s.node.style.backgroundPosition = (n * 100 / (s.frames - 1)) + '% 0'; }
    });
  }
  function drawChrome() {
    document.body.classList.toggle('art-stone', hasArt('stone.png'));
    document.body.classList.toggle('art-page', hasArt('page.png'));
    document.body.classList.toggle('art-band', hasArt('header-band.png'));
    var r = clear($('rune'));
    r.appendChild(artNode('rune-corner', 32, 'corner') || el('span', 'rune'));
    var links = document.querySelectorAll('#tabs a');
    for (var i = 0; i < links.length; i++) {
      var old = links[i].querySelector('.tabrune');
      if (old) old.parentNode.removeChild(old);
      var t = artNode('rune-tab', 16, 'tabrune');
      if (t) links[i].insertBefore(t, links[i].firstChild);
    }
  }
  function golemFigure(g) {
    var st = String((g && g.state) || '').toUpperCase();
    var slot = st === 'RUNNING' ? 'golem-running' : (st === 'OFF' || st === 'RESTING' || st === 'STOPPED' || !st) ? 'golem-dark' : 'golem-idle';
    var n = artNode(slot, 64, 'figure');
    if (n) return n;
    var f = el('span', 'figure stand-in ' + slot);
    f.setAttribute('aria-hidden', 'true');
    f.appendChild(el('span', 'eyes'));
    return f;
  }
  function spinner() { return artNode('spinner', 24, 'spin') || el('span', 'spin stand-in'); }

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
      d.appendChild(artNode('crystal-' + c, 22, 'crystal') || el('span', 'crystal'));
      var txt = el('div', 'tiletext');
      var w = el('div', 'word');
      var ic = artNode('icon-' + (t.key || ''), 20, 'icon');
      if (ic) w.appendChild(ic);
      w.appendChild(document.createTextNode((t.title || '') + ': ' + (t.word || '')));
      txt.appendChild(w);
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
      var cr = artNode('crystal-' + color(l && l.color), 18, 'crystal small');
      if (cr) d.appendChild(cr);
      d.appendChild(el('b', null, p[0] + ': '));
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

  function figureRow(g, lines) {
    var row = el('div', 'figrow');
    row.appendChild(golemFigure(g));
    var words = el('div', 'figwords');
    lines.forEach(function (l) { words.appendChild(el('p', null, l)); });
    row.appendChild(words);
    return row;
  }

  // cc-P184: one card per Golem while several work the queue (status.golem.golems): name, lane, machine, state,
  // the prompt and branch, the locks it holds. Hidden while multiGolem is off.
  function golemCard(c) {
    var li = el('li');
    var head = c.name + ' (' + [c.lane, c.engine, c.machine].filter(function (x) { return x; }).join(', ') + ')' + (c.integrator ? ', integrator' : '');
    li.appendChild(el('div', null, head));
    var s = c.state + (c.current ? ': ' + c.current + (c.branch && c.branch !== 'main' ? ' on ' + c.branch : '') : '') + (c.restReason ? ' (' + c.restReason + ')' : '');
    li.appendChild(el('div', c.state === 'RUNNING' ? 'ok' : (c.state === 'NEEDS YOU' || c.state === 'STOPPED' || c.state === 'UNKNOWN') ? 'warn' : 'quiet', s));
    var bits = [];
    if (c.locks && c.locks.length) bits.push('holds ' + c.locks.join(', '));
    if (c.usageSource) bits.push('usage from the ' + c.usageSource);
    if (c.hours) bits.push('hours ' + c.hours);
    if (c.needsYou) bits.push('needs you: ' + c.needsYou);
    if (bits.length) li.appendChild(el('div', 'meta', bits.join(' | ')));
    return li;
  }
  function drawGolemTeam(g) {
    var card = $('golem-team-card'); if (!card) return;
    var list = clear($('golem-team'));
    var cards = (g && g.golems) || [];
    if (!g || !g.multiGolem || !cards.length) { card.hidden = true; return; }
    card.hidden = false;
    cards.forEach(function (c) { list.appendChild(golemCard(c)); });
  }

  function drawGolem(g) {
    clear($('golem-state')).appendChild(figureRow(g, golemState(g)));
    drawGolemTeam(g);
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
    var pick = (g && g.runNext) || '';
    ((g && g.queue) || []).forEach(function (p, i) { q.appendChild(promptLine(p, i === 0 ? (pick && p.name === pick ? 'next (Run next)' : 'next') : '')); });
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
      bar.appendChild(fill);
      // cc-P160: the pace marker, where the Golem's pace rule allows this window to be now, and the reset time.
      if (typeof r.marker === 'number') {
        var mk = el('i', 'pace'); mk.style.left = Math.max(0, Math.min(100, r.marker)) + '%';
        mk.title = 'pace allows ' + r.marker + '% now'; bar.appendChild(mk);
      }
      u.appendChild(bar);
      var rs = /resets in [^(]+/.exec(r.text || '');
      if (rs || typeof r.marker === 'number') u.appendChild(el('div', 'meta', (typeof r.marker === 'number' ? 'pace ' + r.marker + '%' + (rs ? ', ' : '') : '') + (rs ? rs[0].trim() : '')));
    });
    // cc-P160: the plan usage page's lines: the pace warning, when it was last read, the free reset and the credits.
    ((g && g.usageNotes) || []).forEach(function (n) { u.appendChild(el('p', n.color === 'amber' ? 'warn' : 'quiet', n.text)); });
    // Today's summary card.
    var t = clear(document.querySelector('#today-golem .body'));
    t.appendChild(figureRow(g, golemState(g).slice(0, 2)));
    if (g && g.current) t.appendChild(el('p', null, 'Running P' + g.current.number + ': ' + (g.current.summary || g.current.title)));
    if (g && g.queue && g.queue.length) t.appendChild(el('p', 'meta', g.queue.length + ' queued, next P' + g.queue[0].number + ': ' + (g.queue[0].summary || g.queue[0].title)));
  }

  // --- the Golem's buttons (cc-P182) ------------------------------------------------------------------------------
  // Which of a pair applies now (wrap up or its cancel, night shift on or off); the server and the console check again.
  function applies(b, g) {
    var w = String(b.when || ''), i = w.indexOf(':'), kind = w.slice(0, i), val = w.slice(i + 1);
    if (b.key === 'retry' && !(g && g.stoppedPrompt)) return false;
    if (!w) return true;
    if (kind === 'winding') return !!g && g.winding === val;
    if (kind === 'notWinding') return !g || g.winding !== val;
    if (kind === 'nightShift') return !!(g && g.nightShift && g.nightShift.enabled) === (val === 'on');
    if (kind === 'runNext') return !!(g && g.runNext);
    return true;
  }
  function wordFor(b, g, inputs) {
    if (b.word === '#prompt') return inputs.prompt || '';
    if (b.word === '#stopped') return (g && g.stoppedPrompt) || '';
    return b.word || '';
  }

  function drawControls(j) {
    var a = j.actions || {}, g = (j.status || {}).golem || null, job = a.job;
    var box = clear($('ctl-buttons')), note = $('ctl-note');
    var running = !!(job && job.running);
    note.textContent = !a.canAct ? (a.why || 'Read only here.') : running ? ('Running: ' + job.label + '. One at a time.') : '';
    (a.buttons || []).forEach(function (b) {
      if (!applies(b, g)) return;
      var btn = el('button', 'ctl');
      btn.type = 'button';
      btn.setAttribute('data-key', b.key);
      btn.appendChild(artNode(b.art || '', 28, 'btnart') || el('span', 'glyph', (b.label || '?').charAt(0)));
      btn.appendChild(el('span', 'label', b.key === 'retry' && g && g.stoppedPrompt ? b.label + ' (' + g.stoppedPrompt + ')' : b.label));
      if (b.disabled) { btn.classList.add('off'); btn.title = b.disabled; }
      btn.disabled = !a.canAct || running;
      btn.addEventListener('click', function () { openSheet(b); });
      box.appendChild(btn);
    });
    $('foot-note').textContent = a.canAct ? 'The Golem\'s buttons ask for a typed word; one runs at a time.' : 'Read only here.';
    if (job && running && !jobTimer) { $('job').hidden = false; pollJob(); }
  }

  function field(label, input) {
    var l = el('label', 'field');
    l.appendChild(el('span', null, label));
    l.appendChild(input);
    return l;
  }
  function textInput(id, value) {
    var i = el('input');
    i.id = id; i.type = 'text'; i.value = value || '';
    i.setAttribute('autocomplete', 'off'); i.setAttribute('autocapitalize', 'off'); i.setAttribute('spellcheck', 'false');
    return i;
  }
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function tomorrowAt(h) {
    var d = new Date(Date.now() + 86400000);
    return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + ' ' + pad(h) + ':00';
  }

  function openSheet(b) {
    var g = ((last || {}).status || {}).golem || {};
    sheetFor = b;
    $('sheet-title').textContent = b.label;
    $('sheet-help').textContent = b.help || '';
    $('sheet-msg').textContent = b.disabled || '';
    var box = clear($('sheet-inputs'));
    (b.inputs || []).forEach(function (name) {
      if (name === 'model') {
        var s = el('select'); s.id = 'in-model';
        ['Fable', 'Opus', 'Sonnet'].forEach(function (m) { var o = el('option', null, m); o.value = m; s.appendChild(o); });
        var want = (g.queue && g.queue[0] && g.queue[0].model) || 'Opus';
        if (['Fable', 'Opus', 'Sonnet'].indexOf(want) >= 0) s.value = want;
        box.appendChild(field('Model', s));
      } else if (name === 'cap') {
        box.appendChild(field('Prompts (a number, or all)', textInput('in-cap', 'all')));
      } else if (name === 'until') {
        box.appendChild(field('Until (YYYY-MM-DD HH:MM)', textInput('in-until', tomorrowAt(6))));
      } else if (name === 'start' || name === 'end') {
        var ns = g.nightShift || {};
        box.appendChild(field(name === 'start' ? 'Starts (HH:MM)' : 'Ends (HH:MM)', textInput('in-' + name, name === 'start' ? (ns.start || '22:30') : (ns.end || '06:30'))));
      } else if (name === 'prompt') {
        var p = el('select'); p.id = 'in-prompt';
        (g.queue || []).forEach(function (q) { var o = el('option', null, 'P' + q.number + ': ' + (q.summary || q.title)); o.value = 'P' + q.number; p.appendChild(o); });
        if (!p.options.length) { var o0 = el('option', null, 'The queue is empty'); o0.value = ''; p.appendChild(o0); }
        p.addEventListener('change', checkWord);
        box.appendChild(field('Prompt to run next', p));
      }
    });
    $('sheet-word').value = '';
    $('sheet-confirm').hidden = !!b.disabled;
    $('sheet-run').hidden = !!b.disabled;
    checkWord();
    $('sheet').hidden = false;
    document.body.classList.add('sheet-open');
    if (!b.disabled) $('sheet-word').focus();
  }
  function closeSheet() {
    $('sheet').hidden = true;
    document.body.classList.remove('sheet-open');
    sheetFor = null;
  }
  function sheetInputs() {
    var out = {};
    ((sheetFor && sheetFor.inputs) || []).forEach(function (name) { var i = $('in-' + name); if (i) out[name] = String(i.value || '').trim(); });
    return out;
  }
  function checkWord() {
    if (!sheetFor) return;
    var g = ((last || {}).status || {}).golem || {};
    var want = wordFor(sheetFor, g, sheetInputs());
    $('sheet-want').textContent = want || '(nothing to confirm)';
    var typed = String($('sheet-word').value || '').trim().toLowerCase();
    $('sheet-run').disabled = !want || typed !== want.toLowerCase();
  }

  function post(url, body, cb) {
    var x = new XMLHttpRequest();
    x.open('POST', url, true);
    x.timeout = 15000;
    x.setRequestHeader('Content-Type', 'application/json');
    x.setRequestHeader('X-UOConsole-Action', '1');
    x.onload = function () { var j = null; try { j = JSON.parse(x.responseText); } catch (e) { j = { error: x.responseText }; } cb(x.status, j); };
    x.onerror = x.ontimeout = function () { cb(0, { error: 'no answer' }); };
    x.send(JSON.stringify(body));
  }

  function run() {
    if (!sheetFor) return;
    var b = sheetFor;
    $('sheet-run').disabled = true;
    $('sheet-msg').textContent = 'Starting...';
    post('/api/action', { key: b.key, typed: $('sheet-word').value, inputs: sheetInputs() }, function (code, j) {
      if (code === 202) {
        closeSheet();
        jobSeen = 0;
        clear($('job-lines'));
        $('job').hidden = false;
        $('job-title').textContent = b.label;
        $('job-result').textContent = 'Running...';
        pollJob();
        return;
      }
      $('sheet-msg').textContent = (j && j.error) || ('refused (' + code + ')');
      checkWord();
    });
  }

  function drawJob(j) {
    $('job').hidden = false;
    $('job-title').textContent = j.label || j.key || '';
    var sp = clear($('job-spin'));
    if (j.running) sp.appendChild(spinner());
    var pre = $('job-lines');
    (j.lines || []).forEach(function (l) { pre.appendChild(el('span', 'ln ' + color(l.color), l.text + '\n')); });
    if (typeof j.total === 'number') jobSeen = j.total;
    pre.scrollTop = pre.scrollHeight;
    var r = $('job-result');
    r.textContent = j.running ? 'Running...' : (j.result || '');
    r.className = 'result' + (j.running ? '' : j.code === 0 ? ' green' : j.code === 2 ? ' amber' : ' red');
  }
  function pollJob() {
    if (jobTimer) clearTimeout(jobTimer);
    jobTimer = -1;
    get('/api/action?since=' + jobSeen, function (err, j) {
      if (err) { $('job-result').textContent = 'The web console did not answer: ' + err.message; jobTimer = setTimeout(pollJob, JOB_MS * 3); return; }
      drawJob(j);
      if (j.running) jobTimer = setTimeout(pollJob, JOB_MS);
      else { jobTimer = null; poll(true); }
    });
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
    frames = j.artFrames || {};
    drawChrome();
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
    drawControls(j);
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
    $('sheet-word').addEventListener('input', checkWord);
    $('sheet-form').addEventListener('submit', function (e) { e.preventDefault(); if (!$('sheet-run').disabled) run(); });
    $('sheet-cancel').addEventListener('click', closeSheet);
    $('sheet').addEventListener('click', function (e) { if (e.target === $('sheet')) closeSheet(); });
    $('job-close').addEventListener('click', function () { if (!jobTimer) $('job').hidden = true; });
    poll(false);
    setInterval(function () { poll(false); }, POLL_MS);
    setInterval(stepStrips, 50);
  });
  window.UOConsole = { draw: draw, golemState: golemState, golemCard: golemCard, last: function () { return last; }, applies: applies };
})();
