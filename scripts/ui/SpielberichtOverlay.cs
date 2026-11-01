#nullable enable
using Godot;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Der Bericht eines beliebigen Spiels: oben die Anzeigetafel, darunter als Reiter der Verlauf,
/// die Kennzahlen und die Noten beider Mannschaften - dieselben Bausteine wie nach dem Abpfiff
/// in der Spielansicht.
/// </summary>
public partial class SpielberichtOverlay : Overlaykarte
{
    private long _spielId;
    private int _startReiter;
    private VBoxContainer _inhalt = null!;

    public SpielberichtOverlay() : base("Spielbericht", 900) { }

    /// <param name="startReiter">0 Verlauf, 1 Statistik, 2 Noten.</param>
    public static void Zeige(Node caller, long spielId, int startReiter = 0)
    {
        Oeffne(caller, new SpielberichtOverlay { _spielId = spielId, _startReiter = startReiter },
            spielId.ToString());
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 16);
        _inhalt.AddChild(FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary));
        inhalt.AddChild(_inhalt);
        _ = LadeBericht();
    }

    private async Task LadeBericht()
    {
        var bericht = await ApiClient.GetAsync<SpielBericht>($"spiel/{_spielId}/bericht");
        if (!IsInstanceValid(this)) return;
        foreach (Node kind in _inhalt.GetChildren()) kind.QueueFree();

        if (bericht == null)
        {
            _inhalt.AddChild(FmTheme.MakeLabel("Der Spielbericht konnte nicht geladen werden.", 13, FmTheme.Danger));
            return;
        }

        SetzeUntertitel($"Saison {bericht.Saison}/{bericht.Saison + 1}  ·  "
                        + (string.IsNullOrEmpty(bericht.Bezeichnung) ? $"{bericht.Spieltag}. Spieltag" : bericht.Bezeichnung)
                        + (string.IsNullOrEmpty(bericht.Zusatz) ? "" : $"  ·  {bericht.Zusatz}"));
        _inhalt.AddChild(Anzeigetafel(bericht));

        if (!bericht.Gespielt)
        {
            _inhalt.AddChild(FmTheme.MakeLabel("Dieses Spiel wurde noch nicht ausgetragen.",
                13, FmTheme.TextSecondary, HorizontalAlignment.Center));
            return;
        }

        // So hoch, wie das Fenster erlaubt - im Vollbild stehen beide Notenlisten ohne Bildlauf da.
        float hoehe = Mathf.Clamp(GetViewportRect().Size.Y - 480, 260, 460);
        var reiter = new TabContainer();
        reiter.AddChild(Rollbereich("Verlauf", Verlauf(bericht), hoehe));
        reiter.AddChild(Rollbereich("Statistik", Statistik(bericht), hoehe));
        reiter.AddChild(Rollbereich("Noten", Noten(bericht), hoehe));
        _inhalt.AddChild(reiter);
        reiter.CurrentTab = Mathf.Clamp(_startReiter, 0, reiter.GetTabCount() - 1);
    }

    private static long EigenerVerein => GameState.Instance.VereinId;

    // ── Anzeigetafel ─────────────────────────────────────────────────────────

    /// <summary>Vereine mit Wappen und das Ergebnis in der Zahlenschrift - der eigene Verein mit Bernstein-Ring.</summary>
    private static Control Anzeigetafel(SpielBericht b)
    {
        var tafel = new PanelContainer();
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 12, FmTheme.Border);
        stil.ContentMarginTop = stil.ContentMarginBottom = 14;
        stil.ContentMarginLeft = stil.ContentMarginRight = 20;
        tafel.AddThemeStyleboxOverride("panel", stil);

        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 16);
        tafel.AddChild(reihe);

        var heim = FmTheme.MakeTitel(b.HeimVerein ?? "Heim", 24);
        heim.HorizontalAlignment = HorizontalAlignment.Right;
        heim.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heim.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        heim.ClipText = true;
        reihe.AddChild(heim);
        reihe.AddChild(Wappen.Neu(b.HeimVerein ?? "", 48,
            b.HeimVereinId == EigenerVerein ? FmTheme.Accent : FmTheme.BorderStark));

        var stand = FmTheme.MakeZahl(b.Gespielt ? $"{b.HeimTore} : {b.GastTore}" : "– : –", 44);
        stand.HorizontalAlignment = HorizontalAlignment.Center;
        stand.CustomMinimumSize = new Vector2(130, 0);
        reihe.AddChild(stand);

        reihe.AddChild(Wappen.Neu(b.GastVerein ?? "", 48,
            b.GastVereinId == EigenerVerein ? FmTheme.Accent : FmTheme.BorderStark));
        var gast = FmTheme.MakeTitel(b.GastVerein ?? "Gast", 24);
        gast.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        gast.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        gast.ClipText = true;
        reihe.AddChild(gast);

        return tafel;
    }

    /// <summary>Ein Reiter mit Bildlauf; der Name des Knotens ist die Reiterbeschriftung.</summary>
    private static Control Rollbereich(string titel, Control inhalt, float hoehe)
    {
        var scroll = new ScrollContainer
        {
            Name = titel,
            CustomMinimumSize = new Vector2(0, hoehe),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var rand = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        FmTheme.SetMargin(rand, 4, 14, 14, 4);
        rand.AddChild(inhalt);
        scroll.AddChild(rand);
        return scroll;
    }

    // ── Verlauf ──────────────────────────────────────────────────────────────

    private static Control Verlauf(SpielBericht b)
    {
        var liste = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        liste.AddThemeConstantOverride("separation", 8);

        if (b.Ereignisse.Count == 0)
        {
            liste.AddChild(FmTheme.MakeLabel("Ein ereignisloses Spiel – keine Tore, keine Karten, keine Wechsel.",
                13, FmTheme.TextSecondary));
            return liste;
        }

        // Bei Toren steht der Zwischenstand dabei - so liest sich der Verlauf wie eine Tafel.
        int heimTore = 0, gastTore = 0;
        foreach (var e in b.Ereignisse)
        {
            string? stand = null;
            if (e.IstTor)
            {
                if (e.VereinId == b.HeimVereinId) heimTore++; else gastTore++;
                stand = $"{heimTore} : {gastTore}";
            }
            liste.AddChild(Ereigniszeile(b, e, stand));
        }
        return liste;
    }

    private static Control Ereigniszeile(SpielBericht b, SpielEreignis e, string? stand)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 12);

        var minute = FmTheme.MakeZahl($"{e.Minute}'", 16, FmTheme.TextSecondary);
        minute.HorizontalAlignment = HorizontalAlignment.Right;
        minute.CustomMinimumSize = new Vector2(40, 0);
        zeile.AddChild(minute);

        var zeichen = Zeichen(e.Typ);
        zeile.AddChild(zeichen.HasValue
            ? Symbol.Neu(zeichen.Value, FmTheme.TextPrimary, 18)
            : new Control { CustomMinimumSize = new Vector2(18, 18) });

        bool heim = e.VereinId == b.HeimVereinId;
        zeile.AddChild(Wappen.Neu((heim ? b.HeimVerein : b.GastVerein) ?? "", 22,
            e.VereinId == EigenerVerein ? FmTheme.Accent : FmTheme.BorderStark));

        // Tore des eigenen Vereins in Bernstein, damit der Verlauf schnell lesbar ist.
        var farbe = e.IstTor
            ? (e.VereinId == EigenerVerein ? FmTheme.Accent : FmTheme.TextPrimary)
            : FmTheme.TextSecondary;
        var text = FmTheme.MakeLabel(e.Beschreibung ?? "", 13, farbe);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        zeile.AddChild(text);

        var zwischenstand = FmTheme.MakeZahl(stand ?? "", 16, FmTheme.TextPrimary);
        zwischenstand.HorizontalAlignment = HorizontalAlignment.Right;
        zwischenstand.CustomMinimumSize = new Vector2(52, 0);
        zeile.AddChild(zwischenstand);

        return zeile;
    }

    /// <summary>Das gezeichnete Zeichen eines Ereignisses; keins für Unbekanntes.</summary>
    private static Symbol.Art? Zeichen(string typ) => typ switch
    {
        "TOR"             => Symbol.Art.Ball,
        "GELBE_KARTE"     => Symbol.Art.Gelb,
        "GELB_ROTE_KARTE" => Symbol.Art.GelbRot,
        "ROTE_KARTE"      => Symbol.Art.Rot,
        "WECHSEL"         => Symbol.Art.Wechsel,
        _                 => null,
    };

    // ── Statistik und Noten ──────────────────────────────────────────────────

    private static Control Statistik(SpielBericht b)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12);

        if (b.HeimStatistik == null || b.GastStatistik == null)
        {
            box.AddChild(FmTheme.MakeLabel("Für dieses Spiel wurden noch keine Kennzahlen erfasst.",
                13, FmTheme.TextSecondary));
            return box;
        }

        var kopf = new HBoxContainer();
        var heim = FmTheme.MakeAbschnitt(b.HeimVerein ?? "Heim", 16);
        heim.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(heim);
        var gast = FmTheme.MakeAbschnitt(b.GastVerein ?? "Gast", 16);
        gast.HorizontalAlignment = HorizontalAlignment.Right;
        gast.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(gast);
        box.AddChild(kopf);

        var tafel = new StatistikTafel { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(tafel);
        tafel.Zeige(b.HeimStatistik, b.GastStatistik);
        return box;
    }

    private static Control Noten(SpielBericht b)
    {
        if (b.HeimAufstellung.Count == 0 && b.GastAufstellung.Count == 0)
        {
            return FmTheme.MakeLabel("Für dieses Spiel liegen keine Aufstellungen vor.",
                13, FmTheme.TextSecondary);
        }

        var noten = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        noten.AddThemeConstantOverride("separation", 14);
        noten.AddChild(Notenliste.Spalte(b.HeimVerein ?? "Heim", b.HeimAufstellung,
            b.HeimVereinId == EigenerVerein));
        noten.AddChild(Notenliste.Spalte(b.GastVerein ?? "Gast", b.GastAufstellung,
            b.GastVereinId == EigenerVerein));
        return noten;
    }
}
