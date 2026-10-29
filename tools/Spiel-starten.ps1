# Startet den Fussballmanager und raeumt beim Beenden wieder auf.
#
# Das Skript bleibt offen, solange gespielt wird: Es wartet auf den Godot-Prozess und faehrt danach
# herunter, was zum Spiel gehoert.
#
# Grundsatz: Beendet wird nur, was dieses Skript selbst gestartet hat. Lief PostgreSQL oder das
# Backend schon vorher - etwa aus einer Entwicklungssitzung oder der IDE -, bleibt es auch danach
# stehen. Sonst wuerde ein Doppelklick auf das Spiel eine laufende Entwicklungsumgebung abraeumen.
#
# Parameter:
#   -Behalten   Nach dem Spiel nichts beenden (Backend bleibt fuer den naechsten Start warm)

param(
    [switch]$Behalten
)

$ErrorActionPreference = 'Stop'

$backendPfad = 'C:\git\fm-backend'
$clientPfad  = 'C:\git\FM-Desktop'
$godot       = 'C:\Users\heiko\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
$statusUrl   = 'http://localhost:8081/api/schemas'
$backendPort = 8081
$pgDienst    = 'postgresql-x64-17'
$datenPfad   = Join-Path $env:LOCALAPPDATA 'Fussballmanager'
$protokoll   = Join-Path $datenPfad 'backend.log'

# Fortschrittsmeldung fuer den Ladebildschirm des Clients. Format: "<prozent>|<text>".
$statusDatei = Join-Path $datenPfad 'start-status.txt'

# Was dieses Skript gestartet hat - nur das wird am Ende auch wieder beendet.
$pgSelbstGestartet      = $false
$backendSelbstGestartet = $false

# ── Hilfsfunktionen ──────────────────────────────────────────────────────────

function Test-Backend {
    try {
        $antwort = Invoke-WebRequest -Uri $statusUrl -TimeoutSec 2 -UseBasicParsing
        return $antwort.StatusCode -eq 200
    } catch {
        return $false
    }
}

function Zeige-Fehler($text) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($text, 'Fussballmanager',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
}

# Sagt dem Ladebildschirm, wo der Start gerade steht.
#
# Fehler werden bewusst geschluckt: Der Client liest die Datei nebenher und kommt auch ohne sie
# zurecht - ein Schreibfehler darf niemals den Start des Spiels verhindern.
function Melde-Fortschritt($prozent, $text) {
    Write-Host "    [$prozent%] $text" -ForegroundColor DarkGray
    try {
        Set-Content -Path $statusDatei -Value "$prozent|$text" -Encoding UTF8 -ErrorAction Stop
    } catch {
        # ignorieren
    }
}

# Meldet dem Ladebildschirm, dass der Start gescheitert ist.
#
# Ein negativer Prozentwert ist das Abbruchzeichen: Ohne es wartet der Client stur weiter, bis sein
# eigenes Zeitlimit greift - drei Minuten vor einem Backend, das nie kommen wird.
function Melde-Abbruch($text) {
    Melde-Fortschritt -1 $text
}

# Die PID, die tatsaechlich auf dem Backend-Port lauscht.
#
# Der Umweg ueber den Port ist noetig, weil das Backend als "cmd -> mvnw -> java" startet: Der
# gemerkte cmd-Prozess ist nach dem Start laengst beendet, waehrend die JVM weiterlaeuft. Wer nur
# den cmd-Prozess beendet, laesst ein verwaistes Backend zurueck, das den Port blockiert.
function Get-BackendPid {
    $verbindung = Get-NetTCPConnection -LocalPort $backendPort -State Listen -ErrorAction SilentlyContinue
    if ($verbindung) {
        return ($verbindung | Select-Object -First 1).OwningProcess
    }
    return $null
}

# Beendet einen Prozess samt Kindern - erst hoeflich, dann bestimmt.
#
# Der hoefliche Versuch schickt ein Fensterschliessen-Signal. Eine headless laufende JVM hat kein
# Fenster und lehnt sofort ab; darum wird der Rueckgabewert ausgewertet, statt anschliessend
# blind ein paar Sekunden zu warten - sonst haengt jedes Spielende daran.
function Stop-Prozessbaum($prozessId, $name) {
    if (-not $prozessId) { return }
    if (-not (Get-Process -Id $prozessId -ErrorAction SilentlyContinue)) { return }

    Write-Host "    Beende $name (PID $prozessId) ..." -ForegroundColor DarkGray

    & taskkill.exe /PID $prozessId /T 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        # Angenommen - der JVM kurz Zeit lassen, offene Datenbankverbindungen zu schliessen.
        for ($i = 0; $i -lt 10; $i++) {
            Start-Sleep -Milliseconds 500
            if (-not (Get-Process -Id $prozessId -ErrorAction SilentlyContinue)) {
                Write-Host "    $name beendet" -ForegroundColor Green
                return
            }
        }
    }

    & taskkill.exe /PID $prozessId /T /F 2>&1 | Out-Null
    Start-Sleep -Milliseconds 500
    if (Get-Process -Id $prozessId -ErrorAction SilentlyContinue) {
        # Das Fenster ist im Normalfall versteckt (siehe Verknuepfung auf dem Desktop) - eine
        # Konsolenmeldung saehe hier niemand, deshalb ein Dialog.
        Zeige-Fehler "$name (PID $prozessId) liess sich nicht beenden und laeuft weiter.`n`nBitte im Task-Manager pruefen."
    } else {
        Write-Host "    $name beendet" -ForegroundColor Green
    }
}

# ── Voraussetzungen ──────────────────────────────────────────────────────────

