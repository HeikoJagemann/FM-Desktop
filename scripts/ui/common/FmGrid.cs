#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FMDesktop.UI.Common;

/// <summary>
/// Die eine Tabelle für alle Ansichten: Kopfzeile, Zeilen, Zebrastreifen, Zellfarben,
/// Mouseover je Zelle, Rechtsklick, Sortierung per Kopfklick.
///
/// <para>Baut bewusst auf eigenen Zeilen-Controls statt auf Godots <c>Tree</c> - der kann kein
/// Drag &amp; Drop, das die Aufstellung braucht. Vorher gab es deshalb zwei Tabellenwelten
/// nebeneinander und alles doppelt.</para>
/// </summary>
public partial class FmGrid<T> : VBoxContainer
{
    private const int ZeilenHoehe = 30;

    private readonly List<GridSpalte<T>> _spalten;
    private readonly List<GridZeile<T>>  _zeilen = new();
    private readonly HashSet<string>     _zugeklappt = new();

    private HBoxContainer _kopf      = null!;
    private VBoxContainer _zeilenBox = null!;
    private List<T>       _daten     = new();

    private VScrollBar? _scrollLeiste;
    private Control?    _scrollAusgleich;

    private GridSpalte<T>? _sortSpalte;
    private bool           _sortAbsteigend;

    /// <summary>Hintergrund einer Zeile; das zweite Argument wechselt für den Zebrastreifen.</summary>
    public Func<T, bool, Color>? Zeilenfarbe { get; set; }

    /// <summary>Fasst die Zeilen unter klappbaren Überschriften zusammen.</summary>
    public Func<T, string>? Gruppieren { get; set; }

    /// <summary>Beim Wechsel dieses Werts beginnt der Zebrastreifen von vorn.</summary>
    public Func<T, object>? ZebraNeuBei { get; set; }

    /// <summary>Reihenfolge, solange der Nutzer keinen Spaltenkopf angeklickt hat.</summary>
    public Comparison<T>? Standardsortierung { get; set; }

    /// <summary>Macht Zeilen ziehbar; liefert die Nutzlast für den Drop.</summary>
    public Func<T, Godot.Collections.Dictionary>? ZiehDaten { get; set; }

    /// <summary>Ein zusätzliches Control ganz links in der Zeile, etwa eine Markierung.</summary>
    public Func<T, Control>? ZeilenZusatz { get; set; }

    public event Action<T>? Rechtsklick;
    public event Action<T>? Doppelklick;

    /// <summary>Einfacher Linksklick auf eine Zeile - die Zeile wird dabei hervorgehoben.</summary>
    public event Action<T>? Ausgewaehlt;

    private GridZeile<T>? _gewaehlteZeile;
    private Color _farbeVorAuswahl;

