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

    private AktionsKnopf  _btnFortsetzen        = null!;
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
        _ = ZeigeLetztenSpielstand();
    }

    /// <summary>Schreibt unter "Spiel fortsetzen", welcher Spielstand das ist.</summary>
    private async Task ZeigeLetztenSpielstand()
    {
        var staende = await ApiClient.GetAsync<List<SpielstandInfo>>("schemas/saves/uebersicht");
        if (!IsInstanceValid(this)) return;
        var neuester = staende?.FirstOrDefault(s => s.Ladbar);
        if (neuester == null)
        {
            _btnFortsetzen.Setze("Spiel fortsetzen", "kein Spielstand vorhanden");
            _btnFortsetzen.Disabled = true;
            return;
        }
        _btnFortsetzen.Setze("Spiel fortsetzen",
            string.IsNullOrEmpty(neuester.LigaName) ? neuester.Titel : $"{neuester.Titel} · {neuester.LigaName}");
    }

    // ── UI aufbauen ───────────────────────────────────────────

    private void BuildUI()
    {
        AddChild(Hintergrundfeld.Neu());

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
        vbox.AddThemeConstantOverride("separation", 2);

        var kicker = FmTheme.MakeLabel("DEINE TRAINERKARRIERE", 13, FmTheme.TextSecondary);
        kicker.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        kicker.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(kicker);

        var title = FmTheme.MakeTitel("Fussball-Manager", 64);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        return vbox;
    }

    private Control BuildStartPanel()
    {
        var center = new CenterContainer();

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(380, 0);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(16));
        center.AddChild(panel);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 28, 28, 28, 20);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        margin.AddChild(vbox);

        // Fortsetzen ist der häufigste Weg ins Spiel - deshalb der eine Bernstein-Knopf, und er
        // sagt gleich, welcher Spielstand es ist.
        _btnFortsetzen = AktionsKnopf.Neu(Symbol.Art.Abspielen, "Spiel fortsetzen", "zuletzt gespielter Spielstand");
        _btnFortsetzen.TooltipText = "Zuletzt angelegtes Spiel fortsetzen";
        _btnFortsetzen.Pressed += OnSpielFortsetzen;
        vbox.AddChild(_btnFortsetzen);

        var btnNeuesSpiel = MakeMenuButton("Neues Spiel", FmTheme.BgKnopf);
        btnNeuesSpiel.TooltipText = "Einen Verein in der Oberliga übernehmen";
        btnNeuesSpiel.Pressed += OnSpielStarten;
        vbox.AddChild(btnNeuesSpiel);

        _btnLaden = MakeMenuButton("Spiel laden", FmTheme.BgKnopf);
        _btnLaden.Pressed += OnSpielLaden;
        vbox.AddChild(_btnLaden);

        var btnEinstellungen = MakeMenuButton("Einstellungen", FmTheme.BgKnopf);
        btnEinstellungen.Pressed += () => EinstellungenDialog.Zeige(this);
        vbox.AddChild(btnEinstellungen);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });

        var btnBeenden = new Button
        {
            Text = "Beenden",
            CustomMinimumSize = new Vector2(0, 36),
            FocusMode = FocusModeEnum.None,
        };
        foreach (var zustand in new[] { "normal", "pressed", "focus" })
            btnBeenden.AddThemeStyleboxOverride(zustand, new StyleBoxEmpty());
        btnBeenden.AddThemeStyleboxOverride("hover", FmTheme.Flaeche(FmTheme.BgHover, 10));
        btnBeenden.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        btnBeenden.AddThemeColorOverride("font_hover_color", FmTheme.Danger);
        btnBeenden.Pressed += () => GameState.Instance.Beende();
        vbox.AddChild(btnBeenden);

        return center;
    }

    private static Button MakeMenuButton(string text, Color bg)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(280, 48),
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

        var dbTitel = FmTheme.MakeTitel("Spielwelt wählen", 30);
        dbTitel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(dbTitel);
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

        var btnZurueck = new Button { Text = "Zurück", CustomMinimumSize = new Vector2(140, 44) };
        FmTheme.ApplySecondaryButton(btnZurueck);
        btnZurueck.Pressed += () => ZeigePanel(Zustand.Start);
        hbox.AddChild(btnZurueck);

        var btnWeiter = new Button { Text = "Weiter", CustomMinimumSize = new Vector2(160, 44) };
        FmTheme.ApplyPrimaryButton(btnWeiter);
        btnWeiter.Pressed += OnDbAuswahlBestaetigt;
        hbox.AddChild(btnWeiter);

        return center;
    }

    private void AktualisiereSchemaButtons()
    {
        foreach (var (schema, btn) in _schemaButtons)
        {
            FmTheme.ApplyButton(btn, schema == _gewaehlteSchema ? FmTheme.BgHover : FmTheme.BgKnopf);
            btn.AddThemeColorOverride("font_color", schema == _gewaehlteSchema ? FmTheme.Accent : FmTheme.TextPrimary);
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

        var ladeTitel = FmTheme.MakeTitel("Ligenwelt wird aufgebaut", 30);
        ladeTitel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(ladeTitel);

        _progressLabel = FmTheme.MakeLabel("Starte …", 13, FmTheme.TextSecondary);
        _progressLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_progressLabel);

        _progressBar = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10),
        };
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

        vbox.AddChild(FmTheme.MakeTitel("Spiel laden", 30));

        _spielstandHinweis = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        vbox.AddChild(_spielstandHinweis);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300) };
        vbox.AddChild(scroll);

        _spielstandContainer = new VBoxContainer();
        _spielstandContainer.AddThemeConstantOverride("separation", 6);
        _spielstandContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_spielstandContainer);

        var zurueck = new Button
        {
            Text = "Zurück",
            CustomMinimumSize = new Vector2(140, 44),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        FmTheme.ApplySecondaryButton(zurueck);
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
        FmTheme.ApplyButton(btn, stand.Ladbar ? FmTheme.BgKnopf : FmTheme.BgDark);
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

        var wahlTitel = FmTheme.MakeTitel("Wähle deinen Verein", 34);
        wahlTitel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(wahlTitel);
        vbox.AddChild(FmTheme.MakeLabel("Drei zufällige Vereine aus der Oberliga stehen zur Auswahl.", 13, FmTheme.TextSecondary, HorizontalAlignment.Center));

        _vereineContainer = new HBoxContainer();
        _vereineContainer.AddThemeConstantOverride("separation", 24);
        _vereineContainer.Alignment = BoxContainer.AlignmentMode.Center;
        _vereineContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(_vereineContainer);

        var andereBtn = new Button { Text = "Andere Vereine anzeigen", CustomMinimumSize = new Vector2(240, 44) };
        FmTheme.ApplySecondaryButton(andereBtn);
        andereBtn.Pressed += async () => await LadeAngebote();

        var btnCenter = new CenterContainer();
        btnCenter.AddChild(andereBtn);
        vbox.AddChild(btnCenter);

        return vbox;
    }

    private Control BaueVereinCard(Verein verein)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(290, 330);
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(16));

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        var wappen = new CenterContainer();
        wappen.AddChild(Wappen.Neu(verein.Name, 76, FmTheme.BorderStark));
        vbox.AddChild(wappen);

        var name = FmTheme.MakeTitel(verein.Name, 24);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(name);

        var liga = FmTheme.MakeLabel(verein.Liga?.Name ?? "", 13, FmTheme.TextSecondary);
        liga.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(liga);

        int stars = Math.Clamp((int)Math.Round(verein.Staerke / 20.0), 1, 5);
        var sterne = FmTheme.MakeLabel(new string('★', stars) + new string('☆', 5 - stars), 22, FmTheme.Gold);
        sterne.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(sterne);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        vbox.AddChild(spacer);

        var btn = new Button { Text = "Verein übernehmen", CustomMinimumSize = new Vector2(0, 44) };
        FmTheme.ApplyPrimaryButton(btn);
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
        // db_default ist keine Spielwelt mehr, sondern veraltet mit jedem Ausbau der Welterzeugung.
        // Zur Wahl stehen nur Welten, die jemand im Editor eigens angelegt hat.
        var schemas = (await ApiClient.GetAsync<List<string>>("schemas") ?? new List<string>())
            .Where(s => s != "db_default").ToList();
        if (schemas.Count == 0)
        {
            ZeigePanel(Zustand.Laden);
            if (!await LegeSpielstandAn(NeueWelt)) return;
            await LadeAngebote();
            return;
        }

        _gewaehlteSchema = NeueWelt;
        _schemaButtons.Clear();
        foreach (Node child in _schemaListeContainer.GetChildren())
            child.QueueFree();

        foreach (var schema in schemas.Prepend(NeueWelt))
        {
            var s = schema;
            var btn = new Button
            {
                Text = s == NeueWelt ? "Neue Welt erzeugen (empfohlen)" : s,
                CustomMinimumSize = new Vector2(0, 38),
                Alignment = HorizontalAlignment.Left,
            };
            FmTheme.ApplyButton(btn, s == _gewaehlteSchema ? FmTheme.BgHover : FmTheme.BgKnopf);
            btn.AddThemeColorOverride("font_color", s == _gewaehlteSchema ? FmTheme.Accent : FmTheme.TextPrimary);
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

    /// <summary>Auswahl in der Weltenliste: keine Vorlage, sondern eine frisch erzeugte Welt.</summary>
    private const string NeueWelt = "";

    /// <summary>
    /// Legt einen Spielstand an und spielt darin weiter: mit einer frisch erzeugten Welt
    /// (<see cref="NeueWelt"/>) oder als Kopie einer im Editor angelegten Welt, die dabei
    /// unberührt bleibt. Jedes neue Spiel beginnt bei Spieltag 1.
    /// </summary>
    private async Task<bool> LegeSpielstandAn(string vorlage)
    {
        _progressLabel.Text = "Neues Spiel wird angelegt …";
        _progressBar.Value  = 0;

        var name = $"spiel_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        if (vorlage == NeueWelt)
        {
            return await ErzeugeWelt(name);
        }
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

    /// <summary>
    /// Legt einen leeren Spielstand an und lässt darin Ligen, Vereine, Kader und Spielpläne
    /// erzeugen - das dauert etwa eine halbe Minute, der Balken zeigt den Fortschritt.
    /// </summary>
    private async Task<bool> ErzeugeWelt(string name)
    {
        var antwort = await ApiClient.PostAsync<object, Dictionary<string, string>>(
            "schemas/saves/neu", new { name });
        if (antwort == null || !antwort.TryGetValue("schema", out var schema))
        {
            OS.Alert("Der Spielstand konnte nicht angelegt werden.", "Fehler");
            ZeigePanel(Zustand.Start);
            return false;
        }
        GameState.Instance.SetSchema(schema);

        _progressLabel.Text = "Die Welt wird erzeugt …";
        string? fehler = await ApiClient.PostAsync("spiel/initialisieren") ? null : "Die Erzeugung ließ sich nicht starten.";
        while (fehler == null)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            var stand = await ApiClient.GetAsync<InitFortschritt>("spiel/fortschritt");
            if (!IsInstanceValid(this)) return false;
            if (stand == null)
            {
                fehler = "Das Backend antwortet nicht mehr.";
                break;
            }
            _progressBar.Value  = stand.Prozent;
            _progressLabel.Text = stand.Nachricht;
            if (stand.Fertig)
            {
                // Eine abgebrochene Erzeugung meldet sich auch als fertig - mit "Fehler: ...".
                var ligen = await ApiClient.GetAsync<List<Models.Liga>>("liga");
                if (stand.Nachricht.StartsWith("Fehler") || ligen == null || ligen.Count == 0)
                    fehler = stand.Nachricht;
                break;
            }
        }

        if (fehler != null)
        {
            // Eine halbe Welt ist kein Spielstand - weg damit, statt sie in der Liste zu lassen.
            await ApiClient.SendeAsync("DELETE", $"schemas/saves/{schema}");
            GameState.Instance.SetSchema("db_default");
            OS.Alert($"Die Welt konnte nicht erzeugt werden.\n{fehler}", "Fehler");
            ZeigePanel(Zustand.Start);
            return false;
        }
        _progressLabel.Text = "Lade Vereine …";
        return true;
    }

    /// <summary>Stand der Welterzeugung (GET spiel/fortschritt).</summary>
    private sealed class InitFortschritt
    {
        public string Nachricht { get; set; } = "";
        public int    Prozent   { get; set; }
        public bool   Fertig    { get; set; }
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
