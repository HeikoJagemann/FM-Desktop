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

    public string Ergebnis => Gespielt
        ? $"{HeimTore}:{GastTore}"
        : "–:–";
}
