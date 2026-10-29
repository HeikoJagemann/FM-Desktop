#nullable enable
using Godot;
using System.Collections.Generic;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Spielt die Tick-Pakete des Servers im Wanduhrtakt ab.
///
/// <para>Die Engine rechnet mit zehn Bildern je Sekunde, der Bildschirm zeigt sechzig. Dazwischen
/// wird interpoliert - sonst ruckelte jeder Laufweg in Zehntelsprüngen. Der Puffer gleicht
/// zugleich die Antwortzeit des Servers aus: Solange noch Spielzeit vorrätig ist, läuft das Bild
/// weiter, auch wenn eine Anfrage gerade unterwegs ist.</para>
///
/// <para>Kein Godot-Knoten, sondern eine schlichte Klasse: Sie rechnet nur, gezeichnet wird in
/// <see cref="Spielfeld"/>.</para>
/// </summary>
public sealed class SzenenPlayer
{
    /// <summary>Ein fertig interpoliertes Bild - das, was gezeichnet wird.</summary>
    public sealed class Bild
    {
        public Vector2   Ball      { get; init; }
        public double    BallHoehe { get; init; }
        public int       Besitzer  { get; init; } = -1;
        public Vector2[] Spieler   { get; init; } = System.Array.Empty<Vector2>();

        /// <summary>Spieluhr - die durchlaufende Zeitachse.</summary>
        public double    Zeit      { get; init; }

        /// <summary>Zeit auf der Anzeigetafel; die zweite Halbzeit beginnt wieder bei 45:00.</summary>
        public double    Anzeigezeit { get; init; }

        /// <summary>Anliegende Spielfortsetzung, oder -1 beim rollenden Ball.</summary>
        public int       Standard  { get; init; } = -1;

        /// <summary>Ob gerade Halbzeitpause ist.</summary>
        public bool      Pause => Standard == SpielfeldTick.HalbzeitStandard;
    }

    private readonly List<SpielfeldTick> _puffer = new();

    /// <summary>Spieluhr der Wiedergabe, in Sekunden.</summary>
    private double _uhr;
    private bool _gestartet;

    /// <summary>Wie viel Spielzeit noch im Puffer liegt.</summary>
    public double Vorrat => _puffer.Count == 0 ? 0 : Mathf.Max(0, _puffer[^1].Zeit - _uhr);

    public bool HatBild => _puffer.Count > 0;

    public void Fuettere(SpielfeldPaket paket)
    {
        foreach (var tick in paket.Ticks)
        {
            // Doppelte und überholte Takte verwerfen - Pakete dürfen sich überlappen.
            if (_puffer.Count > 0 && tick.Zeit <= _puffer[^1].Zeit)
            {
                continue;
            }
            _puffer.Add(tick);
        }

        if (!_gestartet && _puffer.Count > 0)
        {
            _uhr = _puffer[0].Zeit;
            _gestartet = true;
        }
    }

    /// <summary>
    /// Rückt die Wiedergabe um <paramref name="delta"/> Sekunden vor.
    /// </summary>
    /// <param name="tempo">Wiedergabegeschwindigkeit - 1 ist Echtzeit.</param>
    /// <returns>das Bild zu diesem Zeitpunkt, oder <c>null</c>, wenn der Puffer leer ist</returns>
    public Bild? Ruecke(double delta, double tempo)
    {
        if (_puffer.Count == 0)
        {
            return null;
        }

        // Nur weiterlaufen, solange Vorrat da ist. Sonst steht das Bild, statt zu springen.
        if (_puffer.Count > 1)
        {
            _uhr = Mathf.Min(_uhr + delta * tempo, _puffer[^1].Zeit);
        }

        VerwerfeVergangenes();

        SpielfeldTick a = _puffer[0];
        if (_puffer.Count == 1)
        {
            return baue(a, a, 0);
        }

        SpielfeldTick b = _puffer[1];
        double spanne = b.Zeit - a.Zeit;
        double anteil = spanne <= 1e-9 ? 0 : Mathf.Clamp((_uhr - a.Zeit) / spanne, 0, 1);
        return baue(a, b, anteil);
    }

    /// <summary>Verwirft, was die Wiedergabe hinter sich gelassen hat.</summary>
    private void VerwerfeVergangenes()
    {
        while (_puffer.Count > 2 && _puffer[1].Zeit <= _uhr)
        {
            _puffer.RemoveAt(0);
        }
    }

    private static Bild baue(SpielfeldTick a, SpielfeldTick b, double anteil)
    {
        int anzahl = Mathf.Min(a.X.Length, b.X.Length);
        var spieler = new Vector2[anzahl];
        for (int i = 0; i < anzahl; i++)
        {
            spieler[i] = new Vector2(
                (float)Mathf.Lerp(a.X[i], b.X[i], anteil),
                (float)Mathf.Lerp(a.Y[i], b.Y[i], anteil));
        }

        return new Bild
        {
            Ball = new Vector2((float)Mathf.Lerp(a.BallX, b.BallX, anteil),
                               (float)Mathf.Lerp(a.BallY, b.BallY, anteil)),
            BallHoehe = Mathf.Lerp(a.BallHoehe, b.BallHoehe, anteil),
            // Der Ballbesitz wird nicht interpoliert - er gehört dem Takt, in dem man steht.
            Besitzer = anteil < 0.5 ? a.Besitzer : b.Besitzer,
            Spieler = spieler,
            Zeit = Mathf.Lerp(a.Zeit, b.Zeit, anteil),
            Anzeigezeit = Mathf.Lerp(a.Az, b.Az, anteil),
            // Wie der Ballbesitz gehört auch die Spielfortsetzung dem Takt, in dem man steht.
            Standard = anteil < 0.5 ? a.St : b.St,
        };
    }
}
