#nullable enable
using System.Collections.Generic;
using Godot;
using FMDesktop.Models;

namespace FMDesktop.UI.Common;

/// <summary>Spaltenkatalog der Buchungsliste in der Finanzansicht.</summary>
public static class FinanzSpalten
{
    public static GridSpalte<BuchungModel> Datum => new()
    {
        Titel = "Datum", Breite = 90,
        Text       = b => b.Datum.ToString("dd.MM.yyyy"),
        Sortierung = b => b.Datum,
    };

    public static GridSpalte<BuchungModel> Art => new()
    {
        Titel = "Art", Breite = 90,
        Text  = b => b.ArtText,
        Farbe = b => FarbeFuerArt(b.Art),
    };

    public static GridSpalte<BuchungModel> Text => new()
    {
        Titel = "Text", Breite = 220, Expand = true,
        Text  = b => b.Text,
    };

    public static GridSpalte<BuchungModel> Betrag => new()
    {
        Titel = "Betrag", Breite = 120, Ausrichtung = HorizontalAlignment.Right,
        Text       = b => b.BetragText,
        Farbe      = b => b.Betrag >= 0 ? FmTheme.Success : FmTheme.Danger,
        Sortierung = b => b.Datum,
    };

    public static IEnumerable<GridSpalte<BuchungModel>> Buchungsliste => new[]
    {
        Datum, Art, Text, Betrag,
    };

    private static Color FarbeFuerArt(string art) => art switch
    {
        "ZUSCHAUER" or "SPONSOR" or "STARTKAPITAL" or "TRANSFER" or "PRAEMIE" => FmTheme.TextPrimary,
        _ => FmTheme.TextSecondary,
    };
}
