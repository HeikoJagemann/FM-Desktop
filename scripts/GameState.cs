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
            _ = Vorschau(argumente[vorschau + 1], verein, argumente[vorschau + 3], argumente[vorschau + 4],
                argumente.Length > vorschau + 5 ? argumente[vorschau + 5] : null);
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
    /// Liest nur - außer bei der Spielansicht, die beim Öffnen eine Partie anpfeift; die
    /// gehört deshalb in einen Wegwerf-Spielstand.
    ///
    /// <para>Ein sechstes Argument öffnet zusätzlich einen Dialog über der Ansicht:
    /// <c>einstellungen</c>, <c>spieler:&lt;id&gt;</c> (aus dem eigenen Kader),
    /// <c>bericht:&lt;spielId&gt;[:reiter]</c> oder <c>wechsel</c> (nur über der Spielansicht).</para>
    /// </summary>
    private async System.Threading.Tasks.Task Vorschau(string schema, long vereinId, string ansicht, string datei,
                                                       string? dialog)
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
        // Eigene Bildschirme (Start, Spiel) werden direkt geöffnet, alle übrigen im Hauptspiel.
        bool eigenerBildschirm = ansicht.EndsWith("StartScreen.tscn") || ansicht.EndsWith("SpielView.tscn");
        StartAnsicht = eigenerBildschirm ? null : ansicht;
        GetTree().ChangeSceneToFile(eigenerBildschirm ? ansicht : "res://scenes/GameMain.tscn");

        // Den Ansichten Zeit lassen, ihre Daten zu holen und sich aufzubauen.
        await ToSignal(GetTree().CreateTimer(5.0), SceneTreeTimer.SignalName.Timeout);
        if (dialog != null)
        {
            await OeffneVorschaudialog(dialog);
            await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(datei);
        GD.Print($"[Vorschau] gespeichert: {datei}");
        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task OeffneVorschaudialog(string dialog)
    {
        var szene = GetTree().CurrentScene;
        var teile = dialog.Split(':');
        switch (teile[0])
        {
            case "einstellungen":
                UI.EinstellungenDialog.Zeige(szene);
                break;
            case "bericht":
                UI.SpielberichtOverlay.Zeige(szene, long.Parse(teile[1]),
                    teile.Length > 2 ? int.Parse(teile[2]) : 0);
                break;
            case "spieler":
                var kader = await ApiClient.GetAsync<System.Collections.Generic.List<Models.Spieler>>(
                    $"verein/{VereinId}/spieler");
                var spieler = kader?.Find(s => s.Id == long.Parse(teile[1]));
                if (spieler != null) UI.SpielerDetailOverlay.Zeige(szene, spieler);
                else GD.PrintErr($"[Vorschau] Spieler {teile[1]} nicht im Kader.");
                break;
            case "wechsel":
                if (szene is UI.SpielView spielView) spielView.OeffneWechsel();
                else GD.PrintErr("[Vorschau] Wechsel nur über der Spielansicht.");
                break;
            default:
                GD.PrintErr($"[Vorschau] Unbekannter Dialog: {dialog}");
                break;
        }
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
