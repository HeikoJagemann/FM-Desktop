#nullable enable
using System.Collections.Generic;
using Godot;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Wettbewerbe;

/// <summary>Spaltenkatalog der K.-o.-Duelle: wer gegen wen, Hin- und Rückspiel, wer weiterkommt.</summary>
public static class PaarungSpalten
{
    private static Color Farbe(PaarungModel p) =>
        p.Betrifft(GameState.Instance.VereinId) ? FmTheme.Accent : FmTheme.TextPrimary;

    private static string MitLand(string? name, string? land) =>
        name == null ? "Freilos" : land == null || land == "GER" ? name : $"{name} ({land})";

    public static GridSpalte<PaarungModel> VereinA => new()
    {
        Titel = "Heim", Breite = 200, Expand = true, Ausrichtung = HorizontalAlignment.Right,
        Text  = p => MitLand(p.VereinA, p.LandA),
        Farbe = Farbe,
    };

    public static GridSpalte<PaarungModel> Hinspiel => new()
    {
        Titel = "Spiel", Breite = 130, Ausrichtung = HorizontalAlignment.Center,
        Text    = p => p.Freilos ? "" : p.Hinspiel?.Text ?? "",
        Farbe   = Farbe,
        Tooltip = p => p.Rueckspiel == null ? "Ein Spiel - bei Gleichstand Verlängerung und Elfmeterschießen."
                                             : "Hinspiel",
    };

    public static GridSpalte<PaarungModel> Rueckspiel => new()
    {
        Titel = "Rückspiel", Breite = 130, Ausrichtung = HorizontalAlignment.Center,
        Text    = p => p.Rueckspiel == null ? "" : InSicht(p),
        Farbe   = Farbe,
        Tooltip = _ => "Aus Sicht der Heimmannschaft des Hinspiels.",
    };

    /// <summary>Das Rückspiel aus Sicht des Hinspiel-Gastgebers - so liest sich das Gesamtergebnis.</summary>
    private static string InSicht(PaarungModel p)
    {
        var r = p.Rueckspiel!;
        if (!r.Gespielt) return r.Text;
        string grund = $"{r.GastTore}:{r.HeimTore}";
        if (r.ElfmeterHeim.HasValue) return $"{grund} i.E. {r.ElfmeterGast}:{r.ElfmeterHeim}";
        return r.Verlaengerung ? $"{grund} n.V." : grund;
    }

    public static GridSpalte<PaarungModel> VereinB => new()
    {
        Titel = "Gast", Breite = 200, Expand = true,
        Text  = p => p.Freilos ? "" : MitLand(p.VereinB, p.LandB),
        Farbe = Farbe,
    };

    public static GridSpalte<PaarungModel> Weiter => new()
    {
        Titel = "Weiter", Breite = 180,
        Text  = p => p.Sieger,
        Farbe = p => p.SiegerId == GameState.Instance.VereinId ? FmTheme.Success
                   : p.Betrifft(GameState.Instance.VereinId) && p.SiegerId != null ? FmTheme.Danger
                   : FmTheme.TextSecondary,
    };

    public static IEnumerable<GridSpalte<PaarungModel>> Liste => new[] { VereinA, Hinspiel, Rueckspiel, VereinB, Weiter };
}
