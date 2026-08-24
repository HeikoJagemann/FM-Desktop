#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Auswechslungen zusammenstellen: links wer geht, rechts wer kommt. Mehrere Paare lassen sich
/// sammeln und gemeinsam ausführen – das zählt nach FIFA-Regel als ein Wechselfenster.
/// Die Spieluhr steht währenddessen still.
/// </summary>
public partial class WechselDialog : Control
{
    private LiveSpiel _spiel = null!;
    private Action<List<(long Raus, long Rein)>>? _beiBestaetigung;
    private Action? _beiAbbruch;

    private long? _gewaehltRaus;
    private long? _gewaehltRein;

    private readonly List<(long Raus, long Rein)> _plan = new();
    private readonly Dictionary<long, LiveSpieler> _nachId = new();

    private VBoxContainer _platzListe = null!;
    private VBoxContainer _bankListe = null!;
    private VBoxContainer _planListe = null!;
    private Label _planTitel = null!;
    private Button _bestaetigen = null!;
    private Button _rueckgaengig = null!;

    public static void Zeige(Node caller, LiveSpiel spiel,
                             Action<List<(long Raus, long Rein)>> beiBestaetigung,
                             Action? beiAbbruch = null)
    {
        var dialog = new WechselDialog
        {
            _spiel = spiel,
            _beiBestaetigung = beiBestaetigung,
            _beiAbbruch = beiAbbruch,
        };
        var scene = caller.GetTree().CurrentScene ?? caller.GetTree().Root;
        scene.AddChild(dialog);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        foreach (var s in _spiel.EigeneAufDemPlatz) _nachId[s.SpielerId] = s;
        foreach (var s in _spiel.EigeneBank)        _nachId[s.SpielerId] = s;

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.72f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);
        center.AddChild(BuildCard());

