#nullable enable
using System.Collections.Generic;
using Godot;
using FMDesktop.Models;

namespace FMDesktop.UI.Common;

/// <summary>
/// Spaltenkataloge des Transfermarkts - für fremde Spieler (Transferliste, Scoutberichte) und für
/// Angebote. Wie die übrigen Kataloge tragen die Spalten ihr Verhalten selbst: Text, Farbe und
/// Mouseover.
/// </summary>
public static class TransferSpalten
{
    // ── Fremde Spieler ───────────────────────────────────────────────────────

    public static GridSpalte<TransferSpieler> Name => new()
    {
        Titel = "Name", Breite = 150, Expand = true, ExpandGewicht = 3,
        Text       = s => s.AufTransferliste ? $"{s.Name}  🏷" : s.Name,
        Tooltip    = s => s.AufTransferliste ? "Steht auf der Transferliste." : "Steht nicht zum Verkauf - abwerben.",
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

    /// <summary>Eine Spanne, keine Zahl - je enger, desto mehr weiß der Verein über den Spieler.</summary>
    public static GridSpalte<TransferSpieler> Staerke => new()
    {
        Titel = "Stärke", Breite = 74, Ausrichtung = HorizontalAlignment.Center,
        Text       = s => s.StaerkeText,
        Farbe      = s => s.StaerkeMitte >= 70 ? FmTheme.Success
                        : s.StaerkeMitte >= 50 ? FmTheme.TextPrimary
                        : FmTheme.TextSecondary,
        Tooltip    = s => s.Beobachtungen == 0
            ? "Nur der öffentliche Eindruck - ein Scoutbericht macht die Spanne enger."
            : $"{s.Beobachtungen} Beobachtung(en), zuletzt {s.Zuletzt:dd.MM.yyyy}.",
        Sortierung = s => -s.StaerkeMitte,
    };

    public static GridSpalte<TransferSpieler> Talent => new()
    {
        Titel = "Talent", Breite = 74, Ausrichtung = HorizontalAlignment.Center,
        Text       = s => s.TalentText,
        Farbe      = s => s.TalentBis == 0 ? FmTheme.TextGedimmt : FmTheme.TextPrimary,
        Tooltip    = _ => "Wie weit sich der Spieler entwickeln kann.\nOhne Scoutbericht unbekannt.",
        Sortierung = s => -(s.TalentVon + s.TalentBis),
    };

    /// <summary>Wie weit der Spieler gescoutet ist - je voller, desto enger die Spannen.</summary>
    public static GridSpalte<TransferSpieler> Scouting => new()
    {
        Titel = "Scouting", Breite = 104,
        Text       = s => s.KenntnisText,
        Farbe      = s => s.Kenntnis >= 80 ? FmTheme.Success
                        : s.Kenntnis >= 30 ? FmTheme.TextPrimary
                        : FmTheme.TextGedimmt,
        Tooltip    = s => s.KenntnisTooltip,
        Sortierung = s => -s.Kenntnis,
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

    public static GridSpalte<TransferSpieler> Marktwert => new()
    {
        Titel = "Marktwert", Breite = 110, Ausrichtung = HorizontalAlignment.Right,
        Text       = s => FmTheme.Geld(s.Wert),
        Sortierung = s => -s.Wert,
    };

    /// <summary>Der ausgeschriebene Preis - rot, wenn das Geld nicht reicht; leer, wenn man bieten muss.</summary>
    public static GridSpalte<TransferSpieler> Preis => new()
    {
        Titel = "Preis", Breite = 110, Ausrichtung = HorizontalAlignment.Right,
        Text       = s => s.Preis > 0 ? FmTheme.Geld(s.Preis) : "auf Anfrage",
        Farbe      = s => s.Preis == 0 ? FmTheme.TextGedimmt
                        : s.Bezahlbar ? FmTheme.TextPrimary : FmTheme.Danger,
        Tooltip    = s => s.Preis == 0
            ? "Steht nicht zum Verkauf. Was der Verein verlangt, erfährt man mit einem Angebot."
            : s.Bezahlbar ? "So viel verlangt der Verein." : "Der Preis übersteigt den Kontostand.",
        Sortierung = s => -s.Preis,
    };

    public static GridSpalte<TransferSpieler> Gehaltsforderung => new()
    {
        Titel = FmTheme.GehaltTitel, Breite = 110, Ausrichtung = HorizontalAlignment.Right,
        Text       = s => FmTheme.Gehalt(s.Gehaltsforderung),
        Tooltip    = _ => "Was der Spieler bei einem Wechsel fordert - etwas über seinem "
                        + "bisherigen Marktsatz.",
        Sortierung = s => -s.Gehaltsforderung,
    };

    public static IEnumerable<GridSpalte<TransferSpieler>> Transferliste => new[]
    {
        Name, Position, Verein, Staerke, Talent, Scouting, Alter, Vertrag, Preis, Gehaltsforderung,
    };

    public static IEnumerable<GridSpalte<TransferSpieler>> Berichte => new[]
    {
        Name, Position, Verein, Staerke, Talent, Scouting, Alter, Vertrag, Marktwert, Preis,
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
        Titel = "Stärke", Breite = 74, Ausrichtung = HorizontalAlignment.Center,
        Text       = a => a.StaerkeText,
        Sortierung = a => -(a.StaerkeVon + a.StaerkeBis),
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
        Titel = FmTheme.GehaltTitel, Breite = 120, Ausrichtung = HorizontalAlignment.Right,
        Text    = a => FmTheme.Gehalt(a.Gehalt),
        Tooltip = a => $"Neuer Vertrag bis Saison {a.VertragBis}/{a.VertragBis + 1}",
    };

    /// <summary>Wie es um ein eigenes Angebot steht - die Begründung einer Absage im Mouseover.</summary>
    public static GridSpalte<TransferangebotModel> AngebotStatus => new()
    {
        Titel = "Antwort", Breite = 220, Expand = true, ExpandGewicht = 3,
        Text       = a => string.IsNullOrEmpty(a.Grund) ? a.StatusText : $"{a.StatusText}: {a.Grund}",
        Farbe      = a => a.Status switch
        {
            "ANGENOMMEN" => FmTheme.Success,
            "ABGELEHNT"  => FmTheme.Danger,
            _            => FmTheme.TextSecondary,
        },
        Tooltip    = a => string.IsNullOrEmpty(a.Grund) ? a.StatusText : a.Grund,
        Sortierung = a => a.Status,
    };

    public static IEnumerable<GridSpalte<TransferangebotModel>> Angebotsliste(bool eingehend) => eingehend
        ? new[] { AngebotSpieler, AngebotPosition, AngebotStaerke, AngebotVerein(true), AngebotAbloese, AngebotGehalt }
        : new[] { AngebotSpieler, AngebotPosition, AngebotStaerke, AngebotVerein(false), AngebotAbloese, AngebotStatus };
}
