#nullable enable
using Godot;

namespace FMDesktop.UI;

/// <summary>
/// Zentrale Farbpalette, Schriften und Style-Helfer für das gesamte UI - die Stilvorlage
/// "Flutlicht": dunkles Grünschwarz, eine schmale Sportschrift für Überschriften und Zahlen,
/// Inter für alles Bedienbare, Bernstein als einzige Akzentfarbe und nur für Aktionen.
///
/// <para>Die meisten Ansichten setzen Farben einzeln über Overrides. Damit auch sie Schrift,
/// Knöpfe, Listen und Tooltips im selben Stil bekommen, legt <see cref="Basistheme"/> ein
/// Theme auf das ganze Fenster; Overrides gehen weiterhin vor.</para>
/// </summary>
public static class FmTheme
{
    // ── Flächen ───────────────────────────────────────────────
    public static readonly Color BgDark      = new("0D1411");
    public static readonly Color BgToolbar   = new("111B17");
    public static readonly Color BgSubbar    = new("0F1814");
    public static readonly Color BgPanel     = new("121C18");
    public static readonly Color BgKnopf     = new("18241F");
    public static readonly Color BgHover     = new("1E2C26");
    public static readonly Color RowAlt      = new("15201B");
    public static readonly Color Border      = new("22302A");
    public static readonly Color BorderStark = new("2E4038");

    // ── Text ──────────────────────────────────────────────────
    public static readonly Color TextPrimary   = new("EEF2EE");
    public static readonly Color TextSecondary = new("9DAEA5");
    public static readonly Color TextNav       = new("A9B8B0");
    public static readonly Color TextGedimmt   = new("6F8178");

    // ── Akzent und Zustände ───────────────────────────────────
    public static readonly Color Accent      = new("F2B33D");
    public static readonly Color AccentHover = new("FFC75A");
    /// <summary>Schrift auf Bernstein - weiß wäre dort unlesbar.</summary>
    public static readonly Color AccentText  = new("1A1405");
    public static readonly Color Success     = new("6FD39A");
    public static readonly Color Danger      = new("F27A6B");
    public static readonly Color Gold        = new("F7CB6E");

    /// <summary>Zeile des eigenen Vereins - der Akzent, ganz zurückgenommen.</summary>
    public static readonly Color MarkiertZeile = BgPanel.Lerp(Accent, 0.12f);
    /// <summary>Ausgewählte Zeile - etwas kräftiger als die Markierung.</summary>
    public static readonly Color AuswahlZeile  = BgPanel.Lerp(Accent, 0.22f);

    // ── Rasen ─────────────────────────────────────────────────
    public static readonly Color Rasen       = new("2E6B3F");
    public static readonly Color RasenStreif = new("2A633A");
    public static readonly Color Linie       = new(1, 1, 1, 0.45f);

    // ── Positionsgruppen ──────────────────────────────────────
    // Ring- und Balkenfarbe je Mannschaftsteil; sie unterscheiden sich auch in der Helligkeit.
    public static readonly Color FarbeTor        = new("F2B33D");
    public static readonly Color FarbeAbwehr     = new("5AA9F0");
    public static readonly Color FarbeMittelfeld = new("6FD39A");
    public static readonly Color FarbeSturm      = new("F27A6B");

    // Zeilenhintergrund: die Gruppenfarbe, kaum mehr als angedeutet - der Text soll nicht leiden.
    public static readonly Color GruppeTor        = BgPanel.Lerp(FarbeTor, 0.07f);
    public static readonly Color GruppeAbwehr     = BgPanel.Lerp(FarbeAbwehr, 0.07f);
    public static readonly Color GruppeMittelfeld = BgPanel.Lerp(FarbeMittelfeld, 0.07f);
    public static readonly Color GruppeSturm      = BgPanel.Lerp(FarbeSturm, 0.07f);

    /// <summary>Hintergrundfarbe einer Spielerzeile nach Positionsgruppe.</summary>
    public static Color FuerGruppe(Models.Positionsgruppe gruppe, bool abgesetzt = false)
    {
        var farbe = gruppe switch
        {
            Models.Positionsgruppe.Tor        => GruppeTor,
            Models.Positionsgruppe.Abwehr     => GruppeAbwehr,
            Models.Positionsgruppe.Mittelfeld => GruppeMittelfeld,
            _                                 => GruppeSturm,
        };
        // Jede zweite Zeile leicht aufgehellt, damit Zeilen unterscheidbar bleiben.
        return abgesetzt ? farbe.Lightened(0.04f) : farbe;
    }

