#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Ergebnis und Verlauf eines Spiels (GET spiel/{id}/bericht).</summary>
public class SpielBericht
{
    public long    SpielId      { get; set; }
    public int     Saison       { get; set; }
    public int     Spieltag     { get; set; }
    public long?   HeimVereinId { get; set; }
    public string? HeimVerein   { get; set; }
    public long?   GastVereinId { get; set; }
    public string? GastVerein   { get; set; }
    public int?    HeimTore     { get; set; }
    public int?    GastTore     { get; set; }
    public bool    Gespielt     { get; set; }

    public List<SpielEreignis> Ereignisse { get; set; } = new();

    /// <summary>Aufstellung der Heimmannschaft, Startelf zuerst.</summary>
    public List<AufstellungsSpieler> HeimAufstellung { get; set; } = new();
    public List<AufstellungsSpieler> GastAufstellung { get; set; } = new();

    /// <summary>Durchschnittliche Stärke der Startelf.</summary>
    public double HeimStaerke { get; set; }
    public double GastStaerke { get; set; }

    public string Ergebnis => Gespielt ? $"{HeimTore} : {GastTore}" : "– : –";
}
