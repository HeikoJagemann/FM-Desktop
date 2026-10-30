#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Der Scoutbericht über einen fremden Spieler: wie weit er gescoutet ist, Stärke und Talent als
/// Spanne und die einzelnen Fähigkeiten, soweit die Scouts sie aufgedeckt haben.
///
/// <para>Ein schwacher Scout deckt auch bei vollem Fortschritt nicht alles auf, und was er
/// aufdeckt, steht als breite Spanne da - der Bericht sagt, dass ein besserer Scout mehr sähe.</para>
/// </summary>
public partial class ScoutberichtDialog : Overlaykarte
{
    private TransferSpieler _spieler = null!;
    private bool _fensterOffen;
    private Action<string>? _meldung;
    private VBoxContainer _inhalt = null!;

    public ScoutberichtDialog() : base("Scoutbericht") { }

    public static void Zeige(Node caller, TransferSpieler spieler, bool fensterOffen, Action<string> meldung)
    {
        Oeffne(caller, new ScoutberichtDialog { _spieler = spieler, _fensterOffen = fensterOffen, _meldung = meldung });
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        var s = _spieler;
        inhalt.AddChild(FmTheme.MakeLabel($"{s.Name}  ·  {s.Position}, {s.Alter} Jahre", 16, FmTheme.TextPrimary));
        inhalt.AddChild(FmTheme.MakeLabel($"{s.Verein} ({s.Liga})", 13, FmTheme.TextSecondary));

        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(FmTheme.MakeLabel("Lade Bericht …", 13, FmTheme.TextSecondary));
        inhalt.AddChild(_inhalt);

        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 10);
        leiste.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var beobachten = new Button { Text = "Beobachten lassen …" };
        FmTheme.ApplySecondaryButton(beobachten);
        beobachten.Pressed += async () =>
        {
            await TransferAktionen.BeobachtenLassen(this, s.SpielerId, s.Name, text => _meldung?.Invoke(text));
        };
        leiste.AddChild(beobachten);
        var bieten = new Button { Text = "Angebot abgeben …", Disabled = !_fensterOffen };
        if (!_fensterOffen) bieten.TooltipText = "Das Transferfenster ist geschlossen.";
        FmTheme.ApplyPrimaryButton(bieten);
        bieten.Pressed += () =>
        {
            var caller = GetParent();
            Schliessen();
            if (caller != null && _meldung != null) BietenDialog.Zeige(caller, s, _meldung);
        };
        leiste.AddChild(bieten);
        inhalt.AddChild(leiste);

        _ = Lade();
    }

    private async System.Threading.Tasks.Task Lade()
    {
        var bericht = await ApiClient.GetAsync<Scoutbericht>($"scouting/spieler/{_spieler.SpielerId}");
        if (!IsInstanceValid(this)) return;
        foreach (var kind in _inhalt.GetChildren()) kind.QueueFree();
        if (bericht == null)
        {
            _inhalt.AddChild(FmTheme.MakeLabel("Der Bericht konnte nicht geladen werden.", 13, FmTheme.Danger));
            return;
        }

        _inhalt.AddChild(Zeile("Stärke", FmTheme.MakeLabel(TransferSpieler.Spanne(bericht.StaerkeVon, bericht.StaerkeBis), 14, FmTheme.TextPrimary)));
        _inhalt.AddChild(Zeile("Talent", FmTheme.MakeLabel(bericht.TalentBis == 0 ? "?"
            : TransferSpieler.Spanne(bericht.TalentVon, bericht.TalentBis), 14, FmTheme.TextPrimary)));

        var fortschritt = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = bericht.Kenntnis, ShowPercentage = true,
            CustomMinimumSize = new Vector2(220, 18),
        };
        fortschritt.AddThemeStyleboxOverride("fill", FmTheme.KonditionsBalken(
            bericht.Kenntnis >= 100 ? FmTheme.Success : FmTheme.Accent));
        fortschritt.AddThemeStyleboxOverride("background", FmTheme.KonditionsBalken(FmTheme.Border));
        _inhalt.AddChild(Zeile("Scouting", fortschritt));

        var erklaerung = FmTheme.MakeLabel(Erklaerung(bericht), 12, FmTheme.TextGedimmt);
        erklaerung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        erklaerung.CustomMinimumSize = new Vector2(420, 0);
        _inhalt.AddChild(erklaerung);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        _inhalt.AddChild(sep);
        _inhalt.AddChild(FmTheme.MakeLabel(
            $"Fähigkeiten - {bericht.Aufgedeckt} von {bericht.Attribute.Count} aufgedeckt", 13, FmTheme.TextSecondary));

        // Zwei Spalten, damit der Bericht nicht über den Bildschirm hinauswächst.
        var raster = new GridContainer { Columns = 4 };
        raster.AddThemeConstantOverride("h_separation", 16);
        raster.AddThemeConstantOverride("v_separation", 4);
        foreach (var a in bericht.Attribute)
        {
            var name = FmTheme.MakeLabel(a.Name, 13, a.Bekannt ? FmTheme.TextSecondary : FmTheme.TextGedimmt);
            name.CustomMinimumSize = new Vector2(150, 0);
            raster.AddChild(name);
            var wert = FmTheme.MakeLabel(a.Bekannt ? TransferSpieler.Spanne(a.Von, a.Bis) : "?", 13,
                a.Bekannt ? FmTheme.TextPrimary : FmTheme.TextGedimmt);
            wert.CustomMinimumSize = new Vector2(60, 0);
            raster.AddChild(wert);
        }
        _inhalt.AddChild(raster);
    }

    private static string Erklaerung(Scoutbericht b)
    {
        if (b.Genau) return "Ein eigener Spieler - alles ist bekannt.";
        if (b.Beobachtungen == 0)
            return "Noch nicht beobachtet - nur der öffentliche Eindruck. Ein Scout deckt Talent und Fähigkeiten auf.";
        string stand = $"{b.Beobachtungen} Beobachtung(en), zuletzt {b.Zuletzt:dd.MM.yyyy}. "
                       + $"Bester Beobachter: Qualität {b.Beobachter}.";
        return b.Kenntnis >= 100
            ? stand + " Mehr findet er nicht heraus - genauer und vollständiger wird der Bericht nur mit einem besseren Scout."
            : stand + " Mit weiteren Beobachtungen wird der Bericht genauer und deckt mehr Fähigkeiten auf.";
    }
}

/// <summary>Der Scoutbericht über einen Spieler (GET scouting/spieler/{id}).</summary>
public class Scoutbericht
{
    public long      SpielerId     { get; set; }
    public bool      Genau         { get; set; }
    public int       StaerkeVon    { get; set; }
    public int       StaerkeBis    { get; set; }
    public int       TalentVon     { get; set; }
    public int       TalentBis     { get; set; }
    public int       Kenntnis      { get; set; }
    public int       Beobachtungen { get; set; }
    public DateOnly? Zuletzt       { get; set; }
    public int       Beobachter    { get; set; }
    public int       Aufgedeckt    { get; set; }
    public List<Attributwert> Attribute { get; set; } = new();

    public class Attributwert
    {
        public string Name    { get; set; } = "";
        public bool   Bekannt { get; set; }
        public int    Von     { get; set; }
        public int    Bis     { get; set; }
    }
}
