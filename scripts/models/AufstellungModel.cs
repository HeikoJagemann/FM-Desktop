using System.Collections.Generic;

namespace FMDesktop.Models;

public class AufstellungModel
{
    public long VereinId { get; set; }
    public string Formation { get; set; } = "4-4-2";
    public Dictionary<string, long> Positionen { get; set; } = new();
    public int Gesamtstaerke { get; set; }
    public Dictionary<string, int> SlotStaerken { get; set; } = new();
}
