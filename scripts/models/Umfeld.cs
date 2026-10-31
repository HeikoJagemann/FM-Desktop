#nullable enable
using System;
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Stadion, Trainings- und Jugendgelände des Vereins (GET umfeld/{vereinId}).</summary>
public class UmfeldModel
{
    public long             Kontostand        { get; set; }
    public int              Vereinsruf        { get; set; }
    public int              Jugendruf         { get; set; }
    public StadionUmfeld?   Stadion           { get; set; }
    public Gelaende?        Trainingsgelaende { get; set; }
    public Gelaende?        Jugendgelaende    { get; set; }
    public List<Bauprojekt> Projekte          { get; set; } = new();
}

public class StadionUmfeld
{
    public string               Name           { get; set; } = "";
    public int                  Kapazitaet     { get; set; }
    public int                  Ticketpreis    { get; set; }
    public long                 UnterhaltWoche { get; set; }
    public List<StadionAusbau>  Ausbau         { get; set; } = new();
    public bool                 ImBau          { get; set; }
}

public class StadionAusbau
{
    public int  Plaetze        { get; set; }
    public int  NeueKapazitaet { get; set; }
    public long Kosten         { get; set; }
    public int  Wochen         { get; set; }
}

public class Gelaende
{
    public string         Art             { get; set; } = "";
    public string         Titel           { get; set; } = "";
    public int            Stufe           { get; set; }
    public int            MaxStufe        { get; set; } = 5;
    public string         Name            { get; set; } = "";
    public int            Standard        { get; set; }
    public double         Faktor          { get; set; } = 1.0;
    public long           MehrkostenWoche { get; set; }
    public GelaendeStufe? Naechste        { get; set; }
    public bool           ImBau           { get; set; }
}

public class GelaendeStufe
{
    public int    Stufe           { get; set; }
    public string Name            { get; set; } = "";
    public long   Kosten          { get; set; }
    public int    Wochen          { get; set; }
    public double Faktor          { get; set; }
    public long   MehrkostenWoche { get; set; }
}

public class Bauprojekt
{
    public string   Art      { get; set; } = "";
    public string   Anzeige  { get; set; } = "";
    public int      Ziel     { get; set; }
    public long     Kosten   { get; set; }
    public DateOnly Begonnen { get; set; }
    public DateOnly Fertig   { get; set; }
}

/// <summary>Was die Jugendscouts vorschlagen (GET nachwuchs/{vereinId}).</summary>
public class NachwuchsModel
{
    public int                   Vereinsruf { get; set; }
    public int                   Jugendruf  { get; set; }
    public long                  Scouts     { get; set; }
    public List<Nachwuchstalent> Talente    { get; set; } = new();
}

public class Nachwuchstalent
{
    public long      Id               { get; set; }
    public string    Name             { get; set; } = "";
    public int       Alter            { get; set; }
    public string    Position         { get; set; } = "";
    public string    Kader            { get; set; } = "";
    public int       Staerke          { get; set; }
    public int       TalentSchaetzung { get; set; }
    public string    Scout            { get; set; } = "";
    public DateOnly  Gefunden         { get; set; }
    public string    Status           { get; set; } = "";
    public DateOnly? Entschieden      { get; set; }
    public DateOnly  VerfaelltAm      { get; set; }
    public string    Aussicht         { get; set; } = "";
    public long?     SpielerId        { get; set; }

    public string KaderText => Kader switch
    {
        "JugendA" => "A-Jugend",
        "JugendB" => "B-Jugend",
        _         => "C-Jugend",
    };

    public string StatusText => Status switch
    {
        "GEFUNDEN"   => "Vorgeschlagen",
        "EINGELADEN" => "Zum Probetraining eingeladen",
        "ZUGESAGT"   => "Zugesagt",
        "ABGESAGT"   => "Abgesagt",
        "VERFALLEN"  => "Woanders hin",
        "VERWORFEN"  => "Abgelehnt",
        _            => Status,
    };
}
