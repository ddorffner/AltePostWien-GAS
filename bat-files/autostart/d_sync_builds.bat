@echo off
setlocal
chcp 1252 >nul

REM --- Define possible sources (KI_WORKER build folder) ---
SET "SOURCE1=\\10.3.92.10\DATA-PG8\KI\SYNC\builds"
SET "SOURCE2=\\10.1.91.11\DATA-PG8\KI\SYNC\builds"
SET "SOURCE3=\\10.3.91.11\DATA-PG8\KI\SYNC\builds"
SET "DEST=D:\KI\SYNC\builds"

REM --- Pick whichever source exists ---
if exist "%SOURCE1%\" (
  SET "SOURCE=%SOURCE1%"
) else if exist "%SOURCE2%\" (
  SET "SOURCE=%SOURCE2%"
) else if exist "%SOURCE3%\" (
  SET "SOURCE=%SOURCE3%"
) else (
  echo ERROR: Neither NAS path for KI build is available.
  exit /b 3
)


REM --- Timestamp for logfile (de-DE friendly; pads hour) ---
set "STAMP=%date:~6,4%-%date:~3,2%-%date:~0,2%_%time:~0,2%-%time:~3,2%-%time:~6,2%"
set "STAMP=%STAMP: =0%"

SET "LOGFILE=D:\_POST_LOGS\robocopy_sync_builds_%STAMP%.log"

REM --- Header ---
(
  echo ==== %DATE% %TIME% - Start Sync (builds) ====
  echo SOURCE: %SOURCE%
  echo DEST  : %DEST%
) > "%LOGFILE%"

REM --- Validate source; fail fast ---
if not exist "%SOURCE%\" (
  echo ERROR: Source path not found: "%SOURCE%"
  echo LOG   : %LOGFILE%
  exit /b 3
)

if not exist "%DEST%" mkdir "%DEST%" 2>nul

REM --- Robocopy (NAS-friendly switches) ---
robocopy "%SOURCE%" "%DEST%" ^
  /MIR /COPY:DAT /DCOPY:T /Z /FFT /XJ /R:0 /W:0 /NP /MT:16 /XF Thumbs.db ^
  >> "%LOGFILE%" 2>&1

set "RC=%ERRORLEVEL%"

REM --- Print Robocopy's own totals (no translation) ---
echo LOG   : %LOGFILE%
for /f "usebackq tokens=*" %%L in (`findstr /i /c:"Verzeich." /c:"Directories:" "%LOGFILE%"`) do echo SUMMARY: %%L
for /f "usebackq tokens=*" %%L in (`findstr /i /c:"Ausgeschl. Dateien:" /c:"Excluded Files:" "%LOGFILE%"`) do echo SUMMARY: %%L
for /f "usebackq tokens=*" %%L in (`findstr /i /c:"Dateien:" /c:"Files:" "%LOGFILE%"`) do echo SUMMARY: %%L
for /f "usebackq tokens=*" %%L in (`findstr /i /c:"Bytes:" "%LOGFILE%"`) do echo SUMMARY: %%L

REM --- Normalize exit code: 0..7 = OK/warnings, >=8 = errors ---
if %RC% GEQ 8 (
  echo ERROR: Robocopy encountered errors. See "%LOGFILE%"
  exit /b %RC%
) else (
  echo KI build sync completed successfully
  exit /b 0
)
