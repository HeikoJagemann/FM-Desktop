#nullable enable
using Godot;
using System.Collections.Generic;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Gegenüberstellung der Kennzahlen beider Mannschaften: je Zeile die beiden Werte und dazwischen
/// ein Balken, der zeigt, wie sie sich aufteilen.
///
/// <para>Eigene Klasse, weil dieselbe Tafel an drei Stellen gebraucht wird: live während der
/// Partie, in der Zusammenfassung danach und im Spielbericht eines beliebigen Spiels.</para>
/// </summary>
public partial class StatistikTafel : VBoxContainer
{
    private const int ZeilenHoehe = 8;

    /// <summary>Baut die Tafel neu auf.</summary>
    /// <param name="nurGefuellte">
    /// Blendet Zeilen aus, in denen beide Mannschaften bei null stehen - während der ersten
    /// Spielminuten wäre die halbe Tafel sonst eine Reihe von Nullen.
    /// </param>
    public void Zeige(Spielstatistik? heim, Spielstatistik? gast, bool nurGefuellte = false)
    {
        foreach (Node kind in GetChildren()) kind.QueueFree();

        AddThemeConstantOverride("separation", 10);

        foreach (var zeile in Spielstatistik.Zeilen(heim, gast))
        {
            if (nurGefuellte && !zeile.HatWerte) continue;
            AddChild(BaueZeile(zeile));
        }
    }

    private static Control BaueZeile(Statistikzeile zeile)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);

        var heimWert = FmTheme.MakeLabel(zeile.HeimText, 13, FmTheme.TextPrimary);
        heimWert.CustomMinimumSize = new Vector2(58, 0);
        kopf.AddChild(heimWert);

        var titel = FmTheme.MakeLabel(zeile.Titel, 12, FmTheme.TextSecondary,
            HorizontalAlignment.Center);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(titel);

        var gastWert = FmTheme.MakeLabel(zeile.GastText, 13, FmTheme.TextPrimary,
            HorizontalAlignment.Right);
        gastWert.CustomMinimumSize = new Vector2(58, 0);
        kopf.AddChild(gastWert);

        box.AddChild(kopf);
        box.AddChild(BaueBalken(zeile.HeimAnteil));
        return box;
    }

    /// <summary>
    /// Der Vergleichsbalken. Zwei Hälften in einem HBox, deren Breitenverhältnis dem Wertverhältnis
    /// entspricht - so ist auf einen Blick zu sehen, wer die Partie bestimmt.
    /// </summary>
    private static Control BaueBalken(double heimAnteil)
    {
        var balken = new HBoxContainer { CustomMinimumSize = new Vector2(0, ZeilenHoehe) };
        balken.AddThemeConstantOverride("separation", 2);

        balken.AddChild(Haelfte(FmTheme.Accent, (float)heimAnteil));
        balken.AddChild(Haelfte(FmTheme.TextSecondary, (float)(1.0 - heimAnteil)));
        return balken;
    }

    private static Control Haelfte(Color farbe, float gewicht)
    {
        var rect = new ColorRect
        {
            Color = farbe,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            // Godot verteilt den Platz nach dem Stretch-Verhältnis; 0 würde die Hälfte ganz
            // verschwinden lassen, ein winziger Rest hält sie als Strich sichtbar.
            SizeFlagsStretchRatio = Mathf.Max(0.001f, gewicht),
            CustomMinimumSize = new Vector2(0, ZeilenHoehe),
        };
        return rect;
    }
}
