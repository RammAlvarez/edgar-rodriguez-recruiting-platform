#!/usr/bin/env bash
# Provider neutrality (kickoff "Non-negotiable rules", ARCHITECTURE §5): vendor SDKs only in their adapter files,
# no cloud SDKs anywhere. Run by CI and by `npm run check:neutrality`.
set -euo pipefail
cd "$(dirname "$0")/.."

fail=0
code() { # list tracked + untracked source files, excluding generated output and reference material
  git ls-files --cached --others --exclude-standard -- "$@" \
    | grep -Ev '^(docs|design-reference|examples)/|/(bin|obj|dist|node_modules)/|package-lock\.json$' || true
}
violation() { echo "✘ $1"; shift; printf '    %s\n' "$@"; fail=1; }

# 1. FirebaseAdmin (server) only in Platform.Api/Auth/Firebase*.cs (+ the package reference itself).
hits=$(code '*.cs' | xargs grep -lE '\b(FirebaseAdmin|Google\.Apis\.Auth)\b' 2>/dev/null \
  | grep -Ev '^services/platform-api/src/Platform\.Api/Auth/Firebase[^/]*\.cs$' || true)
[ -z "$hits" ] || violation "FirebaseAdmin outside Platform.Api/Auth/Firebase*.cs:" $hits

# 2. Firebase JS SDK only in apps/*/src/auth/firebase-*.ts.
hits=$(code '*.ts' '*.mjs' '*.js' | xargs grep -lE "from ['\"](firebase|@firebase)(/|['\"])|import\(['\"](firebase|@firebase)" 2>/dev/null \
  | grep -Ev '^apps/[^/]+/src/auth/firebase-[^/]*\.ts$' || true)
[ -z "$hits" ] || violation "Firebase JS SDK outside apps/*/src/auth/firebase-*.ts:" $hits

# 3. AWSSDK.S3 only in Platform.Infrastructure/Storage/.
hits=$(code '*.cs' | xargs grep -lE '\busing Amazon(\.|;)|\bAmazon\.(S3|Runtime)\b' 2>/dev/null \
  | grep -Ev '^services/platform-api/src/Platform\.Infrastructure/Storage/' || true)
[ -z "$hits" ] || violation "AWS SDK outside Platform.Infrastructure/Storage/:" $hits
hits=$(code '*.csproj' | xargs grep -lE 'Include="AWSSDK\.' 2>/dev/null \
  | grep -Ev '^services/platform-api/src/Platform\.Infrastructure/Platform\.Infrastructure\.csproj$' || true)
[ -z "$hits" ] || violation "AWSSDK package referenced outside Platform.Infrastructure:" $hits
hits=$(code '*.csproj' | xargs grep -lE 'Include="FirebaseAdmin"' 2>/dev/null \
  | grep -Ev '^services/platform-api/src/Platform\.Api/Platform\.Api\.csproj$' || true)
[ -z "$hits" ] || violation "FirebaseAdmin package referenced outside Platform.Api:" $hits

# 4. No other cloud SDKs, managed queues, secret managers, schedulers or DB connectors.
pattern='Google\.Cloud\.|@google-cloud/|firebase-admin|Azure\.|@azure/|aws-sdk|@aws-sdk/|Amazon\.(SQS|SimpleNotification|SecretsManager|Lambda)|Cloud\.SqlConnector|cloud-sql-connector|DigitalOcean'
hits=$(code '*.cs' '*.csproj' '*.props' '*.ts' '*.mjs' '*.js' 'package.json' '*/package.json' | xargs grep -lE "$pattern" 2>/dev/null \
  | grep -v '^scripts/check-neutrality\.sh$' || true)
[ -z "$hits" ] || violation "Cloud vendor SDK found:" $hits

if [ "$fail" -eq 0 ]; then echo "✔ provider neutrality"; fi
exit $fail
