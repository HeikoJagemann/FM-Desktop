using System.Globalization;

namespace FMDesktop.Models;

/// <summary>
/// Baut den Mouseover-Text, der erklärt, wie sich eine angezeigte Stärke zusammensetzt:
/// Grundstärke (der Position) + Eingespieltheit [+ Frische] = Spielstärke.
///
/// Die Endwerte (Stärke, Spielstärke) kommen immer direkt vom Backend, nie aus einer
/// Neuberechnung hier - nur die Zwischenfaktoren werden fürs Verständnis lokal ausgerechnet,
/// damit die angezeigte Rechnung nie von der tatsächlich verwendeten Zahl abweicht.
/// </summary>
public static class StaerkeErklaerung
{
    private static double EingespieltFaktor(int eingespieltheit) => 0.5 + 0.5 * eingespieltheit / 100.0;

    private static string F(double wert) => wert.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Ohne Spielbetrieb: Grundstärke + Eingespieltheit = Stärke.</summary>
    public static string Basis(string position, int grundstaerke, int eingespieltheit, int staerke)
    {
        double faktor = EingespieltFaktor(eingespieltheit);
        return $"Grundstärke ({position}): {grundstaerke}\n"
             + $"Eingespieltheit {eingespieltheit}%: × {F(faktor)}\n"
             + $"= Stärke: {staerke}";
    }

    /// <summary>Im Spielbetrieb: zusätzlich die aktuelle Frische bis zur Spielstärke.</summary>
    public static string MitFrische(string position, int grundstaerke, int eingespieltheit, int staerke,
        double kondition, int effektiveStaerke)
    {
        double konditionsFaktor = staerke > 0 ? effektiveStaerke / (double)staerke : 1.0;
        return Basis(position, grundstaerke, eingespieltheit, staerke)
             + $"\nFrische {kondition:0}%: × {F(konditionsFaktor)}"
             + $"\n= Spielstärke: {effektiveStaerke}";
    }
}
