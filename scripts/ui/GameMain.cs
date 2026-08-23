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
    private Button  _simulierenButton = null!;
    private Timer   _pollTimer = null!;

    /// Aktuell angezeigte Szene - nach einer Simulation wird sie neu geladen,
    /// weil die Views ihre Daten nur einmal in _Ready() holen.
    private string _aktuelleScene = SceneKader;

    private const string SceneKader       = "res://scenes/mannschaft/KaderView.tscn";
    private const string SceneJugend      = "res://scenes/jugend/JugendView.tscn";
    private const string SceneTabelle     = "res://scenes/liga/TabelleView.tscn";
    private const string SceneSpielplan   = "res://scenes/liga/SpielplanView.tscn";
    private const string SceneStatistiken = "res://scenes/liga/StatistikenView.tscn";
    private const string SceneAufstellung = "res://scenes/taktik/AufstellungView.tscn";

    public override async void _Ready()
    {
        BuildUI();
        LadeScene(SceneKader);

        _pollTimer = new Timer { WaitTime = 0.5, OneShot = false, Autostart = false };
        _pollTimer.Timeout += OnPollTimeout;
        AddChild(_pollTimer);

        await AktualisiereSaisonStand();
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
            ("💪  Training", ""),
            ("💶  Finanzen", ""),
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

        // Saison- und Spieltagsanzeige
        _spieltagLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        hbox.AddChild(_spieltagLabel);

        _simulierenButton = new Button { Text = "▶  Spieltag simulieren" };
        FmTheme.ApplyButton(_simulierenButton, FmTheme.Accent);
        _simulierenButton.Pressed += OnSimulierenPressed;
        hbox.AddChild(_simulierenButton);

        hbox.AddChild(new VSeparator());

        // Vereinsname rechts
        _vereinLabel = FmTheme.MakeLabel(GameState.Instance.VereinName, 14, FmTheme.TextPrimary);
        hbox.AddChild(_vereinLabel);

        return panel;
    }

    // ── Spieltagssimulation ──────────────────────────────────────────────────

    private async void OnSimulierenPressed()
    {
        _simulierenButton.Disabled = true;
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
                StarteSimulation);
            return;
        }

        StarteSimulation();
    }

    private async void StarteSimulation()
    {
        _simulierenButton.Disabled = true;
        _fortschrittLabel.Text = "Starte …";

        long vereinId = GameState.Instance.VereinId;
        bool gestartet = await ApiClient.PostAsync($"spiel/spieltag/simulieren?vereinId={vereinId}");
        if (!gestartet)
        {
            _fortschrittLabel.Text = "Simulation konnte nicht gestartet werden";
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
        var stand = await ApiClient.GetAsync<Fortschritt>("spiel/spieltag/fortschritt");
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

        await AktualisiereSaisonStand();

        // Eigenes Spiel live nachspielen. Die Ansicht wird erst beim Schließen aktualisiert,
        // damit das Ergebnis nicht schon im Hintergrund verraten wird.
        var bericht = await ApiClient.GetAsync<SpielBericht>(
            $"spiel/verein/{GameState.Instance.VereinId}/letztes");

        if (bericht is { Gespielt: true })
        {
            LiveSpielDialog.Zeige(this, bericht, () => LadeScene(_aktuelleScene));
        }
        else
        {
            // Spielfrei oder Bericht nicht abrufbar - dann direkt aktualisieren.
            LadeScene(_aktuelleScene);
        }
    }

    private async System.Threading.Tasks.Task AktualisiereSaisonStand()
    {
        var stand = await ApiClient.GetAsync<SaisonStand>("spiel/saison");
        if (stand == null)
        {
            _spieltagLabel.Text = "";
            return;
        }
        _spieltagLabel.Text = stand.Anzeige;
        _simulierenButton.Disabled = stand.SaisonBeendet;
        if (stand.SaisonBeendet)
        {
            _simulierenButton.TooltipText = "Alle Spieltage dieser Saison sind gespielt.";
        }
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
