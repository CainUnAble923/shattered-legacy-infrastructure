/* cc-P155: the web console page. Polls /api/status every 5 s (a slow answer never stacks: one request at a time, each
   with a 10 s timeout) and draws every panel from it. Text is only ever set with textContent: nothing from the
   status reaches the page as HTML.
   cc-P182: the Golem's buttons. Each opens a sheet (the inputs it takes, then "type <word> to confirm"); Run posts
   /api/action, where the server checks the word again (and the console's runner a third time), and the output is
   polled from /api/action once a second until the result line. Only on the tailnet page (canAct); this PC's own page
   shows them greyed. Every console-art slot is drawn when its file exists (art, artFrames: a strip steps through its
   frames), else its drawn stand-in.
   cc-P186: every Golem's card has the same buttons (those marked anyGolem): the sheet then names the Golem and Run
   posts it too. A Golem on this PC runs at once (the job card, as above); one on another PC is queued for its agent,
   and its card shows "queued until <name> is online", what it is running, what came back and what expired. Each card
   shows its machine, lanes, state, last readiness check and its portrait when it has one.
   cc-P187: the Golem picker (status.golemPicker: which Golem the Controls act for; the word then names it, "wake
   bronze", and Wake's model follows its lane), Assign (a queued prompt to the picked Golem), a tap on a queued or
   finished prompt reads it, and the Console tab: one page per Shard Console tab (status.pages, from
   console-actions.json) with its lists (/api/page, tap a row to pick it), its buttons (PC only ones greyed with the
   reason), the console's plan before Run (/api/preview, a dry run), its typed phrase, and for a dangerous button a
   second confirm that says what happens and to what. ASCII only. */
