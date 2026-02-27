timeout /t 5 /nobreak >nul
@echo off
cd "C:\Program Files\vvvv\vvvv_gamma_7.0-win-x64\"
start vvvv.exe -o "C:\Users\SHA.ART\Documents\AltePost\AltePostWien\WatchDog-AltePost\WatchDog_Operator_Tracking.vl" --allowmultiple --package-repositories C:\Users\SHA.ART\Documents\vvvv\vl-libs --editable-packages VL.Fuse*