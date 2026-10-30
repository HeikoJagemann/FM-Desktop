#nullable enable
using System;
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Finanzüberblick eines Vereins (GET finanzen/{vereinId}).</summary>
public class FinanzenModel
{
    public long Kontostand   { get; set; }
    /// <summary>Summe der Buchungen der laufenden Kalenderwoche - Einnahmen minus Ausgaben.</summary>
    public long Wochenbilanz { get; set; }
    /// <summary>Summe der Wochengehälter des aktuellen Profi- und Amateurkaders.</summary>
    public long Gehaltsetat  { get; set; }
    public int  Ticketpreis  { get; set; }
    public List<BuchungModel> Buchungen { get; set; } = new();

    public string KontostandText => Geldformat.Text(Kontostand);
}

/// <summary>Eine einzelne Kontobewegung - positiver Betrag ist eine Einnahme, negativer eine Ausgabe.</summary>
public class BuchungModel
{
    public DateOnly Datum  { get; set; }
    public string   Art    { get; set; } = "";
    public long     Betrag { get; set; }
    public string   Text   { get; set; } = "";

    public string ArtText => Art switch
    {
        "STARTKAPITAL" => "Startkapital",
        "ZUSCHAUER"    => "Zuschauer",
        "GEHALT"       => "Gehälter",
        "BETRIEB"      => "Betrieb",
        "SPONSOR"      => "Sponsor",
        "FERNSEHGELD"  => "Fernsehgeld",
        "TRANSFER"     => "Transfer",
        "SCOUTING"     => "Scouting",
        "PRAEMIE"      => "Prämie",
        _              => Art,
    };

    public string BetragText => (Betrag >= 0 ? "+" : "") + Geldformat.Text(Betrag);
}

/// <summary>
/// Einheitliche Geldformatierung fürs ganze Modell - Tausendertrennung, Euro-Zeichen am Betrag.
/// <see cref="FMDesktop.UI.FmTheme.Geld"/> ruft dieselbe Formatierung fürs UI auf; hier liegt sie,
/// damit Modelle nicht von der UI-Schicht abhängen müssen.
/// </summary>
public static class Geldformat
{
    public static string Text(long betrag) =>
        betrag.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " €";
}

/// <summary>Zeitraum, auf den Gehälter fürs Anzeigen umgerechnet werden - reine Client-Einstellung.</summary>
public enum GehaltsPeriode { Woche, Monat, Jahr }

/// <summary>
/// Rechnet die vom Backend stets als Wochenbetrag gelieferten Gehälter in die vom Nutzer
/// gewählte Anzeigeperiode um. Das Backend selbst kennt nur Wochengehälter - Verträge, Angebote
/// und der Gehaltsetat rechnen intern ausschließlich damit; hier wird nur fürs Anzeigen umgerechnet.
/// </summary>
public static class Gehaltsformat
{
    private const string Schluessel = "gehalt_periode";

    public static GehaltsPeriode Periode
    {
        get => Enum.TryParse<GehaltsPeriode>(
            ClientEinstellungen.GetString(Schluessel, nameof(GehaltsPeriode.Woche)), out var periode)
            ? periode : GehaltsPeriode.Woche;
        set => ClientEinstellungen.SetString(Schluessel, value.ToString());
    }

    public static string PeriodeLabel => Periode switch
    {
        GehaltsPeriode.Monat => "Monat",
        GehaltsPeriode.Jahr  => "Jahr",
        _                    => "Woche",
    };

    /// <summary>Spaltentitel/Zeilenbeschriftung, passend zur aktuell eingestellten Periode.</summary>
    public static string Titel => $"Gehalt/{PeriodeLabel}";

    /// <summary>
    /// Ein Jahr hat 52 Kalenderwochen; ein Monat entsprechend 52/12 Wochen - kein grober
    /// Faktor 4, sonst würde das angezeigte Jahresgehalt nicht dem Zwölffachen des Monatsgehalts
    /// entsprechen.
    /// </summary>
    private static long Umrechnen(long wochenbetrag) => Periode switch
    {
        GehaltsPeriode.Monat => wochenbetrag * 52 / 12,
        GehaltsPeriode.Jahr  => wochenbetrag * 52,
        _                    => wochenbetrag,
    };

    public static string Text(long wochenbetrag) => Geldformat.Text(Umrechnen(wochenbetrag));
}
