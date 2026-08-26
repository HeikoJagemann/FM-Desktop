#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using FMDesktop.Models;

namespace FMDesktop.UI.Common;

/// <summary>
/// Der Spaltenkatalog für Spielerlisten. Jede wiederkehrende Spalte steht hier genau einmal -
/// mit Text, Farbe, Mouseover und Sortierung.
///
/// <para>Vorher war das über die Ansichten verteilt, mit dem Ergebnis, dass dieselbe Spalte je
/// nach Ansicht anders aussah: Die Stärke hatte in vier Ansichten vier verschiedene Verhalten,
/// und in der Trainingsansicht wurde ihr Mouseover von einer Schleife wieder überschrieben.</para>
/// </summary>
public static class SpielerSpalten
{
    // ── Einzelne Spalten ─────────────────────────────────────────────────────

    /// <summary>Der Name trägt den Hinweis, worauf es auf seiner Position ankommt.</summary>
    public static GridSpalte<Spieler> Name => new()
    {
        Titel = "Name", Breite = 140, Expand = true, ExpandGewicht = 3,
        Text       = s => s.Name,
        Tooltip    = s => $"{s.Name} ({s.HauptPosition})\n{s.AttributrollenText}",
        Sortierung = s => s.Name,
    };

    public static GridSpalte<Spieler> Position => new()
    {
        Titel = "Pos", Breite = 46,
        Text       = s => s.HauptPosition,
        Farbe      = s => FmTheme.TextFuerGruppe(s.Gruppe),
        Tooltip    = s => $"Stärkste Position laut Fähigkeiten.\nNominell aufgestellt als: {s.Position}",
        // Von hinten nach vorn, nicht alphabetisch - sonst stünde der Stürmer vor dem Torwart.
        Sortierung = s => PositionsgruppeHelfer.Rang(s.HauptPosition),
    };

    /// <summary>Beste Positionen mit Stärke; der Mouseover schlüsselt jede davon auf.</summary>
    public static GridSpalte<Spieler> Staerke => new()
    {
        Titel = "Stärken", Breite = 160, Expand = true, ExpandGewicht = 2,
        Text       = s => s.Top3PositionenText,
        Tooltip    = s => s.Top3PositionenErklaerung,
        Farbe      = s => StaerkeFarbe(s.BestPositionStaerke),
        Sortierung = s => -s.BestPositionStaerke,
    };

    /// <summary>
    /// Die Stärke mit Trendpfeil aus der Trainingswoche. Erbt denselben Mouseover wie
    /// <see cref="Staerke"/>, statt einen eigenen zu setzen.
    /// </summary>
    public static GridSpalte<Spieler> StaerkeMitTrend(Func<Spieler, int> differenz) => new()
    {
        Titel = "Stärke", Breite = 78,
        Text = s =>
        {
            int d = differenz(s);
            return d == 0 ? s.BestPositionStaerke.ToString()
                          : $"{s.BestPositionStaerke} {(d > 0 ? "▲" : "▼")}{Math.Abs(d)}";
        },
        Tooltip = s => s.BestPositionErklaerung,
        Farbe = s => differenz(s) switch
        {
            > 0 => FmTheme.Success,
            < 0 => FmTheme.Danger,
            _   => FmTheme.TextPrimary,
        },
        Sortierung = s => -s.BestPositionStaerke,
    };

    /// <summary>Roher, positionsunabhängiger Wert - so wird die Jugend bewertet.</summary>
    public static GridSpalte<Spieler> StaerkeRoh => new()
    {
        Titel = "Stärke", Breite = 64,
        Text       = s => s.Staerke.ToString(),
        Farbe      = s => StaerkeFarbe(s.Staerke),
        Tooltip    = _ => "Rohe Stärke ohne Positionsbezug - Grundlage der Talenteinschätzung "
                        + "im Jugendbereich.",
        Sortierung = s => -s.Staerke,
    };

    /// <summary>Sterne zum <em>Talent</em> - siehe auch <see cref="PotenzialSterne"/>.</summary>
    public static GridSpalte<Spieler> Talent => new()
    {
        Titel = "Talent ★", Breite = 88,
        Text       = s => s.TalentSterneText,
        Farbe      = s => s.Talent >= 80 ? FmTheme.Gold
                        : s.Talent >= 65 ? FmTheme.Success
                        : FmTheme.TextSecondary,
        Tooltip    = s => $"Talent {s.Talent} von 100 ({s.TalentSterne} von 5 Sternen).\n"
                        + "Die Skala gilt für alle Ligen: Fünf Sterne hat, wer auch international "
                        + "herausragt.",
        Sortierung = s => -s.Talent,
    };

    public static GridSpalte<Spieler> Alter => new()
    {
        Titel = "Alter", Breite = 52, Ausrichtung = HorizontalAlignment.Center,
        Text       = s => s.Alter.ToString(),
        Tooltip    = s => $"Geboren am {s.Geburtsdatum:dd.MM.yyyy}",
        Sortierung = s => s.Alter,
    };

    public static GridSpalte<Spieler> Frische => new()
    {
        Titel = "Frische", Breite = 66, Ausrichtung = HorizontalAlignment.Center,
        Text       = s => s.Frische > 0 ? $"{s.Frische} %" : "–",
        Farbe      = s => s.Frische switch
        {
            >= 85 => FmTheme.Success,
            >= 70 => FmTheme.TextPrimary,
            > 0   => FmTheme.Danger,
            _     => FmTheme.TextSecondary,
        },
        Tooltip    = s => s.Frische > 0
            ? $"Frische {s.Frische} % - Startwert der Kondition im nächsten Spiel."
            : "Nie gesetzt: Der Spieler geht mit voller Frische ins Spiel.",
        Sortierung = s => -s.Frische,
    };

