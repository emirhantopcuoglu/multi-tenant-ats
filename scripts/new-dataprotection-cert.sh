#!/usr/bin/env bash
# Creates secrets/dataprotection.pfx, the certificate the API encrypts its DataProtection keys with.
# The PFX password is read from DATA_PROTECTION_CERT_PASSWORD (the same variable compose passes on).
set -euo pipefail

if [[ -z "${DATA_PROTECTION_CERT_PASSWORD:-}" ]]; then
  echo "Set DATA_PROTECTION_CERT_PASSWORD first." >&2
  exit 1
fi

# Git Bash would otherwise turn "/CN=..." into a Windows path.
export MSYS2_ARG_CONV_EXCL="/CN"

secrets_dir="$(cd "$(dirname "$0")/.." && pwd)/secrets"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

mkdir -p "$secrets_dir"
openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -subj "/CN=Ats DataProtection" -quiet \
  -keyout "$work_dir/key.pem" -out "$work_dir/cert.pem"
openssl pkcs12 -export -inkey "$work_dir/key.pem" -in "$work_dir/cert.pem" \
  -out "$secrets_dir/dataprotection.pfx" -passout env:DATA_PROTECTION_CERT_PASSWORD

# The API container runs as a different user; the key inside is still password-protected.
chmod 644 "$secrets_dir/dataprotection.pfx"
echo "Wrote $secrets_dir/dataprotection.pfx"
