using System.Globalization;

namespace FMDesktop.Models;

/// <summary>
/// Baut den Mouseover-Text, der erklärt, wie sich eine angezeigte Stärke zusammensetzt:
/// Grundstärke (der Position) × Eingespieltheit × Form [× Frische] = Spielstärke.
///
/// Die Endwerte (Stärke, Spielstärke) kommen immer direkt vom Backend, nie aus einer
/// Neuberechnung hier - nur die Zwischenfaktoren werden fürs Verständnis lokal ausgerechnet,
/// damit die angezeigte Rechnung nie von der tatsächlich verwendeten Zahl abweicht.
/// </summary>
public static class StaerkeErklaerung
{
    private static double EingespieltFaktor(int eingespieltheit) => 0.5 + 0.5 * eingespieltheit / 100.0;

    private static string F(double wert) => wert.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Ohne Spielbetrieb: Grundstärke × Eingespieltheit × Form = Stärke.</summary>
    /// <param name="staerke">die Stärke mit Form - so, wie sie überall angezeigt wird</param>
    public static string Basis(string position, int grundstaerke, int eingespieltheit, int staerke,
        double formFaktor = 1.0)
        => Kopf(position, grundstaerke, eingespieltheit, formFaktor) + $"= Stärke: {staerke}";

    /// <summary>Im Spielbetrieb: zusätzlich die aktuelle Frische bis zur Spielstärke.</summary>
    /// <param name="staerke">die Stärke ohne Form und Frische - der Ausgangswert bei Anpfiff</param>
    public static string MitFrische(string position, int grundstaerke, int eingespieltheit, int staerke,
        double kondition, int effektiveStaerke, double formFaktor = 1.0)
    {
        // Früher stand der ganze Abstand zur Stärke unter "Frische" - die Form steckte mit darin.
        double mitForm = staerke * formFaktor;
        double konditionsFaktor = mitForm > 0 ? effektiveStaerke / mitForm : 1.0;
        return Kopf(position, grundstaerke, eingespieltheit, formFaktor)
             + $"Frische {kondition:0}%: × {F(konditionsFaktor)}\n"
             + $"= Spielstärke: {effektiveStaerke}";
    }

    /// <summary>
    /// In der Aufstellung, vor dem Spiel: alle Faktoren - erst bis zur Stärke, wie sie auf der
    /// Plakette steht, dann mit der Frische bis zu dem, was er bei Anpfiff bringt.
    /// </summary>
    /// <param name="staerke">die Stärke mit Form - die Zahl auf der Plakette</param>
    /// <param name="form">Formwert, 50 ist normal</param>
    /// <param name="frische">Frische bei Anpfiff in Prozent</param>
    /// <param name="frischeFaktor">was die Frische von der Stärke übrig lässt - vom Server</param>
    /// <param name="spielstaerke">Stärke bei Anpfiff - vom Server</param>
    public static string Aufstellung(string position, int grundstaerke, int eingespieltheit, int staerke,
        double formFaktor, int form, int frische, double frischeFaktor, int spielstaerke)
        => Kopf(position, grundstaerke, eingespieltheit, formFaktor, form)
         + $"= Stärke: {staerke}\n"
         + $"Frische {frische}% bei Anpfiff: × {F(frischeFaktor)}\n"
         + $"= Spielstärke bei Anpfiff: {spielstaerke}";

    private static string Kopf(string position, int grundstaerke, int eingespieltheit, double formFaktor,
        int? form = null)
        => $"Grundstärke ({position}): {grundstaerke}\n"
         + $"Eingespieltheit {eingespieltheit}%: × {F(EingespieltFaktor(eingespieltheit))}\n"
         + (form.HasValue ? $"Form {form} ({Formwort(form.Value)}): × {F(formFaktor)}\n" : $"Form: × {F(formFaktor)}\n");

    /// <summary>Formwert in Worten - 50 ist normal, die Spanne reicht von 10 bis 90.</summary>
    private static string Formwort(int form) => form switch
    {
        >= 75 => "bestens",
        >= 60 => "gut",
        > 40  => "normal",
        > 25  => "schwach",
        _     => "miserabel",
    };
}
