#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using FMDesktop;

namespace FMDesktop.Models;

/// <summary>Wie stark eine Fähigkeit für die Position eines Spielers zählt.</summary>
public enum Attributrolle { Primaer, Sekundaer, Nebensaechlich }

public class PositionsStaerke
{
    public string Position     { get; set; } = "";
    public int    Grundstaerke { get; set; }
    public int    Staerke      { get; set; }

    /// <summary>0 bis 100. Darunter fällt die Stärke; bei 0 bleibt dem Spieler die Hälfte.</summary>
    public int    Eingespieltheit { get; set; }

    /// <summary>Kurzzeichen für die Vertrautheit: voll, angelernt, fremd.</summary>
    public string Zeichen => Eingespieltheit >= 90 ? "" : Eingespieltheit >= 40 ? "~" : "?";

    /// <summary>Mouseover-Text: wie sich die Stärke auf dieser Position zusammensetzt.</summary>
    public string Erklaerung => StaerkeErklaerung.Basis(Position, Grundstaerke, Eingespieltheit, Staerke);
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

    /// <summary>Fähigkeiten, auf die es auf seiner Position vor allem ankommt.</summary>
    public List<string> PrimaerAttribute   { get; set; } = new();

    /// <summary>Fähigkeiten, die mitspielen, aber nicht den Ausschlag geben.</summary>
    public List<string> SekundaerAttribute { get; set; } = new();

    /// <summary>Einstufung einer Fähigkeit für diesen Spieler.</summary>
    public Attributrolle RolleVon(string anzeigename) =>
        PrimaerAttribute.Contains(anzeigename)   ? Attributrolle.Primaer
      : SekundaerAttribute.Contains(anzeigename) ? Attributrolle.Sekundaer
      : Attributrolle.Nebensaechlich;

    /// <summary>Kurztext für Mouseover: was der Spieler auf seiner Position braucht.</summary>
    public string AttributrollenText =>
        $"Primär: {string.Join(", ", PrimaerAttribute)}\n"
      + $"Sekundär: {string.Join(", ", SekundaerAttribute)}";

    /// <summary>Potenzial als Spanne - bewusst unscharf, kein Manager kennt den Endwert exakt.</summary>
    public string PotenzialText => PotenzialBis > 0 ? $"{PotenzialVon}-{PotenzialBis}" : "?";

    public string PotenzialSterneText =>
        new string('★', PotenzialSterne) + new string('☆', 5 - PotenzialSterne);

    /// <summary>Talent grob als 1 bis 5 Sterne - stand vorher dreimal wortgleich im UI.</summary>
    public int TalentSterne => Talent switch
    {
        >= 80 => 5, >= 65 => 4, >= 50 => 3, >= 35 => 2, _ => 1,
    };

    public string TalentSterneText =>
        new string('★', TalentSterne) + new string('☆', 5 - TalentSterne);

    public int BestPositionStaerke =>
        Top3Positionen?.Count > 0 ? Top3Positionen[0].Staerke : Staerke;

    /// <summary>Mouseover-Text: wie sich <see cref="BestPositionStaerke"/> zusammensetzt.</summary>
    public string BestPositionErklaerung =>
        Top3Positionen?.Count > 0 ? Top3Positionen[0].Erklaerung : "";

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

    /// <summary>
    /// Beste Positionen mit Stärke. Ein "~" oder "?" hinter der Position zeigt an, dass sie dem
    /// Spieler nur teilweise oder gar nicht vertraut ist - dort bringt er weniger auf den Platz.
    /// </summary>
    public string Top3PositionenText =>
        Top3Positionen?.Count > 0
            ? string.Join(" / ", Top3Positionen.Select(p => $"{p.Position}{p.Zeichen} {p.Staerke}"))
            : Staerke.ToString();

    /// <summary>Mouseover-Text: wie sich die Stärke auf jeder der Top-Positionen zusammensetzt.</summary>
    public string Top3PositionenErklaerung =>
        Top3Positionen?.Count > 0
            ? string.Join("\n\n", Top3Positionen.Select(p => p.Erklaerung))
            : "";
}
