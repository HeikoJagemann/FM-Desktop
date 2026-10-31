#nullable enable
using Godot;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Die Eigenschaften des gewählten fremden Spielers neben der Liste: Stärke, Talent, Scoutingstand
/// und jede Fähigkeit als Spanne auf einem Balken - so weit die Scouts sie aufgedeckt haben.
///
/// <para>Vorher standen die Fähigkeiten nur im Scoutbericht, den man per Doppelklick öffnen musste;
/// beim Durchsehen einer Liste sah man nur die Gesamtstärke.</para>
/// </summary>
public partial class EigenschaftenTafel : PanelContainer
{
    private VBoxContainer _inhalt = null!;
    private long _angefragt;

    public const float Breite = 320f;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Breite, 0);
        AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 16, 14);
        AddChild(rand);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        rand.AddChild(scroll);
        _inhalt = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inhalt.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_inhalt);
        Leer();
    }

    private void Leer()
    {
        Leere();
        _inhalt.AddChild(FmTheme.MakeAbschnitt("Eigenschaften", 18));
        var hinweis = FmTheme.MakeLabel("Einen Spieler anklicken - hier steht, was über ihn bekannt ist.",
            12, FmTheme.TextSecondary);
        hinweis.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _inhalt.AddChild(hinweis);
    }

    private void Leere()
    {
        foreach (var kind in _inhalt.GetChildren()) kind.QueueFree();
    }

    /// <summary>Zeigt einen Spieler; eine ältere, noch laufende Anfrage wird verworfen.</summary>
    public async Task Zeige(TransferSpieler spieler)
    {
        long anfrage = ++_angefragt;
        var bericht = await ApiClient.GetAsync<Scoutbericht>($"scouting/spieler/{spieler.SpielerId}");
        if (!IsInstanceValid(this) || anfrage != _angefragt) return;
        Leere();
        if (bericht == null)
        {
            _inhalt.AddChild(FmTheme.MakeLabel("Keine Angaben.", 12, FmTheme.TextSecondary));
            return;
        }

        var name = FmTheme.MakeAbschnitt(spieler.Name, 18);
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _inhalt.AddChild(name);
        _inhalt.AddChild(FmTheme.MakeLabel($"{spieler.Position} · {spieler.Alter} Jahre · {spieler.Verein}",
            12, FmTheme.TextSecondary));

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 18);
        kopf.AddChild(Kennzahl("STÄRKE", TransferSpieler.Spanne(bericht.StaerkeVon, bericht.StaerkeBis)));
        kopf.AddChild(Kennzahl("TALENT", bericht.TalentBis == 0 ? "?"
            : TransferSpieler.SterneSpanne(bericht.TalentVon, bericht.TalentBis)));
        _inhalt.AddChild(kopf);

        var stand = FmTheme.MakeLabel(bericht.Genau
                ? "Alles bekannt."
                : bericht.Beobachtungen == 0
                    ? "Noch nicht beobachtet - die Fähigkeiten deckt erst ein Scout auf."
                    : $"Gescoutet zu {bericht.Kenntnis} % - {bericht.Aufgedeckt} von {bericht.Attribute.Count} Fähigkeiten aufgedeckt.",
            12, FmTheme.TextSecondary);
        stand.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _inhalt.AddChild(stand);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        _inhalt.AddChild(sep);

        foreach (var a in bericht.Attribute)
            _inhalt.AddChild(Zeile(a));
    }

    private static Control Kennzahl(string titel, string wert)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", -2);
        box.AddChild(FmTheme.MakeLabel(titel, 10, FmTheme.TextSecondary));
        box.AddChild(FmTheme.MakeZahl(wert, 22));
        return box;
    }

    private static Control Zeile(Scoutbericht.Attributwert a)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);
        var name = FmTheme.MakeLabel(a.Name, 12, a.Bekannt ? FmTheme.TextPrimary : FmTheme.TextGedimmt);
        name.CustomMinimumSize = new Vector2(124, 0);
        name.ClipText = true;
        zeile.AddChild(name);

        var balken = new Spannenbalken
        {
            Von = a.Von, Bis = a.Bis, Bekannt = a.Bekannt,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 8),
        };
        zeile.AddChild(balken);

        var wert = FmTheme.MakeZahl(a.Bekannt ? TransferSpieler.Spanne(a.Von, a.Bis) : "?", 14,
            a.Bekannt ? FmTheme.TextPrimary : FmTheme.TextGedimmt);
        wert.HorizontalAlignment = HorizontalAlignment.Right;
        wert.CustomMinimumSize = new Vector2(46, 0);
        zeile.AddChild(wert);
        return zeile;
    }
}

/// <summary>Ein Balken von 0 bis 100, auf dem eine Spanne markiert ist.</summary>
public partial class Spannenbalken : Control
{
    public int Von { get; set; }
    public int Bis { get; set; }
    public bool Bekannt { get; set; }

    public override void _Draw()
    {
        var hinten = new Rect2(0, 0, Size.X, Size.Y);
        DrawStyleBox(FmTheme.Flaeche(FmTheme.BgHover, 4), hinten);
        if (!Bekannt) return;
        float von = Size.X * Mathf.Clamp(Von, 0, 100) / 100f;
        float bis = Size.X * Mathf.Clamp(Bis, 0, 100) / 100f;
        int mitte = (Von + Bis) / 2;
        var farbe = mitte >= 80 ? FmTheme.Gold : mitte >= 60 ? FmTheme.Success : mitte >= 40 ? FmTheme.TextNav : FmTheme.TextGedimmt;
        // Bis zum unteren Rand sicher, die Spanne heller darüber.
        DrawStyleBox(FmTheme.Flaeche(new Color(farbe, 0.55f), 4), new Rect2(0, 0, Mathf.Max(von, 4), Size.Y));
        DrawStyleBox(FmTheme.Flaeche(farbe, 4), new Rect2(von, 0, Mathf.Max(bis - von, 3), Size.Y));
    }
}
