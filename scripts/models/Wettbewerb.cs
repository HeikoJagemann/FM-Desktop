#nullable enable
using System;

namespace FMDesktop.Models;

/// <summary>Ein Spiel eines K.-o.-Duells (GET wettbewerb/{wettbewerb}).</summary>
public class SpielKurz
{
    public long      Id            { get; set; }
    public DateOnly? Datum         { get; set; }
    public long      HeimId        { get; set; }
    public int?      HeimTore      { get; set; }
    public int?      GastTore      { get; set; }
    public bool      Gespielt      { get; set; }
    public bool      Verlaengerung { get; set; }
    public int?      ElfmeterHeim  { get; set; }
    public int?      ElfmeterGast  { get; set; }

    public string Text => !Gespielt ? (Datum.HasValue ? $"{Datum.Value:dd.MM.}" : "–")
        : ElfmeterHeim.HasValue ? $"{HeimTore}:{GastTore} i.E. {ElfmeterHeim}:{ElfmeterGast}"
        : Verlaengerung ? $"{HeimTore}:{GastTore} n.V."
        : $"{HeimTore}:{GastTore}";
}

/// <summary>Ein K.-o.-Duell: ein Spiel oder Hin- und Rückspiel.</summary>
public class PaarungModel
{
    public long       Id         { get; set; }
    public string     Wettbewerb { get; set; } = "";
    public string     Runde      { get; set; } = "";
    public string?    Pfad       { get; set; }
    public long?      VereinAId  { get; set; }
    public string?    VereinA    { get; set; }
    public string?    LandA      { get; set; }
    public long?      VereinBId  { get; set; }
    public string?    VereinB    { get; set; }
    public string?    LandB      { get; set; }
    public long?      SiegerId   { get; set; }
    public SpielKurz? Hinspiel   { get; set; }
    public SpielKurz? Rueckspiel { get; set; }

    public bool Freilos => VereinBId == null;

    /// <summary>Die Runde ohne Zusatz - "Achtelfinale" statt "Achtelfinale, Freilos".</summary>
    public string Stufe => Runde.Split(',')[0];

    /// <summary>Gruppe in der Liste: Runde, im Europapokal samt Weg.</summary>
    public string Gruppe => string.IsNullOrEmpty(Pfad) || Pfad.Length <= 3 || Pfad.EndsWith("*")
        ? Stufe : $"{Stufe} ({Pfad})";

    public string Sieger => SiegerId == null ? "" : SiegerId == VereinAId ? VereinA ?? "" : VereinB ?? "";

    public bool Betrifft(long vereinId) => VereinAId == vereinId || VereinBId == vereinId;
}

/// <summary>Ein Eintrag der UEFA-Fünfjahreswertung.</summary>
public class VerbandModel
{
    public int    Rang   { get; set; }
    public string Code   { get; set; } = "";
    public string Land   { get; set; } = "";
    public double Punkte { get; set; }
}
