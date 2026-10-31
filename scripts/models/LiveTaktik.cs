#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>
/// Taktik und Plätze der eigenen Mannschaft in der laufenden Partie (GET/POST spielfeld/taktik).
/// Zum Umstellen genügen Taktik und je Platz Spieler und Stelle - Slot, Position und Formation
/// benennt der Server neu.
/// </summary>
public class LiveTaktik
{
    public TaktikModel?    Taktik    { get; set; }
    public string          Formation { get; set; } = "";
    public List<LivePlatz> Plaetze   { get; set; } = new();
}

/// <summary>Ein Spieler auf dem Platz und seine Stelle (normiert: X vom eigenen Tor, Y von links).</summary>
public class LivePlatz
{
    public long   SpielerId { get; set; }
    public string Name      { get; set; } = "";
    public string Slot      { get; set; } = "";
    public string Position  { get; set; } = "";
    public double X         { get; set; }
    public double Y         { get; set; }
    public bool   Torwart   { get; set; }

    /// <summary>Seine Stärke je Position - für die Anzeige beim Ziehen.</summary>
    public Dictionary<string, int>? Staerken { get; set; }
}
