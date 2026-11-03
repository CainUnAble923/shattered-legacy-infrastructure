"""cc-P773: the Play Test Goblin. Run nightly on the game PC (scripts/Run-PlayTestGoblin.ps1, which also sends its email):

    python D:/ShatteredLegacy/scripts/playtest_goblin.py [--playtest-dir DIR] [--notes D:/UO/shard-migration]
                                                         [--bank scripts/playtest-task-bank.json] [--dry-run]

What one run does, in order:
  1. Reads the testers (web console's Testers page), every task ever handed out, and the reports the shard appended
     ([tasks buttons and [report). The files are the test world's playtest folder (web-console/playtesters.py).
  2. Writes a plain summary of what came in since the last run to <notes>/notes/goblin/YYYY-MM-DD.md, and a draft entry
     for each problem report to <notes>/notes/goblin/pending-pt.md, for Chase to file as a PT- row or throw away.
  3. Hands out new tasks from the task bank: each tester who is not paused gets up to 3 open tasks, never a card they had
     before (open, answered or pulled back). Cards no human has answered come first, then cards with conflicting
     answers (one Worked and one problem), then the rest; within a rank, cards handed out fewer times tonight first,
     then the bank's order (the Wakening Isle path). A card whose play-test item is already settled on the play-test
     page (pass, fail, moot or blocked) is not handed out. Each task gets a one-line Goblin intro; everything else is
     the card's plain words, with its test id.
  4. When something changed (new reports, or new tasks), writes an email for Chase to <playtest>/goblin-mail.txt
     (subject, a blank line, the body); Run-PlayTestGoblin.ps1 sends it and removes it. Nothing changed: no email.

cc-P789: staff accounts. A card says "needsPlayer": true when it needs a normal player account (a new character, the
tutorial, Young status, anything staff powers skip or change); a card that does not say is taken to need one. The
Goblin reads each tester's access level from tester-accounts.json, which the shard writes beside testers.json
(playtesters.PlayTestData.accounts), and never gives a player-only card to a staff account, or to an account the shard
has not listed yet. Such a tester gets none that night, and why is kept in goblin-state.json for the Play testers page.
Each run also copies needsPlayer from the bank onto tasks handed out before it existed. --pull-staff-player-only is the
one-time cleanup: it pulls back every open player-only task a staff tester holds, through the page's own Pull back.

It never marks a test Pass or Fail and never writes the play-test page's data (testing/play-tests.json is only read):
testers only produce evidence, and Chase alone marks results. The summaries and drafts quote the testers' words, so
notes/goblin/ is git-ignored in shard-migration: they stay on the game PC.

Standard library only. ASCII only. Exit 0 when it ran (even with nothing to do), 2 when a file it must change is broken.
"""
import argparse
import datetime
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(REPO, "web-console"))
import playtesters  # noqa: E402

CAP = 3
SETTLED = ("pass", "fail", "moot", "blocked")
MAIL = "goblin-mail.txt"
STATE = "goblin-state.json"
DEFAULT_NOTES = r"D:\UO\shard-migration"
DEFAULT_BANK = os.path.join(HERE, "playtest-task-bank.json")
# The Goblin's voice lives here and only here: one line above each task. Everything else is plain words.
INTROS = (
    "The Goblin sniffed this one out of a sand dune. It smells suspicious.",
    "The Goblin chewed on this one all night. Your turn.",
    "The Goblin swears this one works. The Goblin has been wrong before.",
    "The Goblin left this in your pack while you slept. Sorry about the crumbs.",
    "The Goblin bet a turnip that this one breaks. Prove it wrong.",
    "The Goblin wants to know. The Goblin always wants to know.",
    "The Goblin found this under a rock and thought of you.",
)
WORDS = playtesters.BUTTON_WORDS


def load_json(path, default=None):
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, ValueError):
        return default


