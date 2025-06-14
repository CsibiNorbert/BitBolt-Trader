try {
    Set-Location "C:\Users\norbe\OneDrive\Desktop\Crypto-Trading-Bot"
    Write-Host "Building solution..."
    
    $output = dotnet build --verbosity normal 2>&1
    $output | Out-File -FilePath "build-results.txt" -Encoding UTF8
    
    Write-Host "Build completed. Output saved to build-results.txt"
    Write-Host "Exit code: $LASTEXITCODE"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed. Showing errors:"
        $output | Where-Object { $_ -match "error|Error|ERROR" }
    }
}
catch {
    Write-Host "Error: $_"
} 