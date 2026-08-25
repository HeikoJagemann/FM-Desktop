#nullable enable
using System;
using Godot;

namespace FMDesktop.UI.Common;

/// <summary>
/// Eine Spalte in einem <see cref="FmGrid{T}"/> - Aussehen und Verhalten an einer Stelle.
///
/// <para>Der Sinn der ganzen Übung: Wer eine Spalte verwendet, bekommt automatisch ihren Text,
/// ihre Farbe, ihren Mouseover und ihre Sortierung mit. Vorher stand das in jeder Ansicht neu,
/// mit dem Ergebnis, dass dieselbe Spalte je nach Ansicht etwas anderes tat.</para>
/// </summary>
public sealed class GridSpalte<T>
{
    public string Titel  { get; init; } = "";
    public int    Breite { get; init; } = 80;

    /// <summary>Nimmt die Spalte den übrigen Platz mit ein?</summary>
    public bool Expand { get; init; }

    /// <summary>Gewicht beim Verteilen des Restplatzes; greift nur mit <see cref="Expand"/>.</summary>
    public int ExpandGewicht { get; init; } = 1;

    public HorizontalAlignment Ausrichtung { get; init; } = HorizontalAlignment.Left;

    /// <summary>Der angezeigte Zellinhalt.</summary>
    public Func<T, string> Text { get; init; } = _ => "";

    /// <summary>Textfarbe der Zelle; ohne Angabe die Standardschriftfarbe.</summary>
    public Func<T, Color>? Farbe { get; init; }

    /// <summary>Mouseover-Text der Zelle; ohne Angabe kein Tooltip.</summary>
    public Func<T, string>? Tooltip { get; init; }

    /// <summary>Sortierschlüssel bei Klick auf den Spaltenkopf; ohne Angabe nicht sortierbar.</summary>
    public Func<T, IComparable>? Sortierung { get; init; }
}
