"""The morning report (cc-P91 section 6): report.md and email.txt in the run folder, from results.json.

    python -m uobot.report --out <run folder> [--rule1-changed sl-modernuo,...]

The bot never marks anything in play-tests.json: this report is evidence for Chase, who marks.
"""
import argparse
import json
import os
import sys


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
    mail = [head, "", head + ".", "Code under test: %s." % world.get("code", "?"), ""]
    for c in bad:
        last = c["runs"][-1] if c["runs"] else {}
        mail.append("%s %s (%d/%d): %s" % (c["result"], c["id"], c["passes"], len(c["runs"]), (last.get("evidence") or "")[:300]))
    if soak:
        mail.append("Soak: %d min, %d loud log lines, memory %s -> %s MB%s." % (
            soak["minutes"], soak.get("loudCount", 0), soak.get("memStartMB"), soak.get("memEndMB"),
            ("; " + "; ".join(soak["errors"])) if soak.get("errors") else ""))
    if a.rule1_changed:
        mail.append("RULE 1: these real shards changed during the run: " + a.rule1_changed)
    mail += ["", "Report: " + os.path.join(a.out, "report.md")]
    with open(os.path.join(a.out, "email.txt"), "w", encoding="ascii", errors="replace", newline="\n") as f:
        f.write("\n".join(mail) + "\n")
    print(head)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
