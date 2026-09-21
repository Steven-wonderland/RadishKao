@echo off
rem Build if needed, then start the cat.
cd /d "%~dp0"
if not exist "bin\Release
et9.0-windows\CatPet.exe" dotnet build -c Release --nologo
start "" "bin\Release
et9.0-windows\CatPet.exe"
