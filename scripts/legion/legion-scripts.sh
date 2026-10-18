#!/bin/bash
# cc-P144. Runs on Haven, called by Publish-LegionScripts.ps1 (scripts\legion\) over SSH, or on this PC in Git
# Bash against a scratch target (-Scratch), with the same steps.
#
#   legion-scripts.sh publish <channel> <scripts.json sha256>
#       $STAGE holds the new scripts.json, files.txt and the changed .py files.
#   legion-scripts.sh promote <scripts.json sha256> <name>...
#       $STAGE holds the new official scripts.json and files.txt; the named .py files are copied from the dev
#       folder, so what goes out is the exact dev bytes, not anything on a PC's disk.
#
# files.txt is every file the new scripts.json lists, in sha256sum's format ("<sha256>  <name>"). Order, stopping
# at the first failure:
#   1. every staged file matches files.txt (and scripts.json its sha256). DRYRUN=1 stops here.
#   2. back up the served folder (each .py and scripts.json) to $BAK_ROOT/<old version>/ (official) or
#      $BAK_ROOT/dev/<old version>/ (dev).
#   3. put the new and changed .py files in place; download EVERY listed file back over HTTP and compare.
#      A mismatch puts the backup back; scripts.json was never touched.
#   4. only then put scripts.json in place and download it back. A mismatch puts back the old scripts.json AND
#      the old files. The order is the point: a client must never read a manifest that describes files not yet
#      served (the same rule as the player package's version.json).
#   5. a .py the new scripts.json no longer lists is removed from the served folder, last. It stays in the backup.
# The served official folder is root-owned (Docker made it), so a throwaway container does those writes there;
# a folder this user can write (the dev folder, a scratch target) is written directly.
set -u
MODE="${1:-}"
STAGE="${STAGE:-$HOME/legion-staging}"
BAK_ROOT="${BAK_ROOT:-$HOME/backups/legion-scripts}"
DEV_DIR="${DEV_DIR:-$HOME/sl-scripts-dev/html}"
DEV_URL="${DEV_URL:-http://127.0.0.1:8097}"
DEV_RESOLVE="${DEV_RESOLVE:-}"
OFFICIAL_URL="${OFFICIAL_URL:-https://get.shatteredlegacyuo.com/scripts}"
OFFICIAL_RESOLVE="${OFFICIAL_RESOLVE---resolve get.shatteredlegacyuo.com:443:127.0.0.1}"
DRYRUN="${DRYRUN:-0}"
# PowerShell cannot pass an empty variable, so the scratch target says none for "no --resolve".
[ "$OFFICIAL_RESOLVE" = "none" ] && OFFICIAL_RESOLVE=""
[ "$DEV_RESOLVE" = "none" ] && DEV_RESOLVE=""
TS=$(date +%Y%m%d-%H%M%S)
JSON=scripts.json

fail() { echo "STOPPED: $1"; exit 1; }
listed() { awk -v n="$1" '$2 == n { f = 1 } END { exit !f }' "$STAGE/files.txt"; }

if [ -z "${OFFICIAL_DIR:-}" ]; then
  # get.shatteredlegacyuo.com is sl-downloads (nginx); its html folder is wherever Docker mounted it.
  GETDIR=$(docker inspect sl-downloads --format '{{range .Mounts}}{{if eq .Destination "/usr/share/nginx/html"}}{{.Source}}{{end}}{{end}}' 2>/dev/null)
  [ -n "$GETDIR" ] || fail "could not find sl-downloads' html folder (docker inspect sl-downloads)"
  OFFICIAL_DIR="$GETDIR/scripts"
fi

case "$MODE" in
  publish)
    CHANNEL="${2:-}"; WANT_JSON="${3:-}"
    [ "$CHANNEL" = "dev" ] || [ "$CHANNEL" = "official" ] || fail "channel must be dev or official, not '$CHANNEL'"
    ;;
  promote)
    CHANNEL=official; WANT_JSON="${2:-}"; shift 2 || fail "promote needs the scripts.json sha256 and the names"
    NAMES="$*"
    [ -n "$NAMES" ] || fail "promote needs at least one script name"
    ;;
  *) fail "usage: legion-scripts.sh publish <dev|official> <sha256> | promote <sha256> <name>..." ;;
esac

if [ "$CHANNEL" = "dev" ]; then
  DIR="$DEV_DIR"; URL="$DEV_URL"; RESOLVE="$DEV_RESOLVE"; BAKBASE="$BAK_ROOT/dev"
else
  DIR="$OFFICIAL_DIR"; URL="$OFFICIAL_URL"; RESOLVE="$OFFICIAL_RESOLVE"; BAKBASE="$BAK_ROOT"
fi
cd "$STAGE" || fail "no staging folder $STAGE"
[ -f files.txt ] || fail "no files.txt in $STAGE"
[ -f "$JSON" ] || fail "no $JSON in $STAGE"
sed -i 's/\r$//' files.txt
while read -r S N; do
  case "$N" in *[!A-Za-z0-9_.-]*|''|.*) fail "files.txt names '$N', which is not a plain file name" ;; esac
done < files.txt

