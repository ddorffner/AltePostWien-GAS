@echo off
setlocal
chcp 1252 >nul

REM --- Define possible sources ---
SET "SOURCE1=\\10.3.92.10\DATA-PG8\Asset"
SET "SOURCE2=\\10.1.91.11\DATA-PG8\Asset"
SET "SOURCE3=\\10.3.91.11\DATA-PG8\Asset"
SET "DEST=C:\Asset"

REM --- Pick whichever source exists ---
if exist "%SOURCE1%\" (
  SET "SOURCE=%SOURCE1%"
) else if exist "%SOURCE2%\" (
  SET "SOURCE=%SOURCE2%"
) else if exist "%SOURCE3%\" (
  SET "SOURCE=%SOURCE3%"
) else (
  echo ERROR: Neither NAS path is available.
  exit /b 3
)


REM --- Header ---
(
  echo ==== %DATE% %TIME% - Start Sync ====
  echo SOURCE: %SOURCE%
  echo DEST  : %DEST%
)

REM --- Validate source; fail fast ---
if not exist "%SOURCE%\" (
  echo ERROR: Source path not found: "%SOURCE%"
  exit /b 3
)

if not exist "%DEST%" mkdir "%DEST%" 2>nul

REM --- Robocopy (NAS-friendly switches) ---
robocopy "%SOURCE%" "%DEST%" ^
  /MIR /COPY:DAT /DCOPY:T /Z /FFT /XJ /R:0 /W:0 /NP /MT:16 /XF Thumbs.db Tags.xml ColorPalette.xml

set "RC1=%ERRORLEVEL%"

REM --- Second pass: copy ColorPalette.xml only if missing (no overwrites) ---
robocopy "%SOURCE%" "%DEST%" ^
  ColorPalette.xml ^
  /E /COPY:DAT /DCOPY:T /Z /FFT /XJ /R:0 /W:0 /NP /MT:16 /XC /XN /XO

set "RC2=%ERRORLEVEL%"

REM --- Print Robocopy’s own totals (no translation) ---
REM (No logfile used; Robocopy output shown above)

REM --- Normalize exit code: 0..7 = OK/warnings, >=8 = errors ---
set "RC=0"
if %RC1% GEQ 8 set "RC=%RC1%"
if %RC2% GEQ 8 set "RC=%RC2%"
if %RC% GEQ 8 (
  echo ERROR: Robocopy encountered errors.
  exit /b %RC%
) else (
  echo Asset Image Sync completed successfully
  exit /b 0
)
