Set-Location "C:\Users\norbe\OneDrive\Desktop\Crypto-Trading-Bot"
dotnet build 2>&1 | Tee-Object -FilePath "build-output.txt"
Get-Content "build-output.txt" 