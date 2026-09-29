#!/bin/bash
# install-wiki-skin.sh - runs ON HAVEN. Copies the Shattered Legacy skin into sl-wiki.
# The DokuWiki image keeps config in /storage/conf and media in /storage/data/media
# (see haven-wiki-fix.sh); falls back to /dokuwiki/... if /storage is not there.
# Backs up anything it replaces to ~/sl-wiki-skin/backup-<time>.
set -u
fail() { echo "STOPPED: $1"; exit 1; }
SRC="$HOME/sl-wiki-skin"
C=sl-wiki

if docker exec "$C" test -d /storage/conf; then CONF=/storage/conf; else CONF=/dokuwiki/conf; fi
if docker exec "$C" test -d /storage/data/media; then MEDIA=/storage/data/media; else MEDIA=/dokuwiki/data/media; fi
docker exec "$C" test -d "$CONF" || fail "no config folder found in $C; nothing changed"
echo "config: $CONF   media: $MEDIA"

TARGETS="$CONF/userstyle.css $CONF/tpl/dokuwiki/style.ini $CONF/footer.html $MEDIA/wiki/logo.svg $MEDIA/wiki/favicon.ico $MEDIA/wiki/favicon.svg"

BK="$SRC/backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BK"
echo "== backing up current files (missing ones are fine)"
for f in $TARGETS; do
  docker cp "$C:$f" "$BK/$(echo "$f" | tr / _)" 2>/dev/null && echo "  saved $f" || true
done

echo "== copying skin"
docker exec -u 0 "$C" mkdir -p "$CONF/tpl/dokuwiki" "$MEDIA/wiki" || fail "could not create folders"
docker cp "$SRC/conf/userstyle.css"            "$C:$CONF/userstyle.css"            || fail "copy userstyle.css"
docker cp "$SRC/conf/tpl/dokuwiki/style.ini"   "$C:$CONF/tpl/dokuwiki/style.ini"   || fail "copy style.ini"
docker cp "$SRC/conf/footer.html"              "$C:$CONF/footer.html"              || fail "copy footer.html"
docker exec -u 0 "$C" rm -f "$CONF/tpl/dokuwiki/footer.html"
for m in logo.svg favicon.ico favicon.svg; do
  docker cp "$SRC/media/wiki/$m" "$C:$MEDIA/wiki/$m" || fail "copy $m"
done

echo "== matching ownership to the wiki's own files"
OWN=$(docker exec "$C" stat -c '%u:%g' "$CONF/local.php" 2>/dev/null || docker exec "$C" stat -c '%u:%g' "$CONF")
docker exec -u 0 "$C" chown -R "$OWN" "$CONF/tpl" "$CONF/userstyle.css" "$CONF/footer.html" "$MEDIA/wiki"
echo "owner: $OWN"

echo "== clearing the stylesheet cache"
docker exec -u 0 "$C" sh -c "touch $CONF/local.php 2>/dev/null; true"

echo "== verify"
docker exec sl-proxy curl -s "http://sl-wiki:8080/lib/exe/css.php?t=dokuwiki" | grep -c 'Georgia' | sed 's/^/Georgia rules in served CSS (want > 0): /'
docker exec sl-proxy curl -s "http://sl-wiki:8080/doku.php?id=start" | grep -o 'media/wiki/logo.svg\|lib/exe/fetch.php[^"]*logo.svg' | head -1 | sed 's/^/logo in header: /'

docker exec sl-proxy curl -s "http://sl-wiki:8080/doku.php?id=start" | grep -c "Broadsword" | sed "s/^/footer on start page (want 1): /"
echo
echo "Done. Reload the wiki with Ctrl+F5. Backup is in $BK"
