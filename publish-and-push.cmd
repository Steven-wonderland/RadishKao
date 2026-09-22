@echo off
setlocal
cd /d "%~dp0"

set "PUBLISH_DIR=%~dp0release\CatPet-win-x64"
set "PACKAGE=%~dp0release\CatPet-win-x64.zip"

echo Publishing CatPet to %PUBLISH_DIR% ...
dotnet publish CatPet.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "%PUBLISH_DIR%"
if errorlevel 1 (
    echo Publish failed.
    exit /b 1
)

echo Creating %PACKAGE% ...
if exist "%PACKAGE%" del /q "%PACKAGE%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%PACKAGE%' -CompressionLevel Optimal"
if errorlevel 1 (
    echo Package creation failed.
    exit /b 1
)

echo Created %PACKAGE%
echo Upload this ZIP as a GitHub Release asset for end users.

echo Preparing Git LFS...
git lfs install
if errorlevel 1 exit /b 1

git add .gitattributes release/CatPet-win-x64

git diff --cached --quiet
if not errorlevel 1 (
    echo No release changes to commit.
    exit /b 0
)

git commit -m "Publish CatPet win-x64"
if errorlevel 1 exit /b 1

git push origin main
if errorlevel 1 exit /b 1

echo Published and pushed successfully.
