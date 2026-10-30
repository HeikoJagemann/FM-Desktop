#nullable enable
using Godot;

namespace FMDesktop.UI;

/// <summary>
/// Ein gezeichnetes Symbol - Pfeil, Zahnrad, Haken und Co.
///
/// <para>Vorher standen an diesen Stellen Emojis. Die rendert jede Schrift anders, farbig und in
/// eigener Größe, und sie lassen sich nicht in die Farbe des Knopfes setzen. Gezeichnet folgt
/// ein Symbol der Farbe und Größe, die man ihm gibt.</para>
/// </summary>
public partial class Symbol : Control
{
    public enum Art { Abspielen, Zurueck, Zahnrad, Blitz, Haken, Warnung, Kreuz }

    private Art _art;
    private Color _farbe = FmTheme.TextPrimary;

    public Art Form
    {
        get => _art;
        set { _art = value; QueueRedraw(); }
    }

    public Color Farbe
    {
        get => _farbe;
        set { _farbe = value; QueueRedraw(); }
    }

    public static Symbol Neu(Art art, Color farbe, float groesse = 20)
    {
        return new Symbol
        {
            _art = art,
            _farbe = farbe,
            CustomMinimumSize = new Vector2(groesse, groesse),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
    }

    public override void _Draw()
    {
        float s = Mathf.Min(Size.X, Size.Y);
        var o = new Vector2((Size.X - s) / 2, (Size.Y - s) / 2);
        Vector2 P(float x, float y) => o + new Vector2(x, y) * s / 24f;
        float strich = Mathf.Max(1.5f, s / 12f);

        switch (_art)
        {
            case Art.Abspielen:
                DrawColoredPolygon(new[] { P(7, 4.5f), P(19, 12), P(7, 19.5f) }, _farbe);
                break;
            case Art.Zurueck:
                DrawPolyline(new[] { P(15, 18), P(9, 12), P(15, 6) }, _farbe, strich, true);
                break;
            case Art.Haken:
                DrawPolyline(new[] { P(4, 12.5f), P(9.5f, 17.5f), P(20, 6.5f) }, _farbe, strich * 1.2f, true);
                break;
            case Art.Kreuz:
                DrawLine(P(6, 6), P(18, 18), _farbe, strich, true);
                DrawLine(P(18, 6), P(6, 18), _farbe, strich, true);
                break;
            case Art.Blitz:
                DrawColoredPolygon(new[] { P(13, 2), P(4, 14), P(11, 14), P(10, 22), P(19, 10), P(12, 10) }, _farbe);
                break;
            case Art.Warnung:
                DrawPolyline(new[] { P(12, 3), P(22, 20), P(2, 20), P(12, 3) }, _farbe, strich, true);
                DrawLine(P(12, 9), P(12, 14), _farbe, strich, true);
                DrawCircle(P(12, 17), strich * 0.7f, _farbe);
                break;
            case Art.Zahnrad:
                for (int i = 0; i < 8; i++)
                {
                    float w = Mathf.Pi * 2 * i / 8;
                    var richtung = new Vector2(Mathf.Cos(w), Mathf.Sin(w));
                    DrawLine(P(12, 12) + richtung * s * 0.30f, P(12, 12) + richtung * s * 0.44f,
                        _farbe, strich * 1.5f, true);
                }
                DrawArc(P(12, 12), s * 0.30f, 0, Mathf.Pi * 2, 32, _farbe, strich, true);
                DrawArc(P(12, 12), s * 0.11f, 0, Mathf.Pi * 2, 24, _farbe, strich, true);
                break;
        }
    }
}
