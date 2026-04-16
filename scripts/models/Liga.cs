namespace FMDesktop.Models;

public class Liga
{
    public long   Id         { get; set; }
    public string Name       { get; set; } = "";
    public int    Stufe      { get; set; }
    public string? Region    { get; set; }
    public int    MinStaerke { get; set; }
    public int    MaxStaerke { get; set; }
}
