#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FMDesktop.Models;

/// <summary>Der Stab des Vereins (GET mitarbeiter/{vereinId}).</summary>
public class StabModel
{
    public List<StabAbteilung> Abteilungen     { get; set; } = new();

    /// <summary>Faktor auf den Trainingsertrag; 1,0 entspricht einem Verein ohne erfassten Stab.</summary>
    public double              Trainingsfaktor { get; set; } = 1.0;

    /// <summary>Zusätzliche Frischepunkte je Woche, auch negativ.</summary>
    public double              Erholungsbonus  { get; set; }
    public int                 MaxJeRolle      { get; set; }
    public int                 AbfindungWochen { get; set; }

    /// <summary>Was der Stab je Woche kostet.</summary>
    public long Wochenkosten => Abteilungen.Sum(a => a.Mitarbeiter.Sum(m => (long)m.Gehalt));
}

/// <summary>Eine Rolle im Stab: wer sie besetzt und wer sich in diesem Monat bewirbt.</summary>
public class StabAbteilung
{
    /// <summary>CO_TRAINER, PHYSIOTHERAPEUT, JUGENDSCOUT oder JUGENDTRAINER.</summary>
    public string              Rolle       { get; set; } = "";
    public string              Anzeige     { get; set; } = "";
    public List<StabPerson>    Mitarbeiter { get; set; } = new();
    public List<ScoutBewerber> Bewerber    { get; set; } = new();
    /// <summary>Wie viele der Verein höchstens beschäftigt.</summary>
    public int                 Max         { get; set; }
    /// <summary>Bei Jugendtrainern die Jugendkader ohne Trainer ("JugendA" …).</summary>
    public List<string>        FreieKader  { get; set; } = new();
}

public class StabPerson
{
    public long      Id          { get; set; }
    public string    Name        { get; set; } = "";
    public int       Qualitaet   { get; set; }
    public int       Gehalt      { get; set; }
    public DateOnly? Eingestellt { get; set; }
    /// <summary>Der Jugendkader eines Jugendtrainers, sonst leer.</summary>
    public string?   Kader       { get; set; }
    /// <summary>Bis wann er auf Lehrgang ist - so lange fehlt er.</summary>
    public DateOnly? FortbildungBis { get; set; }
    public long      FortbildungKosten { get; set; }
    /// <summary>Um wie viel ein Lehrgang ihn besser machen würde.</summary>
    public int       FortbildungPlus { get; set; }
    /// <summary>Warum gerade kein Lehrgang geht, sonst leer.</summary>
    public string?   FortbildungHindernis { get; set; }

    public bool AufLehrgang => FortbildungBis != null;
}
