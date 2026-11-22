#!/usr/bin/env bash
# Phase 1 setup + verification for macOS/Linux. Safe to re-run.
# Requires: .NET 10 SDK, Node.js 20.19+ (22 LTS recommended), Docker (for local PostgreSQL).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API="src/FarmPlatform.API"
INFRA="src/FarmPlatform.Infrastructure"

step() { printf '\n\033[1;32m==> %s\033[0m\n' "$1"; }

step "Checking tools"
dotnet --version | grep -q '^10\.' || { echo "Need .NET 10 SDK (found $(dotnet --version))"; exit 1; }
node --version
docker --version

step "Starting PostgreSQL (docker compose)"
cd "$ROOT"
docker compose up -d --wait postgres

cd "$ROOT/backend"

step "Restoring .NET tools and packages"
dotnet tool restore
dotnet restore

step "Configuring development encryption keys (user-secrets, outside the repo)"
if ! dotnet user-secrets list --project "$API" | grep -q 'Encryption:BlindIndexKey'; then
  dotnet user-secrets set "Encryption:ActiveKeyId" "k1" --project "$API" >/dev/null
  dotnet user-secrets set "Encryption:Keys:k1" "$(openssl rand -base64 32)" --project "$API" >/dev/null
  dotnet user-secrets set "Encryption:BlindIndexKey" "$(openssl rand -base64 32)" --project "$API" >/dev/null
  echo "Generated new development keys."
else
  echo "Keys already configured."
fi

step "Building"
dotnet build --no-restore

step "Creating the initial migration (first run only)"
if [ ! -d "$INFRA/Persistence/Migrations" ]; then
  dotnet ef migrations add InitialCreate --project "$INFRA" --startup-project "$API" --output-dir Persistence/Migrations
  dotnet build --no-restore
else
  echo "Migrations already exist."
fi

step "Applying migrations to local PostgreSQL"
dotnet ef database update --project "$INFRA" --startup-project "$API"

step "Running tests"
dotnet test --no-build

step "Installing and building the frontend"
cd "$ROOT/frontend"
npm ci
npm run build

step "Done"
cat <<'MSG'
Start the app in two terminals:
  1) cd backend && dotnet run --project src/FarmPlatform.API      # http://localhost:5080
  2) cd frontend && npm run dev                                    # http://localhost:5173
Checks:
  curl http://localhost:5080/health/ready
  curl http://localhost:5080/api/v1/lookups/legal-entity-statuses
The footer of the site shows "Server: online" when the API and database are reachable.
MSG