    public FmGrid(IEnumerable<GridSpalte<T>> spalten)
    {
        _spalten = spalten.ToList();
        AddThemeConstantOverride("separation", 0);
        BaueGeruest();
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    private void BaueGeruest()
    {
        var kopfPanel = new PanelContainer();
        var kopfStil = new StyleBoxFlat { BgColor = FmTheme.BgToolbar };
        kopfStil.SetContentMarginAll(0);
        kopfPanel.AddThemeStyleboxOverride("panel", kopfStil);
        AddChild(kopfPanel);

        _kopf = new HBoxContainer();
        _kopf.AddThemeConstantOverride("separation", 0);
        kopfPanel.AddChild(_kopf);

        var trenner = new HSeparator();
        trenner.AddThemeColorOverride("color", FmTheme.Border);
        AddChild(trenner);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical   = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        _zeilenBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _zeilenBox.AddThemeConstantOverride("separation", 1);
        scroll.AddChild(_zeilenBox);

        // Sobald die Liste scrollt, wird der Zeilenbereich schmaler als die Kopfzeile. Ohne
        // Ausgleich stünden Kopf und Spalten um die Breite der Scrollleiste versetzt.
        _scrollLeiste = scroll.GetVScrollBar();
        _scrollLeiste.VisibilityChanged += AktualisiereScrollAusgleich;

        BaueKopf();
    }

    private void AktualisiereScrollAusgleich()
    {
        if (_scrollAusgleich == null || _scrollLeiste == null) return;
        float breite = _scrollLeiste.Visible ? _scrollLeiste.Size.X : 0;
        _scrollAusgleich.CustomMinimumSize = new Vector2(breite, 0);
    }

    private void BaueKopf()
    {
        foreach (var kind in _kopf.GetChildren()) kind.QueueFree();

        // Platzhalter über der Zusatzspalte, damit die Spalten darunter bündig bleiben.
        if (ZeilenZusatz != null)
        {
            _kopf.AddChild(new Control { CustomMinimumSize = new Vector2(ZusatzBreite, 0) });
        }

        foreach (var spalte in _spalten)
        {
            var rand = new MarginContainer();
            FmTheme.SetMargin(rand, 6, 3);
            // Feste Mindesthöhe: Ohne sie fällt die Kopfzeile in sich zusammen.
            rand.CustomMinimumSize = new Vector2(spalte.Breite, KopfHoehe);
            if (spalte.Expand)
            {
                rand.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                rand.SizeFlagsStretchRatio = spalte.ExpandGewicht;
            }

            bool aktiv = _sortSpalte == spalte;
            string pfeil = aktiv ? (_sortAbsteigend ? " ▼" : " ▲") : "";
            var farbe = aktiv ? FmTheme.TextPrimary : FmTheme.TextSecondary;

            if (spalte.Sortierung != null)
            {
                // Der Knopf trägt seine Beschriftung selbst - ein Label als verankertes Kind
                // hätte keine Mindestgröße beigesteuert und den Kopf zusammenfallen lassen.
                var knopf = new Button
                {
                    Text        = spalte.Titel + pfeil,
                    Flat        = true,
                    Alignment   = spalte.Ausrichtung,
                    TooltipText = $"Nach {spalte.Titel} sortieren",
                };
                knopf.AddThemeColorOverride("font_color", farbe);
                knopf.AddThemeColorOverride("font_hover_color", FmTheme.TextPrimary);
                knopf.AddThemeColorOverride("font_pressed_color", FmTheme.TextPrimary);
                knopf.AddThemeFontSizeOverride("font_size", 12);
                knopf.AddThemeStyleboxOverride("normal",  new StyleBoxEmpty());
                knopf.AddThemeStyleboxOverride("hover",   new StyleBoxEmpty());
                knopf.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
                knopf.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
                var lokal = spalte;
                knopf.Pressed += () => SortiereNach(lokal);
                rand.AddChild(knopf);
            }
            else
            {
                rand.AddChild(FmTheme.MakeLabel(spalte.Titel, 12, farbe, spalte.Ausrichtung));
            }

            _kopf.AddChild(rand);
        }

        _scrollAusgleich = new Control();
        _kopf.AddChild(_scrollAusgleich);
        AktualisiereScrollAusgleich();
    }

    private const int KopfHoehe = 26;

    internal const int ZusatzBreite = 18;

    // ── Daten ────────────────────────────────────────────────────────────────

    /// <summary>Ersetzt den Inhalt der Tabelle.</summary>
    public void Zeige(IEnumerable<T> daten)
    {
        _daten = daten.ToList();
        Zeichne();
    }

    /// <summary>Zeichnet die vorhandenen Daten neu - etwa nachdem sich Werte geändert haben.</summary>
    public void Aktualisiere()
    {
        foreach (var zeile in _zeilen) zeile.Aktualisiere();
    }

    private void Zeichne()
    {
        foreach (var kind in _zeilenBox.GetChildren()) kind.QueueFree();
        _zeilen.Clear();
        // Die alten Zeilen sind gleich freigegeben - eine Auswahl darauf waere ein toter Verweis.
        _gewaehlteZeile = null;

        var sortiert = Sortiert(_daten);

        if (Gruppieren == null)
        {
            ZeichneZeilen(sortiert);
            return;
        }

        foreach (var gruppe in sortiert.GroupBy(Gruppieren))
        {
            bool zu = _zugeklappt.Contains(gruppe.Key);
            _zeilenBox.AddChild(BaueGruppenkopf(gruppe.Key, zu, gruppe.Count()));
            if (!zu) ZeichneZeilen(gruppe.ToList());
        }
    }

    private void ZeichneZeilen(List<T> daten)
    {
        bool wechsel = false;
        object? letzteZebragruppe = null;

        foreach (var eintrag in daten)
        {
            if (ZebraNeuBei != null)
            {
                var jetzt = ZebraNeuBei(eintrag);
                if (!Equals(jetzt, letzteZebragruppe)) { wechsel = false; letzteZebragruppe = jetzt; }
            }

            var zeile = new GridZeile<T>(_spalten, eintrag, ZeilenHoehe,
                ZeilenZusatz?.Invoke(eintrag), ZiehDaten);
            zeile.Hintergrund = Zeilenfarbe?.Invoke(eintrag, wechsel)
                                ?? (wechsel ? FmTheme.RowAlt : FmTheme.BgPanel);
            zeile.Angeklickt += OnZeileAngeklickt;
            _zeilenBox.AddChild(zeile);
            _zeilen.Add(zeile);
            wechsel = !wechsel;
        }
    }

    private Control BaueGruppenkopf(string titel, bool zugeklappt, int anzahl)
    {
        var knopf = new Button
        {
            Text = $"  {(zugeklappt ? "▶" : "▼")}  {titel}   ({anzahl})",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 26),
            Flat = true,
        };
        knopf.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        knopf.AddThemeFontSizeOverride("font_size", 12);
        knopf.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = FmTheme.BgToolbar });
        knopf.AddThemeStyleboxOverride("hover",  new StyleBoxFlat { BgColor = FmTheme.BgToolbar.Lightened(0.08f) });
        knopf.Pressed += () =>
        {
            if (!_zugeklappt.Remove(titel)) _zugeklappt.Add(titel);
            Zeichne();
        };
        return knopf;
    }

    /// <summary>Klappt alle Gruppen zu und nur die genannte auf.</summary>
    public void NurGruppeOffen(string titel)
    {
        if (Gruppieren == null) return;
        _zugeklappt.Clear();
        foreach (var gruppe in _daten.Select(Gruppieren).Distinct())
        {
            if (gruppe != titel) _zugeklappt.Add(gruppe);
        }
        Zeichne();
    }

    // ── Sortierung ───────────────────────────────────────────────────────────

    private List<T> Sortiert(List<T> daten)
    {
        // Erst die Grundordnung herstellen, dann stabil nach der angeklickten Spalte sortieren.
        // So bleibt sie als Zweitordnung erhalten: Nach Position sortiert stehen innerhalb einer
        // Position weiter die stärkeren Spieler oben.
        var basis = new List<T>(daten);
        if (Standardsortierung != null) basis.Sort(Standardsortierung);

        if (_sortSpalte?.Sortierung is not { } schluessel) return basis;

        return _sortAbsteigend
            ? basis.OrderByDescending(schluessel).ToList()
            : basis.OrderBy(schluessel).ToList();
    }

    private void SortiereNach(GridSpalte<T> spalte)
    {
        // Erneuter Klick dreht die Richtung, eine neue Spalte beginnt aufsteigend.
        if (_sortSpalte == spalte) _sortAbsteigend = !_sortAbsteigend;
        else { _sortSpalte = spalte; _sortAbsteigend = false; }

        BaueKopf();
        Zeichne();
    }

    private void OnZeileAngeklickt(GridZeile<T> zeile, MouseButton taste, bool doppelt)
    {
        if (taste == MouseButton.Right)
        {
            Rechtsklick?.Invoke(zeile.Datensatz);
            return;
        }
        if (doppelt)
        {
            Doppelklick?.Invoke(zeile.Datensatz);
            return;
        }
        if (Ausgewaehlt != null)
        {
            WaehleAus(zeile);
            Ausgewaehlt.Invoke(zeile.Datensatz);
        }
    }

    /// <summary>
    /// Hebt die angeklickte Zeile hervor und stellt die vorherige wieder her. Ohne sichtbare
    /// Markierung waere fuer den Nutzer nicht erkennbar, worauf sich eine Schaltflaeche bezieht.
    /// </summary>
    private void WaehleAus(GridZeile<T> zeile)
    {
        if (_gewaehlteZeile != null && IsInstanceValid(_gewaehlteZeile))
        {
            _gewaehlteZeile.Hintergrund = _farbeVorAuswahl;
        }
        _gewaehlteZeile = zeile;
        _farbeVorAuswahl = zeile.Hintergrund;
        zeile.Hintergrund = FmTheme.Accent.Darkened(0.35f);
    }
}

