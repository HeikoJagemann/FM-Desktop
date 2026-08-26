#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

public partial class StartScreen : Control
{
    private enum Zustand { Start, DbAuswahl, SpielstandAuswahl, Laden, Auswahl }

    private Control      _startPanel        = null!;
    private Control      _dbAuswahlPanel    = null!;
    private Control      _spielstandPanel   = null!;
    private Control      _ladenPanel        = null!;
    private Control      _auswahlPanel      = null!;

    private Button        _btnFortsetzen        = null!;
    private Button        _btnLaden             = null!;
    private Label         _spielstandHinweis    = null!;
    private VBoxContainer _spielstandContainer  = null!;
    private ProgressBar  _progressBar     = null!;
    private Label        _progressLabel   = null!;
    private HBoxContainer _vereineContainer = null!;

    private VBoxContainer _schemaListeContainer = null!;
    private string        _gewaehlteSchema      = "db_default";
    private readonly Dictionary<string, Button> _schemaButtons = new();

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

        _dbAuswahlPanel = BuildDbAuswahlPanel();
        _dbAuswahlPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_dbAuswahlPanel);

        _spielstandPanel = BuildSpielstandPanel();
        _spielstandPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_spielstandPanel);

        _ladenPanel = BuildLadenPanel();
        _ladenPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_ladenPanel);

        _auswahlPanel = BuildAuswahlPanel();
        _auswahlPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_auswahlPanel);

        // Version aus den Projekteinstellungen, nicht fest verdrahtet: Sonst zeigt der Startbildschirm
        // eine Version an, die mit dem tatsächlich laufenden Stand nichts zu tun hat.
        var version = FmTheme.MakeLabel($"v{Projektversion()}", 11, FmTheme.TextSecondary);
        version.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(version);
    }

    /// <summary>Version aus project.godot (config/version).</summary>
    private static string Projektversion()
    {
        var wert = ProjectSettings.GetSetting("application/config/version");
        var text = wert.AsString();
        return string.IsNullOrEmpty(text) ? "?" : text;
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
        panel.CustomMinimumSize = new Vector2(360, 0);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 40, 48);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("🏆", 52, FmTheme.Gold, HorizontalAlignment.Center));
        vbox.AddChild(FmTheme.MakeLabel("Fussball-Manager", 24, FmTheme.TextPrimary, HorizontalAlignment.Center));

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
        vbox.AddChild(spacer);

        var btnNeuesSpiel = MakeMenuButton("▶   Neues Spiel", FmTheme.Accent);
        btnNeuesSpiel.Pressed += OnSpielStarten;
        vbox.AddChild(btnNeuesSpiel);

        _btnFortsetzen = MakeMenuButton("↩   Spiel fortsetzen", FmTheme.BgPanel);
        _btnFortsetzen.TooltipText = "Zuletzt angelegtes Spiel fortsetzen";
        _btnFortsetzen.Pressed += OnSpielFortsetzen;
        vbox.AddChild(_btnFortsetzen);

        _btnLaden = MakeMenuButton("📂   Spiel laden", FmTheme.BgPanel);
        _btnLaden.Pressed += OnSpielLaden;
        vbox.AddChild(_btnLaden);

        var btnEinstellungen = MakeMenuButton("⚙   Einstellungen", FmTheme.BgPanel);
        btnEinstellungen.Disabled = true;
        btnEinstellungen.TooltipText = "Noch nicht verfügbar";
        vbox.AddChild(btnEinstellungen);

        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 4) };
        vbox.AddChild(spacer2);

        var btnBeenden = MakeMenuButton("✕   Beenden", FmTheme.BgPanel);
        btnBeenden.AddThemeColorOverride("font_color", FmTheme.Danger);
        btnBeenden.Pressed += () => GetTree().Quit();
        vbox.AddChild(btnBeenden);

        return center;
    }

    private static Button MakeMenuButton(string text, Color bg)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(280, 46),
            Flat = false,
        };
        FmTheme.ApplyButton(btn, bg);
        return btn;
    }

    private Control BuildDbAuswahlPanel()
    {
        var center = new CenterContainer();

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(440, 0);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 32, 40);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("🗄   Datenbank auswählen", 22, FmTheme.TextPrimary, HorizontalAlignment.Center));
        vbox.AddChild(FmTheme.MakeLabel("Wähle die Spielwelt, mit der du starten möchtest.", 13, FmTheme.TextSecondary, HorizontalAlignment.Center));

        var scrollContainer = new ScrollContainer { CustomMinimumSize = new Vector2(0, 180) };
        vbox.AddChild(scrollContainer);

        _schemaListeContainer = new VBoxContainer();
        _schemaListeContainer.AddThemeConstantOverride("separation", 6);
        scrollContainer.AddChild(_schemaListeContainer);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(hbox);

        var btnZurueck = new Button { Text = "←   Zurück", CustomMinimumSize = new Vector2(140, 40) };
        FmTheme.ApplyButton(btnZurueck, FmTheme.BgPanel);
        btnZurueck.Pressed += () => ZeigePanel(Zustand.Start);
        hbox.AddChild(btnZurueck);

        var btnWeiter = new Button { Text = "▶   Weiter", CustomMinimumSize = new Vector2(160, 40) };
        FmTheme.ApplyButton(btnWeiter, FmTheme.Accent);
        btnWeiter.Pressed += OnDbAuswahlBestaetigt;
        hbox.AddChild(btnWeiter);

        return center;
    }

    private void AktualisiereSchemaButtons()
    {
        foreach (var (schema, btn) in _schemaButtons)
        {
            var bg = schema == _gewaehlteSchema ? FmTheme.Accent : FmTheme.BgPanel;
            FmTheme.ApplyButton(btn, bg);
        }
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

        vbox.AddChild(FmTheme.MakeLabel("⚙", 48, align: HorizontalAlignment.Center));
        vbox.AddChild(FmTheme.MakeLabel("Ligenwelt wird aufgebaut …", 20, FmTheme.TextPrimary, HorizontalAlignment.Center));

        _progressLabel = FmTheme.MakeLabel("Starte …", 13, FmTheme.TextSecondary);
        _progressLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_progressLabel);

        _progressBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(0, 18) };
        vbox.AddChild(_progressBar);

        return center;
    }

    private Control BuildSpielstandPanel()
    {
        var center = new CenterContainer();

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(560, 0);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 32);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("Spiel laden", 20, FmTheme.TextPrimary));

        _spielstandHinweis = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        vbox.AddChild(_spielstandHinweis);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300) };
        vbox.AddChild(scroll);

        _spielstandContainer = new VBoxContainer();
        _spielstandContainer.AddThemeConstantOverride("separation", 6);
        _spielstandContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_spielstandContainer);

        var zurueck = new Button { Text = "←   Zurück", CustomMinimumSize = new Vector2(140, 40) };
        FmTheme.ApplyButton(zurueck, FmTheme.BgPanel);
        zurueck.Pressed += () => ZeigePanel(Zustand.Start);
        vbox.AddChild(zurueck);

        return center;
    }

    private async void OnSpielLaden()
    {
        ZeigePanel(Zustand.SpielstandAuswahl);
        _spielstandHinweis.Text = "Lade Spielstände …";

        foreach (Node child in _spielstandContainer.GetChildren())
            child.QueueFree();

        var staende = await ApiClient.GetAsync<List<SpielstandInfo>>("schemas/saves/uebersicht");
        if (staende == null || staende.Count == 0)
        {
            _spielstandHinweis.Text = "Keine gespeicherten Spiele vorhanden. "
                                    + "Starte zunächst ein neues Spiel.";
            return;
        }

        _spielstandHinweis.Text = $"{staende.Count} gespeicherte(s) Spiel(e) – neueste zuerst";
        foreach (var stand in staende)
            _spielstandContainer.AddChild(BaueSpielstandZeile(stand));
    }

    private Control BaueSpielstandZeile(SpielstandInfo stand)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(0, 56),
            Alignment = HorizontalAlignment.Left,
            Disabled = !stand.Ladbar,
            TooltipText = stand.Ladbar ? stand.Schema : "Diesem Spielstand fehlt die Vereinszuordnung.",
        };
        FmTheme.ApplyButton(btn, stand.Ladbar ? FmTheme.BgPanel : FmTheme.BgDark);
        btn.Text = $"{stand.Titel}\n{stand.Untertitel}";
        btn.AddThemeColorOverride("font_color",
            stand.Ladbar ? FmTheme.TextPrimary : FmTheme.TextSecondary);

        if (stand.Ladbar)
            btn.Pressed += () => SpielstandLaden(stand);

        return btn;
    }

    private void SpielstandLaden(SpielstandInfo stand)
    {
        GameState.Instance.SetSchema(stand.Schema);
        GameState.Instance.SetVerein(
            stand.VereinId!.Value, stand.VereinName ?? "",
            stand.LigaId ?? 0, stand.LigaName ?? "");
        GetTree().ChangeSceneToFile("res://scenes/GameMain.tscn");
    }

    /// <summary>Setzt den zuletzt angelegten Spielstand fort.</summary>
    private async void OnSpielFortsetzen()
    {
        var staende = await ApiClient.GetAsync<List<SpielstandInfo>>("schemas/saves/uebersicht");
        var neuester = staende?.FirstOrDefault(s => s.Ladbar);
        if (neuester == null)
        {
            OS.Alert("Es gibt kein fortsetzbares Spiel.", "Spiel fortsetzen");
            return;
        }
        SpielstandLaden(neuester);
    }

    private Control BuildAuswahlPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);

        vbox.AddChild(FmTheme.MakeLabel("Wähle deinen Verein – Oberliga", 22, FmTheme.TextPrimary, HorizontalAlignment.Center));
        vbox.AddChild(FmTheme.MakeLabel("Drei zufällige Vereine aus der Oberliga stehen zur Auswahl.", 13, FmTheme.TextSecondary, HorizontalAlignment.Center));

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

        vbox.AddChild(FmTheme.MakeLabel("🛡", 40, align: HorizontalAlignment.Center));

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
        _startPanel.Visible      = z == Zustand.Start;
        _dbAuswahlPanel.Visible  = z == Zustand.DbAuswahl;
        _spielstandPanel.Visible = z == Zustand.SpielstandAuswahl;
        _ladenPanel.Visible      = z == Zustand.Laden;
        _auswahlPanel.Visible    = z == Zustand.Auswahl;
    }

    private async void OnSpielStarten()
    {
        var schemas = await ApiClient.GetAsync<List<string>>("schemas");
        if (schemas == null || schemas.Count <= 1)
        {
            // Nur eine Editor-Datenbank vorhanden - Auswahl überspringen.
            ZeigePanel(Zustand.Laden);
            if (!await LegeSpielstandAn(schemas?.FirstOrDefault() ?? "db_default")) return;
            await LadeAngebote();
            return;
        }

        _gewaehlteSchema = "db_default";
        _schemaButtons.Clear();
        foreach (Node child in _schemaListeContainer.GetChildren())
            child.QueueFree();

        foreach (var schema in schemas)
        {
            var s = schema;
            var btn = new Button
            {
                Text = s,
                CustomMinimumSize = new Vector2(0, 38),
                Alignment = HorizontalAlignment.Left,
            };
            FmTheme.ApplyButton(btn, s == _gewaehlteSchema ? FmTheme.Accent : FmTheme.BgPanel);
            btn.Pressed += () =>
            {
                _gewaehlteSchema = s;
                AktualisiereSchemaButtons();
            };
            _schemaButtons[s] = btn;
            _schemaListeContainer.AddChild(btn);
        }

        ZeigePanel(Zustand.DbAuswahl);
    }

    private async void OnDbAuswahlBestaetigt()
    {
        ZeigePanel(Zustand.Laden);
        if (!await LegeSpielstandAn(_gewaehlteSchema)) return;
        await LadeAngebote();
    }

    /// <summary>
    /// Legt einen frischen Spielstand als Kopie der gewählten Editor-Datenbank an und spielt
    /// darin weiter. So bleibt die Vorlage unberührt und jedes neue Spiel beginnt bei Spieltag 1.
    /// </summary>
    private async Task<bool> LegeSpielstandAn(string vorlage)
    {
        _progressLabel.Text = "Neues Spiel wird angelegt …";
        _progressBar.Value  = 0;

        var name = $"spiel_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        var antwort = await ApiClient.PostAsync<object, Dictionary<string, string>>(
            "schemas/saves", new { name, basedOn = vorlage });

        if (antwort == null || !antwort.TryGetValue("schema", out var schema))
        {
            OS.Alert($"Der Spielstand konnte nicht angelegt werden.\nVorlage: {vorlage}", "Fehler");
            ZeigePanel(Zustand.Start);
            return false;
        }

        GameState.Instance.SetSchema(schema);
        _progressBar.Value = 50;
        _progressLabel.Text = "Lade Vereine …";
        return true;
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

    private async void VereinWaehlen(Verein verein)
    {
        GameState.Instance.SetVerein(
            verein.Id, verein.Name,
            verein.Liga?.Id ?? 0, verein.Liga?.Name ?? "");

        // Im Spielstand hinterlegen, damit das Spiel später fortgesetzt werden kann.
        await ApiClient.PostAsync<object, SpielstandInfo>(
            "spielstand", new { vereinId = verein.Id });

        GetTree().ChangeSceneToFile("res://scenes/GameMain.tscn");
    }
}
