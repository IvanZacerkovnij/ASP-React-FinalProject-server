#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
certificate_dir=/etc/letsencrypt/live/chips-backend.duckdns.org
config_source="$deploy_dir/nginx/threads.conf"
config_target=/etc/nginx/sites-available/threads

if [[ "$EUID" -ne 0 ]]; then
    echo "Run this script with sudo." >&2
    exit 1
fi

if [[ -s "$certificate_dir/fullchain.pem" && -s "$certificate_dir/privkey.pem" ]]; then
    config_source="$deploy_dir/nginx/threads-https.conf"
fi

backup_file="$(mktemp)"
had_config=false
if [[ -f "$config_target" ]]; then
    cp "$config_target" "$backup_file"
    had_config=true
fi
trap 'rm -f "$backup_file"' EXIT

install -d -m 755 /var/www/letsencrypt
install -m 644 "$config_source" "$config_target"
ln -sf "$config_target" /etc/nginx/sites-enabled/threads

if ! nginx -t; then
    if [[ "$had_config" == true ]]; then
        cp "$backup_file" "$config_target"
    else
        rm -f /etc/nginx/sites-enabled/threads "$config_target"
    fi
    exit 1
fi

systemctl enable --now nginx
systemctl reload nginx
