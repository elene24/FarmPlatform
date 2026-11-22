# Phase 1 setup + verification for Windows (PowerShell 7+). Safe to re-run.
# Requires: .NET 10 SDK, Node.js 20.19+ (22 LTS recommended), Docker Desktop.
$ErrorActionPreference = 'Stop'
$Root  = Resolve-Path (Join-Path $PSScriptRoot '..')
$Api   = 'src/FarmPlatform.API'
$Infra = 'src/FarmPlatform.Infrastructure'

function Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Green }
# Runs a native command and stops the script if it fails.
# Plain function (no param block) so flags like -d / --wait are passed through untouched.
function Run {
  $exe  = $args[0]
  $rest = @($args | Select-Object -Skip 1)
  & $exe @rest
  if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $($args -join ' ')" }
}
function NewKey { [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }

Step 'Checking tools'
if (-not ((dotnet --version) -like '10.*')) { throw 'Need .NET 10 SDK' }
node --version; docker --version

Step 'Starting PostgreSQL (docker compose)'
Set-Location $Root
Run docker compose up -d --wait postgres

Set-Location (Join-Path $Root 'backend')

Step 'Restoring .NET tools and packages'
Run dotnet tool restore
Run dotnet restore

Step 'Configuring development encryption keys (user-secrets, outside the repo)'
$secrets = dotnet user-secrets list --project $Api
if (-not ($secrets -match 'Encryption:BlindIndexKey')) {
  Run dotnet user-secrets set 'Encryption:ActiveKeyId' 'k1' --project $Api
  Run dotnet user-secrets set 'Encryption:Keys:k1' (NewKey) --project $Api
  Run dotnet user-secrets set 'Encryption:BlindIndexKey' (NewKey) --project $Api
} else { 'Keys already configured.' }

Step 'Building'
Run dotnet build --no-restore

Step 'Creating the initial migration (first run only)'
if (-not (Test-Path "$Infra/Persistence/Migrations")) {
  Run dotnet ef migrations add InitialCreate --project $Infra --startup-project $Api --output-dir Persistence/Migrations
  Run dotnet build --no-restore
} else { 'Migrations already exist.' }

Step 'Applying migrations to local PostgreSQL'
Run dotnet ef database update --project $Infra --startup-project $Api

Step 'Running tests'
Run dotnet test --no-build

Step 'Installing and building the frontend'
Set-Location (Join-Path $Root 'frontend')
Run npm.cmd ci
Run npm.cmd run build

Step 'Done'
@'
Start the app in two terminals:
  1) cd backend; dotnet run --project src/FarmPlatform.API      # http://localhost:5080
  2) cd frontend; npm run dev                                   # http://localhost:5173
'@