def load_bank(path):
    doc = load_json(path)
    if not isinstance(doc, dict) or not isinstance(doc.get("cards"), list):
        raise SystemExit("The task bank %s cannot be read." % path)
    keys = set()
    for c in doc["cards"]:
        if not c.get("key") or c["key"] in keys:
            raise SystemExit("The task bank has a card with no key or a repeated key: %r" % c.get("key"))
        keys.add(c["key"])
    return doc["cards"]


def settled_tests(notes):
    """The play-test page's settled items (read only). A missing file settles nothing."""
    doc = load_json(os.path.join(notes, "testing", "play-tests.json"), {}) or {}
    return {i.get("id") for i in doc.get("items", []) if isinstance(i, dict) and i.get("status") in SETTLED}


def intro_for(task_id):
    return INTROS[int(hashlib.sha256(task_id.encode("ascii", "replace")).hexdigest(), 16) % len(INTROS)]


def next_id(tasks):
    n = 0
    for t in tasks:
        tid = str(t.get("id", ""))
        if tid.startswith("G-") and tid[2:].isdigit():
            n = max(n, int(tid[2:]))
    return n + 1


def card_answers(tasks, reports):
    """card key -> set of buttons answered on it, by anyone (the tasks say which card; the reports say the button)."""
    card_of = {(str(t.get("account", "")).lower(), t.get("id")): t.get("card") for t in tasks}
    out = {}
    for r in reports:
        if r.get("button") in playtesters.ANSWERS and r.get("task"):
            card = card_of.get((str(r.get("account", "")).lower(), r["task"]))
            if card:
                out.setdefault(card, set()).add(r["button"])
    return out


def rank(buttons):
    """0: no human has answered it; 1: answers conflict (Worked and a problem); 2: answered, and they agree."""
    if not buttons:
        return 0
    if "worked" in buttons and (buttons & {"wrong", "couldnt"}):
        return 1
    return 2


def needs_player(card):
    """True unless the card says it works on a staff account ("needsPlayer": false)."""
    return card.get("needsPlayer") is not False


def access_of(accounts, account):
    """"staff", "player" or "unknown" for a tester's account, from the shard's export (tester-accounts.json)."""
    rec = (accounts or {}).get(str(account).lower())
    if rec is None:
        return "unknown"
    if not rec.get("exists", True):
        return "player"   # not made yet: a new account is a player account when it is made at the login screen
    return "staff" if rec.get("staff") else "player"


STAFF_WHY = ("staff account (%s): only tasks that work on a staff account, and none are left in the bank for them. "
             "Player-only tasks go to player accounts.")
UNKNOWN_WHY = ("the shard has not listed this account's access level yet (tester-accounts.json, written by the test "
               "shard every few minutes), so only tasks that work on a staff account, and none are left")


def assign(testers, tasks, reports, cards, settled, clock, accounts=None):
    """The new tasks for tonight (appended to tasks in place) and why anyone got none. Pure but for clock.
    accounts: the shard's export by lower-case account; a staff or unlisted account gets only staff-safe cards."""
    live = [c for c in cards if not c.get("retired") and c.get("testId", "") not in settled]
    order = {c["key"]: i for i, c in enumerate(live)}
    answers = card_answers(tasks, reports)
    tonight = {}
    new, skipped = [], []
    why = {}
    nid = next_id(tasks)
    for tester in testers:
        acc = str(tester.get("account", ""))
        label = tester.get("name") or acc
        if tester.get("paused"):
            skipped.append("%s: paused" % label)
            why[acc.lower()] = "paused"
            continue
        access = access_of(accounts, acc)
        mine = [t for t in tasks if str(t.get("account", "")).lower() == acc.lower()]
        done = playtesters.answered_ids(reports, acc)
        open_now = [t for t in mine if t.get("status", "open") == "open" and t.get("id") not in done]
        room = CAP - len(open_now)
        if room <= 0:
            skipped.append("%s: already has %d open tasks" % (label, len(open_now)))
            why[acc.lower()] = "already has %d open tasks" % len(open_now)
            continue
        had = {t.get("card") for t in mine}
        pool = sorted((c for c in live if c["key"] not in had and (access == "player" or not needs_player(c))),
                      key=lambda c: (rank(answers.get(c["key"], set())), tonight.get(c["key"], 0), order[c["key"]]))
        if not pool:
            if access == "staff":
                reason = STAFF_WHY % ((accounts or {}).get(acc.lower(), {}).get("accessLevel") or "staff")
            elif access == "unknown":
                reason = UNKNOWN_WHY
            else:
                reason = "has done every card in the bank"
            skipped.append("%s: %s" % (label, reason))
            why[acc.lower()] = reason
            continue
        for c in pool[:room]:
            tid = "G-%04d" % nid
            nid += 1
            task = {"id": tid, "account": acc, "card": c["key"], "testId": c.get("testId", ""), "title": c.get("title", ""),
                    "why": c.get("why", ""), "where": c.get("where", ""), "coords": c.get("coords", ""),
                    "steps": list(c.get("steps", [])), "lookFor": c.get("lookFor", ""), "intro": intro_for(tid),
                    "needsPlayer": needs_player(c), "status": "open", "assignedAt": clock()}
            tasks.append(task)
            new.append(task)
            tonight[c["key"]] = tonight.get(c["key"], 0) + 1
        why[acc.lower()] = "got %d new task%s" % (min(room, len(pool)), "" if min(room, len(pool)) == 1 else "s")
    assign.why = why
    return new, skipped


