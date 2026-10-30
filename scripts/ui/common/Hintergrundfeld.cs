#nullable enable
using Godot;

namespace FMDesktop.UI;

/// <summary>
/// Ein Platz im Halbdunkel: Mittellinie, Mittelkreis und die beiden Strafräume, kaum sichtbar
/// hinter Start- und Ladebildschirm. Gibt der leeren Fläche einen Ort, ohne vom Inhalt
/// abzulenken.
/// </summary>
public partial class Hintergrundfeld : Control
{
    private static readonly Color Linie = new(1, 1, 1, 0.045f);

    public static Hintergrundfeld Neu()
    {
        var feld = new Hintergrundfeld { MouseFilter = MouseFilterEnum.Ignore };
        feld.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        feld.Resized += feld.QueueRedraw;
        return feld;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), FmTheme.BgDark);

        // Etwas unter die Mitte gerückt, damit die Seitenlinie nicht durch den Titel läuft.
        var mitte = Size / 2f + new Vector2(0, Size.Y * 0.06f);
        float hoehe = Size.Y * 0.74f;
        float breite = hoehe * 105f / 68f;
        var feld = new Rect2(mitte - new Vector2(breite, hoehe) / 2f, new Vector2(breite, hoehe));
        float m = hoehe / 68f;

        DrawRect(feld, Linie, false, 2);
        DrawLine(new Vector2(mitte.X, feld.Position.Y), new Vector2(mitte.X, feld.End.Y), Linie, 2);
        DrawArc(mitte, 9.15f * m, 0, Mathf.Pi * 2, 96, Linie, 2, true);
        DrawCircle(mitte, 3, Linie);

        var strafraum = new Vector2(16.5f * m, 40.32f * m);
        DrawRect(new Rect2(new Vector2(feld.Position.X, mitte.Y - strafraum.Y / 2f), strafraum), Linie, false, 2);
        DrawRect(new Rect2(new Vector2(feld.End.X - strafraum.X, mitte.Y - strafraum.Y / 2f), strafraum), Linie, false, 2);
    }
}
