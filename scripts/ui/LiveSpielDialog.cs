#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Spielt eine bereits berechnete Partie Minute für Minute nach: Uhr von 0 bis 90,
/// mitlaufender Spielstand und Ereignisse als Textprotokoll.
///
/// <para>Das Backend rechnet das Spiel in einem Zug durch; hier wird der gespeicherte
/// Minutenverlauf lediglich abgespielt. Dadurch ist die Anzeige jederzeit konsistent mit
/// Tabelle und Statistik.</para>
/// </summary>
public partial class LiveSpielDialog : Control
{
    /// Sekunden pro Spielminute – 90 Minuten dauern damit gut eine Viertelminute.
    private const double SekundenProMinute = 0.18;
    private const int Spielminuten = 90;
    private const int Halbzeit = 45;

    private static LiveSpielDialog? _instanz;

    private SpielBericht _bericht = null!;
    private Action? _beimSchliessen;

    private Label _ergebnisLabel = null!;
    private Label _minuteLabel = null!;
    private ProgressBar _uhr = null!;
    private VBoxContainer _log = null!;
    private ScrollContainer _logScroll = null!;
    private Button _aktionButton = null!;
    private Timer _timer = null!;

    private List<SpielerZeile> _heimZeilen = new();
    private List<SpielerZeile> _gastZeilen = new();

    private int _minute;
    private int _naechstesEreignis;
    private int _heimTore;
    private int _gastTore;
    private bool _abgepfiffen;
    private bool _halbzeitGezeigt;

    public static void Zeige(Node caller, SpielBericht bericht, Action? beimSchliessen = null)
    {
        if (_instanz != null && IsInstanceValid(_instanz))
        {
            _instanz.QueueFree();
        }

        var dialog = new LiveSpielDialog
        {
            _bericht = bericht,
            _beimSchliessen = beimSchliessen,
        };
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
        // Bewusst kein Schliessen per Klick daneben - waehrend des laufenden Spiels waere das
        // zu leicht versehentlich ausgeloest.
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);
        center.AddChild(BuildCard());

        SchreibeZeile(0, "Anpfiff", FmTheme.TextSecondary);

        _timer = new Timer { WaitTime = SekundenProMinute, OneShot = false };
        _timer.Timeout += OnTick;
        AddChild(_timer);
        _timer.Start();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Schliessen();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    /// <summary>Eine Mannschaftsspalte mit Stärke und Spielern samt Frische-Balken.</summary>
    private Control BuildTeamSpalte(List<AufstellungsSpieler> aufstellung, string verein,
                                    double staerke, bool eigener, out List<SpielerZeile> zeilen)
    {
        zeilen = new List<SpielerZeile>();

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(250, 0) };
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

