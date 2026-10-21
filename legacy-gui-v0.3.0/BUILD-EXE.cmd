@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo    SE2 Workshop UGC Fix - Windows build
echo ============================================
where dotnet >NUL 2>&1
if errorlevel 1 (
  echo ERROR: .NET 9 SDK not found. Install the official x64 SDK:
  echo https://dotnet.microsoft.com/download/dotnet/9.0
  pause
  exit /b 1
)
echo.
echo === Run pure C# safety tests before publishing ===
dotnet run --project "tests\MetadataSafetyTests\MetadataSafetyTests.csproj" -c Release
if errorlevel 1 (
 echo SAFETY TESTS FAILED! Aborting build; no release executable created.
 pause
 exit /b 1
)
echo.
echo === Compile self-contained Windows app ===
dotnet publish "SE2WorkshopUGCFix\SE2WorkshopUGCFix.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o "Release\win-x64"
if errorlevel 1 (
 echo BUILD FAILED: Send the complete output to the developer.
 pause
 exit /b 1
)
echo.
echo BUILD COMPLETED! The Windows app is in:
echo %~dp0Release\win-x64
if not exist "Release\win-x64\SE2-Workshop-UGC-Fix.exe" (
 echo ERROR: publish finished but executable was not found.
 pause
 exit /b 1
)
echo.
echo SHA256 (publish this alongside the release for verification):
certutil -hashfile "Release\win-x64\SE2-Workshop-UGC-Fix.exe" SHA256
echo.
echo.
echo === Build a ready-to-send end-user ZIP (EXE, guide and checksum) ===
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $release='Release\win-x64'; $dst='Release\SE2-Workshop-UGC-Fix-v0.3.0-Windows-x64.zip'; $exe=Join-Path $release 'SE2-Workshop-UGC-Fix.exe'; if (-not (Test-Path $exe)) { throw 'EXE missing' }; $hash=(Get-FileHash $exe -Algorithm SHA256).Hash; Set-Content (Join-Path $release 'SHA256.txt') ('SE2-Workshop-UGC-Fix.exe  '+$hash) -Encoding ascii; Copy-Item README-START-HERE-EN.md (Join-Path $release 'README-START-HERE-EN.md') -Force; Copy-Item REPRODUCE-WITH-DISPOSABLE-MOD-EN.md (Join-Path $release 'REPRODUCE-WITH-DISPOSABLE-MOD-EN.md') -Force; if (Test-Path (Join-Path $release 'TEST-DESCRIPTIONS')) { Remove-Item (Join-Path $release 'TEST-DESCRIPTIONS') -Recurse -Force }; Copy-Item TEST-DESCRIPTIONS (Join-Path $release 'TEST-DESCRIPTIONS') -Recurse -Force; if (Test-Path $dst) { Remove-Item $dst -Force }; Compress-Archive -Path $exe,(Join-Path $release 'SHA256.txt'),(Join-Path $release 'README-START-HERE-EN.md'),(Join-Path $release 'REPRODUCE-WITH-DISPOSABLE-MOD-EN.md'),(Join-Path $release 'TEST-DESCRIPTIONS') -DestinationPath $dst -CompressionLevel Optimal -Force; Write-Host ('Created: '+$dst)"
if errorlevel 1 (
 echo Packaging failed. Your EXE is still in Release\win-x64.
 pause
 exit /b 1
)
explorer "%~dp0Release"
pause
