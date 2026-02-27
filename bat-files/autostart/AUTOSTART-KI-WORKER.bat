@echo off
echo ========================================
echo Starting: GIT, ROBOCOPY, vvvv
echo ========================================

echo(
echo(

echo [0/5] Stopping vvvv if running...
for %%N in ("vvvv.exe" "vvvv.gamma.exe" "vvvv*.exe" "KI_WORKER.exe") do (
    taskkill /F /T /IM "%%~N" >NUL 2>&1 && echo - Killed %%~N
)
echo(

echo [1/5] Running Git Pull...
call "D:\AltePostWien\bat-files\autostart\d_git-pull.bat"
if errorlevel 1 (
    echo WARNING: Git pull returned a non-zero exit (continuing)
)

echo(

echo [2/5] Running Robocopy Sync for source-image...
call "D:\AltePostWien\bat-files\autostart\d_sync_source-image.bat"
set "RC=%ERRORLEVEL%"

echo(
echo --- Robocopy Summary (from child) ---
REM Child already printed SUMMARY lines and the LOG path.

REM Only treat >= 8 as failure (Robocopy semantics)
if %RC% GEQ 8 (
    REM echo ERROR: Robocopy sync failed! (exit %RC%)
	echo ERROR: Robocopy for source-image failed! (exit %RC%) >> D:\_POST_LOGS\autostart_errors.log
    REM exit /b 1
) else (
    echo Robocopy OK (exit %RC%)
)

echo(
echo [3/5] Running Robocopy Sync for comfy-custom-nodes...
call "D:\AltePostWien\bat-files\autostart\d_sync-comfy-custom-nodes.bat"
set "RC=%ERRORLEVEL%"

echo(
echo --- Robocopy Summary (from child) ---
REM Child already printed SUMMARY lines and the LOG path.

REM Only treat >= 8 as failure (Robocopy semantics)
if %RC% GEQ 8 (
    REM echo ERROR: Robocopy sync failed! (exit %RC%)
    echo ERROR: Robocopy for comfy-custom-nodes failed! (exit %RC%) >> D:\_POST_LOGS\autostart_errors.log
    REM exit /b 1
) else (
    echo Robocopy OK (exit %RC%)
)

echo(
echo [3/5] Running Robocopy Sync for comfy-models...
call "D:\AltePostWien\bat-files\autostart\d_sync-comfy-models-and-build.bat"
set "RC=%ERRORLEVEL%"

echo(
echo --- Robocopy Summary (from child) ---
REM Child already printed SUMMARY lines and the LOG path.

REM Only treat >= 8 as failure (Robocopy semantics)
if %RC% GEQ 8 (
    REM echo ERROR: Robocopy sync failed! (exit %RC%)
    echo ERROR: Robocopy for comfy-models failed! (exit %RC%) >> D:\_POST_LOGS\autostart_errors.log
    REM exit /b 1
) else (
    echo Robocopy OK (exit %RC%)
)

echo(
echo [4/5] Running Robocopy Sync for KI build...
call "D:\AltePostWien\bat-files\autostart\d_sync_builds.bat"
set "RC=%ERRORLEVEL%"

echo(
echo --- Robocopy Summary (from child) ---
if %RC% GEQ 8 (
    echo ERROR: Robocopy for KI_WORKER build failed! (exit %RC%) >> D:\_POST_LOGS\autostart_errors.log
) else (
    echo Robocopy OK (exit %RC%)
)

echo(
echo [5/5] Starting KI_WORKER build...
call "D:\AltePostWien\bat-files\autostart\d_start-build-ki-worker.bat"

echo(
echo ========================================
echo All tasks completed!
echo ========================================
pause
