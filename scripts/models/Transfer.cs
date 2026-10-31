#nullable enable
using System;
using System.Collections.Generic;

namespace FMDesktop.Models;

/// <summary>Ob gerade gewechselt werden darf (GET transfer/fenster).</summary>
public class TransferfensterModel
{
    public bool   Offen   { get; set; }
    public string Hinweis { get; set; } = "";
    public int    Saison  { get; set; }
}

/// <summary>
/// Ein fremder Spieler auf der Transferliste oder in einem Scoutbericht (GET transfer/liste,
/// GET scouting/berichte).
///
/// <para>Stärke und Talent sind Spannen: Genau kennt der Verein nur seine eigenen Spieler. Ein
/// Talent von 0 bis 0 heißt, der Spieler wurde noch nie beobachtet.</para>
/// </summary>
public class TransferSpieler
{
    public long      SpielerId        { get; set; }
    public string    Name             { get; set; } = "";
    public string    Position         { get; set; } = "";
    public bool      Genau            { get; set; }
    public int       StaerkeVon       { get; set; }
    public int       StaerkeBis       { get; set; }
    public int       TalentVon        { get; set; }
    public int       TalentBis        { get; set; }
    public int       Beobachtungen    { get; set; }
    public DateOnly? Zuletzt          { get; set; }

    /// <summary>
    /// Wie weit der Spieler gescoutet ist, 0 bis 100 - bezogen auf das, was der beste bisherige
    /// Beobachter herausfinden kann. Eigene Spieler 100.
    /// </summary>
    public int       Kenntnis         { get; set; }

    /// <summary>Qualität dieses besten Beobachters; 0 ohne Beobachtung.</summary>
    public int       Beobachter       { get; set; }
    public int       Alter            { get; set; }
    public int       Wert             { get; set; }
    public int       VertragBis       { get; set; }
    public long      VereinId         { get; set; }
    public string    Verein           { get; set; } = "";
    public string    Liga             { get; set; } = "";
    public bool      AufTransferliste { get; set; }

    /// <summary>Ausgeschriebener Preis; 0, wenn der Spieler nicht zum Verkauf steht.</summary>
    public long      Preis            { get; set; }
    public long      Gehaltsforderung { get; set; }

    /// <summary>Ob der Kontostand den Preis deckt - ohne Preis den Marktwert.</summary>
    public bool      Bezahlbar        { get; set; }

    public string VertragText => VertragBis > 0 ? $"bis {VertragBis}/{VertragBis + 1}" : "–";
    public string StaerkeText => Spanne(StaerkeVon, StaerkeBis);
    public string TalentText  => TalentBis == 0 ? "?" : SterneSpanne(TalentVon, TalentBis);
    public int    StaerkeMitte => (StaerkeVon + StaerkeBis) / 2;

    /// <summary>Was ein Angebot ohne eigene Angabe bietet: der Preis, sonst der Marktwert.</summary>
    public long   Vorschlag => Preis > 0 ? Preis : Wert;

    public static string Spanne(int von, int bis) => von == bis ? von.ToString() : $"{von}–{bis}";

    /// <summary>
    /// Eine Talentschätzung als Sterne: eindeutig volle fünf Felder, sonst die Spanne der Sterne
    /// ("★★–★★★★"). Die Zahl selbst zeigt das Spiel nicht.
    /// </summary>
    public static string SterneSpanne(int von, int bis)
    {
        int a = Spieler.Sterne(von), b = Spieler.Sterne(bis);
        return a == b ? Spieler.SterneText(a) : new string('★', a) + "–" + new string('★', b);
    }

    /// <summary>Der Scoutingfortschritt als kleiner Balken aus fünf Feldern samt Prozentzahl.</summary>
    public string KenntnisText
    {
        get
        {
            int voll = (int)Math.Round(Kenntnis / 20.0);
            return new string('■', voll) + new string('□', 5 - voll) + $"  {Kenntnis} %";
        }
    }

    public string KenntnisTooltip => Beobachtungen == 0
        ? "Noch nicht beobachtet - nur der öffentliche Eindruck.\nEin Scout macht Stärke und Talent genauer."
        : $"{Beobachtungen} Beobachtung(en), zuletzt {Zuletzt:dd.MM.yyyy}, bester Beobachter Qualität {Beobachter}.\n"
          + "100 % heißt: Mehr findet dieser Beobachter nicht heraus - ein schwacher Scout\n"
          + "deckt auch dann nicht alles auf, erst ein besserer sieht genauer.\n"
          + "Ein alter Bericht verliert wieder an Stand - Spieler entwickeln sich.\n"
          + "Doppelklick zeigt den Scoutbericht.";
}

/// <summary>Ein Transferangebot in beide Richtungen (GET transfer/eingehend bzw. /ausgehend).</summary>
public class TransferangebotModel
{
    public long     Id           { get; set; }
    public long     SpielerId    { get; set; }
    public string   SpielerName  { get; set; } = "";
    public string   Position     { get; set; } = "";
    public int      StaerkeVon   { get; set; }
    public int      StaerkeBis   { get; set; }
    public int      Alter        { get; set; }
    public long     VonVereinId  { get; set; }
    public string   VonVerein    { get; set; } = "";
    public long     NachVereinId { get; set; }
    public string   NachVerein   { get; set; } = "";
    public long     Abloese      { get; set; }
    public long     Gehalt       { get; set; }
    public int      VertragBis   { get; set; }
    public DateOnly Datum        { get; set; }
    public string   Status       { get; set; } = "";

    /// <summary>Warum abgelehnt wurde - vom abgebenden Verein oder vom Spieler.</summary>
    public string   Grund        { get; set; } = "";

    public string StaerkeText => TransferSpieler.Spanne(StaerkeVon, StaerkeBis);

    /// <summary>Außerhalb des Fensters angenommen - gewechselt wird, sobald es öffnet.</summary>
    public bool Vereinbart => Status == "VEREINBART";

    public string StatusText => Status switch
    {
        "OFFEN"      => "wartet auf Antwort",
        "ANGENOMMEN" => "angenommen",
        "VEREINBART" => "vereinbart - wechselt zum Fensterstart",
        "ABGELEHNT"  => "abgelehnt",
        _            => Status,
    };
}

/// <summary>Ein Scout des eigenen Vereins (GET scouting/{vereinId}).</summary>
public class ScoutModel
{
    public long      Id              { get; set; }
    public string    Name            { get; set; } = "";
    public int       Qualitaet       { get; set; }
    public int       Gehalt          { get; set; }
    public DateOnly? Eingestellt     { get; set; }
    public string?   AuftragArt      { get; set; }
    public long?     AuftragZiel     { get; set; }
    public string?   AuftragPosition { get; set; }
    public int?      AuftragMaxAlter { get; set; }

    /// <summary>Der Auftrag in Worten, leer ohne Auftrag.</summary>
    public string    Auftrag         { get; set; } = "";
}

/// <summary>Ein Bewerber auf eine Scoutstelle - wechselt monatlich.</summary>
public class ScoutBewerber
{
    public int    Nummer    { get; set; }
    public string Name      { get; set; } = "";
    public int    Qualitaet { get; set; }
    public int    Gehalt    { get; set; }
}

/// <summary>Die Scoutabteilung (GET scouting/{vereinId}).</summary>
public class ScoutingModel
{
    public List<ScoutModel>    Scouts          { get; set; } = new();
    public List<ScoutBewerber> Bewerber        { get; set; } = new();
    public int                 MaxScouts       { get; set; }
    public int                 AbfindungWochen { get; set; }
}
