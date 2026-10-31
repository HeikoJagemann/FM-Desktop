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
    /// <summary>CO_TRAINER oder PHYSIOTHERAPEUT.</summary>
    public string              Rolle       { get; set; } = "";
    public string              Anzeige     { get; set; } = "";
    public List<StabPerson>    Mitarbeiter { get; set; } = new();
    public List<ScoutBewerber> Bewerber    { get; set; } = new();
}

public class StabPerson
{
    public long      Id          { get; set; }
    public string    Name        { get; set; } = "";
    public int       Qualitaet   { get; set; }
    public int       Gehalt      { get; set; }
    public DateOnly? Eingestellt { get; set; }
}
