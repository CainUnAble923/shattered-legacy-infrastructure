"""cc-P215: runs uobot.golemcheck from any folder (the readiness check, D:\\UO\\golem\\Test-GolemReady.ps1, calls it by path).

    python -P D:\\ShatteredLegacy\\tools\\playtest-bot\\golemcheck.py login --host 127.0.0.1 --port 2594 --golem Gold
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uobot.golemcheck import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
