#nullable enable
using System;
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Die Startseite des Managers (GET dashboard/{vereinId}).</summary>
public class DashboardModel
{
    public DateOnly                Datum            { get; set; }
    public string                  Phase            { get; set; } = "";
    public string                  Verein           { get; set; } = "";
    public long?                   LigaId           { get; set; }
    public string                  Liga             { get; set; } = "";
    public long                    Kontostand       { get; set; }
    public List<Meldung>           Meldungen        { get; set; } = new();
    public List<TabellenEintrag>   Tabelle          { get; set; } = new();
    public int                     LetzterSpieltag  { get; set; }
    public List<Ergebnis>          LetzteErgebnisse { get; set; } = new();
    public DateOnly?               Trainingswoche   { get; set; }
    public List<Trainingsfortschritt> TrainingWoche  { get; set; } = new();
    public List<Trainingsfortschritt> TrainingSaison { get; set; } = new();
    public NaechstesSpiel?         NaechstesSpiel   { get; set; }
}

/// <summary>Ein Ereignis für den Manager - neu heißt: seit dem letzten Besuch des Dashboards.</summary>
public class Meldung
{
    public long     Id    { get; set; }
    public DateOnly Datum { get; set; }
    public string   Art   { get; set; } = "";
    public string   Titel { get; set; } = "";
    public string   Text  { get; set; } = "";
    public bool     Neu   { get; set; }

    /// <summary>Wohin ein Klick führt, wenn nicht in den Bereich der Rubrik - etwa "SCOUTWOCHE".</summary>
    public string?  Ziel  { get; set; }

    /// <summary>Die Rubrik als Wort - steht als kleines Schild vor der Meldung.</summary>
    public string Rubrik => Art switch
    {
        "TRANSFER" => "Transfer",
        "SCOUTING" => "Scouting",
        "FINANZEN" => "Finanzen",
        "SAISON"   => "Saison",
        "JUGEND"   => "Jugend",
        "UMFELD"   => "Umfeld",
        _          => "",
    };
}

/// <summary>Ein Ergebnis des letzten Spieltags.</summary>
public class Ergebnis
{
    public long   SpielId  { get; set; }
    public long?  HeimId   { get; set; }
    public string Heim     { get; set; } = "";
    public long?  GastId   { get; set; }
    public string Gast     { get; set; } = "";
    public int?   HeimTore { get; set; }
    public int?   GastTore { get; set; }
    public bool   Eigenes  { get; set; }

    public string ToreText => HeimTore == null || GastTore == null ? "-:-" : $"{HeimTore}:{GastTore}";
}

/// <summary>Wie weit ein eigener Spieler in einem Zeitraum gekommen ist.</summary>
public class Trainingsfortschritt
{
    public long   SpielerId     { get; set; }
    public string Name          { get; set; } = "";
    public string Position      { get; set; } = "";
    public int    Alter         { get; set; }
    /// <summary>Fähigkeitspunkte aus Training und Spielpraxis.</summary>
    public int    Punkte        { get; set; }
    public int    StaerkeVorher { get; set; }
    public int    StaerkeJetzt  { get; set; }
}

/// <summary>
/// Die nächste eigene Partie: die eigene Elf genau, der Gegner als Spanne aus den Scoutberichten.
/// </summary>
public class NaechstesSpiel
{
    public long      SpielId          { get; set; }
    public DateOnly? Datum            { get; set; }
    public TimeOnly? Uhrzeit          { get; set; }
    public int       Spieltag         { get; set; }
    public string?   Bezeichnung      { get; set; }
    public long?     HeimId           { get; set; }
    public string    Heim             { get; set; } = "";
    public long?     GastId           { get; set; }
    public string    Gast             { get; set; } = "";
    public bool      Heimspiel        { get; set; }
    public int?      EigenerPlatz     { get; set; }
    public int?      GegnerPlatz      { get; set; }
    public double    EigeneStaerke    { get; set; }
    public double    GegnerStaerkeVon { get; set; }
    public double    GegnerStaerkeBis { get; set; }
    public int       GegnerBeobachtet { get; set; }
    public int       GegnerSpieler    { get; set; }
}
