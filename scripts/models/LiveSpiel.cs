#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace FMDesktop.Models;

/// <summary>Ein Spieler in der laufenden Bildschirmsimulation.</summary>
public class LiveSpieler
{
    public long    SpielerId   { get; set; }
    public string  Name        { get; set; } = "";
    public string  Slot        { get; set; } = "";
    public string  Position    { get; set; } = "";
    public int     Staerke     { get; set; }
    public double  Kondition   { get; set; }
    public bool    AufDemPlatz { get; set; }
    public int     Tore        { get; set; }
    public int     GelbeKarten { get; set; }
    public int     RoteKarten  { get; set; }
}

/// <summary>Zustand der laufenden Partie (GET/POST spiel/live/...).</summary>
public class LiveSpiel
{
    public long    SpielId         { get; set; }
    public int     Minute          { get; set; }
    public bool    Beendet         { get; set; }
    public long?   EigenerVereinId { get; set; }
    public long?   HeimVereinId    { get; set; }
    public string? HeimVerein      { get; set; }
    public long?   GastVereinId    { get; set; }
    public string? GastVerein      { get; set; }
    public int     HeimTore        { get; set; }
    public int     GastTore        { get; set; }
    public double  HeimStaerke     { get; set; }
    public double  GastStaerke     { get; set; }

    public List<LiveSpieler> HeimAufstellung { get; set; } = new();
    public List<LiveSpieler> GastAufstellung { get; set; } = new();

    /// <summary>Ersatzspieler des eigenen Vereins, die noch eingewechselt werden können.</summary>
    public List<LiveSpieler> EigeneBank { get; set; } = new();

    public int  WechselUebrig { get; set; }
    public int  FensterUebrig { get; set; }
    public bool DarfWechseln  { get; set; }

    public List<SpielEreignis> Ereignisse { get; set; } = new();

    public bool EigenesHeimspiel => EigenerVereinId != null && EigenerVereinId == HeimVereinId;

    /// <summary>Spieler der eigenen Mannschaft, die gerade auf dem Platz stehen.</summary>
    public List<LiveSpieler> EigeneAufDemPlatz =>
        (EigenesHeimspiel ? HeimAufstellung : GastAufstellung)
            .Where(s => s.AufDemPlatz)
            .ToList();

    public string WechselHinweis =>
        WechselUebrig <= 0 ? "Keine Wechsel mehr"
        : FensterUebrig <= 0 ? "Alle Wechselfenster verbraucht"
        : $"{WechselUebrig} Wechsel · {FensterUebrig} Fenster";
}
