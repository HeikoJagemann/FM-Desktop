#nullable enable
using Godot;
using System.Collections.Generic;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Das Spielfeld der laufenden Partie: Linien, zweiundzwanzig Marker, der Ball.
///
/// <para>Gezeichnet wird prozedural in <c>_Draw</c> statt aus Sprites zusammengesetzt. Ein
/// Fußballplatz besteht aus Geraden und Kreisbögen mit amtlich festgelegten Maßen - als Grafik
/// wäre er nur unschärfer und müsste für jede Fenstergröße neu skaliert werden.</para>
///
/// <para>Die Positionen kommen vom Server: Die Agenten-Engine rechnet die Partie, der Client
/// zeichnet sie. Zwischen den zehn Takten je Sekunde interpoliert <see cref="SzenenPlayer"/>,
/// damit die Laufwege nicht in Zehntelsprüngen ruckeln.</para>
/// </summary>
public partial class Spielfeld : Control
{
    // ── Maße in Metern nach IFAB-Regel 1 ─────────────────────────────────────
    private const float FeldLaenge        = 105f;
    private const float FeldBreite        = 68f;
    private const float StrafraumTiefe    = 16.5f;
    private const float StrafraumBreite   = 40.32f;
    private const float TorraumTiefe      = 5.5f;
    private const float TorraumBreite     = 18.32f;
    private const float MittelkreisRadius = 9.15f;
    private const float ElfmeterAbstand   = 11f;
    private const float EckenRadius       = 1f;
    private const float TorBreite         = 7.32f;
    private const float TorTiefe          = 2f;

    // Etwas dunkler als der Rasen der Aufstellung: Hier müssen 22 Punkte und der Ball darauf
    // hervortreten, nicht Plaketten mit eigenem Untergrund.
    private static readonly Color Rasen        = new("1E4A2B");
    private static readonly Color RasenStreif  = new("22512F");
    private static readonly Color Linie        = new(1f, 1f, 1f, 0.38f);
    private static readonly Color HeimFarbe    = new("5AA9F0");
    private static readonly Color GastFarbe    = new("F08A4B");
    private static readonly Color TorwartFarbe = new("F7CB6E");
    private static readonly Color BallFarbe    = new(0.97f, 0.97f, 0.95f);

    /// <summary>Zahl der Mähstreifen - ungerade, damit die Mittellinie in einem Streifen liegt.</summary>
    private const int Streifen = 9;

    private const float MarkerRadius = 13f;
    private const float BallRadius   = 5f;

    /// <summary>Wie stark ein hoher Ball größer wirkt - ein Meter Höhe, ein Fünftel mehr.</summary>
    private const float HoehenZoom = 0.2f;

    private List<SpielfeldSpieler> _aufstellung = new();
    private SzenenPlayer.Bild? _bild;

    /// <summary>Die Rasenfläche in Pixeln - in <c>_Draw</c> aus der Knotengröße berechnet.</summary>
    private Rect2 _feld;

    public Spielfeld()
    {
        CustomMinimumSize = new Vector2(0, 260);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }

    public override void _Ready()
    {
        Resized += QueueRedraw;
    }

    /// <summary>Namen und Trikotfarben - kommen einmal und bleiben.</summary>
    public void SetzeAufstellung(List<SpielfeldSpieler> aufstellung)
    {
        _aufstellung = aufstellung;
        QueueRedraw();
    }

    /// <summary>Das nächste zu zeichnende Bild.</summary>
    public void ZeigeBild(SzenenPlayer.Bild bild)
    {
        _bild = bild;
        QueueRedraw();
    }

    // ── Zeichnen ─────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        _feld = BerechneFeld(Size);
        if (_feld.Size.X <= 1f || _feld.Size.Y <= 1f) return;

        ZeichneRasen();
        ZeichneLinien();

        if (_bild == null) return;

