# Fussball-Manager Desktop

Godot 4 C# Desktop-Client für den Fussball-Manager.

## Voraussetzungen

- [Godot Engine 4.6+ (Mono/.NET)](https://godotengine.org/download) – **Mono-Version** erforderlich
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [FM-Backend](https://github.com/HeikoJagemann/FM-Backend) (läuft auf Port 8081)

## Projektstruktur

```
D:\git\
├── FM-Backend\      ← Spring Boot REST-API
└── FM-Desktop\      ← dieses Repository (Godot 4 C#)
```

## Schnellstart

**1. Backend starten:**
```powershell
cd D:\git\FM-Backend
.\start-fm.ps1 -BackendOnly
```

**2. FM-Desktop in Godot öffnen:**
- Godot (Mono) starten
- Projekt `D:\git\FM-Desktop` öffnen
- Im MSBuild-Panel auf **Build** klicken
- **F5** drücken

## Build

Das Projekt verwendet `Godot.NET.Sdk/4.6.2` mit .NET 8.  
Beim ersten Start lädt Godot die NuGet-Pakete automatisch herunter.

Falls der MSBuild-Tab in Godot fehlt: .NET 8 SDK installieren und Godot neu starten.

## Architektur

- **Code-first UI**: Alle Oberflächen werden per C# in `_Ready()` aufgebaut, minimale `.tscn`-Dateien
- **ApiClient**: `HttpClient`-Wrapper für REST-Aufrufe ans Backend (`scripts/api/ApiClient.cs`)
- **GameState**: Godot Autoload-Singleton mit Verein-ID und Liga-ID (`scripts/GameState.cs`)
- **FmTheme**: Zentrale Farbpalette und StyleBox-Helfer (`scripts/ui/FmTheme.cs`)

## Szenen

| Szene | Beschreibung |
|-------|--------------|
| `StartScreen` | Vereinsauswahl, Initialisierungs-Fortschritt |
| `GameMain` | Hauptmenü mit Navigation |
| `KaderView` | Profi- und Amateurspieler nach Position |
| `JugendView` | Jugend A/B/C mit Talent-Sterne-Anzeige |
| `TabelleView` | Ligatabelle |
| `SpielplanView` | Spielplan der Saison |
| `StatistikenView` | Torschützen, Abwehr, Siege |
