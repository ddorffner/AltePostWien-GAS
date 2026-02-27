@echo off
rem Steps: stash +untracked, pull --ff-only; on fail: backup, fetch, hard reset
rem WARNING: does git stash, pull - so local changes have to be retrieved from stash history
setlocal EnableExtensions EnableDelayedExpansion

rem === CONFIG ===
set "REPO=D:\AltePostWien"


rem Use Windows OpenSSH (forward slashes avoid sh-quoting issues)
set "OPENSSH=C:/Windows/System32/OpenSSH"
set "GIT_SSH=%OPENSSH%/ssh.exe"
set "GIT_TERMINAL_PROMPT=0"

rem Prepare timestamp safe for file/branch names
for /f "usebackq delims=" %%t in (`powershell -NoProfile -Command "(Get-Date).ToString('yyyyMMdd_HHmmss')"`) do set "TS_SAFE=%%t"

set "TS=[%DATE% %TIME%]"
echo %TS% Starting git-pull for "%REPO%"

rem Start agent (quiet even if already running)
sc start ssh-agent >NUL 2>&1

rem Add a key only if none is loaded (quiet)
"%OPENSSH%/ssh-add.exe" -l >NUL 2>&1
if errorlevel 1 "%OPENSSH%/ssh-add.exe" "%USERPROFILE%\.ssh\id_ed25519" >NUL 2>&1

rem Optional small wait for network
ping 127.0.0.1 -n 3 >NUL

rem === Preflight checks ===
where git >NUL 2>&1
if errorlevel 1 (
  echo ERROR: git not found in PATH
  set "RC=10"
  goto :finish
)

if not exist "%OPENSSH%/ssh.exe" (
  echo ERROR: ssh.exe not found at %OPENSSH%
  set "RC=11"
  goto :finish
)

if not exist "%REPO%" (
  echo ERROR: REPO path not found: %REPO%
  set "RC=12"
  goto :finish
)

git -C "%REPO%" rev-parse --is-inside-work-tree >NUL 2>&1
if errorlevel 1 (
  echo ERROR: Not a git repository: %REPO%
  set "RC=13"
  goto :finish
)

rem Try to ensure git-lfs is in PATH (common install locations)
where git-lfs >NUL 2>&1
if errorlevel 1 (
  if exist "%ProgramFiles%\Git LFS\git-lfs.exe" set "PATH=%PATH%;%ProgramFiles%\Git LFS"
  if exist "%ProgramFiles%\Git\mingw64\bin\git-lfs.exe" set "PATH=%PATH%;%ProgramFiles%\Git\mingw64\bin"
  if exist "%ProgramFiles(x86)%\Git LFS\git-lfs.exe" set "PATH=%PATH%;%ProgramFiles(x86)%\Git LFS"
  if exist "%ProgramFiles(x86)%\Git\mingw64\bin\git-lfs.exe" set "PATH=%PATH%;%ProgramFiles(x86)%\Git\mingw64\bin"
)

rem Ensure Git Credential Manager is available for LFS HTTPS auth
where git-credential-manager-core >NUL 2>&1
if errorlevel 1 (
  if exist "%ProgramFiles%\Git\mingw64\libexec\git-core\git-credential-manager-core.exe" set "PATH=%PATH%;%ProgramFiles%\Git\mingw64\libexec\git-core"
  if exist "%ProgramFiles(x86)%\Git\mingw64\libexec\git-core\git-credential-manager-core.exe" set "PATH=%PATH%;%ProgramFiles(x86)%\Git\mingw64\libexec\git-core"
)
where git-credential-manager >NUL 2>&1
if errorlevel 1 (
  if exist "%ProgramFiles%\Git\mingw64\libexec\git-core\git-credential-manager.exe" set "PATH=%PATH%;%ProgramFiles%\Git\mingw64\libexec\git-core"
  if exist "%ProgramFiles(x86)%\Git\mingw64\libexec\git-core\git-credential-manager.exe" set "PATH=%PATH%;%ProgramFiles(x86)%\Git\mingw64\libexec\git-core"
)

rem Detect git-lfs availability
set "LFS_AVAILABLE=0"
git -C "%REPO%" lfs version >NUL 2>&1
if not errorlevel 1 set "LFS_AVAILABLE=1"
if "%LFS_AVAILABLE%"=="1" (
  echo git-lfs detected; will populate LFS files after pull
  git -C "%REPO%" lfs install --local >NUL 2>&1
) else (
  echo git-lfs NOT detected; pulling without LFS content
)

rem Decide whether to attempt LFS pull later
set "ENABLE_LFS_PULL=0"
if "%LFS_AVAILABLE%"=="1" set "ENABLE_LFS_PULL=1"
where git-credential-manager-core >NUL 2>&1
if errorlevel 1 (
  where git-credential-manager >NUL 2>&1
  if errorlevel 1 set "ENABLE_LFS_PULL=0"
)

rem Avoid blocking on LFS during pulls/resets
set "GIT_LFS_SKIP_SMUDGE=1"
echo Using GIT_LFS_SKIP_SMUDGE=1 for update

rem === Git workflow ===
rem Stash local changes (tracked + untracked) to avoid conflicts
git -C "%REPO%" stash push -u -m "Auto-stash before pull" >NUL 2>&1

rem Retryable fast-forward pull via SSH-mapped URL
set "MAX_RETRIES=3"
set "DELAY_SEC=5"
set /a ATTEMPT=1

:pull_retry
echo Attempt !ATTEMPT!: git pull --ff-only
git -c credential.helper= -c credential.interactive=never -c url."ssh://git@github.com/".insteadOf=https://github.com/ -C "%REPO%" pull --ff-only
set "RC=%ERRORLEVEL%"
if %RC% EQU 0 goto :success

if !ATTEMPT! GEQ !MAX_RETRIES! goto :fallback
echo Pull failed RC=!RC!, retrying after !DELAY_SEC!s
rem Clear any partial merge/index state from previous attempt
git -C "%REPO%" reset --merge >NUL 2>&1
ping 127.0.0.1 -n !DELAY_SEC! >NUL
set /a ATTEMPT+=1
goto :pull_retry

:fallback
echo Pull failed after !MAX_RETRIES! attempts (RC=!RC!). Fallback starting...

rem Determine upstream (defaults to origin/main)
set "UPSTREAM="
for /f "usebackq tokens=*" %%u in (`git -C "%REPO%" rev-parse --abbrev-ref --symbolic-full-name @{u} 2^>NUL`) do set "UPSTREAM=%%u"
if not defined UPSTREAM set "UPSTREAM=origin/main"

rem Backup current HEAD to a branch
git -C "%REPO%" branch "autobak/%TS_SAFE%" >NUL 2>&1

rem Fetch and hard reset
git -C "%REPO%" fetch --all --prune >NUL 2>&1
git -C "%REPO%" reset --hard "%UPSTREAM%"
set "RC=%ERRORLEVEL%"
if %RC% EQU 0 goto :success

echo Fallback reset failed RC=%RC%
goto :finish

:success
echo SUCCESS
set "RC=0"

rem Populate LFS content if available (non-fatal on failure)
if "%ENABLE_LFS_PULL%"=="1" (
  echo Pulling LFS content...
  git -C "%REPO%" lfs pull
  if errorlevel 1 (
    echo WARNING: git lfs pull failed; content will be fetched later or on demand
  )
) else (
  echo Skipping LFS pull (git-lfs or credential helper not available)
)

:finish
endlocal & exit /b %RC%
