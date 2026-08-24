#nullable enable
namespace FMDesktop.Models;

/// <summary>
/// Grobe Einordnung eines Spielers für Darstellung und Sortierung.
/// Die Reihenfolge bestimmt zugleich die Sortierreihenfolge in den Listen.
/// </summary>
public enum Positionsgruppe
{
    Tor,
    Abwehr,
    Mittelfeld,
    Sturm,
}

public static class PositionsgruppeHelfer
{
    /// <summary>Gruppe zu einem Positionskürzel wie "IV" oder "ST".</summary>
    public static Positionsgruppe Von(string? position) => position switch
    {
        "TW"                                 => Positionsgruppe.Tor,
        "IV" or "LV" or "RV"                 => Positionsgruppe.Abwehr,
        "DM" or "ZM" or "LM" or "RM" or "OM" => Positionsgruppe.Mittelfeld,
        _                                    => Positionsgruppe.Sturm,
    };
}
