#!/usr/bin/env bash
# Build the image through the gates, then bring up the THROWAWAY test shard.
# Never touches the live container, the live save, or DNS.
#
#   docker/uo/test-shard.sh              build + up
#   docker/uo/test-shard.sh --fresh      wipe the test save first
#   docker/uo/test-shard.sh --down       stop it
set -euo pipefail

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$REPO_ROOT"
COMPOSE="docker compose -f docker/uo/docker-compose.test.yml"

if [ "${1:-}" = "--down" ]; then $COMPOSE down; echo "test shard stopped."; exit 0; fi

SERVER=server
TEST_SAVES="$SERVER/lib/uo/modernuo/Saves-test"
TEST_CONF="$SERVER/uo/modernuo/Configuration-test"

if [ "${1:-}" = "--fresh" ] && [ -d "$TEST_SAVES" ]; then
    echo "== wiping the test save (the live save is never touched) ==============="
    rm -rf "$TEST_SAVES"
fi

mkdir -p "$TEST_SAVES"
# A fresh, empty world has no accounts, so ModernUO's AccountPrompt asks "create the
# owner account? (y/n)" on stdin and BLOCKS THERE FOREVER. The container looks healthy
# (docker ps says Up), the listener never opens, and a client hangs at "verifying
# account" with no clue why. Cost a week, 2026-09-19. Seed the accounts so it never asks.
if [ ! -d "$TEST_SAVES/Accounts" ] && [ -d "$SERVER/lib/uo/modernuo/Saves/Accounts" ]; then
    echo "== seeding test accounts from the live save (one-directional copy) ====="
    cp -r "$SERVER/lib/uo/modernuo/Saves/Accounts" "$TEST_SAVES/Accounts"
fi
# D19: Accounts.bin names each account's type by a hash that only SerializedTypes.db can
# resolve. Without it every account is skipped silently, Accounts.Count is 0, and the
# prompt above blocks anyway. Seed the type table with the accounts.
if [ ! -f "$TEST_SAVES/SerializedTypes.db" ] && [ -f "$SERVER/lib/uo/modernuo/Saves/SerializedTypes.db" ]; then
    cp "$SERVER/lib/uo/modernuo/Saves/SerializedTypes.db" "$TEST_SAVES/SerializedTypes.db"
fi
# Its own Configuration copy, so a test run cannot rewrite the live server's config.
if [ ! -d "$TEST_CONF" ]; then
    echo "== seeding Configuration-test from the live Configuration =============="
    cp -r "$SERVER/uo/modernuo/Configuration" "$TEST_CONF"
fi
# Listen on 2594 inside the container and advertise the LAN address, so the relay packet
# says 192.168.1.58:2594: this shard, never the live one on 2593. docker-compose.test.yml
# explains why the ports must match. Applied every run, so an older 2593 copy migrates.
sed -i 's/"0\.0\.0\.0:2593"/"0.0.0.0:2594"/' "$TEST_CONF/modernuo.json"
sed -i 's/"serverListing.address": *\("[^"]*"\|null\)/"serverListing.address": "192.168.1.58"/' "$TEST_CONF/modernuo.json"
sed -i 's/"serverListing.serverName": *"[^"]*"/"serverListing.serverName": "Shattered Legacy TEST"/' "$TEST_CONF/modernuo.json"
if ! grep -q '"0.0.0.0:2594"' "$TEST_CONF/modernuo.json" || grep -q ':2593"' "$TEST_CONF/modernuo.json"; then
    echo "Configuration-test/modernuo.json does not listen on 0.0.0.0:2594 only. Fix it by hand; not starting." >&2
    exit 1
fi

echo "== building through the gates (patches, then tests, then image) ========"
docker/uo/build.sh

echo "== starting the throwaway test shard on port 2594 (LAN) ==============="
$COMPOSE up -d

echo
echo "  test shard up.   connect with:  Play-SL-Admin-TEST.bat"
echo "  live shard:      untouched"
echo "  test save:       $TEST_SAVES"
echo "  stop it with:    docker/uo/test-shard.sh --down"
