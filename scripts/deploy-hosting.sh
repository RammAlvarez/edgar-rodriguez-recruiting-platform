#!/usr/bin/env bash
# Builds the three apps and deploys them to Firebase Hosting (deploy/providers/gcp.md §6). Each app gets the
# environment's deploy/config/<app>.json as its /config.json (from `terraform output -json app_config`).
#   scripts/deploy-hosting.sh dev
set -euo pipefail
cd "$(dirname "$0")/.."

project="${1:?usage: scripts/deploy-hosting.sh <firebase project alias from .firebaserc>}"
for app in admin candidates clients; do
  [ -s "deploy/config/$app.json" ] || { echo "✘ deploy/config/$app.json is missing (copy $app.json.example)"; exit 1; }
done

for app in admin candidates clients; do
  npx ng build "$app"
  cp "deploy/config/$app.json" "dist/$app/browser/config.json"
done
npx firebase deploy --only hosting --project "$project"
