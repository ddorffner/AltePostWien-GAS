@echo off
setlocal

:: Set your source and destination paths here
set "SOURCE_PATH=C:\Asset-GAS-02\Image"
set "DEST_PATH=\\10.3.91.11\data-pg8\Asset-GAS-02\Image"

:: Display the paths being used
echo.
echo Copying XML files ending with "Tags.xml"...
echo Source: %SOURCE_PATH%
echo Destination: %DEST_PATH%
echo.

:: Create destination folder if it doesn't exist
if not exist "%DEST_PATH%" (
    echo Creating destination folder...
    mkdir "%DEST_PATH%"
)

:: Use robocopy to copy files matching the pattern
:: /S = copy subdirectories (but not empty ones)
:: /NDL = no directory list (cleaner output)
:: /NP = no progress indicator
:: /R:3 = retry 3 times on failed copies
:: /W:1 = wait 1 second between retries
echo Starting copy operation...
robocopy "%SOURCE_PATH%" "%DEST_PATH%" *Tags.xml /S /NDL /NP /R:3 /W:1

:: Check the result
if %ERRORLEVEL% LEQ 1 (
    echo.
    echo Copy operation completed successfully!
) else (
    echo.
    echo Copy operation completed with some issues. Error level: %ERRORLEVEL%
)

echo.
pause
