#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

public class AufstellungModel
{
    public long VereinId { get; set; }
    public string Formation { get; set; } = "4-4-2";
    public Dictionary<string, long> Positionen { get; set; } = new();
    /// <summary>
    /// Durchschnittliche Stärke über alle Positionen der Formation – unbesetzte zählen 0.
    /// </summary>
    public double Gesamtstaerke { get; set; }

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
    /// <summary>Reines Können je Slot, ohne Eingespieltheit - für die Stärke-Aufschlüsselung.</summary>
    public Dictionary<string, int> SlotGrundstaerken { get; set; } = new();
    /// <summary>Eingespieltheit (0-100) je Slot - für die Stärke-Aufschlüsselung.</summary>
    public Dictionary<string, int> SlotEingespieltheit { get; set; } = new();

    /// <summary>Mouseover-Text: wie sich die Stärke auf diesem Slot zusammensetzt.</summary>
    public string SlotErklaerung(string slot, string position)
    {
        if (!SlotStaerken.TryGetValue(slot, out int staerke)) return "";
        SlotGrundstaerken.TryGetValue(slot, out int grundstaerke);
        SlotEingespieltheit.TryGetValue(slot, out int eingespieltheit);
        return StaerkeErklaerung.Basis(position, grundstaerke, eingespieltheit, staerke);
    }
}
