// cc-P773: the Testers page. Reads /api/testers, posts the four changes (add, pause, remove, pull back) with the console's
// action header, and draws each tester with their open, finished and pulled-back tasks and their reports. ASCII only.
(function () {
  "use strict";
  var $ = function (id) { return document.getElementById(id); };

  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  function state(text, bad) { var s = $("state"); s.textContent = text; s.className = "state" + (bad ? " err" : ""); }

  function post(path, body) {
    return fetch(path, {
      method: "POST", credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-UOConsole-Action": "1" },
      body: JSON.stringify(body)
    }).then(function (r) {
      return r.json().catch(function () { return {}; }).then(function (j) {
        if (!r.ok) { throw new Error(j.error || ("HTTP " + r.status)); }
        return j;
      });
    });
  }

  function act(path, body, done) {
    state("saving...");
    post(path, body).then(function () { state(done); load(); }).catch(function (e) { state(e.message, true); });
  }

  function taskRow(t, withPull) {
    var cells = "<td>" + esc(t.id) + "</td><td>" + esc(t.testId) + "</td><td>" + esc(t.title) + "</td><td>" + esc(t.assignedAt) + "</td>";
    if (t.answer) {
      cells += "<td>" + esc(t.answer.words) + (t.answer.text ? ": " + esc(t.answer.text) : "") +
        " <span class='muted'>(" + esc(t.answer.character) + ", " + esc(t.answer.at) + ")</span></td>";
    } else if (withPull) {
      cells += "<td><button class='small' data-pull='" + esc(t.id) + "'>Pull back</button></td>";
    } else {
      cells += "<td class='muted'>pulled back " + esc(t.pulledAt || "") + "</td>";
    }
    return "<tr>" + cells + "</tr>";
  }

  function table(title, rows, empty) {
    if (!rows.length) { return "<p class='muted'>" + esc(title) + ": " + esc(empty) + "</p>"; }
    return "<h3>" + esc(title) + "</h3><table><tr><th>Task</th><th>Test</th><th>Title</th><th>Given</th><th></th></tr>" +
      rows.join("") + "</table>";
  }

  function draw(d) {
    var problems = [d.testersError, d.tasksError].filter(Boolean);
    $("problems").textContent = problems.join(" ");
    if (!d.testers.length) {
      $("list").innerHTML = "<p class='muted'>No testers yet. Add one above by their account name.</p>";
      return;
    }
    $("list").innerHTML = d.testers.map(function (t) {
      var reports = t.reports.map(function (r) {
        return "<tr><td>" + esc(r.at) + "</td><td>" + esc(d.buttons[r.button] || r.button) + "</td><td>" + esc(r.task || "") +
          "</td><td>" + esc(r.text) + "</td><td class='muted'>" + esc(r.character) + ", " + esc(r.facet) + " " +
          esc(r.x) + ", " + esc(r.y) + ", " + esc(r.z) + "</td></tr>";
      });
      return "<div class='tester'><h2>" + esc(t.name || t.account) + " <span class='muted'>(account " + esc(t.account) + ")</span>" +
        (t.paused ? " <span class='paused'>paused</span>" : "") + "</h2>" +
        "<button class='small' data-pause='" + esc(t.account) + "' data-paused='" + (t.paused ? "0" : "1") + "'>" +
        (t.paused ? "Resume" : "Pause") + "</button> " +
        "<button class='small' data-remove='" + esc(t.account) + "'>Remove</button>" +
        table("Open tasks", t.open.map(function (k) { return taskRow(k, true); }), "none") +
        table("Finished tasks", t.finished.map(function (k) { return taskRow(k, false); }), "none yet") +
        (t.pulled.length ? table("Pulled back", t.pulled.map(function (k) { return taskRow(k, false); }), "") : "") +
        (reports.length ? "<h3>Reports</h3><table><tr><th>When (UTC)</th><th>Button</th><th>Task</th><th>Words</th><th>Where</th></tr>" +
          reports.join("") + "</table>" : "<p class='muted'>Reports: none yet</p>") +
        "</div>";
    }).join("");
  }

  function load() {
    fetch("/api/testers", { credentials: "same-origin" }).then(function (r) {
      if (!r.ok) { throw new Error("HTTP " + r.status); }
      return r.json();
    }).then(function (d) { draw(d); state("up to date"); }).catch(function (e) { state("could not load: " + e.message, true); });
  }

  document.addEventListener("click", function (ev) {
    var b = ev.target;
    if (b.dataset.pull && confirm("Pull back task " + b.dataset.pull + "? It leaves the tester's list in game at once.")) {
      act("/api/testers/pullback", { id: b.dataset.pull }, "pulled back");
    } else if (b.dataset.pause) {
      act("/api/testers/pause", { account: b.dataset.pause, paused: b.dataset.paused === "1" }, "saved");
    } else if (b.dataset.remove && confirm("Remove " + b.dataset.remove + " from the testers? Their tasks and reports stay.")) {
      act("/api/testers/remove", { account: b.dataset.remove }, "removed");
    }
  });

  $("add").addEventListener("submit", function (ev) {
    ev.preventDefault();
    act("/api/testers/add", { account: $("account").value.trim(), name: $("name").value.trim() }, "added");
    $("account").value = ""; $("name").value = "";
  });

  load();
  setInterval(load, 60000);
})();
