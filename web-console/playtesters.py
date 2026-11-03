"""cc-P773: the play testers, their tasks and their reports, as the shard sees them. One module for the web console's
Testers page (server.py) and the Play Test Goblin (scripts/playtest/goblin.py), so the two never disagree.

The files are in the test world's playtest folder, beside Saves, so a -Fresh start keeps them:
    D:/ShatteredLegacy/server/lib/uo/modernuo-test/playtest      (SL_PLAYTEST_DIR overrides it; tests use a scratch one)
  testers.json             {"version": 1, "testers": [{"account", "name", "paused", "addedAt"}]}   written here only
  tasks.json               {"version": 1, "tasks": [{"id": "G-0001", "account", "testId", "title", "why", "where",
                           "coords", "steps", "lookFor", "intro", "status": "open"|"pulled", "assignedAt", "pulledAt"}]}
                           written here (Pull back) and by the Goblin (new tasks); never deleted from
  playtest-reports.jsonl   one line per answer, appended by the shard (server/customizations/PlayTest/PlayTestTasks.cs);
                           only read here
  tester-accounts.json     cc-P789: each tester's account, whether it exists and its access level, written by the shard
                           only (PlayTestAccounts); read here and by the Goblin, so staff accounts get staff-safe tasks
  goblin-state.json        the Goblin's own state; "why" says, per account, what its last run gave or why not
The shard reads the first two and appends to the third. Every write here is whole and atomic (a temporary file, then a
rename) under playtest/.lock, which the Goblin and the page both take. None of it is ever committed: the world folder
is outside git (server/lib is ignored), and nothing here holds an address, a password or a Pass or Fail.

A task is finished when its tester's report names it with worked, wrong or couldnt. Testers only produce evidence:
nothing here marks a test passed or failed; Chase does that on the play-test page.

Standard library only, like server.py. ASCII only.
"""
import datetime
import json
import os
import re
import time

TESTERS = "testers.json"
TASKS = "tasks.json"
ACCOUNTS = "tester-accounts.json"
GOBLIN_STATE = "goblin-state.json"
REPORTS = "playtest-reports.jsonl"
LOCK = ".lock"
ANSWERS = ("worked", "wrong", "couldnt")
BUTTON_WORDS = {"worked": "Worked", "wrong": "Something was wrong", "couldnt": "Couldn't do it", "report": "Report"}
# A ModernUO account name as players type it at the login screen; the display name is free text, kept short.
ACCOUNT_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 ._-]{0,29}$")
NAME_MAX = 40
LOCK_STALE_SECONDS = 30


def default_dir(repo):
    """Where the shard's test world keeps them: SL_PLAYTEST_DIR, else <repo>/server/lib/uo/modernuo-test/playtest."""
    return os.environ.get("SL_PLAYTEST_DIR") or os.path.join(repo, "server", "lib", "uo", "modernuo-test", "playtest")


def now_iso():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


class PlayTestError(ValueError):
    """Something Chase asked for that cannot be done (an unknown account, a bad name, a broken file)."""


