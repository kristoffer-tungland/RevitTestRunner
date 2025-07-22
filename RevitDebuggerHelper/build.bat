@echo off
echo Building Revit Debugger Helper (.NET Framework 4.8)...
echo.

REM Try to find MSBuild
set MSBUILD_PATH=""
set NUGET_PATH=""

REM Try to find NuGet first
if exist "%~dp0nuget.exe" (
    set NUGET_PATH="%~dp0nuget.exe"
    goto :find_msbuild
)

REM Look for NuGet in common locations
for %%i in (nuget.exe) do if not "%%~$PATH:i"=="" (
    set NUGET_PATH="%%~$PATH:i"
    goto :find_msbuild
)

if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Shared\NuGetPackageManager\5.11.0\nuget.exe" (
    set NUGET_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\Shared\NuGetPackageManager\5.11.0\nuget.exe"
    goto :find_msbuild
)

echo NuGet not found, continuing without package restore...

:find_msbuild
REM Visual Studio 2022
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)

REM Visual Studio 2019
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)

REM Try .NET Framework MSBuild
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe" (
    set MSBUILD_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"
    goto :build
)

echo MSBuild not found. Please ensure Visual Studio or Build Tools are installed.
pause
exit /b 1

:build
echo Using MSBuild: %MSBUILD_PATH%
if not %NUGET_PATH%=="" (
    echo Using NuGet: %NUGET_PATH%
)
echo.

REM Restore packages if NuGet is available
if not %NUGET_PATH%=="" (
    echo Restoring NuGet packages...
    %NUGET_PATH% restore RevitDebuggerHelper.csproj -NonInteractive
    if %ERRORLEVEL% NEQ 0 (
        echo Warning: Package restore failed, continuing with build...
    )
    echo.
)

echo Building project...
%MSBUILD_PATH% RevitDebuggerHelper.csproj /p:Configuration=Release /p:Platform="Any CPU" /v:minimal /nologo

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful! Helper executable is at:
    echo %~dp0bin\Release\RevitDebuggerHelper.exe
    echo.
    echo Testing the executable...
    if exist "%~dp0bin\Release\RevitDebuggerHelper.exe" (
        echo File exists and is ready to use.
        echo.
        echo Usage examples:
        echo   %~dp0bin\Release\RevitDebuggerHelper.exe --find-revit
        echo   %~dp0bin\Release\RevitDebuggerHelper.exe 12345
    ) else (
        echo Warning: Executable not found at expected location.
    )
) else (
    echo.
    echo Build failed with error code %ERRORLEVEL%
    echo.
)

pause