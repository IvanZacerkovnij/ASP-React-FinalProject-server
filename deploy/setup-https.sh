#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

if [[ "$EUID" -ne 0 ]]; then
    echo "Run this script with sudo." >&2
    exit 1
fi

if ! command -v certbot >/dev/null 2>&1; then
    apt-get update
    apt-get install -y certbot
fi

bash "$deploy_dir/configure-nginx.sh"

certbot certonly --webroot --webroot-path /var/www/letsencrypt \
    --cert-name chips-backend.duckdns.org \
    --domain chips-backend.duckdns.org \
    --non-interactive --agree-tos --register-unsafely-without-email \
    --keep-until-expiring

install -d -m 755 /etc/letsencrypt/renewal-hooks/deploy
install -m 755 "$deploy_dir/renew-nginx.sh" /etc/letsencrypt/renewal-hooks/deploy/threads-nginx
systemctl enable --now certbot.timer

bash "$deploy_dir/configure-nginx.sh"
