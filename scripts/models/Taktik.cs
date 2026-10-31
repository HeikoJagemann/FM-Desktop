#nullable enable
namespace FMDesktop.Models;

/// <summary>
/// Die Taktik des Vereins (GET/POST taktik/{vereinId}). Die Werte sind die Namen der Java-Enums;
/// was sie in der Engine bewirken, steht in <c>Taktik.java</c> und <c>AgentKonstanten</c>.
/// </summary>
public class TaktikModel
{
    public string Ausrichtung   { get; set; } = "NEUTRAL";
    public string Deckung       { get; set; } = "MANN";
    public bool   Gegenpressing { get; set; }
    public string Passrichtung  { get; set; } = "AUSGEWOGEN";
    public string Passlaenge    { get; set; } = "GEMISCHT";
}
