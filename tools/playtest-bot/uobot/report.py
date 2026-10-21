"""The morning report (cc-P91 section 6): report.md and email.txt in the run folder, from results.json.

    python -m uobot.report --out <run folder> [--rule1-changed sl-modernuo,...]

The bot never marks anything in play-tests.json: this report is evidence for Chase, who marks.
"""
import argparse
import json
import os
import sys

from uobot import mailshape


def load(path, default=None):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return default


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--rule1-changed", default="")
    ap.add_argument("--archive-note", default="")     # cc-P102: what the 14-day archive did, one line
    a = ap.parse_args(argv)
    res = load(os.path.join(a.out, "results.json"), {"checks": []})
    world = load(os.path.join(a.out, "world.json"), {})
    before = load(os.path.join(a.out, "real-shards-before.json"), {})
    after = load(os.path.join(a.out, "real-shards-after.json"), {})
    checks = res.get("checks", [])
    count = {k: sum(1 for c in checks if c["result"] == k) for k in ("PASS", "FAIL", "FLAKY", "ERROR")}
    stamp = os.path.basename(os.path.normpath(a.out))
    head = "Overnight play tests: %d pass, %d fail, %d flaky" % (count["PASS"], count["FAIL"], count["FLAKY"])
    if count["ERROR"]:
        head += ", %d could not run" % count["ERROR"]

    out = ["# Overnight play tests, %s" % stamp, "",
           "**%s** (%d checks, each run %s times; 3/3 PASS, 0/3 FAIL, anything else FLAKY)." % (
               head.split(": ", 1)[1], len(checks), max([len(c["runs"]) for c in checks] or [0])), "",
           "The bot produces evidence; it never marks a result. Chase marks on the play-test page.", "",
           "- Code under test: %s" % world.get("code", "(unknown)"),
           "- World: a copy of the test world's automatic backup %s, accounts wiped (TestAccountWipe), bot accounts only" % world.get("backup", "?"),
           "- testCenter.enable %s, testCenter.skillGainMultiplier %s, clusterf.skillGain.amountMultiplier %s (T-002 and T-031 depend on them)" % (
               world.get("testCenterEnable"), world.get("testCenterSkillGainMultiplier"), world.get("skillGainAmountMultiplier")),
           "- Rule 1 (live 2593 and test 2594 untouched): %s" % ("**CHANGED: %s**" % a.rule1_changed if a.rule1_changed else
                                                                 "held; both containers' state and start time identical before and after"),
           ""]
    out += ["| Id | Result | Runs passed | Evidence (last run) | Transcript | Left for Chase |", "|---|---|---|---|---|---|"]
    for c in checks:
        last = c["runs"][-1] if c["runs"] else {}
        ev = (last.get("evidence") or "").replace("|", "/").replace("\n", " ")
        if len(ev) > 300:
            ev = ev[:297] + "..."
        links = " ".join("[%d](%s)" % (r["run"], r["transcript"]) for r in c["runs"])
        out.append("| %s | %s | %d/%d | %s | %s | %s |" % (c["id"], c["result"], c["passes"], len(c["runs"]), ev, links,
                                                           c.get("left_for_chase") or ""))
    bad = [c for c in checks if c["result"] in ("FAIL", "FLAKY", "ERROR")]
    if bad:
        out += ["", "## Fail, Flaky and could-not-run, every run", ""]
        for c in bad:
            out.append("- **%s %s** (%s)" % (c["id"], c["result"], c["title"]))
            for r in c["runs"]:
                out.append("  - run %d %s: %s ([transcript](%s))" % (r["run"], r["verdict"].upper(), r["evidence"], r["transcript"]))
    soak = res.get("soak")
    if soak:
        out += ["", "## Soak", "",
                "%d minutes; activity %s." % (soak["minutes"], json.dumps(soak["events"])),
                "Memory %s MB at the start, %s MB at the end, %s MB at most." % (soak.get("memStartMB"), soak.get("memEndMB"), soak.get("memMaxMB")),
                "Loud log lines (Exception, Error, crash, save failures): %d." % soak.get("loudCount", 0)]
        if soak.get("errors"):
            out.append("Problems: " + "; ".join(soak["errors"]))
        if soak.get("loud"):
            out += ["", "```"] + soak["loud"][:40] + ["```"]
    out += ["", "Real shards before: `%s`" % json.dumps(before), "", "Real shards after: `%s`" % json.dumps(after), ""]
    with open(os.path.join(a.out, "report.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))
    # cc-P157: the Golem emails' shape: the night in words, what needs a look, What to do, then Details.
    details = [head + ".", "Code under test: %s." % world.get("code", "?"), ""]
    for c in bad:
        last = c["runs"][-1] if c["runs"] else {}
        details.append("%s %s (%d/%d): %s" % (c["result"], c["id"], c["passes"], len(c["runs"]), (last.get("evidence") or "")[:300]))
    if soak:
        details.append("Soak: %d min, %d loud log lines, memory %s -> %s MB%s." % (
            soak["minutes"], soak.get("loudCount", 0), soak.get("memStartMB"), soak.get("memEndMB"),
            ("; " + "; ".join(soak["errors"])) if soak.get("errors") else ""))
    if a.rule1_changed:
        details.append("RULE 1: these real shards changed during the run: " + a.rule1_changed)
    if a.archive_note:
        details.append(a.archive_note)
    details += ["", "Report: " + os.path.join(a.out, "report.md")]
    n = len(checks)
    parts = [w for w in ("%d passed" % count["PASS"] if count["PASS"] else "", "%d failed" % count["FAIL"] if count["FAIL"] else "",
                         "%d %s flaky" % (count["FLAKY"], "was" if count["FLAKY"] == 1 else "were") if count["FLAKY"] else "",
                         "%d could not run" % count["ERROR"] if count["ERROR"] else "") if w]
    said = parts[0] if len(parts) == 1 else ", ".join(parts[:-1]) + " and " + parts[-1] if parts else ""
    if not n:
        lead = "The overnight test bot ran no checks on the scratch shard."
    elif not bad:
        lead = "The overnight test bot ran %s on the scratch shard, and %s." % (
            "1 check" if n == 1 else "%d checks" % n, "it passed" if n == 1 else "all of them passed")
    else:
        lead = "The overnight test bot ran %d check%s on the scratch shard: %s." % (n, "" if n == 1 else "s", said)
    if a.rule1_changed:
        lead += " The live or test shard changed while it ran, which the bot must never cause."
    how = {"FAIL": "failed every run", "ERROR": "could not run"}
    look = ["%s (%s) %s" % (c["id"], c["title"], how.get(c["result"]) or "passed %d of %d runs" % (c["passes"], len(c["runs"])))
            for c in bad]
    if a.rule1_changed:
        do = "check the live and test shards first, then mark the checks above on the play-test page."
    elif bad:
        do = "look at the checks above on the play-test page and mark them."
    else:
        do = "Nothing to do."
    body = mailshape.shape(lead, do, [("Needs a look", look)], "\n".join(details))
    with open(os.path.join(a.out, "email.txt"), "w", encoding="ascii", errors="replace", newline="\n") as f:
        f.write(head + "\n\n" + body + "\n")
    print(head)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
