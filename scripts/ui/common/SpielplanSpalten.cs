#nullable enable
using System.Collections.Generic;
using Godot;
using FMDesktop.Models;

namespace FMDesktop.UI.Common;

/// <summary>Spaltenkatalog des Spielplans.</summary>
public static class SpielplanSpalten
{
    /// <summary>Eigene Partien werden hervorgehoben - danach sucht der Manager zuerst.</summary>
    private static bool IstEigenes(Spiel s) =>
        s.HeimVerein?.Id == GameState.Instance.VereinId
     || s.GastVerein?.Id == GameState.Instance.VereinId;

    private static Color VereinsFarbe(Spiel s) =>
        IstEigenes(s) ? FmTheme.Accent : FmTheme.TextPrimary;

    public static GridSpalte<Spiel> Termin => new()
    {
        Titel = "Termin", Breite = 130,
        Text       = s => s.TerminText,
        Farbe      = _ => FmTheme.TextSecondary,
        Sortierung = s => s.TerminText,
    };

    public static GridSpalte<Spiel> Heim => new()
    {
        Titel = "Heim", Breite = 160, Expand = true, Ausrichtung = HorizontalAlignment.Right,
        Text  = s => s.HeimVerein?.Name ?? "",
        Farbe = VereinsFarbe,
    };

    public static GridSpalte<Spiel> Ergebnis => new()
    {
        Titel = "Ergebnis", Breite = 76, Ausrichtung = HorizontalAlignment.Center,
        Text  = s => s.Ergebnis,
        Farbe = VereinsFarbe,
    };

    public static GridSpalte<Spiel> Gast => new()
    {
        Titel = "Gast", Breite = 160, Expand = true,
        Text  = s => s.GastVerein?.Name ?? "",
        Farbe = VereinsFarbe,
    };

    public static GridSpalte<Spiel> Status => new()
    {
        Titel = "Status", Breite = 60, Ausrichtung = HorizontalAlignment.Center,
        Text    = s => s.Gespielt ? "✓" : "–",
        Farbe   = s => s.Gespielt ? FmTheme.Success : FmTheme.TextSecondary,
        Tooltip = s => s.Gespielt
            ? "Ausgetragen - Rechtsklick öffnet den Spielbericht."
            : "Noch nicht ausgetragen.",
    };

    public static IEnumerable<GridSpalte<Spiel>> Spielplan => new[]
    {
        Termin, Heim, Ergebnis, Gast, Status,
    };
}
