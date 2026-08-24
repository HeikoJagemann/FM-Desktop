#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Ein wählbarer Wert für die Oberfläche - Schlüssel, Anzeigetext und Erläuterung.</summary>
public class KatalogEintrag
{
    public string Schluessel   { get; set; } = "";
    public string Anzeige      { get; set; } = "";
    public string Beschreibung { get; set; } = "";
}

/// <summary>Trainingsplan eines Vereins samt Auswahlkatalog aus dem Backend.</summary>
public class TrainingsplanModel
{
    public long    VereinId     { get; set; }
    public string  Intensitaet  { get; set; } = "MITTEL";
    public string? Schwerpunkt1 { get; set; }
    public string? Schwerpunkt2 { get; set; }
    public string? Schwerpunkt3 { get; set; }

    public List<KatalogEintrag> Bereiche      { get; set; } = new();
    public List<KatalogEintrag> Intensitaeten { get; set; } = new();
}

/// <summary>Was der Client beim Speichern schickt - ohne den Katalog.</summary>
public class TrainingsplanEingabe
{
    public string  Intensitaet  { get; set; } = "MITTEL";
    public string? Schwerpunkt1 { get; set; }
    public string? Schwerpunkt2 { get; set; }
    public string? Schwerpunkt3 { get; set; }
}

/// <summary>Veränderung einer einzelnen Fähigkeit.</summary>
public class AttributAenderung
{
    public string Attribut  { get; set; } = "";
    public string Anzeige   { get; set; } = "";
    public int    Alt       { get; set; }
    public int    Neu       { get; set; }
    /// <summary>Positiv bei Fortschritt, negativ bei altersbedingtem Abbau.</summary>
    public int    Differenz { get; set; }

    public string Pfeil => Differenz > 0 ? "▲" : "▼";

    /// <summary>Kurzform für die Kaderliste, etwa "▲2 Passspiel".</summary>
    public string Kurz => $"{Pfeil}{System.Math.Abs(Differenz)} {Anzeige}";
}

/// <summary>Was sich bei einem Spieler in einer Trainingswoche getan hat.</summary>
public class SpielerEntwicklung
{
    public long   SpielerId   { get; set; }
    public string SpielerName { get; set; } = "";
    public int    Saison      { get; set; }
    public int    Spieltag    { get; set; }
    public int    StaerkeAlt  { get; set; }
    public int    StaerkeNeu  { get; set; }
    public List<AttributAenderung> Aenderungen { get; set; } = new();

    public int StaerkeDifferenz => StaerkeNeu - StaerkeAlt;
}

/// <summary>Entwicklung eines Spielers über ein Fenster von Spieltagen.</summary>
public class SpielerVerlauf
{
    public long SpielerId   { get; set; }
    public int  Saison      { get; set; }
    public int  VonSpieltag { get; set; }
    public int  BisSpieltag { get; set; }
    public int  StaerkeAlt  { get; set; }
    public int  StaerkeNeu  { get; set; }
    public List<AttributAenderung> Attribute { get; set; } = new();

    public int StaerkeDifferenz => StaerkeNeu - StaerkeAlt;

    /// <summary>Veränderung einer Fähigkeit im Fenster, oder 0.</summary>
    public int DifferenzFuer(string attribut)
    {
        foreach (var a in Attribute)
            if (a.Attribut == attribut) return a.Differenz;
        return 0;
    }
}
