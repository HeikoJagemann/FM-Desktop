# Startet den Fussballmanager: erst das Backend, dann den Godot-Client.
#
# Das Backend laeuft als eigener, unsichtbarer Prozess weiter, solange gespielt wird. Ist es
# bereits erreichbar - etwa weil es aus einer Entwicklungssitzung noch laeuft -, wird kein
# zweiter gestartet; ein zweiter koennte den Port 8081 ohnehin nicht belegen.

$ErrorActionPreference = 'Stop'

$backendPfad = 'C:\git\fm-backend'
$clientPfad  = 'C:\git\FM-Desktop'
$godot       = 'C:\Users\heiko\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
$statusUrl   = 'http://localhost:8081/api/schemas'
$protokoll   = Join-Path $env:LOCALAPPDATA 'Fussballmanager\backend.log'

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

# ── Voraussetzungen ──────────────────────────────────────────────────────────

if (-not (Test-Path $godot)) {
    Zeige-Fehler "Godot wurde nicht gefunden:`n$godot"
    exit 1
}
if (-not (Test-Path $backendPfad)) {
    Zeige-Fehler "Das Backend-Verzeichnis fehlt:`n$backendPfad"
    exit 1
}

# ── Backend ──────────────────────────────────────────────────────────────────

if (-not (Test-Backend)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $protokoll) | Out-Null

    # mvnw ueber cmd starten, damit kein Konsolenfenster stehen bleibt. Der Aufruf braucht den
    # vollen Pfad: cmd durchsucht das aktuelle Verzeichnis nicht nach ausfuehrbaren Dateien.
    $mvnw = Join-Path $backendPfad 'mvnw.cmd'
    $befehl = "cd /d `"$backendPfad`" && `"$mvnw`" spring-boot:run > `"$protokoll`" 2>&1"
    Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $befehl -WindowStyle Hidden

    # Der Start dauert ueblicherweise wenige Sekunden; Maven muss aber ggf. erst aufloesen.
    $wartezeit = 0
    while (-not (Test-Backend)) {
        Start-Sleep -Seconds 2
        $wartezeit += 2
        if ($wartezeit -ge 120) {
            Zeige-Fehler "Das Backend ist nach zwei Minuten nicht erreichbar.`n`nProtokoll:`n$protokoll"
            exit 1
        }
    }
}

# ── Client ───────────────────────────────────────────────────────────────────

Start-Process -FilePath $godot -ArgumentList '--path', $clientPfad -WorkingDirectory $clientPfad