    /// <summary>Ring-, Balken- und Akzentfarbe eines Mannschaftsteils.</summary>
    public static Color GruppeFarbe(Models.Positionsgruppe gruppe) => gruppe switch
    {
        Models.Positionsgruppe.Tor        => FarbeTor,
        Models.Positionsgruppe.Abwehr     => FarbeAbwehr,
        Models.Positionsgruppe.Mittelfeld => FarbeMittelfeld,
        _                                 => FarbeSturm,
    };

    /// <summary>Hintergrund eines Positionschips.</summary>
    public static Color GruppeChip(Models.Positionsgruppe gruppe) => gruppe switch
    {
        Models.Positionsgruppe.Tor        => new Color("3D3013"),
        Models.Positionsgruppe.Abwehr     => new Color("16304A"),
        Models.Positionsgruppe.Mittelfeld => new Color("173A28"),
        _                                 => new Color("45201C"),
    };

    /// <summary>Textfarbe für die Positionsspalte und Positionschips.</summary>
    public static Color TextFuerGruppe(Models.Positionsgruppe gruppe) => gruppe switch
    {
        Models.Positionsgruppe.Tor        => new Color("F7CB6E"),
        Models.Positionsgruppe.Abwehr     => new Color("8EC4F5"),
        Models.Positionsgruppe.Mittelfeld => new Color("8FE0B1"),
        _                                 => new Color("FFA99D"),
    };

    // ── Schriften ─────────────────────────────────────────────
    private const string PfadInter      = "res://assets/fonts/Inter-Variabel.ttf";
    private const string PfadBarlow     = "res://assets/fonts/BarlowCondensed-SemiBold.ttf";
    private const string PfadBarlowFett = "res://assets/fonts/BarlowCondensed-Bold.ttf";

    private static Font? _inter;
    private static readonly System.Collections.Generic.Dictionary<int, Font> _interGewichte = new();
    private static Font? _anzeige;
    private static Font? _zahl;

    /// <summary>Inter in einem Gewicht (400 normal, 500 mittel, 600 halbfett, 700 fett).</summary>
    public static Font SchriftUi(int gewicht = 500)
    {
        if (_interGewichte.TryGetValue(gewicht, out var vorhanden)) return vorhanden;

        _inter ??= Lade(PfadInter);
        var variante = new FontVariation { BaseFont = _inter };
        var server = TextServerManager.GetPrimaryInterface();
        variante.VariationOpentype = new Godot.Collections.Dictionary
        {
            [server.NameToTag("wght")] = gewicht,
        };
        _interGewichte[gewicht] = variante;
        return variante;
    }

    /// <summary>Barlow Condensed Halbfett - Überschriften, Formationen, Reiter der Anzeige.</summary>
    public static Font SchriftAnzeige
    {
        get
        {
            if (_anzeige != null) return _anzeige;
            _anzeige = new FontVariation { BaseFont = Lade(PfadBarlow), SpacingGlyph = 1 };
            return _anzeige;
        }
    }

    /// <summary>Barlow Condensed Fett - Zahlen: Stärke, Ergebnis, Punkte.</summary>
    public static Font SchriftZahl => _zahl ??= Lade(PfadBarlowFett);

    private static Font Lade(string pfad)
    {
        var schrift = ResourceLoader.Exists(pfad) ? GD.Load<Font>(pfad) : null;
        if (schrift == null)
        {
            GD.PrintErr($"[FmTheme] Schrift nicht gefunden: {pfad} - es gilt die Standardschrift.");
            return ThemeDB.FallbackFont;
        }
        return schrift;
    }

