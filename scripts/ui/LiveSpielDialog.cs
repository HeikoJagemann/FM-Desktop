#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Bildschirmsimulation der eigenen Partie: Die Uhr läuft von 0 bis 90, der Spieler kann
/// jederzeit auswechseln.
///
/// <para>Anders als früher wird hier kein fertiges Protokoll abgespielt – der Server rechnet
/// minutenweise weiter, sobald dieser Dialog es anfordert. Nur so kann ein Wechsel den weiteren
/// Verlauf tatsächlich beeinflussen.</para>
/// </summary>
public partial class LiveSpielDialog : Control
{
    /// Sekunden pro Spielminute.
    private const double SekundenProMinute = 0.20;
    private const int Spielminuten = 90;

    private static LiveSpielDialog? _instanz;

    private LiveSpiel _spiel = null!;
    private Action? _beimSchliessen;

    private Label _ergebnisLabel = null!;
    private Label _minuteLabel = null!;
    private Label _wechselLabel = null!;
    private ProgressBar _uhr = null!;
    private VBoxContainer _log = null!;
    private ScrollContainer _logScroll = null!;
    private Button _wechselButton = null!;
    private Button _aktionButton = null!;
    private Timer _timer = null!;

    private VBoxContainer _heimSpalte = null!;
    private VBoxContainer _gastSpalte = null!;
    private Label _heimStaerkeLabel = null!;
    private Label _gastStaerkeLabel = null!;

    private int _gezeigteEreignisse;
    private bool _abgepfiffen;
    private bool _halbzeitGezeigt;
    private bool _wartetAufServer;
    private bool _spieltagLaeuft;

    public static void Zeige(Node caller, LiveSpiel spiel, Action? beimSchliessen = null)
    {
        if (_instanz != null && IsInstanceValid(_instanz)) _instanz.QueueFree();

        var dialog = new LiveSpielDialog { _spiel = spiel, _beimSchliessen = beimSchliessen };
        _instanz = dialog;
        var scene = caller.GetTree().CurrentScene ?? caller.GetTree().Root;
        scene.AddChild(dialog);
    }

    public override void _ExitTree()
    {
        if (_instanz == this) _instanz = null;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.65f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);
        center.AddChild(BuildCard());

        SchreibeZeile(0, "Anpfiff", FmTheme.TextSecondary);
        Aktualisiere(_spiel);

