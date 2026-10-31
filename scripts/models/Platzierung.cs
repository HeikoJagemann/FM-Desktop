#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Ein Platz der Aufstellung auf dem Rasen (normiert: X vom eigenen Tor, Y von links).</summary>
public class PlatzKoordinate
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>Eine Formationsvorlage (GET aufstellung/vorlagen).</summary>
public class FormationsVorlage
{
    public string             Name    { get; set; } = "";
    public List<VorlagenPlatz> Plaetze { get; set; } = new();
}

public class VorlagenPlatz
{
    public string Slot { get; set; } = "";
    public double X    { get; set; }
    public double Y    { get; set; }
}

/// <summary>
/// Welche Position ein Spieler an einer Stelle des Rasens spielt - für die Anzeige, während er
/// gezogen wird.
///
/// <para>Verbindlich rechnet der Server (<c>Formationen.positionAus</c>): Er benennt jede
/// gespeicherte Aufstellung neu. Die Grenzen hier müssen dieselben sein, sonst zeigt die Plakette
/// beim Ziehen eine andere Position als nach dem Loslassen.</para>
/// </summary>
public static class Platzierung
{
    private const double BahnLinks  = 0.22;
    private const double BahnRechts = 0.78;
    private static readonly double[] MitteGrenzen  = { 0.33, 0.43, 0.57, 0.70, 0.83 };
    private static readonly string[] MittePositionen = { "IV", "DM", "ZM", "OM", "HS", "ST" };
    private static readonly double[] AussenGrenzen = { 0.40, 0.66 };

    /// <summary>Grenzen, innerhalb derer Feldspieler stehen dürfen - wie im Server.</summary>
    public const double XMin = 0.08, XMax = 0.95, YMin = 0.04, YMax = 0.96;

    /// <param name="x">Tiefe vom eigenen Tor, 0 bis 1</param>
    /// <param name="y">Seite von links, 0 bis 1</param>
    public static string PositionAus(double x, double y)
    {
        if (y < BahnLinks || y > BahnRechts)
        {
            bool links = y < BahnLinks;
            if (x < AussenGrenzen[0]) return links ? "LV" : "RV";
            if (x < AussenGrenzen[1]) return links ? "LM" : "RM";
            return links ? "LA" : "RA";
        }
        for (int i = 0; i < MitteGrenzen.Length; i++)
            if (x < MitteGrenzen[i]) return MittePositionen[i];
        return MittePositionen[^1];
    }
}
