#!/usr/bin/env bash
set -euo pipefail

echo "[SL] Starting Shattered Legacy ModernUO..."
# SL_MODERNUO_COMMIT is the Dockerfile's MODERNUO_COMMIT build ARG, set in the runtime stage.
echo "[SL] Commit: ${SL_MODERNUO_COMMIT:-unknown}  .NET: $(dotnet --version)"

# Overlay bind-mounted Projects (live scripts) into the Distribution tree
# so hot-edits on the host are picked up on next server restart
if [ -d "/modernuo-projects" ]; then
    echo "[SL] Syncing Projects from bind mount..."
    cp -r /modernuo-projects/. /modernuo/Projects/ 2>/dev/null || true
fi

cd /modernuo

# ModernUO expects UOContent.dll in ./Assemblies/ but dotnet publish puts it flat.
# Create the Assemblies directory and symlink it in.
mkdir -p /modernuo/Assemblies
for dll in /modernuo/*.dll; do
    base=$(basename "$dll")
    target="/modernuo/Assemblies/$base"
    if [ ! -e "$target" ]; then
        ln -s "$dll" "$target"
    fi
done

exec dotnet ModernUO.dll
