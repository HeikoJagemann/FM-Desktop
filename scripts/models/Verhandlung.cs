#nullable enable
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>
/// Stand einer Vertragsverhandlung (vertrag/{spielerId}/verhandlung). Beträge sind
/// Wochengehälter; ein Vertrag über n Jahre endet mit der Saison <c>Saison + n</c>.
/// </summary>
public class Verhandlung
{
    public long    SpielerId       { get; set; }
    public string  Name            { get; set; } = "";
    public string  Position        { get; set; } = "";
    public int     Alter           { get; set; }
    public int     Staerke         { get; set; }
    public int     AktuellesGehalt { get; set; }
    public int     VertragBis      { get; set; }
    public long    Marktsatz       { get; set; }
    public int     Saison          { get; set; }
    public long    Forderung       { get; set; }
    public int     Wunschlaufzeit  { get; set; }
    public int     MinLaufzeit     { get; set; }
    public int     MaxLaufzeit     { get; set; }
    public double  RestSekunden    { get; set; }
    public int     Stimmung        { get; set; }
    public int     Runde           { get; set; }
    public string  Status          { get; set; } = "";
    public List<VerhandlungsZeile> Verlauf { get; set; } = new();
    public int?    NeuesGehalt     { get; set; }
    public int?    NeuerVertragBis { get; set; }

    /// <summary>
    /// Das Transferangebot bei einem Neuzugang, sonst leer. Dann ist <see cref="Staerke"/> 0, und
    /// Gehalt und Vertrag sind die beim bisherigen Verein.
    /// </summary>
    public long?   AngebotId       { get; set; }

    public bool Laeuft => Status == "LAEUFT";

    /// <summary>Kalenderjahr, in dem ein Vertrag über so viele Jahre am 30. Juni endet.</summary>
    public int EndetIm(int laufzeit) => Saison + laufzeit + 1;
}

public class VerhandlungsZeile
{
    /// <summary>"SPIELER" oder "VEREIN".</summary>
    public string Wer    { get; set; } = "";
    public string Text   { get; set; } = "";

    /// <summary>Wochengehalt für den Platzhalter <c>{betrag}</c> im Text.</summary>
    public long?  Betrag { get; set; }
}
