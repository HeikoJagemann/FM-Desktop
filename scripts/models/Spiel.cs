#nullable enable
using System;
namespace FMDesktop.Models;

public class Spiel
{
    public long    Id          { get; set; }
    public int     Spieltag    { get; set; }
    public int     Saison      { get; set; }
    public Verein? HeimVerein  { get; set; }
    public Verein? GastVerein  { get; set; }
    public int?    HeimTore    { get; set; }
    public int?    GastTore    { get; set; }
    public bool    Gespielt    { get; set; }

    /// <summary>"LIGA", "DFB_POKAL", "CHAMPIONS_LEAGUE" …</summary>
    public string  Wettbewerb  { get; set; } = "LIGA";

    /// <summary>Runde ausserhalb der Liga, etwa "Achtelfinale, Hinspiel".</summary>
    public string? Runde       { get; set; }
    public bool    Verlaengerung { get; set; }
    public int?    ElfmeterHeim  { get; set; }
    public int?    ElfmeterGast  { get; set; }

    public bool IstLigaspiel => Wettbewerb == "LIGA";

    /// <summary>"12. Spieltag" oder "DFB-Pokal, Achtelfinale".</summary>
    public string Bezeichnung => IstLigaspiel
        ? $"{Spieltag}. Spieltag"
        : $"{WettbewerbAnzeige(Wettbewerb)}, {Runde}";

    public static string WettbewerbAnzeige(string wettbewerb) => wettbewerb switch
    {
        "DFB_POKAL"         => "DFB-Pokal",
        "CHAMPIONS_LEAGUE"  => "Champions League",
        "EUROPA_LEAGUE"     => "Europa League",
        "CONFERENCE_LEAGUE" => "Conference League",
        "RELEGATION"        => "Relegation",
        "AUFSTIEGSSPIEL"    => "Aufstiegsspiel",
        _                   => "Liga",
    };

    /// <summary>Termin der Partie; der Kalender steuert seit der Zeitachse den Fortschritt.</summary>
    public DateOnly?  Datum   { get; set; }
    public TimeOnly?  Uhrzeit { get; set; }

    /// <summary>Kurzform für die Spielplanliste, etwa "Sa 10.08. 15:30".</summary>
    public string TerminText
    {
        get
        {
            if (!Datum.HasValue) return "";
            string tag = System.Globalization.CultureInfo.GetCultureInfo("de-DE")
                .DateTimeFormat.GetAbbreviatedDayName(Datum.Value.DayOfWeek);
            string zeit = Uhrzeit.HasValue ? " " + Uhrzeit.Value.ToString("HH:mm") : "";
            return $"{tag} {Datum.Value:dd.MM.}{zeit}";
        }
    }

    public string Ergebnis => !Gespielt ? "–:–"
        : ElfmeterHeim.HasValue ? $"{HeimTore}:{GastTore} i.E. {ElfmeterHeim}:{ElfmeterGast}"
        : Verlaengerung ? $"{HeimTore}:{GastTore} n.V."
        : $"{HeimTore}:{GastTore}";
}
