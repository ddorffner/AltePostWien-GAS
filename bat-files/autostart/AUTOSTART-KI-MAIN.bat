@echo off
echo ========================================
echo Starting
echo ========================================

echo [1/3] Running Git Pull...
call "C:\Users\SHA.ART\Documents\AltePost\AltePostWien\bat-files\autostart\git-pull.bat"
if errorlevel 1 (
    echo ERROR: Git pull failed!
    pause
    exit /b 1
)

echo [2/3] Running Robocopy Sync...
call "C:\Users\SHA.ART\Documents\AltePost\AltePostWien\bat-files\autostart\sync_source-image.bat"
if errorlevel 1 (
    echo ERROR: Robocopy sync failed!
    pause
    exit /b 1
)

echo [3/3] Starting vvvv...
call "C:\Users\SHA.ART\Documents\AltePost\AltePostWien\bat-files\autostart\start-patch-ki-main.bat"

echo(
echo ========================================
echo All tasks completed!
echo ========================================
pause