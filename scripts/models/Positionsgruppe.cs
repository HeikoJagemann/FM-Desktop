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

    /// <summary>
    /// Rangfolge einer Position von hinten nach vorn: Tor, Abwehr, Mittelfeld, Sturm.
    ///
    /// <para>Entspricht der Nummerierung des Enums <c>Position</c> im Backend. Alphabetisch zu
    /// sortieren wäre falsch - dann stünde der Stürmer vor dem Torwart.</para>
    /// </summary>
    public static int Rang(string? position) => position switch
    {
        "TW" => 1,
        "IV" => 2, "LV" => 3, "RV" => 4,
        "DM" => 5, "ZM" => 6, "LM" => 7, "RM" => 8, "OM" => 9,
        "RA" => 10, "LA" => 11, "HS" => 12, "ST" => 13,
        _    => 99,
    };
}
