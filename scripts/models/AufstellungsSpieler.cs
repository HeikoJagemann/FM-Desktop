#nullable enable
using System;

namespace FMDesktop.Models;

/// <summary>Ein Spieler, wie er in einem bestimmten Spiel aufgelaufen ist.</summary>
public class AufstellungsSpieler
{
    public long    SpielerId     { get; set; }
    public string  Name          { get; set; } = "";
    public string  Slot          { get; set; } = "";
    public string  Position      { get; set; } = "";
    public int     Staerke       { get; set; }
    public int     Ausdauer      { get; set; }
    public bool    Startelf      { get; set; }

    /// <summary>Einwechselminute; 0 für die Startelf.</summary>
    public int     VonMinute     { get; set; }

    /// <summary>Minute des Abgangs, oder null bei Einsatz bis zum Schluss.</summary>
    public int?    BisMinute     { get; set; }

    /// <summary>Restfrische in Prozent am Ende des Einsatzes.</summary>
    public double  KonditionEnde { get; set; }

    public int     Tore          { get; set; }
    public int     Vorlagen      { get; set; }
    public int     GelbeKarten   { get; set; }
    public int     RoteKarten    { get; set; }
    public double  Note          { get; set; }

    public bool StehtAufDemPlatz(int minute)
        => minute >= VonMinute && (BisMinute == null || minute < BisMinute);

    /// <summary>
    /// Frische zur gegebenen Spielminute. Der Server liefert nur den Endwert, deshalb wird
    /// zwischen 100 % beim Betreten des Platzes und diesem Endwert linear interpoliert –
    /// der Konditionsverfall verläuft nahezu gleichmäßig.
    /// </summary>
    public double FrischeBei(int minute)
    {
        int ende = BisMinute ?? 90;
        if (minute <= VonMinute) return 100.0;
        if (minute >= ende)      return KonditionEnde;

        int dauer = Math.Max(1, ende - VonMinute);
        double anteil = (double)(minute - VonMinute) / dauer;
        return 100.0 - (100.0 - KonditionEnde) * anteil;
    }
}
