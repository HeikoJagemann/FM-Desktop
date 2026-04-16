using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

public partial class StartScreen : Control
{
    private enum Zustand { Start, Laden, Auswahl }

    private Control      _startPanel  = null!;
    private Control      _ladenPanel  = null!;
    private Control      _auswahlPanel = null!;
    private ProgressBar  _progressBar = null!;
    private Label        _progressLabel = null!;
    private HBoxContainer _vereineContainer = null!;
    private Timer?       _pollTimer;
    private bool         _polling;

    public override void _Ready()
    {
        BuildUI();
        ZeigePanel(Zustand.Start);
    }

    // ── UI aufbauen ───────────────────────────────────────────

    private void BuildUI()
    {
        var bg = new ColorRect { Color = FmTheme.BgDark };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var root = new MarginContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        FmTheme.SetMargin(root, 40);
        AddChild(root);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 24);
        root.AddChild(vbox);

        // Hero
        vbox.AddChild(BuildHero());

        // Panels (übereinanderliegend)
        var stack = new Control();
        stack.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(stack);

        _startPanel = BuildStartPanel();
        _startPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_startPanel);

        _ladenPanel = BuildLadenPanel();
        _ladenPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_ladenPanel);

        _auswahlPanel = BuildAuswahlPanel();
        _auswahlPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_auswahlPanel);

        // Version-Label
        var version = FmTheme.MakeLabel($"v0.0.1", 11, FmTheme.TextSecondary);
        version.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(version);
    }

    private Control BuildHero()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);

        var title = FmTheme.MakeLabel("⚽  Fussball-Manager", 34, FmTheme.TextPrimary);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var sub = FmTheme.MakeLabel("Starte deine Karriere als Trainer", 15, FmTheme.TextSecondary);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(sub);

        return vbox;
    }

    private Control BuildStartPanel()
    {
        var center = new CenterContainer();

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(420, 0);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 32);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("🏆", 48, FmTheme.Gold) with { HorizontalAlignment = HorizontalAlignment.Center });
        vbox.AddChild(FmTheme.MakeLabel("Neues Spiel", 22, FmTheme.TextPrimary) with { HorizontalAlignment = HorizontalAlignment.Center });

        var desc = FmTheme.MakeLabel(
            "Du startest als Trainer in der Oberliga.\nWähle einen Verein und führe ihn nach oben.",
            14, FmTheme.TextSecondary);
        desc.HorizontalAlignment = HorizontalAlignment.Center;
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(desc);

        var btn = new Button { Text = "▶   Spiel starten", CustomMinimumSize = new Vector2(220, 48) };
        FmTheme.ApplyButton(btn, FmTheme.Accent);
        btn.Pressed += OnSpielStarten;

        var btnCenter = new CenterContainer();
        btnCenter.AddChild(btn);
        vbox.AddChild(btnCenter);

        return center;
    }

    private Control BuildLadenPanel()
    {
        var center = new CenterContainer();

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(520, 0);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 32);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("⚙", 48) with { HorizontalAlignment = HorizontalAlignment.Center });
        vbox.AddChild(FmTheme.MakeLabel("Ligenwelt wird aufgebaut …", 20, FmTheme.TextPrimary) with { HorizontalAlignment = HorizontalAlignment.Center });

        _progressLabel = FmTheme.MakeLabel("Starte …", 13, FmTheme.TextSecondary);
        _progressLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_progressLabel);

        _progressBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(0, 18) };
        vbox.AddChild(_progressBar);

        return center;
    }

    private Control BuildAuswahlPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);

        vbox.AddChild(FmTheme.MakeLabel("Wähle deinen Verein – Oberliga", 22, FmTheme.TextPrimary) with { HorizontalAlignment = HorizontalAlignment.Center });
        vbox.AddChild(FmTheme.MakeLabel("Drei zufällige Vereine aus der Oberliga stehen zur Auswahl.", 13, FmTheme.TextSecondary) with { HorizontalAlignment = HorizontalAlignment.Center });

        _vereineContainer = new HBoxContainer();
        _vereineContainer.AddThemeConstantOverride("separation", 24);
        _vereineContainer.Alignment = BoxContainer.AlignmentMode.Center;
        _vereineContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(_vereineContainer);

        var andereBtn = new Button { Text = "🔄   Andere Vereine anzeigen", CustomMinimumSize = new Vector2(240, 38) };
        FmTheme.ApplyButton(andereBtn, FmTheme.BgPanel);
        andereBtn.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        andereBtn.Pressed += async () => await LadeAngebote();

        var btnCenter = new CenterContainer();
        btnCenter.AddChild(andereBtn);
        vbox.AddChild(btnCenter);

        return vbox;
    }

    private Control BaueVereinCard(Verein verein)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(290, 310);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 20);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("🛡", 40) with { HorizontalAlignment = HorizontalAlignment.Center });

        var name = FmTheme.MakeLabel(verein.Name, 16, FmTheme.TextPrimary);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(name);

        var liga = FmTheme.MakeLabel(verein.Liga?.Name ?? "", 12, FmTheme.Accent);
        liga.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(liga);

        int stars = Math.Clamp((int)Math.Round(verein.Staerke / 20.0), 1, 5);
        var sterne = FmTheme.MakeLabel(new string('★', stars) + new string('☆', 5 - stars), 22, FmTheme.Gold);
        sterne.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(sterne);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        vbox.AddChild(spacer);

        var btn = new Button { Text = "▶   Auswählen" };
        FmTheme.ApplyButton(btn, FmTheme.Accent);
        var v = verein;
        btn.Pressed += () => VereinWaehlen(v);
        vbox.AddChild(btn);

        return panel;
    }

    // ── Logik ─────────────────────────────────────────────────

    private void ZeigePanel(Zustand z)
    {
        _startPanel.Visible   = z == Zustand.Start;
        _ladenPanel.Visible   = z == Zustand.Laden;
        _auswahlPanel.Visible = z == Zustand.Auswahl;
    }

    private async void OnSpielStarten()
    {
        ZeigePanel(Zustand.Laden);
        _progressBar.Value = 0;
        _progressLabel.Text = "Starte …";

        bool ok = await ApiClient.PostAsync("spiel/initialisieren");
        if (!ok)
        {
            ZeigePanel(Zustand.Start);
            OS.Alert("Backend nicht erreichbar.\nBitte Backend starten.", "Verbindungsfehler");
            return;
        }
        StartePolling();
    }

    private void StartePolling()
    {
        _pollTimer = new Timer { WaitTime = 0.6 };
        _pollTimer.Timeout += OnPollTick;
        AddChild(_pollTimer);
        _pollTimer.Start();
    }

    private async void OnPollTick()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            var f = await ApiClient.GetAsync<Fortschritt>("spiel/fortschritt");
            if (f == null) return;

            _progressBar.Value  = f.Prozent;
            _progressLabel.Text = f.Nachricht;

            if (f.Fertig)
            {
                _pollTimer?.Stop();
                _pollTimer?.QueueFree();
                await LadeAngebote();
            }
        }
        finally { _polling = false; }
    }

    private async Task LadeAngebote()
    {
        var vereine = await ApiClient.GetAsync<List<Verein>>("verein/oberliga/zufaellig");
        if (vereine == null || vereine.Count == 0)
        {
            OS.Alert("Keine Vereine gefunden.", "Fehler");
            ZeigePanel(Zustand.Start);
            return;
        }

        foreach (Node child in _vereineContainer.GetChildren())
            child.QueueFree();

        foreach (var v in vereine)
            _vereineContainer.AddChild(BaueVereinCard(v));

        ZeigePanel(Zustand.Auswahl);
    }

    private void VereinWaehlen(Verein verein)
    {
        GameState.Instance.SetVerein(
            verein.Id, verein.Name,
            verein.Liga?.Id ?? 0, verein.Liga?.Name ?? "");
        GetTree().ChangeSceneToFile("res://scenes/GameMain.tscn");
    }
}
