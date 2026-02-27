@echo off
set VVVV_PATH=C:\Program Files\vvvv\vvvv_gamma_7.1-0059-gb290001154-win-x64\vvvv.exe
set PATCH_PATH=D:\AltePostWien\KI\KI_WORKER.vl

REM Check if VVVV exists
if not exist "%VVVV_PATH%" (
    echo ERROR: VVVV not found at %VVVV_PATH%
    pause
    exit /b 1
)

REM Check if patch exists
if not exist "%PATCH_PATH%" (
    echo ERROR: Patch not found at %PATCH_PATH%
    pause
    exit /b 1
)

REM Start VVVV with patch
echo Starting VVVV Gamma patch with allow-multiple...
start "" "%VVVV_PATH%" -m -o "%PATCH_PATH%"