def backfill(tasks, cards):
    """Copies needsPlayer from the bank onto tasks handed out before the field existed. Returns how many changed."""
    by_key = {c["key"]: c for c in cards}
    n = 0
    for t in tasks:
        if "needsPlayer" not in t and t.get("card") in by_key:
            t["needsPlayer"] = needs_player(by_key[t["card"]])
            n += 1
    return n


def pull_staff_player_only(playtest_dir, bank_path, dry_run=False):
    """The one-time cleanup: every open player-only task held by a staff tester, pulled back with the page's own Pull
    back (status "pulled", pulledAt), so the history stays. Returns the task ids (would be) pulled."""
    data = playtesters.PlayTestData(playtest_dir)
    testers, terr = data.testers()
    if testers is None:
        raise _Broken(terr)
    cards = {c["key"]: c for c in load_bank(bank_path)}
    accounts = data.accounts()
    tasks, kerr = data.tasks()
    if tasks is None:
        raise _Broken(kerr)
    reports = data.reports()
    pulled = []
    for tester in testers:
        acc = str(tester.get("account", ""))
        if access_of(accounts, acc) != "staff":
            continue
        done = playtesters.answered_ids(reports, acc)
        for t in tasks:
            if (str(t.get("account", "")).lower() == acc.lower() and t.get("status", "open") == "open"
                    and t.get("id") not in done):
                card = cards.get(t.get("card"), {})
                np = t.get("needsPlayer") if "needsPlayer" in t else needs_player(card)
                if np is not False:
                    pulled.append(t["id"])
    if not dry_run:
        for tid in pulled:
            data.pull_back(tid)
    return pulled


def describe_report(r, task, tester_name):
    where = "%s %s, %s, %s" % (r.get("facet", ""), r.get("x", ""), r.get("y", ""), r.get("z", ""))
    if task:
        head = '%s, task %s "%s" (test %s)' % (tester_name, task.get("id"), task.get("title", ""), task.get("testId", ""))
    else:
        head = "%s, a free report" % tester_name
    words = r.get("text") or "(no words)"
    return '%s: %s: "%s". Character %s at %s, %s, shard version %s.' % (
        head, WORDS.get(r.get("button"), r.get("button")), words, r.get("character", ""), where, r.get("at", ""),
        r.get("version") or "unknown")


