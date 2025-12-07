@echo off
REM InitialSetup.bat - Quick start script for AppiumBootstrap Windows Agent

echo ========================================
echo Appium Device Monitor - Windows Agent
echo ========================================
echo.

REM Check for Administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script requires Administrator privileges.
    echo Right-click and select "Run as Administrator"
    pause
    exit /b 1
)

REM Set installation directory
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%..\..\..\"
set INSTALL_DIR=%CD%
set APPIUM_HOME=%INSTALL_DIR%\appium-home

echo Installation Directory: %INSTALL_DIR%
echo APPIUM_HOME: %APPIUM_HOME%
echo.

REM Check if Appium is installed
if not exist "%APPIUM_HOME%\node_modules\.bin\appium.cmd" (
    echo Appium not found. Running installation...
    echo.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_DIR%\Platform\Windows\Scripts\InstallDependencies.ps1"
    
    if %errorlevel% neq 0 (
        echo.
        echo Installation failed. Please check the logs.
        pause
        exit /b 1
    )
)

REM Check if NSSM is installed
if not exist "%INSTALL_DIR%\nssm\nssm.exe" (
    echo NSSM not found. Running service setup...
    echo.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_DIR%\Platform\Windows\Scripts\ServiceSetup.ps1"
    
    if %errorlevel% neq 0 (
        echo.
        echo Service setup failed. Please check the logs.
        pause
        exit /b 1
    )
)

echo.
echo ========================================
echo Setup Complete!
echo ========================================
echo.
echo To start the Appium agent, run:
echo   AppiumBootstrapInstaller.exe
echo.
echo To test Appium installation:
echo   %APPIUM_HOME%\node_modules\.bin\appium.cmd driver list
echo.
echo To check connected devices:
echo   adb devices
echo.
pause
