#nullable enable
using Godot;
using System;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

public partial class GameMain : Control
{
    private Control _contentArea = null!;
    private Label   _vereinLabel = null!;
    private Label   _spieltagLabel = null!;
    private Label   _fortschrittLabel = null!;
    private Label   _ereignisLabel = null!;
    private Button  _trainingButton = null!;
    private Button  _simulierenButton = null!;
    private Timer   _pollTimer = null!;
    private KalenderStand? _kalenderStand;

    /// <summary>Sperrt weitere Abfragen, solange eine unterwegs ist.</summary>
    private bool _pollLaeuft;

    /// Aktuell angezeigte Szene - nach einem Wochendurchlauf wird sie neu geladen,
    /// weil die Views ihre Daten nur einmal in _Ready() holen.
    private string _aktuelleScene = SceneKader;

    private const string SceneKader       = "res://scenes/mannschaft/KaderView.tscn";
    private const string SceneJugend      = "res://scenes/jugend/JugendView.tscn";
    private const string SceneTabelle     = "res://scenes/liga/TabelleView.tscn";
    private const string SceneSpielplan   = "res://scenes/liga/SpielplanView.tscn";
    private const string SceneStatistiken = "res://scenes/liga/StatistikenView.tscn";
    private const string SceneAufstellung = "res://scenes/taktik/AufstellungView.tscn";
    private const string SceneTraining     = "res://scenes/training/TrainingView.tscn";
    private const string SceneKalender     = "res://scenes/kalender/KalenderView.tscn";
    private const string SceneFinanzen     = "res://scenes/finanzen/FinanzenView.tscn";
    private const string SceneTransfer     = "res://scenes/transfer/TransferView.tscn";

    /// Eigener Bildschirm statt Overlay - die Partie übernimmt das ganze Fenster.
    private const string SceneSpiel        = "res://scenes/spiel/SpielView.tscn";

    public override async void _Ready()
    {
        BuildUI();
        LadeScene(SceneKader);

        _pollTimer = new Timer { WaitTime = 0.5, OneShot = false, Autostart = false };
        _pollTimer.Timeout += OnPollTimeout;
        AddChild(_pollTimer);

        await AktualisiereKalenderstand();
    }

    private void BuildUI()
    {
        // Hintergrund
        var bg = new ColorRect { Color = FmTheme.BgDark };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 0);
        AddChild(vbox);

        // Toolbar
        vbox.AddChild(BuildToolbar());