def pt_draft(r, task, tester_name):
    lines = ["### Draft PT-? from %s's report of %s" % (tester_name, r.get("at", "")), ""]
    if task:
        lines.append("- Test: %s (task %s, \"%s\")" % (task.get("testId", ""), task.get("id"), task.get("title", "")))
        lines.append("- What they were asked to look for: %s" % task.get("lookFor", ""))
    else:
        lines.append("- Test: none (a free report with [report)")
    lines += ["- Button: %s" % WORDS.get(r.get("button"), r.get("button")),
              "- Their words: \"%s\"" % (r.get("text") or "(no words)"),
              "- Character %s on account %s, at %s %s, %s, %s" % (r.get("character", ""), r.get("account", ""), r.get("facet", ""),
                                                                r.get("x", ""), r.get("y", ""), r.get("z", "")),
              "- Shard version: %s" % (r.get("version") or "unknown"),
              "- File it as a PT- row, or delete this draft. A tester's report is evidence, not a result.", ""]
    return "\n".join(lines)


def run(playtest_dir, notes, bank_path, today, clock=playtesters.now_iso, dry_run=False):
    """One nightly run. Returns a dict: newReports, newTasks, skipped, summary (path), mail (path or "")."""
    data = playtesters.PlayTestData(playtest_dir, clock=clock)
    testers, terr = data.testers()
    if testers is None:
        raise _Broken(terr)
    cards = load_bank(bank_path)
    settled = settled_tests(notes)
    state_path = os.path.join(playtest_dir, STATE)
    state = load_json(state_path, {}) or {}
    with data.lock():
        tasks, kerr = data.tasks()
        if tasks is None:
            raise _Broken(kerr)
        reports = data.reports()
        seen = int(state.get("reportsSeen", 0) or 0)
        if reports and reports[-1]["line"] < seen:
            seen = 0   # the reports file was replaced: everything in it is new to the Goblin
        fresh = [r for r in reports if r["line"] > seen]
        filled = backfill(tasks, cards)
        new, skipped = assign(testers, tasks, reports, cards, settled, clock, data.accounts())
        why = assign.why
        if (new or filled) and not dry_run:
            data.write_tasks(tasks)
    names = {str(t.get("account", "")).lower(): (t.get("name") or t.get("account")) for t in testers}
    task_of = {(str(t.get("account", "")).lower(), t.get("id")): t for t in tasks}

    lines = ["# Play Test Goblin, %s" % today, "",
             "Written by scripts/playtest_goblin.py (cc-P773). Testers only report what they saw; you mark Pass or Fail on "
             "the play-test page. Nothing here is a result.", ""]
    lines.append("## New reports since the last run (%d)" % len(fresh))
    lines.append("")
    drafts = []
    for r in fresh:
        acc = str(r.get("account", "")).lower()
        name = "%s (account %s)" % (names.get(acc, r.get("account", "")), r.get("account", ""))
        task = task_of.get((acc, r.get("task"))) if r.get("task") else None
        lines.append("- " + describe_report(r, task, name))
        if r.get("button") in ("wrong", "couldnt", "report"):
            drafts.append(pt_draft(r, task, name))
    if not fresh:
        lines.append("- None.")
    lines += ["", "## Tasks handed out tonight (%d)" % len(new), ""]
    for t in new:
        lines.append('- %s to %s: "%s" (test %s, card %s)' % (t["id"], names.get(t["account"].lower(), t["account"]),
                                                            t["title"], t["testId"], t["card"]))
    if not new:
        lines.append("- None.")
    lines += ["", "## Nobody new for", ""] + ["- " + s for s in skipped] + ([] if skipped else ["- Everyone got tasks."])
    lines += ["", "## Open now", ""]
    for t in testers:
        acc = str(t.get("account", ""))
        done = playtesters.answered_ids(reports, acc)
        open_now = [k for k in tasks if str(k.get("account", "")).lower() == acc.lower()
                    and k.get("status", "open") == "open" and k.get("id") not in done]
        lines.append("- %s: %s" % (t.get("name") or acc, ", ".join("%s %s" % (k["id"], k.get("title", "")) for k in open_now) or "none"))
    if not testers:
        lines.append("- No testers yet: add one on the web console's Play testers page.")
    if dry_run:
        lines += ["", "(Dry run: nothing was written but this summary.)"]
    lines.append("")

    gob = os.path.join(notes, "notes", "goblin")
    os.makedirs(gob, exist_ok=True)
    summary = os.path.join(gob, "%s.md" % today)
    with open(summary, "a" if os.path.exists(summary) else "w", encoding="utf-8", newline="\n") as f:
        if f.tell():
            f.write("\n---\n\n")
        f.write("\n".join(lines))
    if drafts and not dry_run:
        pend = os.path.join(gob, "pending-pt.md")
        head = not os.path.exists(pend)
        with open(pend, "a", encoding="utf-8", newline="\n") as f:
            if head:
                f.write("# Draft PT- entries from play testers' reports\n\nThe Play Test Goblin adds one draft per problem "
                        "report. File each in the playtest tracker with a real PT- number, or delete it. Nothing here is a "
                        "Pass or Fail.\n\n")
            f.write("\n".join(drafts) + "\n")

    mail = ""
    if (fresh or new) and not dry_run:
        mail = os.path.join(playtest_dir, MAIL)
        subject = "Play testers: %s" % ", ".join(x for x in (
            ("%d new report%s" % (len(fresh), "" if len(fresh) == 1 else "s")) if fresh else "",
            ("%d new task%s handed out" % (len(new), "" if len(new) == 1 else "s")) if new else "") if x)
        body = ["Here is what the play testers sent since last night, and what they were given for today.", ""]
        body += [l[3:] + ":" if l.startswith("## ") else l for l in lines[4:]]
        body += ["", "Nothing to do unless a report needs you. Draft play-test entries for the problem reports are on the "
                 "game PC with tonight's summary. You mark Pass or Fail on the play-test page; testers only report."]
        with open(mail, "w", encoding="utf-8", newline="\n") as f:
            f.write(subject + "\n\n" + "\n".join(l for l in body).strip() + "\n")
    if not dry_run:
        os.makedirs(playtest_dir, exist_ok=True)
        with open(state_path, "w", encoding="utf-8", newline="\n") as f:
            json.dump({"reportsSeen": reports[-1]["line"] if reports else 0, "lastRun": clock(), "why": why}, f, indent=1)
            f.write("\n")
    return {"newReports": fresh, "newTasks": new, "skipped": skipped, "summary": summary, "mail": mail}


