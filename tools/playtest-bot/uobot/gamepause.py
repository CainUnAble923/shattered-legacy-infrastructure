"""cc-P819 (Chase 11-07: "pause bot play testing when im playing other games"): the bot's side of the game pause.

The check itself is the Golem kit's (D:/UO/golem/Test-GameRunning.ps1, over GamePause.ps1: GolemPower's game detection,
our own UO clients left out, settings.json gamePause, the game PC only). This module only runs it:

    now(script)                 -> (playing, line): one look; line is "waiting: Chase is playing <game>" or why not
    wait(script, until, ...)    -> True to go on, False when it was still running at `until` (the night is over)

Run-OvernightPlaytests.ps1 waits before it starts anything; uobot.run (--game-check, --game-until) looks again before
each check and every soak minute, and on a game finishes the step, closes its clients and waits the same way. A
missing script (another PC, an older kit) or a check that cannot run means no pause: the bot goes on, as before.
"""
import os
import subprocess
import sys

DEFAULT_SCRIPT = r"D:\UO\golem\Test-GameRunning.ps1"


def _cmd(script, *args):
    return ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script] + [str(a) for a in args]


def available(script):
    """'' or 'none' (the runner's -NoGameWait: PowerShell 5.1 drops an empty argument) turns the look off."""
    return bool(script) and script.lower() != "none" and os.path.isfile(script)


def now(script):
    """One look. (True, line) while a game runs on the game PC with the pause on; (False, line) otherwise."""
    if not available(script):
        return False, "no game check here (%s missing)" % (script or "none")
    try:
        r = subprocess.run(_cmd(script), capture_output=True, text=True, timeout=120)
    except Exception as e:  # a check that cannot run never stops the bot
        return False, "the game check failed (%s: %s); going on" % (type(e).__name__, e)
    line = (r.stdout or "").strip().splitlines()[-1] if (r.stdout or "").strip() else ""
    return r.returncode == 1, line


def wait(script, until, interval=600, who="overnight", what="the overnight play tests", say=print):
    """Waits while a game runs (the kit's -Wait), each of its lines passed to say(). True: go on. False: skip."""
    if not available(script):
        return True
    args = ["-Wait", "-IntervalSeconds", interval, "-Who", who, "-What", what]
    if until:
        args += ["-Until", until]
    try:
        p = subprocess.Popen(_cmd(script, *args), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        for line in p.stdout:
            line = line.rstrip()
            if line:
                say(line)
        p.wait()
    except Exception as e:
        say("the game wait failed (%s: %s); going on" % (type(e).__name__, e))
        return True
    return p.returncode == 0


if __name__ == "__main__":  # python -m uobot.gamepause [script]: one look, for a person
    playing, line = now(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SCRIPT)
    print(line)
    sys.exit(1 if playing else 0)