        ZeichneListen();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Abbrechen();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    private Control BuildCard()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(860, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(12));

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 28, 24);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 16);
        margin.AddChild(root);

        root.AddChild(BuildKopf());

        var trenner = new HSeparator();
        trenner.AddThemeColorOverride("color", FmTheme.Border);
        root.AddChild(trenner);

        // Zwei Spalten mit Pfeil dazwischen
        var spalten = new HBoxContainer();
        spalten.AddThemeConstantOverride("separation", 14);
        root.AddChild(spalten);

        spalten.AddChild(BuildSpalte("AUF DEM PLATZ", out _platzListe));

        var pfeil = new CenterContainer { CustomMinimumSize = new Vector2(34, 0) };
        pfeil.AddChild(FmTheme.MakeLabel("→", 22, FmTheme.TextSecondary));
        spalten.AddChild(pfeil);

        spalten.AddChild(BuildSpalte("ERSATZBANK", out _bankListe));

        root.AddChild(BuildPlan());
        root.AddChild(BuildLeiste());

        return panel;
    }

    private Control BuildKopf()
    {
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 12);

        var links = new VBoxContainer();
        links.AddThemeConstantOverride("separation", 2);
        links.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        links.AddChild(FmTheme.MakeLabel("Auswechslung", 20, FmTheme.TextPrimary));
        links.AddChild(FmTheme.MakeLabel(
            "Erst wer geht, dann wer kommt.  ◉ markiert  ·  ↓↑ vorgemerkt  ·  "
            + "mehrere Wechsel zählen als ein Fenster.",
            12, FmTheme.TextSecondary));
        kopf.AddChild(links);

        kopf.AddChild(Chip($"{_spiel.Minute}. Minute", FmTheme.TextPrimary));
        kopf.AddChild(Chip($"{_spiel.WechselUebrig} Wechsel",
            _spiel.WechselUebrig > 0 ? FmTheme.Success : FmTheme.Danger));
        kopf.AddChild(Chip($"{_spiel.FensterUebrig} Fenster",
            _spiel.FensterUebrig > 0 ? FmTheme.Success : FmTheme.Danger));

        return kopf;
    }

    private static Control Chip(string text, Color farbe)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = FmTheme.BgToolbar,
            BorderColor = FmTheme.Border,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
        };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(0);
        panel.AddThemeStyleboxOverride("panel", style);

        var m = new MarginContainer();
        FmTheme.SetMargin(m, 12, 5);
        m.AddChild(FmTheme.MakeLabel(text, 12, farbe));
        panel.AddChild(m);
        return panel;
    }

    private Control BuildSpalte(string titel, out VBoxContainer liste)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        vbox.AddChild(FmTheme.MakeLabel(titel, 11, FmTheme.TextSecondary));

        var rahmen = new PanelContainer();
        var style = new StyleBoxFlat { BgColor = FmTheme.BgDark, BorderColor = FmTheme.Border };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(0);
        rahmen.AddThemeStyleboxOverride("panel", style);
        vbox.AddChild(rahmen);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(330, 280) };
        rahmen.AddChild(scroll);

        liste = new VBoxContainer();
        liste.AddThemeConstantOverride("separation", 2);
        liste.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(liste);

        return vbox;
    }

    private Control BuildPlan()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 5);

        _planTitel = FmTheme.MakeLabel("GEPLANTE WECHSEL", 11, FmTheme.TextSecondary);
        vbox.AddChild(_planTitel);

        _planListe = new VBoxContainer();
        _planListe.AddThemeConstantOverride("separation", 3);
        _planListe.CustomMinimumSize = new Vector2(0, 62);
        vbox.AddChild(_planListe);

        return vbox;
    }

    private Control BuildLeiste()
    {
        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 8);

        _rueckgaengig = new Button { Text = "↩  Rückgängig", CustomMinimumSize = new Vector2(160, 40) };
        FmTheme.ApplyButton(_rueckgaengig, FmTheme.BgPanel);
        _rueckgaengig.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        _rueckgaengig.TooltipText = "Nimmt die letzte Auswahl bzw. den zuletzt geplanten Wechsel zurück.";
        _rueckgaengig.Pressed += Rueckgaengig;
        leiste.AddChild(_rueckgaengig);

        leiste.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var abbrechen = new Button { Text = "Abbrechen", CustomMinimumSize = new Vector2(140, 40) };
        FmTheme.ApplyButton(abbrechen, FmTheme.BgPanel);
        abbrechen.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        abbrechen.Pressed += Abbrechen;
        leiste.AddChild(abbrechen);

        _bestaetigen = new Button { CustomMinimumSize = new Vector2(220, 40), Disabled = true };
        FmTheme.ApplyButton(_bestaetigen, FmTheme.Accent);
        _bestaetigen.Pressed += Bestaetigen;
        leiste.AddChild(_bestaetigen);

        return leiste;
    }

    // ── Listen ───────────────────────────────────────────────────────────────

    private void ZeichneListen()
    {
        var vergeben = _plan.SelectMany(p => new[] { p.Raus, p.Rein }).ToHashSet();

        Fuelle(_platzListe, _spiel.EigeneAufDemPlatz, vergeben, true);
        Fuelle(_bankListe, _spiel.EigeneBank, vergeben, false);
        ZeichnePlan();

        int offen = _spiel.WechselUebrig - _plan.Count;
        _bestaetigen.Text = _plan.Count switch
        {
            0 => "Wechsel durchführen",
            1 => "1 Wechsel durchführen",
            _ => $"{_plan.Count} Wechsel durchführen",
        };
        _bestaetigen.Disabled = _plan.Count == 0;
        _rueckgaengig.Disabled = _plan.Count == 0 && _gewaehltRaus == null && _gewaehltRein == null;
        _planTitel.Text = _plan.Count == 0
            ? "GEPLANTE WECHSEL – noch keiner ausgewählt"
            : $"GEPLANTE WECHSEL – {_plan.Count} von {_spiel.WechselUebrig} möglichen"
              + (offen == 0 ? " (Maximum erreicht)" : "");
    }

    /// <summary>Wie eine Zeile gerade dasteht.</summary>
    private enum Zeilenzustand { Frei, Markiert, Verplant }

    private void Fuelle(VBoxContainer liste, List<LiveSpieler> spieler,
                        HashSet<long> vergeben, bool aufDemPlatz)
    {
        foreach (Node kind in liste.GetChildren()) kind.QueueFree();

        if (spieler.Count == 0)
        {
            liste.AddChild(FmTheme.MakeLabel(
                aufDemPlatz ? "  Kein Spieler verfügbar" : "  Bank leer", 12, FmTheme.TextSecondary));
            return;
        }

        bool nochPlatz = _plan.Count < _spiel.WechselUebrig;

        foreach (var s in spieler)
        {
            bool markiert = aufDemPlatz ? _gewaehltRaus == s.SpielerId : _gewaehltRein == s.SpielerId;
            var zustand = vergeben.Contains(s.SpielerId) ? Zeilenzustand.Verplant
                        : markiert                       ? Zeilenzustand.Markiert
                        : Zeilenzustand.Frei;

            // Verplante Spieler bleiben sichtbar, sind aber nicht mehr anklickbar.
            bool aktiv = zustand != Zeilenzustand.Verplant
                         && (nochPlatz || zustand == Zeilenzustand.Markiert);

            liste.AddChild(BaueZeile(s, aufDemPlatz, zustand, aktiv));
        }
    }

    private Control BaueZeile(LiveSpieler s, bool aufDemPlatz, Zeilenzustand zustand, bool aktiv)
    {
        var gruppe = PositionsgruppeHelfer.Von(s.Position);
        bool markiert = zustand == Zeilenzustand.Markiert;
        bool verplant = zustand == Zeilenzustand.Verplant;

        var btn = new Button
        {
            CustomMinimumSize = new Vector2(0, 38),
            Disabled = !aktiv,
            Flat = true,
        };

        var grundfarbe = FmTheme.FuerGruppe(gruppe);
        var style = new StyleBoxFlat
        {
            BgColor = markiert ? grundfarbe.Lightened(0.22f)
                    : verplant ? grundfarbe.Darkened(0.35f)
                    : grundfarbe,
            BorderColor = markiert ? FmTheme.Accent
                        : verplant ? (aufDemPlatz ? FmTheme.Danger : FmTheme.Success)
                        : FmTheme.Border,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        style.SetBorderWidthAll(markiert || verplant ? 2 : 1);
        style.SetContentMarginAll(0);
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", MitRand(style, FmTheme.AccentHover));
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeStyleboxOverride("focus", style);
        btn.AddThemeStyleboxOverride("disabled", style);

        var id = s.SpielerId;
        btn.Pressed += () => { if (aufDemPlatz) WaehleRaus(id); else WaehleRein(id); };

        // Inhalt als eigene Zeile über dem Button, damit Spalten sauber ausgerichtet sind.
        var inhalt = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        inhalt.AddThemeConstantOverride("separation", 8);
        inhalt.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var rand = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        FmTheme.SetMargin(rand, 10, 0);
        rand.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rand.AddChild(inhalt);
        btn.AddChild(rand);

        // Markierungsspalte ganz links: leer, Auswahlpunkt oder Wechselpfeil.
        var marke = FmTheme.MakeLabel(
            markiert ? "◉" : verplant ? (aufDemPlatz ? "↓" : "↑") : "",
            13,
            markiert ? FmTheme.Accent : aufDemPlatz ? FmTheme.Danger : FmTheme.Success,
            HorizontalAlignment.Center);
        marke.CustomMinimumSize = new Vector2(16, 0);
        marke.VerticalAlignment = VerticalAlignment.Center;
        inhalt.AddChild(marke);

        var pos = FmTheme.MakeLabel(aufDemPlatz ? s.Slot : s.Position, 11,
            FmTheme.TextFuerGruppe(gruppe));
        pos.CustomMinimumSize = new Vector2(44, 0);
        pos.VerticalAlignment = VerticalAlignment.Center;
        inhalt.AddChild(pos);

        var name = FmTheme.MakeLabel(s.Name, 12,
            verplant ? FmTheme.TextSecondary : FmTheme.TextPrimary);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        inhalt.AddChild(name);

        var staerke = FmTheme.MakeLabel(s.Staerke.ToString(), 12, FmTheme.TextSecondary,
            HorizontalAlignment.Right);
        staerke.CustomMinimumSize = new Vector2(26, 0);
        staerke.VerticalAlignment = VerticalAlignment.Center;
        inhalt.AddChild(staerke);

        if (aufDemPlatz)
        {
            var frische = new ProgressBar
            {
                MinValue = 0, MaxValue = 100, Value = s.Kondition,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(52, 8),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            frische.AddThemeStyleboxOverride("fill", Balken(FrischeFarbe(s.Kondition)));
            frische.AddThemeStyleboxOverride("background", Balken(FmTheme.BgDark));
            inhalt.AddChild(frische);
        }
        else
        {
            var platzhalter = new Control
            {
                CustomMinimumSize = new Vector2(52, 0),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            inhalt.AddChild(platzhalter);
        }

        return btn;
    }

    private void ZeichnePlan()
    {
        foreach (Node kind in _planListe.GetChildren()) kind.QueueFree();

        if (_plan.Count == 0)
        {
            _planListe.AddChild(FmTheme.MakeLabel(
                "  Wähle links einen Spieler und rechts seinen Ersatz.", 12, FmTheme.TextSecondary));
            return;
        }

        foreach (var (raus, rein) in _plan.ToList())
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 8);

            zeile.AddChild(FmTheme.MakeLabel("↓", 13, FmTheme.Danger));
            var rausLabel = FmTheme.MakeLabel(NameVon(raus), 12, FmTheme.TextSecondary);
            rausLabel.CustomMinimumSize = new Vector2(210, 0);
            zeile.AddChild(rausLabel);

            zeile.AddChild(FmTheme.MakeLabel("↑", 13, FmTheme.Success));
            var reinLabel = FmTheme.MakeLabel(NameVon(rein), 12, FmTheme.TextPrimary);
            reinLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            zeile.AddChild(reinLabel);

            var entfernen = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 26) };
            FmTheme.ApplyButton(entfernen, FmTheme.BgPanel);
            entfernen.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
            var paar = (raus, rein);
            entfernen.Pressed += () => { _plan.Remove(paar); ZeichneListen(); };
            zeile.AddChild(entfernen);

            _planListe.AddChild(zeile);
        }
    }

    private string NameVon(long id) => _nachId.TryGetValue(id, out var s) ? s.Name : $"#{id}";

    // ── Auswahl ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Nimmt zuerst eine noch offene Auswahl zurück, sonst den zuletzt geplanten Wechsel.
    /// </summary>
    private void Rueckgaengig()
    {
        if (_gewaehltRein != null)      _gewaehltRein = null;
        else if (_gewaehltRaus != null) _gewaehltRaus = null;
        else if (_plan.Count > 0)       _plan.RemoveAt(_plan.Count - 1);
        ZeichneListen();
    }

    private void WaehleRaus(long id)
    {
        _gewaehltRaus = _gewaehltRaus == id ? null : id;
        PruefePaar();
    }

    private void WaehleRein(long id)
    {
        _gewaehltRein = _gewaehltRein == id ? null : id;
        PruefePaar();
    }

    /// <summary>Sobald beide Seiten gewählt sind, wandert das Paar in den Plan.</summary>
    private void PruefePaar()
    {
        if (_gewaehltRaus != null && _gewaehltRein != null
            && _plan.Count < _spiel.WechselUebrig)
        {
            _plan.Add((_gewaehltRaus.Value, _gewaehltRein.Value));
            _gewaehltRaus = null;
            _gewaehltRein = null;
        }
        ZeichneListen();
    }

    // ── Abschluss ────────────────────────────────────────────────────────────

    private void Bestaetigen()
    {
        if (_plan.Count == 0) return;
        var beiBestaetigung = _beiBestaetigung;
        var paare = new List<(long, long)>(_plan);
        _beiBestaetigung = null;
        _beiAbbruch = null;
        QueueFree();
        beiBestaetigung?.Invoke(paare);
    }

    private void Abbrechen()
    {
        var beiAbbruch = _beiAbbruch;
        _beiBestaetigung = null;
        _beiAbbruch = null;
        QueueFree();
        beiAbbruch?.Invoke();
    }

    // ── Stil ─────────────────────────────────────────────────────────────────

    private static StyleBoxFlat MitRand(StyleBoxFlat vorlage, Color randfarbe)
    {
        var s = (StyleBoxFlat)vorlage.Duplicate();
        s.BorderColor = randfarbe;
        return s;
    }

    private static StyleBoxFlat Balken(Color farbe) => new()
    {
        BgColor = farbe,
        CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
    };

    private static Color FrischeFarbe(double frische) => frische switch
    {
        >= 85 => FmTheme.Success,
        >= 70 => FmTheme.Gold,
        _     => FmTheme.Danger,
    };
}