class _Broken(Exception):
    pass


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--playtest-dir", default=playtesters.default_dir(REPO))
    ap.add_argument("--notes", default=DEFAULT_NOTES, help="the shard-migration folder (play-tests.json read; notes/goblin written)")
    ap.add_argument("--bank", default=DEFAULT_BANK)
    ap.add_argument("--dry-run", action="store_true", help="write only the summary: no tasks, drafts, mail or state")
    ap.add_argument("--pull-staff-player-only", action="store_true",
                    help="the one-time cleanup: pull back the player-only tasks staff testers hold (with --dry-run: list them)")
    a = ap.parse_args(argv)
    today = datetime.datetime.now().strftime("%Y-%m-%d")
    if a.pull_staff_player_only:
        try:
            ids = pull_staff_player_only(a.playtest_dir, a.bank, dry_run=a.dry_run)
        except (_Broken, playtesters.PlayTestError) as e:
            print("The Goblin stopped: %s. Nothing was pulled back." % e)
            return 2
        print("%s %d player-only task%s from staff testers: %s" % ("Would pull back" if a.dry_run else "Pulled back",
                                                                  len(ids), "" if len(ids) == 1 else "s",
                                                                  ", ".join(ids) or "none"))
        return 0
    try:
        out = run(a.playtest_dir, a.notes, a.bank, today, dry_run=a.dry_run)
    except (_Broken, playtesters.PlayTestError) as e:
        print("The Goblin stopped: %s. Nothing was handed out." % e)
        return 2
    print("%d new reports, %d new tasks. Summary: %s%s" % (len(out["newReports"]), len(out["newTasks"]), out["summary"],
                                                          ("; email: " + out["mail"]) if out["mail"] else "; no email"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
