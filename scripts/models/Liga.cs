#nullable enable
namespace FMDesktop.Models;

public class Liga
{
    public long   Id         { get; set; }
    public string Name       { get; set; } = "";
    public int    Stufe      { get; set; }
    public string? Region    { get; set; }
    public int    MinStaerke { get; set; }
    public int    MaxStaerke { get; set; }

    /// <summary>UEFA-Kuerzel des Verbands, etwa "GER" oder "ENG".</summary>
    public string Land       { get; set; } = "GER";

    /// <summary>Eine Liga ohne Spielbetrieb - die zweite Liga im Ausland, die Verbandsligen.</summary>
    public bool   Ruhend     { get; set; }

    /// <summary>Deutscher Landesname, etwa "England".</summary>
    public string LandName   { get; set; } = "Deutschland";
}
