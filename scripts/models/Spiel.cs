#nullable enable
namespace FMDesktop.Models;

public class Spiel
{
    public long    Id          { get; set; }
    public int     Spieltag    { get; set; }
    public int     Saison      { get; set; }
    public Verein? HeimVerein  { get; set; }
    public Verein? GastVerein  { get; set; }
    public int?    HeimTore    { get; set; }
    public int?    GastTore    { get; set; }
    public bool    Gespielt    { get; set; }

    public string Ergebnis => Gespielt
        ? $"{HeimTore}:{GastTore}"
        : "–:–";
}
