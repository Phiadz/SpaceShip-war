param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "[1/3] Building project..." -ForegroundColor Cyan
dotnet build .\SpaceShipWar.csproj -c $Configuration

$dllPath = Join-Path $PSScriptRoot "bin\$Configuration\net8.0-windows\SpaceShipWar.dll"
if (-not (Test-Path $dllPath)) {
    throw "Cannot find output: $dllPath"
}

Write-Host "[2/3] Starting instance #1..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "`"$dllPath`""

Write-Host "[3/3] Starting instance #2..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "`"$dllPath`""

Write-Host "Done. Two instances were started." -ForegroundColor Yellow
Write-Host "Tip: In one window click Start Host; in the other click Connect." -ForegroundColor Yellow