        _timer = new Timer { WaitTime = SekundenProMinute, OneShot = false };
        _timer.Timeout += OnTick;
        AddChild(_timer);
        _timer.Start();
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    private Control BuildCard()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(940, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(10));

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 24);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        // Kopfzeile
        var kopf = new HBoxContainer();
        var titel = FmTheme.MakeLabel("⚽  Bildschirmsimulation", 14, FmTheme.TextSecondary);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(titel);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(32, 32) };
        FmTheme.ApplyButton(close, FmTheme.BgPanel);
        close.Pressed += Schliessen;
        kopf.AddChild(close);
        root.AddChild(kopf);

        // Spielstand
        var stand = new HBoxContainer();
        stand.AddThemeConstantOverride("separation", 12);

        var heim = FmTheme.MakeLabel(_spiel.HeimVerein ?? "", 17,
            _spiel.EigenesHeimspiel ? FmTheme.Accent : FmTheme.TextPrimary, HorizontalAlignment.Right);
        heim.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stand.AddChild(heim);

        _ergebnisLabel = FmTheme.MakeLabel("0 : 0", 26, FmTheme.TextPrimary, HorizontalAlignment.Center);
        _ergebnisLabel.CustomMinimumSize = new Vector2(110, 0);
        stand.AddChild(_ergebnisLabel);

        var gast = FmTheme.MakeLabel(_spiel.GastVerein ?? "", 17,
            !_spiel.EigenesHeimspiel ? FmTheme.Accent : FmTheme.TextPrimary);
        gast.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stand.AddChild(gast);
        root.AddChild(stand);

        // Uhr
        var uhrZeile = new HBoxContainer();
        uhrZeile.AddThemeConstantOverride("separation", 10);
        _uhr = new ProgressBar
        {
            MinValue = 0, MaxValue = Spielminuten, Value = 0, ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 12),
        };
        _uhr.AddThemeStyleboxOverride("fill", Balken(FmTheme.Success));
        _uhr.AddThemeStyleboxOverride("background", Balken(FmTheme.BgToolbar));
        uhrZeile.AddChild(_uhr);
        _minuteLabel = FmTheme.MakeLabel("0'", 15, FmTheme.TextPrimary, HorizontalAlignment.Right);
        _minuteLabel.CustomMinimumSize = new Vector2(48, 0);
        uhrZeile.AddChild(_minuteLabel);
        root.AddChild(uhrZeile);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        root.AddChild(sep);

        // Aufstellungen links/rechts, Verlauf in der Mitte
        var mitte = new HBoxContainer();
        mitte.AddThemeConstantOverride("separation", 10);
        mitte.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(mitte);

        mitte.AddChild(BuildTeamSpalte(_spiel.HeimVerein ?? "", _spiel.HeimStaerke,
            _spiel.EigenesHeimspiel, out _heimSpalte, out _heimStaerkeLabel));

        _logScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(330, 320),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        mitte.AddChild(_logScroll);
        _log = new VBoxContainer();
        _log.AddThemeConstantOverride("separation", 5);
        _log.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _logScroll.AddChild(_log);

        mitte.AddChild(BuildTeamSpalte(_spiel.GastVerein ?? "", _spiel.GastStaerke,
            !_spiel.EigenesHeimspiel, out _gastSpalte, out _gastStaerkeLabel));

        // Aktionsleiste
        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 8);

        _wechselLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        _wechselLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leiste.AddChild(_wechselLabel);

        _wechselButton = new Button { Text = "🔄  Wechseln", CustomMinimumSize = new Vector2(150, 38) };
        FmTheme.ApplyButton(_wechselButton, FmTheme.BgPanel);
        _wechselButton.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        _wechselButton.Pressed += OnWechselnPressed;
        leiste.AddChild(_wechselButton);

        _aktionButton = new Button { Text = "⏩  Überspringen", CustomMinimumSize = new Vector2(170, 38) };
        FmTheme.ApplyButton(_aktionButton, FmTheme.BgPanel);
        _aktionButton.Pressed += OnAktionPressed;
        leiste.AddChild(_aktionButton);

        root.AddChild(leiste);
        return panel;
    }

    private Control BuildTeamSpalte(string verein, double staerke, bool eigener,
                                    out VBoxContainer spielerBox, out Label staerkeLabel)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(255, 0) };
        var style = new StyleBoxFlat { BgColor = FmTheme.BgToolbar };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(0);
        panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 10, 8);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel(verein, 14, eigener ? FmTheme.Accent : FmTheme.TextPrimary));

        staerkeLabel = FmTheme.MakeLabel(StaerkeHeaderText(staerke, staerke), 12, FmTheme.TextSecondary);
        staerkeLabel.TooltipText = "Elf-Stärke bei Anpfiff gegen die aktuelle Stärke auf dem Platz - "
            + "sinkt mit der Ermüdung, springt bei Wechseln und Platzverweisen.";
        vbox.AddChild(staerkeLabel);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        spielerBox = new VBoxContainer();
        spielerBox.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(spielerBox);

        return panel;
    }

    private static StyleBoxFlat Balken(Color farbe) => new()
    {
        BgColor = farbe,
        CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
    };

    // ── Ablauf ───────────────────────────────────────────────────────────────

    private async void OnTick()
    {
        // Läuft noch eine Anfrage, diesen Tick auslassen - sonst überholen sich die Minuten.
        if (_wartetAufServer || _abgepfiffen) return;

        _wartetAufServer = true;
        var naechste = Math.Min(_spiel.Minute + 1, Spielminuten);
        var zustand = await ApiClient.PostAsync<object, LiveSpiel>(
            $"spiel/live/bis/{naechste}", new { });
        _wartetAufServer = false;

        if (zustand == null)
        {
            _timer.Stop();
            SchreibeZeile(_spiel.Minute, "Verbindung zum Spiel verloren.", FmTheme.Danger);
            _aktionButton.Text = "Schließen";
            _abgepfiffen = true;
            return;
        }

        Aktualisiere(zustand);
        if (zustand.Minute >= Spielminuten) await Abpfiff();
    }

    private void Aktualisiere(LiveSpiel zustand)
    {
        _spiel = zustand;

        _minuteLabel.Text = $"{zustand.Minute}'";
        _uhr.Value = zustand.Minute;
        _ergebnisLabel.Text = $"{zustand.HeimTore} : {zustand.GastTore}";
        _wechselLabel.Text = zustand.WechselHinweis;
        _wechselButton.Disabled = !zustand.DarfWechseln || _abgepfiffen;

        // Anders als früher wird die Elf-Stärke jetzt bei jedem Tick neu gesetzt - vorher stand
        // hier für die ganze Partie unverändert der Wert vom Anpfiff.
        _heimStaerkeLabel.Text = StaerkeHeaderText(zustand.HeimStaerke, zustand.HeimStaerkeAktuell);
        _heimStaerkeLabel.AddThemeColorOverride("font_color",
            StaerkeFarbe(zustand.HeimStaerke, zustand.HeimStaerkeAktuell));
        _gastStaerkeLabel.Text = StaerkeHeaderText(zustand.GastStaerke, zustand.GastStaerkeAktuell);
        _gastStaerkeLabel.AddThemeColorOverride("font_color",
            StaerkeFarbe(zustand.GastStaerke, zustand.GastStaerkeAktuell));

        FuelleSpalte(_heimSpalte, zustand.HeimAufstellung);
        FuelleSpalte(_gastSpalte, zustand.GastAufstellung);

        // Nur die neu hinzugekommenen Ereignisse anhängen.
        for (int i = _gezeigteEreignisse; i < zustand.Ereignisse.Count; i++)
        {
            var e = zustand.Ereignisse[i];
            bool eigenes = e.VereinId == zustand.EigenerVereinId;
            var farbe = e.IstTor ? (eigenes ? FmTheme.Success : FmTheme.Danger) : FmTheme.TextSecondary;
            SchreibeZeile(e.Minute, $"{e.Symbol}  {e.Beschreibung}", farbe);
        }
        _gezeigteEreignisse = zustand.Ereignisse.Count;

        if (!_halbzeitGezeigt && zustand.Minute >= 45)
        {
            _halbzeitGezeigt = true;
            SchreibeZeile(45, $"Halbzeit  –  {zustand.HeimTore} : {zustand.GastTore}",
                FmTheme.TextSecondary);
        }
    }

    private void FuelleSpalte(VBoxContainer box, List<LiveSpieler> spieler)
    {
        foreach (Node kind in box.GetChildren()) kind.QueueFree();

        foreach (var s in spieler)
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 6);

            var slot = FmTheme.MakeLabel(s.Slot, 11, FmTheme.TextSecondary);
            slot.CustomMinimumSize = new Vector2(42, 0);
            zeile.AddChild(slot);

            var name = FmTheme.MakeLabel(s.Name, 11,
                s.AufDemPlatz ? FmTheme.TextPrimary : FmTheme.TextSecondary);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            zeile.AddChild(name);

            // Zeigt die aktuelle, um die Ermüdung verringerte Stärke - nicht den festen
            // Ausgangswert. Ein spürbarer Verlust bekommt zusätzlich einen kleinen Pfeil, damit
            // die Ermüdung nicht erst im Kopfrechnen aus zwei Zahlen auffällt.
            var staerke = FmTheme.MakeLabel(s.EffektiveStaerke.ToString(), 11,
                s.AufDemPlatz ? StaerkeFarbeSpieler(s.Staerke, s.EffektiveStaerke) : FmTheme.TextSecondary,
                HorizontalAlignment.Right);
            staerke.CustomMinimumSize = new Vector2(24, 0);
            staerke.TooltipText = s.AufDemPlatz
                ? s.Erklaerung
                : StaerkeErklaerung.Basis(s.Position, s.Grundstaerke, s.Eingespieltheit, s.Staerke);
            zeile.AddChild(staerke);

            if (s.AufDemPlatz && s.StaerkeVerlust >= 3)
            {
                var verlust = FmTheme.MakeLabel($"▼{s.StaerkeVerlust}", 10, FmTheme.Danger);
                verlust.CustomMinimumSize = new Vector2(22, 0);
                zeile.AddChild(verlust);
            }

            var frische = new ProgressBar
            {
                MinValue = 0, MaxValue = 100,
                Value = s.AufDemPlatz ? s.Kondition : 0,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(46, 8),
            };
            frische.AddThemeStyleboxOverride("fill", Balken(FrischeFarbe(s.Kondition)));
            frische.AddThemeStyleboxOverride("background", Balken(FmTheme.BgDark));
            zeile.AddChild(frische);

            box.AddChild(zeile);
        }
    }

    private static Color FrischeFarbe(double frische) => frische switch
    {
        >= 85 => FmTheme.Success,
        >= 70 => FmTheme.Gold,
        _     => FmTheme.Danger,
    };

    /// <summary>Farbe der Stärke-Zahl eines Spielers, nach Anteil des ermüdungsbedingten Verlusts.</summary>
    private static Color StaerkeFarbeSpieler(int basis, int effektiv)
    {
        if (basis <= 0) return FmTheme.TextPrimary;
        double anteil = (double)effektiv / basis;
        return anteil switch
        {
            >= 0.95 => FmTheme.TextPrimary,
            >= 0.85 => FmTheme.Gold,
            _       => FmTheme.Danger,
        };
    }

    private static string StaerkeHeaderText(double kickoff, double aktuell)
    {
        string de(double w) => w.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));
        double delta = aktuell - kickoff;
        if (Math.Abs(delta) < 0.1) return $"Stärke {de(kickoff)}";
        string vorzeichen = delta > 0 ? "+" : "−";
        return $"Stärke {de(aktuell)}  ({vorzeichen}{de(Math.Abs(delta))})";
    }

    /// <summary>Farbe der Elf-Stärke-Zeile im Kopf, nach Abstand zum Wert bei Anpfiff.</summary>
    private static Color StaerkeFarbe(double kickoff, double aktuell)
    {
        if (kickoff <= 0) return FmTheme.TextSecondary;
        double anteil = aktuell / kickoff;
        return anteil switch
        {
            >= 0.95 => FmTheme.TextSecondary,
            >= 0.85 => FmTheme.Gold,
            _       => FmTheme.Danger,
        };
    }

    // ── Auswechseln ──────────────────────────────────────────────────────────

    private void OnWechselnPressed()
    {
        if (_abgepfiffen) return;

        _timer.Stop(); // Uhr steht, solange gewählt wird
        WechselDialog.Zeige(this, _spiel,
            beiBestaetigung: async paare => await FuehreWechselAus(paare),
            beiAbbruch: () => { if (!_abgepfiffen) _timer.Start(); });
    }

    /// <summary>
    /// Führt alle geplanten Wechsel nacheinander aus. Da die Uhr steht, geschieht das in
    /// derselben Spielminute und zählt damit als ein Wechselfenster.
    /// </summary>
    private async System.Threading.Tasks.Task FuehreWechselAus(List<(long Raus, long Rein)> paare)
    {
        LiveSpiel? zustand = null;
        foreach (var (raus, rein) in paare)
        {
            zustand = await ApiClient.PostAsync<object, LiveSpiel>(
                $"spiel/live/wechsel?raus={raus}&rein={rein}", new { });
            if (zustand == null) break;
        }
        if (zustand != null) Aktualisiere(zustand);
        if (!_abgepfiffen) _timer.Start();
    }

    // ── Abpfiff ──────────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task Abpfiff()
    {
        if (_abgepfiffen) return;
        _abgepfiffen = true;
        _timer.Stop();
        _wechselButton.Disabled = true;

        var zustand = await ApiClient.PostAsync<object, LiveSpiel>("spiel/live/abpfiff", new { });
        if (zustand != null)
        {
            _gezeigteEreignisse = Math.Min(_gezeigteEreignisse, zustand.Ereignisse.Count);
            Aktualisiere(zustand);
        }

        SchreibeZeile(Spielminuten,
            $"Abpfiff  –  Endstand {_spiel.HeimTore} : {_spiel.GastTore}", FmTheme.TextPrimary);
        _uhr.AddThemeStyleboxOverride("fill", Balken(FmTheme.Accent));

        // Der restliche Spieltag läuft im Hintergrund - erst danach sind Tabelle und
        // Statistiken stimmig, deshalb wird bis dahin gewartet.
        _spieltagLaeuft = true;
        _aktionButton.Disabled = true;
        _aktionButton.Text = "Restlicher Spieltag …";
        await WarteAufSpieltag();
        _spieltagLaeuft = false;
        _aktionButton.Disabled = false;
        _aktionButton.Text = "Schließen";
    }

    private async System.Threading.Tasks.Task WarteAufSpieltag()
    {
        for (int i = 0; i < 120; i++)
        {
            var stand = await ApiClient.GetAsync<Fortschritt>("spiel/spieltag/fortschritt");
            if (stand is { Fertig: true }) return;
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        }
    }

    // ── Protokoll ────────────────────────────────────────────────────────────

    private void SchreibeZeile(int minute, string text, Color farbe)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);

        var min = FmTheme.MakeLabel($"{minute}'", 13, FmTheme.TextSecondary, HorizontalAlignment.Right);
        min.CustomMinimumSize = new Vector2(40, 0);
        zeile.AddChild(min);

        var inhalt = FmTheme.MakeLabel(text, 13, farbe);
        inhalt.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        zeile.AddChild(inhalt);

        _log.AddChild(zeile);
        CallDeferred(nameof(ScrolleAnsEnde));
    }

    private void ScrolleAnsEnde()
    {
        var leiste = _logScroll.GetVScrollBar();
        if (leiste != null) _logScroll.ScrollVertical = (int)leiste.MaxValue;
    }

    // ── Steuerung ────────────────────────────────────────────────────────────

    private async void OnAktionPressed()
    {
        if (_spieltagLaeuft) return;

        if (_abgepfiffen)
        {
            Schliessen();
            return;
        }

        // Überspringen: in einem Zug bis zum Ende rechnen lassen.
        _timer.Stop();
        _aktionButton.Disabled = true;
        _wartetAufServer = true;
        var zustand = await ApiClient.PostAsync<object, LiveSpiel>(
            $"spiel/live/bis/{Spielminuten}", new { });
        _wartetAufServer = false;
        if (zustand != null) Aktualisiere(zustand);
        _aktionButton.Disabled = false;
        await Abpfiff();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } && _abgepfiffen && !_spieltagLaeuft)
        {
            Schliessen();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Schliessen()
    {
        _timer?.Stop();
        var beimSchliessen = _beimSchliessen;
        _beimSchliessen = null;
        QueueFree();
        beimSchliessen?.Invoke();
    }
}
