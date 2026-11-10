@echo off
rem Builds SLAL2OStim.exe (needs the .NET 8 SDK) and puts it next to the FNIS folder.
cd /d "%~dp0"
dotnet publish src\SLAL2OStim.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
if errorlevel 1 (echo. & echo BUILD FAILED & pause & exit /b 1)
copy /y "src\bin\Release\net8.0-windows\win-x64\publish\SLAL2OStim.exe" "%~dp0SLAL2OStim.exe" >nul
echo.
echo Built: %~dp0SLAL2OStim.exe
pause
