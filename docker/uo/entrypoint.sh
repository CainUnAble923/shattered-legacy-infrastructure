#!/usr/bin/env bash
set -euo pipefail

echo "[SL] Starting Shattered Legacy ModernUO..."
echo "[SL] Commit: 7c9215d97  .NET: $(dotnet --version)"

# Overlay bind-mounted Projects (live scripts) into the Distribution tree
# so hot-edits on the host are picked up on next server restart
if [ -d "/modernuo-projects" ]; then
    echo "[SL] Syncing Projects from bind mount..."
    cp -r /modernuo-projects/. /modernuo/Projects/ 2>/dev/null || true
fi

cd /modernuo

exec dotnet ModernUO.dll
