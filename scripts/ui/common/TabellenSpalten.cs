#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using FMDesktop.Models;

namespace FMDesktop.UI.Common;

/// <summary>Spaltenkatalog der Ligatabelle.</summary>
public static class TabellenSpalten
{
    public static GridSpalte<TabellenEintrag> Platz => new()
    {
        Titel = "#", Breite = 40, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => e.Platz.ToString(),
        Sortierung = e => e.Platz,
    };

    public static GridSpalte<TabellenEintrag> Verein => new()
    {
        Titel = "Verein", Breite = 180, Expand = true, ExpandGewicht = 3,
        Text       = e => e.Verein?.Name ?? "",
        Sortierung = e => e.Verein?.Name ?? "",
    };

    public static GridSpalte<TabellenEintrag> Spiele => new()
    {
        Titel = "Sp", Breite = 44, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => e.Spiele.ToString(),
        Tooltip    = _ => "Ausgetragene Spiele",
        Sortierung = e => -e.Spiele,
    };

    public static GridSpalte<TabellenEintrag> Siege => new()
    {
        Titel = "G", Breite = 40, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => e.Siege.ToString(),
        Tooltip    = _ => "Gewonnen",
        Sortierung = e => -e.Siege,
    };

    public static GridSpalte<TabellenEintrag> Unentschieden => new()
    {
        Titel = "U", Breite = 40, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => e.Unentschieden.ToString(),
        Tooltip    = _ => "Unentschieden",
        Sortierung = e => -e.Unentschieden,
    };

    public static GridSpalte<TabellenEintrag> Niederlagen => new()
    {
        Titel = "N", Breite = 40, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => e.Niederlagen.ToString(),
        Tooltip    = _ => "Verloren",
        Sortierung = e => -e.Niederlagen,
    };

    public static GridSpalte<TabellenEintrag> Tore => new()
    {
        Titel = "Tore", Breite = 72, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => $"{e.Tore}:{e.Gegentore}",
        Tooltip    = e => $"{e.Tore} erzielt, {e.Gegentore} kassiert",
        Sortierung = e => -e.Tore,
    };

    public static GridSpalte<TabellenEintrag> Tordifferenz => new()
    {
        Titel = "+/-", Breite = 48, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => (e.Tordifferenz >= 0 ? "+" : "") + e.Tordifferenz,
        Farbe      = e => e.Tordifferenz > 0 ? FmTheme.Success
                        : e.Tordifferenz < 0 ? FmTheme.Danger
                        : FmTheme.TextSecondary,
        Sortierung = e => -e.Tordifferenz,
    };

    public static GridSpalte<TabellenEintrag> Punkte => new()
    {
        Titel = "Pkt", Breite = 48, Ausrichtung = HorizontalAlignment.Center,
        Text       = e => e.Punkte.ToString(),
        Sortierung = e => -e.Punkte,
    };

    public static IEnumerable<GridSpalte<TabellenEintrag>> Ligatabelle => new[]
    {
        Platz, Verein, Spiele, Siege, Unentschieden, Niederlagen, Tore, Tordifferenz, Punkte,
    };

    /// <summary>
    /// Zeilenfarbe: der eigene Verein hervorgehoben, Auf- und Abstiegsplätze angedeutet.
    /// </summary>
    public static Func<TabellenEintrag, bool, Color> Zeilenfarbe(long eigenerVerein, int anzahl) =>
        (e, wechsel) =>
        {
            if (e.Verein?.Id == eigenerVerein) return FmTheme.Accent.Darkened(0.55f);
            if (e.Platz <= 2)                  return FmTheme.GruppeMittelfeld;
            if (anzahl - e.Platz < 2)          return FmTheme.GruppeSturm;
            return wechsel ? FmTheme.RowAlt : FmTheme.BgPanel;
        };
}
