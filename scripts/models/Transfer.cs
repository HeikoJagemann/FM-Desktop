#nullable enable
using System;

namespace FMDesktop.Models;

/// <summary>Ob gerade gewechselt werden darf (GET transfer/fenster).</summary>
public class TransferfensterModel
{
    public bool   Offen   { get; set; }
    public string Hinweis { get; set; } = "";
    public int    Saison  { get; set; }
}

/// <summary>Ein Spieler auf dem Transfermarkt (GET transfer/suche).</summary>
public class TransferSpieler
{
    public long   SpielerId        { get; set; }
    public string Name             { get; set; } = "";
    public string Position         { get; set; } = "";
    public int    Staerke          { get; set; }
    public int    Talent           { get; set; }
    public int    Alter            { get; set; }
    public int    Wert             { get; set; }
    public int    VertragBis       { get; set; }
    public long   VereinId         { get; set; }
    public string Verein           { get; set; } = "";
    public string Liga             { get; set; } = "";
    public long   Abloese          { get; set; }
    public long   Gehaltsforderung { get; set; }

    /// <summary>Ob der eigene Verein die Ablöse aufbringen kann - vom Backend gerechnet.</summary>
    public bool Bezahlbar { get; set; }

    public string VertragText => VertragBis > 0 ? $"bis {VertragBis}/{VertragBis + 1}" : "–";
}

/// <summary>Ein Transferangebot in beide Richtungen (GET transfer/eingehend bzw. /ausgehend).</summary>
public class TransferangebotModel
{
    public long     Id           { get; set; }
    public long     SpielerId    { get; set; }
    public string   SpielerName  { get; set; } = "";
    public string   Position     { get; set; } = "";
    public int      Staerke      { get; set; }
    public int      Alter        { get; set; }
    public long     VonVereinId  { get; set; }
    public string   VonVerein    { get; set; } = "";
    public long     NachVereinId { get; set; }
    public string   NachVerein   { get; set; } = "";
    public long     Abloese      { get; set; }
    public long     Gehalt       { get; set; }
    public int      VertragBis   { get; set; }
    public DateOnly Datum        { get; set; }
    public string   Status       { get; set; } = "";
}
