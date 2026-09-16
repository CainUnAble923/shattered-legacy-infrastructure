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
# Its own Configuration copy, so a test run cannot rewrite the live server's config.
if [ ! -d "$TEST_CONF" ]; then
    echo "== seeding Configuration-test from the live Configuration =============="
    cp -r "$SERVER/uo/modernuo/Configuration" "$TEST_CONF"
    # The live config advertises 192.168.1.58. A client connecting to the TEST shard
    # would be relayed straight onto the LIVE shard and never know. Point it at itself.
    sed -i 's/"serverListing.address": *"[^"]*"/"serverListing.address": "127.0.0.1"/' "$TEST_CONF/modernuo.json"
    sed -i 's/"serverListing.serverName": *"[^"]*"/"serverListing.serverName": "Shattered Legacy TEST"/' "$TEST_CONF/modernuo.json"
fi

echo "== building through the gates (patches, then tests, then image) ========"
docker/uo/build.sh

echo "== starting the throwaway test shard on 127.0.0.1:2594 ================="
$COMPOSE up -d

echo
echo "  test shard up.   connect with:  Play-SL-Admin-TEST.bat"
echo "  live shard:      untouched"
echo "  test save:       $TEST_SAVES"
echo "  stop it with:    docker/uo/test-shard.sh --down"
