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
///
/// <para>Ein Klick neben die Karte schließt hier nicht: Er würde die Zusammenstellung
/// verwerfen. Abbrechen, das Kreuz oder Escape tun das bewusst.</para>
/// </summary>
public partial class WechselDialog : Overlaykarte
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

    public WechselDialog() : base("Auswechslung", 880)
    {
        SchliesstBeiKlickDaneben = false;
    }

    public static void Zeige(Node caller, LiveSpiel spiel,
                             Action<List<(long Raus, long Rein)>> beiBestaetigung,
                             Action? beiAbbruch = null)
    {
        Oeffne(caller, new WechselDialog
        {
            _spiel = spiel,
            _beiBestaetigung = beiBestaetigung,
            _beiAbbruch = beiAbbruch,
        });
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        SetzeUntertitel("Erst wer geht, dann wer kommt. Mehrere Wechsel auf einmal zählen als ein Fenster.");

        foreach (var s in _spiel.EigeneAufDemPlatz) _nachId[s.SpielerId] = s;
        foreach (var s in _spiel.EigeneBank)        _nachId[s.SpielerId] = s;

        inhalt.AddChild(BaueLage());

        // Zwei Spalten mit dem Wechselzeichen dazwischen
        var spalten = new HBoxContainer();
        spalten.AddThemeConstantOverride("separation", 14);
        inhalt.AddChild(spalten);

        spalten.AddChild(BaueSpalte("Auf dem Platz", out _platzListe));
        var mitte = new CenterContainer { CustomMinimumSize = new Vector2(30, 0) };
        mitte.AddChild(Symbol.Neu(Symbol.Art.Wechsel, FmTheme.TextSecondary, 26));
        spalten.AddChild(mitte);
        spalten.AddChild(BaueSpalte("Ersatzbank", out _bankListe));

        inhalt.AddChild(BauePlan());
        inhalt.AddChild(BaueLeiste());

        ZeichneListen();
    }

    protected override void BeimSchliessen() => _beiAbbruch?.Invoke();

    // ── Aufbau ───────────────────────────────────────────────────────────────

    /// <summary>Minute, verbliebene Wechsel und Fenster als Schilder.</summary>
    private Control BaueLage()
    {
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 8);
        reihe.AddChild(Schild($"{_spiel.Minute}. Minute", FmTheme.TextPrimary));
        reihe.AddChild(Schild(_spiel.WechselUebrig == 1 ? "1 Wechsel übrig" : $"{_spiel.WechselUebrig} Wechsel übrig",
            _spiel.WechselUebrig > 0 ? FmTheme.Success : FmTheme.Danger));
        reihe.AddChild(Schild(_spiel.FensterUebrig == 1 ? "1 Fenster übrig" : $"{_spiel.FensterUebrig} Fenster übrig",
            _spiel.FensterUebrig > 0 ? FmTheme.Success : FmTheme.Danger));
        return reihe;
    }

    private static Control Schild(string text, Color farbe)
    {
        var panel = new PanelContainer();
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 14, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 12;
        stil.ContentMarginTop = stil.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", stil);
        var label = FmTheme.MakeLabel(text, 12, farbe);
        label.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        panel.AddChild(label);
        return panel;
    }

    private static Control BaueSpalte(string titel, out VBoxContainer liste)
    {
        var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(FmTheme.MakeAbschnitt(titel, 17));

        var rahmen = new PanelContainer();
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.SetContentMarginAll(6);
        rahmen.AddThemeStyleboxOverride("panel", stil);
        vbox.AddChild(rahmen);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 290),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        rahmen.AddChild(scroll);

        liste = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        liste.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(liste);

        return vbox;
    }

    private Control BauePlan()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);

        _planTitel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        vbox.AddChild(_planTitel);

        _planListe = new VBoxContainer { CustomMinimumSize = new Vector2(0, 62) };
        _planListe.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_planListe);

        return vbox;
    }

    private Control BaueLeiste()
    {
        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 10);

        _rueckgaengig = new Button { Text = "Rückgängig", CustomMinimumSize = new Vector2(130, 40) };
        FmTheme.ApplySecondaryButton(_rueckgaengig);
        _rueckgaengig.TooltipText = "Nimmt die letzte Auswahl bzw. den zuletzt geplanten Wechsel zurück.";
        _rueckgaengig.Pressed += Rueckgaengig;
        leiste.AddChild(_rueckgaengig);

        leiste.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var abbrechen = new Button { Text = "Abbrechen", CustomMinimumSize = new Vector2(130, 40) };
        FmTheme.ApplySecondaryButton(abbrechen);
        abbrechen.Pressed += Schliessen;
        leiste.AddChild(abbrechen);

        _bestaetigen = new Button { CustomMinimumSize = new Vector2(220, 40), Disabled = true };
        FmTheme.ApplyPrimaryButton(_bestaetigen);
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
            ? "GEPLANTE WECHSEL  ·  noch keiner"
            : $"GEPLANTE WECHSEL  ·  {_plan.Count} von {_spiel.WechselUebrig} möglichen"
              + (offen == 0 ? ", mehr geht nicht" : "");
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
                aufDemPlatz ? "Kein Spieler verfügbar" : "Die Bank ist leer", 12, FmTheme.TextSecondary));
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
            CustomMinimumSize = new Vector2(0, 40),
            Disabled = !aktiv,
            FocusMode = FocusModeEnum.None,
        };

        var grund = markiert ? FmTheme.AuswahlZeile
                  : verplant ? FmTheme.BgDark
                  : FmTheme.FuerGruppe(gruppe);
        var rand = markiert ? FmTheme.Accent
                 : verplant ? (aufDemPlatz ? FmTheme.Danger : FmTheme.Success).Darkened(0.3f)
                 : FmTheme.Border;
        var stil = FmTheme.Flaeche(grund, 8, rand, markiert ? 2 : 1);
        btn.AddThemeStyleboxOverride("normal", stil);
        btn.AddThemeStyleboxOverride("hover", FmTheme.Flaeche(grund.Lightened(0.05f), 8,
            markiert ? FmTheme.Accent : FmTheme.TextGedimmt, markiert ? 2 : 1));
        btn.AddThemeStyleboxOverride("pressed", stil);
        btn.AddThemeStyleboxOverride("focus", stil);
        btn.AddThemeStyleboxOverride("disabled", stil);

        var id = s.SpielerId;
        btn.Pressed += () => { if (aufDemPlatz) WaehleRaus(id); else WaehleRein(id); };

        // Inhalt als eigene Zeile über dem Button, damit Spalten sauber ausgerichtet sind.
        var inhalt = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        inhalt.AddThemeConstantOverride("separation", 10);
        var abstand = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        FmTheme.SetMargin(abstand, 10, 0);
        abstand.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        abstand.AddChild(inhalt);
        btn.AddChild(abstand);

        // Markierung ganz links: Auswahlpunkt oder Pfeil, wohin der Spieler geht.
        inhalt.AddChild(markiert ? Symbol.Neu(Symbol.Art.Punkt, FmTheme.Accent, 16)
                      : verplant ? Symbol.Neu(aufDemPlatz ? Symbol.Art.Raus : Symbol.Art.Rein,
                                              aufDemPlatz ? FmTheme.Danger : FmTheme.Success, 16)
                      : new Control { CustomMinimumSize = new Vector2(16, 16), MouseFilter = MouseFilterEnum.Ignore });

        inhalt.AddChild(Positionschip(aufDemPlatz ? s.Slot : s.Position, gruppe));

        var name = FmTheme.MakeLabel(s.Name, 13, verplant ? FmTheme.TextGedimmt : FmTheme.TextPrimary);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.ClipText = true;
        inhalt.AddChild(name);

        var staerke = FmTheme.MakeZahl(s.Staerke.ToString(), 18,
            verplant ? FmTheme.TextGedimmt : FmTheme.TextPrimary);
        staerke.HorizontalAlignment = HorizontalAlignment.Right;
        staerke.VerticalAlignment = VerticalAlignment.Center;
        staerke.CustomMinimumSize = new Vector2(26, 0);
        staerke.MouseFilter = MouseFilterEnum.Pass;
        staerke.TooltipText = aufDemPlatz
            ? s.Erklaerung
            : StaerkeErklaerung.Basis(s.Position, s.Grundstaerke, s.Eingespieltheit, s.EffektiveStaerke, s.FormFaktor);
        inhalt.AddChild(staerke);

        // Auf dem Platz die Kondition - nach ihr wird meist gewechselt. Auf der Bank ein
        // gleich breiter Platzhalter, damit die Stärken beider Spalten auf einer Linie stehen.
        if (aufDemPlatz)
        {
            var frische = new ProgressBar
            {
                MinValue = 0, MaxValue = 100, Value = s.Kondition,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(52, 6),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Pass,
                TooltipText = $"Kondition {s.Kondition:0}",
            };
            frische.AddThemeStyleboxOverride("fill", FmTheme.KonditionsBalken(FmTheme.FrischeFarbe(s.Kondition)));
            frische.AddThemeStyleboxOverride("background", FmTheme.KonditionsBalken(FmTheme.BgHover));
            inhalt.AddChild(frische);
        }
        else
        {
            inhalt.AddChild(new Control { CustomMinimumSize = new Vector2(52, 0), MouseFilter = MouseFilterEnum.Ignore });
        }

        return btn;
    }

    /// <summary>Position oder Slot als Chip in der Farbe des Mannschaftsteils.</summary>
    private static Control Positionschip(string text, Positionsgruppe gruppe)
    {
        var chip = FmTheme.MakeLabel(text, 11, FmTheme.TextFuerGruppe(gruppe), HorizontalAlignment.Center);
        chip.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
        chip.AddThemeStyleboxOverride("normal", FmTheme.Flaeche(FmTheme.GruppeChip(gruppe), 5));
        chip.CustomMinimumSize = new Vector2(44, 20);
        chip.VerticalAlignment = VerticalAlignment.Center;
        chip.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        chip.MouseFilter = MouseFilterEnum.Ignore;
        return chip;
    }

    private void ZeichnePlan()
    {
        foreach (Node kind in _planListe.GetChildren()) kind.QueueFree();

        if (_plan.Count == 0)
        {
            _planListe.AddChild(FmTheme.MakeLabel(
                "Wähle links einen Spieler und rechts seinen Ersatz.", 13, FmTheme.TextSecondary));
            return;
        }

        foreach (var (raus, rein) in _plan.ToList())
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 8);

            zeile.AddChild(Symbol.Neu(Symbol.Art.Raus, FmTheme.Danger, 16));
            var rausLabel = FmTheme.MakeLabel(NameVon(raus), 13, FmTheme.TextSecondary);
            rausLabel.CustomMinimumSize = new Vector2(230, 0);
            zeile.AddChild(rausLabel);

            zeile.AddChild(Symbol.Neu(Symbol.Art.Rein, FmTheme.Success, 16));
            var reinLabel = FmTheme.MakeLabel(NameVon(rein), 13, FmTheme.TextPrimary);
            reinLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            zeile.AddChild(reinLabel);

            var entfernen = new Button
            {
                CustomMinimumSize = new Vector2(28, 28),
                FocusMode = FocusModeEnum.None,
                TooltipText = "Diesen Wechsel streichen",
            };
            entfernen.AddThemeStyleboxOverride("normal", FmTheme.Flaeche(new Color(0, 0, 0, 0), 6));
            entfernen.AddThemeStyleboxOverride("hover", FmTheme.Flaeche(FmTheme.BgHover, 6));
            entfernen.AddThemeStyleboxOverride("pressed", FmTheme.Flaeche(FmTheme.Border, 6));
            var kreuz = Symbol.Neu(Symbol.Art.Kreuz, FmTheme.TextSecondary, 14);
            kreuz.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            entfernen.AddChild(kreuz);
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
        // Kein Abbruch - Schließen meldet sonst, es sei nichts gewechselt worden.
        _beiAbbruch = null;
        Schliessen();
        beiBestaetigung?.Invoke(paare);
    }
}
