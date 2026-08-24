#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FMDesktop.Models;

/// <summary>
/// Übersetzt technische Slot-Namen in lesbare Positionsbezeichnungen.
///
/// <para>Gespeichert und übertragen wird weiter "ZM_1" - das ist der Schlüssel, unter dem eine
/// Aufstellung abgelegt ist. Angezeigt wird "LZM": Ein Manager denkt in linkem und rechtem
/// Mittelfeldspieler, nicht in durchnummerierten Plätzen.</para>
///
/// <para>Ob aus "_2" ein "R" wird oder der blanke Positionsname, hängt davon ab, wie oft die
/// Position in der Formation vorkommt: Bei zweien sind es links und rechts, bei dreien liegt
/// einer in der Mitte.</para>
/// </summary>
public static class SlotBezeichnung
{
    /// <summary>Positionsname ohne den _n-Zusatz.</summary>
    public static string Basis(string slot)
    {
        int trenner = slot.LastIndexOf('_');
        return trenner > 0 ? slot[..trenner] : slot;
    }

    private static int Nummer(string slot)
    {
        int trenner = slot.LastIndexOf('_');
        return trenner > 0 && int.TryParse(slot[(trenner + 1)..], out int n) ? n : 1;
    }

    /// <param name="alleSlots">alle Slots derselben Aufstellung - sie legen fest, wie viele
    /// Plätze es je Position gibt</param>
    public static string Fuer(string slot, IEnumerable<string> alleSlots)
    {
        if (string.IsNullOrEmpty(slot)) return "";

        string basis = Basis(slot);
        int anzahl = alleSlots.Count(s => Basis(s) == basis);
        return Fuer(slot, anzahl);
    }

    public static string Fuer(string slot, int anzahlGleicherPosition)
    {
        if (string.IsNullOrEmpty(slot)) return "";

        string basis = Basis(slot);
        if (anzahlGleicherPosition <= 1) return basis;

        int nummer = Nummer(slot);
        return anzahlGleicherPosition switch
        {
            2 => nummer == 1 ? "L" + basis : "R" + basis,
            // Drei Plätze: einer links, einer zentral, einer rechts.
            3 => nummer switch { 1 => "L" + basis, 3 => "R" + basis, _ => basis },
            _ => basis + nummer,
        };
    }
}
