#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

public class AufstellungModel
{
    public long VereinId { get; set; }
    public string Formation { get; set; } = "4-4-2";
    public Dictionary<string, long> Positionen { get; set; } = new();
    public int Gesamtstaerke { get; set; }

    /// <summary>Ersatzbank in Reihenfolge (Spieler-IDs).</summary>
    public List<long> Ersatzbank { get; set; } = new();

    /// <summary>Zulässige Bankplätze – kommt aus der Liga des Vereins.</summary>
    public int MaxErsatzbank { get; set; }

    // ── Spielbereitschaft ────────────────────────────────────────────────────

    public int          AnzahlAufgestellt { get; set; }
    public List<string> UnbesetzteSlots   { get; set; } = new();
    public bool         HatTorwart        { get; set; }
    public bool         Spielbereit       { get; set; }
    public int          MindestSpieler    { get; set; }

    /// <summary>Warntext für unvollständige Aufstellungen, oder null wenn alles besetzt ist.</summary>
    public string? Warnung
    {
        get
        {
            if (AnzahlAufgestellt == 0)
                return "Es ist keine Mannschaft aufgestellt.";

            if (!Spielbereit)
                return $"Nur {AnzahlAufgestellt} Spieler aufgestellt – für einen Anpfiff werden "
                     + $"mindestens {MindestSpieler} benötigt.";

            if (!HatTorwart)
                return "Kein Torwart aufgestellt.";

            if (UnbesetzteSlots.Count > 0)
                return $"{UnbesetzteSlots.Count} Position(en) unbesetzt: "
                     + string.Join(", ", UnbesetzteSlots);

            return null;
        }
    }
    public Dictionary<string, int> SlotStaerken { get; set; } = new();
}
