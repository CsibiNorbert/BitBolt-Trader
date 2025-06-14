@echo off
cd /d "C:\Users\norbe\OneDrive\Desktop\Crypto-Trading-Bot"
echo Building solution...
dotnet build > build-log.txt 2>&1
echo Build completed. Check build-log.txt for details.
type build-log.txt 