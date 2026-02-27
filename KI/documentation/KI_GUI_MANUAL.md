# KI Applikation - Benutzerhandbuch

**Alte Post Wien**

Dieses Handbuch beschreibt die Bedienung der vvvv gamma Applikation zur KI-gestützten Bildgenerierung für die Installation "Alte Post Wien".

---

## 1. Übersicht

Die Applikation dient als zentrale Schnittstelle (Control Center) zur Steuerung mehrerer Worker-PCs, die rechenintensive Bildgenerierungsprozesse (ComfyUI) ausführen.

Die Benutzeroberfläche ist in Tabs unterteilt, die den Arbeitsablauf von der Generierung bis zur Kontrolle abbilden.

---

## 2. Bildgenerierung (Tab: Generate)

Der **Generate** Tab ist der Hauptarbeitsbereich zum Erstellen neuer Bilder und zum Starten von Upscale-Prozessen.

![Generate Tab](images/KI_01_generate.png)

### Workflow

1.  **Quellbilder wählen**: Links im File-Browser Auswahl des Ordners mit den Quellbildern.
2.  **Räume wählen**: Aktivierung der Zielräume (`DGN`, `ENT`, `HOF` oder `GAS`), für die Bilder generiert werden sollen. Diese können gleichzeitig berechnet werden.
    - _Hinweis_: Da der Raum `GAS` andere Quellbilder nutzt, wird dieser üblicherweise in einem separaten Durchgang bearbeitet.
3.  **Parameter einstellen**:
    - Laden eines **Presets** (rechte Seite), um gespeicherte Einstellungen abzurufen.
    - Anpassung der Parameter (Prompt, Seed, LORA) bei Bedarf.
    - Verwendung von String-Templates (siehe Hilfe), um Pfade oder Zeitstempel dynamisch zu setzen.
4.  **Job starten**: Klick auf `Generate`, um die Aufträge an die Worker zu senden.

### Output

Die generierten Rohbilder werden als `image_name_ORIG.png` im konfigurierten Output-Ordner gespeichert.

---

## 3. Upscaling (Upscale Mode)

Das Hochskalieren (Upscaling) wandelt die generierten Bilder in die finale Auflösung um und erzeugt zusätzlich Depth Maps für die Blenden und Thumbs für die Vorschau im BRAIN.

![Upscale Settings](images/KI_02_upscale.png)

### Workflow

1.  **Modus aktivieren**:
    - Laden eines Presets mit "Upscale" im Namen. Dies aktiviert automatisch den **Upscale Toggle**.
    - Alternativ: Manuelle Aktivierung des Toggles.
2.  **Input wählen**: Auswahl des Ordners mit den zuvor generierten `*_ORIG.png` Bildern.
3.  **Starten**: Klick auf `Generate`. Der Prozess läuft nun für alle Bilder im Ordner, unabhängig vom Raum.

### Output Struktur

Der Upscale-Prozess erstellt für jedes Bild einen Unterordner `image_name/` mit folgenden Dateien:

- `[ROOM]_Color.png` (Finale Projektion)
- `[ROOM]_Depth.exr` (Tiefeninformation)
- `[ROOM]_Thumb.png` (Vorschau für BRAIN)
- `[ROOM]_Comfy.png` (Original mit Metadaten)

---

## 4. Monitoring (Tabs: Jobs & Worker)

### Jobs Tab

Anzeige der Warteschlange (Queue) aller Aufträge.

![Jobs Tab](images/KI_03_jobs.png)

- **Status**: Zeigt an, welche Jobs "Pending" (wartend) oder "Processing" (in Bearbeitung) sind.
- **Aktionen**:
  - `CLEAR ALL`: Löschen aller wartenden Jobs aus der Liste.
  - `Copy Request`: Kopieren der JSON-Anfrage eines Jobs in die Zwischenablage (für Debugging).

### Worker Tab

Überwachung und Steuerung der Worker PCs.

![Worker Tab](images/KI_03_worker.png)

- **Status**: Zeigt, ob ein Worker `IDLE` (bereit) oder `PROCESSING` (beschäftigt) ist.
- **Logs**: Zeigt die letzten Rückmeldungen und Fehlermeldungen der Worker.
- **Steuerung**: Einzelne Worker können bei Bedarf deaktiviert werden (Enabled).

---

## 5. Ergebniskontrolle (Tab: Viewer)

Der Viewer ermöglicht den direkten Vergleich zwischen Quellbild und Ergebnis für alle Räume.

![Viewer Tab](images/KI_04_viewer.png)

- **Ansicht**: Rechts das Quellbild, daneben die jeweiligen Räume.
- **Modus**: Mit dem Button `UPSCALE` (Toggle) wird die Ansicht zwischen den `ROOM_ORIG.png` Entwürfen und den fertig hochskalierten Bildern gewechselt `ROOM_COLOR.png`.

---

## 6. Konfiguration & Hilfsmittel

### Files (Tab: Files)

Hier werden die ComfyUI JSON-Workflows den jeweiligen Räumen zugewiesen. Im Normalbetrieb müssen diese Einstellungen nicht geändert werden.

![Files Tab](images/KI_05_files.png)

### Utils (Tab: Utils)

Sammlung von Hilfswerkzeugen für das Asset-Management.

![Utils Tab](images/KI_06_utils.png)

#### Clean Source Image Folder

- Bereitet die Quellbilder vor.
- Benennt Dateien um und skaliert sie auf maximal 2400px.
- Erstellt einen neuen, bereinigten Ordner.

#### Copy Source Images

- Kopiert die Quellbilder vom `Quellbild Folder` in den `Upscaled Folder`.
- Stellt sicher, dass die finalen Asset-Ordner auch das Originalbild enthalten.

#### Copy Comfy Images

- Kopiert die originalen ComfyUI-Bilder (`*_ORIG.png`) vom `Original Folder` in den `Upscaled Folder`.
- Wichtig für die Archivierung der Metadaten.

#### ColorPalette

- Analysiert die Bilder im `Upscaled Folder`.
- Erstellt die `ColorPalette.xml` mit den dominanten Farben für jedes Asset.

#### Copy Classification Files To Folders

- Verteilt die Klassifikationsdaten.
- Nimmt XML-Dateien aus dem `Classifications Folder` (Output des Nodetools).
- Erstellt benannte Unterordner für jedes Asset und benennt die XML zu `Classification.xml` um.

_Hinweis_: Viele dieser Tools bieten eine "Simulate"-Option, um den Vorgang vor der Ausführung zu testen (Dry Run).

### Help (Tab: Help)

Interne Referenz für den Operator.

- Enthält kopierbare Textbausteine (String Templates) für Parameter.
- Liste verfügbarer LORA-Namen.

![Help Tab](images/KI_07_help.png)
