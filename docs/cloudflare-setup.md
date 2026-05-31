# Cloudflare Setup — shatteredlegacyuo.com

## Step 1 — Add Domain to Cloudflare

1. Log in to [dash.cloudflare.com](https://dash.cloudflare.com)
2. Click **Add a Site** → enter `shatteredlegacyuo.com`
3. Choose the **Free** plan
4. Cloudflare will scan existing DNS (nothing to scan on a new domain) — click **Continue**
5. Copy the two **Cloudflare nameservers** shown (e.g. `aria.ns.cloudflare.com`)
6. In your registrar (wherever you bought the domain), replace the default nameservers with the two Cloudflare ones
7. Wait ~5–30 min for propagation — Cloudflare will email you when active

---

## Step 2 — DNS Records

Once the domain is active, add these records in the Cloudflare DNS dashboard:

| Type  | Name                | Content          | Proxy       | TTL  |
|-------|---------------------|------------------|-------------|------|
| A     | `@`                 | *(your WAN IP)*  | **DNS only** (gray cloud) | Auto |
| CNAME | `www`               | `shatteredlegacyuo.com` | Proxied | Auto |
| CNAME | `get`               | `shatteredlegacyuo.com` | Proxied | Auto |
| CNAME | `wiki`              | `shatteredlegacyuo.com` | Proxied | Auto |
| CNAME | `dashboard`         | `shatteredlegacyuo.com` | **DNS only** | Auto |

> **Why `@` is DNS-only (gray cloud):** Port 2593 (UO) is not proxied by Cloudflare — it must hit your IP directly. The A record for `@` must be gray-cloud so players can connect.  
> **Why `dashboard` is DNS-only:** Dashboard is VPN/LAN only — no reason to route through Cloudflare.

The `ddns-updater` container will keep the `@` A record current automatically once you add the API token.

---

## Step 3 — Create API Token

This token is used by both `ddns-updater` (DDNS) and Nginx Proxy Manager (Let's Encrypt DNS challenge).

1. Go to **My Profile** → **API Tokens** → **Create Token**
2. Click **Use template** next to **Edit zone DNS**
3. Under **Zone Resources**: set to **Specific zone** → `shatteredlegacyuo.com`
4. Click **Continue to summary** → **Create Token**
5. **Copy the token now** — it will not be shown again

---

## Step 4 — Get Your Zone ID

1. Go to the `shatteredlegacyuo.com` dashboard on Cloudflare
2. Scroll down the right sidebar to **API** section
3. Copy the **Zone ID**

---

## Step 5 — Fill in .env Files

### `docker/uo/.env`
```
SERVER_PATH=D:/ShatteredLegacy/server
DOMAIN=shatteredlegacyuo.com
CF_ZONE_ID=<paste Zone ID>
CF_API_TOKEN=<paste API Token>
TZ=America/Chicago
```

### Nginx Proxy Manager (done in the UI, not .env)
When adding SSL certificates in NPM:
- Choose **DNS Challenge**
- Provider: **Cloudflare**
- Enter your API Token

---

## Step 6 — SSL Certificates in NPM

After proxy is running (`docker compose up -d` in `docker/proxy/`):

1. Browse to `http://127.0.0.1:81` (or SSH tunnel if remote)
2. Default login: `admin@example.com` / `changeme` — **change immediately**
3. Go to **SSL Certificates** → **Add Certificate** → **Let's Encrypt**
4. Domain names: `shatteredlegacyuo.com`, `*.shatteredlegacyuo.com`
5. Enable **DNS Challenge** → Cloudflare → paste API token
6. Issue certificate

Then add Proxy Hosts:
| Domain | Forward to | Port |
|--------|-----------|------|
| `shatteredlegacyuo.com` | `sl-website` | 80 |
| `get.shatteredlegacyuo.com` | `sl-downloads` | 80 |
| `wiki.shatteredlegacyuo.com` | `sl-wiki` | 8080 |

> Dashboard and UO (port 2593) are **not** proxied through NPM.