/// <summary>Eine Zeile im <see cref="FmGrid{T}"/>. Baut ihre Zellen aus den Spaltendefinitionen.</summary>
public partial class GridZeile<T> : PanelContainer
{
    private readonly IReadOnlyList<GridSpalte<T>> _spalten;
    private readonly List<Label> _zellen = new();
    private readonly List<Control> _zellRahmen = new();
    private readonly Func<T, Godot.Collections.Dictionary>? _ziehDaten;

    public T Datensatz { get; }

    public event Action<GridZeile<T>, MouseButton, bool>? Angeklickt;

    private Color _hintergrund = FmTheme.BgPanel;
    public Color Hintergrund
    {
        get => _hintergrund;
        set { _hintergrund = value; SetzeHintergrund(); }
    }

    public GridZeile(IReadOnlyList<GridSpalte<T>> spalten, T datensatz, int hoehe,
        Control? zusatz, Func<T, Godot.Collections.Dictionary>? ziehDaten)
    {
        _spalten   = spalten;
        Datensatz  = datensatz;
        _ziehDaten = ziehDaten;

        CustomMinimumSize = new Vector2(0, hoehe);
        MouseFilter = MouseFilterEnum.Stop;
        if (ziehDaten != null) MouseDefaultCursorShape = CursorShape.Drag;
        SetzeHintergrund();

        var hbox = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        hbox.AddThemeConstantOverride("separation", 0);
        AddChild(hbox);

        if (zusatz != null)
        {
            zusatz.CustomMinimumSize = new Vector2(FmGrid<T>.ZusatzBreite, 0);
            zusatz.MouseFilter = MouseFilterEnum.Ignore;
            hbox.AddChild(zusatz);
        }

        foreach (var spalte in spalten)
        {
            // Der Rahmen trägt den Mouseover, nicht das Label: Labels stehen in Godot auf
            // MouseFilter.Ignore und würden gar keinen Tooltip zeigen.
            var rahmen = new MarginContainer { MouseFilter = MouseFilterEnum.Pass };
            FmTheme.SetMargin(rahmen, 6, 0);
            rahmen.CustomMinimumSize = new Vector2(spalte.Breite, 0);
            if (spalte.Expand)
            {
                rahmen.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                rahmen.SizeFlagsStretchRatio = spalte.ExpandGewicht;
            }

            var zelle = new Label
            {
                HorizontalAlignment = spalte.Ausrichtung,
                VerticalAlignment   = VerticalAlignment.Center,
                AutowrapMode        = TextServer.AutowrapMode.Off,
                MouseFilter         = MouseFilterEnum.Ignore,
            };
            zelle.AddThemeFontSizeOverride("font_size", 12);
            rahmen.AddChild(zelle);
            hbox.AddChild(rahmen);

            _zellen.Add(zelle);
            _zellRahmen.Add(rahmen);
        }

        Aktualisiere();
    }

