#nullable enable
namespace FMDesktop.Models;

/// <summary>Ein Eintrag der Torschuetzenliste (GET spiel/liga/{id}/torjaeger).</summary>
public class Torjaeger
{
    public long    SpielerId { get; set; }
    public string  Name      { get; set; } = "";
    public long?   VereinId  { get; set; }
    public string? Verein    { get; set; }
    public string? Position  { get; set; }
    public int     Tore      { get; set; }
    public int     Vorlagen  { get; set; }
    public int     Spiele    { get; set; }
}
