namespace FMDesktop.Models;

/// <summary>Wo die Saison gerade steht (GET spiel/saison).</summary>
public class SaisonStand
{
    public int  Saison            { get; set; }
    public int  NaechsterSpieltag { get; set; }
    public int  MaxSpieltag       { get; set; }
    public long OffeneSpiele      { get; set; }
    public bool SaisonBeendet     { get; set; }

    public string Anzeige => SaisonBeendet
        ? $"Saison {Saison} – beendet"
        : $"Saison {Saison} · Spieltag {NaechsterSpieltag}/{MaxSpieltag}";
}
