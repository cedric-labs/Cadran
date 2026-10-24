# Benutzerhandbuch: Datenbank-Verschlüsselung

[← Zurück: 03 - Anmeldung](03-login.md) | [Zurück zum Hauptindex](../README.md) | [Weiter: 05 - Datenbankentsperrung →](05-database-unlock.md)

---

Der Bildschirm **Datenbank-Verschlüsselung** ermöglicht es Ihnen, Ihre lokale Datenbankdatei mit einer Master-Passphrase und hardwareunabhängiger AES-256-Verschlüsselung abzusichern. Dadurch wird gewährleistet, dass niemand Ihre Finanzdaten einsehen kann, selbst wenn die Rohdatei vom Datenträger kopiert wird.

---

## 1. Visuelle Übersicht & Aktionsmarkierungen

![Bildschirm zur Datenbank-Verschlüsselung](../../images/screens/04-database-encryption-screen-annotated.png)

---

## 2. Referenztabelle der Aktionen

| Markierung | Bedienelement | Aktionstyp | Handlungsanweisung |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Master-Passphrase** | **AUSFÜLLEN** | Wählen Sie einen starken Hauptschlüssel (mindestens 8 Zeichen). Dieses Passwort wird zum Entsperren der Datenbank benötigt. |
| <kbd>2</kbd> | **Passphrase bestätigen** | **AUSFÜLLEN** | Wiederholen Sie die Eingabe exakt zur Bestätigung. |
| <kbd>3</kbd> | **Auf diesem Gerät merken** | **OPTIONAL** | Aktivieren Sie diese Option, wenn Cadran den Schlüssel auf diesem PC speichern soll, um ihn beim Start nicht erneut abzufragen. |
| <kbd>4</kbd> | **Schaltfläche "Verschlüsselung aktivieren"** | **HIER KLICKEN** | Klicken Sie hier, um Ihre Datenbankdatei sofort mit AES-256 zu verschlüsseln. |

---

## 3. Schritt-für-Schritt-Anleitung

1. **Hauptschlüssel wählen**:
   - Geben Sie Ihre geheime Passphrase in das Feld **Master-Passphrase** (**[1]**) ein.
   - Bestätigen Sie diese im Feld **Passphrase bestätigen** (**[2]**).
2. **Startverhalten festlegen**:
   - Setzen Sie ein Häkchen bei **Auf diesem Gerät merken** (**[3]**) für ein bequemes automatisches Entsperren auf Ihrem persönlichen Rechner.
   - Lassen Sie das Feld leer, wenn Cadran die Passphrase bei jedem Start manuell anfordern soll.
3. **Aktivieren**:
   - Klicken Sie auf die blaue Schaltfläche **Verschlüsselung jetzt aktivieren** (**[4]**).
   - Ihre Daten sind nun auf dem Datenträger vollständig verschlüsselt.

> [!CAUTION]
> Wenn Sie Ihre Master-Passphrase verlieren und die Option "Auf diesem Gerät merken" nicht gewählt haben, **können Ihre Daten niemals wiederhergestellt werden**. Bewahren Sie die Passphrase sicher in einem Passwort-Manager auf.