    /// <summary>Schreibt Text, Farbe und Mouseover aus den Spaltendefinitionen in die Zellen.</summary>
    public void Aktualisiere()
    {
        for (int i = 0; i < _spalten.Count; i++)
        {
            var spalte = _spalten[i];
            _zellen[i].Text = spalte.Text(Datensatz);
            _zellen[i].AddThemeColorOverride("font_color",
                spalte.Farbe?.Invoke(Datensatz) ?? FmTheme.TextPrimary);
            _zellRahmen[i].TooltipText = spalte.Tooltip?.Invoke(Datensatz) ?? "";
        }
    }

    private void SetzeHintergrund()
    {
        var stil = new StyleBoxFlat { BgColor = _hintergrund };
        stil.SetContentMarginAll(0);
        AddThemeStyleboxOverride("panel", stil);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } maus)
        {
            Angeklickt?.Invoke(this, maus.ButtonIndex, maus.DoubleClick);
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_ziehDaten == null) return default;

        var vorschau = new PanelContainer();
        var stil = new StyleBoxFlat { BgColor = FmTheme.BgPanel };
        stil.SetBorderWidthAll(1);
        stil.BorderColor = FmTheme.Border;
        vorschau.AddThemeStyleboxOverride("panel", stil);

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 8, 4);
        rand.AddChild(FmTheme.MakeLabel(VorschauText(), 13, FmTheme.TextPrimary));
        vorschau.AddChild(rand);
        SetDragPreview(vorschau);

        return _ziehDaten(Datensatz);
    }

    /// <summary>Erste nicht-leere Zelle als Beschriftung der Ziehvorschau.</summary>
    private string VorschauText()
    {
        foreach (var zelle in _zellen)
        {
            if (!string.IsNullOrWhiteSpace(zelle.Text)) return zelle.Text;
        }
        return "";
    }
}
