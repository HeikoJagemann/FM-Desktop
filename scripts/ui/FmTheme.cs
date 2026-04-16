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

    public static Label MakeLabel(string text, int size = 14, Color? color = null)
    {
        var lbl = new Label
        {
            Text = text,
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
