using Godot;

namespace FMDesktop.UI;

/// <summary>Zentrale Farbpalette und Style-Helfer für das gesamte UI.</summary>
public static class FmTheme
{
    // ── Farben ────────────────────────────────────────────────
    public static readonly Color BgDark      = new(0.08f, 0.10f, 0.13f);
    public static readonly Color BgPanel     = new(0.12f, 0.15f, 0.19f);
    public static readonly Color BgToolbar   = new(0.10f, 0.13f, 0.17f);
    public static readonly Color Accent      = new(0.18f, 0.52f, 0.89f);
    public static readonly Color AccentHover = new(0.25f, 0.62f, 0.95f);
    public static readonly Color Success     = new(0.18f, 0.72f, 0.42f);
    public static readonly Color Danger      = new(0.85f, 0.25f, 0.25f);
    public static readonly Color TextPrimary = new(0.95f, 0.95f, 0.95f);
    public static readonly Color TextSecondary = new(0.60f, 0.65f, 0.70f);
    public static readonly Color Border      = new(0.25f, 0.30f, 0.38f);
    public static readonly Color Gold        = new(1.00f, 0.85f, 0.10f);
    public static readonly Color RowAlt      = new(0.10f, 0.13f, 0.17f);

    // ── Positionsgruppen ──────────────────────────────────────
    // Dezent gehalten: Die Zeile soll erkennbar eingefärbt sein, ohne dass der Text leidet.
    public static readonly Color GruppeTor        = new(0.24f, 0.20f, 0.09f);
    public static readonly Color GruppeAbwehr     = new(0.11f, 0.17f, 0.26f);
    public static readonly Color GruppeMittelfeld = new(0.10f, 0.21f, 0.15f);
    public static readonly Color GruppeSturm      = new(0.24f, 0.13f, 0.13f);

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
        return abgesetzt ? farbe.Lightened(0.06f) : farbe;
    }

    /// <summary>Textfarbe für die Positionsspalte - kräftiger als der Zeilenhintergrund.</summary>
    public static Color TextFuerGruppe(Models.Positionsgruppe gruppe) => gruppe switch
    {
        Models.Positionsgruppe.Tor        => Gold,
        Models.Positionsgruppe.Abwehr     => new Color(0.45f, 0.68f, 0.95f),
        Models.Positionsgruppe.Mittelfeld => new Color(0.40f, 0.85f, 0.55f),
        _                                 => new Color(0.95f, 0.50f, 0.45f),
    };

    // ── StyleBoxen ────────────────────────────────────────────
    public static StyleBoxFlat PanelStyle(int radius = 6)
    {
        var s = new StyleBoxFlat
        {
            BgColor     = BgPanel,
            BorderColor = Border,
            CornerRadiusTopLeft     = radius,
            CornerRadiusTopRight    = radius,
            CornerRadiusBottomLeft  = radius,
            CornerRadiusBottomRight = radius,
        };
        s.SetBorderWidthAll(1);
        s.SetContentMarginAll(0);
        return s;
    }

    public static StyleBoxFlat ButtonStyle(Color bg, int radius = 4)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft     = radius,
            CornerRadiusTopRight    = radius,
            CornerRadiusBottomLeft  = radius,
            CornerRadiusBottomRight = radius,
        };
        s.SetContentMarginAll(0);
        return s;
    }

    public static StyleBoxFlat ToolbarStyle()
    {
        var s = new StyleBoxFlat { BgColor = BgToolbar };
        s.SetContentMarginAll(0);
        return s;
    }

    // ── Hilfs-Methoden für Controls ───────────────────────────
    public static void ApplyButton(Button btn, Color bg)
    {
        btn.AddThemeStyleboxOverride("normal",   ButtonStyle(bg));
        btn.AddThemeStyleboxOverride("hover",    ButtonStyle(AccentHover));
        btn.AddThemeStyleboxOverride("pressed",  ButtonStyle(bg.Darkened(0.15f)));
        btn.AddThemeStyleboxOverride("focus",    ButtonStyle(bg));
        btn.AddThemeColorOverride("font_color",  Colors.White);
    }

    /// <summary>
    /// Einheitliche Geldformatierung - Tausendertrennung mit Punkt (deutsch), Euro-Zeichen am
    /// Betrag. Vorher gab es zwei uneinheitliche Schreibweisen: einmal das € im Spaltentitel,
    /// einmal am Betrag selbst. Die eigentliche Formatierung liegt in
    /// <see cref="Models.Geldformat"/>, damit auch die Modellschicht sie nutzen kann.
    /// </summary>
    public static string Geld(long betrag) => Models.Geldformat.Text(betrag);

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
}
