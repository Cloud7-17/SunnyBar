@echo off
setlocal
set "SUNNY_ROOT=%~dp0.."
if exist "%SUNNY_ROOT%\.tools\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%SUNNY_ROOT%\.tools\dotnet"
    set "DOTNET_ROOT_X64=%SUNNY_ROOT%\.tools\dotnet"
)
if not exist "%SUNNY_ROOT%\src\SunnyBar.App\bin\Debug\net10.0-windows\SunnyBar.App.exe" (
    echo Sunny Bar has not been built. See README.md for build instructions.
    exit /b 1
)
start "" "%SUNNY_ROOT%\src\SunnyBar.App\bin\Debug\net10.0-windows\SunnyBar.App.exe" %*
endlocal
