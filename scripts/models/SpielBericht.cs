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

    public string Ergebnis => Gespielt ? $"{HeimTore} : {GastTore}" : "– : –";
}
