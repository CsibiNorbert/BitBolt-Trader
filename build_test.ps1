#!/usr/bin/env pwsh

Write-Host "Starting build test..."

try {
    $buildResult = dotnet build --verbosity normal 2>&1
    Write-Host "Build output:"
    Write-Host $buildResult
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build successful!" -ForegroundColor Green
    } else {
        Write-Host "❌ Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        Write-Host "Build errors:" -ForegroundColor Red
        Write-Host $buildResult -ForegroundColor Red
    }
}
catch {
    Write-Host "❌ Error running build: $_" -ForegroundColor Red
}

Write-Host "Build test completed." 