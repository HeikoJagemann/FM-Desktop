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
    private Label   _vereinUnterzeile = null!;
    private Label   _spieltagLabel = null!;
    private Label   _kontoLabel = null!;
    private Label   _fortschrittLabel = null!;
    private Button  _trainingButton = null!;
    private AktionsKnopf _simulierenButton = null!;
    private HBoxContainer _unterreiter = null!;
    private HBoxContainer _kopfleiste = null!;
    private Control _vereinBlock = null!;
    private Control _zeitBlock = null!;

    /// <summary>Ob der Stand gerade "Training planen" anbietet - unabhängig davon, ob Platz dafür ist.</summary>
    private bool _trainingAngeboten;

    /// <summary>Innenabstand der Reiter; im schmalen Fenster rücken sie zusammen.</summary>
    private int _reiterRand = 14;
    private Timer   _pollTimer = null!;
    private KalenderStand? _kalenderStand;

    /// <summary>Die Reiter der Menüzeile, je Bereich.</summary>
    private readonly System.Collections.Generic.Dictionary<string, Button> _reiter = new();

    /// <summary>Sperrt weitere Abfragen, solange eine unterwegs ist.</summary>
    private bool _pollLaeuft;

    /// Aktuell angezeigte Szene - etwa fuer die Einstellungen, die sie nach dem Schliessen neu
    /// laden, weil die Views ihre Daten nur einmal in _Ready() holen.
    private string _aktuelleScene = SceneDashboard;

    private const string SceneDashboard   = "res://scenes/dashboard/DashboardView.tscn";
    private const string SceneKader       = "res://scenes/mannschaft/KaderView.tscn";
    private const string SceneMitarbeiter = "res://scenes/mitarbeiter/MitarbeiterView.tscn";
    private const string SceneJugend      = "res://scenes/jugend/JugendView.tscn";
    private const string SceneTalente     = "res://scenes/jugend/TalenteView.tscn";
    private const string SceneUmfeld      = "res://scenes/umfeld/UmfeldView.tscn";
    private const string SceneTabelle     = "res://scenes/liga/TabelleView.tscn";
    private const string SceneSpielplan   = "res://scenes/liga/SpielplanView.tscn";
    private const string SceneStatistiken = "res://scenes/liga/StatistikenView.tscn";
    private const string SceneAufstellung = "res://scenes/taktik/AufstellungView.tscn";
    private const string SceneTaktik      = "res://scenes/taktik/TaktikView.tscn";
    private const string SceneTraining     = "res://scenes/training/TrainingView.tscn";
    private const string SceneKalender     = "res://scenes/kalender/KalenderView.tscn";
    private const string SceneFinanzen     = "res://scenes/finanzen/FinanzenView.tscn";
    private const string SceneTransfer     = "res://scenes/transfer/TransferView.tscn";
    private const string SceneScouting     = "res://scenes/transfer/ScoutingView.tscn";
    private const string SceneAngebote     = "res://scenes/transfer/AngeboteView.tscn";

    /// Eigener Bildschirm statt Overlay - die Partie übernimmt das ganze Fenster.
    private const string SceneSpiel        = "res://scenes/spiel/SpielView.tscn";

    /// <summary>
    /// Die Bereiche der Menüzeile und ihre Seiten. Die Seiten eines Bereichs stehen in der
    /// Unterzeile - vorher lagen sie in Ausklappmenüs und waren erst nach einem Klick zu sehen.
    /// </summary>
    private static readonly (string Bereich, (string Seite, string Scene)[] Seiten)[] Bereiche =
    {
        ("Übersicht",  new[] { ("Dashboard", SceneDashboard) }),
        ("Mannschaft", new[] { ("Kader", SceneKader), ("Mitarbeiter", SceneMitarbeiter) }),
        ("Taktik",     new[] { ("Aufstellung", SceneAufstellung), ("Taktik", SceneTaktik) }),
        ("Training",   new[] { ("Trainingsplan", SceneTraining) }),
        ("Liga",       new[] { ("Tabelle", SceneTabelle), ("Spielplan", SceneSpielplan), ("Statistiken", SceneStatistiken) }),
        ("Kalender",   new[] { ("Termine", SceneKalender) }),
        ("Transfers",  new[] { ("Transferliste", SceneTransfer), ("Scouting", SceneScouting), ("Angebote", SceneAngebote) }),
        ("Finanzen",   new[] { ("Übersicht", SceneFinanzen) }),
        ("Umfeld",     new[] { ("Anlagen", SceneUmfeld) }),
        ("Jugend",     new[] { ("Jugendkader", SceneJugend), ("Talente", SceneTalente) }),
    };

    public override async void _Ready()
    {
        BuildUI();

        _pollTimer = new Timer { WaitTime = 0.5, OneShot = false, Autostart = false };
        _pollTimer.Timeout += OnPollTimeout;
        AddChild(_pollTimer);

        // Erst den Kalender, dann die Seite: Sie rechnet das Alter der Spieler mit dem Spieldatum.
        // Vorher kam die Seite zuerst und rechnete mit dem heutigen Kalendertag - ein Spieler war
        // auf der ersten Seite nach dem Start zwei Jahre aelter als auf jeder weiteren.
        await AktualisiereKalenderstand();
        if (!IsInstanceValid(this)) return;

        // Jeder Spielstart beginnt auf dem Dashboard - dort steht, was seit dem letzten Mal passiert ist.
        LadeScene(GameState.Instance.StartAnsicht ?? SceneDashboard);
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

        vbox.AddChild(BuildToolbar());
        vbox.AddChild(BuildUnterzeile());

        // Inhaltsbereich
        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 28, 28, 20, 20);
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(margin);

        _contentArea = new Control();
        _contentArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddChild(_contentArea);
    }

    /// <summary>
    /// Die Menüzeile: links der Verein, in der Mitte die Bereiche als Reiter, rechts Datum,
    /// Kontostand und der Knopf, der die Zeit weiterlaufen lässt.
    /// </summary>
    private Control BuildToolbar()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 72) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.ToolbarStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 12, 16, 0, 0);
        panel.AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(hbox);
        _kopfleiste = hbox;

        // Zurück zum Hauptmenü
        var zurueck = SymbolKnopf(Symbol.Art.Zurueck, "Hauptmenü", flach: true);
        zurueck.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/StartScreen.tscn");
        hbox.AddChild(zurueck);

        // Verein
        hbox.AddChild(Wappen.Neu(GameState.Instance.VereinName));
        var verein = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        verein.AddThemeConstantOverride("separation", 0);
        _vereinLabel = FmTheme.MakeTitel(GameState.Instance.VereinName, 21);
        verein.AddChild(_vereinLabel);
        _vereinUnterzeile = FmTheme.MakeLabel(GameState.Instance.LigaName, 12, FmTheme.TextSecondary);
        verein.AddChild(_vereinUnterzeile);
        hbox.AddChild(verein);
        _vereinBlock = verein;

        // Bereiche
        var reiter = new HBoxContainer();
        reiter.AddThemeConstantOverride("separation", 0);
        foreach (var (bereich, seiten) in Bereiche)
        {
            var knopf = new Button { Text = bereich, FocusMode = FocusModeEnum.None };
            SetzeReiterStil(knopf, aktiv: false);
            var ziel = seiten[0].Scene;
            knopf.Pressed += () => LadeScene(ziel);
            _reiter[bereich] = knopf;
            reiter.AddChild(knopf);
        }
        hbox.AddChild(reiter);

        hbox.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // Fortschritt der laufenden Simulation
        _fortschrittLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        _fortschrittLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        hbox.AddChild(_fortschrittLabel);

        // Spieldatum und Kontostand
        var zeit = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        zeit.AddThemeConstantOverride("separation", 0);
        _spieltagLabel = FmTheme.MakeLabel("", 14, FmTheme.TextPrimary, HorizontalAlignment.Right);
        _spieltagLabel.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        zeit.AddChild(_spieltagLabel);
        _kontoLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary, HorizontalAlignment.Right);
        zeit.AddChild(_kontoLabel);
        hbox.AddChild(zeit);
        _zeitBlock = zeit;

        // Nur am Wochenstart: Abkürzung zur Trainingsplanung, denn genau dafür hält er an.
        _trainingButton = new Button
        {
            Text = "Training planen",
            Visible = false,
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        FmTheme.ApplySecondaryButton(_trainingButton);
        _trainingButton.Pressed += () => LadeScene(SceneTraining);
        hbox.AddChild(_trainingButton);

        _simulierenButton = AktionsKnopf.Neu(Symbol.Art.Abspielen, "Weiter");
        _simulierenButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _simulierenButton.Pressed += OnSimulierenPressed;
        hbox.AddChild(_simulierenButton);

        // Einstellungen (Fenstermodus, Gehaltsanzeige)
        var einstellungen = SymbolKnopf(Symbol.Art.Zahnrad, "Einstellungen", flach: false);
        einstellungen.Pressed += () => EinstellungenDialog.Zeige(this, () => LadeScene(_aktuelleScene));
        hbox.AddChild(einstellungen);

        GetViewport().SizeChanged += PasseKopfleisteAn;
        return panel;
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= PasseKopfleisteAn;

    /// <summary>
    /// Im schmalen Fenster verzichtet die Menüzeile Stück für Stück auf Entbehrliches, statt
    /// breiter als das Fenster zu werden und alles darunter mitzuziehen: zuerst "Training planen"
    /// (der Bereich Training bleibt), dann die Unterzeile des Weiter-Knopfs (sie steht auch im
    /// Tooltip), der Vereinsname neben dem Wappen, der Abstand der Reiter und zuletzt das Datum.
    /// </summary>
    private void PasseKopfleisteAn()
    {
        if (_kopfleiste == null || !IsInstanceValid(_kopfleiste)) return;
        // Ränder der Menüzeile: 12 links, 16 rechts.
        float verfuegbar = GetViewportRect().Size.X - 28;

        _trainingButton.Visible = _trainingAngeboten;
        _simulierenButton.OhneUnterzeile = false;
        _vereinBlock.Visible = true;
        SetzeReiterRand(14);
        _zeitBlock.Visible = true;

        var verzichte = new System.Action[]
        {
            () => _trainingButton.Visible = false,
            () => _simulierenButton.OhneUnterzeile = true,
            () => _vereinBlock.Visible = false,
            () => SetzeReiterRand(7),
            () => _zeitBlock.Visible = false,
        };
        foreach (var schritt in verzichte)
        {
            if (_kopfleiste.GetCombinedMinimumSize().X <= verfuegbar) break;
            schritt();
        }
    }

    private void SetzeReiterRand(int rand)
    {
        if (_reiterRand == rand) return;
        _reiterRand = rand;
        foreach (var knopf in _reiter.Values)
            SetzeReiterStil(knopf, knopf.HasMeta("aktiv") && (bool)knopf.GetMeta("aktiv"));
    }

    /// <summary>Die Seiten des aktiven Bereichs - Tabelle, Spielplan, Statistiken.</summary>
    private Control BuildUnterzeile()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 48) };
        var stil = new StyleBoxFlat { BgColor = FmTheme.BgSubbar, BorderColor = FmTheme.Border, BorderWidthBottom = 1 };
        stil.SetContentMarginAll(0);
        panel.AddThemeStyleboxOverride("panel", stil);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 24, 24, 7, 7);
        panel.AddChild(margin);

        _unterreiter = new HBoxContainer();
        _unterreiter.AddThemeConstantOverride("separation", 4);
        margin.AddChild(_unterreiter);
        return panel;
    }

    /// <summary>Ein quadratischer Knopf mit Symbol statt Text.</summary>
    private static Button SymbolKnopf(Symbol.Art art, string tooltip, bool flach)
    {
        var knopf = new Button
        {
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(44, 44),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.None,
        };
        if (flach)
        {
            foreach (var zustand in new[] { "normal", "focus" })
                knopf.AddThemeStyleboxOverride(zustand, new StyleBoxEmpty());
            knopf.AddThemeStyleboxOverride("hover", FmTheme.Flaeche(FmTheme.BgHover, 10));
            knopf.AddThemeStyleboxOverride("pressed", FmTheme.Flaeche(FmTheme.Border, 10));
        }
        else
        {
            FmTheme.ApplySecondaryButton(knopf);
        }
        var symbol = Symbol.Neu(art, flach ? FmTheme.TextNav : FmTheme.TextPrimary, 20);
        symbol.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        symbol.OffsetLeft = symbol.OffsetTop = 12;
        symbol.OffsetRight = symbol.OffsetBottom = -12;
        knopf.AddChild(symbol);
        return knopf;
    }

    /// <summary>Reiter der Menüzeile: flach, der aktive mit Bernstein-Unterstreichung.</summary>
    private void SetzeReiterStil(Button knopf, bool aktiv)
    {
        knopf.SetMeta("aktiv", aktiv);
        var stil = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = FmTheme.Accent,
            BorderWidthBottom = aktiv ? 3 : 0,
            ContentMarginLeft = _reiterRand,
            ContentMarginRight = _reiterRand,
        };
        var schwebend = (StyleBoxFlat)stil.Duplicate();
        foreach (var zustand in new[] { "normal", "pressed", "focus", "disabled" })
            knopf.AddThemeStyleboxOverride(zustand, stil);
        knopf.AddThemeStyleboxOverride("hover", schwebend);

        var farbe = aktiv ? FmTheme.TextPrimary : FmTheme.TextNav;
        knopf.AddThemeColorOverride("font_color", farbe);
        knopf.AddThemeColorOverride("font_pressed_color", farbe);
        knopf.AddThemeColorOverride("font_focus_color", farbe);
        knopf.AddThemeColorOverride("font_hover_color", FmTheme.TextPrimary);
        knopf.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        knopf.AddThemeFontSizeOverride("font_size", 14);
    }

    /// <summary>Seite der Unterzeile: eine Pille, die aktive hinterlegt.</summary>
    private static void SetzeSeitenStil(Button knopf, bool aktiv)
    {
        var stil = FmTheme.Flaeche(aktiv ? FmTheme.BgHover : new Color(0, 0, 0, 0), 17);
        stil.ContentMarginLeft = stil.ContentMarginRight = 14;
        var schwebend = FmTheme.Flaeche(aktiv ? FmTheme.BgHover : new Color("18241F"), 17);
        schwebend.ContentMarginLeft = schwebend.ContentMarginRight = 14;
        foreach (var zustand in new[] { "normal", "pressed", "focus" })
            knopf.AddThemeStyleboxOverride(zustand, stil);
        knopf.AddThemeStyleboxOverride("hover", schwebend);

        var farbe = aktiv ? FmTheme.TextPrimary : FmTheme.TextNav;
        knopf.AddThemeColorOverride("font_color", farbe);
        knopf.AddThemeColorOverride("font_pressed_color", farbe);
        knopf.AddThemeColorOverride("font_hover_color", FmTheme.TextPrimary);
        knopf.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        knopf.AddThemeFontSizeOverride("font_size", 13);
    }

    /// <summary>Markiert Bereich und Seite der geladenen Szene und füllt die Unterzeile.</summary>
    private void ZeigeNavigation(string scene)
    {
        foreach (var (bereich, seiten) in Bereiche)
        {
            bool aktiv = System.Array.Exists(seiten, x => x.Scene == scene);
            SetzeReiterStil(_reiter[bereich], aktiv);
            if (!aktiv) continue;

            foreach (Node kind in _unterreiter.GetChildren())
                kind.QueueFree();
            foreach (var (seite, ziel) in seiten)
            {
                var knopf = new Button { Text = seite, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 34) };
                SetzeSeitenStil(knopf, ziel == scene);
                var z = ziel;
                knopf.Pressed += () => LadeScene(z);
                _unterreiter.AddChild(knopf);
            }
        }
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
        // Nach jedem Schritt zurück aufs Dashboard: Ereignisse, Ergebnisse und Training der
        // vergangenen Tage stehen dort.
        LadeScene(SceneDashboard);
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

        _spieltagLabel.Text = $"{stand.Wochentag} {stand.Datum:dd.MM.yyyy} · {stand.Uhrzeit:HH\\:mm}";
        _spieltagLabel.TooltipText = $"Saison {stand.SaisonText} · {stand.Phase}";
        ZeigeEreignis(stand);
        await AktualisiereVerein(stand);
    }

    /// <summary>Tabellenplatz, Punkte und Kontostand für die Vereinszeile der Menüzeile.</summary>
    private async System.Threading.Tasks.Task AktualisiereVerein(KalenderStand stand)
    {
        var vereinId = GameState.Instance.VereinId;
        var tabelleTask = ApiClient.GetAsync<System.Collections.Generic.List<TabellenEintrag>>(
            $"liga/{GameState.Instance.LigaId}/tabelle");
        var verein = await ApiClient.GetAsync<Verein>($"verein/{vereinId}");
        var tabelle = await tabelleTask;
        if (!IsInstanceValid(this)) return;

        var eigener = tabelle?.Find(e => e.Verein?.Id == vereinId);
        _vereinUnterzeile.Text = eigener != null && eigener.Spiele > 0
            ? $"{GameState.Instance.LigaName} · {eigener.Platz}. Platz · {eigener.Punkte} Punkte"
            : GameState.Instance.LigaName;

        _kontoLabel.Text = verein != null
            ? $"{stand.Phase} · Konto {FmTheme.Geld(verein.Kontostand)}"
            : stand.Phase;
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

        // Der Wochenstart hält genau dafür an, dass hier geplant wird.
        _trainingAngeboten = hier is { IstWochenstart: true };

        _simulierenButton.Disabled = false;
        // Die Unterzeile sagt, was der Knopf tut: Bei einer eigenen Partie, gegen wen; sonst, bis
        // wohin die Zeit läuft.
        _simulierenButton.Setze(hier?.AktionText ?? "Weiter",
            hier is { IstSpiel: true } ? hier.Titel
            : ziel != null ? $"bis {ziel.Titel}, {ziel.Datum:dd.MM.}" : "");
        _simulierenButton.TooltipText = hier is { IstSpiel: true }
            ? $"{hier.Titel} · {hier.Untertitel}"
            : ziel == null
                ? "Die Zeit weiterlaufen lassen"
                : $"Nächster Halt: {ziel.Titel} ({ziel.ZeitText})";
        PasseKopfleisteAn();
    }

    private void LadeScene(string path)
    {
        _aktuelleScene = path;
        ZeigeNavigation(path);

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