        // Trennlinie
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        // Inhaltsbereich
        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16);
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(margin);

        _contentArea = new Control();
        _contentArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddChild(_contentArea);
    }

    private Control BuildToolbar()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.ToolbarStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 8, 6);
        panel.AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(hbox);

        // Zurück-Button
        var back = new Button { Text = "←", TooltipText = "Hauptmenü", CustomMinimumSize = new Vector2(36, 0) };
        FmTheme.ApplyButton(back, FmTheme.BgPanel);
        back.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/StartScreen.tscn");
        hbox.AddChild(back);

        hbox.AddChild(new VSeparator());

        // Mannschaft-Dropdown
        hbox.AddChild(BuildDropdown("👥  Mannschaft", new[] {
            ("Kader", SceneKader),
        }));

        // Taktik-Dropdown
        hbox.AddChild(BuildDropdown("🎯  Taktik", new[] {
            ("Aufstellung", SceneAufstellung),
        }));

        // Liga-Dropdown
        hbox.AddChild(BuildDropdown("🏆  Liga", new[] {
            ("Tabelle",     SceneTabelle),
            ("Spielplan",   SceneSpielplan),
            ("Statistiken", SceneStatistiken),
        }));

        // Einfache Menüpunkte
        foreach (var (label, scene) in new (string, string)[] {
            ("📅  Kalender", SceneKalender),
            ("💪  Training", SceneTraining),
            ("💶  Finanzen", SceneFinanzen),
            ("🔁  Transfers", SceneTransfer),
            ("🏟  Umfeld",   ""),
            ("👦  Jugend",   SceneJugend),
        })
        {
            var btn = new Button { Text = label };
            FmTheme.ApplyButton(btn, FmTheme.BgPanel);
            btn.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
            var s = scene;
            btn.Pressed += () => { if (!string.IsNullOrEmpty(s)) LadeScene(s); };
            hbox.AddChild(btn);
        }

        // Spacer
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hbox.AddChild(spacer);

        // Fortschritt der laufenden Simulation
        _fortschrittLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        hbox.AddChild(_fortschrittLabel);

        // Ereignis, auf dem der Stand gerade steht
        _ereignisLabel = FmTheme.MakeLabel("", 12, FmTheme.Gold);
        hbox.AddChild(_ereignisLabel);

        // Nur am Wochenstart: Abkürzung zur Trainingsplanung, denn genau dafür hält er an.
        _trainingButton = new Button { Text = "💪  Training planen", Visible = false };
        FmTheme.ApplyButton(_trainingButton, FmTheme.BgPanel);
        _trainingButton.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        _trainingButton.Pressed += () => LadeScene(SceneTraining);
        hbox.AddChild(_trainingButton);

        // Spieldatum und Saisonabschnitt
        _spieltagLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        hbox.AddChild(_spieltagLabel);

        _simulierenButton = new Button { Text = "▶  Weiter" };
        FmTheme.ApplyButton(_simulierenButton, FmTheme.Accent);
        _simulierenButton.Pressed += OnSimulierenPressed;
        hbox.AddChild(_simulierenButton);

        hbox.AddChild(new VSeparator());

        // Vereinsname rechts
        _vereinLabel = FmTheme.MakeLabel(GameState.Instance.VereinName, 14, FmTheme.TextPrimary);
        hbox.AddChild(_vereinLabel);

        // Einstellungen (Fenstermodus, Gehaltsanzeige)
        var einstellungen = new Button { Text = "⚙", TooltipText = "Einstellungen", CustomMinimumSize = new Vector2(36, 0) };
        FmTheme.ApplyButton(einstellungen, FmTheme.BgPanel);
        einstellungen.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        einstellungen.Pressed += () => EinstellungenDialog.Zeige(this, () => LadeScene(_aktuelleScene));
        hbox.AddChild(einstellungen);

        return panel;
    }

    // ── Schritt auf der Zeitachse ────────────────────────────────────────────

    /// <summary>
    /// Ein Schritt von Ereignis zu Ereignis. Steht man auf einer eigenen Partie, wird sie gespielt -
    /// sonst läuft die Zeit bis zum nächsten Halt.
    /// </summary>
    private async void OnSimulierenPressed()
    {
        _simulierenButton.Disabled = true;

        if (_kalenderStand?.AktuellesEreignis is { IstSpiel: true })
        {
            await StarteEigenesSpiel();
            return;
        }
        StarteDurchlauf();
    }

    /// <summary>Prüft die Aufstellung und pfeift dann an.</summary>
    private async System.Threading.Tasks.Task StarteEigenesSpiel()
    {
        _fortschrittLabel.Text = "Prüfe Aufstellung …";
        long vereinId = GameState.Instance.VereinId;
        var aufstellung = await ApiClient.GetAsync<AufstellungModel>($"aufstellung/{vereinId}");

        if (aufstellung is { Spielbereit: false })
        {
            _fortschrittLabel.Text = "";
            _simulierenButton.Disabled = false;
            ZeigeHinweis("Mannschaft nicht spielbereit",
                aufstellung.Warnung + "\n\nStelle deine Mannschaft unter Taktik → Aufstellung auf.");
            return;
        }

        // Unbesetzte Positionen sind erlaubt - die Mannschaft spielt dann in Unterzahl.
        if (aufstellung?.Warnung != null)
        {
            _fortschrittLabel.Text = "";
            _simulierenButton.Disabled = false;
            FrageNach("Aufstellung unvollständig",
                aufstellung.Warnung + "\n\nDeine Mannschaft tritt dann in Unterzahl an. Trotzdem spielen?",
                Anpfiff);
            return;
        }

        Anpfiff();
    }

    /// <summary>
    /// Übergibt an den Spielbildschirm. Die Partie bekommt eine eigene Szene statt eines Overlays:
    /// Während sie läuft, darf weder navigiert noch die Zeit weitergeschaltet werden - diese
    /// Toolbar wäre also durchgehend irreführend. Der Anpfiff selbst geschieht dort.
    /// </summary>
    private void Anpfiff()
    {
        _fortschrittLabel.Text = "";
        GetTree().ChangeSceneToFile(SceneSpiel);
    }

    /// <summary>Lässt die Zeit bis zum nächsten Ereignis laufen.</summary>
    private async void StarteDurchlauf()
    {
        _simulierenButton.Disabled = true;
        _fortschrittLabel.Text = _kalenderStand?.NaechstesEreignis is { } ziel
            ? $"Weiter bis {ziel.Titel} …"
            : "Die Zeit läuft …";

        bool gestartet = await ApiClient.PostAsync("kalender/weiter");
        if (!gestartet)
        {
            _fortschrittLabel.Text = "Der Durchlauf konnte nicht gestartet werden";
            _simulierenButton.Disabled = false;
            return;
        }
        _pollTimer.Start();
    }

    // ── Dialoge ──────────────────────────────────────────────────────────────

    private void ZeigeHinweis(string titel, string text)
    {
        var dialog = new AcceptDialog { Title = titel, DialogText = text, Exclusive = true };
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled  += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void FrageNach(string titel, string text, Action beiZustimmung)
    {
        var dialog = new ConfirmationDialog
        {
            Title = titel,
            DialogText = text,
            OkButtonText = "Trotzdem spielen",
            CancelButtonText = "Abbrechen",
            Exclusive = true,
        };
        dialog.Confirmed += () => { dialog.QueueFree(); beiZustimmung(); };
        dialog.Canceled  += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private async void OnPollTimeout()
    {
        // Der Timer feuert weiter, während die Anfrage unterwegs ist. Ohne diesen Riegel sind
        // mehrere Abfragen gleichzeitig offen, melden alle "fertig" und laden die Ansicht mehrfach.
        if (_pollLaeuft) return;

        Fortschritt? stand;
        _pollLaeuft = true;
        try
        {
            stand = await ApiClient.GetAsync<Fortschritt>("kalender/fortschritt");
        }
        finally
        {
            _pollLaeuft = false;
        }

        if (!IsInstanceValid(this)) return;
        if (stand == null)
        {
            return; // Nächster Tick versucht es erneut.
        }

        _fortschrittLabel.Text = stand.Fertig ? "" : $"{stand.Nachricht} ({stand.Prozent} %)";

        if (!stand.Fertig)
        {
            return;
        }

        _pollTimer.Stop();
        _simulierenButton.Disabled = false;

        await AktualisiereKalenderstand();
        if (!IsInstanceValid(this)) return;
        LadeScene(_aktuelleScene);
    }

    /// <summary>
    /// Holt den Spielzeitpunkt und richtet die Toolbar daran aus. Der Fortschritt haengt seit dem
    /// Kalender an der Zeit, nicht mehr an einem Spieltagszaehler.
    /// </summary>
    private async System.Threading.Tasks.Task AktualisiereKalenderstand()
    {
        var stand = await ApiClient.GetAsync<KalenderStand>("kalender");
        if (stand == null)
        {
            _spieltagLabel.Text = "";
            return;
        }

        GameState.Instance.SetSpieldatum(stand.Datum);
        _kalenderStand = stand;

        _spieltagLabel.Text = $"Saison {stand.SaisonText} · {stand.ZeitpunktText} · {stand.Phase}";
        ZeigeEreignis(stand);
    }

    /// <summary>
    /// Richtet Toolbar und Schaltfläche am Ereignis aus, auf dem der Stand steht. Die Beschriftung
    /// kommt vom Server mit - so heißt der Knopf auf einer eigenen Partie "Spiel starten", ohne
    /// dass der Client die Ereignisarten einzeln kennen müsste.
    /// </summary>
    private void ZeigeEreignis(KalenderStand stand)
    {
        var hier = stand.AktuellesEreignis;
        var ziel = stand.NaechstesEreignis;

        _ereignisLabel.Text = hier != null ? $"{hier.Symbol}  {hier.Titel}" : "";
        // Der Wochenstart hält genau dafür an, dass hier geplant wird.
        _trainingButton.Visible = hier is { IstWochenstart: true };

        _simulierenButton.Disabled = false;
        _simulierenButton.Text = $"▶  {hier?.AktionText ?? "Weiter"}";
        _simulierenButton.TooltipText = hier is { IstSpiel: true }
            ? $"{hier.Titel} · {hier.Untertitel}"
            : ziel == null
                ? "Die Zeit weiterlaufen lassen"
                : $"Nächster Halt: {ziel.Titel} ({ziel.ZeitText})";
    }

    private MenuButton BuildDropdown(string label, (string Label, string Scene)[] items)
    {
        var btn = new MenuButton { Text = label };
        btn.AddThemeStyleboxOverride("normal", FmTheme.ButtonStyle(FmTheme.BgPanel));
        btn.AddThemeColorOverride("font_color", FmTheme.TextPrimary);

        var popup = btn.GetPopup();
        for (int i = 0; i < items.Length; i++)
            popup.AddItem(items[i].Label, i);

        var captured = items;
        popup.IdPressed += id =>
        {
            var scene = captured[(int)id].Scene;
            if (!string.IsNullOrEmpty(scene)) LadeScene(scene);
        };

        return btn;
    }

    private void LadeScene(string path)
    {
        _aktuelleScene = path;

        foreach (Node child in _contentArea.GetChildren())
            child.QueueFree();

        var packed = GD.Load<PackedScene>(path);
        if (packed == null)
        {
            GD.PrintErr($"[GameMain] Szene nicht gefunden: {path}");
            return;
        }
        var instance = packed.Instantiate<Control>();
        instance.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _contentArea.AddChild(instance);
    }
}
