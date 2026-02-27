# ABOUT

This folder contains all files related to autostart.
Capital bat files are for specific computers:
AUTOSTART-GROUP-TYPE.bat

Example:
AUTOSTART-KI-MAIN.bat

Those AUTOSTART.bat files make use of the lowercase bat files to:

- git pull
- robocopy
- start a specific vvvv patch or exe

To add a new AUTOSTART for a specific computer, copy the existing files as needed and rename, then change the paths inside.

Afterwards, these 3 step are needed to
- enable git pull
- execute the AUTOSTART.bat on windows startup
- enable logging to file

# STEP 1: SETUP SSH FOR GIT AUTOPULL

On Windows 11, the SSH agent service is already included,
but it's disabled by default.
Here's how to enable it:

One-Time Setup (PowerShell as Administrator):

## 1.1:

Copy Z:\_SSH into %USERPROFILE%\.ssh\ folder.
Include the post/ folder: .ssh\post\keys_here

## 1.2: Open PowerShell as Admin

Right-click Start menu → "Terminal (Admin)" or "Windows PowerShell (Admin)"

## 1.3: Enable and Start SSH Agent

powershell# Set the service to start automatically
Get-Service ssh-agent | Set-Service -StartupType Automatic

### Start the service now

Start-Service ssh-agent

### Verify it's running

Get-Service ssh-agent
You should see:
Status Name DisplayName

---

Running ssh-agent OpenSSH Authentication Agent

## 1.4: Add Your SSH Key (One Time)

#powershell
ssh-add $env:USERPROFILE\.ssh\post\id_ed25519

## 1.5: Verify Key is Added

#powershell
ssh-add -l

# STEP 2: ADD AUTOSTART

Create a shortcut in Startup folder:
Go to
%USERPROFILE%\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup
Create shortcut to AUTOSTART-X-Y.bat

# STEP 3: CREATE LOGS FOLDER
add C:\_POST_LOGS - without it, robocopy will complain