if (-not (Test-Path $godot)) {
    Zeige-Fehler "Godot wurde nicht gefunden:`n$godot"
    exit 1
}
if (-not (Test-Path $backendPfad)) {
    Zeige-Fehler "Das Backend-Verzeichnis fehlt:`n$backendPfad"
    exit 1
}

try {
    New-Item -ItemType Directory -Force -Path $datenPfad | Out-Null
    Melde-Fortschritt 5 'Startet …'

    # ── Client zuerst ────────────────────────────────────────────────────────
    #
    # Godot laeuft los, bevor Datenbank und Backend stehen: Der Ladebildschirm erscheint dadurch
    # gleich nach dem Klick und zeigt den Fortschritt, statt dass der Nutzer eine halbe Minute auf
    # einen leeren Desktop schaut. Er wartet von sich aus, bis das Backend antwortet.

    Write-Host ">>> Spiel wird gestartet" -ForegroundColor Cyan
    $client = Start-Process -FilePath $godot -ArgumentList '--path', $clientPfad `
                            -WorkingDirectory $clientPfad -PassThru

    # ── PostgreSQL ───────────────────────────────────────────────────────────

    Melde-Fortschritt 15 'Datenbank wird geprüft …'
    $pg = Get-Service -Name $pgDienst -ErrorAction SilentlyContinue
    if (-not $pg) {
        Melde-Abbruch "Datenbankdienst '$pgDienst' nicht gefunden"
        Zeige-Fehler "Der Datenbankdienst '$pgDienst' wurde nicht gefunden."
        exit 1
    }
    if ($pg.Status -ne 'Running') {
        Melde-Fortschritt 25 'Datenbank wird gestartet …'
        try {
            Start-Service -Name $pgDienst -ErrorAction Stop
            $pgSelbstGestartet = $true
            Start-Sleep -Seconds 3
        } catch {
            Melde-Abbruch 'Datenbank liess sich nicht starten'
            Zeige-Fehler ("PostgreSQL konnte nicht gestartet werden. Der Dienst braucht dafuer " +
                          "Administratorrechte.`n`n$($_.Exception.Message)")
            exit 1
        }
    } else {
        Write-Host ">>> PostgreSQL laeuft bereits - bleibt nach dem Spiel an" -ForegroundColor DarkGray
    }

    # ── Backend ──────────────────────────────────────────────────────────────

    if (Test-Backend) {
        Write-Host ">>> Backend laeuft bereits - bleibt nach dem Spiel an" -ForegroundColor DarkGray
        Melde-Fortschritt 95 'Backend läuft bereits'
    } else {
        Melde-Fortschritt 40 'Backend wird gestartet …'

        # mvnw ueber cmd starten, damit kein Konsolenfenster stehen bleibt. Der Aufruf braucht den
        # vollen Pfad: cmd durchsucht das aktuelle Verzeichnis nicht nach ausfuehrbaren Dateien.
        $mvnw = Join-Path $backendPfad 'mvnw.cmd'
        $befehl = "cd /d `"$backendPfad`" && `"$mvnw`" spring-boot:run > `"$protokoll`" 2>&1"
        Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $befehl -WindowStyle Hidden

        # Der Start dauert ueblicherweise wenige Sekunden; Maven muss aber ggf. erst aufloesen.
        # Die Leiste kriecht dabei von 40 auf 90 - genauer laesst sich der Fortschritt von aussen
        # nicht bestimmen, und ueber 90 soll sie nicht, solange nichts antwortet.
        $wartezeit = 0
        $grenze = 120
        while (-not (Test-Backend)) {
            Start-Sleep -Seconds 2
            $wartezeit += 2
            if ($wartezeit -ge $grenze) {
                Melde-Abbruch 'Backend ist nicht hochgekommen'
                Zeige-Fehler "Das Backend ist nach zwei Minuten nicht erreichbar.`n`nProtokoll:`n$protokoll"
                exit 1
            }
            $anteil = [Math]::Min(1.0, $wartezeit / 40.0)
            Melde-Fortschritt ([int](40 + 50 * $anteil)) 'Backend fährt hoch …'
        }
        $backendSelbstGestartet = $true
    }

    Melde-Fortschritt 100 'Bereit'
    Write-Host "    Viel Erfolg. Dieses Fenster raeumt nach dem Spiel auf." -ForegroundColor DarkGray
    Wait-Process -Id $client.Id
}
finally {
    # Laeuft auch bei Abbruch (Strg+C) und wenn oben etwas schiefgeht - was gestartet wurde, wird
    # auch wieder beendet.

    # Weg damit, sonst zeigt der naechste Ladebildschirm kurz den Stand von heute frueh.
    Remove-Item -Path $statusDatei -Force -ErrorAction SilentlyContinue

    if ($Behalten) {
        Write-Host "`n>>> -Behalten: Backend und Datenbank bleiben an" -ForegroundColor Yellow
    } else {
        if ($backendSelbstGestartet -or $pgSelbstGestartet) {
            Write-Host "`n>>> Aufraeumen" -ForegroundColor Cyan
        }

        if ($backendSelbstGestartet) {
            Stop-Prozessbaum (Get-BackendPid) 'Backend'
        }

        # Die Datenbank zuletzt: Das Backend soll seine Verbindungen vorher schliessen koennen.
        if ($pgSelbstGestartet) {
            Write-Host "    Beende PostgreSQL ..." -ForegroundColor DarkGray
            try {
                Stop-Service -Name $pgDienst -Force -ErrorAction Stop
                Write-Host "    PostgreSQL beendet" -ForegroundColor Green
            } catch {
                Write-Host "    PostgreSQL konnte nicht beendet werden: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
    }
}