    // ── StyleBoxen ────────────────────────────────────────────
    /// <summary>Gerundete Fläche, optional mit Rand.</summary>
    public static StyleBoxFlat Flaeche(Color bg, int radius = 10, Color? rand = null, int randBreite = 1)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetCornerRadiusAll(radius);
        if (rand.HasValue)
        {
            s.BorderColor = rand.Value;
            s.SetBorderWidthAll(randBreite);
        }
        s.SetContentMarginAll(0);
        return s;
    }

    public static StyleBoxFlat PanelStyle(int radius = 12) => Flaeche(BgPanel, radius, Border);

    public static StyleBoxFlat ButtonStyle(Color bg, int radius = 10)
    {
        var s = Flaeche(bg, radius);
        s.ContentMarginLeft = s.ContentMarginRight = 14;
        s.ContentMarginTop = s.ContentMarginBottom = 6;
        return s;
    }

    public static StyleBoxFlat ToolbarStyle()
    {
        var s = new StyleBoxFlat { BgColor = BgToolbar, BorderColor = Border, BorderWidthBottom = 1 };
        s.SetContentMarginAll(0);
        return s;
    }

    /// <summary>Gerundeter Balken für Kondition/Frische-Anzeigen (ProgressBar-Stylebox).</summary>
    public static StyleBoxFlat KonditionsBalken(Color farbe)
    {
        var s = new StyleBoxFlat { BgColor = farbe };
        s.SetCornerRadiusAll(3);
        return s;
    }

    /// <summary>Einheitliche Ampelfarbe für einen Frische-/Konditionswert (0-100).</summary>
    /// <summary>Farbe der Form: Grün im Hoch, Rot im Tief, neutral dazwischen.</summary>
    public static Color FormFarbe(int form) => form switch
    {
        >= 75 => Success,
        >= 60 => new Color("A8DDB9"),
        > 40  => TextPrimary,
        > 25  => new Color("F2B0A6"),
        _     => Danger,
    };

    public static Color FrischeFarbe(double frische) => frische switch
    {
        >= 85 => Success,
        >= 70 => Gold,
        _     => Danger,
    };

    /// <summary>Ob auf dieser Farbe dunkle Schrift stehen muss.</summary>
    public static bool IstHell(Color farbe) => farbe.Luminance > 0.45f;

    // ── Hilfs-Methoden für Controls ───────────────────────────
    /// <summary>
    /// Einfärbt einen Knopf. Die Schriftfarbe richtet sich nach dem Hintergrund: Auf Bernstein
    /// steht dunkle Schrift, sonst helle.
    /// </summary>
    public static void ApplyButton(Button btn, Color bg)
    {
        bool hell = IstHell(bg);
        var hover = hell ? bg.Lightened(0.10f) : bg.Lightened(0.06f);

        btn.AddThemeStyleboxOverride("normal",   MitRand(ButtonStyle(bg), hell));
        btn.AddThemeStyleboxOverride("hover",    MitRand(ButtonStyle(hover), hell));
        btn.AddThemeStyleboxOverride("pressed",  MitRand(ButtonStyle(bg.Darkened(0.12f)), hell));
        btn.AddThemeStyleboxOverride("focus",    new StyleBoxEmpty());
        btn.AddThemeStyleboxOverride("disabled", MitRand(ButtonStyle(hell ? bg.Darkened(0.35f) : bg), hell));

        var schrift = hell ? AccentText : TextPrimary;
        btn.AddThemeColorOverride("font_color",          schrift);
        btn.AddThemeColorOverride("font_hover_color",    schrift);
        btn.AddThemeColorOverride("font_pressed_color",  schrift);
        btn.AddThemeColorOverride("font_focus_color",    schrift);
        btn.AddThemeColorOverride("font_disabled_color", hell ? AccentText : TextGedimmt);
        btn.AddThemeFontOverride("font", SchriftUi(hell ? 700 : 600));
    }

    private static StyleBoxFlat MitRand(StyleBoxFlat s, bool hell)
    {
        if (hell) return s;
        s.BorderColor = BorderStark;
        s.SetBorderWidthAll(1);
        return s;
    }

    /// <summary>Der eine Hauptknopf einer Ansicht - Bernstein, dunkle Schrift.</summary>
    public static void ApplyPrimaryButton(Button btn) => ApplyButton(btn, Accent);

    /// <summary>Normaler Knopf - dunkle Fläche mit Rand.</summary>
    public static void ApplySecondaryButton(Button btn) => ApplyButton(btn, BgKnopf);

    /// <summary>
    /// Einheitliche Geldformatierung - Tausendertrennung mit Punkt (deutsch), Euro-Zeichen am
    /// Betrag. Vorher gab es zwei uneinheitliche Schreibweisen: einmal das € im Spaltentitel,
    /// einmal am Betrag selbst. Die eigentliche Formatierung liegt in
    /// <see cref="Models.Geldformat"/>, damit auch die Modellschicht sie nutzen kann.
    /// </summary>
    public static string Geld(long betrag) => Models.Geldformat.Text(betrag);

    /// <summary>
    /// Gehalt in der vom Nutzer eingestellten Periode (Woche/Monat/Jahr) formatiert -
    /// <paramref name="wochenbetrag"/> ist immer der vom Backend gelieferte Wochenbetrag.
    /// </summary>
    public static string Gehalt(long wochenbetrag) => Models.Gehaltsformat.Text(wochenbetrag);

    /// <summary>Spaltentitel "Gehalt/Woche" etc., passend zur eingestellten Periode.</summary>
    public static string GehaltTitel => Models.Gehaltsformat.Titel;

    public static Label MakeLabel(string text, int size = 14, Color? color = null,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var lbl = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        lbl.AddThemeColorOverride("font_color", color ?? TextPrimary);
        lbl.AddThemeFontSizeOverride("font_size", size);
        return lbl;
    }

    /// <summary>Seitenüberschrift: Barlow Condensed, Versalien.</summary>
    public static Label MakeTitel(string text, int size = 34, Color? color = null)
    {
        var lbl = MakeLabel(text.ToUpperInvariant(), size, color);
        lbl.AddThemeFontOverride("font", SchriftAnzeige);
        return lbl;
    }

    /// <summary>Kleine Abschnittsüberschrift über Karten und Listen.</summary>
    public static Label MakeAbschnitt(string text, int size = 20) => MakeTitel(text, size);

    /// <summary>Eine Zahl in der Zahlenschrift - Stärke, Ergebnis, Punkte.</summary>
    public static Label MakeZahl(string text, int size = 20, Color? color = null)
    {
        var lbl = MakeLabel(text, size, color);
        lbl.AddThemeFontOverride("font", SchriftZahl);
        return lbl;
    }

    /// Setzt Innenabstand an einem MarginContainer.
    public static void SetMargin(MarginContainer c, int all) => SetMargin(c, all, all, all, all);
    public static void SetMargin(MarginContainer c, int lr, int tb) => SetMargin(c, lr, lr, tb, tb);
    public static void SetMargin(MarginContainer c, int l, int r, int t, int b)
    {
        c.AddThemeConstantOverride("margin_left",   l);
        c.AddThemeConstantOverride("margin_right",  r);
        c.AddThemeConstantOverride("margin_top",    t);
        c.AddThemeConstantOverride("margin_bottom", b);
    }

    // ── Theme für das ganze Fenster ───────────────────────────
    /// <summary>
    /// Das Grundtheme: Schrift, Knöpfe, Eingabefelder, Menüs, Tooltips, Bildlaufleisten und
    /// Dialoge im Flutlicht-Stil. Wird einmal beim Start auf das Hauptfenster gelegt.
    /// </summary>
    public static Theme Basistheme()
    {
        var t = new Theme
        {
            DefaultFont = SchriftUi(500),
            DefaultFontSize = 14,
        };

        // Text
        t.SetColor("font_color", "Label", TextPrimary);

        // Knöpfe - auch MenuButton, OptionButton und CheckBox erben von Button.
        foreach (var typ in new[] { "Button", "MenuButton", "OptionButton" })
        {
            t.SetStylebox("normal",   typ, MitRand(ButtonStyle(BgKnopf), false));
            t.SetStylebox("hover",    typ, MitRand(ButtonStyle(BgHover), false));
            t.SetStylebox("pressed",  typ, MitRand(ButtonStyle(Border), false));
            t.SetStylebox("disabled", typ, ButtonStyle(BgPanel));
            t.SetStylebox("focus",    typ, new StyleBoxEmpty());
            t.SetFont("font", typ, SchriftUi(600));
            t.SetColor("font_color",          typ, TextPrimary);
            t.SetColor("font_hover_color",    typ, TextPrimary);
            t.SetColor("font_pressed_color",  typ, TextPrimary);
            t.SetColor("font_focus_color",    typ, TextPrimary);
            t.SetColor("font_disabled_color", typ, TextGedimmt);
        }
        foreach (var typ in new[] { "CheckBox", "CheckButton" })
        {
            foreach (var zustand in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
                t.SetStylebox(zustand, typ, new StyleBoxEmpty());
            t.SetColor("font_color",       typ, TextSecondary);
            t.SetColor("font_hover_color", typ, TextPrimary);
            t.SetColor("font_pressed_color", typ, TextPrimary);
        }

        // Eingabefelder
        var feld = Flaeche(BgSubbar, 8, BorderStark);
        feld.ContentMarginLeft = feld.ContentMarginRight = 10;
        feld.ContentMarginTop = feld.ContentMarginBottom = 6;
        var feldFokus = (StyleBoxFlat)feld.Duplicate();
        feldFokus.BorderColor = Accent;
        foreach (var typ in new[] { "LineEdit", "TextEdit" })
        {
            t.SetStylebox("normal", typ, feld);
            t.SetStylebox("focus",  typ, feldFokus);
            t.SetStylebox("read_only", typ, feld);
            t.SetColor("font_color", typ, TextPrimary);
            t.SetColor("caret_color", typ, Accent);
            t.SetColor("selection_color", typ, AuswahlZeile);
        }

        // Menüs und Tooltips
        var menue = Flaeche(new Color("15201B"), 10, BorderStark);
        menue.SetContentMarginAll(6);
        t.SetStylebox("panel", "PopupMenu", menue);
        t.SetStylebox("hover", "PopupMenu", Flaeche(BgHover, 6));
        t.SetColor("font_color", "PopupMenu", TextPrimary);
        t.SetColor("font_hover_color", "PopupMenu", TextPrimary);
        t.SetColor("font_disabled_color", "PopupMenu", TextGedimmt);
        t.SetColor("font_separator_color", "PopupMenu", TextSecondary);
        t.SetConstant("v_separation", "PopupMenu", 8);

        var tooltip = Flaeche(new Color("1C2A24"), 8, BorderStark);
        tooltip.ContentMarginLeft = tooltip.ContentMarginRight = 10;
        tooltip.ContentMarginTop = tooltip.ContentMarginBottom = 8;
        t.SetStylebox("panel", "TooltipPanel", tooltip);
        t.SetColor("font_color", "TooltipLabel", TextPrimary);
        t.SetFontSize("font_size", "TooltipLabel", 13);

        // Flächen
        t.SetStylebox("panel", "PanelContainer", PanelStyle());
        t.SetStylebox("panel", "Panel", PanelStyle());

        // Reiter (TabContainer, TabBar): wie die Untermenüzeile - Pillen, die aktive hinterlegt.
        foreach (var typ in new[] { "TabContainer", "TabBar" })
        {
            var aktiv = Flaeche(BgHover, 8);
            aktiv.ContentMarginLeft = aktiv.ContentMarginRight = 14;
            aktiv.ContentMarginTop = aktiv.ContentMarginBottom = 6;
            var ruhig = (StyleBoxFlat)aktiv.Duplicate();
            ruhig.BgColor = new Color(0, 0, 0, 0);
            var schwebend = (StyleBoxFlat)aktiv.Duplicate();
            schwebend.BgColor = BgKnopf;
            t.SetStylebox("tab_selected", typ, aktiv);
            t.SetStylebox("tab_unselected", typ, ruhig);
            t.SetStylebox("tab_hovered", typ, schwebend);
            t.SetStylebox("tab_disabled", typ, ruhig);
            t.SetStylebox("tab_focus", typ, new StyleBoxEmpty());
            t.SetFont("font", typ, SchriftUi(600));
            t.SetFontSize("font_size", typ, 13);
            t.SetColor("font_selected_color", typ, TextPrimary);
            t.SetColor("font_unselected_color", typ, TextNav);
            t.SetColor("font_hovered_color", typ, TextPrimary);
            t.SetColor("font_disabled_color", typ, TextGedimmt);
        }
        t.SetStylebox("tabbar_background", "TabContainer", new StyleBoxEmpty());
        t.SetStylebox("panel", "TabContainer", new StyleBoxEmpty());
        t.SetConstant("side_margin", "TabContainer", 0);

        // Trennlinien
        var linie = new StyleBoxLine { Color = Border, Thickness = 1 };
        t.SetStylebox("separator", "HSeparator", linie);
        t.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = Border, Thickness = 1, Vertical = true });

        // Bildlaufleisten: schmal und ruhig
        foreach (var typ in new[] { "VScrollBar", "HScrollBar" })
        {
            var spur = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
            spur.SetContentMarginAll(3);
            t.SetStylebox("scroll", typ, spur);
            t.SetStylebox("scroll_focus", typ, spur);
            t.SetStylebox("grabber", typ, Flaeche(BorderStark, 4));
            t.SetStylebox("grabber_highlight", typ, Flaeche(TextGedimmt, 4));
            t.SetStylebox("grabber_pressed", typ, Flaeche(TextSecondary, 4));
        }

        // Fortschritt
        t.SetStylebox("background", "ProgressBar", Flaeche(Border, 3));
        t.SetStylebox("fill", "ProgressBar", KonditionsBalken(Accent));
        t.SetColor("font_color", "ProgressBar", TextPrimary);

        // Dialoge
        var dialog = new StyleBoxFlat { BgColor = BgPanel };
        dialog.SetContentMarginAll(16);
        t.SetStylebox("panel", "AcceptDialog", dialog);
        t.SetStylebox("embedded_border", "Window", Flaeche(BgPanel, 12, BorderStark));
        t.SetColor("title_color", "Window", TextPrimary);

        return t;
    }
}
