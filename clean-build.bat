@echo off
echo Cleaning solution...
dotnet clean --verbosity minimal

echo Building solution...
dotnet build --verbosity minimal

echo Build complete!
pause 