class PlayTestData:
    def __init__(self, folder, clock=now_iso):
        self.folder = folder
        self.clock = clock

    def path(self, name):
        return os.path.join(self.folder, name)

    # --- reading: never throws for a missing or broken file; says what is wrong instead -------------------------------
    def _read_json(self, name, key):
        p = self.path(name)
        if not os.path.exists(p):
            return [], ""
        try:
            with open(p, "r", encoding="utf-8-sig") as f:
                doc = json.load(f)
        except (OSError, ValueError) as e:
            return None, "%s cannot be read: %s" % (name, e)
        if not isinstance(doc, dict) or not isinstance(doc.get(key), list):
            return None, "%s has no %s list" % (name, key)
        return [x for x in doc[key] if isinstance(x, dict)], ""

    def testers(self):
        """(testers, error). A missing file is no testers; a broken one is None and why."""
        return self._read_json(TESTERS, "testers")

    def tasks(self):
        """(tasks, error), every task ever handed out."""
        return self._read_json(TASKS, "tasks")

    def accounts(self):
        """cc-P789: the shard's export, lower-case account -> {"account", "exists", "accessLevel", "staff"}. Missing or
        broken: empty (every tester's level unknown)."""
        try:
            with open(self.path(ACCOUNTS), "r", encoding="utf-8-sig") as f:
                doc = json.load(f)
        except (OSError, ValueError):
            return {}
        rows = doc.get("accounts") if isinstance(doc, dict) else None
        return {str(r.get("account", "")).lower(): r for r in (rows or []) if isinstance(r, dict) and r.get("account")}

    def accounts_written_at(self):
        try:
            with open(self.path(ACCOUNTS), "r", encoding="utf-8-sig") as f:
                return str(json.load(f).get("writtenAt", ""))
        except (OSError, ValueError, AttributeError):
            return ""

    def goblin_why(self):
        """The Goblin's last word per account (goblin-state.json "why"), and when it ran."""
        try:
            with open(self.path(GOBLIN_STATE), "r", encoding="utf-8-sig") as f:
                doc = json.load(f)
        except (OSError, ValueError):
            return {}, ""
        if not isinstance(doc, dict):
            return {}, ""
        why = doc.get("why") if isinstance(doc.get("why"), dict) else {}
        return {str(k).lower(): str(v) for k, v in why.items()}, str(doc.get("lastRun", ""))

    def reports(self):
        """Every readable report line, oldest first, each with its line number ("line"). Broken lines are skipped."""
        out = []
        try:
            with open(self.path(REPORTS), "r", encoding="utf-8-sig") as f:
                for n, line in enumerate(f, 1):
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        r = json.loads(line)
                    except ValueError:
                        continue
                    if isinstance(r, dict):
                        r["line"] = n
                        out.append(r)
        except OSError:
            pass
        return out

    # --- writing ------------------------------------------------------------------------------------------------------
    def lock(self):
        return _Lock(self.path(LOCK))

    def _write(self, name, key, items):
        os.makedirs(self.folder, exist_ok=True)
        p = self.path(name)
        tmp = p + ".tmp-%d" % os.getpid()
        with open(tmp, "w", encoding="utf-8", newline="\n") as f:
            json.dump({"version": 1, key: items}, f, indent=1, ensure_ascii=True)
            f.write("\n")
        os.replace(tmp, p)

    def write_tasks(self, tasks):
        """The Goblin's write (under the lock it already holds)."""
        self._write(TASKS, "tasks", tasks)

    def _testers_for_change(self):
        testers, err = self.testers()
        if testers is None:
            raise PlayTestError(err + ". Fix or remove the file before changing the list.")
        return testers

    def add_tester(self, account, name):
        account = (account or "").strip()
        name = " ".join((name or "").split())[:NAME_MAX] or account
        if not ACCOUNT_RE.match(account):
            raise PlayTestError("An account name is letters and digits (spaces, dots, dashes and underscores inside), "
                                "up to 30 characters: the name typed at the game's login screen.")
        with self.lock():
            testers = self._testers_for_change()
            if any(t.get("account", "").lower() == account.lower() for t in testers):
                raise PlayTestError("%s is already a tester." % account)
            rec = {"account": account, "name": name, "paused": False, "addedAt": self.clock()}
            testers.append(rec)
            self._write(TESTERS, "testers", testers)
        return rec

    def _change_tester(self, account, change):
        with self.lock():
            testers = self._testers_for_change()
            hit = [t for t in testers if t.get("account", "").lower() == (account or "").strip().lower()]
            if not hit:
                raise PlayTestError("No tester has the account %s." % account)
            testers = change(testers, hit[0])
            self._write(TESTERS, "testers", testers)
        return hit[0]

    def set_paused(self, account, paused):
        def change(testers, t):
            t["paused"] = bool(paused)
            return testers
        return self._change_tester(account, change)

    def remove_tester(self, account):
        """Off the list. Their tasks and reports stay (so a tester added back never repeats a test)."""
        return self._change_tester(account, lambda testers, t: [x for x in testers if x is not t])

    def pull_back(self, task_id):
        """An open task off its tester's list ("pulled"). The shard stops showing it at once."""
        with self.lock():
            tasks, err = self.tasks()
            if tasks is None:
                raise PlayTestError(err)
            hit = [t for t in tasks if t.get("id") == task_id]
            if not hit:
                raise PlayTestError("No task %s." % task_id)
            t = hit[0]
            if t.get("status", "open") != "open" or task_id in answered_ids(self.reports(), t.get("account", "")):
                raise PlayTestError("Task %s is not open: it was answered or pulled back already." % task_id)
            t["status"] = "pulled"
            t["pulledAt"] = self.clock()
            self.write_tasks(tasks)
        return t

    # --- the page's view ----------------------------------------------------------------------------------------------
    def view(self):
        """Every tester with their open, finished and pulled-back tasks and their reports, plus anything unreadable."""
        testers, terr = self.testers()
        tasks, kerr = self.tasks()
        reports = self.reports()
        tasks = tasks or []
        by_task = {}
        for r in reports:
            if r.get("task") and r.get("button") in ANSWERS:
                by_task.setdefault((str(r.get("account", "")).lower(), r["task"]), r)
        accounts = {str(t.get("account", "")).lower() for t in (testers or [])}
        levels = self.accounts()
        goblin, goblin_at = self.goblin_why()
        out = []
        for t in testers or []:
            acc = str(t.get("account", "")).lower()
            mine = [k for k in tasks if str(k.get("account", "")).lower() == acc]
            open_, done, pulled = [], [], []
            for k in mine:
                ans = by_task.get((acc, k.get("id")))
                row = dict(k)
                if ans:
                    row["answer"] = {"button": ans.get("button"), "words": BUTTON_WORDS.get(ans.get("button"), ""),
                                     "text": ans.get("text", ""), "at": ans.get("at", ""), "character": ans.get("character", "")}
                    done.append(row)
                elif k.get("status", "open") == "pulled":
                    pulled.append(row)
                else:
                    open_.append(row)
            mine_reports = [r for r in reports if str(r.get("account", "")).lower() == acc]
            lv = levels.get(acc)
            if lv is None:
                access = {"known": False, "staff": False, "level": "",
                          "note": "Access level not known yet: the test shard lists it within a few minutes of a tester "
                                  "being added. Until then only tasks that work on a staff account are given."}
            elif lv.get("staff"):
                access = {"known": True, "staff": True, "level": str(lv.get("accessLevel", "")),
                          "note": "Staff account: gets only tasks that work on a staff account, never player-only ones."}
            else:
                access = {"known": True, "staff": False, "level": str(lv.get("accessLevel", "Player")),
                          "note": "" if lv.get("exists", True) else "No such account on the test shard yet."}
            out.append({"account": t.get("account", ""), "name": t.get("name", ""), "paused": bool(t.get("paused")),
                        "addedAt": t.get("addedAt", ""), "open": open_, "finished": done, "pulled": pulled,
                        "reports": list(reversed(mine_reports)), "access": access, "goblin": goblin.get(acc, "")})
        others = [r for r in reports if str(r.get("account", "")).lower() not in accounts]
        return {"folder": self.folder, "testers": out, "testersError": terr, "tasksError": kerr,
                "goblinAt": goblin_at, "accountsAt": self.accounts_written_at(),
                "otherReports": len(others), "buttons": BUTTON_WORDS}


def answered_ids(reports, account):
    """The task ids this account answered with one of the three buttons."""
    acc = (account or "").lower()
    return {r.get("task") for r in reports
            if str(r.get("account", "")).lower() == acc and r.get("task") and r.get("button") in ANSWERS}


class _Lock:
    """playtest/.lock: made with O_EXCL, waited for up to 10 s, broken when older than LOCK_STALE_SECONDS (a writer that
    died). Both writers hold it only for one read and one write."""

    def __init__(self, path):
        self.path = path

    def __enter__(self):
        os.makedirs(os.path.dirname(self.path), exist_ok=True)
        deadline = time.time() + 10
        while True:
            try:
                fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                os.write(fd, str(os.getpid()).encode("ascii"))
                os.close(fd)
                return self
            except FileExistsError:
                try:
                    if time.time() - os.path.getmtime(self.path) > LOCK_STALE_SECONDS:
                        os.remove(self.path)
                        continue
                except OSError:
                    continue
                if time.time() > deadline:
                    raise PlayTestError("The play test files are busy (another change is being written). Try again.")
                time.sleep(0.1)

    def __exit__(self, *exc):
        try:
            os.remove(self.path)
        except OSError:
            pass
        return False