    public static GridSpalte<Spieler> Wert => new()
    {
        Titel = "Marktwert", Breite = 120, Expand = true, ExpandGewicht = 2,
        Ausrichtung = HorizontalAlignment.Right,
        Text       = s => FmTheme.Geld(s.Wert),
        Sortierung = s => -s.Wert,
    };

    /// <summary>Wochengehalt; leer bei Jugendspielern, die keinen Vertrag haben.</summary>
    public static GridSpalte<Spieler> Gehalt => new()
    {
        Titel = "Gehalt/Woche", Breite = 130, Expand = true, ExpandGewicht = 2,
        Ausrichtung = HorizontalAlignment.Right,
        Text       = s => s.Gehalt > 0 ? FmTheme.Geld(s.Gehalt) : "–",
        Tooltip    = s => s.VertragBis > 0
            ? $"Vertrag bis Saison {s.VertragBis}/{s.VertragBis + 1}"
            : "Kein verwalteter Vertrag.",
        Sortierung = s => -s.Gehalt,
    };

    public static GridSpalte<Spieler> Nation => new()
    {
        Titel = "Nation", Breite = 90, Expand = true,
        Text       = s => s.Nationalitaet,
        Sortierung = s => s.Nationalitaet,
    };

    public static GridSpalte<Spieler> Kader => new()
    {
        Titel = "Kader", Breite = 72,
        Text  = s => s.Kader,
        Farbe = s => s.Kader == "Profi" ? FmTheme.TextPrimary : FmTheme.TextSecondary,
    };

    public static GridSpalte<Spieler> Potenzial => new()
    {
        Titel = "Potenzial", Breite = 80,
        Text       = s => s.PotenzialText,
        Tooltip    = s => $"Geschätzte Spanne {s.PotenzialText} - wo der Spieler einmal landen "
                        + "kann.\nDen genauen Endwert kennt kein Manager.",
        Sortierung = s => -s.PotenzialVon,
    };

    /// <summary>
    /// Sterne zum <em>Potenzial</em>. Der Titel nennt es ausdrücklich, weil die Talentspalte
    /// ebenfalls Sterne zeigt - gleiche Optik, andere Bedeutung.
    /// </summary>
    public static GridSpalte<Spieler> PotenzialSterne => new()
    {
        Titel = "Potenzial ★", Breite = 84,
        Text       = s => s.PotenzialSterneText,
        Farbe      = s => s.PotenzialSterne >= 4 ? FmTheme.Gold : FmTheme.TextSecondary,
        Tooltip    = s => $"Potenzial {s.PotenzialText} als Sterne - nicht zu verwechseln mit "
                        + $"dem Talent ({s.Talent}).",
        Sortierung = s => -s.PotenzialSterne,
    };

    // ── Zusammenstellungen ───────────────────────────────────────────────────

    public static IEnumerable<GridSpalte<Spieler>> Kaderliste => new[]
    {
        Name, Position, Staerke, Talent, Alter, Wert, Gehalt, Nation,
    };

    public static IEnumerable<GridSpalte<Spieler>> Jugendliste => new[]
    {
        Name, Position, StaerkeRoh, Talent, Alter, Nation,
    };

    public static IEnumerable<GridSpalte<Spieler>> Aufstellungsliste => new[]
    {
        Name, Position, Staerke, Talent, Alter, Frische, Kader,
    };

    public static IEnumerable<GridSpalte<Spieler>> Trainingsliste(Func<Spieler, int> differenz,
        GridSpalte<Spieler> fokus, GridSpalte<Spieler> letzteWoche) => new[]
    {
        Name, Position, Alter, StaerkeMitTrend(differenz), Potenzial, PotenzialSterne,
        Frische, fokus, letzteWoche,
    };

    // ── Gemeinsames ──────────────────────────────────────────────────────────

    /// <summary>Reihenfolge im Kader: nach Positionsgruppe, darin die stärkeren zuerst.</summary>
    public static Comparison<Spieler> NachAufstellung => (a, b) =>
    {
        int gruppe = a.Sortierung.Item1.CompareTo(b.Sortierung.Item1);
        if (gruppe != 0) return gruppe;
        int staerke = a.Sortierung.Item2.CompareTo(b.Sortierung.Item2);
        return staerke != 0 ? staerke : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
    };

    /// <summary>Reihenfolge in der Jugend: nach Positionsgruppe, darin die talentiertesten zuerst.</summary>
    public static Comparison<Spieler> NachTalent => (a, b) =>
    {
        int gruppe = a.Sortierung.Item1.CompareTo(b.Sortierung.Item1);
        if (gruppe != 0) return gruppe;
        int talent = b.Talent.CompareTo(a.Talent);
        return talent != 0 ? talent : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
    };

    /// <summary>Zeilenhintergrund nach Positionsgruppe, jede zweite Zeile aufgehellt.</summary>
    public static Color Zeilenfarbe(Spieler s, bool wechsel) => FmTheme.FuerGruppe(s.Gruppe, wechsel);

    private static Color StaerkeFarbe(int staerke) => staerke >= 70 ? FmTheme.Success
                                                    : staerke >= 50 ? FmTheme.TextPrimary
                                                    : FmTheme.TextSecondary;
}
