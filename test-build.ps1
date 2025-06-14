#!/usr/bin/env pwsh

Write-Host "Building Bitcoin Trading Bot..." -ForegroundColor Green

# Build the solution
Write-Host "Running dotnet build..." -ForegroundColor Yellow
$buildResult = dotnet build --verbosity minimal
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    Write-Host "Starting the application..." -ForegroundColor Yellow
    Write-Host "The application will be available at:" -ForegroundColor Cyan
    Write-Host "  - HTTPS: https://localhost:5001" -ForegroundColor Cyan
    Write-Host "  - HTTP:  http://localhost:5000" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Press Ctrl+C to stop the application" -ForegroundColor Yellow
    Write-Host ""
    
    # Run the application
    dotnet run --project src\BitcoinTradingBot.Web
} else {
    Write-Host "Build failed with exit code: $buildExitCode" -ForegroundColor Red
    Write-Host "Build output:" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
} 