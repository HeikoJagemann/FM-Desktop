#nullable enable
using System;
using System.Globalization;

namespace FMDesktop.Models;

/// <summary>Wo die Spielzeit gerade steht.</summary>
public class KalenderStand
{
    public DateOnly  Datum          { get; set; }
    public string    Wochentag      { get; set; } = "";
    public int       Saison         { get; set; }
    public string    Phase          { get; set; } = "";
    public DateOnly  SaisonStart    { get; set; }
    public DateOnly  ErsterSpieltag { get; set; }
    public DateOnly? WinterpauseVon { get; set; }
    public DateOnly? WinterpauseBis { get; set; }
    public DateOnly? SaisonEnde     { get; set; }
    public Termin?   NaechstesSpiel { get; set; }

    /// <summary>Spielzeit als 2024/25 - ein Jahr allein waere missverständlich.</summary>
    public string SaisonText => $"{Saison}/{(Saison + 1) % 100:00}";

    public string DatumText => $"{Wochentag} {Datum.Day}. {Monatsname(Datum.Month)} {Datum.Year}";

    public static string Monatsname(int monat) =>
        CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.GetMonthName(monat);
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
