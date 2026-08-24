#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using FMDesktop;

namespace FMDesktop.Models;

public class PositionsStaerke
{
    public string Position { get; set; } = "";
    public int    Staerke  { get; set; }
}


public class Spieler
{
    public long     Id             { get; set; }
    public string   Name           { get; set; } = "";
    public DateOnly Geburtsdatum   { get; set; }
    public string   Nationalitaet  { get; set; } = "";

    /// <summary>
    /// Alter zum aktuellen Spieldatum, nicht zum heutigen Kalendertag: Ein Spieler altert mit den
    /// Saisons der Spielwelt.
    /// </summary>
    public int Alter
    {
        get
        {
            var heute = GameState.Instance?.Spieldatum ?? DateOnly.FromDateTime(DateTime.Today);
            int alter = heute.Year - Geburtsdatum.Year;
            if (Geburtsdatum.AddYears(alter) > heute) alter--;
            return alter;
        }
    }
    public string Position             { get; set; } = "";
    public string Kader                { get; set; } = "";
    public int    Staerke              { get; set; }
    public int    Talent               { get; set; }
    public int    Wert                 { get; set; }
    // Technisch
    public int    Pass                 { get; set; }
    public int    Ballkontrolle        { get; set; }
    public int    Schusstechnik        { get; set; }
    public int    Schussstaerke        { get; set; }
    public int    Schnelligkeit        { get; set; }
    public int    Ausdauer             { get; set; }
    public int    Stellungsspiel       { get; set; }
    public int    Entscheidungen       { get; set; }
    public int    Kopfball             { get; set; }
    public int    Zweikampf            { get; set; }
    public int    Dribbling            { get; set; }
    public int    LinkerFuss           { get; set; }
    public int    RechterFuss          { get; set; }
    public int    Fuehrungsqualitaet   { get; set; }
    public int    Disziplin            { get; set; }
    // Torwart
    public int    TalentTW              { get; set; }
    public int    Strafraumbeherrschung { get; set; }
    public int    Fangsicherheit        { get; set; }
    public int    Reflexe               { get; set; }
    public int    SpieleInSaison        { get; set; }

    // Training
    /// <summary>Frische zwischen den Spielen (0-100); Startwert der Kondition im nächsten Spiel.</summary>
    public int    Frische               { get; set; }
    /// <summary>Untere Grenze der Potenzialschätzung. Den exakten Wert liefert das Backend nie.</summary>
    public int    PotenzialVon          { get; set; }
    /// <summary>Obere Grenze der Potenzialschätzung.</summary>
    public int    PotenzialBis          { get; set; }
    public int    PotenzialSterne       { get; set; }
    /// <summary>Persönlicher Trainingsschwerpunkt; null, wenn nur im Team trainiert wird.</summary>
    public string? IndividualFokus      { get; set; }

    public List<PositionsStaerke>? Top3Positionen { get; set; }

    /// <summary>Potenzial als Spanne - bewusst unscharf, kein Manager kennt den Endwert exakt.</summary>
    public string PotenzialText => PotenzialBis > 0 ? $"{PotenzialVon}-{PotenzialBis}" : "?";

    public string PotenzialSterneText =>
        new string('★', PotenzialSterne) + new string('☆', 5 - PotenzialSterne);

    public int BestPositionStaerke =>
        Top3Positionen?.Count > 0 ? Top3Positionen[0].Staerke : Staerke;

    /// <summary>
    /// Nominal wird nur zwischen Torwart und Feldspieler unterschieden – die tatsächliche
    /// Position eines Feldspielers ergibt sich aus seinen Einzelfähigkeiten.
    /// </summary>
    public bool IstTorwart => Position == "TW";

    /// <summary>Stärkste Position laut Fähigkeiten; für Torhüter immer TW.</summary>
    public string HauptPosition => IstTorwart
        ? "TW"
        : (Top3Positionen?.Count > 0 ? Top3Positionen[0].Position : Position);

    public Positionsgruppe Gruppe => IstTorwart
        ? Positionsgruppe.Tor
        : HauptPosition switch
        {
            "IV" or "LV" or "RV"                 => Positionsgruppe.Abwehr,
            "DM" or "ZM" or "LM" or "RM" or "OM" => Positionsgruppe.Mittelfeld,
            _                                    => Positionsgruppe.Sturm,
        };

    /// <summary>Sortierschlüssel: Gruppe, darin die stärkeren Spieler zuerst.</summary>
    public (int, int) Sortierung => ((int)Gruppe, -BestPositionStaerke);

    public string Top3PositionenText =>
        Top3Positionen?.Count > 0
            ? string.Join(" / ", Top3Positionen.Select(p => $"{p.Position} {p.Staerke}"))
            : Staerke.ToString();
}
