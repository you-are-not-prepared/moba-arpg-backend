# Runs the backend (database + API) in Docker, from any terminal or editor.
# Windows: .\moba start        (moba.cmd in the repo root runs this script)
# Mac/Linux: pwsh scripts/moba.ps1 start
#
#   moba start    Database + API on localhost:8080, rebuilding after code changes
#   moba db       Only the database, for running the API from your IDE or with dotnet run
#   moba stop     Stop them (data is kept)
#   moba logs     Follow the API log (Ctrl+C stops following, not the API)
#   moba status   Show what's running
#
# To run the game server too, use moba-arpg-game's script instead: it starts this backend first.
param(
    [Parameter(Position = 0)]
    [ValidateSet('start', 'db', 'stop', 'logs', 'status', 'help')]
    [string]$Action = 'help'
)

$backendDir = Split-Path -Parent $PSScriptRoot

function Fail([string]$message) {
    Write-Host $message -ForegroundColor Red
    exit 1
}

function Assert-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Fail 'Docker not found. Install Docker Desktop: https://www.docker.com/products/docker-desktop/'
    }
    # Not "*> $null": in Windows PowerShell, redirecting a native command's stderr can throw.
    $null = docker info --format '{{.ServerVersion}}' 2>$null
    if ($LASTEXITCODE -ne 0) { Fail 'Docker is not running. Start Docker Desktop and try again.' }
}

# Runs "docker compose <args>" in this repo and stops the script if it fails.
function Compose([string[]]$composeArgs) {
    Push-Location $backendDir
    try { docker compose @composeArgs } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($Action -ne 'help') { Assert-Docker }

switch ($Action) {
    'start' {
        Compose @('up', '-d', '--build')
        Write-Host ''
        Write-Host 'Backend: http://localhost:8080/health (may take a few seconds to answer)'
    }
    'db' {
        Compose @('up', '-d', 'db')
        Write-Host ''
        Write-Host 'Database: localhost:5432. Run the API with: dotnet run --project src/Moba.Api'
    }
    'stop' { Compose @('down') }
    'logs' { Compose @('logs', '-f', 'api') }
    'status' { Compose @('ps') }
    'help' {
        Write-Host @'
moba start    Database + API on localhost:8080, rebuilding after code changes
moba db       Only the database, for running the API from your IDE or with dotnet run
moba stop     Stop them (data is kept)
moba logs     Follow the API log (Ctrl+C stops following, not the API)
moba status   Show what's running
'@
    }
}