(function () {
  'use strict';
  var POLL_MS = 5000, JOB_MS = 1000;
  var last = null, busy = false, art = [], frames = {}, sheetFor = null, sheetGolem = null, jobSeen = 0, jobTimer = null, strips = [];
  var picked = '', sheetMode = 'golem', sheetPage = null, sheetInputsFixed = null, preview = null, pageData = {}, pageSel = {}, pageLoading = {};

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
  // cc-P187: #page-<id> is one of the console's pages (the Console tab's grid lists them).
  function showTab() {
    var key = (location.hash || '#today').slice(1), pid = '';
    if (/^page-[a-z]+$/.test(key)) { pid = key.slice(5); key = 'page'; }
    if (!$(key) || !$(key).classList.contains('tab')) key = 'today';
    var tabs = document.querySelectorAll('.tab');
    for (var i = 0; i < tabs.length; i++) tabs[i].classList.toggle('on', tabs[i].id === key);
    var links = document.querySelectorAll('#tabs a');
    for (var j = 0; j < links.length; j++) links[j].classList.toggle('on', links[j].getAttribute('data-tab') === (key === 'page' ? 'console' : key));
    if (key === 'logs' && !$('log-lines').textContent) loadLog();
    if (key === 'page') { $('page').setAttribute('data-page', pid); drawPage(); loadPage(pid, false); }
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
    // cc-P200: where the week lands at its reset at this rate, the free reset advice, and marathon mode.
    if (g.projection && g.projection.text) lines.push('The week: ' + g.projection.text + '.');
    if (g.projection && g.projection.resetAdvice) lines.push('Free reset: ' + g.projection.resetAdvice);
    var mar = g.marathon || {};
    if (mar.mode) lines.push('Marathon: ' + mar.mode + ', ' + (mar.on ? 'ON' + (mar.why ? ' (' + mar.why + ')' : '') : 'off') + '.');
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
  // cc-P186: its portrait (only when the card has one), its last readiness check, whether a remote one's agent is
  // online, the commands waiting for it, and its buttons (a = the status's actions: the buttons and canAct).
  function ago(iso) {
    var t = Date.parse(iso || '');
    if (isNaN(t)) return '';
    var s = Math.max(0, Math.round((Date.now() - t) / 1000));
    return s < 90 ? s + 's ago' : s < 5400 ? Math.round(s / 60) + ' min ago' : when(iso);
  }
  function readyLine(r) {
    if (!r) return null;
    var t = 'Ready check: ' + r.verdict + (r.at ? ', ' + when(r.at) : '') + (r.summary ? ': ' + r.summary : '');
    if (r.failed && r.failed.length && r.verdict !== 'READY') t += ' (failed: ' + r.failed.join(', ') + ')';
    return el('div', r.verdict === 'READY' ? 'ok' : 'warn', t);
  }
  // cc-P207: the glow behind a card's portrait, from the card's glow (panels.glow_state): working, quiet, waiting, needs
  // or off, in the Golem's own metal (metal-<name>). The words are the legend's, as the tooltip.
  var GLOW_WORDS = { working: 'Working', quiet: 'Running but quiet (10+ min)', waiting: 'Waiting', needs: 'Needs you', off: 'Off or offline' };
  function portraitNode(c, alt) {
    var glow = GLOW_WORDS[c.glow] ? c.glow : 'off';
    var wrap = el('span', 'portrait-glow glow-' + glow + ' metal-' + String(c.display || c.name || '').toLowerCase().replace(/[^a-z]+/g, '-'));
    wrap.title = (c.display || c.name) + ': ' + GLOW_WORDS[glow];
    var img = el('img', 'portrait');
    img.src = c.portraitUrl; img.alt = alt; img.width = 48; img.height = 48;
    wrap.appendChild(img);
    return wrap;
  }
  // cc-P208: Shadow Iron, the merge Golem (engine script): no lane, usage or Wake. Its last round, the tickets waiting,
  // the last merge and failure, a dirty-tree warning, a pause; its buttons are those with card 'merge' (Merge now,
  // Pause merging or Resume merging), run as the console's own (no Golem named: the scripts find the merge Golem).
  function mergeCard(c, a) {
    var li = el('li', 'golemcard mergecard');
    var top = el('div', 'cardhead');
    if (c.portraitUrl) top.appendChild(portraitNode(c, (c.display || c.name) + ' Golem'));
    top.appendChild(el('div', null, (c.display || c.name) + ' (merges every 10 minutes' + (c.machine ? ', ' + c.machine : '') + ')'));
    li.appendChild(top);
    var m = c.merge || null;
    var cls = c.state === 'NEEDS YOU' || (m && (m.dirty || m.paused)) ? 'warn' : c.state === 'DISABLED' ? 'quiet' : 'ok';
    li.appendChild(el('div', cls, c.state + (m && m.paused ? ': paused' : '')));
    if (!m) li.appendChild(el('div', 'quiet', 'No round yet: the scheduled task Shadow Iron runs every 10 minutes.'));
    else {
      li.appendChild(el('div', 'meta', 'Last round: ' + (ago(m.lastRound) || 'never') + (m.result ? ' (' + m.result + ')' : '')));
      li.appendChild(el('div', m.waiting ? 'meta' : 'quiet', 'Waiting: ' + (m.waiting ? m.waiting + ' (' + (m.tickets || []).join(', ') + ')' : 'none')));
      if (m.lastMerge) li.appendChild(el('div', 'meta', 'Last merge: ' + m.lastMerge.prompt + ' from ' + m.lastMerge.golem + ', ' + ago(m.lastMerge.at)));
      if (m.lastFailure) li.appendChild(el('div', 'warn', 'Last failure: ' + m.lastFailure.prompt + ' from ' + m.lastFailure.golem + ', ' + ago(m.lastFailure.at) + ': ' + m.lastFailure.why + (m.lastFailure.fixup ? ' (fix-up ' + m.lastFailure.fixup + ')' : ' (needs you)')));
      if (m.dirty) li.appendChild(el('div', 'warn', 'Not merging: the real folders are not clean (since ' + ago(m.dirtySince) + '): ' + m.dirty));
    }
    if (c.needsYou) li.appendChild(el('div', 'warn', 'Needs you: ' + c.needsYou));
    if (a) {
      var box = el('div', 'ctl-grid mini');
      var job = a.job, running = !!(job && job.running);
      (a.buttons || []).forEach(function (b) {
        if (b.card !== 'merge' || b.disabled || !applies(b, c)) return;
        var btn = el('button', 'ctl small');
        btn.type = 'button';
        btn.setAttribute('data-key', b.key);
        btn.appendChild(el('span', 'label', b.label));
        btn.disabled = !a.canAct || running;
        btn.addEventListener('click', function () { openSheet(b, null); });
        box.appendChild(btn);
      });
      if (box.firstChild) li.appendChild(box);
    }
    return li;
  }
  function hasMergeCard() {
    var cards = ((((last || {}).status || {}).golem || {}).golems) || [];
    for (var i = 0; i < cards.length; i++) if (cards[i].script && !isTinker(cards[i])) return true;
    return false;
  }
  // cc-P222: the Tinker, the watchdog (engine script): its last round, fixes today, open questions, the kit on each PC,
  // a pause; its buttons are those with card 'tinker' (Run now, Pause, Resume).
  function isTinker(c) { return !!(c && (c.tinker || c.name === 'Tinker')); }
  function tinkerCard(c, a) {
    var li = el('li', 'golemcard mergecard');
    var top = el('div', 'cardhead');
    if (c.portraitUrl) {
      var img = el('img', 'portrait');
      img.src = c.portraitUrl; img.alt = (c.display || c.name); img.width = 48; img.height = 48;
      top.appendChild(img);
    }
    top.appendChild(el('div', null, (c.display || c.name) + ' (looks every 10 minutes' + (c.machine ? ', ' + c.machine : '') + ')'));
    li.appendChild(top);
    var t = c.tinker || null;
    li.appendChild(el('div', c.state === 'NEEDS YOU' || (t && t.paused) ? 'warn' : c.state === 'DISABLED' ? 'quiet' : 'ok', c.state + (t && t.paused ? ': paused' : '')));
    if (!t) li.appendChild(el('div', 'quiet', 'No round yet: the scheduled task Tinker runs every 10 minutes.'));
    else {
      li.appendChild(el('div', 'meta', 'Last round: ' + (ago(t.lastRound) || 'never') + '. Fixes today: ' + (t.fixesToday || 0) + (t.escalationsToday ? ', reasoning sessions: ' + t.escalationsToday : '')));
      (t.fixes || []).slice(-3).forEach(function (f) { li.appendChild(el('div', f.ok ? 'meta' : 'warn', (f.ok ? 'Fixed ' : 'Tried ') + f.golem + (f.prompt ? ' ' + f.prompt : '') + ': ' + f.kind + ', ' + f.what)); });
      (t.questions || []).forEach(function (q) { li.appendChild(el('div', 'warn', 'Question: ' + q.text)); });
      (t.waiting || []).forEach(function (w) { li.appendChild(el('div', 'meta', 'Waiting: ' + w)); });
      var k = t.kit || {};
      var inStep = (k.golems || []).filter(function (x) { return x.inStep; }).map(function (x) { return x.name; });
      var out = (k.golems || []).filter(function (x) { return !x.inStep; });
      li.appendChild(el('div', out.length ? 'warn' : 'meta', 'Kit ' + (k.id || '?') + ': ' + (inStep.length ? inStep.join(', ') + ' in step' : 'no PC checked yet') + (out.length ? '; not in step: ' + out.map(function (x) { return x.name + ' (' + x.why + ')'; }).join(', ') : '')));
    }
    if (a) {
      var box = el('div', 'ctl-grid mini');
      var job = a.job, running = !!(job && job.running);
      (a.buttons || []).forEach(function (b) {
        if (b.card !== 'tinker' || b.disabled) return;
        if (b.key === 'tinker-pause' && t && t.paused) return;
        if (b.key === 'tinker-resume' && !(t && t.paused)) return;
        var btn = el('button', 'ctl small');
        btn.type = 'button';
        btn.setAttribute('data-key', b.key);
        btn.appendChild(el('span', 'label', b.label));
        btn.disabled = !a.canAct || running;
        btn.addEventListener('click', function () { openSheet(b, null); });
        box.appendChild(btn);
      });
      if (box.firstChild) li.appendChild(box);
    }
    return li;
  }
  // cc-P208: the console's own Golem: home on the card (the integrator, or Iron while Shadow Iron merges).
  function isOwn(c) { return c.home !== undefined ? !!c.home : !!c.integrator; }
  function golemCard(c, a) {
    if (c.script) return isTinker(c) ? tinkerCard(c, a) : mergeCard(c, a);
    var li = el('li', 'golemcard');
    var head = c.name + ' (' + [c.lane, c.engine, c.machine].filter(function (x) { return x; }).join(', ') + ')' + (c.integrator ? ', integrator' : '');
    var top = el('div', 'cardhead');
    if (c.portraitUrl) top.appendChild(portraitNode(c, c.name + ' Golem'));
    top.appendChild(el('div', null, head));
    li.appendChild(top);
    var s = c.state + (c.current ? ': ' + c.current + (c.branch && c.branch !== 'main' ? ' on ' + c.branch : '') : '') + (c.restReason ? ' (' + c.restReason + ')' : '');
    li.appendChild(el('div', c.state === 'RUNNING' ? 'ok' : (c.state === 'NEEDS YOU' || c.state === 'STOPPED' || c.state === 'UNKNOWN') ? 'warn' : 'quiet', s));
    var bits = [];
    if (c.locks && c.locks.length) bits.push('holds ' + c.locks.join(', '));
    if (c.usageSource) bits.push('usage from the ' + c.usageSource);
    // cc-P200: while marathon is on an extra PC's hours are widened to the whole day (its back-off still holds).
    if (c.hours) bits.push('hours ' + c.hours + (c.marathonDay ? ' (all day now: marathon)' : ''));
    if (c.needsYou) bits.push('needs you: ' + c.needsYou);
    if (bits.length) li.appendChild(el('div', 'meta', bits.join(' | ')));
    if (c.remote) {
      li.appendChild(el('div', c.online ? 'ok' : 'warn', 'On another PC: ' + (c.online ? 'its agent is online (' + ago(c.lastSeen) + ')' :
        c.lastSeen ? 'offline, last seen ' + ago(c.lastSeen) : 'its agent has not called in yet')));
    }
    var rl = readyLine(c.ready);
    if (rl) li.appendChild(rl);
    (c.commands || []).slice(0, 4).forEach(function (x) {
      li.appendChild(el('div', x.state === 'done' ? (x.code === 0 ? 'meta' : 'warn') : x.state === 'queued' || x.state === 'taken' ? 'quiet' : 'warn',
        (x.label || x.key) + ': ' + (x.note || x.state) + (x.endedAt ? ' (' + when(x.endedAt) + ')' : '')));
    });
    // The same buttons as the integrator's (those marked anyGolem), for every other Golem that is enabled.
    if (a && !isOwn(c) && c.enabled !== false) {
      var box = el('div', 'ctl-grid mini');
      var job = a.job, running = !!(job && job.running);
      (a.buttons || []).forEach(function (b) {
        if (!b.anyGolem || b.disabled || !applies(b, c)) return;
        var btn = el('button', 'ctl small');
        btn.type = 'button';
        btn.setAttribute('data-key', b.key);
        btn.setAttribute('data-golem', c.name);
        btn.appendChild(el('span', 'label', b.key === 'retry' && c.stoppedPrompt ? b.label + ' (' + c.stoppedPrompt + ')' : b.label));
        btn.disabled = !a.canAct || (running && !c.remote);
        btn.addEventListener('click', function () { openSheet(b, c); });
        box.appendChild(btn);
      });
      if (box.firstChild) li.appendChild(box);
    }
    return li;
  }
  // cc-P149: away mode (until when), the parked prompts and the last email commands, under the team's cards.
  function drawAway(g) {
    var card = $('golem-team-card'); if (!card) return;
    var old = $('golem-away'); if (old) old.parentNode.removeChild(old);
    var aw = (g && g.away) || null, mc = (g && g.mailCommands) || null;
    var box = el('div', 'meta'); box.id = 'golem-away';
    if (aw && aw.on) box.appendChild(el('div', 'warn', 'Away mode on until ' + when(aw.until) + ': around the clock within the night usage rule; a digest each morning.'));
    ((aw && aw.parked) || []).forEach(function (p) { box.appendChild(el('div', 'warn', 'Parked ' + p.prompt + ': ' + p.why + (p.branch ? ' (its work: ' + p.branch + ')' : ''))); });
    if (mc && mc.on && !mc.setUp) box.appendChild(el('div', 'warn', 'Email commands are on, but the IMAP app password is not saved yet.'));
    ((mc && mc.last) || []).slice(-5).forEach(function (e) { box.appendChild(el('div', e.result === 'rejected' ? 'warn' : 'quiet', 'Email ' + when(e.at) + ', ' + e.result + ': ' + (e.line ? "'" + e.line + "'" : '(no line)') + (e.why ? ' (' + e.why + ')' : ''))); });
    if (box.firstChild) card.appendChild(box);
  }
  function drawGolemTeam(g, a) {
    var card = $('golem-team-card'); if (!card) return;
    var list = clear($('golem-team'));
    var cards = (g && g.golems) || [];
    if (!g || !g.multiGolem || !cards.length) { card.hidden = true; return; }
    card.hidden = false;
    cards.forEach(function (c) { list.appendChild(golemCard(c, a)); });
  }

  // --- the Golem picker (cc-P187) -----------------------------------------------------------------------------------
  // status.golemPicker: every Golem in golems.json. The picked one is who the Controls card acts for. A disabled one
  // (or any but the integrator while multiGolem is off) cannot be picked and says why; an offline remote one is greyed
  // but may be picked (its buttons wait for its agent, cc-P186).
  function pickerList() { return (last && last.golemPicker) || []; }
  function pickedEntry() {
    var list = pickerList(), i;
    for (i = 0; i < list.length; i++) if (list[i].name === picked) return list[i];
    for (i = 0; i < list.length; i++) if (list[i].integrator) return list[i];
    return null;
  }
  // The card a picked Golem's buttons act for (null: the integrator, the Controls card as before).
  function pickedCard() {
    var p = pickedEntry();
    if (!p || p.integrator) return null;
    var cards = ((((last || {}).status || {}).golem || {}).golems) || [];
    for (var i = 0; i < cards.length; i++) if (cards[i].name === p.name) { var c = {}; for (var k in cards[i]) c[k] = cards[i][k]; c.lane = c.lane || p.lane; c.remote = p.remote; c.machine = p.machine; return c; }
    return { name: p.name, lane: p.lane, machine: p.machine, remote: p.remote, state: p.state, integrator: false };
  }
  function drawPicker() {
    var box = $('picker'); if (!box) return;
    var list = pickerList();
    $('picker-card').hidden = !list.length;
    clear(box);
    var cur = pickedEntry();
    if (cur && !picked) picked = cur.name;
    list.forEach(function (p) {
      var li = el('li');
      var b = el('button', 'pick' + (cur && p.name === cur.name ? ' on' : '') + (p.grey || !p.usable ? ' grey' : ''));
      b.type = 'button';
      b.setAttribute('data-golem', p.name);
      if (p.portraitUrl) { var img = el('img', 'portrait'); img.src = p.portraitUrl; img.alt = ''; img.width = 40; img.height = 40; b.appendChild(img); }
      else b.appendChild(el('span', 'glyph', p.name.charAt(0)));
      var t = el('span', 'picktext');
      t.appendChild(el('b', null, p.name + (p.integrator ? ' (integrator)' : '')));
      t.appendChild(el('span', 'meta', [p.lane, p.machine, p.state + (p.current ? ' ' + p.current : '')].filter(function (x) { return x; }).join(' | ')));
      if (p.why || p.grey) t.appendChild(el('span', 'warn', p.why || p.grey));
      b.appendChild(t);
      b.disabled = !p.usable;
      b.addEventListener('click', function () { picked = p.name; draw(last); });
      li.appendChild(b);
      box.appendChild(li);
    });
    $('picker-note').textContent = cur ? ('The Controls act for the ' + cur.name + ' Golem' + (cur.integrator ? ' (the integrator).' : ': only the buttons any Golem has, and each word names it.')) : '';
  }

  function drawGolem(g, a) {
    clear($('golem-state')).appendChild(figureRow(g, golemState(g)));
    drawGolemTeam(g, a);
    drawAway(g);
    drawPicker();
    var cur = clear($('golem-current'));
    if (g && g.current) {
      var c = g.current;
      cur.appendChild(el('p', null, 'P' + c.number + ': ' + (c.summary || c.title)));
      var bits = [c.model, c.size];
      if (c.stepsTotal) bits.push('step ' + c.stepsDone + ' of ' + c.stepsTotal + (c.stepNow ? ': ' + c.stepNow : ''));
      if (c.startedAt) bits.push('started ' + when(c.startedAt));
      if (c.lastActivity) bits.push('last activity ' + when(c.lastActivity));
      cur.appendChild(el('p', 'meta', bits.filter(function (x) { return x; }).join(' | ')));
      if (c.name) cur.appendChild(readButton('Read it', { name: c.name }));
    } else cur.appendChild(el('p', 'quiet', 'Nothing running.'));
    var q = clear($('golem-queue'));
    var pick = (g && g.runNext) || '';
    ((g && g.queue) || []).forEach(function (p, i) {
      var li = promptLine(p, i === 0 ? (pick && p.name === pick ? 'next (Run next)' : 'next') : '');
      if (p.name) li.appendChild(readButton('Read', { name: p.name }));
      q.appendChild(li);
    });
    if (!q.firstChild) q.appendChild(el('li', 'quiet', 'The queue is empty.'));
    var f = clear($('golem-finished'));
    ((g && g.finished) || []).forEach(function (x) {
      var li = el('li');
      li.appendChild(el('div', null, 'P' + x.number + ' ' + x.result + ': ' + x.title));
      if (x.headline) li.appendChild(el('div', 'quiet', x.headline));
      li.appendChild(el('div', 'meta', when(x.time)));
      li.appendChild(readButton('Notes', { notes: x.number }));
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

  // cc-P187: read a queued, running or finished prompt (or its notes) on the page, as the console opens it in Notepad.
  function readButton(label, what) {
    var b = el('button', 'small read', label);
    b.type = 'button';
    b.addEventListener('click', function () {
      var url = what.name ? '/api/prompt?name=' + encodeURIComponent(what.name) : '/api/prompt?notes=' + encodeURIComponent(what.notes);
      $('reader').hidden = false;
      $('reader-title').textContent = 'Reading...';
      $('reader-text').textContent = '';
      get(url, function (err, j) {
        if (err) { $('reader-title').textContent = 'Could not read it: ' + err.message; return; }
        $('reader-title').textContent = j.what;
        $('reader-text').textContent = j.text;
        $('reader').scrollIntoView({ block: 'start' });
      });
    });
    return b;
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
    if (kind === 'autoWake') return !!(g && g.autoWake) === (val === 'on');
    if (kind === 'marathonNot') return !g || !g.marathon || g.marathon.mode !== val;   // cc-P200
    if (kind === 'runNext') return !!(g && g.runNext);
    if (kind === 'mergePaused') return !!(g && g.merge && g.merge.paused) === (val === 'on');   // cc-P208: g is Shadow Iron's card
    return true;
  }
  // cc-P187: for another Golem than the integrator a button with a golemWord wants that word and its name.
  function wordFor(b, g, inputs, golemName) {
    var base;
    if (b.word === '#prompt') base = inputs.prompt || '';
    else if (b.word === '#stopped') base = (g && g.stoppedPrompt) || '';
    else if (b.word === '#assign') return inputs.prompt && inputs.assignTo ? inputs.prompt + ' on ' + inputs.assignTo.toLowerCase() : '';
    else base = b.word || '';
    if (golemName && b.golemWord && base) return (b.golemWord.charAt(0) === '#' ? base : b.golemWord) + ' ' + golemName.toLowerCase();
    return base;
  }

  function drawControls(j) {
    var a = j.actions || {}, g = (j.status || {}).golem || null, job = a.job;
    var box = clear($('ctl-buttons')), note = $('ctl-note');
    var running = !!(job && job.running);
    var card = pickedCard();   // cc-P187: null for the integrator
    var gs = card || g;
    note.textContent = !a.canAct ? (a.why || 'Read only here.') : running ? ('Running: ' + job.label + '. One at a time.') : card ? ('For the ' + card.name + ' Golem (picked above).') : '';
    var merging = hasMergeCard();
    (a.buttons || []).forEach(function (b) {
      if (card && !b.anyGolem && b.key !== 'assign') return;
      // cc-P208: Shadow Iron's buttons are on its card; Merge now stays here while there is no such card.
      if (b.card === 'merge' && (merging || b.key !== 'integrate')) return;
      if (b.card === 'tinker') return;   // cc-P222: on the Tinker's card only
      if (!applies(b, gs)) return;
      var btn = el('button', 'ctl');
      btn.type = 'button';
      btn.setAttribute('data-key', b.key);
      btn.appendChild(artNode(b.art || '', 28, 'btnart') || el('span', 'glyph', (b.label || '?').charAt(0)));
      btn.appendChild(el('span', 'label', b.key === 'retry' && gs && gs.stoppedPrompt ? b.label + ' (' + gs.stoppedPrompt + ')' : b.label));
      if (b.disabled) { btn.classList.add('off'); btn.title = b.disabled; }
      btn.disabled = !a.canAct || (running && !(card && card.remote));
      btn.addEventListener('click', function () { openSheet(b, b.key === 'assign' ? null : card); });
      box.appendChild(btn);
    });
    $('foot-note').textContent = a.canAct ? 'Every button shows what it does first and asks the console\'s word; one runs at a time.' : 'Read only here.';
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
  function resetSheet() {
    preview = null;
    clear($('sheet-plan')); $('sheet-plan').hidden = true;
    $('sheet-second').hidden = true;
    $('sheet-first').hidden = false;
    $('sheet-run').textContent = 'Run';
  }

  function openSheet(b, card) {
    var g = ((last || {}).status || {}).golem || {};
    sheetMode = 'golem';
    sheetPage = null;
    sheetFor = b;
    sheetGolem = card || null;   // cc-P186: another Golem's card; null is the integrator (the Controls card)
    resetSheet();
    $('sheet-title').textContent = b.label + (card ? ' (the ' + card.name + ' Golem' + (card.remote ? ', on ' + card.machine : '') + ')' : '');
    $('sheet-help').textContent = b.help || '';
    $('sheet-msg').textContent = b.disabled || '';
    var box = clear($('sheet-inputs'));
    (b.inputs || []).forEach(function (name) {
      if (name === 'model') {
        var s = el('select'); s.id = 'in-model';
        // cc-P153: Auto first and the default: the Golem picks the model from the prompt's Model line (no line: Opus), as
        // its self-wake does; Fable, Opus or Sonnet overrides it for this session.
        var rows = g.queueRows || [];
        var auto = rows.length ? 'Auto: ' + rows[0].model + ', for ' + rows[0].label : 'Auto (best for the next prompt)';
        [['Auto', auto], ['Fable', 'Fable'], ['Opus', 'Opus'], ['Sonnet', 'Sonnet']].forEach(function (m) { var o = el('option', null, m[1]); o.value = m[0]; s.appendChild(o); });
        s.value = 'Auto';
        box.appendChild(field('Model (Auto reads the prompt; change it here to override)', s));
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
        box.appendChild(field(b.key === 'assign' ? 'Prompt to assign' : 'Prompt to run next', p));
      } else if (name === 'assignTo') {
        // cc-P187: Assign: the Golem picked above, or another usable one.
        var gsel = el('select'); gsel.id = 'in-assignTo';
        pickerList().forEach(function (x) { if (!x.usable) return; var o = el('option', null, x.name + ' (' + x.lane + ')'); o.value = x.name; gsel.appendChild(o); });
        var pe = pickedEntry(); if (pe && pe.usable) gsel.value = pe.name;
        gsel.addEventListener('change', checkWord);
        box.appendChild(field('Give it to', gsel));
      } else if (name === 'next') {
        var ns2 = el('select'); ns2.id = 'in-next';
        [['yes', 'Yes, it goes next for that Golem'], ['no', 'No, keep its place']].forEach(function (x) { var o = el('option', null, x[1]); o.value = x[0]; ns2.appendChild(o); });
        box.appendChild(field('Run it next', ns2));
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
    sheetGolem = null;
    sheetPage = null;
    preview = null;
  }
  function sheetInputs() {
    if (sheetMode === 'page') return sheetInputsFixed || {};
    var out = {};
    ((sheetFor && sheetFor.inputs) || []).forEach(function (name) { var i = $('in-' + name); if (i) out[name] = String(i.value || '').trim(); });
    return out;
  }
  function checkWord() {
    if (!sheetFor) return;
    var typed = String($('sheet-word').value || '').trim().toLowerCase();
    if (sheetMode === 'page') {
      var want0 = preview ? preview.expect : '';
      $('sheet-want').textContent = want0 || '';
      $('sheet-run').disabled = !preview || !!preview.refused || (!!want0 && typed !== want0.toLowerCase());
      return;
    }
    var g = sheetGolem || ((last || {}).status || {}).golem || {};
    var want = wordFor(sheetFor, g, sheetInputs(), sheetGolem && !sheetGolem.integrator ? sheetGolem.name : '');
    $('sheet-want').textContent = want || '(nothing to confirm)';
    $('sheet-run').disabled = !want || typed !== want.toLowerCase();
  }

  function post(url, body, cb, timeoutMs) {
    var x = new XMLHttpRequest();
    x.open('POST', url, true);
    x.timeout = timeoutMs || 15000;
    x.setRequestHeader('Content-Type', 'application/json');
    x.setRequestHeader('X-UOConsole-Action', '1');
    x.onload = function () { var j = null; try { j = JSON.parse(x.responseText); } catch (e) { j = { error: x.responseText }; } cb(x.status, j); };
    x.onerror = x.ontimeout = function () { cb(0, { error: 'no answer' }); };
    x.send(JSON.stringify(body));
  }

  function startedJob(label) {
    closeSheet();
    jobSeen = 0;
    clear($('job-lines'));
    $('job').hidden = false;
    $('job-title').textContent = label;
    $('job-result').textContent = 'Running...';
    pollJob();
  }

  function run() {
    if (!sheetFor) return;
    if (sheetMode === 'page') return runPage(false);
    var b = sheetFor, card = sheetGolem;
    $('sheet-run').disabled = true;
    $('sheet-msg').textContent = 'Starting...';
    var body = { key: b.key, typed: $('sheet-word').value, inputs: sheetInputs() };
    if (card) body.golem = card.name;
    post('/api/action', body, function (code, j) {
      if (code === 202 && j && j.queued) {
        // cc-P186: a Golem on another PC: queued for its agent; its card shows the wait and then the result.
        closeSheet();
        clear($('job-lines')); clear($('job-spin'));
        $('job').hidden = false;
        $('job-title').textContent = b.label + ' (the ' + card.name + ' Golem)';
        var r = $('job-result'); r.textContent = j.message || 'queued'; r.className = 'result' + (j.online ? '' : ' amber');
        poll(true);
        return;
      }
      if (code === 202) { startedJob(b.label); return; }
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
      else { jobTimer = null; poll(true); var pid = $('page').getAttribute('data-page'); if (pid && $('page').classList.contains('on')) loadPage(pid, true); }
    });
  }

  // --- the console's pages (cc-P187) ------------------------------------------------------------------------------
  function pagesDoc() { return (last && last.pages) || { fields: {}, pages: [] }; }
  function pageById(id) { var ps = pagesDoc().pages || []; for (var i = 0; i < ps.length; i++) if (ps[i].id === id) return ps[i]; return null; }
  function sel(pid) { if (!pageSel[pid]) pageSel[pid] = { shard: 'test' }; return pageSel[pid]; }

  function drawConsoleGrid() {
    var box = $('page-grid'); if (!box) return;
    var ps = pagesDoc().pages || [];
    if (box.getAttribute('data-n') === String(ps.length)) return;
    clear(box);
    box.setAttribute('data-n', String(ps.length));
    ps.forEach(function (p) {
      var a = el('a', p.id === 'live' ? 'live' : '');
      a.href = '#page-' + p.id;
      a.setAttribute('data-page', p.id);
      a.appendChild(el('b', null, p.title));
      var n = (p.buttons || []).length, pc = (p.buttons || []).filter(function (b) { return b.pcOnly; }).length;
      a.appendChild(el('span', 'meta', n ? n + ' button' + (n === 1 ? '' : 's') + (pc ? ', ' + pc + ' PC only' : '') : 'commands to copy'));
      box.appendChild(a);
    });
  }

  function loadPage(pid, fresh) {
    if (!pid || pageLoading[pid]) return;
    if (['golem', 'diag', 'mapart'].indexOf(pid) >= 0) return;   // no lists
    pageLoading[pid] = true;
    drawPage();
    get('/api/page?id=' + encodeURIComponent(pid) + (fresh ? '&fresh=1' : ''), function (err, j) {
      pageLoading[pid] = false;
      pageData[pid] = err ? { error: err.message } : j;
      drawPage();
    }, 95000);
  }

  // A list of rows to pick one from: { value, cells } (Get-WebConsoleList.ps1's rows).
  function pickList(pid, name, rows, opts) {
    var s = sel(pid), ul = el('ul', 'picklist');
    if (!rows || !rows.length) { ul.appendChild(el('li', 'quiet', (opts && opts.empty) || 'Nothing here.')); return ul; }
    rows.forEach(function (r) {
      var li = el('li'), b = el('button', 'row' + (s[name] === r.value ? ' on' : ''));
      b.type = 'button';
      b.setAttribute('data-value', r.value);
      if (opts && opts.picture && r.picture) { var img = el('img', 'thumb'); img.src = r.picture; img.alt = ''; img.width = 48; img.height = 48; b.appendChild(img); }
      b.appendChild(el('b', null, (opts && opts.title) ? opts.title(r) : r.value));
      var meta = (r.cells || []).filter(function (x) { return x; }).join(' | ');
      if (meta) b.appendChild(el('span', 'meta', meta));
      b.addEventListener('click', function () { s[name] = (s[name] === r.value ? '' : r.value); drawPage(); });
      li.appendChild(b);
      if (opts && opts.link && r.url) { var a = el('a', 'meta', 'Open ' + r.url); a.href = r.url; a.target = '_blank'; a.rel = 'noopener noreferrer'; li.appendChild(a); }
      ul.appendChild(li);
    });
    return ul;
  }
  function shardRadios(pid) {
    var s = sel(pid), d = el('div', 'radios');
    ['test', 'live'].forEach(function (k) {
      var b = el('button', s.shard === k ? 'on' : '', k === 'live' ? 'LIVE shard' : 'Test shard');
      b.type = 'button';
      b.setAttribute('data-shard', k);
      b.addEventListener('click', function () { s.shard = k; s.snapshot = ''; drawPage(); });
      d.appendChild(b);
    });
    return d;
  }
  function textField(pid, name, label, check) {
    var s = sel(pid), i;
    if (check) {
      var l = el('label', 'check');
      i = el('input'); i.type = 'checkbox'; i.checked = s[name] === 'yes';
      i.addEventListener('change', function () { s[name] = i.checked ? 'yes' : ''; });
      l.appendChild(i); l.appendChild(el('span', null, label));
      return l;
    }
    i = textInput('pg-' + name, s[name] || '');
    i.addEventListener('input', function () { s[name] = String(i.value || '').trim(); });
    return field(label, i);
  }
  function copyText(t, done) {
    var ok = function () { if (done) done('copied'); };
    if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(t).then(ok, function () { if (done) done('copy it by hand: ' + t); }); }
    else if (done) done('copy it by hand: ' + t);
  }

  // Why a page button cannot run now (shown greyed with it), or ''.
  function blockedWhy(p, b, data, a) {
    if (b.pcOnly) return 'PC only: ' + b.pcOnly;
    if (b.disabled) return b.disabled;
    var s = sel(p.id);
    if (p.id === 'live' && data && data.liveRetired) return 'Live is ' + (data.line || 'retired') + '.';
    if ((p.id === 'question' || p.id === 'snapshots') && data && data.shards && data.shards[s.shard] && data.shards[s.shard].retired) return 'Live is retired: nothing acts on it.';
    if (p.id === 'question' && data && data.shards && data.shards[s.shard] && (data.shards[s.shard].enabled || []).indexOf(b.key) < 0) return 'Not an answer to this question.';
    if (!a.canAct) return a.why || 'Read only here.';
    return '';
  }

  function drawPage() {
    var host = $('page'); if (!host) return;
    var pid = host.getAttribute('data-page') || '', p = pageById(pid);
    var body = clear($('page-body'));
    if (!p) { $('page-title').textContent = last ? 'No such page' : 'Loading...'; return; }
    var data = pageData[pid], s = sel(pid), a = (last && last.actions) || {}, fields = pagesDoc().fields || {};
    var job = a.job, running = !!(job && job.running);
    $('page-title').textContent = p.title;
    body.appendChild(el('p', 'quiet', p.caption || ''));
    if (pageLoading[pid]) body.appendChild(el('p', 'quiet', 'Reading the lists...'));
    if (data && data.error) body.appendChild(el('p', 'warn', 'The lists could not be read: ' + data.error));
    var used = {};
    (p.buttons || []).forEach(function (b) { (b.inputs || []).forEach(function (n) { used[n] = true; }); });
    if (used.shard) body.appendChild(shardRadios(pid));
    // The page's own panels and lists, as the window's tab shows them.
    if (pid === 'question' && data && data.shards) {
      body.appendChild(el('pre', 'log', ((data.shards[s.shard] || {}).lines || []).join('\n')));
    }
    if (pid === 'snapshots' && data && data.shards) {
      var sh = data.shards[s.shard] || {};
      body.appendChild(el('p', 'meta', 'save: ' + (sh.save || '') + ' | snapshots: ' + (sh.folder || '')));
      body.appendChild(pickList(pid, 'snapshot', sh.rows, { empty: 'No snapshots of this shard.' }));
    }
    if (pid === 'test' && data) { body.appendChild(el('h3', null, 'Images to deploy')); body.appendChild(pickList(pid, 'imageTag', data.images, { empty: 'No sl-modernuo:cc-p* image to deploy.' })); }
    if (pid === 'fix' && data) {
      if (data.lastGood) body.appendChild(el('p', 'meta', 'Last good: ' + data.lastGood + ' (Roll back runs it).'));
      body.appendChild(el('h3', null, 'Commits to rebuild'));
      body.appendChild(pickList(pid, 'commit', data.commits, { title: function (r) { return (r.cells && r.cells[0]) || r.value; } }));
    }
    if (pid === 'commit' && data && data.countdown) body.appendChild(el('p', data.countdown === 'Ready to commit' ? 'ok' : 'warn', data.countdown));
    if (pid === 'package' && data) {
      if (data.client) body.appendChild(el('pre', 'log', data.client));
      body.appendChild(el('h3', null, 'Zips in dist (newest first)'));
      body.appendChild(pickList(pid, 'packageZip', data.zips, { empty: 'No zips in dist.' }));
    }
    if (pid === 'websites' && data) body.appendChild(pickList(pid, 'site', data.sites, { title: function (r) { return (r.cells && r.cells[0]) || r.value; }, link: true }));
    if (pid === 'art' && data) body.appendChild(pickList(pid, 'artId', data.requests, { picture: true, empty: 'No art requests.' }));
    if (pid === 'live' && data) body.appendChild(el('p', data.liveRetired ? 'warn' : 'quiet', 'LIVE: ' + (data.line || '')));
    if (pid === 'world' && data && data.groups) {
      body.appendChild(el('p', 'meta', (data.count || 0) + ' registrations read from the code.'));
      data.groups.forEach(function (gr) {
        var c = el('div', 'card');
        c.appendChild(el('h3', null, gr.title));
        c.appendChild(el('p', 'meta', gr.source || ''));
        var msg = el('p', 'quiet', '');
        if (gr.copyAll && gr.items && gr.items.length) {
          var all = el('button', 'small', 'Copy group');
          all.type = 'button';
          all.addEventListener('click', function () { copyText(gr.items.map(function (x) { return x.command; }).join('\n'), function (m) { msg.textContent = m; }); });
          c.appendChild(all);
        }
        if (!gr.items || !gr.items.length) c.appendChild(el('p', 'quiet', '(none)'));
        (gr.items || []).forEach(function (it) {
          var row = el('div', 'worldrow');
          var tick = el('input'); tick.type = 'checkbox'; tick.checked = !!s['tick:' + it.command];
          tick.addEventListener('change', function () { s['tick:' + it.command] = tick.checked; });
          row.appendChild(tick);
          row.appendChild(el('code', null, it.command));
          var cp = el('button', 'small', 'Copy'); cp.type = 'button';
          cp.addEventListener('click', function () { copyText(it.command, function (m) { msg.textContent = m; }); });
          row.appendChild(cp);
          c.appendChild(row);
          c.appendChild(el('p', it.warn ? 'warn' : 'meta', it.text || ''));
        });
        c.appendChild(msg);
        body.appendChild(c);
      });
    }
    // The text inputs the page's buttons take.
    ['snapshotName', 'commitMessage', 'force', 'packageNotes', 'vdFile', 'body', 'huesFile', 'note'].forEach(function (n) {
      if (used[n]) body.appendChild(textField(pid, n, (fields[n] || {}).label || n, (fields[n] || {}).type === 'check'));
    });
    (p.pcNotes || []).forEach(function (t) { body.appendChild(el('p', 'pconly', t)); });
    if (pid !== 'world' && pid !== 'golem' && pid !== 'diag' && pid !== 'mapart') {
      var rf = el('button', 'small', 'Refresh the lists'); rf.type = 'button';
      rf.addEventListener('click', function () { loadPage(pid, true); });
      body.appendChild(rf);
    }
    var grid = el('div', 'ctl-grid pagebtns');
    (p.buttons || []).forEach(function (b) {
      var cell = el('div', 'btncell');
      var btn = el('button', 'ctl' + (b.kind === 'dangerous' ? ' danger-btn' : '') + (b.pcOnly ? ' off' : ''));
      btn.type = 'button';
      btn.setAttribute('data-key', b.key);
      btn.appendChild(el('span', 'glyph', b.kind === 'read' ? 'R' : b.kind === 'dangerous' ? '!' : 'C'));
      btn.appendChild(el('span', 'label', b.label));
      var why = blockedWhy(p, b, data, a);
      btn.disabled = !!why || running;
      btn.addEventListener('click', function () { pressPage(p, b); });
      cell.appendChild(btn);
      if (why && (b.pcOnly || b.disabled || why.indexOf('Live') === 0)) cell.appendChild(el('p', 'pconly', why));
      grid.appendChild(cell);
    });
    if ((p.buttons || []).length) body.appendChild(grid);
    if (running) body.appendChild(el('p', 'quiet', 'Running: ' + job.label + '. One at a time.'));
  }

  function pageInputs(p, b) {
    var s = sel(p.id), out = {};
    (b.inputs || []).forEach(function (n) { if (s[n]) out[n] = s[n]; });
    return out;
  }

  // A page button: read ones run at a tap; the rest show the console's plan first (a dry run of it), then its phrase,
  // then (dangerous) a second confirm.
  function pressPage(p, b) {
    var inputs = pageInputs(p, b);
    if (b.kind === 'read') {
      post('/api/action', { key: b.key, inputs: inputs }, function (code, j) {
        if (code === 202) startedJob(p.title + ' > ' + b.label);
        else { $('job').hidden = false; $('job-title').textContent = b.label; clear($('job-lines')); $('job-result').textContent = (j && j.error) || ('refused (' + code + ')'); $('job-result').className = 'result amber'; }
      });
      return;
    }
    sheetMode = 'page';
    sheetPage = p;
    sheetFor = b;
    sheetGolem = null;
    sheetInputsFixed = inputs;
    resetSheet();
    $('sheet-title').textContent = p.title + ': ' + b.label;
    var fl = pagesDoc().fields || {};
    $('sheet-help').textContent = Object.keys(inputs).map(function (k) { return String((fl[k] || {}).label || k).replace(/ \(.*$/, '') + ': ' + inputs[k]; }).join(' | ');
    clear($('sheet-inputs'));
    $('sheet-msg').textContent = 'Reading the plan (a dry run of it: nothing is run)...';
    $('sheet-word').value = '';
    $('sheet-confirm').hidden = true;
    $('sheet-run').hidden = false;
    $('sheet-run').disabled = true;
    $('sheet').hidden = false;
    document.body.classList.add('sheet-open');
    post('/api/preview', { key: b.key, inputs: inputs }, function (code, j) {
      if (sheetFor !== b) return;
      var plan = clear($('sheet-plan'));
      plan.hidden = false;
      ((j && j.lines) || []).forEach(function (l) { plan.appendChild(el('span', 'ln ' + color(l.color), l.text + '\n')); });
      if (code !== 200 || !j || j.refused) {
        preview = { refused: true };
        $('sheet-msg').textContent = (j && (j.refused || j.error)) || ('refused (' + code + ')');
        $('sheet-run').hidden = true;
        return;
      }
      preview = j;
      $('sheet-msg').textContent = [j.preface, j.confirmText, j.asks].filter(function (x) { return x; }).join(' ');
      $('sheet-confirm').hidden = !j.expect;
      $('sheet-run').textContent = j.danger ? 'Next' : 'Run';
      checkWord();
      if (j.expect) $('sheet-word').focus();
    }, 125000);
  }

  // Run on a page sheet: a dangerous one goes to the second confirm first (what happens, to what, who is online).
  function runPage(second) {
    if (!preview || preview.refused) return;
    if (preview.danger && !second) {
      $('sheet-first').hidden = true;
      $('sheet-second').hidden = false;
      $('second-text').textContent = preview.dangerText || '';
      $('second-online').textContent = preview.online || '';
      $('second-online').hidden = !preview.online;
      $('second-asks').textContent = preview.asks || '';
      $('second-asks').hidden = !preview.asks;
      $('second-yes').textContent = 'Yes, ' + sheetFor.label.toLowerCase();
      $('second-yes').disabled = false;
      return;
    }
    var b = sheetFor, p = sheetPage;
    $('sheet-run').disabled = true; $('second-yes').disabled = true;
    $('sheet-msg').textContent = 'Starting...';
    post('/api/action', { key: b.key, typed: $('sheet-word').value, inputs: sheetInputsFixed || {}, token: preview.token, second: !!second }, function (code, j) {
      if (code === 202) { startedJob(p.title + ' > ' + b.label); return; }
      $('sheet-first').hidden = false; $('sheet-second').hidden = true;
      $('sheet-msg').textContent = (j && j.error) || ('refused (' + code + ')');
      $('second-yes').disabled = false;
      checkWord();
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
    drawGolem(s.golem, j.actions || null);
    drawControls(j);
    drawConsoleGrid();
    if ($('page').classList.contains('on') && $('sheet').hidden) drawPage();
    var p = j.panels || {};
    drawNeeds(p.needsYou, p.decisionsBuilt);
    drawTimeline(p.timeline);
    drawLinks(p.links);
    // For the phone-size check (tests/test_web_console.py): drawn, and whether anything is wider than the screen.
    document.documentElement.setAttribute('data-drawn', String((document.documentElement.getAttribute('data-drawn') | 0) + 1));
    document.documentElement.setAttribute('data-overflow', document.documentElement.scrollWidth > window.innerWidth + 1 ? 'yes' : 'no');
    document.documentElement.setAttribute('data-viewport', String(window.innerWidth));
    var sel0 = $('log-source');
    if (!sel0.options.length && j.logSources) {
      Object.keys(j.logSources).forEach(function (k) { var o = el('option', null, j.logSources[k]); o.value = k; sel0.appendChild(o); });
    }
  }

  function get(url, cb, timeoutMs) {
    var x = new XMLHttpRequest();
    x.open('GET', url, true);
    x.timeout = timeoutMs || 10000;
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
    $('sheet-form').addEventListener('submit', function (e) { e.preventDefault(); if (!$('sheet-run').disabled && !$('sheet-first').hidden) run(); });
    $('sheet-cancel').addEventListener('click', closeSheet);
    $('second-yes').addEventListener('click', function () { runPage(true); });
    $('second-back').addEventListener('click', function () { $('sheet-second').hidden = true; $('sheet-first').hidden = false; });
    $('sheet').addEventListener('click', function (e) { if (e.target === $('sheet')) closeSheet(); });
    $('job-close').addEventListener('click', function () { if (!jobTimer) $('job').hidden = true; });
    $('reader-close').addEventListener('click', function () { $('reader').hidden = true; });
    $('glow-help').addEventListener('click', function () {   // cc-P207
      var open = $('glow-legend').hidden;
      $('glow-legend').hidden = !open;
      $('glow-help').setAttribute('aria-expanded', open ? 'true' : 'false');
    });
    poll(false);
    setInterval(function () { poll(false); }, POLL_MS);
    setInterval(stepStrips, 50);
  });
  window.UOConsole = { draw: draw, golemState: golemState, golemCard: golemCard, last: function () { return last; }, applies: applies, wordFor: wordFor,
    pick: function (n) { picked = n; if (last) draw(last); } };
})();
