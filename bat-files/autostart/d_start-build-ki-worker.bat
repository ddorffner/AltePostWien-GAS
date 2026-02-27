@echo off
set "BUILD_DIR=D:\KI\SYNC\builds"
set "BUILD_EXE=%BUILD_DIR%\KI_WORKER\KI_WORKER.exe"

REM Check if build folder exists
if not exist "%BUILD_DIR%\" (
    echo ERROR: Build folder not found at %BUILD_DIR%
    pause
    exit /b 1
)

REM Check if build exe exists (vvvv gamma export)
if not exist "%BUILD_EXE%" (
    echo ERROR: Build exe not found at %BUILD_EXE%
    pause
    exit /b 1
)

REM Start KI_WORKER build
echo Starting KI_WORKER build...
start "" "%BUILD_EXE%" -m