# Writes into a served folder: directly when this user can, else through a throwaway container (root-owned).
can_write() { [ -d "$DIR" ] && [ -w "$DIR" ]; }
put() {  # put <source file> <name in $DIR>
  if can_write; then
    cp "$1" "$DIR/.$2.new" && chmod 644 "$DIR/.$2.new" && mv -f "$DIR/.$2.new" "$DIR/$2"
  else
    docker run --rm -v "$DIR":/dst -v "$(dirname "$1")":/src:ro nginx:alpine sh -c "cp '/src/$(basename "$1")' '/dst/.$2.new' && chmod 644 '/dst/.$2.new' && mv -f '/dst/.$2.new' '/dst/$2'"
  fi
}
drop() {  # drop <name in $DIR>
  if can_write; then rm -f "$DIR/$1"; else docker run --rm -v "$DIR":/dst nginx:alpine rm -f "/dst/$1"; fi
}
fetch() {  # fetch <name> <out file>: prints "<http code> <sha256>"
  local CODE
  CODE=$(curl -sk --max-time 30 $RESOLVE "$URL/$1?t=$TS" -o "$2" -w '%{http_code}')
  echo "$CODE $(sha256sum "$2" | cut -d' ' -f1)"
}

echo "===== $MODE to the $CHANNEL channel: $DIR ====="
if [ ! -d "$DIR" ]; then
  mkdir -p "$DIR" 2>/dev/null || docker run --rm -v "$(dirname "$DIR")":/p nginx:alpine mkdir -p "/p/$(basename "$DIR")" || fail "could not make $DIR"
  echo "  made $DIR (the first publish to this channel)"
fi

if [ "$MODE" = "promote" ]; then
  echo "===== copy the promoted scripts from the dev folder ($DEV_DIR) ====="
  for N in $NAMES; do
    case "$N" in *[!A-Za-z0-9_.-]*|''|.*) fail "'$N' is not a plain file name" ;; esac
    listed "$N" || fail "$N is not in the new official files.txt"
    [ -f "$DEV_DIR/$N" ] || fail "$N is not in the dev folder"
    cp "$DEV_DIR/$N" "$STAGE/$N" || fail "copy of $N from the dev folder"
    echo "  $N"
  done
fi

echo "===== staged copies ====="
GOTJ=$(sha256sum "$JSON" | cut -d' ' -f1)
echo "  $JSON sha256 $GOTJ (want $WANT_JSON)"
[ "$GOTJ" = "$WANT_JSON" ] || fail "staged $JSON does not match the PC's copy"
NEWFILES=""
while read -r S N; do
  if [ -f "$STAGE/$N" ]; then
    G=$(sha256sum "$STAGE/$N" | cut -d' ' -f1)
    echo "  $N staged sha256 $G (want $S)"
    [ "$G" = "$S" ] || fail "staged $N does not match files.txt"
    NEWFILES="$NEWFILES $N"
  else
    [ -f "$DIR/$N" ] || fail "$N is listed, not staged, and not already served"
    G=$(sha256sum "$DIR/$N" | cut -d' ' -f1)
    [ "$G" = "$S" ] || fail "$N is listed as unchanged, but the served copy is not the one listed"
    echo "  $N unchanged, already served"
  fi
done < files.txt
[ -n "$NEWFILES" ] || echo "  (no file changes: only $JSON)"

if [ "$DRYRUN" = "1" ]; then
  echo "DRY RUN: stopping before the swap; nothing served was changed."
  rm -rf "$STAGE"
  exit 0
fi

echo "===== back up the served set ====="
OLDVER=$( (grep -o '"version"[[:space:]]*:[[:space:]]*"[^"]*"' "$DIR/$JSON" 2>/dev/null || true) | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
[ -n "$OLDVER" ] || OLDVER="none-$TS"
BAK="$BAKBASE/$OLDVER"
[ -e "$BAK" ] && BAK="$BAK-$TS"
mkdir -p "$BAK" || fail "could not make $BAK"
for F in "$DIR"/*.py "$DIR/$JSON"; do [ -f "$F" ] && cp -p "$F" "$BAK/"; done
echo "  $BAK: $(ls "$BAK" | wc -l) file(s)"

restore() {
  echo "  putting the backup back"
  for F in "$BAK"/*; do [ -f "$F" ] && put "$F" "$(basename "$F")" && echo "    restored $(basename "$F")"; done
  for N in $NEWFILES; do [ -f "$BAK/$N" ] || { drop "$N"; echo "    removed $N (it was new)"; }; done
  [ -f "$BAK/$JSON" ] || { drop "$JSON"; echo "    removed $JSON (there was none before)"; }
}

echo "===== put the new and changed scripts in place ====="
for N in $NEWFILES; do
  put "$STAGE/$N" "$N" || { echo "  copy of $N failed"; restore; fail "copy of $N into $DIR"; }
  echo "  $N"
done

echo "===== download every listed script back ($URL) ====="
BAD=0
while read -r S N; do
  R=$(fetch "$N" /tmp/legion-check.py)
  echo "  $URL/$N -> $R"
  [ "$R" = "200 $S" ] || BAD=1
done < files.txt
rm -f /tmp/legion-check.py
if [ "$BAD" = "1" ]; then
  echo "===== MISMATCH: putting the old scripts back ($JSON untouched) ====="
  restore
  exit 1
fi

echo "===== put $JSON in place (every script verified) ====="
put "$STAGE/$JSON" "$JSON" || { echo "  copy of $JSON failed"; restore; fail "copy of $JSON into $DIR"; }
R=$(fetch "$JSON" /tmp/legion-check.json)
echo "  $URL/$JSON -> $R"
rm -f /tmp/legion-check.json
if [ "$R" != "200 $WANT_JSON" ]; then
  echo "===== MISMATCH: putting the old $JSON and the old scripts back ====="
  restore
  exit 1
fi

echo "===== retire scripts the new $JSON no longer lists ====="
for F in "$DIR"/*.py; do
  [ -f "$F" ] || continue
  N=$(basename "$F")
  listed "$N" || { drop "$N"; echo "  removed $N (kept in $BAK)"; }
done

echo "===== served now ====="
ls -la "$DIR"
rm -rf "$STAGE"
echo "OK: $MODE to $CHANNEL, $JSON $WANT_JSON, backup $BAK"
