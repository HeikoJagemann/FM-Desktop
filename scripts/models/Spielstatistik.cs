#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace FMDesktop.Models;

/// <summary>
/// Kennzahlen einer Mannschaft in einer Partie - je einmal für Heim und Gast.
/// Kommt live aus der laufenden Simulation und nach dem Spiel aus dem Spielbericht.
/// </summary>
public class Spielstatistik
{
    public int    Schuesse          { get; set; }
    public int    SchuesseAufsTor   { get; set; }
    public int    Geblockt          { get; set; }
    public double XGoals            { get; set; }
    public int    BallbesitzProzent { get; set; }
    public int    GelbeKarten       { get; set; }
    public int    RoteKarten        { get; set; }

    /// <summary>xG mit einer Nachkommastelle - mehr Genauigkeit gaukelt Präzision vor.</summary>
    public string XGoalsText => XGoals.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>
    /// Die Zeilen der Statistiktabelle in Anzeigereihenfolge. Hier steht, was verglichen wird -
    /// die Ansicht stellt es nur nebeneinander.
    /// </summary>
    /// <param name="gegner">Werte der anderen Mannschaft, für den Vergleichsbalken.</param>
    public static List<Statistikzeile> Zeilen(Spielstatistik? heim, Spielstatistik? gast)
    {
        heim ??= new Spielstatistik();
        gast ??= new Spielstatistik();

        return new List<Statistikzeile>
        {
            new("Ballbesitz",    $"{heim.BallbesitzProzent} %", $"{gast.BallbesitzProzent} %",
                                 heim.BallbesitzProzent, gast.BallbesitzProzent),
            new("Torschüsse",    heim.SchuesseAufsTor.ToString(), gast.SchuesseAufsTor.ToString(),
                                 heim.SchuesseAufsTor, gast.SchuesseAufsTor),
            new("Schüsse gesamt", heim.Schuesse.ToString(), gast.Schuesse.ToString(),
                                 heim.Schuesse, gast.Schuesse),
            new("Geblockt",      heim.Geblockt.ToString(), gast.Geblockt.ToString(),
                                 heim.Geblockt, gast.Geblockt),
            new("xGoals",        heim.XGoalsText, gast.XGoalsText,
                                 heim.XGoals, gast.XGoals),
            new("Gelbe Karten",  heim.GelbeKarten.ToString(), gast.GelbeKarten.ToString(),
                                 heim.GelbeKarten, gast.GelbeKarten),
            new("Rote Karten",   heim.RoteKarten.ToString(), gast.RoteKarten.ToString(),
                                 heim.RoteKarten, gast.RoteKarten),
        };
    }
}

/// <summary>Eine Zeile der Statistiktabelle: Beschriftung und beide Werte, Text wie Zahl.</summary>
public record Statistikzeile(string Titel, string HeimText, string GastText,
                             double HeimWert, double GastWert)
{
    /// <summary>
    /// Anteil der Heimmannschaft am Balken, 0 bis 1. Bei zwei Nullen steht der Balken in der
    /// Mitte, damit eine leere Zeile nicht wie ein Vorteil aussieht.
    /// </summary>
    public double HeimAnteil
    {
        get
        {
            double summe = HeimWert + GastWert;
            return summe <= 0 ? 0.5 : HeimWert / summe;
        }
    }

    /// <summary>Ob diese Zeile überhaupt etwas zu zeigen hat - sonst bleibt sie weg.</summary>
    public bool HatWerte => HeimWert > 0 || GastWert > 0;
}
