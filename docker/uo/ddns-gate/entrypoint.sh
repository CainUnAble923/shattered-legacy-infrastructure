#!/bin/sh
# cc-P668: sl-ddns-gate's start. Makes the gate's self-signed certificate the first time (kept in the ddns_tls volume,
# so a restart keeps it and sl-ddns, which read the bundle when it started, still trusts it), then rebuilds the bundle
# sl-ddns reads through SSL_CERT_FILE: the public CAs (Cloudflare's API needs them) plus this certificate.
# The certificate is for the name ddns-gate only, the service name on the uo compose network. ASCII only.
set -eu
TLS=${TLS_DIR:-/tls}
mkdir -p "$TLS" /data
if [ ! -s "$TLS/gate.crt" ] || [ ! -s "$TLS/gate.key" ]; then
    command -v openssl >/dev/null 2>&1 || apk add --no-cache -q openssl
    openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -subj "/CN=ddns-gate" \
        -addext "subjectAltName=DNS:ddns-gate" -addext "basicConstraints=critical,CA:TRUE" \
        -keyout "$TLS/gate.key.tmp" -out "$TLS/gate.crt.tmp" 2>/dev/null
    chmod 600 "$TLS/gate.key.tmp"
    mv "$TLS/gate.key.tmp" "$TLS/gate.key"
    mv "$TLS/gate.crt.tmp" "$TLS/gate.crt"
    echo "made the gate's certificate in $TLS"
fi
cat /etc/ssl/certs/ca-certificates.crt "$TLS/gate.crt" > "$TLS/bundle.pem.tmp"
mv "$TLS/bundle.pem.tmp" "$TLS/bundle.pem"
exec python -u /app/gate.py
