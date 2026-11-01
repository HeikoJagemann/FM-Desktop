#nullable enable
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Wettbewerbe;

/// <summary>
/// Die UEFA-Fünfjahreswertung der Verbände: Aus ihr folgt, wie viele Startplätze ein Land in der
/// kommenden Saison bekommt und wo seine Vereine einsteigen.
/// </summary>
public partial class UefaWertungView : Control
{
    private static readonly GridSpalte<VerbandModel>[] Spalten =
    {
        new() { Titel = "#", Breite = 50, Ausrichtung = HorizontalAlignment.Center, Text = v => v.Rang.ToString(), Sortierung = v => v.Rang },
        new() { Titel = "Verband", Breite = 220, Expand = true, Text = v => v.Land,
                Farbe = v => v.Code == "GER" ? FmTheme.Accent : FmTheme.TextPrimary },
        new() { Titel = "Punkte", Breite = 100, Ausrichtung = HorizontalAlignment.Right, Text = v => v.Punkte.ToString("0.000"),
                Sortierung = v => v.Punkte },
        new() { Titel = "Champions League", Breite = 260, Text = v => Plaetze(v.Rang), Farbe = _ => FmTheme.TextSecondary },
    };

    /// <summary>Wie viele Vereine direkt in der Ligaphase der Champions League stehen.</summary>
    private static string Plaetze(int rang) => rang switch
    {
        <= 4  => "4 direkt",
        5     => "3 direkt, 1 Qualifikation",
        6     => "2 direkt, 1 Qualifikation",
        <= 10 => "Meister direkt, 1 Qualifikation",
        <= 14 => "Meister in die Play-offs",
        _     => "Meister in die Qualifikation",
    };

    private Label _status = null!;
    private FmGrid<VerbandModel> _grid = null!;

    public override async void _Ready()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);
        vbox.AddChild(FmTheme.MakeTitel("UEFA-Fünfjahreswertung"));
        _status = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_status);
        _grid = new FmGrid<VerbandModel>(Spalten) { SizeFlagsVertical = SizeFlags.ExpandFill };
        vbox.AddChild(_grid);
        await Lade();
    }

    private async Task Lade()
    {
        var liste = await ApiClient.GetAsync<List<VerbandModel>>("wettbewerb/koeffizienten");
        if (!IsInstanceValid(this)) return;
        if (liste == null) { _status.Text = "Fehler beim Laden."; return; }
        _status.Text = "Summe der letzten fünf Spielzeiten - sie entscheidet über die Startplätze der laufenden Saison.";
        _grid.Zeige(liste);
    }
}
