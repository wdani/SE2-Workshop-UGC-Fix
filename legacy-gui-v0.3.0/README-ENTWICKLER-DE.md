# SE2 Workshop UGC Fix v0.3.0 – Entwicklungsstand

## Ziel

Unabhängiges, einfach bedienbares Windows-Werkzeug für genau den bekannten SE2-Fehler: Steam UGC Developer Metadata > 5.000 UTF-8-Bytes durch eine doppelt eingebettete ModHUB-Beschreibung (`FullMetadata.BaseMetadata.Description`).

Keine allgemeine Steam-Reparatur, keine automatischen Uploads.

## Änderungen gegenüber v0.1

- Reparatur-Schaltfläche ist vor gültiger Diagnose nicht nur funktional deaktiviert, sondern **visuell grau** mit der Aufschrift „CHECK FIRST“.
- Nach einer Änderung der Workshop-ID wird ein vorheriges Prüfergebnis sofort verworfen, ebenso die angezeigte Sicherung und der kopierbare Report.
- Separater „COPY SAFE REPORT“-Button, der nur die zusammenfassenden Felder ohne rohe Metadaten in die Zwischenablage kopiert.
- `MetadataPatch.cs`: Eine einzige, reine C#-Validierungsfunktion wird sowohl beim Scan als auch direkt vor der tatsächlichen Reparatur verwendet. Dadurch kann die reale Prüflogik unabhängig von Steam getestet werden.
- `tests/MetadataSafetyTests/`: 15 C#-Sicherheitstests zu normaler, defekter und abweichender Metadata, die ohne Steam und ohne Workshop-Schreibzugriff laufen.
- Original- und Kandidatensicherung müssen vor dem Upload weiterhin vorhanden und **inhaltlich unverändert** sein.
- `BUILD-EXE.cmd`: Tests vor Publish, danach Erstellung eines einfachen ZIP-Pakets mit EXE, englischer Anleitung und SHA-256.

## Ausführen auf deinem PC

`BUILD-EXE.cmd` starten. Bei Erfolg: `Release/SE2-Workshop-UGC-Fix-v0.3.0-Windows-x64.zip`.

**Vor Weitergabe an AngryWolf:** Das echte Windows-Build und die GUI-Abfrage durchführen (z. B. mit einem bereits reparierten eigenen Workshop-Eintrag: soll ein Ergebnis kleiner 5.000 Bytes und **keine freigegebene Reparatur** zeigen), das lokale Backup kontrollieren und die EXE in einem frischen Ordner starten.

Achtung: Ein erfolgreicher Build ist noch kein realer Steam-Laufzeittest. Einen fehlerhaften Fremd-Workshop-Eintrag nur mit Eigentümerzustimmung und nach unabhängiger Sicherung verändern.

## Datenschutz / Vertrauen

Kein Admin erforderlich, kein Passwort, kein Steam-Web-API-Schlüssel, keine versteckten Netzwerkendpunkte. Die Steamworks-API verwendet die lokal laufende Steam-Sitzung. Code ist einsehbar; das veröffentlichte Binary kann als SHA-256 geprüft werden. Die EXE ist nicht digital signiert, daher kann SmartScreen warnen.

## Neu v0.3.0: Beschreibung vor Veröffentlichung prüfen

Mit **DESCRIPTION PREFLIGHT** lässt sich ein geplanter Workshop-Beschreibungstext lokal schätzen (UTF-8-/JSON-Bytes). Wenn bereits eine Mod geprüft wurde, verwendet der Preflight deren internes JSON als Vorlage. Ohne Scan wird ausdrücklich nur ein Beispiel-Basiswert genutzt. Lange öffentliche Beschreibungen verursachen den Fehler nicht automatisch; die Prognose gilt **nur, falls ModHUB diesen Text zusätzlich in die internen Steam-Entwicklermetadaten übernimmt**. Die App kann einen späteren ModHUB-Upload nicht abfangen. Nach jedem Test-Upload tatsächlich mit `CHECK METADATA` messen.

Für einen sicheren Nachstellversuch mit einer komplett neuen Wegwerf-Mod siehe `TEST-PLAN-DE.md` und `REPRODUCE-WITH-DISPOSABLE-MOD-EN.md`. Die bestehenden produktiven Workshop-IDs bleiben unangetastet.
