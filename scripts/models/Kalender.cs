#nullable enable
using System;
using System.Globalization;

namespace FMDesktop.Models;

/// <summary>Wo die Spielzeit gerade steht.</summary>
public class KalenderStand
{
    public DateOnly  Datum          { get; set; }
    /// <summary>Tageszeit des Standes - der Durchlauf hält auch mitten am Tag.</summary>
    public TimeOnly  Uhrzeit        { get; set; }
    public string    Wochentag      { get; set; } = "";
    public int       Saison         { get; set; }
    public string    Phase          { get; set; } = "";
    public DateOnly  SaisonStart    { get; set; }
    public DateOnly  ErsterSpieltag { get; set; }
    public DateOnly? WinterpauseVon { get; set; }
    public DateOnly? WinterpauseBis { get; set; }
    public DateOnly? SaisonEnde     { get; set; }
    public Termin?   NaechstesSpiel { get; set; }

    /// <summary>Ereignis, auf dem der Stand steht; null zwischen zwei Ereignissen.</summary>
    public Ereignis? AktuellesEreignis { get; set; }
    /// <summary>Wohin der nächste Schritt führt.</summary>
    public Ereignis? NaechstesEreignis { get; set; }

    /// <summary>Spielzeit als 2024/25 - ein Jahr allein waere missverständlich.</summary>
    public string SaisonText => $"{Saison}/{(Saison + 1) % 100:00}";

    public string DatumText => $"{Wochentag} {Datum.Day}. {Monatsname(Datum.Month)} {Datum.Year}";

    /// <summary>Datum mit Uhrzeit - seit der Durchlauf an Ereignissen hält, zählt die Tageszeit.</summary>
    public string ZeitpunktText => $"{DatumText}, {Uhrzeit:HH\\:mm}";

    public static string Monatsname(int monat) =>
        CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.GetMonthName(monat);
}

/// <summary>
/// Ein Halt auf der Zeitachse. Der Durchlauf springt von einem zum nächsten; die Beschriftung der
/// Schaltfläche kommt aus <see cref="AktionText"/>, damit Client und Backend sie nicht doppelt
/// pflegen.
/// </summary>
public class Ereignis
{
    /// <summary>WOCHENSTART, SPIEL - später auch Transfer-, Vertrags- und Sponsorverhandlungen.</summary>
    public string    Art        { get; set; } = "";
    public string    ArtAnzeige { get; set; } = "";
    public DateOnly  Datum      { get; set; }
    public TimeOnly  Uhrzeit    { get; set; }
    public string    Titel      { get; set; } = "";
    public string?   Untertitel { get; set; }
    public string    AktionText { get; set; } = "Weiter";
    public long?     SpielId    { get; set; }

    public bool IstSpiel       => Art == "SPIEL";
    public bool IstWochenstart => Art == "WOCHENSTART";

    public string ZeitText => $"{Datum:dd.MM.} {Uhrzeit:HH\\:mm}";

    /// <summary>Symbol für die Toolbar - je Ereignisart eines.</summary>
    public string Symbol => Art switch
    {
        "SPIEL"       => "⚽",
        "WOCHENSTART" => "🗓",
        _             => "•",
    };
}

/// <summary>Ein Eintrag im Kalender: eine Partie oder ein Abschnitt der Saison.</summary>
public class Termin
{
    public DateOnly  Datum        { get; set; }
    public TimeOnly? Uhrzeit      { get; set; }
    /// <summary>EIGENES_SPIEL, LIGASPIEL, VORBEREITUNG, WINTERPAUSE, SAISONENDE.</summary>
    public string    Art          { get; set; } = "";
    public string    Titel        { get; set; } = "";
    public string    Untertitel   { get; set; } = "";
    public long?     SpielId      { get; set; }
    public bool      EigenesSpiel { get; set; }
    public string?   Ergebnis     { get; set; }

    public bool IstSpiel => SpielId.HasValue;

    public string ZeitText => Uhrzeit.HasValue ? Uhrzeit.Value.ToString("HH:mm") : "";
}