        vbox.AddChild(FmTheme.MakeLabel(verein, 14,
            eigener ? FmTheme.Accent : FmTheme.TextPrimary));
        vbox.AddChild(FmTheme.MakeLabel(
            $"Stärke {staerke.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"))}",
            12, FmTheme.TextSecondary));

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        foreach (var spieler in aufstellung)
        {
            var zeile = new SpielerZeile(spieler);
            zeilen.Add(zeile);
            vbox.AddChild(zeile.Wurzel);
        }

        return panel;
    }

    /// <summary>Eine Zeile in der Mannschaftsspalte; der Frische-Balken läuft mit der Uhr mit.</summary>
    private sealed class SpielerZeile
    {
        public readonly AufstellungsSpieler Spieler;
        public readonly Control Wurzel;
        private readonly ProgressBar _frische;
        private readonly Label _name;

        public SpielerZeile(AufstellungsSpieler spieler)
        {
            Spieler = spieler;

            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 6);

            var slot = FmTheme.MakeLabel(spieler.Slot, 11, FmTheme.TextSecondary);
            slot.CustomMinimumSize = new Vector2(42, 0);
            zeile.AddChild(slot);

            _name = FmTheme.MakeLabel(spieler.Name, 11, FmTheme.TextPrimary);
            _name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            zeile.AddChild(_name);

            var staerke = FmTheme.MakeLabel(spieler.Staerke.ToString(), 11, FmTheme.TextSecondary,
                HorizontalAlignment.Right);
            staerke.CustomMinimumSize = new Vector2(24, 0);
            zeile.AddChild(staerke);

            _frische = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(46, 8),
            };
            zeile.AddChild(_frische);

            Wurzel = zeile;
            Aktualisiere(0);
        }

        public void Aktualisiere(int minute)
        {
            bool aufDemPlatz = Spieler.StehtAufDemPlatz(minute);
            double frische = Spieler.FrischeBei(minute);

            _frische.Value = aufDemPlatz ? frische : 0;
            _frische.AddThemeStyleboxOverride("fill", Balken(FrischeFarbe(frische)));
            _frische.AddThemeStyleboxOverride("background", Balken(FmTheme.BgDark));

            // Wer noch nicht oder nicht mehr spielt, tritt zurück.
            _name.AddThemeColorOverride("font_color",
                aufDemPlatz ? FmTheme.TextPrimary : FmTheme.TextSecondary);
        }

        private static Color FrischeFarbe(double frische) => frische switch
        {
            >= 85 => FmTheme.Success,
            >= 70 => FmTheme.Gold,
            _     => FmTheme.Danger,
        };
    }

    private Control BuildCard()
    {
        // Breit genug für Heim-Spalte, Verlauf und Gast-Spalte nebeneinander.
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(920, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(10));

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 24);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        // Kopfzeile
        var kopf = new HBoxContainer();
        var titel = FmTheme.MakeLabel($"⚽  Spieltag {_bericht.Spieltag}", 14, FmTheme.TextSecondary);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(titel);

        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(32, 32) };
        FmTheme.ApplyButton(close, FmTheme.BgPanel);
        close.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        close.Pressed += Schliessen;
        kopf.AddChild(close);
        root.AddChild(kopf);

        // Spielstand
        var stand = new HBoxContainer();
        stand.AddThemeConstantOverride("separation", 12);

        var heim = FmTheme.MakeLabel(_bericht.HeimVerein ?? "", 17,
            IstEigener(_bericht.HeimVereinId) ? FmTheme.Accent : FmTheme.TextPrimary,
            HorizontalAlignment.Right);
        heim.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stand.AddChild(heim);

        _ergebnisLabel = FmTheme.MakeLabel("0 : 0", 26, FmTheme.TextPrimary, HorizontalAlignment.Center);
        _ergebnisLabel.CustomMinimumSize = new Vector2(110, 0);
        stand.AddChild(_ergebnisLabel);

        var gast = FmTheme.MakeLabel(_bericht.GastVerein ?? "", 17,
            IstEigener(_bericht.GastVereinId) ? FmTheme.Accent : FmTheme.TextPrimary);
        gast.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stand.AddChild(gast);

        root.AddChild(stand);

        // Spieluhr
        var uhrZeile = new HBoxContainer();
        uhrZeile.AddThemeConstantOverride("separation", 10);

        _uhr = new ProgressBar
        {
            MinValue = 0,
            MaxValue = Spielminuten,
            Value = 0,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 12),
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

        // Links Heim, in der Mitte der Verlauf, rechts Gast.
        var mitte = new HBoxContainer();
        mitte.AddThemeConstantOverride("separation", 10);
        mitte.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(mitte);

        mitte.AddChild(BuildTeamSpalte(_bericht.HeimAufstellung, _bericht.HeimVerein ?? "",
            _bericht.HeimStaerke, IstEigener(_bericht.HeimVereinId), out _heimZeilen));

        _logScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(340, 320),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        mitte.AddChild(_logScroll);

        _log = new VBoxContainer();
        _log.AddThemeConstantOverride("separation", 5);
        _log.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _logScroll.AddChild(_log);

        mitte.AddChild(BuildTeamSpalte(_bericht.GastAufstellung, _bericht.GastVerein ?? "",
            _bericht.GastStaerke, IstEigener(_bericht.GastVereinId), out _gastZeilen));

        // Aktionsleiste
        var leiste = new HBoxContainer();
        leiste.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _aktionButton = new Button { Text = "⏩  Überspringen" };
        FmTheme.ApplyButton(_aktionButton, FmTheme.BgPanel);
        _aktionButton.Pressed += OnAktionPressed;
        leiste.AddChild(_aktionButton);

        root.AddChild(leiste);

        return panel;
    }

    private static StyleBoxFlat Balken(Color farbe)
    {
        return new StyleBoxFlat
        {
            BgColor = farbe,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }

    // ── Ablauf ───────────────────────────────────────────────────────────────

    private void OnTick()
    {
        _minute++;
        SpuleBisMinute(_minute);

        if (_minute >= Spielminuten)
        {
            Abpfiff();
        }
    }

    private void SpuleBisMinute(int minute)
    {
        _minute = Math.Min(minute, Spielminuten);
        _minuteLabel.Text = $"{_minute}'";
        _uhr.Value = _minute;

        // Frische beider Mannschaften mitlaufen lassen.
        foreach (var zeile in _heimZeilen) zeile.Aktualisiere(_minute);
        foreach (var zeile in _gastZeilen) zeile.Aktualisiere(_minute);

        while (_naechstesEreignis < _bericht.Ereignisse.Count
               && _bericht.Ereignisse[_naechstesEreignis].Minute <= _minute)
        {
            ZeigeEreignis(_bericht.Ereignisse[_naechstesEreignis]);
            _naechstesEreignis++;
        }

        if (!_halbzeitGezeigt && _minute >= Halbzeit)
        {
            _halbzeitGezeigt = true;
            SchreibeZeile(Halbzeit, $"Halbzeit  –  {_heimTore} : {_gastTore}", FmTheme.TextSecondary);
        }
    }

    private void ZeigeEreignis(SpielEreignis ereignis)
    {
        if (ereignis.IstTor)
        {
            if (ereignis.VereinId == _bericht.HeimVereinId) _heimTore++;
            else _gastTore++;
            AktualisiereErgebnis();
        }

        var farbe = ereignis.IstTor
            ? (IstEigener(ereignis.VereinId) ? FmTheme.Success : FmTheme.Danger)
            : FmTheme.TextSecondary;

        var verein = ereignis.VereinId == _bericht.HeimVereinId
            ? _bericht.HeimVerein
            : _bericht.GastVerein;

        SchreibeZeile(ereignis.Minute,
            $"{ereignis.Symbol}  {ereignis.Beschreibung}   ({verein})",
            farbe);
    }

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
        if (leiste != null)
        {
            _logScroll.ScrollVertical = (int)leiste.MaxValue;
        }
    }

    private void AktualisiereErgebnis()
    {
        _ergebnisLabel.Text = $"{_heimTore} : {_gastTore}";
    }

    private void Abpfiff()
    {
        if (_abgepfiffen) return;
        _abgepfiffen = true;

        _timer.Stop();
        SpuleBisMinute(Spielminuten);
        SchreibeZeile(Spielminuten, $"Abpfiff  –  Endstand {_heimTore} : {_gastTore}", FmTheme.TextPrimary);

        _uhr.AddThemeStyleboxOverride("fill", Balken(FmTheme.Accent));
        _aktionButton.Text = "Schließen";
    }

    private void OnAktionPressed()
    {
        if (_abgepfiffen)
        {
            Schliessen();
        }
        else
        {
            Abpfiff();
        }
    }

    private void Schliessen()
    {
        _timer?.Stop();
        _beimSchliessen?.Invoke();
        _beimSchliessen = null;
        QueueFree();
    }

    private static bool IstEigener(long? vereinId)
    {
        return vereinId.HasValue && vereinId.Value == GameState.Instance.VereinId;
    }
}
