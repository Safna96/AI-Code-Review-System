#!/usr/bin/env bash
# Same purpose as send-test-webhook.ps1, for macOS/Linux/WSL/Git Bash.
#
# Usage:
#   export GITHUB_WEBHOOK_SECRET=your-secret-from-.env
#   ./scripts/send-test-webhook.sh your-username code-review-sandbox 12 [opened|reopened|synchronize]
set -euo pipefail

OWNER="${1:?Usage: $0 <owner> <repo> <pr-number> [action]}"
REPO="${2:?Usage: $0 <owner> <repo> <pr-number> [action]}"
NUMBER="${3:?Usage: $0 <owner> <repo> <pr-number> [action]}"
ACTION="${4:-opened}"
API_URL="${API_URL:-http://localhost:8080/api/webhook/github}"
SECRET="${GITHUB_WEBHOOK_SECRET:?Set GITHUB_WEBHOOK_SECRET (same value as GitHub:WebhookSecret in .env) first}"

PAYLOAD=$(printf '{"action":"%s","number":%s,"repository":{"name":"%s","owner":{"login":"%s"}}}' \
  "$ACTION" "$NUMBER" "$REPO" "$OWNER")

SIGNATURE="sha256=$(printf '%s' "$PAYLOAD" | openssl dgst -sha256 -hmac "$SECRET" | sed 's/^.* //')"

echo "POST $API_URL  (PR $OWNER/$REPO#$NUMBER, action=$ACTION)"

curl -sS -X POST "$API_URL" \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: pull_request" \
  -H "X-Hub-Signature-256: $SIGNATURE" \
  -d "$PAYLOAD"

echo -e "\nQueued. Watch the API console for 'Starting review for ...' and check GET ${API_URL%/webhook/github}/reviews shortly after."
