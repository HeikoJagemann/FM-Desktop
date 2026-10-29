#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Ein Spieler in der Spielfeldansicht - Angaben, die sich nicht ändern.</summary>
public class SpielfeldSpieler
{
    public long   SpielerId { get; set; }
    public string Name      { get; set; } = "";
    public string Slot      { get; set; } = "";
    public bool   Heim      { get; set; }
    public bool   Torwart   { get; set; }

    /// <summary>Auf dem Platz ist kein Raum für Vornamen.</summary>
    public string Nachname
    {
        get
        {
            int leer = Name.LastIndexOf(' ');
            return leer >= 0 ? Name[(leer + 1)..] : Name;
        }
    }
}

/// <summary>
/// Eine Momentaufnahme des Spielfelds - ein Takt der Engine.
///
/// <para>Die Positionen kommen als zwei Zahlenfelder statt als Liste von Objekten. Bei zehn
/// Aufnahmen je Sekunde und zweiundzwanzig Spielern spart das über eine Partie einige Megabyte,
/// und gelesen werden sie ohnehin nur der Reihe nach.</para>
/// </summary>
public class SpielfeldTick
{
    /// <summary>Spieluhr in Sekunden - läuft durch und ist die Zeitachse der Wiedergabe.</summary>
    public double   Zeit      { get; set; }

    /// <summary>Zeit auf der Anzeigetafel; die zweite Halbzeit fängt wieder bei 45:00 an.</summary>
    public double   Az        { get; set; }

    /// <summary>Anliegende Spielfortsetzung als Ordinalzahl, oder -1 beim rollenden Ball.</summary>
    public int      St        { get; set; } = -1;

    /// <summary>
    /// Der Wert von <see cref="St"/> für die Halbzeitpause.
    ///
    /// <para>Die Ordinalzahl des Java-Enums <c>Standard</c>: ANSTOSS, HALBZEIT, EINWURF, ...
    /// Wird dort umsortiert, muss diese Zahl mit - der Vertrag steht in
    /// <c>SpielfeldWireTest</c>.</para>
    /// </summary>
    public const int HalbzeitStandard = 1;

    public double   BallX     { get; set; }
    public double   BallY     { get; set; }
    public double   BallHoehe { get; set; }

    /// <summary>Platz des Ballführenden, oder -1 bei losem Ball.</summary>
    public int      Besitzer  { get; set; }

    public double[] X { get; set; } = System.Array.Empty<double>();
    public double[] Y { get; set; } = System.Array.Empty<double>();
}

/// <summary>
/// Ein Stück Partie: die nächsten Sekunden, fertig gerechnet.
///
/// <para>Der Server rechnet schneller als die Uhr und liefert im Voraus; der Client spielt das
/// Paket im Wanduhrtakt ab. <see cref="Aufstellung"/> kommt nur im ersten Paket.</para>
/// </summary>
public class SpielfeldPaket
{
    public string? HeimVerein { get; set; }
    public string? GastVerein { get; set; }
    public int     HeimTore   { get; set; }
    public int     GastTore   { get; set; }
    public double  Zeit       { get; set; }
    public bool    Beendet    { get; set; }

    /// <summary>Anliegende Spielfortsetzung, oder <c>null</c>, solange gespielt wird.</summary>
    public string? Standard   { get; set; }

    /// <summary>1 oder 2.</summary>
    public int     Halbzeit   { get; set; } = 1;

    /// <summary>Spielrichtung der Heimmannschaft - dreht sich zur Halbzeit um.</summary>
    public bool    HeimNachRechts { get; set; } = true;

    /// <summary>Nur das, was seit dem letzten Paket geschehen ist.</summary>
    public List<SpielfeldVorfall>  Vorfaelle   { get; set; } = new();

    public List<SpielfeldSpieler>? Aufstellung { get; set; }
    public List<SpielfeldTick>     Ticks       { get; set; } = new();
}

/// <summary>
/// Eine Zeile des Spielverlaufs, wie die Engine sie meldet.
///
/// <para>Tore, Karten, Halbzeit und Abpfiff kommen fertig vom Server - der Client entscheidet
/// nur noch über Symbol und Farbe. Vorher stellte er den Verlauf aus dem Zustand der alten
/// Engine zusammen, was nicht zu dem passte, was auf dem Platz zu sehen war.</para>
/// </summary>
public class SpielfeldVorfall
{
    public int     Minute  { get; set; }

    /// <summary>TOR, EIGENTOR, GELB, GELBROT, ROT, HALBZEIT oder ABPFIFF.</summary>
    public string  Art     { get; set; } = "";

    public string? Name    { get; set; }
    public string? Vorlage { get; set; }
    public bool    Heim    { get; set; }
}
