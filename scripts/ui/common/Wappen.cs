#nullable enable
using System.Linq;
using Godot;

namespace FMDesktop.UI;

/// <summary>
/// Platzhalter-Wappen: ein Kreis mit dem Kürzel des Vereins. Echte Wappen gibt es noch nicht;
/// das Kürzel macht den Verein trotzdem auf einen Blick erkennbar.
/// </summary>
public partial class Wappen : Control
{
    private string _kuerzel = "";
    private Color _rand = FmTheme.Accent;

    public static Wappen Neu(string vereinsname, float groesse = 44, Color? rand = null)
    {
        var w = new Wappen
        {
            CustomMinimumSize = new Vector2(groesse, groesse),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            _rand = rand ?? FmTheme.Accent,
        };
        w.Setze(vereinsname);
        return w;
    }

    public void Setze(string vereinsname)
    {
        _kuerzel = Kuerzel(vereinsname);
        TooltipText = vereinsname;
        QueueRedraw();
    }

    /// <summary>
    /// "SV Bergisch Gladbach 09" wird zu "SVB", "FC Iserlohn" zu "FCI", "Westfalia Rhynern" zu
    /// "WR": eine vorangestellte Abkürzung bleibt ganz, dazu der erste Buchstabe des Ortes.
    /// </summary>
    public static string Kuerzel(string name)
    {
        var woerter = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetter(w[0]))
            .ToList();
        if (woerter.Count == 0) return "";

        var erstes = woerter[0];
        bool abkuerzung = erstes.Length <= 4 && erstes.Count(char.IsUpper) >= 2;
        string kuerzel = abkuerzung
            ? erstes.ToUpperInvariant() + (woerter.Count > 1 ? woerter[1][0].ToString() : "")
            : string.Concat(woerter.Take(2).Select(w => w[0]));
        return kuerzel.ToUpperInvariant().Length > 4 ? kuerzel[..4].ToUpperInvariant() : kuerzel.ToUpperInvariant();
    }

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) / 2f;
        var mitte = Size / 2f;
        DrawCircle(mitte, r, new Color("1C2A24"));
        DrawArc(mitte, r - 1, 0, Mathf.Pi * 2, 48, _rand, 2, true);

        var schrift = FmTheme.SchriftZahl;
        int groesse = Mathf.RoundToInt(r * (_kuerzel.Length > 3 ? 0.58f : 0.70f));
        var breite = schrift.GetStringSize(_kuerzel, HorizontalAlignment.Left, -1, groesse);
        var grundlinie = mitte + new Vector2(-breite.X / 2f, schrift.GetAscent(groesse) / 2f - 1);
        DrawString(schrift, grundlinie, _kuerzel, HorizontalAlignment.Left, -1, groesse, _rand);
    }
}
