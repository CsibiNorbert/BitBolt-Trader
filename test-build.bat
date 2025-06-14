@echo off
echo Testing build...
cd /d "C:\Users\norbe\OneDrive\Desktop\Crypto-Trading-Bot"
dotnet build --no-restore --verbosity minimal > build-test-output.txt 2>&1
echo Build test completed. Check build-test-output.txt for results.
echo Exit code: %ERRORLEVEL% 