#nullable enable
namespace FMDesktop.Models;

public class TabellenEintrag
{
    public int     Platz         { get; set; }
    public Verein? Verein        { get; set; }
    public int     Spiele        { get; set; }
    public int     Siege         { get; set; }
    public int     Unentschieden { get; set; }
    public int     Niederlagen   { get; set; }
    public int     Tore          { get; set; }
    public int     Gegentore     { get; set; }
    public int     Tordifferenz  { get; set; }
    public int     Punkte        { get; set; }
}
