@echo off
REM Start Max/MSP and open a specific patch
set MAX_PATH="C:\Program Files\Cycling '74\Max 9\Max.exe"
set PATCH_PATH="C:\AP_MAX\APO_Main\APO_main075_4_in.maxpat"

REM Run Max with the patch
start "" %MAX_PATH% --disable-recovery %PATCH_PATH%