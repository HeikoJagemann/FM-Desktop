#nullable enable
namespace FMDesktop.Models;

public class Verein
{
    public long   Id      { get; set; }
    public string Name    { get; set; } = "";
    public int    Staerke { get; set; }
    public Liga?  Liga    { get; set; }
}
