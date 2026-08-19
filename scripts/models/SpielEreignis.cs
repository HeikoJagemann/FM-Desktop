#nullable enable
namespace FMDesktop.Models;

/// <summary>Eine Zeile im Spielbericht.</summary>
public class SpielEreignis
{
    public int     Minute            { get; set; }
    public string  Typ               { get; set; } = "";
    public long?   SpielerId         { get; set; }
    public string? SpielerName       { get; set; }
    public long?   AssistSpielerId   { get; set; }
    public string? AssistSpielerName { get; set; }
    public long?   VereinId          { get; set; }
    public string? Beschreibung      { get; set; }

    public string Symbol => Typ switch
    {
        "TOR"             => "⚽",
        "GELBE_KARTE"     => "🟨",
        "GELB_ROTE_KARTE" => "🟨🟥",
        "ROTE_KARTE"      => "🟥",
        _                 => "•",
    };

    public bool IstTor => Typ == "TOR";
}
