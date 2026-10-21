# Kontrollierter Nachstellversuch mit einer Wegwerf-Mod (Deutsch)

Wir kennen den **kaputten Datensatz und seine funktionierende Reparatur**, aber noch NICHT die genauen Bedienungsschritte, durch die ModHUB die öffentliche Beschreibung in den internen Steam-Datensatz kopiert hat.

**Bitte nur mit einer komplett neuen, entbehrlichen Test-Mod arbeiten.** Weder die reparierte x100-Mod noch andere genutzte Mods anfassen. Test-Mod nach Möglichkeit privat/nicht gelistet; keine Spielstände darauf aufbauen.

1. Im ModHUB eine neue, einfache Test-Mod anlegen. Falls ein leeres Projekt akzeptiert wird, reicht das. Andernfalls nur minimale harmlose SDK-Testinhalte hinzufügen. Projekt sichern. Als Namen beispielsweise `SE2 UGC Metadata Repro - TEST ONLY` verwenden.
2. Kurzbeschreibung aus `TEST-DESCRIPTIONS/description-stage-A-short.txt` in ModHUB einfügen und Test-Mod veröffentlichen. Die neue ID notieren. Mit der v0.3-App `CHECK METADATA` ausführen und im Backupordner den *sicheren Bericht* aufheben.
3. **Nur über die Steam-Webseite** die öffentliche Beschreibung durch `description-stage-B-2500.txt` ersetzen. Ohne ModHUB-Upload erneut mit der App prüfen. Ändert sich die Grösse der internen Metadaten überhaupt?
4. Danach **nur im Testprojekt** ModHUB neu öffnen. Wenn „Steam Data Available“ erscheint, Screenshot machen. Vor `Load from Steam` eine Projektkopie erstellen. Lade testweise die Steam-Daten in der Testkopie und kontrolliere, ob die lange Beschreibung in die lokalen ModHUB-Felder gelangt.
5. Testprojekt über ModHUB erneut veröffentlichen. Nach jeder Änderung sofort mit `CHECK METADATA` die tatsächlichen Bytes messen. Texte aus `TEST-DESCRIPTIONS` stufenweise testen – zuerst 2.500, dann ~3.250, dann ~3.700 Zeichen. **Beim ersten Hinweis auf >5.000 Bytes oder einen Fehler sofort stoppen** und Logs sichern.
6. Für Keen Uhrzeit, ModHUB-Version, gewählte Schaltflächen, exakten verwendeten Text, reale Steam-UGC-Grösse und Screenshots protokollieren. Die englische Vorlage steht in `REPRODUCE-WITH-DISPOSABLE-MOD-EN.md`.

**Wichtig:** Eine lange Beschreibung auf Steam allein ist kein Beweis für den Fehler. Ausschlaggebend ist das *interne* Feld `FullMetadata.BaseMetadata.Description`. Unser neuer `DESCRIPTION PREFLIGHT` liefert dafür nur eine Hochrechnung, keinen garantierten Schutz vor einer Veröffentlichung. Das Testwerkzeug selbst veröffentlicht oder verändert bei `CHECK METADATA` nichts.

**Risikostopp:** Niemals direkt die bestehende x100-Mod verwenden. Niemals alte Spielstände überschreiben. Falls die Test-Mod unlesbar wird, nicht mehrfach veröffentlichen; zuerst Fehlerdaten sichern. Erst danach getrennt über den Fix nachdenken.
