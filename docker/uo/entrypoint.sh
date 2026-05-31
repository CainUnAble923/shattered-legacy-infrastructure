#!/usr/bin/env bash
set -euo pipefail

SERVER_DIR="${SERVER_DIR:-/modernuo}"

cd "$SERVER_DIR"

# Wait briefly for any volume mounts to settle
sleep 2

exec dotnet ModernUO.dll
