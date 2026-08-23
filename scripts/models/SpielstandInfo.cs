#nullable enable
namespace FMDesktop.Models;

/// <summary>Ein gespeichertes Spiel (GET schemas/saves/uebersicht).</summary>
public class SpielstandInfo
{
    public string  Schema            { get; set; } = "";
    public string  Anzeigename       { get; set; } = "";
    public long?   VereinId          { get; set; }
    public string? VereinName        { get; set; }
    public long?   LigaId            { get; set; }
    public string? LigaName          { get; set; }
    public int?    Saison            { get; set; }
    public int?    NaechsterSpieltag { get; set; }
    public string? AngelegtAm        { get; set; }

    /// <summary>Ob der Spielstand geladen werden kann – ohne Verein fehlt die Zuordnung.</summary>
    public bool Ladbar => VereinId is > 0;

    public string Titel => VereinName ?? Anzeigename;

    public string Untertitel
    {
        get
        {
            if (!Ladbar) return "Kein Verein hinterlegt – nicht ladbar";

            var teile = new System.Collections.Generic.List<string>();
            if (LigaName != null) teile.Add(LigaName);
            if (Saison.HasValue)
                teile.Add(NaechsterSpieltag.HasValue
                    ? $"Saison {Saison} · Spieltag {NaechsterSpieltag}"
                    : $"Saison {Saison} · beendet");
            if (AngelegtAm != null && AngelegtAm.Length >= 16)
                teile.Add("angelegt " + AngelegtAm[..16].Replace('T', ' '));

            return string.Join("   ·   ", teile);
        }
    }
}