        ZeichneSpieler(_bild);
        ZeichneBall(_bild);
    }

    /// <summary>
    /// Größtes Rechteck im Seitenverhältnis 105:68, das in die Fläche passt. Ein verzerrter
    /// Platz wäre schlimmer als ein kleiner: Abstände sollen ablesbar bleiben.
    /// </summary>
    private static Rect2 BerechneFeld(Vector2 flaeche)
    {
        const float rand = 12f;
        float verfuegbareBreite = flaeche.X - 2 * rand;
        float verfuegbareHoehe  = flaeche.Y - 2 * rand;
        if (verfuegbareBreite <= 0f || verfuegbareHoehe <= 0f) return new Rect2();

        const float verhaeltnis = FeldLaenge / FeldBreite;
        float breite = verfuegbareBreite;
        float hoehe  = breite / verhaeltnis;
        if (hoehe > verfuegbareHoehe)
        {
            hoehe = verfuegbareHoehe;
            breite = hoehe * verhaeltnis;
        }

        return new Rect2(new Vector2((flaeche.X - breite) / 2f, (flaeche.Y - hoehe) / 2f),
                         new Vector2(breite, hoehe));
    }

    /// <summary>Meter auf dem Platz zu Pixeln im Knoten.</summary>
    private Vector2 P(float xMeter, float yMeter) => new(
        _feld.Position.X + xMeter / FeldLaenge * _feld.Size.X,
        _feld.Position.Y + yMeter / FeldBreite * _feld.Size.Y);

    /// <summary>Eine Länge in Metern in Pixel. Das Feld ist unverzerrt, also genügt ein Faktor.</summary>
    private float M(float meter) => meter / FeldLaenge * _feld.Size.X;

    private void ZeichneRasen()
    {
        DrawRect(_feld, Rasen);
        float breite = _feld.Size.X / Streifen;
        for (int i = 1; i < Streifen; i += 2)
        {
            DrawRect(new Rect2(_feld.Position.X + i * breite, _feld.Position.Y,
                               breite, _feld.Size.Y), RasenStreif);
        }
    }

    private void ZeichneLinien()
    {
        const float dick = 2f;
        var mittelpunkt = P(FeldLaenge / 2f, FeldBreite / 2f);

        DrawRect(_feld, Linie, filled: false, width: dick);
        DrawLine(P(FeldLaenge / 2f, 0f), P(FeldLaenge / 2f, FeldBreite), Linie, dick);
        DrawArc(mittelpunkt, M(MittelkreisRadius), 0f, Mathf.Tau, 64, Linie, dick);
        DrawCircle(mittelpunkt, 3f, Linie);

        foreach (bool links in new[] { true, false })
        {
            ZeichneKasten(links, StrafraumTiefe, StrafraumBreite, dick);
            ZeichneKasten(links, TorraumTiefe, TorraumBreite, dick);
            ZeichneTor(links, dick);

            float elfmeter = links ? ElfmeterAbstand : FeldLaenge - ElfmeterAbstand;
            DrawCircle(P(elfmeter, FeldBreite / 2f), 3f, Linie);
        }

        ZeichneEckbogen(0f, 0f, 0f);
        ZeichneEckbogen(0f, FeldBreite, -Mathf.Pi / 2f);
        ZeichneEckbogen(FeldLaenge, 0f, Mathf.Pi / 2f);
        ZeichneEckbogen(FeldLaenge, FeldBreite, Mathf.Pi);
    }

    /// <summary>Straf- oder Torraum an einer der beiden Torlinien.</summary>
    private void ZeichneKasten(bool links, float tiefe, float breite, float dicke)
    {
        float x0 = links ? 0f : FeldLaenge - tiefe;
        float y0 = (FeldBreite - breite) / 2f;
        var ecke = P(x0, y0);
        DrawRect(new Rect2(ecke, P(x0 + tiefe, y0 + breite) - ecke), Linie, filled: false, width: dicke);
    }

    /// <summary>Das Tor liegt außerhalb der Torlinie und ragt in den Rand hinein.</summary>
    private void ZeichneTor(bool links, float dicke)
    {
        float x0 = links ? -TorTiefe : FeldLaenge;
        float y0 = (FeldBreite - TorBreite) / 2f;
        var ecke = P(x0, y0);
        DrawRect(new Rect2(ecke, P(x0 + TorTiefe, y0 + TorBreite) - ecke),
                 Linie, filled: false, width: dicke);
    }

    private void ZeichneEckbogen(float xMeter, float yMeter, float startWinkel)
    {
        DrawArc(P(xMeter, yMeter), M(EckenRadius), startWinkel, startWinkel + Mathf.Pi / 2f,
                12, Linie, 2f);
    }

    // ── Spieler und Ball ─────────────────────────────────────────────────────

    private void ZeichneSpieler(SzenenPlayer.Bild bild)
    {
        var font = GetThemeDefaultFont();
        const float kasten = 90f;

        for (int i = 0; i < bild.Spieler.Length && i < _aufstellung.Count; i++)
        {
            Vector2 ort = bild.Spieler[i];
            // Wer die Rote gesehen hat, steht weit außerhalb - der wird nicht gezeichnet.
            if (ort.Y < -3f || ort.Y > FeldBreite + 3f) continue;

            SpielfeldSpieler wer = _aufstellung[i];
            Vector2 mitte = P(ort.X, ort.Y);

            Color farbe = wer.Torwart ? TorwartFarbe : wer.Heim ? HeimFarbe : GastFarbe;
            DrawCircle(mitte, MarkerRadius, farbe);

            // Wer den Ball führt, bekommt einen hellen Ring.
            if (i == bild.Besitzer)
            {
                DrawArc(mitte, MarkerRadius + 3f, 0f, Mathf.Tau, 24, BallFarbe, 2.5f);
            }

            DrawString(font, mitte + new Vector2(-kasten / 2f, MarkerRadius + 14f), wer.Nachname,
                       HorizontalAlignment.Center, kasten, 11, FmTheme.TextPrimary);
        }
    }

    /// <summary>
    /// Der Ball - mit Schatten, solange er fliegt.
    ///
    /// <para>Ohne den Schatten wäre ein hoher Ball von einem flachen nicht zu unterscheiden: Auf
    /// einem Bild von oben sieht beides gleich aus. Der Ball wächst mit der Höhe, sein Schatten
    /// bleibt am Boden liegen.</para>
    /// </summary>
    private void ZeichneBall(SzenenPlayer.Bild bild)
    {
        Vector2 boden = P(bild.Ball.X, bild.Ball.Y);
        bool fliegt = bild.BallHoehe > 0.05;

        if (fliegt)
        {
            DrawCircle(boden, BallRadius * 0.8f, new Color(0f, 0f, 0f, 0.35f));
        }

        float hoehe = (float)bild.BallHoehe;
        Vector2 mitte = fliegt ? boden - new Vector2(0, hoehe * 3f) : boden;
        DrawCircle(mitte, BallRadius * (1f + hoehe * HoehenZoom), BallFarbe);
    }
}
