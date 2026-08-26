#nullable enable
using System.Collections.Generic;
using Godot;
using FMDesktop.Models;

namespace FMDesktop.UI.Common;

/// <summary>
/// Spaltenkataloge des Transfermarkts - einmal für die Spielersuche, einmal für die Angebotsliste.
/// Wie die übrigen Kataloge tragen die Spalten ihr Verhalten selbst: Text, Farbe und Mouseover.
/// </summary>
public static class TransferSpalten
{
    // ── Spielersuche ─────────────────────────────────────────────────────────

    public static GridSpalte<TransferSpieler> Name => new()
    {
        Titel = "Name", Breite = 150, Expand = true, ExpandGewicht = 3,
        Text       = s => s.Name,
        Sortierung = s => s.Name,
    };

    public static GridSpalte<TransferSpieler> Position => new()
    {
        Titel = "Pos", Breite = 46,
        Text       = s => s.Position,
        Farbe      = s => FmTheme.TextFuerGruppe(PositionsgruppeHelfer.Von(s.Position)),
        Sortierung = s => PositionsgruppeHelfer.Rang(s.Position),
    };

    public static GridSpalte<TransferSpieler> Verein => new()
    {
        Titel = "Verein", Breite = 150, Expand = true, ExpandGewicht = 2,
        Text       = s => s.Verein,
        Tooltip    = s => s.Liga,
        Sortierung = s => s.Verein,
    };

    public static GridSpalte<TransferSpieler> Staerke => new()
    {
        Titel = "Stärke", Breite = 64, Ausrichtung = HorizontalAlignment.Center,
        Text       = s => s.Staerke.ToString(),
        Farbe      = s => s.Staerke >= 70 ? FmTheme.Success
                        : s.Staerke >= 50 ? FmTheme.TextPrimary
                        : FmTheme.TextSecondary,
        Sortierung = s => -s.Staerke,
    };

    public static GridSpalte<TransferSpieler> Alter => new()
    {
        Titel = "Alter", Breite = 52, Ausrichtung = HorizontalAlignment.Center,
        Text       = s => s.Alter.ToString(),
        Sortierung = s => s.Alter,
    };

    public static GridSpalte<TransferSpieler> Vertrag => new()
    {
        Titel = "Vertrag", Breite = 90, Ausrichtung = HorizontalAlignment.Center,
        Text       = s => s.VertragText,
        Tooltip    = _ => "Je kürzer der Vertrag läuft, desto günstiger wird der Spieler.\n"
                        + "Nach Vertragsende ist er ablösefrei.",
        Sortierung = s => s.VertragBis,
    };

    /// <summary>Der entscheidende Wert - rot, wenn das Geld nicht reicht.</summary>
    public static GridSpalte<TransferSpieler> Abloese => new()
    {
        Titel = "Ablöse", Breite = 120, Ausrichtung = HorizontalAlignment.Right,
        Text       = s => FmTheme.Geld(s.Abloese),
        Farbe      = s => s.Bezahlbar ? FmTheme.TextPrimary : FmTheme.Danger,
        Tooltip    = s => s.Bezahlbar
            ? $"Marktwert {FmTheme.Geld(s.Wert)}"
            : "Die Ablöse übersteigt den Kontostand.",
        Sortierung = s => -s.Abloese,
    };

    public static GridSpalte<TransferSpieler> Gehaltsforderung => new()
    {
        Titel = "Gehalt/Woche", Breite = 120, Ausrichtung = HorizontalAlignment.Right,
        Text       = s => FmTheme.Geld(s.Gehaltsforderung),
        Tooltip    = _ => "Was der Spieler bei einem Wechsel fordert - etwas über seinem "
                        + "bisherigen Marktsatz.",
        Sortierung = s => -s.Gehaltsforderung,
    };

    public static IEnumerable<GridSpalte<TransferSpieler>> Suchergebnis => new[]
    {
        Name, Position, Verein, Staerke, Alter, Vertrag, Abloese, Gehaltsforderung,
    };

    // ── Angebote ─────────────────────────────────────────────────────────────

    public static GridSpalte<TransferangebotModel> AngebotSpieler => new()
    {
        Titel = "Spieler", Breite = 150, Expand = true, ExpandGewicht = 3,
        Text       = a => a.SpielerName,
        Sortierung = a => a.SpielerName,
    };

    public static GridSpalte<TransferangebotModel> AngebotPosition => new()
    {
        Titel = "Pos", Breite = 46,
        Text  = a => a.Position,
        Farbe = a => FmTheme.TextFuerGruppe(PositionsgruppeHelfer.Von(a.Position)),
    };

    public static GridSpalte<TransferangebotModel> AngebotStaerke => new()
    {
        Titel = "Stärke", Breite = 64, Ausrichtung = HorizontalAlignment.Center,
        Text       = a => a.Staerke.ToString(),
        Sortierung = a => -a.Staerke,
    };

    /// <summary>Bei eingehenden Angeboten der Interessent, bei ausgehenden der abgebende Verein.</summary>
    public static GridSpalte<TransferangebotModel> AngebotVerein(bool eingehend) => new()
    {
        Titel = eingehend ? "Interessent" : "Verein", Breite = 150, Expand = true, ExpandGewicht = 2,
        Text       = a => eingehend ? a.NachVerein : a.VonVerein,
        Sortierung = a => eingehend ? a.NachVerein : a.VonVerein,
    };

    public static GridSpalte<TransferangebotModel> AngebotAbloese => new()
    {
        Titel = "Ablöse", Breite = 120, Ausrichtung = HorizontalAlignment.Right,
        Text       = a => FmTheme.Geld(a.Abloese),
        Farbe      = _ => FmTheme.Success,
        Sortierung = a => -a.Abloese,
    };

    public static GridSpalte<TransferangebotModel> AngebotGehalt => new()
    {
        Titel = "Gehalt/Woche", Breite = 120, Ausrichtung = HorizontalAlignment.Right,
        Text    = a => FmTheme.Geld(a.Gehalt),
        Tooltip = a => $"Neuer Vertrag bis Saison {a.VertragBis}/{a.VertragBis + 1}",
    };

    public static IEnumerable<GridSpalte<TransferangebotModel>> Angebotsliste(bool eingehend) => new[]
    {
        AngebotSpieler, AngebotPosition, AngebotStaerke, AngebotVerein(eingehend),
        AngebotAbloese, AngebotGehalt,
    };
}
