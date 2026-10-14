#!/bin/sh
# cc-P107: keeps read-only clones of both repos current for the ops pages on Haven (docker/ops/docker-compose.yml).
# Every PULL_SECONDS: fetch origin main, reset hard to it, and write /repo/stamp-<name>.txt as
# "<commit> <commit time>", which the page shows as "repo as of". A failed fetch leaves the clone as it was, so the
# page keeps serving the last good notes and says how old they are. Nothing here ever pushes. ASCII only.
set -u

pull() {
    name=$1
    url=$2
    dir=/repo/$name
    if [ ! -d "$dir/.git" ]; then
        rm -rf "$dir.tmp"
        if ! git clone --quiet --depth 1 --branch main "$url" "$dir.tmp"; then
            echo "$(date -u +%FT%TZ) clone of $name FAILED (for shard-migration: is the deploy key added on GitHub?)"
            rm -rf "$dir.tmp"
            return 1
        fi
        mv "$dir.tmp" "$dir"
    fi
    if git -C "$dir" fetch --quiet --depth 1 origin main && git -C "$dir" reset --quiet --hard FETCH_HEAD; then
        printf '%s %s\n' "$(git -C "$dir" rev-parse HEAD)" "$(git -C "$dir" log -1 --format=%cI)" > "/repo/stamp-$name.txt.tmp"
        mv "/repo/stamp-$name.txt.tmp" "/repo/stamp-$name.txt"
    else
        echo "$(date -u +%FT%TZ) fetch of $name FAILED; keeping the clone as it was"
        return 1
    fi
}

git config --global --add safe.directory '*'
while true; do
    pull shard-migration "$SHARD_MIGRATION_URL"
    pull shattered-legacy "$SHATTERED_LEGACY_URL"
    sleep "${PULL_SECONDS:-120}"
done
