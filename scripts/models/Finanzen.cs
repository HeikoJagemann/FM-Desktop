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
        "TRANSFER"     => "Transfer",
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
