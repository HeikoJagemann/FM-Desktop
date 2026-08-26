#nullable enable
namespace FMDesktop.Models;

public class StadionModel
{
    public long   Id          { get; set; }
    public string Name        { get; set; } = "";
    public int    Kapazitaet  { get; set; }
    public string Stadt       { get; set; } = "";
    public int    Ticketpreis { get; set; }
    public long?  VereinId    { get; set; }
    public string? VereinName { get; set; }
}
