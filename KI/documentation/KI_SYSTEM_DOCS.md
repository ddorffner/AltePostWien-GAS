# KI System - Technische Dokumentation

**Alte Post Wien**

Diese Dokumentation beschreibt die technische Architektur, Infrastruktur und Wartungsprozesse des KI-Bildgenerierungssystems.

---

## 1. Systemüberblick

Das System ist eine verteilte Anwendung zur parallelen Bildgenerierung mittels ComfyUI. Es besteht aus einer zentralen Steuereinheit (Control PC) und mehreren Worker PCs.

- **Kommunikation**: Redis Datenbank (Pub/Sub & Keys).
- **Software Basis**: vvvv gamma, ComfyUI, Python.
- **Netzwerk**: Lokales LAN.

---

## 2. Hardware & Rollenverteilung

### AI Control PC

- **Hostname**: `KI-2-DMN`
- **Software**: `KI_MAIN_GUI.vl`
- **Aufgaben**:
  - **Redis Server**: Hostet die Redis-Instanz für die gesamte Kommunikation.
  - **Job Management**: Erstellt Jobs aus User-Input und legt sie in die Queue.
  - **Monitoring**: Überwacht Status der Worker.

### AI Worker PCs

Rechenknoten, die die eigentliche Bildgenerierung durchführen.

- **Hostnames**:
  - `KI-1-DMN`
  - `KI-3-DMN`
  - `KI-1-PG8`
  - `KI-2-PG8`
  - `KI-3-PG8`
- **Software**: `KI_WORKER.vl`
- **Aufgaben**:
  - **Redis Client**: Holt Jobs aus der Queue.
  - **ComfyUI Host**: Führt die lokale ComfyUI Instanz aus (API Mode).
  - **Reporting**: Meldet Status (Idle/Processing) und Logs an den Control PC.

---

## 3. Datenfluss & Speicherstruktur

### 3.1 Generierungsprozess

1.  **Input**: Quellbilder liegen auf dem Netzlaufwerk.
2.  **Generate**:
    - Control PC sendet Job-JSON an Redis.
    - Freier Worker übernimmt Job.
    - ComfyUI generiert Bild und speichert es als `image_name_ORIG.png` in ComfyUI-Output auf dem NAS.
3.  **Upscale**:
    - Worker lädt `*_ORIG.png`.
    - Führt Upscale und Depth-Calculation aus.
    - Speichert Ergebnisse in Unterordner `OUTPUT_FOLDER/image_name/`.

### 3.2 Finaler Asset Folder

Der finale Asset-Ordner für die Medienwiedergabe (BRAIN) muss folgende Struktur pro Quellbild aufweisen:

**Ordner**: `[Asset_Name]/`

- `[ROOM]_Color.png` - Projektionsbild (Upscaled)
- `[ROOM]_Depth.exr` - Tiefeninformation (32-bit float)
- `[ROOM]_Thumb.png` - Vorschau
- `[ROOM]_Comfy.png` - Originalauflösung mit Metadaten
- `Classification.xml` - KI-Rating (0-1) für 40 Labels
- `Tags.xml` - Manuelle Tags
- `ColorPalette.xml` - Dominante Farben

_Hinweis_: `GAS` (Gastronomie) Assets werden in einem separaten Verzeichnisbaum gespeichert.

### 3.3 Externe Tools

- **Nodetool**: Eigenständige Applikation zur Analyse der generierten Bilder. Erstellt die `Classification.xml`.
- **Sync**: Die XML-Dateien werden mittels Hilfsskript (im Utils-Tab der Main GUI) in korrekt benannte Ordner kopiert.

---

## 4. Autostart & Wartung

Das System ist für automatischen Start und Synchronisation konfiguriert. Batch-Skripte befinden sich im Ordner `bat-files/autostart/`.

### 4.1 Main PC (`AUTOSTART-KI-MAIN.bat`)

Der Startvorgang führt folgende Schritte sequenziell aus:

1.  **Git Pull**: Aktualisiert das `AltePostWien` Repository.
2.  **Robocopy**: Synchronisiert Quellbilder vom Server/Master.
3.  **Start vvvv**: Startet `ki-main-gui.vl`.

### 4.2 Worker PCs (`AUTOSTART-KI-WORKER.bat`)

Worker-Start:

1.  **Stop vvvv**: Beendet eventuell laufende vvvv/Comfy Prozesse.
2.  **Git Pull**: Aktualisiert Codebasis.
3.  **Robocopy Sync**:
    - `Source Images`: Quellbilder.
    - `Comfy Custom Nodes`: Stellt sicher, dass alle Worker dieselben Nodes haben.
    - `Comfy Models`: Synchronisiert Models und LORAs.
4.  **Start vvvv**: Startet `ki-worker.vl` (welches wiederum ComfyUI startet).

### 4.3 Manuelle Eingriffe

- **Worker deaktivieren**: Über den "Worker" Tab in der Main GUI können einzelne PCs temporär aus der Verteilung genommen werden, ohne das System zu stoppen.
- **Logs**: Fehlermeldungen der Autostart-Skripte werden in `D:\_POST_LOGS\autostart_errors.log` (auf den jeweiligen Maschinen) geschrieben.
