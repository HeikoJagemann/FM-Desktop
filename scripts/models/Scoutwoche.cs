#nullable enable
using System;
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Was die Scouts in einer Woche herausgefunden haben (GET scouting/woche).</summary>
public class Scoutwoche
{
    public DateOnly              Sonntag          { get; set; }
    public int                   Beobachtungen    { get; set; }
    public int                   AnzahlSpieler    { get; set; }
    public int                   Erstmals         { get; set; }
    public int                   AufTransferliste { get; set; }
    public List<Scoutquelle>     Quellen          { get; set; } = new();
    public List<Scouthoehepunkt> Hoehepunkte      { get; set; } = new();
    public List<long>            Neu              { get; set; } = new();
    public List<TransferSpieler> Spieler          { get; set; } = new();
}

/// <summary>Ein Scout - oder die Spielbeobachtung der eigenen Partien - und was er gesehen hat.</summary>
public class Scoutquelle
{
    public string Name          { get; set; } = "";
    public int    Beobachtungen { get; set; }
    public int    Spieler       { get; set; }
}

public class Scouthoehepunkt
{
    public string Titel     { get; set; } = "";
    public long   SpielerId { get; set; }
}
