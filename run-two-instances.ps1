param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

function Stop-StaleSpaceShipProcesses {
    Write-Host "[0/4] Cleaning stale SpaceShipWar processes..." -ForegroundColor DarkCyan

    $dotnetGameProcesses = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
        Where-Object { $_.CommandLine -like "*SpaceShipWar.dll*" }

    foreach ($proc in $dotnetGameProcesses) {
        try {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
            Write-Host "  - Stopped dotnet host PID $($proc.ProcessId)" -ForegroundColor DarkGray
        }
        catch {
            Write-Warning "  - Cannot stop PID $($proc.ProcessId): $($_.Exception.Message)"
        }
    }

    $exeGameProcesses = Get-Process -Name "SpaceShipWar" -ErrorAction SilentlyContinue
    foreach ($proc in $exeGameProcesses) {
        try {
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
            Write-Host "  - Stopped SpaceShipWar PID $($proc.Id)" -ForegroundColor DarkGray
        }
        catch {
            Write-Warning "  - Cannot stop PID $($proc.Id): $($_.Exception.Message)"
        }
    }
}

Stop-StaleSpaceShipProcesses

Write-Host "[1/4] Rebuilding project (fresh)..." -ForegroundColor Cyan
dotnet build .\SpaceShipWar.csproj -c $Configuration -t:Rebuild

$outputDir = Join-Path $PSScriptRoot "bin\$Configuration\net8.0-windows"
$exePath = Join-Path $outputDir "SpaceShipWar.exe"
$dllPath = Join-Path $outputDir "SpaceShipWar.dll"

if (-not (Test-Path $exePath) -and -not (Test-Path $dllPath)) {
    throw "Cannot find output: $exePath or $dllPath"
}

Write-Host "[2/4] Starting instance #1..." -ForegroundColor Green
if (Test-Path $exePath) {
    $p1 = Start-Process -FilePath $exePath -WorkingDirectory $outputDir -PassThru
}
else {
    $p1 = Start-Process -FilePath dotnet -ArgumentList "`"$dllPath`"" -WorkingDirectory $outputDir -PassThru
}

Write-Host "[3/4] Starting instance #2..." -ForegroundColor Green
if (Test-Path $exePath) {
    $p2 = Start-Process -FilePath $exePath -WorkingDirectory $outputDir -PassThru
}
else {
    $p2 = Start-Process -FilePath dotnet -ArgumentList "`"$dllPath`"" -WorkingDirectory $outputDir -PassThru
}

Write-Host "[4/4] Done. Two instances were started from latest build." -ForegroundColor Yellow
Write-Host "PID #1: $($p1.Id) | PID #2: $($p2.Id)" -ForegroundColor DarkYellow
Write-Host "Tip: In one window click Start Host; in the other click Connect." -ForegroundColor Yellow
