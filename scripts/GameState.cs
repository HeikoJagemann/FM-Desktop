using Godot;
using System;
using FMDesktop.Api;

namespace FMDesktop;

/// <summary>Autoload-Singleton – hält den laufenden Spielstand.</summary>
public partial class GameState : Node
{
    public static GameState Instance { get; private set; } = null!;

    public long   VereinId      { get; private set; }
    public string VereinName    { get; private set; } = "";
    public long   LigaId        { get; private set; }
    public string LigaName      { get; private set; } = "";
    public string CurrentSchema { get; private set; } = "db_default";

    /// <summary>
    /// Das "Heute" der Spielwelt. Das Alter eines Spielers zählt danach, nicht nach der Uhr des
    /// Rechners - sonst altert ein Spieler über den Saisonwechsel hinweg nicht.
    /// </summary>
    public DateOnly Spieldatum { get; private set; } = DateOnly.FromDateTime(DateTime.Today);

    public void SetSpieldatum(DateOnly datum) => Spieldatum = datum;

    public override void _Ready()
    {
        Instance = this;

        // Flutlicht auf das ganze Fenster: Schrift, Knöpfe, Menüs und Tooltips aller Ansichten.
        GetTree().Root.Theme = UI.FmTheme.Basistheme();

        var argumente = OS.GetCmdlineUserArgs();
        int vorschau = Array.IndexOf(argumente, "--vorschau");
        if (vorschau >= 0 && argumente.Length >= vorschau + 5 && long.TryParse(argumente[vorschau + 2], out var verein))
        {
            _ = Vorschau(argumente[vorschau + 1], verein, argumente[vorschau + 3], argumente[vorschau + 4]);
        }

        // Fenstermodus vom letzten Start wiederherstellen - liegt lokal, nicht im Spielstand.
        if (ClientEinstellungen.GetBool("vollbild", false))
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
    }

    /// <summary>Welche Ansicht das Hauptspiel zuerst zeigt - sonst der Kader.</summary>
    public string? StartAnsicht { get; private set; }

    /// <summary>
    /// Vorschau für die Gestaltung: öffnet eine Ansicht eines Spielstands, speichert ein
    /// Bildschirmfoto und beendet das Spiel. Aufruf mit Godot-Benutzerargumenten:
    /// <c>-- --vorschau save_x 192 res://scenes/taktik/AufstellungView.tscn C:/pfad/bild.png</c>.
    /// Liest nur; am Spielstand ändert sich nichts.
    /// </summary>
    private async System.Threading.Tasks.Task Vorschau(string schema, long vereinId, string ansicht, string datei)
    {
        SetSchema(schema);
        var verein = await ApiClient.GetAsync<Models.Verein>($"verein/{vereinId}");
        if (verein == null)
        {
            GD.PrintErr($"[Vorschau] Verein {vereinId} in {schema} nicht gefunden.");
            GetTree().Quit(1);
            return;
        }
        SetVerein(vereinId, verein.Name, verein.Liga?.Id ?? 0, verein.Liga?.Name ?? "");
        StartAnsicht = ansicht;
        GetTree().ChangeSceneToFile("res://scenes/GameMain.tscn");

        // Den Ansichten Zeit lassen, ihre Daten zu holen und sich aufzubauen.
        await ToSignal(GetTree().CreateTimer(5.0), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(datei);
        GD.Print($"[Vorschau] gespeichert: {datei}");
        GetTree().Quit();
    }

    public void SetVerein(long vereinId, string vereinName, long ligaId, string ligaName)
    {
        VereinId   = vereinId;
        VereinName = vereinName;
        LigaId     = ligaId;
        LigaName   = ligaName;
    }

    public void SetSchema(string schema)
    {
        CurrentSchema          = schema;
        ApiClient.CurrentSchema = schema;
    }
}
