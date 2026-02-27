# ============================================================
#   SyncTime.ps1
#   Automatische Zeitsynchronisation für Windows 11
#   - Setzt Zeitzone auf UTC+01:00 (Amsterdam/Berlin/Bern/Rom/Wien)
#   - Stellt den Windows-Zeitdienst sicher
#   - Konfiguriert externen Zeitserver (pool.ntp.org)
#   - Synchronisiert die Systemzeit
#   - Erstellt ein Logfile
# ============================================================

# ----- Einstellungen -----
$LogFile = "C:\LogsTimeSync\TimeSync.log"
$NtpServer = "pool.ntp.org"

# ----- Ordner für Logfile erstellen -----
$LogDir = Split-Path $LogFile
if (!(Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir | Out-Null
}

function Log($msg) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "[$timestamp] $msg" | Out-File $LogFile -Append
}

Log "===== Zeitsynchronisierung gestartet ====="

# ----- Zeitzone setzen -----
try {
    Set-TimeZone -Id "W. Europe Standard Time"
    Log "Zeitzone gesetzt: W. Europe Standard Time (UTC+01:00)"
}
catch {
    Log "Fehler beim Setzen der Zeitzone: $($_.Exception.Message)"
}

# ----- NTP-Server konfigurieren -----
try {
    w32tm /config /manualpeerlist:$NtpServer /syncfromflags:manual /update | Out-Null
    Log "NTP-Server gesetzt: $NtpServer"
}
catch {
    Log "Fehler beim Setzen des NTP-Servers: $($_.Exception.Message)"
}

# ----- Windows-Zeitdienst starten -----
try {
    Start-Service W32Time -ErrorAction Stop
    Log "W32Time Dienst gestartet oder war bereits aktiv."
}
catch {
    Log "Fehler beim Starten des Zeitdienstes: $($_.Exception.Message)"
}

# ----- Synchronisieren -----
try {
    w32tm /resync /force | Out-Null
    Log "Zeitsynchronisation erfolgreich durchgeführt."
}
catch {
    Log "Fehler bei der Zeitsynchronisation: $($_.Exception.Message)"
}

Log "===== Zeitsynchronisierung abgeschlossen ====="
